using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;

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
        public OutputPort ConnectedSource { get; set; }

        public float GetValue()
        {
            // If nothing is connected, return default (0)
            return ConnectedSource?.Value ?? 0.0f;
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
        public enum Operation { Add, Subtract, Multiply, Divide }
        public Operation Op { get; set; }

        public MathNode(Operation op)
        {
            Name = $"Math ({op})";
            Op = op;
            AddInput("A");
            AddInput("B");
            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float a = Inputs[0].GetValue();
            float b = Inputs[1].GetValue();
            float result = 0f;

            switch (Op)
            {
                case Operation.Add: result = a + b; break;
                case Operation.Subtract: result = a - b; break;
                case Operation.Multiply: result = a * b; break;
                case Operation.Divide: result = (Math.Abs(b) > 0.001f) ? a / b : 0f; break;
            }

            Outputs[0].SetValue(result);
        }
    }

    // A Logic Node (AND, NOT)
    public class LogicNode : Node
    {
        public enum LogicType { And, Not, GreaterThan, Or, Xor }
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

            if (currentTrigger && !_lastTriggerState)
            {
                ShouldPlay = true;
                Pitch = Math.Clamp(Inputs[1].GetValue(), -1.0f, 1.0f);
                Volume = Math.Clamp(Inputs[2].GetValue(), 0.0f, 1.0f);
            }
            _lastTriggerState = currentTrigger;
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
            targetPort.ConnectedSource = sourcePort;
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
        private SoundEffect _beepSound;

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
                _beepSound = Content.Load<SoundEffect>("Beep");
            }
            catch
            {
                // Font or sound not found
            }
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
            if (_beepSound != null)
            {
                foreach (var node in _engine.Nodes)
                {
                    if (node is BeepOutputNode beepNode && beepNode.ShouldPlay)
                    {
                        _beepSound.Play(beepNode.Volume, beepNode.Pitch, 0);
                    }
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
                                if (input.ConnectedSource != null)
                                {
                                    var startNode = input.ConnectedSource.ParentNode;
                                    int outputIndex = startNode.Outputs.IndexOf(input.ConnectedSource);
                                    Vector2 startPos = GetOutputPosition(startNode, outputIndex);
                                    Vector2 endPos = GetInputPosition(node, i);
                                    if (GetDistanceFromLineSegment(mousePos.ToVector2(), startPos, endPos) < 8f)
                                    {
                                        input.ConnectedSource = null;
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
                    if (input.ConnectedSource != null)
                    {
                        var startNode = input.ConnectedSource.ParentNode;
                        var endNode = node;

                        int outputIndex = startNode.Outputs.IndexOf(input.ConnectedSource);
                        Vector2 startPos = GetOutputPosition(startNode, outputIndex);
                        Vector2 endPos = GetInputPosition(endNode, i);

                        // Draw line from center of source to center of target
                        DrawLine(_spriteBatch, startPos, endPos, Color.Orange, 2);

                        // Draw Value
                        if (_font != null)
                        {
                            Vector2 mid = (startPos + endPos) / 2;
                            string val = input.ConnectedSource.Value.ToString("0.00");
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

        private char? KeyToChar(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
            if (key == Keys.OemPeriod || key == Keys.Decimal) return '.';
            if (key == Keys.OemMinus || key == Keys.Subtract) return '-';
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
                    if (input.ConnectedSource != null && input.ConnectedSource.ParentNode == node)
                        input.ConnectedSource = null;
                }
            }
        }

        private void SpawnNode(Node node)
        {
            _engine.Nodes.Add(node);
            var mousePos = Mouse.GetState().Position;
            int width = 100;
            int height = 60;
            if (node.Inputs.Count + node.Outputs.Count > 2)
            {
                width = 120;
                height = 80;
            }
            _nodeRects[node] = new Rectangle(mousePos.X, mousePos.Y, width, height);
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
