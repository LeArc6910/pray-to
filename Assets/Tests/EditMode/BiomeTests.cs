using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BiomeTests
    {
        static Dictionary<Biome, int> Count(BiomeMap biomes)
        {
            var counts = new Dictionary<Biome, int>();
            for (int y = 0; y < biomes.Height; y++)
            {
                for (int x = 0; x < biomes.Width; x++) counts[biomes[x, y]] = counts.GetValueOrDefault(biomes[x, y]) + 1;
            }
            return counts;
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(42UL)]
        public void EachCornerOfTheMapIsItsOwnBiomeAroundPlainCave(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var biomes = world.Biomes;
            Assert.AreEqual(Biome.Cave, biomes[world.SpawnX, world.SpawnY]);
            Assert.AreEqual(Biome.Crystal, biomes.At(world.Arenas[1]), "the deep arena is in the crystal caverns");

            var deep = world.Arenas[1] - world.Spawn;
            var opposite = world.Spawn - deep;
            Assert.AreEqual(Biome.Lava, biomes.At(opposite), "the lava fields lie opposite");
            // The two other corners, partway out: the mushroom forest and the ice caves. The far end of one of them is
            // the abyss, with the fourth arena.
            var sides = new[] { world.Spawn + new Vec2(deep.X, -deep.Y) * 0.7f, world.Spawn + new Vec2(-deep.X, deep.Y) * 0.7f }.Select(biomes.At).ToList();
            CollectionAssert.AreEquivalent(new[] { Biome.Mushroom, Biome.Ice }, sides);
            Assert.AreEqual(Biome.Abyss, biomes.At(world.Arenas[3]));
            var abyss = world.Arenas[3] - world.Spawn;
            Assert.AreEqual(0, Vec2.Distance(abyss, new Vec2(deep.X, -deep.Y)) * Vec2.Distance(abyss, new Vec2(-deep.X, deep.Y)), 0.01f,
                "in one of the corners beside the lava fields");
            // The far end of the other one is the underground lake, with the fifth arena.
            Assert.AreEqual(Biome.Lake, biomes.At(world.Arenas[4]));
            Assert.AreEqual(-1, Vec2.Dot(world.Arenas[4] - world.Spawn, abyss) / (abyss.Length * abyss.Length), 0.01f, "the corner across from the abyss");
            // The crystal quarter's near band is the sand caves, with the sixth arena, beyond the first and nearer than
            // the deep one (2026-11-09).
            Assert.AreEqual(Biome.Sand, biomes.At(world.Arenas[5]));
            Assert.Greater(Vec2.Dot(world.Arenas[5] - world.Spawn, deep), 0, "on the deep arena's side");
            Assert.Less(Vec2.Distance(world.Arenas[5], world.Spawn), Vec2.Distance(world.Arenas[1], world.Spawn));
            Assert.Greater(Vec2.Distance(world.Arenas[5], world.Arenas[0]), 2 * world.ArenaRadius, "apart from the first arena");
            // The mushroom forest's near band holds the seventh arena, the venom spider's hollow (2026-11-13), on the side
            // away from the deep arena's, apart from the first and the sand arena.
            Assert.AreEqual(Biome.Mushroom, biomes.At(world.Arenas[6]));
            Assert.Less(Vec2.Distance(world.Arenas[6], world.Spawn), Vec2.Distance(world.Arenas[1], world.Spawn));
            Assert.Greater(Vec2.Distance(world.Arenas[6], world.Arenas[0]), 2 * world.ArenaRadius, "apart from the first arena");
            Assert.Greater(Vec2.Distance(world.Arenas[6], world.Arenas[5]), 2 * world.ArenaRadius, "apart from the sand arena");
            for (int other = 1; other <= 4; other++)
                Assert.Greater(Vec2.Distance(world.Arenas[6], world.Arenas[other]), 2 * world.ArenaRadius, $"apart from arena {other}");

            var counts = Count(biomes);
            foreach (Biome biome in System.Enum.GetValues(typeof(Biome)))
            {
                Assert.Greater(counts.GetValueOrDefault(biome), biome == Biome.Abyss || biome == Biome.Lake ? 300 : 1000, biome.ToString());
            }
        }

        [Test]
        public void TheSameSeedMakesTheSameBiomesAndTheBorderWinds()
        {
            var a = WorldGenerator.Generate(5).Biomes;
            var b = WorldGenerator.Generate(5).Biomes;
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++) Assert.AreEqual(a[x, y], b[x, y]);
            }
            // Along a straight line out from the spawn the plain cave ends at different distances.
            var world = WorldGenerator.Generate(5);
            var ends = new HashSet<int>();
            for (int angle = 0; angle < 360; angle += 15)
            {
                var direction = new Vec2(System.MathF.Cos(angle * System.MathF.PI / 180), System.MathF.Sin(angle * System.MathF.PI / 180));
                int distance = Enumerable.Range(1, 60).First(d => a.At(world.Spawn + direction * d) != Biome.Cave);
                Assert.That(distance, Is.InRange(BiomeMap.CaveRadius - 13, BiomeMap.CaveRadius + 13));
                ends.Add(distance);
            }
            Assert.Greater(ends.Count, 4, "the border is not a circle");
        }

        [Test]
        public void OnlyTheLavaFieldsAndTheAbyssChangedTheCaves()
        {
            // Biomes only say what kind of cave a tile is in. The lava fields carved their arena, a tunnel from the
            // spawn and pools of lava, all in the plain cave and the lava fields; the abyss its arena, veins and a
            // tunnel from the lava arena. The caves of the other three biomes, away from that tunnel, are what they were.
            // The lake (2026-11-09) took the far end of a side corner, carved its arena and a tunnel from the deep
            // arena and set pools into rock: its biome counts with the others, away from its arena and tunnel.
            var world = WorldGenerator.Generate(7);
            var tunnel = NearLine(world, world.Arenas[2], world.Arenas[3], 2);
            var lakeTunnel = NearLine(world, world.Arenas[1], world.Arenas[4], 2);
            var lake = world.Arenas[4];
            // The sand caves (2026-11-09) took the crystal quarter's near band, carved their arena and a tunnel from the
            // first arena and dried the pools in them: they count as crystal did, away from that arena and tunnel.
            var sandTunnel = NearLine(world, world.Arenas[0], world.Arenas[5], 2);
            var sand = world.Arenas[5];
            // The mushroom hollow (2026-11-13) carved its arena and a tunnel from the spawn, and made the tiles round it
            // mushroom forest: they are left out like the sand's.
            var hollowTunnel = NearLine(world, world.Spawn, world.Arenas[6], 2);
            var hollow = world.Arenas[6];
            int floors = 0;
            for (int y = 0; y < world.Map.Height; y++)
            {
                for (int x = 0; x < world.Map.Width; x++)
                {
                    var biome = world.Biomes[x, y];
                    bool carved = tunnel[y * world.Map.Width + x] || lakeTunnel[y * world.Map.Width + x] || sandTunnel[y * world.Map.Width + x]
                        || hollowTunnel[y * world.Map.Width + x]
                        || Vec2.Distance(TileMap.Center(x, y), lake) <= world.ArenaRadius + 1 || Vec2.Distance(TileMap.Center(x, y), sand) <= world.ArenaRadius + 1
                        || Vec2.Distance(TileMap.Center(x, y), hollow) <= 16;
                    bool elsewhere = (biome == Biome.Mushroom || biome == Biome.Ice || biome == Biome.Crystal || biome == Biome.Lake || biome == Biome.Sand) && !carved;
                    if (elsewhere && world.Map[x, y] == TileType.Floor) floors++;
                }
            }
            Assert.AreEqual(FloorsElsewhereOfSeedSeven, floors);
        }

        // Floor tiles of seed 7 in the mushroom forest, ice caves and crystal caverns away from the abyss tunnel,
        // counted on these tiles with the code from before the abyss (5367; before the lava fields the same biomes
        // held 6073), so a change to those caves shows up here. Since the lake (2026-11-09) the count leaves out its
        // arena and tunnel too, whose floors were in the 5367 before; the lake itself only carved those and set pools
        // into rock, so the rest of these caves are the same tiles. Since the sand caves (2026-11-09, 4855 before) it
        // leaves out their arena and tunnel from the first arena as well; the sand only carved those and turned pools
        // to rock, and the tiles round its arena counted as sand whatever their biome was. Since the mushroom hollow
        // (2026-11-13, 4522 before) it leaves out that arena's surroundings (16 tiles round it, which it made mushroom
        // forest) and its tunnel from the spawn too; outside those, the floors are the tiles they were.
        const int FloorsElsewhereOfSeedSeven = 4075;

        // Tiles within reach of the straight line between two points, drawn the way tunnels are.
        static bool[] NearLine(GeneratedWorld world, Vec2 from, Vec2 to, int reach)
        {
            var near = new bool[world.Map.Width * world.Map.Height];
            int x = TileMap.Cell(from.X), y = TileMap.Cell(from.Y), toX = TileMap.Cell(to.X), toY = TileMap.Cell(to.Y);
            int dx = System.Math.Abs(toX - x), dy = -System.Math.Abs(toY - y), stepX = x < toX ? 1 : -1, stepY = y < toY ? 1 : -1, error = dx + dy;
            while (true)
            {
                for (int ty = y - reach; ty <= y + reach; ty++)
                {
                    for (int tx = x - reach; tx <= x + reach; tx++)
                    {
                        if (world.Map.InBounds(tx, ty)) near[ty * world.Map.Width + tx] = true;
                    }
                }
                if (x == toX && y == toY) return near;
                int doubled = 2 * error;
                if (doubled >= dy)
                {
                    error += dy;
                    x += stepX;
                }
                if (doubled <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        [Test]
        public void BiomeMonstersOnlyLiveInTheirBiome()
        {
            var biomes = BiomeMap.Make(3, 130, 130, 65, 65, (1, 1), true);
            var world = new World(Maps.Room(130), TileMap.Center(65, 65), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 600, SpawnDistance = 5 },
                biomes: biomes);

            var placed = world.Enemies.Where(e => e.Definition.Biome != null).ToList();
            Assert.IsNotEmpty(placed);
            Assert.IsTrue(placed.All(e => biomes.At(e.Position) == e.Definition.Biome), "each in its own biome");
            foreach (string id in new[] { "myconid", "glow_bat", "ice_slime", "frost_wisp", "crystal_bat", "crystal_wisp", "magma_slime", "fire_bat" })
            {
                Assert.IsTrue(world.Enemies.Any(e => e.Definition.Id == id), id);
            }
            Assert.IsTrue(world.Enemies.Any(e => e.Definition.Biome == null && biomes.At(e.Position) != Biome.Cave), "everyday monsters live everywhere");
        }

        [Test]
        public void TheBiomeColumnIsChecked()
        {
            var data = MonsterSheet.Parse(Monsters.Header.Replace("weight\n", "weight,biome\n") + "a,가,1,1,0,0,1,0.3,0,0.5,0.5,1,1,swamp\n");
            Assert.IsTrue(data.Errors.Any(e => e.Contains("swamp")), string.Join("\n", data.Errors));
            Assert.AreEqual(Biome.Crystal, Monsters.Shipped.Find("crystal_bat").Biome);
            Assert.IsNull(Monsters.Shipped.Find("slime").Biome);
        }

        [Test]
        public void LampsLightTheCaveFromTheMushroomForest()
        {
            var lamp = Gear.Data.FindMaterial("glow_lamp");
            Assert.AreEqual(TileType.Lamp, lamp.Places);
            Assert.Greater(Tiles.Shipped.Find(TileType.Lamp).Light, Tiles.Shipped.Find(TileType.Torch).Light);
            var recipe = Gear.Data.Recipes.Single(r => r.Id == "glow_lamp");
            Assert.IsTrue(recipe.Ingredients.Any(i => i.Material.Id == "glow_spore"));
        }
    }
}
