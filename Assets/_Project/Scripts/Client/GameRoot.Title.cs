using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The title screen and its pages (Docs/TitleRework.md, 묶음 T3, the owner's request of 2026-11-25: a flowing background,
    // buttons with no box as in Terraria and Core Keeper, characters and worlds picked from lists, finer menus). Under the
    // logo a column of words: [게임 시작] opens the character list, then the world list, then the game ([혼자 하기]);
    // [함께 하기] picks a character, then an online room or a code (or the LAN on a PC). Each page lies on a see-through
    // panel over the drifting cave (TitleBackdrop), its name at the top with [뒤로] (Esc goes back one step too). On a PC
    // the arrow keys and Enter move through the words as the mouse does.
    public sealed partial class GameRoot
    {
        enum MenuPage
        {
            Main,
            Characters,
            MakeCharacter,
            Worlds,
            MakeWorld,
            Together,
            Saves,
            Settings,
            Keys,
        }

        // Which way the character list was opened: [게임 시작] (alone) or [함께 하기].
        enum MenuFlow
        {
            Solo,
            Together,
        }

        // What the world list's main button starts.
        enum HostKind
        {
            Solo,
            Online,
            Lan,
        }

        // A question over a page, with the two answers named for what they do (Docs/TitleRework.md 2, [32]).
        enum MenuAsk
        {
            None,
            DeleteCharacter,
            DeleteWorld,
            Discard,
        }

        MenuPage _page;
        MenuFlow _flow;
        HostKind _hostKind;
        SaveMenu _saveMenu;
        Vector2 _menuScroll;
        // Width of a line on a page this frame.
        float _menuColumn;
        // What a press asked for, done once the pass has drawn everything (so the page's layout stays the same through a
        // pass).
        Action _menuAfter;

        // The lists, read from the saves when a page of them opens, and every biome any character here has been to (the
        // backdrop shows only those); -1 until read.
        List<SaveList.Entry<CharacterSummary>> _characterList = new List<SaveList.Entry<CharacterSummary>>();
        List<SaveList.Entry<WorldSummary>> _worldList = new List<SaveList.Entry<WorldSummary>>();
        int _visitedHere = -1;
        readonly HashSet<string> _beenWorlds = new HashSet<string>();
        // A character made on the page but not yet played (it is saved once played), and a world likewise with its seed.
        string _newCharacter;
        CharacterLook _newCharacterLook = CharacterLook.Default;
        string _newWorld;
        ulong _newWorldSeed;
        string _newWorldSeedTyped = "";

        // The new world's seed as its row shows it: the number, or the words typed for it.
        internal string NewWorldSeedLabel => _newWorld == null ? "" : NewWorld.SeedLabel(_newWorldSeedTyped, _newWorldSeed);
        // The seed of the world being started when it is one made on the page (StartHosting).
        ulong? _startSeed;
        // The world picked on the list.
        string _pickedWorld = "";
        // The making pages.
        string _makeName = "", _makeNameAtStart = "", _makeError = "";
        CharacterLook _makeLook = CharacterLook.Default, _makeLookAtStart = CharacterLook.Default;
        string _makeWorldName = "", _makeSeed = "", _makeWorldError = "";
        MenuAsk _ask;
        string _askName = "";
        // Which answer the keys are on: 0 the one that acts, 1 the one that keeps.
        int _askChoice = 1;
        // When the logo was last tapped (its glint runs again).
        float _logoTapped = -10;

        // ---- Moving with the keys: the words drawn are counted each pass; the arrows move the one picked, Enter presses
        // it. The mouse picks what it moves over. ----

        int _navIndex, _navCount, _navCounter, _navFire = -1;
        Vector2 _navMouse;
        // The arrows moved the lit word and the page's list has not been scrolled to it yet; the list's height while it
        // is being drawn (0 outside it).
        bool _navMoved;
        float _navView;

        bool NavSlot(Rect rect, out bool focused)
        {
            int index = _navCounter++;
            var e = Event.current;
            bool touch = Controls.UsingTouch;
            if (e.type == EventType.Repaint && !touch && GUI.enabled && rect.Contains(e.mousePosition) && e.mousePosition != _navMouse) _navIndex = index;
            focused = !touch && GUI.enabled && index == _navIndex;
            // A word the arrows came to in a long list (the characters, the worlds, the settings): the list scrolls so it
            // shows (2026-11-26: it stayed below the fold). The rect is in the list's own space there.
            if (_navMoved && index == _navIndex && e.type == EventType.Repaint)
            {
                _navMoved = false;
                if (_navView > 0) KeepInView(rect);
            }
            return GUI.enabled && index == _navFire;
        }

        void KeepInView(Rect rect)
        {
            const float Margin = 4;
            // Near the list's top it goes all the way up, so the first group's heading shows with it.
            if (rect.yMin - Margin < _menuScroll.y) _menuScroll.y = rect.yMin < _navView / 2 ? 0 : rect.yMin - Margin;
            else if (rect.yMax + Margin > _menuScroll.y + _navView) _menuScroll.y = rect.yMax + Margin - _navView;
        }

        // Where the page's list is scrolled to, for the playtests (points from its top).
        internal float MenuScroll => _menuScroll.y;

        void MenuKeys(Event e)
        {
            if (e.type != EventType.KeyDown || KeyBindings.Capturing != null) return;
            if (e.keyCode == KeyCode.Escape)
            {
                if (_ask != MenuAsk.None) _ask = MenuAsk.None;
                else if (_page != MenuPage.Main) Later(Back);
                GUIUtility.keyboardControl = 0;
                e.Use();
                return;
            }
            // Keys typed into a field are the field's.
            if (GUIUtility.keyboardControl != 0) return;
            if (_ask != MenuAsk.None)
            {
                if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
                {
                    _askChoice = 1 - _askChoice;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    _askFire = true;
                    e.Use();
                }
                return;
            }
            int count = Mathf.Max(1, _navCount);
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
            {
                _navIndex = ((e.keyCode == KeyCode.UpArrow ? _navIndex - 1 : _navIndex + 1) % count + count) % count;
                _navMoved = true;
                Sounds.Play(Sound.UiHover, 0.35f);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                _navFire = _navIndex;
                e.Use();
            }
        }

        bool _askFire;

        void Later(Action action) => _menuAfter ??= action;

        void RunMenuAfter()
        {
            var after = _menuAfter;
            _menuAfter = null;
            _navFire = -1;
            _askFire = false;
            after?.Invoke();
        }

        // ---- Styles ----

        GUIStyle _titleItem, _pageItem, _smallItem, _note, _mainButton;

        void MenuStyles()
        {
            _titleItem ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _pageItem ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _smallItem ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _note ??= new GUIStyle(GUI.skin.label) { wordWrap = false };
            // The page's one main action and a question's answer that cannot be taken back keep a frame (Ui.Look.Primary,
            // Ui.Look.Danger): the skin's button style, which has one.
            _mainButton ??= new GUIStyle(GUI.skin.button);
            Ui.Text(_mainButton, 15);
            Ui.Text(_titleItem, 18);
            Ui.Text(_pageItem, 15);
            Ui.Text(_smallItem, 13);
            Ui.Text(_note, 13);
            _note.normal.textColor = TitleDraw.Faint;
        }

        // A text item (Ui.Look.Text) that the keys can reach; another look for the page's main action.
        bool Item(Rect rect, string label, GUIStyle style, Ui.Look look = Ui.Look.Text, string probe = null, bool chosen = false)
        {
            bool fire = NavSlot(rect, out bool focused);
            bool pressed = Ui.Button(rect, label, probe ?? label, style, look, chosen || focused);
            if (fire) UiMotion.Confirm();
            return pressed || fire;
        }

        float RowHeight => Controls.UsingTouch ? 44 : 32;

        // ---- The lists ----

        void RefreshSaves()
        {
            _characterList = SaveList.Characters();
            _worldList = SaveList.Worlds();
            int visited = 0;
            foreach (var entry in _characterList)
                if (entry.Summary != null) visited |= entry.Summary.Visited;
            // A character from before biomes were kept has been in the dirt cave at least.
            _visitedHere = visited == 0 ? BiomeVisits.Bit(Biome.Cave) : visited;
            if (_newCharacter != null && _characterList.Any(c => c.Name == _newCharacter)) _newCharacter = null;
            // The worlds the picked character has a map of (it has been there), looked up once here, not every pass.
            _beenWorlds.Clear();
            foreach (var world in _worldList)
                if (world.Summary != null && SaveStore.Exists(SaveStore.MapKey(world.Summary.Seed, world.Summary.Generator, CharacterName))) _beenWorlds.Add(world.Name);
            if (_newWorld != null && _worldList.Any(w => w.Name == _newWorld)) _newWorld = null;
        }

        bool CharacterTaken(string name) => _characterList.Any(c => c.Name == name) || name == _newCharacter;

        bool WorldTaken(string name) => _worldList.Any(w => w.Name == name) || name == _newWorld;

        // ---- The pages ----

        void Open(MenuPage page)
        {
            if (page != _page) Sounds.Play(Sound.UiOpen, 0.5f);
            _page = page;
            _menuScroll = Vector2.zero;
            // The keys start on the page's first word after [뒤로].
            _navIndex = page == MenuPage.Main ? 0 : 1;
            _ask = MenuAsk.None;
            GUIUtility.keyboardControl = 0;
            if (page == MenuPage.Characters || page == MenuPage.Worlds) RefreshSaves();
        }

        // One step back: a list to the one before it, a making page to its list (asking first when something was changed).
        void Back()
        {
            switch (_page)
            {
                case MenuPage.Keys:
                    Open(MenuPage.Settings);
                    break;
                case MenuPage.MakeCharacter:
                    if (_makeName != _makeNameAtStart || !_makeLook.Equals(_makeLookAtStart))
                    {
                        _ask = MenuAsk.Discard;
                        _askChoice = 1;
                    }
                    else Open(MenuPage.Characters);
                    break;
                case MenuPage.Worlds:
                    Open(_flow == MenuFlow.Together ? MenuPage.Together : MenuPage.Characters);
                    break;
                case MenuPage.MakeWorld:
                    Open(MenuPage.Worlds);
                    break;
                case MenuPage.Together:
                    Open(MenuPage.Characters);
                    break;
                default:
                    Open(MenuPage.Main);
                    break;
            }
            Sounds.Play(Sound.UiClose, 0.5f);
        }

        void DrawMenu()
        {
            bool ready = _loot != null && _loot.Success && _monsters != null && _monsters.Success && _bosses != null && _bosses.Success
                && _classes != null && _classes.Success && _tiles != null && _tiles.Success;
            if (_visitedHere < 0) RefreshSaves();
            var e = Event.current;
            MenuKeys(e);
            MenuStyles();
            _navCounter = 0;
            var screen = Ui.FullScreen;
            TitleBackdrop.Draw(screen, _visitedHere, _page == MenuPage.Main ? 0 : 0.3f);
            if (_page == MenuPage.Main) DrawMainMenu(ready);
            else DrawMenuPage(ready);
            if (e.type == EventType.Repaint)
            {
                _navCount = _navCounter;
                _navMouse = e.mousePosition;
            }
            RunMenuAfter();
        }

        // The settings in the game ([메뉴] → [설정], U7): the title's page over the darkened world; its [뒤로] (or Esc,
        // through the screens) shuts it.
        void DrawGameSettings()
        {
            if (_page != MenuPage.Settings && _page != MenuPage.Keys) _page = MenuPage.Settings;
            string status = _status;
            _status = "";
            MenuStyles();
            _navCounter = 0;
            DrawMenuPage(true);
            if (Event.current.type == EventType.Repaint)
            {
                _navCount = _navCounter;
                _navMouse = Event.current.mousePosition;
            }
            _status = status;
            RunMenuAfter();
            if (_page == MenuPage.Main) _screens.Close();
        }

        // ---- The title ----

        void DrawMainMenu(bool ready)
        {
            var items = new List<(string Label, Action Press)>
            {
                (Strings.T("menu.start", "게임 시작"), () =>
                {
                    _flow = MenuFlow.Solo;
                    _hostKind = HostKind.Solo;
                    Open(MenuPage.Characters);
                }),
                (Strings.T("menu.together", "함께 하기"), () =>
                {
                    _flow = MenuFlow.Together;
                    Open(MenuPage.Characters);
                }),
                (Strings.T("menu.saves", "저장본"), () => Open(MenuPage.Saves)),
                (Strings.T("menu.settings", "설정"), () => Open(MenuPage.Settings)),
                (Strings.T("menu.feedback", "의견 보내기"), () => Application.OpenURL(Feedback.FormUrl())),
            };
            if (Application.platform != RuntimePlatform.WebGLPlayer && !Application.isEditor)
                items.Add((Strings.T("menu.quit", "끝내기"), Application.Quit));

            float itemHeight = Controls.UsingTouch ? 44 : Ui.Short ? 32 : 38;
            float itemWidth = Mathf.Min(300, Ui.Width - 32);
            // A phone held sideways: the logo on the left half, the words on the right.
            bool side = Ui.Short && Ui.Width >= Ui.Height * 1.5f;
            float logoTarget = side ? Ui.Width * 0.4f : Mathf.Min(Ui.Width - 48, 440);
            float pixel = Mathf.Max(1, Mathf.Floor(logoTarget * Ui.Scale / TitleArt.LogoWidth)) / Ui.Scale;
            var logoSize = new Vector2(TitleArt.LogoWidth * pixel, TitleArt.LogoHeight * pixel);
            const float tagHeight = 22;
            float column = items.Count * itemHeight;
            float gap = Ui.Short ? 10 : 30;
            Rect logo, items0;
            if (side)
            {
                float half = Ui.Width / 2;
                float top = Mathf.Round((Ui.Height - logoSize.y - tagHeight) / 2) - 10;
                logo = new Rect(Mathf.Round(half / 2 - logoSize.x / 2), top, logoSize.x, logoSize.y);
                items0 = new Rect(Mathf.Round(half + (half - itemWidth) / 2), Mathf.Round((Ui.Height - column) / 2), itemWidth, itemHeight);
            }
            else
            {
                float block = logoSize.y + tagHeight + gap + column;
                float top = Mathf.Max(8, Mathf.Round((Ui.Height - block) / 2 - (Ui.Short ? 0 : 24)));
                logo = new Rect(Mathf.Round((Ui.Width - logoSize.x) / 2), top, logoSize.x, logoSize.y);
                items0 = new Rect(Mathf.Round((Ui.Width - itemWidth) / 2), logo.yMax + tagHeight + gap, itemWidth, itemHeight);
            }
            DrawLogo(logo, pixel);
            TitleDraw.Text(new Rect(logo.center.x - 200, logo.yMax + 2, 400, tagHeight), Strings.T("menu.tagline", "동굴 아래, 무너진 왕국의 이야기"), 13,
                TitleDraw.Faint, TextAnchor.MiddleCenter);

            // A soft dark cloud behind the words, so they read over whatever of the cave drifts behind them.
            TitleDraw.Halo(new Rect(items0.x - 70, items0.y - 30, items0.width + 140, column + 60));
            GUI.enabled = ready;
            var rect = items0;
            foreach (var (label, press) in items)
            {
                if (Item(rect, label, _titleItem)) Later(press);
                rect.y += itemHeight;
            }
            GUI.enabled = true;

            // The corners: the version at the bottom right, the export reminder (web and phone saves live only in the
            // browser or the app) at the bottom left, and why the game cannot start, when it cannot, under the words.
            TitleDraw.Text(new Rect(Ui.Width - 208, Ui.Height - 22, 200, 20), string.Format(Strings.T("menu.version", "버전 {0}"), Protocol.Version), 12,
                TitleDraw.Faint, TextAnchor.MiddleRight);
            float noteWidth = Mathf.Min(side ? Ui.Width / 2 - 24 : 360, Ui.Width - 24);
            if (SaveMenu.ExportDue)
            {
                string note = Ui.Wrap(SaveMenu.ExportReminder, _note, noteWidth);
                float h = _note.CalcHeight(new GUIContent(note), noteWidth);
                var at = new Rect(12, Ui.Height - 24 - h, noteWidth, h);
                // Under the words when the bottom left would meet them.
                if (at.y < rect.y + 4 && !side) at = new Rect(Mathf.Round((Ui.Width - noteWidth) / 2), rect.y + 6, noteWidth, h);
                Note(at, note, new Color(1f, 0.82f, 0.4f));
                rect.y = Mathf.Max(rect.y, at.yMax);
            }
            if (_status.Length > 0)
            {
                float width = Mathf.Min(420, Ui.Width - 24);
                string status = Ui.Wrap(_status, _note, width);
                float h = _note.CalcHeight(new GUIContent(status), width);
                Note(new Rect(Mathf.Round((Ui.Width - width) / 2), Mathf.Min(rect.y + 6, Ui.Height - 26 - h), width, h), status, TitleDraw.Warn);
            }
        }

        // Words wrapped already, over a shadow.
        void Note(Rect rect, string text, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            _note.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _note);
            _note.normal.textColor = color;
            GUI.Label(rect, text, _note);
            _note.normal.textColor = TitleDraw.Faint;
        }

        // The logo floating up and down a little, a glint crossing it every few seconds (or when tapped); still with less
        // motion.
        void DrawLogo(Rect rect, float pixel)
        {
            bool still = Settings.ReduceMotion;
            float now = Time.unscaledTime;
            float bob = still ? 0 : Mathf.Round(Mathf.Sin(now * 1.3f) * 2 * pixel * Ui.Scale) / Ui.Scale;
            float since = now - _logoTapped;
            int frame = -1;
            if (!still)
            {
                float into = since < 1 ? since : now % 6;
                frame = Mathf.FloorToInt(into / 0.06f);
            }
            var image = frame >= 0 && frame < TitleArt.GlintFrames
                ? TitleDraw.Cached("title/logo/" + frame, () => TitleArt.LogoWithGlint(frame))
                : TitleArt.Logo;
            TitleDraw.Picture(new Vector2(rect.x, rect.y + bob), image, pixel);
            // A tap on it sends the glint across again (not a button of the menu: the playtests count those).
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _logoTapped = now;
                Sounds.Play(Sound.UiHover, 0.5f);
            }
        }

        // ---- A page: the panel, its name with [뒤로], what it holds (scrolled when long), its main action kept at the foot ----

        float PageWidth => _page switch
        {
            MenuPage.Characters or MenuPage.Worlds => 560,
            MenuPage.MakeCharacter => 660,
            MenuPage.Saves => 520,
            _ => 480,
        };

        float PageHeight => _page switch
        {
            MenuPage.Saves or MenuPage.Keys or MenuPage.Characters or MenuPage.Worlds or MenuPage.MakeCharacter => 640,
            _ => 560,
        };

        void DrawMenuPage(bool ready)
        {
            float width = Mathf.Min(PageWidth, Ui.Width - (Ui.Width < 480 ? 8 : 24));
            float height = Mathf.Min(PageHeight, Ui.Height - (Ui.Short ? 6 : 20));
            var area = new Rect(Mathf.Round((Ui.Width - width) / 2), Mathf.Round((Ui.Height - height) / 2), width, height);
            TitleDraw.Panel(area);
            float edge = (TitleArt.PanelBorder + 2) * UiSkin.Pt;
            float headHeight = Ui.Short ? 34 : 44;
            var head = new Rect(area.x + edge, area.y + edge - 4, area.width - 2 * edge, headHeight);
            TitleDraw.Heading(head, PageName(_page));
            bool asking = _ask != MenuAsk.None;
            GUI.enabled = !asking;
            if (Item(new Rect(head.x - 4, head.y + 2, 76, headHeight - 10), Strings.T("menu.back", "뒤로"), _smallItem)) Later(Back);

            var (action, error) = FootOf(ready);
            string status = _status.Length > 0 ? _status : error;
            float inner = area.width - 2 * edge;
            string wrapped = status.Length > 0 ? Ui.Wrap(status, _note, inner) : "";
            float statusHeight = wrapped.Length > 0 ? _note.CalcHeight(new GUIContent(wrapped), inner) + 4 : 0;
            float actionHeight = action ? (Controls.UsingTouch ? 48 : 42) : 0;
            float foot = statusHeight + actionHeight + (action || statusHeight > 0 ? 6 : 0);
            var body = Rect.MinMaxRect(area.x + edge, head.yMax + 4, area.xMax - edge, area.yMax - edge - foot);
            _menuColumn = Mathf.Max(120, body.width - GUI.skin.verticalScrollbar.fixedWidth - 8);

            Ui.TouchScroll(body, ref _menuScroll);
            GUILayout.BeginArea(body);
            _menuScroll = Ui.BeginScrollView(_menuScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            _navView = body.height;
            GUILayout.BeginVertical(GUILayout.Width(_menuColumn));
            switch (_page)
            {
                case MenuPage.Characters: DrawCharacterList(); break;
                case MenuPage.MakeCharacter: DrawMakeCharacter(); break;
                case MenuPage.Worlds: DrawWorldList(); break;
                case MenuPage.MakeWorld: DrawMakeWorld(); break;
                case MenuPage.Together: DrawTogetherPage(ready); break;
                case MenuPage.Saves: DrawSaves(); break;
                case MenuPage.Settings: DrawSettingsPage(); break;
                case MenuPage.Keys: DrawKeys(); break;
            }
            GUILayout.EndVertical();
            Ui.EndScrollView();
            _navView = 0;
            GUILayout.EndArea();

            float y = body.yMax + 4;
            if (statusHeight > 0)
            {
                Note(new Rect(body.x, y, inner, statusHeight), wrapped, TitleDraw.Warn);
                y += statusHeight;
            }
            if (action) DrawFoot(new Rect(body.x, y + 2, body.width, actionHeight), ready);
            GUI.enabled = true;
            if (asking) DrawAsk();
        }

        // Whether the page has a main action at its foot, and what it has to say about it (a name taken).
        (bool Action, string Error) FootOf(bool ready) => _page switch
        {
            MenuPage.Worlds => (true, ""),
            MenuPage.MakeCharacter => (true, _makeError),
            MenuPage.MakeWorld => (true, _makeWorldError),
            _ => (false, ""),
        };

        void DrawFoot(Rect foot, bool ready)
        {
            float height = foot.height - 4;
            float width = Mathf.Min(260, foot.width);
            var main = new Rect(Mathf.Round(foot.center.x - width / 2), foot.y, width, height);
            switch (_page)
            {
                case MenuPage.Worlds:
                {
                    bool can = ready && !_connecting && PickedWorldReady;
                    var was = GUI.enabled;
                    GUI.enabled = was && can;
                    if (Item(main, StartLabel, _mainButton, Ui.Look.Primary)) Later(StartPicked);
                    GUI.enabled = was;
                    break;
                }
                case MenuPage.MakeCharacter:
                {
                    float half = Mathf.Min(220, (foot.width - 12) / 2);
                    var dice = new Rect(Mathf.Round(foot.center.x - half - 6), foot.y, half, height);
                    var make = new Rect(Mathf.Round(foot.center.x + 6), foot.y, half, height);
                    if (Item(dice, Strings.T("title.dice", "주사위"), _pageItem)) Later(RollLook);
                    if (Item(make, Strings.T("title.make", "만들기"), _mainButton, Ui.Look.Primary)) Later(MakeCharacter);
                    break;
                }
                case MenuPage.MakeWorld:
                    if (Item(main, Strings.T("title.make", "만들기"), _mainButton, Ui.Look.Primary)) Later(MakeWorld);
                    break;
            }
        }

        // A page's name at its head.
        static string PageName(MenuPage page) => page switch
        {
            MenuPage.Characters => Strings.T("title.characters", "캐릭터 고르기"),
            MenuPage.MakeCharacter => Strings.T("title.make_character", "새 캐릭터 만들기"),
            MenuPage.Worlds => Strings.T("title.worlds", "월드 고르기"),
            MenuPage.MakeWorld => Strings.T("title.make_world", "새 월드 만들기"),
            MenuPage.Together => Strings.T("menu.together", "함께 하기"),
            MenuPage.Saves => Strings.T("menu.saves", "저장본"),
            MenuPage.Settings => Strings.T("menu.settings", "설정"),
            MenuPage.Keys => Strings.T("menu.keys", "키 바꾸기"),
            _ => "",
        };

        // A small heading over a group on a page.
        void Heading(string text)
        {
            GUILayout.Space(6);
            var rect = GUILayoutUtility.GetRect(_menuColumn, 22);
            TitleDraw.Text(rect, text, 13, TitleDraw.GoldInk);
        }

        // A line or lines of faint words, wrapped to the page.
        void PageNote(string text, Color? color = null)
        {
            string wrapped = Ui.Wrap(text, _note, _menuColumn - 4);
            float height = _note.CalcHeight(new GUIContent(wrapped), _menuColumn - 4);
            var rect = GUILayoutUtility.GetRect(_menuColumn, height + 2);
            Note(new Rect(rect.x + 2, rect.y, rect.width - 4, height), wrapped, color ?? TitleDraw.Faint);
        }

        // ---- A row of a list: its band, a press over all of it but its [지우기] at the right ----

        // True when the row (not its [지우기]) is pressed; deleted when its [지우기] is.
        bool ListRow(Rect rect, string name, bool picked, bool enabled, bool deletable, out bool deleted, Action<Rect, TitleState> draw)
        {
            deleted = false;
            var e = Event.current;
            float deleteWidth = deletable ? (Controls.UsingTouch ? 70 : 62) : 0;
            var press = new Rect(rect.x, rect.y, rect.width - deleteWidth, rect.height);
            Ui.Row(name);
            var was = GUI.enabled;
            GUI.enabled = was && enabled;
            bool fire = NavSlot(press, out bool focused);
            bool hover = GUI.enabled && press.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            bool down = hover && Ui.Held && press.Contains(Ui.DownAt);
            var state = down ? TitleState.Pressed : picked ? TitleState.Selected : hover || focused ? TitleState.Hover : TitleState.Normal;
            if (e.type == EventType.Repaint)
            {
                TitleDraw.Row(rect, state);
                draw(rect, state);
            }
            bool pressed = Ui.Invisible(press, name) || fire;
            GUI.enabled = was;
            if (deletable)
            {
                float h = Mathf.Min(rect.height - 8, Controls.UsingTouch ? 44 : 30);
                var delete = new Rect(rect.xMax - deleteWidth, Mathf.Round(rect.center.y - h / 2), deleteWidth - 4, h);
                deleted = Item(delete, Strings.T("title.delete", "지우기"), _smallItem);
            }
            Ui.Row("");
            return pressed;
        }

        float ListRowHeight => Controls.UsingTouch ? 66 : 60;

        // The dashed row that makes a new one.
        bool NewRow(string label)
        {
            GUILayout.Space(2);
            var rect = GUILayoutUtility.GetRect(_menuColumn, Controls.UsingTouch ? 48 : 40);
            var e = Event.current;
            bool fire = NavSlot(rect, out bool focused);
            bool hover = GUI.enabled && rect.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            bool down = hover && Ui.Held && rect.Contains(Ui.DownAt);
            TitleDraw.NewRow(rect, label, down ? TitleState.Pressed : focused ? TitleState.Selected : hover ? TitleState.Hover : TitleState.Normal);
            return Ui.Invisible(rect, label) || fire;
        }

        string ClassNames(IEnumerable<string> ids) => _classes == null ? "" :
            string.Join(" + ", ids.Select(id => _classes.Classes.FirstOrDefault(c => c.Id == id)?.Name).Where(n => n != null));

        // ---- The character list ----

        void DrawCharacterList()
        {
            GUILayout.Space(4);
            if (_flow == MenuFlow.Together) PageNote(Strings.T("title.together_pick", "함께 할 캐릭터를 고른다"));
            if (_characterList.Count == 0 && _newCharacter == null) PageNote(Strings.T("title.no_characters", "아직 캐릭터가 없다. 새 캐릭터를 만든다"));
            int row = 0;
            if (_newCharacter != null)
            {
                var rect = GUILayoutUtility.GetRect(_menuColumn, ListRowHeight);
                string name = _newCharacter;
                var look = _newCharacterLook;
                if (ListRow(rect, name, name == CharacterName, true, false, out _, (r, s) =>
                    CharacterFace(r, s, name, look, Strings.T("title.new_line", "새 캐릭터 · 아직 들어가지 않았다"), "", null, row)))
                    Later(() => PickCharacter(name));
                GUILayout.Space(4);
                row++;
            }
            foreach (var entry in _characterList)
            {
                var rect = GUILayoutUtility.GetRect(_menuColumn, ListRowHeight);
                var summary = entry.Summary;
                string name = entry.Name;
                int index = row++;
                string line, more;
                if (summary == null)
                {
                    line = Strings.T("title.broken", "읽을 수 없는 저장본");
                    more = entry.Broken;
                }
                else
                {
                    string classes = ClassNames(summary.Classes);
                    line = $"Lv {summary.Level}" + (classes.Length > 0 ? " · " + classes : "");
                    more = $"{TitleDraw.Played(summary.PlayedSeconds)} · {TitleDraw.Ago(summary.LastPlayedAt)}";
                }
                Biome? deepest = summary == null ? null : BiomeVisits.Deepest(summary.Visited);
                if (ListRow(rect, name, false, summary != null, true, out bool deleted, (r, s) =>
                    CharacterFace(r, s, name, summary?.Look ?? CharacterLook.Default, line, more, deepest, index, summary == null)))
                    Later(() => PickCharacter(name));
                if (deleted) Later(() => AskDelete(MenuAsk.DeleteCharacter, name));
                GUILayout.Space(4);
            }
            if (NewRow(Strings.T("title.new_character", "새 캐릭터"))) Later(StartMaking);
            GUILayout.Space(6);
        }

        // What a character's row shows: the hero walking in its look, its name, its level and classes, how long played and
        // when last, the deepest biome it has been to.
        void CharacterFace(Rect r, TitleState state, string name, CharacterLook look, string line, string more, Biome? deepest, int index, bool broken = false)
        {
            float slot = 44;
            if (!broken) TitleDraw.HeroIn(new Rect(r.x + 6, r.y + 2, slot, r.height - 6), look, 2, phase: index * 0.37f);
            float x = r.x + slot + 14;
            float right = r.xMax - (Controls.UsingTouch ? 70 : 62) - (deepest != null ? 40 : 0);
            var nameColor = broken ? TitleDraw.Faint : state == TitleState.Normal ? TitleDraw.Ink : TitleDraw.GoldInk;
            float lineHeight = Mathf.Floor((r.height - 6) / 3);
            TitleDraw.Text(new Rect(x, r.y + 3, right - x, lineHeight), name, 13, nameColor);
            TitleDraw.Text(new Rect(x, r.y + 3 + lineHeight, right - x, lineHeight), line, 12, broken ? TitleDraw.Warn : TitleDraw.Faint);
            TitleDraw.Text(new Rect(x, r.y + 3 + lineHeight * 2, right - x, lineHeight), more, 12, TitleDraw.Faint);
            if (deepest is Biome biome) TitleDraw.PictureCentred(new Vector2(right + 20, r.center.y), TitleArt.BiomeIcon(biome), 2);
        }

        void PickCharacter(string name)
        {
            _playerName = name;
            ForgetCharacterClasses();
            NewLook = name == _newCharacter ? _newCharacterLook : CharacterLook.Default;
            if (_flow == MenuFlow.Together)
            {
                Open(MenuPage.Together);
                return;
            }
            _hostKind = HostKind.Solo;
            Open(MenuPage.Worlds);
        }

        // ---- Making a character ----

        // A name not taken yet: "모험가", "모험가 2", ...
        string FreeName()
        {
            string word = Strings.T("title.default_name", "모험가");
            if (!CharacterTaken(word)) return word;
            for (int i = 2; ; i++)
                if (!CharacterTaken($"{word} {i}")) return $"{word} {i}";
        }

        void StartMaking()
        {
            RefreshSaves();
            _makeName = _makeNameAtStart = FreeName();
            _makeLook = _makeLookAtStart = CharacterLook.Default;
            _makeError = "";
            Open(MenuPage.MakeCharacter);
        }

        void RollLook()
        {
            var random = new GameRandom((ulong)DateTime.UtcNow.Ticks);
            var look = CharacterLook.Random(random);
            for (int i = 0; i < 4 && look.Equals(_makeLook); i++) look = CharacterLook.Random(random);
            _makeLook = look;
        }

        void MakeCharacter()
        {
            string name = (_makeName ?? "").Trim();
            if (name.Length == 0)
            {
                _makeError = Strings.T("title.name_empty", "이름을 적는다");
                return;
            }
            if (CharacterTaken(name))
            {
                _makeError = string.Format(Strings.T("title.name_taken", "「{0}」 캐릭터가 이미 있다. 다른 이름을 적는다"), name);
                return;
            }
            _makeError = "";
            _newCharacter = name;
            _newCharacterLook = _makeLook;
            PickCharacter(name);
        }

        // What the making page offers for each part of the look: its name, how many, its value and the names and colours.
        // Part 6, the gender (2026-11-28, Docs/Gender.md), is the first row.
        static readonly int[] LookParts = { GenderPart, 0, 1, 2, 3, 4, 5 };
        const int GenderPart = 6;
        static readonly string[] GenderNames = { "남자", "여자" };

        static string LookPartName(int part) => part switch
        {
            0 => Strings.T("look.hair", "머리 모양"),
            1 => Strings.T("look.hair_color", "머리색"),
            2 => Strings.T("look.skin", "피부"),
            3 => Strings.T("look.eyes", "눈"),
            4 => Strings.T("look.shirt", "윗옷"),
            GenderPart => Strings.T("look.gender", "성별"),
            _ => Strings.T("look.pants", "아랫옷"),
        };

        static int LookValue(CharacterLook look, int part) => part switch
        {
            0 => look.Hair,
            1 => look.HairColor,
            2 => look.Skin,
            3 => look.Eyes,
            4 => look.Shirt,
            GenderPart => look.Gender,
            _ => look.Pants,
        };

        // The gender changes the hair too when it was the other's start (CharacterLook.WithGender).
        static CharacterLook WithPart(CharacterLook look, int part, int value) => part == GenderPart ? look.WithGender(value) : new CharacterLook(
            part == 0 ? value : look.Hair, part == 1 ? value : look.HairColor, part == 2 ? value : look.Skin,
            part == 3 ? value : look.Eyes, part == 4 ? value : look.Shirt, part == 5 ? value : look.Pants, look.Gender);

        // A choice's name from the string sheet ("look.hair.2": 묶은 머리 / Ponytail), the Korean in HeroArt the same.
        static string LookValueName(int part, int value) => part switch
        {
            0 => Strings.T("look.hair." + value, HeroArt.HairNames[value]),
            1 => Strings.T("look.hair_color." + value, HeroArt.HairColorNames[value]),
            2 => Strings.T("look.skin." + value, HeroArt.SkinNames[value]),
            3 => Strings.T("look.eyes." + value, HeroArt.EyeNames[value]),
            4 => Strings.T("look.shirt." + value, HeroArt.ShirtNames[value]),
            GenderPart => Strings.T("look.gender." + value, GenderNames[value]),
            _ => Strings.T("look.pants." + value, HeroArt.PantsNames[value]),
        };

        static Rgba? LookSwatch(int part, int value) => part switch
        {
            0 or GenderPart => null,
            1 => HeroArt.HairColor(value),
            2 => HeroArt.SkinColor(value),
            3 => HeroArt.EyeColor(value),
            4 => HeroArt.ShirtColor(value),
            _ => HeroArt.PantsColor(value),
        };

        void DrawMakeCharacter()
        {
            bool wide = _menuColumn >= 520;
            float row = RowHeight;
            GUILayout.Space(4);
            if (wide)
            {
                GUILayout.BeginHorizontal();
                var preview = GUILayoutUtility.GetRect(200, 260, GUILayout.Width(200), GUILayout.Height(260));
                DrawPreview(preview, 5);
                GUILayout.Space(12);
                GUILayout.BeginVertical(GUILayout.Width(_menuColumn - 212));
                MakeFields(_menuColumn - 212, row);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                var preview = GUILayoutUtility.GetRect(_menuColumn, Ui.Short ? 92 : 124);
                DrawPreview(preview, Ui.Short ? 3 : 4);
                MakeFields(_menuColumn, row);
            }
            GUILayout.Space(6);
            if (_classes != null && _classes.Success)
                PageNote(string.Format(Strings.T("menu.classes_new", "클래스는 게임 안에서 고른다: [스킬] 화면(PC는 K·휴대폰은 [메뉴]의 [스킬])에서 처음에 하나, 레벨 {0}에 둘째, {1}에 셋째. 고르기 전에는 기본 공격만 쓴다."),
                    ClassRules.SlotLevels[1], ClassRules.SlotLevels[2]));
        }

        // The hero big in its well, turning round as it walks.
        void DrawPreview(Rect slot, float pointsPerPixel)
        {
            TitleDraw.Row(slot, TitleState.Normal);
            TitleDraw.HeroIn(new Rect(slot.x, slot.y, slot.width, slot.height - 10), _makeLook, pointsPerPixel, turning: true);
        }

        void MakeFields(float width, float row)
        {
            var nameRow = GUILayoutUtility.GetRect(width, row);
            float label = Mathf.Min(96, width * 0.3f);
            TitleDraw.Text(new Rect(nameRow.x, nameRow.y, label, nameRow.height), Strings.T("look.name", "이름"), 13, TitleDraw.Ink);
            _makeName = TitleDraw.Field(new Rect(nameRow.x + label, nameRow.y + 3, width - label, nameRow.height - 6), _makeName, 16,
                Strings.T("title.name_field", "이름 칸"), Strings.T("title.name_hint", "이름을 적는다"));
            GUILayout.Space(4);
            foreach (int part in LookParts) LookRow(part, width, row, label);
        }

        // "머리색  ◀ ■ 금빛 ▶": the arrows turn through the choices.
        void LookRow(int part, float width, float row, float label)
        {
            var rect = GUILayoutUtility.GetRect(width, row);
            string name = LookPartName(part);
            Ui.Row(name);
            int value = LookValue(_makeLook, part);
            int count = part switch
            {
                0 => CharacterLook.HairStyles,
                1 => CharacterLook.HairColors,
                2 => CharacterLook.Skins,
                3 => CharacterLook.EyeColors,
                4 => CharacterLook.ShirtColors,
                GenderPart => CharacterLook.Genders,
                _ => CharacterLook.PantsColors,
            };
            TitleDraw.Text(new Rect(rect.x, rect.y, label, rect.height), name, 13, TitleDraw.Ink);
            float arrow = Controls.UsingTouch ? 44 : 34;
            var left = new Rect(rect.x + label, rect.y, arrow, rect.height);
            var right = new Rect(rect.xMax - arrow, rect.y, arrow, rect.height);
            if (ArrowItem(left, true)) Later(() => _makeLook = WithPart(_makeLook, part, (value + count - 1) % count));
            if (ArrowItem(right, false)) Later(() => _makeLook = WithPart(_makeLook, part, (value + 1) % count));
            var middle = Rect.MinMaxRect(left.xMax + 4, rect.y, right.xMin - 4, rect.yMax);
            string valueName = LookValueName(part, value);
            float textWidth = TitleDraw.TextWidth(valueName, 13);
            var swatch = LookSwatch(part, value);
            float swatchSize = swatch != null ? 14 : 0;
            float start = Mathf.Round(middle.center.x - (textWidth + (swatchSize > 0 ? swatchSize + 6 : 0)) / 2);
            if (swatch is Rgba color && Event.current.type == EventType.Repaint)
            {
                var box = new Rect(start, Mathf.Round(middle.center.y - swatchSize / 2), swatchSize, swatchSize);
                var previous = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(box.x - 1, box.y - 1, box.width + 2, box.height + 2), Texture2D.whiteTexture);
                GUI.color = TitleDraw.ToColor(color);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = previous;
                start += swatchSize + 6;
            }
            TitleDraw.Text(new Rect(start, middle.y, textWidth + 4, middle.height), valueName, 13, TitleDraw.GoldInk);
            Ui.Row("");
        }

        // An arrow of a value row, known to the playtests as ◀ or ▶ in the row of its part.
        bool ArrowItem(Rect rect, bool left)
        {
            var e = Event.current;
            bool fire = NavSlot(rect, out bool focused);
            bool hover = GUI.enabled && rect.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            bool down = hover && Ui.Held && rect.Contains(Ui.DownAt);
            var state = down ? TitleState.Pressed : hover ? TitleState.Hover : focused ? TitleState.Selected : TitleState.Normal;
            if (e.type == EventType.Repaint)
                UiSkin.ImageCentred(rect.center, TitleDraw.Cached($"title/arrow/{left}/{state}", () => TitleArt.Arrow(left, state)));
            return Ui.Invisible(rect, left ? "◀" : "▶") || fire;
        }

        // ---- The world list ----

        string StartLabel => _pickedWorld == TestGround.Row && _hostKind == HostKind.Solo ? "시험장 들어가기" : _hostKind switch
        {
            HostKind.Online => Strings.T("title.open_room", "방 열기"),
            HostKind.Lan => string.Format(Strings.T("menu.host_lan", "호스트 (포트 {0})"), NetLink.DefaultPort),
            _ => Strings.T("menu.solo", "혼자 하기"),
        };

        bool PickedWorldReady => _pickedWorld.Length > 0
            && (_pickedWorld == _newWorld || _worldList.Any(w => w.Name == _pickedWorld && !w.IsBroken) || _pickedWorld == TestGround.Row && _hostKind == HostKind.Solo);

        void DrawWorldList()
        {
            GUILayout.Space(4);
            // Who is going: the character's look, name and level, and its classes (picked in the game).
            var who = GUILayoutUtility.GetRect(_menuColumn, 44);
            var mine = _characterList.FirstOrDefault(c => c.Name == CharacterName)?.Summary;
            var look = mine?.Look ?? (CharacterName == _newCharacter ? _newCharacterLook : CharacterLook.Default);
            TitleDraw.HeroIn(new Rect(who.x + 2, who.y, 40, who.height), look, 2);
            string headline = mine != null ? $"{CharacterName} · Lv {mine.Level}" : $"{CharacterName} · " + Strings.T("title.new_short", "새 캐릭터");
            TitleDraw.Text(new Rect(who.x + 50, who.y, who.width - 50, who.height), headline, 13, TitleDraw.GoldInk);
            SyncCharacterClasses();
            if (_savedClasses != null && _classes != null)
                PageNote(string.Format(Strings.T("menu.classes_of", "{0}의 클래스: {1}"), CharacterName, ClassNames(_savedClasses)));
            else PageNote(Strings.T("title.classes_later", "클래스는 게임 안 [스킬] 화면에서 고른다"));
            if (_hostKind != HostKind.Solo) PageNote(Strings.T("title.host_pick", "방에서 열 월드를 고른다"));
            GUILayout.Space(4);

            // The test ground (Docs/TestGround.md): alone, never saved, the character played as a copy. Not for a room.
            if (_hostKind == HostKind.Solo)
            {
                var rect = GUILayoutUtility.GetRect(_menuColumn, ListRowHeight);
                if (ListRow(rect, TestGround.Name, _pickedWorld == TestGround.Row, true, false, out _, (r, s) =>
                    WorldFace(r, s, TestGround.Name, "보스·던전·들판으로 바로 · 장비와 레벨을 마음대로", "저장되지 않는다 · 이 캐릭터의 복사본으로 한다", Biome.Crystal, false, true)))
                    Later(() => PressWorld(TestGround.Row));
                GUILayout.Space(4);
            }

            if (_newWorld != null)
            {
                var rect = GUILayoutUtility.GetRect(_menuColumn, ListRowHeight);
                string name = _newWorld;
                string seed = NewWorldSeedLabel;
                if (ListRow(rect, name, _pickedWorld == name, true, false, out _, (r, s) =>
                    WorldFace(r, s, name, Strings.T("title.new_world_line", "새 월드 · 아직 들어가지 않았다"), string.Format(Strings.T("title.seed_line", "시드 {0}"), seed), Biome.Cave, false)))
                    Later(() => PressWorld(name));
                GUILayout.Space(4);
            }
            if (_worldList.Count == 0 && _newWorld == null) PageNote(Strings.T("title.no_worlds", "아직 월드가 없다. 새 월드를 만든다"));
            foreach (var entry in _worldList)
            {
                var rect = GUILayoutUtility.GetRect(_menuColumn, ListRowHeight);
                var summary = entry.Summary;
                string name = entry.Name;
                string line, more;
                bool been = false;
                if (summary == null)
                {
                    line = Strings.T("title.broken", "읽을 수 없는 저장본");
                    more = entry.Broken;
                }
                else
                {
                    line = string.Format(Strings.T("title.world_line", "{0}일째 · {1}"), summary.Day, TitleDraw.Played(summary.PlayedSeconds));
                    // This character's map of it: it has been there.
                    been = _beenWorlds.Contains(name);
                    more = TitleDraw.Ago(summary.LastPlayedAt) + (been ? " · " + Strings.T("title.been", "가 본 월드") : "");
                }
                var deepest = summary?.Deepest ?? Biome.Cave;
                if (ListRow(rect, name, _pickedWorld == name, summary != null, true, out bool deleted, (r, s) => WorldFace(r, s, name, line, more, deepest, summary == null, been)))
                    Later(() => PressWorld(name));
                if (deleted) Later(() => AskDelete(MenuAsk.DeleteWorld, name));
                GUILayout.Space(4);
            }
            if (NewRow(Strings.T("title.new_world", "새 월드"))) Later(StartMakingWorld);
            GUILayout.Space(6);
        }

        void WorldFace(Rect r, TitleState state, string name, string line, string more, Biome biome, bool broken, bool been = false)
        {
            if (!broken) TitleDraw.PictureCentred(new Vector2(r.x + 26, r.center.y), TitleArt.BiomeIcon(biome), 2);
            float x = r.x + 54;
            float right = r.xMax - (Controls.UsingTouch ? 70 : 62);
            var nameColor = broken ? TitleDraw.Faint : state == TitleState.Normal ? TitleDraw.Ink : TitleDraw.GoldInk;
            float lineHeight = Mathf.Floor((r.height - 6) / 3);
            TitleDraw.Text(new Rect(x, r.y + 3, right - x, lineHeight), name, 13, nameColor);
            TitleDraw.Text(new Rect(x, r.y + 3 + lineHeight, right - x, lineHeight), line, 12, broken ? TitleDraw.Warn : TitleDraw.Faint);
            TitleDraw.Text(new Rect(x, r.y + 3 + lineHeight * 2, right - x, lineHeight), more, 12, been ? TitleDraw.GoldInk : TitleDraw.Faint);
        }

        // A world's row: picked, and pressed again it starts.
        void PressWorld(string name)
        {
            if (_pickedWorld == name && PickedWorldReady && !_connecting)
            {
                StartPicked();
                return;
            }
            _pickedWorld = name;
        }

        // The picked character in the picked world, alone, in an online room or as a LAN host.
        void StartPicked()
        {
            if (!PickedWorldReady) return;
            if (_pickedWorld == TestGround.Row)
            {
                StartTestGround();
                return;
            }
            _worldName = _pickedWorld;
            _startSeed = _pickedWorld == _newWorld ? _newWorldSeed : (ulong?)null;
            NewLook = CharacterName == _newCharacter ? _newCharacterLook : CharacterLook.Default;
            switch (_hostKind)
            {
                case HostKind.Online:
                    StartOnlineHost();
                    break;
                case HostKind.Lan:
                    StartHost();
                    break;
                default:
                    StartSolo();
                    break;
            }
        }

        // ---- Making a world ----

        void StartMakingWorld()
        {
            RefreshSaves();
            _makeWorldName = FreeWorldName();
            _makeSeed = "";
            _makeWorldError = "";
            Open(MenuPage.MakeWorld);
        }

        string FreeWorldName()
        {
            var random = new GameRandom((ulong)DateTime.UtcNow.Ticks);
            for (int i = 0; i < 20; i++)
            {
                string name = NewWorld.RandomName(random, Settings.Language == "en");
                if (!WorldTaken(name)) return name;
            }
            return NewWorld.RandomName(random, Settings.Language == "en") + " " + random.Next(2, 100);
        }

        void DrawMakeWorld()
        {
            float row = RowHeight;
            float label = Mathf.Min(96, _menuColumn * 0.3f);
            GUILayout.Space(6);
            var nameRow = GUILayoutUtility.GetRect(_menuColumn, row);
            float dice = Controls.UsingTouch ? 84 : 76;
            TitleDraw.Text(new Rect(nameRow.x, nameRow.y, label, nameRow.height), Strings.T("look.name", "이름"), 13, TitleDraw.Ink);
            _makeWorldName = TitleDraw.Field(new Rect(nameRow.x + label, nameRow.y + 3, _menuColumn - label - dice - 4, nameRow.height - 6), _makeWorldName, NewWorld.NameLength,
                Strings.T("title.world_field", "월드 이름 칸"));
            if (Item(new Rect(nameRow.xMax - dice, nameRow.y, dice, nameRow.height), Strings.T("title.dice", "주사위"), _smallItem)) Later(() => _makeWorldName = FreeWorldName());
            GUILayout.Space(4);
            var seedRow = GUILayoutUtility.GetRect(_menuColumn, row);
            TitleDraw.Text(new Rect(seedRow.x, seedRow.y, label, seedRow.height), Strings.T("title.seed", "시드"), 13, TitleDraw.Ink);
            _makeSeed = TitleDraw.Field(new Rect(seedRow.x + label, seedRow.y + 3, _menuColumn - label, seedRow.height - 6), _makeSeed, NewWorld.SeedLength,
                Strings.T("title.seed_field", "시드 칸"), Strings.T("title.seed_hint", "비우면 무작위"));
            GUILayout.Space(4);
            // How long a day takes in the worlds this player opens or hosts; joiners go by the host's (Docs/Time.md).
            DayLengthRow();
            GUILayout.Space(6);
            PageNote(Strings.T("title.seed_note", "같은 시드를 적으면 같은 동굴이 만들어진다. 숫자가 아니면 글자로 셈한다. 하루 길이는 설정에서도 바꾼다"));
        }

        void DayLengthRow()
        {
            string name = Strings.T("title.daylength_name", "하루 길이");
            string full = string.Format(Strings.T("menu.daylength", "하루 길이: {0}분"), Settings.DayMinutes);
            string value = full.Contains(": ") ? full.Substring(full.IndexOf(": ", StringComparison.Ordinal) + 2) : full;
            if (SettingRow(name, value, full)) Later(() => Settings.DayMinutes = Settings.NextDayMinutes());
        }

        void MakeWorld()
        {
            string name = (_makeWorldName ?? "").Trim();
            if (name.Length == 0)
            {
                _makeWorldError = Strings.T("title.name_empty", "이름을 적는다");
                return;
            }
            if (WorldTaken(name))
            {
                _makeWorldError = string.Format(Strings.T("title.world_taken", "「{0}」 월드가 이미 있다. 다른 이름을 적는다"), name);
                return;
            }
            _makeWorldError = "";
            _newWorld = name;
            // Nine digits when left to chance (NewWorld.RandomSeed), what was typed otherwise.
            _newWorldSeed = NewWorld.Seed(_makeSeed, NewWorld.RandomSeed((ulong)DateTime.UtcNow.Ticks));
            _newWorldSeedTyped = _makeSeed;
            _pickedWorld = name;
            Open(MenuPage.Worlds);
        }

        // ---- Asking before deleting or throwing away ----

        void AskDelete(MenuAsk ask, string name)
        {
            _ask = ask;
            _askName = name;
            _askChoice = 1;
        }

        void DrawAsk()
        {
            var screen = Ui.FullScreen;
            TitleDraw.Veil(screen, 0.55f);
            string question = _ask switch
            {
                MenuAsk.DeleteCharacter => string.Format(Strings.T("title.ask_character", "「{0}」 캐릭터를 지운다. 가방과 레벨, 지도까지 모두 사라지고 되돌릴 수 없다"), _askName),
                MenuAsk.DeleteWorld => string.Format(Strings.T("title.ask_world", "「{0}」 월드를 지운다. 지은 것과 상자가 모두 사라지고 되돌릴 수 없다"), _askName),
                _ => Strings.T("title.ask_discard", "만들던 캐릭터를 버린다"),
            };
            string act = _ask == MenuAsk.Discard ? Strings.T("title.discard", "버리기") : Strings.T("title.delete", "지우기");
            string keep = _ask == MenuAsk.Discard ? Strings.T("title.keep_making", "계속 만들기") : Strings.T("title.keep", "남기기");
            float width = Mathf.Min(400, Ui.Width - 24);
            string wrapped = Ui.Wrap(question, _note, width - 40);
            float textHeight = _note.CalcHeight(new GUIContent(wrapped), width - 40);
            float button = Controls.UsingTouch ? 48 : 40;
            float height = textHeight + button + 60;
            var area = new Rect(Mathf.Round((Ui.Width - width) / 2), Mathf.Round((Ui.Height - height) / 2), width, height);
            TitleDraw.Panel(area);
            Note(new Rect(area.x + 20, area.y + 20, width - 40, textHeight), wrapped, TitleDraw.Ink);
            float half = (width - 52) / 2;
            var yes = new Rect(area.x + 20, area.yMax - 20 - button, half, button);
            var no = new Rect(area.xMax - 20 - half, yes.y, half, button);
            Ui.Row(_askName);
            bool focusKeys = !Controls.UsingTouch;
            bool actPressed = Ui.Button(yes, act, act, _mainButton, Ui.Look.Danger, focusKeys && _askChoice == 0) || _askFire && _askChoice == 0;
            bool keepPressed = Ui.Button(no, keep, keep, _pageItem, Ui.Look.Text, focusKeys && _askChoice == 1) || _askFire && _askChoice == 1;
            Ui.Row("");
            if (actPressed)
            {
                var ask = _ask;
                string name = _askName;
                Later(() => Answer(ask, name));
            }
            else if (keepPressed) Later(() => _ask = MenuAsk.None);
        }

        void Answer(MenuAsk ask, string name)
        {
            _ask = MenuAsk.None;
            switch (ask)
            {
                case MenuAsk.DeleteCharacter:
                    SaveList.DeleteCharacter(name);
                    if (CharacterName == name) _playerName = "";
                    RefreshSaves();
                    break;
                case MenuAsk.DeleteWorld:
                    SaveList.DeleteWorld(name);
                    if (_pickedWorld == name) _pickedWorld = "";
                    RefreshSaves();
                    break;
                case MenuAsk.Discard:
                    Open(MenuPage.Characters);
                    break;
            }
        }

        // ---- 함께 하기: online through Unity Relay (the same on the web and on PC, so they play together), on PC the LAN too ----

        void DrawTogetherPage(bool ready)
        {
            GUILayout.Space(4);
            var who = GUILayoutUtility.GetRect(_menuColumn, 40);
            var mine = _characterList.FirstOrDefault(c => c.Name == CharacterName)?.Summary;
            TitleDraw.HeroIn(new Rect(who.x + 2, who.y, 40, who.height), mine?.Look ?? (CharacterName == _newCharacter ? _newCharacterLook : CharacterLook.Default), 2);
            TitleDraw.Text(new Rect(who.x + 50, who.y, who.width - 50, who.height), CharacterName, 13, TitleDraw.GoldInk);
            var was = GUI.enabled;
            GUI.enabled = was && ready && !_connecting;
            Heading(Strings.T("menu.online", "온라인"));
            PageNote(Strings.T("menu.online_note", "온라인 협동: 방을 만들면 코드가 나온다. 친구는 그 코드로 참가한다(웹과 PC가 함께)"));
            var host = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
            string hostLabel = _connecting ? Strings.T("menu.connecting", "연결하는 중…") : Strings.T("menu.host_online", "온라인 방 만들기");
            if (Item(host, hostLabel, _pageItem)) Later(() =>
            {
                _hostKind = HostKind.Online;
                Open(MenuPage.Worlds);
            });
            var code = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
            float join = Mathf.Min(150, _menuColumn * 0.45f);
            _codeInput = TitleDraw.Field(new Rect(code.x, code.y + 3, code.width - join - 8, code.height - 6), _codeInput, 12, Strings.T("title.code_field", "코드 칸"),
                Strings.T("title.code_hint", "방 코드"));
            if (Item(new Rect(code.xMax - join, code.y, join, code.height), Strings.T("menu.join_code", "코드로 참가"), _pageItem)) Later(StartOnlineJoin);
            // A browser cannot listen for connections, so the LAN way is only on PC.
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Heading(Strings.T("menu.lan", "같은 네트워크(LAN)"));
                var lan = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
                if (Item(lan, string.Format(Strings.T("menu.host_lan", "호스트 (포트 {0})"), NetLink.DefaultPort), _pageItem)) Later(() =>
                {
                    _hostKind = HostKind.Lan;
                    Open(MenuPage.Worlds);
                });
                var address = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
                _address = TitleDraw.Field(new Rect(address.x, address.y + 3, address.width - join - 8, address.height - 6), _address, 64,
                    Strings.T("menu.host_address", "호스트 주소"));
                if (Item(new Rect(address.xMax - join, address.y, join, address.height), Strings.T("menu.join", "참가"), _pageItem)) Later(() => StartJoining());
            }
            GUI.enabled = was;
        }

        // ---- 설정: sound, the screen and play, a row each, its name on the left and its value on the right ----

        // "name: value" as the playtests know it; the value is a text item that turns the setting.
        bool SettingRow(string name, string value, string probe = null)
        {
            var rect = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
            Ui.Row(name);
            float valueWidth = Mathf.Min(170, _menuColumn * 0.45f);
            var e = Event.current;
            if (e.type == EventType.Repaint && rect.Contains(e.mousePosition) && !Controls.UsingTouch) TitleDraw.Row(rect, TitleState.Hover);
            TitleDraw.Text(new Rect(rect.x + 8, rect.y, rect.width - valueWidth - 12, rect.height), name, 13, TitleDraw.Ink);
            bool pressed = Item(new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height), value, _pageItem, probe: probe ?? $"{name}: {value}");
            Ui.Row("");
            return pressed;
        }

        void DrawSettingsPage()
        {
            Heading(Strings.T("menu.sound_group", "소리"));
            // Each press turns it up a quarter, and from full back to off. The menu plays the cave's tune, so the
            // change is heard at once.
            if (SettingRow(Strings.T("menu.sound", "효과음"), Settings.VolumeText)) Later(() =>
            {
                Settings.Volume = (Settings.Volume + 1) % (Settings.MaxVolume + 1);
                Sounds.Play(Sound.Click);
            });
            if (SettingRow(Strings.T("menu.music", "음악"), Settings.MusicVolumeText)) Later(() => Settings.MusicVolume = (Settings.MusicVolume + 1) % (Settings.MaxVolume + 1));
            // CC0 recordings (Recordings, 2026-11-11) or the sounds made in code, to compare.
            if (SettingRow(Strings.T("menu.sound_kind", "소리 종류"), Settings.RecordedSounds ? Strings.T("menu.sound_recorded", "녹음") : Strings.T("menu.sound_made", "합성"))) Later(() =>
            {
                Settings.RecordedSounds = !Settings.RecordedSounds;
                Sounds.Play(Sound.Click);
            });
            Heading(Strings.T("menu.screen_group", "화면"));
            if (SettingRow(Strings.T("menu.darkness", "어둠과 빛"), Strings.OnOff(Settings.Darkness))) Later(() => Settings.Darkness = !Settings.Darkness);
            // The pixel font or the smooth one; the menu is drawn in the new one at once (Ui).
            if (SettingRow(Strings.T("menu.font", "글꼴"), Settings.PixelFont ? Strings.T("menu.font_pixel", "도트") : Strings.T("menu.font_smooth", "매끈"))) Later(() => Settings.PixelFont = !Settings.PixelFont);
            // The words and windows bigger or smaller than the screen's own measure (Ui.PointScale), drawn so at once.
            if (SettingRow(Strings.T("menu.ui_size", "UI 크기"), Settings.UiSize + "%")) Later(() => Settings.UiSize = Settings.NextUiSize());
            // 60 or 30 frames a second, on a phone's own build only (FrameRate).
            if (FrameRate.Choosable && SettingRow(Strings.T("menu.frames", "프레임"), Settings.SmoothFrames ? "60" : "30")) Later(() => Settings.SmoothFrames = !Settings.SmoothFrames);
            if (SettingRow(Strings.T("menu.shake", "화면 흔들림"), Strings.OnOff(Settings.ScreenShake))) Later(() => Settings.ScreenShake = !Settings.ScreenShake);
            if (SettingRow(Strings.T("menu.bossintro", "보스 등장 연출"), Strings.OnOff(Settings.BossIntro))) Later(() => Settings.BossIntro = !Settings.BossIntro);
            // Canopies and roofs fading while someone is behind them (WorldView's standing layer, 묶음 B0).
            if (SettingRow(Strings.T("menu.fade", "물체 뒤 흐리게"), Strings.OnOff(Settings.FadeBehind))) Later(() => Settings.FadeBehind = !Settings.FadeBehind);
            // Windows growing as they open, buttons sinking and shaking, counts popping (UiMotion), the title's drifting
            // cave and floating logo; reduced, only fades and a still picture.
            if (SettingRow(Strings.T("menu.motion", "움직임"), Settings.ReduceMotion ? Strings.T("menu.motion_reduced", "줄이기") : Strings.T("menu.motion_normal", "보통")))
                Later(() => Settings.ReduceMotion = !Settings.ReduceMotion);
            // The words' language, when the string sheet has another (Strings, Docs/Strings.md).
            if (Strings.HasOthers && SettingRow(Strings.T("menu.language", "언어"), Strings.NameOf(Settings.Language))) Later(() => Settings.Language = Strings.NextLanguage());
            Heading(Strings.T("menu.play_group", "플레이"));
            // The goals for new players in the HUD; met goals still count while it is off (Settings.Guide).
            if (SettingRow(Strings.T("menu.guide", "목표 안내"), Strings.OnOff(Settings.Guide))) Later(() => Settings.Guide = !Settings.Guide);
            // Rumours or arrows, and slow or quick rest (묶음 4c, the person's call 2026-11-21: on, with the old way a
            // press away). Rest is the host's: it holds in the worlds this player opens or hosts.
            if (SettingRow(Strings.T("menu.rumors", "길 안내"), Settings.Rumors ? Strings.T("menu.rumors_on", "소문") : Strings.T("menu.rumors_off", "화살표")))
                Later(() => Settings.Rumors = !Settings.Rumors);
            if (SettingRow(Strings.T("menu.rest", "쉴 때 회복"), Settings.QuickRest ? Strings.T("menu.rest_quick", "빠름(예전)") : Strings.T("menu.rest_slow", "느림")))
                Later(() => Settings.QuickRest = !Settings.QuickRest);
            // The length of a day, also on the new world page (2026-11-25): it holds for the worlds this player opens or hosts.
            DayLengthRow();
            PageNote(Strings.T("menu.rumors_note", "소문: 갈 곳을 방향과 생물군계로만 알려 주고 지도에는 옅은 원을 그린다. 느림: 쉬면 8초 뒤 초당 1.5% 찬다(예전은 6초 뒤 3%). 야영지·제단·물약으로 회복한다"));
            // Frames a second, the dark's cost and the last inputs, for checking the game on a device (DevInfo).
            if (SettingRow(Strings.T("menu.devinfo", "개발 정보"), Strings.OnOff(Settings.DevInfo))) Later(() => Settings.DevInfo = !Settings.DevInfo);
            // Keys only matter with a keyboard.
            if (!Controls.UsingTouch)
            {
                GUILayout.Space(6);
                var keys = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
                if (Item(keys, Strings.T("menu.keys", "키 바꾸기"), _pageItem)) Later(() => Open(MenuPage.Keys));
            }
            GUILayout.Space(6);
        }

        void DrawSaves()
        {
            _saveMenu ??= new SaveMenu(name => _playerName = name, name => _worldName = name);
            _saveMenu.Draw(_menuColumn, CharacterName, WorldName);
        }

        // A row per changeable key: what it does and its key. Pressing the key's item waits for the next key pressed.
        void DrawKeys()
        {
            PageNote(Strings.T("menu.keys_note", "키 바꾸기: 단추를 누른 뒤 쓸 키를 누른다. 다른 일에 쓰던 키면 서로 바뀐다. Esc는 그만두기"));
            foreach (var action in KeyBindings.Actions)
            {
                string name = KeyBindings.ActionName(action);
                var rect = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
                Ui.Row(name);
                float valueWidth = Mathf.Min(170, _menuColumn * 0.5f);
                TitleDraw.Text(new Rect(rect.x + 8, rect.y, rect.width - valueWidth - 12, rect.height), name, 13, TitleDraw.Ink);
                bool waiting = KeyBindings.Capturing == action;
                if (Item(new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height), waiting ? Strings.T("menu.press_key", "키를 누르세요…") : KeyBindings.Name(action), _pageItem))
                    Later(() => KeyBindings.StartCapture(action));
                Ui.Row("");
            }
            if (KeyBindings.Message.Length > 0) PageNote(KeyBindings.Message, TitleDraw.Warn);
            var reset = GUILayoutUtility.GetRect(_menuColumn, RowHeight);
            if (Item(reset, Strings.T("menu.keys_reset", "처음 키로"), _pageItem)) Later(KeyBindings.ResetAll);
            PageNote(Strings.T("menu.keys_fixed", "이동 WASD·화살표, 공격 스페이스·마우스, 메뉴 Esc는 바꿀 수 없다."));
        }

        // ---- For the playtests and the play mode tests ----

        internal string TitlePage => _client != null && _screens.Current != GameScreen.Settings ? "" : _page.ToString().ToLowerInvariant();
        internal string TitleFlow => _flow.ToString().ToLowerInvariant();
        // The test ground's row as "testground" (its key is no name a world can have).
        internal string PickedWorld => _pickedWorld == TestGround.Row ? "testground" : _pickedWorld;
        internal string NewCharacterName => _newCharacter ?? "";
        internal string NewWorldName => _newWorld ?? "";
        internal string TitleAsk => _ask == MenuAsk.None ? "" : _ask.ToString().ToLowerInvariant();
        internal IEnumerable<string> CharacterList => _characterList.Select(c => c.Name);
        internal IEnumerable<string> WorldList => _worldList.Select(w => w.Name);
        // A character's row as the list writes it: "name|Lv 3 · 전사|12분 · 오늘|look" (the look packed, hex).
        internal IEnumerable<string> CharacterRows => _characterList.Select(c => c.Summary == null ? $"{c.Name}|broken|{c.Broken}|"
            : $"{c.Name}|Lv {c.Summary.Level}|{TitleDraw.Played(c.Summary.PlayedSeconds)} · {TitleDraw.Ago(c.Summary.LastPlayedAt)}|{c.Summary.Look.Packed:x6}");
        internal IEnumerable<string> WorldRows => _worldList.Select(w => w.Summary == null ? $"{w.Name}|broken|{w.Broken}"
            : $"{w.Name}|{w.Summary.Day}|{TitleDraw.Played(w.Summary.PlayedSeconds)} · {TitleDraw.Ago(w.Summary.LastPlayedAt)}");
        internal CharacterLook MakingLook => _makeLook;

        // Opens a page of the title by name (main, characters, make, worlds, newworld, together, saves, settings, keys); false
        // for no such page or while playing.
        internal bool OpenTitle(string page)
        {
            if (_client != null || Loading) return false;
            MenuPage? target = page switch
            {
                "main" => MenuPage.Main,
                "characters" => MenuPage.Characters,
                "worlds" => MenuPage.Worlds,
                "together" => MenuPage.Together,
                "saves" => MenuPage.Saves,
                "settings" => MenuPage.Settings,
                "keys" => MenuPage.Keys,
                _ => null,
            };
            if (page == "make")
            {
                StartMaking();
                return true;
            }
            if (page == "newworld")
            {
                StartMakingWorld();
                return true;
            }
            if (target == null) return false;
            if (target == MenuPage.Characters) _flow = MenuFlow.Solo;
            if (target == MenuPage.Worlds) _hostKind = HostKind.Solo;
            Open(target.Value);
            return true;
        }

        // Back to the title after a session (its lists read again: a character's time and when it last played changed).
        void BackToTitle()
        {
            _page = MenuPage.Main;
            _ask = MenuAsk.None;
            _visitedHere = -1;
            _startSeed = null;
        }
    }
}
