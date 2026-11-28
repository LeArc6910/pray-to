using System;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // --only weapons (Docs/Weapons.md 묶음 B): a row a weapon kind, the iron one: its icon, the hero standing with it side
    // on and facing down, and the strike frame of each of the three combo steps (side on, three-quarters, side on), so
    // the held shapes can be looked at before the kinds get motions of their own (out/weapons_held.png).
    static class WeaponPreview
    {
        const int Scale = 3;
        static readonly Rgba Tunic = Rgba.Hex("#3F7FD8");
        static readonly string[] Kinds = { "sword", "dagger", "greatsword", "spear", "hammer", "bow", "crossbow", "throwing", "staff", "wand", "orb" };

        public static void Write(string output, LootData loot)
        {
            const int cell = HeroArt.Size;
            var columns = new (Look Look, HeroMove Move)[]
            {
                (Look.Side, HeroMove.Stand), (Look.Down, HeroMove.Stand), (Look.Side, HeroMove.Attack1), (Look.DownSide, HeroMove.Attack2), (Look.Side, HeroMove.Attack3),
            };
            var sheet = new PixelImage((columns.Length + 1) * cell, Kinds.Length * cell);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++) sheet[x, y] = (x / cell + y / cell) % 2 == 0 ? Rgba.Hex("#3A3240") : Rgba.Hex("#332C3A");
            for (int r = 0; r < Kinds.Length; r++)
            {
                string kind = Kinds[r];
                var item = loot.Bases.FirstOrDefault(b => b.Id == $"iron_{(kind == "throwing" ? "boomerang" : kind)}");
                var gear = item != null ? ItemArt.Held(item) : HeroGear.PlainKind(kind);
                if (item != null)
                {
                    var icon = ItemArt.Gear(item);
                    sheet.Draw(icon, (cell - icon.Width) / 2, r * cell + (cell - icon.Height) / 2);
                }
                for (int c = 0; c < columns.Length; c++)
                {
                    var (look, move) = columns[c];
                    int frame = 0;
                    if (move != HeroMove.Stand) frame = HeroArt.StrikeFrame(move, gear.Style);
                    sheet.Draw(HeroArt.Draw(look, move, frame, gear, Tunic), (c + 1) * cell, r * cell);
                }
            }
            var big = new PixelImage(sheet.Width * Scale, sheet.Height * Scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / Scale, y / Scale];
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "weapons_held.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 무기 종류 {Kinds.Length}줄 (아이콘, 서기 옆·아래, 연타 1·2·3단의 타격 장)");
            WriteCombos(output, loot);
            WriteShots(output);
        }

        // out/weapons_shots.png: the kinds' own shots beside the arrow and the ball, tinted as the game tints them
        // (a bow's and a crossbow's a little, a magic one in its colour), the boomerang a quarter turn at a time.
        static void WriteShots(string output)
        {
            var magic = Rgba.Hex("#B07CFF");
            var shots = new (PixelImage Image, Rgba Tint, float Share)[]
            {
                (ItemArt.Arrow, magic, 0), (ItemArt.CrossbowBolt, magic, 0), (ItemArt.BoomerangShot, magic, 0),
                (ItemArt.Orb, magic, 1), (ItemArt.OrbShot, magic, 1), (ItemArt.Spark, magic, 1),
            };
            const int cell = 20, scale = 6;
            var sheet = new PixelImage(cell * (shots.Length + 3), cell);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++) sheet[x, y] = (x / cell) % 2 == 0 ? Rgba.Hex("#3A3240") : Rgba.Hex("#332C3A");
            int column = 0;
            void Put(PixelImage image, Rgba tint, float share)
            {
                var tinted = new PixelImage(image.Width, image.Height);
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        var c = image[x, y];
                        tinted[x, y] = c.A == 0 ? c : new Rgba(c.R * (255 - (int)(share * (255 - tint.R))) / 255, c.G * (255 - (int)(share * (255 - tint.G))) / 255, c.B * (255 - (int)(share * (255 - tint.B))) / 255, c.A);
                    }
                sheet.Draw(tinted, column * cell + (cell - image.Width) / 2, (cell - image.Height) / 2);
                column++;
            }
            foreach (var (image, tint, share) in shots) Put(image, tint, share);
            // The boomerang turned as it spins.
            var turned = ItemArt.BoomerangShot;
            for (int t = 0; t < 3; t++)
            {
                var next = new PixelImage(turned.Height, turned.Width);
                for (int y = 0; y < turned.Height; y++)
                    for (int x = 0; x < turned.Width; x++) next[turned.Height - 1 - y, x] = turned[x, y];
                turned = next;
                Put(turned, magic, 0);
            }
            var big = new PixelImage(sheet.Width * scale, sheet.Height * scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / scale, y / scale];
            string path = Path.Combine(output, "weapons_shots.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 화살, 석궁 볼트, 부메랑, 구슬, 오브 탄, 완드 불꽃, 도는 부메랑");
        }

        // out/weapons_combo.png: a row a kind with motions of its own, side on: for each of the three combo steps the
        // frame a chained blow starts from, the one before the blow, the blow, the hold after it and the way home,
        // with the step's length and the time to its blow (the attack's time between blows from weapon_kinds.csv).
        static readonly string[] Own = { "sword", "dagger", "greatsword", "spear", "hammer", "crossbow", "throwing", "wand", "orb" };

        static void WriteCombos(string output, LootData loot)
        {
            const int cell = HeroArt.Size, per = 5;
            var sheet = new PixelImage((1 + 3 * per) * cell, Own.Length * cell);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++)
                {
                    int step = Math.Max(0, x / cell - 1) / per;
                    sheet[x, y] = x < cell ? Rgba.Hex("#2A2430") : (step % 2 == 0) == ((y / cell) % 2 == 0) ? Rgba.Hex("#3A3240") : Rgba.Hex("#332C3A");
                }
            var lines = new System.Collections.Generic.List<string>();
            for (int r = 0; r < Own.Length; r++)
            {
                string kind = Own[r];
                var item = loot.Bases.FirstOrDefault(b => b.Id == $"iron_{(kind == "throwing" ? "boomerang" : kind)}");
                var gear = item != null ? ItemArt.Held(item) : HeroGear.PlainKind(kind);
                if (item != null)
                {
                    var icon = ItemArt.Gear(item);
                    sheet.Draw(icon, (cell - icon.Width) / 2, r * cell + (cell - icon.Height) / 2);
                }
                var info = $"{kind,-11}";
                for (int s = 0; s < 3; s++)
                {
                    var move = (HeroMove)((int)HeroMove.Attack1 + s);
                    var style = gear.Style;
                    int count = HeroArt.FramesOf(move, style), strike = HeroArt.StrikeFrame(move, style);
                    int chain = HeroArt.FrameAt(move, style, HeroArt.ChainSeconds(move, style) + 1e-4f);
                    int after = Math.Min(count - 1, strike + Math.Max(1, (count - strike) / 4));
                    int home = Math.Min(count - 1, strike + Math.Max(2, (count - strike) * 2 / 3));
                    var frames = new[] { chain, Math.Max(chain, strike - 1), strike, after, home };
                    for (int c = 0; c < per; c++)
                        sheet.Draw(HeroArt.Draw(Look.Side, move, frames[c], gear, Tunic), (1 + s * per + c) * cell, r * cell);
                    info += $"  {s + 1}단 {HeroArt.SecondsOf(move, style):0.00}s 타격 {HeroArt.StrikeSeconds(move, style):0.000}s";
                }
                lines.Add(info);
            }
            var big = new PixelImage(sheet.Width * 3, sheet.Height * 3);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / 3, y / 3];
            string path = Path.Combine(output, "weapons_combo.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 종류마다 연타 1·2·3단 (이어 시작, 타격 앞, 타격, 멈춤, 돌아감)");
            foreach (var line in lines) Console.WriteLine(line);
        }
    }
}
