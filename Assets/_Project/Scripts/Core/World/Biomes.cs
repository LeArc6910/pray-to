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
    }

    // Which biome every tile is in, worked out from the seed with integer math only, so the host and every client
    // agree to the tile. The border is pushed about by a smooth noise so it winds instead of running straight.
    public sealed class BiomeMap
    {
        // Plain cave this far around the spawn (give or take the wobble).
        public const int CaveRadius = 32;
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

        // corner is the deep arena's corner as signs (±1, ±1): the crystal caverns lie there, the lava fields
        // opposite; mushroomFirst picks which of the other two corners gets the mushroom forest.
        public static BiomeMap Make(ulong seed, int width, int height, int spawnX, int spawnY, (int X, int Y) corner, bool mushroomFirst)
        {
            var tiles = new Biome[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - spawnX + Noise(seed, x, y, 1);
                    int dy = y - spawnY + Noise(seed, x, y, 2);
                    if (dx * dx + dy * dy < CaveRadius * CaveRadius) continue;
                    int sx = dx >= 0 ? 1 : -1;
                    int sy = dy >= 0 ? 1 : -1;
                    Biome biome;
                    if (sx == corner.X && sy == corner.Y) biome = Biome.Crystal;
                    else if (sx == -corner.X && sy == -corner.Y) biome = Biome.Lava;
                    else biome = (sx == corner.X) == mushroomFirst ? Biome.Mushroom : Biome.Ice;
                    tiles[y * width + x] = biome;
                }
            }
            return new BiomeMap(width, height, tiles);
        }

        // A smooth value from -Wobble to Wobble: random values on a lattice every Cell tiles, blended between them.
        static int Noise(ulong seed, int x, int y, int salt)
        {
            int cx = Math.DivRem(x, Cell, out int fx);
            int cy = Math.DivRem(y, Cell, out int fy);
            int v00 = Lattice(seed, cx, cy, salt);
            int v10 = Lattice(seed, cx + 1, cy, salt);
            int v01 = Lattice(seed, cx, cy + 1, salt);
            int v11 = Lattice(seed, cx + 1, cy + 1, salt);
            int sum = v00 * (Cell - fx) * (Cell - fy) + v10 * fx * (Cell - fy) + v01 * (Cell - fx) * fy + v11 * fx * fy;
            return sum / (Cell * Cell);
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
            _ => "흙 동굴",
        };
    }
}
