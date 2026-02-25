using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class ConstantNode : Node
    {
        public float StoredValue { get; set; }

        public ConstantNode(float value)
        {
            Name = "Constant";
            StoredValue = value;
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime) => Outputs[0].SetValue(StoredValue);
    }
}