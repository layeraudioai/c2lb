using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class CounterNode : Node
    {
        public float Value { get; set; }
        private bool _prevInc;
        private bool _prevDec;

        public CounterNode()
        {
            Name = "Counter";
            AddInput("Inc");
            AddInput("Dec");
            AddInput("Reset");
            AddOutput("Count");
        }

        public override void Evaluate(GameTime gameTime)
        {
            bool inc = Inputs[0].GetValue() > 0;
            bool dec = Inputs[1].GetValue() > 0;
            if (Inputs[2].GetValue() > 0) Value = 0;
            if (inc && !_prevInc) Value++;
            if (dec && !_prevDec) Value--;
            _prevInc = inc;
            _prevDec = dec;
            Outputs[0].SetValue(Value);
        }
    }
}