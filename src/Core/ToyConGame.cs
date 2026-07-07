using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Text;

namespace ToyConEngine
{
    // --- MONOGAME IMPLEMENTATION ---
    public partial class ToyConGame : Game
    {

        private Random rnd = new Random();
        public static Rectangle ClientBounds { get; private set; }
        private GraphicsDeviceManager _graphics = null!;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!; // Used for drawing lines and rectangles
        private SpriteFont? _font;
        private List<string> _availableSounds = new List<string>();
        private readonly Dictionary<string, SoundEffect> _soundCache = new(StringComparer.OrdinalIgnoreCase);

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

        private GraphEngine _engine = null!;

        // Visual State
        private Dictionary<Node, Rectangle> _nodeRects = new Dictionary<Node, Rectangle>();
        private bool _isDraggingNodes = false;
        private Point _lastMousePos;
        private bool _presentationMode = false;
        private Dictionary<ScreenNode, Texture2D> _screenTextures = new Dictionary<ScreenNode, Texture2D>();
        private KeyboardState _prevKeyboardState;
        private MouseState _prevMouseState;

        private string? _activeMenu;
        private Dictionary<string, List<(string Name, string Description, Func<Node?> Factory)>>? _menus;
        private Rectangle _uiBarRect = new Rectangle(0, 0, 800, 30);
        private const int UiBarHeight = 30;

        private Node? _inspectedNode;
        private Rectangle _overlayRect;
        private double _lastClickTime;
        private const double DoubleClickTime = 0.3;

        private Node? _connectionStartNode;
        private int _connectionStartIndex = -1;
        private string _inputValueBuffer = "";
        private bool _benchmarkMode = false;
        private string _benchmarkResult = "";
        private bool _showBenchmarkPopup = false;
        
        private const string StandaloneMagic = "TOYCON_PKG";

        private double nsPerTick = 0;
        private int _tpsCount = 0;
        private double _tpsElapsed = 0;
        private string _tpsString = "TPS: 0";
        private List<float> _tpsHistory = new List<float>();
        private const int MaxTpsHistory = 60;


        private double tps = 60;
        private bool optimized = false;

        public ToyConGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "ToyCon Engine - MonoGame Port";
        }

        private void setupBench() {
            _graphics.SynchronizeWithVerticalRetrace = false;
            _benchmarkMode = !_benchmarkMode; 
            _benchmarkResult = "Benchmarking...";
            _showBenchmarkPopup = true;
            optimized = false;
        }

        protected override void Initialize()
        {
            _engine = new GraphEngine();

            _menus = new Dictionary<string, List<(string Name, string Description, Func<Node?> Factory)>>
            {
                { "File", new List<(string Name, string Description, Func<Node?> Factory)> {
                    ("Save", "Saves the current graph layout to a chosen file.", () => { 
                        var path = PromptForSavePath("design.toy", "ToyCon Layout|*.toy|All Files|*.*");
                        if (path != null) SaveLayout(path);
                        return null; 
                    }),
                    ("Load", "Loads a graph layout from a chosen file.", () => { 
                        var path = PromptForOpenPath("ToyCon Layout|*.toy|All Files|*.*");
                        if (path != null) LoadLayout(path);
                        return null; 
                    }),
                    ("Benchmark", "Runs a performance benchmark to measure graph throughput.", () => { 
                        setupBench();
                        return null; 
                    }),
                    ("Export EXE", "Packages the current project into a standalone executable.", () => { 
                        var path = PromptForSavePath("ToyCon_Export.exe", "Executable|*.exe");
                        if (path != null) ExportStandalone(path);
                        return null; 
                    }),
                    ("Clear", "Removes every node from the current graph.", () => { _engine.Nodes.Clear(); _nodeRects.Clear(); _selectedNodes.Clear(); _inspectedNode = null; return null; })
                }},
                { "Input", new List<(string Name, string Description, Func<Node?> Factory)> {
                    ("Constant", "Emits a fixed numeric value for use in calculations.", () => new ConstantNode(1.0f)),
                    ("Button", "Outputs a signal while the button area is pressed.", () => new ButtonNode()),
                    ("Key", "Exposes the state of a keyboard key as a signal.", () => new KeyNode()),
                    ("Timer", "Counts elapsed time and can reset when the reset input becomes active.", () => new TimerNode()),
                    ("Cursor", "Outputs the current mouse X and Y coordinates.", () => new CursorNode()),
                    ("Random", "Generates a random value each evaluation.", () => new RandomNode())
                }},
                { "Middle", new List<(string Name, string Description, Func<Node?> Factory)> {
                    ("Math", "Performs arithmetic operations on one or more values.", () => new MathNode(MathNode.Operation.Add)),
                    ("Logic", "Combines boolean signals with logic operators.", () => new LogicNode(LogicNode.LogicType.And)),
                    ("Counter", "Tracks a value and increments or decrements it over time.", () => new CounterNode())
                }},
                { "Output", new List<(string Name, string Description, Func<Node?> Factory)> {
                    ("Color", "Uses input values to drive a color output.", () => new ColorOutputNode()),
                    ("Beep", "Plays a sound effect based on the configured settings.", () => new BeepOutputNode()),
                    ("Screen", "Draws to the game screen output.", () => new ScreenNode())
                }},
                { "Import", new List<(string Name, string Description, Func<Node?> Factory)> {
                    ("Script", "Imports a script graph definition from a text source.", () => new ScriptImporterNode()),
                    ("Toy", "Imports a ToyCon layout or script from a text source.", () => new ScriptImporterNode()),
                    ("MIDI", "Imports a MIDI file into a graph of timers, counters, logic gates, and beeps.", () => new MidiImporterNode())
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
            if (contentDir.Exists) {
                foreach(var file in contentDir.GetFiles("*.xnb", SearchOption.AllDirectories)) {
                    // Get relative path from Content.RootDirectory
                    string relativePath = Path.GetRelativePath(Content.RootDirectory, file.FullName);

                    // Remove the .xnb extension for Content.Load
                    string assetName = Path.ChangeExtension(relativePath, null).Replace('\\', '/'); // Ensure forward slashes for MonoGame

                    try {
                        var sound = LoadSoundEffect(assetName);
                        if (sound != null) {
                        _availableSounds.Add(assetName);
                        }
                    } catch (Exception ex) {
                        // Log and skip invalid assets
                        Console.WriteLine($"Failed to load sound '{assetName}': {ex.Message}");
                    }
                }
            }    
            if (_availableSounds.Count == 0) _availableSounds.Add("Beep");
        }

        private SoundEffect? LoadSoundEffect(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            if (_soundCache.TryGetValue(assetName, out var cached)) return cached;

            try
            {
                var effect = Content.Load<SoundEffect>(assetName);
                _soundCache[assetName] = effect;
                return effect;
            }
            catch
            {
                return null;
            }
        }

        private void benchBiotch(GameTime gameTime) {
            if (!optimized) { nsPerTick = (gameTime.TotalGameTime.TotalSeconds - gameTime.ElapsedGameTime.TotalSeconds)/16; }
            if  (_benchmarkMode || ((rnd.Next(0,100000000) > 98969492)&&!optimized))
            {
                if (!_benchmarkMode) TargetElapsedTime = TimeSpan.FromSeconds(1.0 / (10000*nsPerTick));
                // Run benchmark: execute Tick as many times as possible in 25ms
                int iterations = 0;
                var sw = Stopwatch.StartNew();
                long limitMs = rnd.Next(0,42066964)/10000000;

                while (sw.ElapsedMilliseconds < limitMs)
                {
                    _engine.Tick(gameTime);
                    iterations++;
                    _tpsCount++;
                }
                sw.Stop();

                tps = iterations / sw.Elapsed.TotalSeconds;
                nsPerTick = 1000000.0 / tps;

                _benchmarkResult = $"Magic Number: {nsPerTick:F8}ns\n({tps:F0} TPS)";
                _benchmarkMode = false;
            }
            else
            {
                _engine.Tick(gameTime);
                _tpsCount++;
            }
            if (tps > 1152000) { optimized=true; }
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
            benchBiotch(gameTime);

            _tpsElapsed += gameTime.ElapsedGameTime.TotalSeconds;
            if (_tpsElapsed >= 1.0)
            {
                _tpsString = $"FPS: {_tpsCount}";
                _tpsHistory.Add(_tpsCount);
                if (_tpsHistory.Count > MaxTpsHistory) _tpsHistory.RemoveAt(0);
                _tpsCount = 0;
                _tpsElapsed -= 1.0;
            }

            // 2. Handle Audio Outputs
            foreach (var node in _engine.Nodes)
            {
                if (node is BeepOutputNode beepNode && beepNode.ShouldPlay)
                {
                    var sfx = LoadSoundEffect(beepNode.SoundName);
                    if (sfx != null)
                    {
                        sfx.Play(beepNode.Volume, beepNode.Pitch, 0);
                    }
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
                                TryConnect(_connectionStartNode, _connectionStartIndex, node, i);
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
                                    if (startNode is null) continue;
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
                    Node? clickedNode = null;
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
                            ClampNodeRect(ref r);
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

                // Draw Buttons and Colors in Presentation Mode
                foreach (var kvp in _nodeRects)
                {
                    var node = kvp.Key;
                    var rect = kvp.Value;

                    if (node is ButtonNode btn)
                    {
                        Color c = btn.IsPressed ? Color.Gray : Color.DarkGray;
                        _spriteBatch.Draw(_pixel, rect, c);
                        DrawHollowRect(_spriteBatch, rect, Color.White, 2);
                        if (_font != null)
                        {
                            Vector2 textSize = _font.MeasureString(btn.Name);
                            _spriteBatch.DrawString(_font, btn.Name, rect.Center.ToVector2() - textSize / 2, Color.White);
                        }
                    }
                    else if (node is ColorOutputNode col)
                    {
                        _spriteBatch.Draw(_pixel, rect, col.DisplayColor);
                        DrawHollowRect(_spriteBatch, rect, Color.White, 2);
                    }
                }

                if (_font != null) 
                {
                    _spriteBatch.DrawString(_font, _tpsString, new Vector2(10, 10), Color.Lime);
                    DrawTpsGraph(_spriteBatch, new Rectangle(10, 35, 100, 30));
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
                        if (startNode is null) continue;

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

            if (_font != null)
            {
                Vector2 sz = _font.MeasureString(_tpsString);
                _spriteBatch.DrawString(_font, _tpsString, new Vector2(ClientBounds.Width - sz.X - 10, 5), Color.Lime);
                DrawTpsGraph(_spriteBatch, new Rectangle(ClientBounds.Width - 110, 30, 100, 30));
            }

            if (_showBenchmarkPopup && !string.IsNullOrEmpty(_benchmarkResult))
            {
                Vector2 sz = _font?.MeasureString(_benchmarkResult) ?? Vector2.Zero;
                Vector2 pos = new Vector2((ClientBounds.Width - sz.X) / 2, 50);
                Rectangle popupRect = new Rectangle((int)pos.X - 10, (int)pos.Y - 10, (int)sz.X + 20, (int)sz.Y + 20);
                // Background
                _spriteBatch.Draw(_pixel, popupRect, new Color(0, 0, 0, 200));
                DrawHollowRect(_spriteBatch, popupRect, Color.White);
                // Close button (X)
                Rectangle closeRect = new Rectangle(popupRect.Right - 20, popupRect.Top, 20, 20);
                _spriteBatch.Draw(_pixel, closeRect, Color.DarkRed);
                if (_font != null)
                {
                    // Draw "X" inside close button
                    _spriteBatch.DrawString(_font, "X", new Vector2(closeRect.X + 4, closeRect.Y + 2), Color.White);
                    // Draw benchmark result text
                    _spriteBatch.DrawString(_font, _benchmarkResult, pos, Color.White);
                }
            }

            DrawOverlay();

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void UpdateOverlay(MouseState mouse, KeyboardState keyboard, bool clicked)
        {
            int width = Math.Clamp(ClientBounds.Width - 40, 280, 320);
            int height = Math.Clamp(ClientBounds.Height - 40, 220, 260);
            int x = Math.Clamp(ClientBounds.Width / 2 - width / 2, 10, Math.Max(10, ClientBounds.Width - width - 10));
            int y = Math.Clamp(ClientBounds.Height / 2 - height / 2, 35, Math.Max(35, ClientBounds.Height - height - 10));
            _overlayRect = new Rectangle(x, y, width, height);

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
            int contentY = _overlayRect.Top + 40;
            int contentX = _overlayRect.Left + 20;

            if (_inspectedNode is ConstantNode cNode)
            {
                Rectangle minusRect = new Rectangle(contentX, contentY, 30, 30);
                Rectangle plusRect = new Rectangle(contentX + 100, contentY, 30, 30);

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
                Rectangle btnRect = new Rectangle(contentX, contentY, 200, 30);
                bool change = false;
                int dir = 0;
                if (clicked && btnRect.Contains(mousePos)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Right)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Left)) { change = true; dir = 3; }
                
                if (change)
                {
                    foreach (var n in _selectedNodes.OfType<MathNode>())
                    {
                        n.Op = (MathNode.Operation)(((int)n.Op + dir) % 6); // 6 ops now
                        n.Name = $"Math ({n.Op})";
                    }
                }
            }
            else if (_inspectedNode is LogicNode lNode)
            {
                Rectangle btnRect = new Rectangle(contentX, contentY, 200, 30);
                bool change = false;
                int dir = 0;
                if (clicked && btnRect.Contains(mousePos)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Right)) { change = true; dir = 1; }
                if (IsKeyPressed(keyboard, Keys.Left)) { change = true; dir = 5; }

                if (change)
                {
                    foreach (var n in _selectedNodes.OfType<LogicNode>())
                    {
                        n.Type = (LogicNode.LogicType)(((int)n.Type + dir) % 6);
                        n.Name = $"Logic ({n.Type})";
                    }
                }
            }
            else if (_inspectedNode is KeyNode kNode)
            {
                Rectangle btnRect = new Rectangle(contentX, contentY, 200, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    Keys[] commonKeys = {
                        Keys.Space, Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.Q, Keys.R, Keys.T,
                        Keys.W, Keys.Z, Keys.X, Keys.V,
                        Keys.Up, Keys.Down, Keys.Left, Keys.Right,
                        Keys.Enter,
                        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
                        Keys.LeftShift, Keys.RightShift
                    };
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
                Rectangle resetRect = new Rectangle(contentX, contentY, 100, 30);
                if ((clicked && resetRect.Contains(mousePos)) || IsKeyPressed(keyboard, Keys.R))
                    foreach (var n in _selectedNodes.OfType<TimerNode>()) n.ElapsedTime = 0;
            }
            else if (_inspectedNode is CounterNode cntNode)
            {
                Rectangle minusRect = new Rectangle(contentX, contentY, 30, 30);
                Rectangle plusRect = new Rectangle(contentX + 100, contentY, 30, 30);
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
                Rectangle toggleRect = new Rectangle(contentX, contentY, 200, 30);
                if ((clicked && toggleRect.Contains(mousePos)) || IsKeyPressed(keyboard, Keys.Space))
                    foreach (var n in _selectedNodes.OfType<ButtonNode>()) n.IsToggle = !n.IsToggle;
            }
            else if (_inspectedNode is BeepOutputNode beepNode)
            {
                int btnY = contentY + 30;
                Rectangle prevRect = new Rectangle(contentX, btnY, 30, 30);
                Rectangle nextRect = new Rectangle(contentX + 200, btnY, 30, 30);

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

                Rectangle btnRect = new Rectangle(contentX, contentY + 150, 100, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    ParseAndGenerateGraph(scriptNode.Script); // Only compiles the inspected one for now as it replaces the whole graph
                    _inspectedNode = null;
                }
            }
            else if (_inspectedNode is MidiImporterNode midiNode)
            {
                Rectangle openRect = new Rectangle(contentX, contentY + 70, 110, 30);
                Rectangle importRect = new Rectangle(contentX + 130, contentY + 70, 110, 30);

                Console.WriteLine("MIDI path: {0}, message: {1}", midiNode.MidiPath, midiNode.LastImportMessage);

                if (clicked && openRect.Contains(mousePos))
                {
                    var path = PromptForOpenPath("MIDI Files|*.mid;*.midi|All Files|*.*");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        midiNode.MidiPath = path;
                        midiNode.LastImportMessage = $"Selected: {Path.GetFileName(path)}";
                        _inputValueBuffer = path;
                    }
                }

                if (clicked && importRect.Contains(mousePos))
                {
                    ParseAndGenerateMidiGraph(midiNode.MidiPath, midiNode);
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

            int overlayY = _overlayRect.Top + 40;
            int overlayX = _overlayRect.Left + 20;

            if (_inspectedNode is ConstantNode cNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 30, 30), Color.Gray);
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX + 100, overlayY, 30, 30), Color.Gray);
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "-", new Vector2(overlayX + 10, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer, new Vector2(overlayX + 40, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, "+", new Vector2(overlayX + 110, overlayY + 5), Color.White);
                }
            }
            else if (_inspectedNode is MathNode mNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Op: " + mNode.Op.ToString(), new Vector2(overlayX + 10, overlayY + 5), Color.White);
            }
            else if (_inspectedNode is LogicNode lNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Type: " + lNode.Type.ToString(), new Vector2(overlayX + 10, overlayY + 5), Color.White);
            }
            else if (_inspectedNode is KeyNode kNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 200, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Key: " + kNode.Key.ToString(), new Vector2(overlayX + 10, overlayY + 5), Color.White);
            }
            else if (_inspectedNode is TimerNode tNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 100, 30), Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Reset", new Vector2(overlayX + 10, overlayY + 5), Color.White);
                if (_font != null) _spriteBatch.DrawString(_font, tNode.ElapsedTime.ToString("0.00") + "s", new Vector2(overlayX + 110, overlayY + 5), Color.White);
            }
            else if (_inspectedNode is CounterNode cntNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 30, 30), Color.Gray);
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX + 100, overlayY, 30, 30), Color.Gray);
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "-", new Vector2(overlayX + 10, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer, new Vector2(overlayX + 40, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, "+", new Vector2(overlayX + 110, overlayY + 5), Color.White);
                }
            }
            else if (_inspectedNode is ButtonNode btnNode)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(overlayX, overlayY, 200, 30), btnNode.IsToggle ? Color.Green : Color.Gray);
                if (_font != null) _spriteBatch.DrawString(_font, "Toggle Mode: " + (btnNode.IsToggle ? "ON" : "OFF"), new Vector2(overlayX + 10, overlayY + 5), Color.White);
            }
            else if (_inspectedNode is ColorOutputNode colNode)
            {
                if (_font != null) _spriteBatch.DrawString(_font, $"R:{colNode.DisplayColor.R} G:{colNode.DisplayColor.G} B:{colNode.DisplayColor.B}", new Vector2(overlayX, overlayY), Color.White);
            }
            else if (_inspectedNode is BeepOutputNode beepNode)
            {
                if (_font != null) _spriteBatch.DrawString(_font, $"Vol:{beepNode.Volume:0.0} Pitch:{beepNode.Pitch:0.0}", new Vector2(overlayX, overlayY), Color.White);

                overlayY += 30;
                Rectangle prevRect = new Rectangle(overlayX, overlayY, 30, 30);
                Rectangle nextRect = new Rectangle(overlayX + 200, overlayY, 30, 30);

                _spriteBatch.Draw(_pixel, prevRect, Color.Gray);
                _spriteBatch.Draw(_pixel, nextRect, Color.Gray);

                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "<", new Vector2(overlayX + 10, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, beepNode.SoundName, new Vector2(overlayX + 40, overlayY + 5), Color.White);
                    _spriteBatch.DrawString(_font, ">", new Vector2(overlayX + 210, overlayY + 5), Color.White);
                }
            }
            else if (_inspectedNode is ScriptImporterNode scriptNode)
            {
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "Type script (var a=1; b=a+2;):", new Vector2(overlayX, overlayY), Color.White);
                    _spriteBatch.DrawString(_font, _inputValueBuffer + "|", new Vector2(overlayX, overlayY + 20), Color.Yellow);

                    Rectangle btnRect = new Rectangle(overlayX, overlayY + 150, 100, 30);
                    _spriteBatch.Draw(_pixel, btnRect, Color.Gray);
                    DrawHollowRect(_spriteBatch, btnRect, Color.White);
                    _spriteBatch.DrawString(_font, "Compile", new Vector2(overlayX + 10, overlayY + 155), Color.White);
                }
            }
            else if (_inspectedNode is MidiImporterNode midiNode)
            {
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, "MIDI import", new Vector2(overlayX, overlayY), Color.White);
                    _spriteBatch.DrawString(_font, midiNode.MidiPath, new Vector2(overlayX, overlayY + 20), Color.Yellow);
                    _spriteBatch.DrawString(_font, midiNode.LastImportMessage, new Vector2(overlayX, overlayY + 40), Color.LightGray);

                    Rectangle openRect = new Rectangle(overlayX, overlayY + 70, 110, 30);
                    Rectangle importRect = new Rectangle(overlayX + 130, overlayY + 70, 110, 30);
                    _spriteBatch.Draw(_pixel, openRect, Color.Gray);
                    _spriteBatch.Draw(_pixel, importRect, Color.Gray);
                    DrawHollowRect(_spriteBatch, openRect, Color.White);
                    DrawHollowRect(_spriteBatch, importRect, Color.White);
                    _spriteBatch.DrawString(_font, "Open MIDI", new Vector2(overlayX + 10, overlayY + 75), Color.White);
                    _spriteBatch.DrawString(_font, "Import", new Vector2(overlayX + 140, overlayY + 75), Color.White);
                }
            }
        }

        private static string EscapePowerShellString(string value)
        {
            return value.Replace("'", "''");
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
                        if (source.ParentNode is null) continue;
                        int sourceId = nodeToId[source.ParentNode];
                        int sourceOutputIdx = source.ParentNode.Outputs.IndexOf(source);
                        sb.AppendLine($"CONN {sourceId} {sourceOutputIdx} {targetId} {i}");
                    }
                }
            }

            return sb.ToString();
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
            if (node is MidiImporterNode midi) return Convert.ToBase64String(Encoding.UTF8.GetBytes(midi.MidiPath + "\n" + midi.LastImportMessage));
            return "";
        }

        private void ApplyNodeData(Node node, string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            try {
                if (node is ConstantNode c) c.StoredValue = float.Parse(data);
                if (node is MathNode m) { m.Op = Enum.Parse<MathNode.Operation>(data); m.Name = $"Math ({m.Op})"; }
                if (node is LogicNode l) { l.Type = Enum.Parse<LogicNode.LogicType>(data); l.Name = $"Logic ({l.Type})"; }
                if (node is KeyNode k) { k.Key = Enum.Parse<Keys>(data); k.Name = $"Key ({k.Key})"; }
                if (node is ButtonNode b) b.IsToggle = bool.Parse(data);
                if (node is BeepOutputNode beep) beep.SoundName = data;
                if (node is CounterNode cnt) cnt.Value = float.Parse(data);
                if (node is ScriptImporterNode s) s.Script = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                if (node is MidiImporterNode midi)
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                    var parts = decoded.Split(new[] { '\n' }, 2);
                    midi.MidiPath = parts[0];
                    midi.LastImportMessage = parts.Length > 1 ? parts[1] : "Loaded from layout";
                }
            } catch {}
        }
    }
}