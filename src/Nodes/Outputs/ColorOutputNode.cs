namespace ToyConEngine {    
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
}