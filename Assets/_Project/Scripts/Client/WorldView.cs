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
    // hit flashes, damage numbers, and in a boss fight the telegraphed areas, projectiles, screen shake and
    // banners. Pure presentation; nothing here changes the game.
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

        sealed class Mark
        {
            public Transform Root;
            public SpriteRenderer Edge;
            public SpriteRenderer Fill;
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
        const float BannerSeconds = 2.2f;
        // Impacts further away than this do not shake the screen.
        const float ShakeRange = 16;

        static readonly Color SelfColor = new Color(0.35f, 0.65f, 1f);
        static readonly Color AllyColor = new Color(0.4f, 0.9f, 0.55f);
        static readonly Color SlimeColor = new Color(0.55f, 0.85f, 0.3f);
        static readonly Color BossColor = new Color(0.62f, 0.56f, 0.5f);
        static readonly Color WindupColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color ChargeColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color DeadColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color PathColor = new Color(1f, 0.85f, 0.3f);
        static readonly Color ProjectileColor = new Color(1f, 0.6f, 0.25f);

        readonly WorldClient _client;
        readonly Camera _camera;
        readonly GameObject _root;
        readonly Body _self;
        readonly Dictionary<int, Body> _others = new Dictionary<int, Body>();
        readonly Dictionary<int, Mark> _marks = new Dictionary<int, Mark>();
        readonly Dictionary<int, SpriteRenderer> _projectiles = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, (SpriteRenderer Renderer, string Name, Color Color)> _drops =
            new Dictionary<int, (SpriteRenderer, string, Color)>();
        readonly Dictionary<int, (BossState State, int Phase, string Name)> _bossesSeen = new Dictionary<int, (BossState, int, string)>();
        readonly List<Floating> _floating = new List<Floating>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _gone = new List<int>();
        Vector3 _cameraTarget;
        float _shake;
        string _banner = "";
        float _bannerAge = BannerSeconds;
        GUIStyle _labelStyle;
        GUIStyle _bannerStyle;

        public WorldView(WorldClient client, Camera camera)
        {
            _client = client;
            _camera = camera;
            _root = new GameObject("World");
            BuildTiles(client);
            AddLight();
            _self = MakeBody("나", World.PlayerRadius * 2, SelfColor);
            var start = client.Position;
            _cameraTarget = new Vector3(start.X, start.Y, _camera.transform.position.z);
            _camera.transform.position = _cameraTarget;
        }

        // The awake boss on screen, for the health bar at the top.
        public EntityView? ActiveBoss { get; private set; }

        public void Render(float tickFraction, float deltaTime)
        {
            var selfPosition = Vec2.Lerp(_client.PreviousPosition, _client.Position, Mathf.Clamp01(tickFraction));
            Place(_self, selfPosition, _client.Facing, _client.Health, _client.MaxHealth, SelfColor, deltaTime);
            _self.Name = _client.Name;

            DrawEntities(deltaTime);
            DrawMarks();
            DrawProjectiles();

            foreach (var e in _client.TakeEvents()) Show(e);
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                _floating[i].Age += deltaTime;
                if (_floating[i].Age > FloatSeconds) _floating.RemoveAt(i);
            }
            _bannerAge += deltaTime;

            _cameraTarget = Vector3.Lerp(_cameraTarget, new Vector3(selfPosition.X, selfPosition.Y, _cameraTarget.z), 1 - Mathf.Exp(-12 * deltaTime));
            _shake *= Mathf.Exp(-10 * deltaTime);
            var jolt = (Vector3)(UnityEngine.Random.insideUnitCircle * _shake);
            _camera.transform.position = _cameraTarget + jolt;
        }

        // Names, damage numbers and banners. Call from OnGUI.
        public void DrawGui()
        {
            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15,
            };
            _bannerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 34,
            };

            Label(_self.Position + new Vector3(0, 0.75f, 0), _self.Name, Color.white);
            foreach (var body in _others.Values)
            {
                if (!string.IsNullOrEmpty(body.Name)) Label(body.Position + new Vector3(0, body.Diameter * 0.5f + 0.4f, 0), body.Name, Color.white);
            }
            foreach (var drop in _drops.Values)
            {
                var position = drop.Renderer.transform.localPosition;
                if (Vector3.Distance(position, _self.Position) < 4) Label(position + new Vector3(0, 0.45f, 0), drop.Name, drop.Color);
            }
            foreach (var text in _floating)
            {
                float t = text.Age / FloatSeconds;
                var color = text.Color;
                color.a = 1 - t * t;
                Label(text.Position + new Vector3(0, 0.6f + t * 0.8f, 0), text.Text, color);
            }

            if (_bannerAge < BannerSeconds)
            {
                float fade = Mathf.Clamp01((BannerSeconds - _bannerAge) / 0.5f);
                var rect = new Rect(0, Ui.Height * 0.25f, Ui.Width, 60);
                Shadowed(rect, _banner, new Color(1f, 0.85f, 0.4f, fade), _bannerStyle);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }

        void DrawEntities(float deltaTime)
        {
            ActiveBoss = null;
            _seen.Clear();
            foreach (var view in _client.Others())
            {
                _seen.Add(view.Id);
                if (view.Kind == EntityKind.Drop)
                {
                    DrawDrop(view);
                    continue;
                }
                if (!_others.TryGetValue(view.Id, out var body))
                {
                    string name = view.Kind == EntityKind.Boss ? view.Boss.Id : view.Kind == EntityKind.Monster ? view.Monster.Id : "player";
                    body = MakeBody(name, view.Radius * 2, ColorOf(view));
                    _others[view.Id] = body;
                }
                body.Name = view.IsPlayer ? view.Name : view.Kind == EntityKind.Boss && view.BossState != BossState.Dormant ? view.Boss.Name : null;

                var color = ColorOf(view);
                float pulse = 0;
                if (view.Kind == EntityKind.Monster && view.EnemyState == EnemyState.Windup)
                {
                    color = WindupColor;
                    pulse = 0.12f;
                }
                if (view.Kind == EntityKind.Boss)
                {
                    WatchBoss(view);
                    switch (view.BossState)
                    {
                        case BossState.Dormant:
                            color = Color.Lerp(BossColor, Color.black, 0.4f);
                            break;
                        case BossState.Telegraph:
                            color = Color.Lerp(BossColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 20));
                            break;
                        case BossState.Charge:
                            color = ChargeColor;
                            break;
                        case BossState.Roar:
                            color = WindupColor;
                            pulse = 0.08f;
                            break;
                    }
                    if (view.BossState != BossState.Dormant) ActiveBoss = view;
                }
                Place(body, view.Position, view.Facing, view.Health, view.MaxHealth, color, deltaTime, pulse, showHealth: view.Kind != EntityKind.Boss);
            }

            _gone.Clear();
            foreach (var pair in _others)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Root.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone) _others.Remove(id);

            _gone.Clear();
            foreach (var pair in _drops)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Renderer.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone) _drops.Remove(id);
        }

        // Loot is a small diamond in its rarity's colour, or an orange dot for a rune.
        void DrawDrop(EntityView view)
        {
            if (!_drops.ContainsKey(view.Id))
            {
                bool rune = view.DropRune != null;
                var color = rune ? Palette.Rune : Palette.Of(view.DropRarity);
                var renderer = Renderer(_root.transform, "Drop", rune ? Shapes.Circle : Shapes.Square, color);
                renderer.transform.localScale = Vector3.one * (rune ? 0.32f : 0.3f);
                renderer.transform.localRotation = Quaternion.Euler(0, 0, rune ? 0 : 45);
                renderer.transform.localPosition = new Vector3(view.Position.X, view.Position.Y, 0);
                renderer.sortingOrder = -10;
                _drops[view.Id] = (renderer, rune ? view.DropRune.Name : view.DropBase.Name, color);
            }
        }

        static Color ColorOf(EntityView view) =>
            view.Kind == EntityKind.Boss ? BossColor : view.Kind == EntityKind.Monster ? SlimeColor : AllyColor;

        // Banners for a boss waking up and changing phase.
        void WatchBoss(EntityView view)
        {
            bool known = _bossesSeen.TryGetValue(view.Id, out var before);
            if (view.BossState != BossState.Dormant && (!known || before.State == BossState.Dormant)) ShowBanner($"{view.Boss.Name} 등장!");
            else if (known && view.Phase > before.Phase) ShowBanner($"{view.Phase}페이즈!");
            _bossesSeen[view.Id] = (view.BossState, view.Phase, view.Boss.Name);
        }

        void ShowBanner(string text)
        {
            _banner = text;
            _bannerAge = 0;
        }

        // Telegraphs: a faint outline of the whole area, filled in as the moment it lands comes closer.
        void DrawMarks()
        {
            _seen.Clear();
            foreach (var view in _client.Hazards())
            {
                var hazard = view.State;
                _seen.Add(hazard.Id);
                if (!_marks.TryGetValue(hazard.Id, out var mark))
                {
                    mark = MakeMark(hazard);
                    _marks[hazard.Id] = mark;
                }

                var color = hazard.Harmful ? DangerColor : PathColor;
                mark.Edge.color = new Color(color.r, color.g, color.b, 0.22f);
                mark.Fill.color = new Color(color.r, color.g, color.b, 0.25f + 0.3f * view.Progress);
                switch (hazard.Shape)
                {
                    case HazardShape.Circle:
                        mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2 * view.Progress);
                        break;
                    case HazardShape.Cone:
                        mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2 * view.Progress);
                        break;
                    default:
                        mark.Fill.transform.localScale = new Vector3(hazard.Size * view.Progress, hazard.Width, 1);
                        mark.Fill.transform.localPosition = new Vector3(hazard.Size * view.Progress * 0.5f, 0, 0);
                        break;
                }
            }

            _gone.Clear();
            foreach (var pair in _marks)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Root.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone) _marks.Remove(id);
        }

        Mark MakeMark(HazardState hazard)
        {
            var root = new GameObject("Telegraph").transform;
            root.SetParent(_root.transform, false);
            root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            root.localRotation = Quaternion.Euler(0, 0, hazard.Direction.Angle * Mathf.Rad2Deg);

            Sprite edge;
            Sprite fill;
            switch (hazard.Shape)
            {
                case HazardShape.Circle:
                    edge = Shapes.Circle;
                    fill = Shapes.Circle;
                    break;
                case HazardShape.Cone:
                    edge = Shapes.Wedge(hazard.Width);
                    fill = edge;
                    break;
                default:
                    edge = Shapes.Square;
                    fill = Shapes.Square;
                    break;
            }

            var mark = new Mark
            {
                Root = root,
                Edge = Renderer(root, "Edge", edge, Color.clear),
                Fill = Renderer(root, "Fill", fill, Color.clear),
            };
            mark.Edge.sortingOrder = -50;
            mark.Fill.sortingOrder = -49;
            if (hazard.Shape == HazardShape.Line)
            {
                mark.Edge.transform.localScale = new Vector3(hazard.Size, hazard.Width, 1);
                mark.Edge.transform.localPosition = new Vector3(hazard.Size * 0.5f, 0, 0);
            }
            else
            {
                mark.Edge.transform.localScale = Vector3.one * (hazard.Size * 2);
            }
            return mark;
        }

        void DrawProjectiles()
        {
            _seen.Clear();
            foreach (var projectile in _client.Projectiles())
            {
                _seen.Add(projectile.Id);
                if (!_projectiles.TryGetValue(projectile.Id, out var renderer))
                {
                    renderer = Renderer(_root.transform, "Projectile", Shapes.Circle, ProjectileColor);
                    renderer.transform.localScale = Vector3.one * (projectile.Radius * 2);
                    renderer.sortingOrder = 20000;
                    _projectiles[projectile.Id] = renderer;
                }
                renderer.transform.localPosition = new Vector3(projectile.Position.X, projectile.Position.Y, 0);
            }

            _gone.Clear();
            foreach (var pair in _projectiles)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone) _projectiles.Remove(id);
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
                    if (hurtsMe) _shake = Mathf.Max(_shake, 0.12f);
                    break;
                case SimEventType.Impact:
                    var source = Find(e.Source);
                    if (source == null || Vector3.Distance(source.Position, _self.Position) > ShakeRange) break;
                    _shake = Mathf.Max(_shake, Mathf.Min(0.45f, e.Amount * 0.07f));
                    break;
                case SimEventType.Died:
                    if (e.Source == _client.PlayerId) _floating.Add(new Floating { Position = _self.Position, Text = "쓰러짐", Color = Color.gray });
                    if (_bossesSeen.TryGetValue(e.Source, out var boss))
                    {
                        ShowBanner($"{boss.Name} 처치!");
                        _bossesSeen.Remove(e.Source);
                    }
                    break;
            }
        }

        Body Find(int id) => id == _client.PlayerId ? _self : _others.TryGetValue(id, out var body) ? body : null;

        void Place(Body body, Vec2 position, Vec2 facing, float health, float maxHealth, Color color, float deltaTime, float pulse = 0, bool showHealth = true)
        {
            body.Position = new Vector3(position.X, position.Y, 0);
            body.Root.localPosition = body.Position;
            int order = 10000 - Mathf.RoundToInt(position.Y * 10);

            bool dead = health <= 0;
            float scale = 1 + pulse * Mathf.Sin(Time.time * 30);
            body.Shape.transform.localScale = Vector3.one * (body.Diameter * scale);
            body.Shape.color = dead ? DeadColor : body.FlashTime > 0 ? Color.white : color;
            body.Shape.sortingOrder = order;

            body.Pointer.transform.localPosition = new Vector3(facing.X, facing.Y, 0) * (body.Diameter * 0.5f);
            body.Pointer.enabled = !dead;
            body.Pointer.sortingOrder = order + 1;

            body.Swing.enabled = body.SwingTime > 0;
            body.Swing.color = new Color(1, 1, 1, Mathf.Clamp01(body.SwingTime / SwingSeconds) * 0.6f);
            body.Swing.sortingOrder = order + 2;

            float share = maxHealth > 0 ? Mathf.Clamp01(health / maxHealth) : 0;
            bool barVisible = showHealth && !dead && share < 0.999f;
            body.HealthBack.enabled = barVisible;
            body.HealthFill.enabled = barVisible;
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
            body.Pointer.transform.localScale = Vector3.one * Mathf.Max(0.18f, diameter * 0.15f);
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

        void BuildTiles(WorldClient client)
        {
            var map = client.Map;
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(_root.transform, false);
            var tilesObject = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
            tilesObject.transform.SetParent(grid.transform, false);

            var floor = Shapes.FloorTile();
            var arena = Shapes.ArenaTile();
            var wall = Shapes.WallTile();
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    bool inArena = Vec2.Distance(TileMap.Center(x, y), client.Arena) <= client.ArenaRadius;
                    tiles[y * map.Width + x] = map.IsSolid(x, y) ? wall : inArena ? arena : floor;
                }
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
            Vector2 point = Ui.FromScreen(screen);
            Shadowed(new Rect(point.x - 80, point.y - 12, 160, 24), text, color, _labelStyle);
        }

        static void Shadowed(Rect rect, string text, Color color, GUIStyle style)
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }
    }
}
