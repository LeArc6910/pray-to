using System;

namespace Game.Client
{
    // R4a (2026-11-29, Docs/Bosses.md "R4a 그림", Docs/BossRedesign.md 5.3·5.5): Morg's phantom spearmen (a band laid
    // like the wall of water, and its end at the gap), the mark under whoever his order falls on, and the ring of
    // light a crystal gives off as it chimes.
    static partial class ForceArt
    {
        public const int PhalanxEndWidth = 8;
        public const int TargetSize = 32;
        public const int ChimeSize = 24;

        static readonly Rgba GhostInk = new Rgba(34, 16, 52);
        static readonly Rgba GhostDark = new Rgba(66, 36, 98);
        static readonly Rgba GhostMain = new Rgba(116, 76, 158);
        static readonly Rgba GhostLight = new Rgba(172, 134, 214);
        static readonly Rgba GhostPale = new Rgba(226, 208, 248);
        static readonly Rgba GhostRed = new Rgba(196, 66, 74);
        static readonly Rgba GhostHot = new Rgba(246, 108, 92);

        // One spearman from behind and above, marching up the picture, 16 wide: the spear upright in his right hand,
        // his helm (a red crest on it), shoulders and cloak, a round shield on his left arm, and the hem that is not there.
        // . nothing, T spear head, s shaft, d rim, h shield lit, H shield, r its boss, m helm lit, M helm, p crest,
        // k dark, b cloak, B cloak dark, w wisp.
        static readonly string[] Spearman =
        {
            "............T...",
            "...........TTT..",
            "............T...",
            ".....kkkk...s...",
            "....kmmpMk..s...",
            "....kmMpMk..s...",
            "....kMMMMk..s...",
            "..ddkkkkkkkks...",
            ".dhhdbbbbbbks...",
            "dhHrHdbbbBbks...",
            "dHrrHdbbBBbk....",
            "dHHrHdbBBbbk....",
            ".dHHdkbBbBk.....",
            "..ddwbB.bBw.....",
            "...w.B..w.w.....",
        };

        // A length of the phantom spearmen's line, lengthPx wide and WaveHeight deep, marching up its picture: ghosts
        // of violet with red crests (every other one) and shield bosses, see-through and thinner toward their hems, a haze between them, two to a Period (so
        // lengths that are multiples of it join without a seam). Every other one steps on the off beat; the spear
        // heads glint in turn and the hems flicker.
        public static PixelImage Phalanx(int lengthPx, int frame)
        {
            lengthPx = Math.Max(1, lengthPx);
            frame = Loop(frame, Frames);
            var image = new PixelImage(lengthPx, WaveHeight);
            for (int y = 0; y < WaveHeight; y++)
                for (int x = 0; x < lengthPx; x++)
                    image[x, y] = Spear(x, y, frame);
            return image;
        }

        static Rgba Spear(int x, int y, int frame)
        {
            int wx = Wrap(x), man = wx / 16, lx = wx % 16;
            // The step: a pixel back on two frames of the four, the second man half a loop behind.
            int step = (frame + man * 2) % Frames >= 2 ? 1 : 0;
            int row = y - step;
            if (row < 0) return Rgba.Clear;
            // The hem stays where it is so the band keeps its depth.
            if (y >= WaveHeight - 2) row = y;
            char c = Spearman[row][lx];
            if (step == 1 && y == WaveHeight - 3) c = Spearman[row][lx] == '.' ? '.' : 'B';
            // Between the men a haze of the same violet, so the line reads as one band on any floor.
            if (c == '.') return y >= 5 && y <= 12 && (wx + y) % 2 == 0 ? GhostLight.WithAlpha(y >= 11 ? 50 : 84) : Rgba.Clear;
            if (man == 1 && c == 'p') c = 'M';
            int alpha = y <= 8 ? 232 : y <= 11 ? 212 : y == 12 ? 172 : y == 13 ? 128 : 88;
            uint h = PixelNoise.Hash(wx, y + frame * 5, 660);
            switch (c)
            {
                case 'T': return ((frame + man * 2) % Frames == 0 ? FoamWhite : GhostPale).WithAlpha(245);
                case 's': return (row % 3 == 0 ? GhostLight : GhostPale).WithAlpha(225);
                case 'd': return GhostInk.WithAlpha(235);
                case 'h': return GhostPale.WithAlpha(Math.Max(alpha, 200));
                case 'H': return GhostMain.WithAlpha(Math.Max(alpha, 200));
                case 'r': return (row == 9 || lx == 2 ? GhostHot : GhostRed).WithAlpha(240);
                case 'm': return GhostPale.WithAlpha(alpha);
                case 'M': return GhostLight.Mix(GhostPale, 0.35f).WithAlpha(alpha);
                case 'p': return (row == 4 ? GhostHot : GhostRed).WithAlpha(240);
                case 'k': return GhostInk.WithAlpha(alpha);
                case 'b': return (h % 5 == 0 ? GhostMain : GhostMain.Mix(GhostLight, 0.5f)).WithAlpha(alpha);
                case 'B': return GhostDark.Mix(GhostMain, 0.4f).WithAlpha(alpha);
                default: return h % 3 == 0 ? Rgba.Clear : GhostLight.WithAlpha(alpha);
            }
        }

        // The line's right end, at the gap (flip it for the left end): the last man's standard-bearer side, a spear
        // slanting out with a red pennon, and the line going to nothing in wisps and sparks.
        public static PixelImage PhalanxEnd(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(PhalanxEndWidth, WaveHeight);
            // Half a man fading out across the picture.
            for (int y = 0; y < WaveHeight; y++)
                for (int x = 0; x < PhalanxEndWidth; x++)
                {
                    var c = Spear(x, y, frame);
                    if (c.A == 0) continue;
                    uint h = PixelNoise.Hash(x, y + frame * 3, 661);
                    if (x >= 3 && h % 8 < x - 2) continue;
                    image[x, y] = c.WithAlpha(c.A * (9 - x) / 9);
                }
            // The spear out to the side, its head up and outward, the pennon under the head streaming back.
            int[] sx = { 7, 7, 6, 6, 5, 5, 4, 4, 3 };
            for (int k = 0; k < sx.Length; k++) image[sx[k], k] = (k < 2 ? FoamWhite : GhostPale).WithAlpha(k < 2 ? 250 : 230);
            image[6, 0] = GhostPale.WithAlpha(220);
            int flap = frame % 2;
            image[7, 3] = GhostHot.WithAlpha(240); image[7, 4 + flap] = GhostRed.WithAlpha(230); image[6, 4] = GhostRed.WithAlpha(235);
            image[7, 5 + flap] = GhostRed.WithAlpha(170);
            // Sparks drifting off the end.
            int[] dy = { 9, 11, 8, 12 };
            image[7, dy[frame]] = GhostHot.WithAlpha(200);
            image[6, dy[(frame + 2) % Frames] + 1] = GhostLight.WithAlpha(150);
            return image;
        }

        static readonly Rgba MarkDark = new Rgba(70, 10, 18);
        static readonly Rgba MarkRed = new Rgba(226, 52, 50);
        static readonly Rgba MarkHot = new Rgba(255, 112, 78);
        static readonly Rgba MarkPale = new Rgba(255, 214, 170);

        // The mark of Morg's order under whoever it falls on, placed by its middle: an outer ring of red in eight
        // dashes that turn (a quarter of a dash a frame, so the four frames loop), an inner ring that beats,
        // four arrowheads pointing in that close on the middle with the beat, and a pale point at the middle. Dark
        // edged so it reads on any floor.
        public static PixelImage Target(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(TargetSize, TargetSize);
            const float c = TargetSize / 2f - 0.5f;
            float inner = frame == 0 ? 9 : frame == 2 ? 8 : 8.5f;
            float tip = frame == 0 ? 4.2f : frame == 2 ? 3 : 3.6f;
            for (int y = 0; y < TargetSize; y++)
                for (int x = 0; x < TargetSize; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    // The outer ring's dashes.
                    double turn = Math.Atan2(dy, dx) / (2 * Math.PI) * 8 - frame / 4.0;
                    double within = turn - Math.Floor(turn);
                    bool dash = within < 0.7;
                    float outer = Math.Abs(d - 13.6f);
                    if (dash && outer < 1.05f) { image[x, y] = (d < 13.4f ? MarkHot : MarkRed).WithAlpha(245); continue; }
                    if (dash && outer < 1.9f) { image[x, y] = MarkDark.WithAlpha(150); continue; }
                    // The arrowheads: each from its tip toward the rim, widening.
                    float along = Math.Max(Math.Abs(dx), Math.Abs(dy)), across = Math.Min(Math.Abs(dx), Math.Abs(dy));
                    float into = along - tip;
                    if (into >= 0 && into <= 3.6f)
                    {
                        float half = into * 0.8f + 0.35f;
                        if (across <= half) { image[x, y] = (into < 1.2f ? MarkPale : across > half - 1 ? MarkRed : MarkHot).WithAlpha(250); continue; }
                        if (across <= half + 1) { image[x, y] = MarkDark.WithAlpha(150); continue; }
                    }
                    if (into > 3.6f && into <= 4.6f && across <= 3.2f) { image[x, y] = MarkDark.WithAlpha(150); continue; }
                    // The inner ring.
                    float ring = Math.Abs(d - inner);
                    if (ring < 0.75f) { image[x, y] = (frame == 2 ? MarkPale : MarkHot).WithAlpha(235); continue; }
                    if (ring < 1.5f) { image[x, y] = MarkDark.WithAlpha(120); continue; }
                    // A wash of red inside, and the point.
                    if (d < 1.2f) image[x, y] = MarkPale.WithAlpha(250);
                    else if (d < inner) image[x, y] = MarkRed.WithAlpha(frame == 2 ? 62 : 40);
                }
            return image;
        }

        static readonly Rgba ChimeWhite = new Rgba(246, 252, 255);
        static readonly Rgba ChimeTint = new Rgba(216, 240, 255);

        // The ring of light off a crystal as it chimes, placed by its middle: it starts small and bright with light
        // inside it, and goes out to the edge thinning and fading over the four frames (play it once), sparks on its
        // diagonals as it passes. Near white, so the game colours it.
        public static PixelImage Chime(int frame)
        {
            frame = Loop(frame, Frames);
            var image = new PixelImage(ChimeSize, ChimeSize);
            const float c = ChimeSize / 2f - 0.5f;
            float radius = new[] { 3.4f, 6.4f, 9, 10.9f }[frame];
            float thick = new[] { 1.5f, 1.2f, 0.9f, 0.7f }[frame];
            int alpha = new[] { 252, 220, 160, 96 }[frame];
            for (int y = 0; y < ChimeSize; y++)
                for (int x = 0; x < ChimeSize; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float off = Math.Abs(d - radius);
                    if (off < thick)
                    {
                        // The last ring breaks up.
                        if (frame == 3 && PixelNoise.Hash(x, y, 670) % 3 == 0) continue;
                        image[x, y] = (d < radius ? ChimeTint : ChimeWhite).WithAlpha(alpha);
                    }
                    else if (off < thick + 1 && frame < 3) image[x, y] = ChimeTint.WithAlpha(alpha / 3);
                    else if (d < radius && frame < 2) image[x, y] = ChimeWhite.WithAlpha(frame == 0 ? 150 : 46);
                }
            if (frame == 1 || frame == 2)
            {
                // Sparks just outside the ring on the diagonals.
                int reach = (int)Math.Round((radius + 2) * 0.7071f);
                foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    int x = (int)Math.Round(c + sx * (reach + 0.5f) - 0.5f * sx), y = (int)Math.Round(c + sy * (reach + 0.5f) - 0.5f * sy);
                    if (!image.Inside(x, y)) continue;
                    image[x, y] = ChimeWhite.WithAlpha(250);
                    if (frame == 1)
                        foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                            if (image.Inside(x + ox, y + oy)) image.Blend(x + ox, y + oy, ChimeWhite.WithAlpha(150));
                }
            }
            return image;
        }
    }
}
