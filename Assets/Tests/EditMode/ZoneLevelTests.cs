using NUnit.Framework;

namespace Game.Core.Tests
{
    // The levels a spot is for (Docs/ZoneLevels.md).
    public class ZoneLevelTests
    {
        [Test]
        public void TheZoneIsTheWorldsAndTheRangeGrowsWithItAndTheBand()
        {
            var host = new WorldHost(2, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.NewWorldVersion },
                new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
            var world = host.World;
            foreach (var at in new[] { world.Spawn, world.Spawn + new Vec2(90, 0), world.Spawn + new Vec2(-150, 210), world.Spawn + new Vec2(301, 5) })
                Assert.AreEqual(world.ZoneLevelAt(at), Zones.ZoneAt(world.Spawn, at, world.MapScale), at.ToString());

            Assert.AreEqual((3, 7), Zones.LevelRange(Biome.Cave, 1), "copper's level 5, the first zone");
            Assert.AreEqual((13, 17), Zones.LevelRange(Biome.Ice, 2), "iron's 13, a zone further");
            Assert.AreEqual((32, 36), Zones.LevelRange(Biome.Abyss, 2), "obsidian's 32");
            Assert.AreEqual("Lv 3~7", Zones.Text(Zones.LevelRange(Biome.Cave, 1)));
            Assert.AreEqual((10, 14), Zones.LevelRange(12));

            var range = Zones.LevelRange(Biome.Ice, 2);
            Assert.AreEqual(0, Zones.Danger(13, range));
            Assert.AreEqual(0, Zones.Danger(30, range));
            Assert.AreEqual(1, Zones.Danger(10, range), "a few short: orange");
            Assert.AreEqual(2, Zones.Danger(8, range), "far short: red");
        }
    }
}
