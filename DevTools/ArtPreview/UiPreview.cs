using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The game's own UI skin (Docs/UiRework.md U1), drawn from the pieces the game draws (Art/UiArt.cs, Art/UiFrameArt.cs)
    // as UiSkin lays them out, to look at without Unity (out/ui/, --only ui):
    //  - skin.png: every piece — the three levels of panel, the buttons' kinds and states, the close button, the item slot
    //    in each rarity, the head band of each kind of window, the icons;
    //  - windows.png: the existing screens in the new skin — the bag with its icon tabs and slots and a card over it, the
    //    talk with a speaker's band and hearts, the waypoint list, the full map's window, a menu page;
    //  - pixels.png: why the frames are drawn at whole device pixels — a corner at a phone's pixel ratio 2.625 the old way
    //    (stretched: pixels 5 and 6 device pixels across by turns) and the new (5 each).
    // The writing is the preview's small Latin font (UiMockKit), as the tool cannot draw the game's.
    static class UiPreview
    {
        static readonly Rgba Ground = Rgba.Hex("#17141C");
        static readonly Rgba Ink = Rgba.Hex("#EFE4CC");
        static readonly Rgba Dim = Rgba.Hex("#A8977C");
        static readonly Rgba Shadow = new Rgba(0, 0, 0, 150);
        static readonly Rgba[] Rarities =
        {
            Rgba.Hex("#D9D9D9"), Rgba.Hex("#73F273"), Rgba.Hex("#73B3FF"), Rgba.Hex("#CC80FF"), Rgba.Hex("#FF9E2E"),
        };

        public static void Write(string output, LootData loot)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "ui")).FullName;
            Png.Save(Times(Skin(loot), 3), Path.Combine(folder, "skin.png"));
            Png.Save(Times(Windows(loot), 3), Path.Combine(folder, "windows.png"));
            Png.Save(Pixels(), Path.Combine(folder, "pixels.png"));
            Console.WriteLine($"Wrote the UI skin to {folder}");
        }

        // ---- Pieces laid out as UiSkin draws them ----

        static void Slice(PixelImage g, PixelImage frame, int border, int x, int y, int w, int h) => g.Slice(frame, border, x, y, w, h);

        static void Head(PixelImage g, int x, int y, int w, UiKind kind, Rgba accent)
        {
            var left = UiArt.HeadEnd(kind, accent, true);
            var middle = UiArt.HeadMiddle(kind, accent);
            var right = UiArt.HeadEnd(kind, accent, false);
            g.Draw(left, x, y);
            for (int i = 0; i < w - UiArt.HeadCap * 2; i++)
            {
                int column = (UiArt.HeadCap + i) % UiArt.HeadTile;
                for (int j = 0; j < UiArt.HeadHeight; j++) g[x + UiArt.HeadCap + i, y + j] = middle[column, j];
            }
            g.Draw(right, x + w - UiArt.HeadCap, y);
        }

        // A window as UiSkin.Window draws it: the panel, the head inside its frame, the icon and title (the shop's on its
        // sign, a speaker's at the left), the close button; gives the top of what it holds.
        static int Window(PixelImage g, int x, int y, int w, int h, UiKind kind, string title, string[] icon, Rgba accent, bool close = true, int hearts = -1)
        {
            g.BlendFill(x + 2, y + 3, w, h, new Rgba(0, 0, 0, 110));
            Slice(g, UiArt.Panel, UiArt.PanelBorder, x, y, w, h);
            int hx = x + UiArt.PanelBorder, hy = y + UiArt.PanelBorder, hw = w - UiArt.PanelBorder * 2;
            Head(g, hx, hy, hw, kind, accent);
            var ink = UiArt.HeadInk(kind);
            var shadow = UiArt.HeadShadow(kind) ? Shadow : Rgba.Clear;
            int iconW = icon != null ? UiIcons.Of(icon, ink).Width + 3 : 0;
            int whole = iconW + MockFont.Width(title);
            int room = hw - UiArt.HeadCap * 2 - (close ? UiArt.CloseSize + 6 : 0);
            int tx = kind == UiKind.Talk ? hx + UiArt.HeadCap + 1 : hx + UiArt.HeadCap + Math.Max(0, (room - whole) / 2);
            int middle = kind == UiKind.Shop ? hy + 10 : hy + UiArt.HeadBand / 2;
            if (kind == UiKind.Shop) Slice(g, UiArt.Sign, UiArt.SignBorder, tx - 5, hy + 6, whole + 10, 9);
            if (icon != null)
            {
                var image = UiIcons.Of(icon, ink);
                g.Draw(image, tx, middle - image.Height / 2);
            }
            g.Text(tx + iconW, middle - 3, title, ink, shadow);
            if (close) g.Draw(UiArt.Close(), hx + hw - UiArt.CloseSize - 3, hy + (UiArt.HeadBand - UiArt.CloseSize) / 2);
            if (hearts >= 0)
            {
                int hx0 = hx + hw - UiArt.CloseSize - 8 - 5 * 9;
                for (int i = 0; i < 5; i++) g.Draw(UiIcons.Of(UiIcons.Heart, i < hearts ? Rgba.Hex("#E64050") : Rgba.Hex("#40302E")), hx0 + i * 9, hy + 4);
            }
            return hy + UiArt.HeadHeight + 3;
        }

        enum Press
        {
            Normal,
            Hover,
            Down,
            Primary,
            Danger,
            Off,
        }

        static void Button(PixelImage g, int x, int y, int w, int h, string label, Press press = Press.Normal, string[] icon = null, bool tab = false)
        {
            var frame = UiArt.Button(hover: press == Press.Hover, pressed: press == Press.Down, chosen: press == Press.Primary, danger: press == Press.Danger);
            int down = press == Press.Down ? 1 : press == Press.Hover ? -1 : 0;
            if (tab && press != Press.Primary)
            {
                y += 1;
                h -= 1;
            }
            Slice(g, frame, UiArt.ButtonBorder, x, y + down, w, h);
            if (press == Press.Off) g.BlendFill(x + 1, y + 1, w - 2, h - 2, new Rgba(11, 8, 7, 120));
            var ink = press == Press.Primary ? Rgba.Hex("#FFF6DC") : press == Press.Off ? Rgba.Hex("#7A6C5A") : Ink;
            int iconW = icon != null ? UiIcons.Of(icon, ink).Width + 2 : 0;
            int at = x + (w - iconW - MockFont.Width(label)) / 2;
            if (icon != null)
            {
                var image = UiIcons.Of(icon, press == Press.Primary ? Rgba.Hex("#FFE9A0") : Rgba.Hex("#D8B880"));
                g.Draw(image, at, y + down + (h - image.Height) / 2);
            }
            g.Text(at + iconW, y + down + (h - 7) / 2, label, ink, Shadow);
        }

        static void Slot(PixelImage g, int x, int y, int size, Rgba? tint, PixelImage icon = null, string count = null, bool glow = false, bool picked = false)
        {
            Slice(g, UiArt.Slot(tint, glow), UiArt.SlotBorder, x, y, size, size);
            if (picked)
            {
                g.Box(x - 1, y - 1, size + 2, size + 2, Rgba.Hex("#FFE69A"));
            }
            if (icon != null) g.Draw(icon, x + (size - icon.Width) / 2, y + (size - icon.Height) / 2);
            if (count != null) g.Small(x + size - 2 - MockFont.SmallWidth(count), y + size - 7, count, Rgba.White, new Rgba(0, 0, 0, 230));
        }

        static void Card(PixelImage g, int x, int y, int w, int h)
        {
            g.BlendFill(x + 2, y + 3, w, h, new Rgba(0, 0, 0, 130));
            Slice(g, UiArt.Card, UiArt.CardBorder, x, y, w, h);
        }

        static PixelImage Canvas(int w, int h)
        {
            var g = new PixelImage(w, h);
            g.Fill(0, 0, w, h, Ground);
            return g;
        }

        static void Label(PixelImage g, int x, int y, string text) => g.Small(x, y, text, Dim);

        // ---- skin.png ----

        static PixelImage Skin(LootData loot)
        {
            var g = Canvas(420, 330);
            Label(g, 6, 4, "PANEL");
            Slice(g, UiArt.Panel, UiArt.PanelBorder, 6, 11, 80, 44);
            Label(g, 92, 4, "INSET");
            Slice(g, UiArt.Panel, UiArt.PanelBorder, 92, 11, 70, 44);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, 100, 19, 54, 28);
            Label(g, 168, 4, "CARD");
            Card(g, 168, 11, 80, 44);
            g.Text(174, 18, "IRON SWORD", Rgba.Hex("#F6ECD6"), Shadow);
            g.Small(174, 30, "ATTACK 24", Rgba.Hex("#B8A88E"));
            g.Small(174, 38, "+6% CRIT", Rgba.Hex("#7CE06A"));

            Label(g, 256, 4, "BUTTONS");
            var presses = new[] { (Press.Normal, "OK"), (Press.Hover, "HOVER"), (Press.Down, "DOWN"), (Press.Primary, "MAIN"), (Press.Danger, "DROP"), (Press.Off, "OFF") };
            for (int i = 0; i < presses.Length; i++) Button(g, 256 + i % 3 * 54, 12 + i / 3 * 20, 50, 16, presses[i].Item2, presses[i].Item1);
            Label(g, 256, 52, "CLOSE");
            for (int i = 0; i < 4; i++) g.Draw(UiArt.Close(hover: i % 2 == 1, pressed: i >= 2), 284 + i * 16, 50);

            Label(g, 6, 62, "SLOTS: NONE, RARITY 0-4, GLOW 3-4, PICKED");
            var icons = loot.Bases.Take(6).Select(ItemArt.Gear).ToList();
            Slot(g, 6, 70, 22, null);
            for (int r = 0; r < 5; r++) Slot(g, 32 + r * 26, 70, 22, Rarities[r], icons.Count > r ? icons[r] : null, r == 2 ? "3" : null, glow: r >= 3);
            Slot(g, 32 + 5 * 26 + 4, 70, 22, Rarities[1], icons.Count > 5 ? icons[5] : null, null, picked: true);
            Slot(g, 220, 70, 30, Rarities[4], icons.Count > 0 ? icons[0].Times(1) : null, "+2", glow: true);

            Label(g, 6, 104, "HEAD BANDS");
            var kinds = new (UiKind Kind, string Title, string[] Icon, Rgba Accent)[]
            {
                (UiKind.Stone, "SETTINGS", UiIcons.Gear, Rgba.Hex("#C49A55")),
                (UiKind.Bag, "BAG", UiIcons.Bag, default),
                (UiKind.Shop, "DORA", UiIcons.Shop, default),
                (UiKind.Talk, "BARDO", null, Rgba.Hex("#8A6A4A")),
                (UiKind.Talk, "MINA", null, Rgba.Hex("#C85A6A")),
                (UiKind.Journal, "MAP", UiIcons.Map, default),
                (UiKind.Machine, "CHEST", UiIcons.Chest, default),
                (UiKind.Growth, "SKILLS", UiIcons.Skills, default),
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (kind, title, icon, accent) = kinds[i];
                int x = 6 + i % 2 * 206, y = 112 + i / 2 * 26;
                Window(g, x, y, 200, 24, kind, title, icon, accent);
            }

            Label(g, 6, 220, "ICONS");
            var all = new[] { UiIcons.Bag, UiIcons.Hammer, UiIcons.Skills, UiIcons.Tree, UiIcons.Person, UiIcons.Book, UiIcons.Chest, UiIcons.Shop, UiIcons.Map, UiIcons.Waypoint, UiIcons.Talk, UiIcons.Gear, UiIcons.Coin, UiIcons.Heart, UiIcons.Lock, UiIcons.Check };
            for (int i = 0; i < all.Length; i++)
            {
                g.Draw(UiIcons.Of(all[i], Rgba.Hex("#E8C878")), 6 + i * 16, 228);
                g.Draw(UiIcons.Of(all[i], Rgba.Hex("#8C7A62")), 6 + i * 16, 244);
            }

            Label(g, 6, 262, "TABS (CHOSEN STANDS TALLER)");
            var tabs = new[] { ("GEAR", UiIcons.Bag), ("CRAFT", UiIcons.Hammer), ("SKILLS", UiIcons.Skills), ("TREE", UiIcons.Tree), ("JOURNAL", UiIcons.Book) };
            for (int i = 0; i < tabs.Length; i++) Button(g, 6 + i * 70, 270, 66, 20, tabs[i].Item1, i == 0 ? Press.Primary : Press.Normal, tabs[i].Item2, tab: true);
            Label(g, 6, 298, "HEARTS AND COINS");
            for (int i = 0; i < 5; i++) g.Draw(UiIcons.Of(UiIcons.Heart, i < 3 ? Rgba.Hex("#E64050") : Rgba.Hex("#40302E")), 6 + i * 9, 306);
            g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), 60, 305);
            g.Text(70, 306, "1,250", Rgba.Hex("#FFE9A0"), Shadow);
            return g;
        }

        // ---- windows.png: the existing screens in the new skin ----

        static PixelImage Windows(LootData loot)
        {
            var g = Canvas(640, 360);
            // A cave floor's colours behind, so the panels read as they will over the game.
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (PixelNoise.Value(x / 8, y / 8, 7) > 0.6f) g[x, y] = Rgba.Hex("#221E26");

            // The bag (right, as on a PC): leather head with coins, icon tabs, worn row and carried grid.
            int bx = 390, by = 8, bw = 244, bh = 344;
            int top = Window(g, bx, by, bw, bh, UiKind.Bag, "BAG", UiIcons.Bag, default);
            g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), bx + bw - 60, by + 9);
            g.Text(bx + bw - 51, by + 9, "1250", Rgba.Hex("#FFF2CC"), Shadow);
            var tabs = new[] { ("GEAR", UiIcons.Bag), ("CRAFT", UiIcons.Hammer), ("SKILLS", UiIcons.Skills), ("TREE", UiIcons.Tree), ("HERO", UiIcons.Person), ("LOG", UiIcons.Book) };
            int tw = (bw - 12 - 10) / 3;
            for (int i = 0; i < tabs.Length; i++) Button(g, bx + 6 + i % 3 * (tw + 5), top + i / 3 * 22, tw, 20, tabs[i].Item1, i == 0 ? Press.Primary : Press.Normal, tabs[i].Item2, tab: true);
            int gy = top + 50;
            g.Text(bx + 8, gy, "WORN", Dim, Shadow);
            var gear = loot.Bases.Select(ItemArt.Gear).ToList();
            var random = new Random(3);
            for (int i = 0; i < 6; i++)
            {
                if (i < 2) Slot(g, bx + 8 + i * 26, gy + 10, 22, Rarities[i + 1], gear[(i * 7) % gear.Count]);
                else Slot(g, bx + 8 + i * 26, gy + 10, 22, null);
            }
            g.Text(bx + 8, gy + 40, "CARRIED 9/24", Dim, Shadow);
            for (int i = 0; i < 24; i++)
            {
                int col = i % 8, row = i / 8;
                int sx = bx + 8 + col * 28, sy = gy + 50 + row * 28;
                if (i < 9)
                {
                    int rarity = random.Next(5);
                    Slot(g, sx, sy, 24, Rarities[rarity], gear[random.Next(gear.Count)], i == 4 ? "+1" : null, glow: rarity >= 3, picked: i == 2);
                }
                else Slot(g, sx, sy, 24, null);
            }
            // The picked thing's well with its buttons: equip (main), drop (danger), quick.
            int wy = gy + 140;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, bx + 8, wy, bw - 16, 62);
            g.Text(bx + 14, wy + 6, "IRON SWORD", Rgba.Hex("#73B3FF"), Shadow);
            g.Small(bx + 14, wy + 18, "ATTACK 24  SPEED 1.2", Dim);
            g.Small(bx + 14, wy + 26, "VS WORN: +7 ATTACK", Rgba.Hex("#7CE06A"));
            Button(g, bx + 14, wy + 38, 46, 16, "EQUIP", Press.Primary);
            Button(g, bx + 64, wy + 38, 46, 16, "DROP", Press.Danger);
            Button(g, bx + 114, wy + 38, 46, 16, "QUICK");
            // The talk at the bottom: the speaker's band, the words, the answers.
            int ty = Window(g, 8, 250, 370, 102, UiKind.Talk, "MINA", null, Rgba.Hex("#C85A6A"), hearts: 3);
            g.Bars(22, ty + 4, 340, 3, Ink.WithAlpha(150), 5);
            Button(g, 22, ty + 32, 160, 16, "I WILL HELP");
            Button(g, 22, ty + 50, 160, 16, "NOT NOW");
            g.Small(22, 340, "1/3", Dim);
            Button(g, 300, 330, 64, 16, "NEXT", Press.Primary);

            // The waypoint list: parchment head.
            int wy2 = Window(g, 8, 8, 180, 120, UiKind.Journal, "WAYPOINTS", UiIcons.Waypoint, default);
            g.Small(20, wy2 + 2, "TO A PLACE YOU HAVE BEEN", Dim);
            Button(g, 20, wy2 + 12, 156, 16, "DAWN VILLAGE (HERE)", Press.Off);
            Button(g, 20, wy2 + 31, 156, 16, "ROYAL CITY");
            Button(g, 20, wy2 + 50, 156, 16, "FROST HOLLOW");
            Button(g, 120, wy2 + 72, 56, 16, "CLOSE");

            // A menu page: stone head with [BACK] at its left.
            int my = Window(g, 196, 8, 186, 120, UiKind.Stone, "SETTINGS", UiIcons.Gear, Rgba.Hex("#C49A55"), close: false);
            Button(g, 196 + UiArt.PanelBorder + UiArt.HeadCap, 8 + UiArt.PanelBorder + 1, 32, 13, "BACK");
            var rows = new[] { ("SOUND", "4/4"), ("SHAKE", "ON"), ("MOTION", "REDUCED"), ("GUIDE", "ON") };
            for (int i = 0; i < rows.Length; i++)
            {
                g.Text(208, my + 4 + i * 20, rows[i].Item1, Ink, Shadow);
                Button(g, 296, my + i * 20, 76, 16, rows[i].Item2);
            }

            // The full map: parchment head, map in a well, the key under it.
            int mx = 196, mw = 186, mh = 108;
            int mt = Window(g, mx, 136, mw, mh, UiKind.Journal, "MAP", UiIcons.Map, default);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, mx + 6, mt, mw - 12, mh - (mt - 136) - 16);
            for (int y = mt + 2; y < 136 + mh - 18; y++)
                for (int x = mx + 8; x < mx + mw - 8; x++)
                    g[x, y] = PixelNoise.Value(x / 4, y / 4, 9) > 0.45f ? Rgba.Hex("#6A5A40") : Rgba.Hex("#1A1612");
            g.Small(mx + 8, 136 + mh - 12, "* ME  * PLACES  O BOSS", Dim);
            // A card beside the bag, as the description over everything.
            Card(g, bx - 128, gy + 36, 112, 58);
            g.Text(bx - 122, gy + 42, "FLAME BLADE", Rgba.Hex("#CC80FF"), Shadow);
            g.Small(bx - 122, gy + 54, "SWORD  LV 14", Rgba.Hex("#B8A88E"));
            g.Small(bx - 122, gy + 64, "+18% FIRE DAMAGE", Rgba.Hex("#F6ECD6"));
            g.Small(bx - 122, gy + 72, "+4 FIRE ON HIT", Rgba.Hex("#F6ECD6"));
            g.Small(bx - 122, gy + 82, "SELLS FOR 120", Rgba.Hex("#E8C878"));

            return g;
        }

        // ---- pixels.png: a corner of a panel at a phone's ratio, the old way and the new ----

        static PixelImage Pixels()
        {
            const float ratio = 2.625f;
            var corner = new PixelImage(12, 12);
            corner.Draw(UiArt.Panel, 0, 0);
            // Old: the art doubled into the skin's texture (2 texels a pixel), then the GUI matrix stretches it by the ratio.
            int oldSize = (int)(24 * ratio);
            var old = new PixelImage(oldSize, oldSize);
            for (int y = 0; y < oldSize; y++)
                for (int x = 0; x < oldSize; x++) old[x, y] = corner[(int)(x / ratio) / 2, (int)(y / ratio) / 2];
            // New: Dots = round(2 × 2.625) = 5 device pixels a pixel.
            int dots = (int)Math.Round(2 * ratio);
            var sharp = new PixelImage(12 * dots, 12 * dots);
            for (int y = 0; y < sharp.Height; y++)
                for (int x = 0; x < sharp.Width; x++) sharp[x, y] = corner[x / dots, y / dots];
            var g = Canvas(oldSize + sharp.Width + 30, Math.Max(oldSize, sharp.Height) + 20);
            g.Draw(old, 10, 14);
            g.Draw(sharp, oldSize + 20, 14);
            g.Small(10, 4, "OLD 5-6", Dim);
            g.Small(oldSize + 20, 4, "NEW 5", Dim);
            return Times(g, 4);
        }

        static PixelImage Times(PixelImage image, int scale) => image.Times(scale);
    }
}
