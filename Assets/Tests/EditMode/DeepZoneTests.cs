using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DeepZoneTests
    {
        static readonly BossDefinition Warden = Bosses.Shipped.Bosses[0];
        static readonly BossDefinition Queen = Bosses.Shipped.Bosses.Single(b => b.Id == "crystal_queen");

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        [TestCase(4UL)]
        [TestCase(7UL)]
        public void TheDeepArenaLiesBeyondTheFirstAndCanBeReached(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            Assert.AreEqual(2, world.Arenas.Count);
            Assert.AreEqual(world.Arena, world.Arenas[0]);
            var deep = world.Arenas[1];

            Assert.Greater(Vec2.Distance(deep, world.Spawn), 60, "in the deep zone");
            Assert.Greater(Vec2.Distance(deep, world.Spawn), Vec2.Distance(world.Arena, world.Spawn));
            Assert.Less(Vec2.Distance(deep, world.Arena), Vec2.Distance(deep, world.Spawn), "on the first arena's side");
            Assert.IsFalse(Collision.Overlaps(world.Map, deep, world.ArenaRadius - 1), "its floor is clear");
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(TileMap.Cell(deep.X), TileMap.Cell(deep.Y)));
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        public void MithrilIsOnlyFoundFarOut(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var mithril = Enumerable.Range(0, world.Map.Height)
                .SelectMany(y => Enumerable.Range(0, world.Map.Width).Select(x => (X: x, Y: y)))
                .Where(t => world.Map[t.X, t.Y] == TileType.MithrilOre)
                .ToList();

            Assert.Greater(mithril.Count, 20);
            Assert.IsTrue(mithril.All(t => Vec2.Distance(TileMap.Center(t.X, t.Y), world.Spawn) >= 59));
        }

        [Test]
        public void EachBossWaitsInItsOwnArena()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var arenas = WorldGenerator.Generate(7).Arenas;

            Assert.AreEqual(2, host.World.Bosses.Count);
            Assert.AreEqual(arenas[0], host.World.Bosses.Single(b => b.Definition == Warden).Position);
            Assert.AreEqual(arenas[1], host.World.Bosses.Single(b => b.Definition == Queen).Position);
        }

        [Test]
        public void TheQueenOpensTheMithrilStep()
        {
            Assert.AreEqual(20, Queen.DropLevel, "mithril gear is level 20");
            Assert.Greater(Queen.Health, Warden.Health);
            Assert.IsTrue(Queen.Attacks.Any(a => a.Summon?.Id == "crystal_bat"));
            Assert.AreEqual("queen_heart", Queen.MaterialId);

            var mithrilOre = Tiles.Shipped.Find(TileType.MithrilOre);
            Assert.IsTrue(mithrilOre.Drillable);
            Assert.Greater(mithrilOre.Light, 0, "the veins glow in the dark");
            var bar = Gear.Data.Recipes.Single(r => r.Id == "mithril_bar");
            Assert.AreEqual(TileType.Furnace, bar.Station);
            var sword = Gear.Data.Recipes.Single(r => r.Id == "mithril_sword");
            Assert.IsTrue(sword.Ingredients.Any(i => i.Material.Id == "queen_heart"));
            Assert.AreEqual(20, sword.Item.Level);
        }

        [Test]
        public void DeepMonstersOnlyLiveInTheDeepZone()
        {
            var world = new World(Maps.Room(130), TileMap.Center(65, 65), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 400, SpawnDistance = 5 });

            Assert.AreEqual(3, world.ZoneLevelAt(world.Spawn + new Vec2(61, 0)));
            var deep = world.Enemies.Where(e => e.Definition.Level >= 3).ToList();
            Assert.IsNotEmpty(deep);
            Assert.IsTrue(deep.All(e => Vec2.Distance(e.Position, world.Spawn) >= 60));
        }
    }
}
