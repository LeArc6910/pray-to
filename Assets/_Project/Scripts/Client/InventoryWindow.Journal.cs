using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The journal (J, Docs/UiRework.md 4-1 기록, U5): a parchment book over the whole screen under the records' parchment
    // band, no longer the bag's old page in a wide window:
    //  - its index of five sections at the left (a row of tabs along the top on a phone held upright), the one open marked
    //    by a red ribbon, each with how many there are; the page beside it scrolls.
    //  - 목표: the guide's goal as a note with its progress bar and reward, the story's steps on now (the main one and the
    //    side stories, each with its person's portrait, what to do, which way, [지도에서 보기]), today's requests with how
    //    much of each is held, and the goals passed.
    //  - 이야기: the main story chapter by chapter (the steps passed, the answers given, the step on now), each side story
    //    and each stall keeper's story with the person's portrait and how far along.
    //  - 소문: the rumours heard (묶음 4c), each which way and in which biome, [지도에서 보기] opening the map on its faint ring.
    //  - 도감: the bestiary as a grid of pictures, found ones in colour with their count, unknown ones a dark silhouette with
    //    a question mark; one pressed shows its card at the top.
    //  - 업적: each achievement as a note with its progress bar and reward, a red wax seal once done (rewards are handed out
    //    the moment it is reached, so there is nothing to claim).
    // [추적] on a side story, a stall keeper's story or a request makes the goal list (GameRoot.DrawTracker) show it first
    // (ScreenStack.Tracked). Only this device's view: nothing goes to the host.
    sealed partial class InventoryWindow
    {
        internal enum JournalSection
        {
            Goals,
            Story,
            Rumors,
            Bestiary,
            Deeds,
        }

        // The names on the index, also what the playtests press.
        internal static readonly string[] JournalNames = { "목표", "이야기", "소문", "도감", "업적" };
        static readonly string[][] JournalIcons = { MapIcons.Pin, UiIcons.Book, UiIcons.Talk, MapIcons.Skull, UiIcons.Check };

        JournalSection _journalSection;
        readonly Vector2[] _journalScroll = new Vector2[5];
        bool _showPassedGoals;
        RecordGroup _bestiaryGroup = RecordGroup.Monsters;
        string _bestiaryPick;
        // A change asked for by a press, made once the page is drawn, so the layout of the pass it was pressed in stays.
        Action _journalAfter;

        // The map, for the rumours and [지도에서 보기] (GameRoot hands it over).
        internal Func<MapView> Map { get; set; }

        internal string JournalSectionName => JournalNames[(int)_journalSection];
        internal string BestiaryGroupName => Records.GroupName(_bestiaryGroup);
        internal string BestiaryPick => _bestiaryPick ?? "";

        // Opens the journal on a section (the playtests, the folded goal list).
        internal void ShowJournal(JournalSection section)
        {
            _journalSection = section;
            _screens.Show(GameScreen.Journal);
        }

        // ---- Where its parts go ----

        internal readonly struct JournalLayout
        {
            public JournalLayout(Rect book, Rect index, Rect page, bool strip)
            {
                Book = book;
                Index = index;
                Page = page;
                Strip = strip;
            }

            // The whole parchment, the index (a column, or a strip of tabs along the top) and the page that scrolls.
            public Rect Book { get; }
            public Rect Index { get; }
            public Rect Page { get; }
            public bool Strip { get; }
        }

        // The index at the left wherever the page beside it keeps 300 points; along the top otherwise (a phone held upright).
        internal static JournalLayout LayJournal(Rect body)
        {
            float edge = RecordArt.PageBorder * UiSkin.Pt;
            var book = body;
            var inner = Rect.MinMaxRect(book.x + edge, book.y + edge, book.xMax - edge, book.yMax - edge);
            float tab = Controls.UsingTouch ? 40 : 34;
            if (inner.width >= 520)
            {
                float width = Mathf.Clamp(Mathf.Round(inner.width * 0.22f), 150, 200);
                var index = new Rect(inner.x + 2, inner.y + 4, width - 4, inner.height - 8);
                var page = Rect.MinMaxRect(inner.x + width + 10, inner.y + 4, inner.xMax - 2, inner.yMax - 4);
                return new JournalLayout(book, index, page, false);
            }
            var strip = new Rect(inner.x + 2, inner.y + 4, inner.width - 4, tab);
            return new JournalLayout(book, strip, Rect.MinMaxRect(inner.x + 2, strip.yMax + 6, inner.xMax - 2, inner.yMax - 4), true);
        }

        // Where the index's entry i goes: a tab of the strip, or a row of the column (a finger's 40 points on a touch screen;
        // closer together on a low one so all five fit).
        internal static Rect IndexEntry(JournalLayout lay, int i)
        {
            int count = JournalNames.Length;
            if (lay.Strip)
            {
                float width = (lay.Index.width - 4 * (count - 1)) / count;
                return new Rect(lay.Index.x + i * (width + 4), lay.Index.y, width, lay.Index.height);
            }
            float row = Controls.UsingTouch ? Ui.Short ? 40 : 44 : 38;
            float gap = Ui.Short ? 2 : 4;
            float top = Ui.Short ? 2 : 8;
            return new Rect(lay.Index.x, lay.Index.y + top + i * (row + gap), lay.Index.width, row);
        }

        // ---- The window ----

        void DrawJournalScreen()
        {
            var screen = GameScreen.Journal;
            var area = ScreenStack.AreaOf(screen);
            Controls.Block(area);
            UiMotion.BeginWindow(ScreenStack.MotionKey(screen), area, UiKind.Journal);
            var inside = UiSkin.Window(area, UiKind.Journal, ScreenStack.NameOf(screen) + ScreenStack.KeyHint(screen), ScreenStack.IconOf(screen), out var head);
            float right = UiSkin.CloseRect(head).x - 6;
            DrawCoins(head, right, UiKind.Journal);
            if (UiSkin.CloseButton(head))
            {
                _screens.Close();
                UiMotion.EndWindow();
                return;
            }
            _screens.Tabs(new Rect(inside.x, inside.y, inside.width, ScreenStack.TabHeight), screen);
            var lay = LayJournal(GrowthBody(area));
            _rows = lay.Page;
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Page(lay.Book);
                // The fold between the index and the page.
                if (!lay.Strip)
                {
                    float x = lay.Index.xMax + 4;
                    Fill(new Rect(x, lay.Book.y + 6, 2, lay.Book.height - 12), new Color(0.36f, 0.24f, 0.13f, 0.35f));
                    Fill(new Rect(x + 2, lay.Book.y + 6, 4, lay.Book.height - 12), new Color(0.36f, 0.24f, 0.13f, 0.12f));
                }
            }
            DrawJournalIndex(lay);
            DrawJournalPage(lay.Page);
            UiMotion.EndWindow();
            var after = _journalAfter;
            _journalAfter = null;
            after?.Invoke();
        }

        static readonly Rgba SealRed = Rgba.Hex("#A8322A");
        static readonly Dictionary<int, PixelImage> Seals = new Dictionary<int, PixelImage>();

        static PixelImage Seal(Rgba wax)
        {
            int key = wax.R << 16 | wax.G << 8 | wax.B;
            if (!Seals.TryGetValue(key, out var image)) Seals[key] = image = RecordArt.Seal(wax);
            return image;
        }

        static PixelImage _ribbon;

        // The index: each section's icon, name and how many, the open one under the red ribbon; a press opens it at once.
        void DrawJournalIndex(JournalLayout lay)
        {
            var e = Event.current;
            int count = JournalNames.Length;
            Ui.Row("일지");
            if (lay.Strip)
            {
                for (int i = 0; i < count; i++)
                {
                    var tab = IndexEntry(lay, i);
                    bool chosen = (int)_journalSection == i;
                    if (e.type == EventType.Repaint) UiSkin.Tab(tab, new GUIContent(JournalNames[i]), JournalIcons[i], chosen, tab.Contains(e.mousePosition));
                    IndexPress(tab, i);
                }
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var rect = IndexEntry(lay, i);
                bool chosen = (int)_journalSection == i;
                bool hover = rect.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
                if (e.type == EventType.Repaint)
                {
                    if (chosen) Fill(rect, new Color(0.62f, 0.44f, 0.22f, 0.28f));
                    else if (hover) Fill(rect, new Color(0.62f, 0.44f, 0.22f, 0.14f));
                    // A rule under each entry, as a book's contents.
                    Fill(new Rect(rect.x + 6, rect.yMax + 1, rect.width - 12, 1), new Color(0.43f, 0.31f, 0.18f, 0.35f));
                    var ink = chosen ? InkRed : InkDark;
                    var iconSize = UiSkin.IconSize(JournalIcons[i]);
                    UiSkin.Icon(new Rect(rect.x + 16, rect.y, iconSize.x, rect.height), JournalIcons[i], chosen ? new Color(0.72f, 0.22f, 0.14f) : new Color(0.5f, 0.36f, 0.2f));
                    Write(new Rect(rect.x + 22 + iconSize.x, rect.y, rect.width - 70 - iconSize.x, rect.height), JournalNames[i], ink);
                    Write(new Rect(rect.xMax - 58, rect.y, 52, rect.height), SectionCount((JournalSection)i), InkFaint, align: TextAnchor.MiddleRight);
                    if (chosen)
                    {
                        _ribbon ??= RecordArt.Ribbon(Rgba.Hex("#B03A2E"));
                        UiSkin.Image(new Vector2(rect.x - 2, rect.y - 6), _ribbon);
                    }
                }
                IndexPress(rect, i);
            }
            // Under the index, the day and what is on now, as a note in the margin.
            float bottom = IndexEntry(lay, count - 1).yMax + 14;
            if (bottom + 60 < lay.Index.yMax)
            {
                string margin = $"{_client.Today}일째";
                if (Guide != null) margin += $"\n목표 {Guide.Done}/{Guide.Total}";
                int deeds = Records.Achievements.Count(a => _client.Achievements.Contains(a.Id));
                margin += $"\n업적 {deeds}/{Records.Achievements.Count}";
                Write(new Rect(lay.Index.x + 10, bottom, lay.Index.width - 20, 60), margin, InkFaint, align: TextAnchor.UpperLeft, wrap: true);
                bottom += 64;
            }
            // The book's red wax seal at the foot of the index, where there is room for it.
            var seal = Seal(SealRed);
            var sealSize = UiSkin.SizeOf(seal);
            if (bottom + sealSize.y + 4 < lay.Index.yMax && e.type == EventType.Repaint)
                UiSkin.Image(new Vector2(Mathf.Round(lay.Index.center.x - sealSize.x / 2), Mathf.Round(lay.Index.yMax - sealSize.y - 6)), seal);
        }

        void IndexPress(Rect rect, int i)
        {
            var e = Event.current;
            Ui.Probe(JournalNames[i], rect);
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                if ((int)_journalSection != i)
                {
                    var section = (JournalSection)i;
                    _journalAfter = () => _journalSection = section;
                    UiMotion.Confirm();
                }
                e.Use();
            }
        }

        // How many there are in a section, beside its name in the index.
        string SectionCount(JournalSection section)
        {
            switch (section)
            {
                case JournalSection.Goals:
                    return (_client.CurrentSteps.Count(s => s.Step.Chain.Length == 0 || s.Taken) + (Guide?.Current != null ? 1 : 0)).ToString();
                case JournalSection.Story:
                    return _client.Story == null ? "" : $"{_client.Story.Passed(0, _client.JourneyStep, _client.StoryChoices).Count}";
                case JournalSection.Rumors:
                    return HeardNow().Count(r => !r.Found).ToString();
                case JournalSection.Bestiary:
                {
                    int found = 0, all = 0;
                    foreach (var group in BestiaryGroups)
                    {
                        var entries = _client.RecordEntries(group);
                        all += entries.Count;
                        found += Records.Progress(group, _client.Records, entries);
                    }
                    return $"{found}/{all}";
                }
                default:
                    return $"{Records.Achievements.Count(a => _client.Achievements.Contains(a.Id))}/{Records.Achievements.Count}";
            }
        }

        // ---- The page ----

        static readonly Color InkDark = new Color(0.23f, 0.14f, 0.08f);
        static readonly Color InkFaint = new Color(0.43f, 0.33f, 0.21f);
        static readonly Color InkRed = new Color(0.55f, 0.16f, 0.1f);
        static readonly Color InkGreen = new Color(0.18f, 0.42f, 0.14f);
        static readonly Color InkGold = new Color(0.6f, 0.4f, 0.06f);

        void DrawJournalPage(Rect page)
        {
            int section = (int)_journalSection;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && page.Contains(e.mousePosition))
            {
                float notches = Mathf.Sign(e.delta.y) * Mathf.Clamp(Mathf.Abs(e.delta.y), 1, 3);
                _journalScroll[section].y = Mathf.Max(0, _journalScroll[section].y + notches * WheelStep);
                e.Use();
            }
            Ui.TouchScroll(page, ref _journalScroll[section]);
            float scrollbar = GUI.skin.verticalScrollbar.fixedWidth + 6;
            _textWidth = page.width - scrollbar - 2;
            GUILayout.BeginArea(page);
            _journalScroll[section] = Ui.BeginScrollView(_journalScroll[section], GUILayout.ExpandHeight(true));
            switch (_journalSection)
            {
                case JournalSection.Goals:
                    DrawGoalsPage();
                    break;
                case JournalSection.Story:
                    DrawStoryPage();
                    break;
                case JournalSection.Rumors:
                    DrawRumorsPage();
                    break;
                case JournalSection.Bestiary:
                    DrawBestiaryPage();
                    break;
                default:
                    DrawDeedsPage();
                    break;
            }
            GUILayout.Space(8);
            Ui.EndScrollView();
            GUILayout.EndArea();
        }

        // A heading in red ink with a rule under it and a small diamond at its middle.
        void PageHeading(string text)
        {
            var r = Room(30);
            Write(new Rect(r.x + 2, r.y + 6, r.width - 4, 18), text, InkRed);
            if (Event.current.type != EventType.Repaint) return;
            Fill(new Rect(r.x + 2, r.yMax - 4, r.width - 4, 1), new Color(0.55f, 0.16f, 0.1f, 0.45f));
            float mid = r.x + r.width / 2;
            Fill(new Rect(mid - 2, r.yMax - 6, 5, 5), new Color(0.55f, 0.16f, 0.1f, 0.7f));
        }

        // A plain line in ink, wrapped to the page.
        void PageLine(string text, Color color, float indent = 0)
        {
            float height = TextHeight(text, _textWidth - 8 - indent);
            var r = Room(height + 2);
            Write(new Rect(r.x + 4 + indent, r.y + 1, r.width - 8 - indent, height), text, color, align: TextAnchor.UpperLeft, wrap: true);
        }

        // ---- A note pinned on the page ----

        // A goal, a story, a rumour or a deed: an accent bar at its left, a portrait or an icon, a small tag over its title,
        // lines under it, a progress bar, a red seal when done, and its buttons in a row along the bottom.
        sealed class Note
        {
            public string Tag = "";
            public string Title = "";
            public readonly List<(string Text, Color Color)> Lines = new List<(string, Color)>();
            public float Share = -1;
            public string BarText = "";
            public Color Accent = new Color(0.55f, 0.16f, 0.1f);
            // A person's portrait (by id, look and colour), or an icon in a colour.
            public string Person;
            public string Look;
            public Color PersonColor;
            public string[] Icon;
            public Color IconColor = new Color(0.5f, 0.36f, 0.2f);
            public bool Sealed;
            public bool Tracked;
            public readonly List<(string Label, Action Press, Ui.Look Look)> Buttons = new List<(string, Action, Ui.Look)>();
        }

        const float NotePad = 8;

        // Past the portrait drawn at r.x + 10 (its box grows with the whole-pixel scale, so a fixed 58 let it cover the
        // first letters on some screens).
        float NoteLeft(Note n) => n.Person != null ? Mathf.Max(58, 10 + PortraitBox.x + 6) : n.Icon != null ? 34 : 12;

        float NoteHeight(Note n)
        {
            float width = _textWidth - NoteLeft(n) - NotePad - (n.Sealed ? 34 : 0);
            float h = NotePad;
            if (n.Tag.Length > 0) h += 16;
            h += TextHeight(n.Title, width) + 2;
            foreach (var (text, _) in n.Lines) h += TextHeight(text, width) + 1;
            if (n.Share >= 0) h += 18;
            if (n.Buttons.Count > 0) h += 6 + GrowthButton;
            h += NotePad;
            if (n.Person != null) h = Mathf.Max(h, 2 * NotePad + PortraitBox.y);
            return Mathf.Ceil(h);
        }

        static Vector2 PortraitBox
        {
            get
            {
                var picture = UiSkin.PortraitSize(1, 34);
                float edge = 2 * UiArt.InsetBorder * UiSkin.Pt;
                return new Vector2(picture.x + edge, picture.y + edge);
            }
        }

        void DrawNote(Note n)
        {
            float height = NoteHeight(n);
            var r = Room(height + 6);
            r = new Rect(r.x, r.y, r.width, height);
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                Fill(r, new Color(1f, 0.97f, 0.86f, 0.42f));
                Outline(r, new Color(0.43f, 0.31f, 0.18f, 0.7f), 1);
                Fill(new Rect(r.x + 1, r.y + 1, 3, r.height - 2), n.Accent);
                if (n.Tracked) Outline(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0.72f, 0.22f, 0.14f, 0.9f), 2);
            }
            float left = r.x + NoteLeft(n);
            float width = r.width - NoteLeft(n) - NotePad - (n.Sealed ? 34 : 0);
            if (n.Person != null && repaint)
            {
                var box = PortraitBox;
                UiSkin.Portrait(new Rect(r.x + 10, r.y + NotePad, box.x, box.y), PortraitArt.Of(n.Person, n.Look, Sprites.ToRgba(n.PersonColor), Expression.Neutral), 1, n.PersonColor, 34);
            }
            else if (n.Icon != null && repaint)
            {
                var size = UiSkin.IconSize(n.Icon);
                UiSkin.Icon(new Rect(r.x + 10, r.y + NotePad + 1, size.x, size.y), n.Icon, n.IconColor);
            }
            float y = r.y + NotePad;
            if (n.Tag.Length > 0)
            {
                Write(new Rect(left, y, width, 14), n.Tag, InkFaint);
                y += 16;
            }
            float th = TextHeight(n.Title, width);
            Write(new Rect(left, y, width, th), n.Title, InkDark, align: TextAnchor.UpperLeft, wrap: true);
            y += th + 2;
            foreach (var (text, color) in n.Lines)
            {
                float lh = TextHeight(text, width);
                Write(new Rect(left, y, width, lh), text, color, align: TextAnchor.UpperLeft, wrap: true);
                y += lh + 1;
            }
            if (n.Share >= 0)
            {
                InkBar(new Rect(left, y + 3, Mathf.Min(width, 260), 12), n.Share, n.BarText, n.Accent);
                y += 18;
            }
            if (n.Sealed && repaint) UiSkin.ImageCentred(new Vector2(r.xMax - 20, r.y + 22), Seal(SealRed));
            if (n.Buttons.Count == 0) return;
            y += 6;
            Ui.Row(n.Title);
            float bx = r.xMax - NotePad;
            for (int i = n.Buttons.Count - 1; i >= 0; i--)
            {
                var (label, press, look) = n.Buttons[i];
                float bw = Mathf.Min(r.width - 20, Mathf.Max(GrowthButton * 2, GUI.skin.button.CalcSize(new GUIContent(label)).x + 16));
                bx -= bw;
                if (Ui.Button(new Rect(bx, y, bw, GrowthButton), label, look)) _journalAfter = press;
                bx -= 6;
            }
        }

        // A bar in ink: a dark trough, the share filled in the colour, the count written over it.
        static void InkBar(Rect r, float share, string text, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(r, new Color(0.3f, 0.2f, 0.11f, 0.85f));
            var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
            Fill(inner, new Color(0.86f, 0.78f, 0.6f));
            Fill(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(share), inner.height), new Color(color.r, color.g, color.b, 0.9f));
            if (text.Length > 0) Write(new Rect(r.x, r.y - 1, r.width, r.height + 2), text, Ink, align: TextAnchor.MiddleCenter, shadow: true);
        }

        // "3/5" as a share of the bar; -1 for none.
        static float ShareOf(string progress)
        {
            if (string.IsNullOrEmpty(progress)) return -1;
            var parts = progress.Split('/');
            return parts.Length == 2 && int.TryParse(parts[0], out int have) && int.TryParse(parts[1], out int need) && need > 0 ? (float)have / need : -1;
        }

        static Color ColorOf(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c) ? c : fallback;

        void Person(Note note, CharacterDefinition who)
        {
            if (who == null) return;
            note.Person = who.Id;
            note.Look = who.Look;
            note.PersonColor = ColorOf(who.Color, new Color(0.54f, 0.48f, 0.42f));
        }

        void Person(Note note, NpcDefinition npc)
        {
            note.Person = npc.Id;
            note.Look = null;
            note.PersonColor = ColorOf(npc.Color, new Color(0.6f, 0.35f, 0.8f));
        }

        // ---- 목표 ----

        void DrawGoalsPage()
        {
            if (_client.Rift.Open)
            {
                var rift = new Note { Tag = "지금", Title = $"공허 균열 {_client.Rift.Tier}단계", Icon = MapIcons.Skull, IconColor = new Color(0.55f, 0.3f, 0.85f), Accent = new Color(0.45f, 0.25f, 0.7f) };
                rift.Lines.Add(($"남은 적 {_client.Rift.MonstersLeft} · {_client.Rift.SecondsLeft / 60}:{_client.Rift.SecondsLeft % 60:00}", InkDark));
                DrawNote(rift);
            }
            if (_client.Defense.Active)
            {
                var defense = new Note { Tag = "지금", Title = $"기지 방어 {_client.Defense.Level}단계", Icon = MapIcons.Camp, IconColor = new Color(0.4f, 0.7f, 0.9f), Accent = new Color(0.3f, 0.55f, 0.75f) };
                defense.Lines.Add(($"{_client.Defense.Wave}/{World.DefenseWaves} 물결 · 수호석 {_client.Defense.StonePercent}%", InkDark));
                DrawNote(defense);
            }

            var guide = Guide;
            if (guide != null)
            {
                var goal = guide.Current;
                var note = new Note { Tag = $"목표 안내 {guide.Done}/{guide.Total}", Icon = MapIcons.Pin, IconColor = new Color(0.75f, 0.55f, 0.15f), Accent = InkGold };
                if (goal == null) note.Title = "안내한 목표를 모두 마쳤다";
                else
                {
                    string progress = GuideProgress.Progress(goal, GuideFacts.Of(_client));
                    note.Title = $"지금: {goal.Text}{(progress != null ? $" ({progress})" : "")}";
                    note.Lines.Add((Settings.Rumors ? goal.Rumor : goal.Hint, InkFaint));
                    if (goal.Rewards.Count > 0) note.Lines.Add(($"보상: {goal.RewardText}", InkGreen));
                    note.Share = ShareOf(progress);
                    note.BarText = progress ?? "";
                }
                if (guide.Done > 0) note.Buttons.Add((_showPassedGoals ? "마친 목표 접기" : $"마친 목표 보기 ({guide.Done})", () => _showPassedGoals = !_showPassedGoals, Ui.Look.Normal));
                DrawNote(note);
                if (_showPassedGoals)
                    foreach (var passed in guide.PassedGoals) PageLine($"✓ {passed.Text}", InkFaint, 12);
            }

            // The story's steps on now: the main one first, then each side story (taken or offered).
            foreach (var (step, taken) in _client.CurrentSteps)
            {
                bool side = step.Chain.Length > 0;
                var note = new Note
                {
                    Tag = side ? "곁이야기" : "큰 이야기",
                    Title = string.IsNullOrEmpty(step.Chapter) ? step.Title : $"{step.Chapter}: {step.Title}",
                    Accent = side ? new Color(0.25f, 0.45f, 0.62f) : InkRed,
                };
                Person(note, step.Who);
                note.Lines.Add(($"할 일: {StoryGoal(step, taken)}", InkDark));
                var site = _client.SiteOf(taken ? step.Target : step.Who.Place);
                if (site != null) note.Lines.Add(($"길: {WayTo(site)}", InkFaint));
                if (taken && step.Goal == Game.Core.StoryGoal.Have)
                {
                    int have = step.Material != null ? _client.Bag.CountOfKind(step.Material) : step.Consumable != null ? _client.Bag.CountOf(step.Consumable) : 0;
                    note.Share = Mathf.Clamp01((float)have / Mathf.Max(1, step.Count));
                    note.BarText = $"{Mathf.Min(have, step.Count)}/{step.Count}";
                }
                if (step.Rewards.Count > 0) note.Lines.Add(($"보상: {step.RewardText}", InkGreen));
                if (site != null)
                {
                    var spot = site;
                    note.Buttons.Add(("지도에서 보기", () => ShowPlaceOnMap(spot), Ui.Look.Normal));
                }
                if (side && taken)
                {
                    string key = "side:" + step.Chain;
                    note.Tracked = _screens.Tracked == key;
                    note.Buttons.Add((note.Tracked ? "추적 그만" : "추적", () => _screens.Track(key), note.Tracked ? Ui.Look.Primary : Ui.Look.Normal));
                }
                DrawNote(note);
            }

            // Today's requests of the people met, with how much of each is held.
            PageHeading("오늘의 부탁");
            bool any = false;
            foreach (var npc in People)
            {
                if (!_client.Friendships.ContainsKey(npc.Id)) continue;
                var request = _client.RequestOf(npc);
                if (request == null) continue;
                any = true;
                bool done = _client.RequestDoneToday(npc);
                var note = new Note { Tag = done ? "오늘의 부탁 · 마침" : "오늘의 부탁", Title = $"{npc.Name}: {request.NeedText} → {request.RewardText}{(done ? " (마침)" : "")}", Accent = new Color(0.5f, 0.32f, 0.62f) };
                Person(note, npc);
                int need = 0, have = 0;
                foreach (var (thing, count) in request.Need)
                {
                    int held = thing is MaterialDefinition m ? _client.Bag.CountOfKind(m) : _client.Bag.CountOf((ConsumableDefinition)thing);
                    need += count;
                    have += Math.Min(held, count);
                }
                note.Share = done ? 1 : (float)have / Mathf.Max(1, need);
                note.BarText = done ? "마침" : $"{have}/{need}";
                if (!done)
                {
                    string key = "request:" + npc.Id;
                    note.Tracked = _screens.Tracked == key;
                    note.Buttons.Add((note.Tracked ? "추적 그만" : "추적", () => _screens.Track(key), note.Tracked ? Ui.Look.Primary : Ui.Look.Normal));
                }
                DrawNote(note);
            }
            if (!any) PageLine("만난 사람에게 말을 걸면 그날의 부탁을 알려 준다", InkFaint);

            // The loading screen's help again, all of it (Docs/TitleRework.md 4-4: tips can be read again in the journal).
            PageHeading(Strings.T("journal.help", "도움말"));
            for (int i = 0; i < Tips.Count; i++) PageLine("· " + Tips.Text(i), InkDark);
        }

        IReadOnlyList<NpcDefinition> People => _client.Npcs?.Npcs ?? (IReadOnlyList<NpcDefinition>)Array.Empty<NpcDefinition>();

        // What a step asks, in a few words.
        string StoryGoal(StoryStep step, bool taken)
        {
            if (!taken) return $"{step.Who.Name}에게 말 걸기 ({step.Who.Place.Name})";
            return step.Goal switch
            {
                Game.Core.StoryGoal.Reach => $"{step.Place.Name}에 가기",
                Game.Core.StoryGoal.Boss => $"{_client.BossName(step.Boss)} 쓰러뜨리기",
                Game.Core.StoryGoal.Have => $"{step.Material?.Name ?? step.Consumable?.Name} {step.Count}개를 {step.Who.Name}에게",
                _ => $"{step.Who.Name}에게 말 걸기",
            };
        }

        // Which way a place lies, as the goal list says it: how far once its spot is known, else which way and in which
        // biome (묶음 4c, MapView.KnowledgeOf).
        string WayTo(PlaceSite site)
        {
            var from = _client.WorldPosition;
            if (site.Contains(from)) return $"{site.Definition.Name} (여기)";
            var known = Map?.Invoke()?.KnowledgeOf(site) ?? WayKnown.Known;
            var here = _client.Biomes[TileMap.Cell(from.X), TileMap.Cell(from.Y)];
            var there = _client.Biomes[TileMap.Cell(site.Center.X), TileMap.Cell(site.Center.Y)];
            return $"{site.Definition.Name}, {Rumors.Way(known, from, site.Center, there, here)}";
        }

        // [지도에서 보기] on a place: the map on its spot once known, else on the faint ring where it lies.
        void ShowPlaceOnMap(PlaceSite site)
        {
            var map = Map?.Invoke();
            if (map == null) return;
            var known = map.KnowledgeOf(site);
            if (known == WayKnown.Known) map.ShowAt(site.Center, 30, site.Definition.Name);
            else
            {
                float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
                map.ShowAt(Rumors.Circle(_client.Seed, RumorKind.Arena, 1000 + site.Definition.Index, site.Center, radius), radius * 1.4f, site.Definition.Name);
            }
        }

        // ---- 이야기 ----

        void DrawStoryPage()
        {
            var story = _client.Story;
            if (story == null || story.Steps.Count == 0)
            {
                PageLine("이야기 시트가 없다", InkFaint);
                return;
            }
            PageHeading("큰 이야기");
            string chapter = null;
            foreach (var step in story.Passed(0, _client.JourneyStep, _client.StoryChoices))
            {
                if (step.Chapter != chapter && !string.IsNullOrEmpty(step.Chapter))
                {
                    chapter = step.Chapter;
                    var r = Room(24);
                    Write(new Rect(r.x + 4, r.y + 4, r.width - 8, 18), $"§ {chapter}", InkGold);
                }
                PageLine($"· {step.Title}", InkDark, 12);
                if (step.Choices.Count > 0 && _client.StoryChoices.TryGetValue(step.Index, out int answer) && answer < step.Choices.Count)
                    PageLine($"고른 말: \"{step.Choices[answer].Text}\"", InkFaint, 28);
            }
            var now = _client.CurrentStep;
            var current = new Note { Tag = "지금", Title = now == null ? "이야기는 여기까지다. 다음 이야기가 곧 이어진다" : now.Title, Accent = InkRed };
            if (now != null)
            {
                Person(current, now.Who);
                current.Lines.Add((StoryGoal(now, _client.JourneyTaken), InkDark));
                var site = _client.SiteOf(_client.JourneyTaken ? now.Target : now.Who.Place);
                if (site != null) current.Buttons.Add(("지도에서 보기", () => ShowPlaceOnMap(site), Ui.Look.Normal));
            }
            DrawNote(current);

            if (story.Chains.Count > 0)
            {
                PageHeading("곁이야기");
                foreach (var chain in story.Chains) DrawChain(story, chain);
            }

            PageHeading("사람들의 이야기");
            foreach (var npc in People)
            {
                var steps = _client.Npcs.StoryOf(npc);
                if (steps.Count == 0) continue;
                int done = _client.StorySteps.TryGetValue(npc.Id, out int d) ? d : 0;
                var next = _client.StoryStepOf(npc);
                bool open = next != null && _client.HeartsWith(npc) >= next.Hearts;
                string state = next == null ? "모두 들었다"
                    : !open ? $"다음: {next.Title} (호감 {next.Hearts}에 열린다)"
                    : $"다음: {next.Title}";
                var note = new Note { Title = $"{npc.Name} {done}/{steps.Count} · {state}", Accent = new Color(0.5f, 0.32f, 0.62f), Share = (float)done / steps.Count, BarText = $"{done}/{steps.Count}" };
                Person(note, npc);
                if (open)
                {
                    note.Lines.Add(($"필요한 것: {next.NeedText} · 보답: {next.RewardText}", InkFaint));
                    string key = "story:" + npc.Id;
                    note.Tracked = _screens.Tracked == key;
                    note.Buttons.Add((note.Tracked ? "추적 그만" : "추적", () => _screens.Track(key), note.Tracked ? Ui.Look.Primary : Ui.Look.Normal));
                }
                DrawNote(note);
            }
        }

        void DrawChain(StoryData story, StoryChain chain)
        {
            var rows = story.Steps.Where(s => s.Chain == chain.Id).ToList();
            string name = string.IsNullOrEmpty(chain.First.Chapter) ? chain.First.Title : chain.First.Chapter;
            var note = new Note { Tag = chain.First.Who.Name, Accent = new Color(0.25f, 0.45f, 0.62f) };
            Person(note, chain.First.Who);
            if (_client.JourneyStep < chain.After)
            {
                note.Title = $"{name} · 큰 이야기 {chain.After}단계를 마치면 열린다";
                note.Share = 0;
                note.BarText = $"0/{rows.Count}";
                DrawNote(note);
                return;
            }
            int at = _client.SideSteps.TryGetValue(chain.Id, out int saved) ? saved : chain.First.Index;
            int done = story.Passed(chain.First.Index, at, _client.StoryChoices).Count;
            var step = story.Step(at);
            note.Share = (float)done / Mathf.Max(1, rows.Count);
            if (step == null || step.Chain != chain.Id)
            {
                note.Title = $"{name} {rows.Count}/{rows.Count} · 마쳤다";
                note.Share = 1;
                note.BarText = $"{rows.Count}/{rows.Count}";
                note.Sealed = true;
                DrawNote(note);
                return;
            }
            bool taken = _client.SideTaken.Contains(chain.Id);
            note.Title = $"{name} {done}/{rows.Count} · {step.Title}: {StoryGoal(step, taken)}";
            note.BarText = $"{done}/{rows.Count}";
            if (taken)
            {
                string key = "side:" + chain.Id;
                note.Tracked = _screens.Tracked == key;
                note.Buttons.Add((note.Tracked ? "추적 그만" : "추적", () => _screens.Track(key), note.Tracked ? Ui.Look.Primary : Ui.Look.Normal));
            }
            DrawNote(note);
        }

        // ---- 소문 ----

        readonly struct RumorRow
        {
            public RumorRow(RumorTarget target, int day, bool found)
            {
                Target = target;
                Day = day;
                Found = found;
            }

            public RumorTarget Target { get; }
            public int Day { get; }
            public bool Found { get; }
        }

        // The rumours heard that still hold (the wandering merchant's for its day only), newest first.
        List<RumorRow> HeardNow()
        {
            var rows = new List<RumorRow>();
            var map = Map?.Invoke();
            if (map == null || map.Heard.Count == 0) return rows;
            var targets = Rumors.Targets(_client.Rooms, _client.WandererAt(), _client.Biomes);
            foreach (var heard in map.Heard.Reverse())
            {
                if (heard.Kind == RumorKind.Wanderer && heard.Day != _client.Today) continue;
                int at = targets.FindIndex(t => t.Kind == heard.Kind && t.Index == heard.Index);
                if (at < 0) continue;
                rows.Add(new RumorRow(targets[at], heard.Day, map.Seen(targets[at].X, targets[at].Y)));
            }
            return rows;
        }

        void DrawRumorsPage()
        {
            var map = Map?.Invoke();
            PageLine(Settings.Rumors ? "사람들에게 들은 소문. 지도에는 그 근처가 옅은 원으로 보인다. 가서 보면 지도에 표시된다."
                : "길 안내가 화살표라 장소가 지도에 바로 표시된다. 들은 소문은 그래도 여기에 적힌다.", InkFaint);
            var rows = HeardNow();
            if (rows.Count == 0) PageLine("아직 들은 소문이 없다. 마을 사람에게 말을 걸어 본다.", InkFaint);
            var from = _client.WorldPosition;
            var here = _client.Biomes[TileMap.Cell(from.X), TileMap.Cell(from.Y)];
            foreach (var row in rows)
            {
                var target = row.Target;
                var note = new Note
                {
                    Tag = $"{row.Day}일째에 들었다",
                    Title = $"{Rumors.Name(target.Kind)} · {BiomeMap.Name(target.Biome)}",
                    Icon = MapView.IconOf(target.Kind),
                    IconColor = MapView.RumorColor(target.Kind),
                    Accent = MapView.RumorColor(target.Kind),
                };
                var ring = Rumors.Circle(_client.Seed, target, Rumors.RumorRadius);
                if (row.Found) note.Lines.Add(("찾았다: 지도에 표시되어 있다", InkGreen));
                else note.Lines.Add(($"{Rumors.Way(WayKnown.Rumor, from, ring, target.Biome, here)}", InkDark));
                var spot = row.Found ? target.Center : ring;
                float across = row.Found ? 30 : Rumors.RumorRadius * 2.8f;
                string name = Rumors.Name(target.Kind);
                note.Buttons.Add(("지도에서 보기", () => map?.ShowAt(spot, across, name), Ui.Look.Normal));
                DrawNote(note);
            }
            // The arenas not yet found: a ring each on the map too.
            if (map == null) return;
            var arenas = Enumerable.Range(0, _client.Arenas.Count).Where(i => map.KnowledgeOfArena(i) != WayKnown.Known).ToList();
            if (arenas.Count == 0) return;
            PageHeading("보스 아레나의 소문");
            foreach (int i in arenas)
            {
                var known = map.KnowledgeOfArena(i);
                float radius = known == WayKnown.Path ? Rumors.PathRadius : Rumors.RumorRadius;
                var ring = Rumors.Circle(_client.Seed, RumorKind.Arena, i, _client.Arenas[i], radius);
                var biome = _client.Biomes[TileMap.Cell(_client.Arenas[i].X), TileMap.Cell(_client.Arenas[i].Y)];
                var note = new Note { Title = $"보스 아레나 · {BiomeMap.Name(biome)}", Icon = MapIcons.Arena, IconColor = new Color(0.85f, 0.3f, 0.25f), Accent = new Color(0.85f, 0.3f, 0.25f) };
                note.Lines.Add((Rumors.Way(known, from, ring, biome, here), InkDark));
                note.Buttons.Add(("지도에서 보기", () => map.ShowAt(ring, radius * 2.8f, "보스 아레나"), Ui.Look.Normal));
                DrawNote(note);
            }
        }

        // ---- 도감 ----

        static readonly RecordGroup[] BestiaryGroups = { RecordGroup.Monsters, RecordGroup.Bosses, RecordGroup.Fish, RecordGroup.Crops, RecordGroup.Gear };

        void DrawBestiaryPage()
        {
            var records = _client.Records;
            // The groups as small tabs, each with how many are found.
            var groups = BestiaryGroups.Where(g => _client.RecordEntries(g).Count > 0).ToList();
            if (groups.Count == 0)
            {
                PageLine("도감에 적을 것이 없다", InkFaint);
                return;
            }
            if (!groups.Contains(_bestiaryGroup)) _bestiaryGroup = groups[0];
            float tab = GrowthButton;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((_textWidth + 4) / 104));
            int tabRows = (groups.Count + perRow - 1) / perRow;
            var strip = Room(tabRows * (tab + 4));
            float tabWidth = (strip.width - 4 * (Math.Min(perRow, groups.Count) - 1)) / Math.Min(perRow, groups.Count);
            Ui.Row("도감 묶음");
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var entries = _client.RecordEntries(group);
                int found = Records.Progress(group, records, entries);
                var rect = new Rect(strip.x + i % perRow * (tabWidth + 4), strip.y + i / perRow * (tab + 4), tabWidth, tab);
                string label = $"{Records.GroupName(group)} {found}/{entries.Count}";
                if (Ui.Button(rect, label, Records.GroupName(group), group == _bestiaryGroup ? Ui.Look.Primary : Ui.Look.Normal) && group != _bestiaryGroup)
                {
                    var chosen = group;
                    _journalAfter = () =>
                    {
                        _bestiaryGroup = chosen;
                        _bestiaryPick = null;
                    };
                }
            }
            int felled = Records.Progress(RecordGroup.Felled, records, Array.Empty<(RecordKind, string, string)>());
            PageLine($"잡은 몬스터 모두 {felled}마리", InkFaint);

            var shown = _client.RecordEntries(_bestiaryGroup);
            if (_bestiaryPick != null && shown.Any(s => s.Id == _bestiaryPick)) DrawEntryCard(shown.First(s => s.Id == _bestiaryPick));

            float cell = Controls.UsingTouch ? 52 : 46;
            int columns = Mathf.Max(1, Mathf.FloorToInt((_textWidth + 6) / (cell + 6)));
            int gridRows = (shown.Count + columns - 1) / columns;
            var grid = Room(gridRows * (cell + 6));
            Ui.Row("도감");
            var e = Event.current;
            for (int i = 0; i < shown.Count; i++)
            {
                var entry = shown[i];
                var rect = new Rect(grid.x + i % columns * (cell + 6), grid.y + i / columns * (cell + 6), cell, cell);
                int count = records.TryGetValue((entry.Kind, entry.Id), out int c) ? c : 0;
                bool known = count > 0;
                bool picked = entry.Id == _bestiaryPick;
                if (e.type == EventType.Repaint)
                {
                    UiSkin.SlotFrame(rect, picked ? UiSkin.Gold : known ? new Color(0.75f, 0.62f, 0.4f) : (Color?)null, picked);
                    var inner = new Rect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 10);
                    var sprite = EntrySprite(entry);
                    if (known) FitSprite(inner, sprite, Color.white);
                    else
                    {
                        FitSprite(inner, sprite, new Color(0.08f, 0.06f, 0.05f, 0.92f));
                        Write(rect, "?", new Color(0.95f, 0.85f, 0.6f), 12, TextAnchor.MiddleCenter, shadow: true);
                    }
                    if (known && (entry.Kind == RecordKind.Kill || entry.Kind == RecordKind.Boss))
                        Write(new Rect(rect.x, rect.yMax - 15, rect.width - 4, 13), count > 999 ? "999+" : count.ToString(), Ink, 12, TextAnchor.LowerRight, shadow: true);
                }
                Ui.Probe(known ? entry.Name : "모르는 것", rect);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    UiMotion.Confirm();
                    string id = entry.Id;
                    _journalAfter = () => _bestiaryPick = _bestiaryPick == id ? null : id;
                }
            }
        }

        // The card of the picked entry: its picture large, its name (or ??? while unknown), the count, and what is known.
        void DrawEntryCard((RecordKind Kind, string Id, string Name) entry)
        {
            int count = _client.Records.TryGetValue((entry.Kind, entry.Id), out int c) ? c : 0;
            bool known = count > 0;
            var lines = new List<string>();
            switch (entry.Kind)
            {
                case RecordKind.Kill:
                {
                    var monster = _client.MonsterKinds.Find(entry.Id);
                    if (known && monster != null)
                    {
                        lines.Add($"Lv {monster.Level} · 생명력 {monster.Health} · 피해 {monster.Damage}");
                        if (monster.Biome is Biome biome) lines.Add($"사는 곳: {BiomeMap.Name(biome)}");
                    }
                    lines.Add(known ? $"잡은 수 {count}" : "아직 잡지 못했다");
                    break;
                }
                case RecordKind.Boss:
                    lines.Add(known ? $"쓰러뜨린 수 {count}" : "아직 쓰러뜨리지 못했다");
                    break;
                case RecordKind.Gear:
                {
                    var item = _client.Loot?.Bases.FirstOrDefault(b => b.Id == entry.Id);
                    if (known && item != null) lines.Add($"{ItemText.SlotName(item.Slot)} · Lv {item.RequiredLevel}");
                    lines.Add(known ? "가져 본 적이 있다" : "아직 가져 본 적이 없다");
                    break;
                }
                default:
                {
                    var material = _client.Loot?.Materials.FirstOrDefault(m => m.Id == entry.Id);
                    if (known && material != null && !string.IsNullOrEmpty(material.Description)) lines.Add(material.Description);
                    if (material?.Fish is Biome water) lines.Add($"낚는 곳: {BiomeMap.Name(water)}{(material.FishAtNight ? " · 밤에" : "")}");
                    lines.Add(known ? "가져 본 적이 있다" : "아직 모른다");
                    break;
                }
            }
            float picture = 64;
            float textWidth = _textWidth - picture - 32;
            float height = 10 + 20 + lines.Sum(l => TextHeight(l, textWidth) + 2) + 10;
            height = Mathf.Max(height, picture + 20);
            var r = Room(height + 8);
            r = new Rect(r.x, r.y, r.width, height);
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Card(r);
                var well = new Rect(r.x + 10, r.y + 10, picture, picture);
                UiSkin.Inset(well);
                FitSprite(new Rect(well.x + 6, well.y + 6, well.width - 12, well.height - 12), EntrySprite(entry), known ? Color.white : new Color(0.08f, 0.06f, 0.05f, 0.92f));
            }
            float x = r.x + picture + 22, y = r.y + 10;
            Write(new Rect(x, y, textWidth, 18), known ? entry.Name : "???", UiSkin.Gold);
            y += 20;
            foreach (string line in lines)
            {
                float h = TextHeight(line, textWidth);
                Write(new Rect(x, y, textWidth, h), line, Ink, align: TextAnchor.UpperLeft, wrap: true);
                y += h + 2;
            }
        }

        static readonly Rgba BossColor = new Rgba(158, 143, 128);

        // The picture of a bestiary entry: a monster or boss as it stands, a fish, crop or piece of gear as its icon.
        Sprite EntrySprite((RecordKind Kind, string Id, string Name) entry)
        {
            switch (entry.Kind)
            {
                case RecordKind.Kill:
                {
                    var monster = _client.MonsterKinds.Find(entry.Id);
                    var color = Rgba.Hex(string.IsNullOrEmpty(monster?.Color) ? "#8CD94D" : monster.Color);
                    return Sprites.Centered($"bestiary/{entry.Id}/{Sprites.Key(color)}", () => CreatureArt.Monster(entry.Id, color, 0));
                }
                case RecordKind.Boss:
                    return Sprites.Centered($"bestiary/boss/{entry.Id}", () => CreatureArt.Boss(entry.Id, BossColor, 0));
                case RecordKind.Gear:
                {
                    var item = _client.Loot?.Bases.FirstOrDefault(b => b.Id == entry.Id);
                    return item == null ? null : Sprites.Icon(item);
                }
                default:
                {
                    var material = _client.Loot?.Materials.FirstOrDefault(m => m.Id == entry.Id);
                    return material == null ? null : Sprites.Icon(material);
                }
            }
        }

        // The sprite as large as fits in the rect (a whole number of times when it fits once or more), tinted: white for
        // its own colours, near black for an unknown one's silhouette.
        static void FitSprite(Rect area, Sprite sprite, Color tint)
        {
            if (sprite == null || Event.current.type != EventType.Repaint) return;
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            float scale = Mathf.Min(area.width / rect.width, area.height / rect.height);
            if (scale >= 1) scale = Mathf.Floor(scale);
            var size = new Vector2(rect.width * scale, rect.height * scale);
            var target = new Rect(Mathf.Round(area.center.x - size.x / 2), Mathf.Round(area.center.y - size.y / 2), size.x, size.y);
            var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            var previous = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * previous.a);
            GUI.DrawTextureWithTexCoords(target, texture, uv);
            GUI.color = previous;
        }

        // ---- 업적 ----

        void DrawDeedsPage()
        {
            int done = Records.Achievements.Count(a => _client.Achievements.Contains(a.Id));
            PageLine($"업적 {done}/{Records.Achievements.Count} · 이루면 보상을 바로 받는다", InkFaint);
            // The ones under way first, nearest done first; then the ones done.
            var rows = Records.Achievements.Select(a =>
            {
                var entries = _client.RecordEntries(a.Group);
                int need = Records.Needed(a, entries);
                int have = Math.Min(need, Records.Progress(a.Group, _client.Records, entries));
                return (Deed: a, Have: have, Need: need, Done: _client.Achievements.Contains(a.Id));
            }).OrderBy(r => r.Done).ThenByDescending(r => r.Done ? 0 : (float)r.Have / r.Need).ToList();
            foreach (var (deed, have, need, had) in rows)
            {
                var note = new Note
                {
                    Tag = Records.GroupName(deed.Group),
                    Title = $"{(had ? "[완료] " : "")}{deed.Name} ({have}/{need})",
                    Icon = had ? UiIcons.Check : DeedIcon(deed.Group),
                    IconColor = had ? new Color(0.3f, 0.6f, 0.25f) : new Color(0.5f, 0.36f, 0.2f),
                    Accent = had ? InkGreen : InkGold,
                    Share = (float)have / Mathf.Max(1, need),
                    BarText = $"{have}/{need}",
                    Sealed = had,
                };
                note.Lines.Add(($"보상 {Records.RewardText(deed, _client.Loot)}{(had ? " · 받았다" : "")}", had ? InkGreen : InkDark));
                DrawNote(note);
            }
        }

        static string[] DeedIcon(RecordGroup group) => group switch
        {
            RecordGroup.Bosses => MapIcons.Arena,
            RecordGroup.Fish => MapIcons.Rumor,
            RecordGroup.Crops => MapIcons.Camp,
            RecordGroup.Gear => UiIcons.Chest,
            _ => MapIcons.Skull,
        };
    }
}
