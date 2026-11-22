using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The records of U5 (Docs/UiRework.md 4-1 기록, U5) laid out as the game lays them out (InventoryWindow.Journal.cs,
    // MapView.Full.cs), at their size in points with the frames at the art's scale (one art pixel two points) and doubled,
    // as the growth screens' preview: the journal as a parchment book (its index with the red ribbon and the wax seal, the
    // page of notes with portraits, progress bars and buttons), and the map on its sheet with the unexplored left bare,
    // the marks drawn in code, the legend's toggles, the zoom buttons, the compass and a mark's card. The map is a real
    // generated world (seed 1) with an explored patch about the spawn; the portraits and icons are the game's art. The
    // writing is the preview's Latin font.
    //  - records.png: a PC 1280 x 720: the journal's goals, its story, its bestiary with a picked entry, its deeds, the map
    //    with a waypoint's card, and the map zoomed out with a rumour's ring.
    //  - records_phone.png: a phone held upright (390 x 844): the journal's goals under its tab strip, the bestiary, the map
    //    with the legend under it, the map with a card; a phone held sideways (740 x 360): the journal and the map.
    static partial class UiPreview
    {
        static readonly Rgba InkDark = Rgba.Hex("#3A2414");
        static readonly Rgba InkFaint = Rgba.Hex("#6E5436");
        static readonly Rgba InkRed = Rgba.Hex("#8C2A1A");
        static readonly Rgba InkGreen = Rgba.Hex("#2E6A24");
        static readonly Rgba InkGold = Rgba.Hex("#9A6A10");

        static PixelImage Records(LootData loot, GeneratedWorld world, MonsterData monsters)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1300, 1130);
            Floor(g, 0, 0, g.Width, g.Height, 23);
            g.Small(10, 2, "U5 RECORDS ON A PC 1280X720, ONE PIXEL A POINT: JOURNAL (J) GOALS, STORY, BESTIARY, DEEDS; MAP (M) WITH A CARD, ZOOMED OUT", Dim);
            PcScreen(g, 5, 10, s => Journal(g, s, 0, phone: false, low: false, loot, monsters));
            PcScreen(g, 655, 10, s => Journal(g, s, 1, phone: false, low: false, loot, monsters));
            PcScreen(g, 5, 385, s => Journal(g, s, 3, phone: false, low: false, loot, monsters));
            PcScreen(g, 655, 385, s => Journal(g, s, 4, phone: false, low: false, loot, monsters));
            PcScreen(g, 5, 760, s => MapScreen(g, s, world, phone: false, low: false, card: true, zoom: 1));
            PcScreen(g, 655, 760, s => MapScreen(g, s, world, phone: false, low: false, card: false, zoom: 0.4f));
            return Finish(g);
        }

        static PixelImage RecordsPhone(LootData loot, GeneratedWorld world, MonsterData monsters)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1196, 640);
            Floor(g, 0, 0, g.Width, g.Height, 29);
            g.Small(6, 2, "PHONE 390X844: JOURNAL GOALS AND BESTIARY (TAB STRIP), MAP WITH THE LEGEND UNDER IT, A MARK'S CARD. 740X360: JOURNAL, MAP", Dim);
            for (int i = 0; i < 4; i++)
            {
                int px = 6 + i * 201;
                Phone(g, px, 12, 195, 422);
                var area = (px + 2, 14, 191, 418);
                if (i == 0) Journal(g, area, 0, phone: true, low: false, loot, monsters);
                else if (i == 1) Journal(g, area, 3, phone: true, low: false, loot, monsters);
                else MapScreen(g, area, world, phone: true, low: false, card: i == 3, zoom: i == 3 ? 1.6f : 1);
            }
            Phone(g, 812, 12, 370, 180);
            Journal(g, (814, 14, 366, 176), 0, phone: true, low: true, loot, monsters);
            Phone(g, 812, 200, 370, 180);
            MapScreen(g, (814, 202, 366, 176), world, phone: true, low: true, card: true, zoom: 1);
            Phone(g, 812, 400, 370, 180);
            Journal(g, (814, 402, 366, 176), 1, phone: true, low: true, loot, monsters);
            return Finish(g);
        }

        // ---- Parchment ----

        static PixelImage _parchmentTile;

        // The parchment tiled over the rect from its own corner, times the shade.
        static void Parch(PixelImage g, int x, int y, int w, int h, float shade = 1)
        {
            _parchmentTile ??= RecordArt.Parchment();
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    g[x + i, y + j] = _parchmentTile[i % RecordArt.PageTile, j % RecordArt.PageTile].Scale(shade);
        }

        // The burnt edge round a page, blended over what is under it.
        static void PageEdge(PixelImage g, int x, int y, int w, int h)
        {
            var frame = RecordArt.PageFrame;
            int border = RecordArt.PageBorder;
            for (int j = 0; j < h; j++)
            {
                int sy = j < border ? j : j >= h - border ? frame.Height - (h - j) : border;
                for (int i = 0; i < w; i++)
                {
                    int sx = i < border ? i : i >= w - border ? frame.Width - (w - i) : border;
                    g.Blend(x + i, y + j, frame[sx, sy]);
                }
            }
        }

        // ---- The journal ----

        sealed class PNote
        {
            public string Tag = "";
            public string Title = "";
            public List<(string Text, Rgba Color)> Lines = new List<(string, Rgba)>();
            public float Share = -1;
            public string Bar = "";
            public Rgba Accent = Rgba.Hex("#8C2A1A");
            public string Person;
            public Expression Face = Expression.Neutral;
            public string[] Icon;
            public Rgba IconColor = Rgba.Hex("#80603A");
            public bool Sealed;
            public bool Tracked;
            public List<(string Label, Press Press)> Buttons = new List<(string, Press)>();
        }

        static readonly string[] SectionNames = { "GOALS", "STORY", "RUMOURS", "BESTIARY", "DEEDS" };
        static readonly string[][] SectionIcons = { MapIcons.Pin, UiIcons.Book, UiIcons.Talk, MapIcons.Skull, UiIcons.Check };
        static readonly string[] SectionCounts = { "4", "9", "2", "23/96", "3/14" };

        static void Journal(PixelImage g, (int X, int Y, int W, int H) a, int section, bool phone, bool low, LootData loot, MonsterData monsters)
        {
            int top = Window(g, a.X, a.Y, a.W, a.H, UiKind.Journal, phone ? "JOURNAL" : "JOURNAL (J)", UiIcons.Book, default);
            if (a.W >= 150)
            {
                g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), a.X + a.W - 50, a.Y + 9);
                g.Text(a.X + a.W - 41, a.Y + 9, "1250", UiArt.HeadInk(UiKind.Journal));
            }
            var inside = Inside(a.X, a.Y, a.W, a.H, top);
            GroupTabs(g, inside.X, top, inside.W, new[] { ("JOURNAL", UiIcons.Book), ("MAP", UiIcons.Map) }, 0);
            int bodyTop = top + 20 + (low ? 2 : 4);
            int bx = inside.X, bw = inside.W, bh = inside.Bottom - bodyTop;
            Parch(g, bx, bodyTop, bw, bh);
            PageEdge(g, bx, bodyTop, bw, bh);
            int edge = RecordArt.PageBorder;
            int ix = bx + edge, iy = bodyTop + edge, iw = bw - 2 * edge, ih = bh - 2 * edge;
            int px, py, pw, pbottom;
            if (iw >= 260)
            {
                // The index at the left: its entries, the ribbon on the open one, the margin note and the seal.
                int width = Math.Clamp((int)Math.Round(iw * 0.22), 75, 100);
                int row = phone ? low ? 20 : 22 : 19;
                for (int i = 0; i < 5; i++)
                {
                    int ry = iy + 2 + (low ? 1 : 4) + i * (row + (low ? 1 : 2));
                    bool chosen = i == section;
                    if (chosen) g.BlendFill(ix + 1, ry, width - 2, row, new Rgba(158, 112, 56, 72));
                    g.BlendFill(ix + 4, ry + row, width - 8, 1, new Rgba(110, 80, 46, 90));
                    var icon = UiIcons.Of(SectionIcons[i], chosen ? Rgba.Hex("#B83824") : Rgba.Hex("#80603A"));
                    g.Draw(icon, ix + 8, ry + (row - icon.Height) / 2);
                    g.Text(ix + 11 + icon.Width, ry + (row - 7) / 2, SectionNames[i], chosen ? InkRed : InkDark);
                    string count = SectionCounts[i];
                    g.Small(ix + width - 4 - MockFont.SmallWidth(count), ry + (row - 5) / 2, count, InkFaint);
                    if (chosen) g.Draw(RecordArt.Ribbon(Rgba.Hex("#B03A2E")), ix, ry - 3);
                }
                int marginY = iy + 6 + 5 * (row + 2) + 5;
                if (marginY + 30 < iy + ih)
                {
                    g.Small(ix + 5, marginY, "DAY 12", InkFaint);
                    g.Small(ix + 5, marginY + 8, "GOALS 7/31", InkFaint);
                    g.Small(ix + 5, marginY + 16, "DEEDS 3/14", InkFaint);
                    marginY += 32;
                }
                var seal = RecordArt.Seal(Rgba.Hex("#A8322A"));
                if (marginY + seal.Height + 2 < iy + ih) g.Draw(seal, ix + (width - seal.Width) / 2, iy + ih - seal.Height - 3);
                // The fold between the two pages.
                g.BlendFill(ix + width + 2, bodyTop + 3, 1, bh - 6, new Rgba(92, 61, 33, 90));
                g.BlendFill(ix + width + 3, bodyTop + 3, 2, bh - 6, new Rgba(92, 61, 33, 30));
                px = ix + width + 5;
                py = iy + 2;
                pw = ix + iw - 1 - px;
                pbottom = iy + ih - 2;
            }
            else
            {
                // A phone held upright: the sections as a strip of tabs along the top.
                int tab = phone ? 20 : 17;
                int width = (iw - 2 - 2 * 4) / 5;
                for (int i = 0; i < 5; i++)
                {
                    string name = new[] { "GOALS", "STORY", "RUMOR", "BEAST", "DEEDS" }[i];
                    Button(g, ix + 1 + i * (width + 2), iy + 2, width, tab, name, i == section ? Press.Primary : Press.Normal, tab: true);
                }
                px = ix + 1;
                py = iy + 2 + tab + 3;
                pw = iw - 2;
                pbottom = iy + ih - 2;
            }
            // The page's scroll bar.
            g.BlendFill(px + pw - 3, py + 2, 2, pbottom - py - 4, new Rgba(92, 61, 33, 60));
            g.BlendFill(px + pw - 3, py + 2, 2, (pbottom - py) / 3, new Rgba(92, 61, 33, 160));
            pw -= 6;
            int y = py;
            var notes = section switch
            {
                0 => GoalNotes(),
                1 => StoryNotes(),
                3 => null,
                _ => DeedNotes(),
            };
            if (section == 3)
            {
                Bestiary(g, px, y, pw, pbottom, loot, monsters, phone);
                return;
            }
            foreach (var item in notes)
            {
                if (y >= pbottom - 6) break;
                if (item is string heading)
                {
                    if (heading.StartsWith("#"))
                    {
                        g.Text(px + 3, y + 3, heading.Substring(1), InkGold);
                        y += 12;
                        continue;
                    }
                    if (heading.StartsWith("."))
                    {
                        g.Small(px + 8, y + 1, heading.Substring(1), heading.StartsWith(".  ") ? InkFaint : InkDark);
                        y += 8;
                        continue;
                    }
                    g.Text(px + 2, y + 4, heading, InkRed);
                    g.BlendFill(px + 2, y + 13, pw - 4, 1, new Rgba(140, 42, 26, 110));
                    g.Fill(px + pw / 2 - 1, y + 12, 3, 3, new Rgba(140, 42, 26, 180));
                    y += 17;
                    continue;
                }
                y += DrawPNote(g, px, y, pw, Math.Min(pbottom, y + 200), (PNote)item, phone) + 3;
            }
        }

        // A note as InventoryWindow.DrawNote lays it out; gives its height.
        static int DrawPNote(PixelImage g, int x, int y, int w, int bottom, PNote n, bool phone)
        {
            int left = n.Person != null ? 29 : n.Icon != null ? 17 : 6;
            int width = w - left - 4 - (n.Sealed ? 17 : 0);
            var titleLines = Wrap(n.Title, width);
            var lines = n.Lines.SelectMany(l => Wrap(l.Text, width, small: true).Select(t => (t, l.Color))).ToList();
            int button = phone ? 20 : 14;
            int h = 4 + (n.Tag.Length > 0 ? 8 : 0) + titleLines.Count * 9 + 1 + lines.Count * 7 + (n.Share >= 0 ? 9 : 0) + (n.Buttons.Count > 0 ? 3 + button : 0) + 4;
            if (n.Person != null) h = Math.Max(h, 4 + 19 + 4);
            h = Math.Min(h, bottom - y);
            if (h < 12) return h;
            g.BlendFill(x, y, w, h, new Rgba(255, 248, 220, 105));
            g.Box(x, y, w, h, new Rgba(110, 80, 46, 180));
            g.Fill(x + 1, y + 1, 2, h - 2, n.Accent);
            if (n.Tracked) g.Box(x - 1, y - 1, w + 2, h + 2, Rgba.Hex("#B83824"));
            if (n.Person != null)
            {
                Slice(g, UiArt.Inset, UiArt.InsetBorder, x + 5, y + 4, 23, 19);
                g.BlendFill(x + 6, y + 5, 21, 17, new Rgba(150, 90, 200, 140));
                Faces.Add(((x + 6) * 2, (y + 5) * 2, n.Person, n.Face, 1, 34));
            }
            else if (n.Icon != null) g.Draw(UiIcons.Of(n.Icon, n.IconColor), x + 5, y + 4);
            int ty = y + 4;
            if (n.Tag.Length > 0)
            {
                g.Small(x + left, ty, n.Tag, InkFaint);
                ty += 8;
            }
            foreach (string line in titleLines)
            {
                g.Text(x + left, ty, line, InkDark);
                ty += 9;
            }
            ty += 1;
            foreach (var (line, color) in lines)
            {
                g.Small(x + left, ty, line, color);
                ty += 7;
            }
            if (n.Share >= 0)
            {
                int bw = Math.Min(width, 130);
                g.Fill(x + left, ty + 2, bw, 6, Rgba.Hex("#4C3420"));
                g.Fill(x + left + 1, ty + 3, bw - 2, 4, Rgba.Hex("#DCC89A"));
                g.Fill(x + left + 1, ty + 3, (int)((bw - 2) * Math.Clamp(n.Share, 0, 1)), 4, n.Accent);
                g.Small(x + left + (bw - MockFont.SmallWidth(n.Bar)) / 2, ty + 2, n.Bar, Rgba.White, new Rgba(0, 0, 0, 200));
                ty += 9;
            }
            if (n.Sealed)
            {
                var seal = RecordArt.Seal(Rgba.Hex("#A8322A"));
                g.Draw(seal, x + w - seal.Width - 3, y + 3);
            }
            if (n.Buttons.Count > 0 && ty + 3 + button <= y + h)
            {
                int bx = x + w - 4;
                foreach (var (label, press) in Enumerable.Reverse(n.Buttons))
                {
                    int bw = Math.Max(button * 2, MockFont.Width(label) + 10);
                    bx -= bw;
                    Button(g, bx, ty + 3, bw, button, label, press);
                    bx -= 3;
                }
            }
            return h;
        }

        static List<string> Wrap(string text, int width, bool small = false)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string tried = line.Length == 0 ? word : line + " " + word;
                if ((small ? MockFont.SmallWidth(tried) : MockFont.Width(tried)) > width && line.Length > 0)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = tried;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        static List<object> GoalNotes() => new List<object>
        {
            new PNote
            {
                Tag = "GUIDE 7/31", Title = "NOW: SMELT 5 COPPER BARS (3/5)", Icon = MapIcons.Pin, IconColor = Rgba.Hex("#C08A26"), Accent = InkGold,
                Lines = { ("A FURNACE TURNS ORE INTO BARS", InkFaint), ("REWARD: IRON PICKAXE 1", InkGreen) }, Share = 0.6f, Bar = "3/5",
                Buttons = { ("SHOW DONE (6)", Press.Normal) },
            },
            new PNote
            {
                Tag = "MAIN STORY", Title = "CH 2 THE DROWNED BELL: FIND THE HARBOR", Person = "harbor_master", Accent = InkRed,
                Lines = { ("TO DO: GO TO MISTY HARBOR", InkDark), ("WAY: NORTH-EAST, MUSHROOM WOODS SOMEWHERE", InkFaint) },
                Buttons = { ("ON THE MAP", Press.Normal) },
            },
            new PNote
            {
                Tag = "SIDE STORY", Title = "HARBOR: 3 CAVE MINNOWS FOR IDA", Person = "fisher_ida", Accent = Rgba.Hex("#40729E"), Tracked = true,
                Lines = { ("WAY: MISTY HARBOR, EAST 34", InkFaint) }, Share = 0.33f, Bar = "1/3",
                Buttons = { ("ON THE MAP", Press.Normal), ("UNTRACK", Press.Primary) },
            },
            "TODAY'S REQUESTS",
            new PNote
            {
                Tag = "REQUEST", Title = "DORA: STONE 20 -> COIN 60", Person = "merchant", Face = Expression.Happy, Accent = Rgba.Hex("#80529E"), Share = 0.7f, Bar = "14/20",
                Buttons = { ("TRACK", Press.Normal) },
            },
        };

        static List<object> StoryNotes() => new List<object>
        {
            "MAIN STORY",
            "#§ CH 1 ASHES OF DAWN",
            ".· THE VILLAGE BURNS",
            ".· MINA'S LOST CAT",
            ".     CHOSE: \"NOTHING WAS THERE\"",
            ".· THE ROAD TO THE CITY",
            "#§ CH 2 THE DROWNED BELL",
            ".· AUDIENCE WITH THE QUEEN",
            new PNote { Tag = "NOW", Title = "FIND THE HARBOR", Person = "harbor_master", Lines = { ("GO TO MISTY HARBOR", InkDark) }, Buttons = { ("ON THE MAP", Press.Normal) } },
            "SIDE STORIES",
            new PNote { Tag = "IDA", Title = "MISTY HARBOR 1/4: THE FIRST CATCH", Person = "fisher_ida", Accent = Rgba.Hex("#40729E"), Share = 0.25f, Bar = "1/4", Buttons = { ("TRACK", Press.Normal) } },
            new PNote { Tag = "FOREMAN", Title = "DEEP MINE 4/4 - DONE", Person = "foreman", Accent = Rgba.Hex("#40729E"), Share = 1, Bar = "4/4", Sealed = true },
        };

        static List<object> DeedNotes() => new List<object>
        {
            ".DEEDS 3/14 - REWARDS ARE HANDED OUT AT ONCE",
            new PNote { Tag = "MONSTERS", Title = "SLAY 10 KINDS (8/10)", Icon = MapIcons.Skull, Accent = InkGold, Share = 0.8f, Bar = "8/10", Lines = { ("REWARD COIN 300 - ESSENCE 10", InkDark) } },
            new PNote { Tag = "FISH", Title = "CATCH 3 KINDS OF FISH (1/3)", Icon = MapIcons.Rumor, Accent = InkGold, Share = 0.33f, Bar = "1/3", Lines = { ("REWARD COIN 150 - BAIT 10", InkDark) } },
            new PNote { Tag = "BOSSES", Title = "[DONE] FELL A BOSS (1/1)", Icon = UiIcons.Check, IconColor = Rgba.Hex("#4C9A40"), Accent = InkGreen, Share = 1, Bar = "1/1", Sealed = true, Lines = { ("REWARD COIN 200 - RECEIVED", InkGreen) } },
            new PNote { Tag = "MONSTERS", Title = "[DONE] SLAY 5 KINDS (5/5)", Icon = UiIcons.Check, IconColor = Rgba.Hex("#4C9A40"), Accent = InkGreen, Share = 1, Bar = "5/5", Sealed = true, Lines = { ("REWARD COIN 100 - RECEIVED", InkGreen) } },
        };

        // The bestiary: the groups as small tabs, the felled count, the picked entry's card, the grid of found pictures and
        // dark silhouettes.
        static void Bestiary(PixelImage g, int x, int y, int w, int bottom, LootData loot, MonsterData monsters, bool phone)
        {
            string[] groups = { "MONSTERS 8/31", "BOSSES 1/9", "FISH 1/8", "CROPS 3/9", "GEAR 10/39" };
            int tab = phone ? 20 : 14;
            int perRow = Math.Max(1, (w + 2) / 52);
            int tabWidth = (w - 2 * (Math.Min(perRow, groups.Length) - 1)) / Math.Min(perRow, groups.Length);
            for (int i = 0; i < groups.Length; i++)
                Button(g, x + i % perRow * (tabWidth + 2), y + i / perRow * (tab + 2), tabWidth, tab, Clip(groups[i], tabWidth - 4), i == 0 ? Press.Primary : Press.Normal);
            y += (groups.Length + perRow - 1) / perRow * (tab + 2) + 2;
            g.Small(x + 2, y + 1, "MONSTERS FELLED 412", InkFaint);
            y += 9;
            var kinds = monsters?.Monsters.Where(m => m.Weight > 0).Take(30).ToList() ?? new List<MonsterDefinition>();
            // The picked one's card.
            var picked = kinds.Count > 2 ? kinds[2] : null;
            if (picked != null)
            {
                int h = 40;
                Card(g, x, y, w - 2, h);
                Slice(g, UiArt.Inset, UiArt.InsetBorder, x + 5, y + 5, 30, 30);
                var image = CreatureArt.Monster(picked.Id, Rgba.Hex(picked.Color), 0);
                g.Draw(image, x + 5 + (30 - image.Width) / 2, y + 5 + (30 - image.Height) / 2);
                g.Text(x + 40, y + 6, "SLIME (PICKED)", Rgba.Hex("#F2C76B"), Shadow);
                g.Small(x + 40, y + 17, $"LV {picked.Level} - LIFE {picked.Health} - HIT {picked.Damage}", Ink);
                g.Small(x + 40, y + 25, "LIVES IN: CAVE", Ink);
                g.Small(x + 40, y + 33, "FELLED 37", Ink);
                y += h + 4;
            }
            int cell = phone ? 26 : 23;
            int columns = Math.Max(1, (w + 3) / (cell + 3));
            for (int i = 0; i < kinds.Count; i++)
            {
                int cx = x + i % columns * (cell + 3), cy = y + i / columns * (cell + 3);
                if (cy + cell > bottom) break;
                bool known = i % 4 != 3 && i < 18;
                Slot(g, cx, cy, cell, i == 2 ? Rgba.Hex("#F2C76B") : known ? Rgba.Hex("#BF9E66") : (Rgba?)null, null, null, glow: i == 2);
                var image = CreatureArt.Monster(kinds[i].Id, Rgba.Hex(kinds[i].Color), 0);
                var shown = image.Width > cell - 6 || image.Height > cell - 6 ? Shrink(image, cell - 6) : image;
                if (!known) shown = Dark(shown);
                g.Draw(shown, cx + (cell - shown.Width) / 2, cy + (cell - shown.Height) / 2);
                if (!known) g.Text(cx + (cell - 5) / 2, cy + (cell - 7) / 2, "?", Rgba.Hex("#F2DA9A"), Shadow);
                else g.Small(cx + cell - 2 - MockFont.SmallWidth((i * 7 % 40 + 1).ToString()), cy + cell - 7, (i * 7 % 40 + 1).ToString(), Rgba.White, new Rgba(0, 0, 0, 230));
            }
        }

        static PixelImage Dark(PixelImage image)
        {
            var dark = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image[x, y].A > 0) dark[x, y] = new Rgba(20, 15, 12, 235);
            return dark;
        }

        static PixelImage Shrink(PixelImage image, int size)
        {
            float scale = Math.Min((float)size / image.Width, (float)size / image.Height);
            int w = Math.Max(1, (int)(image.Width * scale)), h = Math.Max(1, (int)(image.Height * scale));
            var small = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) small[x, y] = image[(int)(x / scale), (int)(y / scale)];
            return small;
        }

        // ---- The map ----

        static readonly Rgba MapWall = new Rgba(158, 140, 118);

        static Rgba MapFloor(Biome biome) => biome switch
        {
            Biome.Mushroom => new Rgba(92, 72, 104),
            Biome.Ice => new Rgba(84, 108, 128),
            Biome.Crystal => new Rgba(70, 70, 108),
            Biome.Lava => new Rgba(96, 64, 56),
            Biome.Abyss => new Rgba(48, 38, 72),
            Biome.Lake => new Rgba(40, 78, 84),
            Biome.Sand => new Rgba(118, 92, 56),
            _ => new Rgba(96, 80, 66),
        };

        static Rgba? TileOn(GeneratedWorld world, int x, int y)
        {
            var map = world.Map;
            if (!map.InBounds(x, y)) return null;
            var t = map[x, y];
            if (t == TileType.CopperOre) return Rgba.Hex("#D9824A");
            if (t == TileType.IronOre) return Rgba.Hex("#B8B0A8");
            if (t == TileType.Water) return new Rgba(50, 100, 170);
            if (t == TileType.Lava) return new Rgba(255, 110, 30);
            if (t == TileType.Road) return new Rgba(176, 150, 112);
            if (TileMap.IsPaving(t)) return new Rgba(150, 138, 120);
            if (t == TileType.Waypoint) return new Rgba(110, 170, 255);
            // The full map's own colours (MapView.SheetColor): walls dark ink, floors pale with their biome's tint.
            if (map.IsSolid(x, y)) return Rgba.Hex("#483626");
            return MapFloor(world.Biomes[x, y]).Mix(Rgba.Hex("#ECDEBA"), 0.62f);
        }

        // Explored: near the spawn and along the way to the nearest place, as a player's first hour leaves it.
        static bool Explored(GeneratedWorld world, int x, int y)
        {
            float dx = x - world.SpawnX, dy = y - world.SpawnY;
            if (dx * dx + dy * dy < 46 * 46 + PixelNoise.Value(x / 6, y / 6, 3) * 900) return true;
            var place = world.Places.OrderBy(p => Math.Abs(p.X - world.SpawnX) + Math.Abs(p.Y - world.SpawnY)).FirstOrDefault(p => p.X != world.SpawnX);
            if (place == null) return false;
            float px = place.X - world.SpawnX, py = place.Y - world.SpawnY;
            float length = (float)Math.Sqrt(px * px + py * py);
            float t = Math.Clamp((dx * px + dy * py) / (length * length), 0, 1);
            float ox = dx - px * t, oy = dy - py * t;
            return ox * ox + oy * oy < 14 * 14;
        }

        static readonly string[] PlaceNames = { "DAWN VILLAGE", "ROYAL CITY", "MISTY HARBOR", "FROST HOLLOW", "DEEP MINE", "TEMPLE", "DEMON CAMP" };

        static void MapScreen(PixelImage g, (int X, int Y, int W, int H) a, GeneratedWorld world, bool phone, bool low, bool card, float zoom)
        {
            int top = Window(g, a.X, a.Y, a.W, a.H, UiKind.Journal, phone ? "MAP" : "MAP (M)", UiIcons.Map, default);
            var inside = Inside(a.X, a.Y, a.W, a.H, top);
            GroupTabs(g, inside.X, top, inside.W, new[] { ("JOURNAL", UiIcons.Book), ("MAP", UiIcons.Map) }, 1);
            int bodyTop = top + (low ? 17 : 20) + (low ? 2 : 4);
            int bx = inside.X, bw = inside.W, bottom = inside.Bottom;
            (string Name, string[] Icon, Rgba Color)[] legend =
            {
                ("PLACES", MapIcons.Place, Rgba.Hex("#EAE0C6")), ("WAYPOINTS", MapIcons.Waypoint, Rgba.Hex("#80C6FF")), ("RAILS", MapIcons.Station, Rgba.Hex("#B8C6E0")),
                ("GATES", MapIcons.Gate, Rgba.Hex("#B88CFF")), ("SIGHTS", MapIcons.Chest, Rgba.Hex("#FFD64C")), ("BOSSES", MapIcons.Arena, Rgba.Hex("#FF594C")),
                ("RUMOURS", MapIcons.Rumor, Rgba.Hex("#FFD64C")), ("ORE", MapIcons.Ore, Rgba.Hex("#D98C4C")),
            };
            int chip = phone ? 20 : 15;
            int vx, vy, vw, vh;
            int lx, ly, lw, columns;
            bool side = bw >= 280;
            if (side)
            {
                lw = Math.Clamp((int)Math.Round(bw * 0.22), 90, 110);
                columns = 2;
                lx = bx + bw - lw;
                ly = bodyTop;
                vx = bx;
                vy = bodyTop;
                vw = lx - 4 - bx;
                vh = bottom - bodyTop;
            }
            else
            {
                columns = Math.Clamp((bw + 2) / 44, 1, legend.Length);
                int lines = (legend.Length + columns - 1) / columns;
                lw = bw;
                lx = bx;
                ly = bottom - lines * (chip + 2);
                vx = bx;
                vy = bodyTop;
                vw = bw;
                vh = ly - 3 - bodyTop;
            }
            // The sheet: the margin darker, the world's sheet lighter, the explored tiles over it.
            Parch(g, vx, vy, vw, vh, 0.4f);
            float fit = Math.Min(vw * 2f / world.Map.Width, vh * 2f / world.Map.Height);
            float pointsPerTile = Math.Max(fit, Math.Min(vw, vh) * 2f / 180) * zoom;
            float perPixel = 2 / pointsPerTile;
            float cx = world.SpawnX + 12, cy = world.SpawnY + 8;
            int TileX(int px) => (int)Math.Floor(cx + (px - vx - vw / 2f) * perPixel);
            int TileY(int py) => (int)Math.Floor(cy - (py - vy - vh / 2f) * perPixel);
            int ToPx(float tx) => (int)Math.Round(vx + vw / 2f + (tx - cx) / perPixel);
            int ToPy(float ty) => (int)Math.Round(vy + vh / 2f - (ty - cy) / perPixel);
            var hatch = RecordArt.Hatch();
            for (int py = vy; py < vy + vh; py++)
            {
                for (int px = vx; px < vx + vw; px++)
                {
                    int tx = TileX(px), ty = TileY(py);
                    if (!world.Map.InBounds(tx, ty)) continue;
                    var paper = _parchmentTile[(px - vx) % RecordArt.PageTile, (py - vy) % RecordArt.PageTile];
                    g[px, py] = new Rgba((int)(paper.R * 0.68f), (int)(paper.G * 0.63f), (int)(paper.B * 0.54f));
                    g.Blend(px, py, hatch[(px - vx) % RecordArt.HatchTile, (py - vy) % RecordArt.HatchTile]);
                    if (Explored(world, tx, ty) && TileOn(world, tx, ty) is Rgba c) g[px, py] = c;
                }
            }
            // A rumour's faint ring in the unexplored, a question mark at its middle.
            int rx = ToPx(world.SpawnX - 70), ry = ToPy(world.SpawnY + 40), rr = Math.Max(6, (int)(40 / perPixel));
            for (int j = -rr; j <= rr; j++)
                for (int i = -rr; i <= rr; i++)
                {
                    float d = (float)Math.Sqrt(i * i + j * j);
                    if (d > rr || rx + i < vx || rx + i >= vx + vw || ry + j < vy || ry + j >= vy + vh) continue;
                    g.Blend(rx + i, ry + j, d > rr - 1.2f ? new Rgba(150, 110, 40, 180) : new Rgba(255, 214, 76, 40));
                }
            void Mark(float tx, float ty, string[] icon, Rgba color, string label = null)
            {
                int px = ToPx(tx), py = ToPy(ty);
                if (px < vx + 4 || py < vy + 4 || px > vx + vw - 4 || py > vy + vh - 4) return;
                var image = UiIcons.Of(icon, color);
                g.Draw(image, px - image.Width / 2, py - image.Height / 2);
                if (label == null) return;
                int width = MockFont.Width(label);
                int lx2 = px + 6 + width > vx + vw - 2 ? px - 6 - width : px + 6;
                foreach (var (ox, oy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) }) g.Text(lx2 + ox, py - 3 + oy, label, Rgba.Hex("#1A0F08"));
                g.Text(lx2, py - 3, label, Rgba.Hex("#FFF4DC"));
            }
            Mark(rx, ry, MapIcons.Rumor, Rgba.Hex("#FFD64C"));
            int named = 0;
            foreach (var place in world.Places.OrderBy(p => Math.Abs(p.X - world.SpawnX) + Math.Abs(p.Y - world.SpawnY)).Take(3))
            {
                bool open = named == 0;
                Mark(place.X, place.Y, open ? MapIcons.Waypoint : MapIcons.Place, open ? Rgba.Hex("#80C6FF") : Rgba.Hex("#EAE0C6"), PlaceNames[named++ % PlaceNames.Length]);
            }
            var pois = world.Rooms?.Pois.Where(p => Explored(world, p.X, p.Y)).Take(14).ToList() ?? new List<Poi>();
            foreach (var poi in pois)
            {
                var (icon, color) = poi.Kind switch
                {
                    PoiKind.Camp => (MapIcons.Camp, Rgba.Hex("#FF9A40")),
                    PoiKind.Shrine => (MapIcons.Shrine, Rgba.Hex("#F2F2FF")),
                    PoiKind.Chest => (MapIcons.Chest, Rgba.Hex("#FFD64C")),
                    PoiKind.Station => (MapIcons.Station, Rgba.Hex("#C6D6F0")),
                    PoiKind.Waystone => (MapIcons.Stone, Rgba.Hex("#80C6FF")),
                    _ => (MapIcons.Cart, Rgba.Hex("#C68C52")),
                };
                Mark(poi.X, poi.Y, icon, color);
            }
            Mark(world.SpawnX + 30, world.SpawnY - 22, MapIcons.Gate, Rgba.Hex("#B88CFF"));
            Mark(world.SpawnX - 18, world.SpawnY - 30, MapIcons.Skull, Rgba.Hex("#FF4C40"));
            Mark(world.SpawnX + 8, world.SpawnY + 14, MapIcons.Ore, Rgba.Hex("#D9824A"));
            // The pin being guided to, the other player and the player's arrow (here pointing up).
            Mark(world.SpawnX + 40, world.SpawnY + 30, MapIcons.Pin, Rgba.Hex("#D8402E"));
            Mark(world.SpawnX - 6, world.SpawnY - 4, MapIcons.Arrow, Rgba.Hex("#73B3FF"));
            Mark(world.SpawnX, world.SpawnY, MapIcons.Arrow, Rgba.Hex("#FFF8E6"));
            PageEdge(g, vx - 1, vy - 1, vw + 2, vh + 2);
            var compass = RecordArt.Compass();
            g.Draw(compass, vx + 5, vy + vh - compass.Height - 5);
            // The zoom buttons in the top right corner.
            int button = phone ? 20 : 14;
            int zx = vx + vw - 4 - 3 * button - 4;
            foreach (var (label, i) in new[] { ("-", 0), ("+", 1), ("O", 2) }) Button(g, zx + i * (button + 2), vy + 4, button, button, label);
            if (card)
            {
                // MapView.CardRect: over the map's bottom left corner; on a low view wider, its buttons beside its words.
                bool beside = vh < 140 && vw >= 170;
                int cw = Math.Min(vw - 8, beside ? 230 : 160);
                int bh = phone ? 20 : 14;
                int ch = beside ? Math.Max(5 + 18 + 5, bh + 10) : 18 + 8 + bh + 7;
                int cardY = vy + vh - 4 - ch;
                Card(g, vx + 4, cardY, cw, ch);
                g.Text(vx + 9, cardY + 5, "DAWN VILLAGE", Rgba.Hex("#F2C76B"), Shadow);
                g.Small(vx + 9, cardY + 15, "WAYPOINT OPEN - NORTH-EAST 42", Ink);
                int by = beside ? cardY + (ch - bh) / 2 : cardY + 25;
                Button(g, vx + 4 + cw - 5 - 36, by, 36, bh, "GO", Press.Primary);
                Button(g, vx + 4 + cw - 5 - 36 - 3 - 64, by, 64, bh, "GUIDE ME");
                g.Box(ToPx(world.Places.OrderBy(p => Math.Abs(p.X - world.SpawnX) + Math.Abs(p.Y - world.SpawnY)).First().X) - 6, ToPy(world.Places.OrderBy(p => Math.Abs(p.X - world.SpawnX) + Math.Abs(p.Y - world.SpawnY)).First().Y) - 6, 13, 13, Rgba.Hex("#FFF2B0"));
            }
            // The legend's toggles: beside the map on its own page, or in rows under it.
            int top2 = ly;
            if (side)
            {
                int lh = Math.Min(vh, Math.Max((legend.Length + 1) / 2 * (chip + 2) + 13 + 35, 70));
                Parch(g, lx, ly, lw, lh);
                PageEdge(g, lx, ly, lw, lh);
                g.Text(lx + 5, ly + 4, "LEGEND", InkRed);
                top2 = ly + 13;
            }
            int left = lx + (side ? 4 : 0);
            int width = (lw - (side ? 8 : 0) - 2 * (columns - 1)) / columns;
            for (int i = 0; i < legend.Length; i++)
            {
                int x = left + i % columns * (width + 2), y = top2 + i / columns * (chip + 2);
                bool on = i != 7;
                Button(g, x, y, width, chip, "", Press.Normal);
                var icon = UiIcons.Of(legend[i].Icon, on ? legend[i].Color : legend[i].Color.Scale(0.5f));
                bool room = width >= icon.Width + 25;
                int ix = room ? x + 3 : x + (width - icon.Width) / 2;
                g.Draw(icon, ix, y + (chip - icon.Height) / 2);
                if (room) g.Small(ix + icon.Width + 2, y + (chip - 5) / 2, Clip(legend[i].Name, x + width - ix - icon.Width - 4, small: true), on ? Ink : Rgba.Hex("#7A6C5A"));
                if (!on) g.Fill(x + 2, y + chip / 2, width - 4, 1, Rgba.Hex("#E64C40"));
            }
            if (side)
            {
                int hy = top2 + (legend.Length + 1) / 2 * (chip + 2) + 3;
                if (hy + 20 < ly + vh) foreach (var (line, k) in new[] { ("DRAG TO MOVE, WHEEL", 0), ("TO ZOOM. PRESS A MARK", 1), ("FOR MORE.", 2) }) g.Small(lx + 5, hy + k * 7, line, InkFaint);
            }
        }
    }
}
