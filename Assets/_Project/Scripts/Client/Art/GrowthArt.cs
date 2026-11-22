using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The pictures of the growth screens (Docs/UiRework.md 4-3, U4): the passive tree's points as stars and runes on the
    // night sky (a dim one out of reach, an outlined one that can be learned, a gold orb once learned; a big point with a
    // second ring), the small glyph on each point for what it raises instead of the name's first letter, the colour of
    // its kind (attack red, defence green, magic blue, the rest gold, from passives.csv's stats; the sheet is not
    // changed), and each class's colour and weapon for its card. Plain pixels, so the preview (DevTools/ArtPreview) draws
    // the same.
    static class GrowthArt
    {
        public enum NodeLook
        {
            Locked,
            Open,
            Taken,
            Start,
        }

        // The four kinds of point (4-3: 공격 빨강, 방어 초록, 마법 파랑, 그 밖 금색).
        public static readonly Rgba Attack = Rgba.Hex("#E8574A");
        public static readonly Rgba Defence = Rgba.Hex("#5FCF6A");
        public static readonly Rgba Magic = Rgba.Hex("#5C8EFF");
        public static readonly Rgba Other = Rgba.Hex("#E8B84A");
        public static readonly Rgba Gold = Rgba.Hex("#F4DE9C");
        public static readonly Rgba Night = Rgba.Hex("#0E1230");
        public static readonly Rgba Faint = Rgba.Hex("#3A4266");

        // ---- The points ----

        // How far the image reaches past the point's own circle: a big point's outer ring and a learned one's glow.
        public const int Margin = 3;

        static readonly Dictionary<(int, NodeLook, int, bool), PixelImage> Nodes = new Dictionary<(int, NodeLook, int, bool), PixelImage>();

        // A point of the tree, its circle `radius` pixels round, in its kind's colour: out of reach a dark disc with a dim
        // rim; one that can be learned a dark disc with a bright rim; a learned one a lit orb in a gold rim with a glow;
        // the start a gold-rimmed disc with a diamond. A big point has a second ring round it. The image is square,
        // radius + Margin from its middle to each edge.
        public static PixelImage Node(int radius, NodeLook look, Rgba color, bool notable)
        {
            int key = color.R << 24 | color.G << 16 | color.B << 8 | color.A;
            if (Nodes.TryGetValue((radius, look, key, notable), out var made)) return made;
            int half = radius + Margin;
            var g = new PixelImage(half * 2 + 1, half * 2 + 1);
            var dim = color.Mix(Faint, 0.6f);
            var fillDark = Rgba.Hex("#0B0E22");
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    float dx = x - half, dy = y - half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    Rgba c = Rgba.Clear;
                    if (d <= radius + 0.5f)
                    {
                        bool rim = d > radius - 0.7f;
                        switch (look)
                        {
                            case NodeLook.Locked:
                                c = rim ? dim : fillDark.Mix(Night, 0.5f);
                                break;
                            case NodeLook.Open:
                                c = rim ? color.Mix(Rgba.White, 0.2f) : d > radius - 1.7f ? color.Scale(0.35f) : Night;
                                break;
                            case NodeLook.Taken:
                                if (rim) c = Gold;
                                else
                                {
                                    // Lit from the top left: a light spot, the colour, then its shade at the far side.
                                    float light = (-dx - dy) / (radius * 1.6f) + 0.5f - d / (radius * 2.4f);
                                    c = light > 0.55f ? color.Mix(Rgba.White, 0.55f) : light > 0.15f ? color : color.Scale(0.6f);
                                }
                                break;
                            default:
                                c = rim ? Gold : Rgba.Hex("#241C3C");
                                break;
                        }
                    }
                    else if (d <= radius + 1.5f && look == NodeLook.Taken)
                    {
                        c = Gold.WithAlpha(110);
                    }
                    if (notable && d > radius + 1.5f && d <= radius + 2.5f)
                    {
                        c = look switch
                        {
                            NodeLook.Taken => Gold,
                            NodeLook.Open => color.Mix(Rgba.White, 0.1f).WithAlpha(200),
                            _ => dim.WithAlpha(160),
                        };
                    }
                    if (c.A > 0) g[x, y] = c;
                }
            }
            if (look == NodeLook.Start)
            {
                for (int y = -2; y <= 2; y++)
                    for (int x = -2; x <= 2; x++)
                        if (Math.Abs(x) + Math.Abs(y) <= 2) g[half + x, half + y] = Math.Abs(x) + Math.Abs(y) <= 1 ? Rgba.White : Gold;
            }
            Nodes[(radius, look, key, notable)] = g;
            return g;
        }

        static readonly Dictionary<(int, int, int), PixelImage> Rings = new Dictionary<(int, int, int), PixelImage>();

        // A thin ring `radius` pixels round in the colour, in an image as big as Node's for that radius: the breathing
        // outline of a point that can be learned, the picked point's mark, the flash of one just learned.
        public static PixelImage Ring(int radius, Rgba color, int ring)
        {
            int key = color.R << 24 | color.G << 16 | color.B << 8 | color.A;
            if (Rings.TryGetValue((radius, ring, key), out var made)) return made;
            int half = radius + Margin;
            var g = new PixelImage(half * 2 + 1, half * 2 + 1);
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    float dx = x - half, dy = y - half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > ring - 0.5f && d <= ring + 0.5f) g[x, y] = color;
                }
            }
            Rings[(radius, ring, key)] = g;
            return g;
        }

        // ---- What a point raises ----

        // The kind of a point by its first stat (4-3): attacks (damage that is not a spell's, speed, criticals) red; the
        // body (life, its regeneration, armour) green; spells, elements, mana and spirits blue; the rest (moving, mining,
        // finds, cooldowns, durations) gold.
        public static Rgba KindOf(PassiveNode node)
        {
            if (node.IsStart || node.Modifiers.Count == 0) return Other;
            var first = node.Modifiers[0];
            const SkillTag spells = SkillTag.Magic | SkillTag.Fire | SkillTag.Cold | SkillTag.Lightning | SkillTag.Summon;
            if ((first.Tags & spells) != 0) return Magic;
            return first.Stat switch
            {
                StatType.MaxHealth or StatType.HealthRegen or StatType.Armor => Defence,
                StatType.Damage or StatType.AttackSpeed or StatType.CritChance or StatType.CritDamage => Attack,
                StatType.SkillRank => (first.Tags & (SkillTag.Melee | SkillTag.Ranged)) != 0 ? Attack : Magic,
                StatType.MaxMana or StatType.ManaRegen or StatType.FireDamage or StatType.ColdDamage or StatType.LightningDamage => Magic,
                _ => Other,
            };
        }

        // The kind's name, for the point's card.
        public static string KindName(PassiveNode node)
        {
            var kind = KindOf(node);
            return kind.Equals(Attack) ? "공격" : kind.Equals(Defence) ? "방어" : kind.Equals(Magic) ? "마법" : "그 밖";
        }

        // The glyph of what the point raises most plainly: an element, a spirit, a weapon's kind, then the stat.
        public static string[] GlyphOf(PassiveNode node)
        {
            if (node.IsStart || node.Modifiers.Count == 0) return null;
            var first = node.Modifiers[0];
            return GlyphOf(first.Stat, first.Tags);
        }

        public static string[] GlyphOf(StatType stat, SkillTag tags)
        {
            if (stat == StatType.SkillRank) return Rank;
            if (stat == StatType.FireDamage || (tags & SkillTag.Fire) != 0) return Flame;
            if (stat == StatType.ColdDamage || (tags & SkillTag.Cold) != 0) return Snow;
            if (stat == StatType.LightningDamage || (tags & SkillTag.Lightning) != 0) return Bolt;
            if ((tags & SkillTag.Summon) != 0) return Spirit;
            return stat switch
            {
                StatType.MaxHealth => UiIcons.Heart,
                StatType.HealthRegen => Plus,
                StatType.Armor => Shield,
                StatType.MaxMana or StatType.ManaRegen => Drop,
                StatType.MoveSpeed => Boot,
                StatType.AttackSpeed => Speed,
                StatType.CritChance or StatType.CritDamage => Target,
                StatType.MiningSpeed => Pick,
                StatType.ItemRarity => Gem,
                StatType.CooldownRecovery or StatType.SkillDuration => Hourglass,
                _ => (tags & SkillTag.Ranged) != 0 ? Arrow : (tags & SkillTag.Magic) != 0 ? Orb : Sword,
            };
        }

        // The glyph's own colour: an element's, else the point's kind a little lighter.
        public static Rgba GlyphColor(PassiveNode node)
        {
            var glyph = GlyphOf(node);
            if (glyph == Flame) return Rgba.Hex("#FF9440");
            if (glyph == Snow) return Rgba.Hex("#A8E8FF");
            if (glyph == Bolt) return Rgba.Hex("#FFE860");
            if (glyph == Spirit) return Rgba.Hex("#7AF0D8");
            if (glyph == UiIcons.Heart) return Rgba.Hex("#FF6070");
            return KindOf(node).Mix(Rgba.White, 0.3f);
        }

        public static readonly string[] Sword = { "......o", ".....o#", "....o#.", "#..o#..", ".##x...", ".x#....", "x..#..." };
        public static readonly string[] Arrow = { "...####", ".....##", "....#.#", "...#..#", "..#....", "##.....", "#......" };
        public static readonly string[] Flame = { "...#...", "..##...", "..#o#..", ".#oo#.#", ".#ooo##", "#oooo##", ".#####." };
        public static readonly string[] Snow = { "...#...", ".#.#.#.", "..###..", "###o###", "..###..", ".#.#.#.", "...#..." };
        public static readonly string[] Bolt = { "...###", "..##..", ".##...", "#####.", "..##..", ".##...", "##...." };
        public static readonly string[] Shield = { "#######", "#ooo###", "#ooo###", "#ooo###", ".#oo##.", "..###..", "...#..." };
        public static readonly string[] Drop = { "...#...", "..###..", ".##o##.", ".#ooo#.", "#ooo###", ".#####.", "..###.." };
        public static readonly string[] Boot = { "..###..", "..###..", "..###..", "..####.", ".######", "#######", "xxxxxxx" };
        public static readonly string[] Target = { "..###..", ".#...#.", "#..#..#", "#.###.#", "#..#..#", ".#...#.", "..###.." };
        public static readonly string[] Spirit = { "..###..", ".#####.", "##o#o##", "#######", "#######", "#######", "#.#.#.#" };
        public static readonly string[] Pick = { ".#####.", "#..#..#", "...#...", "...#...", "...#...", "...x...", "...x..." };
        public static readonly string[] Gem = { ".#####.", "#oo#oo#", "#######", ".#####.", "..###..", "...#..." };
        public static readonly string[] Hourglass = { "#######", ".#ooo#.", "..#o#..", "...#...", "..#.#..", ".#ooo#.", "#######" };
        public static readonly string[] Rank = { "...#...", "..###..", ".##.##.", "##...##", "...#...", "..###..", ".##.##." };
        public static readonly string[] Plus = { "..###..", "..#o#..", "###o###", "#ooooo#", "###o###", "..#o#..", "..###.." };
        public static readonly string[] Orb = { "..###..", ".#ooo#.", "#oo####", "#o#####", "#######", ".#####.", "..###.." };
        public static readonly string[] Speed = { "#..#...", ".#..#..", "..#..#.", "...#..#", "..#..#.", ".#..#..", "#..#..." };
        // Coins for the character's sheet (금화) and the points to spend.
        public static readonly string[] Point = { "...#...", "..#o#..", ".#ooo#.", "#ooooo#", ".#ooo#.", "..#o#..", "...#..." };

        // ---- Classes ----

        // A class's colour on its card and its skills' column: from its first skill's tags (a fire mage burns orange), the
        // archer green.
        public static Rgba ClassColor(ClassDefinition chosen)
        {
            if (chosen == null) return Other;
            if (chosen.Id == "archer") return Rgba.Hex("#7CCB5A");
            if (chosen.Id == "warrior") return Rgba.Hex("#C8CCD8");
            var first = chosen.Skills.FirstOrDefault();
            if (first == null) return Other;
            var tags = first.Tags;
            if ((tags & SkillTag.Fire) != 0) return Rgba.Hex("#FF8030");
            if ((tags & SkillTag.Lightning) != 0 || (tags & SkillTag.Cold) != 0) return Rgba.Hex("#7AB8FF");
            if ((tags & SkillTag.Summon) != 0) return Rgba.Hex("#6AE0C8");
            if ((tags & SkillTag.Magic) != 0) return Rgba.Hex("#B88CFF");
            return Other;
        }

        // What a hero of the class holds on its card: a sword, a bow, or a staff for the spells.
        public static HeroWeapon WeaponOf(ClassDefinition chosen) => chosen?.Id switch
        {
            "warrior" => HeroWeapon.Sword,
            "archer" => HeroWeapon.Bow,
            null => HeroWeapon.None,
            _ => HeroWeapon.Staff,
        };

        // The hero standing, facing the viewer, with what they hold, cut to the box round them (24 by 22 pixels: the
        // figure and a weapon at either side) for a class's card or the character's sheet.
        public const int HeroWidth = 24, HeroHeight = 22;

        public static PixelImage Hero(HeroGear gear, Rgba tunic)
        {
            var canvas = HeroArt.Draw(Look.Down, HeroMove.Stand, 0, gear, tunic);
            var cut = new PixelImage(HeroWidth, HeroHeight);
            for (int y = 0; y < cut.Height; y++)
                for (int x = 0; x < cut.Width; x++)
                {
                    int cx = HeroArt.FeetX - 12 + x, cy = HeroArt.FeetY - 20 + y;
                    if (canvas.Inside(cx, cy)) cut[x, y] = canvas[cx, cy];
                }
            return cut;
        }
    }
}
