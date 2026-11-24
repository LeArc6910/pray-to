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
        // The dirt cave's and the mushroom forest's own (묶음 B1a): a cobweb in a corner of the rock, a tuft of grass
        // with glowing tips, white threads of mycelium on the floor, a clump of moss.
        Web,
        GlowGrass,
        Threads,
        Moss,
        // The ice cave's and the sand cave's own (묶음 B2a): a tuft of frosted grass, a shallow patch of snow (flat), a
        // tuft of dry grass.
        FrostGrass,
        Snow,
        DryGrass,
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
        // The biomes whose package is made (B1a: the dirt cave, the mushroom forest; B2a: the ice cave, the sand cave):
        // eight to twelve in a hundred tiles of clear floor once thinned.
        public const int DensePerThousand = 270;
        // The clumps: a smooth noise over cells this many tiles wide raises or lowers the chance, so things gather in
        // groups and leave bare floor between (Docs/BiomeRework.md 4-3 배치, Poisson in clumps rather than a grid).
        const int ClumpCell = 6;

        // What stands on the tile, if anything. A pure function of its arguments: the seed, the tile's biome and where
        // it is. One tile apart at least: of the tiles round it whose roll passes, only the one with the highest
        // priority keeps its thing (a one-tile Poisson disc).
        public static DecorPick At(ulong seed, Biome biome, int x, int y)
        {
            int rate = RateOf(biome);
            if (!Passes(seed, x, y, rate)) return default;
            uint mine = Hash(seed, x, y, 2);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 || !Passes(seed, x + dx, y + dy, rate)) continue;
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
            Biome.Mushroom => roll < 32 ? DecorKind.GlowGrass : roll < 60 ? DecorKind.Caps : roll < 80 ? DecorKind.Threads : DecorKind.Moss,
            Biome.Ice => roll < 30 ? DecorKind.FrostGrass : roll < 60 ? DecorKind.Shards : roll < 85 ? DecorKind.Snow : DecorKind.Pebbles,
            Biome.Crystal => roll < 70 ? DecorKind.Shards : DecorKind.Pebbles,
            Biome.Lava => roll < 60 ? DecorKind.Embers : DecorKind.Pebbles,
            Biome.Abyss => roll < 50 ? DecorKind.Shards : DecorKind.Pebbles,
            Biome.Lake => roll < 60 ? DecorKind.Tuft : DecorKind.Pebbles,
            Biome.Sand => roll < 35 ? DecorKind.DryGrass : roll < 60 ? DecorKind.Bones : DecorKind.Pebbles,
            _ => roll < 40 ? DecorKind.Pebbles : roll < 70 ? DecorKind.Tuft : DecorKind.Caps,
        };

        // How many in a thousand tiles roll a thing, by biome.
        public static int RateOf(Biome biome) => biome == Biome.Cave || biome == Biome.Mushroom || biome == Biome.Ice || biome == Biome.Sand ? DensePerThousand : PerThousand;

        // The kinds that sway: drawn nudged for a moment when someone brushes past (the client's rustle).
        public static bool Sways(DecorKind kind) => kind == DecorKind.Tuft || kind == DecorKind.Caps || kind == DecorKind.GlowGrass || kind == DecorKind.FrostGrass
            || kind == DecorKind.DryGrass;

        // The kinds that lie flat on the floor: no shadow, never in front of anyone's feet.
        public static bool Flat(DecorKind kind) => kind == DecorKind.Web || kind == DecorKind.Threads || kind == DecorKind.Moss || kind == DecorKind.Snow;

        // In a hundred corners of the dirt cave's rock (rock above and at a side), how many have a cobweb.
        public const int WebPercent = 30;

        // The pick as it shows on the map: nothing off clear floor (Clear). In the dirt cave a corner of the rock may
        // have a cobweb instead, hung into the corner (the upper left, or mirrored into the upper right).
        public static DecorPick On(TileMap map, ulong seed, Biome biome, int x, int y)
        {
            if (!Clear(map, x, y)) return default;
            if (biome == Biome.Cave && map[x, y + 1] == TileType.Wall)
            {
                bool left = map[x - 1, y] == TileType.Wall, right = map[x + 1, y] == TileType.Wall;
                uint roll = Hash(seed, x, y, 5);
                if ((left || right) && roll % 100 < WebPercent) return new DecorPick(DecorKind.Web, (int)(roll / 100 % Variants), !left, left ? -2 : 2, Lift);
            }
            return At(seed, biome, x, y);
        }

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
            || type == TileType.ObsidianOre || type == TileType.VoidOre || TileMap.IsRoomWall(type)
            // The biomes' own props (and the right half of a wide one): cover grows up to their feet.
            || BiomeProps.Is(type) || BiomeLandmarks.Is(type) || type == TileType.MushroomTree || type == TileType.StructurePart;

        static bool Passes(ulong seed, int x, int y, int rate)
        {
            // The clump: smooth value noise from the corners of the tile's cell, 0.25 to 1.75 times the rate.
            int cx = Floor(x, ClumpCell), cy = Floor(y, ClumpCell);
            float fx = (x - cx * ClumpCell + 0.5f) / ClumpCell, fy = (y - cy * ClumpCell + 0.5f) / ClumpCell;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Unit(seed, cx, cy), b = Unit(seed, cx + 1, cy), c = Unit(seed, cx, cy + 1), d = Unit(seed, cx + 1, cy + 1);
            float noise = a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
            float clump = 0.25f + 1.5f * noise * noise * (3 - 2 * noise);
            float chance = rate / 1000f * clump;
            return (Hash(seed, x, y, 1) & 0xFFFFFF) * (1f / 16777216f) < chance;
        }

        // The same clumping for another stream's things (the props, WorldGenerator.Props.cs): 0.25 to 1.75, smooth over
        // cells that many tiles wide.
        internal static float Clump(ulong seed, int x, int y, int cell, ulong stream)
        {
            int cx = Floor(x, cell), cy = Floor(y, cell);
            float fx = (x - cx * cell + 0.5f) / cell, fy = (y - cy * cell + 0.5f) / cell;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float U(int ux, int uy) => (Hash(seed, ux, uy, 4, stream) & 0xFFFFFF) * (1f / 16777216f);
            float a = U(cx, cy), b = U(cx + 1, cy), c = U(cx, cy + 1), d = U(cx + 1, cy + 1);
            float noise = a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
            return 0.25f + 1.5f * noise * noise * (3 - 2 * noise);
        }

        static int Floor(int value, int size) => value >= 0 ? value / size : (value - size + 1) / size;

        static float Unit(ulong seed, int cx, int cy) => (Hash(seed, cx, cy, 4) & 0xFFFFFF) * (1f / 16777216f);

        // A 32-bit hash of the seed, the tile and a salt (SplitMix64's finaliser): the same on every runtime.
        static uint Hash(ulong seed, int x, int y, uint salt) => Hash(seed, x, y, salt, Stream);

        internal static uint Hash(ulong seed, int x, int y, uint salt, ulong stream)
        {
            unchecked
            {
                ulong z = seed ^ (stream << 56) ^ ((ulong)(uint)x << 32 | (uint)y) * 0x9E3779B97F4A7C15UL ^ (ulong)salt * 0xD1B54A32D192ED03UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (uint)(z ^ (z >> 32));
            }
        }
    }
}
