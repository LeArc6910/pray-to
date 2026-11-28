using System;
using Game.Core;

namespace Game.Client
{
    // A boss's ground and the things in its arena (2026-11-28, R2, Docs/Bosses.md "지형·물체·분노"), drawn at 16 pixels a
    // tile: lava (a molten orange body under a dark crust rim, bright seams that move frame to frame), cooled black
    // rock, shallow water, a glowing crack; the edge band that closes in from phase 3 (lava, petals, webs); a vent in
    // the floor. First pictures, to be redrawn (WorldView.Ground places them).
    static class GroundArt
    {
        // Frames each kind turns through.
        public const int Frames = 4;

        static readonly Rgba LavaRim = new Rgba(70, 24, 16, 235);
        static readonly Rgba LavaDeep = new Rgba(196, 64, 18, 230);
        static readonly Rgba LavaMain = new Rgba(242, 116, 30, 230);
        static readonly Rgba LavaSeam = new Rgba(255, 214, 110, 240);
        static readonly Rgba RockRim = new Rgba(18, 16, 18, 240);
        static readonly Rgba RockMain = new Rgba(46, 42, 46, 240);
        static readonly Rgba RockLight = new Rgba(78, 72, 76, 240);
        static readonly Rgba WaterRim = new Rgba(40, 80, 130, 200);
        static readonly Rgba WaterMain = new Rgba(80, 140, 200, 150);
        static readonly Rgba WaterLight = new Rgba(190, 225, 250, 190);
        static readonly Rgba CrackDark = new Rgba(40, 14, 10, 235);
        static readonly Rgba CrackGlow = new Rgba(250, 90, 30, 240);
        static readonly Rgba PetalPink = new Rgba(250, 182, 205, 210);
        static readonly Rgba PetalDeep = new Rgba(214, 112, 150, 220);
        static readonly Rgba WebWhite = new Rgba(236, 236, 244, 170);

        // One pool (a circle of width) or strip (width along, height across), in pixels.
        public static PixelImage Ground(HazardShape shape, bool strip, int width, int height, int variant, int frame)
        {
            width = Math.Max(4, width);
            height = Math.Max(4, height);
            var image = new PixelImage(width, height);
            float cx = width / 2f - 0.5f, cy = height / 2f - 0.5f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // How far in from its edge, 0 at the middle and 1 at the rim, with a ragged rim.
                    float d;
                    if (strip)
                    {
                        float across = Math.Abs(y - cy) / (height / 2f);
                        float along = Math.Abs(x - cx) / (width / 2f);
                        d = Math.Max(across + 0.08f * Wobble(x, variant), along > 0.94f ? (along - 0.94f) * 16 : 0);
                    }
                    else
                    {
                        float dx = (x - cx) / (width / 2f), dy = (y - cy) / (height / 2f);
                        double angle = Math.Atan2(dy, dx);
                        d = (float)Math.Sqrt(dx * dx + dy * dy) / (0.93f + 0.05f * (float)Math.Sin(angle * 5 + variant) + 0.03f * (float)Math.Sin(angle * 11 + variant * 2));
                    }
                    if (d > 1) continue;
                    image[x, y] = Pixel(shape, x, y, d, variant, frame);
                }
            return image;
        }

        static float Wobble(int x, int variant) => (float)Math.Sin(x * 0.7f + variant) * 0.5f + (float)Math.Sin(x * 1.9f + variant * 3) * 0.5f;

        static Rgba Pixel(HazardShape shape, int x, int y, float d, int variant, int frame)
        {
            float noise = PixelNoise.Value(x, y, 300 + variant);
            switch (shape)
            {
                case HazardShape.Lava:
                    if (d > 0.86f) return LavaRim;
                    if (d > 0.72f) return LavaDeep;
                    // Seams of bright that drift with the frame.
                    float seam = (float)Math.Sin((x + y) * 0.45f + frame * 1.6f + variant) + (float)Math.Sin((x - y) * 0.3f - frame * 1.1f);
                    return seam > 1.3f ? LavaSeam : noise > 0.8f ? LavaDeep : LavaMain;
                case HazardShape.Rock:
                    if (d > 0.88f) return RockRim;
                    return noise > 0.82f ? RockLight : noise < 0.12f ? RockRim : RockMain;
                case HazardShape.Water:
                    if (d > 0.88f) return WaterRim;
                    float ripple = (float)Math.Sin(d * 14 - frame * 1.6f);
                    return ripple > 0.85f ? WaterLight : WaterMain;
                default:
                    // A crack: a jagged dark line along the middle, glowing at its heart; the rest left clear.
                    float line = Math.Abs((float)Math.Sin(x * 0.5f + variant) * 0.25f + (float)Math.Sin(x * 1.3f) * 0.12f - (d < 1 ? 0 : 1));
                    if (d < 0.25f + line * 0.2f) return frame % 2 == 0 && noise > 0.5f ? LavaSeam : CrackGlow;
                    if (d < 0.6f) return CrackDark;
                    return noise > 0.7f ? CrackDark.WithAlpha(120) : Rgba.Clear;
            }
        }

        // The edge band: from the floor's end out to outer (radii in pixels), as a square image round the middle.
        public static PixelImage Edge(EdgeKind kind, int outer, int floor, int frame)
        {
            int size = outer * 2;
            var image = new PixelImage(size, size);
            float c = outer - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    double angle = Math.Atan2(dy, dx);
                    float inner = floor + 1.2f * (float)Math.Sin(angle * 9 + frame * 0.5f) + 0.7f * (float)Math.Sin(angle * 23);
                    if (r > outer || r < inner) continue;
                    float t = (r - inner) / Math.Max(1, outer - inner);
                    float noise = PixelNoise.Value(x, y, 500 + frame);
                    switch (kind)
                    {
                        case EdgeKind.Lava:
                            image[x, y] = r < inner + 2 ? LavaRim : t < 0.25f ? LavaDeep
                                : (float)Math.Sin(angle * 40 + r * 0.4f + frame * 1.5f) > 0.85f ? LavaSeam : noise > 0.85f ? LavaDeep : LavaMain;
                            break;
                        case EdgeKind.Petal:
                            image[x, y] = noise > 0.75f ? PetalDeep : noise > 0.35f ? PetalPink : PetalPink.WithAlpha(110);
                            break;
                        default:
                            bool strand = (int)(angle * 30) % 2 == 0 && noise > 0.5f || (int)r % 5 == 0;
                            image[x, y] = strand ? WebWhite : WebWhite.WithAlpha(40);
                            break;
                    }
                }
            return image;
        }

        // A vent: a dark mouth in the floor with a molten glow inside that breathes with the frame.
        public static PixelImage Prop(PropKind kind, int frame)
        {
            const int size = 26;
            var image = new PixelImage(size, size);
            float c = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = (y - c) * 1.25f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy) / (size / 2f);
                    if (r > 1) continue;
                    float noise = PixelNoise.Value(x, y, 700);
                    if (kind == PropKind.Vent)
                    {
                        float glow = 0.35f + 0.1f * (frame % Frames);
                        image[x, y] = r > 0.82f ? RockRim : r > 0.6f ? (noise > 0.6f ? RockLight : RockMain) : r < glow ? LavaSeam : r < glow + 0.15f ? LavaMain : LavaDeep;
                    }
                    else
                    {
                        // A pillar or a vein (R2b): a plain block of rock until they are drawn.
                        image[x, y] = r > 0.85f ? RockRim : kind == PropKind.Vein && noise > 0.8f ? new Rgba(120, 230, 255, 240) : noise > 0.7f ? RockLight : RockMain;
                    }
                }
            return image;
        }
    }
}
