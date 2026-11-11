using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Where a player has got to, kept on this device only, and a button that writes it into a feedback form (Docs/
    // Release.md 의견 단추, 2026-11-11, the research report's candidate 13). The first time each step is reached, the
    // play time then is noted: loading, the first move, dig, made thing, kill, boss warning and boss felled. Nothing is
    // sent anywhere; [의견 보내기] opens a GitHub issue form with these filled in, which the player reads and sends
    // themselves. Choosing an analytics service (and telling players of it) is left to a person.
    static class Feedback
    {
        const string StepsKey = "feedback.steps";
        const string PlayedKey = "feedback.played";
        const float SaveSeconds = 10;
        const string IssueUrl = "https://github.com/LeArc6910/pray-to/issues/new";

        public static readonly (string Id, string Name)[] Steps =
        {
            ("load", "로드"), ("move", "첫 이동"), ("dig", "첫 채굴"), ("craft", "첫 제작"), ("kill", "첫 처치"),
            ("boss_warning", "첫 보스 전조"), ("boss", "첫 보스 처치"),
        };

        static Dictionary<string, int> _reached;
        static float _played = -1;
        static float _sinceSave;

        static Dictionary<string, int> Reached => _reached ??= Prefs.Get(StepsKey, "").Split(',')
            .Select(p => p.Split(':')).Where(p => p.Length == 2 && int.TryParse(p[1], out _))
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => int.Parse(g.First()[1]));

        // Seconds played on this device, all worlds together.
        public static float Played
        {
            get
            {
                if (_played < 0) _played = Prefs.Get(PlayedKey, 0);
                return _played;
            }
        }

        // Counts play time while in a world; saved now and then.
        public static void Tick(float seconds)
        {
            _played = Played + seconds;
            _sinceSave += seconds;
            if (_sinceSave < SaveSeconds) return;
            _sinceSave = 0;
            Prefs.Set(PlayedKey, (int)_played);
        }

        // Notes the step the first time it is reached.
        public static void Reach(string id)
        {
            if (Reached.ContainsKey(id)) return;
            Reached[id] = (int)Played;
            Prefs.Set(StepsKey, string.Join(",", Reached.Select(p => $"{p.Key}:{p.Value}")));
            Prefs.Set(PlayedKey, (int)Played);
        }

        public static bool Has(string id) => Reached.ContainsKey(id);

        static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        // "로드 0:00 · 첫 이동 0:04 · …", the steps reached so far.
        public static string Summary => string.Join(" · ", Steps.Where(s => Reached.ContainsKey(s.Id)).Select(s => $"{s.Name} {Clock(Reached[s.Id])}"));

        static string Device => Application.platform == RuntimePlatform.WebGLPlayer ? (Controls.UsingTouch ? "웹(터치)" : "웹")
            : Application.isMobilePlatform ? "휴대폰" : "PC";

        // The issue form, filled in with the version, the device, the play time and the steps.
        public static string FormUrl()
        {
            string body = "## 무엇이 좋았거나 아쉬웠나요?\n\n\n\n## 자동으로 붙은 정보\n\n" +
                          $"- 버전: {Protocol.Version}\n- 기기: {Device}\n- 플레이 시간: {Clock((int)Played)}\n- 단계: {Summary}\n";
            return $"{IssueUrl}?title={Uri.EscapeDataString("[의견] ")}&body={Uri.EscapeDataString(body)}";
        }
    }
}
