using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The antlion of the sand cave (2026-11-27, Docs/Bosses.md 개미귀신): the second boss, a pit-trap larva. Every
    // attack has its own wind-up of the whole body (the jaws spread wide before a bite, the head shoved into the sand
    // before a jet of it is thrown, rocking as the legs rake a pit, rearing up before the sandstorm, sinking before the
    // quicksand, gone under before the floor caves in), the blow lands as the mark fills, and after a bite the jaws
    // stay clamped in the sand a moment, the opening to hit back. It burrows for its charge: a mound of sand runs
    // across the floor and it bursts up where the run ends. ----
    sealed partial class WorldView
    {
        static readonly Color SandColor = new Color(0.86f, 0.68f, 0.42f);
        static readonly Color SandDarkColor = new Color(0.55f, 0.4f, 0.25f);

        // How long a chasing mound's boss takes to go under before the mound runs (R3).
        const float MoundDive = 0.3f;

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static AntlionPose AntlionBlowOf(BossAttack attack) => attack.Id switch
        {
            "bite" => AntlionPose.Snap,
            "sand_jet" => AntlionPose.Flick,
            "pit" => AntlionPose.Toss,
            "sand_trail" => AntlionPose.Gouge,
            "quicksand" or "sand_fountain" => AntlionPose.Churn,
            "sandstorm" => AntlionPose.Crash,
            "sand_cross" => AntlionPose.Clap,
            "sinkholes" or "maw" => AntlionPose.Erupt,
            "drag_bite" => AntlionPose.Snap,
            _ => attack.Shape switch
            {
                AttackShape.Cone => AntlionPose.Snap,
                AttackShape.Line => AntlionPose.Flick,
                AttackShape.Trail => AntlionPose.Gouge,
                AttackShape.Ring or AttackShape.Spiral or AttackShape.Burst => AntlionPose.Churn,
                AttackShape.Cross => AntlionPose.Clap,
                _ => AntlionPose.Toss,
            },
        };

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int AntlionBlowFrame(AntlionPose blow, float seconds)
        {
            for (int f = 0; f < AntlionArt.FramesOf(blow); f++)
            {
                seconds -= AntlionArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // Its pose from what it is doing. A wind-up runs through its telegraph, and the blow's lunge frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateAntlion(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = AntlionPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 5) % AntlionArt.FramesOf(AntlionPose.Idle);
            body.Shape.color = Color.white;
            int Through(AntlionPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * AntlionArt.FramesOf(p)), 0, AntlionArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Buried at the bottom of its pit, the jaw tips showing; woken, it flicks sand out and bursts up.
                pose = AntlionPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : AntlionArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The jaws thrown high and wide, then shaking there.
                pose = AntlionPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < AntlionArt.RoarRise ? rise : AntlionArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // Under the sand: a mound running across the floor.
                pose = AntlionPose.Burrow;
                frame = Mathf.FloorToInt(Time.time * 16) % AntlionArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Six legs scuttling, about two steps a tile.
                pose = AntlionPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 10) % AntlionArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                if (attack.Shape == AttackShape.Charge)
                {
                    (pose, frame) = (AntlionPose.Dive, Through(AntlionPose.Dive, progress));
                }
                else if (attack.Chases)
                {
                    // R3: under the sand at once, then a mound runs after its target (and sits still once it stops).
                    if (since < MoundDive) (pose, frame) = (AntlionPose.Dive, Through(AntlionPose.Dive, since / MoundDive));
                    else (pose, frame) = (AntlionPose.Burrow, moving ? Mathf.FloorToInt(Time.time * 16) % AntlionArt.FramesOf(AntlionPose.Burrow) : 0);
                }
                else
                {
                    var blow = AntlionBlowOf(attack);
                    var windUp = AntlionArt.WindUpOf(blow);
                    int strike = AntlionArt.StrikeFrame(blow);
                    float swing = strike * AntlionArt.FrameSeconds(blow, 0);
                    float wound = 1 - Mathf.Min(0.5f, swing / TelegraphOf(body, attack));
                    if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                    else
                    {
                        int f = AntlionBlowFrame(blow, (progress - wound) * TelegraphOf(body, attack));
                        (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                // From the strike on (or up out of the sand where a run ends), each frame for its own time.
                bool emerges = attack.Shape == AttackShape.Charge || attack.Chases;
                var blow = emerges ? AntlionPose.Emerge : AntlionBlowOf(attack);
                if (attack.Shape != AttackShape.Summon)
                {
                    int strike = emerges ? 0 : AntlionArt.StrikeFrame(blow);
                    float left = since;
                    for (int k = strike; k < AntlionArt.FramesOf(blow); k++)
                    {
                        left -= AntlionArt.FrameSeconds(blow, k);
                        if (left >= 0) continue;
                        (pose, frame) = (blow, k);
                        break;
                    }
                }
            }

            // Churning the sand for its spiral it turns round and round.
            if (attack != null && attack.Shape == AttackShape.Spiral && pose == AntlionPose.Churn)
                sector = (sector + Mathf.FloorToInt(Time.time * 14)) % 8;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 700000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose != AntlionPose.Enter && pose != AntlionPose.Walk && pose != AntlionPose.Idle && pose != AntlionPose.Burrow && pose != AntlionPose.Roar)
            {
                // The lunge begins: a hiss of sand.
                if (frame == 0 && AntlionArt.StrikeFrame(pose) > 0) Hear(Sound.Dig, body.Position, 0.7f);
            }
            body.Frame = code;
            string key = $"antlion/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => AntlionArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, AntlionArt.Floor / (float)AntlionArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + AntlionArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While it winds up: sand trickles and skips round it, swirls up as it rears for the sandstorm, pours into the
        // hole as it goes under; a run under the sand throws up a wake.
        void AntlionExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            // R3: the mound running after its target throws sand up off its back.
            if (view.BossState == BossState.Telegraph && attack != null && attack.Chases)
            {
                if (since < Time.deltaTime * 1.5f) Hear(Sound.Tunnel, feet, 0.8f);
                if (puff && since >= MoundDive) Chips(feet, Vector3.up - ahead * 0.6f, FxRamp.Dust, 2, 12, 60);
                return;
            }
            if (view.BossState == BossState.Telegraph && attack != null && attack.Shape != AttackShape.Summon)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                var windUp = attack.Shape == AttackShape.Charge ? AntlionPose.Dive : AntlionArt.WindUpOf(AntlionBlowOf(attack));
                switch (windUp)
                {
                    case AntlionPose.Scoop:
                    case AntlionPose.Plow:
                        // Sand heaped up on its head.
                        if (puff) Chips(feet + ahead * 1.2f, Vector3.up * 0.5f + ahead * 0.3f, FxRamp.Sand, 1);
                        break;
                    case AntlionPose.Dig:
                    case AntlionPose.Dive:
                        // Sand raked out behind it.
                        if (puff) Chips(feet - ahead * 0.8f + (Vector3)(Random.insideUnitCircle * 0.5f), -ahead + Vector3.up * 0.4f, FxRamp.Sand, 2, 18);
                        break;
                    case AntlionPose.Sink:
                    case AntlionPose.Submerge:
                        // Sand pouring in round it as it goes down; the floor trembling before the cave-in.
                        if (puff)
                        {
                            var from = feet + (Vector3)(Random.insideUnitCircle.normalized * 1.8f);
                            Chips(from, feet - from, FxRamp.Sand, 1, 20, 20);
                        }
                        if (windUp == AntlionPose.Submerge && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.08f * progress);
                        break;
                    case AntlionPose.Rear:
                        // Sand swirling up round it.
                        if (puff)
                        {
                            float a = Time.time * 9;
                            Chips(feet + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0) * 1.6f, new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * 0.5f + 0.6f, 0), FxRamp.Sand, 1, 18, 20);
                        }
                        break;
                }
            }
            if (view.BossState == BossState.Charge)
            {
                // A wake of sand thrown up behind the mound.
                var moved = body.Position - body.LastPosition;
                if (puff)
                {
                    Chips(feet, -moved + Vector3.up * 0.3f, FxRamp.Sand, 3, 20);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.05f);
                    Hear(Sound.Dig, feet, 0.4f);
                }
            }
        }

        // Where one of its blows lands, sand of its own over the blow's shape; false for anyone else's.
        bool AntlionLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "antlion") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Sand, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu };
            switch (id)
            {
                case "bite":
                    // The jaws snapping shut on the sand ahead: sand sprayed on through the wedge and a short stop.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, light: false);
                    c.Width = hazard.Width;
                    AddBossFx(BossFx.SandBurst, at, c);
                    AddFlash(at + along * hazard.Size * 0.5f, 1.2f, Color.white, FlashKind.Impact);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.07f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Bite, at, 1);
                    return true;
                case "sand_jet":
                    // A jet of sand along the line: streams of grains driven down it.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, chunks: false, light: false);
                    AddBossFx(BossFx.SandJet, at, c);
                    Hear(Sound.Splash, at, 0.6f);
                    return true;
                case "pit":
                case "sinkholes":
                    // The floor sliding into a funnel: grains running round and in, rings closing on a dark middle.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Whirl, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "sinkholes" ? 0.3f : 0.18f);
                    Hear(Sound.Dig, at, 1);
                    Hear(Sound.Thump, at, 0.7f);
                    return true;
                case "sand_trail":
                    // Each burst of the furrow: sand flung up out of the floor in a fan.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, chunks: false, light: false, wave: false);
                    c.Width = 0;
                    AddBossFx(BossFx.SandBurst, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.12f);
                    Hear(Sound.Dig, at, 0.7f);
                    return true;
                case "quicksand":
                case "sand_ripple":
                    // A ring of sand thrown out all round its band in streaks, over rolling dust.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    AddBossFx(BossFx.Rumble, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Dig, at, 0.8f);
                    return true;
                case "sandstorm":
                    // The front crashing down where it is shared (the gold circle burns gold): a whirl of sand thrown up.
                    BlastLanded(hazard, null, sparks: false, chunks: false, light: false);
                    Impact(at, 0.9f);
                    c.Big = true;
                    AddBossFx(BossFx.Whirl, at, c);
                    return true;
                case "sand_cross":
                    // Sand driven out along the arm.
                    BlastLanded(hazard, FxRamp.Sand, sparks: false, chunks: false, light: false);
                    AddBossFx(BossFx.SandJet, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Thump, at, 0.8f);
                    return true;
            }
            // Any other of its blows: the shared landing, in sand.
            BlastLanded(hazard, FxRamp.Sand, light: false);
            return true;
        }
    }
}
