using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Everything that moves is a circle; walls are whole tiles. Small sub-steps keep fast movers from skipping
    // through a wall, and pushing out along the shortest way lets circles slide along walls instead of sticking.
    public static class Collision
    {
        const float MaxStep = 0.25f;
        const float Skin = 0.0005f;

        // monster: a monster or boss moving, which doors stop as well (TileMap.BlocksMonsters). passable: tiles (y *
        // width + x) this mover goes through though they are solid: a player's own locked doors (World.DoorsFor).
        public static Vec2 Move(TileMap map, Vec2 position, float radius, Vec2 delta, bool monster = false, ISet<int> passable = null)
        {
            int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length / MaxStep));
            var step = delta / steps;
            for (int i = 0; i < steps; i++) position = Resolve(map, position + step, radius, monster, passable);
            return position;
        }

        public static bool Overlaps(TileMap map, Vec2 position, float radius, bool monster = false, ISet<int> passable = null)
        {
            for (int y = TileMap.Cell(position.Y - radius); y <= TileMap.Cell(position.Y + radius); y++)
            {
                for (int x = TileMap.Cell(position.X - radius); x <= TileMap.Cell(position.X + radius); x++)
                {
                    if (Blocks(map, x, y, monster, passable) && (position - Nearest(map, position, x, y, monster, passable)).LengthSquared < radius * radius) return true;
                }
            }
            return false;
        }

        static bool Blocks(TileMap map, int x, int y, bool monster, ISet<int> passable = null) =>
            (monster ? map.BlocksMonsters(x, y) : map.IsSolid(x, y)) && (passable == null || passable.Count == 0 || !map.InBounds(x, y) || !passable.Contains(y * map.Width + x));

        public static Vec2 Resolve(TileMap map, Vec2 position, float radius, bool monster = false, ISet<int> passable = null)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool pushed = false;
                for (int y = TileMap.Cell(position.Y - radius); y <= TileMap.Cell(position.Y + radius); y++)
                {
                    for (int x = TileMap.Cell(position.X - radius); x <= TileMap.Cell(position.X + radius); x++)
                    {
                        if (!Blocks(map, x, y, monster, passable)) continue;
                        var away = position - Nearest(map, position, x, y, monster, passable);
                        float distanceSquared = away.LengthSquared;
                        if (distanceSquared >= radius * radius) continue;

                        if (distanceSquared > 1e-8f)
                        {
                            float distance = MathF.Sqrt(distanceSquared);
                            position += away * ((radius + Skin - distance) / distance);
                        }
                        else
                        {
                            position = LeaveThroughNearestSide(position, x, y, radius);
                        }
                        pushed = true;
                    }
                }
                if (!pushed) break;
            }
            return position;
        }

        // The point of the wall closest to the position, looking at the tile together with its solid neighbours
        // in a row and in a column. A straight run of walls then acts as one surface, so a circle sliding along it
        // is not nudged at the seams between tiles. Rows and columns are checked apart so an inside corner is not
        // mistaken for wall.
        static Vec2 Nearest(TileMap map, Vec2 position, int x, int y, bool monster, ISet<int> passable = null)
        {
            var row = Clamp(position, Blocks(map, x - 1, y, monster, passable) ? x - 1 : x, Blocks(map, x + 1, y, monster, passable) ? x + 2 : x + 1, y, y + 1);
            var column = Clamp(position, x, x + 1, Blocks(map, x, y - 1, monster, passable) ? y - 1 : y, Blocks(map, x, y + 1, monster, passable) ? y + 2 : y + 1);
            return (position - row).LengthSquared <= (position - column).LengthSquared ? row : column;
        }

        static Vec2 Clamp(Vec2 position, float minX, float maxX, float minY, float maxY) =>
            new Vec2(Math.Clamp(position.X, minX, maxX), Math.Clamp(position.Y, minY, maxY));

        // The centre ended up inside the tile; leave through whichever side is closest.
        static Vec2 LeaveThroughNearestSide(Vec2 position, int x, int y, float radius)
        {
            float left = position.X - x;
            float right = x + 1 - position.X;
            float down = position.Y - y;
            float up = y + 1 - position.Y;
            float nearest = Math.Min(Math.Min(left, right), Math.Min(down, up));
            if (nearest == left) return new Vec2(x - radius - Skin, position.Y);
            if (nearest == right) return new Vec2(x + 1 + radius + Skin, position.Y);
            if (nearest == down) return new Vec2(position.X, y - radius - Skin);
            return new Vec2(position.X, y + 1 + radius + Skin);
        }
    }
}
