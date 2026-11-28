using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // What a warning on the floor says: red for harm, yellow for a charge's path, gold for a place to gather.
    enum WarnTone : byte
    {
        Danger,
        Rush,
        Gather,
        // Blue (R1b, AttackWarning.Far): the black knight's blow that is worst close in.
        Cold,
        // Green (2026-11-28, AttackWarning.Safe): where to stand, the shadow behind the cave warden's pillars.
        Safe,
        // Faint violet (R4b, the void lord's rewind): a blast that landed a moment ago, coming again.
        Echo,
    }

    enum HazardForm : byte
    {
        Circle,
        Donut,
        Cone,
        Band,
        // A ring with a gap (R4b, the void lord's star maze, World.MazeGap degrees round the angle).
        Gapped,
    }

    // What the floor a blow lands on throws up.
    enum FloorStuff : byte
    {
        Stone,
        Ice,
        Crystal,
        Ember,
        Sand,
        Spore,
        Void,
    }

    // A warning's shape laid on the pixel grid at the size and angle it is judged at: how deep inside its edge each
    // pixel lies, how far along the filling and along the blow, and how near its core. Made once for a size and kept
    // (HazardArt.Field); the pictures of a warning filling and of the blow landing are drawn from it.
    sealed class HazardField
    {
        public int Width, Height;
        // Where the hazard's own position falls, in pixels from the left and from the top.
        public float OriginX, OriginY;
        // One drawn pixel is this many of the world's (1, or more for a shape too large to draw pixel for pixel).
        public int Scale = 1;
        public HazardForm Form;
        // 0 outside, 1 the outer line, 2 the line inside it, then how many pixels in from the edge (to 255).
        public byte[] Depth;
        // How far along the filling (0 to 255), along the blow that lands, and from the shape's core out to its edge.
        public byte[] Fill, Sweep, Core;
        // How wide the filling's bright front is, in the same units as Fill.
        public int Front;
        // The way the shape points (y up), for the arrowheads along a rush's path.
        public float Cos = 1, Sin;
        // Where the floor cracks: a point, how far its cracks reach and how many there are.
        public readonly List<(float X, float Y, float Reach, int Count)> Spine = new List<(float, float, float, int)>();

        public byte[] Buffer() => new byte[Width * Height * 4];
    }

    // The boss's warnings on the floor and the blow that lands in the same shape (E1 of Docs/EffectsRework.md).
    // A warning is a two-pixel edge that is there from the start (a bright line and a dark one), a faint wash inside,
    // stripes over the part that has filled with a brighter line at their front, and the whole shape blinking for
    // the last of its time. The blow is a white flash, then fire that runs through the shape the way the shape reads
    // (out from the middle of a circle, round a ring's band, across a wedge, along a line), and what it leaves on the
    // floor. Everything is written as RGBA bytes into a buffer the caller keeps, so a picture a step costs no memory.
    static class HazardArt
    {
        // Bright to dark, five steps.
        static readonly Rgba[][] Tones =
        {
            Ramp("#FFE0D0", "#FF8A6A", "#FF4033", "#B8201E", "#5A1014"),
            Ramp("#FFF8D0", "#FFE880", "#FFD94D", "#C89A20", "#6A4A10"),
            Ramp("#FFF6D8", "#FFD060", "#FFB21E", "#C07A10", "#5E3A08"),
            Ramp("#E0F4FF", "#8AD0FF", "#3A8CFF", "#1E4AB8", "#101E5A"),
            Ramp("#E8FFE0", "#9CFF8A", "#3FE05A", "#1E9A3A", "#0E4A1E"),
            Ramp("#F2E6FF", "#CDA8FF", "#9A6AE0", "#5A3A9A", "#2A1A4A"),
        };

        // Light, middle and dark of what flies up, then the soot or frost left behind.
        static readonly Rgba[][] Stuffs =
        {
            Ramp("#D8B880", "#A8845A", "#3A2A20", "#1C1416"),
            Ramp("#FFFFFF", "#C8F4FF", "#3F86D6", "#24406E"),
            Ramp("#FFFFFF", "#FF9ADF", "#B04AC8", "#3A2260"),
            Ramp("#FF9A2E", "#7A1E1E", "#2A1414", "#140C0C"),
            Ramp("#F4E4C0", "#D8B880", "#A8845A", "#4A3826"),
            Ramp("#D9A8FF", "#A86CE0", "#33204F", "#1E1430"),
            Ramp("#C77DFF", "#3A1C8C", "#150A33", "#0C0620"),
        };

        // How long the white flash lasts, how long a pixel burns once the blow reaches it, and the blow in all.
        public const float FlashSeconds = 0.05f;
        public const float BurnSeconds = 0.26f;
        // A warning blinks over this last part of its time.
        public const float BlinkFrom = 0.86f;
        // Steps a warning fills in: a picture is drawn again only when the step changes.
        public const int FillSteps = 32;

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static readonly Dictionary<long, HazardField> Fields = new Dictionary<long, HazardField>();
        const int MostFields = 48;
        // Past this many pixels across, a shape is drawn at half size (and half again) and shown larger.
        const int MostPixels = 384;

        static Rgba[] Ramp(params string[] hex)
        {
            var ramp = new Rgba[hex.Length];
            for (int i = 0; i < hex.Length; i++) ramp[i] = Rgba.Hex(hex[i]);
            return ramp;
        }

        public static Rgba[] Tone(WarnTone tone) => Tones[(int)tone];

        public static Rgba[] Stuff(FloorStuff stuff) => Stuffs[(int)stuff];

        // Whether dust rolls off this floor when it is struck.
        public static bool Dusty(FloorStuff stuff) => stuff == FloorStuff.Stone || stuff == FloorStuff.Sand;

        static bool Thin(int x, int y, float level) => level * 16 > Bayer[(y & 3) * 4 + (x & 3)];

        // How long the blow takes to cross the shape.
        public static float SweepSeconds(HazardForm form) => form switch
        {
            HazardForm.Circle => 0.12f,
            HazardForm.Donut => 0.16f,
            _ => 0.1f,
        };

        public static float BlastSeconds(HazardForm form) => FlashSeconds + SweepSeconds(form) + BurnSeconds;

        // The field of a shape: size and width in tiles (a ring's width is its hole's radius, a cone's its degrees),
        // the angle in radians (y up). Sizes go in steps of four pixels across and angles in whole degrees, so a few
        // fields serve every warning.
        public static HazardField Field(HazardForm form, float size, float width, float angle)
        {
            int degrees = (int)Math.Round(angle * 180 / Math.PI);
            degrees = ((degrees % 360) + 360) % 360;
            int across = Math.Max(8, (int)Math.Round(size * 2 * TileArt.Size / 4f) * 4);
            int length = Math.Max(4, (int)Math.Round(size * TileArt.Size / 4f) * 4);
            int second;
            switch (form)
            {
                case HazardForm.Circle:
                    degrees = 0;
                    second = 0;
                    break;
                case HazardForm.Donut:
                    // Only where the fire starts round the band turns with it.
                    degrees = degrees / 45 * 45;
                    second = Math.Min(across / 2 - 3, Math.Max(0, (int)Math.Round(width * TileArt.Size / 2f) * 2));
                    break;
                case HazardForm.Gapped:
                    // Its gap must be where the host has it: whole degrees.
                    second = Math.Min(across / 2 - 3, Math.Max(0, (int)Math.Round(width * TileArt.Size / 2f) * 2));
                    break;
                case HazardForm.Cone:
                    second = Math.Clamp((int)Math.Round(width), 1, 360);
                    if (second >= 360)
                    {
                        form = HazardForm.Circle;
                        degrees = 0;
                        second = 0;
                    }
                    break;
                default:
                    across = length;
                    second = Math.Max(2, (int)Math.Round(width * TileArt.Size / 2f) * 2);
                    break;
            }
            long key = (long)form | (long)across << 4 | (long)second << 24 | (long)degrees << 44;
            if (Fields.TryGetValue(key, out var field)) return field;
            if (Fields.Count >= MostFields) Fields.Clear();
            field = Make(form, across, second, degrees);
            Fields[key] = field;
            return field;
        }

        static HazardField Make(HazardForm form, int across, int second, int degrees)
        {
            int scale = 1;
            while ((form == HazardForm.Band ? across : across) / scale > MostPixels) scale *= 2;
            float r = across / 2f / scale, inner = second / (float)scale, wide = second / (float)scale, length = across / (float)scale;
            float turn = degrees * (float)Math.PI / 180, cos = (float)Math.Cos(turn), sin = (float)Math.Sin(turn);
            float half = second * (float)Math.PI / 360;
            int reach = form == HazardForm.Band ? (int)Math.Ceiling(Math.Sqrt(length * length + wide * wide / 4)) + 1 : (int)Math.Ceiling(r) + 1;
            int side = reach * 2;
            var depth = new byte[side * side];
            var fill = new byte[side * side];
            var sweep = new byte[side * side];
            var core = new byte[side * side];
            int minX = side, minY = side, maxX = -1, maxY = -1;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float wx = x + 0.5f - reach, wy = reach - (y + 0.5f);
                    float d = (float)Math.Sqrt(wx * wx + wy * wy);
                    float inside, along, blow, mid;
                    switch (form)
                    {
                        case HazardForm.Circle:
                            inside = d - r;
                            along = blow = mid = d / r;
                            break;
                        case HazardForm.Gapped:
                        {
                            float band = Math.Max(1, r - inner);
                            float off = Math.Abs(Off((float)Math.Atan2(wy, wx) - turn));
                            float gap = World.MazeGap * (float)Math.PI / 360;
                            inside = Math.Max(Math.Max(d - r, inner - d), (gap - off) * d);
                            along = (d - inner) / band;
                            blow = off / (float)Math.PI;
                            mid = Math.Abs(2 * along - 1);
                            break;
                        }
                        case HazardForm.Donut:
                        {
                            float band = Math.Max(1, r - inner);
                            inside = Math.Max(d - r, inner - d);
                            along = (d - inner) / band;
                            blow = Math.Abs(Off((float)Math.Atan2(wy, wx) - turn)) / (float)Math.PI;
                            mid = Math.Abs(2 * along - 1);
                            break;
                        }
                        case HazardForm.Cone:
                        {
                            float off = Off((float)Math.Atan2(wy, wx) - turn);
                            // Past a right angle from the wedge's side the nearest edge is its tip.
                            float away = Math.Abs(off) - half;
                            inside = Math.Max(d - r, away > Math.PI / 2 ? d : (float)Math.Sin(away) * d);
                            along = d / r;
                            blow = (off + half) / (2 * half);
                            mid = 1 - 0.9f * along;
                            break;
                        }
                        default:
                        {
                            float u = wx * cos + wy * sin, v = -wx * sin + wy * cos;
                            inside = Math.Max(Math.Max(-u, u - length), Math.Abs(v) - wide / 2);
                            along = blow = u / length;
                            mid = Math.Abs(v) / (wide / 2);
                            break;
                        }
                    }
                    if (inside >= 0) continue;
                    int i = y * side + x;
                    depth[i] = (byte)Math.Min(255, (int)Math.Ceiling(-inside));
                    fill[i] = Unit(along);
                    sweep[i] = Unit(blow);
                    core[i] = Unit(mid);
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) minX = minY = maxX = maxY = reach;
            int w = maxX - minX + 1, h = maxY - minY + 1;
            float extent = form == HazardForm.Donut || form == HazardForm.Gapped ? r - inner : form == HazardForm.Band ? length : r;
            var field = new HazardField
            {
                Width = w,
                Height = h,
                OriginX = reach - minX,
                OriginY = reach - minY,
                Scale = scale,
                Form = form,
                Depth = Crop(depth, side, minX, minY, w, h),
                Fill = Crop(fill, side, minX, minY, w, h),
                Sweep = Crop(sweep, side, minX, minY, w, h),
                Core = Crop(core, side, minX, minY, w, h),
                Front = (int)Math.Max(9, 255 * 1.6f / Math.Max(1, extent)),
                Cos = cos,
                Sin = sin,
            };
            float ox = field.OriginX, oy = field.OriginY;
            switch (form)
            {
                case HazardForm.Circle:
                    field.Spine.Add((ox, oy, r * 0.9f, r < 14 ? 6 : 11));
                    break;
                case HazardForm.Donut:
                case HazardForm.Gapped:
                {
                    float at = (r + inner) / 2;
                    int n = Math.Clamp((int)Math.Round(2 * Math.PI * at / 28), 6, 14);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 2 * (float)Math.PI / n;
                        if (form == HazardForm.Gapped && Math.Abs(Off(a - turn)) < World.MazeGap * (float)Math.PI / 360) continue;
                        field.Spine.Add((ox + (float)Math.Cos(a) * at, oy - (float)Math.Sin(a) * at, (r - inner) * 0.55f, 3));
                    }
                    break;
                }
                case HazardForm.Cone:
                    foreach (float part in new[] { 0.35f, 0.7f })
                    {
                        float spread = Math.Min(r * 0.4f, r * part * (float)Math.Tan(Math.Min(half, 1.0f)));
                        field.Spine.Add((ox + cos * r * part, oy - sin * r * part, Math.Max(4, spread), 5));
                    }
                    break;
                default:
                    for (float u = Math.Min(10, length / 2); u < length; u += 22)
                        field.Spine.Add((ox + cos * u, oy - sin * u, wide * 0.6f + 3, 3));
                    break;
            }
            return field;
        }

        static float Off(float angle)
        {
            while (angle > Math.PI) angle -= 2 * (float)Math.PI;
            while (angle < -Math.PI) angle += 2 * (float)Math.PI;
            return angle;
        }

        static byte Unit(float v) => (byte)(v <= 0 ? 0 : v >= 1 ? 255 : (int)(v * 255));

        static byte[] Crop(byte[] from, int side, int left, int top, int w, int h)
        {
            var to = new byte[w * h];
            for (int y = 0; y < h; y++) Array.Copy(from, (top + y) * side + left, to, y * w, w);
            return to;
        }

        static void Put(byte[] buffer, int at, Rgba c, float alpha)
        {
            buffer[at] = c.R;
            buffer[at + 1] = c.G;
            buffer[at + 2] = c.B;
            buffer[at + 3] = (byte)(alpha >= 1 ? 255 : alpha <= 0 ? 0 : (int)(alpha * 255));
        }

        // A warning at a moment of its filling (0 to 1). blink: the bright half of its last blinking; lit: its edge
        // at its brightest (a place to gather pulses). flip: rows from the bottom up, as a texture wants them.
        // light: an ordinary monster's, fainter inside. arrows: for a rush's path, arrowheads marching the way it will
        // run, this many pixels along (under 0 for none).
        public static void Warn(HazardField field, WarnTone tone, float progress, bool blink, bool lit, byte[] buffer, bool flip, bool light = false, int arrows = -1)
        {
            var ramp = Tones[(int)tone];
            // An ordinary monster's (E5): the same edge, a fainter inside, since several lie on one screen.
            float wash = light ? 0.07f : 0.14f, stripe = light ? 0.3f : 0.42f, between = light ? 0.15f : 0.26f, head = light ? 0.55f : 0.7f, flare = light ? 0.36f : 0.5f;
            int w = field.Width, h = field.Height, filled = (int)(progress * 255), front = filled - field.Front;
            for (int y = 0; y < h; y++)
            {
                int row = (flip ? h - 1 - y : y) * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, at = row + x * 4;
                    int depth = field.Depth[i];
                    if (depth == 0) buffer[at + 3] = 0;
                    else if (depth == 1) Put(buffer, at, blink || lit ? ramp[0] : ramp[1], 0.95f);
                    else if (depth == 2) Put(buffer, at, ramp[3], 0.75f);
                    else if (arrows >= 0 && Arrow(field, x, y, arrows, out bool bright)) Put(buffer, at, bright ? ramp[0] : ramp[3], bright ? 0.9f : 0.75f);
                    else if (field.Fill[i] <= filled && progress > 0)
                    {
                        // The filling's own front is a brighter line; behind it, stripes of the warning's colour.
                        if (field.Fill[i] > front) Put(buffer, at, ramp[1], head);
                        else Put(buffer, at, ramp[2], blink ? flare : ((x + y) & 3) == 0 ? stripe : between);
                    }
                    else Put(buffer, at, ramp[2], wash);
                }
            }
        }

        // Whether a pixel lies on one of the arrowheads along a band: one every 22 pixels, five pixels to each side
        // of the middle line, a bright line with a dark one behind it.
        static bool Arrow(HazardField field, int x, int y, int march, out bool bright)
        {
            float wx = x + 0.5f - field.OriginX, wy = field.OriginY - (y + 0.5f);
            float u = wx * field.Cos + wy * field.Sin, v = Math.Abs(-wx * field.Sin + wy * field.Cos);
            bright = false;
            if (v > 5.5f) return false;
            int along = (int)Math.Floor(u + v - march);
            along = ((along % 22) + 22) % 22;
            bright = along >= 2;
            return along < 4;
        }

        // The blow at an age in seconds, in the colours given (bright to dark, five). False once it is over. An
        // ordinary monster's is fainter (strength under 1, MonsterFx.BlowStrength).
        public static bool Blast(HazardField field, Rgba[] ramp, float age, int seed, byte[] buffer, bool flip, float strength = 1)
        {
            int w = field.Width, h = field.Height;
            float cross = SweepSeconds(field.Form);
            bool flash = age < FlashSeconds, any = false;
            int flicker = seed * 31 + (int)(age * 20);
            for (int y = 0; y < h; y++)
            {
                int row = (flip ? h - 1 - y : y) * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, at = row + x * 4;
                    int depth = field.Depth[i];
                    buffer[at + 3] = 0;
                    if (depth == 0) continue;
                    if (flash)
                    {
                        Put(buffer, at, Rgba.White, (depth <= 2 ? 1 : 0.85f) * strength);
                        any = true;
                        continue;
                    }
                    float s = field.Sweep[i] / 255f;
                    // A circle's blast swells fast from its middle; the others cross their shape at one pace.
                    float tau = age - FlashSeconds - (field.Form == HazardForm.Circle ? cross * s * s : cross * s);
                    if (tau < 0)
                    {
                        // The whole shape is lit while the blow crosses it.
                        Put(buffer, at, ramp[1], (depth == 1 ? 0.9f : 0.3f) * strength);
                        any = true;
                        continue;
                    }
                    float heat = 1 - tau / BurnSeconds;
                    if (heat <= 0) continue;
                    any = true;
                    if (depth == 1 && tau < 0.09f)
                    {
                        // The edge of what it hits, crisp for a moment.
                        Put(buffer, at, ramp[1], 0.9f * strength);
                        continue;
                    }
                    float level = heat * (1 - 0.62f * field.Core[i] / 255f) + (PixelNoise.Value(x >> 1, y >> 1, flicker) - 0.5f) * 0.1f;
                    if (level > 0.72f) Put(buffer, at, ramp[0], strength);
                    else if (level > 0.54f) Put(buffer, at, ramp[1], strength);
                    else if (level > 0.36f) Put(buffer, at, ramp[2], strength);
                    else if (level > 0.2f) Put(buffer, at, ramp[3], 0.95f * strength);
                    else if (level > 0.1f && Thin(x, y, 0.5f)) Put(buffer, at, ramp[4], 0.85f * strength);
                }
            }
            return any;
        }

        // What the blow leaves on the floor: soot (or frost) with a ragged edge inside the shape, and cracks.
        public static void Scar(HazardField field, FloorStuff stuff, int seed, byte[] buffer, bool flip)
        {
            var colors = Stuffs[(int)stuff];
            Rgba soot = colors[3], crack = stuff == FloorStuff.Ice || stuff == FloorStuff.Crystal ? colors[1] : colors[3].Scale(0.6f);
            int w = field.Width, h = field.Height;
            for (int y = 0; y < h; y++)
            {
                int row = (flip ? h - 1 - y : y) * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int at = row + x * 4, depth = field.Depth[y * w + x];
                    buffer[at + 3] = 0;
                    float ragged = 3 + 6 * PixelNoise.Value(x >> 2, y >> 2, seed);
                    if (depth <= ragged) continue;
                    Put(buffer, at, soot, depth > ragged + 4 || Thin(x, y, 0.6f) ? 0.42f : 0.2f);
                }
            }
            int n = 0;
            foreach (var (sx, sy, reach, count) in field.Spine)
            {
                for (int i = 0; i < count; i++, n++)
                {
                    float dir = (i + 0.4f * Rnd(seed + 1, n)) * 2 * (float)Math.PI / count, far = reach * (0.6f + 0.35f * Rnd(seed + 2, n));
                    float bend = dir + (Rnd(seed + 3, n) - 0.5f) * 0.8f;
                    float midX = sx + (float)Math.Cos(dir) * far * 0.55f, midY = sy - (float)Math.Sin(dir) * far * 0.55f;
                    Line(field, buffer, flip, sx, sy, midX, midY, crack, far > 12 ? 2 : 1);
                    Line(field, buffer, flip, midX, midY, midX + (float)Math.Cos(bend) * far * 0.45f, midY - (float)Math.Sin(bend) * far * 0.45f, crack, 1);
                }
            }
        }

        static float Rnd(int a, int b) => PixelNoise.Value(a, b, 977);

        // A crack, kept inside the shape.
        static void Line(HazardField field, byte[] buffer, bool flip, float x0, float y0, float x1, float y1, Rgba c, int thick)
        {
            int steps = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                int x = (int)Math.Floor(x0 + (x1 - x0) * i / steps), y = (int)Math.Floor(y0 + (y1 - y0) * i / steps);
                for (int o = 0; o < thick; o++)
                {
                    int px = x + o, py = y;
                    if (px < 0 || py < 0 || px >= field.Width || py >= field.Height || field.Depth[py * field.Width + px] < 2) continue;
                    Put(buffer, ((flip ? field.Height - 1 - py : py) * field.Width + px) * 4, c, 0.9f);
                }
            }
        }

        // A buffer written top down, as a picture (the preview's).
        public static PixelImage Image(HazardField field, byte[] buffer)
        {
            var image = new PixelImage(field.Width, field.Height);
            for (int y = 0; y < field.Height; y++)
            {
                for (int x = 0; x < field.Width; x++)
                {
                    int at = (y * field.Width + x) * 4;
                    if (buffer[at + 3] > 0) image[x, y] = new Rgba(buffer[at], buffer[at + 1], buffer[at + 2], buffer[at + 3]);
                }
            }
            return image;
        }

        // A spark with the streak it leaves, flying to the right: its head bright, its tail thinning out.
        public static PixelImage Spark(Rgba head, Rgba tail)
        {
            var image = new PixelImage(6, 1);
            image[5, 0] = head;
            image[4, 0] = head.Mix(tail, 0.5f);
            image[3, 0] = tail;
            image[2, 0] = tail.WithAlpha(190);
            image[1, 0] = tail.WithAlpha(120);
            image[0, 0] = tail.WithAlpha(60);
            return image;
        }

        public const int Chunks = 3;

        // A bit of the floor thrown up: lit from above, dark beneath.
        public static PixelImage Chunk(FloorStuff stuff, int variant)
        {
            var c = Stuffs[(int)stuff];
            var palette = new Dictionary<char, Rgba> { ['L'] = c[0], ['M'] = c[1], ['D'] = c[2] };
            return (variant % Chunks) switch
            {
                0 => PixelImage.FromRows(palette, ".LM.", "LMMD", "MMDD", ".DD."),
                1 => PixelImage.FromRows(palette, "LM.", "MMD", ".DD"),
                _ => PixelImage.FromRows(palette, "LM", "MD"),
            };
        }

        // A puff of dust, white for the view to tint: thinned at its rim.
        public static PixelImage Puff(int size)
        {
            var image = new PixelImage(size, size);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r, d = (float)Math.Sqrt(dx * dx + dy * dy) / r;
                    if (d > 1 || d > 0.6f && !Thin(x, y, 0.5f)) continue;
                    image[x, y] = dy < -r * 0.2f ? Rgba.White : new Rgba(214, 214, 214);
                }
            }
            return image;
        }
    }
}
