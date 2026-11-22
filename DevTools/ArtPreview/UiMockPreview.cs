using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // Mockups of the UI overhaul's candidate looks, for a person to choose from (out/uimock/, --only uimock): the bag,
    // talking to someone, a shop and the passive tree, each in look A (the cave stone panel extended), B (a material
    // for each kind of window), C (minimal translucent) and the recommended mix, on a PC screen (1280 by 720: the
    // world at 3 pixels a pixel, the UI at 2, as the game draws them), and the mix's bag and talk on a phone held
    // upright (390 by 844 points, written at twice that). Pictures only: nothing here is in the game, and the writing
    // is a small Latin font because the tool cannot draw the game's.
    static class UiMockPreview
    {
        const int W = 640, H = 360;
        const int PhoneW = 195, PhoneH = 422;
        const MockKind Bag = MockKind.Bag, Shop = MockKind.Shop, Talk = MockKind.Talk, Growth = MockKind.Growth;

        static LootData _loot;
        static ClassData _classes;
        static Dictionary<string, string> _passiveStats;
        static readonly Rgba Dora = Rgba.Hex("#9A5AC8");

        public static void Write(string output, LootData loot, ClassData classes, string passivesCsv, PixelImage world)
        {
            _loot = loot;
            _classes = classes;
            _passiveStats = passivesCsv.Split('\n').Skip(1).Select(line => line.Trim().Split(',')).Where(c => c.Length >= 6)
                .ToDictionary(c => c[0], c => c[5]);
            string folder = Directory.CreateDirectory(Path.Combine(output, "uimock")).FullName;
            int fx = world.Width / 2 + 8, fy = world.Height / 2 + 14;
            var bags = new List<PixelImage>();
            foreach (var (style, name) in new[] { (MockStyle.A, "a"), (MockStyle.B, "b"), (MockStyle.C, "c"), (MockStyle.R, "mix") })
            {
                var skin = new MockSkin(style);
                var bag = Compose(world, fx, fy, 0.29f, 0.5f, 1280, 720, 3, 0.8f, false, BagScreen(skin), 2);
                Png.Save(bag, Path.Combine(folder, $"{name}_bag.png"));
                if (style != MockStyle.R) bags.Add(bag);
                Png.Save(Compose(world, fx, fy, 0.5f, 0.42f, 1280, 720, 3, 0.9f, false, TalkScreen(skin), 2), Path.Combine(folder, $"{name}_dialogue.png"));
                Png.Save(Compose(world, fx, fy, 0.5f, 0.5f, 1280, 720, 3, 0.42f, true, ShopScreen(skin), 2), Path.Combine(folder, $"{name}_shop.png"));
                Png.Save(Compose(world, fx, fy, 0.5f, 0.5f, 1280, 720, 3, style == MockStyle.C ? 0.5f : 0.3f, true, GrowthScreen(skin), 2), Path.Combine(folder, $"{name}_growth.png"));
            }
            Png.Save(Compose(world, fx, fy, 0.5f, 0.5f, 1280, 720, 3, 0.3f, true, Materials(), 2), Path.Combine(folder, "b_materials.png"));
            var mix = new MockSkin(MockStyle.R);
            Png.Save(Compose(world, fx, fy, 0.5f, 0.12f, 780, 1688, 6, 0.55f, false, PhoneBag(mix), 4), Path.Combine(folder, "mix_phone_bag.png"));
            Png.Save(Compose(world, fx, fy, 0.5f, 0.36f, 780, 1688, 6, 0.9f, false, PhoneTalk(mix), 4), Path.Combine(folder, "mix_phone_dialogue.png"));
            Png.Save(Compare(bags), Path.Combine(folder, "compare_abc.png"));
            Console.WriteLine($"Wrote the UI mockups to {folder}");
        }

        // The world behind (dimmed, and blurred behind a screen that takes the whole view) with the UI over it.
        static PixelImage Compose(PixelImage world, int focusX, int focusY, float atX, float atY, int width, int height, int worldScale, float dim, bool blur, PixelImage ui, int uiScale)
        {
            int w = (width + worldScale - 1) / worldScale, h = (height + worldScale - 1) / worldScale;
            int left = focusX - (int)(w * atX), top = focusY - (int)(h * atY);
            var view = new PixelImage(w, h);
            var night = Rgba.Hex("#0A0A0F");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = world[left + x, top + y];
                    view[x, y] = c.A == 0 ? night : c;
                }
            for (int pass = 0; blur && pass < 2; pass++)
            {
                var soft = new PixelImage(w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int j = -1; j <= 1; j++)
                            for (int i = -1; i <= 1; i++)
                            {
                                if (!view.Inside(x + i, y + j)) continue;
                                var c = view[x + i, y + j];
                                r += c.R;
                                g += c.G;
                                b += c.B;
                                n++;
                            }
                        soft[x, y] = new Rgba(r / n, g / n, b / n);
                    }
                view = soft;
            }
            var result = new PixelImage(width, height);
            var shade = Rgba.Hex("#08060C");
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = shade.Mix(view[x / worldScale, y / worldScale], dim);
                    result.Blend(x, y, ui[x / uiScale, y / uiScale]);
                }
            return result;
        }

        // The bag in A, B and C side by side at half size, with the letter over each.
        static PixelImage Compare(List<PixelImage> bags)
        {
            const int gap = 8, head = 22;
            var sheet = new PixelImage(bags.Count * W + (bags.Count + 1) * gap, H + head + gap);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#141018"));
            string[] names = { "A  CAVE STONE, EXTENDED", "B  A MATERIAL FOR EACH WINDOW", "C  MINIMAL TRANSLUCENT" };
            for (int n = 0; n < bags.Count; n++)
            {
                int left = gap + n * (W + gap);
                sheet.Text(left + 2, 8, names[n], Rgba.Hex("#F4DE9C"));
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int r = 0, g = 0, b = 0;
                        for (int j = 0; j < 2; j++)
                            for (int i = 0; i < 2; i++)
                            {
                                var c = bags[n][x * 2 + i, y * 2 + j];
                                r += c.R;
                                g += c.G;
                                b += c.B;
                            }
                        sheet[left + x, head + y] = new Rgba(r / 4, g / 4, b / 4);
                    }
            }
            return sheet;
        }

        // ---- Pictures from the game ----

        static PixelImage Icon(string id)
        {
            var gear = _loot.Bases.FirstOrDefault(b => b.Id == id);
            if (gear != null) return ItemArt.Gear(gear);
            var use = _loot.Consumables.FirstOrDefault(c => c.Id == id);
            if (use != null) return ItemArt.Consumable(use);
            var material = _loot.Materials.FirstOrDefault(m => m.Id == id);
            return material != null ? ItemArt.Material(material) : null;
        }

        static PixelImage Hero() => HeroArt.Figure(HeroArt.Draw(Look.Down, HeroMove.Stand, 0, HeroGear.None, Rgba.Hex("#3F7FD8")));

        static PixelImage Merchant() => PeopleArt.Person("merchant", Dora, 0);

        // The top of a figure, for a portrait.
        static PixelImage Bust(PixelImage figure, int rows)
        {
            int top = 0;
            while (top < figure.Height && !Enumerable.Range(0, figure.Width).Any(x => figure.IsSet(x, top))) top++;
            var result = new PixelImage(figure.Width, Math.Min(rows, figure.Height - top));
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++) result[x, y] = figure[x, top + y];
            return result;
        }

        // The bag's things: id, rarity (0 common to 4 legendary, -1 for what has none), count.
        static readonly (string Id, int Rarity, string Count)[] Things =
        {
            ("copper_pickaxe", 0, null), ("iron_bow", 1, null), ("frost_staff", 2, null), ("iron_boots", 1, null), ("flame_blade", 3, null),
            ("warden_maul", 4, null), ("mithril_helmet", 2, null), ("copper_amulet", 0, null), ("queen_crown", 4, null), ("iron_ring", 1, null),
            ("healing_potion", -1, "6"), ("mana_potion", -1, "3"), ("grilled_fish", -1, "2"), ("mushroom_soup", -1, "4"), ("recall_scroll", -1, "1"),
            ("torch", -1, "16"), ("stone", -1, "40"), ("copper_ore", -1, "12"), ("iron_ore", -1, "28"), ("iron_bar", -1, "5"),
            ("slime_gel", -1, "6"), ("mushroom", -1, "5"), ("ice_crystal", -1, "3"), ("essence", -1, "2"), ("cave_minnow", -1, "1"),
            ("glow_cap", -1, "7"), ("iron_gear", -1, "4"), ("copper_wire", -1, "9"),
        };

        static void Grid(PixelImage g, MockSkin s, MockKind kind, int x, int y, int columns, int rows, int hover = -1, int skip = 0)
        {
            for (int i = 0; i < columns * rows; i++)
            {
                int sx = x + i % columns * 24, sy = y + i / columns * 24;
                int at = i + skip;
                if (at < Things.Length) s.Slot(g, sx + 1, sy + 1, kind, Icon(Things[at].Id), Things[at].Rarity, Things[at].Count, hover: i == hover);
                else s.Slot(g, sx + 1, sy + 1, kind, null);
            }
        }

        static void Coins(PixelImage g, MockSkin s, MockKind kind, int right, int y, string amount, Rgba? color = null, bool plain = false)
        {
            int w = MockFont.Width(amount);
            g.Text(right - w, y, amount, color ?? Rgba.Hex("#FFE07A"), plain ? Rgba.Clear : s.Shadow(kind));
            MockGlyph.Draw(g, right - w - 10, y, MockGlyph.Coin, Rgba.Hex("#E8B83A"));
        }

        static void Hearts(PixelImage g, int x, int y, int full, int of = 5)
        {
            for (int i = 0; i < of; i++)
            {
                MockGlyph.Draw(g, x + i * 9 + 1, y + 1, MockGlyph.Heart, new Rgba(0, 0, 0, 140));
                MockGlyph.Draw(g, x + i * 9, y, MockGlyph.Heart, i < full ? Rgba.Hex("#F0504A") : Rgba.Hex("#4A3A3A"));
            }
        }

        // ---- The HUD behind every screen ----

        static void Hud(PixelImage g, MockSkin s, int width, int height, bool bars, bool map, bool phone = false, int middle = -1)
        {
            const MockKind k = MockKind.Hud;
            int hw = phone ? 112 : 150;
            s.Window(g, 5, 5, hw, 36, k, null, null, close: false);
            s.Bar(g, 11, 11, hw - 12, 9, 0.8f, Rgba.Hex("#D8483C"), k);
            s.Bar(g, 11, 21, hw - 12, 8, 1f, Rgba.Hex("#3C7CD8"), k);
            s.Bar(g, 11, 30, hw - 12, 5, 0.34f, Rgba.Hex("#E8B83A"), k);
            g.Small(11 + (hw - 12 - MockFont.SmallWidth("192/240")) / 2, 13, "192/240", Rgba.White, new Rgba(0, 0, 0, 200));
            g.Small(11 + (hw - 12 - MockFont.SmallWidth("100/100")) / 2, 22, "100/100", Rgba.White, new Rgba(0, 0, 0, 200));
            if (map)
            {
                int size = phone ? 46 : 62, mx = width - size - 5;
                s.Window(g, mx, 5, size, size, k, null, null, close: false);
                if (s.Style != MockStyle.C) g.Fill(mx + 4, 9, size - 8, size - 8, Rgba.Hex("#0E0C0C"));
                for (int i = 0; i < 9; i++)
                {
                    int bx = mx + size / 2 + (int)(PixelNoise.Hash(i, 0, 71) % (uint)(size / 2)) - size / 4;
                    int by = 5 + size / 2 + (int)(PixelNoise.Hash(i, 1, 71) % (uint)(size / 2)) - size / 4;
                    g.Disc(bx, by, 4 + i % 3 * 2, Rgba.Hex(i % 3 == 0 ? "#6A5A48" : "#57493C").WithAlpha(s.Style == MockStyle.C ? 170 : 255));
                }
                g.Fill(mx + size / 2 - 1, 5 + size / 2 - 1, 3, 3, Rgba.Hex("#7CD0FF"));
                g[mx + size / 2, 5 + size / 2] = Rgba.White;
            }
            if (!bars) return;
            // Quick slots over the skills, as the game stands them at the bottom of a PC screen.
            string[] quick = { "iron_sword", "copper_pickaxe", "healing_potion", "torch", null, null };
            string[] counts = { null, null, "6", "16", null, null };
            if (middle < 0) middle = width / 2;
            int qx = middle - quick.Length * 12;
            for (int i = 0; i < quick.Length; i++)
            {
                s.Slot(g, qx + i * 24 + 1, height - 64, MockKind.Hud, quick[i] != null ? Icon(quick[i]) : null, -1, counts[i], chosen: i == 0);
                g.Small(qx + i * 24 + 3, height - 62, (i + 1).ToString(), s.Dim(k), new Rgba(0, 0, 0, 200));
            }
            var skills = _classes.Skills.Where(skill => SkillArt.HasOwnPicture(skill)).Take(4).ToList();
            string[] keys = { "Q", "E", "R", "F" };
            int kx = middle - skills.Count * 18;
            for (int i = 0; i < skills.Count; i++)
            {
                var icon = SkillArt.Icon(skills[i]).Times(2);
                int ix = kx + i * 36 + 2, iy = height - 38;
                if (s.Style != MockStyle.C)
                {
                    g.Fill(ix - 1, iy - 1, icon.Width + 2, icon.Height + 2, MockSkin.Black);
                    g.Box(ix - 1, iy - 1, icon.Width + 2, icon.Height + 2, MockSkin.GoldDark);
                }
                g.Draw(icon, ix, iy);
                g.Small(ix + 2, iy + 2, keys[i], Rgba.White, new Rgba(0, 0, 0, 220));
            }
        }

        // ---- 1. The bag: what is worn round the hero, the things in a grid, and a card comparing one to what is worn ----

        static PixelImage BagScreen(MockSkin s)
        {
            var g = new PixelImage(W, H);
            Hud(g, s, W, H, bars: true, map: false, middle: 236);
            int x = 368, y = 20, w = 264, h = 326;
            int top = s.Window(g, x, y, w, h, Bag, "BAG", MockGlyph.Bag);
            int cx = x + 10, cw = w - 20;
            int dy = top + 3;
            Doll(g, s, cx, dy, cw, 98, wide: true);
            int fy = dy + 98 + 5;
            Filters(g, s, cx, fy, cw);
            int gy = fy + 19, gx = cx + 2;
            const int hover = 4;
            Grid(g, s, Bag, gx, gy, 10, 6, hover);
            int foot = gy + 6 * 24 + 4;
            g.Small(cx + 2, foot, "28/60", s.Dim(Bag), s.Shadow(Bag));
            string hint = "E EQUIP   Q QUICK SLOT   X DROP";
            g.Small(cx + cw - MockFont.SmallWidth(hint) - 2, foot, hint, s.Dim(Bag), s.Shadow(Bag));

            // The card of the thing under the pointer, and beside it the card of what is worn in its place.
            int ty = gy - 34;
            ItemCard(g, s, x - 244, ty, 116, 124, "IRON SWORD", 2, "SWORD  LV 9", Icon("iron_sword"), "EQUIPPED",
                new (string, string, string, int)[] { ("ATTACK", "24", null, 0), ("SPEED", "1.2", null, 0) }, new[] { "+6% CRIT CHANCE", "+12 MAX LIFE" }, "38");
            ItemCard(g, s, x - 124, ty, 116, 124, "FLAME BLADE", 3, "SWORD  LV 14", Icon("flame_blade"), null,
                new (string, string, string, int)[] { ("ATTACK", "31", "+7", 1), ("SPEED", "1.1", "-0.1", -1) }, new[] { "+18% FIRE DAMAGE", "+4 FIRE ON HIT", "BURNS FOR 3 SEC" }, "120");
            MockGlyph.Draw(g, gx + hover * 24 + 14, gy + 13, MockGlyph.Pointer, Rgba.Hex("#1A1410"));
            MockGlyph.Draw(g, gx + hover * 24 + 13, gy + 12, MockGlyph.Pointer, Rgba.White);
            return g;
        }

        // The hero with the worn things on each side and the numbers that matter.
        static void Doll(PixelImage g, MockSkin s, int x, int y, int w, int h, bool wide)
        {
            s.Well(g, x, y, w, h, Bag);
            int fx = x + w / 2;
            var hero = Hero().Times(3);
            // The ground under the feet.
            for (int i = -22; i <= 22; i++)
                for (int j = -4; j <= 4; j++)
                    if (i * i / 484f + j * j / 16f <= 1) g.Blend(fx + i, y + h - 16 + j, new Rgba(0, 0, 0, 90));
            g.Draw(hero, fx - hero.Width / 2, y + h - 16 - hero.Height + 3);
            int pitch = (h - 8 - 3 * 22) / 2 + 22;
            int lx = fx - (wide ? 34 : 28) - 22, rx = fx + (wide ? 34 : 28);
            s.Slot(g, lx, y + 4, Bag, Icon("copper_helmet"), 1);
            s.Slot(g, lx, y + 4 + pitch, Bag, Icon("iron_armor"), 2);
            s.Slot(g, lx, y + 4 + pitch * 2, Bag, null, hint: MockGlyph.Boot);
            s.Slot(g, rx, y + 4, Bag, Icon("iron_sword"), 2);
            s.Slot(g, rx, y + 4 + pitch, Bag, Icon("mithril_ring"), 3);
            s.Slot(g, rx, y + 4 + pitch * 2, Bag, null, hint: MockGlyph.Gem);
            var ink = s.Ink(Bag);
            var dim = s.Dim(Bag);
            var shadow = s.Shadow(Bag);
            if (wide)
            {
                g.Text(x + 8, y + 9, "LV 11", s.Title(Bag), shadow);
                g.Small(x + 8, y + 21, "WARRIOR", dim, shadow);
                g.Small(x + 8, y + 28, "FIRE MAGE", dim, shadow);
                s.Bar(g, x + 8, y + 38, 44, 5, 0.34f, Rgba.Hex("#E8B83A"), Bag);
                g.Small(x + 8, y + 46, "XP 34%", dim, shadow);
                g.Small(x + 8, y + h - 24, "SET", dim, shadow);
                g.Small(x + 8, y + h - 16, "IRON 2/4", MockSkin.Good, shadow);
            }
            var stats = new[] { (MockGlyph.Heart, "#F0605A", "240"), (MockGlyph.Sword, "#F2C078", "24"), (MockGlyph.Shield, "#A8B4C4", "18"), (MockGlyph.Crit, "#FFC43A", "8%"), (MockGlyph.Boot, "#7CE0C0", "105%") };
            int sx = wide ? x + w - 54 : x + w - 30, sy = y + (wide ? 10 : 6);
            for (int i = 0; i < stats.Length; i++)
            {
                int row = sy + i * (wide ? 16 : 14);
                MockGlyph.Draw(g, sx, row, stats[i].Item1, Rgba.Hex(stats[i].Item2));
                if (wide) g.Text(sx + 12, row, stats[i].Item3, ink, shadow);
                else g.Small(sx + 10, row + 1, stats[i].Item3, ink, shadow);
            }
            if (!wide)
            {
                g.Small(x + 5, y + 7, "LV 11", s.Title(Bag), shadow);
                g.Small(x + 5, y + 15, "WARRIOR", dim, shadow);
                s.Bar(g, x + 5, y + 24, 28, 5, 0.34f, Rgba.Hex("#E8B83A"), Bag);
                g.Small(x + 5, y + h - 20, "SET", dim, shadow);
                g.Small(x + 5, y + h - 12, "IRON 2/4", MockSkin.Good, shadow);
            }
        }

        static void Filters(PixelImage g, MockSkin s, int x, int y, int w, bool phone = false)
        {
            string[] names = { "ALL", "GEAR", "USE", "MATS" };
            int at = x;
            for (int i = 0; i < names.Length; i++)
            {
                int tw = MockFont.Width(names[i]) + 12;
                s.Button(g, at, y, tw, 15, Bag, names[i], press: i == 0 ? MockSkin.Press.Primary : MockSkin.Press.Normal);
                at += tw + 2;
            }
            if (!phone) s.Button(g, x + w - 40, y, 40, 15, Bag, "SORT");
            Coins(g, s, Bag, x + w - (phone ? 2 : 46), y + 4, "1,250");
        }

        static void ItemCard(PixelImage g, MockSkin s, int x, int y, int w, int h, string name, int rarity, string kind, PixelImage icon, string tag,
            (string Label, string Value, string Delta, int Sign)[] stats, string[] affixes, string price)
        {
            var tint = MockSkin.RarityColors[rarity];
            var (ink, dim, shadow) = s.Card(g, x, y, w, h, tint);
            var nameColor = s.Style == MockStyle.B ? tint.Scale(0.5f) : tint;
            int row = y + 9;
            if (tag != null)
            {
                int tw = MockFont.SmallWidth(tag) + 6;
                g.BlendFill(x + w - tw - 5, row - 1, tw, 9, s.Style == MockStyle.B ? new Rgba(90, 60, 30, 70) : new Rgba(255, 255, 255, 30));
                g.Small(x + w - tw - 2, row + 1, tag, dim);
            }
            s.Slot(g, x + 7, row, s.Style == MockStyle.B ? MockKind.Journal : Bag, icon, s.Style == MockStyle.B ? -1 : rarity);
            g.Text(x + 7, row + 27, name, nameColor, shadow);
            g.Small(x + 33, row + 14, kind, dim);
            row += 38;
            g.BlendFill(x + 6, row, w - 12, 1, s.Style == MockStyle.B ? new Rgba(90, 60, 30, 120) : new Rgba(255, 255, 255, 40));
            row += 5;
            foreach (var (label, value, delta, sign) in stats)
            {
                g.Text(x + 7, row, label, dim, shadow);
                g.Text(x + 52, row, value, ink, shadow);
                if (delta != null)
                {
                    var c = sign > 0 ? MockSkin.Good : MockSkin.Bad;
                    if (s.Style == MockStyle.B) c = c.Scale(0.6f);
                    g.Text(x + w - 7 - MockFont.Width(delta), row, delta, c, shadow);
                }
                row += 10;
            }
            row += 3;
            foreach (string affix in affixes)
            {
                g.Fill(x + 7, row + 1, 2, 2, nameColor);
                g.Small(x + 12, row, affix, s.Style == MockStyle.B ? Rgba.Hex("#2A4A8A") : Rgba.Hex("#9AC4FF"));
                row += 8;
            }
            Coins(g, s, Bag, x + w - 7, y + h - 13, price, s.Style == MockStyle.B ? Rgba.Hex("#7A5A10") : (Rgba?)null, plain: s.Style == MockStyle.B);
            g.Small(x + 7, y + h - 12, "SELLS FOR", dim);
        }

        // ---- 2. Talking: a box at the bottom with the speaker's portrait, name plate, words and what to do next ----

        static readonly (string Label, string[] Icon)[] Choices =
        {
            ("SHOP", MockGlyph.Shop), ("REQUEST", MockGlyph.Request), ("GIFT", MockGlyph.Gift), ("TALK", MockGlyph.Talk), ("CLOSE", MockGlyph.SmallX),
        };

        static void Portrait(PixelImage g, MockSkin s, int x, int y, int w, int h, int scale, Rgba color)
        {
            s.Well(g, x, y, w, h, Talk);
            // The speaker's colour behind them, darker toward the floor.
            for (int j = 1; j < h - 1; j++)
            {
                var c = color.Scale(0.5f).Mix(Rgba.Hex("#120E12"), 0.25f + 0.6f * j / h);
                g.BlendFill(x + 1, y + j, w - 2, 1, c.WithAlpha(s.Style == MockStyle.C ? 150 : 255));
            }
            var bust = Bust(Merchant(), (h - 2) / scale).Times(scale);
            g.Draw(bust, x + (w - bust.Width) / 2, y + h - 1 - bust.Height);
            if (s.Style != MockStyle.C) g.Box(x, y, w, h, MockSkin.Black);
            if (s.Style != MockStyle.C) g.Box(x + 1, y + 1, w - 2, h - 2, Ramp.Of(color).Main);
        }

        static PixelImage TalkScreen(MockSkin s)
        {
            var g = new PixelImage(W, H);
            Hud(g, s, W, H, bars: false, map: true);
            bool strip = s.Style == MockStyle.R;
            int bw = 452, bh = 98, bx = (W - bw) / 2, by = H - 8 - bh - (strip ? 16 : 0);
            int top = s.Window(g, bx, by, bw, bh + (strip ? 16 : 0), Talk, strip ? "DORA THE MERCHANT" : null, null, Dora, close: false);
            if (strip) by += 16;
            int px = bx + 8, pw = 66, ph = bh - 14;
            Portrait(g, s, px, by + 7, pw, ph, 4, Dora);
            int tx = px + pw + 10;
            var plate = Ramp.Of(Dora);
            string name = "DORA THE MERCHANT";
            int nw = MockFont.Width(name) + 16;
            if (strip)
            {
                Hearts(g, bx + 8 + MockFont.Width(name) + 16, by - 16 + 9, 3);
            }
            else if (s.Style == MockStyle.C)
            {
                g.BlendFill(tx - 2, by - 15, nw, 15, Dora);
                g.Text(tx + 6, by - 11, name, Rgba.White);
                Hearts(g, tx + nw + 6, by - 11, 3);
            }
            else
            {
                // The name plate stands on the top edge in the speaker's colour.
                int ny = by - 10;
                if (s.Style == MockStyle.A) g.Raised(tx - 2, ny, nw, 17, MockSkin.Black, MockSkin.GoldLight, plate);
                else
                {
                    g.Raised(tx - 2, ny, nw, 17, Rgba.Hex("#120A06"), plate.Light, plate);
                    g.Fill(tx + 1, ny + 3, 2, 2, plate.Light);
                    g.Fill(tx + nw - 7, ny + 3, 2, 2, plate.Light);
                }
                g.Text(tx + 6, ny + 5, name, Rgba.White, new Rgba(0, 0, 0, 170));
                int hx = tx + nw + 2;
                g.Raised(hx, ny + 2, 53, 13, MockSkin.Black, s.Style == MockStyle.A ? MockSkin.Gold : plate.Shade, Ramp.Of("#2A211B"));
                Hearts(g, hx + 5, ny + 6, 3);
            }
            var ink = s.Ink(Talk);
            var shadow = s.Shadow(Talk);
            int ty = by + (strip ? 10 : 14);
            g.Text(tx, ty, "WELCOME BACK, TRAVELLER. THE ROAD TO THE ROYAL CITY", ink, shadow);
            g.Text(tx, ty + 12, "IS OPEN AGAIN, AND I HAVE NEW SEEDS IN FROM THE", ink, shadow);
            g.Text(tx, ty + 24, "FROST CAVES. CARE TO TAKE A LOOK?", ink, shadow);
            MockGlyph.Draw(g, bx + bw - 20, ty + 27, MockGlyph.Next, s.Style == MockStyle.C ? Rgba.White : MockSkin.GoldLight);
            int cy = by + bh - 8 - 20;
            int cw = (bx + bw - 8 - tx - 4 * 4) / 5;
            for (int i = 0; i < Choices.Length; i++)
                s.Button(g, tx + i * (cw + 4), cy, cw, 20, Talk, Choices[i].Label, Choices[i].Icon, i == 0 ? MockSkin.Press.Primary : MockSkin.Press.Normal, Dora, (i + 1).ToString());
            return g;
        }

        // ---- 3. A shop: the keeper at the head, their goods with prices, the player's bag beside ----

        static readonly (string Id, string Name, string Note, string Price)[] Goods =
        {
            ("mushroom_spore", "MUSHROOM SPORE", "SEED", "5"), ("fire_pepper_seed", "FIRE PEPPER SEED", "SEED", "15"),
            ("frost_berry_seed", "FROST BERRY SEED", "SEED  NEW", "20"), ("fertilizer", "FERTILIZER", "FARM", "8"),
            ("healing_potion", "HEALING POTION", "USE  3 LEFT TODAY", "45"), ("mana_potion", "MANA POTION", "USE", "40"),
            ("recall_scroll", "RECALL SCROLL", "USE", "60"), ("map_scrap", "MAP SCRAP", "NEEDS 2 HEARTS", "1,500"),
        };

        static PixelImage ShopScreen(MockSkin s)
        {
            var g = new PixelImage(W, H);
            int x = 62, y = 24, w = 268, h = 314;
            int top = s.Window(g, x, y, w, h, Shop, "DORA'S STALL", MockGlyph.Shop, close: false);
            var ink = s.Ink(Shop);
            var dim = s.Dim(Shop);
            var shadow = s.Shadow(Shop);
            // The keeper.
            Portrait(g, s, x + 10, top + 3, 34, 40, 2, Dora);
            g.Text(x + 50, top + 6, "DORA", s.Title(Shop), shadow);
            g.Small(x + 50, top + 17, "TRAVELLING MERCHANT", dim, shadow);
            Hearts(g, x + 50, top + 27, 3);
            g.Small(x + 98, top + 28, "5% OFF", MockSkin.Good, shadow);
            string[] tabs = { "BUY", "SELL", "ASK" };
            for (int i = 0; i < tabs.Length; i++)
                s.Tab(g, x + w - 10 - (3 - i) * 38, top + 25, 36, 18, Shop, tabs[i], null, i == 0, dot: i == 2);
            int ly = top + 47;
            const int chosen = 4;
            if (s.Style == MockStyle.B) s.Well(g, x + 7, ly - 2, w - 14, Goods.Length * 25 + 3, Shop);
            for (int i = 0; i < Goods.Length; i++)
            {
                int ry = ly + i * 25;
                bool dear = i == Goods.Length - 1;
                if (i == chosen)
                {
                    if (s.Style == MockStyle.C) g.BlendFill(x + 8, ry, w - 16, 24, new Rgba(255, 255, 255, 40));
                    else
                    {
                        g.BlendFill(x + 8, ry, w - 16, 24, new Rgba(255, 222, 150, 46));
                        g.Box(x + 8, ry, w - 16, 24, MockSkin.GoldLight);
                    }
                }
                else if (i > 0 && i != chosen + 1) g.BlendFill(x + 34, ry - 1, w - 46, 1, s.Style == MockStyle.C ? new Rgba(255, 255, 255, 24) : new Rgba(0, 0, 0, 90));
                s.Slot(g, x + 10, ry + 1, Shop, Icon(Goods[i].Id));
                g.Text(x + 38, ry + 4, Goods[i].Name, dear ? ink.WithAlpha(130) : ink, shadow);
                g.Small(x + 38, ry + 15, Goods[i].Note, dear ? MockSkin.Bad : dim, shadow);
                Coins(g, s, Shop, x + w - 14, ry + 8, Goods[i].Price, dear ? MockSkin.Bad : (Rgba?)null);
            }
            int fy = ly + Goods.Length * 25 + 4;
            s.Rule(g, x + 10, fy - 2, w - 20, Shop);
            s.Button(g, x + 10, fy + 2, 18, 18, Shop, "-");
            s.Well(g, x + 30, fy + 2, 30, 18, Shop);
            g.Text(x + 30 + (30 - MockFont.Width("3")) / 2, fy + 8, "3", ink, shadow);
            s.Button(g, x + 62, fy + 2, 18, 18, Shop, "+");
            g.Small(x + 86, fy + 9, "TOTAL", dim, shadow);
            Coins(g, s, Shop, x + 150, fy + 8, "135");
            s.Button(g, x + w - 10 - 92, fy + 1, 92, 20, Shop, "BUY", MockGlyph.Coin, MockSkin.Press.Primary);

            int bx = x + w + 10, bw = 242;
            int btop = s.Window(g, bx, y, bw, h, Bag, "YOUR BAG", MockGlyph.Bag);
            btop = Math.Max(btop, top);
            Filters(g, s, bx + 10, btop + 4, bw - 20, phone: true);
            int rows = (y + h - 26 - (btop + 24)) / 24;
            Grid(g, s, Bag, bx + (bw - 9 * 24) / 2, btop + 24, 9, rows);
            g.Small(bx + 12, y + h - 16, "TAP A THING TO SELL IT   HOLD FOR ITS CARD", s.Dim(Bag), s.Shadow(Bag));
            return g;
        }

        // ---- 4. Growth: skills, passives and the character under tabs, the passive tree over the whole screen ----

        static readonly HashSet<string> Learned = new HashSet<string> { "start", "str1", "str2", "str5", "str6", "str7", "int1", "int2", "int5", "dex1", "sum1" };
        const string Hovered = "int6";

        static (string[] Glyph, Rgba Color) NodeLook(PassiveNode node)
        {
            if (node.IsStart) return (MockGlyph.Sun, Rgba.Hex("#FFE07A"));
            _passiveStats.TryGetValue(node.Id, out string stats);
            stats ??= "";
            bool rank = stats.Contains("SkillRank");
            if (stats.StartsWith("Fire")) return (rank ? MockGlyph.Rank : MockGlyph.Flame, Rgba.Hex("#FF8026"));
            if (stats.StartsWith("Cold")) return (rank ? MockGlyph.Rank : MockGlyph.Snow, Rgba.Hex("#8CD9FF"));
            if (stats.StartsWith("Lightning")) return (rank ? MockGlyph.Rank : MockGlyph.Bolt, Rgba.Hex("#FFF266"));
            if (stats.StartsWith("Summon")) return (rank ? MockGlyph.Rank : MockGlyph.Spirit, Rgba.Hex("#73E6CC"));
            if (stats.StartsWith("Melee")) return (rank ? MockGlyph.Rank : MockGlyph.Sword, Rgba.Hex("#F2A070"));
            if (stats.StartsWith("Ranged")) return (rank ? MockGlyph.Rank : MockGlyph.Arrow, Rgba.Hex("#9AE06A"));
            if (stats.StartsWith("Magic")) return (MockGlyph.Star, Rgba.Hex("#BF8CFF"));
            if (stats.StartsWith("MaxHealth") || stats.StartsWith("HealthRegen")) return (MockGlyph.Heart, Rgba.Hex("#F0605A"));
            if (stats.StartsWith("Armor")) return (MockGlyph.Shield, Rgba.Hex("#B4C0D0"));
            if (stats.StartsWith("MoveSpeed")) return (MockGlyph.Boot, Rgba.Hex("#7CE0C0"));
            if (stats.StartsWith("Crit")) return (MockGlyph.Crit, Rgba.Hex("#FFC43A"));
            if (stats.StartsWith("Mining")) return (MockGlyph.Pick, Rgba.Hex("#D0A870"));
            if (stats.StartsWith("ItemRarity")) return (MockGlyph.Gem, Rgba.Hex("#F2CC5C"));
            if (stats.StartsWith("FireDamage")) return (MockGlyph.Flame, Rgba.Hex("#FF8026"));
            if (stats.StartsWith("ColdDamage")) return (MockGlyph.Snow, Rgba.Hex("#8CD9FF"));
            if (stats.StartsWith("LightningDamage")) return (MockGlyph.Bolt, Rgba.Hex("#FFF266"));
            if (stats.StartsWith("AttackSpeed")) return (MockGlyph.Rank, Rgba.Hex("#F2C078"));
            return (MockGlyph.Glass, Rgba.Hex("#C8A8FF"));
        }

        // state: 0 out of reach, 1 can be learned now, 2 learned.
        static void Node(PixelImage g, MockSkin s, int cx, int cy, PassiveNode node, int state, bool hover)
        {
            var (glyph, color) = NodeLook(node);
            int r = node.IsStart ? 10 : node.Notable ? 9 : 6;
            bool night = s.Style == MockStyle.B || s.Style == MockStyle.R;
            if (s.Style == MockStyle.C)
            {
                var accent = s.Accent(Growth);
                if (state == 2) g.Glow(cx, cy, r + 7, accent, 90);
                if (node.Notable || node.IsStart)
                {
                    g.Diamond(cx, cy, r + 2, state == 2 ? accent : new Rgba(255, 255, 255, state == 1 ? 60 : 34));
                    if (state == 1)
                        for (int i = 0; i <= r + 2; i++)
                        {
                            g.Blend(cx - r - 2 + i, cy - i, accent);
                            g.Blend(cx + r + 2 - i, cy - i, accent);
                            g.Blend(cx - r - 2 + i, cy + i, accent);
                            g.Blend(cx + r + 2 - i, cy + i, accent);
                        }
                }
                else
                {
                    g.Disc(cx, cy, r + 0.4f, state == 2 ? accent : new Rgba(255, 255, 255, state == 1 ? 60 : 34));
                    if (state == 1) g.Ring(cx, cy, r + 0.4f, accent);
                }
                MockGlyph.Centered(g, cx, cy, glyph, state == 2 ? Rgba.Hex("#14101E") : state == 1 ? color : new Rgba(255, 255, 255, 120));
                if (hover) g.Ring(cx, cy, r + 5.4f, Rgba.White);
                return;
            }
            if (night)
            {
                if (state == 2) g.Glow(cx, cy, r + 10, color, 130);
                else if (state == 1) g.Glow(cx, cy, r + 6, Rgba.Hex("#9AB4FF"), 70);
                if (node.Notable || node.IsStart)
                {
                    // A rune circle round the bigger points: a thin ring with ticks at the eight winds.
                    var ring = state == 2 ? Rgba.Hex("#FFE9A8") : state == 1 ? Rgba.Hex("#CFE0FF") : Rgba.Hex("#5A5EA8");
                    g.Ring(cx, cy, r + 4.4f, ring);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.Blend(cx + (int)Math.Round(Math.Cos(a) * (r + 6)), cy + (int)Math.Round(Math.Sin(a) * (r + 6)), ring);
                    }
                }
                g.Disc(cx, cy, r + 1.4f, Rgba.Hex("#05061A"));
                g.Disc(cx, cy, r + 0.4f, state == 2 ? color.Scale(0.42f) : Rgba.Hex("#0E1030"));
                g.Ring(cx, cy, r + 0.4f, state == 2 ? Rgba.White : state == 1 ? Rgba.Hex("#CFE0FF") : Rgba.Hex("#5A5EA8"));
                if (state == 2) g.Ring(cx, cy, r - 0.6f, color);
                MockGlyph.Centered(g, cx, cy, glyph, state == 2 ? color.Mix(Rgba.White, 0.55f) : state == 1 ? color : Rgba.Hex("#7A80C0"));
                if (hover) g.Ring(cx, cy, r + 3.4f, Rgba.White);
                return;
            }
            // Stone: a socket cut in the slab with a gold rim; lit and filled when learned.
            if (state == 2) g.Glow(cx, cy, r + 9, Rgba.Hex("#FFC860"), 120);
            g.Disc(cx, cy, r + 2.4f, MockSkin.Black);
            if (node.Notable || node.IsStart)
            {
                g.Disc(cx, cy, r + 1.4f, state == 2 ? MockSkin.GoldLight : state == 1 ? MockSkin.Gold : MockSkin.GoldDark);
                g.Disc(cx, cy, r + 0.4f, MockSkin.Black);
                foreach (var (dx, dy) in new[] { (0, -r - 2), (0, r + 2), (-r - 2, 0), (r + 2, 0) })
                    g.Fill(cx + dx - 1, cy + dy - 1, 3, 3, state == 2 ? MockSkin.GoldLight : MockSkin.GoldDark);
            }
            g.Disc(cx, cy, r + (node.Notable || node.IsStart ? -0.6f : 1.4f), state == 2 ? MockSkin.GoldLight : state == 1 ? MockSkin.Gold : MockSkin.GoldDark);
            g.Disc(cx, cy, r + (node.Notable || node.IsStart ? -1.6f : 0.4f), state == 2 ? color.Scale(0.4f).Mix(Rgba.Hex("#2A211B"), 0.3f) : Rgba.Hex("#1D1713"));
            MockGlyph.Centered(g, cx, cy, glyph, state == 2 ? color.Mix(Rgba.White, 0.35f) : state == 1 ? color.Scale(0.85f) : Rgba.Hex("#7A6C5A"));
            if (state == 1) g.Ring(cx, cy, r + 4.4f, MockSkin.Gold.WithAlpha(150));
            if (hover) g.Ring(cx, cy, r + 4.4f, Rgba.White);
        }

        static void Link(PixelImage g, MockSkin s, int x0, int y0, int x1, int y1, int state)
        {
            if (s.Style == MockStyle.C)
            {
                var accent = s.Accent(Growth);
                if (state == 2) g.Line(x0, y0, x1, y1, accent, 2);
                else g.Line(x0, y0, x1, y1, new Rgba(255, 255, 255, state == 1 ? 110 : 44));
                return;
            }
            if (s.Style == MockStyle.B || s.Style == MockStyle.R)
            {
                if (state == 2)
                {
                    g.Line(x0, y0, x1, y1, Rgba.Hex("#6AA8FF").WithAlpha(70), 5);
                    g.Line(x0, y0, x1, y1, Rgba.Hex("#9AC8FF").WithAlpha(150), 3);
                    g.Line(x0, y0, x1, y1, Rgba.Hex("#F0F6FF"));
                }
                else g.Line(x0, y0, x1, y1, state == 1 ? Rgba.Hex("#9AB4FF") : Rgba.Hex("#4A4E9A"), 1, state == 1 ? 0 : 2);
                return;
            }
            g.Line(x0, y0, x1, y1, MockSkin.Black, 3);
            if (state == 2)
            {
                g.Line(x0, y0, x1, y1, Rgba.Hex("#FFC860").WithAlpha(70), 5);
                g.Line(x0, y0, x1, y1, Rgba.Hex("#A87A30"), 3);
                g.Line(x0, y0, x1, y1, MockSkin.GoldLight);
            }
            else g.Line(x0, y0, x1, y1, state == 1 ? MockSkin.Gold : Rgba.Hex("#4A3C30"), 1, state == 1 ? 2 : 0);
        }

        static PixelImage GrowthScreen(MockSkin s)
        {
            var g = new PixelImage(W, H);
            bool night = s.Style == MockStyle.B || s.Style == MockStyle.R;
            int x = 6, y = 24, w = W - 12, h = H - 30;
            // The tabs stand on the top edge of the screen's panel.
            (string, string[])[] tabs = { ("SKILLS", MockGlyph.Skills), ("PASSIVES", MockGlyph.Tree), ("CHARACTER", MockGlyph.Person) };
            int tw = 100, tx = (W - tabs.Length * (tw + 4)) / 2;
            if (s.Style == MockStyle.C) g.BlendFill(0, 0, W, H, new Rgba(8, 8, 12, 150));
            else if (s.Style == MockStyle.A)
            {
                s.Window(g, x, y, w, h, MockKind.Hud, null, null, close: false);
            }
            else
            {
                new MockSkin(MockStyle.B).Window(g, x, y, w, h, Growth, null, null, close: false);
                if (s.Style == MockStyle.R)
                {
                    // The mix keeps the gold rim round the night.
                    g.Box(x, y, w, h, MockSkin.Black);
                    g.Box(x + 1, y + 1, w - 2, h - 2, MockSkin.Gold);
                    g.Fill(x + 1, y + 1, w - 2, 1, MockSkin.GoldLight);
                    g.Box(x + 2, y + 2, w - 4, h - 4, MockSkin.Black);
                }
            }
            for (int i = 0; i < tabs.Length; i++) s.Tab(g, tx + i * (tw + 4), y - 20, tw, 22, Growth, tabs[i].Item1, tabs[i].Item2, i == 1, dot: i == 0);
            g.Small(tx - 14, y - 12, "Q", s.Dim(Growth), s.Shadow(Growth));
            g.Small(tx + tabs.Length * (tw + 4) + 8, y - 12, "E", s.Dim(Growth), s.Shadow(Growth));
            if (s.Style == MockStyle.A) g.Raised(W - 22, y - 18, 13, 13, MockSkin.Black, MockSkin.GoldLight, Ramp.Of("#A8382E"));
            else if (s.Style != MockStyle.C) g.Raised(W - 22, y - 18, 13, 13, Rgba.Hex("#120A06"), night && s.Style == MockStyle.B ? Rgba.Hex("#8A8CD0") : MockSkin.GoldLight, Ramp.Of(s.Style == MockStyle.B ? "#2A2A66" : "#A8382E"));
            MockGlyph.Draw(g, W - 18, y - 14, MockGlyph.SmallX, Rgba.White);

            var ink = s.Ink(Growth);
            var dim = s.Dim(Growth);
            var shadow = s.Shadow(Growth);
            int side = 150, ax = x + 8, ay = y + 8, aw = w - 16 - side - 6, ah = h - 16;
            if (s.Style == MockStyle.A) s.Well(g, ax, ay, aw, ah, Growth);
            int cx = ax + aw / 2, cy = ay + 16 + 5 * 28;
            const int stepX = 32, stepY = 28;
            if (night)
            {
                // Rune circles round the start, as a star chart is ruled.
                var rule = Rgba.Hex("#7A7CD0");
                foreach (var (radius, alpha) in new[] { (58f, 70), (116f, 56), (172f, 44) })
                {
                    for (int a = 0; a < 720; a++)
                    {
                        double t = a * Math.PI / 360;
                        int px = cx + (int)Math.Round(Math.Cos(t) * radius * 1.14f), py = cy + (int)Math.Round(Math.Sin(t) * radius);
                        if (px > ax && px < ax + aw && py > ay && py < ay + ah && (radius < 100 || a / 6 % 2 == 0)) g.Blend(px, py, rule.WithAlpha(alpha));
                    }
                    for (int i = 0; i < 24; i++)
                    {
                        double t = i * Math.PI / 12;
                        float r2 = radius + (i % 2 == 0 ? 4 : 2);
                        int px = cx + (int)Math.Round(Math.Cos(t) * r2 * 1.14f), py = cy + (int)Math.Round(Math.Sin(t) * r2);
                        if (px > ax && px < ax + aw && py > ay && py < ay + ah) g.Blend(px, py, rule.WithAlpha(alpha + 60));
                    }
                }
            }
            (int X, int Y) At(PassiveNode node) => (cx + node.X * stepX, cy + node.Y * stepY);
            int State(PassiveNode node) => Learned.Contains(node.Id) ? 2 : node.Links.Any(link => Learned.Contains(link.Id)) ? 1 : 0;
            foreach (var node in _classes.Passives)
                foreach (var other in node.Links)
                {
                    if (string.CompareOrdinal(node.Id, other.Id) >= 0) continue;
                    var (x0, y0) = At(node);
                    var (x1, y1) = At(other);
                    int a = State(node), b = State(other);
                    Link(g, s, x0, y0, x1, y1, a == 2 && b == 2 ? 2 : a == 2 || b == 2 ? 1 : 0);
                }
            PassiveNode hovered = null;
            foreach (var node in _classes.Passives)
            {
                var (nx, ny) = At(node);
                if (node.Id == Hovered) hovered = node;
                Node(g, s, nx, ny, node, State(node), node.Id == Hovered);
            }
            // What the tree is and how many points are left, in its corner.
            g.Text(ax + 8, ay + 8, "PASSIVE TREE", s.Title(Growth), shadow);
            g.Small(ax + 8, ay + 20, "LEARN A POINT NEXT TO ONE YOU HAVE", dim, shadow);
            int bx = ax + 8, bottom = ay + ah - 22;
            s.Well(g, bx, bottom, 116, 16, Growth);
            MockGlyph.Draw(g, bx + 4, bottom + 3, MockGlyph.Search, dim);
            g.Small(bx + 18, bottom + 6, "SEARCH  (FIRE, LIFE...)", dim);
            g.Small(bx + 124, bottom + 6, "WHEEL ZOOM   DRAG MOVE", dim, shadow);

            // The side: points left, what the learned points add up to, the key to the marks.
            int sx = x + w - 8 - side, sy = y + 8;
            if (s.Style == MockStyle.A) s.Well(g, sx, sy, side, ah, Growth);
            else if (s.Style == MockStyle.C) g.BlendFill(sx, sy, side, ah, new Rgba(255, 255, 255, 14));
            else
            {
                g.BlendFill(sx, sy, side, ah, new Rgba(4, 4, 20, 170));
                g.Box(sx, sy, side, ah, s.Style == MockStyle.R ? MockSkin.GoldDark : Rgba.Hex("#4A4C96"));
            }
            g.Text(sx + 10, sy + 10, "POINTS", dim, shadow);
            g.Text(sx + side - 10 - MockFont.Width("3", 2), sy + 6, "3", Rgba.Hex("#FFE07A"), shadow, 2);
            s.Rule(g, sx + 8, sy + 26, side - 16, Growth);
            g.Small(sx + 10, sy + 33, "LEARNED 10 OF 59", dim, shadow);
            (string[], string, string)[] totals =
            {
                (MockGlyph.Heart, "#F0605A", "+5% MAX LIFE"), (MockGlyph.Sword, "#F2A070", "+16% MELEE DMG"), (MockGlyph.Rank, "#F2C078", "+11% ATK SPEED"),
                (MockGlyph.Star, "#BF8CFF", "+12% MAGIC DMG"), (MockGlyph.Drop, "#6AA8FF", "+25 MAX MANA"), (MockGlyph.Snow, "#8CD9FF", "+10% COLD DMG"),
                (MockGlyph.Spirit, "#73E6CC", "+8% SUMMON DMG"),
            };
            for (int i = 0; i < totals.Length; i++)
            {
                MockGlyph.Draw(g, sx + 10, sy + 44 + i * 13, totals[i].Item1, Rgba.Hex(totals[i].Item2));
                g.Text(sx + 22, sy + 44 + i * 13, totals[i].Item3, ink, shadow);
            }
            int ky = sy + 44 + totals.Length * 13 + 6;
            s.Rule(g, sx + 8, ky, side - 16, Growth);
            var sample = _classes.FindPassive("str3");
            var notable = _classes.FindPassive("str4");
            string[] keys = { "OUT OF REACH", "CAN LEARN", "LEARNED" };
            for (int i = 0; i < 3; i++)
            {
                Node(g, s, sx + 18, ky + 17 + i * 21, sample, i, false);
                g.Small(sx + 34, ky + 15 + i * 21, keys[i], i == 2 ? ink : dim, shadow);
            }
            Node(g, s, sx + 104, ky + 28, notable, 1, false);
            g.Small(sx + 90, ky + 46, "BIG POINT", dim, shadow);
            s.Button(g, sx + 8, sy + ah - 50, side - 16, 18, Growth, "RESET  120", MockGlyph.Coin);
            s.Button(g, sx + 8, sy + ah - 28, side - 16, 20, Growth, "LEARN  (CLICK)", null, MockSkin.Press.Primary);

            // The card of the point under the pointer.
            if (hovered != null)
            {
                var (hx, hy) = At(hovered);
                var tint = Rgba.Hex("#8CD9FF");
                int cw = 122, ch = 74, cxx = hx + 16, cyy = hy - 20;
                var (cardInk, cardDim, cardShadow) = s.Card(g, cxx, cyy, cw, ch, tint);
                g.Text(cxx + 8, cyy + 10, "DEEP WINTER", s.Style == MockStyle.B ? tint.Scale(0.45f) : tint, cardShadow);
                g.Small(cxx + 8, cyy + 21, "BIG POINT   COLD", cardDim);
                g.BlendFill(cxx + 6, cyy + 29, cw - 12, 1, s.Style == MockStyle.B ? new Rgba(90, 60, 30, 120) : new Rgba(255, 255, 255, 40));
                var good = s.Style == MockStyle.B ? MockSkin.Good.Scale(0.5f) : MockSkin.Good;
                g.Text(cxx + 8, cyy + 34, "+20% COLD DAMAGE", good, cardShadow);
                g.Text(cxx + 8, cyy + 44, "+5% ARMOR", good, cardShadow);
                g.Small(cxx + 8, cyy + 58, "COSTS 1 POINT", cardInk);
                g.Small(cxx + 8, cyy + 65, "CLICK TO LEARN", cardDim);
                MockGlyph.Draw(g, hx + 4, hy + 4, MockGlyph.Pointer, Rgba.Hex("#1A1410"));
                MockGlyph.Draw(g, hx + 3, hy + 3, MockGlyph.Pointer, Rgba.White);
            }
            return g;
        }

        // ---- B's six materials side by side ----

        static PixelImage Materials()
        {
            var g = new PixelImage(W, H);
            var s = new MockSkin(MockStyle.B);
            (MockKind, string, string[])[] kinds =
            {
                (Bag, "BAG", MockGlyph.Bag), (MockKind.Journal, "JOURNAL", MockGlyph.Book), (Shop, "SHOP", MockGlyph.Shop),
                (Growth, "PASSIVES", MockGlyph.Tree), (Talk, null, null), (MockKind.Machine, "SMELTER", MockGlyph.Gear),
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (kind, title, icon) = kinds[i];
                int x = 14 + i % 3 * 208, y = 16 + i / 3 * 172, w = 196, h = 156;
                int top = s.Window(g, x, y, w, h, kind, title, icon, kind == Talk ? Dora : (Rgba?)null);
                var ink = s.Ink(kind);
                var dim = s.Dim(kind);
                var shadow = s.Shadow(kind);
                if (kind == Talk)
                {
                    g.Raised(x + 10, y - 9, 70, 17, Rgba.Hex("#120A06"), Ramp.Of(Dora).Light, Ramp.Of(Dora));
                    g.Text(x + 18, y - 4, "DIALOGUE", Rgba.White, new Rgba(0, 0, 0, 170));
                    top += 8;
                }
                g.Text(x + 12, top + 4, "A HEADING", s.Title(kind), shadow);
                g.Bars(x + 12, top + 17, w - 24, 3, dim.WithAlpha(190), i);
                for (int c = 0; c < 6; c++)
                    s.Slot(g, x + 12 + c * 24, top + 44, kind, c < 4 ? Icon(Things[c * 3 + 2].Id) : null, c < 4 ? Things[c * 3 + 2].Rarity : -1, c < 4 ? Things[c * 3 + 2].Count : null);
                s.Well(g, x + 12, top + 72, w - 24, 16, kind);
                g.Small(x + 17, top + 78, "A WELL FOR LISTS AND FIELDS", dim);
                s.Button(g, x + 12, y + h - 28, 56, 18, kind, "BUTTON", null, MockSkin.Press.Normal, kind == Talk ? Dora : (Rgba?)null);
                s.Button(g, x + 72, y + h - 28, 50, 18, kind, "HOVER", null, MockSkin.Press.Hover, kind == Talk ? Dora : (Rgba?)null);
                s.Button(g, x + 126, y + h - 28, 58, 18, kind, "CONFIRM", null, MockSkin.Press.Primary, kind == Talk ? Dora : (Rgba?)null);
            }
            return g;
        }

        // ---- The phone, held upright: the mix ----

        static readonly (string Label, string[] Icon)[] PhoneTabs =
        {
            ("BAG", MockGlyph.Bag), ("SKILLS", MockGlyph.Skills), ("TREE", MockGlyph.Tree), ("LOG", MockGlyph.Book), ("MAP", MockGlyph.Map),
        };

        // The bar along the bottom that turns between the screens (the hub on a phone held upright).
        static void TabBar(PixelImage g, MockSkin s, int chosen)
        {
            int y = PhoneH - 30, w = PhoneW / PhoneTabs.Length;
            g.Fill(0, y, PhoneW, 30, Rgba.Hex("#1D1713"));
            g.Fill(0, y, PhoneW, 1, MockSkin.Black);
            g.Fill(0, y + 1, PhoneW, 1, MockSkin.GoldLight);
            g.Fill(0, y + 2, PhoneW, 1, MockSkin.Gold);
            g.Fill(0, y + 3, PhoneW, 1, MockSkin.Black);
            for (int i = 0; i < PhoneTabs.Length; i++)
            {
                int x = i * w;
                bool on = i == chosen;
                if (on)
                {
                    g.Slice(UiArt.Button(chosen: true), UiArt.ButtonBorder, x + 2, y + 5, w - 4, 23);
                    g.Fill(x + w / 2 - 3, y + 1, 7, 2, Rgba.Hex("#FFF6DC"));
                }
                var ink = on ? Rgba.Hex("#FFF6DC") : Rgba.Hex("#A8977C");
                MockGlyph.Centered(g, x + w / 2, y + 13, PhoneTabs[i].Icon, ink);
                g.Small(x + (w - MockFont.SmallWidth(PhoneTabs[i].Label)) / 2, y + 21, PhoneTabs[i].Label, ink);
                if (i == 2)
                {
                    g.Fill(x + w / 2 + 6, y + 6, 5, 5, MockSkin.Black);
                    g.Fill(x + w / 2 + 7, y + 7, 3, 3, Rgba.Hex("#FFC43A"));
                }
            }
        }

        static PixelImage PhoneBag(MockSkin s)
        {
            var g = new PixelImage(PhoneW, PhoneH);
            Hud(g, s, PhoneW, PhoneH, bars: false, map: false, phone: true);
            s.Button(g, PhoneW - 27, 5, 22, 22, MockKind.Hud, null, MockGlyph.Hub);
            // A sheet come up from the bottom, with the world still showing above it.
            int x = 2, y = 84, w = PhoneW - 4, h = PhoneH - 30 - y + 3;
            int top = s.Window(g, x, y, w, h, Bag, "BAG", MockGlyph.Bag, close: false);
            // The handle to drag it down by, and the close button at the size of a finger.
            g.Fill(PhoneW / 2 - 12, y - 4, 24, 3, MockSkin.Black);
            g.Fill(PhoneW / 2 - 11, y - 3, 22, 1, MockSkin.GoldLight);
            g.Raised(x + w - 26, y + 2, 22, 21, MockSkin.Black, MockSkin.GoldLight, Ramp.Of("#A8382E"));
            MockGlyph.Draw(g, x + w - 26 + 6, y + 2 + 6, MockGlyph.Close, Rgba.Hex("#FFF2DC"));
            int cx = x + 8, cw = w - 16;
            Doll(g, s, cx, top + 2, cw, 82, wide: false);
            int fy = top + 88;
            Filters(g, s, cx, fy, cw, phone: true);
            int gy = fy + 19, gx = (PhoneW - 7 * 24) / 2;
            int rows = (PhoneH - 30 - 34 - gy) / 24;
            Grid(g, s, Bag, gx, gy, 7, rows);
            // The thing held under a finger: its slot marked and its card's short form over the grid's foot.
            s.Slot(g, gx + 4 * 24 + 1, gy + 1, Bag, Icon(Things[4].Id), Things[4].Rarity, chosen: true);
            g.Small(cx + cw - MockFont.SmallWidth("28/60") - 2, gy + rows * 24 + 4, "28/60", s.Dim(Bag), s.Shadow(Bag));
            int ay = y + h - 9 - 22;
            s.Rule(g, cx, ay - 5, cw, Bag);
            g.Small(cx + 2, ay + 2, "FLAME BLADE", MockSkin.RarityColors[3], s.Shadow(Bag));
            g.Small(cx + 2, ay + 10, "HOLD FOR CARD", s.Dim(Bag), s.Shadow(Bag));
            s.Button(g, cx + 58, ay, 44, 22, Bag, "EQUIP", null, MockSkin.Press.Primary);
            s.Button(g, cx + 104, ay, 38, 22, Bag, "QUICK");
            s.Button(g, cx + 144, ay, cw - 144, 22, Bag, "DROP");
            TabBar(g, s, 0);
            return g;
        }

        static PixelImage PhoneTalk(MockSkin s)
        {
            var g = new PixelImage(PhoneW, PhoneH);
            Hud(g, s, PhoneW, PhoneH, bars: false, map: true, phone: true);
            s.Button(g, PhoneW - 27, 55, 22, 22, MockKind.Hud, null, MockGlyph.Hub);
            int x = 2, w = PhoneW - 4, h = 146, y = PhoneH - 4 - h;
            int top = s.Window(g, x, y, w, h, Talk, "DORA", null, Dora, close: false);
            Hearts(g, x + 12 + MockFont.Width("DORA") + 12, y + 9, 3);
            g.Small(x + w - 8 - MockFont.SmallWidth("MERCHANT"), y + 10, "MERCHANT", Rgba.Hex("#FFF2CC"), new Rgba(0, 0, 0, 150));
            Portrait(g, s, x + 8, top + 2, 50, 58, 3, Dora);
            var ink = s.Ink(Talk);
            var shadow = s.Shadow(Talk);
            int tx = x + 64;
            string[] lines = { "WELCOME BACK,", "TRAVELLER. I HAVE", "NEW SEEDS FROM THE", "FROST CAVES. CARE", "TO TAKE A LOOK?" };
            for (int i = 0; i < lines.Length; i++) g.Text(tx, top + 4 + i * 11, lines[i], ink, shadow);
            MockGlyph.Draw(g, x + w - 16, top + 52, MockGlyph.Next, MockSkin.GoldLight);
            // What to do next, each the height of a finger (44 points).
            int cy = top + 64, half = (w - 16 - 3) / 2, third = (w - 16 - 6) / 3;
            s.Button(g, x + 8, cy, half, 22, Talk, Choices[0].Label, Choices[0].Icon, MockSkin.Press.Primary);
            s.Button(g, x + 8 + half + 3, cy, half, 22, Talk, Choices[1].Label, Choices[1].Icon);
            for (int i = 0; i < 3; i++) s.Button(g, x + 8 + i * (third + 3), cy + 25, third, 22, Talk, Choices[2 + i].Label, Choices[2 + i].Icon);
            return g;
        }
    }
}
