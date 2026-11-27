using System;
using System.Collections.Generic;

namespace Game.Client
{
    // What the effects rework's mock-ups are drawn with (FxMockPreview, Docs/EffectsRework.md): a picture that takes
    // light added on top of it as well as paint, and the layers an effect is built from (ground mark, glow, body,
    // edge, sparks, smoke). Preview only: the game draws none of this yet.
    sealed class FxCanvas
    {
        public readonly int W, H;
        readonly float[] _p;
        // How many pieces were laid on it: each would be a sprite in the game.
        public int Pieces;

        public FxCanvas(int w, int h, PixelImage floor)
        {
            W = w;
            H = h;
            _p = new float[w * h * 3];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = floor[x % floor.Width, y % floor.Height];
                    int i = (y * w + x) * 3;
                    _p[i] = c.R;
                    _p[i + 1] = c.G;
                    _p[i + 2] = c.B;
                }
            }
        }

        public void Px(int x, int y, Rgba c, float a = 1)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0) return;
            if (a > 1) a = 1;
            int i = (y * W + x) * 3;
            _p[i] += (c.R - _p[i]) * a;
            _p[i + 1] += (c.G - _p[i + 1]) * a;
            _p[i + 2] += (c.B - _p[i + 2]) * a;
        }

        // Light: added, never darker.
        public void Add(int x, int y, Rgba c, float k)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || k <= 0) return;
            int i = (y * W + x) * 3;
            _p[i] += c.R * k;
            _p[i + 1] += c.G * k;
            _p[i + 2] += c.B * k;
        }

        public void Mul(int x, int y, float f)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = (y * W + x) * 3;
            _p[i] *= f;
            _p[i + 1] *= f;
            _p[i + 2] *= f;
        }

        public void Shade(float f)
        {
            for (int i = 0; i < _p.Length; i++) _p[i] *= f;
        }

        public void Lighten(Rgba c, float k)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Add(x, y, c, k);
        }

        public void Sprite(PixelImage s, int left, int top, float a = 1)
        {
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (s.IsSet(x, y)) Px(left + x, top + y, s[x, y], a * s[x, y].A / 255f);
        }

        public PixelImage Image()
        {
            var image = new PixelImage(W, H);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = (y * W + x) * 3;
                    image[x, y] = new Rgba((int)Math.Min(255, _p[i]), (int)Math.Min(255, _p[i + 1]), (int)Math.Min(255, _p[i + 2]));
                }
            }
            return image;
        }
    }

    static class Fx
    {
        // Five steps each, hottest first, the hue turning as it darkens: core, bright, middle, deep, dark.
        public static readonly Rgba[] Fire = Ramp("#FFF6C8", "#FFD45A", "#FF9A2E", "#E0491C", "#7A1E1E");
        public static readonly Rgba[] Ice = Ramp("#FFFFFF", "#C8F4FF", "#7AD0F5", "#3F86D6", "#2A3F8A");
        public static readonly Rgba[] Bolt_ = Ramp("#FFFFFF", "#FFF7A8", "#FFD93D", "#A98CFF", "#4B3AA0");
        public static readonly Rgba[] Poison = Ramp("#F0FFB0", "#B6F03C", "#6CC020", "#2F7F2A", "#17402A");
        public static readonly Rgba[] Spore = Ramp("#F6E0FF", "#D9A8FF", "#A86CE0", "#6A3FA8", "#33204F");
        public static readonly Rgba[] Void = Ramp("#FFD6FF", "#C77DFF", "#7B3FE4", "#3A1C8C", "#150A33");
        public static readonly Rgba[] Crystal = Ramp("#FFFFFF", "#C8F6FF", "#FF9ADF", "#B04AC8", "#4A2A7A");
        public static readonly Rgba[] Steel = Ramp("#FFFFFF", "#DCE6F2", "#9FB2C8", "#5E6F8A", "#353F55");
        public static readonly Rgba[] Earth = Ramp("#F4E4C0", "#D8B880", "#A8845A", "#6E5238", "#3A2A20");
        // A warning's colours stay the game's own red (WorldView.DangerColor) and the charge's yellow (PathColor).
        public static readonly Rgba[] Danger = Ramp("#FFE0D0", "#FF8A6A", "#FF4033", "#B8201E", "#5A1014");
        public static readonly Rgba[] Rush = Ramp("#FFF8D0", "#FFE880", "#FFD94D", "#C89A20", "#6A4A10");
        public static readonly Rgba Smoke = Rgba.Hex("#3A3238"), SmokeLight = Rgba.Hex("#6A5E62"), Dust = Rgba.Hex("#B8A080");

        static Rgba[] Ramp(params string[] hex)
        {
            var ramp = new Rgba[hex.Length];
            for (int i = 0; i < hex.Length; i++) ramp[i] = Rgba.Hex(hex[i]);
            return ramp;
        }

        public const float Tau = 6.2831853f;

        public static float C01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public static float Out3(float t)
        {
            t = C01(t);
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }

        public static float Rnd(int a, int b) => PixelNoise.Value(a, b, 977);

        public static float Cos(float a) => (float)Math.Cos(a);

        public static float Sin(float a) => (float)Math.Sin(a);

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // Thinned out in a pixel pattern instead of faded: how a pixel effect goes away.
        public static bool Thin(int x, int y, float level) => level * 16 > Bayer[(y & 3) * 4 + (x & 3)];

        public static void Disc(FxCanvas cv, float cx, float cy, float r, Rgba c, float a = 1, float thin = 1, float squash = 1)
        {
            if (r <= 0 || a <= 0) return;
            cv.Pieces++;
            for (int y = (int)(cy - r * squash) - 1; y <= cy + r * squash + 1; y++)
            {
                for (int x = (int)(cx - r) - 1; x <= cx + r + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / squash;
                    if (dx * dx + dy * dy <= r * r) cv.Px(x, y, c, a * Math.Min(1, thin));
                }
            }
        }

        public static void Ring(FxCanvas cv, float cx, float cy, float r, float thick, Rgba c, float a = 1, float from = 0, float sweep = Tau, float thin = 1)
        {
            if (r <= 0 || a <= 0) return;
            cv.Pieces++;
            float inner = Math.Max(0, r - thick);
            for (int y = (int)(cy - r) - 1; y <= cy + r + 1; y++)
            {
                for (int x = (int)(cx - r) - 1; x <= cx + r + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d2 = dx * dx + dy * dy;
                    if (d2 > r * r || d2 < inner * inner) continue;
                    if (sweep < Tau)
                    {
                        float at = ((float)Math.Atan2(-dy, dx) - from) % Tau;
                        if (at < 0) at += Tau;
                        if (at > sweep) continue;
                    }
                    if (thin >= 1 || Thin(x, y, thin)) cv.Px(x, y, c, a);
                }
            }
        }

        // Light on the floor and on what stands there: added in three bands, not a smooth blur.
        public static void Glow(FxCanvas cv, float cx, float cy, float r, Rgba c, float k)
        {
            if (r <= 0 || k <= 0) return;
            cv.Pieces++;
            for (int y = (int)(cy - r); y <= cy + r; y++)
            {
                for (int x = (int)(cx - r); x <= cx + r; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / r;
                    if (d >= 1) continue;
                    float band = d < 0.35f ? 1 : d < 0.65f ? 0.55f : 0.25f;
                    // The bands meet in a pattern.
                    if (d > 0.3f && d < 0.35f && Thin(x, y, 0.5f)) band = 0.55f;
                    if (d > 0.6f && d < 0.65f && Thin(x, y, 0.5f)) band = 0.25f;
                    if (d > 0.92f && !Thin(x, y, 0.5f)) continue;
                    cv.Add(x, y, c, k * band);
                }
            }
        }

        // What a blast leaves on the floor: darker, with a ragged edge.
        public static void Scorch(FxCanvas cv, float cx, float cy, float r, int seed, float k)
        {
            if (r <= 0 || k <= 0) return;
            cv.Pieces++;
            for (int y = (int)(cy - r) - 1; y <= cy + r + 1; y++)
            {
                for (int x = (int)(cx - r) - 1; x <= cx + r + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * 1.15f;
                    float at = (float)Math.Atan2(dy, dx);
                    float edge = r * (0.8f + 0.2f * Sin(at * 5 + seed) * Sin(at * 3 + seed * 1.7f));
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > edge) continue;
                    float depth = d < edge * 0.6f ? 1 : Thin(x, y, 0.6f) ? 1 : 0.5f;
                    cv.Mul(x, y, 1 - k * depth);
                }
            }
        }

        public static void Line(FxCanvas cv, float x0, float y0, float x1, float y1, Rgba c, float a = 1, int thick = 1, bool add = false)
        {
            cv.Pieces++;
            int steps = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float x = x0 + (x1 - x0) * i / steps, y = y0 + (y1 - y0) * i / steps;
                for (int oy = 0; oy < thick; oy++)
                {
                    for (int ox = 0; ox < thick; ox++)
                    {
                        int px = (int)Math.Floor(x) + ox - thick / 2, py = (int)Math.Floor(y) + oy - thick / 2;
                        if (add) cv.Add(px, py, c, a);
                        else cv.Px(px, py, c, a);
                    }
                }
            }
        }

        public static void Poly(FxCanvas cv, Rgba c, float a, params float[] xy)
        {
            cv.Pieces++;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            int n = xy.Length / 2;
            for (int i = 0; i < n; i++)
            {
                minX = Math.Min(minX, xy[i * 2]);
                maxX = Math.Max(maxX, xy[i * 2]);
                minY = Math.Min(minY, xy[i * 2 + 1]);
                maxY = Math.Max(maxY, xy[i * 2 + 1]);
            }
            for (int y = (int)minY; y <= maxY; y++)
            {
                for (int x = (int)minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                        if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    if (inside) cv.Px(x, y, c, a);
                }
            }
        }

        // A lump with an uneven edge that moves: fire, smoke, slime.
        public static void Blob(FxCanvas cv, float cx, float cy, float r, int seed, float phase, float wobble, Rgba c, float a = 1, float thin = 1)
        {
            if (r <= 0.3f || a <= 0) return;
            cv.Pieces++;
            float reach = r * (1 + wobble) + 1;
            for (int y = (int)(cy - reach); y <= cy + reach; y++)
            {
                for (int x = (int)(cx - reach); x <= cx + reach; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float at = (float)Math.Atan2(dy, dx);
                    float edge = r * (1 + wobble * (0.6f * Sin(at * 3 + seed + phase) + 0.4f * Sin(at * 7 + seed * 2.3f - phase * 1.7f)));
                    if (dx * dx + dy * dy <= edge * edge) cv.Px(x, y, c, a * Math.Min(1, thin));
                }
            }
        }

        // A tongue of fire standing on its base, in three colours, leaning as it flickers.
        public static void Flame(FxCanvas cv, float cx, float baseY, float tall, float wide, Rgba[] ramp, float t, int seed, float a = 1)
        {
            if (tall < 1 || a <= 0) return;
            cv.Pieces++;
            float lean = Sin(t * 14 + seed) * 1.5f;
            for (int row = 0; row < tall; row++)
            {
                float v = row / tall;
                float half = wide * 0.5f * (v < 0.25f ? 0.7f + 1.2f * v : (1 - v) / 0.75f);
                float mid = cx + lean * v * v + Sin(t * 20 + seed * 3 + row * 0.6f) * 0.6f * v;
                for (int x = (int)Math.Floor(mid - half); x <= mid + half; x++)
                {
                    float u = half < 0.5f ? 0 : Math.Abs(x + 0.5f - mid) / half;
                    float heat = (1 - u) * (1 - v * 0.9f);
                    var c = heat > 0.62f ? ramp[0] : heat > 0.4f ? ramp[1] : heat > 0.18f ? ramp[2] : ramp[3];
                    cv.Px(x, (int)(baseY - row), c, a);
                }
            }
        }

        // Lightning between two points: the line halved again and again, each middle pushed aside, with a halo.
        public static void Bolt(FxCanvas cv, float x0, float y0, float x1, float y1, int seed, Rgba[] ramp, float a = 1, int forks = 1, bool thick = true)
        {
            var points = new List<(float X, float Y)> { (x0, y0), (x1, y1) };
            float push = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 0.22f;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = points.Count - 2; i >= 0; i--)
                {
                    var (ax, ay) = points[i];
                    var (bx, by) = points[i + 1];
                    float nx = -(by - ay), ny = bx - ax, len = (float)Math.Sqrt(nx * nx + ny * ny) + 0.001f;
                    float off = (Rnd(seed * 31 + pass, i) - 0.5f) * 2 * push;
                    points.Insert(i + 1, ((ax + bx) / 2 + nx / len * off, (ay + by) / 2 + ny / len * off));
                }
                push *= 0.5f;
            }
            for (int i = 0; i + 1 < points.Count; i++)
            {
                if (thick) Line(cv, points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, ramp[3], 0.5f * a, 3);
                cv.Pieces--;
            }
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Line(cv, points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, thick ? ramp[0] : ramp[2], a, 1);
                cv.Pieces--;
            }
            cv.Pieces++;
            for (int f = 0; f < forks; f++)
            {
                var (sx, sy) = points[points.Count / 3 + f * points.Count / 4];
                float turn = (Rnd(seed, f + 9) - 0.5f) * 2.2f;
                float dx = x1 - x0, dy = y1 - y0;
                float fx = sx + (dx * Cos(turn) - dy * Sin(turn)) * 0.3f, fy = sy + (dx * Sin(turn) + dy * Cos(turn)) * 0.3f;
                if (forks > 0) Bolt(cv, sx, sy, fx, fy, seed * 7 + f, ramp, a * 0.8f, 0, false);
            }
        }

        // A splinter of ice or crystal: a long kite, lit on one side, pointing along its angle (y up).
        public static void Shard(FxCanvas cv, float cx, float cy, float length, float wide, float angle, Rgba[] ramp, float a = 1, bool outline = false)
        {
            if (length < 1 || a <= 0) return;
            float fx = Cos(angle), fy = -Sin(angle), sx = -fy, sy = fx;
            float tipX = cx + fx * length * 0.6f, tipY = cy + fy * length * 0.6f, tailX = cx - fx * length * 0.4f, tailY = cy - fy * length * 0.4f;
            float midX = cx + fx * length * 0.1f, midY = cy + fy * length * 0.1f;
            if (outline)
                Poly(cv, ramp[4], a, tipX + fx * 1.5f, tipY + fy * 1.5f, midX + sx * (wide / 2 + 1.5f), midY + sy * (wide / 2 + 1.5f), tailX - fx * 1.5f, tailY - fy * 1.5f, midX - sx * (wide / 2 + 1.5f), midY - sy * (wide / 2 + 1.5f));
            Poly(cv, ramp[3], a, tipX, tipY, midX + sx * wide / 2, midY + sy * wide / 2, tailX, tailY);
            Poly(cv, ramp[1], a, tipX, tipY, midX - sx * wide / 2, midY - sy * wide / 2, tailX, tailY);
            Line(cv, tipX, tipY, tailX, tailY, ramp[0], a);
            cv.Pieces -= outline ? 3 : 2;
        }

        // A spark with the streak it leaves behind.
        public static void Spark(FxCanvas cv, float x, float y, float dx, float dy, float length, Rgba head, Rgba tail, float a = 1)
        {
            float len = (float)Math.Sqrt(dx * dx + dy * dy) + 0.001f;
            Line(cv, x, y, x - dx / len * length, y - dy / len * length, tail, a * 0.7f);
            cv.Px((int)Math.Floor(x), (int)Math.Floor(y), head, a);
        }

        public static void Star(FxCanvas cv, float x, float y, int size, Rgba c, float a = 1)
        {
            cv.Pieces++;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            for (int i = -size; i <= size; i++)
            {
                cv.Px(cx + i, cy, c, a);
                cv.Px(cx, cy + i, c, a);
            }
            for (int i = -size / 2; i <= size / 2; i++)
            {
                cv.Px(cx + i, cy + i, c, a * 0.8f);
                cv.Px(cx + i, cy - i, c, a * 0.8f);
            }
        }

        // A blade's smear: a band round a circle, widest and brightest at its head and thinning to nothing behind it.
        public static void Crescent(FxCanvas cv, float cx, float cy, float r, float thick, float head, float sweep, Rgba[] ramp, float a = 1)
        {
            if (sweep <= 0.01f || a <= 0) return;
            cv.Pieces++;
            for (int y = (int)(cy - r) - 1; y <= cy + r + 1; y++)
            {
                for (int x = (int)(cx - r) - 1; x <= cx + r + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    float back = (head - (float)Math.Atan2(-dy, dx)) % Tau;
                    if (back < 0) back += Tau;
                    if (back > sweep) continue;
                    float u = back / sweep;
                    float wide = thick * (float)Math.Pow(1 - u, 0.7);
                    if (d < r - wide) continue;
                    float across = (r - d) / Math.Max(1, wide);
                    if (u > 0.7f && !Thin(x, y, (1 - u) / 0.3f)) continue;
                    var c = u < 0.1f || across < 0.2f && u < 0.5f ? ramp[0] : u < 0.35f ? ramp[1] : u < 0.65f ? ramp[2] : ramp[3];
                    cv.Px(x, y, c, a);
                }
            }
        }

        // A warning on the floor (the rework's): a faint wash over the whole shape, a part that fills as the blow
        // nears, a two-pixel edge that is always there, and the whole shape flashing just before it lands. The shape
        // says how far inside each pixel is (negative inside, in pixels) and how far along the filling it lies (0..1).
        public static void Warn(FxCanvas cv, Func<float, float, (float Inside, float Along)> shape, float x0, float y0, float x1, float y1, float progress, float time, Rgba[] ramp)
        {
            cv.Pieces += 3;
            bool flash = progress > 0.86f && (int)(time * 25) % 2 == 0;
            for (int y = (int)y0; y <= y1; y++)
            {
                for (int x = (int)x0; x <= x1; x++)
                {
                    var (inside, along) = shape(x + 0.5f, y + 0.5f);
                    if (inside >= 0) continue;
                    if (inside >= -1) cv.Px(x, y, flash ? ramp[0] : ramp[1], 0.95f);
                    else if (inside >= -2) cv.Px(x, y, ramp[3], 0.75f);
                    else if (along <= progress)
                    {
                        // The filling's own front is a brighter line; behind it, a pattern of the warning's colour.
                        bool front = along > progress - 0.035f;
                        cv.Px(x, y, front ? ramp[1] : ramp[2], front ? 0.7f : flash ? 0.5f : (x + y) % 4 == 0 ? 0.42f : 0.26f);
                    }
                    else cv.Px(x, y, ramp[2], 0.14f);
                }
            }
        }

        public static Func<float, float, (float, float)> CircleShape(float cx, float cy, float r) => (x, y) =>
        {
            float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return (d - r, d / r);
        };

        public static Func<float, float, (float, float)> DonutShape(float cx, float cy, float inner, float outer) => (x, y) =>
        {
            float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return (Math.Max(d - outer, inner - d), (d - inner) / (outer - inner));
        };

        // A wedge from its tip, pointing along the angle (y up), so wide in all.
        public static Func<float, float, (float, float)> ConeShape(float cx, float cy, float r, float angle, float width) => (x, y) =>
        {
            float dx = x - cx, dy = y - cy, d = (float)Math.Sqrt(dx * dx + dy * dy);
            float off = (float)Math.Atan2(-dy, dx) - angle;
            while (off > Math.PI) off -= Tau;
            while (off < -Math.PI) off += Tau;
            return (Math.Max(d - r, (Math.Abs(off) - width / 2) * d), d / r);
        };

        // A band from a point, along +x.
        public static Func<float, float, (float, float)> BandShape(float x0, float cy, float length, float wide) => (x, y) =>
            (Math.Max(Math.Max(x0 - x, x - (x0 + length)), Math.Abs(y - cy) - wide / 2), (x - x0) / length);

        // A blast as the rework builds one: light on the floor and a scorch under it (ground), then a white flash, a
        // ball of fire that swells fast and hollows into smoke, a ring running past its edge, sparks that slow as they
        // go, bits thrown up, and smoke rising after.
        public static void Blast(FxCanvas cv, bool ground, float cx, float cy, float r, float age, Rgba[] ramp, int seed, int sparks = 10, int debris = 0, bool smoke = true, float linger = 1.3f)
        {
            if (age < 0) return;
            if (ground)
            {
                float left = C01((linger - age) / 0.4f);
                if (age > 0.03f) Scorch(cv, cx, cy, r * 0.8f, seed, 0.42f * left);
                Glow(cv, cx, cy, r * 2.1f, ramp[2], 0.75f * C01(1 - age / 0.45f));
                return;
            }
            float grow = Out3(age / 0.2f);
            if (age < 0.36f)
            {
                float fade = age < 0.26f ? 1 : C01((0.36f - age) / 0.1f);
                float body = r * (0.35f + 0.6f * grow);
                Blob(cv, cx, cy, body, seed, age * 30, 0.16f, ramp[3], 1, fade);
                Blob(cv, cx, cy, body * 0.82f, seed + 1, age * 26, 0.16f, ramp[2], 1, fade);
                if (age < 0.24f) Blob(cv, cx, cy, body * 0.6f, seed + 2, age * 22, 0.14f, ramp[1]);
                if (age < 0.13f) Blob(cv, cx, cy, body * 0.36f, seed + 3, age * 20, 0.1f, ramp[0]);
                // It hollows from the middle into smoke.
                if (age > 0.15f && smoke) Blob(cv, cx, cy - 2, body * C01((age - 0.15f) / 0.2f) * 0.85f, seed + 4, age * 12, 0.2f, Smoke, 1, 0.85f);
            }
            if (age < 0.06f)
            {
                Disc(cv, cx, cy, r * 0.5f, Rgba.White, 0.9f);
                Star(cv, cx, cy, (int)(r * 0.9f), Rgba.White, 0.9f);
            }
            if (age < 0.32f) Ring(cv, cx, cy, r * (0.45f + 0.85f * Out3(age / 0.26f)), age < 0.12f ? 2 : 1, ramp[0], C01(1 - age / 0.32f));
            // The edge of what it hits, crisp for a moment.
            if (age < 0.14f) Ring(cv, cx, cy, r, 1, ramp[1], 0.9f);
            for (int i = 0; i < sparks; i++)
            {
                float life = 0.36f + 0.2f * Rnd(seed, i);
                if (age > life) continue;
                float at = (i + Rnd(seed + 5, i)) * Tau / sparks, far = r * (0.3f + (0.95f + 0.5f * Rnd(seed + 6, i)) * Out3(age / life));
                Spark(cv, cx + Cos(at) * far, cy - Sin(at) * far * 0.9f, Cos(at), -Sin(at), 4 * (1 - age / life) + 1, ramp[age < life * 0.5f ? 0 : 1], ramp[2], C01(2 - 2 * age / life));
            }
            for (int i = 0; i < debris; i++)
            {
                float life = 0.5f + 0.25f * Rnd(seed + 7, i);
                if (age > life) continue;
                float p = age / life, at = (i + 0.5f) * Tau / debris, far = r * (0.4f + 0.9f * p), up = 4 * (10 + 10 * Rnd(seed + 8, i)) * p * (1 - p);
                float x = cx + Cos(at) * far, y = cy - Sin(at) * far * 0.7f;
                Disc(cv, x, y + 1, 1.5f, Smoke, 0.35f);
                Disc(cv, x, y - up, 1.6f, Earth[4]);
                cv.Px((int)x, (int)(y - up - 1), Earth[2]);
            }
            if (smoke)
            {
                for (int i = 0; i < 5; i++)
                {
                    float born = 0.16f + i * 0.05f, life = 0.7f;
                    float p = (age - born) / life;
                    if (p < 0 || p > 1) continue;
                    float x = cx + (Rnd(seed + 9, i) - 0.5f) * r * 1.1f, y = cy + (Rnd(seed + 10, i) - 0.5f) * r * 0.6f - p * (12 + r * 0.25f);
                    Blob(cv, x, y, r * 0.16f + 3 + 3 * p, seed + i, p * 4, 0.2f, p < 0.4f ? Smoke : SmokeLight, 1, 0.8f * (1 - p));
                }
            }
        }
    }
}
