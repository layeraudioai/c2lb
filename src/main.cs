namespace ToyConEngine
{
    //main
    public static class Program
    {
        static void Main(string[] args)
        {
            using var game = new ToyConGame();
            game.Run();
        }
    }
}