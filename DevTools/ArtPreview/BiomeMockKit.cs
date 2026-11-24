using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // What the biome rework's mockups are drawn with (out/biomemock/, --only biomemock, Docs/BiomeRework.md): a small
    // sprite builder, a scene of ground and standing things sorted by their feet, coloured light, and the joined
    // fences. Pictures for a person to choose from: nothing here is in the game.
    static class BM
    {
        public const int T = 16;
        public static readonly Rgba Ink = Rgba.Hex("#140E18");

        public static Rgba C(string hex) => Rgba.Hex(hex);
        public static Ramp R(string hex) => Ramp.Of(hex);
        public static float V(int x, int y, int salt) => PixelNoise.Value(x, y, salt);

        // Value noise, smooth over the given number of pixels.
        public static float Smooth(float x, float y, float scale, int salt)
        {
            x /= scale;
            y /= scale;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = V(x0, y0, salt), b = V(x0 + 1, y0, salt), c = V(x0, y0 + 1, salt), d = V(x0 + 1, y0 + 1, salt);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        // A small sprite from rows split by '|', with a palette written "a=#112233 b=#445566".
        public static PixelImage Rows(string palette, string rows)
        {
            var colours = palette.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToDictionary(p => p[0], p => Rgba.Hex(p.Substring(2)));
            return PixelImage.FromRows(colours, rows.Split('|'));
        }

        // The shadow under a standing thing: a crisp oval, not cast to a side.
        public static void Shadow(PixelImage g, int cx, int cy, int width, int alpha = 105)
        {
            if (width < 2) return;
            float rx = width / 2f, ry = MathF.Max(1.2f, width * 0.35f / 2f);
            for (int y = (int)MathF.Floor(-ry); y <= (int)MathF.Ceiling(ry); y++)
                for (int x = (int)MathF.Floor(-rx); x <= (int)MathF.Ceiling(rx); x++)
                {
                    float u = (x + 0.5f) / rx, v = (y + 0.5f) / ry;
                    if (u * u + v * v <= 1f) g.Blend(cx + x, cy + y, new Rgba(0, 0, 0, alpha));
                }
        }

        public static void DrawAlpha(PixelImage g, PixelImage image, int x, int y, float alpha)
        {
            for (int j = 0; j < image.Height; j++)
                for (int i = 0; i < image.Width; i++)
                {
                    var c = image[i, j];
                    if (c.A > 0) g.Blend(x + i, y + j, c.WithAlpha((int)(c.A * alpha)));
                }
        }

        public static PixelImage Tinted(PixelImage image, Rgba toward, float t, float scale = 1f)
        {
            var result = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y)) result[x, y] = image[x, y].Mix(toward.WithAlpha(image[x, y].A), t).Scale(scale);
            return result;
        }

        // Ground, then the flat things, then every standing thing with its shadow, back to front by its feet.
        public static void Compose(PixelImage g, IEnumerable<MockObj> objects, bool shadows = true)
        {
            var sorted = objects.OrderBy(o => o.Flat ? 0 : 1).ThenBy(o => o.Y).ToList();
            if (shadows)
                foreach (var o in sorted.Where(o => !o.Flat && o.Shadow > 0))
                    Shadow(g, o.X, o.Y - 1, o.Lift > 0 ? (int)(o.Shadow * 0.7f) : o.Shadow, o.Lift > 0 ? 80 : 105);
            foreach (var o in sorted)
            {
                int left = o.X - o.Img.Width / 2, top = o.Y + o.Drop - o.Lift - o.Img.Height;
                if (o.Alpha >= 1f) g.Draw(o.Img, left, top);
                else DrawAlpha(g, o.Img, left, top, o.Alpha);
            }
        }

        // Coloured light: every pixel is the ground in the ambient light plus what each lamp adds in its own colour.
        public static PixelImage Light(PixelImage image, Rgba ambientColour, float ambient, IReadOnlyList<(int X, int Y, int R, Rgba C, float S)> lights)
        {
            var result = new PixelImage(image.Width, image.Height);
            float ar = ambient * (0.45f + 0.55f * ambientColour.R / 255f), ag = ambient * (0.45f + 0.55f * ambientColour.G / 255f), ab = ambient * (0.45f + 0.55f * ambientColour.B / 255f);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var p = image[x, y];
                    if (p.A == 0) continue;
                    float lr = ar, lg = ag, lb = ab;
                    foreach (var l in lights)
                    {
                        float dx = x - l.X, dy = (y - l.Y) * 1.15f;
                        float d = MathF.Sqrt(dx * dx + dy * dy) / l.R;
                        if (d >= 1f) continue;
                        // Banded, as the game's light map is.
                        float w = MathF.Floor((1 - d) * (1 - d) * 6f + 0.5f) / 6f * l.S;
                        lr += w * l.C.R / 255f;
                        lg += w * l.C.G / 255f;
                        lb += w * l.C.B / 255f;
                    }
                    result[x, y] = new Rgba((int)(p.R * MathF.Min(lr, 1.25f)), (int)(p.G * MathF.Min(lg, 1.25f)), (int)(p.B * MathF.Min(lb, 1.25f)), p.A);
                }
            return result;
        }

        public static PixelImage Hero(string tunic = "#4D7FD9", bool flip = false)
        {
            var hero = HeroArt.Figure(HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Sword), Rgba.Hex(tunic)));
            return flip ? hero.FlippedX() : hero;
        }

        // Cuts the clear rows and columns off a sprite, so its feet are its last row.
        public static PixelImage Trim(PixelImage image)
        {
            int x0 = image.Width, y0 = image.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y))
                    {
                        x0 = Math.Min(x0, x);
                        x1 = Math.Max(x1, x);
                        y0 = Math.Min(y0, y);
                        y1 = Math.Max(y1, y);
                    }
            if (x1 < 0) return image;
            var result = new PixelImage(x1 - x0 + 1, y1 - y0 + 1);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x0 + x, y0 + y];
            return result;
        }
    }

    // A sprite built from shaded shapes, lit from the upper left, then outlined.
    sealed class Sp
    {
        public readonly PixelImage I;

        public Sp(int width, int height)
        {
            I = new PixelImage(width, height);
        }

        static Rgba Tone(Ramp r, float t) => t > 0.42f ? r.Light : t > -0.15f ? r.Main : t > -0.62f ? r.Shade : r.Dark;

        public Sp Ell(float cx, float cy, float rx, float ry, Ramp r)
        {
            for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++)
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) I[x, y] = Tone(r, -dx * 0.55f - dy * 0.8f);
                }
            return this;
        }

        // The upper half of an oval standing on a flat base: a cap, a mound, a shell.
        public Sp Dome(float cx, float by, float rx, float ry, Ramp r)
        {
            for (int y = (int)MathF.Floor(by - ry); y < (int)by; y++)
                for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - by) / ry;
                    if (dx * dx + dy * dy <= 1f) I[x, y] = Tone(r, -dx * 0.5f - dy * 1.7f - 0.85f);
                }
            return this;
        }

        public Sp Box(int x, int y, int w, int h, Ramp r)
        {
            I.Fill(x, y, w, h, r.Main);
            I.Fill(x, y, w, 1, r.Light);
            I.Fill(x + w - 1, y + 1, 1, h - 1, r.Shade);
            I.Fill(x, y + h - 1, w, 1, r.Dark);
            return this;
        }

        // An upright round thing: light on its left, dark on its right.
        public Sp Col(int x, int y, int w, int h, Ramp r)
        {
            for (int i = 0; i < w; i++)
            {
                var c = i < Math.Max(1, w / 4) ? r.Light : i < w * 0.62f ? r.Main : i < w - 1 || w < 3 ? r.Shade : r.Dark;
                I.Fill(x + i, y, 1, h, c);
            }
            I.Fill(x, y + h - 1, w, 1, r.Dark);
            return this;
        }

        // A shard from a base to a point: light left of its ridge, dark right.
        public Sp Spike(float bx, float by, float half, float ax, float ay, Ramp r)
        {
            int y0 = (int)MathF.Floor(MathF.Min(ay, by)), y1 = (int)MathF.Ceiling(MathF.Max(ay, by));
            for (int y = y0; y < y1; y++)
            {
                float f = (y + 0.5f - ay) / (by - ay);
                if (f < 0 || f > 1) continue;
                float c = ax + (bx - ax) * f, w = MathF.Max(half * f, 0.5f);
                for (int x = (int)MathF.Floor(c - w); x <= (int)MathF.Ceiling(c + w); x++)
                {
                    float u = (x + 0.5f - c) / w;
                    if (u < -1 || u > 1) continue;
                    I[x, y] = u < -0.3f ? r.Light : u < 0.3f ? r.Main : u < 0.8f ? r.Shade : r.Dark;
                }
            }
            return this;
        }

        public Sp Rect(int x, int y, int w, int h, Rgba c)
        {
            I.Fill(x, y, w, h, c);
            return this;
        }

        public Sp Px(int x, int y, Rgba c)
        {
            I[x, y] = c;
            return this;
        }

        public Sp Px(Rgba c, params int[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) I[xy[i], xy[i + 1]] = c;
            return this;
        }

        public Sp Ln(int x0, int y0, int x1, int y1, Rgba c, int thick = 1)
        {
            I.Line(x0, y0, x1, y1, c, thick);
            return this;
        }

        // Recolours some of the drawn pixels, for fur, moss, spots.
        public Sp Speck(Rgba c, float density, int salt, int y0 = 0, int y1 = 999)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(I.Height, y1); y++)
                for (int x = 0; x < I.Width; x++)
                    if (I.IsSet(x, y) && BM.V(x, y, salt) < density) I[x, y] = c;
            return this;
        }

        // Two bright eyes with dark pupils to the right.
        public Sp Eyes(int x, int y, int gap, Rgba c, int size = 1)
        {
            I.Fill(x, y, size, size, c);
            I.Fill(x + gap, y, size, size, c);
            return this;
        }

        public PixelImage Out() => I.Outlined(BM.Ink);
        public PixelImage Out(Rgba c) => I.Outlined(c);
    }

    // One thing in a scene. X is the middle of its feet and Y the line they stand on, which it is sorted by; Drop is
    // how much of the picture hangs below that line, Lift how far it floats over it.
    sealed class MockObj
    {
        public PixelImage Img;
        public int X, Y, Shadow, Lift, Drop;
        public float Alpha = 1f;
        public bool Flat;
    }

    // What a biome looks like: the table of Docs/BiomeRework.md 4-1 as numbers.
    sealed class BiomeLook
    {
        public string Id, Name, Concept, Hazard, Dungeon;
        public Biome Core;
        public Rgba Floor, Wall, Top, Accent, Extra, Light, Liquid;
        public string TodayFloor;
        public (string Hex, string Role)[] Chips;
        public float Ambient;
        public int FaceH, Corner;
        public bool Chamfer;
        public string[] Map;
    }

    // The proposed ground of a biome, a pixel at a time: its own floor pattern, and walls whose top, face and outline
    // differ from biome to biome. '#' wall, '.' floor, ',' the biome's other floor, '~' its liquid or gap.
    static class BiomeGround
    {
        const int T = BM.T;

        public static PixelImage Draw(BiomeLook b, string[] map)
        {
            int w = map[0].Length, h = map.Length, f = b.FaceH;
            char edge = b.Id == "abyss" ? '~' : '#';
            char At(int cx, int cy) => cx < 0 || cy < 0 || cx >= w || cy >= h ? edge : map[cy][cx];
            int Cell(int p) => p < 0 ? -1 : p / T;
            bool Solid(int px, int py)
            {
                int cx = Cell(px), cy = Cell(py);
                if (At(cx, cy) != '#') return false;
                int lx = px - cx * T, ly = py - cy * T, r = b.Corner;
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        if (At(cx + sx, cy) == '#' || At(cx, cy + sy) == '#') continue;
                        int dx = sx < 0 ? lx : T - 1 - lx, dy = sy < 0 ? ly : T - 1 - ly;
                        if (b.Chamfer)
                        {
                            if (dx + dy < r) return false;
                        }
                        else if (dx < r && dy < r && (r - dx - 0.5f) * (r - dx - 0.5f) + (r - dy - 0.5f) * (r - dy - 0.5f) > r * r) return false;
                    }
                return true;
            }
            float Open(int px, int py)
            {
                int cx = Cell(px), cy = Cell(py);
                float best = 99;
                for (int j = -2; j <= 2; j++)
                    for (int i = -2; i <= 2; i++)
                    {
                        if (At(cx + i, cy + j) == '#') continue;
                        float dx = Math.Max(0, Math.Max((cx + i) * T - px, px - (cx + i) * T - T + 1));
                        float dy = Math.Max(0, Math.Max((cy + j) * T - py, py - (cy + j) * T - T + 1));
                        best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy));
                    }
                return best;
            }

            var g = new PixelImage(w * T, h * T);
            for (int py = 0; py < g.Height; py++)
                for (int px = 0; px < g.Width; px++)
                {
                    char c = At(Cell(px), Cell(py));
                    Rgba colour;
                    if (Solid(px, py))
                    {
                        if (Solid(px, py + f))
                        {
                            float d = Open(px, py + f);
                            colour = TopPx(b, px, py).Scale(d < 5 ? 1f : d < 12 ? 0.86f : d < 22 ? 0.72f : 0.6f);
                            if (!Solid(px, py + f + 1)) colour = b.Top.Mix(Rgba.White, 0.28f);
                            else if (!Solid(px, py - 1) || !Solid(px - 1, py) || !Solid(px + 1, py)) colour = b.Top.Scale(0.5f);
                        }
                        else
                        {
                            int j = 1;
                            while (j < f && Solid(px, py + j)) j++;
                            colour = FacePx(b, px, py, f - j, f);
                            if (j == 1 || !Solid(px - 1, py) || !Solid(px + 1, py)) colour = colour.Scale(0.55f);
                        }
                    }
                    else
                    {
                        if (c == '#') c = '.';
                        if (c == '~') colour = LiquidPx(b, px, py, At(Cell(px), Cell(py - 4)) != '~', At(Cell(px - 1), Cell(py)) != '~' || At(Cell(px + 1), Cell(py)) != '~' || At(Cell(px), Cell(py + 1)) != '~', At(Cell(px), Cell(py - 1)) != '~' ? 0 : At(Cell(px), Cell(py - 2)) != '~' ? 1 : At(Cell(px), Cell(py - 3)) != '~' ? 2 : 3);
                        else colour = FloorPx(b, px, py, c == ',');
                        if (c != '~' || b.Id != "lava")
                        {
                            if (Solid(px, py - 1) || Solid(px, py - 2)) colour = colour.Scale(0.7f);
                            else if (Solid(px, py - 3) || Solid(px - 1, py) || Solid(px + 1, py) || Solid(px, py + 1)) colour = colour.Scale(0.84f);
                        }
                    }
                    g[px, py] = colour;
                }
            Overlay(b, g, map, Solid);
            return g;
        }

        static Rgba TopPx(BiomeLook b, int x, int y)
        {
            var c = b.Top;
            float n = BM.V(x, y, 3);
            switch (b.Id)
            {
                case "cave":
                    if ((y + (int)(BM.Smooth(x, y, 9, 4) * 5)) % 5 == 0) c = c.Scale(0.88f);
                    break;
                case "mushroom":
                    c = c.Mix(BM.C("#5A5A34"), BM.Smooth(x, y, 7, 5) > 0.55f ? 0.5f : 0f);
                    break;
                case "ice":
                    if ((x + 2 * y) % 13 == 0 || (3 * x - y + 400) % 29 == 0) c = c.Mix(Rgba.White, 0.5f);
                    break;
                case "crystal":
                    if ((x + y) % 9 == 0) c = c.Scale(0.85f);
                    if (n > 0.985f) c = BM.V(x, y, 8) > 0.5f ? BM.C("#FF7FD0") : BM.C("#7FF2FF");
                    break;
                case "sand":
                    if (y % 4 == (x / 5 % 2 == 0 ? 0 : 1)) c = c.Scale(0.92f);
                    break;
                case "lava":
                    if (x % 6 == 0 || (y + (x / 6 % 2) * 3) % 6 == 0) c = c.Scale(0.62f);
                    break;
                case "lake":
                    if (BM.Smooth(x, y, 6, 6) > 0.5f) c = c.Mix(BM.C("#5E8A48"), 0.6f);
                    break;
                case "abyss":
                    if (n > 0.975f) c = BM.C("#E6D6FF");
                    break;
            }
            return n < 0.12f ? c.Scale(0.93f) : n > 0.9f ? c.Scale(1.06f) : c;
        }

        // The face of a wall, from its top row (0) down.
        static Rgba FacePx(BiomeLook b, int x, int y, int row, int f)
        {
            var c = b.Wall;
            switch (b.Id)
            {
                case "cave":
                    c = row % 3 == 2 ? c.Scale(0.8f) : BM.V(x / 3, row, 11) > 0.7f ? c.Scale(1.1f) : c;
                    break;
                case "mushroom":
                    c = c.Scale(1.1f - row * 0.06f);
                    if (BM.V(x / 2, y / 2, 12) > 0.93f) c = BM.C("#C8A070");
                    break;
                case "ice":
                    c = c.Mix(Rgba.White, BM.V(x, 0, 13) > 0.7f ? 0.35f : row < 2 ? 0.2f : 0f);
                    if (BM.V(x, 0, 14) > 0.85f) c = c.Scale(0.8f);
                    break;
                case "crystal":
                    c = (x + y) % 6 == 0 ? c.Scale(1.3f) : (x - y + 600) % 9 == 0 ? c.Scale(0.75f) : c;
                    break;
                case "sand":
                    c = row / 2 % 2 == 0 ? c : b.Extra;
                    if (BM.V(x / 4, row / 2, 15) > 0.8f) c = c.Scale(0.9f);
                    break;
                case "lava":
                    c = x % 5 == 0 ? c.Scale(0.55f) : x % 5 == 1 ? c.Scale(1.25f) : c;
                    if (row >= f - 3 && BM.V(x / 5, 0, 16) > 0.7f && x % 5 == 2) c = BM.C("#FF6A20");
                    break;
                case "lake":
                    c = BM.V(x, 0, 17) > 0.75f ? c.Scale(0.78f) : c;
                    if (row == 0 && BM.V(x, 1, 17) > 0.4f) c = BM.C("#5E8A48");
                    break;
                case "abyss":
                    c = c.Mix(BM.C("#05030A"), Math.Min(1f, row / (float)(f - 1)));
                    if (BM.V(x, y, 18) > 0.97f && row < 4) c = BM.C("#A050FF");
                    break;
            }
            return c;
        }

        static Rgba FloorPx(BiomeLook b, int x, int y, bool other)
        {
            var c = b.Floor;
            float n = BM.V(x, y, 21), s = BM.Smooth(x, y, 11, 22), s2 = BM.Smooth(x, y, 17, 23);
            switch (b.Id)
            {
                case "cave":
                    if (s > 0.6f) c = c.Scale(1.12f);
                    if (MathF.Abs(s2 - 0.5f) < 0.012f) c = BM.C("#5E4A36");
                    if (n < 0.03f) c = BM.C("#6A5440");
                    else if (n > 0.975f) c = c.Scale(0.75f);
                    break;
                case "mushroom":
                    if (s > 0.56f) c = BM.C("#2F3A1E");
                    if (s > 0.68f) c = BM.C("#3D4E26");
                    if (MathF.Abs(s2 - 0.5f) < 0.011f) c = BM.C("#8E927A");
                    if (n < 0.025f) c = BM.C("#56682E");
                    break;
                case "ice":
                    if (other)
                    {
                        c = BM.C("#56748E");
                        if ((x - y + 800) % 11 == 0 && n > 0.35f) c = BM.C("#C4E4F6");
                        else if ((x - y + 800) % 11 == 1) c = BM.C("#6688A2");
                    }
                    else
                    {
                        if (MathF.Abs(s2 - 0.5f) < 0.01f) c = BM.C("#22303E");
                        if (s > 0.62f) c = c.Scale(1.08f);
                        if (n < 0.02f) c = BM.C("#8FB4CC");
                    }
                    break;
                case "crystal":
                {
                    int row = y / 9, ox = x + (row % 2) * 7 + (int)(BM.V(row, 0, 24) * 5);
                    if (y % 9 == 0 || ox % 14 == 0) c = c.Scale(0.6f);
                    else if (y % 9 == 1 || ox % 14 == 1) c = c.Scale(1.18f);
                    else c = c.Scale(0.94f + BM.V(ox / 14, row, 25) * 0.16f);
                    if (n > 0.992f) c = BM.V(x, y, 26) > 0.5f ? BM.C("#FF7FD0") : BM.C("#7FF2FF");
                    break;
                }
                case "sand":
                    if (other)
                    {
                        int cx = x / T * T + 8, cy = y / T * T + 8;
                        float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy) * 1.6f) + s * 6;
                        c = (int)d % 4 < 2 ? BM.C("#6E5030") : BM.C("#7E5E3A");
                    }
                    else
                    {
                        float wave = MathF.Sin(y * 0.95f + s * 9f + x * 0.08f);
                        if (wave > 0.78f) c = BM.C("#A27E54");
                        else if (wave < -0.86f) c = c.Scale(0.9f);
                        if (n < 0.015f) c = BM.C("#5A3C24");
                    }
                    break;
                case "lava":
                    if (s > 0.58f) c = BM.C("#2A2020");
                    if (MathF.Abs(s2 - 0.5f) < 0.011f || MathF.Abs(s - 0.42f) < 0.008f) c = BM.Smooth(x, y, 30, 27) > 0.5f ? BM.C("#FF6A20") : BM.C("#0C0808");
                    if (n < 0.012f) c = BM.C("#4A3A38");
                    break;
                case "lake":
                {
                    int gx = x / 5, gy = y / 4;
                    int ox = (int)(BM.V(gx, gy, 28) * 2), oy = (int)(BM.V(gx, gy, 29) * 2);
                    int lx = x - gx * 5 - ox, ly = y - gy * 4 - oy;
                    if (BM.V(gx, gy, 30) > 0.45f && lx >= 0 && lx < 3 && ly >= 0 && ly < 2) c = ly == 0 && lx < 2 ? BM.C("#3A5A58") : BM.C("#2C4848");
                    if (other)
                    {
                        c = c.Mix(BM.C("#3F9A94"), 0.5f);
                        if (MathF.Sin(y * 1.3f + s * 10f) > 0.9f) c = BM.C("#8FE6D8");
                    }
                    break;
                }
                case "abyss":
                {
                    int row = (y + 3) / 16, ox = x + (row % 2) * 13 + (int)(BM.V(row, 1, 31) * 9);
                    int ly = (y + 3) % 16, lx = ox % 29;
                    if (ly < 1 || lx < 1) c = BM.C("#05030A");
                    else if (ly == 1 || lx == 1) c = BM.C("#2E2244");
                    else c = c.Scale(0.9f + BM.V(ox / 29, row, 32) * 0.5f);
                    if (other && ly >= 2 && lx >= 2 && (MathF.Abs(s - 0.5f) < 0.03f || MathF.Abs(BM.Smooth(x, y, 5, 33) - 0.5f) < 0.04f)) c = BM.C("#A050FF");
                    if (n > 0.99f) c = BM.C("#E6D6FF");
                    break;
                }
            }
            return c;
        }

        // Lava, water or the gap between the abyss's plates. Bank is the strip under the floor above it, depth how
        // many pixels under that edge.
        static Rgba LiquidPx(BiomeLook b, int x, int y, bool bank, bool rim, int depth)
        {
            float s = BM.Smooth(x, y, 8, 41), n = BM.V(x, y, 42);
            if (b.Id == "lava")
            {
                if (bank && depth < 2) return BM.C("#3A1408");
                if (bank || rim) return BM.C("#FFC040");
                return s > 0.62f ? BM.C("#FFB030") : s < 0.3f ? BM.C("#B8380C") : n > 0.97f ? BM.C("#FFE080") : BM.C("#FF6A20");
            }
            if (b.Id == "abyss")
            {
                if (bank) return depth == 0 ? BM.C("#3A2A5A") : depth == 1 ? BM.C("#221830") : depth == 2 ? BM.C("#160F22") : BM.C("#0C0814");
                return n > 0.994f ? BM.C("#E6D6FF") : n > 0.985f ? BM.C("#3A2466") : BM.Smooth(x, y, 26, 43) > 0.62f ? BM.C("#0B0714") : BM.C("#05030A");
            }
            if (bank && depth < 2) return BM.C("#10262C");
            if (bank || rim) return BM.C("#3F9A94");
            return MathF.Sin(y * 0.8f + s * 9f) > 0.975f ? BM.C("#3FA8A0") : s > 0.6f ? BM.C("#164852") : BM.C("#113A46");
        }

        // What hangs off the walls: icicles under ice faces, crystal tips over crystal rims, moss and shelf fungi.
        static void Overlay(BiomeLook b, PixelImage g, string[] map, Func<int, int, bool> solid)
        {
            for (int y = 1; y < g.Height - 1; y++)
                for (int x = 1; x < g.Width - 1; x++)
                {
                    // The first floor pixel under a face.
                    if (!solid(x, y - 1) || solid(x, y) || map[Math.Min(map.Length - 1, y / T)][x / T] == '~') continue;
                    float n = BM.V(x, 0, 51);
                    if (b.Id == "ice" && n > 0.55f)
                    {
                        int len = 1 + (int)(BM.V(x, 1, 51) * 5);
                        for (int j = 0; j < len; j++) g[x, y + j] = j == len - 1 ? BM.C("#DCEFFA") : BM.C("#A8D0E8");
                    }
                    if (b.Id == "lake" && n > 0.7f) g[x, y] = BM.C("#4A7040");
                    if (b.Id == "mushroom" && n > 0.93f)
                    {
                        g.Fill(x - 2, y - 4, 5, 1, BM.C("#E8B070"));
                        g.Fill(x - 1, y - 3, 3, 1, BM.C("#A86A3A"));
                    }
                    if (b.Id == "crystal" && n > 0.86f && x % 3 == 0)
                    {
                        var ramp = BM.V(x, 2, 51) > 0.5f ? BM.R("#FF7FD0") : BM.R("#7FF2FF");
                        int top = y - b.FaceH - 2 - (int)(BM.V(x, 3, 51) * 5);
                        for (int j = top; j < y - b.FaceH + 2; j++)
                        {
                            g[x, j] = ramp.Light;
                            g[x + 1, j] = j == top ? ramp.Light : ramp.Shade;
                        }
                    }
                }
        }

        // The same layout in the game's tiles as they are today.
        public static PixelImage Today(BiomeLook b, string[] map)
        {
            int w = map[0].Length, h = map.Length;
            var g = new PixelImage(w * T, h * T);
            char At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? '#' : map[y][x];
            GroundKind Kind(char c) => c == '#' ? GroundKind.Rock : c == '~' && b.Id == "lava" ? GroundKind.Lava : c == '~' && b.Id == "lake" ? GroundKind.Water : GroundKind.Floor;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var kind = Kind(At(x, y));
                    bool Edge(int i, int j) => kind == GroundKind.Floor || kind == GroundKind.Rock ? At(i, j) == '#' : Kind(At(i, j)) != kind;
                    int mask = (Edge(x, y - 1) ? TileArt.Up : 0) | (Edge(x + 1, y) ? TileArt.Right : 0) | (Edge(x, y + 1) ? TileArt.Down : 0) | (Edge(x - 1, y) ? TileArt.Left : 0);
                    g.Draw(TileArt.Ground(TileArt.Index(kind, mask, (int)(PixelNoise.Hash(x, y, 7) % TileArt.Variants), b.Core)), x * T, y * T);
                }
            return g;
        }
    }

    // A scene: a biome's ground, the things standing on it, its lights and what drifts in its air.
    sealed class MockScene
    {
        public readonly BiomeLook B;
        public readonly string[] Map;
        public readonly List<MockObj> Objs = new List<MockObj>();
        public readonly List<(int X, int Y, int R, Rgba C, float S)> Lights = new List<(int, int, int, Rgba, float)>();
        // Drawn over the ground before anything stands on it (hazard marks), and over everything after the light.
        public Action<PixelImage> Under, Over;

        public MockScene(BiomeLook b, string[] map = null)
        {
            B = b;
            Map = map ?? b.Map;
        }

        public int W => Map[0].Length;
        public int H => Map.Length;
        public char At(int cx, int cy) => cx < 0 || cy < 0 || cx >= W || cy >= H ? '#' : Map[cy][cx];

        // Stands a thing in a tile (fractions for things over several tiles), nudged by a few pixels.
        public MockObj Put(PixelImage img, float tx, float ty, int ox = 0, int oy = 0, int shadow = -1, int lift = 0, bool flat = false, float alpha = 1f)
        {
            var o = new MockObj { Img = img, X = (int)(tx * BM.T) + 8 + ox, Y = (int)(ty * BM.T) + 13 + oy, Shadow = flat ? 0 : shadow < 0 ? Math.Max(4, (int)(img.Width * 0.75f)) : shadow, Lift = lift, Flat = flat, Alpha = alpha };
            Objs.Add(o);
            return o;
        }

        public void Glow(MockObj o, Rgba colour, int radius, float strength = 0.8f, int up = 6) => Lights.Add((o.X, o.Y - up - o.Lift, radius, colour, strength));

        // Cover scattered by Poisson's rule: no two closer than the least distance, more of it near walls, none
        // near what already stands.
        public void Scatter(Func<int, PixelImage> pick, float per100, int least, int salt, char on = '.')
        {
            int cells = Map.Sum(row => row.Count(c => c == on)), want = (int)(cells * per100 / 100f), placed = 0;
            var mine = new List<(int X, int Y)>();
            for (int i = 0; i < 6000 && placed < want; i++)
            {
                int x = (int)(BM.V(i, 1, salt) * W * BM.T), y = (int)(BM.V(i, 2, salt) * H * BM.T);
                int cx = x / BM.T, cy = y / BM.T;
                if (At(cx, cy) != on || At(cx, (y - 5) / BM.T) != on || At((x - 4) / BM.T, cy) != on || At((x + 4) / BM.T, cy) != on) continue;
                bool nearWall = false;
                for (int j = -2; j <= 2; j++)
                    for (int k = -2; k <= 2; k++) nearWall |= At(cx + k, cy + j) == '#';
                if (!nearWall && BM.V(i, 3, salt) > 0.5f) continue;
                if (mine.Any(m => (m.X - x) * (m.X - x) + (m.Y - y) * (m.Y - y) < least * least)) continue;
                if (Objs.Any(o => !o.Flat && Math.Abs(o.X - x) < o.Img.Width / 2 + 5 && y > o.Y - 8 && y < o.Y + 7)) continue;
                mine.Add((x, y));
                var img = pick(i);
                if (BM.V(i, 4, salt) > 0.5f) img = img.FlippedX();
                Objs.Add(new MockObj { Img = img, X = x, Y = y, Shadow = Math.Max(3, img.Width - 2) });
                placed++;
            }
        }

        public PixelImage Render(bool props = true, bool lit = true)
        {
            var g = BiomeGround.Draw(B, Map);
            if (props)
            {
                Under?.Invoke(g);
                BM.Compose(g, Objs);
            }
            if (lit) g = BM.Light(g, B.Light, B.Ambient, props ? Lights : new List<(int, int, int, Rgba, float)>());
            if (props) Over?.Invoke(g);
            return g;
        }
    }

    // Fences, hedges, stone walls and iron railings that join their neighbours (Docs/BiomeRework.md 4-4): one picture a
    // tile, 16 wide and 24 tall (8 pixels of headroom over the tile), chosen by which sides have a neighbour
    // (N 1, E 2, S 4, W 8). Seen from the front and a little above: a run east-west shows its rails' faces, a run
    // north-south only their tops.
    static class FenceMock
    {
        public const int Head = 8;
        public const int N = 1, E = 2, S = 4, W = 8;
        public const int PostFoot = 9;
        static readonly Rgba L = BM.C("#E6B878"), M = BM.C("#B88448"), Sh = BM.C("#8A5C30"), D = BM.C("#5A3A20"), O = BM.C("#2A1A12");
        static readonly Rgba GL = BM.C("#F2D494"), GM = BM.C("#D0A05C"), GS = BM.C("#9E7038");

        static void P(PixelImage g, int x, int y, Rgba c) => g[x, y + Head] = c;

        static void Post(PixelImage g, int x, int top, int foot, bool round = false)
        {
            for (int y = top; y <= foot; y++)
            {
                P(g, x - 1, y, O);
                P(g, x + 4, y, O);
                P(g, x, y, M);
                P(g, x + 1, y, M);
                P(g, x + 2, y, Sh);
                P(g, x + 3, y, Sh);
            }
            P(g, x, top + 1, L);
            for (int i = 0; i < 4; i++)
            {
                P(g, x + i, top, L);
                P(g, x + i, top - 1, O);
                P(g, x + i, foot, D);
            }
            if (round)
            {
                P(g, x, top, O);
                P(g, x + 3, top, O);
                P(g, x - 1, top, Rgba.Clear);
                P(g, x + 4, top, Rgba.Clear);
                P(g, x, top - 1, Rgba.Clear);
                P(g, x + 3, top - 1, Rgba.Clear);
                P(g, x + 1, top - 1, L);
                P(g, x + 2, top - 1, L);
                P(g, x + 1, top - 2, O);
                P(g, x + 2, top - 2, O);
            }
        }

        // Two rails seen from the front: a lit top edge over a face.
        static void Rails(PixelImage g, int x0, int x1, bool topGone = false, bool gate = false)
        {
            foreach (int r in new[] { 0, 5 })
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (topGone && r == 0 && x != (x0 == 0 ? x1 : x0)) continue;
                    P(g, x, r - 1, O);
                    P(g, x, r, gate ? GL : L);
                    P(g, x, r + 1, gate ? GM : M);
                    P(g, x, r + 2, gate ? GS : Sh);
                    P(g, x, r + 3, O);
                }
            }
        }

        // The rails running away north: only their tops show, one over the other, from the foot of the post behind
        // to this post's cap.
        static void NorthStrip(PixelImage g)
        {
            for (int y = -6; y <= 0; y++)
            {
                P(g, 6, y, O);
                P(g, 7, y, L);
                P(g, 8, y, Sh);
                P(g, 9, y, O);
            }
        }

        // The rails leaving south, over the front of this post.
        static void SouthStrip(PixelImage g)
        {
            for (int y = 0; y <= PostFoot; y++)
            {
                P(g, 7, y, L);
                P(g, 8, y, M);
            }
        }

        // worn: 1 a rail missing, 2 the post leaning.
        public static PixelImage Wood(int mask, int worn = 0)
        {
            var g = new PixelImage(16, 24);
            int bits = (mask & 1) + (mask >> 1 & 1) + (mask >> 2 & 1) + (mask >> 3 & 1);
            if ((mask & N) != 0) NorthStrip(g);
            Post(g, 6, bits == 0 ? 2 : bits == 1 ? -4 : -3, PostFoot, bits == 1);
            if ((mask & E) != 0) Rails(g, 11, 15, worn == 1);
            if ((mask & W) != 0) Rails(g, 0, 4);
            if ((mask & S) != 0) SouthStrip(g);
            if (worn == 2)
            {
                var lean = new PixelImage(16, 24);
                for (int y = 0; y < 24; y++)
                    for (int x = 5; x <= 10; x++)
                    {
                        int shift = y < Head + 1 ? 2 : y < Head + 5 ? 1 : 0;
                        lean[x + shift, y] = g[x, y];
                        g[x, y] = Rgba.Clear;
                    }
                g.Draw(lean, 0, 0);
            }
            return g;
        }

        // A gate in a straight run. along: true in an east-west run.
        public static PixelImage Gate(bool along, bool open)
        {
            var g = new PixelImage(16, 24);
            if (along)
            {
                Post(g, 0, -3, PostFoot);
                Post(g, 12, -3, PostFoot);
                if (!open)
                {
                    for (int x = 4; x <= 11; x++)
                    {
                        P(g, x, -2, O);
                        for (int y = -1; y <= 7; y++) P(g, x, y, x == 7 ? GS : (x - 4) % 4 == 0 ? GL : GM);
                        P(g, x, -1, GL);
                        P(g, x, 7, GS);
                        P(g, x, 8, O);
                        P(g, x, 1, GS);
                        P(g, x, 5, GS);
                    }
                    P(g, 10, 3, BM.C("#C8CCD8"));
                    P(g, 11, 3, BM.C("#7A8090"));
                }
                else
                {
                    // Swung toward the viewer on the west post: its edge, running down the screen.
                    for (int y = -1; y <= 15; y++)
                    {
                        P(g, 4, y, GL);
                        P(g, 5, y, GM);
                        P(g, 6, y, GS);
                        P(g, 7, y, O);
                    }
                    for (int x = 4; x <= 6; x++)
                    {
                        P(g, x, -2, O);
                        P(g, x, 15, O);
                        P(g, x, 5, GS);
                        P(g, x, 11, GS);
                    }
                }
                return g;
            }
            // North-south: a post at each end of the tile, and the gate's top between them.
            Post(g, 6, -8, -1);
            if (!open)
            {
                for (int y = -6; y <= 9; y++)
                {
                    P(g, 5, y, O);
                    P(g, 6, y, GL);
                    P(g, 7, y, GL);
                    P(g, 8, y, GM);
                    P(g, 9, y, GS);
                    P(g, 10, y, O);
                    if (y % 4 == 0)
                    {
                        P(g, 8, y, GS);
                        P(g, 9, y, D);
                    }
                }
            }
            else
            {
                // Swung east from the north post: seen from the front.
                for (int x = 10; x <= 15; x++)
                {
                    P(g, x, -8, O);
                    for (int y = -7; y <= 0; y++) P(g, x, y, x == 12 ? GS : x == 10 || x == 13 ? GL : GM);
                    P(g, x, -7, GL);
                    P(g, x, -4, GS);
                    P(g, x, 0, GS);
                    P(g, x, 1, O);
                }
            }
            Post(g, 6, 6, 15);
            return g;
        }

        // One of the five pieces alone: 0 the post, 1 the east rails, 2 the west, 3 the north strip, 4 the south.
        public static PixelImage Piece(int which)
        {
            var g = new PixelImage(16, 24);
            if (which == 0) Post(g, 6, -3, PostFoot);
            else if (which == 1) Rails(g, 11, 15);
            else if (which == 2) Rails(g, 0, 4);
            else if (which == 3) NorthStrip(g);
            else SouthStrip(g);
            return g;
        }

        static bool Bit(int mask, int bit) => (mask & bit) != 0;

        // A raised mass over a footprint (hedge, stone wall): its top is the footprint lifted by its height, its face
        // what shows under the top's front edge.
        static PixelImage Extruded(int mask, int height, float arm, float node, bool round, Func<int, int, int, Rgba> top, Func<int, int, int, Rgba> face)
        {
            bool Foot(int x, int y)
            {
                float dx = x + 0.5f - 8, dy = y + 0.5f - 8.5f;
                if (x >= 0 && x < 16 && y >= 0 && y < 16 && (round ? dx * dx + dy * dy <= node * node : MathF.Abs(dx) <= node && MathF.Abs(dy) <= node)) return true;
                if (Bit(mask, N) && MathF.Abs(dx) <= arm && dy <= 0) return true;
                if (Bit(mask, S) && MathF.Abs(dx) <= arm && dy >= 0) return true;
                if (Bit(mask, W) && MathF.Abs(dy) <= arm && dx <= 0) return true;
                if (Bit(mask, E) && MathF.Abs(dy) <= arm && dx >= 0) return true;
                return false;
            }
            bool Mass(int x, int y)
            {
                for (int k = 0; k <= height; k++)
                    if (Foot(x, y + k)) return true;
                return false;
            }
            var g = new PixelImage(16, 24);
            for (int y = -Head; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    if (!Mass(x, y)) continue;
                    if (Foot(x, y + height))
                    {
                        int toFront = 0;
                        while (toFront < 3 && Foot(x, y + height + toFront + 1)) toFront++;
                        bool edge = !Mass(x - 1, y) || !Mass(x + 1, y) || !Mass(x, y - 1);
                        P(g, x, y, edge ? O : top(x, y, toFront));
                    }
                    else
                    {
                        int toGround = 0;
                        while (Mass(x, y + toGround + 1)) toGround++;
                        bool edge = !Mass(x - 1, y) || !Mass(x + 1, y);
                        P(g, x, y, edge ? O : face(x, y, toGround));
                    }
                }
            return g;
        }

        public static PixelImage Hedge(int mask)
        {
            Rgba l = BM.C("#8CC850"), m = BM.C("#5E9A3A"), s = BM.C("#3E7030"), d = BM.C("#2A4E2A");
            return Extruded(mask, 7, 4.5f, 5.6f, true,
                (x, y, front) => front < 2 ? (BM.V(x, y, 61) > 0.3f ? l : m) : BM.V(x, y, 61) > 0.72f ? l : BM.V(x, y, 62) > 0.8f ? s : m,
                (x, y, ground) => ground == 0 ? O : BM.V(x, y, 63) > 0.62f ? d : BM.V(x, y, 64) > 0.85f ? m : s);
        }

        public static PixelImage Stone(int mask)
        {
            Rgba l = BM.C("#C4C2B4"), m = BM.C("#9A988C"), s = BM.C("#6E6C68"), d = BM.C("#4A4850");
            return Extruded(mask, 6, 3.5f, 4.5f, false,
                (x, y, front) => front == 0 ? Rgba.White.Mix(l, 0.6f) : (x + y / 3 * 3) % 5 == 0 || y % 3 == 0 && BM.V(x, y, 65) > 0.5f ? m : l,
                (x, y, ground) => ground == 0 ? d : ground % 3 == 2 || (x + ground / 3 * 3) % 6 == 0 ? d : BM.V(x / 3, ground / 3, 66) > 0.6f ? m : s);
        }

        public static PixelImage Iron(int mask)
        {
            Rgba l = BM.C("#B4BAC8"), m = BM.C("#6A7080"), s = BM.C("#3E4250");
            var g = new PixelImage(16, 24);
            void Bar(int x, int top, int foot, bool thick)
            {
                for (int y = top; y <= foot; y++)
                {
                    P(g, x, y, y == top ? l : m);
                    if (thick) P(g, x + 1, y, s);
                }
                P(g, x, top - 1, l);
            }
            if (Bit(mask, N))
                for (int y = -8; y <= 0; y++)
                {
                    P(g, 6, y, O);
                    P(g, 7, y, (y + 80) % 3 == 0 ? l : m);
                    P(g, 8, y, s);
                    P(g, 9, y, O);
                }
            foreach (int side in new[] { W, E })
            {
                if (!Bit(mask, side)) continue;
                int x0 = side == W ? 0 : 9, x1 = side == W ? 6 : 15;
                for (int x = x0; x <= x1; x++)
                {
                    P(g, x, -1, m);
                    P(g, x, 0, s);
                    P(g, x, 6, m);
                    P(g, x, 7, s);
                }
                for (int x = side == W ? 1 : 11; x <= x1; x += 3) Bar(x, -4, PostFoot, false);
            }
            for (int y = -6; y <= PostFoot; y++)
            {
                P(g, 6, y, O);
                P(g, 7, y, y < -3 ? l : m);
                P(g, 8, y, s);
                P(g, 9, y, O);
            }
            P(g, 7, -7, l);
            P(g, 8, -7, m);
            P(g, 7, -8, O);
            P(g, 8, -8, O);
            P(g, 7, PostFoot, s);
            if (Bit(mask, S))
                for (int y = 1; y <= PostFoot; y++) P(g, 7, y, (y + 80) % 3 == 0 ? l : m);
            return g;
        }

        public static PixelImage Cell(char kind, int mask) => kind switch
        {
            'H' => Hedge(mask),
            'S' => Stone(mask),
            'I' => Iron(mask),
            _ => Wood(mask),
        };

        // The things of a layout, one a tile, each sorted by its own foot. 'F' a piece, 'G' a shut gate, 'O' an open
        // one, 'w' and 'l' the worn pieces.
        public static List<MockObj> Layout(char kind, string[] rows, int left, int top)
        {
            var list = new List<MockObj>();
            bool Is(int x, int y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length && rows[y][x] != '.';
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char c = rows[y][x];
                    if (c == '.') continue;
                    int mask = (Is(x, y - 1) ? N : 0) | (Is(x + 1, y) ? E : 0) | (Is(x, y + 1) ? S : 0) | (Is(x - 1, y) ? W : 0);
                    var img = c == 'G' || c == 'O' ? Gate(mask == (E | W), c == 'O') : c == 'w' ? Wood(mask, 1) : c == 'l' ? Wood(mask, 2) : Cell(kind, mask);
                    int foot = kind == 'H' || kind == 'S' ? 13 : PostFoot;
                    // A gate standing north-south is sorted by its nearer post.
                    if ((c == 'G' || c == 'O') && mask != (E | W)) foot = 15;
                    list.Add(new MockObj { Img = img, X = left + x * 16 + 8, Y = top + y * 16 + foot + 1, Drop = 15 - foot, Shadow = kind == 'H' || kind == 'S' ? 0 : 7 });
                }
            return list;
        }
    }
}
