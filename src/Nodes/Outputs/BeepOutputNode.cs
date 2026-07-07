using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class BeepOutputNode : Node
    {
        public bool ShouldPlay { get; private set; }
        public float Volume => Inputs.Count > 1 ? Inputs[1].GetValue() : 1.0f;
        public float Pitch => Inputs.Count > 2 ? Inputs[2].GetValue() : 0.0f;
        public string SoundName { get; set; } = "KICK-01";
        private bool _prevTrigger;

        public BeepOutputNode()
        {
            Name = "Beep";
            AddInput("Play");
            AddInput("Volume");
            AddInput("Pitch");
        }

        public override void Evaluate(GameTime gameTime)
        {
            bool trigger = Inputs[0].GetValue() > 0;
            ShouldPlay = trigger && !_prevTrigger;
            _prevTrigger = trigger;
        }
    }
}