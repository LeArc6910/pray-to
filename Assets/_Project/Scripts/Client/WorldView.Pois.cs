using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The things to find in the rooms, as the screen shows them (2026-11-22, Docs/WorldRework.md 3단계, 묶음 3a·3b): what
    // each signals from afar (먼저 보이고 나중에 닿는다) and what it does when used. Smoke rises over a camp's fire, a pillar
    // of light stands on a shrine, a chest or cart not yet opened glints, a camp of elites flies a red flag; these are
    // drawn over the dark, so they are seen before the light reaches them, and one in its range but off the screen is
    // pointed to from the screen's edge. A cracked wall puffs dust near by. Each gives light (WorldLights) and a sound.
    partial class WorldView
    {
        // The flag and pillar renderers, reused frame to frame.
        readonly List<SpriteRenderer> _beams = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer Pole, SpriteRenderer Cloth)> _flags = new List<(SpriteRenderer, SpriteRenderer)>();
        // Where the elite camps fly their flags: by each camp's leader's mark (RoomMarkKind.Elite).
        List<Vec2> _campFlags;
        // Signals in range but off the screen, for DrawGui to point to: where, the colour, and whether it is a raid's
        // (bigger, quicker).
        readonly List<(Vec2 At, Color Color, bool Raid)> _farSignals = new List<(Vec2, Color, bool)>();
        float _puffClock, _glintClock, _dustClock, _drumClock, _windClock;

        const int SignalOrder = DarkOrder + 20;
        // How far an elite camp's flag is seen, and its drum heard (Docs/WorldRework.md: 북소리(12칸)).
        const float FlagRange = 24;
        const float DrumRange = 12;
        // Dust from a cracked wall within this, and its draught heard within WindRange.
        const float DustRange = 4;
        const float WindRange = 6;

        static readonly Color SmokeColor = new Color(0.72f, 0.7f, 0.68f, 0.55f);
        static readonly Color BeamColor = new Color(1f, 0.98f, 0.9f, 0.32f);
        static readonly Color GlintColor = new Color(1f, 0.86f, 0.35f, 1f);
        static readonly Color FlagColor = new Color(0.86f, 0.16f, 0.14f, 1f);
        static readonly Color CrackDustColor = new Color(0.78f, 0.72f, 0.62f, 0.7f);

        static Color SignalColor(PoiSignal signal) => signal switch
        {
            PoiSignal.Smoke => new Color(1f, 0.62f, 0.25f),
            PoiSignal.Beam => Color.white,
            PoiSignal.Lamps => LampColor,
            PoiSignal.Flame => FlameColor,
            _ => GlintColor,
        };

        // Whether its signal still shows: a chest or cart until it is opened, a shrine until prayed at today.
        bool Signals(Poi poi) => poi.Definition.Signal != PoiSignal.None && _client.CanUse(poi) && !Sealed(poi);

        // A hidden room in the rock whose cracked wall still stands: nothing in it shows (only the crack's dust).
        bool Sealed(Poi poi)
        {
            var room = _client.Rooms?.Nodes[poi.Room];
            if (room == null || !room.Buried || room.Box is not (int left, int bottom, int w, int h)) return false;
            for (int y = bottom; y < bottom + h; y++)
                for (int x = left; x < left + w; x++)
                    if (_client.Map[x, y] == TileType.CrackedWall) return true;
            return false;
        }

        List<Vec2> CampFlags()
        {
            if (_campFlags != null) return _campFlags;
            _campFlags = new List<Vec2>();
            var rooms = _client.Rooms;
            if (rooms == null) return _campFlags;
            foreach (var room in rooms.Nodes.Where(n => n.Role == RoomRole.Elite))
            {
                var mark = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Elite).Select(m => (RoomMark?)m).FirstOrDefault();
                var at = mark is RoomMark leader ? TileMap.Center(leader.X, leader.Y) : room.Center;
                _campFlags.Add(at + new Vec2(1.1f, 0.6f));
            }
            return _campFlags;
        }

        // Each frame, after the props: the signals near enough, the flags and the dust.
        void DrawPois(float deltaTime)
        {
            _farSignals.Clear();
            var self = _client.Position;
            float halfHeight = _camera.orthographicSize, halfWidth = halfHeight * _camera.aspect;
            bool OnScreen(Vec2 at) => Mathf.Abs(at.X - _cameraTarget.x) <= halfWidth + 1 && Mathf.Abs(at.Y - _cameraTarget.y) <= halfHeight + 1;
            // A raid is seen once it is well in (half a tile in from the edges).
            FollowRaids(at => Mathf.Abs(at.X - _cameraTarget.x) <= halfWidth - 0.5f && Mathf.Abs(at.Y - _cameraTarget.y) <= halfHeight - 0.5f, deltaTime);
            _puffClock += deltaTime;
            _glintClock += deltaTime;
            bool puff = _puffClock >= 0.3f, glint = _glintClock >= 0.9f;
            if (puff) _puffClock = 0;
            if (glint) _glintClock = 0;
            int beams = 0;
            foreach (var poi in _client.Pois)
            {
                var signal = poi.Definition.Signal;
                if (!Signals(poi) || Vec2.Distance(self, poi.Center) > poi.Definition.SignalRange) continue;
                if (!OnScreen(poi.Center))
                {
                    _farSignals.Add((poi.Center, SignalColor(signal), false));
                    continue;
                }
                var at = new Vector3(poi.Center.X, poi.Center.Y, 0);
                switch (signal)
                {
                    case PoiSignal.Smoke:
                        if (puff && Feel.Bits) Puff(at + new Vector3(Random.Range(-0.15f, 0.15f), 0.5f, 0), SmokeColor, new Vector3(Random.Range(-0.1f, 0.1f), 0.9f, 0), 3.2f, 3);
                        break;
                    case PoiSignal.Beam:
                        var beam = Beam(beams++);
                        float breath = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f + poi.Index);
                        beam.transform.localPosition = at + new Vector3(0, 2.4f, 0);
                        beam.color = new Color(BeamColor.r, BeamColor.g, BeamColor.b, BeamColor.a * breath);
                        beam.enabled = true;
                        break;
                    case PoiSignal.Glint:
                        if (glint && Feel.Bits && Random.value < 0.7f)
                            Puff(at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.6f), 0), GlintColor, new Vector3(0, 0.25f, 0), 0.6f, 2);
                        break;
                    // 3c (WorldView.Travel.cs): a waystone's flame; a station's lamps are its lights alone.
                    case PoiSignal.Flame:
                        Flicker(at, deltaTime);
                        break;
                }
            }
            for (int i = beams; i < _beams.Count; i++) _beams[i].enabled = false;

            // The elites' camps fly a red flag, and their drum beats while one is near.
            int flags = 0;
            float nearestCamp = float.MaxValue;
            foreach (var at in CampFlags())
            {
                float distance = Vec2.Distance(self, at);
                nearestCamp = Mathf.Min(nearestCamp, distance);
                if (distance > FlagRange) continue;
                if (!OnScreen(at))
                {
                    _farSignals.Add((at, FlagColor, false));
                    continue;
                }
                var (pole, cloth) = Flag(flags++);
                pole.transform.localPosition = new Vector3(at.X, at.Y + 0.9f, 0);
                float wave = Mathf.Sin(Time.time * 5 + at.X) * 0.06f;
                cloth.transform.localPosition = new Vector3(at.X + 0.32f, at.Y + 1.55f + wave, 0);
                cloth.transform.localScale = new Vector3(0.6f * (0.9f + Mathf.Abs(wave)), 0.38f, 1);
                pole.enabled = cloth.enabled = true;
            }
            for (int i = flags; i < _flags.Count; i++) _flags[i].Pole.enabled = _flags[i].Cloth.enabled = false;
            _drumClock += deltaTime;
            if (nearestCamp <= DrumRange && _drumClock >= 0.9f)
            {
                _drumClock = 0;
                Sounds.Play(Sound.Thump, 0.35f * (1 - nearestCamp / DrumRange));
            }

            // Dust from the cracks of cracked walls close by, and the draught through them heard a little further.
            _dustClock += deltaTime;
            _windClock += deltaTime;
            if (_dustClock < 0.35f) return;
            _dustClock = 0;
            int cx = TileMap.Cell(self.X), cy = TileMap.Cell(self.Y), reach = (int)WindRange;
            float nearestCrack = float.MaxValue;
            var map = _client.Map;
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!map.InBounds(x, y) || map[x, y] != TileType.CrackedWall) continue;
                    // Only a crack facing open floor puffs.
                    if (map.IsSolid(x, y - 1) && map.IsSolid(x + 1, y) && map.IsSolid(x - 1, y) && map.IsSolid(x, y + 1)) continue;
                    float distance = Vec2.Distance(self, TileMap.Center(x, y));
                    nearestCrack = Mathf.Min(nearestCrack, distance);
                    if (distance <= DustRange && Feel.Bits && Random.value < 0.35f)
                        Puff(new Vector3(x + Random.value, y + Random.Range(0.1f, 0.5f), 0), CrackDustColor, new Vector3(Random.Range(-0.3f, 0.3f), -0.2f, 0), 1.4f, 2);
                }
            }
            if (nearestCrack <= WindRange && _windClock >= 2.6f)
            {
                _windClock = 0;
                Sounds.Play(Sound.Dash, 0.25f * (1 - nearestCrack / WindRange));
            }
        }

        // A soft square of smoke, glint or dust drifting off, over the dark.
        void Puff(Vector3 at, Color color, Vector3 velocity, float life, int pixels)
        {
            var renderer = Renderer(_root.transform, "Puff", Shapes.Square, color);
            renderer.transform.localPosition = at;
            renderer.transform.localScale = Vector3.one * (pixels / (float)Sprites.PixelsPerUnit);
            renderer.sortingOrder = SignalOrder;
            _bits.Add(new Bit { Renderer = renderer, Velocity = velocity, Color = color, Life = life, Drag = 0.2f, Sway = 0.12f, Phase = Random.value * 6, LateFade = true });
        }

        SpriteRenderer Beam(int index)
        {
            while (_beams.Count <= index)
            {
                var beam = Renderer(_root.transform, "Beam", Shapes.Square, BeamColor);
                beam.transform.localScale = new Vector3(0.45f, 5f, 1);
                beam.sortingOrder = SignalOrder;
                _beams.Add(beam);
            }
            return _beams[index];
        }

        (SpriteRenderer Pole, SpriteRenderer Cloth) Flag(int index)
        {
            while (_flags.Count <= index)
            {
                var pole = Renderer(_root.transform, "FlagPole", Shapes.Square, new Color(0.35f, 0.24f, 0.16f));
                pole.transform.localScale = new Vector3(2f / Sprites.PixelsPerUnit, 1.8f, 1);
                pole.sortingOrder = SignalOrder;
                var cloth = Renderer(_root.transform, "Flag", Shapes.Square, FlagColor);
                cloth.sortingOrder = SignalOrder + 1;
                _flags.Add((pole, cloth));
            }
            return _flags[index];
        }

        // The lights of the things to find: a camp's fire further than a plain fire's, a shrine's pillar, a chest's
        // glint, an elite camp's red glow.
        void AddPoiLights(int left, int bottom, int size)
        {
            float margin = LightMap.MaxRadius;
            bool Near(Vec2 at) => at.X >= left - margin && at.Y >= bottom - margin && at.X <= left + size + margin && at.Y <= bottom + size + margin;
            foreach (var poi in _client.Pois)
            {
                if (!Near(poi.Center)) continue;
                switch (poi.Definition.Signal)
                {
                    case PoiSignal.Smoke:
                        _lights.Add(new LightSource(poi.Center, 9, 0.9f, Rgba.Hex("#FF9A3C"), tints: true));
                        break;
                    case PoiSignal.Beam:
                        if (Signals(poi)) _lights.Add(new LightSource(poi.Center, 6, 0.9f, Rgba.Hex("#FFF6E0"), tints: true));
                        break;
                    case PoiSignal.Glint:
                        if (Signals(poi)) _lights.Add(new LightSource(poi.Center, 2.5f, 0.7f + 0.2f * Mathf.Sin(Time.time * 3 + poi.Index), Rgba.Hex("#FFD86A"), tints: true));
                        break;
                    case PoiSignal.Flame:
                        _lights.Add(new LightSource(poi.Center, 4.5f, 0.75f + 0.15f * Mathf.Sin(Time.time * 7 + poi.Index), Rgba.Hex("#7ACCFF"), tints: true));
                        break;
                }
            }
            AddTravelLights(Near);
            foreach (var at in CampFlags())
                if (Near(at)) _lights.Add(new LightSource(at, 3.5f, 0.8f, Rgba.Hex("#FF5A40"), tints: true));
        }

        // A signal in range but off the screen: a dot on the screen's edge in its direction.
        void DrawFarSignals()
        {
            if (_farSignals.Count == 0 || Event.current.type != EventType.Repaint) return;
            var center = new Vector2(Ui.Width / 2, Ui.Height / 2);
            float inset = 18;
            foreach (var (at, color, raid) in _farSignals)
            {
                var screen = _camera.WorldToScreenPoint(new Vector3(at.X, at.Y, 0));
                var point = Ui.FromScreen(screen);
                var way = point - center;
                if (way.sqrMagnitude < 1) continue;
                float scale = Mathf.Min((Ui.Width / 2 - inset) / Mathf.Max(1e-3f, Mathf.Abs(way.x)), (Ui.Height / 2 - inset) / Mathf.Max(1e-3f, Mathf.Abs(way.y)));
                var edge = center + way * Mathf.Min(1, scale);
                var previous = GUI.color;
                float breath = 0.6f + 0.4f * Mathf.Sin(Time.time * (raid ? 8 : 3));
                if (raid)
                {
                    // A raid's: a red dot half again as big, quick as a heartbeat, in a dark ring so it reads on light
                    // ground too (B5c: a phone held upright sees few tiles across).
                    GUI.color = new Color(0, 0, 0, 0.55f);
                    GUI.DrawTexture(new Rect(edge.x - 11, edge.y - 11, 22, 22), Shapes.Circle.texture);
                    GUI.color = new Color(color.r, color.g, color.b, 0.75f + 0.25f * breath);
                    GUI.DrawTexture(new Rect(edge.x - 9, edge.y - 9, 18, 18), Shapes.Circle.texture);
                }
                else
                {
                    GUI.color = new Color(color.r, color.g, color.b, breath);
                    GUI.DrawTexture(new Rect(edge.x - 6, edge.y - 6, 12, 12), Shapes.Circle.texture);
                }
                GUI.color = previous;
            }
        }

        // What the screen does when a thing is used (SimEventType.PoiOpened, Blessed). True when it was one of those.
        bool ShowPoi(SimEvent e)
        {
            if (e.Type != SimEventType.PoiOpened && e.Type != SimEventType.Blessed) return false;
            if (e.Amount < 0 || e.Amount >= _client.Pois.Count) return true;
            var poi = _client.Pois[e.Amount];
            var at = new Vector3(e.Position.X, e.Position.Y, 0);
            bool mine = e.Source == _client.PlayerId;
            if (e.Type == SimEventType.PoiOpened)
            {
                Hear(poi.Kind == PoiKind.Chest ? Sound.Chest : Sound.Pickup, at, mine ? 1 : 0.6f);
                AddFlash(at + new Vector3(0, 0.4f, 0), 1.2f, GlintColor, FlashKind.Burst);
                Burst(at + new Vector3(0, 0.4f, 0), GlintColor, 1.2f);
                _floating.Add(new Floating { Position = at + new Vector3(0, 1.1f, 0), Text = poi.Definition.Name, Color = GlintColor, Big = mine });
                return true;
            }
            if (poi.Kind == PoiKind.Camp)
            {
                Hear(Sound.Heal, at, mine ? 1 : 0.5f);
                if (mine) ShowBanner($"{poi.Definition.Name}에서 쉬었다: {PoiText.RestedLine(poi.Definition)}");
                return true;
            }
            Hear(Sound.Learn, at, mine ? 1 : 0.5f);
            AddFlash(at + new Vector3(0, 0.8f, 0), 1.6f, Color.white, FlashKind.Burst);
            if (mine) ShowBanner($"{poi.Definition.Name}의 축복: {PoiText.BlessingLine(poi.Definition)}");
            return true;
        }
    }

    partial class WorldView
    {
        static readonly Color TrapColor = new Color(0.95f, 0.35f, 0.25f);
        static readonly Color AmbushDustColor = new Color(0.66f, 0.6f, 0.52f);
        // Within this of a trap's chest the banners speak to the player (they are shut in, or by it).
        const float TrapHeard = 24;

        // What the screen has shown of the trap rooms and the pacing director, for the playtests' state report.
        public int TrapBanners { get; private set; }
        public int AmbushDustShown { get; private set; }
        public int RaidHowlsHeard { get; private set; }
        public int GratesDropped { get; private set; }

        // A trap room's doors, bands and end (SimEventType.Trap), and the director's ambush dust and raid howl
        // (SimEventType.Ambush, Docs/WorldRework.md 완급 감독). True when it was one of those.
        bool ShowPace(SimEvent e)
        {
            if (e.Type != SimEventType.Trap && e.Type != SimEventType.Ambush) return false;
            var at = new Vector3(e.Position.X, e.Position.Y, 0);
            bool near = Vector3.Distance(at, _self.Position) <= TrapHeard;
            if (e.Type == SimEventType.Ambush)
            {
                if (e.Amount == World.AmbushDust)
                {
                    // Dust and grit off the wall, a second before the ambush comes out of it.
                    AmbushDustShown++;
                    for (int i = 0; i < 3; i++) Kick(at + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0.6f, 0), Vector3.down * 0.6f, AmbushDustColor, 3);
                    AddFlash(at, 0.8f, AmbushDustColor, FlashKind.Burst);
                    Hear(Sound.Break, at, 0.8f);
                    _shake = Mathf.Max(_shake, 0.06f);
                    return true;
                }
                // A raid's howl from far off: heard, a word on the side it came from, and a dot on the screen's edge
                // that way until it is on the screen (FollowRaids).
                RaidHowlsHeard++;
                _raidSigns.Add(new RaidSign { At = e.Position, Since = Time.time });
                Hear(Sound.Roar, at, 0.7f);
                var toward = at - _self.Position;
                var side = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.up;
                _floating.Add(new Floating { Position = _self.Position + side * 2.5f + new Vector3(0, 0.6f, 0), Text = "멀리서 울음소리…", Color = TrapColor });
                return true;
            }
            switch (e.Amount)
            {
                case World.TrapSprung:
                    // The grates fall across the ways out: a thud and dust where each came down.
                    Hear(Sound.DoorShut, at, 1);
                    Hear(Sound.Thump, at, 0.8f);
                    _shake = Mathf.Max(_shake, near ? 0.25f : 0);
                    var map = _client.Map;
                    int cx = TileMap.Cell(e.Position.X), cy = TileMap.Cell(e.Position.Y);
                    for (int y = cy - 32; y <= cy + 32; y++)
                        for (int x = cx - 32; x <= cx + 32; x++)
                        {
                            if (map == null || !map.InBounds(x, y) || map[x, y] != TileType.Grate) continue;
                            GratesDropped++;
                            Kick(new Vector3(x + 0.5f, y + 0.9f, 0), Vector3.down * 0.8f, AmbushDustColor, 1);
                        }
                    if (near) Banner("함정이다! 창살이 내려와 방이 닫혔다");
                    break;
                case World.TrapWon:
                    Hear(Sound.Victory, at, 1);
                    AddFlash(at + new Vector3(0, 0.4f, 0), 1.6f, GlintColor, FlashKind.Burst);
                    if (near) Banner("함정을 이겨 냈다! 창살이 올라가고 상자가 한 단계 좋아졌다");
                    break;
                case World.TrapLost:
                    Hear(Sound.Down, at, 0.8f);
                    if (near) Banner("함정 방의 창살이 올라갔다. 상자는 내일 새벽에 다시 열 수 있다");
                    break;
                default:
                    int wave = e.Amount - World.TrapWave;
                    Hear(Sound.Roar, at, 0.9f);
                    AddFlash(at, 3, TrapColor, FlashKind.Ring);
                    if (near) Banner(wave >= World.TrapWaves ? $"마지막 무리가 온다! ({wave}/{World.TrapWaves})" : $"무리가 몰려온다! ({wave}/{World.TrapWaves})");
                    break;
            }
            return true;
        }

        void Banner(string text)
        {
            TrapBanners++;
            ShowBanner(text);
        }

        // ---- A raid pointed to from the screen's edge until it is on the screen (B5c) ----

        // A raid heard: where it is thought to be now, when it howled, and whether its dot was drawn yet.
        sealed class RaidSign
        {
            public Vec2 At;
            public float Since;
            public bool Pointed;
        }

        readonly List<RaidSign> _raidSigns = new List<RaidSign>();
        // How long after its howl a raid may be pointed to (World.RaidRunSeconds and more), how far from where it was
        // last its monsters are looked for, how fast it is thought to come while none of it is in the snapshot (the
        // host sends what is within its view range), and how far off one is still pointed to.
        const float RaidSignSeconds = 12;
        const float RaidFollow = 6;
        const float RaidGuessPace = 4;
        const float RaidSignRange = 40;

        // The raids' dots drawn this frame, the raids pointed to at all, and those that came on the screen while pointed
        // to (or at once), for the playtests.
        public int RaidDots { get; private set; }
        public int RaidDotsShown { get; private set; }
        public int RaidsOnScreen { get; private set; }

        // Each frame: each raid heard follows its monsters (the one nearest the player leads) and is pointed to from the
        // screen's edge until one of it is on the screen. The screen does not know which monsters are the raid's: those
        // near where it was last are taken for it.
        void FollowRaids(System.Func<Vec2, bool> onScreen, float deltaTime)
        {
            RaidDots = 0;
            var self = _client.Position;
            for (int i = _raidSigns.Count - 1; i >= 0; i--)
            {
                var sign = _raidSigns[i];
                if (Time.time - sign.Since > RaidSignSeconds || Vec2.Distance(self, sign.At) > RaidSignRange || _client.InPocket)
                {
                    _raidSigns.RemoveAt(i);
                    continue;
                }
                Vec2? lead = null;
                bool seen = false;
                float nearest = float.MaxValue;
                foreach (var view in _client.Others())
                {
                    if (view.Kind != EntityKind.Monster || Vec2.Distance(view.Position, sign.At) > RaidFollow) continue;
                    if (onScreen(view.Position)) seen = true;
                    float distance = Vec2.Distance(view.Position, self);
                    if (distance >= nearest) continue;
                    nearest = distance;
                    lead = view.Position;
                }
                if (lead is Vec2 at) sign.At = at;
                else if (Vec2.Distance(sign.At, self) > 1)
                    sign.At += (self - sign.At).Normalized * Mathf.Min(RaidGuessPace * deltaTime, Vec2.Distance(sign.At, self));
                if (seen || onScreen(sign.At))
                {
                    RaidsOnScreen++;
                    _raidSigns.RemoveAt(i);
                    continue;
                }
                if (!sign.Pointed)
                {
                    sign.Pointed = true;
                    RaidDotsShown++;
                }
                RaidDots++;
                _farSignals.Add((sign.At, TrapColor, true));
            }
        }
    }

    // The words the screen uses for the things to find.
    static class PoiText
    {
        public static string BlessingLine(PoiDefinition shrine)
        {
            var bless = shrine.Reward(PoiRewardKind.Bless);
            string what = string.Join(" · ", shrine.Buff.Select(m => StatText.Describe(m)));
            return bless == null ? what : $"{bless.Min / 60}분 동안 {what}";
        }

        public static string RestedLine(PoiDefinition camp)
        {
            var rested = camp.Reward(PoiRewardKind.Rested);
            return rested == null ? "쓰러지면 여기서 일어난다" : $"{rested.Min / 60}분 동안 경험치 +{World.RestedExperiencePercent}%, 쓰러지면 여기서 일어난다";
        }

        // What the button over it says.
        public static string Verb(Poi poi) => poi.Kind switch
        {
            PoiKind.Shrine => "기도하기",
            PoiKind.Cart => "뒤지기",
            PoiKind.Station => "타기",
            _ => "열기",
        };
    }
}
