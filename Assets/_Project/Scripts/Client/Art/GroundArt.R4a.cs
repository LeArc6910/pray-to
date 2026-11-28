using System;

namespace Game.Client
{
    // R4a (2026-11-29, Docs/Bosses.md "R4a 그림", Docs/BossRedesign.md 5.3·5.5): the crystal the crystal queen plants
    // (CrystalQueenArt's ice and pink), the crystal eggs of her interlude, and Morg's war banner (GeneralArt's reds and
    // blacks). Each stands on the floor: place it by its pivot row, as the pillar is.
    static partial class GroundArt
    {
        static readonly Rgba IceInk = new Rgba(22, 40, 78, 255);
        static readonly Rgba IceDark = new Rgba(57, 112, 145, 255);
        static readonly Rgba IceShade = new Rgba(100, 160, 196, 255);
        static readonly Rgba IceMain = new Rgba(134, 198, 223, 255);
        static readonly Rgba IceLight = new Rgba(213, 247, 251, 255);
        static readonly Rgba IceGlint = new Rgba(254, 254, 254, 255);
        static readonly Rgba GemBlush = new Rgba(254, 226, 248, 255);
        static readonly Rgba GemPink = new Rgba(237, 169, 208, 255);
        static readonly Rgba GemHot = new Rgba(255, 120, 200, 255);
        static readonly Rgba GemDeep = new Rgba(178, 50, 126, 255);
        static readonly Rgba GemDark = new Rgba(102, 21, 69, 255);
        static readonly Rgba GemInk = new Rgba(29, 3, 30, 255);

        const int CrystalWidth = 32, CrystalHeight = 40, CrystalFoot = 32;
        // Where the crystal meets the floor in its picture, from the bottom (0) to the top (1).
        public const float CrystalPivot = (CrystalHeight - CrystalFoot - 0.5f) / CrystalHeight;

        // The crystal's shards, back to front: where each stands and where its tip is, and how thick.
        static readonly (float X, float Y, float TipX, float TipY, float Half)[] Spires =
        {
            (10.5f, 32, 4.5f, 14, 3.1f),
            (21.5f, 32, 27, 17, 2.9f),
            (16, 33, 15, 3, 4.4f),
            (12.5f, 35, 9.5f, 27, 1.9f),
            (20, 35, 23, 28, 1.7f),
        };

        // The crystal queen's planted crystal: a tall spire with two leaning beside it and two small ones before it,
        // 1.6 tiles round and two tall, lit from the upper left (a pale facet, a white ridge, a shaded facet with the
        // queen's pink deep in it). Still but for a glint running down the ridge and a twinkle at a tip. Lit (0 to 1)
        // is its turn in the resonance: white light fills it from the foot up, its edges pale, and a glow stands round
        // it that breathes over the four frames.
        public static PixelImage Crystal(int frame, float lit)
        {
            frame = (frame % Frames + Frames) % Frames;
            lit = Math.Max(0, Math.Min(1, lit));
            var image = new PixelImage(CrystalWidth, CrystalHeight);
            var solid = new bool[CrystalWidth, CrystalHeight];
            for (int y = CrystalFoot - 5; y < CrystalHeight; y++)
                for (int x = 0; x < CrystalWidth; x++)
                {
                    float dx = (x - 16.5f) / 13f, dy = (y - CrystalFoot - 1.5f) / 5f;
                    if (dx * dx + dy * dy < 1) image[x, y] = lit > 0 ? GroundShadow.Mix(GemPink, lit * 0.5f).WithAlpha(110 + (int)(60 * lit)) : GroundShadow;
                }
            for (int k = 0; k < Spires.Length; k++)
            {
                var s = Spires[k];
                float ax = s.TipX - s.X, ay = s.TipY - s.Y, length = (float)Math.Sqrt(ax * ax + ay * ay);
                for (int y = 0; y < CrystalHeight; y++)
                    for (int x = 0; x < CrystalWidth; x++)
                    {
                        float px = x - s.X, py = y - s.Y;
                        float t = (px * ax + py * ay) / (length * length);
                        if (t < -0.04f || t > 1) continue;
                        // Across the shard: negative on its lit (left) side.
                        float side = -(px * ay - py * ax) / length;
                        float w = s.Half * (t < 0.1f ? 0.78f + t * 2.2f : t < 0.7f ? 1 : (1 - t) / 0.3f);
                        if (Math.Abs(side) > w + 0.2f) continue;
                        float n = side / Math.Max(0.6f, w);
                        bool rim = Math.Abs(side) > w - 0.8f && w > 1.3f;
                        bool cap = t >= 0.7f;
                        uint h = PixelNoise.Hash(x, y, 640 + k);
                        Rgba c;
                        if (w <= 1.3f) c = t > 0.9f ? IceGlint : IceLight;
                        else if (rim) c = side < 0 ? IceDark : IceInk;
                        else if (Math.Abs(n + 0.12f) < 0.6f / s.Half) c = cap ? IceGlint : GemBlush;
                        else if (n < 0) c = cap ? IceGlint.Mix(IceLight, 0.5f) : n < -0.55f ? IceMain.Mix(IceLight, 0.45f) : IceLight;
                        else if (cap) c = IceMain;
                        else if (n < 0.5f) c = t < 0.4f ? IceShade.Mix(GemPink, 0.55f - t) : IceShade;
                        else c = t < 0.3f ? IceDark.Mix(GemDeep, 0.5f) : h % 5 == 0 ? IceShade : IceDark;
                        // A flaw across the shaded facet.
                        if (!rim && !cap && n > 0.1f && (int)(t * length) == (int)(length * 0.46f) + (int)(n * 2)) c = IceInk.Mix(IceDark, 0.6f);
                        if (lit > 0)
                        {
                            // The light rising from the foot: full under its level, a soft edge above.
                            float level = lit * 1.2f - t;
                            float fill = Math.Max(0, Math.Min(1, level * 4 + 0.5f));
                            float amount = rim ? 0.2f + 0.5f * fill : n > 0.1f && !cap ? 0.2f + 0.5f * fill : 0.3f + 0.62f * fill;
                            c = c.Mix(rim ? GemPink : IceGlint, lit * amount);
                        }
                        image[x, y] = c;
                        solid[x, y] = true;
                    }
            }
            // Splinters at its foot, and a crack in the floor each side where it was driven in.
            foreach (var (x, y) in new[] { (5, 34), (26, 34), (17, 37), (8, 36) })
            {
                if (solid[x, y]) continue;
                image[x, y] = IceLight; image[x + 1, y] = IceShade; image[x, y + 1] = IceInk;
            }
            foreach (var (x, y) in new[] { (3, 33), (2, 34), (28, 35), (29, 36), (23, 37), (24, 38) })
                if (!solid[x, y]) image[x, y] = GemInk.WithAlpha(200);
            // The glint down the main ridge, two pixels, a fifth of the way a frame; a twinkle at a tip on the beat.
            {
                var s = Spires[2];
                float t = 0.62f - frame * 0.14f;
                for (int i = 0; i < 2; i++)
                {
                    int gx = (int)Math.Round(s.X + (s.TipX - s.X) * t - 0.6f), gy = (int)Math.Round(s.Y + (s.TipY - s.Y) * t) + i;
                    if (solid[gx, gy]) image[gx, gy] = IceGlint;
                }
                var tip = frame == 0 ? Spires[0] : frame == 2 ? Spires[1] : frame == 1 ? Spires[2] : Spires[3];
                Twinkle(image, (int)Math.Round(tip.TipX), (int)Math.Round(tip.TipY) + 1, frame % 2 == 1 || lit > 0.3f, IceGlint);
            }
            if (lit > 0.02f)
            {
                // The glow round it: two pixels deep at the top of the breath, one at its bottom.
                int reach = frame == 1 || frame == 2 ? 3 : 2;
                for (int y = 0; y < CrystalHeight; y++)
                    for (int x = 0; x < CrystalWidth; x++)
                    {
                        if (solid[x, y]) continue;
                        int near = 99;
                        for (int oy = -reach; oy <= reach; oy++)
                            for (int ox = -reach; ox <= reach; ox++)
                            {
                                int qx = x + ox, qy = y + oy;
                                if (qx < 0 || qy < 0 || qx >= CrystalWidth || qy >= CrystalHeight || !solid[qx, qy]) continue;
                                near = Math.Min(near, Math.Max(Math.Abs(ox), Math.Abs(oy)) + (ox != 0 && oy != 0 ? 1 : 0) / 2);
                            }
                        if (near > reach) continue;
                        int alpha = (int)(lit * (near == 1 ? 185 : near == 2 ? 110 : 55));
                        image.Blend(x, y, (near == 1 ? IceGlint : GemBlush).WithAlpha(alpha));
                    }
                if (lit > 0.5f)
                {
                    // Sparks off the tips at full light.
                    var a = Spires[frame % 2 == 0 ? 2 : 0];
                    Twinkle(image, (int)Math.Round(a.TipX) + (frame < 2 ? 2 : -2), Math.Max(1, (int)Math.Round(a.TipY) - 1 + frame % 2), true, IceGlint);
                }
            }
            return image;
        }

        // A four-pointed spark: a pixel, or with its four arms.
        static void Twinkle(PixelImage image, int x, int y, bool arms, Rgba color)
        {
            if (image.Inside(x, y)) image[x, y] = color;
            if (!arms) return;
            foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                if (image.Inside(x + ox, y + oy)) image.Blend(x + ox, y + oy, color.WithAlpha(170));
        }

        const int EggSize = 24, EggFoot = 19;
        public const float EggPivot = (EggSize - EggFoot - 0.5f) / EggSize;

        // The cracks of an egg, as runs of points: the first two come at one crack, the others at two.
        static readonly int[][] EggCracks =
        {
            new[] { 14, 4, 13, 6, 15, 8, 13, 10, 14, 12 },
            new[] { 13, 6, 10, 7, 9, 9 },
            new[] { 13, 10, 10, 11, 9, 13, 10, 15, 8, 17 },
            new[] { 15, 8, 17, 10, 16, 13, 17, 15 },
        };

        // A crystal egg of the queen's interlude, a tile round: a shell of pale crystal fuller at the bottom, lit from
        // the upper left, pink light in it and the dark of the young one curled inside, which stirs; the egg swells
        // and sinks as if breathing over the four frames; threads of silk hold it to the floor. Cracks: 0 whole, 1 a
        // crack down from its crown, 2 cracked through with a chip gone and the light coming out (about to break).
        public static PixelImage Egg(int frame, int cracks)
        {
            frame = (frame % Frames + Frames) % Frames;
            cracks = Math.Max(0, Math.Min(2, cracks));
            var image = new PixelImage(EggSize, EggSize);
            var shell = new bool[EggSize, EggSize];
            float swell = frame == 0 ? 0 : frame == 2 ? 0.9f : 0.45f;
            const float cx = 11.5f;
            float ry = 8.2f + swell * 0.5f, cy = EggFoot + 1.2f - ry;
            for (int y = EggFoot - 2; y < EggSize; y++)
                for (int x = 0; x < EggSize; x++)
                {
                    float dx = (x - cx - 1) / 9.5f, dy = (y - EggFoot - 0.5f) / 3.2f;
                    if (dx * dx + dy * dy < 1) image[x, y] = GroundShadow;
                }
            // The young one's place: it shifts a pixel round over the frames, faster once the shell is cracked.
            int[] stirX = { 0, 1, 0, -1 }, stirY = { 0, 0, 1, 0 };
            float bx = cx + 0.3f + stirX[frame], by = cy + 1.6f + stirY[frame];
            for (int y = 0; y < EggSize; y++)
                for (int x = 0; x < EggSize; x++)
                {
                    float ny = (y - cy) / ry;
                    if (Math.Abs(ny) > 1) continue;
                    float rx = (6.6f + swell * 0.5f) * (float)Math.Sqrt(1 - ny * ny) * (1 + 0.14f * ny);
                    float off = x - cx;
                    if (Math.Abs(off) > rx + 0.3f) continue;
                    float nx = off / Math.Max(1, rx);
                    bool rim = Math.Abs(off) > rx - 0.8f || Math.Abs(ny) > 0.9f;
                    float light = -nx * 0.6f - ny * 0.55f;
                    Rgba c;
                    if (rim) c = light > 0.2f ? IceDark : IceInk.Mix(GemDark, 0.5f);
                    else if (light > 0.62f) c = IceGlint;
                    else if (light > 0.3f) c = IceLight;
                    else if (light > -0.1f) c = IceMain.Mix(GemBlush, 0.35f);
                    else if (light > -0.45f) c = IceShade.Mix(GemPink, 0.4f);
                    else c = IceDark.Mix(GemDeep, 0.45f);
                    if (!rim)
                    {
                        // What is inside, seen through the shell: a pink glow, and in it the young one's dark body.
                        float ex = (x - bx) / 3.4f, ey = (y - by) / 4.2f;
                        float e = ex * ex + ey * ey;
                        if (e < 0.38f) c = c.Mix(GemDark, 0.78f);
                        else if (e < 1) c = c.Mix(GemDeep, 0.5f);
                        else if (e < 1.7f) c = c.Mix(GemHot, 0.3f);
                        // Facets: a pale seam slanting across the shell.
                        if ((x * 2 + y) % 9 == 0 && light > -0.3f && e > 0.5f) c = c.Mix(IceGlint, 0.45f);
                    }
                    image[x, y] = c;
                    shell[x, y] = true;
                }
            // Its legs folded against the glow, and two eyes that open on the stir.
            foreach (var (ox, oy) in new[] { (-3, 1), (-2, 3), (3, 0), (2, 3) })
            {
                int lx = (int)Math.Round(bx) + ox, ly = (int)Math.Round(by) + oy;
                if (shell[lx, ly]) image[lx, ly] = image[lx, ly].Mix(GemInk, 0.55f);
            }
            if (frame == 1 || frame == 3 || cracks == 2)
            {
                int exx = (int)Math.Round(bx), eyy = (int)Math.Round(by) - 1;
                if (shell[exx - 1, eyy]) image[exx - 1, eyy] = GemHot;
                if (shell[exx + 1, eyy]) image[exx + 1, eyy] = GemHot;
            }
            // Silk to the floor each side.
            foreach (var (x, y, a) in new[] { (4, 19, 235), (3, 20, 200), (2, 21, 150), (6, 20, 200), (19, 19, 235), (20, 20, 200), (21, 21, 150), (17, 20, 190), (11, 21, 170) })
                if (!shell[x, y]) image[x, y] = WebWhite.WithAlpha(a);
            // The cracks.
            int runs = cracks == 0 ? 0 : cracks == 1 ? 2 : EggCracks.Length;
            for (int r = 0; r < runs; r++)
            {
                int[] run = EggCracks[r];
                for (int i = 0; i + 3 < run.Length; i += 2)
                {
                    int steps = Math.Max(Math.Abs(run[i + 2] - run[i]), Math.Abs(run[i + 3] - run[i + 1]));
                    for (int k = 0; k <= steps; k++)
                    {
                        int x = (int)Math.Round(run[i] + (run[i + 2] - run[i]) * k / (float)steps);
                        int y = (int)Math.Round(run[i + 1] + (run[i + 3] - run[i + 1]) * k / (float)steps);
                        if (!shell[x, y]) continue;
                        // At two the light leaks out along them, flickering.
                        bool hot = cracks == 2 && (x + y + frame) % 3 != 0;
                        image[x, y] = hot ? (r == 0 ? IceGlint : GemHot) : GemInk;
                        if (cracks == 2 && shell[x - 1, y] && image[x - 1, y].R != GemInk.R) image[x - 1, y] = image[x - 1, y].Mix(GemHot, 0.45f);
                    }
                }
            }
            if (cracks == 2)
            {
                // A chip gone from the crown, the hole dark with light at its edge; the chip lies by the egg.
                foreach (var (x, y) in new[] { (11, 4), (12, 4), (12, 5), (11, 5), (10, 5), (12, 6) })
                    if (shell[x, y]) image[x, y] = (x + y) % 2 == 0 ? GemInk : GemDark;
                foreach (var (x, y) in new[] { (10, 4), (13, 5), (11, 6) })
                    if (shell[x, y]) image[x, y] = frame % 2 == 0 ? IceGlint : GemHot;
                image[20, 17] = IceLight; image[21, 17] = IceShade; image[20, 18] = IceInk;
                image[2, 18] = IceLight; image[2, 19] = IceInk;
                // And a glow round it.
                for (int y = 0; y < EggFoot; y++)
                    for (int x = 0; x < EggSize; x++)
                    {
                        if (shell[x, y]) continue;
                        bool near = x > 0 && shell[x - 1, y] || x < EggSize - 1 && shell[x + 1, y] || y > 0 && shell[x, y - 1] || shell[x, y + 1];
                        if (near) image.Blend(x, y, GemHot.WithAlpha(frame % 2 == 0 ? 120 : 80));
                    }
            }
            else
            {
                // The gleam on its shoulder.
                int gy = (int)Math.Round(cy - ry * 0.5f);
                if (shell[8, gy]) image[8, gy] = IceGlint;
                if (shell[9, gy - 1]) image[9, gy - 1] = IceGlint;
            }
            return image;
        }

        static readonly Rgba FlagHot = new Rgba(214, 92, 80, 255);
        static readonly Rgba FlagLight = new Rgba(183, 76, 72, 255);
        static readonly Rgba FlagMain = new Rgba(150, 59, 56, 255);
        static readonly Rgba FlagShade = new Rgba(117, 40, 45, 255);
        static readonly Rgba FlagDark = new Rgba(77, 28, 37, 255);
        static readonly Rgba FlagInk = new Rgba(9, 3, 7, 255);
        static readonly Rgba FlagBlack = new Rgba(25, 15, 22, 255);
        static readonly Rgba FlagBlackLight = new Rgba(59, 46, 48, 255);
        static readonly Rgba PoleLight = new Rgba(154, 141, 132, 255);
        static readonly Rgba PoleMain = new Rgba(99, 92, 82, 255);
        static readonly Rgba PoleDark = new Rgba(44, 31, 34, 255);
        static readonly Rgba Bone = new Rgba(220, 206, 186, 255);
        static readonly Rgba BonePale = new Rgba(240, 237, 229, 255);

        const int BannerWidth = 24, BannerHeight = 48, BannerFoot = 41;
        public const float BannerPivot = (BannerHeight - BannerFoot - 0.5f) / BannerHeight;

        // The emblem on the flag, in the flag's own columns and rows: a horned skull, bone on the red.
        static readonly string[] Emblem =
        {
            "#.....#",
            "##...##",
            ".#####.",
            ".#.#.#.",
            ".#####.",
            "..###..",
            "..#.#..",
        };

        // Morg's war banner, two and a half tiles tall: an iron-shod pole with a bone spearhead, driven into a heap of
        // earth and stones, and a swallow-tailed flag of his red, bound and hemmed in black with a horned skull on it,
        // a tattered streamer under it. The flag's folds run out from the pole over the four frames.
        public static PixelImage Banner(int frame)
        {
            frame = (frame % Frames + Frames) % Frames;
            var image = new PixelImage(BannerWidth, BannerHeight);
            const int pole = 5, flagTop = 7, flagHeight = 19, flagLength = 16;
            for (int y = BannerFoot - 3; y < BannerHeight; y++)
                for (int x = 0; x < BannerWidth; x++)
                {
                    float dx = (x - pole - 3) / 9f, dy = (y - BannerFoot - 1.5f) / 3.6f;
                    if (dx * dx + dy * dy < 1) image[x, y] = GroundShadow;
                }
            // The flag: each column rides the fold passing under it, more the farther from the pole.
            for (int i = 0; i < flagLength; i++)
            {
                double phase = i * 0.62 - frame * Math.PI / 2;
                float far = i / (float)(flagLength - 1);
                int lift = (int)Math.Round(Math.Sin(phase) * (0.4f + 1.7f * far));
                // The fold's slope lights or shades the cloth.
                double slope = Math.Cos(phase) * far;
                for (int j = 0; j < flagHeight; j++)
                {
                    // Swallow tail: a notch into the end between the two points; the end frays.
                    int mid = Math.Abs(j - flagHeight / 2);
                    int length = flagLength - (mid < 3 ? 3 - mid : 0) - (j % 5 == 3 ? 1 : 0);
                    if (i >= length) continue;
                    int x = pole + 2 + i, y = flagTop + j + lift;
                    if (!image.Inside(x, y)) continue;
                    bool edge = j == 0 || j == flagHeight - 1 || i == length - 1;
                    bool band = j <= 2 || j >= flagHeight - 3 || i <= 1;
                    int ei = i - 5, ej = j - 6;
                    bool emblem = ej >= 0 && ej < Emblem.Length && ei >= 0 && ei < 7 && Emblem[ej][ei] == '#';
                    Rgba c;
                    if (edge) c = FlagInk;
                    else if (band) c = slope > 0.35 ? FlagBlackLight : FlagBlack;
                    else if (emblem) c = slope > 0.2 ? BonePale : slope > -0.5 ? Bone : Bone.Mix(FlagDark, 0.4f);
                    else if (slope > 0.55) c = FlagHot;
                    else if (slope > 0.15) c = FlagLight;
                    else if (slope > -0.3) c = FlagMain;
                    else if (slope > -0.7) c = FlagShade;
                    else c = FlagDark;
                    image[x, y] = c;
                }
            }
            // The streamer under the flag: a thin black tail with a red tip, swinging a pixel.
            int[] sway = { 0, 1, 1, 0 };
            for (int k = 0; k < 9; k++)
            {
                int x = pole + 2 + (k + 1) / 3 + (k > 3 ? sway[(frame + k / 4) % 4] : 0), y = flagTop + flagHeight + 1 + k;
                image[x, y] = k >= 7 ? FlagLight : FlagBlack;
                image[x + 1, y] = k >= 7 ? FlagShade : k % 3 == 0 ? FlagBlackLight : FlagInk;
            }
            // The pole: lit on its left, bands of iron, the bone head above a cross-piece.
            for (int y = 5; y <= BannerFoot + 1; y++)
            {
                bool iron = y == 6 || y == flagTop + flagHeight + 1 || y == BannerFoot - 6;
                image[pole, y] = iron ? PoleLight : y % 9 == 4 ? PoleMain : new Rgba(119, 108, 97);
                image[pole + 1, y] = iron ? PoleMain : PoleDark;
                if (iron) { image[pole - 1, y] = PoleMain; image[pole + 2, y] = PoleDark; }
            }
            foreach (var (x, y, c) in new[]
            {
                (pole, 0, BonePale), (pole, 1, BonePale), (pole + 1, 1, Bone), (pole - 1, 2, BonePale), (pole, 2, BonePale), (pole + 1, 2, Bone),
                (pole + 2, 2, PoleMain), (pole - 1, 3, Bone), (pole, 3, BonePale), (pole + 1, 3, Bone), (pole + 2, 3, PoleMain), (pole, 4, Bone), (pole + 1, 4, PoleMain),
                (pole - 2, 5, PoleMain), (pole - 1, 5, PoleLight), (pole + 2, 5, PoleMain), (pole + 3, 5, PoleDark),
            })
                image[x, y] = c;
            // The heap it stands in: earth with three stones, and a wedge driven in beside the pole.
            for (int y = BannerFoot - 2; y <= BannerFoot + 3; y++)
                for (int x = 0; x < 14; x++)
                {
                    float dx = (x - pole - 0.5f) / 5.6f, dy = (y - BannerFoot - 0.5f) / 3f;
                    float d = dx * dx + dy * dy;
                    if (d > 1 || (y < BannerFoot && Math.Abs(x - pole - 0.5f) < 1)) continue;
                    uint h = PixelNoise.Hash(x, y, 650);
                    image[x, y] = d > 0.72f ? FlagInk : dx + dy < -0.35f ? (h % 3 == 0 ? PoleLight : new Rgba(119, 108, 97)) : dx + dy < 0.3f ? (h % 4 == 0 ? PoleDark : PoleMain) : new Rgba(59, 46, 48);
                }
            image[pole + 2, BannerFoot - 3] = PoleLight; image[pole + 3, BannerFoot - 2] = PoleMain; image[pole + 2, BannerFoot - 2] = PoleMain;
            return image;
        }
    }
}
