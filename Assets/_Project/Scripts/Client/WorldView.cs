using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Draws what a WorldClient knows: the cave tiles, every entity as a pixel-art sprite (Art/) with a shadow and a
    // health bar, swings, hit flashes, damage numbers, skills (rings, bursts, buff auras, coloured by their tags),
    // the dark of the caves and the lights in it, and in a boss fight the telegraphed areas, projectiles, screen
    // shake and banners. Pure presentation; nothing here changes the game.
    sealed class WorldView : IDisposable
    {
        enum BodyKind
        {
            Player,
            Monster,
            Boss,
        }

        sealed class Body
        {
            public Transform Root;
            public SpriteRenderer Shape;
            // The same sprite in white over the shape, for flashes: hit, winding up, charging.
            public SpriteRenderer Glow;
            public SpriteRenderer Shadow;
            public SpriteRenderer Swing;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            // A ring while a buff runs, in the buff's colour.
            public SpriteRenderer Aura;
            public BodyKind Kind;
            public string Id;
            // A player's tunic, a monster's sheet colour, the boss's stone.
            public Rgba Color;
            public int Seed;
            public float Diameter;
            // How far below its centre it stands, and how far above its centre the top of its sprite is.
            public float Feet;
            public float Top;
            public float SwingTime;
            public float FlashTime;
            public Vector3 Position;
            public Vector3 LastPosition;
            public float Walked;
            public int Frame = -1;
            public string Name;
        }

        sealed class Drop
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Glow;
            public Vector3 Position;
            public string Name;
            public Color Color;
        }

        sealed class Mark
        {
            public Transform Root;
            public SpriteRenderer Edge;
            public SpriteRenderer Fill;
        }

        // A skill's ring or burst, growing and fading out where it happened.
        sealed class Flash
        {
            public SpriteRenderer Renderer;
            public float Size;
            public float Age;
            public float Life;
            public Color Color;
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
        const float FlashLife = 0.35f;
        // Impacts further away than this do not shake the screen.
        const float ShakeRange = 16;
        // The dark is a square of this many tiles around the camera, wider than any screen shows. It covers what
        // stands in the world; projectiles, telegraphs and skill effects are drawn over it so they show in the dark.
        const int DarkSize = 64;
        const int DarkOrder = 15000;
        // How far a glowing shot lights its way.
        const float ShotLight = 2.2f;

        // Tunics: blue for you, and a colour per player for the others so they can be told apart.
        static readonly Rgba SelfTunic = Rgba.Hex("#3F7FD8");
        static readonly Rgba[] OtherTunics = { Rgba.Hex("#4DB06A"), Rgba.Hex("#E08A3C"), Rgba.Hex("#A070DC"), Rgba.Hex("#3CB8B0") };
        static readonly Rgba SlimeColor = Rgba.Hex("#8CD94D");
        static readonly Rgba BossColor = new Rgba(158, 143, 128);
        static readonly Color DormantTint = new Color(0.6f, 0.6f, 0.62f);
        static readonly Color WindupColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color ChargeColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color DeadColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color PathColor = new Color(1f, 0.85f, 0.3f);
        static readonly Color ProjectileColor = new Color(1f, 0.6f, 0.25f);
        static readonly Color HitFlash = new Color(1, 1, 1, 0.85f);

        readonly WorldClient _client;
        readonly Camera _camera;
        readonly GameObject _root;
        readonly Body _self;
        readonly Dictionary<int, Body> _others = new Dictionary<int, Body>();
        readonly Dictionary<int, Mark> _marks = new Dictionary<int, Mark>();
        readonly Dictionary<int, SpriteRenderer> _projectiles = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, Drop> _drops = new Dictionary<int, Drop>();
        readonly Dictionary<int, (BossState State, int Phase, string Name)> _bossesSeen = new Dictionary<int, (BossState, int, string)>();
        readonly List<Floating> _floating = new List<Floating>();
        readonly List<Flash> _flashes = new List<Flash>();
        // Which skill slots of each other player have a buff running, from their snapshot state.
        readonly Dictionary<int, byte> _buffBits = new Dictionary<int, byte>();
        readonly Dictionary<int, SpriteRenderer> _crops = new Dictionary<int, SpriteRenderer>();
        // Per machine tile: the part that shows which way it faces (a drill's bit, a smelter's arrow, a conveyor's
        // moving belt), and the icon of what a conveyor carries.
        readonly Dictionary<int, (TileType Kind, SpriteRenderer Part, SpriteRenderer Load)> _machines =
            new Dictionary<int, (TileType, SpriteRenderer, SpriteRenderer)>();
        Tilemap _ground;
        Tilemap _objects;
        readonly LightMap _lightMap = new LightMap(DarkSize);
        readonly List<LightSource> _lights = new List<LightSource>();
        readonly WorldLights _worldLights;
        readonly Func<int, int, float> _arenaGlow;
        Texture2D _darkTexture;
        Color32[] _darkPixels;
        SpriteRenderer _dark;
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
            _worldLights = new WorldLights(client.Tiles);
            _arenaGlow = WorldLights.ArenaAmbient(client.Arena, client.ArenaRadius);
            BuildDark();
            _self = MakeBody(BodyKind.Player, "나", 0, World.PlayerRadius, SelfTunic);
            var start = client.Position;
            _cameraTarget = new Vector3(start.X, start.Y, _camera.transform.position.z);
            _camera.transform.position = _cameraTarget;
        }

        // The awake boss on screen, for the health bar at the top.
        public EntityView? ActiveBoss { get; private set; }

        public void Render(float tickFraction, float deltaTime)
        {
            var selfPosition = Vec2.Lerp(_client.PreviousPosition, _client.Position, Mathf.Clamp01(tickFraction));
            Place(_self, selfPosition, _client.Facing, _client.Health, _client.MaxHealth, Color.white, Color.clear, deltaTime);
            _self.Name = _client.Name;
            int selfBuffs = 0;
            for (int slot = 0; slot < _client.Skills.Count; slot++)
            {
                if (_client.BuffLeft(slot) > 0) selfBuffs |= 1 << slot;
            }
            ShowAura(_self, _client.PlayerId, selfBuffs);

            DrawEntities(deltaTime);
            DrawCrops();
            DrawMachines();
            DrawMarks();
            DrawProjectiles();

            foreach (var e in _client.TakeEvents()) Show(e);
            UpdateFlashes(deltaTime);
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
            UpdateDark(selfPosition);
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

            Label(_self.Position + new Vector3(0, _self.Top + 0.4f, 0), _self.Name, Color.white);
            foreach (var body in _others.Values)
            {
                if (!string.IsNullOrEmpty(body.Name)) Label(body.Position + new Vector3(0, body.Top + 0.4f, 0), body.Name, Color.white);
            }
            foreach (var drop in _drops.Values)
            {
                if (Vector3.Distance(drop.Position, _self.Position) < 4) Label(drop.Position + new Vector3(0, 0.5f, 0), drop.Name, drop.Color);
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
            if (_darkTexture != null) Object.Destroy(_darkTexture);
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
                    body = view.Kind == EntityKind.Boss ? MakeBody(BodyKind.Boss, view.Boss.Id, view.Id, view.Radius, BossColor)
                        : view.Kind == EntityKind.Monster ? MakeBody(BodyKind.Monster, view.Monster.Id, view.Id, view.Radius, ColorOf(view.Monster))
                        : MakeBody(BodyKind.Player, "player", view.Id, view.Radius, OtherTunics[view.Id % OtherTunics.Length]);
                    _others[view.Id] = body;
                }
                body.Name = view.IsPlayer ? view.Name : view.Kind == EntityKind.Boss && view.BossState != BossState.Dormant ? view.Boss.Name : null;

                var tint = Color.white;
                var glow = Color.clear;
                float pulse = 0;
                if (view.Kind == EntityKind.Monster && view.EnemyState == EnemyState.Windup)
                {
                    glow = Faded(WindupColor, 0.35f + 0.25f * Mathf.Sin(Time.time * 30));
                    pulse = 0.06f;
                }
                if (view.Kind == EntityKind.Monster && view.EnemyState == EnemyState.Charge) glow = Faded(ChargeColor, 0.45f);
                if (view.Kind == EntityKind.Boss)
                {
                    WatchBoss(view);
                    switch (view.BossState)
                    {
                        case BossState.Dormant:
                            tint = DormantTint;
                            break;
                        case BossState.Telegraph:
                            glow = Faded(Color.white, 0.25f + 0.2f * Mathf.Sin(Time.time * 20));
                            break;
                        case BossState.Charge:
                            glow = Faded(ChargeColor, 0.5f);
                            break;
                        case BossState.Roar:
                            glow = Faded(WindupColor, 0.45f);
                            pulse = 0.05f;
                            break;
                    }
                    if (view.BossState != BossState.Dormant) ActiveBoss = view;
                }
                Place(body, view.Position, view.Facing, view.Health, view.MaxHealth, tint, glow, deltaTime, pulse, showHealth: view.Kind != EntityKind.Boss);
                if (view.IsPlayer)
                {
                    _buffBits[view.Id] = view.State;
                    ShowAura(body, view.Id, view.State);
                }
            }

            _gone.Clear();
            foreach (var pair in _others)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Root.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone)
            {
                _others.Remove(id);
                _buffBits.Remove(id);
            }

            _gone.Clear();
            foreach (var pair in _drops)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Renderer.gameObject);
                if (pair.Value.Glow != null) Object.Destroy(pair.Value.Glow.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int id in _gone) _drops.Remove(id);
        }

        static Color Faded(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // Loot is its icon, bobbing a little; gear better than common has a glow in its rarity's colour under it.
        void DrawDrop(EntityView view)
        {
            if (!_drops.TryGetValue(view.Id, out var drop))
            {
                var color = view.DropRune != null ? Palette.Rune
                    : view.DropConsumable != null ? Palette.Potion
                    : view.DropMaterial != null ? Palette.Material
                    : Palette.Of(view.DropRarity);
                string name = view.DropRune?.Name ?? view.DropConsumable?.Name ?? view.DropMaterial?.Name ?? view.DropBase.Name;
                var icon = view.DropRune != null ? Sprites.Icon(view.DropRune)
                    : view.DropConsumable != null ? Sprites.Icon(view.DropConsumable)
                    : view.DropMaterial != null ? Sprites.Icon(view.DropMaterial)
                    : Sprites.Icon(view.DropBase);
                drop = new Drop
                {
                    Renderer = Renderer(_root.transform, "Drop", icon, Color.white),
                    Position = new Vector3(view.Position.X, view.Position.Y, 0),
                    Name = name,
                    Color = color,
                };
                drop.Renderer.sortingOrder = -10;
                if (view.DropBase != null && view.DropRarity != Rarity.Common)
                {
                    drop.Glow = Renderer(_root.transform, "DropGlow", Shapes.Circle, Faded(color, 0.35f));
                    drop.Glow.transform.localPosition = drop.Position;
                    drop.Glow.transform.localScale = Vector3.one * 0.8f;
                    drop.Glow.sortingOrder = -11;
                }
                _drops[view.Id] = drop;
            }
            float bob = 0.06f * Mathf.Sin(Time.time * 3 + view.Id);
            drop.Renderer.transform.localPosition = drop.Position + new Vector3(0, bob, 0);
        }

        static readonly Dictionary<MonsterDefinition, Rgba> MonsterColors = new Dictionary<MonsterDefinition, Rgba>();

        // A monster's colour comes from its sheet row, so a new monster needs no code here.
        static Rgba ColorOf(MonsterDefinition monster)
        {
            if (!MonsterColors.TryGetValue(monster, out var color))
            {
                color = ColorUtility.TryParseHtmlString(monster.Color, out var parsed) ? Sprites.ToRgba(parsed) : SlimeColor;
                MonsterColors[monster] = color;
            }
            return color;
        }

        // Banners for a boss waking up and changing phase.
        void WatchBoss(EntityView view)
        {
            bool known = _bossesSeen.TryGetValue(view.Id, out var before);
            if (view.BossState != BossState.Dormant && (!known || before.State == BossState.Dormant))
            {
                ShowBanner($"{view.Boss.Name} 등장!");
                Sounds.Play(Sound.Wake);
            }
            else if (known && view.Phase > before.Phase)
            {
                ShowBanner($"{view.Phase}페이즈!");
                Sounds.Play(Sound.Roar);
            }
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
                    Hear(Sound.Warning, new Vector3(hazard.Position.X, hazard.Position.Y, 0));
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
            mark.Edge.sortingOrder = DarkOrder + 10;
            mark.Fill.sortingOrder = DarkOrder + 11;
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

        // A crop grows through four drawings on its patch of soil, and sways a little once ripe.
        void DrawCrops()
        {
            _seen.Clear();
            double now = _client.HostTick;
            foreach (var crop in _client.Crops)
            {
                int key = crop.Y * _client.Map.Width + crop.X;
                _seen.Add(key);
                if (!_crops.TryGetValue(key, out var renderer))
                {
                    renderer = Renderer(_root.transform, "Crop", null, Color.white);
                    renderer.transform.localPosition = new Vector3(crop.X + 0.5f, crop.Y + 1f / Sprites.PixelsPerUnit, 0);
                    renderer.sortingOrder = -20;
                    _crops[key] = renderer;
                }
                bool ripe = crop.IsRipe(now);
                // Four steps, as the docs describe: sprout, young, growing, ripe.
                int stage = ripe ? 3 : Mathf.Min(2, Mathf.FloorToInt(crop.Growth(now) * 3));
                renderer.sprite = CropSprites[stage];
                renderer.transform.localScale = new Vector3(1, ripe ? 1 + 0.05f * Mathf.Sin(Time.time * 5 + key) : 1, 1);
            }

            _gone.Clear();
            foreach (var pair in _crops)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int key in _gone) _crops.Remove(key);
        }

        // Sprites are made on first use, not while the type loads: Unity only makes textures on the main thread.
        static Sprite[] _cropSprites;

        static Sprite[] CropSprites => _cropSprites ??= new[]
        {
            Sprites.Standing("crop/0", () => ItemArt.Crop(0)),
            Sprites.Standing("crop/1", () => ItemArt.Crop(1)),
            Sprites.Standing("crop/2", () => ItemArt.Crop(2)),
            Sprites.Standing("crop/3", () => ItemArt.Crop(3)),
        };

        // Each machine's facing part, turned the way it faces, and the load on each conveyor moving along it toward
        // the front. Belts run at their real speed: 2 pixels a frame of the belt, 16 frames a second, is a tile in
        // half a second.
        void DrawMachines()
        {
            _seen.Clear();
            int beltFrame = Mathf.FloorToInt(Time.time * 16) % ItemArt.BeltFrames;
            foreach (var machine in _client.Machines())
            {
                int key = machine.Y * _client.Map.Width + machine.X;
                _seen.Add(key);
                if (_machines.TryGetValue(key, out var parts) && parts.Kind != machine.Kind)
                {
                    Object.Destroy(parts.Part.gameObject);
                    Object.Destroy(parts.Load.gameObject);
                    _machines.Remove(key);
                }
                if (!_machines.TryGetValue(key, out parts))
                {
                    var part = Renderer(_root.transform, "Machine", null, Color.white);
                    part.sortingOrder = -15;
                    var load = Renderer(_root.transform, "Load", null, Color.white);
                    load.sortingOrder = -14;
                    parts = (machine.Kind, part, load);
                    _machines[key] = parts;
                }
                var step = Machine.Steps[machine.Direction];
                var center = new Vector3(machine.X + 0.5f, machine.Y + 0.5f, 0);
                parts.Part.sprite = machine.Kind == TileType.Drill ? DrillBitSprite
                    : machine.Kind == TileType.Conveyor ? BeltSprites[beltFrame]
                    : ChevronSprite;
                parts.Part.transform.localPosition = center;
                parts.Part.transform.localRotation = Quaternion.Euler(0, 0, machine.Direction * 90);
                parts.Load.enabled = machine.Carried != null;
                if (machine.Carried != null) parts.Load.sprite = Sprites.Icon(machine.Carried);
                parts.Load.transform.localPosition = center + new Vector3(step.X, step.Y, 0) * (machine.Progress - 0.5f);
            }

            _gone.Clear();
            foreach (var pair in _machines)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Part.gameObject);
                Object.Destroy(pair.Value.Load.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int key in _gone) _machines.Remove(key);
        }

        static Sprite DrillBitSprite => Sprites.Centered("machine/drill", () => ItemArt.DrillBit);

        static Sprite ChevronSprite => Sprites.Centered("machine/chevron", () => ItemArt.Chevron);

        static Sprite[] _beltSprites;

        static Sprite[] BeltSprites => _beltSprites ??= new[]
        {
            Sprites.Centered("machine/belt0", () => ItemArt.Belt(0)),
            Sprites.Centered("machine/belt1", () => ItemArt.Belt(1)),
            Sprites.Centered("machine/belt2", () => ItemArt.Belt(2)),
            Sprites.Centered("machine/belt3", () => ItemArt.Belt(3)),
        };

        void DrawProjectiles()
        {
            _seen.Clear();
            foreach (var projectile in _client.Projectiles())
            {
                _seen.Add(projectile.Id);
                if (!_projectiles.TryGetValue(projectile.Id, out var renderer))
                {
                    // A player's is a glowing ball in the colour of its tags, a little larger than it really is so it
                    // reads, or an arrow pointing the way it flies; an enemy's is a spiked ball.
                    bool arrow = projectile.Friendly && (projectile.Tags & SkillTag.Ranged) != 0;
                    var color = projectile.Friendly ? Palette.Of(projectile.Tags) : ProjectileColor;
                    var sprite = arrow ? ArrowSprite : projectile.Friendly ? OrbSprite : SporeSprite;
                    renderer = Renderer(_root.transform, "Projectile", sprite, arrow ? Color.Lerp(Color.white, color, 0.3f) : color);
                    float across = sprite.rect.height / Sprites.PixelsPerUnit;
                    renderer.transform.localScale = Vector3.one * (arrow ? 1 : Mathf.Max(1, projectile.Radius * (projectile.Friendly ? 2.6f : 2) / across));
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, projectile.Velocity.Angle * Mathf.Rad2Deg);
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

        static Sprite ArrowSprite => Sprites.Centered("shot/arrow", () => ItemArt.Arrow);

        static Sprite OrbSprite => Sprites.Centered("shot/orb", () => ItemArt.Orb);

        static Sprite SporeSprite => Sprites.Centered("shot/spore", () => ItemArt.Spore);

        void Show(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Attack:
                    var attacker = Find(e.Source);
                    if (attacker == null) break;
                    var kind = (BasicAttackKind)e.Amount;
                    Hear(kind == BasicAttackKind.Arrow ? Sound.Shoot : kind == BasicAttackKind.Bolt ? Sound.Zap : Sound.Swing, attacker.Position);
                    // Shots show as their projectile, so only swings draw the arc.
                    if (kind != BasicAttackKind.Swing) break;
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
                    Hear(hurtsMe ? Sound.Hurt : e.Critical ? Sound.Crit : Sound.Hit, target.Position);
                    break;
                case SimEventType.Impact:
                    var source = Find(e.Source);
                    if (source == null || Vector3.Distance(source.Position, _self.Position) > ShakeRange) break;
                    _shake = Mathf.Max(_shake, Mathf.Min(0.45f, e.Amount * 0.07f));
                    Hear(Sound.Boom, source.Position, Mathf.Min(1, 0.4f + e.Amount * 0.1f));
                    break;
                case SimEventType.SkillUsed:
                    ShowSkill(e);
                    break;
                case SimEventType.Dug:
                    // A puff of dust where the wall was struck, bigger when it gives way.
                    var struck = new Vector3(e.Position.X, e.Position.Y, 0);
                    AddFlash(struck, e.Amount == 1 ? 0.7f : 0.35f, Palette.Dust, Shapes.Circle);
                    Hear(e.Amount == 1 ? Sound.Break : Sound.Dig, struck);
                    break;
                case SimEventType.Harvested:
                    AddFlash(new Vector3(e.Position.X, e.Position.Y, 0), 0.6f, Palette.Ripe, Shapes.Ring);
                    Hear(Sound.Harvest, new Vector3(e.Position.X, e.Position.Y, 0));
                    break;
                case SimEventType.Healed:
                    var drinker = Find(e.Source);
                    if (drinker == null) break;
                    _floating.Add(new Floating { Position = drinker.Position, Text = "+" + e.Amount, Color = Palette.Heal });
                    Hear(Sound.Heal, drinker.Position);
                    AddFlash(drinker.Position, drinker.Diameter, Palette.Heal, Shapes.Ring);
                    break;
                case SimEventType.Burst:
                    var burst = _client.SkillOf(e.Source, e.Amount);
                    if (burst == null) break;
                    var at = new Vector3(e.Position.X, e.Position.Y, 0);
                    AddFlash(at, Mathf.Max(0.4f, burst.Radius), Palette.Of(burst.Tags | GrantedTags(e.Source)), Shapes.Circle);
                    if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.08f);
                    Hear(Sound.Boom, at, 0.7f);
                    break;
                case SimEventType.Died:
                    if (e.Source == _client.PlayerId)
                    {
                        _floating.Add(new Floating { Position = _self.Position, Text = "쓰러짐", Color = Color.gray });
                        Sounds.Play(Sound.Down);
                    }
                    else if (!_bossesSeen.ContainsKey(e.Source) && Find(e.Source) is Body fallen)
                    {
                        Hear(fallen.Kind == BodyKind.Player ? Sound.Down : Sound.Pop, fallen.Position);
                    }
                    if (_bossesSeen.TryGetValue(e.Source, out var boss))
                    {
                        Sounds.Play(Sound.Victory);
                        ShowBanner($"{boss.Name} 처치!");
                        _bossesSeen.Remove(e.Source);
                    }
                    break;
            }
        }

        // The skill's name over whoever used it, a ring for a nova and a pulse for a buff.
        void ShowSkill(SimEvent e)
        {
            var skill = _client.SkillOf(e.Source, e.Amount);
            var caster = Find(e.Source);
            if (skill == null || caster == null) return;
            var color = Palette.Of(skill.Tags | skill.Grants | GrantedTags(e.Source));
            _floating.Add(new Floating { Position = caster.Position + new Vector3(0, 0.4f, 0), Text = skill.Name, Color = color });
            Hear(skill.Kind == SkillKind.Nova ? Sound.Nova : skill.Kind == SkillKind.Buff ? Sound.Buff : Sound.Zap, caster.Position);
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    AddFlash(caster.Position, skill.Radius + World.PlayerRadius, color, Shapes.Ring);
                    if (e.Source == _client.PlayerId) _shake = Mathf.Max(_shake, 0.06f);
                    break;
                case SkillKind.Buff:
                    AddFlash(caster.Position, caster.Diameter, color, Shapes.Ring);
                    break;
                case SkillKind.Projectile:
                    caster.SwingTime = SwingSeconds;
                    caster.Swing.transform.localRotation = Quaternion.Euler(0, 0, e.Direction.Angle * Mathf.Rad2Deg);
                    break;
            }
        }

        // A sound where something happened, quieter the farther it is from the player (Sounds).
        void Hear(Sound sound, Vector3 at, float volume = 1) => Sounds.PlayAt(sound, at, _self.Position, volume);

        // Tags a player's running buffs add to their attacks, for colouring what they do.
        SkillTag GrantedTags(int playerId)
        {
            if (playerId == _client.PlayerId) return _client.Stats.GrantedTags;
            var tags = SkillTag.None;
            if (!_buffBits.TryGetValue(playerId, out byte bits)) return tags;
            for (int slot = 0; slot < 8; slot++)
            {
                if ((bits & (1 << slot)) != 0) tags |= _client.SkillOf(playerId, slot)?.Grants ?? SkillTag.None;
            }
            return tags;
        }

        // A ring around the player in the colours of the buffs running, mixed when there are several.
        void ShowAura(Body body, int playerId, int bits)
        {
            var mixed = Color.clear;
            int count = 0;
            for (int slot = 0; slot < 8; slot++)
            {
                if ((bits & (1 << slot)) == 0) continue;
                var skill = _client.SkillOf(playerId, slot);
                if (skill == null) continue;
                mixed += Palette.Of(skill.Tags | skill.Grants);
                count++;
            }
            body.Aura.enabled = count > 0;
            if (count == 0) return;
            mixed /= count;
            mixed.a = 0.55f + 0.25f * Mathf.Sin(Time.time * 6);
            body.Aura.color = mixed;
            body.Aura.sortingOrder = body.Shape.sortingOrder - 1;
        }

        void AddFlash(Vector3 position, float radius, Color color, Sprite sprite)
        {
            var renderer = Renderer(_root.transform, "Flash", sprite, color);
            renderer.transform.localPosition = position;
            renderer.sortingOrder = 19000;
            _flashes.Add(new Flash { Renderer = renderer, Size = radius * 2, Life = FlashLife, Color = color });
        }

        // Each flash opens out to its full size and fades.
        void UpdateFlashes(float deltaTime)
        {
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                flash.Age += deltaTime;
                float t = Mathf.Clamp01(flash.Age / flash.Life);
                if (t >= 1)
                {
                    Object.Destroy(flash.Renderer.gameObject);
                    _flashes.RemoveAt(i);
                    continue;
                }
                flash.Renderer.transform.localScale = Vector3.one * (flash.Size * (0.6f + 0.4f * Mathf.Sqrt(t)));
                var color = flash.Color;
                color.a = 0.7f * (1 - t);
                flash.Renderer.color = color;
            }
        }

        Body Find(int id) => id == _client.PlayerId ? _self : _others.TryGetValue(id, out var body) ? body : null;

        // Puts a body where it is this frame: picks its sprite (the way it faces, a step of its walk), tints it
        // (grey when dormant or dead) and lays the glow over it for flashes, then the swing and health bar.
        void Place(Body body, Vec2 position, Vec2 facing, float health, float maxHealth, Color tint, Color glow, float deltaTime, float pulse = 0, bool showHealth = true)
        {
            body.LastPosition = body.Position;
            body.Position = new Vector3(position.X, position.Y, 0);
            body.Root.localPosition = body.Position;
            int order = 10000 - Mathf.RoundToInt(position.Y * 10);

            bool dead = health <= 0;
            Animate(body, facing, deltaTime);
            float scale = 1 + pulse * Mathf.Sin(Time.time * 30);
            body.Shape.transform.localScale = new Vector3(1, scale, 1);
            body.Shape.color = dead ? DeadColor : tint;
            body.Shape.sortingOrder = order;
            body.Glow.transform.localScale = body.Shape.transform.localScale;
            body.Glow.color = dead ? Color.clear : body.FlashTime > 0 ? HitFlash : glow;
            body.Glow.sortingOrder = order + 1;
            body.Shadow.enabled = !dead;

            body.Swing.enabled = body.SwingTime > 0;
            body.Swing.color = new Color(1, 1, 1, Mathf.Clamp01(body.SwingTime / SwingSeconds) * 0.6f);
            body.Swing.sortingOrder = order + 2;

            float share = maxHealth > 0 ? Mathf.Clamp01(health / maxHealth) : 0;
            bool barVisible = showHealth && !dead && share < 0.999f;
            body.HealthBack.enabled = barVisible;
            body.HealthFill.enabled = barVisible;
            body.HealthBack.transform.localPosition = new Vector3(0, body.Top + 0.15f, 0);
            body.HealthFill.transform.localScale = new Vector3(0.8f * share, 0.08f, 1);
            body.HealthFill.transform.localPosition = new Vector3(-0.4f + 0.4f * share, body.Top + 0.15f, 0);
            body.HealthFill.color = share > 0.5f ? new Color(0.4f, 0.9f, 0.4f) : share > 0.25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.3f, 0.25f);
            body.HealthBack.sortingOrder = order + 3;
            body.HealthFill.sortingOrder = order + 4;

            body.SwingTime = Mathf.Max(0, body.SwingTime - deltaTime);
            body.FlashTime = Mathf.Max(0, body.FlashTime - deltaTime);
        }

        // Players walk in three directions (left is right flipped) while they move and stand still otherwise;
        // monsters face left or right and bob, faster while they move; the boss breathes.
        void Animate(Body body, Vec2 facing, float deltaTime)
        {
            float speed = deltaTime > 0 ? Vector3.Distance(body.Position, body.LastPosition) / deltaTime : 0;
            bool moving = speed > 0.3f && speed < 30;
            body.Walked = moving ? body.Walked + deltaTime : 0;
            bool left = facing.X < 0;
            int frame;
            var look = Look.Side;
            switch (body.Kind)
            {
                case BodyKind.Player:
                    look = Mathf.Abs(facing.X) >= Mathf.Abs(facing.Y) ? Look.Side : facing.Y > 0 ? Look.Up : Look.Down;
                    frame = moving ? Mathf.FloorToInt(body.Walked * 8) % CreatureArt.WalkFrames : 0;
                    left &= look == Look.Side;
                    break;
                case BodyKind.Monster:
                    frame = Mathf.FloorToInt(Time.time * (moving ? 6 : 3) + body.Seed * 0.37f) % CreatureArt.IdleFrames;
                    break;
                default:
                    frame = Mathf.FloorToInt(Time.time * 1.5f) % CreatureArt.IdleFrames;
                    break;
            }

            int code = ((int)look * 16 + frame) * 2 + (left ? 1 : 0);
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"{body.Kind}/{body.Id}/{Sprites.Key(body.Color)}/{look}/{frame}";
            Func<PixelImage> draw = body.Kind switch
            {
                BodyKind.Player => () => CreatureArt.Player(look, frame, body.Color),
                BodyKind.Monster => () => CreatureArt.Monster(body.Id, body.Color, frame),
                _ => () => CreatureArt.Boss(body.Id, body.Color, frame),
            };
            body.Shape.sprite = Sprites.Standing(key, draw);
            body.Glow.sprite = Sprites.Standing(key + "/white", () => draw().Silhouette());
            body.Shape.flipX = left;
            body.Glow.flipX = left;
            body.Top = -body.Feet + body.Shape.sprite.rect.height / Sprites.PixelsPerUnit;
        }

        Body MakeBody(BodyKind kind, string id, int seed, float radius, Rgba color)
        {
            var root = new GameObject(id).transform;
            root.SetParent(_root.transform, false);
            float diameter = radius * 2;
            int shadowWidth = Mathf.Max(6, Mathf.RoundToInt(diameter * Sprites.PixelsPerUnit * 0.9f));
            var body = new Body
            {
                Root = root,
                Kind = kind,
                Id = id,
                Seed = seed,
                Color = color,
                Diameter = diameter,
                // Bodies stand a little below their centre, so the round shape the game uses sits around their feet.
                Feet = radius * 0.8f,
                Shape = Renderer(root, "Shape", null, Color.white),
                Glow = Renderer(root, "Glow", null, Color.clear),
                Shadow = Renderer(root, "Shadow", Sprites.Centered("shadow/" + shadowWidth, () => CreatureArt.Shadow(shadowWidth)), Color.white),
                Swing = Renderer(root, "Swing", Shapes.Arc, Color.white),
                HealthBack = Renderer(root, "HealthBack", Shapes.Square, new Color(0, 0, 0, 0.6f)),
                HealthFill = Renderer(root, "HealthFill", Shapes.Square, Color.green),
                Aura = Renderer(root, "Aura", Shapes.Ring, Color.clear),
            };
            body.Shape.transform.localPosition = new Vector3(0, -body.Feet, 0);
            body.Glow.transform.localPosition = body.Shape.transform.localPosition;
            body.Shadow.transform.localPosition = new Vector3(0, -body.Feet + 1f / Sprites.PixelsPerUnit, 0);
            body.Shadow.sortingOrder = -30;
            body.Aura.transform.localScale = Vector3.one * (diameter * 1.7f);
            body.Aura.enabled = false;
            // The swing reaches MeleeReach past the edge of the body.
            body.Swing.transform.localScale = Vector3.one * (diameter + World.MeleeReach * 2);
            body.HealthBack.transform.localScale = new Vector3(0.84f, 0.12f, 1);
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

        // Two tilemaps: the ground (floor and rock, each tile drawn to match its neighbours) and over it the things
        // placed on the floor.
        void BuildTiles(WorldClient client)
        {
            var map = client.Map;
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(_root.transform, false);
            _ground = AddTilemap(grid.transform, "Ground", -100);
            _objects = AddTilemap(grid.transform, "Objects", -90);

            var ground = new TileBase[map.Width * map.Height];
            var objects = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ground[y * map.Width + x] = GroundAt(x, y);
                    objects[y * map.Width + x] = Sprites.Object(map[x, y]);
                }
            }
            var bounds = new BoundsInt(0, 0, 0, map.Width, map.Height, 1);
            _ground.SetTilesBlock(bounds, ground);
            _objects.SetTilesBlock(bounds, objects);
        }

        static Tilemap AddTilemap(Transform grid, string name, int order)
        {
            var tilesObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            tilesObject.transform.SetParent(grid, false);
            tilesObject.GetComponent<TilemapRenderer>().sortingOrder = order;
            return tilesObject.GetComponent<Tilemap>();
        }

        TileBase GroundAt(int x, int y)
        {
            bool inArena = Vec2.Distance(TileMap.Center(x, y), _client.Arena) <= _client.ArenaRadius;
            return Sprites.Ground(TileArt.GroundAt(_client.Map, x, y, inArena));
        }

        static readonly (int X, int Y)[] Neighbourhood = { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) };

        // Redraws tiles that were dug or built on, and their neighbours, whose edges and shadows depend on them.
        public void UpdateTiles(IEnumerable<TileChange> changes)
        {
            var map = _client.Map;
            foreach (var change in changes)
            {
                _objects.SetTile(new Vector3Int(change.X, change.Y, 0), Sprites.Object(map[change.X, change.Y]));
                foreach (var (dx, dy) in Neighbourhood)
                {
                    int x = change.X + dx;
                    int y = change.Y + dy;
                    if (map.InBounds(x, y)) _ground.SetTile(new Vector3Int(x, y, 0), GroundAt(x, y));
                }
            }
        }

        // The project uses URP's 2D renderer, which draws sprites by 2D lights; one global light shows everything.
        // The dark of the caves is not done with 2D lights but with the overlay below, which works the same in a
        // browser, lets walls block light, and leaves telegraphs bright.
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

        void BuildDark()
        {
            _darkTexture = new Texture2D(DarkSize, DarkSize, TextureFormat.RGBA32, false)
            {
                // Smooth between tile centres, so light falls off softly instead of in squares.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _darkPixels = new Color32[DarkSize * DarkSize];
            // One texel a world unit, so texel centres sit on tile centres when the corner is on a tile corner.
            var sprite = Sprite.Create(_darkTexture, new Rect(0, 0, DarkSize, DarkSize), Vector2.zero, 1, 0, SpriteMeshType.FullRect);
            _dark = Renderer(_root.transform, "Dark", sprite, Color.white);
            _dark.sortingOrder = DarkOrder;
        }

        // Every player carries a light, burning tiles (tiles.csv light) and glowing shots light their surroundings,
        // and the boss arena glows. Off in the menu, everything is lit.
        void UpdateDark(Vec2 self)
        {
            _dark.enabled = Settings.Darkness;
            if (!_dark.enabled) return;
            int left = Mathf.FloorToInt(_cameraTarget.x) - DarkSize / 2;
            int bottom = Mathf.FloorToInt(_cameraTarget.y) - DarkSize / 2;
            _lights.Clear();
            _lights.Add(WorldLights.Carried(self));
            foreach (var body in _others.Values)
            {
                if (body.Kind == BodyKind.Player) _lights.Add(WorldLights.Carried(new Vec2(body.Position.x, body.Position.y)));
            }
            _worldLights.AddTiles(_lights, _client.Map, left, bottom, DarkSize, Time.time);
            foreach (var shot in _projectiles.Values)
            {
                if (shot.sprite == ArrowSprite) continue;
                var at = shot.transform.localPosition;
                _lights.Add(new LightSource(new Vec2(at.x, at.y), ShotLight, 0.8f, Sprites.ToRgba(shot.color), tints: true));
            }
            _lightMap.Compute(_client.Map, left, bottom, _lights, _arenaGlow);
            for (int j = 0; j < DarkSize; j++)
            {
                for (int i = 0; i < DarkSize; i++)
                {
                    var o = _lightMap.Overlay(i, j);
                    _darkPixels[j * DarkSize + i] = new Color32(o.R, o.G, o.B, o.A);
                }
            }
            _darkTexture.SetPixels32(_darkPixels);
            _darkTexture.Apply(false);
            _dark.transform.localPosition = new Vector3(left, bottom, 0);
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
