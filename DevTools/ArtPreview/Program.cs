using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // Draws every sprite to sheet.png and two made-up scenes (the spawn with a small base, the boss arena) to
    // cave.png and arena.png, laid out the way WorldView lays them out. Fails if any drawing is malformed.
    //
    //   dotnet run --project DevTools/ArtPreview [-- <output folder>]
    static class Program
    {
        const int Px = TileArt.Size;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F");

        static int Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
            Directory.CreateDirectory(output);
            var loot = LoadLoot();
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters")), MonsterSheet.File)));
            if (!loot.Success || !monsters.Success)
            {
                foreach (string error in loot.Errors.Concat(monsters.Errors)) Console.Error.WriteLine(error);
                return 1;
            }

            Save(Sheet(loot, monsters), Path.Combine(output, "sheet.png"), 4);
            Save(Cave(loot, monsters), Path.Combine(output, "cave.png"), 3);
            Save(Arena(loot), Path.Combine(output, "arena.png"), 3);
            Console.WriteLine($"Wrote sheet.png, cave.png and arena.png to {output}");
            return 0;
        }

        static void Save(PixelImage image, string path, int scale) => Png.Save(Scaled(image, scale), path);

        // ---- Every sprite ----

        static PixelImage Sheet(LootData loot, MonsterData monsters)
        {
            var rows = new List<List<PixelImage>>();
            var tiles = new List<PixelImage>();
            foreach (var kind in (GroundKind[])Enum.GetValues(typeof(GroundKind)))
            {
                foreach (int mask in new[] { 0, TileArt.Up, TileArt.Down, TileArt.Left | TileArt.Right, 15 })
                {
                    tiles.Add(TileArt.Ground(TileArt.Index(kind, mask, 0)));
                }
            }
            rows.Add(tiles);
            rows.Add(new[] { TileType.Workbench, TileType.Furnace, TileType.Chest, TileType.Drill, TileType.Smelter, TileType.Conveyor }
                .Select(TileArt.Object).Concat(Enumerable.Range(0, ItemArt.BeltFrames).Select(ItemArt.Belt))
                .Concat(new[] { ItemArt.DrillBit, ItemArt.Chevron }).ToList());

            var players = new List<PixelImage>();
            foreach (var look in new[] { Look.Down, Look.Side, Look.Up })
            {
                for (int frame = 0; frame < CreatureArt.WalkFrames; frame++) players.Add(CreatureArt.Player(look, frame, Rgba.Hex("#3F7FD8")));
            }
            players.Add(CreatureArt.Player(Look.Down, 0, Rgba.Hex("#4DB06A")));
            players.Add(CreatureArt.Player(Look.Down, 0, Rgba.Hex("#E08A3C")));
            rows.Add(players);

            var creatures = new List<PixelImage>();
            foreach (var monster in monsters.Monsters)
            {
                for (int frame = 0; frame < CreatureArt.IdleFrames; frame++) creatures.Add(CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), frame));
            }
            creatures.Add(CreatureArt.Monster("unknown", Rgba.Hex("#D05050"), 0));
            creatures.Add(CreatureArt.Boss("cave_warden", BossColor, 0));
            creatures.Add(CreatureArt.Boss("cave_warden", BossColor, 1));
            rows.Add(creatures);

            rows.Add(loot.Bases.Select(ItemArt.Gear).ToList());
            rows.Add(loot.Materials.Select(ItemArt.Material)
                .Concat(loot.Consumables.Select(ItemArt.Consumable))
                .Concat(loot.Runes.Take(1).Select(ItemArt.Rune))
                .Concat(Enumerable.Range(0, 4).Select(ItemArt.Crop))
                .Concat(new[] { ItemArt.Arrow, ItemArt.Orb, ItemArt.Spore, CreatureArt.Shadow(12) }).ToList());

            const int gap = 4;
            int width = rows.Max(row => row.Sum(image => image.Width + gap)) + gap;
            int height = rows.Sum(row => row.Max(image => image.Height) + gap) + gap;
            var sheet = new PixelImage(width, height);
            sheet.Fill(0, 0, width, height, Rgba.Hex("#24202A"));
            int y = gap;
            foreach (var row in rows)
            {
                int x = gap;
                foreach (var image in row)
                {
                    sheet.Draw(image, x, y);
                    x += image.Width + gap;
                }
                y += row.Max(image => image.Height) + gap;
            }
            return sheet;
        }

        static readonly Rgba BossColor = new Rgba(158, 143, 128);

        // ---- Scenes ----

        // The spawn of seed 7 with a small base built next to it: stations, a drill on a copper vein feeding belts
        // into a smelter and a chest, a field of mushrooms, two players, monsters and some loot.
        static PixelImage Cave(LootData loot, MonsterData monsters)
        {
            var world = WorldGenerator.Generate(7);
            var map = world.Map;
            int sx = world.SpawnX;
            int sy = world.SpawnY;
            var scene = new Scene(map, sx - 14, sy - 8, 28, 16, _ => false);

            void Put(int dx, int dy, TileType type) => map[sx + dx, sy + dy] = type;
            for (int dx = -5; dx <= 7; dx++) Put(dx, 3, TileType.Floor);
            Put(-4, 3, TileType.Workbench);
            Put(-3, 3, TileType.Furnace);
            Put(-2, 3, TileType.Chest);
            for (int dx = 1; dx <= 9; dx++) Put(dx, -4, TileType.Floor);
            Put(1, -4, TileType.CopperOre);
            Put(1, -3, TileType.Wall);
            Put(1, -5, TileType.Wall);
            Put(2, -4, TileType.Drill);
            for (int dx = 3; dx <= 4; dx++) Put(dx, -4, TileType.Conveyor);
            Put(5, -4, TileType.Smelter);
            Put(6, -4, TileType.Conveyor);
            Put(7, -4, TileType.Chest);
            scene.Machine(sx + 2, sy - 4, 2, ItemArt.DrillBit);
            scene.Machine(sx + 3, sy - 4, 0, ItemArt.Belt(0));
            scene.Machine(sx + 4, sy - 4, 0, ItemArt.Belt(0));
            scene.Machine(sx + 5, sy - 4, 0, ItemArt.Chevron);
            scene.Machine(sx + 6, sy - 4, 0, ItemArt.Belt(0));
            scene.Icon(sx + 3.5f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));
            scene.Icon(sx + 6.8f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));

            for (int i = 0; i < 4; i++) scene.Crop(sx - 5 + i, sy - 2, i);

            scene.Body(sx + 0.5f, sy + 0.5f, 0.35f, CreatureArt.Player(Look.Down, 0, Rgba.Hex("#3F7FD8")));
            scene.Body(sx + 2.5f, sy + 1.2f, 0.35f, CreatureArt.Player(Look.Side, 1, Rgba.Hex("#4DB06A")));
            scene.Body(sx - 1.5f, sy + 1.6f, 0.35f, CreatureArt.Player(Look.Up, 1, Rgba.Hex("#E08A3C")));
            float mx = sx + 6;
            foreach (var monster in monsters.Monsters)
            {
                var image = CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), 0);
                scene.Body(mx, sy + 0.5f, monster.Radius, mx > sx + 7 ? image.FlippedX() : image);
                mx += 1.6f;
            }
            scene.Icon(sx - 1, sy - 0.5f, ItemArt.Gear(loot.Bases.First(b => b.Id == "iron_sword")));
            scene.Icon(sx - 0.2f, sy - 0.8f, ItemArt.Consumable(loot.Consumables[0]));
            scene.Icon(sx + 1.2f, sy - 0.6f, ItemArt.Rune(loot.Runes[0]));
            scene.Icon(sx + 2.2f, sy - 1.2f, ItemArt.Material(loot.FindMaterial("slime_gel")));
            scene.Icon(sx + 3.4f, sy + 0.2f, ItemArt.Arrow);
            return scene.Image;
        }

        // The boss in its arena, awake, with a player in front of it.
        static PixelImage Arena(LootData loot)
        {
            var world = WorldGenerator.Generate(7);
            int ax = world.ArenaX;
            int ay = world.ArenaY;
            var arena = world.Arena;
            var scene = new Scene(world.Map, ax - 13, ay - 8, 26, 16, p => Vec2.Distance(p, arena) <= world.ArenaRadius);
            scene.Body(ax + 0.5f, ay + 1.5f, 1.2f, CreatureArt.Boss("cave_warden", BossColor, 0));
            scene.Body(ax - 2.5f, ay - 3f, 0.35f, CreatureArt.Player(Look.Up, 0, Rgba.Hex("#3F7FD8")));
            scene.Body(ax + 3.5f, ay - 2.5f, 0.35f, CreatureArt.Player(Look.Side, 0, Rgba.Hex("#4DB06A")).FlippedX());
            scene.Icon(ax - 1.5f, ay - 1.5f, ItemArt.Orb);
            scene.Icon(ax + 2f, ay - 0.5f, ItemArt.Spore);
            return scene.Image;
        }

        // A window of the map drawn the way WorldView draws it, with things placed in world units (y up).
        sealed class Scene
        {
            readonly TileMap _map;
            readonly int _left;
            readonly int _bottom;
            readonly int _width;
            readonly int _height;
            readonly Func<Vec2, bool> _inArena;
            readonly List<(float Y, Action<PixelImage> Draw)> _bodies = new List<(float, Action<PixelImage>)>();
            readonly List<Action<PixelImage>> _overlays = new List<Action<PixelImage>>();
            readonly List<Action<PixelImage>> _icons = new List<Action<PixelImage>>();

            public Scene(TileMap map, int left, int bottom, int width, int height, Func<Vec2, bool> inArena)
            {
                _map = map;
                _left = left;
                _bottom = bottom;
                _width = width;
                _height = height;
                _inArena = inArena;
            }

            public PixelImage Image
            {
                get
                {
                    var image = new PixelImage(_width * Px, _height * Px);
                    image.Fill(0, 0, image.Width, image.Height, Backdrop);
                    for (int y = _bottom; y < _bottom + _height; y++)
                    {
                        for (int x = _left; x < _left + _width; x++)
                        {
                            var (px, py) = Corner(x, y + 1);
                            image.Draw(TileArt.Ground(TileArt.GroundAt(_map, x, y, _inArena(TileMap.Center(x, y)))), px, py);
                            var placed = TileArt.Object(_map[x, y]);
                            if (placed != null) image.Draw(placed, px, py);
                        }
                    }
                    foreach (var draw in _overlays) draw(image);
                    foreach (var (_, draw) in _bodies.OrderByDescending(b => b.Y)) draw(image);
                    foreach (var draw in _icons) draw(image);
                    return image;
                }
            }

            (int X, int Y) Corner(float x, float y) => ((int)MathF.Round((x - _left) * Px), (int)MathF.Round((_bottom + _height - y) * Px));

            public void Machine(int x, int y, int direction, PixelImage part)
            {
                var (px, py) = Corner(x, y + 1);
                var turned = Turn(part, direction);
                _overlays.Add(image => image.Draw(turned, px + (Px - turned.Width) / 2, py + (Px - turned.Height) / 2));
            }

            public void Crop(int x, int y, int stage)
            {
                var (px, py) = Corner(x, y);
                var crop = ItemArt.Crop(stage);
                _overlays.Add(image => image.Draw(crop, px + (Px - crop.Width) / 2, py - crop.Height));
            }

            // Bodies stand with their feet a little below their centre, as in WorldView.Place.
            public void Body(float x, float y, float radius, PixelImage sprite)
            {
                var (px, py) = Corner(x, y);
                int feet = py + (int)MathF.Round(radius * 0.8f * Px);
                var shadow = CreatureArt.Shadow(Math.Max(6, (int)MathF.Round(radius * 2 * Px * 0.9f)));
                _bodies.Add((y, image =>
                {
                    image.Draw(shadow, px - shadow.Width / 2, feet - shadow.Height / 2 - 1);
                    image.Draw(sprite, px - sprite.Width / 2, feet - sprite.Height);
                }));
            }

            public void Icon(float x, float y, PixelImage icon)
            {
                var (px, py) = Corner(x, y);
                _icons.Add(image => image.Draw(icon, px - icon.Width / 2, py - icon.Height / 2));
            }
        }

        // Quarter turns counter-clockwise, as a rotation of 90 degrees per step does in Unity.
        static PixelImage Turn(PixelImage image, int quarters)
        {
            var result = image;
            for (int q = 0; q < (quarters % 4 + 4) % 4; q++)
            {
                var turned = new PixelImage(result.Height, result.Width);
                for (int y = 0; y < result.Height; y++)
                {
                    for (int x = 0; x < result.Width; x++) turned[y, result.Width - 1 - x] = result[x, y];
                }
                result = turned;
            }
            return result;
        }

        static PixelImage Scaled(PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x / scale, y / scale];
            }
            return result;
        }

        static LootData LoadLoot()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Loot"));
            string Read(string file) => File.ReadAllText(Path.Combine(folder, file));
            return LootSheets.Parse(
                Read(LootSheets.BasesFile),
                Read(LootSheets.AffixesFile),
                Read(LootSheets.RunesFile),
                Read(LootSheets.RunewordsFile),
                Read(LootSheets.ConsumablesFile),
                Read(LootSheets.MaterialsFile),
                Read(LootSheets.RecipesFile));
        }

        static string FindFolder(string relativePath)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            throw new DirectoryNotFoundException(relativePath);
        }
    }
}
