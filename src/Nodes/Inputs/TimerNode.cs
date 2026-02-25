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
            if (Inputs[0].GetValue() > 0) ElapsedTime = 0;
            ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            Outputs[0].SetValue(ElapsedTime);
        }
    }
}