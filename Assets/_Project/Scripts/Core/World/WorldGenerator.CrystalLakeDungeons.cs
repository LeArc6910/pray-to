using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static partial class WorldGenerator
    {
        // The generator's sixth version (2026-11-25, Docs/BiomeRework.md 묶음 B3): version 5 with the lairs of the crystal
        // cave's and the underground lake's mid-bosses (the geode golem, the moss turtle; WorldGenerator.Cellars.cs) and
        // the lake's shallows round its water (LayShallows). A world made at version 5 or before keeps its lairs and its
        // map as made (their hashes are pinned in B3bTests). CrystalLakeVersion itself is in WorldGenerator.CrystalLake.cs,
        // with the two biomes' props and landmarks laid in the same version.

        // The shallows' own random stream.
        public const ulong ShallowStream = 77;
        // Of the lake's open floor beside its shallows (a tile past the water's edge), this share is shallow too, so the
        // shallows' edge wanders.
        public const float ShallowSpread = 0.45f;
        // No shallows this near the spawn, a place's plaza (and its edge) or an arena.
        const int ShallowClear = 3;

        // Whether a biome's lairs are laid in a world of this version, of those whose dungeon has a mid-boss in person for
        // master: version 4 the dirt cave and the mushroom forest, version 5 the ice cave and the sand cave too, version
        // 6 the crystal cave and the underground lake as well (their dungeons' masters came with it).
        static bool LairsIn(Biome biome, int version) => biome switch
        {
            Biome.Cave or Biome.Mushroom => true,
            Biome.Crystal or Biome.Lake => version >= CrystalLakeVersion,
            _ => version >= IceSandVersion,
        };

        // How many of the zones' rings (World.ZoneRing at the map's scale) a biome's lairs keep from the spawn: two for the
        // ice cave and the sand cave (their mid-bosses are of the third zone, and the sand cave holds the first boss's
        // arena in some worlds: the first hour's bot met a temple guardian there), and for the crystal cave and the
        // lake (the fourth zone's, past the crystal queen), one for the rest.
        static int LairRingsOf(Biome biome) => biome == Biome.Ice || biome == Biome.Sand || biome == Biome.Crystal || biome == Biome.Lake ? 2 : 1;

        // The lake's shallows (4-1 지하 호수: "얕은 물, 걸을 수 있고 속도 70%"): its open floor beside water becomes
        // shallow water, and a share of the floor beside that. Only plain floor turns (not a road, a place's paving, a
        // camp's things or a prop), never by the spawn, a plaza or an arena. Laid last but the pocket: nothing above
        // reads it, and a world before version 6 has none.
        static void LayShallows(GeneratedWorld world, ulong seed)
        {
            var map = world.Map;
            var biomes = world.Biomes;
            var random = new GameRandom(seed, ShallowStream);
            var spawn = world.Spawn;
            var places = (world.Places ?? new List<PlaceSite>()).Select(p => (Center: p.Center, Reach: p.Radius + ShallowClear)).ToList();
            var arenas = world.Arenas.Select(a => (Center: a, Reach: (float)ArenaRadius + ShallowClear)).ToList();
            bool Kept(int x, int y)
            {
                var at = TileMap.Center(x, y);
                if (Vec2.Distance(at, spawn) <= 6 + ShallowClear) return true;
                foreach (var p in places) if (Vec2.Distance(at, p.Center) <= p.Reach) return true;
                foreach (var a in arenas) if (Vec2.Distance(at, a.Center) <= a.Reach) return true;
                return false;
            }
            bool Beside(int x, int y, TileType type) =>
                map.InBounds(x + 1, y) && map[x + 1, y] == type || map.InBounds(x - 1, y) && map[x - 1, y] == type
                || map.InBounds(x, y + 1) && map[x, y + 1] == type || map.InBounds(x, y - 1) && map[x, y - 1] == type;

            // The edge: every open floor tile of the lake beside its water, gathered first so the second ring is not
            // laid beside a tile turned in the same pass.
            var edge = new List<(int X, int Y)>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                    if (map[x, y] == TileType.Floor && biomes[x, y] == Biome.Lake && Beside(x, y, TileType.Water) && !Kept(x, y)) edge.Add((x, y));
            foreach (var (x, y) in edge) map[x, y] = TileType.ShallowWater;
            var past = new List<(int X, int Y)>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                    if (map[x, y] == TileType.Floor && biomes[x, y] == Biome.Lake && Beside(x, y, TileType.ShallowWater) && !Kept(x, y)) past.Add((x, y));
            foreach (var (x, y) in past)
                if (random.NextFloat() < ShallowSpread) map[x, y] = TileType.ShallowWater;
        }
    }
}
