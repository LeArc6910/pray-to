using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The generated map as pictures, to judge its shape by eye (Docs/WorldRework.md 1단계 확인): the whole map of a few
    // seeds a pixel a tile, for each of the generator's versions, and close-ups of the spawn and of every biome four
    // pixels a tile. Rock is black, floor its biome's colour, ore yellow, water blue, lava orange, roads pale, the
    // places' and arenas' floors red. North is up, as on the game's map.
    //
    //   dotnet run --project DevTools/ArtPreview -- --only map      (out/map/)
    static class MapPreview
    {
        const int CloseUp = 96;
        const int SpawnCloseUp = 160;

        static readonly Rgba Rock = Rgba.Hex("#0B0B0E");
        static readonly Rgba Ore = Rgba.Hex("#E6C02E");
        static readonly Rgba Water = Rgba.Hex("#2F7BE0");
        static readonly Rgba Lava = Rgba.Hex("#FF7A1A");
        static readonly Rgba Road = Rgba.Hex("#F2E6C8");
        static readonly Rgba Built = Rgba.Hex("#E86FA0");
        static readonly Rgba ArenaFloor = Rgba.Hex("#C8443A");
        static readonly Rgba Feature = Rgba.Hex("#6FD08A");

        static Rgba Floor(Biome biome) => biome switch
        {
            Biome.Mushroom => Rgba.Hex("#5C4878"),
            Biome.Ice => Rgba.Hex("#8FBAD6"),
            Biome.Crystal => Rgba.Hex("#7458B8"),
            Biome.Lava => Rgba.Hex("#8E3A26"),
            Biome.Abyss => Rgba.Hex("#33265E"),
            Biome.Lake => Rgba.Hex("#2E5E86"),
            Biome.Sand => Rgba.Hex("#A8935A"),
            _ => Rgba.Hex("#4A3E34"),
        };

        static Rgba Colour(GeneratedWorld world, int x, int y)
        {
            var type = world.Map[x, y];
            switch (type)
            {
                case TileType.Wall: return Rock;
                case TileType.Water: return Water;
                case TileType.Lava: return Lava;
                case TileType.CopperOre:
                case TileType.IronOre:
                case TileType.MithrilOre:
                case TileType.ObsidianOre:
                case TileType.VoidOre: return Ore;
                case TileType.Road: return Road;
                case TileType.MushroomTree: return Feature;
                case TileType.CrackedWall: return Rgba.Hex("#B89A6A");
                case TileType.Grate: return Rgba.Hex("#9AA4B0");
                case TileType.ObsidianWall: return Rgba.Hex("#6A3FA8");
                case TileType.VoidWall: return Rgba.Hex("#C9A8FF");
                case TileType.IceFloor: return Rgba.Hex("#DDF4FF");
                case TileType.Quicksand: return Rgba.Hex("#D9B24A");
                case TileType.Torch: return Rgba.Hex("#FFD23F");
                case TileType.Campfire: return Rgba.Hex("#FF9A3C");
                case TileType.Rail: return Rgba.Hex("#8A6A4A");
                case TileType.RuneFloor: return Rgba.Hex("#5BE0E6");
                case TileType.Cobweb: return Rgba.Hex("#C8C8D0");
                case TileType.Crates: return Rgba.Hex("#FFE27A");
            }
            if (world.Map.IsSolid(x, y) || type == TileType.Waypoint) return Built;
            if (world.ArenaKindAt(TileMap.Center(x, y)) != null) return ArenaFloor;
            return Floor(world.Biomes[x, y]);
        }

        // The tiles from (left, bottom), width by height, each scale pixels across; rooms: the graph drawn over them.
        static PixelImage Picture(GeneratedWorld world, int left, int bottom, int width, int height, int scale, bool rooms = false)
        {
            var image = new PixelImage(width * scale, height * scale);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = world.Map.InBounds(left + x, bottom + y);
                    image.Fill(x * scale, (height - 1 - y) * scale, scale, scale, inside ? Colour(world, left + x, bottom + y) : Rock);
                }
            }
            if (rooms && world.Rooms != null)
            {
                void Dot(float wx, float wy, Rgba colour, int size)
                {
                    int px = (int)((wx - left) * scale), py = (int)((height - (wy - bottom)) * scale);
                    for (int oy = -size; oy <= size; oy++)
                        for (int ox = -size; ox <= size; ox++)
                            if (image.Inside(px + ox, py + oy)) image[px + ox, py + oy] = colour;
                }
                foreach (var edge in world.Rooms.Edges)
                {
                    var a = world.Rooms.Nodes[edge.A];
                    var b = world.Rooms.Nodes[edge.B];
                    int steps = Math.Max(1, (int)(Vec2.Distance(a.Center, b.Center) * scale));
                    for (int i = 0; i <= steps; i++)
                    {
                        var p = Vec2.Lerp(a.Center, b.Center, i / (float)steps);
                        Dot(p.X, p.Y, edge.Loop ? Rgba.Hex("#FFD23F") : Rgba.Hex("#5BE0E6"), 0);
                    }
                }
                foreach (var node in world.Rooms.Nodes)
                    Dot(node.Center.X, node.Center.Y, node.Kind == RoomKind.Arena ? Rgba.Hex("#FF3B30") : RoleColour(node.Role), node.Role == RoomRole.Plain || node.Role == RoomRole.Pack ? Math.Max(1, scale / 2) : scale);
            }
            return image;
        }

        // A room's dot on the graph picture, by what it is for.
        static Rgba RoleColour(RoomRole role) => role switch
        {
            RoomRole.Pack => Rgba.Hex("#E0564A"),
            RoomRole.Ore => Rgba.Hex("#E6C02E"),
            RoomRole.Rest => Rgba.Hex("#FF9A3C"),
            RoomRole.Elite => Rgba.Hex("#FF2BD6"),
            RoomRole.Treasure => Rgba.Hex("#FFF27A"),
            RoomRole.Shrine => Rgba.Hex("#5BE0E6"),
            RoomRole.Hidden => Rgba.Hex("#B89A6A"),
            RoomRole.Locked => Rgba.Hex("#A070FF"),
            RoomRole.Trap => Rgba.Hex("#FF7A1A"),
            RoomRole.Cart => Rgba.Hex("#8A6A4A"),
            RoomRole.Gate => Rgba.Hex("#3CFF8A"),
            RoomRole.Pond => Rgba.Hex("#2F7BE0"),
            _ => Rgba.White,
        };

        // One room of each role (the first with a chunk in each of up to four biomes), 36 tiles across, on one sheet: a
        // row to a role.
        const int RoleCloseUp = 36;

        static PixelImage Roles(GeneratedWorld world)
        {
            var roles = Enum.GetValues(typeof(RoomRole)).Cast<RoomRole>().Where(r => r != RoomRole.Plain).ToList();
            const int scale = 6, across = 5, cell = RoleCloseUp * scale + 6;
            var sheet = new PixelImage(across * cell, roles.Count * cell);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#30303A"));
            for (int row = 0; row < roles.Count; row++)
            {
                var rooms = world.Rooms.Nodes.Where(n => n.Role == roles[row] && (n.Chunk != null || roles[row] == RoomRole.Ore)).GroupBy(n => n.Chunk?.Id ?? "").Select(g => g.First()).Take(across).ToList();
                for (int i = 0; i < rooms.Count; i++)
                    sheet.Draw(Picture(world, rooms[i].X - RoleCloseUp / 2, rooms[i].Y - RoleCloseUp / 2, RoleCloseUp, RoleCloseUp, scale), i * cell + 3, row * cell + 3);
            }
            return sheet;
        }

        // The middle of the biome's biggest stretch: the tile furthest inside it, on a coarse grid.
        static (int X, int Y)? Heart(GeneratedWorld world, Biome biome)
        {
            const int Step = 16;
            (int X, int Y)? best = null;
            int bestScore = -1;
            for (int y = CloseUp / 2; y < world.Map.Height - CloseUp / 2; y += Step)
            {
                for (int x = CloseUp / 2; x < world.Map.Width - CloseUp / 2; x += Step)
                {
                    if (world.Biomes[x, y] != biome) continue;
                    int reach = 0;
                    while (reach < 20 && new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.All(w => world.Biomes[x + w.Item1 * (reach + 1) * Step, y + w.Item2 * (reach + 1) * Step] == biome)) reach++;
                    // Of the tiles as far inside, the one whose window has no arena or place in it.
                    int score = reach * 4 + (world.Arenas.Any(a => Math.Abs(a.X - x) < CloseUp && Math.Abs(a.Y - y) < CloseUp) ? 0 : 2)
                        + (world.Places.Any(p => Math.Abs(p.X - x) < CloseUp && Math.Abs(p.Y - y) < CloseUp) ? 0 : 1);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = (x, y);
                    }
                }
            }
            return best;
        }

        public static void Write(string output, StoryData story, ArenaData arenas, TerrainData terrain, IEnumerable<ulong> seeds, bool closeUps = true,
            Action<GeneratedWorld> rooms = null)
        {
            string folder = Path.Combine(output, "map");
            Directory.CreateDirectory(folder);
            int size = WorldGenerator.GameSize;
            ulong first = seeds.First();
            foreach (int version in new[] { WorldGenerator.CaveVersion, WorldGenerator.RoomVersion })
            {
                foreach (ulong seed in seeds)
                {
                    var world = WorldGenerator.Generate(seed, size, size, story, arenas, version, terrain);
                    Png.Save(Picture(world, 0, 0, size, size, 1), Path.Combine(folder, $"v{version}_seed{seed}.png"));
                    Png.Save(Picture(world, world.SpawnX - SpawnCloseUp / 2, world.SpawnY - SpawnCloseUp / 2, SpawnCloseUp, SpawnCloseUp, 4),
                        Path.Combine(folder, $"v{version}_seed{seed}_spawn.png"));
                    if (seed != first || !closeUps) continue;
                    if (world.Rooms != null)
                    {
                        Png.Save(Picture(world, 0, 0, size, size, 2, rooms: true), Path.Combine(folder, $"v{version}_seed{seed}_rooms.png"));
                        Png.Save(Roles(world), Path.Combine(folder, $"v{version}_seed{seed}_roles.png"));
                        // The same rooms as the game draws them (Program.RoomViews).
                        rooms?.Invoke(world);
                    }
                    // Every biome side by side on one sheet, and each on its own.
                    var biomes = Enum.GetValues(typeof(Biome)).Cast<Biome>().ToList();
                    var sheet = new PixelImage(4 * (CloseUp * 4 + 8), (biomes.Count + 3) / 4 * (CloseUp * 4 + 8));
                    sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#30303A"));
                    for (int i = 0; i < biomes.Count; i++)
                    {
                        if (Heart(world, biomes[i]) is not (int hx, int hy)) continue;
                        var picture = Picture(world, hx - CloseUp / 2, hy - CloseUp / 2, CloseUp, CloseUp, 4);
                        Png.Save(picture, Path.Combine(folder, $"v{version}_seed{seed}_{biomes[i].ToString().ToLowerInvariant()}.png"));
                        sheet.Draw(picture, i % 4 * (CloseUp * 4 + 8) + 4, i / 4 * (CloseUp * 4 + 8) + 4);
                    }
                    Png.Save(sheet, Path.Combine(folder, $"v{version}_seed{seed}_biomes.png"));
                }
            }
            Console.WriteLine($"map pictures in {folder}");
        }
    }
}
