using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The people of the grown towns (2026-11-27, Docs/Towns.md, version 10): villagers, farmers and children walking the
    // streets, guards at the gates, merchants by the stalls, nobles about the court (Data/Story/townsfolk.csv). Only to
    // look at: no one talks or blocks, nothing is sent, every client works them out the same from the map and the seed
    // (their routes) and the clock (where on them they are).
    public sealed class TownsfolkKind
    {
        public TownsfolkKind(string id, string name, string place, string look, string color, int count, string walk)
        {
            Id = id;
            Name = name;
            Place = place;
            Look = look;
            Color = color;
            Count = count;
            Walk = walk;
        }

        public string Id { get; }
        public string Name { get; }
        public string Place { get; }
        // A look of PeopleArt (villager, guard, girl, queen …) and its colour.
        public string Look { get; }
        public string Color { get; }
        public int Count { get; }
        // wander: from street to street; run: the same, quicker (children); gate: stands at a way in; stall: stands by a
        // market stall; court: walks the royal court.
        public string Walk { get; }
    }

    public static class TownsfolkSheet
    {
        public const string File = "townsfolk.csv";
        public static readonly string[] Walks = { "wander", "run", "gate", "stall", "court" };

        public static IReadOnlyList<TownsfolkKind> Parse(string csv, List<string> errors = null)
        {
            var kinds = new List<TownsfolkKind>();
            if (string.IsNullOrWhiteSpace(csv)) return kinds;
            var lines = csv.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
            var head = lines[0].Split(',');
            int Col(string name) => Array.IndexOf(head, name);
            foreach (var (line, n) in lines.Skip(1).Select((l, i) => (l, i + 2)))
            {
                var c = line.Split(',');
                string At(string name) => Col(name) >= 0 && Col(name) < c.Length ? c[Col(name)].Trim() : "";
                if (!int.TryParse(At("count"), out int count) || count < 0 || count > 30 || !Walks.Contains(At("walk")))
                {
                    errors?.Add($"{File} {n}줄: count는 0~30, walk는 {string.Join("·", Walks)}");
                    continue;
                }
                kinds.Add(new TownsfolkKind(At("id"), At("name"), At("place"), At("look"), At("color"), count, At("walk")));
            }
            return kinds;
        }
    }

    // One of them: its route (tile centres, walked in a loop) and its pace; or a spot it stands at.
    public sealed class TownPerson
    {
        public const float WalkSpeed = 1.4f, RunSpeed = 2.6f;
        // How long they stand at each end of a stretch.
        public const float Dwell = 3;

        internal TownPerson(TownsfolkKind kind, int index, IReadOnlyList<Vec2> route)
        {
            Kind = kind;
            Index = index;
            Route = route;
        }

        public TownsfolkKind Kind { get; }
        public int Index { get; }
        public IReadOnlyList<Vec2> Route { get; }

        // Where they are at the clock: standing ones at their spot, the rest along their route, a pause at each
        // corner, each at their own point of it. At night they keep to their first spot.
        public Vec2 At(long clock, int dayMinutes = Calendar.BaseDayMinutes)
        {
            if (Route.Count < 2 || Calendar.IsNight(clock)) return Route.Count > 0 ? Route[0] : Vec2.Zero;
            float speed = Kind.Walk == "run" ? RunSpeed : WalkSpeed;
            int n = Route.Count;
            double period = 0;
            for (int i = 0; i < n; i++) period += Vec2.Distance(Route[i], Route[(i + 1) % n]) / speed + (i % 6 == 0 ? Dwell : 0);
            double played = clock * (double)Calendar.DayMinutesOf(dayMinutes) / Calendar.BaseDayMinutes;
            double t = (played / World.TicksPerSecond + Index * 17.3) % period;
            for (int i = 0; i < n; i++)
            {
                if (i % 6 == 0)
                {
                    if (t < Dwell) return Route[i];
                    t -= Dwell;
                }
                double leg = Vec2.Distance(Route[i], Route[(i + 1) % n]) / speed;
                if (t < leg) return Vec2.Lerp(Route[i], Route[(i + 1) % n], (float)(t / leg));
                t -= leg;
            }
            return Route[0];
        }
    }

    public static class Townsfolk
    {
        // The people of a grown town (a place whose radius is not the sheet's: version 10), by the kinds for its place.
        public static List<TownPerson> Of(PlaceSite site, TileMap map, IReadOnlyList<TownsfolkKind> kinds, ulong seed)
        {
            var people = new List<TownPerson>();
            if (site == null || kinds == null || site.Radius == site.Definition.Radius) return people;
            var random = new GameRandom(seed ^ (ulong)(site.X * 73856093 ^ site.Y * 19349663), stream: 61);
            int r = site.Radius;
            bool Inside(int x, int y) => (x - site.X) * (x - site.X) + (y - site.Y) * (y - site.Y) <= r * r && map.InBounds(x, y);
            var streets = new List<(int X, int Y)>();
            var stalls = new List<(int X, int Y)>();
            var gates = new List<(int X, int Y)>();
            for (int y = site.Y - r; y <= site.Y + r; y++)
                for (int x = site.X - r; x <= site.X + r; x++)
                {
                    if (!Inside(x, y)) continue;
                    var t = map[x, y];
                    if (t == TileType.Road || t == TileType.RedCarpet) streets.Add((x, y));
                    if (t == TileType.MarketStall && Walkable(map, x, y - 1)) stalls.Add((x, y - 1));
                    int d2 = (x - site.X) * (x - site.X) + (y - site.Y) * (y - site.Y);
                    if (t == TileType.Road && d2 > (r - 4) * (r - 4)) gates.Add((x, y));
                }
            var court = streets.Where(s => Distance(s, site) < 16).ToList();
            // Spots someone already stands at: the next guard or stall keeper takes another while one is free.
            var taken = new HashSet<(int X, int Y)>();
            var town = streets.Where(s => Distance(s, site) >= 3).ToList();
            foreach (var kind in kinds.Where(k => k.Place == site.Definition.Id))
            {
                for (int i = 0; i < kind.Count; i++)
                {
                    int index = people.Count;
                    List<Vec2> route;
                    switch (kind.Walk)
                    {
                        case "gate":
                            route = Stand(gates.Count > 0 ? gates : town, random, taken);
                            break;
                        case "stall":
                            route = Stand(stalls.Count > 0 ? stalls : town, random, taken);
                            break;
                        case "court":
                            route = Walk(map, court.Count > 0 ? court : town, random, site);
                            break;
                        default:
                            route = Walk(map, town, random, site);
                            break;
                    }
                    if (route.Count > 0) people.Add(new TownPerson(kind, index, route));
                }
            }
            return people;
        }

        static float Distance((int X, int Y) s, PlaceSite site) => Vec2.Distance(TileMap.Center(s.X, s.Y), site.Center);

        static bool Walkable(TileMap map, int x, int y) => map.InBounds(x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Water;

        static List<Vec2> Stand(List<(int X, int Y)> spots, GameRandom random, HashSet<(int X, int Y)> taken)
        {
            if (spots.Count == 0) return new List<Vec2>();
            var free = spots.Where(s => !taken.Contains(s)).ToList();
            var (x, y) = free.Count > 0 ? free[random.Next(free.Count)] : spots[random.Next(spots.Count)];
            taken.Add((x, y));
            return new List<Vec2> { TileMap.Center(x, y) };
        }

        // Three street tiles at random, joined by the way over walkable tiles (tile by tile), in a loop.
        static List<Vec2> Walk(TileMap map, List<(int X, int Y)> streets, GameRandom random, PlaceSite site)
        {
            var route = new List<Vec2>();
            if (streets.Count < 2) return route;
            var stops = Enumerable.Range(0, 3).Select(_ => streets[random.Next(streets.Count)]).ToList();
            for (int i = 0; i < stops.Count; i++)
            {
                var path = Path(map, stops[i], stops[(i + 1) % stops.Count], site);
                if (path == null) return new List<Vec2> { TileMap.Center(stops[0].X, stops[0].Y) };
                // Every other tile is enough to walk by.
                for (int k = 0; k < path.Count - 1; k += 2) route.Add(TileMap.Center(path[k].X, path[k].Y));
            }
            return route;
        }

        // The shortest way over walkable tiles within the town, or null.
        static List<(int X, int Y)> Path(TileMap map, (int X, int Y) from, (int X, int Y) to, PlaceSite site)
        {
            int r = site.Radius + 1;
            var came = new Dictionary<(int, int), (int, int)> { [from] = from };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                if (at == to) break;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var next = (at.X + dx, at.Y + dy);
                    if (came.ContainsKey(next) || !Walkable(map, next.Item1, next.Item2)) continue;
                    if (Math.Abs(next.Item1 - site.X) > r || Math.Abs(next.Item2 - site.Y) > r) continue;
                    came[next] = at;
                    queue.Enqueue(next);
                }
            }
            if (!came.ContainsKey(to)) return null;
            var path = new List<(int X, int Y)> { to };
            while (path[path.Count - 1] != from) path.Add(came[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }
}
