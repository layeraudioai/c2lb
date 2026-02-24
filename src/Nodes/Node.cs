 namespace ToyConEngine{
    // The Base Node (The "Nodon")
    public abstract class Node
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public List<InputPort> Inputs { get; set; } = new List<InputPort>();
        public List<OutputPort> Outputs { get; set; } = new List<OutputPort>();

        // The core logic: Read inputs, process, write to outputs
        public abstract void Evaluate(GameTime gameTime);

        protected void AddInput(string name) => Inputs.Add(new InputPort { Name = name, ParentNode = this });
        protected void AddOutput(string name) => Outputs.Add(new OutputPort { Name = name, ParentNode = this });
    }
 }