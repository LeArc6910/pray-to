using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The encounter windows (Docs/UiRework.md 4-1 만남, U3), out of the bag's tabs into windows of their own, each opened
    // by meeting the thing (G, or the button over it in the world) and shut once it is out of reach (GameRoot):
    //  - 상점: a stall keeper's shop under an awning with her name on its sign; under it her small portrait, hearts and
    //    today's line with [말 걸기]·[이사 권하기]; the tabs 사기·팔기·선물·부탁·이야기; her goods (or what she buys, the
    //    gifts, the request, her story) as cards with their price, the bag's things beside them (under them on a phone).
    //  - 상자: the chest's things with [꺼내기] beside the bag's with [넣기] and [상자에 넣기].
    //  - 작업대: the recipes of the stations in reach (workbench, furnace) and by hand as cards with what they take and
    //    [만들기]·[×N], what is being made over them, the bag's materials beside them.
    //  - 모루: the gear to pick (worn and carried) beside what the anvil does to the picked piece (강화, 강화 옮기기, 분해, 룬
    //    빼기, 접사 다시), and the anvil's own recipes.
    // Each is a window with two panes side by side on a PC and on a phone held sideways, one over the other on a phone held
    // upright; on a phone the window is a sheet over the whole screen (ScreenStack.EncounterArea). Every button and row
    // keeps the words the bag's tabs had, so the playtests press them as before.
    sealed partial class InventoryWindow
    {
        Vector2 _thingScroll, _bagScroll;
        // The pane a finger started dragging in (0 the thing's, 1 the bag's).
        int _dragPane;
        GameScreen _encounterSeen;
        // The station's tab: a station in reach, or null for what is made by hand; the anvil's tab.
        TileType? _stationTab;
        bool _anvilRecipes;

        // Opens an encounter window (its things start at the top).
        internal void ShowEncounter(GameScreen screen) => _screens.Show(screen);

        // The shop of the stall keeper in reach on its tab by name (사기, 팔기, 선물, 부탁, 이야기): a choice of her talk.
        internal void ShowShop(string tab)
        {
            int index = Array.IndexOf(ShopModeNames, tab);
            if (index >= 0) _shopMode = (ShopMode)index;
            _screens.Show(GameScreen.Shop);
        }

        // The shop's tab, for the playtests.
        internal string ShopTab => ShopModeNames[(int)_shopMode];

        // Whether what the window is for is still in reach: GameRoot shuts it once it is not (walked away).
        internal bool InReach(GameScreen screen) => screen switch
        {
            GameScreen.Shop => _client.NpcNear() != null,
            GameScreen.Chest => _client.Chest.IsOpen,
            GameScreen.Station => StationsNear().Count > 0,
            GameScreen.Anvil => _client.NearAnvil,
            _ => true,
        };

        static readonly TileType[] Stations = { TileType.Workbench, TileType.Furnace };

        // The crafting stations within reach, workbench first.
        internal List<TileType> StationsNear()
        {
            var map = _client.Map;
            var near = new List<TileType>();
            if (map == null) return near;
            foreach (var station in Stations)
                if (map.Near(_client.Position, station, World.StationReach)) near.Add(station);
            return near;
        }

        // ---- The window ----

        void DrawEncounter(GameScreen screen)
        {
            var area = ScreenStack.EncounterArea(screen);
            if (_encounterSeen != screen)
            {
                _encounterSeen = screen;
                _thingScroll = _bagScroll = Vector2.zero;
                if (screen == GameScreen.Station) _stationTab = null;
            }
            Controls.Block(area);
            var kind = ScreenStack.KindOf(screen);
            var keeper = screen == GameScreen.Shop ? _client.NpcNear()?.Npc : null;
            var stations = screen == GameScreen.Station ? StationsNear() : null;
            string title = screen switch
            {
                GameScreen.Shop => keeper?.Name ?? "상점",
                GameScreen.Chest => $"상자 {_client.Chest.Contents?.Items.Count ?? 0}/{Inventory.Capacity}",
                GameScreen.Station => stations.Count == 0 ? "작업대" : string.Join("·", stations.Select(StationName)),
                _ => "모루",
            };
            UiMotion.BeginWindow(ScreenStack.MotionKey(screen), area, kind);
            var inside = UiSkin.Window(area, kind, title, ScreenStack.IconOf(screen), out var head);
            float right = UiSkin.CloseRect(head).x - 6;
            DrawCoins(head, right, kind);
            if (UiSkin.CloseButton(head))
            {
                _screens.Close();
                UiMotion.EndWindow();
                return;
            }
            if (_hotbar != null && _hotbar.ShowsMessage)
            {
                var line = new Rect(inside.x, inside.y, inside.width, 20);
                GUI.Label(line, Ui.Wrap(_hotbar.Message, GUI.skin.label, inside.width));
                inside = Rect.MinMaxRect(inside.x, line.yMax, inside.xMax, inside.yMax);
            }
            _rows = inside;
            switch (screen)
            {
                case GameScreen.Shop:
                    DrawShop(inside, keeper);
                    break;
                case GameScreen.Chest:
                    DrawChestWindow(inside);
                    break;
                case GameScreen.Station:
                    DrawStation(inside, stations);
                    break;
                default:
                    DrawAnvil(inside);
                    break;
            }
            UiMotion.EndWindow();
            DrawTooltip(area, false);
        }

        // ---- Panes ----

        const float PaneGap = 8;

        // Two panes: side by side where the window is wide enough (the thing's taking `share` of it), else one over the
        // other.
        static (Rect First, Rect Second) Split(Rect area, float share)
        {
            if (area.width >= 560)
            {
                float first = Mathf.Round((area.width - PaneGap) * share);
                return (new Rect(area.x, area.y, first, area.height), Rect.MinMaxRect(area.x + first + PaneGap, area.y, area.xMax, area.yMax));
            }
            float top = Mathf.Round((area.height - PaneGap) * Mathf.Max(share, 0.56f));
            return (new Rect(area.x, area.y, area.width, top), Rect.MinMaxRect(area.x, area.y + top + PaneGap, area.xMax, area.yMax));
        }

        // A list that scrolls (by the wheel a line a notch, by a finger dragged) in the pane; the rows' widths follow it.
        // The bag's pane sits in a well under its heading.
        Vector2 Pane(Rect pane, Vector2 scroll, Action draw, string heading = null)
        {
            if (heading != null)
            {
                UiSkin.Inset(pane);
                var line = new Rect(pane.x + 8, pane.y + 4, pane.width - 16, 20);
                _paneHeading ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip };
                _paneHeading.normal.textColor = UiSkin.Gold;
                Ui.Row(heading);
                GUI.Label(line, Ui.Wrap(heading, _paneHeading, 9999), _paneHeading);
                pane = Rect.MinMaxRect(pane.x + 4, line.yMax + 2, pane.xMax - 4, pane.yMax - 4);
            }
            float scrollbar = GUI.skin.verticalScrollbar.fixedWidth + 8;
            _textWidth = pane.width - scrollbar - GUI.skin.label.margin.horizontal;
            _nameWidth = Mathf.Clamp(_textWidth - IconRoom - 76, 60, 320);
            _recipeWidth = _textWidth - IconRoom - 120;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && pane.Contains(e.mousePosition))
            {
                float notches = Mathf.Sign(e.delta.y) * Mathf.Clamp(Mathf.Abs(e.delta.y), 1, 3);
                scroll.y = Mathf.Max(0, scroll.y + notches * WheelStep);
                e.Use();
            }
            GUILayout.BeginArea(pane);
            scroll = Ui.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            draw();
            Ui.EndScrollView();
            GUILayout.EndArea();
            return scroll;
        }

        static GUIStyle _paneHeading;

        // A finger dragging scrolls the pane it started in (Ui.TouchScroll keeps one drag at a time).
        void PaneTouch(Rect first, Rect second)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown) _dragPane = second.Contains(e.mousePosition) ? 1 : 0;
            if (_dragPane == 1) Ui.TouchScroll(second, ref _bagScroll);
            else Ui.TouchScroll(first, ref _thingScroll);
        }

        // Tabs in a row across the rect that switch as soon as they are pressed (as the bag's do), each known to the
        // playtests by its name. Gives the one chosen.
        static int RowTabs(Rect row, string[] names, int chosen)
        {
            Ui.Row("");
            const float gap = 4;
            float width = (row.width - gap * (names.Length - 1)) / names.Length;
            var e = Event.current;
            for (int i = 0; i < names.Length; i++)
            {
                var tab = new Rect(row.x + i * (width + gap), row.y, width, row.height);
                if (e.type == EventType.MouseDown && tab.Contains(e.mousePosition))
                {
                    if (chosen != i)
                    {
                        chosen = i;
                        UiMotion.Confirm();
                    }
                    e.Use();
                }
                Ui.Probe(names[i], tab);
                if (e.type == EventType.Repaint) UiSkin.Tab(tab, new GUIContent(names[i]), null, chosen == i, tab.Contains(e.mousePosition));
            }
            return chosen;
        }

        static float TabRowHeight => Controls.UsingTouch ? 40 : 30;
        static float ButtonHeight => Controls.UsingTouch ? 40 : 28;

        // ---- Cards (Docs/UiRework.md 4-2 상점: not the old "icon name · 금화 N [사기][×5]" lines) ----

        static GUIStyle _cardName, _cardSub, _cardPrice;

        // A card in a list: a well with the thing's icon in a slot of its colour, its name over a second line, its price
        // with a coin at the right, and room after that for the caller's buttons (the rect given back). `row` is what the
        // playtests know the row by: the old line's words. A card of a thing not to be had yet is faint.
        Rect Card(Sprite icon, Color edge, string name, string sub, string row, int price, float buttons, Color? subColor = null, string tooltip = null, bool dim = false)
        {
            float height = Controls.UsingTouch ? 50 : 42;
            var rect = GUILayoutUtility.GetRect(_textWidth, height + 4, GUILayout.Width(_textWidth), GUILayout.Height(height + 4));
            rect.height = height;
            Ui.Row(row);
            Hover(rect, tooltip);
            _cardName ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            _cardSub ??= new GUIStyle(_cardName);
            _cardPrice ??= new GUIStyle(_cardName) { alignment = TextAnchor.MiddleRight };
            _cardName.normal.textColor = new Color(0.97f, 0.93f, 0.85f);
            _cardSub.normal.textColor = subColor ?? UiSkin.Muted;
            _cardPrice.normal.textColor = new Color(1f, 0.85f, 0.4f);
            var e = Event.current;
            var previous = GUI.color;
            if (dim) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * 0.6f);
            if (e.type == EventType.Repaint)
            {
                UiSkin.Inset(rect);
                var slot = new Rect(rect.x + 5, rect.y + 5, height - 10, height - 10);
                UiSkin.SlotFrame(slot, edge);
                float inset = Mathf.Round(slot.width * 0.16f);
                if (icon != null) Sprites.DrawInGui(new Rect(slot.x + inset, slot.y + inset, slot.width - 2 * inset, slot.height - 2 * inset), icon);
            }
            float textX = rect.x + height + 2;
            float right = rect.xMax - 6 - buttons;
            if (price >= 0)
            {
                string text = price.ToString("N0");
                float width = _cardPrice.CalcSize(new GUIContent(text)).x;
                var coin = UiSkin.IconSize(UiIcons.Coin);
                GUI.Label(new Rect(right - width - 4, rect.y, width + 4, height), text, _cardPrice);
                UiSkin.Icon(new Rect(right - width - 8 - coin.x, rect.center.y - coin.y / 2, coin.x, coin.y), UiIcons.Coin, new Color(0.95f, 0.75f, 0.25f));
                right -= width + coin.x + 16;
            }
            float textWidth = Mathf.Max(20, right - textX);
            bool two = !string.IsNullOrEmpty(sub);
            GUI.Label(new Rect(textX, two ? rect.y + 3 : rect.y, textWidth, two ? height / 2 - 1 : height), Ui.Wrap(name, _cardName, 9999), _cardName);
            if (two) GUI.Label(new Rect(textX, rect.y + height / 2 - 1, textWidth, height / 2 - 2), Ui.Wrap(sub, _cardSub, 9999), _cardSub);
            GUI.color = previous;
            float buttonHeight = Mathf.Min(ButtonHeight, height - 8);
            return new Rect(rect.xMax - 4 - buttons, rect.y + (height - buttonHeight) / 2, buttons, buttonHeight);
        }

        // A button in a card's room, from its left (`x` moves past it).
        static bool CardButton(Rect room, ref float x, float width, string label, Ui.Look look = Ui.Look.Normal)
        {
            var rect = new Rect(room.x + x, room.y, width, room.height);
            x += width + 4;
            return Ui.Button(rect, label, look);
        }

        // The bag's things as a grid of squares (to look at beside a shop's list: their counts, and a card when the pointer
        // or a finger rests on one). `materialsOnly` for a station's pane.
        void BagGrid(bool materialsOnly = false)
        {
            float cell = Controls.UsingTouch ? 46 : 40;
            var items = materialsOnly ? new List<Item>() : _client.Bag.Items.ToList();
            var consumables = materialsOnly ? new List<KeyValuePair<ConsumableDefinition, int>>() : _client.Bag.Consumables.ToList();
            var materials = _client.Bag.Materials.ToList();
            int count = items.Count + consumables.Count + materials.Count;
            if (count == 0)
            {
                GUILayout.Label(Fit(materialsOnly ? "재료가 없다" : "가방이 비었다", _textWidth));
                return;
            }
            var grid = Grid(count, cell);
            int index = 0;
            foreach (var item in items)
                Cell(CellAt(grid, index++, cell), Sprites.Icon(item.Base), ItemText.Name(item), null, Palette.Of(item.Rarity), false, ItemTooltip(item));
            foreach (var (consumable, number) in consumables)
                Cell(CellAt(grid, index++, cell), Sprites.Icon(consumable), $"{consumable.Name} ×{number}", number.ToString(), Palette.Potion, false, consumable.Description);
            foreach (var (material, number) in materials)
                Cell(CellAt(grid, index++, cell), Sprites.Icon(material), $"{material.Name} ×{number}", number.ToString(), Palette.Material, false, $"{material.Name} ×{number}\n{material.Description}");
        }

        // ---- 상점 ----

        // The stall keeper's shop: her strip (portrait, name, hearts, today's line, [말 걸기], [이사 권하기]), the tabs, her
        // list beside the bag.
        void DrawShop(Rect inside, NpcDefinition npc)
        {
            if (npc == null)
            {
                GUI.Label(inside, Ui.Wrap("곁에 상인이 없다", GUI.skin.label, inside.width));
                return;
            }
            var near = _client.NpcNear().Value;
            int hearts = _client.HeartsWith(npc);
            var coin = _client.Bag.Materials.Keys.FirstOrDefault(m => m.Id == World.Coin);
            int coins = coin == null ? 0 : _client.Bag.CountOf(coin);
            var strip = DrawKeeper(inside, npc, hearts, near.X, near.Y);
            var tabs = new Rect(inside.x, strip.yMax + 4, inside.width, TabRowHeight);
            int mode = RowTabs(tabs, ShopModeNames, (int)_shopMode);
            if (mode != (int)_shopMode)
            {
                _shopMode = (ShopMode)mode;
                _thingScroll = Vector2.zero;
            }
            var rest = Rect.MinMaxRect(inside.x, tabs.yMax + 6, inside.xMax, inside.yMax);
            var (list, bag) = Split(rest, 0.6f);
            PaneTouch(list, bag);
            _thingScroll = Pane(list, _thingScroll, () =>
            {
                if (_shopMode == ShopMode.Buy) DrawBuyingCards(npc, hearts, coins);
                else if (_shopMode == ShopMode.Sell) DrawSellingCards(npc, hearts);
                else if (_shopMode == ShopMode.Gift) DrawGiftCards(npc);
                else if (_shopMode == ShopMode.Request) DrawRequest(npc);
                else DrawStoryTab(npc, hearts);
            });
            _bagScroll = Pane(bag, _bagScroll, () => BagGrid(), $"가방 {_client.Bag.Items.Count}/{Inventory.Capacity}");
        }

        static GUIStyle _keeperName, _keeperLine;

        // Her small portrait (1 point a pixel; the head alone on a phone held sideways), her name and hearts, today's line,
        // and the day's chat and moving in at the right (under the rest in a narrow window). Gives the strip's rect.
        Rect DrawKeeper(Rect inside, NpcDefinition npc, int hearts, int x, int y)
        {
            int rows = Ui.Short ? 30 : 0;
            var picture = UiSkin.PortraitSize(1, rows);
            float edge = 2 * UiArt.InsetBorder * UiSkin.Pt + 4;
            var slot = new Rect(inside.x, inside.y, picture.x + edge, picture.y + edge);
            var (home, fault) = HousingOf(npc, x, y);
            bool narrow = inside.width < 520;
            float buttons = home ? 112 : 224;
            float stripHeight = narrow ? slot.height + 4 + ButtonHeight : Mathf.Max(slot.height, ButtonHeight);
            var strip = new Rect(inside.x, inside.y, inside.width, stripHeight);
            var color = ColorUtility.TryParseHtmlString(npc.Color.StartsWith("#") ? npc.Color : "#" + npc.Color, out var c) ? c : new Color(0.6f, 0.35f, 0.8f);
            var rgba = new Rgba(Mathf.RoundToInt(color.r * 255), Mathf.RoundToInt(color.g * 255), Mathf.RoundToInt(color.b * 255), 255);
            UiSkin.Portrait(slot, PortraitArt.Of(npc.Id, null, rgba, hearts >= 3 ? Expression.Happy : Expression.Neutral), 1, color, rows);
            float textX = slot.xMax + 8;
            float textRight = narrow ? inside.xMax : inside.xMax - buttons - 8;
            _keeperName ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            _keeperLine ??= new GUIStyle(GUI.skin.label) { wordWrap = true, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            _keeperName.normal.textColor = UiSkin.Gold;
            _keeperLine.normal.textColor = new Color(0.9f, 0.86f, 0.78f);
            // The words the bag's tab had at its top, for the playtests and the person both.
            string standing = $"{npc.Name} · 호감 {hearts}/{World.MaxHearts} ({_client.PointsWith(npc)}/{World.MaxHearts * World.PointsPerHeart})";
            Ui.Row(standing);
            var nameLine = new Rect(textX, slot.y + 2, textRight - textX, 18);
            string shownName = Ui.Wrap(standing, _keeperName, 9999);
            GUI.Label(nameLine, shownName, _keeperName);
            float lineTop = nameLine.yMax + 2;
            if (!Ui.Short)
            {
                // Her hearts under the name.
                var heart = UiSkin.IconSize(UiIcons.Heart);
                for (int i = 0; i < World.MaxHearts; i++)
                    UiSkin.Icon(new Rect(textX + i * (heart.x + 2), lineTop, heart.x, heart.y), UiIcons.Heart, i < hearts ? new Color(0.9f, 0.25f, 0.3f) : new Color(0.3f, 0.2f, 0.2f));
                lineTop += heart.y + 4;
            }
            string said = $"\"{npc.LineFor(_client.Today, hearts)}\"";
            GUI.Label(Rect.MinMaxRect(textX, lineTop, textRight, slot.yMax), Ui.Wrap(said, _keeperLine, textRight - textX), _keeperLine);
            // [말 걸기] (once a day) and [이사 권하기] (once she likes the player enough and a room is free).
            var buttonsRow = narrow ? new Rect(inside.x, slot.yMax + 4, inside.width, ButtonHeight) : new Rect(inside.xMax - buttons, slot.y + (slot.height - ButtonHeight) / 2, buttons, ButtonHeight);
            float width = home ? buttonsRow.width : (buttonsRow.width - 4) / 2;
            var enabled = GUI.enabled;
            bool talked = _client.TalkedToday(npc);
            GUI.enabled = enabled && !talked;
            if (Ui.Button(new Rect(buttonsRow.x, buttonsRow.y, width, buttonsRow.height), talked ? "오늘은 이야기했다" : "말 걸기")) Send(_client.TalkCommand(), Sound.Click);
            if (!home)
            {
                GUI.enabled = enabled && fault == null;
                if (Ui.Button(new Rect(buttonsRow.x + width + 4, buttonsRow.y, width, buttonsRow.height), "이사 권하기")) Send(_client.InviteCommand(), Sound.Click);
            }
            GUI.enabled = enabled;
            return strip;
        }

        // Her goods as cards: open ones first with their price, [사기] and [×5]; the ones that open at more hearts at the
        // bottom, faint, with the hearts they wait for (4-2: 잠긴 물건은 목록 맨 아래에).
        void DrawBuyingCards(NpcDefinition npc, int hearts, int coins)
        {
            var near = _client.NpcNear();
            var (home, fault) = near != null ? HousingOf(npc, near.Value.X, near.Value.Y) : (false, null);
            if (home) GUILayout.Label(Fit($"기지의 방에 산다: 말 걸기와 선물로 오르는 호감이 {Housing.FriendshipBonus}배다.", _textWidth));
            else if (fault != null) GUILayout.Label(Fit($"이사: {fault}", _textWidth));
            GUILayout.Label(Fit($"호감이 오를 때마다 사는 값은 {World.HeartPercent}% 싸지고 파는 값은 {World.HeartPercent}% 비싸진다.", _textWidth));
            var enabled = GUI.enabled;
            var entries = _client.ShopTodayOf(npc).ToList();
            foreach (var entry in entries.Where(e => hearts >= e.Hearts).Concat(entries.Where(e => hearts < e.Hearts)))
            {
                int price = World.BuyPrice(entry, hearts, Calendar.Day(_client.Clock));
                string note = entry.Daily ? " (오늘만)" : "";
                bool open = hearts >= entry.Hearts;
                string row = open ? $"{entry.Name}{note} · 금화 {price}" : $"{entry.Name} · 호감 {entry.Hearts}에 열린다";
                string description = entry.Material?.Description ?? entry.Consumable.Description;
                string sub = open ? (entry.Daily ? "오늘만 · " : "") + First(description) : $"호감 {entry.Hearts}에 열린다";
                var icon = entry.Material != null ? Sprites.Icon(entry.Material) : Sprites.Icon(entry.Consumable);
                var room = Card(icon, entry.Material != null ? Palette.Material : Palette.Potion, entry.Name, sub, row, open ? price : -1, 100,
                    open ? (Color?)null : new Color(0.95f, 0.45f, 0.4f), description, dim: !open);
                float x = 0;
                GUI.enabled = enabled && open && coins >= price;
                if (CardButton(room, ref x, 56, "사기", Ui.Look.Primary)) Send(_client.BuyCommand(entry, 1), Sound.Craft);
                GUI.enabled = enabled && open && coins >= price * 5;
                if (CardButton(room, ref x, 40, "×5")) Send(_client.BuyCommand(entry, 5), Sound.Craft);
                GUI.enabled = enabled;
            }
        }

        // The first sentence (or line) of a description, for a card's second line.
        static string First(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int end = text.IndexOfAny(new[] { '\n', '.' });
            return end > 0 ? text.Substring(0, end) : text;
        }

        // What she buys from the bag, as cards: one of a kind a quality (the better dearer), how many more she takes today
        // ([팔기], [모두]); gear by a smith ([팔기 N]).
        void DrawSellingCards(NpcDefinition npc, int hearts)
        {
            bool any = false;
            var enabled = GUI.enabled;
            foreach (var entry in _client.Npcs.Shop.Where(e => e.Sell > 0 && e.Npc == npc))
            {
                int left = _client.SellLeft(entry);
                var kinds = entry.Material != null ? entry.Material.AllQualities.Cast<object>() : new object[] { entry.Consumable };
                foreach (var thing in kinds)
                {
                    var material = thing as MaterialDefinition;
                    int have = material != null ? _client.Bag.CountOf(material) : _client.Bag.CountOf(entry.Consumable);
                    if (have == 0) continue;
                    any = true;
                    string name = material?.Name ?? entry.Name;
                    int price = World.SellPrice(entry, hearts, material?.Quality ?? 0, Calendar.Day(_client.Clock));
                    string room = left > 0 ? $" · 오늘 {left}개 더" : " · 오늘은 그만 산다";
                    string row = $"{name} ×{have} · 하나에 금화 {price}{room}";
                    var icon = material != null ? Sprites.Icon(material) : Sprites.Icon(entry.Consumable);
                    var buttons = Card(icon, material != null ? Palette.Material : Palette.Potion, $"{name} ×{have}", left > 0 ? $"하나에 금화 · 오늘 {left}개 더" : "오늘은 그만 산다",
                        row, price, 108, left > 0 ? (Color?)null : new Color(0.95f, 0.45f, 0.4f), (material?.Description ?? entry.Consumable.Description));
                    float x = 0;
                    GUI.enabled = enabled && left > 0;
                    if (CardButton(buttons, ref x, 56, "팔기")) Send(_client.SellCommand(thing, 1), Sound.Pickup);
                    if (CardButton(buttons, ref x, 48, "모두")) Send(_client.SellCommand(thing, Math.Min(Math.Min(have, left), World.MaxTrade)), Sound.Pickup);
                    GUI.enabled = enabled;
                }
            }
            foreach (var item in npc.BuysGear ? _client.Bag.Items.ToList() : new List<Item>())
            {
                any = true;
                string sockets = item.Sockets.Count == 0 ? "" : " " + string.Concat(item.Sockets.Select(r => r == null ? "○" : "●"));
                string name = ItemText.Name(item) + sockets;
                int value = World.ItemValue(item, hearts);
                var room = Card(Sprites.Icon(item.Base), Palette.Of(item.Rarity), name, ItemText.SlotName(item.Base.Slot), name, value, 92, tooltip: ItemTooltip(item));
                float x = 0;
                if (CardButton(room, ref x, 88, $"팔기 {value}")) Send(_client.SellItemCommand(item), Sound.Pickup);
            }
            if (!any) GUILayout.Label(Fit(npc.BuysGear ? "팔 것이 없다. 입은 장비는 벗어야 판다." : "이 사람이 사는 것이 가방에 없다.", _textWidth));
        }

        // The bag's things to give, as cards with [선물]; one gift a day.
        void DrawGiftCards(NpcDefinition npc)
        {
            if (_client.GiftedToday(npc))
            {
                GUILayout.Label(Fit("오늘은 선물을 주었다. 내일 또 줄 수 있다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit("하루에 하나. 좋아하는 것을 주면 호감이 크게 오른다.", _textWidth));
            foreach (var pair in _client.Bag.Consumables.ToList())
            {
                var room = Card(Sprites.Icon(pair.Key), Palette.Potion, $"{pair.Key.Name} ×{pair.Value}", First(pair.Key.Description), $"{pair.Key.Name} ×{pair.Value}", -1, 60, tooltip: pair.Key.Description);
                float x = 0;
                if (CardButton(room, ref x, 56, "선물")) Send(_client.GiftCommand(pair.Key), Sound.Pop);
            }
            foreach (var pair in _client.Bag.Materials.Where(p => p.Key.Id != World.Coin).ToList())
            {
                var room = Card(Sprites.Icon(pair.Key), Palette.Material, $"{pair.Key.Name} ×{pair.Value}", First(pair.Key.Description), $"{pair.Key.Name} ×{pair.Value}", -1, 60, tooltip: pair.Key.Description);
                float x = 0;
                if (CardButton(room, ref x, 56, "선물")) Send(_client.GiftCommand(pair.Key), Sound.Pop);
            }
        }

        // The stories she told with each heart gift, then her story's steps ([건네기]).
        void DrawStoryTab(NpcDefinition npc, int hearts)
        {
            foreach (var story in npc.Stories.OrderBy(p => p.Key))
                if (_client.HadHeartGift(npc, story.Key)) GUILayout.Label(Fit($"(호감 {story.Key}) \"{story.Value}\"", _textWidth));
            DrawStory(npc, hearts);
        }

        // ---- 상자 ----

        // The chest's things with [꺼내기] beside the bag's with [넣기], and [상자에 넣기] over the bag's.
        void DrawChestWindow(Rect inside)
        {
            var chest = _client.Chest.Contents;
            if (chest == null)
            {
                GUI.Label(inside, Ui.Wrap("곁에 상자가 없다", GUI.skin.label, inside.width));
                return;
            }
            var (first, second) = Split(inside, 0.5f);
            PaneTouch(first, second);
            _thingScroll = Pane(first, _thingScroll, () =>
                DrawContents(chest, "꺼내기", item => _client.TakeItemCommand(item.Id), (kind, definition) => _client.TakeStackCommand(kind, definition)),
                $"상자 ({chest.Items.Count}/{Inventory.Capacity})");
            _bagScroll = Pane(second, _bagScroll, () =>
            {
                DrawStash();
                DrawContents(_client.Bag, "넣기", item => _client.StoreItemCommand(item.Id), (kind, definition) => _client.StoreStackCommand(kind, definition));
            }, $"가방 ({_client.Bag.Items.Count}/{Inventory.Capacity})");
        }

        // ---- 작업대 ----

        // The station's window: what is being made (a bar and [그만]), a tab for each station in reach and for by hand,
        // the recipes as cards beside the bag's materials.
        void DrawStation(Rect inside, List<TileType> stations)
        {
            float top = inside.y;
            if (_client.Crafting != null)
            {
                // What is being made, wrapped to the room beside [그만], and a bar under it.
                string more = _client.CraftLeft > 1 ? $" · 남은 {_client.CraftLeft}개" : "";
                string making = $"만드는 중: {_client.Crafting.Name}{more} (작업대 곁을 떠나면 멈춘다)";
                Ui.Row(making);
                float room = inside.width - 64;
                string wrapped = Ui.Wrap(making, GUI.skin.label, room);
                float height = Mathf.Max(24, GUI.skin.label.CalcHeight(new GUIContent(wrapped), room));
                GUI.Label(new Rect(inside.x, top, room, height), wrapped);
                if (Ui.Button(new Rect(inside.xMax - 56, top, 56, ButtonHeight), "그만")) Send(_client.StopCraftCommand(), Sound.Click);
                float bar = Mathf.Max(height, ButtonHeight) + 2;
                UiSkin.Bar(new Rect(inside.x, top + bar, inside.width, 10), _client.CraftProgress, "");
                top += bar + 16;
            }
            var tabs = stations.Select(s => (TileType?)s).Append(null).ToList();
            if (!tabs.Contains(_stationTab)) _stationTab = tabs[0];
            var names = tabs.Select(t => t == null ? "손 제작" : StationName(t.Value)).ToArray();
            // [만들 수 있는 것만] beside the tabs, or under them in a narrow window.
            float toggle = 156;
            bool narrow = inside.width < 520;
            var row = new Rect(inside.x, top, narrow ? inside.width : inside.width - toggle - 6, TabRowHeight);
            int chosen = RowTabs(row, names, tabs.IndexOf(_stationTab));
            if (tabs[chosen] != _stationTab)
            {
                _stationTab = tabs[chosen];
                _thingScroll = Vector2.zero;
            }
            var toggleRect = narrow ? new Rect(inside.xMax - toggle, row.yMax + 4, toggle, ButtonHeight) : new Rect(row.xMax + 6, top, toggle, TabRowHeight);
            if (Ui.Button(toggleRect, _craftableOnly ? "모두 보기" : "만들 수 있는 것만")) _craftableOnly = !_craftableOnly;
            var rest = Rect.MinMaxRect(inside.x, Mathf.Max(row.yMax, toggleRect.yMax) + 6, inside.xMax, inside.yMax);
            var (list, bag) = Split(rest, 0.62f);
            PaneTouch(list, bag);
            var station = _stationTab;
            _thingScroll = Pane(list, _thingScroll, () => RecipeCards(r => r.Station == station));
            _bagScroll = Pane(bag, _bagScroll, () => BagGrid(materialsOnly: true), "재료");
        }

        // The recipes made at one place as cards: the thing, what it takes against what is at hand (green when all is),
        // how long by hand, [만들기] and [×N] (up to five); facing an assembler, [조립기에].
        void RecipeCards(Func<Recipe, bool> where)
        {
            var enabled = GUI.enabled;
            var assembler = _client.MachineAhead() is MachineView m && World.IsAssembler(m.Kind) ? m : (MachineView?)null;
            bool any = false;
            foreach (var recipe in _client.Recipes.Where(r => !_client.MadeByMachine(r) && where(r)))
            {
                int times = _client.CraftableTimes(recipe);
                if (_craftableOnly && times == 0) continue;
                any = true;
                string needs = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {_client.Available(i.Material)}/{i.Count}"));
                float seconds = _client.CraftSeconds(recipe);
                if (seconds > 0) needs += $" · {seconds:0.#}초";
                var missing = _client.MissingStation(recipe);
                if (missing != null) needs += $" · {StationName(missing.Value)} 필요";
                string name = recipe.Count > 1 ? $"{recipe.Name} ×{recipe.Count}" : recipe.Name;
                var icon = recipe.Item != null ? Sprites.Icon(recipe.Item) : recipe.Consumable != null ? Sprites.Icon(recipe.Consumable) : Sprites.Icon(recipe.Material);
                var edge = recipe.Item != null ? Palette.Of(Rarity.Common) : recipe.Consumable != null ? Palette.Potion : Palette.Material;
                bool toAssembler = assembler != null && World.Assembles(recipe);
                var room = Card(icon, edge, name, needs, $"{name}\n{needs}", -1, toAssembler ? 188 : 112,
                    times > 0 ? new Color(0.55f, 0.9f, 0.5f) : (Color?)null, RecipeTooltip(recipe), dim: times == 0);
                float x = 0;
                var pressed = seconds > 0 ? Sound.Click : Sound.Craft;
                GUI.enabled = enabled && times > 0;
                if (CardButton(room, ref x, 64, "만들기", Ui.Look.Primary)) Send(_client.CraftCommand(recipe), pressed);
                GUI.enabled = enabled && times > 1;
                int bulk = Mathf.Clamp(times, 2, BulkCraft);
                if (CardButton(room, ref x, 40, $"×{bulk}")) Send(_client.CraftCommand(recipe, bulk), pressed);
                GUI.enabled = enabled;
                if (toAssembler && CardButton(room, ref x, 72, "조립기에")) Send(_client.SetRecipeCommand(recipe), Sound.Click);
            }
            if (!any) GUILayout.Label(Fit(_craftableOnly ? "지금 가진 재료로 여기서 만들 수 있는 것이 없다" : "여기서 만들 것이 없다", _textWidth));
        }

        // ---- 모루 ----

        static readonly string[] AnvilTabs = { "장비 다듬기", "모루 조합법" };

        // The anvil's window: the gear to pick (worn, then carried) and the picked piece's [다듬기] in one pane, what the
        // anvil does to it in the other; or the anvil's own recipes.
        void DrawAnvil(Rect inside)
        {
            var row = new Rect(inside.x, inside.y, inside.width, TabRowHeight);
            int chosen = RowTabs(row, AnvilTabs, _anvilRecipes ? 1 : 0);
            if (chosen == 1 != _anvilRecipes)
            {
                _anvilRecipes = chosen == 1;
                _thingScroll = Vector2.zero;
            }
            var rest = Rect.MinMaxRect(inside.x, row.yMax + 6, inside.xMax, inside.yMax);
            if (_anvilRecipes)
            {
                var (list, bag) = Split(rest, 0.62f);
                PaneTouch(list, bag);
                _thingScroll = Pane(list, _thingScroll, () => RecipeCards(r => r.Station == TileType.Anvil));
                _bagScroll = Pane(bag, _bagScroll, () => BagGrid(materialsOnly: true), "재료");
                return;
            }
            var (gear, work) = Split(rest, 0.5f);
            PaneTouch(gear, work);
            _thingScroll = Pane(gear, _thingScroll, AnvilGear);
            _bagScroll = Pane(work, _bagScroll, () =>
            {
                if (_client.Equipment.Items.Concat(_client.Bag.Items).All(i => i.Id != _refining)) _refining = 0;
                if (_refining == 0)
                {
                    var essence = _client.Loot?.FindMaterial(World.Essence);
                    int have = essence == null ? 0 : _client.Bag.CountOf(essence);
                    GUILayout.Label(Fit($"장비를 고르면 여기서 강화, 강화 옮기기, 분해, 룬 빼기, 접사 다시 굴리기를 한다. 강화 정수 {have}개", _textWidth));
                    return;
                }
                DrawAnvilWork();
            }, "모루");
        }

        // What the anvil does to the picked piece, one full-width button a line (the words the bag's anvil lines had before U3): its card with
        // the essence at hand, [강화 +N (...)], [강화 옮기기: ...], [분해 (...)], [룬 빼기 (...)], [접사 다시 (...)].
        void DrawAnvilWork()
        {
            var loot = _client.Loot;
            var essence = loot?.FindMaterial(World.Essence);
            int have = essence == null ? 0 : _client.Bag.CountOf(essence);
            var worn = _client.Equipment.Items.FirstOrDefault(i => i.Id == _refining);
            var item = worn ?? _client.Bag.Find(_refining);
            if (item == null) return;
            Card(Sprites.Icon(item.Base), Palette.Of(item.Rarity), ItemText.Name(item), $"강화 +{item.Upgrade}/{Item.MaxUpgrade} · 강화 정수 {have}개",
                $"모루: {ItemText.Name(item)} · 강화 정수 {have}개", -1, 0, tooltip: ItemTooltip(item));
            var enabled = GUI.enabled;
            var height = GUILayout.Height(ButtonHeight);
            if (World.UpgradeCost(item, loot) is var (cost, metal, metalCount))
            {
                bool can = have >= cost && (metal == null || _client.Bag.CountOf(metal) >= metalCount);
                string needs = metal == null ? $"정수 {cost}" : $"정수 {cost}, {metal.Name} {metalCount}";
                GUI.enabled = enabled && can;
                if (Ui.LayoutButton($"강화 +{item.Upgrade + 1} ({needs})", Ui.Look.Primary, height)) Send(_client.RefineCommand(InventoryCommand.Upgrade, item.Id), Sound.Craft);
                GUI.enabled = enabled;
            }
            else
            {
                GUILayout.Label(Fit($"최대 강화(+{Item.MaxUpgrade})", _textWidth));
            }
            // Older gear of the same slot upgraded further: its steps can move onto this one (World.TransferUpgrade).
            var source = World.UpgradeSource(item, _client.Equipment.Items.Concat(_client.Bag.Items));
            if (source != null)
            {
                GUI.enabled = enabled && have >= World.TransferEssence;
                if (Ui.LayoutButton($"강화 옮기기: {source.Base.Name}의 +{source.Upgrade}를 여기로 (정수 {World.TransferEssence})", height))
                    Send(_client.RefineCommand(InventoryCommand.TransferUpgrade, item.Id), Sound.Craft);
                GUI.enabled = enabled;
            }
            if (worn == null && Ui.LayoutButton($"분해 (정수 +{World.SalvageValue(item)})", Ui.Look.Danger, height)) Send(_client.RefineCommand(InventoryCommand.Salvage, item.Id), Sound.Break);
            if (item.Sockets.Any(r => r != null))
            {
                GUI.enabled = enabled && have >= World.UnsocketEssence;
                if (Ui.LayoutButton($"룬 빼기 (정수 {World.UnsocketEssence})", height)) Send(_client.RefineCommand(InventoryCommand.Unsocket, item.Id), Sound.Click);
                GUI.enabled = enabled;
            }
            int reroll = World.RerollCost(item);
            if (reroll > 0)
            {
                GUI.enabled = enabled && have >= reroll;
                if (Ui.LayoutButton($"접사 다시 (정수 {reroll})", height)) Send(_client.RefineCommand(InventoryCommand.Reroll, item.Id), Sound.Craft);
                GUI.enabled = enabled;
            }
        }

        // The worn pieces, then the carried ones, as squares; picking one works it at the anvil. The picked piece's name and
        // [다듬기] under them.
        void AnvilGear()
        {
            float cell = Controls.UsingTouch ? 48 : 44;
            GUILayout.Label(Fit("착용", _textWidth));
            var worn = _client.Equipment.Items.ToList();
            if (worn.Count > 0)
            {
                var grid = Grid(worn.Count, cell);
                for (int i = 0; i < worn.Count; i++)
                    if (Cell(CellAt(grid, i, cell), Sprites.Icon(worn[i].Base), ItemText.Name(worn[i]), worn[i].Upgrade > 0 ? $"+{worn[i].Upgrade}" : null, Palette.Of(worn[i].Rarity),
                        _picked == worn[i].Id, ItemTooltip(worn[i]))) PickForAnvil(worn[i]);
            }
            GUILayout.Space(4);
            var items = _client.Bag.Items.ToList();
            GUILayout.Label(Fit($"소지품 {items.Count}/{Inventory.Capacity}", _textWidth));
            if (items.Count > 0)
            {
                var grid = Grid(items.Count, cell);
                for (int i = 0; i < items.Count; i++)
                    if (Cell(CellAt(grid, i, cell), Sprites.Icon(items[i].Base), ItemText.Name(items[i]), items[i].Upgrade > 0 ? $"+{items[i].Upgrade}" : null, Palette.Of(items[i].Rarity),
                        _picked == items[i].Id, ItemTooltip(items[i]))) PickForAnvil(items[i]);
            }
            else GUILayout.Label(Fit("가방에 장비가 없다", _textWidth));
            var picked = worn.Concat(items).FirstOrDefault(i => i.Id == _picked);
            if (picked == null) return;
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            Icon(Sprites.Icon(picked.Base));
            var previous = GUI.color;
            GUI.color = Palette.Of(picked.Rarity);
            GUILayout.Label(Fit(ItemText.Name(picked), _textWidth - IconRoom - 64), GUILayout.Width(_textWidth - IconRoom - 64));
            GUI.color = previous;
            RefineButton(picked);
            GUILayout.EndHorizontal();
        }

        void PickForAnvil(Item item)
        {
            Pick(item.Id, null);
            _refining = item.Id;
        }
    }
}
