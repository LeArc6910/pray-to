using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Client
{
    // The screens of their own (Docs/UiRework.md 4-1 화면 나누기, U2): the bag, the growth screens (skills, passives,
    // character) and the records (journal, map), each its own window with its own head and close button. The bag kept
    // only what is worn and carried, with crafting; the rest used to be its tabs.
    enum GameScreen
    {
        None,
        Bag,
        Skills,
        Passives,
        Character,
        Journal,
        Map,
    }

    // Screens of one kind share a window: its head, its place, and tabs that switch between them.
    enum ScreenGroup
    {
        None,
        Bag,
        Growth,
        Records,
    }

    // The screen manager (Docs/UiRework.md U2): which screen is open, one at a time. Rules:
    //  - Opening a screen closes the one open (a key of another screen switches to it; Tab while the skills are up shows
    //    the bag), the touch hub, and the story's talk and waypoint list (StoryScreen, through Opened).
    //  - A screen's own key again closes it; Esc (Back) closes the hub, else the screen, one step at a time.
    //  - While a full screen is open (all but the bag) nothing under it is drawn and the world takes no input: no walking,
    //    attacking, skills, quick slots or world buttons (GameRoot). The bag is a side panel as before: walking goes on.
    //  - Every change bumps Version, and GameRoot then lets go of IMGUI's keyboard focus (a pet's name field).
    // The game state is not touched: a screen only draws and sends the same commands as before.
    sealed class ScreenStack
    {
        public GameScreen Current { get; private set; }

        // The touch hub ([메뉴] under the minimap): a small grid of the screens' buttons.
        public bool HubOpen { get; private set; }

        public int Version { get; private set; }

        // A screen opened (or another took its place).
        public event Action Opened;

        public static readonly GameScreen[] All = { GameScreen.Bag, GameScreen.Skills, GameScreen.Passives, GameScreen.Character, GameScreen.Journal, GameScreen.Map };

        // Full screens cover everything at every size (4-5 전체 화면); the bag stays a side panel on a PC and a sheet under
        // the minimap column on a phone (4-5 세로 화면), so [가방]·[메뉴] stay in reach.
        public bool Full => IsFull(Current);

        public static bool IsFull(GameScreen screen) => screen != GameScreen.None && screen != GameScreen.Bag;

        public void Show(GameScreen screen)
        {
            if (screen == GameScreen.None)
            {
                Close();
                return;
            }
            bool changed = HubOpen || Current != screen;
            HubOpen = false;
            if (Current != screen)
            {
                Current = screen;
                Opened?.Invoke();
            }
            if (changed) Version++;
        }

        // A screen's key: closes it when it is the one open, else opens it in place of any other.
        public void Toggle(GameScreen screen)
        {
            if (Current == screen) Close();
            else Show(screen);
        }

        public void Close()
        {
            if (Current == GameScreen.None && !HubOpen) return;
            Current = GameScreen.None;
            HubOpen = false;
            Version++;
        }

        // Esc: one step back. False when nothing was open (the game then leaves, as before).
        public bool Back()
        {
            if (HubOpen)
            {
                HubOpen = false;
                Version++;
                return true;
            }
            if (Current == GameScreen.None) return false;
            Close();
            return true;
        }

        public void ToggleHub()
        {
            HubOpen = !HubOpen;
            Version++;
        }

        // ---- What each screen is ----

        public static ScreenGroup GroupOf(GameScreen screen) => screen switch
        {
            GameScreen.Bag => ScreenGroup.Bag,
            GameScreen.Skills or GameScreen.Passives or GameScreen.Character => ScreenGroup.Growth,
            GameScreen.Journal or GameScreen.Map => ScreenGroup.Records,
            _ => ScreenGroup.None,
        };

        static readonly GameScreen[] Growth = { GameScreen.Skills, GameScreen.Passives, GameScreen.Character };
        static readonly GameScreen[] Records = { GameScreen.Journal, GameScreen.Map };
        static readonly GameScreen[] BagAlone = { GameScreen.Bag };

        public static IReadOnlyList<GameScreen> Members(ScreenGroup group) => group switch
        {
            ScreenGroup.Growth => Growth,
            ScreenGroup.Records => Records,
            ScreenGroup.Bag => BagAlone,
            _ => Array.Empty<GameScreen>(),
        };

        // The name on its tab, its hub button and in the playtests' "screen" command (also the old bag tabs' names).
        public static string NameOf(GameScreen screen) => screen switch
        {
            GameScreen.Bag => "가방",
            GameScreen.Skills => "스킬",
            GameScreen.Passives => "특성",
            GameScreen.Character => "캐릭터",
            GameScreen.Journal => "일지",
            GameScreen.Map => "지도",
            _ => "",
        };

        // A screen by its name, Korean or English ("스킬" or "skills"); None for anything else.
        public static GameScreen FromName(string name)
        {
            foreach (var screen in All)
            {
                if (name == NameOf(screen) || string.Equals(name, screen.ToString(), StringComparison.OrdinalIgnoreCase)) return screen;
            }
            return name switch
            {
                "skill" or "growth" => GameScreen.Skills,
                "passive" or "tree" => GameScreen.Passives,
                "records" => GameScreen.Journal,
                _ => GameScreen.None,
            };
        }

        public static KeyAction? KeyOf(GameScreen screen) => screen switch
        {
            GameScreen.Bag => KeyAction.Bag,
            GameScreen.Skills => KeyAction.Skills,
            GameScreen.Passives => KeyAction.Passives,
            GameScreen.Character => KeyAction.Character,
            GameScreen.Journal => KeyAction.Journal,
            GameScreen.Map => KeyAction.Map,
            _ => null,
        };

        public static string[] IconOf(GameScreen screen) => screen switch
        {
            GameScreen.Bag => UiIcons.Bag,
            GameScreen.Skills => UiIcons.Skills,
            GameScreen.Passives => UiIcons.Tree,
            GameScreen.Character => UiIcons.Person,
            GameScreen.Journal => UiIcons.Book,
            GameScreen.Map => UiIcons.Map,
            _ => null,
        };

        // The head band of the window (4-4 화면 종류의 표시): the bag's leather, the growth screens' night, the records'
        // parchment.
        public static UiKind KindOf(GameScreen screen) => GroupOf(screen) switch
        {
            ScreenGroup.Growth => UiKind.Growth,
            ScreenGroup.Records => UiKind.Journal,
            _ => UiKind.Bag,
        };

        // The window's key for its opening tween: one per group, so going from tab to tab within it does not open it anew.
        public static string MotionKey(GameScreen screen) => GroupOf(screen) switch
        {
            ScreenGroup.Growth => "growth",
            ScreenGroup.Records => "records",
            _ => "bag",
        };

        // The key that opens it, written after a name on a PC ("스킬 (K)"); nothing on a touch screen or without a key.
        public static string KeyHint(GameScreen screen)
        {
            if (Controls.UsingTouch || KeyOf(screen) is not KeyAction action || KeyBindings.KeyFor(action) == UnityEngine.InputSystem.Key.None) return "";
            return $" ({KeyBindings.Name(action)})";
        }

        // ---- Where each screen goes ----

        // Wide enough for the old bag pages to breathe, not so wide that a line runs across a big screen.
        const float FullMaxWidth = 760;

        // A full screen: the whole screen on a phone (4 points from its edges), else centred, up to FullMaxWidth wide and
        // the screen's height less a margin.
        public static Rect FullArea()
        {
            bool phone = Controls.UsingTouch && (Ui.Width < 600 || Ui.Short);
            float margin = phone ? 4 : 10;
            float width = Mathf.Min(Ui.Width - 2 * margin, FullMaxWidth);
            return new Rect(Mathf.Round((Ui.Width - width) / 2), margin, width, Ui.Height - 2 * margin);
        }

        public static Rect AreaOf(GameScreen screen) => screen switch
        {
            GameScreen.None => default,
            GameScreen.Bag => InventoryWindow.AreaOf(),
            _ => FullArea(),
        };

        // ---- The group's tabs ----

        public const float TabHeight = 40;
        const float TabGap = 4;

        // The tabs of the screen's group in a row across the rect: pressing one switches to that screen as soon as it is
        // pressed (as the bag's tabs do: IMGUI's own switch on release sometimes lost the first click in a browser). Each
        // is known to the playtests by its name; on a PC the mouse over one shows its key.
        public void Tabs(Rect row, GameScreen current)
        {
            var members = Members(GroupOf(current));
            if (members.Count == 0) return;
            float width = (row.width - TabGap * (members.Count - 1)) / members.Count;
            var e = Event.current;
            for (int i = 0; i < members.Count; i++)
            {
                var screen = members[i];
                var tab = new Rect(row.x + i * (width + TabGap), row.y, width, row.height);
                if (e.type == EventType.MouseDown && tab.Contains(e.mousePosition))
                {
                    if (screen != current)
                    {
                        Show(screen);
                        UiMotion.Confirm();
                    }
                    e.Use();
                }
                string name = NameOf(screen);
                Ui.Probe(name, tab);
                bool hover = tab.Contains(e.mousePosition);
                if (e.type == EventType.Repaint) UiSkin.Tab(tab, new GUIContent(name), IconOf(screen), screen == current, hover);
                if (hover && !Controls.UsingTouch) TabHint(tab, screen);
            }
        }

        // On a PC, the key of the screen a tab opens, on a small card under it.
        public static void TabHint(Rect tab, GameScreen screen)
        {
            string key = KeyHint(screen);
            if (key.Length == 0) return;
            string text = $"{NameOf(screen)} 창 · 키{key}";
            var size = Ui.CardSize(text, 200, out string wrapped);
            float x = Mathf.Clamp(tab.center.x - size.x / 2, 4, Mathf.Max(4, Ui.Width - size.x - 4));
            Ui.LateCard(new Rect(x, tab.yMax + 4, size.x, size.y), wrapped);
        }

        // ---- The touch hub (Docs/UiRework.md 4-1 허브, 4-5 허브) ----

        // What the hub offers, in its order, then [나가기] (which [메뉴] took the place of).
        static readonly GameScreen[] HubScreens = { GameScreen.Bag, GameScreen.Skills, GameScreen.Passives, GameScreen.Character, GameScreen.Journal, GameScreen.Map };
        public const string Leave = "나가기";
        public const string HubButton = "메뉴";
        // The row name the hub's buttons are probed under, apart from [가방] and the minimap's [지도] drawn before it.
        public const string HubRow = "메뉴";

        const float HubCellWidth = 92;
        const float HubCellHeight = 44;
        const float HubGap = 6;
        const float HubPad = 8;

        // The hub's panel and its buttons: under the minimap column, as few columns as fit (two on a phone held upright,
        // more on a low screen), right-aligned with the column; beside the column at the top when it fits nowhere under it.
        public static (Rect Panel, List<(string Name, GameScreen Screen, Rect Rect)> Cells) HubLayout()
        {
            int count = HubScreens.Length + 1;
            var minimap = Ui.MinimapArea;
            float top = Ui.RightColumnBottom + 6;
            Rect panel = default;
            int columns = 0;
            foreach (int tried in new[] { 2, 3, 4, count })
            {
                var size = HubSize(tried, count);
                if (size.x > Ui.Width - 20 || top + size.y > Ui.Height - 10) continue;
                columns = tried;
                panel = new Rect(minimap.xMax - size.x, top, size.x, size.y);
                break;
            }
            if (columns == 0)
            {
                // Beside the column: as many columns as keep it inside the screen's height.
                columns = 2;
                while (columns < count && HubSize(columns, count).y > Ui.Height - 20) columns++;
                var size = HubSize(columns, count);
                panel = new Rect(Mathf.Max(10, minimap.x - 10 - size.x), 10, size.x, size.y);
            }
            var cells = new List<(string, GameScreen, Rect)>();
            for (int i = 0; i < count; i++)
            {
                int column = i % columns, row = i / columns;
                var rect = new Rect(panel.x + HubPad + column * (HubCellWidth + HubGap), panel.y + HubPad + row * (HubCellHeight + HubGap), HubCellWidth, HubCellHeight);
                if (i < HubScreens.Length) cells.Add((NameOf(HubScreens[i]), HubScreens[i], rect));
                else cells.Add((Leave, GameScreen.None, rect));
            }
            return (panel, cells);
        }

        static Vector2 HubSize(int columns, int count)
        {
            int rows = (count + columns - 1) / columns;
            return new Vector2(columns * HubCellWidth + (columns - 1) * HubGap + 2 * HubPad, rows * HubCellHeight + (rows - 1) * HubGap + 2 * HubPad);
        }

        static GUIStyle _hubText;

        // The hub over the game: a panel of big buttons, an icon over each name, a gold dot on the growth screens while
        // there are points to spend. Pressing one opens its screen (and shuts the hub); pressing outside shuts it. True
        // when [나가기] was pressed.
        public bool DrawHub(bool skillPoints, bool passivePoints)
        {
            if (!HubOpen) return false;
            var (panel, cells) = HubLayout();
            Controls.Block(panel);
            UiSkin.Card(panel);
            _hubText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, padding = new RectOffset(0, 0, 0, 3), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_hubText, 12, bold: true);
            _hubText.normal.textColor = new Color(0.97f, 0.93f, 0.85f);
            bool leave = false;
            Ui.Row(HubRow);
            foreach (var (name, screen, rect) in cells)
            {
                bool pressed = Ui.Button(rect, "", name);
                if (Event.current.type == EventType.Repaint)
                {
                    // An icon over each screen's name; [나가기] (no screen) is its word alone, in the middle.
                    var icon = IconOf(screen);
                    if (icon != null)
                    {
                        var iconSize = UiSkin.IconSize(icon);
                        UiSkin.Icon(new Rect(rect.x, rect.y + 4, rect.width, iconSize.y), icon, Current == screen ? UiSkin.Gold : new Color(0.9f, 0.78f, 0.55f));
                    }
                    _hubText.alignment = icon != null ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
                    GUI.Label(rect, name, _hubText);
                    if (screen == GameScreen.Skills && skillPoints || screen == GameScreen.Passives && passivePoints) InventoryWindow.Badge(rect);
                }
                if (!pressed) continue;
                if (screen == GameScreen.None) leave = true;
                else Show(screen);
            }
            Ui.Row("");
            // A press anywhere else shuts the hub; the buttons above took theirs first.
            var screenRect = new Rect(0, 0, Ui.Width, Ui.Height);
            Controls.Block(screenRect);
            if (HubOpen && GUI.Button(screenRect, GUIContent.none, GUIStyle.none))
            {
                HubOpen = false;
                Version++;
            }
            return leave;
        }
    }
}
