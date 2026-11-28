using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The cave warden of the fallen shrine (2026-11-26, Docs/Bosses.md 동굴 수호자): the first boss, a golem of
    // mossy rock. It teaches a new player to read a boss: every attack has its own wind-up of the whole body, big and
    // slow enough to see from across the room (both fists hauled overhead, a fist drawn back, a foot lifted, the arms
    // folded over a core that blazes brighter and brighter), the blow lands as the mark fills, and after it the fists
    // stay in the floor a moment, the opening to hit back. Its two roof attacks are told apart from the floor blows:
    // it punches the roof first and the rocks come down after. ----
    sealed partial class WorldView
    {
        static readonly Color AmberColor = new Color(1f, 0.7f, 0.25f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static WardenPose WardenBlowOf(BossAttack attack) => attack.Id switch
        {
            "slam" => WardenPose.Slam,
            "sweep" => WardenPose.Sweep,
            "rock_burst" or "rock_storm" => WardenPose.Burst,
            "quake" => WardenPose.Stomp,
            "aftershock" => WardenPose.Punch,
            "rock_trail" => WardenPose.Gouge,
            "shockwave" or "cave_in" => WardenPose.Pound,
            "meteor" => WardenPose.Uppercut,
            _ => attack.Shape switch
            {
                AttackShape.Cone => WardenPose.Sweep,
                AttackShape.Burst or AttackShape.Spiral => WardenPose.Burst,
                AttackShape.Ring => WardenPose.Pound,
                AttackShape.Trail => WardenPose.Gouge,
                _ => WardenPose.Slam,
            },
        };

        static WardenPose WardenWindUpOf(BossAttack attack) => attack.Id == "cave_in" ? WardenPose.Gather : WardenArt.WindUpOf(WardenBlowOf(attack));

        // How far through the telegraph the blow lands: at the end for the floor blows (the fists meet the floor as the
        // mark fills), early for the roof (it punches the roof, and the rocks it brings down land as the marks fill).
        static float WardenStrikeAt(BossAttack attack) => attack.Id == "meteor" ? 0.35f : attack.Id == "cave_in" ? 0.5f : 1;

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int WardenBlowFrame(WardenPose blow, float seconds)
        {
            for (int f = 0; f < WardenArt.FramesOf(blow); f++)
            {
                seconds -= WardenArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // Its pose from what it is doing. A wind-up runs through its telegraph, and the blow's swing frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateWarden(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = WardenPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 2.2f) % WardenArt.FramesOf(WardenPose.Idle);
            body.Shape.color = Color.white;
            int Through(WardenPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * WardenArt.FramesOf(p)), 0, WardenArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // A heap of mossy stone among the ruins; woken, its eyes and core light and it rises (WardenPose.Enter).
                pose = WardenPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : WardenArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The arms flung open, then shaking there.
                pose = WardenPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.07f);
                frame = rise < WardenArt.RoarRise ? rise : WardenArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                pose = WardenPose.Charge;
                frame = Mathf.FloorToInt(Time.time * 18) % WardenArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // A heavy stride each way about every tile.
                pose = WardenPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 8) % WardenArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                if (attack.Shape == AttackShape.Charge)
                {
                    (pose, frame) = (WardenPose.Brace, Through(WardenPose.Brace, progress));
                }
                else
                {
                    var blow = WardenBlowOf(attack);
                    var windUp = WardenWindUpOf(attack);
                    float strikeAt = WardenStrikeAt(attack);
                    int strike = WardenArt.StrikeFrame(blow);
                    float swing = strike * WardenArt.FrameSeconds(blow, 0);
                    float wound = strikeAt - Mathf.Min(0.5f * strikeAt, swing / TelegraphOf(body, attack));
                    if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                    else
                    {
                        int f = WardenBlowFrame(blow, (progress - wound) * TelegraphOf(body, attack));
                        if (strikeAt >= 1) (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                        else if (f >= 0) (pose, frame) = (blow, f);
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                if (attack.Shape == AttackShape.Charge)
                {
                    // Out of the run: straightening up again.
                    int back = WardenArt.FramesOf(WardenPose.Brace) - 1 - Mathf.FloorToInt(since / 0.07f);
                    if (back >= 0) (pose, frame) = (WardenPose.Brace, back);
                }
                else if (WardenStrikeAt(attack) >= 1 && attack.Shape != AttackShape.Summon)
                {
                    // From the strike on, each frame for its own time.
                    var blow = WardenBlowOf(attack);
                    int strike = WardenArt.StrikeFrame(blow);
                    float left = since;
                    for (int k = strike; k < WardenArt.FramesOf(blow); k++)
                    {
                        left -= WardenArt.FrameSeconds(blow, k);
                        if (left >= 0) continue;
                        (pose, frame) = (blow, k);
                        break;
                    }
                }
            }

            // The body turns with the motion: away from the viewer as a fist is drawn back, round toward the viewer as
            // the sweep comes across.
            sector = Twist(sector, WardenArt.TwistOf(pose, frame));
            _wardenSector = sector;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 600000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            // A foot coming down on a heavy step (frames 2 and 6 of the stride).
            if (pose == WardenPose.Walk && (frame == 2 || frame == 6) && body.KnightStep != frame) Thud(body, 0.04f, 0.4f);
            body.KnightStep = pose == WardenPose.Walk ? frame : -1;
            if (pose != WardenPose.Enter && pose != WardenPose.Walk && pose != WardenPose.Idle && pose != WardenPose.Charge && pose != WardenPose.Roar)
            {
                // The swing begins: a heavy whoosh; a roof punch lands with grit raining down.
                if (frame == 0 && WardenArt.StrikeFrame(pose) > 0) Hear(Sound.Heavy, body.Position, 0.9f);
                if (pose == WardenPose.Uppercut && frame == WardenArt.StrikeFrame(pose))
                    RoofStruck(body, false);
                if (pose == WardenPose.Pound && attack != null && attack.Id == "cave_in" && frame == WardenArt.StrikeFrame(pose))
                {
                    Impact(body.Position + new Vector3(0, -0.3f, 0), 1.4f);
                    RoofStruck(body, true);
                }
            }
            body.Frame = code;
            string key = $"warden/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => WardenArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, WardenArt.Floor / (float)WardenArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + WardenArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // A fist (or both) driven into the roof: grit and stones rain down round the room, the screen jolts.
        void RoofStruck(Body body, bool hard)
        {
            var feet = body.Position;
            for (int i = 0; i < (hard ? 14 : 8); i++)
                Bits(feet + new Vector3(Random.Range(-5f, 5f), Random.Range(2.5f, 5f), 0), Vector3.down, i % 3 == 0 ? RockColor : DustColor, 1);
            AddFlash(feet + new Vector3(0, 2.2f, 0), 1.2f, DustColor, FlashKind.Burst);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, hard ? 0.35f : 0.22f);
            Hear(Sound.Break, feet, 0.9f);
            Hear(Sound.Thump, feet, 0.8f);
        }

        // While it winds up: pebbles skip round its feet, the core draws in sparks as it folds over it, the roof sheds
        // grit before the cave-in, dust is pawed up behind before a run; in the last phase embers drift off its core.
        void WardenExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var core = feet + new Vector3(0, 1.3f, 0);
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            if (view.BossState == BossState.Telegraph && attack != null && attack.Shape != AttackShape.Summon)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                var windUp = attack.Shape == AttackShape.Charge ? WardenPose.Brace : WardenWindUpOf(attack);
                if (puff && progress > 0.5f) Bits(feet + (Vector3)(Random.insideUnitCircle * 1.4f), Vector3.up * 0.4f, RockColor, 1);
                switch (windUp)
                {
                    case WardenPose.Hunch:
                        // Sparks drawn into the core from all round.
                        if (puff)
                        {
                            var from = core + (Vector3)(Random.insideUnitCircle.normalized * 1.4f);
                            Bits(from, core - from, AmberColor, 2);
                        }
                        break;
                    case WardenPose.Gather:
                        if (puff)
                        {
                            Bits(feet + new Vector3(Random.Range(-5f, 5f), Random.Range(2f, 5f), 0), Vector3.down, DustColor, 1);
                            Bits(core, Vector3.up, AmberColor, 1);
                        }
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.03f + 0.08f * progress);
                        break;
                    case WardenPose.Brace:
                        if (puff) Bits(feet - ahead * 0.8f, -ahead + Vector3.up * 0.3f, DirtColor, 2);
                        break;
                    case WardenPose.Lift:
                        if (Settings.ScreenShake && progress > 0.6f) _shake = Mathf.Max(_shake, 0.03f);
                        break;
                }
            }
            if (view.BossState == BossState.Charge)
            {
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff)
                {
                    Bits(feet, -moved, DustColor, 3);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.06f);
                    Hear(Sound.Thump, feet, 0.5f);
                }
            }
            if (view.Phase >= 3 && puff && Random.value < 0.3f) Bits(core, Vector3.up, RageColor, 1);
        }

        // Where one of its blows lands, stone and dust of its own over the blow's shape; false for anyone else's.
        bool WardenLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "cave_warden") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Dust, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width };
            switch (id)
            {
                case "slam":
                    // Both fists into the floor: the circle breaks (Impact cracks it), dust and stones thrown out.
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false, wave: false);
                    Impact(at, 1);
                    c.Plain = true;
                    AddBossFx(BossFx.Slam, at, c);
                    return true;
                case "sweep":
                    // The arm through the wedge: a smear of dust, dust rolling off its arc, stones flung on, one heavy jolt.
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false);
                    AddBossFx(BossFx.Sweep, at, c);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.07f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.3f);
                    Hear(Sound.Thump, at, 1);
                    return true;
                case "quake":
                    // The floor bursting up under the feet it was stamped for.
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false, wave: false);
                    AddBossFx(BossFx.Slam, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.25f);
                    Hear(Sound.Boom, at, 0.6f);
                    return true;
                case "shockwave":
                case "aftershock":
                    // A ring of broken floor rolling out: dust and stones all round the band.
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    AddBossFx(BossFx.Rumble, at, c);
                    if (id == "shockwave") Impact(at, 0.8f);
                    else
                    {
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.25f);
                        Hear(Sound.Boom, at, 0.7f);
                    }
                    return true;
                case "rock_trail":
                    // Each burst of the gouged line: a spike of stone up through the floor.
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false, wave: false);
                    AddBossFx(BossFx.Spike, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.15f);
                    Hear(Sound.Thump, at, 0.7f);
                    return true;
                case "meteor":
                case "cave_in":
                {
                    // Rock from the roof shattering; the cave-in harder.
                    bool hard = id == "cave_in";
                    BlastLanded(hazard, FxRamp.Dust, sparks: false, light: false, wave: false);
                    c.Big = hard;
                    AddBossFx(BossFx.Slam, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, hard ? 0.3f : 0.18f);
                    Hear(Sound.Break, at, 0.9f);
                    if (hard) ScreenFlash(new Color(1, 0.92f, 0.8f, 0.25f), 0.05f);
                    return true;
                }
            }
            // Any other of its blows: the shared landing, in dust.
            BlastLanded(hazard, FxRamp.Dust, light: false);
            return true;
        }

        // The way it last faced, for its fall.
        int _wardenSector = 6;

        // ---- Its fall (2026-11-27, the person: "쓰러지는 연출도 만들어줘"): it does not burst like the other bosses.
        // Staggered back by the last blow with a groan, its core flickers and goes out (a last burst of amber), it
        // drops to its knees with a thud, slumps forward onto its fists, and the stone gives way into a heap of
        // rubble with a crash, dust and stones thrown out; the heap lies a moment, greys and fades. ----

        void WardenFalls(Vector3 at)
        {
            var (view, turned) = BossSectors[_wardenSector];
            var renderer = Renderer(_root.transform, "Fallen warden", null, Color.white);
            renderer.transform.localPosition = at;
            renderer.sortingOrder = FootOrder(at.y);
            renderer.flipX = turned;
            _falls.Add(new Fall { Renderer = renderer, Since = Time.time, View = view, Turned = turned, Warden = true });
            HearRoar("cave_warden", at, 0.7f);
            Hear(Sound.Break, at, 0.6f);
            AddFlash(at + new Vector3(0, 1.3f, 0), 1.2f, AmberColor, FlashKind.Impact);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.18f);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.35f);
            ScreenFlash(new Color(1, 0.95f, 0.85f, 0.3f), 0.15f);
        }

        const float WardenFallFrameSeconds = 0.085f;
        const float WardenFallLie = 1.4f;
        const float WardenFallFade = 1.0f;

        // One frame of a fall: its pose, the beats as they come, greying and fading once it lies still. False once gone.
        bool UpdateWardenFall(Fall fall)
        {
            float age = Time.time - fall.Since;
            int frames = WardenArt.FramesOf(WardenPose.Die);
            float down = frames * WardenFallFrameSeconds;
            if (age > down + WardenFallLie + WardenFallFade)
            {
                Object.Destroy(fall.Renderer.gameObject);
                return false;
            }
            int frame = Mathf.Min(frames - 1, Mathf.FloorToInt(age / WardenFallFrameSeconds));
            var at = fall.Renderer.transform.localPosition;
            if (frame != fall.Frame)
            {
                int last = fall.Frame;
                fall.Frame = frame;
                bool Reached(int f) => last < f && frame >= f;
                var view = fall.View;
                fall.Renderer.sprite = Sprites.Get($"warden/{view}/{WardenPose.Die}/{frame}/1", () => WardenArt.Draw(view, WardenPose.Die, frame, 1), new Vector2(0.5f, WardenArt.Floor / (float)WardenArt.Height));
                // The core going out: a last burst of amber sparks.
                if (Reached(WardenArt.DieOut))
                {
                    Explode(at + new Vector3(0, 1.2f, 0), 1.1f, FxRamp.Fire, air: true);
                    Bits(at + new Vector3(0, 1.2f, 0), Vector3.up, AmberColor, 6);
                    Hear(Sound.Break, at, 0.7f);
                }
                if (Reached(WardenArt.DieKnees))
                {
                    Bits(at + new Vector3(0.6f, 0, 0), new Vector3(1, 0.2f, 0), DustColor, 3);
                    Bits(at + new Vector3(-0.6f, 0, 0), new Vector3(-1, 0.2f, 0), DustColor, 3);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Thump, at, 1);
                }
                // The stone giving way: the heaviest crash of the fight.
                if (Reached(WardenArt.DieCrumble))
                {
                    Impact(at, 1.2f);
                    for (int i = 0; i < 10; i++)
                    {
                        var way = Quaternion.Euler(0, 0, i * 36 + Random.Range(-12f, 12f)) * Vector3.right;
                        Bits(at + way * 0.4f + new Vector3(0, 0.5f, 0), way + Vector3.up * 0.8f, i % 2 == 0 ? RockColor : DustColor, 2);
                    }
                    AddFlash(at, 3f, DustColor, FlashKind.Ring);
                }
                // Grit trickling off it as it breaks up.
                if (frame >= WardenArt.DieCrumble && frame % 3 == 0) Bits(at + new Vector3(Random.Range(-0.8f, 0.8f), 0.8f, 0), Vector3.down * 0.4f, DustColor, 1);
            }
            float gone = Mathf.Clamp01((age - down - WardenFallLie) / WardenFallFade);
            float grey = Mathf.Clamp01((age - down * 0.7f) / 0.8f);
            fall.Renderer.color = new Color(1 - 0.3f * grey, 1 - 0.3f * grey, 1 - 0.3f * grey, 1 - gone);
            return true;
        }
    }
}
