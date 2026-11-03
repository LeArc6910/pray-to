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
        const float GuideCheckSeconds = 0.5f;
        // A goal's hint shows this long after the goal comes up; then only the goal stays.
        const float GoalHintSeconds = 30;
        // The how-to-play lines show this long after the game starts, and for a new character until its first goal.
        const float ControlsSeconds = 60;
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
        // The guide for new players (Docs/Guide.md): how far this character has come, checked twice a second, and
        // when its current goal came up, for showing the hint a while.
        GuideProgress _guideProgress;
        float _guideClock;
        float _goalShownAt;
        // Class ids in the order picked; the first one's skills take the first slots. Both to start with, so a new
        // player sees what mixing does.
        readonly List<string> _chosenClasses = new List<string> { "warrior", "fire_mage" };
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
        // Until when the HUD shows the how-to-play lines (ControlsSeconds).
        float _controlsUntil;

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
            _chosenClasses.RemoveAll(id => !_classes.Classes.Any(c => c.Id == id));
            var errors = new List<string>(_loot.Errors);
            errors.AddRange(_monsters.Errors);
            errors.AddRange(_bosses.Errors);
            errors.AddRange(_classes.Errors);
            errors.AddRange(_tiles.Errors);
            errors.AddRange(_guide.Errors);
            errors.AddRange(_npcs.Errors);
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
            if (!GUI.Button(rect, $"균열 열기 ({_client.Rift.Unlocked}단계)")) return;
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
            if (!GUI.Button(rect, $"기지 방어 시작 ({_client.Defense.Unlocked}단계)")) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.StartDefenseCommand());
        }

        void DrawBedButton()
        {
            if (_client.Bed is not (int x, int y) || !(_client.Sleeping || _client.CanSleep)) return;
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 1.35f, 0));
            if (screen.z < 0) return;
            Vector2 point = Ui.FromScreen(screen);
            var rect = new Rect(point.x - 44, point.y - 13, 88, 26);
            Controls.Block(rect);
            if (!GUI.Button(rect, _client.Sleeping ? "일어나기" : "잠자기")) return;
            Sounds.Play(Sound.Click);
            SendToHost(_client.Sleeping ? _client.WakeCommand() : _client.SleepCommand());
        }

        // The boss tune while a boss is awake on screen, else the biome's; the menu has the cave's. Under it the cave's
        // drips or the lava's bubbles, and the hum of the nearest running machine in view.
        void UpdateMusic(float deltaTime)
        {
            if (_client == null || _client.Status != ClientStatus.Playing || _client.Map == null)
            {
                Music.Update(Tune.Cave, null, 0, deltaTime);
                return;
            }
            // The biome the player has settled in (UpdateBiome), so walking along a border does not keep switching.
            var biome = _biomeAnnounced;
            var tune = _view?.ActiveBoss != null ? Tune.Boss : biome switch
            {
                Biome.Mushroom => Tune.Mushroom,
                Biome.Ice => Tune.Ice,
                Biome.Crystal => Tune.Crystal,
                Biome.Lava => Tune.Lava,
                Biome.Abyss => Tune.Abyss,
                _ => Tune.Cave,
            };
            float nearest = float.MaxValue;
            foreach (var machine in _client.Machines())
            {
                if (machine.On) nearest = Mathf.Min(nearest, Vec2.Distance(_client.Position, TileMap.Center(machine.X, machine.Y)));
            }
            Music.Update(tune, biome == Biome.Lava ? Ambience.Lava : Ambience.Cave, 1 - nearest / MachineHearing, deltaTime);
        }

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

        // "skillorder2": since 2026-11-03 the order picks from every class skill, not just the first four, so the
        // orders kept before mean other skills and are let go.
        string SkillOrderKey => "skillorder2." + CharacterName;

        // The skill order kept for this character, if it is for the classes picked now.
        IReadOnlyList<int> KeptSkillOrder()
        {
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
            var passed = _guideProgress.Update(GuideFacts.Of(_client));
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
            _controlsUntil = Time.time + ControlsSeconds;
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
            if (GUI.Button(rect, text)) ToggleMachine();
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
            if (HoldAttack)
            {
                attack = true;
                if (HeldAim.LengthSquared > 1e-6f) aim = HeldAim;
            }
            byte[] input = _client.Step(move, aim, attack);
            if (input != null) SendToHost(input);

            if (_host == null) return;
            _host.Tick();
            foreach (var message in _host.TakeOutgoing())
            {
                if (message.Connection == LocalConnection) _client.Receive(message.Data);
                else _net?.Send(message.Connection, message.Data, message.Reliable);
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

        // For the play mode tests (Assets/Tests/PlayMode/FeatureTests.cs): the host's world in a solo or hosted game
        // and this player in it, the sheets, and the attack held down toward HeldAim as if the button were.
        internal World HostWorld => _host?.World;
        internal Player HostPlayer => _client == null ? null : _host?.World.FindPlayer(_client.PlayerId);
        internal MonsterData MonsterSheet => _monsters;
        internal LootData LootSheet => _loot;
        internal bool HoldAttack { get; set; }
        internal Vec2 HeldAim { get; set; }

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

            _host = new WorldHost(seed, _monsters, _loot, bosses: _bosses, classes: _classes, tiles: _tiles, guide: _guide, npcs: _npcs);
            if (world != null)
            {
                _host.Restore(world);
                foreach (string warning in world.Warnings) _acquired.Add(("월드 저장본: " + warning, 0));
            }
            _worldKey = SaveStore.WorldKey(WorldName);
            _worldSaveClock = 0;
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses, _tiles)
            {
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
            };
            _kitSeen = -1;
            _hotbar = new Hotbar(_client, _loot, CharacterName);
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar);
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
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses, _tiles)
            {
                Character = character,
                JoinSkillOrder = KeptSkillOrder(),
                Guide = _guide,
                Npcs = _npcs,
            };
            _kitSeen = -1;
            _hotbar = new Hotbar(_client, _loot, CharacterName);
            _inventory = new InventoryWindow(_client, SendToHost, _hotbar);
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
            if (_client.InventoryVersion != _savedInventoryVersion || (_client.Experience != _savedExperience && _client.InventoryVersion > 0))
            {
                _savedInventoryVersion = _client.InventoryVersion;
                _savedExperience = _client.Experience;
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
                DrawRiftButton();
                DrawDefenseButton();
                SkillBar.Draw(_client);
                _hotbar?.Draw(_client.Skills.Count);
            }
            _inventory?.Draw();
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
            if (GUI.Button(bag, "가방")) _inventory.Open = !_inventory.Open;
            // Ends on the next Update, after this OnGUI has finished with the session.
            if (GUI.Button(leave, "나가기")) _endReason = "";
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
            GUILayout.BeginArea(area, GUI.skin.box);
            _menuScroll = GUILayout.BeginScrollView(_menuScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            if (wide) GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(column));
            GUILayout.Label($"pray-to 프로토타입 (버전 {Protocol.Version})");
            GUILayout.Space(8);
            GUILayout.Label("이름 (캐릭터)");
            _playerName = GUILayout.TextField(_playerName, 16);
            GUILayout.Label(SaveStore.Exists(SaveStore.CharacterKey(CharacterName)) ? "저장된 캐릭터로 이어한다" : "새 캐릭터로 시작한다");
            GUILayout.Space(4);
            GUILayout.Label("월드 (혼자 하기·호스트)");
            string worldName = GUILayout.TextField(_worldName, 24);
            if (worldName != _worldName) _confirmDelete = false;
            _worldName = worldName;
            bool worldSaved = SaveStore.Exists(SaveStore.WorldKey(WorldName));
            GUILayout.BeginHorizontal();
            GUILayout.Label(worldSaved ? "저장된 월드를 이어한다" : "새 월드를 만든다");
            if (worldSaved && GUILayout.Button(_confirmDelete ? "한 번 더 누르면 지운다" : "월드 지우기"))
            {
                if (_confirmDelete) SaveStore.Delete(SaveStore.WorldKey(WorldName));
                _confirmDelete = !_confirmDelete;
            }
            GUILayout.EndHorizontal();
            // The saves on this device: pick one, carry them off in a file, bring a file's in (SaveMenu).
            if (GUILayout.Button(_side == MenuSide.Saves ? "저장본 닫기" : "저장본: 고르기·내보내기·가져오기")) OpenSide(MenuSide.Saves);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"어둠과 빛: {(Settings.Darkness ? "켜짐" : "꺼짐")}")) Settings.Darkness = !Settings.Darkness;
            // Each press turns it up a quarter, and from full back to off.
            if (GUILayout.Button($"효과음: {Settings.VolumeText}"))
            {
                Settings.Volume = (Settings.Volume + 1) % (Settings.MaxVolume + 1);
                Sounds.Play(Sound.Click);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            // The menu plays the cave's tune, so the change is heard at once.
            if (GUILayout.Button($"음악: {Settings.MusicVolumeText}")) Settings.MusicVolume = (Settings.MusicVolume + 1) % (Settings.MaxVolume + 1);
            // Frames a second, the dark's cost and the last inputs, for checking the game on a device (DevInfo).
            if (GUILayout.Button($"개발 정보: {(Settings.DevInfo ? "켜짐" : "꺼짐")}")) Settings.DevInfo = !Settings.DevInfo;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            // The pixel font or the smooth one; the menu is drawn in the new one at once (Ui).
            if (GUILayout.Button($"글꼴: {(Settings.PixelFont ? "도트" : "매끈")}")) Settings.PixelFont = !Settings.PixelFont;
            // The goals for new players in the HUD; met goals still count while it is off (Settings.Guide).
            if (GUILayout.Button($"목표 안내: {(Settings.Guide ? "켜짐" : "꺼짐")}")) Settings.Guide = !Settings.Guide;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"화면 흔들림: {(Settings.ScreenShake ? "켜짐" : "꺼짐")}")) Settings.ScreenShake = !Settings.ScreenShake;
            // Keys only matter with a keyboard; the list opens in the other column, in place of the classes.
            if (!Controls.UsingTouch && GUILayout.Button(_side == MenuSide.Keys ? "키 바꾸기 닫기" : "키 바꾸기")) OpenSide(MenuSide.Keys);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUI.enabled = ready && !_connecting;
            // Online, through Unity Relay: the same on the web and on PC, so they play together.
            GUILayout.Label(Ui.Wrap("온라인 협동: 방을 만들면 코드가 나온다. 친구는 그 코드로 참가한다(웹과 PC가 함께)", GUI.skin.label, _menuColumn));
            if (GUILayout.Button(_connecting ? "연결하는 중…" : "온라인 방 만들기")) StartOnlineHost();
            GUILayout.BeginHorizontal();
            _codeInput = GUILayout.TextField(_codeInput, 12, GUILayout.MinWidth(80));
            if (GUILayout.Button("코드로 참가", GUILayout.Width(110))) StartOnlineJoin();
            GUILayout.EndHorizontal();
            // A browser cannot listen for connections, so the LAN way is only on PC.
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GUILayout.Space(8);
                GUILayout.Label("같은 네트워크(LAN)");
                if (GUILayout.Button($"호스트 (포트 {NetLink.DefaultPort})")) StartHost();
                GUILayout.Label("호스트 주소");
                _address = GUILayout.TextField(_address, 64);
                if (GUILayout.Button("참가")) StartJoining();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            if (!wide && Event.current.type == EventType.Repaint) _sideTop = GUILayoutUtility.GetLastRect().yMax;

            if (wide) GUILayout.Space(16);
            GUILayout.BeginVertical(GUILayout.Width(column));
            if (_side == MenuSide.Keys && !Controls.UsingTouch) DrawKeys();
            else if (_side == MenuSide.Saves) DrawSaves();
            else if (_classes != null && _classes.Success) DrawClassPicker();
            GUILayout.EndVertical();

            if (wide) GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (_status.Length > 0) GUILayout.Label(Ui.Wrap(_status, GUI.skin.label, inner - GUI.skin.label.margin.horizontal));
            GUI.enabled = ready;
            if (GUILayout.Button("혼자 하기", GUILayout.Height(36))) StartSolo();
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
            GUILayout.Label(Ui.Wrap("키 바꾸기: 단추를 누른 뒤 쓸 키를 누른다. 다른 일에 쓰던 키면 서로 바뀐다. Esc는 그만두기", GUI.skin.label, _menuColumn));
            foreach (var action in KeyBindings.Actions)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(KeyBindings.ActionName(action), GUILayout.Width(Mathf.Min(90, _menuColumn * 0.4f)));
                bool waiting = KeyBindings.Capturing == action;
                if (GUILayout.Button(waiting ? "키를 누르세요…" : KeyBindings.Name(action))) KeyBindings.StartCapture(action);
                GUILayout.EndHorizontal();
            }
            if (KeyBindings.Message.Length > 0) GUILayout.Label(Ui.Wrap(KeyBindings.Message, GUI.skin.label, _menuColumn));
            if (GUILayout.Button("처음 키로")) KeyBindings.ResetAll();
            GUILayout.Label(Ui.Wrap("이동 WASD·화살표, 공격 스페이스·마우스, 메뉴 Esc는 바꿀 수 없다.", GUI.skin.label, _menuColumn));
        }

        // A button per class; picking adds it after the ones already picked, picking again takes it out. The number
        // on a picked class is its turn in the skill slots.
        void DrawClassPicker()
        {
            int max = ClassRules.DefaultMaxClasses;
            GUILayout.Label(Ui.Wrap($"클래스 (최대 {max}개, 고른 순서대로 스킬 칸을 채운다)", GUI.skin.label, _menuColumn));
            foreach (var option in _classes.Classes)
            {
                int order = _chosenClasses.IndexOf(option.Id);
                bool full = order < 0 && _chosenClasses.Count >= max;
                GUI.enabled = !full;
                string mark = order >= 0 ? $"[{order + 1}] " : "[ ] ";
                if (GUILayout.Button(mark + option.Name))
                {
                    if (order >= 0) _chosenClasses.RemoveAt(order);
                    else _chosenClasses.Add(option.Id);
                }
                GUI.enabled = true;
                GUILayout.Label(Ui.Wrap(
                    $"{option.Description}\n패시브: {SkillText.Passive(option)}\n스킬: {string.Join(", ", option.Skills.Select(k => k.Name))}", GUI.skin.label, _menuColumn));
                GUILayout.Space(4);
            }
            if (_chosenClasses.Count == 0) GUILayout.Label(Ui.Wrap("고르지 않으면 기본 공격만 쓴다.", GUI.skin.label, _menuColumn));
        }

        // The status box: a health bar, then lines that grow the box to fit, so a narrow window wraps them (between
        // words) instead of cutting them off. Compact while the bag is open, so the bag can sit just under it: only the
        // health bar, and the line saying so when the player is down. The how-to-play lines show only for a while
        // (ControlsSeconds), and never on a short screen (a phone held sideways), so the HUD does not cover a big part
        // of the screen (a fifth of an 800×600 window once they go; 2026-11-02 check). Lines sit close together, and a
        // key stays on one line with what it does (Keyed).
        void DrawHud(bool compact)
        {
            var lines = new List<string>();
            bool playing = _client.Status == ClientStatus.Playing;
            if (!playing)
            {
                lines.Add(_status);
            }
            else if (compact)
            {
                if (_client.IsDead) lines.Add("쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
            }
            else
            {
                if (_client.IsDead) lines.Add("쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
                string mode = _mode == Mode.Host ? (_roomCode != null ? $"온라인 방 코드 {_roomCode}" : $"호스트 중 (포트 {NetLink.DefaultPort})")
                    : _mode == Mode.Join ? "참가 중" : "혼자 하기";
                string players = _mode == Mode.Solo ? "" : $" · 접속 {_client.Names.Count}명";
                long clock = _client.Clock;
                int today = Calendar.Day(clock);
                string rain = (World.WeatherOf(today) != Weather.Clear ? " · " + World.WeatherName(World.WeatherOf(today)) : "")
                    + (World.EventOf(today) != DayEvent.None ? " · " + World.EventName(World.EventOf(today)) : "");
                lines.Add($"{Keyed(Calendar.Text(clock), Calendar.PhaseName(Calendar.Phase(clock)))}{rain} · Lv {_client.Level} ({(int)(Leveling.Progress(_client.Experience) * 100)}%){(_client.PassivePoints > 0 ? $" · 특성 {_client.PassivePoints}" : "")} · {BiomeMap.Name(_client.Biomes.At(_client.Position))} · {mode}{players}");
                if (World.EventOf(today) != DayEvent.None) lines.Add(World.EventText(World.EventOf(today)));
                if (_client.Rift.Open)
                {
                    var rift = _client.Rift;
                    string wave = rift.Wave > World.RiftWaves ? "수호자" : $"{rift.Wave}/{World.RiftWaves} 물결";
                    lines.Add($"공허 균열 {rift.Tier}단계 · {wave} · 남은 적 {rift.MonstersLeft} · {rift.SecondsLeft / 60}:{rift.SecondsLeft % 60:00} · {World.AffixNames(rift.Affixes)}");
                }
                if (_client.Defense.Active)
                {
                    var defense = _client.Defense;
                    lines.Add($"기지 방어 {defense.Level}단계 · {defense.Wave}/{World.DefenseWaves} 물결 · 남은 적 {defense.MonstersLeft} · 수호석 {defense.StonePercent}%");
                }
                if (_client.Sleeping)
                {
                    int everyone = Math.Max(1, _client.Names.Count);
                    lines.Add(everyone > 1 ? $"자는 중… 모두 누우면 아침이 된다 ({_client.Sleepers}/{everyone}명)" : "자는 중…");
                }
                // The nearest arena the player is not standing in: the first boss's from the spawn, the deep one's
                // once there.
                int next = -1;
                for (int i = 0; i < _client.Arenas.Count; i++)
                {
                    float distance = Vec2.Distance(_client.Arenas[i], _client.Position);
                    if (distance <= _client.ArenaRadius) continue;
                    if (next < 0 || distance < Vec2.Distance(_client.Arenas[next], _client.Position)) next = i;
                }
                if (next >= 0)
                {
                    var toArena = _client.Arenas[next] - _client.Position;
                    string arena = next == 0 ? "보스 아레나" : next == 1 ? "깊은 아레나" : next == 2 ? "용암 아레나" : "심연 아레나";
                    lines.Add($"{arena}: {Compass(toArena)}쪽 {Mathf.RoundToInt(toArena.Length)}칸");
                }
                if ((_client.SelfStatus & StatusBits.Recalling) != 0) lines.Add("귀환 중… 맞으면 끊긴다");
                if (_client.Food != null)
                {
                    int left = Mathf.CeilToInt(_client.FoodLeft);
                    lines.Add($"음식: {_client.Food.Name} {left / 60}:{left % 60:00}");
                }
                var goal = Settings.Guide ? _guideProgress?.Current : null;
                if (goal != null)
                {
                    string reward = goal.Rewards.Count > 0 ? $" (보상: {goal.RewardText})" : "";
                    lines.Add($"목표 {_guideProgress.Done + 1}/{_guideProgress.Total}: {goal.Text}{reward}");
                    if (!Ui.Short && Time.time - _goalShownAt < GoalHintSeconds) lines.Add(goal.Hint);
                }
            }
            bool showControls = !compact && !Ui.Short && playing
                && (Time.time < _controlsUntil || Settings.Guide && _guideProgress != null && _guideProgress.Done == 0);
            if (showControls && Controls.UsingTouch)
            {
                lines.Add($"{Keyed("왼쪽 스틱", "이동")} · {Keyed("오른쪽 스틱", "공격")}(끄는 쪽으로, 곡괭이를 들면 벽을 캔다)");
                lines.Add($"{Keyed("오른쪽 아래", "스킬·물약")} · 미니맵을 누르면 지도");
            }
            else if (showControls)
            {
                lines.Add($"{Keyed("WASD", "이동")} · {Keyed("마우스", "조준")} · {Keyed("클릭/스페이스", "공격")}(곡괭이를 들면 벽을 캔다)");
                string skills = string.Join(" ", Enumerable.Range(0, 4).Select(Controls.SkillKeyName));
                string quickKeys = $"{KeyBindings.Name(KeyAction.Hotbar1)}~{KeyBindings.Name(KeyAction.Hotbar6)}";
                lines.Add($"{Keyed(skills, "스킬")} · {Keyed(Controls.PotionKeyName, "물약")} · {Keyed(quickKeys, "퀵슬롯")} · {Keyed(KeyBindings.Name(KeyAction.Bag), "가방")} · {Keyed(KeyBindings.Name(KeyAction.Map), "지도")} · {Keyed("Esc", "메뉴")}");
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
            if (playing) height += HealthBarHeight + ManaBarHeight + 2;
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
            }
            foreach (string line in lines) GUILayout.Label(line, label);
            GUILayout.EndArea();

            if (_view?.ActiveBoss is EntityView boss) DrawBossBar(boss);
        }

        GUIStyle _hudLine;

        // A key and what it does, joined by a space no line breaks at (Ui.Wrap breaks only at plain spaces), so the
        // line never ends between them ("· M / 지도"); the spaces inside the key ("Q E R F") hold too.
        static string Keyed(string key, string meaning) => (key + " " + meaning).Replace(' ', '\u00A0');

        // Between the HUD box and the minimap; under the HUD when the screen is too narrow for that.
        static void DrawBossBar(EntityView boss)
        {
            float left = Ui.HudArea.xMax + 10;
            float right = Ui.MinimapArea.x - 10;
            float top = 20;
            if (right - left < 240)
            {
                left = 20;
                top = Ui.HudArea.yMax + 10;
            }
            float width = Mathf.Min(600, right - left);
            float x = left + (right - left - width) / 2;
            // The name line as tall as the font needs, so it is not cut off, and the bar under it.
            var title = new GUIContent($"{boss.Boss.Name} · {boss.Phase}/{boss.Boss.PhaseCount}페이즈");
            float titleHeight = GUI.skin.label.CalcHeight(title, width);
            GUI.Label(new Rect(x, top, width, titleHeight), title);
            float share = boss.MaxHealth > 0 ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0;
            UiSkin.Bar(new Rect(x, top + titleHeight, width, 18), share, "", boss: true);
        }

        // Which of the eight directions an offset points, as a Korean word. Up on screen is north.
        static string Compass(Vec2 offset)
        {
            string[] names = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };
            int sector = Mathf.RoundToInt(offset.Angle / (Mathf.PI / 4));
            return names[(sector % 8 + 8) % 8];
        }
    }
}
