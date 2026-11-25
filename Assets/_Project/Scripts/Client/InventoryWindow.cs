using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bag and the screens that were once its tabs (Docs/UiRework.md U2, U3): the bag (Tab or I) with its gear,
    // materials and crafting; the growth screens (skills K, passives P, character U) and the journal (J), each its own
    // full-screen window with its own head and close button and the tabs of its group (ScreenStack); and the encounter
    // windows (U3, InventoryWindow.Encounters.cs): a merchant's shop, a chest, a crafting station and the anvil, each
    // beside the bag's things. Which is open is the screen manager's (ScreenStack); this draws it. Every button only asks
    // the host for the change; the window redraws from the next inventory message.
    sealed partial class InventoryWindow
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
            // Only a tab of the records group: pressing it shows the map (MapView), not a page here.
            Map,
        }

        const float MaxWidth = 520;
        const float TabHeight = 40;
        const float TooltipWidth = 320;
        // Narrower than this beside the minimap column, the window goes under it instead.
        const float MinWidth = 300;
        // Points one notch of the mouse wheel scrolls; IMGUI's own step is a fraction of a line in browsers.
        const float WheelStep = 30;

        static readonly string[] PageNames = { "장비", "재료·제작", "스킬", "특성", "상자", "상인", "캐릭터", "일지", "지도" };
        static readonly GUIContent[] PageLabels = PageNames.Select(name => new GUIContent(name)).ToArray();

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        readonly Hotbar _hotbar;
        readonly ScreenStack _screens;
        // The bag's page while it is shut or another screen is up, so it opens again where it was left.
        Page _bagPage;
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

        public InventoryWindow(WorldClient client, Action<byte[]> send, Hotbar hotbar = null, ScreenStack screens = null)
        {
            _client = client;
            _send = send;
            _hotbar = hotbar;
            _screens = screens ?? new ScreenStack();
        }

        // Whether one of the screens drawn here is up (the bag, a growth screen, the journal or an encounter window; not
        // the map). Setting it
        // true shows the bag (in place of any other screen); false shuts whichever of them is open.
        public bool Open
        {
            get => Draws(_screens.Current);
            set
            {
                if (value) _screens.Show(GameScreen.Bag);
                else if (!value && Open) _screens.Close();
            }
        }

        public static bool Draws(GameScreen screen) => screen != GameScreen.None && screen != GameScreen.Map && screen != GameScreen.Settings;

        // The bag's own pages (the rest are screens of their own; the chest's and the merchant's are encounter windows
        // since U3).
        static bool InBag(Page page) => page is Page.Gear or Page.Materials;

        static GameScreen ScreenOf(Page page) => page switch
        {
            Page.Classes => GameScreen.Skills,
            Page.Passives => GameScreen.Passives,
            Page.Character => GameScreen.Character,
            Page.Journal => GameScreen.Journal,
            Page.Map => GameScreen.Map,
            _ => GameScreen.Bag,
        };

        // The page the open screen shows.
        Page PageOf(GameScreen screen) => screen switch
        {
            GameScreen.Skills => Page.Classes,
            GameScreen.Passives => Page.Passives,
            GameScreen.Character => Page.Character,
            GameScreen.Journal => Page.Journal,
            _ => _bagPage,
        };
        // The pointer is over a button that works the ground right ahead (plant, water, till, fertilize, lay a floor):
        // the world's frame then marks World.TileAhead, not where a thing would be put (2026-11-09).
        public bool PointsAtSoil { get; private set; }
        // And the size of what it would put down (a big machine 2), for the frame (2026-11-09).
        public int PointsAtSize { get; private set; } = 1;

        // Marks the button just drawn as working the ground ahead, when the pointer is over it.
        void Soil()
        {
            if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) PointsAtSoil = true;
        }

        // Opens the page with that old tab name (장비, 재료·제작 in the bag; 스킬, 특성, 캐릭터, 일지, 지도 as their own
        // screens; 상자 the chest window, 상인 or a stall keeper's role or name the shop, U3), or an encounter window by its
        // name (상점, 상자, 작업대, 모루), for G, the goal list and the tests (Playtest "bag <tab>", the play mode tests'
        // ShowBag).
        internal void Show(string page)
        {
            var meeting = ScreenStack.FromName(page);
            if (ScreenStack.IsEncounter(meeting) || _client.Npcs != null && _client.Npcs.Npcs.Any(n => n.Role == page || n.Name == page))
            {
                ShowEncounter(ScreenStack.IsEncounter(meeting) ? meeting : GameScreen.Shop);
                return;
            }
            int index = Array.IndexOf(PageNames, page);
            if (index < 0) throw new ArgumentException($"no bag page '{page}'");
            var chosen = (Page)index;
            if (InBag(chosen)) _bagPage = chosen;
            _screens.Show(ScreenOf(chosen));
        }

        // Where the open screen is drawn: the bag beside the minimap column (or under it), a growth screen or the journal
        // over the whole screen.
        internal Rect Area => ScreenStack.AreaOf(_screens.Current);

        // The bag's page by its tab name, for the playtests.
        internal string BagPage => PageNames[(int)_bagPage];

        public void Draw()
        {
            if (!Open) return;
            _typing = GUI.GetNameOfFocusedControl() == PetNameControl || WebText.IsTyping(PetNameControl);
            // Only what the pointer is over in this frame. IMGUI's own GUI.tooltip fell back to the last description
            // the pointer had been over, even when set empty, so an iron sword's text picked up on [장비] stood beside
            // the skills page (the 2026-11-09 web check). The window keeps its own.
            if (Event.current.type == EventType.Repaint)
            {
                _hover = "";
                PointsAtSoil = false;
                PointsAtSize = 1;
            }
            if (_selectedRune != null && _client.Bag.CountOf(_selectedRune) == 0) _selectedRune = null;

            var screen = _screens.Current;
            if (ScreenStack.IsEncounter(screen))
            {
                DrawEncounter(screen);
                return;
            }
            // The growth screens are laid out on their own (U4, InventoryWindow.Growth.cs).
            if (ScreenStack.GroupOf(screen) == ScreenGroup.Growth)
            {
                DrawGrowth(screen);
                return;
            }
            // The journal is a parchment book of its own (U5, InventoryWindow.Journal.cs).
            if (screen == GameScreen.Journal)
            {
                DrawJournalScreen();
                return;
            }
            DrawBag();
        }

        // Where the bag goes: left of the minimap column, full height; on narrow screens under the column; under the HUD
        // where the two would overlap (while the bag is open the HUD is only its bars). The page scrolls when it does not
        // fit.
        internal static Rect AreaOf()
        {
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
            // On a low screen (a phone held sideways) the HUD box is not drawn while the bag is open (4-5 가로 화면), so the
            // bag keeps the whole height; elsewhere it goes under the box's bars.
            var hud = Ui.HudArea;
            if (!Ui.Short && area.x < hud.xMax + 10 && area.y < hud.yMax + 10) area = Rect.MinMaxRect(area.x, hud.yMax + 10, area.xMax, area.yMax);
            return area;
        }

        static UiKind KindOf(Page page) => page switch
        {
            Page.Classes or Page.Passives or Page.Character => UiKind.Growth,
            Page.Journal or Page.Map => UiKind.Journal,
            Page.Merchant => UiKind.Shop,
            Page.Chest => UiKind.Machine,
            _ => UiKind.Bag,
        };

        // The small picture of each page, on its tab and in the head (in Page's order).
        static readonly string[][] TabIcons = { UiIcons.Bag, UiIcons.Hammer, UiIcons.Skills, UiIcons.Tree, UiIcons.Chest, UiIcons.Shop, UiIcons.Person, UiIcons.Book, UiIcons.Map };

        static GUIStyle _coinStyle;

        // The coins in the bag at the right of the head, a moment gold and larger when they change; gives where the
        // coins start.
        float DrawCoins(Rect head, float right) => DrawCoins(head, right, KindOf(_page));

        float DrawCoins(Rect head, float right, UiKind kind)
        {
            var coin = _client.Bag.Materials.Keys.FirstOrDefault(m => m.Id == World.Coin);
            int coins = coin == null ? 0 : _client.Bag.CountOf(coin);
            _coinStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_coinStyle, 12, bold: true);
            var ink = UiArt.HeadInk(kind);
            _coinStyle.normal.textColor = new Color(ink.R / 255f, ink.G / 255f, ink.B / 255f);
            string text = coins.ToString("N0");
            float width = _coinStyle.CalcSize(new GUIContent(text)).x;
            float band = UiArt.HeadBand * UiSkin.Pt;
            float middle = kind == UiKind.Shop ? head.y + 10.5f * UiSkin.Pt : head.y + band / 2;
            var line = new Rect(right - width, middle - 9, width, 18);
            var icon = UiSkin.IconSize(UiIcons.Coin);
            // Only where it does not crowd the title.
            if (head.width < 300) return right;
            UiSkin.Count(line, "coins", coins, text, _coinStyle);
            UiSkin.Icon(new Rect(line.x - icon.x - 3, middle - icon.y / 2, icon.x, icon.y), UiIcons.Coin, new Color(0.95f, 0.75f, 0.25f));
            return line.x - icon.x - 6;
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

        // The item (by id) or consumable picked in the grid; nothing picked when both are empty.
        int _picked;
        ConsumableDefinition _pickedConsumable;

        // Pressing a square always picks it (pressing it again keeps it picked), so a press never hides the buttons.
        void Pick(int item, ConsumableDefinition consumable)
        {
            _picked = item;
            _pickedConsumable = consumable;
            _pickedMaterial = null;
            Sounds.Play(Sound.Click);
        }

        // What is picked, as the bag's card (U6, InventoryWindow.Bag.cs), in the flow of a page (the character screen).
        void DrawPicked()
        {
            // Only worn gear is picked there; a material or potion picked in the bag stays the bag's.
            if (_picked == 0) return;
            var card = CardOf();
            if (card == null) return;
            GUILayout.Space(4);
            var rect = GUILayoutUtility.GetRect(_textWidth, CardHeight(card, _textWidth), GUILayout.Width(_textWidth));
            DrawCard(card, rect);
        }

        // What wearing the item instead of the other would change, stat by stat ("피해 +12", "방어도 -3"), and whether
        // each is a gain.
        static List<(string Text, bool Up)> Compare(Item item, Item other)
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
                .Select(pair => (StatText.Describe(new Modifier(pair.Key.Item1, pair.Key.Item2, pair.Value, pair.Key.Item3)), pair.Value > 0))
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

        static readonly Color EpicEdge = Palette.Of(Rarity.Epic), LegendEdge = Palette.Of(Rarity.Legendary);

        static bool Cell(Rect area, Sprite icon, string name, string corner, Color edge, bool picked, string tooltip, bool dim = false)
        {
            Ui.Probe(name, area);
            var e = Event.current;
            bool hover = area.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            bool down = hover && Ui.Held && area.Contains(Ui.DownAt);
            bool pressed = GUI.Button(area, GUIContent.none, GUIStyle.none);
            if (pressed) UiMotion.Confirm();
            Hover(area, tooltip);
            // A well with a rim of the thing's colour (Docs/UiRework.md 4-4 희귀도 테두리), epic and legendary breathing.
            UiSkin.SlotFrame(area, edge, glow: edge == EpicEdge || edge == LegendEdge);
            var previous = GUI.color;
            if (picked || hover)
            {
                GUI.color = picked ? new Color(1f, 0.9f, 0.6f, previous.a) : new Color(1, 1, 1, 0.7f * previous.a);
                Frame(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), 2);
            }
            GUI.color = dim ? new Color(1, 1, 1, 0.5f * previous.a) : new Color(1, 1, 1, previous.a);
            float inset = Mathf.Round(area.width * 0.18f);
            var face = UiMotion.Face(area, hover, down);
            Sprites.DrawInGui(new Rect(face.x + inset, face.y + inset, face.width - inset * 2, face.height - inset * 2), icon);
            GUI.color = previous;
            if (!string.IsNullOrEmpty(corner))
            {
                _countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                Ui.Text(_countStyle, 12, bold: true);
                var box = new Rect(area.x + 2, area.y + 2, area.width - 5, area.height - 4);
                GUI.color = new Color(0, 0, 0, 0.9f * previous.a);
                GUI.Label(new Rect(box.x + 1, box.y + 1, box.width, box.height), corner, _countStyle);
                GUI.color = previous;
                // A count that changes pops (Docs/UiRework.md 4-6 숫자).
                if (int.TryParse(corner, out int number))
                {
                    var size = _countStyle.CalcSize(new GUIContent(corner));
                    int cut = name.IndexOf(" ×", StringComparison.Ordinal);
                    UiSkin.Count(new Rect(box.xMax - size.x, box.yMax - size.y, size.x, size.y), "cell:" + (cut > 0 ? name.Substring(0, cut) : name), number, corner, _countStyle);
                }
                else
                {
                    GUI.Label(box, corner, _countStyle);
                }
            }
            return pressed;
        }

        // A worn slot with nothing in it: its name, faint, in an empty well.
        static void EmptyCell(Rect area, string slot)
        {
            _slotStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0), wordWrap = true };
            Ui.Text(_slotStyle, 12);
            UiSkin.SlotFrame(area, null);
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.35f * previous.a);
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
            TileType.Assembler or TileType.BigAssembler => machine.Recipe != null && machine.Recipe.Ingredients.Any(i => i.Material == material.Kind),
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
            // Nothing while nothing is hovered: the card that stood beside the bag saying how to see one is gone (U6).
            if (!hovering) return;
            string text = _hover;
            float room = Mathf.Min(TooltipWidth, Ui.Width - 20);
            // As wide as the longest line needs, so short descriptions get a small card.
            var size = Ui.CardSize(text, room, out string wrapped);
            float width = size.x, height = size.y;
            float x;
            float y;
            if (besideWindow)
            {
                x = window.x - width - 10;
                y = pointer.y - 12;
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
            // A floating card over everything, drawn last (Ui.LateCard): the minimap and touch buttons come after the bag.
            Ui.LateCard(new Rect(x, y, width, height), wrapped);
        }

        // Materials, counted by kind. One that builds gets [놓기], a seed gets [심기] and the watering can [물 주기];
        // all act on the tile the player faces.

        // What the player's goals still ask for (WorldClient.NeededMaterials), worked out once a frame.
        Dictionary<MaterialDefinition, (string For, int Count)> _needed = new Dictionary<MaterialDefinition, (string For, int Count)>();
        int _neededFrame = -1;

        Dictionary<MaterialDefinition, (string For, int Count)> Needed()
        {
            if (_neededFrame != Time.frameCount)
            {
                _neededFrame = Time.frameCount;
                _needed = _client.NeededMaterials();
            }
            return _needed;
        }

        // "아직 필요: 상인 도라 3/5" for a material a goal asks for, or null.
        string NeededText(MaterialDefinition material) =>
            Needed().TryGetValue(material.Kind, out var need) ? $"아직 필요: {need.For} {Math.Min(_client.Bag.CountOfKind(material), need.Count)}/{need.Count}" : null;

        // Whether [상자에 넣기] takes it: a material a chest nearby already holds, unless it is a tool, in a quick slot,
        // locked, or wanted for a goal (Docs/Crafting.md 근처 상자에 넣기, 2026-11-11).
        bool Stashable(MaterialDefinition material) =>
            !IsTool(material) && !(_hotbar?.Keeps(material) ?? false) && !Needed().ContainsKey(material.Kind)
            && material.AllQualities.Sum(_client.StockOf) > 0;

        // One press puts every such material into the chests nearby that hold it already (World.StashMaterial).
        void DrawStash()
        {
            var stash = _client.Bag.Materials.Keys.Where(Stashable).ToList();
            if (stash.Count == 0) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit($"근처 상자에 있는 재료 {stash.Count}가지 (잠근 것·퀵슬롯·도구·목표에 필요한 것은 남는다)", _textWidth - 104), GUILayout.Width(_textWidth - 104));
            if (Ui.LayoutButton("상자에 넣기", GUILayout.Width(96)))
            {
                Sounds.Play(Sound.Place);
                foreach (var material in stash) _send(_client.StashCommand(material));
            }
            GUILayout.EndHorizontal();
        }

        void DrawMaterials()
        {
            // What can be done here and now (U6): the machine ahead, then a row for each material that is put down, planted,
            // used or put in it. Every material is in the [장비] tab's grid ([재료] chip); a cracked wall or a door ahead
            // has its own button over it in the world (GameRoot.DrawDoorButtons).
            DrawStash();
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
                    GUILayout.Label(Fit($"닭 {machine.Animals}/{World.CoopChickens}마리. 사료가 있으면 20분마다 닭마다 달걀 하나", _textWidth));
                if (machine.Kind == TileType.Barn)
                    GUILayout.Label(Fit($"소 {machine.Animals}/{World.HouseOf(TileType.Barn).Most}마리. 사료 둘이면 20분마다 소마다 우유 하나", _textWidth));
                if (machine.Kind == TileType.Fold)
                    GUILayout.Label(Fit($"양 {machine.Animals}/{World.HouseOf(TileType.Fold).Most}마리. 사료가 있으면 40분마다 양마다 양털 하나", _textWidth));
                if (World.HouseOf(machine.Kind) != null && machine.Animals > 0)
                {
                    // Grazing (World.Grazes): a closed pen in front, four tiles an animal.
                    var step = Machine.Steps[machine.Direction & 3];
                    bool grazing = _client.Map != null && World.Grazes(_client.Map, machine.X + step.X, machine.Y + step.Y, machine.Animals);
                    GUILayout.Label(Fit(grazing
                        ? $"방목 중: 사료가 없어도 {World.GrazingSlowdown:0.#}배 걸려 준다. 사료가 넉넉하면 원래 속도"
                        : $"앞을 울타리로 둘러 마리마다 {World.PenTilesPerAnimal}칸을 주면 방목한다(사료 없이도 준다)", _textWidth));
                    // Affection (World.PetAnimals): once a day, and fonder animals give silver and gold more often.
                    GUILayout.BeginHorizontal();
                    string hearts = new string('■', machine.Affection) + new string('□', World.AffectionMost - machine.Affection);
                    GUILayout.Label(Fit($"애정 {hearts} {machine.Affection}/{World.AffectionMost} (높으면 은빛·금빛)", _textWidth - 104), GUILayout.Width(_textWidth - 104));
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
                string wanted = NeededText(pair.Key);
                TipLabel(new GUIContent(Fit($"{pair.Key.Name} ×{pair.Value}{(wanted != null ? " · 필요" : "")}", _nameWidth), wanted != null ? $"{pair.Key.Description}\n{wanted}" : pair.Key.Description),
                    GUILayout.Width(_nameWidth));
                if (pair.Key.Places != null)
                {
                    if (Ui.LayoutButton("놓기", GUILayout.Width(56))) Send(_client.PlaceCommand(pair.Key), Sound.Place);
                    if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                        PointsAtSize = World.SizeOf(pair.Key.Places.Value);
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
            if (_client.Bag.Materials.Count == 0) GUILayout.Label(Fit("재료가 아직 없다. 벽을 캐거나 몬스터를 잡으면 모인다.", _textWidth));
            else if (plain.Count == _client.Bag.Materials.Count) GUILayout.Label(Fit("놓거나 심거나 쓸 재료가 없다.", _textWidth));
            if (plain.Count == 0) return;
            // The rest are only kept: they are in the [장비] tab's grid under [재료], where a picked one can be locked.
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit($"다른 재료 {plain.Count}가지는 [장비] 탭의 [재료]에서 본다", _textWidth - 104), GUILayout.Width(_textWidth - 104));
            if (Ui.LayoutButton("재료 보기", GUILayout.Width(96)))
            {
                _bagPage = Page.Gear;
                _filter = BagFilter.Materials;
                _scroll[(int)Page.Gear] = Vector2.zero;
            }
            GUILayout.EndHorizontal();
        }

        MaterialDefinition _pickedMaterial;

        // The shop's tabs (InventoryWindow.Encounters.cs).
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

        // A chest's or the bag's things as rows, each with the button that moves it to the other side ([꺼내기], [넣기]).
        // Stacks move whole.
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

        // What is made by hand (U6: the bag keeps only these; a station's recipes are in its window, U3), with what each
        // takes against what the bag and the chests nearby hold; [만들기] only works with everything at hand, [×N] makes it
        // up to five times over. Facing an assembler, what it can be set to make ([조립기에]). By a workbench or furnace a
        // button opens its window; elsewhere a line says where the rest are made.
        void DrawCrafting()
        {
            var recipes = _client.Recipes;
            if (recipes.Count == 0) return;
            GUILayout.Space(6);
            var enabled = GUI.enabled;
            // What is being made (World.StartCrafting, 2026-11-09): a bar, how many more, and [그만].
            if (_client.Crafting != null)
            {
                GUILayout.BeginHorizontal();
                string more = _client.CraftLeft > 1 ? $" · 남은 {_client.CraftLeft}개" : "";
                GUILayout.Label(Fit($"만드는 중: {_client.Crafting.Name}{more} (작업대 곁을 떠나면 멈춘다)", _textWidth - 64), GUILayout.Width(_textWidth - 64));
                if (Ui.LayoutButton("그만", GUILayout.Width(56))) Send(_client.StopCraftCommand(), Sound.Click);
                GUILayout.EndHorizontal();
                var bar = GUILayoutUtility.GetRect(_textWidth, 10);
                if (Event.current.type == EventType.Repaint) UiSkin.Bar(bar, _client.CraftProgress, "");
                GUILayout.Space(4);
            }
            // Facing an assembler: what it makes, [멈추기], and each recipe it can make with [조립기에] (World.SetRecipe).
            var assembler = _client.MachineAhead() is MachineView m && World.IsAssembler(m.Kind) ? m : (MachineView?)null;
            if (assembler != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"조립기: {assembler.Value.Recipe?.Name ?? "정하지 않음"} (아래의 [조립기에]로 정한다)", _textWidth - 64));
                if (assembler.Value.Recipe != null && Ui.LayoutButton("멈추기", GUILayout.Width(56))) Send(_client.SetRecipeCommand(null), Sound.Click);
                GUILayout.EndHorizontal();
                foreach (var recipe in recipes.Where(r => !_client.MadeByMachine(r) && World.Assembles(r)))
                {
                    string makes = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {i.Count}"));
                    GUILayout.BeginHorizontal();
                    Icon(RecipeIcon(recipe));
                    TipLabel(new GUIContent(Fit($"{RecipeName(recipe)}\n{makes}", _recipeWidth + 44), RecipeTooltip(recipe)), GUILayout.Width(_recipeWidth + 44));
                    if (Ui.LayoutButton("조립기에", GUILayout.Width(72))) Send(_client.SetRecipeCommand(recipe), Sound.Click);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(6);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Fit("손 제작", _textWidth - 160), GUILayout.Width(_textWidth - 160));
            if (Ui.LayoutButton(_craftableOnly ? "모두 보기" : "만들 수 있는 것만", GUILayout.Width(156))) _craftableOnly = !_craftableOnly;
            GUILayout.EndHorizontal();
            bool any = false;
            foreach (var recipe in recipes.Where(r => !_client.MadeByMachine(r) && r.Station == null))
            {
                int times = _client.CraftableTimes(recipe);
                if (_craftableOnly && times == 0) continue;
                any = true;
                string needs = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {_client.Available(i.Material)}/{i.Count}"));
                // A thing that takes time by hand says how long (CraftRules.Seconds).
                float seconds = _client.CraftSeconds(recipe);
                if (seconds > 0) needs += $" · {seconds:0.#}초";
                GUILayout.BeginHorizontal();
                Icon(RecipeIcon(recipe));
                TipLabel(new GUIContent(Fit($"{RecipeName(recipe)}\n{needs}", _recipeWidth), RecipeTooltip(recipe)), GUILayout.Width(_recipeWidth));
                GUI.enabled = enabled && times > 0;
                // One that takes time sounds when it is done (SimEventType.Crafted), not when pressed.
                var pressed = seconds > 0 ? Sound.Click : Sound.Craft;
                if (Ui.LayoutButton("만들기", Ui.Look.Primary, GUILayout.Width(64))) Send(_client.CraftCommand(recipe), pressed);
                GUI.enabled = enabled && times > 1;
                int bulk = Mathf.Clamp(times, 2, BulkCraft);
                if (Ui.LayoutButton($"×{bulk}", GUILayout.Width(40))) Send(_client.CraftCommand(recipe, bulk), pressed);
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label(Fit(_craftableOnly ? "지금 가진 재료로 손으로 만들 수 있는 것이 없다" : "손으로 만들 것이 없다", _textWidth));
            // The rest are made at a station, in its window (U3).
            GUILayout.Space(4);
            var near = StationsNear();
            if (near.Count > 0)
            {
                string names = string.Join("·", near.Select(StationName));
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"곁의 {names}에서 만드는 것은 {names} 창에서 만든다", _textWidth - 120), GUILayout.Width(_textWidth - 120));
                if (Ui.LayoutButton($"{StationName(near[0])} 열기", GUILayout.Width(112))) ShowEncounter(GameScreen.Station);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(Fit("작업대·화로·모루에서 만드는 것은 그 곁의 [작업대 열기]·[화로 열기]·[모루 열기]로 연 창에서 만든다", _textWidth));
            }
        }

        static string RecipeName(Recipe recipe) => recipe.Count > 1 ? $"{recipe.Name} ×{recipe.Count}" : recipe.Name;

        static Sprite RecipeIcon(Recipe recipe) =>
            recipe.Item != null ? Sprites.Icon(recipe.Item) : recipe.Consumable != null ? Sprites.Icon(recipe.Consumable) : Sprites.Icon(recipe.Material);

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

        // Beside an anvil: the anvil's window on this piece (U3), or in that window, this piece to work on.
        void RefineButton(Item item)
        {
            if (!_client.NearAnvil) return;
            if (!Ui.LayoutButton("다듬기", GUILayout.Width(56))) return;
            _refining = item.Id;
            if (_screens.Current != GameScreen.Anvil) ShowEncounter(GameScreen.Anvil);
        }

        static string CategoryName(SlotCategory category) => category switch
        {
            SlotCategory.Weapon => "무기",
            SlotCategory.Armor => "방어구",
            _ => "장신구",
        };
    }
}
