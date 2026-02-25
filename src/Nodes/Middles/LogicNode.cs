using Microsoft.Xna.Framework;
using System;

namespace ToyConEngine {
    // A Logic Node (AND, NOT)
    public class LogicNode : Node
    {
        public enum LogicType { And, Not, GreaterThan, LessThan, Or, Xor }
        public LogicType Type { get; set; }

        public LogicNode(LogicType type)
        {
            Name = $"Logic ({type})";
            Type = type;

            AddInput("In 1");
            if (type != LogicType.Not) AddInput("In 2"); // NOT only needs 1 input

            AddOutput("Result");
        }

        public override void Evaluate(GameTime gameTime)
        {
            float a = Inputs[0].GetValue();
            // Logic in GBG usually treats > 0 as True
            bool boolA = Math.Abs(a) > 0.001f;

            float result = 0.0f;

            switch (Type)
            {
                case LogicType.And:
                    float b = Inputs[1].GetValue();
                    bool boolB = Math.Abs(b) > 0.001f;
                    result = (boolA && boolB) ? 1.0f : 0.0f;
                    break;
                case LogicType.Not:
                    result = (!boolA) ? 1.0f : 0.0f;
                    break;
                case LogicType.GreaterThan:
                    float c = Inputs[1].GetValue();
                    result = (a > c) ? 1.0f : 0.0f;
                    break;
                case LogicType.LessThan:
                    float f = Inputs[1].GetValue();
                    result = (a < f) ? 1.0f : 0.0f;
                    break;
                case LogicType.Or:
                    float d = Inputs[1].GetValue();
                    bool boolD = Math.Abs(d) > 0.001f;
                    result = (boolA || boolD) ? 1.0f : 0.0f;
                    break;
                case LogicType.Xor:
                    float e = Inputs[1].GetValue();
                    bool boolE = Math.Abs(e) > 0.001f;
                    result = (boolA ^ boolE) ? 1.0f : 0.0f;
                    break;
            }

            Outputs[0].SetValue(result);
        }
    }
}