using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ToyConEngine
{
    // 1. The Data Type (The "Volt" or Signal)
    // In GBG, almost everything is a float (0.0 to 1.0 or arbitrary numbers).
    public struct Signal
    {
        public float Value;
        public Signal(float value) { Value = value; }
    }

    // 2. Ports (Connection Points)
    public class InputPort
    {
        public string Name { get; set; }
        public Node ParentNode { get; set; }
        public List<OutputPort> ConnectedSources { get; set; } = new List<OutputPort>();

        public float GetValue()
        {
            // If nothing is connected, return default (0)
            if (ConnectedSources.Count == 0) return 0.0f;
            return ConnectedSources.Max(s => s.Value);
        }
    }

    public class OutputPort
    {
        public string Name { get; set; }
        public Node ParentNode { get; set; }
        public float Value { get; private set; }

        // Update the value sitting on this port
        public void SetValue(float value)
        {
            Value = value;
        }
    }

    // 3. The Base Node (The "Nodon")
    public abstract class Node
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public List<InputPort> Inputs { get; set; } = new List<InputPort>();
        public List<OutputPort> Outputs { get; set; } = new List<OutputPort>();

        // The core logic: Read inputs, process, write to outputs
        public abstract void Evaluate(GameTime gameTime);

        protected void AddInput(string name) => Inputs.Add(new InputPort { Name = name, ParentNode = this });
        protected void AddOutput(string name) => Outputs.Add(new OutputPort { Name = name, ParentNode = this });
    }

    // --- CONCRETE NODES ---

    // A Constant Number Node
    public class ConstantNode : Node
    {
        public float StoredValue { get; set; }

        public ConstantNode(float value)
        {
            Name = "Constant";
            StoredValue = value;
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            // Always output the stored value
            Outputs[0].SetValue(StoredValue);
        }
    }

    // A Math Node (Add, Subtract, Multiply)
    public class MathNode : Node
    {
        public enum Operation { Add, Subtract, Multiply, Divide, Abs, Select }
        public Operation Op { get; set; }

        public MathNode(Operation op)
        {
            Name = $"Math ({op})";
            Op = op;
            if (op == Operation.Abs)
            {
                AddInput("A");
            }
            else if (op == Operation.Select)
            {
                AddInput("Cond");
                AddInput("True");
                AddInput("False");
            }
            else
            {
                AddInput("A");
                AddInput("B");
            }
            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float result = 0f;

            if (Op == Operation.Abs)
            {
                result = Math.Abs(Inputs[0].GetValue());
            }
            else if (Op == Operation.Select)
            {
                float cond = Inputs[0].GetValue();
                result = (Math.Abs(cond) > 0.001f) ? Inputs[1].GetValue() : Inputs[2].GetValue();
            }
            else
            {
                float a = Inputs[0].GetValue();
                float b = Inputs[1].GetValue();
                switch (Op)
                {
                    case Operation.Add: result = a + b; break;
                    case Operation.Subtract: result = a - b; break;
                    case Operation.Multiply: result = a * b; break;
                    case Operation.Divide: result = (Math.Abs(b) > 0.001f) ? a / b : 0f; break;
                }
            }

            Outputs[0].SetValue(result);
        }
    }

    // A Logic Node (AND, NOT)
    public class LogicNode : Node
    {
        public enum LogicType { And, Not, GreaterThan, LessThan, Or, Xor }
        public LogicType Type { get; set; }

        public LogicNode(LogicType type)
        {
            Name = $"Logic ({type})";
            Type = type;

            AddInput("In 1");
            if (type != LogicType.Not) AddInput("In 2"); // NOT only needs 1 input

            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float a = Inputs[0].GetValue();
            // Logic in GBG usually treats > 0 as True
            bool boolA = Math.Abs(a) > 0.001f;

            float result = 0.0f;

            switch (Type)
            {
                case LogicType.And:
                    float b = Inputs[1].GetValue();
                    bool boolB = Math.Abs(b) > 0.001f;
                    result = (boolA && boolB) ? 1.0f : 0.0f;
                    break;
                case LogicType.Not:
                    result = (!boolA) ? 1.0f : 0.0f;
                    break;
                case LogicType.GreaterThan:
                    float c = Inputs[1].GetValue();
                    result = (a > c) ? 1.0f : 0.0f;
                    break;
                case LogicType.LessThan:
                    float f = Inputs[1].GetValue();
                    result = (a < f) ? 1.0f : 0.0f;
                    break;
                case LogicType.Or:
                    float d = Inputs[1].GetValue();
                    bool boolD = Math.Abs(d) > 0.001f;
                    result = (boolA || boolD) ? 1.0f : 0.0f;
                    break;
                case LogicType.Xor:
                    float e = Inputs[1].GetValue();
                    bool boolE = Math.Abs(e) > 0.001f;
                    result = (boolA ^ boolE) ? 1.0f : 0.0f;
                    break;
            }

            Outputs[0].SetValue(result);
        }
    }

    // A Timer Node
    public class TimerNode : Node
    {
        public float ElapsedTime { get; set; } = 0f;

        public TimerNode()
        {
            Name = "Timer";
            AddOutput("Time");
        }

        public override void Evaluate(GameTime gameTime)
        {
            ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            Outputs[0].SetValue(ElapsedTime);
        }
    }

    // A Counter Node
    public class CounterNode : Node
    {
        public float Value { get; set; } = 0;
        private bool _lastCountUpState = false;
        private bool _lastCountDownState = false;

        public CounterNode()
        {
            Name = "Counter";
            AddInput("Count Up");
            AddInput("Count Down");
            AddInput("Reset");
            AddOutput("Value");
        }

        public override void Evaluate(GameTime gameTime)
        {
            bool countUp = Inputs[0].GetValue() > 0.5f;
            bool countDown = Inputs[1].GetValue() > 0.5f;
            bool reset = Inputs[2].GetValue() > 0.5f;

            if (reset)
            {
                Value = 0;
            }
            else
            {
                if (countUp && !_lastCountUpState) Value++;
                if (countDown && !_lastCountDownState) Value--;
            }

            _lastCountUpState = countUp;
            _lastCountDownState = countDown;
            Outputs[0].SetValue(Value);
        }
    }

    // A Random Node
    public class RandomNode : Node
    {
        private static Random _rng = new Random();

        public RandomNode()
        {
            Name = "Random";
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            Outputs[0].SetValue((float)_rng.NextDouble());
        }
    }

    // A Button Node (Click to activate)
    public class ButtonNode : Node
    {
        public bool IsPressed { get; set; }
        public bool IsToggle { get; set; }

        public ButtonNode()
        {
            Name = "Button";
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            Outputs[0].SetValue(IsPressed ? 1.0f : 0.0f);
        }
    }

    // A Keyboard Key Node
    public class KeyNode : Node
    {
        public Keys Key { get; set; } = Keys.Space;

        public KeyNode()
        {
            Name = $"Key ({Key})";
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            Outputs[0].SetValue(Keyboard.GetState().IsKeyDown(Key) ? 1.0f : 0.0f);
        }
    }

    // A Cursor Node (Mouse X/Y)
    public class CursorNode : Node
    {
        public CursorNode()
        {
            Name = "Cursor";
            AddOutput("X");
            AddOutput("Y");
        }

        public override void Evaluate(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            var bounds = ToyConGame.ClientBounds;

            float x = (bounds.Width > 0) ? (float)mouse.X / bounds.Width : 0f;
            float y = (bounds.Height > 0) ? (float)mouse.Y / bounds.Height : 0f;

            Outputs[0].SetValue(x);
            Outputs[1].SetValue(y);
        }
    }

    // --- OUTPUT NODES ---

    public class ColorOutputNode : Node
    {
        public Color DisplayColor { get; private set; }

        public ColorOutputNode()
        {
            Name = "Color Output";
            AddInput("R");
            AddInput("G");
            AddInput("B");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float r = Math.Clamp(Inputs[0].GetValue(), 0, 1);
            float g = Math.Clamp(Inputs[1].GetValue(), 0, 1);
            float b = Math.Clamp(Inputs[2].GetValue(), 0, 1);
            DisplayColor = new Color(r, g, b);
        }
    }

    public class BeepOutputNode : Node
    {
        public bool ShouldPlay { get; private set; }
        public string SoundName { get; set; } = "Beep";
        public float Pitch { get; private set; }
        public float Volume { get; private set; }
        private bool _lastTriggerState = false;

        public BeepOutputNode()
        {
            Name = "Beep Output";
            AddInput("Trigger");
            AddInput("Pitch");
            AddInput("Volume");
        }

        public override void Evaluate(GameTime gameTime)
        {
            ShouldPlay = false; // Reset every frame
            bool currentTrigger = Inputs[0].GetValue() > 0.5f;

            Pitch = Math.Clamp(Inputs[1].GetValue(), -1.0f, 1.0f);
            if (Inputs[2].ConnectedSources.Count > 0)
                Volume = Math.Clamp(Inputs[2].GetValue(), 0.0f, 1.0f);
            else
                Volume = 1.0f;

            if (currentTrigger && !_lastTriggerState)
            {
                ShouldPlay = true;
                Pitch = Math.Clamp(Inputs[1].GetValue(), -1.0f, 1.0f);
                Volume = Math.Clamp(Inputs[2].GetValue(), 0.0f, 1.0f);
            }
            _lastTriggerState = currentTrigger;
        }
    }

    // A Script Importer Node
    public class ScriptImporterNode : Node
    {
        public string Script { get; set; } = "";

        public ScriptImporterNode()
        {
            Name = "Script Importer";
        }

        public override void Evaluate(GameTime gameTime)
        {
        }
    }

    // 4. The Graph Manager (The Engine)
    public class GraphEngine
    {
        public List<Node> Nodes { get; set; } = new List<Node>();

        public void Connect(Node sourceNode, int sourceIndex, Node targetNode, int targetIndex)
        {
            var sourcePort = sourceNode.Outputs[sourceIndex];
            var targetPort = targetNode.Inputs[targetIndex];
            if (!targetPort.ConnectedSources.Contains(sourcePort))
                targetPort.ConnectedSources.Add(sourcePort);
        }

        // The "Game Loop"
        public void Tick(GameTime gameTime)
        {
            // In a real engine, you need to sort nodes by dependency (Topological Sort)
            // or run multiple passes to propagate signals correctly.
            // For simplicity, we iterate linearly here.
            foreach (var node in Nodes)
            {
                node.Evaluate(gameTime);
            }
        }
    }

    // --- MONOGAME IMPLEMENTATION ---
    public class ToyConGame : Game
    {
        public static Rectangle ClientBounds { get; private set; }
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel; // Used for drawing lines and rectangles
        private SpriteFont _font;
        private List<string> _availableSounds = new List<string>();

        private GraphEngine _engine;

        // Visual State
        private Dictionary<Node, Rectangle> _nodeRects = new Dictionary<Node, Rectangle>();
        private Node _draggedNode = null;
        private Point _dragOffset;
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
                    ("Beep", () => new BeepOutputNode())
                }},
                { "Import", new List<(string, Func<Node>)> {
                    ("Script", () => new ScriptImporterNode())
                }}
            };

            base.Initialize();
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
            var keyboardState = Keyboard.GetState();
            bool clicked = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            bool rightClicked = mouseState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released;

            if (rightClicked)
            {
                double now = gameTime.TotalGameTime.TotalSeconds;
                if (now - _lastRightClickTime < DoubleClickTime)
                {
                    Node nodeToDelete = null;
                    foreach (var kvp in _nodeRects)
                    {
                        if (kvp.Value.Contains(mousePos))
                        {
                            nodeToDelete = kvp.Key;
                            break;
                        }
                    }
                    if (nodeToDelete != null) DeleteNode(nodeToDelete);
                    _lastRightClickTime = 0;
                }
                else
                    _lastRightClickTime = now;
            }

            if (_inspectedNode != null)
            {
                UpdateOverlay(mouseState, keyboardState, clicked);
                _prevKeyboardState = keyboardState;
                _prevMouseState = mouseState;
                base.Update(gameTime);
                return;
            }

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
                    // Check Nodes (Inspection)
                    foreach (var kvp in _nodeRects)
                    {
                        if (kvp.Value.Contains(mousePos))
                        {
                            _inspectedNode = kvp.Key;
                            _inputValueBuffer = "";
                            if (_inspectedNode is ConstantNode c) _inputValueBuffer = c.StoredValue.ToString();
                            if (_inspectedNode is CounterNode cnt) _inputValueBuffer = cnt.Value.ToString();
                            if (_inspectedNode is ScriptImporterNode sn) _inputValueBuffer = sn.Script;
                            _draggedNode = null;
                            doubleClickHandled = true;
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

            if (!uiCaptured && mouseState.LeftButton == ButtonState.Pressed)
            {
                if (_draggedNode == null)
                {
                    // Hit Test
                    foreach (var kvp in _nodeRects)
                    {
                        if (kvp.Value.Contains(mousePos))
                        {
                            _draggedNode = kvp.Key;
                            _dragOffset = mousePos - kvp.Value.Location;
                            break;
                        }
                    }
                }
                else
                {
                    // Dragging
                    var rect = _nodeRects[_draggedNode];
                    rect.Location = mousePos - _dragOffset;
                    _nodeRects[_draggedNode] = rect;
                }
            }
            else
            {
                _draggedNode = null;
            }

            _prevKeyboardState = keyboardState;
            _prevMouseState = mouseState;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 30, 30)); // Dark background

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
                Color color = Color.Gray;
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

                _spriteBatch.Draw(_pixel, rect, color);

                // Border
                DrawHollowRect(_spriteBatch, rect, Color.White);

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
                            SpawnNode(items[i].Factory());
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
                return;
            }

            // Keyboard Close
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _inspectedNode = null;
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
                    cNode.StoredValue = (float)Math.Floor(cNode.StoredValue - 1.0f);
                    _inputValueBuffer = cNode.StoredValue.ToString();
                }
                if (clicked && plusRect.Contains(mousePos))
                {
                    cNode.StoredValue = (float)Math.Floor(cNode.StoredValue + 1.0f);
                    _inputValueBuffer = cNode.StoredValue.ToString();
                }

                HandleTextInput(keyboard, ref _inputValueBuffer);
                if (float.TryParse(_inputValueBuffer, out float val)) cNode.StoredValue = val;
            }
            else if (_inspectedNode is MathNode mNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                if (clicked && btnRect.Contains(mousePos)) mNode.Op = (MathNode.Operation)(((int)mNode.Op + 1) % 4);
                if (IsKeyPressed(keyboard, Keys.Right)) mNode.Op = (MathNode.Operation)(((int)mNode.Op + 1) % 4);
                if (IsKeyPressed(keyboard, Keys.Left)) mNode.Op = (MathNode.Operation)(((int)mNode.Op + 3) % 4);
            }
            else if (_inspectedNode is LogicNode lNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                if (clicked && btnRect.Contains(mousePos)) lNode.Type = (LogicNode.LogicType)(((int)lNode.Type + 1) % 5);
                if (IsKeyPressed(keyboard, Keys.Right)) lNode.Type = (LogicNode.LogicType)(((int)lNode.Type + 1) % 5);
                if (IsKeyPressed(keyboard, Keys.Left)) lNode.Type = (LogicNode.LogicType)(((int)lNode.Type + 4) % 5);
            }
            else if (_inspectedNode is KeyNode kNode)
            {
                Rectangle btnRect = new Rectangle(x, y, 200, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    Keys[] commonKeys = { Keys.Space, Keys.A, Keys.B, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.W, Keys.S };
                    int idx = Array.IndexOf(commonKeys, kNode.Key);
                    idx = (idx + 1) % commonKeys.Length;
                    kNode.Key = commonKeys[idx];
                    kNode.Name = $"Key ({kNode.Key})";
                }

                Keys[] pressed = keyboard.GetPressedKeys();
                foreach (var k in pressed)
                {
                    if (!_prevKeyboardState.IsKeyDown(k) && k != Keys.Escape)
                    {
                        kNode.Key = k;
                        kNode.Name = $"Key ({kNode.Key})";
                        break;
                    }
                }
            }
            else if (_inspectedNode is TimerNode tNode)
            {
                Rectangle resetRect = new Rectangle(x, y, 100, 30);
                if (clicked && resetRect.Contains(mousePos)) tNode.ElapsedTime = 0;
                if (IsKeyPressed(keyboard, Keys.R)) tNode.ElapsedTime = 0;
            }
            else if (_inspectedNode is CounterNode cntNode)
            {
                Rectangle minusRect = new Rectangle(x, y, 30, 30);
                Rectangle plusRect = new Rectangle(x + 100, y, 30, 30);
                if (clicked && minusRect.Contains(mousePos))
                {
                    cntNode.Value--;
                    _inputValueBuffer = cntNode.Value.ToString();
                }
                if (clicked && plusRect.Contains(mousePos))
                {
                    cntNode.Value++;
                    _inputValueBuffer = cntNode.Value.ToString();
                }

                HandleTextInput(keyboard, ref _inputValueBuffer);
                if (float.TryParse(_inputValueBuffer, out float val)) cntNode.Value = val;
            }
            else if (_inspectedNode is ButtonNode btnNode)
            {
                Rectangle toggleRect = new Rectangle(x, y, 200, 30);
                if (clicked && toggleRect.Contains(mousePos)) btnNode.IsToggle = !btnNode.IsToggle;
                if (IsKeyPressed(keyboard, Keys.Space)) btnNode.IsToggle = !btnNode.IsToggle;
            }
            else if (_inspectedNode is ScriptImporterNode scriptNode)
            {
                HandleScriptInput(keyboard, ref _inputValueBuffer);
                scriptNode.Script = _inputValueBuffer;

                Rectangle btnRect = new Rectangle(x, y + 150, 100, 30);
                if (clicked && btnRect.Contains(mousePos))
                {
                    ParseAndGenerateGraph(scriptNode.Script);
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

                if (clicked)
                {
                    int idx = _availableSounds.IndexOf(beepNode.SoundName);
                    if (idx == -1) idx = 0;

                    if (prevRect.Contains(mousePos)) idx--;
                    if (nextRect.Contains(mousePos)) idx++;

                    if (idx < 0) idx = _availableSounds.Count - 1;
                    if (idx >= _availableSounds.Count) idx = 0;

                    beepNode.SoundName = _availableSounds[idx];
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
            if (_draggedNode == node) _draggedNode = null;

            foreach (var n in _engine.Nodes)
            {
                foreach (var input in n.Inputs)
                {
                    input.ConnectedSources.RemoveAll(s => s.ParentNode == node);
                }
            }
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
        private void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness = 1f)
        {
            var edge = end - start;
            var angle = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(_pixel, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness), null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private void DrawHollowRect(SpriteBatch sb, Rectangle rect, Color color)
        {
            int t = 2; // thickness
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color); // Top
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - t, rect.Width, t), color); // Bottom
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color); // Left
            sb.Draw(_pixel, new Rectangle(rect.X + rect.Width - t, rect.Y, t, rect.Height), color); // Right
        }
    }

    public static class Program
    {
        static void Main(string[] args)
        {
            using var game = new ToyConGame();
            game.Run();
        }
    }
}
