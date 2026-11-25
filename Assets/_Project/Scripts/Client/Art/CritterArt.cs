using System;
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
        // The ice cave (묶음 B2a): a white hare that runs from whoever comes near, ice moths.
        SnowHare,
        IceMoth,
        // The sand cave: a lizard that darts and stops, a scarab rolling its ball.
        Lizard,
        Scarab,
        // The crystal cave (묶음 B3a): a snail whose shell is a crystal, moths that glitter pink.
        CrystalSnail,
        GlitterMoth,
        // The underground lake: a frog on the bank that hops away, marsh lights over the water, the shadow of a fish
        // in a pool.
        Frog,
        MarshFly,
        FishShadow,
        // The lava zone (묶음 B4a): a young fire newt that darts and stops, moths like sparks.
        FireNewt,
        EmberMoth,
        // The abyss: a jelly of light that drifts, rising and sinking; moths of shadow.
        VoidJelly,
        ShadowMoth,
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
        // The ice cave (묶음 B2a): a snowflake drifting down, a glint of frost on the floor.
        Flake,
        Glint,
        // The sand cave: a grain sifting down a rock face, dust blown low over the floor.
        Grain,
        SandDust,
        // The crystal cave (묶음 B3a): a point of light that flashes pink or sky on the floor, and now and then a short
        // streak of the rainbow where the light is bent.
        Sparkle,
        SkySparkle,
        Prism,
        // The lake: a wisp of mist low over the bank, a drop falling from the roof, the ring it makes on the water.
        Mist,
        LakeDrip,
        Ripple,
        // The lava zone (묶음 B4a): a spark rising, a flake of ash sinking, the air shimmering over the lava (not on a
        // phone).
        Ember,
        AshFlake,
        Haze,
        // The abyss: a mote of starlight drawn upward, faster as it goes; a star's glint on the floor.
        StarMote,
        StarGlint,
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
            foreach (string pair in palette.Split(' '))
            {
                // #RRGGBB, or #RRGGBBAA for what is see-through (mist, a ring on the water, a fish's shadow).
                string hex = pair.Substring(2);
                colours[pair[0]] = hex.Length == 9 ? Rgba.Hex(hex.Substring(0, 7)).WithAlpha(Convert.ToInt32(hex.Substring(7), 16)) : Rgba.Hex(hex);
            }
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
                case CritterKind.SnowHare:
                    // Sitting with its ears up, and stretched out in a bound.
                    return second ? Rows("a=#E8EEF4 b=#C4D0DC c=#2A2A3A d=#A8B8C8 e=#FFFFFF", "......bb|.....aaa|eaaaaaac|.aaaaa..|d......d")
                        : Rows("a=#E8EEF4 b=#C4D0DC c=#2A2A3A d=#A8B8C8 e=#FFFFFF", ".....b.b|.....aaa|.aaaaaac|eaaaaaa.|.d...d..");
                case CritterKind.IceMoth:
                    return second ? Rows("a=#D4F0FF b=#7AA8C8", "...|aba|aba") : Rows("a=#D4F0FF b=#7AA8C8", "a.a|aba|.b.");
                case CritterKind.Lizard:
                    // Its legs under it, then splayed; the tail curls.
                    return second ? Rows("a=#B89A5A b=#8A7040 c=#D8BC7A d=#6A5430", "b......|.baaaac|..d..d.") : Rows("a=#B89A5A b=#8A7040 c=#D8BC7A d=#6A5430", ".......|bbaaaac|.d..d..");
                case CritterKind.Scarab:
                    // Pushing its ball, legs stepping.
                    return second ? Rows("a=#34466A b=#8A6A40 c=#1E2638 e=#7A9AD0 f=#A88454", ".ea.fb|aaaabb|.c.c..") : Rows("a=#34466A b=#8A6A40 c=#1E2638 e=#7A9AD0 f=#A88454", ".ea.fb|aaaabb|c.c...");
                case CritterKind.CrystalSnail:
                    // Its shell a pink crystal with a pale facet; the foot drawn in, then stretched.
                    return second ? Rows("a=#FF7FD0 b=#C84CA2 c=#9AA4BC d=#5E6880 e=#FFD2F0", "..eb..|.eaab.|cccccd") : Rows("a=#FF7FD0 b=#C84CA2 c=#9AA4BC d=#5E6880 e=#FFD2F0", "..eb..|.eaab.|.cccd.");
                case CritterKind.GlitterMoth:
                    // Bright wings, then folded and dim.
                    return second ? Rows("a=#FF8AD8 b=#C84CA2", "...|aba|.b.") : Rows("a=#FFD2F0 b=#FF8AD8 c=#F4F0FF", "b.b|bcb|.b.");
                case CritterKind.Frog:
                    // Squatting, and stretched out in a hop.
                    return second ? Rows("a=#6E9A54 b=#48703A c=#1E2A1C d=#C8DC8A", "....ac|baaaad|b....b") : Rows("a=#6E9A54 b=#48703A c=#1E2A1C d=#C8DC8A", "...ac.|.aaaad|bb.bb.");
                case CritterKind.MarshFly:
                    // Bright, then dim.
                    return second ? Rows("a=#70E6D8 b=#3E9488", "...|.a.|...") : Rows("a=#E0FFF8 b=#70E6D8", ".b.|bab|.b.");
                case CritterKind.FishShadow:
                    // A dark shape under the water, its tail one way and then the other.
                    return second ? Rows("a=#0C3038C0 b=#0C303890", "b.aaaa.|.baaaaa|b.aaaa.") : Rows("a=#0C3038C0 b=#0C303890", ".baaaa.|b.aaaaa|.baaaa.");
                case CritterKind.FireNewt:
                    // A dark body with an ember stripe down its back; its legs under it, then splayed.
                    return second ? Rows("a=#5A3A30 b=#3A2420 c=#FF8A3C d=#2A1A18 e=#FFC86A", "b......|.bcecaa|..d..d.") : Rows("a=#5A3A30 b=#3A2420 c=#FF8A3C d=#2A1A18 e=#FFC86A", ".......|bbcecaa|.d..d..");
                case CritterKind.EmberMoth:
                    // A spark with wings: bright, then folded and dull.
                    return second ? Rows("a=#FF8A3C b=#B8401E", "...|aba|.b.") : Rows("a=#FFD86A b=#FF8A3C c=#FFF4C0", "b.b|bcb|.b.");
                case CritterKind.VoidJelly:
                    // A bell of pale light and its trailing threads, drawn in and let out.
                    return second ? Rows("a=#E6D6FFE0 b=#A050FFD0 c=#6A30C0A0", ".aaa.|abbba|.bbb.|.c.c.|.....") : Rows("a=#E6D6FFE0 b=#A050FFD0 c=#6A30C0A0", ".aaa.|abbba|abbba|c.c.c|.c.c.");
                case CritterKind.ShadowMoth:
                    // Dark wings edged in violet.
                    return second ? Rows("a=#2A1E40 b=#6A4AA0", "...|aba|aba") : Rows("a=#2A1E40 b=#6A4AA0", "b.b|aba|.a.");
                default:
                    return second ? Rows("a=#C8E890 b=#6A8A4A", "...|aba|aba") : Rows("a=#C8E890 b=#6A8A4A", "a.a|aba|.b.");
            }
        }

        // The critters that fly (no shadow, drawn over what stands) and those that glow in the dark (drawn over it).
        public static bool Flies(CritterKind kind) => kind == CritterKind.LampMoth || kind == CritterKind.Firefly || kind == CritterKind.SporeMoth || kind == CritterKind.IceMoth
            || kind == CritterKind.GlitterMoth || kind == CritterKind.MarshFly || kind == CritterKind.EmberMoth || kind == CritterKind.VoidJelly || kind == CritterKind.ShadowMoth;
        public static bool Glows(CritterKind kind) => kind == CritterKind.Firefly || kind == CritterKind.GlitterMoth || kind == CritterKind.MarshFly || kind == CritterKind.EmberMoth
            || kind == CritterKind.VoidJelly;
        // What lives in the water: it comes into being on a pool's tile and never leaves the pool.
        public static bool Swims(CritterKind kind) => kind == CritterKind.FishShadow;

        // The biome's critters and how many of each in a hundred, by day and by night (fireflies come out at night).
        public static CritterKind Pick(Biome biome, int roll, bool night) => biome == Biome.Mushroom
            ? roll < (night ? 70 : 40) ? CritterKind.Firefly : roll < (night ? 85 : 70) ? CritterKind.Snail : CritterKind.SporeMoth
            : biome == Biome.Ice ? roll < 45 ? CritterKind.SnowHare : CritterKind.IceMoth
            : biome == Biome.Sand ? roll < 55 ? CritterKind.Lizard : CritterKind.Scarab
            : biome == Biome.Crystal ? roll < 40 ? CritterKind.CrystalSnail : CritterKind.GlitterMoth
            // Marsh lights come out at night, as the forest's fireflies.
            : biome == Biome.Lake ? roll < (night ? 30 : 55) ? CritterKind.Frog : CritterKind.MarshFly
            : biome == Biome.Lava ? roll < 45 ? CritterKind.FireNewt : CritterKind.EmberMoth
            : biome == Biome.Abyss ? roll < 55 ? CritterKind.VoidJelly : CritterKind.ShadowMoth
            : roll < 55 ? CritterKind.Cricket : CritterKind.Mouse;

        // The biome a kind belongs to: a critter comes into being where its biome is (a lamp moth at a dirt cave lamp).
        public static Biome HomeOf(CritterKind kind) => kind == CritterKind.Firefly || kind == CritterKind.Snail || kind == CritterKind.SporeMoth
            ? Biome.Mushroom : kind == CritterKind.SnowHare || kind == CritterKind.IceMoth ? Biome.Ice : kind == CritterKind.Lizard || kind == CritterKind.Scarab ? Biome.Sand
            : kind == CritterKind.CrystalSnail || kind == CritterKind.GlitterMoth ? Biome.Crystal
            : kind == CritterKind.Frog || kind == CritterKind.MarshFly || kind == CritterKind.FishShadow ? Biome.Lake
            : kind == CritterKind.FireNewt || kind == CritterKind.EmberMoth ? Biome.Lava : kind == CritterKind.VoidJelly || kind == CritterKind.ShadowMoth ? Biome.Abyss
            : Biome.Cave;

        // The biome a mote belongs to (the snail's trail is the forest's).
        public static Biome HomeOf(MoteKind kind) => kind == MoteKind.Spore || kind == MoteKind.Trail ? Biome.Mushroom
            : kind == MoteKind.Flake || kind == MoteKind.Glint ? Biome.Ice : kind == MoteKind.Grain || kind == MoteKind.SandDust ? Biome.Sand
            : kind == MoteKind.Sparkle || kind == MoteKind.SkySparkle || kind == MoteKind.Prism ? Biome.Crystal
            : kind == MoteKind.Mist || kind == MoteKind.LakeDrip || kind == MoteKind.Ripple ? Biome.Lake
            : kind == MoteKind.Ember || kind == MoteKind.AshFlake || kind == MoteKind.Haze ? Biome.Lava : kind == MoteKind.StarMote || kind == MoteKind.StarGlint ? Biome.Abyss : Biome.Cave;

        // Whether the biome has critters yet (B1a: the dirt cave and the mushroom forest).
        public static bool Lives(Biome biome) => biome == Biome.Cave || biome == Biome.Mushroom || biome == Biome.Ice || biome == Biome.Sand
            || biome == Biome.Crystal || biome == Biome.Lake || biome == Biome.Lava || biome == Biome.Abyss;

        public static PixelImage Mote(MoteKind kind) => kind switch
        {
            MoteKind.Dust => Rows("a=#FFD8A0", "a"),
            MoteKind.Drip => Rows("a=#A8C8E0 b=#E8F4FF", "a|b"),
            MoteKind.Splash => Rows("a=#A8C8E0", "a.a|.a."),
            MoteKind.Spore => Rows("a=#B8F25A b=#EEFFB8", "ab|aa"),
            MoteKind.Flake => Rows("a=#FFFFFF b=#CFE8FA", "ab"),
            MoteKind.Glint => Rows("a=#F4FCFF b=#A8DCFF", ".b.|bab|.b."),
            MoteKind.Grain => Rows("a=#E8CC94 b=#C49C62", "a|b"),
            MoteKind.SandDust => Rows("a=#D8B882", "aa"),
            MoteKind.Sparkle => Rows("a=#F4F0FF b=#FF8AD8", ".b.|bab|.b."),
            MoteKind.SkySparkle => Rows("a=#F4F0FF b=#8AF0FF", ".b.|bab|.b."),
            // Light bent into its colours: a short slanted streak, red to violet.
            MoteKind.Prism => Rows("a=#FF7A8AC0 b=#FFD27AC0 c=#A8F08AC0 d=#7FD8FFC0 e=#C8A0FFC0", "....a|...ab|..bc.|.cd..|de..."),
            MoteKind.Mist => Rows("a=#CFEFEA50 b=#CFEFEA30", ".baaab.|baaaaab|.bbaab."),
            MoteKind.LakeDrip => Rows("a=#8AE8E0 b=#E0FFF8", "a|b"),
            MoteKind.Ripple => Rows("a=#8AE8E0C0 b=#8AE8E070", ".baaab.|a.....a|.baaab."),
            MoteKind.Ember => Rows("a=#FFD86A b=#FF7A30", "a|b"),
            MoteKind.AshFlake => Rows("a=#8A807A b=#5E5652", "ab"),
            // The air over the lava: a pale wavering thread, nearly clear.
            MoteKind.Haze => Rows("a=#FFE0B040 b=#FFE0B020", ".a.|a..|.a.|..b|.b."),
            MoteKind.StarMote => Rows("a=#F6F0FF b=#A050FF", "a|b|b"),
            MoteKind.StarGlint => Rows("a=#F6F0FF b=#A050FF", ".b.|bab|.b."),
            _ => Rows("a=#B8F25A", "aa"),
        };
    }
}
