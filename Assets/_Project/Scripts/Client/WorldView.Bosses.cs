using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // How plainly a boss's attacks are announced (2026-11-12, AttackWarning, Docs/Bosses.md 광맥 포식자 그룸): an
    // attack the sheet marks none lays nothing on the ground and gives no glow, only the boss's own wind-up shows it;
    // the strongest (strong) show their area and a warning sign over the boss. And the vein eater's own wind-ups and
    // blows: a pose for each kind of attack (VeinEaterArt), dust, a mound running underground, rocks falling from the
    // roof, and a burst of its own where each blow lands.
    sealed partial class WorldView
    {
        static readonly Color DirtColor = new Color(0.5f, 0.37f, 0.26f);
        static readonly Color RockColor = new Color(0.6f, 0.55f, 0.5f);
        static readonly Color OreColor = new Color(1f, 0.62f, 0.25f);
        static readonly Color RageColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color SlashColor = new Color(1f, 0.92f, 0.8f);

        static Sprite WarningSprite => Sprites.Centered("fx/warning", EffectArt.WarningSign);
        static Sprite RockSprite => Sprites.Centered("shot/rock", () => EffectArt.Rock(7));
        static Sprite BoulderSprite => Sprites.Centered("fx/boulder", () => EffectArt.Rock(13));
        static Sprite MoundSprite => Sprites.Standing("fx/mound", () =>
        {
            var image = new PixelImage(20, 7);
            for (int x = 0; x < 20; x++)
            {
                int h = (int)Mathf.Round(6 * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((x - 9.5f) / 10f, 2))));
                for (int y = 0; y < h; y++) image[x, 6 - y] = y == h - 1 ? new Rgba(150, 116, 84) : (x + y) % 3 == 0 ? new Rgba(82, 62, 48) : new Rgba(110, 82, 60);
            }
            return image.Outlined(new Rgba(24, 16, 12));
        });

        // The boss in view, for which attack laid a hazard (its cause) and whose shots are rocks.
        BossDefinition _bossInView;

        // For the playtests: marks drawn on the ground (an unmarked attack's are kept but clear), and whether a
        // warning sign shows over a boss.
        public int MarksShown => _marks.Values.Count(m => m.State.Warning != AttackWarning.Hidden);
        public bool WarningShown => _others.Values.Any(b => b.Sign != null && b.Sign.enabled);

        BossAttack AttackOf(HazardState hazard) =>
            _bossInView != null && hazard.Cause >= 1 && hazard.Cause <= _bossInView.Attacks.Count ? _bossInView.Attacks[hazard.Cause - 1] : null;

        // A boss's attack and when it started on it (or changed state), for the poses and the warning sign.
        void TrackBoss(Body body, EntityView view)
        {
            _bossInView = view.Boss;
            if (body.BossState != view.BossState || body.BossAttack != view.BossAttack) body.BossSince = Time.time;
            body.BossState = view.BossState;
            body.BossAttack = view.BossAttack;
            body.BossPhase = view.Phase;
        }

        // An attack the boss alone announces gives no glow: only its wind-up.
        static bool Unmarked(EntityView view) => view.BossAttack != null && view.BossAttack.Warning == AttackWarning.Hidden;

        // After a boss is placed this frame: the warning sign and, for the vein eater, its dust and mounds.
        void BossExtras(Body body, EntityView view)
        {
            bool strong = view.BossState == BossState.Telegraph && view.BossAttack != null && view.BossAttack.Warning == AttackWarning.Strong;
            ShowWarningSign(body, strong);
            if (body.Id == "vein_eater") VeinEaterExtras(body, view);
            else if (body.Mound != null) body.Mound.enabled = false;
        }

        void ShowWarningSign(Body body, bool on)
        {
            if (!on)
            {
                if (body.Sign != null) body.Sign.enabled = false;
                return;
            }
            if (body.Sign == null)
            {
                body.Sign = Renderer(body.Root, "Warning", WarningSprite, Color.white);
                body.Sign.sortingOrder = 21000;
                Hear(Sound.Warning, body.Position, 1);
            }
            else if (!body.Sign.enabled) Hear(Sound.Warning, body.Position, 1);
            body.Sign.enabled = true;
            float beat = Mathf.Abs(Mathf.Sin(Time.time * 9));
            body.Sign.transform.localPosition = new Vector3(0, body.Top + 0.5f + Mathf.Sin(Time.time * 10) * 0.06f, 0);
            body.Sign.transform.localScale = Vector3.one * (1 + 0.15f * beat);
            body.Sign.color = new Color(1, 1, 1, 0.6f + 0.4f * beat);
        }

        // ---- The vein eater ----

        // Its pose from what it is doing: each kind of attack has a wind-up of its own (VeinPose), run through its
        // telegraph in four steps, and a follow-through just after; a spin turns it round and round.
        void AnimateVeinEater(Body body, Vec2 facing, bool dead)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = VeinPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 1.5f) % 2;
            bool rage = false;
            if (dead)
            {
                frame = 0;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = VeinPose.Roar;
                frame = 0;
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                frame = Mathf.Min(3, Mathf.FloorToInt(progress * 4));
                switch (attack.Shape)
                {
                    case AttackShape.Cone:
                        pose = VeinPose.Rear;
                        break;
                    case AttackShape.Burst:
                        pose = VeinPose.Lift;
                        break;
                    case AttackShape.Charge:
                        pose = VeinPose.Crouch;
                        rage = attack.Count > 1;
                        // Pawing the ground: the crouch rocks back and forth.
                        frame = Mathf.FloorToInt(Time.time * 12) % 2 == 0 ? 1 : 3;
                        break;
                    case AttackShape.Burrow:
                        pose = VeinPose.Sink;
                        frame = Mathf.Min(3, Mathf.FloorToInt(progress / 0.35f * 3));
                        break;
                    case AttackShape.Leap:
                        pose = VeinPose.Leap;
                        frame = progress < 0.4f ? 0 : Mathf.Min(3, 1 + Mathf.FloorToInt((progress - 0.4f) / 0.1f));
                        break;
                    case AttackShape.Trail:
                        pose = VeinPose.Plunge;
                        break;
                    case AttackShape.Spiral:
                        pose = VeinPose.Curl;
                        break;
                    case AttackShape.Summon:
                        pose = VeinPose.Roar;
                        frame = 0;
                        break;
                    default:
                        pose = VeinPose.Slam;
                        break;
                }
            }
            else if (body.BossState == BossState.Charge)
            {
                pose = VeinPose.Rush;
                frame = 0;
                rage = attack != null && attack.Count > 1;
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                if (attack.Shape == AttackShape.Spiral && since < attack.Width * 0.3f + 0.2f)
                {
                    // Spinning on the spot while the volleys fly.
                    pose = VeinPose.Curl;
                    frame = 3;
                    sector = Mathf.FloorToInt(Time.time * 18) % 8;
                }
                else if (since < 0.45f)
                {
                    frame = 0;
                    pose = attack.Shape switch
                    {
                        AttackShape.Cone => VeinPose.Swipe,
                        AttackShape.Burst => VeinPose.Throw,
                        AttackShape.Burrow => VeinPose.Rise,
                        AttackShape.Leap => VeinPose.Pound,
                        AttackShape.Trail => VeinPose.Plunge,
                        AttackShape.Charge => VeinPose.Crouch,
                        AttackShape.Summon => VeinPose.Roar,
                        _ => VeinPose.Pound,
                    };
                    if (attack.Shape == AttackShape.Trail) frame = 3;
                }
            }

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 300000 + ((((int)pose * 8 + (int)view) * 4 + frame) * 4 + phase) * 4 + (rage ? 2 : 0) + (turned ? 1 : 0);
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"vein/{view}/{pose}/{frame}/{phase}/{rage}";
            System.Func<PixelImage> draw = () => VeinEaterArt.Draw(view, pose, frame, phase, rage);
            body.Shape.sprite = Sprites.Standing(key, draw);
            body.Glow.sprite = Sprites.Standing(key + "/white", () => draw().Silhouette());
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            // The name and the warning sign sit over the body, not over the room left above it for a leap.
            body.Top = -body.Feet + VeinEaterArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // Dust, a mound running underground, steam; and rocks falling where the cave-in will land are drawn with the
        // marks (FallingRock).
        void VeinEaterExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            bool mounded = false;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.07f;

            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                switch (attack.Shape)
                {
                    case AttackShape.Charge:
                        // Pawing the ground: dust kicked out behind; in a frenzy, steam off its back.
                        if (puff) Kick(feet - ahead * 0.9f, -ahead, DirtColor, 3);
                        if (puff && attack.Count > 1) Kick(feet + new Vector3(0, 1.8f, 0), Vector3.up, RageColor, 2);
                        break;
                    case AttackShape.Burst:
                        if (puff && progress < 0.5f) Kick(feet + ahead * 0.6f, Vector3.up, DirtColor, 2);
                        break;
                    case AttackShape.Burrow:
                        // Digging in, then a mound running under the floor to where it will burst up. Gone under, it
                        // leaves no shadow.
                        if (progress >= 0.35f) body.Shadow.enabled = false;
                        if (puff && progress < 0.4f) Kick(feet, Vector3.up, DirtColor, 4);
                        if (progress >= 0.3f && LandingOf(attack, out var land))
                        {
                            float run = Mathf.Clamp01((progress - 0.3f) / 0.55f);
                            var at = Vector3.Lerp(feet, land, run);
                            ShowMound(body, at);
                            mounded = true;
                            if (puff) Kick(at, Vector3.up, DirtColor, 2);
                        }
                        break;
                    case AttackShape.Leap:
                        if (puff && progress < 0.4f) Kick(feet, Vector3.up, DirtColor, 2);
                        break;
                    case AttackShape.Trail:
                        if (puff) Kick(feet + ahead * 1.2f, Vector3.up, RockColor, 2);
                        break;
                    case AttackShape.Spiral:
                        if (puff) Kick(feet + new Vector3(0, 1, 0), UnityEngine.Random.insideUnitCircle.normalized, OreColor, 2);
                        break;
                    case AttackShape.Summon:
                        // Head back, a roar at the roof: dust falls round the room.
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 5) + new Vector3(0, 2, 0), Vector3.down, RockColor, 1);
                        _shake = Mathf.Max(_shake, 0.04f);
                        break;
                    default:
                        // Fists up for the cave-in: the roof trickles dust.
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 4) + new Vector3(0, 2.5f, 0), Vector3.down, RockColor, 1);
                        break;
                }
            }
            else if (view.BossState == BossState.Charge && puff)
            {
                // Tearing along: earth thrown up behind, red in a frenzy.
                Kick(feet - ahead * 0.8f, -ahead + Vector3.up * 0.5f, attack != null && attack.Count > 1 ? RageColor : DirtColor, 4);
            }
            else if (view.BossState == BossState.Recover && attack != null && attack.Shape == AttackShape.Spiral && since < attack.Width * 0.3f + 0.2f && puff)
            {
                Kick(feet + new Vector3(0, 1, 0), UnityEngine.Random.insideUnitCircle.normalized, OreColor, 3);
            }
            // In the last phase its ore smoulders.
            if (view.Phase >= 3 && puff && UnityEngine.Random.value < 0.35f) Kick(feet + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 2.2f, 0), Vector3.up, OreColor, 1);
            if (!mounded && body.Mound != null) body.Mound.enabled = false;
        }

        // Where the attack the boss is on comes down: its mark on the ground.
        bool LandingOf(BossAttack attack, out Vector3 at)
        {
            int cause = _bossInView == null ? 0 : _bossInView.Attacks.ToList().IndexOf(attack) + 1;
            foreach (var mark in _marks.Values)
            {
                if (cause == 0 || mark.State.Cause != cause) continue;
                at = new Vector3(mark.State.Position.X, mark.State.Position.Y, 0);
                return true;
            }
            at = default;
            return false;
        }

        void ShowMound(Body body, Vector3 at)
        {
            if (body.Mound == null)
            {
                body.Mound = Renderer(_root.transform, "Mound", MoundSprite, Color.white);
                body.Mound.sortingOrder = 9000;
            }
            body.Mound.enabled = true;
            body.Mound.transform.localPosition = at + new Vector3(Mathf.Sin(Time.time * 40) * 0.04f, -0.2f, 0);
        }

        // A few bits of dust, earth or sparks thrown one way.
        void Kick(Vector3 at, Vector3 toward, Color color, int count)
        {
            if (!Feel.Bits) return;
            for (int i = 0; i < count; i++)
            {
                var renderer = Renderer(_root.transform, "Dust", Shapes.Square, color);
                renderer.transform.localPosition = at + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.3f);
                renderer.transform.localScale = Vector3.one * ((UnityEngine.Random.value < 0.5f ? 2f : 3f) / Sprites.PixelsPerUnit);
                renderer.sortingOrder = 19000;
                var velocity = (toward.normalized + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.6f)) * UnityEngine.Random.Range(1.5f, 3.5f);
                _bits.Add(new Bit { Renderer = renderer, Velocity = velocity, Color = color, Life = 0.5f });
            }
        }

        // A rock falling from the roof onto a strong circle of the vein eater's cave-in, landing as the mark fills.
        void FallingRock(Mark mark, HazardState hazard, float progress)
        {
            var attack = AttackOf(hazard);
            if (attack == null || attack.Shape != AttackShape.Circle || _bossInView.Id != "vein_eater")
            {
                if (mark.Rock != null) mark.Rock.enabled = false;
                return;
            }
            if (mark.Rock == null)
            {
                mark.Rock = Renderer(_root.transform, "Falling rock", BoulderSprite, Color.white);
                mark.Rock.sortingOrder = 20500;
            }
            // Out of sight above for the first part, then dropping faster and faster.
            float fall = Mathf.Clamp01((progress - 0.35f) / 0.65f);
            mark.Rock.enabled = fall > 0;
            float height = 7 * (1 - fall * fall);
            mark.Rock.transform.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y + height, 0);
            mark.Rock.transform.localScale = Vector3.one * (0.7f + 0.5f * fall) * Mathf.Max(1, hazard.Size / 1.2f);
        }

        // Where one of the vein eater's blows lands, a burst of its own; false for anyone else's (HazardLanded draws
        // those).
        bool VeinEaterLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "vein_eater") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                    // Three drill slashes across the wedge, sparks off the drills.
                    for (int i = -1; i <= 1; i++)
                    {
                        var turned = Quaternion.Euler(0, 0, i * attack.Angle * 0.3f) * along;
                        AddFlash(at + turned * (hazard.Size * 0.6f), 0.7f, SlashColor, FlashKind.Impact);
                    }
                    AddFlash(at + along * (hazard.Size * 0.5f), hazard.Size * 0.55f, SlashColor, FlashKind.Vortex);
                    Burst(at + along * (hazard.Size * 0.6f), OreColor, 1);
                    return true;
                case AttackShape.Burrow:
                    // Bursting up out of the floor.
                    AddFlash(at, hazard.Size * 1.1f, DirtColor, FlashKind.Burst);
                    AddFlash(at, hazard.Size * 1.6f, DirtColor, FlashKind.Ring);
                    AddFlash(at, 1.2f, Color.white, FlashKind.Impact);
                    Burst(at, DirtColor, 2.5f);
                    Burst(at, OreColor, 1);
                    _shake = Mathf.Max(_shake, 0.4f);
                    return true;
                case AttackShape.Leap:
                    // Coming down: a shockwave and the floor thrown up.
                    AddFlash(at, hazard.Size * 2, SlashColor, FlashKind.Ring);
                    AddFlash(at, hazard.Size * 1.3f, DirtColor, FlashKind.Ring);
                    AddFlash(at, hazard.Size, DirtColor, FlashKind.Burst);
                    Burst(at, DirtColor, 2.5f);
                    Burst(at, RockColor, 1.5f);
                    _shake = Mathf.Max(_shake, 0.45f);
                    return true;
                case AttackShape.Circle:
                    // The rock from the roof smashing.
                    AddFlash(at, hazard.Size, RockColor, FlashKind.Burst);
                    AddFlash(at, 0.8f, Color.white, FlashKind.Impact);
                    Burst(at, RockColor, 1.4f);
                    _shake = Mathf.Max(_shake, 0.2f);
                    return true;
                case AttackShape.Trail:
                    // A spike of rock bursting up through the floor.
                    AddFlash(at, hazard.Size * 0.9f, RockColor, FlashKind.Burst);
                    AddFlash(at, 0.6f, Color.white, FlashKind.Impact);
                    Kick(at, Vector3.up, RockColor, 5);
                    return true;
            }
            return false;
        }
    }
}
