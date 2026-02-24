namespace ToyConEngine {
    // A Script Importer Node
    public class ScriptImporterNode : Node
    {
        public string Script { get; set; } = "";

        public ScriptImporterNode()
        {
            Name = "Script Importer";
        }

        public override void Evaluate(GameTime gameTime)
        {
        }
    }
}