// src/Core/ToyNode.cs

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ToyConEngine
{
    public class ToyNode : Node
    {
        public string FilePath { get; set; }
        public GraphEngine InternalEngine { get; private set; } = new GraphEngine();
        public Dictionary<Node, Rectangle> InternalRects { get; } = new Dictionary<Node, Rectangle>();
        
        public ToyNode()
        {
            Name = "Toy Project";
            Inputs = new List<InputPort>();
            Outputs = new List<OutputPort>();
            RefreshPorts();
        }

        public override void Evaluate(GameTime gameTime)
        {
            // Map Inputs to Internal ToyInputNodes
            foreach (var inputNode in InternalEngine.Nodes.OfType<ToyInputNode>())
            {
                if (inputNode.Index < Inputs.Count)
                {
                    float val = 0f;
                    foreach (var source in Inputs[inputNode.Index].ConnectedSources)
                        val += source.Value;
                    
                    if (inputNode.Outputs.Count > 0)
                        inputNode.Outputs[0].Value = val;
                }
            }

            InternalEngine.Tick(gameTime);

            // Map Internal ToyOutputNodes to Outputs
            foreach (var outputNode in InternalEngine.Nodes.OfType<ToyOutputNode>())
            {
                if (outputNode.Index < Outputs.Count)
                {
                    float val = 0f;
                    if (outputNode.Inputs.Count > 0)
                    {
                        foreach (var source in outputNode.Inputs[0].ConnectedSources)
                            val += source.Value;
                    }
                    Outputs[outputNode.Index].Value = val;
                }
            }
        }
        
        public void RefreshPorts()
        {
            while (Inputs.Count < 10) Inputs.Add(new InputPort() { ParentNode = this });
            while (Inputs.Count > 10) Inputs.RemoveAt(Inputs.Count - 1);

            while (Outputs.Count < 10) Outputs.Add(new OutputPort() { ParentNode = this });
            while (Outputs.Count > 10) Outputs.RemoveAt(Outputs.Count - 1);
        }

        public ScreenNode GetScreenNode()
        {
            return InternalEngine.Nodes.OfType<ScreenNode>().FirstOrDefault();
        }
    }
}
