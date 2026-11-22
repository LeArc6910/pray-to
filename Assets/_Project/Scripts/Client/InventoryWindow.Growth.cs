using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The growth screens (Docs/UiRework.md 4-3, U4): skills (K), the passive tree (P) and the character (U), each over the
    // whole screen under the night band with the group's tabs, no longer the bag's old tabs moved into a wide window:
    //  - 스킬 (InventoryWindow.Skills.cs): the four slots as a bar along the top, the points and [되돌리기], every chosen
    //    class's six skills as cards (a list on a phone), the picked one's panel beside them (a sheet over the list on a
    //    phone) with what it does now and at the next rank, [배우기]/[올리기], [칸에 넣기] and its crystals; the classes to
    //    pick while a class slot is open.
    //  - 특성 (InventoryWindow.Passives.cs): the tree as stars on the night sky, dragged and zoomed, the picked point's card
    //    with [배우기], the points to spend, [모두 되돌리기], and the points that can be taken now in a drawer.
    //  - 캐릭터 (InventoryWindow.Character.cs): the hero as dressed now among the worn slots, the level, the stats in
    //    groups, the life skills with their perks, the pet.
    // Every button keeps the words the bag's tabs had, so the playtests press them as before.
    sealed partial class InventoryWindow
    {
        // The frame a growth screen was last drawn in, so one shown again after a while starts afresh (the tree centred).
        int _growthFrame = -10;

        void DrawGrowth(GameScreen screen)
        {
            var area = ScreenStack.AreaOf(screen);
            bool fresh = Time.frameCount - _growthFrame > 2;
            _growthFrame = Time.frameCount;
            if (fresh)
            {
                _treeFresh = true;
                _skillSheet = false;
                _placing = null;
                _swapSlot = -1;
            }
            if (screen != GameScreen.Passives) _treeFresh = true;
            Controls.Block(area);
            UiMotion.BeginWindow(ScreenStack.MotionKey(screen), area, UiKind.Growth);
            var inside = UiSkin.Window(area, UiKind.Growth, ScreenStack.NameOf(screen) + ScreenStack.KeyHint(screen), ScreenStack.IconOf(screen), out var head);
            float right = UiSkin.CloseRect(head).x - 6;
            DrawCoins(head, right, UiKind.Growth);
            if (UiSkin.CloseButton(head))
            {
                _screens.Close();
                UiMotion.EndWindow();
                return;
            }
            var tabs = new Rect(inside.x, inside.y, inside.width, ScreenStack.TabHeight);
            var body = GrowthBody(area);
            // The tree's night under everything below the head (4-3: the one screen unlike the rest).
            if (screen == GameScreen.Passives)
                UiSkin.Stars(Rect.MinMaxRect(area.x + UiSkin.FrameEdge, head.yMax, area.xMax - UiSkin.FrameEdge, area.yMax - UiSkin.FrameEdge));
            GrowthTabs(tabs, screen);
            _rows = body;
            switch (screen)
            {
                case GameScreen.Skills:
                    DrawSkillsScreen(body);
                    break;
                case GameScreen.Passives:
                    DrawTreeScreen(body);
                    break;
                default:
                    DrawCharacterSheet(body);
                    break;
            }
            UiMotion.EndWindow();
            DrawTooltip(area, false);
        }

        // The area a growth screen lays its parts in: under the window's head and the group's tabs.
        internal static Rect GrowthBody(Rect area)
        {
            var inside = UiSkin.InsideOf(area);
            return Rect.MinMaxRect(inside.x, inside.y + ScreenStack.TabHeight + (Ui.Short ? 4 : 8), inside.xMax, inside.yMax);
        }

        // The group's tabs ([스킬][특성][캐릭터]), a gold dot on the skills or passives tab while there are points to spend.
        void GrowthTabs(Rect row, GameScreen current)
        {
            _screens.Tabs(row, current);
            if (Event.current.type != EventType.Repaint) return;
            var members = ScreenStack.Members(ScreenGroup.Growth);
            float width = (row.width - 4 * (members.Count - 1)) / members.Count;
            for (int i = 0; i < members.Count; i++)
            {
                bool points = members[i] == GameScreen.Skills && _client.SkillPoints > 0 || members[i] == GameScreen.Passives && _client.PassivePoints > 0;
                if (points) Badge(new Rect(row.x + i * (width + 4), row.y, width, row.height));
            }
        }

        // ---- For the playtests ----

        internal readonly struct GrowthState
        {
            public GrowthState(float zoom, Vector2 pan, Rect sky, string passive, string skill, bool sheet, bool drawer, string placing)
            {
                Zoom = zoom;
                Pan = pan;
                Sky = sky;
                Passive = passive;
                Skill = skill;
                Sheet = sheet;
                Drawer = drawer;
                Placing = placing;
            }

            public float Zoom { get; }
            public Vector2 Pan { get; }
            // Where the tree's sky was last drawn, in points on the screen.
            public Rect Sky { get; }
            public string Passive { get; }
            public string Skill { get; }
            public bool Sheet { get; }
            public bool Drawer { get; }
            public string Placing { get; }
        }

        internal GrowthState Growth => new GrowthState(_treeZoom, _treePan, _treeSky, _pickedPassive?.Name ?? "", _skillPicked?.Id ?? "", _skillSheet, _drawerOpen, _placing?.Id ?? "");

        // ---- Writing and laying out on the growth screens ----

        static readonly Dictionary<(int, TextAnchor, bool), GUIStyle> GrowthStyles = new Dictionary<(int, TextAnchor, bool), GUIStyle>();

        // A plain label style of that size (12 or 24, the pixel font's), colour and alignment, wrapped or clipped.
        static GUIStyle Style(int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (!GrowthStyles.TryGetValue((size, align, wrap), out var style))
            {
                GrowthStyles[(size, align, wrap)] = style = new GUIStyle(GUI.skin.label)
                {
                    alignment = align,
                    wordWrap = wrap,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
            }
            style.font = GUI.skin.label.font;
            Ui.Text(style, size, bold: size > 12);
            style.normal.textColor = color;
            return style;
        }

        static readonly Color Ink = new Color(0.97f, 0.93f, 0.85f);
        static readonly Color Good = new Color(0.55f, 0.92f, 0.5f);
        static readonly Color Bad = new Color(0.95f, 0.45f, 0.4f);
        static readonly Color NightInk = new Color(0.72f, 0.78f, 1f);

        // Text in the rect (broken between words when wrapped), read by the playtests as a person reads it; a shadow
        // under it over the night sky.
        static void Write(Rect rect, string text, Color color, int size = 12, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool shadow = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = Style(size, color, align, wrap);
            string shown = wrap ? Ui.Wrap(text, style, rect.width) : Ui.Wrap(text, style, 99999);
            if (Event.current.type != EventType.Repaint) return;
            if (shadow)
            {
                var previous = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.8f * previous.a);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), shown, style);
                GUI.color = previous;
            }
            GUI.Label(rect, shown, style);
        }

        // How tall the text is wrapped to the width.
        static float TextHeight(string text, float width, int size = 12)
        {
            var style = Style(size, Color.white, TextAnchor.UpperLeft, true);
            return style.CalcHeight(new GUIContent(Ui.Wrap(text, style, width)), width);
        }

        // A strip of the scrolling list as tall as asked, across its width.
        Rect Room(float height) => GUILayoutUtility.GetRect(_textWidth, height, GUILayout.Width(_textWidth), GUILayout.Height(height));

        // A button in the list whose words wrap to its width, known to the playtests by its whole unbroken label (a long
        // "확산의 결정 끼우기: …" stays one name however it breaks).
        bool WrapButton(string text, float width, Ui.Look look = Ui.Look.Normal)
        {
            var style = GUI.skin.button;
            string wrapped = Ui.Wrap(text, style, width - style.padding.horizontal);
            float height = Mathf.Max(GrowthButton, style.CalcHeight(new GUIContent(wrapped), width));
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            return Ui.Button(rect, wrapped, text, look);
        }

        // A finger's 40 points on a touch screen, else the mouse's 28.
        static float GrowthButton => Controls.UsingTouch ? 40 : 28;

        // The scrolling pane a finger started dragging in (0 the first, 1 the second), so two panes scroll on their own.
        int _growthDragPane;

        void TouchPanes(Rect first, ref Vector2 firstScroll, Rect second, ref Vector2 secondScroll)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown) _growthDragPane = second.Contains(e.mousePosition) ? 1 : 0;
            if (_growthDragPane == 1) Ui.TouchScroll(second, ref secondScroll);
            else Ui.TouchScroll(first, ref firstScroll);
        }

        // A thin frame round the rect in the colour (the picked card's gold).
        static void Outline(Rect rect, Color color, float width = 2)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
            Frame(rect, width);
            GUI.color = previous;
        }

        static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // A picture of the art (a skill's icon, a hero) at a whole number of points a pixel, centred in the rect.
        static void Picture(Rect rect, Sprite sprite, int scale)
        {
            if (sprite == null || Event.current.type != EventType.Repaint) return;
            var size = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
            Sprites.DrawInGui(UiSkin.Snap(new Rect(Mathf.Round(rect.center.x - size.x / 2), Mathf.Round(rect.center.y - size.y / 2), size.x, size.y)), sprite);
        }

        static Color ToColor(Rgba c) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

        // Rank pips (●●○○○ drawn as small squares): learned ranks gold, ranks the tree or gear add cyan, the rest dark.
        static void Pips(Vector2 at, int rank, int bonus, int most, float size = 6)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < most; i++)
            {
                var pip = new Rect(at.x + i * (size + 2), at.y, size, size);
                Fill(new Rect(pip.x - 1, pip.y - 1, pip.width + 2, pip.height + 2), new Color(0.05f, 0.04f, 0.03f, 0.9f));
                Fill(pip, i < rank ? new Color(1f, 0.82f, 0.35f) : i < rank + bonus ? new Color(0.45f, 0.85f, 1f) : new Color(0.25f, 0.22f, 0.2f));
            }
        }
    }
}
