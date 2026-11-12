using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The black knight of the hero's hut (2026-11-12, Docs/Bosses.md 흑철 기사 발두인): the samurai's opposite.
    // Every blow is slow to come and plain to read, the great hammer hauled up or drawn back through a long wind-up
    // with the whole body turning into it; and when it lands it lands like a falling wall: the picture stops, the
    // screen jolts, the floor cracks and throws up stone and a ring of dust, and the hammer stays in the floor a moment
    // before it is hauled out. It walks heavily, each step a thud, the hammer's head dragging behind it. ----
    sealed partial class WorldView
    {
        static readonly Color DustColor = new Color(0.62f, 0.56f, 0.5f);
        static readonly Color CrackColor = new Color(0.06f, 0.04f, 0.04f, 0.9f);
        static readonly Color SparkColor = new Color(1f, 0.82f, 0.5f);

        // The blow's frames once its telegraph ends: the swing too fast to see, the impact, held in the floor, hauled out.
        const float KnightSmear = 0.05f;
        const float KnightImpact = 0.17f;
        const float KnightHeld = 0.6f;
        const float KnightHauled = 0.95f;
        // How far through a leap's telegraph it springs up and away.
        const float KnightSpring = 0.55f;

        // A sweep is a wide cone; a narrower one is brought down from overhead.
        static bool Sweeps(BossAttack attack) => attack.Shape == AttackShape.Cone && attack.Angle >= 120;

        // Its pose from what it is doing: a wind-up of its own for each kind of blow, run through the telegraph in six
        // steps, and the blow in four frames after it.
        void AnimateKnight(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = KnightPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 1.2f) % 2;
            body.Shape.color = Color.white;
            if (dead)
            {
                frame = 0;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = KnightPose.Roar;
                frame = Mathf.FloorToInt(Time.time * 6) % 2;
            }
            else if (body.BossState == BossState.Charge)
            {
                pose = KnightPose.Charge;
                frame = Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // A step a foot about every fifth of a tile: slow, heavy.
                pose = KnightPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 4.5f) % 4;
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                frame = Mathf.Min(5, Mathf.FloorToInt(progress * 6));
                switch (attack.Shape)
                {
                    case AttackShape.Cone when Sweeps(attack):
                        pose = KnightPose.Wind;
                        break;
                    case AttackShape.Charge:
                        pose = KnightPose.Brace;
                        break;
                    case AttackShape.Leap:
                        // A deep crouch, then up and out of sight; it lands where the mark is.
                        if (progress < KnightSpring) (pose, frame) = (KnightPose.Crouch, Mathf.Min(5, Mathf.FloorToInt(progress / KnightSpring * 6)));
                        else
                        {
                            pose = KnightPose.Leap;
                            frame = Mathf.Min(3, Mathf.FloorToInt((progress - KnightSpring) / 0.12f));
                            body.Shape.color = new Color(1, 1, 1, Mathf.Clamp01(1 - (progress - KnightSpring - 0.3f) * 6));
                        }
                        break;
                    default:
                        // Every other blow: the hammer hauled up overhead, slowly.
                        pose = KnightPose.Raise;
                        break;
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && since < KnightHauled)
            {
                pose = attack.Shape switch
                {
                    AttackShape.Cone when Sweeps(attack) => KnightPose.Sweep,
                    AttackShape.Cone or AttackShape.Trail => KnightPose.Smash,
                    AttackShape.Charge => KnightPose.Brace,
                    _ => KnightPose.Pound,
                };
                frame = since < KnightSmear ? 0 : since < KnightImpact ? 1 : since < KnightHeld ? 2 : 3;
                if (pose == KnightPose.Brace) frame = 0;
            }

            // The body turns with the blow: away from the viewer as the hammer goes back, round toward the viewer as a
            // sweep comes across.
            int twist = pose switch
            {
                KnightPose.Raise or KnightPose.Wind => frame >= 2 ? -1 : 0,
                KnightPose.Sweep => frame <= 1 ? 1 : 0,
                _ => 0,
            };
            sector = Twist(sector, twist);

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 500000 + ((((int)pose * 8 + (int)view) * 8 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            // A foot coming down on a heavy step.
            if (pose == KnightPose.Walk && (frame == 1 || frame == 3) && body.KnightStep != frame) Thud(body, 0.07f, 0.5f);
            body.KnightStep = pose == KnightPose.Walk ? frame : -1;
            // The swing begins: a heavy whoosh.
            if ((pose == KnightPose.Smash || pose == KnightPose.Sweep || pose == KnightPose.Pound) && frame == 0) Hear(Sound.Heavy, body.Position, 1);
            body.Frame = code;
            string key = $"knight/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => KnightArt.Draw(view, pose, frame, phase);
            // Stood on the row of its feet, above the room left under them for the hammer's head.
            var pivot = new Vector2(0.5f, KnightArt.Floor / (float)KnightArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + KnightArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // A heavy foot, or the knight landing: dust out from under it and the ground shaking a little.
        void Thud(Body body, float shake, float volume)
        {
            var feet = body.Position;
            Kick(feet + new Vector3(0.5f, 0, 0), new Vector3(1, 0.2f, 0), DustColor, 2);
            Kick(feet + new Vector3(-0.5f, 0, 0), new Vector3(-1, 0.2f, 0), DustColor, 2);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, shake);
            Hear(Sound.Thump, feet, volume);
        }

        // While it winds up the ground trembles under it more and more, and for the strongest blows grit falls from the
        // roof; charging, it tears up the floor behind it.
        void KnightExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                if (puff && progress > 0.5f)
                {
                    // Pebbles skipping on the floor round its feet.
                    Kick(feet + (Vector3)(Random.insideUnitCircle * 1.6f), Vector3.up * 0.4f, RockColor, 1);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.05f * progress * (attack.Warning == AttackWarning.Strong ? 2 : 1));
                }
                if (puff && attack.Warning == AttackWarning.Strong && Random.value < progress)
                    Kick(feet + new Vector3(Random.Range(-4f, 4f), Random.Range(2f, 5f), 0), Vector3.down, DustColor, 1);
            }
            if (view.BossState == BossState.Charge)
            {
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff)
                {
                    Kick(feet, -moved, DustColor, 3);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.08f);
                    Hear(Sound.Thump, feet, 0.6f);
                }
            }
            if (view.BossState != BossState.Telegraph) body.Shape.color = Color.white;
        }

        // The floor breaking where a blow lands: dark cracks running out from it, jagged, slow to fade.
        void Cracks(Vector3 at, float reach, int count)
        {
            float px = 1f / Sprites.PixelsPerUnit;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * 360f / count;
                var from = at;
                float left = reach * Random.Range(0.6f, 1f);
                while (left > 0.1f)
                {
                    float step = Mathf.Min(left, Random.Range(0.4f, 0.9f));
                    angle += Random.Range(-35f, 35f);
                    var to = from + Quaternion.Euler(0, 0, angle) * Vector3.right * step;
                    var line = Renderer(_root.transform, "Crack", Shapes.Square, CrackColor);
                    line.transform.localPosition = (from + to) / 2;
                    line.transform.localRotation = Quaternion.Euler(0, 0, angle);
                    line.transform.localScale = new Vector3(step + px, (left > reach * 0.5f ? 2 : 1) * px, 1);
                    line.sortingOrder = 32;
                    _bits.Add(new Bit { Renderer = line, Velocity = Vector3.zero, Color = CrackColor, Life = 1.8f, Drag = 0, LateFade = true });
                    from = to;
                    left -= step;
                }
            }
        }

        // A blow landing: the picture stopped for a moment, the screen jolted, stone and dust thrown out, a ring of
        // dust rolling away, the floor cracked, sparks off the steel.
        void Impact(Vector3 at, float weight)
        {
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.1f + 0.06f * weight);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.25f + 0.25f * weight);
            AddFlash(at, 0.9f + 0.6f * weight, Color.white, FlashKind.Impact);
            AddFlash(at, 1.6f + 1.4f * weight, DustColor, FlashKind.Ring);
            AddFlash(at, 1.2f + 0.8f * weight, DustColor, FlashKind.Burst);
            for (int i = 0; i < 8; i++)
            {
                var way = Quaternion.Euler(0, 0, i * 45 + Random.Range(-15f, 15f)) * Vector3.right;
                Kick(at, way + Vector3.up * 0.6f, i % 2 == 0 ? RockColor : DirtColor, 2);
            }
            Kick(at, Vector3.up, SparkColor, 3);
            Cracks(at, 1.2f + 0.8f * weight, 5 + Mathf.RoundToInt(weight * 2));
            Hear(Sound.Boom, at, 0.6f + 0.3f * weight);
            Hear(Sound.Thump, at, 1);
        }

        // Where one of the knight's blows lands, a burst of its own; false for anyone else's.
        bool KnightLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "black_knight") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            bool strong = attack.Warning == AttackWarning.Strong;
            switch (attack.Shape)
            {
                case AttackShape.Cone when Sweeps(attack):
                {
                    // The sweep: dust thrown along the arc and one heavy jolt.
                    float half = attack.Angle / 2;
                    for (float a = -half; a <= half; a += 20)
                    {
                        var dir = Quaternion.Euler(0, 0, a) * along;
                        Kick(at + dir * hazard.Size * 0.8f, dir, DustColor, 2);
                    }
                    AddFlash(at + along * hazard.Size * 0.5f, hazard.Size * 0.9f, DustColor, FlashKind.Ring);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.09f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.35f);
                    Hear(Sound.Thump, at, 1);
                    return true;
                }
                case AttackShape.Cone:
                    // The hammer comes down where the wedge ends.
                    Impact(at + along * hazard.Size * 0.65f, 1);
                    return true;
                case AttackShape.Trail:
                    // Each burst of the shockwave: stone bursting up out of the floor.
                    AddFlash(at, hazard.Size * 1.3f, DustColor, FlashKind.Burst);
                    Kick(at, Vector3.up, RockColor, 4);
                    Cracks(at, hazard.Size, 3);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Thump, at, 0.7f);
                    return true;
                case AttackShape.Cross:
                    // The floor split along both arms.
                    foreach (var arm in new[] { along, new Vector3(-along.y, along.x, 0) })
                    {
                        for (float d = -hazard.Size; d <= hazard.Size; d += 1.2f)
                        {
                            Kick(at + arm * d, Vector3.up, d % 2.4f < 1.2f ? RockColor : DustColor, 1);
                            Cracks(at + arm * d, 0.6f, 2);
                        }
                    }
                    Impact(at, 1.2f);
                    return true;
                case AttackShape.Circle when attack.Target == AttackTarget.Each:
                    // Rock from the roof shattering.
                    AddFlash(at, hazard.Size * 1.2f, RockColor, FlashKind.Burst);
                    Kick(at, Vector3.up, RockColor, 5);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.15f);
                    Hear(Sound.Break, at, 0.8f);
                    return true;
                case AttackShape.Circle:
                case AttackShape.Leap:
                {
                    // The hammer pounded into the floor at its feet (or the knight landing on its mark): the whole circle
                    // breaks, the heaviest blows whiten the screen.
                    float weight = strong ? 2.2f : 1.5f;
                    Impact(at, weight);
                    AddFlash(at, hazard.Size, DustColor, FlashKind.Ring);
                    Cracks(at, hazard.Size * 0.9f, strong ? 10 : 7);
                    if (strong) ScreenFlash(new Color(1, 0.95f, 0.85f, 0.4f), 0.14f);
                    return true;
                }
            }
            return false;
        }
    }
}
