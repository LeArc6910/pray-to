using Game.Core;

namespace Game.Client
{
    // The biomes' things to pick or break (Docs/Harvest.md, 2026-11-27): one kind to a biome, drawn like its other props,
    // full when grown and as a few stubs, a cracked husk or an empty bed once spent. Small (about one tile), so they
    // read as something to swing at among the bigger props; the spent forms lie flat, walked over.
    static partial class BiomeArt
    {
        static readonly Ramp BrownCap = new Ramp(Rgba.Hex("#E8B888"), Rgba.Hex("#B8784A"), Rgba.Hex("#86502C"), Rgba.Hex("#56321A"));
        static readonly Ramp Herb = new Ramp(Rgba.Hex("#D4F49A"), Rgba.Hex("#8CC85A"), Rgba.Hex("#5A9238"), Rgba.Hex("#365A22"));
        static readonly Ramp Amber = new Ramp(Rgba.Hex("#FFE08A"), Rgba.Hex("#F0A830"), Rgba.Hex("#B86A18"), Rgba.Hex("#7A400E"));
        static readonly Ramp PodSkin = new Ramp(Rgba.Hex("#C8A8F0"), Rgba.Hex("#8E64C8"), Rgba.Hex("#5E3E94"), Rgba.Hex("#38225E"));

        static bool HarvestMirrors(TileType type) => HarvestProps.Is(type) && type != TileType.ClamBed;

        static PixelImage HarvestProp(TileType type, int variant) => type switch
        {
            TileType.MushroomPatch => MushroomPatch(variant, true),
            TileType.MushroomPatchPicked => MushroomPatch(variant, false),
            TileType.HerbBush => HerbBush(variant, true),
            TileType.HerbBushPicked => HerbBush(variant, false),
            TileType.FrostGeode => Geode(variant, true, Snow, DeepIce, IceGlow, IceCore),
            TileType.FrostGeodeBroken => Geode(variant, false, Snow, DeepIce, IceGlow, IceCore),
            TileType.MithrilRock => Geode(variant, true, SlateRock, SkyCrystal, SkyGlow, CrystalGlint),
            TileType.MithrilRockBroken => Geode(variant, false, SlateRock, SkyCrystal, SkyGlow, CrystalGlint),
            TileType.PepperBush => PepperBush(variant, true),
            TileType.PepperBushPicked => PepperBush(variant, false),
            TileType.VoidPod => VoidPod(variant, true),
            TileType.VoidPodPicked => VoidPod(variant, false),
            TileType.ClamBed => ClamBed(variant, true),
            TileType.ClamBedEmpty => ClamBed(variant, false),
            TileType.AmberRock => Geode(variant, true, Sandstone, Amber, Amber.Main, Rgba.Hex("#FFF4C0")),
            TileType.AmberRockBroken => Geode(variant, false, Sandstone, Amber, Amber.Main, Rgba.Hex("#FFF4C0")),
            _ => null,
        };

        // Brown caps in a clump on the dirt; picked, their cut stems.
        static PixelImage MushroomPatch(int variant, bool grown)
        {
            var k = new Kit(14, 11);
            // Three clumps (2026-11-27, the art lint: the third was the first again): two and a small one, one big cap
            // between two, a tall one beside a low pair.
            var spots = variant == 1 ? new[] { (3f, 9, 2.5f), (8f, 7, 3.4f), (12f, 10, 2f) }
                : variant == 2 ? new[] { (4f, 6, 3.2f), (9f, 9, 2.2f), (12f, 8, 1.8f) }
                : new[] { (3f, 8, 3f), (8f, 9, 2.6f), (11f, 7, 2.6f) };
            foreach (var (x, by, r) in spots)
            {
                k.Col((int)x - 1, by - 1, 2, grown ? 11 - by : 2, Stem);
                if (grown)
                {
                    k.Dome(x, by, r, r * 0.9f, BrownCap);
                    k.Px(Rgba.Hex("#FFF0D8"), (int)x - 1, (int)(by - r * 0.6f));
                }
                else k.Px(Stem.Light, (int)x - 1, by - 1).Px(Stem.Light, (int)x, by - 1);
            }
            k.Dome(7f, 11, 6.5f, 1.4f, Earth);
            return Stand(k, 9);
        }

        // A bush of round leaves whose spore pods glow lime; picked, the leaves only.
        static PixelImage HerbBush(int variant, bool grown)
        {
            var k = new Kit(14, 13);
            k.Ln(7, 12, 7, 6, Herb.Dark).Ln(7, 9, 4, 6, Herb.Dark).Ln(7, 8, 10, 5, Herb.Dark);
            k.Ell(4f, 6f, 3f, 2.4f, Herb).Ell(10f, 5f, 3f, 2.4f, Herb).Ell(7f, 4f, 3.2f, 2.6f, Herb);
            if (variant != 1) k.Ell(7f, 8f, 3.6f, 2f, Herb);
            // The third leans: a low leaf out to one side, its pods elsewhere.
            if (variant == 2) k.Ln(7, 10, 2, 9, Herb.Dark).Ell(2.5f, 9f, 2.2f, 1.6f, Herb);
            if (grown)
            {
                foreach (var (x, y) in variant == 2 ? new[] { (4, 4), (9, 4), (7, 2), (2, 8), (9, 8) } : new[] { (3, 5), (10, 3), (7, 2), (5, 8), (11, 7) })
                    k.Px(Lime, x, y).Px(LimeCore, x, y - 1);
            }
            else k.Speck(Herb.Shade, 0.35f, 71, 2, 10);
            k.Dome(7f, 13, 5f, 1.4f, Earth);
            return Stand(k, 11);
        }

        // A rock with crystals or amber showing through its crust; broken, the split halves and a few chips.
        static PixelImage Geode(int variant, bool grown, Ramp crust, Ramp gem, Rgba glow, Rgba core)
        {
            var k = new Kit(15, 13);
            if (grown)
            {
                k.Dome(7.5f, 13, 6.5f, variant == 2 ? 7f : 8.5f, crust);
                // The gems through the crust.
                k.Spike(6f, 9, 1.8f, 5f, 3, gem).Spike(9.5f, 10, 1.6f, 11f, 5, gem);
                if (variant != 1) k.Spike(4f, 11, 1.2f, 2.5f, 7, gem);
                k.Px(core, 5, 4, 10, 6).Px(glow, 6, 7, 9, 8);
                var image = Stand(k, 11);
                foreach (var (dx, dy) in new[] { (-5, 4), (5, 6), (0, 1) }) image.Blend(image.Width / 2 + dx, 2 + dy, glow.WithAlpha(80));
                return image;
            }
            // The halves by the drawing: a tall and a low one, the two the other way round with chips flung wide, one
            // half and a heap of rubble.
            if (variant == 1)
            {
                k.Dome(4f, 13, 3.2f, 3f, crust).Dome(10.5f, 13, 4f, 4.6f, crust);
                k.Ell(4f, 11f, 1.5f, 1f, gem).Ell(10.5f, 10f, 2f, 1.3f, gem);
                k.Px(crust.Shade, 7, 12, 14, 12).Px(gem.Main, 0, 12, 7, 11);
            }
            else if (variant == 2)
            {
                k.Dome(5f, 13, 4.4f, 4.4f, crust).Dome(11.5f, 13, 2.6f, 1.8f, crust);
                k.Ell(5.5f, 10.5f, 2.2f, 1.3f, gem);
                k.Px(crust.Shade, 9, 12, 10, 11, 13, 10).Px(crust.Light, 12, 11).Px(gem.Main, 11, 11, 9, 11);
            }
            else
            {
                k.Dome(4f, 13, 3.5f, 4f, crust).Dome(11f, 13, 3.5f, 3.4f, crust);
                k.Ell(4.5f, 10.5f, 1.8f, 1.2f, gem).Ell(10.5f, 11f, 1.6f, 1f, gem);
                k.Px(crust.Shade, 7, 12, 8, 11).Px(gem.Main, 7, 11);
            }
            return Stand(k, 11);
        }

        // A bush of flame leaves hung with red peppers; picked, the bare charred twigs and a few leaves.
        static PixelImage PepperBush(int variant, bool grown)
        {
            var k = new Kit(14, 14);
            k.Ln(7, 13, 7, 7, Charred.Main).Ln(7, 9, 3, 5, Charred.Main).Ln(7, 8, 11, 4, Charred.Shade);
            k.Ell(4f, 5f, 2f, 2.4f, FlameLeaf).Ell(10f, 4f, 2f, 2.4f, FlameLeaf);
            if (grown || variant == 0) k.Ell(7f, 3f, 2.2f, 2.6f, FlameLeaf);
            // The second has a low branch out to the right, the third to the left: bare or in leaf, each its own.
            if (variant == 1) k.Ln(7, 11, 11, 9, Charred.Shade).Ell(11.5f, 8.5f, 1.6f, 1.4f, FlameLeaf);
            if (variant == 2) k.Ln(7, 11, 3, 10, Charred.Shade).Ell(2.5f, 9.5f, 1.6f, 1.4f, FlameLeaf);
            if (grown)
            {
                var hung = variant == 1 ? new[] { (5, 8), (9, 6), (3, 8), (12, 10) } : variant == 2 ? new[] { (5, 7), (9, 8), (2, 11), (11, 7) } : new[] { (5, 8), (9, 7), (3, 9), (11, 9) };
                foreach (var (x, y) in hung)
                    k.Px(Rgba.Hex("#E03A2A"), x, y).Px(Rgba.Hex("#E03A2A"), x, y + 1).Px(Rgba.Hex("#8E1E1A"), x, y + 2).Px(Herb.Shade, x, y - 1);
            }
            k.Dome(7f, 14, 4.5f, 1.6f, Basalt);
            return Stand(k, 12);
        }

        // Violet pods hanging from a twisted stalk, beans glowing through their skins; picked, split husks.
        static PixelImage VoidPod(int variant, bool grown)
        {
            var k = new Kit(14, 15);
            k.Ln(7, 14, 7, 3, VineBark.Main, 1).Ln(7, 4, 10, 1, VineBark.Shade).Ln(7, 6, 4, 3, VineBark.Shade);
            // Three pods, two, or three hung lower with a second twig.
            if (variant == 2) k.Ln(7, 9, 4, 8, VineBark.Shade);
            foreach (var (x, y) in variant == 1 ? new[] { (4, 6), (10, 5) } : variant == 2 ? new[] { (3, 8), (10, 4), (10, 9) } : new[] { (4, 6), (10, 4), (9, 9) })
            {
                if (grown)
                {
                    k.Ell(x, y + 1.5f, 1.6f, 2.6f, PodSkin);
                    k.Px(VoidGlow, x, y + 1).Px(Starlight, x, y + 2);
                }
                else k.Px(PodSkin.Shade, x - 1, y + 1, x + 1, y + 1, x - 1, y + 2, x + 1, y + 3);
            }
            k.Dome(7f, 15, 4.5f, 1.6f, VoidStone);
            return Stand(k, 13);
        }

        // A heap of clams half sunk in wet sand, one open on its pearl; emptied, broken shells.
        static PixelImage ClamBed(int variant, bool grown)
        {
            var k = new Kit(16, 10);
            k.Ell(8f, 8f, 7.5f, 2.2f, WetStone);
            // The heap by the drawing (not mirrored: the open clam keeps its lit side): five, four with the low pair
            // apart, five and a second pearl; emptied, two shells, three, or two the other way.
            var shells = grown
                ? variant == 1 ? new[] { (3.5f, 6.5f), (8f, 5f), (12f, 6f), (8.5f, 8.5f) } : new[] { (3f, 6f), (8f, 5f), (12.5f, 6.5f), (6f, 8f), (10.5f, 8.5f) }
                : variant == 1 ? new[] { (3f, 8f), (8f, 6.5f), (12.5f, 8f) } : variant == 2 ? new[] { (5.5f, 8.5f), (11.5f, 7f) } : new[] { (4f, 7.5f), (11f, 8f) };
            foreach (var (x, y) in shells) k.Ell(x, y, 2.2f, 1.6f, ShellPale).Px(ShellPale.Shade, (int)x, (int)y + 1);
            if (grown)
            {
                k.Px(Pearl, 8, 4).Px(PearlShade, 8, 5).Px(Rgba.White, 7, 4);
                if (variant == 2) k.Px(Pearl, 13, 6);
            }
            else if (variant == 0) k.Px(ShellPale.Shade, 7, 8, 8, 7, 9, 8);
            else k.Px(ShellPale.Shade, 8, 9, 2, 6);
            k.Px(LakeMoss.Main, 1, 8, 14, 9);
            return Stand(k, 8);
        }
    }
}
