using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- The arenas, each shaped and dressed for its boss (2026-11-17, the person: "the arenas all look the same; sort
    // them by how the boss appears and dress each to its own idea", Docs/Bosses.md 아레나 컨셉). Done last of all on its
    // own random stream, so nothing else in a seed's world moves. The rules that keep a fight and the ways in as they
    // were:
    //  - the round room of ArenaRadius stays open: nothing solid goes nearer the middle than RimFrom, where a boss's
    //    own middle never comes (World.MoveBoss keeps it ArenaRadius less its body from the middle);
    //  - the room only ever grows, and by less than a tile (Reach), which is inside what every other thing keeps clear
    //    of an arena;
    //  - a solid thing goes only where it shuts nothing off (KeepsWay) and not beside a way in (NearEntry);
    //  - the floor's patterns are tiles walked on like the road (Structures.IsArenaFloor), laid only on bare floor.
    // The room is turned so its head (the throne, the altar's end) is on the side away from the way in. ----
    public static partial class WorldGenerator
    {
        const ulong ArenaStream = 21;
        // Squared distances from the arena's middle: solid things from here out; the room grows no further than Reach.
        const int RimFrom = 77;
        const int Reach = 118;

        static void DressArenas(GeneratedWorld world, ulong seed, ArenaData arenas)
        {
            var kinds = new List<ArenaKind>();
            for (int i = 0; i < world.Arenas.Count; i++)
            {
                var kind = ArenaKind.Plain;
                if (arenas != null)
                {
                    if (i < world.CaveArenas) kind = arenas.CaveKind(i);
                    else
                    {
                        var at = world.Arenas[i];
                        string place = world.PlaceArenas.FirstOrDefault(p => p.Value.X == at.X && p.Value.Y == at.Y).Key;
                        kind = arenas.PlaceKind(place);
                    }
                }
                kinds.Add(kind);
            }
            world.ArenaKinds = kinds;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i] == ArenaKind.Plain) continue;
                new ArenaDresser(world.Map, TileMap.Cell(world.Arenas[i].X), TileMap.Cell(world.Arenas[i].Y), new GameRandom(seed, ArenaStream + (ulong)i)).Dress(kinds[i]);
            }
        }

        // Dresses one arena for tools and tests: a round room cleared in an empty map and dressed as the kind.
        public static TileMap ArenaSample(ArenaKind kind, ulong seed, bool entry = true)
        {
            int size = 2 * ArenaRadius + 9;
            var map = new TileMap(size, size);
            int c = size / 2;
            ClearDisc(map, c, c, ArenaRadius);
            // A way in from below, three wide, as the tunnels and roads are.
            if (entry)
                for (int y = 1; y < c; y++)
                    for (int x = c - 1; x <= c + 1; x++)
                        map[x, y] = TileType.Floor;
            new ArenaDresser(map, c, c, new GameRandom(seed, ArenaStream)).Dress(kind);
            return map;
        }

        sealed class ArenaDresser
        {
            readonly TileMap _map;
            readonly int _cx, _cy;
            readonly GameRandom _random;
            readonly List<(int X, int Y)> _entries = new List<(int, int)>();
            // Quarter turns from the room's own frame (its head up) to the map.
            int _turn;

            public ArenaDresser(TileMap map, int cx, int cy, GameRandom random)
            {
                _map = map;
                _cx = cx;
                _cy = cy;
                _random = random;
            }

            static readonly (int X, int Y)[] RingWays = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

            // Whether something solid at x, y would leave the open tiles round it joined as they were: the open tiles
            // among its eight neighbours that touch it by a side are all in one run round it.
            bool KeepsWay(int x, int y)
            {
                int runs = 0;
                for (int i = 0; i < 8; i++)
                {
                    var (ax, ay) = RingWays[i];
                    if (ax != 0 && ay != 0) continue;
                    if (_map.IsSolid(x + ax, y + ay)) continue;
                    // The start of a run: going back round the ring, a closed tile before the next open side.
                    var (bx, by) = RingWays[(i + 7) % 8];
                    var (px, py) = RingWays[(i + 6) % 8];
                    bool joined = !_map.IsSolid(x + bx, y + by) && !_map.IsSolid(x + px, y + py);
                    if (!joined) runs++;
                }
                return runs <= 1;
            }

            // Whether the arena is a room in the rock (ways in through its wall) or stands in open cave, as most do on
            // the game's map: there nothing is a wall to keep clear of, and only a road counts as a way in.
            bool _walled;

            // Beside a way in: in a walled room, near any open tile outside it (so the tunnel's mouth keeps its width);
            // in open cave, near the road that comes to it.
            bool NearEntry(int x, int y)
            {
                if (_walled) return _entries.Any(e => (e.X - x) * (e.X - x) + (e.Y - y) * (e.Y - y) <= 6);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (dx * dx + dy * dy <= 5 && _map[x + dx, y + dy] == TileType.Road) return true;
                return false;
            }

            // From the room's frame to the map.
            (int X, int Y) At(int dx, int dy) => _turn switch
            {
                1 => (_cx - dy, _cy + dx),
                2 => (_cx - dx, _cy - dy),
                3 => (_cx + dy, _cy - dx),
                _ => (_cx + dx, _cy + dy),
            };

            static int Far(int dx, int dy) => dx * dx + dy * dy;

            // A step of so many tenths of a tile along one of the seventy-two ways.
            static (int X, int Y) Polar(int way, int tenths)
            {
                var w = Ways72[((way % 72) + 72) % 72];
                return (Nearest(w.X * tenths), Nearest(w.Y * tenths));
            }

            static int Nearest(int tenThousandths) => tenThousandths >= 0 ? (tenThousandths + 5000) / 10000 : -((-tenThousandths + 5000) / 10000);

            // Rock inside the room's reach becomes floor.
            void Open(int dx, int dy)
            {
                var (x, y) = At(dx, dy);
                if (Far(dx, dy) <= Reach && IsInside(_map, x, y) && _map[x, y] == TileType.Wall) _map[x, y] = TileType.Floor;
            }

            // A pattern on the floor: only on bare floor in the round room.
            void Lay(int dx, int dy, TileType type)
            {
                var (x, y) = At(dx, dy);
                if (Far(dx, dy) <= ArenaRadius * ArenaRadius && IsInside(_map, x, y) && _map[x, y] == TileType.Floor) _map[x, y] = type;
            }

            // Something solid in the rim: in place of rock, or on floor where it shuts nothing off.
            bool Put(int dx, int dy, TileType type, bool byEntry = false)
            {
                var (x, y) = At(dx, dy);
                int far = Far(dx, dy);
                if (far < RimFrom || far > Reach || !IsInside(_map, x, y)) return false;
                if (!byEntry && NearEntry(x, y)) return false;
                if (_map[x, y] == TileType.Wall)
                {
                    // Only rock that shows from the room: with floor beside it.
                    if (!RingWays.Any(w => !_map.IsSolid(x + w.X, y + w.Y))) return false;
                }
                else if (_map[x, y] != TileType.Floor || _walled && _entries.Contains((x, y)) || !KeepsWay(x, y)) return false;
                _map[x, y] = type;
                return true;
            }

            // Water or lava in the rim. Lava is walked through (and burns), so it never lies by a way in.
            void Pool(int dx, int dy, TileType type)
            {
                var (x, y) = At(dx, dy);
                int far = Far(dx, dy);
                if (far < RimFrom || far > Reach || !IsInside(_map, x, y) || NearEntry(x, y)) return;
                if (_map[x, y] == TileType.Wall) _map[x, y] = type;
                else if (_map[x, y] == TileType.Floor && (type == TileType.Lava || KeepsWay(x, y))) _map[x, y] = type;
            }

            // Every tile of the room's frame within the reach.
            static IEnumerable<(int X, int Y)> Tiles()
            {
                for (int dy = -ArenaRadius - 1; dy <= ArenaRadius + 1; dy++)
                    for (int dx = -ArenaRadius - 1; dx <= ArenaRadius + 1; dx++)
                        if (Far(dx, dy) <= Reach) yield return (dx, dy);
            }

            // A ring one tile thick, so many tenths of a tile out.
            void Ring(int tenths, TileType type, Func<int, int, bool> where = null)
            {
                int lo = (tenths - 5) * (tenths - 5), hi = (tenths + 5) * (tenths + 5);
                foreach (var (dx, dy) in Tiles())
                {
                    int far = Far(dx, dy) * 100;
                    if (far > lo && far <= hi && (where == null || where(dx, dy))) Lay(dx, dy, type);
                }
            }

            // So many things evenly round the rim from a first way (of 72), each of a type by its place in the round.
            void Round(int count, int first, int tenths, Func<int, TileType> type)
            {
                for (int i = 0; i < count; i++)
                {
                    // Where the spot will not do (it would shut a corner off), a little further out or in, or a step round.
                    var kind = type(i);
                    foreach (var (turn, reach) in new[] { (0, 0), (0, 6), (0, -5), (1, 0), (-1, 0), (1, 6), (-1, 6) })
                    {
                        var (dx, dy) = Polar(first + i * 72 / count + turn, tenths + reach);
                        if (Put(dx, dy, kind)) break;
                    }
                }
            }

            // So many of a thing at random spots in the rim.
            void Scatter(int count, TileType type)
            {
                for (int tries = 0; tries < count * 12 && count > 0; tries++)
                {
                    var (dx, dy) = Polar(_random.Next(72), 90 + _random.Next(14));
                    if (Put(dx, dy, type)) count--;
                }
            }

            // A line of floor pattern from one spot to another.
            void Line(int x0, int y0, int x1, int y1, TileType type)
            {
                foreach (var (x, y) in WorldGenerator.Line(x0, y0, x1, y1)) Lay(x, y, type);
            }

            public void Dress(ArenaKind kind)
            {
                // The ways in: open tiles just outside the round room, as the world left them.
                int sumX = 0, sumY = 0, round = 0;
                for (int dy = -ArenaRadius - 3; dy <= ArenaRadius + 3; dy++)
                {
                    for (int dx = -ArenaRadius - 3; dx <= ArenaRadius + 3; dx++)
                    {
                        int far = Far(dx, dy);
                        if (far <= ArenaRadius * ArenaRadius || far > (ArenaRadius + 3) * (ArenaRadius + 3)) continue;
                        round++;
                        if (_map.IsSolid(_cx + dx, _cy + dy)) continue;
                        _entries.Add((_cx + dx, _cy + dy));
                        // A road's tiles count for more: the head is away from the road before all else.
                        int weight = _map[_cx + dx, _cy + dy] == TileType.Road ? 8 : 1;
                        sumX += dx * weight;
                        sumY += dy * weight;
                    }
                }
                // A room in the rock: less than a third of what lies round it is open.
                _walled = _entries.Count * 3 < round;
                // The head is on the side away from them.
                if (Math.Abs(sumY) >= Math.Abs(sumX)) _turn = sumY > 0 ? 2 : 0;
                else _turn = sumX > 0 ? 1 : 3;

                switch (kind)
                {
                    case ArenaKind.Ruin:
                        // Eight-sided; a ring of pillars, some broken; statues either side of the head; an inlaid
                        // ring and a cross in the floor; rubble.
                        foreach (var (dx, dy) in Tiles())
                            if (Math.Abs(dx) + Math.Abs(dy) <= 14 && Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 10) Open(dx, dy);
                        Ring(60, TileType.ArenaInlay);
                        for (int i = 2; i <= 4; i++)
                        {
                            Lay(i, 0, TileType.ArenaInlay);
                            Lay(-i, 0, TileType.ArenaInlay);
                            Lay(0, i, TileType.ArenaInlay);
                            Lay(0, -i, TileType.ArenaInlay);
                        }
                        Put(-2, 9, TileType.Statue);
                        Put(2, 9, TileType.Statue);
                        {
                            int broken = _random.Next(8);
                            Round(8, 4, 94, i => i == broken || i == (broken + 3) % 8 || _random.Chance(0.25f) ? TileType.BrokenPillar : TileType.Pillar);
                        }
                        Scatter(5, TileType.Rubble);
                        break;

                    case ArenaKind.Nest:
                        // Webs in patches and thick in the rim; crystal spires round it, cocoons between.
                        foreach (var (dx, dy) in Tiles())
                            if (Far(dx, dy) >= 64 && _random.Chance(0.4f)) Lay(dx, dy, TileType.Cobweb);
                        for (int patch = 0; patch < 6; patch++)
                        {
                            var (px, py) = Polar(_random.Next(72), 40 + _random.Next(35));
                            for (int k = 0; k < 4; k++) Lay(px + _random.Next(-1, 2), py + _random.Next(-1, 2), TileType.Cobweb);
                        }
                        Round(6, 6, 95, _ => TileType.Spire);
                        Scatter(5, TileType.Cocoon);
                        break;

                    case ArenaKind.Caldera:
                    {
                        // Five pools of lava set into the rim, a glowing crack running in from each, rubble.
                        int first = _random.Next(72);
                        for (int k = 0; k < 5; k++)
                        {
                            int way = first + k * 72 / 5 + _random.Next(-2, 3);
                            var (px, py) = Polar(way, 103);
                            foreach (var (dx, dy) in Tiles())
                                if (Far(dx - px, dy - py) <= 4) Pool(dx, dy, TileType.Lava);
                            var (fx, fy) = Polar(way, 82);
                            var (tx, ty) = Polar(way + _random.Next(-4, 5), 40 + _random.Next(15));
                            Line(fx, fy, tx, ty, TileType.Fissure);
                        }
                        Ring(25, TileType.Fissure, (dx, dy) => (dx + dy) % 2 != 0);
                        Scatter(4, TileType.Rubble);
                        break;
                    }

                    case ArenaKind.Altar:
                        // A great circle of runes with a smaller inside it and spokes between; rune stones and
                        // braziers by turns round the rim.
                        Ring(70, TileType.RuneFloor);
                        Ring(35, TileType.RuneFloor);
                        foreach (int way in new[] { 9, 27, 45, 63 })
                            foreach (int tenths in new[] { 50, 60 })
                            {
                                var (dx, dy) = Polar(way, tenths);
                                Lay(dx, dy, TileType.RuneFloor);
                            }
                        Round(12, 3, 94, i => i % 2 == 0 ? TileType.RuneStone : TileType.Brazier);
                        break;

                    case ArenaKind.Flooded:
                        // Water along the walls in four arcs, a pillar standing in each, planks along the shore; an
                        // inlaid ring in the middle.
                        foreach (var (dx, dy) in Tiles())
                        {
                            // The arcs lie on the diagonals: where both dx and dy are well off the axes.
                            if (Far(dx, dy) >= 85 && Math.Abs(dx) >= 4 && Math.Abs(dy) >= 4) Pool(dx, dy, TileType.Water);
                        }
                        foreach (var (dx, dy) in Tiles())
                        {
                            int far = Far(dx, dy);
                            if (far < 61 || far >= 85 || Math.Abs(dx) < 4 || Math.Abs(dy) < 4) continue;
                            var (x, y) = At(dx, dy);
                            if (RingWays.Any(w => _map[x + w.X, y + w.Y] == TileType.Water)) Lay(dx, dy, TileType.Dock);
                        }
                        Round(4, 0, 95, _ => TileType.Pillar);
                        Ring(50, TileType.ArenaInlay);
                        Scatter(3, TileType.BrokenPillar);
                        break;

                    case ArenaKind.Pit:
                        // Rings going down to the middle, and the bones of what went in thrown out round the edge.
                        Ring(85, TileType.ArenaInlay);
                        Ring(60, TileType.ArenaInlay);
                        Ring(35, TileType.ArenaInlay);
                        foreach (var (dx, dy) in Tiles())
                            if (Far(dx, dy) <= 2) Lay(dx, dy, TileType.ArenaInlay);
                        Scatter(9, TileType.Bones);
                        Scatter(3, TileType.Rubble);
                        break;

                    case ArenaKind.Warcamp:
                        // Banners at the corners, fires on the axes, racks of weapons and totems by the head, bones;
                        // a broken ring trodden into the ground.
                        Ring(75, TileType.ArenaInlay, (dx, dy) => (dx + dy) % 2 == 0);
                        Round(4, 9, 95, _ => TileType.DemonBanner);
                        Round(4, 0, 95, _ => TileType.Brazier);
                        Put(-3, 9, TileType.Totem);
                        Put(3, 9, TileType.Totem);
                        Put(-5, 8, TileType.WeaponRack);
                        Put(5, 8, TileType.WeaponRack);
                        Scatter(2, TileType.Spire);
                        Scatter(4, TileType.Bones);
                        break;

                    case ArenaKind.Chapel:
                        // Water in the four corners; an aisle of runes up the middle to a throne at the head;
                        // lanterns along the sides and pillars by the walls.
                        foreach (var (dx, dy) in Tiles())
                            if (Far(dx, dy) >= 88 && Math.Abs(dx) >= 5 && Math.Abs(dy) >= 5) Pool(dx, dy, TileType.Water);
                        for (int dy = -8; dy <= 9; dy++) Lay(0, dy, dy % 2 == 0 ? TileType.RuneFloor : TileType.ArenaInlay);
                        Ring(45, TileType.ArenaInlay, (dx, dy) => dx != 0);
                        Put(0, 10, TileType.Throne);
                        foreach (int side in new[] { -1, 1 })
                        {
                            Put(4 * side, 8, TileType.Lantern);
                            Put(4 * side, -8, TileType.Lantern);
                            Put(9 * side, 2, TileType.Pillar);
                            Put(9 * side, -2, TileType.Pillar);
                        }
                        Scatter(2, TileType.RuneStone);
                        break;

                    case ArenaKind.Mine:
                    {
                        // Two tracks of rail running through the room, a cart at the end of each, heaps of ore and lanterns,
                        // crates; and the floor cracked from below where it comes up.
                        // (The rail's picture runs up and down the map, so the tracks do too, whichever way the room
                        // is turned.)
                        int turned = _turn;
                        _turn = 0;
                        Put(-4, 9, TileType.MineCart);
                        Put(5, -8, TileType.MineCart);
                        for (int d = -ArenaRadius; d <= ArenaRadius; d++)
                        {
                            Lay(-4, d, TileType.Rail);
                            if (d <= 3) Lay(5, d, TileType.Rail);
                        }
                        _turn = turned;
                        Round(4, 9, 95, _ => TileType.Lantern);
                        for (int crack = 0; crack < 4; crack++)
                        {
                            var (tx, ty) = Polar(_random.Next(18) + crack * 18, 30 + _random.Next(20));
                            Line(0, 0, tx, ty, TileType.Fissure);
                        }
                        Scatter(5, TileType.OrePile);
                        Scatter(2, TileType.Crates);
                        Scatter(2, TileType.Woodpile);
                        break;
                    }

                    case ArenaKind.Dojo:
                    {
                        // A round stone court (two inlaid rings), cherry trees round it and their petals on the floor,
                        // stone lanterns at the head, and a gate's posts either side of each way in.
                        Ring(80, TileType.ArenaInlay);
                        Ring(30, TileType.ArenaInlay);
                        foreach (var (dx, dy) in Tiles())
                        {
                            int far = Far(dx, dy);
                            if (far > 9 && _random.Chance(far >= 49 ? 0.3f : 0.1f)) Lay(dx, dy, TileType.Petals);
                        }
                        Gates(TileType.Torii);
                        Round(6, 6, 96, _ => TileType.CherryTree);
                        Put(-2, 9, TileType.Lantern);
                        Put(2, 9, TileType.Lantern);
                        break;
                    }

                    case ArenaKind.Hall:
                        // Squared; a red runner up the middle to the throne; pillars in the corners, armour along the
                        // sides, banners and racks by the head, braziers.
                        foreach (var (dx, dy) in Tiles())
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 9) Open(dx, dy);
                        Put(0, 10, TileType.Throne);
                        for (int dy = -9; dy <= 9; dy++)
                        {
                            Lay(0, dy, TileType.ArenaCarpet);
                            Lay(-1, dy, dy % 3 == 0 ? TileType.ArenaInlay : TileType.Floor);
                            Lay(1, dy, dy % 3 == 0 ? TileType.ArenaInlay : TileType.Floor);
                        }
                        foreach (int side in new[] { -1, 1 })
                        {
                            Put(7 * side, 7, TileType.Pillar);
                            Put(7 * side, -7, TileType.Pillar);
                            Put(9 * side, 4, TileType.ArmorStand);
                            Put(10 * side, 0, TileType.ArmorStand);
                            Put(9 * side, -4, TileType.ArmorStand);
                            Put(3 * side, 9, TileType.RoyalBanner);
                            Put(5 * side, 8, TileType.WeaponRack);
                            Put(8 * side, 5, TileType.Brazier);
                            Put(8 * side, -5, TileType.Brazier);
                        }
                        break;

                    case ArenaKind.Web:
                    {
                        // One great web across the floor: spokes out from the middle and rings across them, thin in
                        // the middle where the fight is and thick in the rim; cocoons and bones round it, a pair of
                        // the crypt's broken pillars.
                        for (int way = 0; way < 72; way += 9)
                            for (int tenths = 50; tenths <= 100; tenths += 10)
                            {
                                var (dx, dy) = Polar(way + _turn, tenths);
                                if (_random.Chance(0.8f)) Lay(dx, dy, TileType.Cobweb);
                            }
                        Ring(60, TileType.Cobweb, (dx, dy) => _random.Chance(0.45f));
                        Ring(85, TileType.Cobweb, (dx, dy) => _random.Chance(0.7f));
                        foreach (var (dx, dy) in Tiles())
                            if (Far(dx, dy) >= 85 && _random.Chance(0.5f)) Lay(dx, dy, TileType.Cobweb);
                        Scatter(8, TileType.Cocoon);
                        Scatter(5, TileType.Bones);
                        Scatter(2, TileType.BrokenPillar);
                        break;
                    }
                }
            }

            // A post either side of each way in (in open cave, of the road): the ways in are gathered by the way they lie from the middle, and a
            // post goes in the rock (or on the floor, where it shuts nothing) two tiles to each side of each.
            void Gates(TileType post)
            {
                var done = new List<(int X, int Y)>();
                // In open cave only the road is a way in to stand a gate at.
                var ways = _walled ? _entries : _entries.Where(e => _map[e.X, e.Y] == TileType.Road).ToList();
                foreach (var entry in ways.OrderBy(e => Far(e.X - _cx, e.Y - _cy)).ThenBy(e => e.Y).ThenBy(e => e.X))
                {
                    int ex = entry.X - _cx, ey = entry.Y - _cy;
                    if (Far(ex, ey) > 121 || done.Any(d => Far(d.X - ex, d.Y - ey) <= 36)) continue;
                    done.Add((ex, ey));
                }
                foreach (var (ex, ey) in done)
                {
                    // The middle of this way in: the mean of the entry tiles near it.
                    var near = ways.Where(e => Far(e.X - _cx - ex, e.Y - _cy - ey) <= 9).ToList();
                    int mx = Nearest(near.Sum(e => e.X - _cx) * 10000 / near.Count), my = Nearest(near.Sum(e => e.Y - _cy) * 10000 / near.Count);
                    // Drawn in to the room's edge along its way from the middle.
                    int length = 1;
                    while ((length + 1) * (length + 1) <= mx * mx + my * my) length++;
                    mx = Nearest(mx * ArenaRadius * 10000 / length);
                    my = Nearest(my * ArenaRadius * 10000 / length);
                    // Across it: a quarter turn from the way out.
                    bool upDown = Math.Abs(my) >= Math.Abs(mx);
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (int off = 2; off <= 3; off++)
                        {
                            int px = mx + (upDown ? side * off : 0), py = my + (upDown ? 0 : side * off);
                            // In the map's frame already: turn back into the room's.
                            var (lx, ly) = _turn switch { 1 => (py, -px), 2 => (-px, -py), 3 => (-py, px), _ => (px, py) };
                            if (Put(lx, ly, post, byEntry: true)) break;
                        }
                    }
                }
            }
        }
    }
}
