using Microsoft.Xna.Framework;
using System;

namespace ToyConEngine
{
    public class MathNode : Node
    {
        public enum Operation { Add, Subtract, Multiply, Divide, Abs, Select }
        public Operation Op { get; set; }

        public MathNode(Operation op)
        {
            Name = $"Math ({op})";
            Op = op;
            AddInput("A");
            if (op != Operation.Abs) AddInput("B");
            if (op == Operation.Select) AddInput("C");
            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float a = Inputs[0].GetValue();
            float b = Inputs.Count > 1 ? Inputs[1].GetValue() : 0;
            float result = 0;
            switch (Op)
            {
                case Operation.Add: result = a + b; break;
                case Operation.Subtract: result = a - b; break;
                case Operation.Multiply: result = a * b; break;
                case Operation.Divide: result = b != 0 ? a / b : 0; break;
                case Operation.Abs: result = Math.Abs(a); break;
                case Operation.Select: result = a > 0 ? b : (Inputs.Count > 2 ? Inputs[2].GetValue() : 0); break;
            }
            Outputs[0].SetValue(result);
        }
    }
}