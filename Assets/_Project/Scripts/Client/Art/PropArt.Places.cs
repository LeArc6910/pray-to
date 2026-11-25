using System;
using Game.Core;

namespace Game.Client
{
    // The places' buildings and dressing in the mood of the biome they stand in (Docs/BiomeRework.md 4-3 장소의 건물 3,
    // 묶음 B1c; the person chose that each biome package redraws the places in it), and the biomes' landmarks.
    //  - The dirt cave, an old mining camp: timber frames and plank roofs, stovepipes, hanging lanterns, ore.
    //  - The mushroom forest: houses under great caps with pale stem walls and round doors, spore lamps, glowing caps.
    // Only the drawing changes, by the biome under the thing (StandArt.ShapeAt): the tile, its footprint, the picture's
    // size and its roof's rows are the old ones, so worlds made before get the new looks too, and nothing is saved or
    // sent. Anywhere else the old drawings stand. Drawn with PropArt's brushes: lit from the upper left, ringed in the
    // dark outline, on a crisp shadow under the foot.
    static partial class PropArt
    {
        static readonly Ramp Timber = new Ramp(Rgba.Hex("#C89458"), Rgba.Hex("#8E6236"), Rgba.Hex("#644224"), Rgba.Hex("#402814"));
        static readonly Ramp Plank = new Ramp(Rgba.Hex("#C2B49A"), Rgba.Hex("#988A70"), Rgba.Hex("#6C6050"), Rgba.Hex("#423A30"));
        static readonly Ramp PlankWall = new Ramp(Rgba.Hex("#9A7850"), Rgba.Hex("#745636"), Rgba.Hex("#523A24"), Rgba.Hex("#362414"));
        static readonly Ramp OldIron = new Ramp(Rgba.Hex("#9AA0AC"), Rgba.Hex("#6A7080"), Rgba.Hex("#484C5A"), Rgba.Hex("#2E303A"));
        static readonly Ramp CaveRock = new Ramp(Rgba.Hex("#9A8062"), Rgba.Hex("#6E5844"), Rgba.Hex("#4E3E30"), Rgba.Hex("#32261E"));
        static readonly Ramp OreCopper = new Ramp(Rgba.Hex("#F0B070"), Rgba.Hex("#D08A4A"), Rgba.Hex("#985A2C"), Rgba.Hex("#603818"));
        static readonly Rgba LampGlow = Rgba.Hex("#FFB060"), LampCore = Rgba.Hex("#FFF0B0");
        static readonly Rgba CaveMoss = Rgba.Hex("#6E7A3C");
        static readonly Rgba Hollow = Rgba.Hex("#0E0A10");

        static readonly Ramp StemWall = new Ramp(Rgba.Hex("#F2ECD2"), Rgba.Hex("#D8D0B0"), Rgba.Hex("#A8A080"), Rgba.Hex("#6E6A50"));
        static readonly Ramp CapBlue = new Ramp(Rgba.Hex("#8AB4FF"), Rgba.Hex("#4F86E8"), Rgba.Hex("#3460B4"), Rgba.Hex("#223C7A"));
        static readonly Ramp CapDeep = new Ramp(Rgba.Hex("#6E9AF0"), Rgba.Hex("#3E6AC8"), Rgba.Hex("#2A4A96"), Rgba.Hex("#1A2E66"));
        static readonly Ramp CapPlum = new Ramp(Rgba.Hex("#E8A0C8"), Rgba.Hex("#C0689A"), Rgba.Hex("#8C4470"), Rgba.Hex("#5A2A4A"));
        static readonly Ramp CapPink = new Ramp(Rgba.Hex("#FFC0D8"), Rgba.Hex("#E88AB0"), Rgba.Hex("#B45C84"), Rgba.Hex("#7A3A58"));
        static readonly Ramp BarkWood = new Ramp(Rgba.Hex("#A8906A"), Rgba.Hex("#7A6040"), Rgba.Hex("#54402A"), Rgba.Hex("#34281A"));
        static readonly Ramp LimeGlow = new Ramp(Rgba.Hex("#EEFFB8"), Rgba.Hex("#B8F25A"), Rgba.Hex("#7CC03C"), Rgba.Hex("#4A7A28"));
        static readonly Rgba Spot = Rgba.Hex("#E4F0FF"), GillPink = Rgba.Hex("#E88AB0"), ForestMoss = Rgba.Hex("#56682E");
        // A mushroom house's windows: lime by night, dark glass by day (Daylit).
        static readonly Rgba SporeWindow = Rgba.Hex("#C8F66E"), SporeWindowCore = Rgba.Hex("#F2FFC8");

        // The looks: 0 the old drawing, 1 the dirt cave's, 2 the mushroom forest's.
        public const int CaveLook = 1, ForestLook = 2;

        // The places' things that are redrawn by biome.
        public static bool HasBiomeLook(TileType type) => type == TileType.Cottage || type == TileType.Cabin || type == TileType.Well || type == TileType.LampPost
            || type == TileType.Signpost || type == TileType.Bench || type == TileType.Flowerbed || type == TileType.Crates;

        // Which look a thing takes in a biome: the dirt cave's, the mushroom forest's, or the old one (no biome given,
        // any other biome, any other thing).
        public static int BiomeLook(TileType type, Biome? biome) =>
            !HasBiomeLook(type) ? 0 : biome == Biome.Cave ? CaveLook : biome == Biome.Mushroom ? ForestLook : biome == Biome.Ice ? IceLook : biome == Biome.Sand ? SandLook
            : biome == Biome.Crystal ? CrystalLook : biome == Biome.Lake ? LakeLook : 0;

        // The light a thing with a biome look gives there, or null for its own (PropArt.Lights).
        public static Rgba? BiomeLight(TileType type, Biome biome) =>
            !HasBiomeLook(type) ? (Rgba?)null : biome == Biome.Mushroom ? Rgba.Hex("#B8F25A") : biome == Biome.Cave ? LampGlow
            // The ice cave's frosted lanterns burn pale blue, the sand cave's braziers gold (묶음 B2a).
            : biome == Biome.Ice ? FrostGlow : biome == Biome.Sand ? GoldLight
            // The crystal cave's caged crystals glow pink, the lake's lanterns teal (묶음 B3a).
            : biome == Biome.Crystal ? PinkLight : biome == Biome.Lake ? TealLight : (Rgba?)null;

        // The drawing of a place's thing in a look (CaveLook, ForestLook), the same size as the old one.
        public static PixelImage PlaceProp(TileType type, int look)
        {
            if (look == IceLook || look == SandLook) return IceSandPlaceProp(type, look);
            if (look == CrystalLook || look == LakeLook) return CrystalLakePlaceProp(type, look);
            bool cave = look != ForestLook;
            return type switch
            {
                TileType.Cottage => cave ? CampHouse(false) : CapHouse(false),
                TileType.Cabin => cave ? CampHouse(true) : CapHouse(true),
                TileType.Well => cave ? ShaftWell() : CapWell(),
                TileType.LampPost => cave ? TimberLampPost() : SporeLamp(),
                TileType.Signpost => cave ? PlankSign() : StemSign(),
                TileType.Bench => cave ? PlankBench() : LogBench(),
                TileType.Flowerbed => cave ? RootTrough() : GlowBed(),
                TileType.Crates => cave ? OreCrates() : CapBaskets(),
                _ => null,
            };
        }

        // The rows of a landmark's picture that fade when someone is behind it.
        public static int LandmarkCanopy(TileType type) => type == TileType.MinerHut ? 30 : CrystalLakeProps.IsLandmark(type) ? CrystalLakeLandmarkCanopy(type) : IceSandLandmarkCanopy(type);

        // The landmarks that stand like the places' structures (the fairy ring's caps are BiomeArt's).
        public static PixelImage Landmark(TileType type) => type == TileType.MinerHut ? OldMinerHut() : type == TileType.MineFrame ? DriftFrame()
            : CrystalLakeProps.IsLandmark(type) ? CrystalLakeLandmark(type) : IceSandLandmark(type);

        // ---- Brushes ----

        static void Line(PixelImage image, int x0, int y0, int x1, int y1, Rgba color)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0 : i / (float)steps;
                int x = (int)MathF.Round(x0 + (x1 - x0) * t), y = (int)MathF.Round(y0 + (y1 - y0) * t);
                if (image.Inside(x, y)) image[x, y] = color;
            }
        }

        // The upper half of an oval on a flat base: a mushroom's cap, lit from the upper left, its rim in shade.
        static void Cap(PixelImage image, float cx, float baseY, float rx, float ry, Ramp ramp)
        {
            for (int y = (int)MathF.Floor(baseY - ry); y < (int)baseY; y++)
            {
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - baseY) / ry;
                    if (dx * dx + dy * dy > 1 || !image.Inside(x, y)) continue;
                    float lit = -dx * 0.5f - dy * 1.7f - 0.85f;
                    image[x, y] = lit > 0.42f ? ramp.Light : lit > -0.15f ? ramp.Main : lit > -0.62f ? ramp.Shade : ramp.Dark;
                }
            }
        }

        // Pale spots on a cap between its rows, by a hash; only where the cap is.
        static void CapSpots(PixelImage image, int x0, int y0, int w, int h, int count, int salt, Rgba spot)
        {
            for (int i = 0; i < count; i++)
            {
                int x = x0 + 2 + (int)(PixelNoise.Value(i, w, salt) * (w - 5)), y = y0 + 2 + (int)(PixelNoise.Value(i, h, salt + 1) * (h - 5));
                if (image[x, y].A < 200 || image[x + 1, y + 1].A < 200 || image[x, y - 2].A < 200 || image[x - 2, y].A < 200 || image[x + 3, y].A < 200) continue;
                image.Fill(x, y, 2, 2, spot);
                image[x + 1, y + 1] = spot.Scale(0.82f);
            }
        }

        // A lantern: an iron cap and foot round a glowing glass, hung from the pixel above its middle.
        static void HangLantern(PixelImage image, int x, int y, bool lit = true)
        {
            image[x + 1, y - 1] = OldIron.Dark;
            Rect(image, x, y, 4, 1, OldIron.Shade);
            Rect(image, x, y + 1, 4, 4, lit ? LampGlow : Rgba.Hex("#4A4038"));
            if (lit) Rect(image, x + 1, y + 1, 2, 2, LampCore);
            Rect(image, x, y + 5, 4, 1, OldIron.Dark);
        }

        // A roof of boards laid down the slope from row top to row bottom: seams between boards, two battens across,
        // a board or two newer than the rest.
        static void PlankRoof(PixelImage image, int w, int top, int bottom, Ramp ramp, int salt)
        {
            for (int y = top; y < bottom; y++)
            {
                int inset = Math.Max(0, (14 - (y - top)) / 4);
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    int board = (x - 2) / 5;
                    float t = (x - 2f) / (w - 4f);
                    var color = t > 0.82f ? ramp.Shade : y < top + 4 ? ramp.Light : ramp.Main;
                    float tone = PixelNoise.Value(board, salt, 77);
                    if (tone > 0.8f) color = color.Mix(Timber.Light, 0.35f);
                    else if (tone < 0.2f) color = color.Scale(0.88f);
                    if ((x - 2) % 5 == 0) color = color.Scale(0.7f);
                    // The boards' ends do not line up.
                    if (y == bottom - 1 - (int)(PixelNoise.Value(board, salt, 78) * 2)) color = color.Scale(0.8f);
                    image[x, y] = color;
                }
            }
            foreach (int batten in new[] { top + (bottom - top) / 3, top + (bottom - top) * 2 / 3 })
                for (int x = 3; x < w - 3; x++)
                    if (image[x, batten].A > 0) image[x, batten] = image[x, batten].Scale(0.72f);
            Rect(image, 2, bottom - 1, w - 4, 2, ramp.Dark);
        }

        // ---- The dirt cave: a mining camp ----

        // A camp house, four tiles by three: a roof of weathered boards with a stovepipe through it, walls of dark planks
        // in a frame of pit timber, a strapped door with a lantern hung beside it and a heap of ore at its
        // corner. The cabin (the hero's, the mine village's) has log walls under the same roof and a tarpaulin
        // over part of it.
        static PixelImage CampHouse(bool logs)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = logs ? 14 : 12, eave = logs ? 44 : 46;
            // The stovepipe, with its rain cap.
            int pipe = logs ? 11 : 45;
            Column(body, pipe, 3, 5, 16, OldIron);
            Rect(body, pipe - 1, 2, 7, 2, OldIron.Dark);
            Rect(body, pipe, 9, 5, 1, OldIron.Dark);
            PlankRoof(body, w, roofTop, eave, Plank, logs ? 5 : 3);
            // The ridge pole, its ends past the gables.
            Rect(body, 5, roofTop, w - 10, 2, Timber.Main);
            Rect(body, 5, roofTop, w - 10, 1, Timber.Light);
            Rect(body, w - 9, roofTop, 4, 2, Timber.Shade);
            if (logs)
            {
                // A tarpaulin lashed over the leak.
                for (int y = 20; y < 36; y++)
                    for (int x = 36; x < 54; x++)
                        body[x, y] = (x + y) % 9 == 0 ? Straw.Shade : y < 22 ? Straw.Light.Scale(0.9f) : x > 50 ? Straw.Shade : Straw.Main.Scale(0.88f);
                foreach (var (cx, cy) in new[] { (36, 20), (53, 20), (36, 35), (53, 35) }) body[cx, cy] = OldIron.Dark;
            }
            int wallTop = eave, wallBottom = 68;
            if (logs)
            {
                for (int y = wallTop; y < wallBottom; y++)
                {
                    int row = (y - wallTop) % 4;
                    var color = row == 0 ? Wood.Light : row == 3 ? Wood.Dark : Wood.Main;
                    Rect(body, 5, y, w - 10, 1, color.Scale(0.92f));
                    Rect(body, w - 8, y, 3, 1, (row == 0 ? Wood.Main : Wood.Shade).Scale(0.92f));
                }
                for (int y = wallTop + 1; y < wallBottom; y += 4)
                {
                    foreach (int cx in new[] { 4, w - 5 })
                    {
                        Oval(body, cx + 0.5f, y + 1.5f, 2.5f, 2, Wood.Light);
                        body[cx, y + 1] = Wood.Shade;
                    }
                }
            }
            else
            {
                // Planks laid across, darker than the frame that holds them.
                for (int y = wallTop; y < wallBottom; y++)
                {
                    var color = (y - wallTop) % 5 == 4 ? PlankWall.Dark : (y - wallTop) % 5 == 0 ? PlankWall.Main.Mix(PlankWall.Light, 0.4f) : PlankWall.Main;
                    Rect(body, 5, y, w - 10, 1, color);
                }
                Rect(body, w - 8, wallTop, 3, wallBottom - wallTop, PlankWall.Shade);
                // The frame: posts, a sill beam under the windows and a brace in each end panel.
                foreach (int post in new[] { 5, 22, 39, w - 8 })
                {
                    Rect(body, post, wallTop, 3, wallBottom - wallTop, Timber.Main);
                    Rect(body, post, wallTop, 1, wallBottom - wallTop, Timber.Light);
                    Rect(body, post + 2, wallTop, 1, wallBottom - wallTop, Timber.Shade);
                }
                Rect(body, 5, 60, w - 10, 2, Timber.Main);
                Rect(body, 5, 60, w - 10, 1, Timber.Light);
                Line(body, 9, 67, 20, 62, Timber.Shade);
                Line(body, 43, 62, 54, 67, Timber.Shade);
            }
            for (int y = wallTop; y < wallTop + 3; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = (logs ? Wood.Shade : PlankWall.Shade).Scale(y == wallTop ? 0.7f : 0.85f);
            // The door: heavy planks with two iron straps under a timber lintel.
            int door = logs ? 38 : 27;
            Rect(body, door - 2, 50, 14, 2, Timber.Light);
            Rect(body, door - 2, 51, 14, 1, Timber.Shade);
            Rect(body, door, 52, 10, 16, DarkWood.Main);
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 53, 1, 15, DarkWood.Dark);
            Rect(body, door, 55, 10, 1, OldIron.Main);
            Rect(body, door, 63, 10, 1, OldIron.Main);
            body[door + 8, 59] = OldIron.Light;
            // The lantern by the door, on its bracket.
            int lamp = door - 7;
            Rect(body, lamp, 51, 5, 1, OldIron.Dark);
            HangLantern(body, lamp, 53);
            // Windows.
            foreach (int wx in logs ? new[] { 13 } : new[] { 10, 45 })
            {
                int ww = logs ? 12 : 9;
                Rect(body, wx, 50, ww, 8, Timber.Dark);
                Rect(body, wx + 1, 51, ww - 2, 6, WindowLight);
                Rect(body, wx + 1, 51, 3, 2, WindowCore);
                Rect(body, wx + ww / 2, 51, 1, 6, Timber.Shade);
                Rect(body, wx + 1, 54, ww - 2, 1, Timber.Shade);
                Rect(body, wx - 1, 58, ww + 2, 1, Timber.Light);
            }
            // The footing: flat stones.
            Rect(body, 4, 68, w - 8, 3, CaveRock.Shade);
            for (int x = 5; x < w - 6; x += 6)
            {
                Rect(body, x, 68, 5, 1, CaveRock.Main);
                body[x + 5, 69] = CaveRock.Dark;
            }
            // A pick leant on the wall and ore heaped at the corner.
            if (logs)
            {
                const int px = 50;
                Line(body, px, 69, px + 5, 59, Timber.Light);
                Line(body, px + 1, 69, px + 6, 59, Timber.Shade);
                Line(body, px + 2, 58, px + 8, 57, OldIron.Light);
                Line(body, px + 8, 57, px + 10, 60, OldIron.Main);
                Line(body, px + 2, 58, px + 1, 61, OldIron.Main);
            }
            int ore = logs ? 6 : 47;
            LitOval(body, ore + 4, 66.5f, 5.5f, 3.6f, CaveRock);
            LitOval(body, ore + 8, 68, 3.5f, 2.6f, CaveRock);
            foreach (var (ox, oy) in new[] { (2, 65), (5, 64), (4, 67), (8, 68), (7, 66) })
            {
                body[ore + ox, oy] = OreCopper.Light;
                body[ore + ox + 1, oy] = OreCopper.Main;
            }
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The camp's well: a shaft cribbed in timber under a headframe, a windlass across it with its rope and bucket.
        static PixelImage ShaftWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            // The crib: squared timbers, their ends showing at the corners.
            Box(body, 2, 21, 16, 3, 8, Timber);
            Rect(body, 4, 22, 12, 2, Hollow);
            Rect(body, 2, 27, 16, 1, Timber.Dark);
            foreach (int cx in new[] { 2, 16 }) Rect(body, cx, 25, 2, 1, Timber.Light);
            // The headframe: two posts and a beam, braced.
            Column(body, 3, 4, 2, 19, Timber);
            Column(body, 15, 4, 2, 19, Timber);
            Box(body, 1, 2, 18, 1, 2, Timber);
            Line(body, 5, 8, 7, 5, Timber.Shade);
            Line(body, 14, 8, 12, 5, Timber.Shade);
            // The windlass and its crank.
            Rect(body, 5, 11, 10, 3, Timber.Main);
            Rect(body, 5, 11, 10, 1, Timber.Light);
            Rect(body, 5, 13, 10, 1, Timber.Dark);
            foreach (int x in new[] { 7, 9, 11 }) body[x, 12] = Straw.Main;
            Rect(body, 17, 12, 2, 1, OldIron.Main);
            Rect(body, 18, 12, 1, 4, OldIron.Shade);
            // The rope down to the bucket on the crib's edge.
            Rect(body, 10, 14, 1, 4, Straw.Main);
            Box(body, 8, 17, 5, 1, 4, OldIron);
            Rect(body, 8, 19, 5, 1, OldIron.Dark);
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lamp post of the camp: a pit timber with a bracket and a lantern hung from it.
        static PixelImage TimberLampPost()
        {
            var body = new PixelImage(T, 42);
            Column(body, 8, 6, 3, 32, Timber);
            for (int y = 12; y < 36; y += 7) body[9, y] = Timber.Dark;
            // The bracket and its brace.
            Rect(body, 2, 7, 7, 2, Timber.Main);
            Rect(body, 2, 7, 7, 1, Timber.Light);
            Line(body, 4, 9, 7, 13, Timber.Shade);
            Rect(body, 7, 4, 5, 2, Timber.Dark);
            HangLantern(body, 2, 11);
            Rect(body, 2, 17, 4, 1, OldIron.Dark);
            // Stones wedged round its foot.
            LitOval(body, 7, 38.5f, 2.5f, 1.8f, CaveRock);
            LitOval(body, 12, 38.5f, 2.5f, 1.8f, CaveRock);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the camp: a board nailed to a post, a pick and a hammer crossed on it.
        static PixelImage PlankSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 7, 9, 3, 15, Timber);
            Rect(body, 1, 3, 14, 3, Plank.Main);
            Rect(body, 1, 6, 14, 3, Plank.Main.Scale(0.9f));
            Rect(body, 1, 3, 14, 1, Plank.Light);
            Rect(body, 1, 8, 14, 1, Plank.Dark);
            Rect(body, 14, 4, 1, 4, Plank.Shade);
            Line(body, 4, 7, 10, 4, Timber.Dark);
            Line(body, 4, 4, 10, 7, Timber.Dark);
            Rect(body, 10, 3, 2, 2, OldIron.Shade);
            body[2, 4] = OldIron.Light;
            body[13, 7] = OldIron.Light;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        // A board across two stones.
        static PixelImage PlankBench()
        {
            var body = new PixelImage(T, 18);
            LitOval(body, 4, 13, 3, 3, CaveRock);
            LitOval(body, 12, 13, 3, 3, CaveRock);
            Box(body, 0, 7, 16, 2, 3, Timber);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 7, 1, 2, Timber.Main);
            body[2, 8] = OldIron.Light;
            body[13, 8] = OldIron.Light;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A trough of earth where the camp grows what grows in the dark: brown caps, pale roots, moss.
        static PixelImage RootTrough()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 8, 14, 2, 7, Timber);
            Rect(body, 2, 8, 12, 2, Rgba.Hex("#3A2B20"));
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 10, 1, 7, Timber.Shade);
            var cap = Ramp.Of("#B07A4A");
            foreach (var (x, y, r) in new[] { (4, 6, 2), (9, 4, 3), (13, 6, 2) })
            {
                Rect(body, x, y, 1, 9 - y, Cream.Main);
                Rect(body, x - r + 1, y - 1, r * 2 - 1, 2, cap.Main);
                Rect(body, x - r + 2, y - 2, Math.Max(1, r * 2 - 3), 1, cap.Light);
                body[x + r - 1, y] = cap.Shade;
            }
            body[6, 8] = CaveMoss;
            body[7, 7] = CaveMoss;
            body[11, 8] = CaveMoss;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Ore crates: an iron-bound crate and an open one on it heaped with copper ore.
        static PixelImage OreCrates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, Timber);
            Rect(body, 1, 16, 14, 1, Timber.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, Timber.Shade);
            foreach (int x in new[] { 1, 13 }) Rect(body, x, 16, 2, 10, OldIron.Shade);
            Rect(body, 1, 20, 14, 1, OldIron.Main);
            // The open crate and its ore.
            Rect(body, 3, 7, 10, 7, Timber.Main);
            Rect(body, 3, 13, 10, 1, Timber.Dark);
            Rect(body, 11, 7, 2, 7, Timber.Shade);
            Rect(body, 3, 7, 10, 1, Timber.Light);
            foreach (int x in new[] { 6, 9 }) Rect(body, x, 8, 1, 5, Timber.Shade);
            LitOval(body, 6, 5.5f, 3, 2.5f, CaveRock);
            LitOval(body, 10, 5, 3, 3, CaveRock);
            foreach (var (x, y) in new[] { (5, 4), (7, 6), (9, 3), (11, 5), (10, 6) })
            {
                body[x, y] = OreCopper.Light;
                body[x + 1, y] = OreCopper.Main;
            }
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The mushroom forest ----

        // A house under a great cap, four tiles by three: the cap for a roof (blue with pale spots; the cabin's plum
        // and flatter, a stone chimney through it), pink gills and lime lights under its rim, a wall of pale stem
        // threaded with mycelium, a round door of bark and round windows that glow lime by night, small caps at its foot.
        static PixelImage CapHouse(bool cabin)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            var cap = cabin ? CapPlum : CapBlue;
            int rim = cabin ? 44 : 46, top = cabin ? 10 : 6;
            if (cabin)
            {
                Column(body, 10, 6, 8, 18, DarkStone);
                for (int i = 0; i < 3; i++) Oval(body, 13 + i, 3 - i, 3, 2, new Rgba(200, 210, 200, 140 - i * 30));
            }
            else
            {
                // A small cap grown from the big one's shoulder, and a bent flue beside it.
                Column(body, 47, 3, 3, 8, OldIron);
                Rect(body, 46, 2, 5, 1, OldIron.Dark);
            }
            // The gills under the rim, then the cap over them.
            Rect(body, 4, rim - 2, w - 8, 3, (cabin ? StemWall.Shade : GillPink).Scale(0.7f));
            for (int x = 6; x < w - 6; x += 3) Rect(body, x, rim - 1, 1, 2, (cabin ? StemWall.Shade : GillPink).Scale(0.5f));
            var roof = new PixelImage(w, h);
            Cap(roof, w / 2f, rim - 1, w / 2f - 1, rim - 1 - top, cap);
            CapSpots(roof, 4, top, w - 8, rim - top - 4, 26, cabin ? 23 : 11, cabin ? Rgba.Hex("#FFF0F6") : Spot);
            body.Draw(roof, 0, 0);
            Rect(body, 3, rim - 2, w - 6, 1, cap.Dark);
            // Lime lights hang under the rim.
            foreach (int x in new[] { 8, 19, 44, 55 })
            {
                body[x, rim + 1] = LimeGlow.Main;
                body[x, rim + 2] = LimeGlow.Light;
            }
            // The stem's wall, bulging a little at its foot.
            int left = 8, right = w - 8;
            for (int y = rim + 1; y < 69; y++)
            {
                int bulge = y > 62 ? 1 : 0;
                for (int x = left - bulge; x < right + bulge; x++)
                {
                    float t = (x - left) / (float)(right - left);
                    body[x, y] = t < 0.12f ? StemWall.Light : t < 0.7f ? StemWall.Main : t < 0.92f ? StemWall.Shade : StemWall.Dark;
                }
            }
            for (int y = rim + 1; y < rim + 4; y++)
                for (int x = left; x < right; x++)
                    body[x, y] = StemWall.Shade.Scale(y == rim + 1 ? 0.75f : 0.9f);
            // Threads of mycelium up the wall.
            foreach (int x in new[] { 12, 23, 41, 52 })
                for (int y = rim + 5; y < 67; y++)
                    if (PixelNoise.Value(x, y, 31) < 0.55f) body[x + (y / 5 % 2), y] = StemWall.Shade;
            // The round door: bark planks in an arch, a ring for a handle, a flat stone before it.
            int door = cabin ? 38 : 27;
            for (int y = 51; y < 69; y++)
            {
                int inset = y < 53 ? 3 - (y - 51) * 1 : y < 55 ? 1 : 0;
                Rect(body, door - 1 + inset, y, 12 - inset * 2, 1, BarkWood.Dark);
                if (y > 51) Rect(body, door + inset, y, 10 - inset * 2, 1, BarkWood.Main);
            }
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 54, 1, 15, BarkWood.Shade);
            Rect(body, door + 1, 53, 8, 1, BarkWood.Light);
            body[door + 7, 61] = Gold.Main;
            body[door + 7, 62] = Gold.Shade;
            // Round windows.
            foreach (int wx in cabin ? new[] { 18 } : new[] { 14, 49 })
            {
                Oval(body, wx + 0.5f, 56.5f, 5, 5, BarkWood.Shade);
                Oval(body, wx + 0.5f, 56.5f, 3.6f, 3.6f, SporeWindow);
                Rect(body, wx - 2, 54, 2, 2, SporeWindowCore);
                Rect(body, wx, 53, 1, 7, BarkWood.Shade);
                Rect(body, wx - 3, 56, 7, 1, BarkWood.Shade);
            }
            // Shelf fungus up one side and small caps at the foot.
            foreach (var (x, y) in new[] { (right - 3, 52), (right - 5, 60) })
            {
                Rect(body, x, y, 6, 2, Rgba.Hex("#E8B070"));
                Rect(body, x, y, 6, 1, Rgba.Hex("#FFD49A"));
                Rect(body, x + 1, y + 2, 4, 1, Rgba.Hex("#A86A3A"));
            }
            foreach (var (x, r, ramp) in new[] { (5, 3, CapPink), (cabin ? 30 : 42, 2, LimeGlow), (58, 3, CapPink) })
            {
                Rect(body, x, 66, 2, 4, StemWall.Main);
                Cap(body, x + 1, 67, r + 0.5f, r, ramp);
            }
            for (int x = left - 1; x < right + 1; x++)
                if (PixelNoise.Value(x, 3, 41) < 0.5f) body[x, 69] = ForestMoss;
                else body[x, 69] = StemWall.Dark;
            Rect(body, door - 2, 69, 14, 2, DarkStone.Light);
            Rect(body, door - 2, 70, 14, 1, DarkStone.Shade);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The forest's well: a mossy ring of stone under a cap held on two stems, a lime drop hanging from it.
        static PixelImage CapWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, Stone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, Stone.Shade);
            Rect(body, 2, 26, 16, 1, Stone.Shade);
            LitOval(body, 10, 22, 8, 3, Stone);
            Oval(body, 10, 22, 6, 1.6f, Water.Dark);
            body[8, 22] = LimeGlow.Main;
            body[12, 21] = LimeGlow.Shade;
            foreach (var (x, y) in new[] { (3, 24), (4, 28), (8, 29), (13, 25), (15, 29), (6, 20), (14, 20) }) body[x, y] = ForestMoss;
            Column(body, 3, 9, 2, 14, StemWall);
            Column(body, 15, 9, 2, 14, StemWall);
            Rect(body, 2, 9, w - 4, 2, GillPink.Scale(0.65f));
            Cap(body, w / 2f, 10, w / 2f - 0.5f, 9, CapBlue);
            foreach (var (x, y) in new[] { (5, 5), (10, 3), (14, 6) })
            {
                body[x, y] = Spot;
                body[x + 1, y] = Spot.Scale(0.85f);
            }
            body[10, 12] = LimeGlow.Main;
            body[10, 13] = LimeGlow.Light;
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A spore lamp: a pale stalk bent at its top, a glowing lime bulb hung under a small cap, tiny caps at its foot.
        static PixelImage SporeLamp()
        {
            var body = new PixelImage(T, 42);
            Column(body, 7, 13, 2, 26, StemWall);
            for (int y = 18; y < 38; y += 6) body[8, y] = StemWall.Dark;
            // The bulb, its core bright, under its cap.
            LitOval(body, 8, 9.5f, 4, 4.5f, LimeGlow);
            Rect(body, 6, 7, 2, 3, LimeGlow.Light);
            body[7, 8] = Rgba.White;
            Rect(body, 2, 5, 12, 1, GillPink.Scale(0.65f));
            Cap(body, 8, 5, 6.5f, 4.5f, CapDeep);
            body[5, 2] = Spot;
            body[9, 1] = Spot;
            // Its foot.
            Rect(body, 6, 38, 4, 2, StemWall.Shade);
            Rect(body, 4, 37, 1, 3, StemWall.Main);
            Cap(body, 4.5f, 38, 2.5f, 2.5f, CapPink);
            Rect(body, 11, 38, 1, 2, StemWall.Main);
            Cap(body, 11.5f, 39, 2.2f, 2, LimeGlow);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the forest: a slab of bark on a pale stalk, a lime arrow painted on it, a cap grown on its top.
        static PixelImage StemSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 7, 9, 2, 15, StemWall);
            for (int x = 1; x < 14; x++)
            {
                int top = x > 11 ? 4 + (x - 11) : 4, bottom = x > 11 ? 10 - (x - 11) : 10;
                for (int y = top; y < bottom; y++) body[x, y] = y == top ? BarkWood.Light : x > 11 ? BarkWood.Shade : BarkWood.Main;
            }
            Rect(body, 3, 6, 6, 1, LimeGlow.Main);
            Rect(body, 3, 8, 4, 1, LimeGlow.Shade);
            Rect(body, 3, 3, 1, 1, StemWall.Main);
            Cap(body, 3.5f, 4, 3, 3, CapPink);
            body[12, 9] = ForestMoss;
            body[2, 9] = ForestMoss;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        // A length of fallen trunk to sit on: bark with moss along its top, its cut end pale, a shelf fungus on it.
        static PixelImage LogBench()
        {
            var body = new PixelImage(T, 18);
            for (int y = 7; y < 15; y++)
            {
                var color = y < 9 ? BarkWood.Light : y < 12 ? BarkWood.Main : y < 14 ? BarkWood.Shade : BarkWood.Dark;
                Rect(body, 2, y, 13, 1, color);
            }
            for (int x = 5; x < 14; x += 3) Rect(body, x, 10, 1, 3, BarkWood.Shade);
            Oval(body, 2.5f, 11, 2.2f, 4, StemWall.Main);
            Oval(body, 2.5f, 11, 1.2f, 2.4f, StemWall.Shade);
            body[2, 11] = StemWall.Dark;
            for (int x = 5; x < 14; x++)
                if (PixelNoise.Value(x, 7, 53) < 0.6f) body[x, 7] = ForestMoss;
            Rect(body, 10, 5, 4, 1, Rgba.Hex("#FFD49A"));
            Rect(body, 10, 6, 4, 1, Rgba.Hex("#E8B070"));
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A bed of the forest's small glowing caps in mossy earth, edged with pale stones.
        static PixelImage GlowBed()
        {
            var body = new PixelImage(T, 18);
            LitOval(body, 8, 12.5f, 7, 3.5f, Ramp.Of("#3E4A26"));
            foreach (int x in new[] { 2, 6, 10, 13 }) LitOval(body, x + 0.5f, 15, 1.6f, 1.3f, Stone);
            foreach (var (x, y, r, ramp) in new[] { (4, 8, 2, CapBlue), (8, 5, 3, LimeGlow), (12, 8, 2, CapPink), (6, 10, 1, LimeGlow), (10, 10, 1, CapBlue) })
            {
                Rect(body, x, y, 1, 12 - y, StemWall.Main);
                Cap(body, x + 0.5f, y + 1, r + 0.5f, r + 0.5f, ramp);
            }
            body[8, 3] = LimeGlow.Light;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Baskets of the forest's harvest: a bark crate with moss on it and a woven basket heaped with caps.
        static PixelImage CapBaskets()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, BarkWood);
            Rect(body, 1, 16, 14, 1, BarkWood.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, BarkWood.Shade);
            for (int x = 2; x < 14; x++)
                if (PixelNoise.Value(x, 12, 59) < 0.45f) body[x, 12 + x % 2] = ForestMoss;
            // The basket: woven straw, its rim lighter.
            for (int y = 7; y < 14; y++)
                for (int x = 3 + (y > 11 ? 1 : 0); x < 13 - (y > 11 ? 1 : 0); x++)
                    body[x, y] = (x + y) % 2 == 0 ? Straw.Main : Straw.Shade;
            Rect(body, 3, 7, 10, 1, Straw.Light);
            Rect(body, 11, 8, 2, 5, Straw.Dark);
            foreach (var (x, r, ramp) in new[] { (5, 2, CapBlue), (10, 2, CapPink), (8, 3, CapBlue) })
            {
                Cap(body, x + 0.5f, 7 - (r == 3 ? 1 : 0), r + 0.5f, r + 0.5f, ramp);
            }
            body[8, 4] = Spot;
            body[5, 5] = Spot;
            body[12, 6] = LimeGlow.Main;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The landmarks ----

        // An old miner's hut, three tiles by two: left a long time. Its board roof sags and has lost a board, moss on
        // it; the stovepipe leans; the window is boarded over, the door hangs open on the dark inside; a broken pick by
        // the step.
        static PixelImage OldMinerHut()
        {
            const int w = 48, h = 58;
            var body = new PixelImage(w, h);
            // The leaning stovepipe.
            for (int y = 2; y < 16; y++) Column(body, 33 + (15 - y) / 5, y, 4, 1, OldIron);
            Rect(body, 34, 1, 6, 2, OldIron.Dark);
            var old = new Ramp(Plank.Light.Scale(0.94f), Plank.Main.Scale(0.92f), Plank.Shade, Plank.Dark);
            PlankRoof(body, w, 8, 30, old, 9);
            // A board gone, the dark of the loft through it; moss along the ridge and in the seams.
            Rect(body, 22, 14, 4, 9, Hollow);
            Rect(body, 22, 14, 4, 1, old.Dark);
            Line(body, 21, 23, 27, 21, old.Light);
            for (int y = 8; y < 28; y++)
                for (int x = 3; x < w - 3; x++)
                    if (body[x, y].A > 200 && !(x >= 22 && x < 26 && y >= 14 && y < 23) && PixelNoise.Value(x / 2, y / 2, 67) < (y < 11 ? 0.35f : 0.07f)) body[x, y] = CaveMoss.Scale(y < 11 ? 1.1f : 0.95f);
            // The wall: upright boards gone grey, corner posts.
            for (int y = 30; y < 52; y++)
            {
                for (int x = 4; x < w - 4; x++)
                {
                    var color = (x - 4) % 4 == 3 ? PlankWall.Shade : PixelNoise.Value((x - 4) / 4, 2, 71) < 0.3f ? PlankWall.Main : PlankWall.Main.Mix(PlankWall.Light, 0.45f);
                    if (y < 33) color = color.Scale(y == 30 ? 0.65f : 0.82f);
                    body[x, y] = color;
                }
            }
            foreach (int post in new[] { 4, w - 7 })
            {
                Rect(body, post, 30, 3, 22, Timber.Shade);
                Rect(body, post, 31, 1, 21, Timber.Main);
            }
            Rect(body, w - 6, 30, 2, 22, PlankWall.Dark);
            // The door stands open: the dark inside, the leaf swung in at an angle.
            Rect(body, 18, 35, 10, 17, Hollow);
            Rect(body, 17, 34, 12, 1, Timber.Main);
            for (int y = 36; y < 52; y++) Rect(body, 24, y, 4, 1, y % 5 == 0 ? DarkWood.Dark : DarkWood.Shade);
            Rect(body, 24, 36, 1, 16, DarkWood.Main);
            // The window, two boards nailed across it.
            Rect(body, 32, 36, 9, 8, Timber.Dark);
            Rect(body, 33, 37, 7, 6, Rgba.Hex("#2A2E36"));
            Line(body, 32, 38, 40, 41, Plank.Main);
            Line(body, 32, 39, 40, 42, Plank.Shade);
            Line(body, 32, 42, 40, 37, Plank.Light);
            // A lantern long out on its hook, and a cobweb in the corner of the eave.
            Rect(body, 9, 35, 5, 1, OldIron.Dark);
            HangLantern(body, 9, 37, lit: false);
            Line(body, 5, 33, 9, 33, new Rgba(220, 220, 230, 150));
            Line(body, 7, 33, 5, 36, new Rgba(220, 220, 230, 150));
            // The footing and the step, a pick with a broken haft beside it.
            Rect(body, 3, 52, w - 6, 3, CaveRock.Shade);
            for (int x = 4; x < w - 5; x += 6)
            {
                Rect(body, x, 52, 5, 1, CaveRock.Main);
                body[x + 5, 53] = CaveRock.Dark;
            }
            Rect(body, 16, 53, 14, 2, CaveRock.Light);
            Line(body, 36, 54, 40, 48, Timber.Light);
            Line(body, 38, 47, 43, 47, OldIron.Light);
            Line(body, 43, 47, 44, 49, OldIron.Main);
            for (int x = 4; x < w - 4; x++)
                if (PixelNoise.Value(x, 5, 73) < 0.3f) body[x, 51] = CaveMoss.Scale(0.85f);
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // The frame of an old drift, three tiles wide against the rock: two pit timbers and a lintel with its wedges,
        // the dark of the drift between them boarded across, the rails running in under the boards, rubble at the posts'
        // feet and a tin plate nailed to the lintel.
        static PixelImage DriftFrame()
        {
            const int w = 48, h = 46;
            var body = new PixelImage(w, h);
            // The dark of the drift, a little of its floor showing before it is lost.
            Rect(body, 7, 12, 34, 31, Hollow);
            for (int y = 36; y < 43; y++)
                for (int x = 8; x < 40; x++)
                    body[x, y] = Rgba.Hex("#3A2B20").Scale(0.25f + (y - 36) * 0.09f);
            // The rails going in, and their sleepers.
            foreach (int rail in new[] { 20, 27 })
                for (int y = 30; y < 43; y++)
                    body[rail + (y < 36 ? (rail == 20 ? 1 : -1) : 0), y] = OldIron.Main.Scale(0.35f + (y - 30) * 0.05f);
            for (int y = 37; y < 43; y += 3) Rect(body, 19, y, 10, 1, Timber.Dark.Scale(0.6f + (y - 37) * 0.06f));
            // The posts and the lintel over them, its ends past the posts, wedges driven in over each.
            Column(body, 2, 11, 7, 32, Timber);
            Column(body, 39, 11, 7, 32, Timber);
            foreach (int post in new[] { 2, 39 })
            {
                for (int y = 16; y < 42; y += 6) body[post + 3 + y % 2, y] = Timber.Dark;
                Rect(body, post, 11, 7, 2, Timber.Dark);
            }
            Box(body, 0, 3, 48, 2, 7, Timber);
            Rect(body, 0, 4, 2, 7, Timber.Light);
            Rect(body, 46, 5, 2, 6, Timber.Dark);
            for (int x = 6; x < 44; x += 9) Rect(body, x, 6, 1, 4, Timber.Shade);
            foreach (int x in new[] { 4, 41 }) Rect(body, x, 1, 4, 2, Timber.Shade);
            // Braces in the corners.
            Line(body, 9, 19, 15, 12, Timber.Main);
            Line(body, 9, 20, 16, 12, Timber.Shade);
            Line(body, 38, 19, 32, 12, Timber.Shade);
            Line(body, 38, 20, 31, 12, Timber.Dark);
            // The boards nailed across the way in.
            void Board(int x0, int y0, int x1, int y1, Ramp ramp)
            {
                for (int i = 0; i < 4; i++) Line(body, x0, y0 + i, x1, y1 + i, i == 0 ? ramp.Light : i == 3 ? ramp.Dark : ramp.Main);
                body[x0 + 1, y0 + 1] = OldIron.Light;
                body[x1 - 1, y1 + 1] = OldIron.Light;
            }
            Board(8, 18, 39, 23, Plank);
            Board(8, 33, 39, 27, Plank);
            Board(9, 25, 38, 25, new Ramp(Plank.Main, Plank.Shade, Plank.Shade, Plank.Dark));
            // A tin plate on the lintel, a cross scratched on it.
            Rect(body, 20, 4, 8, 5, OldIron.Main);
            Rect(body, 20, 4, 8, 1, OldIron.Light);
            Line(body, 22, 5, 25, 8, Royal.Shade);
            Line(body, 25, 5, 22, 8, Royal.Shade);
            // Rubble at the posts' feet.
            LitOval(body, 10, 42, 3, 2, CaveRock);
            LitOval(body, 37, 42.5f, 2.5f, 1.8f, CaveRock);
            LitOval(body, 13.5f, 43, 1.8f, 1.3f, CaveRock);
            return Finish(body, ShadowLayer(w, h, w / 2f, h - 2.5f, w / 2f - 1, 2));
        }
    }
}
