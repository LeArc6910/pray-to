using System;

namespace Game.Client
{
    // The pieces a skill's effect is put together from (SkillFx, Docs/SkillEffects.md): flames, shards of ice, bolts,
    // crescents of steel, cracks, wisps and so on. Each is drawn as shades 0 to 3 (dark, mid, light, core) and coloured
    // by an element's short ramp, so one drawing serves every element and each element keeps a mid-range body with its
    // brightest value only at a core or a tip. Hard-edged, a world pixel a pixel, like everything else.
    enum FxShape : byte
    {
        // A thin crisp ring: size its diameter, frame its thickness (1 to 3).
        Edge,
        // The same ring broken into arcs (BrokenFrame).
        Broken,
        // A tongue of flame, size its height, four frames of flicker.
        Flame,
        // A square of size pixels in shade frame.
        Dot,
        // A puff of smoke, dust or mist: frames 0 to 3 thinning out.
        Smoke,
        // An explosion's ball: frames 0 to 5 from a bright core to dark scraps.
        Blob,
        // A sharp crystal pointing +x, size its length; frame 1 is a lance with a bright spine.
        Shard,
        // A jagged bolt along +x, size its length; frames 0 to 5 are different bolts, +8 a thick one.
        Bolt,
        // An arc of steel round +x, size its radius, frame how many degrees wide.
        Crescent,
        // A crack in the ground along +x, size its length, four kinds.
        Crack,
        // A bit of rubble, size 2 to 5.
        Chunk,
        // A streak pointing +x with its bright head at the right, size its length, frame its thickness (1 to 3).
        Streak,
        // An arrow pointing +x.
        Arrow,
        // A plus sign, size 3 or 5.
        Plus,
        // A wisp with its round head at the right and a wavering tail, size the head's diameter.
        Wisp,
        // A rune circle on the ground, size its diameter, frame its turn.
        Rune,
        // A four-pointed glint, size its span; frame 0 thin, 1 with a bright heart, 2 with diagonal ticks.
        Glint,
        // Four arcs with ticks pointing in, size its diameter.
        Reticle,
        // A ball of fire with its head at the right and a tail of flame, size the head's diameter, three frames.
        Fireball,
        // A burn left on the ground, size its diameter.
        Scorch,
        // A shadow on the ground, size its width.
        Shadow,
        // A dashed line along +x, size its length, four frames walking along it.
        Tether,
        // A sword's blade pointing +x, size its length.
        Blade,
        // A leaf, two frames.
        Leaf,
        // The caster's own picture, tinted (the view fills it in; nothing is drawn here).
        Ghost,
    }

    // An element's colours, dark to core.
    enum FxRamp : byte
    {
        Fire,
        Cold,
        Bolt,
        Steel,
        Dust,
        Spirit,
        Heal,
        Wind,
        Smoke,
        Gold,
        // Pale mist, for what cold leaves behind.
        Mist,
    }

    static class SkillFxArt
    {
        // Dark, mid, light, core. The cores are pale, never pure white over a large area; the darks stay off black
        // (Docs/SkillEffects.md, rule 4). Fire leans to amber and yellow, away from the red of a boss's telegraph.
        static readonly Rgba[][] Ramps =
        {
            new[] { Rgba.Hex("#8A3414"), Rgba.Hex("#E8701E"), Rgba.Hex("#FFB42E"), Rgba.Hex("#FFF0B0") },
            new[] { Rgba.Hex("#3A6EA8"), Rgba.Hex("#6FB8E8"), Rgba.Hex("#B4E6FA"), Rgba.Hex("#F0FBFF") },
            new[] { Rgba.Hex("#B08A28"), Rgba.Hex("#F0D048"), Rgba.Hex("#FFF4A0"), Rgba.Hex("#FFFFF0") },
            new[] { Rgba.Hex("#5A6478"), Rgba.Hex("#A8B4C8"), Rgba.Hex("#DCE4F0"), Rgba.Hex("#FFFFFF") },
            new[] { Rgba.Hex("#6E5E4A"), Rgba.Hex("#A8947A"), Rgba.Hex("#CDBBA0"), Rgba.Hex("#E6DAC4") },
            new[] { Rgba.Hex("#1E7A78"), Rgba.Hex("#3CC8B4"), Rgba.Hex("#8CF0DC"), Rgba.Hex("#E0FFF6") },
            new[] { Rgba.Hex("#2E8A3A"), Rgba.Hex("#5CD860"), Rgba.Hex("#C8F07A"), Rgba.Hex("#FFF6B0") },
            new[] { Rgba.Hex("#6E9A8A"), Rgba.Hex("#A8D4C0"), Rgba.Hex("#D8F0E4"), Rgba.Hex("#FFFFFF") },
            new[] { Rgba.Hex("#3A3440"), Rgba.Hex("#5A525E"), Rgba.Hex("#7C7280"), Rgba.Hex("#9A909C") },
            new[] { Rgba.Hex("#9A5A1E"), Rgba.Hex("#E0A030"), Rgba.Hex("#FFD860"), Rgba.Hex("#FFF6C0") },
            new[] { Rgba.Hex("#8AA8C0"), Rgba.Hex("#B4CCDC"), Rgba.Hex("#DCEAF2"), Rgba.Hex("#F4FAFF") },
        };

        public static Rgba[] Ramp(FxRamp ramp) => Ramps[(int)ramp];

        // Which arcs a broken ring has: its thickness (1 to 3), how many arcs (3 to 8) and a turn of some steps.
        public static int BrokenFrame(int thick, int arcs, int turn = 0) => (Math.Clamp(thick, 1, 3) - 1) + 3 * (Math.Clamp(arcs, 3, 8) - 3) + 18 * (turn & 7);

        // Ring sizes in steps (two pixels while small, four when large), so a growing ring makes few pictures.
        public static int Even(float diameter) => diameter <= 48 ? Math.Max(4, 2 * (int)Math.Round(diameter / 2)) : Math.Min(160, 4 * (int)Math.Round(diameter / 4));

        public static int Step4(float length) => Math.Clamp(4 * (int)Math.Round(length / 4), 4, 128);

        public static PixelImage Draw(FxShape shape, FxRamp ramp, int size, int frame)
        {
            var r = Ramp(ramp);
            size = Math.Max(1, size);
            switch (shape)
            {
                case FxShape.Edge: return Ring(r, size, Math.Clamp(frame, 1, 3), 0, 0);
                case FxShape.Broken: return Ring(r, size, 1 + frame % 3, 3 + frame / 3 % 6, frame / 18);
                case FxShape.Flame: return Flame(r, size, frame & 3);
                case FxShape.Dot: return Dot(r, size, frame);
                case FxShape.Smoke: return Smoke(r, size, frame);
                case FxShape.Blob: return Blob(r, size, frame);
                case FxShape.Shard: return Shard(r, size, frame);
                case FxShape.Bolt: return Bolt(r, size, frame);
                case FxShape.Crescent: return Crescent(r, size, frame);
                case FxShape.Crack: return Crack(r, size, frame);
                case FxShape.Chunk: return Chunk(r, size, frame);
                case FxShape.Streak: return Streak(r, size, Math.Clamp(frame, 1, 3));
                case FxShape.Arrow: return Arrow(r, size);
                case FxShape.Plus: return Plus(r, size);
                case FxShape.Wisp: return Wisp(r, size, frame);
                case FxShape.Rune: return Rune(r, size, frame);
                case FxShape.Glint: return Glint(r, size, frame);
                case FxShape.Reticle: return Reticle(r, size);
                case FxShape.Fireball: return Fireball(r, size, frame);
                case FxShape.Scorch: return Scorch(r, size, frame);
                case FxShape.Shadow: return Shadow(size);
                case FxShape.Tether: return Tether(r, size, frame);
                case FxShape.Blade: return Blade(r, size);
                case FxShape.Leaf: return Leaf(r, frame);
                default: return null;
            }
        }

        static Rgba A(Rgba c, int alpha) => c.WithAlpha(alpha);

        static float Noise(int a, int b, int c) => PixelNoise.Value(a, b, c);

        // A hard bright edge at the true radius and a thin band fading inward, the floor showing through the middle:
        // the area reads by its rim, not as a filled hoop (rule 5).
        static PixelImage Ring(Rgba[] r, int diameter, int thick, int arcs, int turn)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, outer = centre - 0.5f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float depth = outer - (float)Math.Sqrt(u * u + v * v);
                    if (depth < 0 || depth >= thick + 1) continue;
                    if (arcs > 0)
                    {
                        double along = (Math.Atan2(v, u) / (2 * Math.PI) + 1 + turn / 24.0) * arcs;
                        if (along - Math.Floor(along) > 0.62) continue;
                    }
                    image[x, y] = depth < 1 ? (thick >= 3 ? r[3] : r[2]) : depth < thick ? A(r[1], 200) : A(r[0], 120);
                }
            }
            return image;
        }

        // Wide near its foot, drawn up to a tip that leans a different way each frame; hot at the foot's middle.
        static PixelImage Flame(Rgba[] r, int height, int frame)
        {
            int width = Math.Max(3, height * 2 / 3) | 1;
            var image = new PixelImage(width, height);
            for (int y = 0; y < height; y++)
            {
                float v = 1 - (y + 0.5f) / height;
                float profile = v < 0.3f ? 0.55f + 0.45f * (v / 0.3f) : (float)Math.Pow(1 - (v - 0.3f) / 0.7f, 1.2);
                float half = Math.Max(0.5f, profile * width / 2f);
                float middle = width / 2f + (float)Math.Sin(v * 3.5 + frame * 1.9) * v * width * 0.22f;
                for (int x = 0; x < width; x++)
                {
                    float across = Math.Abs(x + 0.5f - middle) / half;
                    if (across > 1) continue;
                    if (across > 0.7f && height > 5 && PixelNoise.Hash(x, y, frame + 11) % 4 == 0) continue;
                    float heat = (1 - across) * (1 - v * 0.9f);
                    image[x, y] = heat > 0.62f ? r[3] : heat > 0.32f ? r[2] : heat > 0.1f ? r[1] : r[0];
                }
            }
            return image;
        }

        static PixelImage Dot(Rgba[] r, int size, int shade)
        {
            var image = new PixelImage(size, size);
            image.Fill(0, 0, size, size, r[Math.Clamp(shade, 0, 3)]);
            return image;
        }

        // Three lobes run together, lit from the upper left; later frames are eaten away.
        static PixelImage Smoke(Rgba[] r, int size, int frame)
        {
            var image = new PixelImage(size, size);
            float centre = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = false, lit = false, low = false;
                    for (int lobe = 0; lobe < 3; lobe++)
                    {
                        float lx = centre + (Noise(lobe, size, 3) - 0.5f) * size * 0.4f, ly = centre + (Noise(lobe, size, 5) - 0.5f) * size * 0.4f;
                        float radius = size * (0.26f + 0.1f * Noise(lobe, size, 7));
                        float u = x + 0.5f - lx, v = y + 0.5f - ly;
                        if (u * u + v * v > radius * radius) continue;
                        inside = true;
                        lit |= u + v < -radius * 0.45f;
                        low |= u + v > radius * 0.6f;
                    }
                    if (!inside) continue;
                    if (frame >= 2 && ((x + y) & 1) == 0 && (frame >= 3 || !lit)) continue;
                    if (frame >= 3 && PixelNoise.Hash(x, y, size) % 3 == 0) continue;
                    image[x, y] = lit ? r[2] : low ? r[0] : r[1];
                }
            }
            return image;
        }

        // A bright core, then a ball that fills out with a ragged edge, hollows from the middle and breaks to scraps.
        static PixelImage Blob(Rgba[] r, int size, int frame)
        {
            frame = Math.Clamp(frame, 0, 5);
            var image = new PixelImage(size, size);
            float centre = size / 2f, full = centre - 0.5f;
            float outer = full * (frame == 0 ? 0.45f : frame == 1 ? 0.75f : 0.85f + 0.03f * frame);
            float hollow = frame < 3 ? 0 : outer * (0.3f + 0.2f * (frame - 3));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    int sector = (int)((Math.Atan2(v, u) / (2 * Math.PI) + 1) * 12) % 12;
                    float edge = outer * (frame < 2 ? 1 : 0.82f + 0.18f * Noise(sector, frame, size));
                    if (distance > edge || distance < hollow) continue;
                    float band = distance / Math.Max(1, edge);
                    Rgba color;
                    switch (frame)
                    {
                        case 0: color = band < 0.7f ? r[3] : r[2]; break;
                        case 1: color = band < 0.4f ? r[3] : band < 0.8f ? r[2] : r[1]; break;
                        case 2: color = band < 0.2f ? r[3] : band < 0.6f ? r[2] : r[1]; break;
                        case 3: color = band < 0.75f ? r[1] : r[0]; break;
                        case 4:
                            if (((x + y) & 1) == 0) continue;
                            color = band < 0.8f ? r[1] : r[0];
                            break;
                        default:
                            if (PixelNoise.Hash(x, y, size) % 3 != 0) continue;
                            color = r[0];
                            break;
                    }
                    image[x, y] = color;
                }
            }
            return image;
        }

        // A kite: widest a third along, light above its spine and mid below, its tip the brightest pixels.
        static PixelImage Shard(Rgba[] r, int length, int kind)
        {
            int half = Math.Max(1, length / (kind == 1 ? 8 : 6));
            var image = new PixelImage(length, 2 * half + 1);
            float widest = length * 0.35f;
            for (int x = 0; x < length; x++)
            {
                float reach = x < widest ? half * (0.4f + 0.6f * x / widest) : half * (1 - (x - widest) / (length - widest));
                int rows = (int)Math.Round(reach);
                for (int dy = -rows; dy <= rows; dy++)
                {
                    bool tip = x >= length - Math.Max(2, length / 4);
                    image[x, half + dy] = x < 1 && length > 4 ? r[0] : dy == 0 ? (tip || kind == 1 && x > length / 2 ? r[3] : r[2]) : dy < 0 ? r[2] : r[1];
                }
            }
            return image;
        }

        // Lightning by midpoint displacement (Docs/SkillEffects.md, source 4): the line from end to end is halved again
        // and again, each midpoint pushed aside by less; a branch or two fork off dimmer.
        static PixelImage Bolt(Rgba[] r, int length, int frame)
        {
            bool thick = frame >= 8;
            int kind = frame & 7;
            int sway = Math.Clamp((int)(length * 0.16f), 2, 9);
            int branch = length >= 12 ? Math.Min(14, (int)(length * 0.3f)) : 0;
            int half = sway + branch + 2;
            var image = new PixelImage(length, 2 * half + 1);
            var offsets = new float[length + 1];
            Displace(offsets, 0, length, sway, kind * 131 + length);
            int last = half;
            for (int x = 0; x < length; x++)
            {
                int y = half + (int)Math.Round(offsets[x]);
                for (int step = Math.Min(last, y); step <= Math.Max(last, y); step++)
                {
                    image[x, step] = r[3];
                    if (thick && image.Inside(x, step + 1)) image[x, step + 1] = r[2];
                }
                last = y;
            }
            int forks = length >= 20 ? 2 : length >= 12 ? 1 : 0;
            for (int fork = 0; fork < forks; fork++)
            {
                int from = (int)(length * (0.25f + 0.45f * Noise(fork, kind, length)));
                float slope = (0.5f + 0.4f * Noise(fork, kind, length + 1)) * ((PixelNoise.Hash(fork, kind, length) & 1) == 0 ? 1 : -1);
                int run = Math.Max(3, (int)(length * (0.2f + 0.15f * Noise(fork, kind, length + 2))));
                float y = half + offsets[from];
                for (int step = 1; step <= run && from + step < length; step++)
                {
                    y += slope + (Noise(fork * 7 + step, kind, length) - 0.5f) * 1.6f;
                    int row = (int)Math.Round(y);
                    if (image.Inside(from + step, row) && !image.IsSet(from + step, row)) image[from + step, row] = step > run * 0.6f ? r[1] : r[2];
                }
            }
            return image;
        }

        static void Displace(float[] offsets, int from, int to, float sway, int seed)
        {
            if (to - from < 2) return;
            int middle = (from + to) / 2;
            offsets[middle] = (offsets[from] + offsets[to]) / 2 + (Noise(from, to, seed) - 0.5f) * 2 * sway;
            Displace(offsets, from, middle, sway * 0.55f, seed);
            Displace(offsets, middle, to, sway * 0.55f, seed);
        }

        // A blade's sweep, turning counterclockwise: thickest and brightest at its leading end, drawn out to nothing
        // behind, so it reads as a smear of motion and not as a hoop.
        static PixelImage Crescent(Rgba[] r, int radius, int degrees)
        {
            int half = Math.Clamp(radius / 6, 3, 7);
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
                    float wide = half * (float)Math.Pow(along, 0.8) * (along > 0.94f ? (1 - along) / 0.06f * 0.6f + 0.4f : 1);
                    float off = (float)Math.Sqrt(u * u + v * v) - radius;
                    // The bright edge is the outer one; the band trails inward.
                    if (off > 0.5f || off < -wide - 0.35f) continue;
                    float across = -off / Math.Max(1, wide);
                    if (along < 0.3f && ((x + y) & 1) == 0) continue;
                    if (across < 0.3f) image[x, y] = along > 0.6f ? r[3] : r[2];
                    else if (across < 0.62f) image[x, y] = along > 0.45f ? r[2] : r[1];
                    else if (across < 0.9f) image[x, y] = A(r[1], 220);
                    else if (((x + y) & 1) == 0) image[x, y] = A(r[0], 160);
                }
            }
            return image;
        }

        // A dark split wandering along +x, two pixels wide near its start, with a pale lip so it shows on dark floors.
        static PixelImage Crack(Rgba[] r, int length, int kind)
        {
            const int half = 6;
            var image = new PixelImage(length, 2 * half + 1);
            var dark = new Rgba(34, 26, 24);
            int y = half;
            for (int x = 0; x < length; x++)
            {
                uint roll = PixelNoise.Hash(x, kind, length) % 5;
                if (roll == 0 && y > 2) y--;
                else if (roll == 1 && y < 2 * half - 2) y++;
                image[x, y] = dark;
                if (x < length * 0.45f) image[x, y + 1] = dark;
                if (!image.IsSet(x, y - 1)) image[x, y - 1] = A(r[2], x < length * 0.7f ? 200 : 120);
                // A short side split.
                if (x == length / 3 || x == length * 2 / 3)
                {
                    int side = (PixelNoise.Hash(x, kind, 9) & 1) == 0 ? 1 : -1;
                    for (int step = 1; step <= 3 && x + step < length; step++)
                    {
                        int row = y + side * (step + 1);
                        if (image.Inside(x + step, row)) image[x + step, row] = dark;
                    }
                }
            }
            return image;
        }

        static PixelImage Chunk(Rgba[] r, int size, int kind)
        {
            size = Math.Clamp(size, 2, 5);
            var image = new PixelImage(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                    if (corner && size > 2 && PixelNoise.Hash(x, y, kind) % 3 != 0) continue;
                    image[x, y] = y == 0 ? r[2] : y == size - 1 ? r[0] : r[1];
                }
            }
            return image;
        }

        // The brightest pixels at the leading end, the tail darker and broken (rule 3).
        static PixelImage Streak(Rgba[] r, int length, int thick)
        {
            int half = thick >= 2 ? 1 : 0;
            var image = new PixelImage(length, 2 * half + 1);
            for (int x = 0; x < length; x++)
            {
                float u = (x + 1f) / length;
                if (u < 0.25f && (x & 1) == 1) continue;
                image[x, half] = u > 0.85f ? r[3] : u > 0.55f ? r[2] : u > 0.25f ? r[1] : r[0];
                if (half == 0 || u <= (thick >= 3 ? 0.3f : 0.55f)) continue;
                image[x, 0] = u > 0.8f ? r[2] : r[1];
                image[x, 2] = u > 0.8f ? r[1] : r[0];
            }
            return image;
        }

        static PixelImage Arrow(Rgba[] r, int length)
        {
            length = Math.Max(7, length);
            var image = new PixelImage(length, 3);
            var shaft = new Rgba(150, 112, 70);
            for (int x = 1; x < length - 3; x++) image[x, 1] = shaft;
            image[0, 0] = image[0, 2] = image[1, 0] = image[1, 2] = r[2];
            image[0, 1] = r[1];
            image[length - 3, 0] = image[length - 3, 2] = r[1];
            image[length - 3, 1] = r[1];
            image[length - 2, 1] = r[2];
            image[length - 1, 1] = r[3];
            return image;
        }

        static PixelImage Plus(Rgba[] r, int size)
        {
            size = size >= 5 ? 5 : 3;
            var image = new PixelImage(size, size);
            int middle = size / 2;
            for (int i = 0; i < size; i++)
            {
                var color = i == 0 || i == size - 1 ? (size == 5 ? r[1] : r[2]) : r[2];
                image[i, middle] = color;
                image[middle, i] = color;
            }
            image[middle, middle] = r[3];
            return image;
        }

        static PixelImage Wisp(Rgba[] r, int head, int frame)
        {
            int tail = head * 3 / 2;
            int width = head + tail, height = head + 4;
            var image = new PixelImage(width, height);
            float cx = width - head / 2f, cy = height / 2f, radius = head / 2f;
            for (int x = 0; x < tail + head / 2; x++)
            {
                float u = (float)x / (tail + head / 2f);
                float reach = radius * u * 0.9f;
                float middle = cy + (float)Math.Sin(x * 0.7 + frame * 1.5) * 1.3f * (1 - u);
                for (int y = 0; y < height; y++)
                {
                    if (Math.Abs(y + 0.5f - middle) > Math.Max(0.5f, reach)) continue;
                    if (u < 0.3f && ((x + y) & 1) == 0) continue;
                    image[x, y] = u < 0.4f ? r[0] : u < 0.75f ? r[1] : r[2];
                }
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x + 0.5f - cx, v = y + 0.5f - cy;
                    float distance = (float)Math.Sqrt(u * u + v * v) / radius;
                    if (distance > 1) continue;
                    image[x, y] = distance < 0.4f ? r[3] : distance < 0.8f ? r[2] : r[1];
                }
            }
            return image;
        }

        // A circle, a dashed circle inside it and a triangle inside that, turned a step each frame.
        static PixelImage Rune(Rgba[] r, int diameter, int frame)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, outer = centre - 0.5f, inner = outer * 0.72f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    if (Math.Abs(distance - (outer - 0.5f)) <= 0.5f) image[x, y] = r[2];
                    else if (Math.Abs(distance - inner) <= 0.5f)
                    {
                        double along = (Math.Atan2(v, u) / (2 * Math.PI) + 1 + frame / 48.0) * 12;
                        if (along - Math.Floor(along) < 0.6) image[x, y] = r[1];
                    }
                }
            }
            double turn = frame * Math.PI / 24;
            for (int corner = 0; corner < 3; corner++)
            {
                double a = turn + corner * 2 * Math.PI / 3, b = turn + (corner + 1) * 2 * Math.PI / 3;
                int x0 = (int)(centre + Math.Cos(a) * (inner - 1)), y0 = (int)(centre + Math.Sin(a) * (inner - 1));
                Line(image, x0, y0, (int)(centre + Math.Cos(b) * (inner - 1)), (int)(centre + Math.Sin(b) * (inner - 1)), A(r[1], 220));
                image[x0, y0] = r[3];
            }
            return image;
        }

        static void Line(PixelImage image, int x0, int y0, int x1, int y1, Rgba color)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (int)Math.Round((x1 - x0) * (float)i / Math.Max(1, steps)), y = y0 + (int)Math.Round((y1 - y0) * (float)i / Math.Max(1, steps));
                if (image.Inside(x, y)) image[x, y] = color;
            }
        }

        static PixelImage Glint(Rgba[] r, int span, int frame)
        {
            span |= 1;
            var image = new PixelImage(span, span);
            int middle = span / 2;
            for (int i = 0; i < span; i++)
            {
                var color = Math.Abs(i - middle) > middle / 2 ? r[1] : r[2];
                image[i, middle] = color;
                image[middle, i] = color;
            }
            image[middle, middle] = r[3];
            if (frame >= 1 && span >= 5)
            {
                image[middle - 1, middle] = image[middle + 1, middle] = image[middle, middle - 1] = image[middle, middle + 1] = r[3];
            }
            if (frame >= 2 && span >= 7)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    image[middle + side, middle + side] = r[2];
                    image[middle + side, middle - side] = r[2];
                }
            }
            return image;
        }

        // Unlike the white corner brackets of the build target (EffectArt.TargetFrame): round, gold, ticks pointing in.
        static PixelImage Reticle(Rgba[] r, int diameter)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, outer = centre - 0.5f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    double quarter = (Math.Atan2(v, u) / (Math.PI / 2) + 4) % 1;
                    if (Math.Abs(distance - (outer - 0.5f)) <= 0.5f && quarter > 0.22 && quarter < 0.78) image[x, y] = r[2];
                    else if (distance < outer - 1 && distance > outer - 4 && Math.Abs(quarter - 0.5) < 0.5 / Math.Max(4, distance)) image[x, y] = r[3];
                }
            }
            return image;
        }

        static PixelImage Fireball(Rgba[] r, int head, int frame)
        {
            int width = head * 2, height = head + 2;
            var image = new PixelImage(width, height);
            float cx = width - head / 2f, cy = height / 2f, radius = head / 2f;
            for (int x = 0; x < width - head / 2; x++)
            {
                float u = 1 - (float)x / (width - head / 2f);
                float reach = radius * (float)Math.Pow(1 - u, 1.3);
                float middle = cy + (float)Math.Sin(u * 5 + frame * 2.1) * 1.2f * u;
                for (int y = 0; y < height; y++)
                {
                    if (Math.Abs(y + 0.5f - middle) > Math.Max(0.5f, reach)) continue;
                    if (u > 0.6f && PixelNoise.Hash(x, y, frame) % 3 == 0) continue;
                    image[x, y] = u < 0.35f ? r[2] : u < 0.7f ? r[1] : r[0];
                }
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x + 0.5f - cx, v = y + 0.5f - cy;
                    float distance = (float)Math.Sqrt(u * u + v * v) / radius;
                    if (distance > 1 || distance > 0.8f && PixelNoise.Hash(x, y, frame + 3) % 5 == 0) continue;
                    image[x, y] = distance < 0.45f ? r[3] : distance < 0.8f ? r[2] : r[1];
                }
            }
            return image;
        }

        static PixelImage Scorch(Rgba[] r, int diameter, int kind)
        {
            var image = new PixelImage(diameter, diameter);
            float centre = diameter / 2f, full = centre - 0.5f;
            var burnt = new Rgba(30, 22, 20);
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float u = x + 0.5f - centre, v = y + 0.5f - centre;
                    float distance = (float)Math.Sqrt(u * u + v * v);
                    int sector = (int)((Math.Atan2(v, u) / (2 * Math.PI) + 1) * 16) % 16;
                    float edge = full * (0.78f + 0.22f * Noise(sector, kind, diameter));
                    if (distance > edge) continue;
                    if (distance > edge * 0.6f && ((x + y) & 1) == 0) continue;
                    image[x, y] = A(burnt, distance < edge * 0.6f ? 170 : 120);
                    if (PixelNoise.Hash(x, y, kind + 5) % 37 == 0 && distance < edge * 0.8f) image[x, y] = r[0];
                }
            }
            return image;
        }

        static PixelImage Shadow(int width)
        {
            int height = Math.Max(2, width / 2);
            var image = new PixelImage(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f - width / 2f) / (width / 2f), v = (y + 0.5f - height / 2f) / (height / 2f);
                    if (u * u + v * v <= 1) image[x, y] = new Rgba(12, 10, 18, 110);
                }
            }
            return image;
        }

        static PixelImage Tether(Rgba[] r, int length, int frame)
        {
            var image = new PixelImage(length, 1);
            for (int x = 0; x < length; x++)
            {
                int along = ((x - frame) % 12 + 12) % 12;
                if (along % 4 < 2) image[x, 0] = along == 0 ? r[3] : r[2];
            }
            return image;
        }

        static PixelImage Blade(Rgba[] r, int length)
        {
            length = Math.Max(7, length);
            var image = new PixelImage(length, 5);
            var hilt = new Rgba(168, 128, 60);
            image[0, 2] = image[1, 2] = hilt;
            for (int y = 0; y < 5; y++) image[2, y] = y == 2 ? hilt : hilt.Scale(0.75f);
            for (int x = 3; x < length; x++)
            {
                image[x, 2] = x >= length - 2 ? r[3] : r[2];
                if (x < length - 2) image[x, 1] = r[1];
                if (x < length - 3) image[x, 3] = r[0];
            }
            return image;
        }

        static PixelImage Leaf(Rgba[] r, int frame)
        {
            var image = new PixelImage(3, 2);
            var dark = new Rgba(74, 132, 62);
            var light = new Rgba(142, 196, 96);
            if ((frame & 1) == 0)
            {
                image[0, 0] = dark;
                image[1, 0] = light;
                image[1, 1] = dark;
                image[2, 1] = light;
            }
            else
            {
                image[2, 0] = dark;
                image[1, 0] = light;
                image[1, 1] = dark;
                image[0, 1] = light;
            }
            return image;
        }
    }
}
