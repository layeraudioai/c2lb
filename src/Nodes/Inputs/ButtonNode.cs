using Microsoft.Xna.Framework;

namespace ToyConEngine {
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
}