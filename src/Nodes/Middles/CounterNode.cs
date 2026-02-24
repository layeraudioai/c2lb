namespace ToyConEngine {
    // A Counter Node
    public class CounterNode : Node
    {
        public float Value { get; set; } = 0;
        private bool _lastCountUpState = false;
        private bool _lastCountDownState = false;

        public CounterNode()
        {
            Name = "Counter";
            AddInput("Count Up");
            AddInput("Count Down");
            AddInput("Reset");
            AddOutput("Value");
        }

        public override void Evaluate(GameTime gameTime)
        {
            bool countUp = Inputs[0].GetValue() > 0.5f;
            bool countDown = Inputs[1].GetValue() > 0.5f;
            bool reset = Inputs[2].GetValue() > 0.5f;

            if (reset)
            {
                Value = 0;
            }
            else
            {
                if (countUp && !_lastCountUpState) Value++;
                if (countDown && !_lastCountDownState) Value--;
            }

            _lastCountUpState = countUp;
            _lastCountDownState = countDown;
            Outputs[0].SetValue(Value);
        }
    }
}