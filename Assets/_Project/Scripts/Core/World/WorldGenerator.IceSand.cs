using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The ice cave's and the sand cave's props and landmarks (Docs/BiomeRework.md 4-1 얼음 동굴·모래 동굴, 묶음 B2a): the
    // tiles that stand in those two biomes, what each biome's scattered props are, and what its landmarks lay.
    public static class IceSandProps
    {
        // The scattered props (blocking, broken or picked).
        public static bool Is(TileType type) => type >= TileType.IceSpike && type <= TileType.BuriedColumn;

        // The tiles only a landmark lays.
        public static bool IsLandmark(TileType type) => type >= TileType.FrozenBeast && type <= TileType.GiantSkull;

        // A frozen fall hangs from the rock: it is laid only with rock right above both its tiles.
        public static bool Hangs(TileType type) => type == TileType.FrozenFall;

        static readonly BiomeProps.Kind[] Ice =
        {
            new BiomeProps.Kind(TileType.IceSpike, 30), new BiomeProps.Kind(TileType.FrozenBush, 14), new BiomeProps.Kind(TileType.SnowDrift, 16),
            new BiomeProps.Kind(TileType.IceCrystals, 14), new BiomeProps.Kind(TileType.IcePillar, 16, 2, big: true),
            new BiomeProps.Kind(TileType.FrozenFall, 10, 1, big: true),
        };

        static readonly BiomeProps.Kind[] Sand =
        {
            new BiomeProps.Kind(TileType.CaveCactus, 24), new BiomeProps.Kind(TileType.Skull, 12), new BiomeProps.Kind(TileType.BrokenUrn, 12),
            new BiomeProps.Kind(TileType.SandstonePillar, 18), new BiomeProps.Kind(TileType.RibArch, 16, 2, big: true),
            new BiomeProps.Kind(TileType.BuriedColumn, 18, 2, big: true),
        };

        static readonly BiomeProps.Kind[] None = new BiomeProps.Kind[0];

        // The two biomes' kinds (the generator lays them in worlds of version 5 on).
        public static IReadOnlyList<BiomeProps.Kind> Of(Biome biome) => biome == Biome.Ice ? Ice : biome == Biome.Sand ? Sand : None;

        // Every biome's kinds, whatever version lays them: for the preview, the playtests and the tests.
        public static IReadOnlyList<BiomeProps.Kind> Any(Biome biome) => BiomeProps.Of(biome).Count > 0 ? BiomeProps.Of(biome) : Of(biome);

        // What each landmark lays, from its box's lower left (y up), as BiomeLandmarks' layouts.
        // A beast frozen in a block of ice, crystals, spikes and a drift of snow round it. Seven by five.
        public static readonly (int X, int Y, TileType Type)[] FrozenBeast =
        {
            (2, 2, TileType.FrozenBeast),
            (0, 1, TileType.IceCrystals), (6, 3, TileType.IceCrystals), (1, 4, TileType.IceSpike), (5, 0, TileType.IceSpike), (6, 1, TileType.SnowDrift),
        };

        // The ruin of an ice keep: two rows of three columns, the way between them open. Seven by four.
        public static readonly (int X, int Y, TileType Type)[] IceRuin =
        {
            (0, 3, TileType.IceColumn), (3, 3, TileType.IceColumn), (6, 3, TileType.IceColumn),
            (0, 0, TileType.IceColumn), (3, 0, TileType.IceColumn), (6, 0, TileType.IceColumn),
        };

        // The front of a buried temple, its top row (3) against the rock: urns by its corners, two pillars before it.
        // Seven by four.
        public static readonly (int X, int Y, TileType Type)[] TempleFront =
        {
            (1, 3, TileType.TempleFront), (0, 3, TileType.BrokenUrn), (6, 3, TileType.BrokenUrn),
            (1, 1, TileType.SandstonePillar), (5, 1, TileType.SandstonePillar),
        };

        // A giant fossil skull and two arches of its ribs behind it, the row between them walked. Seven by four.
        public static readonly (int X, int Y, TileType Type)[] GiantSkull =
        {
            (0, 1, TileType.GiantSkull), (4, 3, TileType.RibArch), (4, 0, TileType.RibArch),
        };

        public static (int X, int Y, TileType Type)[] Layout(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.FrozenBeast => FrozenBeast,
            BiomeLandmarkKind.IceRuin => IceRuin,
            BiomeLandmarkKind.TempleFront => TempleFront,
            BiomeLandmarkKind.GiantSkull => GiantSkull,
            _ => null,
        };

        // A landmark's box (width, height) and, from its lower left, a tile of open floor in front of it.
        public static (int W, int H, int StandX, int StandY) Box(BiomeLandmarkKind kind) => kind switch
        {
            BiomeLandmarkKind.FrozenBeast => (7, 5, 3, 1),
            BiomeLandmarkKind.IceRuin => (7, 4, 3, 1),
            BiomeLandmarkKind.TempleFront => (7, 4, 3, 2),
            _ => (7, 4, 3, 2),
        };

        // Whether a landmark is one of these two biomes'.
        public static bool Lays(BiomeLandmarkKind kind) => kind >= BiomeLandmarkKind.FrozenBeast && kind <= BiomeLandmarkKind.GiantSkull;
    }

    public static partial class WorldGenerator
    {
        // The generator's fifth version (2026-11-24, Docs/BiomeRework.md 묶음 B2): version 4 with the ice cave's and the
        // sand cave's landmarks and props laid after its own props. Worlds made at version 4, 3 or 2 stay as they were
        // made (their map hashes are pinned in the tests).
        public const int IceSandVersion = 5;

        // In a thousand tiles of the two biomes' floor fit for a prop, how many get one before the clumps and the
        // wall's pull: the dirt cave's 70 of version 4 in the ice cave (2.8 props in a hundred tiles of floor), 50 in
        // the sand cave, whose rooms are open and take more of them (2.5; at 70 it had 3.4, the most of any biome, in
        // the biome where the first hour's bot falls most).
        const int IceSandPropsPerThousand = 70;
        const int SandPropsPerThousand = 50;
        // Of a hundred of their rooms left as carved, how many are tried for a landmark.
        const int IceSandLandmarkRoomsPerHundred = 16;
        const int TempleRoomsPerHundred = 40;

        // Lays the two biomes' landmarks, then their scattered props, after everything version 4 lays but its cellars:
        // nothing version 4 made moves. By the props' stream and each tile's or room's own hash, so the host and every
        // client make the same map. kept: what LayProps keeps clear (and the dirt cave's and the mushroom forest's
        // landmarks). Every thing stands on bare floor with bare floor all round its footprint (two tiles round a large
        // one), so none shuts a way; a frozen fall alone stands against the rock above it, with floor on its other
        // three sides.
        static void LayIceSand(GeneratedWorld world, ulong seed, bool[] kept)
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
            bool Rock(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Wall) return false;
                return true;
            }

            // ---- The landmarks ----
            var laid = new List<BiomeLandmark>(world.BiomeLandmarks);
            bool Fits(BiomeLandmarkKind kind, Biome biome, int x, int y)
            {
                var (w, h, _, _) = IceSandProps.Box(kind);
                if (x < 2 || y < 2 || x + w >= width - 2 || y + h >= height - 2) return false;
                if (biomes[x, y] != biome || biomes[x + w - 1, y + h - 1] != biome) return false;
                // The temple's front: its five tiles under plain rock with bare floor before them (the pillars stand a
                // tile in from the box's sides, so they have floor all round); the urns' corners whatever they are.
                if (kind == BiomeLandmarkKind.TempleFront)
                    return Bare(x, y, x + w - 1, y + h - 2, true) && Bare(x + 1, y + h - 1, x + w - 2, y + h - 1, true) && Rock(x + 1, y + h, x + w - 2, y + h);
                return Bare(x - 1, y - 1, x + w, y + h, true);
            }
            foreach (var room in world.Rooms.Nodes)
            {
                if (room.Kind != RoomKind.Room || room.Chunk != null || room.Box != null) continue;
                if (room.Role != RoomRole.Plain && room.Role != RoomRole.Pack && room.Role != RoomRole.Ore) continue;
                if (room.Biome != Biome.Ice && room.Biome != Biome.Sand) continue;
                uint roll = Decor.Hash(seed, room.X, room.Y, 12, PropStream);
                // A temple's front wants a wide level face of rock, which few rooms have: the sand cave's rooms that
                // roll no landmark are still tried for one, some of them.
                bool templeOnly = roll % 100 >= IceSandLandmarkRoomsPerHundred && room.Biome == Biome.Sand && (roll >> 16) % 100 < TempleRoomsPerHundred;
                if (roll % 100 >= IceSandLandmarkRoomsPerHundred && !templeOnly) continue;
                bool first = (roll >> 8) % 10 < 5;
                var kinds = templeOnly ? new[] { BiomeLandmarkKind.TempleFront } : room.Biome == Biome.Ice
                    ? first ? new[] { BiomeLandmarkKind.FrozenBeast, BiomeLandmarkKind.IceRuin } : new[] { BiomeLandmarkKind.IceRuin, BiomeLandmarkKind.FrozenBeast }
                    // The temple's front wants a wide face of rock, which is seldom there: it is tried first more often.
                    : (roll >> 8) % 10 < 7 ? new[] { BiomeLandmarkKind.TempleFront, BiomeLandmarkKind.GiantSkull }
                    : new[] { BiomeLandmarkKind.GiantSkull, BiomeLandmarkKind.TempleFront };
                bool done = false;
                int reach = Math.Max(4, room.Radius);
                foreach (var kind in kinds)
                {
                    var (w, h, standX, standY) = IceSandProps.Box(kind);
                    // Rings out from the room's middle, each in map order: the same on the host and every client.
                    for (int ring = 0; ring <= reach && !done; ring++)
                    {
                        for (int dy = -ring; dy <= ring && !done; dy++)
                        {
                            for (int dx = -ring; dx <= ring && !done; dx++)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                                int left = room.X + dx - w / 2, bottom = room.Y + dy - h / 2;
                                if (!Fits(kind, room.Biome, left, bottom)) continue;
                                foreach (var (tx, ty, type) in IceSandProps.Layout(kind))
                                {
                                    var (fw, fh) = Structures.Footprint(type);
                                    // The urns by a temple's corners only where the rock leaves them floor.
                                    // An urn only in a nook of the rock (rock above it and at its outer side), so it shuts
                                    // nothing off.
                                    if (kind == BiomeLandmarkKind.TempleFront && type == TileType.BrokenUrn
                                        && (map[left + tx, bottom + ty] != TileType.Floor || kept[(bottom + ty) * width + left + tx]
                                            || map[left + tx, bottom + ty + 1] != TileType.Wall || map[left + tx + (tx == 0 ? -1 : 1), bottom + ty] != TileType.Wall)) continue;
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
                // Nothing else is laid in its box or a tile round it (two above it: its things stand tall, and a prop a
                // row behind would stand in their picture).
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
            // Where a kind may stand with its type on (x, y).
            bool Stands(BiomeProps.Kind kind, int x, int y)
            {
                int w = Structures.Footprint(kind.Type).W;
                for (int i = 0; i < w; i++)
                    if (kept[y * width + x + i]) return false;
                // A frozen fall: rock right above it, floor below and beside it.
                if (IceSandProps.Hangs(kind.Type))
                    return Rock(x, y + 1, x + w - 1, y + 1) && Bare(x - kind.Clear, y - kind.Clear - 1, x + w - 1 + kind.Clear, y);
                return Bare(x - kind.Clear, y - kind.Clear, x + w - 1 + kind.Clear, y + kind.Clear);
            }
            var bigs = new List<(int X, int Y)>();
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    if (map[x, y] != TileType.Floor || kept[y * width + x]) continue;
                    var kinds = IceSandProps.Of(biomes[x, y]);
                    if (kinds.Count == 0) continue;
                    uint roll = Decor.Hash(seed, x, y, 1, PropStream);
                    float chance = (biomes[x, y] == Biome.Sand ? SandPropsPerThousand : IceSandPropsPerThousand) / 1000f * Decor.Clump(seed, x, y, PropClump, PropStream);
                    float rolled = (roll & 0xFFFFFF) * (1f / 16777216f);
                    if (rolled >= chance * 2) continue;
                    // Twice as likely two tiles off a wall, as the first two biomes' props.
                    if (rolled >= chance && !WallAt(x, y, 2)) continue;
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
                    bool fits = Stands(chosen, x, y);
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
                        fits = Stands(chosen, x, y);
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
