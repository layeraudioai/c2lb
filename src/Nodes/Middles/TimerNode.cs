namespace ToyConEngine {
    // A Timer Node
    public class TimerNode : Node
    {
        public float ElapsedTime { get; set; } = 0f;

        public TimerNode()
        {
            Name = "Timer";
            AddOutput("Time");
        }

        public override void Evaluate(GameTime gameTime)
        {
            ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            Outputs[0].SetValue(ElapsedTime);
        }
    }
}