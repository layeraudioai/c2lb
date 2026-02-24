 namespace ToyConEngine {
    // Input Port (Connection Points)
    public class InputPort
    {
        public string Name { get; set; }
        public Node ParentNode { get; set; }
        public List<OutputPort> ConnectedSources { get; set; } = new List<OutputPort>();

        public float GetValue()
        {
            // If nothing is connected, return default (0)
            if (ConnectedSources.Count == 0) return 0.0f;
            return ConnectedSources.Max(s => s.Value);
        }
    }
}