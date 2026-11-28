using System;

namespace Game.Client
{
    // The forces of a boss's fight (2026-11-28, R3, Docs/Bosses.md "끌기·숨기·벽", "R3 그림"), at 16 pixels a tile: the
    // tide serpent's wall of water (a band to lay side by side, and the curled head where it stops at the gap), the
    // silk mother's thread, the wake the serpent leaves when it hunts under water, and the grains that show a pull or a
    // push. The water is SerpentArt's, the silk GroundArt's web, the sand AntlionArt's. WorldView.Forces places them.
    static partial class ForceArt
    {
        // Frames the wall, the thread and the wake turn through, a loop; the grain has GrainFrames.
        public const int Frames = 4;
        public const int GrainFrames = 2;
        // The wall is this deep (0.9 tiles); it travels toward the top of its picture.
        public const int WaveHeight = 15;
        public const int WaveEndWidth = 8;
        // The wall's and the thread's pictures repeat every this many pixels, so pieces whose lengths are multiples of
        // it (or one piece of any length) join without a seam.
        public const int Period = 32;
        public const int ThreadHeight = 3;

        static readonly Rgba WaterDark = new Rgba(22, 58, 92);
        static readonly Rgba WaterDeep = new Rgba(36, 86, 126);
        static readonly Rgba WaterMain = new Rgba(52, 118, 160);
        static readonly Rgba WaterMid = new Rgba(84, 156, 192);
        static readonly Rgba WaterLight = new Rgba(120, 196, 224);
        static readonly Rgba Foam = new Rgba(214, 244, 250);
        static readonly Rgba FoamWhite = new Rgba(244, 254, 255);
        static readonly Rgba Silk = new Rgba(226, 228, 242);
        static readonly Rgba SilkKnot = new Rgba(250, 250, 255);
        static readonly Rgba SilkGrey = new Rgba(168, 172, 204);
        static readonly Rgba Dew = new Rgba(176, 226, 246);
        static readonly Rgba DewDark = new Rgba(96, 150, 196);
        static readonly Rgba SandLight = new Rgba(242, 196, 120);
        static readonly Rgba SandMain = new Rgba(214, 164, 96);
        static readonly Rgba SandShade = new Rgba(170, 120, 70);
        static readonly Rgba SandDark = new Rgba(96, 62, 40);

        static int Loop(int frame, int frames) => (frame % frames + frames) % frames;

        static int Wrap(int x) => (x % Period + Period) % Period;

        // A length of the wall of water, lengthPx wide and WaveHeight deep, moving up its picture: spray flung ahead, a
        // crest of foam that rolls along, the lit shoulder behind it, the body, and a torn hem of dark water and spray
        // trailing. Its streaks run back a pixel a frame.
        public static PixelImage WaveBand(int lengthPx, int frame)
        {
            lengthPx = Math.Max(1, lengthPx);
            frame = Loop(frame, Frames);
            var image = new PixelImage(lengthPx, WaveHeight);
            for (int y = 0; y < WaveHeight; y++)
                for (int x = 0; x < lengthPx; x++)
                    image[x, y] = Wave(x, y, frame);
            return image;
        }

        // Where the crest's front is at x (rows from the top): it swells along the wall, the swell moving sideways.
        static int Crest(int x, int frame)
        {
            double a = 2 * Math.PI * Wrap(x) / 16 + frame * Math.PI / 2;
            double b = 2 * Math.PI * Wrap(x) / Period * 3 + 1.3;
            return 3 + (int)Math.Round(1.1 * Math.Sin(a) + 0.6 * Math.Sin(b));
        }

        static Rgba Wave(int x, int y, int frame)
        {
            int wx = Wrap(x);
            int crest = Crest(x, frame);
            uint h = PixelNoise.Hash(wx, y, 610);
            if (y < crest)
            {
                // Spray ahead of the crest: drops thrown up, most just in front of it, flickering.
                int ahead = crest - y;
                uint drop = PixelNoise.Hash(wx, y + frame * 7, 611) % 16;
                if (ahead == 1 && drop < 6) return (drop < 2 ? FoamWhite : Foam).WithAlpha(230);
                if (ahead == 2 && drop < 2) return Foam.WithAlpha(190);
                if (ahead >= 3 && drop == 0) return WaterLight.WithAlpha(150);
                return Rgba.Clear;
            }
            int into = y - crest;
            // The streaks: each column's runs back from the crest, a lit pixel every four rows moving a row a frame.
            int streak = ((y - frame + (int)(PixelNoise.Hash(wx, 0, 612) % 4)) % 4 + 4) % 4;
            bool streaked = PixelNoise.Hash(wx, 1, 612) % 3 != 0;
            if (into == 0) return (h % 5 == 0 ? Foam : FoamWhite).WithAlpha(245);
            if (into == 1) return (h % 4 == 0 ? WaterLight : Foam).WithAlpha(240);
            if (into == 2) return (streaked && streak == 0 ? Foam : h % 3 == 0 ? Foam : WaterLight).WithAlpha(235);
            int left = WaveHeight - 1 - y;
            if (left <= 2)
            {
                // The hem: torn, thinner the farther back, with flecks of foam left behind.
                uint torn = PixelNoise.Hash(wx, y + frame * 3, 613) % 8;
                if (left == 0) return torn < 2 ? WaterLight.WithAlpha(150) : torn < 5 ? WaterDark.WithAlpha(110) : Rgba.Clear;
                if (left == 1) return torn < 1 ? Foam.WithAlpha(190) : torn < 6 ? WaterDark.WithAlpha(165) : WaterDeep.WithAlpha(120);
                return torn < 1 ? WaterLight.WithAlpha(200) : WaterDark.WithAlpha(200);
            }
            if (into <= 4)
            {
                // The lit shoulder.
                if (streaked && streak == 0) return Foam.WithAlpha(230);
                return (h % 4 == 0 ? WaterLight : WaterMid).WithAlpha(228);
            }
            // The body: darker toward the back, its streaks dimmer.
            bool back = left <= 5;
            if (streaked && streak == 0) return (back ? WaterMid : WaterLight).WithAlpha(222);
            if (streaked && streak == 1 && back) return WaterDark.WithAlpha(215);
            return (back ? h % 3 == 0 ? WaterDark : WaterDeep : h % 5 == 0 ? WaterDeep : WaterMain).WithAlpha(220);
        }

        // The wall's right end, where it stops at the gap (flip it for the left end): the water curls round on itself,
        // foam all along its lip and a dark eye in the curl, the hem narrowing behind. Its left column carries the
        // band's rows on.
        public static PixelImage WaveEnd(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(WaveEndWidth, WaveHeight);
            // How far the head reaches at each row, from the crest back to the hem; it breathes a pixel.
            int[] reach = { 0, 3, 5, 6, 7, 7, 7, 6, 6, 5, 4, 4, 3, 2, 1 };
            int swell = frame == 1 ? 1 : 0, sink = frame == 3 ? 1 : 0;
            for (int y = 0; y < WaveHeight; y++)
            {
                int row = y - sink;
                int w = row < 0 ? 0 : Math.Min(WaveEndWidth, reach[Math.Min(WaveHeight - 1, row)] + (y >= 2 && y <= 8 ? swell : 0));
                for (int x = 0; x < w; x++)
                {
                    int top = Top(reach, x, sink);
                    bool lip = x == w - 1 || y == top;
                    bool under = x == w - 2 || y == top + 1;
                    if (y >= WaveHeight - 3 && !lip) { image[x, y] = Wave(x - WaveEndWidth, y, frame); continue; }
                    if (lip) image[x, y] = (PixelNoise.Hash(x, y + frame, 614) % 5 == 0 ? Foam : FoamWhite).WithAlpha(245);
                    else if (under) image[x, y] = (PixelNoise.Hash(x, y, 615) % 3 == 0 ? WaterLight : Foam).WithAlpha(238);
                    else image[x, y] = Wave(x - WaveEndWidth, Math.Max(y, 6), frame);
                }
            }
            // The eye of the curl: a dark hollow the lip turns round, with a glint, stepping round over the frames.
            int ex = 3, ey = 5 + sink;
            image[ex, ey] = WaterDark.WithAlpha(235);
            image[ex + 1, ey] = WaterDeep.WithAlpha(230);
            image[ex, ey + 1] = WaterDeep.WithAlpha(230);
            image[ex + 1, ey + 1] = WaterDark.WithAlpha(235);
            int[] gx = { 0, 1, 1, 0 }, gy = { 0, 0, 1, 1 };
            image[ex + gx[frame], ey + gy[frame]] = WaterMain.WithAlpha(235);
            image[ex + 2, ey - 1] = Foam.WithAlpha(240);
            // Drops thrown off the lip into the gap.
            int[] dx = { 6, 7, 7, 6 }, dy = { 1, 0, 2, 1 };
            image[dx[frame], dy[frame] + sink] = Foam.WithAlpha(210);
            if (frame % 2 == 0) image[7, 9 + frame / 2] = WaterLight.WithAlpha(170);
            return image;
        }

        static int Top(int[] reach, int x, int sink)
        {
            for (int y = 0; y < reach.Length; y++)
                if (reach[y] > x) return y + sink;
            return reach.Length;
        }

        // A strand of the silk mother's thread, lengthPx long and three pixels deep: the silk along the middle, grey
        // where it thins, a bright knot now and then, one bead of dew hanging on it every Period pixels, and a glint that
        // runs along it over the four frames. Near white, so the game can colour it as it nears snapping.
        public static PixelImage Thread(int lengthPx, int frame)
        {
            lengthPx = Math.Max(1, lengthPx);
            frame = Loop(frame, Frames);
            var image = new PixelImage(lengthPx, ThreadHeight);
            for (int x = 0; x < lengthPx; x++)
            {
                int wx = Wrap(x);
                uint h = PixelNoise.Hash(wx, 0, 620);
                // The glint: two pixels, moving four along a frame, every sixteen.
                int glint = ((wx - frame * 4) % 16 + 16) % 16;
                image[x, 1] = glint < 2 ? SilkKnot : h % 7 == 0 ? SilkGrey.WithAlpha(235) : Silk.WithAlpha(245);
                // A fine fuzz of loose silk off the strand.
                if (h % 11 == 3) image[x, (h >> 8) % 2 == 0 ? 0 : 2] = SilkGrey.WithAlpha(110);
                if (wx == 7 || wx == 25) image[x, 1] = SilkKnot;
                // The bead of dew: hanging under the strand, its light turning.
                if (wx >= 15 && wx <= 17)
                {
                    bool mid = wx == 16;
                    image[x, 1] = mid ? (frame % 2 == 0 ? SilkKnot : Dew) : Dew;
                    image[x, 2] = mid ? Dew : frame == 1 && wx == 15 || frame == 3 && wx == 17 ? Dew.WithAlpha(200) : DewDark.WithAlpha(200);
                    if (mid) image[x, 0] = Dew.WithAlpha(frame == 0 ? 230 : 150);
                }
            }
            return image;
        }

        public const int WakeWidth = 24, WakeHeight = 20;

        // The serpent hunting under water, from above, its head to the right: the dark of its head under the surface, a
        // bow of foam pushed up ahead of it, the two arms of the wake trailing back in broken foam that thins, and
        // ripples inside them, each falling behind over the four frames.
        public static PixelImage Wake(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(WakeWidth, WakeHeight);
            const float Slope = 0.42f;
            for (int y = 0; y < WakeHeight; y++)
                for (int x = 0; x < WakeWidth; x++)
                {
                    // Behind the bow, and off the middle line.
                    float behind = WakeWidth - 2 - x;
                    float off = Math.Abs(y - (WakeHeight - 1) / 2f);
                    if (behind < 0)
                    {
                        // The swell just ahead of the bow.
                        if (off < 1.6f && behind > -1.5f && (frame % 2 == 0 || off < 1)) image[x, y] = WaterLight.WithAlpha(150);
                        continue;
                    }
                    float arm = Slope * behind + 1.2f;
                    float from = off - arm;
                    float fade = 1 - behind / (WakeWidth - 2f);
                    if (from > 0.9f) continue;
                    if (from > -0.9f)
                    {
                        // The arm: foam near the bow, broken and thin farther back, its breaks running back.
                        int run = (((int)behind - frame) % 5 + 5) % 5;
                        bool broken = behind > 5 && run == 4 || behind > 13 && run >= 3;
                        if (broken) continue;
                        bool bright = from > -0.2f || behind < 4;
                        image[x, y] = (bright ? behind < 9 ? FoamWhite : Foam : WaterLight).WithAlpha((int)(120 + 125 * fade));
                        continue;
                    }
                    if (behind < 2.5f) { image[x, y] = (off < 1 ? FoamWhite : Foam).WithAlpha(245); continue; }
                    // The head under the water, and the neck going back from it.
                    float hx = (behind - 6) / 4.2f, hy = off / 2.6f;
                    if (hx * hx + hy * hy < 1)
                    {
                        image[x, y] = (hx * hx + hy * hy < 0.35f ? WaterDark : WaterDeep).WithAlpha(hx * hx + hy * hy < 0.35f ? 205 : 175);
                        continue;
                    }
                    if (behind >= 9 && off < 1.6f - (behind - 9) * 0.09f)
                    {
                        // The neck weaves a pixel.
                        float weave = (float)Math.Sin(behind * 0.7f - frame * Math.PI / 2) * 0.6f;
                        if (Math.Abs(y - (WakeHeight - 1) / 2f - weave) < 1.3f - (behind - 9) * 0.07f) { image[x, y] = WaterDeep.WithAlpha((int)(90 + 90 * fade)); continue; }
                    }
                    // Ripples inside the wake: chevrons a little flatter than the arms, six pixels apart, falling behind.
                    float chevron = behind - off / (Slope * 1.5f) - frame * 1.5f;
                    chevron -= 6 * (float)Math.Floor(chevron / 6);
                    if (chevron < 1 && behind > 8 && from < -1.6f && (x + y) % 3 != 0) image[x, y] = WaterLight.WithAlpha((int)(70 + 110 * fade));
                    else if (behind < 12) image[x, y] = WaterMid.WithAlpha((int)(70 * fade));
                }
            return image;
        }

        public const int GrainSize = 4;

        // A grain carried by a pull or a push, moving to the right (turn it to the force): sand (a pale grain with its
        // shade and a crumb behind) or a drop of water (a bead with a light on it and a tail). The two frames tumble.
        public static PixelImage Grain(bool water, int frame)
        {
            frame = Loop(frame, GrainFrames);
            var image = new PixelImage(GrainSize, GrainSize);
            if (water)
            {
                if (frame == 0)
                {
                    image[0, 1] = WaterLight.WithAlpha(150);
                    image[1, 1] = WaterLight.WithAlpha(235); image[2, 1] = FoamWhite; image[3, 1] = WaterLight.WithAlpha(235);
                    image[1, 2] = WaterMain.WithAlpha(235); image[2, 2] = WaterMid.WithAlpha(240); image[3, 2] = WaterMain.WithAlpha(235);
                    image[2, 0] = WaterLight.WithAlpha(210);
                    image[2, 3] = WaterDeep.WithAlpha(200);
                }
                else
                {
                    image[0, 2] = WaterLight.WithAlpha(130);
                    image[1, 2] = WaterMid.WithAlpha(220);
                    image[2, 1] = Foam.WithAlpha(245); image[3, 1] = FoamWhite;
                    image[2, 2] = WaterMain.WithAlpha(240); image[3, 2] = WaterMid.WithAlpha(240);
                    image[2, 3] = WaterDeep.WithAlpha(170); image[3, 0] = WaterLight.WithAlpha(170);
                }
                return image;
            }
            if (frame == 0)
            {
                image[0, 2] = SandShade.WithAlpha(170);
                image[2, 0] = SandLight; image[3, 1] = SandLight;
                image[2, 1] = SandMain; image[1, 1] = SandMain;
                image[2, 2] = SandShade; image[3, 2] = SandDark.WithAlpha(220);
            }
            else
            {
                image[0, 1] = SandShade.WithAlpha(170);
                image[2, 1] = SandLight; image[3, 1] = SandMain;
                image[1, 2] = SandMain; image[2, 2] = SandMain; image[3, 2] = SandShade;
                image[2, 3] = SandDark.WithAlpha(220); image[1, 0] = SandLight.WithAlpha(180);
            }
            return image;
        }
    }
}
