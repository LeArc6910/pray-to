using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Draws what a WorldClient knows: the cave tiles, every entity as a tinted shape with a health bar, swings,
    // hit flashes and damage numbers. Pure presentation; nothing here changes the game.
    sealed class WorldView : IDisposable
    {
        sealed class Body
        {
            public Transform Root;
            public SpriteRenderer Shape;
            public SpriteRenderer Pointer;
            public SpriteRenderer Swing;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            public float Diameter;
            public float SwingTime;
            public float FlashTime;
            public Vector3 Position;
            public string Name;
        }

        sealed class Floating
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public float Age;
        }

        const float SwingSeconds = 0.15f;
        const float FlashSeconds = 0.12f;
        const float FloatSeconds = 0.8f;

        static readonly Color SelfColor = new Color(0.35f, 0.65f, 1f);
        static readonly Color AllyColor = new Color(0.4f, 0.9f, 0.55f);
        static readonly Color SlimeColor = new Color(0.55f, 0.85f, 0.3f);
        static readonly Color WindupColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color DeadColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);

        readonly WorldClient _client;
        readonly Camera _camera;
        readonly GameObject _root;
        readonly Body _self;
        readonly Dictionary<int, Body> _others = new Dictionary<int, Body>();
        readonly List<Floating> _floating = new List<Floating>();
        readonly HashSet<int> _seen = new HashSet<int>();
        GUIStyle _labelStyle;

        public WorldView(WorldClient client, Camera camera)
        {
            _client = client;
            _camera = camera;
            _root = new GameObject("World");
            BuildTiles(client.Map);
            AddLight();
            _self = MakeBody("나", World.PlayerRadius * 2, SelfColor);
            var start = client.Position;
            _camera.transform.position = new Vector3(start.X, start.Y, _camera.transform.position.z);
        }

        public void Render(float tickFraction, float deltaTime)
        {
            var selfPosition = Vec2.Lerp(_client.PreviousPosition, _client.Position, Mathf.Clamp01(tickFraction));
            Place(_self, selfPosition, _client.Facing, _client.Health, _client.MaxHealth, SelfColor, windup: false, deltaTime);
            _self.Name = _client.Name;

            _seen.Clear();
            foreach (var view in _client.Others())
            {
                _seen.Add(view.Id);
                if (!_others.TryGetValue(view.Id, out var body))
                {
                    float diameter = view.IsPlayer ? World.PlayerRadius * 2 : view.Monster.Radius * 2;
                    body = MakeBody(view.IsPlayer ? "player" : view.Monster.Id, diameter, view.IsPlayer ? AllyColor : SlimeColor);
                    _others[view.Id] = body;
                }
                body.Name = view.IsPlayer ? view.Name : null;
                Place(body, view.Position, view.Facing, view.Health, view.MaxHealth, view.IsPlayer ? AllyColor : SlimeColor, view.State == EnemyState.Windup, deltaTime);
            }
            var gone = new List<int>();
            foreach (var pair in _others)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Root.gameObject);
                gone.Add(pair.Key);
            }
            foreach (int id in gone) _others.Remove(id);

            foreach (var e in _client.TakeEvents()) Show(e);
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                _floating[i].Age += deltaTime;
                if (_floating[i].Age > FloatSeconds) _floating.RemoveAt(i);
            }

            var target = new Vector3(selfPosition.X, selfPosition.Y, _camera.transform.position.z);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, target, 1 - Mathf.Exp(-12 * deltaTime));
        }

        // Names over players and damage numbers. Call from OnGUI.
        public void DrawGui()
        {
            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15,
            };

            Label(_self.Position + new Vector3(0, 0.75f, 0), _self.Name, Color.white);
            foreach (var body in _others.Values)
            {
                if (!string.IsNullOrEmpty(body.Name)) Label(body.Position + new Vector3(0, 0.75f, 0), body.Name, Color.white);
            }
            foreach (var text in _floating)
            {
                float t = text.Age / FloatSeconds;
                var color = text.Color;
                color.a = 1 - t * t;
                Label(text.Position + new Vector3(0, 0.6f + t * 0.8f, 0), text.Text, color);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }

        void Show(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Attack:
                    var attacker = Find(e.Source);
                    if (attacker == null) break;
                    attacker.SwingTime = SwingSeconds;
                    attacker.Swing.transform.localRotation = Quaternion.Euler(0, 0, e.Direction.Angle * Mathf.Rad2Deg);
                    break;
                case SimEventType.Hit:
                    var target = Find(e.Target);
                    if (target == null) break;
                    target.FlashTime = FlashSeconds;
                    bool hurtsMe = e.Target == _client.PlayerId;
                    var color = hurtsMe ? new Color(1f, 0.35f, 0.3f) : e.Critical ? new Color(1f, 0.85f, 0.2f) : Color.white;
                    _floating.Add(new Floating { Position = target.Position, Text = e.Critical ? e.Amount + "!" : e.Amount.ToString(), Color = color });
                    break;
                case SimEventType.Died:
                    if (e.Source == _client.PlayerId) _floating.Add(new Floating { Position = _self.Position, Text = "쓰러짐", Color = Color.gray });
                    break;
            }
        }

        Body Find(int id) => id == _client.PlayerId ? _self : _others.TryGetValue(id, out var body) ? body : null;

        void Place(Body body, Vec2 position, Vec2 facing, float health, float maxHealth, Color color, bool windup, float deltaTime)
        {
            body.Position = new Vector3(position.X, position.Y, 0);
            body.Root.localPosition = body.Position;
            int order = 10000 - Mathf.RoundToInt(position.Y * 10);

            bool dead = health <= 0;
            float pulse = windup ? 1 + 0.12f * Mathf.Sin(Time.time * 30) : 1;
            body.Shape.transform.localScale = Vector3.one * (body.Diameter * pulse);
            body.Shape.color = dead ? DeadColor : body.FlashTime > 0 ? Color.white : windup ? WindupColor : color;
            body.Shape.sortingOrder = order;

            body.Pointer.transform.localPosition = new Vector3(facing.X, facing.Y, 0) * (body.Diameter * 0.5f);
            body.Pointer.enabled = !dead;
            body.Pointer.sortingOrder = order + 1;

            body.Swing.enabled = body.SwingTime > 0;
            body.Swing.color = new Color(1, 1, 1, Mathf.Clamp01(body.SwingTime / SwingSeconds) * 0.6f);
            body.Swing.sortingOrder = order + 2;

            float share = maxHealth > 0 ? Mathf.Clamp01(health / maxHealth) : 0;
            bool showHealth = !dead && share < 0.999f;
            body.HealthBack.enabled = showHealth;
            body.HealthFill.enabled = showHealth;
            body.HealthFill.transform.localScale = new Vector3(0.8f * share, 0.08f, 1);
            body.HealthFill.transform.localPosition = new Vector3(-0.4f + 0.4f * share, body.Diameter * 0.5f + 0.15f, 0);
            body.HealthFill.color = share > 0.5f ? new Color(0.4f, 0.9f, 0.4f) : share > 0.25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.3f, 0.25f);
            body.HealthBack.sortingOrder = order + 3;
            body.HealthFill.sortingOrder = order + 4;

            body.SwingTime = Mathf.Max(0, body.SwingTime - deltaTime);
            body.FlashTime = Mathf.Max(0, body.FlashTime - deltaTime);
        }

        Body MakeBody(string name, float diameter, Color color)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_root.transform, false);
            var body = new Body
            {
                Root = root,
                Diameter = diameter,
                Shape = Renderer(root, "Shape", Shapes.Circle, color),
                Pointer = Renderer(root, "Pointer", Shapes.Circle, new Color(0.1f, 0.1f, 0.1f, 0.8f)),
                Swing = Renderer(root, "Swing", Shapes.Arc, Color.white),
                HealthBack = Renderer(root, "HealthBack", Shapes.Square, new Color(0, 0, 0, 0.6f)),
                HealthFill = Renderer(root, "HealthFill", Shapes.Square, Color.green),
            };
            body.Pointer.transform.localScale = Vector3.one * 0.18f;
            // The swing reaches MeleeReach past the edge of the body.
            body.Swing.transform.localScale = Vector3.one * (diameter + World.MeleeReach * 2);
            body.HealthBack.transform.localScale = new Vector3(0.84f, 0.12f, 1);
            body.HealthBack.transform.localPosition = new Vector3(0, diameter * 0.5f + 0.15f, 0);
            return body;
        }

        static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        void BuildTiles(TileMap map)
        {
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(_root.transform, false);
            var tilesObject = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
            tilesObject.transform.SetParent(grid.transform, false);

            var floor = Shapes.FloorTile();
            var wall = Shapes.WallTile();
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++) tiles[y * map.Width + x] = map.IsSolid(x, y) ? wall : floor;
            }
            tilesObject.GetComponent<Tilemap>().SetTilesBlock(new BoundsInt(0, 0, 0, map.Width, map.Height, 1), tiles);
            tilesObject.GetComponent<TilemapRenderer>().sortingOrder = -100;
        }

        // The project uses URP's 2D renderer, which draws sprites by 2D lights; one global light shows everything.
        // Darker caves with a light around each player can come later.
        void AddLight()
        {
            var lightObject = new GameObject("Global Light");
            lightObject.SetActive(false);
            lightObject.transform.SetParent(_root.transform, false);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1;
            lightObject.SetActive(true);
        }

        void Label(Vector3 world, string text, Color color)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0) return;
            var rect = new Rect(screen.x - 60, Screen.height - screen.y - 12, 120, 24);
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _labelStyle);
            GUI.color = color;
            GUI.Label(rect, text, _labelStyle);
            GUI.color = previous;
        }
    }
}
