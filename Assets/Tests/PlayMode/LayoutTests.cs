using System.Collections.Generic;
using System.Linq;
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
