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
        // What the menu's second column shows: the classes, the keys to change, or the saves on this device.
        enum MenuSide
        {
            Classes,
            Keys,
            Saves,
        }

        MenuSide _side;
        SaveMenu _saveMenu;
        Vector2 _menuScroll;
        // On a narrow screen the second column is under the first: where it starts, and whether to scroll there once
        // (after a button opened the keys or the saves).
        float _sideTop;
        bool _scrollToSide;
        // Width of a line of text in a menu column this frame.
        float _menuColumn;
        WorldHost _host;
        WorldClient _client;
        NetLink _net;
        WorldView _view;
        // Rounded-corner tiles within ten cells of the player (WorldView.CornersNear), for the playtests.
        internal int CornersNear => _view == null || _client?.Map == null ? -1
            : _view.CornersNear(TileMap.Cell(_client.Position.X), TileMap.Cell(_client.Position.Y), 10);
        InventoryWindow _inventory;
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
            if (_camera == null) _camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
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
                _map.FullOpen = !_map.FullOpen;
                DevInfo.Note(_map.FullOpen ? "지도 열림" : "지도 닫힘");
            }
            else if (Controls.LeavePressed())
            {
                // Esc closes the full map first.
                if (_map != null && _map.FullOpen)
                {
                    _map.FullOpen = false;
                }
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
            if (Controls.BagPressed())
            {
                _inventory.Open = !_inventory.Open;
                _dropFocus = true;
                DevInfo.Note(_inventory.Open ? "가방 열림" : "가방 닫힘");
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
            SaveProgress(Time.deltaTime);
            _view ??= new WorldView(_client, _camera);
            _map ??= new MapView(_client, SaveStore.Read(MapKey));
            _client.AdvanceRenderClock(Time.deltaTime);
            UpdateGuide(Time.deltaTime);
            UpdateBiome(Time.deltaTime);
            UpdateDay();
            RememberKit();
            var changedTiles = _client.TakeChangedTiles();
            _view.UpdateTiles(changedTiles);
            _map.UpdateTiles(changedTiles);
            _view.ShowTarget = (_inventory != null && _inventory.Open) || (_hotbar != null && _hotbar.PointsAtGround);
            _view.TargetSoil = _inventory != null && _inventory.Open && _inventory.PointsAtSoil || _hotbar != null && _hotbar.PointsAtSoil;
            // A hammer's tap now and then while something is being made by hand (2026-11-09).
            if (_client.Crafting != null && Time.time >= _hammerAt)
            {
                Sounds.Play(Sound.Hammer, 0.5f);
                _hammerAt = Time.time + 0.6f;
            }
            _view.TargetSize = Math.Max(_inventory != null && _inventory.Open ? _inventory.PointsAtSize : 1, _hotbar != null ? _hotbar.PointsAtSize : 1);
            if (_inventory != null) _inventory.CrackAhead = _inventory.Open ? _view.CrackAhead() : null;
            // Typing a pet's name keeps the keys from the game; the bag closing lets go of the field.
            bool typing = _inventory != null && _inventory.Typing;
            if (Controls.Typing && !typing && _inventory != null && !_inventory.Open) GUIUtility.keyboardControl = 0;
            Controls.Typing = typing;
            _view.Render(_tickClock / World.TickSeconds, Time.deltaTime);
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
            Vector2 point = Ui.FromScreen(screen);
            var rect = new Rect(point.x - 70, point.y - 13, 140, 26);
            Controls.Block(rect);
            if (!Ui.Button(rect, $"균열 열기 ({_client.Rift.Unlocked}단계)")) return;
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
            Vector2 point = Ui.FromScreen(screen);
            var rect = new Rect(point.x - 80, point.y - 13, 160, 26);
            Controls.Block(rect);
            if (!Ui.Button(rect, $"기지 방어 시작 ({_client.Defense.Unlocked}단계)")) return;
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
            Vector2 point = Ui.FromScreen(screen);
            var rect = new Rect(point.x - 70, point.y - 13, 140, 26);
            Controls.Block(rect);
            if (!Ui.Button(rect, "집에서 일어나기")) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.GetUpCommand());
        }

        void DrawBedButton()
        {
            if (_client.Bed is not (int x, int y) || !(_client.Sleeping || _client.CanSleep)) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 1.35f, 0));
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            var rect = new Rect(point.x - 44, point.y - 13, 88, 26);
            Controls.Block(rect);
            if (!Ui.Button(rect, _client.Sleeping ? "일어나기" : "잠자기")) return;
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
            var quick = Controls.TakeHotbarPresses();
            bool machine = Controls.MachinePressed();
            if (_inventory.Open || (_map != null && _map.FullOpen)) return;
            var aim = Controls.SkillAim(_camera, _client.Position);
            // On touch a skill button alone aims at the nearest foe, so it needs no second thumb on the stick.
            if (Controls.UsingTouch && aim.LengthSquared < 0.01f) aim = NearestFoeDirection();
            if (slots.Count > 0) LastSkillAim = aim;
            foreach (int slot in slots) UseSkill(slot, aim);
            if (potion) DrinkPotion();
            foreach (int slot in quick) UseQuickSlot(slot);
            if (machine) ToggleMachine();
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
            Vector2 point = Ui.FromScreen(screen);
            string text = machine.On ? "끄기" : "켜기";
            if (!Controls.UsingTouch) text += $" ({KeyBindings.Name(KeyAction.Machine)})";
            var rect = new Rect(point.x - 40, point.y - 13, 80, 26);
            Controls.Block(rect);
            if (Ui.Button(rect, text)) ToggleMachine();
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
            // Clicking in the bag or on the map should not swing the sword.
            if (_inventory.Open || (_map != null && _map.FullOpen)) attack = false;
            if (HoldAttack) attack = true;
            // A tap on the attack stick without pulling it swings at the nearest foe rather than where one walks
            // (Docs/WebDemo.md 터치, 2026-11-11, the research report's candidate 16).
            if (Controls.UsingTouch && attack && !Controls.AimPulled)
            {
                var foe = NearestFoeDirection();
                if (foe.LengthSquared > 0.01f) aim = foe;
            }
            if ((HoldAttack || AimHeld) && HeldAim.LengthSquared > 1e-6f) aim = HeldAim;
            byte[] input = _client.Step(move, aim, attack);
            if (input != null) SendToHost(input);

            if (_host == null) return;
            // Only a playtest runs a solo world faster than time (Playtest.cs, "speed").
            int ticks = _mode == Mode.Solo ? Mathf.Clamp(HostTicksPerStep, 1, 60) : 1;
            for (int i = 0; i < ticks; i++)
            {
                _host.Tick();
                foreach (var message in _host.TakeOutgoing())
                {
                    if (message.Connection == LocalConnection) _client.Receive(message.Data);
                    else _net?.Send(message.Connection, message.Data, message.Reliable);
                }
            }
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
        internal WorldClient LocalClient => _client;
        internal Player HostPlayer => _client == null ? null : _host?.World.FindPlayer(_client.PlayerId);
        internal MonsterData MonsterSheet => _monsters;
        internal LootData LootSheet => _loot;
        internal ClassData ClassSheet => _classes;
        // The guide's goals, for the playtest bot's numbers (PlaytestBot.cs).
        internal GuideData GuideSheet => _guide;
        internal bool HoldAttack { get; set; }
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
        internal int OpenDoors => _view?.OpenDoors ?? 0;
        internal bool MapOpen => _map != null && _map.FullOpen;
        internal IEnumerable<string> FloatingTexts => _view?.FloatingTexts ?? Enumerable.Empty<string>();
        string[] _hudLines = Array.Empty<string>();

        // The bag window open or shut, for the play mode tests' screenshots.
        internal bool BagOpen
        {
            get => _inventory.Open;
            set => _inventory.Open = value;
        }

        internal void ShowBag(string page) => _inventory.Show(page);

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

            _host = new WorldHost(seed, _monsters, _loot, bosses: _bosses, classes: _classes, tiles: _tiles, guide: _guide, npcs: _npcs,
                worldConfig: new WorldConfig { Forage = true, DayMinutes = Settings.DayMinutes }, story: _story);
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
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar);
            _storyScreen = new StoryScreen(_client, SendToHost);
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
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar);
            _storyScreen = new StoryScreen(_client, SendToHost);
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
        string MapKey => SaveStore.MapKey(_client.Seed, CharacterName);

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
            _guideProgress = null;
            _dayPhase = null;
            _acquired.Clear();
            _mode = Mode.Menu;
            _status = status;
            _endReason = null;
        }

        void OnDestroy() => EndSession("");

        void OnGUI()
        {
            Ui.Begin();
            DevInfo.Read(Event.current);
            DrawScreen();
            DevInfo.Draw();
        }

        void DrawScreen()
        {
            if (_dropFocus)
            {
                GUIUtility.keyboardControl = 0;
                _dropFocus = false;
            }
            if (_client == null)
            {
                DrawMenu();
                return;
            }
            // The full map covers everything, and nothing else is drawn under it: no HUD for its title to sit on,
            // and no button a tap meant to close the map could press instead.
            if (_map != null && _map.FullOpen)
            {
                _map.DrawFull();
                return;
            }
            _view?.DrawGui();
            DrawHud(compact: _inventory.Open);
            // Not while the bag is open: skills cannot be used then (ReadSkillPress), a tap in the window must not fire
            // one, and a slot would show in the gap beside the window.
            if (_client.Status == ClientStatus.Playing && !_inventory.Open)
            {
                DrawMachineSwitch();
                DrawBedButton();
                DrawGetUpButton();
                DrawRiftButton();
                _storyScreen?.DrawWorldButtons(_camera);
                DrawDefenseButton();
                SkillBar.Draw(_client);
                _hotbar?.Draw(_client.Skills.Count);
            }
            _inventory?.Draw();
            // The story's talk and the waypoint list, over the rest (Docs/Story.md).
            if (_client.Status == ClientStatus.Playing && !_inventory.Open) _storyScreen?.DrawPanels();
            DrawAcquired();
            _map?.DrawMinimap(Ui.MinimapArea);
            if (Controls.UsingTouch)
            {
                DrawTouchButtons();
                Controls.DrawTouch();
            }
        }

        // The "획득: …" lines in the bottom left, or above the skill buttons where they reach that far, newest at the
        // bottom. Each line is as tall as the font needs and a long one wraps between words, so none is cut off.
        void DrawAcquired()
        {
            var label = GUI.skin.label;
            float width = Mathf.Min(400, Ui.Width - 28);
            // Clear of the skill keys and the quick slots, wherever they are.
            var slots = SkillBar.Bounds(_client.Skills.Count);
            var quick = Hotbar.Bounds(_client.Skills.Count);
            slots = Rect.MinMaxRect(Mathf.Min(slots.xMin, quick.xMin), Mathf.Min(slots.yMin, quick.yMin), Mathf.Max(slots.xMax, quick.xMax), slots.yMax);
            float y = slots.x < 14 + width ? slots.y - 6 : Ui.Height - 10;
            var previous = GUI.color;
            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                var (text, age) = _acquired[i];
                var content = new GUIContent(Ui.Wrap(text, label, width));
                float height = label.CalcHeight(content, width);
                y -= height;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((AcquiredSeconds - age) / 1f));
                GUI.Label(new Rect(14, y, width, height), content);
            }
            GUI.color = previous;
        }

        // Stand-ins for Tab and Esc on phones, under the minimap.
        void DrawTouchButtons()
        {
            var bag = Ui.TouchButtonArea(0, 2);
            var leave = Ui.TouchButtonArea(1, 2);
            Controls.Block(bag);
            Controls.Block(leave);
            if (Ui.Button(bag, "가방")) _inventory.Open = !_inventory.Open;
            if (_client.SkillPoints > 0 || _client.PassivePoints > 0) InventoryWindow.Badge(bag);
            // Ends on the next Update, after this OnGUI has finished with the session.
            if (Ui.Button(leave, "나가기")) _endReason = "";
        }

        // Name and how to play on one side, classes on the other; one above the other on a narrow screen. Scrolls
        // when the screen is too short for it, but [혼자 하기] (and what went wrong last) stays at the bottom, in view
        // however short the screen is.
        void DrawMenu()
        {
            // Two columns side by side wherever they fit, a phone held sideways included.
            bool wide = Ui.Width >= 700;
            float width = wide ? Mathf.Min(740, Ui.Width - 20) : Mathf.Min(380, Ui.Width - 20);
            float height = Mathf.Min(wide ? 460 : 760, Ui.Height - 20);
            // Both columns get a set width that leaves room for the scroll bar, so a long line wraps instead of
            // widening its column, and the menu never scrolls sideways.
            float inner = width - GUI.skin.box.padding.horizontal;
            float list = inner - GUI.skin.verticalScrollbar.fixedWidth - 8;
            float column = wide ? (list - 16) / 2 : list;
            // Text in a column is that wide less a label's margins.
            _menuColumn = column - GUI.skin.label.margin.horizontal;
            bool ready = _loot != null && _loot.Success && _monsters != null && _monsters.Success && _bosses != null && _bosses.Success
                && _classes != null && _classes.Success && _tiles != null && _tiles.Success;
            var area = new Rect((Ui.Width - width) / 2, (Ui.Height - height) / 2, width, height);
            if (_scrollToSide && Event.current.type == EventType.Layout)
            {
                _menuScroll.y = wide ? 0 : _sideTop;
                _scrollToSide = false;
            }
            Ui.TouchScroll(area, ref _menuScroll);
            GUILayout.BeginArea(area, GUI.skin.box);
            _menuScroll = Ui.BeginScrollView(_menuScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            if (wide) GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(column));
            GUILayout.Label(string.Format(Strings.T("menu.title", "pray-to 프로토타입 (버전 {0})"), Protocol.Version));
            GUILayout.Space(8);
            GUILayout.Label(Strings.T("menu.name", "이름 (캐릭터)"));
            _playerName = GUILayout.TextField(_playerName, 16);
            GUILayout.Label(SaveStore.Exists(SaveStore.CharacterKey(CharacterName)) ? Strings.T("menu.character_saved", "저장된 캐릭터로 이어한다") : Strings.T("menu.character_new", "새 캐릭터로 시작한다"));
            GUILayout.Space(4);
            GUILayout.Label(Strings.T("menu.world", "월드 (혼자 하기·호스트)"));
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
            // The words' language, when the string sheet has another (Strings, Docs/Strings.md).
            if (Strings.HasOthers && Ui.LayoutButton($"{Strings.T("menu.language", "언어")}: {Strings.NameOf(Settings.Language)}")) Settings.Language = Strings.NextLanguage();
            // The saves on this device: pick one, carry them off in a file, bring a file's in (SaveMenu).
            if (Ui.LayoutButton(_side == MenuSide.Saves ? Strings.T("menu.saves_close", "저장본 닫기") : Strings.T("menu.saves", "저장본: 고르기·내보내기·가져오기"))) OpenSide(MenuSide.Saves);
            // A GitHub issue form with the version, the device and how far the player has got (Feedback); nothing is
            // sent until the player sends it there.
            if (Ui.LayoutButton(Strings.T("menu.feedback", "의견 보내기"))) Application.OpenURL(Feedback.FormUrl());
            // Web and phone saves live in the browser or the app only: a reminder when nothing was exported for a week.
            if (SaveMenu.ExportDue)
            {
                var was = GUI.color;
                GUI.color = new Color(1f, 0.82f, 0.4f);
                GUILayout.Label(Ui.Wrap(SaveMenu.ExportReminder, GUI.skin.label, _menuColumn));
                GUI.color = was;
            }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton($"{Strings.T("menu.darkness", "어둠과 빛")}: {Strings.OnOff(Settings.Darkness)}")) Settings.Darkness = !Settings.Darkness;
            // Each press turns it up a quarter, and from full back to off.
            if (Ui.LayoutButton($"{Strings.T("menu.sound", "효과음")}: {Settings.VolumeText}"))
            {
                Settings.Volume = (Settings.Volume + 1) % (Settings.MaxVolume + 1);
                Sounds.Play(Sound.Click);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            // The menu plays the cave's tune, so the change is heard at once.
            if (Ui.LayoutButton($"{Strings.T("menu.music", "음악")}: {Settings.MusicVolumeText}")) Settings.MusicVolume = (Settings.MusicVolume + 1) % (Settings.MaxVolume + 1);
            // Frames a second, the dark's cost and the last inputs, for checking the game on a device (DevInfo).
            if (Ui.LayoutButton($"{Strings.T("menu.devinfo", "개발 정보")}: {Strings.OnOff(Settings.DevInfo)}")) Settings.DevInfo = !Settings.DevInfo;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            // The pixel font or the smooth one; the menu is drawn in the new one at once (Ui).
            if (Ui.LayoutButton($"{Strings.T("menu.font", "글꼴")}: {(Settings.PixelFont ? Strings.T("menu.font_pixel", "도트") : Strings.T("menu.font_smooth", "매끈"))}")) Settings.PixelFont = !Settings.PixelFont;
            // The goals for new players in the HUD; met goals still count while it is off (Settings.Guide).
            if (Ui.LayoutButton($"{Strings.T("menu.guide", "목표 안내")}: {Strings.OnOff(Settings.Guide)}")) Settings.Guide = !Settings.Guide;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton($"{Strings.T("menu.shake", "화면 흔들림")}: {Strings.OnOff(Settings.ScreenShake)}")) Settings.ScreenShake = !Settings.ScreenShake;
            // Keys only matter with a keyboard; the list opens in the other column, in place of the classes.
            if (!Controls.UsingTouch && Ui.LayoutButton(_side == MenuSide.Keys ? Strings.T("menu.keys_close", "키 바꾸기 닫기") : Strings.T("menu.keys", "키 바꾸기"))) OpenSide(MenuSide.Keys);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUI.enabled = ready && !_connecting;
            // Online, through Unity Relay: the same on the web and on PC, so they play together.
            GUILayout.Label(Ui.Wrap(Strings.T("menu.online_note", "온라인 협동: 방을 만들면 코드가 나온다. 친구는 그 코드로 참가한다(웹과 PC가 함께)"), GUI.skin.label, _menuColumn));
            if (Ui.LayoutButton(_connecting ? Strings.T("menu.connecting", "연결하는 중…") : Strings.T("menu.host_online", "온라인 방 만들기"))) StartOnlineHost();
            GUILayout.BeginHorizontal();
            _codeInput = GUILayout.TextField(_codeInput, 12, GUILayout.MinWidth(80));
            if (Ui.LayoutButton(Strings.T("menu.join_code", "코드로 참가"), GUILayout.Width(110))) StartOnlineJoin();
            GUILayout.EndHorizontal();
            // A browser cannot listen for connections, so the LAN way is only on PC.
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GUILayout.Space(8);
                GUILayout.Label(Strings.T("menu.lan", "같은 네트워크(LAN)"));
                if (Ui.LayoutButton(string.Format(Strings.T("menu.host_lan", "호스트 (포트 {0})"), NetLink.DefaultPort))) StartHost();
                GUILayout.Label(Strings.T("menu.host_address", "호스트 주소"));
                _address = GUILayout.TextField(_address, 64);
                if (Ui.LayoutButton(Strings.T("menu.join", "참가"))) StartJoining();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            if (!wide && Event.current.type == EventType.Repaint) _sideTop = GUILayoutUtility.GetLastRect().yMax;

            if (wide) GUILayout.Space(16);
            GUILayout.BeginVertical(GUILayout.Width(column));
            if (_side == MenuSide.Keys && !Controls.UsingTouch) DrawKeys();
            else if (_side == MenuSide.Saves) DrawSaves();
            else if (_classes != null && _classes.Success) DrawClassNote();
            GUILayout.EndVertical();

            if (wide) GUILayout.EndHorizontal();
            Ui.EndScrollView();

            if (_status.Length > 0) GUILayout.Label(Ui.Wrap(_status, GUI.skin.label, inner - GUI.skin.label.margin.horizontal));
            GUI.enabled = ready;
            if (Ui.LayoutButton(Strings.T("menu.solo", "혼자 하기"), GUILayout.Height(36))) StartSolo();
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // Opens that in the second column, or the classes again when it is already open. The GUI pass ends here, so
        // the column is laid out anew before it is drawn (IMGUI cannot draw controls it did not lay out).
        void OpenSide(MenuSide side)
        {
            _side = _side == side ? MenuSide.Classes : side;
            _scrollToSide = _side != MenuSide.Classes;
            GUIUtility.ExitGUI();
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
                GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.classes_saved", "저장된 캐릭터는 이 클래스로 시작한다. 빈 클래스 칸(레벨 {0}에 둘째, {1}에 셋째)은 게임 안 가방 [스킬] 탭에서 그냥 고르고, 고른 클래스를 다른 것으로 바꾸려면 작업대 곁에서 금화를 낸다(금화 {2}부터, 레벨이 오를수록 더 든다)."), ClassRules.SlotLevels[1], ClassRules.SlotLevels[2], ClassRules.ChangeCoins(1)), GUI.skin.label, _menuColumn));
                return;
            }
            GUILayout.Label(Ui.Wrap(string.Format(Strings.T("menu.classes_new", "클래스는 게임 안에서 고른다: 가방(PC는 Tab·휴대폰은 [가방])의 [스킬] 탭에서 처음에 하나, 레벨 {0}에 둘째, {1}에 셋째. 고르기 전에는 기본 공격만 쓴다."), ClassRules.SlotLevels[1], ClassRules.SlotLevels[2]), GUI.skin.label, _menuColumn));
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
            if (!playing)
            {
                lines.Add(_status);
            }
            else
            {
                // In co-op a fallen player waits for an ally (World.Revive), and allies are told who is down.
                bool together = _client.Names.Count > 1;
                if (_client.IsDead && !together) lines.Add("쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
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
                    var points = new List<string>();
                    // An open class slot (Docs/Classes.md, 클래스 고르기): picked in the bag's skills page.
                    if (_client.Classes.Count < ClassRules.SlotsAt(_client.Level)) points.Add(Whole(_client.Classes.Count == 0 ? "클래스 고르기" : "클래스 하나 더"));
                    if (_client.SkillPoints > 0) points.Add(Whole($"스킬 포인트 {_client.SkillPoints}"));
                    if (_client.PassivePoints > 0) points.Add(Whole($"특성 포인트 {_client.PassivePoints}"));
                    if (points.Count > 0) lines.Add($"{string.Join(" · ", points)} (가방에서)");
                }
            }

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

            GUILayout.BeginArea(Ui.HudArea, box);
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

            var shown = new List<string>(lines);
            if (playing) shown.Insert(0, $"Lv {_client.Level} ({(int)(Leveling.Progress(_client.Experience) * 100)}%)");
            if (playing && !compact) shown.AddRange(DrawInfo());
            if (playing && !compact) shown.AddRange(DrawTracker());
            else Ui.LeftColumnBottom = Ui.HudArea.yMax;
            if (Event.current.type == EventType.Repaint) _hudLines = shown.ToArray();

            // A boss in sight, still asleep: its first phase made ahead for when it wakes.
            if (_view?.BossInSight is string sleeper) Music.Prepare(Song.Of(sleeper, 1));
            if (_view?.ActiveBoss is EntityView boss) DrawBossBar(boss, _client.Level);
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
            GUI.color = new Color(0, 0, 0, 0.85f);
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
            lines.Add(weather != Weather.Clear ? $"{World.WeatherName(weather)} · {place}" : place);
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
            if (_storyScreen != null) entries.AddRange(_storyScreen.TrackerEntries());
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
                if (!Ui.Short && Time.time - _goalShownAt < GoalHintSeconds) text += $"\n{goal.Hint}";
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
            foreach (var npc in _client.Npcs?.Npcs ?? Enumerable.Empty<NpcDefinition>())
            {
                if (entries.Count(e => e.Title.StartsWith("이야기") || e.Title.StartsWith("부탁")) >= people) break;
                if (!_client.Friendships.ContainsKey(npc.Id)) continue;
                var step = _client.StoryStepOf(npc);
                if (step == null || _client.HeartsWith(npc) < step.Hearts) continue;
                entries.Add(($"이야기: {npc.Name} — {step.Title}", Needs(step) + (Ui.Short ? "" : $"\n보답: {step.RewardText}")));
            }
            foreach (var npc in _client.Npcs?.Npcs ?? Enumerable.Empty<NpcDefinition>())
            {
                if (entries.Count(e => e.Title.StartsWith("이야기") || e.Title.StartsWith("부탁")) >= people) break;
                if (!_client.Friendships.ContainsKey(npc.Id) || _client.RequestDoneToday(npc)) continue;
                var request = _client.RequestOf(npc);
                if (request == null) continue;
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
            var laid = new List<(string Title, string Text, float TitleHeight, float TextHeight)>();
            float height = box.padding.vertical;
            foreach (var (title, raw) in entries)
            {
                string text = string.Join("\n", raw.Split('\n').Select(part => Ui.Wrap(part, style, width)));
                float titleHeight = _trackerTitle.CalcHeight(new GUIContent(title), width);
                float textHeight = style.CalcHeight(new GUIContent(text), width);
                laid.Add((title, text, titleHeight, textHeight));
                height += titleHeight + textHeight + 4;
                shown.Add($"{title}: {raw.Replace('\n', ' ')}");
            }
            var frame = new Rect(area.x, area.yMax + 6, area.width, Mathf.Ceil(height));
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.Box(frame, GUIContent.none);
            GUI.color = previous;
            float y = frame.y + box.padding.top;
            foreach (var (title, text, titleHeight, textHeight) in laid)
            {
                ShadowLabel(new Rect(frame.x + box.padding.left, y, width, titleHeight), title, _trackerTitle, new Color(1f, 0.85f, 0.4f));
                y += titleHeight;
                ShadowLabel(new Rect(frame.x + box.padding.left, y, width, textHeight), text, style, Color.white);
                y += textHeight + 4;
            }
            Ui.LeftColumnBottom = frame.yMax;
            return shown;
        }

        GUIStyle _trackerTitle;

        GUIStyle _hudLine;

        // Spaces no line breaks at, so a name is never split over two lines of the HUD.
        static string Whole(string text) => text.Replace(' ', '\u00A0');

        // Between the HUD box and the minimap; under the HUD when the screen is too narrow for that.
        static void DrawBossBar(EntityView boss, int level)
        {
            float left = Ui.HudArea.xMax + 10;
            float right = Ui.MinimapArea.x - 10;
            float top = 20;
            if (right - left < 240)
            {
                left = 20;
                top = Ui.LeftColumnBottom + 10;
            }
            float width = Mathf.Min(600, right - left);
            float x = left + (right - left - width) / 2;
            // The name line as tall as the font needs, so it is not cut off, and the bar under it.
            string advice = boss.Boss.Level > level ? $" · 권장 Lv {boss.Boss.Level}" : "";
            var title = new GUIContent($"{boss.Boss.Name} · {boss.Phase}/{boss.Boss.PhaseCount}페이즈{advice}");
            float titleHeight = GUI.skin.label.CalcHeight(title, width);
            GUI.Label(new Rect(x, top, width, titleHeight), title);
            float share = boss.MaxHealth > 0 ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0;
            UiSkin.Bar(new Rect(x, top + titleHeight, width, 18), share, "", boss: true);
        }
    }
}
