using System;
using Game.Core;

namespace Game.Client
{
    // The places' buildings and dressing in the crystal cave's and the underground lake's mood, and those two biomes'
    // landmarks (Docs/BiomeRework.md 묶음 B3a; PropArt.Places.cs has the first two biomes' and how a look is chosen).
    //  - The crystal cave, a village cut from the slate: walls of dressed slate studded with crystal under roofs of
    //    slate shingles that crystals have grown through, lamps that are a caged crystal, pink and sky.
    //  - The lake, a fishing hamlet: huts on stilts with reed thatch and driftwood walls, nets and floats hung to dry,
    //    teal lanterns on poles, tubs of lotus.
    // The same tiles, footprints, picture sizes and roof rows as the old drawings.
    static partial class PropArt
    {
        static readonly Ramp SlateBlock = new Ramp(Rgba.Hex("#A2A8BC"), Rgba.Hex("#747A92"), Rgba.Hex("#525870"), Rgba.Hex("#363A4C"));
        static readonly Ramp SlateTile = new Ramp(Rgba.Hex("#8A93AE"), Rgba.Hex("#5E6682"), Rgba.Hex("#434A62"), Rgba.Hex("#2A2F42"));
        static readonly Ramp PinkGem = new Ramp(Rgba.Hex("#FFD2F0"), Rgba.Hex("#FF7FD0"), Rgba.Hex("#C84CA2"), Rgba.Hex("#7E2C6C"));
        static readonly Ramp SkyGem = new Ramp(Rgba.Hex("#D6FCFF"), Rgba.Hex("#7FF2FF"), Rgba.Hex("#3EAED0"), Rgba.Hex("#1E6890"));
        static readonly Rgba GemGlint = Rgba.Hex("#F4F0FF"), PinkLight = Rgba.Hex("#FF8AD8"), SkyLight = Rgba.Hex("#8AF0FF");

        static readonly Ramp Thatch = new Ramp(Rgba.Hex("#C6C884"), Rgba.Hex("#989E5A"), Rgba.Hex("#6C743E"), Rgba.Hex("#444C2A"));
        static readonly Ramp Driftwood = new Ramp(Rgba.Hex("#B4B4A0"), Rgba.Hex("#86887A"), Rgba.Hex("#5E6056"), Rgba.Hex("#3C3E38"));
        static readonly Ramp WetWood = new Ramp(Rgba.Hex("#9C8866"), Rgba.Hex("#6E5C44"), Rgba.Hex("#4A3C2C"), Rgba.Hex("#2E241A"));
        static readonly Ramp BankStone = new Ramp(Rgba.Hex("#94B0A8"), Rgba.Hex("#64847E"), Rgba.Hex("#445F5A"), Rgba.Hex("#2A403E"));
        static readonly Ramp ReedGreen = new Ramp(Rgba.Hex("#B4D068"), Rgba.Hex("#7A9440"), Rgba.Hex("#54702E"), Rgba.Hex("#364A20"));
        static readonly Rgba TealLight = Rgba.Hex("#70E6D8"), TealCore = Rgba.Hex("#E0FFF8"), NetCord = Rgba.Hex("#D8D2B8"), FloatRed = Rgba.Hex("#E0604A");
        static readonly Rgba Lotus = Rgba.Hex("#F090B4"), LotusPale = Rgba.Hex("#FFD2E4"), PondWater = Rgba.Hex("#1E7A84"), PondLight = Rgba.Hex("#8AE8E0");

        public const int CrystalLook = 5, LakeLook = 6;

        static PixelImage CrystalLakePlaceProp(TileType type, int look)
        {
            bool crystal = look == CrystalLook;
            return type switch
            {
                TileType.Cottage => crystal ? SlateHouse(false) : StiltHut(false),
                TileType.Cabin => crystal ? SlateHouse(true) : StiltHut(true),
                TileType.Well => crystal ? CrystalWell() : ReedWell(),
                TileType.LampPost => crystal ? CrystalLamp() : PoleLantern(),
                TileType.Signpost => crystal ? SlateSign() : DriftSign(),
                TileType.Bench => crystal ? SlateBench() : SkiffBench(),
                TileType.Flowerbed => crystal ? CrystalBed() : LotusTub(),
                TileType.Crates => crystal ? ShardCrates() : FishCrates(),
                _ => null,
            };
        }

        static int CrystalLakeLandmarkCanopy(TileType type) => type switch
        {
            TileType.GiantGeode => 24,
            TileType.CrystalArch => 30,
            TileType.ArchPier => 24,
            TileType.SunkenBoat => 18,
            TileType.OldWheel => 22,
            _ => 0,
        };

        static PixelImage CrystalLakeLandmark(TileType type) => type switch
        {
            TileType.GiantGeode => GreatGeode(),
            TileType.ArchPier => ArchPier(),
            TileType.CrystalArch => ArchSpan(),
            TileType.SunkenBoat => BoatInPool(),
            TileType.OldWheel => WaterWheelRuin(),
            _ => null,
        };

        // ---- Brushes ----

        // A shard of crystal from a base to a point: light left of its ridge, dark right.
        static void Shard(PixelImage image, float bx, float by, float half, float ax, float ay, Ramp ramp)
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
                    if (u < -1 || u > 1 || !image.Inside(x, y)) continue;
                    image[x, y] = u < -0.3f ? ramp.Light : u < 0.3f ? ramp.Main : u < 0.8f ? ramp.Shade : ramp.Dark;
                }
            }
        }

        // A roof of slate shingles from row top to row bottom: courses five rows deep, each slate lit along its lower
        // lip, a few slates of another tone, the right side in shade.
        static void SlateRoof(PixelImage image, int w, int top, int bottom, int salt)
        {
            for (int y = top; y < bottom; y++)
            {
                int inset = Math.Max(0, (14 - (y - top)) / 4);
                int course = (y - top) / 5, row = (y - top) % 5;
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    int along = (x + course % 2 * 4) / 8, edge = (x + course % 2 * 4) % 8;
                    float t = (x - 2f) / (w - 4f);
                    float tone = PixelNoise.Value(along, course, salt);
                    var color = t > 0.84f ? SlateTile.Shade : y < top + 4 ? SlateTile.Light : tone > 0.82f ? SlateTile.Light.Mix(SlateTile.Main, 0.5f) : tone < 0.14f ? SlateTile.Shade : SlateTile.Main;
                    if (row == 4) color = SlateTile.Dark;
                    else if (edge == 0) color = color.Scale(0.78f);
                    else if (row == 3) color = color.Mix(SlateTile.Light, 0.3f);
                    image[x, y] = color;
                }
            }
            Rect(image, 2, bottom - 1, w - 4, 2, SlateTile.Dark);
        }

        // A roof of reed thatch from row top to row bottom: strands running down the slope, darker bands where the
        // bundles are tied, a shaggy lower edge.
        static void ThatchRoof(PixelImage image, int w, int top, int bottom, int salt, bool round)
        {
            for (int x = 2; x < w - 2; x++)
            {
                float t = (x - 2f) / (w - 4f);
                // A rounder roof's ends drop lower at the top.
                int rise = round ? (int)(MathF.Pow(MathF.Abs(t - 0.5f) * 2, 2.2f) * 12) : Math.Max(0, 12 - Math.Min(x - 2, w - 3 - x) * 4);
                int drop = (int)(PixelNoise.Value(x, salt, 171) * 3);
                for (int y = top + rise; y < bottom + drop; y++)
                {
                    float strand = PixelNoise.Value(x, salt, 173);
                    var color = t > 0.84f ? Thatch.Shade : y < top + rise + 3 ? Thatch.Light : strand > 0.72f ? Thatch.Light.Mix(Thatch.Main, 0.5f) : strand < 0.22f ? Thatch.Shade : Thatch.Main;
                    int band = (y - top) % 11;
                    if (band == 9) color = color.Scale(0.74f);
                    else if (band == 10) color = color.Mix(Thatch.Light, 0.25f);
                    if (y >= bottom - 1) color = y >= bottom ? Thatch.Dark : color.Scale(0.82f);
                    image[x, y] = color;
                }
            }
        }

        // ---- The crystal cave: a village cut from the slate ----

        // A slate house, four tiles by three: a roof of slate shingles that crystals have broken through along its
        // ridge, walls of dressed slate blocks studded with small crystals (the cabin's of rough dark stone in a timber
        // frame), a banded door with a caged pink crystal beside it, warm windows, crystals grown at the corner.
        static PixelImage SlateHouse(bool cabin)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = cabin ? 14 : 12, eave = cabin ? 44 : 46;
            // The great crystal through the roof where a chimney would be, and its lesser ones.
            int grown = cabin ? 12 : 46;
            Shard(body, grown + 3, roofTop + 8, 4.2f, grown + (cabin ? 1 : 5), 0, cabin ? PinkGem : SkyGem);
            Shard(body, grown - 3, roofTop + 8, 2.6f, grown - 5, 6, cabin ? SkyGem : PinkGem);
            Shard(body, grown + 8, roofTop + 8, 2.4f, grown + 10, 8, cabin ? PinkGem : SkyGem);
            SlateRoof(body, w, roofTop, eave, cabin ? 9 : 6);
            // Where it broke through: the root of the crystal on the slates, a slate or two lifted.
            Shard(body, grown + 3, roofTop + 12, 4.4f, grown + 3, roofTop + 2, cabin ? PinkGem : SkyGem);
            body[grown + 2, roofTop + 5] = GemGlint;
            // Small crystals along the ridge and one low on the slope.
            foreach (var (x, tall, pink) in cabin ? new[] { (30, 5, false), (40, 4, true), (52, 6, false) } : new[] { (10, 5, true), (22, 4, false), (32, 6, true) })
                Shard(body, x, roofTop + 2, 1.8f, x + (pink ? -1 : 1), roofTop + 2 - tall, pink ? PinkGem : SkyGem);
            Shard(body, cabin ? 44 : 16, roofTop + 24, 2f, cabin ? 45 : 15, roofTop + 17, cabin ? SkyGem : PinkGem);
            int wallTop = eave + 1, wallBottom = 68;
            if (cabin)
            {
                // Rough stone, random courses, in a frame of dark timber.
                for (int y = wallTop; y < wallBottom; y++)
                {
                    int course = (y - wallTop) / 4, row = (y - wallTop) % 4;
                    for (int x = 5; x < w - 5; x++)
                    {
                        int along = (x + course * 3) % 7;
                        float tone = PixelNoise.Value((x + course * 3) / 7, course, 57);
                        var color = row == 3 || along == 0 ? DarkStone.Dark : row == 0 ? DarkStone.Light.Mix(SlateBlock.Main, 0.5f) : tone > 0.6f ? SlateBlock.Shade : DarkStone.Main.Mix(SlateBlock.Shade, 0.5f);
                        if (x > w - 9) color = color.Scale(0.8f);
                        body[x, y] = color;
                    }
                }
                foreach (int post in new[] { 5, w - 8 })
                {
                    Rect(body, post, wallTop, 3, wallBottom - wallTop, DarkWood.Main);
                    Rect(body, post, wallTop, 1, wallBottom - wallTop, DarkWood.Light);
                }
            }
            else
            {
                // Courses of dressed slate, each block lit along its top, the joints dark, a crystal set in some.
                for (int y = wallTop; y < wallBottom; y++)
                {
                    int course = (y - wallTop) / 6, row = (y - wallTop) % 6;
                    for (int x = 5; x < w - 5; x++)
                    {
                        int along = (x - 5 + course % 2 * 5) % 10;
                        var color = row == 5 || along == 9 ? SlateBlock.Dark : row == 0 ? SlateBlock.Light : SlateBlock.Main;
                        if (x > w - 9) color = row == 5 ? SlateBlock.Dark : SlateBlock.Shade;
                        body[x, y] = color;
                    }
                }
            }
            for (int y = wallTop; y < wallTop + 2; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = body[x, y].Scale(y == wallTop ? 0.7f : 0.86f);
            // Crystals studded in the wall.
            foreach (var (x, y, pink) in new[] { (8, 63, true), (20, 50, false), (41, 64, true), (55, 52, false), (57, 62, true), (23, 62, false) })
            {
                var gem = pink ? PinkGem : SkyGem;
                body[x, y] = gem.Main;
                body[x, y - 1] = gem.Light;
                body[x + 1, y] = gem.Shade;
            }
            // The door: dark planks banded with iron under a slate lintel.
            int door = cabin ? 38 : 27;
            Rect(body, door - 2, 50, 14, 2, SlateBlock.Light);
            Rect(body, door - 2, 51, 14, 1, SlateBlock.Shade);
            Rect(body, door, 52, 10, 16, DarkWood.Main);
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 53, 1, 15, DarkWood.Dark);
            Rect(body, door, 56, 10, 1, Iron.Main);
            Rect(body, door, 63, 10, 1, Iron.Main);
            // A sky crystal for its handle.
            body[door + 8, 60] = SkyGem.Main;
            body[door + 8, 59] = GemGlint;
            // The lamp by the door: a pink crystal in an iron cage.
            int lamp = door - 7;
            Rect(body, lamp, 51, 5, 1, Iron.Dark);
            Rect(body, lamp, 53, 4, 1, Iron.Shade);
            Rect(body, lamp, 54, 4, 4, PinkLight);
            Rect(body, lamp + 1, 54, 2, 2, GemGlint);
            body[lamp, 56] = Iron.Main;
            body[lamp + 3, 56] = Iron.Shade;
            Rect(body, lamp, 58, 4, 1, Iron.Dark);
            // Windows, warm inside, in slate frames.
            foreach (int wx in cabin ? new[] { 13 } : new[] { 10, 45 })
            {
                int ww = cabin ? 12 : 9;
                Rect(body, wx, 51, ww, 8, SlateBlock.Dark);
                Rect(body, wx + 1, 52, ww - 2, 6, WindowLight);
                Rect(body, wx + 1, 52, 3, 2, WindowCore);
                Rect(body, wx + ww / 2, 52, 1, 6, SlateBlock.Shade);
                Rect(body, wx + 1, 55, ww - 2, 1, SlateBlock.Shade);
                Rect(body, wx - 1, 59, ww + 2, 1, SlateBlock.Light);
            }
            // The footing, and crystals grown against it.
            Rect(body, 4, 68, w - 8, 3, DarkStone.Shade);
            for (int x = 5; x < w - 6; x += 6) Rect(body, x, 68, 5, 1, DarkStone.Main);
            int corner = cabin ? 52 : 6;
            Shard(body, corner + 3, 71, 2.6f, corner + 2, 58, PinkGem);
            Shard(body, corner + 7, 71, 2.2f, corner + 9, 62, SkyGem);
            Shard(body, corner, 71, 1.8f, corner - 2, 65, SkyGem);
            body[corner + 2, 62] = GemGlint;
            Shard(body, cabin ? 8 : 56, 71, 2f, cabin ? 7 : 57, 64, cabin ? SkyGem : PinkGem);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The village's well: a ring of slate, the water in it lit sky blue from below, two crystals for posts with a
        // slate across them, a bucket on its chain.
        static PixelImage CrystalWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, SlateBlock);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, SlateBlock.Shade);
            Rect(body, 2, 26, 16, 1, SlateBlock.Shade);
            LitOval(body, 10, 22, 8, 3, SlateBlock);
            Oval(body, 10, 22, 6, 1.6f, SkyGem.Shade);
            body[7, 22] = GemGlint;
            body[8, 21] = SkyGem.Main;
            body[12, 22] = SkyGem.Main;
            // The posts: a pink shard and a sky one, and the slab they carry.
            Shard(body, 4, 22, 1.8f, 3.5f, 6, PinkGem);
            Shard(body, 16, 22, 1.8f, 16.5f, 6, SkyGem);
            Box(body, 1, 6, 18, 2, 2, SlateTile);
            Shard(body, 10, 6, 2.2f, 10, 1, SkyGem);
            body[10, 3] = GemGlint;
            // The chain and the bucket.
            for (int y = 10; y < 17; y++) body[10, y] = y % 2 == 0 ? Iron.Light : Iron.Shade;
            Box(body, 8, 16, 5, 1, 4, Iron);
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lamp of the village: a post of slate, and on it an iron cage holding a pink crystal that glows.
        static PixelImage CrystalLamp()
        {
            var body = new PixelImage(T, 42);
            Column(body, 6, 14, 4, 24, SlateBlock);
            for (int y = 18; y < 38; y += 6) Rect(body, 6, y, 4, 1, SlateBlock.Dark);
            // The cage and the crystal in it.
            Rect(body, 4, 13, 8, 1, Iron.Dark);
            Shard(body, 8, 13, 2.6f, 8, 3, PinkGem);
            Rect(body, 7, 6, 2, 5, PinkLight);
            body[7, 6] = GemGlint;
            body[7, 8] = GemGlint;
            foreach (int x in new[] { 4, 11 }) Rect(body, x, 4, 1, 9, Iron.Shade);
            Rect(body, 4, 3, 8, 1, Iron.Main);
            Rect(body, 7, 1, 2, 2, Iron.Shade);
            // Its foot, a small sky crystal grown by it.
            Rect(body, 4, 38, 8, 2, SlateBlock.Shade);
            Rect(body, 4, 38, 8, 1, SlateBlock.Main);
            Shard(body, 12, 40, 1.6f, 13, 35, SkyGem);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the village: a slab of slate on end, an arrow of sky crystal set in it.
        static PixelImage SlateSign()
        {
            var body = new PixelImage(T, 26);
            for (int y = 5; y < 24; y++)
            {
                int inset = y < 8 ? 8 - y : 0;
                for (int x = 3 + inset; x < 13; x++) body[x, y] = x < 5 + inset ? SlateBlock.Light : x > 10 ? SlateBlock.Shade : SlateBlock.Main;
            }
            Rect(body, 3, 23, 10, 1, SlateBlock.Dark);
            // The arrow.
            Rect(body, 5, 13, 5, 1, SkyGem.Main);
            body[9, 12] = SkyGem.Light;
            body[9, 14] = SkyGem.Shade;
            body[10, 13] = GemGlint;
            Rect(body, 5, 17, 4, 1, SlateBlock.Dark);
            Rect(body, 5, 19, 3, 1, SlateBlock.Dark);
            Shard(body, 2, 24, 1.6f, 1, 19, PinkGem);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 5, 1.5f));
        }

        // A bench: a slab of slate across two stubs of crystal.
        static PixelImage SlateBench()
        {
            var body = new PixelImage(T, 18);
            Rect(body, 2, 10, 3, 6, PinkGem.Shade);
            Rect(body, 2, 10, 1, 6, PinkGem.Main);
            Rect(body, 11, 10, 3, 6, SkyGem.Shade);
            Rect(body, 11, 10, 1, 6, SkyGem.Main);
            Box(body, 0, 6, 16, 3, 2, SlateBlock);
            Rect(body, 1, 7, 5, 1, SlateBlock.Light.Mix(Rgba.White, 0.25f));
            body[3, 12] = GemGlint;
            body[12, 13] = GemGlint;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A bed of crystal: small shards grown in a trough of slate, as another village would grow flowers.
        static PixelImage CrystalBed()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 10, 14, 2, 5, SlateBlock);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 12, 1, 5, SlateBlock.Shade);
            foreach (var (x, top, pink) in new[] { (3f, 5, true), (6f, 3, false), (9f, 6, true), (12f, 2, false), (13.5f, 7, true) })
                Shard(body, x, 11, 1.6f, x, top, pink ? PinkGem : SkyGem);
            body[6, 5] = GemGlint;
            body[12, 4] = GemGlint;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Stores: a crate banded with iron, a smaller one on it, and a basket of shards.
        static PixelImage ShardCrates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, DarkWood);
            Rect(body, 1, 16, 14, 1, DarkWood.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, DarkWood.Shade);
            Rect(body, 1, 20, 14, 1, Iron.Main);
            Box(body, 2, 5, 9, 3, 5, SlateBlock);
            Rect(body, 6, 8, 1, 4, SlateBlock.Dark);
            // The shards heaped in the open top crate.
            Shard(body, 4, 6, 1.4f, 3.5f, 2, PinkGem);
            Shard(body, 7, 6, 1.6f, 7.5f, 0, SkyGem);
            Shard(body, 9.5f, 6, 1.2f, 10, 3, PinkGem);
            body[7, 2] = GemGlint;
            Shard(body, 13.5f, 24, 1.8f, 14, 18, SkyGem);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The lake: a fishing hamlet ----

        // A hut on stilts, four tiles by three: reed thatch over walls of driftwood boards, the floor raised on posts
        // above the damp with steps up to the door, a net with its floats hung to dry (the cabin: fish on a line and a
        // rounder roof), a teal lantern by the door.
        static PixelImage StiltHut(bool round)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = round ? 14 : 12, eave = round ? 44 : 46;
            ThatchRoof(body, w, roofTop, eave, round ? 8 : 5, round);
            // The ridge: a roll of reed bound with cord.
            Rect(body, round ? 14 : 6, roofTop, w - (round ? 28 : 12), 3, Thatch.Light);
            Rect(body, round ? 14 : 6, roofTop + 2, w - (round ? 28 : 12), 1, Thatch.Shade);
            for (int x = round ? 18 : 10; x < w - (round ? 16 : 8); x += 8) Rect(body, x, roofTop, 1, 3, WetWood.Shade);
            int wallTop = eave + 1, floor = 63;
            // Boards on end, weathered grey, a darker one here and there.
            for (int x = 6; x < w - 6; x++)
            {
                int board = (x - 6) / 4;
                float tone = PixelNoise.Value(board, round ? 3 : 1, 181);
                var color = (x - 6) % 4 == 3 ? Driftwood.Dark : tone > 0.75f ? Driftwood.Light.Mix(Driftwood.Main, 0.5f) : tone < 0.2f ? Driftwood.Shade : Driftwood.Main;
                if (x > w - 10) color = (x - 6) % 4 == 3 ? Driftwood.Dark : Driftwood.Shade;
                for (int y = wallTop; y < floor; y++) body[x, y] = y - wallTop < 2 ? color.Scale(y == wallTop ? 0.7f : 0.86f) : color;
            }
            // The floor beam and the stilts under it, dark between them.
            Rect(body, 4, floor, w - 8, 2, WetWood.Main);
            Rect(body, 4, floor, w - 8, 1, WetWood.Light);
            Rect(body, w - 8, floor, 4, 2, WetWood.Shade);
            Rect(body, 6, floor + 2, w - 12, 7, Rgba.Hex("#141E1E"));
            foreach (int post in new[] { 5, 20, 41, w - 8 })
            {
                Rect(body, post, floor + 2, 3, 8, WetWood.Main);
                Rect(body, post, floor + 2, 1, 8, WetWood.Light);
                Rect(body, post + 2, floor + 2, 1, 8, WetWood.Dark);
                // Moss at the waterline of each.
                body[post, floor + 8] = ReedGreen.Main;
                body[post + 1, floor + 9] = ReedGreen.Shade;
            }
            Line(body, 8, floor + 3, 19, floor + 8, WetWood.Shade);
            Line(body, 44, floor + 8, 55, floor + 3, WetWood.Shade);
            // The door, and the steps up to it.
            int door = round ? 38 : 27;
            Rect(body, door - 1, 49, 12, 1, WetWood.Light);
            Rect(body, door, 50, 10, 13, WetWood.Shade);
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 51, 1, 12, WetWood.Dark);
            body[door + 8, 57] = NetCord;
            for (int step = 0; step < 3; step++)
            {
                Rect(body, door - 1 - step, floor + 2 + step * 3, 12 + step * 2, 1, WetWood.Light);
                Rect(body, door - 1 - step, floor + 3 + step * 3, 12 + step * 2, 2, WetWood.Main);
            }
            // The lantern by the door, teal.
            int lamp = door - 7;
            Rect(body, lamp, 49, 5, 1, WetWood.Dark);
            body[lamp + 1, 50] = WetWood.Dark;
            Rect(body, lamp, 51, 4, 1, Iron.Shade);
            Rect(body, lamp, 52, 4, 4, TealLight);
            Rect(body, lamp + 1, 52, 2, 2, TealCore);
            Rect(body, lamp, 56, 4, 1, Iron.Dark);
            // A window.
            int wx = round ? 13 : 45;
            int ww = round ? 12 : 9;
            Rect(body, wx, 50, ww, 8, WetWood.Dark);
            Rect(body, wx + 1, 51, ww - 2, 6, WindowLight);
            Rect(body, wx + 1, 51, 3, 2, WindowCore);
            Rect(body, wx + ww / 2, 51, 1, 6, WetWood.Shade);
            Rect(body, wx - 1, 58, ww + 2, 1, WetWood.Light);
            if (round)
            {
                // Fish drying on a line under the eave, a barrel by the steps.
                Rect(body, 50, 48, 9, 1, NetCord);
                foreach (int fx in new[] { 51, 54, 57 })
                {
                    Rect(body, fx, 49, 2, 4, Rgba.Hex("#B8C4C8"));
                    body[fx, 49] = Rgba.Hex("#E4ECEE");
                    body[fx, 53] = Rgba.Hex("#8A989E");
                    body[fx + 1, 53] = Rgba.Hex("#8A989E");
                }
                Column(body, 52, 63, 7, 9, WetWood);
                Rect(body, 52, 65, 7, 1, Iron.Shade);
                Rect(body, 52, 69, 7, 1, Iron.Shade);
                Rect(body, 53, 62, 5, 1, WetWood.Light);
            }
            else
            {
                // A net hung on the wall to dry: a slack mesh from two pegs, floats along its top.
                for (int x = 8; x < 23; x++)
                {
                    int sag = (int)(3.5f * (1 - MathF.Pow((x - 15.5f) / 7.5f, 2)));
                    for (int y = 50; y < 57 + sag; y++)
                        if ((x + y) % 3 == 0 || (x - y + 60) % 3 == 0) body[x, y] = NetCord.Mix(Driftwood.Main, (x + y) % 2 == 0 ? 0.15f : 0.45f);
                    body[x, 50 + (x == 8 || x == 22 ? 0 : 1)] = NetCord;
                }
                foreach (int fx in new[] { 10, 15, 20 })
                {
                    body[fx, 50] = FloatRed;
                    body[fx, 51] = FloatRed.Scale(0.75f);
                    body[fx + 1, 50] = Rgba.Hex("#F4EEDC");
                }
                // An oar leant by the corner.
                Line(body, 58, 71, 60, 52, WetWood.Light);
                Rect(body, 59, 48, 3, 5, WetWood.Main);
                Rect(body, 59, 48, 1, 5, WetWood.Light);
            }
            // Reeds grown up by a stilt.
            foreach (var (x, top) in new[] { (2, 60), (4, 56), (61, 58) })
            {
                Line(body, x, 72, x, top, ReedGreen.Main);
                body[x, top] = WetWood.Main;
                body[x, top + 1] = WetWood.Shade;
            }
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The hamlet's well: a ring of bank stone, mossed, under a little roof of reed on two poles, a pail on its rope.
        static PixelImage ReedWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, BankStone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, BankStone.Shade);
            Rect(body, 2, 26, 16, 1, BankStone.Shade);
            LitOval(body, 10, 22, 8, 3, BankStone);
            Oval(body, 10, 22, 6, 1.6f, PondWater);
            body[7, 22] = PondLight;
            body[8, 21] = PondLight.Scale(0.8f);
            foreach (var (x, y) in new[] { (3, 25), (4, 28), (14, 24), (15, 29), (9, 30) }) body[x, y] = ReedGreen.Main;
            // The poles and the reed roof.
            Column(body, 3, 9, 2, 14, WetWood);
            Column(body, 15, 9, 2, 14, WetWood);
            for (int x = 0; x < 20; x++)
            {
                int top = 3 + Math.Abs(x - 10) * 4 / 10;
                int bottom = 10 + (int)(PixelNoise.Value(x, 4, 191) * 2);
                for (int y = top; y < bottom; y++) body[x, y] = x > 15 ? Thatch.Shade : y == top ? Thatch.Light : (x + y) % 5 == 0 ? Thatch.Shade : Thatch.Main;
            }
            Rect(body, 5, 2, 10, 2, Thatch.Light);
            Rect(body, 5, 3, 10, 1, Thatch.Shade);
            // The rope and the pail.
            Rect(body, 10, 11, 1, 6, NetCord.Scale(0.8f));
            Box(body, 8, 16, 5, 1, 4, WetWood);
            Rect(body, 8, 18, 5, 1, Iron.Shade);
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lantern on a pole: a driftwood pole leaning a little, a crook at its top, a teal lantern hung from it.
        static PixelImage PoleLantern()
        {
            var body = new PixelImage(T, 42);
            for (int y = 4; y < 40; y++)
            {
                int x = y < 16 ? 9 : 8;
                body[x, y] = WetWood.Main;
                body[x + 1, y] = WetWood.Shade;
                if (y % 7 == 3) body[x, y] = WetWood.Light;
            }
            // The crook, the cord, the lantern.
            Rect(body, 4, 3, 7, 1, WetWood.Main);
            Rect(body, 4, 4, 6, 1, WetWood.Shade);
            body[5, 5] = NetCord;
            Rect(body, 3, 6, 5, 1, Iron.Shade);
            Rect(body, 3, 7, 5, 5, TealLight);
            Rect(body, 4, 8, 2, 2, TealCore);
            Rect(body, 3, 7, 1, 5, Iron.Main);
            Rect(body, 7, 7, 1, 5, Iron.Shade);
            Rect(body, 3, 12, 5, 1, Iron.Dark);
            // A float tied on, and reeds at its foot.
            body[10, 20] = NetCord;
            body[11, 21] = FloatRed;
            body[11, 22] = FloatRed.Scale(0.75f);
            Heap(body, 8.5f, 40, 4.5f, 2.6f, BankStone);
            Line(body, 5, 39, 4, 32, ReedGreen.Main);
            Line(body, 12, 39, 13, 34, ReedGreen.Light);
            body[4, 32] = WetWood.Main;
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the hamlet: a board of driftwood on a post, a fish cut into it.
        static PixelImage DriftSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 7, 9, 3, 15, WetWood);
            for (int x = 1; x < 15; x++)
            {
                int top = 4 + (x > 12 ? x - 12 : 0) + (x < 2 ? 1 : 0), bottom = 11 - (x > 12 ? x - 12 : 0);
                for (int y = top; y < bottom; y++) body[x, y] = y == top ? Driftwood.Light : x > 11 ? Driftwood.Shade : Driftwood.Main;
            }
            // The fish: a body, a tail, an eye.
            Rect(body, 5, 7, 5, 2, Driftwood.Dark);
            body[4, 7] = Driftwood.Dark;
            body[10, 7] = Driftwood.Dark;
            body[3, 6] = Driftwood.Dark;
            body[3, 8] = Driftwood.Dark;
            body[9, 7] = TealLight;
            body[2, 12] = FloatRed;
            body[2, 11] = NetCord;
            Line(body, 12, 24, 13, 19, ReedGreen.Main);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        // A bench: a skiff's bow turned keel up, sat on.
        static PixelImage SkiffBench()
        {
            var body = new PixelImage(T, 18);
            for (int x = 0; x < 16; x++)
            {
                float t = (x - 7.5f) / 8f;
                int top = 6 + (int)(t * t * 5);
                for (int y = top; y < 16; y++)
                    body[x, y] = y == top ? WetWood.Light : (y - top) % 3 == 2 ? WetWood.Shade : x > 12 ? WetWood.Shade : WetWood.Main;
            }
            // The keel along its back and the damp on it.
            Rect(body, 2, 5, 12, 1, WetWood.Light);
            Rect(body, 2, 6, 12, 1, WetWood.Dark);
            body[4, 9] = ReedGreen.Main;
            body[5, 10] = ReedGreen.Shade;
            body[11, 12] = ReedGreen.Main;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A tub of lotus: a half barrel of water, pads floating in it and a flower open.
        static PixelImage LotusTub()
        {
            var body = new PixelImage(T, 18);
            Column(body, 1, 9, 14, 8, WetWood);
            Rect(body, 1, 11, 14, 1, Iron.Shade);
            Rect(body, 1, 15, 14, 1, Iron.Dark);
            LitOval(body, 8, 9, 7, 2.6f, WetWood);
            Oval(body, 8, 9, 6, 1.8f, PondWater);
            // Pads and a lotus standing over them.
            Rect(body, 3, 9, 3, 1, ReedGreen.Main);
            Rect(body, 10, 8, 3, 1, ReedGreen.Light);
            body[11, 9] = ReedGreen.Shade;
            Rect(body, 7, 5, 1, 4, ReedGreen.Shade);
            Rect(body, 6, 3, 3, 2, Lotus);
            body[7, 2] = LotusPale;
            body[6, 3] = LotusPale;
            body[8, 4] = Lotus.Scale(0.8f);
            body[7, 4] = Rgba.Hex("#FFE07A");
            body[12, 6] = Lotus;
            body[12, 7] = ReedGreen.Shade;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Stores: a crate of the day's fish, a creel on it, a coil of net and a float.
        static PixelImage FishCrates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, Driftwood);
            Rect(body, 1, 16, 14, 1, Driftwood.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, Driftwood.Shade);
            // The fish laid in the open crate.
            foreach (int fx in new[] { 2, 6, 10 })
            {
                Rect(body, fx, 13, 3, 2, Rgba.Hex("#B8C4C8"));
                body[fx, 13] = Rgba.Hex("#E4ECEE");
                body[fx + 3, 14] = Rgba.Hex("#8A989E");
            }
            // The creel: a woven basket with a lid.
            Column(body, 3, 5, 8, 7, Straw);
            for (int y = 6; y < 12; y += 2) Rect(body, 3, y, 8, 1, Straw.Shade);
            Rect(body, 2, 4, 10, 1, Straw.Light);
            body[7, 3] = WetWood.Main;
            // The net and its float.
            for (int y = 20; y < 25; y++)
                for (int x = 9; x < 15; x++)
                    if ((x + y) % 2 == 0) body[x, y] = NetCord.Mix(Driftwood.Main, 0.3f);
            body[13, 19] = FloatRed;
            body[14, 19] = Rgba.Hex("#F4EEDC");
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The landmarks ----

        // A giant geode, three tiles by two and taller than a house's wall: a rough shell of slate split open toward us,
        // its hollow lined with crystal, pink at one side and sky at the other, lit from within; rubble where the shell
        // fell away, crystals grown out through its crown.
        static PixelImage GreatGeode()
        {
            const int w = 48, h = 54;
            var body = new PixelImage(w, h);
            // Crystals through the crown, behind the shell.
            Shard(body, 14, 16, 3.4f, 10, 1, PinkGem);
            Shard(body, 30, 14, 3f, 34, 0, SkyGem);
            Shard(body, 22, 12, 2.4f, 22, 3, SkyGem);
            // The shell.
            LitOval(body, 24, 30, 22, 21, SlateBlock);
            for (int i = 0; i < 26; i++)
            {
                int x = 4 + (int)(PixelNoise.Value(i, 1, 201) * 40), y = 11 + (int)(PixelNoise.Value(i, 2, 201) * 20);
                if (body[x, y].A > 0 && body[x + 1, y + 1].A > 0)
                {
                    body[x, y] = SlateBlock.Light.Mix(SlateBlock.Main, 0.4f);
                    body[x + 1, y + 1] = SlateBlock.Dark;
                }
            }
            // The split: the hollow, wide at the foot, and the bright band of the shell's cut edge round it.
            for (int y = 16; y < 50; y++)
            {
                float t = (y - 16) / 34f;
                float half = 3 + 13 * MathF.Sin(t * MathF.PI * 0.62f);
                float mid = 25 + 2 * MathF.Sin(t * 5);
                for (int x = (int)(mid - half - 2); x <= (int)(mid + half + 2); x++)
                {
                    if (body[x, y].A == 0) continue;
                    float d = MathF.Abs(x + 0.5f - mid);
                    body[x, y] = d > half ? (x < mid ? Rgba.Hex("#D8DCEA") : SlateBlock.Light) : Rgba.Hex("#141826");
                }
            }
            // The lining: shards pointing in from both sides and up from the floor of the hollow.
            foreach (var (bx, by, ax, ay, pink) in new[] { (13f, 30f, 19f, 27f, true), (12f, 37f, 19f, 35f, true), (14f, 44f, 20f, 40f, true), (17f, 24f, 21f, 23f, true),
                (37f, 30f, 31f, 28f, false), (38f, 38f, 31f, 35f, false), (36f, 44f, 30f, 41f, false), (33f, 23f, 29f, 23f, false) })
            {
                var gem = pink ? PinkGem : SkyGem;
                Line(body, (int)bx, (int)by, (int)ax, (int)ay, gem.Main);
                Line(body, (int)bx, (int)by + 1, (int)ax, (int)ay + 1, gem.Shade);
                Line(body, (int)bx, (int)by - 1, (int)ax, (int)ay, gem.Light);
            }
            Shard(body, 21, 50, 2.8f, 20, 38, PinkGem);
            Shard(body, 26, 50, 3.2f, 26, 33, SkyGem);
            Shard(body, 31, 50, 2.4f, 32, 41, PinkGem);
            foreach (var (x, y) in new[] { (26, 36), (20, 41), (25, 42), (18, 28), (32, 29) }) body[x, y] = GemGlint;
            // Light held in the hollow: a soft wash over its dark.
            for (int y = 18; y < 48; y++)
                for (int x = 12; x < 38; x++)
                    if (Same(body[x, y], Rgba.Hex("#141826"))) body[x, y] = body[x, y].Mix(x < 25 ? PinkLight : SkyLight, 0.12f + 0.1f * (y - 18) / 30f);
            // Cracks from the split, and the rubble at its foot.
            Line(body, 24, 16, 22, 10, SlateBlock.Dark);
            Line(body, 22, 10, 24, 6, SlateBlock.Dark);
            Line(body, 38, 26, 43, 22, SlateBlock.Dark);
            Line(body, 10, 34, 5, 38, SlateBlock.Dark);
            LitOval(body, 7, 48, 5, 3.4f, SlateBlock);
            LitOval(body, 41, 49, 4.5f, 3, SlateBlock);
            LitOval(body, 13, 50.5f, 3, 2, SlateBlock);
            body[7, 46] = PinkGem.Main;
            body[41, 47] = SkyGem.Main;
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // A pier of the crystal arch: a thick six-sided column of crystal on a foot of slate, sky blue low down and
        // paling upward.
        static PixelImage ArchPier()
        {
            const int w = T, h = 46;
            var body = new PixelImage(w, h);
            for (int y = 4; y < 42; y++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var color = i < 2 ? SkyGem.Light : i < 6 ? SkyGem.Main : i < 9 ? SkyGem.Shade : SkyGem.Dark;
                    body[3 + i, y] = y < 12 ? color.Mix(GemGlint, 0.25f) : color;
                }
            }
            // Its facets' edges and flaws.
            Rect(body, 5, 5, 1, 36, SkyGem.Light.Mix(GemGlint, 0.5f));
            Rect(body, 9, 5, 1, 36, SkyGem.Shade.Scale(0.9f));
            Line(body, 6, 18, 8, 24, SkyGem.Dark);
            Line(body, 7, 30, 5, 35, SkyGem.Dark);
            foreach (var (x, y) in new[] { (5, 9), (5, 22), (6, 33) }) body[x, y] = GemGlint;
            // The foot: slate broken round it, a pink shard grown beside.
            Box(body, 1, 40, 14, 2, 3, SlateBlock);
            Shard(body, 13.5f, 43, 1.8f, 14.5f, 35, PinkGem);
            Shard(body, 2, 43, 1.6f, 1, 37, PinkGem);
            return Finish(body, ShadowLayer(w, h, 9, h - 3, 7, 2));
        }

        // The span of the crystal arch, five tiles wide, on the tile half way between its piers (walked under): a low
        // bow of crystal from pier to pier, pink at one end and sky at the other with a pale keystone, small shards
        // hanging under it. Only the span: the lower rows of the picture are empty.
        static PixelImage ArchSpan()
        {
            const int w = 80, h = 48;
            var body = new PixelImage(w, h);
            for (int x = 5; x < w - 5; x++)
            {
                float t = (x - 39.5f) / 34.5f;
                int top = 3 + (int)MathF.Round(13 * t * t);
                int thick = 7 + (int)MathF.Round(3 * t * t);
                // Pink from the left pier, sky from the right, pale where they meet.
                float mix = Math.Clamp((x - 30) / 20f, 0, 1);
                bool key = x >= 36 && x < 44;
                int block = (x + 2) / 6;
                for (int j = 0; j < thick; j++)
                {
                    float d = j / (float)(thick - 1);
                    var pink = d < 0.2f ? PinkGem.Light : d < 0.55f ? PinkGem.Main : d < 0.85f ? PinkGem.Shade : PinkGem.Dark;
                    var sky = d < 0.2f ? SkyGem.Light : d < 0.55f ? SkyGem.Main : d < 0.85f ? SkyGem.Shade : SkyGem.Dark;
                    // Each block is one colour or the other: more sky ones toward the right.
                    var color = PixelNoise.Value(block, 7, 211) < mix ? sky : pink;
                    if (key) color = color.Mix(GemGlint, 0.55f);
                    if ((x + 2) % 6 == 0 && j > 0) color = color.Scale(0.72f);
                    body[x, top + j] = color;
                }
            }
            // Shards hanging under it, and glints along its back.
            foreach (var (x, len, pink) in new[] { (16, 5, true), (24, 7, true), (33, 4, false), (41, 8, true), (48, 5, false), (57, 7, false), (65, 4, false) })
            {
                float t = (x - 39.5f) / 34.5f;
                int under = 3 + (int)MathF.Round(13 * t * t) + 7 + (int)MathF.Round(3 * t * t);
                Shard(body, x, under - 1, 1.8f, x, under + len, pink ? PinkGem : SkyGem);
            }
            foreach (var (x, y) in new[] { (12, 13), (22, 7), (39, 4), (40, 4), (56, 6), (68, 13) }) body[x, y] = GemGlint;
            return Finish(body);
        }

        // A boat sunk in a pool, three tiles by two of its water: the hull down by the stern with its bow lifted clear,
        // the gunwale's strakes sprung, a broken mast leaning with a rag of sail on its yard, moss on everything; the
        // part under the water seen dimly through it, a pale line where the pool laps the planks.
        static PixelImage BoatInPool()
        {
            const int w = 48, h = 44;
            var body = new PixelImage(w, h);
            var under = new PixelImage(w, h);
            const int water = 34;
            // The mast behind the hull, leaning back to the left, its yard and the sail's rag.
            for (int y = 2; y < 26; y++)
            {
                int x = 24 - (26 - y) / 3;
                body[x, y] = WetWood.Main;
                body[x + 1, y] = WetWood.Shade;
            }
            Line(body, 10, 7, 24, 4, WetWood.Main);
            Line(body, 10, 8, 24, 5, WetWood.Dark);
            for (int x = 11; x < 21; x++)
            {
                int top = 8 - (x - 11) / 5, length = 5 + (int)(PixelNoise.Value(x, 1, 221) * 7);
                for (int y = top; y < top + length; y++) body[x, y] = x > 18 ? Cream.Shade : (x + y) % 6 == 0 ? Cream.Shade : y == top ? Cream.Light : Cream.Main;
            }
            // The hull: its sheer rising to the bow at the right, strakes along it, the far gunwale showing behind.
            for (int x = 3; x < 45; x++)
            {
                float t = (x - 3) / 42f;
                int sheer = 30 - (int)MathF.Round(13 * t * t) - (int)MathF.Round(3 * t);
                int keel = 39 + (int)MathF.Round(3 * MathF.Sin(t * MathF.PI)) - (x > 40 ? (x - 40) * 2 : 0);
                // The inside of the far side, dark, above the near gunwale.
                if (x > 8 && x < 42) for (int y = sheer - 3; y < sheer; y++) body[x, y] = y == sheer - 3 ? WetWood.Shade : Rgba.Hex("#1C1814");
                for (int y = sheer; y < keel; y++)
                {
                    int strake = (y - sheer) % 4;
                    var color = y == sheer ? WetWood.Light : strake == 3 ? WetWood.Dark : t > 0.86f ? WetWood.Shade : strake == 0 ? WetWood.Main.Mix(WetWood.Light, 0.35f) : WetWood.Main;
                    if (y < water) body[x, y] = color;
                    else under[x, y] = color.Mix(PondWater, 0.62f).WithAlpha(170);
                }
            }
            // The stem post at the bow, a ring on it; a strake sprung loose amidships; ribs showing where planks are gone.
            for (int y = 9; y < 22; y++)
            {
                body[43 + (y < 13 ? 1 : 0), y] = WetWood.Main;
                body[44 + (y < 13 ? 1 : 0), y] = WetWood.Dark;
            }
            body[44, 8] = WetWood.Light;
            body[42, 16] = Iron.Light;
            Line(body, 18, 28, 30, 22, WetWood.Light);
            Line(body, 18, 29, 30, 23, WetWood.Dark);
            foreach (int x in new[] { 14, 20, 26 })
            {
                Rect(body, x, 30, 3, 3, Rgba.Hex("#1C1814"));
                Rect(body, x + 1, 29, 1, 5, WetWood.Shade);
            }
            // Moss on the gunwale and the mast, a shell or two.
            for (int x = 5; x < 44; x++)
                if (PixelNoise.Value(x, 2, 223) < 0.4f)
                {
                    float t = (x - 3) / 42f;
                    int sheer = 30 - (int)MathF.Round(13 * t * t) - (int)MathF.Round(3 * t);
                    if (sheer < water) body[x, sheer] = PixelNoise.Value(x, 3, 223) < 0.5f ? ReedGreen.Main : ReedGreen.Light;
                    if (sheer + 1 < water && PixelNoise.Value(x, 4, 223) < 0.4f) body[x, sheer + 1] = ReedGreen.Shade;
                }
            body[20, 14] = ReedGreen.Main;
            body[19, 19] = ReedGreen.Main;
            body[34, 24] = Cream.Light;
            // The waterline: a pale broken line along the hull, rings off its ends.
            for (int x = 2; x < 44; x++)
            {
                float t = (x - 3) / 42f;
                int sheer = 30 - (int)MathF.Round(13 * t * t) - (int)MathF.Round(3 * t);
                if (sheer <= water && PixelNoise.Value(x, 5, 223) < 0.7f) under[x, water] = PondLight.WithAlpha(220);
            }
            foreach (var (x, y) in new[] { (1, 33), (0, 35), (45, 36), (46, 34), (10, 41), (11, 41), (30, 42), (31, 42), (32, 42) }) under[x, y] = PondLight.WithAlpha(150);
            var image = Finish(body, under);
            return image;
        }

        // An old water wheel by the water, two tiles by two: a great wheel of soaked timber on an axle between two
        // posts, some of its paddles gone, moss on its rim, the stone trough it turned in under it.
        static PixelImage WaterWheelRuin()
        {
            const int w = 32, h = 46;
            var body = new PixelImage(w, h);
            const float cx = 16, cy = 20;
            // The far post.
            Rect(body, 24, 16, 3, 26, WetWood.Shade);
            // The paddles: boards across the rim at eight places, three of them broken off.
            for (int i = 0; i < 12; i++)
            {
                if (i == 2 || i == 7 || i == 10) continue;
                float a = i * MathF.PI / 6 + 0.26f;
                float px = cx + MathF.Cos(a) * 16.5f, py = cy + MathF.Sin(a) * 16.5f, qx = cx + MathF.Cos(a) * 12.5f, qy = cy + MathF.Sin(a) * 12.5f;
                Line(body, (int)MathF.Round(qx), (int)MathF.Round(qy), (int)MathF.Round(px), (int)MathF.Round(py), WetWood.Light);
                Line(body, (int)MathF.Round(qx) + 1, (int)MathF.Round(qy), (int)MathF.Round(px) + 1, (int)MathF.Round(py), WetWood.Shade);
            }
            // The rim: two rings of timber; the spokes; the hub.
            for (int y = 3; y < 38; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d > 12f && d <= 14.6f) body[x, y] = d > 13.8f ? (dx + dy > 0 ? WetWood.Dark : WetWood.Main) : dx + dy < -6 ? WetWood.Light : dx + dy > 10 ? WetWood.Shade : WetWood.Main;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathF.PI / 3 + 0.26f;
                Line(body, (int)cx, (int)cy, (int)MathF.Round(cx + MathF.Cos(a) * 12.5f), (int)MathF.Round(cy + MathF.Sin(a) * 12.5f), i < 3 ? WetWood.Shade : WetWood.Main);
            }
            LitOval(body, cx, cy, 3.2f, 3.2f, WetWood);
            body[15, 19] = Iron.Light;
            body[16, 20] = Iron.Shade;
            // Moss on the rim's top, weed hanging from its lower paddles.
            for (int x = 4; x < 28; x++)
                for (int y = 4; y < 14; y++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d > 13f && d <= 14.6f && PixelNoise.Value(x, y, 231) < 0.55f) body[x, y] = PixelNoise.Value(x, y, 232) < 0.4f ? ReedGreen.Light : ReedGreen.Main;
                }
            foreach (var (x, y, len) in new[] { (9, 33, 4), (14, 35, 5), (21, 34, 3) })
                for (int k = 0; k < len; k++) body[x, y + k] = k == len - 1 ? ReedGreen.Shade : ReedGreen.Main;
            // The near post with its brace, and the axle's end on it.
            Rect(body, 5, 16, 3, 27, WetWood.Main);
            Rect(body, 5, 16, 1, 27, WetWood.Light);
            Rect(body, 7, 16, 1, 27, WetWood.Dark);
            Line(body, 8, 34, 13, 42, WetWood.Shade);
            Rect(body, 4, 19, 5, 3, WetWood.Light);
            Rect(body, 4, 21, 5, 1, WetWood.Dark);
            // The trough: bank stone, a little water still in it.
            Box(body, 1, 39, 30, 2, 4, BankStone);
            Rect(body, 9, 39, 15, 2, PondWater);
            Rect(body, 11, 39, 4, 1, PondLight);
            foreach (var (x, y) in new[] { (2, 41), (3, 43), (27, 42), (29, 41), (18, 43) }) body[x, y] = ReedGreen.Main;
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3));
        }
    }
}
