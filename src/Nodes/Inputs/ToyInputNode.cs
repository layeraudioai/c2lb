using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ToyConEngine
{
    public class ToyInputNode : Node
    {
        public int Index { get; set; } = 0;
        public ToyInputNode()
        {
            Name = "Toy Input";
            Outputs = new List<OutputPort> { new OutputPort() { ParentNode = this } };
        }
        public override void Evaluate(GameTime gameTime) { }
    }
}