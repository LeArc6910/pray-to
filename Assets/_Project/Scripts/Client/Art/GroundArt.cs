using System;
using Game.Core;

namespace Game.Client
{
    // A boss's ground and the things in its arena (2026-11-28, R2, Docs/Bosses.md "지형·물체·분노", "R2a 그림"), drawn at
    // 16 pixels a tile in the lava biome's colours (TileArt.Lava, LavaTitanArt): lava (a dark crust at the bank, plates
    // of crust, a molten body whose gold veins drift and whose bubbles swell and burst over the four frames), cooled
    // rock (black plates, the cracks at its heart still red), shallow water (rings going out to a pale bank), a crack
    // (a jagged black fissure with fire running along its heart); the edge band that closes in from phase 3 (lava with
    // a ragged bank; petals and webs are R2b's); a vent (a black mouth ringed with rock, the glow in it breathing).
    // WorldView.Ground places them.
    static class GroundArt
    {
        // Frames each kind turns through, a loop.
        public const int Frames = 4;

        static readonly Rgba LavaTip = new Rgba(255, 232, 128, 245);
        static readonly Rgba LavaHot = new Rgba(255, 210, 74, 245);
        static readonly Rgba LavaMain = new Rgba(255, 122, 30, 240);
        static readonly Rgba LavaDeep = new Rgba(216, 64, 26, 240);
        static readonly Rgba LavaCrust = new Rgba(90, 36, 22, 245);
        static readonly Rgba LavaCrustDark = new Rgba(46, 18, 12, 250);
        static readonly Rgba RockRim = new Rgba(14, 12, 16, 250);
        static readonly Rgba RockShade = new Rgba(30, 26, 30, 250);
        static readonly Rgba RockMain = new Rgba(50, 44, 48, 250);
        static readonly Rgba RockMid = new Rgba(66, 58, 60, 250);
        static readonly Rgba RockLight = new Rgba(104, 94, 94, 250);
        static readonly Rgba RockEmber = new Rgba(150, 46, 20, 250);
        static readonly Rgba RockEmberDim = new Rgba(104, 32, 20, 250);
        static readonly Rgba WaterRim = new Rgba(34, 70, 122, 215);
        static readonly Rgba WaterDeep = new Rgba(58, 108, 172, 170);
        static readonly Rgba WaterMain = new Rgba(84, 146, 204, 155);
        static readonly Rgba WaterLight = new Rgba(176, 218, 246, 200);
        static readonly Rgba WaterGlint = new Rgba(232, 246, 255, 225);
        static readonly Rgba CrackDark = new Rgba(24, 10, 10, 245);
        static readonly Rgba CrackEdge = new Rgba(62, 22, 14, 235);
        static readonly Rgba CrackDim = new Rgba(184, 58, 20, 245);
        static readonly Rgba CrackGlow = new Rgba(255, 106, 32, 245);
        static readonly Rgba CrackCore = new Rgba(255, 192, 64, 245);
        static readonly Rgba PetalPink = new Rgba(250, 182, 205, 210);
        static readonly Rgba PetalDeep = new Rgba(214, 112, 150, 220);
        static readonly Rgba WebWhite = new Rgba(236, 236, 244, 170);

        // One pool (a circle of width) or strip (width along, height across), in pixels.
        public static PixelImage Ground(HazardShape shape, bool strip, int width, int height, int variant, int frame)
        {
            width = Math.Max(4, width);
            height = Math.Max(4, height);
            frame = (frame % Frames + Frames) % Frames;
            var image = new PixelImage(width, height);
            float cx = width / 2f - 0.5f, cy = height / 2f - 0.5f;
            float half = Math.Min(width, height) / 2f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // In: pixels in from its ragged rim. d: 0 at the middle, 1 at the rim.
                    float d, inside;
                    if (strip)
                    {
                        float across = Math.Abs(y - cy) / (height / 2f) + 0.08f * Wobble(x, variant);
                        float end = width / 2f - Math.Abs(x - cx) - 0.8f - 0.8f * Wobble(y * 3, variant + 5);
                        inside = Math.Min((1 - across) * height / 2f, end);
                        d = Math.Max(across, 1 - end / (width / 2f));
                    }
                    else
                    {
                        float dx = (x - cx) / (width / 2f), dy = (y - cy) / (height / 2f);
                        double angle = Math.Atan2(dy, dx);
                        float reach = 0.93f + 0.05f * (float)Math.Sin(angle * 5 + variant) + 0.03f * (float)Math.Sin(angle * 11 + variant * 2);
                        d = (float)Math.Sqrt(dx * dx + dy * dy) / reach;
                        inside = (1 - d) * reach * half;
                    }
                    if (inside <= 0) continue;
                    switch (shape)
                    {
                        case HazardShape.Lava: image[x, y] = Lava(x, y, inside, 300 + variant, frame); break;
                        case HazardShape.Rock: image[x, y] = Rock(x, y, inside, d, 320 + variant, frame); break;
                        case HazardShape.Water: image[x, y] = Water(x, y, inside, 340 + variant, frame); break;
                        default:
                            image[x, y] = strip ? CrackStrip(x, y, width, height, d, 360 + variant, frame)
                                : CrackPool(x - cx, y - cy, half, d, x, y, 360 + variant, frame);
                            break;
                    }
                }
            return image;
        }

        static float Wobble(int x, int variant) => (float)Math.Sin(x * 0.7f + variant) * 0.5f + (float)Math.Sin(x * 1.9f + variant * 3) * 0.5f;

        // Value noise smoothed over cells of the given size, 0 to 1.
        static float Smooth(float x, float y, int cell, int salt)
        {
            float fx = x / cell, fy = y / cell;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = PixelNoise.Value(x0, y0, salt), b = PixelNoise.Value(x0 + 1, y0, salt);
            float c = PixelNoise.Value(x0, y0 + 1, salt), e = PixelNoise.Value(x0 + 1, y0 + 1, salt);
            return (a + (b - a) * tx) * (1 - ty) + (c + (e - c) * tx) * ty;
        }

        // Molten rock, `inside` pixels in from the bank: the bank's dark crust, plates of crust with the glow between
        // them, then the body: its heat swirls round a small circle over the four frames (so it loops), a thin gold
        // vein winds through it, and bubbles swell, burst and leave a dimple, each on its own beat.
        static Rgba Lava(int x, int y, float inside, int salt, int frame)
        {
            if (inside < 1) return LavaCrustDark;
            if (inside < 2) return PixelNoise.Hash(x / 2, y / 2, salt) % 5 == 0 ? LavaDeep : LavaCrust;
            if (inside < 3.2f) return PixelNoise.Hash(x / 2, y / 2, salt + 1) % 3 == 0 ? LavaCrust : LavaDeep;
            double turn = frame * Math.PI / 2;
            float ox = 1.7f * (float)Math.Cos(turn), oy = 1.7f * (float)Math.Sin(turn);
            float heat = 0.6f * Smooth(x + ox, y + oy, 5, salt) + 0.4f * Smooth(x - oy * 1.4f, y + ox * 1.4f, 9, salt + 1);
            // A bubble in one cell in three, 9 pixels a cell.
            const int Cell = 9;
            int bx = (int)Math.Floor(x / (float)Cell), by = (int)Math.Floor(y / (float)Cell);
            uint h = PixelNoise.Hash(bx, by, salt + 7);
            if (h % 3 == 0 && inside > 5)
            {
                float px = bx * Cell + 3 + (h >> 4) % 4, py = by * Cell + 3 + (h >> 8) % 4;
                float away = (float)Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                switch ((frame + (int)((h >> 12) % 4)) % 4)
                {
                    case 0: if (away < 0.8f) return LavaHot; break;
                    case 1: if (away < 1.7f) return away < 0.8f ? LavaTip : LavaHot; break;
                    case 2: if (away < 2.7f) return away > 1.7f ? LavaHot : away < 0.8f ? LavaTip : LavaMain; break;
                    default: if (away < 2.3f) return away > 1.5f && (x + y) % 2 == 0 ? LavaHot : LavaDeep; break;
                }
            }
            // The vein runs on the diagonals, so it does not follow the noise's grid.
            float vein = Math.Abs(Smooth((x + y) * 0.7f + ox * 1.5f, (x - y) * 0.7f + oy * 1.5f, 8, salt + 2) - 0.5f);
            if (vein < 0.022f && heat > 0.4f) return LavaHot;
            if (heat > 0.7f) return LavaHot;
            if (heat > 0.44f) return LavaMain;
            if (heat > 0.34f) return LavaDeep;
            return heat > 0.27f ? LavaCrust : LavaCrustDark;
        }

        // How near the seam between two plates of rock (6 pixels a plate): under about 1 is the seam. Also the plate.
        static float Seam(int x, int y, int salt, out uint plate)
        {
            const int Cell = 6;
            int gx = (int)Math.Floor(x / (float)Cell), gy = (int)Math.Floor(y / (float)Cell);
            float first = 99, second = 99;
            plate = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    uint h = PixelNoise.Hash(gx + i, gy + j, salt);
                    float px = (gx + i) * Cell + 1 + h % 4, py = (gy + j) * Cell + 1 + (h >> 4) % 4;
                    float away = (float)Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (away < first) { second = first; first = away; plate = h; }
                    else if (away < second) second = away;
                }
            return second - first;
        }

        // Cooled rock: black plates lit from the upper left, the seams between them dark, and red still at its heart
        // (the last of it to cool), glimmering with the frame.
        static Rgba Rock(int x, int y, float inside, float d, int salt, int frame)
        {
            if (inside < 1) return RockRim;
            float seam = Seam(x, y, salt, out uint plate);
            if (seam < 0.9f)
            {
                if (d < 0.62f && PixelNoise.Hash(x / 2, y / 2, salt + 3) % 3 != 0)
                    return (PixelNoise.Hash(x / 3, y / 3, salt + 4) + (uint)frame) % 4 < 2 ? RockEmber : RockEmberDim;
                return RockRim;
            }
            if (inside < 2) return RockShade;
            // The plate's lit upper-left lip and shaded lower-right.
            if (Seam(x - 1, y - 1, salt, out _) < 0.9f) return RockLight;
            if (Seam(x + 1, y + 1, salt, out _) < 0.9f) return RockShade;
            uint tone = (plate >> 8) % 4;
            return tone == 0 ? RockMid : PixelNoise.Hash(x, y, salt + 5) % 11 == 0 ? RockShade : tone == 1 && (x + y) % 2 == 0 ? RockMid : RockMain;
        }

        // Shallow water: a dark line at the bank, pale foam inside it, and broken rings going out to the bank.
        static Rgba Water(int x, int y, float inside, int salt, int frame)
        {
            if (inside < 1) return WaterRim;
            float gate = Smooth(x, y, 6, salt);
            if (inside < 2.2f) return gate > 0.38f ? WaterLight : WaterMain;
            float ring = (inside + frame * 2) % 8;
            if (ring < 1.1f && gate > 0.45f) return gate > 0.7f && ring < 0.6f ? WaterGlint : WaterLight;
            if (ring >= 1.1f && ring < 2.2f && gate > 0.5f) return WaterDeep;
            if ((PixelNoise.Hash(x, y, salt + 1) + (uint)frame) % 97 == 0) return WaterGlint;
            return Smooth(x, y, 8, salt + 2) < 0.4f ? WaterDeep : WaterMain;
        }

        // The crack's heart: fire running along it, a beat every five pixels.
        static Rgba CrackFire(int along, int x, int y, int salt, int frame)
        {
            int beat = ((along / 5 - frame) % 4 + 4) % 4;
            return beat == 0 ? CrackCore : beat == 2 ? CrackDim : beat == 1 && PixelNoise.Hash(x, y, salt) % 3 == 0 ? CrackCore : CrackGlow;
        }

        // The scorched ground round a crack, thinning toward the end of its reach.
        static Rgba Scorch(int x, int y, float d, int salt) =>
            PixelNoise.Value(x, y, salt + 9) > 0.3f + 0.75f * d ? CrackEdge.WithAlpha(d < 0.45f ? 150 : 100) : Rgba.Clear;

        // A crack along a strip: a jagged fissure down the middle with short side cracks.
        static Rgba CrackStrip(int x, int y, int width, int height, float d, int salt, int frame)
        {
            const int Knot = 6;
            float cy = height / 2f - 0.5f;
            float amp = Math.Min(height * 0.22f, 5);
            float At(int k) => (PixelNoise.Value(k, 0, salt) * 2 - 1) * amp;
            int knot = x / Knot;
            float t = (x - knot * Knot) / (float)Knot;
            float line = cy + At(knot) + (At(knot + 1) - At(knot)) * t;
            float taper = Math.Min(1, Math.Min(x, width - 1 - x) / 7f);
            float thick = (0.5f + 1.3f * Smooth(x, 0, 4, salt + 1)) * taper + 0.25f;
            float away = Math.Abs(y - line);
            if (away < Math.Max(0.55f, thick * 0.45f) && taper > 0.2f) return CrackFire(x, x, y, salt, frame);
            if (away < thick + 0.9f) return CrackDark;
            if (away < thick + 1.9f && PixelNoise.Hash(x, y, salt + 2) % 2 == 0) return CrackEdge;
            // Side cracks off the knots.
            for (int k = knot - 1; k <= knot + 1; k++)
            {
                uint h = PixelNoise.Hash(k, 1, salt);
                if (k <= 0 || h % 3 == 0) continue;
                int kx = k * Knot;
                float from = cy + At(k);
                int side = (h >> 3) % 2 == 0 ? 1 : -1;
                float lean = (h >> 5) % 2 == 0 ? 0.7f : -0.7f;
                float out_ = (y - from) * side;
                if (out_ >= 1 && out_ < 3 + (h >> 7) % (uint)Math.Max(2, height / 4) && Math.Abs(x - (kx + out_ * lean)) < 0.8f) return CrackDark;
            }
            return Scorch(x, y, d, salt);
        }

        // A crack as a pool: five jagged arms out of a pit.
        static Rgba CrackPool(float dx, float dy, float half, float d, int x, int y, int salt, int frame)
        {
            float r = (float)Math.Sqrt(dx * dx + dy * dy);
            if (r < 1.3f) return CrackFire(frame * 5, x, y, salt, 0);
            if (r < 2.6f) return CrackDark;
            double angle = Math.Atan2(dy, dx);
            for (int k = 0; k < 5; k++)
            {
                float reach = half * (0.7f + 0.22f * PixelNoise.Value(k, 2, salt));
                if (r > reach) continue;
                double arm = k * Math.PI * 2 / 5 + salt + 0.3 * Math.Sin(r * 0.35 + k * 2) + 0.12 * Math.Sin(r * 0.9 + k);
                double off = angle - arm;
                if (Math.Cos(off) <= 0) continue;
                float away = r * (float)Math.Abs(Math.Sin(off));
                float thick = 1.5f - 1.1f * r / reach;
                if (away < Math.Max(0.55f, thick * 0.45f) && r < reach * 0.8f) return CrackFire((int)r, x, y, salt, frame);
                if (away < thick + 0.8f) return CrackDark;
                if (away < thick + 1.7f && PixelNoise.Hash(x, y, salt + 2) % 2 == 0) return CrackEdge;
            }
            return Scorch(x, y, d, salt);
        }

        // The edge band: from the floor's end out to outer (radii in pixels), as a square image round the middle. The
        // lava's bank is ragged (and the same on every frame, but for a slow lapping); its body is the pools'.
        public static PixelImage Edge(EdgeKind kind, int outer, int floor, int frame)
        {
            int size = outer * 2;
            var image = new PixelImage(size, size);
            float c = outer - 0.5f;
            int lap = (frame % Frames + Frames) % Frames;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (r > outer || r < floor - 6) continue;
                    double angle = Math.Atan2(dy, dx);
                    if (kind == EdgeKind.Lava)
                    {
                        float bank = floor + 2.2f * (float)Math.Sin(angle * 9) + 1.4f * (float)Math.Sin(angle * 23 + 1) + 0.9f * (float)Math.Sin(angle * 41 + 2)
                            + 0.6f * (float)Math.Sin(angle * 6 + lap * Math.PI / 2);
                        float inside = Math.Min(r - bank, outer - r + 1.5f);
                        if (inside > 0) image[x, y] = Lava(x, y, inside, 500, lap);
                        continue;
                    }
                    float inner = floor + 1.2f * (float)Math.Sin(angle * 9 + frame * 0.5f) + 0.7f * (float)Math.Sin(angle * 23);
                    if (r < inner) continue;
                    float noise = PixelNoise.Value(x, y, 500 + frame);
                    if (kind == EdgeKind.Petal) image[x, y] = noise > 0.75f ? PetalDeep : noise > 0.35f ? PetalPink : PetalPink.WithAlpha(110);
                    else
                    {
                        bool strand = (int)(angle * 30) % 2 == 0 && noise > 0.5f || (int)r % 5 == 0;
                        image[x, y] = strand ? WebWhite : WebWhite.WithAlpha(40);
                    }
                }
            return image;
        }

        // A vent: a black mouth in the floor inside a ring of rock (lit from the upper left, cracked and glowing), the
        // molten glow in it swelling and sinking over the four frames, a spark or two leaving it at the top of a breath.
        public static PixelImage Prop(PropKind kind, int frame)
        {
            const int size = 26;
            var image = new PixelImage(size, size);
            float c = size / 2f - 0.5f;
            frame = (frame % Frames + Frames) % Frames;
            int breath = frame == 3 ? 1 : frame;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = (y - c) * 1.25f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy) / (size / 2f);
                    if (r > 1) continue;
                    float noise = PixelNoise.Value(x, y, 700);
                    if (kind == PropKind.Vent)
                    {
                        if (r > 0.9f) { image[x, y] = RockRim; continue; }
                        if (r > 0.6f)
                        {
                            // Three cracks through the ring, lit from below.
                            double angle = Math.Atan2(dy, dx);
                            bool crack = false;
                            for (int k = 0; k < 3; k++)
                            {
                                double off = angle - (0.6 + k * 2.2);
                                if (Math.Cos(off) > 0 && Math.Abs(Math.Sin(off)) * r * size / 2 < 0.7f) crack = true;
                            }
                            if (crack && r < 0.84f) image[x, y] = breath == 2 ? CrackGlow : breath == 1 ? CrackDim : RockEmberDim;
                            else
                            {
                                float lit = (-dx - dy) / (size / 2f);
                                image[x, y] = lit > 0.55f && noise > 0.25f ? RockLight : lit > 0.2f ? (noise > 0.6f ? RockLight : RockMid)
                                    : lit < -0.6f ? RockShade : noise > 0.75f ? RockMid : RockMain;
                            }
                            continue;
                        }
                        if (r > 0.5f) { image[x, y] = RockRim; continue; }
                        // Inside: the far wall dark above, the glow below it.
                        float gx = dx, gy = dy - 2.2f;
                        float glow = (float)Math.Sqrt(gx * gx + gy * gy) / (size / 2f);
                        image[x, y] = glow < 0.09f * breath ? LavaTip : glow < 0.14f + 0.07f * breath ? LavaHot : glow < 0.27f + 0.06f * breath ? LavaMain
                            : glow < 0.4f + 0.03f * breath ? LavaDeep : dy < -1 ? CrackDark : LavaCrust;
                    }
                    else
                    {
                        // A pillar or a vein (R2b): a plain block of rock until they are drawn.
                        image[x, y] = r > 0.85f ? RockRim : kind == PropKind.Vein && noise > 0.8f ? new Rgba(120, 230, 255, 240) : noise > 0.7f ? RockLight : RockMain;
                    }
                }
            if (kind == PropKind.Vent)
            {
                int mid = size / 2;
                if (frame == 2) { image[mid - 3, mid - 5] = LavaHot; image[mid + 3, mid - 6] = LavaMain; }
                if (frame == 3) { image[mid - 4, mid - 8] = LavaMain; image[mid + 4, mid - 9] = LavaDeep; }
            }
            return image;
        }
    }
}
