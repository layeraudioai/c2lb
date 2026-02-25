using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class TimerNode : Node
    {
        public float ElapsedTime { get; set; }

        public TimerNode()
        {
            Name = "Timer";
            AddOutput("Time");
        }

        public override void Evaluate(GameTime gameTime) => ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }
}