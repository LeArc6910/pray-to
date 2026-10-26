using System;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // dotnet run -- --dice [seed] [rolls]   rolls a d6 with GameRandom and prints how often each face came up
    // Balance simulations (loot drops, bot parties against bosses, farming against combat income) go here.
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--dice")
            {
                ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : (ulong)Environment.TickCount64;
                int rolls = args.Length > 2 ? int.Parse(args[2]) : 6000;
                RollDice(seed, rolls);
                return;
            }

            Console.WriteLine("usage: dotnet run -- --dice [seed] [rolls]");
        }

        static void RollDice(ulong seed, int rolls)
        {
            Console.WriteLine($"seed = {seed}, rolls = {rolls}");
            var random = new GameRandom(seed);
            var counts = new int[6];
            for (int i = 0; i < rolls; i++) counts[random.Next(6)]++;

            int widest = counts.Max();
            for (int face = 0; face < 6; face++)
            {
                string bar = new string('#', counts[face] * 40 / widest);
                Console.WriteLine($"  {face + 1}: {counts[face],6}  {bar}");
            }
        }
    }
}
