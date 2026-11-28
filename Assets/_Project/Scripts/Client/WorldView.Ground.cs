using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- A boss's ground and arena (2026-11-28, R2, Docs/Bosses.md "지형·물체·분노"): lava that glows and slowly
    // darkens as it cools, the black rock it leaves, shallow water, cracks; the edge that closes in from phase 3 (a band
    // of lava with islands of rock, a wall of petals, a curtain of webs); the vents standing in the arena; a raging
    // boss's white-hot glow. The pictures come from GroundArt; this places them and makes them move. ----
    sealed partial class WorldView
    {
        static readonly Color GroundLavaColor = new Color(1f, 0.45f, 0.12f);
        static readonly Color GroundLavaBright = new Color(1f, 0.85f, 0.4f);
        static readonly Color GroundRockColor = new Color(0.16f, 0.14f, 0.15f);
        static readonly Color GroundWaterColor = new Color(0.35f, 0.6f, 0.9f);
        static readonly Color GroundCrackColor = new Color(0.85f, 0.22f, 0.08f);
        static readonly Color GroundPetalColor = new Color(1f, 0.72f, 0.82f);
        static readonly Color GroundRageColor = new Color(1f, 0.95f, 0.8f);

        static int Px(float tiles) => Mathf.Max(4, Mathf.RoundToInt(tiles * Sprites.PixelsPerUnit));

        static Sprite GroundSprite(HazardShape shape, bool strip, float width, float height, int id, int frame)
        {
            int w = Px(width), h = Px(height), variant = id % 3;
            return Sprites.Centered($"fx/ground/{shape}/{(strip ? 1 : 0)}/{w}x{h}/{variant}/{frame}", () => GroundArt.Ground(shape, strip, w, h, variant, frame));
        }

        static Sprite EdgeSprite(EdgeKind kind, float outer, float floor, int frame)
        {
            int o = Px(outer), f = Px(floor);
            return Sprites.Centered($"fx/edge/{kind}/{o}/{f}/{frame}", () => GroundArt.Edge(kind, o, f, frame));
        }

        static Sprite PropSprite(PropKind kind, int frame) => Sprites.Centered($"fx/prop/{kind}/{frame}", () => GroundArt.Prop(kind, frame));

        static bool IsGround(HazardState hazard) => hazard.Shape == HazardShape.Lava || hazard.Shape == HazardShape.Water
            || hazard.Shape == HazardShape.Crack || hazard.Shape == HazardShape.Rock;

        // Ground on the floor now, for the playtests (each kind).
        public int GroundShown(HazardShape shape) => _marks.Values.Count(m => m.State.Shape == shape);

        // A pool is a circle of Size; a strip runs Size along Direction from Position and Width across.
        Mark MakeGround(HazardState hazard)
        {
            var root = new GameObject("Ground").transform;
            root.SetParent(_root.transform, false);
            bool strip = hazard.Width > 0;
            var middle = strip ? hazard.Position + hazard.Direction * (hazard.Size / 2) : hazard.Position;
            root.localPosition = new Vector3(middle.X, middle.Y, 0);
            if (strip) root.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(hazard.Direction.Y, hazard.Direction.X) * Mathf.Rad2Deg);
            var (w, h) = strip ? (hazard.Size, hazard.Width) : (hazard.Size * 2, hazard.Size * 2);
            var mark = new Mark
            {
                Root = root,
                Edge = Renderer(root, "Ground", GroundSprite(hazard.Shape, strip, w, h, hazard.Id, 0), Color.clear),
                Fill = Renderer(root, "Glow", strip ? Shapes.Square : Shapes.Circle, Color.clear),
                State = hazard,
            };
            mark.Fill.transform.localScale = new Vector3(w, h, 1);
            // Under every body, with webs and venom; rock over the lava it cools on.
            mark.Fill.sortingOrder = 31;
            mark.Edge.sortingOrder = hazard.Shape == HazardShape.Rock ? 33 : 32;
            return mark;
        }

        // Lava seethes and darkens over its last seconds; rock fades in from the lava's colour; water ripples; a crack
        // glows at its heart. All fade as their time runs out. The first lava of a fight (harmless) is dimmer.
        void DrawGround(Mark mark, HazardView view)
        {
            var hazard = view.State;
            bool strip = hazard.Width > 0;
            float age = view.Progress * hazard.Duration / World.TicksPerSecond;
            float left = (1 - view.Progress) * hazard.Duration / World.TicksPerSecond;
            float grown = Mathf.Clamp01(age / 0.25f);
            float fade = Mathf.Clamp01(left / 1.0f);
            var (w, h) = strip ? (hazard.Size, hazard.Width) : (hazard.Size * 2, hazard.Size * 2);
            int frame = Mathf.FloorToInt(Time.time * 4 + hazard.Id) % GroundArt.Frames;
            mark.Edge.sprite = GroundSprite(hazard.Shape, strip, w, h, hazard.Id, frame);
            float spread = 0.5f + 0.5f * (1 - (1 - grown) * (1 - grown));
            mark.Edge.transform.localScale = strip ? new Vector3(spread, 1, 1) : Vector3.one * spread;
            switch (hazard.Shape)
            {
                case HazardShape.Lava:
                    // Cooling: its last two seconds it goes from glowing to dark (then rock takes its place).
                    float cool = Mathf.Clamp01(left / 2f);
                    float dim = hazard.Harmful ? 1 : 0.7f;
                    mark.Edge.color = Color.Lerp(new Color(0.45f, 0.3f, 0.28f), Color.white, cool) * new Color(1, 1, 1, dim);
                    mark.Fill.color = new Color(GroundLavaColor.r, GroundLavaColor.g, GroundLavaColor.b, cool * dim * (0.12f + 0.06f * Mathf.Sin(Time.time * 5 + hazard.Id)));
                    if (Feel.Bits && hazard.Harmful && Random.value < 0.05f) Ember(hazard);
                    if (hazard.Harmful && cool > 0.5f) Bubble(hazard);
                    break;
                case HazardShape.Rock:
                    float set = Mathf.Clamp01(age / 0.6f);
                    mark.Edge.color = new Color(1, 1, 1, fade);
                    mark.Fill.color = new Color(GroundLavaColor.r, GroundLavaColor.g, GroundLavaColor.b, (1 - set) * 0.25f * fade);
                    break;
                case HazardShape.Water:
                    mark.Edge.color = new Color(1, 1, 1, fade * 0.9f);
                    mark.Fill.color = new Color(GroundWaterColor.r, GroundWaterColor.g, GroundWaterColor.b, fade * (0.1f + 0.04f * Mathf.Sin(Time.time * 3 + hazard.Id)));
                    break;
                default:
                    mark.Edge.color = new Color(1, 1, 1, fade * (hazard.Harmful ? 1 : 0.7f));
                    mark.Fill.color = new Color(GroundCrackColor.r, GroundCrackColor.g, GroundCrackColor.b, fade * (0.08f + 0.06f * Mathf.Sin(Time.time * 7 + hazard.Id)));
                    break;
            }
        }

        // Lava bubbling: a small burst now and then from one of the harmful pools near enough to be on the screen, never
        // two within BubbleGap of each other however much lava lies about.
        const float BubbleGap = 0.9f, BubbleNear = 9;
        float _bubbleAfter;
        public int BubblesHeard { get; private set; }

        void Bubble(HazardState hazard)
        {
            if (Time.time < _bubbleAfter || Random.value > 0.04f) return;
            var at = hazard.Width > 0 ? hazard.Position + hazard.Direction * (hazard.Size / 2) : hazard.Position;
            var place = new Vector3(at.X, at.Y, 0);
            if (Vector2.Distance(place, _self.Position) > BubbleNear + hazard.Size) return;
            _bubbleAfter = Time.time + BubbleGap + Random.value * 1.4f;
            BubblesHeard++;
            Hear(Sound.LavaBubble, place, 0.55f);
        }

        // A spark off lava, rising and going out.
        void Ember(HazardState hazard)
        {
            var at = hazard.Width > 0
                ? hazard.Position + hazard.Direction * (Random.value * hazard.Size) + new Vec2(-hazard.Direction.Y, hazard.Direction.X) * ((Random.value - 0.5f) * hazard.Width)
                : hazard.Position + new Vec2(Random.insideUnitCircle.x, Random.insideUnitCircle.y) * hazard.Size * 0.8f;
            var spark = Renderer(_root.transform, "Ember", Shapes.Square, GroundLavaBright);
            spark.transform.localPosition = new Vector3(at.X, at.Y, 0);
            spark.transform.localScale = Vector3.one * (1f / Sprites.PixelsPerUnit);
            spark.sortingOrder = 19000;
            _bits.Add(new Bit { Renderer = spark, Velocity = new Vector3(0, 1.1f, 0), Color = GroundLavaBright, Life = 0.6f, Drag = 0.4f, Sway = 0.3f, Phase = Random.value * 6 });
        }

        // ---- The edge, the vents and the rage, drawn each frame from the bosses in view ----

        SpriteRenderer _edgeBand;
        readonly List<SpriteRenderer> _islands = new List<SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _propSprites = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _rageGlows = new Dictionary<int, SpriteRenderer>();
        float _edgeSince = -1;

        // For the playtests: whether an edge is drawn, how many vents, and whether a boss glows with rage.
        public bool EdgeShown => _edgeBand != null && _edgeBand.enabled;
        public int PropsShown => _propSprites.Count;
        public bool RageShown => _rageGlows.Values.Any(g => g.enabled);

        void DrawArenaGround()
        {
            var bosses = _client.Others().Where(e => e.Kind == EntityKind.Boss && !e.IsDead).ToList();
            DrawEdge(bosses);
            DrawProps();
            DrawRage(bosses);
        }

        void DrawEdge(List<EntityView> bosses)
        {
            (Vec2 Home, float Arena, float Floor, BossEdge Edge)? shown = null;
            foreach (var boss in bosses)
            {
                shown = _client.EdgeOf(boss);
                if (shown != null) break;
            }
            if (shown == null)
            {
                if (_edgeBand != null) _edgeBand.enabled = false;
                foreach (var island in _islands) island.enabled = false;
                _edgeSince = -1;
                return;
            }
            var (home, arena, floor, edge) = shown.Value;
            if (_edgeSince < 0) _edgeSince = Time.time;
            float outer = arena + 1;
            if (_edgeBand == null)
            {
                _edgeBand = Renderer(_root.transform, "Edge", Shapes.Circle, Color.clear);
                _edgeBand.sortingOrder = 30;
            }
            _edgeBand.enabled = true;
            _edgeBand.sprite = EdgeSprite(edge.Kind, outer, floor, Mathf.FloorToInt(Time.time * 4) % GroundArt.Frames);
            _edgeBand.transform.localPosition = new Vector3(home.X, home.Y, 0);
            _edgeBand.transform.localScale = Vector3.one;
            // It rises over the grace (World.EdgeGraceSeconds): faint at first, then whole and biting.
            float rise = Mathf.Clamp01((Time.time - _edgeSince) / World.EdgeGraceSeconds);
            _edgeBand.color = new Color(1, 1, 1, 0.35f + 0.65f * rise);
            var spots = World.IslandSpots(home, arena, edge).ToList();
            while (_islands.Count < spots.Count)
            {
                var island = Renderer(_root.transform, "Island", GroundSprite(HazardShape.Rock, false, World.IslandRadius * 2, World.IslandRadius * 2, 7 + _islands.Count, 0), Color.white);
                island.sortingOrder = 33;
                _islands.Add(island);
            }
            for (int i = 0; i < _islands.Count; i++)
            {
                _islands[i].enabled = i < spots.Count;
                if (i < spots.Count) _islands[i].transform.localPosition = new Vector3(spots[i].X, spots[i].Y, 0);
            }
        }

        readonly HashSet<int> _propsSeen = new HashSet<int>();

        void DrawProps()
        {
            _propsSeen.Clear();
            foreach (var prop in _client.Props())
            {
                _propsSeen.Add(prop.Id);
                if (!_propSprites.TryGetValue(prop.Id, out var sprite))
                {
                    sprite = Renderer(_root.transform, "Prop", PropSprite(prop.Kind, 0), Color.white);
                    // A vent is a hole in the floor, under everyone.
                    sprite.sortingOrder = 35;
                    _propSprites[prop.Id] = sprite;
                }
                sprite.transform.localPosition = new Vector3(prop.Position.X, prop.Position.Y, 0);
                sprite.sprite = PropSprite(prop.Kind, Mathf.FloorToInt(Time.time * 6 + prop.Id) % GroundArt.Frames);
            }
            foreach (int id in _propSprites.Keys.Where(id => !_propsSeen.Contains(id)).ToList())
            {
                Object.Destroy(_propSprites[id].gameObject);
                _propSprites.Remove(id);
            }
        }

        // A raging boss glows white-hot under itself, pulsing; the moment it starts it flares (SimEventType.Rage).
        void DrawRage(List<EntityView> bosses)
        {
            foreach (var boss in bosses)
            {
                if (!_rageGlows.TryGetValue(boss.Id, out var glow))
                {
                    glow = Renderer(_root.transform, "Rage", Shapes.Circle, Color.clear);
                    glow.sortingOrder = 36;
                    _rageGlows[boss.Id] = glow;
                }
                glow.enabled = boss.Raging;
                if (!boss.Raging) continue;
                glow.transform.localPosition = new Vector3(boss.Position.X, boss.Position.Y, 0);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9);
                glow.transform.localScale = Vector3.one * (boss.Radius * 2.6f + 0.3f * pulse);
                glow.color = new Color(GroundRageColor.r, GroundRageColor.g, GroundRageColor.b, 0.18f + 0.14f * pulse);
            }
            foreach (int id in _rageGlows.Keys.Where(id => bosses.All(b => b.Id != id)).ToList())
            {
                Object.Destroy(_rageGlows[id].gameObject);
                _rageGlows.Remove(id);
            }
        }

        // The rage starting or spent (SimEventType.Rage): words over the boss and a sound.
        void ShowRage(SimEvent e, Vector3 at)
        {
            bool on = e.Amount != 0;
            _floating.Add(new Floating { Position = at + new Vector3(0, 1.4f, 0), Text = on ? "과열!" : "식는다", Color = on ? GroundRageColor : new Color(0.7f, 0.7f, 0.75f) });
            Hear(on ? Sound.Overheat : Sound.Sizzle, at, on ? 0.9f : 0.6f);
            if (on && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.12f);
        }
    }
}
