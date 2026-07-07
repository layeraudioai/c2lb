namespace ToyConEngine
{
    public class OutputPort
    {
        public string Name { get; set; } = string.Empty;
        public Node? ParentNode { get; set; }
        public float Value { get; private set; }

        public void SetValue(float value)
        {
            Value = value;
        }
    }
}