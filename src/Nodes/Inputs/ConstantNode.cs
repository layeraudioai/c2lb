namespace ToyConEngine{
    // A Constant Number Node
    public class ConstantNode : Node
    {
        public float StoredValue { get; set; }

        public ConstantNode(float value)
        {
            Name = "Constant";
            StoredValue = value;
            AddOutput("Out");
        }

        public override void Evaluate(GameTime gameTime)
        {
            // Always output the stored value
            Outputs[0].SetValue(StoredValue);
        }
    }
}