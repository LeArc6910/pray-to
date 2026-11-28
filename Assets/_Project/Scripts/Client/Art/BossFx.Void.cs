using System;
using System.Collections.Generic;

namespace Game.Client
{
    // ---- The void lord's own effects (2026-11-28, Docs/Bosses.md 공허의 군주): the void (violet) and the cold (frost) it
    // is made of. Claws of the void raking through a wedge and closing, a star come down out of the dark onto a circle and
    // bursting into ice, a rift torn along a line with ice left standing in it (or a lance of ice thrown down it), the
    // floor drawn into a point and thrown back out, and a horizon of turning rings with stars running round its band. ----
    static partial class BossEffects
    {
        // A mote of the void or of frost in shade (0 core to 4 darkest), wavering with the time.
        static void Mote(List<FxStamp> o, FxRamp ramp, float size, int shade, float t, int i, float x, float y, float alpha = 1, bool low = false)
        {
            int frame = 4 * shade + (((int)(t * 14) + i) & 3);
            if (low) PutLow(o, FxShape.Lump, ramp, Even(size), frame, x, y, alpha);
            else Put(o, FxShape.Lump, ramp, Even(size), frame, x, y, alpha);
        }

        static float VoidClaw(in BossContext c, float t, List<FxStamp> o)
        {
            const float reach = 0.12f, close = 0.3f, life = 0.7f;
            float R = c.Size, wide = Math.Clamp(c.Width, 20, 300), half = wide / 2;
            // Five fingers of the void thrown out across the wedge, then closing on its middle like a hand.
            for (int k = 0; k < 5; k++)
            {
                float spread = -half + wide * (k + 0.5f) / 5;
                float shut = U(t, reach, close);
                float dir = c.Dir + spread * (1 - 0.8f * shut * shut);
                float far = R * (t < reach ? Out(t / reach) : 1 - 0.25f * shut);
                float fade = t < close ? 1 : Math.Max(0, 1 - (t - close) / 0.25f);
                if (fade <= 0) continue;
                // Each finger in pieces from the root out, darker at the root and pale at its claw.
                int pieces = Math.Clamp((int)(far / 12), 2, 6);
                for (int i = 1; i <= pieces; i++)
                {
                    float along = far * i / pieces, bend = 4 * Sin(i * 50 + k * 70 + t * 600) * (1 - shut);
                    float x = Cos(dir) * along - Sin(dir) * bend, y = Sin(dir) * along + Cos(dir) * bend;
                    Mote(o, FxRamp.Violet, i == pieces ? 7 : 9 - i, i == pieces ? 1 : 3, t, i + k, x, y, fade * 0.9f, true);
                }
                Put(o, FxShape.Streak, FxRamp.Violet, Math.Max(6, (int)(far * 0.5f)), 2, Cos(dir) * far * 0.75f, Sin(dir) * far * 0.75f, fade, dir);
                Put(o, FxShape.Shard, FxRamp.Cold, 7, 1, Cos(dir) * far, Sin(dir) * far, fade, dir);
            }
            // Where the hand shuts: a pop of frost, ice flung on along its middle.
            if (t >= close)
            {
                float p = U(t, close, life);
                float mid = R * 0.72f, mx = Cos(c.Dir) * mid, my = Sin(c.Dir) * mid;
                Put(o, FxShape.Glint, FxRamp.Cold, Odd(16 * (1 - p)), 1, mx, my, 1 - p);
                Put(o, FxShape.Edge, FxRamp.Violet, SkillFxArt.Even(10 + 30 * Out(p)), 1, mx, my, 1 - p, 0, true);
                for (int i = 0; i < 7; i++)
                {
                    float dir = c.Dir + (Rnd(c.Seed, i, 61) - 0.5f) * Math.Min(90, wide), go = 10 + 26 * Out(p) * (0.6f + 0.6f * Rnd(c.Seed, i, 62));
                    Put(o, FxShape.Shard, FxRamp.Cold, 4 + (i & 1), 2, mx + Cos(dir) * go, my + Sin(dir) * go + 10 * p * (1 - p), 1 - p * p, dir);
                }
            }
            if (t < 0.35f) FxLayers.Light(o, FxRamp.Violet, R * 1.4f, 0.5f * (1 - t / 0.35f), Cos(c.Dir) * R * 0.5f, Sin(c.Dir) * R * 0.5f, 2);
            return life;
        }

        static float Comet(in BossContext c, float t, List<FxStamp> o)
        {
            const float fall = 0.14f, life = 0.8f;
            float R = Math.Max(10, c.Size), high = (c.Big ? 90 : 70);
            if (t < fall)
            {
                // The star coming down out of the dark at a slant, a tail of frost behind it.
                float p = t / fall, h = high * (1 - p * p);
                float x = -h * 0.35f, y = h;
                Put(o, FxShape.Streak, FxRamp.Cold, Math.Clamp((int)(26 + 20 * p), 8, 48), 3, x + 10, y + 26, 1, -70);
                Put(o, FxShape.Glint, FxRamp.Cold, 13, 1, x, y, 1);
                Mote(o, FxRamp.Violet, 8, 1, t, 0, x, y);
                return life;
            }
            float u = U(t, fall, life);
            // It strikes: a white star, a ring of frost thrown out over the floor, the void shell after it.
            Put(o, FxShape.Glint, FxRamp.Cold, Odd(R * 1.6f * (1 - u)), 1, 0, 2, 1 - u);
            for (int ring = 0; ring < 2; ring++)
            {
                float p = (t - fall - 0.08f * ring) / 0.45f;
                if (p < 0 || p >= 1) continue;
                Put(o, FxShape.Shock, ring == 0 ? FxRamp.Cold : FxRamp.Violet, SkillFxArt.Even(2 * R * (0.4f + 0.9f * Out(p))), (int)(p * 4), 0, 0, 1 - p, 0, true);
            }
            // Ice standing up round where it struck, then breaking.
            int spikes = c.Big ? 8 : 6;
            for (int i = 0; i < spikes; i++)
            {
                float dir = i * 360f / spikes + 20 * Rnd(c.Seed, i, 63), far = R * (0.45f + 0.35f * Rnd(c.Seed, i, 64));
                float p = U(t, fall, fall + 0.5f);
                float x = Cos(dir) * far, y = Sin(dir) * far * 0.8f;
                if (p < 0.6f) Put(o, FxShape.Shard, FxRamp.Cold, 6 + (i & 1) * 2, 2, x, y + 3, 1, 90 + (dir > 180 ? -15 : 15));
                else
                {
                    float q = (p - 0.6f) / 0.4f;
                    Put(o, FxShape.Dot, FxRamp.Cold, 2, 2, x + Cos(dir) * 6 * q, y + 5 + 8 * q * (1 - q), 1 - q);
                }
            }
            // Stars and frost motes thrown up and drifting back.
            for (int i = 0; i < 6; i++)
            {
                float p = (t - fall) / (0.45f + 0.2f * Rnd(c.Seed, i, 65));
                if (p >= 1) continue;
                float dir = i * 60 + 30, go = R * (0.5f + 0.6f * Out(p));
                if ((i & 1) == 0) Put(o, FxShape.Glint, FxRamp.Violet, 5, 0, Cos(dir) * go, Sin(dir) * go * 0.6f + 18 * p * (1 - p), 1 - p);
                else Mote(o, FxRamp.Cold, 4, 2, t, i, Cos(dir) * go, Sin(dir) * go * 0.6f + 14 * p * (1 - p), 1 - p);
            }
            Put(o, FxShape.Scorch, FxRamp.Violet, SkillFxArt.Step4(R * 1.1f), 4 + (c.Seed & 3), 0, 0, 0.5f * (1 - u), 0, true);
            if (u < 0.5f) FxLayers.Light(o, FxRamp.Cold, R * 2.4f, 0.8f * (1 - u / 0.5f), 0, 0, 1);
            return life;
        }

        static float Rift(in BossContext c, float t, List<FxStamp> o)
        {
            float L = Math.Max(24, c.Size), W = Math.Max(8, c.Width), dx = Cos(c.Dir), dy = Sin(c.Dir);
            if (c.Big)
            {
                // A lance of ice thrown down the line, the void torn open behind it.
                const float fly = 0.16f;
                float p = Math.Min(1, t / fly), head = L * Out(p);
                if (t < fly + 0.05f) Put(o, FxShape.Spear, FxRamp.Cold, Math.Clamp((int)(W * 3), 20, 44), 0, dx * head, dy * head, 1, c.Dir);
                if (t < fly + 0.05f) Put(o, FxShape.Spear, FxRamp.Violet, Math.Clamp((int)(W * 3), 20, 44), 1, dx * (head - 12), dy * (head - 12), 0.6f, c.Dir);
                float left = 1 - U(t, fly, 0.7f);
                for (float d = 8; d < head; d += 14)
                {
                    float q = d / L;
                    Mote(o, FxRamp.Violet, W * (0.7f - 0.3f * q), 2 + ((int)(d / 14) & 1), t, (int)d, dx * d, dy * d, left * (0.5f + 0.5f * q), true);
                }
                if (t >= fly) Puff(new BossContext { Ramp = FxRamp.Cold, Seed = c.Seed }, t - fly, o, dx * L, dy * L, Math.Clamp(W, 10, 18), 1, false);
                return 0.8f;
            }
            // A rift torn along the line: thin, then wide and dark with a pale edge, narrowing; ice left standing in it.
            const float life = 0.5f;
            if (t < life)
            {
                float body = t < 0.05f ? 0.35f : t < 0.32f ? 1 : (life - t) / 0.18f;
                for (float d = 16; ; d += 32)
                {
                    float mid = L < 32 ? L / 2 : Math.Min(d, L - 16);
                    Put(o, FxShape.Beam, FxRamp.Violet, Even(W * body), (int)(t * 25) & 3, dx * mid, dy * mid, 1, c.Dir);
                    if (d >= L - 16) break;
                }
                Put(o, FxShape.Glint, FxRamp.Violet, Odd(W * 1.6f * body), 1, 0, 0);
            }
            // Ice standing up along it, then shattering.
            int each = Math.Clamp((int)(L / 22), 3, 9);
            for (int i = 0; i < each; i++)
            {
                float along = L * (i + 0.5f) / each, off = (Rnd(c.Seed, i, 66) - 0.5f) * W * 0.8f;
                float x = dx * along - dy * off, y = dy * along + dx * off, p = U(t, 0.05f + 0.02f * i, 0.75f);
                if (p <= 0) continue;
                if (p < 0.65f) Put(o, FxShape.Shard, FxRamp.Cold, 7 + (i & 1) * 2, 2, x, y + 3, 1, 90 + 20 * (Rnd(c.Seed, i, 67) - 0.5f));
                else
                {
                    float q = (p - 0.65f) / 0.35f;
                    for (int k = 0; k < 2; k++) Put(o, FxShape.Dot, FxRamp.Cold, 2, 2, x + (k * 2 - 1) * 5 * q, y + 6 + 6 * q * (1 - q), 1 - q);
                }
            }
            if (t < 0.4f) FxLayers.Light(o, FxRamp.Violet, Math.Min(L, 80), 0.6f * (1 - t / 0.4f), dx * L / 2, dy * L / 2, 2);
            return 0.8f;
        }

        static float Singularity(in BossContext c, float t, List<FxStamp> o)
        {
            const float draw = 0.28f;
            float R = Math.Max(12, c.Size), life = c.Big ? 1.0f : 0.85f;
            if (t < draw)
            {
                // Everything drawn in: rings closing on the middle, scraps and frost pulled after them, the middle black.
                float p = t / draw;
                for (int ring = 0; ring < 3; ring++)
                {
                    float q = SkillFx.Frac(p * 1.5f + ring / 3f);
                    Put(o, FxShape.Edge, ring == 1 ? FxRamp.Cold : FxRamp.Violet, SkillFxArt.Even(2 * R * (1.2f - q)), 1, 0, 0, 0.4f + 0.6f * q, 0, true);
                }
                int scraps = c.Big ? 14 : 10;
                for (int i = 0; i < scraps; i++)
                {
                    float q = SkillFx.Frac(p * 1.3f + Rnd(c.Seed, i, 68)), dir = i * 360f / scraps + 120 * q, far = R * 1.2f * (1 - q * q);
                    Put(o, FxShape.Ember, (i & 1) == 0 ? FxRamp.Violet : FxRamp.Cold, 3 + (i & 1), 0, Cos(dir) * far, Sin(dir) * far * 0.85f, 0.5f + 0.5f * q, dir + 180);
                }
                Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(R * (0.4f + 0.5f * p)), 4 + (c.Seed & 3), 0, 0, 0.7f, 0, true);
                Mote(o, FxRamp.Violet, 6 + 6 * p, 4, t, 0, 0, 2);
                return life;
            }
            // Thrown back out: a pale flash, a shock of frost over the floor, ice flung out, the dark left lying a moment.
            float u = U(t, draw, life);
            Put(o, FxShape.Glint, FxRamp.Cold, Odd(R * 1.2f * (1 - u)), 2, 0, 2, 1 - u);
            for (int ring = 0; ring < (c.Big ? 3 : 2); ring++)
            {
                float p = (t - draw - 0.07f * ring) / 0.4f;
                if (p < 0 || p >= 1) continue;
                Put(o, FxShape.Shock, ring == 1 ? FxRamp.Violet : FxRamp.Cold, SkillFxArt.Even(2 * R * (0.3f + 0.95f * Out(p))), (int)(p * 4), 0, 0, 1 - p, 0, true);
            }
            int shards = c.Big ? 12 : 8;
            for (int i = 0; i < shards; i++)
            {
                float p = (t - draw) / (0.35f + 0.15f * Rnd(c.Seed, i, 69));
                if (p >= 1) continue;
                float dir = i * 360f / shards + 15, go = R * (0.2f + 1.0f * Out(p));
                Put(o, FxShape.Shard, FxRamp.Cold, 5 + (i & 1), 2, Cos(dir) * go, Sin(dir) * go * 0.85f + 12 * p * (1 - p), 1 - p * p, dir);
            }
            Put(o, FxShape.Scorch, FxRamp.Violet, SkillFxArt.Step4(R * 0.9f), 4 + (c.Seed & 3), 0, 0, 0.6f * (1 - u), 0, true);
            if (u < 0.4f) FxLayers.Light(o, FxRamp.Cold, R * 2.6f, 0.9f * (1 - u / 0.4f), 0, 0, 1);
            return life;
        }

        static float Horizon(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.9f;
            float outer = Math.Max(12, c.Size), inner = c.Inner, mid = inner > 0 ? (inner + outer) / 2 : outer * 0.7f, left = U(life - t, 0, 0.3f);
            int turn = (int)(t * 10);
            // Broken rings turning opposite ways at the band's edges; the band itself darkened.
            Put(o, FxShape.Broken, FxRamp.Violet, SkillFxArt.Even(2 * outer), SkillFxArt.BrokenFrame(2, 6, turn), 0, 0, 0.9f * left, 0, true);
            Put(o, FxShape.Broken, FxRamp.Cold, SkillFxArt.Even(2 * (inner > 0 ? inner : outer * 0.45f)), SkillFxArt.BrokenFrame(1, 5, 7 - turn), 0, 0, 0.8f * left, 0, true);
            // Stars running round the band, frost motes between them.
            int stars = Math.Clamp((int)(mid / 6), 8, 14);
            for (int i = 0; i < stars; i++)
            {
                float angle = i * 360f / stars + 220 * t * ((i & 1) == 0 ? 1 : -0.6f);
                float far = mid + (outer - Math.Max(inner, outer * 0.45f)) * 0.3f * Sin(i * 97 + t * 300);
                float x = Cos(angle) * far, y = Sin(angle) * far * 0.9f, a = left * Math.Min(1, t * 8);
                if ((i % 3) == 0) Put(o, FxShape.Glint, FxRamp.Cold, 5 + 2 * ((i / 3) & 1), (i / 3) & 1, x, y, a);
                else Mote(o, (i & 1) == 0 ? FxRamp.Violet : FxRamp.Cold, 4 + (i & 1) * 2, 2, t, i, x, y, a * 0.85f, true);
            }
            // Frost thrown off the outside.
            for (int i = 0; i < 8; i++)
            {
                float p = (t - 0.04f * (i & 3)) / (0.4f + 0.15f * Rnd(c.Seed, i, 70));
                if (p < 0 || p >= 1) continue;
                float dir = i * 45 + 22, far = outer * (0.92f + 0.22f * Out(p));
                Put(o, FxShape.Shard, FxRamp.Cold, 4, 2, Cos(dir) * far, Sin(dir) * far * 0.9f + 10 * p * (1 - p), 1 - p * p, dir);
            }
            if (t < 0.4f) FxLayers.Light(o, FxRamp.Violet, outer * 2, 0.5f * (1 - t / 0.4f), 0, 0, 2);
            return life;
        }
    }
}
