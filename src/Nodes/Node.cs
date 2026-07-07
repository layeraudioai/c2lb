using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ToyConEngine
{
    public abstract class Node
    {
        public string Name { get; set; } = string.Empty;
        public List<InputPort> Inputs { get; set; } = new();
        public List<OutputPort> Outputs { get; set; } = new();

        public abstract void Evaluate(GameTime gameTime);

        protected void AddInput(string name)
        {
            Inputs.Add(new InputPort { Name = name, ParentNode = this });
        }

        protected void AddOutput(string name)
        {
            Outputs.Add(new OutputPort { Name = name, ParentNode = this });
        }
    }
}