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
            Arenas = arenas;
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
        // have ArenaRadius.
        public IReadOnlyList<Vec2> Arenas { get; }
        // Which kind of cave each tile is in (Docs/Biomes.md).
        public BiomeMap Biomes { get; }
        // The places of the story (Docs/Story.md), in sheet order; none when made without the sheets.
        public IReadOnlyList<PlaceSite> Places { get; internal set; } = new List<PlaceSite>();
    }

    // Where a place was carved: its middle (the waypoint), its plaza's radius, and where its people stand.
    public sealed class PlaceSite
    {
        internal PlaceSite(PlaceDefinition definition, int x, int y, IReadOnlyList<(CharacterDefinition Character, Vec2 At)> people)
        {
            Definition = definition;
            X = x;
            Y = y;
            People = people;
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
        const ulong Stream = 1;
        const int WallPercent = 45;
        const int SmoothingPasses = 5;
        const int SpawnClearing = 5;
        public const int ArenaRadius = 10;
        const int ArenaDistance = 38;
        const int TunnelHalfWidth = 1;
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
        // Sixteen ways round, as thousandths (integers only, so every machine carves the same places).
        static readonly (int X, int Y)[] Ways =
        {
            (1000, 0), (924, 383), (707, 707), (383, 924), (0, 1000), (-383, 924), (-707, 707), (-924, 383),
            (-1000, 0), (-924, -383), (-707, -707), (-383, -924), (0, -1000), (383, -924), (707, -707), (924, -383),
        };

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
            foreach (var place in story.Places)
            {
                int r = place.Radius;
                int cx = world.SpawnX, cy = world.SpawnY;
                if (place.Distance > 0)
                {
                    var from = spots.FirstOrDefault(s => s.Place.Id == place.Road);
                    int fromX = from.Place != null ? from.X : world.SpawnX, fromY = from.Place != null ? from.Y : world.SpawnY;
                    int start = random.Next(Ways.Length);
                    (int X, int Y)? best = null;
                    int bestScore = int.MaxValue;
                    for (int i = 0; i < Ways.Length; i++)
                    {
                        var way = Ways[(start + i) % Ways.Length];
                        int x = Clamp(world.SpawnX + way.X * place.Distance / 1000, r + 3, map.Width - r - 4);
                        int y = Clamp(world.SpawnY + way.Y * place.Distance / 1000, r + 3, map.Height - r - 4);
                        bool clear = arenas.All(a => Square(x - a.X, y - a.Y) >= (ArenaRadius + r + 3) * (ArenaRadius + r + 3))
                            && spots.All(s => Square(x - s.X, y - s.Y) >= (s.Place.Radius + r + 4) * (s.Place.Radius + r + 4));
                        // Clear ways first, the one nearest where its road comes from; crowded ones only when no way is clear.
                        int score = Square(x - fromX, y - fromY) + (clear ? 0 : 1 << 24);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = (x, y);
                        }
                    }
                    (cx, cy) = best.Value;
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
            foreach (var spot in spots)
            {
                var from = spots.FirstOrDefault(s => s.Place.Id == spot.Place.Road);
                if (from.Place == null || from.Place == spot.Place) continue;
                List<(int X, int Y)> path = null;
                int fewest = int.MaxValue;
                foreach (int bend in new[] { 0, 250, -250, 450, -450 })
                {
                    int midX = (from.X + spot.X) / 2 - (spot.Y - from.Y) * bend / 1000;
                    int midY = (from.Y + spot.Y) / 2 + (spot.X - from.X) * bend / 1000;
                    midX = Clamp(midX, 2, map.Width - 3);
                    midY = Clamp(midY, 2, map.Height - 3);
                    var line = Line(from.X, from.Y, midX, midY).Concat(Line(midX, midY, spot.X, spot.Y)).ToList();
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
            }

            var sites = new List<PlaceSite>();
            foreach (var (place, cx, cy) in spots)
            {
                var wanted = Decorate(map, cx, cy, place);
                var people = Stand(map, cx, cy, place, story.Characters.Where(c => c.Place == place).ToList(), wanted);
                Open(map, cx, cy, place, people.Select(p => (TileMap.Cell(p.Item2.X), TileMap.Cell(p.Item2.Y))));
                Tidy(map, cx, cy, place);
                sites.Add(new PlaceSite(place, cx, cy, people));
            }
            return sites;
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
                    var basins = new[] { (5, -3), (5, -5), (5, 1), (4, 5) };
                    PutPair(TileType.BigFountain, true, basins);
                    PutRow(TileType.Hedge, new[] { (6, 1), (7, 1) }, (0, 0), (0, 1), (0, -1), (0, 2), (0, -2), (-1, 3));
                    PutSmall(TileType.RoyalBanner, (5, 4), (4, 4), (6, 4), (5, 5), (5, 3));
                    PutSmall(TileType.Flowerbed, (5, -6), (4, -6), (6, -5), (4, -5), (6, -4), (2, -4), (2, -3), (6, -1));
                    // Lamp posts beside the fountains: on the carpet side, or the other side where a gate is in the way.
                    foreach (var (fx, fy) in basins)
                    {
                        if (map[cx + fx, cy + fy] != TileType.BigFountain) continue;
                        PutSmall(TileType.LampPost, (fx - 1, fy), (fx + 2, fy), (fx - 1, fy + 1), (fx + 2, fy + 1));
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

            var (offsetX, offsetY) = ArenaOffsets[random.Next(ArenaOffsets.Length)];
            int arenaX = Clamp(spawnX + offsetX, ArenaRadius + 2, width - ArenaRadius - 3);
            int arenaY = Clamp(spawnY + offsetY, ArenaRadius + 2, height - ArenaRadius - 3);
            ClearDisc(map, arenaX, arenaY, ArenaRadius);
            CarveTunnel(map, spawnX, spawnY, arenaX, arenaY);

            FillUnreachable(map, spawnX, spawnY);
            var ore = new GameRandom(seed, OreStream);
            PlaceVeins(map, ore, TileType.CopperOre, CopperVeins, spawnX, spawnY, 0);
            PlaceVeins(map, ore, TileType.IronOre, IronVeins, spawnX, spawnY, IronDistance);

            // The deep arena: a far corner on the first arena's side, so the way there runs through the first.
            var deep = new GameRandom(seed, DeepStream);
            var corners = new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) }
                .Where(c => c.Item1 * offsetX + c.Item2 * offsetY > 0)
                .ToArray();
            var (cornerX, cornerY) = corners[deep.Next(corners.Length)];
            int deepX = Clamp(spawnX + cornerX * DeepArenaOffset, ArenaRadius + 2, width - ArenaRadius - 3);
            int deepY = Clamp(spawnY + cornerY * DeepArenaOffset, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, arenaX, arenaY, deepX, deepY);
            ClearDisc(map, deepX, deepY, ArenaRadius);
            PlaceVeins(map, new GameRandom(seed, MithrilStream), TileType.MithrilOre, MithrilVeins, spawnX, spawnY, MithrilDistance);
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
                (spawnX + cornerX * SandArenaOffset.Along, spawnY + cornerY * SandArenaOffset.Across),
                (spawnX + cornerX * SandArenaOffset.Across, spawnY + cornerY * SandArenaOffset.Along),
            };
            int Far((int X, int Y) f) => (f.X - arenaX) * (f.X - arenaX) + (f.Y - arenaY) * (f.Y - arenaY);
            var (sandX, sandY) = Far(flanks[0]) == Far(flanks[1]) ? flanks[sand.Next(2)] : Far(flanks[0]) > Far(flanks[1]) ? flanks[0] : flanks[1];
            sandX = Clamp(sandX, ArenaRadius + 2, width - ArenaRadius - 3);
            sandY = Clamp(sandY, ArenaRadius + 2, height - ArenaRadius - 3);
            var biomes = BiomeMap.Make(seed, width, height, spawnX, spawnY, (cornerX, cornerY), mushroomFirst, (abyssCornerX, abyssCornerY),
                (lakeCornerX, lakeCornerY), (sandX, sandY));

            var lava = new GameRandom(seed, LavaStream);
            int lavaX = Clamp(spawnX - cornerX * DeepArenaOffset, ArenaRadius + 2, width - ArenaRadius - 3);
            int lavaY = Clamp(spawnY - cornerY * DeepArenaOffset, ArenaRadius + 2, height - ArenaRadius - 3);
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
            PlaceVeins(map, lava, TileType.ObsidianOre, ObsidianVeins, (x, y) => map[x, y] == TileType.Wall && InLava(x, y, ObsidianDistance));
            PlaceLava(map, lava, (x, y) => map[x, y] == TileType.Floor && InLava(x, y, LavaPoolDistance) && !NearArena(x, y));

            int abyssX = Clamp(spawnX + abyssCornerX * DeepArenaOffset, ArenaRadius + 2, width - ArenaRadius - 3);
            int abyssY = Clamp(spawnY + abyssCornerY * DeepArenaOffset, ArenaRadius + 2, height - ArenaRadius - 3);
            CarveTunnel(map, lavaX, lavaY, abyssX, abyssY);
            ClearDisc(map, abyssX, abyssY, ArenaRadius);
            PlaceVeins(map, abyss, TileType.VoidOre, VoidVeins, (x, y) => map[x, y] == TileType.Wall && biomes[x, y] == Biome.Abyss
                && (x - abyssX) * (x - abyssX) + (y - abyssY) * (y - abyssY) > (ArenaRadius + 2) * (ArenaRadius + 2));
            var arenas = new[] { (arenaX, arenaY), (deepX, deepY), (lavaX, lavaY), (abyssX, abyssY) };
            bool Shore(int x, int y)
            {
                if (map[x, y] != TileType.Wall || biomes[x, y] == Biome.Lava) return false;
                if ((x - spawnX) * (x - spawnX) + (y - spawnY) * (y - spawnY) < WaterDistance * WaterDistance) return false;
                return !arenas.Any(a => (x - a.Item1) * (x - a.Item1) + (y - a.Item2) * (y - a.Item2) <= (ArenaRadius + 3) * (ArenaRadius + 3));
            }
            PlaceWater(map, new GameRandom(seed, WaterStream), Shore);

            var lake = new GameRandom(seed, LakeStream);
            int lakeX = Clamp(spawnX + lakeCornerX * DeepArenaOffset, ArenaRadius + 2, width - ArenaRadius - 3);
            int lakeY = Clamp(spawnY + lakeCornerY * DeepArenaOffset, ArenaRadius + 2, height - ArenaRadius - 3);
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
            PlaceWater(map, lake, LakeShore);
            PlaceWater(map, lake, LakeShore);

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
        static void PlaceWater(TileMap map, GameRandom random, System.Func<int, int, bool> allowed)
        {
            for (int pool = 0; pool < WaterPools; pool++)
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

        static void PlaceLava(TileMap map, GameRandom random, System.Func<int, int, bool> allowed)
        {
            for (int pool = 0; pool < LavaPools; pool++)
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
