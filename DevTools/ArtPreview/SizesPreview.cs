using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // The monsters' size classes as the game draws them (2026-11-24, Docs/BiomeRework.md B0b, --only sizes → out/sizes/):
    // the classes side by side with a player, each body standing on its feet with its round hit shape and shadow
    // (lineup.png); the big kinds' frames, poses and back views (poses.png); an elite leader's gold ring against the
    // old 1.3 stretch (leaders.png); and the room at each tile (World.Clearance) of a room with ways one, two and three
    // tiles wide, with which bodies fit where (clearance.png). Fails when a big kind's picture is not the width its size
    // class asks (MonsterSizes: L 24 to 32 pixels, XL 40 to 56).
    static class SizesPreview
    {
        const int T = TileArt.Size;
        static readonly Rgba Back = Rgba.Hex("#14121A"), Text = Rgba.Hex("#E8E4F0"), Dim = Rgba.Hex("#8E8AA4"), Yellow = Rgba.Hex("#FFD860"),
            Green = Rgba.Hex("#80E070"), Red = Rgba.Hex("#FF6070"), Rock = Rgba.Hex("#1C1712"), RockTop = Rgba.Hex("#3A3028");
        static readonly Rgba Gold = Rgba.Hex("#FFC740");

        // The kinds shown, smallest first.
        static readonly string[] Lineup = { "cave_rat", "puffball", "bomb_scarab", "glow_bat", "slime", "cave_beetle", "frost_wolf", "crystal_mimic", "tentacle", "big_slime", "big_beetle", "cap_giant", "yeti", "great_worm", "crystal_tortoise", "great_crab", "slime_king", "slime_mother", "mycelium_mother", "frost_mammoth", "temple_guardian", "geode_golem", "moss_turtle", "salamander", "void_eye", "magma_hurler", "void_splitter", "forge_keeper", "gate_warden" };

        public static bool Write(string output, MonsterData monsters, PixelImage hero)
        {
            string folder = Path.Combine(output, "sizes");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            report.AppendLine("id              size  radius  picture(w×h)  class band(px)  hit circle(px)");
            bool fine = true;
            foreach (var monster in monsters.Monsters)
            {
                var image = Picture(monster, 0);
                var band = MonsterSizes.Of(monster.Size);
                bool inBand = image.Width >= band.MinPixels && image.Width <= band.MaxPixels;
                report.AppendLine($"{monster.Id,-15} {MonsterSizes.Name(monster.Size),-5} {monster.Radius,6:0.00}  {image.Width,4}×{image.Height,-6}  {band.MinPixels,3}~{band.MaxPixels,-9} {monster.Radius * 2 * T,6:0.0}{(inBand ? "" : "  (밖)")}");
                if (monster.Big && !inBand)
                {
                    Console.Error.WriteLine($"sizes {monster.Id}: its picture is {image.Width} pixels wide, outside {MonsterSizes.Name(monster.Size)}'s {band.MinPixels}~{band.MaxPixels}");
                    fine = false;
                }
            }
            Save(LineupPicture(monsters, hero), Path.Combine(folder, "lineup.png"), 4);
            Save(Poses(monsters), Path.Combine(folder, "poses.png"), 4);
            Save(Leaders(monsters), Path.Combine(folder, "leaders.png"), 4);
            Save(ClearancePicture(monsters, hero), Path.Combine(folder, "clearance.png"), 3);
            Save(Kinds(monsters, hero, IceSandKinds, "ICE AND SAND CAVE KINDS (B2B): FRAMES, POSES, BACK, DORMANT"), Path.Combine(folder, "icesand.png"), 6);
            Save(Kinds(monsters, hero, CrystalLakeKinds, "CRYSTAL CAVE AND LAKE KINDS (B3B): FRAMES, POSES, BACK, DORMANT"), Path.Combine(folder, "crystallake.png"), 6);
            Save(Kinds(monsters, hero, LavaVoidKinds, "LAVA FIELDS AND VOID ABYSS KINDS (B4B): FRAMES, POSES, BACK, DORMANT"), Path.Combine(folder, "lavavoid.png"), 6);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Console.WriteLine($"Wrote sizes/lineup.png, poses.png, leaders.png, clearance.png, icesand.png, crystallake.png, lavavoid.png and report.txt to {folder}");
            return fine;
        }

        static Rgba ColorOf(MonsterDefinition monster) => Rgba.Hex(monster.Color.Length > 0 ? monster.Color : "#8CD94D");

        static PixelImage Picture(MonsterDefinition monster, int frame, MonsterView view = MonsterView.Side, MonsterPose pose = MonsterPose.Idle) =>
            CreatureArt.Monster(monster.Id, ColorOf(monster), frame, view, pose);

        static void Save(PixelImage image, string path, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x / scale, y / scale];
            Png.Save(result, path);
        }

        static void Floor(PixelImage g, int left, int top, int columns, int rows)
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x * 7 + y * 3) % TileArt.Variants)), left + x * T, top + y * T);
        }

        // A body as the game stands it (WorldView.MakeBody): its middle at (cx, cy) in pixels, its picture's bottom at
        // its feet (MonsterLook.FootOffset), its shadow under them, and its round hit shape.
        static void Stand(PixelImage g, PixelImage picture, float radius, int cx, int cy, Rgba? ring = null)
        {
            int feet = cy + (int)MathF.Round(radius * 0.8f * T);
            int shadow = Math.Max(6, (int)MathF.Round(radius * 2 * T * 0.9f));
            var under = CreatureArt.Shadow(shadow);
            g.Draw(under, cx - under.Width / 2, feet - under.Height / 2);
            if (ring is Rgba colour) g.Ring(cx, cy, radius * T, colour);
            g.Draw(picture, cx - picture.Width / 2, feet - picture.Height);
        }

        static PixelImage LineupPicture(MonsterData monsters, PixelImage hero)
        {
            var kinds = Lineup.Select(monsters.Find).ToList();
            int width = 20 + kinds.Sum(k => Math.Max(44, Picture(k, 0).Width + 14)) + 40;
            var g = new PixelImage(width, 150);
            g.Fill(0, 0, width, g.Height, Back);
            g.Text(6, 5, "MONSTER SIZE CLASSES", Text, Back);
            g.Small(6, 15, "DRAWN AT 16 PX A TILE LIKE THE GAME. YELLOW RING: THE HIT CIRCLE. NOTHING STRETCHED", Dim, Back);
            Floor(g, 0, 26, width / T + 1, 5);
            int ground = 26 + 3 * T + 8;
            int x = 20;
            var figure = HeroArt.Figure(hero);
            g.Draw(figure, x - figure.Width / 2, ground + 6 - figure.Height);
            g.Ring(x, ground + 1, World.PlayerRadius * T, Yellow);
            g.Small(x - 7, 26 + 5 * T + 4, "YOU", Text, Back);
            x += 30;
            foreach (var kind in kinds)
            {
                var picture = Picture(kind, 0);
                int slot = Math.Max(44, picture.Width + 14);
                int cx = x + slot / 2;
                Stand(g, picture, kind.Radius, cx, ground, Yellow);
                string label = MonsterSizes.Name(kind.Size).ToUpperInvariant() + (kind.Size == MonsterSize.L ? " 2X2" : kind.Size == MonsterSize.XL ? " 3X3" : "");
                g.Small(cx - MockFont.SmallWidth(label) / 2, 26 + 5 * T + 4, label, Yellow, Back);
                string px = $"{picture.Width}PX", radius = $"R{kind.Radius:0.00}".Replace(',', '.');
                g.Small(cx - MockFont.SmallWidth(px) / 2, 26 + 5 * T + 12, px, Dim, Back);
                g.Small(cx - MockFont.SmallWidth(radius) / 2, 26 + 5 * T + 20, radius, Dim, Back);
                x += slot;
            }
            return g;
        }

        // The ice cave's and the sand cave's kinds (B2b), each beside a player on the floor: both frames, the poses, from
        // behind, and a mid-boss standing dormant (CreatureArt.Dormant) the way the game draws it before it wakes.
        static readonly string[] IceSandKinds = { "frost_wolf", "frost_beetle", "frost_lord", "yeti", "frost_mammoth", "bomb_scarab", "sandstone_beetle", "scorpion_queen", "great_worm", "temple_guardian" };

        // The crystal cave's and the underground lake's (B3b), after the kinds they had: a mimic's dormant picture is its
        // clump of crystals.
        static readonly string[] CrystalLakeKinds = { "crystal_bat", "crystal_wisp", "crystal_mimic", "crystal_shooter", "prism_wisp", "crystal_tortoise", "geode_golem",
            "lake_crab", "tide_wisp", "tentacle", "mud_slime", "tide_chief", "great_crab", "moss_turtle" };
        // B4b: the lava fields' six and the abyss's six (and the splitter's halves), smallest first in each.
        static readonly string[] LavaVoidKinds = { "fire_bat", "magma_slime", "salamander", "obsidian_beetle", "forge_slime", "magma_hurler", "forge_keeper",
            "void_mote", "void_wisp", "abyss_stalker", "void_eye", "star_slime", "void_shard", "shadow_stalker", "void_splitter", "gate_warden" };

        static PixelImage Kinds(MonsterData monsters, PixelImage hero, string[] ids, string title)
        {
            var kinds = ids.Select(monsters.Find).ToList();
            var columns = new (string Label, int Frame, MonsterView View, MonsterPose Pose, bool Dormant)[]
            {
                ("IDLE 1", 0, MonsterView.Side, MonsterPose.Idle, false), ("IDLE 2", 1, MonsterView.Side, MonsterPose.Idle, false),
                ("WINDUP", 0, MonsterView.Side, MonsterPose.Windup, false), ("STRIKE", 0, MonsterView.Side, MonsterPose.Strike, false),
                ("HURT", 0, MonsterView.Side, MonsterPose.Hurt, false), ("BACK", 0, MonsterView.Back, MonsterPose.Idle, false),
                ("DORMANT", 0, MonsterView.Side, MonsterPose.Idle, true),
            };
            var figure = HeroArt.Figure(hero);
            int cellW = Math.Max(30, kinds.Max(k => Picture(k, 1, MonsterView.Side, MonsterPose.Windup).Width) + 6);
            var rows = kinds.Select(k => Math.Max(figure.Height, kinds.Where(o => o == k).Max(o => Picture(o, 0, MonsterView.Side, MonsterPose.Strike).Height)) + 14).ToList();
            var g = new PixelImage(8 + 24 + columns.Length * cellW, 18 + rows.Sum());
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(6, 4, title, Text, Back);
            int top = 16;
            for (int r = 0; r < kinds.Count; r++)
            {
                int bottom = top + rows[r] - 6;
                for (int x = 0; x < g.Width; x += T)
                    g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x / T + r) % TileArt.Variants)), x, bottom - 10);
                g.Draw(figure, 6, bottom - figure.Height);
                for (int c = 0; c < columns.Length; c++)
                {
                    var (label, frame, view, pose, dormant) = columns[c];
                    if (dormant && !CreatureArt.Sleeps(kinds[r].Id)) continue;
                    var picture = Picture(kinds[r], frame, view, pose);
                    if (dormant) picture = CreatureArt.Dormant(kinds[r].Id, picture);
                    int cx = 32 + c * cellW + cellW / 2;
                    g.Draw(picture, cx - picture.Width / 2, bottom - picture.Height);
                    if (r == 0) g.Small(cx - MockFont.SmallWidth(label) / 2, 10, label, Dim, Back);
                }
                g.Small(32, top, kinds[r].Id.ToUpperInvariant().Replace('_', ' '), Yellow, Back);
                top += rows[r];
            }
            return g;
        }

        // The big kinds: both idle frames, then the poses, from the side and from behind.
        static PixelImage Poses(MonsterData monsters)
        {
            var big = monsters.Monsters.Where(m => m.Big).ToList();
            var cells = new List<(string Label, MonsterDefinition Kind, int Frame, MonsterView View, MonsterPose Pose)>();
            int cellW = big.Max(k => Picture(k, 1, MonsterView.Side, MonsterPose.Windup).Width) + 8;
            int rowH = big.Max(k => Picture(k, 0, MonsterView.Side, MonsterPose.Strike).Height) + 18;
            var columns = new (string Label, int Frame, MonsterView View, MonsterPose Pose)[]
            {
                ("IDLE 1", 0, MonsterView.Side, MonsterPose.Idle), ("IDLE 2", 1, MonsterView.Side, MonsterPose.Idle),
                ("WINDUP", 0, MonsterView.Side, MonsterPose.Windup), ("STRIKE", 0, MonsterView.Side, MonsterPose.Strike),
                ("HURT", 0, MonsterView.Side, MonsterPose.Hurt), ("BACK", 0, MonsterView.Back, MonsterPose.Idle),
            };
            var g = new PixelImage(8 + columns.Length * cellW, 20 + big.Count * rowH);
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(6, 4, "BIG BODIES: FRAMES AND POSES, WHOLE PIXELS (CREATUREART.BIG)", Text, Back);
            for (int r = 0; r < big.Count; r++)
            {
                int bottom = 20 + (r + 1) * rowH - 10;
                for (int c = 0; c < columns.Length; c++)
                {
                    var (label, frame, view, pose) = columns[c];
                    var picture = Picture(big[r], frame, view, pose);
                    int cx = 8 + c * cellW + cellW / 2;
                    g.Draw(picture, cx - picture.Width / 2, bottom - picture.Height);
                    if (r == big.Count - 1) g.Small(cx - MockFont.SmallWidth(label) / 2, bottom + 3, label, Dim, Back);
                }
            }
            return g;
        }

        // An elite leader as it was (1.3 times, its pixels uneven) and as it is now (a gold ring, CreatureArt.Crowned).
        static PixelImage Leaders(MonsterData monsters)
        {
            var kinds = new[] { "slime", "myconid", "cave_beetle", "glow_bat" }.Select(monsters.Find).ToList();
            int cellW = 70;
            var g = new PixelImage(10 + kinds.Count * cellW, 96);
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(6, 4, "ELITE LEADER: PLAIN  /  OLD 1.3X STRETCH  /  NOW: GOLD RING", Text, Back);
            Floor(g, 0, 14, g.Width / T + 1, 4);
            for (int i = 0; i < kinds.Count; i++)
            {
                var plain = Picture(kinds[i], 0);
                var stretched = Stretch(plain, 1.3f);
                var ringed = CreatureArt.Crowned(plain, Gold);
                int left = 10 + i * cellW, feet = 14 + 3 * T + 6;
                int x = left;
                foreach (var picture in new[] { plain, stretched, ringed })
                {
                    g.Draw(picture, x, feet - picture.Height);
                    x += picture.Width + 3;
                }
                g.Small(left, 14 + 4 * T + 4, kinds[i].Id.ToUpperInvariant().Replace('_', ' '), Dim, Back);
            }
            return g;
        }

        // Nearest-neighbour scaling by a share, the way a 1.3 scale on the screen lands on its pixels.
        static PixelImage Stretch(PixelImage image, float share)
        {
            int w = (int)MathF.Round(image.Width * share), h = (int)MathF.Round(image.Height * share);
            var result = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) result[x, y] = image[(int)(x / share), (int)(y / share)];
            return result;
        }

        // A room with three ways east (one, two and three tiles wide): each tile shaded by the room a body has there, and
        // at each way's mouth and inside it the bodies that fit (green ring) and those that wait at the mouth (red).
        static PixelImage ClearancePicture(MonsterData monsters, PixelImage hero)
        {
            string[] rows =
            {
                "##############################",
                "#..........###################",
                "#..........###################",
                "#.............................",
                "#..........###################",
                "#..........###################",
                "#.............................",
                "#.............................",
                "#..........###################",
                "#..........###################",
                "#.............................",
                "#.............................",
                "#.............................",
                "#..........###################",
                "#..........###################",
                "##############################",
            };
            var map = new TileMap(rows[0].Length, rows.Length, TileType.Floor);
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] == '#') map[x, rows.Length - 1 - r] = TileType.Wall;
            var clearance = new Clearance(map);
            int top = 22;
            var g = new PixelImage(map.Width * T, top + map.Height * T + 30);
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(6, 4, "ROOM AT EACH TILE (CLEARANCE): BRIGHTER = MORE. GREEN: FITS AND WALKS ON, RED: WAITS AT THE MOUTH", Text, Back);
            g.Small(6, 12, "WAYS 1, 2, 3 TILES WIDE. A BIG BODY NEEDS ITS RADIUS + 0.2 OF ROOM (L 0.95, XL 1.5)", Dim, Back);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int px = x * T, py = top + (map.Height - 1 - y) * T;
                    if (map.BlocksMonsters(x, y))
                    {
                        g.Fill(px, py, T, T, Rock);
                        if (!map.BlocksMonsters(x, y - 1)) g.Fill(px, py + T - 3, T, 3, RockTop);
                        continue;
                    }
                    g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x * 7 + y * 3) % TileArt.Variants)), px, py);
                    float room = clearance.At(x, y) / Clearance.Cap;
                    g.BlendFill(px, py, T, T, new Rgba((int)(40 + 200 * room), (int)(60 + 140 * room), 255, 70));
                    string value = clearance.At(x, y).ToString("0.0").Replace(',', '.');
                    if (x >= 11 && x % 3 == 0) g.Small(px + 2, py + 5, value, Text);
                }
            }
            var big = monsters.Find("big_slime");
            var king = monsters.Find("slime_king");
            var slime = monsters.Find("slime");
            // Ways: one tile (y 12), two (y 8..9), three (y 3..5), in map rows from the top 3, 6-7, 10-12.
            var bodies = new (MonsterDefinition Kind, float X, float Y)[]
            {
                (slime, 16.5f, 12.5f), (big, 9.5f, 12.5f),
                (big, 18f, 9f), (king, 8.6f, 9f),
                (king, 20f, 4.5f),
            };
            foreach (var (kind, bx, by) in bodies)
            {
                int cx = (int)MathF.Round(bx * T), cy = top + (int)MathF.Round((map.Height - by) * T);
                bool fits = clearance.Fits(TileMap.Cell(bx), TileMap.Cell(by), kind.Radius) && bx > 11;
                Stand(g, Picture(kind, 0), kind.Radius, cx, cy, fits ? Green : Red);
            }
            g.Small(6, g.Height - 22, "1 TILE: M PASSES, L WAITS.  2 TILES: L PASSES, XL WAITS.  3 TILES: XL PASSES.", Yellow, Back);
            g.Small(6, g.Height - 12, "SPAWN: L ONLY IN ROOMS OF RADIUS 5+, XL ONLY WITH 7X7 OPEN FLOOR, BOTH TIED TO THEIR ROOM + 3", Dim, Back);
            return g;
        }
    }
}
