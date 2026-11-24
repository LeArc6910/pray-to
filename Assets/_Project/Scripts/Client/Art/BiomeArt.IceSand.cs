using System;
using Game.Core;

namespace Game.Client
{
    // The ice cave's and the sand cave's props (Docs/BiomeRework.md 4-1 얼음 동굴·모래 동굴, 묶음 B2a), drawn like the
    // first two biomes': shaded shapes lit from the upper left, a dark rim, a contact row, a crisp shadow under the
    // foot, three drawings each by the tile.
    //  - The ice cave, a frozen great hall: clear blue ice with pale facets, snow lying on whatever is flat, a cold
    //    sky-blue glow in the crystals.
    //  - The sand cave, an old temple under the sand: warm sandstone carved and worn, bleached bone, fired clay, a
    //    cactus's dusty green, sand heaped against every foot.
    static partial class BiomeArt
    {
        static readonly Ramp Ice = new Ramp(Rgba.Hex("#EAF8FF"), Rgba.Hex("#A8D6F0"), Rgba.Hex("#6FA4CC"), Rgba.Hex("#3E6890"));
        static readonly Ramp DeepIce = new Ramp(Rgba.Hex("#BCE4FA"), Rgba.Hex("#7CB8E0"), Rgba.Hex("#4C82B4"), Rgba.Hex("#2C5080"));
        static readonly Ramp Snow = new Ramp(Rgba.Hex("#FFFFFF"), Rgba.Hex("#E4F0FA"), Rgba.Hex("#B4CCE0"), Rgba.Hex("#7E9CB8"));
        static readonly Ramp FrostTwig = new Ramp(Rgba.Hex("#C8DCE8"), Rgba.Hex("#8EA8BC"), Rgba.Hex("#5E7890"), Rgba.Hex("#3C5068"));
        static readonly Ramp FrostBark = new Ramp(Rgba.Hex("#B8C4D0"), Rgba.Hex("#8894A4"), Rgba.Hex("#5E6A7C"), Rgba.Hex("#3C4658"));
        static readonly Rgba IceGlow = Rgba.Hex("#6FD0FF"), IceCore = Rgba.Hex("#F0FCFF");

        static readonly Ramp Sandstone = new Ramp(Rgba.Hex("#EBCB94"), Rgba.Hex("#C49C62"), Rgba.Hex("#96703E"), Rgba.Hex("#654624"));
        static readonly Ramp SandHeap = new Ramp(Rgba.Hex("#D8B482"), Rgba.Hex("#B08A58"), Rgba.Hex("#866238"), Rgba.Hex("#5A3C24"));
        static readonly Ramp OldBone = new Ramp(Rgba.Hex("#FFF8E4"), Rgba.Hex("#EDE3C8"), Rgba.Hex("#BCAE8C"), Rgba.Hex("#857860"));
        static readonly Ramp Cactus = new Ramp(Rgba.Hex("#A8D08A"), Rgba.Hex("#6E9E5A"), Rgba.Hex("#4A743E"), Rgba.Hex("#2E4C2A"));
        static readonly Ramp Clay = new Ramp(Rgba.Hex("#E8A474"), Rgba.Hex("#BE7044"), Rgba.Hex("#8C4828"), Rgba.Hex("#5A2C18"));
        static readonly Ramp PalmBark = new Ramp(Rgba.Hex("#C8A070"), Rgba.Hex("#96703E"), Rgba.Hex("#6E4E2A"), Rgba.Hex("#46301A"));
        static readonly Rgba CoinGold = Rgba.Hex("#F0BE40"), CoinCore = Rgba.Hex("#FFE48A"), UrnBand = Rgba.Hex("#3E6E8E"), Bloom = Rgba.Hex("#F0709A");
        static readonly Rgba Socket = Rgba.Hex("#2A1E16");

        static bool IceSandMirrors(TileType type) => type == TileType.IceSpike || type == TileType.FrozenBush || type == TileType.SnowDrift || type == TileType.IceCrystals
            || type == TileType.IcePillar || type == TileType.CaveCactus || type == TileType.Skull || type == TileType.BrokenUrn || type == TileType.RibArch;

        static int IceSandCanopy(TileType type) => type switch
        {
            TileType.IcePillar => 20,
            TileType.RibArch => 14,
            TileType.IceColumn => 30,
            _ => 0,
        };

        static PixelImage IceSandProp(TileType type, int variant) => type switch
        {
            TileType.IceSpike => IceSpike(variant),
            TileType.FrozenBush => FrozenBush(variant),
            TileType.SnowDrift => SnowDrift(variant),
            TileType.IceCrystals => IceCrystals(variant),
            TileType.IcePillar => IcePillar(variant),
            TileType.FrozenFall => FrozenFall(variant),
            TileType.CaveCactus => CaveCactus(variant),
            TileType.Skull => Skull(variant),
            TileType.BrokenUrn => BrokenUrn(variant),
            TileType.SandstonePillar => SandstonePillar(variant),
            TileType.RibArch => RibArch(variant),
            TileType.BuriedColumn => BuriedColumn(variant),
            _ => null,
        };

        // A column of an ice keep's ruin (a landmark's tile, TileType.IceColumn): three drawings by the tile, whole with
        // a frozen banner, whole and bare, broken off. shape: Variant's bits.
        public static PixelImage IceColumn(int shape)
        {
            int variant = shape & 3;
            var k = new Kit(18, 46);
            int top = variant == 2 ? 20 : 0;
            // The shaft: dressed blocks of blue ice, their courses showing.
            k.Col(4, top + 5, 10, 41 - top - 5, DeepIce);
            for (int y = top + 10; y < 40; y += 6) k.Tint(y, 0.84f);
            k.Ln(6, top + 7, 6, 38, Ice.Light);
            if (variant == 2)
            {
                // Broken off: a jagged pale face, a block fallen at its foot.
                k.Spike(6.5f, top + 6, 2.5f, 5f, top, Ice).Spike(11.5f, top + 6, 2.5f, 12.5f, top + 2, Ice);
                k.Box(14, 41, 4, 5, Ice);
            }
            else
            {
                // The capital, snow lying on it and icicles under its lip.
                k.Box(1, 2, 16, 5, Ice);
                k.Dome(9f, 3, 8f, 3.4f, Snow);
                k.Px(Ice.Light, 3, 7, 3, 8, 8, 7, 14, 7, 14, 8, 14, 9);
            }
            // The plinth, snow drifted against it.
            k.Box(2, 41, 14, 5, DeepIce);
            k.Dome(4f, 46, 4.5f, 3f, Snow);
            if (variant == 0)
            {
                // The keep's banner, frozen stiff mid-wave: blue cloth rimed white, hung from a rod.
                k.Rect(12, 10, 6, 1, OldIron.Shade);
                for (int y = 11; y < 26; y++)
                {
                    int sway = y > 20 ? 1 : 0;
                    k.Rect(13 + sway, y, 5 - (y > 23 ? y - 23 : 0), 1, y < 13 ? Rgba.Hex("#5A86D0") : Rgba.Hex("#3E62A8"));
                    k.Px(17 + sway - (y > 23 ? y - 23 : 0), y, Rgba.Hex("#2A4478"));
                }
                k.Px(Snow.Main, 13, 11, 14, 11, 15, 12, 14, 17, 15, 18, 16, 17).Px(Ice.Light, 14, 25, 15, 26, 14, 26);
            }
            var image = Stand(k, 13);
            return (shape & 4) != 0 ? image : image;
        }

        // ---- The ice cave ----

        // A spike of ice risen from the floor, snow round its foot.
        static PixelImage IceSpike(int variant)
        {
            var k = new Kit(14, 24);
            if (variant == 0)
            {
                k.Spike(7, 24, 4.2f, 6.5f, 0, Ice);
                k.Ln(7, 5, 8, 20, DeepIce.Shade);
            }
            else if (variant == 1)
            {
                k.Spike(5, 24, 3.4f, 4.5f, 3, Ice);
                k.Spike(10.5f, 24, 2.6f, 11.5f, 12, DeepIce);
                k.Ln(5, 9, 6, 20, DeepIce.Shade);
            }
            else
            {
                // Three short ones leaning apart.
                k.Spike(3.5f, 24, 2.4f, 1.5f, 13, DeepIce);
                k.Spike(7.5f, 24, 3.2f, 7.5f, 7, Ice);
                k.Spike(11.5f, 24, 2.2f, 13f, 15, DeepIce);
            }
            k.Px(IceCore, variant == 1 ? 4 : 6, variant == 2 ? 11 : 6);
            k.Dome(7f, 24, 6.5f, 2.6f, Snow);
            return Stand(k, 11);
        }

        // A bush frozen through: grey-blue twigs rimed white, the frost berries still on it and glowing a little.
        static PixelImage FrozenBush(int variant)
        {
            var k = new Kit(16, 14);
            k.Dome(8f, 14, 7.5f, variant == 1 ? 9f : 11f, FrostTwig);
            // The twigs through it, and the rime on its top.
            k.Ln(8, 13, 4, 6, FrostTwig.Dark).Ln(8, 13, 11, 5, FrostTwig.Dark).Ln(8, 13, 8, 5, FrostTwig.Shade).Ln(5, 9, 2, 9, FrostTwig.Dark).Ln(11, 8, 14, 9, FrostTwig.Dark);
            k.Speck(Snow.Light, 0.42f, 81 + variant, 0, 7);
            k.Speck(Snow.Main, 0.2f, 85 + variant, 7, 10);
            k.Px(IceGlow, 4, 8, 10, 6).Px(IceCore, 4, 7);
            if (variant != 1) k.Px(IceGlow, 12, 10, 7, 10).Px(IceCore, 12, 9);
            if (variant == 2) k.Px(IceGlow, 6, 5).Px(IceCore, 9, 9);
            return Stand(k, 14);
        }

        // Snow drifted into a heap.
        static PixelImage SnowDrift(int variant)
        {
            var k = new Kit(18, 11);
            k.Dome(8f, 11, 8f, variant == 1 ? 7f : 9f, Snow);
            k.Dome(13f, 11, 5f, 5f, Snow);
            // The wind's edge along its back.
            k.Ln(3, 7, 9, 4, Snow.Light).Px(Snow.Shade, 11, 8, 12, 9, 6, 9);
            if (variant == 1) k.Ln(5, 5, 3, 1, FrostTwig.Dark).Px(FrostTwig.Dark, 5, 2);
            if (variant == 2)
            {
                k.Spike(14.5f, 8, 1.8f, 15.5f, 2, Ice);
                k.Px(IceCore, 14, 5);
            }
            return Stand(k, 16);
        }

        // A clump of ice crystals that glow sky blue: picked for ice crystal.
        static PixelImage IceCrystals(int variant)
        {
            var k = new Kit(16, 15);
            k.Spike(4f, 15, 2.6f, 1.5f, 5, DeepIce);
            k.Spike(8.5f, 15, 3.2f, 8.5f, variant == 2 ? 3 : 0, Ice);
            k.Spike(12.5f, 15, 2.4f, 14.5f, 7, DeepIce);
            if (variant == 1) k.Spike(6f, 15, 1.6f, 5f, 9, Ice);
            // The glow in their hearts.
            k.Px(IceGlow, 8, 8, 8, 10, 4, 11, 12, 12).Px(IceCore, 8, 9, 7, 4);
            k.Dome(8f, 15, 7f, 1.8f, Snow);
            var image = Stand(k, 13);
            // A cold halo.
            int cx = image.Width / 2;
            foreach (var (dx, dy) in new[] { (-7, 6), (7, 8), (0, -1), (-4, 1), (5, 2) }) image.Blend(cx + dx, 2 + dy, IceGlow.WithAlpha(90));
            return image;
        }

        // A pillar of clear ice two tiles wide and more than two tall, standing on its lower row: a great six-sided
        // shaft with smaller ones grown against it, light caught inside.
        static PixelImage IcePillar(int variant)
        {
            var k = new Kit(30, 42);
            // The small ones behind.
            k.Col(3, 24, 6, 18, DeepIce).Spike(6f, 24, 3f, 5f, 16, DeepIce);
            if (variant != 2) k.Col(21, 28, 6, 14, DeepIce).Spike(24f, 28, 3f, 25.5f, 21, DeepIce);
            // The shaft and its point.
            int top = variant == 1 ? 14 : 9;
            k.Col(8, top, 14, 42 - top, Ice);
            if (variant == 1)
            {
                // Its top sheared off flat.
                k.Rect(8, top - 2, 14, 2, Ice.Light).Rect(10, top - 3, 10, 1, IceCore).Px(Ice.Main, 20, top - 2, 21, top - 1, 21, top - 2);
            }
            else k.Spike(15f, top, 7f, 14f, 0, Ice);
            // The facets' edges and the light inside.
            k.Ln(12, top + 1, 12, 40, Ice.Light).Ln(18, top + 1, 18, 40, Ice.Shade);
            k.Ln(14, top + 6, 16, top + 14, DeepIce.Light).Ln(15, 24, 13, 32, DeepIce.Light);
            k.Px(IceGlow, 15, top + 9, 14, 27, 16, 34, 5, 30).Px(IceCore, 15, top + 10, 14, 28);
            if (variant == 2) k.Ln(20, top + 4, 17, top + 16, DeepIce.Dark).Ln(17, top + 16, 19, top + 24, DeepIce.Dark);
            k.Dome(15f, 42, 14f, 3.4f, Snow);
            return Stand(k, 28);
        }

        // A fall of water frozen as it fell, two tiles wide, hung from the rock above it: ropes of ice down to a swollen
        // foot, snow on its lip.
        static PixelImage FrozenFall(int variant)
        {
            var k = new Kit(30, 44);
            for (int x = 2; x < 28; x++)
            {
                // Each rope its own length and tone; the middle ones reach the foot.
                int rope = (x - 2) / 3;
                float n = PixelNoise.Value(rope, variant, 91);
                int end = x > 6 && x < 23 ? 40 : 26 + (int)(n * 10);
                int phase = (x - 2) % 3;
                var tone = phase == 0 ? Ice.Light : phase == 1 ? (n > 0.5f ? Ice.Main : DeepIce.Light) : DeepIce.Main;
                if (x > 22) tone = phase == 0 ? Ice.Main : DeepIce.Shade;
                k.Rect(x, 0, 1, end, tone);
                if (phase == 2 && end < 40) k.Px(x, end - 1, Rgba.Clear).Px(x - 2, end - 1, Rgba.Clear);
            }
            // The foot, swollen where it met the floor, and the splash frozen round it.
            k.Ell(15f, 39f, 13.5f, 5f, Ice);
            k.Ell(15f, 37.5f, 9f, 3f, new Ramp(IceCore, Ice.Light, Ice.Main, Ice.Shade));
            k.Px(IceGlow, 10, 14, 16, 22, 19, 9, 13, 30).Px(IceCore, 10, 13, 16, 21);
            // Snow on the lip under the rock, and a hollow behind the ropes.
            if (variant == 1) k.Rect(12, 18, 3, 9, DeepIce.Dark).Rect(13, 17, 1, 1, DeepIce.Dark);
            if (variant == 2) k.Spike(4f, 44, 2.6f, 3f, 34, DeepIce).Spike(26f, 44, 2.4f, 27f, 36, DeepIce);
            k.Rect(1, 0, 28, 2, Snow.Main).Rect(1, 0, 28, 1, Snow.Light).Px(Snow.Main, 4, 2, 5, 2, 12, 2, 20, 2, 21, 2, 22, 3);
            return Stand(k, 28);
        }

        // A dead tree frozen white, snow along its boughs and icicles under them: what an ice cave's village has where
        // a forest would have a tree. The size of the old mushroom tree.
        static PixelImage FrozenTree()
        {
            var k = new Kit(30, 40);
            k.Col(12, 12, 6, 28, FrostBark);
            k.Px(FrostBark.Dark, 14, 20, 16, 27, 14, 33);
            // The boughs.
            k.Ln(13, 17, 4, 9, FrostBark.Shade, 3).Ln(4, 9, 1, 10, FrostBark.Shade, 2).Ln(8, 12, 6, 4, FrostBark.Shade, 2);
            k.Ln(17, 15, 26, 7, FrostBark.Shade, 3).Ln(26, 7, 29, 9, FrostBark.Dark, 2).Ln(21, 11, 23, 3, FrostBark.Shade, 2);
            k.Ln(14, 13, 14, 3, FrostBark.Main, 2).Ln(15, 13, 15, 3, FrostBark.Shade, 2).Ln(15, 7, 11, 2, FrostBark.Shade, 2).Ln(15, 8, 19, 1, FrostBark.Shade, 2);
            // Snow lying thick on them.
            foreach (var (x, y, r) in new[] { (3f, 10, 3.5f), (7f, 5, 3f), (11.5f, 3, 3f), (15f, 4, 3f), (19f, 2, 3f), (23f, 4, 3.4f), (27f, 8, 3.2f), (20f, 10, 2.6f), (9f, 11, 2.6f) })
                k.Dome(x, y, r, 2.6f, Snow);
            // Icicles under the boughs.
            foreach (var (x, y, h) in new[] { (3, 12, 4), (7, 14, 3), (10, 16, 2), (20, 13, 4), (24, 11, 5), (27, 11, 3) })
            {
                k.Rect(x, y, 1, h, Ice.Main);
                k.Px(x, y + h - 1, IceCore);
            }
            k.Dome(15f, 40, 8f, 3.4f, Snow);
            return Stand(k, 20, 32, 44);
        }

        // ---- The sand cave ----

        // A cave cactus, taller than its tile: a ribbed column with arms, a fruit or a bloom on it, sand at its foot.
        static PixelImage CaveCactus(int variant)
        {
            var k = new Kit(15, 27);
            int top = variant == 1 ? 9 : 3;
            k.Col(5, top + 2, 5, 25 - top, Cactus);
            k.Ell(7.5f, top + 2.5f, 2.5f, 2.6f, Cactus);
            if (variant != 1)
            {
                // The arms: out, then up.
                k.Col(1, 9, 3, 6, Cactus).Rect(1, 14, 4, 2, Cactus.Shade).Px(Cactus.Light, 1, 9, 2, 8);
                k.Col(11, 12, 3, 6, Cactus).Rect(10, 17, 4, 2, Cactus.Dark).Px(Cactus.Main, 12, 11);
            }
            if (variant == 1) k.Col(10, 16, 3, 5, Cactus).Rect(9, 20, 4, 2, Cactus.Dark).Px(Cactus.Main, 11, 15);
            if (variant == 2) k.Col(1, 17, 2, 4, Cactus).Rect(1, 20, 4, 2, Cactus.Shade);
            // Ribs, spines and what grows on it.
            k.Ln(7, top + 3, 7, 24, Cactus.Shade);
            for (int y = top + 4; y < 24; y += 4) k.Px(Rgba.Hex("#F0ECC8"), 5, y, 9, y + 2);
            if (variant == 2) k.Px(Bloom, 6, top - 1, 7, top - 1, 8, top - 1, 7, top - 2).Px(Rgba.Hex("#FFE07A"), 7, top - 1);
            else k.Ell(7.5f, top + 0.2f, 1.6f, 1.6f, new Ramp(Rgba.Hex("#FF9AB4"), Rgba.Hex("#E8507A"), Rgba.Hex("#B03058"), Rgba.Hex("#7A1E3C")));
            if (variant == 0) k.Px(Rgba.Hex("#E8507A"), 2, 7).Px(Rgba.Hex("#FF9AB4"), 1, 7);
            k.Dome(7.5f, 27, 6.5f, 2.8f, SandHeap);
            return Stand(k, 11);
        }

        // A beast's skull, horned, half sunk in the sand.
        static PixelImage Skull(int variant)
        {
            var k = new Kit(18, 14);
            // The horn first, then the cranium over its root.
            k.Ln(5, 6, 2, 1, OldBone.Shade, 2).Ln(2, 1, 1, 0, OldBone.Main).Px(OldBone.Light, 4, 5, 3, 3);
            if (variant != 2) k.Ln(10, 5, 13, 0, OldBone.Shade, 2).Px(OldBone.Light, 11, 3);
            k.Ell(7.5f, 8f, 5.5f, 4.6f, OldBone);
            // The snout, its teeth, the sockets.
            k.Box(10, 8, 7, 4, OldBone);
            k.Rect(11, 12, 6, 1, OldBone.Shade).Px(Socket, 12, 12, 14, 12, 16, 12);
            k.Rect(6, 7, 3, 3, Socket).Px(OldBone.Dark, 6, 7).Px(Socket, 15, 9);
            k.Px(OldBone.Dark, 4, 11, 8, 12);
            if (variant == 1) k.Ln(1, 12, 6, 13, OldBone.Main).Px(OldBone.Shade, 2, 13);
            k.Dome(variant == 2 ? 5f : 13f, 14, 5f, 2.4f, SandHeap);
            return Stand(k, 15);
        }

        // An old urn of fired clay, cracked or broken open: struck, it gives up its coins and amber.
        static PixelImage BrokenUrn(int variant)
        {
            var k = new Kit(variant == 2 ? 18 : 14, 16);
            if (variant == 2)
            {
                // Tipped on its side, its mouth toward us, coins spilled.
                k.Ell(9f, 10.5f, 7.5f, 5f, Clay);
                k.Rect(8, 8, 1, 6, UrnBand).Rect(11, 8, 1, 6, UrnBand.Scale(0.8f));
                k.Ell(3f, 10.5f, 2.6f, 3.4f, new Ramp(Clay.Main, Clay.Shade, Clay.Dark, Clay.Dark));
                k.Px(Socket, 2, 10, 3, 10, 2, 11, 3, 11, 3, 9);
                k.Px(CoinGold, 1, 14, 4, 15, 6, 14).Px(CoinCore, 1, 13, 5, 14);
                return Stand(k, 16);
            }
            k.Ell(7f, 10f, 6f, 5.6f, Clay);
            k.Rect(4, 2, 6, 4, Clay.Main).Rect(4, 2, 1, 4, Clay.Light).Rect(9, 2, 1, 4, Clay.Shade);
            k.Rect(3, 1, 8, 1, Clay.Light).Px(Clay.Shade, 10, 1);
            // The painted band, the handles.
            k.Rect(2, 9, 10, 1, UrnBand).Px(UrnBand.Scale(0.75f), 10, 9, 11, 9).Px(Rgba.Hex("#E8DCC0"), 3, 11, 5, 11, 7, 11, 9, 11);
            k.Px(Clay.Shade, 1, 6, 12, 6, 1, 7, 12, 7);
            if (variant == 0)
            {
                // A crack down from its lip.
                k.Ln(8, 2, 7, 6, Clay.Dark).Ln(7, 6, 9, 10, Clay.Dark);
            }
            else
            {
                // Its neck broken away: the dark inside, a coin's glint, shards at its foot.
                k.Rect(3, 0, 8, 4, Rgba.Clear).Px(Rgba.Clear, 4, 4, 9, 4, 5, 5);
                k.Rect(5, 5, 4, 1, Socket).Px(Socket, 6, 4, 7, 4, 8, 4).Px(CoinGold, 6, 5).Px(CoinCore, 7, 5);
                k.Px(Clay.Light, 4, 5, 9, 5).Px(Clay.Main, 12, 15, 13, 15, 0, 15).Px(Clay.Light, 12, 14);
            }
            return Stand(k, 12);
        }

        // A pillar of carved sandstone, worn: a capital, bands of glyphs, a base; or its stump.
        static PixelImage SandstonePillar(int variant)
        {
            var k = new Kit(14, 26);
            int top = variant == 1 ? 10 : 0;
            k.Col(3, top + 3, 8, 23 - top - 3, Sandstone);
            if (variant == 1)
            {
                // Broken off: a slanted pale face.
                k.Rect(3, top + 1, 5, 2, Sandstone.Light).Rect(8, top + 2, 3, 1, Sandstone.Main).Px(Sandstone.Light, 3, top);
            }
            else
            {
                k.Box(1, 0, 12, 4, Sandstone);
                k.Rect(2, 4, 10, 1, Sandstone.Dark);
            }
            // Bands and the glyphs between them.
            for (int y = top + 8; y < 22; y += 6) k.Tint(y, 0.8f);
            k.Px(Sandstone.Dark, 5, top + 11, 7, top + 10, 7, top + 12, 9, top + 11);
            if (variant != 1) k.Px(Sandstone.Dark, 5, 17, 6, 18, 8, 17, 8, 19);
            k.Box(1, 22, 12, 4, Sandstone);
            if (variant == 2) k.Dome(10f, 26, 4.5f, 5f, SandHeap);
            else k.Dome(4f, 26, 4f, 2.4f, SandHeap);
            return Stand(k, 12);
        }

        // The ribs of something vast, two tiles wide: its spine sunk in the sand, three ribs arched up over the tiles
        // behind (walked behind; they fade).
        static PixelImage RibArch(int variant)
        {
            var k = new Kit(32, 32);
            // The spine: a row of vertebrae along the ground.
            for (int x = 2; x < 29; x += 5) k.Ell(x + 2.5f, 28.5f, 2.8f, 2.6f, OldBone);
            k.Rect(3, 29, 26, 1, OldBone.Shade);
            // The ribs: each rises from the spine, nearly upright, and curls over to the right at its tip.
            int ribs = variant == 2 ? 2 : 3;
            for (int i = 0; i < ribs; i++)
            {
                int x0 = 3 + i * (variant == 2 ? 12 : 8);
                bool snapped = variant == 1 && i == 1;
                float last = snapped ? 0.5f : 1f;
                for (float t = 0; t <= last; t += 0.01f)
                {
                    int x = (int)MathF.Round(x0 + 10f * t * t * t + 1.5f * t), y = (int)MathF.Round(27 - 26f * MathF.Sin(t * MathF.PI * 0.5f));
                    int thick = t < 0.7f ? 3 : 2;
                    for (int j = 0; j < thick; j++) k.Px(x + j, y, j == 0 ? OldBone.Light : j == thick - 1 ? OldBone.Shade : OldBone.Main);
                    if (t > 0.75f) k.Px(x + 1, y + 1, OldBone.Shade);
                }
                if (snapped) k.Px(OldBone.Dark, x0 + 2, 8, x0 + 3, 8).Px(OldBone.Light, x0 + 1, 8);
            }
            k.Dome(variant == 2 ? 23f : 8f, 32, 8f, 3.4f, SandHeap);
            k.Dome(variant == 2 ? 6f : 26f, 32, 5f, 2.2f, SandHeap);
            return Stand(k, 30);
        }

        // A fluted column toppled and half buried, two tiles long: its drum rising out of the sand at a slant, the cut
        // end toward us.
        static PixelImage BuriedColumn(int variant)
        {
            var k = new Kit(32, 24);
            // The shaft, lying from the lower left up to the right: lit along its top, dark beneath.
            int x0 = 3, y0 = 14, x1 = variant == 1 ? 17 : 24, y1 = variant == 1 ? 8 : 4;
            for (int j = 0; j < 9; j++)
            {
                var tone = j < 2 ? Sandstone.Light : j < 5 ? Sandstone.Main : j < 7 ? Sandstone.Shade : Sandstone.Dark;
                k.Ln(x0, y0 + j, x1, y1 + j, tone);
            }
            // The flutes and the joints between its drums.
            k.Ln(x0, y0 + 3, x1, y1 + 3, Sandstone.Shade);
            for (int x = x0 + 6; x < x1 - 2; x += 7)
            {
                int y = y0 + (y1 - y0) * (x - x0) / (x1 - x0);
                k.Rect(x, y, 1, 9, Sandstone.Dark);
            }
            // Its end: a round face with the dowel hole.
            k.Ell(x1 + 2f, y1 + 4.5f, 3.4f, 5.2f, new Ramp(Sandstone.Light.Mix(Rgba.White, 0.2f), Sandstone.Light, Sandstone.Main, Sandstone.Shade));
            k.Px(Sandstone.Dark, x1 + 2, y1 + 4, x1 + 2, y1 + 5);
            if (variant == 1)
            {
                // The next drum lying apart, flat on its end.
                k.Col(22, 15, 8, 7, Sandstone);
                k.Ell(26f, 15f, 4f, 2f, new Ramp(Sandstone.Light.Mix(Rgba.White, 0.2f), Sandstone.Light, Sandstone.Main, Sandstone.Shade));
                k.Px(Sandstone.Dark, 26, 15);
            }
            if (variant == 2)
            {
                // Its capital beside it.
                k.Box(24, 17, 7, 5, Sandstone);
                k.Rect(25, 16, 5, 1, Sandstone.Light);
            }
            // The sand it lies in.
            k.Dome(8f, 24, 9f, 7f, SandHeap);
            k.Dome(21f, 24, 9f, 3.6f, SandHeap);
            return Stand(k, 30);
        }

        // A cave palm: a ringed trunk, a crown of dusty fronds, dates under it. What a sand cave's village has where a
        // forest would have a tree. The size of the old mushroom tree.
        static PixelImage CavePalm()
        {
            var k = new Kit(30, 40);
            // The trunk leans a little; rings up it.
            for (int y = 14; y < 40; y++)
            {
                int x = 13 + (y < 24 ? 1 : 0);
                k.Rect(x, y, 4, 1, PalmBark.Main).Px(x, y, PalmBark.Light).Px(x + 3, y, PalmBark.Shade);
                if (y % 3 == 0) k.Rect(x, y, 4, 1, PalmBark.Shade).Px(x + 3, y, PalmBark.Dark);
            }
            // The fronds: arcs out from the crown, each with its leaflets.
            void Frond(float dx, float dy, float droop, Ramp ramp)
            {
                for (float t = 0; t <= 1; t += 0.03f)
                {
                    int x = (int)MathF.Round(15.5f + dx * t), y = (int)MathF.Round(13 + dy * t + droop * t * t);
                    k.Px(x, y, t < 0.5f ? ramp.Light : ramp.Main).Px(x, y + 1, ramp.Shade);
                    if ((int)(t * 30) % 3 == 0) k.Px(x, y + 2, ramp.Dark).Px(x + (dx < 0 ? -1 : 1), y - 1, ramp.Main);
                }
            }
            var dusty = new Ramp(Rgba.Hex("#B8D08A"), Rgba.Hex("#86A85E"), Rgba.Hex("#5E8044"), Rgba.Hex("#3A5630"));
            Frond(-14, -9, 12, dusty);
            Frond(14, -9, 12, dusty);
            Frond(-13, -2, 14, dusty);
            Frond(13, -2, 14, dusty);
            Frond(-6, -12, 6, dusty);
            Frond(6, -12, 6, dusty);
            Frond(0, -13, 2, dusty);
            // The crown's heart and the dates.
            k.Ell(15.5f, 13f, 3f, 2.4f, PalmBark);
            k.Px(Rgba.Hex("#D88A3A"), 12, 16, 13, 17, 18, 16, 19, 17, 12, 18).Px(Rgba.Hex("#F0B060"), 12, 15, 18, 15);
            k.Dome(15f, 40, 7f, 3f, SandHeap);
            return Stand(k, 18, 32, 44);
        }
    }
}
