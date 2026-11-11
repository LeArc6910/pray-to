using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class GeneratedWorld
    {
        public GeneratedWorld(TileMap map, int spawnX, int spawnY, int arenaX, int arenaY, int arenaRadius, int deepArenaX, int deepArenaY,
            BiomeMap biomes = null, (int X, int Y)? lavaArena = null, (int X, int Y)? abyssArena = null, (int X, int Y)? lakeArena = null,
            (int X, int Y)? sandArena = null)
        {
            Biomes = biomes ?? BiomeMap.Plain(map.Width, map.Height);
            Map = map;
            SpawnX = spawnX;
            SpawnY = spawnY;
            ArenaX = arenaX;
            ArenaY = arenaY;
            ArenaRadius = arenaRadius;
            var arenas = new List<Vec2> { TileMap.Center(arenaX, arenaY), TileMap.Center(deepArenaX, deepArenaY) };
            if (lavaArena is (int lx, int ly)) arenas.Add(TileMap.Center(lx, ly));
            if (abyssArena is (int ax, int ay)) arenas.Add(TileMap.Center(ax, ay));
            if (lakeArena is (int kx, int ky)) arenas.Add(TileMap.Center(kx, ky));
            if (sandArena is (int sx, int sy)) arenas.Add(TileMap.Center(sx, sy));
            _arenas = arenas;
            CaveArenas = arenas.Count;
        }

        readonly List<Vec2> _arenas;
        readonly Dictionary<string, Vec2> _placeArenas = new Dictionary<string, Vec2>();

        // A boss arena carved beside a place of the story (2026-11-10, the demon nest's): after the caves' own.
        internal void AddPlaceArena(string place, Vec2 center)
        {
            _arenas.Add(center);
            _placeArenas[place] = center;
        }

        public TileMap Map { get; }
        public int SpawnX { get; }
        public int SpawnY { get; }
        public Vec2 Spawn => TileMap.Center(SpawnX, SpawnY);
        // The open round room where the boss waits, joined to the spawn by a tunnel.
        public int ArenaX { get; }
        public int ArenaY { get; }
        public int ArenaRadius { get; }
        public Vec2 Arena => TileMap.Center(ArenaX, ArenaY);
        // Every boss arena in the order the bosses go in (bosses.csv): the first one's, then the deep one far out,
        // reached by a tunnel from the first, then the lava one in the opposite corner, reached from the spawn. All
        // have ArenaRadius. After the caves' own come those beside places of the story (PlaceArenas).
        public IReadOnlyList<Vec2> Arenas => _arenas;
        // How many of Arenas are the caves' own, for the bosses with no place, in sheet order.
        public int CaveArenas { get; }
        // The arena beside each place of the story that has one, by place id (bosses.csv place).
        public IReadOnlyDictionary<string, Vec2> PlaceArenas => _placeArenas;
        // Which kind of cave each tile is in (Docs/Biomes.md).
        public BiomeMap Biomes { get; }
        // The places of the story (Docs/Story.md), in sheet order; none when made without the sheets.
        public IReadOnlyList<PlaceSite> Places { get; internal set; } = new List<PlaceSite>();
        // The small places along the roads (CarveLandmarks); none without the story sheets.
        public IReadOnlyList<Landmark> Landmarks { get; internal set; } = new List<Landmark>();
        // The signposts by the roads (2026-11-11) and the place each points to along its road, for the screen.
        public IReadOnlyList<(int X, int Y, PlaceDefinition Toward)> Signposts => _signposts;
        readonly List<(int X, int Y, PlaceDefinition Toward)> _signposts = new List<(int, int, PlaceDefinition)>();
        internal void AddSignpost(int x, int y, PlaceDefinition toward) => _signposts.Add((x, y, toward));
    }

    // Where a place was carved: its middle (the waypoint), its plaza's radius, where its people stand at home, and the
    // spots each walks between by day.
    // A small place by a road (WorldGenerator.CarveLandmarks): a camp, ruins, a shrine, a hamlet or an outpost.
    public sealed class Landmark
    {
        public Landmark(int x, int y, string style)
        {
            X = x;
            Y = y;
            Style = style;
        }

        public int X { get; }
        public int Y { get; }
        public string Style { get; }
        public Vec2 Center => TileMap.Center(X, Y);
    }

    public sealed class PlaceSite
    {
        internal PlaceSite(PlaceDefinition definition, int x, int y, IReadOnlyList<(CharacterDefinition Character, Vec2 At)> people,
            IReadOnlyDictionary<string, IReadOnlyList<Vec2>> rounds = null)
        {
            Definition = definition;
            X = x;
            Y = y;
            People = people;
            Rounds = rounds ?? new Dictionary<string, IReadOnlyList<Vec2>>();
        }

        // The people of the story go about the plaza by day (2026-11-11, characters.csv walk): from home to two other
        // spots and back, lingering at each, on a loop of about RoundSeconds (half that for those who run), each person
        // at their own point in it; at night everyone is home. Where they are is worked out from the world's clock
        // alone, so the host and every client see them in the same place without a word between them.
        public const float WalkSpeed = 1.1f;
        public const float RunSpeed = 2.4f;
        public const float RoundSeconds = 70;
        // Playtests hold everyone at home so they can be walked up to (Playtest "still").
        public static bool Still;

        // Where the stalls of its shops stand (WorldGenerator.CarveShops), in order; empty when it has none.
        public IReadOnlyList<(int X, int Y)> Shops { get; internal set; } = new List<(int X, int Y)>();

        // Each wanderer's spots by character id, home first.
        public IReadOnlyDictionary<string, IReadOnlyList<Vec2>> Rounds { get; }

        // Longer days slow the clock, not the walking: dayMinutes turns the clock back into seconds of play.
        public Vec2 PersonAt(CharacterDefinition character, Vec2 home, long clock, int dayMinutes = Calendar.BaseDayMinutes)
        {
            if (Still || !Rounds.TryGetValue(character.Id, out var spots) || spots.Count < 2 || Calendar.IsNight(clock)) return home;
            bool runs = character.Walk == "run";
            float speed = runs ? RunSpeed : WalkSpeed;
            int n = spots.Count;
            var walks = new float[n];
            float walking = 0;
            for (int i = 0; i < n; i++)
            {
                walks[i] = Vec2.Distance(spots[i], spots[(i + 1) % n]) / speed;
                walking += walks[i];
            }
            float round = runs ? RoundSeconds / 2 : RoundSeconds;
            float dwell = System.Math.Max(2, (round - walking) / n);
            double period = walking + dwell * n;
            double played = clock * (double)Calendar.DayMinutesOf(dayMinutes) / Calendar.BaseDayMinutes;
            double t = (played / World.TicksPerSecond + character.Index * 13.7) % period;
            for (int i = 0; i < n; i++)
            {
                if (t < dwell) return spots[i];
                t -= dwell;
                if (t < walks[i]) return Vec2.Lerp(spots[i], spots[(i + 1) % n], (float)(t / walks[i]));
                t -= walks[i];
            }
            return home;
        }

        // Everyone here and where they are at that time.
        public IEnumerable<(CharacterDefinition Character, Vec2 At)> PeopleAt(long clock, int dayMinutes = Calendar.BaseDayMinutes)
        {
            foreach (var (character, home) in People) yield return (character, PersonAt(character, home, clock, dayMinutes));
        }

        public PlaceDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }
        public int Radius => Definition.Radius;
        public Vec2 Center => TileMap.Center(X, Y);
        public IReadOnlyList<(CharacterDefinition Character, Vec2 At)> People { get; }

        public bool Contains(Vec2 position) => Vec2.Distance(position, Center) <= Radius + 0.5f;
    }

    // Caves from a seed: random fill, a few smoothing passes, a clearing around the spawn, a boss arena some way
    // off with a tunnel to it, every cave the spawn cannot reach filled in, then ore veins in the rock. Only integer
    // math, so the host and every client build the same map from the same seed and the host only sends the tiles
    // players have changed since.
    public static class WorldGenerator
    {
        public const int DefaultSize = 128;
        // The map the game makes (2026-11-11, the owner: "the map is too small; the waypoints at least 200 tiles
        // apart"). Tests and most sims keep DefaultSize; every distance and count below was set for it and grows with
        // ScaleOf.
        public const int GameSize = 1024;

        // How many times DefaultSize the map is across (1 for it or smaller).
        public static int ScaleOf(int width, int height) => Math.Max(1, Math.Min(width, height) / DefaultSize);
        const ulong Stream = 1;
        const int WallPercent = 45;
        const int SmoothingPasses = 5;
        const int SpawnClearing = 5;
        public const int ArenaRadius = 10;
        const int ArenaDistance = 38;
        const int TunnelHalfWidth = 1;
        // How many tiles of road between its lamp posts (2026-11-11; the roads between places are 10 to 30 long).
        const int RoadLampEvery = 5;
        // Ore gets its own random stream, so adding it left the caves exactly as they were.
        const ulong OreStream = 4;
        const int CopperVeins = 60;
        const int IronVeins = 25;
        // Iron is only found at least this far from the spawn.
        const int IronDistance = 30;
        const int VeinPercent = 60;
        // The deep zone (Docs/Bosses.md): mithril only this far out, which on the default map is its four corners,
        // and the second boss's arena in one of them, about DeepArenaOffset tiles along each axis from the spawn. Both
        // were added after the rest and use their own random streams, so the caves, the first arena and the copper
        // and iron veins of every seed stayed exactly as they were.
        const ulong DeepStream = 5;
        const ulong MithrilStream = 6;
        const int MithrilDistance = 60;
        const int MithrilVeins = 30;
        const int DeepArenaOffset = 50;
        // Biomes came later still, from their own stream; they only say which kind of cave a tile is in.
        const ulong BiomeStream = 7;
        // The lava fields (Docs/Biomes.md) were carved last of all, on their own stream: the third arena in the corner
        // opposite the deep one with a tunnel from the spawn, obsidian veins and pools of lava.
        const ulong LavaStream = 8;
        const int ObsidianDistance = 55;
        const int ObsidianVeins = 26;
        const int LavaPoolDistance = 45;
        const int LavaPools = 30;
        // The abyss (Docs/Biomes.md), carved after the lava fields on its own stream: the fourth arena at the far end of
        // one of the two corners beside the lava fields, reached by a tunnel from the lava arena, and void ore there.
        const ulong AbyssStream = 9;
        const int VoidVeins = 22;
        // Pools of water (Docs/Fishing.md), last of all on their own stream: set into the rock at the edge of the
        // caves, so no way through is ever closed, away from the spawn and the arenas and not in the lava fields.
        const ulong WaterStream = 10;
        const int WaterPools = 40;
        const int WaterDistance = 10;
        // The underground lake (Docs/Biomes.md, 2026-11-09), last of all on its own stream: the fifth arena at the far
        // end of the side corner the abyss did not take, reached by a tunnel from the deep arena, and many more pools.
        const ulong LakeStream = 11;
        // The sand caves (Docs/Biomes.md, 2026-11-09), after the lake: the sixth arena in the crystal quarter's near band,
        // on the flank further from the first arena, about SandArenaDistance from the spawn, reached by a tunnel from
        // the first arena.
        const ulong SandStream = 12;
        static readonly (int Along, int Across) SandArenaOffset = (44, 12);

        // The arena lies in one of eight directions from the spawn, about ArenaDistance tiles away.
        static readonly (int X, int Y)[] ArenaOffsets =
        {
            (38, 0), (27, 27), (0, 38), (-27, 27), (-38, 0), (-27, -27), (0, -38), (27, -27),
        };

        // The places of the story (Docs/Story.md, 2026-11-10), carved last of all on their own stream, so a world made
        // without the story sheets (the tests, the sims) is the caves it always was.
        const ulong PlaceStream = 13;
        // Twenty-four ways round, every 15 degrees, as thousandths (integers only, so every machine carves the same
        // places; 2026-11-11, from sixteen, so six places can stand 60 degrees apart).
        static readonly (int X, int Y)[] Ways =
        {
            (1000, 0), (966, 259), (866, 500), (707, 707), (500, 866), (259, 966), (0, 1000), (-259, 966),
            (-500, 866), (-707, 707), (-866, 500), (-966, 259), (-1000, 0), (-966, -259), (-866, -500), (-707, -707),
            (-500, -866), (-259, -966), (0, -1000), (259, -966), (500, -866), (707, -707), (866, -500), (966, -259),
        };

        // Seventy-two ways round, every 5 degrees, as thousandths (integers only, so every machine carves the same).
        static readonly (int X, int Y)[] Ways72 =
        {
            (1000, 0), (996, 87), (985, 174), (966, 259), (940, 342), (906, 423), (866, 500), (819, 574), (766, 643),
            (707, 707), (643, 766), (574, 819), (500, 866), (423, 906), (342, 940), (259, 966), (174, 985), (87, 996), (0,
            1000), (-87, 996), (-174, 985), (-259, 966), (-342, 940), (-423, 906), (-500, 866), (-574, 819), (-643, 766),
            (-707, 707), (-766, 643), (-819, 574), (-866, 500), (-906, 423), (-940, 342), (-966, 259), (-985, 174), (-996,
            87), (-1000, 0), (-996, -87), (-985, -174), (-966, -259), (-940, -342), (-906, -423), (-866, -500), (-819,
            -574), (-766, -643), (-707, -707), (-643, -766), (-574, -819), (-500, -866), (-423, -906), (-342, -940),
            (-259, -966), (-174, -985), (-87, -996), (0, -1000), (87, -996), (174, -985), (259, -966), (342, -940), (423,
            -906), (500, -866), (574, -819), (643, -766), (707, -707), (766, -643), (819, -574), (866, -500), (906, -423),
            (940, -342), (966, -259), (985, -174), (996, -87)
        };

        // How far apart the places of the story stand at least, on the game's map (scaled for smaller maps).
        public const int MinPlaceGap = 200;

        public static GeneratedWorld Generate(ulong seed, int width = DefaultSize, int height = DefaultSize, StoryData story = null)
        {
            var world = GenerateCaves(seed, width, height);
            if (story != null && story.Places.Count > 0) world.Places = CarvePlaces(world, seed, story);
            return world;
        }

        // Each place round its plaza, in sheet order: at its distance from the spawn, of the sixteen ways the one
        // nearest the place its road comes from that keeps clear of the arenas and the other places, with its waypoint
        // in the middle. Then the roads, three wide and paved, bent once to keep out of the arenas, never laid over a
        // plaza. Then each plaza is dressed in its style (Decorate), its people stood on open floor, and any person or
        // road the dressing shut off from the waypoint opened again.
        static List<PlaceSite> CarvePlaces(GeneratedWorld world, ulong seed, StoryData story)
        {
            var map = world.Map;
            var random = new GameRandom(seed, PlaceStream);
            var arenas = world.Arenas.Select(a => (X: TileMap.Cell(a.X), Y: TileMap.Cell(a.Y))).ToList();
            var spots = new List<(PlaceDefinition Place, int X, int Y)>();
            int Square(int dx, int dy) => dx * dx + dy * dy;
            // The places branch out from the spawn along the roads (2026-11-12, the owner: "a vast map like Diablo; the
            // places far apart along big roads, small places between, not near and even"): each stands its places.csv
            // distance from the place its road comes from, heading on outward the way that road was going (the village's
            // own roads spread round it), turned and pulled in as little as it must to keep clear of the map's edge, the
            // arenas and the other places (MinPlaceGap apart). Distances are for GameSize and shrink with smaller maps.
            int size = System.Math.Min(map.Width, map.Height);
            int gap = MinPlaceGap * size / GameSize;
            bool ClearOfArenas((int X, int Y) at, int radius) =>
                arenas.All(a => Square(at.X - a.X, at.Y - a.Y) >= (ArenaRadius + radius + 3) * (ArenaRadius + radius + 3));
            int Heading(int dx, int dy)
            {
                int bestWay = 0;
                long bestDot = long.MinValue;
                for (int w = 0; w < Ways72.Length; w++)
                {
                    long dot = (long)Ways72[w].X * dx + (long)Ways72[w].Y * dy;
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestWay = w;
                    }
                }
                return bestWay;
            }
            var rootPlaces = story.Places.Where(p => p.Distance > 0 && (p.Road.Length == 0 || story.Places.Any(q => q.Id == p.Road && q.Distance == 0))).ToList();
            int rootTurn = random.Next(Ways72.Length);
            var children = new Dictionary<string, int>();
            foreach (var place in story.Places)
            {
                int r = place.Radius;
                int cx = world.SpawnX, cy = world.SpawnY;
                if (place.Distance > 0)
                {
                    var from = spots.FirstOrDefault(s => s.Place.Id == place.Road);
                    bool root = from.Place == null || from.Place.Distance == 0;
                    int fromX = root ? world.SpawnX : from.X, fromY = root ? world.SpawnY : from.Y;
                    int sibling = children.TryGetValue(place.Road, out int n) ? n : 0;
                    children[place.Road] = sibling + 1;
                    int heading = root
                        ? (rootTurn + rootPlaces.IndexOf(place) * Ways72.Length / System.Math.Max(1, rootPlaces.Count) + random.Next(5) - 2 + Ways72.Length) % Ways72.Length
                        : (Heading(fromX - world.SpawnX, fromY - world.SpawnY) + (sibling % 2 == 0 ? 1 : -1) * (2 + random.Next(5)) + Ways72.Length) % Ways72.Length;
                    int full = System.Math.Max(r + 25, place.Distance * size / GameSize);
                    // Clear of the map's edge by a twentieth of it, so a far place does not hug the rim.
                    int rim = r + 3 + size / 20;
                    bool Fits(int x, int y) => x >= rim && y >= rim && x < map.Width - rim && y < map.Height - rim;
                    bool Clear(int x, int y, int apart) => Fits(x, y) && ClearOfArenas((x, y), r)
                        && spots.All(s => Square(x - s.X, y - s.Y) >= System.Math.Max(apart, s.Place.Radius + r + 4) * System.Math.Max(apart, s.Place.Radius + r + 4));
                    (int X, int Y)? found = null;
                    // Turned up to all the way round and pulled in up to three fifths, nearest the heading first.
                    for (int pull = 0; pull <= 6 && found == null; pull++)
                        for (int turn = 0; turn <= 36 && found == null; turn++)
                            foreach (int side in turn == 0 ? new[] { 0 } : new[] { 1, -1 })
                            {
                                int way = (heading + side * turn + Ways72.Length) % Ways72.Length;
                                int distance = full * (10 - pull) / 10;
                                int x = fromX + Ways72[way].X * distance / 1000, y = fromY + Ways72[way].Y * distance / 1000;
                                if (!Clear(x, y, gap)) continue;
                                found = (x, y);
                                break;
                            }
                    // Failing that (the small maps of the tests), any way round the spawn clear of the rest.
                    for (int distance = full; distance >= r + 12 && found == null; distance -= 2)
                        for (int way = 0; way < Ways72.Length && found == null; way++)
                        {
                            int x = world.SpawnX + Ways72[(way + rootTurn) % Ways72.Length].X * distance / 1000;
                            int y = world.SpawnY + Ways72[(way + rootTurn) % Ways72.Length].Y * distance / 1000;
                            if (Clear(x, y, 0)) found = (x, y);
                        }
                    (cx, cy) = found ?? (Clamp(fromX + Ways72[heading].X * full / 1000, r + 3, map.Width - r - 4), Clamp(fromY + Ways72[heading].Y * full / 1000, r + 3, map.Height - r - 4));
                }
                for (int y = cy - r; y <= cy + r; y++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (IsInside(map, x, y) && Square(x - cx, y - cy) <= r * r) map[x, y] = place.Floor;
                    }
                }
                if (place.Waypoint) map[cx, cy] = TileType.Waypoint;
                spots.Add((place, cx, cy));
            }

            bool InPlaza(int x, int y) => spots.Any(s => Square(x - s.X, y - s.Y) <= s.Place.Radius * s.Place.Radius);
            int ArenaTiles(List<(int X, int Y)> line) => line.Count(p => arenas.Any(a => Square(p.X - a.X, p.Y - a.Y) <= (ArenaRadius + 2) * (ArenaRadius + 2)));
            var roads = new List<(List<(int X, int Y)> Path, (PlaceDefinition Place, int X, int Y) From, (PlaceDefinition Place, int X, int Y) To)>();
            foreach (var spot in spots)
            {
                var from = spots.FirstOrDefault(s => s.Place.Id == spot.Place.Road);
                if (from.Place == null || from.Place == spot.Place) continue;
                // A road that wanders (2026-11-12): through a bend every 60 tiles or so, each pushed off the straight
                // line by up to a seventh of the road's length; of six such, the one over the fewest arena tiles.
                List<(int X, int Y)> path = null;
                int fewest = int.MaxValue;
                int length = (int)System.Math.Sqrt(Square(spot.X - from.X, spot.Y - from.Y));
                int bends = System.Math.Max(1, length / 60);
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    var line = new List<(int X, int Y)>();
                    int lastX = from.X, lastY = from.Y;
                    for (int k = 1; k <= bends + 1; k++)
                    {
                        int px = from.X + (spot.X - from.X) * k / (bends + 1), py = from.Y + (spot.Y - from.Y) * k / (bends + 1);
                        if (k <= bends)
                        {
                            int push = length > 0 ? (random.Next(2 * length / 7 + 1) - length / 7) : 0;
                            px = Clamp(px - (spot.Y - from.Y) * push / System.Math.Max(1, length), 2, map.Width - 3);
                            py = Clamp(py + (spot.X - from.X) * push / System.Math.Max(1, length), 2, map.Height - 3);
                        }
                        line.AddRange(Line(lastX, lastY, px, py));
                        (lastX, lastY) = (px, py);
                    }
                    int crossed = ArenaTiles(line);
                    if (crossed < fewest)
                    {
                        fewest = crossed;
                        path = line;
                    }
                }
                foreach (var (x, y) in path)
                {
                    for (int ty = y - TunnelHalfWidth; ty <= y + TunnelHalfWidth; ty++)
                    {
                        for (int tx = x - TunnelHalfWidth; tx <= x + TunnelHalfWidth; tx++)
                        {
                            if (IsInside(map, tx, ty) && !InPlaza(tx, ty)) map[tx, ty] = TileType.Road;
                        }
                    }
                }
                roads.Add((path, from, spot));
            }

            // Beside the demon nest, its lord's arena (2026-11-10, bosses.csv place): on the side away from the road
            // that comes in, clear of the other places and arenas, reached from the nest by a short road.
            foreach (var (place, cx, cy) in spots.Where(s => s.Place.Arena).ToList())
            {
                int r = place.Radius;
                var from = spots.FirstOrDefault(s => s.Place.Id == place.Road);
                int fromX = from.Place != null ? from.X : world.SpawnX, fromY = from.Place != null ? from.Y : world.SpawnY;
                // Nearest the nest first, then furthest from where the road comes from.
                (int X, int Y)? best = null;
                int bestScore = int.MinValue;
                for (int reach = r + ArenaRadius + 3; reach <= r + ArenaRadius + 60 && best == null; reach += 2)
                {
                    foreach (var way in Ways)
                    {
                        int ax = cx + way.X * reach / 1000, ay = cy + way.Y * reach / 1000;
                        if (ax < ArenaRadius + 1 || ay < ArenaRadius + 1 || ax > map.Width - ArenaRadius - 2 || ay > map.Height - ArenaRadius - 2) continue;
                        if (spots.Any(s => s.Place != place && Square(ax - s.X, ay - s.Y) < (s.Place.Radius + ArenaRadius + 3) * (s.Place.Radius + ArenaRadius + 3))) continue;
                        if (arenas.Any(a => Square(ax - a.X, ay - a.Y) < (2 * ArenaRadius + 2) * (2 * ArenaRadius + 2))) continue;
                        int score = Square(ax - fromX, ay - fromY);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = (ax, ay);
                        }
                    }
                }
                if (best is not (int bx, int by)) continue;
                ClearDisc(map, bx, by, ArenaRadius);
                foreach (var (x, y) in Line(cx, cy, bx, by))
                {
                    for (int ty = y - TunnelHalfWidth; ty <= y + TunnelHalfWidth; ty++)
                        for (int tx = x - TunnelHalfWidth; tx <= x + TunnelHalfWidth; tx++)
                            if (IsInside(map, tx, ty) && !InPlaza(tx, ty) && Square(tx - bx, ty - by) > ArenaRadius * ArenaRadius) map[tx, ty] = TileType.Road;
                }
                world.AddPlaceArena(place.Id, TileMap.Center(bx, by));
                arenas.Add((bx, by));
            }

            // Along each road (2026-11-11, Docs/Story.md 길가): a lamp post just past its edge every RoadLampEvery
            // tiles, on alternate sides; a signpost where it leaves each place; and about halfway a rest spot beside
            // it, paved, with a campfire and a bench. They go on bare floor or plain rock only, never where they would
            // shut a bit of floor off from the road, and the road itself stays clear.
            // Outside the square round each plaza too, so the plaza's own dressing is all that stands near it.
            bool Free(int x, int y) => IsInside(map, x, y) && (map[x, y] == TileType.Wall || map[x, y] == TileType.Floor)
                && spots.All(p => System.Math.Abs(x - p.X) > p.Place.Radius + 1 || System.Math.Abs(y - p.Y) > p.Place.Radius + 1)
                && arenas.All(a => Square(x - a.X, y - a.Y) > (ArenaRadius + 2) * (ArenaRadius + 2));
            // Whether something solid at x, y would leave open floor beside it with no way to a road nearby.
            bool Shuts(int x, int y)
            {
                var was = map[x, y];
                map[x, y] = TileType.Wall;
                bool shut = false;
                foreach (var (sx, sy) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!IsInside(map, sx, sy) || map.IsSolid(sx, sy)) continue;
                    var seen = new HashSet<(int, int)> { (sx, sy) };
                    var open = new Queue<(int X, int Y, int D)>();
                    open.Enqueue((sx, sy, 0));
                    bool road = false;
                    while (open.Count > 0 && !road)
                    {
                        var (cx, cy, d) = open.Dequeue();
                        if (Structures.IsWay(map[cx, cy]) || map[cx, cy] == TileType.Waypoint) road = true;
                        else if (d < 10)
                        {
                            foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                                if (IsInside(map, nx, ny) && !map.IsSolid(nx, ny) && seen.Add((nx, ny))) open.Enqueue((nx, ny, d + 1));
                        }
                    }
                    if (!road) shut = true;
                }
                map[x, y] = was;
                return shut;
            }
            bool Set(int x, int y, TileType type)
            {
                if (map[x, y] != TileType.Wall && Shuts(x, y)) return false;
                map[x, y] = type;
                return true;
            }
            foreach (var (path, from, to) in roads)
            {
                bool Clear(int i, (PlaceDefinition Place, int X, int Y) end, int margin) =>
                    Square(path[i].X - end.X, path[i].Y - end.Y) > (end.Place.Radius + margin) * (end.Place.Radius + margin);
                bool Away(int i, int margin) => Clear(i, from, margin) && Clear(i, to, margin);
                // Which way the road runs at path[i], and across it (to its left).
                (int X, int Y) Along(int i)
                {
                    var (bx, by) = path[System.Math.Max(0, i - 1)];
                    var (ax, ay) = path[System.Math.Min(path.Count - 1, i + 1)];
                    return (System.Math.Sign(ax - bx), System.Math.Sign(ay - by));
                }
                // Sets the tile into the rock just past the road's edge beside path[i], on that side or else the other.
                bool Edge(int i, int side, TileType type, PlaceDefinition toward = null)
                {
                    var (dx, dy) = Along(i);
                    if (dx == 0 && dy == 0) return false;
                    foreach (int s in new[] { side, -side })
                    {
                        int px = -dy * s, py = dx * s;
                        int x = path[i].X + px * (TunnelHalfWidth + 1), y = path[i].Y + py * (TunnelHalfWidth + 1);
                        int ex = path[i].X + px * TunnelHalfWidth, ey = path[i].Y + py * TunnelHalfWidth;
                        if (!Free(x, y) || !IsInside(map, ex, ey) || map[ex, ey] != TileType.Road) continue;
                        if (!Set(x, y, type)) continue;
                        if (toward != null) world.AddSignpost(x, y, toward);
                        return true;
                    }
                    return false;
                }
                // A signpost where it leaves each end's plaza, pointing on along the road to the other end.
                for (int i = 0; i < path.Count; i++)
                {
                    if (Clear(i, from, 2) && Edge(i, 1, TileType.Signpost, to.Place)) break;
                }
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    if (Clear(i, to, 2) && Edge(i, -1, TileType.Signpost, from.Place)) break;
                }
                // The rest spot: nearest halfway, three tiles along and two deep of bare floor or rock just past the
                // road's edge on one side (the edge found column by column, so a sloping road gets a stepped alcove).
                bool rested = false;
                for (int k = 0; k <= path.Count / 2 && !rested; k++)
                {
                    foreach (int i in new[] { path.Count / 2 + k, path.Count / 2 - k })
                    {
                        if (rested || i < 1 || i >= path.Count - 1 || !Away(i, 4)) continue;
                        var (ax, ay) = path[System.Math.Max(0, i - 3)];
                        var (bx, by) = path[System.Math.Min(path.Count - 1, i + 3)];
                        bool eastWest = System.Math.Abs(bx - ax) >= System.Math.Abs(by - ay);
                        int ux = eastWest ? 1 : 0, uy = eastWest ? 0 : 1, vx = 1 - ux, vy = 1 - uy;
                        foreach (int s in new[] { 1, -1 })
                        {
                            var front = new List<(int X, int Y)>();
                            var back = new List<(int X, int Y)>();
                            for (int a = -1; a <= 1; a++)
                            {
                                int x = path[i].X + ux * a, y = path[i].Y + uy * a, steps = 0;
                                while (steps < TunnelHalfWidth + 3 && IsInside(map, x, y) && map[x, y] == TileType.Road)
                                {
                                    x += vx * s;
                                    y += vy * s;
                                    steps++;
                                }
                                if (steps == 0 || !Free(x, y) || !Free(x + vx * s, y + vy * s)) break;
                                front.Add((x, y));
                                back.Add((x + vx * s, y + vy * s));
                            }
                            if (front.Count < 3) continue;
                            foreach (var (x, y) in front.Concat(back)) map[x, y] = TileType.Road;
                            Set(back[0].X, back[0].Y, TileType.Campfire);
                            Set(back[2].X, back[2].Y, TileType.Bench);
                            rested = true;
                            break;
                        }
                    }
                }
                // The lamps, from just outside the places (they have their own), the first as soon as there is room.
                int lampEvery = RoadLampEvery * ScaleOf(map.Width, map.Height);
                int since = lampEvery - 1, lampSide = 1;
                for (int i = 0; i < path.Count; i++)
                {
                    if (!Away(i, 2)) continue;
                    if (++since < lampEvery) continue;
                    if (Edge(i, lampSide, TileType.LampPost))
                    {
                        since = 0;
                        lampSide = -lampSide;
                    }
                }
            }

            world.Landmarks = CarveLandmarks(map, roads.Select(rd => rd.Path).ToList(), spots.Select(sp => (sp.X, sp.Y, sp.Place.Radius)).ToList(), arenas, new GameRandom(seed, LandmarkStream), size);

            var sites = new List<PlaceSite>();
            foreach (var (place, cx, cy) in spots)
            {
                var wanted = Decorate(map, cx, cy, place);
                var people = Stand(map, cx, cy, place, story.Characters.Where(c => c.Place == place).ToList(), wanted);
                Open(map, cx, cy, place, people.Select(p => (TileMap.Cell(p.Item2.X), TileMap.Cell(p.Item2.Y))));
                Tidy(map, cx, cy, place);
                sites.Add(new PlaceSite(place, cx, cy, people, Rounds(map, cx, cy, place, people)));
            }
            foreach (var site in sites.Where(s => s.Definition.Shops > 0))
                site.Shops = CarveShops(map, site, spots.Select(p => (p.X, p.Y, p.Place.Radius)).ToList(), arenas);
            return sites;
        }

        // The small places along the roads between the places of the story (2026-11-12, the owner: "the big places far
        // along big roads, and small places in between", as in Diablo): every LandmarkEvery tiles of road (scaled with
        // the map), on one side of it or the other, a little clearing of radius LandmarkRadius joined to the road,
        // dressed as one of the styles. Only where all of it and a tile round it is plain rock or floor clear of the
        // places, the arenas and the other landmarks; where not, that one is skipped.
        public const int LandmarkEvery = 110, LandmarkRadius = 4;
        public static readonly string[] LandmarkStyles = { "camp", "ruins", "shrine", "hamlet", "outpost" };
        const ulong LandmarkStream = 17;

        static List<Landmark> CarveLandmarks(TileMap map, List<List<(int X, int Y)>> roads, List<(int X, int Y, int Radius)> places,
            List<(int X, int Y)> arenas, GameRandom random, int size)
        {
            var landmarks = new List<Landmark>();
            int every = System.Math.Max(40, LandmarkEvery * size / GameSize), r = LandmarkRadius;
            int Square(int dx, int dy) => dx * dx + dy * dy;
            bool Rock(TileType t) => t == TileType.Wall || t == TileType.Floor || t == TileType.CopperOre || t == TileType.IronOre
                || t == TileType.MithrilOre || t == TileType.ObsidianOre || t == TileType.VoidOre;
            bool Room(int cx, int cy)
            {
                if (places.Any(p => Square(cx - p.X, cy - p.Y) < (p.Radius + r + 10) * (p.Radius + r + 10))) return false;
                if (arenas.Any(a => Square(cx - a.X, cy - a.Y) < (ArenaRadius + r + 4) * (ArenaRadius + r + 4))) return false;
                if (landmarks.Any(l => Square(cx - l.X, cy - l.Y) < every * every / 4)) return false;
                for (int y = cy - r - 1; y <= cy + r + 1; y++)
                    for (int x = cx - r - 1; x <= cx + r + 1; x++)
                        if (Square(x - cx, y - cy) <= (r + 1) * (r + 1) && (!IsInside(map, x, y) || !Rock(map[x, y]))) return false;
                return true;
            }
            foreach (var path in roads)
            {
                int side = random.Next(2) == 0 ? 1 : -1;
                for (int i = every / 2; i < path.Count - every / 3; i += every)
                {
                    var (px, py) = path[i];
                    var (ax, ay) = path[System.Math.Max(0, i - 4)];
                    var (bx, by) = path[System.Math.Min(path.Count - 1, i + 4)];
                    int dx = bx - ax, dy = by - ay;
                    int length = System.Math.Max(1, (int)System.Math.Sqrt(Square(dx, dy)));
                    (int X, int Y)? at = null;
                    foreach (int s in new[] { side, -side })
                        foreach (int off in new[] { r + 4, r + 6, r + 8 })
                        {
                            if (at != null) break;
                            int cx = px - s * dy * off / length, cy = py + s * dx * off / length;
                            if (Room(cx, cy)) at = (cx, cy);
                        }
                    side = -side;
                    if (at is not (int lx, int ly)) continue;
                    string style = LandmarkStyles[random.Next(LandmarkStyles.Length)];
                    var floor = style == "shrine" || style == "outpost" ? TileType.StoneFloor : TileType.Floor;
                    for (int y = ly - r; y <= ly + r; y++)
                        for (int x = lx - r; x <= lx + r; x++)
                            if (Square(x - lx, y - ly) <= r * r) map[x, y] = floor;
                    // A path to the road: from the clearing's edge straight to the road.
                    foreach (var (tx, ty) in Line(lx, ly, px, py))
                    {
                        if (Structures.IsWay(map[tx, ty])) break;
                        if (Square(tx - lx, ty - ly) > (r - 1) * (r - 1) && (Rock(map[tx, ty]) || map[tx, ty] == floor)) map[tx, ty] = TileType.Road;
                    }
                    bool Put(int dx2, int dy2, TileType type)
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int k = 0; k < w; k++)
                            {
                                int x = lx + dx2 + k, y = ly + dy2 + j;
                                if (map[x, y] != floor || Square(x - lx, y - ly) > (r - 1) * (r - 1) + 1) return false;
                            }
                        for (int j = 0; j < h; j++)
                            for (int k = 0; k < w; k++)
                                map[lx + dx2 + k, ly + dy2 + j] = j == 0 && k == 0 ? type : TileType.StructurePart;
                        return true;
                    }
                    switch (style)
                    {
                        case "camp":
                            Put(0, 0, TileType.Campfire);
                            Put(-2, 1, TileType.Tent);
                            Put(2, 0, TileType.Bench);
                            Put(1, 2, TileType.Woodpile);
                            Put(-2, -2, TileType.Crates);
                            Put(2, -2, TileType.Barrel);
                            break;
                        case "ruins":
                            Put(-2, 2, TileType.BrokenPillar);
                            Put(2, 2, TileType.BrokenPillar);
                            Put(-2, -2, TileType.BrokenPillar);
                            Put(2, -1, TileType.Rubble);
                            Put(0, 2, TileType.Rubble);
                            Put(-1, -3, TileType.FallenColumn);
                            break;
                        case "shrine":
                            Put(0, 1, TileType.RuneStone);
                            Put(-2, 1, TileType.Brazier);
                            Put(2, 1, TileType.Brazier);
                            Put(-1, -2, TileType.Flowerbed);
                            Put(1, -2, TileType.Flowerbed);
                            break;
                        case "hamlet":
                            Put(-3, 0, TileType.Cottage);
                            Put(2, -1, TileType.Well);
                            Put(2, 1, TileType.Barrel);
                            Put(-2, -2, TileType.Woodpile);
                            Put(0, -3, TileType.Hedge);
                            break;
                        default:
                            Put(-2, 1, TileType.Lantern);
                            Put(2, 1, TileType.Lantern);
                            Put(-1, 2, TileType.Crates);
                            Put(1, 2, TileType.Barrel);
                            Put(0, -2, TileType.Totem);
                            break;
                    }
                    landmarks.Add(new Landmark(lx, ly, style));
                }
            }
            return landmarks;
        }

        // A shop: a timber room ShopWidth by ShopHeight outside (walls round a floor of the place's own paving, or
        // stone), its door in the middle of the front (lower) wall, the stall against the back wall facing it, and a
        // lantern in a back corner. A short road leads from the door to the plaza.
        public const int ShopWidth = 7, ShopHeight = 6;

        // Round the plaza, outside it (2026-11-11, the owner: "the merchants should not stand in the street but each
        // in a shop of their own"): up to the place's number of shops, each where all of it and a tile round it is
        // bare floor or rock clear of the other places, the arenas, the roads and their dressing, with a clear way
        // from its door to the plaza. Above the plaza first, so the doors face it; the counter spots, in order.
        static List<(int X, int Y)> CarveShops(TileMap map, PlaceSite site, List<(int X, int Y, int Radius)> plazas, List<(int X, int Y)> arenas)
        {
            var counters = new List<(int X, int Y)>();
            int r = site.Radius, cx = site.X, cy = site.Y;
            int Square(int dx, int dy) => dx * dx + dy * dy;
            bool Rock(TileType t) => t == TileType.Wall || t == TileType.Floor || t == TileType.CopperOre || t == TileType.IronOre
                || t == TileType.MithrilOre || t == TileType.ObsidianOre || t == TileType.VoidOre;
            // Outside the square round each plaza (its dressing is checked over that square).
            bool Bare(int x, int y, bool margin) => IsInside(map, x, y) && (Rock(map[x, y]) || margin && Structures.IsWay(map[x, y]))
                && plazas.All(p => System.Math.Max(System.Math.Abs(x - p.X), System.Math.Abs(y - p.Y)) > p.Radius + 1)
                && arenas.All(a => Square(x - a.X, y - a.Y) > (ArenaRadius + 2) * (ArenaRadius + 2));
            bool InThisPlaza(int x, int y) => Square(x - cx, y - cy) <= r * r;
            var floor = TileMap.IsPaving(site.Definition.Floor) ? site.Definition.Floor : TileType.StoneFloor;
            // The ways round, above the plaza first (sideways next, below last).
            var ways = Enumerable.Range(0, Ways.Length).OrderByDescending(w => Ways[w].Y).ThenBy(w => System.Math.Abs(Ways[w].X)).ToList();
            foreach (int way in ways)
            {
                if (counters.Count >= site.Definition.Shops) break;
                for (int reach = r + ShopHeight / 2 + 2; reach <= r + ShopHeight + 14; reach++)
                {
                    // The lower left corner of the walls.
                    int left = cx + Ways[way].X * reach / 1000 - ShopWidth / 2;
                    int bottom = cy + Ways[way].Y * reach / 1000 - ShopHeight / 2;
                    bool clear = true;
                    for (int y = bottom - 1; y <= bottom + ShopHeight && clear; y++)
                        for (int x = left - 1; x <= left + ShopWidth && clear; x++)
                            clear = Bare(x, y, x < left || x >= left + ShopWidth || y < bottom || y >= bottom + ShopHeight);
                    if (!clear) continue;
                    // From just below the door toward the waypoint, until the plaza: over bare floor or rock only.
                    int doorX = left + ShopWidth / 2, doorY = bottom;
                    var path = new List<(int X, int Y)>();
                    bool reached = false;
                    foreach (var (px, py) in Line(doorX, doorY - 1, cx, cy))
                    {
                        if (InThisPlaza(px, py))
                        {
                            reached = true;
                            break;
                        }
                        bool ownWall = px >= left && px < left + ShopWidth && py >= bottom && py < bottom + ShopHeight;
                        if (ownWall || !(Rock(map[px, py]) || Structures.IsWay(map[px, py]))) break;
                        path.Add((px, py));
                    }
                    if (!reached) continue;
                    for (int y = bottom - 1; y <= bottom + ShopHeight; y++)
                        for (int x = left - 1; x <= left + ShopWidth; x++)
                        {
                            bool wall = x == left || x == left + ShopWidth - 1 || y == bottom || y == bottom + ShopHeight - 1;
                            bool outside = x < left || x >= left + ShopWidth || y < bottom || y >= bottom + ShopHeight;
                            if (outside && Structures.IsWay(map[x, y])) continue;
                            map[x, y] = outside ? TileType.Floor : wall ? TileType.TimberWall : floor;
                        }
                    map[doorX, doorY] = TileType.Door;
                    foreach (var (px, py) in path)
                        if (!Structures.IsWay(map[px, py])) map[px, py] = TileType.Road;
                    var counter = (doorX, bottom + ShopHeight - 2);
                    map[left + 1, bottom + ShopHeight - 2] = TileType.Lantern;
                    map[left + ShopWidth - 2, bottom + ShopHeight - 2] = TileType.Crates;
                    counters.Add(counter);
                    // The tiles round it are taken now.
                    plazas.Add((doorX, bottom + ShopHeight / 2, ShopWidth / 2 + 1));
                    break;
                }
            }
            return counters;
        }

        // Dresses a plaza in its style (Docs/Story.md, 장소 꾸밈) and gives where its people would stand, in
        // characters.csv order (more people than that stand round a ring). Nothing goes on the waypoint or right
        // beside it, off the plaza, or on anything but the plaza's own floor; a structure of more than one tile goes
        // only where all of it fits.
        static List<(int X, int Y)> Decorate(TileMap map, int cx, int cy, PlaceDefinition place)
        {
            int r = place.Radius;
            bool Free(int dx, int dy)
            {
                int x = cx + dx, y = cy + dy;
                return IsInside(map, x, y) && dx * dx + dy * dy <= r * r && map[x, y] == place.Floor
                    && !(System.Math.Abs(dx) <= 1 && System.Math.Abs(dy) <= 1);
            }
            void Put(int dx, int dy, TileType type)
            {
                var (w, h) = Structures.Footprint(type);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        if (!Free(dx + i, dy + j)) return;
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        map[cx + dx + i, cy + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
            }
            void Both(int dx, int dy, TileType type)
            {
                Put(dx, dy, type);
                Put(-dx - Structures.Footprint(type).W + 1, dy, type);
            }
            // A structure out on the plaza's water (the harbour's boats), all of it on water.
            void Afloat(int dx, int dy, TileType type)
            {
                var (w, h) = Structures.Footprint(type);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int x = cx + dx + i, y = cy + dy + j;
                        if (!IsInside(map, x, y) || (dx + i) * (dx + i) + (dy + j) * (dy + j) > r * r || map[x, y] != TileType.Water) return;
                    }
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        map[cx + dx + i, cy + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
            }
            void Lay(int dx, int dy, TileType floor)
            {
                int x = cx + dx, y = cy + dy;
                if (IsInside(map, x, y) && dx * dx + dy * dy <= r * r && map[x, y] == place.Floor && !(dx == 0 && dy == 0)) map[x, y] = floor;
            }
            // Where Open will cut a gate through a wall round the plaza (2026-11-10): from the first tile, in its
            // order, of each road that comes up to the plaza, a way three wide straight to the middle (the rest of
            // that road is then reached through it). The royal city sets its towers, fountains and lamp posts clear
            // of these rather than lose them to the gate.
            var gates = new HashSet<(int X, int Y)>();
            int reach = r + 2;
            bool Approach(int x, int y)
            {
                int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                return IsInside(map, x, y) && map[x, y] == TileType.Road && d > r * r && d <= reach * reach;
            }
            var seen = new HashSet<(int X, int Y)>();
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!Approach(x, y) || !seen.Add((x, y))) continue;
                    var road = new Queue<(int X, int Y)>();
                    road.Enqueue((x, y));
                    while (road.Count > 0)
                    {
                        var (rx, ry) = road.Dequeue();
                        foreach (var (nx, ny) in new[] { (rx + 1, ry), (rx - 1, ry), (rx, ry + 1), (rx, ry - 1) })
                            if (Approach(nx, ny) && seen.Add((nx, ny))) road.Enqueue((nx, ny));
                    }
                    foreach (var (lx, ly) in Line(x, y, cx, cy))
                        for (int ty = ly - TunnelHalfWidth; ty <= ly + TunnelHalfWidth; ty++)
                            for (int tx = lx - TunnelHalfWidth; tx <= lx + TunnelHalfWidth; tx++)
                                gates.Add((tx - cx, ty - cy));
                }
            }
            // At the first of the spots where all of the structure fits clear of the gates (and, when inner, inside
            // the castle wall's ring).
            bool Clear(TileType type, bool inner, int dx, int dy)
            {
                var (w, h) = Structures.Footprint(type);
                for (int j = 0; j < h; j++)
                {
                    for (int i = 0; i < w; i++)
                    {
                        int x = dx + i, y = dy + j;
                        if (!Free(x, y) || gates.Contains((x, y)) || inner && x * x + y * y > (r - 2) * (r - 2)) return false;
                    }
                }
                return true;
            }
            void PutClear(TileType type, bool inner, params (int X, int Y)[] spots)
            {
                foreach (var (dx, dy) in spots)
                {
                    if (!Clear(type, inner, dx, dy)) continue;
                    Put(dx, dy, type);
                    return;
                }
            }
            // A pair mirrored across the middle at the first of the spots (given on the right) where both stand clear,
            // so a gate on one side moves both alike (2026-11-10); where no spot holds both, each takes its own.
            void PutPair(TileType type, bool inner, params (int X, int Y)[] spots) => PutMirrored(type, inner, true, spots);
            // The same for small things, which rather than stand alone are left out, so the place stays mirrored.
            void PutSmall(TileType type, params (int X, int Y)[] spots) => PutMirrored(type, true, false, spots);
            // A row of small things of one type, moved whole with its mirror to the first of the shifts where all
            // of both stand clear.
            void PutRow(TileType type, (int X, int Y)[] row, params (int X, int Y)[] shifts)
            {
                foreach (var (sx, sy) in shifts)
                {
                    bool clear = row.All(t => Clear(type, true, t.X + sx, t.Y + sy) && Clear(type, true, -(t.X + sx), t.Y + sy));
                    if (!clear) continue;
                    foreach (var (tx, ty) in row)
                    {
                        Put(tx + sx, ty + sy, type);
                        Put(-(tx + sx), ty + sy, type);
                    }
                    return;
                }
            }
            // Just behind a tower or a house, whose picture stands two tiles above its footprint and would hide it.
            bool Behind(int dx, int dy)
            {
                int x = cx + dx, y = cy + dy;
                for (int ay = y - 5; ay < y; ay++)
                {
                    for (int ax = x - 3; ax <= x; ax++)
                    {
                        if (!IsInside(map, ax, ay)) continue;
                        var t = map[ax, ay];
                        if (t != TileType.Tower && t != TileType.Cottage && t != TileType.Cabin) continue;
                        var (w, h) = Structures.Footprint(t);
                        if (x < ax + w && y >= ay + h && y < ay + h + 2) return true;
                    }
                }
                return false;
            }
            void PutMirrored(TileType type, bool inner, bool alone, (int X, int Y)[] spots)
            {
                int w = Structures.Footprint(type).W;
                var mirrored = spots.Select(s => (-s.X - w + 1, s.Y)).ToArray();
                for (int k = 0; k < spots.Length; k++)
                {
                    if (!Clear(type, inner, spots[k].X, spots[k].Y) || !Clear(type, inner, mirrored[k].Item1, mirrored[k].Item2)) continue;
                    // A small thing would be lost behind a big one.
                    if (!alone && (Behind(spots[k].X, spots[k].Y) || Behind(mirrored[k].Item1, mirrored[k].Item2))) continue;
                    Put(spots[k].X, spots[k].Y, type);
                    Put(mirrored[k].Item1, mirrored[k].Item2, type);
                    return;
                }
                if (!alone) return;
                PutClear(type, inner, spots);
                PutClear(type, inner, mirrored);
            }
            void Lamps(TileType lamp)
            {
                foreach (int w in new[] { 2, 6, 10, 14 }) Put(Ways[w].X * (r - 1) / 1000, Ways[w].Y * (r - 1) / 1000, lamp);
            }
            var wanted = new List<(int X, int Y)>();
            switch (place.Style)
            {
                case "village":
                    // Road paths crossing at the waypoint (road, so nothing is built on them and no paving comes up
                    // free), two cottages, a well, lamp posts along the paths, a signpost at the crossing, crates and
                    // barrels, benches, flowerbeds and giant mushrooms.
                    for (int a = -r; a <= r; a++)
                        for (int b = -1; b <= 1; b++)
                        {
                            Lay(a, b, TileType.Road);
                            Lay(b, a, TileType.Road);
                        }
                    Put(-7, 2, TileType.Cottage);
                    Put(4, 2, TileType.Cottage);
                    Both(-3, 2, TileType.Flowerbed);
                    Put(-4, -4, TileType.Well);
                    foreach (var (dx, dy) in new[] { (-2, 5), (2, 5), (-2, -5), (2, -5) }) Put(dx, dy, TileType.LampPost);
                    Put(2, -2, TileType.Signpost);
                    foreach (var (dx, dy) in new[] { (4, -4), (5, -4) }) Put(dx, dy, TileType.Crates);
                    Put(4, -5, TileType.Barrel);
                    Put(6, -4, TileType.Barrel);
                    Both(-6, -2, TileType.Bench);
                    foreach (var (dx, dy) in new[] { (-8, -3), (8, -3), (-3, -8), (3, -8), (-6, -6), (6, -6) }) Put(dx, dy, TileType.MushroomTree);
                    wanted.Add((cx, cy + 3));
                    wanted.Add((cx - 2, cy - 3));
                    break;
                case "kingdom":
                    // Towers on the four corners of a castle wall round the edge (gates are opened where the roads
                    // come in, by Open), a red carpet up the middle to the throne, statues by its south end, pillars,
                    // two great fountains, hedges, flowerbeds, royal banners and lamp posts. All of it is mirrored left
                    // and right (2026-11-10): a pair one of which is in a gate's way moves both alike to the next spot
                    // where both stand clear, the towers along the wall and the rest inside it. Towers and fountains
                    // always stand, alone if no spot holds both; a small pair with no spot for both is left out.
                    var corner = new[] { (6, 6), (7, 4), (4, 7), (8, 2), (2, 8) };
                    PutPair(TileType.Tower, false, corner);
                    PutPair(TileType.Tower, false, corner.Select(c => (c.Item1, -c.Item2 - 1)).ToArray());
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int d = dx * dx + dy * dy;
                            if (d <= r * r && d > (r - 2) * (r - 2)) Put(dx, dy, TileType.CastleWall);
                        }
                    for (int dy = -r + 1; dy <= r - 4; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            Lay(dx, dy, TileType.RedCarpet);
                    Put(0, r - 3, TileType.Throne);
                    PutSmall(TileType.Pillar, (2, r - 3), (3, r - 3), (3, r - 4));
                    // The row of pillars along the carpet, each a step along or out if a gate is in its way. Statues
                    // stand just inside the wall (on its ring they never stood: the wall is laid first).
                    foreach (int dy in new[] { -5, -2, 2 })
                        PutSmall(TileType.Pillar, (3, dy), (3, dy + 1), (3, dy - 1), (4, dy), (2, dy), (4, dy + 1), (4, dy - 1), (2, dy + 1), (2, dy - 1));
                    PutSmall(TileType.Statue, (2, -r + 3), (3, -r + 3), (2, -r + 4), (3, -r + 4), (4, -r + 4), (4, -r + 5), (5, -r + 5));
                    var basins = new[] { (5, -3), (5, -5), (5, 1), (4, 5), (6, -1), (4, -1), (4, 3), (3, -6), (6, 2) };
                    PutPair(TileType.BigFountain, true, basins);
                    PutRow(TileType.Hedge, new[] { (6, 1), (7, 1) }, (0, 0), (0, 1), (0, -1), (0, 2), (0, -2), (-1, 3));
                    PutSmall(TileType.RoyalBanner, (5, 4), (4, 4), (6, 4), (5, 5), (5, 3));
                    PutSmall(TileType.Flowerbed, (5, -6), (4, -6), (6, -5), (4, -5), (6, -4), (2, -4), (2, -3), (6, -1));
                    // Lamp posts beside the fountains: on the carpet side, or the other side, or the nearest spot round it where a
                    // gate or a tower is in the way.
                    foreach (var (fx, fy) in basins)
                    {
                        if (map[cx + fx, cy + fy] != TileType.BigFountain) continue;
                        PutSmall(TileType.LampPost, new[] { (fx - 1, fy), (fx + 2, fy), (fx - 1, fy + 1), (fx + 2, fy + 1) }
                            .Concat(from dy in Enumerable.Range(fy - 2, 5) from dx in Enumerable.Range(fx - 3, 8)
                                    where dx < fx || dx > fx + 1 || dy < fy || dy > fy + 1
                                    orderby System.Math.Abs(2 * dx - 2 * fx - 1) + 2 * System.Math.Abs(dy - fy), dy, dx
                                    select (dx, dy)).ToArray());
                        break;
                    }
                    wanted.Add((cx, cy + r - 4));
                    wanted.Add((cx - 2, cy + r - 5));
                    break;
                case "hut":
                    // A log cabin, a fenced garden with a scarecrow, a woodpile, a training dummy and a weapon rack
                    // (the hero keeps his hand in), a campfire with a bench, a barrel, a lamp post and giant mushrooms.
                    Put(-3, 2, TileType.Cabin);
                    foreach (var (dx, dy) in new[] { (2, 3), (3, 3), (2, 4), (3, 4) }) Lay(dx, dy, TileType.Soil);
                    foreach (var (dx, dy) in new[] { (4, 2), (4, 3), (4, 4), (2, 5), (3, 5) }) Put(dx, dy, TileType.Fence);
                    Put(2, 2, TileType.Scarecrow);
                    Put(1, 2, TileType.Woodpile);
                    Put(-4, -2, TileType.TrainingDummy);
                    Put(-4, 0, TileType.WeaponRack);
                    Put(3, -2, TileType.Campfire);
                    Put(4, -3, TileType.Bench);
                    Put(-2, -4, TileType.Barrel);
                    Put(0, -5, TileType.LampPost);
                    foreach (var (dx, dy) in new[] { (-5, -3), (5, -1), (3, -5), (-5, 1) }) Put(dx, dy, TileType.MushroomTree);
                    wanted.Add((cx + 1, cy - 2));
                    wanted.Add((cx - 2, cy - 2));
                    break;
                case "nest":
                    // Great demon tents, a horned totem between two braziers of violet fire, demon banners, cages,
                    // crystal spires round the edge for light, bones strewn about.
                    Both(4, 2, TileType.BigTent);
                    Both(4, -4, TileType.BigTent);
                    Put(0, 5, TileType.Totem);
                    Both(2, 5, TileType.Brazier);
                    Both(3, 6, TileType.DemonBanner);
                    Both(3, -6, TileType.Cage);
                    foreach (int w in new[] { 1, 4, 7, 9, 12, 15 }) Put(Ways[w].X * (r - 1) / 1000, Ways[w].Y * (r - 1) / 1000, TileType.Spire);
                    foreach (var (dx, dy) in new[] { (-2, -4), (3, -3), (-5, 0), (6, 0), (-1, -6), (2, -6), (-3, 3), (4, 5) }) Put(dx, dy, TileType.Bones);
                    wanted.Add((cx, cy + 3));
                    wanted.Add((cx - 2, cy + 2));
                    break;
                case "harbor":
                    // An underground harbour (2026-11-10): the south of the plaza is the lake, with a quay along its
                    // shore, a plank pier out over the water and boats moored either side; bollards on the quay, a
                    // lighthouse, a fisher's cottage, nets drying, an anchor, crates and barrels, lamp posts.
                    for (int dy = -3; dy >= -r; dy--)
                        for (int dx = -1; dx <= 1; dx++)
                            if (dy >= -r + 2) Lay(dx, dy, TileType.Dock);
                    for (int dy = -3; dy >= -r; dy--)
                        for (int dx = -r; dx <= r; dx++)
                            Lay(dx, dy, TileType.Water);
                    Afloat(-5, -6, TileType.Boat);
                    Afloat(3, -7, TileType.Boat);
                    foreach (int dx in new[] { 2, 6 }) Both(dx, -2, TileType.Bollard);
                    Both(4, -2, TileType.LampPost);
                    Put(5, 3, TileType.Lighthouse);
                    Put(-7, 2, TileType.Cottage);
                    foreach (int dx in new[] { -3, -2 }) Put(dx, 2, TileType.NetRack);
                    Put(3, 1, TileType.Anchor);
                    Put(2, -1, TileType.Crates);
                    Put(3, -1, TileType.Barrel);
                    Put(-3, -1, TileType.Barrel);
                    Put(-7, -1, TileType.Crates);
                    wanted.Add((cx + 1, cy + 3));
                    wanted.Add((cx, cy - 6));
                    break;
                case "ruins":
                    // A ruined temple (2026-11-10): an altar of glowing runes at the north, rune stones beside it, a
                    // broken ring of pillars (some still standing), fallen columns, rubble everywhere and giant
                    // mushrooms grown up through the floor.
                    Put(-1, 4, TileType.Altar);
                    Both(3, 4, TileType.RuneStone);
                    foreach (int w in new[] { 0, 2, 4, 6, 8, 10, 12, 14 })
                        Put(Ways[w].X * (r - 2) / 1000, Ways[w].Y * (r - 2) / 1000, w % 4 == 0 ? TileType.Pillar : TileType.BrokenPillar);
                    Put(-5, -3, TileType.FallenColumn);
                    Put(3, -5, TileType.FallenColumn);
                    foreach (var (dx, dy) in new[] { (-2, 2), (4, 1), (-4, -1), (2, -3), (-1, -5), (5, -2), (-6, 2), (1, 6), (-3, -6) }) Put(dx, dy, TileType.Rubble);
                    foreach (var (dx, dy) in new[] { (-6, -5), (6, 4) }) Put(dx, dy, TileType.MushroomTree);
                    wanted.Add((cx, cy + 2));
                    wanted.Add((cx + 2, cy - 2));
                    break;
                case "mine":
                    // A mining village (2026-11-10): a timbered mine entrance in the north, rails running out of it
                    // past the waypoint, a cart and heaps of ore, lanterns, a miner's cabin, woodpile, crates and
                    // barrels.
                    Put(2, 5, TileType.MineEntrance);
                    foreach (var (dx, dy) in new[] { (1, 5), (5, 5), (-2, -3), (6, -2) }) Put(dx, dy, TileType.Lantern);
                    Put(4, -3, TileType.MineCart);
                    foreach (var (dx, dy) in new[] { (5, 3), (1, 3), (5, -5) }) Put(dx, dy, TileType.OrePile);
                    for (int dy = 4; dy >= -r + 1; dy--) Lay(3, dy, TileType.Rail);
                    Put(-7, 1, TileType.Cabin);
                    Put(-3, 1, TileType.Woodpile);
                    foreach (var (dx, dy) in new[] { (-6, -3), (-5, -3) }) Put(dx, dy, TileType.Crates);
                    Put(-4, -4, TileType.Barrel);
                    wanted.Add((cx - 2, cy + 3));
                    wanted.Add((cx + 1, cy - 3));
                    break;
                default:
                    Lamps(TileType.LampPost);
                    break;
            }
            if (place.Style == "village" || place.Style == "hut") Lamps(TileType.Lamp);
            return wanted;
        }

        // After Open: a structure Open cut into is taken away whole, so none is left with tiles missing, nor tiles
        // standing for one no longer there.
        static void Tidy(TileMap map, int cx, int cy, PlaceDefinition place)
        {
            int r = place.Radius;
            var covered = new HashSet<(int, int)>();
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!IsInside(map, x, y)) continue;
                    var (w, h) = Structures.Footprint(map[x, y]);
                    if (map[x, y] == TileType.StructurePart || (w == 1 && h == 1)) continue;
                    bool whole = true;
                    for (int j = 0; j < h; j++)
                        for (int i = 0; i < w; i++)
                            if ((i > 0 || j > 0) && (!IsInside(map, x + i, y + j) || map[x + i, y + j] != TileType.StructurePart)) whole = false;
                    for (int j = 0; j < h; j++)
                        for (int i = 0; i < w; i++)
                        {
                            if (!IsInside(map, x + i, y + j)) continue;
                            if (whole) covered.Add((x + i, y + j));
                            else if (i > 0 || j > 0 ? map[x + i, y + j] == TileType.StructurePart : true) map[x + i, y + j] = place.Floor;
                        }
                }
            }
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (IsInside(map, x, y) && map[x, y] == TileType.StructurePart && !covered.Contains((x, y))) map[x, y] = place.Floor;
        }

        // Each person on the open tile nearest where they would stand (their style's spot, else a ring round the
        // waypoint), with open floor below them to talk from, one to a tile.
        static List<(CharacterDefinition Character, Vec2 At)> Stand(TileMap map, int cx, int cy, PlaceDefinition place, List<CharacterDefinition> living,
            List<(int X, int Y)> wanted)
        {
            int r = place.Radius;
            var taken = new HashSet<(int, int)>();
            var people = new List<(CharacterDefinition, Vec2)>();
            bool Open(int x, int y) => IsInside(map, x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Waypoint && map[x, y] != TileType.Door;
            for (int i = 0; i < living.Count; i++)
            {
                (int X, int Y) aim;
                if (i < wanted.Count) aim = wanted[i];
                else
                {
                    int lap = i / 8;
                    var way = Ways[(4 + (i % 8) * 2 + lap) % Ways.Length];
                    int dist = System.Math.Max(2, r - 3 - lap);
                    aim = (cx + way.X * dist / 1000, cy + way.Y * dist / 1000);
                }
                (int X, int Y)? found = null;
                for (int ring = 0; ring <= r && found == null; ring++)
                {
                    for (int dy = -ring; dy <= ring && found == null; dy++)
                    {
                        for (int dx = -ring; dx <= ring && found == null; dx++)
                        {
                            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != ring) continue;
                            int x = aim.X + dx, y = aim.Y + dy;
                            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > (r - 1) * (r - 1)) continue;
                            if (taken.Contains((x, y)) || (x == cx && y == cy) || !Open(x, y) || !Open(x, y - 1)) continue;
                            found = (x, y);
                        }
                    }
                }
                var (px, py) = found ?? (cx + (i % 2 == 0 ? 1 : -1), cy + 1 + i / 2);
                taken.Add((px, py));
                people.Add((living[i], TileMap.Center(px, py)));
            }
            return people;
        }

        // Where each person who goes about walks by day (PlaceSite.PersonAt): from home to a spot two and a half to six
        // tiles off, on to another as far, and home, each leg a straight line over open floor, away from the other
        // people's homes. Spots are tried in an order fixed by the person and the tile, so it is the same every time.
        static Dictionary<string, IReadOnlyList<Vec2>> Rounds(TileMap map, int cx, int cy, PlaceDefinition place,
            List<(CharacterDefinition Character, Vec2 At)> people)
        {
            var rounds = new Dictionary<string, IReadOnlyList<Vec2>>();
            int r = place.Radius;
            bool Walkable(Vec2 p) => !map.IsSolid(TileMap.Cell(p.X), TileMap.Cell(p.Y)) && map[TileMap.Cell(p.X), TileMap.Cell(p.Y)] != TileType.Water;
            bool Clear(Vec2 a, Vec2 b)
            {
                float length = Vec2.Distance(a, b);
                for (float along = 0; along <= length; along += 0.25f)
                {
                    var p = Vec2.Lerp(a, b, length < 1e-3f ? 0 : along / length);
                    if (!Walkable(p) || !Walkable(p + new Vec2(0.3f, 0)) || !Walkable(p + new Vec2(-0.3f, 0))) return false;
                }
                return true;
            }
            var floor = new List<Vec2>();
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > (r - 1) * (r - 1) || !IsInside(map, x, y)) continue;
                    if (map.IsSolid(x, y) || map[x, y] == TileType.Waypoint || map[x, y] == TileType.Water || map.IsSolid(x, y - 1)) continue;
                    floor.Add(TileMap.Center(x, y));
                }
            foreach (var (character, home) in people)
            {
                if (character.Walk == "stay") continue;
                int seed = character.Index * 7919 + 17;
                var order = floor.OrderBy(p => ((int)p.X * 73856093 ^ (int)p.Y * 19349663 ^ seed) & 0x7fffffff).ToList();
                bool AwayFromOthers(Vec2 p) => people.All(o => o.Character == character || Vec2.Distance(o.At, p) >= 1.5f);
                Vec2? first = order.Cast<Vec2?>().FirstOrDefault(p => Vec2.Distance(p.Value, home) is float d && d >= 2.5f && d <= 6
                    && AwayFromOthers(p.Value) && Clear(home, p.Value));
                if (first is not Vec2 a) continue;
                Vec2? second = order.Cast<Vec2?>().FirstOrDefault(p => Vec2.Distance(p.Value, a) is float d && d >= 2.5f && d <= 6
                    && Vec2.Distance(p.Value, home) >= 2 && AwayFromOthers(p.Value) && Clear(a, p.Value) && Clear(p.Value, home));
                rounds[character.Id] = second is Vec2 b ? new[] { home, a, b } : new[] { home, a };
            }
            return rounds;
        }

        // Whatever the dressing shut off is opened again: each person (and the tile below them) and each road that
        // comes up to the plaza, walked to from the waypoint over open tiles (doors too); one that cannot be gets a way
        // three wide straight to the middle, the dressing in it taken away.
        static void Open(TileMap map, int cx, int cy, PlaceDefinition place, IEnumerable<(int X, int Y)> people)
        {
            int r = place.Radius;
            int reach = r + 2;
            bool Near(int x, int y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= reach * reach;
            var targets = new List<(int X, int Y)>();
            foreach (var (x, y) in people)
            {
                targets.Add((x, y));
                targets.Add((x, y - 1));
            }
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (IsInside(map, x, y) && map[x, y] == TileType.Road && d > r * r && Near(x, y)) targets.Add((x, y));
                }
            }
            foreach (var target in targets)
            {
                if (Reached(map, cx, cy, Near).Contains(target)) continue;
                foreach (var (x, y) in Line(target.X, target.Y, cx, cy))
                {
                    for (int ty = y - TunnelHalfWidth; ty <= y + TunnelHalfWidth; ty++)
                    {
                        for (int tx = x - TunnelHalfWidth; tx <= x + TunnelHalfWidth; tx++)
                        {
                            if (!IsInside(map, tx, ty) || map[tx, ty] == TileType.Waypoint) continue;
                            if ((tx - cx) * (tx - cx) + (ty - cy) * (ty - cy) <= r * r && map.IsSolid(tx, ty)) map[tx, ty] = place.Floor;
                        }
                    }
                }
            }
        }

        // The open tiles (doors too) walked to from the middle without leaving the area.
        static HashSet<(int X, int Y)> Reached(TileMap map, int cx, int cy, System.Func<int, int, bool> within)
        {
            var seen = new HashSet<(int X, int Y)> { (cx, cy) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((cx, cy));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!IsInside(map, nx, ny) || !within(nx, ny) || map.IsSolid(nx, ny) || !seen.Add((nx, ny))) continue;
                    queue.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        // The tiles of a straight line, both ends in (Bresenham's).
        static IEnumerable<(int X, int Y)> Line(int fromX, int fromY, int toX, int toY)
        {
            int dx = System.Math.Abs(toX - fromX);
            int dy = -System.Math.Abs(toY - fromY);
            int stepX = fromX < toX ? 1 : -1;
            int stepY = fromY < toY ? 1 : -1;
            int error = dx + dy;
            int x = fromX, y = fromY;
            while (true)
            {
                yield return (x, y);
                if (x == toX && y == toY) yield break;
                int twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    x += stepX;
                }
                if (twice <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        static GeneratedWorld GenerateCaves(ulong seed, int width, int height)
        {
            var random = new GameRandom(seed, Stream);
            var map = new TileMap(width, height);
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    map[x, y] = random.Next(100) < WallPercent ? TileType.Wall : TileType.Floor;
                }
            }

            for (int pass = 0; pass < SmoothingPasses; pass++) map = Smooth(map);

            int spawnX = width / 2;
            int spawnY = height / 2;
            ClearDisc(map, spawnX, spawnY, SpawnClearing);
            // Distances grow with the map's width, counts with its area.
            int k = ScaleOf(width, height), area = k * k;

            var (offsetX, offsetY) = ArenaOffsets[random.Next(ArenaOffsets.Length)];
            offsetX *= k;
            offsetY *= k;
            int arenaX = Clamp(spawnX + offsetX, ArenaRadius + 2, width - ArenaRadius - 3);
            int arenaY = Clamp(spawnY + offsetY, ArenaRadius + 2, height - ArenaRadius - 3);
            ClearDisc(map, arenaX, arenaY, ArenaRadius);
            CarveTunnel(map, spawnX, spawnY, arenaX, arenaY);

            FillUnreachable(map, spawnX, spawnY);
            var ore = new GameRandom(seed, OreStream);
            PlaceVeins(map, ore, TileType.CopperOre, CopperVeins * area, spawnX, spawnY, 0);
            PlaceVeins(map, ore, TileType.IronOre, IronVeins * area, spawnX, spawnY, IronDistance * k);

            // The deep arena: a far corner on the first arena's side, so the way there runs through the first.
            var deep = new GameRandom(seed, DeepStream);
            var corners = new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) }
                .Where(c => c.Item1 * offsetX + c.Item2 * offsetY > 0)
                .ToArray();
            var (cornerX, cornerY) = corners[deep.Next(corners.Length)];
            int deepX = Clamp(spawnX + cornerX * DeepArenaOffset * k, ArenaRadius + 2, width - ArenaRadius - 3);
            int deepY = Clamp(spawnY + cornerY * DeepArenaOffset * k, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, arenaX, arenaY, deepX, deepY);
            ClearDisc(map, deepX, deepY, ArenaRadius);
            PlaceVeins(map, new GameRandom(seed, MithrilStream), TileType.MithrilOre, MithrilVeins * area, spawnX, spawnY, MithrilDistance * k);
            bool mushroomFirst = new GameRandom(seed, BiomeStream).Next(2) == 0;
            var abyss = new GameRandom(seed, AbyssStream);
            // One of the two corners beside both the crystal caverns and the lava fields.
            var (abyssCornerX, abyssCornerY) = abyss.Next(2) == 0 ? (cornerX, -cornerY) : (-cornerX, cornerY);
            // The lake: the other of those two corners.
            var (lakeCornerX, lakeCornerY) = abyssCornerX == cornerX ? (-cornerX, cornerY) : (cornerX, -cornerY);
            // The sand arena (carved last): of the two flanks of the crystal quarter, the one further from the first arena.
            var sand = new GameRandom(seed, SandStream);
            var flanks = new[]
            {
                (spawnX + cornerX * SandArenaOffset.Along * k, spawnY + cornerY * SandArenaOffset.Across * k),
                (spawnX + cornerX * SandArenaOffset.Across * k, spawnY + cornerY * SandArenaOffset.Along * k),
            };
            int Far((int X, int Y) f) => (f.X - arenaX) * (f.X - arenaX) + (f.Y - arenaY) * (f.Y - arenaY);
            var (sandX, sandY) = Far(flanks[0]) == Far(flanks[1]) ? flanks[sand.Next(2)] : Far(flanks[0]) > Far(flanks[1]) ? flanks[0] : flanks[1];
            sandX = Clamp(sandX, ArenaRadius + 2, width - ArenaRadius - 3);
            sandY = Clamp(sandY, ArenaRadius + 2, height - ArenaRadius - 3);
            var biomes = BiomeMap.Make(seed, width, height, spawnX, spawnY, (cornerX, cornerY), mushroomFirst, (abyssCornerX, abyssCornerY),
                (lakeCornerX, lakeCornerY), (sandX, sandY));

            var lava = new GameRandom(seed, LavaStream);
            int lavaX = Clamp(spawnX - cornerX * DeepArenaOffset * k, ArenaRadius + 2, width - ArenaRadius - 3);
            int lavaY = Clamp(spawnY - cornerY * DeepArenaOffset * k, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, spawnX, spawnY, lavaX, lavaY);
            ClearDisc(map, lavaX, lavaY, ArenaRadius);
            bool InLava(int x, int y, int distance)
            {
                int dx = x - spawnX;
                int dy = y - spawnY;
                return biomes[x, y] == Biome.Lava && dx * dx + dy * dy >= distance * distance;
            }
            bool NearArena(int x, int y) =>
                new[] { (arenaX, arenaY), (deepX, deepY), (lavaX, lavaY) }
                    .Any(a => (x - a.Item1) * (x - a.Item1) + (y - a.Item2) * (y - a.Item2) <= (ArenaRadius + 2) * (ArenaRadius + 2));
            PlaceVeins(map, lava, TileType.ObsidianOre, ObsidianVeins * area, (x, y) => map[x, y] == TileType.Wall && InLava(x, y, ObsidianDistance * k));
            PlaceLava(map, lava, LavaPools * area, (x, y) => map[x, y] == TileType.Floor && InLava(x, y, LavaPoolDistance * k) && !NearArena(x, y));

            int abyssX = Clamp(spawnX + abyssCornerX * DeepArenaOffset * k, ArenaRadius + 2, width - ArenaRadius - 3);
            int abyssY = Clamp(spawnY + abyssCornerY * DeepArenaOffset * k, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, lavaX, lavaY, abyssX, abyssY);
            ClearDisc(map, abyssX, abyssY, ArenaRadius);
            PlaceVeins(map, abyss, TileType.VoidOre, VoidVeins * area, (x, y) => map[x, y] == TileType.Wall && biomes[x, y] == Biome.Abyss
                && (x - abyssX) * (x - abyssX) + (y - abyssY) * (y - abyssY) > (ArenaRadius + 2) * (ArenaRadius + 2));
            var arenas = new[] { (arenaX, arenaY), (deepX, deepY), (lavaX, lavaY), (abyssX, abyssY) };
            bool Shore(int x, int y)
            {
                if (map[x, y] != TileType.Wall || biomes[x, y] == Biome.Lava) return false;
                if ((x - spawnX) * (x - spawnX) + (y - spawnY) * (y - spawnY) < WaterDistance * k * WaterDistance * k) return false;
                return !arenas.Any(a => (x - a.Item1) * (x - a.Item1) + (y - a.Item2) * (y - a.Item2) <= (ArenaRadius + 3) * (ArenaRadius + 3));
            }
            PlaceWater(map, new GameRandom(seed, WaterStream), WaterPools * area, Shore);

            var lake = new GameRandom(seed, LakeStream);
            int lakeX = Clamp(spawnX + lakeCornerX * DeepArenaOffset * k, ArenaRadius + 2, width - ArenaRadius - 3);
            int lakeY = Clamp(spawnY + lakeCornerY * DeepArenaOffset * k, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, deepX, deepY, lakeX, lakeY);
            ClearDisc(map, lakeX, lakeY, ArenaRadius);
            // Pools laid before the arena was there keep their distance from it, as all pools do from arenas.
            for (int y = lakeY - ArenaRadius - 3; y <= lakeY + ArenaRadius + 3; y++)
            {
                for (int x = lakeX - ArenaRadius - 3; x <= lakeX + ArenaRadius + 3; x++)
                {
                    if (IsInside(map, x, y) && map[x, y] == TileType.Water && (x - lakeX) * (x - lakeX) + (y - lakeY) * (y - lakeY) <= (ArenaRadius + 3) * (ArenaRadius + 3))
                        map[x, y] = TileType.Wall;
                }
            }
            bool LakeShore(int x, int y) => map[x, y] == TileType.Wall && biomes[x, y] == Biome.Lake
                && (x - lakeX) * (x - lakeX) + (y - lakeY) * (y - lakeY) > (ArenaRadius + 3) * (ArenaRadius + 3);
            // Twice over: a lake is mostly water at its edges.
            PlaceWater(map, lake, WaterPools * area, LakeShore);
            PlaceWater(map, lake, WaterPools * area, LakeShore);

            CarveTunnel(map, arenaX, arenaY, sandX, sandY);
            ClearDisc(map, sandX, sandY, ArenaRadius);
            for (int y = sandY - ArenaRadius - 3; y <= sandY + ArenaRadius + 3; y++)
            {
                for (int x = sandX - ArenaRadius - 3; x <= sandX + ArenaRadius + 3; x++)
                {
                    if (IsInside(map, x, y) && map[x, y] == TileType.Water && (x - sandX) * (x - sandX) + (y - sandY) * (y - sandY) <= (ArenaRadius + 3) * (ArenaRadius + 3))
                        map[x, y] = TileType.Wall;
                }
            }
            // The sand caves are dry: pools laid in them earlier are rock again.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map[x, y] == TileType.Water && biomes[x, y] == Biome.Sand) map[x, y] = TileType.Wall;
                }
            }
            return new GeneratedWorld(map, spawnX, spawnY, arenaX, arenaY, ArenaRadius, deepX, deepY, biomes, (lavaX, lavaY), (abyssX, abyssY), (lakeX, lakeY),
                (sandX, sandY));
        }

        static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        // Small patches of ore in plain rock: a random wall tile at least minDistance from the spawn, and each wall
        // around it turned to ore at VeinPercent odds.
        static void PlaceVeins(TileMap map, GameRandom random, TileType ore, int veins, int spawnX, int spawnY, int minDistance)
        {
            for (int vein = 0; vein < veins; vein++)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int x = 1 + random.Next(map.Width - 2);
                    int y = 1 + random.Next(map.Height - 2);
                    int dx = x - spawnX;
                    int dy = y - spawnY;
                    if (map[x, y] != TileType.Wall || dx * dx + dy * dy < minDistance * minDistance) continue;
                    for (int vy = y - 1; vy <= y + 1; vy++)
                    {
                        for (int vx = x - 1; vx <= x + 1; vx++)
                        {
                            if (IsInside(map, vx, vy) && map[vx, vy] == TileType.Wall && (vx == x && vy == y || random.Next(100) < VeinPercent))
                                map[vx, vy] = ore;
                        }
                    }
                    break;
                }
            }
        }

        // Ore veins only where the test allows: a centre tile that passes it, and walls around it that pass it.
        static void PlaceVeins(TileMap map, GameRandom random, TileType ore, int veins, System.Func<int, int, bool> allowed)
        {
            for (int vein = 0; vein < veins; vein++)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    int x = 1 + random.Next(map.Width - 2);
                    int y = 1 + random.Next(map.Height - 2);
                    if (!allowed(x, y)) continue;
                    for (int vy = y - 1; vy <= y + 1; vy++)
                    {
                        for (int vx = x - 1; vx <= x + 1; vx++)
                        {
                            if (IsInside(map, vx, vy) && allowed(vx, vy) && (vx == x && vy == y || random.Next(100) < VeinPercent)) map[vx, vy] = ore;
                        }
                    }
                    break;
                }
            }
        }

        // Pools of lava on open floor: a centre and some of the floor around it, two rings out.
        // Each pool starts in rock right beside a floor tile and spreads a little into the rock around it.
        static void PlaceWater(TileMap map, GameRandom random, int pools, System.Func<int, int, bool> allowed)
        {
            for (int pool = 0; pool < pools; pool++)
            {
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    int x = 2 + random.Next(map.Width - 4);
                    int y = 2 + random.Next(map.Height - 4);
                    bool shore = map[x + 1, y] == TileType.Floor || map[x - 1, y] == TileType.Floor || map[x, y + 1] == TileType.Floor || map[x, y - 1] == TileType.Floor;
                    if (!shore || !allowed(x, y)) continue;
                    for (int vy = y - 2; vy <= y + 2; vy++)
                    {
                        for (int vx = x - 2; vx <= x + 2; vx++)
                        {
                            int ring = System.Math.Max(System.Math.Abs(vx - x), System.Math.Abs(vy - y));
                            int odds = ring == 0 ? 100 : ring == 1 ? 70 : 30;
                            if (IsInside(map, vx, vy) && allowed(vx, vy) && random.Next(100) < odds) map[vx, vy] = TileType.Water;
                        }
                    }
                    break;
                }
            }
        }

        static void PlaceLava(TileMap map, GameRandom random, int pools, System.Func<int, int, bool> allowed)
        {
            for (int pool = 0; pool < pools; pool++)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    int x = 2 + random.Next(map.Width - 4);
                    int y = 2 + random.Next(map.Height - 4);
                    if (!allowed(x, y)) continue;
                    for (int vy = y - 2; vy <= y + 2; vy++)
                    {
                        for (int vx = x - 2; vx <= x + 2; vx++)
                        {
                            int ring = System.Math.Max(System.Math.Abs(vx - x), System.Math.Abs(vy - y));
                            int odds = ring == 0 ? 100 : ring == 1 ? 60 : 25;
                            if (IsInside(map, vx, vy) && allowed(vx, vy) && random.Next(100) < odds) map[vx, vy] = TileType.Lava;
                        }
                    }
                    break;
                }
            }
        }

        static void ClearDisc(TileMap map, int centerX, int centerY, int radius)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius && IsInside(map, x, y)) map[x, y] = TileType.Floor;
                }
            }
        }

        // A straight tunnel three tiles wide, stepping along the longer axis (Bresenham's line).
        static void CarveTunnel(TileMap map, int fromX, int fromY, int toX, int toY)
        {
            int dx = System.Math.Abs(toX - fromX);
            int dy = -System.Math.Abs(toY - fromY);
            int stepX = fromX < toX ? 1 : -1;
            int stepY = fromY < toY ? 1 : -1;
            int error = dx + dy;
            int x = fromX;
            int y = fromY;
            while (true)
            {
                for (int ty = y - TunnelHalfWidth; ty <= y + TunnelHalfWidth; ty++)
                {
                    for (int tx = x - TunnelHalfWidth; tx <= x + TunnelHalfWidth; tx++)
                    {
                        if (IsInside(map, tx, ty)) map[tx, ty] = TileType.Floor;
                    }
                }
                if (x == toX && y == toY) return;
                int doubled = 2 * error;
                if (doubled >= dy)
                {
                    error += dy;
                    x += stepX;
                }
                if (doubled <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        static bool IsInside(TileMap map, int x, int y) => x > 0 && y > 0 && x < map.Width - 1 && y < map.Height - 1;

        // A tile becomes wall when most of its eight neighbours are walls and floor when most are not.
        static TileMap Smooth(TileMap map)
        {
            var next = new TileMap(map.Width, map.Height);
            for (int y = 1; y < map.Height - 1; y++)
            {
                for (int x = 1; x < map.Width - 1; x++)
                {
                    int walls = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx != 0 || dy != 0) && map.IsSolid(x + dx, y + dy)) walls++;
                        }
                    }
                    next[x, y] = walls > 4 ? TileType.Wall : walls < 4 ? TileType.Floor : map[x, y];
                }
            }
            return next;
        }

        static void FillUnreachable(TileMap map, int startX, int startY)
        {
            var reached = new bool[map.Width * map.Height];
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));
            reached[startY * map.Width + startX] = true;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (map.IsSolid(nx, ny) || reached[ny * map.Width + nx]) continue;
                    reached[ny * map.Width + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (!reached[y * map.Width + x]) map[x, y] = TileType.Wall;
                }
            }
        }
    }
}
