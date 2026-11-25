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

        // frame: 0 at rest; 1 and 2 are a swaying kind (Decor.Sways) nudged a pixel right and left above its foot, shown
        // for a moment when someone brushes past (Docs/BiomeRework.md 4-3 흔들림).
        public static PixelImage Image(DecorKind kind, Biome biome, int variant, int frame = 0)
        {
            var image = Still(kind, biome, variant);
            return frame != 0 && Decor.Sways(kind) ? Swayed(image, frame == 1 ? 1 : -1) : image;
        }

        // The rows above the foot shifted a pixel (the top two rows two), the foot and the shadow where they were.
        static PixelImage Swayed(PixelImage still, int way)
        {
            var g = new PixelImage(still.Width, still.Height);
            int foot = still.Height - 4;
            for (int y = 0; y < still.Height; y++)
            {
                int shift = y >= foot ? 0 : y < foot - 4 ? way * 2 : way;
                for (int x = 0; x < still.Width; x++)
                    if (still[x, y].A > 0) g[x + shift, y] = still[x, y];
            }
            return g;
        }

        static PixelImage Still(DecorKind kind, Biome biome, int variant)
        {
            // Reeds stand taller than the rest (묶음 B3a): their picture is ReedRise rows higher, its foot where the others' is.
            if (kind == DecorKind.Reeds) return Reeds(variant, (int)kind * 31 + variant * 7);
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
                    // The forest's little caps tan, some pink; the dirt cave's brown.
                    Caps(g, biome == Biome.Mushroom ? Ramp.Of(variant == 2 ? "#E88AB0" : "#D8A070") : Ramp.Of("#B8824E"), salt, variant);
                    break;
                case DecorKind.Web:
                    Web(g, variant);
                    break;
                case DecorKind.GlowGrass:
                    GlowGrass(g, salt, variant);
                    break;
                case DecorKind.Threads:
                    Threads(g, salt, variant);
                    break;
                case DecorKind.Moss:
                    MossClump(g, salt, variant);
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
                // The ice cave's and the sand cave's (묶음 B2a).
                case DecorKind.FrostGrass:
                    FrostGrass(g, salt, variant);
                    break;
                case DecorKind.Snow:
                    SnowPatch(g, salt, variant);
                    break;
                case DecorKind.DryGrass:
                    DryGrass(g, salt, variant);
                    break;
                // The crystal cave's and the lake's (묶음 B3a).
                case DecorKind.CrystalDust:
                    CrystalDust(g, salt, variant);
                    break;
                case DecorKind.Shells:
                    Shells(g, salt, variant);
                    break;
                case DecorKind.LilyPad:
                    LilyPad(g, variant);
                    break;
            }
            return g;
        }

        // A cobweb across the upper left corner where two rock faces meet (mirrored for the other corner): thin pale
        // threads, see-through, flat.
        static void Web(PixelImage g, int variant)
        {
            var thread = new Rgba(226, 220, 208, 150);
            var faint = new Rgba(226, 220, 208, 90);
            // Spokes out of the corner, then two or three arcs across them.
            for (int i = 0; i < 8; i++)
            {
                g.Blend(i, i, thread);
                g.Blend(i, 0, faint);
                g.Blend(0, i, faint);
                if (i < 6) g.Blend(i * 2 / 3, i, faint);
                if (i < 6) g.Blend(i, i * 2 / 3, faint);
            }
            foreach (int r in variant == 0 ? new[] { 3, 6 } : variant == 1 ? new[] { 2, 4, 7 } : new[] { 4, 7 })
                for (int i = 0; i <= r; i++) g.Blend(i, r - i, thread);
            if (variant == 2) g[5, 4] = Rgba.Hex("#3A2E28");
        }

        // A tuft of forest grass whose tips glow lime.
        static void GlowGrass(PixelImage g, int salt, int variant)
        {
            var ramp = Ramp.Of("#5E8A3A");
            var tip = Rgba.Hex("#B8F25A");
            var core = Rgba.Hex("#EEFFB8");
            Shadow(g, 6, 7);
            int blades = 4 + variant;
            for (int i = 0; i < blades; i++)
            {
                int x = 2 + i * 7 / blades + (int)(PixelNoise.Hash(i, 1, salt) % 2);
                int tall = 4 + (int)(PixelNoise.Hash(i, 2, salt) % 4);
                int lean = i < blades / 2 ? -1 : i > blades / 2 ? 1 : 0;
                for (int k = 0; k < tall; k++)
                {
                    int bx = x + (k > tall / 2 ? lean : 0);
                    g[bx, Height - 3 - k] = k == tall - 1 ? (i % 2 == 0 ? core : tip) : k == tall - 2 ? tip : k < 2 ? ramp.Shade : ramp.Main;
                }
            }
            for (int x = 1; x < 11; x++)
                if (g[x, Height - 3].A > 0) g[x, Height - 3] = ramp.Dark;
        }

        // White threads of mycelium branching over the floor: flat, see-through.
        static void Threads(PixelImage g, int salt, int variant)
        {
            var thread = new Rgba(216, 224, 200, 150);
            var faint = new Rgba(216, 224, 200, 80);
            int y = 5 + variant % 2;
            for (int x = 0; x < Width; x++)
            {
                y += (int)(PixelNoise.Hash(x, variant, salt) % 3) - 1;
                y = Math.Clamp(y, 2, Height - 3);
                g.Blend(x, y, thread);
                // Side branches.
                if (PixelNoise.Hash(x, 7, salt) % 4 == 0)
                {
                    int way = PixelNoise.Hash(x, 8, salt) % 2 == 0 ? -1 : 1;
                    g.Blend(x, y + way, faint);
                    g.Blend(x + 1, y + way * 2, faint);
                }
            }
            if (variant == 2) g[6, Math.Clamp(y - 1, 1, Height - 2)] = Rgba.Hex("#EEFFB8");
        }

        // A low clump of moss: flat, its upper left lit.
        static void MossClump(PixelImage g, int salt, int variant)
        {
            var moss = Ramp.Of("#56682E");
            float rx = 3.6f + variant * 0.7f, ry = 1.8f + (variant == 1 ? 0.6f : 0);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float dx = (x + 0.5f - 6) / rx, dy = (y + 0.5f - 6.5f) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1 + (PixelNoise.Value(x, y, salt) - 0.5f) * 0.5f) continue;
                    g[x, y] = -dx * 0.5f - dy * 0.8f > 0.35f ? moss.Light.Mix(moss.Main, 0.5f) : d > 0.6f ? moss.Shade : moss.Main;
                }
            }
            if (variant == 2) g[7, 5] = Rgba.Hex("#B8F25A");
        }

        // A tuft of grass frozen stiff: grey-blue blades with white rime at their tips.
        static void FrostGrass(PixelImage g, int salt, int variant)
        {
            var ramp = Ramp.Of("#7E9CB4");
            var rime = Rgba.Hex("#F0FAFF");
            Shadow(g, 6, 7);
            int blades = 4 + variant;
            for (int i = 0; i < blades; i++)
            {
                int x = 2 + i * 7 / blades + (int)(PixelNoise.Hash(i, 1, salt) % 2);
                int tall = 3 + (int)(PixelNoise.Hash(i, 2, salt) % 4);
                int lean = i < blades / 2 ? -1 : i > blades / 2 ? 1 : 0;
                for (int k = 0; k < tall; k++)
                {
                    int bx = x + (k > tall / 2 ? lean : 0);
                    g[bx, Height - 3 - k] = k == tall - 1 ? rime : k == tall - 2 ? ramp.Light : k < 2 ? ramp.Shade : ramp.Main;
                }
            }
            for (int x = 1; x < 11; x++)
                if (g[x, Height - 3].A > 0) g[x, Height - 3] = ramp.Dark;
        }

        // A shallow patch of snow: flat, its windward edge bright, a little see-through at its rim.
        static void SnowPatch(PixelImage g, int salt, int variant)
        {
            var snow = Rgba.Hex("#E4F0FA");
            float rx = 4.2f + variant * 0.6f, ry = 2f + (variant == 1 ? 0.7f : 0);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float dx = (x + 0.5f - 6) / rx, dy = (y + 0.5f - 6.5f) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1 + (PixelNoise.Value(x, y, salt) - 0.5f) * 0.6f) continue;
                    g[x, y] = d > 0.7f ? Rgba.Hex("#B4CCE0").WithAlpha(190) : -dx * 0.5f - dy * 0.8f > 0.3f ? Rgba.White : snow;
                }
            }
            if (variant == 2) g[8, 6] = Rgba.Hex("#A8DCF4");
        }

        // A tuft of dry grass: straw-yellow blades, taller than the cave's roots, a seed head on some.
        static void DryGrass(PixelImage g, int salt, int variant)
        {
            var ramp = Ramp.Of("#C8A860");
            Shadow(g, 6, 7);
            int blades = 4 + variant;
            for (int i = 0; i < blades; i++)
            {
                int x = 2 + i * 7 / blades + (int)(PixelNoise.Hash(i, 1, salt) % 2);
                int tall = 4 + (int)(PixelNoise.Hash(i, 2, salt) % 4);
                int lean = i < blades / 2 ? -1 : i > blades / 2 ? 1 : 0;
                for (int k = 0; k < tall; k++)
                {
                    int bx = x + (k > tall / 2 ? lean : 0);
                    g[bx, Height - 3 - k] = k == tall - 1 ? (i % 2 == 0 ? Rgba.Hex("#F0E0A0") : ramp.Light) : k < 2 ? ramp.Shade : ramp.Main;
                }
            }
            for (int x = 1; x < 11; x++)
                if (g[x, Height - 3].A > 0) g[x, Height - 3] = ramp.Dark;
        }

        // Crystal dust on the slate: a scatter of single grains, pink and sky and a white one that catches the light,
        // flat and half see-through.
        static void CrystalDust(PixelImage g, int salt, int variant)
        {
            var pink = Rgba.Hex("#FF7FD0");
            var sky = Rgba.Hex("#7FF2FF");
            int grains = 5 + variant;
            for (int i = 0; i < grains; i++)
            {
                int x = 1 + (int)(PixelNoise.Hash(i, 1, salt) % 9), y = 3 + (int)(PixelNoise.Hash(i, 2, salt) % 6);
                uint tone = PixelNoise.Hash(i, 3, salt) % 5;
                var grain = tone == 0 ? Rgba.Hex("#F4F0FF") : (tone % 2 == 1 ? pink : sky).WithAlpha(tone > 2 ? 150 : 215);
                // Each grain two pixels: the bright one and its dimmer side, so none is a lone speck.
                g[x, y] = grain;
                if (g[x + 1, y].A == 0) g[x + 1, y] = (tone % 2 == 1 ? pink : sky).WithAlpha(90);
            }
        }

        public const int ReedRise = 9;

        // A stand of reeds at the water's edge: tall green stalks, brown heads on some, bent blades at their feet.
        static PixelImage Reeds(int variant, int salt)
        {
            var g = new PixelImage(Width, Height + ReedRise);
            var ramp = Ramp.Of("#7A9440");
            var head = Ramp.Of("#8A5A3A");
            int foot = g.Height - 3;
            PropArt.Shade(g, 6, g.Height - 2, 8, 90);
            int stalks = 4 + variant;
            for (int i = 0; i < stalks; i++)
            {
                int x = 2 + i * 8 / stalks + (int)(PixelNoise.Hash(i, 1, salt) % 2);
                int tall = 8 + (int)(PixelNoise.Hash(i, 2, salt) % 8);
                int lean = i < stalks / 2 ? -1 : i > stalks / 2 ? 1 : 0;
                bool headed = PixelNoise.Hash(i, 3, salt) % 3 != 0;
                for (int k = 0; k < tall; k++)
                {
                    int bx = x + (k > tall * 2 / 3 ? lean : 0);
                    g[bx, foot - k] = k < 2 ? ramp.Shade : headed && k >= tall - 3 ? (k == tall - 1 ? head.Light : head.Main) : k == tall - 1 ? ramp.Light : i % 2 == 0 ? ramp.Main : ramp.Light.Mix(ramp.Main, 0.5f);
                }
                // A blade bent out low down.
                if (i % 2 == 0)
                {
                    g[x + (lean == 0 ? 1 : lean), foot - 3] = ramp.Main;
                    g[x + (lean == 0 ? 2 : lean * 2), foot - 4] = ramp.Light;
                }
            }
            for (int x = 0; x < Width; x++)
                if (g[x, foot].A > 200) g[x, foot] = ramp.Dark;
            return g;
        }

        // A few shells on the wet stone: a pale fan, a pink one, a whorl.
        static void Shells(PixelImage g, int salt, int variant)
        {
            var pale = Ramp.Of("#E8E0CC");
            var pink = Ramp.Of("#EC9A8A");
            Shadow(g, 6, 7 + variant);
            int foot = Height - 3;
            int count = 2 + variant % 2;
            for (int i = 0; i < count; i++)
            {
                int x = 1 + i * 4 + (int)(PixelNoise.Hash(i, 4, salt) % 2);
                var ramp = (i + variant) % 3 == 1 ? pink : pale;
                int y = foot - (i == 2 ? 2 : 0);
                g[x, y] = ramp.Shade;
                g[x + 1, y] = ramp.Dark;
                g[x + 2, y] = ramp.Dark;
                g[x, y - 1] = ramp.Light;
                g[x + 1, y - 1] = ramp.Main;
                g[x + 2, y - 1] = ramp.Shade;
                if (i == 0) g[x + 1, y - 2] = ramp.Light;
                Outline(g, x - 1, y - 3, 5, 5);
            }
        }

        // A lily pad lying on the water: a round green leaf with a notch, a smaller one by it, a lotus bud on one drawing.
        static void LilyPad(PixelImage g, int variant)
        {
            var leaf = Ramp.Of("#5E9A58");
            void Pad(float cx, float cy, float rx, float ry, int notch)
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy > 1) continue;
                        g[x, y] = -dx * 0.5f - dy * 0.8f > 0.3f ? leaf.Light.Mix(leaf.Main, 0.5f) : dx * dx + dy * dy > 0.62f ? leaf.Shade : leaf.Main;
                    }
                // The notch, cut toward one side, and a vein.
                g[(int)cx + notch, (int)cy] = Rgba.Clear;
                g[(int)cx + notch * 2, (int)cy] = Rgba.Clear;
                g[(int)cx - notch, (int)cy] = leaf.Shade;
            }
            Pad(variant == 1 ? 5 : 6, 6.5f, variant == 1 ? 3.2f : 4.2f, variant == 1 ? 2.2f : 2.8f, variant == 2 ? -1 : 1);
            if (variant != 0) Pad(variant == 1 ? 9.5f : 2.5f, variant == 1 ? 4.5f : 4.0f, 2.1f, 1.5f, 1);
            if (variant == 0)
            {
                // A lotus bud.
                g[5, 5] = Rgba.Hex("#FFD2E4");
                g[6, 5] = Rgba.Hex("#F090B4");
                g[5, 6] = Rgba.Hex("#F090B4");
                g[5, 4] = Rgba.Hex("#FFF0F6");
            }
        }

        // The stone of the biome's floor, a step lighter so it shows on it.
        static Ramp StoneOf(Biome biome) => biome switch
        {
            Biome.Mushroom => Ramp.Of("#5E6248"),
            Biome.Ice => Ramp.Of("#7A8C9C"),
            Biome.Crystal => Ramp.Of("#70768C"),
            Biome.Lava => Ramp.Of("#4E4440"),
            Biome.Abyss => Ramp.Of("#4A4058"),
            Biome.Lake => Ramp.Of("#5E7470"),
            Biome.Sand => Ramp.Of("#A88A62"),
            _ => Ramp.Of("#7A6450"),
        };

        static Ramp ShardOf(Biome biome, int variant) => biome switch
        {
            Biome.Ice => Ramp.Of("#A8DCF4"),
            Biome.Crystal => variant == 1 ? Ramp.Of("#7FF2FF") : Ramp.Of("#FF7FD0"),
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
            // Two, three, or (the third drawing) a tall one with a small one at its foot.
            int count = 2 + variant % 2;
            for (int i = 0; i < count; i++)
            {
                int x = 1 + i * 4 + (int)(PixelNoise.Hash(i, 5, salt) % 2) + (variant == 2 ? 2 : 0);
                int tall = variant == 2 ? (i == 0 ? 5 : 1) : 2 + (int)(PixelNoise.Hash(i, 6, salt) % 3) + (i == 1 ? 1 : 0);
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
