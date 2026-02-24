namespace ToyConEngine
{
    // Output Connection Ports (Connection Points)
    public class OutputPort
    {
        public string Name { get; set; }
        public Node ParentNode { get; set; }
        public float Value { get; private set; }

        // Update the value sitting on this port
        public void SetValue(float value)
        {
            Value = value;
        }
    }
}