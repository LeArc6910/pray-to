using System;
using System.Collections.Generic;
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
        // Half the view's height in tiles on a landscape screen. Portrait screens zoom out to keep this much width.
        const float ViewSize = 7;

        Mode _mode = Mode.Menu;
        string _playerName = "플레이어";
        string _address = "127.0.0.1";
        string _status = "";
        LootData _loot;
        MonsterData _monsters;
        BossData _bosses;
        ClassData _classes;
        // Class ids in the order picked; the first one's skills take the first slots. Both to start with, so a new
        // player sees what mixing does.
        readonly List<string> _chosenClasses = new List<string> { "warrior", "fire_mage" };
        Vector2 _menuScroll;
        WorldHost _host;
        WorldClient _client;
        NetLink _net;
        WorldView _view;
        InventoryWindow _inventory;
        MapView _map;
        // "획득: …" lines for things picked up, each shown for a few seconds.
        readonly List<(string Text, float Age)> _acquired = new List<(string, float)>();
        Camera _camera;
        float _tickClock;
        bool _joinSent;
        // Set from Netcode callbacks; the session ends on the next Update, outside the callback.
        string _endReason;

        void Awake()
        {
            Application.runInBackground = true;
            _camera = Camera.main;
            if (_camera == null) _camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = ViewSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

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
            _chosenClasses.RemoveAll(id => !_classes.Classes.Any(c => c.Id == id));
            var errors = new List<string>(_loot.Errors);
            errors.AddRange(_monsters.Errors);
            errors.AddRange(_bosses.Errors);
            errors.AddRange(_classes.Errors);
            if (errors.Count > 0) _status = "데이터 시트 오류:\n" + string.Join("\n", errors);
        }

        void Update()
        {
            Controls.UpdateTouches();
            _camera.orthographicSize = ViewSize * Mathf.Max(1, (float)Screen.height / Screen.width);
            if (_endReason != null)
            {
                EndSession(_endReason);
                return;
            }
            if (_client == null) return;
            if (_map != null && Controls.MapPressed())
            {
                _map.FullOpen = !_map.FullOpen;
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
            if (Controls.BagPressed()) _inventory.Open = !_inventory.Open;
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
            _view ??= new WorldView(_client, _camera);
            _map ??= new MapView(_client);
            _client.AdvanceRenderClock(Time.deltaTime);
            _view.Render(_tickClock / World.TickSeconds, Time.deltaTime);
            _map.Update();

            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                var (text, age) = _acquired[i];
                if (age + Time.deltaTime > AcquiredSeconds) _acquired.RemoveAt(i);
                else _acquired[i] = (text, age + Time.deltaTime);
            }
            foreach (string line in _client.TakeAcquired()) _acquired.Add((line, 0));
        }

        // Skills go to the host as soon as they are pressed, not with the next tick's input.
        void ReadSkillPress()
        {
            var slots = Controls.TakeSkillPresses();
            if (slots.Count == 0 || _inventory.Open || (_map != null && _map.FullOpen)) return;
            var aim = Controls.SkillAim(_camera, _client.Position);
            foreach (int slot in slots) UseSkill(slot, aim);
        }

        void FixedTick()
        {
            var (move, aim, attack) = Controls.Read(_camera, _client.Position);
            // Clicking in the bag or on the map should not swing the sword.
            if (_inventory.Open || (_map != null && _map.FullOpen)) attack = false;
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

        internal string Status => _status;

        internal int SkillCount => _client?.Skills.Count ?? 0;

        // False when the skill is not ready. Zero aim uses it the way the player faces.
        internal bool UseSkill(int slot, Vec2 aim = default)
        {
            byte[] command = _client?.SkillCommand(slot, aim);
            if (command == null) return false;
            SendToHost(command);
            return true;
        }

        void StartHosting(Mode mode)
        {
            if (mode == Mode.Host)
            {
                _net = NetLink.Host(NetLink.DefaultPort);
                if (_net == null)
                {
                    _status = $"호스트를 시작하지 못했습니다. 포트 {NetLink.DefaultPort}를 다른 프로그램이 쓰고 있는지 확인하세요.";
                    return;
                }
                _net.Received += (sender, data) => _host?.Receive(sender, data);
                _net.Disconnected += id => _host?.Disconnect(id);
            }

            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            _host = new WorldHost(seed, _monsters, _loot, bosses: _bosses, classes: _classes);
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses);
            _inventory = new InventoryWindow(_client, SendToHost);
            _host.Receive(LocalConnection, _client.JoinMessage());
            _mode = mode;
            _status = "";
        }

        void StartJoining()
        {
            _net = NetLink.Join(_address.Trim(), NetLink.DefaultPort);
            if (_net == null)
            {
                _status = "접속을 시작하지 못했습니다. 주소를 확인하세요.";
                return;
            }
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot, _classes, _chosenClasses);
            _inventory = new InventoryWindow(_client, SendToHost);
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
            _mode = Mode.Join;
            _status = "접속 중…";
        }

        void EndSession(string status)
        {
            _view?.Dispose();
            _map?.Dispose();
            _net?.Dispose();
            _view = null;
            _map = null;
            _net = null;
            _host = null;
            _client = null;
            _inventory = null;
            _acquired.Clear();
            _mode = Mode.Menu;
            _status = status;
            _endReason = null;
        }

        void OnDestroy() => EndSession("");

        void OnGUI()
        {
            Ui.Begin();
            if (_client == null)
            {
                DrawMenu();
                return;
            }
            _view?.DrawGui();
            DrawHud();
            // While the full map is up, the controls under it are drawn disabled: IMGUI hands a click to the first
            // control drawn under the pointer, so a tap meant to close the map could press [나가기] instead.
            bool mapOpen = _map != null && _map.FullOpen;
            GUI.enabled = !mapOpen;
            // Under the bag window. On a touch screen the buttons go away while the bag is open, so a tap in the
            // window cannot fire a skill.
            if (_client.Status == ClientStatus.Playing && !(Controls.UsingTouch && _inventory.Open)) SkillBar.Draw(_client);
            _inventory?.Draw();
            // Bottom left, or above the skill buttons where they reach that far.
            var skills = SkillBar.Bounds(_client.Skills.Count);
            float bottom = skills.width > 0 && skills.x < 14 + 400 ? skills.y - 30 : Ui.Height - 40;
            for (int i = 0; i < _acquired.Count; i++)
            {
                var (text, age) = _acquired[i];
                var previous = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((AcquiredSeconds - age) / 1f));
                GUI.Label(new Rect(14, bottom - 22 * (_acquired.Count - 1 - i), 400, 22), text);
                GUI.color = previous;
            }
            _map?.DrawMinimap(Ui.MinimapArea);
            if (Controls.UsingTouch)
            {
                DrawTouchButtons();
                Controls.DrawTouch();
            }
            GUI.enabled = true;
            // Last, over everything else.
            if (mapOpen) _map.DrawFull();
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
        // when the screen is too short for it.
        void DrawMenu()
        {
            bool wide = Ui.Width >= 760;
            float width = wide ? 740 : Mathf.Min(380, Ui.Width - 20);
            float height = Mathf.Min(wide ? 380 : 640, Ui.Height - 20);
            var area = new Rect((Ui.Width - width) / 2, (Ui.Height - height) / 2, width, height);
            GUILayout.BeginArea(area, GUI.skin.box);
            _menuScroll = GUILayout.BeginScrollView(_menuScroll);
            if (wide) GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(wide ? 340 : width - 40));
            GUILayout.Label("pray-to (M4 프로토타입)");
            GUILayout.Space(8);
            GUILayout.Label("이름");
            _playerName = GUILayout.TextField(_playerName, 16);
            GUILayout.Space(8);

            bool ready = _loot != null && _loot.Success && _monsters != null && _monsters.Success && _bosses != null && _bosses.Success
                && _classes != null && _classes.Success;
            GUI.enabled = ready;
            if (GUILayout.Button("혼자 하기")) StartSolo();
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // A browser cannot listen for connections, and joining would need a WebSocket transport.
                GUILayout.Space(8);
                GUILayout.Label("웹 데모에서는 혼자 하기만 됩니다. 협동은 PC 버전에서 합니다.");
            }
            else
            {
                if (GUILayout.Button($"호스트 (포트 {NetLink.DefaultPort})")) StartHost();
                GUILayout.Space(8);
                GUILayout.Label("호스트 주소");
                _address = GUILayout.TextField(_address, 64);
                if (GUILayout.Button("참가")) StartJoining();
            }
            GUI.enabled = true;

            if (_status.Length > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(_status);
            }
            GUILayout.EndVertical();

            if (wide) GUILayout.Space(16);
            GUILayout.BeginVertical();
            if (_classes != null && _classes.Success) DrawClassPicker();
            GUILayout.EndVertical();

            if (wide) GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // A button per class; picking adds it after the ones already picked, picking again takes it out. The number
        // on a picked class is its turn in the skill slots.
        void DrawClassPicker()
        {
            int max = ClassRules.DefaultMaxClasses;
            GUILayout.Label($"클래스 (최대 {max}개, 고른 순서대로 스킬 칸을 채운다)");
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
                GUILayout.Label($"{option.Description}\n패시브: {SkillText.Passive(option)}\n스킬: {string.Join(", ", option.Skills.Select(k => k.Name))}");
                GUILayout.Space(4);
            }
            if (_chosenClasses.Count == 0) GUILayout.Label("고르지 않으면 기본 공격만 쓴다.");
        }

        // The status box grows to fit its lines, so a narrow window wraps them instead of cutting them off.
        void DrawHud()
        {
            var lines = new List<string>();
            if (_client.Status != ClientStatus.Playing)
            {
                lines.Add(_status);
            }
            else
            {
                lines.Add($"생명력 {Mathf.CeilToInt(_client.Health)} / {Mathf.CeilToInt(_client.MaxHealth)}");
                if (_client.IsDead) lines.Add("쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
                string mode = _mode == Mode.Host ? $"호스트 중 (포트 {NetLink.DefaultPort})" : _mode == Mode.Join ? "참가 중" : "혼자 하기";
                lines.Add($"{mode} · 접속 {_client.Names.Count}명");
                var toArena = _client.Arena - _client.Position;
                if (toArena.Length > _client.ArenaRadius) lines.Add($"보스 아레나: {Compass(toArena)}쪽 {Mathf.RoundToInt(toArena.Length)}칸");
            }
            if (Controls.UsingTouch)
            {
                lines.Add("왼쪽 스틱 이동 · 오른쪽 스틱 공격(끄는 쪽으로)");
                lines.Add("오른쪽 아래 스킬 · 미니맵을 누르면 지도");
            }
            else
            {
                lines.Add("WASD 이동 · 마우스 조준 · 클릭/스페이스 공격");
                lines.Add("Q E R F 스킬 · Tab 가방 · M 지도 · Esc 메뉴");
            }

            var box = GUI.skin.box;
            var label = GUI.skin.label;
            float width = Ui.HudArea.width - box.padding.horizontal - label.margin.horizontal;
            float gap = Mathf.Max(label.margin.top, label.margin.bottom);
            float height = box.padding.vertical + label.margin.top + label.margin.bottom + gap * (lines.Count - 1);
            foreach (string line in lines) height += label.CalcHeight(new GUIContent(line), width);
            Ui.HudHeight = Mathf.Ceil(height) + 2;

            GUILayout.BeginArea(Ui.HudArea, box);
            foreach (string line in lines) GUILayout.Label(line);
            GUILayout.EndArea();

            if (_view?.ActiveBoss is EntityView boss) DrawBossBar(boss);
        }

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
            var bar = new Rect(left + (right - left - width) / 2, top, width, 18);
            GUI.Label(new Rect(bar.x, bar.y - 2, width, 22), $"{boss.Boss.Name} · {boss.Phase}/{boss.Boss.PhaseCount}페이즈");
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(bar.x, bar.y + 20, width, bar.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.25f, 0.2f);
            float share = boss.MaxHealth > 0 ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0;
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 22, (width - 4) * share, bar.height - 4), Texture2D.whiteTexture);
            GUI.color = previous;
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
