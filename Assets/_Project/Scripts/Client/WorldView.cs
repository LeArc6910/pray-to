using System;
using System.Collections.Generic;
using System.Linq;
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
    sealed partial class WorldView : IDisposable
    {
        enum BodyKind
        {
            Player,
            Monster,
            Boss,
            Minion,
        }

        sealed class Body
        {
            public Transform Root;
            public SpriteRenderer Shape;
            // The same sprite in white over the shape, for flashes: hit, winding up, charging.
            public SpriteRenderer Glow;
            // Makes the white picture of the frame just set, when the animation leaves it until it is shown (WhiteOf).
            public System.Func<Sprite> White;
            public SpriteRenderer Shadow;
            // A player's weapon in hand, and which one it shows.
            public SpriteRenderer Weapon;
            public ItemBase Held;
            public SpriteRenderer Swing;
            public SpriteRenderer HealthBack;
            public SpriteRenderer HealthFill;
            // A ring while a buff runs, in the buff's colour.
            public SpriteRenderer Aura;
            // Sizes of the swing and the buff ring, in pixels (EffectArt).
            public int SwingDiameter;
            public int AuraDiameter;
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
            // A short lunge toward each attack: time left and which way.
            public float LungeTime;
            public Vector2 Lunge;
            // How far the lunge goes, in lunges (a finisher goes twice as far).
            public float LungeReach = 1;
            // Knocked back by a blow: time left and which way, and how far (in lunges).
            public float KnockTime;
            public Vector2 Knock;
            public float KnockReach = 1;
            // The step of a player's basic attack combo (World.ComboStepOf) and how long its strike pose lasts.
            public int Combo;
            public float StrikeLength;
            // A player's poses (CreatureArt.Pose): time left in a strike, a cast and a flinch.
            public float StrikeTime;
            public float CastTime;
            // A motion of the player's own (2026-11-09): drawing a bow, a staff's zap, drinking, a skill thrown forward
            // or round; its time left and length, the sector it began facing (for a spin), and what is in hand.
            public Motion Motion;
            public float MotionTime;
            public float MotionLength;
            public int SpinFrom;
            public Sprite Holding;
            // A boss's state as the snapshots say, for its pose; a monster's likewise.
            public BossState BossState;
            // The attack a boss is on, when it took it up (or changed state) and its phase; the warning sign over it
            // and, for the vein eater, the mound it runs under the floor and when it last threw up dust
            // (WorldView.Bosses.cs).
            public BossAttack BossAttack;
            public float BossSince;
            // Winding up straight out of a charge to charge again (a chain of dashes), in less time.
            public bool BossAgain;
            public int BossPhase;
            public SpriteRenderer Sign;
            public SpriteRenderer Mound;
            public float DustAt;
            // The samurai dashing, and where the dash began (WorldView.Bosses.cs).
            public bool Dashing;
            public Vector3 DashFrom;
            // Its blade has clicked home after the last cut.
            public bool Clicked;
            // When it last left an afterimage, how many it has left (they take turns in colour), and whether it has
            // vanished in petals for a flash step.
            public float EchoAt;
            public int Echoes;
            public bool Vanished;
            // Where its last afterimage was left (they are spaced by distance, not by time), and whether it is leaving
            // them now; where the mouth of its scabbard is from its middle; and whether the guard has glinted before
            // this draw.
            public Vector3 EchoFrom;
            public bool Echoing;
            public Vector3 Hip;
            public bool Glinted;
            // The black knight's last walking frame, for a thud as each foot comes down (WorldView.Knight.cs).
            public int KnightStep = -1;
            // A boss's state before this one (the venom spider comes down from a roar). The venom spider's pose, frame and
            // view as drawn (for where its head and spinnerets are), when its legs last ticked, whether it has hissed and
            // how many times it has plucked in this wind-up, and its body coming down its thread over the mark with the
            // thread and its shadow (WorldView.Spider.cs).
            public BossState BossWas;
            public SpiderPose SpiderPose;
            public int SpiderFrame;
            public BossView SpiderView;
            public bool SpiderTurned;
            public float SkitterAt;
            public bool Hissed;
            public int Twangs;
            public SpriteRenderer Faller;
            public SpriteRenderer FallerThread;
            public SpriteRenderer FallerShadow;
            // Whether it was plucking its webs last frame (the room dimmed).
            public bool Plucked;
            public EnemyState EnemyState;
            // When a monster may cry out again on noticing someone, and where another player was last frame and how
            // far they have walked since their last step (2026-11-09, sounds).
            public float CryAt;
            public Vector3 LastStep;
            public float Stride;
            // A player lying in their bed (World.Sleep): "Zz" over them.
            public bool Sleeping;
            public float HurtTime;
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
            public HazardState State;
            // A rock falling onto it from the roof (the vein eater's cave-in, WorldView.Bosses.cs).
            public SpriteRenderer Rock;
            // When strands next cling to whoever stands in this web (the venom spider's, WorldView.Spider.cs).
            public float StuckAt;
        }

        // A player's pet (Pets.cs): its monster drawn small, trotting after them a little behind, hopping or bobbing.
        sealed class PetBody
        {
            public SpriteRenderer Renderer;
            public PetDefinition Pet;
            public Vector3 Position;
            public bool Left;
        }

        readonly Dictionary<int, PetBody> _pets = new Dictionary<int, PetBody>();
        const float PetScale = 0.6f;

        void DrawPet(int owner, Body body, PetDefinition pet, float deltaTime)
        {
            _pets.TryGetValue(owner, out var shown);
            if (pet == null || body == null)
            {
                if (shown != null) Object.Destroy(shown.Renderer.gameObject);
                _pets.Remove(owner);
                return;
            }
            if (shown == null || shown.Pet != pet)
            {
                if (shown != null) Object.Destroy(shown.Renderer.gameObject);
                shown = new PetBody { Renderer = Renderer(_root.transform, "Pet", null, Color.white), Pet = pet, Position = body.Position };
                shown.Renderer.transform.localScale = Vector3.one * PetScale;
                _pets[owner] = shown;
            }
            // Behind and beside its player, catching up smoothly; it faces the way it is going.
            var target = body.Position + new Vector3(shown.Left ? 0.75f : -0.75f, -0.15f, 0);
            var before = shown.Position;
            shown.Position = Vector3.Lerp(shown.Position, target, 1 - Mathf.Exp(-5 * deltaTime));
            float moved = shown.Position.x - before.x;
            if (Mathf.Abs(moved) > 0.002f) shown.Left = moved < 0;
            bool moving = (shown.Position - before).sqrMagnitude > 0.00001f;
            bool flies = pet.Look.Contains("bat") || pet.Look.Contains("wisp");
            float lift = flies ? 0.35f + 0.06f * Mathf.Sin(Time.time * 5 + owner) : moving ? Mathf.Abs(Mathf.Sin(Time.time * 12)) * 0.08f : 0;
            int frame = Mathf.FloorToInt(Time.time * (moving ? 6 : 3)) % CreatureArt.IdleFrames;
            var look = _client.MonsterKinds?.Monsters.FirstOrDefault(m => m.Id == pet.Look);
            var color = look != null && ColorUtility.TryParseHtmlString(look.Color, out var parsed) ? Sprites.ToRgba(parsed) : SlimeColor;
            string key = $"Pet/{pet.Look}/{frame}";
            shown.Renderer.sprite = Sprites.Standing(key, () => CreatureArt.Monster(pet.Look, color, frame));
            shown.Renderer.flipX = shown.Left;
            shown.Renderer.transform.localPosition = shown.Position + new Vector3(0, lift, 0);
            shown.Renderer.sortingOrder = 10000 - Mathf.RoundToInt(shown.Position.y * 10);
        }

        // Telegraphs just gone, by id, for the blast drawn when the boss's Impact for them comes (HazardLanded).
        readonly Dictionary<int, (HazardState State, float Time)> _landed = new Dictionary<int, (HazardState, float)>();

        // A skill's ring or burst, growing and fading out where it happened.
        sealed class Flash
        {
            public SpriteRenderer Renderer;
            public FlashKind Kind;
            public int Diameter;
            public float Age;
            public float Life;
            public Color Color;
        }

        // What a flash looks like: a pixel effect from EffectArt, run frame by frame.
        enum FlashKind
        {
            // A blast: skill bursts, dust, splashes.
            Burst,
            // A ring opening out: novas, summons, a harvest.
            Ring,
            // Sparkles rising: healing, food, buffs.
            Sparkles,
            // A star where a blow lands.
            Impact,
            // Blades of light whirling round: spinning strikes and ice storms.
            Vortex,
        }

        // A bit flying off something that died, fading as it slows.
        sealed class Bit
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Age;
            public Color Color;
            public float Life = BitSeconds;
            // How fast it slows; a petal (2026-11-12, the samurai) slows less, is blown down and aside, sways, turns
            // over and over (its width shrinking and growing), and stays bright until late in its life.
            public float Drag = 6;
            public Vector3 Fall;
            public float Sway;
            public float Spin;
            public float Phase;
            public bool Tumble;
            public bool LateFade;
            // The line of a cut (2026-11-13, the samurai) stays whole a moment and then closes to nothing across its
            // width, from the size it was made at.
            public bool Thin;
            public Vector3 Scale;
        }

        // Where a body was and what colour it was, kept for the frame it is removed in: its death event comes after.
        readonly struct Departed
        {
            public Departed(Vector3 position, Color color, BodyKind kind, string id = null, BossView view = BossView.Front, bool turned = false)
            {
                Position = position;
                Color = color;
                Kind = kind;
                Id = id;
                View = view;
                Turned = turned;
            }

            public Vector3 Position { get; }
            public Color Color { get; }
            public BodyKind Kind { get; }
            // Which body it was and the way it was drawn last (the venom spider falls as it stood).
            public string Id { get; }
            public BossView View { get; }
            public bool Turned { get; }
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
        const float LungeSeconds = 0.14f;
        // How long a player holds each pose: a strike (the first part with the weapon raised), a cast, a flinch.
        const float StrikeSeconds = 0.24f;
        const float WindUpSeconds = 0.07f;
        const float CastSeconds = 0.3f;
        const float ShootSeconds = 0.3f;
        const float DrinkSeconds = 0.6f;
        const float ThrustSeconds = 0.32f;
        const float SpinSeconds = 0.36f;

        enum Motion
        {
            None,
            Shoot,
            Zap,
            Drink,
            Thrust,
            Spin,
        }

        // The eight ways a player looks, from facing right and turning counterclockwise: right, up-right, up, up-left,
        // left, down-left, down, down-right. Left-facing ones are right-facing pictures flipped.
        static readonly (Look Look, bool Left)[] Sectors =
        {
            (Look.Side, false), (Look.UpSide, false), (Look.Up, false), (Look.UpSide, true),
            (Look.Side, true), (Look.DownSide, true), (Look.Down, false), (Look.DownSide, false),
        };

        // A boss's views for the same eight ways (CreatureArt.BossView).
        static readonly (BossView View, bool Left)[] BossSectors =
        {
            (BossView.Side, false), (BossView.BackSide, false), (BossView.Back, false), (BossView.BackSide, true),
            (BossView.Side, true), (BossView.FrontSide, true), (BossView.Front, false), (BossView.FrontSide, false),
        };

        static int SectorOf(Vec2 facing) =>
            facing.LengthSquared < 1e-6f ? 6 : (Mathf.RoundToInt(Mathf.Atan2(facing.Y, facing.X) / (Mathf.PI / 4)) % 8 + 8) % 8;

        static void Start(Body body, Motion motion, float seconds, Vec2 facing)
        {
            body.Motion = motion;
            body.MotionTime = seconds;
            body.MotionLength = seconds;
            body.SpinFrom = SectorOf(facing);
            body.StrikeTime = 0;
        }
        const float HurtSeconds = 0.25f;
        // How long a struck body stays squashed, and what one pixel of a 16-pixel body is as a share of it.
        const float SquashSeconds = 0.1f;
        const float OnePixel = 1f / 16;
        // Hit feel: how long a struck body is knocked back, how long a heavy blow of one's own holds the picture still
        // (hit-stop), and how long the screen's edges glow red when one is hurt.
        const float KnockSeconds = 0.12f;
        const float HitStopSeconds = 0.06f;
        const float HurtGlowSeconds = 0.4f;
        static readonly Color ImpactWhite = new Color(1, 1, 1, 0.95f);
        static readonly Color ImpactGold = new Color(1f, 0.85f, 0.3f, 0.95f);
        static readonly Color ImpactRed = new Color(1f, 0.35f, 0.3f, 0.95f);
        // A standing player breathes in every other BreathSeconds and blinks for BlinkSeconds every BlinkEvery.
        const float BreathSeconds = 0.8f;
        const float BlinkEvery = 3.4f;
        const float BlinkSeconds = 0.13f;
        // How far a body leans into an attack, in tiles (two pixels).
        const float LungeDistance = 2f / Sprites.PixelsPerUnit;
        const float BitSeconds = 0.5f;
        const int BitsPerDeath = 10;
        // Impacts further away than this do not shake the screen.
        const float ShakeRange = 16;
        // The dark is a square of at least this many tiles around the camera, and wider when the view is (a very wide
        // window). It covers what stands in the world; projectiles, telegraphs and skill effects are drawn over it so
        // they show in the dark.
        const int DarkSize = 64;
        const int DarkOrder = 15000;

        // The tile a [놓기] would build on (World.PlaceAhead), framed while ShowTarget (the bag open, or the pointer on
        // a quick slot that builds or plants; 2026-11-09): white and breathing on open floor, red where it cannot
        // go. Over the dark, so it shows in an unlit cave.
        public bool ShowTarget { get; set; }

        SpriteRenderer _target;
        // The frame marks the tile ground work acts on rather than where a thing would be put (GameRoot sets it from
        // what the pointer is over).
        public bool TargetSoil { get; set; }
        // How many tiles each way the thing pointed at takes (a big machine 2, 2026-11-09): the frame covers them all.
        public int TargetSize { get; set; } = 1;

        void DrawTarget()
        {
            if (_target == null)
            {
                _target = Renderer(_root.transform, "Target", Sprites.Centered("fx/target", EffectArt.TargetFrame), Color.white);
                _target.sortingOrder = DarkOrder + 5;
            }
            _target.enabled = ShowTarget && _client.Map != null && !_client.IsDead;
            if (!_target.enabled) return;
            // Where the action pointed at works: a thing put down goes on the first tile clear of the body, ground work
            // (planting, watering, tilling, fertilizing, laying a floor) is on the tile right ahead.
            var (x, y) = TargetSoil ? World.TileAhead(_client.Position, _client.Facing) : World.PlaceAhead(_client.Position, _client.Facing);
            var map = _client.Map;
            int size = TargetSoil ? 1 : Mathf.Max(1, TargetSize);
            if (size > 1) (x, y) = World.BigOrigin(x, y, _client.Facing, size);
            bool open = World.Footprint(x, y, size).All(t => map.InBounds(t.X, t.Y) && !map.IsEdge(t.X, t.Y) && TileMap.IsGround(map[t.X, t.Y]));
            _target.transform.localPosition = new Vector3(x + size / 2f, y + size / 2f, 0);
            _target.transform.localScale = new Vector3(size, size, 1);
            _target.color = open ? new Color(1, 1, 1, 0.6f + 0.25f * Mathf.Sin(Time.time * 4)) : new Color(1, 0.35f, 0.3f, 0.8f);
        }
        // Scarecrows sway in a draught (2026-11-09): a few degrees each way about their foot, each a little out of step.
        readonly HashSet<Vector3Int> _scarecrows = new HashSet<Vector3Int>();

        void SwayScarecrows()
        {
            foreach (var cell in _scarecrows)
            {
                if (!Shown(cell.x, cell.y)) continue;
                float degrees = Mathf.Sin(Time.time * 1.7f + cell.x * 0.9f + cell.y * 0.4f) * 6;
                var sway = Matrix4x4.Translate(new Vector3(0, -0.45f, 0)) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, degrees)) * Matrix4x4.Translate(new Vector3(0, 0.45f, 0));
                _objects.SetTileFlags(cell, TileFlags.None);
                _objects.SetTransformMatrix(cell, sway);
            }
        }

        // How far a glowing shot lights its way.
        const float ShotLight = 2.2f;
        // A summoned spirit glows a little in its colour.
        const float MinionLight = 3f;

        // Tunics: blue for you, and a colour per player for the others so they can be told apart.
        static readonly Rgba SelfTunic = Rgba.Hex("#3F7FD8");
        static readonly Rgba[] OtherTunics = { Rgba.Hex("#4DB06A"), Rgba.Hex("#E08A3C"), Rgba.Hex("#A070DC"), Rgba.Hex("#3CB8B0") };
        static readonly Rgba SlimeColor = Rgba.Hex("#8CD94D");
        static readonly Rgba BossColor = new Rgba(158, 143, 128);
        static readonly Color DormantTint = new Color(0.6f, 0.6f, 0.62f);
        static readonly Color WindupColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color ChargeColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color ManaColor = new Color(0.45f, 0.65f, 1f);
        static readonly Color DeadColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color PathColor = new Color(1f, 0.85f, 0.3f);
        // A shared circle: gold, to say "stand in it together".
        static readonly Color ShareColor = new Color(1f, 0.8f, 0.2f);
        static readonly Color BlastColor = new Color(1f, 0.45f, 0.25f, 0.95f);
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
        readonly List<Bit> _bits = new List<Bit>();
        readonly Dictionary<int, Departed> _departed = new Dictionary<int, Departed>();
        // Which skill slots of each other player have a buff running, from their snapshot state.
        readonly Dictionary<int, byte> _buffBits = new Dictionary<int, byte>();
        readonly Dictionary<int, SpriteRenderer> _crops = new Dictionary<int, SpriteRenderer>();
        // Cracks over rock that has taken hits, by tile, until it breaks or changes.
        readonly Dictionary<int, SpriteRenderer> _cracks = new Dictionary<int, SpriteRenderer>();
        // Each player's float on the water while they fish, by player id.
        readonly Dictionary<int, SpriteRenderer> _bobbers = new Dictionary<int, SpriteRenderer>();
        // Per machine tile: the part that shows which way it faces (a drill's bit, a smelter's arrow, a conveyor's
        // moving belt), and the icon of what a conveyor carries.
        readonly Dictionary<int, (TileType Kind, SpriteRenderer Part, SpriteRenderer Load)> _machines =
            new Dictionary<int, (TileType, SpriteRenderer, SpriteRenderer)>();
        Tilemap _ground;
        Tilemap _corners;
        // The floor under things (PropArt.Underlay): a place's floor, or paving round it, between ground and objects.
        Tilemap _underlay;
        Tilemap _objects;
        LightMap _lightMap;
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
        float _hitStop;
        float _hurtGlow;
        float _hurtGlowStrength;
        Texture2D _vignette;
        // How far the player has walked since their last footstep.
        float _stride;
        // About two steps a second at a walk (2026-11-11: at 0.6 the steps came eight a second, a rattle).
        const float StepLength = 1.15f;
        // When the "곡괭이가 필요하다" reminder may show again.
        float _pickaxeHintAt;
        string _banner = "";
        float _bannerAge = BannerSeconds;
        // Banners that came while another was still fresh: a boss's fall and the level it gave land in the same
        // moment, and the second would hide the first at once. Each shows at least BannerTurnSeconds, then the next.
        readonly Queue<string> _bannerQueue = new Queue<string>();
        const float BannerTurnSeconds = 1.3f;
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
            // The arenas are always dimly lit; the mushroom forest and the lava fields glow a little of themselves.
            var arenas = WorldLights.ArenaAmbient(client.Arenas, client.ArenaRadius);
            _arenaGlow = (x, y) => Mathf.Max(arenas(x, y), WorldLights.BiomeGlow(client.Biomes[x, y]));
            BuildDark(DarkSize);
            _self = MakeBody(BodyKind.Player, "나", 0, World.PlayerRadius, SelfTunic);
            var start = client.Position;
            _cameraTarget = new Vector3(start.X, start.Y, _camera.transform.position.z);
            _camera.transform.position = _cameraTarget;
        }

        // The awake boss on screen, for the health bar at the top.
        public EntityView? ActiveBoss { get; private set; }

        // A fight is on (for the music, 2026-11-09): a monster within CombatReach chasing, winding up, striking or
        // charging, or the player just hurt or hitting something; it stays on CombatHold seconds after the last of that.
        public bool InCombat => Time.time < _combatUntil;
        // The id of a boss on screen, awake or not.
        public string BossInSight { get; private set; }
        const float CombatReach = 10;
        const float CombatHold = 5;
        float _combatUntil = -1;

        public void Render(float tickFraction, float deltaTime)
        {
            _hurtGlow = Mathf.Max(0, _hurtGlow - deltaTime);
            // A heavy blow of one's own holds the picture still for a moment (hit-stop); the world goes on underneath and
            // the events wait. Off with screen shake.
            if (_hitStop > 0 && Settings.ScreenShake && Feel.HitStop)
            {
                _hitStop -= deltaTime;
                _shake *= Mathf.Exp(-10 * deltaTime);
                _camera.transform.position = _cameraTarget + (Vector3)(UnityEngine.Random.insideUnitCircle * _shake);
                return;
            }
            _hitStop = 0;
            var selfPosition = Vec2.Lerp(_client.PreviousPosition, _client.Position, Mathf.Clamp01(tickFraction));
            var (selfTint, selfGlow) = StatusLook(_client.SelfStatus, Color.white, Color.clear);
            Place(_self, selfPosition, _client.Facing, _client.Health, _client.MaxHealth, selfTint, selfGlow, deltaTime,
                weapon: _client.Equipment[ItemSlot.Weapon]?.Base);
            _self.Name = _client.Name;
            _self.Sleeping = _client.Sleeping;
            DrawPet(_client.PlayerId, _self, _client.Pet, deltaTime);
            Footsteps(_self.Position - _self.LastPosition);
            StreamChunks(_self.Position);
            int selfBuffs = 0;
            for (int slot = 0; slot < _client.Skills.Count; slot++)
            {
                if (_client.BuffLeft(slot) > 0) selfBuffs |= 1 << slot;
            }
            ShowAura(_self, _client.PlayerId, selfBuffs);

            _rendered = 1;
            DrawEntities(deltaTime);
            _rendered = 2;
            DrawCrops();
            DrawMachines();
            DrawPeople();
            DrawStoryPeople();
            DrawProps();
            AnnouncePlace();
            DrawChickens();
            SwingDoors();
            DrawMarks();
            _rendered = 3;
            DrawProjectiles();
            DrawTarget();
            SwayScarecrows();

            foreach (var e in _client.TakeEvents()) Show(e);
            _rendered = 4;
            UpdateFlashes(deltaTime);
            UpdateTracers(deltaTime);
            UpdateRain(deltaTime);
            BobBobbers();
            UpdateBits(deltaTime);
            UpdateSpiderRemains();
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                _floating[i].Age += deltaTime;
                if (_floating[i].Age > FloatSeconds) _floating.RemoveAt(i);
            }
            _bannerAge += deltaTime;
            if (_bannerQueue.Count > 0 && _bannerAge >= BannerTurnSeconds)
            {
                _banner = _bannerQueue.Dequeue();
                _bannerAge = 0;
            }

            _cameraTarget = Vector3.Lerp(_cameraTarget, new Vector3(selfPosition.X, selfPosition.Y, _cameraTarget.z), 1 - Mathf.Exp(-12 * deltaTime));
            // Off in the menu (Settings.ScreenShake), hits still flash and sound but the view holds still.
            _shake = Settings.ScreenShake && Feel.Shake ? _shake * Mathf.Exp(-10 * deltaTime) : 0;
            var jolt = (Vector3)(UnityEngine.Random.insideUnitCircle * _shake);
            _camera.transform.position = _cameraTarget + jolt;
            UpdateDark(selfPosition);
        }

        Texture2D _fog;

        void DrawWeather()
        {
            var weather = World.WeatherOf(Calendar.Day(_client.Clock));
            var before = GUI.color;
            if (weather == Weather.Fog)
            {
                _fog ??= MakeFog();
                // Two layers of the same soft noise drifting slowly past each other.
                float w = Ui.Width, h = Ui.Height, size = Mathf.Max(w, h) * 1.2f;
                for (int layer = 0; layer < 2; layer++)
                {
                    float drift = Time.time * (layer == 0 ? 6 : -4);
                    float x = Mathf.Repeat(drift, size) - size;
                    GUI.color = new Color(0.78f, 0.8f, 0.84f, layer == 0 ? 0.32f : 0.22f);
                    GUI.DrawTexture(new Rect(x, -size * 0.1f, size, size), _fog);
                    GUI.DrawTexture(new Rect(x + size, -size * 0.1f, size, size), _fog);
                }
            }
            if (_lightning > 0)
            {
                GUI.color = new Color(0.9f, 0.95f, 1f, Mathf.Clamp01(_lightning / 0.18f) * 0.45f);
                GUI.DrawTexture(new Rect(0, 0, Ui.Width, Ui.Height), Texture2D.whiteTexture);
            }
            GUI.color = before;
        }

        // Soft blotchy white, tiling, for fog: value noise smoothed over an 8-cell lattice that wraps.
        static Texture2D MakeFog()
        {
            const int size = 64, cells = 8;
            var lattice = new float[cells, cells];
            var random = new System.Random(5);
            for (int y = 0; y < cells; y++)
            {
                for (int x = 0; x < cells; x++) lattice[x, y] = (float)random.NextDouble();
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x * cells / (float)size, v = y * cells / (float)size;
                    int x0 = (int)u, y0 = (int)v;
                    float fu = Mathf.SmoothStep(0, 1, u - x0), fv = Mathf.SmoothStep(0, 1, v - y0);
                    float a = Mathf.Lerp(lattice[x0 % cells, y0 % cells], lattice[(x0 + 1) % cells, y0 % cells], fu);
                    float b = Mathf.Lerp(lattice[x0 % cells, (y0 + 1) % cells], lattice[(x0 + 1) % cells, (y0 + 1) % cells], fu);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Lerp(a, b, fv) * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Clear in the middle and thickening toward the edges and most in the corners, white, for GUI.color to tint.
        static Texture2D MakeVignette()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2 - 1;
                    float v = (y + 0.5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                    float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Names, damage numbers and banners. Call from OnGUI.
        public void DrawGui()
        {
            // Fog drifts over the view, and a storm's lightning flashes white.
            if (Event.current.type == EventType.Repaint) DrawWeather();
            // Hurt: the screen's edges glow red for a moment, more for a harder blow.
            if (_hurtGlow > 0 && Feel.HurtGlow && Event.current.type == EventType.Repaint)
            {
                _vignette ??= MakeVignette();
                var before = GUI.color;
                GUI.color = new Color(0.9f, 0.1f, 0.08f, _hurtGlowStrength * _hurtGlow / HurtGlowSeconds);
                GUI.DrawTexture(new Rect(0, 0, Ui.Width, Ui.Height), _vignette);
                GUI.color = before;
            }
            // Centred on their spot and allowed past their box, so a tall font is never cut off at the bottom.
            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            _bannerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            Ui.Text(_labelStyle, 15, bold: true);
            Ui.Text(_bannerStyle, 34, bold: true);

            Label(_self.Position + new Vector3(0, _self.Top + 0.4f, 0), _self.Name, Color.white);
            foreach (var body in _others.Values)
            {
                if (!string.IsNullOrEmpty(body.Name)) Label(body.Position + new Vector3(0, body.Top + 0.4f, 0), body.Name, Color.white);
            }
            // Whoever is lying in bed, with a slow "Zz" rising over them.
            foreach (var body in _others.Values.Append(_self))
            {
                if (body.Sleeping) Label(body.Position + new Vector3(0.3f, body.Top + 0.8f + 0.1f * Mathf.Sin(Time.time * 2), 0), "Zz", SleepLabel);
            }
            foreach (var drop in _drops.Values)
            {
                if (Vector3.Distance(drop.Position, _self.Position) < 4) Label(drop.Position + new Vector3(0, 0.5f, 0), drop.Name, drop.Color);
            }
            foreach (var machine in _client.Machines())
            {
                var at = new Vector3(machine.X + 0.5f, machine.Y + 0.5f, 0);
                if (!machine.On && Vector3.Distance(at, _self.Position) < 6) Label(at, "꺼짐", OffLabel);
                // An assembler nearby shows what it makes.
                else if (World.IsAssembler(machine.Kind) && Vector3.Distance(at, _self.Position) < 4)
                    Label(at + new Vector3(0, 0.7f, 0), machine.Recipe?.Name ?? "정하지 않음", Color.white);
                // A pole or generator nearby shows what its grid gives and draws.
                else if (machine.Supply >= 0 && Vector3.Distance(at, _self.Position) < 4)
                    Label(at + new Vector3(0, 0.9f, 0), $"전력 {machine.Supply}/{machine.Demand}", machine.Supply >= machine.Demand ? PowerLabel : OffLabel);
            }
            DrawReelGauge();
            // The people of the story: their names, gold for whoever a story (main or side) waits on now.
            var waiting = _client.CurrentSteps.Select(s => s.Step.Who).ToList();
            foreach (var (character, at) in _storyInView)
            {
                var head = new Vector3(at.X, at.Y + 0.95f, 0);
                if (Vector3.Distance(head, _self.Position) >= 7) continue;
                bool turn = waiting.Contains(character);
                Label(head, turn ? $"! {character.Name}" : character.Name, turn ? StoryLabel : Color.white);
            }
            // A signpost close by says where its road goes and which way (2026-11-11).
            foreach (var (sx, sy, toward) in _client.Signposts)
            {
                var post = new Vector3(sx + 0.5f, sy + 2.2f, 0);
                if (Vector3.Distance(post, _self.Position) >= 3.5f) continue;
                var there = _client.SiteOf(toward);
                if (there == null) continue;
                // Through Ui.Wrap, so the playtests read it as a person does.
                string way = Ui.Wrap($"{toward.Name} · {Compass(there.Center.X - (sx + 0.5f), there.Center.Y - (sy + 0.5f))}쪽", _labelStyle, 600);
                Label(post, way, new Color(1f, 0.95f, 0.75f));
            }

            // Each person's name and how well this player knows them, close by; the one near enough to deal with also
            // says where to (the bag's tab named for them).
            var dealing = _client.NpcNear();
            foreach (var (x, y, npc) in _stalls)
            {
                // Above the awning on the tile over the stall (2026-11-09), not across it.
                var at = new Vector3(x + 0.5f, y + 2.05f, 0);
                if (Vector3.Distance(at, _self.Position) >= 5) continue;
                bool near = dealing != null && dealing.Value.Npc == npc;
                Label(at, $"{npc.Name} 호감 {_client.HeartsWith(npc)}/{World.MaxHearts}" + (near ? " · 가방에서 거래" : ""), PowerLabel);
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
            BossInSight = null;
            _bossInView = null;
            // The samurai's dimming holds only while it is there to set it (WorldView.Bosses.cs).
            if (_dim != null) _dim.color = Color.clear;
            _seen.Clear();
            foreach (var view in _client.Others())
            {
                _seen.Add(view.Id);
                if (view.Kind == EntityKind.Monster && view.EnemyState != EnemyState.Idle
                    && Vector2.Distance(new Vector2(view.Position.X, view.Position.Y), _self.Position) < CombatReach)
                {
                    _combatUntil = Time.time + CombatHold;
                }
                if (view.Kind == EntityKind.Drop)
                {
                    DrawDrop(view);
                    continue;
                }
                if (!_others.TryGetValue(view.Id, out var body))
                {
                    body = view.Kind == EntityKind.Boss ? MakeBody(BodyKind.Boss, view.Boss.Id, view.Id, view.Radius, BossColor)
                        : view.Kind == EntityKind.Monster ? MakeBody(BodyKind.Monster, view.Monster.Id, view.Id, view.Radius, ColorOf(view.Monster))
                        : view.Kind == EntityKind.Minion ? MakeBody(BodyKind.Minion, view.Skill.Id, view.Id, view.Radius, Sprites.ToRgba(Palette.Of(view.Skill.Tags)))
                        : MakeBody(BodyKind.Player, "player", view.Id, view.Radius, OtherTunics[view.Id % OtherTunics.Length]);
                    _others[view.Id] = body;
                }
                body.Name = view.IsPlayer ? view.Name : view.Kind == EntityKind.Boss && view.BossState != BossState.Dormant ? view.Boss.Name : null;
                if (view.Kind == EntityKind.Boss) TrackBoss(body, view);
                if (view.Kind == EntityKind.Monster)
                {
                    // Its voice: a cry on coming for someone (not more than every CrySeconds), a sound as it winds up.
                    var was = body.EnemyState;
                    body.EnemyState = view.EnemyState;
                    var voiceAt = new Vector3(view.Position.X, view.Position.Y, 0);
                    if (was != EnemyState.Windup && view.EnemyState == EnemyState.Windup)
                        Sounds.PlayCreatureAt(view.Monster.Id, Voice.Attack, voiceAt, _self.Position);
                    else if (was == EnemyState.Idle && view.EnemyState == EnemyState.Chase && Time.time >= body.CryAt)
                    {
                        body.CryAt = Time.time + CrySeconds;
                        Sounds.PlayCreatureAt(view.Monster.Id, Voice.Cry, voiceAt, _self.Position, 0.5f);
                    }
                }
                if (view.IsPlayer)
                {
                    // Other players' footsteps, quieter than one's own.
                    var now = new Vector3(view.Position.X, view.Position.Y, 0);
                    float moved = (now - body.LastStep).magnitude;
                    body.LastStep = now;
                    if (moved < 1)
                    {
                        body.Stride += moved;
                        if (body.Stride >= StepLength)
                        {
                            body.Stride -= StepLength;
                            if (_self != null) Sounds.StepAt(GroundUnder(now), now, _self.Position, 0.2f);
                        }
                    }
                }
                body.Sleeping = view.IsPlayer && PlayerPhase.IsSleeping(view.Phase);
                if (view.IsPlayer) DrawPet(view.Id, body, PlayerPhase.PetOf(view.Phase), deltaTime);

                var tint = Color.white;
                var glow = Color.clear;
                float pulse = 0;
                if (view.Kind == EntityKind.Monster && view.EnemyState == EnemyState.Windup)
                {
                    glow = Faded(WindupColor, 0.35f + 0.25f * Mathf.Sin(Time.time * 30));
                    pulse = 0.06f;
                }
                if (view.Kind == EntityKind.Monster && view.EnemyState == EnemyState.Charge) glow = Faded(ChargeColor, 0.45f);
                (tint, glow) = StatusLook(view.Status, tint, glow);
                // A rift's monsters glow violet; its guardian more, and has a name.
                if (view.Kind == EntityKind.Monster && view.Phase == World.DefenseMonster && glow.a < 0.01f) glow = Faded(DefenseColor, 0.2f);
                else if (view.Kind == EntityKind.Monster && view.Phase >= World.RiftMonster && glow.a < 0.01f)
                    glow = Faded(RiftColor, view.Phase == World.RiftGuardian ? 0.35f + 0.15f * Mathf.Sin(Time.time * 4) : 0.18f);
                if (view.Kind == EntityKind.Monster && view.Phase == World.RiftGuardian) body.Name = "균열 수호자";
                // A spirit flickers in its last two seconds (its health counts down the ticks it has left).
                if (view.Kind == EntityKind.Minion && view.Health < World.TicksPerSecond * 2) tint = new Color(1, 1, 1, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 12)));
                if (view.Kind == EntityKind.Boss)
                {
                    WatchBoss(view);
                    switch (view.BossState)
                    {
                        case BossState.Dormant:
                            tint = DormantTint;
                            break;
                        case BossState.Telegraph:
                            // The same glow for the same kind of attack on every boss (World.TellOf, Docs/Bosses.md 전조 말),
                            // but none for an attack only the boss's wind-up announces (AttackWarning.Hidden).
                            if (!Unmarked(view)) glow = TellGlow(view.Tell);
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
                    BossInSight = view.Boss.Id;
                }
                Place(body, view.Position, view.Facing, view.Health, view.MaxHealth, tint, glow, deltaTime, pulse,
                    showHealth: view.Kind != EntityKind.Boss && view.Kind != EntityKind.Minion,
                    weapon: view.Weapon);
                if (view.Kind == EntityKind.Boss) BossExtras(body, view);
                if (view.IsPlayer)
                {
                    _buffBits[view.Id] = view.State;
                    ShowAura(body, view.Id, view.State);
                }
            }

            _gone.Clear();
            _departed.Clear();
            foreach (var pair in _others)
            {
                if (_seen.Contains(pair.Key)) continue;
                _departed[pair.Key] = new Departed(pair.Value.Position, BodyColor(pair.Value), pair.Value.Kind, pair.Value.Id, pair.Value.SpiderView, pair.Value.SpiderTurned);
                // A boss out of sight may go back to sleep (its arena empty) unseen, so it counts as asleep: seen awake
                // again, it is announced again. Its name stays for the "처치!" banner, since a boss that falls leaves
                // the view in the same update its death arrives.
                if (pair.Value.Kind == BodyKind.Boss && _bossesSeen.TryGetValue(pair.Key, out var left))
                    _bossesSeen[pair.Key] = (BossState.Dormant, 1, left.Name);
                if (pair.Value.Mound != null) Object.Destroy(pair.Value.Mound.gameObject);
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
                // Under the level it is made for, a word of warning with it (bosses.csv level).
                ShowBanner(view.Boss.Level > _client.Level ? $"{view.Boss.Name} 등장! (권장 레벨 {view.Boss.Level})" : $"{view.Boss.Name} 등장!");
                Sounds.Play(Sound.Wake);
            }
            else if (known && view.Phase > before.Phase)
            {
                ShowBanner($"{view.Phase}페이즈!");
                Sounds.Play(Sound.Roar);
                // The boss flares up: a ring bursting out and blades of light whirling round it, and the ground shakes.
                var boss = Find(view.Id);
                if (boss != null)
                {
                    AddFlash(boss.Position, boss.Diameter * 2.2f, WindupColor, FlashKind.Ring);
                    AddFlash(boss.Position, boss.Diameter * 1.4f, ImpactGold, FlashKind.Vortex);
                    AddFlash(boss.Position, boss.Diameter, WindupColor, FlashKind.Burst);
                    _shake = Mathf.Max(_shake, 0.35f);
                }
            }
            // Shots and summons leave nothing on the ground, so their warning starts with a ring off the boss in their
            // colour as well as the glow.
            else if (known && view.BossState == BossState.Telegraph && before.State != BossState.Telegraph && (view.Tell == Tell.Barrage || view.Tell == Tell.Call))
            {
                var boss = Find(view.Id);
                if (boss != null) AddFlash(boss.Position, boss.Diameter * 1.3f, view.Tell == Tell.Call ? CallColor : Color.white, FlashKind.Ring);
            }
            _bossesSeen[view.Id] = (view.BossState, view.Phase, view.Boss.Name);
        }

        static readonly Color CallColor = new Color(0.75f, 0.45f, 1f);

        // A winding-up boss's glow for each kind of warning: red for marks on the ground, yellow for a charge, gold to
        // gather, a quick white flicker for shots, violet for monsters called.
        static Color TellGlow(Tell tell) => tell switch
        {
            Tell.Dash => Faded(PathColor, 0.45f + 0.15f * Mathf.Sin(Time.time * 20)),
            Tell.Gather => Faded(ShareColor, 0.4f + 0.2f * Mathf.Sin(Time.time * 8)),
            Tell.Barrage => Faded(Color.white, Mathf.Sin(Time.time * 40) > 0 ? 0.55f : 0.15f),
            Tell.Call => Faded(CallColor, 0.45f + 0.15f * Mathf.Sin(Time.time * 6)),
            _ => Faded(DangerColor, 0.3f + 0.2f * Mathf.Sin(Time.time * 20)),
        };

        // The banner while it shows, for the playtests (GameRoot.Banner).
        internal string ShowingBanner => _bannerAge < BannerSeconds ? _banner : null;

        // The words floating over the world (damage, "곡괭이가 필요하다", a level), for the playtests.
        internal IEnumerable<string> FloatingTexts => _floating.Select(f => f.Text);

        // A banner shows at once, or after the one still fresh. One that comes first (a boss's fall) takes the screen
        // now and the fresh one waits behind it. Levels gained together show only the last ("레벨 5!", not 2 to 5).
        public void ShowBanner(string text, bool first = false)
        {
            bool fresh = _bannerAge < BannerTurnSeconds && _banner.Length > 0;
            if (IsLevel(text))
            {
                var others = _bannerQueue.Where(b => !IsLevel(b)).ToList();
                _bannerQueue.Clear();
                foreach (string other in others) _bannerQueue.Enqueue(other);
                if (fresh && IsLevel(_banner))
                {
                    _banner = text;
                    return;
                }
            }
            if (fresh && first && text != _banner)
            {
                var waiting = _bannerQueue.ToList();
                _bannerQueue.Clear();
                _bannerQueue.Enqueue(_banner);
                foreach (string other in waiting) _bannerQueue.Enqueue(other);
            }
            else if (fresh)
            {
                if (text != _banner && !_bannerQueue.Contains(text)) _bannerQueue.Enqueue(text);
                return;
            }
            _banner = text;
            _bannerAge = 0;
        }

        static bool IsLevel(string banner) => banner.StartsWith("레벨 ", StringComparison.Ordinal);

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
                    if (hazard.Warning != AttackWarning.Hidden && hazard.Shape != HazardShape.Web) Hear(Sound.Warning, new Vector3(hazard.Position.X, hazard.Position.Y, 0));
                }
                FallingRock(mark, hazard, view.Progress);
                // A web the venom spider left on the floor (2026-11-13): drawn as a web, not a warning.
                if (hazard.Shape == HazardShape.Web)
                {
                    SpiderWeb(mark, view);
                    continue;
                }
                // An attack only the boss's wind-up announces lays nothing to see: its mark is kept (for its blast
                // when it lands) but clear (AttackWarning.Hidden, 2026-11-12).
                if (hazard.Warning == AttackWarning.Hidden)
                {
                    mark.Edge.color = Color.clear;
                    mark.Fill.color = Color.clear;
                    continue;
                }

                var color = hazard.Shape == HazardShape.Share ? ShareColor : hazard.Harmful ? DangerColor : PathColor;
                // A shared circle's edge pulses, so it reads as a place to gather rather than to flee.
                float edge = hazard.Shape == HazardShape.Share ? 0.35f + 0.25f * Mathf.Sin(Time.time * 8) : 0.22f;
                mark.Edge.color = new Color(color.r, color.g, color.b, edge);
                mark.Fill.color = new Color(color.r, color.g, color.b, 0.25f + 0.3f * view.Progress);
                switch (hazard.Shape)
                {
                    case HazardShape.Circle:
                    case HazardShape.Share:
                        mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2 * view.Progress);
                        break;
                    case HazardShape.Ring:
                        // The whole ring darkens; its hole stays clear.
                        mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2);
                        mark.Fill.color = new Color(color.r, color.g, color.b, 0.1f + 0.4f * view.Progress);
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
                if (pair.Value.Rock != null) Object.Destroy(pair.Value.Rock.gameObject);
                // A web gone (its time over, or plucked) fades away rather than vanishing.
                if (pair.Value.State.Shape == HazardShape.Web && pair.Value.Fill.color.a > 0)
                {
                    var web = pair.Value.Fill;
                    web.transform.SetParent(_root.transform, true);
                    _bits.Add(new Bit { Renderer = web, Velocity = Vector3.zero, Color = web.color, Life = 0.35f, Drag = 0 });
                }
                Object.Destroy(pair.Value.Root.gameObject);
                _gone.Add(pair.Key);
                _landed[pair.Key] = (pair.Value.State, Time.time);
            }
            foreach (int id in _gone) _marks.Remove(id);
            if (_landed.Count > 32)
            {
                foreach (int old in _landed.Where(p => Time.time - p.Value.Time > 2).Select(p => p.Key).ToList()) _landed.Remove(old);
            }
        }

        // A telegraph landing: a blast that fills its shape, so the hit reads where it fell.
        void HazardLanded(int id)
        {
            HazardState hazard;
            if (_marks.TryGetValue(id, out var mark)) hazard = mark.State;
            else if (_landed.TryGetValue(id, out var gone)) hazard = gone.State;
            else return;
            if (!hazard.Harmful) return;
            if (VeinEaterLanded(hazard) || KnightLanded(hazard) || SpiderLanded(hazard)) return;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (hazard.Shape)
            {
                case HazardShape.Circle:
                    AddFlash(at, hazard.Size, BlastColor, FlashKind.Burst);
                    break;
                case HazardShape.Share:
                    AddFlash(at, hazard.Size, ShareColor, FlashKind.Burst);
                    AddFlash(at, hazard.Size * 1.2f, ShareColor, FlashKind.Ring);
                    break;
                case HazardShape.Ring:
                    // A shockwave out to the edge, and blasts round the band itself.
                    AddFlash(at, hazard.Size, BlastColor, FlashKind.Ring);
                    float band = (hazard.Size + hazard.Width) / 2;
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        AddFlash(at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * band, Mathf.Max(0.5f, (hazard.Size - hazard.Width) / 2), BlastColor, FlashKind.Burst);
                    }
                    break;
                case HazardShape.Line:
                    for (float d = 0.6f; d < hazard.Size; d += 1.4f) AddFlash(at + along * d, Mathf.Max(0.45f, hazard.Width / 2 + 0.2f), BlastColor, FlashKind.Burst);
                    break;
                case HazardShape.Cone:
                    AddFlash(at + along * (hazard.Size * 0.55f), hazard.Size * 0.5f, BlastColor, FlashKind.Burst);
                    break;
            }
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
                case HazardShape.Share:
                    edge = Shapes.Circle;
                    fill = Shapes.Circle;
                    break;
                case HazardShape.Ring:
                    edge = Shapes.Donut(hazard.Size > 0 ? hazard.Width / hazard.Size : 0);
                    fill = edge;
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
                State = hazard,
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
                renderer.sprite = CropSprite(crop, stage, crop.IsWet(now));
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
        static Sprite CropSprite(Crop crop, int stage, bool wet)
        {
            string id = crop.Definition.Id;
            return Sprites.Standing($"crop/{id}/{stage}/{(wet ? "wet" : "dry")}", () => ItemArt.Crop(id, stage, wet));
        }

        // Each machine's facing part, turned the way it faces, and the load on each conveyor moving along it toward
        // the front. Belts run at their real speed: 2 pixels a frame of the belt, 16 frames a second, is a tile in
        // half a second.
        void DrawMachines()
        {
            _seen.Clear();
            int beltFrame = Mathf.FloorToInt(Time.time * 16) % ItemArt.BeltFrames;
            // A fast belt runs twice as fast, so its marks do too.
            int fastFrame = Mathf.FloorToInt(Time.time * 32) % ItemArt.BeltFrames;
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
                    : machine.Kind == TileType.Conveyor ? BeltSprites[machine.On ? beltFrame : 0]
                    : machine.Kind == TileType.FastConveyor ? FastBeltSprites[machine.On ? fastFrame : 0]
                    : machine.Kind == TileType.UndergroundBelt ? UndergroundSprites[machine.On ? beltFrame : 0]
                    : machine.Kind == TileType.PowerPole || machine.Kind == TileType.SolarPanel || machine.Kind == TileType.TeslaTower ? null
                    : machine.Kind == TileType.WaterWheel ? WheelSprite
                    : machine.Kind == TileType.Turret ? TurretBarrelSprite
                    : machine.Kind == TileType.Splitter ? SplitterSprite
                    : machine.Kind == TileType.Arm ? ArmSprite
                    : machine.Kind == TileType.Sprinkler ? SprinklerSprite
                    : machine.Kind == TileType.Harvester ? ReelSprite
                    : machine.Kind == TileType.Filter ? SplitterSprite
                    // A generator's flame only while it burns.
                    : machine.Kind == TileType.Generator ? (machine.On && machine.Progress > 0 ? FlameSprites[Mathf.FloorToInt(Time.time * 8) % ItemArt.FlameFrames] : null)
                    : machine.Kind == TileType.Lever ? LeverSprite
                    // A big machine's whole body, over its four tiles (the map draws none of them).
                    : machine.Kind == TileType.BigAssembler ? BigAssemblerSprite
                    : machine.Kind == TileType.BigSmelter ? BigSmelterSprite
                    : ChevronSprite;
                int size = World.SizeOf(machine.Kind);
                // A machine standing as a block (2026-11-11, PropArt.Raises): its parts on its top, that much higher.
                float lift = PropArt.Raises(machine.Kind) ? PropArt.LiftOf(machine.Kind) / (float)TileArt.Size
                    : size > 1 ? PropArt.BigLift / (float)TileArt.Size : 0;
                // A big machine's body stands from its foot; what it carries shows on its top.
                if (size > 1) center = new Vector3(machine.X + size / 2f, machine.Y + size / 2f, 0);
                parts.Part.transform.localPosition = size > 1 ? new Vector3(center.x, machine.Y, 0) : center + new Vector3(0, lift, 0);
                center += new Vector3(0, lift, 0);
                // Switched off, the moving part stands still and greys out; powered by a generator, it glows a little.
                parts.Part.color = !machine.On ? OffTint : machine.Powered ? PoweredTint : Color.white;
                // A robot arm waits pointing back at where it picks up, and swings over to the front as it carries.
                float swing = machine.Kind == TileType.Arm ? Mathf.Lerp(180, 0, machine.Carried != null ? machine.Progress : 0) : 0;
                // A sprinkler's head spins while it runs, whichever way it was placed.
                float turn = machine.Kind == TileType.Sprinkler ? (machine.On ? Time.time * 240 % 360 : 0)
                    // A lever leans right when on, left when off, and is not turned by its direction.
                    : machine.Kind == TileType.Lever ? (machine.On ? -30 : 30)
                    : machine.Kind == TileType.Generator ? 0
                    // A water wheel turns while there is water beside it.
                    : machine.Kind == TileType.WaterWheel ? (machine.On && machine.Progress > 0 ? -Time.time * 90 % 360 : 0)
                    // A turret points where it last shot.
                    : machine.Kind == TileType.Turret ? _turretAim.GetValueOrDefault(key, 90)
                    : size > 1 ? 0
                    : machine.Direction * 90 + swing;
                parts.Part.transform.localRotation = Quaternion.Euler(0, 0, turn);
                // On a raised body or a big machine (a block itself), sorted by its foot just in front of the body, so people
                // pass in front of and behind it; on a flat one, on the floor.
                int body = 10000 - Mathf.RoundToInt(machine.Y * 10);
                bool standing = lift > 0 || size > 1;
                parts.Part.sortingOrder = standing ? body + 1 : machine.Kind == TileType.Arm ? -13 : -15;
                parts.Load.sortingOrder = standing ? body + 2 : machine.Kind == TileType.Arm ? -12 : -14;
                // A filter shows the material it picks out while it carries nothing; a generator's fuel is not shown.
                bool carrying = machine.Carried != null && machine.Kind != TileType.Generator;
                bool picking = machine.Kind == TileType.Filter && !carrying && machine.Settings.Count > 0;
                parts.Load.enabled = carrying || picking;
                if (carrying) parts.Load.sprite = Sprites.Icon(machine.Carried);
                // With several picks, it shows each in turn.
                else if (picking) parts.Load.sprite = Sprites.Icon(machine.Settings[Mathf.FloorToInt(Time.time) % machine.Settings.Count]);
                parts.Load.transform.localScale = Vector3.one * (picking ? 0.6f : 1);
                if (machine.Kind == TileType.Arm)
                {
                    // In the claw.
                    float angle = (machine.Direction * 90 + swing) * Mathf.Deg2Rad;
                    parts.Load.transform.localPosition = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.45f;
                }
                else if (machine.Kind == TileType.Splitter || machine.Kind == TileType.Filter)
                {
                    parts.Load.transform.localPosition = center;
                }
                else
                {
                    parts.Load.transform.localPosition = center + new Vector3(step.X, step.Y, 0) * (machine.Progress - 0.5f);
                }
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
            DrawWires();
        }

        // The person behind each stall in view, drawn as themselves and busy with their trade (PeopleArt), with the
        // counter drawn again over their legs.
        readonly Dictionary<int, (SpriteRenderer Figure, SpriteRenderer Counter, SpriteRenderer Awning)> _people =
            new Dictionary<int, (SpriteRenderer, SpriteRenderer, SpriteRenderer)>();
        readonly List<(int X, int Y, NpcDefinition Npc)> _stalls = new List<(int X, int Y, NpcDefinition Npc)>();
        const int PeopleRange = 18;

        void DrawPeople()
        {
            var map = _client.Map;
            _seen.Clear();
            _stalls.Clear();
            int cx = Mathf.FloorToInt(_self.Position.x), cy = Mathf.FloorToInt(_self.Position.y);
            var people = _client.Npcs?.Npcs;
            for (int y = cy - PeopleRange; y <= cy + PeopleRange && people != null; y++)
            {
                for (int x = cx - PeopleRange; x <= cx + PeopleRange; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    foreach (var npc in people)
                    {
                        if (map[x, y] == npc.Tile) _stalls.Add((x, y, npc));
                    }
                }
            }
            // Each person loops their own little motion (PeopleArt), started at a different time by where they stand.
            foreach (var (x, y, npc) in _stalls)
            {
                var tunic = Rgba.Hex(npc.Color);
                int frame = Mathf.FloorToInt(Time.time * PeopleArt.FramesPerSecond + x * 7 + y * 3) % PeopleArt.Frames;
                int stand = frame % CreatureArt.StandFrames;
                var figure = Sprites.Standing($"npc/{npc.Id}/{frame}",
                    () => PeopleArt.Person(npc.Id, tunic, frame) ?? CreatureArt.Player(Look.Down, stand, tunic, Pose.Stand));
                int key = y * map.Width + x;
                _seen.Add(key);
                if (!_people.TryGetValue(key, out var person))
                {
                    var tile = npc.Tile;
                    person = (Renderer(_root.transform, "Merchant", null, Color.white),
                        Renderer(_root.transform, "Counter", Sprites.Centered("npc/counter", () => TileArt.MerchantCounter), Color.white),
                        Renderer(_root.transform, "Awning", Sprites.Centered($"npc/awning/{tile}", () => TileArt.Awning(tile)), Color.white));
                    _people[key] = person;
                }
                int order = 10000 - Mathf.RoundToInt((y + 0.3f) * 10);
                person.Figure.sprite = figure;
                person.Figure.transform.localPosition = new Vector3(x + 0.5f, y + 0.3f, 0);
                person.Figure.sortingOrder = order;
                person.Counter.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0);
                person.Counter.sortingOrder = order + 2;
                // The awning on the tile above, over the person's head (and over anyone walking behind the stall).
                person.Awning.transform.localPosition = new Vector3(x + 0.5f, y + 1.5f, 0);
                person.Awning.sortingOrder = order + 3;
            }
            _gone.Clear();
            foreach (var pair in _people)
            {
                if (_seen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.Figure.gameObject);
                Object.Destroy(pair.Value.Counter.gameObject);
                Object.Destroy(pair.Value.Awning.gameObject);
                _gone.Add(pair.Key);
            }
            foreach (int key in _gone) _people.Remove(key);
        }

        // The people of the story at their places (Docs/Story.md, 2026-11-10), each in their look (PeopleArt.Look),
        // breathing and blinking; their names are drawn in DrawGui.
        readonly Dictionary<string, SpriteRenderer> _storyPeople = new Dictionary<string, SpriteRenderer>();
        readonly HashSet<string> _storySeen = new HashSet<string>();
        readonly List<string> _storyGone = new List<string>();
        readonly List<(CharacterDefinition Character, Vec2 At)> _storyInView = new List<(CharacterDefinition, Vec2)>();

        void DrawStoryPeople()
        {
            _storySeen.Clear();
            _storyInView.Clear();
            var self = new Vec2(_self.Position.x, _self.Position.y);
            long clock = _client.Clock;
            foreach (var site in _client.Places)
            {
                if (Vec2.Distance(self, site.Center) > PeopleRange + site.Radius) continue;
                foreach (var (character, home) in site.People)
                {
                    // Where they are now on their day's round (PlaceSite.PersonAt), and a moment ago, to face the way
                    // they walk and bob as they go.
                    var at = site.PersonAt(character, home, clock, _client.DayMinutes);
                    var before = site.PersonAt(character, home, clock - 6, _client.DayMinutes);
                    if (Vec2.Distance(self, at) > PeopleRange) continue;
                    _storyInView.Add((character, at));
                    var color = Rgba.Hex(string.IsNullOrEmpty(character.Color) ? "#8A7A6A" : character.Color);
                    int frame = Mathf.FloorToInt(Time.time * PeopleArt.FramesPerSecond + home.X * 7 + home.Y * 3) % PeopleArt.Frames;
                    bool walking = Vec2.Distance(at, before) > 0.01f;
                    var figure = Sprites.Standing($"story/{character.Id}/{frame}", () => PeopleArt.Look(character.Look, color, frame));
                    _storySeen.Add(character.Id);
                    if (!_storyPeople.TryGetValue(character.Id, out var renderer))
                    {
                        renderer = Renderer(_root.transform, "Story " + character.Id, null, Color.white);
                        _storyPeople[character.Id] = renderer;
                    }
                    renderer.sprite = figure;
                    if (walking) renderer.flipX = at.X < before.X - 0.005f;
                    float bob = walking ? Mathf.Abs(Mathf.Sin(Time.time * (character.Walk == "run" ? 16 : 10))) * 0.07f : 0;
                    renderer.transform.localPosition = new Vector3(at.X, at.Y - 0.3f + bob, 0);
                    renderer.sortingOrder = 10000 - Mathf.RoundToInt((at.Y - 0.3f) * 10);
                }
            }
            _storyGone.Clear();
            foreach (var pair in _storyPeople)
            {
                if (_storySeen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.gameObject);
                _storyGone.Add(pair.Key);
            }
            foreach (string id in _storyGone) _storyPeople.Remove(id);
        }

        // The structures of the story's places (PropArt, Docs/Story.md 장소 꾸밈) in view: each standing on its
        // footprint's bottom edge, rising above its tiles, in front of or behind people by where its foot is. Walls
        // join their neighbours (PropArt.Sides).
        readonly Dictionary<int, SpriteRenderer> _props = new Dictionary<int, SpriteRenderer>();
        readonly HashSet<int> _propsSeen = new HashSet<int>();
        readonly List<int> _propsGone = new List<int>();
        const int PropRange = 20;

        bool _windowsLit;

        void DrawProps()
        {
            var map = _client.Map;
            var phase = Calendar.Phase(_client.Clock);
            _windowsLit = phase == DayPhase.Evening || phase == DayPhase.Night;
            _propsSeen.Clear();
            int cx = Mathf.FloorToInt(_self.Position.x), cy = Mathf.FloorToInt(_self.Position.y);
            for (int y = cy - PropRange; y <= cy + PropRange; y++)
            {
                for (int x = cx - PropRange - 3; x <= cx + PropRange; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    var type = map[x, y];
                    // The places' structures and the things players build that stand (PropArt.Stands, 2026-11-10).
                    if (!(Structures.IsDecor(type) || PropArt.Stands(type)) || type == TileType.StructurePart) continue;
                    int sides = PropArt.IsWall(type) ? PropArt.Sides(map, x, y) : 0;
                    // Houses show their windows lit from the evening (2026-11-11), dark glass by day.
                    bool lit = PropArt.LitAtNight(type) && _windowsLit;
                    var sprite = PropArt.LitAtNight(type)
                        ? Sprites.Standing($"prop/{type}/{(lit ? "lit" : "day")}", () => lit ? PropArt.Prop(type) : PropArt.Daylit(PropArt.Prop(type)))
                        : Sprites.Standing($"prop/{type}/{sides}", () => PropArt.Prop(type, sides));
                    int key = y * map.Width + x;
                    _propsSeen.Add(key);
                    if (!_props.TryGetValue(key, out var renderer))
                    {
                        renderer = Renderer(_root.transform, "Prop", null, Color.white);
                        _props[key] = renderer;
                    }
                    var (w, _) = Structures.Footprint(type);
                    renderer.sprite = sprite;
                    renderer.transform.localPosition = new Vector3(x + w / 2f, y, 0);
                    renderer.sortingOrder = 10000 - Mathf.RoundToInt(y * 10);
                }
            }
            _propsGone.Clear();
            foreach (var pair in _props)
            {
                if (_propsSeen.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.gameObject);
                _propsGone.Add(pair.Key);
            }
            foreach (int key in _propsGone) _props.Remove(key);
        }

        // Which of eight ways (y up is north).
        static string Compass(float dx, float dy)
        {
            string[] names = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            return names[Mathf.RoundToInt((angle + 360) % 360 / 45f) % 8];
        }

        // The place the player stands in, last announced: entering another shows its name and line (Docs/Story.md).
        PlaceDefinition _placeAnnounced;

        void AnnouncePlace()
        {
            var here = _client.PlaceHere()?.Definition;
            if (here == _placeAnnounced) return;
            _placeAnnounced = here;
            if (here != null) ShowBanner(here.Name);
        }

        readonly List<SpriteRenderer> _chickens = new List<SpriteRenderer>();

        // A coop's chickens strut about in front of it, each on a slow loop of its own, now and then pecking. Only for
        // the look: the coop keeps them (World.Animals.cs).
        void DrawChickens()
        {
            int used = 0;
            foreach (var machine in _client.Machines())
            {
                if (World.HouseOf(machine.Kind) == null) continue;
                // Cows and sheep wander slower and wider than chickens.
                bool cows = machine.Kind == TileType.Barn;
                bool sheep = machine.Kind == TileType.Fold;
                bool big = cows || sheep;
                // Penned in (World.Grazes): they roam the pen's tiles instead, from one to another (2026-11-09).
                var pen = PenFor(machine);
                for (int i = 0; i < machine.Animals; i++)
                {
                    if (used == _chickens.Count) _chickens.Add(Renderer(_root.transform, "Chicken", null, Color.white));
                    var chicken = _chickens[used++];
                    chicken.enabled = true;
                    float t = Time.time * 0.45f + i * 1.7f + machine.X * 0.3f;
                    // Cows wander slower and wider.
                    if (big) t *= 0.6f;
                    var at = new Vector3(machine.X + 0.5f + Mathf.Sin(t) * (big ? 1.6f : 1.1f), machine.Y - (big ? 0.9f : 0.6f) + Mathf.Sin(t * 1.9f + i) * 0.35f, 0);
                    bool grazingNow = false;
                    if (pen != null)
                    {
                        float leg = Time.time / (big ? 5f : 3.5f) + i * 0.37f + machine.X * 0.11f;
                        int k = Mathf.FloorToInt(leg);
                        float f = Mathf.SmoothStep(0, 1, Mathf.Clamp01((leg - k) * 1.6f));
                        var from = pen[(i * 7 + k * 5) % pen.Count];
                        var to = pen[(i * 7 + (k + 1) * 5) % pen.Count];
                        at = new Vector3(Mathf.Lerp(from.X, to.X, f) + 0.5f, Mathf.Lerp(from.Y, to.Y, f) + 0.25f, 0);
                        t = (to.X - from.X) >= 0 ? 0 : Mathf.PI;
                        // Grazing when it has got there.
                        grazingNow = leg - k > 0.65f;
                    }
                    bool pecking = pen != null ? grazingNow : Mathf.Sin(t * 3.1f) > 0.6f;
                    int frame = pecking ? 1 : 0;
                    chicken.sprite = cows ? Sprites.Standing("animal/cow/" + frame, () => ItemArt.Cow(frame))
                        : sheep ? Sprites.Standing("animal/sheep/" + frame, () => ItemArt.Sheep(frame))
                        : Sprites.Standing("animal/chicken/" + frame, () => ItemArt.Chicken(frame));
                    // Facing the way it is going: left while sin falls.
                    chicken.flipX = Mathf.Cos(t) > 0;
                    chicken.transform.localPosition = at;
                    chicken.sortingOrder = 10000 - Mathf.RoundToInt(at.y * 10);
                }
            }
            for (int i = used; i < _chickens.Count; i++) _chickens[i].enabled = false;
        }

        // Doors (2026-11-09): a door stands open while a player is in its doorway or right beside it, and swings shut
        // once they have gone, with a creak and a thud. Only the look: monsters are kept out either way (World rules).
        const float DoorReach = 1.05f;
        // Less than a player stopped against the door stands from its middle (half a tile and their radius).
        const float LockedDoorReach = 0.8f;
        readonly HashSet<int> _openDoors = new HashSet<int>();
        bool _trampleHintShown;
        // How many doors stand open now, for the playtest.
        public int OpenDoors => _openDoors.Count;
        readonly HashSet<int> _doorsNear = new HashSet<int>();
        readonly List<int> _doorsShut = new List<int>();

        void SwingDoors()
        {
            var map = _client.Map;
            if (map == null || _objects == null || _self == null) return;
            _doorsNear.Clear();
            foreach (var body in _others.Values.Append(_self))
            {
                if (body == null) continue;
                int cx = Mathf.FloorToInt(body.Position.x), cy = Mathf.FloorToInt(body.Position.y);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (!map.InBounds(x, y) || !TileMap.IsDoor(map[x, y])) continue;
                        // A locked door opens only for whoever steps into it, its owner: anyone else stops short of it.
                        float reach = map[x, y] == TileType.LockedDoor ? LockedDoorReach : DoorReach;
                        if (Vector2.Distance(body.Position, new Vector2(x + 0.5f, y + 0.5f)) <= reach) _doorsNear.Add(y * map.Width + x);
                    }
                }
            }
            foreach (int key in _doorsNear)
            {
                var cell = new Vector3Int(key % map.Width, key / map.Width, 0);
                bool opened = _openDoors.Add(key);
                // Also after the tiles round it were redrawn closed.
                if (_objects.GetTile(cell) != Sprites.OpenDoor) _objects.SetTile(cell, Sprites.OpenDoor);
                if (opened) Hear(Sound.Door, cell + new Vector3(0.5f, 0.5f, 0));
            }
            _doorsShut.Clear();
            foreach (int key in _openDoors) if (!_doorsNear.Contains(key)) _doorsShut.Add(key);
            foreach (int key in _doorsShut)
            {
                _openDoors.Remove(key);
                var cell = new Vector3Int(key % map.Width, key / map.Width, 0);
                if (!TileMap.IsDoor(map[cell.x, cell.y]) || !Shown(cell.x, cell.y)) continue;
                _objects.SetTile(cell, Sprites.Object(map[cell.x, cell.y]));
                Hear(Sound.DoorShut, cell + new Vector3(0.5f, 0.5f, 0));
            }
        }

        // Each house's pen as last worked out, looked at again every two seconds.
        readonly Dictionary<(int, int), (float At, List<(int X, int Y)> Pen)> _pens = new Dictionary<(int, int), (float, List<(int X, int Y)>)>();

        List<(int X, int Y)> PenFor(MachineView house)
        {
            var key = (house.X, house.Y);
            if (_pens.TryGetValue(key, out var known) && Time.time - known.At < 2) return known.Pen;
            var step = Machine.Steps[house.Direction & 3];
            int fx = house.X + step.X, fy = house.Y + step.Y;
            var pen = World.Grazes(_client.Map, fx, fy, house.Animals) ? World.PenOf(_client.Map, fx, fy) : null;
            _pens[key] = (Time.time, pen);
            return pen;
        }

        readonly List<SpriteRenderer> _wires = new List<SpriteRenderer>();
        readonly List<MachineView> _nodes = new List<MachineView>();

        // A thin copper line between each pair of generators and power poles that are wired together (World.Wired),
        // sagging nowhere: it is seen from above.
        void DrawWires()
        {
            _nodes.Clear();
            foreach (var machine in _client.Machines())
            {
                if (World.IsPowerNode(machine.Kind)) _nodes.Add(machine);
            }
            int used = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var a = _nodes[i];
                    var b = _nodes[j];
                    if (!World.Wired(a.X, a.Y, b.X, b.Y)) continue;
                    if (used == _wires.Count)
                    {
                        var wire = Renderer(_root.transform, "Wire", Shapes.Square, WireColor);
                        wire.sortingOrder = 30;
                        _wires.Add(wire);
                    }
                    var line = _wires[used++];
                    line.enabled = true;
                    // From the top of one pole to the other, where the insulators are.
                    var from = new Vector3(a.X + 0.5f, a.Y + 0.85f, 0);
                    var to = new Vector3(b.X + 0.5f, b.Y + 0.85f, 0);
                    var along = to - from;
                    line.transform.localPosition = (from + to) / 2;
                    line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                    line.transform.localScale = new Vector3(along.magnitude, 1 / 16f, 1);
                }
            }
            for (int i = used; i < _wires.Count; i++) _wires[i].enabled = false;
        }

        static Sprite TurretBarrelSprite => Sprites.Centered("machine/turret", () => ItemArt.TurretBarrel);

        readonly Dictionary<int, float> _turretAim = new Dictionary<int, float>();
        readonly List<(SpriteRenderer Line, float Age)> _tracers = new List<(SpriteRenderer, float)>();
        const float TracerSeconds = 0.12f;

        // A storm: now and then lightning lights the whole cave for a moment through the cracks above, and thunder rolls.
        float _lightning;
        float _nextLightning = 4;

        void UpdateStorm(bool storm, float deltaTime)
        {
            _lightning = Mathf.Max(0, _lightning - deltaTime);
            if (!storm) return;
            _nextLightning -= deltaTime;
            if (_nextLightning > 0) return;
            _nextLightning = 6 + UnityEngine.Random.value * 9;
            _lightning = 0.18f;
            Sounds.Play(Sound.Thunder, 0.8f);
        }

        // On a rainy day thin drops fall through the view: rain from the land above seeping down (World.IsRainy).
        readonly List<SpriteRenderer> _raindrops = new List<SpriteRenderer>();
        static readonly Color RainColor = new Color(0.6f, 0.75f, 1f, 0.35f);
        const int RainDrops = 40;

        void UpdateRain(float deltaTime)
        {
            var weather = World.WeatherOf(Calendar.Day(_client.Clock));
            bool raining = weather is Weather.Rain or Weather.Storm;
            UpdateStorm(weather == Weather.Storm, deltaTime);
            if (!raining)
            {
                foreach (var drop in _raindrops) drop.enabled = false;
                return;
            }
            // A storm pours down harder and slants more.
            bool storm = weather == Weather.Storm;
            int wanted = storm ? RainDrops * 2 : RainDrops;
            var fall = storm ? new Vector3(-0.45f, -1, 0) * 13 : new Vector3(-0.2f, -1, 0) * 9;
            var centre = _camera.transform.position;
            float height = _camera.orthographicSize * 2 + 2;
            float width = height * _camera.aspect + 2;
            for (int i = 0; i < _raindrops.Count; i++) _raindrops[i].enabled = i < wanted;
            while (_raindrops.Count < wanted)
            {
                var drop = Renderer(_root.transform, "Rain", Shapes.Square, RainColor);
                drop.sortingOrder = 29;
                drop.transform.localScale = new Vector3(1 / 16f, 0.5f, 1);
                drop.transform.localPosition = centre + new Vector3((UnityEngine.Random.value - 0.5f) * width, (UnityEngine.Random.value - 0.5f) * height, 0);
                _raindrops.Add(drop);
            }
            foreach (var drop in _raindrops)
            {
                if (!drop.enabled) continue;
                var at = drop.transform.localPosition + fall * deltaTime;
                // Wrapped round the view as it moves.
                if (at.y < centre.y - height / 2) at.y += height;
                if (at.x < centre.x - width / 2) at.x += width;
                if (at.x > centre.x + width / 2) at.x -= width;
                if (at.y > centre.y + height / 2) at.y -= height;
                at.z = 0;
                drop.transform.localPosition = at;
            }
        }

        void AddTracer(Vector3 from, Vector3 to, Color? color = null)
        {
            var line = Renderer(_root.transform, "Tracer", Shapes.Square, color ?? TracerColor);
            line.sortingOrder = 31;
            var along = to - from;
            line.transform.localPosition = (from + to) / 2;
            line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(along.magnitude, 1 / 16f, 1);
            _tracers.Add((line, 0));
        }

        void UpdateTracers(float deltaTime)
        {
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var (line, age) = _tracers[i];
                age += deltaTime;
                if (age >= TracerSeconds)
                {
                    Object.Destroy(line.gameObject);
                    _tracers.RemoveAt(i);
                    continue;
                }
                var tint = line.color;
                line.color = new Color(tint.r, tint.g, tint.b, TracerColor.a * (1 - age / TracerSeconds));
                _tracers[i] = (line, age);
            }
        }

        static Sprite DrillBitSprite => Sprites.Centered("machine/drill", () => ItemArt.DrillBit);

        static Sprite ChevronSprite => Sprites.Centered("machine/chevron", () => ItemArt.Chevron);

        static Sprite SplitterSprite => Sprites.Centered("machine/splitter", () => ItemArt.SplitterArrows);

        static Sprite ArmSprite => Sprites.Centered("machine/arm", () => ItemArt.RobotArm);

        static Sprite SprinklerSprite => Sprites.Centered("machine/sprinkler", () => ItemArt.SprinklerHead);

        static Sprite ReelSprite => Sprites.Centered("machine/reel", () => ItemArt.HarvesterReel);

        static Sprite WheelSprite => Sprites.Centered("machine/wheel", () => ItemArt.WaterWheel);

        static Sprite LeverSprite => Sprites.Centered("machine/lever", () => ItemArt.LeverHandle);

        // The big machines stand as blocks over their four tiles, from their foot (2026-11-11).
        static Sprite BigAssemblerSprite => Sprites.Standing("machine/big_assembler", () => PropArt.Raised(TileArt.BigAssembler, PropArt.BigLift));
        static Sprite BigSmelterSprite => Sprites.Standing("machine/big_smelter", () => PropArt.Raised(TileArt.BigSmelter, PropArt.BigLift));

        static Sprite[] _flameSprites;

        static Sprite[] FlameSprites => _flameSprites ??= Enumerable.Range(0, ItemArt.FlameFrames)
            .Select(frame => Sprites.Centered("machine/flame" + frame, () => ItemArt.Flame(frame))).ToArray();

        static readonly Color PoweredTint = new Color(1f, 1f, 0.7f);

        static readonly Color OffTint = new Color(0.5f, 0.5f, 0.56f);
        static readonly Color LevelColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color FondColor = new Color(1f, 0.55f, 0.7f);
        static readonly Color OffLabel = new Color(1f, 0.55f, 0.45f);
        static readonly Color SleepLabel = new Color(0.75f, 0.85f, 1f);
        static readonly Color RiftColor = new Color(0.7f, 0.45f, 1f);
        // Monsters come for the ward stone glow orange; turret shots are a brief yellow streak.
        static readonly Color DefenseColor = new Color(1f, 0.55f, 0.2f);
        static readonly Color TracerColor = new Color(1f, 0.9f, 0.45f, 0.9f);
        static readonly Color FrostShot = new Color(0.55f, 0.85f, 1f, 0.9f);
        static readonly Color FireShot = new Color(1f, 0.5f, 0.2f, 0.9f);
        static readonly Color BoltShot = new Color(0.65f, 0.9f, 1f, 0.95f);
        static readonly Color WireColor = new Color(0.78f, 0.45f, 0.2f, 0.85f);
        static readonly Color PowerLabel = new Color(1f, 0.85f, 0.45f);

        static Sprite[] _beltSprites;

        static Sprite[] BeltSprites => _beltSprites ??= new[]
        {
            Sprites.Centered("machine/belt0", () => ItemArt.Belt(0)),
            Sprites.Centered("machine/belt1", () => ItemArt.Belt(1)),
            Sprites.Centered("machine/belt2", () => ItemArt.Belt(2)),
            Sprites.Centered("machine/belt3", () => ItemArt.Belt(3)),
        };

        static Sprite[] _fastBeltSprites;

        static Sprite[] FastBeltSprites => _fastBeltSprites ??= Enumerable.Range(0, ItemArt.BeltFrames)
            .Select(frame => Sprites.Centered("machine/fastbelt" + frame, () => ItemArt.Belt(frame, fast: true))).ToArray();

        static Sprite[] _undergroundSprites;

        static Sprite[] UndergroundSprites => _undergroundSprites ??= Enumerable.Range(0, ItemArt.BeltFrames)
            .Select(frame => Sprites.Centered("machine/underground" + frame, () => ItemArt.Underground(frame))).ToArray();

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
                    // The vein eater's are rocks (2026-11-12).
                    bool rock = !projectile.Friendly && _bossInView?.Id == "vein_eater";
                    // The samurai's are crescents of light, the blade's cut flying (2026-11-12).
                    bool crescent = !projectile.Friendly && _bossInView?.Id == "shrine_samurai";
                    // The venom spider's are globs of venom (2026-11-13).
                    bool venom = !projectile.Friendly && _bossInView?.Id == "venom_spider";
                    var sprite = arrow ? ArrowSprite : projectile.Friendly ? OrbSprite : rock ? RockSprite : crescent ? CrescentSprite : venom ? VenomSprite : SporeSprite;
                    renderer = Renderer(_root.transform, "Projectile", sprite, arrow ? Color.Lerp(Color.white, color, 0.3f) : rock || venom ? Color.white : crescent ? SteelColor : color);
                    float across = sprite.rect.height / Sprites.PixelsPerUnit;
                    renderer.transform.localScale = Vector3.one * (arrow ? 1 : Mathf.Max(1, projectile.Radius * (projectile.Friendly ? 2.6f : 2) / across));
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, projectile.Velocity.Angle * Mathf.Rad2Deg);
                    renderer.sortingOrder = 20000;
                    _projectiles[projectile.Id] = renderer;
                }
                renderer.transform.localPosition = new Vector3(projectile.Position.X, projectile.Position.Y, 0);
                // The samurai's crescents shed petals as they fly.
                if (!projectile.Friendly && _bossInView?.Id == "shrine_samurai" && UnityEngine.Random.value < 0.45f)
                    Petals(new Vector3(projectile.Position.X, projectile.Position.Y, 0), -new Vector3(projectile.Velocity.X, projectile.Velocity.Y, 0), 1, 1.5f, 0.8f, 0.2f);
                // The venom spider's drip as they fly.
                if (!projectile.Friendly && _bossInView?.Id == "venom_spider" && UnityEngine.Random.value < 0.35f)
                    Venom(new Vector3(projectile.Position.X, projectile.Position.Y, 0), -new Vector3(projectile.Velocity.X, projectile.Velocity.Y, 0), 1, 0.6f);
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
                    // Players' basic attacks come in a combo (World.ComboStepOf): a downward cut, a cut back up, and a
                    // finisher that winds up, lunges twice as far and sweeps wider. Monsters and bosses only lunge.
                    var kind = World.AttackKindOf(e.Amount);
                    int step = attacker.Kind == BodyKind.Player ? World.ComboStepOf(e.Amount) : 0;
                    bool finisher = step == World.ComboSteps - 1;
                    attacker.Combo = step;
                    attacker.LungeTime = LungeSeconds;
                    attacker.LungeReach = finisher ? 2 : 1;
                    attacker.Lunge = new Vector2(e.Direction.X, e.Direction.Y).normalized;
                    attacker.StrikeLength = finisher ? StrikeSeconds * 1.4f : StrikeSeconds;
                    attacker.StrikeTime = attacker.StrikeLength;
                    // A spirit just darts at what it strikes; the hit makes the sound.
                    if (attacker.Kind == BodyKind.Minion) break;
                    Hear(kind == BasicAttackKind.Arrow ? Sound.Shoot : kind == BasicAttackKind.Bolt ? Sound.Zap : finisher ? Sound.Heavy : Sound.Swing, attacker.Position);
                    if (finisher && attacker == _self) _shake = Mathf.Max(_shake, 0.04f);
                    // Shots show as their projectile (a finisher's is bigger), with a puff where it leaves; only swings
                    // draw the arc. A bow is drawn and let go, a staff raised and pointed.
                    if (kind != BasicAttackKind.Swing && attacker.Kind == BodyKind.Player)
                        Start(attacker, kind == BasicAttackKind.Arrow ? Motion.Shoot : Motion.Zap, ShootSeconds, e.Direction);
                    if (kind != BasicAttackKind.Swing)
                    {
                        if (finisher) AddFlash(attacker.Position + (Vector3)(attacker.Lunge * 0.45f), 0.35f, ImpactWhite, FlashKind.Ring);
                        break;
                    }
                    // A boss lunges but draws no blade's arc in front of it: its blows show where they land (2026-11-12,
                    // the person: the sword-swing arc in front of a boss is better gone).
                    if (attacker.Kind == BodyKind.Boss)
                    {
                        if (attacker.Id == "shrine_samurai") SamuraiDraws(attacker);
                        if (attacker.Id == "venom_spider") SpiderUnleashes(attacker);
                        break;
                    }
                    attacker.SwingTime = SwingSeconds;
                    attacker.Swing.transform.localRotation = Quaternion.Euler(0, 0, e.Direction.Angle * Mathf.Rad2Deg);
                    // The second cut comes back up: the crescent mirrored across the way it points. The finisher is bigger.
                    attacker.Swing.transform.localScale = step == 1 ? new Vector3(1, -1, 1) : finisher ? Vector3.one * 1.4f : Vector3.one;
                    break;
                case SimEventType.Hit:
                    var target = Find(e.Target);
                    if (target == null) break;
                    if (target.Kind == BodyKind.Monster) Sounds.PlayCreatureAt(target.Id, Voice.Hurt, target.Position, _self.Position, 0.45f);
                    target.FlashTime = FlashSeconds;
                    target.HurtTime = HurtSeconds;
                    bool hurtsMe = e.Target == _client.PlayerId;
                    var hitter = Find(e.Source);
                    if (target.Kind == BodyKind.Boss && target.Id == "venom_spider") SpiderStruck(target, hitter, e.Critical);
                    // A finisher's blow: the hitter is a player still in the strike of the combo's last step.
                    bool heavy = hitter != null && hitter.Kind == BodyKind.Player && hitter.Combo == World.ComboSteps - 1 && hitter.StrikeTime > 0;
                    // A weapon's elemental hit (World.AddElements) in its element's colour, a little aside of the blow's own number.
                    var element = World.ElementOfCause(e.Cause);
                    var color = hurtsMe ? new Color(1f, 0.35f, 0.3f) : element != SkillTag.None ? Palette.Of(element) : e.Critical ? new Color(1f, 0.85f, 0.2f) : Color.white;
                    var numberAt = element == SkillTag.None ? target.Position : target.Position + new Vector3(0.35f, 0.2f, 0);
                    if (Feel.Numbers) _floating.Add(new Floating { Position = numberAt, Text = e.Critical ? e.Amount + "!" : e.Amount.ToString(), Color = color });
                    // And a burst of the element where it struck, for everyone to see (2026-11-09).
                    if (element != SkillTag.None) AddFlash(target.Position + new Vector3(0, target.Diameter * 0.15f, 0), 0.5f, Palette.Of(element), FlashKind.Burst);
                    // Knocked back a little, away from whoever struck, and a star on its near side where the blow landed.
                    var away = hitter != null ? (Vector2)(target.Position - hitter.Position) : Vector2.zero;
                    if (away.sqrMagnitude > 1e-4f)
                    {
                        target.Knock = away.normalized;
                        target.KnockTime = KnockSeconds;
                        target.KnockReach = heavy ? 2.5f : e.Critical ? 1.6f : 1;
                    }
                    var landed = target.Position - (Vector3)(away.normalized * target.Diameter * 0.3f) + new Vector3(0, target.Diameter * 0.2f, 0);
                    AddFlash(landed, heavy || e.Critical ? 0.55f : 0.38f, hurtsMe ? ImpactRed : e.Critical ? ImpactGold : ImpactWhite, FlashKind.Impact);
                    if (hurtsMe)
                    {
                        // The harder the blow for the health one has, the more the screen jolts and its edges glow red.
                        float share = Mathf.Clamp01(e.Amount / Mathf.Max(1, _client.MaxHealth) * 4);
                        _shake = Mathf.Max(_shake, 0.12f + 0.25f * share);
                        _hurtGlow = HurtGlowSeconds;
                        _combatUntil = Time.time + CombatHold;
                        _hurtGlowStrength = 0.35f + 0.45f * share;
                    }
                    else if (e.Source == _client.PlayerId)
                    {
                        _shake = Mathf.Max(_shake, heavy ? 0.1f : e.Critical ? 0.07f : 0.025f);
                        if (heavy || e.Critical) _hitStop = HitStopSeconds;
                        _combatUntil = Time.time + CombatHold;
                    }
                    Hear(hurtsMe ? Sound.Hurt : heavy ? Sound.Thump : e.Critical ? Sound.Crit : Sound.Hit, target.Position);
                    break;
                case SimEventType.Revived:
                    // An ally got someone up where they fell (World.Revive): a burst of light on them.
                    var raised = Find(e.Target);
                    if (raised == null) break;
                    AddFlash(raised.Position, raised.Diameter * 1.6f, LevelColor, FlashKind.Sparkles);
                    _floating.Add(new Floating { Position = raised.Position + new Vector3(0, 0.7f, 0), Text = "일어났다!", Color = LevelColor });
                    Hear(Sound.Learn, raised.Position);
                    break;
                case SimEventType.Impact:
                    if (e.Target != 0) HazardLanded(e.Target);
                    var source = Find(e.Source);
                    if (source == null || Vector3.Distance(source.Position, _self.Position) > ShakeRange) break;
                    _shake = Mathf.Max(_shake, Mathf.Min(0.45f, e.Amount * 0.07f));
                    Hear(Sound.Boom, source.Position, Mathf.Min(1, 0.4f + e.Amount * 0.1f));
                    break;
                case SimEventType.SkillUsed:
                    ShowSkill(e);
                    break;
                case SimEventType.Trampled:
                    // A monster trampled a crop at night: dust, a word, and once a session what to do about it.
                    var trampled = new Vector3(e.Position.X, e.Position.Y, 0);
                    AddFlash(trampled, 0.6f, Palette.Dust, FlashKind.Burst);
                    Hear(Sound.Trample, trampled, 0.6f);
                    _floating.Add(new Floating { Position = trampled + new Vector3(0, 0.5f, 0), Text = "짓밟혔다", Color = new Color(0.85f, 0.65f, 0.4f) });
                    if (!_trampleHintShown && Vector3.Distance(trampled, _self.Position) < 12)
                    {
                        _trampleHintShown = true;
                        ShowBanner($"밤에는 몬스터가 밭을 밟는다. 허수아비를 세우면 둘레 {World.ScarecrowReach}칸을 지킨다");
                    }
                    break;
                case SimEventType.Crafted:
                    // Something made by hand that took time is done (Amount 1), or the work stopped (0).
                    if (e.Source != _client.PlayerId) break;
                    if (e.Amount > 0)
                    {
                        Sounds.Play(Sound.Craft);
                        AddFlash(_self.Position + new Vector3(0, 0.6f, 0), 0.8f, LevelColor, FlashKind.Sparkles);
                    }
                    else
                    {
                        _floating.Add(new Floating { Position = _self.Position + new Vector3(0, 0.9f, 0), Text = "만들기를 멈췄다", Color = new Color(1f, 0.85f, 0.5f) });
                    }
                    break;
                case SimEventType.Story:
                    // A step of the story taken or done (Docs/Story.md).
                    if (e.Source != _client.PlayerId) break;
                    var storyStep = _client.Story?.Step(e.Target);
                    if (storyStep == null) break;
                    if (e.Amount == World.StoryTaken)
                    {
                        Sounds.Play(Sound.Learn);
                        ShowBanner($"이야기: {storyStep.Title}");
                    }
                    else
                    {
                        Sounds.Play(Sound.LevelUp);
                        AddFlash(_self.Position + new Vector3(0, 0.6f, 0), 1.2f, StoryLabel, FlashKind.Sparkles);
                        ShowBanner($"이야기 완료: {storyStep.Title}");
                    }
                    break;
                case SimEventType.Waypoint:
                    if (e.Source != _client.PlayerId || _client.Story == null || e.Target >= _client.Story.Places.Count) break;
                    var waypointPlace = _client.Story.Places[e.Target];
                    var waypointAt = new Vector3(e.Position.X, e.Position.Y, 0);
                    AddFlash(waypointAt, 1.4f, WaypointGlow, FlashKind.Sparkles);
                    Sounds.Play(Sound.Learn);
                    if (e.Amount == World.WaypointOpened) ShowBanner($"웨이포인트 열림: {waypointPlace.Name}");
                    break;
                case SimEventType.AnimalsPetted:
                    // Pink sparkles over the house, and how fond they are now.
                    var petted = new Vector3(e.Position.X, e.Position.Y - 0.6f, 0);
                    AddFlash(petted, 1.3f, FondColor, FlashKind.Sparkles);
                    Hear(Sound.Harvest, petted);
                    _floating.Add(new Floating { Position = petted + new Vector3(0, 0.8f, 0), Text = $"애정 {e.Amount}/{World.AffectionMost}", Color = FondColor });
                    break;
                case SimEventType.PetLevelUp:
                    // A pet grown: sparkles on it, and the news for its player.
                    var keeperOfPet = Find(e.Source);
                    var grownPet = Pets.ByIndex(e.Target);
                    if (keeperOfPet == null || grownPet == null) break;
                    var petAt = _pets.TryGetValue(e.Source, out var petBody) ? petBody.Position : keeperOfPet.Position;
                    AddFlash(petAt, 1.1f, LevelColor, FlashKind.Sparkles);
                    if (e.Source == _client.PlayerId)
                    {
                        ShowBanner($"펫 {_client.PetName(grownPet)}이(가) Lv {e.Amount}이 되었다!");
                        Sounds.Play(Sound.Learn);
                    }
                    break;
                case SimEventType.ManaRestored:
                    // A mana potion: the bottle to the mouth, like a healing one, and blue sparkles.
                    var sipper = Find(e.Source);
                    if (sipper == null) break;
                    var drunk = _client.Loot != null && e.Target >= 0 && e.Target < _client.Loot.Consumables.Count ? _client.Loot.Consumables[e.Target] : null;
                    if (sipper.Kind == BodyKind.Player && drunk != null)
                    {
                        Start(sipper, Motion.Drink, DrinkSeconds, Vec2.Zero);
                        sipper.Holding = Sprites.Icon(drunk);
                    }
                    if (e.Amount > 0) _floating.Add(new Floating { Position = sipper.Position, Text = $"+{e.Amount} 마나", Color = ManaColor });
                    Hear(Sound.Heal, sipper.Position);
                    AddFlash(sipper.Position, sipper.Diameter, ManaColor, FlashKind.Sparkles);
                    break;
                case SimEventType.MovedIn:
                    // A person moving into a room of the base: a sparkle at their new place and a banner for everyone.
                    var mover = _client.Npcs != null && e.Amount >= 0 && e.Amount < _client.Npcs.Npcs.Count ? _client.Npcs.Npcs[e.Amount] : null;
                    if (mover == null) break;
                    AddFlash(new Vector3(e.Position.X, e.Position.Y, 0), 1.6f, LevelColor, FlashKind.Sparkles);
                    ShowBanner($"{mover.Name}이(가) 기지의 방으로 이사 왔다!");
                    Sounds.Play(Sound.Learn);
                    break;
                case SimEventType.Hatched:
                    var keeper = Find(e.Source);
                    var hatched = Pets.ByIndex(e.Target);
                    if (keeper != null && hatched != null)
                    {
                        AddFlash(keeper.Position, keeper.Diameter * 1.4f, LevelColor, FlashKind.Sparkles);
                        if (e.Source == _client.PlayerId)
                        {
                            ShowBanner($"펫: {hatched.Name}이(가) 따라온다!");
                            Sounds.Play(Sound.Learn);
                        }
                    }
                    break;
                case SimEventType.Achievement:
                    // One of ours: its name in a banner, a chime and sparkles (Records.cs).
                    if (e.Source == _client.PlayerId && e.Target >= 1 && e.Target <= Records.Achievements.Count)
                    {
                        ShowBanner($"업적: {Records.Achievements[e.Target - 1].Name}!");
                        Sounds.Play(Sound.Story);
                        AddFlash(_self.Position, _self.Diameter * 1.6f, LevelColor, FlashKind.Sparkles);
                    }
                    break;
                case SimEventType.SkillLearned:
                    // A point spent: the skill's name and rank over the player, and a chime for their own.
                    var learner = Find(e.Source);
                    var learnt = _client.SkillNamed(e.Target);
                    if (learner != null && e.Target == 0)
                    {
                        _floating.Add(new Floating { Position = learner.Position + new Vector3(0, 0.6f, 0), Text = "스킬을 되돌렸다", Color = LevelColor });
                        if (e.Source == _client.PlayerId) Sounds.Play(Sound.Click);
                        break;
                    }
                    if (learner == null || learnt == null) break;
                    _floating.Add(new Floating
                    {
                        Position = learner.Position + new Vector3(0, 0.6f, 0),
                        Text = e.Amount > 1 ? $"{learnt.Name} {e.Amount}단계" : $"배움: {learnt.Name}",
                        Color = Palette.Of(learnt.Tags | learnt.Grants),
                    });
                    AddFlash(learner.Position, learner.Diameter * 1.4f, Palette.Of(learnt.Tags | learnt.Grants), FlashKind.Sparkles);
                    if (e.Source == _client.PlayerId) Sounds.Play(Sound.Learn);
                    break;
                case SimEventType.Dug:
                    // A puff of dust where the wall was struck, bigger when it gives way.
                    var struck = new Vector3(e.Position.X, e.Position.Y, 0);
                    if (e.Amount < 0)
                    {
                        // Rock struck without a (good enough) pickaxe: a dull knock, and every few seconds the reminder.
                        if (e.Source != _client.PlayerId) break;
                        Hear(Sound.Hit, struck, 0.35f);
                        if (Time.time < _pickaxeHintAt) break;
                        _pickaxeHintAt = Time.time + 2.5f;
                        string hint = e.Amount == World.NeedsBetterPickaxe ? "더 좋은 곡괭이가 필요하다(미스릴 이상)" : "곡괭이가 필요하다";
                        _floating.Add(new Floating { Position = struck, Text = hint, Color = new Color(1f, 0.85f, 0.5f) });
                        break;
                    }
                    if (e.Amount == World.Repaired)
                    {
                        // Mended: the cracks go, a little gleam.
                        int mx = TileMap.Cell(e.Position.X), my = TileMap.Cell(e.Position.Y);
                        int mended = my * _client.Map.Width + mx;
                        if (_cracks.TryGetValue(mended, out var gone))
                        {
                            Object.Destroy(gone.gameObject);
                            _cracks.Remove(mended);
                        }
                        AddFlash(struck, 0.6f, LevelColor, FlashKind.Sparkles);
                        Hear(Sound.Place, struck);
                        break;
                    }
                    bool broke = e.Amount >= World.DugThrough;
                    AddFlash(struck, broke ? 0.7f : 0.35f, Palette.Dust, FlashKind.Burst);
                    Hear(broke ? Sound.Break : Sound.Dig, struck);
                    if (!broke) Crack(TileMap.Cell(e.Position.X), TileMap.Cell(e.Position.Y), e.Amount);
                    break;
                case SimEventType.Harvested:
                    AddFlash(new Vector3(e.Position.X, e.Position.Y, 0), 0.6f, Palette.Ripe, FlashKind.Sparkles);
                    Hear(Sound.Harvest, new Vector3(e.Position.X, e.Position.Y, 0));
                    break;
                case SimEventType.Watered:
                    // A splash over the watered tiles. The one who poured already heard it when they pressed.
                    var poured = new Vector3(e.Position.X, e.Position.Y, 0);
                    // From no player: a sprinkler, watering the 5×5 square around it.
                    AddFlash(poured, e.Source == 0 ? 3.6f : 2.6f, Palette.Water, FlashKind.Ring);
                    if (e.Source != _client.PlayerId) Hear(Sound.Water, poured);
                    break;
                case SimEventType.Ate:
                    var eater = Find(e.Source);
                    var food = _client.ConsumableAt(e.Amount);
                    if (eater == null || food == null) break;
                    _floating.Add(new Floating { Position = eater.Position + new Vector3(0, 0.4f, 0), Text = food.Name, Color = Palette.Food });
                    if (eater.Kind == BodyKind.Player)
                    {
                        Start(eater, Motion.Drink, DrinkSeconds, Vec2.Zero);
                        eater.Holding = Sprites.Icon(food);
                    }
                    Hear(Sound.Eat, eater.Position);
                    AddFlash(eater.Position, eater.Diameter, Palette.Food, FlashKind.Sparkles);
                    break;
                case SimEventType.Fishing:
                    ShowFishing(e);
                    break;
                case SimEventType.Rift:
                    // A rift opened, was beaten or ran out.
                    Sounds.Play(e.Amount == World.RiftOpened ? Sound.Roar : e.Amount == World.RiftCleared ? Sound.Victory : Sound.Down);
                    ShowBanner(e.Amount == World.RiftOpened ? "공허 균열이 열렸다!" : e.Amount == World.RiftCleared ? "균열을 닫았다! 제단에 보상이 떨어졌다" : "균열이 닫혀 버렸다");
                    AddFlash(new Vector3(e.Position.X, e.Position.Y, 0), 3, RiftColor, FlashKind.Ring);
                    break;
                case SimEventType.LifeLevel:
                    // A life skill grew a level.
                    var skill = (LifeSkill)(e.Amount / 16);
                    var grower = new Vector3(e.Position.X, e.Position.Y + 0.9f, 0);
                    _floating.Add(new Floating { Position = grower, Text = $"{LifeSkills.Name(skill)} {e.Amount % 16}", Color = LevelColor });
                    AddFlash(grower, 1, LevelColor, FlashKind.Sparkles);
                    if (e.Source == _client.PlayerId) Sounds.Play(Sound.LevelUp);
                    // A level that opens a special perk: say where to pick it.
                    if (e.Source == _client.PlayerId && Array.IndexOf(LifeSkills.PerkLevels, e.Amount % 16) >= 0)
                        ShowBanner($"{LifeSkills.Name(skill)} {e.Amount % 16}레벨! 가방 [캐릭터]에서 특별 혜택을 고른다");
                    break;
                case SimEventType.Rain:
                    if (e.Amount > 0) ShowBanner("비가 스며 밭이 젖었다");
                    break;
                case SimEventType.Defense:
                    Sounds.Play(e.Amount == World.DefenseStarted ? Sound.Warning : e.Amount == World.DefenseWon ? Sound.Victory : Sound.Down);
                    ShowBanner(e.Amount == World.DefenseStarted ? "몬스터가 수호석을 노린다!" : e.Amount == World.DefenseWon ? "기지를 지켰다! 수호석 곁에 보상이 떨어졌다" : "수호석이 무너졌다");
                    AddFlash(new Vector3(e.Position.X, e.Position.Y, 0), 3, DefenseColor, FlashKind.Ring);
                    break;
                case SimEventType.TurretShot:
                    // A streak from the turret along its shot, coloured by what it fired, and the barrel turned that way.
                    // A tesla tower's bolt is a jagged streak, and leaps on from monster to monster.
                    var muzzle = new Vector3(e.Position.X, e.Position.Y, 0);
                    var along = new Vector3(e.Direction.X, e.Direction.Y, 0);
                    int shotKind = e.Amount / World.TurretShotKind;
                    float length = e.Amount % World.TurretShotKind / 10f;
                    var shotColor = shotKind == 1 ? FrostShot : shotKind == 2 ? FireShot : shotKind == 3 ? BoltShot : TracerColor;
                    if (shotKind == 3)
                    {
                        var side = new Vector3(-along.y, along.x, 0);
                        var a = muzzle + along * 0.3f;
                        var b = muzzle + along * (length * 0.4f) + side * 0.25f;
                        var c = muzzle + along * (length * 0.7f) - side * 0.25f;
                        var d = muzzle + along * length;
                        AddTracer(a, b, shotColor);
                        AddTracer(b, c, shotColor);
                        AddTracer(c, d, shotColor);
                        AddFlash(d, 0.35f, shotColor, FlashKind.Impact);
                        Hear(Sound.Zap, muzzle, 0.6f);
                        break;
                    }
                    AddTracer(muzzle + along * 0.4f, muzzle + along * length, shotColor);
                    _turretAim[TileMap.Cell(e.Position.Y) * _client.Map.Width + TileMap.Cell(e.Position.X)] = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
                    Hear(Sound.Shoot, muzzle, 0.5f);
                    break;
                case SimEventType.Npc:
                    // A gift taken well or badly, or a chat.
                    var reaction = e.Amount >= World.HeartGift ? $"호감 {e.Amount - World.HeartGift}: 선물을 받았다!" : e.Amount switch
                    {
                        World.RequestDone => "부탁을 들어주었다!",
                        World.StoryDone => "이야기를 하나 마쳤다!",
                        World.Loved => "정말 좋아한다!",
                        World.Liked => "좋아한다",
                        World.Disliked => "별로인 모양이다...",
                        World.Talked => "즐겁게 이야기했다",
                        _ => "고맙다고 한다",
                    };
                    var where = new Vector3(e.Position.X, e.Position.Y + 1.2f, 0);
                    _floating.Add(new Floating { Position = where, Text = reaction, Color = e.Amount == World.Disliked ? OffLabel : PowerLabel });
                    if (e.Amount >= World.Liked) AddFlash(where, 1, PowerLabel, FlashKind.Sparkles);
                    if (e.Source == _client.PlayerId) Sounds.Play(e.Amount == World.Disliked ? Sound.Down : e.Amount == World.StoryDone ? Sound.Story : Sound.Pop);
                    if (e.Amount == World.StoryDone) AddFlash(where, 1.6f, PowerLabel, FlashKind.Sparkles);
                    break;
                case SimEventType.Morning:
                    // Everyone slept: the next day's morning (GameRoot shows the banner, as for any morning).
                    Sounds.Play(Sound.Heal);
                    break;
                case SimEventType.LevelUp:
                    var climber = Find(e.Source);
                    if (climber == null) break;
                    AddFlash(climber.Position, climber.Diameter * 1.6f, LevelColor, FlashKind.Ring);
                    _floating.Add(new Floating { Position = climber.Position + new Vector3(0, 0.6f, 0), Text = $"Lv {e.Amount}", Color = LevelColor });
                    if (e.Source != _client.PlayerId) break;
                    Sounds.Play(Sound.LevelUp);
                    // A class slot opens at levels 10 and 20 (Docs/Classes.md, 클래스 고르기).
                    ShowBanner(ClassRules.SlotsAt(e.Amount) > ClassRules.SlotsAt(e.Amount - 1)
                        ? $"레벨 {e.Amount}! 클래스를 하나 더 고를 수 있다(가방 [스킬] 탭)"
                        : $"레벨 {e.Amount}! 스킬·특성 포인트 +1");
                    break;
                case SimEventType.Recall:
                    var traveller = Find(e.Source);
                    var place = new Vector3(e.Position.X, e.Position.Y, 0);
                    bool mine = e.Source == _client.PlayerId;
                    if (e.Amount == World.RecallStarted)
                    {
                        AddFlash(place, 1.6f, RecallColor, FlashKind.Sparkles);
                        Hear(Sound.Buff, place);
                        if (traveller != null) _floating.Add(new Floating { Position = traveller.Position, Text = "귀환…", Color = RecallColor });
                    }
                    else if (e.Amount == World.RecallArrived)
                    {
                        AddFlash(place, 2.2f, RecallColor, FlashKind.Ring);
                        if (mine) Sounds.Play(Sound.Wake);
                        else Hear(Sound.Wake, place);
                    }
                    else if (mine)
                    {
                        _floating.Add(new Floating { Position = place, Text = "귀환이 끊겼다", Color = Color.gray });
                    }
                    break;
                case SimEventType.Chained:
                    // A jagged line of light from what was struck to where the lightning jumped.
                    var from = Find(e.Source);
                    var to = Find(e.Target);
                    if (from != null && to != null) Bolt(from.Position + new Vector3(0, 0.3f, 0), to.Position + new Vector3(0, 0.3f, 0));
                    break;
                case SimEventType.Healed:
                    var drinker = Find(e.Source);
                    if (drinker == null) break;
                    _floating.Add(new Floating { Position = drinker.Position, Text = "+" + e.Amount, Color = Palette.Heal });
                    // A potion (not a healing skill, which casts): the bottle to the mouth. Food that heals has
                    // its own motion from Ate just before.
                    if (drinker.Kind == BodyKind.Player && drinker.CastTime <= 0 && !(drinker.Motion == Motion.Drink && drinker.MotionTime > 0))
                    {
                        Start(drinker, Motion.Drink, DrinkSeconds, Vec2.Zero);
                        var potion = _client.Loot?.Consumables.FirstOrDefault(c => c.Id == "healing_potion");
                        drinker.Holding = potion == null ? null : Sprites.Icon(potion);
                    }
                    Hear(Sound.Heal, drinker.Position);
                    AddFlash(drinker.Position, drinker.Diameter, Palette.Heal, FlashKind.Sparkles);
                    break;
                case SimEventType.Burst:
                    var burst = _client.SkillOf(e.Source, e.Amount);
                    if (burst == null) break;
                    var at = new Vector3(e.Position.X, e.Position.Y, 0);
                    AddFlash(at, Mathf.Max(0.4f, burst.Radius), Palette.Of(burst.Tags | GrantedTags(e.Source)), FlashKind.Burst);
                    if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.08f);
                    Hear(Sound.Boom, at, 0.7f);
                    break;
                case SimEventType.Died:
                    if (e.Source == _client.PlayerId)
                    {
                        _floating.Add(new Floating { Position = _self.Position, Text = "쓰러짐", Color = Color.gray });
                        Sounds.Play(Sound.Down);
                    }
                    else if (Find(e.Source) is Body fallen)
                    {
                        if (!_bossesSeen.ContainsKey(e.Source)) Hear(fallen.Kind == BodyKind.Player ? Sound.Down : Sound.Pop, fallen.Position, fallen.Kind == BodyKind.Minion ? 0.4f : 1);
                        if (fallen.Kind == BodyKind.Boss && fallen.Id == "venom_spider") SpiderFalls(fallen.Position, fallen.SpiderView, fallen.SpiderTurned);
                        else if (fallen.Kind != BodyKind.Player) Burst(fallen.Position, BodyColor(fallen), fallen.Kind == BodyKind.Minion ? 0.6f : fallen.Diameter);
                    }
                    else if (_departed.TryGetValue(e.Source, out var gone))
                    {
                        if (!_bossesSeen.ContainsKey(e.Source)) Hear(gone.Kind == BodyKind.Player ? Sound.Down : Sound.Pop, gone.Position, gone.Kind == BodyKind.Minion ? 0.4f : 1);
                        // The venom spider falls where it stood, its legs curling up (WorldView.Spider.cs).
                        if (gone.Kind == BodyKind.Boss && gone.Id == "venom_spider") SpiderFalls(gone.Position, gone.View, gone.Turned);
                        else if (gone.Kind != BodyKind.Player) Burst(gone.Position, gone.Color, gone.Kind == BodyKind.Boss ? 2.4f : gone.Kind == BodyKind.Minion ? 0.6f : 0.8f);
                    }
                    if (_bossesSeen.TryGetValue(e.Source, out var boss))
                    {
                        Sounds.Play(Sound.Victory);
                        ShowBanner($"{boss.Name} 처치!", first: true);
                        _bossesSeen.Remove(e.Source);
                    }
                    break;
            }
        }

        // The skill's name over whoever used it, a ring for a nova and a pulse for a buff.
        void ShowSkill(SimEvent e)
        {
            // The event names the skill (its Target); the slot stands in for an older host's.
            var skill = _client.SkillNamed(e.Target) ?? _client.SkillOf(e.Source, e.Amount);
            var caster = Find(e.Source);
            if (skill == null || caster == null) return;
            var color = Palette.Of(skill.Tags | skill.Grants | GrantedTags(e.Source));
            _floating.Add(new Floating { Position = caster.Position + new Vector3(0, 0.4f, 0), Text = skill.Name, Color = color });
            // Each skill its own sound (SoundArt.Skill). One without: its kind's, cold skills ringing like ice, and the
            // big novas landing with a boom too.
            if (!Sounds.PlaySkillAt(skill.Id, caster.Position, _self.Position))
            {
                var sound = skill.Kind switch
                {
                    SkillKind.Buff => Sound.Buff,
                    SkillKind.Heal => Sound.Heal,
                    SkillKind.Dash => Sound.Dash,
                    _ when (skill.Tags & SkillTag.Cold) != 0 => Sound.Ice,
                    SkillKind.Nova => Sound.Nova,
                    _ => Sound.Zap,
                };
                Hear(sound, caster.Position);
                if (skill.Kind == SkillKind.Nova && skill.Radius >= 3) Hear(Sound.Boom, caster.Position, 0.6f);
            }
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    // Arms flung wide and a full turn on the spot.
                    Start(caster, Motion.Spin, SpinSeconds, e.Direction);
                    AddFlash(caster.Position, skill.Radius + World.PlayerRadius, color, FlashKind.Ring);
                    // Spinning blades for the whirls, a fiery blast for the big fires, whirling ice for the cold.
                    if ((skill.Tags & SkillTag.Melee) != 0 || (skill.Tags & SkillTag.Cold) != 0 && skill.Radius >= 3)
                        AddFlash(caster.Position, skill.Radius + World.PlayerRadius, color, FlashKind.Vortex);
                    else if ((skill.Tags & SkillTag.Fire) != 0 && skill.Radius >= 3)
                        AddFlash(caster.Position, skill.Radius, color, FlashKind.Burst);
                    if (e.Source == _client.PlayerId) _shake = Mathf.Max(_shake, 0.06f);
                    break;
                case SkillKind.Buff:
                case SkillKind.Summon:
                    caster.CastTime = CastSeconds;
                    AddFlash(caster.Position, caster.Diameter * (skill.Kind == SkillKind.Summon ? 2 : 1), color, skill.Kind == SkillKind.Summon ? FlashKind.Ring : FlashKind.Sparkles);
                    break;
                case SkillKind.Dash:
                    // A puff where the dash began; the player is already where it ended (the host moved them).
                    Start(caster, Motion.Thrust, ThrustSeconds, e.Direction);
                    var from = caster.Position - new Vector3(e.Direction.X, e.Direction.Y, 0) * skill.Range;
                    AddFlash(from, 0.8f, Palette.Dust, FlashKind.Burst);
                    if (skill.Damage > 0) AddFlash(caster.Position, (skill.Radius + World.PlayerRadius) * 2, color, FlashKind.Ring);
                    if (e.Source == _client.PlayerId) _shake = Mathf.Max(_shake, 0.05f);
                    break;
                case SkillKind.Heal:
                    caster.CastTime = CastSeconds;
                    AddFlash(caster.Position, caster.Diameter * 1.8f, Palette.Heal, FlashKind.Sparkles);
                    break;
                case SkillKind.Projectile:
                    // Both hands sent forward after it. A sword still cuts the arc; a bow or staff is aimed instead.
                    Start(caster, Motion.Thrust, ThrustSeconds, e.Direction);
                    if (caster.Held != null && !IsMelee(caster.Held)) break;
                    caster.Combo = 0;
                    caster.SwingTime = SwingSeconds;
                    caster.Swing.transform.localRotation = Quaternion.Euler(0, 0, e.Direction.Angle * Mathf.Rad2Deg);
                    caster.Swing.transform.localScale = Vector3.one;
                    break;
            }
        }

        // Seconds before a monster that lost interest cries out again on noticing someone.
        const float CrySeconds = 6;

        // A quiet step every StepLength the player walks. A jump of more than a tile in one frame is a snap or a
        // recall, not a walk.
        Ground GroundUnder(Vector3 at) => Recordings.GroundAt(_client.Map, _client.Biomes, Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));

        void Footsteps(Vector3 moved)
        {
            float distance = moved.magnitude;
            if (distance > 1 || _client.IsDead) return;
            _stride += distance;
            if (_stride < StepLength) return;
            _stride -= StepLength;
            Sounds.Step(GroundUnder(_self.Position), 0.3f);
        }

        // A float where a line goes in; a splash and a "!" when a fish bites; a splash when one is landed; the float
        // gone when the line comes out.
        void ShowFishing(SimEvent e)
        {
            var spot = new Vector3(e.Position.X, e.Position.Y, 0);
            bool mine = e.Source == _client.PlayerId;
            switch (World.FishingKind(e.Amount))
            {
                case World.FishingCast:
                    if (!_bobbers.TryGetValue(e.Source, out var bobber))
                    {
                        bobber = Renderer(_root.transform, "Bobber", Sprites.Centered("bobber", () => ItemArt.Bobber), Color.white);
                        bobber.sortingOrder = -9;
                        _bobbers[e.Source] = bobber;
                    }
                    bobber.transform.localPosition = spot;
                    AddFlash(spot, 0.4f, Palette.Water, FlashKind.Ring);
                    if (!mine) Hear(Sound.Cast, spot, 0.6f);
                    break;
                case World.FishingBite:
                    AddFlash(spot, 0.5f, Palette.Water, FlashKind.Burst);
                    _floating.Add(new Floating { Position = spot + new Vector3(0, 0.3f, 0), Text = "!", Color = Color.white });
                    if (mine) Sounds.Play(Sound.Bite);
                    break;
                default:
                    if (_bobbers.TryGetValue(e.Source, out var gone))
                    {
                        Object.Destroy(gone.gameObject);
                        _bobbers.Remove(e.Source);
                    }
                    if (e.Amount == World.FishingCaught)
                    {
                        AddFlash(spot, 0.8f, Palette.Water, FlashKind.Ring);
                        Hear(Sound.Splash, spot);
                    }
                    else if (e.Amount == World.FishingEscaped)
                    {
                        AddFlash(spot, 0.4f, Palette.Water, FlashKind.Ring);
                        if (mine) _floating.Add(new Floating { Position = spot + new Vector3(0, 0.3f, 0), Text = "놓쳤다", Color = Color.gray });
                    }
                    else if (e.Amount == World.FishingTreasure)
                    {
                        // Something else came up with the fish: a rune or a chest of coins (World.Fishing.cs).
                        AddFlash(spot, 1.2f, LevelColor, FlashKind.Sparkles);
                        _floating.Add(new Floating { Position = spot + new Vector3(0, 0.6f, 0), Text = "보물!", Color = LevelColor });
                        if (mine) Sounds.Play(Sound.Story);
                    }
                    else if (World.FishingKind(e.Amount) == World.FishingPrize)
                    {
                        _floating.Add(new Floating { Position = spot + new Vector3(0, 0.9f, 0), Text = $"대회 상금 +{e.Amount >> 4}", Color = LevelColor });
                        if (mine) Sounds.Play(Sound.Pickup);
                    }
                    break;
            }
        }

        // Floats rock gently on the water.
        void BobBobbers()
        {
            float bob = Mathf.Round(Mathf.Sin(Time.time * 4) * 1.5f) / Sprites.PixelsPerUnit;
            foreach (var bobber in _bobbers.Values)
            {
                var p = bobber.transform.localPosition;
                bobber.transform.localPosition = new Vector3(p.x, Mathf.Floor(p.y) + 0.5f + bob, 0);
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
            mixed.a = 0.7f + 0.2f * Mathf.Sin(Time.time * 6);
            body.Aura.color = mixed;
            // Its dashes walk round, a step every tenth of a second.
            int frame = Mathf.FloorToInt(Time.time * 10) % EffectArt.AuraFrames;
            body.Aura.sprite = Sprites.Centered($"fx/aura/{body.AuraDiameter}/{frame}", () => EffectArt.Aura(body.AuraDiameter, frame));
            body.Aura.sortingOrder = body.Shape.sortingOrder - 1;
        }

        void AddFlash(Vector3 position, float radius, Color color, FlashKind kind)
        {
            if (kind == FlashKind.Impact && !Feel.Impact) return;
            int diameter = EffectArt.Diameter(radius * 2);
            var renderer = Renderer(_root.transform, "Flash", EffectSprite(kind, diameter, 0), color);
            renderer.transform.localPosition = position;
            renderer.sortingOrder = 19000;
            // Sparkles drift up for a little longer than a blast lasts.
            float life = kind == FlashKind.Sparkles ? FlashLife * 1.6f : FlashLife;
            _flashes.Add(new Flash { Renderer = renderer, Kind = kind, Diameter = diameter, Life = life, Color = color });
        }

        // Each flash runs through its frames, drawn at its own size, and fades over the last of them.
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
                int frame = Mathf.Min(EffectArt.Frames - 1, Mathf.FloorToInt(t * EffectArt.Frames));
                flash.Renderer.sprite = EffectSprite(flash.Kind, flash.Diameter, frame);
                var color = flash.Color;
                color.a = t < 0.7f ? 0.9f : 0.9f * (1 - t) / 0.3f;
                flash.Renderer.color = color;
            }
        }

        static Sprite EffectSprite(FlashKind kind, int diameter, int frame) => kind switch
        {
            FlashKind.Ring => Sprites.Centered($"fx/ring/{diameter}/{frame}", () => EffectArt.Ring(diameter, frame)),
            FlashKind.Vortex => Sprites.Centered($"fx/vortex/{diameter}/{frame}", () => EffectArt.Vortex(diameter, frame)),
            FlashKind.Sparkles => Sprites.Centered($"fx/sparkles/{diameter}/{frame}", () => EffectArt.Sparkles(diameter, frame)),
            FlashKind.Impact => Sprites.Centered($"fx/impact/{diameter}/{frame}", () => EffectArt.Impact(diameter, frame)),
            _ => Sprites.Centered($"fx/burst/{diameter}/{frame}", () => EffectArt.Burst(diameter, frame)),
        };

        Body Find(int id) => id == _client.PlayerId ? _self : _others.TryGetValue(id, out var body) ? body : null;

        // Puts a body where it is this frame: picks its sprite (the way it faces, a step of its walk), tints it
        // (grey when dormant or dead) and lays the glow over it for flashes, then the swing and health bar.
        void Place(Body body, Vec2 position, Vec2 facing, float health, float maxHealth, Color tint, Color glow, float deltaTime, float pulse = 0, bool showHealth = true,
            ItemBase weapon = null)
        {
            body.LastPosition = body.Position;
            body.Position = new Vector3(position.X, position.Y, 0);
            body.Root.localPosition = body.Position;
            int order = 10000 - Mathf.RoundToInt(position.Y * 10);

            bool dead = health <= 0;
            Animate(body, facing, deltaTime, dead);
            float scale = 1 + pulse * Mathf.Sin(Time.time * 30);
            // One pixel of squash and stretch (Docs/Feel.md, 2026-11-11, the research report's candidate 22): squat and
            // wide for a moment when struck, tall and narrow while leaning into a blow. A pixel of a 16-pixel body.
            float squashX = 1, squashY = 1;
            if (Feel.Squash && !dead)
            {
                if (body.HurtTime > HurtSeconds - SquashSeconds) (squashX, squashY) = (1 + OnePixel, 1 - OnePixel);
                else if (body.LungeTime > 0) (squashX, squashY) = (1 - OnePixel, 1 + OnePixel);
            }
            body.Shape.transform.localScale = new Vector3(squashX, scale * squashY, 1);
            // Leaning into an attack: out and back along its direction.
            if (!Feel.Lunge) body.LungeTime = 0;
            if (!Feel.Knock) body.KnockTime = 0;
            if (!Feel.Flash) body.FlashTime = 0;
            if (!Feel.Swing) body.SwingTime = 0;
            float lean = body.LungeTime > 0 ? Mathf.Sin(body.LungeTime / LungeSeconds * Mathf.PI) * LungeDistance * body.LungeReach : 0;
            // Knocked back by a blow: out and back the other way.
            float knock = body.KnockTime > 0 ? Mathf.Sin(body.KnockTime / KnockSeconds * Mathf.PI) * LungeDistance * body.KnockReach : 0;
            var offset = (Vector3)(body.Lunge * lean + body.Knock * knock);
            body.KnockTime = Mathf.Max(0, body.KnockTime - deltaTime);
            body.Shape.transform.localPosition = new Vector3(0, -body.Feet, 0) + offset;
            body.Glow.transform.localPosition = body.Shape.transform.localPosition;
            body.LungeTime = Mathf.Max(0, body.LungeTime - deltaTime);
            ShowWeapon(body, weapon, facing, order, dead, offset);
            body.Shape.color = dead ? DeadColor : tint;
            body.Shape.sortingOrder = order;
            body.Glow.transform.localScale = body.Shape.transform.localScale;
            body.Glow.color = dead ? Color.clear : body.FlashTime > 0 ? HitFlash : glow;
            if (body.Glow.color.a > 0) WhiteOf(body);
            body.Glow.sortingOrder = order + 1;
            body.Shadow.enabled = !dead;

            body.Swing.enabled = body.SwingTime > 0;
            if (body.SwingTime > 0)
            {
                // The frame for how far through the swing it is (EffectArt.Swing).
                int frame = Mathf.Clamp(Mathf.FloorToInt((1 - body.SwingTime / SwingSeconds) * EffectArt.SwingFrames), 0, EffectArt.SwingFrames - 1);
                int size = body.SwingDiameter;
                body.Swing.sprite = Sprites.Centered($"fx/swing/{size}/{frame}", () => EffectArt.Swing(size, frame));
            }
            body.Swing.color = new Color(1, 1, 1, 0.85f);
            body.Swing.sortingOrder = order + 3;

            float share = maxHealth > 0 ? Mathf.Clamp01(health / maxHealth) : 0;
            bool barVisible = showHealth && !dead && share < 0.999f;
            body.HealthBack.enabled = barVisible;
            body.HealthFill.enabled = barVisible;
            body.HealthBack.transform.localPosition = new Vector3(0, body.Top + 0.15f, 0);
            body.HealthFill.transform.localScale = new Vector3(0.8f * share, 0.08f, 1);
            body.HealthFill.transform.localPosition = new Vector3(-0.4f + 0.4f * share, body.Top + 0.15f, 0);
            body.HealthFill.color = share > 0.5f ? new Color(0.4f, 0.9f, 0.4f) : share > 0.25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.3f, 0.25f);
            body.HealthBack.sortingOrder = order + 4;
            body.HealthFill.sortingOrder = order + 5;

            body.SwingTime = Mathf.Max(0, body.SwingTime - deltaTime);
            body.FlashTime = Mathf.Max(0, body.FlashTime - deltaTime);
            body.StrikeTime = Mathf.Max(0, body.StrikeTime - deltaTime);
            body.CastTime = Mathf.Max(0, body.CastTime - deltaTime);
            body.HurtTime = Mathf.Max(0, body.HurtTime - deltaTime);
            body.MotionTime = Mathf.Max(0, body.MotionTime - deltaTime);
            if (body.MotionTime <= 0) body.Motion = Motion.None;
        }

        // Players look three ways (left is right flipped) and strike, cast, flinch, walk or stand (CreatureArt.Pose);
        // monsters face left or right and bob, faster while they move; the boss breathes.
        void Animate(Body body, Vec2 facing, float deltaTime, bool dead)
        {
            float speed = deltaTime > 0 ? Vector3.Distance(body.Position, body.LastPosition) / deltaTime : 0;
            bool moving = speed > 0.3f && speed < 30;
            body.Walked = moving ? body.Walked + deltaTime : 0;
            bool left = facing.X < 0;
            int frame;
            var look = Look.Side;
            var pose = Pose.Stand;
            switch (body.Kind)
            {
                case BodyKind.Player:
                    // Eight ways; a spin turns through all eight once over its time.
                    int sector = SectorOf(facing);
                    if (body.Motion == Motion.Spin && body.MotionTime > 0)
                        sector = (body.SpinFrom + Mathf.FloorToInt((1 - body.MotionTime / body.MotionLength) * 8)) % 8;
                    (look, left) = Sectors[sector];
                    (pose, frame) = PlayerPose(body, moving, dead);
                    break;
                case BodyKind.Monster:
                case BodyKind.Minion:
                    frame = Mathf.FloorToInt(Time.time * (moving ? 6 : 3) + body.Seed * 0.37f) % CreatureArt.IdleFrames;
                    break;
                default:
                    frame = Mathf.FloorToInt(Time.time * (moving ? 4 : 1.5f)) % CreatureArt.IdleFrames;
                    break;
            }

            if (body.Kind == BodyKind.Boss && body.Id == "vein_eater")
            {
                AnimateVeinEater(body, facing, dead, moving);
                return;
            }
            if (body.Kind == BodyKind.Boss && body.Id == "shrine_samurai")
            {
                AnimateSamurai(body, facing, dead, moving, speed);
                return;
            }
            if (body.Kind == BodyKind.Boss && body.Id == "black_knight")
            {
                AnimateKnight(body, facing, dead, moving);
                return;
            }
            if (body.Kind == BodyKind.Boss && body.Id == "venom_spider")
            {
                AnimateSpider(body, facing, dead, moving, speed);
                return;
            }

            if (body.Kind == BodyKind.Boss)
            {
                // Eight ways, and a pose from what it is doing: winding up while its telegraph shows, the strike just
                // after an attack lands, leaning into a charge, roaring.
                var (view, turned) = BossSectors[SectorOf(facing)];
                var bossPose = dead ? BossPose.Idle
                    : body.StrikeTime > 0 ? BossPose.Strike
                    : body.BossState == BossState.Telegraph ? BossPose.Windup
                    : body.BossState == BossState.Charge ? BossPose.Charge
                    : body.BossState == BossState.Roar ? BossPose.Roar
                    : BossPose.Idle;
                int bossCode = 100000 + (((int)bossPose * 8 + (int)view) * 16 + frame) * 2 + (turned ? 1 : 0);
                if (bossCode == body.Frame) return;
                body.Frame = bossCode;
                string bossKey = $"{body.Kind}/{body.Id}/{Sprites.Key(body.Color)}/{view}/{bossPose}/{frame}";
                Func<PixelImage> drawBoss = () => CreatureArt.Boss(body.Id, body.Color, frame, view, bossPose);
                body.Shape.sprite = Sprites.Standing(bossKey, drawBoss);
                body.Glow.sprite = Sprites.Standing(bossKey + "/white", () => drawBoss().Silhouette());
                body.Shape.flipX = turned;
                body.Glow.flipX = turned;
                body.Top = -body.Feet + body.Shape.sprite.rect.height / Sprites.PixelsPerUnit;
                return;
            }

            if (body.Kind == BodyKind.Monster)
            {
                // Eight ways as two looks flipped: from the side, or from behind going away (up, or up and aside);
                // and a pose: hit, striking, winding up (2026-11-09).
                int sector = SectorOf(facing);
                var monsterView = !dead && (sector == 1 || sector == 2 || sector == 3) ? MonsterView.Back : MonsterView.Side;
                var monsterPose = dead ? MonsterPose.Idle
                    : body.HurtTime > 0 ? MonsterPose.Hurt
                    : body.StrikeTime > 0 ? MonsterPose.Strike
                    : body.EnemyState == EnemyState.Windup ? MonsterPose.Windup
                    : MonsterPose.Idle;
                int monsterCode = 200000 + (((int)monsterPose * 4 + (int)monsterView) * 16 + frame) * 2 + (left ? 1 : 0);
                if (monsterCode == body.Frame) return;
                body.Frame = monsterCode;
                string monsterKey = $"{body.Kind}/{body.Id}/{Sprites.Key(body.Color)}/{monsterView}/{monsterPose}/{frame}";
                Func<PixelImage> drawMonster = () => CreatureArt.Monster(body.Id, body.Color, frame, monsterView, monsterPose);
                body.Shape.sprite = Sprites.Standing(monsterKey, drawMonster);
                body.Glow.sprite = Sprites.Standing(monsterKey + "/white", () => drawMonster().Silhouette());
                body.Shape.flipX = left;
                body.Glow.flipX = left;
                body.Top = -body.Feet + body.Shape.sprite.rect.height / Sprites.PixelsPerUnit;
                return;
            }

            int code = (((int)pose * 8 + (int)look) * 16 + frame) * 2 + (left ? 1 : 0);
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"{body.Kind}/{body.Id}/{Sprites.Key(body.Color)}/{look}/{pose}/{frame}";
            Func<PixelImage> draw = body.Kind switch
            {
                BodyKind.Player => () => CreatureArt.Player(look, frame, body.Color, pose),
                BodyKind.Monster => () => CreatureArt.Monster(body.Id, body.Color, frame),
                BodyKind.Minion => () => CreatureArt.Minion(body.Color, frame),
                _ => () => CreatureArt.Boss(body.Id, body.Color, frame),
            };
            body.Shape.sprite = Sprites.Standing(key, draw);
            body.Glow.sprite = Sprites.Standing(key + "/white", () => draw().Silhouette());
            body.Shape.flipX = left;
            body.Glow.flipX = left;
            body.Top = -body.Feet + body.Shape.sprite.rect.height / Sprites.PixelsPerUnit;
        }

        // What a player is doing, most pressing first: flinching from a hit, striking (a swing, a shot, a thrown
        // skill), casting, walking; standing, they breathe and now and then blink, each at their own moment.
        static (Pose, int) PlayerPose(Body body, bool moving, bool dead)
        {
            if (dead) return (Pose.Stand, 0);
            if (body.HurtTime > 0) return (Pose.Hurt, 0);
            if (body.Motion != Motion.None && body.MotionTime > 0)
            {
                // Two frames over the motion: the first part held back (drawn, raised, lifted), then let go.
                int second = body.MotionTime < body.MotionLength * 0.45f ? 1 : 0;
                return body.Motion switch
                {
                    Motion.Shoot => (Pose.Shoot, second),
                    Motion.Zap => (Pose.Zap, second),
                    Motion.Drink => (Pose.Drink, body.MotionTime < body.MotionLength * 0.6f ? 1 : 0),
                    Motion.Spin => (Pose.Spread, 0),
                    _ => (Pose.Thrust, 0),
                };
            }
            if (body.StrikeTime > 0)
            {
                // The combo's steps strike differently: the first winds up and follows through, the second the other
                // way round (it comes back from where the first ended), and the finisher raises both arms first.
                bool finisher = body.Combo == World.ComboSteps - 1;
                float length = body.StrikeLength > 0 ? body.StrikeLength : StrikeSeconds;
                bool early = body.StrikeTime > length - (finisher ? WindUpSeconds * 2 : WindUpSeconds);
                if (finisher && early) return (Pose.Cast, 0);
                return (Pose.Strike, body.Combo == 1 ? (early ? 1 : 0) : (early ? 0 : 1));
            }
            if (body.CastTime > 0) return (Pose.Cast, 0);
            if (moving) return (Pose.Walk, Mathf.FloorToInt(body.Walked * 8) % CreatureArt.WalkFrames);
            float time = Time.time + body.Seed * 0.61f;
            if (time % BlinkEvery < BlinkSeconds) return (Pose.Stand, CreatureArt.Blink);
            return (Pose.Stand, Mathf.FloorToInt(time / BreathSeconds) % 2 == 1 ? CreatureArt.Breath : 0);
        }

        // The weapon in a player's hand, beside them on the side they face (behind them when they face away). A swing
        // sweeps it down through the attack; facing left mirrors it.
        void ShowWeapon(Body body, ItemBase weapon, Vec2 facing, int order, bool dead, Vector3 offset)
        {
            if (weapon != body.Held)
            {
                body.Held = weapon;
                body.Weapon.sprite = weapon == null ? null : HeldSprite(weapon);
            }
            var (look, left) = Sectors[SectorOf(facing)];
            bool away = look == Look.Up || look == Look.UpSide;
            // Drinking or eating, the bottle or food is in the hand at the mouth instead, tipping back: beside the
            // mouth facing the viewer, behind the head beside it facing away.
            if (body.Motion == Motion.Drink && body.MotionTime > 0 && body.Holding != null && !dead)
            {
                body.Weapon.enabled = true;
                body.Weapon.sprite = body.Holding;
                body.Held = null;
                float tip = Mathf.Lerp(20, 120, 1 - body.MotionTime / body.MotionLength);
                bool mirrored = facing.X < 0;
                float side = away ? 0.22f : 0.2f;
                body.Weapon.transform.localPosition = new Vector3(mirrored ? -side : side, -body.Feet + (away ? 0.6f : 0.52f), 0) + offset;
                body.Weapon.transform.localRotation = Quaternion.Euler(0, 0, mirrored ? -tip : tip);
                body.Weapon.transform.localScale = Vector3.one * HeldScale;
                body.Weapon.flipX = mirrored;
                body.Weapon.sortingOrder = away ? order - 1 : order + 2;
                return;
            }
            body.Weapon.transform.localScale = Vector3.one;
            body.Weapon.enabled = weapon != null && !dead;
            if (!body.Weapon.enabled) return;
            // Eight ways: beside on the side faced, behind when facing away (wholly or turned away), in front when
            // facing the viewer.
            float x = look == Look.Up ? -0.22f : look == Look.Down ? 0.24f : left ? -0.24f : 0.24f;
            if (look == Look.UpSide) x = left ? -0.2f : 0.2f;
            if (look == Look.DownSide) x = left ? -0.26f : 0.26f;
            body.Weapon.transform.localPosition = new Vector3(x, -body.Feet + 0.3f, 0) + offset;
            // Aimed: a bow drawn, a staff raised then pointed, either sending a skill forward. A sword is swung.
            bool aimed = body.MotionTime > 0 && (body.Motion == Motion.Shoot || body.Motion == Motion.Zap || body.Motion == Motion.Thrust && !IsMelee(weapon));
            if (aimed)
            {
                float angle = Mathf.Atan2(facing.Y, facing.X) * Mathf.Rad2Deg;
                bool raised = body.Motion == Motion.Zap && body.MotionTime >= body.MotionLength * 0.45f;
                var way = new Vector3(facing.X, facing.Y, 0).normalized;
                if (raised)
                {
                    body.Weapon.transform.localPosition = new Vector3(x, -body.Feet + 0.5f, 0) + offset;
                    body.Weapon.transform.localRotation = Quaternion.identity;
                }
                else if ((weapon.Tags & SkillTag.Ranged) != 0)
                {
                    // The bow's icon stands upright, bowed to the left: turned so it bows toward the target, its
                    // string to the player, held out round the chest the way they aim, clear of the body.
                    body.Weapon.transform.localPosition = new Vector3(way.x * 0.42f, -body.Feet + 0.5f + way.y * 0.38f, 0) + offset;
                    body.Weapon.transform.localRotation = Quaternion.Euler(0, 0, angle + 180);
                }
                else
                {
                    // A staff's icon lies corner to corner, grip at the bottom left: turned so it points where the
                    // player looks, from the hand.
                    var ahead = new Vector3(way.x, way.y * 0.6f, 0).normalized * 0.28f;
                    body.Weapon.transform.localPosition = new Vector3(ahead.x, -body.Feet + 0.42f + ahead.y * 0.5f, 0) + offset;
                    body.Weapon.transform.localRotation = Quaternion.Euler(0, 0, angle - 45);
                }
                body.Weapon.flipX = false;
                body.Weapon.sortingOrder = away ? order - 1 : order + 2;
                return;
            }
            // Down for the first cut, back up for the second, a wider sweep for the finisher.
            float through = body.SwingTime / SwingSeconds;
            float sweep = body.SwingTime <= 0 ? 0
                : body.Combo == 1 ? Mathf.Lerp(40, -80, through)
                : body.Combo == World.ComboSteps - 1 ? Mathf.Lerp(-115, 70, through)
                : Mathf.Lerp(-80, 40, through);
            body.Weapon.transform.localRotation = Quaternion.Euler(0, 0, left ? -sweep : sweep);
            body.Weapon.flipX = left;
            body.Weapon.sortingOrder = away ? order - 1 : order + 2;
        }

        // A bottle or food in the hand is drawn smaller than its icon, so it does not hide the face.
        const float HeldScale = 0.55f;

        static bool IsMelee(ItemBase weapon) => (weapon.Tags & (SkillTag.Ranged | SkillTag.Magic)) == 0;

        // The weapon's icon held by its grip: the bottom left of a sword or staff, the middle of a bow.
        static Sprite HeldSprite(ItemBase weapon)
        {
            bool bow = (weapon.Tags & SkillTag.Ranged) != 0;
            return Sprites.Get("held/" + weapon.Id, () => ItemArt.Gear(weapon), bow ? new Vector2(0.35f, 0.5f) : new Vector2(0.2f, 0.2f));
        }

        static Color BodyColor(Body body) => new Color32(body.Color.R, body.Color.G, body.Color.B, 255);

        // Burning flickers orange over a body; chilled tints it icy blue. A telegraph's glow wins over the burn.
        static (Color Tint, Color Glow) StatusLook(byte status, Color tint, Color glow)
        {
            if ((status & StatusBits.Chilled) != 0) tint *= ChillTint;
            if ((status & StatusBits.Burning) != 0 && glow.a <= 0) glow = Faded(BurnColor, 0.18f + 0.14f * Mathf.Abs(Mathf.Sin(Time.time * 17)));
            return (tint, glow);
        }

        static readonly Color ChillTint = new Color(0.62f, 0.82f, 1f);
        static readonly Color BurnColor = new Color(1f, 0.5f, 0.15f);
        static readonly Color BoltColor = new Color(1f, 0.95f, 0.5f);
        static readonly Color RecallColor = new Color(0.55f, 0.75f, 1f);

        // Lightning's jump: little squares along a jagged path that vanish in a moment.
        void Bolt(Vector3 from, Vector3 to)
        {
            float length = Vector3.Distance(from, to);
            int steps = Mathf.Max(3, Mathf.RoundToInt(length * 6));
            var side = Vector3.Cross(to - from, Vector3.forward).normalized;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float jag = i == 0 || i == steps ? 0 : (UnityEngine.Random.value - 0.5f) * 0.25f;
                var renderer = Renderer(_root.transform, "Bolt", Shapes.Square, BoltColor);
                renderer.transform.localPosition = Vector3.Lerp(from, to, t) + side * jag;
                renderer.transform.localScale = Vector3.one * (2f / Sprites.PixelsPerUnit);
                renderer.sortingOrder = 19001;
                _bits.Add(new Bit { Renderer = renderer, Velocity = Vector3.zero, Color = BoltColor, Life = 0.18f });
            }
            Hear(Sound.Zap, to, 0.5f);
        }

        // Bits flying out of something that died, in its colour, more for something bigger.
        void Burst(Vector3 at, Color color, float size)
        {
            if (!Feel.Bits) return;
            int count = Mathf.RoundToInt(BitsPerDeath * Mathf.Clamp(size, 0.6f, 2.5f));
            for (int i = 0; i < count; i++)
            {
                float angle = (i + UnityEngine.Random.value * 0.5f) / count * Mathf.PI * 2;
                float speed = (1.5f + UnityEngine.Random.value * 2.5f) * Mathf.Max(1, size * 0.6f);
                var renderer = Renderer(_root.transform, "Bit", Shapes.Square, color);
                renderer.transform.localPosition = at + new Vector3(0, 0.1f, 0);
                renderer.transform.localScale = Vector3.one * (2f / Sprites.PixelsPerUnit);
                renderer.sortingOrder = 19000;
                _bits.Add(new Bit { Renderer = renderer, Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Color = color });
            }
        }

        void UpdateBits(float deltaTime)
        {
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var bit = _bits[i];
                bit.Age += deltaTime;
                if (bit.Age >= bit.Life)
                {
                    Object.Destroy(bit.Renderer.gameObject);
                    _bits.RemoveAt(i);
                    continue;
                }
                bit.Velocity *= Mathf.Exp(-bit.Drag * deltaTime);
                bit.Velocity += bit.Fall * deltaTime;
                var step = bit.Velocity;
                if (bit.Sway != 0) step.x += Mathf.Sin(bit.Age * 4 + bit.Phase) * bit.Sway;
                var place = bit.Renderer.transform;
                place.localPosition += step * deltaTime;
                if (bit.Spin != 0) place.localRotation = Quaternion.Euler(0, 0, bit.Phase * Mathf.Rad2Deg + bit.Spin * bit.Age);
                if (bit.Tumble)
                {
                    var scale = place.localScale;
                    scale.x = scale.y * Mathf.Max(0.2f, Mathf.Abs(Mathf.Cos(bit.Age * 7 + bit.Phase)));
                    place.localScale = scale;
                }
                if (bit.Thin) place.localScale = new Vector3(bit.Scale.x, bit.Scale.y * Mathf.Clamp01((1 - bit.Age / bit.Life) * 1.8f), 1);
                var color = bit.Color;
                // Fading from the alpha it started with (a faint flash stays faint); a petal only over its last third.
                float left = 1 - bit.Age / bit.Life;
                color.a = bit.Color.a * (bit.LateFade ? Mathf.Clamp01(left * 3) : left);
                bit.Renderer.color = color;
            }
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
                Swing = Renderer(root, "Swing", null, Color.white),
                // The swing reaches MeleeReach past the edge of the body; the buff ring sits a little outside it.
                SwingDiameter = EffectArt.Diameter(diameter + World.MeleeReach * 2),
                AuraDiameter = EffectArt.Diameter(diameter * 1.7f),
                HealthBack = Renderer(root, "HealthBack", Shapes.Square, new Color(0, 0, 0, 0.6f)),
                HealthFill = Renderer(root, "HealthFill", Shapes.Square, Color.green),
                Aura = Renderer(root, "Aura", null, Color.clear),
                Weapon = Renderer(root, "Weapon", null, Color.white),
            };
            body.Shape.transform.localPosition = new Vector3(0, -body.Feet, 0);
            body.Glow.transform.localPosition = body.Shape.transform.localPosition;
            body.Shadow.transform.localPosition = new Vector3(0, -body.Feet + 1f / Sprites.PixelsPerUnit, 0);
            body.Shadow.sortingOrder = -30;
            body.Aura.enabled = false;
            body.HealthBack.transform.localScale = new Vector3(0.84f, 0.12f, 1);
            // A weapon icon is drawn to fill a bag slot; in the hand it is a little smaller than the body.
            body.Weapon.transform.localScale = Vector3.one * 0.8f;
            body.Weapon.enabled = false;
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
            _corners = AddTilemap(grid.transform, "Corners", -99);
            _underlay = AddTilemap(grid.transform, "Underlay", -95);
            _objects = AddTilemap(grid.transform, "Objects", -90);

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map[x, y] == TileType.Scarecrow) _scarecrows.Add(new Vector3Int(x, y, 0));
            StreamChunks(new Vector2(client.Position.X, client.Position.Y));
        }

        // The tilemaps hold only the chunks round the player (2026-11-12: the game's map is 1024 tiles across, a
        // million tiles a layer, too many for a browser to hold at once): those within ChunkReach chunks are laid, and
        // those that fall more than a chunk further away are cleared. A chunk is laid from the map as it is then, so a
        // tile that changed while its chunk was away is drawn right when it comes back.
        const int ChunkSize = 32, ChunkReach = 2;
        readonly HashSet<(int X, int Y)> _chunks = new HashSet<(int X, int Y)>();
        readonly List<(int X, int Y)> _chunksGone = new List<(int X, int Y)>();

        bool Shown(int x, int y) => _chunks.Contains((x / ChunkSize, y / ChunkSize));

        void StreamChunks(Vector2 at)
        {
            var map = _client.Map;
            int cx = Mathf.FloorToInt(at.x) / ChunkSize, cy = Mathf.FloorToInt(at.y) / ChunkSize;
            int across = (map.Width + ChunkSize - 1) / ChunkSize, up = (map.Height + ChunkSize - 1) / ChunkSize;
            for (int y = cy - ChunkReach; y <= cy + ChunkReach; y++)
                for (int x = cx - ChunkReach; x <= cx + ChunkReach; x++)
                    if (x >= 0 && y >= 0 && x < across && y < up && _chunks.Add((x, y))) LayChunk(x, y, true);
            _chunksGone.Clear();
            foreach (var chunk in _chunks)
                if (Mathf.Abs(chunk.X - cx) > ChunkReach + 1 || Mathf.Abs(chunk.Y - cy) > ChunkReach + 1) _chunksGone.Add(chunk);
            foreach (var chunk in _chunksGone)
            {
                _chunks.Remove(chunk);
                LayChunk(chunk.X, chunk.Y, false);
            }
        }

        // Lays a chunk's tiles on all four tilemaps, or clears them.
        void LayChunk(int chunkX, int chunkY, bool lay)
        {
            var map = _client.Map;
            int x0 = chunkX * ChunkSize, y0 = chunkY * ChunkSize;
            int w = Mathf.Min(ChunkSize, map.Width - x0), h = Mathf.Min(ChunkSize, map.Height - y0);
            var ground = new TileBase[w * h];
            var corners = new TileBase[w * h];
            var underlay = new TileBase[w * h];
            var objects = new TileBase[w * h];
            if (lay)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        ground[i] = GroundAt(x0 + x, y0 + y);
                        corners[i] = CornersAt(x0 + x, y0 + y);
                        underlay[i] = UnderlayAt(x0 + x, y0 + y);
                        objects[i] = ObjectTileAt(map, x0 + x, y0 + y);
                    }
                }
            }
            var bounds = new BoundsInt(x0, y0, 0, w, h, 1);
            _ground.SetTilesBlock(bounds, ground);
            _corners.SetTilesBlock(bounds, corners);
            _underlay.SetTilesBlock(bounds, underlay);
            _objects.SetTilesBlock(bounds, objects);
        }

        TileBase UnderlayAt(int x, int y) => PropArt.Underlay(_client.Map, _client.Places, x, y) is TileType floor ? Sprites.Object(floor) : null;

        // The flat tile of a placed thing, or none for one that stands on the floor (PropArt.Stands, DrawProps).
        static TileBase ObjectTile(TileType type) => PropArt.Stands(type) ? null : Sprites.Object(type);

        // The tile at x, y, a road matched to its neighbours (TileArt.RoadVariant).
        static TileBase ObjectTileAt(TileMap map, int x, int y) =>
            map[x, y] == TileType.Road ? Sprites.Road(TileArt.RoadVariant(map, x, y)) : ObjectTile(map[x, y]);

        static Tilemap AddTilemap(Transform grid, string name, int order)
        {
            var tilesObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            tilesObject.transform.SetParent(grid, false);
            tilesObject.GetComponent<TilemapRenderer>().sortingOrder = order;
            return tilesObject.GetComponent<Tilemap>();
        }

        TileBase GroundAt(int x, int y) => Sprites.Ground(GroundIndex(x, y));

        int GroundIndex(int x, int y) => TileArt.GroundAt(_client.Map, x, y, InArena(_client, x, y), _client.Biomes[x, y]);

        // How many tiles round the given cell have a rounded-corner layer on them, read back from the tilemap (Playtest).
        internal int CornersNear(int cx, int cy, int reach)
        {
            if (_corners == null) return -1;
            int count = 0;
            for (int y = cy - reach; y <= cy + reach; y++)
                for (int x = cx - reach; x <= cx + reach; x++)
                    if (_corners.GetTile(new Vector3Int(x, y, 0)) != null) count++;
            return count;
        }

        // The rounded corners over the tile at x, y (TileArt.Rounds), each from the ground tile beside it.
        readonly int[] _cornerSources = new int[4];

        TileBase CornersAt(int x, int y)
        {
            var map = _client.Map;
            for (int c = 0; c < TileArt.CornerWays.Length; c++)
            {
                var (sx, sy) = TileArt.CornerWays[c];
                _cornerSources[c] = map.InBounds(x + sx, y) && TileArt.Rounds(map, x, y, sx, sy) ? GroundIndex(x + sx, y) : -1;
            }
            return Sprites.Corners(_cornerSources);
        }

        // Whether the tile is in any boss's arena.
        public static bool InArena(WorldClient client, int x, int y)
        {
            var center = TileMap.Center(x, y);
            foreach (var arena in client.Arenas)
            {
                if (Vec2.Distance(center, arena) <= client.ArenaRadius) return true;
            }
            return false;
        }

        static readonly (int X, int Y)[] Neighbourhood = { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) };

        // Whether the tile shows cracks (a monster or a pickaxe struck it and it still stands).
        public bool Cracked(int x, int y) => _client.Map != null && _client.Map.InBounds(x, y) && _cracks.ContainsKey(y * _client.Map.Width + x);

        // The cracked tile in front of the player, which the bag offers to mend (World.Repair), or none.
        public TileType? CrackAhead()
        {
            if (_client.Map == null) return null;
            var (x, y) = World.TileAhead(_client.Position, _client.Facing);
            return Cracked(x, y) && _client.Map.BlocksMonsters(x, y) ? _client.Map[x, y] : null;
        }

        // Cracks the rock at a tile as far as it is dug (percent): longer and more of them with each quarter.
        void Crack(int x, int y, int percent)
        {
            if (!_client.Map.InBounds(x, y)) return;
            int key = y * _client.Map.Width + x;
            if (!_cracks.TryGetValue(key, out var renderer))
            {
                renderer = Renderer(_root.transform, "Crack", null, Color.white);
                renderer.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0);
                // Over the rock and what is built on it, under everything standing in front.
                renderer.sortingOrder = -85;
                _cracks[key] = renderer;
            }
            int stage = Mathf.Clamp(1 + percent * TileArt.CrackStages / 100, 1, TileArt.CrackStages);
            renderer.sprite = CrackSprites[stage - 1];
        }

        static Sprite[] _crackSprites;

        static Sprite[] CrackSprites => _crackSprites ??= Enumerable.Range(1, TileArt.CrackStages)
            .Select(stage => Sprites.Centered("crack/" + stage, () => TileArt.Cracks(stage)))
            .ToArray();

        // Redraws tiles that were dug or built on, and their neighbours, whose edges and shadows depend on them. A
        // changed tile loses its cracks.
        public void UpdateTiles(IEnumerable<TileChange> changes)
        {
            var map = _client.Map;
            foreach (var change in changes)
            {
                int key = change.Y * map.Width + change.X;
                if (_cracks.TryGetValue(key, out var crack))
                {
                    Object.Destroy(crack.gameObject);
                    _cracks.Remove(key);
                }
                if (Shown(change.X, change.Y)) _objects.SetTile(new Vector3Int(change.X, change.Y, 0), ObjectTileAt(map, change.X, change.Y));
                if (map[change.X, change.Y] == TileType.Scarecrow) _scarecrows.Add(new Vector3Int(change.X, change.Y, 0));
                else _scarecrows.Remove(new Vector3Int(change.X, change.Y, 0));
                // The corners round by the neighbours and take the ground beside, which itself depends on its neighbours.
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (map.InBounds(change.X + dx, change.Y + dy) && Shown(change.X + dx, change.Y + dy))
                            _corners.SetTile(new Vector3Int(change.X + dx, change.Y + dy, 0), CornersAt(change.X + dx, change.Y + dy));
                    }
                }
                foreach (var (dx, dy) in Neighbourhood)
                {
                    int x = change.X + dx;
                    int y = change.Y + dy;
                    if (!map.InBounds(x, y) || !Shown(x, y)) continue;
                    _ground.SetTile(new Vector3Int(x, y, 0), GroundAt(x, y));
                    _underlay.SetTile(new Vector3Int(x, y, 0), UnderlayAt(x, y));
                    // A road beside it may gain or lose a kerb.
                    if (map[x, y] == TileType.Road) _objects.SetTile(new Vector3Int(x, y, 0), ObjectTileAt(map, x, y));
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

        void BuildDark(int size)
        {
            if (_darkTexture != null) Object.Destroy(_darkTexture);
            if (_dark != null) Object.Destroy(_dark.gameObject);
            _lightMap = new LightMap(size);
            _darkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                // Smooth between tile centres, so light falls off softly instead of in squares.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _darkPixels = new Color32[size * size];
            // One texel a world unit, so texel centres sit on tile centres when the corner is on a tile corner.
            var sprite = Sprite.Create(_darkTexture, new Rect(0, 0, size, size), Vector2.zero, 1, 0, SpriteMeshType.FullRect);
            _dark = Renderer(_root.transform, "Dark", sprite, Color.white);
            _dark.sortingOrder = DarkOrder;
        }

        // Every player carries a light, burning tiles (tiles.csv light) and glowing shots light their surroundings,
        // and the boss arena glows. Off in the menu, everything is lit.
        // Daylight through the cave's cracks (World.SkyWells) and the skylights players lay: warm by morning, white by
        // day, orange at evening, a little blue moonlight at night; dimmer under rain and fog.
        List<(int X, int Y)> _skyWells;

        void AddSkyLights(int left, int bottom, int size)
        {
            _skyWells ??= World.SkyWells(_client.Map, _client.Spawn, _client.Seed);
            long clock = _client.Clock;
            var (color, radius, intensity) = Calendar.Phase(clock) switch
            {
                DayPhase.Morning => (Rgba.Hex("#FFE6B0"), 4.5f, 0.9f),
                DayPhase.Day => (Rgba.Hex("#FFF8E8"), 5f, 1f),
                DayPhase.Evening => (Rgba.Hex("#FF9A5A"), 4f, 0.75f),
                _ => (Rgba.Hex("#8AA8FF"), 2.5f, 0.45f),
            };
            var weather = World.WeatherOf(Calendar.Day(clock));
            if (weather is Weather.Rain or Weather.Storm)
            {
                radius *= 0.8f;
                intensity *= 0.7f;
            }
            else if (weather == Weather.Fog)
            {
                radius *= 0.85f;
                intensity *= 0.8f;
            }
            const int margin = 6;
            bool Near(int x, int y) => x >= left - margin && x < left + size + margin && y >= bottom - margin && y < bottom + size + margin;
            foreach (var (x, y) in _skyWells)
            {
                if (Near(x, y)) _lights.Add(new LightSource(TileMap.Center(x, y), radius, intensity, color, tints: true));
            }
            var map = _client.Map;
            for (int y = Mathf.Max(0, bottom - margin); y < Mathf.Min(map.Height, bottom + size + margin); y++)
            {
                for (int x = Mathf.Max(0, left - margin); x < Mathf.Min(map.Width, left + size + margin); x++)
                {
                    if (map[x, y] == TileType.Skylight) _lights.Add(new LightSource(TileMap.Center(x, y), radius * 0.8f, intensity, color, tints: true));
                }
            }
        }

        // In the evening and at night (2026-11-11), the windows of the places' houses glow warm out onto the plaza,
        // and the lamps along the roads and in the places throw their light further.
        void AddNightLights(int left, int bottom, int size)
        {
            var phase = Calendar.Phase(_client.Clock);
            if (phase != DayPhase.Evening && phase != DayPhase.Night || _client.Places.Count == 0) return;
            float strength = phase == DayPhase.Night ? 1 : 0.6f;
            var warm = Rgba.Hex("#FFC870");
            var map = _client.Map;
            const int margin = 6;
            for (int y = Mathf.Max(0, bottom - margin); y < Mathf.Min(map.Height, bottom + size + margin); y++)
            {
                for (int x = Mathf.Max(0, left - margin); x < Mathf.Min(map.Width, left + size + margin); x++)
                {
                    var type = map[x, y];
                    if (PropArt.LitAtNight(type))
                    {
                        var (w, _) = Structures.Footprint(type);
                        _lights.Add(new LightSource(new Vec2(x + w / 2f, y + 0.6f), 3.2f, 0.7f * strength, warm, tints: true));
                    }
                    else if (type == TileType.LampPost || type == TileType.Lantern || type == TileType.Brazier || type == TileType.Campfire)
                        _lights.Add(new LightSource(TileMap.Center(x, y), 4.5f, 0.45f * strength, warm, tints: true));
                }
            }
        }

        void UpdateDark(Vec2 self)
        {
            _dark.enabled = Settings.Darkness;
            if (!_dark.enabled) return;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            float halfHeight = _camera.orthographicSize;
            int needed = Mathf.CeilToInt(Mathf.Max(halfHeight, halfHeight * _camera.aspect) * 2) + 8;
            if (needed > _lightMap.Size) BuildDark(needed);
            int size = _lightMap.Size;
            int left = Mathf.FloorToInt(_cameraTarget.x) - size / 2;
            int bottom = Mathf.FloorToInt(_cameraTarget.y) - size / 2;
            _lights.Clear();
            _lights.Add(WorldLights.Carried(self));
            foreach (var body in _others.Values)
            {
                if (body.Kind == BodyKind.Player) _lights.Add(WorldLights.Carried(new Vec2(body.Position.x, body.Position.y)));
                else if (body.Kind == BodyKind.Minion) _lights.Add(new LightSource(new Vec2(body.Position.x, body.Position.y), MinionLight, 0.7f, body.Color, tints: true));
            }
            _worldLights.AddTiles(_lights, _client.Map, left, bottom, size, Time.time);
            AddSkyLights(left, bottom, size);
            AddNightLights(left, bottom, size);
            foreach (var shot in _projectiles.Values)
            {
                if (shot.sprite == ArrowSprite) continue;
                var at = shot.transform.localPosition;
                _lights.Add(new LightSource(new Vec2(at.x, at.y), ShotLight, 0.8f, Sprites.ToRgba(shot.color), tints: true));
            }
            _lightMap.Compute(_client.Map, left, bottom, _lights, _arenaGlow);
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    var o = _lightMap.Overlay(i, j);
                    _darkPixels[j * size + i] = new Color32(o.R, o.G, o.B, o.A);
                }
            }
            _darkTexture.SetPixels32(_darkPixels);
            _darkTexture.Apply(false);
            _dark.transform.localPosition = new Vector3(left, bottom, 0);
            DevInfo.DarkTime((float)timer.Elapsed.TotalMilliseconds);
        }

        void Label(Vector3 world, string text, Color color)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            Shadowed(new Rect(point.x - 80, point.y - 12, 160, 24), text, color, _labelStyle);
        }

        static readonly Color StoryLabel = new Color(1f, 0.85f, 0.35f);
        static readonly Color WaypointGlow = new Color(0.55f, 0.8f, 1f);

        static readonly Color GaugeBack = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        static readonly Color GaugeZone = new Color(0.35f, 0.85f, 0.45f, 0.9f);

        // While a fish bites: a bar over the player with the catch zone in its middle and a marker sliding to and fro
        // (World.GaugeAt). Pressing the fishing button with the marker in the zone lands the fish.
        void DrawReelGauge()
        {
            if (_client.Fishing != World.FishingBite) return;
            Vector3 screen = _camera.WorldToScreenPoint(_self.Position + new Vector3(0, _self.Top + 1.1f, 0));
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            const float width = 140, height = 14;
            var bar = new Rect(point.x - width / 2, point.y - height / 2, width, height);
            int difficulty = _client.FishingDifficulty;
            float half = World.CatchHalfWidth(difficulty, _client.LifeLevel(LifeSkill.Fishing));
            float marker = World.GaugeAt(_client.BiteTicksSeen, difficulty);
            var previous = GUI.color;
            GUI.color = GaugeBack;
            GUI.DrawTexture(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), Texture2D.whiteTexture);
            GUI.color = GaugeZone;
            GUI.DrawTexture(new Rect(bar.x + bar.width * (0.5f - half), bar.y, bar.width * half * 2, bar.height), Texture2D.whiteTexture);
            GUI.color = World.InCatchZone(marker, difficulty, _client.LifeLevel(LifeSkill.Fishing)) ? Color.white : new Color(1, 0.6f, 0.5f);
            GUI.DrawTexture(new Rect(bar.x + bar.width * marker - 2, bar.y - 4, 4, bar.height + 8), Texture2D.whiteTexture);
            GUI.color = previous;
            Shadowed(new Rect(point.x - 120, bar.yMax + 4, 240, 20), "초록 칸에서 낚아채기!", Color.white, _labelStyle);
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
