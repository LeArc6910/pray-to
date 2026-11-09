using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bag screen, opened with Tab or I, in tabs: gear (stats, consumables, worn gear, carried items, runes),
    // materials (materials with [놓기]/[심기], and the crafting list), and while the player stands by a chest, the
    // chest (what it holds with [꺼내기], what the bag holds with [넣기]). Every button only asks the host for the
    // change; the window redraws from the next inventory message.
    sealed class InventoryWindow
    {
        enum Page
        {
            Gear,
            Materials,
            Classes,
            Passives,
            Chest,
            Merchant,
            Character,
            Journal,
        }

        const float MaxWidth = 520;
        const float TabHeight = 40;
        const float TooltipWidth = 320;
        // Narrower than this beside the minimap column, the window goes under it instead.
        const float MinWidth = 300;
        // Points one notch of the mouse wheel scrolls; IMGUI's own step is a fraction of a line in browsers.
        const float WheelStep = 30;

        static GUIStyle _tooltipStyle;
        static readonly string[] PageNames = { "장비", "재료·제작", "스킬", "특성", "상자", "상인", "캐릭터", "일지" };
        static readonly GUIContent[] PageLabels = PageNames.Select(name => new GUIContent(name)).ToArray();

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        readonly Hotbar _hotbar;
        readonly Vector2[] _scroll = new Vector2[PageNames.Length];
        Page _page;
        RuneDefinition _selectedRune;
        // The item being worked at an anvil, by id, or 0 (World.Refining.cs).
        int _refining;
        float _nameWidth;
        // A 56-point button and the gap before it.
        const float ExtraButton = 60;
        float _recipeWidth;
        // Width of a line of text in the scrolling list.
        float _textWidth;
        // Where the scrolling list is on the screen, for showing descriptions only while the pointer is over it.
        Rect _rows;
        // Each row starts with the thing's icon.
        const float IconSize = 24;
        const float IconRoom = IconSize + 4;

        public InventoryWindow(WorldClient client, Action<byte[]> send, Hotbar hotbar = null)
        {
            _client = client;
            _send = send;
            _hotbar = hotbar;
        }

        public bool Open { get; set; }
        // The pointer is over a button that works the ground right ahead (plant, water, till, fertilize, lay a floor):
        // the world's frame then marks World.TileAhead, not where a thing would be put (2026-11-09).
        public bool PointsAtSoil { get; private set; }

        // Marks the button just drawn as working the ground ahead, when the pointer is over it.
        void Soil()
        {
            if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) PointsAtSoil = true;
        }

        // Opens the bag on the page with that tab name (장비, 재료·제작, 스킬, 특성), for the tests' pictures.
        internal void Show(string page)
        {
            int index = Array.IndexOf(PageNames, page);
            if (index < 0) throw new ArgumentException($"no bag page '{page}'");
            _page = (Page)index;
            Open = true;
        }

        public void Draw()
        {
            if (!Open) return;
            _typing = GUI.GetNameOfFocusedControl() == PetNameControl;
            // Only what the pointer is over in this frame. IMGUI's own GUI.tooltip fell back to the last description
            // the pointer had been over, even when set empty, so an iron sword's text picked up on [장비] stood beside
            // the skills page (the 2026-11-09 web check). The window keeps its own.
            if (Event.current.type == EventType.Repaint)
            {
                _hover = "";
                PointsAtSoil = false;
            }
            if (_selectedRune != null && _client.Bag.CountOf(_selectedRune) == 0) _selectedRune = null;

            // Left of the minimap column, full height; on narrow screens under the column. The page scrolls when it
            // does not fit.
            Rect area;
            float room = Ui.MinimapArea.x - 20;
            if (room >= MinWidth)
            {
                float width = Mathf.Min(MaxWidth, room);
                area = new Rect(Ui.MinimapArea.x - 10 - width, 10, width, Ui.Height - 20);
            }
            else
            {
                float top = Ui.RightColumnBottom + 10;
                area = new Rect(10, top, Ui.Width - 20, Ui.Height - top - 10);
            }
            // Under the HUD where the two would overlap. While the bag is open the HUD is only its health bar.
            var hud = Ui.HudArea;
            if (area.x < hud.xMax + 10 && area.y < hud.yMax + 10) area = Rect.MinMaxRect(area.x, hud.yMax + 10, area.xMax, area.yMax);
            bool besideWindow = area.x >= TooltipWidth + 20;
            float inner = area.width - GUI.skin.box.padding.horizontal;
            float scrollbar = GUI.skin.verticalScrollbar.fixedWidth + 8;
            // A label in the list is that wide less its own margins.
            _textWidth = inner - scrollbar - GUI.skin.label.margin.horizontal;
            // Room for two buttons and [퀵] beside each name, and for [끼우기] while a rune is picked and [다듬기] by
            // an anvil: without that room a row ran past the window, its [퀵] cut off.
            float extra = (_selectedRune != null ? ExtraButton : 0) + (_client.NearAnvil ? ExtraButton : 0);
            _nameWidth = Mathf.Clamp(_textWidth - IconRoom - 150 - QuickWidth - extra, 60, 240);
            _recipeWidth = _textWidth - IconRoom - 120;
            Controls.Block(area);
            Wheel(area);
            Ui.TouchScroll(area, ref _scroll[(int)_page]);
            GUILayout.BeginArea(area, GUI.skin.box);
            string close = Controls.UsingTouch ? "가방 버튼으로 닫기" : "Tab 또는 I로 닫기";
            // Without room beside the window the hint goes in the title, so nothing sits over the list.
            string hint = besideWindow ? "" : Controls.UsingTouch ? " · 이름을 누르고 있으면 설명" : " · 이름에 마우스를 올리면 설명";
            GUILayout.Label(Fit($"가방 ({close}){hint}", inner));
            // The chest tab is there only while a chest is in reach, the merchant's while she is.
            bool chest = _client.Chest.IsOpen;
            var person = _client.NpcNear();
            bool merchant = person != null;
            // The tab is named for whoever is near: 상인, 대장장이, 어부.
            if (merchant) PageLabels[(int)Page.Merchant].text = person.Value.Npc.Role;
            if (!chest && _page == Page.Chest || !merchant && _page == Page.Merchant) _page = Page.Gear;
            _pages.Clear();
            _pages.AddRange(new[] { Page.Gear, Page.Materials, Page.Classes, Page.Passives, Page.Character, Page.Journal });
            if (chest) _pages.Add(Page.Chest);
            if (merchant) _pages.Add(Page.Merchant);
            _page = Tabs(_page, _pages, inner);
            if (_hotbar != null && _hotbar.ShowsMessage) GUILayout.Label(Fit(_hotbar.Message, inner));
            int page = (int)_page;
            // The list takes whatever height the title and tabs leave, so it never runs past the frame.
            _scroll[page] = Ui.BeginScrollView(_scroll[page], GUILayout.ExpandHeight(true));
            if (_page == Page.Gear) DrawGear();
            else if (_page == Page.Materials) DrawMaterialsPage();
            else if (_page == Page.Classes) DrawClasses();
            else if (_page == Page.Passives) DrawPassives();
            else if (_page == Page.Merchant) DrawMerchant();
            else if (_page == Page.Character) DrawCharacter();
            else if (_page == Page.Journal) DrawJournal();
            else DrawChest();
            Ui.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                var rows = GUILayoutUtility.GetLastRect();
                _rows = new Rect(area.x + rows.x, area.y + rows.y, rows.width, rows.height);
            }
            GUILayout.EndArea();
            DrawTooltip(area, besideWindow);
        }

        // Tabs that switch as soon as they are pressed. IMGUI's toolbar switches on release, and in the browser its
        // first click after the bag opened was sometimes lost (LOCAL_TASKS.md, the 2026-10-31 UI check).
        readonly List<Page> _pages = new List<Page>();

        Page Tabs(Page page, IReadOnlyList<Page> pages, float width)
        {
            int count = pages.Count;
            const float gap = 4;
            // In two rows when one would make the tabs too narrow for their names (a phone held upright). Decided
            // from the window's width, which is the same in every event: a rect from GetRect is a dummy while IMGUI
            // lays out, so deciding from it asked for a second row there and not when drawing, and the mismatch
            // threw and left every page blank.
            int perRow = (width - gap * (count - 1)) / count < 64 ? (count + 1) / 2 : count;
            var probe = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.button, GUILayout.Height(TabHeight), GUILayout.ExpandWidth(true));
            var second = perRow < count ? GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.button, GUILayout.Height(TabHeight), GUILayout.ExpandWidth(true)) : probe;
            float tabWidth = (probe.width - gap * (perRow - 1)) / perRow;
            var e = Event.current;
            for (int i = 0; i < count; i++)
            {
                var row = i < perRow ? probe : second;
                int column = i < perRow ? i : i - perRow;
                var tab = new Rect(row.x + column * (tabWidth + gap), row.y, tabWidth, row.height);
                if (e.type == EventType.MouseDown && tab.Contains(e.mousePosition))
                {
                    if (page != pages[i])
                    {
                        page = pages[i];
                        Sounds.Play(Sound.Click);
                    }
                    e.Use();
                }
                Ui.Probe(PageLabels[(int)pages[i]].text, tab);
                if (e.type == EventType.Repaint) GUI.skin.button.Draw(tab, PageLabels[(int)pages[i]], tab.Contains(e.mousePosition), false, page == pages[i], false);
                // Points to spend: a gold dot in the corner of the skills or passives tab.
                bool points = pages[i] == Page.Classes && _client.SkillPoints > 0 || pages[i] == Page.Passives && _client.PassivePoints > 0;
                if (points && e.type == EventType.Repaint) Badge(tab);
            }
            return page;
        }

        // The character (as Core Keeper shows one): the worn slots laid out round the body, the picked one's panel,
        // then every stat, the classes and skills' hits, and the life skills.
        void DrawCharacter()
        {
            GUILayout.Label(Fit($"{_client.Name} · Lv {_client.Level} ({(int)(Leveling.Progress(_client.Experience) * 100)}%) · " +
                (_client.Classes.Count == 0 ? "클래스 없음" : string.Join(" + ", _client.Classes.Select(c => c.Name))), _textWidth));
            float cell = Controls.UsingTouch ? 52 : 48;
            // Helmet and amulet on top, weapon, chest and ring across the middle, boots below.
            var places = new (ItemSlot Slot, int Column, int Row)[]
            {
                (ItemSlot.Helmet, 1, 0), (ItemSlot.Amulet, 2, 0),
                (ItemSlot.Weapon, 0, 1), (ItemSlot.Chest, 1, 1), (ItemSlot.Ring, 2, 1),
                (ItemSlot.Boots, 1, 2),
            };
            var doll = GUILayoutUtility.GetRect(_textWidth, cell * 3 + CellGap * 2, GUILayout.Width(_textWidth));
            float left = doll.x + (doll.width - (cell * 3 + CellGap * 2)) / 2;
            Ui.Row("캐릭터");
            foreach (var (slot, column, row) in places)
            {
                var area = new Rect(left + column * (cell + CellGap), doll.y + row * (cell + CellGap), cell, cell);
                var item = _client.Equipment[slot];
                if (item == null)
                {
                    EmptyCell(area, ItemText.SlotName(slot));
                    continue;
                }
                if (Cell(area, Sprites.Icon(item.Base), ItemText.Name(item), item.Upgrade > 0 ? $"+{item.Upgrade}" : null, Palette.Of(item.Rarity), _picked == item.Id, ItemTooltip(item)))
                {
                    Pick(item.Id, null);
                }
            }
            DrawPicked();
            GUILayout.Space(6);
            DrawStats();
            DrawPet();
            GUILayout.Label(Fit($"스킬 포인트 {_client.SkillPoints} · 특성 포인트 {_client.PassivePoints} · 배운 특성 {_client.Passives.Count}", _textWidth));
            DrawLifeSkills();
        }

        // The pet (Docs/Animals.md 펫 키우기): its picture, level and how far to the next, and what it gives now.
        void DrawPet()
        {
            var pet = _client.Pet;
            if (pet == null)
            {
                GUILayout.Label(Fit("펫 없음 · 몬스터가 가끔 떨어뜨리는 알을 [재료·제작]의 [부화]로 깨운다", _textWidth));
                return;
            }
            int level = _client.PetLevel;
            _client.PetExperience.TryGetValue(pet.Id, out int had);
            string next = level >= Pets.MaxLevel ? "최고 레벨" : $"경험 {had - Pets.TotalFor(level)}/{Pets.TotalFor(level + 1) - Pets.TotalFor(level)}";
            string gives = string.Join(", ", Pets.ModifiersAt(pet, level).Select(StatText.Describe));
            if (pet.Bites) gives += $", 곁의 몬스터를 {Pets.BiteSeconds:0}초마다 문다(피해의 {Pets.BiteShareAt(level) * 100:0}%)";
            string called = _client.PetName(pet);
            string kind = called == pet.Name ? "" : $"({pet.Name}) ";
            GUILayout.BeginHorizontal();
            Icon(PetSprite(pet));
            GUILayout.Label(Fit($"펫 {called} {kind}Lv {level} ({next}) · {gives}. 함께 싸우면 자란다", _textWidth - 40));
            GUILayout.EndHorizontal();
            // Its name (2026-11-09): typed, or one suggested (a phone may have no keyboard up).
            if (_petNameFor != pet.Id)
            {
                _petNameFor = pet.Id;
                _petName = _client.PetNames.TryGetValue(pet.Id, out var given) ? given : "";
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("이름", GUILayout.Width(40));
            GUI.SetNextControlName(PetNameControl);
            _petName = GUILayout.TextField(_petName ?? "", Pets.NameMost, GUILayout.Width(Mathf.Clamp(_textWidth - 220, 60, 140)));
            bool typing = GUI.GetNameOfFocusedControl() == PetNameControl;
            // Esc lets go of the field (the game's own Esc waits while typing).
            if (typing && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
            }
            bool enter = typing && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (Ui.LayoutButton("이름 짓기", GUILayout.Width(88)) || enter)
            {
                _client.NamePet(pet, _petName);
                _petName = _client.PetNames.TryGetValue(pet.Id, out var kept) ? kept : "";
                GUIUtility.keyboardControl = 0;
                Sounds.Play(Sound.Learn);
            }
            if (Ui.LayoutButton("추천", GUILayout.Width(56)))
            {
                var names = Pets.SuggestedNames;
                _petName = names[(Array.IndexOf(names.ToArray(), _petName) + 1 + UnityEngine.Random.Range(0, names.Count - 1)) % names.Count];
            }
            GUILayout.EndHorizontal();
        }

        const string PetNameControl = "PetName";
        string _petNameFor;
        string _petName;

        // Whether the pet's name field has the keyboard, so walking and hotkeys keep still while typing.
        public bool Typing => Open && _typing;
        bool _typing;

        Sprite PetSprite(PetDefinition pet)
        {
            var look = _client.MonsterKinds?.Monsters.FirstOrDefault(m => m.Id == pet.Look);
            var color = look != null && ColorUtility.TryParseHtmlString(look.Color, out var parsed) ? Sprites.ToRgba(parsed) : Rgba.Hex("#8CD94D");
            return Sprites.Centered($"PetIcon/{pet.Look}", () => CreatureArt.Monster(pet.Look, color, 0));
        }

        bool _showPassedGoals;

        static readonly RecordGroup[] BestiaryGroups = { RecordGroup.Monsters, RecordGroup.Bosses, RecordGroup.Fish, RecordGroup.Crops, RecordGroup.Gear };

        // Each group: how many of all there are found, the names of those found (with how many felled, for monsters
        // and bosses), and how many are still unknown.
        void DrawBestiary()
        {
            var records = _client.Records;
            int felled = Records.Progress(RecordGroup.Felled, records, System.Array.Empty<(RecordKind, string, string)>());
            JournalHeading($"잡은 몬스터 모두 {felled}마리");
            foreach (var group in BestiaryGroups)
            {
                var entries = _client.RecordEntries(group);
                if (entries.Count == 0) continue;
                int found = Records.Progress(group, records, entries);
                JournalHeading($"{Records.GroupName(group)} {found}/{entries.Count}");
                bool counted = group == RecordGroup.Monsters || group == RecordGroup.Bosses;
                var known = entries.Where(e => records.TryGetValue((e.Kind, e.Id), out int count) && count > 0)
                    .Select(e => counted ? $"{e.Name} {records[(e.Kind, e.Id)]}" : e.Name).ToList();
                GUILayout.Label(Ui.Wrap(known.Count == 0 ? "아직 없다" : string.Join(" · ", known), GUI.skin.label, _textWidth));
                if (found < entries.Count)
                {
                    var faint = GUI.color;
                    GUI.color = new Color(1, 1, 1, 0.5f);
                    GUILayout.Label(Fit($"모르는 것 {entries.Count - found}", _textWidth));
                    GUI.color = faint;
                }
            }
        }

        // Every achievement: done or how far along, and its reward (handed out the moment it is reached).
        void DrawAchievements()
        {
            int done = Records.Achievements.Count(a => _client.Achievements.Contains(a.Id));
            JournalHeading($"업적 {done}/{Records.Achievements.Count} · 이루면 보상을 바로 받는다");
            foreach (var achievement in Records.Achievements)
            {
                var entries = _client.RecordEntries(achievement.Group);
                int need = Records.Needed(achievement, entries);
                int have = Math.Min(need, Records.Progress(achievement.Group, _client.Records, entries));
                bool had = _client.Achievements.Contains(achievement.Id);
                var previous = GUI.color;
                if (had) GUI.color = new Color(0.75f, 1f, 0.75f);
                GUILayout.Label(Ui.Wrap($"{(had ? "[완료] " : "")}{achievement.Name} ({have}/{need}) · 보상 {Records.RewardText(achievement, _client.Loot)}",
                    GUI.skin.label, _textWidth));
                GUI.color = previous;
            }
        }

        // What the player is doing and has done: the guide's goal and how far along, the goals passed, each person's
        // story and today's requests, a rift or defense under way.
        // The journal's three pages: goals and stories, the bestiary, the achievements (Docs/Guide.md 도감과 업적).
        int _journalPage;
        static readonly string[] JournalPages = { "목표·이야기", "도감", "업적" };

        void JournalHeading(string text)
        {
            GUILayout.Space(6);
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.4f);
            GUILayout.Label(Fit(text, _textWidth));
            GUI.color = previous;
        }

        void DrawJournal()
        {
            GUILayout.BeginHorizontal();
            float pageWidth = (_textWidth - 8) / JournalPages.Length;
            for (int i = 0; i < JournalPages.Length; i++)
            {
                var previous = GUI.color;
                if (i == _journalPage) GUI.color = new Color(1f, 0.9f, 0.55f);
                if (Ui.LayoutButton(JournalPages[i], GUILayout.Width(pageWidth)) && _journalPage != i)
                {
                    _journalPage = i;
                    Sounds.Play(Sound.Click);
                }
                GUI.color = previous;
            }
            GUILayout.EndHorizontal();
            if (_journalPage == 1)
            {
                DrawBestiary();
                return;
            }
            if (_journalPage == 2)
            {
                DrawAchievements();
                return;
            }
            if (_client.Rift.Open) GUILayout.Label(Fit($"공허 균열 {_client.Rift.Tier}단계 · 남은 적 {_client.Rift.MonstersLeft} · {_client.Rift.SecondsLeft / 60}:{_client.Rift.SecondsLeft % 60:00}", _textWidth));
            if (_client.Defense.Active) GUILayout.Label(Fit($"기지 방어 {_client.Defense.Level}단계 · {_client.Defense.Wave}/{World.DefenseWaves} 물결 · 수호석 {_client.Defense.StonePercent}%", _textWidth));

            var heading = new Color(1f, 0.85f, 0.4f);
            void Heading(string text)
            {
                GUILayout.Space(6);
                var previous = GUI.color;
                GUI.color = heading;
                GUILayout.Label(Fit(text, _textWidth));
                GUI.color = previous;
            }

            var guide = Guide;
            if (guide != null)
            {
                Heading($"목표 안내 {guide.Done}/{guide.Total}");
                var goal = guide.Current;
                if (goal == null) GUILayout.Label(Fit("안내한 목표를 모두 마쳤다", _textWidth));
                else
                {
                    string progress = GuideProgress.Progress(goal, GuideFacts.Of(_client)) is string p ? $" ({p})" : "";
                    GUILayout.Label(Fit($"지금: {goal.Text}{progress}", _textWidth));
                    GUILayout.Label(Fit(goal.Hint, _textWidth));
                    if (goal.Rewards.Count > 0) GUILayout.Label(Fit($"보상: {goal.RewardText}", _textWidth));
                }
                if (guide.Done > 0 && Ui.LayoutButton(_showPassedGoals ? "마친 목표 접기" : $"마친 목표 보기 ({guide.Done})")) _showPassedGoals = !_showPassedGoals;
                if (_showPassedGoals)
                {
                    foreach (var passed in guide.PassedGoals) GUILayout.Label(Fit($"· {passed.Text}", _textWidth));
                }
            }

            var people = _client.Npcs?.Npcs ?? (IReadOnlyList<NpcDefinition>)Array.Empty<NpcDefinition>();
            Heading("이야기");
            foreach (var npc in people)
            {
                var story = _client.Npcs.StoryOf(npc);
                if (story.Count == 0) continue;
                int done = _client.StorySteps.TryGetValue(npc.Id, out int d) ? d : 0;
                var next = _client.StoryStepOf(npc);
                string state = next == null ? "모두 들었다"
                    : _client.HeartsWith(npc) < next.Hearts ? $"다음: {next.Title} (호감 {next.Hearts}에 열린다)"
                    : $"다음: {next.Title}";
                GUILayout.Label(Fit($"{npc.Name} {done}/{story.Count} · {state}", _textWidth));
            }

            Heading("오늘의 부탁");
            bool any = false;
            foreach (var npc in people)
            {
                if (!_client.Friendships.ContainsKey(npc.Id)) continue;
                var request = _client.RequestOf(npc);
                if (request == null) continue;
                any = true;
                GUILayout.Label(Fit($"{npc.Name}: {request.NeedText} → {request.RewardText}{(_client.RequestDoneToday(npc) ? " (마침)" : "")}", _textWidth));
            }
            if (!any) GUILayout.Label(Fit("만난 사람에게 말을 걸면 그날의 부탁을 알려 준다", _textWidth));
        }

        // The guide's progress for the journal (GameRoot hands it over when the guide starts).
        public GuideProgress Guide { get; set; }

        // A small gold dot in a button's top right corner: something to do there (points to spend).
        internal static void Badge(Rect button)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.1f, 0.06f, 0.02f);
            GUI.DrawTexture(new Rect(button.xMax - 13, button.y + 3, 10, 10), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.8f, 0.25f);
            GUI.DrawTexture(new Rect(button.xMax - 12, button.y + 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // Text broken between words to fit the width (Ui.Wrap).
        // The playtests learn which row a button is on from the text written before it (Ui.Row).
        static string Fit(string text, float width)
        {
            Ui.Row(text);
            return Ui.Wrap(text, GUI.skin.label, width);
        }

        // Scrolls the open page by whole lines per notch while the pointer is over the window.
        void Wheel(Rect area)
        {
            var e = Event.current;
            if (e.type != EventType.ScrollWheel || !area.Contains(e.mousePosition)) return;
            int page = (int)_page;
            float notches = Mathf.Sign(e.delta.y) * Mathf.Clamp(Mathf.Abs(e.delta.y), 1, 3);
            _scroll[page].y = Mathf.Max(0, _scroll[page].y + notches * WheelStep);
            e.Use();
        }

        // The gear page as a grid, the way Terraria and Core Keeper show a bag: the six worn slots in a row, then
        // every carried item, consumable and rune as a square with its icon, rarity edge and count. Pressing one picks
        // it, and the panel under the grid tells what it is and holds its buttons ([장착], [버리기], [마시기], ...).
        // With a rune picked, an item's panel offers [끼우기]. Stats and life skills fold away at the bottom.
        void DrawGear()
        {
            DrawRefining();

            float cell = Controls.UsingTouch ? 48 : 44;
            GUILayout.Label(Fit("착용", _textWidth));
            var slots = (ItemSlot[])Enum.GetValues(typeof(ItemSlot));
            var worn = Grid(slots.Length, cell);
            for (int i = 0; i < slots.Length; i++)
            {
                var item = _client.Equipment[slots[i]];
                var area = CellAt(worn, i, cell);
                if (item == null)
                {
                    EmptyCell(area, ItemText.SlotName(slots[i]));
                    continue;
                }
                if (Cell(area, Sprites.Icon(item.Base), ItemText.Name(item), null, Palette.Of(item.Rarity), _picked == item.Id, ItemTooltip(item)))
                {
                    Pick(item.Id, null);
                }
            }

            GUILayout.Space(4);
            var items = _client.Bag.Items.ToList();
            var consumables = _client.Bag.Consumables.ToList();
            var runes = _client.Bag.Runes.ToList();
            float wait = _client.ConsumableCooldownLeft;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit($"소지품 {items.Count}/{Inventory.Capacity}" + (wait > 0 ? $" · 소모품은 {Mathf.CeilToInt(wait)}초 뒤에" : ""), _textWidth - 68), GUILayout.Width(_textWidth - 68));
            // Gear in slot order, the best first (Inventory.Sort).
            GUI.enabled = items.Count > 1;
            if (Ui.LayoutButton("정렬", GUILayout.Width(60))) Send(_client.SortBagCommand(), Sound.Click);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            int count = Math.Max(items.Count + consumables.Count + runes.Count, 1);
            var grid = Grid(count, cell);
            int index = 0;
            foreach (var item in items)
            {
                bool wearable = item.Base.RequiredLevel <= _client.Level;
                if (Cell(CellAt(grid, index++, cell), Sprites.Icon(item.Base), ItemText.Name(item), wearable ? null : $"Lv{item.Base.RequiredLevel}",
                    Palette.Of(item.Rarity), _picked == item.Id, ItemTooltip(item), dim: !wearable))
                {
                    Pick(item.Id, null);
                }
            }
            foreach (var (consumable, number) in consumables)
            {
                if (Cell(CellAt(grid, index++, cell), Sprites.Icon(consumable), $"{consumable.Name} ×{number}", number.ToString(), Palette.Potion,
                    _pickedConsumable == consumable, consumable.Description))
                {
                    Pick(0, consumable);
                }
            }
            foreach (var (rune, number) in runes)
            {
                var tooltip = string.Join("\n", new[] { SlotCategory.Weapon, SlotCategory.Armor, SlotCategory.Jewelry }
                    .Select(c => $"{CategoryName(c)}: {string.Join(", ", rune.ModifiersFor(c).Select(StatText.Describe))}"));
                if (Cell(CellAt(grid, index++, cell), Sprites.Icon(rune), $"{rune.Name} ×{number}", number.ToString(), Palette.Rune,
                    _selectedRune == rune, $"{rune.Name}\n{tooltip}"))
                {
                    _selectedRune = _selectedRune == rune ? null : rune;
                    Sounds.Play(Sound.Click);
                }
            }
            if (index == 0) GUILayout.Label(Fit("가방이 비었다", _textWidth));

            DrawPicked();

            GUILayout.Space(8);
            GUILayout.Label(Fit("능력치와 생활 기술은 [캐릭터] 탭에 있다", _textWidth));
        }
        // The item (by id) or consumable picked in the grid; nothing picked when both are empty.
        int _picked;
        ConsumableDefinition _pickedConsumable;

        // Pressing a square always picks it (pressing it again keeps it picked), so a press never hides the buttons.
        void Pick(int item, ConsumableDefinition consumable)
        {
            _picked = item;
            _pickedConsumable = consumable;
            Sounds.Play(Sound.Click);
        }

        // What is picked, under the grid: its icon and name, what it is, and what can be done with it. A rune picked
        // for socketing says so here too.
        void DrawPicked()
        {
            if (_selectedRune != null)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(_selectedRune));
                GUILayout.Label(Fit($"{_selectedRune.Name}: 끼울 아이템을 고르고 [끼우기]. 넣은 룬은 모루에서 정수를 들여야 뺄 수 있다", _textWidth - IconRoom - 64));
                if (Ui.LayoutButton("그만", GUILayout.Width(56))) _selectedRune = null;
                GUILayout.EndHorizontal();
            }

            var item = _picked == 0 ? null : _client.Equipment.Items.Concat(_client.Bag.Items).FirstOrDefault(i => i.Id == _picked);
            if (item == null) _picked = 0;
            if (_pickedConsumable != null && _client.Bag.CountOf(_pickedConsumable) == 0) _pickedConsumable = null;
            if (item == null && _pickedConsumable == null)
            {
                if (_selectedRune == null) GUILayout.Label(Fit(Controls.UsingTouch ? "칸을 누르면 여기에 자세히 나온다" : "칸을 누르면 여기에 자세히 나온다(마우스를 올려도 설명)", _textWidth));
                return;
            }

            GUILayout.Space(4);
            GUILayout.BeginVertical(GUI.skin.box);
            float width = _textWidth - GUI.skin.box.padding.horizontal;
            if (item != null)
            {
                bool wearing = _client.Equipment[item.Base.Slot] == item;
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(item.Base));
                var previous = GUI.color;
                GUI.color = Palette.Of(item.Rarity);
                GUILayout.Label(Fit(ItemText.Name(item) + (wearing ? " (착용 중)" : ""), width - IconRoom));
                GUI.color = previous;
                GUILayout.EndHorizontal();
                GUILayout.Label(Ui.Wrap(string.Join("\n", ItemText.Describe(item).Skip(1).Select(line => line.Trim())), GUI.skin.label, width));
                // Against what is worn in its slot: what putting it on would change.
                var worn = _client.Equipment[item.Base.Slot];
                if (!wearing && worn != null)
                {
                    var change = Compare(item, worn);
                    var previousColor = GUI.color;
                    GUI.color = new Color(0.85f, 0.9f, 1f);
                    GUILayout.Label(Ui.Wrap(change.Count == 0 ? $"지금 낀 {ItemText.Name(worn)}와 같다" : $"지금 낀 {ItemText.Name(worn)}보다: {string.Join(", ", change)}", GUI.skin.label, width));
                    GUI.color = previousColor;
                }
                GUILayout.BeginHorizontal();
                if (wearing)
                {
                    if (Ui.LayoutButton("벗기", GUILayout.Width(64))) Send(_client.UnequipCommand(item.Base.Slot), Sound.Click);
                }
                else
                {
                    // Too high a level to wear yet: the button says which level instead.
                    bool wearable = item.Base.RequiredLevel <= _client.Level;
                    GUI.enabled = wearable;
                    if (Ui.LayoutButton(wearable ? "장착" : $"Lv{item.Base.RequiredLevel}", GUILayout.Width(64))) Send(_client.EquipCommand(item.Id), Sound.Place);
                    GUI.enabled = true;
                    if (Ui.LayoutButton("버리기", GUILayout.Width(64))) Send(_client.DiscardCommand(item.Id), Sound.Click);
                }
                RuneButton(item);
                RefineButton(item);
                QuickButton(_hotbar?.SlotOf(item), () => _hotbar.Toggle(item));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                var consumable = _pickedConsumable;
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(consumable));
                GUILayout.Label(Fit($"{consumable.Name} ×{_client.Bag.CountOf(consumable)}", width - IconRoom));
                GUILayout.EndHorizontal();
                GUILayout.Label(Ui.Wrap(consumable.Description, GUI.skin.label, width));
                GUILayout.BeginHorizontal();
                // A potion is drunk; a food is eaten, and still gives its buff at full health.
                if (Ui.LayoutButton(consumable.IsFood ? "먹기" : consumable.Recalls ? "쓰기" : "마시기", GUILayout.Width(64)))
                {
                    byte[] command = _client.UseConsumableCommand(consumable);
                    if (command != null) _send(command);
                }
                QuickButton(_hotbar?.SlotOf(consumable), () => _hotbar.Toggle(consumable));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        // What wearing the item instead of the other would change, stat by stat ("피해 +12", "방어도 -3").
        static List<string> Compare(Item item, Item other)
        {
            var totals = new Dictionary<(StatType, ModifierKind, SkillTag), int>();
            void Add(Item from, int sign)
            {
                foreach (var modifier in from.Modifiers())
                {
                    var key = (modifier.Stat, modifier.Kind, modifier.Tags);
                    totals[key] = (totals.TryGetValue(key, out int value) ? value : 0) + sign * modifier.Value;
                }
            }
            Add(item, 1);
            Add(other, -1);
            return totals.Where(pair => pair.Value != 0)
                .Select(pair => StatText.Describe(new Modifier(pair.Key.Item1, pair.Key.Item2, pair.Value, pair.Key.Item3)))
                .ToList();
        }

        // The item's description for the pointer's box, as the old rows had it.
        static string ItemTooltip(Item item) => string.Join("\n", ItemText.Describe(item));

        const float CellGap = 4;

        // Room in the layout for so many cells, as many to a row as fit.
        Rect Grid(int count, float cell)
        {
            int columns = Columns(cell);
            int rows = (count + columns - 1) / columns;
            return GUILayoutUtility.GetRect(_textWidth, rows * cell + (rows - 1) * CellGap, GUILayout.Width(_textWidth));
        }

        int Columns(float cell) => Math.Max(1, (int)((_textWidth + CellGap) / (cell + CellGap)));

        Rect CellAt(Rect grid, int index, float cell)
        {
            int columns = Columns(cell);
            return new Rect(grid.x + index % columns * (cell + CellGap), grid.y + index / columns * (cell + CellGap), cell, cell);
        }

        static GUIStyle _countStyle;
        static GUIStyle _slotStyle;

        // A square with the thing's icon, an edge in its colour (rarity, potion, rune), a count in the corner and a
        // bright edge while picked. Its name is what the playtests press it by (Ui.Probe). True when pressed.
        // The description of whatever the pointer is over in this frame's drawing, for DrawTooltip.
        static string _hover = "";

        static void Hover(Rect area, string tooltip)
        {
            if (!string.IsNullOrEmpty(tooltip) && Event.current.type == EventType.Repaint && area.Contains(Event.current.mousePosition)) _hover = tooltip;
        }

        static void TipLabel(GUIContent content, params GUILayoutOption[] options)
        {
            GUILayout.Label(content, options);
            Hover(GUILayoutUtility.GetLastRect(), content.tooltip);
        }

        static bool TipButton(GUIContent content, params GUILayoutOption[] options)
        {
            bool pressed = Ui.LayoutButton(content, options);
            Hover(GUILayoutUtility.GetLastRect(), content.tooltip);
            return pressed;
        }

        static bool Cell(Rect area, Sprite icon, string name, string corner, Color edge, bool picked, string tooltip, bool dim = false)
        {
            Ui.Probe(name, area);
            bool pressed = GUI.Button(area, GUIContent.none);
            Hover(area, tooltip);
            var previous = GUI.color;
            GUI.color = new Color(edge.r, edge.g, edge.b, 0.8f);
            Frame(area, 2);
            if (picked)
            {
                GUI.color = Color.white;
                Frame(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), 2);
            }
            GUI.color = dim ? new Color(1, 1, 1, 0.5f) : Color.white;
            float inset = Mathf.Round(area.width * 0.18f);
            Sprites.DrawInGui(new Rect(area.x + inset, area.y + inset, area.width - inset * 2, area.height - inset * 2), icon);
            GUI.color = previous;
            if (!string.IsNullOrEmpty(corner))
            {
                _countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                Ui.Text(_countStyle, 12, bold: true);
                var box = new Rect(area.x + 2, area.y + 2, area.width - 5, area.height - 4);
                GUI.color = new Color(0, 0, 0, 0.9f);
                GUI.Label(new Rect(box.x + 1, box.y + 1, box.width, box.height), corner, _countStyle);
                GUI.color = previous;
                GUI.Label(box, corner, _countStyle);
            }
            return pressed;
        }

        // A worn slot with nothing in it: its name, faint.
        static void EmptyCell(Rect area, string slot)
        {
            _slotStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0), wordWrap = true };
            Ui.Text(_slotStyle, 12);
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.35f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(area, slot, _slotStyle);
            GUI.color = previous;
        }

        static void Frame(Rect area, float width)
        {
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.yMax - width, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.y, width, area.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.xMax - width, area.y, width, area.height), Texture2D.whiteTexture);
        }

        void DrawMaterialsPage()
        {
            DrawMaterials();
            DrawProduction();
            DrawCrafting();
        }

        // Whether the machine makes anything from the material (World.Takes, as far as the client can tell).
        bool Takes(MachineView machine, MaterialDefinition material) => machine.Kind switch
        {
            TileType.Assembler => machine.Recipe != null && machine.Recipe.Ingredients.Any(i => i.Material == material.Kind),
            TileType.Coop or TileType.Barn or TileType.Fold => material.Id == World.HouseOf(machine.Kind).Animal || material.Id == World.Feed,
            TileType.CrabPot => material.Id == World.Bait,
            TileType.Turret => World.IsAmmo(material),
            TileType.Harvester => _client.CropFrom(material) != null,
            _ => _client.ProcessesOf(machine.Kind).Any(r => r.Ingredients.Any(i => i.Material == material.Kind)),
        };

        bool _showProduction;

        // What the machines made lately (World.Production), folded away until asked for. Only once anything was made.
        void DrawProduction()
        {
            var made = _client.Production;
            if (made.Count == 0) return;
            GUILayout.Space(6);
            int minutes = Mathf.Max(1, Mathf.RoundToInt(_client.ProductionMinutes));
            if (Ui.LayoutButton(_showProduction ? $"공장 생산 접기 (최근 {minutes}분)" : $"공장 생산 보기 (최근 {minutes}분, {made.Count}가지)")) _showProduction = !_showProduction;
            if (!_showProduction) return;
            foreach (var (thing, count) in made)
            {
                GUILayout.BeginHorizontal();
                var material = thing as MaterialDefinition;
                var consumable = thing as ConsumableDefinition;
                Icon(material != null ? Sprites.Icon(material) : Sprites.Icon(consumable));
                string name = material?.Name ?? consumable?.Name;
                float perMinute = count / Mathf.Max(1 / 60f, _client.ProductionMinutes);
                GUILayout.Label(Fit($"{name} {count}개 · 분당 {perMinute:0.#}", _textWidth - IconRoom));
                GUILayout.EndHorizontal();
            }
        }

        // The hovered or held item's description, only while the pointer is over the list, so a description left over
        // from another tab never shows. Beside the window at the pointer's height when there is room there (a hint
        // waits there while nothing is hovered); otherwise next to the pointer, above a finger so it is not under it,
        // and kept on the screen. Drawn outside the layout so its changing size never shifts the window's controls.
        void DrawTooltip(Rect window, bool besideWindow)
        {
            var pointer = Event.current.mousePosition;
            bool hovering = !string.IsNullOrEmpty(_hover) && _rows.Contains(pointer);
            if (!hovering && !besideWindow) return;
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            string text = hovering ? _hover
                : Controls.UsingTouch ? "이름이나 칸을 누르고 있으면 설명이 보입니다." : "이름이나 칸에 마우스를 올리면 설명이 보입니다.";
            float room = Mathf.Min(TooltipWidth, Ui.Width - 20);
            var content = new GUIContent(Ui.Wrap(text, _tooltipStyle, room));
            // As wide as the longest line needs, so short descriptions get a small box.
            float width = Mathf.Min(room, Mathf.Ceil(_tooltipStyle.CalcSize(content).x) + 2);
            float height = _tooltipStyle.CalcHeight(content, width);
            float x;
            float y;
            if (besideWindow)
            {
                x = window.x - width - 10;
                y = hovering ? pointer.y - 12 : Mathf.Max(window.y, Ui.HudArea.yMax + 10);
            }
            else
            {
                x = pointer.x + 16;
                if (x + width > Ui.Width - 10) x = pointer.x - width - 16;
                y = Controls.UsingTouch ? pointer.y - height - 28 : pointer.y + 20;
                if (y + height > Ui.Height - 10) y = pointer.y - height - 12;
                if (y < 10) y = pointer.y + 28;
            }
            x = Mathf.Clamp(x, 10, Mathf.Max(10, Ui.Width - width - 10));
            y = Mathf.Clamp(y, 10, Mathf.Max(10, Ui.Height - height - 10));
            GUI.Box(new Rect(x, y, width, height), content, _tooltipStyle);
        }

        // With the classes, what each damaging skill would hit for now: the same tag rules as the host, so swapping
        // gear or starting a buff shows its effect on every skill.
        void DrawStats()
        {
            var stats = _client.Stats;
            GUILayout.Label(Fit(
                $"생명력 {stats.MaxHealth:0} · 재생 {stats.HealthRegen:0.#}/초 · 마나 {stats.MaxMana:0} · 재생 {stats.ManaRegen:0.#}/초 · 방어도 {stats.Armor:0} · 이동 {stats.MoveSpeed:0.#}\n" +
                $"{stats.Attack.Name} 피해 {stats.Damage:0.#} · 초당 공격 {stats.AttacksPerSecond:0.##} · 치명타 {stats.CritChance:0.#}% (×{stats.CritDamage:0}%) · 아이템 희귀도 +{stats.ItemRarity:0}%",
                _textWidth));
            if (_client.Classes.Count == 0) return;
            var hits = _client.Skills
                .Where(skill => skill.Damage > 0)
                .Select(skill => $"{skill.Name} {stats.DamageFor(stats.ContextFor(skill)) * skill.Damage / 100f * SkillRules.DamageScale(_client.PowerOf(skill)):0.#}");
            GUILayout.Label(Fit($"클래스 {string.Join(" + ", _client.Classes.Select(c => c.Name))} · 스킬 피해 {string.Join(" · ", hits)}", _textWidth));
        }

        // Each life skill's level, how far to the next, and what it gives (World.Life.cs).
        void DrawLifeSkills()
        {
            GUILayout.Space(6);
            GUILayout.Label("생활 기술");
            foreach (LifeSkill skill in Enum.GetValues(typeof(LifeSkill)))
            {
                int experience = _client.LifeExperience[(int)skill];
                int level = LifeSkills.LevelOf(experience);
                string next = level >= LifeSkills.MaxLevel ? "최고" : $"{experience - LifeSkills.TotalFor(level)}/{LifeSkills.TotalFor(level + 1) - LifeSkills.TotalFor(level)}";
                GUILayout.Label(Fit($"{LifeSkills.Name(skill)} {level} ({next}) · {LifeSkills.Perk(skill, level)}", _textWidth));
                // The special perks (2026-11-09): picked, or two to pick from once the level is reached.
                for (int tier = 0; tier < LifeSkills.PerkTiers; tier++)
                {
                    int picked = _client.LifePerks[LifeSkills.PerkIndex(skill, tier)];
                    int at = LifeSkills.PerkLevels[tier];
                    if (picked != 0)
                    {
                        var (name, text) = LifeSkills.PerkOf(skill, tier, picked);
                        GUILayout.Label(Fit($"  {at}레벨 혜택: {name} - {text}", _textWidth));
                        continue;
                    }
                    if (level < at) continue;
                    GUILayout.Label(Fit($"  {at}레벨 혜택을 하나 고른다(바꿀 수 없다)", _textWidth));
                    for (int choice = 1; choice <= 2; choice++)
                    {
                        var (name, text) = LifeSkills.PerkOf(skill, tier, choice);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Fit($"  {name}: {text}", _textWidth - 72), GUILayout.Width(_textWidth - 72));
                        if (Ui.LayoutButton("고르기", GUILayout.Width(64))) Send(_client.ChoosePerkCommand(skill, tier, choice), Sound.Learn);
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        // Materials, counted by kind. One that builds gets [놓기], a seed gets [심기] and the watering can [물 주기];
        // all act on the tile the player faces.
        // The cracked built tile ahead, when there is one (GameRoot sets it from the view): mended from the bag.
        public TileType? CrackAhead { get; set; }

        void DrawMaterials()
        {
            if (CrackAhead is TileType cracked)
            {
                string name = _client.TileName(cracked);
                string cost = cracked == TileType.IronWall ? "철 주괴 1" : "돌 1";
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"앞의 {name}에 금이 갔다", _textWidth - 120), GUILayout.Width(_textWidth - 120));
                if (Ui.LayoutButton($"고치기 ({cost})", GUILayout.Width(112))) Send(_client.RepairCommand(), Sound.Place);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("재료");
            if (_client.Bag.Materials.Count == 0) GUILayout.Label(Fit("아직 없다. 벽을 캐거나 몬스터를 잡으면 모인다.", _textWidth));
            // The machine the player faces, for the filter and generator buttons.
            var ahead = _client.MachineAhead();
            if (ahead?.Kind == TileType.Filter)
            {
                GUILayout.BeginHorizontal();
                string picks = ahead.Value.Settings.Count == 0 ? "정하지 않음" : string.Join(", ", ahead.Value.Settings.Select(m => m.Name));
                GUILayout.Label(Fit($"거름망 분배기: {picks} (재료의 [거르기]로 {World.FilterPicks}개까지 더하고 [빼기]로 뺀다)", _textWidth - 64));
                if (ahead.Value.Settings.Count > 0 && Ui.LayoutButton("비우기", GUILayout.Width(56))) Send(_client.SetFilterCommand(null), Sound.Click);
                GUILayout.EndHorizontal();
            }
            // A machine that makes things: what it holds and has made, [꺼내기], and for a keg or a jar what it makes.
            bool loads = ahead != null && World.LoadsByHand(ahead.Value.Kind) && ahead.Value.Kind != TileType.Generator;
            if (ahead != null && (loads || ahead.Value.Held > 0))
            {
                var machine = ahead.Value;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"{_client.TileName(machine.Kind)}: 넣은 것 {machine.Holding} · 만든 것 {machine.Held}", _textWidth - 72));
                if (machine.Held > 0 && Ui.LayoutButton("꺼내기", GUILayout.Width(64))) Send(_client.UnloadCommand(), Sound.Pickup);
                GUILayout.EndHorizontal();
                if (machine.Kind == TileType.Coop)
                    GUILayout.Label(Fit($"닭 {machine.Animals}/{World.CoopChickens}마리. 사료가 있으면 하루(20분)마다 닭마다 달걀 하나", _textWidth));
                if (machine.Kind == TileType.Barn)
                    GUILayout.Label(Fit($"소 {machine.Animals}/{World.HouseOf(TileType.Barn).Most}마리. 사료 둘이면 하루(20분)마다 소마다 우유 하나", _textWidth));
                if (machine.Kind == TileType.Fold)
                    GUILayout.Label(Fit($"양 {machine.Animals}/{World.HouseOf(TileType.Fold).Most}마리. 사료가 있으면 이틀(40분)마다 양마다 양털 하나", _textWidth));
                if (World.HouseOf(machine.Kind) != null && machine.Animals > 0)
                {
                    // Affection (World.PetAnimals): once a day, and fonder animals give silver and gold more often.
                    GUILayout.BeginHorizontal();
                    string hearts = new string('■', machine.Affection) + new string('□', World.AffectionMost - machine.Affection);
                    GUILayout.Label(Fit($"애정 {hearts} {machine.Affection}/{World.AffectionMost} · 높을수록 은빛·금빛이 잘 나온다", _textWidth - 104), GUILayout.Width(_textWidth - 104));
                    if (machine.Petted) GUILayout.Label("오늘 쓰다듬음", GUILayout.Width(96));
                    else if (Ui.LayoutButton("쓰다듬기", GUILayout.Width(96))) Send(_client.PetAnimalsCommand(), Sound.Harvest);
                    GUILayout.EndHorizontal();
                }
                if (machine.Kind == TileType.CrabPot)
                    GUILayout.Label(Fit("바로 옆이 물이어야 한다. 미끼 하나로 15분에 하나씩 든다", _textWidth));
                if (machine.Kind == TileType.ArcFurnace)
                    GUILayout.Label(Fit(machine.Powered ? "전력으로 돈다. 광석 둘에 주괴 하나, 두 개마다 하나를 더 만든다" : "전력이 없어 멈춰 있다. 발전기·물레방아·태양광 판이 6타일 안에 있거나 전신주로 이어져야 한다", _textWidth));
                if (machine.Kind == TileType.Harvester)
                    GUILayout.Label(Fit("씨앗을 넣으면 앞쪽 3×3칸의 빈 밭에 심는다", _textWidth));
                if (machine.Kind == TileType.Keg || machine.Kind == TileType.PreservesJar)
                {
                    string makes = string.Join(" · ", _client.ProcessesOf(machine.Kind)
                        .Select(r => $"{string.Join("+", r.Ingredients.Select(i => $"{i.Material.Name} {i.Count}"))} → {r.Name}"));
                    GUILayout.Label(Fit($"만드는 것: {makes}", _textWidth));
                }
            }
            // Materials with something to do with them (build, plant, use, or put in the machine ahead) are rows with
            // their buttons; the rest sit in a grid of squares under them, as the gear page's bag does.
            var plain = new List<KeyValuePair<MaterialDefinition, int>>();
            foreach (var pair in _client.Bag.Materials.ToList())
            {
                bool acts = pair.Key.Places != null || _client.CropFrom(pair.Key) != null || IsTool(pair.Key) || Pets.ByEgg(pair.Key.Id) != null || ahead?.Kind == TileType.Filter
                    || ahead?.Kind == TileType.Generator && pair.Key.Fuel > 0 || loads && Takes(ahead.Value, pair.Key);
                if (!acts)
                {
                    plain.Add(pair);
                    continue;
                }
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                TipLabel(new GUIContent(Fit($"{pair.Key.Name} ×{pair.Value}", _nameWidth), pair.Key.Description), GUILayout.Width(_nameWidth));
                if (pair.Key.Places != null)
                {
                    if (Ui.LayoutButton("놓기", GUILayout.Width(56))) Send(_client.PlaceCommand(pair.Key), Sound.Place);
                    if (TileMap.IsPaving(pair.Key.Places.Value)) Soil();
                }
                var crop = _client.CropFrom(pair.Key);
                if (crop != null)
                {
                    if (Ui.LayoutButton("심기", GUILayout.Width(56))) Send(_client.PlantCommand(crop), Sound.Place);
                    Soil();
                }
                if (pair.Key.Id == World.WateringCan)
                {
                    if (Ui.LayoutButton("물 주기", GUILayout.Width(64))) Send(_client.WaterCommand(), Sound.Water);
                    Soil();
                }
                if (pair.Key.Id == World.FishingRod && Ui.LayoutButton(FishingLabel(_client), GUILayout.Width(72))) Send(_client.FishCommand(), Sound.Cast);
                if (pair.Key.Id == World.Hoe)
                {
                    if (Ui.LayoutButton("밭 갈기", GUILayout.Width(64))) Send(_client.TillCommand(), Sound.Dig);
                    Soil();
                }
                // Facing a filter, any material can be what it picks out; facing a generator, fuel goes in.
                if (ahead?.Kind == TileType.Filter && Ui.LayoutButton(ahead.Value.Settings.Contains(pair.Key.Kind) ? "빼기" : "거르기", GUILayout.Width(56)))
                    Send(_client.SetFilterCommand(pair.Key), Sound.Click);
                if (ahead?.Kind == TileType.Generator && pair.Key.Fuel > 0 && Ui.LayoutButton("연료", GUILayout.Width(48))) Send(_client.RefuelCommand(pair.Key), Sound.Place);
                // Facing a machine that makes things from this, it goes in (all of it, up to what the machine holds).
                if (loads && Takes(ahead.Value, pair.Key) && Ui.LayoutButton("넣기", GUILayout.Width(48)))
                    Send(_client.LoadCommand(pair.Key, Math.Min(pair.Value, World.MaxTrade)), Sound.Place);
                if (pair.Key.Id == World.Fertilizer)
                {
                    if (Ui.LayoutButton("거름 주기", GUILayout.Width(72))) Send(_client.FertilizeCommand(), Sound.Place);
                    Soil();
                }
                // An egg hatches into the pet that follows the player (Pets.cs); the one they had goes back to its egg.
                var hatches = Pets.ByEgg(pair.Key.Id);
                if (hatches != null)
                {
                    GUI.enabled = _client.Pet != hatches;
                    if (Ui.LayoutButton("부화", GUILayout.Width(56))) Send(_client.HatchCommand(pair.Key), Sound.Learn);
                    GUI.enabled = true;
                }
                // Only what a quick slot can use: things that build, seeds and the tools.
                if (pair.Key.Places != null || crop != null || IsTool(pair.Key))
                {
                    var material = pair.Key;
                    QuickButton(_hotbar?.SlotOf(material), () => _hotbar.Toggle(material));
                }
                GUILayout.EndHorizontal();
            }
            if (plain.Count == 0) return;
            GUILayout.Space(4);
            float cell = Controls.UsingTouch ? 48 : 44;
            var grid = Grid(plain.Count, cell);
            for (int i = 0; i < plain.Count; i++)
            {
                var (material, count) = (plain[i].Key, plain[i].Value);
                if (Cell(CellAt(grid, i, cell), Sprites.Icon(material), $"{material.Name} ×{count}", count.ToString(), Palette.Material, _pickedMaterial == material,
                    $"{material.Name} ×{count}\n{material.Description}"))
                {
                    _pickedMaterial = material;
                    Sounds.Play(Sound.Click);
                }
            }
            if (_pickedMaterial != null && plain.Any(p => p.Key == _pickedMaterial))
            {
                GUILayout.Label(Fit($"{_pickedMaterial.Name} ×{_client.Bag.CountOf(_pickedMaterial)}: {_pickedMaterial.Description}", _textWidth));
            }
        }

        MaterialDefinition _pickedMaterial;

        // Both sides of the chest: what it holds, each with [꺼내기], and what the bag holds, each with [넣기]. Stacks
        // move whole.
        enum ShopMode
        {
            Buy,
            Sell,
            Gift,
            Request,
            Story,
        }

        ShopMode _shopMode;
        static readonly string[] ShopModeNames = { "사기", "팔기", "선물", "부탁", "이야기" };

        // The merchant (World.Merchant.cs): who she is and how well this player knows her, today's line, a chat once a
        // day, and buying, selling and a gift once a day.
        void DrawMerchant()
        {
            var near = _client.NpcNear();
            if (near == null) return;
            var npc = near.Value.Npc;
            int hearts = _client.HeartsWith(npc);
            var coin = _client.Bag.Materials.Keys.FirstOrDefault(m => m.Id == World.Coin);
            int coins = coin == null ? 0 : _client.Bag.CountOf(coin);
            GUILayout.Label(Fit($"{npc.Name} · 호감 {hearts}/{World.MaxHearts} ({_client.PointsWith(npc)}/{World.MaxHearts * World.PointsPerHeart}) · 금화 {coins}", _textWidth));
            GUILayout.Label(Fit($"\"{npc.LineFor(_client.Today, hearts)}\"", _textWidth));
            // The stories told with each heart gift, once had.
            foreach (var story in npc.Stories.OrderBy(p => p.Key))
            {
                if (_client.HadHeartGift(npc, story.Key)) GUILayout.Label(Fit($"(호감 {story.Key}) \"{story.Value}\"", _textWidth));
            }
            GUILayout.BeginHorizontal();
            var enabled = GUI.enabled;
            GUI.enabled = enabled && !_client.TalkedToday(npc);
            if (Ui.LayoutButton(_client.TalkedToday(npc) ? "오늘은 이야기했다" : "말 걸기")) Send(_client.TalkCommand(), Sound.Click);
            // Moving in (Docs/Npcs.md, 이사): [이사 권하기] once they like the player enough and a room is free.
            var (home, fault) = HousingOf(npc, near.Value.X, near.Value.Y);
            if (!home)
            {
                GUI.enabled = enabled && fault == null;
                if (Ui.LayoutButton("이사 권하기")) Send(_client.InviteCommand(), Sound.Click);
            }
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            if (home) GUILayout.Label(Fit($"기지의 방에 산다: 말 걸기와 선물로 오르는 호감이 {Housing.FriendshipBonus}배다.", _textWidth));
            else if (fault != null) GUILayout.Label(Fit($"이사: {fault}", _textWidth));
            GUILayout.Label(Fit($"호감이 오를 때마다 사는 값은 {World.HeartPercent}% 싸지고 파는 값은 {World.HeartPercent}% 비싸진다.", _textWidth));
            int mode = Ui.Toolbar((int)_shopMode, ShopModeNames);
            if (mode != (int)_shopMode)
            {
                _shopMode = (ShopMode)mode;
                Sounds.Play(Sound.Click);
            }
            GUILayout.Space(4);
            if (_shopMode == ShopMode.Buy) DrawBuying(npc, hearts, coins);
            else if (_shopMode == ShopMode.Sell) DrawSelling(npc, hearts);
            else if (_shopMode == ShopMode.Gift) DrawGifts(npc);
            else if (_shopMode == ShopMode.Request) DrawRequest(npc);
            else DrawStory(npc, hearts);
        }

        // Whether the person lives in a room and why they could not move in, worked out from the map once a frame
        // (IMGUI draws the window several times a frame).
        (bool Home, string Fault) HousingOf(NpcDefinition npc, int x, int y)
        {
            if (_housingFrame != Time.frameCount || _housingNpc != npc)
            {
                _housingFrame = Time.frameCount;
                _housingNpc = npc;
                _housing = _client.LivesHere(npc, x, y) ? (true, null) : (false, _client.InviteFault(npc, x, y));
            }
            return _housing;
        }

        int _housingFrame = -1;
        NpcDefinition _housingNpc;
        (bool Home, string Fault) _housing;

        // The person's story (Docs/Npcs.md, 이야기): the steps done, the next one with what it needs and gives, and
        // [건네기] once the bag has it all; the step waits for its hearts.
        void DrawStory(NpcDefinition npc, int hearts)
        {
            var story = _client.Npcs?.StoryOf(npc) ?? new List<RequestDefinition>();
            if (story.Count == 0)
            {
                GUILayout.Label(Fit("들려줄 이야기가 없다고 한다.", _textWidth));
                return;
            }
            var next = _client.StoryStepOf(npc);
            foreach (var step in story.Where(s => next == null || s.Step < next.Step))
            {
                GUILayout.Label(Fit($"{step.Step}. {step.Title} (마침)", _textWidth));
            }
            if (next == null)
            {
                GUILayout.Label(Fit("이야기를 모두 들었다.", _textWidth));
                return;
            }
            GUILayout.Space(4);
            GUILayout.Label(Fit($"{next.Step}/{story.Count}. {next.Title}", _textWidth));
            if (hearts < next.Hearts)
            {
                GUILayout.Label(Fit($"호감 {next.Hearts}이 되면 이야기를 꺼낸다(지금 {hearts}).", _textWidth));
                return;
            }
            GUILayout.Label(Fit($"\"{next.Text}\"", _textWidth));
            var needs = next.Need.Select(n => $"{RequestDefinition.NameOf(n.Thing)} {Math.Min(Have(n.Thing), n.Count)}/{n.Count}");
            GUILayout.Label(Fit($"필요한 것: {string.Join(" · ", needs)}", _textWidth));
            GUILayout.Label(Fit($"보답: {next.RewardText} · 호감 +{next.Points}", _textWidth));
            bool ready = next.Need.All(n => Have(n.Thing) >= n.Count);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && ready;
            if (Ui.LayoutButton(ready ? "건네기" : "아직 모자라다")) Send(_client.StoryCommand(), Sound.Craft);
            GUI.enabled = enabled;

            int Have(object thing) => thing is MaterialDefinition m ? _client.Bag.CountOfKind(m) : _client.Bag.CountOf((ConsumableDefinition)thing);
        }

        // What the person asks today, what they give for it, and [건네기] once the bag has it all.
        void DrawRequest(NpcDefinition npc)
        {
            var request = _client.RequestOf(npc);
            if (request == null)
            {
                GUILayout.Label(Fit("오늘은 부탁할 것이 없다고 한다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit($"\"{request.Text}\"", _textWidth));
            GUILayout.Label(Fit($"필요한 것: {request.NeedText}", _textWidth));
            GUILayout.Label(Fit($"보답: {request.RewardText} · 호감 +{request.Points}", _textWidth));
            if (_client.RequestDoneToday(npc))
            {
                GUILayout.Label(Fit("오늘 부탁은 들어주었다. 내일 또 다른 부탁이 있다.", _textWidth));
                return;
            }
            bool ready = request.Need.All(n => n.Thing is MaterialDefinition m ? _client.Bag.CountOfKind(m) >= n.Count : _client.Bag.CountOf((ConsumableDefinition)n.Thing) >= n.Count);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && ready;
            if (Ui.LayoutButton(ready ? "건네기" : "아직 모자라다")) Send(_client.RequestCommand(), Sound.Craft);
            GUI.enabled = enabled;
        }

        void DrawBuying(NpcDefinition npc, int hearts, int coins)
        {
            var enabled = GUI.enabled;
            foreach (var entry in _client.ShopTodayOf(npc))
            {
                GUILayout.BeginHorizontal();
                Icon(entry.Material != null ? Sprites.Icon(entry.Material) : Sprites.Icon(entry.Consumable));
                int price = World.BuyPrice(entry, hearts, Calendar.Day(_client.Clock));
                string note = entry.Daily ? " (오늘만)" : "";
                bool open = hearts >= entry.Hearts;
                string text = open ? $"{entry.Name}{note} · 금화 {price}" : $"{entry.Name} · 호감 {entry.Hearts}에 열린다";
                TipLabel(new GUIContent(Fit(text, _recipeWidth), entry.Material?.Description ?? entry.Consumable.Description), GUILayout.Width(_recipeWidth));
                GUI.enabled = enabled && open && coins >= price;
                if (Ui.LayoutButton("사기", GUILayout.Width(56))) Send(_client.BuyCommand(entry, 1), Sound.Craft);
                GUI.enabled = enabled && open && coins >= price * 5;
                if (Ui.LayoutButton("×5", GUILayout.Width(40))) Send(_client.BuyCommand(entry, 5), Sound.Craft);
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
        }

        void DrawSelling(NpcDefinition npc, int hearts)
        {
            bool any = false;
            foreach (var entry in _client.Npcs.Shop.Where(e => e.Sell > 0 && e.Npc == npc))
            {
                // Each person takes only so many of a thing a day (World.DailySellLimit).
                int left = _client.SellLeft(entry);
                // A crop in each quality the bag holds, the better ones dearer.
                var kinds = entry.Material != null ? entry.Material.AllQualities.Cast<object>() : new object[] { entry.Consumable };
                foreach (var thing in kinds)
                {
                    var material = thing as MaterialDefinition;
                    int have = material != null ? _client.Bag.CountOf(material) : _client.Bag.CountOf(entry.Consumable);
                    if (have == 0) continue;
                    any = true;
                    GUILayout.BeginHorizontal();
                    Icon(material != null ? Sprites.Icon(material) : Sprites.Icon(entry.Consumable));
                    string name = material?.Name ?? entry.Name;
                    string room = left > 0 ? $" · 오늘 {left}개 더" : " · 오늘은 그만 산다";
                    GUILayout.Label(Fit($"{name} ×{have} · 하나에 금화 {World.SellPrice(entry, hearts, material?.Quality ?? 0, Calendar.Day(_client.Clock))}{room}", _recipeWidth), GUILayout.Width(_recipeWidth));
                    GUI.enabled = left > 0;
                    if (Ui.LayoutButton("팔기", GUILayout.Width(56))) Send(_client.SellCommand(thing, 1), Sound.Pickup);
                    if (Ui.LayoutButton("모두", GUILayout.Width(48))) Send(_client.SellCommand(thing, Math.Min(Math.Min(have, left), World.MaxTrade)), Sound.Pickup);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            foreach (var item in npc.BuysGear ? _client.Bag.Items.ToList() : new List<Item>())
            {
                any = true;
                GUILayout.BeginHorizontal();
                ItemLabel(item);
                if (Ui.LayoutButton($"팔기 {World.ItemValue(item, hearts)}", GUILayout.Width(88))) Send(_client.SellItemCommand(item), Sound.Pickup);
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label(Fit(npc.BuysGear ? "팔 것이 없다. 입은 장비는 벗어야 판다." : "이 사람이 사는 것이 가방에 없다.", _textWidth));
        }

        void DrawGifts(NpcDefinition npc)
        {
            if (_client.GiftedToday(npc))
            {
                GUILayout.Label(Fit("오늘은 선물을 주었다. 내일 또 줄 수 있다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit("하루에 하나. 좋아하는 것을 주면 호감이 크게 오른다.", _textWidth));
            foreach (var pair in _client.Bag.Consumables.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(Fit($"{pair.Key.Name} ×{pair.Value}", _recipeWidth), GUILayout.Width(_recipeWidth));
                if (Ui.LayoutButton("선물", GUILayout.Width(56))) Send(_client.GiftCommand(pair.Key), Sound.Pop);
                GUILayout.EndHorizontal();
            }
            foreach (var pair in _client.Bag.Materials.Where(p => p.Key.Id != World.Coin).ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(Fit($"{pair.Key.Name} ×{pair.Value}", _recipeWidth), GUILayout.Width(_recipeWidth));
                if (Ui.LayoutButton("선물", GUILayout.Width(56))) Send(_client.GiftCommand(pair.Key), Sound.Pop);
                GUILayout.EndHorizontal();
            }
        }

        void DrawChest()
        {
            var chest = _client.Chest.Contents;
            GUILayout.Label($"상자 ({chest.Items.Count}/{Inventory.Capacity})");
            DrawContents(chest, "꺼내기", item => _client.TakeItemCommand(item.Id), (kind, definition) => _client.TakeStackCommand(kind, definition));
            GUILayout.Space(8);
            GUILayout.Label($"가방 ({_client.Bag.Items.Count}/{Inventory.Capacity})");
            DrawContents(_client.Bag, "넣기", item => _client.StoreItemCommand(item.Id), (kind, definition) => _client.StoreStackCommand(kind, definition));
        }

        void DrawContents(Inventory contents, string action, Func<Item, byte[]> moveItem, Func<StackKind, object, byte[]> moveStack)
        {
            if (contents.Items.Count == 0 && contents.Runes.Count == 0 && contents.Consumables.Count == 0 && contents.Materials.Count == 0)
            {
                GUILayout.Label("비어 있다.");
                return;
            }
            foreach (var item in contents.Items.ToList())
            {
                GUILayout.BeginHorizontal();
                ItemLabel(item);
                if (Ui.LayoutButton(action, GUILayout.Width(64))) Send(moveItem(item), Sound.Click);
                GUILayout.EndHorizontal();
            }
            void Stack(StackKind kind, object definition, Sprite icon, string name, int count, string description)
            {
                GUILayout.BeginHorizontal();
                Icon(icon);
                TipLabel(new GUIContent(Fit($"{name} ×{count}", _nameWidth), description), GUILayout.Width(_nameWidth));
                if (Ui.LayoutButton(action, GUILayout.Width(64))) Send(moveStack(kind, definition), Sound.Click);
                GUILayout.EndHorizontal();
            }
            foreach (var pair in contents.Consumables.ToList()) Stack(StackKind.Consumable, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Materials.ToList()) Stack(StackKind.Material, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Runes.ToList()) Stack(StackKind.Rune, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, "");
        }

        // Every recipe, with what it takes against what the bag and the chests nearby hold, and the station it needs
        // when that is not in reach. [만들기] only works with everything at hand; [×N] makes it up to five times over.
        void DrawCrafting()
        {
            var recipes = _client.Recipes;
            if (recipes.Count == 0) return;
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit("제작", _textWidth - 160), GUILayout.Width(_textWidth - 160));
            if (Ui.LayoutButton(_craftableOnly ? "모두 보기" : "만들 수 있는 것만", GUILayout.Width(156))) _craftableOnly = !_craftableOnly;
            GUILayout.EndHorizontal();
            var enabled = GUI.enabled;
            // Facing an assembler, a recipe it can make gets [조립기에] (World.SetRecipe).
            var assembler = _client.MachineAhead() is MachineView m && m.Kind == TileType.Assembler ? m : (MachineView?)null;
            if (assembler != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"조립기: {assembler.Value.Recipe?.Name ?? "정하지 않음"} (조합법의 [조립기에]로 정한다)", _textWidth - 64));
                if (assembler.Value.Recipe != null && Ui.LayoutButton("멈추기", GUILayout.Width(56))) Send(_client.SetRecipeCommand(null), Sound.Click);
                GUILayout.EndHorizontal();
            }
            // By where they are made: by hand, then at each station, the station's own name over its recipes.
            // No heading yet: started on a group no recipe has.
            bool started = false;
            TileType? group = null;
            bool any = false;
            foreach (var recipe in recipes.Where(r => !_client.MadeByMachine(r)).OrderBy(r => r.Station.HasValue ? 1 + (int)r.Station.Value : 0))
            {
                if (_craftableOnly && _client.CraftableTimes(recipe) == 0) continue;
                any = true;
                if (!started || recipe.Station != group)
                {
                    started = true;
                    group = recipe.Station;
                    string where = recipe.Station == null ? "손으로" : $"{StationName(recipe.Station.Value)}에서" + (_client.MissingStation(recipe) != null ? " (가까이 없다)" : "");
                    var heading = GUI.color;
                    GUI.color = new Color(1f, 0.85f, 0.4f);
                    GUILayout.Label(Fit(where, _textWidth));
                    GUI.color = heading;
                }
                string needs = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {_client.Available(i.Material)}/{i.Count}"));
                var station = _client.MissingStation(recipe);
                if (station != null) needs += $" · {StationName(station.Value)} 필요";
                string name = recipe.Count > 1 ? $"{recipe.Name} ×{recipe.Count}" : recipe.Name;
                GUILayout.BeginHorizontal();
                Icon(recipe.Item != null ? Sprites.Icon(recipe.Item) : recipe.Consumable != null ? Sprites.Icon(recipe.Consumable) : Sprites.Icon(recipe.Material));
                // Facing an assembler the row has [조립기에] too: the text gives it room, or the row ran past the window
                // and the button was out of sight.
                bool toAssembler = assembler != null && World.Assembles(recipe);
                float width = toAssembler ? Mathf.Max(80, _recipeWidth - 76) : _recipeWidth;
                TipLabel(new GUIContent(Fit($"{name}\n{needs}", width), RecipeTooltip(recipe)), GUILayout.Width(width));
                int times = _client.CraftableTimes(recipe);
                GUI.enabled = enabled && times > 0;
                if (Ui.LayoutButton("만들기", GUILayout.Width(64))) Send(_client.CraftCommand(recipe), Sound.Craft);
                GUI.enabled = enabled && times > 1;
                int bulk = Mathf.Clamp(times, 2, BulkCraft);
                if (Ui.LayoutButton($"×{bulk}", GUILayout.Width(40))) Send(_client.CraftCommand(recipe, bulk), Sound.Craft);
                GUI.enabled = enabled;
                if (toAssembler && Ui.LayoutButton("조립기에", GUILayout.Width(72)))
                    Send(_client.SetRecipeCommand(recipe), Sound.Click);
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label(Fit(_craftableOnly ? "지금 가진 재료와 곁의 작업대로 만들 수 있는 것이 없다" : "만들 것이 없다", _textWidth));
        }

        // The crafting list shows only what can be made now (CraftableTimes) when this is on.
        bool _craftableOnly;

        const int BulkCraft = 5;

        // Materials that are used rather than built with: the watering can, the rod, the hoe and fertilizer.
        internal static bool IsTool(MaterialDefinition material) =>
            material.Id == World.WateringCan || material.Id == World.FishingRod || material.Id == World.Hoe || material.Id == World.Fertilizer;

        // What pressing the rod does now: cast, reel in, or land the fish that is biting.
        internal static string FishingLabel(WorldClient client) =>
            client.Fishing == World.FishingBite ? "낚아채기!" : client.Fishing == World.FishingCast ? "걷기" : "낚시";

        static string StationName(TileType station) => station switch
        {
            TileType.Workbench => "작업대",
            TileType.Furnace => "화로",
            TileType.Anvil => "모루",
            _ => station.ToString(),
        };

        static string RecipeTooltip(Recipe recipe)
        {
            if (recipe.Item != null) return string.Join("\n", recipe.Item.Implicits.Select(StatText.Describe));
            return recipe.Consumable?.Description ?? recipe.Material.Description;
        }


        // The classes picked here but not yet taken, as ids; null shows the ones the player has.
        System.Collections.Generic.List<string> _pickedClasses;
        int _pickedForKit = -1;

        // The skills in their slots, each with buttons to move it up or down (anywhere, at once), and the classes,
        // which change beside a workbench (Docs/Classes.md, 게임 중에 바꾸기).
        // The skill slot being filled from the unused skills, or -1.
        int _swapSlot = -1;

        // The skills page (Docs/Classes.md, 스킬 포인트와 단계): the points to spend, the four slots and their order,
        // every skill of the chosen classes with [배우기] or [올리기], and changing classes beside a workbench.
        void DrawClasses()
        {
            if (_pickedForKit != _client.KitVersion)
            {
                _pickedClasses = null;
                _pickedForKit = _client.KitVersion;
            }
            int points = _client.SkillPoints;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit($"스킬 포인트 {points} · Lv {_client.Level} (레벨이 오를 때마다 1)", _textWidth - 150), GUILayout.Width(_textWidth - 150));
            // Every learned skill forgotten for coins, the points back to spend again (World.ResetSkills).
            int cost = _client.SkillResetCost;
            var coin = _client.Loot.FindMaterial(World.Coin);
            GUI.enabled = cost > 0 && coin != null && _client.Bag.CountOf(coin) >= cost;
            if (Ui.LayoutButton(cost > 0 ? $"되돌리기 (금화 {cost})" : "되돌리기", GUILayout.Width(146))) Send(_client.ResetSkillsCommand(), Sound.Click);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.Label(Fit("스킬 칸(어디서나 바꾼다). [바꾸기]를 누르고 아래에서 그 칸에 넣을 배운 스킬을 고른다", _textWidth));
            var order = _client.SkillOrder.ToList();
            var all = ClassRules.AllSkillsFor(_client.Classes);
            var learned = all.Where(s => _client.RankOf(s) > 0).ToList();
            if (_swapSlot >= _client.Skills.Count) _swapSlot = -1;
            float slotName = _textWidth - IconRoom - 40 - 40 - 56 - 16;
            for (int slot = 0; slot < _client.Skills.Count; slot++)
            {
                var skill = _client.Skills[slot];
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(skill));
                string key = Controls.UsingTouch ? $"{slot + 1}" : Controls.SkillKeyName(slot);
                TipLabel(new GUIContent(Fit($"{key}  {skill.Name} {RankText(skill)}", slotName), SkillTooltip(skill)), GUILayout.Width(slotName));
                GUI.enabled = slot > 0;
                if (Ui.LayoutButton("▲", GUILayout.Width(40))) Swap(order, slot, slot - 1);
                GUI.enabled = slot < _client.Skills.Count - 1;
                if (Ui.LayoutButton("▼", GUILayout.Width(40))) Swap(order, slot, slot + 1);
                GUI.enabled = learned.Count > _client.Skills.Count;
                if (Ui.LayoutButton(_swapSlot == slot ? "그만" : "바꾸기", GUILayout.Width(56))) _swapSlot = _swapSlot == slot ? -1 : slot;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            // The learned skills not in a slot; with a slot picked, each can go in it.
            var unused = learned.Where(s => !_client.Skills.Contains(s)).ToList();
            if (unused.Count > 0 && _swapSlot >= 0)
            {
                foreach (var skill in unused)
                {
                    if (!Ui.LayoutButton(Ui.Wrap($"{_swapSlot + 1}번 칸에: {skill.Name}", GUI.skin.button, _textWidth))) continue;
                    order[_swapSlot] = all.IndexOf(skill);
                    _swapSlot = -1;
                    Send(_client.ClassCommand(_client.Classes, order), Sound.Click);
                }
            }

            // Every skill of each class, in the order they open up; a learned one also takes a support crystal
            // ([변형], Docs/Classes.md 스킬 변형).
            float rowName = _textWidth - IconRoom - 64 - 60 - 8;
            foreach (var owner in _client.Classes)
            {
                GUILayout.Space(8);
                GUILayout.Label(Fit($"{owner.Name} 스킬", _textWidth));
                foreach (var skill in owner.Skills)
                {
                    int rank = _client.RankOf(skill);
                    bool open = _client.Level >= skill.Level;
                    GUILayout.BeginHorizontal();
                    var previous = GUI.color;
                    if (rank == 0) GUI.color = new Color(1, 1, 1, open ? 0.8f : 0.4f);
                    Icon(Sprites.Icon(skill));
                    string state = rank > 0 ? $"{RankText(skill)}/{SkillRules.MaxRank}" : open ? "배우지 않음" : $"Lv {skill.Level}부터";
                    var held = _client.GemsIn(skill);
                    if (held.Count > 0) state += $" · 결정 {held.Count}/{SkillSupports.SlotsFor(rank)}";
                    TipLabel(new GUIContent(Fit($"{skill.Name} · {state}", rowName), SkillTooltip(skill)), GUILayout.Width(rowName));
                    GUI.color = previous;
                    bool can = _client.CanRaise(skill);
                    GUI.enabled = can;
                    string label = rank >= SkillRules.MaxRank ? "최고" : rank > 0 ? "올리기" : "배우기";
                    if (Ui.LayoutButton(label, GUILayout.Width(64))) Send(_client.LearnSkillCommand(skill), Sound.Click);
                    GUI.enabled = rank > 0;
                    if (Ui.LayoutButton(_supportFor == skill ? "닫기" : "변형", GUILayout.Width(56))) _supportFor = _supportFor == skill ? null : skill;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    if (_supportFor == skill && rank > 0) DrawSupports(skill);
                    // What it does at its rank now, or at the first for one not learned; and what the next adds.
                    var dim = GUI.color;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f, rank > 0 ? 1 : 0.7f);
                    GUILayout.Label(Ui.Wrap(SkillText.Numbers(skill, Math.Max(1, _client.PowerOf(skill)), _client.Stats, _client.SupportsOf(skill)), GUI.skin.label, _textWidth));
                    GUI.color = dim;
                }
            }

            GUILayout.Space(8);
            bool near = _client.Map != null && _client.Map.Near(_client.Position, TileType.Workbench, World.StationReach);
            GUILayout.Label(Fit(near
                ? $"클래스 바꾸기(최대 {ClassRules.DefaultMaxClasses}개, 고른 순서대로 칸을 채운다. 배운 스킬은 클래스를 바꿔도 남는다)"
                : "클래스는 작업대 곁에서 바꿀 수 있다", _textWidth));
            _pickedClasses ??= _client.Classes.Select(c => c.Id).ToList();
            GUI.enabled = near;
            foreach (var option in _client.AllClasses)
            {
                int picked = _pickedClasses.IndexOf(option.Id);
                bool full = picked < 0 && _pickedClasses.Count >= ClassRules.DefaultMaxClasses;
                GUI.enabled = near && !full;
                if (TipButton(new GUIContent((picked >= 0 ? $"[{picked + 1}] " : "[ ] ") + option.Name, option.Description)))
                {
                    if (picked >= 0) _pickedClasses.RemoveAt(picked);
                    else _pickedClasses.Add(option.Id);
                    Sounds.Play(Sound.Click);
                }
            }
            bool differs = !_pickedClasses.SequenceEqual(_client.Classes.Select(c => c.Id));
            GUI.enabled = near && differs;
            if (Ui.LayoutButton("이대로 바꾸기"))
            {
                var classes = _pickedClasses.Select(id => _client.AllClasses.First(c => c.Id == id)).ToList();
                Send(_client.ClassCommand(classes, null), Sound.Buff);
            }
            GUI.enabled = true;

            void Swap(System.Collections.Generic.List<int> list, int a, int b)
            {
                (list[a], list[b]) = (list[b], list[a]);
                Send(_client.ClassCommand(_client.Classes, list), Sound.Click);
            }
        }

        SkillDefinition _supportFor;

        // The skill's crystal slots (one, two from rank 3, three at rank 5): each crystal in it with [빼기], the
        // slots still shut, and while one is free, the crystals in the bag that would go in, each a button.
        void DrawSupports(SkillDefinition skill)
        {
            float width = _textWidth - 16;
            int rank = _client.RankOf(skill);
            int slots = SkillSupports.SlotsFor(rank);
            var held = _client.GemsIn(skill);
            Ui.Row($"{skill.Name}의 변형");
            for (int slot = 0; slot < SkillSupports.MaxSlots; slot++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                if (slot < held.Count && SkillSupports.KindOf(held[slot]) is SupportKind kind)
                {
                    Icon(Sprites.Icon(held[slot]));
                    GUILayout.Label(Ui.Wrap($"{slot + 1}칸 {held[slot].Name}: {SkillSupports.Describe(kind, skill)}", GUI.skin.label, width - IconRoom - 64),
                        GUILayout.Width(width - IconRoom - 64));
                    if (Ui.LayoutButton("빼기", GUILayout.Width(56))) Send(_client.RemoveSupportCommand(skill, slot), Sound.Click);
                }
                else if (slot < slots) GUILayout.Label(Fit($"{slot + 1}칸 비어 있다", width));
                else
                {
                    var faint = GUI.color;
                    GUI.color = new Color(1, 1, 1, 0.5f);
                    GUILayout.Label(Fit($"{slot + 1}칸은 스킬 {SkillSupports.RankForSlot(slot)}단계부터", width));
                    GUI.color = faint;
                }
                GUILayout.EndHorizontal();
            }
            if (held.Count >= slots) return;
            var kinds = _client.SupportsOf(skill);
            var gems = _client.Bag.Materials.Keys.Where(m => SkillSupports.KindOf(m) is SupportKind k && SkillSupports.CanAdd(skill, rank, kinds, k)).ToList();
            if (gems.Count == 0)
            {
                GUILayout.Label(Fit("끼울 결정이 가방에 없다. 모루에서 강화 정수로 만든다(확산·분열·가속·증폭·화염·냉기·번개·관통·지속·흡혈)", _textWidth));
                return;
            }
            foreach (var gem in gems)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                Icon(Sprites.Icon(gem));
                if (Ui.LayoutButton(Ui.Wrap($"{gem.Name} 끼우기: {SkillSupports.Describe(SkillSupports.KindOf(gem).Value, skill)}", GUI.skin.button, width - IconRoom), GUILayout.Width(width - IconRoom)))
                    Send(_client.SupportCommand(skill, gem), Sound.Craft);
                GUILayout.EndHorizontal();
            }
        }

        // "2단계", or "2+1단계" when the passive tree or gear add a rank to it.
        string RankText(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int bonus = _client.PowerOf(skill) - rank;
            return bonus > 0 ? $"{rank}+{bonus}단계" : $"{rank}단계";
        }

        // A skill's description and numbers at the rank it works at, and what the next learned rank would make them.
        string SkillTooltip(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int power = Math.Max(1, _client.PowerOf(skill));
            string tags = skill.Tags == SkillTag.None ? "" : $" · {StatText.TagNames(skill.Tags)}";
            var supports = _client.SupportsOf(skill);
            string text = $"{skill.Name} ({skill.Class.Name}){tags}\n{skill.Description}\n{SkillText.Numbers(skill, power, _client.Stats, supports)}";
            if (rank > 0 && rank < SkillRules.MaxRank) text += $"\n다음 단계: {SkillText.Numbers(skill, power + 1, _client.Stats, supports)}";
            var gems = _client.GemsIn(skill);
            if (gems.Count > 0) text += $"\n변형: {string.Join(", ", gems.Select(g => g.Name))}";
            return text;
        }

        const float QuickWidth = 50;

        // [퀵] puts the thing in the first empty quick slot, or takes it out of the one it is in ([퀵2]).
        void QuickButton(int? slot, Action toggle)
        {
            if (slot == null) return;
            if (!Ui.LayoutButton(slot < 0 ? "퀵" : $"퀵{slot + 1}", GUILayout.Width(QuickWidth))) return;
            toggle();
            Sounds.Play(Sound.Click);
        }

        void ItemLabel(Item item)
        {
            Icon(Sprites.Icon(item.Base));
            var previous = GUI.color;
            GUI.color = Palette.Of(item.Rarity);
            string sockets = item.Sockets.Count == 0 ? "" : " " + string.Concat(item.Sockets.Select(r => r == null ? "○" : "●"));
            TipLabel(new GUIContent(Fit(ItemText.Name(item) + sockets, _nameWidth), string.Join("\n", ItemText.Describe(item))), GUILayout.Width(_nameWidth));
            GUI.color = previous;
        }

        // A command from a button, with a sound right away so the press is felt before the host answers. The host
        // may still refuse it (no room, no station in reach).
        void Send(byte[] command, Sound sound)
        {
            if (command == null) return;
            Sounds.Play(sound);
            _send(command);
        }

        static void Icon(Sprite sprite)
        {
            var area = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
            if (Event.current.type == EventType.Repaint) Sprites.DrawInGui(area, sprite);
        }

        // The passive tree (Docs/Classes.md, 패시브 트리): the points spent and left, the picked point with [배우기], the
        // tree drawn as buttons joined by dotted lines, and the points that can be taken now as big buttons too, for
        // fingers when the tree is drawn small.
        PassiveNode _pickedPassive;

        void DrawPassives()
        {
            var tree = _client.PassiveTree;
            if (tree.Count == 0)
            {
                GUILayout.Label(Fit("특성 트리 시트가 없다. 명령 한 줄로 프로젝트를 다시 설정한다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit($"특성 포인트 {_client.PassivePoints}개 (레벨마다 1개). 시작점이나 배운 점과 이어진 점을 배운다.", _textWidth));
            GUILayout.BeginHorizontal();
            if (_client.Passives.Count > 0 && Ui.LayoutButton("모두 되돌리기", GUILayout.Width(110))) Send(_client.ResetPassivesCommand(), Sound.Click);
            GUILayout.EndHorizontal();

            if (_pickedPassive != null)
            {
                bool taken = _client.Passives.Contains(_pickedPassive);
                string stats = _pickedPassive.Modifiers.Count == 0 ? "" : " · " + string.Join(", ", _pickedPassive.Modifiers.Select(StatText.Describe));
                GUILayout.Label(Fit($"{_pickedPassive.Name}{(_pickedPassive.Notable ? " (큰 점)" : "")}{stats}", _textWidth));
                GUILayout.BeginHorizontal();
                GUI.enabled = _client.CanAllocate(_pickedPassive);
                if (Ui.LayoutButton(taken ? "배움" : "배우기", GUILayout.Width(80))) Send(_client.AllocatePassiveCommand(_pickedPassive), Sound.LevelUp);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            DrawPassiveTree(tree);

            var open = tree.Where(_client.CanAllocate).ToList();
            if (open.Count == 0) return;
            GUILayout.Label("지금 배울 수 있는 점");
            foreach (var node in open)
            {
                string stats = string.Join(", ", node.Modifiers.Select(StatText.Describe));
                if (Ui.LayoutButton(Ui.Wrap($"{node.Name}: {stats}", GUI.skin.button, _textWidth))) _pickedPassive = node;
            }
        }

        void DrawPassiveTree(System.Collections.Generic.IReadOnlyList<PassiveNode> tree)
        {
            int minX = tree.Min(n => n.X), maxX = tree.Max(n => n.X), minY = tree.Min(n => n.Y), maxY = tree.Max(n => n.Y);
            // Steps that fit the column, between small and roomy: the whole tree always fits its width (a phone held
            // upright gets small points; the open ones are also big buttons under the tree).
            float step = Mathf.Clamp((_textWidth - 30) / Mathf.Max(1, maxX - minX), 16, 48);
            float size = Mathf.Clamp(step - 4, 12, 34);
            var canvas = GUILayoutUtility.GetRect((maxX - minX) * step + size + 8, (maxY - minY) * step + size + 8, GUILayout.ExpandWidth(false));
            Vector2 At(PassiveNode n) => new Vector2(canvas.x + 4 + size / 2 + (n.X - minX) * step, canvas.y + 4 + size / 2 + (n.Y - minY) * step);
            bool Taken(PassiveNode n) => n.IsStart || _client.Passives.Contains(n);

            if (Event.current.type == EventType.Repaint)
            {
                foreach (var node in tree)
                {
                    foreach (var other in node.Links.Where(o => string.CompareOrdinal(o.Id, node.Id) > 0))
                    {
                        var color = Taken(node) && Taken(other) ? Palette.Worn : new Color(0.55f, 0.5f, 0.45f);
                        Dots(At(node), At(other), color);
                    }
                }
            }
            foreach (var node in tree)
            {
                float s = node.Notable || node.IsStart ? size : size * 0.75f;
                var center = At(node);
                var rect = new Rect(center.x - s / 2, center.y - s / 2, s, s);
                var previous = GUI.color;
                GUI.color = node == _pickedPassive ? new Color(1f, 0.6f, 0.8f)
                    : Taken(node) ? Palette.Worn
                    : _client.CanAllocate(node) ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f);
                // The start as a dot: neither game font has "★" (Galmuri11, Noto Sans KR subsets), which drew as a box.
                if (Ui.Button(rect, node.IsStart ? "●" : node.Name.Substring(0, 1))) _pickedPassive = node.IsStart ? null : node;
                GUI.color = previous;
            }
        }

        // A dotted line: small squares every few points.
        static void Dots(Vector2 from, Vector2 to, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            float length = Vector2.Distance(from, to);
            for (float d = 0; d <= length; d += 5) GUI.DrawTexture(new Rect(Vector2.Lerp(from, to, d / length) - Vector2.one, Vector2.one * 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // Beside an anvil: picks the item to work on, or puts it back.
        void RefineButton(Item item)
        {
            if (!_client.NearAnvil) return;
            if (Ui.LayoutButton(_refining == item.Id ? "닫기" : "다듬기", GUILayout.Width(56))) _refining = _refining == item.Id ? 0 : item.Id;
        }

        // At the top of the gear page while an anvil is near: what can be done to the picked item and what it costs.
        void DrawRefining()
        {
            if (!_client.NearAnvil)
            {
                _refining = 0;
                return;
            }
            var loot = _client.Loot;
            var essence = loot?.FindMaterial(World.Essence);
            int have = essence == null ? 0 : _client.Bag.CountOf(essence);
            var worn = _client.Equipment.Items.FirstOrDefault(i => i.Id == _refining);
            var item = worn ?? _client.Bag.Find(_refining);
            if (item == null)
            {
                _refining = 0;
                GUILayout.Label(Fit($"모루: 장비의 [다듬기]를 누르면 강화, 분해, 룬 빼기, 접사 다시 굴리기를 한다. 강화 정수 {have}개", _textWidth));
                GUILayout.Space(6);
                return;
            }
            GUILayout.Label(Fit($"모루: {ItemText.Name(item)} · 강화 정수 {have}개", _textWidth));
            GUILayout.BeginHorizontal();
            if (World.UpgradeCost(item, loot) is var (cost, metal, metalCount))
            {
                bool can = have >= cost && (metal == null || _client.Bag.CountOf(metal) >= metalCount);
                string needs = metal == null ? $"정수 {cost}" : $"정수 {cost}, {metal.Name} {metalCount}";
                GUI.enabled = can;
                if (Ui.LayoutButton($"강화 +{item.Upgrade + 1} ({needs})")) Send(_client.RefineCommand(InventoryCommand.Upgrade, item.Id), Sound.Craft);
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"최대 강화(+{Item.MaxUpgrade})");
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (worn == null && Ui.LayoutButton($"분해 (정수 +{World.SalvageValue(item)})")) Send(_client.RefineCommand(InventoryCommand.Salvage, item.Id), Sound.Break);
            GUI.enabled = have >= World.UnsocketEssence;
            if (item.Sockets.Any(r => r != null) && Ui.LayoutButton($"룬 빼기 (정수 {World.UnsocketEssence})"))
                Send(_client.RefineCommand(InventoryCommand.Unsocket, item.Id), Sound.Click);
            int reroll = World.RerollCost(item);
            GUI.enabled = have >= reroll;
            if (reroll > 0 && Ui.LayoutButton($"접사 다시 (정수 {reroll})")) Send(_client.RefineCommand(InventoryCommand.Reroll, item.Id), Sound.Craft);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // Shown while a rune is picked and the item still has room for it.
        void RuneButton(Item item)
        {
            if (_selectedRune == null || !item.Sockets.Contains(null)) return;
            if (Ui.LayoutButton("끼우기", GUILayout.Width(56))) Send(_client.InsertRuneCommand(item.Id, _selectedRune), Sound.Craft);
        }

        static string CategoryName(SlotCategory category) => category switch
        {
            SlotCategory.Weapon => "무기",
            SlotCategory.Armor => "방어구",
            _ => "장신구",
        };
    }
}
