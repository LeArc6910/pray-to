using System;
using Game.Core;

namespace Game.Client
{
    // The biomes' own props, drawn as things that stand on the floor apart from it (Docs/BiomeRework.md 4-1, 4-3 그리는
    // 규칙, 묶음 B1a: the dirt cave and the mushroom forest; the mockups of DevTools/ArtPreview's BiomeMockArt drawn
    // properly). Each is built from shaded shapes lit from the upper left, ringed in the dark outline, its lowest row
    // darkened where it meets the floor, and set on a crisp oval shadow straight under its foot (PropArt.Shade). The
    // picture's bottom edge is the bottom edge of its footprint and its middle the footprint's middle, so WorldView's
    // standing layer and the preview's scenes place it like every other structure. Variants by the tile (Variant):
    // two or three drawings each and, for what has no lit side to speak of, mirrored.
    static partial class BiomeArt
    {
        static readonly Rgba Ink = Rgba.Hex("#140E18");
        // The dirt cave: packed earth and old mine timber, lamp amber.
        static readonly Ramp Earth = Ramp.Of("#7A5E44");
        static readonly Ramp Rock = new Ramp(Rgba.Hex("#9A8062"), Rgba.Hex("#6E5844"), Rgba.Hex("#4E3E30"), Rgba.Hex("#32261E"));
        static readonly Ramp Timber = new Ramp(Rgba.Hex("#C89458"), Rgba.Hex("#8E6236"), Rgba.Hex("#644224"), Rgba.Hex("#402814"));
        static readonly Ramp OldIron = new Ramp(Rgba.Hex("#9AA0AC"), Rgba.Hex("#6A7080"), Rgba.Hex("#484C5A"), Rgba.Hex("#2E303A"));
        static readonly Ramp Sack = new Ramp(Rgba.Hex("#D8C294"), Rgba.Hex("#B09A6A"), Rgba.Hex("#80704A"), Rgba.Hex("#564A30"));
        static readonly Ramp CopperOre = new Ramp(Rgba.Hex("#F0B070"), Rgba.Hex("#D08A4A"), Rgba.Hex("#985A2C"), Rgba.Hex("#603818"));
        static readonly Rgba LampGlow = Rgba.Hex("#FFB060"), LampCore = Rgba.Hex("#FFF0B0"), LampOut = Rgba.Hex("#4A4038");
        static readonly Rgba Moss = Rgba.Hex("#6E7A3C");
        // The mushroom forest: mossy olive earth, pale stems, blue caps, lime glow, pink gills.
        static readonly Ramp Stem = new Ramp(Rgba.Hex("#F2ECD2"), Rgba.Hex("#D8D0B0"), Rgba.Hex("#A8A080"), Rgba.Hex("#6E6A50"));
        static readonly Ramp BlueCap = new Ramp(Rgba.Hex("#8AB4FF"), Rgba.Hex("#4F86E8"), Rgba.Hex("#3460B4"), Rgba.Hex("#223C7A"));
        static readonly Ramp DeepCap = new Ramp(Rgba.Hex("#6E9AF0"), Rgba.Hex("#3E6AC8"), Rgba.Hex("#2A4A96"), Rgba.Hex("#1A2E66"));
        static readonly Ramp PinkCap = new Ramp(Rgba.Hex("#FFC0D8"), Rgba.Hex("#E88AB0"), Rgba.Hex("#B45C84"), Rgba.Hex("#7A3A58"));
        static readonly Ramp LimeCap = new Ramp(Rgba.Hex("#E4FF9A"), Rgba.Hex("#B8F25A"), Rgba.Hex("#7CC03C"), Rgba.Hex("#4A7A28"));
        static readonly Ramp SacSkin = new Ramp(Rgba.Hex("#E8F0A0"), Rgba.Hex("#C8D070"), Rgba.Hex("#8E9A48"), Rgba.Hex("#5A6A2A"));
        static readonly Ramp Bark = new Ramp(Rgba.Hex("#A8906A"), Rgba.Hex("#7A6040"), Rgba.Hex("#54402A"), Rgba.Hex("#34281A"));
        static readonly Ramp Shelf = new Ramp(Rgba.Hex("#FFD49A"), Rgba.Hex("#E8B070"), Rgba.Hex("#A86A3A"), Rgba.Hex("#6E4220"));
        static readonly Rgba Lime = Rgba.Hex("#B8F25A"), LimeCore = Rgba.Hex("#EEFFB8"), Gill = Rgba.Hex("#E88AB0"), ForestMoss = Rgba.Hex("#56682E");

        // How many drawings a prop has (Variant picks one by the tile).
        public const int Variants = 3;

        // Which drawing the prop on a tile is and whether it is mirrored: bits 0-1 the drawing, bit 2 mirrored. The same
        // on every client (a hash of the tile).
        public static int Variant(TileType type, int x, int y)
        {
            uint hash = PixelNoise.Hash(x, y, 9157);
            int variant = (int)(hash % Variants);
            bool flip = Mirrors(type) && (hash >> 8 & 1) == 1;
            return variant | (flip ? 4 : 0);
        }

        // What is near enough symmetric to mirror: plants and stones. Timber, carts and lamps keep their lit side.
        static bool Mirrors(TileType type) => type == TileType.RingCap || type == TileType.Stalagmite || type == TileType.Boulder || type == TileType.PillarCap || type == TileType.GlowCaps
            || type == TileType.SporeSac || type == TileType.ShelfStump || type == TileType.OreSack || type == TileType.FallenLog || IceSandMirrors(type) || CrystalLakeMirrors(type) || LavaVoidMirrors(type);

        // How far above the bottom edge of its footprint a prop's foot line is, in pixels: the rows of shadow under it.
        public const int FootRows = 3;

        // The rows from the top of the picture that are a canopy (fading while someone is behind), 0 for none.
        public static int CanopyRows(TileType type) => type switch
        {
            TileType.PillarCap => 12,
            TileType.GiantCap => 28,
            _ => Math.Max(Math.Max(IceSandCanopy(type), CrystalLakeCanopy(type)), LavaVoidCanopy(type)),
        };

        // The picture of a biome prop, or null for any other tile. shape: Variant's bits.
        public static PixelImage Prop(TileType type, int shape)
        {
            int variant = shape & 3;
            PixelImage image = type switch
            {
                TileType.Stalagmite => Stalagmite(variant),
                TileType.PitProp => PitProp(variant),
                TileType.TippedCart => TippedCart(variant),
                TileType.OreSack => OreSack(variant),
                TileType.UnlitLamp => Lamp(false),
                TileType.LitLamp => Lamp(true),
                TileType.Boulder => Boulder(variant),
                TileType.CaveIn => CaveIn(variant),
                TileType.PillarCap => PillarCap(variant),
                TileType.ShelfStump => ShelfStump(variant),
                TileType.SporeSac => SporeSac(variant),
                TileType.GlowCaps => GlowCaps(variant),
                TileType.FallenLog => FallenLog(variant),
                TileType.GiantCap => GiantCap(),
                _ => IceSandProp(type, variant) ?? CrystalLakeProp(type, variant) ?? LavaVoidProp(type, variant),
            };
            return image != null && (shape & 4) != 0 ? image.FlippedX() : image;
        }

        // A cap of a fairy ring (묶음 B1c, TileType.RingCap): small, pink, a lime glow under it. Walked through, so it
        // stands in the middle of its tile (RingFoot above the tile's bottom edge) and whoever is on the tile is in
        // front of it or behind it by where their feet are. variant: Variant's bits.
        public const int RingFoot = 8;

        public static PixelImage RingCap(int shape)
        {
            int variant = shape & 3;
            var k = new Kit(12, 13);
            if (variant == 0)
            {
                k.Col(5, 6, 2, 7, Stem);
                k.Dome(6f, 8, 5f, 6.5f, PinkCap);
                k.Rect(2, 7, 8, 1, PinkCap.Dark);
                k.Px(Rgba.Hex("#FFF0F6"), 4, 3, 7, 4, 6, 2);
                k.Px(Lime, 4, 8, 7, 8);
            }
            else if (variant == 1)
            {
                // Two, a taller and a small one leaning from its foot.
                k.Col(3, 5, 2, 8, Stem);
                k.Dome(4f, 7, 4f, 5.5f, PinkCap);
                k.Rect(1, 6, 6, 1, PinkCap.Dark);
                k.Col(9, 9, 1, 4, Stem);
                k.Dome(9.5f, 11, 2.5f, 3f, PinkCap);
                k.Px(Rgba.Hex("#FFF0F6"), 3, 3, 5, 4, 9, 9);
                k.Px(Lime, 3, 7);
            }
            else
            {
                // A flat old one, its rim turned up.
                k.Col(5, 7, 2, 6, Stem);
                k.Dome(6f, 8, 6f, 4.5f, PinkCap);
                k.Rect(1, 7, 10, 1, PinkCap.Dark);
                k.Px(PinkCap.Light, 0, 6, 11, 6);
                k.Px(Rgba.Hex("#FFF0F6"), 3, 5, 6, 4, 8, 5);
                k.Px(Lime, 4, 8, 8, 8).Px(LimeCore, 6, 8);
            }
            var stood = Stand(k, 8);
            // Lifted to the middle of its tile.
            var image = new PixelImage(stood.Width, stood.Height + RingFoot - FootRows);
            image.Draw(stood, 0, 0);
            return (shape & 4) != 0 ? image.FlippedX() : image;
        }

        // The place dressing's tall thing by the biome it stands in (Docs/BiomeRework.md 4-3 장소의 건물 3): in the
        // mushroom forest the forest's own great mushroom, anywhere else underground a mine timber with a lantern hung
        // from it. Both the size of the old mushroom tree (32 by 44, the top 26 rows fading), so only the picture changes.
        public static PixelImage PlaceTree(Biome biome) => biome == Biome.Mushroom ? GreatMushroom() : biome == Biome.Ice ? FrozenTree() : biome == Biome.Sand ? CavePalm()
            : biome == Biome.Crystal ? CrystalTree() : biome == Biome.Lake ? LakeWillow() : biome == Biome.Lava ? CharredTree() : biome == Biome.Abyss ? VoidTree() : TimberLamp();

        // ---- The dirt cave ----

        static PixelImage Stalagmite(int variant)
        {
            var k = new Kit(14, 24);
            if (variant == 0)
            {
                k.Spike(7, 24, 4.5f, 6.5f, 0, Earth);
                k.Ln(6, 6, 5, 20, Earth.Light);
            }
            else if (variant == 1)
            {
                // A tall one and its small neighbour.
                k.Spike(5, 24, 3.6f, 5f, 2, Earth);
                k.Spike(10.5f, 24, 2.6f, 11f, 12, Earth);
                k.Ln(4, 8, 3, 20, Earth.Light);
            }
            else
            {
                // Broken off short, its top a flat pale face.
                k.Spike(7, 24, 5f, 7f, 6, Earth);
                k.Rect(5, 9, 5, 2, Earth.Light).Rect(6, 8, 3, 1, Earth.Light.Mix(Rgba.White, 0.25f));
                k.Rect(0, 0, 14, 8, Rgba.Clear);
            }
            // Rings of the stone's growth and a touch of moss at its foot.
            for (int y = 10; y < 22; y += 4) k.Tint(y, 0.86f);
            k.Speck(Moss, 0.25f, 31, 20, 24);
            return Stand(k, 11);
        }

        // Two posts and a beam across them: a pit prop left holding up nothing.
        static PixelImage PitProp(int variant)
        {
            var k = new Kit(16, 27);
            k.Col(1, 4, 3, 23, Timber).Col(12, 4, 3, 23, Timber);
            k.Box(0, 0, 16, 5, Timber);
            k.Rect(0, 0, 16, 1, Timber.Light.Mix(Rgba.White, 0.2f));
            // The beam's end grain and the pegs.
            k.Px(Timber.Dark, 1, 2, 14, 2).Px(Timber.Dark, 2, 6, 13, 6);
            // A brace from post to beam.
            if (variant != 2)
            {
                k.Ln(4, 11, 7, 5, Timber.Shade).Ln(4, 10, 6, 5, Timber.Main);
            }
            if (variant == 1)
            {
                // A cobweb in the other corner.
                var web = new Rgba(220, 216, 204, 150);
                k.Ln(11, 6, 8, 5, web).Ln(11, 9, 9, 6, web).Ln(11, 12, 11, 6, web);
            }
            if (variant == 2)
            {
                // The beam cracked and sagging.
                k.Rect(7, 0, 2, 5, Timber.Dark).Rect(8, 5, 2, 1, Timber.Main).Rect(6, 5, 2, 1, Timber.Shade);
            }
            return Stand(k, 15);
        }

        // A minecart on its side, its load of ore spilled.
        static PixelImage TippedCart(int variant)
        {
            var k = new Kit(20, 14);
            // The tub, tipped toward us: its open mouth dark, rim lit.
            k.Box(3, 3, 13, 9, OldIron);
            k.Rect(4, 5, 11, 5, Rgba.Hex("#22222A"));
            k.Rect(3, 3, 13, 1, OldIron.Light).Rect(3, 11, 13, 1, OldIron.Dark);
            // Rivets and a dent.
            k.Px(OldIron.Light, 4, 4, 14, 4).Px(OldIron.Dark, 9, 11);
            // Its wheels in the air.
            k.Ell(5.5f, 2f, 2.2f, 2f, OldIron).Ell(13.5f, 2f, 2.2f, 2f, OldIron);
            k.Px(Rgba.Hex("#22222A"), 5, 2, 13, 2);
            // The spill.
            k.Ell(5f, 9.5f, 2.2f, 1.6f, CopperOre).Ell(9f, 10.5f, 2f, 1.5f, Rock);
            if (variant != 1) k.Ell(17f, 12f, 2f, 1.5f, CopperOre);
            if (variant == 2) k.Ell(1.5f, 12.4f, 1.5f, 1.2f, Rock);
            return Stand(k, 17);
        }

        static PixelImage OreSack(int variant)
        {
            var k = new Kit(variant == 2 ? 15 : 11, 13);
            k.Ell(5.5f, 8f, 5f, 5f, Sack);
            // Tied at the neck, ore showing at its mouth.
            k.Rect(4, 2, 3, 2, Sack.Shade).Px(Sack.Dark, 4, 3, 5, 3, 6, 3);
            k.Px(CopperOre.Light, 4, 1, 6, 1).Px(CopperOre.Main, 5, 1, 5, 0);
            // A fold and a patch.
            k.Ln(3, 6, 4, 10, Sack.Shade).Px(Sack.Light, 7, 6, 8, 7);
            if (variant == 1) k.Px(CopperOre.Main, 9, 12, 10, 12).Px(CopperOre.Light, 9, 11);
            if (variant == 2)
            {
                // A second, smaller sack slumped against it.
                k.Ell(11.5f, 10f, 3.4f, 3f, Sack);
                k.Px(Sack.Dark, 11, 7, 12, 7).Px(CopperOre.Light, 11, 6);
            }
            return Stand(k, variant == 2 ? 14 : 11);
        }

        // A miner's lamp on a post: out, its glass dark; lit, amber with a pale core and a little halo.
        static PixelImage Lamp(bool lit)
        {
            var k = new Kit(11, 22);
            k.Col(4, 9, 3, 13, Timber);
            k.Rect(3, 20, 5, 2, Timber.Shade);
            // The cage: a cap, the glass, a base.
            k.Rect(2, 1, 7, 1, OldIron.Shade).Rect(4, 0, 3, 1, OldIron.Main);
            k.Rect(2, 2, 7, 6, lit ? LampGlow : LampOut);
            k.Rect(2, 8, 7, 1, OldIron.Dark);
            k.Rect(2, 2, 1, 6, OldIron.Main).Rect(8, 2, 1, 6, OldIron.Shade).Rect(5, 2, 1, 6, OldIron.Shade);
            if (lit)
            {
                k.Rect(3, 3, 2, 4, LampCore).Rect(6, 3, 2, 4, LampGlow.Mix(LampCore, 0.5f));
                k.Px(LampCore, 3, 5, 4, 5, 6, 5);
            }
            else
            {
                // Cold glass with a glint, and the dead wick.
                k.Rect(3, 3, 2, 5, Rgba.Hex("#5E6670")).Rect(6, 3, 2, 5, Rgba.Hex("#4A525C"));
                k.Px(Rgba.Hex("#9AA4AE"), 3, 3).Px(Rgba.Hex("#2A2420"), 4, 6, 7, 6);
            }
            var image = Stand(k, 9);
            if (lit)
            {
                // The halo: a few soft pixels round the glass, over the outline.
                int cx = image.Width / 2;
                foreach (var (dx, dy) in new[] { (-6, 5), (6, 5), (0, -1), (-5, 2), (5, 2), (-5, 8), (5, 8) })
                    image.Blend(cx + dx, 1 + dy, LampGlow.WithAlpha(110));
            }
            return image;
        }

        // A boulder two tiles wide and nearly two tall, standing on its lower row.
        static PixelImage Boulder(int variant)
        {
            var k = new Kit(30, 24);
            k.Ell(15f, 15f, 14.5f, 9.5f, Rock);
            k.Ell(11f, 10f, 10f, 8.5f, Rock);
            if (variant == 1) k.Ell(22f, 12f, 7f, 7f, Rock);
            // Cracks, and moss on its top.
            k.Ln(16, 6, 14, 14, Rock.Dark).Ln(14, 14, 20, 21, Rock.Dark).Ln(6, 15, 14, 14, Rock.Shade);
            if (variant != 0) k.Speck(Moss, 0.35f, 37, 2, 8);
            if (variant == 2) k.Ell(25f, 20f, 3.5f, 3f, Rock);
            return Stand(k, 28);
        }

        // A cave-in: a heap of rock with broken timbers across it.
        static PixelImage CaveIn(int variant)
        {
            var k = new Kit(32, 21);
            k.Ell(10f, 14f, 9.5f, 7f, Rock).Ell(22f, 15f, 9.5f, 6f, Rock).Ell(16f, 11f, 7f, 6f, Rock);
            k.Ln(5, 16, 25, 4, Timber.Main, 3).Ln(5, 15, 25, 3, Timber.Light);
            k.Ln(11, 3, 28, 15, Timber.Shade, 2).Ln(11, 2, 27, 13, Timber.Main);
            // The split ends.
            k.Px(Timber.Dark, 25, 3, 26, 4, 5, 17);
            if (variant == 1) k.Ell(28f, 19f, 3f, 2f, Rock).Ell(3f, 19f, 2.5f, 1.8f, Rock);
            if (variant == 2) k.Ell(16f, 18.5f, 3f, 2f, CopperOre);
            return Stand(k, 30);
        }

        // A mine timber with a cross-beam and a lantern hung from each end: what a dirt cave's village has where a
        // forest would have a tree.
        static PixelImage TimberLamp()
        {
            var k = new Kit(30, 40);
            k.Col(13, 4, 5, 36, Timber);
            k.Box(3, 2, 24, 4, Timber);
            k.Rect(3, 2, 24, 1, Timber.Light.Mix(Rgba.White, 0.2f));
            // Braces up to the beam from both sides, and the post's knots.
            k.Ln(12, 13, 8, 6, Timber.Shade).Ln(12, 12, 9, 6, Timber.Main);
            k.Ln(18, 13, 22, 6, Timber.Dark).Ln(18, 12, 21, 6, Timber.Shade);
            k.Px(Timber.Dark, 14, 17, 16, 25, 14, 31);
            // The chains and the lanterns.
            foreach (int x in new[] { 3, 22 })
            {
                k.Rect(x + 2, 6, 1, 3, OldIron.Shade);
                k.Rect(x, 9, 5, 1, OldIron.Shade).Rect(x, 10, 5, 6, LampGlow).Rect(x, 16, 5, 1, OldIron.Dark);
                k.Rect(x + 1, 11, 2, 4, LampCore).Rect(x, 10, 1, 6, OldIron.Main).Rect(x + 4, 10, 1, 6, OldIron.Shade);
            }
            // Stones heaped at its foot.
            k.Ell(11f, 38f, 3f, 2f, Rock).Ell(20f, 38.4f, 3f, 1.8f, Rock);
            var image = Stand(k, 18, 32, 44);
            foreach (int cx in new[] { 6, 25 })
                foreach (var (dx, dy) in new[] { (-4, 13), (4, 13), (0, 19) }) image.Blend(cx + dx, dy + 3, LampGlow.WithAlpha(100));
            return image;
        }

        // ---- The mushroom forest ----

        // A tall thin stem with a pink cap: the cap is over the tile above and is walked behind.
        static PixelImage PillarCap(int variant)
        {
            int tall = variant == 1 ? 14 : 17;
            var k = new Kit(15, 9 + tall);
            k.Col(6, 7, 3, tall + 2, Stem);
            k.Rect(5, 7 + tall, 5, 2, Stem.Shade);
            k.Dome(7.5f, 9, 7.5f, 9, PinkCap);
            k.Rect(2, 8, 11, 1, PinkCap.Dark);
            // Pale spots that glow a little.
            k.Px(Rgba.Hex("#FFE4F0"), 4, 3, 9, 2, 11, 5, 6, 5);
            if (variant == 2) k.Dome(12f, 6 + tall, 2.5f, 3, PinkCap).Rect(12, 6 + tall, 1, 3, Stem.Main);
            return Stand(k, 9);
        }

        // A rotten stump with shelves of fungus up its side.
        static PixelImage ShelfStump(int variant)
        {
            var k = new Kit(15, 15);
            k.Col(4, 4, 8, 11, Bark);
            k.Ell(8f, 4f, 4f, 2f, new Ramp(Bark.Light.Mix(Rgba.White, 0.2f), Bark.Light, Bark.Main, Bark.Shade));
            k.Px(Bark.Shade, 7, 4, 8, 3, 9, 4);
            k.Px(Bark.Dark, 6, 8, 6, 9, 9, 10, 9, 11);
            // The shelves: lit tops, darker under.
            k.Rect(0, 6, 5, 2, Shelf.Main).Rect(0, 6, 5, 1, Shelf.Light).Rect(1, 8, 3, 1, Shelf.Shade);
            k.Rect(10, 9, 5, 2, Shelf.Main).Rect(10, 9, 5, 1, Shelf.Light).Rect(11, 11, 3, 1, Shelf.Shade);
            if (variant != 0) k.Rect(1, 11, 4, 2, Shelf.Main).Rect(1, 11, 4, 1, Shelf.Light);
            if (variant == 2) k.Px(Lime, 6, 2, 10, 2).Px(LimeCore, 7, 1);
            k.Speck(ForestMoss, 0.3f, 43, 12, 15);
            return Stand(k, 12);
        }

        // A swollen sac of spores, glowing through its skin: struck, it bursts into a cloud.
        static PixelImage SporeSac(int variant)
        {
            var k = new Kit(12, 13);
            k.Ell(6f, 7.5f, 5.5f, 5.5f, SacSkin);
            k.Px(SacSkin.Dark, 5, 1, 6, 1, 6, 0);
            // The glow inside and the seams.
            k.Px(Lime, 3, 5, 8, 7, 5, 10, 4, 8).Px(LimeCore, 4, 6, 7, 9);
            k.Ln(6, 2, 6, 12, SacSkin.Shade);
            if (variant == 1) k.Px(Lime, 9, 4, 2, 9);
            if (variant == 2) k.Ell(10.5f, 11f, 1.6f, 1.6f, SacSkin);
            return Stand(k, 11);
        }

        // A clump of caps that glow lime: picked for glow spores.
        static PixelImage GlowCaps(int variant)
        {
            var k = new Kit(15, 12);
            k.Col(2, 6, 2, 6, Stem).Col(7, 4, 2, 8, Stem).Col(12, 8, 2, 4, Stem);
            k.Dome(3f, 7, 3f, 4, LimeCap).Dome(8f, 5, 4f, 5, LimeCap).Dome(13f, 9, 2.5f, 3, LimeCap);
            k.Px(LimeCore, 7, 1, 2, 4, 12, 7);
            if (variant == 1) k.Col(5, 9, 1, 3, Stem).Dome(5.5f, 10, 1.5f, 2, LimeCap);
            if (variant == 2) k.Rect(0, 0, 15, 3, Rgba.Clear).Dome(8f, 6, 3.5f, 3, LimeCap);
            return Stand(k, 13);
        }

        // A fallen trunk two tiles long, moss and small caps along it, its cut end pale.
        static PixelImage FallenLog(int variant)
        {
            var k = new Kit(31, 13);
            k.Box(1, 3, 27, 10, Bark);
            k.Rect(1, 3, 27, 2, Bark.Light).Rect(1, 5, 27, 1, Bark.Main.Mix(Bark.Light, 0.4f)).Rect(1, 11, 27, 2, Bark.Dark);
            // The grain and the end.
            k.Px(Bark.Shade, 5, 7, 6, 7, 7, 7, 13, 9, 14, 9, 20, 7, 21, 7);
            k.Ell(28f, 8f, 2.8f, 4.8f, new Ramp(Rgba.Hex("#EAD0A0"), Rgba.Hex("#C8A878"), Rgba.Hex("#9A7A50"), Rgba.Hex("#6E5234")));
            k.Px(Rgba.Hex("#9A7A50"), 28, 8, 27, 7);
            k.Speck(ForestMoss, 0.32f, 53, 3, 6);
            // What grows on it.
            k.Rect(6, 1, 4, 2, Shelf.Main).Rect(6, 1, 4, 1, Shelf.Light);
            k.Rect(15, 0, 1, 3, Stem.Main).Rect(14, 0, 3, 1, Lime).Px(LimeCore, 15, 0);
            if (variant != 0) k.Rect(20, 1, 1, 2, Stem.Main).Rect(19, 1, 3, 1, Lime);
            if (variant == 2) k.Rect(2, 1, 3, 2, Shelf.Main).Rect(2, 1, 3, 1, Shelf.Light);
            return Stand(k, 29);
        }

        // The forest's great mushroom: a blue cap two tiles wide over a pale stem, gills pink beneath.
        static PixelImage GreatMushroom()
        {
            var k = new Kit(30, 40);
            k.Col(11, 16, 8, 24, Stem);
            k.Rect(10, 37, 10, 3, Stem.Shade);
            k.Px(Stem.Shade, 13, 24, 15, 30, 13, 33);
            // The underside, then the dome.
            k.Rect(3, 17, 24, 2, Gill.Scale(0.75f)).Rect(6, 19, 18, 1, Gill.Scale(0.55f));
            k.Dome(15f, 18, 15f, 18, BlueCap);
            Spots(k, 30, 16, Rgba.Hex("#CFE4FF"), 61);
            // Moss at its foot.
            k.Speck(ForestMoss, 0.4f, 63, 36, 40);
            return Stand(k, 20, 32, 44);
        }

        // A giant cap three tiles wide whose stem alone blocks: walked under and behind.
        static PixelImage GiantCap()
        {
            var k = new Kit(50, 50);
            k.Col(20, 22, 11, 28, Stem);
            k.Rect(18, 46, 15, 4, Stem.Shade);
            k.Px(Stem.Shade, 23, 30, 26, 36, 23, 41, 27, 44);
            k.Rect(5, 24, 40, 2, Gill.Scale(0.75f)).Rect(9, 26, 32, 1, Gill.Scale(0.55f));
            k.Dome(25f, 25, 25f, 25, DeepCap);
            Spots(k, 50, 23, Rgba.Hex("#CFE4FF"), 67);
            // Lime lights hang under its rim.
            k.Px(Lime, 8, 27, 16, 28, 34, 28, 42, 27).Px(LimeCore, 8, 28, 42, 28);
            k.Speck(ForestMoss, 0.4f, 69, 45, 50);
            return Stand(k, 26);
        }

        static void Spots(Kit k, int capW, int capH, Rgba spot, int salt)
        {
            for (int i = 0; i < capW * capH / 24; i++)
            {
                int x = 2 + (int)(PixelNoise.Value(i, capW, salt) * (capW - 5)), y = 1 + (int)(PixelNoise.Value(i, capH, salt + 1) * (capH - 5));
                if (!k.I.IsSet(x, y) || !k.I.IsSet(x + 1, y) || !k.I.IsSet(x, y - 1)) continue;
                bool big = capW > 24 && i % 2 == 0;
                k.Rect(x, y, big ? 2 : 1, big ? 2 : 1, spot);
                if (big) k.Px(x + 1, y + 1, spot.Scale(0.8f));
            }
        }

        // ---- Standing a body on its shadow ----

        // The finished picture: the body ringed in the outline, its lowest row darkened, standing on a crisp shadow of
        // that width with its foot about FootRows above the picture's bottom edge (a wide thing's deeper shadow takes
        // a row or two more). width/height: the picture's size when it must match another's (0 for the body's own,
        // widened to an even number so its middle is on a pixel edge).
        static PixelImage Stand(Kit k, int shadow, int width = 0, int height = 0)
        {
            var body = k.I;
            // The contact row: the lowest pixel of each column, where it meets the floor, a step darker.
            int lowest = -1;
            for (int y = body.Height - 1; y >= 0 && lowest < 0; y--)
                for (int x = 0; x < body.Width; x++)
                    if (body[x, y].A >= 200) lowest = y;
            if (lowest >= 0)
                for (int x = 0; x < body.Width; x++)
                    if (body[x, lowest].A >= 200) body.Shade(x, lowest, 0.62f);
            var outlined = body.Outlined(Ink);
            // The shadow straight under the foot: the rows it needs below the outline's last row.
            float ry = Math.Max(1.2f, shadow * 0.35f / 2f);
            int drop = Math.Max(FootRows - 1, (int)Math.Ceiling(ry) - 1);
            int w = width > 0 ? width : Math.Max(outlined.Width, shadow + 2);
            if (w % 2 == 1) w++;
            int h = height > 0 ? height : outlined.Height + drop;
            var image = new PixelImage(w, h);
            PropArt.Shade(image, w / 2f, h - drop - 0.5f, shadow);
            image.Draw(outlined, (w - outlined.Width) / 2, h - drop - outlined.Height);
            return image;
        }

        // ---- A sprite from shaded shapes, lit from the upper left ----

        sealed class Kit
        {
            public readonly PixelImage I;

            public Kit(int width, int height)
            {
                I = new PixelImage(width, height);
            }

            static Rgba Tone(Ramp r, float t) => t > 0.42f ? r.Light : t > -0.15f ? r.Main : t > -0.62f ? r.Shade : r.Dark;

            public Kit Ell(float cx, float cy, float rx, float ry, Ramp r)
            {
                for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++)
                    for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy <= 1f && I.Inside(x, y)) I[x, y] = Tone(r, -dx * 0.55f - dy * 0.8f);
                    }
                return this;
            }

            // The upper half of an oval on a flat base: a cap, a mound.
            public Kit Dome(float cx, float by, float rx, float ry, Ramp r)
            {
                for (int y = (int)MathF.Floor(by - ry); y < (int)by; y++)
                    for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - by) / ry;
                        if (dx * dx + dy * dy <= 1f && I.Inside(x, y)) I[x, y] = Tone(r, -dx * 0.5f - dy * 1.7f - 0.85f);
                    }
                return this;
            }

            // A block: its top row lit, right side in shade, foot dark.
            public Kit Box(int x, int y, int w, int h, Ramp r)
            {
                I.Fill(x, y, w, h, r.Main);
                I.Fill(x, y, w, 1, r.Light);
                I.Fill(x + w - 1, y + 1, 1, h - 1, r.Shade);
                I.Fill(x, y + h - 1, w, 1, r.Dark);
                return this;
            }

            // An upright round thing: light on its left, dark on its right.
            public Kit Col(int x, int y, int w, int h, Ramp r)
            {
                for (int i = 0; i < w; i++)
                {
                    var c = i < Math.Max(1, w / 4) ? r.Light : i < w * 0.62f ? r.Main : i < w - 1 || w < 3 ? r.Shade : r.Dark;
                    I.Fill(x + i, y, 1, h, c);
                }
                return this;
            }

            // A shard from a base to a point: light left of its ridge, dark right.
            public Kit Spike(float bx, float by, float half, float ax, float ay, Ramp r)
            {
                int y0 = (int)MathF.Floor(MathF.Min(ay, by)), y1 = (int)MathF.Ceiling(MathF.Max(ay, by));
                for (int y = y0; y < y1; y++)
                {
                    float f = (y + 0.5f - ay) / (by - ay);
                    if (f < 0 || f > 1) continue;
                    float c = ax + (bx - ax) * f, w = MathF.Max(half * f, 0.5f);
                    for (int x = (int)MathF.Floor(c - w); x <= (int)MathF.Ceiling(c + w); x++)
                    {
                        float u = (x + 0.5f - c) / w;
                        if (u < -1 || u > 1 || !I.Inside(x, y)) continue;
                        I[x, y] = u < -0.3f ? r.Light : u < 0.3f ? r.Main : u < 0.8f ? r.Shade : r.Dark;
                    }
                }
                return this;
            }

            public Kit Rect(int x, int y, int w, int h, Rgba c)
            {
                I.Fill(x, y, w, h, c);
                return this;
            }

            public Kit Px(int x, int y, Rgba c)
            {
                if (I.Inside(x, y)) I[x, y] = c;
                return this;
            }

            public Kit Px(Rgba c, params int[] xy)
            {
                for (int i = 0; i + 1 < xy.Length; i += 2)
                    if (I.Inside(xy[i], xy[i + 1])) I[xy[i], xy[i + 1]] = c;
                return this;
            }

            // A line, thick rows deep.
            public Kit Ln(int x0, int y0, int x1, int y1, Rgba c, int thick = 1)
            {
                int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                for (int i = 0; i <= steps; i++)
                {
                    float t = steps == 0 ? 0 : i / (float)steps;
                    int x = (int)MathF.Round(x0 + (x1 - x0) * t), y = (int)MathF.Round(y0 + (y1 - y0) * t);
                    for (int j = 0; j < thick; j++)
                    {
                        if (!I.Inside(x, y + j)) continue;
                        if (c.A >= 250) I[x, y + j] = c;
                        else I.Blend(x, y + j, c);
                    }
                }
                return this;
            }

            // Darkens one row of what is drawn: a band round a stone.
            public Kit Tint(int y, float factor)
            {
                for (int x = 0; x < I.Width; x++)
                    if (I.IsSet(x, y)) I.Shade(x, y, factor);
                return this;
            }

            // Recolours some of the drawn pixels between two rows: moss, lichen.
            public Kit Speck(Rgba c, float density, int salt, int y0 = 0, int y1 = 999)
            {
                for (int y = Math.Max(0, y0); y < Math.Min(I.Height, y1); y++)
                    for (int x = 0; x < I.Width; x++)
                        if (I.IsSet(x, y) && PixelNoise.Value(x, y, salt) < density) I[x, y] = c;
                return this;
            }
        }
    }
}
