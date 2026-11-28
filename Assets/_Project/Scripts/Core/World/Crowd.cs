using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Keeps the people walking about a town from walking through one another (the owner, 2026-11-29: "when their ways
    // cross the NPCs walk over each other"). The townsfolk and the people of the story are only to look at: where the
    // clock puts them (TownPerson.At, PlaceSite.PersonAt) is where they are, and nothing about them is sent. So this
    // keeps no state either: from where the clock puts everyone now and which way they are walking, it nudges anyone
    // standing too close to someone else a little apart. Two who meet head-on each step aside (to the side they already
    // lean to, or each to their own right), one who stands barely moves and lets the walker go round, and no one is
    // nudged onto a wall or into water or more than MaxNudge from their own way. Worked out afresh each frame from
    // positions that move smoothly, the nudges move smoothly too.
    public static class Crowd
    {
        // How far apart two people's feet keep, in tiles (a figure is about 0.6 of a tile wide).
        public const float Gap = 0.7f;
        // The furthest anyone is nudged from where the clock puts them.
        public const float MaxNudge = 0.45f;
        // How far off two who walk towards each other begin to step aside.
        public const float Ahead = 2f;
        const int Rounds = 4;

        // Where everyone stands once nudged apart: at is where the clock puts each, heading the way each is walking
        // (zero for one who stands). The map keeps them off walls and water; null skips that.
        public static Vec2[] Spread(IReadOnlyList<Vec2> at, IReadOnlyList<Vec2> heading, TileMap map = null)
        {
            int n = at.Count;
            var result = new Vec2[n];
            for (int i = 0; i < n; i++) result[i] = at[i];
            for (int round = 0; round < Rounds; round++)
            {
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        var d = result[j] - result[i];
                        float distance = d.Length;
                        var hi = Direction(heading[i]);
                        var hj = Direction(heading[j]);
                        Vec2 pushI, pushJ;
                        if (distance < Ahead && hi.LengthSquared > 0 && hj.LengthSquared > 0 && Vec2.Dot(hi, hj) < -0.5f)
                        {
                            // Head-on: both step aside across the way, a little from Ahead tiles off and fully as they
                            // pass, each to the side they already lean to, or j to i's left (each keeps to their right).
                            var right = Right(hi);
                            float lean = Vec2.Dot(d, right);
                            float near = 1 - Math.Min(1, Math.Abs(Vec2.Dot(d, hi)) / Ahead);
                            float lack = Gap * near * near * (3 - 2 * near) - Math.Abs(lean);
                            if (lack <= 0) continue;
                            float side = Math.Abs(lean) > 0.05f ? Math.Sign(lean) : -1;
                            pushI = right * (-side * lack / 2);
                            pushJ = right * (side * lack / 2);
                        }
                        else
                        {
                            if (distance >= Gap) continue;
                            float overlap = Gap - distance;
                            Vec2 away = distance > 0.01f ? d / distance
                                : hi.LengthSquared > 0 ? Right(hi) : hj.LengthSquared > 0 ? -Right(hj) : new Vec2(1, 0);
                            // One who stands gives way only a little: the walker goes round them.
                            float wi = hi.LengthSquared > 0 ? 1 : 0.15f, wj = hj.LengthSquared > 0 ? 1 : 0.15f;
                            pushI = away * (-overlap * wi / (wi + wj));
                            pushJ = away * (overlap * wj / (wi + wj));
                        }
                        moved |= Nudge(result, at, i, pushI, map);
                        moved |= Nudge(result, at, j, pushJ, map);
                    }
                }
                if (!moved) break;
            }
            return result;
        }

        // Moves one along, kept within MaxNudge of their own way and off walls and water. False when they cannot go.
        static bool Nudge(Vec2[] result, IReadOnlyList<Vec2> at, int i, Vec2 push, TileMap map)
        {
            if (push.LengthSquared < 1e-8f) return false;
            var to = at[i] + (result[i] + push - at[i]).ClampLength(MaxNudge);
            if (map != null && !Open(map, to)) return false;
            result[i] = to;
            return true;
        }

        static bool Open(TileMap map, Vec2 p)
        {
            int x = TileMap.Cell(p.X), y = TileMap.Cell(p.Y);
            return map.InBounds(x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Water;
        }

        static Vec2 Direction(Vec2 v) => v.LengthSquared > 1e-8f ? v / v.Length : Vec2.Zero;

        // The right hand of one walking that way (y up is north).
        static Vec2 Right(Vec2 h) => new Vec2(h.Y, -h.X);
    }
}
