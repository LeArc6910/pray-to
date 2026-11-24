using System;

namespace Game.Core
{
    // How much room a body has at each tile (2026-11-24, Docs/BiomeRework.md 4-2 큰 몸이 들어가는 법 3): the radius of the
    // widest circle that can stand somewhere in the tile (its middle or one of its corners) without touching a tile
    // monsters cannot walk through, up to Cap. A corridor two tiles wide has 1.0 (its middle line runs along the
    // tiles' corners), three wide 1.5, a one-tile gap or door 0.5. The big bodies (L, XL) step only onto tiles with
    // their radius and MonsterSizes.FitMargin of room (World.WayFor), and come into being only there.
    //
    // Worked out a tile at a time when first asked and kept in a byte a tile (16ths of a tile), all forgotten when any
    // tile of the map changes (TileMap.Version): a world with no big body about never works out any of it.
    public sealed class Clearance
    {
        public const float Cap = 2.5f;
        const int Units = 16;
        const byte Unknown = 255;
        // Blocking tiles further than this from the tile cannot be nearer than Cap to any of its points.
        const int Window = 4;

        readonly TileMap _map;
        byte[] _known;
        int _version = -1;

        public Clearance(TileMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        // The room at a tile, in tiles: 0 on a tile monsters cannot walk.
        public float At(int x, int y)
        {
            if (!_map.InBounds(x, y)) return 0;
            if (_known == null || _known.Length != _map.Width * _map.Rows)
            {
                _known = new byte[_map.Width * _map.Rows];
                _version = -1;
            }
            if (_version != _map.Version)
            {
                Array.Fill(_known, Unknown);
                _version = _map.Version;
            }
            int index = y * _map.Width + x;
            if (_known[index] == Unknown) _known[index] = (byte)MathF.Round(Measure(x, y) * Units);
            return _known[index] / (float)Units;
        }

        // Whether a body of that radius may step onto the tile: any open tile for one that fits a one-tile gap (XS to M),
        // else one with its radius and the margin of room.
        public bool Fits(int x, int y, float radius)
        {
            float need = MonsterSizes.ClearanceFor(radius);
            if (need <= 0) return !_map.BlocksMonsters(x, y);
            return At(x, y) >= need - 0.5f / Units;
        }

        public bool Fits(Vec2 at, float radius) => Fits(TileMap.Cell(at.X), TileMap.Cell(at.Y), radius);

        // Which of the tiles round the one measured block, read once for its five points.
        readonly bool[] _blocks = new bool[(2 * Window + 1) * (2 * Window + 1)];

        float Measure(int x, int y)
        {
            if (_map.BlocksMonsters(x, y)) return 0;
            const int side = 2 * Window + 1;
            for (int dy = -Window; dy <= Window; dy++)
                for (int dx = -Window; dx <= Window; dx++)
                    _blocks[(dy + Window) * side + dx + Window] = _map.BlocksMonsters(x + dx, y + dy);
            float best = 0;
            for (int point = 0; point < 5; point++)
            {
                float px = point == 0 ? 0.5f : point & 1;
                float py = point == 0 ? 0.5f : (point >> 1) & 1;
                float nearest = Cap * Cap;
                for (int dy = -Window; dy <= Window; dy++)
                {
                    for (int dx = -Window; dx <= Window; dx++)
                    {
                        if (!_blocks[(dy + Window) * side + dx + Window]) continue;
                        float ax = Math.Max(0, Math.Max(dx - px, px - (dx + 1)));
                        float ay = Math.Max(0, Math.Max(dy - py, py - (dy + 1)));
                        float squared = ax * ax + ay * ay;
                        if (squared < nearest) nearest = squared;
                    }
                }
                best = Math.Max(best, MathF.Sqrt(nearest));
            }
            return Math.Min(Cap, best);
        }
    }
}
