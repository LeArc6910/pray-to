using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The story on screen (Docs/Story.md, 2026-11-10): a [말 걸기] over the person of the story in reach and a
    // [웨이포인트] over a waypoint; the talk at the bottom of the screen a line at a time ([다음], then [알겠다], or the answers
    // to pick from where the story asks); the list
    // of waypoints to go to; and the story's line at the top of the goal list, with which way and how far.
    sealed class StoryScreen
    {
        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        // The talk shown: who says each line, and the line on screen; whether its end gives (or finishes) the step.
        List<(string Name, string Text)> _lines;
        int _line;
        bool _tellsHost;
        bool _waypoints;
        // What the player may answer at the end of the talk (story.csv choices, 2026-11-10), or null.
        IReadOnlyList<(string Text, int Step)> _choices;
        // After an answer: who asked and at which step, so their next words follow on as soon as the host moves on.
        CharacterDefinition _answered;
        int _answeredAt = -1;

        // How far up from the bottom of the screen the talk sits, clear of the quick slots and skill keys.
        const float TalkLift = 140;

        public StoryScreen(WorldClient client, Action<byte[]> send)
        {
            _client = client;
            _send = send;
        }

        // Whether a talk or the waypoint list covers the screen (the game keeps running underneath).
        public bool Open => _lines != null || _waypoints;

        public void Close()
        {
            _lines = null;
            _choices = null;
            _waypoints = false;
        }

        // The buttons over the person in reach and the waypoint in reach.
        public void DrawWorldButtons(Camera camera)
        {
            if (Open || _client.Places.Count == 0) return;
            var near = _client.CharacterNear();
            if (near is var (character, at))
            {
                bool turn = _client.CurrentStep?.Who == character;
                string talk = $"{character.Name}{(Final(character.Name) ? "과" : "와")} 말하기";
                if (WorldButton(camera, new Vector3(at.X, at.Y + 1.35f, 0), turn ? "! " + talk : talk)) Talk(character);
                return;
            }
            var waypoint = _client.WaypointNear();
            if (waypoint != null && WorldButton(camera, new Vector3(waypoint.Center.X, waypoint.Center.Y + 1f, 0), "웨이포인트"))
            {
                _waypoints = true;
                Sounds.Play(Sound.Click);
            }
        }

        // Whether the name ends in a closed Hangul syllable (세라핀 → 과, 바르도 → 와).
        static bool Final(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            char last = name[name.Length - 1];
            return last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
        }

        static bool WorldButton(Camera camera, Vector3 world, string text)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z < 0) return false;
            Vector2 point = Ui.FromScreen(screen);
            float width = Mathf.Max(120, GUI.skin.button.CalcSize(new GUIContent(text)).x + 16);
            var rect = new Rect(point.x - width / 2, point.y - 14, width, 28);
            Controls.Block(rect);
            return Ui.Button(rect, text);
        }

        // What talking to them says: the step's lines from its giver, a reminder of the step once taken, or else a line
        // of their own.
        void Talk(CharacterDefinition character)
        {
            Sounds.Play(Sound.Click);
            var step = _client.CurrentStep;
            _line = 0;
            _tellsHost = false;
            _choices = null;
            if (step != null && step.Who == character && step.Choices.Count > 0)
            {
                // A question: its words (just the last once heard), then the answers to pick from.
                _lines = (_client.JourneyTaken ? step.Lines.Skip(step.Lines.Count - 1) : step.Lines).Select(l => (l.Speaker.Name, l.Text)).ToList();
                _choices = step.Choices;
                return;
            }
            if (step != null && step.Who == character)
            {
                if (!_client.JourneyTaken)
                {
                    _lines = step.Lines.Select(l => (l.Speaker.Name, l.Text)).ToList();
                    _tellsHost = true;
                }
                else if (step.Goal == StoryGoal.Have && Held(step) >= step.Count)
                {
                    _lines = new List<(string, string)> { (character.Name, "가져왔군. 고맙다.") };
                    _tellsHost = true;
                }
                else _lines = new List<(string, string)> { (character.Name, step.Lines[step.Lines.Count - 1].Text), ("", Doing(step)) };
                return;
            }
            _lines = new List<(string, string)> { (character.Name, character.LineFor(_client.Today)) };
        }

        int Held(StoryStep step) => step.Material != null ? _client.Bag.CountOf(step.Material) : _client.Bag.CountOf(step.Consumable);

        // The talk and the waypoint list, over everything else.
        public void DrawPanels()
        {
            // Once the host has taken the answer, the one who asked says what follows.
            if (_answered != null && _client.JourneyStep != _answeredAt)
            {
                var asked = _answered;
                _answered = null;
                if (_client.CurrentStep?.Who == asked) Talk(asked);
            }
            if (_lines != null) DrawTalk();
            else if (_waypoints) DrawWaypoints();
        }

        void DrawTalk()
        {
            float width = Mathf.Min(560, Ui.Width - 24);
            var label = GUI.skin.label;
            var (name, text) = _lines[_line];
            string wrapped = Ui.Wrap(text, label, width - 32);
            float textHeight = label.CalcHeight(new GUIContent(wrapped), width - 32);
            bool asking = _choices != null && _line >= _lines.Count - 1;
            const float ChoiceRow = 38;
            float height = textHeight + 96 + (asking ? _choices.Count * ChoiceRow : 0);
            // Above the quick slots and skill keys (2026-11-10: at the very bottom it covered them), as low as fits.
            var panel = new Rect((Ui.Width - width) / 2, Mathf.Max(8, Ui.Height - height - TalkLift), width, height);
            Controls.Block(panel);
            GUI.Box(panel, GUIContent.none);
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(panel.x + 16, panel.y + 10, width - 32, 24), name.Length > 0 ? name : "할 일");
            GUI.color = previous;
            GUI.Label(new Rect(panel.x + 16, panel.y + 38, width - 32, textHeight), wrapped);
            if (asking)
            {
                // The answers, one under another below the words; picking one tells the host.
                float y = panel.y + 44 + textHeight;
                for (int i = 0; i < _choices.Count; i++)
                {
                    if (Ui.Button(new Rect(panel.x + 16, y + i * ChoiceRow, width - 32, ChoiceRow - 6), _choices[i].Text))
                    {
                        Sounds.Play(Sound.Click);
                        _send(_client.ChooseStoryCommand(i));
                        _answered = _client.CurrentStep?.Who;
                        _answeredAt = _client.JourneyStep;
                        _lines = null;
                        _choices = null;
                        return;
                    }
                }
                GUI.Label(new Rect(panel.x + 16, panel.yMax - 36, 120, 24), $"{_line + 1}/{_lines.Count}");
                return;
            }
            bool last = _line >= _lines.Count - 1;
            string next = last ? (_tellsHost ? "알겠다" : "닫기") : "다음";
            if (Ui.Button(new Rect(panel.xMax - 116, panel.yMax - 40, 100, 30), next))
            {
                Sounds.Play(Sound.Click);
                if (!last)
                {
                    _line++;
                    return;
                }
                if (_tellsHost) _send(_client.TalkStoryCommand());
                _lines = null;
            }
            GUI.Label(new Rect(panel.x + 16, panel.yMax - 36, 120, 24), $"{_line + 1}/{_lines?.Count ?? 0}");
        }

        void DrawWaypoints()
        {
            var places = _client.Story?.Places.Where(p => p.Waypoint).ToList() ?? new List<PlaceDefinition>();
            float width = Mathf.Min(420, Ui.Width - 24);
            float row = 34;
            float height = 70 + row * places.Count + 40;
            var panel = new Rect((Ui.Width - width) / 2, Mathf.Max(8, (Ui.Height - height) / 2), width, Mathf.Min(height, Ui.Height - 16));
            Controls.Block(panel);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 16, panel.y + 10, width - 32, 24), "웨이포인트: 가 본 곳으로 간다");
            var here = _client.WaypointNear()?.Definition;
            float y = panel.y + 44;
            foreach (var place in places)
            {
                bool open = _client.Waypoints.Contains(place.Index);
                string text = place == here ? $"{place.Name} (여기)" : open ? place.Name : $"{place.Name} — 아직 가 보지 않았다";
                GUI.enabled = open && place != here && here != null;
                if (Ui.Button(new Rect(panel.x + 16, y, width - 32, row - 4), text))
                {
                    _send(_client.TeleportCommand(place));
                    _waypoints = false;
                }
                GUI.enabled = true;
                y += row;
            }
            if (Ui.Button(new Rect(panel.xMax - 116, panel.yMax - 40, 100, 30), "닫기")) _waypoints = false;
            // Walked away from the waypoint: the list goes.
            if (here == null) _waypoints = false;
        }

        // The goal list's line for the story: its chapter and title, then what to do and which way.
        public (string Title, string Text)? TrackerEntry()
        {
            var step = _client.CurrentStep;
            if (step == null) return null;
            string title = string.IsNullOrEmpty(step.Chapter) ? $"이야기: {step.Title}" : $"{step.Chapter}: {step.Title}";
            string text = _client.JourneyTaken ? Doing(step) : $"{step.Who.Name}에게 말 걸기";
            var site = _client.SiteOf(_client.JourneyTaken ? step.Target : step.Who.Place);
            if (site != null && (step.Goal == StoryGoal.Reach || !_client.JourneyTaken || step.Goal == StoryGoal.Have))
                text += " · " + Way(site);
            if (!Ui.Short && !string.IsNullOrEmpty(step.Hint) && _client.JourneyTaken) text += "\n" + step.Hint;
            return (title, text);
        }

        // What the taken step asks now.
        string Doing(StoryStep step) => step.Goal switch
        {
            StoryGoal.Reach => $"{step.Place.Name}에 가기",
            StoryGoal.Boss => $"{_client.BossName(step.Boss)} 쓰러뜨리기",
            StoryGoal.Have => $"{(step.Material?.Name ?? step.Consumable.Name)} {Math.Min(Held(step), step.Count)}/{step.Count} 모아 {step.Who.Name}에게 가져가기",
            _ => $"{step.Who.Name}에게 말 걸기",
        };

        // "왕도 루미나, 북동 34칸", or the name alone once there.
        string Way(PlaceSite site)
        {
            float dx = site.Center.X - _client.Position.X, dy = site.Center.Y - _client.Position.Y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (site.Contains(_client.Position)) return $"{site.Definition.Name} (여기)";
            return $"{site.Definition.Name}, {Compass(dx, dy)} {Mathf.RoundToInt(distance)}칸";
        }

        // Which of eight ways (y up is north).
        static string Compass(float dx, float dy)
        {
            string[] names = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt((angle + 360) % 360 / 45f) % 8;
            return names[index];
        }
    }
}
