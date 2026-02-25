using Microsoft.Xna.Framework;
using System;

namespace ToyConEngine
{
    public class RandomNode : Node
    {
        private static readonly Random _random = new Random();

        public RandomNode()
        {
            Name = "Random";
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime) => Outputs[0].SetValue((float)_random.NextDouble());
    }
}