using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The crystal cave's and the underground lake's props and landmarks (Docs/BiomeRework.md 4-1 수정 동굴·지하 호수,
    // 묶음 B3a): the tiles that stand in those two biomes, what each biome's scattered props are, and what its
    // landmarks lay.
    public static class CrystalLakeProps
    {
        // The scattered props (blocking, broken or picked).
        public static bool Is(TileType type) => type >= TileType.CrystalCluster && type <= TileType.MossIsle;

        // The tiles only a landmark lays.
        public static bool IsLandmark(TileType type) => type >= TileType.GiantGeode && type <= TileType.OldWheel;

        // What stands in the water: an islet, a sunken boat. The ground under it is the pool's water.
        public static bool Floats(TileType type) => type == TileType.MossIsle || type == TileType.SunkenBoat;

        // Whether the tile is water to the eye: a pool's own tile, or one that an islet or a sunken boat stands on.
        public static bool Wet(TileMap map, int x, int y)
        {
            if (!map.InBounds(x, y)) return false;
            var type = map[x, y];
            if (type == TileType.Water || Floats(type)) return true;
            if (type != TileType.StructurePart) return false;
            // The other tiles of an islet (two wide) or a boat (three by two), from its lower left.
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 3; i++)
                {
                    if (i == 0 && j == 0 || !map.InBounds(x - i, y - j)) continue;
                    var anchor = map[x - i, y - j];
                    if (!Floats(anchor)) continue;
                    var (w, h) = Structures.Footprint(anchor);
                    if (i < w && j < h) return true;
                }
            return false;
        }

        static readonly BiomeProps.Kind[] Crystal =
        {
            new BiomeProps.Kind(TileType.CrystalCluster, 26), new BiomeProps.Kind(TileType.CrystalSpire, 24), new BiomeProps.Kind(TileType.ResonantCrystal, 8),
            new BiomeProps.Kind(TileType.CrackedGeode, 12), new BiomeProps.Kind(TileType.CrystalMass, 16, 2, big: true),
            new BiomeProps.Kind(TileType.LeaningCrystal, 14, 2, big: true),
        };

        // The lake's on dry floor, and by the water (a tile with water beside it), where shells, coral and clams gather.
        static readonly BiomeProps.Kind[] Lake =
        {
            new BiomeProps.Kind(TileType.MossRock, 44), new BiomeProps.Kind(TileType.ShellHeap, 14), new BiomeProps.Kind(TileType.Coral, 10),
            new BiomeProps.Kind(TileType.GiantClam, 6), new BiomeProps.Kind(TileType.Wreckage, 14, 1, big: true),
        };

        static readonly BiomeProps.Kind[] Shore =
        {
            new BiomeProps.Kind(TileType.MossRock, 26), new BiomeProps.Kind(TileType.ShellHeap, 22), new BiomeProps.Kind(TileType.Coral, 26),
            new BiomeProps.Kind(TileType.GiantClam, 16), new BiomeProps.Kind(TileType.Wreckage, 6, 1, big: true),
        };

        static readonly BiomeProps.Kind[] LakeAll =
        {
            new BiomeProps.Kind(TileType.MossRock, 44), new BiomeProps.Kind(TileType.ShellHeap, 14), new BiomeProps.Kind(TileType.Coral, 10),
            new BiomeProps.Kind(TileType.GiantClam, 6), new BiomeProps.Kind(TileType.Wreckage, 14, 1, big: true), new BiomeProps.Kind(TileType.MossIsle, 0, 1, big: true),
        };

        static readonly BiomeProps.Kind[] None = new BiomeProps.Kind[0];

        // The two biomes' kinds (the generator lays them in worlds of version 6 on). shore: the lake's by the water.
        public static IReadOnlyList<BiomeProps.Kind> Of(Biome biome, bool shore = false) => biome == Biome.Crystal ? Crystal : biome == Biome.Lake ? shore ? Shore : Lake : None;

        // Every kind of a biome that has its props, the islet in the water with the lake's: for the preview, the
        // playtests and the tests.
        public static IReadOnlyList<BiomeProps.Kind> Any(Biome biome) => biome == Biome.Lake ? LakeAll : biome == Biome.Crystal ? Crystal : IceSandProps.Any(biome);

        // What each landmark lays, from its box's lower left (y up), as BiomeLandmarks' layouts.
        // A giant geode split open, clusters and spires grown round it, a small cracked one beside. Seven by five.
        public static readonly (int X, int Y, TileType Type)[] GiantGeode =
        {
            (2, 2, TileType.GiantGeode),
            (0, 1, TileType.CrystalCluster), (6, 3, TileType.CrystalCluster), (1, 4, TileType.CrystalSpire), (5, 0, TileType.CrystalSpire), (6, 1, TileType.CrackedGeode),
        };

        // An arch of crystal: two piers and, on the tile half way between them, the span (walked under). Seven by three.
        public static readonly (int X, int Y, TileType Type)[] CrystalArch =
        {
            (1, 1, TileType.ArchPier), (5, 1, TileType.ArchPier), (3, 1, TileType.CrystalArch),
        };

        // A boat sunk in a pool, its upper two rows the water it lies in, the row under them the shore. Three by three.
        public static readonly (int X, int Y, TileType Type)[] SunkenBoat =
        {
            (0, 1, TileType.SunkenBoat),
        };

        // An old water wheel by the water, two tiles by two with a tile round it. Four by four.
        public static readonly (int X, int Y, TileType Type)[] OldWheel =
        {
            (1, 1, TileType.OldWheel),
        };

        public static (int X, int Y, TileType Type)[] Layout(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.GiantGeode => GiantGeode,
            BiomeLandmarkKind.CrystalArch => CrystalArch,
            BiomeLandmarkKind.SunkenBoat => SunkenBoat,
            BiomeLandmarkKind.OldWheel => OldWheel,
            _ => IceSandProps.Layout(kind),
        };

        // A landmark's box (width, height) and, from its lower left, a tile of open floor in front of it (the wheel's
        // and the boat's are found where they are laid: BiomeLandmark.StandX).
        public static (int W, int H, int StandX, int StandY) Box(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.GiantGeode => (7, 5, 3, 1),
            BiomeLandmarkKind.CrystalArch => (7, 3, 3, 0),
            BiomeLandmarkKind.SunkenBoat => (3, 3, 1, 0),
            BiomeLandmarkKind.OldWheel => (4, 4, 1, 0),
            _ => IceSandProps.Box(kind),
        };

        // Whether a landmark is one of these two biomes'.
        public static bool Lays(BiomeLandmarkKind kind) => kind >= BiomeLandmarkKind.GiantGeode && kind <= BiomeLandmarkKind.OldWheel;
    }

    public static partial class WorldGenerator
    {
        // The generator's sixth version (2026-11-25, Docs/BiomeRework.md 묶음 B3): version 5 with the crystal cave's and
        // the underground lake's landmarks and props laid after its own. Worlds made at version 5 or before stay as they
        // were made (their map hashes are pinned in the tests).
        public const int CrystalLakeVersion = 6;

        // In a thousand tiles of the two biomes' floor fit for a prop, how many get one before the clumps and the
        // wall's (or the water's) pull.
        const int CrystalPropsPerThousand = 70;
        const int LakePropsPerThousand = 50;
        // In a thousand tiles of a pool with water all round them, how many get an islet; islets stand this far apart.
        const int IsletsPerThousand = 16;
        const int IsletGap = 8;
        // Of a hundred of their rooms left as carved, how many are tried for a landmark.
        // (the crystal cave's clefts are narrow and few of its rooms fit one, so more of them are tried).
        const int CrystalLandmarkRoomsPerHundred = 32, LakeLandmarkRoomsPerHundred = 18;

        // Lays the two biomes' landmarks, then their scattered props, after everything version 5 lays but its cellars:
        // nothing version 5 made moves. By the props' stream and each tile's or room's own hash, so the host and every
        // client make the same map. kept: what LayProps keeps clear (and the landmarks laid before).
        //  - The crystal cave's things stand on bare floor with bare floor all round their footprint (two tiles round a
        //    large one), as every biome's before it.
        //  - The lake's may stand by the water: the tiles round a footprint are each bare floor or water, and the floor
        //    among them is one unbroken run more than half way round, so whoever walked over the footprint walks round
        //    it. An islet and a sunken boat take tiles of a pool, which nothing walked.
        static void LayCrystalLake(GeneratedWorld world, ulong seed, bool[] kept)
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
                        if (map[x, y] != TileType.Water || biomes[x, y] != Biome.Lake) return false;
                return true;
            }
            // The tiles round a footprint (x..x+w-1, y..y+h-1), walked once round: each bare floor or water, the floor
            // in one run and more than half of them. water: how many are water. floorX/floorY: a floor tile under the
            // footprint if there is one, else any of the run.
            bool Round(int x, int y, int w, int h, out int water, out int floorX, out int floorY)
            {
                water = 0;
                floorX = floorY = -1;
                if (x < 3 || y < 3 || x + w >= width - 3 || y + h >= height - 3) return false;
                int count = 2 * (w + h) + 4, floor = 0, turns = 0;
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
                    else if (i < w + 2 + h)
                    {
                        rx = x + w;
                        ry = y + (i - w - 2);
                    }
                    else if (i < 2 * w + 4 + h)
                    {
                        rx = x + w - (i - w - 2 - h);
                        ry = y + h;
                    }
                    else
                    {
                        rx = x - 1;
                        ry = y + h - 1 - (i - 2 * w - 4 - h);
                    }
                    var type = map[rx, ry];
                    bool isFloor = type == TileType.Floor;
                    if (!isFloor && type != TileType.Water) return false;
                    if (isFloor)
                    {
                        floor++;
                        if (floorX < 0 || ry == y - 1 && floorY != y - 1)
                        {
                            floorX = rx;
                            floorY = ry;
                        }
                    }
                    else water++;
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
                if (room.Biome != Biome.Crystal && room.Biome != Biome.Lake) continue;
                uint roll = Decor.Hash(seed, room.X, room.Y, 31, PropStream);
                if (roll % 100 >= (room.Biome == Biome.Crystal ? CrystalLandmarkRoomsPerHundred : LakeLandmarkRoomsPerHundred)) continue;
                bool first = (roll >> 8) % 10 < 5;
                var kinds = room.Biome == Biome.Crystal
                    ? first ? new[] { BiomeLandmarkKind.GiantGeode, BiomeLandmarkKind.CrystalArch } : new[] { BiomeLandmarkKind.CrystalArch, BiomeLandmarkKind.GiantGeode }
                    : first ? new[] { BiomeLandmarkKind.SunkenBoat, BiomeLandmarkKind.OldWheel } : new[] { BiomeLandmarkKind.OldWheel, BiomeLandmarkKind.SunkenBoat };
                bool done = false;
                // The lake's stand at the water, which is often past the room's own round.
                int reach = Math.Max(4, room.Radius) + (room.Biome == Biome.Lake ? 3 : 0);
                foreach (var kind in kinds)
                {
                    var (w, h, standX, standY) = CrystalLakeProps.Box(kind);
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
                                int sx = left + standX, sy = bottom + standY;
                                if (kind == BiomeLandmarkKind.SunkenBoat)
                                {
                                    // Its six tiles a pool's, and open floor under its middle to look at it from.
                                    if (!Pool(left, bottom + 1, left + 2, bottom + 2) || map[sx, sy] != TileType.Floor || kept[sy * width + sx]) continue;
                                }
                                else if (kind == BiomeLandmarkKind.OldWheel)
                                {
                                    // Its four tiles bare floor kept clear of nothing, water at two or more of the
                                    // tiles round them.
                                    if (!Bare(left + 1, bottom + 1, left + 2, bottom + 2, true) || !Round(left + 1, bottom + 1, 2, 2, out int water, out sx, out sy) || water < 2) continue;
                                }
                                else if (!Bare(left - 1, bottom - 1, left + w, bottom + h, true)) continue;
                                foreach (var (tx, ty, type) in CrystalLakeProps.Layout(kind))
                                {
                                    var (fw, fh) = Structures.Footprint(type);
                                    for (int j = 0; j < fh; j++)
                                        for (int i = 0; i < fw; i++)
                                            map[left + tx + i, bottom + ty + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                                }
                                laid.Add(new BiomeLandmark(kind, left, bottom, w, h, sx, sy));
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
            bool WaterBy(int x, int y)
            {
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (map[x + dx, y + dy] == TileType.Water) return true;
                return false;
            }
            // Where a kind may stand with its type on (x, y).
            bool Stands(BiomeProps.Kind kind, int x, int y, bool lake)
            {
                int w = Structures.Footprint(kind.Type).W;
                for (int i = 0; i < w; i++)
                    if (kept[y * width + x + i] || map[x + i, y] != TileType.Floor) return false;
                if (lake) return Round(x, y, w, 1, out _, out _, out _);
                return Bare(x - kind.Clear, y - kind.Clear, x + w - 1 + kind.Clear, y + kind.Clear);
            }
            var bigs = new List<(int X, int Y)>();
            var islets = new List<(int X, int Y)>();
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    var biome = biomes[x, y];
                    if (biome != Biome.Crystal && biome != Biome.Lake) continue;
                    // An islet: two tiles of a pool with water all round them.
                    if (biome == Biome.Lake && map[x, y] == TileType.Water)
                    {
                        if ((Decor.Hash(seed, x, y, 33, PropStream) & 0xFFFFFF) * (1f / 16777216f) >= IsletsPerThousand / 1000f || !Pool(x - 1, y - 1, x + 2, y + 1)) continue;
                        bool apart = true;
                        foreach (var (ix, iy) in islets)
                            if (Math.Abs(ix - x) < IsletGap + 2 && Math.Abs(iy - y) < IsletGap) apart = false;
                        if (!apart) continue;
                        islets.Add((x, y));
                        map[x, y] = TileType.MossIsle;
                        map[x + 1, y] = TileType.StructurePart;
                        continue;
                    }
                    if (map[x, y] != TileType.Floor || kept[y * width + x]) continue;
                    bool lake = biome == Biome.Lake;
                    bool shore = lake && WaterBy(x, y);
                    var kinds = CrystalLakeProps.Of(biome, shore);
                    uint roll = Decor.Hash(seed, x, y, 31, PropStream);
                    float chance = (lake ? LakePropsPerThousand : CrystalPropsPerThousand) / 1000f * Decor.Clump(seed, x, y, PropClump, PropStream);
                    float rolled = (roll & 0xFFFFFF) * (1f / 16777216f);
                    if (rolled >= chance * 2) continue;
                    // Twice as likely two tiles off a wall, as the other biomes' props; the lake's by the water.
                    if (rolled >= chance && !(lake ? shore : WallAt(x, y, 2))) continue;
                    uint pick = Decor.Hash(seed, x, y, 32, PropStream);
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
                    bool fits = Stands(chosen, x, y, lake);
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
                        fits = Stands(chosen, x, y, lake);
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
