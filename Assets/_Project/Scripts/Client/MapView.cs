using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // The minimap in the top right corner and the full map it opens. Only tiles that have come within a few tiles of
    // the local player are drawn, so the map fills in as the caves are explored; the boss arena is always marked, as
    // the HUD already points to it. What has been explored lives here, in this session only: it is a view, not game
    // state, and is not saved or shared.
    sealed class MapView : IDisposable
    {
        // Matches what the camera shows around the player.
        const int RevealRadius = 8;
        // Tiles across the minimap.
        const int MinimapTiles = 40;
        const float MarkerSize = 7;

        static readonly Color32 Floor = new Color32(96, 80, 66, 255);
        static readonly Color32 Wall = new Color32(158, 140, 118, 255);
        static readonly Color32 ArenaFloor = new Color32(120, 52, 46, 255);
        static readonly Color Backdrop = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        static readonly Color OtherPlayer = new Color(0.45f, 0.7f, 1f);
        static readonly Color ArenaMark = new Color(1f, 0.35f, 0.3f);

        readonly WorldClient _client;
        readonly TileMap _map;
        readonly Texture2D _texture;
        readonly Color32[] _pixels;
        readonly bool[] _explored;
        readonly List<Vec2> _others = new List<Vec2>();
        int _revealedX = int.MinValue;
        int _revealedY = int.MinValue;
        bool _dirty;

        public MapView(WorldClient client)
        {
            _client = client;
            _map = client.Map;
            _pixels = new Color32[_map.Width * _map.Height];
            _explored = new bool[_pixels.Length];
            _texture = new Texture2D(_map.Width, _map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Every pixel starts transparent: unexplored.
            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        // The full map is showing. Touches and attacks are held back meanwhile.
        public bool FullOpen { get; set; }

        public void Dispose() => Object.Destroy(_texture);

        // Redraws dug or built tiles that have already been explored.
        public void UpdateTiles(IEnumerable<TileChange> changes)
        {
            foreach (var change in changes)
            {
                int index = change.Y * _map.Width + change.X;
                if (!_explored[index]) continue;
                _pixels[index] = TileColor(change.X, change.Y);
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
            Fill(area, Backdrop);
            float half = MinimapTiles / 2f;
            // Keep the window inside the map so the edges never smear.
            float left = Mathf.Clamp(_client.Position.X - half, 0, Mathf.Max(0, _map.Width - MinimapTiles));
            float bottom = Mathf.Clamp(_client.Position.Y - half, 0, Mathf.Max(0, _map.Height - MinimapTiles));
            var window = new Rect(left, bottom, MinimapTiles, MinimapTiles);
            DrawTiles(area, window);
            if (GUI.Button(area, GUIContent.none, GUIStyle.none)) FullOpen = true;
        }

        // The whole map over the game. Tapping or clicking anywhere closes it.
        public void DrawFull()
        {
            var screen = new Rect(0, 0, Ui.Width, Ui.Height);
            Controls.Block(screen);
            Fill(screen, new Color(0, 0, 0, 0.6f));
            float size = Mathf.Min(Ui.Width, Ui.Height - 60) - 20;
            var area = new Rect((Ui.Width - size) / 2, 34, size, size);
            Fill(area, Backdrop);
            DrawTiles(area, new Rect(0, 0, _map.Width, _map.Height));

            string close = Controls.UsingTouch ? "지도 (탭하면 닫힘)" : "지도 (클릭하거나 M·Esc로 닫기)";
            GUI.Label(new Rect(area.x, 8, area.width, 24), close);
            GUI.Label(new Rect(area.x, area.yMax + 4, area.width, 24), "● 나   ● 다른 플레이어   ○ 보스 아레나");
            if (GUI.Button(screen, GUIContent.none, GUIStyle.none)) FullOpen = false;
        }

        // Draws the tiles of the given window of the map (in tiles, y up) into a GUI area, then the markers.
        void DrawTiles(Rect area, Rect window)
        {
            var uv = new Rect(window.x / _map.Width, window.y / _map.Height, window.width / _map.Width, window.height / _map.Height);
            GUI.DrawTextureWithTexCoords(area, _texture, uv);

            Marker(area, window, _client.Arena, Shapes.Ring, ArenaMark, MarkerSize * 2.2f);
            foreach (var position in _others) Marker(area, window, position, Shapes.Circle, OtherPlayer, MarkerSize);
            Marker(area, window, _client.Position, Shapes.Circle, Color.white, MarkerSize);
        }

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
                    if (_explored[index]) continue;
                    _explored[index] = true;
                    _pixels[index] = TileColor(x, y);
                    _dirty = true;
                }
            }
        }

        Color32 TileColor(int x, int y)
        {
            if (_map[x, y] == TileType.CopperOre) return Palette.CopperOre;
            if (_map[x, y] == TileType.IronOre) return Palette.IronOre;
            if (_map.IsSolid(x, y)) return Wall;
            return Vec2.Distance(TileMap.Center(x, y), _client.Arena) <= _client.ArenaRadius ? ArenaFloor : Floor;
        }
    }
}
