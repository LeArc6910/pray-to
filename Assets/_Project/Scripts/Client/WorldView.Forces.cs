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

        readonly Dictionary<int, List<SpriteRenderer>> _forces = new Dictionary<int, List<SpriteRenderer>>();
        // What each was when last seen: a thread's cuts left and length, whether a wall had begun to move (for its sound).
        readonly Dictionary<int, (HazardShape Shape, float Cuts, float Size, bool Moving, Vec2 At)> _forceKnown = new Dictionary<int, (HazardShape, float, float, bool, Vec2)>();
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
                if (!_forces.TryGetValue(hazard.Id, out var parts))
                {
                    _forces[hazard.Id] = parts = new List<SpriteRenderer>();
                    // A pull or push begins: sand (or water) rushing.
                    if (hazard.Shape == HazardShape.Pull || hazard.Shape == HazardShape.Push) Hear(Sound.SandDrag, new Vector3(hazard.Position.X, hazard.Position.Y, 0), 0.7f);
                }
                (HazardShape Shape, float Cuts, float Size, bool Moving, Vec2 At) was = _forceKnown.TryGetValue(hazard.Id, out var known) ? known : (hazard.Shape, hazard.Width, hazard.Size, false, hazard.Position);
                var mid = hazard.Position + hazard.Direction * (hazard.Size / 2);
                // A thread cut once: a twang where it was cut.
                if (hazard.Shape == HazardShape.Tether && hazard.Width < was.Cuts) Hear(Sound.ThreadCut, new Vector3(mid.X, mid.Y, 0), 0.9f);
                bool moving = hazard.Shape == HazardShape.Wave && view.Progress > 0;
                if (moving && !was.Moving) Hear(Sound.WaveRush, new Vector3(hazard.Position.X, hazard.Position.Y, 0), 1);
                _forceKnown[hazard.Id] = (hazard.Shape, hazard.Width, hazard.Size, moving, mid);
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
                // A thread gone with its last cut, or as two held together came too close: it snaps.
                if (_forceKnown.TryGetValue(id, out var gone) && gone.Shape == HazardShape.Tether && (gone.Cuts <= 1 || gone.Size < World.ThreadSnap + 0.3f))
                    Hear(Sound.ThreadSnap, new Vector3(gone.At.X, gone.At.Y, 0), 1);
                _forceKnown.Remove(id);
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

        // A straight piece from a to b: its picture's width runs from a to b (its top on the left of the way), drawn
        // at a length rounded to step pixels and stretched the rest.
        static void Lay(SpriteRenderer part, Vec2 a, Vec2 b, string key, int step, System.Func<int, PixelImage> draw)
        {
            var run = b - a;
            var middle = a + run * 0.5f;
            int pixels = Mathf.Max(step, Mathf.RoundToInt(run.Length * Sprites.PixelsPerUnit / step) * step);
            part.sprite = Sprites.Centered($"{key}/{pixels}", () => draw(pixels));
            part.transform.localPosition = new Vector3(middle.X, middle.Y, 0);
            part.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(run.Y, run.X) * Mathf.Rad2Deg);
            part.transform.localScale = new Vector3(Mathf.Max(0.01f, run.Length * Sprites.PixelsPerUnit / pixels), 1, 1);
        }

        // Silk from its anchor to whoever it holds: two pixels, fainter with each cut, reddening as two held together
        // close in on where it snaps, and a little slack-sway when it is not stretched.
        void DrawThread(List<SpriteRenderer> parts, HazardState thread)
        {
            var from = thread.Position + new Vec2(0, 0.6f);
            var to = thread.Position + thread.Direction * thread.Size + new Vec2(0, 0.5f);
            var part = Part(parts, 0, DarkOrder + 14);
            int frame = Mathf.FloorToInt(Time.time * 6 + thread.Id) % ForceArt.Frames;
            Lay(part, from, to, $"fx/thread/{frame}", 16, px => ForceArt.Thread(px, frame));
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
            float alpha = moving ? 1 : 0.35f + 0.15f * Mathf.Sin(Time.time * 6);
            int frame = Mathf.FloorToInt(Time.time * 8 + wave.Id) % ForceArt.Frames;
            int ends = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                var (from, to) = pieces[i];
                var part = Part(parts, i, DarkOrder + 12);
                // Laid from its right end to its left, so the picture's top (its foaming front) faces the way it runs.
                Lay(part, front + side * to, front + side * from, $"fx/wave/{frame}", 8, px => ForceArt.WaveBand(px, frame));
                part.color = new Color(1, 1, 1, alpha);
                // At the gap the wall curls over (the right-hand end drawn; the left one turned over).
                if (wave.Width > 0)
                {
                    bool left = to <= 0;
                    var curl = Part(parts, pieces.Count + ends++, DarkOrder + 13);
                    var at = front + side * (left ? to : from);
                    curl.sprite = Sprites.Centered($"fx/waveend/{frame}", () => ForceArt.WaveEnd(frame));
                    curl.transform.localPosition = new Vector3(at.X, at.Y, 0);
                    curl.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-side.Y, -side.X) * Mathf.Rad2Deg);
                    curl.transform.localScale = new Vector3(left ? 1 : -1, 1, 1);
                    curl.color = new Color(1, 1, 1, alpha);
                }
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
            bool water = _bossInView != null && _bossInView.Id == "tide_serpent";
            int frame = Random.value < 0.5f ? 0 : 1;
            var grain = Renderer(_root.transform, "Grain", Sprites.Centered($"fx/grain/{water}/{frame}", () => ForceArt.Grain(water, frame)), Color.white);
            grain.sortingOrder = DarkOrder + 13;
            grain.transform.localPosition = new Vector3(at.X, at.Y, 0);
            var heading = way.Normalized;
            grain.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.Y, heading.X) * Mathf.Rad2Deg);
            float speed = drag.Shape == HazardShape.Pull ? 2.5f : 2f;
            _bits.Add(new Bit { Renderer = grain, Velocity = new Vector3(heading.X, heading.Y, 0) * speed, Color = Color.white, Life = 0.7f, Drag = 0 });
        }

        // The serpent's dive: a V of ripples left where its head passes under the water, fading behind it.
        void Wake(Vector3 at, Vec2 facing)
        {
            if (!Feel.Bits || facing.LengthSquared < 1e-4f) return;
            int frame = Mathf.FloorToInt(Time.time * 8) % ForceArt.Frames;
            var wake = Renderer(_root.transform, "Wake", Sprites.Centered($"fx/wake/{frame}", () => ForceArt.Wake(frame)), Color.white);
            wake.sortingOrder = DarkOrder + 13;
            wake.transform.localPosition = at;
            wake.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.Y, facing.X) * Mathf.Rad2Deg);
            _bits.Add(new Bit { Renderer = wake, Velocity = Vector3.zero, Color = Color.white, Life = 0.6f });
        }
    }
}
