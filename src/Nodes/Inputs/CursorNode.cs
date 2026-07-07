using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ToyConEngine
{
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
            var mouseState = Mouse.GetState();
            Outputs[0].SetValue(Math.Max(0, mouseState.X));
            Outputs[1].SetValue(Math.Max(0, mouseState.Y));
        }
    }
}