using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
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
        // The encounter windows (Docs/UiRework.md 4-1 만남, U3): a merchant's shop, a storage chest, a crafting station
        // (workbench or furnace) and the anvil. Each is a window of its own beside the world, the bag's things in it.
        Shop,
        Chest,
        Station,
        Anvil,
        // The settings in the game ([메뉴] → [설정], U7): the start menu's page, a full screen.
        Settings,
    }

    // Screens of one kind share a window: its head, its place, and tabs that switch between them.
    enum ScreenGroup
    {
        None,
        Bag,
        Growth,
        Records,
        Encounter,
    }

    // The screen manager (Docs/UiRework.md U2): which screen is open, one at a time. Rules:
    //  - Opening a screen closes the one open (a key of another screen switches to it; Tab while the skills are up shows
    //    the bag), the touch hub, and the story's talk and waypoint list (StoryScreen, through Opened).
    //  - A screen's own key again closes it; Esc (Back) closes the hub, else the screen, one step at a time.
    //  - While a full screen is open (all but the bag and the encounter windows) nothing under it is drawn and the world
    //    takes no input: no walking, attacking, skills, quick slots or world buttons (GameRoot). The bag is a side panel as
    //    before: walking goes on. The encounter windows (U3) are side panels on a PC and sheets over everything on a
    //    phone (Covers), and shut once their thing is out of reach (GameRoot, InventoryWindow.InReach).
    //  - Every change bumps Version, and GameRoot then lets go of IMGUI's keyboard focus (a pet's name field).
    // The game state is not touched: a screen only draws and sends the same commands as before.
    sealed class ScreenStack
    {
        public GameScreen Current { get; private set; }

        // The touch hub ([메뉴] under the minimap): a small grid of the screens' buttons.
        public bool HubOpen { get; private set; }

        public int Version { get; private set; }

        // What the goal list follows besides the story and the guide (Docs/UiRework.md U5 추적): "side:<chain>" a side story,
        // "story:<person>" a stall keeper's story, "request:<person>" today's request, all picked in the journal, or "pin"
        // a spot picked on the map ([여기로 길 안내]), at PinAt and called PinName; "" for none. One at a time, and only
        // this device's view: nothing of it goes to the host or into a save.
        public string Tracked { get; set; } = "";
        public Vec2 PinAt { get; set; }
        public string PinName { get; set; } = "";

        // Follows that one (or lets it go when it was already followed); true when it is now followed.
        public bool Track(string key)
        {
            Tracked = Tracked == key ? "" : key;
            return Tracked == key;
        }

        // A screen opened (or another took its place).
        public event Action Opened;

        public static readonly GameScreen[] All = { GameScreen.Bag, GameScreen.Skills, GameScreen.Passives, GameScreen.Character, GameScreen.Journal, GameScreen.Map };

        // The encounter windows (U3), opened by meeting the thing (G or the button over it), not by a key of their own.
        public static readonly GameScreen[] Encounters = { GameScreen.Shop, GameScreen.Chest, GameScreen.Station, GameScreen.Anvil };

        // Full screens cover everything at every size (4-5 전체 화면); the bag stays a side panel on a PC and a sheet under
        // the minimap column on a phone (4-5 세로 화면), so [가방]·[메뉴] stay in reach.
        public bool Full => IsFull(Current);

        public static bool IsFull(GameScreen screen) => screen != GameScreen.None && screen != GameScreen.Bag && !IsEncounter(screen);

        public static bool IsEncounter(GameScreen screen) => GroupOf(screen) == ScreenGroup.Encounter;

        // Whether the open screen covers the whole screen: a full screen anywhere, and an encounter window on a phone,
        // where it is a sheet over everything (Docs/UiRework.md U3: the HUD pieces it would sit on are not drawn).
        public bool Covers => Full || IsEncounter(Current) && PhoneSheet;

        // A phone held either way: the encounter windows are a sheet over the whole screen there.
        public static bool PhoneSheet => Controls.UsingTouch && (Ui.Width < 600 || Ui.Short);

        public void Show(GameScreen screen)
        {
            if (screen == GameScreen.None)
            {
                Close();
                return;
            }
            bool changed = HubOpen || Current != screen;
            HubOpen = false;
            LeaveAsked = false;
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
            LeaveAsked = false;
            Version++;
        }

        // Esc: one step back. False when nothing was open (the game then leaves, as before).
        public bool Back()
        {
            if (LeaveAsked)
            {
                LeaveAsked = false;
                Version++;
                return true;
            }
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
            LeaveAsked = false;
            Version++;
        }

        // [나가기] was pressed in the hub: it asks once more ("저장하고 나가기" or "남기", T3).
        public bool LeaveAsked { get; private set; }
        // Asks it as [나가기] does (the play mode tests' picture of the question).
        public void AskLeave()
        {
            if (!HubOpen) HubOpen = true;
            LeaveAsked = true;
            Version++;
        }

        public const string LeaveConfirm = "저장하고 나가기";
        public const string Stay = "남기";

        // ---- What each screen is ----

        public static ScreenGroup GroupOf(GameScreen screen) => screen switch
        {
            GameScreen.Bag => ScreenGroup.Bag,
            GameScreen.Skills or GameScreen.Passives or GameScreen.Character => ScreenGroup.Growth,
            GameScreen.Journal or GameScreen.Map => ScreenGroup.Records,
            GameScreen.Shop or GameScreen.Chest or GameScreen.Station or GameScreen.Anvil => ScreenGroup.Encounter,
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
            GameScreen.Shop => "상점",
            GameScreen.Chest => "상자",
            GameScreen.Station => "작업대",
            GameScreen.Anvil => "모루",
            GameScreen.Settings => "설정",
            _ => "",
        };

        // A screen by its name, Korean or English ("스킬" or "skills"); None for anything else.
        public static GameScreen FromName(string name)
        {
            foreach (var screen in All.Concat(Encounters))
            {
                if (name == NameOf(screen) || string.Equals(name, screen.ToString(), StringComparison.OrdinalIgnoreCase)) return screen;
            }
            return name switch
            {
                "skill" or "growth" => GameScreen.Skills,
                "passive" or "tree" => GameScreen.Passives,
                "records" => GameScreen.Journal,
                "상인" or "merchant" => GameScreen.Shop,
                "화로" or "workbench" or "furnace" => GameScreen.Station,
                "설정" or "settings" => GameScreen.Settings,
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
            GameScreen.Shop => UiIcons.Shop,
            GameScreen.Chest => UiIcons.Chest,
            GameScreen.Station => UiIcons.Hammer,
            GameScreen.Anvil => UiIcons.Hammer,
            GameScreen.Settings => UiIcons.Gear,
            _ => null,
        };

        // The head band of the window (4-4 화면 종류의 표시): the bag's leather, the growth screens' night, the records'
        // parchment.
        public static UiKind KindOf(GameScreen screen) => screen == GameScreen.Shop ? UiKind.Shop : GroupOf(screen) switch
        {
            ScreenGroup.Growth => UiKind.Growth,
            ScreenGroup.Records => UiKind.Journal,
            // A chest, a station and the anvil: steel with bolts (4-4: 만남은 나무(상점)·쇠(기계·모루)).
            ScreenGroup.Encounter => UiKind.Machine,
            _ => UiKind.Bag,
        };

        // The window's key for its opening tween: one per group, so going from tab to tab within it does not open it anew.
        public static string MotionKey(GameScreen screen) => GroupOf(screen) switch
        {
            ScreenGroup.Growth => "growth",
            ScreenGroup.Records => "records",
            ScreenGroup.Encounter => "meet:" + screen,
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
        // The growth screens (U4) are wider: the skills' cards beside the picked one's panel, the passive tree's sky
        // beside its panel, the character's picture beside the stats (Docs/UiRework.md 4-3: 둘 다 전체 화면).
        const float GrowthMaxWidth = 1180;

        // The records (U5) are wider too: the journal's index beside its page, the map beside its legend.
        const float RecordsMaxWidth = 1060;

        // A full screen: the whole screen on a phone (4 points from its edges), else centred, up to FullMaxWidth wide and
        // the screen's height less a margin.
        public static Rect FullArea() => FullArea(FullMaxWidth);

        // The records' window (the journal and the map).
        public static Rect RecordsArea() => FullArea(RecordsMaxWidth);

        static Rect FullArea(float most)
        {
            bool phone = Controls.UsingTouch && (Ui.Width < 600 || Ui.Short);
            float margin = phone ? 4 : 10;
            float width = Mathf.Min(Ui.Width - 2 * margin, most);
            return new Rect(Mathf.Round((Ui.Width - width) / 2), margin, width, Ui.Height - 2 * margin);
        }

        public static Rect AreaOf(GameScreen screen) => screen switch
        {
            GameScreen.None => default,
            GameScreen.Bag => InventoryWindow.AreaOf(),
            _ when IsEncounter(screen) => EncounterArea(screen),
            _ when GroupOf(screen) == ScreenGroup.Growth => FullArea(GrowthMaxWidth),
            _ when GroupOf(screen) == ScreenGroup.Records => RecordsArea(),
            _ => FullArea(),
        };

        // How wide an encounter window would be, with its two panes (the thing's and the bag's) side by side.
        const float EncounterMaxWidth = 880;
        const float EncounterMaxHeight = 640;

        // An encounter window (U3): on a phone a sheet over the whole screen (4 points from its edges); elsewhere left of
        // the minimap column (so the minimap and, on a tablet, [가방]·[메뉴] stay in reach), centred in the room there, as
        // tall as the screen less a margin up to EncounterMaxHeight.
        public static Rect EncounterArea(GameScreen screen)
        {
            if (PhoneSheet) return new Rect(4, 4, Ui.Width - 8, Ui.Height - 8);
            float left = 10, right = Ui.MinimapArea.x - 10;
            float wanted = screen == GameScreen.Chest || screen == GameScreen.Anvil ? 760 : EncounterMaxWidth;
            // Clear of the HUD's bars at the top left (U7; they were under the window on a PC, U3 남은 문제): beside them
            // when the two panes still have room there, else under them.
            var hud = Ui.HudArea;
            float top = 10, bottom = Ui.Height - 10;
            if (right - (hud.xMax + 10) >= 560) left = hud.xMax + 10;
            else if (!Ui.Short) top = hud.yMax + 10;
            float width = Mathf.Min(wanted, right - left);
            float height = Mathf.Min(EncounterMaxHeight, bottom - top);
            return new Rect(Mathf.Round(left + (right - left - width) / 2), Mathf.Round(Mathf.Max(top, top + (bottom - top - height) / 2)), width, height);
        }

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

        // What the hub offers, in its order, then [나가기] (which [메뉴] took the place of). [설정] since U7.
        static readonly GameScreen[] HubScreens = { GameScreen.Bag, GameScreen.Skills, GameScreen.Passives, GameScreen.Character, GameScreen.Journal, GameScreen.Map, GameScreen.Settings };
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
            // On a PC it is the Esc menu (U7): in the middle of the screen, four to a row.
            if (!Controls.UsingTouch)
            {
                columns = HubSize(4, count).x <= Ui.Width - 20 ? 4 : 2;
                var size = HubSize(columns, count);
                panel = new Rect(Mathf.Round((Ui.Width - size.x) / 2), Mathf.Round(Mathf.Max(10, (Ui.Height - size.y) / 2)), size.x, size.y);
            }
            else foreach (int tried in new[] { 2, 3, 4, count })
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

        static GUIStyle _hubText, _askText, _leaveButton;

        // The hub over the game in the title's look (Docs/TitleRework.md 4-3, 묶음 T3): the see-through panel with its thin
        // gold rim, each screen a word with no box and its icon above it, lit gold when pointed at, a gold dot on the growth
        // screens while there are points to spend. Pressing one opens its screen (and shuts the hub); pressing outside shuts
        // it. [나가기] asks once more on the same panel: [저장하고 나가기] or [남기]. True when the game is to be left.
        public bool DrawHub(bool skillPoints, bool passivePoints)
        {
            if (!HubOpen) return false;
            var (panel, cells) = HubLayout();
            Controls.Block(panel);
            // Darker under the see-through panel than on the title: over the game the names over people and the clock
            // showed through between the words (2026-11-25 sweep photos).
            TitleDraw.Veil(panel, 0.5f);
            TitleDraw.Panel(panel);
            _hubText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, padding = new RectOffset(0, 0, 0, 6), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_hubText, 13);
            bool leave = false;
            Ui.Row(HubRow);
            if (LeaveAsked) leave = DrawLeaveAsk(panel);
            else foreach (var (name, screen, rect) in cells)
            {
                var icon = IconOf(screen);
                _hubText.alignment = icon != null ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
                bool pressed = Ui.Button(rect, name, name, _hubText, Ui.Look.Text, Current == screen);
                if (Event.current.type == EventType.Repaint)
                {
                    // An icon over each screen's name; [나가기] (no screen) is its word alone, in the middle.
                    if (icon != null)
                    {
                        var iconSize = UiSkin.IconSize(icon);
                        bool lit = Current == screen || rect.Contains(Event.current.mousePosition) && !Controls.UsingTouch;
                        UiSkin.Icon(new Rect(rect.x, rect.y + 5, rect.width, iconSize.y), icon, lit ? UiSkin.Gold : new Color(0.78f, 0.68f, 0.52f));
                    }
                    if (screen == GameScreen.Skills && skillPoints || screen == GameScreen.Passives && passivePoints) InventoryWindow.Badge(rect);
                }
                if (!pressed) continue;
                if (screen == GameScreen.None)
                {
                    LeaveAsked = true;
                    Version++;
                }
                else Show(screen);
            }
            Ui.Row("");
            // A press anywhere else shuts the hub; the buttons above took theirs first.
            var screenRect = new Rect(0, 0, Ui.Width, Ui.Height);
            Controls.Block(screenRect);
            if (HubOpen && GUI.Button(screenRect, GUIContent.none, GUIStyle.none))
            {
                HubOpen = false;
                LeaveAsked = false;
                Version++;
            }
            return leave;
        }

        // The question in the hub's panel: what leaving does, and the two answers side by side (one above the other when
        // the panel is narrow).
        bool DrawLeaveAsk(Rect panel)
        {
            _askText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Ui.Text(_askText, 13);
            _askText.normal.textColor = new Color(0.94f, 0.9f, 0.84f);
            var inner = new Rect(panel.x + HubPad, panel.y + HubPad, panel.width - 2 * HubPad, panel.height - 2 * HubPad);
            bool stacked = inner.width < 2 * HubCellWidth + HubGap + 40;
            float button = HubCellHeight;
            float answers = stacked ? 2 * button + HubGap : button;
            var question = new Rect(inner.x, inner.y, inner.width, Mathf.Max(24, inner.height - answers - HubGap));
            if (Event.current.type == EventType.Repaint)
                GUI.Label(question, Ui.Wrap(Strings.T("hub.leave_ask", "저장하고 시작 화면으로 나간다"), _askText, question.width), _askText);
            Rect yes, no;
            if (stacked)
            {
                yes = new Rect(inner.x, inner.yMax - answers, inner.width, button);
                no = new Rect(inner.x, yes.yMax + HubGap, inner.width, button);
            }
            else
            {
                float half = (inner.width - HubGap) / 2;
                yes = new Rect(inner.x, inner.yMax - button, half, button);
                no = new Rect(yes.xMax + HubGap, yes.y, half, button);
            }
            _hubText.alignment = TextAnchor.MiddleCenter;
            _leaveButton ??= new GUIStyle(GUI.skin.button);
            Ui.Text(_leaveButton, 13);
            bool leave = Ui.Button(yes, LeaveConfirm, LeaveConfirm, _leaveButton, Ui.Look.Danger, false);
            if (Ui.Button(no, Stay, Stay, _hubText, Ui.Look.Text, !Controls.UsingTouch))
            {
                LeaveAsked = false;
                Version++;
            }
            return leave;
        }
    }
}
