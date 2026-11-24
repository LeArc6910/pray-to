using System;
using Game.Core;

namespace Game.Client
{
    // The places' buildings and dressing in the ice cave's and the sand cave's mood, and those two biomes' landmarks
    // (Docs/BiomeRework.md 묶음 B2a; PropArt.Places.cs has the first two biomes' and how a look is chosen).
    //  - The ice cave, a hold dug in for the cold: walls of cut ice or rimed logs under roofs heavy with snow, icicles
    //    along every eave, lanterns frosted pale blue, warm windows.
    //  - The sand cave, a town among the old temple's stones: flat-roofed adobe with awnings and beam ends, a mud dome,
    //    clay jars, braziers on stone posts, carved sandstone.
    // The same tiles, footprints, picture sizes and roof rows as the old drawings.
    static partial class PropArt
    {
        static readonly Ramp IceBlock = new Ramp(Rgba.Hex("#DDF0FA"), Rgba.Hex("#A6CCE4"), Rgba.Hex("#749FC4"), Rgba.Hex("#48709A"));
        static readonly Ramp ClearIce = new Ramp(Rgba.Hex("#EAF8FF"), Rgba.Hex("#A8D6F0"), Rgba.Hex("#6FA4CC"), Rgba.Hex("#3E6890"));
        static readonly Ramp SnowLie = new Ramp(Rgba.Hex("#FFFFFF"), Rgba.Hex("#E4F0FA"), Rgba.Hex("#B4CCE0"), Rgba.Hex("#7E9CB8"));
        static readonly Ramp Slate = new Ramp(Rgba.Hex("#8AA4C0"), Rgba.Hex("#5E7A9C"), Rgba.Hex("#445C7C"), Rgba.Hex("#2C3E58"));
        static readonly Ramp RimedLog = new Ramp(Rgba.Hex("#B8B0A8"), Rgba.Hex("#8A7C70"), Rgba.Hex("#5E5048"), Rgba.Hex("#3A3028"));
        static readonly Rgba FrostGlow = Rgba.Hex("#BFE6FF"), FrostCore = Rgba.Hex("#F4FCFF"), FrostBerry = Rgba.Hex("#6FD0FF");
        static readonly Rgba BeastShade = Rgba.Hex("#2A4466"), BeastDeep = Rgba.Hex("#1A2C48");

        static readonly Ramp Adobe = new Ramp(Rgba.Hex("#F0D4A0"), Rgba.Hex("#D0A870"), Rgba.Hex("#A47C4A"), Rgba.Hex("#72522C"));
        static readonly Ramp Sandstone = new Ramp(Rgba.Hex("#EBCB94"), Rgba.Hex("#C49C62"), Rgba.Hex("#96703E"), Rgba.Hex("#654624"));
        static readonly Ramp SandDrift = new Ramp(Rgba.Hex("#D8B482"), Rgba.Hex("#B08A58"), Rgba.Hex("#866238"), Rgba.Hex("#5A3C24"));
        static readonly Ramp ClayPot = new Ramp(Rgba.Hex("#E8A474"), Rgba.Hex("#BE7044"), Rgba.Hex("#8C4828"), Rgba.Hex("#5A2C18"));
        static readonly Ramp AwningRed = new Ramp(Rgba.Hex("#E87A5A"), Rgba.Hex("#C04E3A"), Rgba.Hex("#8E3228"), Rgba.Hex("#5E201C"));
        static readonly Ramp FossilBone = new Ramp(Rgba.Hex("#FFF8E4"), Rgba.Hex("#EDE3C8"), Rgba.Hex("#BCAE8C"), Rgba.Hex("#857860"));
        static readonly Ramp CactusGreen = new Ramp(Rgba.Hex("#A8D08A"), Rgba.Hex("#6E9E5A"), Rgba.Hex("#4A743E"), Rgba.Hex("#2E4C2A"));
        static readonly Rgba AwningCream = Rgba.Hex("#F0E0B8"), TealPaint = Rgba.Hex("#3E8E8E"), GoldLight = Rgba.Hex("#FFC870");

        public const int IceLook = 3, SandLook = 4;

        static PixelImage IceSandPlaceProp(TileType type, int look)
        {
            bool ice = look == IceLook;
            return type switch
            {
                TileType.Cottage => ice ? SnowHouse(false) : AdobeHouse(false),
                TileType.Cabin => ice ? SnowHouse(true) : AdobeHouse(true),
                TileType.Well => ice ? FrozenWell() : SandWell(),
                TileType.LampPost => ice ? FrostLamp() : BrazierPost(),
                TileType.Signpost => ice ? SnowSign() : StoneSign(),
                TileType.Bench => ice ? IceBench() : SlabBench(),
                TileType.Flowerbed => ice ? BerryBed() : CactusBed(),
                TileType.Crates => ice ? SnowCrates() : ClayJars(),
                _ => null,
            };
        }

        static int IceSandLandmarkCanopy(TileType type) => type == TileType.FrozenBeast ? 30 : type == TileType.GiantSkull ? 18 : 0;

        static PixelImage IceSandLandmark(TileType type) => type switch
        {
            TileType.FrozenBeast => BeastInIce(),
            TileType.TempleFront => BuriedTemple(),
            TileType.GiantSkull => FossilSkull(),
            _ => null,
        };

        // ---- Brushes ----

        // Icicles hung under a row: one every few pixels, each its own length.
        static void Icicles(PixelImage image, int x0, int x1, int y, int salt, int longest = 5)
        {
            for (int x = x0; x < x1; x++)
            {
                uint hash = PixelNoise.Hash(x, salt, 211);
                if (hash % 3 != 0) continue;
                int length = 1 + (int)(hash / 3 % (uint)longest);
                for (int k = 0; k < length; k++) image[x, y + k] = k == length - 1 ? FrostCore : k == 0 ? ClearIce.Main : ClearIce.Light;
            }
        }

        // Snow lying on a ledge: a bright top row over a shaded one.
        static void SnowOn(PixelImage image, int x, int y, int w)
        {
            Rect(image, x, y, w, 1, SnowLie.Light);
            Rect(image, x, y + 1, w, 1, SnowLie.Main);
            image[x + w - 1, y + 1] = SnowLie.Shade;
        }

        // A heap of snow or sand on the ground with its foot on row baseY.
        static void Heap(PixelImage image, float cx, int baseY, float rx, float ry, Ramp ramp)
        {
            for (int y = (int)MathF.Floor(baseY - ry); y < baseY; y++)
            {
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - baseY) / ry;
                    if (dx * dx + dy * dy > 1 || !image.Inside(x, y)) continue;
                    float lit = -dx * 0.5f - dy * 1.5f - 0.7f;
                    image[x, y] = lit > 0.42f ? ramp.Light : lit > -0.2f ? ramp.Main : ramp.Shade;
                }
            }
        }

        // A roof under snow from row top to row bottom: slate showing through in a few places, the snow's lumps along
        // its ridge, a shaded right side, the eave's dark edge.
        static void SnowRoof(PixelImage image, int w, int top, int bottom, int salt)
        {
            for (int y = top; y < bottom; y++)
            {
                int inset = Math.Max(0, (14 - (y - top)) / 4);
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    float t = (x - 2f) / (w - 4f);
                    // The snow slid off in patches lower down, the slates' courses showing there.
                    bool bare = y > top + 12 && PixelNoise.Value((x + (y - top) / 6 * 3) / 6, (y - top) / 6, salt) < 0.24f && PixelNoise.Value(x / 2, y / 2, salt + 3) < 0.85f;
                    Rgba color;
                    if (bare)
                    {
                        color = (y - top) % 6 == 5 ? Slate.Dark : (x + (y - top) / 6 * 3) % 7 == 0 ? Slate.Shade : t > 0.82f ? Slate.Shade : Slate.Main;
                    }
                    else
                    {
                        float n = PixelNoise.Value(x / 3, y / 3, salt + 1);
                        color = t > 0.84f ? SnowLie.Shade : y < top + 5 ? SnowLie.Light : n > 0.72f ? SnowLie.Light : n < 0.16f ? SnowLie.Shade.Mix(SnowLie.Main, 0.5f) : SnowLie.Main;
                    }
                    image[x, y] = color;
                }
            }
            // The snow's edge lies thick over the eave.
            Rect(image, 2, bottom - 3, w - 4, 2, SnowLie.Light);
            Rect(image, 2, bottom - 1, w - 4, 1, SnowLie.Shade);
            Rect(image, w - 9, bottom - 3, 7, 2, SnowLie.Main);
            Rect(image, 2, bottom, w - 4, 1, Slate.Dark);
            // Lumps along the ridge.
            for (int x = 8; x < w - 8; x += 9) Heap(image, x + PixelNoise.Hash(x, salt, 5) % 3, top + 1, 5, 2.4f, SnowLie);
        }

        // ---- The ice cave ----

        // A house of the hold, four tiles by three: a roof deep in snow with icicles along its eave and a stone chimney
        // smoking through it, walls of cut ice blocks (the cabin's of rimed logs), a heavy door with a frosted lantern
        // by it, warm windows, snow drifted against the corner.
        static PixelImage SnowHouse(bool logs)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = logs ? 14 : 12, eave = logs ? 44 : 46;
            // The chimney: rough stone, snow on its cap, smoke.
            int flue = logs ? 10 : 44;
            Column(body, flue, 4, 8, 16, DarkStone);
            Rect(body, flue - 1, 3, 10, 2, DarkStone.Light);
            SnowOn(body, flue - 1, 1, 10);
            for (int i = 0; i < 3; i++) Oval(body, flue + 4 + i * 2, -1 - i, 3, 2, new Rgba(214, 226, 236, 150 - i * 40));
            SnowRoof(body, w, roofTop, eave, logs ? 7 : 4);
            int wallTop = eave + 1, wallBottom = 68;
            if (logs)
            {
                for (int y = wallTop; y < wallBottom; y++)
                {
                    int row = (y - wallTop) % 4;
                    var color = row == 0 ? RimedLog.Light : row == 3 ? RimedLog.Dark : RimedLog.Main;
                    Rect(body, 5, y, w - 10, 1, color);
                    Rect(body, w - 8, y, 3, 1, row == 0 ? RimedLog.Main : RimedLog.Shade);
                    // Rime along the top of each log.
                    if (row == 0)
                        for (int x = 6; x < w - 8; x++)
                            if (PixelNoise.Value(x, y, 37) < 0.3f) body[x, y] = SnowLie.Main;
                }
                for (int y = wallTop + 1; y < wallBottom; y += 4)
                    foreach (int cx in new[] { 4, w - 5 })
                    {
                        Oval(body, cx + 0.5f, y + 1.5f, 2.5f, 2, RimedLog.Light);
                        body[cx, y + 1] = RimedLog.Shade;
                    }
            }
            else
            {
                // Courses of cut ice, each block lit along its top, the joints darker, a glint in some.
                for (int y = wallTop; y < wallBottom; y++)
                {
                    int course = (y - wallTop) / 6, row = (y - wallTop) % 6;
                    for (int x = 5; x < w - 5; x++)
                    {
                        int along = (x - 5 + course % 2 * 5) % 10;
                        var color = row == 5 || along == 9 ? IceBlock.Shade : row == 0 ? IceBlock.Light : IceBlock.Main;
                        if (x > w - 9) color = row == 5 ? IceBlock.Dark : IceBlock.Shade;
                        if (row == 2 && along == 2 && PixelNoise.Value(x, course, 43) < 0.5f) color = FrostCore;
                        body[x, y] = color;
                    }
                }
            }
            for (int y = wallTop; y < wallTop + 2; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = body[x, y].Scale(y == wallTop ? 0.72f : 0.88f);
            Icicles(body, 4, w - 4, eave + 1, logs ? 3 : 1);
            // The door: dark planks banded with iron under a timber lintel.
            int door = logs ? 38 : 27;
            Rect(body, door - 2, 50, 14, 2, DarkWood.Light);
            SnowOn(body, door - 2, 48, 14);
            Rect(body, door, 52, 10, 16, DarkWood.Main);
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 53, 1, 15, DarkWood.Dark);
            Rect(body, door, 56, 10, 1, Iron.Main);
            Rect(body, door, 63, 10, 1, Iron.Main);
            body[door + 8, 60] = Iron.Light;
            // The lantern by the door, frosted pale blue.
            int lamp = door - 7;
            Rect(body, lamp, 51, 5, 1, Iron.Dark);
            Rect(body, lamp, 53, 4, 1, Iron.Shade);
            Rect(body, lamp, 54, 4, 4, FrostGlow);
            Rect(body, lamp + 1, 54, 2, 2, FrostCore);
            Rect(body, lamp, 58, 4, 1, Iron.Dark);
            body[lamp + 1, 59] = ClearIce.Light;
            // Windows, warm inside, snow on their sills.
            foreach (int wx in logs ? new[] { 13 } : new[] { 10, 45 })
            {
                int ww = logs ? 12 : 9;
                Rect(body, wx, 51, ww, 8, DarkWood.Dark);
                Rect(body, wx + 1, 52, ww - 2, 6, WindowLight);
                Rect(body, wx + 1, 52, 3, 2, WindowCore);
                Rect(body, wx + ww / 2, 52, 1, 6, DarkWood.Shade);
                Rect(body, wx + 1, 55, ww - 2, 1, DarkWood.Shade);
                SnowOn(body, wx - 1, 59, ww + 2);
                // Frost in the lower corners of the glass.
                body[wx + 1, 57] = FrostCore;
                body[wx + ww - 2, 57] = FrostGlow;
            }
            // The footing, and the drifts against it.
            Rect(body, 4, 68, w - 8, 3, DarkStone.Shade);
            for (int x = 5; x < w - 6; x += 6) Rect(body, x, 68, 5, 1, DarkStone.Main);
            Heap(body, logs ? 54 : 8, 71, 7, 6, SnowLie);
            Heap(body, logs ? 9 : 55, 71, 5, 3.5f, SnowLie);
            Heap(body, door + 16, 71, 4, 2.5f, SnowLie);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The hold's well: a ring of stone, the water in it frozen over, a small roof on two posts deep in snow, the
        // bucket frozen where it hangs.
        static PixelImage FrozenWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, DarkStone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, DarkStone.Shade);
            Rect(body, 2, 26, 16, 1, DarkStone.Shade);
            LitOval(body, 10, 22, 8, 3, DarkStone);
            Oval(body, 10, 22, 6, 1.6f, ClearIce.Main);
            body[7, 22] = FrostCore;
            body[8, 21] = ClearIce.Light;
            body[12, 22] = ClearIce.Shade;
            // The posts and the roof.
            Column(body, 3, 9, 2, 14, DarkWood);
            Column(body, 15, 9, 2, 14, DarkWood);
            Rect(body, 1, 7, 18, 3, Slate.Main);
            Rect(body, 1, 9, 18, 1, Slate.Dark);
            Heap(body, 10, 8, 9.5f, 6, SnowLie);
            Rect(body, 1, 6, 18, 1, SnowLie.Main);
            Icicles(body, 1, 19, 10, 9, 4);
            // The rope and the bucket, rimed.
            Rect(body, 10, 10, 1, 7, Straw.Shade);
            Box(body, 8, 16, 5, 1, 4, Iron);
            Rect(body, 8, 16, 5, 1, SnowLie.Light);
            body[9, 21] = ClearIce.Light;
            // Snow on the ring.
            foreach (var (x, y) in new[] { (3, 20), (4, 20), (15, 20), (16, 21), (5, 19) }) body[x, y] = SnowLie.Light;
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lamp of the hold: an iron post rimed white, a lantern whose frosted glass glows pale blue, snow on its cap
        // and an icicle under it.
        static PixelImage FrostLamp()
        {
            var body = new PixelImage(T, 42);
            Column(body, 7, 12, 2, 27, Iron);
            for (int y = 16; y < 38; y += 5) body[7, y] = SnowLie.Main;
            // The lantern.
            Rect(body, 4, 4, 8, 1, Iron.Shade);
            Rect(body, 5, 5, 6, 6, FrostGlow);
            Rect(body, 6, 6, 3, 3, FrostCore);
            Rect(body, 5, 5, 1, 6, Iron.Main);
            Rect(body, 10, 5, 1, 6, Iron.Shade);
            Rect(body, 4, 11, 8, 1, Iron.Dark);
            SnowOn(body, 4, 2, 8);
            body[6, 1] = SnowLie.Light;
            body[7, 1] = SnowLie.Light;
            body[5, 12] = ClearIce.Light;
            body[5, 13] = FrostCore;
            body[10, 12] = ClearIce.Main;
            // Its foot in a drift.
            Rect(body, 5, 38, 6, 2, Iron.Shade);
            Heap(body, 8, 40, 5, 3, SnowLie);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the hold: a board on a post, snow along its top and icicles under it.
        static PixelImage SnowSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 7, 9, 3, 15, DarkWood);
            for (int x = 1; x < 14; x++)
            {
                int top = x > 11 ? 4 + (x - 11) : 4, bottom = x > 11 ? 10 - (x - 11) : 10;
                for (int y = top; y < bottom; y++) body[x, y] = y == top ? Wood.Light : x > 11 ? Wood.Shade : Wood.Main;
            }
            Rect(body, 3, 6, 6, 1, DarkWood.Dark);
            Rect(body, 3, 8, 4, 1, DarkWood.Dark);
            SnowOn(body, 1, 2, 11);
            body[12, 4] = SnowLie.Main;
            Icicles(body, 1, 12, 10, 13, 3);
            Heap(body, 8, 24, 4, 2.4f, SnowLie);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        // A bench cut from a block of ice, a fur thrown over it.
        static PixelImage IceBench()
        {
            var body = new PixelImage(T, 18);
            Box(body, 0, 7, 16, 3, 7, ClearIce);
            Rect(body, 1, 8, 6, 1, FrostCore);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 10, 1, 6, ClearIce.Shade);
            // The fur: brown with a pale edge, hanging over the front.
            var fur = Ramp.Of("#8A6A4A");
            Rect(body, 8, 6, 7, 3, fur.Main);
            Rect(body, 8, 6, 7, 1, fur.Light);
            Rect(body, 9, 9, 6, 3, fur.Shade);
            for (int x = 9; x < 15; x += 2) body[x, 12] = Cream.Main;
            body[2, 6] = SnowLie.Light;
            body[3, 6] = SnowLie.Light;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A bed of frost berries: low rimed bushes in a planter of ice blocks, their berries glowing.
        static PixelImage BerryBed()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 9, 14, 2, 6, IceBlock);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 11, 1, 6, IceBlock.Shade);
            var bush = new Ramp(Rgba.Hex("#C8DCE8"), Rgba.Hex("#8EA8BC"), Rgba.Hex("#5E7890"), Rgba.Hex("#3C5068"));
            foreach (var (cx, r) in new[] { (4f, 3f), (8.5f, 4f), (12.5f, 3f) }) Heap(body, cx, 11, r, r + 1.5f, bush);
            foreach (var (x, y) in new[] { (3, 8), (6, 7), (8, 6), (10, 8), (12, 7), (13, 9) })
                body[x, y] = FrostBerry;
            body[8, 5] = FrostCore;
            body[4, 6] = SnowLie.Light;
            body[9, 4] = SnowLie.Light;
            body[12, 6] = SnowLie.Light;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Stores under snow: a crate with a smaller one on it, snow on both, a block of cut ice beside.
        static PixelImage SnowCrates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, Wood);
            Rect(body, 1, 16, 14, 1, Wood.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, Wood.Shade);
            Rect(body, 1, 20, 14, 1, Iron.Main);
            SnowOn(body, 1, 11, 14);
            Box(body, 3, 5, 9, 3, 5, DarkWood);
            Rect(body, 7, 8, 1, 4, DarkWood.Dark);
            Heap(body, 7.5f, 6, 5, 3, SnowLie);
            // A block of ice leant on it.
            Box(body, 11, 19, 5, 2, 5, ClearIce);
            body[12, 20] = FrostCore;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The sand cave ----

        // A house of the town, four tiles by three. The cottage: a flat roof walled by a low parapet, a striped awning
        // stretched over half of it, jars and a ladder's top up there; beam ends under the roof line, adobe walls, an
        // arched door under a small awning, arched windows, jars at the corner. The cabin: a mud dome with a finial over
        // the same walls.
        static PixelImage AdobeHouse(bool dome)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int eave = dome ? 44 : 46;
            if (dome)
            {
                // The dome and its finial, courses of mud brick showing faintly, a vent in it.
                var roof = new PixelImage(w, h);
                Cap(roof, w / 2f, eave, w / 2f - 5, eave - 12, Adobe);
                for (int y = 16; y < eave; y += 6)
                    for (int x = 4; x < w - 4; x++)
                        if (roof[x, y].A > 0) roof[x, y] = roof[x, y].Scale(0.9f);
                body.Draw(roof, 0, 0);
                Rect(body, 31, 7, 2, 6, Gold.Main);
                Oval(body, 32, 6.5f, 2, 2, Gold.Light);
                body[32, 4] = Gold.Light;
                Rect(body, 20, 28, 4, 5, Sandstone.Dark);
                Rect(body, 20, 28, 4, 1, Adobe.Light);
                // The shoulders the dome sits on.
                Rect(body, 3, eave - 4, w - 6, 4, Adobe.Main);
                Rect(body, 3, eave - 4, w - 6, 1, Adobe.Light);
                Rect(body, w - 9, eave - 3, 6, 3, Adobe.Shade);
            }
            else
            {
                // The flat roof seen from above: its floor, the parapet round it, crenels on the back wall.
                Rect(body, 3, 16, w - 6, eave - 16, Adobe.Main);
                for (int y = 18; y < eave - 4; y++)
                    for (int x = 6; x < w - 6; x++)
                        body[x, y] = PixelNoise.Value(x / 4, y / 3, 61) < 0.2f ? Adobe.Shade.Mix(Adobe.Main, 0.6f) : Adobe.Main.Mix(Adobe.Shade, 0.25f);
                Rect(body, 3, 14, w - 6, 4, Adobe.Light);
                Rect(body, 3, 18, w - 6, 1, Adobe.Shade);
                for (int x = 5; x < w - 8; x += 8) Rect(body, x, 11, 5, 3, Adobe.Light);
                for (int x = 5; x < w - 8; x += 8) Rect(body, x + 4, 12, 1, 2, Adobe.Main);
                Rect(body, 3, 18, 3, eave - 18, Adobe.Light);
                Rect(body, w - 6, 18, 3, eave - 18, Adobe.Shade);
                Rect(body, 3, eave - 4, w - 6, 4, Adobe.Main);
                Rect(body, 3, eave - 4, w - 6, 1, Adobe.Light);
                Rect(body, w - 9, eave - 3, 6, 3, Adobe.Shade);
                // The awning over the roof's west half: red and cream stripes on four poles.
                for (int y = 20; y < 36; y++)
                    for (int x = 8; x < 34; x++)
                    {
                        bool red = (x - 8) / 4 % 2 == 0;
                        body[x, y] = y > 33 ? (red ? AwningRed.Shade : AwningCream.Scale(0.8f)) : y < 22 ? (red ? AwningRed.Light : Rgba.White) : red ? AwningRed.Main : AwningCream;
                    }
                for (int x = 8; x < 34; x += 4) body[x + 1, 36] = (x - 8) / 4 % 2 == 0 ? AwningRed.Shade : AwningCream.Scale(0.8f);
                foreach (int px in new[] { 8, 33 }) Rect(body, px, 36, 1, 4, DarkWood.Main);
                // Jars and the top of the ladder up from the street.
                LitOval(body, 42, 27, 3.5f, 4, ClayPot);
                Rect(body, 41, 22, 3, 2, ClayPot.Shade);
                LitOval(body, 49, 30, 2.6f, 3, ClayPot);
                Rect(body, 53, 34, 1, 9, Wood.Main);
                Rect(body, 57, 34, 1, 9, Wood.Shade);
                for (int y = 36; y < 42; y += 3) Rect(body, 53, y, 5, 1, Wood.Light);
            }
            // The wall, with the beam ends under the roof line.
            int wallTop = eave, wallBottom = 69;
            for (int y = wallTop; y < wallBottom; y++)
                for (int x = 5; x < w - 5; x++)
                {
                    float t = (x - 5f) / (w - 10f);
                    var color = t > 0.9f ? Adobe.Shade : Adobe.Main;
                    if (PixelNoise.Value(x / 5, y / 4, 67) < 0.14f) color = color.Mix(Adobe.Light, 0.4f);
                    if (y > wallBottom - 4) color = color.Mix(Adobe.Shade, 0.5f);
                    body[x, y] = color;
                }
            for (int y = wallTop; y < wallTop + 3; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = Adobe.Shade.Scale(y == wallTop ? 0.72f : 0.9f);
            for (int x = 9; x < w - 8; x += 8)
            {
                Rect(body, x, wallTop + 3, 3, 3, DarkWood.Main);
                Rect(body, x, wallTop + 3, 3, 1, DarkWood.Light);
                body[x + 2, wallTop + 5] = DarkWood.Dark;
            }
            // A crack and the bricks under the plaster where it has fallen.
            Rect(body, 50, 60, 6, 2, Sandstone.Shade);
            Rect(body, 51, 62, 5, 2, Sandstone.Main);
            body[53, 61] = Sandstone.Dark;
            // The arched door, a small awning over it.
            int door = dome ? 38 : 27;
            for (int y = 52; y < 69; y++)
            {
                int inset = y < 54 ? 3 - (y - 52) : y < 56 ? 1 : 0;
                Rect(body, door - 1 + inset, y, 12 - inset * 2, 1, Sandstone.Dark);
                if (y > 52) Rect(body, door + inset, y, 10 - inset * 2, 1, TealPaint.Scale(y < 56 ? 1.1f : 0.9f));
            }
            for (int x = door + 3; x < door + 9; x += 3) Rect(body, x, 55, 1, 14, TealPaint.Scale(0.62f));
            body[door + 7, 62] = Gold.Main;
            for (int x = door - 3; x < door + 13; x++)
            {
                bool red = (x - door + 3) / 3 % 2 == 0;
                Rect(body, x, 49, 1, 3, red ? AwningRed.Main : AwningCream);
                body[x, 49] = red ? AwningRed.Light : Rgba.White;
                if ((x - door + 3) % 3 == 1) body[x, 52] = red ? AwningRed.Shade : AwningCream.Scale(0.8f);
            }
            // Arched windows, lit warm.
            foreach (int wx in dome ? new[] { 16 } : new[] { 11, 47 })
            {
                Rect(body, wx - 1, 55, 8, 9, Sandstone.Dark);
                Rect(body, wx, 54, 6, 1, Sandstone.Dark);
                Rect(body, wx, 56, 6, 7, WindowLight);
                Rect(body, wx + 1, 55, 4, 1, WindowLight);
                Rect(body, wx, 56, 2, 2, WindowCore);
                Rect(body, wx + 3, 55, 1, 8, Sandstone.Shade);
                Rect(body, wx - 2, 64, 10, 1, Adobe.Light);
            }
            // The step, jars at the corner and sand drifted against the wall.
            Rect(body, door - 2, 69, 14, 2, Sandstone.Light);
            Rect(body, door - 2, 70, 14, 1, Sandstone.Shade);
            int jars = dome ? 8 : 40;
            LitOval(body, jars + 3, 66, 3.5f, 4, ClayPot);
            Rect(body, jars + 2, 61, 3, 2, ClayPot.Shade);
            Rect(body, jars + 1, 60, 5, 1, ClayPot.Light);
            LitOval(body, jars + 9, 67.5f, 2.6f, 3, ClayPot);
            Rect(body, jars + 8, 64, 3, 1, ClayPot.Light);
            Heap(body, dome ? 56 : 8, 71, 7, 4.5f, SandDrift);
            Heap(body, dome ? 24 : 58, 71, 4, 2.5f, SandDrift);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The town's well: a ring of sandstone, a beam on two forked posts with its rope and a clay jar, a shade of
        // palm thatch over it.
        static PixelImage SandWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, Sandstone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, Sandstone.Shade);
            Rect(body, 2, 26, 16, 1, Sandstone.Shade);
            LitOval(body, 10, 22, 8, 3, Sandstone);
            Oval(body, 10, 22, 6, 1.6f, Water.Dark);
            body[8, 22] = Water.Main;
            Column(body, 3, 9, 2, 14, DarkWood);
            Column(body, 15, 9, 2, 14, DarkWood);
            // The thatch: dry fronds laid both ways from a ridge pole.
            for (int y = 3; y < 10; y++)
                for (int x = 1 - (y - 3) / 3; x < 19 + (y - 3) / 3; x++)
                    if (body.Inside(x, y)) body[x, y] = (x + y) % 3 == 0 ? Straw.Shade : y < 5 ? Straw.Light : x > 14 ? Straw.Shade : Straw.Main;
            Rect(body, 2, 3, 16, 1, DarkWood.Light);
            for (int x = 0; x < 20; x += 3) body[x, 10] = Straw.Dark;
            // The rope and the jar.
            Rect(body, 10, 10, 1, 6, Straw.Shade);
            LitOval(body, 10.5f, 18.5f, 2.6f, 2.8f, ClayPot);
            Rect(body, 9, 15, 3, 1, ClayPot.Light);
            Heap(body, 3, 32, 3, 2, SandDrift);
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A brazier on a post of carved sandstone: a bronze bowl, its fire burning gold.
        static PixelImage BrazierPost()
        {
            var body = new PixelImage(T, 42);
            Column(body, 6, 14, 4, 24, Sandstone);
            for (int y = 19; y < 36; y += 6) Rect(body, 6, y, 4, 1, Sandstone.Dark);
            Box(body, 4, 11, 8, 1, 3, Sandstone);
            Box(body, 4, 36, 8, 1, 3, Sandstone);
            // The bowl and the fire.
            Rect(body, 3, 8, 10, 3, Copper.Main);
            Rect(body, 3, 8, 10, 1, Copper.Light);
            Rect(body, 4, 10, 8, 1, Copper.Dark);
            Rect(body, 11, 9, 2, 1, Copper.Shade);
            Oval(body, 8, 5.5f, 3.4f, 3.4f, Flame);
            Oval(body, 8, 6.5f, 2, 2.2f, FlameCore);
            body[8, 1] = Flame;
            body[7, 2] = Flame;
            body[9, 3] = FlameCore;
            Heap(body, 8, 40, 5, 2.4f, SandDrift);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the town: a small stele of sandstone, an arrow and glyphs cut in it and painted, a rag tied to its top.
        static PixelImage StoneSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 4, 5, 8, 19, Sandstone);
            Rect(body, 5, 4, 6, 1, Sandstone.Light);
            Rect(body, 6, 3, 4, 1, Sandstone.Light);
            // The arrow and two glyphs.
            Rect(body, 5, 9, 5, 1, TealPaint);
            body[9, 8] = TealPaint;
            body[9, 10] = TealPaint;
            body[10, 9] = TealPaint;
            Rect(body, 6, 14, 1, 3, Sandstone.Dark);
            Rect(body, 8, 14, 2, 1, Sandstone.Dark);
            Rect(body, 8, 16, 2, 1, Sandstone.Dark);
            Rect(body, 6, 19, 4, 1, Sandstone.Dark);
            // The rag.
            Rect(body, 11, 5, 3, 2, AwningRed.Main);
            Rect(body, 12, 7, 2, 2, AwningRed.Shade);
            body[13, 9] = AwningRed.Shade;
            Heap(body, 6, 24, 5, 2.4f, SandDrift);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        // A slab of sandstone on two blocks, a striped rug over it.
        static PixelImage SlabBench()
        {
            var body = new PixelImage(T, 18);
            Box(body, 2, 11, 4, 1, 4, Sandstone);
            Box(body, 10, 11, 4, 1, 4, Sandstone);
            Box(body, 0, 7, 16, 2, 3, Sandstone);
            for (int x = 4; x < 12; x++)
            {
                bool red = (x - 4) / 2 % 2 == 0;
                Rect(body, x, 6, 1, 4, red ? AwningRed.Main : AwningCream);
                body[x, 6] = red ? AwningRed.Light : Rgba.White;
                body[x, 10] = red ? AwningRed.Shade : AwningCream.Scale(0.8f);
            }
            for (int x = 4; x < 12; x += 2) body[x, 11] = Gold.Shade;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A bed of small cacti in a trough of fired clay, one in bloom.
        static PixelImage CactusBed()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 9, 14, 2, 6, ClayPot);
            Rect(body, 2, 9, 12, 2, SandDrift.Shade);
            Rect(body, 1, 13, 14, 1, AwningCream.Scale(0.85f));
            foreach (var (x, top, wide) in new[] { (3, 5, 3), (8, 2, 3), (12, 6, 2) })
            {
                Column(body, x, top, wide, 10 - top, CactusGreen);
                body[x, top] = CactusGreen.Light;
                body[x + wide - 1, top] = CactusGreen.Shade;
            }
            Rect(body, 6, 5, 2, 1, CactusGreen.Shade);
            body[6, 4] = CactusGreen.Main;
            body[9, 1] = Rgba.Hex("#F0709A");
            body[8, 1] = Rgba.Hex("#FF9AB4");
            body[4, 4] = Rgba.Hex("#E8507A");
            body[12, 5] = Rgba.Hex("#FFE07A");
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Jars of fired clay: a tall amphora with a painted band, a squat one stoppered, a small one, a basket of dates.
        static PixelImage ClayJars()
        {
            var body = new PixelImage(T, 26);
            // The amphora behind.
            LitOval(body, 5.5f, 14, 4.5f, 7, ClayPot);
            Rect(body, 4, 4, 4, 4, ClayPot.Main);
            Rect(body, 4, 4, 1, 4, ClayPot.Light);
            Rect(body, 3, 3, 6, 1, ClayPot.Light);
            Rect(body, 2, 12, 8, 1, TealPaint);
            body[2, 7] = ClayPot.Shade;
            body[9, 7] = ClayPot.Shade;
            // The squat one, a cloth tied over its mouth.
            LitOval(body, 11, 19.5f, 4.5f, 4.5f, ClayPot);
            Rect(body, 9, 14, 5, 2, AwningCream);
            Rect(body, 9, 14, 5, 1, Rgba.White);
            Rect(body, 8, 16, 7, 1, AwningRed.Main);
            // The basket of dates before them.
            for (int y = 20; y < 25; y++)
                for (int x = 1; x < 8; x++)
                    body[x, y] = (x + y) % 2 == 0 ? Straw.Main : Straw.Shade;
            Rect(body, 1, 20, 7, 1, Straw.Light);
            foreach (var (x, y) in new[] { (2, 19), (4, 18), (5, 19), (6, 19), (3, 19) }) body[x, y] = Rgba.Hex("#B86A30");
            body[4, 18] = Rgba.Hex("#E09A50");
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The landmarks ----

        // A beast frozen in a block of ice, three tiles by two: a great rough block, clear enough that the dark shape in
        // it shows (a horned back, a lowered head, a foreleg), cracks through it, snow on its top and round its foot.
        static PixelImage BeastInIce()
        {
            const int w = 48, h = 58;
            var body = new PixelImage(w, h);
            // The block: an uneven six-sided mass, its top face lighter.
            for (int y = 4; y < 54; y++)
            {
                int inset = y < 14 ? (14 - y) * 2 / 3 : y > 48 ? (y - 48) / 2 : 0;
                int lean = y < 30 ? 1 : 0;
                for (int x = 3 + inset + lean; x < w - 3 - inset; x++)
                {
                    float t = (x - 3f) / (w - 6f);
                    var color = y < 12 ? ClearIce.Light : t < 0.14f ? ClearIce.Light : t < 0.7f ? ClearIce.Main : t < 0.9f ? ClearIce.Shade : ClearIce.Dark;
                    body[x, y] = color;
                }
            }
            // The facets' edges.
            Line(body, 12, 5, 9, 53, ClearIce.Light);
            Line(body, 36, 6, 39, 53, ClearIce.Shade);
            Rect(body, 10, 12, 28, 1, FrostCore);
            // The beast inside, seen through the ice in two tones of deep blue: in profile, its head low to the left with
            // jaws apart and a horn swept back, spines down its back, a foreleg and a haunch, the tail curled up.
            Oval(body, 27, 30, 11, 7, BeastShade);
            Oval(body, 35, 32, 6, 7.5f, BeastShade);
            Rect(body, 15, 25, 9, 8, BeastShade);
            Oval(body, 14, 27, 5, 4.5f, BeastShade);
            Rect(body, 7, 25, 6, 3, BeastShade);
            Rect(body, 8, 30, 5, 2, BeastShade);
            Rect(body, 20, 34, 4, 12, BeastShade);
            Rect(body, 18, 45, 6, 2, BeastShade);
            Rect(body, 34, 36, 5, 10, BeastShade);
            Rect(body, 32, 45, 7, 2, BeastShade);
            Oval(body, 28, 32, 8, 4, BeastDeep);
            Rect(body, 25, 36, 3, 8, BeastDeep);
            foreach (var (x, y) in new[] { (20, 22), (24, 21), (28, 21), (32, 22), (36, 24) })
            {
                body[x, y] = BeastShade;
                body[x, y + 1] = BeastShade;
                body[x + 1, y + 1] = BeastShade;
                body[x - 1, y + 2] = BeastShade;
                body[x, y + 2] = BeastShade;
                body[x + 1, y + 2] = BeastShade;
            }
            // The horn, the tail and the eye, which still catches the light.
            Line(body, 15, 23, 20, 17, BeastDeep);
            Line(body, 16, 23, 21, 17, BeastShade);
            Line(body, 16, 24, 21, 18, BeastShade);
            Line(body, 40, 30, 42, 24, BeastShade);
            Line(body, 41, 30, 43, 24, BeastShade);
            Line(body, 42, 24, 40, 20, BeastShade);
            body[13, 26] = FrostBerry;
            body[12, 26] = FrostCore;
            // Light on the ice over it: streaks down the face, a crack from the top.
            for (int y = 16; y < 46; y++)
            {
                if (y % 7 < 4) body[19 + y / 9, y] = body[19 + y / 9, y].Mix(FrostCore, 0.55f);
                if (y % 9 < 3) body[33, y] = body[33, y].Mix(FrostCore, 0.4f);
            }
            Line(body, 26, 4, 24, 12, ClearIce.Dark);
            Line(body, 24, 12, 27, 17, ClearIce.Dark);
            Line(body, 40, 30, 43, 38, ClearIce.Dark);
            // Snow on its top and drifted round its foot.
            Heap(body, 22, 6, 13, 4, SnowLie);
            Heap(body, 35, 7, 6, 2.5f, SnowLie);
            Heap(body, 8, 55, 8, 5, SnowLie);
            Heap(body, 38, 55, 9, 4, SnowLie);
            Heap(body, 24, 55, 6, 2.5f, SnowLie);
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // The front of a temple the sand took, five tiles wide against the rock: four columns under a carved lintel and
        // a broken pediment with a sun disc, the doorway between the middle two choked with sand, one column snapped,
        // drifts over its steps.
        static PixelImage BuriedTemple()
        {
            const int w = 80, h = 60;
            var body = new PixelImage(w, h);
            // The wall behind the columns, in shade, and the doorway.
            Rect(body, 6, 22, 68, 34, Sandstone.Shade);
            for (int y = 24; y < 56; y += 6) Rect(body, 6, y, 68, 1, Sandstone.Dark);
            Rect(body, 31, 26, 18, 30, Hollow);
            Rect(body, 30, 25, 20, 1, Sandstone.Dark);
            // The pediment: a low triangle, its right corner broken away, the sun disc in it.
            for (int y = 2; y < 14; y++)
            {
                int half = (y - 1) * 3;
                int left = Math.Max(4, w / 2 - half), right = Math.Min(w - 4, w / 2 + half);
                if (y < 12) right = Math.Min(right, 56 + y);
                for (int x = left; x < right; x++) body[x, y] = y == 2 || x == left ? Sandstone.Light : x > 54 ? Sandstone.Shade : Sandstone.Main;
            }
            Oval(body, 40, 9, 4, 3.4f, Gold.Main);
            Oval(body, 39.5f, 8.5f, 2, 1.8f, Gold.Light);
            foreach (var (x, y) in new[] { (33, 9), (47, 9), (40, 4), (35, 6), (45, 6) }) body[x, y] = Gold.Shade;
            // The lintel: a light top edge, a frieze of glyphs, a dark underside.
            Rect(body, 3, 14, 74, 8, Sandstone.Main);
            Rect(body, 3, 14, 74, 1, Sandstone.Light);
            Rect(body, 3, 21, 74, 1, Sandstone.Dark);
            Rect(body, 70, 15, 7, 6, Sandstone.Shade);
            for (int x = 7; x < 72; x += 5)
            {
                uint glyph = PixelNoise.Hash(x, 3, 97) % 4;
                var cut = Sandstone.Dark;
                if (glyph == 0) Rect(body, x, 16, 1, 4, cut);
                else if (glyph == 1) Rect(body, x, 17, 3, 1, cut);
                else if (glyph == 2)
                {
                    Rect(body, x, 16, 3, 1, cut);
                    Rect(body, x, 19, 3, 1, cut);
                }
                else
                {
                    body[x, 16] = cut;
                    body[x + 1, 17] = cut;
                    body[x + 2, 18] = cut;
                    body[x + 1, 19] = TealPaint;
                }
            }
            // The columns: fluted shafts with capitals; the third snapped half way.
            int[] posts = { 8, 24, 49, 65 };
            for (int i = 0; i < posts.Length; i++)
            {
                int x = posts[i], top = i == 2 ? 38 : 22;
                Column(body, x, top, 7, 56 - top, Sandstone);
                Rect(body, x + 2, top + 1, 1, 55 - top, Sandstone.Light.Mix(Sandstone.Main, 0.5f));
                Rect(body, x + 4, top + 1, 1, 55 - top, Sandstone.Shade);
                if (i == 2)
                {
                    Rect(body, x, top - 1, 4, 1, Sandstone.Light);
                    Rect(body, x, top - 2, 2, 1, Sandstone.Light);
                }
                else
                {
                    Rect(body, x - 1, 22, 9, 2, Sandstone.Light);
                    Rect(body, x - 1, 24, 9, 1, Sandstone.Dark);
                }
                for (int y = top + 9; y < 54; y += 10) Rect(body, x, y, 7, 1, Sandstone.Dark);
            }
            // The snapped column's top lying in the sand before it.
            Rect(body, 52, 51, 10, 4, Sandstone.Main);
            Rect(body, 52, 51, 10, 1, Sandstone.Light);
            Rect(body, 61, 52, 1, 3, Sandstone.Dark);
            // The sand: in the doorway, over the steps, heaped at each end.
            Heap(body, 40, 57, 11, 14, SandDrift);
            Heap(body, 12, 58, 13, 7, SandDrift);
            Heap(body, 68, 58, 12, 9, SandDrift);
            Heap(body, 30, 58, 8, 4, SandDrift);
            Heap(body, 54, 58, 8, 3.5f, SandDrift);
            // A step showing between the drifts.
            Rect(body, 22, 55, 6, 2, Sandstone.Light);
            Rect(body, 22, 56, 6, 1, Sandstone.Shade);
            return Finish(body, ShadowLayer(w, h, w / 2f, h - 2.5f, w / 2f - 1, 2));
        }

        // A giant fossil skull, three tiles by two, sunk to its jaw in the sand: a domed cranium, a deep socket, a nasal
        // hollow, a row of teeth as long as a hand, a horn swept back.
        static PixelImage FossilSkull()
        {
            const int w = 48, h = 46;
            var body = new PixelImage(w, h);
            // The horn behind the cranium, swept back to the left.
            for (float t = 0; t <= 1; t += 0.02f)
            {
                int x = (int)MathF.Round(16 - 13 * t), y = (int)MathF.Round(14 - 12 * t + 6 * t * t);
                int thick = 4 - (int)(t * 3);
                for (int k = 0; k < thick; k++) body[x, y + k] = k == 0 ? FossilBone.Light : k == thick - 1 ? FossilBone.Shade : FossilBone.Main;
            }
            // The cranium and the long snout.
            LitOval(body, 19, 22, 14, 13, FossilBone);
            LitOval(body, 33, 28, 12, 9, FossilBone);
            Rect(body, 30, 34, 15, 4, FossilBone.Main);
            Rect(body, 43, 30, 2, 8, FossilBone.Shade);
            // The brow ridge over the socket, the socket, the nasal hollow.
            Line(body, 12, 18, 26, 15, FossilBone.Light);
            Oval(body, 19, 24, 5.5f, 5, Hollow);
            Oval(body, 18.5f, 23.5f, 3.5f, 3, Rgba.Hex("#2A1E16"));
            Oval(body, 39, 26, 3, 2.4f, Hollow);
            // Sutures and the age in it.
            Line(body, 26, 11, 28, 20, FossilBone.Shade);
            Line(body, 28, 20, 34, 22, FossilBone.Shade);
            Line(body, 9, 28, 14, 33, FossilBone.Dark);
            foreach (var (x, y) in new[] { (22, 13), (15, 14), (31, 25), (36, 31), (24, 32) }) body[x, y] = FossilBone.Shade;
            // The teeth along the jaw.
            for (int x = 27; x < 45; x += 4)
            {
                Rect(body, x, 38, 3, 3, FossilBone.Light);
                Rect(body, x + 2, 38, 1, 3, FossilBone.Shade);
                body[x + 1, 41] = FossilBone.Main;
            }
            Rect(body, 26, 37, 19, 1, FossilBone.Dark);
            // The sand it lies in.
            Heap(body, 11, 43, 11, 7, SandDrift);
            Heap(body, 30, 43, 9, 3, SandDrift);
            Heap(body, 43, 43, 5, 2.4f, SandDrift);
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }
    }
}
