using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The big bodies (2026-11-24, Docs/BiomeRework.md 4-2 큰 몸이 들어가는 법, B0b): an L or XL monster walks only where
    // it fits (Clearance), comes only in a room that holds it and keeps to that room, and is thrown by a blow by its size
    // class's share. The swarms, fliers and ordinary bodies (XS to M) go as they always did.
    public sealed partial class World
    {
        Clearance _clearance;

        // How much room there is at each tile, worked out a tile at a time as the big bodies ask (Clearance).
        public Clearance Clearance => _clearance ??= new Clearance(Map);

        // The way toward its target along the players' flow field. A big body steps only onto tiles with room for it,
        // inside its room's leash (Enemy.BoundRadius); zero when no such tile is nearer than where it stands, and then it
        // waits there (the callers).
        internal Vec2 WayFor(Enemy enemy)
        {
            var definition = enemy.Definition;
            if (!definition.Big) return _flow.DirectionFrom(enemy.Position);
            return _flow.DirectionFrom(enemy.Position, (x, y) => Clearance.Fits(x, y, definition.Radius) && InBound(enemy, TileMap.Center(x, y)));
        }

        // Whether a spot is inside a big body's leash: its room and MonsterSizes.RoomLeash past its edge. Everywhere for
        // one with no room (a playtest's, a test's).
        static bool InBound(Enemy enemy, Vec2 at) =>
            float.IsPositiveInfinity(enemy.BoundRadius) || Vec2.Distance(at, enemy.BoundCenter) <= enemy.BoundRadius;

        // Ties a big body to the room it came in: it never steps further than MonsterSizes.RoomLeash past its edge.
        static void Bind(Enemy enemy, Vec2 middle, float radius)
        {
            if (!enemy.Definition.Big) return;
            enemy.BoundCenter = middle;
            enemy.BoundRadius = radius + MonsterSizes.RoomLeash;
        }

        // How fast a blow throws a monster: the blow's speed times its size class's share (XS ×2 … L ×0.3, XL not at all).
        static Vec2 Knock(Enemy enemy, Vec2 direction, float speed) =>
            direction * (speed * MonsterSizes.Of(enemy.Definition.Size).Knockback);

        // ---- Where a big body may come ----

        // Whether a room of the world holds a body of that kind: L in a room of radius MonsterSizes.LeastRoomRadius or
        // more, XL only where a square of open floor MonsterSizes.XlOpenSquare wide is. Any room holds XS to M.
        bool RoomHolds(RoomNode room, MonsterDefinition kind)
        {
            if (!kind.Big) return true;
            if (room.Radius < MonsterSizes.LeastRoomRadius) return false;
            if (kind.Size == MonsterSize.L) return true;
            return OpenSquareIn(room.Center, room.Radius);
        }

        // Rooms already looked at for the XL rule, by the map's version and the middle they were looked at from.
        readonly Dictionary<(int X, int Y, int Version), bool> _openSquares = new Dictionary<(int X, int Y, int Version), bool>();

        // Whether a square of open floor MonsterSizes.XlOpenSquare tiles wide lies wholly within radius of the middle.
        internal bool OpenSquareIn(Vec2 middle, float radius)
        {
            var key = (TileMap.Cell(middle.X), TileMap.Cell(middle.Y), Map.Version);
            if (_openSquares.TryGetValue(key, out bool known)) return known;
            int side = MonsterSizes.XlOpenSquare;
            int reach = (int)MathF.Ceiling(radius);
            bool found = false;
            for (int y0 = key.Item2 - reach; y0 <= key.Item2 + reach - side + 1 && !found; y0++)
            {
                for (int x0 = key.Item1 - reach; x0 <= key.Item1 + reach - side + 1 && !found; x0++)
                {
                    var middleOf = new Vec2(x0 + side / 2f, y0 + side / 2f);
                    if (Vec2.Distance(middleOf, middle) + side * 0.5f > radius + 1) continue;
                    found = true;
                    for (int y = y0; y < y0 + side && found; y++)
                        for (int x = x0; x < x0 + side && found; x++)
                            if (Map.BlocksMonsters(x, y) || Map[x, y] == TileType.Lava || Map[x, y] == TileType.Water) found = false;
                }
            }
            if (_openSquares.Count > 256) _openSquares.Clear();
            _openSquares[key] = found;
            return found;
        }

        // A spot for a big body near the middle where it fits (Clearance) and fits(at) allows, away from the spots already
        // taken by bigger bodies (each with its radius); null when there is none. Searched ring by ring out to within.
        Vec2? BigSpot(MonsterDefinition kind, Vec2 middle, float within, Func<Vec2, bool> fits, List<(Vec2 At, float Radius)> taken)
        {
            int reach = (int)MathF.Ceiling(within);
            int cx = TileMap.Cell(middle.X), cy = TileMap.Cell(middle.Y);
            for (int ring = 0; ring <= reach; ring++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        var at = TileMap.Center(cx + dx, cy + dy);
                        if (Vec2.Distance(at, middle) > within || !Clearance.Fits(cx + dx, cy + dy, kind.Radius) || !fits(at)) continue;
                        if (taken.Any(t => Vec2.Distance(t.At, at) < t.Radius + kind.Radius + MonsterSizes.PackGap)) continue;
                        return at;
                    }
                }
            }
            return null;
        }

        // A roster with its big bodies placed first (Muster, a dungeon's room): each big one gets a spot where it fits or
        // is left out, and the rest are spread by spread(count, away) with away keeping them clear of the big ones. The
        // roster comes back in its own order with a spot each, less the big ones left out; the small ones spread found no
        // spot for come last, in waiting (a cramped room's Crowd may yet place them).
        (List<(MonsterDefinition Kind, Vec2 Spot)> Placed, List<MonsterDefinition> Waiting) PlaceRoster(List<MonsterDefinition> roster, Vec2 middle, float within, Func<Vec2, bool> fits,
            Func<int, Func<Vec2, bool>, List<Vec2>> spread)
        {
            var taken = new List<(Vec2 At, float Radius)>();
            var bigSpots = new Dictionary<int, Vec2>();
            for (int i = 0; i < roster.Count; i++)
            {
                if (!roster[i].Big) continue;
                var spot = BigSpot(roster[i], middle, within, fits, taken);
                if (spot == null) continue;
                bigSpots[i] = spot.Value;
                taken.Add((spot.Value, roster[i].Radius));
            }
            int small = roster.Count(k => !k.Big);
            var smallSpots = small == 0 ? new List<Vec2>()
                : spread(small, at => taken.All(t => Vec2.Distance(t.At, at) >= t.Radius + MonsterSizes.SlimRadius + MonsterSizes.PackGap));
            var placed = new List<(MonsterDefinition, Vec2)>();
            var waiting = new List<MonsterDefinition>();
            int next = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Big)
                {
                    if (bigSpots.TryGetValue(i, out var spot)) placed.Add((roster[i], spot));
                }
                else if (next < smallSpots.Count) placed.Add((roster[i], smallSpots[next++]));
                else waiting.Add(roster[i]);
            }
            return (placed, waiting);
        }

        // The bands that come where no room is to hold a big body (the director's surges and lurkers, a trap's waves, a
        // world of caves' clumps): its big ones stay away.
        static void LeaveOutBig(List<MonsterDefinition> roster) => roster.RemoveAll(k => k.Big);

        // For the playtests (Client/Playtest.cs "mob <id> spawn") and the tests: a monster of the kind where a body its
        // size fits nearest the spot (within reach tiles), held to a leash of that radius round it when it is big.
        // Null when no spot near fits.
        public Enemy SpawnFitting(MonsterDefinition kind, Vec2 near, float reach = 6, float leash = float.PositiveInfinity)
        {
            var spot = BigSpot(kind, near, reach, at => !Map.BlocksMonsters(TileMap.Cell(at.X), TileMap.Cell(at.Y))
                && !Collision.Overlaps(Map, at, Math.Min(kind.Radius, MonsterSizes.SlimRadius), monster: true), new List<(Vec2, float)>());
            if (spot == null) return null;
            var enemy = SpawnEnemy(kind, Collision.Resolve(Map, spot.Value, kind.Radius, monster: true));
            enemy.Home = enemy.Position;
            if (!float.IsPositiveInfinity(leash)) Bind(enemy, enemy.Position, leash - MonsterSizes.RoomLeash);
            return enemy;
        }
    }
}
