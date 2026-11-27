using System;

namespace Game.Client
{
    // The pieces every effect's layers are made of since the effects rework (E3, Docs/EffectsRework.md 8): a blast's
    // ball with wavering edges, smoke that empties from the middle, rising smoke, sparks with tails, a shock wave,
    // rocks, soot and glowing splits on the floor, and a soft floor light. They are coloured by an element's five
    // colours (Ramp5), whose darker end turns in hue, so a fire cools from pale yellow through orange to a deep red.
    static partial class SkillFxArt
    {
        // The two darker colours each element adds to its ramp, hue turned: the fourth and fifth of Ramp5.
        static readonly Rgba[][] Turns =
        {
            new[] { Rgba.Hex("#D8421C"), Rgba.Hex("#7A1E22") },
            new[] { Rgba.Hex("#3F86D6"), Rgba.Hex("#2A3F8A") },
            new[] { Rgba.Hex("#A98CFF"), Rgba.Hex("#4B3AA0") },
            new[] { Rgba.Hex("#5E6F8A"), Rgba.Hex("#353F55") },
            new[] { Rgba.Hex("#6E5238"), Rgba.Hex("#3A2A20") },
            new[] { Rgba.Hex("#1E7A8A"), Rgba.Hex("#163F5E") },
            new[] { Rgba.Hex("#2F7F2A"), Rgba.Hex("#17402A") },
            new[] { Rgba.Hex("#5E8A8A"), Rgba.Hex("#34525A") },
            new[] { Rgba.Hex("#3A3238"), Rgba.Hex("#2A2228") },
            new[] { Rgba.Hex("#C0701E"), Rgba.Hex("#6A3A18") },
            new[] { Rgba.Hex("#8AA8C0"), Rgba.Hex("#5E7896") },
            new[] { Rgba.Hex("#B8201E"), Rgba.Hex("#5A1014") },
            new[] { Rgba.Hex("#6A3FA8"), Rgba.Hex("#33204F") },
            new[] { Rgba.Hex("#5E5E5E"), Rgba.Hex("#333333") },
            new[] { Rgba.Hex("#7A3AA8"), Rgba.Hex("#3F2468") },
            new[] { Rgba.Hex("#6E4E28"), Rgba.Hex("#3E2A18") },
        };

        static readonly Rgba[][] Fives = new Rgba[Turns.Length][];

        // An element's five colours, brightest first: core, light, mid (the old ramp's upper three), then the two that
        // turn in hue as they darken. The old four-colour ramp (Ramp) is untouched, so the older pieces look as before.
        public static Rgba[] Ramp5(FxRamp ramp)
        {
            int i = (int)ramp;
            if (Fives[i] == null)
            {
                var four = Ramps[i];
                Fives[i] = new[] { four[3], four[2], four[1], Turns[i][0], Turns[i][1] };
            }
            return Fives[i];
        }

        // How long a ball's flash lasts and each frame after it, in seconds, and how long the ball is there in all.
        public const float BurstFlash = 0.05f, BurstStep = 0.039f, BurstLife = BurstFlash + 8 * BurstStep;

        // The ball's frame at this age (0 to 8), for one of four different balls.
        public static int BurstFrame(float age, int variant = 0) =>
            (age < BurstFlash ? 0 : Math.Min(8, 1 + (int)((age - BurstFlash) / BurstStep))) + 16 * (variant & 3);

        // A split's frame: which of four, and how far it has cooled (0 glowing, 1 dull, 2 cold).
        public static int SplitFrame(int variant, int cooled) => 4 + (variant & 3) + 4 * Math.Clamp(cooled, 0, 2);

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // Thinned in a pattern rather than by alpha, so every pixel stays a solid colour.
        static bool Thin(int x, int y, float level) => level * 16 > Bayer[(y & 3) * 4 + (x & 3)];

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        static float Out3(float u)
        {
            u = Clamp01(u);
            return 1 - (1 - u) * (1 - u) * (1 - u);
        }

        // The edge of a lump that wavers: two sines round it, moved by the phase.
        static float Wobble(float angle, float seed, float phase, float amount) =>
            1 + amount * (0.6f * (float)Math.Sin(angle * 3 + seed + phase) + 0.4f * (float)Math.Sin(angle * 7 + seed * 2.3f - phase * 1.7f));

        // The lit side of a lump of smoke: a crescent along its upper left, not half of it.
        static bool Lit(float u, float v, float edge)
        {
            float su = u - edge * 0.28f, sv = v - edge * 0.28f;
            return su * su + sv * sv > edge * edge * 0.78f;
        }

        static readonly Rgba SmokeDark = Rgba.Hex("#3A3238"), SmokeMid = Rgba.Hex("#5A525E"), SmokeLight = Rgba.Hex("#7C7280");

        static PixelImage Burst(Rgba[] r, int size, int frame)
        {
            int stage = Math.Clamp(frame & 15, 0, 8), seed = 3 + 5 * (frame >> 4 & 3);
            float age = stage == 0 ? 0.02f : BurstFlash + (stage - 0.5f) * BurstStep;
            float reach = size / 2f;
            int side = (int)Math.Ceiling(size * 1.2f) + 4 & ~1;
            var image = new PixelImage(side, side);
            float centre = side / 2f;
            float body = reach * (0.35f + 0.6f * Out3(age / 0.2f));
            float fade = age < 0.26f ? 1 : Clamp01((BurstLife - age) / 0.11f);
            float smoke = age > 0.15f ? body * Clamp01((age - 0.15f) / 0.2f) * 0.85f : 0;
            float smokeThin = age < 0.27f ? 0.85f : age < 0.31f ? 0.7f : 0.5f;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v), angle = (float)Math.Atan2(v, u);
                    Rgba color = default;
                    if (distance <= body * Wobble(angle, seed, age * 30, 0.16f) && Thin(x, y, fade)) color = r[3];
                    if (distance <= body * 0.82f * Wobble(angle, seed + 1, age * 26, 0.16f) && Thin(x, y, fade)) color = r[2];
                    if (age < 0.24f && distance <= body * 0.6f * Wobble(angle, seed + 2, age * 22, 0.14f)) color = r[1];
                    if (age < 0.13f && distance <= body * 0.36f * Wobble(angle, seed + 3, age * 20, 0.1f)) color = r[0];
                    if (smoke > 0)
                    {
                        // It hollows from the middle: smoke, a little above, and through the smoke the floor.
                        float sv = v + 2, sd = (float)Math.Sqrt(u * u + sv * sv);
                        float edge = smoke * Wobble((float)Math.Atan2(sv, u), seed + 4, age * 12, 0.2f);
                        if (sd <= edge) color = Thin(x, y, smokeThin) ? (Lit(u, sv, edge) ? SmokeMid : SmokeDark) : default;
                    }
                    if (stage == 0)
                    {
                        // The flash: a white disc and a four-pointed star, one frame.
                        float arm = Math.Abs(u) + Math.Abs(v);
                        bool star = (Math.Abs(u) < 1 || Math.Abs(v) < 1) && arm <= reach * 0.9f || arm <= reach * 0.3f;
                        if (distance <= reach * 0.5f || star) color = new Rgba(255, 252, 240);
                    }
                    if (color.A > 0) image[x, y] = color;
                }
            }
            return image;
        }

        static PixelImage Pall(int size, int frame)
        {
            frame = Math.Clamp(frame, 0, 5);
            var image = new PixelImage(size, size);
            float centre = size / 2f, full = centre - 1;
            float hollow = full * (0.25f + 0.13f * frame), thin = 0.8f - 0.11f * frame;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v), angle = (float)Math.Atan2(v, u);
                    float edge = full * 0.86f * Wobble(angle, size, frame * 0.9f, 0.14f);
                    if (distance > edge || distance < hollow * Wobble(angle, size + 3, frame * 0.7f, 0.25f) || !Thin(x, y, thin)) continue;
                    bool lit = Lit(u, v, edge);
                    image[x, y] = frame < 3 ? (lit ? SmokeMid : SmokeDark) : lit ? SmokeLight : SmokeMid;
                }
            }
            return image;
        }

        static PixelImage Plume(int size, int frame)
        {
            frame = Math.Clamp(frame, 0, 5);
            var image = new PixelImage(size, size);
            float centre = size / 2f, full = centre - 0.5f;
            float thin = frame < 2 ? 0.95f : 0.8f - 0.14f * (frame - 2);
            float hollow = frame < 4 ? 0 : full * (0.3f + 0.2f * (frame - 4));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    float edge = full * 0.84f * Wobble((float)Math.Atan2(v, u), size * 1.3f, frame * 0.8f, 0.18f);
                    if (distance > edge || distance < hollow || !Thin(x, y, thin)) continue;
                    bool lit = Lit(u, v, edge);
                    image[x, y] = frame < 2 ? (lit ? SmokeMid : SmokeDark) : lit ? SmokeLight : SmokeMid;
                }
            }
            return image;
        }

        // The head is the brightest pixel; the streak behind it steps down the ramp and thins.
        static PixelImage Ember(Rgba[] r, int length, int cooled)
        {
            length = Math.Clamp(length, 1, 8);
            cooled = Math.Clamp(cooled, 0, 2);
            var image = new PixelImage(length, 1);
            for (int x = 0; x < length; x++)
            {
                int back = length - 1 - x;
                if (back == 0) image[x, 0] = r[cooled];
                else
                {
                    float along = back / (float)length;
                    image[x, 0] = A(r[Math.Min(4, cooled + (along < 0.5f ? 1 : 2))], along < 0.5f ? 210 : 130);
                }
            }
            return image;
        }

        static PixelImage Shock(Rgba[] r, int diameter, int frame)
        {
            frame = Math.Clamp(frame, 0, 3);
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, outer = centre - 0.5f;
            int thick = frame == 0 ? 2 : 1;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float depth = outer - (float)Math.Sqrt(u * u + v * v);
                    if (depth < 0 || depth >= thick) continue;
                    if (frame == 2 && !Thin(x, y, 0.65f) || frame == 3 && !Thin(x, y, 0.35f)) continue;
                    image[x, y] = frame == 0 ? (depth < 1 ? r[0] : r[1]) : frame == 1 ? r[1] : frame == 2 ? r[2] : r[3];
                }
            }
            return image;
        }

        // Three bands, meeting in a pattern, the outer rim thinned: drawn with plain blending at a low alpha.
        static PixelImage Glow(Rgba[] r, int diameter)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float d = (float)Math.Sqrt(u * u + v * v) / centre;
                    if (d >= 1 || d > 0.92f && !Thin(x, y, 0.5f)) continue;
                    int band = d < 0.35f ? 0 : d < 0.65f ? 1 : 2;
                    if (d > 0.3f && d < 0.35f && Thin(x, y, 0.5f)) band = 1;
                    if (d > 0.6f && d < 0.65f && Thin(x, y, 0.5f)) band = 2;
                    image[x, y] = A(r[2], band == 0 ? 150 : band == 1 ? 85 : 40);
                }
            }
            return image;
        }

        // A split in the floor from its root at the left: two pixels thick for half its way, then bent and thin.
        static PixelImage Split(Rgba[] r, int length, int frame)
        {
            int variant = frame & 3, cooled = Math.Clamp((frame - 4) / 4, 0, 2);
            var five = r;
            var color = cooled == 0 ? five[2] : cooled == 1 ? A(five[0], 230) : new Rgba(34, 26, 24, 210);
            var hot = cooled == 0 ? five[3] : color;
            int half = Math.Max(3, length / 4);
            var image = new PixelImage(length, 2 * half + 1);
            float bend = (Noise(variant, length, 4) - 0.5f) * 0.9f, lean = (Noise(variant, length, 6) - 0.5f) * 0.3f;
            float y = half;
            for (int x = 0; x < length; x++)
            {
                bool root = x < length / 2;
                y += root ? lean : bend;
                if (PixelNoise.Hash(x, variant, length) % 4 == 0) y += (PixelNoise.Hash(x, variant, 9) & 1) == 0 ? 0.6f : -0.6f;
                int row = Math.Clamp((int)Math.Round(y), 0, 2 * half - 1);
                image[x, row] = root && x < length / 4 ? hot : color;
                if (root) image[x, row + 1] = color;
            }
            return image;
        }

        // A dark rock with its top lit, four shapes.
        static PixelImage Rock(Rgba[] r, int size, int kind)
        {
            size = Math.Clamp(size, 3, 6);
            var image = new PixelImage(size, size);
            var deep = new Rgba(58, 42, 32);
            float centre = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    if (u * u + v * v > centre * centre * (0.8f + 0.4f * Noise(x + kind, y, size))) continue;
                    image[x, y] = v < -centre * 0.35f ? r[1] : v > centre * 0.3f ? deep.Scale(0.7f) : deep;
                }
            }
            image[Math.Min(size - 1, size / 2 - 1 + (kind & 1)), size <= 3 ? 0 : 1] = r[2];
            return image;
        }

        // Soot: solid in the middle, its ragged rim thinned in a pattern, a little wider than tall.
        static PixelImage Soot(int diameter, int kind)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, full = centre - 0.5f;
            var burnt = new Rgba(26, 18, 18);
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = (y + 0.5f - centre) * 1.15f;
                    float angle = (float)Math.Atan2(v, u);
                    float edge = full * (0.8f + 0.2f * (float)Math.Sin(angle * 5 + kind) * (float)Math.Sin(angle * 3 + kind * 1.7f));
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    if (distance > edge) continue;
                    bool rim = distance > edge * 0.6f;
                    if (rim && distance > edge * 0.86f && !Thin(x, y, 0.5f)) continue;
                    image[x, y] = A(burnt, !rim || Thin(x, y, 0.6f) ? 150 : 80);
                }
            }
            return image;
        }

        // ---- ordinary monsters' pieces (E5) ----

        // A pool: its edge heaves with the frame, dark at the rim, the element's deep colour within, a lighter patch
        // off its middle and a few pale flecks on that.
        static PixelImage Puddle(Rgba[] r, int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, reach = centre - 1, phase = (frame & 3) * 1.5708f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float d = (float)Math.Sqrt(u * u + v * v), a = (float)Math.Atan2(v, u);
                    float edge = reach * (0.89f + 0.06f * (float)Math.Sin(a * 3 + phase) + 0.05f * (float)Math.Sin(a * 5 - phase + 1.3f));
                    float depth = edge - d;
                    if (depth < 0) continue;
                    if (depth < 1.5f)
                    {
                        image[x, y] = A(r[4], 230);
                        continue;
                    }
                    float du = u + reach * 0.12f, dv = v + reach * 0.1f, inner = reach * (0.52f + 0.07f * (float)Math.Sin(a * 4 - phase));
                    float dd = (float)Math.Sqrt(du * du + dv * dv);
                    if (dd < inner * 0.5f && Noise(x, y, 31 + (frame & 3)) > 0.82f) image[x, y] = r[1];
                    else if (dd < inner) image[x, y] = A(r[2], 215);
                    else image[x, y] = A(r[3], 215);
                }
            }
            return image;
        }

        // Dots of a ring, each about four pixels long with as much between: red with a dark pixel inside it so it
        // reads on any pool.
        static PixelImage Dashes(Rgba[] r, int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, outer = centre - 0.5f;
            int dots = Math.Max(6, (int)Math.Round(Math.PI * diameter / 8));
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float depth = outer - (float)Math.Sqrt(u * u + v * v);
                    if (depth < 0 || depth >= 2) continue;
                    double along = (Math.Atan2(v, u) / (2 * Math.PI) + 1) * dots + (frame & 1) * 0.5;
                    if (along - Math.Floor(along) > 0.5) continue;
                    image[x, y] = depth < 1 ? r[2] : A(r[0], 190);
                }
            }
            return image;
        }

        // ---- the players' skills (E4) ----

        // One colour, an edge that wavers with the frame.
        static PixelImage Lump(Rgba[] r, int diameter, int frame)
        {
            diameter = Math.Max(1, diameter);
            var color = r[Math.Clamp(frame >> 2, 0, 4)];
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, phase = (frame & 3) * 1.5708f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float edge = centre * (diameter < 5 ? 1 : Wobble((float)Math.Atan2(v, u), diameter, phase, 0.16f) * 0.88f);
                    if (u * u + v * v <= edge * edge) image[x, y] = color;
                }
            }
            return image;
        }

        // A thin patch of the element over the floor: deep colour thinned in a pattern, a lighter middle, pale flecks.
        static PixelImage Wash(Rgba[] r, int diameter, int kind)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, full = centre - 0.5f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float angle = (float)Math.Atan2(v, u), d = (float)Math.Sqrt(u * u + v * v);
                    float edge = full * (0.93f + 0.07f * (float)Math.Sin(angle * 7 + kind * 1.9f) * (float)Math.Sin(angle * 4 + kind));
                    if (d > edge) continue;
                    float k = d / edge;
                    if (k > 0.9f && !Thin(x, y, 0.5f)) continue;
                    if (k < 0.8f && Noise(x, y, 57 + kind) > 0.965f) image[x, y] = A(r[1], 200);
                    else if (k < 0.62f) image[x, y] = A(r[3], Thin(x, y, 0.5f) ? 120 : 90);
                    else image[x, y] = A(r[4], Thin(x, y, 0.6f) ? 130 : 95);
                }
            }
            return image;
        }

        // A blade's sweep turning counterclockwise, heavier than Crescent: a broad pale head with a white edge, the
        // band narrowing behind it and breaking into dots at its tail.
        static PixelImage Smear(Rgba[] r, int radius, int degrees)
        {
            float thick = Math.Clamp(radius * 0.26f, 5, 12);
            int size = 2 * (radius + 1) + 2;
            var image = new PixelImage(size, size);
            float centre = size / 2f, span = degrees / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = centre - (y + 0.5f);
                    float angle = (float)(Math.Atan2(v, u) * 180 / Math.PI);
                    if (Math.Abs(angle) > span) continue;
                    float along = (angle + span) / (2 * span);
                    // Rounded off at the very head.
                    float wide = thick * (float)Math.Pow(along, 1.1) * (along > 0.93f ? 0.35f + 0.65f * (1 - along) / 0.07f : 1);
                    float off = (float)Math.Sqrt(u * u + v * v) - radius;
                    if (off > 0.5f || off < -wide - 0.5f) continue;
                    float across = -off / Math.Max(1, wide);
                    if (along < 0.22f && ((x + y) & 3) != 0) continue;
                    if (along < 0.4f && ((x + y) & 1) == 0) continue;
                    if (across < 0.28f) image[x, y] = along > 0.5f ? r[0] : r[1];
                    else if (across < 0.6f) image[x, y] = along > 0.7f ? r[0] : along > 0.4f ? r[1] : r[2];
                    else if (across < 0.86f) image[x, y] = along > 0.6f ? r[1] : r[2];
                    else if (((x + y) & 1) == 0) image[x, y] = A(r[3], 200);
                }
            }
            return image;
        }

        // ---- the bosses' own pieces (E6) ----

        // A stretch of a beam: its half width ripples along its length with a period of the stretch, so stretches
        // join; from the middle out a pale core, the element's light and mid colours and a darker rim.
        static PixelImage Beam(Rgba[] r, int thick, int frame)
        {
            const int length = 32;
            thick = Math.Clamp(thick, 1, 48);
            int half = thick / 2 + 3;
            var image = new PixelImage(length, 2 * half + 1);
            float phase = (frame & 3) * (float)Math.PI / 2;
            for (int x = 0; x < length; x++)
            {
                if (thick <= 2)
                {
                    image[x, half] = r[thick == 1 ? 2 : 1];
                    continue;
                }
                float ripple = thick < 8 ? 0.5f * (float)Math.Sin(x * Math.PI / 4 - phase)
                    : 1.2f * (float)Math.Sin(x * Math.PI / 8 - phase) + 0.8f * (float)Math.Sin(x * Math.PI / 4 + 2 * phase);
                float reach = Math.Max(1, thick / 2f + ripple);
                for (int dy = -half; dy <= half; dy++)
                {
                    float u = Math.Abs(dy) / reach;
                    if (u > 1) continue;
                    image[x, half + dy] = u < 0.3f ? r[0] : u < 0.62f ? r[1] : u < 0.85f ? r[2] : r[3];
                }
            }
            return image;
        }

        static PixelImage Spear(Rgba[] r, int length, int frame)
        {
            length = Math.Max(8, length);
            var image = new PixelImage(length, 5);
            bool after = frame == 1;
            int head = Math.Clamp(length / 3, 5, 9);
            for (int x = 0; x < length - head; x++) image[x, 2] = after ? A(r[3], 150) : x % 6 == 5 ? r[3] : r[2];
            for (int x = length - head; x < length; x++)
            {
                float along = (x - (length - head)) / (float)(head - 1);
                int reach = along < 0.3f ? (int)Math.Round(along / 0.3f * 2) : (int)Math.Round((1 - along) / 0.7f * 2);
                for (int dy = -reach; dy <= reach; dy++) image[x, 2 + dy] = after ? A(r[3], 150) : dy == 0 ? r[0] : dy < 0 ? r[1] : r[2];
            }
            return image;
        }

        // Grains strung along a line, close and bright at the head (the right), thinning behind it.
        static PixelImage Grains(Rgba[] r, int length, int frame)
        {
            length = Math.Clamp(length, 4, 48);
            var image = new PixelImage(length, 5);
            for (int x = 0; x < length; x++)
            {
                float along = x / (float)(length - 1);
                for (int row = 0; row < 5; row++)
                {
                    int away = Math.Abs(row - 2);
                    // More of them near the head and near the middle of the pour.
                    float chance = along * along * (away == 0 ? 0.95f : away == 1 ? 0.6f : 0.22f);
                    if (Noise(x + (frame & 3) * 5, row, 77 + length) >= chance) continue;
                    image[x, row] = away == 0 && along > 0.75f ? r[0] : Noise(x, row, 78) < 0.5f ? r[1] : away == 2 ? r[3] : r[2];
                }
            }
            return image;
        }
    }
}
