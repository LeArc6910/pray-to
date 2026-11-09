using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ForageTests
    {
        [Test]
        public void EachDayEveryBiomeLaysItsForageAwayFromTheSpawnAndAWalkPicksItUp()
        {
            // 2026-11-09, Docs/Biomes.md 채집물. A plain room is all cave: its forage is the cave mushroom.
            var world = new World(Maps.Room(40), TileMap.Center(20, 20), Monsters.Shipped, 3,
                new WorldConfig { EnemyTarget = 0, Forage = true }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            world.Step();
            var first = world.ForageIds.ToList();
            Assert.AreEqual(World.ForagePerBiome, first.Count, "four of the cave's own");
            var mushroom = Gear.Data.FindMaterial(World.ForageOf[Biome.Cave]);
            foreach (int id in first)
            {
                var loot = world.FindLoot(id);
                Assert.AreSame(mushroom, loot.Material);
                Assert.GreaterOrEqual(Vec2.Distance(loot.Position, world.Spawn), 8f, "not at the spawn");
            }

            // Walked over, it comes into the bag like any drop.
            player.Position = world.FindLoot(first[0]).Position;
            world.Step();
            Assert.AreEqual(1, player.Inventory.CountOf(mushroom));

            // The next day lays fresh ones in place of what is left.
            world.RestoreClock(Calendar.TicksPerDay + 10);
            world.Step();
            var second = world.ForageIds.ToList();
            Assert.AreEqual(World.ForagePerBiome, second.Count);
            Assert.IsFalse(second.Intersect(first).Any(), "yesterday's went");
            Assert.IsFalse(world.GroundLoot.Any(l => first.Contains(l.Id)));
        }

        [Test]
        public void ForageIsOffUnlessTheWorldAsks()
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 20), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            world.Step();
            CollectionAssert.IsEmpty(world.GroundLoot);
        }
    }
}
