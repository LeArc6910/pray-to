using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The bag and the HUD of U6 and U7 (Docs/UiRework.md) laid out as the game lays them out, at their size in points (one
    // pixel of the picture two points, doubled at the end as the other UI pictures are). Positions follow
    // InventoryWindow.BagLayout and AreaOf, GameRoot.DrawHud, DeathCardArea, BannerRoom and FoldedTrackerArea and
    // ScreenStack.HubLayout, rounded to even points. The writing is the preview's Latin font.
    //  - bag_hud.png: a PC 1280 x 720: the bag with a picked sword's card; the HUD in play (bars with their marks, a buff,
    //    the goal list with [fold], a banner beside the columns, the "got" lines); a boss fight at low health (red edges,
    //    the boss bar with its skull and phase pips); the fallen card over the journal left open; the Esc menu;
    //  - bag_hud_phone.png: a phone held upright (390 x 844): the bag sheet with its card, the HUD with the folded goal line
    //    and a buff, the hub open with the level's banner under it, the fallen card over the bag; held sideways (740 x 360):
    //    the bag with its tab rail, the HUD at low health with the boss bar.
    static partial class UiPreview
    {
        static readonly Rgba Red = Rgba.Hex("#E64B3C");

        static PixelImage BagHud(LootData loot)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1300, 1120);
            Floor(g, 0, 0, g.Width, g.Height, 19);
            var things = Things(loot);
            g.Small(10, 2, "U6 BAG AND U7 HUD ON A PC 1280X720, ONE PIXEL TWO POINTS: BAG WITH A CARD; HUD IN PLAY; BOSS AT LOW HEALTH; FALLEN OVER A WINDOW; ESC MENU", Dim);
            // 1. The bag open beside the world, a sword picked.
            World(g, 5, 10, 640, 360, 1);
            Hud(g, 5 + 5, 10 + 5, 170, compact: true, buffs: false, low: false);
            Minimap(g, 5 + 640 - 65, 15, 60);
            Bag(g, 5 + 310, 10 + 5, 260, 350, things, loot, phone: false, low: false, filter: 0, card: true);
            // 2. The HUD in play.
            World(g, 655, 10, 640, 360, 2);
            int left = Hud(g, 655 + 5, 15, 170, compact: false, buffs: true, low: false);
            Tracker(g, 655 + 5, left + 3, 170, folded: false, touch: false);
            Minimap(g, 655 + 640 - 65, 15, 60);
            Info(g, 655 + 640 - 5, 15 + 62);
            Banner(g, 655 + 180, 10 + 90, 640 - 180 - 70, "LEVEL 7! HEALTH +12 - DAMAGE +3%");
            Got(g, 655 + 7, 10 + 300, new[] { "GOT: COPPER ORE X3", "GOT: SLIME GEL" });
            SkillRow(g, 655, 10, 640, 360, touch: false);
            // 3. A boss fight at low health.
            World(g, 5, 380, 640, 360, 3);
            Hud(g, 10, 385, 170, compact: false, buffs: true, low: true);
            Tracker(g, 10, 385 + 70, 170, folded: true, touch: false);
            Minimap(g, 5 + 640 - 65, 385, 60);
            BossBar(g, 5 + 185, 385 + 5, 640 - 185 - 75, phase: 2, phases: 3, share: 0.62f);
            SkillRow(g, 5, 380, 640, 360, touch: false);
            Edges(g, 5, 380, 640, 360, 150);
            // 4. Fallen with the journal open.
            World(g, 655, 380, 640, 360, 4);
            g.BlendFill(655, 380, 640, 360, new Rgba(0, 0, 0, 178));
            Window(g, 655 + 60, 385, 520, 350, UiKind.Journal, "JOURNAL (J)", UiIcons.Book, default);
            Parch(g, 655 + 66, 385 + UiArt.PanelBorder + UiArt.HeadHeight + 26, 508, 350 - UiArt.HeadHeight - 36);
            Fallen(g, 655, 380, 640, 360, phone: false);
            // 5. The Esc menu over the world (the hub in the middle), and the bag at low height (a PC window 800x600).
            World(g, 5, 750, 640, 360, 5);
            Hud(g, 10, 755, 170, compact: false, buffs: false, low: false);
            Minimap(g, 5 + 640 - 65, 755, 60);
            Hub(g, 5 + 320 - 101, 750 + 180 - 28, 4, touch: false);
            World(g, 655, 750, 400, 300, 6);
            Hud(g, 660, 755, 168, compact: true, buffs: false, low: false);
            Minimap(g, 655 + 400 - 65, 755, 60);
            Bag(g, 655 + 70, 755 + 54, 260, 236, things, loot, phone: false, low: false, filter: 2, card: true);
            g.Small(1060, 760, "800X600: THE BAG UNDER THE", Dim);
            g.Small(1060, 768, "HUD'S BARS, [MATERIALS] CHIP,", Dim);
            g.Small(1060, 776, "A STONE PICKED: [PLACE] FIRST", Dim);
            return Finish(g);
        }

        static PixelImage BagHudPhone(LootData loot)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1210, 640);
            Floor(g, 0, 0, g.Width, g.Height, 23);
            var things = Things(loot);
            g.Small(6, 2, "PHONE 390X844: BAG SHEET WITH A CARD; HUD WITH THE FOLDED GOAL LINE; HUB WITH THE BANNER UNDER IT; FALLEN OVER THE BAG. 740X360: BAG RAIL; LOW HEALTH", Dim);
            for (int i = 0; i < 4; i++)
            {
                int px = 6 + i * 201;
                Phone(g, px, 12, 195, 422);
                int left = Hud(g, px + 5, 17, 110, compact: i == 0 || i == 3, buffs: i == 1, low: false);
                Minimap(g, px + 195 - 65, 17, 60);
                Button(g, px + 195 - 65, 82, 28, 20, "BAG");
                Button(g, px + 195 - 33, 82, 28, 20, "MENU", i == 2 ? Press.Primary : Press.Normal);
                switch (i)
                {
                    case 0:
                        Bag(g, px + 5, 12 + 95, 185, 322, things, loot, phone: true, low: false, filter: 0, card: true);
                        break;
                    case 1:
                        Tracker(g, px + 5, left + 3, 110, folded: true, touch: true);
                        SkillRow(g, px, 12, 195, 422, touch: true);
                        Got(g, px + 7, 12 + 300, new[] { "GOT: STONE X4" });
                        break;
                    case 2:
                        Tracker(g, px + 5, left + 3, 110, folded: true, touch: true);
                        Hub(g, px + 195 - 108, 12 + 108, 2, touch: true);
                        Banner(g, px, 12 + 108 + 214, 195, "LEVEL 7!");
                        SkillRow(g, px, 12, 195, 422, touch: true);
                        break;
                    default:
                        Bag(g, px + 5, 12 + 95, 185, 322, things, loot, phone: true, low: false, filter: 0, card: false);
                        Fallen(g, px, 12, 195, 422, phone: true);
                        break;
                }
            }
            // Held sideways: the bag with its rail (the HUD box not drawn while it is open), and low health in a boss fight.
            Phone(g, 812, 12, 370, 180);
            Minimap(g, 812 + 370 - 65, 17, 60);
            Button(g, 812 + 370 - 65, 82, 28, 20, "BAG", Press.Primary);
            Button(g, 812 + 370 - 33, 82, 28, 20, "MENU");
            Bag(g, 812 + 40, 17, 260, 170, things, loot, phone: true, low: true, filter: 0, card: true);
            Phone(g, 812, 210, 370, 180);
            int bottom = Hud(g, 817, 215, 155, compact: false, buffs: true, low: true);
            Tracker(g, 817, bottom + 3, 155, folded: false, touch: true);
            Minimap(g, 812 + 370 - 65, 215, 60);
            BossBar(g, 812 + 165, 215, 370 - 165 - 72, phase: 3, phases: 3, share: 0.2f);
            SkillRow(g, 812, 210, 370, 180, touch: true);
            Edges(g, 812, 210, 370, 180, 150);
            Phone(g, 812, 408, 370, 180);
            Hud(g, 817, 413, 155, compact: false, buffs: false, low: false);
            Minimap(g, 812 + 370 - 65, 413, 60);
            Fallen(g, 812, 408, 370, 180, phone: true);
            return Finish(g);
        }

        // ---- Pieces ----

        sealed class Thing
        {
            public PixelImage Icon;
            public Rgba Edge;
            public string Count;
            public int Kind; // 1 gear, 2 materials, 3 food, 4 put down
        }

        static List<Thing> Things(LootData loot)
        {
            var things = new List<Thing>();
            var random = new Random(7);
            foreach (var item in loot.Bases.Where(b => b.Id.Contains("sword") || b.Id.Contains("helmet") || b.Id.Contains("ring")).Take(7))
                things.Add(new Thing { Icon = ItemArt.Gear(item), Edge = Rarities[random.Next(5)], Kind = 1 });
            foreach (var consumable in loot.Consumables.Take(4))
                things.Add(new Thing { Icon = ItemArt.Consumable(consumable), Edge = Rgba.Hex("#E07070"), Count = (1 + random.Next(6)).ToString(), Kind = 3 });
            foreach (var material in loot.Materials.Where(m => m.Base == null).Take(14))
                things.Add(new Thing { Icon = ItemArt.Material(material), Edge = Rgba.Hex("#BFBFB8"), Count = (1 + random.Next(40)).ToString(), Kind = material.Places != null ? 4 : 2 });
            return things;
        }

        // A piece of the world under the UI: the floor, a few walls, the hero in the middle.
        static void World(PixelImage g, int x, int y, int w, int h, int salt)
        {
            Phone(g, x, y, w, h);
            var random = new Random(salt);
            for (int i = 0; i < 18; i++) g.Fill(x + random.Next(w - 16), y + random.Next(h - 16), 16, 16, Rgba.Hex("#3A3038"));
            g.Fill(x + w / 2 - 4, y + h / 2 - 8, 8, 12, Rgba.Hex("#3F7FD8"));
            g.Fill(x + w / 2 - 3, y + h / 2 - 12, 6, 5, Rgba.Hex("#F0C8A0"));
        }

        // The HUD's box: a heart, a drop and a star beside the three bars, then the buffs; gives its bottom.
        static int Hud(PixelImage g, int x, int y, int w, bool compact, bool buffs, bool low)
        {
            int h = 7 + 11 + 1 + 8 + 1 + 7 + 4 + (buffs ? 16 : 0) + (compact ? 0 : 10);
            Slice(g, UiArt.Panel, UiArt.PanelBorder, x, y, w, h);
            int bx = x + 6 + 9, bw = w - 12 - 9, by = y + 6;
            g.Draw(UiIcons.Of(UiIcons.Heart, low ? Rgba.Hex("#FF4040") : Rgba.Hex("#F05050")), x + 6, by + 2);
            Meter(g, bx, by, bw, 11, low ? 0.15f : 0.8f, Rgba.Hex("#C83A3A"), low ? "HP 36/240" : "HP 192/240");
            by += 12;
            g.Draw(UiIcons.Of(GrowthArt.Drop, Rgba.Hex("#70A8FF")), x + 6, by);
            Meter(g, bx, by, bw, 8, 0.6f, Rgba.Hex("#3C6CD8"), "MP 60/100");
            by += 9;
            g.Draw(UiIcons.Of(GrowthArt.Point, Rgba.Hex("#FFD060")), x + 6, by);
            Meter(g, bx, by, bw, 7, 0.42f, Rgba.Hex("#E8C04A"), "LV 6 42%");
            by += 9;
            if (buffs)
            {
                int cx = x + 6;
                foreach (var (glyph, color, time, bad) in new[] { (GrowthArt.Flame, Rgba.Hex("#FF9440"), "", true), (MapIcons.Shrine, Rgba.Hex("#FFD966"), "9:41", false), (UiIcons.Heart, Rgba.Hex("#FF8080"), "4:02", false) })
                {
                    int cw = time.Length > 0 ? 20 : 13;
                    Slice(g, UiArt.Slot(bad ? Red : (Rgba?)null), UiArt.SlotBorder, cx, by, cw, 13);
                    var icon = UiIcons.Of(glyph, color);
                    g.Draw(icon, cx + (cw - icon.Width) / 2, by + 1);
                    if (time.Length > 0) g.Small(cx + cw - 1 - MockFont.SmallWidth(time), by + 8, time, Rgba.White, new Rgba(0, 0, 0, 230));
                    cx += cw + 2;
                }
                by += 15;
            }
            if (!compact) g.Small(x + 6, by + 1, "SKILL POINT 1 (K-P)", Ink);
            return y + h;
        }

        static void Meter(PixelImage g, int x, int y, int w, int h, float share, Rgba fill, string text)
        {
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            g.Fill(x + 1, y + 1, (int)((w - 2) * share), h - 2, fill);
            g.Small(x + w / 2 - MockFont.SmallWidth(text) / 2, y + (h - 5) / 2, text, Rgba.White, new Rgba(0, 0, 0, 220));
        }

        // The goal list: folded to a line (the followed one's flag, its title cut, "+2", the book, [unfold] on a PC), or
        // open with [fold] in its corner.
        static void Tracker(PixelImage g, int x, int y, int w, bool folded, bool touch)
        {
            var gold = Rgba.Hex("#FFD966");
            if (folded)
            {
                int h = touch ? 20 : 12;
                g.BlendFill(x + 1, y + 1, w, h, new Rgba(0, 0, 0, 60));
                Slice(g, UiArt.Panel, UiArt.PanelBorder, x, y, w, h);
                int right = x + w - 4;
                if (!touch)
                {
                    g.Draw(UiIcons.Of(new[] { "##...##", "#oo#oo#", ".#ooo#.", "..#o#..", "...#..." }, Rgba.Hex("#E6CC99")), right - 7, y + (h - 5) / 2);
                    right -= 12;
                }
                var book = UiIcons.Of(UiIcons.Book, Rgba.Hex("#F2D899"));
                g.Draw(book, right - book.Width, y + (h - book.Height) / 2);
                right -= book.Width + 4;
                g.Text(right - MockFont.Width("+2"), y + (h - 7) / 2, "+2", Ink, Shadow);
                right -= MockFont.Width("+2") + 4;
                var flag = UiIcons.Of(MapIcons.Pin, Red);
                g.Draw(flag, x + 4, y + (h - flag.Height) / 2);
                g.Text(x + 6 + flag.Width, y + (h - 7) / 2, Clip("REQUEST: DORA - COPPER 3/5", right - x - 8 - flag.Width), gold, Shadow);
                return;
            }
            int hh = 52;
            g.BlendFill(x + 1, y + 1, w, hh, new Rgba(0, 0, 0, 60));
            Slice(g, UiArt.Panel, UiArt.PanelBorder, x, y, w, hh);
            g.Draw(UiIcons.Of(new[] { "...#...", "..#o#..", ".#ooo#.", "#oo#oo#", "##...##" }, Rgba.Hex("#E6CC99")), x + w - (touch ? 14 : 10), y + (touch ? 7 : 4));
            g.Text(x + 5, y + 5, "GOAL 3/30", gold, Shadow);
            g.Small(x + 5, y + 14, "STONE FLOOR X4 (2/4)", Ink);
            g.Small(x + 5, y + 21, "REWARD: COPPER ORE 3", Ink);
            g.Text(x + 5, y + 30, "STORY: MINA", gold, Shadow);
            g.Small(x + 5, y + 39, "MUSHROOM 1/3", Ink);
        }

        // The day and the place under the minimap, right-aligned.
        static void Info(PixelImage g, int right, int y)
        {
            foreach (string line in new[] { "DAY 3 08:20 MORNING", "MUSHROOM WOODS" })
            {
                g.Small(right - MockFont.SmallWidth(line), y, line, Ink, new Rgba(0, 0, 0, 200));
                y += 7;
            }
        }

        static void Banner(PixelImage g, int x, int y, int w, string text)
        {
            int tw = MockFont.Width(text, 2);
            int cx = x + w / 2;
            g.BlendFill(cx - tw / 2 - 6, y - 2, tw + 12, 18, new Rgba(0, 0, 0, 128));
            g.Text(cx - tw / 2, y + 1, text, Rgba.Hex("#FFD966"), Shadow, scale: 2);
        }

        static void Got(PixelImage g, int x, int y, string[] lines)
        {
            foreach (string line in lines)
            {
                g.Text(x, y, line, Ink, Shadow);
                y += 9;
            }
        }

        // The skill keys and quick slots along the bottom right, as SkillBar and Hotbar lay them out roughly.
        static void SkillRow(PixelImage g, int x, int y, int w, int h, bool touch)
        {
            int cell = touch ? 22 : 18;
            for (int i = 0; i < 4; i++) Slot(g, x + w - 6 - (4 - i) * (cell + 3), y + h - 6 - cell, cell, Rgba.Hex("#73B3FF"));
            for (int i = 0; i < 6; i++) Slot(g, x + w - 6 - 4 * (cell + 3) - 4 - (6 - i) * (cell - 4 + 2), y + h - 6 - (cell - 4), cell - 4, null);
        }

        // The boss's bar: a dark strip with a skull, the name and a pip a phase, the bar in its well under it.
        static void BossBar(PixelImage g, int x, int y, int w, int phase, int phases, float share)
        {
            g.BlendFill(x, y, w, 10, new Rgba(13, 5, 5, 150));
            var skull = UiIcons.Of(MapIcons.Skull, Rgba.Hex("#EBD9BF"));
            g.Draw(skull, x + 2, y + 1);
            string name = $"CAVE WARDEN - {phase}/{phases}";
            g.Text(x + w / 2 - MockFont.Width(name) / 2, y + 2, name, Rgba.Hex("#FFE0CC"), Shadow);
            for (int i = 0; i < phases; i++)
            {
                int px = x + w - 2 - phases * 6 + i * 6;
                g.Fill(px, y + 3, 4, 4, Rgba.Hex("#000000"));
                g.Fill(px + 1, y + 4, 2, 2, i + 1 < phase ? Rgba.Hex("#731F19") : i + 1 == phase ? Rgba.Hex("#FF664C") : Rgba.Hex("#594C47"));
            }
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y + 10, w, 9);
            g.Fill(x + 1, y + 11, (int)((w - 2) * share), 7, Rgba.Hex("#B02828"));
        }

        // The screen's edges red, that strong at the very edge (low health, falling).
        static void Edges(PixelImage g, int x, int y, int w, int h, int strength)
        {
            int reach = Math.Max(8, Math.Min(w, h) / 6);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int edge = Math.Min(Math.Min(i, w - 1 - i), Math.Min(j, h - 1 - j));
                    if (edge >= reach) continue;
                    float a = 1 - edge / (float)reach;
                    g.Blend(x + i, y + j, new Rgba(230, 20, 15, (byte)(strength * a * a)));
                }
            }
        }

        // The fallen card: the screen darkened and red at its edges, the card low in the middle with "FALLEN", what did it,
        // when the player gets up, and [GET UP AT HOME].
        static void Fallen(PixelImage g, int x, int y, int w, int h, bool phone)
        {
            g.BlendFill(x, y, w, h, new Rgba(0, 0, 0, 102));
            Edges(g, x, y, w, h, 140);
            int cw = Math.Min(190, w - 12);
            int ch = 75;
            int cy = h < 240 ? y + h - ch - 5 : y + (int)(h * 0.62f) - ch / 2;
            int cx = x + (w - cw) / 2;
            Card(g, cx, cy, cw, ch);
            string title = "FALLEN";
            g.Text(cx + cw / 2 - MockFont.Width(title, 2) / 2, cy + 6, title, Rgba.Hex("#FF6B5C"), Shadow, scale: 2);
            foreach (var (line, row) in new[] { ("FELLED BY A CAVE SLIME", 0), ("UP AT HOME IN A MOMENT", 1) })
                g.Small(cx + cw / 2 - MockFont.SmallWidth(line) / 2, cy + 26 + row * 7, line, Ink);
            int bw = Math.Min(110, cw - 12);
            Button(g, cx + cw / 2 - bw / 2, cy + ch - 26, bw, 22, "GET UP AT HOME", Press.Primary);
        }

        // The hub's panel: the screens' buttons with their icons, [SETTINGS] and [LEAVE].
        static void Hub(PixelImage g, int x, int y, int columns, bool touch)
        {
            var cells = new (string Name, string[] Icon)[]
            {
                ("BAG", UiIcons.Bag), ("SKILLS", UiIcons.Skills), ("TREE", UiIcons.Tree), ("HERO", UiIcons.Person),
                ("JOURNAL", UiIcons.Book), ("MAP", UiIcons.Map), ("SETTINGS", UiIcons.Gear), ("LEAVE", null),
            };
            int rows = (cells.Length + columns - 1) / columns;
            int w = columns * 46 + (columns - 1) * 3 + 8, h = rows * 22 + (rows - 1) * 3 + 8;
            Card(g, x, y, w, h);
            for (int i = 0; i < cells.Length; i++)
            {
                int bx = x + 4 + i % columns * 49, by = y + 4 + i / columns * 25;
                Button(g, bx, by, 46, 22, "");
                var (name, icon) = cells[i];
                if (icon != null)
                {
                    var image = UiIcons.Of(icon, Rgba.Hex("#E6C78C"));
                    g.Draw(image, bx + 23 - image.Width / 2, by + 2);
                    g.Small(bx + 23 - MockFont.SmallWidth(name) / 2, by + 15, name, Ink);
                }
                else g.Text(bx + 23 - MockFont.Width(name) / 2, by + 8, name, Ink, Shadow);
            }
        }

        // The bag (U6): the leather head with the coins, the tabs (a rail on a low screen), the chips and [SORT], the hero
        // among the worn slots (a row of six on a low screen), the one grid with its empty wells, the picked thing's card
        // at the bottom with its buttons first.
        static void Bag(PixelImage g, int x, int y, int w, int h, List<Thing> things, LootData loot, bool phone, bool low, int filter, bool card)
        {
            int top = Window(g, x, y, w, h, UiKind.Bag, phone ? "BAG" : "BAG (TAB)", UiIcons.Bag, default);
            g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), x + w - 50, y + 9);
            g.Text(x + w - 41, y + 9, "1250", Rgba.Hex("#FFF2CC"), Shadow);
            var (ix, iy, iw, bottom) = Inside(x, y, w, h, top);
            int cx = ix, cw = iw, cyTop = iy;
            if (low)
            {
                Button(g, ix, iy, 46, 20, "GEAR", Press.Primary, UiIcons.Bag, tab: true);
                Button(g, ix, iy + 22, 46, 20, "CRAFT", Press.Normal, UiIcons.Hammer, tab: true);
                cx = ix + 49;
                cw = iw - 49;
            }
            else
            {
                int tw = (iw - 2) / 2;
                Button(g, ix, iy, tw, 20, "GEAR", Press.Primary, UiIcons.Bag, tab: true);
                Button(g, ix + tw + 2, iy, tw, 20, "CRAFT", Press.Normal, UiIcons.Hammer, tab: true);
                cyTop = iy + 23;
            }
            // The chips and [SORT].
            int chip = phone ? 20 : 15;
            string[] names = { "ALL", "GEAR", "MATS", "FOOD", "PUT" };
            int each = (cw - 30 - 2 * names.Length) / names.Length;
            for (int i = 0; i < names.Length; i++) Button(g, cx + i * (each + 2), cyTop, each, chip, names[i], i == filter ? Press.Primary : Press.Normal);
            Button(g, cx + cw - 30, cyTop, 30, chip, "SORT");
            int listTop = cyTop + chip + 3;
            // The card at the bottom, as tall as it needs (its buttons first).
            // On a low screen the card stands at the right, as tall as the list (InventoryWindow.BagLayout).
            bool side = low && card;
            int cardH = side ? bottom - listTop : card ? Math.Min(phone ? 92 : 80, bottom - listTop - 55) : 0;
            int listBottom = card && !side ? bottom - cardH - 3 : bottom;
            int fullWidth = cw;
            if (side) cw -= 113;
            int ly = listTop;
            int cell = phone ? 24 : 22;
            var sword = loot.Bases.FirstOrDefault(b => b.Id == "iron_sword") ?? loot.Bases.First();
            Clipped(g, cx, listTop, cw, listBottom - listTop, () =>
            {
                if (low || listBottom - listTop < 165)
                {
                    g.Small(cx, ly, "WORN", Dim);
                    ly += 6;
                    for (int i = 0; i < 6; i++) Slot(g, cx + i * (cell + 2), ly, cell, i == 0 ? Rarities[2] : i < 3 ? Rarities[1] : null, i == 0 ? ItemArt.Gear(sword) : i < 3 ? things[i].Icon : null);
                    ly += cell + 4;
                }
                else
                {
                    int column = cell * 3 + 4;
                    int heroW = Math.Min(cw - 2 * cell - 12, 75);
                    int left = cx + (cw - (2 * cell + heroW + 8)) / 2;
                    int stageX = left + cell + 4;
                    Slice(g, UiArt.Inset, UiArt.InsetBorder, stageX, ly, heroW, column + 4);
                    g.BlendFill(stageX + 1, ly + 1, heroW - 2, column + 2, Rgba.Hex("#3D2E1F").WithAlpha(90));
                    g.BlendFill(stageX + 6, ly + column - 7, heroW - 12, 3, new Rgba(0, 0, 0, 90));
                    int heroScale = Math.Max(2, Math.Min((heroW * 2 - 8) / 24, ((column + 4) * 2 - 8) / 22));
                    Pictures.Add((stageX * 2 + (heroW * 2 - 24 * heroScale) / 2, ly * 2 + ((column + 4) * 2 - 10 - 22 * heroScale) / 2 + 2, GrowthArt.Hero(ItemArt.Held(sword), Rgba.Hex("#3F7FD8")), heroScale));
                    for (int r = 0; r < 3; r++)
                    {
                        Slot(g, left, ly + 2 + r * (cell + 2), cell, r == 0 ? Rarities[2] : r == 1 ? Rarities[1] : null, r == 0 ? ItemArt.Gear(sword) : r == 1 ? things[2].Icon : null);
                        Slot(g, stageX + heroW + 4, ly + 2 + r * (cell + 2), cell, r < 2 ? Rarities[1 + r] : null, r < 2 ? things[3 + r].Icon : null);
                    }
                    ly += column + 8;
                }
                g.Small(cx, ly, "CARRIED 9/24", Dim);
                ly += 7;
                var shown = things.Where(t => filter == 0 || t.Kind == filter).ToList();
                int columns = Math.Max(1, (cw + 2) / (cell + 2));
                int total = (shown.Count + columns - 1) / columns * columns;
                if (filter == 1) total = Math.Max(total, 24);
                for (int i = 0; i < total; i++)
                {
                    int gx = cx + i % columns * (cell + 2), gy = ly + i / columns * (cell + 2);
                    if (i < shown.Count) Slot(g, gx, gy, cell, shown[i].Edge, shown[i].Icon, shown[i].Count, glow: shown[i].Edge.Equals(Rarities[3]) || shown[i].Edge.Equals(Rarities[4]), picked: card && i == (filter == 2 ? 0 : 1));
                    else Slot(g, gx, gy, cell, null);
                }
            });
            if (!card) return;
            int cy = bottom - cardH;
            if (side)
            {
                cx += cw + 3;
                cw = fullWidth - cw - 3;
            }
            Slice(g, UiArt.Inset, UiArt.InsetBorder, cx, cy, cw, cardH);
            int bh = phone ? 20 : 14;
            if (filter == 2)
            {
                var stone = things.FirstOrDefault(t => t.Kind == 2) ?? things[0];
                g.Draw(stone.Icon, cx + 4, cy + 3);
                g.Text(cx + 22, cy + 7, "STONE X12", Ink, Shadow);
                int bx = cx + 4;
                foreach (var (label, bw, press) in new[] { ("PLACE", 30, Press.Primary), ("LOCK", 30, Press.Normal) })
                {
                    Button(g, bx, cy + 20, bw, bh, label, press);
                    bx += bw + 2;
                }
                QuickRow(g, cx + 4, cy + 22 + bh, cw - 8, bh, -1);
                g.Small(cx + 4, cy + 26 + 2 * bh, "A BUILDING BLOCK. PLACES A WALL", Ink);
                return;
            }
            g.Draw(ItemArt.Gear(sword), cx + 4, cy + 3);
            g.Text(cx + 22, cy + 7, "IRON SWORD", Rarities[2], Shadow);
            int ax = cx + 4;
            foreach (var (label, bw, press) in new[] { ("EQUIP", 30, Press.Primary), ("DROP", 30, Press.Danger) })
            {
                Button(g, ax, cy + 20, bw, bh, label, press);
                ax += bw + 2;
            }
            QuickRow(g, cx + 4, cy + 22 + bh, cw - 8, bh, 2);
            int quickRows = cw < 150 ? 2 : 1;
            int ty = cy + 25 + (1 + quickRows) * bh + (quickRows - 1) * 2;
            foreach (var (line, color) in new[] { ("DAMAGE 14-19", Ink), ("THAN THE COPPER SWORD:", Rgba.Hex("#D1DBF2")), ("  DAMAGE +6", Good), ("  ATTACK SPEED -4%", Bad) })
            {
                if (ty + 6 > cy + cardH - 2) break;
                g.Small(cx + 4, ty, line, color);
                ty += 7;
            }
        }

        // The quick slots a picked thing may go in, the one it is in gold.
        static void QuickRow(PixelImage g, int x, int y, int w, int h, int at)
        {
            g.Small(x, y + (h - 5) / 2, "QUICK", Dim);
            int rows = w < 142 ? 2 : 1, across = 6 / rows;
            int size = Math.Min(rows > 1 ? 32 : h, (w - 28 - 2 * (across - 1)) / across);
            for (int i = 0; i < 6; i++) Button(g, x + 26 + i % across * (size + 2), y + i / across * (h + 2), size, h, (i + 1).ToString(), i == at ? Press.Primary : Press.Normal);
        }
    }
}
