using System;
using System.Linq;
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

        // Tiers by item level (item_bases.csv): copper, iron, mithril, obsidian, void.
        static Ramp Metal(int level) => Ramp.Of(level >= 40 ? "#B48CFF" : level >= 30 ? "#6E5A9E" : level >= 20 ? "#7CD2E2" : level >= 10 ? "#B4BECC" : "#D27C46");

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

        // A pick head across the top, its handle running down to the bottom left where it is held.
        static readonly string[] PickaxeRows =
        {
            "...LLMm...",
            ".LMMMMMmm.",
            "Lm..hHm.mm",
            "L...hH...m",
            "...hH.....",
            "..hH......",
            ".hH.......",
            "hH........",
            "H.........",
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
            // A legendary is gold, with fiery stones.
            var metal = item.IsLegendary ? Ramp.Of("#E8B23A") : Metal(item.Level);
            var colors = new Dictionary<char, Rgba>(Common)
            {
                ['L'] = metal.Light,
                ['M'] = metal.Main,
                ['m'] = metal.Shade,
                ['d'] = metal.Dark,
                ['g'] = item.IsLegendary ? Rgba.Hex("#FF8A3C") : item.Slot == ItemSlot.Weapon ? Rgba.Hex("#C88CFF") : Rgba.Hex("#E8485A"),
                ['G'] = item.IsLegendary ? Rgba.Hex("#B83A10") : item.Slot == ItemSlot.Weapon ? Rgba.Hex("#7E46D0") : Rgba.Hex("#9A2234"),
            };
            // A weapon that adds an element's damage (2026-11-09) wears that element's stone: ember, frost or spark.
            var adds = item.Implicits.Select(m => m.Stat).FirstOrDefault(s => s == StatType.FireDamage || s == StatType.ColdDamage || s == StatType.LightningDamage);
            if (!item.IsLegendary && adds != default)
            {
                (colors['g'], colors['G']) = adds switch
                {
                    StatType.FireDamage => (Rgba.Hex("#FF9A3C"), Rgba.Hex("#C8401A")),
                    StatType.ColdDamage => (Rgba.Hex("#A8E8FF"), Rgba.Hex("#3E8CC8")),
                    _ => (Rgba.Hex("#FFF06A"), Rgba.Hex("#C8A020")),
                };
            }
            string[] rows = item.Slot switch
            {
                ItemSlot.Weapon => item.IsPickaxe ? PickaxeRows : (item.Tags & SkillTag.Ranged) != 0 ? Bow : (item.Tags & SkillTag.Magic) != 0 ? Staff : Sword,
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

        // The same flask, filled blue.
        static readonly PixelImage ManaPotion = PixelImage.FromRows(new Dictionary<char, Rgba>(PotionColors)
            {
                ['R'] = Rgba.Hex("#4A7CE8"),
                ['p'] = Rgba.Hex("#2A3FA0"),
            },
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

        static readonly Dictionary<char, Rgba> BowlColors = new Dictionary<char, Rgba>(Common)
        {
            ['b'] = Rgba.Hex("#D8C4A0"),
            ['B'] = Rgba.Hex("#9C8462"),
            ['o'] = Rgba.Hex("#FF9A5A"),
            ['R'] = Rgba.Hex("#E0402A"),
            ['g'] = Rgba.Hex("#7CC85A"),
            ['L'] = Rgba.Hex("#F0D49C"),
            ['M'] = Rgba.Hex("#D8A866"),
            ['m'] = Rgba.Hex("#A8743E"),
        };

        // Peppers and mushroom fried in a bowl.
        static readonly PixelImage SpicyFry = PixelImage.FromRows(BowlColors,
            "...w..w...",
            "..oR.gR...",
            ".RoMgRoMg.",
            "bbbbbbbbbb",
            ".bBBBBBBb.",
            "..bBBBBb..",
            "...bbbb...").Outlined(Outline);

        // A rolled scroll tied with a blue ribbon.
        static readonly PixelImage RecallScroll = PixelImage.FromRows(BowlColors,
            "..bbbbbb..",
            ".bLLLLLLb.",
            ".bLMmMMLb.",
            ".bLLLLLLb.",
            ".bLMMmMLb.",
            "..bbwwbb..",
            ".bLLLLLLb.",
            ".bLMmMMLb.",
            "..bbbbbb..").Outlined(Outline);

        // A bowl of creamy soup with bits of mushroom.
        static readonly PixelImage MushroomSoup = PixelImage.FromRows(BowlColors,
            "..w...w...",
            "...w...w..",
            ".LLmLLLmL.",
            "bbbbbbbbbb",
            ".bBBBBBBb.",
            "..bBBBBb..",
            "...bbbb...").Outlined(Outline);

        public static PixelImage Consumable(ConsumableDefinition consumable) => consumable.Id switch
        {
            "roasted_mushroom" => RoastedMushroom,
            "spicy_fry" => SpicyFry,
            "mushroom_soup" => MushroomSoup,
            "recall_scroll" => RecallScroll,
            "mana_potion" => ManaPotion,
            "grilled_fish" => Ramped(FishRows, Ramp.Of("#C4783E")),
            "fish_stew" => MushroomSoup,
            "eel_skewer" => SpicyFry,
            "abyss_feast" => MushroomSoup,
            "glow_stirfry" => SpicyFry,
            "frost_sorbet" => MushroomSoup,
            "star_punch" => MushroomSoup,
            "baked_potato" => RoastedMushroom,
            "grilled_corn" => SpicyFry,
            "kimchi_stew" => MushroomSoup,
            "fried_egg" => RoastedMushroom,
            "steamed_crab" => MushroomSoup,
            "fried_shrimp" => SpicyFry,
            "warm_milk" => MushroomSoup,
            _ => Potion,
        };

        // A fish, facing left, in its colour.
        static readonly string[] FishRows =
        {
            "...LL.....",
            ".LLMMLL.L.",
            "LwMMMMMLL.",
            "MMMMMMmmM.",
            ".mmmmmm.m.",
            "...mm.....",
        };

        // A hoe: a wooden handle and a copper blade.
        static readonly PixelImage HoeIcon = PixelImage.FromRows(new Dictionary<char, Rgba>(Common)
            {
                ['h'] = Rgba.Hex("#9A7452"),
                ['H'] = Rgba.Hex("#6A4A32"),
                ['c'] = Rgba.Hex("#D6783F"),
                ['C'] = Rgba.Hex("#9A4E22"),
            },
            "......cccc",
            "......hCC.",
            ".....h....",
            "....h.....",
            "...h......",
            "..h.......",
            ".H........",
            "H.........").Outlined(Outline);

        // A rod with its line and float.
        static readonly PixelImage FishingRodIcon = PixelImage.FromRows(new Dictionary<char, Rgba>(Common)
            {
                ['h'] = Rgba.Hex("#9A7452"),
                ['H'] = Rgba.Hex("#6A4A32"),
                ['l'] = Rgba.Hex("#C8D0DC"),
                ['r'] = Rgba.Hex("#E8485A"),
            },
            ".........h",
            "........hl",
            ".......h.l",
            "......h..l",
            ".....h...l",
            "....h....l",
            "...h.....r",
            "..h......w",
            ".HH.......",
            "HH........").Outlined(Outline);

        // The float on a line in the water, red over white, drawn in the world where a line went in.
        public static readonly PixelImage Bobber = PixelImage.FromRows(new Dictionary<char, Rgba>(Common)
            {
                ['r'] = Rgba.Hex("#E8485A"),
                ['R'] = Rgba.Hex("#A8243A"),
            },
            "..r..",
            ".rrR.",
            ".wwR.",
            "..w..").Outlined(Outline);

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

        // An ingot in the ramp's colours, like the iron bar.
        static PixelImage Bar(Ramp ramp)
        {
            var colors = new Dictionary<char, Rgba> { ['L'] = ramp.Light, ['M'] = ramp.Main, ['m'] = ramp.Shade, ['d'] = ramp.Dark };
            return PixelImage.FromRows(colors,
                "...LLLLL..",
                "..LMMMMMm.",
                ".LMMMMMmd.",
                "LMMMMMMmd.",
                "mmmmmmmd..").Outlined(Outline);
        }

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

        // A turret's barrel pointing along +x from the middle, with a dark muzzle.
        public static readonly PixelImage TurretBarrel = MakeBarrel();

        static PixelImage MakeBarrel()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            for (int x = 7; x < 15; x++)
            {
                image[x, 6] = Outline;
                image[x, 7] = x == 14 ? Outline : SteelLight;
                image[x, 8] = x == 14 ? Outline : Steel;
                image[x, 9] = Outline;
            }
            for (int y = 5; y <= 10; y++)
            {
                for (int x = 5; x <= 9; x++) image[x, y] = y == 5 || y == 10 || x == 5 || x == 9 ? Outline : SteelDark;
            }
            return image;
        }

        // A small white hen, side on, facing left; two frames, its head up and pecking.
        public static PixelImage Chicken(int frame) => PixelImage.FromRows(ChickenColors, frame % 2 == 0 ? ChickenUp : ChickenPeck).Outlined(Outline);

        static readonly Dictionary<char, Rgba> ChickenColors = new Dictionary<char, Rgba>(Common)
        {
            ['W'] = Rgba.Hex("#F6F2E8"),
            ['G'] = Rgba.Hex("#C8C0B0"),
            ['R'] = Rgba.Hex("#E0402A"),
            ['Y'] = Rgba.Hex("#F2B840"),
        };

        static readonly string[] ChickenUp =
        {
            ".R......",
            "WW......",
            "YWW...WW",
            ".WWWWWW.",
            ".WWWGGW.",
            "..WWWW..",
            "...YY...",
        };

        static readonly string[] ChickenPeck =
        {
            "........",
            "......WW",
            ".R..WWW.",
            "WWWWWWW.",
            "YWWWGGW.",
            "..WWWW..",
            "...YY...",
        };

        // A small black-and-white cow, side on, facing left; two frames, head up and grazing.
        public static PixelImage Cow(int frame) => PixelImage.FromRows(CowColors, frame % 2 == 0 ? CowUp : CowGraze).Outlined(Outline);

        // A sheep (2026-11-09): a cream fleece in puffs, a dark face and legs; head up, then down grazing.
        public static PixelImage Sheep(int frame) => PixelImage.FromRows(SheepColors, frame % 2 == 0 ? SheepUp : SheepGraze).Outlined(Outline);

        static readonly Dictionary<char, Rgba> SheepColors = new Dictionary<char, Rgba>(Common)
        {
            ['W'] = Rgba.Hex("#F4F0E6"),
            ['w'] = Rgba.Hex("#D6CCBA"),
            ['K'] = Rgba.Hex("#3A322C"),
            ['e'] = Rgba.Hex("#F2E6C8"),
        };

        static readonly string[] SheepUp =
        {
            "....w.w.w...",
            ".KK.wWwWwWw.",
            "KeKwWWWWWWWw",
            "KKKWWWwWWWWW",
            ".KwWWWWWWwWW",
            "..wWWWWWWWWw",
            "...wwwwwwww.",
            "...K.K..K.K.",
        };

        static readonly string[] SheepGraze =
        {
            "............",
            "....w.w.w...",
            "...wWwWwWwW.",
            "..wWWWWWWWWw",
            ".KWWWwWWWWWW",
            "KeKWWWWWWwWW",
            "KKKwwwwwwww.",
            "...K.K..K.K.",
        };

        static readonly string[] Fleece =
        {
            "..L.L.L...",
            ".LMLMLML..",
            "LMMMLMMMm.",
            "MMLMMMLMm.",
            ".MMMMMMm..",
            "..mmdmm...",
        };

        static readonly Dictionary<char, Rgba> CowColors = new Dictionary<char, Rgba>(Common)
        {
            ['W'] = Rgba.Hex("#F6F2E8"),
            ['K'] = Rgba.Hex("#2A2420"),
            ['P'] = Rgba.Hex("#F0A0A0"),
            ['G'] = Rgba.Hex("#C8C0B0"),
        };

        static readonly string[] CowUp =
        {
            "KK..........",
            "WKW.........",
            "WWWWWKKWWWW.",
            "PWWWKKWWWKWW",
            ".WWWWWWWWWWW",
            ".WWKKWWWWKKW",
            "..K.K....K.K",
        };

        static readonly string[] CowGraze =
        {
            "............",
            "....WKKWWWW.",
            "KK.WKKWWWKWW",
            "WKWWWWWWWWWW",
            "WWWWKWWWWKKW",
            "PW.K.K...K.K",
            "............",
        };

        // A crab, front on.
        static readonly string[] Crab =
        {
            "L......L",
            "MM....MM",
            ".M.ww.M.",
            ".MMMMMM.",
            "MMmMMmMM",
            ".m.mm.m.",
        };

        // A bottle with a cork, for juices and wines.
        static readonly string[] Bottle =
        {
            "...SS...",
            "...hh...",
            "...LM...",
            "..LwMm..",
            ".LwMMmd.",
            ".LMMMmd.",
            ".MMMmmd.",
            "..mmdd..",
        };

        // A small jar with a cloth top, for pickles.
        static readonly string[] Pickles =
        {
            ".hhhhhh.",
            ".SSSSSS.",
            "..LMMm..",
            ".LwMMmd.",
            ".LMMMmd.",
            ".MMMmmd.",
            "..mmdd..",
        };

        // A round coin with a square hole and a glint.
        static readonly string[] CoinRows =
        {
            "..LLLM..",
            ".LwLMMm.",
            "LLMddMmd",
            "LMMddmmd",
            ".MMmmmd.",
            "..mmdd..",
        };

        // A cog with a hole through it.
        static readonly string[] Cog =
        {
            "...LM.LM...",
            "...LMMMm...",
            ".LMMLwMMmd.",
            ".LMMm.MMmd.",
            "LMMm...mMmd",
            ".MMm...mmd.",
            ".MMMmmmmmd.",
            "..mMmmmmd..",
            "...md.md...",
        };

        // A coil of wire with its end loose.
        static readonly string[] Coil =
        {
            "...LLMM...",
            "..LwMMmm..",
            ".LMd..dmm.",
            ".LM....mm.",
            ".LMd..dmm.",
            "..MMmmmd..",
            "...Mmmd.MM",
            "........Mm",
        };

        // A green board with copper tracks and a black chip.
        static readonly string[] Board =
        {
            "MMMMMMMMMM",
            "MyMMkkkMyM",
            "MyyMkkkMYM",
            "MMyMkkkyyM",
            "MMyyyMMMMM",
            "MLMMyMMLMM",
            "MMMMyyyMMm",
            "mmmmmmmmmm",
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

        static readonly string[] Seeds =
        {
            "..........",
            "..LM...LM.",
            ".LwMm.LwMm",
            "..mm...mm.",
            "....LM....",
            "...LwMm...",
            "....mm....",
        };

        static readonly Dictionary<char, Rgba> PepperColors = new Dictionary<char, Rgba>(Common)
        {
            ['g'] = Rgba.Hex("#8AD05A"),
            ['G'] = Rgba.Hex("#4E8A34"),
            ['L'] = Rgba.Hex("#FF8A5A"),
            ['M'] = Rgba.Hex("#E0402A"),
            ['m'] = Rgba.Hex("#A82418"),
            ['d'] = Rgba.Hex("#6E140E"),
        };

        static readonly PixelImage Pepper = PixelImage.FromRows(PepperColors,
            "....GG....",
            "...GgG....",
            "..LMMw....",
            ".LMMMMm...",
            ".MMMMMm...",
            "..MMMmm...",
            "...mmd....",
            "....d.....").Outlined(Outline);

        static readonly Dictionary<char, Rgba> CanColors = new Dictionary<char, Rgba>(Common)
        {
            ['L'] = Rgba.Hex("#9CD6EC"),
            ['M'] = Rgba.Hex("#5AA0C8"),
            ['m'] = Rgba.Hex("#3A6E9C"),
            ['d'] = Rgba.Hex("#24466E"),
        };

        // A watering can, spout to the right.
        static readonly PixelImage WateringCanIcon = PixelImage.FromRows(CanColors,
            "...mmmm.....",
            "..m....m....",
            ".LLLLLLL...L",
            ".LwMMMMM..Lm",
            ".LMMMMMMLMm.",
            ".MMMMMMMMm..",
            ".MMMMMMMm...",
            ".mmmmmmmd...").Outlined(Outline);

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
            // A silver or gold crop: the plain one with a star in its corner.
            if (material.Base != null)
            {
                var plain = MaterialIcon(material.Base).Copy();
                var star = Rgba.Hex(material.Quality >= 2 ? "#F2C440" : "#D4DCE6");
                int right = plain.Width - 4, top = 0;
                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (2, 1), (1, 2) }) plain[right + dx, top + dy] = star;
                foreach (var (dx, dy) in new[] { (1, -1), (-1, 1), (3, 1), (1, 3), (0, 0), (2, 0), (0, 2), (2, 2) })
                {
                    int x = right + dx, y = top + dy;
                    if (x >= 0 && y >= 0 && x < plain.Width && y < plain.Height && plain[x, y].A == 0) plain[x, y] = Outline;
                }
                return plain;
            }
            var placed = material.Places is TileType places && places != TileType.Wall ? TileArt.Object(places) : null;
            if (placed != null && material.Places == TileType.Conveyor)
            {
                // The frame alone is empty; the icon shows it with its belt.
                placed = placed.Copy();
                placed.Draw(Belt(0), 0, 0);
            }
            else if (placed != null && material.Places == TileType.FastConveyor)
            {
                placed = placed.Copy();
                placed.Draw(Belt(0, fast: true), 0, 0);
            }
            else if (placed != null && material.Places == TileType.UndergroundBelt)
            {
                placed = placed.Copy();
                placed.Draw(Underground(0), 0, 0);
            }
            else if (placed != null && material.Places == TileType.Splitter)
            {
                placed = placed.Copy();
                placed.Draw(SplitterArrows, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Arm)
            {
                placed = placed.Copy();
                placed.Draw(RobotArm, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Sprinkler)
            {
                placed = placed.Copy();
                placed.Draw(SprinklerHead, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Harvester)
            {
                placed = placed.Copy();
                placed.Draw(HarvesterReel, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Filter)
            {
                placed = placed.Copy();
                placed.Draw(SplitterArrows, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Generator)
            {
                placed = placed.Copy();
                placed.Draw(Flame(0), 0, 0);
            }
            else if (placed != null && material.Places == TileType.WaterWheel)
            {
                placed = placed.Copy();
                placed.Draw(WaterWheel, 0, 0);
            }
            else if (placed != null && material.Places == TileType.Lever)
            {
                placed = placed.Copy();
                placed.Draw(LeverHandle, 0, 0);
            }
            if (placed != null) return placed;
            switch (material.Id)
            {
                case "copper_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#D6783F")), OreChunk).Outlined(Outline);
                case "iron_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#B8CCE4")), OreChunk).Outlined(Outline);
                case "iron_bar": return IronBar;
                case "mithril_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#8FE6F2")), OreChunk).Outlined(Outline);
                case "mithril_bar": return Bar(Ramp.Of("#7CD2E2"));
                case "queen_heart": return Ramped(Gem, Ramp.Of("#FF6E9C"));
                case "void_ore": return PixelImage.FromRows(RockAnd(Ramp.Of("#B48CFF")), OreChunk).Outlined(Outline);
                case "void_bar": return Bar(Ramp.Of("#9A74F0"));
                case "void_essence": return Ramped(Gem, Ramp.Of("#C8A8FF"));
                case "slime_gel": return Ramped(Goo, Ramp.Of("#8CD94D"));
                case "warden_core": return Ramped(Gem, Ramp.Of("#FFA83A"));
                case "mushroom_spore": return Ramped(Spores, Ramp.Of("#B07CE0"));
                case "mushroom": return Ramped(CapMushroom, Ramp.Of("#E8A866"));
                case "fire_pepper_seed": return Ramped(Seeds, Ramp.Of("#E8D27C"));
                case "fire_pepper": return Pepper;
                case "watering_can": return WateringCanIcon;
                case "fishing_rod": return FishingRodIcon;
                case "hoe": return HoeIcon;
                case "fertilizer": return Ramped(Goo, Ramp.Of("#8A6A3A"));
                case "glow_cap": return Ramped(CapMushroom, Ramp.Of("#6FE8D0"));
                case "frost_berry_seed": return Ramped(Seeds, Ramp.Of("#A8D8F0"));
                case "frost_berry": return Ramped(Gem, Ramp.Of("#8CD8FF"));
                case "star_melon_seed": return Ramped(Seeds, Ramp.Of("#F2E08C"));
                case "star_melon": return Ramped(Goo, Ramp.Of("#F2C440"));
                case "cave_minnow": return Ramped(FishRows, Ramp.Of("#A8B4C0"));
                case "spore_catfish": return Ramped(FishRows, Ramp.Of("#9A7CB8"));
                case "ice_trout": return Ramped(FishRows, Ramp.Of("#8CD8FF"));
                case "crystal_eel": return Ramped(FishRows, Ramp.Of("#7CD2E2"));
                case "void_angler": return Ramped(FishRows, Ramp.Of("#7A5CC8"));
                case "firefly_catfish": return Ramped(FishRows, Ramp.Of("#E8D86A"));
                case "essence": return Ramped(Gem, Ramp.Of("#C88CFF"));
                // Support crystals (Docs/Classes.md, 스킬 변형), each in the colour of what it does.
                case "support_area": return Ramped(Gem, Ramp.Of("#7CE0C8"));
                case "support_multi": return Ramped(Gem, Ramp.Of("#F2E25C"));
                case "support_haste": return Ramped(Gem, Ramp.Of("#8CB4FF"));
                case "support_power": return Ramped(Gem, Ramp.Of("#FF6E6E"));
                case "support_fire": return Ramped(Gem, Ramp.Of("#FF9A3C"));
                case "support_cold": return Ramped(Gem, Ramp.Of("#B4ECFF"));
                case "support_lightning": return Ramped(Gem, Ramp.Of("#FFF6A0"));
                case "support_pierce": return Ramped(Gem, Ramp.Of("#C8C8D8"));
                case "support_duration": return Ramped(Gem, Ramp.Of("#B48CFF"));
                case "support_leech": return Ramped(Gem, Ramp.Of("#C83C5A"));
                case "rift_key": return Ramped(Gem, Ramp.Of("#7A44E8"));
                case "rift_shard": return Ramped(Gem, Ramp.Of("#E86ED8"));
                case "pearl": return Ramped(Gem, Ramp.Of("#EDE6DA"));
                case "serpent_scale": return Ramped(Gem, Ramp.Of("#2E8C9A"));
                case "amber": return Ramped(Gem, Ramp.Of("#E8A030"));
                case "antlion_jaw": return Ramped(Gem, Ramp.Of("#E2D2A8"));
                case "lake_pike": return Ramped(FishRows, Ramp.Of("#5AA89A"));
                case "iron_gear": return Ramped(Cog, Ramp.Of("#B8BCC4"));
                case "coin": return Ramped(CoinRows, Ramp.Of("#F2C440"));
                case "cave_potato_seed": return Ramped(Seeds, Ramp.Of("#B08850"));
                case "cave_berry_seed": return Ramped(Seeds, Ramp.Of("#E07080"));
                case "moon_grape_seed": return Ramped(Seeds, Ramp.Of("#A88AE0"));
                case "snow_cabbage_seed": return Ramped(Seeds, Ramp.Of("#C8F0C0"));
                case "ember_corn_seed": return Ramped(Seeds, Ramp.Of("#F2C060"));
                case "void_bean_seed": return Ramped(Seeds, Ramp.Of("#8A6AD0"));
                case "cave_potato": return Ramped(Goo, Ramp.Of("#B08850"));
                case "cave_berry": return Ramped(Gem, Ramp.Of("#E04060"));
                case "moon_grape": return Ramped(Spores, Ramp.Of("#8A5AE0"));
                case "snow_cabbage": return Ramped(CapMushroom, Ramp.Of("#C8F0C0"));
                case "ember_corn": return Ramped(Gem, Ramp.Of("#F2A030"));
                case "void_bean": return Ramped(Goo, Ramp.Of("#6A44B8"));
                case "berry_juice": return Ramped(Bottle, Ramp.Of("#E04060"));
                case "moon_wine": return Ramped(Bottle, Ramp.Of("#8A5AE0"));
                case "frost_juice": return Ramped(Bottle, Ramp.Of("#8CD8FF"));
                case "star_wine": return Ramped(Bottle, Ramp.Of("#F2C440"));
                case "pickled_pepper": return Ramped(Pickles, Ramp.Of("#E0402A"));
                case "kimchi": return Ramped(Pickles, Ramp.Of("#D8603A"));
                case "pickled_bean": return Ramped(Pickles, Ramp.Of("#6A44B8"));
                case "chicken": return Chicken(0);
                case "cow": return Cow(0);
                case "sheep": return Sheep(0);
                case "wool": return Ramped(Fleece, Ramp.Of("#F4F0E6"));
                case "milk": return Ramped(Bottle, Ramp.Of("#F6F2E8"));
                case "cheese": return Ramped(Goo, Ramp.Of("#F2D060"));
                case "ammo": return Ramped(Seeds, Ramp.Of("#D8B050"));
                case "frost_ammo": return Ramped(Seeds, Ramp.Of("#8CD8FF"));
                case "fire_ammo": return Ramped(Seeds, Ramp.Of("#FF7A3C"));
                case "feed": return Ramped(Seeds, Ramp.Of("#D8B070"));
                case "egg": return Ramped(Goo, Ramp.Of("#F6F0E0"));
                // Pet eggs (Pets.cs), each in its creature's colour.
                case "slime_egg": return Ramped(Goo, Ramp.Of("#7CD06A"));
                case "bat_egg": return Ramped(Goo, Ramp.Of("#9A84C8"));
                case "wisp_egg": return Ramped(Goo, Ramp.Of("#9AE8F0"));
                case "crab_egg": return Ramped(Goo, Ramp.Of("#4AA0B4"));
                case "bait": return Ramped(Goo, Ramp.Of("#C87850"));
                case "mayonnaise": return Ramped(Pickles, Ramp.Of("#F6F0D0"));
                case "cave_crab": return Ramped(Crab, Ramp.Of("#E06040"));
                case "cave_shrimp": return Ramped(FishRows, Ramp.Of("#F08870"));
                case "cave_snail": return Ramped(CapMushroom, Ramp.Of("#A08870"));
                case "copper_wire": return Ramped(Coil, Ramp.Of("#E08A4A"));
                case "circuit": return Ramped(Board, Ramp.Of("#4CA85A"));
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
            // Watered soil, dark and a little blue.
            ['u'] = Rgba.Hex("#2E2C40"),
            ['U'] = Rgba.Hex("#1C1A2C"),
            ['r'] = Rgba.Hex("#E0402A"),
            ['R'] = Rgba.Hex("#A82418"),
            ['o'] = Rgba.Hex("#FF8A5A"),
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

        // A bushy pepper plant whose peppers redden as it ripens.
        static readonly string[][] PepperStages =
        {
            CropStagesSprout,
            new[]
            {
                "............",
                "............",
                "............",
                ".....g......",
                "....gGg.....",
                ".....G......",
            },
            new[]
            {
                "............",
                "....g.g.....",
                "...gGgGg....",
                "....gGg.....",
                "...g.G.g....",
                ".....G......",
            },
            new[]
            {
                "....g..g....",
                "...gGggGg...",
                "..ro.gG.or..",
                "..rR.G..rR..",
                "...R.G...R..",
                ".....G......",
            },
        };

        static string[] CropStagesSprout => CropStages[0];

        // Four steps as Docs/Farming.md describes: sprout, young, growing, ripe. Each crop has its own drawings (a
        // crop without any looks like a mushroom); watered soil is darker.
        public static PixelImage Crop(int stage) => Crop("mushroom", stage);

        public static PixelImage Crop(string id, int stage, bool watered = false)
        {
            // The new crops are the mushroom's and the pepper's drawings in colours of their own.
            var stages = FruitCrops.Contains(id) ? PepperStages : CropStages;
            var rows = new List<string>(stages[System.Math.Clamp(stage, 0, stages.Length - 1)]);
            rows.AddRange(watered ? Soil.Select(row => row.Replace('t', 'u').Replace('T', 'U')) : Soil);
            var colors = CropColors;
            if (CropTints.TryGetValue(id, out var tint))
            {
                colors = new Dictionary<char, Rgba>(CropColors);
                foreach (var pair in tint) colors[pair.Key] = pair.Value;
            }
            return PixelImage.FromRows(colors, rows.ToArray()).Outlined(Outline.WithAlpha(200));
        }

        // Crops drawn as a bush with fruit on it; the rest as the mushroom's head of leaves or cap.
        static readonly HashSet<string> FruitCrops = new HashSet<string>
        {
            "fire_pepper", "frost_berry", "star_melon", "cave_berry", "moon_grape", "ember_corn", "void_bean",
        };

        static readonly Dictionary<string, Dictionary<char, Rgba>> CropTints = new Dictionary<string, Dictionary<char, Rgba>>
        {
            // Leafy potato tops, and a pale round cabbage.
            ["cave_potato"] = new Dictionary<char, Rgba> { ['L'] = Rgba.Hex("#B8E08A"), ['M'] = Rgba.Hex("#7CB850"), ['m'] = Rgba.Hex("#4E8A30") },
            ["snow_cabbage"] = new Dictionary<char, Rgba> { ['L'] = Rgba.Hex("#F0FFF0"), ['M'] = Rgba.Hex("#C8F0C0"), ['m'] = Rgba.Hex("#88C090") },
            // Red berries, purple grapes, golden corn, dark beans.
            ["cave_berry"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#E04060"), ['R'] = Rgba.Hex("#A02040"), ['o'] = Rgba.Hex("#FF9AB0") },
            ["moon_grape"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#8A5AE0"), ['R'] = Rgba.Hex("#5A30A8"), ['o'] = Rgba.Hex("#C8A8FF") },
            ["ember_corn"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#F2A030"), ['R'] = Rgba.Hex("#B86A10"), ['o'] = Rgba.Hex("#FFE08A") },
            ["void_bean"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#6A44B8"), ['R'] = Rgba.Hex("#3A2470"), ['o'] = Rgba.Hex("#B48CFF") },
            // A glowing teal cap.
            ["glow_cap"] = new Dictionary<char, Rgba> { ['L'] = Rgba.Hex("#C8FFF0"), ['M'] = Rgba.Hex("#6FE8D0"), ['m'] = Rgba.Hex("#2EA890") },
            // Pale blue berries.
            ["frost_berry"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#8CD8FF"), ['R'] = Rgba.Hex("#4A8CC8"), ['o'] = Rgba.Hex("#E0F6FF") },
            // Round yellow melons with a pale star.
            ["star_melon"] = new Dictionary<char, Rgba> { ['r'] = Rgba.Hex("#F2C440"), ['R'] = Rgba.Hex("#B88A20"), ['o'] = Rgba.Hex("#FFF4C0") },
        };

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

        // A splitter's arrows: out of its front (+x) and both sides, the way it sends loads.
        public static readonly PixelImage SplitterArrows = MakeSplitterArrows();

        static PixelImage MakeSplitterArrows()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var yellow = Rgba.Hex("#F2C440");
            for (int i = 0; i < 3; i++)
            {
                // Right edge, pointing right.
                image[12 + i, 6 + i] = yellow;
                image[12 + i, 9 - i] = yellow;
                // Top edge, pointing up.
                image[6 + i, 12 + i] = yellow;
                image[9 - i, 12 + i] = yellow;
                // Bottom edge, pointing down.
                image[6 + i, 3 - i] = yellow;
                image[9 - i, 3 - i] = yellow;
            }
            return image.Outlined(Outline);
        }

        // A robot arm reaching along +x from the middle of its tile, a claw at the end. The view swings it from the
        // tile behind to the tile ahead while it carries something.
        public static readonly PixelImage RobotArm = MakeRobotArm();

        static PixelImage MakeRobotArm()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var joint = Rgba.Hex("#F2C440");
            for (int x = 7; x < 13; x++)
            {
                image[x, 7] = SteelLight;
                image[x, 8] = Steel;
                image[x, 9] = SteelDark;
            }
            for (int y = 6; y < 11; y++)
            {
                image[6, y] = joint;
                image[7, y] = y == 6 || y == 10 ? joint : image[7, y];
            }
            // The claw: two fingers.
            for (int x = 13; x < 16; x++)
            {
                image[x, 5] = Steel;
                image[x, 11] = Steel;
            }
            image[13, 6] = SteelDark;
            image[13, 10] = SteelDark;
            image[12, 6] = Steel;
            image[12, 10] = Steel;
            return image.Outlined(Outline);
        }

        // A sprinkler's head: four arms from a hub in the middle of the tile, a nozzle at each end. The view spins it
        // while the sprinkler runs.
        public static readonly PixelImage SprinklerHead = MakeSprinklerHead();

        static PixelImage MakeSprinklerHead()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var hub = Rgba.Hex("#F2C440");
            var nozzle = Rgba.Hex("#8CC4F4");
            for (int i = 2; i < 6; i++)
            {
                image[7 + i, 8] = Steel;
                image[8 - i, 7] = Steel;
                image[8, 8 - i] = Steel;
                image[7, 7 + i] = Steel;
            }
            image[13, 8] = image[13, 9] = nozzle;
            image[2, 7] = image[2, 6] = nozzle;
            image[8, 2] = image[7, 2] = nozzle;
            image[7, 13] = image[8, 13] = nozzle;
            for (int y = 6; y < 10; y++)
            {
                for (int x = 6; x < 10; x++) image[x, y] = (x == 6 || y == 9) ? SteelLight : (x == 9 || y == 6) ? SteelDark : hub;
            }
            return image.Outlined(Outline);
        }

        // A harvester's reel: a toothed bar across the +x edge of the tile, on an axle from the middle. The view
        // turns it to face the field.
        public static readonly PixelImage HarvesterReel = MakeHarvesterReel();

        // A water wheel: a wooden rim with eight paddles and spokes round an iron axle, drawn centred so it can turn.
        public static readonly PixelImage WaterWheel = MakeWaterWheel();

        static PixelImage MakeWaterWheel()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var wood = Rgba.Hex("#9A6A3C");
            var dark = Rgba.Hex("#6A4424");
            var light = Rgba.Hex("#C08850");
            for (int y = 0; y < TileArt.Size; y++)
            {
                for (int x = 0; x < TileArt.Size; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float angle = MathF.Atan2(dy, dx);
                    // Which of the eight paddles or spokes it is nearest, and how far from that line.
                    float sector = angle / (MathF.PI / 4);
                    float off = MathF.Abs(sector - MathF.Round(sector)) * d * MathF.PI / 4;
                    if (d < 1.6f) image[x, y] = d < 0.8f ? SteelDark : Steel;
                    else if (d >= 4.6f && d < 6.0f) image[x, y] = ((int)MathF.Floor(sector + 8) & 1) == 0 ? wood : dark;
                    else if (d >= 6.0f && d < 7.6f && off < 0.9f) image[x, y] = light;
                    else if (d < 4.6f && off < 0.6f) image[x, y] = wood;
                }
            }
            return image.Outlined(Outline);
        }

        static PixelImage MakeHarvesterReel()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            for (int x = 8; x < 12; x++) image[x, 8] = SteelDark;
            for (int y = 3; y < 13; y++)
            {
                image[12, y] = SteelDark;
                image[13, y] = Steel;
                if (y % 2 == 1) image[14, y] = SteelLight;
            }
            image[12, 8] = Rgba.Hex("#F2C440");
            return image.Outlined(Outline);
        }

        // A generator's flame behind its grate, flickering between two frames.
        public const int FlameFrames = 2;

        public static PixelImage Flame(int frame)
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var hot = Rgba.Hex("#FFE07A");
            var main = Rgba.Hex("#FF8A3C");
            var deep = Rgba.Hex("#C8401E");
            // Rows 5 to 7 of the tile from the top, inside the grate (x 5 to 10).
            for (int x = 5; x <= 10; x++)
            {
                int top = 5 + ((x + frame) % 3 == 0 ? 0 : 1);
                for (int y = top; y <= 7; y++) image[x, y] = y == 7 ? deep : (x + y + frame) % 2 == 0 ? hot : main;
            }
            return image;
        }

        // A lever's handle: a steel rod standing up from the middle of the base, with a red knob. The view leans it.
        public static readonly PixelImage LeverHandle = MakeLeverHandle();

        static PixelImage MakeLeverHandle()
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            for (int y = 3; y <= 9; y++)
            {
                image[7, y] = SteelLight;
                image[8, y] = Steel;
            }
            var knob = Rgba.Hex("#E05040");
            for (int y = 1; y <= 3; y++)
            {
                for (int x = 6; x <= 9; x++) image[x, y] = y == 1 && (x == 6 || x == 9) ? image[x, y] : knob;
            }
            return image.Outlined(Outline);
        }

        // An underground belt along +x: a belt going into a dark hooded mouth on its front half, where loads go down
        // (or come up).
        public static PixelImage Underground(int frame)
        {
            var image = Belt(frame);
            var hood = Rgba.Hex("#5C6270");
            var hoodLight = Rgba.Hex("#8A92A2");
            var dark = Rgba.Hex("#121318");
            for (int y = 1; y <= 14; y++)
            {
                for (int x = 8; x < TileArt.Size; x++)
                {
                    bool edge = y == 1 || y == 14 || x == 8;
                    image[x, y] = edge ? Outline : y <= 3 || y >= 12 ? (x == 9 ? hoodLight : hood) : x <= 9 ? hoodLight : dark;
                }
            }
            return image;
        }

        // A belt running along +x, rails on both sides and chevrons that move a little each frame. It runs edge to
        // edge, so belts in a line join up. A fast belt's rails are painted yellow.
        public static PixelImage Belt(int frame, bool fast = false)
        {
            var image = new PixelImage(TileArt.Size, TileArt.Size);
            var rubber = Rgba.Hex("#2C2E36");
            var mark = Rgba.Hex("#4E525E");
            var light = fast ? Rgba.Hex("#F2C440") : SteelLight;
            var rail = fast ? Rgba.Hex("#C8962C") : Steel;
            int phase = (frame % BeltFrames) * 2;
            for (int x = 0; x < TileArt.Size; x++)
            {
                image[x, 1] = Outline;
                image[x, 2] = light;
                image[x, 3] = x % 4 == 1 ? SteelDark : rail;
                for (int r = 0; r < 8; r++)
                {
                    int offset = r < 4 ? r : 7 - r;
                    int along = ((x - phase - offset) % 8 + 8) % 8;
                    image[x, 4 + r] = along < 2 ? mark : rubber;
                }
                image[x, 12] = x % 4 == 1 ? SteelDark : rail;
                image[x, 13] = SteelDark;
                image[x, 14] = Outline;
            }
            return image;
        }
    }
}
