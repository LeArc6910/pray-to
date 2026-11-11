using System;

namespace Game.Core
{
    // The kinds of cave (Docs/Biomes.md). The middle of the map is plain cave; further out each quarter of the map
    // has its own: the crystal caverns on the deep arena's side, the lava fields opposite, and the mushroom forest
    // and the ice caves in the other two.
    public enum Biome : byte
    {
        Cave,
        Mushroom,
        Ice,
        Crystal,
        Lava,
        // The far end of one of the corners beside the lava fields, past AbyssDistance (Docs/Biomes.md).
        Abyss,
        // The far end of the other of those corners: an underground lake of many pools (2026-11-09).
        Lake,
        // The near band of the crystal caverns' quarter, between the plain cave and the crystal: sandstone and drifts
        // (2026-11-09, the level 15 zone between the first boss and the second).
        Sand,
    }

    // Which biome every tile is in, worked out from the seed with integer math only, so the host and every client
    // agree to the tile. The border is pushed about by a smooth noise so it winds instead of running straight.
    public sealed class BiomeMap
    {
        // Plain cave this far around the spawn (give or take the wobble).
        public const int CaveRadius = 32;
        // The abyss takes its corner this far from the spawn, along either axis.
        public const int AbyssDistance = 40;
        // The sand caves are the crystal quarter's tiles nearer the spawn than this (give or take its own wobble).
        public const int SandRadius = 52;
        // And all of it this near the sand arena (WorldGenerator), so the arena is always in it.
        const int SandArenaReach = 15;
        const int Wobble = 8;
        const int Cell = 16;

        readonly Biome[] _tiles;

        BiomeMap(int width, int height, Biome[] tiles)
        {
            Width = width;
            Height = height;
            _tiles = tiles;
        }

        public int Width { get; }
        public int Height { get; }

        // Outside the map counts as the nearest tile inside it.
        public Biome this[int x, int y] => _tiles[Math.Clamp(y, 0, Height - 1) * Width + Math.Clamp(x, 0, Width - 1)];

        public Biome At(Vec2 position) => this[TileMap.Cell(position.X), TileMap.Cell(position.Y)];

        // Everything plain cave, for worlds made without a seed (tests, rooms).
        public static BiomeMap Plain(int width, int height) => new BiomeMap(width, height, new Biome[width * height]);

        // Each tile as the function says, for rooms with biomes laid out by hand (tests).
        public static BiomeMap Of(int width, int height, Func<int, int, Biome> biome)
        {
            var tiles = new Biome[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) tiles[y * width + x] = biome(x, y);
            }
            return new BiomeMap(width, height, tiles);
        }

        // corner is the deep arena's corner as signs (±1, ±1): the crystal caverns lie there, the lava fields
        // opposite; mushroomFirst picks which of the other two corners gets the mushroom forest.
        // abyss is the corner whose far end is the abyss, lake the one whose far end is the lake; null for none.
        public static BiomeMap Make(ulong seed, int width, int height, int spawnX, int spawnY, (int X, int Y) corner, bool mushroomFirst,
            (int X, int Y)? abyss = null, (int X, int Y)? lake = null, (int X, int Y)? sandArena = null)
        {
            var tiles = new Biome[width * height];
            // Every distance and the borders' wobble grow with the map (2026-11-11: the game's map is four times as wide
            // as the 128 tiles these were set for, WorldGenerator.ScaleOf).
            int k = WorldGenerator.ScaleOf(width, height);
            int caveRadius = CaveRadius * k, sandRadius = SandRadius * k, abyssDistance = AbyssDistance * k, sandArenaReach = SandArenaReach * k;
            int Noise(ulong noiseSeed, int nx, int ny, int salt) => NoiseAt(noiseSeed, nx, ny, salt, Cell * k, Wobble * k);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - spawnX + Noise(seed, x, y, 1);
                    int dy = y - spawnY + Noise(seed, x, y, 2);
                    // Round the sand arena it is sand whatever the wobble says.
                    if (sandArena is (int ax0, int ay0) && (x - ax0) * (x - ax0) + (y - ay0) * (y - ay0) <= sandArenaReach * sandArenaReach)
                    {
                        tiles[y * width + x] = Biome.Sand;
                        continue;
                    }
                    if (dx * dx + dy * dy < caveRadius * caveRadius) continue;
                    int sx = dx >= 0 ? 1 : -1;
                    int sy = dy >= 0 ? 1 : -1;
                    Biome biome;
                    if (sx == corner.X && sy == corner.Y)
                    {
                        int sandX = dx + Noise(seed, x, y, 5), sandY = dy + Noise(seed, x, y, 6);
                        biome = sandX * sandX + sandY * sandY < sandRadius * sandRadius ? Biome.Sand : Biome.Crystal;
                    }
                    else if (sx == -corner.X && sy == -corner.Y) biome = Biome.Lava;
                    else biome = (sx == corner.X) == mushroomFirst ? Biome.Mushroom : Biome.Ice;
                    // The square at the far end of the abyss corner, with a wobbly edge of its own.
                    if (abyss is (int ax, int ay) && sx == ax && sy == ay
                        && Math.Min(Math.Abs(dx), Math.Abs(dy)) + Noise(seed, x, y, 3) / 2 >= abyssDistance - Wobble * k
                        && Math.Max(Math.Abs(dx), Math.Abs(dy)) >= abyssDistance)
                    {
                        biome = Biome.Abyss;
                    }
                    // The lake takes the far end of the other side corner the same way, on its own wobble.
                    if (lake is (int lx, int ly) && sx == lx && sy == ly
                        && Math.Min(Math.Abs(dx), Math.Abs(dy)) + Noise(seed, x, y, 4) / 2 >= abyssDistance - Wobble * k
                        && Math.Max(Math.Abs(dx), Math.Abs(dy)) >= abyssDistance)
                    {
                        biome = Biome.Lake;
                    }
                    tiles[y * width + x] = biome;
                }
            }
            return new BiomeMap(width, height, tiles);
        }

        // A smooth value from -wobble to wobble: random values on a lattice every cell tiles, blended between them.
        static int NoiseAt(ulong seed, int x, int y, int salt, int cell, int wobble)
        {
            int cx = Math.DivRem(x, cell, out int fx);
            int cy = Math.DivRem(y, cell, out int fy);
            int v00 = Lattice(seed, cx, cy, salt);
            int v10 = Lattice(seed, cx + 1, cy, salt);
            int v01 = Lattice(seed, cx, cy + 1, salt);
            int v11 = Lattice(seed, cx + 1, cy + 1, salt);
            int sum = v00 * (cell - fx) * (cell - fy) + v10 * fx * (cell - fy) + v01 * (cell - fx) * fy + v11 * fx * fy;
            // The lattice gives -Wobble to Wobble; a bigger wobble is the same shape stretched.
            return sum * (wobble / Wobble) / (cell * cell);
        }

        static int Lattice(ulong seed, int x, int y, int salt)
        {
            ulong h = seed ^ 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)x * 0xBF58476D1CE4E5B9UL;
            h ^= (ulong)(uint)y * 0x94D049BB133111EBUL;
            h ^= (ulong)salt * 0xD6E8FEB86659FD93UL;
            h ^= h >> 31;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 29;
            return (int)(h % (2 * Wobble + 1)) - Wobble;
        }

        public static string Name(Biome biome) => biome switch
        {
            Biome.Mushroom => "버섯 숲",
            Biome.Ice => "얼음 동굴",
            Biome.Crystal => "수정 동굴",
            Biome.Lava => "용암 지대",
            Biome.Abyss => "공허의 심연",
            Biome.Lake => "지하 호수",
            Biome.Sand => "모래 동굴",
            _ => "흙 동굴",
        };
    }
}
