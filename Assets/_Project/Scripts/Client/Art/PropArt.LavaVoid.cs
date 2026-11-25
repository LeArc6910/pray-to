using System;
using Game.Core;

namespace Game.Client
{
    // The places' buildings and dressing in the lava zone's and the void abyss's mood, and those two biomes' landmarks
    // (Docs/BiomeRework.md 묶음 B4a; PropArt.Places.cs has the first two biomes' and how a look is chosen).
    //  - The lava zone, a forge town: houses of basalt block bound with iron under roofs of riveted plate, a brick
    //    chimney alight on each, braziers for lamps, troughs of fire peppers.
    //  - The abyss, a ruin that did not fall: houses of violet-black stone whose roofs have come apart and hang where
    //    they were, runes that still glow, obelisks for lamps, slabs that float for benches and signs.
    // The same tiles, footprints, picture sizes and roof rows as the old drawings.
    static partial class PropArt
    {
        static readonly Ramp BasaltBlock = new Ramp(Rgba.Hex("#8A7A74"), Rgba.Hex("#5E504C"), Rgba.Hex("#423634"), Rgba.Hex("#2A2020"));
        static readonly Ramp ForgePlate = new Ramp(Rgba.Hex("#8E8684"), Rgba.Hex("#5E5656"), Rgba.Hex("#403A3C"), Rgba.Hex("#28242A"));
        static readonly Ramp ForgeBrick = new Ramp(Rgba.Hex("#C87A5A"), Rgba.Hex("#9A5038"), Rgba.Hex("#6E3426"), Rgba.Hex("#48201A"));
        static readonly Ramp Coal = new Ramp(Rgba.Hex("#5A5258"), Rgba.Hex("#38323A"), Rgba.Hex("#241F28"), Rgba.Hex("#141018"));
        static readonly Rgba ForgeLight = Rgba.Hex("#FF7A30"), ForgeCore = Rgba.Hex("#FFD86A"), ForgeHot = Rgba.Hex("#FFF2B8"), ForgeDeep = Rgba.Hex("#D8401A"), PepperRed = Rgba.Hex("#E03A2A");

        static readonly Ramp RuinStone = new Ramp(Rgba.Hex("#8E7EBE"), Rgba.Hex("#5C4C86"), Rgba.Hex("#3C2F5E"), Rgba.Hex("#221A3A"));
        static readonly Ramp RuinDark = new Ramp(Rgba.Hex("#64568E"), Rgba.Hex("#403264"), Rgba.Hex("#2A2046"), Rgba.Hex("#18122C"));
        static readonly Ramp VoidShard = new Ramp(Rgba.Hex("#E2C8FF"), Rgba.Hex("#A050FF"), Rgba.Hex("#6A30C0"), Rgba.Hex("#3A1870"));
        static readonly Rgba VoidLight = Rgba.Hex("#A050FF"), StarPale = Rgba.Hex("#E6D6FF"), StarCore = Rgba.Hex("#F6F0FF"), VoidDeep = Rgba.Hex("#08051A");

        public const int LavaLook = 7, AbyssLook = 8;

        static PixelImage LavaVoidPlaceProp(TileType type, int look)
        {
            bool lava = look == LavaLook;
            return type switch
            {
                TileType.Cottage => lava ? ForgeHouse(false) : RuinHouse(false),
                TileType.Cabin => lava ? ForgeHouse(true) : RuinHouse(true),
                TileType.Well => lava ? QuenchWell() : VoidWell(),
                TileType.LampPost => lava ? ForgeBrazier() : ObeliskLamp(),
                TileType.Signpost => lava ? IronSign() : RuneSign(),
                TileType.Bench => lava ? BasaltBench() : HoverBench(),
                TileType.Flowerbed => lava ? PepperTrough() : StarBowl(),
                TileType.Crates => lava ? CoalCrates() : VoidCoffers(),
                _ => null,
            };
        }

        static int LavaVoidLandmarkCanopy(TileType type) => type switch
        {
            TileType.FallenChimney => 22,
            TileType.GiantAnvil => 20,
            TileType.BrokenGate => 34,
            TileType.StarAltar => 16,
            _ => 0,
        };

        static PixelImage LavaVoidLandmark(TileType type) => type switch
        {
            TileType.FallenChimney => ChimneyRuin(),
            TileType.GiantAnvil => GreatAnvil(),
            TileType.BrokenGate => GateRuin(),
            TileType.StarAltar => StarDais(),
            _ => null,
        };

        // ---- Brushes ----

        // A flame: a teardrop, deep at its edge, pale at its heart.
        static void Fire(PixelImage image, float cx, float baseY, float half, float tall)
        {
            for (int y = (int)(baseY - tall); y <= (int)baseY; y++)
            {
                float t = (baseY - y) / tall;
                float w = half * MathF.Sqrt(MathF.Max(0, 1 - t)) * (0.55f + 0.45f * MathF.Min(1, t * 4 + 0.4f));
                float lean = MathF.Sin(t * 4.2f) * 0.7f;
                for (int x = (int)MathF.Floor(cx + lean - w); x <= (int)MathF.Ceiling(cx + lean + w); x++)
                {
                    float u = MathF.Abs(x + 0.5f - cx - lean) / MathF.Max(0.6f, w);
                    if (u > 1) continue;
                    image[x, y] = u < 0.3f && t < 0.5f ? ForgeHot : u < 0.6f && t < 0.75f ? ForgeCore : t > 0.8f ? ForgeDeep : ForgeLight;
                }
            }
        }

        // Courses of block from row top to row bottom between two columns: each block lit along its top, the joints
        // dark, the right end in shade.
        static void Blocks(PixelImage image, int left, int right, int top, int bottom, Ramp ramp, int deep, int wide, int salt)
        {
            for (int y = top; y < bottom; y++)
            {
                int course = (y - top) / deep, row = (y - top) % deep;
                for (int x = left; x < right; x++)
                {
                    int along = (x - left + course % 2 * (wide / 2)) % wide;
                    float tone = PixelNoise.Value((x - left + course % 2 * (wide / 2)) / wide, course, salt);
                    var color = row == deep - 1 || along == wide - 1 ? ramp.Dark : row == 0 ? ramp.Light : tone > 0.72f ? ramp.Main.Mix(ramp.Light, 0.3f) : tone < 0.2f ? ramp.Shade : ramp.Main;
                    if (x > right - 5) color = row == deep - 1 ? ramp.Dark : ramp.Shade;
                    image[x, y] = color;
                }
            }
        }

        // A roof of riveted plate from row top to row bottom: wide plates in three rows, a pale lip along each one's
        // lower edge, rivets at their corners, a patch of rust here and there.
        static void PlateRoof(PixelImage image, int w, int top, int bottom, int salt)
        {
            int tall = bottom - top;
            for (int y = top; y < bottom; y++)
            {
                int inset = Math.Max(0, (12 - (y - top)) / 3);
                int course = (y - top) * 3 / tall, row = (y - top) - course * tall / 3;
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    int along = (x + course % 2 * 7) / 14, edge = (x + course % 2 * 7) % 14;
                    float t = (x - 2f) / (w - 4f);
                    float tone = PixelNoise.Value(along, course, salt);
                    var color = t > 0.84f ? ForgePlate.Shade : y < top + 3 ? ForgePlate.Light : tone > 0.7f ? ForgePlate.Main.Mix(ForgePlate.Light, 0.35f) : tone < 0.2f ? ForgePlate.Shade : ForgePlate.Main;
                    if (row == tall / 3 - 1) color = ForgePlate.Dark;
                    else if (edge == 0) color = ForgePlate.Dark;
                    else if (row == tall / 3 - 2) color = color.Mix(ForgePlate.Light, 0.35f);
                    else if ((edge == 2 || edge == 12) && row == 1) color = ForgePlate.Light.Mix(Rgba.White, 0.2f);
                    else if (tone < 0.16f && PixelNoise.Hash(x, y, salt + 3) % 6 == 0) color = Rgba.Hex("#7A4A34");
                    image[x, y] = color;
                }
            }
            Rect(image, 2, bottom - 1, w - 4, 2, ForgePlate.Dark);
        }

        // ---- The lava zone: a forge town ----

        // A forge house, four tiles by three: a roof of riveted plate with a brick chimney alight, walls of basalt
        // block bound in iron (the cabin's of dark brick), an iron door with the fire showing round it, a brazier by
        // the door, windows hot orange, an anvil at the corner.
        static PixelImage ForgeHouse(bool cabin)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = cabin ? 14 : 12, eave = cabin ? 44 : 46;
            // The chimney, behind the roof: brick, banded, its mouth alight.
            int stack = cabin ? 10 : 44;
            Blocks(body, stack, stack + 10, 2, roofTop + 10, ForgeBrick, 3, 5, 31);
            Rect(body, stack - 1, 1, 12, 2, BasaltBlock.Main);
            Rect(body, stack - 1, 1, 12, 1, BasaltBlock.Light);
            Rect(body, stack + 1, 0, 8, 1, ForgeLight);
            Rect(body, stack + 3, 0, 4, 1, ForgeCore);
            Rect(body, stack, 8, 10, 1, Iron.Shade);
            PlateRoof(body, w, roofTop, eave, cabin ? 9 : 6);
            // The chimney's foot on the plates.
            Blocks(body, stack, stack + 10, roofTop + 6, roofTop + 14, ForgeBrick, 3, 5, 33);
            Rect(body, stack, roofTop + 14, 10, 1, ForgePlate.Dark);
            // The ridge: a beam of iron.
            Rect(body, 6, roofTop, w - 12, 2, Iron.Main);
            Rect(body, 6, roofTop, w - 12, 1, Iron.Light);
            Blocks(body, stack, stack + 10, roofTop - 2, roofTop + 4, ForgeBrick, 3, 5, 31);
            int wallTop = eave + 1, wallBottom = 68;
            if (cabin) Blocks(body, 5, w - 5, wallTop, wallBottom, ForgeBrick, 4, 8, 37);
            else Blocks(body, 5, w - 5, wallTop, wallBottom, BasaltBlock, 6, 10, 35);
            for (int y = wallTop; y < wallTop + 2; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = body[x, y].Scale(y == wallTop ? 0.7f : 0.86f);
            // Iron straps up the corners.
            foreach (int post in new[] { 5, w - 8 })
            {
                Rect(body, post, wallTop, 3, wallBottom - wallTop, Iron.Shade);
                Rect(body, post, wallTop, 1, wallBottom - wallTop, Iron.Main);
                for (int y = wallTop + 3; y < wallBottom; y += 6) body[post + 1, y] = Iron.Light;
            }
            // The door: iron plate, riveted, the forge's light at its edges.
            int door = cabin ? 38 : 27;
            Rect(body, door - 2, 50, 14, 2, BasaltBlock.Light);
            Rect(body, door - 2, 51, 14, 1, BasaltBlock.Shade);
            Rect(body, door - 1, 52, 12, 16, ForgeLight);
            Rect(body, door, 52, 10, 16, Iron.Shade);
            Rect(body, door, 52, 10, 1, ForgeCore);
            Rect(body, door + 5, 53, 1, 15, Iron.Dark);
            Rect(body, door, 57, 10, 1, Iron.Main);
            Rect(body, door, 63, 10, 1, Iron.Main);
            foreach (var (x, y) in new[] { (1, 55), (8, 55), (1, 61), (8, 61), (1, 66), (8, 66) }) body[door + x, y] = Iron.Light;
            // The brazier by the door: a bowl on a stem, alight.
            int lamp = door - 8;
            Fire(body, lamp + 2.5f, 58, 2.6f, 7);
            Rect(body, lamp - 1, 58, 7, 2, Iron.Main);
            Rect(body, lamp - 1, 58, 7, 1, Iron.Light);
            Rect(body, lamp, 60, 5, 1, Iron.Shade);
            Rect(body, lamp + 2, 61, 1, 7, Iron.Shade);
            Rect(body, lamp + 1, 67, 3, 1, Iron.Dark);
            // Windows: small, barred, the forge's glow inside.
            foreach (int wx in cabin ? new[] { 13 } : new[] { 10, 45 })
            {
                int ww = cabin ? 12 : 9;
                Rect(body, wx, 51, ww, 8, Iron.Dark);
                Rect(body, wx + 1, 52, ww - 2, 6, ForgeLight);
                Rect(body, wx + 1, 55, ww - 2, 3, ForgeCore);
                Rect(body, wx + 2, 56, 3, 2, ForgeHot);
                for (int x = wx + 3; x < wx + ww - 1; x += 3) Rect(body, x, 52, 1, 6, Iron.Dark);
                Rect(body, wx - 1, 59, ww + 2, 1, BasaltBlock.Light);
            }
            // The footing, and what stands against it: an anvil on its block, a heap of coal.
            Rect(body, 4, 68, w - 8, 3, BasaltBlock.Shade);
            for (int x = 5; x < w - 6; x += 7) Rect(body, x, 68, 6, 1, BasaltBlock.Main);
            int corner = cabin ? 51 : 7;
            Rect(body, corner + 1, 66, 7, 5, BasaltBlock.Main);
            Rect(body, corner + 1, 66, 7, 1, BasaltBlock.Light);
            Rect(body, corner, 61, 9, 2, Iron.Light);
            Rect(body, corner - 2, 62, 3, 1, Iron.Main);
            Rect(body, corner + 2, 63, 5, 3, Iron.Shade);
            Rect(body, corner, 62, 9, 1, Iron.Main);
            int heap = cabin ? 9 : 55;
            LitOval(body, heap, 69, 4, 2.6f, Coal);
            body[heap - 1, 68] = ForgeLight;
            body[heap + 1, 69] = ForgeDeep;
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The town's well is its quenching trough: a ring of basalt hooped in iron, dark water with the fire's light on
        // it, two iron posts and a bar, a pail on its chain.
        static PixelImage QuenchWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 22, 16, 9, BasaltBlock);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, BasaltBlock.Shade);
            Rect(body, 2, 25, 16, 1, Iron.Main);
            Rect(body, 2, 29, 16, 1, Iron.Shade);
            LitOval(body, 10, 22, 8, 3, BasaltBlock);
            Oval(body, 10, 22, 6, 1.6f, Rgba.Hex("#1A2630"));
            body[7, 22] = ForgeLight;
            body[8, 21] = ForgeCore;
            body[12, 22] = ForgeDeep;
            // The posts and the bar.
            foreach (int x in new[] { 3, 16 })
            {
                Rect(body, x, 6, 2, 16, Iron.Shade);
                Rect(body, x, 6, 1, 16, Iron.Main);
            }
            Rect(body, 1, 5, 18, 2, Iron.Main);
            Rect(body, 1, 5, 18, 1, Iron.Light);
            // The chain and the pail.
            for (int y = 7; y < 16; y++) body[10, y] = y % 2 == 0 ? Iron.Light : Iron.Shade;
            Box(body, 8, 16, 5, 1, 4, Iron);
            // A wisp of steam.
            foreach (var (x, y) in new[] { (12, 18), (13, 15), (12, 12) }) body.Blend(x, y, new Rgba(230, 226, 220, 110));
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lamp of the town: an iron post, and on it a brazier alight.
        static PixelImage ForgeBrazier()
        {
            var body = new PixelImage(T, 42);
            Rect(body, 7, 15, 2, 23, Iron.Shade);
            Rect(body, 7, 15, 1, 23, Iron.Main);
            for (int y = 20; y < 38; y += 6) Rect(body, 6, y, 4, 1, Iron.Dark);
            // The bowl and the fire in it.
            Fire(body, 8, 11, 3.6f, 10);
            Rect(body, 3, 11, 10, 2, Iron.Main);
            Rect(body, 3, 11, 10, 1, Iron.Light);
            Rect(body, 4, 13, 8, 1, Iron.Shade);
            Rect(body, 5, 14, 6, 1, Iron.Dark);
            body[4, 12] = ForgeLight;
            body[8, 12] = ForgeLight;
            body[11, 12] = ForgeDeep;
            // Its foot: three toes of iron.
            Rect(body, 4, 38, 8, 2, Iron.Shade);
            Rect(body, 4, 38, 8, 1, Iron.Main);
            body[3, 39] = Iron.Dark;
            body[12, 39] = Iron.Dark;
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the town: a plate of iron hung from a basalt post, a hammer struck into it and still glowing.
        static PixelImage IronSign()
        {
            var body = new PixelImage(T, 26);
            Column(body, 3, 3, 3, 21, BasaltBlock);
            Rect(body, 3, 5, 11, 1, Iron.Main);
            body[7, 6] = Iron.Shade;
            body[12, 6] = Iron.Shade;
            Rect(body, 6, 7, 8, 9, Iron.Shade);
            Rect(body, 6, 7, 8, 1, Iron.Light);
            Rect(body, 6, 7, 1, 9, Iron.Main);
            Rect(body, 6, 15, 8, 1, Iron.Dark);
            // The hammer: a head and a haft.
            Rect(body, 8, 9, 4, 2, ForgeLight);
            body[8, 9] = ForgeCore;
            Rect(body, 10, 11, 1, 3, ForgeDeep);
            Rect(body, 2, 23, 5, 1, BasaltBlock.Dark);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 5, 1.5f));
        }

        // A bench: a slab of basalt across two blocks of iron.
        static PixelImage BasaltBench()
        {
            var body = new PixelImage(T, 18);
            Rect(body, 2, 10, 3, 6, Iron.Shade);
            Rect(body, 2, 10, 1, 6, Iron.Main);
            Rect(body, 11, 10, 3, 6, Iron.Shade);
            Rect(body, 11, 10, 1, 6, Iron.Main);
            Box(body, 0, 6, 16, 3, 2, BasaltBlock);
            Rect(body, 1, 7, 5, 1, BasaltBlock.Light.Mix(Rgba.White, 0.2f));
            // A seam still warm across it.
            body[9, 7] = ForgeDeep;
            body[10, 8] = ForgeLight;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A trough of iron where fire peppers are grown in ash.
        static PixelImage PepperTrough()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 10, 14, 2, 5, Iron);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 12, 1, 5, Iron.Dark);
            Rect(body, 2, 10, 12, 1, Coal.Main);
            foreach (var (x, top) in new[] { (3, 5), (6, 3), (9, 6), (12, 4) })
            {
                Rect(body, x, top + 1, 1, 10 - top - 1, Rgba.Hex("#5E7A3A"));
                body[x - 1, top + 2] = Rgba.Hex("#7A9A48");
                body[x, top] = PepperRed;
                body[x + 1, top + 1] = PepperRed;
                body[x + 1, top + 2] = Rgba.Hex("#8E1E1A");
            }
            body[6, 3] = ForgeCore;
            body[12, 4] = ForgeLight;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Stores: an iron-bound crate, a box of coal on it, ingots stacked beside.
        static PixelImage CoalCrates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, DarkWood);
            Rect(body, 1, 16, 14, 1, DarkWood.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, Iron.Shade);
            Rect(body, 1, 20, 14, 1, Iron.Main);
            Box(body, 2, 6, 9, 2, 5, Iron);
            // The coal heaped in it, one lump alight.
            LitOval(body, 6.5f, 5.5f, 4, 2.4f, Coal);
            body[5, 5] = ForgeLight;
            body[7, 4] = Coal.Light;
            // Ingots.
            Rect(body, 11, 9, 4, 2, Rgba.Hex("#B8BCC8"));
            Rect(body, 11, 9, 4, 1, Rgba.Hex("#E4E8F0"));
            Rect(body, 12, 7, 3, 2, Rgba.Hex("#9AA0AE"));
            Rect(body, 12, 7, 3, 1, Rgba.Hex("#D0D4DE"));
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The abyss: a ruin that did not fall ----

        // The dark between two pieces of something broken: the void, a star in it here and there.
        static void Gap(PixelImage image, int x, int y, int w, int h, int salt)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    uint roll = PixelNoise.Hash(x + i, y + j, salt) % 23;
                    image[x + i, y + j] = roll == 0 ? StarPale : roll == 1 ? VoidLight.Scale(0.7f) : VoidDeep;
                }
        }

        // A ruin house, four tiles by three: a roof of stone slabs that has broken into pieces which hang a little
        // apart (the void and its stars between them), walls of fitted violet-black stone with a band of runes that
        // glows, an arched door full of violet light, a crystal hung in a niche beside it, the wall's foot crumbling
        // into pieces that do not fall.
        static PixelImage RuinHouse(bool cabin)
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            int roofTop = cabin ? 14 : 12, eave = cabin ? 44 : 46;
            // The roof: slabs in three courses, lit along each one's lower lip.
            int tall = eave - roofTop;
            for (int y = roofTop; y < eave; y++)
            {
                int inset = Math.Max(0, (12 - (y - roofTop)) / 3);
                int course = (y - roofTop) * 3 / tall, row = (y - roofTop) - course * tall / 3;
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    int along = (x + course % 2 * 6) / 12, edge = (x + course % 2 * 6) % 12;
                    float t = (x - 2f) / (w - 4f);
                    float tone = PixelNoise.Value(along, course, cabin ? 43 : 41);
                    var color = t > 0.84f ? RuinDark.Shade : y < roofTop + 3 ? RuinStone.Light : tone > 0.7f ? RuinStone.Main : tone < 0.2f ? RuinDark.Shade : RuinDark.Main;
                    if (row == tall / 3 - 1 || edge == 0) color = RuinDark.Dark;
                    else if (row == tall / 3 - 2) color = color.Mix(RuinStone.Light, 0.35f);
                    body[x, y] = color;
                }
            }
            Rect(body, 2, eave - 1, w - 4, 2, RuinDark.Dark);
            // Where it broke: a rift through the roof and a corner gone adrift.
            int rift = cabin ? 40 : 20;
            for (int y = roofTop; y < eave - 3; y++)
            {
                int x = rift + (y - roofTop) / 4 + (int)(PixelNoise.Value(y, 1, 45) * 2);
                Gap(body, x, y, 3, 1, 47);
                body[x - 1, y] = RuinStone.Light;
                body[x + 3, y] = RuinDark.Dark;
            }
            int corner = cabin ? 4 : w - 14;
            for (int y = roofTop; y < roofTop + 11; y++)
                for (int x = corner; x < corner + 10; x++)
                    body[x, y] = Rgba.Clear;
            // The piece that came off, hanging over where it was.
            int px = cabin ? 3 : w - 12, py = roofTop - 8;
            Rect(body, px, py, 9, 6, RuinDark.Main);
            Rect(body, px, py, 9, 1, RuinStone.Light);
            Rect(body, px, py + 5, 9, 1, RuinDark.Dark);
            Rect(body, px + 7, py + 1, 2, 4, RuinDark.Shade);
            body[px + 3, py + 7] = RuinStone.Main;
            body[px + 6, py + 8] = RuinDark.Main;
            body[px + 1, py + 9] = RuinDark.Shade;
            // A star-crystal on the ridge.
            int gem = cabin ? 28 : 36;
            Shard(body, gem, roofTop + 1, 2.2f, gem, roofTop - 7, VoidShard);
            body[gem, roofTop - 4] = StarCore;
            int wallTop = eave + 1, wallBottom = 68;
            Blocks(body, 5, w - 5, wallTop, wallBottom, cabin ? RuinDark : RuinStone, cabin ? 5 : 6, cabin ? 8 : 10, 49);
            for (int y = wallTop; y < wallTop + 2; y++)
                for (int x = 5; x < w - 5; x++)
                    body[x, y] = body[x, y].Scale(y == wallTop ? 0.7f : 0.86f);
            // The band of runes under the eave.
            for (int x = 8; x < w - 8; x += 3)
            {
                uint glyph = PixelNoise.Hash(x, cabin ? 2 : 1, 51) % 4;
                body[x, wallTop + 3] = glyph == 0 ? StarPale : VoidLight;
                if (glyph != 1) body[x, wallTop + 4] = VoidLight.Scale(0.8f);
                if (glyph == 2) body[x + 1, wallTop + 3] = VoidLight;
            }
            // The door: an arch, the void's light in it.
            int door = cabin ? 38 : 27;
            Rect(body, door - 1, 53, 12, 15, RuinStone.Light);
            Rect(body, door + 1, 51, 8, 2, RuinStone.Light);
            Rect(body, door, 54, 10, 14, VoidDeep);
            Rect(body, door + 2, 52, 6, 2, VoidDeep);
            for (int y = 53; y < 68; y++)
                for (int x = door + 1; x < door + 9; x++)
                {
                    float d = MathF.Abs(x - door - 4.5f) / 4f + (67 - y) / 26f;
                    if (d < 0.75f) body[x, y] = VoidLight.Mix(VoidDeep, d * 1.1f);
                }
            body[door + 4, 62] = StarPale;
            body[door + 6, 58] = StarPale;
            body[door + 3, 56] = VoidLight;
            // The niche by the door and the crystal hung in it.
            int lamp = door - 7;
            Rect(body, lamp, 51, 5, 8, RuinDark.Dark);
            Shard(body, lamp + 2.5f, 53, 1.6f, lamp + 2.5f, 51, VoidShard);
            Shard(body, lamp + 2.5f, 53, 1.6f, lamp + 2.5f, 58, VoidShard);
            body[lamp + 2, 54] = StarCore;
            // Windows: tall slits, starlit.
            foreach (int wx in cabin ? new[] { 13, 22 } : new[] { 11, 17, 46, 52 })
            {
                Rect(body, wx - 1, 51, 5, 9, RuinDark.Dark);
                Rect(body, wx, 52, 3, 7, VoidLight.Mix(VoidDeep, 0.45f));
                body[wx + 1, 54] = StarPale;
                body[wx, 57] = VoidLight;
                Rect(body, wx - 1, 60, 5, 1, RuinStone.Light);
            }
            // The footing: crumbled away in places, the pieces a little below where they were.
            Rect(body, 4, 68, w - 8, 3, RuinDark.Shade);
            for (int x = 5; x < w - 6; x += 7) Rect(body, x, 68, 6, 1, RuinDark.Main);
            foreach (int gx in cabin ? new[] { 8, 30, 52 } : new[] { 12, 44, 54 })
            {
                Rect(body, gx, 66, 5, 5, Rgba.Clear);
                Gap(body, gx, 66, 5, 3, 53);
                Rect(body, gx + 1, 70, 3, 2, RuinDark.Main);
                body[gx + 1, 70] = RuinStone.Main;
            }
            // Crystals grown at the corner.
            int grown = cabin ? 56 : 5;
            Shard(body, grown + 2, 71, 2.2f, grown + 1, 61, VoidShard);
            Shard(body, grown + 5, 71, 1.8f, grown + 7, 65, VoidShard);
            body[grown + 1, 64] = StarCore;
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The ruin's well: a ring of stone whose upper course floats a hand above the lower, the void and its stars
        // inside, shards circling over it.
        static PixelImage VoidWell()
        {
            const int w = T + 4, h = 34;
            var body = new PixelImage(w, h);
            Column(body, 2, 25, 16, 6, RuinStone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 25, 1, 6, RuinStone.Shade);
            Rect(body, 2, 25, 16, 1, RuinStone.Dark);
            // The course that floats, and the light between.
            Column(body, 2, 18, 16, 4, RuinStone);
            Rect(body, 2, 21, 16, 1, VoidLight);
            for (int x = 4; x < 17; x += 5) body[x, 23] = RuinStone.Main;
            LitOval(body, 10, 18, 8, 3, RuinStone);
            Oval(body, 10, 18, 6, 1.6f, VoidDeep);
            body[8, 18] = StarPale;
            body[12, 17] = VoidLight;
            body[10, 18] = VoidLight.Scale(0.7f);
            // Shards over it, the largest in the middle.
            Shard(body, 10, 12, 2f, 10, 5, VoidShard);
            Shard(body, 10, 12, 2f, 10, 14, VoidShard);
            body[10, 8] = StarCore;
            Shard(body, 4, 10, 1.2f, 4, 6, VoidShard);
            Shard(body, 16, 9, 1.2f, 16, 4, VoidShard);
            body[6, 2] = StarPale;
            body[14, 1] = VoidLight;
            return Finish(body, ShadowLayer(w, h, 11, 32, 8, 2));
        }

        // A lamp of the ruin: a small obelisk, runes down its face, a crystal hanging over its point and alight.
        static PixelImage ObeliskLamp()
        {
            var body = new PixelImage(T, 42);
            // The shaft, tapering, on a stepped foot.
            for (int y = 13; y < 38; y++)
            {
                int half = y < 17 ? (y - 13) / 2 + 1 : 3;
                for (int x = 8 - half; x < 8 + half; x++) body[x, y] = x < 8 - half + 1 ? RuinStone.Light : x < 8 ? RuinStone.Main : x < 8 + half - 1 ? RuinStone.Shade : RuinStone.Dark;
            }
            for (int y = 19; y < 36; y += 4)
            {
                body[7, y] = VoidLight;
                body[8, y + 1] = y % 8 == 3 ? StarPale : VoidLight.Scale(0.8f);
            }
            Rect(body, 4, 38, 8, 2, RuinStone.Shade);
            Rect(body, 4, 38, 8, 1, RuinStone.Main);
            // The crystal over it.
            Shard(body, 8, 7, 2.4f, 8, 1, VoidShard);
            Shard(body, 8, 7, 2.4f, 8, 11, VoidShard);
            body[7, 5] = StarCore;
            body[8, 7] = StarPale;
            body[3, 9] = RuinStone.Main;
            body[13, 6] = RuinStone.Shade;
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // A sign of the ruin: a slab that hangs over its broken stub, an arrow of light cut in it.
        static PixelImage RuneSign()
        {
            var body = new PixelImage(T, 26);
            for (int y = 3; y < 16; y++)
            {
                int inset = y < 6 ? 6 - y : 0;
                for (int x = 3 + inset; x < 13; x++) body[x, y] = x < 5 + inset ? RuinStone.Light : x > 10 ? RuinStone.Shade : RuinStone.Main;
            }
            // Its broken lower edge.
            for (int x = 3; x < 13; x++) body[x, 16 + (x * 7 % 3 == 0 ? 1 : 0)] = RuinStone.Dark;
            // The arrow.
            Rect(body, 5, 10, 5, 1, VoidLight);
            body[9, 9] = StarPale;
            body[9, 11] = VoidLight.Scale(0.75f);
            body[10, 10] = StarCore;
            // The crumbs and the stub it stood on.
            body[6, 19] = RuinStone.Main;
            body[10, 20] = RuinStone.Shade;
            Rect(body, 5, 22, 6, 2, RuinStone.Shade);
            Rect(body, 5, 22, 6, 1, RuinStone.Main);
            body[7, 21] = RuinStone.Light;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 5, 1.5f));
        }

        // A bench: a slab that floats over the two stones it once lay on.
        static PixelImage HoverBench()
        {
            var body = new PixelImage(T, 18);
            Rect(body, 2, 13, 3, 3, RuinStone.Shade);
            Rect(body, 2, 13, 3, 1, RuinStone.Main);
            Rect(body, 11, 13, 3, 3, RuinStone.Shade);
            Rect(body, 11, 13, 3, 1, RuinStone.Main);
            Box(body, 0, 5, 16, 3, 2, RuinStone);
            Rect(body, 1, 6, 5, 1, RuinStone.Light.Mix(Rgba.White, 0.2f));
            // The light under it.
            for (int x = 1; x < 15; x += 2) body[x, 10] = VoidLight.WithAlpha(190);
            body[4, 11] = StarPale;
            body[12, 11] = VoidLight;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A bowl of stone where star flowers are kept: pale five-pointed blooms on dark stems.
        static PixelImage StarBowl()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 10, 14, 2, 5, RuinStone);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 12, 1, 5, RuinStone.Dark);
            Rect(body, 2, 10, 12, 1, VoidDeep);
            foreach (var (x, top) in new[] { (3, 5), (6, 2), (9, 6), (12, 3) })
            {
                Rect(body, x, top + 1, 1, 10 - top - 1, Rgba.Hex("#4A3A6E"));
                body[x, top] = StarCore;
                body[x - 1, top] = StarPale;
                body[x + 1, top] = StarPale;
                body[x, top - 1] = StarPale;
                body[x, top + 1] = VoidLight;
            }
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // Stores: stone coffers, the lid of the upper one adrift over it, void shards kept in it.
        static PixelImage VoidCoffers()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 13, 14, 3, 10, RuinDark);
            Rect(body, 1, 16, 14, 1, RuinDark.Dark);
            foreach (int x in new[] { 5, 10 }) Rect(body, x, 17, 1, 9, RuinDark.Shade);
            Rect(body, 6, 19, 3, 3, VoidLight.Scale(0.8f));
            body[7, 20] = StarPale;
            Box(body, 3, 8, 9, 1, 4, RuinStone);
            Rect(body, 4, 8, 7, 1, VoidDeep);
            Shard(body, 5, 9, 1.3f, 4.5f, 5, VoidShard);
            Shard(body, 8, 9, 1.5f, 8.5f, 4, VoidShard);
            body[8, 6] = StarCore;
            // The lid, hanging.
            Rect(body, 4, 1, 9, 2, RuinStone.Main);
            Rect(body, 4, 1, 9, 1, RuinStone.Light);
            body[12, 2] = RuinStone.Shade;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The landmarks ----

        // The fallen chimney of a forge, three tiles by two: the stump of the firebox still standing at the left, its
        // mouth alight; the stack lying across the floor to the right in two broken lengths, bricks spilled between.
        static PixelImage ChimneyRuin()
        {
            const int w = 48, h = 54;
            var body = new PixelImage(w, h);
            // The stack, lying: two lengths end to end, sloping down to the right, seen from the side and a little above.
            void Length(int x0, int y0, int x1, int y1, int thick, int salt)
            {
                int steps = x1 - x0;
                for (int i = 0; i <= steps; i++)
                {
                    int x = x0 + i, top = y0 + (y1 - y0) * i / Math.Max(1, steps);
                    for (int j = 0; j < thick; j++)
                    {
                        int course = j / 3;
                        bool joint = j % 3 == 2 || (i + course % 2 * 3) % 6 == 5;
                        float tone = PixelNoise.Value((i + course % 2 * 3) / 6, course, salt);
                        var color = joint ? ForgeBrick.Dark : j < 3 ? ForgeBrick.Light : j > thick - 4 ? ForgeBrick.Shade : tone > 0.7f ? ForgeBrick.Main.Mix(ForgeBrick.Light, 0.4f) : ForgeBrick.Main;
                        body[x, top + j] = color;
                    }
                }
                // The iron hoop round it.
                for (int j = 0; j < thick; j++) body[x0 + steps / 2, y0 + (y1 - y0) / 2 + j] = j < 2 ? Iron.Light : Iron.Shade;
            }
            Length(17, 27, 31, 31, 13, 61);
            Length(34, 34, 46, 37, 12, 63);
            // The broken ends: dark hollows, soot.
            LitOval(body, 32.5f, 37.5f, 2.2f, 5.6f, Coal);
            LitOval(body, 46f, 43f, 1.8f, 5.2f, Coal);
            // Bricks spilled in the gap and before it.
            foreach (var (x, y) in new[] { (31, 46), (35, 48), (27, 47), (40, 50), (22, 48) })
            {
                Rect(body, x, y, 4, 2, ForgeBrick.Main);
                Rect(body, x, y, 4, 1, ForgeBrick.Light);
                body[x + 3, y + 1] = ForgeBrick.Dark;
            }
            // The firebox: a block of basalt and brick, taller than a person, its arched mouth alight, the stump of
            // the stack on it.
            Blocks(body, 2, 20, 22, 50, BasaltBlock, 6, 9, 65);
            Rect(body, 1, 20, 20, 3, BasaltBlock.Light);
            Rect(body, 1, 22, 20, 1, BasaltBlock.Shade);
            Blocks(body, 5, 15, 8, 20, ForgeBrick, 3, 5, 67);
            // The stump's ragged top.
            for (int x = 5; x < 15; x++)
                for (int j = 0; j < (int)(PixelNoise.Value(x, 3, 69) * 4); j++) body[x, 8 + j] = Rgba.Clear;
            Rect(body, 4, 14, 12, 1, Iron.Main);
            // The mouth.
            Rect(body, 5, 34, 12, 14, Coal.Dark);
            Rect(body, 6, 32, 10, 2, Coal.Dark);
            Rect(body, 4, 33, 1, 15, BasaltBlock.Light);
            Rect(body, 6, 31, 10, 1, BasaltBlock.Light);
            Fire(body, 9, 47, 3.4f, 9);
            Fire(body, 13.5f, 47, 2.6f, 6);
            Rect(body, 6, 46, 10, 2, ForgeDeep);
            Rect(body, 7, 46, 4, 1, ForgeCore);
            for (int x = 6; x < 16; x += 3) body[x, 44] = Coal.Main;
            // The light it throws on its own sill, and a pair of tongs left against it.
            Rect(body, 3, 48, 16, 2, BasaltBlock.Main);
            Rect(body, 5, 48, 12, 1, ForgeLight.Mix(BasaltBlock.Main, 0.45f));
            Line(body, 20, 49, 22, 36, Iron.Main);
            Line(body, 21, 49, 24, 37, Iron.Shade);
            // Smoke still rising from the stump.
            foreach (var (x, y, a) in new[] { (10, 5, 120), (11, 3, 90), (9, 2, 70), (12, 1, 50) }) body.Blend(x, y, new Rgba(200, 190, 184, a));
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // A giant anvil, three tiles by two, on a block of basalt: its horn to the left, a face a person could lie on,
        // an ingot left on it still glowing, a great hammer leant against its waist.
        static PixelImage GreatAnvil()
        {
            const int w = 48, h = 54;
            var body = new PixelImage(w, h);
            var steel = new Ramp(Rgba.Hex("#C0C6D2"), Rgba.Hex("#808898"), Rgba.Hex("#565C6C"), Rgba.Hex("#34384A"));
            // The block under it.
            Blocks(body, 6, 42, 38, 50, BasaltBlock, 6, 9, 71);
            Rect(body, 5, 35, 38, 4, BasaltBlock.Light);
            Rect(body, 5, 38, 38, 1, BasaltBlock.Shade);
            Rect(body, 41, 35, 2, 4, BasaltBlock.Main);
            // The foot and the waist.
            Rect(body, 12, 30, 26, 6, steel.Main);
            Rect(body, 12, 30, 26, 1, steel.Light);
            Rect(body, 34, 31, 4, 5, steel.Shade);
            Rect(body, 12, 35, 26, 1, steel.Dark);
            for (int y = 20; y < 30; y++)
            {
                int pinch = 3 - Math.Abs(y - 25) / 2;
                for (int x = 16 + pinch; x < 34 - pinch; x++) body[x, y] = x < 19 + pinch ? steel.Light : x > 30 - pinch ? steel.Dark : x > 26 ? steel.Shade : steel.Main;
            }
            // The body: the horn tapering away to the left, the heel square at the right.
            for (int y = 8; y < 20; y++)
            {
                for (int x = 1; x < 45; x++)
                {
                    bool horn = x < 14;
                    float half = horn ? 1 + (x - 1) * 5f / 13f : 6;
                    float mid = horn ? 12.5f : 14;
                    if (MathF.Abs(y + 0.5f - mid) > half) continue;
                    float v = (y + 0.5f - (mid - half)) / (2 * half);
                    body[x, y] = v < 0.22f ? steel.Light : v < 0.6f ? steel.Main : v < 0.86f ? steel.Shade : steel.Dark;
                }
            }
            // The face: a pale flat top, its near edge bright.
            Rect(body, 13, 6, 32, 3, steel.Light.Mix(Rgba.White, 0.25f));
            Rect(body, 13, 8, 32, 1, steel.Light);
            Rect(body, 43, 9, 2, 10, steel.Dark);
            // The hardy hole and the wear on the edge.
            Rect(body, 38, 7, 3, 1, steel.Dark);
            foreach (int x in new[] { 18, 25, 33 }) body[x, 9] = steel.Shade;
            // The ingot on it, alight.
            Rect(body, 22, 4, 9, 3, ForgeLight);
            Rect(body, 22, 4, 9, 1, ForgeHot);
            Rect(body, 23, 5, 6, 1, ForgeCore);
            Rect(body, 30, 5, 1, 2, ForgeDeep);
            Rect(body, 21, 7, 11, 1, ForgeLight.Mix(steel.Light, 0.4f));
            // The hammer, leant against the waist: a long haft, a head as big as a barrel.
            Line(body, 40, 49, 35, 22, DarkWood.Main);
            Line(body, 41, 49, 36, 22, DarkWood.Shade);
            Rect(body, 31, 17, 10, 6, Iron.Main);
            Rect(body, 31, 17, 10, 1, Iron.Light);
            Rect(body, 39, 18, 2, 5, Iron.Shade);
            Rect(body, 31, 22, 10, 1, Iron.Dark);
            // Scale and sparks about it.
            foreach (var (x, y) in new[] { (9, 48), (14, 51), (30, 51), (38, 49) }) body[x, y] = Coal.Main;
            body[11, 33] = ForgeLight;
            body[27, 2] = ForgeCore;
            body[33, 1] = ForgeLight;
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // A broken gate, three tiles wide: a ring of stone standing on end, its crown broken out and the pieces hanging
        // where they were; what is left of the way through still flickers inside it; chains from its sides end in the
        // air.
        static PixelImage GateRuin()
        {
            const int w = 48, h = 58;
            var body = new PixelImage(w, h);
            float cx = 24, cy = 30, outer = 21.5f, inner = 14;
            for (int y = 2; y < 52; y++)
            {
                for (int x = 1; x < 47; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * 0.86f;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float angle = MathF.Atan2(-dy, dx);
                    if (d < inner)
                    {
                        // What is left of the way: a thin shimmer, denser low down, stars through it.
                        uint roll = PixelNoise.Hash(x, y, 81) % 100;
                        float fill = 0.10f + 0.5f * (y - 14) / 38f;
                        if (y > 12 + (int)(PixelNoise.Value(x, 2, 83) * 8) && roll < fill * 100 && (x + y) % 2 == 0)
                            body[x, y] = (roll % 7 == 0 ? StarPale : VoidLight.Mix(VoidDeep, 0.35f)).WithAlpha(150);
                        continue;
                    }
                    if (d > outer) continue;
                    // The crown is gone between these angles, but for the pieces drawn after.
                    if (angle > 0.95f && angle < 2.05f) continue;
                    float t = (d - inner) / (outer - inner);
                    // Lit from the upper left: the outer rim pale on the left, the inner rim pale on the right.
                    float lit = (-dx * 0.6f - dy * 0.8f) / d * (t > 0.5f ? 1 : -1);
                    var color = t > 0.88f || t < 0.1f ? RuinStone.Dark : lit > 0.3f ? RuinStone.Light : lit > -0.45f ? RuinStone.Main : RuinStone.Shade;
                    // The voussoirs' joints, radial.
                    if ((int)MathF.Floor((angle + MathF.PI) * 6.2f) != (int)MathF.Floor((MathF.Atan2(-(dy + 0.86f), dx + 1) + MathF.PI) * 6.2f)) color = RuinStone.Dark;
                    body[x, y] = color;
                }
            }
            // Runes round the ring that still glow, on the stones nearest the break.
            foreach (var (x, y) in new[] { (7, 22), (6, 30), (8, 38), (41, 24), (42, 32), (40, 40), (12, 14), (37, 15) })
            {
                body[x, y] = VoidLight;
                body[x, y + 1] = StarPale;
                body[x + 1, y + 2] = VoidLight.Scale(0.8f);
            }
            // The crown's pieces, hanging in the gap.
            foreach (var (x, y, pw, ph) in new[] { (17, 3, 6, 5), (25, 1, 5, 4), (31, 5, 4, 4), (22, 9, 3, 2), (29, 11, 2, 2) })
            {
                Rect(body, x, y, pw, ph, RuinStone.Main);
                Rect(body, x, y, pw, 1, RuinStone.Light);
                Rect(body, x + pw - 1, y + 1, 1, ph - 1, RuinStone.Shade);
                Rect(body, x, y + ph - 1, pw, 1, RuinStone.Dark);
            }
            body[20, 9] = VoidLight;
            body[27, 6] = StarPale;
            // The plinth it stands in.
            Blocks(body, 6, 42, 47, 54, RuinDark, 4, 9, 85);
            Rect(body, 5, 45, 38, 3, RuinStone.Main);
            Rect(body, 5, 45, 38, 1, RuinStone.Light);
            Rect(body, 5, 47, 38, 1, RuinStone.Shade);
            // The chains: from a ring at each side up and outward, ending in nothing.
            foreach (int side in new[] { -1, 1 })
            {
                int x = 24 + side * 20, y = 34;
                for (int i = 0; i < 6; i++)
                {
                    body[x, y] = i % 2 == 0 ? Iron.Light : Iron.Shade;
                    x += side * (i % 2);
                    y -= 2;
                    if (x < 1 || x > 46) break;
                }
            }
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }

        // A star altar, three tiles by two: a round dais of dark stone, stepped, the stars cut in its top and joined by
        // lines of light; a crystal hanging over its middle, a faint column of light under it.
        static PixelImage StarDais()
        {
            const int w = 48, h = 50;
            var body = new PixelImage(w, h);
            // The drum: two steps, each a band of fitted stone under an oval top.
            for (int step = 0; step < 2; step++)
            {
                float rx = step == 0 ? 23 : 19.5f, ry = step == 0 ? 5.5f : 5f;
                int top = step == 0 ? 35 : 28, deep = step == 0 ? 6 : 5;
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - 24) / rx;
                    if (MathF.Abs(dx) > 1) continue;
                    int front = top + (int)(MathF.Sqrt(1 - dx * dx) * ry);
                    for (int y = top; y < front + deep; y++)
                    {
                        int row = y - front;
                        var color = dx < -0.5f ? RuinStone.Main : dx > 0.62f ? RuinDark.Shade : RuinDark.Main;
                        if (row >= 0 && ((x + (row / 3 % 2) * 4) % 8 == 0 || row % 3 == 2)) color = RuinDark.Dark;
                        if (row == deep - 1) color = RuinDark.Dark;
                        body[x, y] = color;
                    }
                }
                // Its top face: an oval, pale at the upper left, a bright lip along its near edge.
                LitOval(body, 24, top, rx, ry, step == 0 ? RuinDark : RuinStone);
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - 24) / rx;
                    if (MathF.Abs(dx) > 0.96f) continue;
                    body[x, top + (int)(MathF.Sqrt(1 - dx * dx) * ry)] = dx < 0.3f ? RuinStone.Light : RuinStone.Main;
                }
            }
            // The dark field the stars are cut in.
            Oval(body, 24, 28, 17, 3.8f, Rgba.Hex("#161030"));
            // The stars and the lines that join them.
            var stars = new[] { (11, 28), (16, 26), (22, 28), (27, 25), (33, 27), (37, 29), (29, 31), (19, 30) };
            for (int i = 0; i + 1 < stars.Length; i++)
            {
                var (ax, ay) = stars[i];
                var (bx, by) = stars[i + 1];
                Line(body, ax, ay, bx, by, VoidLight.Mix(Rgba.Hex("#161030"), 0.35f));
            }
            foreach (var (x, y) in stars)
            {
                body[x, y] = StarCore;
                body[x - 1, y] = VoidLight;
                body[x + 1, y] = VoidLight;
            }
            // The column of light and the crystal over the middle.
            for (int y = 14; y < 27; y++)
                for (int x = 22; x < 27; x++)
                    if ((x + y) % 2 == 0) body.Blend(x, y, VoidLight.WithAlpha(x == 24 ? 110 : 60));
            Shard(body, 24.5f, 9, 3.2f, 24.5f, 0, VoidShard);
            Shard(body, 24.5f, 9, 3.2f, 24.5f, 15, VoidShard);
            body[23, 6] = StarCore;
            body[24, 9] = StarPale;
            // Small ones at its shoulders, and crumbs adrift.
            Shard(body, 8, 20, 1.6f, 8, 15, VoidShard);
            Shard(body, 8, 20, 1.6f, 8, 23, VoidShard);
            Shard(body, 40, 18, 1.6f, 40, 13, VoidShard);
            Shard(body, 40, 18, 1.6f, 40, 21, VoidShard);
            body[14, 11] = RuinStone.Main;
            body[35, 8] = RuinStone.Shade;
            // Runes round the lower step.
            for (int x = 6; x < 43; x += 4)
                body[x, 37 + (int)(MathF.Sqrt(MathF.Max(0, 1 - MathF.Pow((x + 0.5f - 24) / 23f, 2))) * 5.5f)] = x % 8 == 2 ? StarPale : VoidLight;
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 3, w / 2f - 1, 3.5f));
        }
    }
}
