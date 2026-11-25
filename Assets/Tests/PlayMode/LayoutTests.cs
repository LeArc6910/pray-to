using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    // The screen's fixed parts fit every screen the game means to run on (Docs/Design.md, 기술 원칙 7): on phones held
    // either way, a small PC window and a tablet, with one to four skills, every skill button, quick slot, the
    // minimap, the HUD box, the time and place lines and the touch buttons lie inside the screen and clear of one
    // another. The layout is worked
    // out for a stand-in screen size (Ui.SizeOverride), so this runs anywhere, batch mode too.
    public class LayoutTests
    {
        static readonly (string Name, float Width, float Height, bool Touch)[] Sizes =
        {
            ("휴대폰 세로", 390, 844, true),
            ("작은 휴대폰 세로", 360, 640, true),
            ("휴대폰 가로", 844, 390, true),
            ("작은 휴대폰 가로", 667, 375, true),
            ("태블릿", 1024, 768, true),
            ("작은 PC 창", 800, 600, false),
            ("PC", 1280, 720, false),
        };

        float _hudHeight;
        float _leftBottom;

        [SetUp]
        public void Remember()
        {
            _hudHeight = Ui.HudHeight;
            _leftBottom = Ui.LeftColumnBottom;
        }

        [TearDown]
        public void PutBack()
        {
            Ui.SizeOverride = null;
            Ui.InsetOverride = null;
            Ui.HudHeight = _hudHeight;
            Ui.LeftColumnBottom = _leftBottom;
            Controls.ForceTouch(false);
        }

        static bool Overlap(Rect a, Rect b) => a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        static bool Inside(Rect r, float width, float height) =>
            r.xMin >= -0.01f && r.yMin >= -0.01f && r.xMax <= width + 0.01f && r.yMax <= height + 0.01f;

        [Test]
        public void TheFixedPartsFitEveryScreen()
        {
            var problems = new List<string>();
            foreach (var (name, width, height, touch) in Sizes) problems.AddRange(FixedPartProblems(name, width, height, touch));
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // A phone's notch, camera hole and rounded corners (Screen.safeArea, 2026-11-26): the screens lay themselves out
        // in the safe area, so with its edges kept clear (as a notched phone's are, upright and sideways) the fixed parts
        // still fit inside it and clear of one another, and the screen's size in points is the safe area's.
        [Test]
        public void TheFixedPartsKeepToTheSafeArea()
        {
            var problems = new List<string>();
            var insets = new[]
            {
                ("휴대폰 세로", 390f, 844f, new Ui.Edges(0, 47, 0, 34)),
                ("작은 휴대폰 세로", 360f, 640f, new Ui.Edges(0, 24, 0, 16)),
                ("휴대폰 가로", 844f, 390f, new Ui.Edges(47, 0, 47, 21)),
                ("작은 휴대폰 가로", 667f, 375f, new Ui.Edges(32, 0, 32, 16)),
            };
            foreach (var (name, width, height, inset) in insets)
            {
                Ui.InsetOverride = inset;
                Ui.SizeOverride = new Vector2(width, height);
                Assert.AreEqual(width - inset.Left - inset.Right, Ui.Width, 0.01f, name);
                Assert.AreEqual(height - inset.Top - inset.Bottom, Ui.Height, 0.01f, name);
                var full = Ui.FullScreen;
                Assert.AreEqual(new Rect(-inset.Left, -inset.Top, width, height), full, name + ": the whole screen");
                problems.AddRange(FixedPartProblems($"{name}(안전 영역 {inset})", width, height, true));
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The checks for one screen, at one to four skills: every fixed part inside the screen's safe area (Ui.Width ×
        // Ui.Height) and clear of the others.
        static List<string> FixedPartProblems(string name, float width, float height, bool touch)
        {
            var problems = new List<string>();
            foreach (int skills in new[] { 1, 2, 3, 4 })
            {
                Ui.SizeOverride = new Vector2(width, height);
                Ui.HudHeight = 160;
                Controls.ForceTouch(touch);
                float safeWidth = Ui.Width, safeHeight = Ui.Height;
                var keys = Enumerable.Range(0, skills + 1).Select(slot => ($"스킬 {slot}", SkillBar.Area(slot, skills))).ToList();
                keys.Add(("구르기", SkillBar.RollArea(skills)));
                var quick = Enumerable.Range(0, Hotbar.Slots).Select(slot => ($"퀵슬롯 {slot}", Hotbar.Area(slot, skills))).ToList();
                // The time and place lines: up to four small lines (an online room's code the fourth).
                var top = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea), ("HUD", Ui.HudArea), ("시간·장소 줄", Ui.InfoArea(60)) };
                if (touch) top.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                string where = $"{name} {width}×{height}, 스킬 {skills}";

                foreach (var (part, rect) in keys.Concat(quick).Concat(top))
                {
                    if (!Inside(rect, safeWidth, safeHeight)) problems.Add($"{where}: 화면 밖 - {part} {rect}");
                }
                var bottom = keys.Concat(quick).ToList();
                for (int i = 0; i < bottom.Count; i++)
                {
                    for (int j = i + 1; j < bottom.Count; j++)
                    {
                        if (Overlap(bottom[i].Item2, bottom[j].Item2)) problems.Add($"{where}: 겹침 - {bottom[i].Item1} / {bottom[j].Item1}");
                    }
                }
                // The top parts with one another, and each with everything along the bottom.
                for (int i = 0; i < top.Count; i++)
                {
                    var others = top.Skip(i + 1).Concat(bottom);
                    foreach (var (other, otherRect) in others)
                    {
                        if (Overlap(top[i].Item2, otherRect)) problems.Add($"{where}: 겹침 - {top[i].Item1} / {other}");
                    }
                }
            }
            return problems;
        }

        // Buttons over things in the world never sit on one spot (U0, 2026-11-22: on a phone a machine's [끄기] lay under
        // [○○와 말하기]): however many want the same place, each is moved up past the ones before, stays on the screen
        // across, and is a finger's 40 points tall on a touch screen.
        [Test]
        public void WorldButtonsNeverShareASpot()
        {
            var problems = new List<string>();
            foreach (var (name, width, height, touch) in Sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Controls.ForceTouch(touch);
                float tall = Ui.WorldButtonHeight;
                if (touch && tall < 40) problems.Add($"{name}: 월드 단추 높이 {tall}");
                var taken = new List<Rect>();
                var wanted = new[] { 80f, 220f, 140f, 300f, 88f };
                for (int i = 0; i < wanted.Length; i++)
                {
                    // All round the middle of the screen, a few points apart, as a machine and a person in reach are.
                    var rect = Ui.Stack(new Rect(width / 2 - wanted[i] / 2 + i * 6, height / 2 - tall / 2 - i * 3, wanted[i], tall), taken, width);
                    foreach (var other in taken)
                        if (Overlap(rect, other)) problems.Add($"{name}: 겹침 {rect} / {other}");
                    if (rect.xMin < 0 || rect.xMax > width) problems.Add($"{name}: 화면 밖 {rect}");
                    taken.Add(rect);
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The goal list folds to one line on a phone held upright, and in a boss fight whose bar goes under it (U0,
        // phone-boss-bar: the list took 40% of the top and pushed the bar over the player). The boss bar lies inside the
        // screen, clear of the minimap column, and above the player (the middle of the screen) by a head's height.
        [Test]
        public void TheBossBarKeepsClearOfThePlayer()
        {
            var problems = new List<string>();
            foreach (var (name, width, height, touch) in Sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Controls.ForceTouch(touch);
                if (height > width && width < 600 && !GameRoot.TrackerFolds(false)) problems.Add($"{name}: 세로 휴대폰에서 목표 목록이 접히지 않는다");
                bool folds = GameRoot.TrackerFolds(true);
                if (!GameRoot.BossBarBesideHud && !folds) problems.Add($"{name}: 보스 막대가 아래로 가는데 목표 목록이 접히지 않는다");
                // In a boss fight under the HUD, the box is its bars and a line; the folded list one line under it.
                Ui.HudHeight = GameRoot.BossBarBesideHud ? 200 : GameRoot.HudBarsHeight;
                // The folded line is a finger tall on a touch screen (U7).
                Ui.LeftColumnBottom = Ui.HudArea.yMax + 6 + (folds ? GameRoot.FoldedTrackerArea(28).height : 220);
                var bar = GameRoot.BossBarArea(40);
                string where = $"{name} {width}×{height}";
                if (!Inside(bar, width, height)) problems.Add($"{where}: 화면 밖 {bar}");
                if (bar.width < 160) problems.Add($"{where}: 너무 좁다 {bar}");
                if (bar.yMax > height / 2 - 30) problems.Add($"{where}: 플레이어 머리 위를 덮는다 {bar}");
                var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea), ("HUD", Ui.HudArea) };
                if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                foreach (var (part, rect) in column)
                    if (Overlap(bar, rect)) problems.Add($"{where}: 겹침 - {part}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // Every screen of its own (Docs/UiRework.md U2: the bag, skills, passives, character, journal, map) fits the
        // screens the person named (a small PC window, a small phone held upright and on its side) and the rest: inside
        // the screen with room under its head and its close button on the screen; the full screens cover a phone; the bag
        // keeps clear of the minimap column so [가방]·[메뉴] stay in reach; and the touch hub's buttons are a finger's
        // size, inside the screen, apart, and clear of the column.
        [Test]
        public void EveryScreenFits()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U2)", 800, 600, false),
                ("작은 휴대폰 세로 (U2)", 375, 812, true),
                ("작은 휴대폰 가로 (U2)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Ui.HudHeight = 160;
                Controls.ForceTouch(touch);
                string where = $"{name} {width}×{height}";
                var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea) };
                if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                foreach (var screen in ScreenStack.All)
                {
                    // While the bag is open the HUD box is only its bars (GameRoot.DrawHud compact), and the bag goes under it.
                    Ui.HudHeight = screen == GameScreen.Bag ? GameRoot.HudBarsHeight : 160;
                    var area = ScreenStack.AreaOf(screen);
                    string what = $"{where}, {ScreenStack.NameOf(screen)}";
                    if (!Inside(area, width, height)) problems.Add($"{what}: 화면 밖 {area}");
                    var inside = UiSkin.InsideOf(area);
                    if (inside.width < 260 || inside.height < 150) problems.Add($"{what}: 내용 자리가 좁다 {inside}");
                    var close = UiSkin.CloseRect(UiSkin.HeadOf(area));
                    if (!Inside(close, width, height)) problems.Add($"{what}: 닫기 단추가 화면 밖 {close}");
                    if (ScreenStack.IsFull(screen) && touch && (width < 600 || height < 480) && (area.width < width - 10 || area.height < height - 10))
                        problems.Add($"{what}: 휴대폰에서 전체 화면이 아니다 {area}");
                    if (screen == GameScreen.Bag && touch)
                        foreach (var (part, rect) in column)
                            if (Overlap(area, rect)) problems.Add($"{what}: 겹침 - {part}");
                }
                if (!touch) continue;
                var (panel, cells) = ScreenStack.HubLayout();
                if (!Inside(panel, width, height)) problems.Add($"{where}, 메뉴: 화면 밖 {panel}");
                if (cells.Count != 8) problems.Add($"{where}, 메뉴: 단추 {cells.Count}개");
                for (int i = 0; i < cells.Count; i++)
                {
                    var (label, _, rect) = cells[i];
                    if (rect.height < 40 || rect.width < 80) problems.Add($"{where}, 메뉴 [{label}]: 작다 {rect}");
                    if (!Inside(rect, width, height)) problems.Add($"{where}, 메뉴 [{label}]: 화면 밖 {rect}");
                    for (int j = i + 1; j < cells.Count; j++)
                        if (Overlap(rect, cells[j].Rect)) problems.Add($"{where}, 메뉴: 겹침 - {label} / {cells[j].Name}");
                }
                foreach (var (part, rect) in column)
                    if (Overlap(panel, rect)) problems.Add($"{where}, 메뉴: 겹침 - {part}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The growth screens (Docs/UiRework.md U4: skills, the passive tree, the character) fit the screens the person
        // named (a small PC window, a small phone held upright and on its side) and the rest: every part inside the area
        // under the tabs and clear of the others; the skills' slots and their [바꾸기] inside the bar, a finger tall on a
        // touch screen, apart; the list and the picked skill's panel with room; the tree's sky with room, its zoom buttons
        // in it, and on a phone the points, [모두 되돌리기], the point's card and the drawer over the sky without covering
        // one another or the zoom buttons, the sky's middle still free; the character's two columns (or one) inside.
        [Test]
        public void TheGrowthScreensFit()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U4)", 800, 600, false),
                ("작은 휴대폰 세로 (U4)", 375, 812, true),
                ("작은 휴대폰 가로 (U4)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Ui.HudHeight = 160;
                Controls.ForceTouch(touch);
                float finger = touch ? 40 : 28;
                foreach (var screen in ScreenStack.Members(ScreenGroup.Growth))
                {
                    string where = $"{name} {width}×{height}, {ScreenStack.NameOf(screen)}";
                    var area = ScreenStack.AreaOf(screen);
                    if (!Inside(area, width, height)) problems.Add($"{where}: 화면 밖 {area}");
                    var body = InventoryWindow.GrowthBody(area);
                    if (body.width < 300 || body.height < 200) problems.Add($"{where}: 내용 자리가 좁다 {body}");
                    bool Within(Rect r) => r.xMin >= body.xMin - 0.5f && r.yMin >= body.yMin - 0.5f && r.xMax <= body.xMax + 0.5f && r.yMax <= body.yMax + 0.5f;
                    var parts = new List<(string, Rect)>();
                    if (screen == GameScreen.Skills)
                    {
                        var lay = InventoryWindow.LaySkills(body);
                        parts.Add(("스킬 칸 줄", lay.Slots));
                        parts.Add(("포인트", lay.Points));
                        parts.Add(("스킬 목록", lay.List));
                        if (lay.Side) parts.Add(("고른 스킬", lay.Detail));
                        if (lay.List.height < 120 || lay.List.width < 280) problems.Add($"{where}: 스킬 목록이 좁다 {lay.List}");
                        if (lay.Detail.height < 120 || lay.Detail.width < 260) problems.Add($"{where}: 고른 스킬 판이 좁다 {lay.Detail}");
                        var slots = new List<Rect>();
                        for (int i = 0; i < ClassRules.SkillSlots; i++)
                        {
                            var (cell, swap) = InventoryWindow.SlotRects(lay, i);
                            foreach (var (part, rect) in new[] { ($"칸 {i + 1}", cell), ($"칸 {i + 1} [바꾸기]", swap) })
                            {
                                if (!lay.Slots.Contains(rect.min) || !lay.Slots.Contains(rect.max - Vector2.one * 0.01f)) problems.Add($"{where}: {part}이 칸 줄 밖 {rect}");
                                if (rect.height < finger - 0.5f) problems.Add($"{where}: {part}이 낮다 {rect.height}");
                                foreach (var other in slots)
                                    if (Overlap(rect, other)) problems.Add($"{where}: {part}이 다른 칸과 겹친다");
                                slots.Add(rect);
                            }
                        }
                    }
                    else if (screen == GameScreen.Passives)
                    {
                        // A phone's worst: the drawer open with many points, a long card.
                        var lay = InventoryWindow.LayTree(body, true, 8, touch ? 150 : 0);
                        parts.Add(("하늘", lay.View));
                        if (lay.HasSide) parts.Add(("옆 판", lay.Side));
                        if (lay.View.width < 260 || lay.View.height < 180) problems.Add($"{where}: 하늘이 좁다 {lay.View}");
                        if (!lay.View.Contains(lay.Zoom.min) || !lay.View.Contains(lay.Zoom.max - Vector2.one * 0.01f)) problems.Add($"{where}: 확대 단추가 하늘 밖 {lay.Zoom}");
                        if (lay.Zoom.height < finger - 0.5f) problems.Add($"{where}: 확대 단추가 낮다 {lay.Zoom.height}");
                        if (!lay.HasSide)
                        {
                            var over = new List<(string, Rect)> { ("확대 단추", lay.Zoom), ("포인트", lay.Points), ("모두 되돌리기", lay.Reset), ("고른 점", lay.Card), ("서랍", lay.Drawer) };
                            for (int i = 0; i < over.Count; i++)
                            {
                                var (part, rect) = over[i];
                                if (!lay.View.Contains(rect.min) || !lay.View.Contains(rect.max - Vector2.one * 0.01f)) problems.Add($"{where}: {part}이 하늘 밖 {rect}");
                                for (int j = i + 1; j < over.Count; j++)
                                    if (Overlap(rect, over[j].Item2)) problems.Add($"{where}: {part}과 {over[j].Item1}이 겹친다");
                            }
                            if (lay.Card.y - lay.Reset.yMax < 100) problems.Add($"{where}: 고른 점과 서랍이 하늘을 거의 다 덮는다 ({lay.Card.y - lay.Reset.yMax})");
                        }
                    }
                    else
                    {
                        var (left, right, split) = InventoryWindow.LaySheet(body);
                        parts.Add(("그림 열", left));
                        if (split) parts.Add(("능력치 열", right));
                        if (left.width < 280) problems.Add($"{where}: 그림 열이 좁다 {left}");
                    }
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var (part, rect) = parts[i];
                        if (!Within(rect)) problems.Add($"{where}: {part}이 내용 자리 밖 {rect} (자리 {body})");
                        for (int j = i + 1; j < parts.Count; j++)
                            if (Overlap(rect, parts[j].Item2)) problems.Add($"{where}: {part}과 {parts[j].Item1}이 겹친다");
                    }
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The records (Docs/UiRework.md U5: the journal and the map) fit the screens the person named (a small PC window, a
        // small phone held upright and on its side) and the rest: the window inside the screen; the journal's book under
        // its tabs, its page with room to read, the index's five entries inside the index and the book, apart, a finger tall
        // on a touch screen; the map with room, its zoom buttons in it, and the legend's toggles (in the world and in a
        // dungeon) inside the legend and the area under the tabs, apart, clear of the map, a finger tall on a touch screen.
        [Test]
        public void TheRecordsScreensFit()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U5)", 800, 600, false),
                ("작은 휴대폰 세로 (U5)", 375, 812, true),
                ("작은 휴대폰 가로 (U5)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Ui.HudHeight = 160;
                Controls.ForceTouch(touch);
                float finger = touch ? 40 : 28;
                string where = $"{name} {width}×{height}";
                bool Within(Rect r, Rect area) => r.xMin >= area.xMin - 0.5f && r.yMin >= area.yMin - 0.5f && r.xMax <= area.xMax + 0.5f && r.yMax <= area.yMax + 0.5f;

                // The journal.
                var area = ScreenStack.AreaOf(GameScreen.Journal);
                if (!Inside(area, width, height)) problems.Add($"{where}, 일지: 화면 밖 {area}");
                var body = InventoryWindow.GrowthBody(area);
                var lay = InventoryWindow.LayJournal(body);
                if (!Within(lay.Book, body)) problems.Add($"{where}, 일지: 책이 내용 자리 밖 {lay.Book}");
                if (!Within(lay.Page, lay.Book) || !Within(lay.Index, lay.Book)) problems.Add($"{where}, 일지: 쪽이나 목차가 책 밖 {lay.Page} {lay.Index}");
                if (lay.Page.width < 260 || lay.Page.height < 150) problems.Add($"{where}, 일지: 쪽이 좁다 {lay.Page}");
                if (Overlap(lay.Page, lay.Index)) problems.Add($"{where}, 일지: 쪽과 목차가 겹친다");
                if (touch && width < 600 && height > width && !lay.Strip) problems.Add($"{where}, 일지: 세로 휴대폰에서 목차가 위의 탭 줄이 아니다");
                var entries = new List<Rect>();
                for (int i = 0; i < InventoryWindow.JournalNames.Length; i++)
                {
                    var entry = InventoryWindow.IndexEntry(lay, i);
                    string part = $"{where}, 일지 [{InventoryWindow.JournalNames[i]}]";
                    if (!Within(entry, lay.Index)) problems.Add($"{part}: 목차 밖 {entry} (목차 {lay.Index})");
                    if (entry.height < finger - 0.5f) problems.Add($"{part}: 낮다 {entry.height}");
                    if (entry.width < (lay.Strip ? 56 : 120)) problems.Add($"{part}: 좁다 {entry.width}");
                    foreach (var other in entries)
                        if (Overlap(entry, other)) problems.Add($"{part}: 다른 목차와 겹친다");
                    entries.Add(entry);
                }

                // The map, in the world and in a dungeon.
                var panel = ScreenStack.AreaOf(GameScreen.Map);
                if (!Inside(panel, width, height)) problems.Add($"{where}, 지도: 화면 밖 {panel}");
                var mapBody = MapView.FullBody(panel);
                foreach (bool dungeon in new[] { false, true })
                {
                    string part = $"{where}, 지도{(dungeon ? "(던전)" : "")}";
                    int count = MapView.LegendCount(dungeon);
                    var map = MapView.LayMap(mapBody, count);
                    if (!Within(map.View, mapBody)) problems.Add($"{part}: 지도가 내용 자리 밖 {map.View}");
                    if (map.View.width < 260 || map.View.height < 180) problems.Add($"{part}: 지도가 좁다 {map.View}");
                    if (!Within(map.Zoom, map.View)) problems.Add($"{part}: 확대 단추가 지도 밖 {map.Zoom}");
                    if (map.Zoom.height < finger - 0.5f) problems.Add($"{part}: 확대 단추가 낮다 {map.Zoom.height}");
                    if (!Within(map.Legend, mapBody)) problems.Add($"{part}: 범례가 내용 자리 밖 {map.Legend}");
                    if (Overlap(map.Legend, map.View)) problems.Add($"{part}: 범례와 지도가 겹친다");
                    var chips = new List<Rect>();
                    for (int i = 0; i < count; i++)
                    {
                        var chip = MapView.LegendChip(map, i);
                        if (!Within(chip, map.Legend)) problems.Add($"{part}: 범례 {i}이 범례 밖 {chip} (범례 {map.Legend})");
                        if (chip.height < finger - 0.5f) problems.Add($"{part}: 범례 {i}이 낮다 {chip.height}");
                        if (chip.width < 40) problems.Add($"{part}: 범례 {i}이 좁다 {chip.width}");
                        foreach (var other in chips)
                            if (Overlap(chip, other)) problems.Add($"{part}: 범례 {i}이 다른 것과 겹친다");
                        chips.Add(chip);
                    }
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The encounter windows (Docs/UiRework.md U3: the shop, the chest, the crafting station, the anvil) fit the screens
        // the person named (a small PC window, a small phone held upright and on its side) and the rest: inside the screen
        // with room for their two panes under the head, the close button on the screen; a sheet over the whole screen on a
        // phone (nothing of the HUD under it takes a tap), and elsewhere clear of the minimap column (the minimap and, on a
        // tablet, [가방]·[메뉴] stay in reach).
        [Test]
        public void EveryEncounterWindowFits()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U3)", 800, 600, false),
                ("작은 휴대폰 세로 (U3)", 375, 812, true),
                ("작은 휴대폰 가로 (U3)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Ui.HudHeight = GameRoot.HudBarsHeight;
                Controls.ForceTouch(touch);
                string where = $"{name} {width}×{height}";
                var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea) };
                if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                bool phone = touch && (width < 600 || height < 480);
                foreach (var screen in ScreenStack.Encounters)
                {
                    var area = ScreenStack.AreaOf(screen);
                    string what = $"{where}, {ScreenStack.NameOf(screen)}";
                    if (!ScreenStack.IsEncounter(screen) || ScreenStack.IsFull(screen)) problems.Add($"{what}: 만남 화면이 아니다");
                    if (!Inside(area, width, height)) problems.Add($"{what}: 화면 밖 {area}");
                    var inside = UiSkin.InsideOf(area);
                    if (inside.width < 300 || inside.height < 240) problems.Add($"{what}: 내용 자리가 좁다 {inside}");
                    var close = UiSkin.CloseRect(UiSkin.HeadOf(area));
                    if (!Inside(close, width, height)) problems.Add($"{what}: 닫기 단추가 화면 밖 {close}");
                    if (phone && (area.width < width - 10 || area.height < height - 10)) problems.Add($"{what}: 휴대폰에서 시트가 아니다 {area}");
                    if (!phone)
                        foreach (var (part, rect) in column)
                            if (Overlap(area, rect)) problems.Add($"{what}: 겹침 - {part}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The talk box (Docs/UiRework.md 4-2, U3) at the bottom of every screen, with or without a portrait, with one to
        // three rows of buttons under a line of one to five rows: inside the screen, clear of the minimap column and the
        // touch buttons, its portrait, words and buttons inside it, the buttons a finger tall on a touch screen. The skill
        // keys and quick slots are not drawn while it is up (on a phone held upright it lay over them, the U1 sweep), so it
        // may stand where they were; on a phone held upright it stays in the lower part, a talk of two rows of buttons
        // under 45% of the height.
        [Test]
        public void TheTalkBoxFits()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U3)", 800, 600, false),
                ("작은 휴대폰 세로 (U3)", 375, 812, true),
                ("작은 휴대폰 가로 (U3)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Controls.ForceTouch(touch);
                var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea) };
                if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                foreach (bool portrait in new[] { true, false })
                {
                    foreach (int rows in new[] { 1, 2, 3 })
                    {
                        foreach (float text in new[] { 18f, 54f, 90f })
                        {
                            var lay = StoryScreen.Lay(text, portrait, rows);
                            string where = $"{name} {width}×{height}, 초상 {portrait}, 단추 {rows}줄, 글 {text}";
                            if (!Inside(lay.Panel, width, height)) problems.Add($"{where}: 화면 밖 {lay.Panel}");
                            foreach (var (part, rect) in column)
                                if (Overlap(lay.Panel, rect)) problems.Add($"{where}: 겹침 - {part}");
                            foreach (var (part, rect) in new[] { ("초상", lay.Slot), ("글", lay.Text), ("단추", lay.Actions) })
                            {
                                if (part == "초상" && !portrait) continue;
                                if (!lay.Panel.Contains(rect.min) || !lay.Panel.Contains(rect.max - Vector2.one * 0.01f)) problems.Add($"{where}: {part}이 상자 밖 {rect}");
                            }
                            if (portrait && Overlap(lay.Slot, lay.Text)) problems.Add($"{where}: 초상과 글이 겹친다");
                            if (Overlap(lay.Text, lay.Actions)) problems.Add($"{where}: 글과 단추가 겹친다");
                            if (touch && lay.Actions.height < rows * 40 - 0.5f) problems.Add($"{where}: 단추가 손가락보다 낮다 {lay.Actions.height}");
                            if (touch && height > width && rows <= 2 && text <= 54 && lay.Panel.height > height * 0.45f) problems.Add($"{where}: 세로 휴대폰에서 너무 높다 {lay.Panel.height}");
                        }
                    }
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The waypoint and waystone list (StoryScreen.ListArea, 2026-11-22: centred on a phone held upright, the minimap
        // and the [가방]·[나가기] buttons covered its top rows and took their taps) lies inside every screen and clear of
        // the top right column, however long the list.
        [Test]
        public void TheWaypointListKeepsClearOfTheMinimapColumn()
        {
            var problems = new List<string>();
            foreach (var (name, width, height, touch) in Sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Controls.ForceTouch(touch);
                foreach (float wanted in new[] { 200f, 664f, 2000f })
                {
                    var list = StoryScreen.ListArea(Mathf.Min(420, width - 24), wanted);
                    var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea) };
                    if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                    string where = $"{name} {width}×{height}, 목록 {wanted}";
                    if (!Inside(list, width, height)) problems.Add($"{where}: 화면 밖 {list}");
                    if (list.height < 120) problems.Add($"{where}: 너무 낮다 {list}");
                    foreach (var (part, rect) in column)
                        if (Overlap(list, rect)) problems.Add($"{where}: 겹침 - {part}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // The bag and the HUD (Docs/UiRework.md U6, U7) fit the screens the person named (a small PC window, a small phone
        // held upright and on its side) and the rest: the bag's tabs (a rail on a low screen), its kind chips and [정렬]
        // inside it and clear of one another, a finger tall on a touch screen, the list with room left beside a tall card
        // and the card under it; the folded goal line, the fallen card and its button inside the screen, a finger tall, clear
        // of the minimap column; an encounter window clear of the HUD's bars on a PC; a banner kept off the open hub, the bag
        // and the two columns.
        [Test]
        public void TheBagAndHudFit()
        {
            var problems = new List<string>();
            var sizes = new List<(string Name, float Width, float Height, bool Touch)>
            {
                ("작은 PC 창 (U6)", 800, 600, false),
                ("작은 휴대폰 세로 (U6)", 375, 812, true),
                ("작은 휴대폰 가로 (U6)", 740, 360, true),
            };
            sizes.AddRange(Sizes);
            foreach (var (name, width, height, touch) in sizes)
            {
                Ui.SizeOverride = new Vector2(width, height);
                Controls.ForceTouch(touch);
                Ui.HudHeight = GameRoot.HudBarsHeight;
                Ui.LeftColumnBottom = Ui.HudArea.yMax;
                string where = $"{name} {width}×{height}";
                var column = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea) };
                if (touch) column.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                float finger = touch ? 39.5f : 0;

                // The bag, with a card as tall as it may be.
                var bag = ScreenStack.AreaOf(GameScreen.Bag);
                var inside = UiSkin.InsideOf(bag);
                foreach (float card in new[] { 0f, 160f, 2000f })
                {
                    var parts = InventoryWindow.BagLayout(inside, card, true);
                    string what = $"{where}, 가방 (카드 {card})";
                    var pieces = new List<(string, Rect)>();
                    for (int i = 0; i < parts.Tabs.Length; i++) pieces.Add(($"탭 {i}", parts.Tabs[i]));
                    for (int i = 0; i < parts.Chips.Length; i++) pieces.Add(($"칩 {InventoryWindow.FilterNames[i]}", parts.Chips[i]));
                    pieces.Add(("정렬", parts.Sort));
                    pieces.Add(("목록", parts.List));
                    if (parts.Card.height > 0) pieces.Add(("카드", parts.Card));
                    foreach (var (part, rect) in pieces)
                    {
                        if (rect.xMin < inside.xMin - 0.5f || rect.xMax > inside.xMax + 0.5f || rect.yMin < inside.yMin - 0.5f || rect.yMax > inside.yMax + 0.5f)
                            problems.Add($"{what}: 창 밖 - {part} {rect}");
                    }
                    for (int i = 0; i < pieces.Count; i++)
                        for (int j = i + 1; j < pieces.Count; j++)
                            if (Overlap(pieces[i].Item2, pieces[j].Item2)) problems.Add($"{what}: 겹침 - {pieces[i].Item1} / {pieces[j].Item1}");
                    foreach (var chip in parts.Chips.Append(parts.Sort))
                    {
                        if (chip.height < finger) problems.Add($"{what}: 칩이 손가락보다 낮다 {chip}");
                        if (chip.width < 44) problems.Add($"{what}: 칩이 좁다 {chip}");
                    }
                    if (parts.List.height < 100) problems.Add($"{what}: 목록이 낮다 {parts.List}");
                    if (card > 0 && parts.Card.height < 60) problems.Add($"{what}: 카드가 낮다 {parts.Card}");
                    if (parts.Rail != (height < 480)) problems.Add($"{what}: 낮은 화면에서만 탭 레일 ({parts.Rail})");
                }
                if (touch)
                    foreach (var (part, rect) in column)
                        if (Overlap(bag, rect)) problems.Add($"{where}, 가방: 겹침 - {part}");

                // The HUD's pieces.
                if (!Inside(Ui.HudArea, width, height)) problems.Add($"{where}: HUD 상자가 화면 밖 {Ui.HudArea}");
                var folded = GameRoot.FoldedTrackerArea(20);
                if (!Inside(folded, width, height)) problems.Add($"{where}: 접힌 목표 줄이 화면 밖 {folded}");
                if (folded.height < finger) problems.Add($"{where}: 접힌 목표 줄이 손가락보다 낮다 {folded}");
                var death = GameRoot.DeathCardArea();
                var rise = new Rect(death.center.x - Mathf.Min(220, death.width - 24) / 2, death.yMax - 52, Mathf.Min(220, death.width - 24), 44);
                if (!Inside(death, width, height)) problems.Add($"{where}: 쓰러짐 카드가 화면 밖 {death}");
                if (rise.yMin < death.yMin + 60 || rise.yMax > death.yMax) problems.Add($"{where}: [집에서 일어나기]가 카드 안 아래에 없다 {rise}");
                if (Overlap(death, folded) || Overlap(death, Ui.HudArea)) problems.Add($"{where}: 쓰러짐 카드가 HUD를 덮는다 {death}");
                foreach (var (part, rect) in column)
                {
                    if (Overlap(death, rect)) problems.Add($"{where}: 쓰러짐 카드 겹침 - {part}");
                    if (Overlap(folded, rect)) problems.Add($"{where}: 접힌 목표 줄 겹침 - {part}");
                }

                // An encounter window beside the world (not a phone's sheet) keeps off the HUD's bars.
                if (!ScreenStack.PhoneSheet)
                    foreach (var screen in ScreenStack.Encounters)
                        if (Overlap(ScreenStack.AreaOf(screen), Ui.HudArea)) problems.Add($"{where}, {ScreenStack.NameOf(screen)}: HUD 상자를 덮는다");

                // A banner: inside the screen, off the bag, off the hub, off the two columns.
                var room = GameRoot.BannerRoom(default, null);
                var withBag = GameRoot.BannerRoom(bag, null);
                // With the bag across the middle there may be no room: the banner then waits for it to shut (width 0).
                if (!Inside(room, width, height) || withBag.width > 0 && !Inside(withBag, width, height)) problems.Add($"{where}: 배너 자리가 화면 밖 {room} {withBag}");
                if (withBag.width > 0 && Overlap(withBag, bag)) problems.Add($"{where}: 배너가 가방에 겹친다 {withBag}");
                if (withBag.width > 0 && withBag.width < 200 || room.width < 200) problems.Add($"{where}: 배너 자리가 좁다 {room} {withBag}");
                foreach (var (part, rect) in column)
                    if (Overlap(room, rect)) problems.Add($"{where}: 배너 겹침 - {part}");
                if (touch)
                {
                    var hub = ScreenStack.HubLayout().Panel;
                    var beside = GameRoot.BannerRoom(default, hub);
                    if (Overlap(beside, hub)) problems.Add($"{where}: 배너가 [메뉴] 판에 겹친다 {beside} {hub}");
                    if (!Inside(beside, width, height)) problems.Add($"{where}: 메뉴가 열린 배너가 화면 밖 {beside}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
