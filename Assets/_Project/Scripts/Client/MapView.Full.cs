using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The full map (M, the minimap pressed; Docs/UiRework.md 4-1 기록, U5): the records' parchment window over the darkened
    // world, the records' tabs ([일지][지도]) under its head, and under them
    //  - the map on a sheet of parchment, the unexplored left as the bare sheet (its fog), dragged with the mouse or a finger,
    //    zoomed by the wheel (round the pointer), two fingers or [+]/[-]; [◇] back to where the player is. It opens on the
    //    player (or on a spot the journal asked to see, MapView.ShowAt);
    //  - marks drawn in code instead of dots (MapIcons): a house for a place, a crystal for a waypoint, a standing stone, a
    //    cart on its rail, an arch for a dungeon's gate, a chest, a shrine's flame, a tent, a skull for an elite camp, two
    //    swords for an arena, a cut stone for a vein, a question mark in a rumour's faint ring, a star where the story
    //    leads, a flag where the player is guided to, and arrows for the player and the others, turned as they face;
    //  - the legend as toggles (장소, 웨이포인트, 광차, 던전 문, 볼거리, 보스, 소문, 광맥; in a dungeon its own): beside the
    //    map on a wide screen, under it on a phone held upright;
    //  - a mark pressed (or under the mouse on a PC) shows its card: its name, what it is, which way and how far, and
    //    [여기로 길 안내] (the goal list follows it, ScreenStack.Tracked "pin") or, by an open waypoint or waystone, [이동]
    //    to another open one, as the waypoint list does (the same rule: only from one).
    // In a dungeon it shows the dungeon alone, as before. What is drawn is this device's view: nothing goes to the host but
    // a [이동] (the waypoint list's own command).
    sealed partial class MapView
    {
        // What a mark is, for the legend's toggles. The player, the others, where the story leads and the guided spot are
        // always shown.
        internal enum MarkKind
        {
            Place,
            Waypoint,
            Rail,
            Gate,
            Poi,
            Boss,
            Rumor,
            Ore,
            Hunt,
            Zone,
            Door,
            Pack,
            Master,
            Things,
            Shortcut,
            Always,
        }

        // The legend's toggles: in the world, and in a dungeon.
        static readonly (MarkKind Kind, string Name, string[] Icon, Color Color)[] WorldLegend =
        {
            (MarkKind.Place, "장소", MapIcons.Place, new Color(0.92f, 0.88f, 0.78f)),
            (MarkKind.Waypoint, "웨이포인트", MapIcons.Waypoint, new Color(0.5f, 0.78f, 1f)),
            (MarkKind.Rail, "광차", MapIcons.Station, new Color(0.72f, 0.78f, 0.88f)),
            (MarkKind.Gate, "던전 문", MapIcons.Gate, new Color(0.72f, 0.55f, 1f)),
            (MarkKind.Poi, "볼거리", MapIcons.Chest, new Color(1f, 0.84f, 0.3f)),
            (MarkKind.Boss, "보스", MapIcons.Arena, new Color(1f, 0.35f, 0.3f)),
            (MarkKind.Hunt, "사냥터", MapIcons.Hunt, HuntMark),
            (MarkKind.Zone, "구역", MapIcons.Zone, new Color(0.95f, 0.8f, 0.45f)),
            (MarkKind.Rumor, "소문", MapIcons.Rumor, new Color(1f, 0.84f, 0.3f)),
            (MarkKind.Ore, "광맥", MapIcons.Ore, new Color(0.85f, 0.55f, 0.3f)),
        };

        static readonly (MarkKind Kind, string Name, string[] Icon, Color Color)[] DungeonLegend =
        {
            (MarkKind.Door, "나가는 문", MapIcons.Gate, new Color(0.72f, 0.55f, 1f)),
            (MarkKind.Pack, "남은 무리", MapIcons.Pack, new Color(0.95f, 0.3f, 0.3f)),
            (MarkKind.Master, "주인", MapIcons.Skull, new Color(1f, 0.3f, 0.25f)),
            (MarkKind.Things, "상자·모닥불", MapIcons.Chest, new Color(1f, 0.85f, 0.35f)),
            (MarkKind.Shortcut, "지름길 돌", MapIcons.Stone, new Color(0.43f, 0.75f, 1f)),
        };

        static readonly Color HuntMark = new Color(1f, 0.55f, 0.25f);

        static (MarkKind Kind, string Name, string[] Icon, Color Color)[] LegendOf(bool dungeon) => dungeon ? DungeonLegend : WorldLegend;

        readonly HashSet<MarkKind> _hidden = new HashSet<MarkKind>();

        // A mark on the full map: where, what for the legend (and a second kind it also shows under: a place with a
        // waypoint), its picture, colour and size (1 the icon's own), the way it points (degrees clockwise from north, for
        // the arrows), its name and what it says on its card, and what [이동] sends to go there (null when it cannot).
        sealed class Mark
        {
            public Vec2 At;
            public MarkKind Kind;
            public MarkKind? Also;
            public string[] Icon;
            public Color Color;
            public float Turn = float.NaN;
            public string Name = "";
            public string Info = "";
            public bool Exact = true;
            public bool Label;
            public byte[] Travel;
            public string TravelWhy = "";
        }

        readonly List<Mark> _marks = new List<Mark>();

        // The view: the tile at its middle and how many points a tile takes.
        float _mapZoom = 3;
        Vector2 _mapCenter;
        int _fullFrame = -10;
        bool _mapFresh = true;
        // Where the journal asked the map to open (ShowAt), how many tiles across, what it is, and when.
        Vec2? _focus;
        float _focusAcross;
        string _focusName = "";
        float _focusAt = -10;
        // The mark picked (its card shows), by its name and spot.
        string _pickedName;
        Vec2 _pickedAt;
        // A drag of the map (mouse or finger) and a pinch of two fingers.
        bool _mapArmed, _mapDragging, _mapPinching;
        Vector2 _mapFrom, _mapCenterFrom;
        float _pinchFrom, _pinchZoom;
        Vector2 _pinchMid, _pinchCenter;
        Rect _view;

        // Sends a command to the host ([이동], GameRoot hands it over).
        public Action<byte[]> Send { get; set; }

        // For the playtests (sweep.js "records"): the zoom, the middle tile, where the map was drawn, the legend's hidden
        // names, the mark whose card shows, the spot the journal asked for.
        internal float Zoom => _mapZoom;
        internal Vector2 Middle => _mapCenter;
        internal Rect ViewShown => _view;
        internal string HiddenNames => string.Join(",", LegendOf(_client.DungeonHere != null).Where(l => _hidden.Contains(l.Kind)).Select(l => l.Name));
        internal string Picked => _pickedName ?? "";
        internal string FocusName => _focus.HasValue ? _focusName : "";

        // Opens the map on a spot (the journal's [지도에서 보기]): `across` tiles across the view, the spot ringed a moment.
        public void ShowAt(Vec2 at, float across, string name)
        {
            _focus = at;
            _focusAcross = Mathf.Max(12, across);
            _focusName = name ?? "";
            _focusAt = Time.unscaledTime;
            _mapFresh = true;
            _pickedName = null;
            FullOpen = true;
        }

        // ---- Where its parts go ----

        internal readonly struct MapLayout
        {
            public MapLayout(Rect view, Rect legend, Rect zoom, bool side, int columns, float chip)
            {
                View = view;
                Legend = legend;
                Zoom = zoom;
                Side = side;
                Columns = columns;
                Chip = chip;
            }

            // The map, the legend's toggles (beside it or under it), the zoom buttons in the map's top right corner, and
            // how the toggles are laid: columns and each one's height.
            public Rect View { get; }
            public Rect Legend { get; }
            public Rect Zoom { get; }
            public bool Side { get; }
            public int Columns { get; }
            public float Chip { get; }
        }

        static float Finger => Controls.UsingTouch ? 40 : 28;

        // The legend at the right where the map keeps 360 points beside it; under the map otherwise (a phone held upright),
        // as many toggles a row as fit at 84 points each.
        internal static MapLayout LayMap(Rect body, int toggles)
        {
            float chip = Controls.UsingTouch ? 40 : 30;
            float button = Finger;
            float zoomWidth = 3 * button + 8;
            if (body.width >= 560)
            {
                float width = Mathf.Clamp(Mathf.Round(body.width * 0.22f), 180, 220);
                int columns = 2;
                int rows = (toggles + columns - 1) / columns;
                var legend = new Rect(body.xMax - width, body.y, width, rows * (chip + 4) + 26);
                var view = Rect.MinMaxRect(body.x, body.y, legend.x - 8, body.yMax);
                return new MapLayout(view, legend, new Rect(view.xMax - 8 - zoomWidth, view.y + 8, zoomWidth, button), true, columns, chip);
            }
            int perRow = Mathf.Clamp(Mathf.FloorToInt((body.width + 4) / 88), 1, toggles);
            int lines = (toggles + perRow - 1) / perRow;
            float height = lines * (chip + 4);
            var under = new Rect(body.x, body.yMax - height, body.width, height);
            var map = Rect.MinMaxRect(body.x, body.y, body.xMax, under.y - 6);
            return new MapLayout(map, under, new Rect(map.xMax - 8 - zoomWidth, map.y + 8, zoomWidth, button), false, perRow, chip);
        }

        // ---- The window ----

        public void DrawFull()
        {
            var screen = Ui.FullScreen;
            Controls.Block(screen);
            UiSkin.FullBackdrop(screen);
            var dungeon = _client.DungeonHere;
            var panel = ScreenStack.RecordsArea();
            if (Time.frameCount - _fullFrame > 2) _mapFresh = true;
            _fullFrame = Time.frameCount;
            UiMotion.BeginWindow(ScreenStack.MotionKey(GameScreen.Map), panel, UiKind.Journal);
            string title = (dungeon != null ? dungeon.Name : "지도") + ScreenStack.KeyHint(GameScreen.Map);
            var inside = UiSkin.Window(panel, UiKind.Journal, title, UiIcons.Map, out var band);
            bool closed = UiSkin.CloseButton(band, "지도 닫기");
            if (Screens != null) Screens.Tabs(new Rect(inside.x, inside.y, inside.width, TabsHeight), GameScreen.Map);
            var body = FullBody(panel);
            var legend = LegendOf(dungeon != null);
            var lay = LayMap(body, legend.Length);
            _view = lay.View;
            BuildMarks(dungeon);
            var picked = PickedMark();
            var card = picked != null ? CardRect(lay.View, picked) : default;
            var covers = new List<Rect> { lay.Zoom };
            if (picked != null) covers.Add(card);
            var shown = Shown();
            if (_mapFresh) OpenView(lay.View, shown);
            MapInput(lay.View, shown, covers);
            DrawSheet(lay.View, shown);
            DrawMarks(lay.View, covers);
            DrawMapZoom(lay.View, lay.Zoom, shown);
            if (picked != null) DrawCard(card, picked);
            DrawLegend(lay, legend);
            UiMotion.EndWindow();
            if (closed)
            {
                FullOpen = false;
                return;
            }
            // Outside the window (the sides of a wide screen), a press closes it.
            var e = Event.current;
            if (e.type == EventType.MouseDown && !panel.Contains(e.mousePosition))
            {
                FullOpen = false;
                e.Use();
            }
        }

        static float TabsHeight => Ui.Short ? 34 : ScreenStack.TabHeight;

        // The area under the window's head and the records' tabs, for the map and its legend.
        internal static Rect FullBody(Rect panel)
        {
            var inside = UiSkin.InsideOf(panel);
            return Rect.MinMaxRect(inside.x, inside.y + TabsHeight + (Ui.Short ? 4 : 8), inside.xMax, inside.yMax);
        }

        // How many toggles the legend has (in the world or in a dungeon), for the layout tests.
        internal static int LegendCount(bool dungeon) => LegendOf(dungeon).Length;

        // ---- Zoom and pan ----

        float FitZoom(Rect view, Rect shown) => Mathf.Min(view.width / shown.width, view.height / shown.height);

        float LeastZoom(Rect view, Rect shown) => FitZoom(view, shown) * 0.9f;

        const float MostZoom = 14;

        // The view it opens with: on the spot the journal asked for, else on the player about 180 tiles across (the whole
        // dungeon in one); never smaller than the whole map.
        void OpenView(Rect view, Rect shown)
        {
            _mapFresh = false;
            _mapArmed = _mapDragging = _mapPinching = false;
            bool focus = _focus.HasValue && Time.unscaledTime - _focusAt < 5;
            if (focus)
            {
                _mapCenter = new Vector2(_focus.Value.X, _focus.Value.Y);
                _mapZoom = Mathf.Clamp(Mathf.Min(view.width, view.height) / _focusAcross, LeastZoom(view, shown), MostZoom);
            }
            else
            {
                _focus = null;
                var player = _client.Position;
                _mapCenter = new Vector2(player.X, player.Y);
                float fit = FitZoom(view, shown);
                _mapZoom = _client.DungeonHere != null ? Mathf.Clamp(fit, fit, MostZoom) : Mathf.Clamp(Mathf.Max(fit, Mathf.Min(view.width, view.height) / 180), LeastZoom(view, shown), MostZoom);
            }
            ClampView(view, shown);
        }

        // The middle kept so the map never leaves the view: within the shown part.
        void ClampView(Rect view, Rect shown)
        {
            float halfW = view.width / 2 / _mapZoom, halfH = view.height / 2 / _mapZoom;
            _mapCenter.x = shown.width <= halfW * 2 ? shown.center.x : Mathf.Clamp(_mapCenter.x, shown.x + halfW * 0.4f, shown.xMax - halfW * 0.4f);
            _mapCenter.y = shown.height <= halfH * 2 ? shown.center.y : Mathf.Clamp(_mapCenter.y, shown.y + halfH * 0.4f, shown.yMax - halfH * 0.4f);
        }

        // A tile spot to a point on the screen, and back.
        Vector2 ToScreen(Rect view, Vec2 at) => new Vector2(view.center.x + (at.X - _mapCenter.x) * _mapZoom, view.center.y - (at.Y - _mapCenter.y) * _mapZoom);

        Vector2 ToTiles(Rect view, Vector2 point) => new Vector2(_mapCenter.x + (point.x - view.center.x) / _mapZoom, _mapCenter.y - (point.y - view.center.y) / _mapZoom);

        void ZoomAt(Rect view, Rect shown, Vector2 point, float zoom)
        {
            var at = ToTiles(view, point);
            _mapZoom = Mathf.Clamp(zoom, LeastZoom(view, shown), MostZoom);
            // The tile under the point stays under it.
            _mapCenter = new Vector2(at.x - (point.x - view.center.x) / _mapZoom, at.y + (point.y - view.center.y) / _mapZoom);
            ClampView(view, shown);
        }

        void MapInput(Rect view, Rect shown, List<Rect> covers)
        {
            var e = Event.current;
            bool Covered(Vector2 point) => covers.Any(c => c.Contains(point));
            if (e.type == EventType.Layout) Pinch(view, shown);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition) && !Covered(e.mousePosition))
            {
                float notches = Mathf.Clamp(e.delta.y, -3, 3);
                ZoomAt(view, shown, e.mousePosition, _mapZoom * Mathf.Pow(1.2f, -Mathf.Sign(notches) * Mathf.Max(1, Mathf.Abs(notches) / 3 * 2)));
                e.Use();
            }
            switch (e.type)
            {
                case EventType.MouseDown:
                    _mapArmed = view.Contains(e.mousePosition) && !Covered(e.mousePosition) && !_mapPinching;
                    _mapDragging = false;
                    _mapFrom = e.mousePosition;
                    _mapCenterFrom = _mapCenter;
                    break;
                case EventType.MouseDrag:
                    if (!_mapArmed || _mapPinching || Controls.FingerCount >= 2) break;
                    if (!_mapDragging && (e.mousePosition - _mapFrom).magnitude > 6)
                    {
                        _mapDragging = true;
                        GUIUtility.hotControl = 0;
                    }
                    if (_mapDragging)
                    {
                        var moved = e.mousePosition - _mapFrom;
                        _mapCenter = _mapCenterFrom + new Vector2(-moved.x, moved.y) / _mapZoom;
                        ClampView(view, shown);
                        e.Use();
                    }
                    break;
            }
            // A press of the map that was no drag: the nearest mark under it is picked (its card shows); none lets it go.
            if (e.rawType == EventType.MouseUp)
            {
                if (e.type == EventType.MouseUp && _mapArmed && !_mapDragging && view.Contains(e.mousePosition) && !Covered(e.mousePosition))
                {
                    var hit = MarkAt(view, e.mousePosition, Controls.UsingTouch ? 26 : 14);
                    _pickedName = hit?.Name;
                    if (hit != null)
                    {
                        _pickedAt = hit.At;
                        UiMotion.Confirm();
                    }
                    e.Use();
                }
                else if (_mapDragging && e.type == EventType.MouseUp) e.Use();
                _mapArmed = _mapDragging = false;
            }
        }

        // Two fingers on the map: it zooms by how far apart they are and its spot under their middle stays there.
        void Pinch(Rect view, Rect shown)
        {
            if (Controls.FingerCount < 2)
            {
                _mapPinching = false;
                return;
            }
            var a = Controls.FingerAt(0);
            var b = Controls.FingerAt(1);
            float apart = Mathf.Max(1, Vector2.Distance(a, b));
            var middle = (a + b) / 2;
            if (!_mapPinching)
            {
                if (!view.Contains(middle)) return;
                _mapPinching = true;
                _pinchFrom = apart;
                _pinchZoom = _mapZoom;
                _pinchMid = middle;
                _pinchCenter = _mapCenter;
                _mapArmed = _mapDragging = false;
                GUIUtility.hotControl = 0;
                return;
            }
            // The tile that was under the fingers' middle when they came down stays under their middle now.
            float atX = _pinchCenter.x + (_pinchMid.x - view.center.x) / _pinchZoom;
            float atY = _pinchCenter.y - (_pinchMid.y - view.center.y) / _pinchZoom;
            _mapZoom = Mathf.Clamp(_pinchZoom * apart / _pinchFrom, LeastZoom(view, shown), MostZoom);
            _mapCenter = new Vector2(atX - (middle.x - view.center.x) / _mapZoom, atY + (middle.y - view.center.y) / _mapZoom);
            ClampView(view, shown);
        }

        // [-] [+] [◇]: zoom out, in, and back to the player.
        void DrawMapZoom(Rect view, Rect strip, Rect shown)
        {
            float button = strip.height;
            Ui.Row("지도");
            if (Ui.Button(new Rect(strip.x, strip.y, button, button), "-", "축소")) ZoomAt(view, shown, view.center, _mapZoom / 1.4f);
            if (Ui.Button(new Rect(strip.x + button + 4, strip.y, button, button), "+", "확대")) ZoomAt(view, shown, view.center, _mapZoom * 1.4f);
            if (Ui.Button(new Rect(strip.x + 2 * (button + 4), strip.y, button, button), "◇", "가운데로"))
            {
                _focus = null;
                OpenView(view, shown);
            }
            Ui.Row("");
        }

        // ---- The sheet and the tiles ----

        static readonly Color Fog = new Color(0.68f, 0.63f, 0.54f);

        // The parchment under everything (the unexplored left bare, its fog), the explored tiles over it, the rumours' faint
        // rings and the spot the journal asked for, all clipped to the view; the burnt edge round it; the compass.
        void DrawSheet(Rect view, Rect shown)
        {
            if (Event.current.type != EventType.Repaint) return;
            // The margin beyond the map darkest, the map's own sheet a shade lighter and hatched where unexplored.
            UiSkin.Parchment(view, new Color(0.42f, 0.37f, 0.31f), ToScreen(view, new Vec2(shown.x, shown.yMax)));
            GUI.BeginClip(view);
            Vector2 Local(Vec2 at) => ToScreen(view, at) - view.position;
            var corner = Local(new Vec2(shown.x, shown.yMax));
            var sheet = new Rect(corner.x, corner.y, shown.width * _mapZoom, shown.height * _mapZoom);
            var previous = GUI.color;
            var fogged = Rect.MinMaxRect(Mathf.Max(0, sheet.x), Mathf.Max(0, sheet.y), Mathf.Min(view.width, sheet.xMax), Mathf.Min(view.height, sheet.yMax));
            GUI.EndClip();
            var onScreen = new Rect(fogged.x + view.x, fogged.y + view.y, fogged.width, fogged.height);
            var pinned = new Vector2(sheet.x + view.x, sheet.y + view.y);
            UiSkin.Parchment(onScreen, Fog, pinned);
            UiSkin.Hatch(onScreen, pinned);
            GUI.BeginClip(view);
            var uv = new Rect(shown.x / _map.Width, shown.y / _map.Rows, shown.width / _map.Width, shown.height / _map.Rows);
            GUI.DrawTextureWithTexCoords(sheet, Sheet(), uv);
            // The rumours' faint rings (묶음 4c) under the marks.
            if (_client.DungeonHere == null && !_hidden.Contains(MarkKind.Rumor)) DrawRings(view);
            // The zones' edges and the levels each is for (Docs/ZoneLevels.md).
            if (_client.DungeonHere == null && !_hidden.Contains(MarkKind.Zone)) DrawZones(view);
            // The spot the journal asked to see: a ring that pulses for a few seconds.
            if (_focus is Vec2 focus && Time.unscaledTime - _focusAt < 4)
            {
                float t = Time.unscaledTime - _focusAt;
                float pulse = UiMotion.Reduced ? 1 : 0.5f + 0.5f * Mathf.Sin(t * 6);
                float size = Mathf.Max(28, _focusAcross * 0.35f * _mapZoom) * (UiMotion.Reduced ? 1 : 1 + 0.15f * pulse);
                var p = Local(focus);
                GUI.color = new Color(0.72f, 0.16f, 0.1f, (0.5f + 0.5f * pulse) * previous.a);
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Shapes.Ring.texture);
                GUI.color = previous;
            }
            GUI.EndClip();
            UiSkin.PageEdge(new Rect(view.x - 3, view.y - 3, view.width + 6, view.height + 6));
            // The compass in the bottom left corner.
            _compass ??= RecordArt.Compass();
            var size2 = UiSkin.SizeOf(_compass);
            UiSkin.Image(new Vector2(view.x + 10, view.yMax - size2.y - 10), _compass);
        }

        static PixelImage _compass;

        // Each rumour heard and not yet seen, each arena not found, and where the story leads while only its way is known:
        // a wide faint disc and its rim, never centred on the thing (Rumors.Circle).
        void DrawRings(Rect view)
        {
            foreach (var mark in _marks)
            {
                if (mark.Kind != MarkKind.Rumor || mark.Exact) continue;
                float size = 2 * RingRadius(mark) * _mapZoom;
                var p = ToScreen(view, mark.At) - view.position;
                var previous = GUI.color;
                GUI.color = new Color(mark.Color.r, mark.Color.g, mark.Color.b, 0.16f * previous.a);
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Shapes.Circle.texture);
                GUI.color = new Color(mark.Color.r * 0.6f, mark.Color.g * 0.5f, mark.Color.b * 0.4f, 0.7f * previous.a);
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Rim.texture);
                GUI.color = previous;
            }
        }

        // The zones round the spawn (World.ZoneLevelAt): a faint rim at each edge, and on the band between two edges, on the
        // player's side of the spawn, "n구역 · Lv a~b" for the biome there, orange or red when the player is short of it.
        void DrawZones(Rect view)
        {
            var spawn = _client.Spawn;
            float ring = new WorldConfig().ZoneRing * WorldGenerator.ScaleOf(_map.Width, _map.Rows);
            if (ring <= 0) return;
            float reach = Mathf.Sqrt(_map.Width * _map.Width + _map.Rows * _map.Rows);
            var toward = _client.Position - spawn;
            var way = toward.LengthSquared > 1 ? toward.Normalized : new Vec2(0, 1);
            var previous = GUI.color;
            _nameStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft };
            var spawnAt = ToScreen(view, spawn) - view.position;
            for (int k = 1; k * ring < reach; k++)
            {
                float size = 2 * k * ring * _mapZoom;
                GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.45f * previous.a);
                GUI.DrawTexture(new Rect(spawnAt.x - size / 2, spawnAt.y - size / 2, size, size), Rim.texture);
            }
            for (int k = 0; (k + 0.5f) * ring < reach; k++)
            {
                var at = spawn + way * ((k + 0.5f) * ring);
                int cx = TileMap.Cell(at.X), cy = TileMap.Cell(at.Y);
                if (!_map.InBounds(cx, cy)) continue;
                int zone = Zones.ZoneAt(spawn, at, WorldGenerator.ScaleOf(_map.Width, _map.Rows));
                var range = Zones.LevelRange(_client.Biomes[cx, cy], zone);
                string text = $"{zone}구역 · {Zones.Text(range)}";
                var p = ToScreen(view, at) - view.position;
                var size = _nameStyle.CalcSize(new GUIContent(text));
                var rect = new Rect(p.x - size.x / 2, p.y - size.y / 2, size.x, size.y);
                GUI.color = previous;
                _nameStyle.normal.textColor = new Color(0.1f, 0.06f, 0.03f, 0.9f);
                foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) }) GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, _nameStyle);
                _nameStyle.normal.textColor = GameRoot.DangerColor(Zones.Danger(_client.Level, range));
                GUI.Label(rect, text, _nameStyle);
            }
            GUI.color = previous;
        }

        // A rumour's ring is RumorRadius wide, PathRadius once the way is known (kept in the mark's info).
        readonly Dictionary<Mark, float> _ringRadius = new Dictionary<Mark, float>();

        float RingRadius(Mark mark) => _ringRadius.TryGetValue(mark, out float radius) ? radius : Rumors.RumorRadius;

        // ---- The marks ----

        static readonly Color MeColor = new Color(1f, 0.97f, 0.9f);

        bool Shows(Mark mark) => mark.Kind == MarkKind.Always || !_hidden.Contains(mark.Kind) || mark.Also is MarkKind also && !_hidden.Contains(also);

        void DrawMarks(Rect view, List<Rect> covers)
        {
            var e = Event.current;
            Ui.Row("지도 표시");
            Mark hovered = null;
            if (!Controls.UsingTouch && view.Contains(e.mousePosition) && !_mapDragging && !covers.Any(c => c.Contains(e.mousePosition))) hovered = MarkAt(view, e.mousePosition, 12);
            foreach (var mark in _marks)
            {
                if (!Shows(mark)) continue;
                var p = ToScreen(view, mark.At);
                if (!view.Contains(p)) continue;
                bool picked = mark.Name == _pickedName && Vec2.Distance(mark.At, _pickedAt) < 0.6f;
                if (e.type == EventType.Repaint)
                {
                    var image = UiIcons.Of(mark.Icon, Sprites.ToRgba(mark.Color));
                    if (picked || mark == hovered)
                    {
                        float ring = Mathf.Max(UiSkin.SizeOf(image).x, UiSkin.SizeOf(image).y) + 8;
                        var previous = GUI.color;
                        GUI.color = new Color(1f, 0.95f, 0.7f, (picked ? 0.95f : 0.6f) * previous.a);
                        GUI.DrawTexture(new Rect(p.x - ring / 2, p.y - ring / 2, ring, ring), Shapes.Ring.texture);
                        GUI.color = previous;
                    }
                    if (float.IsNaN(mark.Turn)) UiSkin.ImageCentred(p, image);
                    else
                    {
                        var matrix = GUI.matrix;
                        GUIUtility.RotateAroundPivot(mark.Turn, p);
                        UiSkin.ImageCentred(p, image);
                        GUI.matrix = matrix;
                    }
                    if (mark.Label) Name(p, mark.Name, view);
                }
                float hit = Controls.UsingTouch ? 44 : 20;
                Ui.Probe(mark.Name, new Rect(p.x - hit / 2, p.y - hit / 2, hit, hit));
            }
            Ui.Row("");
            // On a PC the mark under the mouse is told on a small card beside it.
            if (hovered != null && hovered.Name != _pickedName && e.type == EventType.Repaint)
            {
                string text = hovered.Info.Length > 0 ? $"{hovered.Name}\n{hovered.Info}" : hovered.Name;
                var size = Ui.CardSize(text, 240, out string wrapped);
                var pointer = e.mousePosition;
                float x = pointer.x + 16 + size.x > Ui.Width - 8 ? pointer.x - 16 - size.x : pointer.x + 16;
                float y = Mathf.Clamp(pointer.y - 10, 8, Mathf.Max(8, Ui.Height - size.y - 8));
                Ui.LateCard(new Rect(x, y, size.x, size.y), wrapped);
            }
        }

        static GUIStyle _nameStyle;

        // A place's name beside its mark, kept inside the view (left of the mark near the right edge).
        static void Name(Vector2 at, string name, Rect view)
        {
            _nameStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft };
            Ui.Text(_nameStyle, 12);
            float width = _nameStyle.CalcSize(new GUIContent(name)).x;
            float x = at.x + 12 + width > view.xMax - 4 ? at.x - 12 - width : at.x + 12;
            var rect = new Rect(x, at.y - 9, width + 2, 18);
            var previous = GUI.color;
            _nameStyle.normal.textColor = new Color(0.1f, 0.06f, 0.03f, 0.9f);
            foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) }) GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), name, _nameStyle);
            _nameStyle.normal.textColor = new Color(1f, 0.96f, 0.86f);
            GUI.Label(rect, name, _nameStyle);
            GUI.color = previous;
        }

        // The mark nearest the point within `reach` points (the ones shown), or null.
        Mark MarkAt(Rect view, Vector2 point, float reach)
        {
            Mark best = null;
            float nearest = reach;
            foreach (var mark in _marks)
            {
                if (!Shows(mark)) continue;
                var p = ToScreen(view, mark.At);
                if (!view.Contains(p)) continue;
                float d = Vector2.Distance(p, point);
                // The player's own arrow gives way to what lies under it.
                if (mark.Name == "나") d += 6;
                if (d > nearest) continue;
                (best, nearest) = (mark, d);
            }
            return best;
        }

        Mark PickedMark() => _pickedName == null ? null : _marks.FirstOrDefault(m => m.Name == _pickedName && Vec2.Distance(m.At, _pickedAt) < 0.6f && Shows(m));

        // ---- What is on the map ----

        void BuildMarks(DungeonInstance dungeon)
        {
            _marks.Clear();
            _ringRadius.Clear();
            var from = dungeon != null ? _client.Position : _client.WorldPosition;
            var here = _client.Biomes[TileMap.Cell(_client.WorldPosition.X), TileMap.Cell(_client.WorldPosition.Y)];
            string Way(Vec2 to) => Rumors.Way(WayKnown.Known, from, to, here, here);
            if (dungeon != null) DungeonMarks(dungeon, Way);
            else WorldMarks(Way, here);
            // The pin being guided to (ScreenStack.Tracked "pin"), the other players, and the player, last.
            if (Screens != null && Screens.Tracked == "pin")
                _marks.Add(new Mark { At = Screens.PinAt, Kind = MarkKind.Always, Icon = MapIcons.Pin, Color = new Color(0.85f, 0.25f, 0.2f), Name = Screens.PinName, Info = "길 안내 중 · " + Way(Screens.PinAt) });
            foreach (var other in _client.Others())
            {
                if (!other.IsPlayer) continue;
                _marks.Add(new Mark { At = other.Position, Kind = MarkKind.Always, Icon = MapIcons.Arrow, Color = OtherPlayer, Turn = TurnOf(other.Facing), Name = string.IsNullOrEmpty(other.Name) ? "다른 플레이어" : other.Name, Info = "다른 플레이어 · " + Way(other.Position) });
            }
            _marks.Add(new Mark { At = _client.Position, Kind = MarkKind.Always, Icon = MapIcons.Arrow, Color = MeColor, Turn = TurnOf(_client.Facing), Name = "나", Info = BiomeMap.Name(here) });
        }

        // Degrees clockwise from north for a facing (y up).
        static float TurnOf(Vec2 facing) => facing.LengthSquared < 0.0001f ? 0 : Mathf.Atan2(facing.X, facing.Y) * Mathf.Rad2Deg;

        void WorldMarks(Func<Vec2, string> way, Biome here)
        {
            var place = _client.WaypointNear();
            var stone = _client.StoneNear();
            var at = place != null && _client.Waypoints.Contains(place.Definition.Index) ? place.Definition : null;
            var atStone = stone != null && _client.StoneOpen(stone.Index) ? stone : null;
            bool by = !_client.Riding && (at != null || atStone != null);
            const string notBy = "열린 웨이포인트나 길잡이 돌 곁에서 갈 수 있다";

            // The places of the story whose spot is known: a house, or the waypoint's crystal once it is open.
            foreach (var site in _client.Places)
            {
                if (KnowledgeOf(site) != WayKnown.Known) continue;
                var definition = site.Definition;
                bool open = _client.Waypoints.Contains(definition.Index);
                var mark = new Mark
                {
                    At = site.Center, Kind = MarkKind.Place, Also = definition.Waypoint ? MarkKind.Waypoint : (MarkKind?)null,
                    Icon = definition.Waypoint && open ? MapIcons.Waypoint : MapIcons.Place,
                    Color = open ? WaypointOpen : new Color(0.9f, 0.86f, 0.76f), Name = definition.Name, Label = true,
                };
                mark.Info = (definition.Waypoint ? open ? "웨이포인트 열림" : "웨이포인트: 아직 가 보지 않았다" : "장소") + " · " + (site.Contains(_client.WorldPosition) ? "여기" : way(site.Center));
                if (definition.Waypoint && open && definition != at)
                {
                    if (by) mark.Travel = _client.TeleportCommand(definition);
                    else mark.TravelWhy = notBy;
                }
                _marks.Add(mark);
            }
            // Where the story leads, and each side story taken: a star once its spot is known, else a faint ring about it.
            var leads = _client.SiteOf(_client.CurrentStep?.Target);
            if (leads != null) Leads(leads, "이야기", new Color(1f, 0.82f, 0.3f), way);
            foreach (var (step, taken) in _client.CurrentSteps)
            {
                if (step.Chain.Length == 0 || !taken || !(_client.SiteOf(step.Target) is PlaceSite side) || side == leads) continue;
                Leads(side, "곁이야기", new Color(1f, 0.93f, 0.7f), way);
            }

            // The things to find once their tile has been seen (묶음 3a~3c).
            foreach (var poi in _client.Pois)
            {
                if (!Seen(poi.X, poi.Y)) continue;
                bool live = _client.CanUse(poi);
                string name = poi.Definition.Name;
                switch (poi.Kind)
                {
                    case PoiKind.Station:
                    {
                        var line = _client.LineOf(poi);
                        bool running = line != null && _client.RailFixed(line.Index);
                        _marks.Add(new Mark { At = poi.Center, Kind = MarkKind.Rail, Icon = MapIcons.Station, Color = running ? new Color(0.78f, 0.84f, 0.94f) : new Color(0.6f, 0.55f, 0.5f), Name = name, Info = (running ? "선로가 이어졌다" : "선로가 끊겼다") + " · " + way(poi.Center) });
                        continue;
                    }
                    case PoiKind.Waystone:
                    {
                        bool open = _client.StoneOpen(poi.Index);
                        var mark = new Mark { At = poi.Center, Kind = MarkKind.Waypoint, Icon = MapIcons.Stone, Color = open ? WaypointOpen : new Color(0.6f, 0.7f, 0.82f), Name = $"{BiomeMap.Name(poi.Biome)}의 {name}", Info = (open ? "길잡이 돌 열림" : "아직 만지지 않았다") + " · " + way(poi.Center) };
                        if (open && poi != atStone)
                        {
                            if (by) mark.Travel = _client.TravelToStoneCommand(poi);
                            else mark.TravelWhy = notBy;
                        }
                        _marks.Add(mark);
                        continue;
                    }
                }
                var (icon, color, state) = poi.Kind switch
                {
                    PoiKind.Camp => (MapIcons.Camp, new Color(1f, 0.6f, 0.25f), "야영지"),
                    PoiKind.Shrine => (MapIcons.Shrine, live ? new Color(0.95f, 0.95f, 1f) : new Color(0.6f, 0.6f, 0.6f), live ? "기도할 수 있다" : "오늘은 기도했다"),
                    PoiKind.Chest => (MapIcons.Chest, live ? new Color(1f, 0.84f, 0.3f) : new Color(0.6f, 0.52f, 0.36f), live ? "아직 열지 않았다" : "열었다"),
                    _ => (MapIcons.Cart, live ? new Color(0.78f, 0.55f, 0.32f) : new Color(0.5f, 0.42f, 0.34f), live ? "뒤져 볼 수 있다" : "뒤졌다"),
                };
                _marks.Add(new Mark { At = poi.Center, Kind = MarkKind.Poi, Icon = icon, Color = color, Name = name, Info = state + " · " + way(poi.Center) });
            }
            if (_client.WandererAt() is (int wx, int wy) && Seen(wx, wy))
                _marks.Add(new Mark { At = TileMap.Center(wx, wy), Kind = MarkKind.Poi, Icon = MapIcons.Stall, Color = new Color(0.25f, 0.75f, 0.68f), Name = "떠돌이 상인", Info = "오늘 여기 있다 · " + way(TileMap.Center(wx, wy)) });
            if (Scrapped is Poi scrapped && !Seen(scrapped.X, scrapped.Y))
                _marks.Add(new Mark { At = scrapped.Center, Kind = MarkKind.Poi, Icon = MapIcons.Lead, Color = new Color(1f, 0.9f, 0.5f), Name = "지도 조각이 가리킨 곳", Info = way(scrapped.Center) });
            foreach (var (x, y) in _camps)
                if (Seen(x, y)) _marks.Add(new Mark { At = TileMap.Center(x, y), Kind = MarkKind.Boss, Icon = MapIcons.Skull, Color = new Color(1f, 0.3f, 0.25f), Name = "정예 야영지", Info = way(TileMap.Center(x, y)) });
            for (int i = 0; i < _client.Arenas.Count; i++)
            {
                var known = KnowledgeOfArena(i);
                if (known == WayKnown.Known)
                {
                    _marks.Add(new Mark { At = _client.Arenas[i], Kind = MarkKind.Boss, Icon = MapIcons.Arena, Color = ArenaMark, Name = "보스 아레나", Info = way(_client.Arenas[i]) });
                    continue;
                }
                float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
                Ring(Rumors.Circle(_client.Seed, RumorKind.Arena, i, _client.Arenas[i], radius), radius, ArenaMark, "보스 아레나의 소문", here);
            }
            // The rumours heard and not yet seen.
            if (_heard.Count > 0)
            {
                var targets = Rumors.Targets(_client.Rooms, _client.WandererAt(), _client.Biomes);
                foreach (var heard in _heard)
                {
                    if (heard.Kind == RumorKind.Wanderer && heard.Day != _client.Today) continue;
                    int index = targets.FindIndex(t => t.Kind == heard.Kind && t.Index == heard.Index);
                    if (index < 0 || Seen(targets[index].X, targets[index].Y)) continue;
                    Ring(Rumors.Circle(_client.Seed, targets[index], Rumors.RumorRadius), Rumors.RumorRadius, RumorColor(heard.Kind), $"소문: {Rumors.Name(heard.Kind)}", here, targets[index].Biome);
                }
            }
            // The dungeons' gates once seen.
            foreach (var gate in _client.Gates)
            {
                if (!Seen(gate.X, gate.Y)) continue;
                int inside = _client.DungeonOf(gate) is DungeonInstance standing ? _client.PlayersIn(standing) : 0;
                _marks.Add(new Mark { At = gate.Center, Kind = MarkKind.Gate, Icon = MapIcons.Gate, Color = inside > 0 ? GateMark : new Color(0.62f, 0.5f, 0.85f), Name = _client.DungeonNameOf(gate), Info = (inside > 0 ? $"안에 {inside}명 · " : "던전 문 · ") + way(gate.Center) });
            }
            // The hunting grounds once seen (version 8, Docs/HuntingGrounds.md).
            if (_client.Rooms is RoomGraph rooms)
                foreach (int index in rooms.HuntingGrounds)
                {
                    var ground = rooms.Nodes[index];
                    if (!Seen(ground.X, ground.Y)) continue;
                    _marks.Add(new Mark { At = ground.Center, Kind = MarkKind.Hunt, Icon = MapIcons.Hunt, Color = HuntMark, Name = "사냥터", Info = "몬스터가 계속 나온다 · " + way(ground.Center) });
                }
            // The nearest veins (a prospector's feel, Sense).
            foreach (var (vein, color) in _veins)
                _marks.Add(new Mark { At = vein, Kind = MarkKind.Ore, Icon = MapIcons.Ore, Color = color, Name = "가까운 광맥", Info = way(vein) });
        }

        void Leads(PlaceSite site, string what, Color color, Func<Vec2, string> way)
        {
            var known = KnowledgeOf(site);
            if (known == WayKnown.Known)
            {
                _marks.Add(new Mark { At = site.Center + new Vec2(0, 1.5f), Kind = MarkKind.Always, Icon = MapIcons.Lead, Color = color, Name = $"{what}: {site.Definition.Name}", Info = way(site.Center) });
                return;
            }
            float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
            var here = _client.Biomes[TileMap.Cell(_client.WorldPosition.X), TileMap.Cell(_client.WorldPosition.Y)];
            var there = _client.Biomes[TileMap.Cell(site.Center.X), TileMap.Cell(site.Center.Y)];
            Ring(Rumors.Circle(_client.Seed, RumorKind.Arena, 1000 + site.Definition.Index, site.Center, radius), radius, color, $"{what}: {site.Definition.Name}", here, there);
        }

        // A rumour's faint ring and the question mark at its middle (a mark, so it can be pressed for its card).
        void Ring(Vec2 middle, float radius, Color color, string name, Biome here, Biome? there = null)
        {
            var biome = there ?? _client.Biomes[TileMap.Cell(middle.X), TileMap.Cell(middle.Y)];
            var mark = new Mark
            {
                At = middle, Kind = MarkKind.Rumor, Icon = MapIcons.Rumor, Color = color, Name = name, Exact = false,
                Info = $"이 원 어딘가 · {Rumors.Way(WayKnown.Rumor, _client.WorldPosition, middle, biome, here)}",
            };
            _ringRadius[mark] = radius;
            _marks.Add(mark);
        }

        void DungeonMarks(DungeonInstance dungeon, Func<Vec2, string> way)
        {
            foreach (var (x, y) in dungeon.Layout.Exits)
                if (Seen(x, y)) _marks.Add(new Mark { At = TileMap.Center(x, y), Kind = MarkKind.Door, Icon = MapIcons.Gate, Color = GateMark, Name = "나가는 문", Info = way(TileMap.Center(x, y)) });
            foreach (var room in dungeon.Layout.Nodes)
            {
                if (!SeenRoom(room)) continue;
                if (room.Letter == 'B')
                {
                    bool down = dungeon.Has(DungeonFlags.BossDown);
                    _marks.Add(new Mark { At = room.Center, Kind = MarkKind.Master, Icon = MapIcons.Skull, Color = down ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.3f, 0.25f), Name = "주인의 방", Info = (down ? "쓰러뜨렸다 · " : "") + way(room.Center) });
                }
                else if (DungeonSheet.HasPack(room.Letter) && !dungeon.RoomCleared(room.Index))
                    _marks.Add(new Mark { At = room.Center, Kind = MarkKind.Pack, Icon = MapIcons.Pack, Color = room.Letter == 'M' ? new Color(1f, 0.6f, 0.2f) : new Color(0.95f, 0.3f, 0.3f), Name = "남은 무리", Info = way(room.Center) });
            }
            foreach (var thing in dungeon.Layout.Things)
            {
                if (!Seen(thing.X, thing.Y)) continue;
                bool used = dungeon.ThingUsed(thing.Index);
                var (icon, color, name) = thing.Kind == DungeonThingKind.Fire ? (MapIcons.Fire, FireMark, "모닥불")
                    : thing.Kind == DungeonThingKind.Shrine ? (MapIcons.Shrine, new Color(0.4f, 0.9f, 0.95f), "제단")
                    : (MapIcons.Chest, ChestMark, thing.End ? "끝 상자" : "상자");
                if (used) color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f);
                _marks.Add(new Mark { At = thing.Center, Kind = MarkKind.Things, Icon = icon, Color = color, Name = name, Info = (used ? "썼다 · " : "") + way(thing.Center) });
            }
            foreach (var stone in new[] { dungeon.Layout.ShortcutFrom, dungeon.Layout.ShortcutTo })
                if (stone is (int sx, int sy) && Seen(sx, sy))
                {
                    bool open = dungeon.Has(DungeonFlags.ShortcutOpen);
                    _marks.Add(new Mark { At = TileMap.Center(sx, sy), Kind = MarkKind.Shortcut, Icon = MapIcons.Stone, Color = open ? new Color(0.43f, 0.75f, 1f) : new Color(0.5f, 0.6f, 0.7f), Name = "지름길 돌", Info = (open ? "열렸다 · " : "") + way(TileMap.Center(sx, sy)) });
                }
        }

        // ---- The card ----

        static GUIStyle _cardName, _cardLine;

        // On a low view (a phone held sideways) the card's buttons stand at its right, beside the words, so it stays short.
        bool _cardBeside;

        float CardButtonsWidth(Mark mark) => (mark.Travel != null ? 72 + 6 : 0) + (mark.Name != "나" && mark.Exact ? 128 : 0);

        float CardHeight(Mark mark, float width)
        {
            CardStyles();
            float text = width - 20 - (_cardBeside ? CardButtonsWidth(mark) + 6 : 0);
            float h = 10 + _cardName.CalcHeight(new GUIContent(Ui.Wrap(mark.Name, _cardName, text)), text) + 2;
            if (mark.Info.Length > 0) h += _cardLine.CalcHeight(new GUIContent(Ui.Wrap(mark.Info, _cardLine, text)), text) + 2;
            if (mark.TravelWhy.Length > 0) h += _cardLine.CalcHeight(new GUIContent(Ui.Wrap(mark.TravelWhy, _cardLine, text)), text) + 2;
            if (CardButtons(mark) > 0) h = _cardBeside ? Mathf.Max(h, 10 + Finger) : h + 6 + Finger;
            return Mathf.Ceil(h + 10);
        }

        static void CardStyles()
        {
            _cardName ??= new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.UpperLeft };
            _cardLine ??= new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.UpperLeft };
            Ui.Text(_cardName, 12, bold: true);
            Ui.Text(_cardLine, 12);
            _cardName.normal.textColor = UiSkin.Gold;
            _cardLine.normal.textColor = new Color(0.97f, 0.93f, 0.85f);
        }

        // [여기로 길 안내] for anything but the player and a rumour's ring, and [이동] where it can be gone to now.
        int CardButtons(Mark mark) => (mark.Name != "나" && mark.Exact ? 1 : 0) + (mark.Travel != null ? 1 : 0);

        // The card over the map's bottom left corner (the whole width on a narrow view; wider on a low one, its buttons
        // beside its words).
        Rect CardRect(Rect view, Mark mark)
        {
            _cardBeside = view.height < 280 && view.width >= 340;
            float width = Mathf.Min(view.width - 16, _cardBeside ? 460 : 320);
            float height = CardHeight(mark, width);
            return new Rect(view.x + 8, view.yMax - 8 - height, width, height);
        }

        void DrawCard(Rect r, Mark mark)
        {
            CardStyles();
            Controls.Block(r);
            if (Event.current.type == EventType.Repaint) UiSkin.Card(r);
            float buttons = _cardBeside && CardButtons(mark) > 0 ? CardButtonsWidth(mark) + 6 : 0;
            float x = r.x + 10, width = r.width - 20 - buttons, y = r.y + 10;
            string name = Ui.Wrap(mark.Name, _cardName, width);
            float h = _cardName.CalcHeight(new GUIContent(name), width);
            GUI.Label(new Rect(x, y, width, h), name, _cardName);
            y += h + 2;
            foreach (string line in new[] { mark.Info, mark.TravelWhy })
            {
                if (line.Length == 0) continue;
                string wrapped = Ui.Wrap(line, _cardLine, width);
                h = _cardLine.CalcHeight(new GUIContent(wrapped), width);
                GUI.Label(new Rect(x, y, width, h), wrapped, _cardLine);
                y += h + 2;
            }
            if (CardButtons(mark) == 0) return;
            y = _cardBeside ? r.y + (r.height - Finger) / 2 : y + 6;
            Ui.Row(mark.Name);
            float right = r.xMax - 10;
            if (mark.Travel != null)
            {
                float w = 72;
                if (Ui.Button(new Rect(right - w, y, w, Finger), "이동", Ui.Look.Primary))
                {
                    Send?.Invoke(mark.Travel);
                    FullOpen = false;
                }
                right -= w + 6;
            }
            if (mark.Name != "나" && mark.Exact && Screens != null)
            {
                bool guided = Screens.Tracked == "pin" && Vec2.Distance(Screens.PinAt, mark.At) < 0.6f;
                string label = guided ? "길 안내 그만" : "여기로 길 안내";
                float w = Mathf.Min(right - r.x - 10, 128);
                if (Ui.Button(new Rect(right - w, y, w, Finger), label))
                {
                    if (guided) Screens.Tracked = "";
                    else
                    {
                        Screens.Tracked = "pin";
                        Screens.PinAt = mark.At;
                        Screens.PinName = mark.Name;
                    }
                }
            }
            Ui.Row("");
        }

        // ---- The legend ----

        static GUIStyle _chipText;

        // Where the legend's toggle i goes: in two columns under the side page's heading, or in rows under the map.
        internal static Rect LegendChip(MapLayout lay, int i)
        {
            var area = lay.Legend;
            float top = lay.Side ? area.y + 26 : area.y;
            float width = (area.width - (lay.Side ? 16 : 0) - 4 * (lay.Columns - 1)) / lay.Columns;
            float left = area.x + (lay.Side ? 8 : 0);
            return new Rect(left + i % lay.Columns * (width + 4), top + i / lay.Columns * (lay.Chip + 4), width, lay.Chip);
        }

        // The legend's toggles: each kind's mark and name; a press hides or shows its marks. Beside the map, under its
        // heading and a hint of how to move it; or in rows under the map.
        void DrawLegend(MapLayout lay, (MarkKind Kind, string Name, string[] Icon, Color Color)[] legend)
        {
            var e = Event.current;
            _chipText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            Ui.Text(_chipText, 12, bold: true);
            var area = lay.Legend;
            float top = area.y;
            if (lay.Side)
            {
                if (e.type == EventType.Repaint) UiSkin.Page(new Rect(area.x, area.y, area.width, Mathf.Min(lay.View.height, Mathf.Max(area.height + 70, 140))));
                _chipText.normal.textColor = new Color(0.55f, 0.16f, 0.1f);
                GUI.Label(new Rect(area.x + 10, area.y + 6, area.width - 20, 16), Ui.Wrap("범례", _chipText, 999), _chipText);
                top = area.y + 26;
            }
            Ui.Row("범례");
            for (int i = 0; i < legend.Length; i++)
            {
                var (kind, name, icon, color) = legend[i];
                bool on = !_hidden.Contains(kind);
                var rect = LegendChip(lay, i);
                float width = rect.width;
                if (Ui.Button(rect, "", name))
                {
                    if (on) _hidden.Add(kind);
                    else _hidden.Remove(kind);
                    if (PickedMark() == null) _pickedName = null;
                }
                if (e.type != EventType.Repaint) continue;
                var previous = GUI.color;
                if (!on) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * 0.4f);
                var image = UiIcons.Of(icon, Sprites.ToRgba(color));
                var size = UiSkin.SizeOf(image);
                bool room = width >= size.x + 50;
                float iconX = room ? rect.x + 6 : rect.center.x - size.x / 2;
                UiSkin.Image(new Vector2(Mathf.Round(iconX), Mathf.Round(rect.center.y - size.y / 2)), image);
                _chipText.normal.textColor = new Color(0.97f, 0.93f, 0.85f);
                if (room) GUI.Label(new Rect(iconX + size.x + 4, rect.y, rect.xMax - iconX - size.x - 8, rect.height), name, _chipText);
                // A struck line through one hidden.
                if (!on)
                {
                    GUI.color = new Color(0.9f, 0.3f, 0.25f, previous.a * 0.8f);
                    GUI.DrawTexture(new Rect(rect.x + 4, rect.center.y, rect.width - 8, 2), Texture2D.whiteTexture);
                }
                GUI.color = previous;
            }
            Ui.Row("");
            if (!lay.Side) return;
            // Under the toggles: how to move the map.
            float hintTop = top + (legend.Length + lay.Columns - 1) / lay.Columns * (lay.Chip + 4) + 6;
            float bottom = Mathf.Min(lay.View.yMax, area.y + Mathf.Max(area.height + 70, 140)) - 8;
            if (bottom - hintTop < 30) return;
            _chipText.normal.textColor = new Color(0.43f, 0.33f, 0.21f);
            _chipText.wordWrap = true;
            string hint = Controls.UsingTouch ? "끌어서 옮기고 두 손가락으로 확대. 표시를 누르면 자세히." : "끌어서 옮기고 휠로 확대. 표시를 누르면 자세히.";
            string wrapped = Ui.Wrap(hint, _chipText, area.width - 20);
            GUI.Label(new Rect(area.x + 10, hintTop, area.width - 20, bottom - hintTop), wrapped, _chipText);
            _chipText.wordWrap = false;
        }
    }
}
