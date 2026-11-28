using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The void lord of the abyss (2026-11-28, Docs/Bosses.md 공허의 군주): a dark emperor hovering over the floor
    // under a crown of ice, a cape of night fraying into the void, a black orb in one hand. Every attack has its own
    // wind-up of the whole body (the empty hand drawn back before the grasp, the orb held out swelling before the beam
    // and the lance, both arms raised before the stars fall, arms and cape spread trembling before the rings of shots,
    // dissolving before he blinks across, the hand raised and clenching before the floor collapses, the orb raised
    // overhead before the cross and the horizon), the blow lands as the mark fills, and after a grasp, a crush or a
    // smite he stays stooped a moment, the opening to hit back. His blows are the void and the cold: claws closing,
    // stars come down, rifts torn open with ice standing in them, the floor drawn into a point and thrown back out. ----
    sealed partial class WorldView
    {
        static readonly Color VoidLordColor = new Color(0.72f, 0.55f, 1f);

        // Which blow follows each of his attacks (by the attack's id, boss_attacks.csv; an attack he does not know by
        // name by its shape).
        static VoidLordPose VoidLordBlowOf(BossAttack attack) => attack.Id switch
        {
            "grasp" => VoidLordPose.Grasp,
            "beam" or "void_lance" => VoidLordPose.Cast,
            "star_fall" => VoidLordPose.Starfall,
            "void_ring" or "void_storm" or "void_spiral" => VoidLordPose.Burst,
            "blink" => VoidLordPose.Blink,
            "collapse" or "black_holes" or "gravity_share" or "void_ripple" => VoidLordPose.Crush,
            "void_cross" or "event_horizon" => VoidLordPose.Smite,
            _ => attack.Shape switch
            {
                AttackShape.Cone => VoidLordPose.Grasp,
                AttackShape.Line => VoidLordPose.Cast,
                AttackShape.Burst or AttackShape.Spiral => VoidLordPose.Burst,
                AttackShape.Charge => VoidLordPose.Blink,
                AttackShape.Cross or AttackShape.Ring => VoidLordPose.Smite,
                AttackShape.Share => VoidLordPose.Crush,
                _ => VoidLordPose.Starfall,
            },
        };

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int VoidLordBlowFrame(VoidLordPose blow, float seconds)
        {
            for (int f = 0; f < VoidLordArt.FramesOf(blow); f++)
            {
                seconds -= VoidLordArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // His pose from what he is doing. A wind-up runs through its telegraph, and the blow's lunge frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateVoidLord(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = VoidLordPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 6) % VoidLordArt.FramesOf(VoidLordPose.Idle);
            body.Shape.color = Color.white;
            int Through(VoidLordPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * VoidLordArt.FramesOf(p)), 0, VoidLordArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Under the altar's floor; woken, a rift opens and he rises out of it.
                pose = VoidLordPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : VoidLordArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // Arms and cape spread, rising, then shaking there.
                pose = VoidLordPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < VoidLordArt.RoarRise ? rise : VoidLordArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // Gone into the void while he crosses; only motes of him go on ahead (VoidLordExtras).
                pose = VoidLordPose.Blink;
                frame = VoidLordArt.StrikeFrame(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Gliding, leaning into it.
                pose = VoidLordPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 8) % VoidLordArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                var blow = VoidLordBlowOf(attack);
                var windUp = VoidLordArt.WindUpOf(blow);
                int strike = VoidLordArt.StrikeFrame(blow);
                float swing = strike * VoidLordArt.FrameSeconds(blow, 0);
                float wound = 1 - Mathf.Min(0.5f, swing / Mathf.Max(0.1f, attack.Telegraph));
                if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                else
                {
                    int f = VoidLordBlowFrame(blow, (progress - wound) * attack.Telegraph);
                    (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape != AttackShape.Summon)
            {
                // From the strike on, each frame for its own time.
                var blow = VoidLordBlowOf(attack);
                int strike = VoidLordArt.StrikeFrame(blow);
                float left = since;
                for (int k = strike; k < VoidLordArt.FramesOf(blow); k++)
                {
                    left -= VoidLordArt.FrameSeconds(blow, k);
                    if (left >= 0) continue;
                    (pose, frame) = (blow, k);
                    break;
                }
            }

            // Throwing his shots in a spiral he turns round and round.
            if (attack != null && attack.Shape == AttackShape.Spiral && (pose == VoidLordPose.Burst || pose == VoidLordPose.Spread) && body.BossState != BossState.Telegraph)
                sector = (sector + Mathf.FloorToInt(Time.time * 12)) % 8;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 810000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose != VoidLordPose.Enter && pose != VoidLordPose.Walk && pose != VoidLordPose.Idle && pose != VoidLordPose.Roar)
            {
                // The blow begins: a rush of the void.
                if (frame == 0 && VoidLordArt.StrikeFrame(pose) > 0) Hear(pose == VoidLordPose.Blink ? Sound.Dash : Sound.Cast, body.Position, 0.7f);
            }
            body.Frame = code;
            string key = $"voidlord/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => VoidLordArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, VoidLordArt.Floor / (float)VoidLordArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + VoidLordArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While he winds up: the void drawn into his empty hand, frost drawn into the orb, stars rising off his raised
        // arms, motes thrown off his spread cape, the void swirling as he dissolves, the floor trembling as the hand
        // clenches; while he blinks across, motes of him streaming on.
        void VoidLordExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            var chest = feet + new Vector3(0, 2.4f, 0);
            if (view.BossState == BossState.Charge)
            {
                if (puff) Chips(feet + new Vector3(0, 1.4f, 0) + (Vector3)(Random.insideUnitCircle * 0.8f), -ahead, FxRamp.Violet, 2, 14, 60);
                return;
            }
            if (view.BossState != BossState.Telegraph || attack == null || attack.Shape == AttackShape.Summon) return;
            float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
            switch (VoidLordArt.WindUpOf(VoidLordBlowOf(attack)))
            {
                case VoidLordPose.Reach:
                    // The void curling into his drawn-back hand.
                    if (puff) Chips(chest - ahead * 0.9f + (Vector3)(Random.insideUnitCircle * 0.5f), -ahead + Vector3.up * 0.3f, FxRamp.Violet, 1, 10, 60);
                    break;
                case VoidLordPose.Gather:
                {
                    // Frost drawn in from all round to the orb held out ahead.
                    if (puff)
                    {
                        var orb = chest + ahead * 0.8f;
                        var from = orb + (Vector3)(Random.insideUnitCircle.normalized * 1.4f);
                        Chips(from, orb - from, FxRamp.Cold, 1, 16, 20);
                    }
                    break;
                }
                case VoidLordPose.Exalt:
                case VoidLordPose.Raise:
                    // Stars and frost rising off the orb held high; the abyss begins to shake.
                    if (puff) Chips(chest + new Vector3(0, 1.4f, 0) + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.up, FxRamp.Cold, 1, 18, 50);
                    if ((attack.Id == "event_horizon" || attack.Id == "star_fall") && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.06f * progress);
                    break;
                case VoidLordPose.Spread:
                    // Motes of the void thrown off his spread cape all round.
                    if (puff)
                    {
                        float a = Random.value * 360;
                        var round = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad) * 0.6f, 0);
                        Chips(feet + new Vector3(0, 1f, 0) + round * 0.8f, round, FxRamp.Violet, 1, 14, 30);
                    }
                    break;
                case VoidLordPose.Fade:
                {
                    // The void swirling round him as he goes into it.
                    if (puff)
                    {
                        float a = Time.time * 9;
                        var from = feet + new Vector3(Mathf.Cos(a), 1.4f + Mathf.Sin(a) * 0.5f, 0) * 1.1f;
                        Chips(from, new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * 0.5f, 0), FxRamp.Violet, 1, 12, 20);
                    }
                    break;
                }
                case VoidLordPose.Clench:
                {
                    // The floor drawn in towards him, trembling more as the hand shuts.
                    if (puff)
                    {
                        var from = feet + (Vector3)(Random.insideUnitCircle.normalized * 2.2f);
                        Chips(from, feet - from, FxRamp.Violet, 1, 18, 20);
                    }
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + (attack.Id == "gravity_share" || attack.Id == "black_holes" ? 0.07f : 0.03f) * progress);
                    break;
                }
            }
        }

        // Where one of his blows lands, the void and the cold of his own over the blow's shape; false for anyone else's.
        bool VoidLordLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "void_lord") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Violet, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu, Seed = hazard.Id };
            switch (id)
            {
                case "grasp":
                    // Claws of the void raked through the wedge and shut.
                    BlastLanded(hazard, FxRamp.Violet, sparks: false, chunks: false, light: false);
                    c.Width = hazard.Width;
                    AddBossFx(BossFx.VoidClaw, at, c);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Ice, at, 0.8f);
                    Hear(Sound.Heavy, at, 0.5f);
                    return true;
                case "star_fall":
                    // A star come down out of the dark, bursting into ice.
                    BlastLanded(hazard, FxRamp.Cold, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Comet, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.12f);
                    Hear(Sound.Shatter, at, 0.6f);
                    return true;
                case "beam":
                case "void_lance":
                case "void_cross":
                    // A rift torn down the line (each arm of the cross its own); the lance a spear of ice thrown down it.
                    BlastLanded(hazard, FxRamp.Violet, sparks: false, chunks: false, light: false, wave: false);
                    c.Big = id == "void_lance";
                    AddBossFx(BossFx.Rift, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "void_cross" ? 0.18f : 0.12f);
                    Hear(c.Big ? Sound.Ice : Sound.Zap, at, 0.8f);
                    return true;
                case "collapse":
                case "black_holes":
                    // The floor drawn into a point and thrown back out.
                    BlastLanded(hazard, FxRamp.Violet, sparks: false, chunks: false, light: false, wave: false);
                    c.Big = id == "black_holes";
                    AddBossFx(BossFx.Singularity, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, c.Big ? 0.2f : 0.14f);
                    Hear(Sound.Nova, at, c.Big ? 0.9f : 0.7f);
                    return true;
                case "gravity_share":
                    // Where it is shared (the gold circle burns gold): the heaviest collapse.
                    BlastLanded(hazard, null, sparks: false, chunks: false, light: false);
                    Impact(at, 0.9f);
                    c.Big = true;
                    AddBossFx(BossFx.Singularity, at, c);
                    Hear(Sound.Nova, at, 1);
                    return true;
                case "event_horizon":
                case "void_ripple":
                    // Rings turning round the band, stars running round it.
                    BlastLanded(hazard, FxRamp.Violet, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    AddBossFx(BossFx.Horizon, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "event_horizon" ? 0.22f : 0.1f);
                    Hear(Sound.Nova, at, id == "event_horizon" ? 1 : 0.6f);
                    return true;
            }
            // Any other of his blows: the shared landing and what the void does over it.
            return false;
        }
    }
}
