using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ToyConEngine
{
    // --- MONOGAME IMPLEMENTATION ---
    public class ToyConGame : Game
    {
        public static Rectangle ClientBounds { get; private set; }
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel; // Used for drawing lines and rectangles
        private SpriteFont _font;
        private List<string> _availableSounds = new List<string>();

        // Selection & Clipboard
        private List<Node> _selectedNodes = new List<Node>();
        private bool _isSelecting = false;
        private Point _selectionStart;
        private Rectangle _selectionRect;
        private List<(Node Node, Point Offset)> _clipboardNodes = new List<(Node, Point)>();
        // We need to store connections for clipboard. 
        // Since we clone nodes, we need to know which input index connects to which output index of which node index in the list.
        private class ConnectionData { public int TargetNodeIdx; public int TargetInputIdx; public int SourceNodeIdx; public int SourceOutputIdx; }
        private List<ConnectionData> _clipboardConnections = new List<ConnectionData>();

        private GraphEngine _engine;

        // Visual State
        private Dictionary<Node, Rectangle> _nodeRects = new Dictionary<Node, Rectangle>();
        private bool _isDraggingNodes = false;
        private Point _lastMousePos;
        private bool _presentationMode = false;
        private Dictionary<ScreenNode, Texture2D> _screenTextures = new Dictionary<ScreenNode, Texture2D>();
        private KeyboardState _prevKeyboardState;
        private MouseState _prevMouseState;

        private string _activeMenu = null;
        private Dictionary<string, List<(string Name, Func<Node> Factory)>> _menus;
        private Rectangle _uiBarRect = new Rectangle(0, 0, 800, 30);

        private Node _inspectedNode = null;
        private Rectangle _overlayRect;
        private double _lastClickTime;
        private double _lastRightClickTime;
        private const double DoubleClickTime = 0.3;

        private Node _connectionStartNode = null;
        private int _connectionStartIndex = -1;
        private string _inputValueBuffer = "";
        
        private const string StandaloneMagic = "TOYCON_PKG";

        public ToyConGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "ToyCon Engine - MonoGame Port";
        }

        protected override void Initialize()
        {
            _engine = new GraphEngine();

            _menus = new Dictionary<string, List<(string Name, Func<Node> Factory)>>
            {
                { "File", new List<(string, Func<Node>)> {
                    ("Save", () => { SaveLayout("design.toy"); return null; }),
                    ("Load", () => { LoadLayout("design.toy"); return null; }),
                    ("Export EXE", () => { ExportStandalone("ToyCon_Export.exe"); return null; }),
                    ("Clear", () => { _engine.Nodes.Clear(); _nodeRects.Clear(); _selectedNodes.Clear(); _inspectedNode = null; return null; })
                }},
                { "Input", new List<(string, Func<Node>)> {
                    ("Constant", () => new ConstantNode(1.0f)),
                    ("Button", () => new ButtonNode()),
                    ("Key", () => new KeyNode()),
                    ("Timer", () => new TimerNode()),
                    ("Cursor", () => new CursorNode()),
                    ("Random", () => new RandomNode())
                }},
                { "Middle", new List<(string, Func<Node>)> {
                    ("Math", () => new MathNode(MathNode.Operation.Add)),
                    ("Logic", () => new LogicNode(LogicNode.LogicType.And)),
                    ("Counter", () => new CounterNode())
                }},
                { "Output", new List<(string, Func<Node>)> {
                    ("Color", () => new ColorOutputNode()),
                    ("Beep", () => new BeepOutputNode()),
                    ("Screen", () => new ScreenNode())
                }},
                { "Import", new List<(string, Func<Node>)> {
                    ("Script", () => new ScriptImporterNode())
                }}
            };

            base.Initialize();
            
            // Check if this is a standalone build with embedded data
            if (TryLoadEmbeddedLayout()) _presentationMode = true;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Generate a 1x1 white texture for drawing primitives
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Load font - NOTE: You must add a SpriteFont named "Font" to your Content
            try
            {
                _font = Content.Load<SpriteFont>("Font");
            }
            catch
            {
                // Font or sound not found
            }

            // Scan for sounds
            var contentDir = new DirectoryInfo(Content.RootDirectory);
            if (contentDir.Exists)
            {
                foreach (var file in contentDir.GetFiles("*.xnb"))
                {
                    string assetName = Path.GetFileNameWithoutExtension(file.Name);
                    try
                    {
                        // Try to load as SoundEffect to verify type
                        Content.Load<SoundEffect>(assetName);
                        _availableSounds.Add(assetName);
                    }
                    catch { }
                }
            }
            if (_availableSounds.Count == 0) _availableSounds.Add("Beep");
        }

        protected override void Update(GameTime gameTime)
        {
            ClientBounds = Window.ClientBounds;
            var mouseState = Mouse.GetState();
            var mousePos = mouseState.Position;
            var keyboardState = Keyboard.GetState();

            // Update ButtonNodes
            foreach (var kvp in _nodeRects)
            {
                if (kvp.Key is ButtonNode btnNode)
                {
                    if (btnNode.IsToggle)
                    {
                        if (kvp.Value.Contains(mousePos) && mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                            btnNode.IsPressed = !btnNode.IsPressed;
                    }
                    else
                    {
                        btnNode.IsPressed = kvp.Value.Contains(mousePos) && mouseState.LeftButton == ButtonState.Pressed;
                    }
                }
            }

            // 1. Logic Tick
            _engine.Tick(gameTime);

            // 2. Handle Audio Outputs
            foreach (var node in _engine.Nodes)
            {
                if (node is BeepOutputNode beepNode && beepNode.ShouldPlay)
                {
                    try
                    {
                        var sfx = Content.Load<SoundEffect>(beepNode.SoundName);
                        sfx.Play(beepNode.Volume, beepNode.Pitch, 0);
                    }
                    catch { }
                }
            }

            // 3. Input Handling (Drag and Drop)
            bool clicked = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            bool rightClicked = mouseState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released;

            // Shortcuts
            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            if (IsKeyPressed(keyboardState, Keys.Delete)) DeleteSelectedNodes();
            if (IsKeyPressed(keyboardState, Keys.F5)) _presentationMode = !_presentationMode;

            if (ctrl && IsKeyPressed(keyboardState, Keys.C)) CopyNodes();
            if (ctrl && IsKeyPressed(keyboardState, Keys.V)) PasteNodes();

            if (_inspectedNode != null)
            {
                UpdateOverlay(mouseState, keyboardState, clicked);
                _prevKeyboardState = keyboardState;
                _prevMouseState = mouseState;
                _lastMousePos = mouseState.Position;
                base.Update(gameTime);
                return;
            }

            if (_presentationMode) return; // Skip UI updates in presentation mode

            bool uiCaptured = UpdateUI(mouseState);

            // Handle Connection Dragging Start
            if (!uiCaptured && clicked)
            {
                foreach (var kvp in _nodeRects)
                {
                    var node = kvp.Key;
                    for (int i = 0; i < node.Outputs.Count; i++)
                    {
                        Vector2 portPos = GetOutputPosition(node, i);
                        Rectangle portRect = new Rectangle((int)portPos.X - 6, (int)portPos.Y - 6, 12, 12);
                        if (portRect.Contains(mousePos))
                        {
                            _connectionStartNode = node;
                            _connectionStartIndex = i;
                            uiCaptured = true;
                            break;
                        }
                    }
                    if (uiCaptured) break;
                }
            }

            // Handle Connection Dragging End
            if (_connectionStartNode != null)
            {
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    foreach (var kvp in _nodeRects)
                    {
                        var node = kvp.Key;
                        for (int i = 0; i < node.Inputs.Count; i++)
                        {
                            Vector2 portPos = GetInputPosition(node, i);
                            Rectangle portRect = new Rectangle((int)portPos.X - 6, (int)portPos.Y - 6, 12, 12);
                            if (portRect.Contains(mousePos))
                            {
                                _engine.Connect(_connectionStartNode, _connectionStartIndex, node, i);
                                break;
                            }
                        }
                    }
                    _connectionStartNode = null;
                    _connectionStartIndex = -1;
                }
                uiCaptured = true;
            }

            if (!uiCaptured && clicked)
            {
                double now = gameTime.TotalGameTime.TotalSeconds;
                if (now - _lastClickTime < DoubleClickTime)
                {
                    bool doubleClickHandled = false;
                    // Check Nodes (Inspection) - Only if single node selected or all same type
                    foreach (var kvp in _nodeRects)
                    {
                        if (kvp.Value.Contains(mousePos))
                        {
                            // If we double click a node, ensure it is selected
                            if (!_selectedNodes.Contains(kvp.Key))
                            {
                                _selectedNodes.Clear();
                                _selectedNodes.Add(kvp.Key);
                            }

                            // Check if all selected nodes are same type
                            bool allSame = true;
                            Type firstType = _selectedNodes[0].GetType();
                            foreach(var n in _selectedNodes) if(n.GetType() != firstType) allSame = false;

                            if (allSame)
                            {
                                _inspectedNode = _selectedNodes[0]; // Use first as representative
                                _inputValueBuffer = "";
                                if (_inspectedNode is ConstantNode c) _inputValueBuffer = c.StoredValue.ToString();
                                if (_inspectedNode is CounterNode cnt) _inputValueBuffer = cnt.Value.ToString();
                                if (_inspectedNode is ScriptImporterNode sn) _inputValueBuffer = sn.Script;
                                doubleClickHandled = true;
                            }
                            break; 
                        }
                    }
                    // Check Wires (Deletion)
                    if (!doubleClickHandled)
                    {
                        foreach (var node in _engine.Nodes)
                        {
                            for (int i = 0; i < node.Inputs.Count; i++)
                            {
                                var input = node.Inputs[i];
                                for (int j = input.ConnectedSources.Count - 1; j >= 0; j--)
                                {
                                    var source = input.ConnectedSources[j];
                                    var startNode = source.ParentNode;
                                    int outputIndex = startNode.Outputs.IndexOf(source);
                                    Vector2 startPos = GetOutputPosition(startNode, outputIndex);
                                    Vector2 endPos = GetInputPosition(node, i);
                                    if (GetDistanceFromLineSegment(mousePos.ToVector2(), startPos, endPos) < 8f)
                                    {
                                        input.ConnectedSources.RemoveAt(j);
                                        doubleClickHandled = true;
                                        break;
                                    }
                                }
                            }
                            if (doubleClickHandled) break;
                        }
                    }
                    _lastClickTime = 0;
                }
                else
                {
                    _lastClickTime = now;
                }
            }

            // Selection and Dragging Logic
            if (!uiCaptured)
            {
                if (mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    // Clicked
                    Node clickedNode = null;
                    foreach (var kvp in _nodeRects)
                    {
                        if (kvp.Value.Contains(mousePos))
                        {
                            clickedNode = kvp.Key;
                            break;
                        }
                    }
                    if (clickedNode != null)
                    {
                        if (!_selectedNodes.Contains(clickedNode))
                        {
                            if (!ctrl) _selectedNodes.Clear();
                            _selectedNodes.Add(clickedNode);
                        }
                        else if (ctrl)
                        {
                            _selectedNodes.Remove(clickedNode);
                        }
                        _isDraggingNodes = true;
                    }
                    else
                    {
                        // Start Selection Box
                        _isSelecting = true;
                        _selectionStart = mousePos;
                        _selectionRect = new Rectangle(mousePos.X, mousePos.Y, 0, 0);
                        if (!ctrl) _selectedNodes.Clear();
                    }
                }
                else if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    // Dragging
                    if (_isDraggingNodes)
                    {
                        Point delta = mousePos - _lastMousePos;
                        foreach (var node in _selectedNodes)
                        {
                            var r = _nodeRects[node];
                            r.Location += delta;
                            _nodeRects[node] = r;
                        }
                    }
                    else if (_isSelecting)
                    {
                        int x = Math.Min(_selectionStart.X, mousePos.X);
                        int y = Math.Min(_selectionStart.Y, mousePos.Y);
                        int w = Math.Abs(_selectionStart.X - mousePos.X);
                        int h = Math.Abs(_selectionStart.Y - mousePos.Y);
                        _selectionRect = new Rectangle(x, y, w, h);
                    }
                }
                else
                {
                    // Released
                    if (_isSelecting)
                    {
                        foreach (var kvp in _nodeRects)
                        {
                            if (_selectionRect.Intersects(kvp.Value))
                            {
                                if (!_selectedNodes.Contains(kvp.Key)) _selectedNodes.Add(kvp.Key);
                            }
                        }
                        _isSelecting = false;
                        _selectionRect = Rectangle.Empty;
                    }
                    _isDraggingNodes = false;
                }
            }

            _lastMousePos = mousePos;
            _prevKeyboardState = keyboardState;
            _prevMouseState = mouseState;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 30, 30)); // Dark background

            if (_presentationMode)
            {
                GraphicsDevice.Clear(Color.Black);
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                
                var screens = _engine.Nodes.OfType<ScreenNode>().ToList();
                if (screens.Count > 0)
                {
                    // Draw the first screen node scaled to fit
                    var screen = screens[0];
                    if (!_screenTextures.ContainsKey(screen)) _screenTextures[screen] = new Texture2D(GraphicsDevice, ScreenNode.Width, ScreenNode.Height);
                    _screenTextures[screen].SetData(screen.Buffer);

                    int scale = Math.Min(ClientBounds.Width / ScreenNode.Width, ClientBounds.Height / ScreenNode.Height);
                    int w = ScreenNode.Width * scale;
                    int h = ScreenNode.Height * scale;
                    int x = (ClientBounds.Width - w) / 2;
                    int y = (ClientBounds.Height - h) / 2;
                    _spriteBatch.Draw(_screenTextures[screen], new Rectangle(x, y, w, h), Color.White);
                }
                _spriteBatch.End();
                return;
            }

            _spriteBatch.Begin();

            // Draw Wires
            foreach (var node in _engine.Nodes)
            {
                for (int i = 0; i < node.Inputs.Count; i++)
                {
                    var input = node.Inputs[i];
                    foreach (var source in input.ConnectedSources)
                    {
                        var startNode = source.ParentNode;
                        var endNode = node;

                        int outputIndex = startNode.Outputs.IndexOf(source);
                        Vector2 startPos = GetOutputPosition(startNode, outputIndex);
                        Vector2 endPos = GetInputPosition(endNode, i);

                        // Draw line from center of source to center of target
                        DrawLine(_spriteBatch, startPos, endPos, Color.Orange, 2);

                        // Draw Value
                        if (_font != null)
                        {
                            Vector2 mid = (startPos + endPos) / 2;
                            string val = source.Value.ToString("0.00");
                            _spriteBatch.DrawString(_font, val, mid - new Vector2(0, 15), Color.White);
                        }
                    }
                }
            }

            // Draw Nodes
            foreach (var kvp in _nodeRects)
            {
                var node = kvp.Key;
                var rect = kvp.Value;

                // Color code based on type
                Color color = _selectedNodes.Contains(node) ? Color.Lerp(Color.Gray, Color.White, 0.5f) : Color.Gray;
                if (node is MathNode) color = Color.RoyalBlue;
                if (node is LogicNode) color = Color.Crimson;
                if (node is ConstantNode) color = Color.ForestGreen;
                if (node is TimerNode) color = Color.MediumPurple;
                if (node is CounterNode) color = Color.DarkOrange;
                if (node is ColorOutputNode colorOutput)
                {
                    color = colorOutput.DisplayColor;
                }
                if (node is BeepOutputNode) color = Color.HotPink;
                if (node is ScreenNode screenNode)
                {
                    color = Color.Black;
                    if (!_screenTextures.ContainsKey(screenNode)) _screenTextures[screenNode] = new Texture2D(GraphicsDevice, ScreenNode.Width, ScreenNode.Height);
                    _screenTextures[screenNode].SetData(screenNode.Buffer);
                    // We'll draw the texture after the rect
                }

                if (_selectedNodes.Contains(node))
                    color = Color.Lerp(color, Color.White, 0.3f);

                _spriteBatch.Draw(_pixel, rect, color);

                // Border
                DrawHollowRect(_spriteBatch, rect, _selectedNodes.Contains(node) ? Color.Yellow : Color.White, _selectedNodes.Contains(node) ? 3 : 1);

                if (node is ScreenNode sn && _screenTextures.ContainsKey(sn))
                {
                    // Draw screen content inside node
                    _spriteBatch.Draw(_screenTextures[sn], new Rectangle(rect.Center.X - 32, rect.Center.Y - 20, 64, 64), Color.White);
                }

                // Draw Input Ports
                for (int i = 0; i < node.Inputs.Count; i++)
                {
                    Vector2 pos = GetInputPosition(node, i);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 3, (int)pos.Y - 3, 6, 6), Color.Yellow);
                }
                // Draw Output Ports
                for (int i = 0; i < node.Outputs.Count; i++)
                {
                    Vector2 pos = GetOutputPosition(node, i);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 3, (int)pos.Y - 3, 6, 6), Color.Cyan);
                }

                // Draw Label and Value
                if (_font != null)
                {
                    string label = node.Name;
                    if (node.Outputs.Count > 0) label += $"\n{node.Outputs[0].Value:0.00}";

                    Vector2 textSize = _font.MeasureString(label);
                    _spriteBatch.DrawString(_font, label, rect.Center.ToVector2() - textSize / 2, Color.White);
                }
            }

            // Draw Dragging Wire
            if (_connectionStartNode != null)
            {
                Vector2 startPos = GetOutputPosition(_connectionStartNode, _connectionStartIndex);
                Vector2 endPos = Mouse.GetState().Position.ToVector2();
                DrawLine(_spriteBatch, startPos, endPos, Color.White, 2);
            }

            // Draw Selection Box
            if (_isSelecting)
            {
                _spriteBatch.Draw(_pixel, _selectionRect, new Color(255, 255, 255, 50));
                DrawHollowRect(_spriteBatch, _selectionRect, Color.White);
            }

            DrawUI();

            DrawOverlay();

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && !_prevKeyboardState.IsKeyDown(key);
        }

        private bool UpdateUI(MouseState mouseState)
        {
            _uiBarRect.Width = Window.ClientBounds.Width;
            bool clicked = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            Point mousePos = mouseState.Position;
            bool captured = false;

            if (_uiBarRect.Contains(mousePos)) captured = true;

            if (clicked)
            {
                int x = 10;
                bool menuClicked = false;
                foreach (var category in _menus.Keys)
                {
                    Rectangle btnRect = new Rectangle(x, 0, 80, 30);
                    if (btnRect.Contains(mousePos))
                    {
                        _activeMenu = (_activeMenu == category) ? null : category;
                        menuClicked = true;
                        captured = true;
                        break;
                    }
                    x += 90;
                }

                if (!menuClicked && _activeMenu != null)
                {
                    int menuX = 10;
                    foreach (var category in _menus.Keys) { if (category == _activeMenu) break; menuX += 90; }

                    var items = _menus[_activeMenu];
                    int y = 30;
                    for (int i = 0; i < items.Count; i++)
                    {
                        Rectangle itemRect = new Rectangle(menuX, y, 120, 30);
                        if (itemRect.Contains(mousePos))
                        {
                            var n = items[i].Factory();
                            if (n != null) SpawnNode(n);
                            _activeMenu = null;
                            captured = true;
                            menuClicked = true;
                            break;
                        }
                        y += 30;
                    }
                    if (!menuClicked && !_uiBarRect.Contains(mousePos)) _activeMenu = null;
                }
                else if (!menuClicked && _activeMenu != null && !_uiBarRect.Contains(mousePos)) _activeMenu = null;
            }
            return captured;
        }

        private void DrawUI()
        {
            _spriteBatch.Draw(_pixel, _uiBarRect, new Color(40, 40, 40));
            DrawHollowRect(_spriteBatch, _uiBarRect, Color.Gray);

            int x = 10;
            foreach (var category in _menus.Keys)
            {
                Rectangle btnRect = new Rectangle(x, 0, 80, 30);
                _spriteBatch.Draw(_pixel, btnRect, (_activeMenu == category) ? Color.Gray : Color.DarkGray);
                DrawHollowRect(_spriteBatch, btnRect, Color.White);
                if (_font != null) _spriteBatch.DrawString(_font, category, new Vector2(x + 10, 5), Color.White);

                if (_activeMenu == category)
                {
                    var items = _menus[category];
                    int y = 30;
                    for (int i = 0; i < items.Count; i++)
                    {
                        Rectangle itemRect = new Rectangle(x, y, 120, 30);
                        _spriteBatch.Draw(_pixel, itemRect, new Color(50, 50, 50));
                        DrawHollowRect(_spriteBatch, itemRect, Color.LightGray);
                        if (_font != null) _spriteBatch.DrawString(_font, items[i].Name, new Vector2(x + 5, y + 5), Color.White);
                        y += 30;
                    }
                }
                x += 90;
            }
        }

        private void HandleScriptInput(KeyboardState current, ref string buffer)
        {
            bool ctrl = current.IsKeyDown(Keys.LeftControl) || current.IsKeyDown(Keys.RightControl);

            foreach (Keys key in current.GetPressedKeys())
            {
                if (!_prevKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Back && buffer.Length > 0)
                        buffer = buffer.Substring(0, buffer.Length - 1);
                    else if (key == Keys.Enter)
                        buffer += "\n";
                    else if (key == Keys.Space)
                        buffer += " ";
                    else if (ctrl && key == Keys.V)
                    {
                        buffer += GetClipboard();
                    }
                    else if (!ctrl)
                    {
                        char? c = ScriptKeyToChar(key, current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift));
                        if (c.HasValue) buffer += c.Value;
                    }
                }
            }
        }

        private void ParseAndGenerateGraph(string script)
        {
            _engine.Nodes.Clear();
            _nodeRects.Clear();
            _connectionStartNode = null;

            // Tokenize
            string pattern = @"([(){},;=+\-*/><&|^!]+|\s+|[A-Za-z_][A-Za-z0-9_]*|[0-9.]+)";
            var tokens = Regex.Split(script, pattern)
                              .Where(t => !string.IsNullOrWhiteSpace(t))
                              .ToList();

            var variables = new Dictionary<string, Node>();
            int currentY = 100;
            int currentX = 100;
            int tokenIndex = 0;

            void Spawn(Node n)
            {
                SpawnNodeAt(n, currentX, currentY);
                currentY += 80;
                if (currentY > 400) { currentY = 100; currentX += 200; }
            }

            try
            {
                ParseBlock(tokens, ref tokenIndex, variables, null, Spawn);
            }
            catch { }
        }

        private void ParseBlock(List<string> tokens, ref int index, Dictionary<string, Node> variables, Node conditionNode, Action<Node> spawner)
        {
            while (index < tokens.Count)
            {
                string t = tokens[index];

                if (t == "}")
                {
                    index++;
                    return;
                }
                else if (t == "var" || t == "int" || t == "float")
                {
                    index++;
                    string name = tokens[index++];
                    if (tokens[index] == "=")
                    {
                        index++;
                        Node valNode = ParseExpression(tokens, ref index, variables, spawner);
                        variables[name] = valNode;
                    }
                    if (index < tokens.Count && tokens[index] == ";") index++;
                }
                else if (t == "if")
                {
                    index++;
                    if (tokens[index] == "(") index++;
                    Node cond = ParseExpression(tokens, ref index, variables, spawner);
                    if (tokens[index] == ")") index++;

                    Node effectiveCond = cond;
                    if (conditionNode != null)
                    {
                        var andNode = new LogicNode(LogicNode.LogicType.And);
                        spawner(andNode);
                        _engine.Connect(conditionNode, 0, andNode, 0);
                        _engine.Connect(cond, 0, andNode, 1);
                        effectiveCond = andNode;
                    }

                    if (tokens[index] == "{")
                    {
                        index++;
                        ParseBlock(tokens, ref index, variables, effectiveCond, spawner);
                    }
                }
                else if (t == "new")
                {
                    index++;
                    string typeName = tokens[index++];
                    if (tokens[index] == "(") index++;
                    var args = ParseArguments(tokens, ref index, variables, spawner);
                    if (index < tokens.Count && tokens[index] == ";") index++;
                    CreateNode(typeName, args, conditionNode, spawner);
                }
                else if (IsIdentifier(t) && index + 1 < tokens.Count && tokens[index + 1] == "(")
                {
                    string funcName = tokens[index++];
                    index++;
                    var args = ParseArguments(tokens, ref index, variables, spawner);
                    if (index < tokens.Count && tokens[index] == ";") index++;
                    CreateNode(funcName, args, conditionNode, spawner);
                }
                else if (IsIdentifier(t) && index + 1 < tokens.Count && tokens[index + 1] == "=")
                {
                    string name = tokens[index++];
                    index++;
                    Node valNode = ParseExpression(tokens, ref index, variables, spawner);

                    if (conditionNode != null && variables.ContainsKey(name))
                    {
                        var selectNode = new MathNode(MathNode.Operation.Select);
                        spawner(selectNode);
                        _engine.Connect(conditionNode, 0, selectNode, 0);
                        _engine.Connect(valNode, 0, selectNode, 1);
                        _engine.Connect(variables[name], 0, selectNode, 2);
                        variables[name] = selectNode;
                    }
                    else
                    {
                        variables[name] = valNode;
                    }

                    if (index < tokens.Count && tokens[index] == ";") index++;
                }
                else
                {
                    index++;
                }
            }
        }

        private bool IsIdentifier(string s) => char.IsLetter(s[0]) || s[0] == '_';

        private List<Node> ParseArguments(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            var args = new List<Node>();
            while (index < tokens.Count && tokens[index] != ")")
            {
                args.Add(ParseExpression(tokens, ref index, variables, spawner));
                if (tokens[index] == ",") index++;
            }
            if (index < tokens.Count) index++;
            return args;
        }

        private Node ParseExpression(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            Node left = ParseTerm(tokens, ref index, variables, spawner);

            while (index < tokens.Count)
            {
                string op = tokens[index];
                if (op == "+" || op == "-" || op == "*" || op == "/" || op == ">" || op == "<")
                {
                    index++;
                    Node right = ParseTerm(tokens, ref index, variables, spawner);

                    Node opNode = null;
                    if (op == "+") opNode = new MathNode(MathNode.Operation.Add);
                    if (op == "-") opNode = new MathNode(MathNode.Operation.Subtract);
                    if (op == "*") opNode = new MathNode(MathNode.Operation.Multiply);
                    if (op == "/") opNode = new MathNode(MathNode.Operation.Divide);
                    if (op == ">") opNode = new LogicNode(LogicNode.LogicType.GreaterThan);
                    if (op == "<") opNode = new LogicNode(LogicNode.LogicType.LessThan);

                    spawner(opNode);
                    _engine.Connect(left, 0, opNode, 0);
                    _engine.Connect(right, 0, opNode, 1);
                    left = opNode;
                }
                else break;
            }
            return left;
        }

        private Node ParseTerm(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            string t = tokens[index++];
            if (float.TryParse(t, out float val))
            {
                var c = new ConstantNode(val);
                spawner(c);
                return c;
            }
            if (variables.ContainsKey(t)) return variables[t];
            if (t == "abs" && tokens[index] == "(")
            {
                index++;
                Node arg = ParseExpression(tokens, ref index, variables, spawner);
                if (tokens[index] == ")") index++;
                var absNode = new MathNode(MathNode.Operation.Abs);
                spawner(absNode);
                _engine.Connect(arg, 0, absNode, 0);
                return absNode;
            }
            if (t == "(")
            {
                Node n = ParseExpression(tokens, ref index, variables, spawner);
                if (tokens[index] == ")") index++;
                return n;
            }
            return new ConstantNode(0);
        }

        private void CreateNode(string name, List<Node> args, Node condition, Action<Node> spawner)
        {
            Node n = null;
            if (name == "beep")
            {
                var b = new BeepOutputNode();
                n = b;
                spawner(n);
                if (condition != null) _engine.Connect(condition, 0, n, 0);
                else { var c = new ConstantNode(1); spawner(c); _engine.Connect(c, 0, n, 0); }
                if (args.Count > 0) _engine.Connect(args[0], 0, n, 1);
                if (args.Count > 1) _engine.Connect(args[1], 0, n, 2);
            }
            else if (name == "ColorNode")
            {
                var c = new ColorOutputNode();
                n = c;
                spawner(n);
                if (args.Count > 0) _engine.Connect(args[0], 0, n, 0);
                if (args.Count > 1) _engine.Connect(args[1], 0, n, 1);
                if (args.Count > 2) _engine.Connect(args[2], 0, n, 2);
            }
        }

        private void UpdateOverlay(MouseState mouse, KeyboardState keyboard, bool clicked)
        {
            _overlayRect = new Rectangle(Window.ClientBounds.Width / 2 - 150, Window.ClientBounds.Height / 2 - 100, 300, 200);

            Point mousePos = mouse.Position;

            // Close Button
            Rectangle closeRect = new Rectangle(_overlayRect.Right - 25, _overlayRect.Top + 5, 20, 20);
            if (clicked && closeRect.Contains(mousePos))
            {
                _inspectedNode = null;
                _isDraggingNodes = false;
                return;
            }

            // Keyboard Close
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _inspectedNode = null;
                _isDraggingNodes = false;
                return;
            }

            // Content
            int y = _overlayRect.Top + 40;
            int x = _overlayRect.Left + 20;

            if (_inspectedNode is ConstantNode cNode)
            {
                Rectangle minusRect = new Rectangle(x, y, 30, 30);
                Rectangle plusRect = new Rectangle(x + 100, y, 30, 30);

                if (clicked && minusRect.Contains(mousePos))
                {
                    foreach (var n in _selectedNodes.OfType<ConstantNode>()) n.StoredValue -= 0.1f;
                    _inputValueBuffer = cNode.StoredValue.ToString();
                }
                if (clicked && plusRect.Contains(mousePos))
                {
                    foreach (var n in _selectedNodes.OfType<ConstantNode>()) n.StoredValue += 0.1f;
                    _inputValueBuffer = cNode.StoredValue.ToString();
                }

                HandleTextInput(keyboard, ref _inputValueBuffer);
                if (float.TryParse(_inputValueBuffer, out float val)) foreach (var n in _selectedNodes.OfType<ConstantNode>()) n.StoredValue = val;
            }
            else if (_inspectedNode is MathNode mNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                bool change = false;
                int dir = 0;
                if (clicked && btnRect.Contains(mousePos)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Right)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Left)) { change = true; dir = 3; }
                
                if (change)
                {
                    foreach (var n in _selectedNodes.OfType<MathNode>())
                        n.Op = (MathNode.Operation)(((int)n.Op + dir) % 6); // 6 ops now
                }
            }
            else if (_inspectedNode is LogicNode lNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                bool change = false;
                int dir = 0;
                if (clicked && btnRect.Contains(mousePos)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Right)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Left)) { change = true; dir = 5; }

                if (change)
                {
                    foreach (var n in _selectedNodes.OfType<LogicNode>())
                        n.Type = (LogicNode.LogicType)(((int)n.Type + dir) % 6);
                }
            }
            else if (_inspectedNode is KeyNode kNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    Keys[] commonKeys = { Keys.Space, Keys.A, Keys.B, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.W, Keys.S };
                    int idx = Array.IndexOf(commonKeys, kNode.Key);
                    idx = (idx + 1) % commonKeys.Length;
                    foreach (var n in _selectedNodes.OfType<KeyNode>()) { n.Key = commonKeys[idx]; n.Name = $"Key ({n.Key})"; }
                }

                Keys[] pressed = keyboard.GetPressedKeys();
                foreach (var k in pressed)
                {
                    if (!_prevKeyboardState.IsKeyDown(k) && k != Keys.Escape)
                    {
                        foreach (var n in _selectedNodes.OfType<KeyNode>()) { n.Key = k; n.Name = $"Key ({n.Key})"; }
                        break;
                    }
                }
            }
            else if (_inspectedNode is TimerNode tNode)
            {
                Rectangle resetRect = new Rectangle(x, y, 100, 30);
                if ((clicked && resetRect.Contains(mousePos)) || IsKeyPressed(keyboard, Keys.R))
                    foreach (var n in _selectedNodes.OfType<TimerNode>()) n.ElapsedTime = 0;
            }
            else if (_inspectedNode is CounterNode cntNode)
            {
                Rectangle minusRect = new Rectangle(x, y, 30, 30);
                Rectangle plusRect = new Rectangle(x + 100, y, 30, 30);
                if (clicked && minusRect.Contains(mousePos))
                {
                    foreach (var n in _selectedNodes.OfType<CounterNode>()) n.Value -= 0.1f;
                    _inputValueBuffer = cntNode.Value.ToString();
                }
                if (clicked && plusRect.Contains(mousePos))
                {
                    foreach (var n in _selectedNodes.OfType<CounterNode>()) n.Value += 0.1f;
                    _inputValueBuffer = cntNode.Value.ToString();
                }

                HandleTextInput(keyboard, ref _inputValueBuffer);
                if (float.TryParse(_inputValueBuffer, out float val)) foreach (var n in _selectedNodes.OfType<CounterNode>()) n.Value = val;
            }
            else if (_inspectedNode is ButtonNode btnNode)
            {
                Rectangle toggleRect = new Rectangle(x, y, 200, 30);
                if ((clicked && toggleRect.Contains(mousePos)) || IsKeyPressed(keyboard, Keys.Space))
                    foreach (var n in _selectedNodes.OfType<ButtonNode>()) n.IsToggle = !n.IsToggle;
            }
            else if (_inspectedNode is BeepOutputNode beepNode)
            {
                int btnY = y + 30;
                Rectangle prevRect = new Rectangle(x, btnY, 30, 30);
                Rectangle nextRect = new Rectangle(x + 200, btnY, 30, 30);

                if (clicked)
                {
                    int idx = _availableSounds.IndexOf(beepNode.SoundName);
                    if (idx == -1) idx = 0;
                    if (prevRect.Contains(mousePos)) idx--;
                    if (nextRect.Contains(mousePos)) idx++;
                    if (idx < 0) idx = _availableSounds.Count - 1;
                    if (idx >= _availableSounds.Count) idx = 0;
                    foreach (var n in _selectedNodes.OfType<BeepOutputNode>()) n.SoundName = _availableSounds[idx];
                }
            }
            else if (_inspectedNode is ScriptImporterNode scriptNode)
            {
                HandleScriptInput(keyboard, ref _inputValueBuffer);
                foreach (var n in _selectedNodes.OfType<ScriptImporterNode>()) n.Script = _inputValueBuffer;

                Rectangle btnRect = new Rectangle(x, y + 150, 100, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    ParseAndGenerateGraph(scriptNode.Script); // Only compiles the inspected one for now as it replaces the whole graph
                    _inspectedNode = null;
                }
            }
        }

        private void DrawOverlay()
        {
            if (_inspectedNode == null) return;

            _spriteBatch.Draw(_pixel, _overlayRect, new Color(0, 0, 0, 230));
            DrawHollowRect(_spriteBatch, _overlayRect, Color.White);

            if (_font != null) _spriteBatch.DrawString(_font, "Properties: " + _inspectedNode.Name, new Vector2(_overlayRect.X + 10, _overlayRect.Y + 10), Color.White);

            Rectangle closeRect = new Rectangle(_overlayRect.Right - 25, _overlayRect.Top + 5, 20, 20);
            _spriteBatch.Draw(_pixel, closeRect, Color.Red);

            int y = _overlayRect.Top + 40;
            int x = _overlayRect.Left + 20;

            if (_inspectedNode is ConstantNode cNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 30, 30), Color.Gray);
                _spriteBatch.Draw(_pixel, new Rectangle(x + 100, y, 30, 30), Color.Gray);
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "-", new Vector2(x + 10, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer, new Vector2(x + 40, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, "+", new Vector2(x + 110, y + 5), Color.White);
                }
            }
            else if (_inspectedNode is MathNode mNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Op: " + mNode.Op.ToString(), new Vector2(x + 10, y + 5), Color.White);
            }
            else if (_inspectedNode is LogicNode lNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Type: " + lNode.Type.ToString(), new Vector2(x + 10, y + 5), Color.White);
            }
            else if (_inspectedNode is KeyNode kNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Key: " + kNode.Key.ToString(), new Vector2(x + 10, y + 5), Color.White);
            }
            else if (_inspectedNode is TimerNode tNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 100, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Reset", new Vector2(x + 10, y + 5), Color.White);
                if (_font != null) _spriteBatch.DrawString(_font, tNode.ElapsedTime.ToString("0.00") + "s", new Vector2(x + 110, y + 5), Color.White);
            }
            else if (_inspectedNode is CounterNode cntNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 30, 30), Color.Gray);
                _spriteBatch.Draw(_pixel, new Rectangle(x + 100, y, 30, 30), Color.Gray);
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "-", new Vector2(x + 10, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer, new Vector2(x + 40, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, "+", new Vector2(x + 110, y + 5), Color.White);
                }
            }
            else if (_inspectedNode is ButtonNode btnNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 200, 30), btnNode.IsToggle ? Color.Green : Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Toggle Mode: " + (btnNode.IsToggle ? "ON" : "OFF"), new Vector2(x + 10, y + 5), Color.White);
            }
            else if (_inspectedNode is ColorOutputNode colNode)
            {
                if (_font != null) _spriteBatch.DrawString(_font, $"R:{colNode.DisplayColor.R} G:{colNode.DisplayColor.G} B:{colNode.DisplayColor.B}", new Vector2(x, y), Color.White);
            }
            else if (_inspectedNode is BeepOutputNode beepNode)
            {
                if (_font != null) _spriteBatch.DrawString(_font, $"Vol:{beepNode.Volume:0.0} Pitch:{beepNode.Pitch:0.0}", new Vector2(x, y), Color.White);

                y += 30;
                Rectangle prevRect = new Rectangle(x, y, 30, 30);
                Rectangle nextRect = new Rectangle(x + 200, y, 30, 30);

                _spriteBatch.Draw(_pixel, prevRect, Color.Gray);
                _spriteBatch.Draw(_pixel, nextRect, Color.Gray);

                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "<", new Vector2(x + 10, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, beepNode.SoundName, new Vector2(x + 40, y + 5), Color.White);
                    _spriteBatch.DrawString(_font, ">", new Vector2(x + 210, y + 5), Color.White);
                }
            }
            else if (_inspectedNode is ScriptImporterNode scriptNode)
            {
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "Type script (var a=1; b=a+2;):", new Vector2(x, y), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer + "|", new Vector2(x, y + 20), Color.Yellow);

                    Rectangle btnRect = new Rectangle(x, y + 150, 100, 30);
                    _spriteBatch.Draw(_pixel, btnRect, Color.Gray);
                    DrawHollowRect(_spriteBatch, btnRect, Color.White);
                    _spriteBatch.DrawString(_font, "Compile", new Vector2(x + 10, y + 155), Color.White);
                }
            }
        }

        private void HandleTextInput(KeyboardState current, ref string buffer)
        {
            foreach (Keys key in current.GetPressedKeys())
            {
                if (!_prevKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Back && buffer.Length > 0)
                        buffer = buffer.Substring(0, buffer.Length - 1);
                    else
                    {
                        char? c = KeyToChar(key);
                        if (c.HasValue) buffer += c.Value;
                    }
                }
            }
        }

        private string GetClipboard()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("powershell", "-command \"Get-Clipboard\"");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                    using var p = Process.Start(psi);
                    string text = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return text.TrimEnd();
                }
            }
            catch { }
            return "";
        }

        private char? KeyToChar(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
            if (key == Keys.OemPeriod || key == Keys.Decimal) return '.';
            if (key == Keys.OemMinus || key == Keys.Subtract) return '-';
            return null;
        }

        private char? ScriptKeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z) return shift ? key.ToString()[0] : key.ToString().ToLower()[0];
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                string s = (key - Keys.D0).ToString();
                if (shift)
                {
                    if (s == "9") return '(';
                    if (s == "0") return ')';
                    if (s == "8") return '*';
                    if (s == "5") return '%';
                }
                return s[0];
            }
            if (key == Keys.OemPlus || key == Keys.Add) return shift ? '+' : '=';
            if (key == Keys.OemMinus || key == Keys.Subtract) return shift ? '_' : '-';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.OemComma) return shift ? '<' : ',';
            if (key == Keys.OemSemicolon) return ';';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';
            return null;
        }


        private void DeleteNode(Node node)
        {
            _engine.Nodes.Remove(node);
            _nodeRects.Remove(node);
            if (_inspectedNode == node) _inspectedNode = null;
            _selectedNodes.Remove(node);

            foreach (var n in _engine.Nodes)
            {
                foreach (var input in n.Inputs)
                {
                    input.ConnectedSources.RemoveAll(s => s.ParentNode == node);
                }
            }
        }

        private void DeleteSelectedNodes()
        {
            // Create a copy to avoid modification during iteration issues
            var nodesToDelete = new List<Node>(_selectedNodes);
            foreach (var node in nodesToDelete)
            {
                DeleteNode(node);
            }
            _selectedNodes.Clear();
        }

        private Node CloneNode(Node original)
        {
            Node clone = null;
            if (original is ConstantNode c) clone = new ConstantNode(c.StoredValue);
            else if (original is MathNode m) clone = new MathNode(m.Op);
            else if (original is LogicNode l) clone = new LogicNode(l.Type);
            else if (original is TimerNode) clone = new TimerNode();
            else if (original is CounterNode cnt) { clone = new CounterNode(); ((CounterNode)clone).Value = cnt.Value; }
            else if (original is RandomNode) clone = new RandomNode();
            else if (original is ButtonNode b) { clone = new ButtonNode(); ((ButtonNode)clone).IsToggle = b.IsToggle; }
            else if (original is KeyNode k) { clone = new KeyNode(); ((KeyNode)clone).Key = k.Key; ((KeyNode)clone).Name = k.Name; }
            else if (original is CursorNode) clone = new CursorNode();
            else if (original is ColorOutputNode) clone = new ColorOutputNode();
            else if (original is BeepOutputNode bp) { clone = new BeepOutputNode(); ((BeepOutputNode)clone).SoundName = bp.SoundName; }
            else if (original is ScreenNode) clone = new ScreenNode();
            else if (original is ScriptImporterNode s) { clone = new ScriptImporterNode(); ((ScriptImporterNode)clone).Script = s.Script; }
            
            if (clone != null)
            {
                // Copy generic properties if needed, though most are set in constructor
            }
            return clone;
        }

        private void CopyNodes()
        {
            _clipboardNodes.Clear();
            _clipboardConnections.Clear();

            if (_selectedNodes.Count == 0) return;

            // Calculate bounds for relative positioning
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var node in _selectedNodes)
            {
                if (_nodeRects.TryGetValue(node, out Rectangle r))
                {
                    if (r.X < minX) minX = r.X;
                    if (r.Y < minY) minY = r.Y;
                }
            }

            // 1. Clone Nodes
            var nodeMap = new Dictionary<Node, int>(); // Map Original -> Index in Clipboard
            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                var original = _selectedNodes[i];
                var clone = CloneNode(original);
                if (clone != null)
                {
                    Point offset = Point.Zero;
                    if (_nodeRects.TryGetValue(original, out Rectangle r))
                        offset = new Point(r.X - minX, r.Y - minY);

                    _clipboardNodes.Add((clone, offset));
                    nodeMap[original] = _clipboardNodes.Count - 1;
                }
            }

            // 2. Record Connections (only internal to selection)
            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                var original = _selectedNodes[i];
                if (!nodeMap.ContainsKey(original)) continue;

                for (int inputIdx = 0; inputIdx < original.Inputs.Count; inputIdx++)
                {
                    var input = original.Inputs[inputIdx];
                    foreach (var source in input.ConnectedSources)
                    {
                        if (nodeMap.ContainsKey(source.ParentNode))
                        {
                            int sourceNodeIdx = nodeMap[source.ParentNode];
                            int sourceOutputIdx = source.ParentNode.Outputs.IndexOf(source);
                            
                            _clipboardConnections.Add(new ConnectionData 
                            { 
                                TargetNodeIdx = nodeMap[original], 
                                TargetInputIdx = inputIdx, 
                                SourceNodeIdx = sourceNodeIdx, 
                                SourceOutputIdx = sourceOutputIdx 
                            });
                        }
                    }
                }
            }
        }

        private void PasteNodes()
        {
            if (_clipboardNodes.Count == 0) return;

            _selectedNodes.Clear();
            var newNodes = new List<Node>();
            Point mousePos = Mouse.GetState().Position;

            // 1. Instantiate new nodes from clipboard templates
            foreach (var entry in _clipboardNodes)
            {
                var newNode = CloneNode(entry.Node);
                if (newNode != null)
                {
                    newNodes.Add(newNode);
                    SpawnNodeAt(newNode, mousePos.X + entry.Offset.X, mousePos.Y + entry.Offset.Y);
                    _selectedNodes.Add(newNode);
                }
            }

            // 2. Restore connections
            foreach (var conn in _clipboardConnections)
            {
                if (conn.TargetNodeIdx < newNodes.Count && conn.SourceNodeIdx < newNodes.Count)
                {
                    var target = newNodes[conn.TargetNodeIdx];
                    var source = newNodes[conn.SourceNodeIdx];
                    
                    if (conn.TargetInputIdx < target.Inputs.Count && conn.SourceOutputIdx < source.Outputs.Count)
                    {
                        _engine.Connect(source, conn.SourceOutputIdx, target, conn.TargetInputIdx);
                    }
                }
            }

            // 3. Offset positions slightly to indicate new paste (or follow mouse if we tracked relative positions)
            // For now, SpawnNode puts them at mouse position, but they will all stack.
            // Better: Keep relative positions from clipboard.
            // Since SpawnNode uses Mouse.GetState().Position, they all spawn there.
            // We should arrange them relative to the first node.
            // But we didn't store positions in clipboard.
            // Let's just scatter them slightly or leave them stacked (user can drag).
            // Actually, SpawnNodeAt uses specific X/Y. SpawnNode uses Mouse.
            // Let's rely on the user dragging them apart for now, or improve Copy to store relative positions.
            // Improvement: Store relative positions in Copy.
            // But for this request, basic paste is fine.
        }

        private void SpawnNode(Node node)
        {
            var mousePos = Mouse.GetState().Position;
            SpawnNodeAt(node, mousePos.X, mousePos.Y);
        }

        private void SpawnNodeAt(Node node, int x, int y)
        {
            _engine.Nodes.Add(node);
            int width = 100;
            int height = 60;
            if (node.Inputs.Count + node.Outputs.Count > 2)
            {
                width = 120;
                height = 80;
            }
            if (node is ScreenNode)
            {
                width = 140;
                height = 140;
            }
            _nodeRects[node] = new Rectangle(x, y, width, height);
        }

        private Vector2 GetInputPosition(Node node, int slotIndex)
        {
            var rect = _nodeRects[node];
            float y = rect.Y + (rect.Height * (slotIndex + 1) / (float)(node.Inputs.Count + 1));
            return new Vector2(rect.Left, y);
        }

        private Vector2 GetOutputPosition(Node node, int slotIndex)
        {
            var rect = _nodeRects[node];
            float y = rect.Y + (rect.Height * (slotIndex + 1) / (float)(node.Outputs.Count + 1));
            return new Vector2(rect.Right, y);
        }

        private float GetDistanceFromLineSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.LengthSquared();
            if (l2 == 0) return (p - a).Length();
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
            Vector2 projection = a + t * ab;
            return (p - projection).Length();
        }

        // Helper to draw lines using the 1x1 pixel texture
        private void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness = 2f)
        {
            var edge = end - start;
            var angle = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(_pixel, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness), null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private void DrawHollowRect(SpriteBatch sb, Rectangle rect, Color color, int thickness = 2)
        {
            int t = thickness;
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color); // Top
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - t, rect.Width, t), color); // Bottom
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color); // Left
            sb.Draw(_pixel, new Rectangle(rect.X + rect.Width - t, rect.Y, t, rect.Height), color); // Right
        }

        private string SerializeGraph()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TOYCON_v1");
            
            // Map nodes to IDs
            var nodeToId = new Dictionary<Node, int>();
            for (int i = 0; i < _engine.Nodes.Count; i++)
            {
                var node = _engine.Nodes[i];
                nodeToId[node] = i;
                Rectangle r = _nodeRects[node];
                string type = node.GetType().Name;
                string data = GetNodeData(node);
                sb.AppendLine($"NODE {i} {type} {r.X} {r.Y} {data}");
            }

            // Save Connections
            foreach (var node in _engine.Nodes)
            {
                int targetId = nodeToId[node];
                for (int i = 0; i < node.Inputs.Count; i++)
                {
                    var input = node.Inputs[i];
                    foreach (var source in input.ConnectedSources)
                    {
                        int sourceId = nodeToId[source.ParentNode];
                        int sourceOutputIdx = source.ParentNode.Outputs.IndexOf(source);
                        sb.AppendLine($"CONN {sourceId} {sourceOutputIdx} {targetId} {i}");
                    }
                }
            }

            return sb.ToString();
        }

        private void SaveLayout(string filename)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename), SerializeGraph());
        }

        private void LoadLayoutFromLines(string[] lines)
        {
            _engine.Nodes.Clear();
            _nodeRects.Clear();
            _selectedNodes.Clear();
            _inspectedNode = null;
            _connectionStartNode = null;

            if (lines.Length == 0 || lines[0] != "TOYCON_v1") return;

            var idToNode = new Dictionary<int, Node>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ');
                if (parts[0] == "NODE")
                {
                    int id = int.Parse(parts[1]);
                    string type = parts[2];
                    int x = int.Parse(parts[3]);
                    int y = int.Parse(parts[4]);
                    string data = parts.Length > 5 ? string.Join(" ", parts.Skip(5)) : "";

                    Node n = null;
                    if (type == "ConstantNode") n = new ConstantNode(0);
                    else if (type == "MathNode") n = new MathNode(MathNode.Operation.Add);
                    else if (type == "LogicNode") n = new LogicNode(LogicNode.LogicType.And);
                    else if (type == "TimerNode") n = new TimerNode();
                    else if (type == "CounterNode") n = new CounterNode();
                    else if (type == "RandomNode") n = new RandomNode();
                    else if (type == "ButtonNode") n = new ButtonNode();
                    else if (type == "KeyNode") n = new KeyNode();
                    else if (type == "CursorNode") n = new CursorNode();
                    else if (type == "ColorOutputNode") n = new ColorOutputNode();
                    else if (type == "BeepOutputNode") n = new BeepOutputNode();
                    else if (type == "ScreenNode") n = new ScreenNode();
                    else if (type == "ScriptImporterNode") n = new ScriptImporterNode();

                    if (n != null)
                    {
                        ApplyNodeData(n, data);
                        SpawnNodeAt(n, x, y);
                        idToNode[id] = n;
                    }
                }
                else if (parts[0] == "CONN")
                {
                    int srcId = int.Parse(parts[1]);
                    int srcSlot = int.Parse(parts[2]);
                    int tgtId = int.Parse(parts[3]);
                    int tgtSlot = int.Parse(parts[4]);

                    if (idToNode.ContainsKey(srcId) && idToNode.ContainsKey(tgtId))
                    {
                        _engine.Connect(idToNode[srcId], srcSlot, idToNode[tgtId], tgtSlot);
                    }
                }
            }
        }

        private void LoadLayout(string filename)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (!File.Exists(path)) return;
            LoadLayoutFromLines(File.ReadAllLines(path));
        }

        private void ExportStandalone(string filename)
        {
            try
            {
                var currentExe = Process.GetCurrentProcess().MainModule.FileName;
                var dir = Path.GetDirectoryName(currentExe);
                var exportPath = Path.Combine(dir, filename);

                // 1. Copy the current executable
                File.Copy(currentExe, exportPath, true);

                // 2. Prepare data
                string graphData = SerializeGraph();
                byte[] dataBytes = Encoding.UTF8.GetBytes(graphData);
                byte[] lengthBytes = BitConverter.GetBytes(dataBytes.Length);
                byte[] magicBytes = Encoding.UTF8.GetBytes(StandaloneMagic); // 10 bytes

                // 3. Append data to the end of the new executable
                using (var stream = new FileStream(exportPath, FileMode.Append))
                {
                    stream.Write(dataBytes, 0, dataBytes.Length);
                    stream.Write(lengthBytes, 0, lengthBytes.Length);
                    stream.Write(magicBytes, 0, magicBytes.Length);
                }
            }
            catch { /* Handle permission errors etc */ }
        }

        private bool TryLoadEmbeddedLayout()
        {
            try
            {
                var currentExe = Process.GetCurrentProcess().MainModule.FileName;
                using (var stream = new FileStream(currentExe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 20) return false; // Magic(10) + Int(4) + minimal data

                    byte[] magicCheck = new byte[10];
                    stream.Seek(-10, SeekOrigin.End);
                    stream.Read(magicCheck, 0, 10);
                    string magic = Encoding.UTF8.GetString(magicCheck);
                    
                    if (magic != StandaloneMagic) return false;

                    byte[] lengthCheck = new byte[4];
                    stream.Seek(-14, SeekOrigin.End);
                    stream.Read(lengthCheck, 0, 4);
                    int dataLength = BitConverter.ToInt32(lengthCheck, 0);

                    byte[] data = new byte[dataLength];
                    stream.Seek(-(14 + dataLength), SeekOrigin.End);
                    stream.Read(data, 0, dataLength);

                    string layout = Encoding.UTF8.GetString(data);
                    // Split by newline, handling both \r\n and \n
                    LoadLayoutFromLines(layout.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                    return true;
                }
            }
            catch { return false; }
        }

        private string GetNodeData(Node node)
        {
            if (node is ConstantNode c) return c.StoredValue.ToString();
            if (node is MathNode m) return m.Op.ToString();
            if (node is LogicNode l) return l.Type.ToString();
            if (node is KeyNode k) return k.Key.ToString();
            if (node is ButtonNode b) return b.IsToggle.ToString();
            if (node is BeepOutputNode beep) return beep.SoundName;
            if (node is CounterNode cnt) return cnt.Value.ToString();
            if (node is ScriptImporterNode s) return Convert.ToBase64String(Encoding.UTF8.GetBytes(s.Script));
            return "";
        }

        private void ApplyNodeData(Node node, string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            try {
                if (node is ConstantNode c) c.StoredValue = float.Parse(data);
                if (node is MathNode m) m.Op = Enum.Parse<MathNode.Operation>(data);
                if (node is LogicNode l) l.Type = Enum.Parse<LogicNode.LogicType>(data);
                if (node is KeyNode k) { k.Key = Enum.Parse<Keys>(data); k.Name = $"Key ({k.Key})"; }
                if (node is ButtonNode b) b.IsToggle = bool.Parse(data);
                if (node is BeepOutputNode beep) beep.SoundName = data;
                if (node is CounterNode cnt) cnt.Value = float.Parse(data);
                if (node is ScriptImporterNode s) s.Script = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            } catch {}
        }
    }
}