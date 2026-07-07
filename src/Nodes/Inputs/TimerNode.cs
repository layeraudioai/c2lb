using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class TimerNode : Node
    {
        public float ElapsedTime { get; set; }

        public TimerNode()
        {
            Name = "Timer";
            AddInput("Reset");
            AddOutput("Time");
        }

        public override void Evaluate(GameTime gameTime)
        {
            bool shouldReset = Inputs[0].GetValue() > 0;
            if (shouldReset)
            {
                ElapsedTime = 0f;
            }
            else
            {
                ElapsedTime += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
            }

            Outputs[0].SetValue(ElapsedTime);
        }
    }
}