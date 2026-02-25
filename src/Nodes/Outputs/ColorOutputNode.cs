using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class ColorOutputNode : Node
    {
        public Color DisplayColor { get; private set; }

        public ColorOutputNode()
        {
            Name = "Color";
            AddInput("R");
            AddInput("G");
            AddInput("B");
        }

        public override void Evaluate(GameTime gameTime) => DisplayColor = new Color(Inputs[0].GetValue(), Inputs[1].GetValue(), Inputs[2].GetValue());
    }
}