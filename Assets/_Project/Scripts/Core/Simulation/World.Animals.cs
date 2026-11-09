using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The coop, the barn and the crab pot (Docs/Animals.md): machines that look after themselves once given what they eat.
    public sealed partial class World
    {
        public const string Chicken = "chicken";
        public const string Cow = "cow";
        public const string Sheep = "sheep";
        public const string Wool = "wool";
        public const string Feed = "feed";
        public const string Egg = "egg";
        public const string Milk = "milk";
        public const string Bait = "bait";
        // How many chickens a coop holds, and how much feed.
        public const int CoopChickens = 4;
        public const int CoopFeed = 20;
        // Affection (2026-11-09, Docs/Animals.md): petting a house's animals once a day raises it by one up to
        // AffectionMost; a day's products come out silver or gold more often the fonder they are. Left unpetted more
        // than AffectionPatience days, it falls by one each time they give.
        public const int AffectionMost = 10;
        public const int AffectionPatience = 2;

        // A house for animals: which animal, how many fit, what they eat and how much for each thing they give.
        public sealed class AnimalHouse
        {
            internal AnimalHouse(TileType tile, string animal, int most, string product, int feedEach)
            {
                Tile = tile;
                Animal = animal;
                Most = most;
                Product = product;
                FeedEach = feedEach;
            }

            public TileType Tile { get; }
            public string Animal { get; }
            public int Most { get; }
            public string Product { get; }
            public int FeedEach { get; }
        }

        public static readonly IReadOnlyList<AnimalHouse> AnimalHouses = new[]
        {
            new AnimalHouse(TileType.Coop, Chicken, CoopChickens, Egg, 1),
            new AnimalHouse(TileType.Barn, Cow, 2, Milk, 2),
            new AnimalHouse(TileType.Fold, Sheep, 3, Wool, 1),
        };

        public static AnimalHouse HouseOf(TileType tile) => AnimalHouses.FirstOrDefault(h => h.Tile == tile);

        // The animals in a coop or barn.
        public static int AnimalsIn(Machine house) =>
            HouseOf(house.Kind) is AnimalHouse kind ? house.Inputs.Where(p => p.Key.Id == kind.Animal).Sum(p => p.Value) : 0;

        public static int ChickensIn(Machine coop) => AnimalsIn(coop);

        // A coop or barn takes its animals up to how many fit and feed up to CoopFeed; a crab pot takes bait up to its
        // capacity.
        static bool AnimalsTake(Machine machine, MaterialDefinition material)
        {
            int have = machine.Input.GetValueOrDefault(material);
            if (machine.Kind == TileType.CrabPot) return material.Id == Bait && have < machine.Definition.Capacity;
            var house = HouseOf(machine.Kind);
            if (house == null) return false;
            return material.Id == house.Animal ? have < house.Most : material.Id == Feed && have < CoopFeed;
        }

        // Once a day (the house's seconds, real time) every animal gives its product, eating its feed for it. The day
        // only runs while there are animals, feed and room. What they give goes out of the front like a smelter's bars.
        void RunAnimals(Machine house)
        {
            var kind = HouseOf(house.Kind);
            var feed = house.Input.Keys.FirstOrDefault(m => m.Id == Feed);
            var product = Loot?.Materials.FirstOrDefault(m => m.Id == kind.Product);
            int fed = feed == null ? 0 : house.Input[feed] / kind.FeedEach;
            int giving = Math.Min(AnimalsIn(house), fed);
            // A hungry house waits where its day was, rather than starting it over.
            bool ready = product != null && giving > 0 && Count(house.Output) + giving <= house.Definition.Capacity;
            if (ready && ++house.Timer >= house.Definition.Ticks)
            {
                house.Timer = 0;
                Add(house.Input, feed, -giving * kind.FeedEach);
                if (house.Affection > 0 && Today - house.PettedDay > AffectionPatience) house.Affection--;
                for (int i = 0; i < giving; i++)
                {
                    var made = AnimalQuality(product, house.Affection);
                    Add(house.Output, made, 1);
                    CountMade(made, 1);
                }
            }
            SendOut(house, house.Front);
        }

        // The quality of one thing an animal gives: gold 3% for each point of affection above 4, silver 5% a point,
        // so at the most 18% gold and 50% silver. Plain when the product has no quality.
        internal MaterialDefinition AnimalQuality(MaterialDefinition product, int affection)
        {
            if (product.Variants.Count < MaterialDefinition.MaxQuality || affection <= 0) return product;
            int roll = _random.Next(100);
            int gold = Math.Max(0, affection - 4) * 3;
            int silver = gold + affection * 5;
            return roll < gold ? product.Variants[1] : roll < silver ? product.Variants[0] : product;
        }

        // Pets the animals of the house ahead: once a day for each house, its affection rises by one. False when there
        // is no house with animals ahead or they were petted today.
        public bool PetAnimals(int playerId)
        {
            var player = FindPlayer(playerId);
            var house = player == null || player.IsDead ? null : MachineFacing(player.Position, player.Facing);
            if (house == null || HouseOf(house.Kind) == null || AnimalsIn(house) == 0 || house.PettedDay == Today) return false;
            house.PettedDay = Today;
            house.Affection = Math.Min(AffectionMost, house.Affection + 1);
            _events.Add(new SimEvent(SimEventType.AnimalsPetted, player.Id, amount: house.Affection, position: TileMap.Center(house.X, house.Y)));
            return true;
        }

        // What a house's setting carries over the network (WorldHost.SendMachines): its animals, its affection × 16,
        // and 256 when petted today. AnimalsOf, AffectionOf and PettedOf read it back.
        public static int HouseSetting(int animals, int affection, bool pettedToday) => animals + 16 * affection + (pettedToday ? 256 : 0);
        public static int AnimalsOf(int setting) => setting < 0 ? 0 : setting % 16;
        public static int AffectionOf(int setting) => setting < 0 ? 0 : setting / 16 % 16;
        public static bool PettedOf(int setting) => setting >= 256;

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
