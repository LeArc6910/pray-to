using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The coop and the crab pot (Docs/Animals.md): machines that look after themselves once given what they eat.
    public sealed partial class World
    {
        public const string Chicken = "chicken";
        public const string Feed = "feed";
        public const string Egg = "egg";
        public const string Bait = "bait";
        // How many chickens a coop holds, and how much feed.
        public const int CoopChickens = 4;
        public const int CoopFeed = 20;

        public static int ChickensIn(Machine coop) => coop.Inputs.Where(p => p.Key.Id == Chicken).Sum(p => p.Value);

        // A coop takes chickens up to CoopChickens and feed up to CoopFeed; a crab pot takes bait up to its capacity.
        static bool AnimalsTake(Machine machine, MaterialDefinition material)
        {
            int have = machine.Input.GetValueOrDefault(material);
            if (machine.Kind == TileType.CrabPot) return material.Id == Bait && have < machine.Definition.Capacity;
            return material.Id == Chicken ? have < CoopChickens : material.Id == Feed && have < CoopFeed;
        }

        // Once a day (the coop's seconds, real time) every chicken lays an egg, eating one feed for it. The day only
        // runs while there are chickens and feed and room for the eggs. Eggs go out of the front like a smelter's bars.
        void RunCoop(Machine coop)
        {
            var feed = coop.Input.Keys.FirstOrDefault(m => m.Id == Feed);
            var egg = Loot?.Materials.FirstOrDefault(m => m.Id == Egg);
            int chickens = ChickensIn(coop);
            int laying = Math.Min(chickens, feed == null ? 0 : coop.Input[feed]);
            // A hungry coop waits where its day was, rather than starting it over.
            bool ready = egg != null && laying > 0 && Count(coop.Output) + laying <= coop.Definition.Capacity;
            if (ready && ++coop.Timer >= coop.Definition.Ticks)
            {
                coop.Timer = 0;
                Add(coop.Input, feed, -laying);
                Add(coop.Output, egg, laying);
                CountMade(egg, laying);
            }
            SendOut(coop, coop.Front);
        }

        // Every so often a baited pot beside water catches one of what pots catch (materials.csv fish "pot"), eating a
        // bait. On dry land it does nothing.
        void RunCrabPot(Machine pot)
        {
            var bait = pot.Input.Keys.FirstOrDefault(m => m.Id == Bait);
            var catches = Loot?.Materials.Where(m => m.PotCatch).ToList();
            bool ready = bait != null && catches != null && catches.Count > 0 && BesideWater(pot.X, pot.Y) && Count(pot.Output) < pot.Definition.Capacity;
            if (ready && ++pot.Timer >= pot.Definition.Ticks)
            {
                pot.Timer = 0;
                Add(pot.Input, bait, -1);
                var caught = catches[_random.Next(catches.Count)];
                Add(pot.Output, caught, 1);
                CountMade(caught, 1);
            }
            SendOut(pot, pot.Front);
        }

        // Whether a tile has water right beside it (not on a diagonal).
        public bool BesideWater(int x, int y) => Machine.Steps.Any(s => Map.InBounds(x + s.X, y + s.Y) && Map[x + s.X, y + s.Y] == TileType.Water);
    }
}
