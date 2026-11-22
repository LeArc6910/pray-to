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
            foreach (var (name, width, height, touch) in Sizes)
            {
                foreach (int skills in new[] { 1, 2, 3, 4 })
                {
                    Ui.SizeOverride = new Vector2(width, height);
                    Ui.HudHeight = 160;
                    Controls.ForceTouch(touch);
                    var keys = Enumerable.Range(0, skills + 1).Select(slot => ($"스킬 {slot}", SkillBar.Area(slot, skills))).ToList();
                    keys.Add(("구르기", SkillBar.RollArea(skills)));
                    var quick = Enumerable.Range(0, Hotbar.Slots).Select(slot => ($"퀵슬롯 {slot}", Hotbar.Area(slot, skills))).ToList();
                    // The time and place lines: up to four small lines (an online room's code the fourth).
                    var top = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea), ("HUD", Ui.HudArea), ("시간·장소 줄", Ui.InfoArea(60)) };
                    if (touch) top.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                    string where = $"{name} {width}×{height}, 스킬 {skills}";

                    foreach (var (part, rect) in keys.Concat(quick).Concat(top))
                    {
                        if (!Inside(rect, width, height)) problems.Add($"{where}: 화면 밖 - {part} {rect}");
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
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
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
                Ui.LeftColumnBottom = Ui.HudArea.yMax + 6 + (folds ? 34 : 220);
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
                if (cells.Count != 7) problems.Add($"{where}, 메뉴: 단추 {cells.Count}개");
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
    }
}
