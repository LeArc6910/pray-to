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
        // nearest the place its road comes from that keeps clear of the arenas and the other places; lamps round the
        // edge, a waypoint in the middle, its people on a ring inside. Then the roads, three wide and paved, bent once
        // to keep out of the arenas, never laid over a plaza.
        static List<PlaceSite> CarvePlaces(GeneratedWorld world, ulong seed, StoryData story)
        {
            var map = world.Map;
            var random = new GameRandom(seed, PlaceStream);
            var arenas = world.Arenas.Select(a => (X: TileMap.Cell(a.X), Y: TileMap.Cell(a.Y))).ToList();
            var sites = new List<PlaceSite>();
            int Square(int dx, int dy) => dx * dx + dy * dy;
            foreach (var place in story.Places)
            {
                int r = place.Radius;
                int cx = world.SpawnX, cy = world.SpawnY;
                if (place.Distance > 0)
                {
                    var from = sites.FirstOrDefault(s => s.Definition.Id == place.Road);
                    int fromX = from?.X ?? world.SpawnX, fromY = from?.Y ?? world.SpawnY;
                    int start = random.Next(Ways.Length);
                    (int X, int Y)? best = null;
                    int bestScore = int.MaxValue;
                    for (int i = 0; i < Ways.Length; i++)
                    {
                        var way = Ways[(start + i) % Ways.Length];
                        int x = Clamp(world.SpawnX + way.X * place.Distance / 1000, r + 3, map.Width - r - 4);
                        int y = Clamp(world.SpawnY + way.Y * place.Distance / 1000, r + 3, map.Height - r - 4);
                        bool clear = arenas.All(a => Square(x - a.X, y - a.Y) >= (ArenaRadius + r + 3) * (ArenaRadius + r + 3))
                            && sites.All(s => Square(x - s.X, y - s.Y) >= (s.Radius + r + 4) * (s.Radius + r + 4));
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
                // Lamps on the four diagonals near the edge, so a place is lit from afar.
                foreach (int w in new[] { 2, 6, 10, 14 })
                {
                    int lx = cx + Ways[w].X * (r - 1) / 1000, ly = cy + Ways[w].Y * (r - 1) / 1000;
                    if (IsInside(map, lx, ly)) map[lx, ly] = TileType.Lamp;
                }
                if (place.Waypoint) map[cx, cy] = TileType.Waypoint;
                var people = new List<(CharacterDefinition, Vec2)>();
                var living = story.Characters.Where(c => c.Place == place).ToList();
                int ring = System.Math.Max(2, r - 3);
                for (int i = 0; i < living.Count; i++)
                {
                    // From the top of the plaza round, evenly; a second lap a little further in when there are many.
                    int lap = i / 8;
                    var way = Ways[(4 + (i % 8) * 2 + lap) % Ways.Length];
                    int dist = System.Math.Max(2, ring - lap);
                    int px = cx + way.X * dist / 1000, py = cy + way.Y * dist / 1000;
                    if (map[px, py] != place.Floor) (px, py) = (cx + (i % 2 == 0 ? 1 : -1), cy + 1 + i / 2);
                    people.Add((living[i], TileMap.Center(px, py)));
                }
                sites.Add(new PlaceSite(place, cx, cy, people));
            }

            bool InPlaza(int x, int y) => sites.Any(s => Square(x - s.X, y - s.Y) <= s.Radius * s.Radius);
            int ArenaTiles(List<(int X, int Y)> line) => line.Count(p => arenas.Any(a => Square(p.X - a.X, p.Y - a.Y) <= (ArenaRadius + 2) * (ArenaRadius + 2)));
            foreach (var site in sites)
            {
                var from = sites.FirstOrDefault(s => s.Definition.Id == site.Definition.Road);
                if (from == null || from == site) continue;
                List<(int X, int Y)> path = null;
                int fewest = int.MaxValue;
                foreach (int bend in new[] { 0, 250, -250, 450, -450 })
                {
                    int midX = (from.X + site.X) / 2 - (site.Y - from.Y) * bend / 1000;
                    int midY = (from.Y + site.Y) / 2 + (site.X - from.X) * bend / 1000;
                    midX = Clamp(midX, 2, map.Width - 3);
                    midY = Clamp(midY, 2, map.Height - 3);
                    var line = Line(from.X, from.Y, midX, midY).Concat(Line(midX, midY, site.X, site.Y)).ToList();
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
            return sites;
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
