using System.Collections.Generic;

namespace Game.Core
{
    public sealed class GeneratedWorld
    {
        public GeneratedWorld(TileMap map, int spawnX, int spawnY, int arenaX, int arenaY, int arenaRadius)
        {
            Map = map;
            SpawnX = spawnX;
            SpawnY = spawnY;
            ArenaX = arenaX;
            ArenaY = arenaY;
            ArenaRadius = arenaRadius;
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

        // The arena lies in one of eight directions from the spawn, about ArenaDistance tiles away.
        static readonly (int X, int Y)[] ArenaOffsets =
        {
            (38, 0), (27, 27), (0, 38), (-27, 27), (-38, 0), (-27, -27), (0, -38), (27, -27),
        };

        public static GeneratedWorld Generate(ulong seed, int width = DefaultSize, int height = DefaultSize)
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
            return new GeneratedWorld(map, spawnX, spawnY, arenaX, arenaY, ArenaRadius);
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
