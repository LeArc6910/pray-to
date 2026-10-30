using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Icons for what lies on the ground or rides a conveyor (gear, runes, consumables, materials), crops as they
    // grow, projectiles and the moving parts of machines. Gear is drawn by slot and weapon kind in the colour of
    // its tier, so a new item in item_bases.csv gets an icon without a drawing of its own.
    static class ItemArt
    {
        public const int BeltFrames = 4;

        static readonly Rgba Outline = Rgba.Hex("#15100E");

        static readonly Dictionary<char, Rgba> Common = new Dictionary<char, Rgba>
        {
            ['h'] = Rgba.Hex("#8E5C34"),
            ['H'] = Rgba.Hex("#5E3C20"),
            ['r'] = Rgba.Hex("#A8442E"),
            ['y'] = Rgba.Hex("#F2CC5C"),
            ['Y'] = Rgba.Hex("#B88A2A"),
            ['s'] = Rgba.Hex("#ECE4D2"),
            ['w'] = Rgba.Hex("#FFFFFF"),
            ['k'] = Rgba.Hex("#2A2420"),
        };

        // ---- Gear ----

        // Tiers by item level (item_bases.csv): copper, iron, mithril, obsidian.
        static Ramp Metal(int level) => Ramp.Of(level >= 30 ? "#6E5A9E" : level >= 20 ? "#7CD2E2" : level >= 10 ? "#B4BECC" : "#D27C46");

        static readonly string[] Sword =
        {
            ".........L",
            "........LM",
            ".......LMm",
            "......LMm.",
            "..y..LMm..",
            "...yLMm...",
            "....yy....",
            "...ryy....",
            "..rr..y...",
            ".yr.......",
        };

        static readonly string[] Bow =
        {
            ".....Ls...",
            "....hHs...",
            "...hH.s...",
            "..hH..s...",
            "..rr..s...",
            "..rr..s...",
            "..hH..s...",
            "...hH.s...",
            "....hHs...",
            ".....Ls...",
        };

        static readonly string[] Staff =
        {
            ".......M..",
            "......MgG.",
            ".....MgwgM",
            "......GgM.",
            ".....h.M..",
            "....hH....",
            "...hH.....",
            "..hH......",
            ".hH.......",
            "hH........",
        };

        static readonly string[] Helmet =
        {
            "...LLLL...",
            "..LMMMMm..",
            ".LMMMMMMm.",
            ".LMMMMMMm.",
            ".MMddMdMm.",
            ".Mm.dM.mm.",
            ".Mm.dM.mm.",
            ".dd.dd.dd.",
        };

        static readonly string[] Armor =
        {
            ".LL....LL.",
            "LMMLLLLMMm",
            "LMMMMMMMMm",
            "mMMMyyMMmd",
            ".MMMMMMMm.",
            ".MMMMMMMm.",
            ".mMMMMMmm.",
            ".dmmmmmmd.",
        };

        static readonly string[] Boots =
        {
            ".LMm......",
            ".LMm.LMm..",
            ".LMm.LMm..",
            ".LMm.LMm..",
            ".LMMmLMm..",
            ".LMMMLMMm.",
            ".ddddLMMMm",
            ".....ddddd",
        };

        static readonly string[] Ring =
        {
            "....gG....",
            "...gwgG...",
            "...LGGm...",
            "..L....m..",
            ".L......m.",
            ".M......m.",
            ".M......d.",
            "..m....d..",
            "...mmdd...",
        };

        static readonly string[] Amulet =
        {
            "m........m",
            ".m......m.",
            "..m....m..",
            "...m..m...",
            "....MM....",
            "...LgGM...",
            "...gwgG...",
            "...MgGm...",
            "....mm....",
        };

        public static PixelImage Gear(ItemBase item)
        {
            var metal = Metal(item.Level);
            var colors = new Dictionary<char, Rgba>(Common)
            {
                ['L'] = metal.Light,
                ['M'] = metal.Main,
                ['m'] = metal.Shade,
                ['d'] = metal.Dark,
                ['g'] = item.Slot == ItemSlot.Weapon ? Rgba.Hex("#C88CFF") : Rgba.Hex("#E8485A"),
                ['G'] = item.Slot == ItemSlot.Weapon ? Rgba.Hex("#7E46D0") : Rgba.Hex("#9A2234"),
            };
            string[] rows = item.Slot switch
            {
                ItemSlot.Weapon => (item.Tags & SkillTag.Ranged) != 0 ? Bow : (item.Tags & SkillTag.Magic) != 0 ? Staff : Sword,
                ItemSlot.Helmet => Helmet,
                ItemSlot.Chest => Armor,
                ItemSlot.Boots => Boots,
                ItemSlot.Ring => Ring,
                _ => Amulet,
            };
            return PixelImage.FromRows(colors, rows).Outlined(Outline);
        }

        // ---- Runes and consumables ----

        static readonly Dictionary<char, Rgba> RuneColors = new Dictionary<char, Rgba>(Common)
        {
            ['L'] = Rgba.Hex("#A8A4A0"),
            ['M'] = Rgba.Hex("#85817D"),
            ['m'] = Rgba.Hex("#625E5C"),
            ['g'] = Rgba.Hex("#FFB050"),
            ['G'] = Rgba.Hex("#FFE0A0"),
        };

        static readonly PixelImage RuneIcon = PixelImage.FromRows(RuneColors,
            "..LLLLL..",
            ".LMMMMMm.",
            "LMMgGgMMm",
            "LMMMgMMMm",
            "LMMMgMMMm",
            "LMMgMgMMm",
            "LMMMMMMMm",
            ".mmmmmmm.").Outlined(Outline);

        public static PixelImage Rune(RuneDefinition rune) => RuneIcon;

        static readonly Dictionary<char, Rgba> PotionColors = new Dictionary<char, Rgba>(Common)
        {
            ['c'] = Rgba.Hex("#B07A48"),
            ['g'] = Rgba.Hex("#CFE6F0"),
            ['R'] = Rgba.Hex("#E8485A"),
            ['p'] = Rgba.Hex("#A8243A"),
        };

        static readonly PixelImage Potion = PixelImage.FromRows(PotionColors,
            "...cc...",
            "...gg...",
            "...gg...",
            "..gRRg..",
            ".gRwRRg.",
            ".gRRRRg.",
            ".gRRRpg.",
            "..gppg..").Outlined(Outline);

        static readonly Dictionary<char, Rgba> FoodColors = new Dictionary<char, Rgba>(Common)
        {
            ['L'] = Rgba.Hex("#E8A866"),
            ['M'] = Rgba.Hex("#C4783E"),
            ['m'] = Rgba.Hex("#8E4E24"),
        };

        static readonly PixelImage RoastedMushroom = PixelImage.FromRows(FoodColors,
            "........h.",
            "..LLL..h..",
            ".LMwMm.h..",
            ".MMMMmh...",
            "..mmmh....",
            "..LLhL....",
            ".LMhMMm...",
            ".MhMMMm...",
            "..hmmm....",
            ".h........").Outlined(Outline);

        public static PixelImage Consumable(ConsumableDefinition consumable) => consumable.Id == "roasted_mushroom" ? RoastedMushroom : Potion;

        // ---- Materials ----

        static Dictionary<char, Rgba> RockAnd(Ramp ore) => new Dictionary<char, Rgba>(Common)
        {
            ['L'] = Rgba.Hex("#9C9088"),
            ['M'] = Rgba.Hex("#7C7068"),
            ['m'] = Rgba.Hex("#5C524C"),
            ['d'] = Rgba.Hex("#423A36"),
            ['O'] = ore.Light,
            ['o'] = ore.Main,
            ['p'] = ore.Shade,
        };

        static readonly string[] Stone =
        {
            "...LLL....",
            "..LMMMLm..",
            ".LMMMMMMm.",
            ".MMMMMMmm.",
            ".mMMMMmmd.",
            "..dddddd..",
        };

        static readonly string[] OreChunk =
        {
            "...LLL....",
            "..LMOoLm..",
            ".LMMopMMm.",
            ".MOoMMOmm.",
            ".mopMMpmd.",
            "..dddddd..",
        };

        static readonly Dictionary<char, Rgba> BarColors = new Dictionary<char, Rgba>
        {
            ['L'] = Rgba.Hex("#E0E8F2"),
            ['M'] = Rgba.Hex("#AEBACA"),
            ['m'] = Rgba.Hex("#7A8698"),
            ['d'] = Rgba.Hex("#525C6C"),
        };

        static readonly PixelImage IronBar = PixelImage.FromRows(BarColors,
            "...LLLLL..",
            "..LMMMMMm.",
            ".LMMMMMmd.",
            "LMMMMMMmd.",
            "mmmmmmmd..").Outlined(Outline);

        static readonly string[] Goo =
        {
            "....L.....",
            "...LM.....",
            "..LMMm....",
            ".LMwMMm...",
            ".MwMMMm...",
            ".MMMMmm...",
            "..mmmd....",
        };

        static readonly string[] Gem =
        {
            "...LL...",
            "..LwLM..",
            ".LwLMMm.",
            "LLMMMMmd",
            ".MMMmmd.",
            "..Mmmd..",
            "...dd...",
        };

        static readonly string[] Spores =
        {
            "..LM......",
            ".LwMm..LM.",
            ".MMmm.LwMm",
            "..mm..MMmm",
            "...LM..mm.",
            "..LwMm....",
            "..MMmm....",
            "...mm.....",
        };

        static readonly string[] CapMushroom =
        {
            "..LLLL....",
            ".LMwMMm...",
            "LMwMMMMm..",
            "MMMMMMmm..",
            ".dmmmmd...",
            "...sS.....",
            "...sS.....",
            "..sSSS....",
        };

        static readonly Dictionary<string, PixelImage> MaterialIcons = new Dictionary<string, PixelImage>();

        public static PixelImage Material(MaterialDefinition material)
        {
            if (MaterialIcons.TryGetValue(material.Id, out var icon)) return icon;
            icon = MaterialIcon(material);
            MaterialIcons[material.Id] = icon;
            return icon;
        }

        static PixelImage MaterialIcon(MaterialDefinition material)
        {
            var placed = material.Places is TileType places && places != TileType.Wall ? TileArt.Object(places) : null;
            if (placed != null && material.Places == TileType.Conveyor)
            {
                // The frame alone is empty; the icon shows it with its belt.
                placed = placed.Copy();
                placed.Draw(Belt(0), 0, 0);
            }
            if (placed != null) return placed;
            switch (material.Id)
            {
                case "copper_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#D6783F")), OreChunk).Outlined(Outline);
                case "iron_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#B8CCE4")), OreChunk).Outlined(Outline);
                case "iron_bar": return IronBar;
                case "slime_gel": return Ramped(Goo, Ramp.Of("#8CD94D"));
                case "warden_core": return Ramped(Gem, Ramp.Of("#FFA83A"));
                case "mushroom_spore": return Ramped(Spores, Ramp.Of("#B07CE0"));
                case "mushroom": return Ramped(CapMushroom, Ramp.Of("#E8A866"));
                default: return PixelImage.FromRows(RockAnd(Ramp.Of("#7C7068")), Stone).Outlined(Outline);
            }
        }

        static PixelImage Ramped(string[] rows, Ramp ramp)
        {
            var colors = new Dictionary<char, Rgba>(Common)
            {
                ['L'] = ramp.Light,
                ['M'] = ramp.Main,
                ['m'] = ramp.Shade,
                ['d'] = ramp.Dark,
                ['S'] = Rgba.Hex("#C8B496"),
            };
            return PixelImage.FromRows(colors, rows).Outlined(ramp.Outline);
        }

        // ---- Crops ----

        static readonly Dictionary<char, Rgba> CropColors = new Dictionary<char, Rgba>(Common)
        {
            ['g'] = Rgba.Hex("#8AD05A"),
            ['G'] = Rgba.Hex("#5A9A3A"),
            ['L'] = Rgba.Hex("#F6D0A0"),
            ['M'] = Rgba.Hex("#E8A866"),
            ['m'] = Rgba.Hex("#B8743E"),
            ['d'] = Rgba.Hex("#7E4A26"),
            ['c'] = Rgba.Hex("#D8C4A8"),
            ['C'] = Rgba.Hex("#B09474"),
            ['S'] = Rgba.Hex("#C8B496"),
            ['t'] = Rgba.Hex("#4A3426"),
            ['T'] = Rgba.Hex("#33241A"),
        };

        // Planted soil under every crop, so a field reads as a field.
        static readonly string[] Soil =
        {
            "..tttttttt..",
            ".tTTTTTTTTt.",
        };

        static readonly string[][] CropStages =
        {
            new[]
            {
                "............",
                "............",
                "............",
                "............",
                ".....gg.....",
                "......G.....",
            },
            new[]
            {
                "............",
                "............",
                "............",
                ".....cc.....",
                "....cCCc....",
                ".....sS.....",
            },
            new[]
            {
                "............",
                "............",
                "....MMMM....",
                "...MwMMmm...",
                "....dmmd....",
                ".....sS.....",
            },
            new[]
            {
                ".LLLL.......",
                "LMwMMm......",
                "MMMMMmm.LL..",
                ".dmmmd.LwMm.",
                "...sS..dmmd.",
                "...sS...sS..",
            },
        };

        // Four steps as Docs/Farming.md describes: sprout, young, growing, ripe.
        public static PixelImage Crop(int stage)
        {
            var rows = new List<string>(CropStages[System.Math.Clamp(stage, 0, CropStages.Length - 1)]);
            rows.AddRange(Soil);
            return PixelImage.FromRows(CropColors, rows.ToArray()).Outlined(Outline.WithAlpha(200));
        }

        // ---- Projectiles ----

        static readonly Dictionary<char, Rgba> ArrowColors = new Dictionary<char, Rgba>(Common)
        {
            ['M'] = Rgba.Hex("#C8D0DC"),
            ['m'] = Rgba.Hex("#7A8494"),
        };

        // Points along +x; the view turns it the way it flies.
        public static readonly PixelImage Arrow = PixelImage.FromRows(ArrowColors,
            "ws......m..",
            ".shhhhhhMMM",
            "ws......m..");

        // A glowing ball in white and greys, for the view to tint with the shot's colour.
        public static readonly PixelImage Orb = Ball(spiky: false);

        // An enemy's shot: the same ball with spikes, so it reads as harmful whatever its colour.
        public static readonly PixelImage Spore = Ball(spiky: true);

        static PixelImage Ball(bool spiky)
        {
            const int size = 9;
            var image = new PixelImage(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 4;
                    float dy = y - 4;
                    float d = System.MathF.Sqrt(dx * dx + dy * dy);
                    if (d <= 1.2f) image[x, y] = Rgba.White;
                    else if (d <= 2.4f) image[x, y] = new Rgba(225, 225, 225);
                    else if (d <= 3.4f) image[x, y] = new Rgba(160, 160, 160);
                    else if (spiky && (x == 4 || y == 4 || x == y || x + y == 8) && d <= 4.5f) image[x, y] = new Rgba(90, 90, 90);
                }
            }
            image[3, 3] = Rgba.White;
            return image;
        }

        // ---- Machines ----

        static readonly Rgba Steel = Rgba.Hex("#A4AEBC");
        static readonly Rgba SteelLight = Rgba.Hex("#D4DCE6");
        static readonly Rgba SteelDark = Rgba.Hex("#5A6270");

        // The drill's bit, a cone pointing along +x from the middle of the tile to its edge. The view turns it to
        // face the vein.
        public static readonly PixelImage DrillBit = MakeDrillBit();

        static PixelImage MakeDrillBit()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            for (int y = 4; y < 12; y++)
            {
                image[9, y] = SteelDark;
                image[10, y] = Steel;
            }
            for (int x = 11; x < 16; x++)
            {
                int half = (17 - x) / 2;
                for (int y = 8 - half; y < 8 + half; y++)
                {
                    image[x, y] = (x + y) % 3 == 0 ? SteelDark : y < 8 ? SteelLight : Steel;
                }
            }
            return image.Outlined(Outline);
        }

        // The arrow on a smelter's front, where its bars come out.
        public static readonly PixelImage Chevron = MakeChevron();

        static PixelImage MakeChevron()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var yellow = Rgba.Hex("#F2C440");
            for (int i = 0; i < 4; i++)
            {
                image[11 + i, 4 + i] = yellow;
                image[12 + i, 4 + i] = yellow;
                image[11 + i, 11 - i] = yellow;
                image[12 + i, 11 - i] = yellow;
            }
            return image.Outlined(Outline);
        }

        // A belt running along +x, rails on both sides and chevrons that move a little each frame. It runs edge to
        // edge, so belts in a line join up.
        public static PixelImage Belt(int frame)
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var rubber = Rgba.Hex("#2C2E36");
            var mark = Rgba.Hex("#4E525E");
            int phase = (frame % BeltFrames) * 2;
            for (int x = 0; x < TileArt.Size; x++)
            {
                image[x, 1] = Outline;
                image[x, 2] = SteelLight;
                image[x, 3] = x % 4 == 1 ? SteelDark : Steel;
                for (int r = 0; r < 8; r++)
                {
                    int offset = r < 4 ? r : 7 - r;
                    int along = ((x - phase - offset) % 8 + 8) % 8;
                    image[x, 4 + r] = along < 2 ? mark : rubber;
                }
                image[x, 12] = x % 4 == 1 ? SteelDark : Steel;
                image[x, 13] = SteelDark;
                image[x, 14] = Outline;
            }
            return image;
        }
    }
}
