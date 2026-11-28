using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The weapon arts' effects (Docs/Weapons.md 묶음 C, the mock-up out/weapon_arts.png): a program for each art's
    // beginning (ArtStart: a guard, a charge, an aim, a lifted hammer, a thrown boomerang, an orb's well) and for its
    // blow (Art). Laid out from the player's feet, or for a dagger's stab, a crossbow's line end and an orb's well
    // from where the host says. Context: Radius the reach in pixels, Dir the way, Length a line's or a rush's length.
    static partial class SkillFx
    {
        const string ArtPrefix = "art_";

        // The program for an art at a phase: "art_parry" for the blow, "art_parry_start", "art_parry_blocked".
        public static string ArtId(WeaponArtKind kind, ArtPhase phase) =>
            ArtPrefix + kind.ToString().ToLowerInvariant() + (phase == ArtPhase.Start ? "_start" : phase == ArtPhase.Blocked ? "_blocked" : "");

        public static bool IsArt(string skill) => skill != null && skill.StartsWith(ArtPrefix, StringComparison.Ordinal);

        static float ArtCast(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            float R = c.Radius, face = c.Dir * Deg, dx = (float)Math.Cos(c.Dir), dy = (float)Math.Sin(c.Dir);
            int seed = c.Seed;
            switch (skill)
            {
                // ---------------- sword ----------------
                case "art_parry_start":
                {
                    // A guard of steel before the body, pulsing while it holds.
                    const float life = 0.4f;
                    if (!In(t, 0, life)) return life;
                    float pulse = 0.65f + 0.35f * (float)Math.Sin(t * 40);
                    Put(o, FxShape.Smear, FxRamp.Steel, 13, 110, dx * 2, Body + dy * 2, pulse, face);
                    Put(o, FxShape.Smear, FxRamp.Steel, 9, 70, dx * 2, Body + dy * 2, 0.5f * pulse, face);
                    FxLayers.Light(o, FxRamp.Steel, 16, 0.35f, dx * 8, Body, 2);
                    return life;
                }
                case "art_parry_blocked":
                {
                    // The blow meets the guard: a gold star and sparks thrown back.
                    if (In(t, 0, 0.18f)) Put(o, FxShape.Glint, FxRamp.Gold, t < 0.08f ? 11 : 7, 0, dx * 10, Body + dy * 10);
                    FxLayers.Embers(o, FxRamp.Gold, 8, t, 0, 18, seed, dx * 10, Body + dy * 10, 160, face, 0.24f);
                    FxLayers.Shock(o, FxRamp.Gold, t, 0, 10, dx * 10, Body + dy * 10, 0.2f);
                    if (In(t, 0, 0.2f)) FxLayers.Light(o, FxRamp.Gold, 30, 0.8f * (1 - U(t, 0.05f, 0.2f)), dx * 10, Body, 3);
                    return 0.3f;
                }
                case "art_parry":
                {
                    // The answer: one broad steel sweep round the front at the true reach.
                    const float life = 0.36f;
                    if (In(t, 0, life))
                    {
                        float u = U(t, 0, life), alpha = u < 0.6f ? 1 : 1 - (u - 0.6f) / 0.4f;
                        int wide = 10 * (int)Math.Round((60 + 140 * Out(u)) / 10);
                        Put(o, FxShape.Smear, FxRamp.Steel, (int)R, wide, 0, Body, alpha, face - 100 + wide / 2f);
                        Put(o, FxShape.Smear, FxRamp.Steel, (int)(R * 0.6f), wide, 0, Body, alpha * 0.5f, face - 100 + wide / 2f);
                    }
                    Ring(o, FxRamp.Steel, t, 0, R, 1, 0.08f, 0.2f);
                    FxLayers.Embers(o, FxRamp.Steel, 6, t, 0.05f, 14, seed, dx * R * 0.8f, Body + dy * R * 0.8f, 120, face, 0.22f);
                    return 0.45f;
                }
                // ---------------- dagger ----------------
                case "art_vital":
                {
                    // At the one stabbed: a thin steel line in, a red-gold star, sparks of blood-red.
                    if (In(t, 0, 0.14f)) Put(o, FxShape.Streak, FxRamp.Steel, 16, 2, -dx * 10, Body - dy * 10, 1 - U(t, 0.06f, 0.14f), face);
                    if (In(t, 0.02f, 0.26f)) Put(o, FxShape.Glint, FxRamp.Gold, t < 0.1f ? 11 : 7, 0, 0, Body);
                    FxLayers.Embers(o, FxRamp.Danger, 7, t, 0.03f, 16, seed, 0, Body, 140, face, 0.26f);
                    FxLayers.Shock(o, FxRamp.Danger, t, 0.02f, 9, 0, Body, 0.2f);
                    if (In(t, 0, 0.2f)) FxLayers.Light(o, FxRamp.Danger, 22, 0.6f, 0, Body, 2);
                    return 0.4f;
                }
                // ---------------- greatsword ----------------
                case "art_charge_start":
                {
                    // Gathering, as long as it is held (the view ends it at the blow): lines drawn in, over and over,
                    // a gold glow that grows.
                    const float most = 1.6f;
                    float k = Math.Min(1, t / 1f);
                    float cycle = Frac(t / 0.3f) * 0.3f;
                    Gather(o, FxRamp.Gold, cycle, 0.3f, 18 + 6 * k, 6, seed + (int)(t / 0.3f), cy: Body);
                    if (k >= 1 && ((int)(t * 12) & 1) == 0) Put(o, FxShape.Glint, FxRamp.Gold, 7, 0, 0, Body + 12);
                    FxLayers.Light(o, FxRamp.Gold, 14 + 14 * k, 0.3f + 0.4f * k, 0, Body, 2);
                    return most;
                }
                case "art_charge":
                {
                    // A great cleave of fire-gold, wider than the sword's, the floor shaken under it.
                    const float life = 0.4f;
                    if (In(t, 0, life))
                    {
                        float u = U(t, 0, life), alpha = u < 0.55f ? 1 : 1 - (u - 0.55f) / 0.45f;
                        int wide = 10 * (int)Math.Round((60 + 140 * Out(u)) / 10);
                        Put(o, FxShape.Smear, FxRamp.Fire, (int)R, wide, 0, Body, alpha, face - 100 + wide / 2f);
                        Put(o, FxShape.Smear, FxRamp.Gold, (int)(R * 0.7f), wide, 0, Body, alpha * 0.7f, face - 100 + wide / 2f);
                    }
                    FxLayers.Shock(o, FxRamp.Gold, t, 0.05f, R / 1.3f, 0, 0, 0.3f, true);
                    Ring(o, FxRamp.Gold, t, 0.05f, R, 2, 0.1f, 0.26f);
                    FxLayers.Embers(o, FxRamp.Fire, 9, t, 0.08f, R * 0.7f, seed, dx * R * 0.6f, Body + dy * R * 0.6f, 160, face, 0.3f);
                    if (In(t, 0, 0.3f)) FxLayers.Light(o, FxRamp.Fire, R + 10, 0.7f * (1 - U(t, 0.1f, 0.3f)), dx * R * 0.5f, Body, 3);
                    return 0.6f;
                }
                // ---------------- spear ----------------
                case "art_dash":
                {
                    // From where the rush ended: streaks back along the way, the point's flash ahead, dust.
                    float L = c.Length, bx = -dx, by = -dy;
                    if (In(t, 0, 0.3f))
                    {
                        float u = t / 0.3f;
                        for (int i = 0; i < 7; i++)
                        {
                            float along = L * Rnd(seed, i, 11), aside = (Rnd(seed, i, 12) - 0.5f) * 2 * R;
                            Put(o, FxShape.Streak, FxRamp.Gold, 12 + 4 * (i % 3), 2, bx * along - by * aside, Body + by * along + bx * aside, 1 - u, face);
                        }
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float at = L * (k + 0.7f) / 3.7f, from = k * 0.05f;
                        if (In(t, 0, from + 0.2f)) Put(o, FxShape.Ghost, FxRamp.Steel, 1, 0, bx * at, by * at, 0.4f * (1 - U(t, from, from + 0.2f)));
                    }
                    if (In(t, 0, 0.16f)) Put(o, FxShape.Glint, FxRamp.Gold, t < 0.07f ? 9 : 5, 0, dx * 14, Body + dy * 14);
                    FxLayers.Embers(o, FxRamp.Gold, 6, t, 0, 14, seed, dx * 12, Body + dy * 12, 90, face, 0.22f);
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 5, t, 0.02f, 0.35f, 3, 12, seed, rise: 3);
                    return 0.45f;
                }
                // ---------------- hammer ----------------
                case "art_slam_start":
                {
                    // Lifted: the reach marked on the floor in pale amber (the player's own mark, never an enemy's
                    // red), filling as the blow comes; dust drawn in.
                    float time = c.Length > 0 ? c.Length : 0.35f;
                    if (!In(t, 0, time)) return time;
                    float u = t / time;
                    Put(o, FxShape.Edge, FxRamp.Gold, SkillFxArt.Even(2 * R), 1, 0, 0, MarkAlpha(u), 0, true);
                    Put(o, FxShape.Edge, FxRamp.Gold, SkillFxArt.Even(2 * R * Out(u)), 1, 0, 0, 0.3f, 0, true);
                    Gather(o, FxRamp.Dust, t, time, R * 0.6f, 6, seed, cy: 2);
                    return time;
                }
                case "art_slam":
                    // The blow is the warrior's ground slam at the hammer's reach.
                    return Cast("ground_slam", c, t + Lead, o) - Lead;
                // ---------------- bow ----------------
                case "art_backstep":
                {
                    // Dust kicked up where the jump began (Length behind the way it shoots), streaks of the leap.
                    float L = c.Length;
                    Puffs(o, FxRamp.Dust, 4, t, 0, 0.4f, 2, 8, seed, 5, 0.5f, dx * L, dy * L, 3);
                    if (In(t, 0, 0.22f))
                    {
                        float u = t / 0.22f;
                        for (int i = 0; i < 4; i++)
                        {
                            float along = L * (i + 0.5f) / 4, aside = (i - 1.5f) * 4;
                            Put(o, FxShape.Streak, FxRamp.Wind, 10, 1, dx * along - dy * aside, Body + dy * along + dx * aside, 1 - u, face + 180);
                        }
                    }
                    if (In(t, 0, 0.12f)) Put(o, FxShape.Glint, FxRamp.Wind, 7, 0, dx * 10, Body + dy * 10);
                    return 0.45f;
                }
                // ---------------- crossbow ----------------
                case "art_pierce_start":
                {
                    // Aiming: a thin red-amber line out the way it will go, a ring closing on the bolt's tip.
                    float time = 0.5f, L = c.Length;
                    if (!In(t, 0, time)) return time;
                    float u = t / time;
                    for (float along = 10; along < L; along += 10)
                        Put(o, FxShape.Dot, FxRamp.Danger, 1, 1, dx * along, Body + dy * along, 0.35f + 0.4f * u);
                    Put(o, FxShape.Edge, FxRamp.Danger, SkillFxArt.Even(22 - 14 * Out(u)), 1, dx * 10, Body + dy * 10, 0.8f);
                    return time;
                }
                case "art_pierce":
                {
                    // The shot: a white-cold line the whole way, sparks at the bow, a light along it.
                    float L = c.Length;
                    const float life = 0.32f;
                    if (In(t, 0, life))
                    {
                        float u = t / life, alpha = 1 - u * u;
                        for (float along = 8; along < L; along += 16)
                        {
                            Put(o, FxShape.Streak, FxRamp.Cold, 16, 2, dx * along, Body + dy * along, alpha, face);
                            if (u < 0.4f) Put(o, FxShape.Streak, FxRamp.Cold, 16, 1, dx * along - dy * 3, Body + dy * along + dx * 3, alpha * 0.5f, face);
                        }
                    }
                    if (In(t, 0, 0.14f)) Put(o, FxShape.Glint, FxRamp.Cold, 9, 0, dx * 8, Body + dy * 8);
                    FxLayers.Embers(o, FxRamp.Cold, 6, t, 0, 12, seed, dx * 8, Body + dy * 8, 70, face, 0.22f);
                    if (In(t, 0, 0.25f)) FxLayers.Light(o, FxRamp.Cold, 24, 0.6f * (1 - U(t, 0.1f, 0.25f)), dx * Math.Min(L, 40), Body, 2);
                    return 0.4f;
                }
                // ---------------- boomerang ----------------
                case "art_whirl_start":
                {
                    // The boomerang goes round at the true reach for the whole throw, a trail behind it, the circle
                    // faintly on the floor.
                    float time = c.Length > 0 ? c.Length : 1.5f;
                    if (!In(t, 0, time)) return time;
                    float turn = face + 720 * t / time;
                    for (int k = 0; k < 4; k++)
                    {
                        float a = turn - k * 14;
                        if (k == 0) Put(o, FxShape.Blade, FxRamp.Sand, 9, (int)(t * 20) & 3, Cos(a) * R, Body + Sin(a) * R * 0.85f, 1, t * 900);
                        else Put(o, FxShape.Dot, FxRamp.Sand, 2, 2, Cos(a) * R, Body + Sin(a) * R * 0.85f, 0.6f - k * 0.14f);
                    }
                    Put(o, FxShape.Edge, FxRamp.Sand, SkillFxArt.Even(2 * R), 1, 0, 0, 0.18f, 0, true);
                    return time;
                }
                case "art_whirl":
                    Ring(o, FxRamp.Sand, t, 0, R, 1, 0.06f, 0.18f);
                    return 0.25f;
                // ---------------- staff ----------------
                case "art_wave":
                {
                    // Three arcs of force running out over the cone, the nearest brightest.
                    for (int k = 0; k < 3; k++)
                    {
                        float from = k * 0.05f;
                        if (!In(t, from, from + 0.32f)) continue;
                        float u = U(t, from, from + 0.32f), radius = R * (0.3f + 0.7f * Out(u));
                        Put(o, FxShape.Smear, FxRamp.Spirit, Math.Max(4, (int)radius), 90, 0, Body, (1 - u) * (1 - k * 0.25f), face);
                    }
                    FxLayers.Shock(o, FxRamp.Spirit, t, 0, R / 1.6f, dx * R * 0.4f, dy * R * 0.4f, 0.28f, true);
                    Fly(o, FxShape.Dot, FxRamp.Spirit, 2, 2, 7, t, 0.04f, 0.35f, R * 0.3f, R, seed, cone: 80, toward: face);
                    return 0.45f;
                }
                // ---------------- wand ----------------
                case "art_barrage_start":
                {
                    // Sparks at the wand's tip for the whole barrage.
                    float time = c.Length > 0 ? c.Length : 0.8f;
                    if (!In(t, 0, time)) return time;
                    if (((int)(t * 20) & 1) == 0) Put(o, FxShape.Glint, FxRamp.Fire, 5, 0, dx * 9, Body + dy * 9);
                    FxLayers.Embers(o, FxRamp.Fire, 3, Frac(t / 0.2f) * 0.2f, 0, 8, seed + (int)(t * 5), dx * 9, Body + dy * 9, 70, face, 0.18f);
                    FxLayers.Light(o, FxRamp.Fire, 14, 0.4f, dx * 9, Body, 2);
                    return time;
                }
                // ---------------- orb ----------------
                case "art_gravity_start":
                {
                    // The well: a dark core ringed in violet, lines drawn in from all round, a ring closing on it.
                    float time = c.Length > 0 ? c.Length : 1.5f;
                    if (!In(t, 0, time)) return time;
                    float u = t / time;
                    Put(o, FxShape.Blob, FxRamp.Violet, 6 + (int)(2 * u), (int)(t * 12) & 3, 0, Body, 0.9f);
                    Put(o, FxShape.Edge, FxRamp.Violet, SkillFxArt.Even(18 + 4 * u), 1, 0, Body, 0.9f);
                    Put(o, FxShape.Edge, FxRamp.Violet, SkillFxArt.Even(2 * R * (1 - Frac(t * 1.6f))), 1, 0, 0, 0.35f, 0, true);
                    Gather(o, FxRamp.Violet, Frac(t / 0.35f) * 0.35f, 0.35f, R, 8, seed + (int)(t / 0.35f), cy: Body);
                    FxLayers.Light(o, FxRamp.Violet, R * 0.8f, 0.45f, 0, Body, 2);
                    return time;
                }
                case "art_gravity":
                {
                    // The burst: a violet ball, a shock over the floor, sparks.
                    FxLayers.Ball(o, FxRamp.Violet, t, 0, R * 0.6f, seed & 1, 0, Body * 0.5f);
                    FxLayers.Shock(o, FxRamp.Violet, t, 0, R / 1.3f, 0, 0, 0.3f, true);
                    Ring(o, FxRamp.Violet, t, 0, R, 2, 0.1f, 0.3f);
                    FxLayers.Embers(o, FxRamp.Violet, 10, t, 0.02f, R * 0.8f, seed, 0, Body, 360, 0, 0.3f);
                    if (In(t, 0, 0.3f)) FxLayers.Light(o, FxRamp.Violet, R + 12, 0.8f * (1 - U(t, 0.1f, 0.3f)), 0, Body, 3);
                    return 0.65f;
                }
                // The rest (a wand's blow, an art that ended) show nothing of their own.
                default:
                    return 0;
            }
        }
    }
}
