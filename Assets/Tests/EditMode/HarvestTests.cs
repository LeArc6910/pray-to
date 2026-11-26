using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Version 9 (Docs/Harvest.md): the biomes' things to pick or break, in place of the floor's forage.
    public class HarvestTests
    {
        static WorldHost Host(ulong seed, int version) =>
            new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = version },
                new WorldConfig { EnemyTarget = 0, Forage = true }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        [Test]
        public void VersionNineStandsEachBiomesKindInItsRoomsOverVersionEightsFloor()
        {
            Assert.AreEqual(9, WorldGenerator.HarvestVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.HarvestVersion);
            var eight = WorldGenerator.Generate(4, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.HuntVersion, Tiles.Shipped.Terrain);
            var nine = WorldGenerator.Generate(4, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.HarvestVersion, Tiles.Shipped.Terrain);
            int laid = 0;
            var biomes = new System.Collections.Generic.HashSet<Biome>();
            for (int y = 0; y < nine.Map.Height; y++)
            {
                for (int x = 0; x < nine.Map.Width; x++)
                {
                    if (nine.Map[x, y] == eight.Map[x, y]) continue;
                    Assert.IsTrue(HarvestProps.IsGrown(nine.Map[x, y]) && eight.Map[x, y] == TileType.Floor, $"({x},{y}) {eight.Map[x, y]} -> {nine.Map[x, y]}");
                    Assert.AreEqual(HarvestProps.Of(nine.Biomes[x, y]), nine.Map[x, y]);
                    biomes.Add(nine.Biomes[x, y]);
                    laid++;
                }
            }
            Assert.Greater(laid, 300);
            Assert.AreEqual(8, biomes.Count, "every biome's kind");
            Assert.AreEqual(eight.Rooms.HuntingGrounds, nine.Rooms.HuntingGrounds);
            // Each grown kind is dug for its biome's forage; the spent ones are walked and never dug.
            foreach (var pair in World.ForageOf)
            {
                var kind = HarvestProps.Of(pair.Key);
                Assert.AreEqual(pair.Value, Tiles.Shipped.Find(kind).Material.Id, kind.ToString());
                Assert.IsNull(Tiles.Shipped.Find(HarvestProps.SpentOf(kind)));
            }
        }

        [Test]
        public void APickedPatchGivesItsMushroomsAndGrowsBackAtDawnWithNobodyOnIt()
        {
            var host = Host(4, WorldGenerator.HarvestVersion);
            var world = host.World;
            var map = world.Map;
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            // The patch nearest the spawn, approached from below.
            var (px, py) = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)))
                .Where(t => map[t.x, t.y] == TileType.MushroomPatch && map[t.x, t.y - 1] == TileType.Floor)
                .OrderBy(t => Vec2.Distance(TileMap.Center(t.x, t.y), world.Spawn)).First();
            player.Position = TileMap.Center(px, py - 1);
            player.Facing = new Vec2(0, 1);
            int before = player.Inventory.CountOf(Gear.Data.Materials.First(m => m.Id == "mushroom"));
            world.SetAttacking(player.Id, true);
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < World.TicksPerSecond && map[px, py] == TileType.MushroomPatch; i++)
            {
                world.Step();
                seen.AddRange(world.TakeEvents().Where(e => e.Source == player.Id).Select(e => $"{e.Type}:{e.Amount}"));
            }
            Assert.IsTrue(seen.Contains($"Dug:{World.DugThrough}"), string.Join(" ", seen));
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(TileType.MushroomPatchPicked, map[px, py], "one swing picks it");
            Assert.GreaterOrEqual(player.Inventory.CountOf(Gear.Data.Materials.First(m => m.Id == "mushroom")), before + 2);
            Assert.IsFalse(map.IsSolid(px, py), "what is left is walked over");
            Assert.AreEqual(1, world.SpentHarvests);

            // The same day it stays picked; the next, it grows back, but not under a player.
            for (int i = 0; i < 2 * World.TicksPerSecond; i++) world.Step();
            Assert.AreEqual(TileType.MushroomPatchPicked, map[px, py]);
            player.Position = TileMap.Center(px, py);
            world.RestoreClock(Calendar.NextMorning(world.Clock) + 10);
            for (int i = 0; i < 2 * World.TicksPerSecond; i++) world.Step();
            Assert.AreEqual(TileType.MushroomPatchPicked, map[px, py], "not while someone stands on it");
            player.Position = TileMap.Center(px, py - 2);
            for (int i = 0; i < 2 * World.TicksPerSecond; i++) world.Step();
            Assert.AreEqual(TileType.MushroomPatch, map[px, py], "grown back");
            Assert.AreEqual(0, world.SpentHarvests);
        }

        [Test]
        public void VersionNineLaysNoForageOnTheFloor()
        {
            var eight = Host(4, WorldGenerator.HuntVersion).World;
            var nine = Host(4, WorldGenerator.HarvestVersion).World;
            eight.AddPlayer("a", Gear.Wearing("copper_sword"));
            nine.AddPlayer("a", Gear.Wearing("copper_sword"));
            for (int i = 0; i < World.TicksPerSecond; i++)
            {
                eight.Step();
                nine.Step();
            }
            Assert.Greater(eight.ForageIds.Count, 0);
            Assert.AreEqual(0, nine.ForageIds.Count);
        }
    }
}
