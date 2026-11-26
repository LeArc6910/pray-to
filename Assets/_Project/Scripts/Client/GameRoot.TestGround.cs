using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The test ground (Docs/TestGround.md, the owner's request of 2026-11-26: "보스방으로 가는 길도 너무 멀고, 장비 획득도
    // 너무 오래 걸려"): the world list's [시험장] row starts the picked character, a copy of it, alone in a world made from
    // TestGround.Seed. Nothing of it is written (GameRoot.Save, the device's per-character settings, the play steps), the
    // panel's commands are refused anywhere else (Playtest.TestGround.cs), and the hub's [시험판], its key and the log on
    // the screen exist only while it is played.
    public sealed partial class GameRoot
    {
        bool _testGround;

        // Whether the game being played is the test ground (its host is here: it is only ever solo).
        internal bool InTestGround => _testGround && _client != null && _host != null && _mode == Mode.Solo;

        // From the world list: the picked character in the test ground, alone.
        void StartTestGround()
        {
            if (_loader != null || _client != null) return;
            _testGround = true;
            TestGround.Reset();
            TestGround.Active = true;
            _worldName = TestGround.Name;
            _startSeed = TestGround.Seed;
            NewLook = CharacterName == _newCharacter ? _newCharacterLook : CharacterLook.Default;
            StartSolo();
            // A broken save stops the start with a reason (StartHosting), and the test ground with it.
            if (_loader == null && _client == null) EndTestGround();
        }

        // For the play mode tests: as the row's [시험장 들어가기] would.
        internal void StartTestGroundNow() => StartTestGround();

        void EndTestGround()
        {
            _testGround = false;
            TestGround.Reset();
        }

        // The panel's key (T, or F1) and the hub's [시험판] open it; Esc or its key again shuts it.
        void TestGroundKeys()
        {
            if (!InTestGround || _client.Status != ClientStatus.Playing) return;
            TestPanel.Tick();
            if (TestGround.PanelPressed()) ToggleScreen(GameScreen.Test);
        }

        void DrawTestPanel() => TestPanel.Draw(this, () => _screens.Close());

        // A line in the corner (the "획득" lines), as the panel's answer once it has shut.
        internal void Notice(string text) => _acquired.Add((text, 0));

        // The badge and the hit log under the minimap column, over the world (not under a window or the hub).
        void DrawTestOverlay()
        {
            if (!InTestGround || _client.Status != ClientStatus.Playing) return;
            TestGround.DrawOverlay(HostWorld, HostPlayer, _client.Skills.Count, () => _screens.Show(GameScreen.Test));
        }
    }
}
