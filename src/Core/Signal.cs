namespace ToyConEngine
{
    // The Data Type (The "Volt" or Signal)
    // In GBG, almost everything is a float (0.0 to 1.0 or arbitrary numbers).
    public struct Signal
    {
        public float Value;
        public Signal(float value) { Value = value; }
    }
}