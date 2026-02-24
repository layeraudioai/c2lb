namespace ToyConEngine {  
    // A Math Node (Add, Subtract, Multiply)
    public class MathNode : Node
    {
        public enum Operation { Add, Subtract, Multiply, Divide, Abs, Select }
        public Operation Op { get; set; }

        public MathNode(Operation op)
        {
            Name = $"Math ({op})";
            Op = op;
            if (op == Operation.Abs)
            {
                AddInput("A");
            }
            else if (op == Operation.Select)
            {
                AddInput("Cond");
                AddInput("True");
                AddInput("False");
            }
            else
            {
                AddInput("A");
                AddInput("B");
            }
            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float result = 0f;

            if (Op == Operation.Abs)
            {
                result = Math.Abs(Inputs[0].GetValue());
            }
            else if (Op == Operation.Select)
            {
                float cond = Inputs[0].GetValue();
                result = (Math.Abs(cond) > 0.001f) ? Inputs[1].GetValue() : Inputs[2].GetValue();
            }
            else
            {
                float a = Inputs[0].GetValue();
                float b = Inputs[1].GetValue();
                switch (Op)
                {
                    case Operation.Add: result = a + b; break;
                    case Operation.Subtract: result = a - b; break;
                    case Operation.Multiply: result = a * b; break;
                    case Operation.Divide: result = (Math.Abs(b) > 0.001f) ? a / b : 0f; break;
                }
            }

            Outputs[0].SetValue(result);
        }
    }
}