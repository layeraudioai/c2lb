namespace ToyConEngine {
    public class BeepOutputNode : Node
    {
        public bool ShouldPlay { get; private set; }
        public string SoundName { get; set; } = "Beep";
        public float Pitch { get; private set; }
        public float Volume { get; private set; }
        private bool _lastTriggerState = false;

        public BeepOutputNode()
        {
            Name = "Beep Output";
            AddInput("Trigger");
            AddInput("Pitch");
            AddInput("Volume");
        }

        public override void Evaluate(GameTime gameTime)
        {
            ShouldPlay = false; // Reset every frame
            bool currentTrigger = Inputs[0].GetValue() > 0.5f;

            Pitch = Math.Clamp(Inputs[1].GetValue(), -1.0f, 1.0f);
            if (Inputs[2].ConnectedSources.Count > 0)
                Volume = Math.Clamp(Inputs[2].GetValue(), 0.0f, 1.0f);
            else
                Volume = 1.0f;

            if (currentTrigger && !_lastTriggerState)
            {
                ShouldPlay = true;
                Pitch = Math.Clamp(Inputs[1].GetValue(), -1.0f, 1.0f);
                Volume = Math.Clamp(Inputs[2].GetValue(), 0.0f, 1.0f);
            }
            _lastTriggerState = currentTrigger;
        }
    }
}