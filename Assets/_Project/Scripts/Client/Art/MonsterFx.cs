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
        // The effects rework (E5, 2026-11-27). A loop on a body that runs at someone (a charge): dust kicked up behind
        // its feet and lines of speed off its back (Dir the way it runs, Size its body's radius in pixels).
        Rush,
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
        // A thrown thing's mark: where its thrower stands from the mark, in pixels (y up).
        public bool Thrown;
        public float FromX, FromY;
    }

    // Ordinary monsters' warnings and effects as functions of time (2026-11-21, Docs/Combat.md 몬스터의 공격 시트,
    // Docs/SkillEffects.md): like SkillFx it keeps no state and says which pieces are where at a moment, so the game
    // (WorldView.MonsterFx) and the preview (ArtPreview --only monsterfx) draw the same. The rules: three parts
    // (gathering = the warning, the blow at its brightest and shortest, what thins out); the rim at the real reach;
    // no more than Most pieces to an effect. Since the effects rework (E5, Docs/EffectsRework.md 9) a warning's rim
    // and filling are one picture of its shape (HazardArt.Warn, the boss's, fainter inside: Field and Tone say
    // which), a shot is ShotArt's in what the kind is made of (ShotOf), what lies on the floor is a pool with a
    // dotted red rim, and a thrown thing bursts as its element does (FxLayers).
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

        // What a kind's shot is made of (ShotArt, E2): by what the kind is made of and its colours. The plain kinds'
        // is the ember every other enemy's is.
        public static ShotElement ShotOf(MobLook look, FxRamp ramp)
        {
            switch (look)
            {
                case MobLook.Spore: return ramp == FxRamp.Spirit ? ShotElement.Mote : ShotElement.Spore;
                case MobLook.Crystal: return ShotElement.Crystal;
                case MobLook.Frost: return ShotElement.Frost;
                case MobLook.Ember: return ShotElement.Fire;
                case MobLook.Sand: return ShotElement.Sand;
                case MobLook.Water: return ShotElement.Water;
                case MobLook.Shadow: return ShotElement.Void;
                case MobLook.Iron: return ShotElement.Iron;
                case MobLook.Hex: return ShotElement.Hex;
            }
            return ramp == FxRamp.Cold ? ShotElement.Frost : ramp == FxRamp.Fire ? ShotElement.Fire : ramp == FxRamp.Violet ? ShotElement.Void
                : ramp == FxRamp.Spirit ? ShotElement.Water : ramp == FxRamp.Dust ? ShotElement.Sand : ShotElement.Ember;
        }

        // A kind made of nothing in particular shoots in its own colour (F2): its tint, or 0 for a kind whose shot
        // is an element's.
        public static uint ShotTint(MobLook look, FxRamp ramp, uint tint) => look == MobLook.Plain && ramp == FxRamp.Plain ? tint : 0;

        // A mark's picture (E5): its shape at the size it is judged at, on the pixel grid (sizes in pixels as the
        // context has them, a cone's width in degrees).
        public static HazardField Field(in MobContext c)
        {
            float tile = TileArt.Size;
            switch (c.Area)
            {
                case HazardShape.Cone: return HazardArt.Field(HazardForm.Cone, c.Size / tile, c.Width, c.Dir);
                case HazardShape.Line: return HazardArt.Field(HazardForm.Band, c.Size / tile, c.Width / tile, c.Dir);
                case HazardShape.Ring: return HazardArt.Field(HazardForm.Donut, c.Size / tile, c.Width / tile, c.Dir);
                default: return HazardArt.Field(HazardForm.Circle, c.Size / tile, 0, 0);
            }
        }

        // A path or a place to come out at is yellow; anything that will hurt is red.
        public static WarnTone Tone(in MobContext c) => Dash(c.Shape) ? WarnTone.Rush : WarnTone.Danger;

        // How far a mark's picture has filled, in HazardArt.FillSteps: a trap fills while it is being set and then
        // lies full; the rest fill to the moment they land.
        public static int WarnStep(in MobContext c, float armsAt)
        {
            float p = Math.Clamp(c.Progress, 0, 1);
            if (c.Area == HazardShape.Mine) p = c.Armed ? 1 : armsAt > 0 ? Math.Min(0.8f, p / armsAt * 0.8f) : 0;
            // What harms nothing (where a blink comes out, the flash of the fallen) only shows its rim.
            if (c.Harmless || c.Shape == MonsterAttackShape.Blink) p = 0;
            return Math.Clamp((int)(p * HazardArt.FillSteps), 0, HazardArt.FillSteps);
        }

        // Whether it blinks whole: the last of its time (a trap never: it lies there for long).
        public static bool WarnBlinks(in MobContext c) => c.Area != HazardShape.Mine && !c.Harmless && c.Progress > HazardArt.BlinkFrom;

        // Arrowheads march along a rush's path, this many pixels on (under 0: none).
        public static int WarnArrows(in MobContext c, float t, bool still) =>
            c.Area == HazardShape.Line && Dash(c.Shape) ? (still ? 0 : (int)(t * 12) * 3 % 22) : -1;

        // Whether a blow lands as its warning's own shape burning in its element's colours (F2, as a boss's does, E1):
        // a wedge, a thrust or a beam, a ring. A circle bursts as before (Burst), and a path run down is no blow.
        public static bool ShapeBlow(in MobContext c) =>
            (c.Area == HazardShape.Cone || c.Area == HazardShape.Line || c.Area == HazardShape.Ring) && !Dash(c.Shape);

        // Fainter than a boss's blow, as the warning is.
        public const float BlowStrength = 0.7f;

        // What a kind's blow is made of, as an element's colours: by what the kind is made of, else its own ramp
        // (Plain: its own colour).
        public static FxRamp ElementOf(in MobContext c)
        {
            switch (c.Look)
            {
                case MobLook.Spore: return c.Ramp == FxRamp.Spirit ? FxRamp.Spirit : FxRamp.Violet;
                case MobLook.Crystal: return c.Ramp == FxRamp.Violet ? FxRamp.Violet : FxRamp.Cold;
                case MobLook.Frost: return FxRamp.Cold;
                case MobLook.Ember: return FxRamp.Fire;
                case MobLook.Sand: return FxRamp.Sand;
                case MobLook.Water: return FxRamp.Spirit;
                case MobLook.Shadow:
                case MobLook.Hex: return FxRamp.Violet;
                case MobLook.Iron: return FxRamp.Steel;
            }
            return c.Ramp;
        }

        // The five colours the shape burns in, bright to dark: the element's, or for a kind with none its own colour
        // from pale to deep.
        public static Rgba[] BlowRamp(in MobContext c)
        {
            var element = ElementOf(c);
            if (element != FxRamp.Plain || c.Tint == 0) return SkillFxArt.Ramp5(element);
            return OwnRamp(c.Tint);
        }

        // A kind's own colour as five, pale to deep (its blow, and its shot's four: ShotArt takes the first four).
        public static Rgba[] OwnRamp(uint tint)
        {
            var own = new Rgba((byte)(tint >> 16), (byte)(tint >> 8), (byte)tint);
            return new[] { own.Mix(Rgba.White, 0.8f), own.Mix(Rgba.White, 0.4f), own, own.Scale(0.65f), own.Scale(0.4f) };
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
                    if (ShapeBlow(c))
                    {
                        // The warning's own shape burns under this (BlowRamp, laid by the view); here the blow's
                        // one stroke and what its element does inside the shape.
                        life = ShapeLand(c, t, o);
                        break;
                    }
                    life = Land(c, t, o, out bool whole);
                    if (t < life && !whole) LandLook(c, t, c.Size, o);
                    break;
                case MobFx.Rush: Rush(c, t, o); break;
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
            for (int i = from; i < o.Count; i++)
            {
                var stamp = o[i];
                // Smoke and dust in the air lie under the warnings on the floor, so they never hide one (E5).
                bool low = !stamp.Ground && (stamp.Shape == FxShape.Smoke || stamp.Shape == FxShape.Plume || stamp.Shape == FxShape.Pall);
                // Greys take the monster's own colour.
                bool tinted = c.Tint != 0 && stamp.Ramp == FxRamp.Plain;
                if (!low && !tinted) continue;
                stamp.Low = low;
                if (tinted) stamp.Tint = c.Tint;
                o[i] = stamp;
            }
            return t < life;
        }

        static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false) =>
            SkillFx.Put(o, shape, ramp, size, frame, x, y, alpha, angle, ground);

        static bool Dash(MonsterAttackShape shape) => MonsterAttackSheet.TellOf(shape) == Core.Tell.Dash;

        // ---- The warning on the ground ----

        // What goes with a mark's picture (the rim, the faint inside and the filling are HazardArt.Warn's, laid by the
        // view from Field and Tone): what is coming down or up, the wisp where a blink will come out, a trap's rune.
        static void Mark(in MobContext c, float t, List<FxStamp> o)
        {
            float p = Math.Clamp(c.Progress, 0, 1);
            if (c.Area == HazardShape.Cone || c.Area == HazardShape.Line || c.Area == HazardShape.Ring) return;
            if (c.Shape == MonsterAttackShape.Blink)
            {
                // Where it will come out: a wisp of what it is made of.
                Put(o, FxShape.Smoke, c.Ramp, 8, 3 - Math.Min(3, (int)(p * 4)), 0, Body, 0.5f * p);
                return;
            }
            if (c.Harmless)
            {
                // The flash of the fallen before its shards.
                if (SkillFx.Frac(t * (6 + 10 * p)) < 0.5f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 1, 0, 3);
                return;
            }
            switch (c.Shape)
            {
                case MonsterAttackShape.Lob when c.Thrown:
                {
                    // The thrown thing itself, up and over from its thrower, its shadow sliding along the floor to
                    // where it will fall.
                    float x = c.FromX * (1 - p), y = c.FromY * (1 - p), up = 4 * 30 * p * (1 - p);
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 4 + 2 * (int)(3 * p), 0, x, y, 0.6f, 0, true);
                    for (int k = 2; k >= 1; k--)
                    {
                        float q = Math.Max(0, p - k * 0.04f);
                        Put(o, FxShape.Dot, c.Ramp, 4 - k, 1, c.FromX * (1 - q), c.FromY * (1 - q) + 4 * 30 * q * (1 - q) + Body, 0.65f - k * 0.2f);
                    }
                    Lump(c, o, x, y + up + Body, t, (float)Math.Atan2(-c.FromY + 4 * 30 * (1 - 2 * p), -c.FromX) * Deg);
                    break;
                }
                case MonsterAttackShape.Lob:
                case MonsterAttackShape.Delayed:
                case MonsterAttackShape.Leap:
                    // What is coming down throws a growing shadow, and a thrown thing is seen falling at the end.
                    Put(o, FxShape.Shadow, FxRamp.Smoke, Math.Max(4, (int)(c.Size * 1.3f * (0.3f + 0.7f * p))), 0, 0, 0, 0.7f, 0, true);
                    if (c.Shape != MonsterAttackShape.Leap && p > 0.55f)
                    {
                        float u = (p - 0.55f) / 0.45f;
                        Lump(c, o, 0, 60 * (1 - u * u) + 4, t, -90);
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
        }

        // A thrown thing in the air, as its kind makes it: a shard of crystal or ice, a ball of fire, else a lump
        // with a pale spot.
        static void Lump(in MobContext c, List<FxStamp> o, float x, float y, float t, float angle)
        {
            if (c.Look == MobLook.Crystal || c.Look == MobLook.Frost || c.Ramp == FxRamp.Cold) Put(o, FxShape.Shard, c.Ramp, 9, 1, x, y, 1, angle);
            else if (c.Look == MobLook.Ember || c.Ramp == FxRamp.Fire) Put(o, FxShape.Fireball, FxRamp.Fire, 7, (int)(t * 14) % 3, x, y, 1, angle);
            else
            {
                Put(o, FxShape.Blob, c.Ramp, 8, 1 + ((int)(t * 8) & 1), x, y);
                Put(o, FxShape.Dot, c.Ramp, 2, 3, x - 1, y + 1);
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

        static float Land(in MobContext c, float t, List<FxStamp> o, out bool whole)
        {
            float dir = c.Dir * Deg;
            float r = c.Size;
            // Whether the kind's own touch (LandLook) is already in it.
            whole = false;
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
                    // It comes down: a flash of itself, drops flung up and out, a ring, a puddle that sinks away, and the
                    // dust its weight throws out along the floor.
                    float life = 0.4f;
                    for (int i = -1; i <= 1; i += 2)
                        Put(o, FxShape.Smoke, FxRamp.Dust, 8, Math.Min(3, (int)(t / life * 4)), i * r * (0.5f + 0.5f * SkillFx.Out(t / life)), 2, 0.6f * (1 - t / life), 0, true);
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
                    float burst = Burst(c, t, o, true);
                    if (burst > 0)
                    {
                        whole = true;
                        return burst;
                    }
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
                    float burst = Burst(c, t, o, false);
                    if (burst > 0)
                    {
                        whole = true;
                        return burst;
                    }
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

        // A blow in its warning's shape (F2): the shape itself burns in the element's colours under this (the view's
        // and the preview's, HazardArt.Blast with BlowRamp). Here: the one stroke that says what struck (a cut, a
        // thrust's streak, a beam's white heart), a light, and the element going off at a few places inside the shape
        // (three across a wedge, two to four along a line, five round a ring), each a moment after the last.
        static float ShapeLand(in MobContext c, float t, List<FxStamp> o)
        {
            const float life = 0.62f;
            float dir = c.Dir * Deg, r = c.Size;
            var element = ElementOf(c);
            switch (c.Shape)
            {
                case MonsterAttackShape.Strike:
                case MonsterAttackShape.Sweep:
                    if (t < 0.24f) Put(o, FxShape.Crescent, element, (int)Math.Max(6, r * 0.8f), (int)Math.Clamp(c.Width, 40, 170), 0, 0, t < 0.08f ? 1 : 1 - (t - 0.08f) / 0.16f, dir);
                    break;
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                    if (t < 0.24f && c.Area == HazardShape.Line)
                    {
                        float alpha = t < 0.1f ? 1 : 1 - (t - 0.1f) / 0.14f, mx = SkillFx.Cos(dir) * r / 2, my = SkillFx.Sin(dir) * r / 2;
                        Put(o, FxShape.Streak, c.Shape == MonsterAttackShape.Beam ? FxRamp.Steel : element, SkillFxArt.Step4(r), c.Shape == MonsterAttackShape.Beam ? 2 : 3, mx, my, alpha, dir);
                    }
                    break;
            }
            float inner = c.Area == HazardShape.Ring ? Math.Min(c.Width, r - 4) : 0;
            int spots = c.Area == HazardShape.Cone ? (c.Width < 60 ? 2 : 3) : c.Area == HazardShape.Line ? Math.Clamp((int)Math.Round(r / 20), 2, 4) : 5;
            float girth = c.Area == HazardShape.Cone ? r * 0.24f : c.Area == HazardShape.Line ? c.Width / 2 : (r - inner) / 2;
            girth = Math.Clamp(girth, 5, 12);
            float lx = 0, ly = 0;
            for (int i = 0; i < spots; i++)
            {
                float x, y;
                if (c.Area == HazardShape.Cone)
                {
                    float a = dir + (spots == 1 ? 0 : (i / (float)(spots - 1) - 0.5f) * Math.Min(c.Width, 300) * 0.62f);
                    x = SkillFx.Cos(a) * r * 0.62f;
                    y = SkillFx.Sin(a) * r * 0.62f;
                }
                else if (c.Area == HazardShape.Line)
                {
                    float along = r * (i + 0.5f) / spots;
                    x = SkillFx.Cos(dir) * along;
                    y = SkillFx.Sin(dir) * along;
                }
                else
                {
                    float a = i * 72 + c.Seed % 72, mid = (r + inner) / 2;
                    x = SkillFx.Cos(a) * mid;
                    y = SkillFx.Sin(a) * mid;
                }
                if (i == spots / 2)
                {
                    lx = x;
                    ly = y;
                }
                Pop(c, element, t - 0.035f * i, o, x, y, girth, c.Seed + i * 17);
            }
            if (c.Area == HazardShape.Ring) (lx, ly) = (0, 0);
            if (element != FxRamp.Dust && element != FxRamp.Sand && element != FxRamp.Plain && element != FxRamp.Steel)
                FxLayers.Light(o, element, Math.Max(r * 0.9f, girth * 3), 0.7f * (1 - t / 0.35f), lx, ly);
            return life;
        }

        // The element going off at one place in a blow's shape, small: at most four pieces at a moment.
        static void Pop(in MobContext c, FxRamp element, float t, List<FxStamp> o, float x, float y, float rr, int seed)
        {
            if (t < 0) return;
            if (element == FxRamp.Fire || (element == FxRamp.Violet && c.Look != MobLook.Spore))
            {
                // A small ball of fire and sparks; fire burns on a moment, the void's has a star going out in it.
                FxLayers.Ball(o, element, t, 0, rr, seed & 3, x, y + 3);
                FxLayers.Embers(o, element, 2, t, 0.02f, rr * 1.8f, seed, x, y + 3, life: 0.26f);
                if (element == FxRamp.Fire) { if (SkillFx.In(t, 0.2f, 0.58f)) Put(o, FxShape.Flame, FxRamp.Fire, 7 + (seed & 1) * 2, (int)(t * 12 + seed) & 3, x, y + 4, 1 - (t - 0.2f) / 0.38f); }
                else if (SkillFx.In(t, 0.1f, 0.4f) && SkillFx.Frac(t * 12 + seed * 0.37f) < 0.6f) Put(o, FxShape.Glint, FxRamp.Violet, 5, 1, x, y + 4);
            }
            else if (c.Look == MobLook.Spore || c.Look == MobLook.Water)
            {
                // Lumps (drops, of water) thrown up and out, and a puff going up after.
                bool wet = c.Look == MobLook.Water;
                float p = SkillFx.U(t, 0, 0.3f);
                for (int i = 0; i < 3 && p < 1; i++)
                {
                    float a = i * 120 + seed % 97, far = rr * (0.3f + 1.1f * SkillFx.Out(p));
                    Put(o, wet ? FxShape.Dot : FxShape.Blob, element, wet ? 3 : 2 * (3 - (int)(2 * p)), wet ? 3 : 1, x + SkillFx.Cos(a) * far, y + SkillFx.Sin(a) * far * 0.8f + 4 * 7 * p * (1 - p));
                }
                float q = (t - 0.1f) / 0.5f;
                if (q >= 0 && q < 1) Put(o, FxShape.Smoke, wet ? FxRamp.Mist : element, SkillFxArt.Even(7 + 5 * q), Math.Min(3, (int)(q * 4)), x, y + 2 + q * 12, 0.5f * (1 - q));
            }
            else if (element == FxRamp.Cold)
            {
                // A spike stands, holds a moment and breaks.
                if (t < 0.06f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, x, y + 4);
                int tall = 5 + (int)((5 + 3 * (seed & 1)) * SkillFx.Out(SkillFx.U(t, 0, 0.1f)));
                if (t < 0.28f) Put(o, FxShape.Shard, c.Ramp == FxRamp.Plain ? FxRamp.Cold : c.Ramp, tall, 0, x, y + tall / 2f, 1, 90 + ((seed & 3) - 1.5f) * 8);
                SkillFx.Fly(o, FxShape.Shard, c.Ramp == FxRamp.Plain ? FxRamp.Cold : c.Ramp, 5, 0, 3, t, 0.28f, 0.26f, 2, rr * 1.5f, seed, hop: 6, cx: x, cy: y + 3, aimed: true);
            }
            else if (element == FxRamp.Sand || element == FxRamp.Dust)
            {
                // A stone thrown up over its shadow, and dust low along the floor.
                FxLayers.Rubble(o, FxRamp.Dust, 1, t, 0, rr, seed, x, y);
                float q = SkillFx.U(t, 0, 0.5f);
                if (q > 0 && q < 1) Put(o, FxShape.Smoke, FxRamp.Dust, SkillFxArt.Even(7 + 5 * q), Math.Min(3, (int)(q * 4)), x, y + 2, 0.7f * (1 - q), 0, true);
                if (c.Shape == MonsterAttackShape.Thrust && c.Look == MobLook.Sand) SkillFx.Fly(o, FxShape.Dot, FxRamp.Heal, 2, 2, 1, t, 0.04f, 0.3f, 2, 8, seed + 13, hop: 5, cx: x, cy: y);
            }
            else if (element == FxRamp.Steel)
            {
                // Sparks off iron.
                if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, x, y + 3);
                FxLayers.Embers(o, FxRamp.Steel, 3, t, 0, rr * 1.8f, seed, x, y + 3, life: 0.22f);
            }
            else
            {
                // A kind with no element: a flash of its own colour and bits of it thrown out.
                if (t < 0.08f) Put(o, FxShape.Blob, element, SkillFxArt.Even(rr * 1.3f), 0, x, y + 3);
                SkillFx.Fly(o, FxShape.Dot, element, 2, 2, 3, t, 0.02f, 0.28f, 2, rr * 1.6f, seed, hop: 6, cx: x, cy: y + 2);
            }
        }

        // Something thrown, left or carried goes off as what it is made of (E5, FxLayers): fire and the void's violet
        // as a blast, spores and water as lumps thrown out with a cloud going up, ice and crystal as spikes that stand
        // and break, earth as dust and stones. 0 for a kind with no element of its own (the shape's plain blow then).
        static float Burst(in MobContext c, float t, List<FxStamp> o, bool big)
        {
            float r = c.Size;
            int seed = c.Seed;
            if (c.Look == MobLook.Ember || c.Ramp == FxRamp.Fire)
                return Math.Min(1f, FxLayers.Blast(o, t, r, FxRamp.Fire, seed, big ? 8 : 6, big ? 2 : 0, true, 0.9f));
            if (c.Look == MobLook.Shadow || c.Look == MobLook.Hex)
            {
                // Violet fire with no smoke, and stars going out in it.
                for (int i = 0; i < 3; i++)
                    if (SkillFx.In(t, 0.05f * i, 0.3f + 0.05f * i) && SkillFx.Frac(t * 12 + i * 0.4f) < 0.6f)
                        Put(o, FxShape.Glint, FxRamp.Violet, 5, 1, SkillFx.Cos(i * 120 + seed % 90) * r * 0.5f, SkillFx.Sin(i * 120 + seed % 90) * r * 0.4f + 4);
                return FxLayers.Blast(o, t, r, FxRamp.Violet, seed, big ? 8 : 6, 0, false, 0.6f);
            }
            if (c.Look == MobLook.Spore || c.Look == MobLook.Water)
            {
                float life = 0.8f;
                bool wet = c.Look == MobLook.Water;
                FxLayers.Light(o, c.Ramp, r * 1.8f, 0.6f * (1 - t / 0.4f));
                if (t < 0.06f) Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(r * 0.9f), 0, 0, 2);
                FxLayers.Shock(o, c.Ramp, t, 0, r, ground: true);
                // Lumps thrown up and out.
                float p = SkillFx.U(t, 0, 0.3f);
                for (int i = 0; i < 7 && p < 1; i++)
                {
                    float a = i * 360f / 7 + seed % 51, far = r * (0.3f + 0.9f * SkillFx.Out(p));
                    Put(o, wet ? FxShape.Dot : FxShape.Blob, c.Ramp, wet ? 3 : 2 * (3 - (int)(2 * p)), wet ? 3 : 1, SkillFx.Cos(a) * far, SkillFx.Sin(a) * far * 0.8f + 4 * 7 * p * (1 - p));
                }
                // A cloud of it going up (spray, from water), and motes drifting after.
                for (int i = 0; i < 4; i++)
                {
                    float q = (t - 0.1f - i * 0.06f) / 0.6f;
                    if (q < 0 || q >= 1) continue;
                    Put(o, FxShape.Smoke, wet ? FxRamp.Mist : c.Ramp, SkillFxArt.Even(8 + 6 * q), Math.Min(3, (int)(q * 4)), ((i * 2 + 1) % 4 - 1.5f) * r * 0.45f + (SkillFx.Rnd(seed, i, 51) - 0.5f) * 4, 2 + (i & 1) * 3 + q * 14, 0.55f * (1 - q));
                }
                SkillFx.Float(o, FxShape.Dot, c.Ramp, 2, 2, 5, t, 0.05f, life, r * 0.8f, 14, seed + 11);
                return life;
            }
            if (c.Look == MobLook.Frost || c.Look == MobLook.Crystal || c.Ramp == FxRamp.Cold)
            {
                float life = 0.62f;
                FxLayers.Light(o, FxRamp.Cold, r * 1.6f, 0.5f * (1 - t / 0.35f), 0, 0, 2);
                if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 13, 2, 0, 4);
                FxLayers.Shock(o, FxRamp.Cold, t, 0, r, ground: true);
                // Spikes stand up round where it fell, hold a moment, and break.
                float rise = SkillFx.Out(SkillFx.U(t, 0, 0.1f));
                for (int i = 0; i < 6 && t < 0.3f; i++)
                {
                    float a = i * 60 + 20 + seed % 40;
                    int tall = 5 + (int)((5 + 3 * (i & 1)) * rise);
                    Put(o, FxShape.Shard, c.Ramp, tall, 0, SkillFx.Cos(a) * r * 0.6f, SkillFx.Sin(a) * r * 0.42f + tall / 2f, 1, 90 + (i - 2.5f) * 7);
                }
                SkillFx.Fly(o, FxShape.Shard, c.Ramp, 5, 0, 7, t, 0.28f, 0.28f, r * 0.4f, r * 1.25f, seed, hop: 7, aimed: true);
                SkillFx.Float(o, FxShape.Smoke, FxRamp.Mist, 8, 2, 3, t, 0.28f, life, r * 0.5f, 6, seed);
                return life;
            }
            if (c.Look == MobLook.Sand || c.Ramp == FxRamp.Dust)
            {
                float life = 0.75f;
                FxLayers.Shock(o, FxRamp.Dust, t, 0, r, ground: true);
                if (t < 0.08f) Put(o, FxShape.Blob, FxRamp.Dust, SkillFxArt.Even(r * 0.9f), 0, 0, 3);
                FxLayers.Splits(o, FxRamp.Dust, 3, t, 0, life, r, seed);
                FxLayers.Rubble(o, FxRamp.Dust, big ? 4 : 3, t, 0, r, seed);
                // Dust rolling out low along the floor, under any warning.
                for (int i = 0; i < 5; i++)
                {
                    float q = SkillFx.U(t, 0.02f * i, 0.5f + 0.04f * i);
                    if (q <= 0 || q >= 1) continue;
                    float a = i * 72 + seed % 72;
                    Put(o, FxShape.Smoke, FxRamp.Dust, SkillFxArt.Even(7 + 5 * q), Math.Min(3, (int)(q * 4)), SkillFx.Cos(a) * r * (0.4f + 0.7f * q), SkillFx.Sin(a) * r * 0.6f * (0.4f + 0.7f * q) + 2, 0.7f * (1 - q), 0, true);
                }
                return life;
            }
            return 0;
        }

        // ---- A body that runs at someone ----

        static void Rush(in MobContext c, float t, List<FxStamp> o)
        {
            float dir = c.Dir * Deg, bx = -SkillFx.Cos(dir), by = -SkillFx.Sin(dir);
            // Behind its body, however large that is (Size: its radius in pixels).
            float behind = Math.Max(8, c.Size);
            // Lines of speed off its back.
            for (int i = 0; i < 4; i++)
            {
                float lag = 12 + 8 * SkillFx.Rnd(c.Seed + (int)(t * 25), i, 61) + behind * 0.4f, side = (i - 1.5f) * Math.Max(4, behind * 0.4f);
                Put(o, FxShape.Streak, c.Ramp, SkillFxArt.Step4(lag), 1, bx * (behind + 2 + lag / 2) - by * side, Body + by * (behind + 2 + lag / 2) + bx * side, 0.6f, dir);
            }
            // Dust kicked up behind its feet, on the floor; and what its kind is made of thrown up with it.
            for (int i = 0; i < 5; i++)
            {
                float u = SkillFx.Frac(t * 3 + i / 5f), far = behind + 22 * u, off = (SkillFx.Rnd(c.Seed, i, 62) - 0.5f) * Math.Max(8, behind);
                float x = bx * far - by * off, y = 1 + by * far + bx * off;
                Put(o, FxShape.Smoke, FxRamp.Dust, SkillFxArt.Even(6 + 5 * u), Math.Min(3, (int)(u * 4)), x, y, 0.75f * (1 - u), 0, true);
                if ((i & 1) != 0) continue;
                float hop = 4 * 8 * u * (1 - u);
                if (c.Look == MobLook.Frost || c.Look == MobLook.Crystal) Put(o, FxShape.Shard, c.Ramp, 5 - (int)(2 * u), 0, x, y + 3 + hop, 1 - u, 60 + u * 400 + i * 50);
                else if (c.Look == MobLook.Ember) Put(o, FxShape.Flame, FxRamp.Fire, 7 - (int)(3 * u), (int)(t * 12 + i) & 3, x, y + 3, 1 - u);
                else Put(o, FxShape.Chunk, FxRamp.Dust, 3, i, x, y + 2 + hop, 1 - u);
            }
        }

        // ---- What lies on the floor ----

        static void Pool(in MobContext c, float t, List<FxStamp> o)
        {
            float fade = Math.Clamp(c.Progress, 0, 1);
            float r = c.Size;
            // What a blow along a line left is one band (F2): Width its length between its ends' middles, Dir the way
            // it lies; the pieces in it are spread along it, more of them the longer it is.
            float along = c.Area == HazardShape.Balm ? 0 : Math.Max(0, c.Width);
            bool band = along >= 4;
            float dir = c.Dir * Deg, ax = SkillFx.Cos(dir), ay = SkillFx.Sin(dir);
            int seed = c.Seed;
            int count = Math.Clamp((int)(r / 4) + 2, 3, 7);
            if (band) count = Math.Min(10, (int)(count * (1 + along / (2.5f * r))));
            // A place along the band for the i-th piece (the middle of a round pool).
            (float X, float Y) At(int i, int salt)
            {
                if (!band) return (0, 0);
                float d = (SkillFx.Rnd(seed, i, salt) - 0.5f + i - (count - 1) / 2f) / count * along;
                return (ax * d, ay * d);
            }
            void Floor(FxRamp ramp, int heave, float alpha)
            {
                if (band) Put(o, FxShape.Strip, ramp, SkillFxArt.Even(2 * r), SkillFxArt.StripFrame(along, heave), 0, 0, alpha, dir, true);
                else Put(o, FxShape.Puddle, ramp, SkillFxArt.Even(2 * r), heave & 3, 0, 0, alpha, 0, true);
            }
            void Lights(FxRamp ramp, float radius, float strength)
            {
                if (!band) FxLayers.Light(o, ramp, radius, strength);
                else for (int i = -1; i <= 1; i += 2) FxLayers.Light(o, ramp, radius, strength, ax * along * 0.28f * i, ay * along * 0.28f * i);
            }
            switch (c.Area)
            {
                case HazardShape.Fire:
                    // Soot under it, tongues of flame that leap and lean, embers going up, its light on the floor.
                    if (band) Floor(FxRamp.Smoke, seed, 0.7f * fade);
                    else Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(r * 1.7f), 4 + (seed & 3), 0, 0, 0.75f * fade, 0, true);
                    Lights(FxRamp.Fire, r * 2.2f, (0.5f + 0.08f * (float)Math.Sin(t * 20 + seed)) * fade);
                    for (int i = 0; i < count; i++)
                    {
                        float a = SkillFx.Rnd(seed, i, 1) * 360, d = r * 0.7f * (float)Math.Sqrt(SkillFx.Rnd(seed, i, 2));
                        int tall = 8 + 3 * (i & 1) + (int)(2 * Math.Sin(t * 7 + i * 2.1f));
                        var (x, y) = At(i, 21);
                        Put(o, FxShape.Flame, FxRamp.Fire, tall, (int)(t * 9 + i * 1.7f) & 3, x + SkillFx.Cos(a) * d, y + SkillFx.Sin(a) * d * 0.7f + tall / 2f - 1, fade);
                    }
                    for (int i = 0; i < (band ? 4 : 3); i++)
                    {
                        float u = SkillFx.Frac(t * 1.5f + SkillFx.Rnd(seed, i, 6));
                        float d = band ? (SkillFx.Rnd(seed, i, 8) - 0.5f) * along : 0;
                        Put(o, FxShape.Dot, FxRamp.Fire, 1, u < 0.5f ? 3 : 2, ax * d + (SkillFx.Rnd(seed, i, 7) - 0.5f) * r * 1.2f + (float)Math.Sin(u * 6) * 2, ay * d + 6 + 20 * u, fade * (1 - u));
                    }
                    break;
                case HazardShape.Ice:
                    // A sheet of ice with crystals standing in it, one glinting now and then.
                    Floor(FxRamp.Cold, seed, 0.6f * fade);
                    Lights(FxRamp.Cold, r * 1.5f, 0.25f * fade);
                    for (int i = 0; i < count - 1; i++)
                    {
                        float a = SkillFx.Rnd(seed, i, 3) * 360, d = r * 0.6f * (float)Math.Sqrt(SkillFx.Rnd(seed, i, 4));
                        int tall = 5 + 2 * (i % 3);
                        var (x, y) = At(i, 22);
                        Put(o, FxShape.Shard, FxRamp.Cold, tall, 0, x + SkillFx.Cos(a) * d, y + SkillFx.Sin(a) * d * 0.7f + tall / 2f - 1, 0.95f * fade, 90 + (i % 5 - 2) * 9);
                    }
                    if (SkillFx.Frac(t * 0.8f) < 0.25f)
                    {
                        var (x, y) = At((int)(t * 0.8f) % count, 23);
                        Put(o, FxShape.Glint, FxRamp.Cold, 7, 1, x + SkillFx.Cos(t * 40) * r * 0.3f, y + 4, fade);
                    }
                    break;
                case HazardShape.Spore:
                {
                    // A pool that heaves: bubbles swell on it and pop, motes drift up out of it.
                    Floor(FxRamp.Violet, (int)(t * 1.5f), 0.8f * fade);
                    Lights(FxRamp.Violet, r * 1.7f, 0.3f * fade);
                    int bubbles = band ? 6 : 4;
                    for (int i = 0; i < bubbles; i++)
                    {
                        float u = SkillFx.Frac(t * 1.1f + SkillFx.Rnd(seed, i, 9));
                        float a = SkillFx.Rnd(seed, i, 10) * 360, d = r * 0.6f * SkillFx.Rnd(seed, i, 11);
                        float off = band ? (SkillFx.Rnd(seed, i, 12) - 0.5f) * along : 0;
                        float x = ax * off + SkillFx.Cos(a) * d, y = ay * off + SkillFx.Sin(a) * d * 0.8f;
                        if (u < 0.75f) Put(o, FxShape.Edge, FxRamp.Violet, u < 0.4f ? 4 : 6, 1, x, y, fade, 0, true);
                        else Put(o, FxShape.Broken, FxRamp.Violet, 8, SkillFxArt.BrokenFrame(1, 4, i), x, y, fade * (1 - u) * 4, 0, true);
                    }
                    if (!band) SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 3, 4, SkillFx.Frac(t * 0.7f), 0, 1, r * 0.75f, 13, seed, alpha: fade);
                    else
                        for (int i = -1; i <= 1; i++)
                            SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 3, 2, SkillFx.Frac(t * 0.7f + i * 0.3f), 0, 1, r * 0.75f, 13, seed + i, cx: ax * along * 0.33f * i, cy: ay * along * 0.33f * i, alpha: fade);
                    break;
                }
                case HazardShape.Sand:
                    // Loose sand, grains turning round and down to its middle (along a band, in toward its middle line).
                    Floor(FxRamp.Dust, (int)(t * 2), 0.6f * fade);
                    for (int i = 0; i < count + 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.5f + i / (float)(count + 2));
                        float a = i * 360f / (count + 2) + t * 200;
                        var (x, y) = At(i, 24);
                        Put(o, FxShape.Dot, FxRamp.Dust, 2, u > 0.5f ? 0 : 3, x + SkillFx.Cos(a) * r * (band ? 0.55f : 0.85f) * (1 - u), y + SkillFx.Sin(a) * r * (band ? 0.45f : 0.6f) * (1 - u), fade, 0, true);
                    }
                    break;
                case HazardShape.Balm:
                    // A soft green cloud: it harms no one, so no red.
                    Put(o, FxShape.Smoke, FxRamp.Heal, SkillFxArt.Even(r * 1.3f), 2, 0, 2, 0.3f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Heal, 2, 2, count + 2, SkillFx.Frac(t * 0.6f), 0, 1, r * 0.8f, 12, seed, alpha: fade);
                    for (int i = 0; i < 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.8f + i * 0.5f);
                        Put(o, FxShape.Plus, FxRamp.Heal, 3, 0, (i == 0 ? -1 : 1) * r * 0.35f, 4 + 9 * u, fade * (1 - u));
                    }
                    break;
            }
            // An enemy's, so while it hurts it wears a dotted red rim at its true reach whatever it is made of, its
            // dots stepping round (a band's round the whole band); a cloud that only fills monsters does not.
            if (c.Area == HazardShape.Balm) return;
            if (band) Put(o, FxShape.StripDashes, FxRamp.Danger, SkillFxArt.Even(2 * r), SkillFxArt.StripFrame(along, (int)(t * 4) & 1), 0, 0, 0.9f * fade, dir, true);
            else Put(o, FxShape.Dashes, FxRamp.Danger, SkillFxArt.Even(2 * r), (int)(t * 4) & 1, 0, 0, 0.9f * fade, 0, true);
        }

        // ---- A shot ----

        // The shot itself is ShotArt's picture in what its kind is made of (ShotOf: the view and the preview lay it);
        // this is only what goes over it: a returning one blinks where it stands before it comes back.
        static void Shot(in MobContext c, float t, List<FxStamp> o)
        {
            if (c.Shape == MonsterAttackShape.Boomerang && c.Armed && SkillFx.Frac(t * 10) < 0.4f) Put(o, FxShape.Glint, c.Ramp, 13, 2, 0, 0);
        }
    }
}
