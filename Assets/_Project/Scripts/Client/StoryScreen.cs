using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The story on screen (Docs/Story.md, 2026-11-10): a [○○와 말하기] over the person of the story in reach and over a
    // stall keeper in reach (U3), a [웨이포인트] over a waypoint; the talk at the bottom of the screen a line at a time
    // ([다음], then [알겠다], or the answers to pick from where the story asks; a stall keeper's [거래]·[부탁]·[선물]·
    // [이야기]·[말 걸기]); the list of waypoints to go to; and the story's line at the top of the goal list, with which way
    // and how far. The talk (Docs/UiRework.md 4-2, U3) is a box at the bottom with the speaker's portrait (PortraitArt, a
    // face chosen for each line), a band in their colour with their name and hearts, and the words typed out.
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
        // The stall keeper talked to (U3), whose choices open her shop; null in a talk of the story.
        NpcDefinition _merchant;
        // When the line on screen began to be typed out, and whether a tap (or a key) showed the rest at once.
        float _lineAt;
        bool _revealed;

        // Opens the shop of the stall keeper in reach on one of its tabs (사기, 선물, 부탁, 이야기): GameRoot's.
        public Action<string> OpenShop { get; set; }

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

        // Whether a talk is on screen: the skill keys and quick slots are not drawn meanwhile (4-2), and G, Space and
        // Enter go to it (Advance).
        public bool TalkOpen => _lines != null;

        // Whether the line on screen is still being typed out (for the playtests' report).
        public bool Typing { get; private set; }

        // The portrait on screen: the person's id and the face of the line ("" without one), for the playtests.
        public string PortraitShown { get; private set; } = "";

        // Where the talk box was drawn in the last repaint (zero when none), for the playtests.
        public static Rect TalkShown { get; private set; }

        // Which line of how many is on screen ("2/5"; "" without a talk), for the playtests.
        public string LineShown => _lines == null ? "" : $"{_line + 1}/{_lines.Count}";

        // The stall keeper whose talk is on screen, or null.
        public NpcDefinition Merchant => _lines != null ? _merchant : null;

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
            _merchant = null;
            _waypoints = false;
            TalkShown = default;
            Typing = false;
            PortraitShown = "";
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
            // A stall keeper in reach (U3, Docs/UiRework.md 4-2: everyone has the button, the "가방에서 거래" note is gone).
            if (_client.NpcNear() is var (npc, stallX, stallY) && WorldButton(camera, new Vector3(stallX + 0.5f, stallY + 2.75f, 0), TalkLabel(npc.Name) + MerchantKeyHint))
            {
                TalkMerchant(npc);
                return;
            }
            if (_client.Places.Count == 0) return;
            var near = _client.CharacterNear();
            if (near is var (character, at))
            {
                bool turn = _client.StepGivenBy(character) != null;
                string talk = TalkLabel(character.Name);
                if (WorldButton(camera, new Vector3(at.X, at.Y + 1.35f, 0), (turn ? "! " + talk : talk) + KeyHint)) Talk(character);
                return;
            }
            var waypoint = _client.WaypointNear();
            if (waypoint != null && WorldButton(camera, new Vector3(waypoint.Center.X, waypoint.Center.Y + 1f, 0), "웨이포인트"))
            {
                _waypoints = true;
                Sounds.Play(Sound.Click);
            }
        }

        // Opens the waypoint list when the player stands by an open waypoint or waystone (G, and the playtests).
        public bool OpenWaypoints()
        {
            if (!CanOpenWaypoints) return false;
            _waypoints = true;
            return true;
        }

        public bool CanOpenWaypoints
        {
            get
            {
                var place = _client.WaypointNear();
                var stone = _client.StoneNear();
                return place != null && _client.Waypoints.Contains(place.Definition.Index) || stone != null && _client.StoneOpen(stone.Index);
            }
        }

        // What follows the name on the button over a person: " (G)" on a PC while G would talk to them (GameRoot.Meet).
        public string KeyHint { get; set; } = "";

        // The same over a stall keeper.
        public string MerchantKeyHint { get; set; } = "";

        // "촌장 바르도와 말하기", "상인 도라와 말하기".
        static string TalkLabel(string name) => $"{name}{(Final(name) ? "과" : "와")} 말하기";

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
            float width = Mathf.Max(120, GUI.skin.button.CalcSize(new GUIContent(text)).x + 16);
            return Ui.WorldButton(Ui.FromScreen(screen), text, width);
        }

        // What talking to them says: the lines of the step they give (main story first, then a side story), a reminder
        // of it once taken, or else a line of their own. Lines that wait on an earlier answer show only after it.
        void Talk(CharacterDefinition character)
        {
            Sounds.Play(Sound.Click);
            _line = 0;
            _tellsHost = false;
            _choices = null;
            _merchant = null;
            StartLine();
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

        // A stall keeper's talk (U3, Docs/UiRework.md 4-2): today's line, then what to do with her: [거래] her shop,
        // [부탁] today's request, [선물] a gift, [이야기] her story, [말 걸기] the day's chat (once a day, a little liking),
        // [닫기].
        void TalkMerchant(NpcDefinition npc)
        {
            Sounds.Play(Sound.Click);
            _merchant = npc;
            _line = 0;
            _tellsHost = false;
            _choices = null;
            LastRumor = null;
            _lines = new List<(string, string)> { (npc.Name, npc.LineFor(_client.Today, _client.HeartsWith(npc))) };
            StartLine();
        }

        // Talks to the stall keeper in reach, as G or her button would. False when none is in reach.
        public bool TalkMerchantNear()
        {
            if (!(_client.NpcNear() is var (npc, _, _))) return false;
            TalkMerchant(npc);
            return true;
        }

        void StartLine()
        {
            _lineAt = Time.unscaledTime;
            _revealed = false;
        }

        // A tap on the talk, G, Space or Enter: the rest of the line at once while it is being typed, else the next line
        // (as [다음]); the last line's [알겠다]·[닫기] and the answers wait for their own buttons.
        public void Advance()
        {
            if (_lines == null) return;
            if (Typing)
            {
                _revealed = true;
                Typing = false;
                return;
            }
            if (_merchant != null || _choices != null && _line >= _lines.Count - 1) return;
            if (_line < _lines.Count - 1)
            {
                Sounds.Play(Sound.Click);
                _line++;
                StartLine();
            }
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
            if (Event.current.type == EventType.Repaint)
            {
                TalkShown = default;
                if (_lines == null)
                {
                    Typing = false;
                    PortraitShown = "";
                }
            }
            if (_lines != null) DrawTalk();
            else if (_waypoints) DrawWaypoints();
        }

        // ---- The talk box (Docs/UiRework.md 4-2 대화 화면, U3) ----

        // The words come a letter at a time (35 ms), with a breath after a stop (200 ms); at once in the playtests and with
        // less motion (UiMotion.Reduced).
        const float LetterSeconds = 0.035f;
        const float StopSeconds = 0.2f;

        // How many letters of the line are out so far.
        int Shown(string text)
        {
            if (_revealed || UiMotion.Reduced) return text.Length;
            float elapsed = Time.unscaledTime - _lineAt, at = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\n' && c != ' ') at += LetterSeconds;
                if ((c == '.' || c == '!' || c == '?' || c == '…' || c == ',') && (i + 1 == text.Length || text[i + 1] == ' ' || text[i + 1] == '\n')) at += StopSeconds;
                if (at > elapsed) return i;
            }
            return text.Length;
        }

        // The face for a line (Docs/UiRework.md 4-2; the sheets have no column for it, so it is read from the words): an
        // angry word, a sad one, a glad one, then "!" surprised, "?" or a line opening with "..." thinking, else as usual.
        // The question a talk ends on, with the answers under it, thinks.
        internal static Expression ExpressionOf(string text, bool asking = false)
        {
            if (string.IsNullOrEmpty(text)) return Expression.Neutral;
            foreach (string word in AngryWords) if (text.Contains(word)) return Expression.Angry;
            foreach (string word in SadWords) if (text.Contains(word)) return Expression.Sad;
            foreach (string word in GladWords) if (text.Contains(word)) return Expression.Happy;
            if (text.Contains("!")) return Expression.Surprised;
            if (asking || text.Contains("?") || text.TrimStart().StartsWith("...") || text.TrimStart().StartsWith("…")) return Expression.Thinking;
            return Expression.Neutral;
        }

        static readonly string[] AngryWords = { "감히", "용서 못", "용서하지", "괘씸", "이놈", "닥쳐", "화가 나", "가만두지" };
        static readonly string[] SadWords = { "슬프", "미안", "불탔", "잃었", "눈물", "그리워", "외로", "죽었" };
        static readonly string[] GladWords = { "고맙", "고마워", "다행", "반갑", "기쁘", "잘했", "축하", "하하", "좋아!", "멋지" };

        // Who says a line: their id and look for the portrait, and their colour (characters.csv, npcs.csv); "" for the
        // lines of what to do, which have no one.
        (string Id, string Look, Color? Color, NpcDefinition Npc) SpeakerOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return ("", null, null, null);
            var character = _client.Story?.Characters.FirstOrDefault(c => c.Name == name);
            if (character != null) return (character.Id, character.Look, Parse(character.Color), _client.Npcs?.Npcs.FirstOrDefault(n => n.Name == name));
            var npc = _client.Npcs?.Npcs.FirstOrDefault(n => n.Name == name);
            if (npc != null) return (npc.Id, null, Parse(npc.Color), npc);
            return ("", null, null, null);
        }

        static Color? Parse(string hex) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var color) ? color : (Color?)null;

        // Points a portrait pixel takes: 3 on a PC (and a tablet) at least 700 points tall, 2 on a phone or a small window,
        // 1.5 on a phone held sideways, whose talk box shows the head and shoulders only (PortraitRows). The box stays
        // under a third of the screen's height that way.
        public static float PortraitScale => Ui.Short ? 1.5f : ScreenStack.PhoneSheet || Ui.Height < 700 ? 2 : 3;
        public static int PortraitRows => Ui.Short ? 36 : 0;

        public const float TalkPad = 10;
        const float ChoiceGap = 6;
        public static float ActionHeight => Controls.UsingTouch ? Ui.TouchButtonHeight : 30;

        // The box's parts: the panel, the portrait's slot (empty without one), where the words go, and the row(s) of
        // buttons under them.
        public readonly struct TalkLayout
        {
            public TalkLayout(Rect panel, Rect slot, Rect text, Rect actions)
            {
                Panel = panel;
                Slot = slot;
                Text = text;
                Actions = actions;
            }

            public Rect Panel { get; }
            public Rect Slot { get; }
            public Rect Text { get; }
            public Rect Actions { get; }
        }

        // How wide the box is: across a phone held upright (4 points from its edges); on a low screen from the left edge to
        // the minimap column, so a tall box (answers to pick) never runs into it; else up to 660 points in the middle.
        public static float TalkWidth => Ui.Width < 600 && ScreenStack.PhoneSheet ? Ui.Width - 8 : Ui.Short ? Ui.MinimapArea.x - 16 : Mathf.Min(660, Ui.Width - 24);

        // How wide the words may run beside a portrait (or without one).
        public static float TextWidth(bool portrait) => TalkWidth - 2 * TalkPad - (portrait ? SlotSize().x + TalkPad : 4);

        static Vector2 SlotSize()
        {
            var size = UiSkin.PortraitSize(PortraitScale, PortraitRows);
            float edge = 2 * UiArt.InsetBorder * UiSkin.Pt + 4;
            return new Vector2(size.x + edge, size.y + edge);
        }

        // Rows of buttons under the words (`actions`, each a finger tall on touch) and whether they run under the portrait
        // too (a narrow box) or beside it: the box at the bottom of the screen, where the skill keys and quick slots were
        // (they are not drawn while it is up, Docs/UiRework.md 4-2: on a phone held upright it lay over them, the U1 sweep),
        // as tall as its parts.
        public static TalkLayout Lay(float textHeight, bool portrait, int actionRows)
        {
            float width = TalkWidth;
            var slot = portrait ? SlotSize() : Vector2.zero;
            float top = UiSkin.FrameEdge + UiSkin.HeadHeight + (Ui.Short ? 4 : 8);
            float actions = actionRows * ActionHeight + Mathf.Max(0, actionRows - 1) * ChoiceGap;
            bool under = portrait && width < 520;
            float textX = TalkPad + (portrait ? slot.x + TalkPad : 4);
            float body = under ? Mathf.Max(slot.y, textHeight) + ChoiceGap + actions : Mathf.Max(slot.y, textHeight + ChoiceGap + actions);
            float height = top + body + TalkPad;
            float x = Ui.Short ? 8 : Mathf.Round((Ui.Width - width) / 2);
            float y = Mathf.Round(Ui.Height - height - (ScreenStack.PhoneSheet ? 4 : 8));
            var panel = new Rect(x, y, width, height);
            var slotRect = portrait ? new Rect(x + TalkPad, y + top, slot.x, slot.y) : default;
            var text = new Rect(x + textX, y + top, width - textX - TalkPad, textHeight);
            float actionsX = under ? x + TalkPad : x + textX;
            var actionsRect = new Rect(actionsX, y + top + body - actions, x + width - TalkPad - actionsX, actions);
            return new TalkLayout(panel, slotRect, text, actionsRect);
        }

        static GUIStyle _counter;

        // The talk at the bottom of the screen: the speaker's band (their colour, name, hearts for a stall keeper), their
        // portrait with the line's face, the words typed out with a bouncing arrow once they are all out and more follow,
        // and under them [다음]/[알겠다]/[닫기], the answers to pick from, or a stall keeper's choices. A tap on the words
        // (or G, Space, Enter) shows the rest of the line, then goes on.
        void DrawTalk()
        {
            var stall = _client.NpcNear();
            if (_merchant != null && (stall == null || stall.Value.Npc != _merchant))
            {
                // Walked away from her stall.
                Close();
                return;
            }
            var label = GUI.skin.label;
            var (name, text) = _lines[_line];
            var speaker = SpeakerOf(name);
            bool portrait = speaker.Id.Length > 0;
            bool last = _line >= _lines.Count - 1;
            bool asking = _choices != null && last;
            float textWidth = TextWidth(portrait);
            string wrapped = Ui.Wrap(text, label, textWidth);
            float textHeight = label.CalcHeight(new GUIContent(wrapped), textWidth);
            int actionRows = asking ? (_choices.Count + AnswerColumns - 1) / AnswerColumns : _merchant != null ? (MerchantChoices.Length + MerchantColumns - 1) / MerchantColumns : 1;
            var lay = Lay(textHeight, portrait, actionRows);
            var panel = lay.Panel;
            Controls.Block(panel);
            if (Event.current.type == EventType.Repaint) TalkShown = panel;
            var accent = speaker.Color is Color color ? Color.Lerp(color, new Color(0.35f, 0.25f, 0.2f), 0.25f) : (Color?)null;
            var kind = accent != null ? UiKind.Talk : UiKind.Stone;
            UiMotion.BeginWindow("talk", panel, kind, accent);
            UiSkin.Window(panel, kind, name.Length > 0 ? name : "할 일", accent != null ? null : UiIcons.Book, out var head, accent);
            // A stall keeper's liking as hearts beside the name (Docs/UiRework.md 4-2).
            var npc = speaker.Npc;
            if (npc != null && (_merchant == npc || _client.Friendships.ContainsKey(npc.Id))) UiSkin.Hearts(head, UiSkin.CloseRect(head).x - 8, _client.HeartsWith(npc), World.MaxHearts);
            if (UiSkin.CloseButton(head))
            {
                Close();
                UiMotion.EndWindow();
                return;
            }
            var face = ExpressionOf(text, asking);
            PortraitShown = portrait ? $"{speaker.Id}:{face}" : "";
            if (portrait)
            {
                var backing = speaker.Color ?? new Color(0.54f, 0.48f, 0.42f);
                UiSkin.Portrait(lay.Slot, PortraitArt.Of(speaker.Id, speaker.Look, ToRgba(backing), face), PortraitScale, backing, PortraitRows);
            }
            // The words so far; the layout is the whole line's, so nothing moves as it comes out.
            int shown = Shown(wrapped);
            bool typing = shown < wrapped.Length;
            if (!typing) _revealed = true;
            if (Event.current.type == EventType.Repaint) Typing = typing;
            GUI.Label(lay.Text, typing ? wrapped.Substring(0, shown) : wrapped);
            bool more = !typing && !last;
            if (more) UiSkin.NextArrow(new Vector2(lay.Text.xMax - 8, lay.Text.yMax - 4), UiSkin.Gold);

            bool done = false;
            if (_merchant != null) done = MerchantActions(lay.Actions, typing);
            else if (asking) done = Answers(lay.Actions, typing);
            else done = NextButton(lay, last, typing);
            if (done || _lines == null)
            {
                UiMotion.EndWindow();
                return;
            }
            // A tap on the rest of the box (the buttons above took theirs first): the rest of the line, then the next.
            var body = Rect.MinMaxRect(panel.x, head.yMax, panel.xMax, panel.yMax);
            Ui.Probe("대화 넘기기", body);
            if (GUI.Button(body, GUIContent.none, GUIStyle.none)) Advance();
            UiMotion.EndWindow();
        }

        static Rgba ToRgba(Color color) => new Rgba(Mathf.RoundToInt(color.r * 255), Mathf.RoundToInt(color.g * 255), Mathf.RoundToInt(color.b * 255), 255);

        // [다음], or on the last line [알겠다] (the step is taken) or [닫기]; which line of how many at the left. While the line
        // is still being typed, [다음] shows the rest of it first. True when the talk ended.
        bool NextButton(TalkLayout lay, bool last, bool typing)
        {
            float height = ActionHeight;
            var row = lay.Actions;
            string next = last ? (_tellsHost ? "알겠다" : "닫기") : "다음";
            if (Ui.Button(new Rect(row.xMax - 100, row.y, 100, height), next, last && _tellsHost ? Ui.Look.Primary : Ui.Look.Normal))
            {
                if (typing)
                {
                    _revealed = true;
                    return false;
                }
                Sounds.Play(Sound.Click);
                if (!last)
                {
                    _line++;
                    StartLine();
                    return false;
                }
                if (_tellsHost) _send(_client.TalkStoryCommand());
                _lines = null;
                return true;
            }
            Counter(new Rect(row.x, row.y, 120, height));
            return false;
        }

        // The answers one under another, two to a row on a low screen.
        public static int AnswerColumns => Ui.Short ? 2 : 1;

        // The answers below the words, once the question is all out; picking one tells the host.
        bool Answers(Rect area, bool typing)
        {
            if (typing) return false;
            float height = ActionHeight;
            int columns = AnswerColumns;
            float width = (area.width - (columns - 1) * ChoiceGap) / columns;
            for (int i = 0; i < _choices.Count; i++)
            {
                var rect = new Rect(area.x + i % columns * (width + ChoiceGap), area.y + i / columns * (height + ChoiceGap), width, height);
                if (!Ui.Button(rect, _choices[i].Text)) continue;
                Sounds.Play(Sound.Click);
                _send(_client.ChooseStoryCommand(i));
                var asker = _client.CurrentSteps.FirstOrDefault(s => s.Step.Choices == _choices).Step;
                _answered = asker?.Who;
                _answeredAt = asker?.Index ?? -1;
                _lines = null;
                _choices = null;
                return true;
            }
            return false;
        }

        // A stall keeper's choices (Docs/UiRework.md 4-2), with the shop's tab each opens; [말 걸기] once a day.
        static readonly (string Label, string Tab)[] MerchantChoices = { ("거래", "사기"), ("부탁", "부탁"), ("선물", "선물"), ("이야기", "이야기"), ("말 걸기", null), ("닫기", null) };

        // Three to a row in a box across a phone held upright, else all in one row.
        static int MerchantColumns => TalkWidth < 520 ? 3 : MerchantChoices.Length;

        bool MerchantActions(Rect area, bool typing)
        {
            int columns = MerchantColumns;
            float height = ActionHeight;
            float width = (area.width - (columns - 1) * ChoiceGap) / columns;
            for (int i = 0; i < MerchantChoices.Length; i++)
            {
                var (label, tab) = MerchantChoices[i];
                var rect = new Rect(area.x + i % columns * (width + ChoiceGap), area.y + i / columns * (height + ChoiceGap), width, height);
                bool talked = label == "말 걸기" && _client.TalkedToday(_merchant);
                var enabled = GUI.enabled;
                GUI.enabled = enabled && !talked;
                bool pressed = Ui.Button(rect, label, i == 0 ? Ui.Look.Primary : Ui.Look.Normal);
                GUI.enabled = enabled;
                if (!pressed) continue;
                if (typing) _revealed = true;
                if (tab != null)
                {
                    Sounds.Play(Sound.Click);
                    // The shop opens in place of the talk (ScreenStack.Opened closes it).
                    OpenShop?.Invoke(tab);
                    return true;
                }
                if (label == "말 걸기")
                {
                    Sounds.Play(Sound.Click);
                    _send(_client.TalkCommand());
                    return false;
                }
                Close();
                return true;
            }
            return false;
        }

        // Which line of how many, small at the bottom left.
        void Counter(Rect rect)
        {
            _counter ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            _counter.normal.textColor = UiSkin.Muted;
            GUI.Label(rect, $"{_line + 1}/{_lines?.Count ?? 0}", _counter);
        }

        // The waypoints and the waystones (3c) open to this player, to go to from the one they stand by: a list that
        // scrolls when it is longer than the screen (a finger drags it).
        void DrawWaypoints()
        {
            var places = _client.Story?.Places.Where(p => p.Waypoint).ToList() ?? new List<PlaceDefinition>();
            var stones = _client.Stones.OrderBy(s => s.Biome).ToList();
            float width = Mathf.Min(420, Ui.Width - 24);
            float row = 34;
            var panel = ListArea(width, 70 + UiSkin.HeadHeight + row * (places.Count + stones.Count + (stones.Count > 0 ? 1 : 0)) + 50);
            Controls.Block(panel);
            var here = _client.WaypointNear()?.Definition;
            if (here != null && !_client.Waypoints.Contains(here.Index)) here = null;
            var hereStone = _client.StoneNear();
            if (hereStone != null && !_client.StoneOpen(hereStone.Index)) hereStone = null;
            bool by = here != null || hereStone != null;
            // A tap outside the list closes it, as outside a sheet on a phone (Docs/UiRework.md 4-5 닫기).
            var e = Event.current;
            if (e.type == EventType.MouseDown && !panel.Contains(e.mousePosition) && !Ui.MinimapArea.Contains(e.mousePosition))
            {
                _waypoints = false;
                UiMotion.Cancel();
                e.Use();
                return;
            }
            Ui.TouchScroll(panel, ref _waypointScroll);
            UiMotion.BeginWindow("waypoints", panel, UiKind.Journal);
            var inside = UiSkin.Window(panel, UiKind.Journal, "웨이포인트", UiIcons.Waypoint, out var head);
            if (UiSkin.CloseButton(head)) _waypoints = false;
            GUILayout.BeginArea(inside);
            GUILayout.Label("가 본 곳으로 간다", GUILayout.Height(24));
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
            UiMotion.EndWindow();
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
