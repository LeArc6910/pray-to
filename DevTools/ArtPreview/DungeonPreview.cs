using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The instance dungeons as DungeonGenerator lays them (Docs/WorldRework.md 5단계, 묶음 5b), written by --only dungeons
    // and with everything else:
    //  - dungeons.png: each row of dungeons.csv (a dungeon of any biome in the sand caves) laid from four seeds, one tile
    //    a pixel and three across: floor in the biome's colour, the rooms' letters as squares in their middles (the
    //    entrance green, packs red, the champions orange, the boss crimson, the end gold, treasure and hidden yellow,
    //    the shrine cyan, the rest room white), the doors (grate grey, crack tan), the doors out and the shortcut's stones.
    //  - dungeons/<id>.png: its first seed as the game draws it, lit as the dark shows it, a monster on each pack's spots
    //    and the master in the boss room; dungeons/<id>_rooms.png: each room of it cut out, unlit, to see the chunks.
    static partial class Program
    {
        static readonly ulong[] DungeonSeeds = { 1, 2, 3, 4 };

        static Rgba LetterColour(char letter) => letter switch
        {
            'E' => Rgba.Hex("#4DD06A"),
            'C' or 'K' or 'L' => Rgba.Hex("#E04848"),
            'M' => Rgba.Hex("#FF9A3C"),
            'B' => Rgba.Hex("#B01040"),
            'X' => Rgba.Hex("#FFD23F"),
            'T' or 'H' => Rgba.Hex("#F2E66A"),
            'S' => Rgba.Hex("#5BE0E6"),
            'R' => Rgba.Hex("#FFFFFF"),
            _ => Rgba.Hex("#888888"),
        };

        static Rgba DungeonFloor(Biome biome) => biome switch
        {
            Biome.Mushroom => Rgba.Hex("#4E603A"),
            Biome.Ice => Rgba.Hex("#8FBAD6"),
            Biome.Crystal => Rgba.Hex("#7458B8"),
            Biome.Lava => Rgba.Hex("#8E3A26"),
            Biome.Abyss => Rgba.Hex("#33265E"),
            Biome.Lake => Rgba.Hex("#2E5E86"),
            Biome.Sand => Rgba.Hex("#A8935A"),
            _ => Rgba.Hex("#4A3E34"),
        };

        // A map of one slot and nothing else, for laying dungeons on.
        static TileMap DungeonMap()
        {
            var map = new TileMap(DungeonPocket.Span, 8, TileType.Wall);
            map.AddPocket(DungeonPocket.Rows);
            return map;
        }

        static void Dungeons(TerrainData terrain, MonsterData monsters, WorldLights lights, string output, StoryData story = null)
        {
            var data = terrain.Dungeons;
            if (data.Dungeons.Count == 0) return;
            string folder = Directory.CreateDirectory(Path.Combine(output, "dungeons")).FullName;
            const int scale = 3, gap = 6;
            int cell = DungeonPocket.Inner * scale + gap;
            var sheet = new PixelImage(DungeonSeeds.Length * cell + gap, data.Dungeons.Count * cell + gap);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#30303A"));
            var report = new List<string>();
            for (int row = 0; row < data.Dungeons.Count; row++)
            {
                var definition = data.Dungeons[row];
                var biome = definition.Biome ?? (definition.Cellar ? Biome.Cave : Biome.Sand);
                for (int i = 0; i < DungeonSeeds.Length; i++)
                {
                    var map = DungeonMap();
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var layout = DungeonGenerator.Lay(map, 0, DungeonSeeds[i], definition, biome, terrain);
                    double ms = clock.Elapsed.TotalMilliseconds;
                    report.Add($"{definition.Id} seed {DungeonSeeds[i]}: rooms {layout.Nodes.Count} ({new string(layout.Nodes.Select(n => n.Letter).ToArray())}), tries {DungeonGenerator.LastTries}, {ms:0.0} ms");
                    sheet.Draw(Schematic(map, layout, scale), gap + i * cell, gap + row * cell);
                }
                // The first seed as the game draws it.
                var drawn = DungeonMap();
                var first = DungeonGenerator.Lay(drawn, 0, DungeonSeeds[0], definition, biome, terrain);
                var (ix, iy) = DungeonPocket.Inside(drawn, 0);
                var scene = new Scene(drawn, ix, iy, DungeonPocket.Inner, DungeonPocket.Inner, _ => false) { Biome = biome };
                scene.Places(Array.Empty<PlaceSite>());
                Populate(scene, first, monsters, biome);
                scene.Player(first.Entrance.X, first.Entrance.Y, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                scene.Lit(lights, null);
                Save(scene.Image, Path.Combine(folder, definition.Id + ".png"), 1);
                // Each room cut out, unlit.
                const int across = 24, columns = 4;
                int rows = (first.Nodes.Count + columns - 1) / columns;
                var rooms = new PixelImage(columns * (across * Px + 4), rows * (across * Px + 4));
                rooms.Fill(0, 0, rooms.Width, rooms.Height, Rgba.Hex("#30303A"));
                for (int k = 0; k < first.Nodes.Count; k++)
                {
                    var room = first.Nodes[k];
                    var c = room.Center;
                    var view = new Scene(drawn, (int)c.X - across / 2, (int)c.Y - across / 2, across, across, _ => false) { Biome = biome };
                    view.Places(Array.Empty<PlaceSite>());
                    Populate(view, first, monsters, biome, room);
                    rooms.Draw(view.Image, (k % columns) * (across * Px + 4) + 2, (k / columns) * (across * Px + 4) + 2);
                }
                Save(rooms, Path.Combine(folder, definition.Id + "_rooms.png"), 1);
            }
            Save(sheet, Path.Combine(output, "dungeons.png"), 1);
            report.AddRange(Gates(terrain, monsters, lights, folder, story));
            File.WriteAllLines(Path.Combine(folder, "report.txt"), report);
            foreach (string line in report) Console.WriteLine(line);
        }

        // A monster of the biome's own on each pack room's spots (as many as its chunk marks), and its master in the boss
        // room, so a room reads as the fight it will be.
        static void Populate(Scene scene, DungeonLayout layout, MonsterData monsters, Biome biome, DungeonRoom only = null)
        {
            var own = monsters.Packs?.Packs.Where(p => p.Biome == biome && p.Weight > 0).ToList();
            var pack = own != null && own.Count > 0 ? own[0] : monsters.Packs?.Packs.FirstOrDefault(p => p.Biome == null);
            if (pack == null) return;
            var boss = layout.Definition?.Boss is string id ? monsters.Find(id) : null;
            foreach (var room in layout.Nodes.Where(r => DungeonSheet.HasPack(r.Letter) && (only == null || r == only)))
            {
                int k = 0;
                foreach (var spot in room.Monsters)
                {
                    var kind = pack.Members[k++ % pack.Members.Count].Monster;
                    scene.Body(spot.X, spot.Y, kind.Radius, CreatureArt.Monster(kind.Id, Rgba.Hex(kind.Color), 0));
                }
                if (room.Elite is Vec2 at)
                {
                    var lead = room.Letter == 'B' && boss != null ? boss : pack.Members[pack.Members.Count - 1].Monster;
                    var art = CreatureArt.Monster(lead.Id, Rgba.Hex(lead.Color), 0);
                    // A mid-boss in person (B1c, an XL) is drawn its own size; a big elite twice its kind's.
                    scene.Body(at.X, at.Y, lead.Radius * 2, room.Letter == 'B' && !lead.Big ? Scaled(art, 2) : art);
                }
            }
        }

        // B1c: dungeons/gates.png. A world of the generator's version 4 (seed 1): the dirt cave's and the mushroom
        // forest's gate rooms lit, their torches in the dungeon's light (dungeons.csv light, WorldLights.GateColors) beside
        // the same rooms in plain torchlight; and a camp's trapdoor shut and thrown open (its cellar's way down).
        static IEnumerable<string> Gates(TerrainData terrain, MonsterData monsters, WorldLights lights, string folder, StoryData story)
        {
            var world = WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, story, WorldArenaSheet(monsters), WorldGenerator.LandmarkVersion, terrain);
            var gates = DungeonPocket.Gates(world.Rooms, world.Map);
            var colours = WorldLights.GateColors(gates, terrain.Dungeons);
            const int across = 22, high = 16;
            var shots = new List<PixelImage>();
            var lines = new List<string>();
            PixelImage Shot(Vec2 at, Func<int, int, Rgba?> recolor)
            {
                int left = (int)at.X - across / 2, bottom = (int)at.Y - high / 2;
                var scene = new Scene(world.Map, left, bottom, across, high, _ => false) { Night = true, BiomeAt = (x, y) => world.Biomes[x, y], Biome = world.Biomes[(int)at.X, (int)at.Y] };
                scene.Places(world.Places);
                scene.Player(at.X, at.Y - 2, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
                scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(world.Biomes[x, y]), (x, y) => WorldLights.BiomeTint(world.Biomes[x, y]), recolor);
                return scene.Image;
            }
            foreach (var biome in new[] { Biome.Cave, Biome.Mushroom })
            {
                var gate = gates.FirstOrDefault(g => !g.Cellar && g.Biome == biome);
                if (gate == null) continue;
                var row = terrain.Dungeons.For(gate);
                shots.Add(Shot(gate.Center, colours));
                shots.Add(Shot(gate.Center, null));
                lines.Add($"gate {gate.Index} ({biome}) at {gate.X},{gate.Y}: {row?.Id} light {row?.Light}");
            }
            var cellar = gates.FirstOrDefault(g => g.Cellar);
            if (cellar != null)
            {
                shots.Add(Shot(cellar.Center, null));
                world.Map[cellar.X, cellar.Y] = TileType.OpenTrapdoor;
                shots.Add(Shot(cellar.Center, null));
                world.Map[cellar.X, cellar.Y] = TileType.Trapdoor;
                lines.Add($"cellars {gates.Count(g => g.Cellar)} of camps {world.Rooms.Nodes.Count(n => n.Role == RoomRole.Elite)}; first at {cellar.X},{cellar.Y} ({cellar.Biome})");
            }
            lines.Add($"lairs {world.Rooms.Lairs.Count}: " + string.Join(", ", world.Rooms.Lairs.Select(i => $"{world.Rooms.Nodes[i].Biome} {world.Rooms.Nodes[i].X},{world.Rooms.Nodes[i].Y} r{world.Rooms.Nodes[i].Radius}")));
            if (shots.Count == 0) return lines;
            int w = shots[0].Width, h = shots[0].Height;
            var image = new PixelImage(2 * (w + 4) + 4, (shots.Count + 1) / 2 * (h + 4) + 4);
            image.Fill(0, 0, image.Width, image.Height, Rgba.Hex("#30303A"));
            for (int i = 0; i < shots.Count; i++) image.Draw(shots[i], 4 + i % 2 * (w + 4), 4 + i / 2 * (h + 4));
            Save(image, Path.Combine(folder, "gates.png"), 1);
            return lines;
        }

        static PixelImage Schematic(TileMap map, DungeonLayout layout, int scale)
        {
            var (ix, iy) = DungeonPocket.Inside(map, 0);
            int n = DungeonPocket.Inner;
            var image = new PixelImage(n * scale, n * scale);
            void Tile(int x, int y, Rgba colour) => image.Fill((x - ix) * scale, (n - 1 - (y - iy)) * scale, scale, scale, colour);
            for (int y = iy; y < iy + n; y++)
            {
                for (int x = ix; x < ix + n; x++)
                {
                    var type = map[x, y];
                    var colour = type switch
                    {
                        TileType.Wall => Rgba.Hex("#0B0B0E"),
                        TileType.Grate => Rgba.Hex("#9AA4B0"),
                        TileType.CrackedWall => Rgba.Hex("#B89A6A"),
                        TileType.Torii => Rgba.Hex("#E03A3A"),
                        // The biomes' props in the dungeons' chunks (B1c).
                        _ when BiomeProps.Is(type) => Rgba.Hex("#A08060"),
                        TileType.Waystone => Rgba.Hex("#6EC0FF"),
                        TileType.Torch or TileType.Lantern => Rgba.Hex("#FFD23F"),
                        TileType.Campfire => Rgba.Hex("#FF9A3C"),
                        TileType.Lava => Rgba.Hex("#FF7A1A"),
                        TileType.ArenaInlay => Rgba.Hex("#C8443A"),
                        TileType.RuneFloor => Rgba.Hex("#5BE0E6"),
                        TileType.WoodChest or TileType.IronChest or TileType.GoldChest => Rgba.Hex("#FFE27A"),
                        _ => map.IsSolid(x, y) ? Rgba.Hex("#6A6A74") : DungeonFloor(layout.Biome),
                    };
                    Tile(x, y, colour);
                }
            }
            foreach (var room in layout.Nodes)
            {
                var c = room.Center;
                int px = (int)((c.X - ix) * scale), py = (int)((n - (c.Y - iy)) * scale);
                image.Fill(px - 4, py - 4, 9, 9, Rgba.Hex("#000000"));
                image.Fill(px - 3, py - 3, 7, 7, LetterColour(room.Letter));
                foreach (var spot in room.Monsters) image.Fill((int)((spot.X - ix) * scale) - 1, (int)((n - (spot.Y - iy)) * scale) - 1, 2, 2, Rgba.Hex("#FF4040"));
            }
            return image;
        }
    }
}
