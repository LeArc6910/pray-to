using System;

namespace Game.Core
{
    public enum TileType : byte
    {
        Floor,
        Wall,
    }

    public sealed class TileMap
    {
        readonly TileType[] _tiles;

        public TileMap(int width, int height, TileType fill = TileType.Wall)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            if (fill != default) Array.Fill(_tiles, fill);
        }

        public int Width { get; }
        public int Height { get; }

        // Outside the map reads as wall, so nothing can walk off the edge.
        public TileType this[int x, int y]
        {
            get => InBounds(x, y) ? _tiles[y * Width + x] : TileType.Wall;
            set
            {
                if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the map.");
                _tiles[y * Width + x] = value;
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsSolid(int x, int y) => this[x, y] == TileType.Wall;

        public static int Cell(float coordinate) => (int)MathF.Floor(coordinate);

        public static Vec2 Center(int x, int y) => new Vec2(x + 0.5f, y + 0.5f);
    }
}
