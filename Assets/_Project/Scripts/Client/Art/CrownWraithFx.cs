using System;
using System.Collections.Generic;

namespace Game.Client
{
    // ---- The crown wraith's own blows (2026-11-28, Docs/Bosses.md 왕관의 망령): lightning and drowned souls, never
    // water (that is the tide serpent's). A forked bolt down out of the dark onto a circle with arcs crawling over the
    // floor; a spear of lightning thrown down a line; the claws of its spectral hand raked through a wedge, crackling;
    // lightning running round a ring's band from the crown. Lengths in pixels, angles in degrees, y up. ----
    static partial class BossEffects
    {
        // A bolt from one point to another, a new one every 1/25 s (kind 0..5; thick and a halo for the heavier).
        static void Arc(List<FxStamp> o, float t, float x0, float y0, float x1, float y1, int seed, bool thick = false, float alpha = 1, bool halo = false)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 4 || alpha <= 0.03f) return;
            int kind = (int)((uint)(seed * 7 + (int)(t * 25)) % 6);
            Put(o, FxShape.Bolt, FxRamp.Bolt, SkillFxArt.Step4(length), kind + (thick ? 8 : 0) + (halo ? 16 : 0), (x0 + x1) / 2, (y0 + y1) / 2, alpha,
                (float)(Math.Atan2(dy, dx) * 180 / Math.PI));
        }

        // A bolt too long for one piece, laid in pieces end to end with a little bend at each join.
        static void LongArc(List<FxStamp> o, float t, float x0, float y0, float x1, float y1, int seed, bool thick, float alpha, bool halo, float bend = 6)
        {
            float dx = x1 - x0, dy = y1 - y0, length = (float)Math.Sqrt(dx * dx + dy * dy);
            int pieces = Math.Max(1, (int)Math.Ceiling(length / 72));
            float nx = length > 0 ? -dy / length : 0, ny = length > 0 ? dx / length : 0;
            float px = x0, py = y0;
            int flick = (int)(t * 25);
            for (int i = 1; i <= pieces; i++)
            {
                float u = i / (float)pieces, off = i == pieces ? 0 : (Rnd(seed + flick, i, 61) - 0.5f) * 2 * bend;
                float qx = x0 + dx * u + nx * off, qy = y0 + dy * u + ny * off;
                Arc(o, t, px, py, qx, qy, seed + i * 5, thick, alpha, halo);
                (px, py) = (qx, qy);
            }
        }

        // Lightning's flicker: on, a blink off, on again, off, and a last dim one.
        static float Flick(float t) => t < 0.05f ? 1 : t < 0.07f ? 0 : t < 0.12f ? 1 : t < 0.14f ? 0 : t < 0.18f ? 0.6f : 0;

        // Small arcs crawling over the floor from the middle out to the edge, redrawn as they flicker.
        static void Crawl(List<FxStamp> o, float t, float from, float until, float R, int count, int seed, float cx = 0, float cy = 0)
        {
            if (!In(t, from, until)) return;
            float left = U(until - t, 0, (until - from) * 0.5f);
            int flick = (int)(t * 25);
            for (int i = 0; i < count; i++)
            {
                if (((flick + i) % 3) == 0) continue;
                float dir = (i + Rnd(seed, i, 62)) * 360f / count + 25 * Rnd(seed + flick, i, 63);
                float near = R * (0.15f + 0.3f * Rnd(seed + flick, i, 64)), far = R * (0.6f + 0.45f * Rnd(seed + flick, i, 65));
                Arc(o, t, cx + Cos(dir) * near, cy + Sin(dir) * near * 0.9f, cx + Cos(dir + 12) * far, cy + Sin(dir + 12) * far * 0.9f, seed + i, false, 0.85f * left);
            }
        }

        // Drowned souls torn loose by a blow, curling up and away.
        static void Souls(List<FxStamp> o, float t, float from, int count, float R, int seed, float cx = 0, float cy = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float own = 0.5f + 0.25f * Rnd(seed, i, 66), p = (t - from - 0.03f * i) / own;
                if (p < 0 || p >= 1) continue;
                float dir = (i + 0.5f) * 360f / count + 30 * Rnd(seed, i, 67), far = R * (0.3f + 0.6f * Out(p)), sway = 3 * Sin(p * 520 + i * 70);
                Put(o, FxShape.Wisp, FxRamp.Spirit, 4 + (i & 1), (int)(t * 10) + i, cx + Cos(dir) * far + sway, cy + Sin(dir) * far * 0.6f + 20 * p, 0.9f * (1 - p * p),
                    90 + (Cos(dir) > 0 ? -35 : 35) * (1 - p));
            }
        }

        // A forked bolt down out of the dark onto a circle: drawn anew as it flickers, a branch or two off it, a flash
        // and a star where it lands, arcs crawling over the floor to the edge, the floor singed, a soul or two torn
        // loose. Size the circle's radius. Big: the heavier (thicker, struck twice, more arcs).
        static float Thunder(in BossContext c, float t, List<FxStamp> o)
        {
            float R = Math.Max(10, c.Size), high = c.Big ? 120 : 96;
            float on = Math.Max(Flick(t), c.Big ? Flick(t - 0.22f) : 0);
            if (on > 0)
            {
                int flick = (int)(t * 25);
                float mx = (Rnd(c.Seed + flick, 0, 68) - 0.5f) * 16, my = high * 0.5f;
                LongArc(o, t, mx * 0.4f, high, mx, my, c.Seed, true, on, true, 4);
                LongArc(o, t, mx, my, 0, 0, c.Seed + 3, true, on, true, 4);
                // A branch or two forking off it on the way down.
                for (int b = 0; b < (c.Big ? 3 : 2); b++)
                {
                    float at = 0.3f + 0.35f * Rnd(c.Seed + flick, b, 69), side = (b & 1) == 0 ? 1 : -1;
                    float bx = mx * (1 - at) * 2, by = my * at * 2 * 0.9f;
                    Arc(o, t, bx, by, bx + side * (10 + 10 * Rnd(c.Seed, b, 70)), by - 12 - 8 * Rnd(c.Seed, b, 71), c.Seed + b + 9, false, on * 0.8f);
                }
                FxLayers.Light(o, FxRamp.Bolt, R * (c.Big ? 3.4f : 2.8f), on, 0, 0, 3);
                Put(o, FxShape.Glint, FxRamp.Bolt, Odd(Math.Min(25, R * 1.1f)), 1, 0, 2, on);
            }
            FxLayers.Scorch(o, t, 0.04f, 1.0f, R * 0.55f, c.Seed);
            FxLayers.Shock(o, FxRamp.Bolt, t, 0.02f, R, 0, 0, 0.3f, true);
            Crawl(o, t, 0.03f, c.Big ? 0.6f : 0.45f, R, c.Big ? 7 : 5, c.Seed);
            FxLayers.Embers(o, FxRamp.Bolt, c.Big ? 8 : 6, t, 0.02f, R, c.Seed);
            Souls(o, t, 0.12f, c.Big ? 4 : 2, R, c.Seed);
            return 1.0f;
        }

        // A spear of lightning thrown down a line: its head races out to the end, a thick crackling shaft behind it,
        // arcs snapping off its sides, a burst where it stops; then the shaft thins and souls drift up off it. Size the
        // line's length, Width how wide, Dir its way.
        static float Lance(in BossContext c, float t, List<FxStamp> o)
        {
            const float race = 0.08f, life = 0.7f;
            float L = Math.Max(24, c.Size), W = Math.Max(8, c.Width), dx = Cos(c.Dir), dy = Sin(c.Dir), nx = -dy, ny = dx;
            float head = L * Out(U(t, 0, race));
            float body = t < 0.26f ? 1 : U(0.42f - t, 0, 0.16f);
            if (body > 0 && head > 6)
            {
                LongArc(o, t, 0, 0, dx * head, dy * head, c.Seed, true, body, true, Math.Min(5, W * 0.25f));
                if (t < 0.2f) LongArc(o, t + 0.02f, 0, 0, dx * head, dy * head, c.Seed + 17, false, 0.7f * body, false, W * 0.4f);
                // The head of it.
                if (t < race + 0.06f) Put(o, FxShape.Glint, FxRamp.Bolt, Odd(Math.Min(21, W * 1.3f)), 1, dx * head, dy * head);
            }
            // Arcs snapping off its sides.
            int flick = (int)(t * 25);
            for (int i = 0; i < 6; i++)
            {
                float along = (i + 0.5f) / 6 * L;
                if (along > head || !In(t, 0.03f + 0.02f * i, 0.32f) || ((flick + i) & 1) == 0) continue;
                float side = (i & 1) == 0 ? 1 : -1, reach = W * (0.6f + 0.5f * Rnd(c.Seed + flick, i, 72));
                float x = dx * along, y = dy * along;
                Arc(o, t, x, y, x + nx * side * reach + dx * 6, y + ny * side * reach + dy * 6, c.Seed + i * 3, false, body);
            }
            // A burst where it stops.
            if (In(t, race, race + 0.3f))
            {
                float u = (t - race) / 0.3f;
                if (u < 0.4f) FxLayers.Light(o, FxRamp.Bolt, Math.Max(20, W * 2.6f), 1 - u / 0.4f, dx * L, dy * L, 3);
                FxLayers.Shock(o, FxRamp.Bolt, t, race, Math.Clamp(W, 10, 20), dx * L, dy * L, 0.26f, true);
            }
            FxLayers.Embers(o, FxRamp.Bolt, 6, t, race, Math.Clamp(W, 10, 18), c.Seed, dx * L, dy * L);
            // Souls drifting up off where it ran.
            for (int i = 0; i < 5; i++)
            {
                float p = (t - 0.18f - 0.04f * i) / 0.5f;
                if (p < 0 || p >= 1) continue;
                float along = (i + 0.3f + 0.4f * Rnd(c.Seed, i, 73)) / 5 * L;
                Put(o, FxShape.Wisp, FxRamp.Spirit, 4, (int)(t * 10) + i, dx * along + 3 * Sin(p * 500 + i * 60), dy * along + 18 * p, 0.85f * (1 - p * p), 90);
            }
            return life;
        }

        // The claws of its spectral hand raked through a wedge: three pale streaks swept across it one after
        // another, lightning crackling along their tracks, sparks flung off the far side. Size the reach, Width the
        // wedge in degrees, Dir its middle.
        static float Talons(in BossContext c, float t, List<FxStamp> o)
        {
            const float sweep = 0.12f;
            float R = c.Size, wide = Math.Clamp(c.Width, 20, 300), half = wide / 2;
            for (int k = 0; k < 3; k++)
            {
                float own = t - 0.025f * k, swept = U(own, 0, sweep);
                if (own < 0 || own >= 0.32f) continue;
                float fade = own < 0.16f ? 1 : (0.32f - own) / 0.16f, radius = R * (0.62f + 0.17f * k);
                int span = 10 * Math.Max(2, (int)Math.Round(wide * (0.3f + 0.7f * swept) / 10));
                float head = c.Dir - half + wide * swept;
                Put(o, FxShape.Smear, FxRamp.Spirit, SkillFxArt.Even(radius), span, 0, 0, fade * (k == 1 ? 1 : 0.8f), head - span / 2f);
                // Lightning along the track behind the claw.
                if (own > 0.03f && ((int)(t * 25) + k) % 2 == 0)
                {
                    float a0 = head - span * 0.9f, a1 = head - span * 0.4f;
                    Arc(o, t, Cos(a0) * radius, Sin(a0) * radius * 0.95f, Cos(a1) * radius, Sin(a1) * radius * 0.95f, c.Seed + k, false, fade);
                }
            }
            // Sparks flung on past the wedge's far edge and a soul or two shaken out.
            for (int i = 0; i < 7; i++)
            {
                float turn = (i + 0.5f) / 7, p = (t - sweep * turn) / (0.3f + 0.12f * Rnd(c.Seed, i, 74));
                if (p < 0 || p >= 1) continue;
                float dir = c.Dir - half + wide * turn, far = R * (0.95f + 0.35f * Out(p));
                Put(o, FxShape.Ember, FxRamp.Bolt, 3, 0, Cos(dir) * far, Sin(dir) * far, 1 - p * p, dir);
            }
            if (t < 0.2f) FxLayers.Light(o, FxRamp.Spirit, R * 1.2f, 0.6f * (1 - t / 0.2f), Cos(c.Dir) * R * 0.6f, Sin(c.Dir) * R * 0.6f, 2);
            Souls(o, t, 0.1f, 2, R * 0.6f, c.Seed, Cos(c.Dir) * R * 0.7f, Sin(c.Dir) * R * 0.7f);
            return 0.6f;
        }

        // Lightning running round a ring's band from Dir both ways, linking points round its middle: the band lit, a
        // star at each point, souls rising off it. Size the band's outer radius, Inner its inner. Plain: the smaller
        // ripple of souls (fewer points, no flash).
        static float ArcRing(in BossContext c, float t, List<FxStamp> o)
        {
            const float run = 0.18f, life = 0.62f;
            float outer = c.Size, inner = c.Inner, mid = inner > 0 ? (inner + outer) / 2 : outer * 0.6f;
            int points = Math.Clamp((int)(mid * 6.28f / 26), 8, 14);
            float left = U(life - t, 0, 0.25f);
            // The band's edges flash and run out.
            if (!c.Plain && t < 0.1f) FxLayers.Light(o, FxRamp.Bolt, outer * 1.4f, 1 - t / 0.1f, 0, 0, 3);
            FxLayers.Shock(o, FxRamp.Bolt, t, 0, outer, 0, 0, 0.3f, true);
            if (inner > 0 && t < 0.4f) Put(o, FxShape.Edge, FxRamp.Spirit, SkillFxArt.Even(2 * inner), 1, 0, 0, 0.8f * (1 - t / 0.4f), 0, true);
            for (int i = 0; i < points; i++)
            {
                // How far round from where it started (both ways), so the lightning runs round to meet behind.
                int step = Math.Min(i, points - i);
                float lit = (t - run * step / (points / 2f));
                if (lit < 0) continue;
                float a0 = c.Dir + i * 360f / points, a1 = c.Dir + (i + 1) * 360f / points;
                float r0 = mid + (outer - mid) * 0.5f * (Rnd(c.Seed + (int)(t * 25), i, 75) - 0.5f);
                float r1 = mid + (outer - mid) * 0.5f * (Rnd(c.Seed + (int)(t * 25), i + 1, 75) - 0.5f);
                float on = left * (lit < 0.3f ? 1 : 0.75f);
                if (((int)(t * 25) + i) % 4 != 0)
                    Arc(o, t, Cos(a0) * r0, Sin(a0) * r0 * 0.9f, Cos(a1) * r1, Sin(a1) * r1 * 0.9f, c.Seed + i, true, on, lit < (c.Plain ? 0.12f : 0.3f));
                if (lit < 0.1f) Put(o, FxShape.Glint, FxRamp.Bolt, c.Plain ? 7 : 9, 1, Cos(a0) * mid, Sin(a0) * mid * 0.9f, 1 - lit / 0.1f);
            }
            Souls(o, t, 0.1f, c.Plain ? 3 : 5, mid, c.Seed);
            return life;
        }
    }
}
