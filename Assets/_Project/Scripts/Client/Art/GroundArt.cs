using System;
using Game.Core;

namespace Game.Client
{
    // A boss's ground and the things in its arena (2026-11-28, R2, Docs/Bosses.md "지형·물체·분노", "R2a 그림"), drawn at
    // 16 pixels a tile in the lava biome's colours (TileArt.Lava, LavaTitanArt): lava (a dark crust at the bank, plates
    // of crust, a molten body whose gold veins drift and whose bubbles swell and burst over the four frames), cooled
    // rock (black plates, the cracks at its heart still red), shallow water (rings going out to a pale bank), a crack
    // (a jagged black fissure with fire running along its heart); the edge band that closes in from phase 3 (lava with
    // a ragged bank), a vent (a black mouth ringed with rock, the glow in it breathing). R2b ("R2b 그림"): the cave
    // warden's standing pillar of mossy rock (WardenArt's stone and moss), the vein eater's vein (a lump of mine rock
    // set with obsidian and teal crystal, its light beating, cracked as it is eaten), the wall of cherry petals and the
    // curtain of web that close the forgotten shrine's floor in. WorldView.Ground places them.
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
        // Cherry petals (the shrine samurai's), heaped: from the pale crest to the shade between them.
        static readonly Rgba PetalWhite = new Rgba(255, 238, 244, 250);
        static readonly Rgba PetalLight = new Rgba(252, 206, 222, 245);
        static readonly Rgba PetalPink = new Rgba(244, 164, 194, 245);
        static readonly Rgba PetalDeep = new Rgba(206, 104, 146, 240);
        static readonly Rgba PetalShade = new Rgba(132, 56, 98, 225);
        static readonly Rgba PetalDark = new Rgba(84, 34, 70, 215);
        // Spider silk (the silk mother's) over a dim veil.
        static readonly Rgba WebKnot = new Rgba(250, 250, 255, 250);
        static readonly Rgba WebWhite = new Rgba(226, 228, 242, 235);
        static readonly Rgba WebGrey = new Rgba(168, 172, 204, 200);
        static readonly Rgba WebVeil = new Rgba(120, 124, 168, 70);
        static readonly Rgba WebDark = new Rgba(44, 42, 70, 120);
        // The cave warden's stone and moss (WardenArt's palette).
        static readonly Rgba StoneInk = new Rgba(15, 9, 5, 255);
        static readonly Rgba StoneDeep = new Rgba(44, 32, 25, 255);
        static readonly Rgba StoneDark = new Rgba(60, 46, 37, 255);
        static readonly Rgba StoneShade = new Rgba(78, 63, 52, 255);
        static readonly Rgba StoneMain = new Rgba(93, 86, 70, 255);
        static readonly Rgba StoneLight = new Rgba(114, 107, 85, 255);
        static readonly Rgba StonePale = new Rgba(140, 132, 106, 255);
        static readonly Rgba MossDark = new Rgba(36, 90, 31, 255);
        static readonly Rgba MossMain = new Rgba(29, 113, 28, 255);
        static readonly Rgba MossLight = new Rgba(101, 124, 82, 255);
        static readonly Rgba GroundShadow = new Rgba(10, 8, 12, 110);
        // The iron valley's rock (VeinEaterArt's), obsidian, and the teal crystal in it.
        static readonly Rgba MineInk = new Rgba(20, 12, 10, 255);
        static readonly Rgba MineDark = new Rgba(74, 62, 56, 255);
        static readonly Rgba MineMain = new Rgba(120, 104, 92, 255);
        static readonly Rgba MineLight = new Rgba(162, 146, 128, 255);
        static readonly Rgba ObsidianDark = new Rgba(18, 14, 28, 255);
        static readonly Rgba ObsidianMain = new Rgba(40, 32, 58, 255);
        static readonly Rgba ObsidianGlint = new Rgba(150, 140, 200, 255);
        static readonly Rgba TealDeep = new Rgba(18, 84, 100, 255);
        static readonly Rgba TealMain = new Rgba(40, 168, 172, 255);
        static readonly Rgba TealLight = new Rgba(120, 232, 216, 255);
        static readonly Rgba TealTip = new Rgba(226, 255, 246, 255);

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
                        case HazardShape.Pit: image[x, y] = Pit(x - cx, y - cy, d, frame); break;
                        default:
                            image[x, y] = strip ? CrackStrip(x, y, width, height, d, 360 + variant, frame)
                                : CrackPool(x - cx, y - cy, half, d, x, y, 360 + variant, frame);
                            break;
                    }
                }
            return image;
        }

        // R3 (placeholder until its pixel art): a sand funnel, pale at its rim and dark at its throat, with a swirl of
        // grains running in.
        static readonly Rgba PitRim = new Rgba(222, 186, 126, 255), PitSand = new Rgba(196, 156, 98, 255),
            PitShade = new Rgba(150, 110, 64, 255), PitThroat = new Rgba(70, 48, 30, 255);

        static Rgba Pit(float dx, float dy, float d, int frame)
        {
            double angle = Math.Atan2(dy, dx);
            float swirl = (float)Math.Sin(angle * 3 + d * 9 - frame * Math.PI / 2);
            if (d < 0.18f) return PitThroat;
            if (d > 0.9f) return PitRim;
            var tone = d < 0.45f ? PitShade : PitSand;
            return swirl > 0.75f ? (d < 0.45f ? PitThroat : PitShade) : tone;
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
                    if (kind == EdgeKind.Water)
                    {
                        float shore = floor + 1.2f * (float)Math.Sin(angle * 7 + lap * Math.PI / 2) + 0.7f * (float)Math.Sin(angle * 19 + 1);
                        float wet = Math.Min(r - shore, outer - r + 1.5f);
                        if (wet > 0) image[x, y] = Water(x, y, wet, 540, lap);
                        continue;
                    }
                    var pixel = kind == EdgeKind.Petal ? Petal(x, y, r, angle, floor, outer, lap) : Web(r, angle, floor, outer, lap);
                    if (pixel.A > 0) image[x, y] = pixel;
                }
            return image;
        }

        // A petal of the wall, r pixels from the middle: loose petals lie ahead of the heap, its crest is pale and
        // ragged (drifts), and behind it the petals lie thick over their own shade, darker toward the wall's back. Each
        // petal (one to a five pixel cell, lying one of four ways) stirs round a small circle over the four frames, on
        // its own beat, and the loose ones flutter.
        static Rgba Petal(int x, int y, float r, double angle, int floor, int outer, int frame)
        {
            float inner = floor + 2.4f * (float)Math.Sin(angle * 7 + 0.5) + 1.6f * (float)Math.Sin(angle * 17 + 2) + 1.0f * (float)Math.Sin(angle * 37 + 4);
            float depth = r - inner;
            if (depth < -7) return Rgba.Clear;
            const int Cell = 5;
            int gx = (int)Math.Floor(x / (float)Cell), gy = (int)Math.Floor(y / (float)Cell);
            float nearest = 99, across = 0;
            uint who = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    uint h = PixelNoise.Hash(gx + i, gy + j, 520);
                    double beat = (frame + (h >> 20) % 4) * Math.PI / 2;
                    float dx = x - ((gx + i) * Cell + 1 + h % 4 + 0.75f * (float)Math.Cos(beat));
                    float dy = y - ((gy + j) * Cell + 1 + (h >> 4) % 4 + 0.75f * (float)Math.Sin(beat));
                    float u, v;
                    switch ((h >> 8) % 4)
                    {
                        case 0: u = dx; v = dy; break;
                        case 1: u = dy; v = dx; break;
                        case 2: u = (dx + dy) * 0.707f; v = (dx - dy) * 0.707f; break;
                        default: u = (dx - dy) * 0.707f; v = (dx + dy) * 0.707f; break;
                    }
                    float d = u * u / (2.4f * 2.4f) + v * v / (1.4f * 1.4f);
                    if (d < nearest) { nearest = d; who = h; across = v; }
                }
            float chance = depth < 0 ? 0.08f + 0.035f * (depth + 7) : Math.Min(1, 0.5f + depth * 0.12f);
            bool petal = nearest < 1 && ((who >> 12) & 0xFF) / 255f < chance;
            if (depth < 0)
            {
                if (!petal) return Rgba.Clear;
                return ((who >> 24) + (uint)frame) % 2 == 0 ? PetalLight : PetalPink;
            }
            float back = Math.Min(1, depth / Math.Max(1, outer - floor));
            if (petal)
            {
                float tone = ((who >> 16) & 0xF) / 15f;
                if (depth < 4) return tone > 0.3f ? PetalWhite : PetalLight;
                // Lit along one side.
                if (tone + back * 0.55f > 1.05f) return across < -0.4f ? PetalPink : PetalDeep;
                if (tone > 0.35f) return across < -0.4f ? PetalLight : PetalPink;
                return across < -0.4f ? PetalWhite : PetalLight;
            }
            if (depth < 2) return PetalDeep;
            return depth < 6 ? PetalShade : PixelNoise.Hash(x / 2, y / 2, 521) % 3 == 0 || back > 0.75f ? PetalDark : PetalShade;
        }

        // The web curtain, r pixels from the middle: spokes of silk about thirteen pixels apart (each a little off its
        // place, swaying with the frame), their ends hanging in past the hem as a drop; the hem and a thread every six
        // pixels strung between them, sagging outward, a knot where they meet; a thin veil between, thicker toward the
        // wall, dark beside each spoke so the silk stands out on any floor.
        static Rgba Web(float r, double angle, int floor, int outer, int frame)
        {
            int spokes = Math.Max(12, (int)Math.Round(2 * Math.PI * floor / 13));
            double step = 2 * Math.PI / spokes;
            double a = angle < 0 ? angle + 2 * Math.PI : angle;
            double At(int i)
            {
                int m = (i % spokes + spokes) % spokes;
                uint h = PixelNoise.Hash(m, 0, 540);
                return i * step + step * (0.5 + (h % 100 / 100.0 - 0.5) * 0.36) + Math.Sin(frame * Math.PI / 2 + m * 1.7) * 0.8 / Math.Max(8, floor);
            }
            int k = (int)Math.Floor(a / step);
            double a0 = At(k);
            if (a < a0) a0 = At(--k);
            double a1 = At(k + 1);
            float u = (float)((a - a0) / (a1 - a0));
            int seg = (k % spokes + spokes) % spokes;
            uint hs = PixelNoise.Hash(seg, 1, 540);
            float arch = 4 * u * (1 - u);
            float hemSag = 2.5f + hs % 3;
            float hem = floor + 0.5f + hemSag * arch;
            float toSpoke = (float)Math.Min(a - a0, a1 - a) * r;
            if (toSpoke < 0.6f && r >= floor - 4) return r < floor - 2.5f ? WebKnot : WebWhite;
            if (r < hem) return Rgba.Clear;
            const float Gap = 6;
            int ring = (int)Math.Floor((r - floor) / Gap);
            for (int j = Math.Max(0, ring - 1); j <= ring + 1; j++)
            {
                uint hj = PixelNoise.Hash(seg, 2 + j, 540);
                // A thread or two missing.
                if (j > 0 && hj % 6 == 0) continue;
                float sag = j == 0 ? hemSag : 1 + (hj >> 3) % 3 + 0.5f * (float)Math.Sin(frame * Math.PI / 2 + seg + j);
                float at = floor + 0.8f + j * Gap + sag * arch;
                if (Math.Abs(r - at) < 0.62f) return toSpoke < 1.4f ? WebKnot : (hj >> 8) % 4 == 0 ? WebGrey : WebWhite;
            }
            if (toSpoke < 1.7f) return WebDark;
            float thick = (r - floor) / Math.Max(1, outer - floor);
            return WebVeil.WithAlpha((int)(46 + 80 * thick));
        }

        // Where a standing thing meets the floor in its picture, from the bottom (0) to the top (1): WorldView stands
        // the pillar and the vein on this row; the vent is a hole, placed by its middle.
        public static float PropPivot(PropKind kind) => kind switch
        {
            PropKind.Pillar => (PillarHeight - PillarFoot - 0.5f) / PillarHeight,
            PropKind.Vein => (VeinHeight - VeinFoot - 0.5f) / VeinHeight,
            _ => 0.5f,
        };

        // A standing thing: a vent, a pillar or a whole vein.
        public static PixelImage Prop(PropKind kind, int frame)
        {
            frame = (frame % Frames + Frames) % Frames;
            return kind == PropKind.Pillar ? Pillar(frame) : kind == PropKind.Vein ? VeinCracked(frame, 0) : Vent(frame);
        }

        const int PillarWidth = 32, PillarHeight = 48, PillarFoot = 39;

        // The cave warden's pillar: three boulders stacked nine tenths of a tile round and two and a half tall, lit
        // from the upper left, moss on its top and trailing down from it and on each ledge, rubble and a shadow at its
        // foot. All but still: a grain of grit falls down each side over the four frames.
        static PixelImage Pillar(int frame)
        {
            var image = new PixelImage(PillarWidth, PillarHeight);
            const float cx = 15.5f;
            const int top = 5;
            for (int y = PillarFoot - 6; y < PillarHeight; y++)
                for (int x = 0; x < PillarWidth; x++)
                {
                    float dx = (x - cx - 1.5f) / 14.5f, dy = (y - PillarFoot - 1.5f) / 6.5f;
                    if (dx * dx + dy * dy < 1) image[x, y] = GroundShadow;
                }
            // The boulders, from the foot up: where each ends (its seam's row), its half width and its lean aside.
            int seamLow = PillarFoot - 12, seamHigh = PillarFoot - 24;
            for (int y = top; y <= PillarFoot + 5; y++)
            {
                int block = y > seamLow ? 0 : y > seamHigh ? 1 : 2;
                float half = block == 0 ? 11.4f : block == 1 ? 10.4f : 9.4f;
                float lean = block == 0 ? 0 : block == 1 ? 0.8f : -0.6f;
                // Rounded in at each seam and under the foot.
                int toSeam = Math.Min(Math.Abs(y - seamLow), Math.Abs(y - seamHigh));
                if (toSeam == 0) half -= 1.2f;
                else if (y == seamLow + 1 || y == seamHigh + 1) half -= 0.5f;
                if (y > PillarFoot) half *= (float)Math.Sqrt(Math.Max(0, 1 - (y - PillarFoot) * (y - PillarFoot) / 30f));
                half += 0.5f * (float)Math.Sin(y * 0.9f + block * 2);
                if (half < 1) continue;
                for (int x = 0; x < PillarWidth; x++)
                {
                    float off = x - cx - lean;
                    if (Math.Abs(off) > half) continue;
                    float n = off / half;
                    bool rim = Math.Abs(off) > half - 1 || y == PillarFoot + 5;
                    uint h = PixelNoise.Hash(x, y, 560);
                    Rgba c;
                    if (rim) c = StoneInk;
                    else if (toSeam == 0) c = StoneDeep;
                    else if (y == seamLow + 1 || y == seamHigh + 1) c = n < 0.3f ? StonePale : StoneMain;
                    else if (n < -0.5f) c = h % 6 == 0 ? StonePale : StoneLight;
                    else if (n < 0.2f) c = h % 7 == 0 ? StoneShade : h % 11 == 0 ? StoneLight : StoneMain;
                    else if (n < 0.62f) c = h % 6 == 0 ? StoneDark : StoneShade;
                    else c = h % 5 == 0 ? StoneDeep : StoneDark;
                    // Under the foot's curve the front falls into shade.
                    if (!rim && y > PillarFoot + 2) c = n < 0 ? StoneShade : StoneDeep;
                    image[x, y] = c;
                }
            }
            // Cracks: one down the lower boulder, one across the middle, a chip out of the top one.
            int[][] cracks = { new[] { 11, seamLow + 2, 12, seamLow + 5, 11, seamLow + 8 }, new[] { 18, seamHigh + 3, 20, seamHigh + 5, 19, seamHigh + 8, 21, seamHigh + 10 }, new[] { 13, top + 6, 14, top + 9 } };
            foreach (var crack in cracks)
                for (int i = 0; i + 3 < crack.Length; i += 2)
                    Line(image, crack[i], crack[i + 1], crack[i + 2], crack[i + 3], StoneDeep);
            // Moss on each ledge (the lit side), trailing a pixel or two down.
            foreach (int seam in new[] { seamLow, seamHigh })
                for (int x = 5; x < 22; x++)
                {
                    uint h = PixelNoise.Hash(x, seam, 561);
                    if (h % 3 == 0 || image[x, seam - 1].A < 255 || image[x - 1, seam - 1].A < 255) continue;
                    image[x, seam - 1] = h % 4 == 1 ? MossLight : MossMain;
                    if (h % 5 < 2 && image[x - 1, seam + 1].A == 255) image[x, seam + 1] = MossDark;
                }
            // Its top: a face seen from above, pale, most of it under moss that trails down the front.
            for (int y = top - 3; y <= top + 3; y++)
                for (int x = 0; x < PillarWidth; x++)
                {
                    float dx = (x - cx + 0.6f) / 9.4f, dy = (y - top) / 3.2f;
                    float d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    bool rim = d > 0.78f;
                    float moss = Smooth(x, y * 2, 4, 562);
                    image[x, y] = rim ? (dy < 0 ? StoneInk : StoneDeep) : moss > 0.42f ? (moss > 0.66f || dx + dy < -0.5f ? MossLight : MossMain)
                        : dx + dy < -0.2f ? StonePale : StoneLight;
                }
            for (int x = 7; x < 25; x++)
            {
                uint h = PixelNoise.Hash(x, 0, 563);
                if (h % 5 < 2) continue;
                int trail = 2 + (int)((h >> 4) % 6);
                for (int k = 0; k < trail; k++)
                {
                    int y = top + 3 + k - (Math.Abs(x - 15.5f) > 6 ? 1 : 0);
                    if (image[x, y].A < 255 || image[x - 1, y].A < 255 || image[x + 1, y].A < 255) continue;
                    image[x, y] = k == trail - 1 ? MossDark : x < 13 && (h >> 8) % 3 == 0 ? MossLight : MossMain;
                }
            }
            // A clump of moss and three stones at its foot.
            foreach (var (x, y) in new[] { (5, PillarFoot + 2), (6, PillarFoot + 3), (7, PillarFoot + 3), (6, PillarFoot + 2) })
                image[x, y] = (x + y) % 2 == 0 ? MossMain : MossDark;
            foreach (var (x, y, big) in new[] { (2, PillarFoot + 1, true), (27, PillarFoot + 3, true), (24, PillarFoot + 6, false) })
            {
                image[x, y] = StoneLight;
                image[x + 1, y] = StoneMain;
                image[x, y + 1] = big ? StoneShade : StoneInk;
                image[x + 1, y + 1] = big ? StoneDark : StoneInk;
                if (big) { image[x, y + 2] = StoneInk; image[x + 1, y + 2] = StoneInk; }
            }
            // Grit: a grain down the left side, another down the right half a loop later.
            image[3, PillarFoot - 26 + frame * 7] = StonePale.WithAlpha(frame == 3 ? 120 : 220);
            int other = (frame + 2) % Frames;
            image[28, PillarFoot - 22 + other * 6] = StoneLight.WithAlpha(other == 3 ? 120 : 220);
            return image;
        }

        static void Line(PixelImage image, int x0, int y0, int x1, int y1, Rgba color)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0 : i / (float)steps;
                int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
                if (image[x, y].A == 255) image[x, y] = color;
            }
        }

        const int VeinWidth = 28, VeinHeight = 30, VeinFoot = 21;

        // The crystals of a vein: where each stands in the rock and where its tip is, how thick, and how much of it is
        // left at each count of cracks (0 gone).
        static readonly (float X, float Y, float TipX, float TipY, float Half, float Left1, float Left2)[] Shards =
        {
            (8, 18, 3, 8, 2.4f, 1, 0.5f),
            (18.5f, 18, 23.5f, 7, 2.6f, 0.45f, 0),
            (13, 18, 12, 2, 3.3f, 1, 0.55f),
            (16, 22, 18, 15, 1.9f, 1, 1),
            (6, 21.5f, 4, 17, 1.4f, 0, 0),
        };

        // The vein eater's vein: a lump of the iron valley's rock eight tenths of a tile round, seams of obsidian
        // through it and teal crystals standing out of it, their light beating over the four frames (dim, bright,
        // brightest, bright) with a glow round them at the top of the beat. Cracks (0 to 2) shows how much of it has
        // been eaten: at 1 a crystal is snapped, a bite is out of its side and a crack runs down it; at 2 the bite is
        // deep, the big crystals are stumps, and shards and crumbs lie round it.
        public static PixelImage VeinCracked(int frame, int cracks)
        {
            frame = (frame % Frames + Frames) % Frames;
            cracks = Math.Max(0, Math.Min(2, cracks));
            int beat = frame == 3 ? 1 : frame;
            var image = new PixelImage(VeinWidth, VeinHeight);
            var crystal = new bool[VeinWidth, VeinHeight];
            const float cx = 13.5f, cy = 19;
            // What has been bitten out of it.
            float biteX = cracks == 2 ? 21.5f : 24, biteY = cracks == 2 ? 15.5f : 16.5f, bite = cracks == 2 ? 6.2f : cracks == 1 ? 3.6f : 0;
            for (int y = 0; y < VeinHeight; y++)
                for (int x = 0; x < VeinWidth; x++)
                {
                    float sx = (x - cx - 1) / 13f, sy = (y - VeinFoot - 1.5f) / 6f;
                    if (sx * sx + sy * sy < 1) image[x, y] = GroundShadow;
                    float dx = (x - cx) / 12f, dy = (y - cy) / (y < cy ? 8.5f : 6.5f);
                    double angle = Math.Atan2(dy, dx);
                    float reach = 1 + 0.07f * (float)Math.Sin(angle * 5 + 1) + 0.05f * (float)Math.Sin(angle * 9);
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / reach;
                    if (d > 1) continue;
                    float bitten = (float)Math.Sqrt((x - biteX) * (x - biteX) + (y - biteY) * (y - biteY)) - bite;
                    if (bite > 0 && bitten < 0) continue;
                    uint h = PixelNoise.Hash(x, y, 580);
                    Rgba c;
                    float lit = -dx - dy * 0.9f;
                    if (d > 0.87f) c = MineInk;
                    else if (bite > 0 && bitten < 1.3f) c = bitten < 0.6f ? MineInk : MineDark;
                    else
                    {
                        // Obsidian runs through it in a band or two on the diagonal.
                        float band = Math.Abs(Smooth((x + y) * 0.9f, (x - y) * 0.9f, 5, 581) - 0.5f);
                        if (band < 0.05f) c = h % 7 == 0 ? ObsidianGlint : band < 0.028f ? ObsidianMain : ObsidianDark;
                        else if (Seam(x, y, 582, out _) < 0.75f) c = MineDark;
                        else if (h % 19 == 0) c = beat == 2 ? TealLight : beat == 1 ? TealMain : TealDeep;
                        else if (lit > 0.55f) c = h % 4 == 0 ? MineMain : MineLight;
                        else if (lit > -0.1f) c = h % 6 == 0 ? MineLight : MineMain;
                        else if (lit > -0.6f) c = (x + y) % 2 == 0 ? MineMain : MineDark;
                        else c = MineDark;
                    }
                    image[x, y] = c;
                }
            // Cracks down the rock.
            if (cracks >= 1)
            {
                Line(image, 9, 20, 11, 23, MineInk);
                Line(image, 11, 23, 10, 25, MineInk);
            }
            if (cracks >= 2)
            {
                Line(image, 15, 19, 14, 22, MineInk);
                Line(image, 14, 22, 16, 25, MineInk);
                Line(image, 5, 19, 7, 22, MineInk);
                Line(image, 11, 23, 13, 24, MineInk);
            }
            // The crystals, back to front.
            Rgba Beat(Rgba c) => beat == 0 ? c.Mix(TealDeep, 0.3f) : beat == 2 ? c.Mix(TealTip, 0.35f) : c;
            foreach (var shard in Shards)
            {
                float left = cracks == 0 ? 1 : cracks == 1 ? shard.Left1 : shard.Left2;
                if (left <= 0) continue;
                float ax = shard.TipX - shard.X, ay = shard.TipY - shard.Y;
                float length = (float)Math.Sqrt(ax * ax + ay * ay);
                ax /= length;
                ay /= length;
                for (int y = 0; y < VeinHeight; y++)
                    for (int x = 0; x < VeinWidth; x++)
                    {
                        float along = (x - shard.X) * ax + (y - shard.Y) * ay, o = (x - shard.X) * -ay + (y - shard.Y) * ax;
                        float t = along / length;
                        if (t < -0.05f || t > 1) continue;
                        // Snapped: a jagged end where it broke.
                        float end = left < 1 ? left + 0.06f * (o > 0 ? 1 : -0.5f) : 1;
                        if (t > end) continue;
                        float half = shard.Half * (float)Math.Pow(Math.Max(0, 1 - t), 0.6f) + 0.35f;
                        if (Math.Abs(o) > half) continue;
                        bool rim = Math.Abs(o) > half - 0.95f || t < 0.02f;
                        Rgba c;
                        if (left < 1 && t > end - 0.09f) c = TealTip;
                        else if (rim) c = TealDeep;
                        else if (t > 0.78f) c = TealTip;
                        // The ridge down its middle, the face toward the light (the left), the face away.
                        else if (Math.Abs(o) < 0.5f) c = beat == 0 ? TealLight : TealTip;
                        else c = o < 0 ? TealLight : TealMain;
                        image[x, y] = Beat(c);
                        crystal[x, y] = true;
                    }
            }
            // What has broken off lies round it.
            if (cracks >= 1)
            {
                image[23, 24] = TealLight; image[24, 24] = TealMain; image[24, 25] = TealDeep;
                image[26, 21] = MineMain; image[26, 22] = MineInk;
                image[2, 23] = TealMain; image[2, 24] = TealDeep;
            }
            if (cracks >= 2)
            {
                image[20, 26] = TealTip; image[21, 26] = TealLight; image[21, 27] = TealDeep; image[20, 27] = TealDeep;
                image[25, 19] = TealLight; image[25, 20] = TealDeep;
                image[8, 27] = MineLight; image[9, 27] = MineMain; image[8, 28] = MineInk; image[9, 28] = MineInk;
                image[4, 26] = TealMain; image[4, 27] = TealDeep;
                image[17, 28] = MineMain; image[17, 29] = MineInk;
            }
            // The glow round the crystals at the top of the beat (fainter once it is nearly eaten).
            if (beat > 0)
            {
                int alpha = (beat == 2 ? 96 : 44) - cracks * 12;
                for (int y = 0; y < VeinHeight; y++)
                    for (int x = 0; x < VeinWidth; x++)
                    {
                        if (image[x, y].A == 255) continue;
                        bool near = false;
                        for (int j = -1; j <= 1 && !near; j++)
                            for (int i = -1; i <= 1 && !near; i++)
                                if (x + i >= 0 && y + j >= 0 && x + i < VeinWidth && y + j < VeinHeight && crystal[x + i, y + j]) near = true;
                        if (near) image[x, y] = TealLight.WithAlpha(alpha);
                    }
            }
            return image;
        }

        // A vent: a black mouth in the floor inside a ring of rock (lit from the upper left, cracked and glowing), the
        // molten glow in it swelling and sinking over the four frames, a spark or two leaving it at the top of a breath.
        static PixelImage Vent(int frame)
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
                }
            {
                int mid = size / 2;
                if (frame == 2) { image[mid - 3, mid - 5] = LavaHot; image[mid + 3, mid - 6] = LavaMain; }
                if (frame == 3) { image[mid - 4, mid - 8] = LavaMain; image[mid + 4, mid - 9] = LavaDeep; }
            }
            return image;
        }
    }
}
