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

        [SetUp]
        public void Remember() => _hudHeight = Ui.HudHeight;

        [TearDown]
        public void PutBack()
        {
            Ui.SizeOverride = null;
            Ui.HudHeight = _hudHeight;
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
                    var quick = Enumerable.Range(0, Hotbar.Slots).Select(slot => ($"퀵슬롯 {slot}", Hotbar.Area(slot, skills))).ToList();
                    // The time and place lines: up to four small lines (an online room's code the fourth).
                    var top = new List<(string, Rect)> { ("미니맵", Ui.MinimapArea), ("HUD", Ui.HudArea), ("시간·장소 줄", Ui.InfoArea(60)) };
                    if (touch) top.AddRange(Enumerable.Range(0, 2).Select(i => ($"터치 단추 {i}", Ui.TouchButtonArea(i, 2))));
                    string where = $"{name} {width}×{height}, 스킬 {skills}";

                    foreach (var (part, rect) in keys.Concat(quick).Concat(top))
                    {
                        if (!Inside(rect, width, height)) problems.Add($"{where}: {part}이(가) 화면 밖 {rect}");
                    }
                    var bottom = keys.Concat(quick).ToList();
                    for (int i = 0; i < bottom.Count; i++)
                    {
                        for (int j = i + 1; j < bottom.Count; j++)
                        {
                            if (Overlap(bottom[i].Item2, bottom[j].Item2)) problems.Add($"{where}: {bottom[i].Item1}와(과) {bottom[j].Item1}이(가) 겹침");
                        }
                    }
                    // The top parts with one another, and each with everything along the bottom.
                    for (int i = 0; i < top.Count; i++)
                    {
                        var others = top.Skip(i + 1).Concat(bottom);
                        foreach (var (other, otherRect) in others)
                        {
                            if (Overlap(top[i].Item2, otherRect)) problems.Add($"{where}: {top[i].Item1}와(과) {other}이(가) 겹침");
                        }
                    }
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
