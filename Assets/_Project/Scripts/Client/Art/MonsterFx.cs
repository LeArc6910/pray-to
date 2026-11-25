using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Which effect of an ordinary monster.
    enum MobFx : byte
    {
        // The warning on the ground at the mark's own place: a thin red rim at the real reach, the inside filling in.
        Mark,
        // The monster's own wind-up cue, round its feet.
        Tell,
        // Where the attack landed: the blow, then what thins out.
        Land,
        // What a blow left on the floor, for as long as it lies there.
        Pool,
        // A shot in flight.
        Shot,
        // One-offs: a shot off a wall, a returning shot turning, where a blink left from, a split, a charge into rock,
        // a call made, shards of the fallen, a trap set off, a blow turned by a guard.
        Bounce,
        Turn,
        BlinkOut,
        Split,
        Stagger,
        Rally,
        Deathburst,
        Trip,
        Guard,
        // Loops on a body: stars over a dazed one, flames at the feet of an empowered one, a mending line (from the
        // mender, Size its length) and the plus signs over whoever it fills.
        Stars,
        Empowered,
        Tether,
        Mended,
        // The rest of the kinds (2026-11-22). One-offs: one gone to come out behind someone, a deathburst put out
        // (steam), one coming up out of the ground and one going back under.
        Vanish,
        Quench,
        Surface,
        Dive,
        // Loops: the mound of one under the ground (Dir the way it goes, Armed when it moves), the thin red line
        // between two that stand shoulder to shoulder (from one, Size its length), the drops on a poisoned player,
        // and the afterimages a swoop leaves behind it in the air (Dir the way it flies).
        Mound,
        Link,
        Poisoned,
        Wake,
        // The ice cave and the sand cave (2026-11-24, B2b). A loop: sand pouring from the ceiling onto one buried in the
        // sand cave (World.UnderFallingSand), so the buried are seen before they come up. One-offs: ice thrown up behind
        // a charge that slides on (World.MonsterFxSlide), and a dormant mid-boss breaking out of its stone or its ice
        // (World.MonsterFxWake).
        SandFall,
        Slide,
        Rouse,
        // The crystal cave and the underground lake (2026-11-25, B3b). A loop: the rings and bubbles over one under the
        // water (World.UnderWater) in place of a mound of earth. One-offs: a shot glancing off crystal (World.MonsterFxGlance),
        // a shell turning a shot back (World.MonsterFxReflect), a prism's shot breaking into three (World.MonsterFxRefract),
        // and a splash where someone wades the shallows.
        Ripple,
        Glance,
        Reflect,
        Refract,
        Splash,
        // The lava fields and the void abyss (2026-11-25, B4b). One-offs: a steam vent bursting (a column of steam
        // and sparks, Hazard.Steam), a cracked plate giving way into the void (World.MonsterFxCrumble), and a player
        // coming up on firm ground after a fall (World.MonsterFxFall). A loop: the cracks spreading in a plate under
        // someone's feet, Progress how near it is to going (World.PlateHold).
        Vent,
        Crumble,
        Fall,
        Crack,
    }

    // What a kind's effects are made of besides its colours, so each reads as itself (Docs/MonsterPatterns.md): told
    // from its id (MonsterFx.LookOf).
    enum MobLook : byte
    {
        Plain,
        // Motes that drift up: the mushrooms and the bat of their wood.
        Spore,
        // Hard shards and white glints: the crystal caverns.
        Crystal,
        // Splinters of ice.
        Frost,
        // Embers and tongues of flame.
        Ember,
        // Grit thrown up.
        Sand,
        // Drops and spray.
        Water,
        // Dark smoke with points of starlight: the abyss.
        Shadow,
        // Sparks off iron: the demon soldiers.
        Iron,
        // Violet fire and runes: the demon casters.
        Hex,
    }

    struct MobContext
    {
        // The attack (Mark, Tell, Land, Shot).
        public MonsterAttackShape Shape;
        // A mark's geometry, or which pool.
        public HazardShape Area;
        // The monster's colours: an element's ramp, or Plain with its own colour as Tint.
        public FxRamp Ramp;
        public uint Tint;
        // Radians.
        public float Dir;
        // In pixels: a circle's radius as it really reaches (the player's radius included), a cone's or a line's length.
        public float Size;
        // A cone's degrees, a line's width or a ring's inner radius in pixels.
        public float Width;
        // A mark's: 0 when laid, 1 when it lands. A pool's: how far faded in (or not yet out).
        public float Progress;
        // A wind-up's whole length in seconds.
        public float Life;
        public int Seed;
        // A trap that is armed; a returning shot standing still at its turn.
        public bool Armed;
        // A mark that harms nothing: where a blink will come out, the flash before the shards of the fallen.
        public bool Harmless;
        // What the kind's effects are made of (MonsterFx.LookOf).
        public MobLook Look;
    }

    // Ordinary monsters' warnings and effects as functions of time (2026-11-21, Docs/Combat.md 몬스터의 공격 시트,
    // Docs/SkillEffects.md): like SkillFx it keeps no state and says which pieces are where at a moment, so the game
    // (WorldView.MonsterFx) and the preview (ArtPreview --only monsterfx) draw the same. The rules: three parts
    // (gathering = the warning, the blow at its brightest and shortest, what thins out in 0.2 to 0.4 s); the rim at
    // the real reach; anything of an enemy's on the ground wears a thin red rim, its inside the element's own shape;
    // a monster's shot is its body's colour with a bright head and a dark rim; no more than Most pieces to an effect.
    static class MonsterFx
    {
        public const int Most = 24;
        const float Deg = 180f / (float)Math.PI;
        // The middle of a monster's body above its feet, in pixels.
        const float Body = 6;

        // The colours a monster's effects are drawn in: cold, fire or the void's violet, else greys tinted its own colour.
        public static FxRamp RampOf(string id, SkillTag element)
        {
            if (id != null && (id.StartsWith("void") || id.StartsWith("abyss") || id == "demon_caster" || id == "spore_shooter" || id == "myconid")) return FxRamp.Violet;
            // The lake's are water whatever the cold in it; the glowing bat's spores are its own teal; sand is sand.
            // The mushroom forest's new kinds (B1b): its spore clouds are the green-teal of the glowing wood.
            if (id == "lake_crab" || id == "tide_wisp" || id == "glow_bat" || id == "puffball" || id == "glow_shooter" || id == "cap_giant"
                || id == "mycelium_mother") return FxRamp.Spirit;
            if (id == "myconid_captain") return FxRamp.Violet;
            if (id == "sand_scorpion" || id == "sand_worm") return FxRamp.Dust;
            // The sand cave's new kinds (B2b) are sand too; the queen's stings are venom green. The ice cave's are cold
            // by their element, its wolf and beetle as well.
            if (id == "bomb_scarab" || id == "great_worm" || id == "sandstone_beetle" || id == "temple_guardian") return FxRamp.Dust;
            if (id == "scorpion_queen") return FxRamp.Spirit;
            if (id == "frost_wolf" || id == "frost_beetle" || id == "frost_mammoth") return FxRamp.Cold;
            // The lake's new kinds (B3b) are water; the crystal cave's are the crystals' sky blue, the golem's chest violet.
            if (id == "tentacle" || id == "great_crab" || id == "tide_chief" || id == "moss_turtle") return FxRamp.Spirit;
            if (id == "mud_slime") return FxRamp.Dust;
            if (id == "crystal_mimic" || id == "crystal_tortoise" || id == "crystal_shooter" || id == "prism_wisp") return FxRamp.Cold;
            if (id == "geode_golem") return FxRamp.Violet;
            // The lava fields' new kinds (B4b) burn whatever their element; the abyss's are the void's violet.
            if (id == "salamander" || id == "magma_hurler" || id == "obsidian_beetle" || id == "forge_slime" || id == "forge_keeper") return FxRamp.Fire;
            if (id == "star_slime" || id == "shadow_stalker" || id == "gate_warden") return FxRamp.Violet;
            if (element == SkillTag.Cold) return FxRamp.Cold;
            if (element == SkillTag.Fire) return FxRamp.Fire;
            return FxRamp.Plain;
        }

        // What a kind's effects are made of, by its id.
        public static MobLook LookOf(string id)
        {
            switch (id)
            {
                case "spore_shooter":
                case "myconid":
                case "glow_bat":
                case "puffball":
                case "cap_giant":
                case "glow_shooter":
                case "myconid_captain":
                case "mycelium_mother":
                    return MobLook.Spore;
                case "crystal_bat":
                case "crystal_wisp":
                case "crystal_mimic":
                case "crystal_tortoise":
                case "crystal_shooter":
                case "prism_wisp":
                case "geode_golem":
                    return MobLook.Crystal;
                case "ice_slime":
                case "frost_wisp":
                case "frost_wolf":
                case "yeti":
                case "frost_beetle":
                case "frost_lord":
                case "frost_mammoth":
                    return MobLook.Frost;
                case "magma_slime":
                case "fire_bat":
                case "salamander":
                case "magma_hurler":
                case "obsidian_beetle":
                case "forge_slime":
                case "forge_keeper":
                    return MobLook.Ember;
                case "sand_scorpion":
                case "sand_worm":
                case "bomb_scarab":
                case "great_worm":
                case "sandstone_beetle":
                case "scorpion_queen":
                case "temple_guardian":
                    return MobLook.Sand;
                case "lake_crab":
                case "tide_wisp":
                case "tentacle":
                case "great_crab":
                case "mud_slime":
                case "tide_chief":
                case "moss_turtle":
                    return MobLook.Water;
                case "abyss_stalker":
                case "void_wisp":
                case "void_eye":
                case "void_splitter":
                case "void_shard":
                case "void_mote":
                case "star_slime":
                case "shadow_stalker":
                case "gate_warden":
                    return MobLook.Shadow;
                case "demon_soldier":
                    return MobLook.Iron;
                case "demon_caster":
                    return MobLook.Hex;
                default:
                    return MobLook.Plain;
            }
        }

        public static uint TintOf(Rgba color) => 0x1000000u | (uint)color.R << 16 | (uint)color.G << 8 | color.B;

        // Adds this moment's pieces. False once a one-off is over (a Mark, Pool, Shot or loop never is).
        public static bool Stamps(MobFx fx, in MobContext c, float t, List<FxStamp> o)
        {
            if (t < 0) return true;
            int from = o.Count;
            float life = float.PositiveInfinity;
            switch (fx)
            {
                case MobFx.Mark: Mark(c, t, o); break;
                case MobFx.Tell: Tell(c, t, o); break;
                case MobFx.Land:
                    life = Land(c, t, o);
                    if (t < life) LandLook(c, t, c.Size, o);
                    break;
                case MobFx.Pool: Pool(c, t, o); break;
                case MobFx.Shot: Shot(c, t, o); break;
                case MobFx.Bounce:
                    life = 0.25f;
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, 0, 0);
                    SkillFx.Fly(o, FxShape.Streak, c.Ramp, 4, 1, 4, t, 0, 0.2f, 2, 9, c.Seed, aimed: true);
                    break;
                case MobFx.Turn:
                    life = 0.3f;
                    if (SkillFx.Frac(t * 8) < 0.6f) Put(o, FxShape.Glint, c.Ramp, 9, 1, 0, 0);
                    Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(10 + 14 * t / life), SkillFxArt.BrokenFrame(1, 4, (int)(t * 20)), 0, 0, 1 - t / life);
                    break;
                case MobFx.BlinkOut:
                    life = 0.35f;
                    Put(o, FxShape.Smoke, c.Ramp, 12, Math.Min(3, (int)(t / life * 4)), 0, Body, 0.9f);
                    Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(18 * (1 - t / life) + 4), SkillFxArt.BrokenFrame(1, 5, (int)(t * 24)), 0, Body, 0.8f);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 2, 5, t, 0, 0.3f, 2, 10, c.Seed, rise: 6, cy: Body);
                    break;
                case MobFx.Split:
                    life = 0.35f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float u = t / life;
                        float along = c.Dir * Deg + 90 * side;
                        Put(o, FxShape.Blob, c.Ramp, 8, 1, SkillFx.Cos(along) * 8 * SkillFx.Out(u), Body + SkillFx.Sin(along) * 8 * SkillFx.Out(u) + (float)Math.Sin(u * Math.PI) * 5, 1 - u * u);
                    }
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 1, 6, t, 0, 0.3f, 2, 11, c.Seed, hop: 6, cy: 2);
                    break;
                case MobFx.Stagger:
                    life = 0.3f;
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 5, t, 0, 0.28f, 2, 12, c.Seed, hop: 5, cy: Body, cone: 120, toward: c.Dir * Deg + 180);
                    SkillFx.Ring(o, FxRamp.Dust, t, 0, 10, 2, cy: Body);
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 11, 2, SkillFx.Cos(c.Dir * Deg) * 6, Body + SkillFx.Sin(c.Dir * Deg) * 6);
                    break;
                case MobFx.Rally:
                    life = 0.4f;
                    SkillFx.Ring(o, FxRamp.Violet, t, 0, c.Size, 2, life: 0.36f, ground: true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, 6, t, 0, life, 10, 14, c.Seed, cy: 4);
                    break;
                case MobFx.Deathburst:
                    life = 0.3f;
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 13, 2, 0, Body);
                    SkillFx.Ring(o, c.Ramp, t, 0, 9, 2, cy: Body);
                    SkillFx.Fly(o, FxShape.Shard, c.Ramp, 6, 0, 6, t, 0, 0.26f, 3, 14, c.Seed, cy: Body, aimed: true);
                    break;
                case MobFx.Trip:
                    life = 0.2f;
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Danger, 11, 1, 0, 0);
                    SkillFx.Ring(o, FxRamp.Danger, t, 0, c.Size, 2, grow: 0.08f, life: 0.2f, ground: true);
                    break;
                case MobFx.Guard:
                    life = 0.2f;
                    if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, SkillFx.Cos(c.Dir * Deg) * 6, Body + SkillFx.Sin(c.Dir * Deg) * 6);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Steel, 4, 1, 3, t, 0, 0.18f, 4, 10, c.Seed, cx: SkillFx.Cos(c.Dir * Deg) * 6, cy: Body + SkillFx.Sin(c.Dir * Deg) * 6,
                        aimed: true, cone: 100, toward: c.Dir * Deg);
                    break;
                case MobFx.Stars:
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = t * 220 + i * 120;
                        Put(o, FxShape.Glint, FxRamp.Bolt, 5, SkillFx.Sin(angle) > 0 ? 1 : 0, SkillFx.Cos(angle) * 6, 17 + SkillFx.Sin(angle) * 2);
                    }
                    break;
                case MobFx.Empowered:
                    for (int i = 0; i < 2; i++)
                        Put(o, FxShape.Flame, FxRamp.Danger, 6, (int)(t * 9 + i * 2) & 3, i == 0 ? -5 : 5, 3, 0.9f);
                    Put(o, FxShape.Broken, FxRamp.Danger, 14, SkillFxArt.BrokenFrame(1, 4, (int)(t * 6)), 0, 0, 0.6f, 0, true);
                    break;
                case MobFx.Tether:
                {
                    // A dashed line walking from the mender to whoever it fills, in two pieces when it is long.
                    int pieces = c.Size > 120 ? 2 : 1;
                    float each = c.Size / pieces;
                    for (int i = 0; i < pieces; i++)
                    {
                        float middle = each * (i + 0.5f);
                        Put(o, FxShape.Tether, FxRamp.Spirit, SkillFxArt.Step4(each), (int)(t * 14) & 3, SkillFx.Cos(c.Dir * Deg) * middle, Body + SkillFx.Sin(c.Dir * Deg) * middle, 0.9f, c.Dir * Deg);
                    }
                    Put(o, FxShape.Glint, FxRamp.Spirit, 7, (int)(t * 6) & 1, 0, Body, 0.9f);
                    break;
                }
                case MobFx.Mended:
                    for (int i = 0; i < 2; i++)
                    {
                        float u = SkillFx.Frac(t * 1.2f + i * 0.5f);
                        Put(o, FxShape.Plus, FxRamp.Heal, 5, 0, i == 0 ? -4 : 4, 12 + 8 * u, 1 - u);
                    }
                    break;
                case MobFx.Vanish:
                    // It comes apart into dark smoke that is drawn up and in, a last star where its heart was.
                    life = 0.4f;
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.U(t, 0.03f * i, 0.3f + 0.03f * i);
                        Put(o, FxShape.Smoke, c.Ramp, 12 - 2 * i, Math.Min(3, (int)(u * 4)), (i - 1) * 5 * (1 - u), Body + 3 * i + 10 * u, 0.9f * (1 - u));
                    }
                    SkillFx.Fly(o, FxShape.Streak, c.Ramp, 5, 1, 6, t, 0, 0.26f, 12, 2, c.Seed, cy: Body, aimed: true);
                    if (t < 0.12f || SkillFx.In(t, 0.2f, 0.28f)) Put(o, FxShape.Glint, FxRamp.Steel, t < 0.12f ? 9 : 5, 1, 0, Body);
                    break;
                case MobFx.Quench:
                    // Melted before it could burst: steam going up, a few drops, no shards.
                    life = 0.45f;
                    SkillFx.Float(o, FxShape.Smoke, FxRamp.Mist, 9, 1, 4, t, 0, life, 5, 14, c.Seed, cy: 3);
                    SkillFx.Fly(o, FxShape.Dot, FxRamp.Cold, 2, 2, 5, t, 0, 0.3f, 2, 8, c.Seed, hop: 4);
                    Put(o, FxShape.Smoke, FxRamp.Cold, 12, Math.Min(3, (int)(t / life * 4)), 0, 0, 0.5f * (1 - t / life), 0, true);
                    break;
                case MobFx.Surface:
                    // Up out of the ground: a column of grit, clods thrown wide, a ring in the dust.
                    life = 0.4f;
                    for (int i = -1; i <= 1; i++)
                        Put(o, FxShape.Streak, FxRamp.Dust, SkillFxArt.Step4(8 + 10 * SkillFx.Out(SkillFx.U(t, 0, 0.1f))), 2, i * 4, 7, t < 0.14f ? 1 : 1 - (t - 0.14f) / 0.2f, 90);
                    SkillFx.Ring(o, FxRamp.Dust, t, 0, 11, 2, ground: true);
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 7, t, 0, 0.34f, 3, 13, c.Seed, hop: 10);
                    SkillFx.Float(o, FxShape.Smoke, FxRamp.Dust, 7, 2, 2, t, 0.1f, life, 6, 5, c.Seed);
                    break;
                case MobFx.SandFall:
                {
                    // A thin stream from high above down onto the mound, grains running down it, a little heap and puffs
                    // where it lands. Pale and soft: the sand's own colour, no red (it is a sign, not a warning).
                    Put(o, FxShape.Streak, FxRamp.Dust, 32, 2, 0, 22, 0.8f, -90);
                    Put(o, FxShape.Streak, FxRamp.Dust, 24, 1, 2, 18, 0.5f, -90);
                    for (int i = 0; i < 8; i++)
                    {
                        float u = SkillFx.Frac(t * 1.6f + i / 8f + (c.Seed & 7) / 8f);
                        Put(o, FxShape.Dot, FxRamp.Dust, i % 3 == 0 ? 2 : 1, i & 1, ((i * 5 + c.Seed) % 5) - 2, 38 - 34 * u, 1);
                    }
                    Put(o, FxShape.Blob, FxRamp.Dust, 8, 2, 0, 2);
                    Put(o, FxShape.Smoke, FxRamp.Dust, 9, 1 + ((int)(t * 4) & 1), 0, 5, 0.6f);
                    break;
                }
                case MobFx.Slide:
                    // Ice thrown up behind it as it skids, and a cold mist.
                    life = 0.4f;
                    SkillFx.Fly(o, FxShape.Shard, FxRamp.Cold, 4, 0, 6, t, 0, 0.3f, 2, 10, c.Seed, hop: 5, cy: 2, cone: 70, toward: c.Dir * Deg + 180);
                    SkillFx.Float(o, FxShape.Smoke, FxRamp.Mist, 8, 1, 3, t, 0, life, 5, 6, c.Seed, cy: 2);
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Cold, 9, 1, 0, 3);
                    break;
                case MobFx.Rouse:
                    // A statue's stone or a beast's ice breaks: a bright flash, chunks thrown wide, a ring on the floor.
                    life = 0.6f;
                    if (t < 0.12f) Put(o, FxShape.Glint, c.Ramp, 17, 2, 0, Body * 2);
                    SkillFx.Fly(o, c.Ramp == FxRamp.Cold ? FxShape.Shard : FxShape.Chunk, c.Ramp, 5, 0, 12, t, 0, 0.45f, 6, 22, c.Seed, hop: 10, cy: Body * 2);
                    SkillFx.Ring(o, c.Ramp, t, 0, 24, 2, ground: true);
                    SkillFx.Float(o, FxShape.Smoke, c.Ramp == FxRamp.Cold ? FxRamp.Mist : FxRamp.Dust, 12, 1, 4, t, 0.05f, life, 8, 10, c.Seed, cy: Body);
                    break;
                case MobFx.Ripple:
                {
                    // Not a warning (no red): dark water over it, two rings widening out from it in turn, bubbles rising
                    // and bursting; one that travels draws a wake of broken rings behind it. Tells the buried in the lake.
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 20, 0, 0, 0, 0.55f, 0, true);
                    Put(o, FxShape.Broken, FxRamp.Cold, 16, SkillFxArt.BrokenFrame(1, 4, (int)(t * 3) & 3), 0, 1, 0.7f, 0, true);
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.Frac(t * 0.7f + i / 3f + (c.Seed & 7) / 8f);
                        Put(o, FxShape.Edge, FxRamp.Cold, SkillFxArt.Even(8 + 20 * u), u < 0.3f ? 2 : 1, 0, 1, 0.95f * (1 - u), 0, true);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float u = SkillFx.Frac(t * 1.3f + i / 4f + (c.Seed & 15) / 16f);
                        if (u < 0.75f) Put(o, FxShape.Dot, FxRamp.Cold, u < 0.6f ? 2 : 1, 2, ((i * 7 + c.Seed) % 9) - 4, 1 + 7 * u, 1 - u * 0.8f);
                    }
                    if (c.Armed)
                    {
                        float back = c.Dir * Deg + 180;
                        for (int i = 0; i < 3; i++)
                        {
                            float u = SkillFx.Frac(t * 1.6f + i / 3f);
                            Put(o, FxShape.Broken, FxRamp.Cold, SkillFxArt.Even(8 + 10 * u), SkillFxArt.BrokenFrame(1, 4, i), SkillFx.Cos(back) * (4 + 10 * u), SkillFx.Sin(back) * (4 + 10 * u), 0.8f * (1 - u), 0, true);
                        }
                    }
                    break;
                }
                case MobFx.Glance:
                    // A shot off crystal: a white-pink flash at the crystal, a sky ring and splinters of light.
                    life = 0.3f;
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Cold, 13, 2, 0, 0);
                    Put(o, FxShape.Edge, FxRamp.Cold, SkillFxArt.Even(6 + 12 * t / life), 1, 0, 0, 1 - t / life);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Cold, 5, 1, 5, t, 0, 0.25f, 3, 11, c.Seed, aimed: true);
                    break;
                case MobFx.Reflect:
                    // A shell turning a shot: a crescent of light on the shell's face and a bright glint.
                    life = 0.35f;
                    if (t < 0.12f) Put(o, FxShape.Glint, FxRamp.Steel, 15, 2, 0, Body);
                    Put(o, FxShape.Crescent, FxRamp.Cold, 10, 90, 0, Body, 1 - t / life, c.Dir * Deg);
                    SkillFx.Fly(o, FxShape.Shard, FxRamp.Cold, 4, 0, 4, t, 0, 0.3f, 3, 10, c.Seed, hop: 4, cy: Body);
                    break;
                case MobFx.Refract:
                    // A prism's shot breaking: a bright burst and three streaks of colour fanning out.
                    life = 0.35f;
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Gold, 13, 2, 0, 0);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Violet, 2, 2, 3, t, 0, 0.3f, 4, 14, c.Seed, aimed: true);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Cold, 2, 2, 3, t, 0, 0.3f, 4, 14, c.Seed + 3, aimed: true);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Gold, 2, 2, 3, t, 0, 0.3f, 4, 14, c.Seed + 7, aimed: true);
                    break;
                case MobFx.Vent:
                    // A burst of steam: a white flash at the vent, a column of steam rising and spreading, sparks thrown
                    // up and a scald ring on the floor (red-rimmed: it hurt whoever stood there).
                    life = 0.7f;
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Fire, 13, 2, 0, 4);
                    for (int i = 0; i < 4; i++)
                    {
                        float u = SkillFx.U(t, i * 0.06f, 0.6f);
                        if (u <= 0 || u >= 1) continue;
                        Put(o, FxShape.Smoke, FxRamp.Mist, SkillFxArt.Even(8 + 10 * u), Math.Min(3, (int)(u * 4)), ((i * 5 + c.Seed) % 5) - 2, 4 + 26 * u, 0.95f * (1 - u * u));
                    }
                    SkillFx.Fly(o, FxShape.Dot, FxRamp.Fire, 2, 2, 6, t, 0, 0.4f, 2, 10, c.Seed, hop: 12, cy: 4);
                    SkillFx.Ring(o, FxRamp.Danger, t, 0, c.Size, 1, grow: 0.1f, life: 0.3f, ground: true);
                    break;
                case MobFx.Crumble:
                    // A plate gives way: its pieces drop in and shrink into the dark, violet dust, a few stars pulled down.
                    life = 0.6f;
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72 + c.Seed % 72, u = SkillFx.U(t, 0, 0.5f);
                        Put(o, FxShape.Chunk, FxRamp.Violet, Math.Max(2, (int)(5 * (1 - u))), i & 1, SkillFx.Cos(a) * 5 * (1 - u), SkillFx.Sin(a) * 4 * (1 - u) - 4 * u, 1 - u, 0, true);
                    }
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 18, 0, 0, 0, 0.7f, 0, true);
                    SkillFx.Ring(o, FxRamp.Violet, t, 0, 12, 2, ground: true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, 5, t, 0, life, 8, -6, c.Seed);
                    break;
                case MobFx.Fall:
                    // Up out of the void: a swirl of stars round the feet and a violet puff.
                    life = 0.6f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60 + t * 540, u = t / life;
                        Put(o, FxShape.Glint, FxRamp.Violet, 5, i & 1, SkillFx.Cos(a) * 9 * (1 - u), 6 + SkillFx.Sin(a) * 4 + 10 * u, 1 - u);
                    }
                    Put(o, FxShape.Smoke, FxRamp.Violet, 12, Math.Min(3, (int)(t / life * 4)), 0, 4, 0.8f * (1 - t / life));
                    break;
                case MobFx.Crack:
                {
                    // The plate under someone's feet cracking: a red rim at the plate's edge closing in as it nears giving
                    // way, cracks spreading from the middle, and dust beginning to fall (the warning; red is danger).
                    float p = Math.Clamp(c.Progress, 0, 1);
                    Put(o, FxShape.Edge, FxRamp.Danger, SkillFxArt.Even(16 - 4 * p), p > 0.6f ? 2 : 1, 0, 0, 0.5f + 0.5f * p, 0, true);
                    Put(o, FxShape.Broken, FxRamp.Violet, SkillFxArt.Even(6 + 10 * p), SkillFxArt.BrokenFrame(1, 4, (int)(p * 8)), 0, 0, 0.9f, c.Seed % 90, true);
                    if (p > 0.5f) SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 1, 1, 3, t, 0, 0.6f, 6, -4, c.Seed);
                    break;
                }
                case MobFx.Splash:
                    // A step in the shallows: a little ring and drops thrown up.
                    life = 0.35f;
                    Put(o, FxShape.Edge, FxRamp.Cold, SkillFxArt.Even(6 + 14 * t / life), 2, 0, 0, 0.9f * (1 - t / life), 0, true);
                    SkillFx.Fly(o, FxShape.Dot, FxRamp.Cold, 2, 2, 5, t, 0, 0.3f, 3, 9, c.Seed, hop: 8);
                    break;
                case MobFx.Dive:
                    // Back under: the ground closes over it, a puff and a few grains sliding in.
                    life = 0.35f;
                    Put(o, FxShape.Edge, FxRamp.Dust, SkillFxArt.Even(20 * (1 - t / life) + 4), 1, 0, 0, 1 - t / life, 0, true);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60 + c.Seed % 60, u = SkillFx.U(t, 0, 0.3f);
                        Put(o, FxShape.Dot, FxRamp.Dust, 2, i & 1, SkillFx.Cos(a) * 10 * (1 - u), SkillFx.Sin(a) * 7 * (1 - u), 1 - u * u, 0, true);
                    }
                    SkillFx.Float(o, FxShape.Smoke, FxRamp.Dust, 7, 2, 3, t, 0, life, 5, 7, c.Seed);
                    break;
                case MobFx.Mound:
                {
                    // Not a warning (no red, no rim): heaped earth with a crack across it, a grain or two rolling off
                    // now and then; one that travels throws a ripple out behind it.
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 18, 0, 0, 0, 0.6f, 0, true);
                    Put(o, FxShape.Edge, FxRamp.Dust, 14, 1, 0, 1, 0.7f, 0, true);
                    Put(o, FxShape.Blob, FxRamp.Dust, 12, 2, 0, 3);
                    Put(o, FxShape.Chunk, FxRamp.Dust, 4, 1, -3, 4);
                    Put(o, FxShape.Chunk, FxRamp.Dust, 3, 2, 3, 3);
                    Put(o, FxShape.Crack, FxRamp.Smoke, 8, c.Seed & 3, 0, 3, 0.8f, 20);
                    float roll = SkillFx.Frac(t * 0.7f + (c.Seed & 7) / 8f);
                    if (roll < 0.35f) Put(o, FxShape.Dot, FxRamp.Dust, 2, 3, 5 + 5 * roll / 0.35f, 4 - 8 * roll, 1 - roll / 0.35f);
                    // What lies in wait shows the tip of itself now and then.
                    if (!c.Armed && SkillFx.Frac(t * 0.33f + (c.Seed & 15) / 16f) < 0.12f) Put(o, FxShape.Shard, c.Ramp, 6, 0, 0, 8, 1, 80);
                    if (c.Armed)
                    {
                        float back = c.Dir * Deg + 180;
                        for (int i = 0; i < 3; i++)
                        {
                            float u = SkillFx.Frac(t * 1.6f + i / 3f);
                            Put(o, FxShape.Broken, FxRamp.Dust, SkillFxArt.Even(8 + 10 * u), SkillFxArt.BrokenFrame(1, 4, i), SkillFx.Cos(back) * (4 + 10 * u), SkillFx.Sin(back) * (4 + 10 * u), 0.8f * (1 - u), 0, true);
                        }
                    }
                    break;
                }
                case MobFx.Link:
                    // Shoulder to shoulder: a thin red line between the two, and a spark at each end.
                    Put(o, FxShape.Tether, FxRamp.Danger, SkillFxArt.Step4(c.Size), (int)(t * 6) & 3, SkillFx.Cos(c.Dir * Deg) * c.Size / 2, Body - 3 + SkillFx.Sin(c.Dir * Deg) * c.Size / 2, 0.55f,
                        c.Dir * Deg);
                    Put(o, FxShape.Dot, FxRamp.Danger, 2, 2, 0, Body - 3, 0.8f);
                    break;
                case MobFx.Wake:
                {
                    // Three afterimages strung out behind it, each fainter: what its kind is made of.
                    float back = c.Dir * Deg + 180;
                    for (int i = 1; i <= 3; i++)
                    {
                        float x = SkillFx.Cos(back) * i * 7, y = Body + SkillFx.Sin(back) * i * 7;
                        if (c.Look == MobLook.Crystal) Put(o, FxShape.Shard, FxRamp.Steel, 7 - i, 0, x, y, 1 - i * 0.28f, c.Dir * Deg);
                        else if (c.Look == MobLook.Ember) Put(o, FxShape.Flame, FxRamp.Fire, 8 - i, (int)(t * 12 + i) & 3, x, y - 2, 1 - i * 0.28f);
                        else Put(o, FxShape.Dot, c.Ramp, 4 - i, 2, x, y, 1 - i * 0.28f, 45);
                        Put(o, FxShape.Streak, c.Ramp, 6, 1, x, y, 0.6f - i * 0.15f, c.Dir * Deg);
                    }
                    break;
                }
                case MobFx.Poisoned:
                    // Green drops welling up off whoever is poisoned.
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.Frac(t * 1.1f + i / 3f);
                        Put(o, FxShape.Dot, FxRamp.Heal, 2, u > 0.6f ? 2 : 1, (i - 1) * 5 + (float)Math.Sin(t * 5 + i) * 1.5f, 4 + 12 * u, 1 - u);
                    }
                    Put(o, FxShape.Broken, FxRamp.Heal, 14, SkillFxArt.BrokenFrame(1, 4, (int)(t * 4)), 0, 0, 0.5f, 0, true);
                    break;
            }
            // Greys take the monster's own colour.
            for (int i = from; c.Tint != 0 && i < o.Count; i++)
            {
                if (o[i].Ramp != FxRamp.Plain) continue;
                var stamp = o[i];
                stamp.Tint = c.Tint;
                o[i] = stamp;
            }
            return t < life;
        }

        static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false) =>
            SkillFx.Put(o, shape, ramp, size, frame, x, y, alpha, angle, ground);

        static bool Dash(MonsterAttackShape shape) => MonsterAttackSheet.TellOf(shape) == Core.Tell.Dash;

        // ---- The warning on the ground ----

        static void Mark(in MobContext c, float t, List<FxStamp> o)
        {
            // A path or a place to come out at is yellow; anything that will hurt is red.
            var ramp = Dash(c.Shape) ? FxRamp.Bolt : FxRamp.Danger;
            float p = Math.Clamp(c.Progress, 0, 1);
            // The rim beats faster as the blow nears.
            float beat = 0.72f + 0.28f * (float)Math.Sin(t * (8 + 26 * p));
            float dir = c.Dir * Deg;
            switch (c.Area)
            {
                case HazardShape.Cone:
                {
                    float half = c.Width / 2;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float a = dir + half * side;
                        Put(o, FxShape.Tether, ramp, SkillFxArt.Step4(c.Size), 0, SkillFx.Cos(a) * c.Size / 2, SkillFx.Sin(a) * c.Size / 2, beat, a, true);
                    }
                    int dots = Math.Clamp((int)(c.Width / 12), 3, 12);
                    for (int i = 0; i <= dots; i++)
                    {
                        float a = dir - half + c.Width * i / dots;
                        Put(o, FxShape.Dot, ramp, 2, 2, SkillFx.Cos(a) * c.Size, SkillFx.Sin(a) * c.Size, beat, 0, true);
                        // The inside fills outward, faintly.
                        if (i % 2 == 0 && p > 0.1f) Put(o, FxShape.Dot, ramp, 2, 1, SkillFx.Cos(a) * c.Size * p, SkillFx.Sin(a) * c.Size * p, 0.5f, 0, true);
                    }
                    break;
                }
                case HazardShape.Line:
                {
                    float side = c.Width / 2;
                    int frame = Dash(c.Shape) ? (int)(t * 16) & 3 : 0;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = SkillFx.Cos(dir) * c.Size / 2 - SkillFx.Sin(dir) * side * s, y = SkillFx.Sin(dir) * c.Size / 2 + SkillFx.Cos(dir) * side * s;
                        Put(o, FxShape.Tether, ramp, SkillFxArt.Step4(c.Size), frame, x, y, beat, dir, true);
                    }
                    for (int i = -1; i <= 1; i++)
                        Put(o, FxShape.Dot, ramp, 2, 2, SkillFx.Cos(dir) * c.Size - SkillFx.Sin(dir) * side * i, SkillFx.Sin(dir) * c.Size + SkillFx.Cos(dir) * side * i, beat, 0, true);
                    // A thin line runs out along it: the aim of a beam, the way of a rush.
                    float reach = Math.Max(4, c.Size * (Dash(c.Shape) ? 1 : p));
                    Put(o, FxShape.Streak, ramp, SkillFxArt.Step4(reach), 1, SkillFx.Cos(dir) * reach / 2, SkillFx.Sin(dir) * reach / 2, 0.5f, dir, true);
                    break;
                }
                case HazardShape.Ring:
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Size), 1, 0, 0, beat, 0, true);
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Width), 1, 0, 0, beat, 0, true);
                    Put(o, FxShape.Broken, ramp, SkillFxArt.Even(2 * (c.Width + (c.Size - c.Width) * p)), SkillFxArt.BrokenFrame(1, 6, (int)(t * 6)), 0, 0, 0.5f, 0, true);
                    break;
                default:
                    if (c.Shape == MonsterAttackShape.Blink)
                    {
                        // Where it will come out: a turning yellow ring and a wisp of what it is made of.
                        Put(o, FxShape.Broken, FxRamp.Bolt, SkillFxArt.Even(2 * c.Size), SkillFxArt.BrokenFrame(1, 5, (int)(t * 14)), 0, 0, beat, 0, true);
                        Put(o, FxShape.Smoke, c.Ramp, 8, 3 - Math.Min(3, (int)(p * 4)), 0, Body, 0.5f * p);
                        break;
                    }
                    // A trap not yet armed is drawn faintly, rim and all (it does nothing yet if stepped on).
                    bool waiting = c.Area == HazardShape.Mine && !c.Armed;
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Size), 1, 0, 0, waiting ? 0.45f : beat, 0, true);
                    if (c.Harmless)
                    {
                        // The flash of the fallen before its shards.
                        if (SkillFx.Frac(t * (6 + 10 * p)) < 0.5f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 1, 0, 3);
                        break;
                    }
                    if (p > 0.08f && !waiting) Put(o, FxShape.Broken, ramp, SkillFxArt.Even(2 * c.Size * p), SkillFxArt.BrokenFrame(1, 6, (int)(t * 6)), 0, 0, 0.5f, 0, true);
                    switch (c.Shape)
                    {
                        case MonsterAttackShape.Lob:
                        case MonsterAttackShape.Delayed:
                        case MonsterAttackShape.Leap:
                            // What is coming down throws a growing shadow, and a thrown thing is seen falling at the end.
                            Put(o, FxShape.Shadow, FxRamp.Smoke, Math.Max(4, (int)(c.Size * 1.3f * (0.3f + 0.7f * p))), 0, 0, 0, 0.7f, 0, true);
                            if (c.Shape != MonsterAttackShape.Leap && p > 0.55f)
                            {
                                float u = (p - 0.55f) / 0.45f;
                                Put(o, FxShape.Shard, c.Ramp, 9, 1, 0, 60 * (1 - u * u) + 4, 1, -90);
                            }
                            break;
                        case MonsterAttackShape.Burrow:
                        case MonsterAttackShape.Ambush:
                            for (int i = 0; i < 4; i++)
                                Put(o, FxShape.Chunk, FxRamp.Dust, 3, i, (SkillFx.Rnd(c.Seed, i, 1) - 0.5f) * c.Size, (SkillFx.Rnd(c.Seed, i, 2) - 0.5f) * c.Size * 0.6f + (float)Math.Abs(Math.Sin(t * 14 + i)) * 3, 0.9f);
                            break;
                    }
                    if (c.Area == HazardShape.Pull)
                    {
                        // Motes drawn round and in toward the middle.
                        for (int i = 0; i < 8; i++)
                        {
                            float u = SkillFx.Frac(t * 0.6f + i / 8f);
                            float a = i * 45 + t * 150;
                            Put(o, FxShape.Dot, c.Ramp, 2, u > 0.6f ? 3 : 2, SkillFx.Cos(a) * c.Size * (1 - u), SkillFx.Sin(a) * c.Size * (1 - u), 0.4f + 0.6f * u, 0, true);
                        }
                        Put(o, FxShape.Glint, c.Ramp, 7, (int)(t * 8) & 1, 0, 0, 0.5f + 0.5f * p);
                    }
                    else if (c.Area == HazardShape.Mine)
                    {
                        // A rune drawn on the floor: faint until it is armed.
                        Put(o, FxShape.Rune, FxRamp.Danger, SkillFxArt.Even(2 * c.Size * 0.8f), c.Armed ? (int)(t * 3) & 3 : 0, 0, 0, c.Armed ? 0.9f : 0.35f, 0, true);
                    }
                    break;
            }
        }

        // ---- The monster's own cue while it winds up ----

        // The kind's own touch on its wind-up, a piece or three: what it is made of gathering about it.
        static void TellLook(in MobContext c, float t, float p, List<FxStamp> o)
        {
            switch (c.Look)
            {
                case MobLook.Spore:
                    // Its cap swells and lets motes go.
                    SkillFx.Float(o, FxShape.Dot, c.Ramp, 2, 2, 3, SkillFx.Frac(t * 1.4f), 0, 1, 7, 9, c.Seed + 3, cy: Body + 3);
                    break;
                case MobLook.Crystal:
                    for (int i = 0; i < 2; i++)
                        if (SkillFx.Frac(t * 5 + i * 0.5f) < 0.45f) Put(o, FxShape.Glint, FxRamp.Steel, 5, 0, SkillFx.Cos(t * 240 + i * 180) * 8, Body + SkillFx.Sin(t * 240 + i * 180) * 5);
                    break;
                case MobLook.Frost:
                    for (int i = 0; i < 2; i++)
                        Put(o, FxShape.Shard, FxRamp.Cold, 4 + (int)(3 * p), 0, (i == 0 ? -7 : 7), 2, 0.8f, 90, true);
                    break;
                case MobLook.Ember:
                    // It glows hotter and sheds sparks.
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Fire, 1, 3, 3, SkillFx.Frac(t * 1.8f), 0, 1, 6, 12, c.Seed + 5, cy: Body);
                    Put(o, FxShape.Flame, FxRamp.Fire, 5 + (int)(4 * p), (int)(t * 10) & 3, 0, Body + 5, 0.9f);
                    break;
                case MobLook.Sand:
                    for (int i = -1; i <= 1; i += 2)
                        Put(o, FxShape.Chunk, FxRamp.Dust, 2, i > 0 ? 1 : 0, i * (6 + 2 * (float)Math.Sin(t * 20 + i)), 1 + (float)Math.Abs(Math.Sin(t * 16 + i)) * 2);
                    break;
                case MobLook.Water:
                    // Drops drawn up off the floor to it.
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.Frac(t * 1.6f + i / 3f);
                        Put(o, FxShape.Dot, FxRamp.Spirit, 2, 2 + (i & 1), (i - 1) * 8 * (1 - u), 1 + (Body + 4) * u, 0.5f + 0.5f * u);
                    }
                    break;
                case MobLook.Shadow:
                    Put(o, FxShape.Smoke, FxRamp.Violet, 9, 1 + ((int)(t * 6) & 1), 0, Body - 2, 0.45f);
                    if (SkillFx.Frac(t * 4) < 0.5f) Put(o, FxShape.Glint, FxRamp.Violet, 5, 1, SkillFx.Cos(t * 300) * 7, Body + SkillFx.Sin(t * 300) * 6);
                    break;
                case MobLook.Iron:
                    if (p > 0.6f) Put(o, FxShape.Glint, FxRamp.Steel, 7, 2, SkillFx.Cos(c.Dir * Deg) * 9, Body + SkillFx.Sin(c.Dir * Deg) * 9);
                    break;
                case MobLook.Hex:
                    Put(o, FxShape.Rune, FxRamp.Violet, 14, (int)(t * 4) & 3, 0, 0, 0.35f + 0.4f * p, 0, true);
                    Put(o, FxShape.Flame, FxRamp.Violet, 5, (int)(t * 10) & 3, 0, Body + 9, 0.9f);
                    break;
            }
        }

        // The kind's own touch where its blow landed: at most five pieces, after the shape's own.
        static void LandLook(in MobContext c, float t, float r, List<FxStamp> o)
        {
            float dir = c.Dir * Deg;
            bool ahead = c.Area == HazardShape.Cone || c.Area == HazardShape.Line;
            // Out along a wedge or a line, in the middle of a circle.
            float cx = ahead ? SkillFx.Cos(dir) * r * 0.55f : 0, cy = ahead ? SkillFx.Sin(dir) * r * 0.55f : 0;
            float reach = ahead ? r * 0.45f : r * 0.8f;
            switch (c.Look)
            {
                case MobLook.Spore:
                    // A puff of spores that hangs and drifts up after the blow is over.
                    SkillFx.Float(o, FxShape.Dot, c.Ramp, 2, 2, 4, t, 0.04f, 0.45f, reach, 9, c.Seed + 11, cx: cx, cy: cy);
                    Put(o, FxShape.Smoke, c.Ramp, SkillFxArt.Even(Math.Min(22, reach * 1.2f)), Math.Min(3, (int)(t / 0.4f * 4)), cx, cy + 2, 0.4f * (1 - t / 0.45f));
                    break;
                case MobLook.Crystal:
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 11, 2, cx, cy + 3);
                    SkillFx.Fly(o, FxShape.Shard, FxRamp.Steel, 5, 0, 4, t, 0.02f, 0.3f, 3, reach * 1.2f, c.Seed + 11, hop: 5, cx: cx, cy: cy, aimed: true);
                    break;
                case MobLook.Frost:
                    SkillFx.Fly(o, FxShape.Shard, FxRamp.Cold, 5, 0, 4, t, 0.02f, 0.3f, 3, reach * 1.1f, c.Seed + 11, hop: 4, cx: cx, cy: cy, aimed: true);
                    break;
                case MobLook.Ember:
                    for (int i = 0; i < 3; i++)
                        Put(o, FxShape.Flame, FxRamp.Fire, 7 + 2 * (i & 1), (int)(t * 12 + i) & 3, cx + SkillFx.Cos(i * 120 + 40) * reach * 0.5f, cy + SkillFx.Sin(i * 120 + 40) * reach * 0.4f + 3, 1 - t / 0.4f);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Fire, 1, 3, 2, t, 0.05f, 0.4f, reach * 0.6f, 12, c.Seed + 11, cx: cx, cy: cy);
                    break;
                case MobLook.Sand:
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 4, t, 0, 0.32f, 3, reach * 1.1f, c.Seed + 11, hop: 8, cx: cx, cy: cy);
                    // A sting leaves green drops.
                    if (c.Shape == MonsterAttackShape.Thrust)
                        SkillFx.Fly(o, FxShape.Dot, FxRamp.Heal, 2, 2, 3, t, 0.04f, 0.3f, 2, 8, c.Seed + 13, hop: 5, cx: SkillFx.Cos(dir) * r * 0.9f, cy: SkillFx.Sin(dir) * r * 0.9f);
                    break;
                case MobLook.Water:
                    SkillFx.Fly(o, FxShape.Dot, FxRamp.Spirit, 2, 3, 4, t, 0.02f, 0.32f, 3, reach * 1.1f, c.Seed + 11, hop: 10, cx: cx, cy: cy);
                    Put(o, FxShape.Streak, FxRamp.Spirit, SkillFxArt.Step4(8 + 6 * SkillFx.Out(SkillFx.U(t, 0, 0.1f))), 2, cx, cy + 6, t < 0.12f ? 0.9f : 0.9f * (1 - (t - 0.12f) / 0.2f), 90);
                    break;
                case MobLook.Shadow:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = dir + (i - 1) * 40, u = SkillFx.U(t, 0.03f * i, 0.3f);
                        if (u > 0 && u < 1 && SkillFx.Frac(t * 14 + i * 0.3f) < 0.6f)
                            Put(o, FxShape.Glint, FxRamp.Violet, 5, 1, cx + SkillFx.Cos(a) * reach * u, cy + SkillFx.Sin(a) * reach * u + 4 * u, 1 - u);
                    }
                    Put(o, FxShape.Smoke, FxRamp.Smoke, SkillFxArt.Even(Math.Min(20, reach)), Math.Min(3, (int)(t / 0.35f * 4)), cx, cy + 2, 0.5f * (1 - t / 0.4f));
                    break;
                case MobLook.Iron:
                    if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 11, 2, SkillFx.Cos(dir) * r, SkillFx.Sin(dir) * r);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Steel, 4, 1, 4, t, 0, 0.2f, 3, 10, c.Seed + 11, cx: SkillFx.Cos(dir) * r, cy: SkillFx.Sin(dir) * r, aimed: true, cone: 140, toward: dir + 180);
                    break;
                case MobLook.Hex:
                    for (int i = 0; i < 3; i++)
                        Put(o, FxShape.Flame, FxRamp.Violet, 8 + 2 * (i & 1), (int)(t * 12 + i) & 3, cx + SkillFx.Cos(i * 120 + 10) * reach * 0.5f, cy + SkillFx.Sin(i * 120 + 10) * reach * 0.4f + 3, 1 - t / 0.4f);
                    Put(o, FxShape.Rune, FxRamp.Violet, SkillFxArt.Even(Math.Min(24, reach * 1.6f)), (int)(t * 8) & 3, cx, cy, 0.7f * (1 - t / 0.4f), 0, true);
                    break;
            }
        }

        static void Tell(in MobContext c, float t, List<FxStamp> o)
        {
            float p = c.Life > 0 ? Math.Clamp(t / c.Life, 0, 1) : 1;
            float dir = c.Dir * Deg;
            TellLook(c, t, p, o);
            switch (MonsterAttackSheet.TellOf(c.Shape))
            {
                case Core.Tell.Dash:
                    if (c.Shape == MonsterAttackShape.Blink)
                    {
                        Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(22 * (1 - p) + 6), SkillFxArt.BrokenFrame(1, 5, (int)(t * 20)), 0, Body, 0.9f);
                        Put(o, FxShape.Smoke, c.Ramp, 10, Math.Min(3, (int)(p * 4)), 0, Body, 0.6f);
                        break;
                    }
                    // Dust kicked up behind it as it paws the ground.
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.Frac(t * 3 + i / 3f);
                        float a = dir + 180 + (i - 1) * 25;
                        Put(o, FxShape.Smoke, FxRamp.Dust, 6, Math.Min(3, (int)(u * 4)), SkillFx.Cos(a) * (5 + 8 * u), 1 + SkillFx.Sin(a) * (5 + 8 * u) + 3 * u, 0.8f * (1 - u));
                    }
                    break;
                case Core.Tell.Barrage:
                    // A quick flicker where the shot will leave from.
                    if (Math.Sin(t * 40) > 0) Put(o, FxShape.Glint, FxRamp.Steel, 7 + (int)(4 * p), 1, SkillFx.Cos(dir) * 7, Body + SkillFx.Sin(dir) * 7);
                    for (int i = 0; i < 2; i++)
                    {
                        float a = t * 500 + i * 180;
                        Put(o, FxShape.Dot, c.Ramp, 2, 3, SkillFx.Cos(dir) * 7 + SkillFx.Cos(a) * 5 * (1 - p), Body + SkillFx.Sin(dir) * 7 + SkillFx.Sin(a) * 5 * (1 - p));
                    }
                    break;
                case Core.Tell.Call:
                    Put(o, FxShape.Broken, FxRamp.Violet, 22, SkillFxArt.BrokenFrame(2, 5, (int)(t * 8)), 0, 0, 0.9f, 0, true);
                    Put(o, FxShape.Rune, FxRamp.Violet, 16, (int)(t * 3) & 3, 0, 0, 0.3f + 0.5f * p, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, 4, SkillFx.Frac(t * 1.5f), 0, 1, 9, 14, c.Seed, cy: 3);
                    break;
                default:
                    switch (c.Shape)
                    {
                        case MonsterAttackShape.Leap:
                            // It gathers itself: dust at its feet, and off the ground as it goes up.
                            for (int i = -1; i <= 1; i += 2)
                                Put(o, FxShape.Smoke, FxRamp.Dust, 6, Math.Min(3, (int)(p * 4)), i * (5 + 3 * p), 1, 0.7f);
                            break;
                        case MonsterAttackShape.Strike:
                        case MonsterAttackShape.Sweep:
                        case MonsterAttackShape.Thrust:
                        case MonsterAttackShape.Beam:
                            // A glint where the blow will come from, growing.
                            Put(o, FxShape.Glint, c.Shape == MonsterAttackShape.Beam ? FxRamp.Steel : c.Ramp, 5 + 2 * (int)(3 * p), p > 0.7f ? 2 : 1,
                                SkillFx.Cos(dir) * 6, Body + SkillFx.Sin(dir) * 6, 0.6f + 0.4f * p);
                            break;
                        case MonsterAttackShape.Lob:
                        case MonsterAttackShape.Delayed:
                        case MonsterAttackShape.Mine:
                        case MonsterAttackShape.Push:
                            // What it will throw gathers over its head.
                            Put(o, FxShape.Blob, c.Ramp, 4 + 2 * (int)(3 * p), 1, 0, 14 + 2 * p);
                            SkillFx.Gather(o, c.Ramp, SkillFx.Frac(t * 2.5f) * 0.4f, 0.4f, 12, 4, c.Seed, 0, 14);
                            break;
                        case MonsterAttackShape.Burrow:
                        case MonsterAttackShape.Ambush:
                            SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 6, t, 0, 0.3f, 2, 10, c.Seed, hop: 7);
                            break;
                        default:
                            // A ring of itself, a pull, a blast: lines drawn in to its middle, again and again.
                            SkillFx.Gather(o, c.Ramp, SkillFx.Frac(t * 2.2f) * 0.45f, 0.45f, Math.Min(c.Size, 30), 7, c.Seed, 0, Body);
                            if (c.Shape == MonsterAttackShape.Explode && SkillFx.Frac(t * (4 + 10 * p)) < 0.5f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 1, 0, Body);
                            break;
                    }
                    break;
            }
        }

        // ---- Where it landed ----

        static float Land(in MobContext c, float t, List<FxStamp> o)
        {
            float dir = c.Dir * Deg;
            float r = c.Size;
            switch (c.Shape)
            {
                case MonsterAttackShape.Strike:
                case MonsterAttackShape.Sweep:
                {
                    // The cut itself, then sparks off it.
                    float life = 0.3f;
                    int span = (int)Math.Clamp(c.Width, 40, 170);
                    Put(o, FxShape.Crescent, c.Ramp, (int)Math.Max(6, r * 0.8f), span, 0, 0, t < 0.08f ? 1 : 1 - (t - 0.08f) / 0.2f, dir);
                    SkillFx.Fly(o, FxShape.Streak, c.Ramp, 4, 1, c.Shape == MonsterAttackShape.Sweep ? 6 : 3, t, 0.02f, 0.24f, r * 0.6f, r * 1.1f, c.Seed, aimed: true, cone: c.Width * 0.8f, toward: dir);
                    if (c.Shape == MonsterAttackShape.Sweep) SkillFx.Float(o, FxShape.Smoke, FxRamp.Dust, 6, 2, 2, t, 0.08f, life, r * 0.5f, 5, c.Seed, cx: SkillFx.Cos(dir) * r * 0.5f, cy: SkillFx.Sin(dir) * r * 0.5f);
                    return life;
                }
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                case MonsterAttackShape.Charge:
                case MonsterAttackShape.Swoop:
                {
                    // A streak the whole way, bright for a moment; a beam has a white heart and leaves dust falling.
                    float life = 0.35f;
                    float alpha = t < 0.1f ? 1 : 1 - (t - 0.1f) / 0.18f;
                    float mx = SkillFx.Cos(dir) * r / 2, my = SkillFx.Sin(dir) * r / 2;
                    Put(o, FxShape.Streak, c.Ramp, SkillFxArt.Step4(r), 3, mx, my, alpha, dir);
                    if (c.Shape == MonsterAttackShape.Beam) Put(o, FxShape.Streak, FxRamp.Steel, SkillFxArt.Step4(r), 2, mx, my, alpha, dir);
                    for (int i = 0; i < 5; i++)
                    {
                        float along = r * (i + 0.5f) / 5, u = SkillFx.U(t, 0.06f + 0.02f * i, life);
                        if (u <= 0 || u >= 1) continue;
                        Put(o, FxShape.Dot, c.Ramp, 2, 2, SkillFx.Cos(dir) * along + (SkillFx.Rnd(c.Seed, i, 1) - 0.5f) * 6, SkillFx.Sin(dir) * along - 5 * u + (SkillFx.Rnd(c.Seed, i, 2) - 0.5f) * 6, 1 - u);
                    }
                    return life;
                }
                case MonsterAttackShape.Leap:
                {
                    // It comes down: a flash of itself, drops flung up and out, a ring, a puddle that sinks away.
                    float life = 0.4f;
                    if (t < 0.09f) Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(r * 1.2f), t < 0.04f ? 0 : 1, 0, 2);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 2, 6, t, 0.02f, 0.3f, 3, r, c.Seed, hop: 8);
                    Put(o, FxShape.Smoke, c.Ramp, SkillFxArt.Even(r * 0.9f), Math.Min(3, (int)(t / life * 4)), 0, 0, 0.5f * (1 - t / life), 0, true);
                    return life;
                }
                case MonsterAttackShape.Nova:
                {
                    // Spikes stand up all round the rim, then mist.
                    float life = 0.45f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45 + 22;
                        float rise = SkillFx.Out(SkillFx.U(t, 0, 0.1f));
                        Put(o, FxShape.Shard, c.Ramp, 6 + (int)(5 * rise), 0, SkillFx.Cos(a) * r * 0.86f, SkillFx.Sin(a) * r * 0.86f, t < 0.2f ? 1 : 1 - (t - 0.2f) / 0.2f, a);
                    }
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 13, 2, 0, Body);
                    SkillFx.Float(o, FxShape.Smoke, c.Ramp == FxRamp.Cold ? FxRamp.Mist : FxRamp.Smoke, 8, 2, 5, t, 0.1f, life, r * 0.7f, 5, c.Seed);
                    return life;
                }
                case MonsterAttackShape.Pull:
                {
                    // It caves in on itself: the rim rushes to the middle and a point of light goes out.
                    float life = 0.35f;
                    float u = SkillFx.U(t, 0, 0.16f);
                    if (u < 1) Put(o, FxShape.Edge, c.Ramp, SkillFxArt.Even(2 * r * (1 - u) + 4), 2, 0, 0, 1, 0, true);
                    if (t > 0.1f && t < 0.2f) Put(o, FxShape.Glint, FxRamp.Steel, 15, 2, 0, 2);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45 + c.Seed % 45;
                        Put(o, FxShape.Streak, c.Ramp, 6, 1, SkillFx.Cos(a) * r * (1 - u) * 0.9f, SkillFx.Sin(a) * r * (1 - u) * 0.9f, 1 - u, a + 180);
                    }
                    SkillFx.Float(o, FxShape.Dot, c.Ramp, 2, 2, 6, t, 0.16f, life, 8, 10, c.Seed);
                    return life;
                }
                case MonsterAttackShape.Push:
                {
                    // A column thrown up, and spray.
                    float life = 0.35f;
                    for (int i = -1; i <= 1; i++)
                        Put(o, FxShape.Streak, c.Ramp, SkillFxArt.Step4(10 + 8 * SkillFx.Out(SkillFx.U(t, 0, 0.1f))), 2, i * r * 0.4f, 8, t < 0.12f ? 1 : 1 - (t - 0.12f) / 0.2f, 90);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 1, ground: true);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 3, 6, t, 0.03f, 0.3f, 3, r, c.Seed, hop: 9);
                    return life;
                }
                case MonsterAttackShape.Explode:
                {
                    // A white heart for a moment, then the ball of fire, balls of it thrown out, and smoke.
                    float life = 0.45f;
                    if (t < 0.06f) Put(o, FxShape.Glint, FxRamp.Steel, 17, 2, 0, Body);
                    Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(Math.Min(48, r * 1.7f * (0.5f + 0.5f * SkillFx.Out(SkillFx.U(t, 0, 0.1f))))), Math.Min(5, (int)(t / 0.32f * 6)), 0, Body,
                        t < 0.3f ? 1 : 1 - (t - 0.3f) / 0.15f);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 3, ground: true);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72 + c.Seed % 72, u = SkillFx.U(t, 0.03f, 0.3f);
                        if (u > 0 && u < 1) Put(o, FxShape.Fireball, c.Ramp, 7, (int)(t * 14 + i) % 3, SkillFx.Cos(a) * r * SkillFx.Out(u), Body + SkillFx.Sin(a) * r * 0.8f * SkillFx.Out(u), 1 - u * u, a);
                    }
                    Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Even(r * 1.3f), 0, 0, 0, 0.6f * (1 - t / life), 0, true);
                    SkillFx.Float(o, FxShape.Smoke, FxRamp.Smoke, 9, 1, 4, t, 0.14f, life, r * 0.6f, 9, c.Seed);
                    return life;
                }
                case MonsterAttackShape.Slam:
                case MonsterAttackShape.Burrow:
                case MonsterAttackShape.Ambush:
                {
                    // The ground takes it: a ring, cracks, rubble thrown up.
                    float life = 0.4f;
                    SkillFx.Ring(o, FxRamp.Dust, t, 0, r, 2, ground: true);
                    for (int i = 0; i < 3; i++)
                        Put(o, FxShape.Crack, FxRamp.Smoke, SkillFxArt.Step4(r * 0.7f), i, SkillFx.Cos(i * 120 + 30) * r * 0.35f, SkillFx.Sin(i * 120 + 30) * r * 0.35f, 1 - t / life, i * 120 + 30, true);
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 7, t, 0, 0.32f, 3, r, c.Seed, hop: 9);
                    if (t < 0.08f) Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(r * 0.9f), 0, 0, 3);
                    return life;
                }
                default:
                {
                    // Something thrown or left has gone off: a ball from a bright heart to dark scraps, chips, smoke.
                    float life = 0.42f;
                    Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(Math.Min(44, r * 1.5f)), Math.Min(5, (int)(t / 0.3f * 6)), 0, 3, t < 0.3f ? 1 : 1 - (t - 0.3f) / 0.12f);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    SkillFx.Fly(o, c.Ramp == FxRamp.Cold ? FxShape.Shard : FxShape.Dot, c.Ramp, c.Ramp == FxRamp.Cold ? 5 : 2, c.Ramp == FxRamp.Cold ? 0 : 2, 6, t, 0.02f, 0.3f, 3, r * 1.1f, c.Seed, hop: 6, aimed: c.Ramp == FxRamp.Cold);
                    if (c.Shape == MonsterAttackShape.Mine)
                    {
                        for (int i = 0; i < 4; i++)
                            Put(o, FxShape.Flame, c.Ramp, 8 + 2 * (i & 1), (int)(t * 12 + i) & 3, SkillFx.Cos(i * 90 + 45) * r * 0.45f, SkillFx.Sin(i * 90 + 45) * r * 0.45f + 4, 1 - t / life);
                    }
                    SkillFx.Float(o, FxShape.Smoke, c.Ramp == FxRamp.Cold ? FxRamp.Mist : FxRamp.Smoke, 8, 2, 3, t, 0.12f, life, r * 0.5f, 6, c.Seed);
                    return life;
                }
            }
        }

        // ---- What lies on the floor ----

        static void Pool(in MobContext c, float t, List<FxStamp> o)
        {
            float fade = Math.Clamp(c.Progress, 0, 1);
            float r = c.Size;
            // An enemy's, so it wears the thin red rim whatever it is made of; a cloud that only fills monsters does not.
            if (c.Area != HazardShape.Balm) Put(o, FxShape.Edge, FxRamp.Danger, SkillFxArt.Even(2 * r), 1, 0, 0, 0.75f * fade, 0, true);
            int count = Math.Clamp((int)(r / 4) + 2, 3, 7);
            switch (c.Area)
            {
                case HazardShape.Fire:
                    // Low flames that flicker, and an ember or two going up.
                    for (int i = 0; i < count; i++)
                    {
                        float a = SkillFx.Rnd(c.Seed, i, 1) * 360, d = r * 0.7f * (float)Math.Sqrt(SkillFx.Rnd(c.Seed, i, 2));
                        Put(o, FxShape.Flame, FxRamp.Fire, 6 + 2 * (i & 1), (int)(t * 9 + i * 1.7f) & 3, SkillFx.Cos(a) * d, SkillFx.Sin(a) * d * 0.7f + 3, fade);
                    }
                    Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Even(r * 1.4f), 0, 0, 0, 0.5f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Fire, 1, 3, 3, SkillFx.Frac(t * 0.9f), 0, 1, r * 0.6f, 12, c.Seed, alpha: fade);
                    break;
                case HazardShape.Ice:
                    // Crystals lying where they froze, one glinting now and then.
                    for (int i = 0; i < count; i++)
                    {
                        float a = SkillFx.Rnd(c.Seed, i, 3) * 360, d = r * 0.72f * (float)Math.Sqrt(SkillFx.Rnd(c.Seed, i, 4));
                        Put(o, FxShape.Shard, FxRamp.Cold, 5 + 2 * (i % 3), 0, SkillFx.Cos(a) * d, SkillFx.Sin(a) * d * 0.7f, 0.9f * fade, a * 3, true);
                    }
                    if (SkillFx.Frac(t * 0.8f) < 0.25f) Put(o, FxShape.Glint, FxRamp.Cold, 7, 1, SkillFx.Cos(t * 40) * r * 0.3f, 2, fade);
                    break;
                case HazardShape.Spore:
                    // Motes drifting up out of it.
                    Put(o, FxShape.Smoke, FxRamp.Violet, SkillFxArt.Even(r * 1.2f), 2 + ((int)(t * 2) & 1), 0, 1, 0.35f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, count + 2, SkillFx.Frac(t * 0.7f), 0, 1, r * 0.8f, 11, c.Seed, alpha: fade);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 1, 3, 3, SkillFx.Frac(t * 0.7f + 0.5f), 0, 1, r * 0.8f, 13, c.Seed + 7, alpha: fade);
                    break;
                case HazardShape.Sand:
                    // Grains turning round and down to its middle.
                    for (int i = 0; i < count + 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.5f + i / (float)(count + 2));
                        float a = i * 360f / (count + 2) + t * 200;
                        Put(o, FxShape.Dot, FxRamp.Dust, 2, u > 0.5f ? 0 : 2, SkillFx.Cos(a) * r * 0.85f * (1 - u), SkillFx.Sin(a) * r * 0.6f * (1 - u), fade, 0, true);
                    }
                    break;
                case HazardShape.Balm:
                    // A soft green cloud: it harms no one, so no red.
                    Put(o, FxShape.Smoke, FxRamp.Heal, SkillFxArt.Even(r * 1.3f), 2, 0, 2, 0.3f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Heal, 2, 2, count + 2, SkillFx.Frac(t * 0.6f), 0, 1, r * 0.8f, 12, c.Seed, alpha: fade);
                    for (int i = 0; i < 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.8f + i * 0.5f);
                        Put(o, FxShape.Plus, FxRamp.Heal, 3, 0, (i == 0 ? -1 : 1) * r * 0.35f, 4 + 9 * u, fade * (1 - u));
                    }
                    break;
            }
        }

        // ---- A shot: the body's colour, a bright head, a dark rim ----

        static void Shot(in MobContext c, float t, List<FxStamp> o)
        {
            float dir = c.Dir * Deg;
            float bx = -SkillFx.Cos(dir), by = -SkillFx.Sin(dir);
            switch (c.Shape)
            {
                case MonsterAttackShape.Boomerang:
                    // An orb with stars strung out behind it; it blinks where it stands before it comes back.
                    if (c.Armed && SkillFx.Frac(t * 10) < 0.4f) Put(o, FxShape.Glint, c.Ramp, 13, 2, 0, 0);
                    Put(o, FxShape.Dot, c.Ramp, 7, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Blob, c.Ramp, 8, 1, 0, 0);
                    for (int i = 1; i <= 3 && !c.Armed; i++)
                        Put(o, FxShape.Glint, c.Ramp, 5, (i + (int)(t * 12)) & 1, bx * i * 5, by * i * 5, 1 - i * 0.25f);
                    break;
                case MonsterAttackShape.Bounce:
                    // A hard diamond of light.
                    Put(o, FxShape.Dot, c.Ramp, 7, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Dot, c.Ramp, 5, 2, 0, 0, 1, 45);
                    Put(o, FxShape.Glint, FxRamp.Steel, 5, 1, -bx * 2, -by * 2);
                    for (int i = 1; i <= 2; i++) Put(o, FxShape.Dot, c.Ramp, 3 - i, 1, bx * i * 4, by * i * 4, 1 - i * 0.3f, 45);
                    break;
                default:
                    // A thorn or a spat thing: pointed the way it flies.
                    Put(o, FxShape.Dot, c.Ramp, 5, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Shard, c.Ramp, 8, 0, 0, 0, 1, dir);
                    Put(o, FxShape.Dot, c.Ramp, 1, 3, -bx * 3, -by * 3);
                    for (int i = 1; i <= 2; i++) Put(o, FxShape.Dot, c.Ramp, 2, 1, bx * (3 + i * 3), by * (3 + i * 3), 0.7f - i * 0.25f);
                    break;
            }
        }
    }
}
