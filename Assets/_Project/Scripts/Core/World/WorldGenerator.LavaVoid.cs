using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The lava zone's and the void abyss's props and landmarks (Docs/BiomeRework.md 4-1 용암 지대·공허의 심연, 묶음 B4a):
    // the tiles that stand in those two biomes, what each biome's scattered props are, and what its landmarks lay.
    public static class LavaVoidProps
    {
        // The scattered props (blocking, broken or picked).
        public static bool Is(TileType type) => type >= TileType.ObsidianSpike && type <= TileType.ShatteredPillar;

        // The tiles only a landmark lays.
        public static bool IsLandmark(TileType type) => type >= TileType.FallenChimney && type <= TileType.StarAltar;

        // What stands in a pool of lava: the ground under it is the lava.
        public static bool InLava(TileType type) => type == TileType.LavaRock;

        // Whether the tile is lava to the eye: a pool's own tile, or one a rock stands in.
        public static bool Molten(TileMap map, int x, int y) => map.InBounds(x, y) && (map[x, y] == TileType.Lava || InLava(map[x, y]));

        // The lava zone's on dry basalt, and at the lava's edge (a tile with lava beside it), where the cooled crust
        // heaps up and what burns grows.
        static readonly BiomeProps.Kind[] Lava =
        {
            new BiomeProps.Kind(TileType.ObsidianSpike, 26), new BiomeProps.Kind(TileType.FireBush, 14), new BiomeProps.Kind(TileType.SlagHeap, 20),
            new BiomeProps.Kind(TileType.CooledBoulder, 20, 2, big: true), new BiomeProps.Kind(TileType.BasaltColumns, 20, 2, big: true),
        };

        static readonly BiomeProps.Kind[] Shore =
        {
            new BiomeProps.Kind(TileType.ObsidianSpike, 18), new BiomeProps.Kind(TileType.FireBush, 28), new BiomeProps.Kind(TileType.SlagHeap, 8),
            new BiomeProps.Kind(TileType.CooledBoulder, 32, 1, big: true), new BiomeProps.Kind(TileType.BasaltColumns, 14, 1, big: true),
        };

        static readonly BiomeProps.Kind[] LavaAll =
        {
            new BiomeProps.Kind(TileType.ObsidianSpike, 26), new BiomeProps.Kind(TileType.FireBush, 14), new BiomeProps.Kind(TileType.SlagHeap, 20),
            new BiomeProps.Kind(TileType.CooledBoulder, 20, 2, big: true), new BiomeProps.Kind(TileType.BasaltColumns, 20, 2, big: true), new BiomeProps.Kind(TileType.LavaRock, 0),
        };

        static readonly BiomeProps.Kind[] Abyss =
        {
            new BiomeProps.Kind(TileType.VoidCrystal, 22), new BiomeProps.Kind(TileType.TwistedVine, 22), new BiomeProps.Kind(TileType.ObeliskShard, 16),
            new BiomeProps.Kind(TileType.ChainPost, 10), new BiomeProps.Kind(TileType.FloatingRock, 16, 2, big: true), new BiomeProps.Kind(TileType.ShatteredPillar, 14, 2, big: true),
        };

        static readonly BiomeProps.Kind[] None = new BiomeProps.Kind[0];

        // The two biomes' kinds (the generator lays them in worlds of version 7 on). shore: the lava zone's by the lava.
        public static IReadOnlyList<BiomeProps.Kind> Of(Biome biome, bool shore = false) => biome == Biome.Lava ? shore ? Shore : Lava : biome == Biome.Abyss ? Abyss : None;

        // Every kind of a biome that has its props, the rock in the lava with the lava zone's: for the preview, the
        // playtests and the tests.
        public static IReadOnlyList<BiomeProps.Kind> Any(Biome biome) => biome == Biome.Lava ? LavaAll : biome == Biome.Abyss ? Abyss : CrystalLakeProps.Any(biome);

        // What each landmark lays, from its box's lower left (y up), as BiomeLandmarks' layouts.
        // The chimney of a forge fallen across the floor, its firebox still alight; slag and what burns round it.
        // Seven by five.
        public static readonly (int X, int Y, TileType Type)[] FallenChimney =
        {
            (2, 2, TileType.FallenChimney),
            (0, 1, TileType.SlagHeap), (6, 3, TileType.SlagHeap), (1, 4, TileType.FireBush), (5, 0, TileType.ObsidianSpike), (6, 1, TileType.FireBush),
        };

        // A giant anvil on its block of basalt, slag and spikes of obsidian round it. Seven by five.
        public static readonly (int X, int Y, TileType Type)[] GiantAnvil =
        {
            (2, 2, TileType.GiantAnvil),
            (0, 3, TileType.SlagHeap), (6, 1, TileType.SlagHeap), (1, 0, TileType.FireBush), (5, 4, TileType.ObsidianSpike), (0, 0, TileType.ObsidianSpike),
        };

        // A broken gate: what is left of its ring, a chain post at each side, void crystals behind. Seven by four.
        public static readonly (int X, int Y, TileType Type)[] BrokenGate =
        {
            (2, 2, TileType.BrokenGate),
            (0, 1, TileType.ChainPost), (6, 1, TileType.ChainPost), (1, 3, TileType.VoidCrystal), (5, 3, TileType.VoidCrystal),
        };

        // A star altar: a round dais with the stars cut in its top, shards of an obelisk hanging at its four corners
        // (the ring of floating stones). Seven by five.
        public static readonly (int X, int Y, TileType Type)[] StarAltar =
        {
            (2, 2, TileType.StarAltar),
            (0, 0, TileType.ObeliskShard), (6, 0, TileType.ObeliskShard), (0, 4, TileType.ObeliskShard), (6, 4, TileType.ObeliskShard),
            (0, 2, TileType.VoidCrystal), (6, 2, TileType.VoidCrystal),
        };

        public static (int X, int Y, TileType Type)[] Layout(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.FallenChimney => FallenChimney,
            BiomeLandmarkKind.GiantAnvil => GiantAnvil,
            BiomeLandmarkKind.BrokenGate => BrokenGate,
            BiomeLandmarkKind.StarAltar => StarAltar,
            _ => CrystalLakeProps.Layout(kind),
        };

        // A landmark's box (width, height) and, from its lower left, a tile of open floor in front of it.
        public static (int W, int H, int StandX, int StandY) Box(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.FallenChimney => (7, 5, 3, 1),
            BiomeLandmarkKind.GiantAnvil => (7, 5, 3, 1),
            BiomeLandmarkKind.BrokenGate => (7, 4, 3, 1),
            BiomeLandmarkKind.StarAltar => (7, 5, 3, 1),
            _ => CrystalLakeProps.Box(kind),
        };

        // Whether a landmark is one of these two biomes'.
        public static bool Lays(BiomeLandmarkKind kind) => kind >= BiomeLandmarkKind.FallenChimney && kind <= BiomeLandmarkKind.StarAltar;
    }

    public static partial class WorldGenerator
    {
        // The generator's seventh version (2026-11-25, Docs/BiomeRework.md 묶음 B4): version 6 with the lava zone's and
        // the void abyss's landmarks and props laid after its own. Worlds made at version 6 or before stay as they were
        // made (their map hashes are pinned in the tests).
        public const int LavaVoidVersion = 7;

        // In a thousand tiles of the two biomes' floor fit for a prop, how many get one before the clumps and the
        // wall's (or the lava's) pull.
        const int LavaPropsPerThousand = 60;
        const int AbyssPropsPerThousand = 70;
        // In a thousand tiles of a pool of lava with lava all round them, how many get a rock; rocks stand this far apart.
        const int LavaRocksPerThousand = 35;
        const int LavaRockGap = 3;
        // Of a hundred of their rooms left as carved, how many are tried for a landmark (the abyss has few rooms and
        // few of them fit one, so more of them are tried).
        const int LavaLandmarkRoomsPerHundred = 16, AbyssLandmarkRoomsPerHundred = 45;

        // Lays the two biomes' landmarks, then their scattered props, after everything version 6 lays but its cellars:
        // nothing version 6 made moves. By the props' stream (salts 41 to 43) and each tile's or room's own hash, so the
        // host and every client make the same map. kept: what LayProps keeps clear (and the landmarks laid before).
        //  - The abyss's things stand on bare floor with bare floor all round their footprint (two tiles round a large
        //    one), as every biome's before it.
        //  - The lava zone's may stand at the lava's edge: the tiles round a footprint are each bare floor or lava, and
        //    the floor among them is one unbroken run more than half way round, so whoever walked over the footprint
        //    walks round it on dry floor. A rock in the lava takes a tile of a pool with lava all round it.
        static void LayLavaVoid(GeneratedWorld world, ulong seed, bool[] kept)
        {
            var map = world.Map;
            var biomes = world.Biomes;
            int width = map.Width, height = map.Height;
            bool Bare(int x0, int y0, int x1, int y1, bool free = false)
            {
                if (x0 < 2 || y0 < 2 || x1 >= width - 2 || y1 >= height - 2) return false;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Floor || free && kept[y * width + x]) return false;
                return true;
            }
            bool Pool(int x0, int y0, int x1, int y1)
            {
                if (x0 < 2 || y0 < 2 || x1 >= width - 2 || y1 >= height - 2) return false;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Lava || biomes[x, y] != Biome.Lava || kept[y * width + x]) return false;
                return true;
            }
            // The tiles round a footprint (x..x+w-1, y), walked once round: each bare floor or lava, the floor in one
            // run and more than half of them.
            bool Round(int x, int y, int w)
            {
                if (x < 3 || y < 3 || x + w >= width - 3 || y + 1 >= height - 3) return false;
                int count = 2 * (w + 1) + 4, floor = 0, turns = 0;
                bool first = false, last = false;
                for (int i = 0; i < count; i++)
                {
                    // Along the bottom to the right, up the right side, back along the top, down the left side.
                    int rx, ry;
                    if (i < w + 2)
                    {
                        rx = x - 1 + i;
                        ry = y - 1;
                    }
                    else if (i < w + 3)
                    {
                        rx = x + w;
                        ry = y;
                    }
                    else if (i < 2 * w + 5)
                    {
                        rx = x + w - (i - w - 3);
                        ry = y + 1;
                    }
                    else
                    {
                        rx = x - 1;
                        ry = y;
                    }
                    var type = map[rx, ry];
                    bool isFloor = type == TileType.Floor;
                    if (!isFloor && type != TileType.Lava) return false;
                    if (isFloor) floor++;
                    if (i == 0) first = isFloor;
                    else if (isFloor != last) turns++;
                    last = isFloor;
                }
                if (first != last) turns++;
                return turns <= 2 && floor * 2 > count;
            }

            // ---- The landmarks ----
            var laid = new List<BiomeLandmark>(world.BiomeLandmarks);
            foreach (var room in world.Rooms.Nodes)
            {
                if (room.Kind != RoomKind.Room || room.Chunk != null || room.Box != null) continue;
                if (room.Role != RoomRole.Plain && room.Role != RoomRole.Pack && room.Role != RoomRole.Ore) continue;
                if (room.Biome != Biome.Lava && room.Biome != Biome.Abyss) continue;
                uint roll = Decor.Hash(seed, room.X, room.Y, 41, PropStream);
                if (roll % 100 >= (room.Biome == Biome.Lava ? LavaLandmarkRoomsPerHundred : AbyssLandmarkRoomsPerHundred)) continue;
                bool first = (roll >> 8) % 10 < 5;
                var kinds = room.Biome == Biome.Lava
                    ? first ? new[] { BiomeLandmarkKind.FallenChimney, BiomeLandmarkKind.GiantAnvil } : new[] { BiomeLandmarkKind.GiantAnvil, BiomeLandmarkKind.FallenChimney }
                    : first ? new[] { BiomeLandmarkKind.BrokenGate, BiomeLandmarkKind.StarAltar } : new[] { BiomeLandmarkKind.StarAltar, BiomeLandmarkKind.BrokenGate };
                bool done = false;
                int reach = Math.Max(4, room.Radius);
                foreach (var kind in kinds)
                {
                    var (w, h, standX, standY) = LavaVoidProps.Box(kind);
                    // Rings out from the room's middle, each in map order: the same on the host and every client.
                    for (int ring = 0; ring <= reach && !done; ring++)
                    {
                        for (int dy = -ring; dy <= ring && !done; dy++)
                        {
                            for (int dx = -ring; dx <= ring && !done; dx++)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                                int left = room.X + dx - w / 2, bottom = room.Y + dy - h / 2;
                                if (left < 3 || bottom < 3 || left + w >= width - 3 || bottom + h >= height - 3) continue;
                                if (biomes[left, bottom] != room.Biome || biomes[left + w - 1, bottom + h - 1] != room.Biome) continue;
                                if (!Bare(left - 1, bottom - 1, left + w, bottom + h, true)) continue;
                                foreach (var (tx, ty, type) in LavaVoidProps.Layout(kind))
                                {
                                    var (fw, fh) = Structures.Footprint(type);
                                    for (int j = 0; j < fh; j++)
                                        for (int i = 0; i < fw; i++)
                                            map[left + tx + i, bottom + ty + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                                }
                                laid.Add(new BiomeLandmark(kind, left, bottom, w, h, left + standX, bottom + standY));
                                done = true;
                            }
                        }
                    }
                    if (done) break;
                }
                if (!done) continue;
                // Nothing else is laid in its box or a tile round it (two above it: its things stand tall).
                var made = laid[laid.Count - 1];
                for (int y = made.Bottom - 1; y <= made.Bottom + made.Height + 1; y++)
                    for (int x = made.Left - 1; x <= made.Left + made.Width; x++)
                        kept[y * width + x] = true;
            }
            world.BiomeLandmarks = laid;

            // ---- The scattered props (LayProps' way, for these two biomes) ----
            bool WallAt(int x, int y, int reach)
            {
                for (int d = -reach; d <= reach; d++)
                    if (map[x + d, y - reach] == TileType.Wall || map[x + d, y + reach] == TileType.Wall || map[x - reach, y + d] == TileType.Wall
                        || map[x + reach, y + d] == TileType.Wall)
                        return true;
                return false;
            }
            bool LavaBy(int x, int y)
            {
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (map[x + dx, y + dy] == TileType.Lava) return true;
                return false;
            }
            // Where a kind may stand with its type on (x, y).
            bool Stands(BiomeProps.Kind kind, int x, int y, bool shore)
            {
                int w = Structures.Footprint(kind.Type).W;
                for (int i = 0; i < w; i++)
                    if (kept[y * width + x + i] || map[x + i, y] != TileType.Floor) return false;
                if (shore) return Round(x, y, w);
                return Bare(x - kind.Clear, y - kind.Clear, x + w - 1 + kind.Clear, y + kind.Clear);
            }
            var bigs = new List<(int X, int Y)>();
            var rocks = new List<(int X, int Y)>();
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    var biome = biomes[x, y];
                    if (biome != Biome.Lava && biome != Biome.Abyss) continue;
                    // A rock in the lava: a tile of a pool with lava all round it.
                    if (biome == Biome.Lava && map[x, y] == TileType.Lava)
                    {
                        if ((Decor.Hash(seed, x, y, 43, PropStream) & 0xFFFFFF) * (1f / 16777216f) >= LavaRocksPerThousand / 1000f || !Pool(x - 1, y - 1, x + 1, y + 1)) continue;
                        bool apart = true;
                        foreach (var (rx, ry) in rocks)
                            if (Math.Abs(rx - x) < LavaRockGap && Math.Abs(ry - y) < LavaRockGap) apart = false;
                        if (!apart) continue;
                        rocks.Add((x, y));
                        map[x, y] = TileType.LavaRock;
                        continue;
                    }
                    if (map[x, y] != TileType.Floor || kept[y * width + x]) continue;
                    bool lava = biome == Biome.Lava;
                    bool shore = lava && LavaBy(x, y);
                    var kinds = LavaVoidProps.Of(biome, shore);
                    uint roll = Decor.Hash(seed, x, y, 41, PropStream);
                    float chance = (lava ? LavaPropsPerThousand : AbyssPropsPerThousand) / 1000f * Decor.Clump(seed, x, y, PropClump, PropStream);
                    float rolled = (roll & 0xFFFFFF) * (1f / 16777216f);
                    if (rolled >= chance * 2) continue;
                    // Twice as likely two tiles off a wall, as the other biomes' props; the lava zone's by the lava too.
                    if (rolled >= chance && !(shore || WallAt(x, y, 2))) continue;
                    uint pick = Decor.Hash(seed, x, y, 42, PropStream);
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
                    bool fits = Stands(chosen, x, y, shore);
                    if (fits && chosen.Big)
                    {
                        int w = Structures.Footprint(chosen.Type).W;
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
                        fits = Stands(chosen, x, y, shore);
                    }
                    if (!fits) continue;
                    if (chosen.Big) bigs.Add((x, y));
                    int wide = Structures.Footprint(chosen.Type).W;
                    map[x, y] = chosen.Type;
                    for (int i = 1; i < wide; i++) map[x + i, y] = TileType.StructurePart;
                }
            }
        }
    }
}
