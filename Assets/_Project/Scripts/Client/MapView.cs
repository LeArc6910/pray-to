using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // The minimap in the top right corner and the full map it opens (the full map's window, U5: MapView.Full.cs). Only tiles that have come within a few tiles of
    // the local player are drawn, so the map fills in as the caves are explored. With rumours guiding (Settings.Rumors,
    // 묶음 4c) a boss arena, a place or where the story leads that has not been seen is a wide faint circle on the full
    // map (Rumors.Circle), as are the rumours heard from people; with arrows, they are marked as before. What has been
    // explored and heard is a view, not game state: it is not shared, and each player keeps their own on their device
    // for each character and world (MapSave, GameRoot.MapKey).
    sealed partial class MapView : IDisposable
    {
        // Matches what the camera shows around the player.
        const int RevealRadius = 8;
        // Tiles across the minimap.
        const int MinimapTiles = 40;
        const float MarkerSize = 7;

        static readonly Color32 Floor = new Color32(96, 80, 66, 255);

        // Each biome's floor on the map, so its regions show.
        static Color32 BiomeFloor(Biome biome) => biome switch
        {
            Biome.Mushroom => new Color32(78, 96, 58, 255),
            Biome.Ice => new Color32(84, 108, 128, 255),
            Biome.Crystal => new Color32(66, 72, 92, 255),
            Biome.Lava => new Color32(96, 64, 56, 255),
            Biome.Abyss => new Color32(48, 38, 72, 255),
            Biome.Lake => new Color32(40, 78, 84, 255),
            Biome.Sand => new Color32(118, 92, 56, 255),
            _ => Floor,
        };
        static readonly Color32 Wall = new Color32(158, 140, 118, 255);
        static readonly Color32 ArenaFloor = new Color32(120, 52, 46, 255);
        static readonly Color Backdrop = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        static readonly Color OtherPlayer = new Color(0.45f, 0.7f, 1f);
        static readonly Color ArenaMark = new Color(1f, 0.35f, 0.3f);

        // The nearest vein of each early ore within SenseTiles, explored or not (2026-11-09): a prospector's feel for
        // where the copper is, so finding the next vein is not a blind dig. Looked for again every SenseSeconds.
        const int SenseTiles = 16;
        const float SenseSeconds = 0.5f;
        static readonly (TileType Ore, Color Color)[] SensedOres = { (TileType.CopperOre, Palette.CopperOre), (TileType.IronOre, Palette.IronOre) };
        readonly List<(Vec2 At, Color Color)> _veins = new List<(Vec2, Color)>();
        float _sensedAt = -1;

        void Sense()
        {
            _veins.Clear();
            int cx = TileMap.Cell(_client.Position.X), cy = TileMap.Cell(_client.Position.Y);
            foreach (var (ore, color) in SensedOres)
            {
                float best = float.MaxValue;
                Vec2? at = null;
                for (int y = cy - SenseTiles; y <= cy + SenseTiles; y++)
                {
                    for (int x = cx - SenseTiles; x <= cx + SenseTiles; x++)
                    {
                        if (!_map.InBounds(x, y) || _map[x, y] != ore) continue;
                        float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d > SenseTiles * SenseTiles || d >= best) continue;
                        best = d;
                        at = TileMap.Center(x, y);
                    }
                }
                if (at.HasValue) _veins.Add((at.Value, color));
            }
        }

        readonly WorldClient _client;
        readonly TileMap _map;
        readonly Texture2D _texture;
        readonly Color32[] _pixels;
        readonly bool[] _explored;
        readonly List<Vec2> _others = new List<Vec2>();
        int _revealedX = int.MinValue;
        int _revealedY = int.MinValue;
        bool _dirty;

        // saved is what an earlier session explored here (MapSave), or null to start with nothing.
        public MapView(WorldClient client, byte[] saved = null)
        {
            _client = client;
            _map = client.Map;
            // The world's rows and the dungeons' pocket above them (TileMap.Rows, 묶음 5a); only the world's are kept.
            _pixels = new Color32[_map.Width * _map.Rows];
            _sheetPixels = new Color32[_pixels.Length];
            _explored = new bool[_pixels.Length];
            if (MapSave.Read(saved, _map.Width, _map.Height) is bool[] kept) Array.Copy(kept, _explored, kept.Length);
            _heard = MapSave.ReadRumors(saved, _map.Width, _map.Height);
            _texture = new Texture2D(_map.Width, _map.Rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Unexplored pixels stay transparent.
            for (int i = 0; i < _explored.Length; i++)
            {
                if (_explored[i]) Paint(i % _map.Width, i / _map.Width);
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply();
            // The hidden rooms set into the rock (묶음 3b): their tiles are not revealed while their cracked wall stands.
            var rooms = client.Rooms;
            if (rooms != null)
            {
                foreach (var room in rooms.Nodes)
                {
                    if (!room.Buried || room.Box is not (int left, int bottom, int w, int h)) continue;
                    _buried.Add((left, bottom, w, h));
                }
            }
            foreach (var room in rooms?.Nodes.Where(n => n.Role == RoomRole.Elite) ?? Enumerable.Empty<RoomNode>())
            {
                var mark = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Elite).Select(m => (RoomMark?)m).FirstOrDefault();
                _camps.Add(mark is RoomMark leader ? (leader.X, leader.Y) : (room.X, room.Y));
            }
        }

        // The hidden rooms in the rock, by their boxes, and where each elite camp's leader stands.
        readonly List<(int Left, int Bottom, int W, int H)> _buried = new List<(int, int, int, int)>();
        readonly List<(int X, int Y)> _camps = new List<(int, int)>();

        // Whether the tile is in a hidden room whose cracked wall still stands.
        bool Shut(int x, int y)
        {
            foreach (var (left, bottom, w, h) in _buried)
            {
                if (x < left || y < bottom || x >= left + w || y >= bottom + h) continue;
                for (int ty = bottom; ty < bottom + h; ty++)
                    for (int tx = left; tx < left + w; tx++)
                        if (_map[tx, ty] == TileType.CrackedWall) return true;
                return false;
            }
            return false;
        }

        bool[] _worldSeen;

        // A slot of the pocket laid again or gone back to rock (WorldClient.TakeRebuiltSlots): nothing of it seen.
        public void ResetSlots(IEnumerable<int> slots)
        {
            foreach (int slot in slots)
            {
                var (left, bottom, w, h) = DungeonPocket.Box(_map, slot);
                for (int y = bottom; y < bottom + h; y++)
                    for (int x = left; x < left + w; x++)
                    {
                        _explored[y * _map.Width + x] = false;
                        _pixels[y * _map.Width + x] = default;
                        _sheetPixels[y * _map.Width + x] = default;
                    }
            }
            _revealedX = _revealedY = int.MinValue;
            _dirty = true;
            _sheetDirty = true;
        }

        // The part of the map the player is in: the world, or their dungeon's slot of the pocket.
        Rect Shown()
        {
            if (_client.DungeonHere is DungeonInstance dungeon)
            {
                var (x, y) = DungeonPocket.Inside(_map, dungeon.Slot);
                return new Rect(x, y, DungeonPocket.Inner, DungeonPocket.Inner);
            }
            return new Rect(0, 0, _map.Width, _map.Height);
        }

        // Whether more has been explored since the last Save.
        public bool Changed { get; private set; }

        // What has been explored, to keep (MapSave).
        public byte[] Save()
        {
            Changed = false;
            // The world's rows only: what was seen in a dungeon is for that dungeon (Docs/WorldRework.md 지도와 화면).
            _worldSeen ??= new bool[_map.Width * _map.Height];
            Array.Copy(_explored, _worldSeen, _worldSeen.Length);
            return MapSave.Write(_worldSeen, _map.Width, _map.Height, _heard);
        }

        // ---- Rumours (묶음 4c, Docs/WorldRework.md 덜 정확한 안내) ----

        // The rumours heard here, oldest first (MapSave format 2).
        readonly List<HeardRumor> _heard;
        public IReadOnlyList<HeardRumor> Heard => _heard;

        // Whether the tile has been seen on this map.
        public bool Seen(int x, int y) => _map.InBounds(x, y) && _explored[y * _map.Width + x];
        public bool Seen(Vec2 at) => Seen(TileMap.Cell(at.X), TileMap.Cell(at.Y));

        // Notes a rumour heard (the person's line, StoryScreen): its faint ring shows on the full map until the thing is
        // seen (the wandering merchant's for the day only).
        public void Hear(RumorTarget target, int day)
        {
            _heard.RemoveAll(h => h.Kind == target.Kind && h.Index == target.Index);
            _heard.Add(new HeardRumor(target.Kind, target.Index, day));
            Changed = true;
        }

        // Whether the rumour of it was heard on that day (a person tells today's rumour again however often asked).
        public bool HeardOn(RumorTarget target, int day) => _heard.Any(h => h.Kind == target.Kind && h.Index == target.Index && h.Day == day);

        public bool HasHeard(RumorTarget target) => _heard.Any(h => h.Kind == target.Kind && h.Index == target.Index && (target.Kind != RumorKind.Wanderer || h.Day == _client.Today));

        // Whether a thing is already known, so a person tells of another (Rumors.Pick): seen, heard of, or used up
        // (an opened chest, a mended line, a touched waystone).
        public bool Knows(RumorTarget target)
        {
            if (Seen(target.X, target.Y) || HasHeard(target)) return true;
            if (target.Kind == RumorKind.Elite || target.Kind == RumorKind.Wanderer || target.Kind == RumorKind.Arena) return false;
            var poi = target.Index >= 0 && target.Index < _client.Pois.Count ? _client.Pois[target.Index] : null;
            if (poi == null) return true;
            if (poi.Kind == PoiKind.Waystone) return _client.StoneOpen(poi.Index);
            if (poi.Kind == PoiKind.Station) return _client.LineOf(poi) is RailLine line && _client.RailFixed(line.Index);
            return !_client.CanUse(poi) && poi.Definition.Again == PoiAgain.Once;
        }

        // How well the way to a place of the story is known (Rumors.Knowledge): for sure with arrows chosen, in the
        // story's first chapter, once its waypoint is open or its middle seen; the way once a signpost toward it has
        // been seen or within Rumors.PathReach.
        public WayKnown KnowledgeOf(PlaceSite site)
        {
            bool sure = !Settings.Rumors || Rumors.FirstChapter(_client.Story, _client.JourneyStep) || _client.Waypoints.Contains(site.Definition.Index) || Seen(site.Center);
            bool signposted = !sure && _client.Signposts.Any(p => p.Toward == site.Definition && Seen(p.X, p.Y));
            return Rumors.Knowledge(Settings.Rumors, sure, Vec2.Distance(site.Center, _client.WorldPosition), signposted);
        }

        // How well an arena's spot is known: the first boss's always (the first hour as before), any once seen.
        public WayKnown KnowledgeOfArena(int index)
        {
            var at = _client.Arenas[index];
            bool sure = index == 0 || Seen(at) || Seen(TileMap.Cell(at.X) + 3, TileMap.Cell(at.Y)) || Seen(TileMap.Cell(at.X) - 3, TileMap.Cell(at.Y));
            return Rumors.Knowledge(Settings.Rumors, sure, Vec2.Distance(at, _client.WorldPosition));
        }

        // The rumours on the map: the things heard of and not yet seen, the arenas, and where the story leads when
        // only its way is known. Each a disc and its rim, wide and faint, never centred on the thing.
        void DrawRumors(Rect area, Rect window)
        {
            float breath = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2);
            var targets = _heard.Count == 0 ? null : Rumors.Targets(_client.Rooms, _client.WandererAt(), _client.Biomes);
            foreach (var heard in _heard)
            {
                if (heard.Kind == RumorKind.Wanderer && heard.Day != _client.Today) continue;
                int at = targets.FindIndex(t => t.Kind == heard.Kind && t.Index == heard.Index);
                if (at < 0) continue;
                var target = targets[at];
                if (Seen(target.X, target.Y)) continue;
                Faint(area, window, Rumors.Circle(_client.Seed, target, Rumors.RumorRadius), Rumors.RumorRadius, RumorColor(target.Kind), breath);
            }
            for (int i = 0; i < _client.Arenas.Count; i++)
            {
                var known = KnowledgeOfArena(i);
                if (known == WayKnown.Known) continue;
                float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
                Faint(area, window, Rumors.Circle(_client.Seed, RumorKind.Arena, i, _client.Arenas[i], radius), radius, ArenaMark, breath);
            }
        }

        internal static Color RumorColor(RumorKind kind) => kind switch
        {
            RumorKind.Camp => new Color(1f, 0.6f, 0.25f),
            RumorKind.Shrine or RumorKind.Waystone => new Color(0.75f, 0.88f, 1f),
            RumorKind.Elite => new Color(1f, 0.3f, 0.25f),
            RumorKind.Wanderer => new Color(0.18f, 0.8f, 0.7f),
            RumorKind.Station or RumorKind.Cart => new Color(0.78f, 0.6f, 0.4f),
            _ => new Color(1f, 0.84f, 0.3f),
        };

        // The mark of a rumour's kind on the full map and in the journal (MapIcons).
        internal static string[] IconOf(RumorKind kind) => kind switch
        {
            RumorKind.Camp => MapIcons.Camp,
            RumorKind.Shrine => MapIcons.Shrine,
            RumorKind.Chest or RumorKind.Hidden or RumorKind.Vault => MapIcons.Chest,
            RumorKind.Cart => MapIcons.Cart,
            RumorKind.Station => MapIcons.Station,
            RumorKind.Waystone => MapIcons.Stone,
            RumorKind.Elite => MapIcons.Skull,
            RumorKind.Wanderer => MapIcons.Stall,
            RumorKind.Arena => MapIcons.Arena,
            _ => MapIcons.Rumor,
        };

        // A wide faint disc of the given radius in tiles, and its rim.
        static void Faint(Rect area, Rect window, Vec2 center, float radius, Color color, float alpha)
        {
            float size = radius * 2 / window.width * area.width;
            Marker(area, window, center, Shapes.Circle, new Color(color.r, color.g, color.b, 0.14f * alpha), size);
            Marker(area, window, center, Rim, new Color(color.r, color.g, color.b, 0.55f * alpha), size);
        }

        static Sprite _rim;
        static Sprite Rim => _rim ??= Shapes.Donut(0.93f);

        // The thing a map scrap pointed to (SimEventType.MapRead, 3c), ringed on the map until it is seen; null for none.
        public Poi Scrapped { get; private set; }

        // A map scrap read: the nearest thing to find that still gives and whose tile this map has not seen (not one
        // in a hidden room still shut in the rock). Null when there is none.
        public Poi ReadScrap()
        {
            var rooms = _client.Rooms;
            Scrapped = _client.Pois.Where(p => p.Kind != PoiKind.Camp && !_explored[p.Y * _map.Width + p.X] && _client.CanUse(p)
                    && !(rooms != null && rooms.Nodes[p.Room].Buried))
                .OrderBy(p => Vec2.Distance(p.Center, _client.WorldPosition)).FirstOrDefault();
            return Scrapped;
        }

        // The screen manager (ScreenStack): the full map is its map screen, in the records group with the journal.
        public ScreenStack Screens { get; set; }

        bool _fullOpen;

        // The full map is showing. Touches and attacks are held back meanwhile.
        public bool FullOpen
        {
            get => Screens != null ? Screens.Current == GameScreen.Map : _fullOpen;
            set
            {
                if (Screens == null) _fullOpen = value;
                else if (value) Screens.Show(GameScreen.Map);
                else if (Screens.Current == GameScreen.Map) Screens.Close();
            }
        }

        public void Dispose()
        {
            Object.Destroy(_texture);
            if (_sheet != null) Object.Destroy(_sheet);
        }

        // Redraws dug or built tiles that have already been explored.
        public void UpdateTiles(IEnumerable<TileChange> changes)
        {
            foreach (var change in changes)
            {
                int index = change.Y * _map.Width + change.X;
                if (!_explored[index]) continue;
                Paint(change.X, change.Y);
                _dirty = true;
            }
        }

        // Once per frame: reveals the tiles around the player and notes where the other players are.
        public void Update()
        {
            int cx = TileMap.Cell(_client.Position.X);
            int cy = TileMap.Cell(_client.Position.Y);
            if (cx != _revealedX || cy != _revealedY)
            {
                Reveal(cx, cy);
                _revealedX = cx;
                _revealedY = cy;
            }
            if (_dirty)
            {
                _texture.SetPixels32(_pixels);
                _texture.Apply();
                _dirty = false;
            }

            if (Time.unscaledTime - _sensedAt >= SenseSeconds)
            {
                _sensedAt = Time.unscaledTime;
                Sense();
            }

            _others.Clear();
            foreach (var entity in _client.Others())
            {
                if (entity.IsPlayer) _others.Add(entity.Position);
            }
        }

        // The minimap, centred on the player. Tapping or clicking it opens the full map.
        public void DrawMinimap(Rect area)
        {
            Controls.Block(area);
            Framed(area);
            float half = MinimapTiles / 2f;
            // Keep the window inside the map (or the dungeon the player is in) so the edges never smear.
            var shown = Shown();
            float left = Mathf.Clamp(_client.Position.X - half, shown.x, Mathf.Max(shown.x, shown.xMax - MinimapTiles));
            float bottom = Mathf.Clamp(_client.Position.Y - half, shown.y, Mathf.Max(shown.y, shown.yMax - MinimapTiles));
            var window = new Rect(left, bottom, MinimapTiles, MinimapTiles);
            DrawTiles(area, window);
            Ui.Probe("지도", area);
            if (GUI.Button(area, GUIContent.none, GUIStyle.none)) FullOpen = true;
        }

        // Draws the tiles of the given window of the map (in tiles, y up) into a GUI area, then the markers.
        void DrawTiles(Rect area, Rect window)
        {
            var uv = new Rect(window.x / _map.Width, window.y / _map.Rows, window.width / _map.Width, window.height / _map.Rows);
            GUI.DrawTextureWithTexCoords(area, _texture, uv);
            // The rumours' faint circles (묶음 4c), over the tiles and under the marks; on the full map only, as a circle
            // is wider than the minimap's window.
            if (window.width >= _map.Width) DrawRumors(area, window);

            for (int i = 0; i < _client.Arenas.Count; i++)
                if (KnowledgeOfArena(i) == WayKnown.Known) Marker(area, window, _client.Arenas[i], Shapes.Ring, ArenaMark, MarkerSize * 2.2f);
            // The nearest veins, breathing so they catch the eye.
            float breath = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4);
            foreach (var (at, color) in _veins) Marker(area, window, at, Shapes.Ring, new Color(color.r, color.g, color.b, breath), MarkerSize * 1.6f);
            // The places of the story (Docs/Story.md, 2026-11-10): blue where the waypoint is open, grey elsewhere,
            // and the place the story leads to now ringed in gold.
            // With rumours (묶음 4c) a place not yet known shows only as the faint circle where the story leads.
            bool full = window.width >= _map.Width;
            foreach (var site in _client.Places)
            {
                if (KnowledgeOf(site) != WayKnown.Known) continue;
                Marker(area, window, site.Center, Shapes.Circle, _client.Waypoints.Contains(site.Definition.Index) ? WaypointOpen : PlaceMark, MarkerSize * 1.5f);
            }
            var leads = _client.SiteOf(_client.CurrentStep?.Target);
            if (leads != null) Leads(area, window, leads, new Color(1f, 0.85f, 0.35f, breath), MarkerSize * 2.8f, full);
            // A side story taken leads too, in a paler, smaller ring (2026-11-11).
            foreach (var (step, taken) in _client.CurrentSteps)
            {
                if (step.Chain.Length == 0 || !taken || !(_client.SiteOf(step.Target) is PlaceSite side) || side == leads) continue;
                Leads(area, window, side, new Color(1f, 0.95f, 0.75f, breath * 0.8f), MarkerSize * 2.2f, full);
            }
            DrawPoiMarks(area, window);
            DrawDungeonMarks(area, window);
            foreach (var position in _others) Marker(area, window, position, Shapes.Circle, OtherPlayer, MarkerSize);
            Marker(area, window, _client.Position, Shapes.Circle, Color.white, MarkerSize);
        }

        // The things to find (묶음 3a), once their tile has been seen: a camp orange, a shrine white (grey once prayed at
        // today), a chest gold and a cart brown (faint once opened); an elite camp a red skull (2-2's mark). 3c: a
        // minecart station steel (a ring while its line is broken), a waystone blue (pale until touched), the
        // wandering merchant's stall teal, and a thing a map scrap marked ringed until it is seen.
        void DrawPoiMarks(Rect area, Rect window)
        {
            bool Seen(int x, int y) => _map.InBounds(x, y) && _explored[y * _map.Width + x];
            foreach (var poi in _client.Pois)
            {
                if (!Seen(poi.X, poi.Y)) continue;
                bool live = _client.CanUse(poi);
                if (poi.Kind == PoiKind.Station)
                {
                    var line = _client.LineOf(poi);
                    bool running = line != null && _client.RailFixed(line.Index);
                    Marker(area, window, poi.Center, running ? Shapes.Square : Shapes.Ring, new Color(0.72f, 0.78f, 0.88f), MarkerSize * 1.3f);
                    continue;
                }
                if (poi.Kind == PoiKind.Waystone)
                {
                    bool open = _client.StoneOpen(poi.Index);
                    Marker(area, window, poi.Center, Shapes.Circle, open ? WaypointOpen : new Color(0.55f, 0.75f, 1f, 0.6f), MarkerSize * (open ? 1.4f : 1.1f));
                    continue;
                }
                var color = poi.Kind switch
                {
                    PoiKind.Camp => new Color(1f, 0.6f, 0.25f),
                    PoiKind.Shrine => live ? Color.white : new Color(0.6f, 0.6f, 0.6f),
                    PoiKind.Chest => live ? new Color(1f, 0.84f, 0.3f) : new Color(0.55f, 0.48f, 0.3f, 0.8f),
                    _ => live ? new Color(0.78f, 0.55f, 0.32f) : new Color(0.45f, 0.36f, 0.28f, 0.8f),
                };
                Marker(area, window, poi.Center, poi.Kind == PoiKind.Camp || poi.Kind == PoiKind.Shrine ? Shapes.Circle : Shapes.Square, color,
                    MarkerSize * (live ? 1.1f : 0.8f));
            }
            // The wandering merchant's stall where it stands today, once seen there.
            if (_client.WandererAt() is (int wx, int wy) && Seen(wx, wy)) Marker(area, window, TileMap.Center(wx, wy), Shapes.Square, new Color(0.18f, 0.66f, 0.6f), MarkerSize * 1.5f);
            // A thing a map scrap pointed to, not seen yet: a breathing ring where it is.
            if (Scrapped is Poi scrapped && !Seen(scrapped.X, scrapped.Y))
                Marker(area, window, scrapped.Center, Shapes.Ring, new Color(1f, 0.9f, 0.5f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3)), MarkerSize * 2.4f);
            foreach (var (x, y) in _camps)
                if (Seen(x, y)) Marker(area, window, TileMap.Center(x, y), Skull, new Color(1f, 0.3f, 0.25f), MarkerSize * 1.8f);
        }

        static readonly Color GateMark = new Color(0.72f, 0.55f, 1f);

        // The dungeons' gates once seen (World.Dungeon.cs, 묶음 5a), brighter with someone inside; in a dungeon, its doors out.
        void DrawDungeonMarks(Rect area, Rect window)
        {
            foreach (var gate in _client.Gates)
            {
                if (!Seen(gate.X, gate.Y)) continue;
                bool busy = _client.DungeonOf(gate) is DungeonInstance standing && _client.PlayersIn(standing) > 0;
                Marker(area, window, gate.Center, Diamond, busy ? GateMark : new Color(GateMark.r, GateMark.g, GateMark.b, 0.65f), MarkerSize * 1.6f);
            }
            if (_client.DungeonHere is DungeonInstance dungeon)
            {
                foreach (var (x, y) in dungeon.Layout.Exits)
                    if (Seen(x, y)) Marker(area, window, TileMap.Center(x, y), Diamond, GateMark, MarkerSize * 1.6f);
                DrawDungeonRooms(area, window, dungeon);
            }
        }

        static readonly Color ChestMark = new Color(1f, 0.85f, 0.35f);
        static readonly Color FireMark = new Color(1f, 0.6f, 0.25f);

        // In a dungeon (묶음 5b), once seen: its master's room a skull (grey once it fell), the chests gold (dim once opened),
        // the rest room's fire, the shortcut's stones, and a pack room not yet cleared a red dot.
        void DrawDungeonRooms(Rect area, Rect window, DungeonInstance dungeon)
        {
            foreach (var room in dungeon.Layout.Nodes)
            {
                if (!SeenRoom(room)) continue;
                if (room.Letter == 'B')
                    Marker(area, window, room.Center, Skull, dungeon.Has(DungeonFlags.BossDown) ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : new Color(1f, 0.3f, 0.25f), MarkerSize * 2.2f);
                else if (DungeonSheet.HasPack(room.Letter) && !dungeon.RoomCleared(room.Index))
                    Marker(area, window, room.Center, Shapes.Circle, room.Letter == 'M' ? new Color(1f, 0.6f, 0.2f) : new Color(0.95f, 0.3f, 0.3f), MarkerSize * 1.2f);
            }
            foreach (var thing in dungeon.Layout.Things)
            {
                if (!Seen(thing.X, thing.Y)) continue;
                var colour = thing.Kind == DungeonThingKind.Fire ? FireMark : thing.Kind == DungeonThingKind.Shrine ? new Color(0.4f, 0.9f, 0.95f) : ChestMark;
                if (dungeon.ThingUsed(thing.Index)) colour.a = 0.45f;
                Marker(area, window, thing.Center, thing.Kind == DungeonThingKind.Chest ? Diamond : Shapes.Circle, colour, MarkerSize * (thing.End ? 1.8f : 1.3f));
            }
            foreach (var stone in new[] { dungeon.Layout.ShortcutFrom, dungeon.Layout.ShortcutTo })
                if (stone is (int sx, int sy) && Seen(sx, sy))
                    Marker(area, window, TileMap.Center(sx, sy), Shapes.Circle, dungeon.Has(DungeonFlags.ShortcutOpen) ? new Color(0.43f, 0.75f, 1f) : new Color(0.43f, 0.75f, 1f, 0.4f), MarkerSize * 1.2f);
        }

        // A room is on the map once any tile of its box was seen.
        bool SeenRoom(DungeonRoom room)
        {
            var (l, b, w, h) = room.Box;
            for (int y = b; y < b + h; y += 2)
                for (int x = l; x < l + w; x += 2)
                    if (Seen(x, y)) return true;
            return false;
        }

        static Sprite _diamond;

        // A small diamond, drawn in pixels: a dungeon's gate or door out on the map.
        static Sprite Diamond
        {
            get
            {
                if (_diamond != null) return _diamond;
                string[] rows = { "...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..." };
                var texture = new Texture2D(7, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                        texture.SetPixel(x, 6 - y, rows[y][x] == '#' ? Color.white : Color.clear);
                texture.Apply();
                return _diamond = Sprite.Create(texture, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 7);
            }
        }

        static Sprite _skull;

        // A small skull, drawn in pixels: an elite camp on the map.
        static Sprite Skull
        {
            get
            {
                if (_skull != null) return _skull;
                string[] rows = { ".#####.", "#######", "#..#..#", "#######", ".##.##.", ".#.#.#.", "......." };
                var texture = new Texture2D(7, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                        texture.SetPixel(x, 6 - y, rows[y][x] == '#' ? Color.white : Color.clear);
                texture.Apply();
                return _skull = Sprite.Create(texture, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 7);
            }
        }

        // Where the story leads: a ring on the spot once it is known, else (on the full map) a faint circle about it.
        void Leads(Rect area, Rect window, PlaceSite site, Color color, float size, bool full)
        {
            var known = KnowledgeOf(site);
            if (known == WayKnown.Known)
            {
                Marker(area, window, site.Center, Shapes.Ring, color, size);
                return;
            }
            if (!full) return;
            float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
            Faint(area, window, Rumors.Circle(_client.Seed, RumorKind.Arena, 1000 + site.Definition.Index, site.Center, radius), radius, color, 1);
        }

        static readonly Color WaypointOpen = new Color(0.5f, 0.78f, 1f);
        static readonly Color PlaceMark = new Color(0.7f, 0.7f, 0.7f);

        static void Marker(Rect area, Rect window, Vec2 world, Sprite sprite, Color color, float size)
        {
            float u = (world.X - window.x) / window.width;
            float v = (world.Y - window.y) / window.height;
            if (u < 0 || u > 1 || v < 0 || v > 1) return;
            var center = new Vector2(area.x + u * area.width, area.yMax - v * area.height);
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), sprite.texture);
            GUI.color = previous;
        }

        // The pixel-art panel (UiSkin) around the map, and the dark under its unexplored tiles.
        static void Framed(Rect area)
        {
            float frame = UiSkin.FrameEdge;
            UiSkin.Panel(new Rect(area.x - frame, area.y - frame, area.width + frame * 2, area.height + frame * 2));
            Fill(area, Backdrop);
        }

        static void Fill(Rect area, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void Reveal(int cx, int cy)
        {
            for (int y = cy - RevealRadius; y <= cy + RevealRadius; y++)
            {
                for (int x = cx - RevealRadius; x <= cx + RevealRadius; x++)
                {
                    if (!_map.InBounds(x, y)) continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy > RevealRadius * RevealRadius) continue;
                    int index = y * _map.Width + x;
                    if (_explored[index] || _buried.Count > 0 && Shut(x, y)) continue;
                    _explored[index] = true;
                    Paint(x, y);
                    _dirty = true;
                    if (!_map.InPocket(y)) Changed = true;
                }
            }
        }

        // ---- The full map's own colours (U5) ----

        // The full map is drawn on parchment, as a map is inked (MapView.Full.cs): the walls dark ink, the floors pale with
        // their biome's tint, the rest (ore, water, lava, roads, machines) as on the minimap; the unexplored left as the
        // bare sheet. Kept beside the minimap's colours and sent to its texture only while the full map is up.
        readonly Color32[] _sheetPixels;
        Texture2D _sheet;
        bool _sheetDirty = true;
        static readonly Color32 SheetInk = new Color32(72, 54, 38, 255);
        static readonly Color32 SheetPaper = new Color32(236, 222, 186, 255);

        void Paint(int x, int y)
        {
            int index = y * _map.Width + x;
            var c = TileColor(x, y);
            _pixels[index] = c;
            _sheetPixels[index] = SheetColor(x, y, c);
            _sheetDirty = true;
        }

        Color32 SheetColor(int x, int y, Color32 c)
        {
            static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
            if (Same(c, Wall)) return SheetInk;
            if (Same(c, ArenaFloor) || Same(c, BiomeFloor(_client.Biomes[x, y])) || Same(c, Floor)) return Color32.Lerp(c, SheetPaper, 0.62f);
            return c;
        }

        // The full map's texture, brought up to date.
        Texture2D Sheet()
        {
            if (_sheet == null)
            {
                _sheet = new Texture2D(_map.Width, _map.Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                _sheetDirty = true;
            }
            if (_sheetDirty)
            {
                _sheet.SetPixels32(_sheetPixels);
                _sheet.Apply();
                _sheetDirty = false;
            }
            return _sheet;
        }

        Color32 TileColor(int x, int y)
        {
            if (_map[x, y] == TileType.CopperOre) return Palette.CopperOre;
            if (_map[x, y] == TileType.IronOre) return Palette.IronOre;
            if (_map[x, y] == TileType.Workbench) return Palette.Workbench;
            if (_map[x, y] == TileType.Furnace) return Palette.Furnace;
            if (_map[x, y] == TileType.Chest) return Palette.Chest;
            if (_map[x, y] == TileType.Drill) return Palette.Drill;
            if (World.IsSmelter(_map[x, y])) return Palette.Smelter;
            if (World.IsBelt(_map[x, y])) return Palette.Conveyor;
            if (_map[x, y] == TileType.Splitter) return Palette.Splitter;
            if (_map[x, y] == TileType.Arm) return Palette.Arm;
            if (_map[x, y] == TileType.Torch || _map[x, y] == TileType.Lamp) return Palette.Torch;
            if (_map[x, y] == TileType.MithrilOre) return Palette.MithrilOre;
            if (_map[x, y] == TileType.ObsidianOre) return new Color32(110, 70, 150, 255);
            if (_map[x, y] == TileType.VoidOre) return new Color32(180, 140, 255, 255);
            if (TileMap.IsPaving(_map[x, y])) return new Color32(150, 138, 120, 255);
            if (TileMap.IsDoor(_map[x, y])) return new Color32(170, 120, 70, 255);
            // The rooms' own tiles (Docs/WorldRework.md 방의 역할): a cracked wall shows as a paler wall, so a short cut
            // or a hidden room can be found on the map by one who looks.
            if (_map[x, y] == TileType.CrackedWall) return new Color32(132, 112, 92, 255);
            if (_map[x, y] == TileType.ObsidianWall) return new Color32(96, 60, 150, 255);
            if (_map[x, y] == TileType.VoidWall) return new Color32(170, 130, 245, 255);
            if (_map[x, y] == TileType.Grate) return new Color32(150, 160, 172, 255);
            // A camp's trapdoor (B1c): brown shut, the dark of its hole once open.
            if (_map[x, y] == TileType.Trapdoor) return new Color32(150, 104, 60, 255);
            if (_map[x, y] == TileType.OpenTrapdoor) return new Color32(60, 40, 26, 255);
            if (_map[x, y] >= TileType.WoodChest && _map[x, y] <= TileType.GoldChest) return new Color32(255, 214, 90, 255);
            if (_map[x, y] == TileType.OpenChest) return new Color32(150, 126, 80, 255);
            if (_map[x, y] == TileType.Waystone) return new Color32(110, 190, 255, 255);
            if (_map[x, y] == TileType.IceFloor) return new Color32(200, 232, 248, 255);
            if (_map[x, y] == TileType.Quicksand) return new Color32(196, 160, 84, 255);
            // The lake's shallows (B3b): a paler teal than its deep water.
            if (_map[x, y] == TileType.ShallowWater) return new Color32(92, 186, 176, 255);
            if (_map[x, y] == TileType.Road) return new Color32(176, 150, 112, 255);
            if (_map[x, y] == TileType.Dock) return new Color32(168, 116, 68, 255);
            if (_map[x, y] == TileType.Rail) return new Color32(120, 110, 100, 255);
            if (_map[x, y] == TileType.Waypoint) return new Color32(110, 170, 255, 255);
            // The dressing of the story's places (Docs/Story.md): walls by what they are made of, the rest warm.
            if (_map[x, y] == TileType.TimberWall) return new Color32(150, 104, 58, 255);
            if (_map[x, y] == TileType.CastleWall || _map[x, y] == TileType.Pillar) return new Color32(190, 186, 176, 255);
            if (_map[x, y] == TileType.Spire) return new Color32(120, 220, 236, 255);
            if (_map[x, y] == TileType.Campfire) return Palette.Torch;
            if (_map[x, y] == TileType.Cottage || _map[x, y] == TileType.Cabin) return new Color32(176, 72, 58, 255);
            if (_map[x, y] == TileType.Tower) return new Color32(74, 108, 192, 255);
            if (_map[x, y] == TileType.BigTent || _map[x, y] == TileType.Totem || _map[x, y] == TileType.Brazier) return new Color32(128, 40, 74, 255);
            // The biomes' props (묶음 B1a): a lit lamp as a fire; the rest a speck between the floor's colour and rock's, so
            // a room strewn with them does not read as walled off.
            if (_map[x, y] == TileType.LitLamp) return Palette.Torch;
            if (BiomeProps.Is(_map[x, y]) || BiomeLandmarks.Is(_map[x, y]) || BiomeProps.AnchorOf(_map, x, y) != null) return Color32.Lerp(BiomeFloor(_client.Biomes[x, y]), Wall, 0.4f);
            if (Structures.IsDecor(_map[x, y])) return new Color32(140, 120, 100, 255);
            if (_map[x, y] == TileType.Table || _map[x, y] == TileType.Banner || _map[x, y] == TileType.Pot) return new Color32(200, 160, 110, 255);
            if (_map[x, y] == TileType.Lava) return new Color32(255, 110, 30, 255);
            if (_map[x, y] == TileType.Water) return new Color32(50, 100, 170, 255);
            if (TileMap.IsSoil(_map[x, y])) return new Color32(110, 80, 56, 255);
            if (_map[x, y] == TileType.Filter) return Palette.Splitter;
            if (_map[x, y] == TileType.MerchantStall || _map[x, y] == TileType.SmithStall || _map[x, y] == TileType.FisherHut || _map[x, y] == TileType.AlchemyTable
                || _map[x, y] == TileType.RoyalStall || _map[x, y] == TileType.DemonStall || _map[x, y] == TileType.OreStall || _map[x, y] == TileType.WandererStall)
                return new Color32(220, 90, 100, 255);
            if (_map[x, y] == TileType.WardStone || _map[x, y] == TileType.TeslaTower) return new Color32(140, 224, 255, 255);
            if (World.IsPowerNode(_map[x, y]) || _map[x, y] == TileType.Lever) return Palette.Drill;
            if (_map[x, y] == TileType.Bed) return new Color32(80, 130, 220, 255);
            if (_map[x, y] == TileType.Sprinkler) return new Color32(90, 150, 230, 255);
            if (_map[x, y] == TileType.Harvester) return new Color32(110, 170, 80, 255);
            if (_map.IsSolid(x, y)) return Wall;
            return WorldView.InArena(_client, x, y) ? ArenaFloor : BiomeFloor(_client.Biomes[x, y]);
        }
    }
}
