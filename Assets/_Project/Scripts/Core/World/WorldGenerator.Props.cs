using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The biomes' props that are tiles (Docs/BiomeRework.md 4-3 막는 소품·캐거나 깨는 소품, 묶음 B1a): what blocks and
    // what is broken or picked. The small cover that is only looked at is Decor, worked out on the client.
    public static class BiomeProps
    {
        public static bool Is(TileType type) => type >= TileType.Stalagmite && type <= TileType.GiantCap;

        // A lamp, out or lit.
        public static bool IsLamp(TileType type) => type == TileType.UnlitLamp || type == TileType.LitLamp;

        // The props two tiles wide: their type is on the left tile and StructurePart on the right.
        public static bool Wide(TileType type) => Is(type) && Structures.Footprint(type).W > 1;

        // For a tile that is the right half of a wide prop, the tile its type is on.
        public static (int X, int Y)? AnchorOf(TileMap map, int x, int y)
        {
            if (!map.InBounds(x, y) || map[x, y] != TileType.StructurePart || !map.InBounds(x - 1, y) || !Wide(map[x - 1, y])) return null;
            return (x - 1, y);
        }

        // What a biome's props are and how often each, out of the sum of its weights. Clear: how many tiles of bare
        // floor it wants round its footprint (a way round it at least that wide).
        public readonly struct Kind
        {
            public Kind(TileType type, int weight, int clear = 1, bool big = false)
            {
                Type = type;
                Weight = weight;
                Clear = clear;
                Big = big;
            }

            public TileType Type { get; }
            public int Weight { get; }
            public int Clear { get; }
            // The large ones keep BigGap apart from each other.
            public bool Big { get; }
        }

        static readonly Kind[] Cave =
        {
            new Kind(TileType.Stalagmite, 36), new Kind(TileType.PitProp, 7), new Kind(TileType.TippedCart, 6), new Kind(TileType.OreSack, 10),
            new Kind(TileType.UnlitLamp, 7), new Kind(TileType.Boulder, 22, 2, big: true), new Kind(TileType.CaveIn, 14, 2, big: true),
        };

        static readonly Kind[] Mushroom =
        {
            new Kind(TileType.PillarCap, 30), new Kind(TileType.ShelfStump, 16), new Kind(TileType.SporeSac, 14), new Kind(TileType.GlowCaps, 22),
            new Kind(TileType.FallenLog, 22, 2, big: true), new Kind(TileType.GiantCap, 8, 3, big: true),
        };

        static readonly Kind[] None = new Kind[0];

        // The biomes that have their props so far (B1a: the dirt cave and the mushroom forest; the rest with their
        // own packages).
        public static IReadOnlyList<Kind> Of(Biome biome) => biome == Biome.Cave ? Cave : biome == Biome.Mushroom ? Mushroom : None;
    }

    public static partial class WorldGenerator
    {
        // The generator's third version (2026-11-24, Docs/BiomeRework.md 묶음 B1a): version 2's rooms with the biomes'
        // props laid over them last of all, and the hero's hut's garden fenced round its corner. A world saved at
        // version 2 stays as it was made.
        public const int PropVersion = 3;
        // The props' own random stream, so nothing laid before them changes.
        public const ulong PropStream = 57;
        // In a thousand tiles of floor fit for a prop, how many get one (before the clumps and the wall's pull).
        const int PropsPerThousand = 55;
        const int PropClump = 7;
        // Large props stand at least this far from each other, and nothing near the spawn.
        const int BigGap = 5;
        const int SpawnClear = 5;

        // Lays the props: each tile of bare floor of a biome that has props, in map order, rolls by its own hash (the
        // stream, the tile), more likely in a clump and three tiles off a wall (cave plants and rubble keep to the
        // walls); what passes is one of the biome's kinds, set down only where its footprint and the tiles round it
        // (two wide, three for a giant cap) are all bare floor. So every prop has open floor all round it: none shuts
        // a way, none touches another, a wall, a road, a door, a place, an arena's floor, a chest or a rail, and a
        // way under three tiles wide gets none. Kept clear besides: the spawn, the middle of each room, the middle three
        // tiles of every way between rooms, the chunks set into rooms, the dungeons' gates, the things to find and the
        // marks packs stand on, the places and the arenas.
        static void LayProps(GeneratedWorld world, ulong seed)
        {
            var map = world.Map;
            var biomes = world.Biomes;
            var graph = world.Rooms;
            if (graph == null) return;
            int width = map.Width, height = map.Height;
            var kept = new bool[width * height];
            void Keep(int cx, int cy, int reach)
            {
                for (int y = Math.Max(0, cy - reach); y <= Math.Min(height - 1, cy + reach); y++)
                    for (int x = Math.Max(0, cx - reach); x <= Math.Min(width - 1, cx + reach); x++)
                        kept[y * width + x] = true;
            }
            Keep(world.SpawnX, world.SpawnY, SpawnClear);
            foreach (var room in graph.Nodes)
            {
                Keep(room.AnchorX, room.AnchorY, 2);
                Keep(room.X, room.Y, room.Role == RoomRole.Gate ? room.Radius + 2 : 1);
                if (room.Box is (int left, int bottom, int w, int h))
                {
                    for (int y = Math.Max(0, bottom - 1); y <= Math.Min(height - 1, bottom + h); y++)
                        for (int x = Math.Max(0, left - 1); x <= Math.Min(width - 1, left + w); x++)
                            kept[y * width + x] = true;
                }
            }
            // The middle of every way between rooms, three tiles wide (Docs/BiomeRework.md 4-3 비우는 곳: 통로 가운데 줄).
            foreach (var edge in graph.Edges)
                if (edge.Path != null)
                    foreach (var (x, y) in edge.Path) Keep(x, y, 1);
            foreach (var poi in graph.Pois) Keep(poi.X, poi.Y, 3);
            foreach (var mark in graph.Marks) Keep(mark.X, mark.Y, 2);
            foreach (var site in world.Places) Keep(site.X, site.Y, site.Radius + 2);
            foreach (var arena in world.Arenas) Keep(TileMap.Cell(arena.X), TileMap.Cell(arena.Y), ArenaRadius + 3);

            bool Bare(int x0, int y0, int x1, int y1)
            {
                if (x0 < 1 || y0 < 1 || x1 >= width - 1 || y1 >= height - 1) return false;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Floor) return false;
                return true;
            }
            bool WallAt(int x, int y, int reach)
            {
                for (int d = -reach; d <= reach; d++)
                    if (map[x + d, y - reach] == TileType.Wall || map[x + d, y + reach] == TileType.Wall || map[x - reach, y + d] == TileType.Wall
                        || map[x + reach, y + d] == TileType.Wall)
                        return true;
                return false;
            }
            var bigs = new List<(int X, int Y)>();
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    if (map[x, y] != TileType.Floor || kept[y * width + x]) continue;
                    var kinds = BiomeProps.Of(biomes[x, y]);
                    if (kinds.Count == 0) continue;
                    uint roll = Decor.Hash(seed, x, y, 1, PropStream);
                    // The mushroom forest's ways are narrow and few tiles fit, so more of them roll.
                    float chance = (biomes[x, y] == Biome.Mushroom ? 2 : 1) * PropsPerThousand / 1000f * Decor.Clump(seed, x, y, PropClump, PropStream);
                    if ((roll & 0xFFFFFF) * (1f / 16777216f) >= chance * 2) continue;
                    // The cheap roll first, then the wall: twice as likely three tiles off one.
                    if ((roll & 0xFFFFFF) * (1f / 16777216f) >= chance && !WallAt(x, y, 2)) continue;
                    uint pick = Decor.Hash(seed, x, y, 2, PropStream);
                    int total = 0;
                    foreach (var kind in kinds) total += kind.Weight;
                    int at = (int)(pick % (uint)total);
                    var chosen = kinds[0];
                    foreach (var kind in kinds)
                    {
                        if (at < kind.Weight)
                        {
                            chosen = kind;
                            break;
                        }
                        at -= kind.Weight;
                    }
                    int w = Structures.Footprint(chosen.Type).W;
                    bool fits = Bare(x - chosen.Clear, y - chosen.Clear, x + w - 1 + chosen.Clear, y + chosen.Clear) && !(w > 1 && kept[y * width + x + 1]);
                    if (fits && chosen.Big)
                    {
                        foreach (var (bx, by) in bigs)
                            if (Math.Abs(bx - x) < BigGap + w && Math.Abs(by - y) < BigGap) fits = false;
                    }
                    if (!fits && chosen.Big)
                    {
                        // No room for a large one here: one of the biome's small ones instead, by the same roll.
                        int small = 0;
                        foreach (var kind in kinds)
                            if (!kind.Big) small += kind.Weight;
                        at = (int)((pick >> 8) % (uint)small);
                        foreach (var kind in kinds)
                        {
                            if (kind.Big) continue;
                            if (at < kind.Weight)
                            {
                                chosen = kind;
                                break;
                            }
                            at -= kind.Weight;
                        }
                        w = 1;
                        fits = Bare(x - chosen.Clear, y - chosen.Clear, x + chosen.Clear, y + chosen.Clear);
                    }
                    if (!fits) continue;
                    if (chosen.Big) bigs.Add((x, y));
                    map[x, y] = chosen.Type;
                    for (int i = 1; i < w; i++) map[x + i, y] = TileType.StructurePart;
                }
            }
        }
    }
}
