using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class MidiImporterNode : Node
    {
        public string MidiPath { get; set; } = "";
        public string LastImportMessage { get; set; } = "No file selected";

        public MidiImporterNode()
        {
            Name = "MIDI";
            AddOutput("Imported");
        }

        public override void Evaluate(GameTime gameTime) { }
    }
}
