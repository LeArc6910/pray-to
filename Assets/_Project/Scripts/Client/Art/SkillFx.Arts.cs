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

        // A wand's barrage as the sheet has it (weapon_kinds.csv: art_count, art_arc, shot_speed in pixels a second),
        // for the tails the effect draws behind its sparks. The sparks themselves are the game's projectiles.
        const int BarrageShots = 8;
        const float BarrageArc = 30, BarrageSpeed = 14 * 16;

        // Where a wand's spark ends (WorldView, a Spark shot gone).
        public const string SparkHit = "art_spark_hit";

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
                    // A guard before the body for as long as it holds: a film of steel thrown up (out from the hand in
                    // the first moment) in three bands, the outer one breathing, and a glint running along its edge.
                    const float life = 0.4f;
                    if (!In(t, 0, life)) return life;
                    float up = Out(U(t, 0, 0.08f)), breath = 0.5f + 0.5f * (float)Math.Sin(t * 16);
                    float gx = dx * 3, gy = Body + dy * 3;
                    int band = (int)(7 + 6 * up);
                    Put(o, FxShape.Crescent, FxRamp.Steel, band, 130, gx, gy, 1, face);
                    Put(o, FxShape.Crescent, FxRamp.Steel, band + 2, 130, gx, gy, 0.85f, face);
                    Put(o, FxShape.Crescent, FxRamp.Steel, band + 4, 120, gx, gy, 0.6f, face);
                    Put(o, FxShape.Crescent, FxRamp.Steel, band + 7, 100, gx, gy, 0.25f + 0.3f * breath, face);
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Steel, t < 0.05f ? 11 : 7, 1, gx + dx * 15, gy + dy * 15);
                    else
                    {
                        // One glint sliding from one end of the arc to the other, again and again.
                        float along = face - 55 + 110 * Frac((t - 0.1f) / 0.15f);
                        Put(o, FxShape.Glint, FxRamp.Steel, 5, 1, gx + Cos(along) * 15, gy + Sin(along) * 15);
                    }
                    FxLayers.Light(o, FxRamp.Steel, 22, 0.45f + 0.15f * breath, dx * 10, Body, 2);
                    return life;
                }
                case "art_parry_blocked":
                {
                    // The blow meets the guard: the arc lit gold for a moment, a gold star, a ring and sparks thrown back.
                    float bx = dx * 14, by = Body + dy * 14;
                    if (In(t, 0, 0.14f))
                    {
                        Put(o, FxShape.Crescent, FxRamp.Gold, 16, 140, dx * 3, Body + dy * 3, 1 - U(t, 0.06f, 0.14f), face);
                        Put(o, FxShape.Crescent, FxRamp.Gold, 21, 120, dx * 3, Body + dy * 3, 0.6f * (1 - U(t, 0, 0.14f)), face);
                    }
                    if (In(t, 0, 0.2f)) Put(o, FxShape.Glint, FxRamp.Gold, t < 0.07f ? 15 : t < 0.13f ? 11 : 7, t < 0.13f ? 2 : 1, bx, by);
                    FxLayers.Embers(o, FxRamp.Gold, 12, t, 0, 22, seed, bx, by, 170, face, 0.28f);
                    FxLayers.Shock(o, FxRamp.Gold, t, 0, 14, bx, by, 0.22f);
                    if (In(t, 0, 0.22f)) FxLayers.Light(o, FxRamp.Gold, 34, 1 - U(t, 0.05f, 0.22f), dx * 10, Body, 3);
                    return 0.32f;
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
                    // From where the jump ended (it began Length ahead, the way it shoots): dust kicked up where the
                    // feet left, the leap's afterimage and streaks between, a skid of dust under the landing, and at
                    // the bow a flash and the three arrows' first streaks, fanned as the host fans them.
                    float L = c.Length;
                    Puffs(o, FxRamp.Dust, 6, t, 0, 0.45f, 3, 11, seed, 8, 0.85f, dx * L, dy * L, 4);
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 6, t, 0, 0.3f, 2, 12, seed, hop: 5, cx: dx * L, cy: dy * L, cone: 120, toward: face);
                    if (In(t, 0, 0.2f)) Put(o, FxShape.Ghost, FxRamp.Wind, 1, 0, dx * L * 0.55f, dy * L * 0.55f, 0.3f * (1 - U(t, 0, 0.2f)));
                    if (In(t, 0, 0.26f))
                    {
                        float u = t / 0.26f;
                        for (int i = 0; i < 4; i++)
                        {
                            float along = L * (0.35f + 0.65f * (i + Rnd(seed, i, 21)) / 4), aside = (i * 2 % 5 - 2) * 3.5f;
                            Put(o, FxShape.Streak, FxRamp.Wind, 12 + 4 * (i % 2), 2, dx * along - dy * aside, Body + dy * along + dx * aside, 1 - u * u, face + 180);
                        }
                    }
                    Puffs(o, FxRamp.Dust, 3, t, 0.06f, 0.4f, 1, 6, seed + 3, 5, 0.7f, -dx * 2, -dy * 2, 3);
                    Fly(o, FxShape.Dot, FxRamp.Dust, 1, 2, 4, t, 0.05f, 0.25f, 1, 9, seed + 5, hop: 3, cone: 80, toward: face + 180);
                    // The shots: fifteen degrees apart (weapon_kinds.csv art_arc), each streak running out with its arrow.
                    float tipX = dx * 9, tipY = Body + dy * 9;
                    if (In(t, 0, 0.14f)) Put(o, FxShape.Glint, FxRamp.Wind, t < 0.06f ? 11 : 7, t < 0.06f ? 2 : 1, tipX, tipY);
                    if (In(t, 0, 0.16f))
                    {
                        float u = t / 0.16f, gone = 256 * t;
                        for (int i = -1; i <= 1; i++)
                        {
                            float a = face + i * 15;
                            Put(o, FxShape.Streak, FxRamp.Wind, 20, 2, tipX + Cos(a) * (gone - 8), tipY + Sin(a) * (gone - 8), 1 - u * u, a);
                            Put(o, FxShape.Streak, FxRamp.Wind, 12, 1, tipX + Cos(a) * (gone - 24), tipY + Sin(a) * (gone - 24), 0.6f * (1 - u), a);
                        }
                    }
                    FxLayers.Embers(o, FxRamp.Wind, 5, t, 0, 12, seed, tipX, tipY, 60, face, 0.2f);
                    if (In(t, 0, 0.18f)) FxLayers.Light(o, FxRamp.Wind, 20, 0.6f * (1 - U(t, 0.06f, 0.18f)), tipX, Body, 2);
                    return 0.5f;
                }
                // ---------------- crossbow ----------------
                case "art_pierce_start":
                {
                    // Aiming, half a second that reads: two dashed lines either side of the way it will go, closing
                    // onto it as the shot comes; the line itself brightening; a reticle closing on the bolt's tip,
                    // sparks drawn in and a glint that grows. Pale amber: the player's own mark, never an enemy's red.
                    float time = 0.5f, L = c.Length;
                    if (!In(t, 0, time)) return time;
                    float u = t / time, apart = 7 * (1 - Out(u));
                    int walk = (int)(t * 16) & 3;
                    for (float along = 12; along + 32 <= L + 16; along += 32)
                    {
                        float mx = dx * (along + 16), my = Body + dy * (along + 16);
                        Put(o, FxShape.Tether, FxRamp.Gold, 32, walk, mx, my, 0.35f + 0.65f * u, face);
                        if (apart < 0.75f) continue;
                        Put(o, FxShape.Tether, FxRamp.Gold, 32, walk + 2, mx - dy * apart, my + dx * apart, 0.55f, face);
                        Put(o, FxShape.Tether, FxRamp.Gold, 32, walk + 2, mx + dy * apart, my - dx * apart, 0.55f, face);
                    }
                    float tipX = dx * 10, tipY = Body + dy * 10;
                    Put(o, FxShape.Reticle, FxRamp.Gold, SkillFxArt.Even(28 - 16 * Out(u)), 0, tipX, tipY, 0.6f + 0.4f * u);
                    Gather(o, FxRamp.Gold, Frac(t / 0.25f) * 0.25f, 0.25f, 16, 5, seed + (int)(t / 0.25f), tipX, tipY);
                    if (u > 0.5f) Put(o, FxShape.Glint, FxRamp.Gold, u > 0.85f ? 9 : 5, u > 0.85f ? 2 : 1, tipX, tipY);
                    FxLayers.Light(o, FxRamp.Gold, 12 + 10 * u, 0.3f + 0.4f * u, tipX, Body, 2);
                    return time;
                }
                case "art_pierce":
                {
                    // The shot: a thick white-cold beam the whole way for a moment (the line's true width), thinning
                    // to a streak that lingers; a burst at the bow, a star where it ends, sparks shed along it.
                    float L = Math.Max(16, c.Length);
                    const float life = 0.4f;
                    int thick = t < 0.08f ? 7 : t < 0.14f ? 5 : t < 0.2f ? 3 : 0;
                    if (thick > 0)
                    {
                        int ripple = (int)(t * 30) & 3;
                        for (float along = 6; along < L - 8; along += 32)
                            Put(o, FxShape.Beam, FxRamp.Cold, thick, ripple, dx * (along + 16), Body + dy * (along + 16), 1, face);
                    }
                    if (In(t, 0.14f, life))
                    {
                        float alpha = 1 - U(t, 0.2f, life);
                        for (float along = 8; along < L; along += 16)
                            Put(o, FxShape.Streak, FxRamp.Cold, 16, t < 0.26f ? 2 : 1, dx * along, Body + dy * along, alpha, face);
                    }
                    if (In(t, 0.02f, 0.36f))
                    {
                        float u = U(t, 0.02f, 0.36f);
                        for (int i = 0; i < 10; i++)
                        {
                            float along = L * (i + Rnd(seed, i, 22)) / 10, aside = (Rnd(seed, i, 23) < 0.5f ? -1 : 1) * (3 + 7 * Out(u) * (0.5f + 0.5f * Rnd(seed, i, 24)));
                            Put(o, FxShape.Dot, FxRamp.Cold, 1 + (i & 1), 2, dx * along - dy * aside, Body + dy * along + dx * aside, 1 - u * u);
                        }
                    }
                    float tipX = dx * 9, tipY = Body + dy * 9, endX = dx * L, endY = Body + dy * L;
                    if (In(t, 0, 0.16f)) Put(o, FxShape.Glint, FxRamp.Cold, t < 0.07f ? 15 : 9, 2, tipX, tipY);
                    FxLayers.Shock(o, FxRamp.Cold, t, 0, 12, tipX, tipY, 0.2f);
                    FxLayers.Embers(o, FxRamp.Cold, 8, t, 0, 16, seed, tipX, tipY, 80, face, 0.26f);
                    if (In(t, 0.01f, 0.2f)) Put(o, FxShape.Glint, FxRamp.Cold, t < 0.1f ? 11 : 7, 1, endX, endY);
                    FxLayers.Embers(o, FxRamp.Cold, 6, t, 0.02f, 14, seed + 9, endX, endY, 200, face, 0.26f);
                    if (In(t, 0, 0.28f))
                    {
                        float glow = 1 - U(t, 0.08f, 0.28f);
                        FxLayers.Light(o, FxRamp.Cold, 28, 0.9f * glow, tipX, Body, 3);
                        FxLayers.Light(o, FxRamp.Cold, 24, 0.6f * glow, dx * L * 0.5f, Body + dy * L * 0.5f, 2);
                        FxLayers.Light(o, FxRamp.Cold, 22, 0.7f * glow, endX, endY, 2);
                    }
                    return life + 0.05f;
                }
                // ---------------- boomerang ----------------
                case "art_whirl_start":
                {
                    // The boomerang goes twice round at the true reach for the whole throw: the blade spinning, a long
                    // smear of sand behind it along its way, the circle on the floor in stepping dashes.
                    float time = c.Length > 0 ? c.Length : 1.5f;
                    if (!In(t, 0, time)) return time;
                    float turn = face + 720 * t / time, fade = Math.Min(1, Math.Min(t / 0.1f, (time - t) / 0.15f));
                    float bx = Cos(turn) * R, by = Body + Sin(turn) * R;
                    Put(o, FxShape.Edge, FxRamp.Sand, SkillFxArt.Even(2 * R), 1, 0, 0, 0.3f, 0, true);
                    Put(o, FxShape.Dashes, FxRamp.Sand, SkillFxArt.Even(2 * R), (int)(t * 10) & 1, 0, 0, 0.75f, 0, true);
                    Put(o, FxShape.Smear, FxRamp.Sand, (int)R, 140, 0, Body, 0.9f * fade, turn - 70);
                    for (int k = 1; k <= 3; k++)
                    {
                        float a = turn - k * 9;
                        Put(o, FxShape.Blade, FxRamp.Sand, 11, 0, Cos(a) * R, Body + Sin(a) * R, 0.45f - 0.12f * k, t * 1080 - k * 70);
                    }
                    Put(o, FxShape.Blade, FxRamp.Sand, 13, 0, bx, by, 1, t * 1080);
                    Put(o, FxShape.Blade, FxRamp.Sand, 13, 0, bx, by, 1, t * 1080 + 90);
                    if (((int)(t * 15) & 1) == 0) Put(o, FxShape.Glint, FxRamp.Sand, 5, 1, bx, by);
                    FxLayers.Embers(o, FxRamp.Sand, 3, Frac(t / 0.15f) * 0.15f, 0, 7, seed + (int)(t / 0.15f), bx, by, 90, turn - 90, 0.15f);
                    FxLayers.Light(o, FxRamp.Sand, 16, 0.5f, bx, by - Body, 2);
                    return time;
                }
                case "art_whirl":
                    // Each of its blows: the circle flashes out to the reach.
                    Ring(o, FxRamp.Sand, t, 0, R, 2, 0.08f, 0.24f, 0, 0, true);
                    FxLayers.Shock(o, FxRamp.Sand, t, 0, R / 1.3f, 0, 0, 0.24f, true);
                    return 0.3f;
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
                    // The whole barrage from the wand's tip: a fire that stays lit, and for each of the eight sparks
                    // (weapon_kinds.csv art_count over art_time, fanned out and back inside art_arc as the host fans
                    // them) a flash at the tip, a tail of flame running out behind it and embers thrown its way.
                    float time = c.Length > 0 ? c.Length : 0.8f;
                    if (!In(t, 0, time + 0.2f)) return time + 0.2f;
                    float tipX = dx * 9, tipY = Body + dy * 9, kick = 0;
                    for (int i = 0; i < BarrageShots; i++)
                    {
                        float from = i * time / BarrageShots, age = t - from;
                        if (age < 0 || age >= 0.22f) continue;
                        float side = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) / (float)((BarrageShots + 1) / 2);
                        float a = face + side * BarrageArc / 2, gone = BarrageSpeed * age;
                        if (age < 0.1f)
                        {
                            float u = age / 0.1f;
                            Put(o, FxShape.Streak, FxRamp.Fire, 20, u < 0.6f ? 3 : 2, tipX + Cos(a) * (gone - 8), tipY + Sin(a) * (gone - 8), 1 - u * u * u, a);
                        }
                        FxLayers.Embers(o, FxRamp.Fire, 3, t, from, 12, seed + i, tipX, tipY, 50, a, 0.2f);
                        kick = Math.Max(kick, 1 - age / 0.1f);
                    }
                    if (t < time)
                    {
                        Put(o, FxShape.Flame, FxRamp.Fire, 7, (int)(t * 16) & 3, tipX, tipY + 2);
                        Put(o, FxShape.Glint, FxRamp.Fire, kick > 0.5f ? 11 : kick > 0 ? 7 : 5, kick > 0.5f ? 2 : 1, tipX, tipY);
                        FxLayers.Light(o, FxRamp.Fire, 18 + 8 * kick, 0.5f + 0.4f * kick, tipX, Body, 2);
                    }
                    return time + 0.2f;
                }
                case "art_spark_hit":
                {
                    // Where a wand's spark ends: a small star and a few embers.
                    if (In(t, 0, 0.14f)) Put(o, FxShape.Glint, FxRamp.Fire, t < 0.06f ? 9 : 5, t < 0.06f ? 2 : 1, 0, 0);
                    FxLayers.Embers(o, FxRamp.Fire, 4, t, 0, 9, seed, 0, 0, 360, 0, 0.2f);
                    return 0.22f;
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
