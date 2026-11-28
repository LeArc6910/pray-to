using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The boss bundle R4b (2026-11-28, Docs/BossRedesign.md 5.6·5.8, Docs/Bosses.md "분신·되감기·별 미로") on the
    // screen. The crown wraith's images wear its pose with a dull crown (only the true one's is gold), its puddles lie
    // in the floor as its shallow water does and flash when lightning runs through them, its hands rise out of them;
    // it goes to mist through its wind-up and its interlude. The void lord's gates stand, an hourglass turns over its
    // head as it rewinds (the blasts coming again drawn in faint violet, WorldView.Hazards), its star maze falls in
    // gold, and from its last phase its gravity is turned over: a violet rim round the screen (GameRoot). ----
    sealed partial class WorldView
    {
        // For the playtests: images, puddles, gates, hands, echoes and maze rings drawn now; whether the hourglass and
        // the mist are shown; puddle flashes and images broken since the start.
        public int ClonesShown { get; private set; }
        public int PuddlesShown { get; private set; }
        public int GatesShown { get; private set; }
        public int HandsShown { get; private set; }
        public int EchoesShown { get; private set; }
        public int MazeRingsShown { get; private set; }
        public bool HourglassShown { get; private set; }
        public bool MistShown { get; private set; }
        public int PuddleFlashes { get; private set; }
        public int ClonesBroken { get; private set; }
        // Whether the void lord's gravity is turned over (its last phase), for the rim round the screen.
        public bool GravityTurned { get; private set; }

        readonly Dictionary<int, (SpriteRenderer Sprite, PropKind Kind, Vec2 At)> _mirrorProps = new Dictionary<int, (SpriteRenderer, PropKind, Vec2)>();
        readonly Dictionary<int, (SpriteRenderer Sprite, float Born)> _hands = new Dictionary<int, (SpriteRenderer, float)>();
        readonly Dictionary<int, Vec2> _flashesComing = new Dictionary<int, Vec2>();
        readonly HashSet<int> _mirrorSeen = new HashSet<int>();
        SpriteRenderer _hourglass, _mist;
        float _hourglassSince = -1;
        // How the wraith was last drawn, for its images (WorldView.CrownWraith.cs).
        (BossView View, CrownWraithPose Pose, int Frame, int Phase, bool Turned)? _wraithLook;

        static bool IsMirrorProp(PropKind kind) => kind == PropKind.Clone || kind == PropKind.Puddle || kind == PropKind.Gate;

        void DrawMirror()
        {
            _mirrorSeen.Clear();
            int clones = 0, puddles = 0, gates = 0;
            foreach (var prop in _client.Props())
            {
                if (!IsMirrorProp(prop.Kind)) continue;
                _mirrorSeen.Add(prop.Id);
                if (!_mirrorProps.TryGetValue(prop.Id, out var known))
                {
                    var made = Renderer(_root.transform, prop.Kind.ToString(), null, Color.white);
                    known = (made, prop.Kind, prop.Position);
                    if (prop.Kind == PropKind.Clone) Impact(new Vector3(prop.Position.X, prop.Position.Y, 0), 0.4f);
                }
                known.At = prop.Position;
                _mirrorProps[prop.Id] = known;
                var sprite = known.Sprite;
                var at = new Vector3(prop.Position.X, prop.Position.Y, 0);
                sprite.transform.localPosition = at;
                switch (prop.Kind)
                {
                    case PropKind.Puddle:
                        // Shallow sea water in the floor, under everyone.
                        puddles++;
                        int ripple = Mathf.FloorToInt(Time.time * 3 + prop.Id) % GroundArt.Frames;
                        sprite.sprite = GroundSprite(HazardShape.Water, false, prop.Radius * 2, prop.Radius * 2, prop.Id, ripple);
                        sprite.sortingOrder = 31;
                        sprite.color = new Color(1, 1, 1, 0.9f);
                        break;
                    case PropKind.Gate:
                        gates++;
                        int turn = Mathf.FloorToInt(Time.time * 6 + prop.Id) % ForceArt.Frames;
                        sprite.sprite = Sprites.Get($"fx/gate/{turn}", () => ForceArt.Gate(turn), new Vector2(0.5f, ForceArt.GatePivot));
                        sprite.sortingOrder = FootOrder(prop.Position.Y - prop.Radius * 0.5f);
                        break;
                    default:
                        // An image: the wraith's own pose, its crown dull and itself a little thinner.
                        clones++;
                        if (_wraithLook is { } look)
                        {
                            var pivot = new Vector2(0.5f, CrownWraithArt.Floor / (float)CrownWraithArt.Height);
                            sprite.sprite = Sprites.Get($"wraith-false/{look.View}/{look.Pose}/{look.Frame}/{look.Phase}",
                                () => CrownWraithArt.DrawFalse(look.View, look.Pose, look.Frame, look.Phase), pivot);
                            sprite.flipX = look.Turned;
                        }
                        sprite.sortingOrder = FootOrder(prop.Position.Y);
                        sprite.color = new Color(1, 1, 1, 0.92f);
                        break;
                }
            }
            foreach (int id in _mirrorProps.Keys.Where(id => !_mirrorSeen.Contains(id)).ToList())
            {
                var (sprite, kind, at) = _mirrorProps[id];
                Object.Destroy(sprite.gameObject);
                _mirrorProps.Remove(id);
                if (kind != PropKind.Clone) continue;
                // An image goes in a breath of mist.
                ClonesBroken++;
                var where = new Vector3(at.X, at.Y, 0);
                AddFlash(where + new Vector3(0, 0.8f, 0), 0.8f, new Color(0.6f, 0.95f, 0.85f), FlashKind.Impact);
                Hear(Sound.CloneBreak, where, 0.8f);
            }
            ClonesShown = clones;
            PuddlesShown = puddles;
            GatesShown = gates;
            DrawMirrorHazards();
            DrawBossTells();
        }

        // Hands rising out of the water, echoes and maze rings counted, a puddle's flash as its lightning lands.
        void DrawMirrorHazards()
        {
            int echoes = 0, rings = 0;
            var puddles = _client.Props().Where(p => p.Kind == PropKind.Puddle).ToList();
            var seen = new HashSet<int>();
            foreach (var view in _client.Hazards())
            {
                var hazard = view.State;
                if (hazard.Shape == HazardShape.Maze)
                {
                    rings++;
                    if (!_mazeComing.ContainsKey(hazard.Id)) _mazeComing[hazard.Id] = hazard;
                    continue;
                }
                var attack = AttackOf(hazard);
                if (attack == null) continue;
                if (attack.Shape == AttackShape.Rewind) echoes++;
                // A whole puddle marked: its flash when it lands.
                if (hazard.Harmful && puddles.Any(p => Vec2.Distance(p.Position, hazard.Position) < 0.05f && Mathf.Abs(p.Radius - hazard.Size) < 0.05f))
                    _flashesComing[hazard.Id] = hazard.Position;
                if (attack.Shape != AttackShape.Hands) continue;
                seen.Add(hazard.Id);
                if (!_hands.TryGetValue(hazard.Id, out var hand))
                {
                    hand = (Renderer(_root.transform, "Hand", null, Color.white), Time.time);
                    _hands[hazard.Id] = hand;
                }
                // Ripples, then rising as it lands, holding, sinking.
                int frame = Mathf.Clamp(Mathf.FloorToInt(view.Progress * 2), 0, 1);
                hand.Sprite.sprite = Sprites.Get($"fx/hand/{frame}", () => ForceArt.Hand(frame), new Vector2(0.5f, ForceArt.HandPivot));
                hand.Sprite.transform.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
                hand.Sprite.sortingOrder = FootOrder(hazard.Position.Y);
            }
            foreach (int id in _hands.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                var (sprite, _) = _hands[id];
                // Landed: it grabs, holds and sinks over the hold (World.HandHold), then is gone.
                float age = Time.time - _hands[id].Born;
                var at = sprite.transform.localPosition;
                Hear(Sound.HandGrab, at, 0.8f);
                Object.Destroy(sprite.gameObject);
                _hands.Remove(id);
                var rising = Renderer(_root.transform, "HandHold", null, Color.white);
                rising.transform.localPosition = at;
                rising.sortingOrder = FootOrder(at.y);
                _handsHolding.Add((rising, Time.time));
            }
            for (int i = _handsHolding.Count - 1; i >= 0; i--)
            {
                var (sprite, born) = _handsHolding[i];
                float t = (Time.time - born) / World.HandHold;
                if (t >= 1)
                {
                    Object.Destroy(sprite.gameObject);
                    _handsHolding.RemoveAt(i);
                    continue;
                }
                int frame = Mathf.Clamp(2 + Mathf.FloorToInt(t * (ForceArt.HandFrames - 2)), 2, ForceArt.HandFrames - 1);
                sprite.sprite = Sprites.Get($"fx/hand/{frame}", () => ForceArt.Hand(frame), new Vector2(0.5f, ForceArt.HandPivot));
            }
            HandsShown = _hands.Count + _handsHolding.Count;
            foreach (int id in _flashesComing.Keys.Where(id => _client.Hazards().All(h => h.State.Id != id)).ToList())
            {
                // The puddle flashes white-gold as its lightning runs through.
                var at = _flashesComing[id];
                _flashesComing.Remove(id);
                PuddleFlashes++;
                var where = new Vector3(at.X, at.Y, 0);
                AddFlash(where, World.PuddleRadius * 1.2f, new Color(1f, 0.92f, 0.55f), FlashKind.Impact);
                Hear(Sound.PuddleZap, where, 0.9f);
            }
            EchoesShown = echoes;
            MazeRingsShown = rings;
            DrawStars();
        }

        // ---- The star maze's stars: as a ring lands, stars rain down all round it but for its gap ----

        readonly Dictionary<int, HazardState> _mazeComing = new Dictionary<int, HazardState>();
        readonly List<(SpriteRenderer Sprite, Vector3 To, float Born, Vector3 Drift)> _stars = new List<(SpriteRenderer, Vector3, float, Vector3)>();
        // The void lord's falling stars already coming down (boost bend), by mark.
        readonly HashSet<int> _starsFalling = new HashSet<int>();
        const float StarBend = 2.2f;
        // For the playtests: stars falling now.
        public int StarsShown => _stars.Count;
        const float StarFall = 0.35f, StarHeight = 3f;

        void DrawStars()
        {
            foreach (int id in _mazeComing.Keys.Where(id => _client.Hazards().All(h => h.State.Id != id)).ToList())
            {
                var ring = _mazeComing[id];
                _mazeComing.Remove(id);
                float middle = (ring.Size + ring.Width) / 2;
                float gap = World.MazeGap * Mathf.Deg2Rad / 2;
                int count = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * middle / 1.8f), 6, 18);
                for (int i = 0; i < count; i++)
                {
                    float angle = ring.Direction.Angle + gap + (2 * Mathf.PI - 2 * gap) * (i + Random.value * 0.6f) / count;
                    var to = new Vector3(ring.Position.X + Mathf.Cos(angle) * middle, ring.Position.Y + Mathf.Sin(angle) * middle, 0);
                    var star = Renderer(_root.transform, "Star", null, Color.white);
                    star.sortingOrder = DarkOrder + 13;
                    _stars.Add((star, to, Time.time - Random.value * 0.1f, Vector3.zero));
                }
                if (Vec2.Distance(ring.Position, new Vec2(_self.Position.x, _self.Position.y)) < ring.Size + 6)
                    Hear(Sound.StarFall, new Vector3(ring.Position.X, ring.Position.Y, 0), 0.8f);
            }
            // The void lord's falling stars (R4b left over, boost bend): each comes down StarFall before its mark lands,
            // curving in from the side away from the lord as its gravity bends it.
            var marks = _client.Hazards();
            _starsFalling.RemoveWhere(id => marks.All(h => h.State.Id != id));
            foreach (var mark in marks)
            {
                if (_starsFalling.Contains(mark.State.Id) || !mark.State.Harmful) continue;
                var (boss, attack) = BlowOf(mark.State);
                if (boss != "void_lord" || attack != "star_fall") continue;
                if ((1 - mark.Progress) * mark.State.Duration / World.TicksPerSecond > StarFall) continue;
                _starsFalling.Add(mark.State.Id);
                var to = new Vector3(mark.State.Position.X, mark.State.Position.Y, 0);
                var lords = _client.Others().Where(e => e.Kind == EntityKind.Boss && e.Boss?.Id == "void_lord").OrderBy(e => Vec2.Distance(e.Position, mark.State.Position)).Take(1).ToList();
                var drift = Vector3.zero;
                foreach (var lord in lords)
                {
                    var away = to - new Vector3(lord.Position.X, lord.Position.Y, 0);
                    if (away.sqrMagnitude > 1e-4f) drift = away.normalized * StarBend;
                }
                var star = Renderer(_root.transform, "Star", null, Color.white);
                star.sortingOrder = DarkOrder + 13;
                _stars.Add((star, to, Time.time, drift));
            }
            for (int i = _stars.Count - 1; i >= 0; i--)
            {
                var (star, to, born, drift) = _stars[i];
                float t = (Time.time - born) / StarFall;
                if (t >= 1)
                {
                    AddFlash(to, 0.5f, new Color(1f, 0.9f, 0.55f), FlashKind.Impact);
                    Object.Destroy(star.gameObject);
                    _stars.RemoveAt(i);
                    continue;
                }
                int frame = Mathf.Clamp(Mathf.FloorToInt(t * ForceArt.Frames), 0, ForceArt.Frames - 1);
                star.sprite = Sprites.Get($"fx/star/{frame}", () => ForceArt.Star(frame), new Vector2(0.5f, 0));
                float high = 1 - Mathf.Max(0, t);
                star.transform.localPosition = to + new Vector3(0, high * StarHeight, 0) + drift * (high * high);
            }
        }

        readonly List<(SpriteRenderer Sprite, float Born)> _handsHolding = new List<(SpriteRenderer, float)>();

        // Over the bosses: the hourglass turning as the void lord rewinds; the wraith's mist while it is out of reach.
        void DrawBossTells()
        {
            bool hourglass = false, mist = false;
            GravityTurned = false;
            foreach (var boss in _client.Others().Where(e => e.Kind == EntityKind.Boss && !e.IsDead))
            {
                var at = new Vector3(boss.Position.X, boss.Position.Y, 0);
                if (boss.Boss?.Id == "void_lord" && boss.Phase >= 3 && boss.BossState != BossState.Interlude) GravityTurned = true;
                if (boss.BossState == BossState.Telegraph && boss.BossAttack?.Shape == AttackShape.Rewind)
                {
                    hourglass = true;
                    if (_hourglass == null)
                    {
                        _hourglass = Renderer(_root.transform, "Hourglass", null, Color.white);
                        _hourglass.sortingOrder = DarkOrder + 16;
                    }
                    if (_hourglassSince < 0)
                    {
                        _hourglassSince = Time.time;
                        Hear(Sound.Rewind, at, 0.9f);
                    }
                    int frame = Mathf.Clamp(Mathf.FloorToInt((Time.time - _hourglassSince) * 6), 0, ForceArt.HourglassFrames - 1);
                    _hourglass.sprite = Sprites.Centered($"fx/hourglass/{frame}", () => ForceArt.Hourglass(frame));
                    _hourglass.transform.localPosition = at + new Vector3(0, boss.Radius * 2 + 1.2f, 0);
                    _hourglass.transform.localScale = Vector3.one * 1.5f;
                }
                bool misty = boss.Boss?.Id == "crown_wraith"
                    && (boss.BossState == BossState.Interlude || boss.BossState == BossState.Telegraph && boss.BossAttack?.Shape == AttackShape.Clone);
                if (misty)
                {
                    mist = true;
                    if (_mist == null)
                    {
                        _mist = Renderer(_root.transform, "Mist", null, new Color(1, 1, 1, 0.85f));
                        _mist.sortingOrder = DarkOrder + 12;
                    }
                    int frame = Mathf.FloorToInt(Time.time * 5) % ForceArt.Frames;
                    _mist.sprite = Sprites.Centered($"fx/mist/{frame}", () => ForceArt.Mist(frame));
                    _mist.transform.localPosition = at + new Vector3(0, 0.6f, 0);
                    _mist.transform.localScale = Vector3.one * 2f;
                }
            }
            if (!hourglass) _hourglassSince = -1;
            if (_hourglass != null) _hourglass.enabled = hourglass;
            if (_mist != null) _mist.enabled = mist;
            HourglassShown = hourglass;
            MistShown = mist;
        }

        // A boss's R4b cue (SimEventType.Cue): images formed, a blow from behind, a rewind begun. Whether it was one.
        bool ShowMirrorCue(SimEvent e, Body body)
        {
            var at = body.Position;
            if (e.Amount == World.CueClones)
            {
                AddFlash(at + new Vector3(0, 0.8f, 0), 1.2f, new Color(0.6f, 0.95f, 0.85f), FlashKind.Impact);
                Hear(Sound.CloneSplit, at, 0.9f);
                return true;
            }
            if (e.Amount == World.CueBehind)
            {
                // Out of a wisp of mist behind its target.
                AddFlash(at + new Vector3(0, 0.6f, 0), 0.9f, new Color(0.6f, 0.95f, 0.85f), FlashKind.Impact);
                Hear(Sound.GateWarp, at, 0.7f);
                return true;
            }
            if (e.Amount == World.CueRewind)
            {
                AddFlash(at + new Vector3(0, 1.2f, 0), 1f, new Color(0.8f, 0.65f, 1f), FlashKind.Impact);
                return true;
            }
            return false;
        }

        // The wraith's look this frame, for its images (WorldView.CrownWraith.cs).
        void KeepWraithLook(BossView view, CrownWraithPose pose, int frame, int phase, bool turned) => _wraithLook = (view, pose, frame, phase, turned);
    }
}
