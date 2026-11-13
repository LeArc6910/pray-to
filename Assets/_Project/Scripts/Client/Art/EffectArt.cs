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
        // ---- The venom spider (2026-11-13, WorldView.Spider.cs) ----

        // A sticky orb web lying on the floor: eight spokes out from a hub and a spiral of thread round them, pale and
        // half clear so the floor shows through, dew beads here and there. Its threads catch the light when it is plucked
        // (lit).
        public static PixelImage Web(int diameter, bool lit)
        {
            var image = new PixelImage(diameter, diameter);
            float c = diameter / 2f, radius = c - 1;
            var thread = lit ? new Rgba(214, 255, 160, 235) : new Rgba(226, 230, 240, 170);
            var bead = lit ? new Rgba(250, 255, 220) : new Rgba(255, 255, 255, 230);
            const int spokes = 9;
            var ends = new (float X, float Y)[spokes];
            for (int i = 0; i < spokes; i++)
            {
                double a = (i + 0.18f * Math.Sin(i * 2.3)) * 2 * Math.PI / spokes;
                float reach = radius * (0.86f + 0.14f * (float)Math.Abs(Math.Sin(i * 1.7)));
                ends[i] = ((float)Math.Cos(a) * reach, (float)Math.Sin(a) * reach);
                Line(image, c, c, c + ends[i].X, c + ends[i].Y, thread);
            }
            // The spiral: rings of straight threads between neighbouring spokes, sagging a little toward the hub.
            for (float ring = 0.22f; ring < 0.98f; ring += 0.15f)
            {
                for (int i = 0; i < spokes; i++)
                {
                    var a = ends[i];
                    var b = ends[(i + 1) % spokes];
                    float u = ring, v = ring + 0.04f;
                    Line(image, c + a.X * u, c + a.Y * u, c + b.X * v, c + b.Y * v, thread);
                }
            }
            for (int i = 0; i < spokes; i += 2)
            {
                int x = (int)(c + ends[i].X * 0.55f), y = (int)(c + ends[i].Y * 0.55f);
                if (image.Inside(x, y)) image[x, y] = bead;
            }
            int h = (int)c;
            if (image.Inside(h, h)) image[h, h] = bead;
            return image;
        }

        static void Line(PixelImage image, float x0, float y0, float x1, float y1, Rgba color)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy)) + 1;
            for (int k = 0; k <= steps; k++)
            {
                int x = (int)Math.Round(x0 + dx * k / steps - 0.5f), y = (int)Math.Round(y0 + dy * k / steps - 0.5f);
                if (image.Inside(x, y)) image[x, y] = color;
            }
        }

        // A glob of venom: sickly green, a bright highlight, dark at the bottom, outlined.
        public static PixelImage Venom(int size)
        {
            var image = new PixelImage(size, size);
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - r) / r, dy = (y - r) / r;
                    float d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    image[x, y] = dx + dy < -0.7f ? new Rgba(232, 255, 176) : dy > 0.45f ? new Rgba(78, 140, 40) : new Rgba(150, 222, 82);
                }
            }
            return image.Outlined(new Rgba(20, 40, 12));
        }

        // An egg sac: a pale silk ball, veined, with a green glow showing through its top.
        public static PixelImage EggSac(int size)
        {
            var image = new PixelImage(size, size);
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - r) / r, dy = (y - r) / (r * 0.9f);
                    float d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    var color = dy < -0.35f && Math.Abs(dx) < 0.4f ? new Rgba(196, 240, 150) : dx + dy > 0.6f ? new Rgba(170, 160, 150) : new Rgba(232, 226, 210);
                    if ((x + 2 * y) % 5 == 0 && d > 0.2f) color = new Rgba(150, 140, 136);
                    image[x, y] = color;
                }
            }
            return image.Outlined(new Rgba(40, 30, 40));
        }

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
