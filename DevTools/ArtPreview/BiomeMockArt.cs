using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using static Game.Client.BM;

namespace Game.Client
{
    // The mockups' drawings (Docs/BiomeRework.md 4-1, 4-2): each biome's look, its props in four tiers, its small
    // creatures and its monsters at their size classes. Rough sprites built from shaded shapes, to show silhouettes
    // and sizes; the game's own would be drawn properly.
    static class BiomeMockArt
    {
        // Tags of a roster entry: 0 in the game now, 1 new (first wave), 2 new (second wave), 3 a recolour of an
        // existing body, 4 a mid-boss, 5 an existing monster redrawn.
        public sealed class Mon
        {
            public string Name, Size;
            public int Tag, Lift;
            public PixelImage Img;
        }

        public sealed class Prop
        {
            public string Name, Tier;
            public PixelImage Img;
        }

        static readonly Dictionary<string, string> Colours = new Dictionary<string, string>
        {
            ["slime"] = "#8CD94D", ["spore_shooter"] = "#B07CE0", ["cave_beetle"] = "#A0703C", ["crystal_bat"] = "#7CD2E2", ["crystal_wisp"] = "#C8F4FF",
            ["myconid"] = "#C86ED8", ["glow_bat"] = "#5FE0C8", ["ice_slime"] = "#8CC8F0", ["frost_wisp"] = "#BFE8FF", ["magma_slime"] = "#E0541E",
            ["fire_bat"] = "#FF8A3A", ["void_wisp"] = "#A07CFF", ["abyss_stalker"] = "#5E3E96", ["lake_crab"] = "#4AA0B4", ["tide_wisp"] = "#5AD2E6",
            ["sand_scorpion"] = "#D2A050", ["sand_worm"] = "#C8965A",
        };

        public static PixelImage Old(string id, string colour = null) => Trim(CreatureArt.Monster(id, C(colour ?? Colours[id]), 0));

        static Mon M(string name, string size, int tag, PixelImage img, int lift = 0) => new Mon { Name = name, Size = size, Tag = tag, Img = img, Lift = lift };
        static Prop P(string tier, string name, PixelImage img) => new Prop { Tier = tier, Name = name, Img = img };

        public static readonly BiomeLook[] Looks =
        {
            new BiomeLook
            {
                Id = "cave", Name = "DIRT CAVE", Concept = "WARM EARTH TUNNELS THE OLD MINERS LEFT", Core = Biome.Cave, TodayFloor = "#352B24",
                Floor = C("#3A2B20"), Wall = C("#5C4532"), Top = C("#7A5E44"), Accent = C("#E0A050"), Extra = C("#6E7A3C"), Light = C("#FFB060"), Ambient = 0.62f,
                FaceH = 8, Corner = 6, Hazard = "NO HAZARD. LIT LAMPS KEEP RATS AWAY", Dungeon = "ABANDONED MINE",
                Chips = new[] { ("#3A2B20", "FLOOR"), ("#5C4532", "WALL"), ("#7A5E44", "TOP"), ("#E0A050", "LAMP"), ("#6E7A3C", "MOSS") },
                Map = new[]
                {
                    "####################", "#####.......########", "###...........######", "##.............#####", "##..............####", "#................###",
                    "#...............#...", "##..................", "##..............#...", "###............#####", "#####........#######", "####################",
                },
            },
            new BiomeLook
            {
                Id = "mushroom", Name = "MUSHROOM FOREST", Concept = "A BREATHING WOOD OF MYCELIUM", Core = Biome.Mushroom, TodayFloor = "#2C2532",
                Floor = C("#24291C"), Wall = C("#3E3A2A"), Top = C("#565038"), Accent = C("#B8F25A"), Extra = C("#4F86E8"), Light = C("#A8F06A"), Ambient = 0.78f,
                FaceH = 7, Corner = 7, Hazard = "SPORE CLOUD: MONSTERS INSIDE HEAL", Dungeon = "SPORE BURROW",
                Chips = new[] { ("#24291C", "FLOOR"), ("#3E3A2A", "WALL"), ("#4F86E8", "CAP"), ("#B8F25A", "SPORE"), ("#E88AB0", "GILL") },
                Map = new[]
                {
                    "####################", "#......##......#####", "#......##........###", "#...##......##.....#", "#...##......##.....#", "#........##........#",
                    "##.......##....##..#", "##..##.........##..#", "#...##....##.......#", "#.........##......##", "###.....#######..###", "####################",
                },
            },
            new BiomeLook
            {
                Id = "ice", Name = "ICE CAVE", Concept = "A GREAT FROZEN HALL", Core = Biome.Ice, TodayFloor = "#2E3A46",
                Floor = C("#34485A"), Wall = C("#8FB4CC"), Top = C("#DCEFFA"), Accent = C("#6FD0FF"), Extra = C("#1E2C3C"), Light = C("#A8DCFF"), Ambient = 0.86f,
                FaceH = 10, Corner = 5, Chamfer = true, Hazard = "SLIDING ICE + FALLING ICICLES (1S RING)", Dungeon = "FROZEN TOMB",
                Chips = new[] { ("#34485A", "FLOOR"), ("#8FB4CC", "WALL"), ("#DCEFFA", "TOP"), ("#6FD0FF", "GLOW"), ("#1E2C3C", "SHADE") },
                Map = new[]
                {
                    "####################", "####################", "#..................#", "#....,,,,..........#", "#...,,,,,,....,,,..#", "#...,,,,,,...,,,,,.#",
                    "#....,,,,....,,,,,.#", "#.............,,,..#", "#..................#", "#..................#", "####################", "####################",
                },
            },
            new BiomeLook
            {
                Id = "crystal", Name = "CRYSTAL CAVERN", Concept = "FISSURES OF LIGHT-BENDING CRYSTAL", Core = Biome.Crystal, TodayFloor = "#262536",
                Floor = C("#2B2E38"), Wall = C("#4B5060"), Top = C("#666C7E"), Accent = C("#FF7FD0"), Extra = C("#7FF2FF"), Light = C("#9ADCFF"), Ambient = 0.8f,
                FaceH = 9, Corner = 6, Chamfer = true, Hazard = "BIG CRYSTALS BOUNCE SHOTS (YOURS TOO). LIGHT BREATHES PINK - CYAN", Dungeon = "INSIDE THE GEODE",
                Chips = new[] { ("#2B2E38", "SLATE"), ("#4B5060", "WALL"), ("#FF7FD0", "PINK"), ("#7FF2FF", "CYAN"), ("#F4F0FF", "GLINT") },
                Map = new[]
                {
                    "####################", "###....#####....####", "##......###......###", "##.......#........##", "###...............##", "####.............###",
                    "###.......#.......##", "##.......###.......#", "##......#####......#", "###....######.....##", "####..########..####", "####################",
                },
            },
            new BiomeLook
            {
                Id = "sand", Name = "SAND CAVE", Concept = "AN OLD TEMPLE BURIED IN SAND", Core = Biome.Sand, TodayFloor = "#6A5236",
                Floor = C("#8C6A44"), Wall = C("#B98C5A"), Top = C("#D2A874"), Accent = C("#EDE3C8"), Extra = C("#9A6E44"), Light = C("#FFD28A"), Ambient = 1f,
                FaceH = 8, Corner = 7, Hazard = "QUICKSAND. SAND STREAMS MARK BURIED FOES", Dungeon = "BURIED TEMPLE",
                Chips = new[] { ("#8C6A44", "FLOOR"), ("#B98C5A", "WALL"), ("#9A6E44", "BAND"), ("#EDE3C8", "BONE"), ("#5A3C24", "SHADE") },
                Map = new[]
                {
                    "####################", "##................##", "#..................#", "#......,,..........#", "#.....,,,,.........#", "#......,,......##..#",
                    "#..............##..#", "#..##..............#", "#..##.........,,...#", "#............,,,...#", "##................##", "####################",
                },
            },
            new BiomeLook
            {
                Id = "lava", Name = "LAVA FIELDS", Concept = "A BASALT FORGE THAT NEVER COOLS", Core = Biome.Lava, TodayFloor = "#2A2220",
                Floor = C("#1E1716"), Wall = C("#362826"), Top = C("#4A3836"), Accent = C("#FF6A20"), Extra = C("#FFC040"), Light = C("#FF7A30"), Liquid = C("#FF6A20"), Ambient = 0.85f,
                FaceH = 10, Corner = 3, Chamfer = true, Hazard = "LAVA + STEAM VENTS (1S WARNING, EVERY 4S)", Dungeon = "EMBER FORGE",
                Chips = new[] { ("#1E1716", "BASALT"), ("#362826", "WALL"), ("#FF6A20", "CRACK"), ("#FFC040", "FIRE"), ("#2A1E3A", "OBSID.") },
                Map = new[]
                {
                    "####################", "##.......~~~......##", "#........~~~.......#", "#.......~~~~.......#", "#.......~~~........#", "#..................#",
                    "#.......~~~........#", "#......~~~~~.......#", "#......~~~~........#", "#.......~~~........#", "##......~~~.......##", "####################",
                },
            },
            new BiomeLook
            {
                Id = "lake", Name = "UNDERGROUND LAKE", Concept = "MOSSY SHORES AND ISLANDS", Core = Biome.Lake, TodayFloor = "#22343A",
                Floor = C("#22383A"), Wall = C("#3A5650"), Top = C("#527468"), Accent = C("#4FD2C0"), Extra = C("#E8E0CC"), Light = C("#70E6D8"), Ambient = 0.85f,
                FaceH = 6, Corner = 6, Hazard = "SHALLOWS SLOW YOU. TENTACLES ONLY NEAR WATER", Dungeon = "FLOODED CHANNEL",
                Chips = new[] { ("#22383A", "FLOOR"), ("#3A5650", "WALL"), ("#4FD2C0", "WATER"), ("#E8E0CC", "SHELL"), ("#7A9440", "REED") },
                Map = new[]
                {
                    "####################", "##................##", "#....,,,,,.........#", "#...,~~~~~,........#", "#..,~~~~~~~,....,,.#", "#..,~~~..~~,...,~~,#",
                    "#..,~~~..~~,...,~~,#", "#...,~~~~~,.....,,.#", "#....,,,,,.........#", "#..................#", "##................##", "####################",
                },
            },
            new BiomeLook
            {
                Id = "abyss", Name = "VOID ABYSS", Concept = "BROKEN LAND ADRIFT IN THE DARK", Core = Biome.Abyss, TodayFloor = "#16121F",
                Floor = C("#1A1428"), Wall = C("#221830"), Top = C("#33264A"), Accent = C("#A050FF"), Extra = C("#E6D6FF"), Light = C("#6A30C0"), Ambient = 0.9f,
                FaceH = 8, Corner = 5, Chamfer = true, Hazard = "CRACKED PLATES FALL AFTER 1S. TORCHES -30%", Dungeon = "BEYOND THE BROKEN GATE",
                Chips = new[] { ("#110D1A", "PLATE"), ("#221830", "WALL"), ("#A050FF", "VOID"), ("#E6D6FF", "STAR"), ("#05030A", "GAP") },
                Map = new[]
                {
                    "~~~~~~~~~~~~~~~~~~~~", "~~.....~~~~......~~~", "~.......~~........~~", "~.......,,........~~", "~~..#..~~~~......~~~", "~~~..~~~~~~~~.,.~~~~",
                    "~~~..~~~~~~~~...~~~~", "~~.....~~~......~~~~", "~.......,,....##.~~~", "~.......~~.......~~~", "~~.....~~~~.....~~~~", "~~~~~~~~~~~~~~~~~~~~",
                },
            },
        };

        public static BiomeLook Look(string id) => Looks.First(l => l.Id == id);

        // ---- Shapes many props share ----

        public static PixelImage Pebbles(Ramp r) => new Sp(7, 4).Ell(2f, 2.4f, 2f, 1.5f, r).Ell(5.2f, 2.8f, 1.7f, 1.2f, r).Out();

        public static PixelImage Tuft(Ramp r, int w, int h, int seed, Rgba tip = default)
        {
            var s = new Sp(w, h);
            for (int i = 0; i < w; i += 2)
            {
                int top = (int)(V(i, seed, 71) * h * 0.55f), lean = i < w / 2 ? -1 : 1;
                s.Ln(i + (i == 0 || i >= w - 2 ? lean : 0), top, i, h - 1, i % 4 == 0 ? r.Light : r.Main);
                if (tip.A > 0) s.Px(i + (i == 0 || i >= w - 2 ? lean : 0), top, tip);
            }
            return s.Out(r.Dark.Scale(0.6f));
        }

        public static PixelImage Stalag(int w, int h, Ramp r) => new Sp(w, h).Spike(w / 2f, h, w / 2f, w / 2f - 0.5f, 0, r).Out();

        public static PixelImage Boulder(int w, int h, Ramp r, Rgba crack = default)
        {
            var s = new Sp(w, h).Ell(w / 2f, h * 0.64f, w / 2f, h * 0.64f, r).Ell(w * 0.36f, h * 0.45f, w * 0.32f, h * 0.4f, r);
            if (crack.A > 0) s.Ln((int)(w * 0.55f), (int)(h * 0.2f), (int)(w * 0.45f), (int)(h * 0.6f), crack).Ln((int)(w * 0.45f), (int)(h * 0.6f), (int)(w * 0.7f), h - 2, crack).Ln((int)(w * 0.2f), (int)(h * 0.6f), (int)(w * 0.45f), (int)(h * 0.6f), crack);
            return s.Out();
        }

        public static PixelImage Crystals(int w, int h, Ramp a, Ramp b, int n, int seed)
        {
            var s = new Sp(w, h);
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => MathF.Abs((i + 0.5f) / n - 0.5f)))
            {
                float f = (i + 0.5f) / n, bx = 2 + f * (w - 4), len = h * (0.5f + 0.5f * V(i, seed, 72)) * (1.15f - MathF.Abs(f - 0.5f));
                s.Spike(bx, h, 1.7f + V(i, seed, 73) * 1.3f, bx + (f - 0.5f) * w * 0.45f, h - len, i % 2 == 0 ? a : b);
            }
            return s.Out();
        }

        // part: 0 whole, 1 the stem alone, 2 the cap alone (for fading the cap).
        public static PixelImage MushTree(int capW, int capH, int stemW, int stemH, Ramp cap, Ramp stem, Rgba spot, int part = 0, Rgba gill = default)
        {
            var s = new Sp(capW, capH + stemH);
            if (part != 2) s.Col((capW - stemW) / 2, capH - 3, stemW, stemH + 3, stem);
            if (part != 1)
            {
                s.Dome(capW / 2f, capH, capW / 2f, capH, cap);
                if (gill.A > 0) s.Rect(2, capH - 1, capW - 4, 1, gill);
                for (int i = 0; i < capW * capH / 26; i++)
                {
                    int x = 2 + (int)(V(i, capW, 74) * (capW - 5)), y = 1 + (int)(V(i, capH, 75) * (capH - 4));
                    if (s.I.IsSet(x, y) && s.I.IsSet(x + 1, y)) s.Rect(x, y, capW > 20 ? 2 : 1, capW > 20 && i % 2 == 0 ? 2 : 1, spot);
                }
            }
            return s.Out();
        }

        public static PixelImage Pillar(int w, int h, Ramp r, bool broken = false)
        {
            var s = new Sp(w, h).Col(1, 2, w - 2, h - 4, r).Box(0, h - 3, w, 3, r);
            if (!broken) s.Box(0, 0, w, 3, r);
            else
                for (int x = 1; x < w - 1; x++) s.Rect(x, 0, 1, 2 + (int)(V(x, w, 76) * 4), Rgba.Clear);
            for (int y = 5; y < h - 4; y += 4) s.Px(r.Shade, w / 2, y, w / 2, y + 1);
            return s.Out();
        }

        // ---- Props, biome by biome ----

        static readonly Ramp Wood = R("#8A5C30"), WoodL = R("#B88448"), Iron = R("#6A7080"), Bone = R("#EDE3C8");

        public static List<Prop> Props(string id)
        {
            var p = new List<Prop>();
            switch (id)
            {
                case "cave":
                {
                    var rock = R("#6A5440");
                    p.Add(P("cover", "PEBBLES", Pebbles(rock)));
                    p.Add(P("cover", "SHROOM", Rows("a=#C09060 b=#8A5C30 c=#E8DCC0 o=#2A1A12", ".ooo.|oaaao|obbbo|.oco.|.oco.")));
                    p.Add(P("cover", "ROOTS", Tuft(R("#8A6A48"), 7, 5, 1)));
                    p.Add(P("medium", "STALAGMITE", Stalag(9, 21, R("#7A5E44"))));
                    p.Add(P("medium", "PIT PROP", new Sp(16, 22).Col(1, 3, 3, 19, Wood).Col(12, 3, 3, 19, Wood).Box(0, 0, 16, 4, WoodL).Out()));
                    p.Add(P("medium", "CART", new Sp(17, 12).Box(2, 2, 13, 7, Iron).Rect(3, 3, 11, 1, C("#3E4250")).Ell(5f, 10f, 2f, 2f, R("#3E4250")).Ell(12f, 10f, 2f, 2f, R("#3E4250")).Px(C("#D08A4A"), 0, 10, 1, 11, 16, 11, 4, 2, 7, 2, 9, 3).Out()));
                    p.Add(P("medium", "ORE SACK", new Sp(9, 10).Ell(4.5f, 6f, 4.3f, 4f, R("#B09A6A")).Px(C("#6A5438"), 4, 1, 5, 1, 4, 2).Px(C("#D08A4A"), 3, 3, 5, 3).Out()));
                    p.Add(P("medium", "LAMP", new Sp(7, 17).Col(3, 7, 2, 10, Wood).Box(1, 1, 5, 6, R("#E0A050")).Px(C("#FFF0B0"), 3, 3, 3, 4, 2, 4).Rect(1, 0, 5, 1, C("#5A3A20")).Out()));
                    p.Add(P("large", "CAVE-IN 2X2", new Sp(30, 19).Ell(9f, 13f, 8.5f, 6.5f, rock).Ell(21f, 14f, 8.5f, 5.5f, rock).Ln(4, 15, 24, 3, Wood.Main, 3).Ln(4, 14, 24, 2, Wood.Light).Ln(10, 2, 27, 14, Wood.Shade, 2).Out()));
                    p.Add(P("large", "BOULDER 2X2", Boulder(28, 22, rock)));
                    p.Add(P("landmark", "MINE GATE", new Sp(46, 42).Rect(8, 8, 30, 34, C("#0E0A0A")).Rect(8, 36, 30, 6, C("#1E1612")).Ln(12, 41, 20, 30, C("#6A7080")).Ln(33, 41, 25, 30, C("#6A7080")).Col(2, 5, 7, 37, Wood).Col(37, 5, 7, 37, Wood).Box(0, 0, 46, 8, WoodL).Ln(4, 12, 12, 8, Wood.Shade, 2).Ln(41, 12, 33, 8, Wood.Shade, 2).Box(21, 9, 4, 5, R("#E0A050")).Out()));
                    break;
                }
                case "mushroom":
                {
                    var cap = R("#4F86E8"); var stem = R("#D8D0B0"); var lime = C("#B8F25A");
                    p.Add(P("cover", "GLOW GRASS", Tuft(R("#5E8A3A"), 7, 6, 2, lime)));
                    p.Add(P("cover", "CAPS", Rows("a=#D8A070 b=#A86A3A c=#E8DCC0 o=#1A1410", ".oo...oo.|oaaoooaao|obbooobbo|.co.c.co.|....c....")));
                    p.Add(P("cover", "MOSS", new Sp(8, 4).Ell(4f, 2.6f, 4f, 2f, R("#56682E")).Out(C("#1E2814"))));
                    p.Add(P("medium", "PILLAR CAP", MushTree(13, 8, 4, 16, R("#E88AB0"), stem, C("#FFD8E8"), 0, C("#A04A70"))));
                    p.Add(P("medium", "SHELF STUMP", new Sp(13, 13).Col(3, 3, 7, 10, R("#6A5238")).Ell(6.5f, 3f, 3.5f, 1.8f, R("#9A7A50")).Rect(0, 6, 4, 2, C("#E8B070")).Rect(9, 9, 4, 2, C("#E8B070")).Rect(0, 8, 3, 1, C("#A86A3A")).Rect(10, 11, 3, 1, C("#A86A3A")).Out()));
                    p.Add(P("medium", "SPORE SAC", new Sp(10, 11).Ell(5f, 6f, 4.6f, 5f, R("#C8D070")).Px(C("#5A6A2A"), 4, 1, 5, 1).Px(lime, 3, 4, 6, 6, 4, 8).Out()));
                    p.Add(P("medium", "GLOW CAPS", new Sp(13, 10).Col(2, 5, 2, 5, stem).Col(6, 3, 2, 7, stem).Col(10, 6, 2, 4, stem).Dome(3f, 6f, 3f, 3f, R("#8AD848")).Dome(7f, 4f, 3.5f, 4f, R("#B8F25A")).Dome(11f, 7f, 2.5f, 3f, R("#8AD848")).Out()));
                    p.Add(P("large", "MUSH TREE 2X2", MushTree(30, 16, 7, 18, cap, stem, C("#CFE4FF"), 0, C("#2A4A90"))));
                    p.Add(P("large", "FALLEN LOG 2X1", new Sp(30, 11).Box(1, 2, 26, 9, R("#7A6040")).Ell(27f, 6.5f, 2.8f, 4.5f, R("#C8A878")).Speck(C("#56682E"), 0.3f, 3, 0, 4).Rect(6, 0, 4, 2, C("#E8B070")).Rect(15, 0, 5, 2, lime).Out()));
                    p.Add(P("landmark", "GIANT CAP 3X3", MushTree(50, 24, 11, 22, R("#3E6AC8"), stem, C("#CFE4FF"), 0, C("#2A4A90"))));
                    break;
                }
                case "ice":
                {
                    var ice = R("#A8D4EC"); var snow = R("#E8F4FC");
                    p.Add(P("cover", "FROST GRASS", Tuft(R("#9AC8DC"), 7, 5, 3, C("#F0FAFF"))));
                    p.Add(P("cover", "SHARDS", new Sp(8, 5).Spike(2f, 5f, 1.6f, 1f, 1f, ice).Spike(6f, 5f, 1.4f, 7f, 2f, ice).Out(C("#22364A"))));
                    p.Add(P("cover", "SNOW", new Sp(10, 3).Ell(5f, 2.2f, 5f, 1.8f, snow).Out(C("#5A7C96"))));
                    p.Add(P("medium", "ICE SPIKE", Stalag(9, 22, ice)));
                    p.Add(P("medium", "FROZEN BUSH", new Sp(13, 11).Ell(6.5f, 6.5f, 6f, 5f, R("#6A9AB4")).Speck(C("#F0FAFF"), 0.3f, 4, 0, 6).Px(C("#E05A7A"), 4, 6, 9, 5, 7, 8).Out()));
                    p.Add(P("medium", "SNOW MOUND", new Sp(15, 8).Dome(7.5f, 8f, 7.5f, 7.5f, snow).Out(C("#5A7C96"))));
                    p.Add(P("medium", "ICE CRYSTALS", Crystals(13, 12, R("#8AE0FF"), R("#D8F4FF"), 4, 5)));
                    p.Add(P("large", "ICE PILLAR 2X2", new Sp(22, 38).Col(3, 3, 16, 33, ice).Box(0, 33, 22, 5, ice).Spike(11f, 6f, 8f, 9f, 0f, ice).Ln(7, 8, 7, 30, C("#F0FAFF")).Ln(13, 12, 15, 26, C("#6FA8C8")).Out(C("#22364A"))));
                    p.Add(P("large", "ICEFALL 2X3", Icefall()));
                    p.Add(P("landmark", "BEAST IN ICE", BeastInIce()));
                    break;
                }
                case "crystal":
                {
                    var pink = R("#FF7FD0"); var cyan = R("#7FF2FF"); var slate = R("#5A6074");
                    p.Add(P("cover", "DUST", Rows("a=#FF7FD0 b=#7FF2FF c=#F4F0FF", "a...b|..c..|b...a")));
                    p.Add(P("cover", "SHARDS", new Sp(8, 6).Spike(2f, 6f, 1.6f, 1f, 1f, pink).Spike(6f, 6f, 1.5f, 7f, 2f, cyan).Out()));
                    p.Add(P("cover", "SLATE", new Sp(8, 4).Box(0, 1, 5, 3, slate).Box(4, 0, 4, 3, slate).Out()));
                    p.Add(P("medium", "CLUSTER", Crystals(14, 14, pink, cyan, 5, 6)));
                    p.Add(P("medium", "SPIRE 1X2", new Sp(9, 26).Spike(4.5f, 26f, 4.5f, 5f, 0f, cyan).Spike(2f, 26f, 2f, 1f, 16f, pink).Out()));
                    p.Add(P("medium", "RESONATOR", new Sp(13, 18).Spike(6.5f, 14f, 3.5f, 6.5f, 2f, R("#F4F0FF")).Spike(6.5f, 14f, 3.5f, 6.5f, 17f, R("#B8B0E0")).Box(2, 15, 9, 3, slate).Px(cyan.Light, 1, 6, 11, 6, 0, 9, 12, 9).Out()));
                    p.Add(P("medium", "GEODE", new Sp(13, 11).Ell(6.5f, 6f, 6.3f, 5.3f, slate).Ell(7f, 6f, 3.5f, 3f, pink).Px(C("#F4F0FF"), 6, 5, 8, 6).Ln(2, 3, 5, 5, C("#22242E")).Out()));
                    p.Add(P("large", "CRYSTAL MASS 2X2", new Sp(30, 30).Spike(8f, 30f, 6f, 4f, 10f, cyan).Spike(22f, 30f, 6f, 27f, 8f, pink).Spike(15f, 30f, 8f, 15f, 0f, R("#C8B8FF")).Spike(10f, 30f, 4f, 9f, 17f, pink).Spike(21f, 30f, 4f, 20f, 15f, cyan).Out()));
                    p.Add(P("large", "LEANING 1X3", new Sp(20, 40).Spike(5f, 40f, 5f, 17f, 0f, cyan).Spike(8f, 40f, 3f, 3f, 26f, pink).Out()));
                    p.Add(P("landmark", "GIANT GEODE 3X3", new Sp(48, 40).Ell(24f, 24f, 23.5f, 20f, slate).Ell(24f, 25f, 15f, 13f, R("#C050A0")).Ell(24f, 26f, 10f, 9f, R("#FFB8E8")).Spike(15f, 36f, 3f, 13f, 22f, cyan).Spike(33f, 36f, 3f, 35f, 22f, cyan).Spike(24f, 37f, 3.5f, 24f, 20f, R("#F4F0FF")).Spike(8f, 12f, 3f, 4f, 0f, pink).Spike(40f, 12f, 3f, 44f, 1f, cyan).Out()));
                    break;
                }
                case "sand":
                {
                    var stone = R("#C89C68"); var green = R("#7A9A4A");
                    p.Add(P("cover", "DRY GRASS", Tuft(R("#C8A860"), 7, 6, 4)));
                    p.Add(P("cover", "BONES", Rows("a=#EDE3C8 b=#B8A888 o=#3A2818", "oo....oo|oaooooao|.oaaaab.|oaooooao|oo....oo")));
                    p.Add(P("cover", "STONES", Pebbles(R("#A07C52"))));
                    p.Add(P("medium", "CACTUS 1X2", new Sp(13, 22).Col(5, 1, 4, 21, green).Col(1, 7, 3, 6, green).Rect(1, 12, 5, 2, green.Main).Col(10, 4, 3, 7, green).Rect(8, 10, 4, 2, green.Shade).Px(C("#E05A7A"), 6, 0, 7, 0, 2, 6).Out()));
                    p.Add(P("medium", "SKULL", new Sp(10, 9).Ell(5f, 4f, 4.6f, 3.8f, Bone).Box(2, 6, 6, 3, Bone).Px(C("#3A2818"), 3, 4, 3, 5, 6, 4, 6, 5, 4, 8, 6, 8).Out()));
                    p.Add(P("medium", "BROKEN POT", new Sp(11, 11).Ell(5.5f, 6.5f, 5f, 4.5f, R("#B86A3A")).Rect(2, 0, 7, 3, Rgba.Clear).Px(C("#E8A060"), 2, 3, 4, 2, 6, 3, 8, 2).Rect(3, 6, 5, 1, C("#EDE3C8")).Out()));
                    p.Add(P("medium", "PILLAR STUB", Pillar(10, 15, stone, true)));
                    p.Add(P("medium", "SAND STREAM", new Sp(11, 34).Rect(5, 0, 1, 30, C("#EDD8A8")).Rect(6, 0, 1, 30, C("#C8A470")).Px(C("#EDD8A8"), 4, 6, 7, 13, 4, 21, 7, 26).Dome(5.5f, 34f, 5.5f, 5f, R("#C8A470")).Out(C("#5A3C24"))));
                    p.Add(P("large", "RIB ARCH 2X2", RibArch()));
                    p.Add(P("large", "SUNK COLUMN 2X2", new Sp(26, 28).Col(7, 3, 11, 22, stone).Box(5, 0, 15, 4, R("#D8B078")).Px(stone.Shade, 12, 8, 12, 9, 12, 14, 12, 15).Dome(13f, 28f, 13f, 6f, R("#A27E54")).Out(C("#3A2818"))));
                    p.Add(P("landmark", "TEMPLE FRONT", Temple()));
                    break;
                }
                case "lava":
                {
                    var basalt = R("#3E3230"); var obs = R("#3A2A52"); var fire = C("#FF6A20");
                    p.Add(P("cover", "ASH", new Sp(9, 4).Ell(4.5f, 2.6f, 4.5f, 2f, R("#5A5050")).Out()));
                    p.Add(P("cover", "EMBER ROCK", new Sp(6, 5).Ell(3f, 3f, 3f, 2.4f, basalt).Px(fire, 2, 2, 3, 3).Px(C("#FFC040"), 3, 2).Out()));
                    p.Add(P("cover", "BUBBLE", new Sp(8, 4).Dome(2.5f, 4f, 2.5f, 3f, basalt).Dome(6f, 4f, 2f, 2f, basalt).Out()));
                    p.Add(P("medium", "OBSIDIAN 1X2", new Sp(11, 24).Spike(5.5f, 24f, 4.5f, 6f, 0f, obs).Spike(2.5f, 24f, 2.5f, 1f, 13f, obs).Px(C("#B8A0E8"), 4, 8, 4, 9, 5, 14).Out()));
                    p.Add(P("medium", "FIRE BUSH", new Sp(13, 13).Ln(6, 12, 6, 6, C("#2A1E1A"), 2).Ln(6, 8, 2, 4, C("#2A1E1A")).Ln(7, 8, 11, 4, C("#2A1E1A")).Ell(2.5f, 3.5f, 2.3f, 2.5f, R("#FF8A20")).Ell(10.5f, 3.5f, 2.3f, 2.5f, R("#FF8A20")).Ell(6.5f, 3f, 2.6f, 3f, R("#FFC040")).Out()));
                    p.Add(P("medium", "SLAG HEAP", new Sp(15, 9).Dome(7.5f, 9f, 7.5f, 8.5f, R("#5A4A48")).Px(fire, 4, 5, 9, 3, 11, 6).Px(C("#B4BAC8"), 6, 4, 8, 6).Out()));
                    p.Add(P("medium", "STEAM VENT", Rows("a=#0C0808 b=#FF6A20 c=#FFC040 o=#4A3A38", ".oooooooo.|oaabccbaao|.oooooooo.")));
                    p.Add(P("large", "COOLED ROCK 2X2", Boulder(28, 22, basalt, fire)));
                    p.Add(P("large", "BASALT COLUMNS", new Sp(28, 30).Col(0, 12, 7, 18, basalt).Col(7, 4, 7, 26, basalt).Col(14, 0, 7, 30, basalt).Col(21, 9, 7, 21, basalt).Rect(1, 12, 5, 2, C("#6A5A56")).Rect(8, 4, 5, 2, C("#6A5A56")).Rect(15, 0, 5, 2, C("#6A5A56")).Rect(22, 9, 5, 2, C("#6A5A56")).Out()));
                    p.Add(P("landmark", "GIANT ANVIL", new Sp(48, 30).Box(12, 22, 24, 8, R("#4A4450")).Box(18, 12, 12, 11, R("#5A5460")).Box(4, 2, 40, 11, R("#6A6470")).Spike(4f, 7f, 5f, -6f, 6f, R("#6A6470")).Rect(8, 2, 34, 2, C("#A8A4B4")).Ln(22, 13, 26, 21, fire).Px(C("#FFC040"), 24, 16, 25, 18).Out()));
                    break;
                }
                case "lake":
                {
                    var reed = R("#7A9440"); var shell = R("#E8E0CC");
                    p.Add(P("cover", "WATER WEED", Tuft(R("#4A8A6A"), 7, 6, 5)));
                    p.Add(P("cover", "SHELL", new Sp(6, 4).Dome(3f, 4f, 3f, 4f, shell).Px(C("#B8A890"), 2, 2, 4, 2).Out()));
                    p.Add(P("cover", "MOSS", new Sp(8, 4).Ell(4f, 2.6f, 4f, 2f, R("#4A7A48")).Out(C("#16282A"))));
                    p.Add(P("medium", "REEDS 1X2", Reeds()));
                    p.Add(P("medium", "CORAL", new Sp(13, 12).Col(5, 4, 3, 8, R("#E87A7A")).Col(1, 2, 2, 7, R("#E87A7A")).Col(10, 3, 2, 6, R("#E87A7A")).Rect(2, 8, 4, 2, C("#C85A6A")).Rect(7, 8, 4, 2, C("#C85A6A")).Px(C("#FFC8C0"), 5, 3, 6, 3, 1, 1, 10, 2).Out()));
                    p.Add(P("medium", "PEARL CLAM", Clam()));
                    p.Add(P("medium", "LILY PAD", Rows("a=#5E9A4A b=#3E7038 c=#F0B8D0 o=#16282A", ".oooooo..|oaaaaaao.|oaaobbbbo|obbbbcco.|.oooooo..")));
                    p.Add(P("large", "WRECK 2X2", new Sp(30, 20).Ln(2, 18, 12, 4, Wood.Main, 3).Ln(10, 19, 18, 2, Wood.Shade, 3).Ln(18, 18, 27, 6, Wood.Main, 3).Box(0, 15, 30, 5, R("#6A4A30")).Speck(C("#4A7A48"), 0.2f, 6).Out()));
                    p.Add(P("large", "CAP ISLE 2X2", MushTree(24, 12, 6, 10, R("#3E9A8A"), R("#C8D8C0"), C("#B0F0E0"))));
                    p.Add(P("landmark", "SUNKEN SHIP", Ship()));
                    break;
                }
                case "abyss":
                {
                    var rock = R("#3A2C58"); var voidr = R("#A050FF"); var star = C("#E6D6FF");
                    p.Add(P("cover", "STAR DUST", Rows("a=#E6D6FF b=#A050FF", "a..b.|..a..|b...a")));
                    p.Add(P("cover", "DRIFT ROCK", new Sp(6, 4).Ell(3f, 2f, 3f, 2f, rock).Out()));
                    p.Add(P("cover", "VINE", Tuft(R("#6A3A9A"), 7, 7, 6, star)));
                    p.Add(P("medium", "VOID CRYSTAL", Crystals(12, 14, voidr, R("#5A2AB0"), 3, 7)));
                    p.Add(P("medium", "OBELISK", new Sp(9, 20).Spike(4.5f, 15f, 4f, 4.5f, 0f, rock).Spike(4.5f, 15f, 4f, 4.5f, 20f, R("#221830")).Px(voidr.Main, 4, 6, 4, 8, 4, 10).Out()));
                    p.Add(P("medium", "CHAIN STAKE", new Sp(11, 15).Col(4, 3, 3, 12, R("#4A4458")).Ell(5.5f, 2.5f, 2.5f, 2.5f, R("#6A6478")).Px(C("#8A84A0"), 8, 5, 9, 7, 10, 9, 10, 11).Out()));
                    p.Add(P("medium", "TWIST VINE", new Sp(11, 18).Ln(5, 17, 3, 11, C("#5A2A8A"), 2).Ln(3, 11, 7, 6, C("#6A3A9A"), 2).Ln(7, 6, 4, 1, C("#8A5AC0"), 2).Px(star, 4, 0, 9, 8, 1, 12).Out()));
                    p.Add(P("large", "FLOAT ROCK 2X2", FloatRock()));
                    p.Add(P("large", "BROKEN COLUMN", Pillar(16, 28, R("#4A3C6A"), true)));
                    p.Add(P("landmark", "BROKEN GATE", BrokenGate()));
                    break;
                }
            }
            return p;
        }

        static PixelImage Icefall()
        {
            var s = new Sp(26, 40);
            for (int x = 0; x < 26; x++)
            {
                int len = 30 + (int)(V(x / 2, 0, 81) * 10);
                var c = x % 5 == 0 ? C("#F0FAFF") : x % 5 == 3 ? C("#7AB0D0") : C("#B8DCF0");
                s.Rect(x, 0, 1, len, c);
            }
            return s.Out(C("#22364A"));
        }

        static PixelImage BeastInIce()
        {
            var s = new Sp(46, 40).Spike(23f, 40f, 23f, 20f, -2f, R("#A8D4EC")).Box(2, 30, 42, 10, R("#A8D4EC"));
            // The shadow of what is frozen inside.
            var beast = new Sp(46, 40).Ell(22f, 24f, 11f, 8f, R("#3A5A78")).Ell(30f, 18f, 5f, 5f, R("#3A5A78")).Rect(14, 28, 4, 8, C("#3A5A78")).Rect(26, 28, 4, 8, C("#3A5A78")).Ln(33, 20, 37, 27, C("#3A5A78"), 2);
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 46; x++)
                    if (beast.I.IsSet(x, y) && s.I.IsSet(x, y)) s.I[x, y] = (x + 2 * y) % 11 == 0 ? C("#7AA8C8") : C("#2E4A68");
            s.Px(C("#6FD0FF"), 31, 17, 32, 17).Ln(10, 30, 16, 8, C("#F0FAFF")).Ln(36, 12, 40, 30, C("#7AB0D0"));
            return s.Out(C("#22364A"));
        }

        // Two rib posts and the arch over them: only the posts' feet block the way.
        static PixelImage RibArch()
        {
            var s = new Sp(34, 30);
            for (int i = 0; i < 3; i++)
            {
                int o = i * 4;
                for (int a = 0; a <= 40; a++)
                {
                    float t = a / 40f * MathF.PI;
                    int x = (int)(17 - MathF.Cos(t) * (15 - o * 0.6f)), y = (int)(29 - MathF.Sin(t) * (27 - o * 1.6f)) ;
                    s.Rect(x, y, 2, 2, i == 0 ? Bone.Main : i == 1 ? Bone.Shade : Bone.Dark);
                    if (i == 0) s.Px(x, y, Bone.Light);
                }
            }
            return s.Out(C("#3A2818"));
        }

        static PixelImage Temple()
        {
            var stone = R("#C89C68"); var dark = R("#9A6E44");
            var s = new Sp(64, 46).Box(0, 8, 64, 38, stone).Box(2, 0, 60, 9, R("#D8B078"));
            for (int y = 14; y < 44; y += 5) s.Rect(1, y, 62, 1, dark.Main);
            s.Rect(24, 18, 16, 28, C("#1A100A")).Rect(26, 16, 12, 2, C("#1A100A")).Box(22, 14, 20, 3, dark);
            foreach (int x in new[] { 6, 14, 44, 52 }) s.Col(x, 10, 6, 36, stone).Box(x - 1, 9, 8, 3, R("#D8B078")).Box(x - 1, 43, 8, 3, dark);
            s.Rect(28, 3, 8, 3, C("#E0B040")).Px(C("#3A2818"), 31, 4, 32, 4).Px(C("#70E6D8"), 9, 20, 55, 20);
            // Sand drifted against it.
            s.Dome(8f, 46f, 12f, 6f, R("#A27E54")).Dome(58f, 46f, 10f, 5f, R("#A27E54"));
            return s.Out(C("#3A2818"));
        }

        static PixelImage Reeds()
        {
            var r = R("#7A9440");
            var s = new Sp(11, 24);
            foreach (var (x, top, lean) in new[] { (1, 6, -1), (3, 1, 0), (5, 4, 0), (7, 0, 1), (9, 7, 1) })
            {
                s.Ln(x + lean, top, x, 23, x % 4 == 1 ? r.Light : r.Main);
                if (top < 5) s.Rect(x + lean, top, 1, 4, C("#6A4A30"));
            }
            return s.Out(C("#16282A"));
        }

        public static PixelImage Clam(bool big = false)
        {
            var shell = R("#E8E0CC");
            int w = big ? 18 : 13, h = big ? 14 : 10;
            var s = new Sp(w, h).Ell(w / 2f, h - 3f, w / 2f, 3f, shell).Dome(w / 2f, h - 5f, w / 2f, h - 5f, shell);
            for (int x = 2; x < w - 1; x += 3) s.Ln(x, h - 6, x + (x < w / 2 ? 1 : -1), 2, shell.Shade);
            s.Rect(2, h - 5, w - 4, 2, C("#3A2030")).Ell(w / 2f, h - 4f, 1.6f, 1.6f, R("#FFE8F4"));
            return s.Out();
        }

        static PixelImage Ship()
        {
            var hull = R("#6A4A30");
            var s = new Sp(62, 44);
            s.Ln(30, 30, 40, 0, Wood.Main, 3).Ln(34, 10, 48, 14, Wood.Shade, 2).Rect(41, 2, 9, 7, C("#C8C0A8")).Rect(41, 9, 7, 3, C("#A8A088"));
            s.Dome(31f, 44f, 31f, 16f, hull);
            for (int x = 6; x < 58; x += 7) s.Ln(x, 43, x + (x < 31 ? 3 : -3), 29, hull.Dark);
            s.Rect(4, 30, 54, 2, hull.Light).Speck(C("#4A7A48"), 0.18f, 7, 28, 44).Rect(20, 36, 6, 5, C("#10262C"));
            return s.Out();
        }

        static PixelImage FloatRock()
        {
            var rock = R("#3A2C58");
            var s = new Sp(28, 24).Ell(14f, 9f, 14f, 9f, rock).Spike(14f, 10f, 12f, 12f, 24f, R("#221830"));
            s.Rect(3, 4, 22, 2, C("#5A4A80")).Px(C("#E6D6FF"), 8, 8, 19, 6, 13, 14).Ln(10, 10, 14, 16, C("#A050FF"));
            return s.Out();
        }

        static PixelImage BrokenGate()
        {
            var stone = R("#4A3C6A");
            var s = new Sp(56, 52);
            // The ring's remains: two arcs with a gap at the top, and the void between.
            for (int a = 0; a < 360; a += 2)
            {
                if (a > 70 && a < 112 || a > 200 && a < 215) continue;
                float t = a * MathF.PI / 180f;
                int x = (int)(28 + MathF.Cos(t) * 22), y = (int)(26 - MathF.Sin(t) * 22);
                s.Rect(x - 2, y - 2, 5, 5, MathF.Cos(t) < -0.3f || MathF.Sin(t) > 0.6f ? stone.Light : MathF.Sin(t) < -0.5f ? stone.Dark : stone.Main);
            }
            for (int y = 8; y < 46; y++)
                for (int x = 8; x < 48; x++)
                {
                    float d = MathF.Sqrt((x - 28) * (x - 28) + (y - 26) * (y - 26));
                    if (d < 18 && !s.I.IsSet(x, y) && V(x, y, 82) < 0.5f - d / 45f) s.I[x, y] = d < 7 ? C("#E6D6FF") : d < 12 ? C("#A050FF") : C("#5A2AB0");
                }
            s.Box(2, 46, 52, 6, stone).Px(C("#A050FF"), 12, 20, 44, 30, 28, 4);
            return s.Out();
        }

        // ---- Small creatures (no health bar, low contrast next to monsters) ----

        public static List<Prop> Critters(string id) => id switch
        {
            "cave" => new List<Prop> { P("critter", "CRICKET", Rows("a=#6A5A3A b=#2A2018", "..b.|aaab|b.b.")), P("critter", "MOUSE", Rows("a=#9A8A7A b=#E0A0A0 c=#2A2018", "....a.|.aaaac|baaaa.")), P("critter", "MOTH", Rows("a=#D8C8A0 b=#8A7A5A", "a.a|aba|.b.")) },
            "mushroom" => new List<Prop> { P("critter", "FIREFLY", Rows("a=#E8FF90 b=#B8F25A", ".b.|bab|.b.")), P("critter", "SNAIL", Rows("a=#C8A070 b=#8A6A40 c=#B8F25A", "..bb.|.baab|cccc.")), P("critter", "MOTH", Rows("a=#C8E890 b=#6A8A4A", "a.a|aba|.b.")) },
            "ice" => new List<Prop> { P("critter", "HARE", Rows("a=#F0F6FA b=#B8C8D4 c=#2A3440", ".a....|.a.aa.|aaaaac|baab..")), P("critter", "ICE MOTH", Rows("a=#D8F4FF b=#8AB8D0", "a.a|aba|.b.")) },
            "crystal" => new List<Prop> { P("critter", "SNAIL", Rows("a=#7FF2FF b=#FF7FD0 c=#8A90A0", "..ab.|.abba|cccc.")), P("critter", "MOTH", Rows("a=#FFB0E8 b=#C050A0", "a.a|aba|.b.")) },
            "sand" => new List<Prop> { P("critter", "LIZARD", Rows("a=#B8A060 b=#6A5430", "a....a.|.aaaaab|.b..b..")), P("critter", "DUNG BEETLE", Rows("a=#3A3040 b=#8A6A40", ".bb.aa|bbbbaa|.bb.a.")) },
            "lava" => new List<Prop> { P("critter", "NEWT", Rows("a=#E06A30 b=#FFC040", "a...a.|.abaab|.a..a.")), P("critter", "FIRE MOTH", Rows("a=#FFC040 b=#E05A20", "a.a|aba|.b.")) },
            "lake" => new List<Prop> { P("critter", "FISH SHADOW", Rows("a=#123840", ".aaa..a|aaaaaaa|.aaa..a")), P("critter", "FROG", Rows("a=#6AA84A b=#2A3A20", "b..b|aaaa|a..a")), P("critter", "FIREFLY", Rows("a=#E8FFD0 b=#70E6D8", ".b.|bab|.b.")) },
            _ => new List<Prop> { P("critter", "LIGHT JELLY", Rows("a=#E6D6FF b=#A050FF", ".aaa.|aaaaa|b.b.b|b...b")), P("critter", "SHADE MOTH", Rows("a=#5A3A9A b=#2A1A4A", "a.a|aba|.b.")) },
        };

        // ---- Monsters ----

        public static List<Mon> Roster(string id)
        {
            var hot = C("#FFE060");
            switch (id)
            {
                case "cave":
                    return new List<Mon>
                    {
                        M("SLIME", "M", 0, Old("slime")), M("SPORE SHOOTER", "S", 0, Old("spore_shooter")), M("BEETLE", "M", 0, Old("cave_beetle")),
                        M("CAVE RAT", "XS", 1, new Sp(10, 6).Ln(0, 4, 2, 3, C("#C09A8A")).Ell(5f, 3.6f, 3.4f, 2.3f, R("#B4A8A0")).Ell(8f, 3.4f, 1.9f, 1.7f, R("#B4A8A0")).Px(C("#F0A0A0"), 7, 1).Px(C("#FF4040"), 8, 3).Out()),
                        M("BIG BEETLE", "L", 1, new Sp(31, 22).Ln(6, 17, 3, 21, Ink, 2).Ln(14, 18, 13, 21, Ink, 2).Ln(21, 17, 23, 21, Ink, 2).Ell(25f, 13f, 4.5f, 4.5f, R("#4A3220")).Spike(26f, 11f, 2f, 30f, 3f, R("#D8C8A0")).Ell(13.5f, 10f, 13f, 9.5f, R("#8A5A30")).Ln(14, 1, 12, 18, C("#4A2E18")).Px(C("#D8A868"), 6, 5, 7, 4, 8, 4, 17, 4).Px(hot, 26, 12).Out()),
                        M("MOLE", "S", 2, new Sp(13, 10).Dome(6.5f, 10f, 6.5f, 5f, R("#6A4E38")).Ell(6.5f, 4.5f, 3.5f, 3.2f, R("#5A5060")).Px(C("#F0A0B0"), 6, 5, 7, 5).Px(C("#EDE3C8"), 2, 7, 3, 7, 10, 7, 9, 7).Px(Ink, 5, 3, 8, 3).Out()),
                        M("IRON BEETLE", "M", 3, Old("cave_beetle", "#8A8E9E")),
                        M("SLIME MOTHER", "XL", 4, SlimeMother()),
                    };
                case "mushroom":
                    return new List<Mon>
                    {
                        M("MYCONID", "M", 0, Old("myconid")), M("GLOW BAT", "S", 0, Old("glow_bat"), 5),
                        M("PUFFBALL", "S", 1, new Sp(12, 12).Ell(6f, 5.6f, 5.6f, 5.2f, R("#D8D0A0")).Px(C("#8A8450"), 3, 3, 8, 2, 9, 6, 2, 7).Px(C("#3A3A1A"), 5, 0, 6, 0).Px(Ink, 4, 6, 8, 6, 3, 11, 8, 11).Px(C("#B8F25A"), 5, 2, 6, 3).Out()),
                        M("CAP GIANT", "L", 1, CapGiant()),
                        M("LURKER CAP", "S", 2, new Sp(12, 11).Col(4, 7, 4, 4, R("#D8D0B0")).Dome(6f, 7f, 6f, 6f, R("#B07848")).Rect(2, 6, 8, 2, C("#1A1410")).Px(C("#B8F25A"), 4, 6, 8, 6).Px(C("#E8DCC0"), 3, 2, 8, 3).Out()),
                        M("SHAMAN", "M", 2, new Sp(17, 19).Ln(15, 4, 15, 18, C("#6A5238")).Ell(15f, 2.5f, 2f, 2f, R("#B8F25A")).Col(5, 9, 7, 10, R("#D8D0B0")).Dome(8.5f, 10f, 7.5f, 8f, R("#E88AB0")).Rect(3, 9, 11, 1, C("#A04A70")).Px(Ink, 7, 12, 10, 12).Px(C("#FFD8E8"), 5, 4, 10, 3, 8, 6).Out()),
                        M("GLOW SHOOTER", "S", 3, Old("spore_shooter", "#A8E05A")), M("MYCONID CHIEF", "M", 3, Old("myconid", "#E88AB0")),
                        M("MYCEL. MOTHER", "XL", 4, MycelMother()),
                    };
                case "ice":
                    return new List<Mon>
                    {
                        M("ICE SLIME", "M", 0, Old("ice_slime")), M("FROST WISP", "S", 0, Old("frost_wisp"), 4),
                        M("FROST WOLF", "M", 1, new Sp(19, 13).Ln(1, 7, 0, 3, C("#C8DCEC"), 2).Rect(3, 9, 2, 4, C("#7A94AC")).Rect(6, 9, 2, 4, C("#9AB4C8")).Rect(10, 9, 2, 4, C("#7A94AC")).Rect(13, 9, 2, 4, C("#9AB4C8")).Ell(8.5f, 7f, 6.5f, 3.6f, R("#C8DCEC")).Ell(15f, 5f, 3.2f, 3f, R("#C8DCEC")).Rect(17, 6, 2, 1, C("#F0FAFF")).Px(C("#F0FAFF"), 13, 1, 16, 1, 5, 3, 8, 3, 11, 3).Px(C("#30A0E0"), 16, 4).Px(Ink, 18, 6).Out()),
                        M("YETI", "L", 1, Yeti()),
                        M("ICICLE LURKER", "S", 2, new Sp(8, 15).Spike(4f, 0f, 3.6f, 4f, 15f, R("#A8D4EC")).Px(Ink, 2, 3, 5, 3).Px(C("#F0FAFF"), 2, 1, 3, 6).Out(), 8),
                        M("FROST BEETLE", "M", 3, Old("cave_beetle", "#9AC8E0")), M("WISP LORD", "S", 3, Old("frost_wisp", "#F4FAFF"), 4),
                        M("FROST MAMMOTH", "XL", 4, Mammoth()),
                    };
                case "crystal":
                    return new List<Mon>
                    {
                        M("CRYSTAL BAT", "S", 0, Old("crystal_bat"), 5), M("CRYSTAL WISP", "S", 0, Old("crystal_wisp"), 4),
                        M("MIMIC", "M", 1, Mimic()),
                        M("TORTOISE", "L", 1, new Sp(32, 24).Rect(4, 19, 5, 5, C("#5A6878")).Rect(19, 19, 5, 5, C("#5A6878")).Ell(27f, 17f, 4.5f, 3.8f, R("#8A9AAA")).Dome(14f, 20f, 13.5f, 12f, R("#5A6478")).Spike(7f, 13f, 2.5f, 4f, 3f, R("#7FF2FF")).Spike(13f, 10f, 3f, 13f, 0f, R("#FF7FD0")).Spike(20f, 12f, 2.5f, 23f, 2f, R("#7FF2FF")).Spike(16f, 14f, 2f, 18f, 6f, R("#F4F0FF")).Px(Ink, 29, 16).Ln(3, 19, 25, 19, C("#3A4050")).Out()),
                        M("SHARD MITE", "XS", 2, new Sp(8, 7).Spike(4f, 7f, 3f, 4f, 0f, R("#7FF2FF")).Px(Ink, 0, 6, 7, 6, 1, 4, 6, 4).Out()),
                        M("CRYSTAL SHOOTER", "S", 3, Old("spore_shooter", "#FF7FD0")), M("PRISM WISP", "S", 3, Old("crystal_wisp", "#FFB0E8"), 4),
                        M("GEODE GOLEM", "XL", 4, GeodeGolem()),
                    };
                case "sand":
                    return new List<Mon>
                    {
                        M("SCORPION", "M", 0, Old("sand_scorpion")), M("SAND WORM", "S", 0, Old("sand_worm")),
                        M("BOMB SCARAB", "S", 1, new Sp(13, 10).Px(Ink, 2, 9, 5, 9, 9, 9).Ell(11f, 6f, 2f, 2.2f, R("#3A3044")).Ell(6f, 5.2f, 5.6f, 4.6f, R("#3A3044")).Ell(4f, 5.5f, 2.6f, 2.6f, R("#FF9A30")).Px(hot, 3, 5).Px(C("#8A80A0"), 7, 2, 8, 2).Out()),
                        M("GREAT WORM", "L", 1, GreatWorm()),
                        M("SAND LIZARD", "XS", 2, new Sp(10, 5).Ln(0, 3, 2, 3, C("#D8B878")).Ell(5f, 2.6f, 3.2f, 1.7f, R("#D8B878")).Ell(8.2f, 2.2f, 1.7f, 1.5f, R("#D8B878")).Px(Ink, 9, 2, 3, 4, 6, 4).Out()),
                        M("TOMB KEEPER", "M", 2, new Sp(15, 20).Ln(13, 2, 13, 19, C("#E0B040")).Ell(13f, 2.5f, 1.8f, 2f, R("#E0B040")).Col(3, 8, 8, 12, R("#C8B890")).Ell(7f, 5f, 3.6f, 3.6f, R("#D8CCA8")).Rect(3, 0, 8, 2, C("#3A6AA0")).Rect(4, 1, 6, 1, C("#E0B040")).Px(C("#70E6D8"), 5, 5, 8, 5).Rect(3, 11, 8, 1, C("#9A8A68")).Rect(3, 15, 8, 1, C("#9A8A68")).Out()),
                        M("SAND BEETLE", "M", 3, Old("cave_beetle", "#D2B070")), M("SCORPION QUEEN", "M", 3, Old("sand_scorpion", "#C84A3A")),
                        M("TEMPLE GUARD", "XL", 4, Guardian()),
                    };
                case "lava":
                    return new List<Mon>
                    {
                        M("MAGMA SLIME", "M", 0, Old("magma_slime")), M("FIRE BAT", "S", 0, Old("fire_bat"), 5),
                        M("SALAMANDER", "M", 1, new Sp(21, 10).Ln(0, 7, 4, 6, C("#C8421E"), 2).Rect(5, 8, 2, 2, C("#8A2A14")).Rect(12, 8, 2, 2, C("#8A2A14")).Ell(9.5f, 6f, 7f, 2.8f, R("#D84A20")).Ell(17f, 5.5f, 3.2f, 2.6f, R("#D84A20")).Px(C("#FFC040"), 4, 3, 6, 2, 8, 3, 10, 2, 12, 3, 14, 2).Px(C("#FF8A20"), 5, 3, 7, 3, 9, 3, 11, 3, 13, 3).Px(hot, 18, 5).Out()),
                        M("MAGMA HURLER", "L", 1, Hurler()),
                        M("CINDER GOLEM", "M", 2, new Sp(16, 15).Ell(8f, 8.5f, 7.6f, 7f, R("#3E3230")).Ln(4, 4, 7, 8, C("#FF6A20")).Ln(7, 8, 5, 12, C("#FF6A20")).Ln(11, 5, 10, 10, C("#FF6A20")).Px(hot, 6, 7, 10, 7).Out()),
                        M("EMBER MITE", "XS", 2, new Sp(8, 6).Ell(4f, 3.4f, 3.2f, 2.3f, R("#5A2A1A")).Px(C("#FF8A20"), 3, 2, 4, 2, 4, 3).Px(Ink, 0, 5, 7, 5, 2, 5, 5, 5).Out()),
                        M("OBSID. BEETLE", "M", 3, Old("cave_beetle", "#4A3A6A")), M("FORGE SLIME", "M", 3, Old("magma_slime", "#FFB030")),
                        M("FORGE KEEPER", "XL", 4, ForgeKeeper()),
                    };
                case "lake":
                    return new List<Mon>
                    {
                        M("LAKE CRAB", "M", 0, Old("lake_crab")), M("TIDE WISP", "S", 0, Old("tide_wisp"), 4),
                        M("TENTACLE", "M", 1, Tentacle()),
                        M("GREAT CRAB", "L", 1, new Sp(34, 21).Ln(8, 15, 3, 20, Ink, 2).Ln(11, 17, 8, 20, Ink, 2).Ln(25, 15, 30, 20, Ink, 2).Ln(22, 17, 25, 20, Ink, 2).Ln(14, 6, 14, 2, C("#3A8AA0")).Ln(20, 6, 20, 2, C("#3A8AA0")).Px(Rgba.White, 14, 1, 20, 1).Ell(17f, 12f, 11f, 7f, R("#3A8AA0")).Ell(5f, 8f, 5f, 5f, R("#58B0C0")).Ell(29f, 8f, 5f, 5f, R("#58B0C0")).Ln(2, 7, 6, 9, Ink).Ln(32, 7, 28, 9, Ink).Px(C("#8AD8E0"), 12, 8, 13, 7, 14, 7).Ln(12, 15, 22, 15, C("#1E5A6A")).Out()),
                        M("FROG", "XS", 2, new Sp(8, 6).Ell(4f, 3.6f, 3.6f, 2.4f, R("#6AA84A")).Px(C("#E8FFD0"), 2, 1, 5, 1).Px(Ink, 2, 2, 5, 2, 0, 5, 7, 5).Out()),
                        M("GIANT CLAM", "M", 2, Clam(true)),
                        M("MUD SLIME", "M", 3, Old("slime", "#8A7450")), M("TIDE LEADER", "S", 3, Old("tide_wisp", "#B0FFF0"), 4),
                        M("MOSS TURTLE", "XL", 4, MossTurtle()),
                    };
                default:
                    var stalker = new Sp(19, 17).Ln(5, 12, 4, 16, C("#2A1E48"), 2).Ln(10, 13, 11, 16, C("#2A1E48"), 2).Ell(8f, 9f, 6.5f, 5f, R("#3A2A6A")).Ell(14.5f, 5f, 3.6f, 3.2f, R("#3A2A6A")).Spike(5f, 6f, 1.5f, 3f, 0f, R("#A050FF")).Spike(8f, 5f, 1.5f, 8f, 0f, R("#A050FF")).Spike(11f, 5f, 1.5f, 12f, 1f, R("#A050FF")).Ln(11, 10, 16, 14, C("#3A2A6A"), 2).Px(C("#E6D6FF"), 17, 15, 18, 16, 16, 16, 14, 4, 16, 4).Out();
                    return new List<Mon>
                    {
                        M("VOID WISP", "S", 0, Old("void_wisp"), 4), M("STALKER", "M", 5, stalker),
                        M("VOID EYE", "M", 1, new Sp(16, 20).Ln(5, 14, 3, 19, C("#4A2A8A")).Ln(8, 15, 8, 19, C("#4A2A8A")).Ln(11, 14, 13, 19, C("#4A2A8A")).Ell(8f, 8f, 7.6f, 7.6f, R("#3A2A6A")).Ell(8.5f, 8.5f, 4.8f, 4.8f, R("#E6D6FF")).Ell(9f, 9f, 2.6f, 2.6f, R("#A050FF")).Rect(9, 8, 1, 3, Ink).Out(), 5),
                        M("SPLITTER", "L", 1, new Sp(31, 25).Ell(10f, 14f, 9.8f, 10.5f, R("#4A2A8A")).Ell(21f, 14f, 9.8f, 10.5f, R("#5A3AA0")).Ln(15, 4, 17, 10, C("#E6D6FF")).Ln(17, 10, 14, 16, C("#E6D6FF")).Ln(14, 16, 16, 24, C("#E6D6FF")).Eyes(7, 11, 16, C("#E6D6FF"), 2).Px(Ink, 8, 12, 24, 12).Out()),
                        M("VOID LARVA", "XS", 2, new Sp(10, 6).Ell(2.2f, 4f, 2.2f, 2f, R("#6A4AB0")).Ell(5f, 3.4f, 2.4f, 2.4f, R("#6A4AB0")).Ell(8f, 3f, 2f, 2.2f, R("#7A5AC8")).Px(C("#E6D6FF"), 8, 2).Out()),
                        M("NULL PRIEST", "M", 2, Priest(), 3),
                        M("STAR SLIME", "M", 3, Old("slime", "#6A4AC0")), M("ELITE STALKER", "M", 3, Tinted(stalker, C("#FF5080"), 0.35f)),
                        M("GATE WARDEN", "XL", 4, Warden()),
                    };
            }
        }

        static PixelImage Priest()
        {
            var s = new Sp(15, 22);
            s.I.Ring(7, 3, 3.5f, C("#A050FF"));
            s.Spike(7.5f, 22f, 6.5f, 7.5f, 5f, R("#2A2040")).Ell(7.5f, 8f, 4f, 4f, R("#3A2C58")).Ell(7.5f, 8.8f, 2.3f, 2.6f, R("#05030A")).Px(C("#A050FF"), 2, 15, 12, 15).Px(C("#E6D6FF"), 7, 9);
            return s.Out();
        }

        static PixelImage SlimeMother()
        {
            var green = R("#8CD94D");
            var s = new Sp(50, 38).Ell(25f, 24f, 24.5f, 17f, green).Ell(25f, 28f, 13f, 7f, R("#5EA838"));
            s.Ell(13f, 8f, 5f, 4f, R("#A8E86A")).Ell(25f, 5f, 6f, 5f, R("#A8E86A")).Ell(37f, 8f, 5f, 4f, R("#A8E86A"));
            s.Rect(15, 17, 5, 7, Ink).Rect(30, 17, 5, 7, Ink).Px(Rgba.White, 16, 18, 17, 18, 31, 18, 32, 18).Ln(21, 28, 29, 28, Ink).Px(Ink, 20, 27, 30, 27);
            s.Px(C("#E0FFB0"), 8, 20, 9, 19, 41, 26, 6, 28, 38, 14).Px(Ink, 12, 8, 14, 8, 24, 5, 27, 5, 36, 8, 38, 8);
            return s.Out();
        }

        static PixelImage CapGiant()
        {
            var body = R("#D8D0B0"); var cap = R("#3E7A5A");
            var s = new Sp(32, 31).Col(2, 17, 5, 10, body).Col(25, 17, 5, 10, body).Col(9, 14, 14, 15, body).Rect(9, 29, 6, 2, body.Shade).Rect(17, 29, 6, 2, body.Shade);
            s.Dome(16f, 16f, 16f, 15f, cap).Rect(3, 15, 26, 1, C("#1E3A2A"));
            s.Rect(7, 5, 3, 2, C("#B8F25A")).Rect(15, 3, 3, 3, C("#B8F25A")).Rect(22, 7, 3, 2, C("#B8F25A")).Rect(11, 10, 2, 2, C("#B8F25A")).Eyes(12, 19, 6, C("#B8F25A"), 2).Rect(13, 24, 6, 1, Ink);
            return s.Out();
        }

        static PixelImage MycelMother()
        {
            var cap = R("#5A8A4A"); var flesh = R("#CFC6A0");
            var s = new Sp(56, 42);
            foreach (var (x0, x1) in new[] { (18, 3), (22, 12), (28, 28), (34, 44), (38, 53) }) s.Ln(x0, 34, x1, 41, flesh.Shade, 2).Ln(x0, 33, x1, 40, flesh.Main);
            s.Ell(28f, 30f, 15f, 9f, flesh).Dome(28f, 25f, 28f, 22f, cap).Rect(4, 24, 48, 1, C("#A04A70")).Rect(8, 25, 40, 1, C("#E88AB0"));
            foreach (var (x, y) in new[] { (10, 12), (18, 6), (28, 3), (38, 7), (46, 13), (24, 13), (34, 15) }) s.Rect(x, y, 3, 3, C("#B8F25A")).Px(C("#E8FFB0"), x, y);
            foreach (int x in new[] { 18, 23, 31, 36 }) s.Rect(x, 29, 2, 2, C("#B8F25A"));
            s.Ln(22, 35, 34, 35, Ink).Px(Ink, 21, 34, 35, 34);
            return s.Out();
        }

        static PixelImage Yeti()
        {
            var fur = R("#E4EEF6");
            var s = new Sp(30, 31).Rect(8, 27, 5, 4, fur.Shade).Rect(17, 27, 5, 4, fur.Shade).Col(1, 12, 6, 14, fur).Col(23, 12, 6, 14, fur).Ell(4f, 26f, 3.5f, 3f, R("#B8CCDC")).Ell(26f, 26f, 3.5f, 3f, R("#B8CCDC"));
            s.Ell(15f, 18f, 10.5f, 11f, fur).Ell(15f, 7f, 6.5f, 6f, fur).Ell(15f, 8.5f, 4f, 3.2f, R("#5A7A9A")).Px(Rgba.White, 13, 8, 16, 8).Rect(13, 10, 5, 1, Ink).Px(Rgba.White, 13, 10, 17, 10);
            s.Speck(fur.Shade, 0.08f, 9, 12, 28);
            return s.Out(C("#3A5068"));
        }

        static PixelImage Mammoth()
        {
            var fur = R("#8AA4BC"); var tusk = C("#F0E8D0");
            var s = new Sp(58, 44).Col(6, 30, 8, 14, R("#6A84A0")).Col(32, 31, 8, 13, R("#6A84A0")).Col(16, 32, 8, 12, fur).Col(40, 30, 8, 14, fur);
            s.Ell(26f, 21f, 22f, 15f, fur).Dome(26f, 12f, 17f, 6f, R("#E8F4FC")).Ell(45f, 19f, 9.5f, 11f, fur).Ell(39f, 17f, 4f, 7f, R("#6A84A0"));
            s.Col(49, 22, 5, 16, fur).Rect(50, 38, 5, 3, fur.Shade).Ln(44, 27, 50, 36, tusk, 2).Ln(50, 36, 56, 31, tusk, 2).Px(C("#6FD0FF"), 47, 15, 48, 15).Speck(fur.Shade, 0.1f, 10, 14, 34).Px(C("#DCEFFA"), 12, 34, 20, 36, 30, 36, 13, 35);
            return s.Out();
        }

        static PixelImage Mimic()
        {
            var s = new Sp(16, 16).Spike(4f, 12f, 2.6f, 1f, 2f, R("#7FF2FF")).Spike(12f, 12f, 2.6f, 15f, 3f, R("#7FF2FF")).Spike(8f, 12f, 3.5f, 8f, 0f, R("#FF7FD0"));
            s.Box(1, 11, 14, 5, R("#5A6074")).Rect(2, 12, 12, 2, C("#2A0E20")).Px(C("#F4F0FF"), 3, 12, 5, 13, 7, 12, 9, 13, 11, 12, 13, 13).Px(C("#FFE060"), 7, 6, 9, 6);
            return s.Out();
        }

        static PixelImage GeodeGolem()
        {
            var rock = R("#565C70");
            var s = new Sp(50, 48).Col(14, 36, 9, 12, rock).Col(27, 36, 9, 12, rock).Ell(6f, 27f, 6f, 10f, rock).Ell(44f, 27f, 6f, 10f, rock).Ell(6f, 36f, 5f, 4f, R("#6A7084")).Ell(44f, 36f, 5f, 4f, R("#6A7084"));
            s.Ell(25f, 24f, 17.5f, 15.5f, rock).Ell(25f, 9f, 6.5f, 5.5f, rock).Ell(25f, 25f, 8f, 10f, R("#C050A0")).Ell(25f, 25f, 5f, 7f, R("#FFB8E8")).Spike(25f, 32f, 2f, 25f, 20f, R("#F4F0FF"));
            s.Spike(10f, 16f, 3f, 6f, 4f, R("#7FF2FF")).Spike(40f, 16f, 3f, 44f, 4f, R("#7FF2FF")).Spike(15f, 12f, 2f, 14f, 4f, R("#FF7FD0")).Spike(35f, 12f, 2f, 36f, 4f, R("#FF7FD0")).Eyes(22, 8, 5, C("#7FF2FF"), 2);
            return s.Out();
        }

        static PixelImage GreatWorm()
        {
            var skin = R("#C8965A");
            var s = new Sp(28, 36).Col(9, 6, 11, 26, skin).Ell(14.5f, 7f, 6.5f, 6.5f, skin).Ell(14.5f, 5.5f, 4.5f, 3.5f, R("#5A1E1E"));
            for (int y = 13; y < 30; y += 4) s.Rect(9, y, 11, 1, skin.Shade);
            s.Px(C("#EDE3C8"), 10, 4, 12, 3, 14, 2, 16, 3, 18, 4, 11, 7, 14, 8, 17, 7).Dome(14f, 36f, 14f, 7f, R("#B98C5A")).Px(C("#EDD8A8"), 4, 32, 22, 33, 8, 31);
            return s.Out(C("#3A2818"));
        }

        static PixelImage Guardian()
        {
            var stone = R("#B98C5A"); var gold = R("#E0B040");
            var s = new Sp(48, 54).Box(8, 46, 32, 8, R("#9A6E44")).Box(12, 34, 10, 13, stone).Box(26, 34, 10, 13, stone).Box(2, 18, 7, 20, stone).Box(39, 18, 7, 20, stone).Box(9, 17, 30, 18, stone);
            s.Box(13, 5, 4, 11, R("#3A6AA0")).Box(31, 5, 4, 11, R("#3A6AA0")).Box(17, 5, 14, 12, stone).Box(14, 0, 20, 6, gold).Rect(20, 9, 3, 2, C("#70E6D8")).Rect(26, 9, 3, 2, C("#70E6D8")).Rect(22, 14, 4, 1, C("#5A3C24"));
            s.Box(19, 21, 10, 8, gold).Px(C("#3A6AA0"), 23, 24, 24, 24, 23, 25, 24, 25).Ln(11, 30, 16, 26, C("#5A3C24")).Ln(34, 19, 37, 24, C("#5A3C24")).Rect(2, 36, 7, 2, C("#9A6E44")).Rect(39, 36, 7, 2, C("#9A6E44"));
            return s.Out(C("#3A2818"));
        }

        static PixelImage Hurler()
        {
            var rock = R("#4A3A38");
            var s = new Sp(32, 32).Box(8, 26, 6, 6, rock).Box(17, 26, 6, 6, rock).Col(1, 15, 6, 12, rock).Col(24, 6, 6, 14, rock).Ell(27f, 4.5f, 4.5f, 4.5f, R("#FF8A20")).Px(C("#FFE060"), 26, 3, 27, 3);
            s.Ell(15f, 19f, 11f, 10f, rock).Ell(15f, 8f, 5.5f, 5f, rock).Ln(9, 15, 13, 20, C("#FF6A20")).Ln(13, 20, 11, 26, C("#FF6A20")).Ln(19, 13, 21, 22, C("#FF6A20")).Eyes(13, 7, 4, C("#FFC040")).Rect(13, 10, 5, 1, C("#FF6A20"));
            return s.Out();
        }

        static PixelImage ForgeKeeper()
        {
            var iron = R("#5A4C4A"); var hot = C("#FF6A20");
            var s = new Sp(50, 54).Ln(44, 36, 44, 4, C("#6A5238"), 2).Box(37, 0, 13, 9, R("#8A8490")).Col(12, 40, 9, 14, iron).Col(25, 40, 9, 14, iron).Col(2, 18, 7, 20, iron).Col(37, 18, 7, 18, iron).Ell(5.5f, 39f, 4.5f, 4f, R("#6A5C5A")).Ell(41f, 36f, 4.5f, 4f, R("#6A5C5A"));
            s.Box(8, 16, 30, 25, iron).Ell(8f, 17f, 6f, 5f, R("#6A5C5A")).Ell(38f, 17f, 6f, 5f, R("#6A5C5A")).Box(15, 3, 16, 14, iron).Rect(18, 8, 10, 2, hot).Px(C("#FFE060"), 20, 8, 25, 8);
            s.Rect(14, 23, 18, 12, C("#2A1A14")).Rect(15, 24, 16, 10, hot).Rect(17, 27, 12, 6, C("#FFC040")).Rect(19, 24, 1, 10, C("#2A1A14")).Rect(23, 24, 1, 10, C("#2A1A14")).Rect(27, 24, 1, 10, C("#2A1A14")).Box(19, 0, 4, 4, R("#3A2E2C"));
            return s.Out();
        }

        static PixelImage Tentacle()
        {
            var flesh = R("#B85A8A");
            var s = new Sp(14, 22).Ell(7f, 20f, 7f, 2f, R("#4FD2C0"));
            for (int i = 0; i <= 15; i++)
            {
                float t = i / 15f;
                s.Ell(6.5f + MathF.Sin(t * 3.4f) * 3f, 19f - t * 16f, 2.8f - t * 1.5f, 2.8f - t * 1.5f, flesh);
            }
            s.Px(C("#FFD0E0"), 8, 15, 9, 12, 9, 9, 8, 6);
            return s.Out();
        }

        static PixelImage MossTurtle()
        {
            var shell = R("#4A6A48"); var skin = R("#6A8A6A");
            var s = new Sp(58, 42).Ell(8f, 35f, 7.5f, 4.5f, skin).Ell(40f, 36f, 7.5f, 4.5f, skin).Ell(49f, 27f, 7.5f, 6.5f, skin).Px(Ink, 52, 25).Px(Rgba.White, 51, 25).Ln(49, 31, 55, 31, C("#3A5A3A"));
            s.Dome(26f, 34f, 24.5f, 24f, shell).Rect(3, 33, 46, 2, C("#8A7A50")).Ln(14, 14, 20, 32, shell.Dark).Ln(26, 10, 26, 32, shell.Dark).Ln(38, 14, 32, 32, shell.Dark).Ln(6, 24, 46, 24, shell.Dark);
            s.Ell(16f, 12f, 6f, 3f, R("#7AAA4A")).Ell(30f, 9f, 7f, 3f, R("#7AAA4A")).Ell(40f, 16f, 4f, 2.5f, R("#7AAA4A"));
            foreach (var (x, top) in new[] { (20, 0), (23, 2), (26, 0), (36, 3), (38, 1) }) s.Ln(x, top, x, 10, C("#7A9440")).Rect(x, top, 1, 3, C("#6A4A30"));
            s.Col(11, 5, 2, 5, R("#C8D8C0")).Dome(12f, 6f, 3.5f, 3.5f, R("#3E9A8A"));
            return s.Out();
        }

        static PixelImage Warden()
        {
            var plate = R("#3A2C58"); var dark = R("#2A2040"); var glow = C("#A050FF"); var star = C("#E6D6FF");
            var s = new Sp(52, 56);
            for (int a = 0; a < 360; a += 3)
            {
                if (a > 250 && a < 290 || a > 20 && a < 40 || a > 140 && a < 155) continue;
                float t = a * MathF.PI / 180f;
                s.Rect((int)(26 + MathF.Cos(t) * 23) - 1, (int)(22 - MathF.Sin(t) * 21) - 1, 3, 3, a % 9 == 0 ? star : glow);
            }
            s.Col(16, 40, 8, 16, dark).Col(28, 40, 8, 16, dark).Col(5, 22, 7, 22, plate).Col(40, 22, 7, 22, plate).Spike(26f, 44f, 14f, 26f, 14f, dark).Box(13, 20, 26, 20, plate);
            s.Spike(13f, 22f, 4f, 6f, 12f, plate).Spike(39f, 22f, 4f, 46f, 12f, plate).Ell(26f, 13f, 6f, 7.5f, plate).Rect(22, 12, 8, 2, star).Spike(26f, 8f, 3f, 26f, 0f, R("#A050FF"));
            s.Ell(26f, 29f, 4.5f, 4.5f, R("#A050FF")).Px(star, 25, 28, 26, 28).Ell(8.5f, 45f, 4f, 3.5f, R("#4A3C6A")).Ell(43.5f, 45f, 4f, 3.5f, R("#4A3C6A"));
            return s.Out();
        }
    }
}
