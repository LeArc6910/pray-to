using System;

namespace Game.Core
{
    // The small things on a biome's floor that are only looked at (Docs/BiomeRework.md 4-3 꾸밈 층, 묶음 B0): pebbles,
    // tufts of roots and reeds, little caps, shards of ice, crystal and void, embers, bones. Nothing in the game's state:
    // no tile, no save, no message. Every client works out the same ones from the world's seed and the tile alone, so
    // they are where they were the last time and the same for everyone; where a tile stops being open floor (dug, built
    // on) the client hides what stood there. Nothing blocks.
    // B0 lays a small proof set at a low density; the biome packages (B1 to B4) add each biome's own.
    public enum DecorKind : byte
    {
        None,
        Pebbles,
        Tuft,
        Caps,
        Shards,
        Embers,
        Bones,
    }

    // One tile's pick: what, which of its drawings, mirrored or not, and how far off the tile's middle it stands (in
    // pixels, a tile being sixteen).
    public readonly struct DecorPick
    {
        public DecorPick(DecorKind kind, int variant, bool flip, int offsetX, int offsetY)
        {
            Kind = kind;
            Variant = (byte)variant;
            Flip = flip;
            OffsetX = (sbyte)offsetX;
            OffsetY = (sbyte)offsetY;
        }

        public DecorKind Kind { get; }
        public byte Variant { get; }
        public bool Flip { get; }
        public sbyte OffsetX { get; }
        public sbyte OffsetY { get; }
        public bool Any => Kind != DecorKind.None;
    }

    public static class Decor
    {
        // The random stream the decor's hash is salted with (Docs/BiomeRework.md 2절: 53 and on are the rework's).
        public const ulong Stream = 53;
        // Drawings of each kind (DecorArt).
        public const int Variants = 3;
        // How far a thing may stand off its tile's middle, in pixels across and up.
        public const int Shift = 3;
        public const int Lift = 2;
        // In a thousand tiles of open floor, before the one-tile spacing thins it a little (B0's low density; the biome
        // packages raise it toward 8 to 12 in a hundred).
        public const int PerThousand = 34;
        // The clumps: a smooth noise over cells this many tiles wide raises or lowers the chance, so things gather in
        // groups and leave bare floor between (Docs/BiomeRework.md 4-3 배치, Poisson in clumps rather than a grid).
        const int ClumpCell = 6;

        // What stands on the tile, if anything. A pure function of its arguments: the seed, the tile's biome and where
        // it is. One tile apart at least: of the tiles round it whose roll passes, only the one with the highest
        // priority keeps its thing (a one-tile Poisson disc).
        public static DecorPick At(ulong seed, Biome biome, int x, int y)
        {
            if (!Passes(seed, x, y)) return default;
            uint mine = Hash(seed, x, y, 2);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 || !Passes(seed, x + dx, y + dy)) continue;
                    uint theirs = Hash(seed, x + dx, y + dy, 2);
                    // Ties go to the lower tile then the left one, so exactly one of two keeps it.
                    if (theirs > mine || theirs == mine && (dy < 0 || dy == 0 && dx < 0)) return default;
                }
            }
            uint pick = Hash(seed, x, y, 3);
            var kind = KindOf(biome, (int)(pick % 100));
            int variant = (int)(pick / 100 % Variants);
            bool flip = (pick >> 12 & 1) == 1;
            int offsetX = (int)((pick >> 13) % (Shift * 2 + 1)) - Shift;
            int offsetY = (int)((pick >> 20) % (Lift * 2 + 1)) - Lift;
            return new DecorPick(kind, variant, flip, offsetX, offsetY);
        }

        // The kinds of each biome and how often, out of a hundred (B0's proof set: a common one and the biome's own).
        public static DecorKind KindOf(Biome biome, int roll) => biome switch
        {
            Biome.Mushroom => roll < 60 ? DecorKind.Caps : DecorKind.Pebbles,
            Biome.Ice => roll < 60 ? DecorKind.Shards : DecorKind.Pebbles,
            Biome.Crystal => roll < 70 ? DecorKind.Shards : DecorKind.Pebbles,
            Biome.Lava => roll < 60 ? DecorKind.Embers : DecorKind.Pebbles,
            Biome.Abyss => roll < 50 ? DecorKind.Shards : DecorKind.Pebbles,
            Biome.Lake => roll < 60 ? DecorKind.Tuft : DecorKind.Pebbles,
            Biome.Sand => roll < 40 ? DecorKind.Bones : DecorKind.Pebbles,
            _ => roll < 60 ? DecorKind.Pebbles : DecorKind.Tuft,
        };

        // Whether the tile may hold a thing at all: open floor, with nothing round it but floor and cave rock (so not
        // on or beside a road, a door, a laid floor, a place's things, an arena's pattern, anything built, water or
        // lava). The client also keeps the story's places and the arenas clear, which it knows and the map does not.
        public static bool Clear(TileMap map, int x, int y)
        {
            if (map[x, y] != TileType.Floor) return false;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var type = map[x + dx, y + dy];
                    if (type != TileType.Floor && !Natural(type)) return false;
                }
            }
            // Two tiles off a road or a door, so a way through stays bare (Docs/BiomeRework.md 4-3 비우는 곳).
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    var type = map[x + dx, y + dy];
                    if (Structures.IsWay(type) || TileMap.IsDoor(type)) return false;
                }
            }
            return true;
        }

        // The cave's own rock: the walls, ore and the rooms' walls.
        static bool Natural(TileType type) =>
            type == TileType.Wall || type == TileType.CopperOre || type == TileType.IronOre || type == TileType.MithrilOre
            || type == TileType.ObsidianOre || type == TileType.VoidOre || TileMap.IsRoomWall(type);

        static bool Passes(ulong seed, int x, int y)
        {
            // The clump: smooth value noise from the corners of the tile's cell, 0.25 to 1.75 times the rate.
            int cx = Floor(x, ClumpCell), cy = Floor(y, ClumpCell);
            float fx = (x - cx * ClumpCell + 0.5f) / ClumpCell, fy = (y - cy * ClumpCell + 0.5f) / ClumpCell;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Unit(seed, cx, cy), b = Unit(seed, cx + 1, cy), c = Unit(seed, cx, cy + 1), d = Unit(seed, cx + 1, cy + 1);
            float noise = a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
            float clump = 0.25f + 1.5f * noise * noise * (3 - 2 * noise);
            float chance = PerThousand / 1000f * clump;
            return (Hash(seed, x, y, 1) & 0xFFFFFF) * (1f / 16777216f) < chance;
        }

        static int Floor(int value, int size) => value >= 0 ? value / size : (value - size + 1) / size;

        static float Unit(ulong seed, int cx, int cy) => (Hash(seed, cx, cy, 4) & 0xFFFFFF) * (1f / 16777216f);

        // A 32-bit hash of the seed, the tile and a salt (SplitMix64's finaliser): the same on every runtime.
        static uint Hash(ulong seed, int x, int y, uint salt)
        {
            unchecked
            {
                ulong z = seed ^ (Stream << 56) ^ ((ulong)(uint)x << 32 | (uint)y) * 0x9E3779B97F4A7C15UL ^ (ulong)salt * 0xD1B54A32D192ED03UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (uint)(z ^ (z >> 32));
            }
        }
    }
}
