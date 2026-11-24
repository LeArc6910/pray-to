using System;
using Game.Core;

namespace Game.Client
{
    // The belts as they lie on the floor (2026-11-25, "입체감 없이 타일 형태로 칸 꽉 채우는거 다 찾아서 점검해"). A belt
    // used to be one drawing running east, turned by the view to face its way over a dark square frame: turned, its lit
    // rail fell on the wrong side, and where a line turned a corner the two straight pieces butted at a right angle.
    // Now each belt is drawn the way it lies, lit from the top left whichever way it runs, with a line of shadow on the
    // floor under its south or east rail, and a belt fed from the side (Feed) is a quarter curve, rails and chevrons
    // sweeping round the corner. An underground belt's hood is drawn the same way. The view does not turn these.
    static class BeltArt
    {
        const int Size = TileArt.Size;
        public const int Frames = ItemArt.BeltFrames;
        // How a belt is fed: from behind (a straight piece), from the left of its travel, or from the right.
        public const int Straight = 0, FromLeft = 1, FromRight = 2;

        static readonly Rgba Outline = Rgba.Hex("#15100E");
        static readonly Rgba Steel = Rgba.Hex("#A4AEBC"), SteelLight = Rgba.Hex("#D4DCE6"), SteelDark = Rgba.Hex("#5A6270");
        static readonly Rgba FastLight = Rgba.Hex("#F2C440"), FastRail = Rgba.Hex("#C8962C"), FastDark = Rgba.Hex("#7A5A18");
        static readonly Rgba Rubber = Rgba.Hex("#2C2E36"), Mark = Rgba.Hex("#4E525E");
        static readonly Rgba Shadow = new Rgba(0, 0, 0, 90);

        static readonly PixelImage[] Belts = new PixelImage[2 * 4 * 3 * Frames];
        static readonly PixelImage[] Hoods = new PixelImage[4 * Frames];

        public static bool IsBelt(TileType kind) => kind == TileType.Conveyor || kind == TileType.FastConveyor;

        // How the belt at (x, y) facing `facing` is fed: a curve when no belt behind it runs into it and a belt on
        // exactly one side does.
        public static int Feed(MachineArt.Lookup at, int x, int y, int facing)
        {
            if (at == null) return Straight;
            bool Into(int from)
            {
                // The neighbour on the side `from` (a facing: 0 east, 1 north, 2 west, 3 south) that runs toward this tile.
                var step = Machine.Steps[from & 3];
                var there = at(x + step.X, y + step.Y);
                return there is (TileType kind, int way) && World.IsBelt(kind) && (way & 3) == ((from + 2) & 3);
            }
            if (Into(facing + 2)) return Straight;
            bool left = Into(facing + 1), right = Into(facing + 3);
            return left == right ? Straight : left ? FromLeft : FromRight;
        }

        // The belt lying the way it runs: facing 0 east, 1 north, 2 west, 3 south.
        public static PixelImage Belt(bool fast, int facing, int feed, int frame)
        {
            facing &= 3;
            frame = (frame % Frames + Frames) % Frames;
            int index = (((fast ? 1 : 0) * 4 + facing) * 3 + feed) * Frames + frame;
            return Belts[index] ??= feed == Straight ? DrawStraight(fast, facing, frame) : DrawCurve(fast, facing, feed == FromLeft, frame);
        }

        // An underground belt's end: a belt running into (or out of) a hooded mouth on the half ahead.
        public static PixelImage Underground(int facing, int frame)
        {
            facing &= 3;
            frame = (frame % Frames + Frames) % Frames;
            return Hoods[facing * Frames + frame] ??= DrawHood(facing, frame);
        }

        // The pixel for a point `along` the belt's travel and `across` it (0..15 each; across counts from the travel's
        // left for a belt running east, so it is the row).
        static (int X, int Y) At(int facing, int along, int across) => facing switch
        {
            0 => (along, across),
            1 => (across, Size - 1 - along),
            2 => (Size - 1 - along, across),
            _ => (across, along),
        };

        // The band across a belt by how far from its top or left edge: outline, lit rail, rail, rubber, rail, dark, outline.
        static Rgba Band(int side, int along, int phase, bool fast)
        {
            var light = fast ? FastLight : SteelLight;
            var rail = fast ? FastRail : Steel;
            var dark = fast ? FastDark : SteelDark;
            switch (side)
            {
                case 1: return Outline;
                case 2: return light;
                case 3: return along % 4 == 1 ? dark : rail;
                case 12: return along % 4 == 1 ? dark : rail;
                case 13: return dark;
                case 14: return Outline;
            }
            if (side < 4 || side > 11) return Rgba.Clear;
            // Chevrons pointing the way the belt runs, two pixels thick, eight apart.
            int r = side - 4, offset = r < 4 ? r : 7 - r;
            int step = ((along - phase - offset) % 8 + 8) % 8;
            return step < 2 ? Mark : Rubber;
        }

        static PixelImage DrawStraight(bool fast, int facing, int frame)
        {
            var image = new PixelImage(Size, Size);
            int phase = frame * 2;
            for (int along = 0; along < Size; along++)
            {
                for (int side = 0; side < Size; side++)
                {
                    // The side is counted from the screen's top (a belt running east or west) or left (north or south),
                    // so the lit rail is always the one toward the light and the shadow falls south or east.
                    var (x, y) = At(facing, along, side);
                    var colour = side == 15 ? Shadow : Band(side, along, phase, fast);
                    if (colour.A > 0) image[x, y] = colour;
                }
            }
            return image;
        }

        // A quarter curve from the side the belt is fed on round to the side it runs out of.
        static PixelImage DrawCurve(bool fast, int facing, bool fromLeft, int frame)
        {
            var image = new PixelImage(Size, Size);
            int phase = frame * 2;
            // The unit steps of the way out and of the side it comes in from, in screen pixels (y down).
            var outStep = Screen(facing);
            var inStep = Screen(fromLeft ? facing + 1 : facing + 3);
            // The corner both sides share is the curve's centre.
            float cx = 8 + 8 * (outStep.X + inStep.X), cy = 8 + 8 * (outStep.Y + inStep.Y);
            var light = fast ? FastLight : SteelLight;
            var rail = fast ? FastRail : Steel;
            var dark = fast ? FastDark : SteelDark;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < 1 || d >= 16) continue;
                    // Away from the centre, as a direction; toward the light is up and to the left.
                    float ux = dx / d, uy = dy / d;
                    float lit = -(ux + uy);
                    // How far round from the side it comes in (0) to the side it goes out (1): the angle from the
                    // inward side's edge, which lies along the way out from the centre.
                    float fromIn = MathF.Atan2(-(dx * inStep.X + dy * inStep.Y), -(dx * outStep.X + dy * outStep.Y));
                    float t = Math.Clamp(fromIn / (MathF.PI / 2), 0, 1);
                    int along = (int)MathF.Floor(t * Size);
                    Rgba colour;
                    if (d < 2) colour = Outline;
                    else if (d < 3) colour = lit < 0 ? light : dark;
                    else if (d < 4) colour = along % 4 == 1 ? dark : rail;
                    else if (d < 12)
                    {
                        int r = (int)MathF.Floor(d) - 4, offset = r < 4 ? r : 7 - r;
                        int step = ((along - phase - offset) % 8 + 8) % 8;
                        colour = step < 2 ? Mark : Rubber;
                    }
                    else if (d < 13) colour = along % 4 == 1 ? dark : rail;
                    else if (d < 14) colour = lit > 0 ? light : dark;
                    else if (d < 15) colour = Outline;
                    // A line of shadow on the floor outside the outer rail where it faces away from the light.
                    else if (lit < -0.3f) colour = Shadow;
                    else continue;
                    image[x, y] = colour;
                }
            }
            return image;
        }

        // A facing as a step on the screen, whose rows run down.
        static (int X, int Y) Screen(int facing) => (facing & 3) switch
        {
            0 => (1, 0),
            1 => (0, -1),
            2 => (-1, 0),
            _ => (0, 1),
        };

        static PixelImage DrawHood(int facing, int frame)
        {
            var image = DrawStraight(false, facing, frame).Copy();
            var hood = Rgba.Hex("#5C6270");
            var hoodLight = Rgba.Hex("#8A92A2");
            var hoodDark = Rgba.Hex("#3C404C");
            var dark = Rgba.Hex("#121318");
            for (int along = 8; along < Size; along++)
            {
                for (int side = 1; side <= 14; side++)
                {
                    var (x, y) = At(facing, along, side);
                    bool edge = side == 1 || side == 14 || along == 8 || along == Size - 1;
                    // The hood's cheeks either side of the mouth, lit on the top or left one; the dark of the mouth.
                    var colour = edge ? Outline : side <= 3 ? (side == 2 ? hoodLight : hood) : side >= 12 ? (side == 13 ? hoodDark : hood)
                        : along <= 9 ? hoodLight : along >= Size - 3 ? hood : dark;
                    image[x, y] = colour;
                }
            }
            return image;
        }

        // ---- What lies under them on the tile layer ----

        // Under a belt: a faint dark bed, all that shows of a belt the host has not told this client about (too far, or
        // past its count), and the dark under the outside of a curve.
        public static readonly PixelImage Bed = DrawBed();

        static PixelImage DrawBed()
        {
            var image = new PixelImage(Size, Size);
            for (int y = 2; y < Size - 1; y++)
                for (int x = 2; x < Size - 1; x++)
                {
                    bool corner = (x == 2 || x == Size - 2) && (y == 2 || y == Size - 2);
                    if (!corner) image[x, y] = new Rgba(0, 0, 0, 70);
                }
            return image;
        }

        // A splitter's or a filter's housing with a line of shadow on the floor south and east of it: a low box, walked
        // over like a belt, so it stays on the floor.
        public static PixelImage Housing(TileType kind)
        {
            var image = TileArt.Object(kind).Copy();
            for (int i = 2; i < Size; i++)
            {
                if (!image.IsSet(i, Size - 1)) image[i, Size - 1] = Shadow;
                if (!image.IsSet(Size - 1, i)) image[Size - 1, i] = new Rgba(0, 0, 0, 64);
            }
            return image;
        }
    }
}
