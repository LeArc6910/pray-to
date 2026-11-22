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
        Vector2 _waypointScroll;
        // What the player may answer at the end of the talk (story.csv choices, 2026-11-10), or null.
        IReadOnlyList<(string Text, int Step)> _choices;
        // After an answer: who asked and at which step, so their next words follow on as soon as the host moves on.
        CharacterDefinition _answered;
        int _answeredAt = -1;

        // How far up from the bottom of the screen the talk sits, clear of the quick slots and skill keys.
        const float TalkLift = 140;

        // The map (for what has been seen and the rumours heard, 묶음 4c); null before it is made.
        readonly Func<MapView> _map;

        public StoryScreen(WorldClient client, Action<byte[]> send, Func<MapView> map = null)
        {
            _client = client;
            _send = send;
            _map = map ?? (() => null);
        }

        // The rumour told in the talk on screen, or null (for the playtests).
        public string LastRumor { get; private set; }

        // Whether a talk or the waypoint list covers the screen (the game keeps running underneath).
        public bool Open => _lines != null || _waypoints;

        // Whether the waypoint list is open: nothing that takes a tap is drawn under it then (GameRoot), since in IMGUI
        // a control drawn earlier wins a press over one drawn on top of it.
        public bool ListOpen => _waypoints;

        // Where the waypoint list goes (2026-11-22, the web sweep on a phone held upright: centred, its top rows sat
        // under the minimap and the [가방]·[나가기] buttons, which are drawn later and took the taps): clear of the top
        // right column, beside it where the screen is wide enough, else under it; centred in what is left, as tall as
        // it wants up to that.
        public static Rect ListArea(float width, float wanted)
        {
            float right = Ui.MinimapArea.x - 8, top = 8;
            float x = (Ui.Width - width) / 2;
            if (x + width > right)
            {
                if (right - width >= 8) x = right - width;
                else top = Ui.RightColumnBottom + 8;
            }
            float height = Mathf.Max(80, Mathf.Min(wanted, Ui.Height - top - 8));
            return new Rect(x, top + Mathf.Max(0, (Ui.Height - top - 8 - height) / 2), width, height);
        }

        public void Close()
        {
            _lines = null;
            _choices = null;
            _waypoints = false;
        }

        // The buttons over the person in reach and the waypoint in reach.
        public void DrawWorldButtons(Camera camera)
        {
            if (Open || _client.Riding) return;
            // A waystone touched (3c, World.Travel.cs): its button opens the same list as a place's waypoint.
            var stone = _client.StoneNear();
            if (stone != null && _client.StoneOpen(stone.Index) && WorldButton(camera, new Vector3(stone.Center.X, stone.Center.Y + 1.6f, 0), "길잡이 돌"))
            {
                _waypoints = true;
                Sounds.Play(Sound.Click);
                return;
            }
            if (_client.Places.Count == 0) return;
            var near = _client.CharacterNear();
            if (near is var (character, at))
            {
                bool turn = _client.StepGivenBy(character) != null;
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

        // Opens the waypoint list when the player stands by an open waypoint or waystone (for the playtests).
        public bool OpenWaypoints()
        {
            var place = _client.WaypointNear();
            var stone = _client.StoneNear();
            if (!(place != null && _client.Waypoints.Contains(place.Definition.Index) || stone != null && _client.StoneOpen(stone.Index))) return false;
            _waypoints = true;
            return true;
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

        // What talking to them says: the lines of the step they give (main story first, then a side story), a reminder
        // of it once taken, or else a line of their own. Lines that wait on an earlier answer show only after it.
        void Talk(CharacterDefinition character)
        {
            Sounds.Play(Sound.Click);
            _line = 0;
            _tellsHost = false;
            _choices = null;
            LastRumor = null;
            if (_client.StepGivenBy(character) is (StoryStep step, bool taken))
            {
                var said = step.LinesFor(_client.StoryChoices);
                if (step.Choices.Count > 0)
                {
                    // A question: its words (just the last once heard), then the answers to pick from.
                    _lines = (taken ? said.Skip(said.Count - 1) : said).Select(l => (l.Speaker.Name, l.Text)).ToList();
                    _choices = step.Choices;
                }
                else if (!taken)
                {
                    _lines = said.Select(l => (l.Speaker.Name, l.Text)).ToList();
                    _tellsHost = true;
                }
                else if (step.Goal == StoryGoal.Have && Held(step) >= step.Count)
                {
                    _lines = new List<(string, string)> { (character.Name, "가져왔군. 고맙다.") };
                    _tellsHost = true;
                }
                else _lines = new List<(string, string)> { (character.Name, said[said.Count - 1].Text), ("", Doing(step)) };
                return;
            }
            _lines = new List<(string, string)> { (character.Name, character.LineFor(_client.Today, _client.StoryChoices)) };
            // Then a rumour of something to find not yet known (묶음 4c, characters.csv rumor): a faint ring on the map.
            var map = _map();
            if (map == null) return;
            var targets = Rumors.Targets(_client.Rooms, _client.WandererAt(), _client.Biomes);
            var from = _client.SiteOf(character.Place)?.Center ?? _client.Position;
            // What was heard today counts as not yet known, so asked again today the person says the same.
            int today = _client.Today;
            var told = Rumors.Tell(character, targets, _client.Seed, today, from, t => map.Knows(t) && !map.HeardOn(t, today));
            if (told == null) return;
            var (line, target) = told.Value;
            map.Hear(target, today);
            LastRumor = line;
            _lines.Add((character.Name, line + "\n(소문: 지도에 옅은 원으로 적었다)"));
        }

        // Talks to the person of the story nearest the player, as their button would (for the playtests).
        public bool TalkNear()
        {
            if (!(_client.CharacterNear() is var (character, _))) return false;
            Talk(character);
            return true;
        }

        // The lines of the talk on screen, joined (for the playtests).
        public string TalkText => _lines == null ? null : string.Join(" / ", _lines.Select(l => l.Text));

        int Held(StoryStep step) => step.Material != null ? _client.Bag.CountOf(step.Material) : _client.Bag.CountOf(step.Consumable);

        // The talk and the waypoint list, over everything else.
        public void DrawPanels()
        {
            // Once the host has taken the answer, the one who asked says what follows.
            if (_answered != null && _client.StepGivenBy(_answered)?.Step.Index != _answeredAt)
            {
                var asked = _answered;
                _answered = null;
                if (_client.StepGivenBy(asked) != null) Talk(asked);
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
                        var asker = _client.CurrentSteps.FirstOrDefault(s => s.Step.Choices == _choices).Step;
                        _answered = asker?.Who;
                        _answeredAt = asker?.Index ?? -1;
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

        // The waypoints and the waystones (3c) open to this player, to go to from the one they stand by: a list that
        // scrolls when it is longer than the screen (a finger drags it).
        void DrawWaypoints()
        {
            var places = _client.Story?.Places.Where(p => p.Waypoint).ToList() ?? new List<PlaceDefinition>();
            var stones = _client.Stones.OrderBy(s => s.Biome).ToList();
            float width = Mathf.Min(420, Ui.Width - 24);
            float row = 34;
            var panel = ListArea(width, 70 + row * (places.Count + stones.Count + (stones.Count > 0 ? 1 : 0)) + 50);
            Controls.Block(panel);
            var here = _client.WaypointNear()?.Definition;
            if (here != null && !_client.Waypoints.Contains(here.Index)) here = null;
            var hereStone = _client.StoneNear();
            if (hereStone != null && !_client.StoneOpen(hereStone.Index)) hereStone = null;
            bool by = here != null || hereStone != null;
            Ui.TouchScroll(panel, ref _waypointScroll);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("웨이포인트: 가 본 곳으로 간다", GUILayout.Height(24));
            _waypointScroll = Ui.BeginScrollView(_waypointScroll, GUILayout.ExpandHeight(true));
            foreach (var place in places)
            {
                bool open = _client.Waypoints.Contains(place.Index);
                string text = place == here ? $"{place.Name} (여기)" : open ? place.Name : $"{place.Name} — 아직 가 보지 않았다";
                GUI.enabled = open && place != here && by;
                if (Ui.LayoutButton(text, GUILayout.Height(row - 4)))
                {
                    _send(_client.TeleportCommand(place));
                    _waypoints = false;
                }
                GUI.enabled = true;
            }
            if (stones.Count > 0) GUILayout.Label("길잡이 돌", GUILayout.Height(22));
            foreach (var stone in stones)
            {
                bool open = _client.StoneOpen(stone.Index);
                string name = $"{BiomeMap.Name(stone.Biome)}의 {stone.Definition.Name}";
                string text = stone == hereStone ? $"{name} (여기)" : open ? name : $"{name} — 아직 만지지 않았다";
                GUI.enabled = open && stone != hereStone && by;
                if (Ui.LayoutButton(text, GUILayout.Height(row - 4)))
                {
                    _send(_client.TravelToStoneCommand(stone));
                    _waypoints = false;
                }
                GUI.enabled = true;
            }
            Ui.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (Ui.LayoutButton("닫기", GUILayout.Width(100), GUILayout.Height(30))) _waypoints = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            // Walked away from the waypoint or stone: the list goes.
            if (!by) _waypoints = false;
        }

        // The goal list's lines for the story: the main story's, then each side story taken (one on a short screen),
        // each its chapter and title, then what to do and which way.
        public List<(string Title, string Text)> TrackerEntries()
        {
            var entries = new List<(string, string)>();
            int sides = 0;
            foreach (var (step, taken) in _client.CurrentSteps)
            {
                bool side = step.Chain.Length > 0;
                if (side && (!taken || sides >= (Ui.Short ? 1 : 2))) continue;
                if (side) sides++;
                string title = string.IsNullOrEmpty(step.Chapter) ? $"이야기: {step.Title}" : $"{step.Chapter}: {step.Title}";
                string text = taken ? Doing(step) : $"{step.Who.Name}에게 말 걸기";
                var site = _client.SiteOf(taken ? step.Target : step.Who.Place);
                if (site != null && (step.Goal == StoryGoal.Reach || !taken || step.Goal == StoryGoal.Have))
                    text += " · " + Way(site);
                string hint = Settings.Rumors ? step.Rumor : step.Hint;
                if (!Ui.Short && !side && !string.IsNullOrEmpty(hint) && taken) text += "\n" + hint;
                entries.Add((title, text));
            }
            return entries;
        }

        // What the taken step asks now.
        string Doing(StoryStep step) => step.Goal switch
        {
            StoryGoal.Reach => $"{step.Place.Name}에 가기",
            StoryGoal.Boss => $"{_client.BossName(step.Boss)} 쓰러뜨리기",
            StoryGoal.Have => $"{(step.Material?.Name ?? step.Consumable.Name)} {Math.Min(Held(step), step.Count)}/{step.Count} 모아 {step.Who.Name}에게 가져가기",
            _ => $"{step.Who.Name}에게 말 걸기",
        };

        // "왕도 루미나, 북동 34칸", or the name alone once there. With rumours (묶음 4c, MapView.KnowledgeOf) "북동쪽"
        // once the way is known, and "북동쪽, 버섯 숲 어딘가" before.
        string Way(PlaceSite site)
        {
            // In a dungeon, from its gate out in the world (묶음 5b: the pocket's rows lie past the world's edge).
            var from = _client.WorldPosition;
            if (site.Contains(from)) return $"{site.Definition.Name} (여기)";
            var known = _map()?.KnowledgeOf(site) ?? WayKnown.Known;
            var here = _client.Biomes[TileMap.Cell(from.X), TileMap.Cell(from.Y)];
            var there = _client.Biomes[TileMap.Cell(site.Center.X), TileMap.Cell(site.Center.Y)];
            string way = $"{site.Definition.Name}, {Rumors.Way(known, from, site.Center, there, here)}";
            return _client.DungeonHere != null ? way + " (던전 문에서)" : way;
        }
    }
}
