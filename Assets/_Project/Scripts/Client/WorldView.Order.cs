using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The boss bundle R4a (2026-11-28, Docs/BossRedesign.md 5.3·5.5, Docs/Bosses.md "순서·막간") on the screen. The
    // queen's crystals light one by one (each burst's mark sits on its crystal; its place in the order is read from how
    // much longer than the first it waits), while their marks fill in together and then blink until each bursts, so the
    // order is only in the lights: remember it. The interlude is named under the boss bar with the time it has left. ----
    sealed partial class WorldView
    {
        // For the playtests: crystals lit now, and the most lit at once since the last order began; the interlude named.
        public int CrystalsLit { get; private set; }
        public int CrystalsLitSeen { get; private set; }
        public int ChimesHeard { get; private set; }

        // Where each light of an order is, and the seconds since it lit (negative before), refreshed each frame.
        readonly List<(Vec2 At, float Since)> _orderLights = new List<(Vec2, float)>();
        readonly HashSet<int> _chimed = new HashSet<int>();

        // An order's crystals light spread seconds apart from the start of its wind-up, in the order they will burst.
        void ReadLights()
        {
            _orderLights.Clear();
            int lit = 0;
            foreach (var view in _client.Hazards())
            {
                var hazard = view.State;
                var attack = AttackOf(hazard)?.ForPhase(_bossPhaseInView);
                if (attack == null || attack.Shape != AttackShape.Order) continue;
                int first = World.Ticks(World.OrderTelegraph(attack));
                int place = Mathf.Max(0, Mathf.RoundToInt((hazard.Duration - first) / (float)World.Ticks(World.OrderBurstGap)));
                float elapsed = view.Progress * hazard.Duration / World.TicksPerSecond;
                float since = elapsed - place * attack.Spread;
                _orderLights.Add((hazard.Position, since));
                if (since >= 0 && since < LightSeconds) lit++;
                // Each light chimes once, louder down the order.
                if (since >= 0 && _chimed.Add(hazard.Id))
                {
                    ChimesHeard++;
                    Hear(Sound.CrystalChime, new Vector3(hazard.Position.X, hazard.Position.Y, 0), 0.6f + 0.1f * Mathf.Min(place, 4));
                }
            }
            CrystalsLit = lit;
            if (_orderLights.Count == 0) _chimed.Clear();
            CrystalsLitSeen = Mathf.Max(lit, _orderLights.Count == 0 ? 0 : CrystalsLitSeen);
        }

        // ---- What the order, the marks and the bands put on the floor (Fable's pictures, ForceArt.R4a.cs) ----

        // For the playtests: targets drawn under marked players, chime rings now, and volleys heard.
        public int TargetsShown { get; private set; }
        public int RingsShown { get; private set; }
        public int VolleysHeard { get; private set; }

        readonly Dictionary<int, SpriteRenderer> _targets = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, (SpriteRenderer Ring, float Born)> _rings = new Dictionary<int, (SpriteRenderer, float)>();
        readonly HashSet<int> _volleys = new HashSet<int>();
        readonly HashSet<int> _marksSeen = new HashSet<int>();
        float _volleyAt = -10;

        // A marked player's target turns at their feet; a crystal's light rings out; a volley of bands rains down.
        void DrawOrderFx()
        {
            _marksSeen.Clear();
            foreach (var view in _client.Hazards())
            {
                var hazard = view.State;
                var attack = AttackOf(hazard)?.ForPhase(_bossPhaseInView);
                if (attack == null) continue;
                var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
                if (attack.Shape == AttackShape.Mark)
                {
                    _marksSeen.Add(hazard.Id);
                    if (!_targets.TryGetValue(hazard.Id, out var target))
                    {
                        target = Renderer(_root.transform, "Target", null, Color.white);
                        target.sortingOrder = DarkOrder + 14;
                        _targets[hazard.Id] = target;
                    }
                    int frame = Mathf.FloorToInt(Time.time * 8) % ForceArt.Frames;
                    target.sprite = Sprites.Centered($"fx/target/{frame}", () => ForceArt.Target(frame));
                    target.transform.localPosition = at;
                    target.transform.localScale = Vector3.one * (hazard.Size * 2 * Sprites.PixelsPerUnit / ForceArt.TargetSize);
                }
                else if (attack.Shape == AttackShape.Bands && _volleys.Add(hazard.Id) && Time.time - _volleyAt > 2)
                {
                    // Once for the volley, as its bands are laid.
                    _volleyAt = Time.time;
                    VolleysHeard++;
                    Hear(Sound.ArrowRain, at, 0.9f);
                }
            }
            foreach (int id in _targets.Keys.Where(id => !_marksSeen.Contains(id)).ToList())
            {
                Object.Destroy(_targets[id].gameObject);
                _targets.Remove(id);
            }
            TargetsShown = _targets.Count;
            if (_volleys.Count > 64) _volleys.Clear();
            // Each light rings once, spreading and fading over its four frames.
            foreach (var (spot, since) in _orderLights)
            {
                int key = Mathf.RoundToInt(spot.X * 100) * 7919 + Mathf.RoundToInt(spot.Y * 100);
                if (since < 0 || since > LightSeconds || _rings.ContainsKey(key)) continue;
                var ring = Renderer(_root.transform, "Chime", null, new Color(0.75f, 0.95f, 1f));
                ring.sortingOrder = DarkOrder + 15;
                ring.transform.localPosition = new Vector3(spot.X, spot.Y + 0.6f, 0);
                _rings[key] = (ring, Time.time);
            }
            foreach (var key in _rings.Keys.ToList())
            {
                var (ring, born) = _rings[key];
                float age = Time.time - born;
                if (age > LightSeconds * 1.5f)
                {
                    Object.Destroy(ring.gameObject);
                    _rings.Remove(key);
                    continue;
                }
                int frame = Mathf.Min(ForceArt.Frames - 1, Mathf.FloorToInt(age / LightSeconds * ForceArt.Frames));
                ring.sprite = Sprites.Centered($"fx/chime/{frame}", () => ForceArt.Chime(frame));
                ring.transform.localScale = Vector3.one * 1.6f;
            }
            RingsShown = _rings.Count;
        }

        // How long a crystal stays lit after its turn.
        const float LightSeconds = 0.45f;

        // How lit the crystal standing at a spot is now: 1 at its turn, fading over LightSeconds.
        float CrystalLight(Vec2 at)
        {
            float best = 0;
            foreach (var (spot, since) in _orderLights)
            {
                if (Vec2.Distance(spot, at) > 0.3f || since < 0 || since > LightSeconds) continue;
                best = Mathf.Max(best, 1 - since / LightSeconds);
            }
            return best;
        }

        // An order's mark fills over the time to its first burst, the same for all, then stays full, blinking, until it
        // bursts: the marks do not give the order away.
        float OrderProgress(HazardState hazard, BossAttack attack, float progress)
        {
            float seconds = progress * hazard.Duration / World.TicksPerSecond;
            return Mathf.Clamp01(seconds / Mathf.Max(0.1f, World.OrderTelegraph(attack)));
        }

        // The interlude beginning (horn) and over (SimEventType.Cue, World.CueInterlude): words over the boss.
        void ShowInterlude(SimEvent e, Body body)
        {
            var at = body.Root.localPosition;
            bool begins = e.Amount == World.CueInterlude;
            string text = begins ? "막간!" : "막간 끝";
            _floating.Add(new Floating { Position = at + new Vector3(0, 1.6f, 0), Text = text, Color = begins ? new Color(1f, 0.8f, 0.4f) : new Color(0.75f, 0.75f, 0.8f) });
            if (begins)
            {
                Hear(Sound.WarHorn, at, 0.9f);
                if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.1f);
            }
        }
    }
}
