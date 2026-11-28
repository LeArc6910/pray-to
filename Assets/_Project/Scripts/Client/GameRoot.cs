using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Client
{
    // The one component in the scene. It shows the start menu, then runs a session: solo (a host and its own
    // client in this process), hosting (the same, plus remote players over Netcode) or joining someone's host.
    // Core does the game; this steps it at the fixed tick rate, routes the messages and hands the view what to draw.
    public sealed partial class GameRoot : MonoBehaviour
    {
        enum Mode
        {
            Menu,
            Solo,
            Host,
            Join,
        }

        // The local player's connection on a host. Netcode numbers remote clients from 1, so this cannot clash.
        const ulong LocalConnection = ulong.MaxValue;
        // After a long hitch, drop the backlog instead of fast-forwarding through it.
        const int MaxTicksPerFrame = 5;
        const float AcquiredSeconds = 4;
        // The view keeps about this many tiles across the screen's short side, zoomed by a whole number (below).
        const float ShortSideTiles = 14;
        // Its frame is 4 points thick at top and bottom, which leaves a 14-point well for a 12-point line of text.
        const float HealthBarHeight = 22;
        const float ManaBarHeight = 16;
        const float ExperienceBarHeight = 14;
        const float GuideCheckSeconds = 0.5f;
        // A goal's hint shows this long after the goal comes up; then only the goal stays.
        const float GoalHintSeconds = 30;
        // The host saves its world this often, and when the session ends. Characters save whenever the bag changes.
        const float WorldSaveSeconds = 30;

        Mode _mode = Mode.Menu;
        string _playerName = "플레이어";
        string _worldName = "내 월드";
        // While hosting: the save the world goes to.
        string _worldKey;
        float _worldSaveClock;
        int _savedInventoryVersion;
        int _savedExperience;
        int _savedPetNames;
        int _savedKit;
        string _address = "127.0.0.1";
        // Online play (NetLink.HostOnline): the code typed in to join, the code of the room this host made, and
        // whether a room is being made or looked up.
        string _codeInput = "";
        string _roomCode;
        bool _connecting;
        // A room holds the host and this many others.
        const int OnlineGuests = 3;
        string _status = "";
        LootData _loot;
        MonsterData _monsters;
        BossData _bosses;
        ClassData _classes;
        TileData _tiles;
        GuideData _guide;
        NpcData _npcs;
        // The story, its places and people (Docs/Story.md, 2026-11-10); empty in a build from before its sheets.
        StoryData _story;
        StoryScreen _storyScreen;
        // The guide for new players (Docs/Guide.md): how far this character has come, checked twice a second, and
        // when its current goal came up, for showing the hint a while.
        GuideProgress _guideProgress;
        float _guideClock;
        float _goalShownAt;
        // Class ids in the order picked; the first one's skills take the first slots. Both to start with, so a new
        // player sees what mixing does.
        // The classes the character joins with: a saved character's own; none for a new one, who picks in the game
        // (Docs/Classes.md, 클래스 고르기, 2026-11-09).
        readonly List<string> _chosenClasses = new List<string>();
        WorldHost _host;
        WorldClient _client;
        NetLink _net;
        WorldView _view;
        // Rounded-corner tiles within ten cells of the player (WorldView.CornersNear), for the playtests.
        // Boss marks on the ground and the warning sign, as drawn (WorldView.Bosses.cs), for the playtests.
        // The view itself, for the playtests' looks at the standing layer (Playtest depth).
        internal WorldView View => _view;
        internal int MarksShown => _view?.MarksShown ?? 0;
        // Skill effects running and their pieces on screen (WorldView.SkillFx), for the playtests.
        internal int SkillFxAlive => _view?.SkillFxAlive ?? 0;
        // Rolls begun on the screen and dodged blows shown (WorldView.StartRoll), for the playtests.
        internal int RollsShown => _view?.RollsShown ?? 0;
        // The weapon arts shown (WorldView.ShowArt) and the client's count of the cooldown, for the playtests.
        internal int ArtsShown => _view?.ArtsShown ?? 0;
        internal string LastArt => _view == null || _view.ArtsShown == 0 ? null : $"{_view.LastArt} {_view.LastArtPhase}";
        internal float ArtCooldownLeft => _client?.ArtCooldownLeft ?? 0;
        internal int DodgesShown => _view?.DodgesShown ?? 0;
        internal int SkillFxPieces => _view?.SkillFxPieces ?? 0;
        internal int SkillFxLights => _view?.SkillFxLights ?? 0;
        internal int SkillShotsDrawn => _view?.SkillShotsDrawn ?? 0;
        internal bool WarningShown => _view != null && _view.WarningShown;
        // R1b (WorldView.Cues.cs): cues and signs seen, a late blow's mark as drawn, helmets dropped.
        internal int CuesSeen => _view?.CuesSeen ?? 0;
        internal string BadgeShown => _view?.BadgeShown ?? "";
        internal float HeldFill => _view?.HeldFill ?? -1;
        internal bool HeldBlinked => _view != null && _view.HeldBlinked;
        internal int HelmetsDropped => _view?.HelmetsDropped ?? 0;
        internal int PoolsShown => _view?.PoolsShown ?? 0;
        // Ordinary monsters' marks, pools, one-off effects, pieces and shots as drawn (WorldView.MonsterFx).
        internal int MonsterMarksShown => _view?.MonsterMarksShown ?? 0;
        // E5: monsters' marks drawn as a picture of their shape last frame, and bodies drawn rushing.
        internal int MonsterWarnsShown => _view?.MonsterWarnsShown ?? 0;
        internal int MonsterRushesShown => _view?.MonsterRushesShown ?? 0;
        // How a monster is drawn now: its picture's pixels, its health bar, its feet and its scale (the "sizes" playtest).
        internal (int Width, int Height, float Bar, float Feet, float Scale)? MonsterDrawn(int id) => _view?.MonsterDrawn(id);
        // Whether the screen shows my body in a spore cloud (its green haze, B1b).
        internal bool InSporeCloud => _view != null && _view.InSporeCloud();
        internal int CloudNumbersShown => _view?.CloudNumbers ?? 0;
        // Streams of falling sand drawn over the sand cave's buried, and icicles shown falling (B2b).
        internal int SandFallsShown => _view?.SandFallsShown ?? 0;
        internal int IciclesShown => _view?.IciclesShown ?? 0;
        // Ripples over the lake's ones under the water, shots glancing off crystal, shells turning shots, prism shots
        // breaking and splashes in the shallows (B3b).
        internal int RipplesShown => _view?.RipplesShown ?? 0;
        internal int GlancesShown => _view?.GlancesShown ?? 0;
        // B4b: steam vents seen hissing and bursting, plates giving way, falls, plates back, plates cracking now, and the
        // abyss's lights dimmed (WorldView.LavaVoid.cs).
        internal int VentsShown => _view?.VentsShown ?? 0;
        internal int VentBurstsShown => _view?.VentBurstsShown ?? 0;
        internal int CrumblesShown => _view?.CrumblesShown ?? 0;
        internal int FallsShown => _view?.FallsShown ?? 0;
        internal int PlatesBackShown => _view?.PlatesBackShown ?? 0;
        internal int CracksShown => _view?.CracksShown ?? 0;
        internal int AbyssLightsDimmed => _view?.AbyssLightsDimmed ?? 0;
        internal int ReflectsShown => _view?.ReflectsShown ?? 0;
        internal int RefractsShown => _view?.RefractsShown ?? 0;
        internal int SplashesShown => _view?.SplashesShown ?? 0;
        internal int MonsterPoolsShown => _view?.MonsterPoolsShown ?? 0;
        // A dungeon's name cards shown, and the last name (WorldView.Dungeon.cs).
        internal int DungeonCardsShown => _view?.DungeonCardsShown ?? 0;
        internal string DungeonCardName => _view?.DungeonCardName ?? "";
        internal string DungeonCardLine => _view?.DungeonCardLine ?? "";
        internal int DungeonClearsShown => _view?.ClearsShown ?? 0;
        internal bool DungeonCardShowing => _view != null && _view.DungeonCardShowing;
        internal int MonsterFxAlive => _view?.MonsterFxAlive ?? 0;
        internal int MonsterFxPieces => _view?.MonsterFxPieces ?? 0;
        // The boss's warnings and the blow in their shape (WorldView.Hazards.cs), for the playtest.
        internal int WarnsShown => _view?.WarnCount ?? 0;
        internal int BlastPieces => _view?.LastBlastPieces ?? 0;
        internal int FloorScars => _view?.ScarCount ?? 0;
        // The bosses' own effects (WorldView.BossFx.cs, E6): how many run and the most pieces one laid last frame.
        internal int BossFxCount => _view?.BossFxCount ?? 0;
        internal int BossFxPieces => _view?.BossFxPieces ?? 0;
        // Blows' effects on the bodies they land on and the damage numbers (WorldView.HitFx.cs, E7).
        internal int HitFxCount => _view?.HitFxCount ?? 0;
        internal int HitFxPieces => _view?.HitFxPieces ?? 0;
        internal int HitFxAll => _view?.HitFxAll ?? 0;
        internal int NumbersShown => _view?.NumbersShown ?? 0;
        internal int NumberSlots => _view?.NumberSlots ?? 0;
        // The enemies' shots drawn in their element, the rings where they ended, and the dots of the aim lines (E2).
        internal int EnemyShots => _view?.EnemyShotCount ?? 0;
        internal int ShotEnds => _view?.ShotPopCount ?? 0;
        internal int AimDots => _view?.AimDotCount ?? 0;
        internal int MonsterShotsDrawn => _view?.MonsterShotsDrawn ?? 0;
        internal int MonsterBlowsShown => _view?.MonsterBlowsShown ?? 0;
        internal int AlertsShown => _view?.AlertsShown ?? 0;
        // What the screen showed of the trap rooms and the pacing director (WorldView.ShowPace), for the playtests.
        internal (int Banners, int Dust, int Howls, int Grates) PaceShown => _view == null ? (0, 0, 0, 0)
            : (_view.TrapBanners, _view.AmbushDustShown, _view.RaidHowlsHeard, _view.GratesDropped);
        // A raid's dot on the screen's edge (WorldView.FollowRaids, B5c): drawn now, raids pointed to, raids that came on.
        internal (int Dots, int Shown, int OnScreen) RaidSigns => _view == null ? (0, 0, 0) : (_view.RaidDots, _view.RaidDotsShown, _view.RaidsOnScreen);
        // Field elites' name tags drawn, the widest in points, and a guarding leader's lines (WorldView.MonsterFx).
        internal int EliteTagsShown => _view?.EliteTagsShown ?? 0;
        internal float EliteTagWidest => _view?.EliteTagWidest ?? 0;
        internal int EliteGuardLinksShown => _view?.EliteGuardLinksShown ?? 0;
        internal int MonsterMoundsShown => _view?.MonsterMoundsShown ?? 0;
        internal int MonsterLinksShown => _view?.MonsterLinksShown ?? 0;
        internal int ExplosionsShown => _view?.ExplosionsShown ?? 0;
        // How far the local player has moved with nothing held since a playtest scene (Playtest.cs "coast").
        internal float Coasted { get; set; }
        internal int GuardedNumbersShown => _view?.GuardedNumbersShown ?? 0;
        internal string RenderTrace => _view?.RenderTrace;

        internal int CornersNear => _view == null || _client?.Map == null ? -1
            : _view.CornersNear(TileMap.Cell(_client.Position.X), TileMap.Cell(_client.Position.Y), 10);
        InventoryWindow _inventory;
        // Which screen is open (the bag, growth, records), one at a time (Docs/UiRework.md U2), and the version of it the
        // last OnGUI saw, to let go of the keyboard when it changes.
        ScreenStack _screens = new ScreenStack();
        int _screensSeen;
        // The biome the player is in, how long they have been there, and the last one announced (UpdateBiome).
        Biome _biomeHere;
        float _biomeTime;
        Biome _biomeAnnounced;
        const float BiomeBannerDelay = 1.5f;
        Hotbar _hotbar;
        // The kit last kept (RememberKit).
        int _kitSeen = -1;
        MapView _map;
        // "획득: …" lines for things picked up, each shown for a few seconds.
        readonly List<(string Text, float Age)> _acquired = new List<(string, float)>();
        Camera _camera;
        float _tickClock;
        bool _joinSent;
        // Set from Netcode callbacks; the session ends on the next Update, outside the callback.
        string _endReason;
        // Clears IMGUI's keyboard focus at the next OnGUI, so a name field left focused in the menu does not keep
        // taking keys once the game starts.
        bool _dropFocus;

        void Awake()
        {
            Application.runInBackground = true;
            // 60 frames a second on a phone's own build (30 if the menu says), the display's refresh on a PC's.
            FrameRate.Apply();
            Controls.HearKeysWithoutFocus();
            _camera = Camera.main;
            if (_camera == null)
            {
                // Made here, so tagged as the main one and taken away with this root (2026-11-11: untagged, each play
                // mode test made another camera and listener, up to fifteen "audio listeners in the scene").
                _camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                _camera.tag = "MainCamera";
                _ownCamera = true;
            }
            _camera.orthographic = true;
            _camera.orthographicSize = ViewHalfHeight(Screen.width, Screen.height);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            // The scene's camera has no listener of its own; sounds are 2D and only need one.
            if (_camera.GetComponent<AudioListener>() == null) _camera.gameObject.AddComponent<AudioListener>();

            var sheets = GameSheets.Load();
            if (sheets == null)
            {
                _status = "데이터가 없습니다. 에디터에서 Game > Set Up Project를 실행하세요.";
                return;
            }
            _loot = sheets.LoadLoot();
            _monsters = sheets.LoadMonsters();
            _bosses = sheets.LoadBosses(_monsters);
            _classes = sheets.LoadClasses();
            _tiles = sheets.LoadTiles(_loot);
            _guide = sheets.LoadGuide(_loot);
            _npcs = sheets.LoadNpcs(_loot);
            _story = sheets.LoadStory(_loot, _bosses);
            Strings.Use(sheets.LoadStrings());
            // The days from their sheet; a build from before it (no sheet) keeps the ones in code.
            var days = sheets.LoadEvents();
            if (days.Events.Count > 0) World.DayEvents = days;
            _chosenClasses.RemoveAll(id => !_classes.Classes.Any(c => c.Id == id));
            var errors = new List<string>(_loot.Errors);
            errors.AddRange(_monsters.Errors);
            errors.AddRange(_bosses.Errors);
            errors.AddRange(_classes.Errors);
            errors.AddRange(_tiles.Errors);
            errors.AddRange(_guide.Errors);
            errors.AddRange(_npcs.Errors);
            errors.AddRange(days.Errors);
            errors.AddRange(_story.Errors);
            if (errors.Count > 0) _status = "데이터 시트 오류:\n" + string.Join("\n", errors);
        }

        void Update()
        {
            DevInfo.Frame(Time.unscaledDeltaTime);
            KeyBindings.Poll();
            Controls.UpdateTouches();
            UpdateMusic(Time.unscaledDeltaTime);
            _camera.orthographicSize = ViewHalfHeight(Screen.width, Screen.height);
            if (_endReason != null)
            {
                // A joiner of an online room dropped (a phone sent to the background, a tunnel): try the same code again a
                // few times before giving up (Docs/CrossPlay.md).
                bool rejoin = _mode == Mode.Join && _lastJoinCode != null && _endReason.StartsWith(LostHost) && _rejoinTries < RejoinTries;
                EndSession(_endReason);
                if (rejoin) Rejoin();
                return;
            }
            if (_client == null)
            {
                // A solo or hosted world being made, a slice of time a frame (WorldLoader); the game starts once it is.
                if (_loader != null)
                {
                    if (_loader.Advance()) FinishHosting();
                    return;
                }
                _saveMenu?.Poll();
                return;
            }
            // A joined world's map being made after the host's Welcome (WorldClient.DeferMap), likewise.
            if (_loader == null && _client.MapGeneration != null) _loader = new WorldLoader(_client.MapGeneration, _client.StepMap);
            if (_loader != null && _client.MapGeneration == null && _loader.Done) _loader = null;
            if (_loader != null && _loader.Advance()) _loader = null;
            NoteSteps(Time.unscaledDeltaTime);
            if (_map != null && Controls.MapPressed())
            {
                _screens.Toggle(GameScreen.Map);
                DevInfo.Note(_map.FullOpen ? "지도 열림" : "지도 닫힘");
            }
            else if (Controls.LeavePressed())
            {
                // Esc goes one step back: the hub, the open screen, the talk or waypoint list; with nothing open it opens
                // the menu (U7: the hub's screens, [설정] and [나가기]), from which the game is left.
                if (_screens.Back()) DevInfo.Note("화면 닫힘");
                else if (_storyScreen != null && _storyScreen.Open) _storyScreen.Close();
                else
                {
                    _screens.ToggleHub();
                    DevInfo.Note("메뉴 열림");
                }
            }
            if (_client.Status == ClientStatus.Refused)
            {
                EndSession("참가 거절: " + _client.RefusedReason);
                return;
            }
            // The screens' keys (the person's keys of 2026-11-22): Tab·I the bag, K skills, P passives, U the character, J
            // the journal. A screen's key again closes it; another's switches to it.
            if (_client.Status == ClientStatus.Playing)
            {
                if (Controls.BagPressed()) ToggleScreen(GameScreen.Bag);
                foreach (var screen in new[] { GameScreen.Skills, GameScreen.Passives, GameScreen.Character, GameScreen.Journal })
                    if (Controls.ScreenPressed(screen)) ToggleScreen(screen);
                TestGroundKeys();
            }
            ReadSkillPress();

            _tickClock += Time.deltaTime;
            int ticks = 0;
            while (_tickClock >= World.TickSeconds && ticks < MaxTicksPerFrame)
            {
                _tickClock -= World.TickSeconds;
                ticks++;
                FixedTick();
            }
            if (ticks == MaxTicksPerFrame) _tickClock = 0;

            if (_client.Status != ClientStatus.Playing) return;
            // Time played, for the lists (Docs/TitleRework.md 4-2): the character's on this device, the world's on its host.
            _client.NotePlayed(Time.unscaledDeltaTime);
            _host?.NotePlayed(Time.unscaledDeltaTime);
            // An encounter window shuts once its thing is out of reach (walked away from the stall, chest or station).
            if (ScreenStack.IsEncounter(_screens.Current) && _inventory != null && !_inventory.InReach(_screens.Current)) _screens.Close();
            SaveProgress(Time.deltaTime);
            _view ??= new WorldView(_client, _camera);
            // A world of rooms made before its map was kept under the generator (2026-11-22) left it under the seed alone.
            _map ??= new MapView(_client, SaveStore.Read(MapKey)
                ?? (_client.Generator > WorldGenerator.CaveVersion ? SaveStore.Read(SaveStore.MapKey(_client.Seed, WorldGenerator.CaveVersion, CharacterName)) : null)) { Screens = _screens, Send = SendToHost };
            // A map scrap read marks a thing on this player's map (WorldView.Travel.cs).
            _view.MapScrap ??= () => _map?.ReadScrap();
            _client.AdvanceRenderClock(Time.deltaTime);
            UpdateGuide(Time.deltaTime);
            UpdateBiome(Time.deltaTime);
            UpdateDay();
            RememberKit();
            var changedTiles = _client.TakeChangedTiles();
            _view.UpdateTiles(changedTiles);
            _map.UpdateTiles(changedTiles);
            // A dungeon laid in the pocket or gone back to rock (WorldClient.TakeRebuiltSlots, 묶음 5a).
            var slots = _client.TakeRebuiltSlots();
            if (slots.Count > 0)
            {
                _view.RelaySlots(slots);
                _map.ResetSlots(slots);
            }
            bool bag = _screens.Current == GameScreen.Bag;
            _view.ShowTarget = bag || (_hotbar != null && _hotbar.PointsAtGround);
            _view.TargetSoil = bag && _inventory.PointsAtSoil || _hotbar != null && _hotbar.PointsAtSoil;
            // A hammer's tap now and then while something is being made by hand (2026-11-09).
            if (_client.Crafting != null && Time.time >= _hammerAt)
            {
                Sounds.Play(Sound.Hammer, 0.5f);
                _hammerAt = Time.time + 0.6f;
            }
            _view.TargetSize = Math.Max(bag ? _inventory.PointsAtSize : 1, _hotbar != null ? _hotbar.PointsAtSize : 1);
            // Typing a pet's name keeps the keys from the game; the bag closing lets go of the field.
            bool typing = _inventory != null && _inventory.Typing;
            if (Controls.Typing && !typing && _inventory != null && !_inventory.Open) GUIUtility.keyboardControl = 0;
            Controls.Typing = typing;
            _view.Render(_tickClock / World.TickSeconds, Time.deltaTime);
            UiMotion.NoteHealth(_client.Health);
            UiMotion.HealthShare = _client.IsDead ? 0 : _client.MaxHealth > 0 ? _client.Health / _client.MaxHealth : 1;
            _map.Update();

            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                var (text, age) = _acquired[i];
                if (age + Time.deltaTime > AcquiredSeconds) _acquired.RemoveAt(i);
                else _acquired[i] = (text, age + Time.deltaTime);
            }
            foreach (string line in _client.TakeAcquired())
            {
                _acquired.Add((line, 0));
                Sounds.Play(Sound.Pickup);
            }
            foreach (string line in _client.TakeNotices()) _acquired.Add((line, 0));
        }

        // Half the view's height in tiles for a whole-number zoom: every pixel of the art (a sixteenth of a tile)
        // covers zoom × zoom screen pixels, so pixels look even at any screen size. The zoom keeps about
        // ShortSideTiles tiles across the short side, which also keeps a phone held upright showing as much width.
        static float ViewHalfHeight(int width, int height)
        {
            int zoom = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(width, height) / (ShortSideTiles * Sprites.PixelsPerUnit)));
            return height / (2f * Sprites.PixelsPerUnit * zoom);
        }

        // Passes the guide's goals the player has met, with a line and a sound for each, and keeps how far this
        // character has come on this device.
        // A banner with a biome's name when the player has been in it a moment, once until they go somewhere else, so
        // walking along a border does not keep showing it.
        void UpdateBiome(float deltaTime)
        {
            var biome = _client.Biomes.At(_client.Position);
            // In a dungeon its name card says where (WorldView.Dungeon.cs); its biome is the one settled in, for the music.
            // An area is the biome, the zone and a hunting ground (GameRoot.Zones.cs): a new one of any of them has its banner
            // with the levels it is for.
            int area = AreaKey(_client.Position, biome);
            if (_client.InPocket)
            {
                _biomeHere = _biomeAnnounced = biome;
                _areaHere = _areaAnnounced = area;
                _biomeTime = 0;
                return;
            }
            // The first area of a session is where the player stands: no banner for it.
            if (_areaAnnounced == NoArea) _areaHere = _areaAnnounced = area;
            if (biome != _biomeHere || area != _areaHere)
            {
                _biomeHere = biome;
                _areaHere = area;
                _biomeTime = 0;
                return;
            }
            _biomeTime += deltaTime;
            if (_biomeTime < BiomeBannerDelay || area == _areaAnnounced) return;
            _biomeAnnounced = biome;
            _areaAnnounced = area;
            // Inside a place of the story its own name card says where (WorldView.AnnouncePlace): no zone's warning there.
            if (_client.PlaceHere() == null) _view?.ShowBanner(AreaBanner(_client.Position, biome));
        }

        // Says so when night falls and when morning comes, whether by the clock or by everyone sleeping (Docs/Time.md).
        void UpdateDay()
        {
            var phase = Calendar.Phase(_client.Clock);
            if (_dayPhase == phase) return;
            bool first = _dayPhase == null;
            _dayPhase = phase;
            if (first) return;
            if (phase == DayPhase.Night) _view?.ShowBanner("밤이 되었다");
            else if (phase == DayPhase.Morning)
            {
                // The day's weather and what the calendar holds, when there is anything.
                int day = Calendar.Day(_client.Clock);
                var weather = World.WeatherOf(day);
                var festival = World.EventOf(day);
                string more = (weather != Weather.Clear ? " · " + World.WeatherName(weather) : "") + (festival != DayEvent.None ? " · " + World.EventName(festival) : "");
                _view?.ShowBanner($"{day}일째 아침{more}");
            }
        }

        DayPhase? _dayPhase;

        // Lies down in the player's own bed, or gets up: the button over the bed, for a finger or the mouse.
        // Opens a void rift: the button over the nearest altar while the player has a key and none is open.
        void DrawRiftButton()
        {
            if (!_client.CanOpenRift) return;
            (int X, int Y)? altar = null;
            float best = float.MaxValue;
            int cx = TileMap.Cell(_client.Position.X), cy = TileMap.Cell(_client.Position.Y);
            for (int y = cy - 3; y <= cy + 3; y++)
            {
                for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if (!_client.Map.InBounds(x, y) || _client.Map[x, y] != TileType.RiftAltar) continue;
                    float d = Vec2.Distance(_client.Position, TileMap.Center(x, y));
                    if (d < best) (altar, best) = ((x, y), d);
                }
            }
            if (altar is not (int ax, int ay)) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(ax + 0.5f, ay + 1.35f, 0));
            if (screen.z < 0) return;
            if (!Ui.WorldButton(Ui.FromScreen(screen), $"균열 열기 ({_client.Rift.Unlocked}단계)", 140)) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.OpenRiftCommand());
        }

        // Starts the day's base defense: the button over the nearest ward stone.
        void DrawDefenseButton()
        {
            if (!_client.CanStartDefense) return;
            (int X, int Y)? stone = null;
            float best = float.MaxValue;
            int cx = TileMap.Cell(_client.Position.X), cy = TileMap.Cell(_client.Position.Y);
            for (int y = cy - 3; y <= cy + 3; y++)
            {
                for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if (!_client.Map.InBounds(x, y) || _client.Map[x, y] != TileType.WardStone) continue;
                    float d = Vec2.Distance(_client.Position, TileMap.Center(x, y));
                    if (d < best) (stone, best) = ((x, y), d);
                }
            }
            if (stone is not (int sx, int sy)) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(sx + 0.5f, sy + 1.35f, 0));
            if (screen.z < 0) return;
            if (!Ui.WorldButton(Ui.FromScreen(screen), $"기지 방어 시작 ({_client.Defense.Unlocked}단계)", 160)) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.StartDefenseCommand());
        }

        // Where the last skill press aimed, for the playtests (Playtest "skillAim").
        internal Vec2 LastSkillAim { get; private set; }

        // How far a touch player's skills and taps reach for a foe on their own.
        const float AutoAimReach = 8;

        // The way to the nearest living monster or boss within AutoAimReach, or zero when there is none.
        Vec2 NearestFoeDirection()
        {
            Vec2 best = Vec2.Zero;
            float nearest = AutoAimReach;
            foreach (var other in _client.Others())
            {
                if ((other.Kind != EntityKind.Monster && other.Kind != EntityKind.Boss) || other.Health <= 0) continue;
                var to = other.Position - _client.Position;
                float distance = to.Length;
                if (distance >= nearest || distance < 1e-3f) continue;
                nearest = distance;
                best = to / distance;
            }
            return best;
        }

        // Whether the guide's goal is the boss the main story's taken step asks for (its drop is the goal's).
        bool SameAsStory(Goal goal)
        {
            foreach (var (step, taken) in _client.CurrentSteps)
            {
                if (!taken || step.Chain.Length > 0 || step.Goal != StoryGoal.Boss) continue;
                string drop = _bosses?.Find(step.Boss)?.MaterialId;
                if (goal.Conditions.Any(c => c.Kind == "boss" && c.Id == step.Boss)) return true;
                if (!string.IsNullOrEmpty(drop) && goal.Conditions.Any(c => c.Kind == "have" && c.Id == drop)) return true;
            }
            return false;
        }

        // Where this device's player has got to, for the feedback form (Feedback, Docs/Release.md 의견 단추).
        Vec2? _firstPosition;

        void NoteSteps(float deltaTime)
        {
            // The test ground counts for nothing kept on this device (Docs/TestGround.md).
            if (_client.Status != ClientStatus.Playing || _testGround) return;
            Feedback.Tick(deltaTime);
            Feedback.Reach("load");
            _firstPosition ??= _client.Position;
            if (!Feedback.Has("move") && Vec2.Distance(_firstPosition.Value, _client.Position) > 2) Feedback.Reach("move");
            var passed = _guideProgress?.Passed;
            if (passed != null && passed.Contains("stone")) Feedback.Reach("dig");
            if (passed != null && passed.Contains("workbench")) Feedback.Reach("craft");
            if (_client.Experience > 0 || _client.Level > 1) Feedback.Reach("kill");
            if (_view?.ActiveBoss is EntityView boss && boss.BossState == BossState.Telegraph) Feedback.Reach("boss_warning");
            if (passed != null && passed.Contains("boss")) Feedback.Reach("boss");
        }

        // When the player fell; for the time left before getting up at home in co-op.
        float _downSince;
        bool _wasDown;

        void DrawBedButton()
        {
            if (_client.Bed is not (int x, int y) || !(_client.Sleeping || _client.CanSleep)) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 1.35f, 0));
            if (screen.z < 0) return;
            if (!Ui.WorldButton(Ui.FromScreen(screen), _client.Sleeping ? "일어나기" : "잠자기", 88)) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.Sleeping ? _client.WakeCommand() : _client.SleepCommand());
        }

        // The boss's theme at its phase while a boss is awake on screen, else the biome's tune, urgent in a fight
        // (WorldView.InCombat); the menu has the cave's. Under it the cave's drips or the lava's bubbles, and the hum of
        // the nearest running machine in view. The songs likely next are made ahead (Music.Prepare).
        void UpdateMusic(float deltaTime)
        {
            if (_client == null || _client.Status != ClientStatus.Playing || _client.Map == null)
            {
                Music.Update(Song.Title, null, 0, deltaTime);
                return;
            }
            // The biome the player has settled in (UpdateBiome), so walking along a border does not keep switching; inside
            // a place of the story (2026-11-11), the place's own tune instead.
            var biome = _biomeAnnounced;
            var place = MusicPlace();
            var tune = place != null && PlaceTune(place.Definition.Style) is Tune own ? own : biome switch
            {
                Biome.Mushroom => Tune.Mushroom,
                Biome.Ice => Tune.Ice,
                Biome.Crystal => Tune.Crystal,
                Biome.Lava => Tune.Lava,
                Biome.Abyss => Tune.Abyss,
                Biome.Lake => Tune.Lake,
                Biome.Sand => Tune.Sand,
                _ => Tune.Cave,
            };
            bool fight = _view != null && _view.InCombat;
            Song song;
            // A boss in sight, still asleep: its first phase made ahead for when it wakes.
            if (_view?.BossInSight is string sleeper) Music.Prepare(Song.Of(sleeper, 1));
            if (_view?.ActiveBoss is EntityView boss)
            {
                song = Song.Of(boss.Boss.Id, boss.Phase);
                for (int phase = song.Phase + 1; phase <= MusicArt.BossPhases; phase++) Music.Prepare(Song.Of(boss.Boss.Id, phase));
            }
            else
            {
                // A place's tune softens at night (2026-11-11, MusicArt.Night); a fight still wakes it.
                bool night = place != null && PlaceTune(place.Definition.Style) == tune && Calendar.IsNight(_client.Clock);
                song = Song.Field(tune, fight, night);
                // Inside a dungeon its own recording, where there is one (2026-11-26, Recordings.MusicName).
                if (_client.DungeonHere != null) song = song.In("dungeon");
                Music.Prepare(Song.Field(tune, !fight, night));
            }
            float nearest = float.MaxValue;
            foreach (var machine in _client.Machines())
            {
                if (machine.On) nearest = Mathf.Min(nearest, Vec2.Distance(_client.Position, TileMap.Center(machine.X, machine.Y)));
            }
            // The places close by get their tunes made ahead, so arriving does not wait on them.
            foreach (var site in _client.Places)
            {
                if (site != place && PlaceTune(site.Definition.Style) is Tune next && Vec2.Distance(_client.Position, site.Center) <= site.Radius + PlaceHearing)
                    Music.Prepare(Song.Field(next));
            }
            // The biome's bed (묶음 B1a: the dirt cave's and the mushroom forest's own; the rest keep the bare cave's
            // until their packages).
            var ambience = place != null ? PlaceAmbience(place.Definition.Style) : biome switch
            {
                // The lava zone's and the abyss's (묶음 B4a; the lava zone had the lava's bare rumble).
                Biome.Lava => Ambience.Forge,
                Biome.Abyss => Ambience.Abyss,
                Biome.Cave => Ambience.DirtCave,
                Biome.Mushroom => Ambience.Spores,
                // The ice cave's and the sand cave's (묶음 B2a).
                Biome.Ice => Ambience.Frost,
                Biome.Sand => Ambience.Dunes,
                // The crystal cave's and the lake's (묶음 B3a).
                Biome.Crystal => Ambience.Chimes,
                Biome.Lake => Ambience.Lakeside,
                _ => Ambience.Cave,
            };
            Music.Update(song, ambience, 1 - nearest / MachineHearing, deltaTime);
        }

        // How far outside a place its tune is made ready.
        const float PlaceHearing = 14;
        PlaceSite _musicPlace;

        // The place whose music plays: entered on stepping into its plaza, left a few tiles past its edge.
        PlaceSite MusicPlace()
        {
            var at = _client.Position;
            if (_musicPlace != null && (!_client.Places.Contains(_musicPlace) || Vec2.Distance(at, _musicPlace.Center) > _musicPlace.Radius + 4)) _musicPlace = null;
            if (_musicPlace == null) _musicPlace = _client.Places.FirstOrDefault(p => Vec2.Distance(at, p.Center) <= p.Radius);
            return _musicPlace;
        }

        // Each style of place's tune (MusicArt), or null for a bare plaza, which keeps the cave's.
        static Tune? PlaceTune(string style) => style switch
        {
            "village" => Tune.Village,
            "kingdom" => Tune.Kingdom,
            "hut" => Tune.Hut,
            "nest" => Tune.Nest,
            "harbor" => Tune.Harbor,
            "ruins" => Tune.Ruins,
            "mine" => Tune.Mine,
            _ => null,
        };

        // Under it: water at the harbour, a hearth where people keep house, the cave's drips at the nest and temple.
        static Ambience PlaceAmbience(string style) => style switch
        {
            "harbor" => Ambience.Waves,
            "village" or "kingdom" or "hut" or "mine" => Ambience.Hearth,
            _ => Ambience.Cave,
        };

        // How far off a running machine can be heard.
        const float MachineHearing = 7;

        // After the host gives the player classes or an order, the menu picks those classes next time, and the order
        // is kept on this device for them (Prefs "skillorder.<character>": the classes it is for, then the order).
        void RememberKit()
        {
            if (_client.KitVersion == _kitSeen) return;
            _kitSeen = _client.KitVersion;
            _chosenClasses.Clear();
            _chosenClasses.AddRange(_client.Classes.Select(c => c.Id));
            if (!_testGround) Prefs.Set(SkillOrderKey, string.Join(",", _chosenClasses) + "|" + string.Join(",", _client.SkillOrder));
        }

        // "skillorder3": since 2026-11-08 each class has six skills (Docs/Classes.md, 스킬 포인트와 단계), so the
        // indexes kept before mean other skills and are let go.
        string SkillOrderKey => "skillorder3." + CharacterName;

        // The classes a saved character plays (character save format 16, or before it the classes the skill order was
        // kept for on this device), read once each time the name in the menu changes. A saved character starts with
        // them; changing them is done in the game, beside a workbench, for coins (Docs/Classes.md, 게임 중에 바꾸기).
        string _classesFor;
        List<string> _savedClasses;
        IReadOnlyList<int> _savedOrder;

        // The playtest's "solo" starts a character afresh under a name the menu may have read already.
        internal void ForgetCharacterClasses() => _classesFor = null;

        float _hammerAt;

        void SyncCharacterClasses()
        {
            if (_classesFor == CharacterName || _classes == null || _loot == null) return;
            _classesFor = CharacterName;
            _savedClasses = null;
            _savedOrder = null;
            // Not the last character's classes: a new character starts with none.
            _chosenClasses.Clear();
            var data = SaveStore.Read(SaveStore.CharacterKey(CharacterName));
            if (data == null) return;
            List<string> ids;
            try
            {
                var save = CharacterSave.Read(data, _loot);
                ids = save.Classes.ToList();
                if (ids.Count > 0) _savedOrder = save.SkillOrder;
            }
            catch (System.IO.InvalidDataException)
            {
                return;
            }
            // An older save: the classes this device last kept a skill order for.
            if (ids.Count == 0)
            {
                string kept = Prefs.Get(SkillOrderKey, "").Split('|')[0];
                ids = kept.Split(',').Where(id => id.Length > 0).ToList();
            }
            ids = ids.Where(id => _classes.Classes.Any(c => c.Id == id)).Take(ClassRules.DefaultMaxClasses).ToList();
            if (ids.Count == 0) return;
            _savedClasses = ids;
            _chosenClasses.Clear();
            _chosenClasses.AddRange(ids);
        }

        // The skill order kept for this character, if it is for the classes picked now.
        IReadOnlyList<int> KeptSkillOrder()
        {
            if (_savedOrder != null && _savedOrder.Count > 0 && _savedClasses != null && _savedClasses.SequenceEqual(_chosenClasses)) return _savedOrder;
            string[] parts = Prefs.Get(SkillOrderKey, "").Split('|');
            if (parts.Length != 2 || parts[0] != string.Join(",", _chosenClasses)) return null;
            var order = new List<int>();
            foreach (string part in parts[1].Split(','))
            {
                if (!int.TryParse(part, out int index)) return null;
                order.Add(index);
            }
            return order;
        }

        void UpdateGuide(float deltaTime)
        {
            if (_guideProgress == null || _guideProgress.Current == null) return;
            _guideClock += deltaTime;
            if (_guideClock < GuideCheckSeconds) return;
            _guideClock = 0;
            var facts = GuideFacts.Of(_client);
            var passed = _guideProgress.Update(facts);
            _goalProgress = _guideProgress.Current != null ? GuideProgress.Progress(_guideProgress.Current, facts) : null;
            if (passed.Count == 0) return;
            foreach (var goal in passed)
            {
                if (Settings.Guide) _acquired.Add(("목표 달성: " + goal.Text, 0));
                // The host gives the reward once, if it sees the goal met too.
                if (goal.Rewards.Count > 0 && !_client.ClaimedGoals.Contains(goal.Id)) SendToHost(_client.ClaimGoalCommand(IndexOfGoal(goal)));
            }
            _goalShownAt = Time.time;
            if (!_testGround) Prefs.Set(PassedKey, string.Join(",", _guideProgress.Passed));
            // Turned off in the menu, goals still count and bring their rewards, but nothing is said.
            if (!Settings.Guide) return;
            if (_guideProgress.Current == null) _acquired.Add(("안내한 목표를 모두 마쳤습니다", 0));
            Sounds.Play(Sound.Craft);
        }

        // How far along the current goal is ("3/10"), as of the last check, or null.
        string _goalProgress;

        // The goals passed, by id; before 2026-11-03 how many (GuideKey).
        string PassedKey => "guidepassed." + CharacterName;

        string GuideKey => "guide." + CharacterName;

        int IndexOfGoal(Goal goal)
        {
            for (int i = 0; i < _guide.Goals.Count; i++)
            {
                if (_guide.Goals[i] == goal) return i;
            }
            return -1;
        }

        void StartGuide()
        {
            _biomeHere = _biomeAnnounced = Biome.Cave;
            _areaHere = _areaAnnounced = NoArea;
            _biomeTime = 0;
            if (_guide == null || _guide.Goals.Count == 0)
            {
                _guideProgress = null;
            }
            else
            {
                string passed = Prefs.Get(PassedKey, null);
                _guideProgress = passed != null
                    ? new GuideProgress(_guide, passed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    : new GuideProgress(_guide, GuideProgress.FromCount(_guide, Prefs.Get(GuideKey, 0)));
            }
            _guideClock = GuideCheckSeconds;
            _goalShownAt = Time.time;
            if (_inventory != null) _inventory.Guide = _guideProgress;
        }

        // Playtests: every goal before that one passed, so it is the one now (Playtest "guide").
        internal void GuideUpTo(string id)
        {
            if (_guide == null) return;
            _guideProgress = new GuideProgress(_guide, _guide.Goals.TakeWhile(g => g.Id != id).Select(g => g.Id));
            _guideClock = GuideCheckSeconds;
            if (_inventory != null) _inventory.Guide = _guideProgress;
        }

        // Skills go to the host as soon as they are pressed, not with the next tick's input.
        void ReadSkillPress()
        {
            var slots = Controls.TakeSkillPresses();
            bool potion = Controls.TakePotionPress();
            bool roll = Controls.TakeRollPress();
            bool swap = Controls.TakeSwapPress();
            bool art = Controls.TakeArtPress();
            var quick = Controls.TakeHotbarPresses();
            bool machine = Controls.MachinePressed();
            bool advance = Controls.AdvancePressed();
            if (_screens.Current != GameScreen.None || _screens.HubOpen) return;
            // While a talk is up, G, Space and Enter go to it (Docs/UiRework.md 4-2): the rest of the line, then the next.
            if (_storyScreen != null && _storyScreen.TalkOpen && (machine || advance))
            {
                _storyScreen.Advance();
                machine = false;
            }
            // The roll is the player's own movement (WorldClient.Step), so it waits for the next fixed tick.
            RollPressed |= roll;
            var aim = Controls.SkillAim(_camera, _client.Position);
            // On touch a skill button alone aims at the nearest foe, so it needs no second thumb on the stick.
            if (Controls.UsingTouch && aim.LengthSquared < 0.01f) aim = NearestFoeDirection();
            if (slots.Count > 0) LastSkillAim = aim;
            foreach (int slot in slots) UseSkill(slot, aim);
            if (potion) DrinkPotion();
            if (swap) SwapWeapons();
            if (art) UseWeaponArt(aim);
            // A held art (a greatsword's charge) lets go when its key, button or finger does.
            if (_client.ArtBusy && _client.Art?.Holds == true && !Controls.ArtHeld) ReleaseWeaponArt(aim);
            foreach (int slot in quick) UseQuickSlot(slot);
            // G meets what is in reach (Docs/UiRework.md 4-1, the person's pick of 2026-11-22): see Meet.
            if (machine) Meet();
        }

        // ---- G, the key that meets things (Docs/UiRework.md U2) ----

        // What G would do now, in this order: a machine ahead is switched (its [끄기 (G)]); a person of the story in
        // reach is talked to; a stall keeper in reach is talked to (her talk opens her shop, U3); an open waypoint or
        // waystone opens its list; a chest, cart or shrine of the world is used; a dungeon's gate, door or thing; a storage
        // chest by the player opens the chest window; an anvil the anvil's window (다듬기); a workbench or furnace the
        // station's window (U3: each a window of its own, no longer the bag's tabs). Facing a machine wins because that is
        // aimed; a person before a place because talking is what a person is for.
        internal enum Meeting
        {
            None,
            Machine,
            // The door or cracked wall ahead: lock, unlock or mend it (U6).
            Door,
            Person,
            Merchant,
            Waypoint,
            Poi,
            Dungeon,
            Chest,
            Anvil,
            Station,
        }

        // Asked by every button's label each OnGUI pass, so worked out once a frame.
        int _meetFrame = -1;
        Meeting _meet;

        internal Meeting NextMeeting()
        {
            if (_meetFrame == Time.frameCount) return _meet;
            _meetFrame = Time.frameCount;
            _meet = FindMeeting();
            return _meet;
        }

        Meeting FindMeeting()
        {
            if (_client == null || _client.Status != ClientStatus.Playing || _client.IsDead) return Meeting.None;
            if (_client.MachineAhead() != null) return Meeting.Machine;
            if (DoorWork() != null) return Meeting.Door;
            // Not while a talk is on (G again would start it over).
            if (_storyScreen != null && _storyScreen.Open) return Meeting.None;
            if (!_client.Riding && _client.Places.Count > 0 && _client.CharacterNear() != null) return Meeting.Person;
            if (_client.NpcNear() != null) return Meeting.Merchant;
            if (!_client.Riding && _storyScreen != null && _storyScreen.CanOpenWaypoints) return Meeting.Waypoint;
            if (!_client.Riding && _client.PoiNear() != null) return Meeting.Poi;
            if (!_client.Riding && _client.DungeonChargeKind == 0 && (_client.DungeonThingNear() != null || _client.GateNear() is DungeonGate near && World.GateOpen(_client.Map, near) || _client.ExitNear() != null)) return Meeting.Dungeon;
            if (_client.Chest.IsOpen) return Meeting.Chest;
            if (_client.NearAnvil) return Meeting.Anvil;
            var map = _client.Map;
            if (map != null && (map.Near(_client.Position, TileType.Workbench, World.StationReach) || map.Near(_client.Position, TileType.Furnace, World.StationReach))) return Meeting.Station;
            return Meeting.None;
        }

        // Does what G does now (NextMeeting). False when there is nothing to meet.
        internal bool Meet()
        {
            _meetFrame = -1;
            switch (NextMeeting())
            {
                case Meeting.Machine: return ToggleMachine();
                case Meeting.Door: return DoDoorWork();
                case Meeting.Person: return _storyScreen.TalkNear();
                case Meeting.Merchant: return _storyScreen.TalkMerchantNear();
                case Meeting.Waypoint:
                    Sounds.Play(Sound.Click);
                    return _storyScreen.OpenWaypoints();
                case Meeting.Poi: return UsePoi();
                case Meeting.Dungeon: return UseDungeon();
                case Meeting.Chest: return OpenEncounter(GameScreen.Chest);
                case Meeting.Anvil: return OpenEncounter(GameScreen.Anvil);
                case Meeting.Station: return OpenEncounter(GameScreen.Station);
                default: return false;
            }
        }

        bool OpenEncounter(GameScreen screen)
        {
            Sounds.Play(Sound.Click);
            _inventory.ShowEncounter(screen);
            return true;
        }

        // Buttons over the storage chest, the crafting stations and the anvil in reach (U3, Docs/UiRework.md U2 남은 문제:
        // a workbench had no sign that G opens it), for a finger or the mouse; on a PC the one G would open says "(G)".
        void DrawStationButtons()
        {
            if (_client.Riding || _client.Map == null) return;
            if (_client.Chest.IsOpen) StationButton(_client.Chest.X, _client.Chest.Y, "상자 열기" + MeetHint(Meeting.Chest), GameScreen.Chest);
            foreach (var (tile, name, screen, meeting) in new[] { (TileType.Anvil, "모루", GameScreen.Anvil, Meeting.Anvil), (TileType.Workbench, "작업대", GameScreen.Station, Meeting.Station), (TileType.Furnace, "화로", GameScreen.Station, Meeting.Station) })
            {
                if (NearestTile(tile) is not (int x, int y)) continue;
                StationButton(x, y, $"{name} 열기" + MeetHint(meeting), screen);
            }
        }

        void StationButton(int x, int y, string text, GameScreen screen)
        {
            Vector3 point = _camera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 1.45f, 0));
            if (point.z < 0) return;
            float width = Mathf.Min(Ui.Width - 16, 40 + 12 * text.Length);
            if (Ui.WorldButton(Ui.FromScreen(point), text, width)) OpenEncounter(screen);
        }

        // The nearest tile of the kind within reach of the player (World.StationReach), or null.
        (int X, int Y)? NearestTile(TileType tile)
        {
            var map = _client.Map;
            int r = (int)Mathf.Ceil(World.StationReach) + 1;
            int cx = TileMap.Cell(_client.Position.X), cy = TileMap.Cell(_client.Position.Y);
            (int, int)? best = null;
            float bestDistance = float.MaxValue;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!map.InBounds(x, y) || map[x, y] != tile) continue;
                    float distance = Vec2.Distance(_client.Position, TileMap.Center(x, y));
                    if (distance > World.StationReach || distance >= bestDistance) continue;
                    best = (x, y);
                    bestDistance = distance;
                }
            }
            return best;
        }

        // On a PC, " (G)" after the label of a button over the thing G would meet now, so the key is learnt.
        string MeetHint(Meeting meeting) => !Controls.UsingTouch && NextMeeting() == meeting ? $" ({KeyBindings.Name(KeyAction.Machine)})" : "";

        // A screen's key: closes it when open, else shows it in place of any other.
        void ToggleScreen(GameScreen screen)
        {
            _screens.Toggle(screen);
            DevInfo.Note(_screens.Current == screen ? $"{ScreenStack.NameOf(screen)} 열림" : $"{ScreenStack.NameOf(screen)} 닫힘");
        }

        ScreenStack NewScreens()
        {
            var screens = new ScreenStack();
            // A screen opening ends the talk and shuts the waypoint list (Docs/UiRework.md 4-1: 대화는 다른 화면 위에 남지 않는다).
            screens.Opened += () => _storyScreen?.Close();
            return screens;
        }

        // Opens the chest or cart, or prays at the shrine, within reach (World.UsePoi): the key, or the button over it.
        // False when there is none.
        internal bool UsePoi()
        {
            if (_client == null || _client.Riding || _client.PoiNear() is not Poi poi) return false;
            Sounds.Play(Sound.Click);
            SendToHost(_client.UsePoiCommand(poi));
            return true;
        }

        // ---- Dungeons (World.Dungeon.cs, 묶음 5a) ----

        // Goes into the dungeon at the gate within reach, or out by the door of the dungeon within reach: the key, or the
        // button over it. False when there is neither, or the player is going in or out already.
        internal bool UseDungeon()
        {
            if (_client == null || _client.IsDead || _client.DungeonChargeKind != 0) return false;
            // A chest or shrine of the dungeon within reach comes first (묶음 5b).
            if (_client.DungeonThingNear() is DungeonThing thing)
            {
                Sounds.Play(Sound.Click);
                SendToHost(_client.UseDungeonThingCommand(thing));
                return true;
            }
            if (_client.GateNear() is DungeonGate gate && World.GateOpen(_client.Map, gate))
            {
                Sounds.Play(Sound.Click);
                SendToHost(_client.EnterDungeonCommand(gate));
                return true;
            }
            if (_client.ExitNear() == null) return false;
            Sounds.Play(Sound.Click);
            SendToHost(_client.LeaveDungeonCommand(byMenu: false));
            return true;
        }

        // Out of the dungeon from anywhere in it, slower (the button under the time lines).
        internal bool LeaveDungeonFromMenu()
        {
            if (_client == null || _client.IsDead || _client.DungeonHere == null || _client.DungeonChargeKind != 0) return false;
            Sounds.Play(Sound.Click);
            SendToHost(_client.LeaveDungeonCommand(byMenu: true));
            return true;
        }

        // A button over the gate within reach ("○○의 던전 들어가기", with how many are in it), or over the door out of the
        // dungeon; and, in a dungeon, [던전 나가기] under the time lines. While going in or out, a bar fills under the
        // player. For a finger or the mouse; on a keyboard the gate's and door's show the key, which does the same.
        void DrawDungeonButtons()
        {
            if (_client.Riding) return;
            bool going = _client.DungeonChargeKind != 0;
            var gate = _client.GateNear();
            var thing = gate == null ? _client.DungeonThingNear() : null;
            var exit = gate == null && thing == null ? _client.ExitNear() : null;
            if (gate != null || exit != null || thing != null)
            {
                var spot = gate != null ? gate.Center : thing != null ? thing.Center : TileMap.Center(exit.Value.X, exit.Value.Y);
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(spot.X, spot.Y + 1.5f, 0));
                if (screen.z >= 0)
                {
                    Vector2 point = Ui.FromScreen(screen);
                    string text;
                    // A camp's trapdoor still shut (B1c): it says what opens it, and cannot be pressed.
                    bool shut = gate != null && !World.GateOpen(_client.Map, gate);
                    if (shut) text = "잠긴 뚜껑 문 · 야영지의 대장을 쓰러뜨리면 열린다";
                    else if (gate != null)
                    {
                        var standing = _client.DungeonOf(gate);
                        int inside = standing == null ? 0 : _client.PlayersIn(standing);
                        text = $"{_client.DungeonNameOf(gate)} 들어가기" + (inside > 0 ? $" (안에 {inside}명)" : "");
                    }
                    else if (thing != null) text = DungeonThingLabel(thing);
                    else text = "던전 나가기";
                    if (going) text = gate != null ? "들어가는 중…" : "나가는 중…";
                    else if (!shut) text += MeetHint(Meeting.Dungeon);
                    float width = Mathf.Min(Ui.Width - 16, 40 + 12 * text.Length);
                    GUI.enabled = !going && !_client.IsDead && !shut;
                    if (Ui.WorldButton(point, text, width)) UseDungeon();
                    GUI.enabled = true;
                }
            }
            if (_client.DungeonHere != null && !_client.IsDead)
            {
                var info = Ui.InfoArea(0);
                var rect = new Rect(Ui.MinimapArea.xMax - 120, Mathf.Max(info.y, Ui.InfoBottom) + 4, 120, 30);
                Controls.Block(rect);
                GUI.enabled = !going;
                if (Ui.Button(rect, _client.DungeonChargeKind == World.DungeonLeavingMenu ? "나가는 중…" : "던전 나가기")) LeaveDungeonFromMenu();
                GUI.enabled = true;
            }
            if (going)
            {
                Vector3 feet = _camera.WorldToScreenPoint(new Vector3(_client.Position.X, _client.Position.Y - 0.9f, 0));
                if (feet.z < 0) return;
                Vector2 under = Ui.FromScreen(feet);
                var bar = new Rect(under.x - 40, under.y, 80, 8);
                var previous = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = new Color(0.72f, 0.55f, 1f);
                GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * _client.DungeonCharge, bar.height - 2), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        // What the button over a dungeon's chest or shrine says (묶음 5b).
        static string DungeonThingLabel(DungeonThing thing) => thing.Kind == DungeonThingKind.Shrine
            ? $"{thing.Definition?.Name ?? "제단"}에 기도하기"
            : thing.End ? "보상 상자 열기" : $"{thing.Definition?.Name ?? "상자"} 열기";

        // The dungeon's master while someone fights it: the bar of the leader standing in the boss room (묶음 5b).
        void DrawDungeonBossBar()
        {
            var dungeon = _client.DungeonHere;
            var room = dungeon?.Layout.Room('B');
            if (room == null || dungeon.Has(DungeonFlags.BossDown) || !room.Contains(_client.Position, 2)) return;
            foreach (var other in _client.Others())
            {
                if (other.Elite != EliteRank.Leader || !room.Contains(other.Position, 4) || other.Health <= 0) continue;
                var kind = other.Monster;
                var place = BossBarArea(0);
                float width = place.width, x = place.x, top = place.y;
                var title = new GUIContent($"{dungeon.Name}의 주인 · {World.EliteName(kind, EliteRank.Leader, other.Affixes)} · {World.EliteAffixLine(other.Affixes)}");
                float titleHeight = GUI.skin.label.CalcHeight(title, width);
                GUI.Label(new Rect(x, top, width, titleHeight), title);
                UiSkin.Bar(new Rect(x, top + titleHeight, width, 18), other.MaxHealth > 0 ? Mathf.Clamp01(other.Health / other.MaxHealth) : 0, "", boss: true);
                if (Event.current.type == EventType.Repaint) BossBarShown = new Rect(x, top, width, titleHeight + 18);
                return;
            }
        }

        // Opens the list of waypoints and waystones, as the button over an open one does (Playtest "waypoints").
        internal bool OpenWaypoints() => _storyScreen != null && _storyScreen.OpenWaypoints();
        // The talk and the rumours (묶음 4c), for the playtests.
        internal bool TalkNear() => _storyScreen != null && _storyScreen.TalkNear();
        internal string TalkText => _storyScreen?.TalkText;
        // The talk box (U3): whether one is up, its line still being typed, the portrait ("id:Expression"), where it was
        // drawn; a stall keeper's talk in reach; the shop's tab and who keeps the stall in reach.
        internal bool Talking => _storyScreen != null && _storyScreen.TalkOpen;
        internal bool TalkTyping => _storyScreen != null && _storyScreen.Typing;
        internal string TalkPortrait => _storyScreen?.PortraitShown ?? "";
        internal string TalkLine => _storyScreen?.LineShown ?? "";
        internal bool TalkMerchantNear() => _storyScreen != null && _storyScreen.TalkMerchantNear();
        internal string ShopTab => _inventory?.ShopTab ?? "";
        // The growth screens' state for the playtests (U4): the tree's zoom, pan, sky and picked point, the picked skill,
        // a phone's sheet, the drawer, the skill waiting for a slot.
        internal InventoryWindow.GrowthState Growth => _inventory?.Growth ?? default;
        // The records (U5): the journal's section, the bestiary's group and picked entry, what the goal list follows.
        internal string JournalSection => _inventory?.JournalSectionName ?? "";
        internal string BestiaryGroup => _inventory?.BestiaryGroupName ?? "";
        internal string BestiaryPick => _inventory?.BestiaryPick ?? "";
        internal string Tracked => _screens?.Tracked ?? "";
        internal void ShowJournal(InventoryWindow.JournalSection section) => _inventory?.ShowJournal(section);
        internal string Shopkeeper => _client?.NpcNear()?.Npc.Name ?? "";
        internal string LastRumor => _storyScreen?.LastRumor;
        internal MapView Map => _map;

        // A button over the chest, cart or shrine within reach that would give now (Docs/WorldRework.md 3단계 볼거리),
        // for a finger or the mouse; on a keyboard it shows the key, which does the same.
        void DrawPoiButton()
        {
            if (_client.Riding || _client.PoiNear() is not Poi poi || _client.MachineAhead() != null) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(poi.X + 0.5f, poi.Y + 1.5f, 0));
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            string text = $"{poi.Definition.Name} {PoiText.Verb(poi)}";
            bool usable = true;
            // A minecart station (3c): ride, mend the broken end, or why not (WorldView.Travel.cs).
            if (poi.Kind == PoiKind.Station) (text, usable) = TravelText.StationButton(_client, poi);
            if (usable) text += MeetHint(Meeting.Poi);
            float width = Mathf.Min(Ui.Width - 16, 40 + 12 * text.Length);
            GUI.enabled = usable;
            if (Ui.WorldButton(point, text, width)) UsePoi();
            GUI.enabled = true;
        }

        // Switches the machine ahead off or on: the key, or the button over it. False when there is none.
        internal bool ToggleMachine()
        {
            if (_client?.MachineAhead() == null) return false;
            Sounds.Play(Sound.Click);
            SendToHost(_client.ToggleMachineCommand());
            return true;
        }

        // A button over the machine ahead that switches it off or on, for a finger or the mouse. The key does the
        // same; on a keyboard the button shows which key.
        void DrawMachineSwitch()
        {
            if (_client.MachineAhead() is not MachineView machine) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(machine.X + 0.5f, machine.Y + 1.35f, 0));
            if (screen.z < 0) return;
            string text = machine.On ? "끄기" : "켜기";
            if (!Controls.UsingTouch) text += $" ({KeyBindings.Name(KeyAction.Machine)})";
            if (Ui.WorldButton(Ui.FromScreen(screen), text, 80)) ToggleMachine();
        }

        // Does what the quick slot holds: puts on the gear, uses the potion or food, places, plants or waters.
        internal bool UseQuickSlot(int slot)
        {
            var (command, sound) = _hotbar?.Use(slot) ?? (null, null);
            if (command == null) return false;
            if (sound is Sound played) Sounds.Play(played);
            SendToHost(command);
            return true;
        }

        // Also called by the play mode tests. False when there is none, it is still waiting or health is full.
        internal bool DrinkPotion()
        {
            byte[] command = _client?.UseConsumableCommand(_client.QuickConsumable);
            if (command == null) return false;
            SendToHost(command);
            return true;
        }

        // The weapon in hand for the spare (Docs/Weapons.md). Also called by the play mode tests. False with neither.
        internal bool SwapWeapons()
        {
            if (_client == null || _client.Equipment[ItemSlot.Weapon] == null && _client.Equipment.Spare == null) return false;
            Sounds.Play(Sound.Click);
            SendToHost(_client.SwapWeaponsCommand());
            return true;
        }

        // The weapon's own art (Docs/Weapons.md 묶음 C). Also called by the playtests. False when it cannot be used now.
        internal bool UseWeaponArt(Vec2 aim = default)
        {
            byte[] command = _client?.ArtCommand(aim);
            if (command == null) return false;
            SendToHost(command);
            return true;
        }

        internal bool ReleaseWeaponArt(Vec2 aim = default)
        {
            byte[] command = _client?.ArtReleaseCommand(aim);
            if (command == null) return false;
            SendToHost(command);
            return true;
        }

        void FixedTick()
        {
            NoteJoined();
            var (move, aim, attack) = Controls.Read(_camera, _client.Position);
            // Clicking in the bag, on a screen or the hub should not swing the sword; a full screen also holds the walk
            // (Docs/UiRework.md U2: the world takes no input under it).
            if (_screens.Current != GameScreen.None || _screens.HubOpen) attack = false;
            // Space goes to a talk on screen, not the sword (Docs/UiRework.md 4-2).
            if (_storyScreen != null && _storyScreen.TalkOpen && Controls.SpaceHeld) attack = false;
            if (_screens.Covers) move = Vec2.Zero;
            if (HoldAttack) attack = true;
            // A tap on the attack stick without pulling it swings at the nearest foe rather than where one walks
            // (Docs/WebDemo.md 터치, 2026-11-11, the research report's candidate 16).
            if (Controls.UsingTouch && attack && !Controls.AimPulled)
            {
                var foe = NearestFoeDirection();
                if (foe.LengthSquared > 0.01f) aim = foe;
            }
            if ((HoldAttack || AimHeld) && HeldAim.LengthSquared > 1e-6f) aim = HeldAim;
            var before = _client.Position;
            byte[] input = _client.Step(move, aim, attack, RollPressed);
            if (move.LengthSquared < 1e-6f && !_client.Rolling) Coasted += Vec2.Distance(before, _client.Position);
            RollPressed = false;
            if (input != null) SendToHost(input);

            if (_host == null) return;
            // Only a playtest runs a solo world faster than time (Playtest.cs, "speed").
            // 0 holds the world still (for a picture), as the playtest's "speed 0" says.
            int ticks = _mode == Mode.Solo ? Mathf.Clamp(HostTicksPerStep, 0, 60) : 1;
            for (int i = 0; i < ticks; i++)
            {
                _host.Tick();
                StrikeRoller();
                foreach (var message in _host.TakeOutgoing())
                {
                    if (message.Connection == LocalConnection) _client.Receive(message.Data);
                    else _net?.Send(message.Connection, message.Data, message.Reliable);
                }
            }
        }

        // Playtests ("rollhit", Playtest.cs): a blow on the player so many host ticks into their next roll, to see
        // that it goes through inside the roll's unhurt ticks and lands after them.
        float _rollHit;
        int _rollHitAfter;
        int _rollHitRolls;
        int _rollHitTicks;

        internal void ArmRollHit(float amount, int afterTicks)
        {
            _rollHit = amount;
            _rollHitAfter = afterTicks;
            _rollHitRolls = HostPlayer?.Rolls ?? 0;
            _rollHitTicks = 0;
        }

        void StrikeRoller()
        {
            var player = _rollHit > 0 ? HostPlayer : null;
            if (player == null || player.Rolls <= _rollHitRolls || ++_rollHitTicks < _rollHitAfter) return;
            HostWorld.Hurt(player, _rollHit);
            _rollHit = 0;
        }

        void SendToHost(byte[] data)
        {
            if (_host != null) _host.Receive(LocalConnection, data);
            else _net?.Send(NetworkManager.ServerClientId, data, reliable: Protocol.TypeOf(data) != MessageType.Input);
        }

        // The menu buttons, also called by the play mode tests.
        internal void StartSolo() => StartHosting(Mode.Solo);

        internal void StartHost() => StartHosting(Mode.Host);

        internal bool Playing => _client != null && _client.Status == ClientStatus.Playing;

        internal bool IsSolo => _mode == Mode.Solo;

        // The cloud's playtests of the web demo call this from the page: unityInstance.SendMessage("Game", "Playtest",
        // "<commands>"). See Playtest.cs and Docs/Playtest.md.
        [UnityEngine.Scripting.Preserve]
        void Playtest(string commands) => Game.Client.Playtest.Run(this, commands);

        // For the play mode tests (Assets/Tests/PlayMode/FeatureTests.cs): the host's world in a solo or hosted game
        // and this player in it, the sheets, and the attack held down toward HeldAim as if the button were.
        internal World HostWorld => _host?.World;
        // The world as the generator made it: its rooms and what each is for (Playtest "rooms", "goto room").
        internal GeneratedWorld HostGenerated => _host?.Generated;
        internal WorldClient LocalClient => _client;
        internal Player HostPlayer => _client == null ? null : _host?.World.FindPlayer(_client.PlayerId);
        internal MonsterData MonsterSheet => _monsters;
        internal BossData BossSheet => _bosses;
        internal LootData LootSheet => _loot;
        internal ClassData ClassSheet => _classes;
        // The guide's goals, for the playtest bot's numbers (PlaytestBot.cs).
        internal GuideData GuideSheet => _guide;
        internal bool HoldAttack { get; set; }
        // The roll key or button was pressed since the last fixed tick (also set by Playtest.cs, "roll").
        internal bool RollPressed { get; set; }
        internal Vec2 HeldAim { get; set; }
        // Facing HeldAim without attacking, as a mouse held to one side does (Playtest.cs, "face").
        internal bool AimHeld { get; set; }
        // Host ticks to each fixed step in a solo game: more runs the world faster (Playtest.cs, "speed").
        internal int HostTicksPerStep { get; set; } = 1;

        // What the screen says, for the playtests: the HUD's lines as last drawn, the "획득: …" lines and notices in
        // the corner, and the banner in the middle while it shows.
        internal IReadOnlyList<string> HudLines => _hudLines;
        internal IEnumerable<string> AcquiredLines => _acquired.Select(a => a.Text);
        internal string Banner => _view?.ShowingBanner;
        internal bool EntranceFits => _view == null || _view.EntranceFits;
        // The boss whose entrance is on show: its id, name and title as the card writes them, or null.
        internal string EntranceShown => _view?.EntranceShown is string id && _bosses?.Bosses.FirstOrDefault(b => b.Id == id) is BossDefinition entering
            ? $"{id}|{entering.Name}|{entering.Arena?.Title}" : null;
        internal int OpenDoors => _view?.OpenDoors ?? 0;
        internal bool MapOpen => _map != null && _map.FullOpen;
        internal IEnumerable<string> FloatingTexts => _view?.FloatingTexts ?? Enumerable.Empty<string>();
        string[] _hudLines = Array.Empty<string>();

        // The bag window open or shut, for the play mode tests' screenshots.
        // Where the player stands on the screen (a point-sized rect at the feet's middle), and whether a boss bar is up,
        // for the playtests' UI report.
        internal Rect PlayerOnScreen
        {
            get
            {
                if (_client == null || _camera == null) return default;
                var screen = _camera.WorldToScreenPoint(new Vector3(_client.Position.X, _client.Position.Y, 0));
                var point = Ui.FromScreen(screen);
                return new Rect(point.x, point.y, 0, 0);
            }
        }

        internal bool BossShown => _view?.ActiveBoss != null;

        // Whether one of the screens that were the bag's tabs is up (the bag, a growth screen or the journal; the playtests'
        // "bag"); set, it opens the bag or shuts that screen.
        internal bool BagOpen
        {
            get => _inventory.Open;
            set => _inventory.Open = value;
        }

        // Opens the page of that old tab name: the bag's pages in the bag, 스킬·특성·캐릭터·일지·지도 as their screens.
        internal void ShowBag(string page) => _inventory.Show(page);

        // The open screen by name ("" for none), the hub, and opening one by name (Playtest "screen", Docs/UiRework.md U2).
        internal string ScreenName => ScreenStack.NameOf(_screens.Current);
        internal GameScreen OpenScreen => _screens.Current;
        internal bool HubOpen => _screens.HubOpen;
        internal bool LeaveAsked => _screens.LeaveAsked;
        internal void AskLeave() => _screens.AskLeave();
        internal string BagPage => _inventory?.BagPage ?? "";
        // The bag's kind chip and the picked thing's card title (U6), for the playtests.
        internal string BagFilter => _inventory?.FilterName ?? "";
        internal bool SetBagFilter(string name) => _inventory != null && _inventory.SetFilter(name);
        internal string BagCard => _inventory?.CardTitle ?? "";
        internal string QuickNames => _hotbar?.SlotNames() ?? "";

        internal void ShowScreen(GameScreen screen) => _screens.Show(screen);

        internal void ShowHub(bool open)
        {
            if (open != _screens.HubOpen) _screens.ToggleHub();
        }

        // Whether the frame on the tile a [놓기] would build on is drawn (WorldView.ShowTarget).
        internal bool TargetShown => _view != null && _view.ShowTarget;
        // The song playing (MusicArt Song.Key), for the playtest.
        internal string MusicPlaying => Music.Playing;

        internal string Status => _status;

        internal int SkillCount => _client?.Skills.Count ?? 0;

        // The base of the weapon in hand, for the play mode tests.
        internal string HeldWeaponId => _client?.Equipment[ItemSlot.Weapon]?.Base.Id;

        // False when the skill is not ready. Zero aim uses it the way the player faces.
        internal bool UseSkill(int slot, Vec2 aim = default)
        {
            byte[] command = _client?.SkillCommand(slot, aim);
            if (command == null) return false;
            SendToHost(command);
            return true;
        }

        // Makes an online room and hosts in it; the code shows in the HUD for the others.
        async void StartOnlineHost()
        {
            if (_connecting || _client != null || _loader != null) return;
            _connecting = true;
            _status = "온라인 방을 만드는 중…";
            var (link, code, error) = await NetLink.HostOnline(OnlineGuests);
            _connecting = false;
            if (link == null)
            {
                _status = "온라인 방을 만들지 못했습니다. " + error;
                return;
            }
            // Something else started while the room was being made.
            if (_client != null || _loader != null)
            {
                link.Dispose();
                return;
            }
            _roomCode = code;
            StartHosting(Mode.Host, link);
        }

        async void StartOnlineJoin()
        {
            string code = _codeInput.Trim();
            if (_connecting || _client != null || _loader != null || code.Length == 0) return;
            _connecting = true;
            _status = "방을 찾는 중…";
            var (link, error) = await NetLink.JoinOnline(code);
            _connecting = false;
            if (link == null)
            {
                _status = "참가하지 못했습니다. " + error;
                if (_rejoinTries > 0 && _rejoinTries < RejoinTries) Rejoin();
                return;
            }
            _lastJoinCode = code;
            if (_client != null)
            {
                link.Dispose();
                return;
            }
            StartJoining(link);
        }

        // Solo, a LAN host on the port, or a host in an online room whose link is already started. The world is made over
        // the next frames (WorldLoader, Docs/TitleRework.md 4-4) and the game starts when it is (FinishHosting).
        void StartHosting(Mode mode, NetLink online = null)
        {
            if (_loader != null || _client != null)
            {
                online?.Dispose();
                return;
            }
            // Saves are read first, so a broken one stops the start with a reason instead of being overwritten.
            if (!LoadCharacter(out byte[] character) || !ReadWorld(out var world))
            {
                online?.Dispose();
                _roomCode = null;
                EndTestGround();
                return;
            }

            if (mode == Mode.Host)
            {
                _net = online ?? NetLink.Host(NetLink.DefaultPort);
                if (_net == null)
                {
                    _status = $"호스트를 시작하지 못했습니다. 포트 {NetLink.DefaultPort}를 다른 프로그램이 쓰고 있는지 확인하세요.";
                    return;
                }
                // Whoever comes while the world is still being made waits for it (their Join is held, not lost).
                _net.Received += (sender, data) =>
                {
                    if (_host != null) _host.Receive(sender, data);
                    else _heldForHost.Add((sender, data));
                };
                _net.Disconnected += id =>
                {
                    if (_host != null) _host.Disconnect(id);
                    else _heldForHost.Add((id, null));
                };
            }

            // A new world takes the seed its page was given (Docs/TitleRework.md 4-2), or one at random.
            ulong seed = world?.Seed ?? _startSeed ?? (ulong)DateTime.UtcNow.Ticks;
            _startSeed = null;
            // A new world is the game's big map; a saved one keeps the size it was made at (2026-11-11), and the
            // generator that made it (2026-11-21: a world from before rooms stays the caves it was).
            int size = world?.Width ?? WorldGenerator.GameSize;
            int version = world?.Generator ?? WorldGenerator.NewWorldVersion;
            _starting = (mode, character, world, seed, size, version);
            _loader = new WorldLoader(new WorldGeneration(seed, size, size, _story, _bosses?.Arenas, version, _tiles?.Terrain));
            _status = "";
            PrepareLoading();
        }

        // The world being made for a solo or hosted game (StartHosting), and what the game starts with once it is.
        WorldLoader _loader;
        (Mode Mode, byte[] Character, WorldSave World, ulong Seed, int Size, int Version) _starting;
        // What came over the network for the host before it was there.
        readonly List<(ulong Sender, byte[] Data)> _heldForHost = new List<(ulong, byte[])>();

        // The world is made: the host builds on it, and this player's own client starts from a copy of the map as made,
        // so it is not made twice (2026-11-25).
        void FinishHosting()
        {
            var (mode, character, world, seed, size, version) = _starting;
            var generated = _loader.World;
            _loader = null;
            _starting = default;
            var made = generated.Copy();
            _host = new WorldHost(seed, _monsters, _loot,
                new HostConfig { MapSize = size, MapVersion = version },
                bosses: _bosses, classes: _classes, tiles: _tiles, guide: _guide, npcs: _npcs,
                worldConfig: new WorldConfig { Forage = true, DayMinutes = Settings.DayMinutes, QuickRest = Settings.QuickRest, Director = true }, story: _story,
                generated: generated);
            if (world != null)
            {
                _host.Restore(world);
                foreach (string warning in world.Warnings) _acquired.Add(("월드 저장본: " + warning, 0));
            }
            _worldKey = SaveStore.WorldKey(WorldName);
            _worldSaveClock = 0;
            SyncCharacterClasses();
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses, _tiles)
            {
                Look = NewLook,
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
                Story = _story,
                Made = made,
            };
            _kitSeen = -1;
            _hotbar = new Hotbar(_client, _loot, CharacterName) { Keep = !_testGround };
            _screens = NewScreens();
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar, _screens) { Map = () => _map };
            _storyScreen = new StoryScreen(_client, SendToHost, () => _map) { OpenShop = tab => _inventory?.ShowShop(tab) };
            // The test ground (Docs/TestGround.md): the host keeps the hit log and may keep a player from falling there.
            if (_testGround)
            {
                _host.World.TestGround = true;
                // Longer than the other lines (from 8 seconds before): it is the one thing to read on coming in.
                _acquired.Add(($"시험장: 여기서 한 일은 저장되지 않는다. 시험판은 {(Controls.UsingTouch ? "[메뉴] → [시험판]" : TestGround.PanelKeyName + " 또는 Esc → [시험판]")}", -8));
            }
            _host.Receive(LocalConnection, _client.JoinMessage());
            foreach (var (sender, data) in _heldForHost)
            {
                if (data != null) _host.Receive(sender, data);
                else _host.Disconnect(sender);
            }
            _heldForHost.Clear();
            _mode = mode;
            _status = "";
            _dropFocus = true;
            StartGuide();
        }

        // How a new character looks (CharacterLook): set by the making screen before the game starts (T3). A saved
        // character keeps its own.
        internal CharacterLook NewLook { get; set; } = CharacterLook.Default;

        // The world being made, for the loading screen (Docs/TitleRework.md 4-4): whether it is, the stage (GenerationStage's
        // keys, LoadText for the words) and how far along, 0 to 1. A joined world counts from the host's Welcome.
        internal bool Loading => _loader != null && !_loader.Done;
        internal string LoadStage => _loader?.Stage ?? "";
        internal float LoadFraction => _loader?.Fraction ?? 0;

        // What the loading screen says while a stage is made.
        internal static string LoadText(string stage) => stage switch
        {
            GenerationStage.Caves => Strings.T("load.caves", "동굴을 파는 중"),
            GenerationStage.Biomes => Strings.T("load.biomes", "땅의 결을 나누는 중"),
            GenerationStage.Rooms => Strings.T("load.rooms", "방과 길을 잇는 중"),
            GenerationStage.Floors => Strings.T("load.floors", "방바닥을 고르는 중"),
            GenerationStage.Ways => Strings.T("load.ways", "통로와 물길을 내는 중"),
            GenerationStage.Features => Strings.T("load.features", "방을 채우는 중"),
            GenerationStage.Ore => Strings.T("load.ore", "광맥을 묻는 중"),
            GenerationStage.Pools => Strings.T("load.pools", "웅덩이에 물을 채우는 중"),
            GenerationStage.Places => Strings.T("load.places", "마을을 세우는 중"),
            GenerationStage.Arenas => Strings.T("load.arenas", "보스의 방을 꾸미는 중"),
            GenerationStage.Furnish => Strings.T("load.furnish", "방마다 쓰임을 정하는 중"),
            GenerationStage.Pois => Strings.T("load.pois", "보물을 숨기는 중"),
            GenerationStage.Rails => Strings.T("load.rails", "광차 선로를 놓는 중"),
            GenerationStage.Join => Strings.T("load.join", "막힌 길을 트는 중"),
            GenerationStage.Props => Strings.T("load.props", "동굴을 꾸미는 중"),
            GenerationStage.Cellars => Strings.T("load.cellars", "굴과 지하실을 파는 중"),
            GenerationStage.Water => Strings.T("load.water", "얕은 물과 틈을 내는 중"),
            GenerationStage.Pocket => Strings.T("load.pocket", "던전 자리를 마련하는 중"),
            GenerationStage.Done => Strings.T("load.done", "들어가는 중"),
            _ => Strings.T("load.start", "월드를 만드는 중"),
        };

        // The world save of this name, or null for a new world. False when there is one that cannot be read.
        bool ReadWorld(out WorldSave world)
        {
            world = null;
            // The test ground is made new each time from its seed, whatever world of that name there may be.
            if (_testGround) return true;
            byte[] worldData = SaveStore.Read(SaveStore.WorldKey(WorldName));
            if (worldData == null) return true;
            try
            {
                world = WorldSave.Read(worldData, _tiles, _loot);
                return true;
            }
            catch (InvalidDataException e)
            {
                _status = $"월드 '{WorldName}'의 저장본을 읽지 못했습니다: {e.Message} 다른 월드를 고르거나 월드 목록의 [지우기]로 지우세요.";
                return false;
            }
        }

        // To a LAN host at the address, or into an online room through a link from NetLink.JoinOnline, started here
        // once its events are heard.
        void StartJoining(NetLink online = null)
        {
            if (!LoadCharacter(out byte[] character))
            {
                online?.Dispose();
                return;
            }
            _net = online ?? NetLink.Join(_address.Trim(), NetLink.DefaultPort);
            if (_net == null)
            {
                _status = "접속을 시작하지 못했습니다. 주소를 확인하세요.";
                return;
            }
            SyncCharacterClasses();
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses, _tiles)
            {
                Look = NewLook,
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
                Story = _story,
                // The map is made a stage at a time after Welcome, behind the loading screen (Docs/TitleRework.md 4-4).
                DeferMap = true,
            };
            _kitSeen = -1;
            _hotbar = new Hotbar(_client, _loot, CharacterName);
            _screens = NewScreens();
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar, _screens) { Map = () => _map };
            _storyScreen = new StoryScreen(_client, SendToHost, () => _map) { OpenShop = tab => _inventory?.ShowShop(tab) };
            _joinSent = false;
            _net.Connected += id =>
            {
                if (id != _net.LocalClientId || _joinSent) return;
                _joinSent = true;
                SendToHost(_client.JoinMessage());
            };
            _net.Received += (sender, data) => _client?.Receive(data);
            _net.Disconnected += id =>
            {
                if (id == _net.LocalClientId || id == NetworkManager.ServerClientId) _endReason = LostHost + _net.DisconnectReason;
            };
            if (online != null && !online.StartClient())
            {
                _net = null;
                _client = null;
                _inventory = null;
                _hotbar = null;
                _status = "방에 접속하지 못했습니다.";
                return;
            }
            _mode = Mode.Join;
            _status = "접속 중…";
            PrepareLoading();
            _dropFocus = true;
            StartGuide();
        }

        string CharacterName => string.IsNullOrWhiteSpace(_playerName) ? "플레이어" : _playerName.Trim();

        string WorldName => string.IsNullOrWhiteSpace(_worldName) ? "내 월드" : _worldName.Trim();

        static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // The saved character for this name, checked here so a broken save stops the start with a reason (and is
        // never overwritten). Null data means a new character.
        bool LoadCharacter(out byte[] data)
        {
            data = SaveStore.Read(SaveStore.CharacterKey(CharacterName));
            if (data == null) return true;
            try
            {
                foreach (string warning in CharacterSave.Read(data, _loot).Warnings) _acquired.Add(("캐릭터 저장본: " + warning, 0));
                return true;
            }
            catch (InvalidDataException e)
            {
                _status = $"캐릭터 '{CharacterName}'의 저장본을 읽지 못했습니다: {e.Message} 다른 이름으로 새 캐릭터를 만드세요.";
                return false;
            }
        }

        // The character whenever the host sends a changed bag; the world every WorldSaveSeconds while hosting.
        void SaveProgress(float deltaTime)
        {
            // The character is kept when its gear or bag changes, and when it gains experience.
            if (_client.InventoryVersion != _savedInventoryVersion || (_client.Experience != _savedExperience && _client.InventoryVersion > 0)
                || (_client.PetNamesVersion != _savedPetNames && _client.InventoryVersion > 0)
                || (_client.KitVersion != _savedKit && _client.InventoryVersion > 0))
            {
                _savedKit = _client.KitVersion;
                _savedInventoryVersion = _client.InventoryVersion;
                _savedExperience = _client.Experience;
                _savedPetNames = _client.PetNamesVersion;
                Save(SaveStore.CharacterKey(CharacterName), _client.SaveCharacter(Now()));
            }
            _worldSaveClock += deltaTime;
            if (_worldSaveClock < WorldSaveSeconds) return;
            _worldSaveClock = 0;
            if (_map != null && _map.Changed) Save(MapKey, _map.Save());
            if (_host != null) Save(_worldKey, _host.SaveWorld(Now()));
        }

        // Where this character's map of this world is kept, by the world's seed so a joined world has one too.
        string MapKey => SaveStore.MapKey(_client.Seed, _client.Generator, CharacterName);

        void Save(string key, byte[] data)
        {
            // Nothing of the test ground is written: not the character played there, its map, nor the world.
            if (key == null || data == null || _testGround) return;
            try
            {
                SaveStore.Write(key, data);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Could not save {key}: {e.Message}");
            }
        }

        // Also called by the play mode tests.
        internal void Leave() => EndSession("");

        internal string PlayerName
        {
            get => _playerName;
            set => _playerName = value;
        }

        internal string WorldNameField
        {
            get => _worldName;
            set => _worldName = value;
        }

        void EndSession(string status)
        {
            if (_client != null) Save(SaveStore.CharacterKey(CharacterName), _client.SaveCharacter(Now()));
            if (_host != null) Save(_worldKey, _host.SaveWorld(Now()));
            // A start still making its world is let go (nothing was played, nothing to save).
            _loader = null;
            _starting = default;
            _heldForHost.Clear();
            if (_map != null && _client != null && _client.Status == ClientStatus.Playing) Save(MapKey, _map.Save());
            _worldKey = null;
            _savedInventoryVersion = 0;
            // The menu reads the character's classes again (they may have changed in the game).
            _classesFor = null;
            _savedExperience = 0;
            _view?.Dispose();
            _map?.Dispose();
            _net?.Dispose();
            _view = null;
            _map = null;
            _net = null;
            _roomCode = null;
            _host = null;
            _client = null;
            _inventory = null;
            _screens = new ScreenStack();
            _guideProgress = null;
            _dayPhase = null;
            _acquired.Clear();
            _mode = Mode.Menu;
            _status = status;
            _endReason = null;
            EndTestGround();
            BackToTitle();
        }

        // ---- Saving when the game is left without [나가기] (Docs/NativeBuild.md): a phone sends the app to the background
        // (home, a call, the screen off) and may close it there without a word, a PC's window can be closed, a browser's
        // tab hidden. The character, its map of the world and the world when hosting are written then, as the timed saves
        // do; the game goes on. Focus coming and going quickly (an alt-tab, the page's own text box on the web) writes at
        // most once every LeaveSaveGap seconds; quitting always writes. ----

        const float LeaveSaveGap = 5;
        float _leaveSavedAt = float.NegativeInfinity;

        internal int LeaveSaves { get; private set; }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveOnLeaving(false);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveOnLeaving(false);
        }

        void OnApplicationQuit() => SaveOnLeaving(true);

        // The web page's tab hidden or the page leaving (Assets/WebGLTemplates/Fit/index.html, through SendMessage): a
        // browser on a phone may drop a hidden tab without another word.
        void PageHidden() => SaveOnLeaving(false);

        internal void SaveOnLeaving(bool always)
        {
            if (_client == null || _client.Status != ClientStatus.Playing || _testGround) return;
            if (!always && Time.unscaledTime - _leaveSavedAt < LeaveSaveGap) return;
            _leaveSavedAt = Time.unscaledTime;
            LeaveSaves++;
            Save(SaveStore.CharacterKey(CharacterName), _client.SaveCharacter(Now()));
            if (_map != null && _map.Changed) Save(MapKey, _map.Save());
            if (_host != null) Save(_worldKey, _host.SaveWorld(Now()));
            DevInfo.Note("나가며 저장");
        }

        bool _ownCamera;

        void OnDestroy()
        {
            EndSession("");
            if (_ownCamera && _camera != null) Destroy(_camera.gameObject);
        }

        void OnGUI()
        {
            Ui.Begin();
            DevInfo.Read(Event.current);
            DrawScreen();
            Ui.End();
            DevInfo.Draw();
        }

        void DrawScreen()
        {
            // A screen opened or shut (or the menu's name field was left focused): IMGUI's keyboard focus goes, so a
            // pet's name field on a screen now gone does not keep taking keys.
            if (_dropFocus || _screens.Version != _screensSeen)
            {
                GUIUtility.keyboardControl = 0;
                _dropFocus = false;
                _screensSeen = _screens.Version;
            }
            // The settings opened in the game ([메뉴] → [설정], U7) are the start menu's page: back to the first page once shut.
            if (_screens.Current != GameScreen.Settings && (_page == MenuPage.Settings || _page == MenuPage.Keys) && _client != null) _page = MenuPage.Main;
            if (Loading)
            {
                DrawLoading();
                return;
            }
            if (_client == null)
            {
                DrawMenu();
                return;
            }
            bool dead = _client.Status == ClientStatus.Playing && _client.IsDead;
            if (dead && !_wasDown) _downSince = Time.time;
            _wasDown = dead;
            // Fallen (U7 쓰러짐 화면): the card is drawn over everything, a window left open included, and nothing under it
            // takes a press meant for its button (IMGUI gives a press to the first control drawn under it, so the press
            // is hidden from the rest while they draw).
            var card = dead ? DeathCardArea() : default;
            Ui.ProbeCover = card;
            var e = Event.current;
            var held = EventType.Ignore;
            if (dead && (e.isMouse || e.type == EventType.ScrollWheel) && card.Contains(e.mousePosition))
            {
                held = e.type;
                e.type = EventType.Ignore;
            }
            // A full screen (growth, records, settings) covers everything, and nothing else is drawn under it: the world
            // darkened 70% and red at the edges while the player is hurt or low, no HUD for its title to sit on, and no
            // button a tap meant for the screen could press instead (Docs/UiRework.md 4-5 전체 화면, 4-7 그리는 순서).
            // On a phone an encounter window is a sheet over everything too (U3): no HUD piece under it takes its taps.
            if (_screens.Covers && _client.Status == ClientStatus.Playing)
            {
                // A banner waits for the full screen to shut (U7: never under a window).
                _view?.HoldBanner();
                DrawFullScreen();
            }
            else DrawPlay(dead);
            if (held != EventType.Ignore) e.type = held;
            Ui.ProbeCover = Rect.zero;
            if (Event.current.type == EventType.Repaint) DeathShown = Rect.zero;
            if (dead) DrawDeath(card);
        }

        void DrawPlay(bool dead)
        {
            // The bag or an encounter window beside the world (Docs/UiRework.md U3): the HUD is its bars, no world button,
            // skill key or quick slot is drawn under it. On a low screen (a phone held sideways) the box is not drawn at
            // all while a window is open (4-5 가로 화면: the window takes the height), though its lines are still read.
            bool side = _screens.Current != GameScreen.None;
            var window = side ? ScreenStack.AreaOf(_screens.Current) : default;
            _view?.DrawGui();
            // Low on health: the screen's edges glow red, pulsing slowly unless motion is reduced (U7).
            if (!dead && _client.Status == ClientStatus.Playing && Feel.HurtGlow) UiSkin.RedEdges(Ui.FullScreen, UiMotion.LowHealthGlow(LowHealthShare));
            if (side && Ui.Short)
            {
                var color = GUI.color;
                GUI.color = Color.clear;
                DrawHud(compact: true);
                GUI.color = color;
            }
            else
            {
                DrawHud(compact: side);
            }
            // Banners in the room the windows, the hub and the two columns leave (U7: never over a window).
            var bannerWindow = _screens.Covers ? default : window;
            Rect? hubPanel = _screens.HubOpen ? ScreenStack.HubLayout().Panel : (Rect?)null;
            var bannerRoom = BannerRoom(bannerWindow, hubPanel);
            // A long banner (a level's gains) wraps over lines in a narrow room: the room again, as tall as that.
            float bannerTall = _view?.BannerHeight(bannerRoom.width) ?? 0;
            if (bannerTall > bannerRoom.height) bannerRoom = BannerRoom(bannerWindow, hubPanel, bannerTall);
            _view?.DrawBanner(bannerRoom);
            // The "획득: …" lines under every window (Docs/UiRework.md 4-7 그리는 순서): drawn after the bag, they ran
            // over it on a phone (phone-bag-gear).
            DrawAcquired(dead);
            // Not while the bag, a window or the hub is open: skills cannot be used then (ReadSkillPress), a tap in the
            // window must not fire one, and a slot would show in the gap beside the window. Nor under the waypoint list: a
            // button drawn before it would take a tap meant for one of its rows (2026-11-22, the web sweep on a phone).
            // While a talk is up its box sits where the skill keys and quick slots are, which are not drawn then
            // (Docs/UiRework.md 4-2; on a phone held upright the box lay over the quick slots, the U1 sweep). Nor while
            // fallen: the card's button is the one thing to press then (U7).
            if (_storyScreen != null)
            {
                _storyScreen.KeyHint = MeetHint(Meeting.Person);
                _storyScreen.MerchantKeyHint = MeetHint(Meeting.Merchant);
            }
            if (_client.Status == ClientStatus.Playing && !side && !_screens.HubOpen && !(_storyScreen?.ListOpen ?? false) && !dead)
            {
                DrawMachineSwitch();
                DrawDoorButtons();
                DrawPoiButton();
                DrawDungeonButtons();
                DrawBedButton();
                DrawRiftButton();
                if (!(_storyScreen?.TalkOpen ?? false)) DrawStationButtons();
                _storyScreen?.DrawWorldButtons(_camera);
                DrawDefenseButton();
                if (!(_storyScreen?.TalkOpen ?? false))
                {
                    SkillBar.Draw(_client);
                    _hotbar?.Draw(_client.Skills.Count);
                }
            }
            // A window closing shrinks away under the one opening.
            UiMotion.DrawGhosts();
            if (side) _inventory?.Draw();
            // The story's talk and the waypoint list, over the rest (Docs/Story.md).
            if (_client.Status == ClientStatus.Playing && !side) _storyScreen?.DrawPanels();
            // Not where a window lies over it (an encounter window on a small screen reaches the column).
            if (!side || !window.Overlaps(Ui.MinimapArea)) _map?.DrawMinimap(Ui.MinimapArea);
            // The test ground's badge and hit log, under the column's lines (not under a window or the hub).
            if (!side && !_screens.HubOpen) DrawTestOverlay();
            else if (Event.current.type == EventType.Repaint) TestGround.Hide();
            if (Controls.UsingTouch)
            {
                DrawTouchButtons();
                Controls.DrawTouch();
            }
            // On a PC the hub is the Esc menu (U7): the screens, [설정] and [나가기], in the middle of the screen.
            else if (_screens.HubOpen && _screens.DrawHub(HasSkillPoints, _client.PassivePoints > 0))
            {
                _endReason = "";
            }
        }

        // A full screen over the darkened world: the growth screens and the journal (InventoryWindow), the map, or the
        // settings.
        void DrawFullScreen()
        {
            var screen = Ui.FullScreen;
            Controls.Block(screen);
            if (_screens.Current == GameScreen.Map)
            {
                if (_map != null) _map.DrawFull();
                else _screens.Close();
                return;
            }
            if (_screens.Full) UiSkin.FullBackdrop(screen);
            UiMotion.DrawGhosts();
            // The settings in the game ([메뉴] → [설정], U7): the title's page over the darkened world; its [뒤로]
            // (or Esc) shuts it.
            if (_screens.Current == GameScreen.Settings)
            {
                DrawGameSettings();
                return;
            }
            // The test ground's panel (Docs/TestGround.md).
            if (_screens.Current == GameScreen.Test)
            {
                DrawTestPanel();
                return;
            }
            _inventory?.Draw();
        }

        bool HasSkillPoints => _client.SkillPoints > 0 || _client.Classes.Count < ClassRules.SlotsAt(_client.Level);

        // Where a banner may go: a quarter down the screen, across what a side window leaves, beside the HUD's column and
        // the minimap's (and the hub's panel) where it would meet them, or under them where too little is left beside.
        // No width when there is no room (a window across the middle): the banner then waits (WorldView.HoldBanner).
        internal static Rect BannerRoom(Rect window, Rect? hub, float tall = 64)
        {
            float left = 0, right = Ui.Width;
            if (window.width > 0)
            {
                // The wider side of the window.
                if (window.x - 6 >= Ui.Width - window.xMax - 6) right = window.x - 6;
                else left = window.xMax + 6;
                if (right - left < 200) return new Rect(left, Mathf.Round(Ui.Height * 0.25f), 0, 0);
            }
            float top = Mathf.Round(Ui.Height * 0.25f);
            var obstacles = new List<Rect>
            {
                Rect.MinMaxRect(Ui.HudArea.x, Ui.HudArea.y, Ui.HudArea.xMax, Mathf.Max(Ui.HudArea.yMax, Ui.LeftColumnBottom)),
                Rect.MinMaxRect(Ui.MinimapArea.x, Ui.MinimapArea.y, Ui.MinimapArea.xMax, Mathf.Max(Ui.RightColumnBottom, Ui.InfoBottom)),
            };
            if (hub is Rect panel) obstacles.Add(panel);
            // The test ground's hit log (Docs/TestGround.md).
            if (TestGround.Shown.width > 0) obstacles.Add(TestGround.Shown);
            // One that starts inside the banner's lower part (the [메뉴] panel on a low screen, under a banner over two or
            // three lines): the banner rises above it where the screen has the room, rather than run onto it.
            foreach (var o in obstacles)
                if (o.y > top && o.y < top + tall && o.xMax > left && o.x < right && o.y - 8 - tall >= 8) top = o.y - 8 - tall;
            float l = left, r = right;
            foreach (var o in obstacles)
            {
                if (top >= o.yMax || top + tall <= o.y || o.xMax <= l || o.x >= r) continue;
                if (o.center.x < (l + r) / 2) l = Mathf.Max(l, o.xMax + 8);
                else r = Mathf.Min(r, o.x - 8);
            }
            if (r - l >= Mathf.Min(260, right - left)) return new Rect(l, top, r - l, tall);
            // Too little beside them: under the lowest of them that reaches across.
            foreach (var o in obstacles)
                if (o.xMax > left && o.x < right) top = Mathf.Max(top, o.yMax + 8);
            return new Rect(left, Mathf.Min(top, Ui.Height - tall - 10), right - left, tall);
        }

        // ---- The fallen card (Docs/UiRework.md 4-1 쓰러짐, U7) ----

        // Where the card goes: in the lower part of the screen, clear of the player in the middle, a finger's button in it.
        internal static Rect DeathCardArea()
        {
            float width = Mathf.Min(380, Ui.Width - 24);
            const float height = 150;
            float y = Ui.Short ? Ui.Height - height - 10 : Mathf.Round(Ui.Height * 0.62f - height / 2);
            return new Rect(Mathf.Round((Ui.Width - width) / 2), Mathf.Clamp(y, 10, Ui.Height - height - 10), width, height);
        }

        // Where the card was drawn in the last repaint (zero when not), for the playtests.
        internal static Rect DeathShown { get; private set; }
        internal string DeathCause => _view?.DeathCause ?? "";

        static GUIStyle _deathTitle, _deathLine;

        // The screen darkened and red at its edges, and a card over everything: "쓰러졌다", what felled the player when
        // known, when and where they get up, and [집에서 일어나기] (in a dungeon [입구에서 일어나기]) to get up there now
        // (World.GetUp: alone or with others).
        void DrawDeath(Rect card)
        {
            var screen = Ui.FullScreen;
            Controls.Block(card);
            if (Event.current.type == EventType.Repaint)
            {
                var previous = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.4f * previous.a);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = previous;
                UiSkin.RedEdges(screen, 0.55f);
                DeathShown = card;
            }
            UiSkin.Card(card);
            _deathTitle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _deathLine ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            Ui.Text(_deathTitle, 24, bold: true);
            Ui.Text(_deathLine, 12);
            float x = card.x + 12, width = card.width - 24;
            var title = new Rect(x, card.y + 8, width, 32);
            var shade = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f * shade.a);
            GUI.Label(new Rect(title.x + 1, title.y + 2, title.width, title.height), "쓰러졌다", _deathTitle);
            GUI.color = new Color(1f, 0.42f, 0.36f, shade.a);
            GUI.Label(title, "쓰러졌다", _deathTitle);
            GUI.color = shade;
            bool together = _client.Names.Count > 1;
            bool dungeon = _client.DungeonHere != null;
            string cause = DeathCause;
            string when = together ? $"동료가 곁에 {World.ReviveSeconds:0}초 서 있으면 이 자리에서 일어난다 · {DownLeft()}초 뒤 {(dungeon ? "던전 입구" : "집")}에서"
                : dungeon ? "잠시 후 던전 입구에서 일어난다"
                : _client.LastCamp != null ? "잠시 후 마지막에 쉰 야영지에서 일어난다" : "잠시 후 집에서 일어난다";
            string words = (cause.Length > 0 ? $"{cause}에게 당했다\n" : "") + when;
            GUI.Label(new Rect(x, title.yMax + 2, width, card.yMax - 56 - title.yMax), Ui.Wrap(words, _deathLine, width), _deathLine);
            float button = Mathf.Min(220, width);
            var rise = new Rect(card.center.x - button / 2, card.yMax - 52, button, 44);
            if (Ui.Button(rise, dungeon ? "입구에서 일어나기" : "집에서 일어나기", Ui.Look.Primary))
            {
                Sounds.Play(Sound.Click);
                SendToHost(_client.GetUpCommand());
            }
        }

        // ---- Doors and cracked walls (U6: from the bag's [재료·제작] to buttons over them) ----

        // What G or the button over the door or wall ahead does now: lock the door, unlock one locked by the player,
        // mend a cracked wall (with its cost); null for none.
        string DoorWork()
        {
            if (_client == null || _client.Map == null || _client.Riding) return null;
            if (_client.DoorAhead() is (int x, int y))
            {
                if (_client.Map[x, y] == TileType.Door) return "문 잠그기";
                return _client.IsMyDoor(x, y) ? "잠금 풀기" : null;
            }
            if (_view?.CrackAhead() is TileType cracked) return $"고치기 ({(cracked == TileType.IronWall ? "철 주괴 1" : "돌 1")})";
            return null;
        }

        bool DoDoorWork()
        {
            string work = DoorWork();
            if (work == null) return false;
            if (work == "문 잠그기") SendToHost(_client.LockDoorCommand());
            else if (work == "잠금 풀기") SendToHost(_client.UnlockDoorCommand());
            else SendToHost(_client.RepairCommand());
            Sounds.Play(work == "문 잠그기" ? Sound.DoorShut : work == "잠금 풀기" ? Sound.Door : Sound.Place);
            return true;
        }

        // The button over the door or the cracked wall ahead ([문 잠그기]/[잠금 풀기]/[고치기 (…)], "(G)" on a PC).
        void DrawDoorButtons()
        {
            string work = DoorWork();
            if (work == null) return;
            var (x, y) = _client.DoorAhead() ?? World.TileAhead(_client.Position, _client.Facing);
            Vector3 point = _camera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 1.35f, 0));
            if (point.z < 0) return;
            string text = work + MeetHint(Meeting.Door);
            if (Ui.WorldButton(Ui.FromScreen(point), text, Mathf.Min(Ui.Width - 16, 40 + 12 * text.Length))) DoDoorWork();
        }

        // The "획득: …" lines in the bottom left, or above the skill buttons where they reach that far, newest at the
        // bottom. Each line is as tall as the font needs and a long one wraps between words, so none is cut off. They
        // stop under the goal list rather than run into it (a phone held sideways, phone-wide-small-hud-field), and a
        // line that would fall on the open bag is not drawn (phone-bag-gear): it shows again once the bag closes.
        void DrawAcquired(bool dead)
        {
            var label = GUI.skin.label;
            float width = Mathf.Min(400, Ui.Width - 28);
            // Clear of the skill keys and the quick slots, wherever they are.
            var slots = SkillBar.Bounds(_client.Skills.Count);
            var quick = Hotbar.Bounds(_client.Skills.Count);
            slots = Rect.MinMaxRect(Mathf.Min(slots.xMin, quick.xMin), Mathf.Min(slots.yMin, quick.yMin), Mathf.Max(slots.xMax, quick.xMax), slots.yMax);
            float y = slots.x < 14 + width ? slots.y - 6 : Ui.Height - 10;
            float floor = Ui.LeftColumnBottom + 4;
            Rect? bag = _screens.Current != GameScreen.None ? ScreenStack.AreaOf(_screens.Current) : (Rect?)null;
            var shown = Rect.zero;
            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                var (text, age) = _acquired[i];
                var content = new GUIContent(Ui.Wrap(text, label, width));
                float height = label.CalcHeight(content, width);
                y -= height;
                if (y < floor) break;
                var line = new Rect(14, y, width, height);
                if (bag is Rect window && line.Overlaps(window)) continue;
                // Nor over a banner or the fallen card (U7: the lines, banners and cards never on one another).
                if (line.Overlaps(WorldView.BannerShown) || dead && line.Overlaps(DeathCardArea())) continue;
                // Each on the HUD's panel (3a), fading with its line.
                float fade = Mathf.Clamp01((AcquiredSeconds - age) / 1f);
                var plate = GUI.color;
                GUI.color = new Color(1, 1, 1, fade * plate.a);
                float textWidth = Mathf.Min(width, label.CalcSize(new GUIContent(content.text)).x);
                UiSkin.HudPanel(new Rect(line.x - 6, line.y - 2, textWidth + 12, line.height + 4));
                GUI.color = plate;
                ShadowLabel(line, content.text, label, new Color(1, 1, 1, fade));
                shown = shown == Rect.zero ? line : Rect.MinMaxRect(Mathf.Min(shown.xMin, line.xMin), Mathf.Min(shown.yMin, line.yMin), Mathf.Max(shown.xMax, line.xMax), Mathf.Max(shown.yMax, line.yMax));
            }
            if (Event.current.type == EventType.Repaint) AcquiredArea = shown;
        }

        // Where the "획득" lines were drawn in the last repaint (zero when none), for the playtests.
        internal static Rect AcquiredArea { get; private set; }

        // On phones, under the minimap (Docs/UiRework.md 4-1 허브, 4-5 허브): [가방] for Tab, and [메뉴], the hub, a grid
        // of the screens' buttons (가방, 스킬, 특성, 캐릭터, 일지, 지도) and [나가기], which was beside [가방] before. A gold dot
        // on [메뉴] while there are skill or passive points to spend.
        void DrawTouchButtons()
        {
            var bag = Ui.TouchButtonArea(0, 2);
            var menu = Ui.TouchButtonArea(1, 2);
            Controls.Block(bag);
            Controls.Block(menu);
            if (Ui.Button(bag, "가방")) ToggleScreen(GameScreen.Bag);
            if (Ui.Button(menu, ScreenStack.HubButton, _screens.HubOpen ? Ui.Look.Primary : Ui.Look.Normal)) _screens.ToggleHub();
            bool skillPoints = HasSkillPoints, passivePoints = _client.PassivePoints > 0;
            if (skillPoints || passivePoints) InventoryWindow.Badge(menu);
            // Ends on the next Update, after this OnGUI has finished with the session.
            if (_screens.DrawHub(skillPoints, passivePoints)) _endReason = "";
        }

        // The status box in the top left corner (Core Keeper and Terraria keep theirs as lean): health, mana and the
        // level's experience as bars, and only the lines that matter now under them: down, asleep, going home, a meal
        // running, points to spend. The time, weather and place go under the minimap (DrawInfo) and the goals and
        // missions in a list under this box (DrawTracker); how to play is on the start menu. While the bag is open
        // only the bars show, so the bag can sit just under them. Lines that do not fit wrap between words.
        void DrawHud(bool compact)
        {
            // Each line with whether the box writes it: the buffs' lines are icons with their time (U7), but the
            // playtests read every line (_hudLines), as before.
            var all = new List<(string Text, bool Drawn)>();
            bool playing = _client.Status == ClientStatus.Playing;
            // The lines about someone being down: the only ones the box keeps in a boss fight whose bar goes under it.
            int urgent = 0;
            if (!playing)
            {
                all.Add((_status, true));
            }
            else
            {
                // In co-op a fallen player waits for an ally (World.Revive), and allies are told who is down.
                bool together = _client.Names.Count > 1;
                if (_client.IsDead && !together) all.Add((_client.LastCamp != null ? "쓰러졌습니다. 잠시 후 마지막에 쉰 야영지에서 일어납니다." : "쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.", true));
                if (_client.IsDead && together)
                    all.Add(($"쓰러졌다. 동료가 곁에 {World.ReviveSeconds:0}초 서 있으면 이 자리에서 일어난다 ({DownLeft()}초 뒤 집에서)", true));
                if (!_client.IsDead)
                {
                    foreach (var other in _client.Others())
                    {
                        if (other.Kind == EntityKind.Player && other.Health <= 0 && _client.Names.TryGetValue(other.Id, out var fallen))
                            all.Add(($"{fallen}이(가) 쓰러졌다: 곁에 {World.ReviveSeconds:0}초 서 있으면 일으킨다", true));
                    }
                }
                urgent = all.Count;
                if (!compact)
                {
                    if (_client.Sleeping)
                    {
                        int everyone = Math.Max(1, _client.Names.Count);
                        all.Add((everyone > 1 ? $"자는 중… 모두 누우면 아침이 된다 ({_client.Sleepers}/{everyone}명)" : "자는 중…", true));
                    }
                    if ((_client.SelfStatus & StatusBits.Recalling) != 0) all.Add(("귀환 중… 맞으면 끊긴다", true));
                    // Making something by hand (World.StartCrafting): what, how far, how many more.
                    if (_client.Crafting != null)
                        all.Add(($"만드는 중: {Whole(_client.Crafting.Name)} {_client.CraftProgress * 100:0}%" + (_client.CraftLeft > 1 ? $" (남은 {_client.CraftLeft}개)" : ""), true));
                    // A food's buff, a shrine's blessing and a camp's rest (World.Pois.cs): icons with their time (Buffs).
                    if (_client.Food != null)
                    {
                        int left = Mathf.CeilToInt(_client.FoodLeft);
                        all.Add(($"{Whole(_client.Food.Name)} {left / 60}:{left % 60:00}", false));
                    }
                    if (_client.Blessing != null)
                    {
                        int left = Mathf.CeilToInt(_client.BlessingSeconds);
                        all.Add(($"{Whole(_client.Blessing.Name)}의 축복 {left / 60}:{left % 60:00}", false));
                    }
                    if (_client.RestedSeconds > 0)
                    {
                        int left = Mathf.CeilToInt(_client.RestedSeconds);
                        all.Add(($"{Whole("잘 쉼")} {left / 60}:{left % 60:00}", false));
                    }
                    var points = new List<string>();
                    // An open class slot (Docs/Classes.md, 클래스 고르기): picked on the skills screen.
                    if (_client.Classes.Count < ClassRules.SlotsAt(_client.Level)) points.Add(Whole(_client.Classes.Count == 0 ? "클래스 고르기" : "클래스 하나 더"));
                    if (_client.SkillPoints > 0) points.Add(Whole($"스킬 포인트 {_client.SkillPoints}"));
                    if (_client.PassivePoints > 0) points.Add(Whole($"특성 포인트 {_client.PassivePoints}"));
                    // Where to spend them (U2): the skills and passives screens' keys, or the hub on a phone.
                    string where = Controls.UsingTouch ? "[메뉴]에서" : $"{KeyBindings.Name(KeyAction.Skills)}·{KeyBindings.Name(KeyAction.Passives)}";
                    if (points.Count > 0) all.Add(($"{string.Join(" · ", points)} ({where})", true));
                }
            }

            // In a boss fight on a narrow screen the box shows only the bars and those lines, so the boss bar under it
            // stays clear of the player (Docs/UiRework.md U0, phone-boss-bar); the playtests still read every line.
            var shownLines = all.Select(l => l.Text).ToList();
            bool squeezed = playing && !compact && _view?.ActiveBoss != null && !BossBarBesideHud;
            var lines = all.Where((l, i) => l.Drawn && (!squeezed || i < urgent)).Select(l => l.Text).ToList();
            var buffs = playing && !compact && !squeezed ? Buffs() : new List<Buff>();
            var box = GUI.skin.box;
            // Lines a point apart rather than the label's usual gap.
            _hudLine ??= new GUIStyle(GUI.skin.label)
            {
                margin = new RectOffset(GUI.skin.label.margin.left, GUI.skin.label.margin.right, 1, 1),
                padding = new RectOffset(GUI.skin.label.padding.left, GUI.skin.label.padding.right, 0, 0),
            };
            var label = _hudLine;
            float width = Ui.HudArea.width - box.padding.horizontal - label.margin.horizontal;
            for (int i = 0; i < lines.Count; i++) lines[i] = Ui.Wrap(lines[i], label, width);
            float gap = Mathf.Max(label.margin.top, label.margin.bottom);
            float height = box.padding.vertical + (lines.Count > 0 ? label.margin.top + label.margin.bottom + gap * (lines.Count - 1) : 0);
            foreach (string line in lines) height += label.CalcHeight(new GUIContent(line), width);
            if (playing) height += HealthBarHeight + ManaBarHeight + ExperienceBarHeight + 4;
            if (buffs.Count > 0) height += BuffSize + 6;
            Ui.HudHeight = Mathf.Ceil(height) + 2;

            UiSkin.HudPanel(Ui.HudArea);
            GUILayout.BeginArea(Ui.HudArea, UiSkin.PanelArea);
            if (playing)
            {
                // The bars with their marks (U7): a heart, a drop of mana, the level's star; the heart beats while low.
                float share = _client.MaxHealth > 0 ? _client.Health / _client.MaxHealth : 0;
                var bar = MarkedBar(width, HealthBarHeight, UiIcons.Heart, new Color(0.95f, 0.32f, 0.3f), share > 0 && share <= LowHealthShare ? UiMotion.LowHealthBeat() : 1);
                UiSkin.Bar(bar, share, $"생명력 {Mathf.CeilToInt(_client.Health)} / {Mathf.CeilToInt(_client.MaxHealth)}");
                GUILayout.Space(2);
                float manaShare = _client.MaxMana > 0 ? _client.Mana / _client.MaxMana : 0;
                var manaBar = MarkedBar(width, ManaBarHeight, GrowthArt.Drop, new Color(0.45f, 0.65f, 1f), 1);
                UiSkin.Bar(manaBar, manaShare, $"마나 {Mathf.FloorToInt(_client.Mana)} / {Mathf.CeilToInt(_client.MaxMana)}", mana: true, textSize: 12);
                GUILayout.Space(2);
                float progress = Leveling.Progress(_client.Experience);
                var experienceBar = MarkedBar(width, ExperienceBarHeight, GrowthArt.Point, new Color(1f, 0.82f, 0.35f), 1);
                UiSkin.Bar(experienceBar, progress, $"Lv {_client.Level} · {(int)(progress * 100)}%", textSize: 12, experience: true);
                if (buffs.Count > 0)
                {
                    GUILayout.Space(4);
                    var row = GUILayoutUtility.GetRect(width, BuffSize, GUILayout.ExpandWidth(true));
                    DrawBuffs(row, buffs);
                }
            }
            foreach (string line in lines) GUILayout.Label(line, label);
            GUILayout.EndArea();

            var shown = shownLines;
            if (playing) shown.Insert(0, $"Lv {_client.Level} ({(int)(Leveling.Progress(_client.Experience) * 100)}%)");
            if (playing && !compact) shown.AddRange(DrawInfo());
            if (playing && !compact) shown.AddRange(DrawTracker());
            else Ui.LeftColumnBottom = Ui.HudArea.yMax;
            if (Event.current.type == EventType.Repaint) _hudLines = shown.ToArray();

            // A boss in sight, still asleep: its first phase made ahead for when it wakes.
            if (_view?.BossInSight is string sleeper) Music.Prepare(Song.Of(sleeper, 1));
            if (Event.current.type == EventType.Repaint) BossBarShown = Rect.zero;
            if (_view?.ActiveBoss is EntityView boss) DrawBossBar(boss, _client.Level);
            else if (playing) DrawDungeonBossBar();
        }

        // Seconds until a fallen player gets up at home in co-op.
        int DownLeft() => Mathf.Max(0, Mathf.CeilToInt(World.DownSeconds - (Time.time - _downSince)));

        // A bar's row in the HUD with its mark at the left (the mark's brightness for a beat); gives the bar's rect.
        static Rect MarkedBar(float width, float height, string[] mark, Color color, float beat)
        {
            var row = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var size = UiSkin.IconSize(mark);
                var tint = new Color(color.r * beat, color.g * beat, color.b * beat, color.a);
                UiSkin.Icon(new Rect(row.x, row.y + (row.height - size.y) / 2, Mathf.Max(size.x, MarkWidth - 4), size.y), mark, tint);
            }
            return new Rect(row.x + MarkWidth, row.y, row.width - MarkWidth, row.height);
        }

        const float MarkWidth = 18;
        // Health at or under this share is low: the screen's edges glow red and the heart beats (U7).
        internal const float LowHealthShare = 0.3f;

        // ---- Buffs and ailments (U7): icons under the bars with their time ----

        internal const float BuffSize = 26;

        internal struct Buff
        {
            public string Name;
            public string Time;
            public string Tip;
            public string[] Glyph;
            public Sprite Icon;
            public Color Color;
            public bool Bad;
        }

        List<Buff> Buffs()
        {
            var buffs = new List<Buff>();
            static string Clock(float seconds)
            {
                int left = Mathf.CeilToInt(seconds);
                return $"{left / 60}:{left % 60:00}";
            }
            if (_client.Food != null)
                buffs.Add(new Buff { Name = _client.Food.Name, Time = Clock(_client.FoodLeft), Tip = _client.Food.Description, Icon = Sprites.Icon(_client.Food), Color = Color.white });
            if (_client.Blessing != null)
                buffs.Add(new Buff { Name = $"{_client.Blessing.Name}의 축복", Time = Clock(_client.BlessingSeconds), Tip = PoiText.BlessingLine(_client.Blessing), Glyph = MapIcons.Shrine, Color = new Color(1f, 0.85f, 0.4f) });
            if (_client.RestedSeconds > 0)
                buffs.Add(new Buff { Name = "잘 쉼", Time = Clock(_client.RestedSeconds), Tip = "야영지에서 쉬었다: 생명력이 더 빨리 찬다", Glyph = MapIcons.Camp, Color = new Color(1f, 0.6f, 0.3f) });
            byte status = _client.SelfStatus;
            if ((status & StatusBits.Burning) != 0) buffs.Add(new Buff { Name = "불붙음", Tip = "불이 붙었다: 잠시 피해를 입는다", Glyph = GrowthArt.Flame, Color = new Color(1f, 0.55f, 0.25f), Bad = true });
            if ((status & StatusBits.Chilled) != 0) buffs.Add(new Buff { Name = "얼어붙음", Tip = "차가워졌다: 잠시 느려진다", Glyph = GrowthArt.Snow, Color = new Color(0.65f, 0.9f, 1f), Bad = true });
            if ((status & StatusBits.Poisoned) != 0) buffs.Add(new Buff { Name = "독", Tip = "독에 걸렸다: 잠시 피해를 입는다", Glyph = GrowthArt.Drop, Color = new Color(0.55f, 0.95f, 0.4f), Bad = true });
            if ((status & StatusBits.Snared) != 0) buffs.Add(new Buff { Name = "거미줄", Tip = "거미줄에 걸렸다: 느려진다", Glyph = GrowthArt.Boot, Color = new Color(0.9f, 0.9f, 0.9f), Bad = true });
            if ((status & StatusBits.Rooted) != 0) buffs.Add(new Buff { Name = "묶임", Tip = "발이 묶였다: 움직이지 못한다", Glyph = UiIcons.Lock, Color = new Color(0.85f, 0.75f, 0.6f), Bad = true });
            return buffs;
        }

        // Where the buffs were drawn in the last repaint and their names, for the playtests.
        internal static Rect BuffsShown { get; private set; }
        internal static string BuffNames { get; private set; } = "";

        static GUIStyle _buffTime;

        // Each buff a small well with its icon (red rimmed for an ailment), its time under it in small letters; the mouse
        // over one (a finger held on it) shows a card with its name and what it does. Known to the playtests by its name
        // (row "버프").
        static void DrawBuffs(Rect row, List<Buff> buffs)
        {
            _buffTime ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, padding = new RectOffset(0, 1, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_buffTime, 12, bold: true);
            Ui.Row("버프");
            var e = Event.current;
            float x = row.x;
            var names = new List<string>();
            foreach (var buff in buffs)
            {
                float w = buff.Time != null ? Mathf.Max(BuffSize, _buffTime.CalcSize(new GUIContent(buff.Time)).x + 6) : BuffSize;
                var cell = new Rect(x, row.y, w, BuffSize);
                if (cell.xMax > row.xMax) break;
                x += w + 3;
                names.Add(buff.Name);
                Ui.Probe(buff.Name, cell);
                if (e.type == EventType.Repaint)
                {
                    UiSkin.SlotFrame(cell, buff.Bad ? new Color(0.9f, 0.3f, 0.25f) : (Color?)null);
                    var icon = new Rect(cell.x + (cell.width - 16) / 2, cell.y + 2, 16, 16);
                    if (buff.Icon != null) Sprites.DrawInGui(icon, buff.Icon);
                    else if (buff.Glyph != null)
                    {
                        var size = UiSkin.IconSize(buff.Glyph);
                        UiSkin.Icon(new Rect(cell.x + (cell.width - size.x) / 2, cell.y + 3, size.x, size.y), buff.Glyph, buff.Color);
                    }
                    if (buff.Time != null)
                    {
                        var previous = GUI.color;
                        GUI.color = new Color(0, 0, 0, 0.9f * previous.a);
                        GUI.Label(new Rect(cell.x + 1, cell.y + 1, cell.width - 2, cell.height - 1), buff.Time, _buffTime);
                        GUI.color = previous;
                        GUI.Label(new Rect(cell.x, cell.y, cell.width - 2, cell.height - 2), buff.Time, _buffTime);
                    }
                }
                bool over = cell.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
                if (over)
                {
                    string text = buff.Time != null ? $"{buff.Name} · {buff.Time}\n{buff.Tip}" : $"{buff.Name}\n{buff.Tip}";
                    var size = Ui.CardSize(text, 240, out string wrapped);
                    Ui.LateCard(new Rect(Mathf.Clamp(cell.x, 4, Mathf.Max(4, Ui.Width - size.x - 4)), cell.yMax + 4, size.x, size.y), wrapped);
                }
            }
            Ui.Row("");
            if (e.type == EventType.Repaint)
            {
                BuffsShown = new Rect(row.x, row.y, x - row.x, BuffSize);
                BuffNames = string.Join(",", names);
            }
        }

        // The time line's place in a dungeon: its name, level, how long the run has gone and how many rooms are cleared;
        // once done, its time (묶음 5b).
        string DungeonLine(DungeonInstance dungeon)
        {
            string line = $"{dungeon.Name} · 레벨 {dungeon.Level}";
            int packs = dungeon.Layout.Nodes.Count(n => DungeonSheet.HasPack(n.Letter));
            if (packs == 0) return line;
            int cleared = dungeon.Layout.Nodes.Count(n => DungeonSheet.HasPack(n.Letter) && dungeon.RoomCleared(n.Index));
            int seconds = dungeon.State == DungeonState.Done ? dungeon.ClearSeconds
                : dungeon.Started < 0 ? 0 : (int)Math.Max(0, (_client.HostTick - dungeon.Started) / World.TicksPerSecond);
            return $"{line} · {seconds / 60}:{seconds % 60:00} · 방 {cleared}/{packs}" + (dungeon.State == DungeonState.Done ? " · 완주" : "");
        }

        GUIStyle _smallLine;

        // Small lines that read over any ground: a dark shadow under each.
        GUIStyle SmallLine()
        {
            _smallLine ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_smallLine, 12);
            return _smallLine;
        }

        void ShadowLabel(Rect area, string text, GUIStyle style, Color color)
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f * color.a);
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, style);
            GUI.color = color;
            GUI.Label(area, text, style);
            GUI.color = previous;
        }

        // The day and hour, the weather and the day's event, where the player is, and an online room's code: small
        // lines under the minimap (and the touch buttons), right-aligned to it.
        List<string> DrawInfo()
        {
            long clock = _client.Clock;
            int today = Calendar.Day(clock);
            var lines = new List<string> { $"{Calendar.Text(clock)} {Calendar.PhaseName(Calendar.Phase(clock))}" };
            int placeLine = -1;
            var weather = World.WeatherOf(today);
            string place = BiomeMap.Name(_client.Biomes.At(_client.Position));
            // In a dungeon its name and level, and no weather down there (묶음 5a).
            if (_client.DungeonHere is DungeonInstance dungeon) lines.Add(DungeonLine(dungeon));
            else
            {
                // The levels it is for beside its name, orange or red when the player is short of them (Docs/ZoneLevels.md).
                place = $"{place} · {Zones.Text(RangeHere(_client.Position))}";
                lines.Add(weather != Weather.Clear ? $"{World.WeatherName(weather)} · {place}" : place);
                placeLine = lines.Count - 1;
            }
            if (World.EventOf(today) != DayEvent.None) lines.Add(World.EventName(World.EventOf(today)));
            if (_mode != Mode.Solo)
            {
                string room = _mode == Mode.Host ? (_roomCode != null ? $"방 코드 {_roomCode}" : $"호스트 · 포트 {NetLink.DefaultPort}") : "참가 중";
                // A room hosted on the web stops while its tab is hidden (Docs/CrossPlay.md).
                if (_mode == Mode.Host && Application.platform == RuntimePlatform.WebGLPlayer) lines.Add(WebHostWarning);
                lines.Add($"{room} · {_client.Names.Count}명");
            }

            var style = SmallLine();
            style.alignment = TextAnchor.UpperRight;
            var area = Ui.InfoArea(0);
            float y = area.y;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                float height = style.CalcHeight(new GUIContent(line), area.width);
                ShadowLabel(new Rect(area.x, y, area.width, height), line, style, i == placeLine ? DangerColor(Zones.Danger(_client.Level, RangeHere(_client.Position))) : new Color(0.92f, 0.9f, 0.85f));
                y += height;
            }
            Ui.InfoBottom = y;
            return lines;
        }

        // What the player is doing: a rift or a defense under way, and the guide's next goal with how far along it is
        // and what it gives. Under the status box, in a box of its own, and gone while the bag is open.
        List<string> DrawTracker()
        {
            var entries = new List<(string Title, string Text)>();
            if (_client.Rift.Open)
            {
                var rift = _client.Rift;
                string wave = rift.Wave > World.RiftWaves ? "수호자" : $"{rift.Wave}/{World.RiftWaves} 물결";
                entries.Add(($"공허 균열 {rift.Tier}단계", $"{wave} · 남은 적 {rift.MonstersLeft} · {rift.SecondsLeft / 60}:{rift.SecondsLeft % 60:00}"
                    + (Ui.Short ? "" : $"\n{World.AffixNames(rift.Affixes)}")));
            }
            if (_client.Defense.Active)
            {
                var defense = _client.Defense;
                entries.Add(($"기지 방어 {defense.Level}단계", $"{defense.Wave}/{World.DefenseWaves} 물결 · 남은 적 {defense.MonstersLeft} · 수호석 {defense.StonePercent}%"));
            }
            // The story first (Docs/Story.md): where it leads and which way, then the side stories taken.
            int storyAt = entries.Count;
            // What the journal or the map was asked to follow (Docs/UiRework.md U5 추적, ScreenStack.Tracked): a spot on the
            // map first, which way and how far; a side story right after the main story's; a person's story or request
            // before the other people's. It is marked with a flag beside its title.
            string tracked = _screens.Tracked ?? "";
            int trackedAt = -1;
            if (tracked == "pin")
            {
                trackedAt = entries.Count;
                entries.Add(($"길 안내: {_screens.PinName}", PinWay()));
                storyAt = entries.Count;
            }
            if (_storyScreen != null)
            {
                entries.AddRange(_storyScreen.TrackerEntries(tracked.StartsWith("side:") ? tracked.Substring(5) : null, out int side));
                if (side >= 0) trackedAt = storyAt + side;
            }
            var goal = Settings.Guide ? _guideProgress?.Current : null;
            // One next thing, not two (Docs/Guide.md 하나의 다음 목표, 2026-11-11, the research report's candidate 4): when
            // the guide's goal is the boss the story's step asks for, the story's entry says it and carries the guide's
            // count and reward, and the guide's own entry goes.
            if (goal != null && entries.Count > storyAt && SameAsStory(goal))
            {
                var (title, text) = entries[storyAt];
                string progress = _goalProgress != null ? $" ({_goalProgress})" : "";
                entries[storyAt] = ($"{title} · 목표 {_guideProgress.Done + 1}/{_guideProgress.Total}",
                    text + progress + (goal.Rewards.Count > 0 && !Ui.Short ? $"\n보상: {goal.RewardText}" : ""));
                goal = null;
            }
            if (goal != null)
            {
                string progress = _goalProgress != null ? $" ({_goalProgress})" : "";
                string text = goal.Text + progress;
                if (goal.Rewards.Count > 0 && !Ui.Short) text += $"\n보상: {goal.RewardText}";
                // With rumours (묶음 4c) the hint as hearsay: no marker to walk to.
                if (!Ui.Short && Time.time - _goalShownAt < GoalHintSeconds) text += $"\n{(Settings.Rumors ? goal.Rumor : goal.Hint)}";
                entries.Add(($"목표 {_guideProgress.Done + 1}/{_guideProgress.Total}", text));
            }
            // The next story step of each person met whose hearts allow it (Docs/Npcs.md, 이야기), then today's
            // requests not yet done, with how much of each thing the bag holds. Two of these at most, one on a short
            // screen.
            int people = Ui.Short ? 1 : 2;
            string Needs(RequestDefinition asked) => string.Join(" · ", asked.Need.Select(n =>
            {
                int have = n.Thing is MaterialDefinition m ? _client.Bag.CountOfKind(m) : _client.Bag.CountOf((ConsumableDefinition)n.Thing);
                return $"{RequestDefinition.NameOf(n.Thing)} {Math.Min(have, n.Count)}/{n.Count}";
            }));
            var everyone = (_client.Npcs?.Npcs ?? Enumerable.Empty<NpcDefinition>()).OrderBy(n => tracked == "story:" + n.Id || tracked == "request:" + n.Id ? 0 : 1).ToList();
            foreach (var npc in everyone)
            {
                bool follow = tracked == "story:" + npc.Id;
                if (!follow && entries.Count(e => e.Title.StartsWith("이야기") || e.Title.StartsWith("부탁")) >= people) continue;
                if (!_client.Friendships.ContainsKey(npc.Id)) continue;
                var step = _client.StoryStepOf(npc);
                if (step == null || _client.HeartsWith(npc) < step.Hearts) continue;
                if (follow) trackedAt = entries.Count;
                entries.Add(($"이야기: {npc.Name} — {step.Title}", Needs(step) + (Ui.Short ? "" : $"\n보답: {step.RewardText}")));
            }
            foreach (var npc in everyone)
            {
                bool follow = tracked == "request:" + npc.Id;
                if (!follow && entries.Count(e => e.Title.StartsWith("이야기") || e.Title.StartsWith("부탁")) >= people) continue;
                if (!_client.Friendships.ContainsKey(npc.Id) || _client.RequestDoneToday(npc)) continue;
                var request = _client.RequestOf(npc);
                if (request == null) continue;
                if (follow) trackedAt = entries.Count;
                var needs = request.Need.Select(n =>
                {
                    int have = n.Thing is MaterialDefinition m ? _client.Bag.CountOfKind(m) : _client.Bag.CountOf((ConsumableDefinition)n.Thing);
                    string name = n.Thing is MaterialDefinition material ? material.Name : ((ConsumableDefinition)n.Thing).Name;
                    return $"{name} {Math.Min(have, n.Count)}/{n.Count}";
                });
                entries.Add(($"부탁: {npc.Name}", string.Join(" · ", needs) + (Ui.Short ? "" : $"\n보답: {request.RewardText}")));
            }
            var shown = new List<string>();
            if (entries.Count == 0)
            {
                Ui.LeftColumnBottom = Ui.HudArea.yMax;
                return shown;
            }

            var box = GUI.skin.box;
            var style = SmallLine();
            style.alignment = TextAnchor.UpperLeft;
            style.wordWrap = true;
            _trackerTitle ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            Ui.Text(_trackerTitle, 12, bold: true);
            var area = Ui.HudArea;
            float width = area.width - box.padding.horizontal;
            foreach (var (title, raw) in entries) shown.Add($"{title}: {raw.Replace('\n', ' ')}");
            var previous = GUI.color;
            // Folded to one line on a phone held upright, in a boss fight that needs the room, and when folded by hand
            // (TrackerFolds): the followed entry if any (U5 추적), else the first, "+N" for the rest and the journal's book;
            // a tap opens the journal's goals. The lines above still carry every entry for the playtests.
            bool canFold = !TrackerFoldsAnyway(_view?.ActiveBoss != null);
            float toggle = Controls.UsingTouch ? Ui.TouchButtonHeight : 20;
            if (TrackerFolds(_view?.ActiveBoss != null))
            {
                var (firstTitle, _) = entries[trackedAt >= 0 ? trackedAt : 0];
                float lineHeight = _trackerTitle.CalcHeight(new GUIContent("가"), width);
                var folded = FoldedTrackerArea(lineHeight + box.padding.vertical - 4);
                UiSkin.HudPanel(folded);
                // From the right: [목표 펴기] (when folded by hand), the book, "+N", then the title cut to what is left.
                float right = folded.xMax - box.padding.right;
                if (canFold)
                {
                    var unfold = new Rect(folded.xMax - toggle - 2, folded.y + (folded.height - Mathf.Min(toggle, folded.height)) / 2, toggle, Mathf.Min(toggle, folded.height));
                    Controls.Block(unfold);
                    if (FoldButton(unfold, "목표 펴기", folds: false)) TrackerFoldedByHand = false;
                    right = unfold.x - 2;
                }
                var book = UiSkin.IconSize(UiIcons.Book);
                UiSkin.Icon(new Rect(right - book.x, folded.y + (folded.height - book.y) / 2, book.x, book.y), UiIcons.Book, new Color(0.95f, 0.85f, 0.6f));
                right -= book.x + 8;
                _trackerTitle.wordWrap = false;
                _trackerTitle.clipping = TextClipping.Clip;
                if (entries.Count > 1)
                {
                    string more = $"+{entries.Count - 1}";
                    float moreWidth = _trackerTitle.CalcSize(new GUIContent(more)).x;
                    ShadowLabel(new Rect(right - moreWidth, folded.y + (folded.height - lineHeight) / 2, moreWidth + 2, lineHeight), more, _trackerTitle, new Color(0.9f, 0.88f, 0.8f));
                    right -= moreWidth + 8;
                }
                float left = folded.x + box.padding.left;
                if (trackedAt >= 0)
                {
                    var flag = UiSkin.IconSize(MapIcons.Pin);
                    UiSkin.Icon(new Rect(left, folded.y + (folded.height - flag.y) / 2, flag.x, flag.y), MapIcons.Pin, new Color(0.9f, 0.3f, 0.22f));
                    left += flag.x + 4;
                }
                ShadowLabel(new Rect(left, folded.y + (folded.height - lineHeight) / 2, Mathf.Max(0, right - left), lineHeight), Shortened(firstTitle, _trackerTitle, right - left), _trackerTitle, new Color(1f, 0.85f, 0.4f));
                _trackerTitle.wordWrap = true;
                var tap = canFold ? Rect.MinMaxRect(folded.x, folded.y, folded.xMax - toggle - 4, folded.yMax) : folded;
                Ui.Probe("일지 열기", tap);
                Controls.Block(folded);
                if (GUI.Button(tap, GUIContent.none, GUIStyle.none)) _inventory.ShowJournal(InventoryWindow.JournalSection.Goals);
                Ui.LeftColumnBottom = folded.yMax;
                return shown;
            }
            // Unfolded (3a, 2026-11-27, Docs/HudLook.md): the start screen's see-through gold-rimmed panel; each entry an icon
            // by its kind, its title in gold (a chapter's with a gold rule under it), its text, a bar where it counts
            // something (the first "n/m"), and what it gives on a gold line with a chest. [목표 접기] in the top right
            // corner (not on a phone held upright, where it is always one line), the first entry kept clear of it.
            var laid = new List<(string Title, string Text, string Reward, float TitleHeight, float TextHeight, float RewardHeight, float Width, float Progress, string[] Icon)>();
            // As wide as the widest entry icon and a gap, so no icon covers the start of its title.
            float iconRoom = new[] { UiIcons.Book, MapIcons.Place, MapIcons.Chest, MapIcons.Lead }.Max(i => UiSkin.IconSize(i).x) + 5;
            float chestRoom = UiSkin.IconSize(MapIcons.Chest).x + 4;
            float height = box.padding.vertical;
            for (int i = 0; i < entries.Count; i++)
            {
                var (title, raw) = entries[i];
                float room = (i == 0 && canFold ? width - toggle - 2 : width) - iconRoom;
                var (body, reward) = SplitReward(raw);
                string text = string.Join("\n", body.Split('\n').Select(part => Ui.Wrap(part, style, room)));
                string rewardText = reward == null ? null : Ui.Wrap(reward, style, room - chestRoom);
                float titleHeight = _trackerTitle.CalcHeight(new GUIContent(title), room) + (IsChapter(title) ? 5 : 0);
                float textHeight = text.Length > 0 ? style.CalcHeight(new GUIContent(text), room) : 0;
                float rewardHeight = rewardText != null ? style.CalcHeight(new GUIContent(rewardText), room - chestRoom) : 0;
                float progress = CountIn(body);
                laid.Add((title, text, rewardText, titleHeight, textHeight, rewardHeight, room, progress, EntryIcon(title)));
                height += titleHeight + textHeight + rewardHeight + (progress >= 0 ? TrackerBarHeight + 3 : 0) + 6;
            }
            if (canFold && Controls.UsingTouch) height = Mathf.Max(height, toggle + 4);
            var frame = new Rect(area.x, area.yMax + 6, area.width, Mathf.Ceil(height));
            UiSkin.HudPanel(frame);
            if (canFold)
            {
                var fold = new Rect(frame.xMax - toggle - 2, frame.y + 2, toggle, toggle);
                Controls.Block(fold);
                if (FoldButton(fold, "목표 접기", folds: true)) TrackerFoldedByHand = true;
            }
            float y = frame.y + box.padding.top;
            float column = frame.x + box.padding.left;
            for (int i = 0; i < laid.Count; i++)
            {
                var (title, text, reward, titleHeight, textHeight, rewardHeight, room, progress, icon) = laid[i];
                var iconSize = UiSkin.IconSize(icon);
                UiSkin.Icon(new Rect(column + (iconRoom - 5 - iconSize.x) / 2, y + 2, iconSize.x, iconSize.y), icon, new Color(1f, 0.85f, 0.45f));
                float x = column + iconRoom;
                ShadowLabel(new Rect(x, y, room, titleHeight), title, _trackerTitle, new Color(1f, 0.85f, 0.4f));
                if (IsChapter(title)) UiSkin.Divider(new Rect(x, y + titleHeight - 5, room, 4));
                if (i == trackedAt)
                {
                    var flag = UiSkin.IconSize(MapIcons.Pin);
                    UiSkin.Icon(new Rect(x + room - flag.x + 2, y + 1, flag.x, flag.y), MapIcons.Pin, new Color(0.9f, 0.3f, 0.22f));
                }
                y += titleHeight;
                if (textHeight > 0) ShadowLabel(new Rect(x, y, room, textHeight), text, style, Color.white);
                y += textHeight;
                if (progress >= 0)
                {
                    TrackerBar(new Rect(x, y + 2, room, TrackerBarHeight), progress);
                    y += TrackerBarHeight + 3;
                }
                if (reward != null)
                {
                    var chest = UiSkin.IconSize(MapIcons.Chest);
                    UiSkin.Icon(new Rect(x, y + 2, chest.x, chest.y), MapIcons.Chest, new Color(1f, 0.82f, 0.35f));
                    ShadowLabel(new Rect(x + chestRoom, y, room - chestRoom, rewardHeight), reward, style, new Color(1f, 0.86f, 0.5f));
                    y += rewardHeight;
                }
                y += 6;
            }
            Ui.LeftColumnBottom = frame.yMax;
            return shown;
        }

        // ---- The goal list's look (3a, Docs/HudLook.md) ----

        const float TrackerBarHeight = 5;

        // A story chapter's entry ("1장 · …").
        static bool IsChapter(string title) => title.Length > 1 && char.IsDigit(title[0]) && title.Contains("장");

        static string[] EntryIcon(string title) =>
            IsChapter(title) ? UiIcons.Book
            : title.StartsWith("이야기") ? MapIcons.Place
            : title.StartsWith("부탁") ? MapIcons.Chest
            : MapIcons.Lead;

        // The text and, apart, what it gives ("보상: …", "보답: …"), or null.
        static (string Body, string Reward) SplitReward(string raw)
        {
            var lines = raw.Split('\n').ToList();
            int at = lines.FindIndex(l => l.StartsWith("보상:") || l.StartsWith("보답:"));
            if (at < 0) return (raw, null);
            string reward = lines[at].Substring(3).Trim();
            lines.RemoveAt(at);
            return (string.Join("\n", lines), reward);
        }

        static readonly System.Text.RegularExpressions.Regex Count = new System.Text.RegularExpressions.Regex(@"(\d+)\s*/\s*(\d+)");

        // The share done of the first "n/m" in the text, or -1 when it counts nothing.
        internal static float CountIn(string text)
        {
            var match = Count.Match(text ?? "");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int done) || !int.TryParse(match.Groups[2].Value, out int of) || of <= 0) return -1;
            return Mathf.Clamp01(done / (float)of);
        }

        // A thin gold bar in a dark well with a gold rim, green when full.
        static void TrackerBar(Rect rect, float share)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(0.62f, 0.48f, 0.22f, previous.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.06f, 0.1f, previous.a);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Texture2D.whiteTexture);
            GUI.color = share >= 1 ? new Color(0.5f, 0.82f, 0.42f, previous.a) : new Color(0.95f, 0.76f, 0.32f, previous.a);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * share, rect.height - 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // The goal list folded by hand ([목표 접기], U7): this device's choice for this game, not saved.
        internal static bool TrackerFoldedByHand { get; set; }

        // A small chevron button that folds or unfolds the goal list, known to the playtests by its name.
        static bool FoldButton(Rect rect, string name, bool folds)
        {
            Ui.Probe(name, rect);
            bool hover = rect.Contains(Event.current.mousePosition) && !Controls.UsingTouch;
            if (Event.current.type == EventType.Repaint)
            {
                var glyph = folds ? FoldUp : FoldDown;
                var size = UiSkin.IconSize(glyph);
                UiSkin.Icon(new Rect(rect.center.x - size.x / 2, rect.center.y - size.y / 2, size.x, size.y), glyph, hover ? UiSkin.Gold : new Color(0.9f, 0.8f, 0.6f));
            }
            bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (pressed) UiMotion.Confirm();
            return pressed;
        }

        static readonly string[] FoldUp = { "...#...", "..#o#..", ".#ooo#.", "#oo#oo#", "##...##" };
        static readonly string[] FoldDown = { "##...##", "#oo#oo#", ".#ooo#.", "..#o#..", "...#..." };

        // The text cut with "…" to fit the width in the style.
        static string Shortened(string text, GUIStyle style, float width)
        {
            if (width <= 0) return "";
            if (style.CalcSize(new GUIContent(text)).x <= width) return text;
            for (int length = text.Length - 1; length > 0; length--)
            {
                string cut = text.Substring(0, length).TrimEnd() + "…";
                if (style.CalcSize(new GUIContent(cut)).x <= width) return cut;
            }
            return "…";
        }

        // Where the folded goal list goes under the HUD box: one line, a finger tall on a touch screen.
        internal static Rect FoldedTrackerArea(float lineHeight)
        {
            var area = Ui.HudArea;
            return new Rect(area.x, area.yMax + 6, area.width, Mathf.Ceil(Controls.UsingTouch ? Mathf.Max(Ui.TouchButtonHeight, lineHeight) : lineHeight));
        }

        GUIStyle _trackerTitle;

        // Which way and how far the spot picked on the map lies ([여기로 길 안내], U5); "도착" once there.
        string PinWay()
        {
            var from = _client.WorldPosition;
            var pin = _screens.PinAt;
            if (Vec2.Distance(from, pin) < 3) return "도착";
            var here = _client.Biomes[TileMap.Cell(from.X), TileMap.Cell(from.Y)];
            return Rumors.Way(WayKnown.Known, from, pin, here, here);
        }

        GUIStyle _hudLine;

        // Spaces no line breaks at, so a name is never split over two lines of the HUD.
        static string Whole(string text) => text.Replace(' ', '\u00A0');

        // Whether the goal list folds to one line (Docs/UiRework.md 4-5 10): on a phone held upright, where it covered
        // 40% of the top, and in a boss fight whose bar has to go under it, where it pushed the bar down over the player
        // (phone-boss-bar).
        // Folded by hand too ([목표 접기], U7), where it is not folded anyway.
        internal static bool TrackerFolds(bool boss) => TrackerFoldsAnyway(boss) || TrackerFoldedByHand;

        internal static bool TrackerFoldsAnyway(bool boss) => Ui.Width < 600 && Ui.Height > Ui.Width || boss && !BossBarBesideHud;

        // Whether the boss bar fits between the HUD box and the minimap; else it goes under the left column.
        internal static bool BossBarBesideHud => Ui.MinimapArea.x - 10 - (Ui.HudArea.xMax + 10) >= 200;

        // How tall the HUD box is with its bars alone (and one line under them), for the layout tests.
        internal static float HudBarsHeight => HealthBarHeight + ManaBarHeight + ExperienceBarHeight + 4 + 22 + 20;

        // Where a boss's bar goes (its name line and the bar under it): between the HUD box and the minimap; under the
        // HUD and the (folded) goal list when the screen is too narrow for that.
        internal static Rect BossBarArea(float titleHeight)
        {
            float left = Ui.HudArea.xMax + 10;
            float right = Ui.MinimapArea.x - 10;
            float top = 20;
            if (!BossBarBesideHud)
            {
                left = 20;
                top = Ui.LeftColumnBottom + 10;
            }
            float width = Mathf.Min(600, right - left);
            return new Rect(left + (right - left - width) / 2, top, width, titleHeight + 18);
        }

        // Where the boss bar was drawn in the last repaint (zero when none), for the playtests.
        internal static Rect BossBarShown { get; private set; }

        static GUIStyle _bossTitle;

        // The boss's bar (U7 보스 막대 장식): its name line over a dark strip with a skull at the left and a pip for each
        // phase at the right (passed ones dark red, the one now bright), the bar in a framed well under it.
        static void DrawBossBar(EntityView boss, int level)
        {
            var place = BossBarArea(0);
            float width = place.width, x = place.x, top = place.y;
            // The name line as tall as the font needs, so it is not cut off, and the bar under it.
            string advice = boss.Boss.Level > level ? $" · 권장 Lv {boss.Boss.Level}" : "";
            var title = new GUIContent($"{boss.Boss.Name} · {boss.Phase}/{boss.Boss.PhaseCount}페이즈{advice}");
            _bossTitle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Ui.Text(_bossTitle, 12, bold: true);
            int phases = Mathf.Max(1, boss.Boss.PhaseCount);
            const float pip = 8;
            float pips = phases * (pip + 3);
            var skull = UiSkin.IconSize(MapIcons.Skull);
            float room = Mathf.Max(40, width - skull.x - 8 - pips - 8);
            float titleHeight = Mathf.Max(20, _bossTitle.CalcHeight(title, room));
            var line = new Rect(x, top, width, titleHeight);
            if (Event.current.type == EventType.Repaint)
            {
                var previous = GUI.color;
                GUI.color = new Color(0.05f, 0.02f, 0.02f, 0.6f * previous.a);
                GUI.DrawTexture(line, Texture2D.whiteTexture);
                GUI.color = previous;
                UiSkin.Icon(new Rect(x + 4, top + (titleHeight - skull.y) / 2, skull.x, skull.y), MapIcons.Skull, new Color(0.92f, 0.85f, 0.75f));
                for (int i = 0; i < phases; i++)
                {
                    var at = new Rect(line.xMax - 4 - pips + i * (pip + 3), top + (titleHeight - pip) / 2, pip, pip);
                    GUI.color = new Color(0, 0, 0, 0.8f * previous.a);
                    GUI.DrawTexture(at, Texture2D.whiteTexture);
                    GUI.color = i + 1 < boss.Phase ? new Color(0.45f, 0.12f, 0.1f, previous.a) : i + 1 == boss.Phase ? new Color(1f, 0.4f, 0.3f, previous.a) : new Color(0.35f, 0.3f, 0.28f, previous.a);
                    GUI.DrawTexture(new Rect(at.x + 1, at.y + 1, at.width - 2, at.height - 2), Texture2D.whiteTexture);
                }
                GUI.color = previous;
            }
            var words = new Rect(x + skull.x + 8, top, room, titleHeight);
            var shade = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f * shade.a);
            GUI.Label(new Rect(words.x + 1, words.y + 1, words.width, words.height), title, _bossTitle);
            GUI.color = new Color(1f, 0.88f, 0.8f, shade.a);
            GUI.Label(words, title, _bossTitle);
            GUI.color = shade;
            float share = boss.MaxHealth > 0 ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0;
            UiSkin.Bar(new Rect(x, top + titleHeight, width, 18), share, "", boss: true);
            if (Event.current.type == EventType.Repaint) BossBarShown = new Rect(x, top, width, titleHeight + 18);
            DrawFinisherName(boss, new Rect(x, top + titleHeight + 18 + 4, width, 0));
        }

        // Where a finisher's name was drawn in the last repaint (zero when none), and the name, for the playtests.
        internal static Rect FinisherShown { get; private set; }
        internal static string FinisherName { get; private set; }

        static GUIStyle _finisher;

        // A finisher (warn strong) is named while it winds up, under the boss bar, like a spell card (2026-11-28,
        // Docs/BossRedesign.md 4.2): 「오의 천섬」 in gold on a dark band, beating with the warning sign.
        static void DrawFinisherName(EntityView boss, Rect under)
        {
            bool on = boss.BossState == BossState.Telegraph && boss.BossAttack != null && boss.BossAttack.Warning == AttackWarning.Strong;
            if (Event.current.type == EventType.Repaint)
            {
                FinisherShown = Rect.zero;
                FinisherName = on ? boss.BossAttack.Name : null;
            }
            if (!on) return;
            _finisher ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Ui.Text(_finisher, 15, bold: true);
            var words = new GUIContent($"「{boss.BossAttack.Name}」");
            float width = Mathf.Min(under.width, _finisher.CalcSize(words).x + 40);
            float height = Mathf.Max(26, _finisher.CalcHeight(words, width));
            var band = new Rect(under.x + (under.width - width) / 2, under.y, width, height);
            float beat = Mathf.Abs(Mathf.Sin(Time.time * 9));
            var previous = GUI.color;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(0.12f, 0.02f, 0.02f, 0.75f * previous.a);
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.35f, 0.2f, (0.5f + 0.5f * beat) * previous.a);
                GUI.DrawTexture(new Rect(band.x, band.yMax - 2, band.width, 2), Texture2D.whiteTexture);
                FinisherShown = band;
            }
            GUI.color = new Color(0, 0, 0, 0.9f * previous.a);
            GUI.Label(new Rect(band.x + 1, band.y + 1, band.width, band.height), words, _finisher);
            GUI.color = new Color(1f, 0.82f + 0.12f * beat, 0.45f, previous.a);
            GUI.Label(band, words, _finisher);
            GUI.color = previous;
        }
    }
}
