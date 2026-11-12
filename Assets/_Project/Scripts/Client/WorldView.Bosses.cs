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

        // How far the last frame got (1 entities, 2 past them, 3 past the marks, 4 past the events), and how each boss
        // body is drawn: where, its sprite and its colour, for the playtests to see why a boss would not show.
        int _rendered;
        public string RenderTrace => _rendered + " " + string.Join(" ", _others.Values.Where(b => b.Kind == BodyKind.Boss).Select(b =>
            $"{b.Id}@{b.Root.localPosition.x:0.0},{b.Root.localPosition.y:0.0}:{(b.Shape.sprite == null ? "nosprite" : b.Shape.sprite.name + "/" + b.Shape.sprite.rect.width + "x" + b.Shape.sprite.rect.height)}:a{b.Shape.color.a:0.00}:o{b.Shape.sortingOrder}:{(b.Shape.enabled ? "on" : "off")}:f{b.Frame}"));

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
            RunLater();
            if (body.Id == "vein_eater") VeinEaterExtras(body, view);
            else if (body.Mound != null) body.Mound.enabled = false;
            if (body.Id == "shrine_samurai") SamuraiExtras(body, view);
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
        void AnimateVeinEater(Body body, Vec2 facing, bool dead, bool moving)
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
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Walking: a step a foot about every seventh of a second at its pace.
                pose = VeinPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 7) % 4;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = VeinPose.Roar;
                frame = Mathf.FloorToInt(Time.time * 8) % 2;
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                // Six steps through a wind-up.
                frame = Mathf.Min(5, Mathf.FloorToInt(progress * 6));
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
                        frame = Mathf.FloorToInt(Time.time * 8) % 2;
                        break;
                    default:
                        pose = VeinPose.Slam;
                        break;
                }
            }
            else if (body.BossState == BossState.Charge)
            {
                // Running flat out: legs at twice the walk.
                pose = VeinPose.Rush;
                frame = Mathf.FloorToInt(Time.time * 14) % 4;
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
                    // Three frames of follow-through.
                    frame = Mathf.Min(2, Mathf.FloorToInt(since / 0.15f));
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
                    if (attack.Shape == AttackShape.Trail) frame = 5;
                    if (attack.Shape == AttackShape.Charge || attack.Shape == AttackShape.Summon) frame = 0;
                }
            }

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 300000 + ((((int)pose * 8 + (int)view) * 8 + frame) * 4 + phase) * 4 + (rage ? 2 : 0) + (turned ? 1 : 0);
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
            if (SamuraiLanded(hazard)) return true;
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "vein_eater") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                    // The drills strike across the wedge: three hits, sparks and grit off them (no blade's whirl).
                    for (int i = -1; i <= 1; i++)
                    {
                        var turned = Quaternion.Euler(0, 0, i * attack.Angle * 0.3f) * along;
                        AddFlash(at + turned * (hazard.Size * 0.6f), 0.7f, SlashColor, FlashKind.Impact);
                        Kick(at + turned * (hazard.Size * 0.6f), turned, OreColor, 3);
                    }
                    AddFlash(at + along * (hazard.Size * 0.55f), hazard.Size * 0.4f, DirtColor, FlashKind.Burst);
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

        // ---- The shrine samurai (2026-11-12, Docs/Bosses.md 벚꽃 검귀 시구레): iaido. A long, plain stance with the hand on
        // the hilt (and the marks on the ground), then the draw too fast to see: the picture stops for a moment, a white
        // line is left where the blade went, and a beat later the cut opens along it in sparks and petals. ----

        static readonly Color SteelColor = new Color(0.86f, 0.92f, 1f);
        static readonly Color PetalColor = new Color(1f, 0.72f, 0.82f);
        static readonly Color PetalDeep = new Color(0.94f, 0.45f, 0.6f);

        // Seconds the picture holds still when the blade comes out, and how long the line it leaves stays.
        const float DrawStop = 0.07f;
        const float CutLine = 0.32f;
        // A beat after the line, the cut opens.
        const float CutDelay = 0.13f;

        // Faster than this (tiles a second) it is darting, not walking.
        const float DartSpeed = 6;
        // The sheathing ends this long after the cut.
        const float NotoDone = 0.95f;

        // Cuts across (a cross, a crescent wave, the petals) come down from overhead; everything else is drawn from the
        // scabbard.
        static bool FromAbove(BossAttack attack) =>
            attack.Shape == AttackShape.Cross || attack.Shape == AttackShape.Burst || attack.Shape == AttackShape.Circle && attack.Target == AttackTarget.Each;

        static Sprite CrescentSprite => Sprites.Centered("shot/crescent", () => EffectArt.Swing(24, 2));

        // Things to happen a moment later: the cut opening along a line already drawn.
        readonly System.Collections.Generic.List<(float At, System.Action Do)> _later = new System.Collections.Generic.List<(float, System.Action)>();

        void Later(float seconds, System.Action action) => _later.Add((Time.time + seconds, action));

        void RunLater()
        {
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                if (Time.time < _later[i].At) continue;
                var action = _later[i].Do;
                _later.RemoveAt(i);
                action();
            }
        }

        void AnimateSamurai(Body body, Vec2 facing, bool dead, bool moving, float speed)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = SamuraiPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 1.5f) % 2;
            if (dead)
            {
                frame = 0;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = SamuraiPose.Kiai;
                frame = Mathf.FloorToInt(Time.time * 8) % 2;
            }
            else if (body.BossState == BossState.Charge || moving && speed > DartSpeed)
            {
                // Dashing, or darting in: low, the blade trailing.
                pose = SamuraiPose.Dash;
                frame = Mathf.FloorToInt(Time.time * 16) % 4;
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                pose = SamuraiPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 8) % 4;
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                frame = Mathf.Min(5, Mathf.FloorToInt(progress * 6));
                pose = FromAbove(attack) ? SamuraiPose.Jodan : SamuraiPose.Stance;
            }
            else if (body.BossState == BossState.Recover && attack != null && since < NotoDone)
            {
                // The cut in three quick frames (from the scabbard, or down from overhead), the blood flicked off, then
                // the blade slid home slowly.
                var cut = FromAbove(attack) ? SamuraiPose.Kiri : SamuraiPose.Draw;
                if (since < 0.04f) (pose, frame) = (cut, 0);
                else if (since < 0.14f) (pose, frame) = (cut, 1);
                else if (since < 0.26f) (pose, frame) = (cut, 2);
                else if (since < 0.4f) (pose, frame) = (SamuraiPose.Chiburi, 0);
                else (pose, frame) = (SamuraiPose.Noto, Mathf.Min(2, Mathf.FloorToInt((since - 0.4f) / ((NotoDone - 0.4f) / 3))));
            }

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 400000 + ((((int)pose * 8 + (int)view) * 8 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"samurai/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => SamuraiArt.Draw(view, pose, frame, phase);
            body.Shape.sprite = Sprites.Standing(key, draw);
            body.Glow.sprite = Sprites.Standing(key + "/white", () => draw().Silhouette());
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + SamuraiArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While it winds up, petals stir round it; dashing, it leaves afterimages; out of a dash, the line it cut.
        void SamuraiExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.08f;
            var feet = body.Position;
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                // Gathering itself: petals drawn in toward it, more as the draw nears.
                if (puff && UnityEngine.Random.value < 0.3f + progress)
                {
                    var from = feet + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * 2.2f) + new Vector3(0, 0.8f, 0);
                    Kick(from, feet + new Vector3(0, 0.8f, 0) - from, UnityEngine.Random.value < 0.5f ? PetalColor : PetalDeep, 1);
                }
                // A flash step: gone just before it lands behind its target.
                if (attack.GoesAway && progress > 0.6f) body.Shape.color = new Color(1, 1, 1, Mathf.Max(0.08f, 1 - (progress - 0.6f) * 4));
            }
            // Darting in, or dashing: streaks behind it.
            var moved = body.Position - body.LastPosition;
            bool darting = moved.magnitude / Mathf.Max(1e-3f, Time.deltaTime) > DartSpeed;
            if (darting || view.BossState == BossState.Charge) SpeedLines(feet, moved);
            if (darting && view.BossState != BossState.Charge && puff)
            {
                var echo = Renderer(_root.transform, "Afterimage", body.Shape.sprite, new Color(1f, 0.45f, 0.55f, 0.45f));
                echo.transform.localPosition = body.Shape.transform.position - _root.transform.position;
                echo.flipX = body.Shape.flipX;
                echo.sortingOrder = body.Shape.sortingOrder - 1;
                _bits.Add(new Bit { Renderer = echo, Velocity = Vector3.zero, Color = new Color(1f, 0.45f, 0.55f, 0.45f), Life = 0.2f });
            }
            // The ultimate's wind-up dims the room.
            bool gathering = view.BossState == BossState.Telegraph && attack != null && attack.Warning == AttackWarning.Strong;
            Dim(gathering ? 0.5f * Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph)) : 0);
            if (view.BossState == BossState.Charge)
            {
                if (!body.Dashing)
                {
                    body.Dashing = true;
                    body.DashFrom = feet;
                }
                // Afterimages along the dash.
                var ghost = Renderer(_root.transform, "Afterimage", body.Shape.sprite, new Color(1f, 0.5f, 0.6f, 0.55f));
                ghost.transform.localPosition = body.Shape.transform.position - _root.transform.position;
                ghost.flipX = body.Shape.flipX;
                ghost.sortingOrder = body.Shape.sortingOrder - 1;
                _bits.Add(new Bit { Renderer = ghost, Velocity = Vector3.zero, Color = new Color(1f, 0.5f, 0.6f, 0.55f), Life = 0.25f });
            }
            else if (body.Dashing)
            {
                // Out of the dash: the line it cut, and the cut opening along it a beat later.
                body.Dashing = false;
                Cut(body.DashFrom + new Vector3(0, 0.5f, 0), feet + new Vector3(0, 0.5f, 0), 1.2f);
            }
        }

        // The draw itself: the picture stops for an instant, the sound of steel.
        void SamuraiDraws(Body body)
        {
            // A long lunge into the cut and a white flash of the whole screen.
            body.LungeReach = 6;
            ScreenFlash(new Color(1, 1, 1, 0.35f), 0.09f);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, DrawStop);
            _shake = Mathf.Max(_shake, 0.18f);
            Hear(Sound.Heavy, body.Position, 1);
            Hear(Sound.Crit, body.Position, 0.6f);
        }

        // Where the blade went: a pink glow, a pale blue band and a white core, bright and gone fast; a beat later the cut
        // opens, its two edges parting, sparks and a gust of petals along it.
        void Cut(Vector3 from, Vector3 to, float heavy)
        {
            var along = to - from;
            float length = along.magnitude;
            if (length < 0.05f) return;
            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            var normal = new Vector3(-along.y, along.x, 0) / length;
            SpriteRenderer Line(Vector3 middle, float span, float thickness, Color color, float life, int order, Vector3 drift)
            {
                var line = Renderer(_root.transform, "Cut", Shapes.Square, color);
                line.transform.localPosition = middle;
                line.transform.localRotation = Quaternion.Euler(0, 0, angle);
                line.transform.localScale = new Vector3(span, thickness, 1);
                line.sortingOrder = order;
                _bits.Add(new Bit { Renderer = line, Velocity = drift, Color = color, Life = life });
                return line;
            }
            var middle = (from + to) / 2;
            float px = 1f / Sprites.PixelsPerUnit;
            Line(middle, length + 1.2f, 9 * px * heavy, new Color(1f, 0.55f, 0.75f, 0.35f), CutLine * 1.4f, 19499, Vector3.zero);
            Line(middle, length + 0.9f, 4 * px * heavy, new Color(0.65f, 0.85f, 1f, 0.8f), CutLine * 1.2f, 19500, Vector3.zero);
            Line(middle, length + 0.7f, 2 * px, Color.white, CutLine, 19501, Vector3.zero);
            Later(CutDelay, () =>
            {
                // The two edges of the cut part and fade.
                Line(middle, length, 2 * px, Color.white, 0.25f, 19502, normal * 1.6f);
                Line(middle, length, 2 * px, new Color(1f, 0.75f, 0.85f), 0.25f, 19502, -normal * 1.6f);
                int steps = Mathf.Max(2, Mathf.RoundToInt(length / 0.9f));
                for (int i = 0; i <= steps; i++)
                {
                    var at = Vector3.Lerp(from, to, i / (float)steps);
                    AddFlash(at, 0.6f * heavy, SteelColor, FlashKind.Impact);
                    Kick(at, normal * (i % 2 == 0 ? 1 : -1), i % 2 == 0 ? PetalColor : PetalDeep, 4);
                }
                AddFlash(middle, Mathf.Min(2.4f, length * 0.35f) * heavy, PetalColor, FlashKind.Burst);
                _shake = Mathf.Max(_shake, 0.16f * heavy);
                Hear(Sound.Hit, middle, 0.8f);
            });
        }

        // The whole screen washed white for an instant (the draw), or darkened (the ultimate's wind-up).
        void ScreenFlash(Color color, float life)
        {
            var flash = Renderer(_root.transform, "Screen flash", Shapes.Square, color);
            var at = _camera.transform.position;
            flash.transform.localPosition = new Vector3(at.x, at.y, 0) - _root.transform.position;
            flash.transform.localScale = new Vector3(60, 40, 1);
            flash.sortingOrder = 30000;
            _bits.Add(new Bit { Renderer = flash, Velocity = Vector3.zero, Color = color, Life = life });
        }

        SpriteRenderer _dim;

        // The ultimate's wind-up: the room dims as it gathers itself (the marks and effects stay bright over it).
        void Dim(float amount)
        {
            if (_dim == null)
            {
                _dim = Renderer(_root.transform, "Dim", Shapes.Square, Color.clear);
                _dim.transform.localScale = new Vector3(60, 40, 1);
                _dim.sortingOrder = DarkOrder + 5;
            }
            var at = _camera.transform.position;
            _dim.transform.localPosition = new Vector3(at.x, at.y, 0) - _root.transform.position;
            _dim.color = new Color(0.05f, 0, 0.04f, amount);
        }

        // Thin white streaks behind something moving fast.
        void SpeedLines(Vector3 at, Vector3 moving)
        {
            if (moving.sqrMagnitude < 1e-4f) return;
            var dir = moving.normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                var line = Renderer(_root.transform, "Speed", Shapes.Square, new Color(1, 1, 1, 0.7f));
                line.transform.localPosition = at - dir * UnityEngine.Random.Range(0.6f, 1.4f) + new Vector3(-dir.y, dir.x, 0) * UnityEngine.Random.Range(-0.7f, 0.7f) + new Vector3(0, 0.8f, 0);
                line.transform.localRotation = Quaternion.Euler(0, 0, angle);
                line.transform.localScale = new Vector3(UnityEngine.Random.Range(0.8f, 1.6f), 1f / Sprites.PixelsPerUnit, 1);
                line.sortingOrder = 19400;
                _bits.Add(new Bit { Renderer = line, Velocity = -dir * 4, Color = new Color(1, 1, 1, 0.7f), Life = 0.16f });
            }
        }

        // Where one of the samurai's cuts lands: its line drawn the way the blade went, the cut opening after.
        bool SamuraiLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "shrine_samurai") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            var across = new Vector3(-along.y, along.x, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                {
                    // A sweep across the front: a curve of short lines round the wedge.
                    float half = attack.Angle / 2;
                    Vector3 Point(float a) => at + Quaternion.Euler(0, 0, a) * along * (hazard.Size * 0.7f);
                    for (float a = -half; a < half; a += half / 3) Cut(Point(a), Point(a + half / 3), 1);
                    return true;
                }
                case AttackShape.Line:
                    // One arm of the cross: a long straight cut.
                    Cut(at, at + along * hazard.Size, 1.2f);
                    return true;
                case AttackShape.Ring:
                    // The thousand flashes: lines every way through the band, a white shockwave, the picture stopping.
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i * 40 + UnityEngine.Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        var mid = at + dir * ((hazard.Size + hazard.Width) / 2);
                        var cross = new Vector3(-dir.y, dir.x, 0) * 2.6f;
                        Cut(mid - cross, mid + cross, 1.3f);
                    }
                    AddFlash(at, hazard.Size, Color.white, FlashKind.Ring);
                    AddFlash(at, hazard.Size * 0.7f, PetalColor, FlashKind.Ring);
                    ScreenFlash(new Color(1, 1, 1, 0.75f), 0.18f);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, DrawStop * 2.2f);
                    _shake = Mathf.Max(_shake, 0.45f);
                    return true;
                case AttackShape.Circle when attack.Target == AttackTarget.Self:
                    // A turn with the blade: a ring of cuts round it.
                    for (int i = 0; i < 6; i++)
                    {
                        float a0 = i * 60 * Mathf.Deg2Rad, a1 = (i + 1) * 60 * Mathf.Deg2Rad;
                        Cut(at + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * hazard.Size * 0.8f, at + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * hazard.Size * 0.8f, 1);
                    }
                    return true;
                case AttackShape.Circle:
                    // A cut in the air: an X where it lands, in petals.
                    Cut(at - (along + across) * hazard.Size * 0.8f, at + (along + across) * hazard.Size * 0.8f, 0.9f);
                    Cut(at - (along - across) * hazard.Size * 0.8f, at + (along - across) * hazard.Size * 0.8f, 0.9f);
                    return true;
                case AttackShape.Leap:
                    // Out of nowhere beside its target: one cut straight down through where it stood.
                    Cut(at + new Vector3(-0.8f, 1.6f, 0), at + new Vector3(0.8f, -0.8f, 0), 1.3f);
                    return true;
            }
            return false;
        }
    }
}
