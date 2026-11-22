using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The growth screens of U4 (Docs/UiRework.md 4-3, U4) laid out as the game lays them out (InventoryWindow.Growth.cs,
    // .Skills.cs, .Passives.cs, .Character.cs), at their size in points with the frames at the art's scale (one art
    // pixel two points) and doubled, as the encounter windows' preview. The passive tree is the real sheet's, its points
    // drawn with GrowthArt at the zoom the game opens with; skills, icons and heroes are the game's art. The writing is the
    // preview's Latin font.
    //  - growth.png: a PC 1280 x 720: the skills (two classes, cards, the picked skill's panel with its crystals), the
    //    passive tree on its night with its panel, the character's sheet, and the classes to pick (a new character).
    //  - growth_phone.png: a phone held upright (390 x 844): the skills' list, the picked skill's sheet, the tree with its
    //    card and drawer, the character's sheet; a phone held sideways (740 x 360): the skills and the tree.
    static partial class UiPreview
    {
        static readonly Rgba Gold = Rgba.Hex("#F2C76B");
        static readonly Rgba Green = Rgba.Hex("#8CE680");

        // A made-up character for the pictures: warrior and fire mage at level 12, a few skills learned and raised, some
        // of the tree learned toward the warrior's side.
        sealed class Hero
        {
            public ClassData Data;
            public List<ClassDefinition> Classes;
            public Dictionary<string, int> Ranks;
            public List<string> Slots;
            public HashSet<PassiveNode> Taken;
            public int Level = 12;
            public int SkillPoints = 2;
            public int PassivePoints => Level - 1 - Taken.Count;

            public int Rank(SkillDefinition skill) => Ranks.TryGetValue(skill.Id, out int rank) ? rank : 0;

            public bool CanLearn(PassiveNode node) => !node.IsStart && PassivePoints > 0 && !Taken.Contains(node) && node.Links.Any(n => n.IsStart || Taken.Contains(n));

            public bool IsTaken(PassiveNode node) => node.IsStart || Taken.Contains(node);
        }

        static Hero MakeHero(ClassData data, bool fresh = false)
        {
            var hero = new Hero
            {
                Data = data,
                Classes = fresh ? new List<ClassDefinition>() : new List<ClassDefinition> { data.Find("warrior"), data.Find("fire_mage") },
                Ranks = fresh ? new Dictionary<string, int>() : new Dictionary<string, int> { ["whirlwind"] = 3, ["war_cry"] = 1, ["charge"] = 2, ["fireball"] = 2, ["flame_brand"] = 1 },
                Slots = fresh ? new List<string>() : new List<string> { "whirlwind", "fireball", "charge", "war_cry" },
                Taken = new HashSet<PassiveNode>(),
            };
            if (fresh)
            {
                hero.Level = 1;
                hero.SkillPoints = 0;
            }
            foreach (string id in fresh ? Array.Empty<string>() : new[] { "str1", "str2", "str3", "str5", "dex1", "int1" })
            {
                var node = data.FindPassive(id);
                if (node != null) hero.Taken.Add(node);
            }
            return hero;
        }

        // Pictures to put over the doubled picture at their own whole scale (in points): a skill's icon three times, a
        // hero twice or four times.
        static readonly List<(int X, int Y, PixelImage Image, int Scale)> Pictures = new List<(int, int, PixelImage, int)>();

        static PixelImage Growth(LootData loot, ClassData data)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1300, 760);
            Floor(g, 0, 0, g.Width, g.Height, 13);
            var hero = MakeHero(data);
            g.Small(10, 2, "U4 GROWTH SCREENS ON A PC 1280X720, ONE PIXEL A POINT: SKILLS (K), TREE (P), CHARACTER (U), CLASS PICK", Dim);
            // Each full screen as on a 1280 x 720 screen (the window 1180 x 700 in the middle).
            PcScreen(g, 5, 10, s => SkillsScreen(g, s, hero, phone: false));
            PcScreen(g, 655, 10, s => TreeScreen(g, s, hero, phone: false));
            PcScreen(g, 5, 385, s => SheetScreen(g, s, hero, loot, phone: false));
            PcScreen(g, 655, 385, s => SkillsScreen(g, s, MakeHero(data, fresh: true), phone: false));
            return Finish(g);
        }

        static PixelImage GrowthPhone(LootData loot, ClassData data)
        {
            Faces.Clear();
            Pictures.Clear();
            var g = Canvas(1210, 640);
            Floor(g, 0, 0, g.Width, g.Height, 17);
            var hero = MakeHero(data);
            g.Small(6, 2, "PHONE 390X844: SKILL LIST, A SKILL'S SHEET, THE TREE WITH ITS CARD AND DRAWER, THE CHARACTER. 740X360: SKILLS, TREE", Dim);
            for (int i = 0; i < 4; i++)
            {
                int px = 6 + i * 201;
                Phone(g, px, 12, 195, 422);
                var area = (px + 2, 14, 191, 418);
                switch (i)
                {
                    case 0:
                        SkillsScreen(g, area, hero, phone: true);
                        break;
                    case 1:
                        SkillsScreen(g, area, hero, phone: true, sheet: true);
                        break;
                    case 2:
                        TreeScreen(g, area, hero, phone: true);
                        break;
                    default:
                        SheetScreen(g, area, hero, loot, phone: true);
                        break;
                }
            }
            Phone(g, 812, 12, 370, 180);
            SkillsScreen(g, (814, 14, 366, 176), hero, phone: true, low: true);
            Phone(g, 812, 200, 370, 180);
            TreeScreen(g, (814, 202, 366, 176), hero, phone: true, low: true);
            Phone(g, 812, 400, 370, 180);
            SheetScreen(g, (814, 402, 366, 176), hero, loot, phone: true, low: true);
            return Finish(g);
        }

        // A PC's screen (640 x 360 pixels, 1280 x 720 points): the world dark behind, the growth window in the middle.
        static void PcScreen(PixelImage g, int x, int y, Action<(int X, int Y, int W, int H)> draw)
        {
            Phone(g, x, y, 640, 360);
            g.BlendFill(x, y, 640, 360, new Rgba(0, 0, 0, 178));
            draw((x + 25, y + 5, 590, 350));
        }

        // The window with its night head, coins and close button, and the growth tabs; gives the body under the tabs.
        static (int X, int Y, int W, int Bottom) GrowthWindow(PixelImage g, (int X, int Y, int W, int H) a, int chosen, string title, string[] icon, bool low, bool stars = false)
        {
            int top = Window(g, a.X, a.Y, a.W, a.H, UiKind.Growth, title, icon, default);
            g.Draw(UiIcons.Of(UiIcons.Coin, Rgba.Hex("#F2C040")), a.X + a.W - 50, a.Y + 9);
            g.Text(a.X + a.W - 41, a.Y + 9, "1250", Rgba.Hex("#FFF2CC"), Shadow);
            if (stars)
            {
                var night = UiArt.StarField();
                int nx = a.X + UiArt.PanelBorder, ny = a.Y + UiArt.PanelBorder + UiArt.HeadHeight;
                for (int yy = ny; yy < a.Y + a.H - UiArt.PanelBorder; yy++)
                    for (int xx = nx; xx < a.X + a.W - UiArt.PanelBorder; xx++) g[xx, yy] = night[(xx - nx) % UiArt.StarTile, (yy - ny) % UiArt.StarTile];
            }
            var (ix, iy, iw, bottom) = Inside(a.X, a.Y, a.W, a.H, top);
            GroupTabs(g, ix, iy, iw, new[] { ("SKILLS", UiIcons.Skills), ("TREE", UiIcons.Tree), ("HERO", UiIcons.Person) }, chosen, badge: 1);
            return (ix, iy + 20 + (low ? 2 : 4), iw, low ? a.Y + a.H - 5 : bottom);
        }

        // ---- Skills ----

        static void SkillsScreen(PixelImage g, (int X, int Y, int W, int H) a, Hero hero, bool phone, bool sheet = false, bool low = false)
        {
            var (x, y, w, bottom) = GrowthWindow(g, a, 0, phone ? "SKILLS" : "SKILLS (K)", UiIcons.Skills, low);
            int button = phone ? 20 : 14;
            int slot = low ? 20 : 24;
            int cell = slot + 2 + 30;
            bool wide = w >= 4 * (cell + 4) + 125;
            int barBottom;
            var skills = hero.Slots.Select(id => hero.Data.Skills.First(s => s.Id == id)).ToList();
            var picked = hero.Classes.Count > 0 ? hero.Data.Skills.First(s => s.Id == "whirlwind") : null;
            // The slot bar: icon, key, rank, [SWAP] beside (under on a phone held upright).
            string[] keys = phone ? new[] { "1", "2", "3", "4" } : new[] { "Q", "E", "R", "F" };
            int pointsX, pointsY, pointsW, pointsH;
            if (wide)
            {
                int height = Math.Max(slot, button);
                for (int i = 0; i < 4; i++)
                {
                    int sx = x + i * (cell + 4);
                    SlotCell(g, sx, y + (height - slot) / 2, slot, i < skills.Count ? skills[i] : null, keys[i], hero, skills.Count > i && skills[i] == picked);
                    if (i < skills.Count) Button(g, sx + slot + 2, y + (height - button) / 2, 30, button, "SWAP");
                }
                pointsX = x + 4 * (cell + 4) + 2;
                pointsY = y;
                pointsW = x + w - pointsX;
                pointsH = height;
                barBottom = y + height;
            }
            else
            {
                int cw = (w - 12) / 4;
                for (int i = 0; i < 4; i++)
                {
                    int sx = x + i * (cw + 4);
                    SlotCell(g, sx + (cw - slot) / 2, y, slot, i < skills.Count ? skills[i] : null, keys[i], hero, skills.Count > i && skills[i] == picked);
                    int bw = Math.Min(cw, 38);
                    if (i < skills.Count) Button(g, sx + (cw - bw) / 2, y + slot + 2, bw, button, "SWAP");
                }
                pointsX = x;
                pointsY = y + slot + 2 + button + 3;
                pointsW = w;
                pointsH = button;
                barBottom = pointsY + pointsH;
            }
            // The points in large gold and [RESET (20)] in red.
            if (hero.Classes.Count > 0)
            {
                int rw = phone ? 78 : 73;
                Button(g, pointsX + pointsW - rw, pointsY + (pointsH - button) / 2, rw, button, "RESET (50)", Press.Danger);
                g.Draw(UiIcons.Of(GrowthArt.Point, Gold), pointsX, pointsY + pointsH / 2 - 4);
                int nw = g.Text(pointsX + 12, pointsY + pointsH / 2 - 7, hero.SkillPoints.ToString(), Rgba.Hex("#FFD659"), Shadow, 2);
                int room = pointsW - rw - 26 - nw;
                if (room < 30) { }
                else if (pointsH >= 22)
                {
                    g.Text(pointsX + 18 + nw, pointsY + pointsH / 2 - 8, "SKILL POINTS", Ink, Shadow);
                    g.Small(pointsX + 18 + nw, pointsY + pointsH / 2 + 2, "LV 12 - ONE A LEVEL", Dim);
                }
                else g.Small(pointsX + 18 + nw, pointsY + pointsH / 2 - 2, Clip("SKILL POINTS - ONE A LEVEL", room, small: true), Ink);
            }
            int restY = barBottom + (low ? 3 : 5);
            bool side = w >= 320;
            int listX = x, listW = w, detailX = x, detailW = w;
            if (side && hero.Classes.Count > 0)
            {
                detailW = Math.Clamp((int)Math.Round(w * 0.34), 135, 190);
                detailX = x + w - detailW;
                listW = w - detailW - 5;
            }
            if (!side && sheet)
            {
                Clipped(g, detailX, restY, detailW, bottom - restY, () => SkillDetail(g, detailX, restY, detailW, bottom - restY, picked, hero, true, phone));
                return;
            }
            // The list: the classes to pick, or each class's band and skills as cards (rows on a phone or a low screen).
            int ly = restY;
            if (hero.Classes.Count == 0)
            {
                g.Bars(listX + 2, ly + 2, listW - 20, 2, Ink.WithAlpha(150), 4, 6);
                ly += 16;
                foreach (var option in hero.Data.Classes)
                {
                    if (ly + 46 > bottom) break;
                    ClassCard(g, listX, ly, listW - 10, 46, option, phone, option.Id == "fire_mage");
                    ly += 49;
                }
            }
            else
            {
                g.Text(listX + 2, ly, "CLASSES 2/3 - ONE MORE AT LV 20", Ink, Shadow);
                ly += 11;
                int inner = listW - 11;
                bool cards = side && !low && inner >= 3 * 59;
                int perRow = !cards ? 1 : inner >= 6 * 59 ? 6 : 3;
                foreach (var owner in hero.Classes)
                {
                    if (ly + 12 > bottom) break;
                    var color = GrowthArt.ClassColor(owner);
                    g.Fill(listX, ly + 1, 2, 9, color);
                    int tw = g.Text(listX + 5, ly + 2, owner.Id == "warrior" ? "WARRIOR SKILLS" : "FIRE MAGE SKILLS", color, Shadow);
                    g.Small(listX + 12 + tw, ly + 3, owner.Id == "warrior" ? "PASSIVE: +20% LIFE, +10% MELEE" : "PASSIVE: +15% FIRE", Green);
                    g.BlendFill(listX + 2, ly + 11, listW - 14, 1, color.WithAlpha(90));
                    ly += 14;
                    if (cards)
                    {
                        int cw = (inner - 4 * (perRow - 1)) / perRow;
                        for (int i = 0; i < owner.Skills.Count; i++)
                        {
                            int cx = listX + i % perRow * (cw + 4), cy = ly + i / perRow * 54;
                            if (cy + 50 > bottom) continue;
                            SkillCardArt(g, cx, cy, cw, 50, owner.Skills[i], hero, true, phone, owner.Skills[i] == picked);
                        }
                        ly += (owner.Skills.Count + perRow - 1) / perRow * 54;
                    }
                    else
                    {
                        int rh = phone ? 27 : 23;
                        foreach (var skill in owner.Skills)
                        {
                            if (ly + rh > bottom) break;
                            SkillCardArt(g, listX, ly, inner, rh, skill, hero, false, phone, skill == picked);
                            ly += rh + 2;
                        }
                    }
                    ly += 3;
                }
            }
            // The scroll bar of the list.
            g.BlendFill(listX + listW - 6, restY, 3, bottom - restY, new Rgba(0, 0, 0, 120));
            g.BlendFill(listX + listW - 6, restY, 3, (bottom - restY) / 2, Rgba.Hex("#6B5230"));
            if (side && hero.Classes.Count > 0) Clipped(g, detailX, restY, detailW, bottom - restY, () => SkillDetail(g, detailX, restY, detailW, bottom - restY, picked, hero, false, phone));
        }

        static void SlotCell(PixelImage g, int x, int y, int size, SkillDefinition skill, string key, Hero hero, bool picked)
        {
            Slot(g, x, y, size, skill != null ? Tint(skill.Tags) : (Rgba?)null, skill != null ? SkillArt.Icon(skill) : null, picked: picked);
            if (skill == null) g.Small(x + (size - MockFont.SmallWidth("EMPTY")) / 2, y + size / 2 - 2, "EMPTY", Dim);
            g.Text(x + 2, y + size - 8, key, Ink, Shadow);
            if (skill != null && hero.Rank(skill) > 1) g.Small(x + size - 2 - MockFont.SmallWidth(Roman(hero.Rank(skill))), y + 2, Roman(hero.Rank(skill)), Gold, new Rgba(0, 0, 0, 220));
        }

        static string Roman(int rank) => rank switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "I" };

        static Rgba Tint(SkillTag tags)
        {
            if ((tags & SkillTag.Fire) != 0) return Rgba.Hex("#FF8026");
            if ((tags & SkillTag.Cold) != 0) return Rgba.Hex("#8CD9FF");
            if ((tags & SkillTag.Lightning) != 0) return Rgba.Hex("#FFF266");
            if ((tags & SkillTag.Summon) != 0) return Rgba.Hex("#73E6CC");
            if ((tags & SkillTag.Magic) != 0) return Rgba.Hex("#BF8CFF");
            if ((tags & SkillTag.Melee) != 0) return Rgba.Hex("#E6E6F2");
            return Rgba.Hex("#FFCC59");
        }

        static void Pips(PixelImage g, int x, int y, int rank, int size = 3)
        {
            for (int i = 0; i < 5; i++)
            {
                g.Fill(x + i * (size + 1), y, size, size, Rgba.Hex("#0D0A08"));
                g.Fill(x + i * (size + 1), y, size - 1 > 0 ? size : 1, size - 1 > 0 ? size - 1 : 1, i < rank ? Rgba.Hex("#FFD159") : Rgba.Hex("#403833"));
            }
        }

        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["whirlwind"] = "WHIRLWIND", ["war_cry"] = "WAR CRY", ["charge"] = "CHARGE", ["ground_slam"] = "GROUND SLAM", ["iron_skin"] = "IRON SKIN",
            ["blade_storm"] = "BLADE STORM", ["fireball"] = "FIREBALL", ["flame_brand"] = "FLAME BRAND", ["fire_ring"] = "FIRE RING", ["meteor"] = "METEOR",
            ["flame_burst"] = "FLAME BURST", ["inferno"] = "INFERNO",
        };

        static string NameOf(SkillDefinition skill) => English.TryGetValue(skill.Id, out var name) ? name : skill.Id.ToUpperInvariant().Replace('_', ' ');

        // A skill's card (big icon, name, pips, lock) or its row on a phone, with [LEARN]/[RAISE] and [CRYSTAL].
        static void SkillCardArt(PixelImage g, int x, int y, int w, int h, SkillDefinition skill, Hero hero, bool card, bool phone, bool picked)
        {
            int rank = hero.Rank(skill);
            bool open = hero.Level >= skill.Level;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            var tag = Tint(skill.Tags);
            if (card) g.BlendFill(x + 1, y + 1, w - 2, 2, tag.WithAlpha(rank > 0 ? 230 : 90));
            else g.BlendFill(x + 1, y + 1, 2, h - 2, tag.WithAlpha(rank > 0 ? 230 : 90));
            if (picked) g.Box(x - 1, y - 1, w + 2, h + 2, Rgba.Hex("#FFD966"));
            int button = phone ? 20 : 14;
            var icon = SkillArt.Icon(skill);
            if (rank == 0)
            {
                var faded = new PixelImage(icon.Width, icon.Height);
                for (int j = 0; j < icon.Height; j++)
                    for (int i = 0; i < icon.Width; i++) faded[i, j] = icon[i, j].WithAlpha(icon[i, j].A * (open ? 190 : 100) / 255);
                icon = faded;
            }
            var name = NameOf(skill);
            var nameInk = rank > 0 ? (picked ? Rgba.Hex("#FFE080") : Ink) : open ? Rgba.Hex("#D8CCB8") : Dim;
            string state = rank > 0 ? $"RANK {rank}" : open ? "NOT LEARNED" : $"FROM LV {skill.Level}";
            var stateInk = rank > 0 ? Green : open ? Dim : Bad;
            string learn = rank >= 5 ? "MAX" : rank > 0 ? "RAISE" : "LEARN";
            bool can = open && rank < 5 && hero.SkillPoints > 0;
            if (card)
            {
                g.Text(x + 4, y + 4, Clip(name, w - 8), nameInk, Shadow);
                Slot(g, x + 3, y + 12, 20, rank > 0 ? tag : (Rgba?)null, icon);
                if (!open) g.Draw(UiIcons.Of(UiIcons.Lock, Rgba.Hex("#D8CCB3")), x + 15, y + 23);
                Pips(g, x + 26, y + 14, rank);
                g.Small(x + 26, y + 20, Clip(state, (w - 28), small: true), stateInk);
                if (rank > 0) g.Small(x + 26, y + 27, Clip($"GEMS {(rank >= 3 ? 1 : 0)}/{(rank >= 5 ? 3 : rank >= 3 ? 2 : 1)}", (w - 28), small: true), Dim);
                int lw = (w - 6 - 2) * 56 / 100;
                Button(g, x + 3, y + h - button - 3, lw, button, learn, !can ? Press.Off : rank == 0 ? Press.Primary : Press.Normal);
                Button(g, x + 5 + lw, y + h - button - 3, w - 8 - lw, button, "MOD", rank > 0 ? Press.Normal : Press.Off);
            }
            else
            {
                Slot(g, x + 2, y + (h - 20) / 2, 20, rank > 0 ? tag : (Rgba?)null, icon);
                if (!open) g.Draw(UiIcons.Of(UiIcons.Lock, Rgba.Hex("#D8CCB3")), x + 14, y + (h - 20) / 2 + 11);
                int sw = phone ? 28 : 25, lw = phone ? 34 : 30;
                int sx = x + w - 2 - sw, lx = sx - 2 - lw;
                g.Text(x + 26, y + 3, Clip(name, lx - x - 28), nameInk, Shadow);
                Pips(g, x + 26, y + h - 8, rank);
                g.Small(x + 48, y + h - 9, Clip(state, (lx - x - 50), small: true), stateInk);
                Button(g, lx, y + (h - button) / 2, lw, button, learn, !can ? Press.Off : rank == 0 ? Press.Primary : Press.Normal);
                Button(g, sx, y + (h - button) / 2, sw, button, "MOD", rank > 0 ? Press.Normal : Press.Off);
            }
        }

        // The picked skill's panel (or a phone's sheet): the icon three times its size, name, class and tags, pips; what
        // it does now and at the next rank side by side; [RAISE] [TO SLOT]; its crystal slots and the crystals to put in.
        static void SkillDetail(PixelImage g, int x, int y, int w, int h, SkillDefinition skill, Hero hero, bool sheet, bool phone)
        {
            if (sheet) Card(g, x, y, w, h);
            else Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            int button = phone ? 20 : 14;
            int top = y + 3;
            if (sheet)
            {
                Button(g, x + 4, top, 52, button, "< LIST");
                top += button + 3;
            }
            if (skill == null)
            {
                g.Small(x + 5, top + 4, "PRESS A SKILL TO SEE IT HERE", Dim);
                return;
            }
            int rank = hero.Rank(skill);
            var tag = Tint(skill.Tags);
            Slot(g, x + 5, top + 1, 28, tag, SkillArt.Icon(skill).Times(1));
            Pictures.Add(((x + 5) * 2 + 4, (top + 1) * 2 + 4, SkillArt.Icon(skill), 3));
            int tx = x + 38;
            g.Text(tx, top, NameOf(skill), Rgba.Hex("#FFD966"), Shadow);
            g.Small(tx, top + 10, "WARRIOR - MELEE", tag);
            Pips(g, tx, top + 19, rank, 4);
            g.Small(tx + 28, top + 19, $"RANK {rank}", Green);
            int by = top + 33;
            g.Bars(x + 5, by, w - 14, 2, Ink.WithAlpha(140), 11, 6);
            by += 14;
            int half = (w - 14) / 2;
            g.Text(x + 5, by, $"NOW ({rank})", Gold, Shadow);
            g.Text(x + 9 + half, by, $"NEXT ({rank + 1})", Gold, Shadow);
            by += 10;
            var now = new[] { "DAMAGE 31 (150%)", "AREA 1.8", "MANA 12", "COOLDOWN 3.6S" };
            var next = new[] { "DAMAGE 35 (172%)", "AREA 1.8", "MANA 12", "COOLDOWN 3.4S" };
            for (int i = 0; i < now.Length; i++)
            {
                if (i % 2 == 0) g.BlendFill(x + 5, by - 1, w - 12, 9, new Rgba(255, 255, 255, 9));
                g.Small(x + 5, by + 1, Clip(now[i], half - 2, small: true), Ink);
                g.Small(x + 9 + half, by + 1, Clip(next[i], half - 2, small: true), next[i] != now[i] ? Green : Dim);
                by += 9;
            }
            by += 4;
            int lw = Math.Min(60, (w - 13) / 2);
            Button(g, x + 5, by, lw, button, "RAISE", Press.Primary);
            Button(g, x + 8 + lw, by, Math.Min(60, w - 13 - lw), button, "TO SLOT");
            by += button + 3;
            g.Small(x + 5, by, "IN SLOT 1", Dim);
            by += 9;
            g.Text(x + 5, by, "CRYSTALS 1/2", Gold, Shadow);
            g.BlendFill(x + 5, by + 9, w - 12, 1, Gold.WithAlpha(80));
            by += 12;
            int cw = w - 12;
            Slot(g, x + 5, by, 14, Rgba.Hex("#B98CFF"), null);
            g.Small(x + 22, by + 2, Clip("1 AREA: +20% REACH, +20% MANA", (cw - 50), small: true), Ink);
            Button(g, x + w - 7 - 29, by + (14 - button) / 2, 29, button, "OUT");
            by += Math.Max(16, button + 2);
            Slot(g, x + 5, by, 11, null);
            g.Small(x + 20, by + 3, "2 EMPTY", Ink);
            by += 13;
            Slot(g, x + 5, by, 11, null);
            g.Draw(UiIcons.Of(UiIcons.Lock, Dim), x + 7, by + 2);
            g.Small(x + 20, by + 3, "3 FROM RANK 5", Dim);
            by += 15;
            foreach (var gem in new[] { "LEECH CRYSTAL IN: HEAL 3% OF DAMAGE", "HASTE CRYSTAL IN: -25% COOLDOWN" })
            {
                if (by + button > y + h - 3) break;
                g.Draw(UiIcons.Of(GrowthArt.Gem, Rgba.Hex("#B98CFF")), x + 5, by + (button - 9) / 2);
                Button(g, x + 17, by, w - 23, button, Clip(gem, w - 30));
                by += button + 2;
            }
        }

        // A class to pick (a new character): the hero with its weapon in the class's colour, name, words, passive, first
        // skills, [PICK] (gold) and [PREVIEW].
        static void ClassCard(PixelImage g, int x, int y, int w, int h, ClassDefinition option, bool phone, bool preview)
        {
            var color = GrowthArt.ClassColor(option);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            g.Fill(x + 1, y + 1, 2, h - 2, color);
            int wellH = Math.Min(h - 6, 40);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x + 4, y + 3, 38, wellH);
            g.BlendFill(x + 5, y + 4, 36, wellH - 2, color.WithAlpha(46));
            Pictures.Add(((x + 4) * 2 + 2, (y + 3) * 2 + (wellH * 2 - 66) / 2, GrowthArt.Hero(HeroGear.Plain(GrowthArt.WeaponOf(option)), Rgba.Hex("#3F7FD8")), 3));
            int bw = 42, button = phone ? 20 : 14;
            int tx = x + 46;
            string name = option.Id switch { "warrior" => "WARRIOR", "fire_mage" => "FIRE MAGE", "archer" => "ARCHER", "summoner" => "SUMMONER", _ => "STORM CALLER" };
            g.Text(tx, y + 3, name, color, Shadow);
            g.Bars(tx, y + 13, w - 44 - bw - 8, 2, Ink.WithAlpha(140), option.Id.Length, 6);
            g.Small(tx, y + 26, Clip("PASSIVE: " + (option.Id == "warrior" ? "+20% LIFE, +10% MELEE" : "+15% DAMAGE"), (w - 44 - bw - 8), small: true), Green);
            for (int i = 0; i < Math.Min(3, option.Skills.Count); i++) Slot(g, tx + i * 15, y + 33, 13, Tint(option.Skills[i].Tags), null);
            Button(g, x + w - bw - 3, y + 3, bw, button, "PICK", Press.Primary);
            Button(g, x + w - bw - 3, y + 5 + button, bw, button, preview ? "CLOSE" : "PREVIEW");
        }

        // ---- The passive tree ----

        static void TreeScreen(PixelImage g, (int X, int Y, int W, int H) a, Hero hero, bool phone, bool low = false)
        {
            var (x, y, w, bottom) = GrowthWindow(g, a, 1, phone ? "TREE" : "TREE (P)", UiIcons.Tree, low, stars: true);
            int button = phone ? 20 : 14;
            var tree = hero.Data.Passives;
            bool side = w >= 320;
            int viewX = x, viewY = y, viewW = w, viewH = bottom - y;
            int sideX = 0, sideW = 0;
            if (side)
            {
                sideW = Math.Clamp((int)Math.Round(w * 0.3), 125, 165);
                sideX = x + w - sideW;
                viewW = w - sideW - 4;
            }
            // The zoom the game opens with: the whole tree on a PC, a finger's 40 points round every point on a phone.
            int minX = tree.Min(n => n.X), maxX = tree.Max(n => n.X), minY = tree.Min(n => n.Y), maxY = tree.Max(n => n.Y);
            float fit = Math.Min((viewW * 2 - 48f) / Math.Max(1, maxX - minX), (viewH * 2 - 48f) / Math.Max(1, maxY - minY));
            float zoom = phone ? Math.Clamp(Math.Max(fit, 46), 20, 110) : Math.Clamp(fit, 30, 72);
            var last = hero.Taken.LastOrDefault() ?? tree.First(n => n.IsStart);
            // Centred on the last point learned, in pixels (two points a pixel).
            float cx = viewX + viewW / 2f - last.X * zoom / 2, cy = viewY + viewH / 2f - last.Y * zoom / 2;
            (int, int) At(PassiveNode n) => ((int)Math.Round(cx + n.X * zoom / 2), (int)Math.Round(cy + n.Y * zoom / 2));
            var view = new PixelImage(g.Width, g.Height);
            int tier = zoom < 36 ? 0 : zoom < 58 ? 1 : 2;
            foreach (var node in tree)
            {
                foreach (var other in node.Links)
                {
                    if (string.CompareOrdinal(other.Id, node.Id) <= 0) continue;
                    var (ax, ay) = At(node);
                    var (bx, by) = At(other);
                    bool ta = hero.IsTaken(node), tb = hero.IsTaken(other);
                    if (ta && tb)
                    {
                        view.Line(ax, ay, bx, by, Rgba.Hex("#FFCC59").WithAlpha(46), 4);
                        view.Line(ax, ay, bx, by, Rgba.Hex("#FFDB80").WithAlpha(240), 2);
                    }
                    else if ((ta || tb) && (hero.CanLearn(node) || hero.CanLearn(other))) view.Line(ax, ay, bx, by, Rgba.Hex("#FFD173").WithAlpha(128), 1);
                    else view.Line(ax, ay, bx, by, Rgba.Hex("#7385D9").WithAlpha(90), 1);
                }
            }
            PassiveNode picked = tree.FirstOrDefault(n => n.Id == "str4");
            foreach (var node in tree)
            {
                var (px, py) = At(node);
                var look = node.IsStart ? GrowthArt.NodeLook.Start : hero.IsTaken(node) ? GrowthArt.NodeLook.Taken : hero.CanLearn(node) ? GrowthArt.NodeLook.Open : GrowthArt.NodeLook.Locked;
                int radius = node.IsStart ? 5 + tier : node.Notable ? 6 + 2 * tier : 4 + tier;
                var image = GrowthArt.Node(radius, look, GrowthArt.KindOf(node), node.Notable);
                view.Draw(image, px - image.Width / 2, py - image.Height / 2);
                if (look == GrowthArt.NodeLook.Open) view.Draw(GrowthArt.Ring(radius, GrowthArt.KindOf(node).Mix(Rgba.White, 0.5f), radius + 1), px - image.Width / 2, py - image.Height / 2);
                var glyph = GrowthArt.GlyphOf(node);
                if (glyph != null && (tier > 0 || node.Notable))
                {
                    var color = GrowthArt.GlyphColor(node);
                    if (look == GrowthArt.NodeLook.Locked) color = color.Mix(GrowthArt.Faint, 0.55f);
                    if (look == GrowthArt.NodeLook.Taken) color = color.Mix(Rgba.White, 0.35f);
                    var icon = UiIcons.Of(glyph, color);
                    view.Draw(icon, px - icon.Width / 2, py - icon.Height / 2);
                }
                if (node == picked) view.Draw(GrowthArt.Ring(radius, Rgba.White, radius + GrowthArt.Margin), px - image.Width / 2, py - image.Height / 2);
                if (zoom >= 70 || node.Notable && zoom >= 48 || node == picked)
                {
                    string label = Ascii(node.Id).ToUpperInvariant();
                    view.Small(px - MockFont.SmallWidth(label) / 2, py + radius + 3, label, look == GrowthArt.NodeLook.Taken ? Rgba.Hex("#FFE08C") : look == GrowthArt.NodeLook.Open ? Ink : Rgba.Hex("#9EA8D9"), new Rgba(0, 0, 0, 220));
                }
            }
            // The view clipped to the sky.
            for (int yy = viewY; yy < viewY + viewH; yy++)
                for (int xx = viewX; xx < viewX + viewW; xx++)
                    if (view[xx, yy].A > 0) g.Blend(xx, yy, view[xx, yy]);
            // [-] [+] [<>] in the sky's top right.
            int zx = viewX + viewW - 4 - (3 * button + 4);
            Button(g, zx, viewY + 4, button, button, "-");
            Button(g, zx + button + 2, viewY + 4, button, button, "+");
            Button(g, zx + 2 * (button + 2), viewY + 4, button, button, "O");
            int open = tree.Count(hero.CanLearn);
            if (side)
            {
                Slice(g, UiArt.Inset, UiArt.InsetBorder, sideX, y, sideW, bottom - y);
                g.BlendFill(sideX + 1, y + 1, sideW - 2, bottom - y - 2, new Rgba(8, 10, 26, 140));
                int sy = y + 4;
                TreePoints(g, sideX + 4, sy, hero, false);
                sy += 26;
                g.Bars(sideX + 5, sy, sideW - 12, 2, Ink.WithAlpha(120), 21, 6);
                sy += 14;
                if (!low)
                {
                    sy = NodeCardArt(g, sideX + 4, sy, sideW - 8, picked, hero, button) + 6;
                    Button(g, sideX + 4, sy, Math.Min(110, sideW - 8), button, "RESET ALL (30)", Press.Danger);
                    sy += button + 6;
                }
                Button(g, sideX + 4, sy, sideW - 8, button, $"CAN LEARN NOW {open} V", Press.Primary);
                sy += button + 3;
                foreach (var node in tree.Where(hero.CanLearn))
                {
                    if (sy + button > bottom - 3) break;
                    Button(g, sideX + 4, sy, sideW - 8, button, Clip($"{Ascii(node.Id).ToUpperInvariant()}: +STAT", sideW - 16));
                    sy += button + 2;
                }
                return;
            }
            // A phone held upright: the points chip, [RESET ALL], the picked point's card and the drawer over the sky.
            Card(g, viewX + 3, viewY + 3, 82, 22);
            TreePoints(g, viewX + 6, viewY + 3, hero, true);
            Button(g, viewX + 3, viewY + 28, 100, button, "RESET ALL (30)", Press.Danger);
            int rows = Math.Min(open, 3);
            int drawerH = button + 4 + rows * (button + 2) + 2;
            int dy = viewY + viewH - 3 - drawerH;
            int cardH = 66;
            Card(g, viewX + 3, dy - 3 - cardH, viewW - 6, cardH);
            NodeCardArt(g, viewX + 6, dy - cardH, viewW - 12, picked, hero, button, card: false);
            Card(g, viewX + 3, dy, viewW - 6, drawerH);
            Button(g, viewX + 5, dy + 2, viewW - 10, button, $"CAN LEARN NOW {open} V", Press.Primary);
            int iy = dy + button + 4;
            foreach (var node in tree.Where(hero.CanLearn).Take(rows))
            {
                Button(g, viewX + 5, iy, viewW - 10, button, Clip($"{Ascii(node.Id).ToUpperInvariant()}: +STAT", viewW - 18));
                iy += button + 2;
            }
        }

        static string Ascii(string id) => new string(id.Where(c => c < 128).ToArray());

        static void TreePoints(PixelImage g, int x, int y, Hero hero, bool chip)
        {
            g.Draw(UiIcons.Of(GrowthArt.Point, Gold), x + 2, y + 7);
            int nw = g.Text(x + 12, y + 4, hero.PassivePoints.ToString(), Rgba.Hex("#FFD659"), Shadow, 2);
            g.Text(x + 18 + nw, y + 3, chip ? "POINTS" : "PASSIVE POINTS", Ink, Shadow);
            g.Small(x + 18 + nw, y + 13, $"LEARNED {hero.Taken.Count}/50", Dim);
        }

        // The picked point's card: name, kind, what it raises in green, whether it can be learned, [LEARN].
        static int NodeCardArt(PixelImage g, int x, int y, int w, PassiveNode node, Hero hero, int button, bool card = true)
        {
            if (node == null) return y;
            int h = 66;
            if (card) Card(g, x, y, w, h);
            var kind = GrowthArt.KindOf(node);
            g.Fill(x + 2, y + 2, 2, h - 4, kind);
            g.Text(x + 7, y + 4, "STEEL SKIN", Rgba.Hex("#FFDB73"), Shadow);
            g.Small(x + 7, y + 14, "DEFENCE - BIG POINT", kind);
            g.Small(x + 7, y + 22, "+10% MAX LIFE", Green);
            g.Small(x + 7, y + 29, "+15% ARMOUR", Green);
            g.Small(x + 7, y + 37, Clip(hero.CanLearn(node) ? "CAN LEARN - PRESS AGAIN TO LEARN" : "LEARN A LINKED POINT FIRST", (w - 10), small: true), hero.CanLearn(node) ? Gold : Dim);
            Button(g, x + 7, y + h - button - 3, 60, button, "LEARN", hero.CanLearn(node) ? Press.Primary : Press.Off);
            return y + h;
        }

        // ---- The character ----

        static void SheetScreen(PixelImage g, (int X, int Y, int W, int H) a, Hero hero, LootData loot, bool phone, bool low = false)
        {
            var (x, y, w, bottom) = GrowthWindow(g, a, 2, phone ? "HERO" : "CHARACTER (U)", UiIcons.Person, low);
            int pictures = Pictures.Count;
            Clipped(g, x, y, w, bottom - y, () => SheetBody(g, x, y, w, bottom, hero, loot, phone));
            // A picture laid where the pane has scrolled out of sight is not seen either.
            for (int i = Pictures.Count - 1; i >= pictures; i--)
                if (Pictures[i].Y / 2 + Pictures[i].Image.Height * Pictures[i].Scale / 2 > bottom) Pictures.RemoveAt(i);
        }

        // Everything but the pixels inside the rect is put back as it was: a pane's scroll view cuts what runs past it.
        static void Clipped(PixelImage g, int x, int y, int w, int h, Action draw)
        {
            var before = g.Copy();
            draw();
            for (int j = 0; j < g.Height; j++)
                for (int i = 0; i < g.Width; i++)
                    if (i < x || i >= x + w || j < y || j >= y + h) g[i, j] = before[i, j];
        }

        static void SheetBody(PixelImage g, int x, int y, int w, int bottom, Hero hero, LootData loot, bool phone)
        {
            bool split = w >= 320;
            int lw = split ? Math.Clamp((int)Math.Round(w * 0.36), 145, 195) : w;
            int ly = y;
            int lx = x;
            if (split) Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, lw, bottom - y);
            int px = split ? lx + 4 : lx;
            int pw = split ? lw - 10 : w - 8;
            ly += split ? 4 : 0;
            g.Text(px, ly, "HERO", Rgba.Hex("#FFDB73"), Shadow);
            g.Text(px + pw - MockFont.Width("LV 12"), ly, "LV 12", Ink, Shadow);
            ly += 10;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, px, ly, pw, 8);
            g.Fill(px + 1, ly + 1, (pw - 2) * 42 / 100, 6, Rgba.Hex("#E8C04A"));
            g.Small(px + pw / 2 - 6, ly + 2, "42%", Rgba.White, new Rgba(0, 0, 0, 200));
            ly += 11;
            int chipX = px;
            foreach (var chosen in hero.Classes)
            {
                string name = chosen.Id == "warrior" ? "WARRIOR" : "FIRE MAGE";
                int cw = MockFont.Width(name) + 8;
                Slice(g, UiArt.Inset, UiArt.InsetBorder, chipX, ly, cw, 10);
                g.Fill(chipX + 1, ly + 1, 2, 8, GrowthArt.ClassColor(chosen));
                g.Text(chipX + 5, ly + 2, name, GrowthArt.ClassColor(chosen), Shadow);
                chipX += cw + 2;
            }
            ly += 13;
            g.Bars(px, ly, pw - 4, 1, Ink.WithAlpha(130), 31, 6);
            ly += 8;
            // The doll: the hero large in the middle, the worn slots at either side.
            int cell = phone ? 26 : 24;
            int column = cell * 3 + 4;
            int heroW = Math.Min(pw - 2 * cell - 12, 75);
            int left = px + (pw - (2 * cell + heroW + 8)) / 2;
            int stageX = left + cell + 4;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, stageX, ly, heroW, column + 4);
            g.BlendFill(stageX + 1, ly + 1, heroW - 2, column + 2, Rgba.Hex("#3D2E1F").WithAlpha(90));
            g.BlendFill(stageX + 6, ly + column - 7, heroW - 12, 3, new Rgba(0, 0, 0, 90));
            var sword = loot.Bases.FirstOrDefault(b => b.Id == "iron_sword") ?? loot.Bases.First();
            int heroScale = Math.Max(2, Math.Min((heroW * 2 - 8) / 24, ((column + 4) * 2 - 8) / 22));
            Pictures.Add((stageX * 2 + (heroW * 2 - 24 * heroScale) / 2, ly * 2 + ((column + 4) * 2 - 10 - 22 * heroScale) / 2 + 2, GrowthArt.Hero(ItemArt.Held(sword), Rgba.Hex("#3F7FD8")), heroScale));
            var gear = loot.Bases.Select(ItemArt.Gear).ToList();
            for (int r = 0; r < 3; r++)
            {
                Slot(g, left, ly + 2 + r * (cell + 2), cell, r == 0 ? Rarities[2] : null, r == 0 ? ItemArt.Gear(sword) : null);
                Slot(g, stageX + heroW + 4, ly + 2 + r * (cell + 2), cell, r < 2 ? Rarities[1 + r] : null, r < 2 ? gear[(r * 11 + 3) % gear.Count] : null);
            }
            ly += column + 8;
            Slice(g, UiArt.Inset, UiArt.InsetBorder, px, ly, pw, 30);
            g.Text(px + 4, ly + 3, "IRON SWORD (WORN)", Rarities[2], Shadow);
            g.Bars(px + 4, ly + 13, pw - 12, 1, Ink.WithAlpha(130), 32, 6);
            Button(g, px + 4, ly + 19, 34, phone ? 20 : 10, "OFF");
            ly += 34;
            g.Small(px, ly, "SKILL 2 - PASSIVE 5 - LEARNED 6", Dim);
            // The stats in groups, the life skills, the pet.
            int rx = split ? x + lw + 5 : x;
            int ry = split ? y : ly + 12;
            int rw = split ? x + w - rx : w;
            if (!split && ry > bottom - 40) return;
            bool two = rw >= 250;
            int gw = two ? (rw - 4) / 2 : rw;
            var groups = new (string Title, Rgba Color, string[] Rows)[]
            {
                ("ATTACK", GrowthArt.Attack, new[] { "SLASH DAMAGE|15 + FIRE 6", "ATTACKS A SECOND|1.4", "CRIT CHANCE|5%", "CRIT DAMAGE|X150%", "WHIRLWIND|23.4", "FIREBALL|31" }),
                ("DEFENCE", GrowthArt.Defence, new[] { "LIFE|240", "LIFE REGEN|1.5/S", "ARMOUR|38" }),
                ("MANA, SKILLS", GrowthArt.Magic, new[] { "MANA|110", "MANA REGEN|2/S", "COOLDOWNS|+0%", "DURATIONS|+0%" }),
                ("ELEMENTS", Rgba.Hex("#FF994D"), new[] { "FIRE ADDED|+6", "COLD ADDED|-", "LIGHTNING ADDED|-" }),
                ("MOVING, OTHER", GrowthArt.Other, new[] { "MOVE SPEED|5", "MINING SPEED|1", "ITEM RARITY|+5%" }),
            };
            var glyphs = new[] { GrowthArt.Sword, UiIcons.Heart, GrowthArt.Drop, GrowthArt.Flame, GrowthArt.Boot };
            for (int i = 0; i < groups.Length; i += two ? 2 : 1)
            {
                int rows = groups[i].Rows.Length;
                if (two && i + 1 < groups.Length) rows = Math.Max(rows, groups[i + 1].Rows.Length);
                int gh = 14 + rows * 9 + 3;
                if (ry + gh > bottom) return;
                StatCard(g, rx, ry, gw, gh, groups[i], glyphs[i]);
                if (two && i + 1 < groups.Length) StatCard(g, rx + gw + 4, ry, gw, gh, groups[i + 1], glyphs[i + 1]);
                ry += gh + 4;
            }
            if (ry + 44 > bottom) return;
            g.Text(rx, ry, "PET", Gold, Shadow);
            g.BlendFill(rx, ry + 9, rw, 1, Gold.WithAlpha(80));
            ry += 12;
            Slot(g, rx, ry, 38, Rgba.Hex("#8CD966"), CreatureArt.Monster("slime", Rgba.Hex("#8CD94D"), 0));
            g.Text(rx + 42, ry + 1, "MALLANGI", Rgba.Hex("#FFDB73"), Shadow);
            g.Text(rx + rw - MockFont.Width("LV 5"), ry + 1, "LV 5", Ink, Shadow);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, rx + 42, ry + 12, rw - 42, 7);
            g.Fill(rx + 43, ry + 13, (rw - 44) * 3 / 10, 5, Rgba.Hex("#E8C04A"));
            g.Small(rx + 42, ry + 22, "+5% LIFE, BITES EVERY 2S", Green);
            ry += 42;
            if (ry + 14 > bottom) return;
            g.Text(rx, ry + 3, "NAME", Ink, Shadow);
            Slice(g, UiArt.Inset, UiArt.InsetBorder, rx + 20, ry + 1, 70, 12);
            Button(g, rx + 92, ry, 44, phone ? 20 : 14, "NAME IT");
            Button(g, rx + 138, ry, 28, phone ? 20 : 14, "IDEA");
            ry += (phone ? 20 : 14) + 6;
            if (ry + 12 > bottom) return;
            g.Text(rx, ry, "LIFE SKILLS", Gold, Shadow);
            g.BlendFill(rx, ry + 9, rw, 1, Gold.WithAlpha(80));
            ry += 12;
            foreach (var (name, level, share) in new[] { ("FARMING", 3, 40), ("FISHING", 1, 10), ("MINING", 5, 70), ("COOKING", 2, 55) })
            {
                if (ry + 18 > bottom) return;
                g.Text(rx, ry + 1, $"{name} {level}", Ink, Shadow);
                Slice(g, UiArt.Inset, UiArt.InsetBorder, rx + 55, ry, rw - 55, 7);
                g.Fill(rx + 56, ry + 1, (rw - 57) * share / 100, 5, Rgba.Hex("#E8C04A"));
                g.Bars(rx, ry + 10, rw - 20, 1, Ink.WithAlpha(110), level, 6);
                ry += 15;
                if (name == "MINING")
                {
                    if (ry + 12 > bottom) return;
                    g.Small(rx + 6, ry + 3, "SWIFT PICK: MINE 15% FASTER", Green);
                    Button(g, rx + rw - 36, ry, 36, phone ? 20 : 12, "PICK", Press.Primary);
                    ry += phone ? 22 : 14;
                }
            }
        }

        static void StatCard(PixelImage g, int x, int y, int w, int h, (string Title, Rgba Color, string[] Rows) group, string[] glyph)
        {
            Slice(g, UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            g.BlendFill(x + 1, y + 1, w - 2, 2, group.Color.WithAlpha(200));
            g.Text(x + 5, y + 4, group.Title, group.Color, Shadow);
            int ry = y + 14;
            foreach (string row in group.Rows)
            {
                var parts = row.Split('|');
                g.Draw(UiIcons.Of(glyph, group.Color), x + 4, ry);
                g.Small(x + 15, ry + 2, Clip(parts[0], (w - 50), small: true), Ink);
                g.Small(x + w - 4 - MockFont.SmallWidth(parts[1]), ry + 2, parts[1], Rgba.Hex("#FFE6A6"));
                ry += 9;
            }
        }
    }
}
