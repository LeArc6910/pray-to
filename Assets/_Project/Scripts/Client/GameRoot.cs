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
    public sealed class GameRoot : MonoBehaviour
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
        // [월드 지우기] asks once more before it deletes.
        bool _confirmDelete;
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
        internal int MarksShown => _view?.MarksShown ?? 0;
        // Skill effects running and their pieces on screen (WorldView.SkillFx), for the playtests.
        internal int SkillFxAlive => _view?.SkillFxAlive ?? 0;
        // Rolls begun on the screen and dodged blows shown (WorldView.StartRoll), for the playtests.
        internal int RollsShown => _view?.RollsShown ?? 0;
        internal int DodgesShown => _view?.DodgesShown ?? 0;
        internal int SkillFxPieces => _view?.SkillFxPieces ?? 0;
        internal int SkillShotsDrawn => _view?.SkillShotsDrawn ?? 0;
        internal bool WarningShown => _view != null && _view.WarningShown;
        internal int PoolsShown => _view?.PoolsShown ?? 0;
        // Ordinary monsters' marks, pools, one-off effects, pieces and shots as drawn (WorldView.MonsterFx).
        internal int MonsterMarksShown => _view?.MonsterMarksShown ?? 0;
        internal int MonsterPoolsShown => _view?.MonsterPoolsShown ?? 0;
        // A dungeon's name cards shown, and the last name (WorldView.Dungeon.cs).
        internal int DungeonCardsShown => _view?.DungeonCardsShown ?? 0;
        internal string DungeonCardName => _view?.DungeonCardName ?? "";
        internal string DungeonCardLine => _view?.DungeonCardLine ?? "";
        internal int DungeonClearsShown => _view?.ClearsShown ?? 0;
        internal bool DungeonCardShowing => _view != null && _view.DungeonCardShowing;
        internal int MonsterFxAlive => _view?.MonsterFxAlive ?? 0;
        internal int MonsterFxPieces => _view?.MonsterFxPieces ?? 0;
        internal int MonsterShotsDrawn => _view?.MonsterShotsDrawn ?? 0;
        internal int MonsterBlowsShown => _view?.MonsterBlowsShown ?? 0;
        internal int AlertsShown => _view?.AlertsShown ?? 0;
        // What the screen showed of the trap rooms and the pacing director (WorldView.ShowPace), for the playtests.
        internal (int Banners, int Dust, int Howls, int Grates) PaceShown => _view == null ? (0, 0, 0, 0)
            : (_view.TrapBanners, _view.AmbushDustShown, _view.RaidHowlsHeard, _view.GratesDropped);
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
                EndSession(_endReason);
                return;
            }
            if (_client == null)
            {
                _saveMenu?.Poll();
                return;
            }
            NoteSteps(Time.unscaledDeltaTime);
            if (_map != null && Controls.MapPressed())
            {
                _screens.Toggle(GameScreen.Map);
                DevInfo.Note(_map.FullOpen ? "지도 열림" : "지도 닫힘");
            }
            else if (Controls.LeavePressed())
            {
                // Esc goes one step back: the hub, the open screen, the talk or waypoint list; with nothing open it leaves
                // the game, as before.
                if (_screens.Back()) DevInfo.Note("화면 닫힘");
                else if (_storyScreen != null && _storyScreen.Open) _storyScreen.Close();
                else
                {
                    EndSession("");
                    return;
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
            if (_inventory != null) _inventory.CrackAhead = bag ? _view.CrackAhead() : null;
            // Typing a pet's name keeps the keys from the game; the bag closing lets go of the field.
            bool typing = _inventory != null && _inventory.Typing;
            if (Controls.Typing && !typing && _inventory != null && !_inventory.Open) GUIUtility.keyboardControl = 0;
            Controls.Typing = typing;
            _view.Render(_tickClock / World.TickSeconds, Time.deltaTime);
            UiMotion.NoteHealth(_client.Health);
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
            if (_client.InPocket)
            {
                _biomeHere = _biomeAnnounced = biome;
                _biomeTime = 0;
                return;
            }
            if (biome != _biomeHere)
            {
                _biomeHere = biome;
                _biomeTime = 0;
                return;
            }
            _biomeTime += deltaTime;
            if (_biomeTime < BiomeBannerDelay || biome == _biomeAnnounced) return;
            _biomeAnnounced = biome;
            _view?.ShowBanner(BiomeMap.Name(biome));
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
            if (_client.Status != ClientStatus.Playing) return;
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

        // In co-op, over the fallen player: get up at home now rather than wait for an ally (World.GetUp).
        void DrawGetUpButton()
        {
            if (_client.IsDead && !_wasDown) _downSince = Time.time;
            _wasDown = _client.IsDead;
            if (!_client.IsDead || _client.Names.Count <= 1) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(_client.Position.X, _client.Position.Y + 1.3f, 0));
            if (screen.z < 0) return;
            // In a dungeon, at its entrance (World.Dungeon.cs).
            if (!Ui.WorldButton(Ui.FromScreen(screen), _client.DungeonHere != null ? "입구에서 일어나기" : "집에서 일어나기", 140)) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.GetUpCommand());
        }

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
                Music.Update(Tune.Cave, null, 0, deltaTime);
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
            var ambience = place != null ? PlaceAmbience(place.Definition.Style) : biome == Biome.Lava ? Ambience.Lava : Ambience.Cave;
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
            Prefs.Set(SkillOrderKey, string.Join(",", _chosenClasses) + "|" + string.Join(",", _client.SkillOrder));
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
            Prefs.Set(PassedKey, string.Join(",", _guideProgress.Passed));
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
            // Not while a talk is on (G again would start it over).
            if (_storyScreen != null && _storyScreen.Open) return Meeting.None;
            if (!_client.Riding && _client.Places.Count > 0 && _client.CharacterNear() != null) return Meeting.Person;
            if (_client.NpcNear() != null) return Meeting.Merchant;
            if (!_client.Riding && _storyScreen != null && _storyScreen.CanOpenWaypoints) return Meeting.Waypoint;
            if (!_client.Riding && _client.PoiNear() != null) return Meeting.Poi;
            if (!_client.Riding && _client.DungeonChargeKind == 0 && (_client.DungeonThingNear() != null || _client.GateNear() != null || _client.ExitNear() != null)) return Meeting.Dungeon;
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
            if (_client.GateNear() is DungeonGate gate)
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
                    if (gate != null)
                    {
                        var standing = _client.DungeonOf(gate);
                        int inside = standing == null ? 0 : _client.PlayersIn(standing);
                        text = $"{_client.DungeonNameOf(gate)} 들어가기" + (inside > 0 ? $" (안에 {inside}명)" : "");
                    }
                    else if (thing != null) text = DungeonThingLabel(thing);
                    else text = "던전 나가기";
                    if (going) text = gate != null ? "들어가는 중…" : "나가는 중…";
                    else text += MeetHint(Meeting.Dungeon);
                    float width = Mathf.Min(Ui.Width - 16, 40 + 12 * text.Length);
                    GUI.enabled = !going && !_client.IsDead;
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

        void FixedTick()
        {
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
        internal string BagPage => _inventory?.BagPage ?? "";

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
            if (_connecting || _client != null) return;
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
            if (_client != null)
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
            if (_connecting || _client != null || code.Length == 0) return;
            _connecting = true;
            _status = "방을 찾는 중…";
            var (link, error) = await NetLink.JoinOnline(code);
            _connecting = false;
            if (link == null)
            {
                _status = "참가하지 못했습니다. " + error;
                return;
            }
            if (_client != null)
            {
                link.Dispose();
                return;
            }
            StartJoining(link);
        }

        // Solo, a LAN host on the port, or a host in an online room whose link is already started.
        void StartHosting(Mode mode, NetLink online = null)
        {
            // Saves are read first, so a broken one stops the start with a reason instead of being overwritten.
            if (!LoadCharacter(out byte[] character) || !ReadWorld(out var world))
            {
                online?.Dispose();
                _roomCode = null;
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
                _net.Received += (sender, data) => _host?.Receive(sender, data);
                _net.Disconnected += id => _host?.Disconnect(id);
            }

            ulong seed = world?.Seed ?? (ulong)DateTime.UtcNow.Ticks;

            // A new world is the game's big map; a saved one keeps the size it was made at (2026-11-11), and the
            // generator that made it (2026-11-21: a world from before rooms stays the caves it was).
            _host = new WorldHost(seed, _monsters, _loot,
                new HostConfig { MapSize = world?.Width ?? WorldGenerator.GameSize, MapVersion = world?.Generator ?? WorldGenerator.NewWorldVersion },
                bosses: _bosses, classes: _classes, tiles: _tiles, guide: _guide, npcs: _npcs,
                worldConfig: new WorldConfig { Forage = true, DayMinutes = Settings.DayMinutes, QuickRest = Settings.QuickRest, Director = true }, story: _story);
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
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
                Story = _story,
            };
            _kitSeen = -1;
            _hotbar = new Hotbar(_client, _loot, CharacterName);
            _screens = NewScreens();
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar, _screens) { Map = () => _map };
            _storyScreen = new StoryScreen(_client, SendToHost, () => _map) { OpenShop = tab => _inventory?.ShowShop(tab) };
            _host.Receive(LocalConnection, _client.JoinMessage());
            _mode = mode;
            _status = "";
            _dropFocus = true;
            StartGuide();
        }

        // The world save of this name, or null for a new world. False when there is one that cannot be read.
        bool ReadWorld(out WorldSave world)
        {
            world = null;
            byte[] worldData = SaveStore.Read(SaveStore.WorldKey(WorldName));
            if (worldData == null) return true;
            try
            {
                world = WorldSave.Read(worldData, _tiles, _loot);
                return true;
            }
            catch (InvalidDataException e)
            {
                _status = $"월드 '{WorldName}'의 저장본을 읽지 못했습니다: {e.Message} 다른 월드 이름을 쓰거나 [월드 지우기]로 지우세요.";
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
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
                Story = _story,
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
                if (id == _net.LocalClientId || id == NetworkManager.ServerClientId) _endReason = "호스트와 연결이 끊어졌습니다. " + _net.DisconnectReason;
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
                Save(SaveStore.CharacterKey(CharacterName), _client.SaveCharacter());
            }
            _worldSaveClock += deltaTime;
            if (_worldSaveClock < WorldSaveSeconds) return;
            _worldSaveClock = 0;
            if (_map != null && _map.Changed) Save(MapKey, _map.Save());
            if (_host != null) Save(_worldKey, _host.SaveWorld(Now()));
        }

        // Where this character's map of this world is kept, by the world's seed so a joined world has one too.
        string MapKey => SaveStore.MapKey(_client.Seed, _client.Generator, CharacterName);

        static void Save(string key, byte[] data)
        {
            if (key == null || data == null) return;
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
            if (_client != null) Save(SaveStore.CharacterKey(CharacterName), _client.SaveCharacter());
            if (_host != null) Save(_worldKey, _host.SaveWorld(Now()));
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
            if (_client == null)
            {
                DrawMenu();
                return;
            }
            // A full screen (growth, records) covers everything, and nothing else is drawn under it: the world darkened
            // 70% and red at the edges while the player is hurt, no HUD for its title to sit on, and no button a tap meant
            // for the screen could press instead (Docs/UiRework.md 4-5 전체 화면, 4-7 그리는 순서).
            // On a phone an encounter window is a sheet over everything too (U3): no HUD piece under it takes its taps.
            if (_screens.Covers && _client.Status == ClientStatus.Playing)
            {
                DrawFullScreen();
                return;
            }
            // The bag or an encounter window beside the world (Docs/UiRework.md U3): the HUD is its bars, no world button,
            // skill key or quick slot is drawn under it.
            bool side = _screens.Current != GameScreen.None;
            var window = side ? ScreenStack.AreaOf(_screens.Current) : default;
            _view?.DrawGui();
            DrawHud(compact: side);
            _view?.DrawBanner();
            // The "획득: …" lines under every window (Docs/UiRework.md 4-7 그리는 순서): drawn after the bag, they ran
            // over it on a phone (phone-bag-gear).
            DrawAcquired();
            // Not while the bag, a window or the hub is open: skills cannot be used then (ReadSkillPress), a tap in the
            // window must not fire one, and a slot would show in the gap beside the window. Nor under the waypoint list: a
            // button drawn before it would take a tap meant for one of its rows (2026-11-22, the web sweep on a phone).
            // While a talk is up its box sits where the skill keys and quick slots are, which are not drawn then
            // (Docs/UiRework.md 4-2; on a phone held upright the box lay over the quick slots, the U1 sweep).
            if (_storyScreen != null)
            {
                _storyScreen.KeyHint = MeetHint(Meeting.Person);
                _storyScreen.MerchantKeyHint = MeetHint(Meeting.Merchant);
            }
            if (_client.Status == ClientStatus.Playing && !side && !_screens.HubOpen && !(_storyScreen?.ListOpen ?? false))
            {
                DrawMachineSwitch();
                DrawPoiButton();
                DrawDungeonButtons();
                DrawBedButton();
                DrawGetUpButton();
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
            if (Controls.UsingTouch)
            {
                DrawTouchButtons();
                Controls.DrawTouch();
            }
        }

        // A full screen over the darkened world: the growth screens and the journal (InventoryWindow), or the map.
        void DrawFullScreen()
        {
            var screen = new Rect(0, 0, Ui.Width, Ui.Height);
            Controls.Block(screen);
            if (_screens.Current == GameScreen.Map)
            {
                if (_map != null) _map.DrawFull();
                else _screens.Close();
                return;
            }
            if (_screens.Full) UiSkin.FullBackdrop(screen);
            UiMotion.DrawGhosts();
            _inventory?.Draw();
        }

        // The "획득: …" lines in the bottom left, or above the skill buttons where they reach that far, newest at the
        // bottom. Each line is as tall as the font needs and a long one wraps between words, so none is cut off. They
        // stop under the goal list rather than run into it (a phone held sideways, phone-wide-small-hud-field), and a
        // line that would fall on the open bag is not drawn (phone-bag-gear): it shows again once the bag closes.
        void DrawAcquired()
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
                ShadowLabel(line, content.text, label, new Color(1, 1, 1, Mathf.Clamp01((AcquiredSeconds - age) / 1f)));
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
            bool skillPoints = _client.SkillPoints > 0 || _client.Classes.Count < ClassRules.SlotsAt(_client.Level), passivePoints = _client.PassivePoints > 0;
            if (skillPoints || passivePoints) InventoryWindow.Badge(menu);
            // Ends on the next Update, after this OnGUI has finished with the session.
            if (_screens.DrawHub(skillPoints, passivePoints)) _endReason = "";
        }

        // Name and how to play on one side, classes on the other; one above the other on a narrow screen. Scrolls
        // when the screen is too short for it, but [혼자 하기] (and what went wrong last) stays at the bottom, in view
        // however short the screen is.
        // The start screen as other games have it (2026-11-11, the owner: "the start screen is messy; tidy the menu like
        // other games, 게임 시작, 설정 and so on"): the name of the game over a column of a few big buttons, each opening a
        // page of its own with [뒤로] back. Esc goes back too.
        void DrawMenu()
        {
            bool ready = _loot != null && _loot.Success && _monsters != null && _monsters.Success && _bosses != null && _bosses.Success
                && _classes != null && _classes.Success && _tiles != null && _tiles.Success;
            if (_page != MenuPage.Main && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape
                && KeyBindings.Capturing == null)
            {
                Back();
                Event.current.Use();
            }
            UiSkin.Backdrop(new Rect(0, 0, Ui.Width, Ui.Height));
            if (_page == MenuPage.Main) DrawMainMenu(ready);
            else DrawMenuPage(ready);
        }

        enum MenuPage
        {
            Main,
            Play,
            Together,
            Saves,
            Settings,
            Keys,
        }

        MenuPage _page;
        SaveMenu _saveMenu;
        Vector2 _menuScroll;
        // Width of a line of text on a menu page this frame.
        float _menuColumn;

        void Open(MenuPage page)
        {
            _page = page;
            _menuScroll = Vector2.zero;
            _confirmDelete = false;
            Sounds.Play(Sound.Click);
            // The pass ends here, so the new page is laid out before it is drawn (IMGUI cannot draw what it did not lay out).
            GUIUtility.ExitGUI();
        }

        void Back() => _page = _page == MenuPage.Keys ? MenuPage.Settings : MenuPage.Main;

        GUIStyle _logo, _logoShadow, _tagline, _bigButton, _small;

        void DrawMainMenu(bool ready)
        {
            _logo ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _logoShadow ??= new GUIStyle(_logo);
            _tagline ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _bigButton ??= new GUIStyle(GUI.skin.button);
            _small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter };
            Ui.Text(_logo, Ui.Short ? 36 : 48, bold: true);
            Ui.Text(_logoShadow, Ui.Short ? 36 : 48, bold: true);
            Ui.Text(_tagline, 15);
            Ui.Text(_bigButton, 18);
            Ui.Text(_small, 12);
            _logo.normal.textColor = UiSkin.Gold;
            _logoShadow.normal.textColor = new Color(0, 0, 0, 0.8f);
            _tagline.normal.textColor = UiSkin.Muted;
            _small.normal.textColor = UiSkin.Muted;

            // The buttons and how tall the column is: the logo above it, the whole in the middle of the screen.
            var buttons = new List<(string Label, Action Press, bool Enabled)>
            {
                (Strings.T("menu.start", "게임 시작"), () => Open(MenuPage.Play), true),
                (Strings.T("menu.together", "함께 하기"), () => Open(MenuPage.Together), true),
                (Strings.T("menu.saves", "저장본"), () => Open(MenuPage.Saves), true),
                (Strings.T("menu.settings", "설정"), () => Open(MenuPage.Settings), true),
                (Strings.T("menu.feedback", "의견 보내기"), () => Application.OpenURL(Feedback.FormUrl()), true),
            };
            if (Application.platform != RuntimePlatform.WebGLPlayer && !Application.isEditor)
                buttons.Add((Strings.T("menu.quit", "끝내기"), Application.Quit, true));
            float logoHeight = Ui.Short ? 46 : 64, tagHeight = 24;
            float buttonHeight = Ui.Short ? 34 : 42, gap = Ui.Short ? 6 : 10;
            float width = Mathf.Min(300, Ui.Width - 40);
            float column = logoHeight + tagHeight + (Ui.Short ? 12 : 28) + buttons.Count * (buttonHeight + gap);
            float top = Mathf.Max(8, (Ui.Height - column) / 2 - (Ui.Short ? 0 : 20));
            float left = (Ui.Width - width) / 2;

            var logo = new Rect(0, top, Ui.Width, logoHeight);
            GUI.Label(new Rect(logo.x + 3, logo.y + 3, logo.width, logo.height), "pray-to", _logoShadow);
            GUI.Label(logo, "pray-to", _logo);
            GUI.Label(new Rect(0, logo.yMax, Ui.Width, tagHeight), Strings.T("menu.tagline", "동굴 아래, 무너진 왕국의 이야기"), _tagline);
            float y = logo.yMax + tagHeight + (Ui.Short ? 12 : 28);
            GUI.enabled = ready;
            foreach (var (label, press, enabled) in buttons)
            {
                GUI.enabled = ready && enabled;
                if (Ui.Button(new Rect(left, y, width, buttonHeight), label, _bigButton)) press();
                y += buttonHeight + gap;
            }
            GUI.enabled = true;

            // Under the column: the export reminder (web and phone saves live only in the browser or the app) and why
            // the game cannot start, when it cannot.
            float noteWidth = Mathf.Min(420, Ui.Width - 32);
            if (SaveMenu.ExportDue)
            {
                var was = GUI.color;
                GUI.color = new Color(1f, 0.82f, 0.4f);
                string note = Ui.Wrap(SaveMenu.ExportReminder, GUI.skin.label, noteWidth);
                float h = GUI.skin.label.CalcHeight(new GUIContent(note), noteWidth);
                GUI.Label(new Rect((Ui.Width - noteWidth) / 2, y, noteWidth, h), note);
                y += h;
                GUI.color = was;
            }
            if (_status.Length > 0)
            {
                string status = Ui.Wrap(_status, GUI.skin.label, noteWidth);
                GUI.Label(new Rect((Ui.Width - noteWidth) / 2, y, noteWidth, GUI.skin.label.CalcHeight(new GUIContent(status), noteWidth)), status);
            }
            GUI.Label(new Rect(0, Ui.Height - 22, Ui.Width - 8, 20), string.Format(Strings.T("menu.version", "버전 {0}"), Protocol.Version), _small);
        }

        // A page: its name and [뒤로] on a bar at the top, what it holds in a column under it (scrolled when long), and on
        // the play page the start button kept at the bottom.
        void DrawMenuPage(bool ready)
        {
            float width = Mathf.Min(460, Ui.Width - 20);
            float height = Mathf.Min(_page == MenuPage.Saves || _page == MenuPage.Keys ? 640 : 520, Ui.Height - 20);
            var area = new Rect((Ui.Width - width) / 2, (Ui.Height - height) / 2, width, height);
            float inner = width - GUI.skin.box.padding.horizontal;
            _menuColumn = inner - GUI.skin.verticalScrollbar.fixedWidth - 8 - GUI.skin.label.margin.horizontal;

            Ui.TouchScroll(area, ref _menuScroll);
            // A stone window with the page's name in its head band and [뒤로] at the head's left (U1).
            UiMotion.BeginWindow("menu", area, UiKind.Stone);
            var inside = UiSkin.Window(area, UiKind.Stone, PageName(_page), PageIcon(_page), out var head, closable: false);
            float band = UiArt.HeadBand * UiSkin.Pt;
            if (Ui.Button(new Rect(head.x + UiArt.HeadCap * UiSkin.Pt, head.y + 2, 68, band - 4), Strings.T("menu.back", "뒤로"))) Back();
            GUILayout.BeginArea(inside);
            _menuScroll = Ui.BeginScrollView(_menuScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUILayout.Width(_menuColumn + GUI.skin.label.margin.horizontal));
            switch (_page)
            {
                case MenuPage.Play: DrawPlayPage(); break;
                case MenuPage.Together: DrawTogetherPage(ready); break;
                case MenuPage.Saves: DrawSaves(); break;
                case MenuPage.Settings: DrawSettingsPage(); break;
                case MenuPage.Keys: DrawKeys(); break;
            }
            GUILayout.EndVertical();
            Ui.EndScrollView();
            if (_status.Length > 0) GUILayout.Label(Ui.Wrap(_status, GUI.skin.label, inner - GUI.skin.label.margin.horizontal));
            if (_page == MenuPage.Play)
            {
                _bigButton ??= new GUIStyle(GUI.skin.button);
                Ui.Text(_bigButton, 18);
                GUI.enabled = ready;
                if (Ui.LayoutButton(Strings.T("menu.solo", "혼자 하기"), _bigButton, Ui.Look.Primary, GUILayout.Height(42))) StartSolo();
                GUI.enabled = true;
            }
            GUILayout.EndArea();
            UiMotion.EndWindow();
        }

        // The small picture in a menu page's head.
        static string[] PageIcon(MenuPage page) => page switch
        {
            MenuPage.Play => UiIcons.Person,
            MenuPage.Together => UiIcons.Talk,
            MenuPage.Saves => UiIcons.Book,
            MenuPage.Settings or MenuPage.Keys => UiIcons.Gear,
            _ => null,
        };

        static string PageName(MenuPage page) => page switch
        {
            MenuPage.Play => Strings.T("menu.start", "게임 시작"),
            MenuPage.Together => Strings.T("menu.together", "함께 하기"),
            MenuPage.Saves => Strings.T("menu.saves", "저장본"),
            MenuPage.Settings => Strings.T("menu.settings", "설정"),
            MenuPage.Keys => Strings.T("menu.keys", "키 바꾸기"),
            _ => "",
        };

        // A small heading over a group on a page.
        void Heading(string text)
        {
            GUILayout.Space(6);
            var was = GUI.color;
            GUI.color = UiSkin.Gold;
            GUILayout.Label(text);
            GUI.color = was;
        }

        // The character and the world to play, how long a day is, and how classes go.
        void DrawPlayPage()
        {
            DrawCharacterField();
            Heading(Strings.T("menu.world", "월드"));
            string worldName = GUILayout.TextField(_worldName, 24);
            if (worldName != _worldName) _confirmDelete = false;
            _worldName = worldName;
            bool worldSaved = SaveStore.Exists(SaveStore.WorldKey(WorldName));
            GUILayout.BeginHorizontal();
            GUILayout.Label(worldSaved ? Strings.T("menu.world_saved", "저장된 월드를 이어한다") : Strings.T("menu.world_new", "새 월드를 만든다"));
            if (worldSaved && Ui.LayoutButton(_confirmDelete ? Strings.T("menu.world_delete_again", "한 번 더 누르면 지운다") : Strings.T("menu.world_delete", "월드 지우기")))
            {
                if (_confirmDelete) SaveStore.Delete(SaveStore.WorldKey(WorldName));
                _confirmDelete = !_confirmDelete;
            }
            GUILayout.EndHorizontal();
            // How long a day takes in the worlds this player opens or hosts; joiners go by the host's (Docs/Time.md).
            if (Ui.LayoutButton(string.Format(Strings.T("menu.daylength", "하루 길이: {0}분"), Settings.DayMinutes))) Settings.DayMinutes = Settings.NextDayMinutes();
            Heading(Strings.T("menu.classes", "클래스"));
            if (_classes != null && _classes.Success) DrawClassNote();
        }

        void DrawCharacterField()
        {
            Heading(Strings.T("menu.name", "캐릭터 이름"));
            _playerName = GUILayout.TextField(_playerName, 16);
            GUILayout.Label(SaveStore.Exists(SaveStore.CharacterKey(CharacterName)) ? Strings.T("menu.character_saved", "저장된 캐릭터로 이어한다") : Strings.T("menu.character_new", "새 캐릭터로 시작한다"));
        }

        // Online through Unity Relay (the same on the web and on PC, so they play together), and on PC the LAN too.
        void DrawTogetherPage(bool ready)
        {
            DrawCharacterField();
            GUI.enabled = ready && !_connecting;
            Heading(Strings.T("menu.online", "온라인"));
            GUILayout.Label(Ui.Wrap(Strings.T("menu.online_note", "온라인 협동: 방을 만들면 코드가 나온다. 친구는 그 코드로 참가한다(웹과 PC가 함께)"), GUI.skin.label, _menuColumn));
            GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.host_world", "방을 만들면 [게임 시작]의 월드({0})가 열린다"), WorldName), GUI.skin.label, _menuColumn));
            if (Ui.LayoutButton(_connecting ? Strings.T("menu.connecting", "연결하는 중…") : Strings.T("menu.host_online", "온라인 방 만들기"), GUILayout.Height(34))) StartOnlineHost();
            GUILayout.BeginHorizontal();
            _codeInput = GUILayout.TextField(_codeInput, 12, GUILayout.MinWidth(80));
            if (Ui.LayoutButton(Strings.T("menu.join_code", "코드로 참가"), GUILayout.Width(120))) StartOnlineJoin();
            GUILayout.EndHorizontal();
            // A browser cannot listen for connections, so the LAN way is only on PC.
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Heading(Strings.T("menu.lan", "같은 네트워크(LAN)"));
                if (Ui.LayoutButton(string.Format(Strings.T("menu.host_lan", "호스트 (포트 {0})"), NetLink.DefaultPort))) StartHost();
                GUILayout.Label(Strings.T("menu.host_address", "호스트 주소"));
                _address = GUILayout.TextField(_address, 64);
                if (Ui.LayoutButton(Strings.T("menu.join", "참가"))) StartJoining();
            }
            GUI.enabled = true;
        }

        // Sound, the screen and play, a row each: what it is on the left, its button on the right.
        void DrawSettingsPage()
        {
            float button = Mathf.Min(150, _menuColumn * 0.45f);
            bool Setting(string name, string value)
            {
                GUILayout.BeginHorizontal();
                Ui.Row(name);
                GUILayout.Label(name, GUILayout.Width(_menuColumn - button - 8));
                // Only the value on the button (its name is beside it); the probe still reads "name: value".
                var rect = GUILayoutUtility.GetRect(new GUIContent(value), GUI.skin.button, GUILayout.Width(button));
                bool pressed = Ui.Button(rect, value, $"{name}: {value}");
                GUILayout.EndHorizontal();
                return pressed;
            }
            Heading(Strings.T("menu.sound_group", "소리"));
            // Each press turns it up a quarter, and from full back to off. The menu plays the cave's tune, so the
            // change is heard at once.
            if (Setting(Strings.T("menu.sound", "효과음"), Settings.VolumeText))
            {
                Settings.Volume = (Settings.Volume + 1) % (Settings.MaxVolume + 1);
                Sounds.Play(Sound.Click);
            }
            if (Setting(Strings.T("menu.music", "음악"), Settings.MusicVolumeText)) Settings.MusicVolume = (Settings.MusicVolume + 1) % (Settings.MaxVolume + 1);
            // CC0 recordings (Recordings, 2026-11-11) or the sounds made in code, to compare.
            if (Setting(Strings.T("menu.sound_kind", "소리 종류"), Settings.RecordedSounds ? Strings.T("menu.sound_recorded", "녹음") : Strings.T("menu.sound_made", "합성")))
            {
                Settings.RecordedSounds = !Settings.RecordedSounds;
                Sounds.Play(Sound.Click);
            }
            Heading(Strings.T("menu.screen_group", "화면"));
            if (Setting(Strings.T("menu.darkness", "어둠과 빛"), Strings.OnOff(Settings.Darkness))) Settings.Darkness = !Settings.Darkness;
            // The pixel font or the smooth one; the menu is drawn in the new one at once (Ui).
            if (Setting(Strings.T("menu.font", "글꼴"), Settings.PixelFont ? Strings.T("menu.font_pixel", "도트") : Strings.T("menu.font_smooth", "매끈"))) Settings.PixelFont = !Settings.PixelFont;
            if (Setting(Strings.T("menu.shake", "화면 흔들림"), Strings.OnOff(Settings.ScreenShake))) Settings.ScreenShake = !Settings.ScreenShake;
            if (Setting(Strings.T("menu.bossintro", "보스 등장 연출"), Strings.OnOff(Settings.BossIntro))) Settings.BossIntro = !Settings.BossIntro;
            // Windows growing as they open, buttons sinking and shaking, counts popping (UiMotion); reduced, only fades.
            if (Setting(Strings.T("menu.motion", "움직임"), Settings.ReduceMotion ? Strings.T("menu.motion_reduced", "줄이기") : Strings.T("menu.motion_normal", "보통")))
                Settings.ReduceMotion = !Settings.ReduceMotion;
            // The words' language, when the string sheet has another (Strings, Docs/Strings.md).
            if (Strings.HasOthers && Setting(Strings.T("menu.language", "언어"), Strings.NameOf(Settings.Language))) Settings.Language = Strings.NextLanguage();
            Heading(Strings.T("menu.play_group", "플레이"));
            // The goals for new players in the HUD; met goals still count while it is off (Settings.Guide).
            if (Setting(Strings.T("menu.guide", "목표 안내"), Strings.OnOff(Settings.Guide))) Settings.Guide = !Settings.Guide;
            // Rumours or arrows, and slow or quick rest (묶음 4c, the person's call 2026-11-21: on, with the old way a
            // press away). Rest is the host's: it holds in the worlds this player opens or hosts.
            if (Setting(Strings.T("menu.rumors", "길 안내"), Settings.Rumors ? Strings.T("menu.rumors_on", "소문") : Strings.T("menu.rumors_off", "화살표")))
                Settings.Rumors = !Settings.Rumors;
            if (Setting(Strings.T("menu.rest", "쉴 때 회복"), Settings.QuickRest ? Strings.T("menu.rest_quick", "빠름(예전)") : Strings.T("menu.rest_slow", "느림")))
                Settings.QuickRest = !Settings.QuickRest;
            GUILayout.Label(Ui.Wrap(Strings.T("menu.rumors_note", "소문: 갈 곳을 방향과 생물군계로만 알려 주고 지도에는 옅은 원을 그린다. 느림: 쉬면 8초 뒤 초당 1.5% 찬다(예전은 6초 뒤 3%). 야영지·제단·물약으로 회복한다"),
                GUI.skin.label, _menuColumn));
            // Frames a second, the dark's cost and the last inputs, for checking the game on a device (DevInfo).
            if (Setting(Strings.T("menu.devinfo", "개발 정보"), Strings.OnOff(Settings.DevInfo))) Settings.DevInfo = !Settings.DevInfo;
            // Keys only matter with a keyboard.
            if (!Controls.UsingTouch)
            {
                GUILayout.Space(6);
                if (Ui.LayoutButton(Strings.T("menu.keys", "키 바꾸기"))) Open(MenuPage.Keys);
            }
        }

        void DrawSaves()
        {
            _saveMenu ??= new SaveMenu(name => _playerName = name, name =>
            {
                _worldName = name;
                _confirmDelete = false;
            });
            _saveMenu.Draw(_menuColumn, CharacterName, WorldName);
        }

        // A row per changeable key: what it does and its key. Pressing the key's button waits for the next key pressed.
        void DrawKeys()
        {
            GUILayout.Label(Ui.Wrap(Strings.T("menu.keys_note", "키 바꾸기: 단추를 누른 뒤 쓸 키를 누른다. 다른 일에 쓰던 키면 서로 바뀐다. Esc는 그만두기"), GUI.skin.label, _menuColumn));
            foreach (var action in KeyBindings.Actions)
            {
                GUILayout.BeginHorizontal();
                Ui.Row(KeyBindings.ActionName(action));
                GUILayout.Label(KeyBindings.ActionName(action), GUILayout.Width(Mathf.Min(90, _menuColumn * 0.4f)));
                bool waiting = KeyBindings.Capturing == action;
                if (Ui.LayoutButton(waiting ? Strings.T("menu.press_key", "키를 누르세요…") : KeyBindings.Name(action))) KeyBindings.StartCapture(action);
                GUILayout.EndHorizontal();
            }
            if (KeyBindings.Message.Length > 0) GUILayout.Label(Ui.Wrap(KeyBindings.Message, GUI.skin.label, _menuColumn));
            if (Ui.LayoutButton(Strings.T("menu.keys_reset", "처음 키로"))) KeyBindings.ResetAll();
            GUILayout.Label(Ui.Wrap(Strings.T("menu.keys_fixed", "이동 WASD·화살표, 공격 스페이스·마우스, 메뉴 Esc는 바꿀 수 없다."), GUI.skin.label, _menuColumn));
        }

        // Classes are picked in the game, not here (2026-11-09, the owner's call): a saved character's are shown, and
        // for a new one how it goes.
        void DrawClassNote()
        {
            SyncCharacterClasses();
            if (_savedClasses != null)
            {
                var names = _savedClasses.Select(id => _classes.Classes.First(c => c.Id == id).Name);
                GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.classes_of", "{0}의 클래스: {1}"), CharacterName, string.Join(" + ", names)), GUI.skin.label, _menuColumn));
                GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.classes_saved", "저장된 캐릭터는 이 클래스로 시작한다. 빈 클래스 칸(레벨 {0}에 둘째, {1}에 셋째)은 게임 안 [스킬] 화면(K, 휴대폰은 [메뉴]의 [스킬])에서 그냥 고르고, 고른 클래스를 다른 것으로 바꾸려면 작업대 곁에서 금화를 낸다(금화 {2}부터, 레벨이 오를수록 더 든다)."), ClassRules.SlotLevels[1], ClassRules.SlotLevels[2], ClassRules.ChangeCoins(1)), GUI.skin.label, _menuColumn));
                return;
            }
            GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.classes_new", "클래스는 게임 안에서 고른다: [스킬] 화면(PC는 K·휴대폰은 [메뉴]의 [스킬])에서 처음에 하나, 레벨 {0}에 둘째, {1}에 셋째. 고르기 전에는 기본 공격만 쓴다."), ClassRules.SlotLevels[1], ClassRules.SlotLevels[2]), GUI.skin.label, _menuColumn));
            foreach (var option in _classes.Classes)
            {
                GUILayout.Label(Ui.Wrap($"{option.Name}: {option.Description}", GUI.skin.label, _menuColumn));
                GUILayout.Space(2);
            }
        }

        // The status box in the top left corner (Core Keeper and Terraria keep theirs as lean): health, mana and the
        // level's experience as bars, and only the lines that matter now under them: down, asleep, going home, a meal
        // running, points to spend. The time, weather and place go under the minimap (DrawInfo) and the goals and
        // missions in a list under this box (DrawTracker); how to play is on the start menu. While the bag is open
        // only the bars show, so the bag can sit just under them. Lines that do not fit wrap between words.
        void DrawHud(bool compact)
        {
            var lines = new List<string>();
            bool playing = _client.Status == ClientStatus.Playing;
            // The lines about someone being down: the only ones the box keeps in a boss fight whose bar goes under it.
            int urgent = 0;
            if (!playing)
            {
                lines.Add(_status);
            }
            else
            {
                // In co-op a fallen player waits for an ally (World.Revive), and allies are told who is down.
                bool together = _client.Names.Count > 1;
                if (_client.IsDead && !together) lines.Add(_client.LastCamp != null ? "쓰러졌습니다. 잠시 후 마지막에 쉰 야영지에서 일어납니다." : "쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
                if (_client.IsDead && together)
                    lines.Add($"쓰러졌다. 동료가 곁에 {World.ReviveSeconds:0}초 서 있으면 이 자리에서 일어난다 ({Mathf.Max(0, Mathf.CeilToInt(World.DownSeconds - (Time.time - _downSince)))}초 뒤 집에서)");
                if (!_client.IsDead)
                {
                    foreach (var other in _client.Others())
                    {
                        if (other.Kind == EntityKind.Player && other.Health <= 0 && _client.Names.TryGetValue(other.Id, out var fallen))
                            lines.Add($"{fallen}이(가) 쓰러졌다: 곁에 {World.ReviveSeconds:0}초 서 있으면 일으킨다");
                    }
                }
                urgent = lines.Count;
                if (!compact)
                {
                    if (_client.Sleeping)
                    {
                        int everyone = Math.Max(1, _client.Names.Count);
                        lines.Add(everyone > 1 ? $"자는 중… 모두 누우면 아침이 된다 ({_client.Sleepers}/{everyone}명)" : "자는 중…");
                    }
                    if ((_client.SelfStatus & StatusBits.Recalling) != 0) lines.Add("귀환 중… 맞으면 끊긴다");
                    // Making something by hand (World.StartCrafting): what, how far, how many more.
                    if (_client.Crafting != null)
                        lines.Add($"만드는 중: {Whole(_client.Crafting.Name)} {_client.CraftProgress * 100:0}%" + (_client.CraftLeft > 1 ? $" (남은 {_client.CraftLeft}개)" : ""));
                    if (_client.Food != null)
                    {
                        int left = Mathf.CeilToInt(_client.FoodLeft);
                        lines.Add($"{Whole(_client.Food.Name)} {left / 60}:{left % 60:00}");
                    }
                    // A shrine's blessing and a camp's rest (World.Pois.cs).
                    if (_client.Blessing != null)
                    {
                        int left = Mathf.CeilToInt(_client.BlessingSeconds);
                        lines.Add($"{Whole(_client.Blessing.Name)}의 축복 {left / 60}:{left % 60:00}");
                    }
                    if (_client.RestedSeconds > 0)
                    {
                        int left = Mathf.CeilToInt(_client.RestedSeconds);
                        lines.Add($"{Whole("잘 쉼")} {left / 60}:{left % 60:00}");
                    }
                    var points = new List<string>();
                    // An open class slot (Docs/Classes.md, 클래스 고르기): picked on the skills screen.
                    if (_client.Classes.Count < ClassRules.SlotsAt(_client.Level)) points.Add(Whole(_client.Classes.Count == 0 ? "클래스 고르기" : "클래스 하나 더"));
                    if (_client.SkillPoints > 0) points.Add(Whole($"스킬 포인트 {_client.SkillPoints}"));
                    if (_client.PassivePoints > 0) points.Add(Whole($"특성 포인트 {_client.PassivePoints}"));
                    // Where to spend them (U2): the skills and passives screens' keys, or the hub on a phone.
                    string where = Controls.UsingTouch ? "[메뉴]에서" : $"{KeyBindings.Name(KeyAction.Skills)}·{KeyBindings.Name(KeyAction.Passives)}";
                    if (points.Count > 0) lines.Add($"{string.Join(" · ", points)} ({where})");
                }
            }

            // In a boss fight on a narrow screen the box shows only the bars and those lines, so the boss bar under it
            // stays clear of the player (Docs/UiRework.md U0, phone-boss-bar); the playtests still read every line.
            var shownLines = new List<string>(lines);
            if (playing && !compact && _view?.ActiveBoss != null && !BossBarBesideHud) lines.RemoveRange(urgent, lines.Count - urgent);
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
            Ui.HudHeight = Mathf.Ceil(height) + 2;

            UiSkin.Panel(Ui.HudArea);
            GUILayout.BeginArea(Ui.HudArea, UiSkin.PanelArea);
            if (playing)
            {
                var bar = GUILayoutUtility.GetRect(width, HealthBarHeight, GUILayout.ExpandWidth(true));
                float share = _client.MaxHealth > 0 ? _client.Health / _client.MaxHealth : 0;
                UiSkin.Bar(bar, share, $"생명력 {Mathf.CeilToInt(_client.Health)} / {Mathf.CeilToInt(_client.MaxHealth)}");
                GUILayout.Space(2);
                var manaBar = GUILayoutUtility.GetRect(width, ManaBarHeight, GUILayout.ExpandWidth(true));
                float manaShare = _client.MaxMana > 0 ? _client.Mana / _client.MaxMana : 0;
                UiSkin.Bar(manaBar, manaShare, $"마나 {Mathf.FloorToInt(_client.Mana)} / {Mathf.CeilToInt(_client.MaxMana)}", mana: true, textSize: 12);
                GUILayout.Space(2);
                var experienceBar = GUILayoutUtility.GetRect(width, ExperienceBarHeight, GUILayout.ExpandWidth(true));
                float progress = Leveling.Progress(_client.Experience);
                UiSkin.Bar(experienceBar, progress, $"Lv {_client.Level} · {(int)(progress * 100)}%", textSize: 12, experience: true);
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
            var weather = World.WeatherOf(today);
            string place = BiomeMap.Name(_client.Biomes.At(_client.Position));
            // In a dungeon its name and level, and no weather down there (묶음 5a).
            if (_client.DungeonHere is DungeonInstance dungeon) lines.Add(DungeonLine(dungeon));
            else lines.Add(weather != Weather.Clear ? $"{World.WeatherName(weather)} · {place}" : place);
            if (World.EventOf(today) != DayEvent.None) lines.Add(World.EventName(World.EventOf(today)));
            if (_mode != Mode.Solo)
            {
                string room = _mode == Mode.Host ? (_roomCode != null ? $"방 코드 {_roomCode}" : $"호스트 · 포트 {NetLink.DefaultPort}") : "참가 중";
                lines.Add($"{room} · {_client.Names.Count}명");
            }

            var style = SmallLine();
            style.alignment = TextAnchor.UpperRight;
            var area = Ui.InfoArea(0);
            float y = area.y;
            foreach (string line in lines)
            {
                float height = style.CalcHeight(new GUIContent(line), area.width);
                ShadowLabel(new Rect(area.x, y, area.width, height), line, style, new Color(0.92f, 0.9f, 0.85f));
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
            // Folded to its first line on a phone held upright and in a boss fight that needs the room (TrackerFolds): the
            // lines above still carry every entry for the playtests. A tap opens the journal.
            if (TrackerFolds(_view?.ActiveBoss != null))
            {
                string first = entries[0].Title + (entries.Count > 1 ? $"  +{entries.Count - 1}" : "");
                float lineHeight = _trackerTitle.CalcHeight(new GUIContent("가"), width);
                var folded = new Rect(area.x, area.yMax + 6, area.width, Mathf.Ceil(lineHeight + box.padding.vertical - 4));
                GUI.color = new Color(1, 1, 1, 0.85f * previous.a);
                UiSkin.Panel(folded);
                GUI.color = previous;
                _trackerTitle.wordWrap = false;
                _trackerTitle.clipping = TextClipping.Clip;
                ShadowLabel(new Rect(folded.x + box.padding.left, folded.y + (folded.height - lineHeight) / 2, width - 14, lineHeight), first, _trackerTitle, new Color(1f, 0.85f, 0.4f));
                _trackerTitle.wordWrap = true;
                UiSkin.Icon(new Rect(folded.xMax - box.padding.right - 10, folded.y, 12, folded.height), UiIcons.Book, new Color(0.95f, 0.85f, 0.6f));
                Ui.Probe("일지 열기", folded);
                Controls.Block(folded);
                if (GUI.Button(folded, GUIContent.none, GUIStyle.none)) _inventory.ShowJournal(InventoryWindow.JournalSection.Goals);
                Ui.LeftColumnBottom = folded.yMax;
                return shown;
            }
            var laid = new List<(string Title, string Text, float TitleHeight, float TextHeight)>();
            float height = box.padding.vertical;
            foreach (var (title, raw) in entries)
            {
                string text = string.Join("\n", raw.Split('\n').Select(part => Ui.Wrap(part, style, width)));
                float titleHeight = _trackerTitle.CalcHeight(new GUIContent(title), width);
                float textHeight = style.CalcHeight(new GUIContent(text), width);
                laid.Add((title, text, titleHeight, textHeight));
                height += titleHeight + textHeight + 4;
            }
            var frame = new Rect(area.x, area.yMax + 6, area.width, Mathf.Ceil(height));
            GUI.color = new Color(1, 1, 1, 0.85f * previous.a);
            UiSkin.Panel(frame);
            GUI.color = previous;
            float y = frame.y + box.padding.top;
            for (int i = 0; i < laid.Count; i++)
            {
                var (title, text, titleHeight, textHeight) = laid[i];
                ShadowLabel(new Rect(frame.x + box.padding.left, y, width, titleHeight), title, _trackerTitle, new Color(1f, 0.85f, 0.4f));
                if (i == trackedAt)
                {
                    var flag = UiSkin.IconSize(MapIcons.Pin);
                    UiSkin.Icon(new Rect(frame.xMax - box.padding.right - flag.x + 2, y + 1, flag.x, flag.y), MapIcons.Pin, new Color(0.9f, 0.3f, 0.22f));
                }
                y += titleHeight;
                ShadowLabel(new Rect(frame.x + box.padding.left, y, width, textHeight), text, style, Color.white);
                y += textHeight + 4;
            }
            Ui.LeftColumnBottom = frame.yMax;
            return shown;
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
        internal static bool TrackerFolds(bool boss) => Ui.Width < 600 && Ui.Height > Ui.Width || boss && !BossBarBesideHud;

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

        static void DrawBossBar(EntityView boss, int level)
        {
            var place = BossBarArea(0);
            float width = place.width, x = place.x, top = place.y;
            // The name line as tall as the font needs, so it is not cut off, and the bar under it.
            string advice = boss.Boss.Level > level ? $" · 권장 Lv {boss.Boss.Level}" : "";
            var title = new GUIContent($"{boss.Boss.Name} · {boss.Phase}/{boss.Boss.PhaseCount}페이즈{advice}");
            float titleHeight = GUI.skin.label.CalcHeight(title, width);
            GUI.Label(new Rect(x, top, width, titleHeight), title);
            float share = boss.MaxHealth > 0 ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0;
            UiSkin.Bar(new Rect(x, top + titleHeight, width, 18), share, "", boss: true);
            if (Event.current.type == EventType.Repaint) BossBarShown = new Rect(x, top, width, titleHeight + 18);
        }
    }
}
