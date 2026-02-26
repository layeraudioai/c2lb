using Microsoft.Xna.Framework;

namespace ToyConEngine
{
    public class ScreenNode : Node
    {
        public const int Width = 64;
        public const int Height = 64;
        public Color[] Buffer;

        public ScreenNode()
        {
            Name = "Screen";
            AddInput("X");
            AddInput("Y");
            AddInput("R");
            AddInput("G");
            AddInput("B");
            AddInput("Draw");
            AddInput("Clear");
            
            Buffer = new Color[Width * Height];
            for (int i = 0; i < Buffer.Length; i++) Buffer[i] = Color.Black;
        }

        public override void Evaluate(GameTime gameTime)
        {
            if (Inputs[6].GetValue() > 0) // Clear
            {
                for (int i = 0; i < Buffer.Length; i++) Buffer[i] = Color.Black;
            }

            if (Inputs[5].GetValue() > 0) // Draw
            {
                int x = (int)Inputs[0].GetValue();
                int y = (int)Inputs[1].GetValue();
                float r = Inputs[2].GetValue();
                float g = Inputs[3].GetValue();
                float b = Inputs[4].GetValue();

                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    Buffer[y * Width + x] = new Color(r, g, b);
                }
            }
        }
    }
}