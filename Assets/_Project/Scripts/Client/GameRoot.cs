using System;
using System.Collections.Generic;
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

        Mode _mode = Mode.Menu;
        string _playerName = "플레이어";
        string _address = "127.0.0.1";
        string _status = "";
        LootData _loot;
        MonsterData _monsters;
        BossData _bosses;
        WorldHost _host;
        WorldClient _client;
        NetLink _net;
        WorldView _view;
        InventoryWindow _inventory;
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
            _camera.orthographicSize = 7;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

            var sheets = GameSheets.Load();
            if (sheets == null)
            {
                _status = "데이터가 없습니다. 에디터에서 Game > Set Up Main Scene을 실행하세요.";
                return;
            }
            _loot = sheets.LoadLoot();
            _monsters = sheets.LoadMonsters();
            _bosses = sheets.LoadBosses(_monsters);
            var errors = new List<string>(_loot.Errors);
            errors.AddRange(_monsters.Errors);
            errors.AddRange(_bosses.Errors);
            if (errors.Count > 0) _status = "데이터 시트 오류:\n" + string.Join("\n", errors);
        }

        void Update()
        {
            if (_endReason != null)
            {
                EndSession(_endReason);
                return;
            }
            if (_client == null) return;
            if (Controls.LeavePressed())
            {
                EndSession("");
                return;
            }
            if (_client.Status == ClientStatus.Refused)
            {
                EndSession("참가 거절: " + _client.RefusedReason);
                return;
            }
            if (Controls.BagPressed()) _inventory.Open = !_inventory.Open;

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
            _client.AdvanceRenderClock(Time.deltaTime);
            _view.Render(_tickClock / World.TickSeconds, Time.deltaTime);

            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                var (text, age) = _acquired[i];
                if (age + Time.deltaTime > AcquiredSeconds) _acquired.RemoveAt(i);
                else _acquired[i] = (text, age + Time.deltaTime);
            }
            foreach (string line in _client.TakeAcquired()) _acquired.Add((line, 0));
        }

        void FixedTick()
        {
            var (move, aim, attack) = Controls.Read(_camera, _client.Position);
            // Clicking in the bag should not swing the sword.
            if (_inventory.Open) attack = false;
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

        void StartSolo() => StartHosting(Mode.Solo);

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
            _host = new WorldHost(seed, _monsters, _loot, bosses: _bosses);
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot);
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
            _client = new WorldClient(Guid.NewGuid().ToString(), _playerName, _monsters, _bosses, _loot);
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
            _net?.Dispose();
            _view = null;
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
            if (_client == null)
            {
                DrawMenu();
                return;
            }
            _view?.DrawGui();
            DrawHud();
            _inventory?.Draw();
            for (int i = 0; i < _acquired.Count; i++)
            {
                var (text, age) = _acquired[i];
                var previous = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((AcquiredSeconds - age) / 1f));
                GUI.Label(new Rect(14, Screen.height - 40 - 22 * (_acquired.Count - 1 - i), 400, 22), text);
                GUI.color = previous;
            }
        }

        void DrawMenu()
        {
            var area = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 150, 360, 300);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("pray-to (M3b 프로토타입)");
            GUILayout.Space(8);
            GUILayout.Label("이름");
            _playerName = GUILayout.TextField(_playerName, 16);
            GUILayout.Space(8);

            bool ready = _loot != null && _loot.Success && _monsters != null && _monsters.Success && _bosses != null && _bosses.Success;
            GUI.enabled = ready;
            if (GUILayout.Button("혼자 하기")) StartSolo();
            if (GUILayout.Button($"호스트 (포트 {NetLink.DefaultPort})")) StartHosting(Mode.Host);
            GUILayout.Space(8);
            GUILayout.Label("호스트 주소");
            _address = GUILayout.TextField(_address, 64);
            if (GUILayout.Button("참가")) StartJoining();
            GUI.enabled = true;

            if (_status.Length > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(_status);
            }
            GUILayout.EndArea();
        }

        void DrawHud()
        {
            GUILayout.BeginArea(new Rect(10, 10, 340, 160), GUI.skin.box);
            if (_client.Status != ClientStatus.Playing)
            {
                GUILayout.Label(_status);
            }
            else
            {
                GUILayout.Label($"생명력 {Mathf.CeilToInt(_client.Health)} / {Mathf.CeilToInt(_client.MaxHealth)}");
                if (_client.IsDead) GUILayout.Label("쓰러졌습니다. 잠시 후 시작 지점에서 일어납니다.");
                string mode = _mode == Mode.Host ? $"호스트 중 (포트 {NetLink.DefaultPort})" : _mode == Mode.Join ? "참가 중" : "혼자 하기";
                GUILayout.Label($"{mode} · 접속 {_client.Names.Count}명");
                var toArena = _client.Arena - _client.Position;
                if (toArena.Length > _client.ArenaRadius) GUILayout.Label($"보스 아레나: {Compass(toArena)}쪽 {Mathf.RoundToInt(toArena.Length)}칸");
            }
            GUILayout.Label("WASD 이동 · 마우스 조준 · 왼쪽 클릭/스페이스 공격 · Tab 가방 · Esc 메뉴");
            GUILayout.EndArea();

            if (_view?.ActiveBoss is EntityView boss) DrawBossBar(boss);
        }

        static void DrawBossBar(EntityView boss)
        {
            float width = Mathf.Min(600, Screen.width - 40);
            var bar = new Rect((Screen.width - width) / 2, 20, width, 18);
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
