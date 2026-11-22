using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The talk box's portraits for a person to look at (out/portraits/, --only portraits): sheet.png has a row for
    // every person of the story and every stall keeper (their sprite in the world first, then each expression) and a
    // last row for someone unknown; order.txt lists the rows and columns; close_N.png are the same rows four at a
    // time and bigger; dialogue.png is a talk box as the mockups draw it (the UI at 2, the portrait at 3). Fails if
    // someone in the sheets has no portrait of their own.
    static class PortraitPreview
    {
        static readonly Rgba Ground = Rgba.Hex("#0A0A0F");

        public static bool Write(string output, StoryData story, NpcData npcs)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "portraits")).FullName;
            var rows = new List<(string Id, string Name, string Look, Rgba Color, PixelImage Sprite)>();
            foreach (var c in story.Characters) rows.Add((c.Id, c.Name, c.Look, Rgba.Hex(c.Color), PeopleArt.Look(c.Look, Rgba.Hex(c.Color), 0)));
            foreach (var n in npcs.Npcs)
            {
                var color = Rgba.Hex(n.Color);
                rows.Add((n.Id, n.Name, null, color, PeopleArt.Person(n.Id, color, 0) ?? HeroArt.Figure(HeroArt.Draw(Look.Down, HeroMove.Stand, 0, HeroGear.None, color))));
            }
            var missing = rows.Where(r => PortraitArt.People.All(p => p.Id != r.Id)).Select(r => r.Id).ToList();
            var stale = rows.Where(r => PortraitArt.People.Any(p => p.Id == r.Id && (Rgba.Hex(p.Color).R != r.Color.R || Rgba.Hex(p.Color).G != r.Color.G || Rgba.Hex(p.Color).B != r.Color.B
                || r.Look != null && p.Look != r.Look))).Select(r => r.Id).ToList();
            foreach (string id in missing) Console.Error.WriteLine($"초상이 없는 인물: {id} (PortraitArt.People)");
            foreach (string id in stale) Console.Error.WriteLine($"초상의 모습·색이 시트와 다른 인물: {id} (PortraitArt.People)");
            rows.Add(("(unknown)", "모르는 인물", "villager", Rgba.Hex("#8A7A6A"), null));

            var cells = rows.Select(r => PortraitArt.Expressions.Select(e => r.Sprite == null ? PortraitArt.Of("nobody", e) : PortraitArt.Of(r.Id, r.Look, r.Color, e)).ToList()).ToList();
            Png.Save(Sheet(rows.Select(r => (r.Sprite, r.Color)).ToList(), cells, 0, rows.Count).Times(2), Path.Combine(folder, "sheet.png"));
            for (int i = 0; i * 4 < rows.Count; i++)
                Png.Save(Sheet(rows.Select(r => (r.Sprite, r.Color)).ToList(), cells, i * 4, Math.Min(4, rows.Count - i * 4)).Times(4), Path.Combine(folder, $"close_{i + 1}.png"));

            var lines = new List<string>
            {
                "sheet.png: 한 줄에 한 사람. 맨 왼쪽은 월드의 인물 그림, 그 오른쪽으로 표정 순서대로.",
                "열: 월드 그림, " + string.Join(", ", PortraitArt.Expressions),
                $"초상 크기: {PortraitArt.Width}x{PortraitArt.Height} 아트 픽셀 (sheet.png는 2배, close_N.png는 4배, dialogue.png는 3배)",
                "",
            };
            for (int i = 0; i < rows.Count; i++) lines.Add($"{i + 1,2}줄 (close_{i / 4 + 1}.png의 {i % 4 + 1}째): {rows[i].Id} — {rows[i].Name}");
            File.WriteAllLines(Path.Combine(folder, "order.txt"), lines);

            Png.Save(Dialogue(), Path.Combine(folder, "dialogue.png"));
            Console.WriteLine($"초상 {rows.Count}명 x 표정 {PortraitArt.Expressions.Length}개 -> {folder}");
            return missing.Count == 0 && stale.Count == 0;
        }

        // Rows of portraits, each on a ground of the person's colour as the talk box has it.
        static PixelImage Sheet(List<(PixelImage Sprite, Rgba Color)> people, List<List<PixelImage>> cells, int first, int count)
        {
            const int Gap = 3, Side = 26;
            int cw = PortraitArt.Width + Gap, ch = PortraitArt.Height + Gap;
            var g = new PixelImage(Side + cw * PortraitArt.Expressions.Length + Gap, ch * count + Gap);
            g.Fill(0, 0, g.Width, g.Height, Ground);
            for (int r = 0; r < count; r++)
            {
                var (sprite, color) = people[first + r];
                int y = Gap + r * ch;
                if (sprite != null) g.Draw(sprite, (Side - sprite.Width) / 2, y + PortraitArt.Height - sprite.Height);
                for (int c = 0; c < PortraitArt.Expressions.Length; c++) Slot(g, Side + Gap + c * cw - Gap + Gap, y, color, cells[first + r][c], 1);
            }
            return g;
        }

        static void Slot(PixelImage g, int x, int y, Rgba color, PixelImage portrait, int scale)
        {
            int w = portrait.Width * scale, h = portrait.Height * scale;
            for (int j = 0; j < h; j++) g.Fill(x, y + j, w, 1, color.Scale(0.5f).Mix(Rgba.Hex("#120E12"), 0.25f + 0.6f * j / h));
            g.Draw(portrait.Times(scale), x, y);
        }

        // Four talk boxes one over another, each a different speaker and face.
        static PixelImage Dialogue()
        {
            var talks = new (string Id, Expression Face, string Name, string[] Words)[]
            {
                ("merchant", Expression.Happy, "DORA THE MERCHANT", new[] { "WELCOME BACK, TRAVELLER. THE ROAD TO THE ROYAL CITY", "IS OPEN AGAIN, AND I HAVE NEW SEEDS IN." }),
                ("hero", Expression.Angry, "KYLE THE HERO", new[] { "THE QUEEN KNOWS, THEN.", "... STILL, SHE HAS NOT SENT THE ARMY YET." }),
                ("lu", Expression.Sad, "LU", new[] { "UNCLE CLEANS HIS SWORD AT NIGHT THESE DAYS.", "IT SCARES ME." }),
                ("demon_elder", Expression.Thinking, "UR THE DEMON ELDER", new[] { "A HUMAN, HERE. THAT IS RARE.", "THE KING'S BREATH STILL LINGERS IN THE CAVES." }),
            };
            const int BoxW = 452, BoxH = 98, Pad = 8;
            var skin = new MockSkin(MockStyle.R);
            var ui = new PixelImage(BoxW + Pad * 2, (BoxH + 16 + Pad) * talks.Length + Pad);
            ui.Fill(0, 0, ui.Width, ui.Height, Rgba.Hex("#2A2622"));
            var slots = new List<(int X, int Y, int W, int H)>();
            for (int i = 0; i < talks.Length; i++)
            {
                var (id, _, name, words) = talks[i];
                var color = Rgba.Hex(PortraitArt.People.First(p => p.Id == id).Color);
                int bx = Pad, by = Pad + i * (BoxH + 16 + Pad);
                skin.Window(ui, bx, by, BoxW, BoxH + 16, MockKind.Talk, name, null, color, close: false);
                int px = bx + 8, py = by + 16 + 7, pw = 66, ph = BoxH - 14;
                skin.Well(ui, px, py, pw, ph, MockKind.Talk);
                for (int j = 1; j < ph - 1; j++) ui.Fill(px + 1, py + j, pw - 2, 1, color.Scale(0.5f).Mix(Rgba.Hex("#120E12"), 0.25f + 0.6f * j / ph));
                ui.Box(px, py, pw, ph, MockSkin.Black);
                ui.Box(px + 1, py + 1, pw - 2, ph - 2, Ramp.Of(color).Main);
                slots.Add((px, py, pw, ph));
                for (int l = 0; l < words.Length; l++) ui.Text(px + pw + 10, py + 6 + l * 12, words[l], Rgba.Hex("#EFE4CC"), MockSkin.Black);
            }
            var g = ui.Times(2);
            for (int i = 0; i < talks.Length; i++)
            {
                var (x, y, w, h) = slots[i];
                var portrait = PortraitArt.Of(talks[i].Id, talks[i].Face).Times(3);
                // Standing on the slot's floor, inside its rim.
                g.Draw(portrait, x * 2 + (w * 2 - portrait.Width) / 2, (y + h - 2) * 2 - portrait.Height);
            }
            return g;
        }
    }
}
