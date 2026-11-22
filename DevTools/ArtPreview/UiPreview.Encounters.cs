using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The encounter windows of U3 (Docs/UiRework.md 4-2, 4-4, U3) laid out as the game lays them out, at their size in points
    // (one pixel of the picture a point, as on a PC): the frames, bands and buttons are drawn at the art's scale (one art
    // pixel two points, UiSkin.Pt on a PC) and doubled, and the portraits go over them at their own whole scale (3 points a
    // pixel in a PC's talk, 2 on a phone, 1 in the shop's strip). Positions follow StoryScreen.Lay, ScreenStack.EncounterArea
    // and the panes of InventoryWindow.Encounters.cs, rounded to even points. The writing is the preview's Latin font.
    //  - encounters.png: a PC's shop, station, chest and anvil windows, a stall keeper's talk and a talk with answers;
    //  - encounters_phone.png: a phone held upright (390 x 844) with a stall keeper's talk over where the quick slots were, the
    //    shop and the station as sheets; a phone held sideways (740 x 360) with the shop sheet and a talk.
    static partial class UiPreview
    {
        static readonly Rgba Good = Rgba.Hex("#8CE680");
        static readonly Rgba Bad = Rgba.Hex("#F27366");
        static readonly Rgba Price = Rgba.Hex("#FFD966");

        // Portraits to put over the doubled picture: where (in points), whose, which face, how many points a pixel, how many
        // rows (0 for all).
        static readonly List<(int X, int Y, string Id, Expression Face, int Scale, int Rows)> Faces = new List<(int, int, string, Expression, int, int)>();

        static PixelImage Encounters(LootData loot)
        {
            Faces.Clear();
            var g = Canvas(910, 806);
            Floor(g, 0, 0, g.Width, g.Height, 11);
            var icons = Icons(loot);
            g.Small(10, 2, "U3 ENCOUNTER WINDOWS ON A PC, ONE PIXEL A POINT", Dim);
            Shop(g, 10, 10, 440, 320, icons, phone: false);
            Station(g, 460, 10, 440, 320, icons, phone: false);
            Chest(g, 10, 340, 380, 320, icons);
            Anvil(g, 400, 340, 380, 320, icons);
            g.Small(10, 668, "TALK: A STALL KEEPER (PORTRAIT 3X, HER BAND AND HEARTS, CHOICES) AND A STORY TALK WITH ANSWERS", Dim);
            Talk(g, 10, 678, 330, "MERCHANT DORA", "merchant", "#9A5AC8", Expression.Happy, 3, 2, merchant: true, hearts: 3, columns: 6, touch: false);
            Talk(g, 350, 678, 330, "MINA", "mina", "#C85A6A", Expression.Thinking, 3, 3, merchant: false, hearts: -1, columns: 1, touch: false, answers: 2);
            return Finish(g);
        }

        static PixelImage EncountersPhone(LootData loot)
        {
            Faces.Clear();
            var g = Canvas(1000, 440);
            Floor(g, 0, 0, g.Width, g.Height, 3);
            var icons = Icons(loot);
            g.Small(6, 2, "PHONE 390X844: TALK OVER WHERE THE QUICK SLOTS WERE (NOT DRAWN), SHOP AND STATION SHEETS. 740X360: SHOP SHEET, TALK", Dim);
            // A phone held upright with the HUD, the minimap, [BAG][MENU] and a stall keeper's talk at the bottom.
            Phone(g, 6, 12, 195, 422);
            Slice(g, UiArt.Panel, UiArt.PanelBorder, 11, 17, 120, 40);
            g.Small(17, 25, "HP 192/240", Dim);
            Minimap(g, 6 + 195 - 65, 17, 60);
            Button(g, 6 + 195 - 65, 82, 28, 20, "BAG");
            Button(g, 6 + 195 - 33, 82, 28, 20, "MENU");
            // StoryScreen.Lay: 382 points wide, portrait 2x in its slot, two rows of three choices, 4 points off the bottom.
            Talk(g, 8, 12 + 422 - 2 - 130, 191, "MERCHANT DORA", "merchant", "#9A5AC8", Expression.Happy, 2, 2, merchant: true, hearts: 3, columns: 3, touch: true);
            Phone(g, 207, 12, 195, 422);
            Shop(g, 209, 14, 191, 418, icons, phone: true);
            Phone(g, 408, 12, 195, 422);
            Station(g, 410, 14, 191, 418, icons, phone: true);
            // A phone held sideways: the shop sheet, two panes side by side.
            Phone(g, 610, 12, 370, 180);
            Shop(g, 612, 14, 366, 176, icons, phone: true);
            Phone(g, 610, 200, 370, 180);
            Minimap(g, 610 + 370 - 65, 205, 60);
            Button(g, 610 + 370 - 65, 270, 28, 20, "BAG");
            Button(g, 610 + 370 - 33, 270, 28, 20, "MENU");
            // A low screen: the talk from the left edge to the minimap column, the head and shoulders only.
            Talk(g, 614, 200 + 180 - 2 - 70, 297, "MINA", "mina", "#C85A6A", Expression.Surprised, 2, 1, merchant: false, hearts: -1, columns: 2, touch: true, answers: 2, rows: 36);
            return Finish(g);
        }

        // The doubled picture with the portraits over it.
        static PixelImage Finish(PixelImage g)
        {
            var big = g.Times(2);
            foreach (var (x, y, id, face, scale, rows) in Faces)
            {
                var image = PortraitArt.Of(id, face);
                int shown = rows > 0 ? rows : image.Height;
                for (int j = 0; j < shown * scale; j++)
                    for (int i = 0; i < image.Width * scale; i++) big.Blend(x + i, y + j, image[i / scale, j / scale]);
            }
            return big;
        }

        static List<PixelImage> Icons(LootData loot)
        {
            var list = loot.Bases.Select(ItemArt.Gear).ToList();
            return list.Count > 0 ? list : new List<PixelImage> { new PixelImage(16, 16) };
        }

        static void Floor(PixelImage g, int x, int y, int w, int h, int salt)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    if (PixelNoise.Value(i / 8, j / 8, salt) > 0.6f) g[i, j] = Rgba.Hex("#221E26");
        }

        static void Phone(PixelImage g, int x, int y, int w, int h)
        {
            g.Box(x - 1, y - 1, w + 2, h + 2, Rgba.Hex("#000000"));
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    g[i, j] = PixelNoise.Value(i / 6, j / 6, 5) > 0.55f ? Rgba.Hex("#2E2832") : Rgba.Hex("#1E1A22");
        }

        static void Minimap(PixelImage g, int x, int y, int size)
        {
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, size, size);
            for (int j = y + 3; j < y + size - 3; j++)
                for (int i = x + 3; i < x + size - 3; i++)
                    g[i, j] = PixelNoise.Value(i / 3, j / 3, 4) > 0.5f ? Rgba.Hex("#6A5A40") : Rgba.Hex("#1A1612");
        }

        // A window's inside (UiSkin.InsideOf) in art pixels: under the head, 12 points (6 pixels) in from the sides.
        static (int X, int Y, int W, int Bottom) Inside(int x, int y, int w, int h, int top) => (x + 6, top, w - 12, y + h - 5);

        // Tabs as RowTabs draws them, the chosen one gold and taller.
        static void RowTabs(PixelImage g, int x, int y, int w, int h, string[] names, int chosen)
        {
            int width = (w - 2 * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++) Button(g, x + i * (width + 2), y, width, h, names[i], i == chosen ? Press.Primary : Press.Normal, tab: true);
        }

        // A card (InventoryWindow.Card): the well, the slot with the icon, the name over the second line, the price with a
        // coin, then the buttons.
        static void CardRow(PixelImage g, int x, int y, int w, int h, PixelImage icon, Rgba edge, string name, string sub, Rgba subColor, int price,
            (string Label, int Width, Press Press)[] buttons, bool dim = false)
        {
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            int slot = h - 5;
            Slot(g, x + 2, y + 2, slot, edge, icon);
            int right = x + w - 3 - buttons.Sum(b => b.Width + 2);
            if (price >= 0)
            {
                string text = price.ToString();
                int tw = MockFont.Width(text);
                g.Text(right - tw - 2, y + h / 2 - 3, text, Price, Shadow);
                g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), right - tw - 12, y + h / 2 - 3);
                right -= tw + 16;
            }
            var ink = dim ? Rgba.Hex("#8A7C6A") : Ink;
            if (string.IsNullOrEmpty(sub)) g.Text(x + slot + 6, y + h / 2 - 3, Clip(name, right - x - slot - 8), ink, Shadow);
            else
            {
                g.Text(x + slot + 6, y + 3, Clip(name, right - x - slot - 8), ink, Shadow);
                g.Small(x + slot + 6, y + h - 8, Clip(sub, (right - x - slot - 8) * 3 / 2, small: true), subColor);
            }
            int bx = x + w - 3 - buttons.Sum(b => b.Width + 2) + 2;
            foreach (var (label, width, press) in buttons)
            {
                Button(g, bx, y + (h - 14) / 2, width, 14, label, dim ? Press.Off : press);
                bx += width + 2;
            }
        }

        static string Clip(string text, int width, bool small = false)
        {
            while (text.Length > 1 && (small ? MockFont.SmallWidth(text) : MockFont.Width(text)) > width) text = text.Substring(0, text.Length - 1);
            return text;
        }

        // A pane in a well with its heading (the bag's side).
        static int Well(PixelImage g, int x, int y, int w, int h, string heading)
        {
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            g.Text(x + 4, y + 3, heading, Rgba.Hex("#F2C76B"), Shadow);
            return y + 13;
        }

        static void Grid(PixelImage g, int x, int y, int w, int bottom, int cell, List<PixelImage> icons, int count, int salt, bool materials = false)
        {
            int columns = Math.Max(1, (w + 2) / (cell + 2));
            var random = new Random(salt);
            for (int i = 0; ; i++)
            {
                int cx = x + i % columns * (cell + 2), cy = y + i / columns * (cell + 2);
                if (cy + cell > bottom) break;
                if (i < count)
                {
                    int rarity = materials ? 0 : random.Next(5);
                    Slot(g, cx, cy, cell, materials ? Rgba.Hex("#BFBFB8") : Rarities[rarity], icons[random.Next(icons.Count)], materials || i % 3 == 0 ? (1 + random.Next(40)).ToString() : null, glow: rarity >= 3);
                }
                else Slot(g, cx, cy, cell, null);
            }
        }

        // ---- The shop ----

        static void Shop(PixelImage g, int x, int y, int w, int h, List<PixelImage> icons, bool phone)
        {
            int top = Window(g, x, y, w, h, UiKind.Shop, "MERCHANT DORA", UiIcons.Shop, default);
            g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), x + w - 50, y + 9);
            g.Text(x + w - 41, y + 9, "1250", Rgba.Hex("#FFF2CC"), Shadow);
            var (ix, iy, iw, bottom) = Inside(x, y, w, h, top);
            bool low = h < 200;
            bool narrow = iw < 260;
            // Her strip: the portrait at one point a pixel (the head alone on a low screen), name, hearts, today's line, buttons.
            int slotW = 27, slotH = low ? 21 : 32;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, ix, iy, slotW, slotH);
            g.BlendFill(ix + 2, iy + 2, slotW - 4, slotH - 4, Rgba.Hex("#9A5AC8").WithAlpha(140));
            Faces.Add((ix * 2 + 6, iy * 2 + slotH * 2 - 4 - (low ? 30 : 52), "merchant", Expression.Happy, 1, low ? 30 : 0));
            int tx = ix + slotW + 4;
            int buttons = narrow ? 0 : 112;
            g.Text(tx, iy + 1, Clip("MERCHANT DORA  3/5 (310/500)", iw - slotW - buttons - 8), Rgba.Hex("#F2C76B"), Shadow);
            if (!low)
            {
                for (int i = 0; i < 5; i++) g.Draw(UiIcons.Of(UiIcons.Heart, i < 3 ? Rgba.Hex("#E64050") : Rgba.Hex("#40302E")), tx + i * 8, iy + 10);
                g.Bars(tx, iy + 20, iw - slotW - buttons - 10, 2, Ink.WithAlpha(150), 3, 6);
            }
            else g.Bars(tx, iy + 11, iw - slotW - buttons - 10, 1, Ink.WithAlpha(150), 3, 6);
            int bh = phone ? 20 : 14;
            int stripH = narrow ? slotH + 2 + bh : Math.Max(slotH, bh);
            if (narrow)
            {
                Button(g, ix, iy + slotH + 2, (iw - 2) / 2, bh, "TALK");
                Button(g, ix + (iw - 2) / 2 + 2, iy + slotH + 2, (iw - 2) / 2, bh, "INVITE", Press.Off);
            }
            else
            {
                Button(g, ix + iw - 112, iy + (slotH - bh) / 2, 55, bh, "TALK");
                Button(g, ix + iw - 55, iy + (slotH - bh) / 2, 55, bh, "INVITE", Press.Off);
            }
            int tabsY = iy + stripH + 2, tabH = phone ? 20 : 15;
            RowTabs(g, ix, tabsY, iw, tabH, new[] { "BUY", "SELL", "GIFT", "ASK", "STORY" }, 0);
            int panesY = tabsY + tabH + 3;
            var (list, bag) = SplitArt(ix, panesY, iw, bottom - panesY, 0.6f);
            int cardH = phone ? 25 : 21;
            var goods = new (string Name, string Sub, int Price, bool Open)[]
            {
                ("MUSHROOM SPORE", "SEED", 5, true), ("FIRE PEPPER SEED", "SEED", 15, true), ("HEALING POTION", "TODAY ONLY", 45, true),
                ("MANA POTION", "USE", 40, true), ("RECALL SCROLL", "USE", 60, true), ("FERTILIZER", "FARM", 8, true), ("MAP SCRAP", "OPENS AT 4 HEARTS", 0, false),
            };
            g.Small(list.X, list.Y, "EACH HEART: BUY 5% LESS, SELL 5% MORE", Dim);
            int cy = list.Y + 8;
            for (int i = 0; i < goods.Length && cy + cardH <= list.Y + list.H; i++, cy += cardH + 2)
            {
                var (name, sub, price, open) = goods[i];
                CardRow(g, list.X, cy, list.W - 6, cardH, icons[(i * 5) % icons.Count], i % 2 == 0 ? Rgba.Hex("#BFBFB8") : Rgba.Hex("#F25973"), name, sub, open ? Dim : Bad,
                    open ? price : -1, new[] { ("BUY", 28, Press.Primary), ("X5", 20, Press.Normal) }, dim: !open);
            }
            int gy = Well(g, bag.X, bag.Y, bag.W, bag.H, "BAG 9/24");
            Grid(g, bag.X + 4, gy, bag.W - 8, bag.Y + bag.H - 3, phone ? 23 : 20, icons, 14, 5);
        }

        // Two panes as InventoryWindow.Split: side by side when the inside is 560 points (280 pixels) wide, else one over
        // the other.
        static ((int X, int Y, int W, int H) First, (int X, int Y, int W, int H) Second) SplitArt(int x, int y, int w, int h, float share)
        {
            if (w >= 280)
            {
                int first = (int)Math.Round((w - 4) * share);
                return ((x, y, first, h), (x + first + 4, y, w - first - 4, h));
            }
            int top = (int)Math.Round((h - 4) * Math.Max(share, 0.56f));
            return ((x, y, w, top), (x, y + top + 4, w, h - top - 4));
        }

        // ---- The station ----

        static void Station(PixelImage g, int x, int y, int w, int h, List<PixelImage> icons, bool phone)
        {
            int top = Window(g, x, y, w, h, UiKind.Machine, "WORKBENCH-FURNACE", UiIcons.Hammer, default);
            var (ix, iy, iw, bottom) = Inside(x, y, w, h, top);
            int bh = phone ? 20 : 14;
            g.Text(ix, iy + 2, Clip("MAKING: COPPER SWORD, 2 MORE", iw - 34), Ink, Shadow);
            Button(g, ix + iw - 28, iy, 28, bh, "STOP");
            Slice(g, UiArt.Inset, UiArt.InsetBorder, ix, iy + bh + 1, iw, 5);
            g.Fill(ix + 2, iy + bh + 2, (iw - 4) * 3 / 5, 2, Rgba.Hex("#FFCC59"));
            int tabsY = iy + bh + 9, tabH = phone ? 20 : 15;
            bool narrow = iw < 260;
            RowTabs(g, ix, tabsY, narrow ? iw : iw - 81, tabH, new[] { "BENCH", "FURNACE", "HAND" }, 0);
            if (narrow) Button(g, ix + iw - 78, tabsY + tabH + 2, 78, bh, "CRAFTABLE ONLY");
            else Button(g, ix + iw - 78, tabsY, 78, tabH, "CRAFTABLE ONLY");
            int panesY = tabsY + tabH + 3 + (narrow ? bh + 2 : 0);
            var (list, bag) = SplitArt(ix, panesY, iw, bottom - panesY, 0.62f);
            int cardH = phone ? 25 : 21;
            var recipes = new (string Name, string Needs, bool Ready)[]
            {
                ("COPPER SWORD", "COPPER ORE 12/5 - WOOD 3/2 - 3S", true), ("COPPER ARMOR", "COPPER ORE 12/8 - 4S", true), ("FURNACE", "STONE 30/20", true),
                ("CHEST", "WOOD 3/8", false), ("TORCH X4", "WOOD 3/1 - COAL 0/1", false), ("IRON SWORD", "IRON BAR 0/6 - 5S", false), ("FLOOR", "STONE 30/2", true),
            };
            int cy = list.Y;
            for (int i = 0; i < recipes.Length && cy + cardH <= list.Y + list.H; i++, cy += cardH + 2)
            {
                var (name, needs, ready) = recipes[i];
                CardRow(g, list.X, cy, list.W - 6, cardH, icons[(i * 7 + 2) % icons.Count], Rgba.Hex("#D9D9D9"), name, needs, ready ? Good : Dim, -1,
                    new[] { ("MAKE", 32, Press.Primary), ("X2", 20, Press.Normal) }, dim: !ready);
            }
            int gy = Well(g, bag.X, bag.Y, bag.W, bag.H, "MATERIALS");
            Grid(g, bag.X + 4, gy, bag.W - 8, bag.Y + bag.H - 3, phone ? 23 : 20, icons, 9, 8, materials: true);
        }

        // ---- The chest ----

        static void Chest(PixelImage g, int x, int y, int w, int h, List<PixelImage> icons)
        {
            int top = Window(g, x, y, w, h, UiKind.Machine, "CHEST 3/24", UiIcons.Chest, default);
            var (ix, iy, iw, bottom) = Inside(x, y, w, h, top);
            var (chest, bag) = SplitArt(ix, iy, iw, bottom - iy, 0.5f);
            int cy = Well(g, chest.X, chest.Y, chest.W, chest.H, "CHEST (3/24)");
            string[] stored = { "STONE X40", "COPPER ORE X12", "SLIME GEL X6", "IRON SWORD" };
            for (int i = 0; i < stored.Length; i++) Row(g, chest.X + 4, cy + i * 17, chest.W - 8, icons[(i * 3) % icons.Count], stored[i], "TAKE");
            int by = Well(g, bag.X, bag.Y, bag.W, bag.H, "BAG (9/24)");
            g.Small(bag.X + 4, by + 2, "IN CHESTS NEARBY: 2 KINDS", Dim);
            Button(g, bag.X + bag.W - 54, by, 50, 12, "STASH");
            string[] carried = { "STONE X12", "TORCH X8", "HEALING POTION X4", "COPPER SWORD", "WOOD X3", "MUSHROOM X5" };
            for (int i = 0; i < carried.Length; i++) Row(g, bag.X + 4, by + 16 + i * 17, bag.W - 8, icons[(i * 5 + 1) % icons.Count], carried[i], "PUT");
        }

        // A row as InventoryWindow.DrawContents draws it: the icon, the name, the button.
        static void Row(PixelImage g, int x, int y, int w, PixelImage icon, string name, string action)
        {
            g.Draw(icon, x, y);
            g.Text(x + 16, y + 4, Clip(name, w - 56), Ink, Shadow);
            Button(g, x + w - 34, y, 32, 14, action);
        }

        // ---- The anvil ----

        static void Anvil(PixelImage g, int x, int y, int w, int h, List<PixelImage> icons)
        {
            int top = Window(g, x, y, w, h, UiKind.Machine, "ANVIL", UiIcons.Hammer, default);
            var (ix, iy, iw, bottom) = Inside(x, y, w, h, top);
            RowTabs(g, ix, iy, iw, 15, new[] { "REFINE GEAR", "ANVIL RECIPES" }, 0);
            var (gear, work) = SplitArt(ix, iy + 18, iw, bottom - iy - 18, 0.5f);
            g.Text(gear.X, gear.Y + 2, "WORN", Dim, Shadow);
            Grid(g, gear.X, gear.Y + 11, gear.W - 6, gear.Y + 11 + 22, 22, icons, 3, 2);
            g.Text(gear.X, gear.Y + 38, "CARRIED 4/24", Dim, Shadow);
            Grid(g, gear.X, gear.Y + 47, gear.W - 6, gear.Y + 47 + 46, 22, icons, 4, 4);
            Slot(g, gear.X + 24, gear.Y + 47, 22, Rarities[2], icons[3 % icons.Count], "+1", picked: true);
            g.Draw(icons[3 % icons.Count], gear.X, gear.Y + 100);
            g.Text(gear.X + 16, gear.Y + 104, "IRON SWORD", Rgba.Hex("#73B3FF"), Shadow);
            Button(g, gear.X + gear.W - 40, gear.Y + 100, 34, 14, "REFINE");
            int wy = Well(g, work.X, work.Y, work.W, work.H, "ANVIL");
            CardRow(g, work.X + 4, wy, work.W - 8, 21, icons[3 % icons.Count], Rarities[2], "IRON SWORD", "UPGRADE +1/10 - ESSENCE 60", Dim, -1, Array.Empty<(string, int, Press)>());
            string[] actions = { "UPGRADE +2 (ESSENCE 4, IRON BAR 2)", "MOVE UPGRADE: COPPER SWORD +2 HERE", "SALVAGE (ESSENCE +3)", "REROLL AFFIXES (ESSENCE 8)" };
            var looks = new[] { Press.Primary, Press.Normal, Press.Danger, Press.Normal };
            for (int i = 0; i < actions.Length; i++) Button(g, work.X + 4, wy + 25 + i * 17, work.W - 8, 14, actions[i], looks[i]);
        }

        // ---- The talk (StoryScreen.Lay) ----

        // The talk box at (x, y) art pixels, `w` wide: the speaker's band and hearts, the portrait in its slot at `scale`
        // points a pixel (only its top `rows` rows when given), lines of words, and under them a stall keeper's six choices
        // (`columns` to a row) or the answers to pick, or [NEXT].
        static void Talk(PixelImage g, int x, int y, int w, string name, string id, string color, Expression face, int scale, int lines, bool merchant, int hearts, int columns, bool touch,
            int answers = 0, int rows = 0)
        {
            var accent = Rgba.Hex(color);
            int shown = rows > 0 ? rows : PortraitArt.Height;
            int slotW = (PortraitArt.Width * scale + 12) / 2, slotH = (shown * scale + 12) / 2;
            int button = touch ? 20 : 15;
            int actionRows = merchant ? (6 + columns - 1) / columns : answers > 0 ? (answers + columns - 1) / columns : 1;
            int actions = actionRows * button + (actionRows - 1) * 3;
            bool under = w < 260;
            int textH = lines * 9;
            int body = under ? Math.Max(slotH, textH) + 3 + actions : Math.Max(slotH, textH + 3 + actions);
            int top = 4 + UiArt.HeadHeight + 4;
            int h = top + body + 5;
            int inner = Window(g, x, y, w, h, UiKind.Talk, name, null, accent.Mix(Rgba.Hex("#594033"), 0.25f), hearts: hearts);
            int sx = x + 5, sy = y + top;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, sx, sy, slotW, slotH);
            g.BlendFill(sx + 2, sy + 2, slotW - 4, slotH - 4, accent.WithAlpha(140));
            Faces.Add((sx * 2 + (slotW * 2 - PortraitArt.Width * scale) / 2, sy * 2 + slotH * 2 - 4 - shown * scale, id, face, scale, rows));
            int tx = sx + slotW + 5;
            g.Bars(tx, sy + 2, x + w - 5 - tx, lines, Ink.WithAlpha(170), id.Length, 9);
            int ax = under ? x + 5 : tx, aw = x + w - 5 - ax, ay = sy + body - actions;
            if (merchant)
            {
                string[] labels = { "TRADE", "ASK", "GIFT", "STORY", "CHAT", "CLOSE" };
                int bw = (aw - (columns - 1) * 3) / columns;
                for (int i = 0; i < labels.Length; i++) Button(g, ax + i % columns * (bw + 3), ay + i / columns * (button + 3), bw, button, labels[i], i == 0 ? Press.Primary : Press.Normal);
            }
            else if (answers > 0)
            {
                string[] labels = { "IT WAS NOTHING", "I SAW SOMETHING", "LEAVE IT" };
                int bw = (aw - (columns - 1) * 3) / columns;
                for (int i = 0; i < answers; i++) Button(g, ax + i % columns * (bw + 3), ay + i / columns * (button + 3), bw, button, labels[i]);
            }
            else
            {
                g.Small(ax, ay + button / 2 - 2, "1/3", Dim);
                Button(g, ax + aw - 50, ay, 50, button, "NEXT");
            }
            if (!merchant && answers == 0) g.Small(x + w - 12, ay - 8, "V", Rgba.Hex("#F2C76B"));
        }
    }
}
