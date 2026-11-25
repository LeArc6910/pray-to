using System;
using Game.Core;

namespace Game.Client
{
    // The lava zone's and the void abyss's props (Docs/BiomeRework.md 4-1 용암 지대·공허의 심연, 묶음 B4a), drawn like the
    // biomes' before them: shaded shapes lit from the upper left, a dark rim, a contact row, a crisp shadow under the
    // foot, three drawings each by the tile.
    //  - The lava zone, a forge of basalt that never cools: near-black stone whose cracks glow orange, black glass,
    //    slag, things that burn.
    //  - The abyss, broken ground hanging over nothing: violet-black stone that floats (its shadow lies apart from
    //    it), crystals of the void, chains that hold nothing. The only biome that is violet.
    static partial class BiomeArt
    {
        static readonly Ramp Basalt = new Ramp(Rgba.Hex("#7C6C66"), Rgba.Hex("#544642"), Rgba.Hex("#3A2E2C"), Rgba.Hex("#221A19"));
        static readonly Ramp Obsidian = new Ramp(Rgba.Hex("#A4AAC8"), Rgba.Hex("#3C3A52"), Rgba.Hex("#24222E"), Rgba.Hex("#121018"));
        static readonly Ramp Slag = new Ramp(Rgba.Hex("#A89A90"), Rgba.Hex("#74655E"), Rgba.Hex("#4E423E"), Rgba.Hex("#2C2424"));
        static readonly Ramp FlameLeaf = new Ramp(Rgba.Hex("#FFE07A"), Rgba.Hex("#FF9A3C"), Rgba.Hex("#E0541E"), Rgba.Hex("#8E2A14"));
        static readonly Ramp Charred = new Ramp(Rgba.Hex("#6A5248"), Rgba.Hex("#44322C"), Rgba.Hex("#2C201C"), Rgba.Hex("#1A1210"));
        static readonly Rgba Seam = Rgba.Hex("#FF6A20"), SeamCore = Rgba.Hex("#FFC040"), SeamDim = Rgba.Hex("#B83A14"), EmberGlow = Rgba.Hex("#FF7A30"), Rust = Rgba.Hex("#B8562A");
        static readonly Rgba ObsidianGlint = Rgba.Hex("#EEF0FF");

        static readonly Ramp VoidStone = new Ramp(Rgba.Hex("#8474B4"), Rgba.Hex("#54447A"), Rgba.Hex("#362A54"), Rgba.Hex("#1E1634"));
        static readonly Ramp VoidGem = new Ramp(Rgba.Hex("#E2C8FF"), Rgba.Hex("#A050FF"), Rgba.Hex("#6A30C0"), Rgba.Hex("#3A1870"));
        static readonly Ramp VineBark = new Ramp(Rgba.Hex("#9E88BC"), Rgba.Hex("#6A5484"), Rgba.Hex("#46365C"), Rgba.Hex("#2A1E3C"));
        static readonly Rgba VoidGlow = Rgba.Hex("#A050FF"), Starlight = Rgba.Hex("#E6D6FF"), VoidGlint = Rgba.Hex("#F6F0FF");

        static bool LavaVoidMirrors(TileType type) => type == TileType.ObsidianSpike || type == TileType.FireBush || type == TileType.SlagHeap || type == TileType.CooledBoulder
            || type == TileType.LavaRock || type == TileType.VoidCrystal || type == TileType.TwistedVine || type == TileType.FloatingRock;

        static int LavaVoidCanopy(TileType type) => type switch
        {
            TileType.BasaltColumns => 14,
            TileType.ShatteredPillar => 14,
            TileType.FloatingRock => 10,
            _ => 0,
        };

        static PixelImage LavaVoidProp(TileType type, int variant) => type switch
        {
            TileType.ObsidianSpike => ObsidianSpike(variant),
            TileType.FireBush => FireBush(variant),
            TileType.SlagHeap => SlagHeap(variant),
            TileType.CooledBoulder => CooledBoulder(variant),
            TileType.BasaltColumns => BasaltColumns(variant),
            TileType.LavaRock => LavaRock(variant),
            TileType.VoidCrystal => VoidCrystal(variant),
            TileType.TwistedVine => TwistedVine(variant),
            TileType.ObeliskShard => ObeliskShard(variant),
            TileType.ChainPost => ChainPost(variant),
            TileType.FloatingRock => FloatingRock(variant),
            TileType.ShatteredPillar => ShatteredPillar(variant),
            _ => null,
        };

        // A glowing crack over what is drawn: a line of the seam's orange with a hot core at some of its pixels.
        static void GlowSeam(Kit k, int salt, params int[] xy)
        {
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                int x0 = xy[i], y0 = xy[i + 1], x1 = xy[i + 2], y1 = xy[i + 3];
                int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                for (int j = 0; j <= steps; j++)
                {
                    float t = steps == 0 ? 0 : j / (float)steps;
                    int x = (int)MathF.Round(x0 + (x1 - x0) * t), y = (int)MathF.Round(y0 + (y1 - y0) * t);
                    if (!k.I.IsSet(x, y)) continue;
                    uint roll = PixelNoise.Hash(x, y, salt) % 5;
                    k.Px(x, y, roll == 0 ? SeamCore : roll == 1 ? SeamDim : Seam);
                }
            }
        }

        // ---- The lava zone ----

        // Spikes of obsidian: black glass in long sharp shards, a hard pale edge down each, a white glint. Dug for its
        // ore with the pickaxe the veins want.
        static PixelImage ObsidianSpike(int variant)
        {
            var k = new Kit(14, 26);
            if (variant == 0)
            {
                k.Spike(10f, 26, 2.4f, 12f, 12, Obsidian);
                k.Spike(6f, 26, 3.4f, 5.5f, 0, Obsidian);
                k.Spike(2.5f, 26, 1.8f, 1.5f, 17, Obsidian);
                k.Ln(5, 5, 4, 22, Obsidian.Light).Px(ObsidianGlint, 5, 7, 4, 13, 10, 16);
            }
            else if (variant == 1)
            {
                // Two leaning apart.
                k.Spike(4.5f, 26, 2.8f, 2f, 4, Obsidian);
                k.Spike(9.5f, 26, 2.8f, 12.5f, 8, Obsidian);
                k.Ln(2, 8, 3, 22, Obsidian.Light).Px(ObsidianGlint, 2, 9, 11, 12, 3, 16);
            }
            else
            {
                // Snapped: a thick stump with a slanting bright break, a splinter beside it.
                k.Spike(7f, 26, 4.4f, 6.5f, 2, Obsidian);
                for (int y = 0; y < 12; y++)
                    for (int x = 0; x < 14; x++)
                        if (y < 11 - (x - 3) / 2) k.Px(x, y, Rgba.Clear);
                for (int x = 3; x < 12; x++)
                    if (k.I.IsSet(x, 12 - (x - 3) / 2)) k.Px(x, 12 - (x - 3) / 2, x < 7 ? ObsidianGlint : Obsidian.Light);
                k.Spike(11.5f, 26, 1.6f, 12.5f, 17, Obsidian);
                k.Px(ObsidianGlint, 5, 16);
            }
            // Chips of basalt and glass round the foot.
            k.Dome(7f, 26, 6.5f, 2.2f, Basalt);
            k.Px(Obsidian.Light, 3, 25).Px(Obsidian.Main, 10, 25);
            return Stand(k, 11);
        }

        // A bush that burns and is not burnt: charred twigs, leaves of flame at their ends, a glow at its root. Picked
        // for its pepper.
        static PixelImage FireBush(int variant)
        {
            var k = new Kit(16, 17);
            // The twigs, forking up from one root.
            k.Ln(8, 16, 8, 10, Charred.Main, 1).Ln(7, 16, 7, 12, Charred.Light);
            k.Ln(8, 11, 4, 7, Charred.Main).Ln(8, 10, 12, 6, Charred.Shade).Ln(8, 9, 8, 4, Charred.Main);
            if (variant != 1) k.Ln(5, 9, 2, 9, Charred.Shade).Ln(11, 8, 14, 9, Charred.Shade);
            // The leaves: small flames, pale at the heart.
            void Flame(float x, float y, float r)
            {
                k.Ell(x, y, r, r + 0.6f, FlameLeaf);
                k.Px((int)x, (int)(y - r - 0.6f), FlameLeaf.Main).Px((int)x, (int)y, FlameLeaf.Light);
            }
            Flame(4f, 6f, 2.2f);
            Flame(12f, 5f, 2.2f);
            Flame(8f, 3.2f, 2.6f);
            if (variant != 1)
            {
                Flame(2f, 9.5f, 1.4f);
                Flame(14f, 9.5f, 1.4f);
            }
            if (variant == 2) Flame(8f, 8.5f, 1.6f);
            k.Px(Rgba.Hex("#FFF4C0"), 8, 3, 4, 6, 12, 5);
            // Peppers hanging, and the root's own glow.
            k.Px(Rgba.Hex("#E03A2A"), 6, 11, variant == 0 ? 10 : 11, 10).Px(Rgba.Hex("#8E1E1A"), 6, 12);
            k.Dome(8f, 17, 4.5f, 1.8f, Basalt);
            k.Px(Seam, 6, 16, 9, 16);
            return Halo(Stand(k, 11), EmberGlow, (-7, 5), (7, 4), (0, 0), (-5, 1), (5, 0), (-8, 9), (8, 9));
        }

        // A heap of slag: the dull lumps a forge throws out, a glint of metal in them, rust, and on one drawing a seam
        // still hot.
        static PixelImage SlagHeap(int variant)
        {
            var k = new Kit(16, 12);
            k.Ell(5f, 8.5f, 4.6f, 3.4f, Slag);
            k.Ell(11f, 9f, 4.6f, 3f, Slag);
            k.Ell(8f, 5.5f, 4.4f, 3.6f, Slag);
            if (variant == 1) k.Ell(13f, 6f, 2.4f, 2.2f, Slag);
            // Pits, rust, a bead of bright metal.
            k.Px(Slag.Dark, 6, 6, 10, 8, 4, 9, 12, 10).Px(Rust, 7, 8, 11, 6, 3, 8).Px(Rgba.Hex("#E8E4DC"), 8, 3, 5, 7);
            if (variant == 2) GlowSeam(k, 71, 6, 5, 9, 7, 9, 7, 11, 8);
            if (variant == 0) k.Px(Rust.Scale(0.8f), 9, 10, 13, 9);
            return Stand(k, 14);
        }

        // A boulder of cooled lava two tiles wide: black rounded crust, split by cracks that still glow, hottest where
        // they are deepest.
        static PixelImage CooledBoulder(int variant)
        {
            var k = new Kit(30, 23);
            k.Ell(15f, 14.5f, 14.5f, 8.5f, Basalt);
            k.Ell(11f, 10f, 9.5f, 8f, Basalt);
            if (variant == 1) k.Ell(22f, 11.5f, 7f, 6.5f, Basalt);
            if (variant == 2) k.Ell(24.5f, 19f, 4f, 3.4f, Basalt);
            // The ropy folds of the crust.
            k.Ln(4, 12, 12, 9, Basalt.Light).Ln(8, 16, 18, 13, Basalt.Shade).Ln(16, 8, 23, 10, Basalt.Shade);
            // The cracks.
            GlowSeam(k, 73 + variant, 14, 3, 13, 9, 13, 9, 16, 14, 16, 14, 14, 21);
            GlowSeam(k, 75 + variant, 16, 14, 23, 16, 23, 16, 27, 15);
            GlowSeam(k, 77 + variant, 13, 9, 7, 12, 7, 12, 4, 17);
            if (variant != 0) GlowSeam(k, 79, 20, 6, 22, 10);
            k.Px(SeamCore, 14, 10, 15, 13);
            return Halo(Stand(k, 28), EmberGlow, (-15, 16), (15, 17), (-2, 2), (-10, 5), (10, 4));
        }

        // A stand of basalt columns two tiles wide: six-sided shafts of uneven height, each with a flat pale top, the
        // tallest behind. Walked behind: its top fades.
        static PixelImage BasaltColumns(int variant)
        {
            var k = new Kit(30, 34);
            // Back row first, then the front: (left, top, width).
            var shafts = variant == 0 ? new[] { (3, 10, 6), (9, 2, 7), (16, 6, 6), (22, 13, 6), (0, 20, 6), (6, 16, 6), (12, 21, 7), (19, 18, 6), (25, 24, 5) }
                : variant == 1 ? new[] { (2, 12, 6), (8, 5, 6), (14, 0, 7), (21, 8, 6), (0, 22, 5), (5, 18, 7), (12, 23, 6), (18, 16, 6), (24, 21, 6) }
                : new[] { (4, 6, 7), (11, 11, 6), (17, 3, 7), (24, 15, 5), (1, 19, 6), (7, 23, 6), (13, 17, 6), (19, 22, 6), (25, 26, 5) };
            foreach (var (x, top, w) in shafts)
            {
                // The shaft: three faces, lit, plain, in shade; a dark seam where it meets the next.
                for (int y = top + 2; y < 34; y++)
                    for (int i = 0; i < w; i++)
                        k.Px(x + i, y, i == 0 ? Basalt.Dark : i < w / 3 + 1 ? Basalt.Light.Mix(Basalt.Main, 0.35f) : i < w - 2 ? Basalt.Main : Basalt.Shade);
                // Its six-sided top: a pale flat cap, clipped at the corners.
                k.Rect(x + 1, top, w - 2, 1, Basalt.Light.Mix(Rgba.White, 0.18f));
                k.Rect(x, top + 1, w, 1, Basalt.Light);
                k.Px(x + w - 1, top + 1, Basalt.Main);
                // The joints across it, where a length has cracked through.
                for (int y = top + 7 + x % 4; y < 32; y += 9) k.Rect(x + 1, y, w - 1, 1, Basalt.Shade.Mix(Basalt.Dark, 0.5f));
            }
            // A glow between two of them, low down, and rubble at the foot.
            GlowSeam(k, 81 + variant, 12, 27, 12, 33);
            if (variant == 2) GlowSeam(k, 83, 19, 26, 19, 31);
            k.Dome(5f, 34, 4f, 2f, Basalt).Dome(24f, 34, 4.5f, 2.2f, Basalt);
            return Stand(k, 28);
        }

        // A rock standing in a pool of lava: a stack of basalt, its foot lit from below where the lava laps it.
        static PixelImage LavaRock(int variant)
        {
            var k = new Kit(14, 15);
            if (variant == 1)
            {
                // Two, a tall one and a low one.
                k.Col(2, 3, 5, 12, Basalt);
                k.Rect(2, 2, 5, 1, Basalt.Light).Rect(3, 1, 3, 1, Basalt.Light.Mix(Rgba.White, 0.15f));
                k.Col(8, 8, 5, 7, Basalt);
                k.Rect(8, 7, 5, 1, Basalt.Light);
            }
            else
            {
                k.Ell(7f, 9.5f, 6.4f, 5.5f, Basalt);
                k.Ell(6f, 6f, 4.4f, 4.4f, Basalt);
                if (variant == 2) k.Ell(10.5f, 5.5f, 2.6f, 3f, Basalt);
                k.Ln(4, 7, 8, 5, Basalt.Light).Ln(6, 10, 10, 9, Basalt.Shade);
            }
            // The glow up its foot: the lowest two rows orange, the row over them reddened.
            for (int x = 0; x < 14; x++)
            {
                if (!k.I.IsSet(x, 14)) continue;
                k.Px(x, 14, x % 3 == 1 ? SeamCore : Seam);
                if (k.I.IsSet(x, 13)) k.Px(x, 13, x % 2 == 0 ? SeamDim : Seam);
                if (k.I.IsSet(x, 12)) k.Px(x, 12, k.I[x, 12].Mix(SeamDim, 0.45f));
            }
            var image = Stand(k, 0);
            // No shadow on the lava: a hot rim round its foot instead.
            int cx = image.Width / 2, foot = image.Height - FootRows;
            for (int dx = -7; dx <= 6; dx++)
                if (image[cx + dx, foot].A == 0 && image[cx + dx, foot - 1].A > 0) image[cx + dx, foot] = SeamCore.WithAlpha(dx % 2 == 0 ? 220 : 150);
            return image;
        }

        // ---- The void abyss ----

        // A void crystal: violet shards out of a dark root, a pale core, a splinter of it hanging in the air over its
        // point. Dug for its ore with the pickaxe the veins want.
        static PixelImage VoidCrystal(int variant)
        {
            var k = new Kit(16, 19);
            k.Spike(4f, 19, 2.4f, 2f, 10, VoidGem);
            k.Spike(12f, 19, 2.4f, 14f, 11, VoidGem);
            k.Spike(8f, 19, 3.2f, variant == 2 ? 7f : 8.5f, 5, VoidGem);
            if (variant == 1) k.Spike(6f, 19, 1.6f, 5f, 13, VoidGem);
            // The splinter over it.
            k.Px(VoidGem.Light, variant == 2 ? 7 : 8, 1).Px(VoidGem.Main, variant == 2 ? 7 : 8, 2).Px(VoidGem.Shade, variant == 2 ? 8 : 9, 2);
            if (variant != 0) k.Px(VoidGem.Main, 12, 6).Px(VoidGem.Shade, 13, 6);
            k.Px(VoidGlint, 8, 9, 8, 12, 4, 14).Px(VoidGem.Light, 7, 8).Px(VoidGem.Light, 12, 14);
            k.Dome(8f, 19, 7f, 2.2f, VoidStone);
            k.Px(VoidGem.Main, 3, 18, 11, 18);
            return Halo(Stand(k, 13), VoidGlow, (-7, 11), (7, 12), (0, 3), (-4, 6), (5, 7), (-2, 0), (3, 0));
        }

        // A vine with nothing to climb, twisted up on itself: a thick dark stem in a slow S, thorns, buds that glow.
        static PixelImage TwistedVine(int variant)
        {
            var k = new Kit(16, 24);
            int tall = variant == 1 ? 18 : 22;
            for (int y = 24 - tall; y < 24; y++)
            {
                float t = (23 - y) / (float)tall;
                float sway = MathF.Sin(t * (variant == 2 ? 8.2f : 6.4f)) * (1.4f + t * 2.6f);
                int x = (int)MathF.Round(7 + sway);
                int w = t > 0.85f ? 1 : t > 0.6f ? 2 : 3;
                for (int i = 0; i < w; i++) k.Px(x + i, y, i == 0 ? VineBark.Light : i == w - 1 && w > 1 ? VineBark.Shade : VineBark.Main);
                // The twist: a dark band winding across it.
                if ((y + x) % 4 == 0 && w > 1) k.Px(x + 1, y, VineBark.Dark);
                // Thorns.
                if (y % 5 == 2 && t < 0.85f) k.Px(x - 1, y, VineBark.Shade).Px(x + w, y - 1, VineBark.Shade);
            }
            if (variant == 1)
            {
                // A second stem wound the other way.
                for (int y = 9; y < 24; y++)
                {
                    float t = (23 - y) / 15f;
                    int x = (int)MathF.Round(9 - MathF.Sin(t * 6.0f) * (1.2f + t * 2.4f));
                    k.Px(x, y, VineBark.Main).Px(x + 1, y, VineBark.Shade);
                }
            }
            // The curled tip and the buds.
            int tip = 24 - tall;
            k.Px(VineBark.Main, 9, tip, 10, tip + 1).Px(VineBark.Light, 8, Math.Max(0, tip - 1));
            foreach (var (bx, by) in variant == 0 ? new[] { (4, 8), (11, 13), (5, 17) } : variant == 1 ? new[] { (5, 11), (12, 15) } : new[] { (11, 6), (4, 12), (11, 18) })
                k.Px(VoidGem.Main, bx, by).Px(Starlight, bx, by - 1).Px(VoidGem.Shade, bx + 1, by);
            k.Dome(8f, 24, 5f, 1.8f, VoidStone);
            return Halo(Stand(k, 10), VoidGlow, (-5, 9), (5, 13), (-4, 18));
        }

        // A shard of an obelisk hanging over the floor: its point up, its broken end down, a line of runes that glows,
        // crumbs of it under it and its shadow on the floor apart from it.
        static PixelImage ObeliskShard(int variant)
        {
            var k = new Kit(12, 28);
            int left = variant == 1 ? 2 : 3, w = 6;
            // The point and the shaft.
            k.Spike(left + 3f, 6, 3f, left + 3f, 0, VoidStone);
            k.Col(left, 6, w, variant == 2 ? 10 : 13, VoidStone);
            int end = variant == 2 ? 16 : 19;
            // The broken end: a ragged slant.
            for (int i = 0; i < w; i++)
            {
                int drop = variant == 1 ? i / 2 : (w - 1 - i) / 2;
                for (int j = 0; j < drop; j++) k.Px(left + i, end + j, i < 2 ? VoidStone.Main : VoidStone.Shade);
                k.Px(left + i, end + drop, VoidStone.Dark);
            }
            // The runes down its lit face.
            for (int y = 7; y < end - 1; y += 3) k.Px(left + 2, y, VoidGem.Main).Px(left + 3, y, y % 2 == 1 ? Starlight : VoidGem.Light);
            k.Px(Starlight, left + 3, 2);
            // Crumbs hanging under it.
            k.Px(VoidStone.Main, left + 1, end + 5, left + 4, end + 4).Px(VoidStone.Shade, left + 2, end + 5, left + 5, end + 6);
            if (variant != 0) k.Px(VoidStone.Light, left - 1, end + 2).Px(VoidStone.Shade, left - 1, end + 3);
            return Halo(Stand(k, 9), VoidGlow, (-5, 9), (4, 12), (0, 22), (-1, 23));
        }

        // A stone post with an iron ring, and a chain from it that hangs up into the air and ends: whatever it held is
        // gone, and so is which way is down.
        static PixelImage ChainPost(int variant)
        {
            var k = new Kit(16, 24);
            int post = variant == 2 ? 3 : 5;
            k.Col(post, 9, 5, 15, VoidStone);
            k.Box(post - 1, 7, 7, 3, VoidStone);
            k.Px(VoidStone.Dark, post + 2, 14, post + 3, 18).Px(VoidGem.Shade, post + 1, 12);
            // The ring.
            k.Px(OldIron.Light, post + 2, 5).Px(OldIron.Main, post + 1, 6, post + 3, 6, post + 2, 4).Px(OldIron.Shade, post + 3, 5);
            // The chain: links going up and away, the last one broken open.
            int cx = post + 4, cy = 4;
            int links = variant == 1 ? 4 : 6;
            for (int i = 0; i < links; i++)
            {
                k.Px(cx, cy, i % 2 == 0 ? OldIron.Light : OldIron.Shade).Px(cx + 1, cy, OldIron.Main);
                cx += variant == 1 ? 1 : i % 2 == 0 ? 1 : 2;
                cy -= i % 2 == 0 ? 1 : 0;
                if (variant == 1 && i % 2 == 1) cy += 2;
            }
            k.Px(OldIron.Light, cx, cy).Px(VoidGem.Light, cx + 1, cy - 1);
            // Its footing.
            k.Dome(post + 2.5f, 24, 5f, 2f, VoidStone);
            return Stand(k, 11);
        }

        // A rock that floats, two tiles wide: a flat slab, ragged teeth of rock hanging under it, crumbs following under
        // them, a violet light on its underside. Its shadow lies on the floor apart from it.
        static PixelImage FloatingRock(int variant)
        {
            var k = new Kit(30, 31);
            // The underside first: ragged teeth of rock hanging from the slab, the longest off its middle.
            var teeth = variant == 0 ? new[] { (5f, 3.6f, 4f, 16), (11f, 5f, 12f, 24), (18f, 5.4f, 17f, 21), (24f, 3.8f, 26f, 15) }
                : variant == 1 ? new[] { (6f, 4.4f, 7f, 19), (13f, 5.2f, 15f, 26), (20f, 4.6f, 19f, 18), (25f, 3f, 27f, 14) }
                : new[] { (4f, 3f, 3f, 14), (10f, 5f, 9f, 20), (17f, 5.6f, 19f, 25), (24f, 4f, 24f, 17) };
            foreach (var (bx, half, ax, ay) in teeth) k.Spike(bx, 9, half, ax, ay, VoidStone);
            // The top: a flat slab, lit, its rim uneven.
            k.Ell(15f, 8f, 14f, 4.6f, VoidStone);
            k.Ell(14f, 6.6f, 12f, 3.2f, new Ramp(VoidStone.Light.Mix(Rgba.White, 0.12f), VoidStone.Light, VoidStone.Main, VoidStone.Shade));
            k.Rect(variant == 1 ? 3 : 24, 4, 3, 1, Rgba.Clear).Rect(variant == 2 ? 6 : 19, 3, 2, 1, Rgba.Clear);
            // Cracks across the top and down the body, starlight in them.
            k.Ln(8, 6, 13, 8, VoidStone.Dark).Ln(13, 8, 12, 15, VoidStone.Dark).Ln(19, 5, 21, 9, VoidStone.Shade);
            k.Px(Starlight, 12, 11, 20, 7).Px(VoidGem.Main, 12, 12, 9, 6);
            // The light on its underside: the lowest pixel of each column.
            for (int y = 12; y < 27; y++)
                for (int x = 0; x < 30; x++)
                    if (k.I.IsSet(x, y) && !k.I.IsSet(x, y + 1)) k.Px(x, y, x % 3 == 0 ? VoidGem.Main : VoidGem.Shade);
            // A small crystal grown on it, and crumbs under.
            if (variant != 1) k.Spike(22f, 6, 1.6f, 22.5f, 0, VoidGem);
            k.Px(VoidStone.Main, 7, 23, 22, 22, 14, 29).Px(VoidStone.Shade, 8, 24, 23, 23, 15, 29).Px(VoidStone.Light, 7, 22);
            return Halo(Stand(k, 22), VoidGlow, (-3, 27), (1, 28), (-8, 22), (7, 21));
        }

        // A pillar broken through and not fallen: its base stands, the length above hangs over it a little aside, the
        // pieces between hold their places in the air.
        static PixelImage ShatteredPillar(int variant)
        {
            var k = new Kit(28, 36);
            // The base: a plinth and a drum, its top a ragged break.
            k.Box(6, 31, 16, 5, VoidStone);
            k.Col(8, 23, 12, 8, VoidStone);
            for (int x = 8; x < 20; x++)
            {
                int rag = (int)(PixelNoise.Value(x, variant, 91) * 3);
                for (int j = 0; j < rag; j++) k.Px(x, 23 + j, Rgba.Clear);
                k.Px(x, 23 + rag, VoidStone.Light);
            }
            // The flutes.
            for (int x = 10; x < 20; x += 3) k.Rect(x, 26, 1, 5, VoidStone.Shade);
            // The length above, hanging: shifted aside, its lower end ragged, its top a capital on two drawings.
            int shift = variant == 1 ? -3 : 3, top = variant == 2 ? 6 : 2;
            k.Col(8 + shift, top, 12, 15 - top + 2, VoidStone);
            for (int x = 8 + shift; x < 20 + shift; x++)
            {
                int rag = (int)(PixelNoise.Value(x, variant, 93) * 3);
                for (int j = 0; j < rag; j++) k.Px(x, 16 - j, Rgba.Clear);
                k.Px(x, 16 - rag, VoidStone.Dark);
            }
            for (int x = 10 + shift; x < 20 + shift; x += 3) k.Rect(x, top + 2, 1, 11 - top, VoidStone.Shade);
            if (variant != 2)
            {
                k.Box(6 + shift, top, 16, 3, VoidStone);
                k.Rect(6 + shift, top, 16, 1, VoidStone.Light.Mix(Rgba.White, 0.15f));
            }
            else
            {
                // Its top broken too.
                for (int x = 8 + shift; x < 20 + shift; x++)
                    for (int j = 0; j < (int)(PixelNoise.Value(x, 5, 95) * 4); j++) k.Px(x, top + j, Rgba.Clear);
            }
            // The pieces between, and the light in the break.
            k.Px(VoidStone.Main, 11, 19, 16, 20, 21, 18).Px(VoidStone.Shade, 12, 19, 17, 20, 21, 19).Px(VoidStone.Light, 11, 18, 16, 19);
            k.Px(VoidGem.Main, 13, 21, 15, 18).Px(Starlight, 14, 20);
            k.Px(VoidGem.Shade, 9 + shift, 9, 18 + shift, 6);
            return Halo(Stand(k, 20), VoidGlow, (-2, 21), (1, 20), (0, 22));
        }

        // ---- The places' tall things ----

        // A tree burnt to charcoal and still alight inside: a black trunk and bare limbs, cracks glowing up it, embers
        // at the ends of its twigs: what a lava zone's village has where a forest would have a tree. The size of the
        // old mushroom tree.
        static PixelImage CharredTree()
        {
            var k = new Kit(30, 40);
            k.Col(12, 14, 6, 26, Charred);
            // The limbs, and the twigs off them.
            k.Ln(13, 16, 5, 6, Charred.Main, 2).Ln(17, 16, 25, 7, Charred.Shade, 2).Ln(15, 14, 15, 2, Charred.Main, 2);
            k.Ln(5, 6, 2, 2, Charred.Main).Ln(8, 10, 3, 11, Charred.Shade).Ln(25, 7, 28, 3, Charred.Shade).Ln(22, 10, 27, 12, Charred.Shade);
            k.Ln(15, 4, 11, 0, Charred.Main).Ln(16, 5, 20, 1, Charred.Shade);
            // The bark's plates, and the fire in the cracks between.
            k.Px(Charred.Light, 12, 20, 12, 27, 13, 33).Px(Charred.Dark, 16, 18, 15, 25, 16, 31);
            GlowSeam(k, 85, 14, 16, 14, 24, 14, 24, 15, 30, 15, 30, 14, 37);
            GlowSeam(k, 87, 9, 11, 12, 15);
            GlowSeam(k, 89, 21, 11, 17, 15);
            // Embers at the twigs' ends.
            foreach (var (x, y) in new[] { (2, 2), (3, 11), (28, 3), (27, 12), (11, 0), (20, 1) })
                k.Px(SeamCore, x, y).Px(Seam, x + (x < 15 ? 1 : -1), y);
            k.Dome(15f, 40, 8f, 2.8f, Basalt);
            k.Px(Seam, 11, 39, 18, 39);
            return Halo(Stand(k, 20, 32, 44), EmberGlow, (-13, 5), (13, 6), (-12, 14), (12, 15), (-4, 3), (5, 4));
        }

        // A tree of the abyss: a pale twisted trunk, a crown of dark leaves full of stars, pieces of the crown adrift
        // beside it. The size of the old mushroom tree.
        static PixelImage VoidTree()
        {
            var k = new Kit(30, 40);
            var leaf = new Ramp(Rgba.Hex("#7A5CC0"), Rgba.Hex("#4E3690"), Rgba.Hex("#332264"), Rgba.Hex("#1E1440"));
            // The trunk: two stems wound round each other.
            for (int y = 14; y < 40; y++)
            {
                float t = (39 - y) / 25f;
                int a = (int)MathF.Round(14 + MathF.Sin(t * 7f) * 2f), b = (int)MathF.Round(15 - MathF.Sin(t * 7f) * 2f);
                k.Px(b, y, VineBark.Shade).Px(b + 1, y, VineBark.Dark);
                k.Px(a, y, VineBark.Light).Px(a + 1, y, VineBark.Main).Px(a + 2, y, VineBark.Shade);
            }
            // The crown, and what has drifted off it.
            k.Ell(15f, 10f, 12f, 9f, leaf);
            k.Ell(9f, 13f, 6f, 5f, leaf);
            k.Ell(22f, 13.5f, 5.5f, 4.5f, leaf);
            k.Ell(2.5f, 6f, 2.2f, 1.8f, leaf).Ell(27.5f, 19f, 2.2f, 1.8f, leaf).Ell(26f, 3f, 1.8f, 1.5f, leaf);
            // Stars in it.
            foreach (var (x, y) in new[] { (9, 6), (17, 4), (21, 10), (12, 12), (6, 13), (24, 14), (15, 16) })
                k.Px(Starlight, x, y).Px(VoidGem.Main, x + 1, y, x, y + 1);
            k.Px(VoidGlint, 17, 4, 12, 12);
            k.Px(Starlight, 2, 6).Px(VoidGem.Light, 27, 19);
            k.Dome(15f, 40, 7f, 2.6f, VoidStone);
            return Halo(Stand(k, 20, 32, 44), VoidGlow, (-14, 12), (14, 12), (0, 2), (-9, 4), (9, 3));
        }
    }
}
