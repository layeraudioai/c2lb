using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ToyConEngine
{
    public class ToyOutputNode : Node
    {
        public int Index { get; set; } = 0;
        public ToyOutputNode()
        {
            Name = "Toy Output";
            Inputs = new List<InputPort> { new InputPort() { ParentNode = this } };
        }
        public override void Evaluate(GameTime gameTime) { }
    }
}