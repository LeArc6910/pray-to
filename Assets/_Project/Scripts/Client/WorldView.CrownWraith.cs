using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The crown wraith of the misty harbour (2026-11-28, Docs/Bosses.md 왕관의 망령): a drowned king of the demon
    // army's sunken fleet under a rusted crown, floating on a ghostly tail. Every attack has its own wind-up of the whole
    // body (the clawed hand drawn up and back before the rake, both arms raised to the dark before the bolts fall, a
    // spear of lightning forming in the drawn-back hand, hunched and going thin before the rush, the crown lifting off
    // its head before the ring, rising with the crown storming before the brands, a ball of lightning held overhead
    // before the shared blow, the fists pulled in before the pulse), the blow lands as the mark fills, and after the
    // rake, the rush and the shared blow it stays low a moment, the opening to hit back. Its blows are lightning and
    // drowned souls, never water: bolts out of the dark, a thrown spear, claws raking, lightning round the ring. ----
    sealed partial class WorldView
    {
        static readonly Color WraithBolt = new Color(1f, 0.9f, 0.45f);
        static readonly Color WraithSoul = new Color(0.6f, 0.95f, 0.85f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static CrownWraithPose WraithBlowOf(BossAttack attack) => attack.Id switch
        {
            "grasp" => CrownWraithPose.Rake,
            "lightning_fall" => CrownWraithPose.Call,
            "soul_lance" => CrownWraithPose.Hurl,
            "phantom_rush" => CrownWraithPose.Rush,
            "crown_ring" => CrownWraithPose.Flare,
            "storm_brand" => CrownWraithPose.Brand,
            "sunken_hands" => CrownWraithPose.Smite,
            "mirror" => CrownWraithPose.Call,
            _ => attack.Shape switch
            {
                AttackShape.Cone => CrownWraithPose.Rake,
                AttackShape.Line or AttackShape.Cross => CrownWraithPose.Hurl,
                AttackShape.Charge or AttackShape.Trail => CrownWraithPose.Rush,
                AttackShape.Ring or AttackShape.Burst => CrownWraithPose.Flare,
                AttackShape.Share => CrownWraithPose.Smite,
                _ => CrownWraithPose.Call,
            },
        };

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int WraithBlowFrame(CrownWraithPose blow, float seconds)
        {
            for (int f = 0; f < CrownWraithArt.FramesOf(blow); f++)
            {
                seconds -= CrownWraithArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // The way it faced last, for its fall.
        int _wraithSector = 6;

        // Its pose from what it is doing. A wind-up runs through its telegraph, and the blow's lunge frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateCrownWraith(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = CrownWraithPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle);
            // Gone to mist (R4b: its interlude, and the wind-up before its images form): barely there.
            bool misty = body.BossState == BossState.Interlude || body.BossState == BossState.Telegraph && attack?.Shape == AttackShape.Clone;
            body.Shape.color = misty ? new Color(1, 1, 1, 0.2f) : Color.white;
            int Through(CrownWraithPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * CrownWraithArt.FramesOf(p)), 0, CrownWraithArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Only its crown on the chapel floor; woken, the crown rises and the body gathers under it.
                pose = CrownWraithPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : CrownWraithArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The arms spread and the crown sparking, then shaking there.
                pose = CrownWraithPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < CrownWraithArt.RoarRise ? rise : CrownWraithArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // The rush itself: thrown far ahead, thin as a ghost.
                pose = CrownWraithPose.Rush;
                frame = CrownWraithArt.StrikeFrame(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Drifting over the floor, leant into its way.
                pose = CrownWraithPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 6) % CrownWraithArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                var blow = WraithBlowOf(attack);
                var windUp = CrownWraithArt.WindUpOf(blow);
                int strike = CrownWraithArt.StrikeFrame(blow);
                float swing = strike * CrownWraithArt.FrameSeconds(blow, 0);
                float wound = 1 - Mathf.Min(0.5f, swing / TelegraphOf(body, attack));
                if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                else
                {
                    int f = WraithBlowFrame(blow, (progress - wound) * TelegraphOf(body, attack));
                    (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape != AttackShape.Summon)
            {
                // From the strike on, each frame for its own time.
                var blow = WraithBlowOf(attack);
                int strike = CrownWraithArt.StrikeFrame(blow);
                float left = since;
                for (int k = strike; k < CrownWraithArt.FramesOf(blow); k++)
                {
                    left -= CrownWraithArt.FrameSeconds(blow, k);
                    if (left >= 0) continue;
                    (pose, frame) = (blow, k);
                    break;
                }
            }

            if (!dead) _wraithSector = sector;
            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 900000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose != CrownWraithPose.Enter && pose != CrownWraithPose.Walk && pose != CrownWraithPose.Idle && pose != CrownWraithPose.Roar)
            {
                // The lunge begins: a crack of lightning.
                if (frame == 0 && CrownWraithArt.StrikeFrame(pose) > 0) Hear(Sound.Zap, body.Position, 0.6f);
            }
            body.Frame = code;
            string key = $"wraith/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => CrownWraithArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, CrownWraithArt.Floor / (float)CrownWraithArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + CrownWraithArt.BodyHeight / (float)Sprites.PixelsPerUnit;
            // Its images wear the same pose (R4b, WorldView.Mirror.cs).
            KeepWraithLook(view, pose, frame, phase, turned);
        }

        // While it winds up: sparks gathering in the clawed hand, the air crackling round it as it calls the bolts,
        // mist streaming off it as it goes thin, sparks rising off the lifted crown, the hall shaking as it storms; and
        // on the rush, pale afterimages of itself left behind it.
        void CrownWraithExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            var head = feet + new Vector3(0, 2.6f, 0);
            if (view.BossState == BossState.Charge)
            {
                WraithEcho(body);
                return;
            }
            body.Echoing = false;
            if (view.BossState != BossState.Telegraph || attack == null || attack.Shape == AttackShape.Summon) return;
            float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
            switch (CrownWraithArt.WindUpOf(WraithBlowOf(attack)))
            {
                case CrownWraithPose.Clutch:
                case CrownWraithPose.Draw:
                case CrownWraithPose.Clench:
                    // Sparks drawn into the hand.
                    if (puff) Chips(feet + new Vector3(0, 1.6f, 0) - ahead * 0.5f + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.up * 0.4f, FxRamp.Bolt, 1, 8, 120);
                    break;
                case CrownWraithPose.Beckon:
                case CrownWraithPose.Lift:
                    // The air crackling over it, sparks going up off the crown.
                    if (puff) Chips(head + (Vector3)(Random.insideUnitCircle * 0.5f), Vector3.up, FxRamp.Bolt, 1, 14, 60);
                    break;
                case CrownWraithPose.Hunch:
                    // Mist streaming off it as it goes thin.
                    if (puff) Chips(feet + new Vector3(0, 1.2f, 0) + (Vector3)(Random.insideUnitCircle * 0.7f), -ahead + Vector3.up * 0.3f, FxRamp.Spirit, 2, 14, 50);
                    break;
                case CrownWraithPose.Storm:
                case CrownWraithPose.Loom:
                    // The storm gathering: sparks and souls drawn up to it, the hall shaking more and more.
                    if (puff)
                    {
                        Chips(head + (Vector3)(Random.insideUnitCircle * 0.8f), Vector3.up, FxRamp.Bolt, 1, 16, 90);
                        Chips(feet + (Vector3)(Random.insideUnitCircle * 1.6f), Vector3.up, FxRamp.Spirit, 1, 18, 30);
                    }
                    if (attack.Id == "storm_brand" && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.03f + 0.08f * progress);
                    break;
            }
        }

        // Afterimages of its rush: its silhouette left behind, pale sea green and white in turn, each fading where it
        // stands, spaced by distance as the samurai's are.
        void WraithEcho(Body body)
        {
            if (WhiteOf(body) == null) return;
            var now = body.Shape.transform.position - _root.transform.position;
            if (!body.Echoing)
            {
                body.Echoing = true;
                body.EchoFrom = now;
                return;
            }
            var step = now - body.EchoFrom;
            float distance = step.magnitude;
            int count = Mathf.Min(10, Mathf.FloorToInt(distance / 0.7f));
            for (int k = 1; k <= count; k++)
            {
                body.Echoes++;
                var color = body.Echoes % 2 == 0 ? new Color(0.75f, 1f, 0.92f, 0.5f) : new Color(0.95f, 1f, 1f, 0.42f);
                var ghost = Renderer(_root.transform, "Wraith afterimage", body.Glow.sprite, color);
                ghost.transform.localPosition = body.EchoFrom + step * (k * 0.7f / distance);
                ghost.flipX = body.Shape.flipX;
                ghost.sortingOrder = body.Shape.sortingOrder - 1;
                _bits.Add(new Bit { Renderer = ghost, Velocity = Vector3.up * 0.4f, Color = color, Life = 0.4f });
            }
            if (count > 0) body.EchoFrom = distance > 10 * 0.7f ? now : body.EchoFrom + step * (count * 0.7f / distance);
            if (Time.time < body.EchoAt) return;
            body.EchoAt = Time.time + 0.06f;
            Chips(body.Position + new Vector3(0, 1f, 0), body.LastPosition - body.Position, FxRamp.Bolt, 1, 10, 50);
        }

        // Where one of its blows lands, lightning and souls of its own over the blow's shape; false for anyone else's.
        bool CrownWraithLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "crown_wraith") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Bolt, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu, Seed = hazard.Id };
            switch (id)
            {
                case "grasp":
                    // The claws of its hand raked through the wedge, crackling.
                    BlastLanded(hazard, FxRamp.Spirit, sparks: false, chunks: false, light: false);
                    c.Width = hazard.Width;
                    AddBossFx(BossFx.Talons, at, c);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.16f);
                    Hear(Sound.Hit, at, 0.8f);
                    Hear(Sound.Zap, at, 0.7f);
                    return true;
                case "lightning_fall":
                case "storm_brand":
                    // A bolt out of the dark onto each circle (the brands the heavier, struck twice).
                    BlastLanded(hazard, FxRamp.Bolt, sparks: false, chunks: false, light: false, wave: false);
                    c.Big = id == "storm_brand";
                    AddBossFx(BossFx.Thunder, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, c.Big ? 0.22f : 0.14f);
                    Hear(Sound.Thunder, at, c.Big ? 1 : 0.7f);
                    return true;
                case "soul_lance":
                    // A spear of lightning thrown down the line.
                    BlastLanded(hazard, FxRamp.Bolt, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Lance, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.14f);
                    Hear(Sound.Zap, at, 1);
                    return true;
                case "crown_ring":
                case "soul_ripple":
                    // Lightning running round the band (the ripple the smaller, of souls).
                    BlastLanded(hazard, id == "soul_ripple" ? FxRamp.Spirit : FxRamp.Bolt, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    c.Plain = id == "soul_ripple";
                    AddBossFx(BossFx.ArcRing, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, c.Plain ? 0.08f : 0.18f);
                    Hear(c.Plain ? Sound.Nova : Sound.Zap, at, c.Plain ? 0.6f : 0.9f);
                    return true;
                case "deep_share":
                    // The ball brought down where it is shared (the gold circle burns gold): the heaviest bolt.
                    BlastLanded(hazard, null, sparks: false, chunks: false, light: false);
                    Impact(at, 0.9f);
                    c.Big = true;
                    AddBossFx(BossFx.Thunder, at, c);
                    Hear(Sound.Thunder, at, 1);
                    return true;
            }
            // Any other of its blows: the shared landing and what lightning does over it.
            return false;
        }

        // ---- Its fall: thrown back, the lightning flickering out, the body coming apart into mist from the tail up, and
        // the crown dropping to the floor, where it lies a moment and fades. ----

        void CrownWraithFalls(Vector3 at)
        {
            var (view, turned) = BossSectors[_wraithSector];
            var renderer = Renderer(_root.transform, "Fallen wraith", null, Color.white);
            renderer.transform.localPosition = at;
            renderer.sortingOrder = FootOrder(at.y);
            renderer.flipX = turned;
            _falls.Add(new Fall { Renderer = renderer, Since = Time.time, View = view, Turned = turned, Wraith = true });
            HearRoar("crown_wraith", at, 0.8f);
            Hear(Sound.Thunder, at, 0.7f);
            AddFlash(at + new Vector3(0, 1.8f, 0), 1.4f, WraithBolt, FlashKind.Impact);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.18f);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.3f);
            ScreenFlash(new Color(1, 0.97f, 0.85f, 0.32f), 0.15f);
        }

        const float WraithFallFrameSeconds = 0.085f;
        const float WraithFallLie = 1.2f;
        const float WraithFallFade = 1.0f;

        // One frame of its fall: its pose, mist coming off it as it comes apart, the crown's clang, fading once the
        // crown lies still. False once gone.
        bool UpdateWraithFall(Fall fall)
        {
            float age = Time.time - fall.Since;
            int frames = CrownWraithArt.FramesOf(CrownWraithPose.Die);
            float down = frames * WraithFallFrameSeconds;
            if (age > down + WraithFallLie + WraithFallFade)
            {
                Object.Destroy(fall.Renderer.gameObject);
                return false;
            }
            int frame = Mathf.Min(frames - 1, Mathf.FloorToInt(age / WraithFallFrameSeconds));
            var at = fall.Renderer.transform.localPosition;
            if (frame != fall.Frame)
            {
                int last = fall.Frame;
                fall.Frame = frame;
                var view = fall.View;
                fall.Renderer.sprite = Sprites.Get($"wraith/{view}/{CrownWraithPose.Die}/{frame}/1", () => CrownWraithArt.Draw(view, CrownWraithPose.Die, frame, 1),
                    new Vector2(0.5f, CrownWraithArt.Floor / (float)CrownWraithArt.Height));
                // Coming apart: mist and souls lifting off it.
                if (frame >= 10 && frame < CrownWraithArt.FallCrownDown && frame % 2 == 0)
                    Chips(at + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.8f), 0), Vector3.up, FxRamp.Spirit, 2, 16, 60);
                // The crown hits the floor.
                if (last < CrownWraithArt.FallCrownDown && frame >= CrownWraithArt.FallCrownDown)
                {
                    Hear(Sound.Hammer, at, 0.6f);
                    Bits(at, Vector3.up, WraithBolt, 3);
                    AddFlash(at, 1.4f, WraithSoul, FlashKind.Ring);
                }
            }
            float gone = Mathf.Clamp01((age - down - WraithFallLie) / WraithFallFade);
            fall.Renderer.color = new Color(1, 1, 1, 1 - gone);
            return true;
        }
    }
}
