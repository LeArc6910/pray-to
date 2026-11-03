using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Step counts from the nearest target over open tiles, so enemies walk around walls toward players instead of
    // pressing into them. Diagonal steps are only allowed when both sides are open, so nothing cuts a corner.
    public sealed class FlowField
    {
        public const int Unreached = int.MaxValue;

        static readonly (int X, int Y)[] Neighbours =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };

        readonly TileMap _map;
        readonly int[] _distance;
        readonly Queue<(int X, int Y)> _queue = new Queue<(int X, int Y)>();

        // Blocking tiles a monster would rather break through than give up on (a base's built walls during a defense,
        // World.Defense.cs): counted open here. Null for none.
        public Func<int, int, bool> Through { get; set; }

        bool Blocks(int x, int y) => _map.BlocksMonsters(x, y) && (Through == null || !Through(x, y));

        public FlowField(TileMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _distance = new int[map.Width * map.Height];
            Array.Fill(_distance, Unreached);
        }

        public void Build(IEnumerable<Vec2> targets, int maxDistance)
        {
            Array.Fill(_distance, Unreached);
            _queue.Clear();
            foreach (var target in targets)
            {
                int x = TileMap.Cell(target.X);
                int y = TileMap.Cell(target.Y);
                if (Blocks(x, y) || _distance[Index(x, y)] == 0) continue;
                _distance[Index(x, y)] = 0;
                _queue.Enqueue((x, y));
            }

            while (_queue.Count > 0)
            {
                var (x, y) = _queue.Dequeue();
                int next = _distance[Index(x, y)] + 1;
                if (next > maxDistance) continue;
                foreach (var (dx, dy) in Neighbours)
                {
                    if (!CanStep(x, y, dx, dy) || _distance[Index(x + dx, y + dy)] <= next) continue;
                    _distance[Index(x + dx, y + dy)] = next;
                    _queue.Enqueue((x + dx, y + dy));
                }
            }
        }

        public int DistanceAt(int x, int y) => _map.InBounds(x, y) ? _distance[Index(x, y)] : Unreached;

        // Toward the centre of the neighbouring tile that is closest to a target. Zero when there is no way or the
        // position is already on a target's tile.
        public Vec2 DirectionFrom(Vec2 position)
        {
            int x = TileMap.Cell(position.X);
            int y = TileMap.Cell(position.Y);
            int best = DistanceAt(x, y);
            if (best == Unreached || best == 0) return Vec2.Zero;

            int bestX = x;
            int bestY = y;
            foreach (var (dx, dy) in Neighbours)
            {
                if (!CanStep(x, y, dx, dy)) continue;
                int distance = DistanceAt(x + dx, y + dy);
                if (distance >= best) continue;
                best = distance;
                bestX = x + dx;
                bestY = y + dy;
            }
            return bestX == x && bestY == y ? Vec2.Zero : (TileMap.Center(bestX, bestY) - position).Normalized;
        }

        bool CanStep(int x, int y, int dx, int dy)
        {
            // Monsters follow it, so doors are walls here.
            if (Blocks(x + dx, y + dy)) return false;
            return dx == 0 || dy == 0 || (!Blocks(x + dx, y) && !Blocks(x, y + dy));
        }

        int Index(int x, int y) => y * _map.Width + x;
    }
}
