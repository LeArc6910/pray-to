using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static partial class WorldGenerator
    {
        // The camps' trapdoors and the mid-bosses' lairs (2026-11-24, Docs/BiomeRework.md 묶음 B1c), only in a world of
        // version 4 (LandmarkVersion) and after its props: nothing laid before them moves, and a world of version 3 is
        // as it was. Their own random stream.
        public const ulong CellarStream = 58;
        // Of the camps that hold their band (World's pack rooms), this share has a trapdoor down to a cellar (4-5 지하실).
        public const float CellarShare = 0.3f;
        // The trapdoor stands this far from the camp's mark (its leader) at least and at most.
        const float CellarLeast = 2.5f;
        const float CellarMost = 6;
        // How many lairs a biome with a mid-boss of its own gets at most, at least this many zones' rings from the
        // spawn (World.ZoneRing × the map's scale): the field's XL only there (사람이 정한 것), never on the first
        // boss's way.
        public const int LairsPerBiome = 2;
        const int LairRing = 30;
        // A lair's room holds a square of open floor this wide (MonsterSizes.XlOpenSquare).
        const int LairSquare = 7;

        static void LayCellars(GeneratedWorld world, ulong seed, TerrainData terrain, int version = LandmarkVersion)
        {
            var rooms = world.Rooms;
            var map = world.Map;
            if (rooms == null) return;
            var random = new GameRandom(seed, CellarStream);
            bool ByArena(RoomNode room) => room.Edges.Any(i => rooms.Nodes[rooms.Edges[i].A == room.Index ? rooms.Edges[i].B : rooms.Edges[i].A].Kind == RoomKind.Arena);
            // The rooms that hold a pack in the world (World.SetRooms' pack rooms).
            bool Holds(RoomNode room) => room.Kind == RoomKind.Room && !room.Roadside && !ByArena(room);

            foreach (var room in rooms.Nodes.Where(n => n.Role == RoomRole.Elite && Holds(n)))
            {
                if (random.NextFloat() >= CellarShare) continue;
                var mark = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Elite).Select(m => (X: m.X, Y: m.Y)).DefaultIfEmpty((X: room.AnchorX, Y: room.AnchorY)).First();
                var monsters = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Monster).ToList();
                var spots = new List<(int X, int Y)>();
                int reach = (int)Math.Ceiling(CellarMost);
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int x = mark.X + dx, y = mark.Y + dy;
                        float d = MathF.Sqrt(dx * dx + dy * dy);
                        if (d < CellarLeast || d > CellarMost || Vec2.Distance(TileMap.Center(x, y), room.Center) > room.Radius - 1) continue;
                        if (!OpenAround(map, x, y, 1) || monsters.Any(m => Math.Abs(m.X - x) <= 1 && Math.Abs(m.Y - y) <= 1)) continue;
                        if (NearWay(map, x, y, 2)) continue;
                        spots.Add((x, y));
                    }
                }
                if (spots.Count == 0) continue;
                var (tx, ty) = spots[random.Next(spots.Count)];
                map[tx, ty] = TileType.Trapdoor;
                rooms.CellarList.Add((room.Index, tx, ty));
            }

            // The lairs: the biomes whose gate's dungeon has a mid-boss of its own for master (dungeons.csv boss), of those
            // the world's version lays them in (LairsIn: version 4 the dirt cave and the mushroom forest, version 5 on the
            // ice cave and the sand cave too).
            var dungeons = terrain?.Dungeons ?? DungeonData.None;
            int ring = LairRing * ScaleOf(map.Width, map.Height);
            var spawn = TileMap.Center(world.SpawnX, world.SpawnY);
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var row = dungeons.For(biome);
                if (row == null || !row.Master || row.Biome != biome || !LairsIn(biome, version)) continue;
                // The ice cave's and the sand cave's mid-bosses (version 5) are a zone further out (LairRingsOf): the sand cave
                // holds the first boss's arena in some worlds, and its first hour stays as it was.
                int from = ring * LairRingsOf(biome);
                var fit = rooms.Nodes.Where(n => n.Biome == biome && n.Role == RoomRole.Pack && Holds(n) && Vec2.Distance(n.Center, spawn) >= from
                    && OpenSquare(map, n)).ToList();
                for (int i = 0; i < LairsPerBiome && fit.Count > 0; i++)
                {
                    var pick = fit[random.Next(fit.Count)];
                    rooms.LairList.Add(pick.Index);
                    // Not two lairs side by side.
                    fit.RemoveAll(n => Vec2.Distance(n.Center, pick.Center) < 4 * (pick.Radius + n.Radius));
                }
            }
            rooms.LairList.Sort();
        }

        // Floor on the tile and every tile within reach of it.
        static bool OpenAround(TileMap map, int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                    if (!map.InBounds(x + dx, y + dy) || map[x + dx, y + dy] != TileType.Floor) return false;
            return true;
        }

        static bool NearWay(TileMap map, int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                    if (map.InBounds(x + dx, y + dy) && (Structures.IsWay(map[x + dx, y + dy]) || map[x + dx, y + dy] == TileType.Waypoint)) return true;
            return false;
        }

        // Whether a square of open floor LairSquare wide lies wholly within the room's radius (World.OpenSquareIn's rule).
        static bool OpenSquare(TileMap map, RoomNode room)
        {
            int cx = room.X, cy = room.Y, reach = room.Radius, side = LairSquare;
            for (int y0 = cy - reach; y0 <= cy + reach - side + 1; y0++)
            {
                for (int x0 = cx - reach; x0 <= cx + reach - side + 1; x0++)
                {
                    var middle = new Vec2(x0 + side / 2f, y0 + side / 2f);
                    if (Vec2.Distance(middle, room.Center) + side * 0.5f > room.Radius + 1) continue;
                    bool open = true;
                    for (int y = y0; y < y0 + side && open; y++)
                        for (int x = x0; x < x0 + side && open; x++)
                            if (!map.InBounds(x, y) || map.IsSolid(x, y) || map[x, y] == TileType.Lava || map[x, y] == TileType.Water) open = false;
                    if (open) return true;
                }
            }
            return false;
        }
    }
}
