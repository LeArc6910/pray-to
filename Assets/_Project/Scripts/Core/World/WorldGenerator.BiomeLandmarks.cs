using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The biomes' landmarks (Docs/BiomeRework.md 4-1 랜드마크·세트 피스, 묶음 B1c): rare things a room is remembered by.
    public enum BiomeLandmarkKind : byte
    {
        // The dirt cave: an old miner's hut, its yard fenced on three sides, a lamp that is out, a sack of ore and a
        // tipped cart left in it.
        MinerHut,
        // The dirt cave: the timber frame of a boarded-up drift set against the rock, a few rails running out of it past
        // a cart off them, a lamp and a sack of ore by its posts.
        MineFrame,
        // The mushroom forest: a ring of small caps round a clump of glowing ones.
        FairyRing,
        // The ice cave's and the sand cave's (묶음 B2a, WorldGenerator.IceSand.cs, worlds of version 5): a beast frozen
        // in a block of ice with crystals round it, the two rows of columns of an ice keep's ruin; the front of a
        // buried temple against the rock with urns and pillars before it, a giant fossil skull with its ribs.
        FrozenBeast,
        IceRuin,
        TempleFront,
        GiantSkull,
    }

    // One landmark as laid: the box of tiles it took (its lower left and size) and a tile of open floor in front of it.
    public readonly struct BiomeLandmark
    {
        public BiomeLandmark(BiomeLandmarkKind kind, int left, int bottom, int width, int height, int standX, int standY)
        {
            Kind = kind;
            Left = left;
            Bottom = bottom;
            Width = width;
            Height = height;
            StandX = standX;
            StandY = standY;
        }

        public BiomeLandmarkKind Kind { get; }
        public int Left { get; }
        public int Bottom { get; }
        public int Width { get; }
        public int Height { get; }
        public int StandX { get; }
        public int StandY { get; }

        public bool Holds(int x, int y) => x >= Left && y >= Bottom && x < Left + Width && y < Bottom + Height;
    }

    public static class BiomeLandmarks
    {
        // The tiles only a landmark lays.
        public static bool Is(TileType type) => type >= TileType.MinerHut && type <= TileType.RingCap || IceSandProps.IsLandmark(type);

        // What each lays, from its box's lower left (y up). A wide thing's type is on its lower left tile; the generator
        // fills the rest of its footprint with StructurePart.
        public static readonly (int X, int Y, TileType Type)[] MinerHut =
        {
            (0, 2, TileType.MinerHut),
            (3, 3, TileType.Fence), (4, 3, TileType.Fence), (5, 3, TileType.Fence), (6, 3, TileType.Fence),
            (6, 2, TileType.Fence), (6, 1, TileType.Fence), (6, 0, TileType.Fence), (5, 0, TileType.Fence), (4, 0, TileType.Fence),
            (3, 2, TileType.UnlitLamp), (5, 2, TileType.OreSack), (5, 1, TileType.TippedCart),
        };

        // Its top row (4) is the one against the rock.
        public static readonly (int X, int Y, TileType Type)[] MineFrame =
        {
            (1, 4, TileType.MineFrame), (0, 4, TileType.UnlitLamp), (4, 4, TileType.OreSack),
            (2, 3, TileType.Rail), (2, 2, TileType.Rail), (2, 1, TileType.Rail), (3, 1, TileType.TippedCart),
        };

        // Seven tiles across: twelve caps, the four ways in on its axes left open.
        public static readonly (int X, int Y, TileType Type)[] FairyRing =
        {
            (2, 0, TileType.RingCap), (4, 0, TileType.RingCap), (1, 1, TileType.RingCap), (5, 1, TileType.RingCap),
            (0, 2, TileType.RingCap), (6, 2, TileType.RingCap), (0, 4, TileType.RingCap), (6, 4, TileType.RingCap),
            (1, 5, TileType.RingCap), (5, 5, TileType.RingCap), (2, 6, TileType.RingCap), (4, 6, TileType.RingCap),
            (3, 3, TileType.GlowCaps),
        };

        // Five across where seven do not fit: eight caps.
        public static readonly (int X, int Y, TileType Type)[] SmallRing =
        {
            (1, 0, TileType.RingCap), (3, 0, TileType.RingCap), (0, 1, TileType.RingCap), (4, 1, TileType.RingCap),
            (0, 3, TileType.RingCap), (4, 3, TileType.RingCap), (1, 4, TileType.RingCap), (3, 4, TileType.RingCap),
            (2, 2, TileType.GlowCaps),
        };
    }

    public static partial class WorldGenerator
    {
        // The generator's fourth version (2026-11-24, Docs/BiomeRework.md 묶음 B1c): version 3 with the biomes' landmarks
        // laid before its props and the props denser (WorldGenerator.Props.cs), then the camps' trapdoors and the
        // mid-bosses' lairs after them (WorldGenerator.Cellars.cs). Worlds made at version 3 or 2 stay as they were made
        // (their map hashes are pinned in the tests).
        public const int LandmarkVersion = 4;

        // Of a hundred rooms of a biome left as carved (no chunk set into them) that are passed through, hold a pack or
        // have ore, how many are tried for a landmark; fewer get one, as it must fit.
        const int LandmarkRoomsPerHundred = 14;

        // Lays the landmarks, before the scattered props (LayProps, which keeps off them): for each room that rolls
        // one by its own hash (the props' stream, the room's middle), the first tile out from its middle where all of
        // the landmark's box and a tile round it is bare floor kept clear of nothing else. The dirt cave's rooms get a
        // miner's hut or a drift's frame (the frame only under a face of plain rock three tiles wide), the mushroom
        // forest's a fairy ring. Everything is on open floor with a way round it, so no way is shut; a world of
        // version 3 or before has none.
        static List<BiomeLandmark> LayBiomeLandmarks(GeneratedWorld world, ulong seed, bool[] kept)
        {
            var map = world.Map;
            var biomes = world.Biomes;
            int width = map.Width, height = map.Height;
            var laid = new List<BiomeLandmark>();
            bool Bare(int x0, int y0, int x1, int y1)
            {
                if (x0 < 2 || y0 < 2 || x1 >= width - 2 || y1 >= height - 2) return false;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Floor || kept[y * width + x]) return false;
                return true;
            }
            bool Rock(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Wall) return false;
                return true;
            }
            // A ring's caps are walked through, so it needs only floor: none of it on a way's own tiles, a door or a
            // road (all bare floor), and its middle, which blocks, off what is kept clear.
            bool Floor(int x0, int y0, int x1, int y1)
            {
                if (x0 < 2 || y0 < 2 || x1 >= width - 2 || y1 >= height - 2) return false;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (map[x, y] != TileType.Floor) return false;
                return !kept[(y0 + y1) / 2 * width + (x0 + x1) / 2];
            }
            void Lay((int X, int Y, TileType Type)[] things, int left, int bottom, bool loose = false)
            {
                foreach (var (dx, dy, type) in things)
                {
                    var (w, h) = Structures.Footprint(type);
                    // The small things by a frame's posts only where the rock leaves them floor.
                    if (loose && w == 1 && h == 1 && (map[left + dx, bottom + dy] != TileType.Floor || kept[(bottom + dy) * width + left + dx])) continue;
                    for (int j = 0; j < h; j++)
                        for (int i = 0; i < w; i++)
                            map[left + dx + i, bottom + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                }
            }
            // What fits with its box's lower left at (x, y), in the biome all through.
            bool Fits(BiomeLandmarkKind kind, Biome biome, int x, int y, out int w, out int h)
            {
                (w, h) = kind == BiomeLandmarkKind.MinerHut ? (7, 4) : kind == BiomeLandmarkKind.MineFrame ? (5, 5) : (7, 7);
                if (biomes[x, y] != biome || biomes[x + w - 1, y + h - 1] != biome) return false;
                if (kind == BiomeLandmarkKind.MineFrame)
                    return Bare(x - 1, y - 1, x + w, y + h - 2) && Bare(x + 1, y + h - 1, x + w - 2, y + h - 1) && Rock(x + 1, y + h, x + w - 2, y + h);
                if (kind == BiomeLandmarkKind.FairyRing) return Floor(x, y, x + w - 1, y + h - 1);
                return Bare(x - 1, y - 1, x + w, y + h);
            }
            foreach (var room in world.Rooms.Nodes)
            {
                if (room.Kind != RoomKind.Room || room.Chunk != null || room.Box != null) continue;
                if (room.Role != RoomRole.Plain && room.Role != RoomRole.Pack && room.Role != RoomRole.Ore) continue;
                if (room.Biome != Biome.Cave && room.Biome != Biome.Mushroom) continue;
                uint roll = Decor.Hash(seed, room.X, room.Y, 11, PropStream);
                if (roll % 100 >= LandmarkRoomsPerHundred) continue;
                var kinds = room.Biome == Biome.Mushroom ? new[] { BiomeLandmarkKind.FairyRing }
                    : (roll >> 8) % 10 < 3 ? new[] { BiomeLandmarkKind.MinerHut, BiomeLandmarkKind.MineFrame }
                    : new[] { BiomeLandmarkKind.MineFrame, BiomeLandmarkKind.MinerHut };
                bool done = false;
                int reach = Math.Max(4, room.Radius);
                foreach (var kind in kinds)
                {
                    // Rings out from the room's middle, each in map order: the same on the host and every client.
                    for (int ring = 0; ring <= reach && !done; ring++)
                    {
                        for (int dy = -ring; dy <= ring && !done; dy++)
                        {
                            for (int dx = -ring; dx <= ring && !done; dx++)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                                int cx = room.X + dx, cy = room.Y + dy;
                                if (!Fits(kind, room.Biome, cx - 3, cy - 2, out int w, out int h))
                                {
                                    // A small ring where the wide one has no room.
                                    if (kind != BiomeLandmarkKind.FairyRing || biomes[cx - 2, cy - 2] != room.Biome || !Floor(cx - 2, cy - 2, cx + 2, cy + 2)) continue;
                                    Lay(BiomeLandmarks.SmallRing, cx - 2, cy - 2);
                                    laid.Add(new BiomeLandmark(kind, cx - 2, cy - 2, 5, 5, cx, cy - 1));
                                    done = true;
                                    continue;
                                }
                                int left = cx - 3, bottom = cy - 2;
                                Lay(kind == BiomeLandmarkKind.MinerHut ? BiomeLandmarks.MinerHut : kind == BiomeLandmarkKind.MineFrame ? BiomeLandmarks.MineFrame
                                    : BiomeLandmarks.FairyRing, left, bottom, loose: kind == BiomeLandmarkKind.MineFrame);
                                var stand = kind == BiomeLandmarkKind.MinerHut ? (left + 1, bottom + 1) : kind == BiomeLandmarkKind.MineFrame ? (left + 1, bottom + 2)
                                    : (left + 3, bottom + 2);
                                laid.Add(new BiomeLandmark(kind, left, bottom, w, h, stand.Item1, stand.Item2));
                                done = true;
                            }
                        }
                    }
                    if (done) break;
                }
                if (!done) continue;
                // Nothing else is laid in its box or a tile round it, and no second landmark in the same room.
                var made = laid[laid.Count - 1];
                for (int y = made.Bottom - 1; y <= made.Bottom + made.Height; y++)
                    for (int x = made.Left - 1; x <= made.Left + made.Width; x++)
                        kept[y * width + x] = true;
            }
            return laid;
        }
    }
}
