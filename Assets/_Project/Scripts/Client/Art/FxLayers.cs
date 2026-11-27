using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The layers every effect is built from since the effects rework (E3, Docs/EffectsRework.md 8), as functions of
    // time that add pieces to an effect's list, for skills (SkillFx), monsters (MonsterFx) and bosses alike: what is
    // left on the floor, light on the floor, the body, the edge and the particles. Blast puts them together into an
    // explosion. Nothing here keeps state. Positions are in pixels from the effect's origin, y up.
    static class FxLayers
    {
        // The most lights one effect may ask for at once; the view takes twelve a frame in all.
        public const int MostLights = 3;

        static float U(float t, float from, float to) => SkillFx.U(t, from, to);

        static float Rnd(int seed, int i, int salt) => SkillFx.Rnd(seed, i, salt);

        // A light for this moment on the light map (no shader, no picture): its radius in pixels, how strong (0 to
        // 1) and which of the element's colours (0 dark to 3 core; 1 is the body's colour).
        public static void Light(List<FxStamp> o, FxRamp ramp, float radius, float strength, float cx = 0, float cy = 0, int shade = 1)
        {
            if (strength <= 0.03f || radius < 1) return;
            o.Add(new FxStamp { Shape = FxShape.Light, Ramp = ramp, Size = (int)radius, Frame = shade, X = cx, Y = cy, Alpha = Math.Min(1, strength) });
        }

        // The ball alone: a white flash for 0.05 s, then lumps swelling for 0.2 s and hollowing into smoke.
        public static void Ball(List<FxStamp> o, FxRamp ramp, float t, float from, float R, int variant = 0, float cx = 0, float cy = 0)
        {
            float age = t - from;
            if (age < 0 || age >= SkillFxArt.BurstLife) return;
            SkillFx.Put(o, FxShape.Burst, ramp, SkillFxArt.Even(2 * R), SkillFxArt.BurstFrame(age, variant), cx, cy);
        }

        // What the ball leaves where it was: smoke that empties from the middle as it lifts.
        public static void Pall(List<FxStamp> o, float t, float from, float R, float cx = 0, float cy = 0, float life = 0.42f)
        {
            if (!SkillFx.In(t, from, from + life)) return;
            float u = (t - from) / life;
            SkillFx.Put(o, FxShape.Pall, FxRamp.Smoke, SkillFxArt.Even(R * (1.5f + 0.3f * u)), Math.Min(5, (int)(u * 6)), cx, cy + 2 + 6 * u, 0.85f * (1 - 0.5f * u));
        }

        // Smoke rising after: puffs born one after another over the place, swelling as they lift and thin.
        public static void Plumes(List<FxStamp> o, int count, float t, float from, float R, int seed, float cx = 0, float cy = 0, float life = 0.7f)
        {
            for (int i = 0; i < count; i++)
            {
                float p = (t - from - i * 0.05f) / life;
                if (p < 0 || p >= 1) continue;
                float x = cx + (Rnd(seed, i, 41) - 0.5f) * R * 1.1f, y = cy + (Rnd(seed, i, 42) - 0.5f) * R * 0.6f + p * (12 + R * 0.25f);
                SkillFx.Put(o, FxShape.Plume, FxRamp.Smoke, SkillFxArt.Even(2 * (R * 0.16f + 3 + 3 * p)), Math.Min(5, (int)(p * 6)), x, y, 0.9f * (1 - 0.5f * p));
            }
        }

        // The shock wave: a ring that runs out past the blast's reach, fast then slow, and breaks up.
        public static void Shock(List<FxStamp> o, FxRamp ramp, float t, float from, float R, float cx = 0, float cy = 0, float life = 0.32f, bool ground = false)
        {
            if (!SkillFx.In(t, from, from + life)) return;
            float u = (t - from) / life;
            float diameter = 2 * R * (0.45f + 0.85f * SkillFx.Out(U(u, 0, 0.8f)));
            SkillFx.Put(o, FxShape.Shock, ramp, SkillFxArt.Even(diameter), u < 0.36f ? 0 : u < 0.6f ? 1 : u < 0.82f ? 2 : 3, cx, cy, 1 - 0.6f * u, 0, ground);
        }

        // Sparks with tails thrown out all round, slowing as they go, their tails shortening as they cool.
        public static void Embers(List<FxStamp> o, FxRamp ramp, int count, float t, float from, float R, int seed, float cx = 0, float cy = 0,
            float cone = 360, float toward = 0, float life = 0.36f)
        {
            float age = t - from;
            if (age < 0) return;
            for (int i = 0; i < count; i++)
            {
                float own = life + 0.2f * Rnd(seed, i, 43);
                if (age >= own) continue;
                float p = age / own;
                float angle = toward + ((i + Rnd(seed, i, 44)) / count - 0.5f) * cone;
                float far = R * (0.3f + (0.95f + 0.5f * Rnd(seed, i, 45)) * SkillFx.Out(p));
                SkillFx.Put(o, FxShape.Ember, ramp, 2 + (int)Math.Round(4 * (1 - p)), p < 0.5f ? 0 : 1, cx + SkillFx.Cos(angle) * far, cy + SkillFx.Sin(angle) * far * 0.9f,
                    Math.Min(1, 2 - 2 * p), angle);
            }
        }

        // Rocks thrown up and out, each over its shadow on the floor: two pieces a rock.
        public static void Rubble(List<FxStamp> o, FxRamp ramp, int count, float t, float from, float R, int seed, float cx = 0, float cy = 0)
        {
            float age = t - from;
            if (age < 0) return;
            for (int i = 0; i < count; i++)
            {
                float own = 0.5f + 0.25f * Rnd(seed, i, 46);
                if (age >= own) continue;
                float p = age / own, angle = (i + 0.5f) / count * 360;
                float far = R * (0.4f + 0.9f * p), up = 4 * (10 + 10 * Rnd(seed, i, 47)) * p * (1 - p);
                float x = cx + SkillFx.Cos(angle) * far, y = cy + SkillFx.Sin(angle) * far * 0.7f;
                SkillFx.Put(o, FxShape.Shadow, FxRamp.Smoke, 4, 0, x, y - 1, 0.7f, 0, true);
                SkillFx.Put(o, FxShape.Chunk, ramp, 3 + (i & 1), 4 + (i & 3), x, y + up);
            }
        }

        // Soot on the floor from a moment after the blast until it fades over the last 0.4 s.
        public static void Scorch(List<FxStamp> o, float t, float from, float until, float R, int seed, float cx = 0, float cy = 0)
        {
            if (!SkillFx.In(t, from, until)) return;
            SkillFx.Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(R * 1.6f), 4 + (seed & 3), cx, cy, U(until - t, 0, 0.4f), 0, true);
        }

        // The floor splits from the middle: glowing, then dull, then cold and fading.
        public static void Splits(List<FxStamp> o, FxRamp ramp, int count, float t, float from, float until, float R, int seed, float cx = 0, float cy = 0)
        {
            if (!SkillFx.In(t, from, until)) return;
            float age = t - from;
            int cooled = age < 0.3f ? 0 : age < 0.7f ? 1 : 2;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.4f * Rnd(seed, i, 48)) / count * 360;
                float reach = R * (0.55f + 0.4f * Rnd(seed, i, 49)) * SkillFx.Out(U(age, 0, 0.12f));
                int length = SkillFxArt.Step4(reach);
                SkillFx.Put(o, FxShape.Crack, ramp, length, SkillFxArt.SplitFrame(i + seed, cooled), cx + SkillFx.Cos(angle) * length / 2, cy + SkillFx.Sin(angle) * length / 2,
                    0.9f * U(until - t, 0, 0.4f), angle, true);
            }
        }

        // An explosion in the rework's order (Docs/EffectsRework.md 3 and 8): a white flash for 0.05 s, a ball of
        // pale core, yellow, orange and red swelling for 0.2 s and hollowing into smoke, the shock wave, the edge of
        // what it hits, sparks that slow, rocks (the large ones), smoke rising and soot left behind, with up to three
        // lights on the floor. R is the blast's real reach in pixels. Returns when it is over (linger: how long the
        // soot stays). Count at most: 3 + embers + 2 * rubble + plumes + 1 pieces at once.
        public static float Blast(List<FxStamp> o, float t, float R, FxRamp ramp, int seed, int embers = 12, int rubble = 0, bool smoke = true, float linger = 1.1f,
            float cx = 0, float cy = 0)
        {
            R = Math.Max(8, R);
            Scorch(o, t, 0.03f, linger, R, seed, cx, cy);
            // Light on the floor: a pale flash, the fire's own colour while it burns, a dim glow from the embers after.
            if (t < 0.08f) Light(o, ramp, R * 2.8f, 1, cx, cy, 3);
            if (t < 0.45f) Light(o, ramp, R * 2.1f, 0.9f * (1 - t / 0.45f), cx, cy, 1);
            if (SkillFx.In(t, 0.2f, 0.8f)) Light(o, ramp, R * 1.3f, 0.35f * (1 - U(t, 0.5f, 0.8f)), cx, cy, 0);
            Ball(o, ramp, t, 0, R, seed, cx, cy);
            if (smoke) Pall(o, t, SkillFxArt.BurstLife - 0.04f, R, cx, cy);
            Shock(o, ramp, t, 0, R, cx, cy);
            // The edge of what it hits, crisp for a moment (rule 5).
            if (t < 0.14f) SkillFx.Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * R), 1, cx, cy, 0.9f);
            Embers(o, ramp, embers, t, 0, R, seed, cx, cy);
            Rubble(o, FxRamp.Dust, rubble, t, 0.06f, R, seed + 1, cx, cy);
            if (smoke) Plumes(o, 5, t, 0.16f, R, seed, cx, cy);
            return Math.Max(linger, smoke ? 1.06f : 0.6f);
        }
    }
}
