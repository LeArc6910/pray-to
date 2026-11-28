using System;

namespace Game.Client
{
    // R4b (2026-11-29, Docs/Bosses.md "R4b 그림", Docs/BossRedesign.md 5.6·5.8): the crown wraith gone to mist between
    // its phases, the drowned hand out of a puddle; the void lord's gate, a falling star of its maze, and the
    // hourglass that turns over above it before it winds time back.
    static partial class ForceArt
    {
        public const int MistWidth = 40, MistHeight = 24;
        public const int HandWidth = 16, HandHeight = 24, HandFrames = 6;
        const int HandFoot = 3;
        // The water line the hand comes out of, as a share of the picture's height from its bottom.
        public const float HandPivot = (HandHeight - HandFoot - 0.5f) / HandHeight;
        public const int GateWidth = 24, GateHeight = 32;
        const int GateFoot = 2;
        public const float GatePivot = (GateHeight - GateFoot - 0.5f) / GateHeight;
        public const int StarWidth = 8, StarHeight = 20;
        public const int HourglassWidth = 16, HourglassHeight = 20, HourglassFrames = 6;

        static readonly Rgba SoulPale = new Rgba(206, 250, 238);
        static readonly Rgba SoulMain = new Rgba(142, 200, 194);
        static readonly Rgba SoulGrey = new Rgba(104, 132, 138);
        static readonly Rgba SoulDark = new Rgba(44, 76, 84);
        static readonly Rgba EmberCore = new Rgba(255, 250, 200);
        static readonly Rgba EmberGold = new Rgba(255, 208, 64);

        // The wraith between its phases, placed by its middle: a low bank of soul mist, teal and grey, its clumps
        // drifting round over the four frames, wisps coming off the top; and one ember of the crown's gold wandering
        // in it (a small loop, a fading tail behind it) so it reads as the wraith and not as weather.
        public static PixelImage Mist(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(MistWidth, MistHeight);
            double turn = frame * Math.PI / 2;
            // Clumps: middle, half widths, and where in the loop each starts.
            var clumps = new (float X, float Y, float W, float H, double At)[]
            {
                (13, 15, 11, 6, 0), (26, 14, 11, 6.5f, 2.1), (20, 11, 8, 5, 4.2), (7, 17, 6, 4, 1.2), (33, 17, 6, 4, 3.3), (19, 18, 13, 4.5f, 5.1),
            };
            for (int y = 0; y < MistHeight; y++)
                for (int x = 0; x < MistWidth; x++)
                {
                    float density = 0;
                    foreach (var c in clumps)
                    {
                        float cx = c.X + 2.2f * (float)Math.Cos(turn + c.At), cy = c.Y + 1.2f * (float)Math.Sin(turn + c.At);
                        float dx = (x - cx) / c.W, dy = (y - cy) / c.H;
                        density += Math.Max(0, 1 - dx * dx - dy * dy);
                    }
                    // Broken up, more toward the edge; the grain drifts sideways with the frames.
                    density -= 0.34f * PixelNoise.Value(x + frame * 3, y, 680) + 0.1f * (((x + y + frame) & 1) == 0 ? 1 : 0);
                    if (density <= 0.06f) continue;
                    // Lit from the upper left: paler toward the top of each clump.
                    // Lit from the upper left: paler toward the top of each clump, darker folds low and inside.
                    float lit = density * 0.7f + (MistHeight - y) * 0.022f + 0.3f * PixelNoise.Value((x + frame * 2) / 3, y / 2, 681);
                    var colour = lit > 1.12f ? SoulPale : lit > 0.8f ? SoulMain : lit > 0.52f ? SoulGrey : SoulDark;
                    int alpha = density > 0.7f ? 204 : density > 0.4f ? 170 : density > 0.2f ? 124 : 80;
                    image[x, y] = colour.WithAlpha(alpha);
                }
            // Wisps rising off the top, one step a frame.
            int[] wispX = { 9, 17, 24, 31 };
            for (int k = 0; k < wispX.Length; k++)
            {
                int step = (frame + k) % Frames;
                int wx = wispX[k] + (step % 2), wy = 7 - step - k % 2;
                if (image.Inside(wx, wy)) image[wx, wy] = SoulPale.WithAlpha(190 - step * 36);
                if (image.Inside(wx, wy + 1)) image.Blend(wx, wy + 1, SoulMain.WithAlpha(150 - step * 28));
            }
            // The ember: round a small loop, the last two places behind it fading.
            (int X, int Y) Ember(int f) => (20 + (int)Math.Round(5.5 * Math.Cos(f * Math.PI / 2 + 0.6)), 13 + (int)Math.Round(2.6 * Math.Sin(f * Math.PI / 2 + 0.6)));
            var (t2x, t2y) = Ember(frame - 2);
            var (t1x, t1y) = Ember(frame - 1);
            var (ex, ey) = Ember(frame);
            image[t2x, t2y] = image[t2x, t2y].Mix(EmberGold, 0.45f).WithAlpha(200);
            image[t1x, t1y] = EmberGold.WithAlpha(235);
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                image[ex + ox, ey + oy] = image[ex + ox, ey + oy].Mix(EmberGold, 0.75f).WithAlpha(240);
            image[ex, ey] = EmberCore;
            return image;
        }

        static readonly Rgba DrownInk = new Rgba(20, 36, 46);
        static readonly Rgba DrownDark = new Rgba(58, 96, 104);
        static readonly Rgba DrownMain = new Rgba(118, 166, 160);
        static readonly Rgba DrownPale = new Rgba(190, 226, 210);
        static readonly Rgba PoolDark = new Rgba(22, 62, 96);
        static readonly Rgba PoolMain = new Rgba(62, 138, 184);
        static readonly Rgba PoolPale = new Rgba(160, 222, 244);

        // The drowned one's arm, the hand open and reaching: o outline, L lit, M skin, S shade, n a nail.
        static readonly string[] OpenHand =
        {
            "...n..n.....",
            "..oLo.oLo.n.",
            "n.oLo.oLooLo",
            "LooLooLMooMo",
            "LMoLMoLMoMSo",
            "oLMLMMMMMMSo",
            "oLMMMMMMMSo.",
            ".oLMMoMMMSo.",
            ".oLMMMMMSo..",
            "..oLMMMSSo..",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
        };

        // Shut on what it caught: three knuckles up, the thumb folded across below them.
        static readonly string[] Fist =
        {
            "............",
            "..oo.oo.oo..",
            ".oLLoLMoMMo.",
            ".oLMoLMoMSo.",
            ".oLMMMMMMSo.",
            ".oLLLLMoSSo.",
            "..oooLMoSo..",
            "..oMMooSSo..",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
            "...oLMMSo...",
            "...oLMSSo...",
        };

        // The drowned hand out of a puddle, placed by its water line (HandPivot), played once over six frames: the
        // water stirs; fingers break it with a splash; the arm is up and the hand wide; it shuts; it holds, dragging
        // down, the water ringing out; it goes under and leaves the rings. The water is drawn too (a ring in the
        // puddle's blues), so it sits on any puddle.
        public static PixelImage Hand(int frame)
        {
            frame = Math.Max(0, Math.Min(HandFrames - 1, frame));
            var image = new PixelImage(HandWidth, HandHeight);
            int line = HandHeight - HandFoot - 1;
            const float mid = 7.5f;
            float[] ringW = { 2.6f, 4.6f, 5.8f, 6.2f, 7, 7.4f };
            float rw = ringW[frame], rh = rw * 0.36f;

            void Ring(bool front)
            {
                for (int y = line - 3; y < HandHeight; y++)
                    for (int x = 0; x < HandWidth; x++)
                    {
                        if (y >= line != front) continue;
                        float dx = (x - mid) / rw, dy = (y - line) / Math.Max(0.9f, rh);
                        float d = dx * dx + dy * dy;
                        if (d > 1.25f) continue;
                        // A dark disc of stirred water, a pale rim, a second ring inside once it is wide.
                        bool rim = d > 0.72f;
                        bool inner = rw > 5.5f && d > 0.22f && d < 0.42f;
                        var colour = rim ? (dx < 0 == dy < 0 ? PoolPale : PoolMain) : inner ? PoolMain : PoolDark;
                        image[x, y] = colour.WithAlpha(rim ? 235 : inner ? 200 : 170);
                    }
            }

            void Arm(string[] rows, int shown, int left)
            {
                for (int r = 0; r < Math.Min(shown, rows.Length); r++)
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        char ch = rows[r][c];
                        if (ch == '.') continue;
                        int x = left + c, y = line - shown + r + 1;
                        if (!image.Inside(x, y)) continue;
                        image[x, y] = ch == 'o' ? DrownInk : ch == 'L' ? DrownPale : ch == 'M' ? DrownMain : ch == 'S' ? DrownDark : DrownInk.Mix(DrownDark, 0.5f);
                    }
            }

            Ring(false);
            switch (frame)
            {
                case 1: Arm(OpenHand, 6, 2); break;
                case 2: Arm(OpenHand, 17, 2); break;
                case 3: Arm(Fist, 15, 2); break;
                case 4: Arm(Fist, 12, 2); break;
                case 5: Arm(OpenHand, 3, 2); break;
            }
            Ring(true);
            // The front rim only, so the arm stands in the water and not behind it.
            if (frame >= 1 && frame <= 4)
                for (int x = 5; x <= 10; x++)
                    if (image.Inside(x, line) && image[x, line].A > 0 && Math.Abs(x - mid) < 3) image[x, line] = PoolPale.WithAlpha(245);
            // Drops: thrown up as it breaks the water, falling back as it holds.
            (int X, int Y)[] drops = frame switch
            {
                0 => new[] { (7, line - 1) },
                1 => new[] { (1, line - 5), (13, line - 6), (4, line - 9), (11, line - 10) },
                2 => new[] { (0, line - 3), (14, line - 4), (15, line - 8) },
                4 => new[] { (1, line - 2), (14, line - 2) },
                5 => new[] { (5, line - 4), (10, line - 5) },
                _ => new (int, int)[0],
            };
            foreach (var (x, y) in drops)
            {
                image[x, y] = PoolPale.WithAlpha(240);
                if (frame != 0 && image.Inside(x, y + 1) && image[x, y + 1].A == 0) image[x, y + 1] = PoolMain.WithAlpha(180);
            }
            return image;
        }

        static readonly Rgba GateInk = new Rgba(18, 6, 34);
        static readonly Rgba GateDark = new Rgba(52, 22, 92);
        static readonly Rgba GateMain = new Rgba(118, 62, 190);
        static readonly Rgba GateLight = new Rgba(184, 132, 246);
        static readonly Rgba GatePale = new Rgba(232, 208, 255);
        static readonly Rgba GateDeep = new Rgba(10, 4, 24);
        static readonly Rgba StarWhite = new Rgba(255, 255, 246);
        static readonly Rgba StarBlue = new Rgba(150, 190, 255);
        static readonly Rgba StarGold = new Rgba(255, 232, 150);

        // The void lord's gate, standing, placed by its foot line (GatePivot): an upright ring of black violet, lit on
        // its upper left, the dark inside it turning as a whirl of starlight (two arms, once round in the four
        // frames) about a white point, stars in it blinking; sparks of violet leaving the ring; its light on the
        // floor under it.
        public static PixelImage Gate(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(GateWidth, GateHeight);
            const float cx = 11.5f, cy = 14, rx = 10.2f, ry = 13.2f;
            // The light on the floor.
            int foot = GateHeight - GateFoot - 1;
            for (int y = foot - 1; y < GateHeight; y++)
                for (int x = 2; x < GateWidth - 2; x++)
                {
                    float dx = (x - cx) / 9.5f, dy = (y - foot) / 2.2f;
                    if (dx * dx + dy * dy <= 1 && ((x + y + frame) & 1) == 0) image[x, y] = GateMain.WithAlpha(120);
                }
            for (int y = 0; y < GateHeight; y++)
                for (int x = 0; x < GateWidth; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1.02f) continue;
                    if (d > 0.9f) { image[x, y] = GateInk; continue; }
                    if (d > 0.68f)
                    {
                        // The ring: light on the upper left, dark on the lower right, a pale inner lip.
                        float side = -(dx + dy) * 0.7071f / Math.Max(0.01f, d);
                        var c = d > 0.82f ? (side > 0.35f ? GateMain : GateDark) : side > 0.5f ? GatePale : side > -0.2f ? GateLight : GateMain;
                        // A band of brightness goes round it with the whirl.
                        double round = Math.Atan2(dy, dx) / (2 * Math.PI) - frame / 4.0;
                        if (round - Math.Floor(round) < 0.1 && d <= 0.82f) c = GatePale;
                        image[x, y] = c;
                        continue;
                    }
                    // Inside: the whirl.
                    float r = d / 0.68f;
                    double angle = Math.Atan2(dy, dx);
                    double arm = Math.Sin(2 * angle + r * 7 - frame * Math.PI / 2);
                    Rgba inside = arm > 0.72 ? StarBlue.Mix(StarWhite, 1 - r) : arm > 0.25 ? GateMain.Mix(StarBlue, 0.35f * (1 - r)) : arm > -0.4 ? GateDark : GateDeep;
                    if (r > 0.86f) inside = inside.Mix(GateInk, 0.6f);
                    image[x, y] = inside;
                }
            // The point at the middle and the stars in the dark.
            image[11, 14] = StarWhite; image[12, 14] = StarWhite; image[11, 13] = GatePale; image[12, 15] = GatePale;
            (int X, int Y)[] stars = { (8, 8), (15, 10), (7, 17), (14, 20), (16, 16), (10, 22), (13, 6), (6, 12) };
            for (int k = 0; k < stars.Length; k++)
            {
                int blink = (frame + k) % Frames;
                if (blink == 3) continue;
                image[stars[k].X, stars[k].Y] = blink == 0 ? StarWhite : k % 3 == 0 ? StarGold : StarBlue;
            }
            // Sparks off the ring, going outward and up.
            (int X, int Y, int Dx)[] sparks = { (2, 5, -1), (21, 4, 1), (0, 16, -1), (23, 18, 1), (4, 1, 0), (19, 1, 0) };
            for (int k = 0; k < sparks.Length; k++)
            {
                int step = (frame + k * 3) % Frames;
                if (step == 3) continue;
                int x = sparks[k].X + sparks[k].Dx * (step / 2), y = sparks[k].Y - step;
                if (image.Inside(x, y) && image[x, y].A == 0) image[x, y] = (step == 0 ? GatePale : GateLight).WithAlpha(240 - step * 50);
            }
            return image;
        }

        // A star of the maze coming down, the picture's bottom where it lands (place it by its middle and move it
        // down): a white star at the foot (a cross that beats, gold at its points), a streak of light above it going
        // thin and blue toward the top, glints running down the streak over the four frames.
        public static PixelImage Star(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(StarWidth, StarHeight);
            const int hy = StarHeight - 4;
            // The streak: two pixels wide near the star, one above, broken toward its top.
            for (int y = 0; y < hy; y++)
            {
                float along = y / (float)hy;
                int alpha = (int)(40 + 200 * along * along);
                var colour = along > 0.75f ? StarWhite : along > 0.45f ? StarGold.Mix(StarBlue, 0.4f) : StarBlue;
                bool glint = (y + StarHeight - frame * 3) % 6 == 0;
                if (along < 0.35f && (y + frame) % 2 == 0) continue;
                image[3, y] = (glint ? StarWhite : colour).WithAlpha(glint ? Math.Min(255, alpha + 90) : alpha);
                if (along > 0.4f) image[4, y] = (glint ? StarWhite : colour).WithAlpha(alpha);
                if (along > 0.7f)
                {
                    image[2, y] = StarBlue.WithAlpha(alpha / 2);
                    image[5, y] = StarBlue.WithAlpha(alpha / 2);
                }
            }
            // The star: a white heart, arms that beat long and short.
            int reach = frame % 2 == 0 ? 3 : 2;
            for (int k = 1; k <= reach; k++)
            {
                var c = k == reach ? StarGold : StarWhite;
                foreach (var (x, y) in new[] { (3 - k, hy), (4 + k, hy), (3, hy - k), (4, hy - k), (3, hy + k), (4, hy + k), (3 - k, hy + 1), (4 + k, hy + 1) })
                    if (image.Inside(x, y)) image[x, y] = c;
            }
            foreach (var (x, y) in new[] { (2, hy - 1), (5, hy - 1), (2, hy + 2), (5, hy + 2) })
                image[x, y] = (frame % 2 == 0 ? StarGold : StarBlue).WithAlpha(230);
            for (int y = hy - 1; y <= hy + 2; y++)
                for (int x = 3; x <= 4; x++) image[x, y] = StarWhite;
            image[3, hy] = StarWhite; image[4, hy + 1] = StarWhite;
            return image;
        }

        static readonly Rgba GlassGold = new Rgba(250, 200, 60);
        static readonly Rgba GlassGoldLight = new Rgba(255, 240, 160);
        static readonly Rgba GlassGoldDark = new Rgba(146, 92, 20);
        static readonly Rgba GlassViolet = new Rgba(92, 52, 160);
        static readonly Rgba GlassVioletLight = new Rgba(168, 128, 236);
        static readonly Rgba GlassSand = new Rgba(240, 214, 255);

        // The hourglass over the void lord before it winds time back, placed by its middle, played once over six
        // frames: it stands with its sand run down, turns over (a half turn, dust of violet swept round with it), and
        // stands again with the sand above and the first of it falling. Gold caps, violet glass, pale sand,
        // a dark edge so it reads over anything.
        public static PixelImage Hourglass(int frame)
        {
            frame = Math.Max(0, Math.Min(HourglassFrames - 1, frame));
            var image = new PixelImage(HourglassWidth, HourglassHeight);
            const float cx = 7.5f, cy = 9.5f;
            double a = frame * Math.PI / (HourglassFrames - 1);
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            // What the glass is at a point of its own (u across, v down, before the turn): 0 nothing.
            Rgba At(float u, float v)
            {
                float au = Math.Abs(u), av = Math.Abs(v);
                if (av > 7.6f) return Rgba.Clear;
                // The caps.
                if (av > 5.6f) return au > 5.1f ? Rgba.Clear : av > 6.6f ? (u < -1 ? GlassGoldLight : GlassGold) : GlassGoldDark;
                // The glass: two bulbs meeting at a neck.
                float half = 0.6f + av * 0.72f;
                if (au > half) return Rgba.Clear;
                // The sand lies in the bulb that was the lower one, heaped toward the cap.
                if (v > 2.2f && au < half - 0.5f) return v > 4.4f ? GlassSand : GlassVioletLight.Mix(GlassSand, 0.5f);
                if (au > half - 0.9f) return u < 0 ? GlassVioletLight : GlassViolet;
                return GlassViolet.Mix(GateDeep, 0.45f);
            }
            for (int y = 0; y < HourglassHeight; y++)
                for (int x = 0; x < HourglassWidth; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    image[x, y] = At(dx * ca + dy * sa, -dx * sa + dy * ca);
                }
            if (frame == HourglassFrames - 1)
            {
                // Turned: the sand is above now and the first of it falls through the neck.
                for (int y = 10; y <= 13; y++) image[y % 2 == 0 ? 7 : 8, y] = GlassSand;
                image[7, 14] = GlassSand; image[8, 14] = GlassSand;
            }
            image = image.Outlined(GateInk);
            // Dust swept round with the turn, and a glint when it stands.
            if (frame >= 1 && frame <= 4)
                for (int k = 0; k < 4; k++)
                {
                    double at = a - 0.5 - k * 0.28 + (k % 2 == 0 ? 0 : Math.PI);
                    int x = (int)Math.Round(cx + 7.4 * Math.Sin(at)), y = (int)Math.Round(cy - 9 * Math.Cos(at));
                    if (image.Inside(x, y) && image[x, y].A == 0) image[x, y] = (k < 2 ? GlassVioletLight : GlassGoldLight).WithAlpha(230 - k * 40);
                }
            else
            {
                int gx = frame == 0 ? 3 : 12, gy = frame == 0 ? 2 : 17;
                image[gx, gy] = StarWhite;
            }
            return image;
        }
    }
}
