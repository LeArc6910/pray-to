using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // The minecarts, waystones and map scraps as the screen shows them (2026-11-22, Docs/WorldRework.md 광차·길잡이 돌·
    // 떠돌이 상인, 묶음 3c). Each station has a cart standing on its rails; a line not yet mended shows a torn-up rail at
    // its broken end; whoever rides sits in a cart that runs along the rails; lamps light the rails out from a station
    // and a waystone burns with a blue flame. A map scrap read rings a thing on the map (MapView.ReadScrap).
    partial class WorldView
    {
        readonly Dictionary<int, SpriteRenderer> _stationCarts = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _brokenRails = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _rideCarts = new Dictionary<int, SpriteRenderer>();
        readonly HashSet<int> _travelSeen = new HashSet<int>();
        readonly List<int> _travelGone = new List<int>();
        float _flameClock, _rattleClock;

        // How far round the camera the stations' carts are drawn.
        const float StationRange = 30;
        // Lamps along the rails from each station: one every LampEvery tiles, LampsOut of them.
        const int LampEvery = 7;
        const int LampsOut = 5;

        static readonly Color FlameColor = new Color(0.55f, 0.85f, 1f, 0.9f);
        static readonly Color LampColor = new Color(1f, 0.78f, 0.42f);

        // Reads a map scrap on this player's map (MapView.ReadScrap), set by GameRoot: the thing it rings, or null.
        public Func<Poi> MapScrap { get; set; }

        static Sprite CartSprite => Sprites.Standing("travel/cart", PropArt.RideCart);
        static Sprite BrokenRailSprite => Sprites.Standing("travel/broken_rail", PropArt.BrokenRail);

        // Each frame, after the things to find: the stations' carts, the broken ends and the riders' carts.
        void DrawTravel(float deltaTime)
        {
            var lines = _client.Lines;
            var target = new Vec2(_cameraTarget.x, _cameraTarget.y);

            // The carts at the stations near by, and the torn-up rail at the broken end of a line not mended.
            _travelSeen.Clear();
            foreach (var line in lines)
            {
                foreach (int index in new[] { line.StationA, line.StationB })
                {
                    if (index < 0 || index >= _client.Pois.Count) continue;
                    var station = _client.Pois[index];
                    if (Vec2.Distance(station.Center, target) > StationRange) continue;
                    _travelSeen.Add(index);
                    if (!_stationCarts.TryGetValue(index, out var cart))
                    {
                        cart = Renderer(_root.transform, "StationCart", CartSprite, Color.white);
                        _stationCarts[index] = cart;
                    }
                    cart.transform.localPosition = new Vector3(station.X + 0.5f, station.Y, 0);
                    cart.sortingOrder = 10000 - Mathf.RoundToInt(station.Y * 10);
                    bool broken = !_client.RailFixed(line.Index) && index == line.Broken;
                    _brokenRails.TryGetValue(index, out var rail);
                    if (broken && rail == null)
                    {
                        // Two tiles in from the end, where the rails run on toward the other station.
                        var (rx, ry) = line.Route[line.Route.Count - 3 < 0 ? 0 : line.Route.Count - 3];
                        if (index == line.StationA) (rx, ry) = line.Route[Math.Min(2, line.Route.Count - 1)];
                        rail = Renderer(_root.transform, "BrokenRail", BrokenRailSprite, Color.white);
                        rail.transform.localPosition = new Vector3(rx + 0.5f, ry, 0);
                        rail.sortingOrder = 10000 - Mathf.RoundToInt(ry * 10);
                        _brokenRails[index] = rail;
                    }
                    else if (!broken && rail != null)
                    {
                        Object.Destroy(rail.gameObject);
                        _brokenRails.Remove(index);
                    }
                }
            }
            Forget(_stationCarts, _travelSeen);
            Forget(_brokenRails, _travelSeen);

            // Whoever rides sits in a cart, the front of it over their legs; a rattle while one's own runs.
            _travelSeen.Clear();
            foreach (var view in RiderBodies())
            {
                var (id, body, way) = view;
                _travelSeen.Add(id);
                if (!_rideCarts.TryGetValue(id, out var cart))
                {
                    cart = Renderer(_root.transform, "RideCart", CartSprite, Color.white);
                    _rideCarts[id] = cart;
                }
                var at = body.Position;
                cart.transform.localPosition = new Vector3(at.x, at.y - 0.55f, 0);
                cart.flipX = way.X < 0;
                cart.sortingOrder = 10000 - Mathf.RoundToInt((at.y - 0.55f) * 10) + 2;
            }
            Forget(_rideCarts, _travelSeen);
            _rattleClock += deltaTime;
            if (_client.Riding && _rattleClock >= 0.22f)
            {
                _rattleClock = 0;
                Sounds.Play(Sound.Step, 0.35f);
            }
        }

        IEnumerable<(int Id, Body Body, Vec2 Way)> RiderBodies()
        {
            foreach (var player in new[] { _client.PlayerId }.Concat(_others.Keys))
            {
                if (_client.RideOf(player) is not var (_, _, way)) continue;
                var body = Find(player);
                if (body != null) yield return (player, body, way);
            }
        }

        static void Forget(Dictionary<int, SpriteRenderer> renderers, HashSet<int> seen)
        {
            var gone = renderers.Keys.Where(k => !seen.Contains(k)).ToList();
            foreach (int key in gone)
            {
                Object.Destroy(renderers[key].gameObject);
                renderers.Remove(key);
            }
        }

        // A waystone's flame flickers up (PoiSignal.Flame), in DrawPois.
        void Flicker(Vector3 at, float deltaTime)
        {
            _flameClock += deltaTime;
            if (_flameClock < 0.18f || !Feel.Bits) return;
            _flameClock = 0;
            Puff(at + new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), 1.55f, 0), FlameColor, new Vector3(UnityEngine.Random.Range(-0.05f, 0.05f), 0.8f, 0), 0.7f, 2);
        }

        // The lights of the rails and stones: lamps along the rails out from each station, a waystone's blue flame.
        void AddTravelLights(Func<Vec2, bool> near)
        {
            foreach (var line in _client.Lines)
            {
                for (int i = 0; i < LampsOut; i++)
                {
                    foreach (int step in new[] { i * LampEvery, line.Route.Count - 1 - i * LampEvery })
                    {
                        if (step < 0 || step >= line.Route.Count) continue;
                        var at = TileMap.Center(line.Route[step].X, line.Route[step].Y);
                        if (near(at)) _lights.Add(new LightSource(at, i == 0 ? 4 : 2.5f, 0.8f, Rgba.Hex("#FFC86A"), tints: true));
                    }
                }
            }
        }

        // What the screen does for a minecart, a waystone or a map scrap (SimEventType.Rail, Waystone, MapRead). True
        // when it was one of those.
        bool ShowTravel(SimEvent e)
        {
            bool mine = e.Source == _client.PlayerId;
            var at = new Vector3(e.Position.X, e.Position.Y, 0);
            switch (e.Type)
            {
                case SimEventType.Rail:
                    if (e.Target == World.RailMended)
                    {
                        Hear(Sound.Hammer, at, mine ? 1 : 0.6f);
                        Burst(at + new Vector3(0, 0.4f, 0), LampColor, 1);
                        if (mine) ShowBanner("선로를 고쳤다: 이제 이 광차는 양쪽 역에서 탄다");
                    }
                    else if (e.Target == World.RailSet)
                    {
                        Hear(Sound.Dash, at, mine ? 1 : 0.5f);
                        if (mine) ShowBanner("광차를 탔다: 가는 동안 맞지 않는다");
                    }
                    else Hear(Sound.Thump, at, mine ? 0.8f : 0.4f);
                    return true;
                case SimEventType.Waystone:
                    Hear(Sound.Learn, at, mine ? 1 : 0.5f);
                    AddFlash(at + new Vector3(0, 0.8f, 0), 1.4f, FlameColor, FlashKind.Burst);
                    if (mine && e.Target == World.StoneOpened)
                        ShowBanner("길잡이 돌을 만졌다: 웨이포인트와 다른 길잡이 돌에서 여기로 올 수 있다");
                    return true;
                case SimEventType.MapRead:
                    if (!mine) return true;
                    Sounds.Play(Sound.Pickup);
                    var found = MapScrap?.Invoke();
                    if (found == null) ShowBanner("지도 조각: 더 표시할 것이 없다");
                    else
                    {
                        var way = found.Center - _client.Position;
                        ShowBanner($"지도 조각: {found.Definition.Name}, {Compass(way.X, way.Y)} {Mathf.RoundToInt(way.Length)}칸 (지도에 표시)");
                    }
                    return true;
            }
            return false;
        }
    }

    // The words the screen uses for the minecarts and waystones.
    static class TravelText
    {
        // What the button over a station says, and whether it can be pressed: ride, mend (with what), or why not.
        public static (string Text, bool Usable) StationButton(WorldClient client, Poi station)
        {
            var line = client.LineOf(station);
            if (line == null) return (station.Definition.Name, false);
            if (client.RailFixed(line.Index)) return ($"{station.Definition.Name}: 광차 타기", true);
            if (station.Index != line.Broken) return ($"{station.Definition.Name}: 선로가 끊겼다(반대쪽 역에서 고친다)", false);
            var repair = station.Definition.Reward(PoiRewardKind.Repair);
            if (repair == null) return ($"{station.Definition.Name}: 선로 고치기", true);
            var material = client.Loot?.Materials.FirstOrDefault(m => m.Id == repair.Material);
            int have = material == null ? 0 : client.Bag.CountOf(material);
            string name = material?.Name ?? repair.Material;
            return ($"선로 고치기 ({name} {have}/{repair.Min})", have >= repair.Min);
        }
    }
}
