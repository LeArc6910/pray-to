using System;

namespace Game.Client
{
    // Pixel effects drawn a frame at a time: the swing of a weapon, bursts, rings, sparkles and the ring of a buff.
    // They are greys and white, so a SpriteRenderer's colour tints them (a fire burst comes out orange with a pale
    // core) and one drawing serves every colour. Each is drawn at the size it shows, at 16 pixels a tile like
    // everything else, so its pixels line up with the world's. See Docs/ArtPipeline.md.
    static class EffectArt
    {
        // Frames in a burst, a ring and a sparkle; the view runs through them over a flash's life.
        public const int Frames = 6;
        // Frames in a swing, from the start of the sweep to the end.
        public const int SwingFrames = 5;
        // Frames in a buff ring's turn.
        public const int AuraFrames = 8;
        // How far each side of the way a swing points it reaches, in degrees.
        public const float SwingHalfAngle = 60;

        static readonly Rgba Core = Rgba.White;
        static readonly Rgba Light = new Rgba(214, 214, 214);
        static readonly Rgba Mid = new Rgba(158, 158, 158);
        static readonly Rgba Dim = new Rgba(108, 108, 108);

        // Sizes come in steps of four pixels, so a few drawings serve every radius.
        public static int Diameter(float tiles) => Math.Clamp((int)Math.Round(tiles * TileArt.Size / 4f) * 4, 8, 160);

        // The frame round the tile a [놓기] would build on (WorldView, 2026-11-09): a tile and a pixel each side, its four
        // corners bracketed in white with a dark edge, tinted by the screen (white open, red blocked).
        public const int TargetSize = TileArt.Size + 2;

        public static PixelImage TargetFrame()
        {
            var image = new PixelImage(TargetSize, TargetSize);
            const int arm = 5;
            for (int i = 0; i < arm; i++)
            {
                foreach (var (x, y) in new[] { (i, 0), (0, i), (TargetSize - 1 - i, 0), (TargetSize - 1, i), (i, TargetSize - 1), (0, TargetSize - 1 - i),
                    (TargetSize - 1 - i, TargetSize - 1), (TargetSize - 1, TargetSize - 1 - i) })
                {
                    image[x, y] = Rgba.White;
                }
            }
            return image.Outlined(new Rgba(20, 16, 24, 200));
        }

        // The warning over a boss winding up one of its strongest blows (2026-11-12, AttackWarning.Strong): a red
        // triangle with a white '!' and a dark edge, drawn in its own colours (not tinted).
        public static PixelImage WarningSign()
        {
            const int size = 13;
            var image = new PixelImage(size, size - 1);
            var red = new Rgba(232, 52, 36);
            var shine = new Rgba(255, 120, 90);
            for (int y = 0; y < size - 1; y++)
            {
                int half = y / 2 + 1;
                for (int x = size / 2 - half + 1; x <= size / 2 + half - 1; x++) image[x, y] = y < 3 ? shine : red;
            }
            for (int y = 3; y <= 7; y++) image[size / 2, y] = Rgba.White;
            image[size / 2, 9] = Rgba.White;
            return image.Outlined(new Rgba(40, 8, 8));
        }

        // Cherry blossom for the shrine samurai's fight (2026-11-12, the person: "make the blossom stand out, more
        // splendid"), drawn in its own colours: a single petal, notched at its broad end, pale or deep (kind 0, 1), or a
        // whole five-petal flower with a gold heart (kind 2). A faint dark edge keeps them on a pale floor.
        public static PixelImage Petal(int kind)
        {
            if (kind == 2)
            {
                const int size = 9;
                var flower = new PixelImage(size, size);
                float c = (size - 1) / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            double a = (90 + 72 * k) * System.Math.PI / 180;
                            float px = c + 2.3f * (float)System.Math.Cos(a), py = c - 2.3f * (float)System.Math.Sin(a);
                            float d = (x - px) * (x - px) + (y - py) * (y - py);
                            if (d > 2.1f) continue;
                            // Paler toward the tip, deeper toward the heart.
                            float out2 = (x - c) * (x - c) + (y - c) * (y - c);
                            flower[x, y] = out2 > 9 ? new Rgba(255, 228, 238) : out2 > 4 ? new Rgba(255, 178, 204) : new Rgba(236, 112, 152);
                        }
                    }
                }
                flower[(int)c, (int)c] = new Rgba(255, 214, 110);
                return flower.Outlined(new Rgba(90, 24, 48, 150));
            }
            // Tilted, broad and notched at the top right, narrowing to its foot at the bottom left.
            string[] rows =
            {
                "...a.a",
                "..abbb",
                ".abbbc",
                "abbbc.",
                ".bcc..",
                ".c....",
            };
            var light = kind == 0 ? new Rgba(255, 238, 244) : new Rgba(255, 196, 214);
            var body = kind == 0 ? new Rgba(255, 178, 204) : new Rgba(240, 120, 160);
            var deep = kind == 0 ? new Rgba(234, 116, 156) : new Rgba(196, 64, 112);
            var image = new PixelImage(6, 6);
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    image[x, y] = rows[y][x] switch { 'a' => light, 'b' => body, 'c' => deep, _ => image[x, y] };
            return image.Outlined(new Rgba(90, 24, 48, 150));
        }

        // The line a cut leaves (2026-11-13, the samurai): not a bar but a blade of light, a point at each end and full in
        // the middle, bowed a little like the katana that made it; a white core, a pale steel-blue body and a blush of
        // blossom pink at its edges, in its own colours (not tinted). Drawn along +x, as long as asked.
        public static PixelImage Slash(int length)
        {
            const int half = 4;
            float bow = Math.Min(3, length / 28f);
            var image = new PixelImage(length, 2 * half + 1 + (int)Math.Ceiling(bow));
            for (int x = 0; x < length; x++)
            {
                float t = (x + 0.5f) / length;
                // Fat through the middle, drawn out to a point at each end.
                float wide = half * (float)Math.Pow(Math.Sin(Math.PI * t), 0.55);
                float middle = half + bow * (1 - (float)Math.Sin(Math.PI * t));
                for (int y = 0; y < image.Height; y++)
                {
                    float off = Math.Abs(y - middle);
                    if (off > wide + 0.35f) continue;
                    image[x, y] = SlashColour(wide < 0.01f ? 0 : off / Math.Max(1, wide), wide, x, y);
                }
            }
            return image;
        }

        // The same blade of light bent round an arc: a sweep across the front (a wedge) or right round (a turn). The arc
        // is centred on +x from the middle of the picture, so many degrees wide, its radius in pixels.
        public static PixelImage SlashArc(int radius, int degrees)
        {
            const int half = 4;
            int size = 2 * (radius + half) + 2;
            var image = new PixelImage(size, size);
            float centre = size / 2f, span = degrees / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = centre - (y + 0.5f);
                    float angle = (float)(Math.Atan2(v, u) * 180 / Math.PI);
                    if (Math.Abs(angle) > span) continue;
                    float wide = half * (float)Math.Pow(Math.Cos(angle / span * Math.PI / 2), 0.55);
                    float off = Math.Abs((float)Math.Sqrt(u * u + v * v) - radius);
                    if (off > wide + 0.35f) continue;
                    image[x, y] = SlashColour(off / Math.Max(1, wide), wide, x, y);
                }
            }
            return image;
        }

        // Across a cut's line: white at its heart, then pale steel, then blossom, the outermost pixels thinned out.
        static Rgba SlashColour(float across, float wide, int x, int y)
        {
            if (across < 0.3f || wide < 1.2f) return Rgba.White;
            if (across < 0.62f) return new Rgba(206, 226, 250);
            if (across < 0.9f) return new Rgba(255, 176, 206, 230);
            return ((x + y) & 1) == 0 ? new Rgba(255, 150, 190, 150) : Rgba.Clear;
        }

        // A rock in flight or falling (the vein eater's throws and cave-in, 2026-11-12), drawn in its own colours.
        public static PixelImage Rock(int size)
        {
            var image = new PixelImage(size, size);
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - r) / r, dy = (y - r) / (r * 0.85f);
                    float d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    image[x, y] = dx + dy < -0.6f ? new Rgba(170, 154, 136) : dx + dy > 0.7f ? new Rgba(70, 58, 52) : (x * 5 + y * 3) % 7 == 0 ? new Rgba(92, 78, 70) : new Rgba(124, 108, 96);
                }
            }
            return image.Outlined(new Rgba(24, 16, 14));
        }

        // A web left on the floor (2026-11-13, the silk mother, HazardShape.Web): spokes from its middle, not quite
        // evenly, joined by rings of thread that sag between them. Each of a few kinds is a little different.
        public static PixelImage Web(int diameter, int kind)
        {
            var image = new PixelImage(diameter, diameter);
            float r = diameter / 2f - 1;
            float cx = diameter / 2f - 0.5f, cy = cx;
            var thread = new Rgba(236, 236, 244, 205);
            var faint = new Rgba(222, 222, 236, 120);
            var knot = new Rgba(255, 255, 255, 240);
            const int spokes = 11;
            var angles = new float[spokes];
            for (int i = 0; i < spokes; i++) angles[i] = (i + (PixelNoise.Value(i, kind, 91) - 0.5f) * 0.5f) * 2 * (float)System.Math.PI / spokes;
            void Thread(float x0, float y0, float x1, float y1, Rgba color)
            {
                int steps = (int)(System.Math.Max(System.Math.Abs(x1 - x0), System.Math.Abs(y1 - y0)) * 1.5f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    int x = (int)System.Math.Round(x0 + (x1 - x0) * i / steps), y = (int)System.Math.Round(y0 + (y1 - y0) * i / steps);
                    if (image.Inside(x, y) && image[x, y].A < color.A) image[x, y] = color;
                }
            }
            // A thin veil under it, so the patch reads as a patch on any floor.
            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    float d = (float)System.Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d <= 0.97f && ((x + y) & 1) == 0) image[x, y] = new Rgba(210, 210, 228, d > 0.8f ? 26 : 44);
                }
            for (int i = 0; i < spokes; i++)
            {
                float reach = r * (0.9f + 0.1f * PixelNoise.Value(i, kind, 92));
                Thread(cx, cy, cx + (float)System.Math.Cos(angles[i]) * reach, cy + (float)System.Math.Sin(angles[i]) * reach, thread);
            }
            foreach (float ring in new[] { 0.24f, 0.46f, 0.68f, 0.88f })
            {
                for (int i = 0; i < spokes; i++)
                {
                    int next = (i + 1) % spokes;
                    float a = r * ring * (0.94f + 0.1f * PixelNoise.Value(i, kind + (int)(ring * 100), 93));
                    float b = r * ring * (0.94f + 0.1f * PixelNoise.Value(next, kind + (int)(ring * 100), 93));
                    float ax = cx + (float)System.Math.Cos(angles[i]) * a, ay = cy + (float)System.Math.Sin(angles[i]) * a;
                    float bx = cx + (float)System.Math.Cos(angles[next]) * b, by = cy + (float)System.Math.Sin(angles[next]) * b;
                    // Sagging toward the middle between two spokes.
                    float mx = (ax + bx) / 2 + (cx - (ax + bx) / 2) * 0.12f, my = (ay + by) / 2 + (cy - (ay + by) / 2) * 0.12f;
                    Thread(ax, ay, mx, my, ring > 0.8f ? thread : faint);
                    Thread(mx, my, bx, by, ring > 0.8f ? thread : faint);
                    int kx = (int)System.Math.Round(ax), ky = (int)System.Math.Round(ay);
                    if (image.Inside(kx, ky)) image[kx, ky] = knot;
                }
            }
            return image;
        }

        public const int VenomFrames = 4;

        // A pool of venom on the floor (HazardShape.Venom): a sickly green puddle with a dark rim and a ragged edge,
        // bubbles rising and bursting on it frame by frame.
        public static PixelImage VenomPool(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float r = diameter / 2f - 1;
            float cx = diameter / 2f - 0.5f, cy = cx;
            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = (float)System.Math.Sqrt(dx * dx + dy * dy) / r;
                    double angle = System.Math.Atan2(dy, dx);
                    float edge = 0.9f + 0.07f * (float)System.Math.Sin(angle * 5 + 1) + 0.04f * (float)System.Math.Sin(angle * 11);
                    if (d > edge) continue;
                    image[x, y] = d > edge - 0.09f ? new Rgba(38, 78, 34, 225)
                        : d > edge - 0.2f ? new Rgba(74, 138, 52, 205)
                        : PixelNoise.Value(x, y, 60 + frame / 2) > 0.965f ? new Rgba(196, 248, 130, 220)
                        : new Rgba(104, 180, 64, 190);
                }
            // Bubbles: each swells over the frames and bursts to a ring.
            for (int i = 0; i < 5; i++)
            {
                float a = PixelNoise.Value(i, diameter, 71) * 6.283f, at = PixelNoise.Value(i, diameter, 72) * 0.6f * r;
                int bx = (int)(cx + System.Math.Cos(a) * at), by = (int)(cy + System.Math.Sin(a) * at);
                int age = (frame + i) % VenomFrames;
                if (age == VenomFrames - 1)
                {
                    foreach (var (ox, oy) in new[] { (-2, 0), (2, 0), (0, -2), (0, 2) })
                        if (image.Inside(bx + ox, by + oy) && image[bx + ox, by + oy].A > 0) image[bx + ox, by + oy] = new Rgba(214, 255, 150, 230);
                    continue;
                }
                for (int oy = -age; oy <= age; oy++)
                    for (int ox = -age; ox <= age; ox++)
                    {
                        if (ox * ox + oy * oy > age * age + 1 || !image.Inside(bx + ox, by + oy) || image[bx + ox, by + oy].A == 0) continue;
                        image[bx + ox, by + oy] = ox + oy < 0 ? new Rgba(214, 255, 150, 235) : new Rgba(150, 226, 92, 225);
                    }
            }
            return image;
        }

        // A gob of venom or of silk in flight.
        public static PixelImage Glob(int size, bool silk)
        {
            var image = new PixelImage(size + 3, size);
            float r = size / 2f - 0.5f;
            var light = silk ? Rgba.White : new Rgba(214, 255, 150);
            var main = silk ? new Rgba(232, 232, 242) : new Rgba(150, 226, 92);
            var dark = silk ? new Rgba(176, 176, 200) : new Rgba(74, 138, 52);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size + 3; x++)
                {
                    // A ball at the front (the right), drawn out into a tail behind it.
                    float dx = (x - 3 - r) / r, dy = (y - r) / r;
                    float d = dx * dx + dy * dy;
                    bool tail = x < 3 + r && System.Math.Abs(y - r) <= r * (x / (3 + r)) * 0.8f;
                    if (d > 1 && !tail) continue;
                    image[x, y] = d <= 1 && dx - dy > 0.5f ? light : d <= 1 && dx - dy < -0.7f ? dark : main;
                }
            return image.Outlined(silk ? new Rgba(120, 120, 150) : new Rgba(24, 48, 22));
        }

        // A ring of light opening out: thick and bright at first, then thinner and dimmer, and broken into bits at
        // the end. For novas, summons and anything that spreads from a spot.
        public static PixelImage Ring(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float t = (frame + 1f) / Frames;
            float centre = diameter / 2f;
            float outer = centre * (0.5f + 0.5f * t);
            float thickness = Math.Max(1.5f, (1 - t) * 0.22f * diameter + 1);
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float r = Distance(x, y, centre);
                    if (r > outer || r < outer - thickness) continue;
                    if (frame >= Frames - 2 && Hash(x, y, frame) % 3 == 0) continue;
                    float depth = (outer - r) / thickness;
                    var color = depth < 0.34f ? Core : depth < 0.67f ? Light : Mid;
                    image[x, y] = frame >= Frames - 2 ? Fade(color) : color;
                }
            }
            return image;
        }

        // A whirl: three blades of light sweeping round the middle, turning further each frame and thinning out at the
        // end, for spinning strikes (whirlwind, blade storm) and storms of ice (blizzard).
        public static PixelImage Vortex(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float t = (frame + 1f) / Frames;
            float centre = diameter / 2f;
            float outer = centre - 1;
            double turn = t * Math.PI * 1.4;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float r = Distance(x, y, centre);
                    if (r > outer || r < outer * 0.25f) continue;
                    double angle = Math.Atan2(y + 0.5 - centre, x + 0.5 - centre) - turn - r / outer * 1.6;
                    // Three arms: how near this pixel is to the middle of one, 0 at its middle and 1 between arms.
                    double arm = Math.Abs(((angle / (2 * Math.PI / 3)) % 1 + 1) % 1 - 0.5) * 2;
                    double width = 0.35 * (1 - t * 0.5);
                    if (arm > width) continue;
                    if (frame >= Frames - 2 && Hash(x, y, frame) % 3 == 0) continue;
                    float edge = (float)(arm / width);
                    var color = edge < 0.35f ? Core : edge < 0.7f ? Light : Mid;
                    image[x, y] = frame >= Frames - 2 ? Fade(color) : color;
                }
            }
            return image;
        }

        // A blast: a disc that fills out with a white-hot middle, then hollows from the inside and breaks up, with
        // sparks thrown past its edge.
        public static PixelImage Burst(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float t = (frame + 1f) / Frames;
            float centre = diameter / 2f;
            float outer = (centre - 1) * (0.5f + 0.4f * (float)Math.Sqrt(t));
            float hollow = t < 0.45f ? 0 : outer * (t - 0.45f) / 0.55f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float r = Distance(x, y, centre);
                    if (r > outer || r < hollow) continue;
                    // The last frames thin out to a checkerboard.
                    if (frame >= Frames - 2 && ((x + y + frame) & 1) == 0) continue;
                    float band = (r - hollow) / Math.Max(1, outer - hollow);
                    var color = frame < 2 ? (band < 0.6f ? Core : Light) : band < 0.3f ? Light : band < 0.7f ? Core : Mid;
                    image[x, y] = color;
                }
            }
            // Sparks: eight short streaks flying out, each at its own angle.
            int sparks = 8;
            for (int i = 0; i < sparks; i++)
            {
                double angle = (i + (Hash(i, diameter, 7) % 100) / 200.0) / sparks * Math.PI * 2;
                float reach = Math.Min(centre - 1, outer + 1 + t * centre * 0.25f);
                for (int step = 0; step < 2; step++)
                {
                    int x = (int)Math.Floor(centre + Math.Cos(angle) * (reach - step));
                    int y = (int)Math.Floor(centre + Math.Sin(angle) * (reach - step));
                    if (image.Inside(x, y)) image[x, y] = step == 0 ? Core : Mid;
                }
            }
            return image;
        }

        // Sparkles rising: little four-pointed stars that twinkle up and away. For healing, food and buffs.
        public static PixelImage Sparkles(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float t = (frame + 1f) / Frames;
            int count = Math.Clamp(diameter / 4, 4, 12);
            for (int i = 0; i < count; i++)
            {
                // Spread round the middle, each a little later than the last.
                double angle = (i + 0.5) / count * Math.PI * 2 + (Hash(i, diameter, 3) % 60) / 100.0;
                float spread = (diameter / 2f - 3) * (0.45f + 0.55f * ((Hash(i, diameter, 5) % 100) / 100f));
                float rise = t * diameter * 0.3f;
                int x = (int)Math.Round(diameter / 2f + Math.Cos(angle) * spread);
                int y = (int)Math.Round(diameter / 2f + Math.Sin(angle) * spread * 0.7f - diameter * 0.12f + rise);
                bool big = (i + frame) % 3 == 0 && frame < Frames - 1;
                var color = frame >= Frames - 2 ? Mid : Core;
                Plot(image, x, y, color);
                Plot(image, x + 1, y, Light);
                Plot(image, x - 1, y, Light);
                Plot(image, x, y + 1, Light);
                Plot(image, x, y - 1, Light);
                if (!big) continue;
                Plot(image, x + 2, y, Dim);
                Plot(image, x - 2, y, Dim);
                Plot(image, x, y + 2, Dim);
                Plot(image, x, y - 2, Dim);
            }
            return image;
        }

        // A blow landing: a white star of eight spikes, long and short in turn, that snaps out to its full reach and
        // then comes away from the middle as it fades.
        public static PixelImage Impact(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float t = (frame + 1f) / Frames;
            float centre = diameter / 2f;
            float reach = (centre - 0.5f) * (t < 0.35f ? 0.6f + t / 0.35f * 0.4f : 1);
            float gap = t < 0.5f ? 0 : reach * (t - 0.5f) / 0.5f * 0.8f;
            for (int k = 0; k < 8; k++)
            {
                double angle = k * Math.PI / 4;
                float length = k % 2 == 0 ? reach : reach * 0.55f;
                for (float r = gap; r <= length; r += 0.5f)
                {
                    int x = (int)Math.Floor(centre + Math.Cos(angle) * r);
                    int y = (int)Math.Floor(centre + Math.Sin(angle) * r);
                    float along = r / Math.Max(1, length);
                    var color = along < 0.35f ? Core : along < 0.7f ? Light : Mid;
                    Plot(image, x, y, frame >= Frames - 2 ? Fade(color) : color);
                }
            }
            if (t < 0.4f)
            {
                int c = (int)Math.Floor(centre);
                Plot(image, c, c, Core);
                Plot(image, c - 1, c, Core);
                Plot(image, c, c - 1, Core);
                Plot(image, c - 1, c - 1, Core);
            }
            return image;
        }

        // A weapon's swing: a crescent pointing along +x, swept from its upper end (+SwingHalfAngle) down to its
        // lower, the way the weapon in hand turns. The newest part of the sweep is white and the trail behind it dims
        // and breaks up; in the last frames the trail catches up, leaving a thin tail.
        public static PixelImage Swing(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float progress = (frame + 1f) / SwingFrames;
            float centre = diameter / 2f;
            float outer = centre - 0.5f;
            float span = SwingHalfAngle * 2;
            float lead = SwingHalfAngle - span * Math.Min(1, progress * 1.25f);
            float tail = progress <= 0.6f ? SwingHalfAngle : SwingHalfAngle - (SwingHalfAngle - lead - 20) * (progress - 0.6f) / 0.4f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre;
                    // Row 0 is the top: up is positive here, as in the game.
                    float v = centre - (y + 0.5f);
                    float r = (float)Math.Sqrt(u * u + v * v);
                    float angle = (float)(Math.Atan2(v, u) * 180 / Math.PI);
                    if (angle < lead || angle > tail || r > outer) continue;
                    // Thickest in the middle of the sweep, thin at its ends.
                    float inner = outer * (0.4f + 0.38f * Math.Abs(angle) / SwingHalfAngle);
                    if (r < inner) continue;
                    float age = (angle - lead) / span;
                    bool rim = r > outer - 1.5f;
                    if (age >= 0.6f && ((x + y) & 1) == 0) continue;
                    image[x, y] = age < 0.15f || (rim && age < 0.4f) ? Core : age < 0.35f || rim ? Light : age < 0.6f ? Mid : Dim;
                }
            }
            return image;
        }

        // The ring a buff keeps round its player: a dotted circle whose dashes walk round, a step a frame.
        public static PixelImage Aura(int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f;
            float radius = centre - 1;
            const int dashes = 12;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float r = Distance(x, y, centre);
                    if (r > radius || r < radius - 1.5f) continue;
                    double angle = Math.Atan2(y + 0.5 - centre, x + 0.5 - centre) / (Math.PI * 2) + 1;
                    int slice = (int)Math.Floor(angle * dashes * 2 + frame * 2.0 / AuraFrames * 2) % 4;
                    if (slice == 3) continue;
                    image[x, y] = slice == 0 ? Core : slice == 1 ? Light : Mid;
                }
            }
            return image;
        }

        static float Distance(int x, int y, float centre)
        {
            float u = x + 0.5f - centre;
            float v = y + 0.5f - centre;
            return (float)Math.Sqrt(u * u + v * v);
        }

        static Rgba Fade(Rgba color) => color.R > Mid.R ? Mid : Dim;

        static void Plot(PixelImage image, int x, int y, Rgba color)
        {
            if (image.Inside(x, y) && (!image.IsSet(x, y) || image[x, y].R < color.R)) image[x, y] = color;
        }

        // A fixed scramble, so the same frame always comes out the same.
        static int Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
