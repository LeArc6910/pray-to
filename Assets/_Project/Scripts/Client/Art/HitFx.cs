using System;
using System.Collections.Generic;

namespace Game.Client
{
    // What a blow is made of where it lands on a body (E7, Docs/EffectsRework.md 12).
    enum HitElement : byte
    {
        // Steel: a star and sparks.
        Plain,
        // Sparks and small tongues of flame.
        Fire,
        // Splinters of a crystal breaking.
        Cold,
        // A few short bent lines, drawn anew every 0.04 s.
        Bolt,
        // Drops thrown up that fall.
        Poison,
        // Stout shards and white glints.
        Crystal,
        // Grit and a puff of dust.
        Sand,
        // Bits of rock and a puff of dust.
        Stone,
        // Drops and a small ring.
        Water,
        // Dark lumps drifting off and points of starlight.
        Void,
    }

    // The effect on a body that was struck, as a function of time that adds pieces to a list (the same pieces as
    // every other effect, FxStamp). It is the effect seen most often, so it is light: six to twelve pieces, 0.2 to
    // 0.4 s, thrown to the far side of where the blow came from, shrinking as they go. Nothing is left on the floor;
    // only a strong blow (a crit, a finisher) lights it, once and briefly. Nothing here keeps state.
    static class HitFx
    {
        // The most pieces one blow lays at once, and how long the longest lasts.
        public const int Most = 12;
        public const float Longest = 0.4f;
        public const int Elements = (int)HitElement.Void + 1;

        // An element's own colours when the striker brings none.
        public static FxRamp RampOf(HitElement element) => element switch
        {
            HitElement.Fire => FxRamp.Fire,
            HitElement.Cold => FxRamp.Cold,
            HitElement.Bolt => FxRamp.Bolt,
            HitElement.Poison => FxRamp.Heal,
            HitElement.Crystal => FxRamp.Crystal,
            HitElement.Sand => FxRamp.Sand,
            HitElement.Stone => FxRamp.Dust,
            HitElement.Water => FxRamp.Spirit,
            HitElement.Void => FxRamp.Violet,
            _ => FxRamp.Steel,
        };

        public static float Life(HitElement element, bool strong) => element switch
        {
            HitElement.Plain => strong ? 0.3f : 0.22f,
            HitElement.Bolt => strong ? 0.26f : 0.2f,
            HitElement.Poison => strong ? Longest : 0.34f,
            _ => strong ? 0.38f : 0.3f,
        };

        // The pieces at age t. dir: the way the blow travels, in degrees (pieces fly on that way). ramp: the element's
        // colours. star: the colours of the star where it landed (white, gold for a crit, red on oneself), or null for
        // none (a weapon's added element lands on the star of the blow itself). strong: a crit or a finisher. thin:
        // many are landing at once, so half the pieces. False once it is over.
        public static bool Stamps(List<FxStamp> o, HitElement element, FxRamp ramp, FxRamp? star, float dir, bool strong, bool thin, int seed, float t)
        {
            float life = Life(element, strong);
            if (t < 0 || t >= life) return false;
            float p = t / life;
            // How far the pieces are thrown, in pixels.
            float reach = strong ? 17 : 11;
            if (strong && t < 0.12f) FxLayers.Light(o, star ?? ramp, 30, 0.55f * (1 - t / 0.12f), 0, 0, 2);
            switch (element)
            {
                case HitElement.Fire:
                    Sparks(o, ramp, N(strong ? 7 : 4, thin), t, life, reach, dir, strong ? 130 : 100, seed);
                    for (int i = 0, n = N(strong ? 3 : 2, thin); i < n; i++)
                    {
                        // Small tongues along the way, lifting as they burn down.
                        float far = reach * (0.3f + 0.35f * i) * SkillFx.Out(p), turn = dir + (Rnd(seed, i, 71) - 0.5f) * 50;
                        int tall = p < 0.35f ? 7 : p < 0.7f ? 6 : 4;
                        SkillFx.Put(o, FxShape.Flame, ramp, tall, ((int)(t / 0.06f) + i) & 3, SkillFx.Cos(turn) * far, SkillFx.Sin(turn) * far * 0.9f + 5 * p, Fade(p));
                    }
                    break;
                case HitElement.Cold:
                case HitElement.Crystal:
                    bool gem = element == HitElement.Crystal;
                    for (int i = 0, n = N(strong ? (gem ? 7 : 8) : gem ? 4 : 5, thin); i < n; i++)
                    {
                        // Splinters turning as they fly and fall.
                        float angle = Spread(dir, i, n, strong ? 140 : 110, seed);
                        float far = reach * (0.35f + (0.8f + 0.5f * Rnd(seed, i, 72)) * SkillFx.Out(p));
                        int length = p < 0.3f ? 6 : p < 0.6f ? 5 : p < 0.85f ? 4 : 3;
                        if ((i & 1) == 1) length--;
                        SkillFx.Put(o, FxShape.Shard, ramp, length, gem ? 2 : 0, SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f - 7 * p * p, Fade(p),
                            angle + (Rnd(seed, i, 73) - 0.5f) * 300 * p);
                    }
                    for (int i = 0, n = N(strong ? (gem ? 3 : 2) : gem ? 2 : 1, thin); i < n; i++)
                    {
                        // Glints that blink where it broke.
                        if ((((int)(t / 0.05f) + i) & 1) == 1 && p > 0.2f) continue;
                        float angle = dir + (Rnd(seed, i, 74) - 0.5f) * 160, far = reach * (0.3f + 0.5f * Rnd(seed, i, 75));
                        SkillFx.Put(o, FxShape.Glint, gem ? FxRamp.Steel : ramp, p < 0.5f ? 5 : 3, 0, SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f, Fade(p));
                    }
                    break;
                case HitElement.Bolt:
                    int redrawn = (int)(t / 0.04f);
                    for (int i = 0, n = N(strong ? 5 : 3, thin); i < n; i++)
                    {
                        // Short bent lines from where it struck, new ones every 0.04 s, shorter as it dies.
                        float angle = Spread(dir, i, n, strong ? 200 : 140, seed + redrawn * 13);
                        int length = (p < 0.4f ? 12 : p < 0.75f ? 10 : 8) + (strong && i == 0 ? 4 : 0);
                        SkillFx.Put(o, FxShape.Bolt, ramp, length, (seed + i * 3 + redrawn) % 6 + (strong && i == 0 ? 8 : 0), SkillFx.Cos(angle) * length * 0.5f,
                            SkillFx.Sin(angle) * length * 0.5f, Fade(p), angle);
                    }
                    for (int i = 0, n = N(strong ? 5 : 3, thin); i < n; i++)
                    {
                        float angle = Spread(dir, i, n, 220, seed + 5), far = reach * (0.5f + 0.7f * SkillFx.Out(p)) * (0.7f + 0.5f * Rnd(seed, i, 76));
                        SkillFx.Put(o, FxShape.Dot, ramp, p < 0.5f ? 2 : 1, p < 0.5f ? 3 : 2, SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f, Fade(p));
                    }
                    break;
                case HitElement.Poison:
                case HitElement.Water:
                    for (int i = 0, n = N(strong ? (element == HitElement.Water ? 8 : 10) : element == HitElement.Water ? 5 : 6, thin); i < n; i++)
                    {
                        // Drops thrown up and over, falling as they shrink.
                        float angle = Spread(dir, i, n, strong ? 150 : 120, seed);
                        float far = reach * (0.3f + (0.7f + 0.5f * Rnd(seed, i, 77)) * p);
                        float up = (9 + 7 * Rnd(seed, i, 78)) * 4 * p * (1 - p) - 6 * p * p;
                        float x = SkillFx.Cos(angle) * far, y = SkillFx.Sin(angle) * far * 0.8f + up;
                        if (p < 0.75f) SkillFx.Put(o, FxShape.Lump, ramp, p < 0.4f ? 4 : 3, ((i & 1) + 1) * 4 + (i & 3), x, y, 1);
                        else SkillFx.Put(o, FxShape.Dot, ramp, 2, 1 + (i & 1), x, y, Fade(p));
                    }
                    if (element == HitElement.Water && !thin) SkillFx.Put(o, FxShape.Edge, ramp, SkillFxArt.Even(6 + 10 * SkillFx.Out(p)), 1, 0, 0, 0.9f * (1 - p));
                    break;
                case HitElement.Sand:
                case HitElement.Stone:
                    bool rock = element == HitElement.Stone;
                    for (int i = 0, n = N(strong ? (rock ? 6 : 9) : rock ? 3 : 6, thin); i < n; i++)
                    {
                        // Grit (or bits of rock) thrown and falling fast.
                        float angle = Spread(dir, i, n, strong ? 150 : 120, seed);
                        float far = reach * (0.3f + (0.8f + 0.6f * Rnd(seed, i, 79)) * p);
                        float up = (6 + 6 * Rnd(seed, i, 80)) * 4 * p * (1 - p) - 8 * p * p;
                        float x = SkillFx.Cos(angle) * far, y = SkillFx.Sin(angle) * far * 0.8f + up;
                        if (rock) SkillFx.Put(o, FxShape.Chunk, ramp, p < 0.6f ? 4 : 3, 4 + (i & 3), x, y, Fade(p));
                        else SkillFx.Put(o, FxShape.Dot, ramp, p < 0.5f && (i & 1) == 0 ? 2 : 1, 1 + i % 3, x, y, Fade(p));
                    }
                    for (int i = 0, n = N(strong ? 2 : 1, thin); i < n; i++)
                    {
                        float far = reach * (0.35f + 0.3f * i) * SkillFx.Out(p), turn = dir + (i == 0 ? -20 : 25);
                        SkillFx.Put(o, FxShape.Smoke, FxRamp.Dust, 8 + 2 * (int)(p * 2), Math.Min(3, (int)(p * 4)), SkillFx.Cos(turn) * far, SkillFx.Sin(turn) * far * 0.9f + 3 * p, 0.8f * (1 - 0.6f * p));
                    }
                    break;
                case HitElement.Void:
                    for (int i = 0, n = N(strong ? 6 : 4, thin); i < n; i++)
                    {
                        // Lumps that drift off, thin and darken. A shade lighter than they were (2026-11-27: at the two
                        // darkest shades they were lost on the floor in the grey sheet; now the middle of the ramp first).
                        float angle = Spread(dir, i, n, strong ? 170 : 130, seed);
                        float far = reach * (0.3f + 0.7f * SkillFx.Out(p)) * (0.7f + 0.4f * Rnd(seed, i, 81));
                        SkillFx.Put(o, FxShape.Lump, ramp, p < 0.4f ? 5 : p < 0.75f ? 4 : 3, ((p < 0.5f ? 1 : 2) + (i & 1)) * 4 + (i & 3), SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f + 4 * p,
                            0.9f * (1 - 0.6f * p));
                    }
                    for (int i = 0, n = N(strong ? 3 : 2, thin); i < n; i++)
                    {
                        if ((((int)(t / 0.05f) + i) & 1) == 1) continue;
                        float angle = dir + (Rnd(seed, i, 82) - 0.5f) * 200, far = reach * (0.4f + 0.6f * Rnd(seed, i, 83));
                        SkillFx.Put(o, FxShape.Glint, ramp, p < 0.5f ? 5 : 3, 0, SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f, Fade(p));
                    }
                    break;
                default:
                    Sparks(o, star ?? ramp, N(strong ? 8 : 5, thin), t, life, reach, dir, strong ? 120 : 80, seed);
                    break;
            }
            // The star where it landed, over the pieces: with ticks for a moment, then a plain one, then gone.
            if (star.HasValue && t < 0.17f)
            {
                int span = strong ? 13 : 9;
                SkillFx.Put(o, FxShape.Glint, star.Value, t < 0.06f ? span : t < 0.12f ? span - 2 : span - 4, t < 0.06f ? 2 : t < 0.12f ? 1 : 0, 0, 0);
            }
            return true;
        }

        static int N(int count, bool thin) => thin ? (count + 1) / 2 : count;

        static float Rnd(int seed, int i, int salt) => SkillFx.Rnd(seed, i, salt);

        static float Fade(float p) => Math.Min(1, 2.2f - 2.2f * p);

        // The i-th of n ways inside a cone round dir, each a little off its even place.
        static float Spread(float dir, int i, int n, float cone, int seed) => dir + ((i + 0.2f + 0.6f * Rnd(seed, i, 84)) / n - 0.5f) * cone;

        // Sparks with tails thrown on the blow's way, fast then slow, their tails shortening.
        static void Sparks(List<FxStamp> o, FxRamp ramp, int count, float t, float life, float reach, float dir, float cone, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float own = life * (0.7f + 0.3f * Rnd(seed, i, 85));
                if (t >= own) continue;
                float p = t / own, angle = Spread(dir, i, count, cone, seed);
                float far = reach * (0.3f + (0.8f + 0.5f * Rnd(seed, i, 86)) * SkillFx.Out(p));
                SkillFx.Put(o, FxShape.Ember, ramp, 2 + (int)Math.Round(4 * (1 - p)), p < 0.5f ? 0 : 1, SkillFx.Cos(angle) * far, SkillFx.Sin(angle) * far * 0.9f, Fade(p), angle);
            }
        }
    }
}
