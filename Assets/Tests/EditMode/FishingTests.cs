using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class FishingTests
    {
        static readonly MaterialDefinition Rod = Gear.Data.FindMaterial(World.FishingRod);
        static readonly MaterialDefinition Minnow = Gear.Data.FindMaterial("cave_minnow");

        // A room with a pool along its right side; the player stands beside it, facing it, rod in the bag.
        static World Pond(out Player player, bool rod = true)
        {
            var map = Maps.FromRows(
                "#######",
                "#....~#",
                "#....~#",
                "#....~#",
                "#######");
            var world = new World(map, TileMap.Center(4, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            if (rod) player.Inventory.AddMaterial(Rod);
            return world;
        }

        static List<SimEvent> RunUntil(World world, System.Func<List<SimEvent>, bool> done, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds) && !done(events); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        static bool Has(List<SimEvent> events, int amount) => events.Any(e => e.Type == SimEventType.Fishing && World.FishingKind(e.Amount) == amount);

        [Test]
        public void CastWaitForTheBiteAndLandAFishOfTheBiome()
        {
            var world = Pond(out var player);
            Assert.IsTrue(world.Fish(player.Id));
            var events = world.TakeEvents();
            var cast = events.Single(e => e.Type == SimEventType.Fishing);
            Assert.AreEqual(World.FishingCast, cast.Amount);
            Assert.AreEqual(TileMap.Center(5, 2), cast.Position, "into the water ahead");

            events = RunUntil(world, e => Has(e, World.FishingBite), World.BiteMaxSeconds + 0.5f);
            Assert.IsTrue(Has(events, World.FishingBite), "a bite within the time");
            Reels.Land(world, player);
            Assert.IsTrue(Has(world.TakeEvents(), World.FishingCaught));
            Assert.That(player.Inventory.CountOf(Minnow), Is.InRange(1, 2), "the plain cave's fish");
            Assert.IsNull(player.Fishing);
        }

        [Test]
        public void BaitIsUsedOnACastAndTheFishBitesSooner()
        {
            var bait = Gear.Data.FindMaterial(World.Bait);
            var world = Pond(out var player);
            player.Inventory.AddMaterial(bait, 2);
            Assert.IsTrue(world.Fish(player.Id));
            Assert.AreEqual(1, player.Inventory.CountOf(bait), "one bait on the hook");
            // 70% of the longest wait is enough with bait.
            var events = RunUntil(world, e => Has(e, World.FishingBite), World.BiteMaxSeconds * 0.7f + 0.5f);
            Assert.IsTrue(Has(events, World.FishingBite));
        }

        [Test]
        public void TheFishingContestPaysAPrizeForEachFish()
        {
            Assert.AreEqual(DayEvent.FishingContest, World.EventOf(6));
            Assert.AreEqual(DayEvent.Market, World.EventOf(5));
            Assert.AreEqual(DayEvent.FishingContest, World.EventOf(30), "rarer than market day");
            var coin = Gear.Data.FindMaterial(World.Coin);
            var world = Pond(out var player);
            world.RestoreClock(5 * Calendar.TicksPerDay + Calendar.TicksPerDay / 6);
            Assert.AreEqual(DayEvent.FishingContest, world.EventToday);
            Assert.IsTrue(world.Fish(player.Id));
            RunUntil(world, e => Has(e, World.FishingBite), World.BiteMaxSeconds + 0.5f);
            Reels.Land(world, player);
            var events = world.TakeEvents();
            Assert.IsTrue(Has(events, World.FishingPrize));
            Assert.GreaterOrEqual(player.Inventory.CountOf(coin), World.ContestPrizePerDifficulty);
        }

        [Test]
        public void TooLateTheFishGetsAway()
        {
            var world = Pond(out var player);
            world.Fish(player.Id);
            var events = RunUntil(world, e => Has(e, World.FishingEscaped), World.BiteMaxSeconds + World.BiteSeconds + 0.5f);
            Assert.IsTrue(Has(events, World.FishingEscaped));
            Assert.AreEqual(0, player.Inventory.CountOf(Minnow));
        }

        [Test]
        public void PressingEarlyWalkingOffOrBeingHitReelsTheLineIn()
        {
            var world = Pond(out var player);
            world.Fish(player.Id);
            world.TakeEvents();
            Assert.IsTrue(world.Fish(player.Id), "before the bite");
            Assert.IsTrue(Has(world.TakeEvents(), World.FishingReeled));

            world.Fish(player.Id);
            world.MovePlayer(player.Id, player.Position + new Vec2(-1, 0), new Vec2(-1, 0), 10);
            Assert.IsTrue(Has(RunUntil(world, e => Has(e, World.FishingReeled), 0.2f), World.FishingReeled), "walked off");

            world.MovePlayer(player.Id, TileMap.Center(4, 2), new Vec2(1, 0), 10);
            world.Fish(player.Id);
            world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(-0.9f, 0));
            Assert.IsTrue(Has(RunUntil(world, e => Has(e, World.FishingReeled), 3), World.FishingReeled), "hit");
        }

        [Test]
        public void NoRodOrNoWaterNoFishing()
        {
            var world = Pond(out var player, rod: false);
            Assert.IsFalse(world.Fish(player.Id), "no rod");
            player.Inventory.AddMaterial(Rod);
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 1);
            Assert.IsFalse(world.Fish(player.Id), "facing the floor");
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        public void PoolsLieAtTheEdgesOfTheCavesOutsideTheLavaFields(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var map = world.Map;
            var water = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)))
                .Where(t => map[t.x, t.y] == TileType.Water).ToList();
            Assert.Greater(water.Count, 60);
            Assert.IsTrue(water.All(t => world.Biomes[t.x, t.y] != Biome.Lava));
            Assert.IsTrue(water.All(t => world.Arenas.All(a => Vec2.Distance(TileMap.Center(t.x, t.y), a) > world.ArenaRadius + 2)));
            Assert.IsTrue(water.All(t => Vec2.Distance(TileMap.Center(t.x, t.y), world.Spawn) > 9));
            // Many can be fished from the floor beside them.
            int shore = water.Count(t => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => map[t.x + d.Item1, t.y + d.Item2] == TileType.Floor));
            Assert.Greater(shore, 30);
            // Every biome with water has its own fish.
            foreach (var biome in water.Select(t => world.Biomes[t.x, t.y]).Distinct())
            {
                Assert.IsTrue(Gear.Data.Materials.Any(m => m.Fish == biome), biome.ToString());
            }
        }
    }
}
