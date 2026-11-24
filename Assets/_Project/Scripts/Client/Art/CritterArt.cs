using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The biomes' small creatures (Docs/BiomeRework.md 4-1 작은 생물, 묶음 B1a): only looked at, on the client alone.
    enum CritterKind : byte
    {
        // The dirt cave: a cricket that hops, a mouse that runs from whoever comes near, moths round a lit lamp.
        Cricket,
        Mouse,
        LampMoth,
        // The mushroom forest: fireflies (more at night), a snail that leaves a glowing trail, spore moths.
        Firefly,
        Snail,
        SporeMoth,
    }

    // What drifts in the air (4-1 입자): dust in a lamp's light, a drip from the ceiling and its splash, a spore rising.
    enum MoteKind : byte
    {
        Dust,
        Drip,
        Splash,
        Spore,
        // The snail's trail: a dab of glow left on the floor that fades.
        Trail,
    }

    // A few pixels each, in muted colours a step or two off the floor's, with no outline and no health bar, so none is
    // taken for a monster (Docs/BiomeRework.md 4-6: Dead Cells' order of contrast). Two frames each. Drawn facing right;
    // the view mirrors them.
    static class CritterArt
    {
        public const int Frames = 2;

        static PixelImage Rows(string palette, string rows)
        {
            var colours = new Dictionary<char, Rgba>();
            foreach (string pair in palette.Split(' ')) colours[pair[0]] = Rgba.Hex(pair.Substring(2));
            return PixelImage.FromRows(colours, rows.Split('|'));
        }

        public static PixelImage Image(CritterKind kind, int frame)
        {
            bool second = frame % Frames == 1;
            switch (kind)
            {
                case CritterKind.Cricket:
                    // Crouched, and stretched out in a hop.
                    return second ? Rows("a=#7A6844 b=#2E241A c=#4A3E28", "...b.|aaaab|c...c") : Rows("a=#7A6844 b=#2E241A c=#4A3E28", "..b..|aaab.|c.c..");
                case CritterKind.Mouse:
                    // Running: the legs under it, then out.
                    return second ? Rows("a=#9A8A7A b=#D8A0A0 c=#2A2018 d=#6E6258", "....aa.|.aaaaac|baaaad.|.d...d.") : Rows("a=#9A8A7A b=#D8A0A0 c=#2A2018 d=#6E6258", "....aa.|.aaaaac|baaaad.|..d.d..");
                case CritterKind.LampMoth:
                    return second ? Rows("a=#D8C8A0 b=#8A7A5A", "...|aba|aba") : Rows("a=#D8C8A0 b=#8A7A5A", "a.a|aba|.b.");
                case CritterKind.Firefly:
                    // Bright, then dim.
                    return second ? Rows("a=#B8F25A b=#6E9A3A", "...|.a.|...") : Rows("a=#EEFFB8 b=#B8F25A", ".b.|bab|.b.");
                case CritterKind.Snail:
                    // Its foot drawn in, then stretched.
                    return second ? Rows("a=#C8A070 b=#8A6A40 c=#A8B878 d=#5A6A3A", "..bb..|.baab.|cccccd") : Rows("a=#C8A070 b=#8A6A40 c=#A8B878 d=#5A6A3A", "..bb..|.baab.|.cccd.");
                default:
                    return second ? Rows("a=#C8E890 b=#6A8A4A", "...|aba|aba") : Rows("a=#C8E890 b=#6A8A4A", "a.a|aba|.b.");
            }
        }

        // The critters that fly (no shadow, drawn over what stands) and those that glow in the dark (drawn over it).
        public static bool Flies(CritterKind kind) => kind == CritterKind.LampMoth || kind == CritterKind.Firefly || kind == CritterKind.SporeMoth;
        public static bool Glows(CritterKind kind) => kind == CritterKind.Firefly;

        // The biome's critters and how many of each in a hundred, by day and by night (fireflies come out at night).
        public static CritterKind Pick(Biome biome, int roll, bool night) => biome == Biome.Mushroom
            ? roll < (night ? 70 : 40) ? CritterKind.Firefly : roll < (night ? 85 : 70) ? CritterKind.Snail : CritterKind.SporeMoth
            : roll < 55 ? CritterKind.Cricket : CritterKind.Mouse;

        // The biome a kind belongs to: a critter comes into being where its biome is (a lamp moth at a dirt cave lamp).
        public static Biome HomeOf(CritterKind kind) => kind == CritterKind.Firefly || kind == CritterKind.Snail || kind == CritterKind.SporeMoth
            ? Biome.Mushroom : Biome.Cave;

        // Whether the biome has critters yet (B1a: the dirt cave and the mushroom forest).
        public static bool Lives(Biome biome) => biome == Biome.Cave || biome == Biome.Mushroom;

        public static PixelImage Mote(MoteKind kind) => kind switch
        {
            MoteKind.Dust => Rows("a=#FFD8A0", "a"),
            MoteKind.Drip => Rows("a=#A8C8E0 b=#E8F4FF", "a|b"),
            MoteKind.Splash => Rows("a=#A8C8E0", "a.a|.a."),
            MoteKind.Spore => Rows("a=#B8F25A b=#EEFFB8", "ab|aa"),
            _ => Rows("a=#B8F25A", "aa"),
        };
    }
}
