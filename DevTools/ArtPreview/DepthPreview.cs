using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // The standing layer as the game draws it (묶음 B0, Docs/BiomeRework.md 4-3·4-4, --only depth, out/depth/): the joined
    // fences, hedges and walls in all sixteen shapes and the four gates, a fenced pen with corners, a T, a cross and
    // gates with people in front of and behind it, a place of the story with someone behind a roof and a mushroom tree
    // (faded and with the setting off), and a room of each biome with its decor. Drawn by StandArt, PropArt and
    // DecorArt, the game's own code, through the Scene the other previews use.
    static partial class Program
    {
        static void WriteDepth(string output, StoryData story, WorldLights lights)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "depth")).FullName;
            var index = new StringBuilder("# 서 있는 것의 깊이(묶음 B0)\n\n`dotnet run --project DevTools/ArtPreview -- --only depth`. 게임의 그림 코드(`StandArt`, `PropArt.Fences.cs`, `DecorArt`)로 그렸다.\n\n| 그림 | 보여 주는 것 |\n|---|---|\n");
            void Write(PixelImage image, string name, int scale, string what)
            {
                Png.Save(image.Times(scale), Path.Combine(folder, name));
                index.Append($"| `{name}` | {what} |\n");
            }
            Write(JoinedSet(), "joined.png", 3, "이어지는 것 16모양(N1 E2 S4 W8): 나무 울타리·산울타리·철벽·성벽·나무벽, 울타리 문 넷(가로·세로 × 닫힘·열림)");
            Write(Pen(true), "pen.png", 3, "울타리로 두른 방목장: ㄱ 모서리, T자, 십자, 끝, 가로·세로 문(닫힘·열림), 산울타리와 철벽 줄, 울타리 뒤(다리가 가려짐)·앞의 사람, 버섯 나무 뒤의 사람과 몬스터(갓이 흐려짐), 바닥 꾸밈");
            Write(Pen(false), "pen_nofade.png", 3, "같은 장면을 [물체 뒤 흐리게: 끔]으로");
            var world = WorldGenerator.Generate(7, story: story);
            foreach (string id in new[] { "dawn_village", "hero_hut" })
            {
                var site = world.Places.First(p => p.Definition.Id == id);
                Write(PlaceDepth(world, site, lights, true), $"place_{id}.png", 3, $"{site.Definition.Name}: 건물·소품의 발 정렬과 그림자, 지붕·갓 뒤의 사람(흐려짐), 울타리·산울타리 이음");
                Write(PlaceDepth(world, site, lights, false), $"place_{id}_nofade.png", 3, $"{site.Definition.Name}을 흐림 끔으로");
            }
            Write(Biomes(), "biomes.png", 2, "생물군계 여덟의 방에 깔린 꾸밈 시범(B0의 낮은 밀도: 생물군계마다 두 종류, 세 그림, 뒤집기·자리 흔들기)");
            File.WriteAllText(Path.Combine(folder, "index.md"), index.ToString());
            Console.WriteLine($"Wrote the standing layer's pictures to {folder}");
        }

        static readonly Rgba DepthBack = Rgba.Hex("#14121A"), DepthText = Rgba.Hex("#E8E4F0"), DepthDim = Rgba.Hex("#8E8AA4");

        // Every joined kind in its sixteen shapes, each on a tile of floor with its picture as the game stands it.
        static PixelImage JoinedSet()
        {
            var kinds = new[] { (TileType.Fence, "WOOD FENCE"), (TileType.Hedge, "HEDGE"), (TileType.IronWall, "IRON WALL"), (TileType.CastleWall, "CASTLE WALL"), (TileType.TimberWall, "TIMBER WALL") };
            const int cell = 30, top = 18, row = 52;
            var g = new PixelImage(16 * cell + 12, top + row * (kinds.Length + 1) + 10);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Small(6, 4, "JOINED: 16 SHAPES BY N1 E2 S4 W8, 3/4 VIEW (NOT ROTATED). GATES: DOORS IN A FENCE RUN", DepthText, DepthBack);
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0));
            for (int k = 0; k < kinds.Length; k++)
            {
                var (type, name) = kinds[k];
                int y0 = top + k * row;
                g.Small(6, y0, name, DepthDim, DepthBack);
                for (int mask = 0; mask < 16; mask++)
                {
                    int x0 = 6 + mask * cell;
                    g.Draw(floor, x0, y0 + 18);
                    var picture = PropArt.Prop(type, mask);
                    g.Draw(picture, x0 + 8 - picture.Width / 2, y0 + 34 - picture.Height);
                    g.Small(x0 + 2, y0 + 38, mask.ToString(), DepthDim, DepthBack);
                }
            }
            int gy = top + kinds.Length * row;
            g.Small(6, gy, "GATES: E-W SHUT, E-W OPEN, N-S SHUT, N-S OPEN (THE LEAF OVER THE NEXT TILE)", DepthDim, DepthBack);
            int gx = 6;
            foreach (var (along, open) in new[] { (true, false), (true, true), (false, false), (false, true) })
            {
                var picture = PropArt.FenceGate(along, open);
                g.Draw(floor, gx, gy + 18);
                if (!along && open) g.Draw(floor, gx + 16, gy + 18);
                g.Draw(picture, gx, gy + 34 - picture.Height);
                gx += cell + (picture.Width > 16 ? 16 : 0) + 10;
            }
            return g;
        }

        // A small floor of the cave laid from rows (the first is the north): '#' rock, 'F' fence, 'G' a door (a gate when
        // fences are either side), 'O' one standing open, 'H' hedge, 'I' iron wall, 'T' a mushroom tree, anything else
        // floor.
        static (TileMap Map, List<(int X, int Y)> Open) Layout(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var map = new TileMap(w, h, TileType.Floor);
            var open = new List<(int, int)>();
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    int y = h - 1 - r;
                    char c = rows[r][x];
                    map[x, y] = c switch
                    {
                        '#' => TileType.Wall,
                        'F' => TileType.Fence,
                        'G' or 'O' => TileType.Door,
                        'H' => TileType.Hedge,
                        'I' => TileType.IronWall,
                        'T' => TileType.MushroomTree,
                        _ => TileType.Floor,
                    };
                    if (c == 'O') open.Add((x, y));
                }
            }
            return (map, open);
        }

        static PixelImage Pen(bool fade)
        {
            var (map, open) = Layout(new[]
            {
                "##########################",
                "#........................#",
                "#..FFFFFFFFFF....T.......#",
                "#..F....F...F........T...#",
                "#..F....F...O............#",
                "#..F....F...F...HHHHH....#",
                "#..FFFFFFFFFF...H........#",
                "#..F....F.......H...IIII.#",
                "#..F....F.......H...I....#",
                "#..F....G...........I....#",
                "#..F....F................#",
                "#..FFGFFFFOFF............#",
                "#........................#",
                "##########################",
            });
            var scene = new Scene(map, 0, 0, map.Width, map.Height, _ => false) { Fade = fade, Open = (x, y) => open.Contains((x, y)) };
            scene.Places(null);
            scene.Decor(11, (x, y) => Biome.Cave);
            // Behind the top run (legs hidden by it), in front of the bottom run, inside, in the open gate, behind the
            // mushroom trees (a player and a monster: their caps fade), and beside one (it does not).
            scene.Player(6.5f, 12.4f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(4.5f, 1.6f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8703F"));
            scene.Player(10.5f, 6.5f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#5FBF5A"));
            scene.Player(12.5f, 9.55f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#C85AD8"));
            scene.Player(17.5f, 12.4f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Body(21.5f, 11.4f, 0.4f, CreatureArt.Monster("slime", Rgba.Hex("#6CC35A"), 0));
            scene.Player(19.4f, 11.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8C83F"));
            return Captioned(scene.Image, fade ? $"FADE ON: {scene.Faded} CANOPIES FADED, {scene.DecorShown} DECOR" : "FADE OFF (SETTING)");
        }

        static PixelImage PlaceDepth(GeneratedWorld world, PlaceSite site, WorldLights lights, bool fade)
        {
            int half = site.Radius + 5;
            var scene = new Scene(world.Map, site.X - half, site.Y - half, half * 2, half * 2, _ => false) { Fade = fade, Night = false };
            scene.Places(world.Places);
            scene.Decor(7, (x, y) => world.Biomes[x, y]);
            foreach (var (character, at) in site.People)
                scene.Player(at.X, at.Y - 0.3f, PeopleArt.Look(character.Look, Rgba.Hex(character.Color), 0));
            // Someone just behind each house and each mushroom tree in the picture, where their feet are behind its foot
            // and their body under its roof or cap.
            var map = world.Map;
            var spots = new List<(int Rank, float X, float Y)>();
            for (int y = site.Y - half; y < site.Y + half; y++)
            {
                for (int x = site.X - half; x < site.X + half; x++)
                {
                    var type = map[x, y];
                    if (StandArt.CanopyRows(type) == 0) continue;
                    var (w, h) = Structures.Footprint(type);
                    bool tree = type == TileType.MushroomTree || type == TileType.CherryTree;
                    float bx = x + w / 2f + (tree ? 0.6f : -0.5f), by = y + h + (tree ? 0.7f : 0.9f);
                    if (map.IsSolid(TileMap.Cell(bx), TileMap.Cell(by))) continue;
                    spots.Add((tree ? 1 : 0, bx, by));
                }
            }
            // The houses first, then up to two trees.
            foreach (var (_, bx, by) in spots.OrderBy(s => s.Rank).Take(spots.Count(s => s.Rank == 0) + 2))
                scene.Player(bx, by, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
            return Captioned(scene.Image, fade ? $"FADE ON: {scene.Faded} FADED" : "FADE OFF");
        }

        // A room of each biome with its decor, a mushroom tree and a player, side by side.
        static PixelImage Biomes()
        {
            var biomes = (Biome[])Enum.GetValues(typeof(Biome));
            var panels = new List<PixelImage>();
            foreach (var biome in biomes)
            {
                var (map, _) = Layout(new[]
                {
                    "################",
                    "#..............#",
                    "#..............#",
                    "#.........T....#",
                    "#..............#",
                    "#..............#",
                    "#..............#",
                    "#..............#",
                    "#..............#",
                    "################",
                });
                var scene = new Scene(map, 0, 0, map.Width, map.Height, _ => false) { Biome = biome };
                scene.Places(null);
                scene.Decor(3 + (ulong)biome, (x, y) => biome);
                scene.Player(5.5f, 4.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                panels.Add(Captioned(scene.Image, $"{biome.ToString().ToUpperInvariant()}: {scene.DecorShown} DECOR"));
            }
            int pw = panels[0].Width + 6, ph = panels[0].Height + 6;
            var g = new PixelImage(pw * 4, ph * 2);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            for (int i = 0; i < panels.Count; i++) g.Draw(panels[i], i % 4 * pw + 3, i / 4 * ph + 3);
            return g;
        }

        static PixelImage Captioned(PixelImage image, string caption)
        {
            var g = new PixelImage(image.Width, image.Height + 12);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Draw(image, 0, 12);
            g.Small(2, 2, caption, DepthText, DepthBack);
            return g;
        }
    }
}
