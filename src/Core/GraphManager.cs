using Microsoft.Xna.Framework;
using System.Collections.Generic;

//The Graph Manager (The Engine)
namespace ToyConEngine {
    public class GraphEngine
    {
        public List<Node> Nodes { get; set; } = new List<Node>();

        public void Connect(Node sourceNode, int sourceIndex, Node targetNode, int targetIndex)
        {
            var sourcePort = sourceNode.Outputs[sourceIndex];
            var targetPort = targetNode.Inputs[targetIndex];
            if (!targetPort.ConnectedSources.Contains(sourcePort))
                targetPort.ConnectedSources.Add(sourcePort);
        }

        // The "Game Loop"
        public void Tick(GameTime gameTime)
        {
            // In a real engine, you need to sort nodes by dependency (Topological Sort)
            // or run multiple passes to propagate signals correctly.
            // For simplicity, we iterate linearly here.
            foreach (var node in Nodes)
            {
                node.Evaluate(gameTime);
            }
        }
    }
}