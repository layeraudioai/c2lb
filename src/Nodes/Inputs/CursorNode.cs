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

        public override void Evaluate(GameTime gameTime) => Outputs[0].SetValue(Mouse.GetState().X);
    }
}