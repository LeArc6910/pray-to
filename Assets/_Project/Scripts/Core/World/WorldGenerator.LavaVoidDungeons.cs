using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static partial class WorldGenerator
    {
        // The generator's seventh version (2026-11-25, Docs/BiomeRework.md 묶음 B4): version 6 with the lava fields' steam
        // vents and the abyss's cracked plates in their rooms (LayLavaVoid, last but the pocket), and the lairs of the two
        // biomes' mid-bosses (the ember smith, the gate warden; LayCellars). A world made at version 6 or before keeps its
        // map and its lairs as made (their hashes are pinned in B4bTests).
        public const int LavaVoidVersion = 7;

        // The vents' and the plates' own random stream, and the two biomes' lairs' (so the lairs of the biomes before
        // them stay where version 6 put them).
        public const ulong LavaVoidStream = 91;
        public const ulong LavaVoidLairStream = 92;
        // A pack room of the lava fields has a steam vent for about every this many tiles of its open floor (at least
        // one, at most VentsMost), VentGap apart at least.
        const int VentFloor = 70;
        const int VentsMost = 5;
        const int VentGap = 4;
        // A room of the abyss has a patch or two of cracked plates (up to PlatesMost tiles each) in its outer part.
        const int PlatePatchesMost = 3;
        const int PlatesMost = 9;
        // Nothing of either this near the spawn, a place (and its edge) or an arena, or within a tile of a way.
        const int LavaVoidClear = 3;

        static bool LavaVoidLairs(Biome biome) => biome == Biome.Lava || biome == Biome.Abyss;

        static void LayLavaVoid(GeneratedWorld world, ulong seed)
        {
            var map = world.Map;
            var rooms = world.Rooms;
            if (rooms == null) return;
            var random = new GameRandom(seed, LavaVoidStream);
            var spawn = world.Spawn;
            var places = (world.Places ?? new List<PlaceSite>()).Select(p => (Center: p.Center, Reach: p.Radius + LavaVoidClear)).ToList();
            var arenas = world.Arenas.Select(a => (Center: a, Reach: (float)ArenaRadius + LavaVoidClear)).ToList();
            bool Kept(int x, int y)
            {
                var at = TileMap.Center(x, y);
                if (Vec2.Distance(at, spawn) <= 6 + LavaVoidClear) return true;
                foreach (var p in places) if (Vec2.Distance(at, p.Center) <= p.Reach) return true;
                foreach (var a in arenas) if (Vec2.Distance(at, a.Center) <= a.Reach) return true;
                return NearWay(map, x, y, 1);
            }
            bool Open(int x, int y) => map.InBounds(x, y) && map[x, y] == TileType.Floor && !Kept(x, y);

            foreach (var room in rooms.Nodes)
            {
                if (room.Kind != RoomKind.Room || room.Biome != Biome.Lava && room.Biome != Biome.Abyss) continue;
                // The room's open floor, in a fixed order.
                var floor = new List<(int X, int Y)>();
                for (int y = room.Y - room.Radius; y <= room.Y + room.Radius; y++)
                    for (int x = room.X - room.Radius; x <= room.X + room.Radius; x++)
                        if (Vec2.Distance(TileMap.Center(x, y), room.Center) <= room.Radius && Open(x, y)) floor.Add((x, y));
                if (floor.Count < 12) continue;
                if (room.Biome == Biome.Lava) LayVents(map, floor, random);
                else LayPlates(map, room, floor, random);
            }
        }

        // Vents on open floor with floor all round them (a vent is a spot of the floor, not a wall's foot).
        static void LayVents(TileMap map, List<(int X, int Y)> floor, GameRandom random)
        {
            int wanted = Math.Clamp(floor.Count / VentFloor, 1, VentsMost);
            var laid = new List<(int X, int Y)>();
            for (int attempt = 0; attempt < wanted * 8 && laid.Count < wanted; attempt++)
            {
                var (x, y) = floor[random.Next(floor.Count)];
                if (map[x, y] != TileType.Floor || !OpenAround(map, x, y, 1)) continue;
                if (laid.Any(v => Math.Abs(v.X - x) < VentGap && Math.Abs(v.Y - y) < VentGap)) continue;
                map[x, y] = TileType.SteamVent;
                laid.Add((x, y));
            }
        }

        // Patches of cracked plates in the outer part of the room (where the floating ground breaks up), each grown from
        // a tile over the floor beside it.
        static void LayPlates(TileMap map, RoomNode room, List<(int X, int Y)> floor, GameRandom random)
        {
            var open = new HashSet<(int, int)>(floor);
            var rim = floor.Where(t => Vec2.Distance(TileMap.Center(t.X, t.Y), room.Center) >= room.Radius * 0.45f).ToList();
            if (rim.Count == 0) return;
            int patches = 1 + random.Next(PlatePatchesMost);
            for (int p = 0; p < patches; p++)
            {
                var (sx, sy) = rim[random.Next(rim.Count)];
                if (map[sx, sy] != TileType.Floor) continue;
                int size = 4 + random.Next(PlatesMost - 3);
                var patch = new List<(int X, int Y)> { (sx, sy) };
                map[sx, sy] = TileType.CrackedPlate;
                for (int grow = 0; grow < size * 4 && patch.Count < size; grow++)
                {
                    var (bx, by) = patch[random.Next(patch.Count)];
                    int way = random.Next(4);
                    int nx = bx + (way == 0 ? 1 : way == 1 ? -1 : 0), ny = by + (way == 2 ? 1 : way == 3 ? -1 : 0);
                    if (!open.Contains((nx, ny)) || map[nx, ny] != TileType.Floor) continue;
                    map[nx, ny] = TileType.CrackedPlate;
                    patch.Add((nx, ny));
                }
            }
        }
    }
}
