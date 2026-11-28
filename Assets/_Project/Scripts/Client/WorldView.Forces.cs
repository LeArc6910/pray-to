using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- R3 (2026-11-28, Docs/Bosses.md "끌기·숨기·벽"): what drags and what sweeps. A boss's pull draws sand or water
    // in toward its middle, a push throws it out (specks streaming the way the force goes, so the way to walk against it
    // reads); a thread is a fine line of silk from the boss (or the other player) to whoever it holds, fainter for each
    // cut; a wall of water is a band across the arena, only as wide as the arena's floor, with its gap open. The band
    // waits faintly where it will start while the boss winds up. ----
    sealed partial class WorldView
    {
        static readonly Color ThreadColor = new Color(0.96f, 0.95f, 1f);
        static readonly Color ThreadSnapColor = new Color(1f, 0.45f, 0.4f);
        static readonly Color WaveColor = new Color(0.42f, 0.72f, 1f);

        readonly Dictionary<int, List<SpriteRenderer>> _forces = new Dictionary<int, List<SpriteRenderer>>();
        readonly HashSet<int> _forcesSeen = new HashSet<int>();
        float _forceSpeckAt;

        // For the playtests: threads, walls and drags drawn now.
        public int ThreadsShown { get; private set; }
        public int WavesShown { get; private set; }
        public int DragsShown { get; private set; }

        static bool IsForce(HazardState hazard) => !hazard.FromMonster && (hazard.Shape == HazardShape.Pull || hazard.Shape == HazardShape.Push
            || hazard.Shape == HazardShape.Tether || hazard.Shape == HazardShape.Wave);

        void DrawForces(List<HazardView> views)
        {
            _forcesSeen.Clear();
            ThreadsShown = WavesShown = DragsShown = 0;
            bool speck = Time.time >= _forceSpeckAt;
            if (speck) _forceSpeckAt = Time.time + 0.04f;
            foreach (var view in views)
            {
                var hazard = view.State;
                if (!IsForce(hazard)) continue;
                _forcesSeen.Add(hazard.Id);
                if (!_forces.TryGetValue(hazard.Id, out var parts)) _forces[hazard.Id] = parts = new List<SpriteRenderer>();
                switch (hazard.Shape)
                {
                    case HazardShape.Tether:
                        DrawThread(parts, hazard);
                        ThreadsShown++;
                        break;
                    case HazardShape.Wave:
                        if (DrawWave(parts, hazard, view.Progress)) WavesShown++;
                        break;
                    default:
                        if (speck) Drift(hazard);
                        DragsShown++;
                        break;
                }
            }
            foreach (int id in _forces.Keys.Where(id => !_forcesSeen.Contains(id)).ToList())
            {
                foreach (var part in _forces[id]) Object.Destroy(part.gameObject);
                _forces.Remove(id);
            }
        }

        SpriteRenderer Part(List<SpriteRenderer> parts, int index, int order)
        {
            while (parts.Count <= index)
            {
                var part = Renderer(_root.transform, "Force", Shapes.Square, Color.clear);
                part.sortingOrder = order;
                parts.Add(part);
            }
            parts[index].enabled = true;
            return parts[index];
        }

        // A straight piece from a to b, thick tiles across.
        static void Lay(SpriteRenderer part, Vec2 a, Vec2 b, float thick)
        {
            var run = b - a;
            var middle = a + run * 0.5f;
            part.transform.localPosition = new Vector3(middle.X, middle.Y, 0);
            part.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(run.Y, run.X) * Mathf.Rad2Deg);
            part.transform.localScale = new Vector3(Mathf.Max(0.01f, run.Length), thick, 1);
        }

        // Silk from its anchor to whoever it holds: two pixels, fainter with each cut, reddening as two held together
        // close in on where it snaps, and a little slack-sway when it is not stretched.
        void DrawThread(List<SpriteRenderer> parts, HazardState thread)
        {
            var from = thread.Position + new Vec2(0, 0.6f);
            var to = thread.Position + thread.Direction * thread.Size + new Vec2(0, 0.5f);
            var part = Part(parts, 0, DarkOrder + 14);
            Lay(part, from, to, 2f / Sprites.PixelsPerUnit);
            float left = Mathf.Clamp01(thread.Width / World.ThreadCuts);
            bool taut = thread.Size >= World.ThreadSlack - 0.2f;
            float near = Mathf.Clamp01((World.ThreadSnap + 1.5f - thread.Size) / 1.5f);
            var colour = Color.Lerp(ThreadColor, ThreadSnapColor, near * (0.5f + 0.5f * Mathf.Sin(Time.time * 18)));
            part.color = new Color(colour.r, colour.g, colour.b, (taut ? 0.95f : 0.7f) * (0.35f + 0.65f * left));
        }

        // A wall of water across the arena's floor at where it has got to (from its start, faint, until it moves), split
        // round its gap.
        bool DrawWave(List<SpriteRenderer> parts, HazardState wave, float progress)
        {
            foreach (var p in parts) p.enabled = false;
            var front = wave.Position + wave.Direction * (wave.Size * progress);
            var side = new Vec2(-wave.Direction.Y, wave.Direction.X);
            if (_client.Arenas.Count == 0) return false;
            var home = _client.Arenas.OrderBy(a => Vec2.Distance(a, front)).First();
            float reach = _client.ArenaRadius + 0.5f;
            float off = Vec2.Dot(home - front, wave.Direction);
            if (Mathf.Abs(off) >= reach) return false;
            float chord = Mathf.Sqrt(reach * reach - off * off);
            float middle = Vec2.Dot(home - front, side);
            float lo = middle - chord, hi = middle + chord;
            var pieces = new List<(float From, float To)>();
            if (wave.Width > 0)
            {
                if (lo < -wave.Width / 2) pieces.Add((lo, Mathf.Min(hi, -wave.Width / 2)));
                if (hi > wave.Width / 2) pieces.Add((Mathf.Max(lo, wave.Width / 2), hi));
            }
            else pieces.Add((lo, hi));
            bool moving = progress > 0;
            float alpha = moving ? 0.8f : 0.3f + 0.15f * Mathf.Sin(Time.time * 6);
            for (int i = 0; i < pieces.Count; i++)
            {
                var (from, to) = pieces[i];
                var part = Part(parts, i, DarkOrder + 12);
                Lay(part, front + side * from, front + side * to, World.WaveThick);
                part.color = new Color(WaveColor.r, WaveColor.g, WaveColor.b, alpha);
                // Foam thrown off its leading edge as it runs.
                if (moving && Feel.Bits && Random.value < 0.5f)
                {
                    var at = front + side * Mathf.Lerp(from, to, Random.value) + wave.Direction * (World.WaveThick / 2);
                    Chips(new Vector3(at.X, at.Y, 0), new Vector3(wave.Direction.X, wave.Direction.Y, 0) + Vector3.up * 0.4f, FxRamp.Mist, 1, 8, 50);
                }
            }
            return pieces.Count > 0;
        }

        // A speck of sand (or water, for the serpent) streaming toward a pull's middle or out of a push's.
        void Drift(HazardState drag)
        {
            if (!Feel.Bits) return;
            float reach = Mathf.Min(drag.Size, _client.ArenaRadius > 0 ? _client.ArenaRadius : drag.Size);
            var at = drag.Position + new Vec2(Random.insideUnitCircle.x, Random.insideUnitCircle.y) * reach;
            var way = drag.Shape == HazardShape.Pull ? drag.Position - at : at - drag.Position;
            if (way.LengthSquared < 0.01f) return;
            var ramp = _bossInView != null && _bossInView.Id == "tide_serpent" ? FxRamp.Cold : FxRamp.Dust;
            Chips(new Vector3(at.X, at.Y, 0), new Vector3(way.X, way.Y, 0), ramp, 1, 10, 12);
        }
    }
}
