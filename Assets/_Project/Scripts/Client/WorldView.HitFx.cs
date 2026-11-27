using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // What a blow looks like on the body it lands on, and the number that comes off it (E7, Docs/EffectsRework.md 12):
    // the element's own pieces (HitFx) thrown to the far side of the blow, a star where it landed, and numbers that
    // pop, lean away from the blow, wear a dark outline and stand apart when several come off one body at once.
    sealed partial class WorldView
    {
        sealed class HitSpark
        {
            public Vector3 At;
            public float Age, Dir, Alpha;
            public HitElement Element;
            public FxRamp Ramp;
            public FxRamp? Star;
            public bool Strong, Thin;
            public int Seed;
        }

        readonly List<HitSpark> _hitSparks = new List<HitSpark>();
        // Which element each player's last skill carried, and when: a blow of theirs within a moment of it lands in
        // that element (the host's hit events name only a weapon's added element).
        readonly Dictionary<int, (SkillTag Tags, float Time)> _castTags = new Dictionary<int, (SkillTag, float)>();
        const float CastTagSeconds = 0.35f;
        // Past this many alive at once new ones lay half their pieces, and past the most none are added: a crowd
        // struck together stays far inside the screen's 420 pieces.
        const int HitFxThinOver = 10;
        const int HitFxMostAlive = 36;
        int _hitSeed;

        // Blows' effects running, the most pieces one of them laid last frame and all of theirs together, for the playtests.
        public int HitFxCount => _hitSparks.Count;
        public int HitFxPieces { get; private set; }
        public int HitFxAll { get; private set; }
        // Damage numbers showing, and how many places side by side they take (1 when none share a body).
        public int NumbersShown { get; private set; }
        public int NumberSlots { get; private set; }

        void AddHitFx(Vector3 at, HitElement element, FxRamp ramp, FxRamp? star, float degrees, bool strong, float alpha)
        {
            if (!Feel.Impact || _hitSparks.Count >= HitFxMostAlive) return;
            _hitSparks.Add(new HitSpark
            {
                At = at, Element = element, Ramp = ramp, Star = star, Dir = degrees, Strong = strong, Alpha = alpha,
                Thin = _hitSparks.Count >= HitFxThinOver, Seed = ++_hitSeed & 0xFFFF,
            });
        }

        // Laid with the skills' effects, on the same pooled sprites (called before the pool's rest is put away).
        void UpdateHitFx(float deltaTime)
        {
            int most = 0, all = 0;
            for (int i = _hitSparks.Count - 1; i >= 0; i--)
            {
                var hit = _hitSparks[i];
                hit.Age += deltaTime;
                if (Feel.SkillFxHold >= 0) hit.Age = Mathf.Min(hit.Age, Feel.SkillFxHold);
                _fxStamps.Clear();
                if (!Feel.Impact || !HitFx.Stamps(_fxStamps, hit.Element, hit.Ramp, hit.Star, hit.Dir, hit.Strong, hit.Thin, hit.Seed, hit.Age))
                {
                    _hitSparks.RemoveAt(i);
                    continue;
                }
                int before = _fxUsed;
                EmitFx(hit.At, hit.Alpha, null);
                most = Mathf.Max(most, _fxUsed - before);
                all += _fxUsed - before;
            }
            HitFxPieces = most;
            HitFxAll = all;
        }

        static HitElement HitElementOf(SkillTag tags) =>
            (tags & SkillTag.Fire) != 0 ? HitElement.Fire : (tags & SkillTag.Cold) != 0 ? HitElement.Cold : (tags & SkillTag.Lightning) != 0 ? HitElement.Bolt : HitElement.Plain;

        static HitElement HitElementOf(MobLook look) => look switch
        {
            MobLook.Spore => HitElement.Poison,
            MobLook.Crystal => HitElement.Crystal,
            MobLook.Frost => HitElement.Cold,
            MobLook.Ember => HitElement.Fire,
            MobLook.Sand => HitElement.Sand,
            MobLook.Water => HitElement.Water,
            MobLook.Shadow => HitElement.Void,
            MobLook.Hex => HitElement.Void,
            _ => HitElement.Plain,
        };

        static HitElement HitElementOf(ShotElement element) => element switch
        {
            ShotElement.Stone => HitElement.Stone,
            ShotElement.Crystal => HitElement.Crystal,
            ShotElement.Sand => HitElement.Sand,
            ShotElement.Fire => HitElement.Fire,
            ShotElement.Lava => HitElement.Fire,
            ShotElement.Void => HitElement.Void,
            ShotElement.Wraith => HitElement.Void,
            ShotElement.Water => HitElement.Water,
            _ => HitElement.Plain,
        };

        // What the striker's blows are made of: a player's by the element a buff lends or the skill just used, a
        // monster's by its kind and in its own colours, a boss's by its element.
        (HitElement Element, FxRamp Ramp) HitLookOf(int source, Body hitter)
        {
            if (hitter == null) return (HitElement.Plain, FxRamp.Steel);
            HitElement element;
            switch (hitter.Kind)
            {
                case BodyKind.Player:
                    var tags = GrantedTags(source);
                    if (_castTags.TryGetValue(source, out var cast) && Time.time - cast.Time < CastTagSeconds) tags |= cast.Tags;
                    element = HitElementOf(tags);
                    break;
                case BodyKind.Monster:
                    element = hitter.MobLooked ? HitElementOf(hitter.MobKind) : HitElement.Plain;
                    if (element != HitElement.Plain && hitter.MobRamp != FxRamp.Plain) return (element, hitter.MobRamp);
                    break;
                case BodyKind.Boss:
                    element = HitElementOf(ShotArt.ElementOf(hitter.Id));
                    break;
                default:
                    element = HitElement.Plain;
                    break;
            }
            return (element, HitFx.RampOf(element));
        }

        // A number off a struck body. Several within a moment off one body take places side by side (the middle,
        // right, left, further right, further left) instead of lying over each other. side: which way the blow
        // travelled across the screen (-1 to 1), for the lean.
        void AddNumber(Vector3 at, int target, string text, Color color, bool big, float side)
        {
            if (!Feel.Numbers) return;
            int taken = 0;
            foreach (var other in _floating)
            {
                if (other.Number && other.Target == target && other.Age < NumberCrowdSeconds) taken |= 1 << other.Slot;
            }
            int slot = 0;
            while (slot < NumberPlaces - 1 && (taken & (1 << slot)) != 0) slot++;
            _floating.Add(new Floating { Position = at, Text = text, Color = color, Big = big, Number = true, Target = target, Slot = slot, Side = Mathf.Clamp(side, -1, 1) });
        }

        const float NumberCrowdSeconds = 0.3f;
        const int NumberPlaces = 5;
        // Points between two numbers side by side at the usual size.
        const float NumberApart = 30;
        static readonly Color NumberEdge = new Color(0.07f, 0.04f, 0.1f);

        // A damage number: larger for a moment as it comes out, leaning away from the blow as it rises, with a dark
        // outline so it reads over any floor. size: a crit's or a finisher's is 1.35 times (as before).
        void NumberLabel(Floating text, Color color)
        {
            float t = text.Age / FloatSeconds;
            float size = text.Big ? (text.Age < 0.08f ? 1.6f : 1.35f) : text.Age < 0.06f ? 1.25f : 1;
            float lean = text.Side * 0.45f * (1 - (1 - t) * (1 - t));
            Vector3 screen = _camera.WorldToScreenPoint(text.Position + new Vector3(lean, 0.6f + t * 0.8f, 0));
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            // Its place among the numbers off the same body: 0, +1, -1, +2, -2, the outer ones a little higher.
            int step = (text.Slot + 1) / 2;
            point.x += (text.Slot % 2 == 1 ? step : -step) * NumberApart * (text.Big ? 1.35f : 1);
            point.y -= step * 7;
            _bigLabelStyle ??= new GUIStyle(_labelStyle);
            Ui.Text(_bigLabelStyle, Mathf.RoundToInt(15 * size), bold: true);
            var rect = new Rect(point.x - 100, point.y - 18, 200, 36);
            var previous = GUI.color;
            var edge = NumberEdge;
            edge.a = color.a;
            GUI.color = edge;
            float thick = text.Big ? 1.5f : 1;
            for (int i = 0; i < 4; i++)
            {
                GUI.Label(new Rect(rect.x + (i < 2 ? -thick : thick), rect.y + (i % 2 == 0 ? -thick : thick), rect.width, rect.height), text.Text, _bigLabelStyle);
            }
            GUI.color = color;
            GUI.Label(rect, text.Text, _bigLabelStyle);
            GUI.color = previous;
        }

        void CountNumbers()
        {
            int numbers = 0, slots = 0;
            foreach (var text in _floating)
            {
                if (!text.Number) continue;
                numbers++;
                slots |= 1 << text.Slot;
            }
            NumbersShown = numbers;
            int places = 0;
            for (int i = 0; i < NumberPlaces; i++) if ((slots & (1 << i)) != 0) places++;
            NumberSlots = places;
        }

        // A blow put on the floor by hand for the playtests' pictures (the command hit): the same effect and number a
        // real one gets. strong: a crit (gold star, big number). amount 0: no number. mine: struck oneself (red).
        public void StageHit(HitElement element, Vec2 at, float degrees, bool strong, int amount, bool added, bool mine)
        {
            var place = new Vector3(at.X, at.Y, 0);
            AddHitFx(place, element, HitFx.RampOf(element), added ? (FxRamp?)null : mine ? FxRamp.Danger : strong ? FxRamp.Gold : FxRamp.Steel, degrees, strong, 1);
            if (amount <= 0) return;
            var tag = element == HitElement.Fire ? SkillTag.Fire : element == HitElement.Cold ? SkillTag.Cold : element == HitElement.Bolt ? SkillTag.Lightning : SkillTag.None;
            var color = mine ? HurtNumber : added && tag != SkillTag.None ? Palette.Of(tag) : strong ? CritNumber : Color.white;
            AddNumber(place, -1000 - Mathf.RoundToInt(at.X * 4) * 1000 - Mathf.RoundToInt(at.Y * 4), strong ? amount + "!" : amount.ToString(), color, strong, Mathf.Cos(degrees * Mathf.Deg2Rad));
        }

        static readonly Color HurtNumber = new Color(1f, 0.35f, 0.3f);
        static readonly Color CritNumber = new Color(1f, 0.85f, 0.2f);
    }
}
