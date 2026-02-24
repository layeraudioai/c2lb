namespace ToyConEngine {
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
}