using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Dungeons as the screen shows them (Docs/WorldRework.md 5단계 지도와 화면, 묶음 5a): going in, the view goes dark and
    // comes up on the dungeon with its name large in a band across the middle; coming out, the same dark and a line.
    // A slot of the pocket laid or cleared is drawn again (RelaySlots). The gates are the gate rooms' torii; the doors out
    // of a dungeon are the same posts with a rune between them (DungeonPocket.Carve).
    partial class WorldView
    {
        // How long the dark of going in or out takes to lift, and how long the name stays.
        const float DungeonFadeSeconds = 0.7f;
        const float DungeonCardSeconds = 3.2f;
        static readonly Color GateGlow = new Color(0.7f, 0.55f, 1f);

        float _dungeonFade;
        float _dungeonCardAge = float.MaxValue;
        string _dungeonCardName = "";
        string _dungeonCardLine = "";
        GUIStyle _dungeonName, _dungeonLine;

        // For the playtests: how many name cards and darks have shown, and the last name.
        public int DungeonCardsShown { get; private set; }
        public string DungeonCardName => _dungeonCardName;
        public bool DungeonCardShowing => _dungeonCardAge < DungeonCardSeconds;

        // A slot of the pocket laid or cleared (WorldClient.TakeRebuiltSlots): its chunks on screen are laid again.
        public void RelaySlots(IEnumerable<int> slots)
        {
            var map = _client.Map;
            foreach (int slot in slots)
            {
                var (left, bottom, w, h) = DungeonPocket.Box(map, slot);
                foreach (var chunk in new List<(int X, int Y)>(_chunks))
                {
                    int x0 = chunk.X * ChunkSize, y0 = chunk.Y * ChunkSize;
                    if (x0 + ChunkSize <= left || x0 >= left + w || y0 + ChunkSize <= bottom || y0 >= bottom + h) continue;
                    LayChunk(chunk.X, chunk.Y, true);
                }
            }
        }

        // What the screen does for a Dungeon event. True when it was one.
        bool ShowDungeon(SimEvent e)
        {
            if (e.Type != SimEventType.Dungeon) return false;
            bool mine = e.Source == _client.PlayerId;
            var at = new Vector3(e.Position.X, e.Position.Y, 0);
            switch (e.Amount)
            {
                case World.DungeonEntering:
                case World.DungeonLeaving:
                    Hear(Sound.Cast, at, mine ? 1 : 0.5f);
                    AddFlash(at + new Vector3(0, 0.5f, 0), 1.6f, GateGlow, FlashKind.Ring);
                    break;
                case World.DungeonLeavingMenu:
                    Hear(Sound.Cast, at, mine ? 1 : 0.5f);
                    if (mine) ShowBanner($"{World.MenuLeaveSeconds:0}초 뒤 던전 밖 문 앞으로 나간다 (맞으면 멈춘다)");
                    break;
                case World.DungeonEntered:
                    Hear(Sound.Story, at, mine ? 1 : 0.5f);
                    AddFlash(at + new Vector3(0, 0.5f, 0), 2f, GateGlow, FlashKind.Burst);
                    if (!mine) break;
                    _dungeonFade = DungeonFadeSeconds;
                    var dungeon = _client.DungeonAt(e.Position);
                    if (dungeon == null) break;
                    int minutes = dungeon.Definition?.Minutes ?? 0;
                    ShowDungeonCard(dungeon.Name, $"레벨 {dungeon.Level}" + (minutes > 0 ? $" · 약 {minutes}분" : "") + " · 쓰러지면 입구에서 다시 일어난다");
                    break;
                case World.DungeonLeft:
                    Hear(Sound.Door, at, mine ? 1 : 0.5f);
                    AddFlash(at + new Vector3(0, 0.5f, 0), 1.6f, GateGlow, FlashKind.Burst);
                    if (!mine) break;
                    _dungeonFade = DungeonFadeSeconds;
                    _dungeonCardAge = float.MaxValue;
                    ShowBanner("던전에서 나왔다");
                    break;
                case World.DungeonRose:
                    if (!mine) break;
                    _dungeonFade = DungeonFadeSeconds;
                    ShowBanner("던전 입구에서 다시 일어났다");
                    break;
                case World.DungeonBroken:
                    if (mine) ShowBanner("멈췄다: 맞거나 문에서 벗어나면 다시 해야 한다");
                    break;
                // B1c: a camp's band fell and its trapdoor opened.
                case World.DungeonCellarOpened:
                    Hear(Sound.Door, at, 1);
                    AddFlash(at, 2f, EliteKeyGold, FlashKind.Ring);
                    if (Vector3.Distance(at, _self.Position) < 24) ShowBanner("야영지의 뚜껑 문이 열렸다: 지하실로 내려갈 수 있다");
                    break;
                case World.DungeonFull:
                    if (mine) ShowBanner("던전 자리가 모두 찼다: 다른 던전이 빌 때까지 기다려야 한다");
                    break;
                // 묶음 5b: the run's turns, told to everyone in the dungeon (the host sends a dungeon's events to its part only).
                case World.DungeonKey:
                    Hear(Sound.LevelUp, at, 1);
                    AddFlash(at, 2.4f, EliteKeyGold, FlashKind.Burst);
                    ShowBanner("열쇠를 얻었다: 잠긴 방의 창살 앞에 서면 열린다");
                    break;
                case World.DungeonDoorOpened:
                    Hear(Sound.Door, at, 1);
                    AddFlash(at, 2f, GateGlow, FlashKind.Ring);
                    break;
                case World.DungeonBossWoke:
                {
                    Hear(Sound.Roar, at, 1);
                    var woke = _client.DungeonAt(e.Position);
                    string master = BodyName(e.Source);
                    ShowDungeonCard(master ?? "던전의 주인", $"{woke?.Name ?? "던전"}의 주인이 깨어났다 · 크게 휘두르기 전에 비켜서라");
                    break;
                }
                case World.DungeonBossDown:
                    Hear(Sound.Victory, at, 1);
                    AddFlash(at, 3f, EliteKeyGold, FlashKind.Burst);
                    ShowDungeonCard("주인을 쓰러뜨렸다", "끝 방의 창살이 열렸다 · 보상 상자를 열자");
                    break;
                case World.DungeonRoomCleared:
                {
                    var run = _client.DungeonAt(e.Position);
                    if (run == null) break;
                    int packs = 0, cleared = 0;
                    foreach (var room in run.Layout.Nodes)
                    {
                        if (!DungeonSheet.HasPack(room.Letter)) continue;
                        packs++;
                        if (run.RoomCleared(room.Index)) cleared++;
                    }
                    ShowBanner($"방을 비웠다 ({cleared}/{packs})");
                    break;
                }
                case World.DungeonShortcutOpened:
                    Hear(Sound.Cast, at, 1);
                    AddFlash(at, 2f, GateGlow, FlashKind.Ring);
                    ShowBanner("지름길이 열렸다: 입구의 푸른 돌에 닿으면 이 쉼터로 온다");
                    break;
                case World.DungeonShortcutTaken:
                    Hear(Sound.Blink, at, mine ? 1 : 0.5f);
                    AddFlash(at + new Vector3(0, 0.5f, 0), 1.8f, GateGlow, FlashKind.Burst);
                    if (mine) _dungeonFade = DungeonFadeSeconds * 0.6f;
                    break;
                case World.DungeonUsed:
                    Hear(Sound.Door, at, mine ? 1 : 0.6f);
                    AddFlash(at, 1.6f, EliteKeyGold, FlashKind.Burst);
                    break;
                case World.DungeonCleared:
                {
                    Hear(Sound.Victory, at, 1);
                    var done = _client.DungeonAt(e.Position);
                    if (done == null) break;
                    string line = $"{done.ClearSeconds / 60}분 {done.ClearSeconds % 60}초 · 쓰러짐 {done.Deaths}";
                    if (done.Has(DungeonFlags.BestTime)) line += " · 가장 빠른 기록";
                    if (done.Has(DungeonFlags.FirstToday)) line += " · 오늘 첫 완주: 희귀 장비와 룬 하나 더";
                    ShowDungeonCard($"{done.Name} 완주!", line + " · 끝 방의 문으로 나간다");
                    ClearsShown++;
                    break;
                }
            }
            return true;
        }

        static readonly Color EliteKeyGold = new Color(1f, 0.82f, 0.35f);

        // For the playtests: how many clear cards have shown.
        public int ClearsShown { get; private set; }
        public string DungeonCardLine => _dungeonCardLine;

        // The name of a body on screen (an elite's as its tag shows it), or null.
        string BodyName(int id)
        {
            foreach (var other in _client.Others())
            {
                if (other.Id != id) continue;
                var kind = other.Monster;
                return World.EliteName(kind, other.Elite, other.Affixes) ?? kind?.Name;
            }
            return null;
        }

        // The band across the middle: a name large and a line under it.
        void ShowDungeonCard(string name, string line)
        {
            _dungeonCardName = name;
            _dungeonCardLine = line;
            _dungeonCardAge = 0;
            DungeonCardsShown++;
        }

        // Over everything but the HUD's buttons: the dark lifting, then the dungeon's name in a band.
        void DrawDungeonCard()
        {
            float delta = Time.unscaledDeltaTime;
            if (Event.current.type == EventType.Repaint)
            {
                _dungeonFade = Mathf.Max(0, _dungeonFade - delta);
                if (_dungeonCardAge < DungeonCardSeconds) _dungeonCardAge += delta;
            }
            var before = GUI.color;
            if (_dungeonFade > 0)
            {
                GUI.color = new Color(0, 0, 0, _dungeonFade / DungeonFadeSeconds);
                GUI.DrawTexture(new Rect(0, 0, Ui.Width, Ui.Height), Texture2D.whiteTexture);
            }
            if (_dungeonCardAge >= DungeonCardSeconds)
            {
                GUI.color = before;
                return;
            }
            _dungeonName ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _dungeonLine ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            float room = Ui.Width - 24;
            int nameSize = 34;
            foreach (int size in new[] { 34, 28, 24, 20, 16 })
            {
                nameSize = size;
                Ui.Text(_dungeonName, size, bold: true);
                if (_dungeonName.CalcSize(new GUIContent(_dungeonCardName)).x <= room) break;
            }
            Ui.Text(_dungeonLine, 14, bold: false);
            string line = Ui.Wrap(_dungeonCardLine, _dungeonLine, room);
            float lineHeight = _dungeonLine.CalcHeight(new GUIContent(line), room);
            float nameHeight = nameSize * 1.35f, height = nameHeight + lineHeight + 18;
            float fade = Mathf.Clamp01(Mathf.Min(_dungeonCardAge / 0.3f, (DungeonCardSeconds - _dungeonCardAge) / 0.5f));
            float top = Mathf.Max(64, Ui.Height * 0.3f);
            GUI.color = new Color(0.04f, 0.02f, 0.08f, 0.66f * fade);
            GUI.DrawTexture(new Rect(0, top, Ui.Width, height), Texture2D.whiteTexture);
            GUI.color = new Color(GateGlow.r, GateGlow.g, GateGlow.b, 0.85f * fade);
            GUI.DrawTexture(new Rect(0, top, Ui.Width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, top + height - 1, Ui.Width, 1), Texture2D.whiteTexture);
            GUI.color = before;
            Shadowed(new Rect(0, top + 9, Ui.Width, nameHeight), _dungeonCardName, new Color(0.88f, 0.8f, 1f, fade), _dungeonName);
            Shadowed(new Rect(12, top + 9 + nameHeight, room, lineHeight), line, new Color(0.92f, 0.9f, 0.86f, fade), _dungeonLine);
        }

        // The card's box on the screen while it shows (zero when none), for the playtests' layout checks.
        public Rect DungeonCardRect => !DungeonCardShowing ? default : new Rect(0, Mathf.Max(64, Ui.Height * 0.3f), Ui.Width, 80);
    }
}
