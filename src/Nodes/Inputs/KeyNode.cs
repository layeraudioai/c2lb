namespace ToyConEngine {
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
}