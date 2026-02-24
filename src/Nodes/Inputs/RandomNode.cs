namespace ToyConEngine {
    // A Random Node
    public class RandomNode : Node
    {
        private static Random _rng = new Random();

        public RandomNode()
        {
            Name = "Random";
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            Outputs[0].SetValue((float)_rng.NextDouble());
        }
    }
}