using System;
using Game.Core;

namespace Game.Client
{
    // The crystal cave's and the underground lake's props (Docs/BiomeRework.md 4-1 수정 동굴·지하 호수, 묶음 B3a), drawn
    // like the biomes' before them: shaded shapes lit from the upper left, a dark rim, a contact row, a crisp shadow
    // under the foot, three drawings each by the tile.
    //  - The crystal cave, a rift that bends the light: flat blue-grey slate, and out of it crystals of two colours,
    //    pink and sky, each with a pale core and a glint. No purple: that is the abyss's.
    //  - The underground lake, mossy banks and islands: wet limestone, moss on whatever is flat, pale shells, coral left
    //    by older water, soaked timber.
    static partial class BiomeArt
    {
        static readonly Ramp SlateRock = new Ramp(Rgba.Hex("#8E94A8"), Rgba.Hex("#626880"), Rgba.Hex("#454A5C"), Rgba.Hex("#2B2E3A"));
        static readonly Ramp PinkCrystal = new Ramp(Rgba.Hex("#FFD2F0"), Rgba.Hex("#FF7FD0"), Rgba.Hex("#C84CA2"), Rgba.Hex("#7E2C6C"));
        static readonly Ramp SkyCrystal = new Ramp(Rgba.Hex("#D6FCFF"), Rgba.Hex("#7FF2FF"), Rgba.Hex("#3EAED0"), Rgba.Hex("#1E6890"));
        static readonly Rgba CrystalGlint = Rgba.Hex("#F4F0FF"), PinkGlow = Rgba.Hex("#FF8AD8"), SkyGlow = Rgba.Hex("#8AF0FF");

        static readonly Ramp WetStone = new Ramp(Rgba.Hex("#94B0A8"), Rgba.Hex("#64847E"), Rgba.Hex("#445F5A"), Rgba.Hex("#2A403E"));
        static readonly Ramp LakeMoss = new Ramp(Rgba.Hex("#B4D068"), Rgba.Hex("#7A9440"), Rgba.Hex("#54702E"), Rgba.Hex("#364A20"));
        static readonly Ramp ShellPale = new Ramp(Rgba.Hex("#FFF8EA"), Rgba.Hex("#E8E0CC"), Rgba.Hex("#B8AE96"), Rgba.Hex("#857C68"));
        static readonly Ramp CoralRed = new Ramp(Rgba.Hex("#FFB09A"), Rgba.Hex("#EC7462"), Rgba.Hex("#B4483E"), Rgba.Hex("#782C2C"));
        static readonly Ramp CoralTeal = new Ramp(Rgba.Hex("#B4F4E8"), Rgba.Hex("#4FD2C0"), Rgba.Hex("#2E9488"), Rgba.Hex("#1C5E5A"));
        static readonly Ramp SoakedWood = new Ramp(Rgba.Hex("#9C8866"), Rgba.Hex("#6E5C44"), Rgba.Hex("#4A3C2C"), Rgba.Hex("#2E241A"));
        static readonly Ramp IsleEarth = new Ramp(Rgba.Hex("#6E8472"), Rgba.Hex("#4C6258"), Rgba.Hex("#344844"), Rgba.Hex("#20302E"));
        static readonly Ramp WillowLeaf = new Ramp(Rgba.Hex("#A8D488"), Rgba.Hex("#6EA860"), Rgba.Hex("#487C48"), Rgba.Hex("#2C5234"));
        static readonly Rgba Pearl = Rgba.Hex("#FBF6FF"), PearlShade = Rgba.Hex("#C8C0E0"), LakeGlow = Rgba.Hex("#70E6D8"), LakeCore = Rgba.Hex("#E0FFF8"), Ripple = Rgba.Hex("#8AE8E0");

        static bool CrystalLakeMirrors(TileType type) => type == TileType.CrystalCluster || type == TileType.CrystalSpire || type == TileType.CrackedGeode || type == TileType.CrystalMass
            || type == TileType.MossRock || type == TileType.Coral || type == TileType.ShellHeap || type == TileType.MossIsle;

        static int CrystalLakeCanopy(TileType type) => type switch
        {
            TileType.LeaningCrystal => 18,
            TileType.CrystalMass => 10,
            TileType.MossIsle => 12,
            _ => 0,
        };

        static PixelImage CrystalLakeProp(TileType type, int variant) => type switch
        {
            TileType.CrystalCluster => CrystalCluster(variant),
            TileType.CrystalSpire => CrystalSpire(variant),
            TileType.ResonantCrystal => ResonantCrystal(variant),
            TileType.CrackedGeode => CrackedGeode(variant),
            TileType.CrystalMass => CrystalMass(variant),
            TileType.LeaningCrystal => LeaningCrystal(variant),
            TileType.MossRock => MossRock(variant),
            TileType.Coral => Coral(variant),
            TileType.GiantClam => GiantClam(variant),
            TileType.ShellHeap => ShellHeap(variant),
            TileType.Wreckage => Wreckage(variant),
            TileType.MossIsle => MossIsle(variant),
            _ => null,
        };

        // A faint halo of a crystal's own colour on a few pixels round it.
        static PixelImage Halo(PixelImage image, Rgba glow, params (int X, int Y)[] at)
        {
            int cx = image.Width / 2;
            foreach (var (dx, dy) in at)
                if (image[cx + dx, dy].A == 0) image.Blend(cx + dx, dy, glow.WithAlpha(90));
            return image;
        }

        // ---- The crystal cave ----

        // A cluster of crystals, pink and sky together, grown from a crack in the slate: picked for its ore.
        static PixelImage CrystalCluster(int variant)
        {
            var k = new Kit(16, 15);
            var first = variant == 1 ? SkyCrystal : PinkCrystal;
            var second = variant == 1 ? PinkCrystal : SkyCrystal;
            k.Spike(4f, 15, 2.6f, 1.5f, 6, second);
            k.Spike(12.5f, 15, 2.4f, 14.5f, 7, variant == 2 ? second : first);
            k.Spike(8.5f, 15, 3.2f, variant == 2 ? 7f : 9f, variant == 2 ? 2 : 0, first);
            if (variant != 0) k.Spike(6f, 15, 1.6f, 5f, 10, first);
            // The pale core of each and a glint on the tallest.
            k.Px(CrystalGlint, 8, 5, 8, 8, 4, 11).Px(first.Light, 7, 4).Px(second.Light, 12, 11);
            // Chips of slate round the foot.
            k.Dome(8f, 15, 7f, 2f, SlateRock);
            k.Px(first.Main, 3, 14).Px(second.Main, 11, 14);
            return Halo(Stand(k, 13), variant == 1 ? SkyGlow : PinkGlow, (-7, 8), (7, 9), (0, 0), (-4, 3), (5, 3));
        }

        // A spire of crystal taller than its tile: one long shard, a shorter one against it.
        static PixelImage CrystalSpire(int variant)
        {
            var k = new Kit(14, 28);
            var tall = variant == 1 ? PinkCrystal : SkyCrystal;
            var low = variant == 1 ? SkyCrystal : PinkCrystal;
            if (variant == 2)
            {
                // Two of a height, leaning apart.
                k.Spike(4.5f, 28, 2.8f, 2.5f, 5, tall);
                k.Spike(9.5f, 28, 2.8f, 11.5f, 8, low);
            }
            else
            {
                k.Spike(10f, 28, 2.4f, 11.5f, 14, low);
                k.Spike(6f, 28, 3.4f, 6.5f, 0, tall);
                k.Spike(3f, 28, 1.8f, 1.5f, 18, low);
            }
            // The ridge's light and the glints.
            k.Ln(5, 6, 5, 22, tall.Light).Px(CrystalGlint, 5, 9, 5, 15, 10, 19);
            k.Dome(7f, 28, 6.5f, 2.4f, SlateRock);
            return Halo(Stand(k, 11), variant == 1 ? PinkGlow : SkyGlow, (-6, 14), (6, 12), (0, 0), (-4, 6), (4, 5));
        }

        // A crystal that rings when struck: a squat six-sided prism standing clear on a slab of slate, hairline rings in
        // the air beside it.
        static PixelImage ResonantCrystal(int variant)
        {
            var k = new Kit(18, 20);
            var ramp = variant == 1 ? PinkCrystal : SkyCrystal;
            // The slab.
            k.Box(3, 15, 12, 5, SlateRock);
            k.Px(SlateRock.Light, 4, 15, 5, 15).Px(SlateRock.Dark, 8, 17, 11, 18);
            // The prism: a flat-topped shaft with a point, its middle facet pale.
            k.Col(6, 5, 6, 10, ramp);
            k.Spike(9f, 5, 3f, 9f, 0, ramp);
            k.Rect(8, 3, 1, 11, ramp.Light).Px(CrystalGlint, 8, 5, 8, 9);
            if (variant == 2) k.Col(12, 10, 3, 5, PinkCrystal).Spike(13.5f, 10, 1.5f, 13.5f, 7, PinkCrystal);
            var image = Stand(k, 13);
            // The rings: two short arcs at each side, fainter outward.
            int cx = image.Width / 2;
            foreach (int side in new[] { -1, 1 })
            {
                foreach (var (dx, dy, a) in new[] { (7, 6, 150), (8, 7, 150), (8, 8, 150), (7, 9, 150) })
                    if (image[cx + side * dx - (side < 0 ? 1 : 0), dy].A == 0) image.Blend(cx + side * dx - (side < 0 ? 1 : 0), dy, CrystalGlint.WithAlpha(a));
            }
            return image;
        }

        // A small geode cracked open: a rough grey shell, the hollow lined with crystal: broken for what is in it.
        static PixelImage CrackedGeode(int variant)
        {
            var k = new Kit(16, 14);
            var inside = variant == 1 ? SkyCrystal : PinkCrystal;
            var other = variant == 1 ? PinkCrystal : SkyCrystal;
            // The lower half of the shell, a bowl; its broken rim pale; the dark of the hollow seen over the rim.
            k.Ell(8f, 9f, 7f, 4.6f, SlateRock);
            k.Ell(8f, 7.5f, 5.6f, 2.6f, new Ramp(Rgba.Hex("#1A1E2C"), Rgba.Hex("#1A1E2C"), Rgba.Hex("#141722"), Rgba.Hex("#141722")));
            for (int x = 2; x < 14; x++)
                if (k.I[x, 6].A > 0 && k.I[x, 5].A == 0 || x == 2 || x == 13) k.Px(x, 6, SlateRock.Light);
            // The crystals standing up out of it.
            k.Spike(5.5f, 9, 1.6f, 4.5f, variant == 2 ? 3 : 4, inside);
            k.Spike(8.5f, 9, 2f, 8.5f, variant == 2 ? 0 : 1, inside);
            k.Spike(11f, 9, 1.5f, 12f, 4, variant == 0 ? inside : other);
            k.Px(CrystalGlint, 8, 3, 5, 6);
            // The front of the bowl over their feet, a crack down it, the piece that fell away.
            k.Rect(3, 9, 10, 1, SlateRock.Main).Rect(3, 9, 4, 1, SlateRock.Light);
            k.Ln(9, 10, 10, 12, SlateRock.Dark);
            if (variant != 1) k.Box(variant == 2 ? 13 : 0, 11, 3, 3, SlateRock).Px(inside.Main, variant == 2 ? 14 : 1, 10);
            return Stand(k, 14);
        }

        // A great mass of crystal two tiles wide, taller than a person: thick shards grown out of one root. What a shot
        // would glance off.
        static PixelImage CrystalMass(int variant)
        {
            var k = new Kit(30, 32);
            var a = variant == 1 ? SkyCrystal : PinkCrystal;
            var b = variant == 1 ? PinkCrystal : SkyCrystal;
            // Behind: the outer shards leaning away.
            k.Spike(5f, 32, 4f, 1.5f, 12, b);
            k.Spike(25f, 32, 4f, 28.5f, 14, variant == 2 ? a : b);
            k.Spike(20f, 32, 4.5f, 22.5f, 4, a);
            // The great one in the middle and a shoulder against it.
            k.Spike(13f, 32, 6.5f, 12f, 0, a);
            k.Spike(8.5f, 32, 3.6f, 7f, 15, variant == 2 ? b : a);
            if (variant != 1) k.Spike(17f, 32, 3f, 18f, 18, b);
            // Facet edges and light held inside.
            k.Ln(11, 6, 10, 28, a.Light).Ln(14, 4, 16, 28, a.Shade);
            k.Px(CrystalGlint, 11, 9, 11, 16, 20, 10, 5, 18, 12, 23).Px(a.Light, 12, 10, 12, 17);
            k.Dome(15f, 32, 14f, 3.2f, SlateRock);
            k.Px(a.Main, 4, 30, 9, 31).Px(b.Main, 22, 30, 26, 31);
            return Halo(Stand(k, 28), variant == 1 ? SkyGlow : PinkGlow, (-14, 20), (14, 22), (-2, 0), (-9, 9), (9, 5), (12, 12));
        }

        // A pillar of crystal three tiles long that leans, two tiles of footprint: its root in broken slate, its point
        // high over the tile beside it (walked behind; it fades).
        static PixelImage LeaningCrystal(int variant)
        {
            var k = new Kit(32, 42);
            var ramp = variant == 1 ? PinkCrystal : SkyCrystal;
            var other = variant == 1 ? SkyCrystal : PinkCrystal;
            // The shaft: seven pixels across, its foot at the left, its point up to the right.
            int reach = variant == 2 ? 14 : 18;
            for (int y = 4; y < 40; y++)
            {
                float t = (39 - y) / 35f;
                int x = 5 + (int)MathF.Round(reach * t);
                int w = t > 0.86f ? Math.Max(1, (int)MathF.Round(7 * (1 - t) / 0.14f)) : 7;
                for (int i = 0; i < w; i++)
                    k.Px(x + i + (7 - w) / 2, y, i < 2 ? ramp.Light : i < 4 ? ramp.Main : i < 6 ? ramp.Shade : ramp.Dark);
            }
            // A bright edge along its upper facet, glints, a flaw across it.
            for (int y = 8; y < 38; y += 1)
            {
                float t = (39 - y) / 35f;
                if (y % 9 < 6) k.Px(5 + (int)MathF.Round(reach * t) + 2, y, CrystalGlint.Mix(ramp.Light, 0.5f));
            }
            k.Px(CrystalGlint, 5 + (int)MathF.Round(reach * 0.7f) + 2, 14, 5 + (int)MathF.Round(reach * 0.3f) + 2, 28);
            int flaw = variant == 0 ? 22 : 26;
            k.Ln(5 + (int)MathF.Round(reach * (39 - flaw) / 35f), flaw, 5 + (int)MathF.Round(reach * (39 - flaw) / 35f) + 6, flaw + 2, ramp.Dark);
            // The smaller ones at its root, and the slate it broke through.
            k.Spike(20f, 42, 3f, 23f, 28, other);
            if (variant != 2) k.Spike(26f, 42, 2.4f, 27.5f, 33, ramp);
            k.Spike(4f, 42, 2.2f, 2f, 32, other);
            k.Px(CrystalGlint, 21, 34);
            k.Dome(13f, 42, 12f, 4f, SlateRock);
            k.Box(22, 38, 6, 4, SlateRock);
            return Halo(Stand(k, 28), variant == 1 ? PinkGlow : SkyGlow, (-12, 30), (2, 8), (12, 4), (14, 26));
        }

        // A tall growth of crystal with shards for boughs: what a crystal cave's village has where a forest would have a
        // tree. The size of the old mushroom tree.
        static PixelImage CrystalTree()
        {
            var k = new Kit(30, 40);
            var pale = new Ramp(CrystalGlint, SkyCrystal.Light, SkyCrystal.Main, SkyCrystal.Shade);
            // The boughs first, behind: shards out and up, pink to the left, sky to the right.
            k.Spike(13f, 26, 2.8f, 2f, 13, PinkCrystal).Spike(13f, 20, 3f, 5f, 3, PinkCrystal);
            k.Spike(17f, 26, 2.8f, 28f, 14, SkyCrystal).Spike(17f, 20, 3f, 25f, 4, SkyCrystal);
            k.Spike(12f, 32, 2f, 5f, 26, SkyCrystal).Spike(18f, 33, 2f, 25f, 27, PinkCrystal);
            // The trunk: one great pale shard from the ground to its point.
            k.Spike(15f, 38, 4.4f, 15f, 0, pale);
            k.Ln(14, 6, 13, 36, CrystalGlint).Ln(17, 10, 18, 36, SkyCrystal.Shade);
            k.Px(CrystalGlint, 7, 8, 23, 9, 4, 15, 26, 16).Px(PinkGlow, 9, 12).Px(SkyGlow, 21, 13);
            k.Dome(15f, 40, 8f, 3.4f, SlateRock);
            k.Px(PinkCrystal.Main, 9, 38).Px(SkyCrystal.Main, 20, 39);
            return Halo(Stand(k, 20, 32, 44), PinkGlow, (-13, 14), (13, 14), (-10, 6), (10, 6), (0, 2));
        }

        // ---- The underground lake ----

        // A rock by the water, wet and mossed over.
        static PixelImage MossRock(int variant)
        {
            var k = new Kit(18, 14);
            if (variant == 1)
            {
                // Two, the smaller before the larger.
                k.Dome(7f, 13, 6.5f, 10f, WetStone);
                k.Dome(13f, 14, 4.5f, 6f, WetStone);
                k.Speck(LakeMoss.Main, 0.55f, 71, 3, 7).Speck(LakeMoss.Light, 0.2f, 72, 3, 6);
                k.Px(LakeMoss.Main, 12, 9, 13, 8, 14, 9);
            }
            else
            {
                k.Dome(9f, 14, 8f, variant == 2 ? 9f : 11f, WetStone);
                // Moss lying on its top, creeping down the lit side.
                int top = variant == 2 ? 5 : 3;
                k.Speck(LakeMoss.Main, 0.7f, 73 + variant, top, top + 4).Speck(LakeMoss.Light, 0.25f, 75, top, top + 3).Speck(LakeMoss.Shade, 0.25f, 76 + variant, top + 4, top + 7);
            }
            // The wet shine, and a crack.
            k.Px(WetStone.Light, 4, 9, 5, 8).Px(WetStone.Dark, 10, 10, 11, 11);
            if (variant == 2)
            {
                // A little cap growing in its lee.
                k.Rect(15, 11, 1, 3, Stem.Main).Rect(14, 10, 3, 1, CoralTeal.Main).Px(LakeCore, 15, 10);
            }
            return Stand(k, 16);
        }

        // Coral left from when the water stood higher: a red branching clump, a teal fan, a pale brain of it.
        static PixelImage Coral(int variant)
        {
            var k = new Kit(16, 16);
            if (variant == 0)
            {
                // Branches forking up from a foot.
                k.Ln(8, 15, 8, 8, CoralRed.Main, 1).Ln(7, 15, 7, 9, CoralRed.Light).Ln(9, 14, 9, 9, CoralRed.Shade);
                k.Ln(7, 9, 3, 4, CoralRed.Main).Ln(4, 5, 4, 1, CoralRed.Light).Ln(3, 4, 1, 3, CoralRed.Main);
                k.Ln(8, 8, 8, 2, CoralRed.Main).Ln(8, 5, 10, 3, CoralRed.Shade);
                k.Ln(9, 10, 13, 6, CoralRed.Shade).Ln(13, 6, 13, 3, CoralRed.Main).Ln(12, 7, 14, 6, CoralRed.Shade);
                k.Ln(6, 12, 3, 10, CoralRed.Main).Ln(10, 13, 12, 11, CoralRed.Shade);
                k.Px(CoralRed.Light, 4, 1, 8, 2, 13, 3, 1, 3, 3, 10);
            }
            else if (variant == 1)
            {
                // A fan: a flat lattice on a short stalk.
                k.Dome(8f, 12, 7f, 11f, CoralTeal);
                for (int x = 2; x < 15; x += 2) k.Ln(8, 12, x, 2 + Math.Abs(x - 8) / 2, CoralTeal.Dark);
                k.Tint(5, 0.85f).Tint(8, 0.85f);
                k.Rect(7, 12, 2, 4, CoralTeal.Shade).Px(CoralTeal.Light, 5, 3, 8, 2, 11, 4);
            }
            else
            {
                // A brain coral: a pale dome grooved all over.
                k.Dome(8f, 16, 7f, 9f, new Ramp(ShellPale.Light, CoralRed.Light, CoralRed.Main, CoralRed.Shade));
                foreach (var (x0, y0, x1, y1) in new[] { (3, 12, 6, 10), (6, 10, 9, 12), (9, 12, 12, 10), (4, 14, 8, 14), (9, 9, 11, 8), (5, 9, 7, 8) })
                    k.Ln(x0, y0, x1, y1, CoralRed.Shade);
            }
            // The stony foot and a shell beside it.
            k.Dome(8f, 16, 5f, 2f, WetStone);
            k.Px(ShellPale.Main, 13, 15).Px(ShellPale.Light, 13, 14);
            return Stand(k, 12);
        }

        // A giant clam, its ridged shell agape: a pearl sits in it. Picked for the pearl.
        static PixelImage GiantClam(int variant)
        {
            var k = new Kit(20, 16);
            var lining = new Ramp(Rgba.Hex("#FFF4F0"), Rgba.Hex("#F4D8D0"), Rgba.Hex("#D0A8A8"), Rgba.Hex("#9A7880"));
            // The upper valve standing open behind: a fan, its inside toward us, ridged from the hinge, scalloped along
            // its edge. Half shut on one drawing.
            float tall = variant == 1 ? 5f : 9f;
            k.Dome(10f, 10, 9f, tall, lining);
            foreach (int x in new[] { 2, 5, 8, 12, 15, 18 }) k.Ln(10, 10, x, 10 - (int)(tall * MathF.Sqrt(Math.Max(0, 1 - (x - 10) * (x - 10) / 81f))) + 1, lining.Shade);
            for (int x = 1; x < 19; x += 3)
                for (int y = 0; y < 10; y++)
                    if (k.I[x, y].A > 0)
                    {
                        k.Px(x, y, Rgba.Clear);
                        break;
                    }
            // The lower valve, a ridged bowl, and the mantle between them: dark, a teal lip, the pearl.
            k.Ell(10f, 12f, 9.5f, 3.6f, ShellPale);
            k.Ell(10f, 10.6f, 7.5f, 1.7f, new Ramp(CoralTeal.Main, CoralTeal.Shade, Rgba.Hex("#16242A"), Rgba.Hex("#16242A")));
            if (variant != 1) k.Px(Pearl, variant == 2 ? 12 : 9, 10, variant == 2 ? 13 : 10, 10).Px(PearlShade, variant == 2 ? 13 : 10, 11).Px(Rgba.White, variant == 2 ? 12 : 9, 9);
            foreach (int x in new[] { 3, 6, 9, 12, 15 }) k.Px(x, 13, ShellPale.Shade).Px(x, 14, ShellPale.Shade).Px(x + 1, 12, ShellPale.Light);
            k.Px(LakeMoss.Main, 1, 13, 2, 14, 17, 14).Px(LakeMoss.Light, 1, 12);
            return Stand(k, 17);
        }

        // A heap of shells washed up: small clams and a whorl on top. Broken for what is among them.
        static PixelImage ShellHeap(int variant)
        {
            var k = new Kit(18, 11);
            k.Dome(9f, 11, 8f, variant == 1 ? 6f : 8f, ShellPale);
            // Each shell's lower edge, so the heap reads as many.
            for (int i = 0; i < 9; i++)
            {
                int x = 2 + (int)(PixelNoise.Value(i, variant, 131) * 13), y = 5 + (int)(PixelNoise.Value(i, variant, 132) * 5);
                if (k.I[x, y].A == 0 || k.I[x + 1, y].A == 0) continue;
                k.Px(ShellPale.Shade, x, y, x + 1, y).Px(x + 2, y - 1, ShellPale.Shade).Px(x, y - 1, ShellPale.Light);
            }
            // A whorled one and a pink one.
            if (variant != 1) k.Px(CoralRed.Light, 6, 4, 7, 4, 7, 5).Px(CoralRed.Main, 6, 5, 8, 5).Px(ShellPale.Light, 7, 3);
            k.Px(CoralRed.Light, 12, 7, 13, 7).Px(CoralRed.Main, 12, 8);
            if (variant == 2) k.Px(Pearl, 10, 6).Px(PearlShade, 10, 7);
            k.Px(CoralTeal.Main, 4, 8, 5, 8).Px(CoralTeal.Shade, 4, 9).Px(ShellPale.Dark, 9, 9, 10, 9, 14, 9);
            k.Px(LakeMoss.Main, 1, 10, 16, 10);
            return Stand(k, 16);
        }

        // The wreckage of a boat two tiles wide, cast up and rotting: ribs and strakes of soaked timber, moss on it.
        static PixelImage Wreckage(int variant)
        {
            var k = new Kit(32, 22);
            if (variant == 1)
            {
                // Planks heaped, a barrel among them.
                for (int i = 0; i < 4; i++) k.Ln(3 + i, 19 - i * 2, 22 + i, 13 - i * 2, i % 2 == 0 ? SoakedWood.Main : SoakedWood.Shade, 2);
                k.Ln(4, 14, 20, 20, SoakedWood.Light, 2).Ln(4, 16, 20, 22, SoakedWood.Dark);
                k.Col(21, 10, 8, 11, SoakedWood);
                k.Rect(21, 12, 8, 1, OldIron.Shade).Rect(21, 18, 8, 1, OldIron.Shade).Rect(22, 9, 6, 1, SoakedWood.Light);
            }
            else
            {
                // The hull's bow, on its side: the keel along the ground, ribs standing from it, a few strakes still on.
                k.Ln(2, 19, 28, 19, SoakedWood.Main, 3);
                k.Ln(2, 19, 28, 19, SoakedWood.Light);
                int ribs = variant == 2 ? 4 : 5;
                for (int i = 0; i < ribs; i++)
                {
                    int x = 5 + i * 5, top = 5 + Math.Abs(i - 2) * 2 + (variant == 2 && i == 1 ? 7 : 0);
                    k.Ln(x, 19, x + 2, top, SoakedWood.Main, 1).Ln(x + 1, 19, x + 3, top, SoakedWood.Shade);
                    k.Px(x + 2, top, SoakedWood.Light);
                }
                // Strakes across the ribs, broken short.
                k.Ln(4, 15, variant == 2 ? 16 : 24, 14, SoakedWood.Main, 2).Ln(4, 15, variant == 2 ? 16 : 24, 14, SoakedWood.Light);
                k.Ln(6, 11, 18, 10, SoakedWood.Shade, 2).Ln(6, 11, 18, 10, SoakedWood.Main);
                // The stem post curving up at the bow.
                k.Ln(27, 19, 30, 8, SoakedWood.Main, 2).Ln(28, 19, 31, 8, SoakedWood.Dark).Px(SoakedWood.Light, 30, 7);
                k.Px(OldIron.Light, 10, 15, 18, 14).Px(OldIron.Shade, 8, 11);
            }
            // Moss and a shell on it, wet sand heaped against it.
            k.Speck(LakeMoss.Main, 0.16f, 141 + variant, 4, 18);
            k.Dome(8f, 22, 8f, 3f, WetStone);
            k.Dome(24f, 22, 7f, 2.4f, WetStone);
            k.Px(ShellPale.Light, 15, 21).Px(ShellPale.Main, 16, 21);
            return Stand(k, 30);
        }

        // An islet in a pool, two tiles of its water: a hump of dark earth ringed by a pale waterline, mossed over, with
        // glowing caps on it, a small willow, or reeds round a stone.
        static PixelImage MossIsle(int variant)
        {
            var k = new Kit(32, 32);
            // The hump and its moss.
            k.Dome(16f, 31, 15f, 7f, IsleEarth);
            k.Dome(16f, 29, 13f, 5.5f, LakeMoss);
            k.Speck(LakeMoss.Light, 0.18f, 151 + variant, 23, 27).Speck(LakeMoss.Shade, 0.2f, 153, 26, 29);
            // The waterline: a broken pale ring where the pool laps it.
            for (int x = 1; x < 31; x++)
                if (PixelNoise.Value(x, variant, 155) < 0.6f) k.Px(x, 31, Ripple);
            if (variant == 0)
            {
                // Glowing caps: three teal ones, the tallest in the middle.
                foreach (var (x, top, r) in new[] { (9, 17, 3.5f), (16, 10, 5f), (23, 18, 3f) })
                {
                    k.Col(x, top + 2, 2, 26 - top - 2, Stem);
                    k.Dome(x + 1f, top + 4, r, 4f, CoralTeal);
                    k.Px(LakeCore, x, top + 1).Px(LakeGlow, x - 1, top + 4, x + 2, top + 4);
                }
                k.Px(LakeGlow, 13, 25, 20, 26).Px(LakeCore, 13, 24);
            }
            else if (variant == 1)
            {
                // A small willow: a bent trunk, its leaves hanging to the water.
                k.Ln(16, 26, 15, 12, SoakedWood.Main, 1).Ln(17, 26, 16, 12, SoakedWood.Shade).Ln(15, 12, 11, 8, SoakedWood.Main).Ln(16, 12, 21, 7, SoakedWood.Shade);
                k.Dome(16f, 12, 11f, 11f, WillowLeaf);
                for (int x = 6; x < 27; x += 2)
                {
                    int length = 5 + (int)(PixelNoise.Value(x, 3, 157) * 8);
                    for (int j = 0; j < length; j++) k.Px(x, 11 + j, j % 3 == 2 ? WillowLeaf.Shade : x < 14 ? WillowLeaf.Light : WillowLeaf.Main);
                }
                k.Ln(16, 20, 16, 26, SoakedWood.Main).Ln(17, 20, 17, 26, SoakedWood.Shade);
            }
            else
            {
                // A stone and reeds.
                k.Dome(19f, 26, 6f, 7f, WetStone);
                k.Speck(LakeMoss.Main, 0.5f, 159, 19, 22);
                foreach (var (x, top) in new[] { (7, 9), (9, 12), (11, 7), (13, 13), (26, 14), (28, 11) })
                {
                    k.Ln(x, 26, x + (x < 16 ? -1 : 1), top, x % 2 == 1 ? LakeMoss.Main : LakeMoss.Light);
                    k.Px(x + (x < 16 ? -1 : 1), top, SoakedWood.Main).Px(x + (x < 16 ? -1 : 1), top + 1, SoakedWood.Shade);
                }
            }
            return Stand(k, 30);
        }

        // A willow grown in the damp, its leaves hanging in curtains: what a lake's village has where a forest would have
        // a tree. The size of the old mushroom tree.
        static PixelImage LakeWillow()
        {
            var k = new Kit(30, 40);
            // The trunk, leaning a little, forked.
            k.Col(13, 16, 5, 24, SoakedWood);
            k.Ln(14, 17, 9, 9, SoakedWood.Main, 2).Ln(16, 17, 22, 8, SoakedWood.Shade, 2);
            k.Px(LakeMoss.Main, 13, 30, 13, 31, 14, 34).Px(SoakedWood.Dark, 15, 24, 16, 29);
            // The crown and the hanging curtains of leaf.
            k.Dome(15f, 15, 14.5f, 15f, WillowLeaf);
            for (int x = 1; x < 29; x++)
            {
                if (x > 11 && x < 19) continue;
                int length = 6 + (int)(PixelNoise.Value(x, 5, 161) * 13);
                for (int j = 0; j < length; j++)
                {
                    if (x % 2 == 1 && j > length - 3) continue;
                    k.Px(x, 14 + j, j % 4 == 3 ? WillowLeaf.Dark : x < 10 ? WillowLeaf.Light : x > 22 ? WillowLeaf.Shade : WillowLeaf.Main);
                }
            }
            k.Speck(WillowLeaf.Light, 0.14f, 163, 0, 8);
            k.Px(LakeGlow, 6, 22, 24, 25).Px(LakeCore, 6, 21);
            k.Dome(15f, 40, 7f, 2.6f, LakeMoss);
            return Stand(k, 20, 32, 44);
        }
    }
}
