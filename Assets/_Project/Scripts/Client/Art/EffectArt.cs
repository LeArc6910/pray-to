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
