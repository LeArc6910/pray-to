using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bag (Tab or I) as a bag again (Docs/UiRework.md 4-1 가방, U6): the leather head with the coins; the tabs [장비]
    // and [재료·제작] (a rail at the left on a low screen, 4-5 가로 화면); on [장비] the worn things round the hero (a row
    // of six on a low screen), the kind chips (전체, 장비, 재료, 먹을것, 놓을것) with [정렬], every carried thing in one
    // grid with its empty wells, and the picked thing's card fixed at the bottom: its buttons first (so a low screen
    // shows them), the quick slots to put it in, then its words and how it compares with what is worn. [재료·제작] keeps
    // what is done with materials where the player stands: the machine ahead, the rows that put a thing down, plant it or
    // use it, the hand-made recipes; a station's recipes are in its own window (U3).
    sealed partial class InventoryWindow
    {
        // The kinds the grid shows (the chips), in their order.
        internal enum BagFilter
        {
            All,
            Gear,
            Materials,
            Food,
            Place,
        }

        internal static readonly string[] FilterNames = { "전체", "장비", "재료", "먹을것", "놓을것" };
        BagFilter _filter;

        // The chip picked, for the playtests (state "bagFilter").
        internal string FilterName => FilterNames[(int)_filter];

        // A chip by its name, for the playtests ("bag filter 재료"); false for none.
        internal bool SetFilter(string name)
        {
            int index = Array.IndexOf(FilterNames, name);
            if (index < 0) return false;
            _filter = (BagFilter)index;
            return true;
        }

        // What the card shows, for the playtests: the picked thing's title, or "".
        internal string CardTitle { get; private set; } = "";

        static readonly Page[] BagPages = { Page.Gear, Page.Materials };
        bool _compactDoll;

        // The tabs' rail on a low screen, the chips' and the card's buttons' height (a finger's 40 points on a touch screen).
        const float RailWidth = 92;
        static float ChipHeight => Controls.UsingTouch ? 40 : 30;
        static float ActionHeight => Controls.UsingTouch ? 40 : 28;
        const float SortWidth = 60;
        // The list keeps at least this much under the chips, whatever the card takes.
        const float MinList = 110;
        const float CardPad = 8;
        const float CardHead = 28;

        // Where the bag's parts go inside its window (also for the layout tests): the tabs (a row, or a rail on a low
        // screen), the chips and [정렬] (on [장비]), the scrolling list, and the card at the bottom as tall as asked, but
        // never so tall that the list has less than MinList.
        internal struct BagParts
        {
            public bool Rail;
            public Rect[] Tabs;
            public Rect Content;
            public Rect[] Chips;
            public Rect Sort;
            public Rect List;
            public Rect Card;
        }

        internal static BagParts BagLayout(Rect inside, float card, bool chips)
        {
            var parts = new BagParts { Rail = Ui.Short };
            Rect content;
            if (parts.Rail)
            {
                parts.Tabs = new[] { new Rect(inside.x, inside.y, RailWidth, TabHeight), new Rect(inside.x, inside.y + TabHeight + 4, RailWidth, TabHeight) };
                content = Rect.MinMaxRect(inside.x + RailWidth + 6, inside.y, inside.xMax, inside.yMax);
            }
            else
            {
                float width = (inside.width - 4) / 2;
                parts.Tabs = new[] { new Rect(inside.x, inside.y, width, TabHeight), new Rect(inside.x + width + 4, inside.y, width, TabHeight) };
                content = Rect.MinMaxRect(inside.x, inside.y + TabHeight + 6, inside.xMax, inside.yMax);
            }
            parts.Content = content;
            float top = content.y;
            parts.Chips = Array.Empty<Rect>();
            if (chips)
            {
                parts.Sort = new Rect(content.xMax - SortWidth, top, SortWidth, ChipHeight);
                float each = (content.width - SortWidth - 4 * FilterNames.Length) / FilterNames.Length;
                parts.Chips = Enumerable.Range(0, FilterNames.Length).Select(i => new Rect(content.x + i * (each + 4), top, each, ChipHeight)).ToArray();
                top += ChipHeight + 6;
            }
            // On a low screen wide enough the card stands at the right, as tall as the list, so the grid keeps the height
            // (a phone held sideways had about one row of squares over a card under it).
            if (SideCard(content) && card > 0)
            {
                parts.Card = new Rect(content.xMax - SideCardWidth, top, SideCardWidth, content.yMax - top);
                parts.List = Rect.MinMaxRect(content.x, top, parts.Card.x - 6, content.yMax);
                return parts;
            }
            float height = Mathf.Min(card, Mathf.Max(0, content.yMax - top - MinList - 6));
            parts.Card = new Rect(content.x, content.yMax - height, content.width, height);
            parts.List = Rect.MinMaxRect(content.x, top, content.xMax, height > 0 ? parts.Card.y - 6 : content.yMax);
            return parts;
        }

        const float SideCardWidth = 220;

        static bool SideCard(Rect content) => Ui.Short && content.width >= 2 * SideCardWidth - 40;

        // How wide the card is drawn in this content: the whole width under the list, or the side card's.
        static float CardWidth(Rect content) => SideCard(content) ? SideCardWidth : content.width;

        void DrawBag()
        {
            const GameScreen screen = GameScreen.Bag;
            if (_bagPage != Page.Gear && _bagPage != Page.Materials) _bagPage = Page.Gear;
            _page = _bagPage;
            var area = Area;
            Controls.Block(area);
            UiMotion.BeginWindow(ScreenStack.MotionKey(screen), area, UiKind.Bag);
            var inside = UiSkin.Window(area, UiKind.Bag, "가방" + ScreenStack.KeyHint(screen), UiIcons.Bag, out var head);
            DrawCoins(head, UiSkin.CloseRect(head).x - 6, UiKind.Bag);
            if (UiSkin.CloseButton(head))
            {
                _screens.Close();
                UiMotion.EndWindow();
                return;
            }
            // A quick slot's word ("퀵슬롯이 가득 찼다") over the rest, for its moment.
            if (_hotbar != null && _hotbar.ShowsMessage)
            {
                var line = new Rect(inside.x, inside.y, inside.width, 20);
                GUI.Label(line, Ui.Wrap(_hotbar.Message, GUI.skin.label, inside.width));
                inside = Rect.MinMaxRect(inside.x, line.yMax + 2, inside.xMax, inside.yMax);
            }
            bool gear = _page == Page.Gear;
            var card = gear ? CardOf() : null;
            CardTitle = card?.Title ?? "";
            var parts = BagLayout(inside, 0, gear);
            if (card != null) parts = BagLayout(inside, CardHeight(card, CardWidth(parts.Content)), gear);

            var chosen = TabsAt(_page, BagPages, parts.Tabs);
            if (chosen != _page)
            {
                _bagPage = chosen;
                _page = chosen;
            }
            if (gear)
            {
                Ui.Row("분류");
                for (int i = 0; i < FilterNames.Length; i++)
                {
                    bool on = (int)_filter == i;
                    if (Ui.Button(parts.Chips[i], FilterNames[i], "분류: " + FilterNames[i], on ? Ui.Look.Primary : Ui.Look.Normal) && !on)
                    {
                        _filter = (BagFilter)i;
                        _scroll[(int)Page.Gear] = Vector2.zero;
                    }
                }
                // Gear in slot order, the best first (Inventory.Sort).
                GUI.enabled = _client.Bag.Items.Count > 1;
                if (Ui.Button(parts.Sort, "정렬")) Send(_client.SortBagCommand(), Sound.Click);
                GUI.enabled = true;
                Ui.Row("");
            }

            var rows = parts.List;
            // The hero's doll wants about 170 points: where the list is lower (a low screen, a small PC window with a card
            // up), the six worn slots go in a row instead.
            _compactDoll = Ui.Short || rows.height < 330;
            float scrollbar = GUI.skin.verticalScrollbar.fixedWidth + 8;
            _textWidth = rows.width - scrollbar - GUI.skin.label.margin.horizontal;
            // Room for two buttons and [퀵] beside each name on [재료·제작].
            _nameWidth = Mathf.Clamp(_textWidth - IconRoom - 150 - QuickWidth, 60, 240);
            _recipeWidth = _textWidth - IconRoom - 120;
            int page = (int)_page;
            Wheel(rows);
            Ui.TouchScroll(rows, ref _scroll[page]);
            GUILayout.BeginArea(rows);
            _scroll[page] = Ui.BeginScrollView(_scroll[page], GUILayout.ExpandHeight(true));
            if (gear) DrawBagGrid();
            else DrawMaterialsPage();
            Ui.EndScrollView();
            GUILayout.EndArea();
            _rows = rows;
            if (card != null && parts.Card.height > 0) DrawCard(card, parts.Card);
            UiMotion.EndWindow();
            DrawTooltip(area, area.x >= TooltipWidth + 20);
        }

        // Tabs in the rects given (a row, or a rail), switching as soon as one is pressed. IMGUI's toolbar switches on
        // release, and in the browser its first click after the bag opened was sometimes lost (LOCAL_TASKS.md, the
        // 2026-10-31 UI check).
        Page TabsAt(Page page, IReadOnlyList<Page> pages, IReadOnlyList<Rect> rects)
        {
            var e = Event.current;
            for (int i = 0; i < pages.Count && i < rects.Count; i++)
            {
                var tab = rects[i];
                if (e.type == EventType.MouseDown && tab.Contains(e.mousePosition))
                {
                    if (page != pages[i])
                    {
                        page = pages[i];
                        UiMotion.Confirm();
                    }
                    e.Use();
                }
                var label = PageLabels[(int)pages[i]];
                Ui.Probe(label.text, tab);
                bool hover = tab.Contains(e.mousePosition);
                if (e.type == EventType.Repaint) UiSkin.Tab(tab, label, TabIcons[(int)pages[i]], page == pages[i], hover);
            }
            return page;
        }

        // ---- [장비]: the hero and everything carried ----

        void DrawBagGrid()
        {
            // By an anvil, the anvil's window works the gear (U3); a picked thing's [다듬기] opens it on that piece.
            if (_client.NearAnvil)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit("모루 곁: 장비를 고르고 [다듬기], 또는 모루 창에서 강화·분해·룬 빼기·접사 다시", _textWidth - 104), GUILayout.Width(_textWidth - 104));
                if (Ui.LayoutButton("모루 열기", GUILayout.Width(96))) ShowEncounter(GameScreen.Anvil);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            float cell = Controls.UsingTouch ? 48 : 44;
            // The hero among the six worn slots; on a low screen the six in a row, so the grid stays in sight.
            if (_compactDoll)
            {
                GUILayout.Label(Fit("착용", _textWidth));
                var slots = (ItemSlot[])Enum.GetValues(typeof(ItemSlot));
                var worn = Grid(slots.Length, cell);
                for (int i = 0; i < slots.Length; i++)
                {
                    var item = _client.Equipment[slots[i]];
                    var area = CellAt(worn, i, cell);
                    if (item == null) EmptyCell(area, ItemText.SlotName(slots[i]));
                    else if (Cell(area, Sprites.Icon(item.Base), ItemText.Name(item), item.Upgrade > 0 ? $"+{item.Upgrade}" : null, Palette.Of(item.Rarity), _picked == item.Id, ItemTooltip(item)))
                        Pick(item.Id, null);
                }
            }
            else
            {
                DrawDoll("착용");
            }

            GUILayout.Space(4);
            var items = _client.Bag.Items.ToList();
            float wait = _client.ConsumableCooldownLeft;
            GUILayout.Label(Fit($"소지품 {items.Count}/{Inventory.Capacity}" + (wait > 0 ? $" · 소모품은 {Mathf.CeilToInt(wait)}초 뒤에" : ""), _textWidth));
            var cells = new List<Func<Rect, bool>>();
            bool Shows(BagFilter kind) => _filter == BagFilter.All || _filter == kind;
            if (Shows(BagFilter.Gear))
            {
                foreach (var item in items)
                {
                    bool wearable = item.Base.RequiredLevel <= _client.Level;
                    cells.Add(area =>
                    {
                        if (Cell(area, Sprites.Icon(item.Base), ItemText.Name(item), wearable ? null : $"Lv{item.Base.RequiredLevel}", Palette.Of(item.Rarity), _picked == item.Id,
                            ItemTooltip(item), dim: !wearable))
                            Pick(item.Id, null);
                        return true;
                    });
                }
                // The rest of the bag's room, as empty wells (4-1: 빈 칸 그리기), only while gear alone is shown.
                if (_filter == BagFilter.Gear)
                    for (int i = items.Count; i < Inventory.Capacity; i++) cells.Add(area => Empty(area));
            }
            if (Shows(BagFilter.Food))
            {
                foreach (var (consumable, number) in _client.Bag.Consumables.ToList())
                {
                    cells.Add(area =>
                    {
                        if (Cell(area, Sprites.Icon(consumable), $"{consumable.Name} ×{number}", number.ToString(), Palette.Potion, _pickedConsumable == consumable, consumable.Description))
                            Pick(0, consumable);
                        return true;
                    });
                }
            }
            if (Shows(BagFilter.Gear))
            {
                foreach (var (rune, number) in _client.Bag.Runes.ToList())
                {
                    var tooltip = RuneText(rune);
                    cells.Add(area =>
                    {
                        if (Cell(area, Sprites.Icon(rune), $"{rune.Name} ×{number}", number.ToString(), Palette.Rune, _selectedRune == rune, $"{rune.Name}\n{tooltip}"))
                        {
                            _selectedRune = _selectedRune == rune ? null : rune;
                            Sounds.Play(Sound.Click);
                        }
                        return true;
                    });
                }
            }
            foreach (var (material, count) in _client.Bag.Materials.ToList())
            {
                if (!Shows(KindOf(material))) continue;
                string wanted = NeededText(material);
                bool locked = _hotbar?.IsLocked(material) ?? false;
                cells.Add(area =>
                {
                    // A goal's material carries the badge (as a tab with points to spend does); a locked one says so.
                    if (Cell(area, Sprites.Icon(material), $"{material.Name} ×{count}{(wanted != null ? " · 필요" : "")}{(locked ? " · 잠금" : "")}",
                        locked ? $"잠{count}" : count.ToString(), Palette.Material, _pickedMaterial == material,
                        $"{material.Name} ×{count}\n{material.Description}{(wanted != null ? "\n" + wanted : "")}{(locked ? "\n잠금: [상자에 넣기]가 두고 간다" : "")}"))
                        PickMaterial(material);
                    if (wanted != null && Event.current.type == EventType.Repaint) Badge(area);
                    return true;
                });
            }
            if (cells.Count == 0)
            {
                GUILayout.Label(Fit(_filter == BagFilter.All ? "가방이 비었다" : $"{FilterNames[(int)_filter]}: 가진 것이 없다", _textWidth));
                return;
            }
            // The last row filled out with empty wells, so the grid reads as a bag's squares.
            int columns = Columns(cell);
            int pad = (columns - cells.Count % columns) % columns;
            for (int i = 0; i < pad; i++) cells.Add(area => Empty(area));
            var grid = Grid(cells.Count, cell);
            for (int i = 0; i < cells.Count; i++) cells[i](CellAt(grid, i, cell));

            static bool Empty(Rect area)
            {
                if (Event.current.type == EventType.Repaint) UiSkin.SlotFrame(area, null);
                return false;
            }
        }

        // Which chip a material goes under: what is put down, planted or used on the ground (놓을것), else 재료.
        BagFilter KindOf(MaterialDefinition material) =>
            material.Places != null || _client.CropFrom(material) != null || IsTool(material) || Pets.ByEgg(material.Id) != null ? BagFilter.Place : BagFilter.Materials;

        void PickMaterial(MaterialDefinition material)
        {
            _picked = 0;
            _pickedConsumable = null;
            _pickedMaterial = material;
            Sounds.Play(Sound.Click);
        }

        static string RuneText(RuneDefinition rune) => string.Join("\n", new[] { SlotCategory.Weapon, SlotCategory.Armor, SlotCategory.Jewelry }
            .Select(c => $"{CategoryName(c)}: {string.Join(", ", rune.ModifiersFor(c).Select(StatText.Describe))}"));

        // ---- The picked thing's card ----

        struct CardAction
        {
            public string Label;
            public Ui.Look Look;
            public bool Enabled;
            public Action Do;
            // Works the ground ahead (the world's frame marks the tile), and the size of what it would put down.
            public bool Soil;
            public int Size;
        }

        sealed class BagCard
        {
            public Sprite Icon;
            public string Title;
            public Color TitleColor = Color.white;
            public readonly List<(string Text, Color Color)> Lines = new List<(string, Color)>();
            public readonly List<CardAction> Actions = new List<CardAction>();
            // Which quick slot it is in (-1 none), and how to put it in one; no quick slots when null.
            public int QuickAt = -1;
            public Action<int> Quick;
        }

        static readonly Color Better = new Color(0.55f, 0.92f, 0.5f), Worse = new Color(1f, 0.45f, 0.4f), Quiet = new Color(0.82f, 0.86f, 0.95f);

        void Add(BagCard card, string label, Action action, Ui.Look look = Ui.Look.Normal, bool enabled = true, bool soil = false, int size = 1) =>
            card.Actions.Add(new CardAction { Label = label, Do = action, Look = look, Enabled = enabled, Soil = soil, Size = size });

        // What is picked (an item, a consumable, a material, or a rune for socketing), as a card; null for nothing.
        BagCard CardOf()
        {
            var item = _picked == 0 ? null : _client.Equipment.Items.Concat(_client.Bag.Items).FirstOrDefault(i => i.Id == _picked);
            if (item == null) _picked = 0;
            if (_pickedConsumable != null && _client.Bag.CountOf(_pickedConsumable) == 0) _pickedConsumable = null;
            if (_pickedMaterial != null && _client.Bag.CountOf(_pickedMaterial) == 0) _pickedMaterial = null;
            BagCard card = null;
            if (item != null) card = ItemCard(item);
            else if (_pickedConsumable != null) card = ConsumableCard(_pickedConsumable);
            else if (_pickedMaterial != null) card = MaterialCard(_pickedMaterial);
            if (_selectedRune == null) return card;
            var rune = _selectedRune;
            if (card == null)
            {
                card = new BagCard { Icon = Sprites.Icon(rune), Title = $"{rune.Name} ×{_client.Bag.CountOf(rune)}", TitleColor = Palette.Rune };
                foreach (string line in RuneText(rune).Split('\n')) card.Lines.Add((line, Color.white));
            }
            // A rune picked for socketing says so on every card, with [그만].
            card.Lines.Insert(0, ($"{rune.Name}: 끼울 아이템을 고르고 [끼우기]. 넣은 룬은 모루에서 정수를 들여야 뺄 수 있다", UiSkin.Gold));
            Add(card, "그만", () => _selectedRune = null);
            return card;
        }

        BagCard ItemCard(Item item)
        {
            bool wearing = _client.Equipment[item.Base.Slot] == item;
            var card = new BagCard { Icon = Sprites.Icon(item.Base), Title = ItemText.Name(item) + (wearing ? " (착용 중)" : ""), TitleColor = Palette.Of(item.Rarity) };
            if (wearing)
            {
                Add(card, "벗기", () => Send(_client.UnequipCommand(item.Base.Slot), Sound.Click));
            }
            else
            {
                // Too high a level to wear yet: the button says which level instead.
                bool wearable = item.Base.RequiredLevel <= _client.Level;
                Add(card, wearable ? "장착" : $"Lv{item.Base.RequiredLevel}", () => Send(_client.EquipCommand(item.Id), Sound.Place), Ui.Look.Primary, wearable);
                Add(card, "버리기", () => Send(_client.DiscardCommand(item.Id), Sound.Click), Ui.Look.Danger);
            }
            if (_selectedRune != null && item.Sockets.Contains(null))
            {
                var rune = _selectedRune;
                Add(card, "끼우기", () => Send(_client.InsertRuneCommand(item.Id, rune), Sound.Craft));
            }
            if (_client.NearAnvil)
            {
                Add(card, "다듬기", () =>
                {
                    _refining = item.Id;
                    if (_screens.Current != GameScreen.Anvil) ShowEncounter(GameScreen.Anvil);
                });
            }
            if (_hotbar != null)
            {
                card.QuickAt = _hotbar.SlotOf(item);
                card.Quick = slot => _hotbar.Put(item, slot);
            }
            foreach (string line in ItemText.Describe(item).Skip(1)) card.Lines.Add((line.Trim(), Color.white));
            // Against what is worn in its slot: what putting it on would change, better in green, worse in red
            // (Docs/UiRework.md 4-4 비교는 기본으로 켠다).
            var worn = _client.Equipment[item.Base.Slot];
            if (!wearing && worn != null)
            {
                var change = Compare(item, worn);
                card.Lines.Add((change.Count == 0 ? $"지금 낀 {ItemText.Name(worn)}와 같다" : $"지금 낀 {ItemText.Name(worn)}보다:", Quiet));
                foreach (var (text, up) in change) card.Lines.Add(("  " + text, up ? Better : Worse));
            }
            return card;
        }

        BagCard ConsumableCard(ConsumableDefinition consumable)
        {
            var card = new BagCard { Icon = Sprites.Icon(consumable), Title = $"{consumable.Name} ×{_client.Bag.CountOf(consumable)}", TitleColor = Palette.Potion };
            // A potion is drunk; a food is eaten, and still gives its buff at full health.
            Add(card, consumable.IsFood ? "먹기" : consumable.Recalls ? "쓰기" : consumable.ReadsMap ? "읽기" : "마시기", () =>
            {
                byte[] command = _client.UseConsumableCommand(consumable);
                if (command != null) _send(command);
            }, Ui.Look.Primary);
            if (_hotbar != null)
            {
                card.QuickAt = _hotbar.SlotOf(consumable);
                card.Quick = slot => _hotbar.Put(consumable, slot);
            }
            card.Lines.Add((consumable.Description, Color.white));
            float wait = _client.ConsumableCooldownLeft;
            if (wait > 0) card.Lines.Add(($"소모품은 {Mathf.CeilToInt(wait)}초 뒤에 쓴다", Quiet));
            return card;
        }

        BagCard MaterialCard(MaterialDefinition material)
        {
            var card = new BagCard { Icon = Sprites.Icon(material), Title = $"{material.Name} ×{_client.Bag.CountOf(material)}", TitleColor = new Color(0.95f, 0.9f, 0.8f) };
            // The quick actions kept from the rows of [재료·제작]: put down, plant, water, fish, till, fertilize, hatch.
            if (material.Places != null)
                Add(card, "놓기", () => Send(_client.PlaceCommand(material), Sound.Place), Ui.Look.Primary, soil: TileMap.IsPaving(material.Places.Value), size: World.SizeOf(material.Places.Value));
            var crop = _client.CropFrom(material);
            if (crop != null) Add(card, "심기", () => Send(_client.PlantCommand(crop), Sound.Place), Ui.Look.Primary, soil: true);
            if (material.Id == World.WateringCan) Add(card, "물 주기", () => Send(_client.WaterCommand(), Sound.Water), Ui.Look.Primary, soil: true);
            if (material.Id == World.FishingRod) Add(card, FishingLabel(_client), () => Send(_client.FishCommand(), Sound.Cast), Ui.Look.Primary);
            if (material.Id == World.Hoe) Add(card, "밭 갈기", () => Send(_client.TillCommand(), Sound.Dig), Ui.Look.Primary, soil: true);
            if (material.Id == World.Fertilizer) Add(card, "거름 주기", () => Send(_client.FertilizeCommand(), Sound.Place), Ui.Look.Primary, soil: true);
            var hatches = Pets.ByEgg(material.Id);
            if (hatches != null) Add(card, "부화", () => Send(_client.HatchCommand(material), Sound.Learn), enabled: _client.Pet != hatches);
            if (_hotbar != null)
            {
                bool locked = _hotbar.IsLocked(material);
                Add(card, locked ? "잠금 풀기" : "잠그기", () =>
                {
                    _hotbar.ToggleLock(material);
                    Sounds.Play(Sound.Click);
                });
                // Only what a quick slot can use: things that build, seeds and the tools.
                if (material.Places != null || crop != null || IsTool(material))
                {
                    card.QuickAt = _hotbar.SlotOf(material);
                    card.Quick = slot => _hotbar.Put(material, slot);
                }
            }
            card.Lines.Add((material.Description, Color.white));
            string wanted = NeededText(material);
            if (wanted != null) card.Lines.Add((wanted, UiSkin.Gold));
            if (_hotbar != null) card.Lines.Add((_hotbar.IsLocked(material) ? "잠금: [상자에 넣기]가 가방에 남긴다" : "잠그면 [상자에 넣기]가 가방에 남긴다", Quiet));
            return card;
        }

        static GUIStyle _cardTitle, _cardLine;

        static GUIStyle CardTitleStyle()
        {
            _cardTitle ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            Ui.Text(_cardTitle, 12, bold: true);
            return _cardTitle;
        }

        static GUIStyle CardLineStyle()
        {
            _cardLine ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false };
            Ui.Text(_cardLine, 12);
            return _cardLine;
        }

        // The card's buttons in rows across the width, each as wide as its word needs.
        static List<Rect> ActionRects(BagCard card, float x, float y, float width)
        {
            var rects = new List<Rect>();
            float at = x, row = y;
            foreach (var action in card.Actions)
            {
                float w = Mathf.Max(56, Mathf.Ceil(GUI.skin.button.CalcSize(new GUIContent(action.Label)).x) + 16);
                if (at > x && at + w > x + width)
                {
                    at = x;
                    row += ActionHeight + 4;
                }
                rects.Add(new Rect(at, row, Mathf.Min(w, width), ActionHeight));
                at += w + 4;
            }
            return rects;
        }

        static float QuickLabel => 56;

        // The quick slots in one row of six, or two of three on a narrow card so each stays wide enough for a finger.
        static int QuickRows(float inner) => (inner - QuickLabel - 5 * 4) / Hotbar.Slots < (Controls.UsingTouch ? 36 : 22) ? 2 : 1;

        // How tall the card is for this width: its head, its buttons, the quick slots, then its words.
        static float CardHeight(BagCard card, float width)
        {
            float inner = width - CardPad * 2;
            float height = CardPad + CardHead + 4;
            var rects = ActionRects(card, 0, 0, inner);
            if (rects.Count > 0) height += rects[rects.Count - 1].yMax + 4;
            if (card.Quick != null) height += QuickRows(inner) * (ActionHeight + 4);
            height += TextHeight(card, inner) + CardPad;
            return Mathf.Ceil(height);
        }

        static float TextHeight(BagCard card, float width)
        {
            var style = CardLineStyle();
            float height = 0;
            foreach (var (text, _) in card.Lines) height += style.CalcHeight(new GUIContent(Ui.Wrap(text, style, width)), width);
            return height;
        }

        Vector2 _cardScroll;
        string _cardSeen;

        // The card in its rect: a well, the icon and the name (its row for the playtests), the buttons, the quick slots
        // ([퀵슬롯 1]..[6], the one it is in gold), then its words, scrolling inside the card when they do not fit.
        void DrawCard(BagCard card, Rect rect)
        {
            if (_cardSeen != card.Title)
            {
                _cardSeen = card.Title;
                _cardScroll = Vector2.zero;
            }
            if (Event.current.type == EventType.Repaint) UiSkin.Inset(rect);
            float x = rect.x + CardPad, inner = rect.width - CardPad * 2;
            float y = rect.y + CardPad - 2;
            if (Event.current.type == EventType.Repaint && card.Icon != null) Sprites.DrawInGui(new Rect(x, y + 2, 24, 24), card.Icon);
            var title = CardTitleStyle();
            var previous = GUI.color;
            GUI.color = new Color(card.TitleColor.r, card.TitleColor.g, card.TitleColor.b, previous.a);
            GUI.Label(new Rect(x + 30, y, inner - 30, CardHead), card.Title, title);
            GUI.color = previous;
            // The buttons after it are on its row: the playtests press [장착] in the row "철 검".
            Ui.Row(card.Title);
            y += CardHead + 4;
            var rects = ActionRects(card, x, y, inner);
            var e = Event.current;
            for (int i = 0; i < card.Actions.Count; i++)
            {
                var action = card.Actions[i];
                var at = rects[i];
                var enabled = GUI.enabled;
                GUI.enabled = enabled && action.Enabled;
                if (Ui.Button(at, action.Label, action.Look)) action.Do?.Invoke();
                GUI.enabled = enabled;
                if (e.type == EventType.Repaint && at.Contains(e.mousePosition))
                {
                    if (action.Soil) PointsAtSoil = true;
                    if (action.Size > 1) PointsAtSize = action.Size;
                }
            }
            if (rects.Count > 0) y = rects[rects.Count - 1].yMax + 4;
            if (card.Quick != null)
            {
                GUI.Label(new Rect(x, y, QuickLabel, ActionHeight), "퀵슬롯", CardLineStyle());
                int lines = QuickRows(inner), across = Hotbar.Slots / lines;
                float size = Mathf.Min(lines > 1 ? 64 : ActionHeight, (inner - QuickLabel - (across - 1) * 4) / across);
                for (int slot = 0; slot < Hotbar.Slots; slot++)
                {
                    var at = new Rect(x + QuickLabel + slot % across * (size + 4), y + slot / across * (ActionHeight + 4), size, ActionHeight);
                    bool here = card.QuickAt == slot;
                    if (Ui.Button(at, (slot + 1).ToString(), $"퀵슬롯 {slot + 1}", here ? Ui.Look.Primary : Ui.Look.Normal))
                    {
                        card.Quick(slot);
                        Sounds.Play(Sound.Click);
                    }
                }
                y += lines * (ActionHeight + 4);
            }
            Ui.Row("");
            // The words, scrolling when the card is held short (a low screen, a long item).
            var words = Rect.MinMaxRect(x, y, x + inner, rect.yMax - CardPad + 2);
            if (words.height < 8) return;
            var style = CardLineStyle();
            float height = TextHeight(card, inner);
            Ui.TouchScroll(words, ref _cardScroll);
            _cardScroll = GUI.BeginScrollView(words, _cardScroll, new Rect(0, 0, inner - 2, Mathf.Max(height, words.height)), false, false, GUIStyle.none, GUIStyle.none);
            float line = 0;
            foreach (var (text, color) in card.Lines)
            {
                string wrapped = Ui.Wrap(text, style, inner);
                float h = style.CalcHeight(new GUIContent(wrapped), inner);
                GUI.color = new Color(color.r, color.g, color.b, previous.a);
                GUI.Label(new Rect(0, line, inner, h), wrapped, style);
                line += h;
            }
            GUI.color = previous;
            GUI.EndScrollView();
            // More below: a small fade at the foot says so.
            if (height > words.height + 1 && _cardScroll.y < height - words.height - 1 && Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(0, 0, 0, 0.45f * previous.a);
                GUI.DrawTexture(new Rect(words.x, words.yMax - 8, words.width, 8), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
    }
}
