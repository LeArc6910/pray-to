using System;
using Game.Core;

namespace Game.Client
{
    // The biomes' small looked-at things (Decor, Docs/BiomeRework.md 4-3 꾸밈 층, 묶음 B0): pebbles, a tuft of roots or
    // reeds, little caps, shards, embers, bones, a few pixels each in the biome's colours, lit from the upper left like
    // everything else, with a crisp shadow under them and a dark contact row. Low and muted (Dead Cells' order of
    // contrast: the floor's things below the monsters), so none is mistaken for something to pick up or fight. Three
    // drawings of each; WorldView mirrors and nudges them by the tile (DecorPick). B0's proof set; the biome packages
    // add their own sets.
    static class DecorArt
    {
        public const int Width = 12, Height = 11;
        static readonly Rgba Ink = Rgba.Hex("#140E18");

        public static PixelImage Image(DecorKind kind, Biome biome, int variant)
        {
            var g = new PixelImage(Width, Height);
            int salt = (int)kind * 31 + variant * 7 + (int)biome * 131;
            switch (kind)
            {
                case DecorKind.Pebbles:
                    Pebbles(g, StoneOf(biome), salt, variant);
                    break;
                case DecorKind.Tuft:
                    Tuft(g, biome == Biome.Lake ? Ramp.Of("#6E9A54") : Ramp.Of("#B8A27A"), salt, variant, biome == Biome.Lake);
                    break;
                case DecorKind.Caps:
                    Caps(g, biome == Biome.Mushroom ? Ramp.Of("#8AC85A") : Ramp.Of("#C88A5A"), salt, variant);
                    break;
                case DecorKind.Shards:
                    Shards(g, ShardOf(biome, variant), salt, variant);
                    break;
                case DecorKind.Embers:
                    Embers(g, salt, variant);
                    break;
                case DecorKind.Bones:
                    Bones(g, variant);
                    break;
            }
            return g;
        }

        // The stone of the biome's floor, a step lighter so it shows on it.
        static Ramp StoneOf(Biome biome) => biome switch
        {
            Biome.Mushroom => Ramp.Of("#6E6A5A"),
            Biome.Ice => Ramp.Of("#7A8C9C"),
            Biome.Crystal => Ramp.Of("#6E7484"),
            Biome.Lava => Ramp.Of("#4E4440"),
            Biome.Abyss => Ramp.Of("#4A4058"),
            Biome.Lake => Ramp.Of("#5E7470"),
            Biome.Sand => Ramp.Of("#A88A62"),
            _ => Ramp.Of("#7A6A5A"),
        };

        static Ramp ShardOf(Biome biome, int variant) => biome switch
        {
            Biome.Ice => Ramp.Of("#A8DCF4"),
            Biome.Crystal => variant == 1 ? Ramp.Of("#7AC8F0") : Ramp.Of("#E88AC8"),
            Biome.Abyss => Ramp.Of("#9A6AE0"),
            _ => Ramp.Of("#8A9AB0"),
        };

        static void Shadow(PixelImage g, float cx, float width) => PropArt.Shade(g, cx, Height - 2, width, 90);

        static void Pebbles(PixelImage g, Ramp ramp, int salt, int variant)
        {
            int count = 2 + variant % 2;
            Shadow(g, 6, 8 + variant);
            for (int i = 0; i < count; i++)
            {
                int w = 2 + (int)(PixelNoise.Hash(i, variant, salt) % 2) + (i == 0 ? 1 : 0);
                int x = 1 + i * 4 - (i == 2 ? 2 : 0) + (int)(PixelNoise.Hash(i, 3, salt) % 2);
                int y = Height - 3 - (i == 2 ? 2 : 0);
                Stone(g, x, y, w, ramp);
            }
        }

        // A stone w wide sitting with its foot on row y: its top lit, its foot the contact row, in a dark outline.
        static void Stone(PixelImage g, int x, int y, int w, Ramp ramp)
        {
            for (int i = 0; i < w; i++)
            {
                g[x + i, y] = ramp.Dark;
                g[x + i, y - 1] = i == 0 ? ramp.Main : ramp.Shade;
                if (w > 2) g[x + i, y - 2] = i < w - 1 ? ramp.Light : ramp.Main;
            }
            Outline(g, x - 1, y - (w > 2 ? 3 : 2), w + 2, w > 2 ? 4 : 3);
        }

        static void Tuft(PixelImage g, Ramp ramp, int salt, int variant, bool reeds)
        {
            Shadow(g, 6, 7);
            int blades = 4 + variant;
            for (int i = 0; i < blades; i++)
            {
                int x = 3 + i * 6 / blades + (int)(PixelNoise.Hash(i, 1, salt) % 2);
                int tall = (reeds ? 5 : 3) + (int)(PixelNoise.Hash(i, 2, salt) % (reeds ? 4 : 3));
                int lean = i < blades / 2 ? -1 : i > blades / 2 ? 1 : 0;
                for (int k = 0; k < tall; k++)
                {
                    int bx = x + (k > tall / 2 ? lean : 0);
                    g[bx, Height - 3 - k] = k == tall - 1 ? ramp.Light : k < 2 ? ramp.Shade : ramp.Main;
                }
                if (reeds) g[x + lean, Height - 3 - tall] = Rgba.Hex("#8A5A3A");
            }
            for (int x = 2; x < 10; x++)
                if (g[x, Height - 3].A > 0) g[x, Height - 3] = ramp.Dark;
        }

        static void Caps(PixelImage g, Ramp cap, int salt, int variant)
        {
            var stem = Ramp.Of("#D8CCB0");
            Shadow(g, 6, 8);
            int count = 2 + variant % 2;
            for (int i = 0; i < count; i++)
            {
                int x = 1 + i * 4 + (int)(PixelNoise.Hash(i, 5, salt) % 2);
                int tall = 2 + (int)(PixelNoise.Hash(i, 6, salt) % 3) + (i == 1 ? 1 : 0);
                int foot = Height - 3;
                for (int k = 0; k < tall; k++) g[x + 1, foot - k] = k == 0 ? stem.Shade : stem.Main;
                int top = foot - tall;
                g[x, top] = cap.Main;
                g[x + 1, top] = cap.Main;
                g[x + 2, top] = cap.Shade;
                g[x + 1, top - 1] = cap.Light;
                g[x, top - 1] = cap.Light;
                g[x + 2, top - 1] = cap.Main;
                Outline(g, x - 1, top - 2, 5, tall + 3);
            }
        }

        static void Shards(PixelImage g, Ramp ramp, int salt, int variant)
        {
            Shadow(g, 6, 8);
            int count = 2 + (variant == 2 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                int x = 2 + i * 3 + (int)(PixelNoise.Hash(i, 8, salt) % 2);
                int tall = 3 + (int)(PixelNoise.Hash(i, 9, salt) % 3) + (i == 0 ? 2 : 0);
                int foot = Height - 3;
                for (int k = 0; k < tall; k++)
                {
                    bool tip = k == tall - 1;
                    g[x, foot - k] = k == 0 ? ramp.Dark : tip ? ramp.Light : ramp.Light.Mix(ramp.Main, 0.4f);
                    if (!tip) g[x + 1, foot - k] = k == 0 ? ramp.Dark : ramp.Shade;
                }
                Outline(g, x - 1, foot - tall - 1, 4, tall + 2);
            }
        }

        // A dark lump of rock with an orange crack glowing in it.
        static void Embers(PixelImage g, int salt, int variant)
        {
            var rock = Ramp.Of("#4A3A36");
            var glow = Rgba.Hex("#FF8A3C");
            var core = Rgba.Hex("#FFD07A");
            Shadow(g, 6, 8);
            int w = 4 + variant;
            int x0 = (Width - w) / 2, foot = Height - 3;
            for (int i = 0; i < w; i++)
            {
                g[x0 + i, foot] = rock.Dark;
                g[x0 + i, foot - 1] = i < w / 2 ? rock.Main : rock.Shade;
                if (i > 0 && i < w - 1) g[x0 + i, foot - 2] = i < w / 2 ? rock.Light : rock.Main;
            }
            int crack = x0 + 1 + (int)(PixelNoise.Hash(variant, 1, salt) % Math.Max(1, w - 2));
            g[crack, foot - 1] = glow;
            g[crack + 1, foot - 1] = variant == 0 ? glow : core;
            Outline(g, x0 - 1, foot - 3, w + 2, 4);
        }

        static void Bones(PixelImage g, int variant)
        {
            var bone = Ramp.Of("#E2DCCA");
            Shadow(g, 6, 8);
            int foot = Height - 3;
            // A long bone lying across, its knobbed ends.
            for (int x = 2; x <= 8; x++) g[x, foot - 1] = x == 2 || x == 8 ? bone.Main : bone.Light;
            g[2, foot] = bone.Shade;
            g[8, foot] = bone.Shade;
            g[1, foot - 1] = bone.Main;
            g[9, foot - 1] = bone.Shade;
            for (int x = 1; x <= 9; x++) if (g[x, foot].A == 0) g[x, foot] = bone.Dark;
            if (variant == 1)
            {
                // A second, shorter one over it.
                for (int x = 5; x <= 9; x++) g[x, foot - 3] = x == 5 ? bone.Light : bone.Main;
                g[10, foot - 3] = bone.Shade;
                g[5, foot - 2] = bone.Shade;
            }
            else if (variant == 2)
            {
                // A little skull at its end, its eye dark.
                for (int x = 1; x <= 3; x++)
                    for (int y = foot - 4; y <= foot - 2; y++) g[x, y] = y == foot - 4 || x == 1 ? bone.Light : bone.Main;
                g[2, foot - 3] = Ink;
                g[3, foot - 2] = bone.Shade;
            }
            Outline(g, 0, foot - (variant > 0 ? 4 : 2), 12, variant > 0 ? 6 : 4);
        }

        // Rings what is drawn inside the box in the dark outline.
        static void Outline(PixelImage g, int left, int top, int w, int h)
        {
            var copy = g.Copy();
            for (int y = top; y < top + h; y++)
            {
                for (int x = left; x < left + w; x++)
                {
                    if (copy[x, y].A >= 200) continue;
                    if (copy[x - 1, y].A >= 200 || copy[x + 1, y].A >= 200 || copy[x, y - 1].A >= 200 || copy[x, y + 1].A >= 200) g[x, y] = Ink;
                }
            }
        }
    }
}
