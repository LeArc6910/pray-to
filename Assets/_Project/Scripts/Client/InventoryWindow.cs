using System;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bag screen, opened with Tab or I, in tabs: gear (stats, consumables, worn gear, carried items, runes),
    // materials (materials with [놓기]/[심기], and the crafting list), and while the player stands by a chest, the
    // chest (what it holds with [꺼내기], what the bag holds with [넣기]). Every button only asks the host for the
    // change; the window redraws from the next inventory message.
    sealed class InventoryWindow
    {
        enum Page
        {
            Gear,
            Materials,
            Chest,
        }

        const float MaxWidth = 520;
        // Room for the title line and the tabs above the scrolling part.
        const float HeaderHeight = 78;
        const float TabHeight = 40;
        const float TooltipWidth = 320;
        // Narrower than this beside the minimap column, the window goes under it instead.
        const float MinWidth = 300;
        // Points one notch of the mouse wheel scrolls; IMGUI's own step is a fraction of a line in browsers.
        const float WheelStep = 30;

        static GUIStyle _tooltipStyle;
        static readonly string[] PageNames = { "장비", "재료·제작", "상자" };

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        readonly Vector2[] _scroll = new Vector2[PageNames.Length];
        Page _page;
        RuneDefinition _selectedRune;
        float _nameWidth;
        float _recipeWidth;
        // Each row starts with the thing's icon.
        const float IconSize = 24;
        const float IconRoom = IconSize + 4;

        public InventoryWindow(WorldClient client, Action<byte[]> send)
        {
            _client = client;
            _send = send;
        }

        public bool Open { get; set; }

        public void Draw()
        {
            if (!Open) return;
            if (_selectedRune != null && _client.Bag.CountOf(_selectedRune) == 0) _selectedRune = null;

            // Left of the minimap column, full height; on narrow screens under the column. The page scrolls when it
            // does not fit.
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
            bool besideWindow = area.x >= TooltipWidth + 20;
            _nameWidth = Mathf.Min(240, area.width - 200 - IconRoom);
            _recipeWidth = area.width - 110 - IconRoom;
            Controls.Block(area);
            Wheel(area);
            // The panel (UiSkin) is all but opaque, so names drawn over the world do not show through the list.
            GUILayout.BeginArea(area, GUI.skin.box);
            string close = Controls.UsingTouch ? "가방 버튼으로 닫기" : "Tab 또는 I로 닫기";
            // Without room beside the window the hint goes in the title, so nothing sits over the list.
            string hint = besideWindow ? "" : Controls.UsingTouch ? " · 이름을 누르고 있으면 설명" : " · 이름에 마우스를 올리면 설명";
            GUILayout.Label($"가방 ({close}){hint}");
            // The chest tab is there only while a chest is in reach.
            bool chest = _client.Chest.IsOpen;
            if (!chest && _page == Page.Chest) _page = Page.Gear;
            _page = (Page)GUILayout.Toolbar((int)_page, chest ? PageNames : PageNames.Take(2).ToArray(), GUILayout.Height(TabHeight));
            int page = (int)_page;
            _scroll[page] = GUILayout.BeginScrollView(_scroll[page], GUILayout.Height(area.height - HeaderHeight));
            if (_page == Page.Gear) DrawGear();
            else if (_page == Page.Materials) DrawMaterialsPage();
            else DrawChest();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawTooltip(area, besideWindow);
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

        void DrawGear()
        {
            DrawStats();
            DrawConsumables();

            GUILayout.Space(6);
            GUILayout.Label("착용");
            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
            {
                var item = _client.Equipment[slot];
                GUILayout.BeginHorizontal();
                GUILayout.Label(ItemText.SlotName(slot), GUILayout.Width(50));
                if (item == null)
                {
                    GUILayout.Label("-");
                }
                else
                {
                    ItemLabel(item);
                    RuneButton(item);
                    if (GUILayout.Button("벗기", GUILayout.Width(56))) Send(_client.UnequipCommand(slot), Sound.Click);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label($"소지품 {_client.Bag.Items.Count}/{Inventory.Capacity}");
            foreach (var item in _client.Bag.Items.ToList())
            {
                GUILayout.BeginHorizontal();
                ItemLabel(item);
                RuneButton(item);
                if (GUILayout.Button("장착", GUILayout.Width(56))) Send(_client.EquipCommand(item.Id), Sound.Place);
                if (GUILayout.Button("버리기", GUILayout.Width(56))) Send(_client.DiscardCommand(item.Id), Sound.Click);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("룬");
            GUILayout.BeginHorizontal();
            foreach (var pair in _client.Bag.Runes)
            {
                var previous = GUI.color;
                GUI.color = pair.Key == _selectedRune ? Palette.Rune : previous;
                var tooltip = string.Join("\n", new[] { SlotCategory.Weapon, SlotCategory.Armor, SlotCategory.Jewelry }
                    .Select(c => $"{CategoryName(c)}: {string.Join(", ", pair.Key.ModifiersFor(c).Select(StatText.Describe))}"));
                if (GUILayout.Button(new GUIContent($"{pair.Key.Name} ×{pair.Value}", tooltip)))
                {
                    _selectedRune = pair.Key == _selectedRune ? null : pair.Key;
                }
                GUI.color = previous;
            }
            GUILayout.EndHorizontal();
            if (_selectedRune != null) GUILayout.Label($"{_selectedRune.Name}: 넣을 아이템의 [끼우기]를 누르세요. 한 번 넣은 룬은 뺄 수 없습니다.");
        }

        void DrawMaterialsPage()
        {
            DrawMaterials();
            DrawCrafting();
        }

        // The hovered item's description: in a box beside the window, where the hint shows when nothing is hovered;
        // without room there, over the window's bottom edge, and only while something is hovered or held. Drawn
        // outside the layout so its changing height never shifts the window's controls.
        static void DrawTooltip(Rect window, bool besideWindow)
        {
            bool hovering = !string.IsNullOrEmpty(GUI.tooltip);
            if (!hovering && !besideWindow) return;
            string text = hovering ? GUI.tooltip
                : Controls.UsingTouch ? "아이템 이름을 누르고 있으면 설명이 보입니다." : "아이템 이름에 마우스를 올리면 설명이 보입니다.";
            var content = new GUIContent(text);
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            // As wide as the longest line needs (CalcSize ignores wrapping), so short hints do not wrap their last word.
            float room = besideWindow ? TooltipWidth : window.width - 20;
            float width = Mathf.Min(room, Mathf.Ceil(_tooltipStyle.CalcSize(content).x) + 2);
            float height = _tooltipStyle.CalcHeight(content, width);
            var rect = besideWindow
                ? new Rect(window.x - width - 10, Mathf.Max(window.y, Ui.HudArea.yMax + 10), width, height)
                : new Rect(window.x + 10, window.yMax - height - 10, width, height);
            GUI.Box(rect, content, _tooltipStyle);
        }

        // With the classes, what each damaging skill would hit for now: the same tag rules as the host, so swapping
        // gear or starting a buff shows its effect on every skill.
        void DrawStats()
        {
            var stats = _client.Stats;
            GUILayout.Label(
                $"생명력 {stats.MaxHealth:0} · 재생 {stats.HealthRegen:0.#}/초 · 방어도 {stats.Armor:0} · 이동 {stats.MoveSpeed:0.#}\n" +
                $"{stats.Attack.Name} 피해 {stats.Damage:0.#} · 초당 공격 {stats.AttacksPerSecond:0.##} · 치명타 {stats.CritChance:0.#}% (×{stats.CritDamage:0}%) · 아이템 희귀도 +{stats.ItemRarity:0}%");
            if (_client.Classes.Count == 0) return;
            var hits = _client.Skills
                .Where(skill => skill.Damage > 0)
                .Select(skill => $"{skill.Name} {stats.DamageFor(stats.ContextFor(skill)) * skill.Damage / 100f:0.#}");
            GUILayout.Label($"클래스 {string.Join(" + ", _client.Classes.Select(c => c.Name))} · 스킬 피해 {string.Join(" · ", hits)}");
        }

        // Materials, counted by kind. One that builds gets [놓기] and a seed gets [심기]; both act on the tile the
        // player faces.
        void DrawMaterials()
        {
            GUILayout.Label("재료");
            if (_client.Bag.Materials.Count == 0) GUILayout.Label("아직 없다. 벽을 캐거나 몬스터를 잡으면 모인다.");
            foreach (var pair in _client.Bag.Materials.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(new GUIContent($"{pair.Key.Name} ×{pair.Value}", pair.Key.Description), GUILayout.Width(_nameWidth));
                if (pair.Key.Places != null && GUILayout.Button("놓기", GUILayout.Width(56))) Send(_client.PlaceCommand(pair.Key), Sound.Place);
                var crop = _client.CropFrom(pair.Key);
                if (crop != null && GUILayout.Button("심기", GUILayout.Width(56))) Send(_client.PlantCommand(crop), Sound.Place);
                GUILayout.EndHorizontal();
            }
        }

        // Both sides of the chest: what it holds, each with [꺼내기], and what the bag holds, each with [넣기]. Stacks
        // move whole.
        void DrawChest()
        {
            var chest = _client.Chest.Contents;
            GUILayout.Label($"상자 ({chest.Items.Count}/{Inventory.Capacity})");
            DrawContents(chest, "꺼내기", item => _client.TakeItemCommand(item.Id), (kind, definition) => _client.TakeStackCommand(kind, definition));
            GUILayout.Space(8);
            GUILayout.Label($"가방 ({_client.Bag.Items.Count}/{Inventory.Capacity})");
            DrawContents(_client.Bag, "넣기", item => _client.StoreItemCommand(item.Id), (kind, definition) => _client.StoreStackCommand(kind, definition));
        }

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
                if (GUILayout.Button(action, GUILayout.Width(64))) Send(moveItem(item), Sound.Click);
                GUILayout.EndHorizontal();
            }
            void Stack(StackKind kind, object definition, Sprite icon, string name, int count, string description)
            {
                GUILayout.BeginHorizontal();
                Icon(icon);
                GUILayout.Label(new GUIContent($"{name} ×{count}", description), GUILayout.Width(_nameWidth));
                if (GUILayout.Button(action, GUILayout.Width(64))) Send(moveStack(kind, definition), Sound.Click);
                GUILayout.EndHorizontal();
            }
            foreach (var pair in contents.Consumables.ToList()) Stack(StackKind.Consumable, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Materials.ToList()) Stack(StackKind.Material, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Runes.ToList()) Stack(StackKind.Rune, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, "");
        }

        // Every recipe, with what it takes against what the bag holds, and the station it needs when that is not in
        // reach. [만들기] only works with everything at hand.
        void DrawCrafting()
        {
            var recipes = _client.Recipes;
            if (recipes.Count == 0) return;
            GUILayout.Space(6);
            GUILayout.Label("제작");
            var enabled = GUI.enabled;
            foreach (var recipe in recipes)
            {
                string needs = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {_client.Bag.CountOf(i.Material)}/{i.Count}"));
                var station = _client.MissingStation(recipe);
                if (station != null) needs += $" · {StationName(station.Value)} 필요";
                string name = recipe.Count > 1 ? $"{recipe.Name} ×{recipe.Count}" : recipe.Name;
                GUILayout.BeginHorizontal();
                Icon(recipe.Item != null ? Sprites.Icon(recipe.Item) : recipe.Consumable != null ? Sprites.Icon(recipe.Consumable) : Sprites.Icon(recipe.Material));
                GUILayout.Label(new GUIContent($"{name}\n{needs}", RecipeTooltip(recipe)), GUILayout.Width(_recipeWidth));
                GUI.enabled = enabled && _client.CanCraft(recipe);
                if (GUILayout.Button("만들기", GUILayout.Width(64))) Send(_client.CraftCommand(recipe), Sound.Craft);
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
        }

        static string StationName(TileType station) => station switch
        {
            TileType.Workbench => "작업대",
            TileType.Furnace => "화로",
            _ => station.ToString(),
        };

        static string RecipeTooltip(Recipe recipe)
        {
            if (recipe.Item != null) return string.Join("\n", recipe.Item.Implicits.Select(StatText.Describe));
            return recipe.Consumable?.Description ?? recipe.Material.Description;
        }

        void DrawConsumables()
        {
            if (_client.Bag.Consumables.Count == 0) return;
            GUILayout.Space(6);
            float wait = _client.ConsumableCooldownLeft;
            GUILayout.Label(wait > 0 ? $"소모품 (다시 마시기까지 {Mathf.CeilToInt(wait)}초)" : "소모품");
            foreach (var pair in _client.Bag.Consumables.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(new GUIContent($"{pair.Key.Name} ×{pair.Value}", pair.Key.Description), GUILayout.Width(_nameWidth));
                if (GUILayout.Button("마시기", GUILayout.Width(56)))
                {
                    byte[] command = _client.UseConsumableCommand(pair.Key);
                    if (command != null) _send(command);
                }
                GUILayout.EndHorizontal();
            }
        }

        void ItemLabel(Item item)
        {
            Icon(Sprites.Icon(item.Base));
            var previous = GUI.color;
            GUI.color = Palette.Of(item.Rarity);
            string sockets = item.Sockets.Count == 0 ? "" : " " + string.Concat(item.Sockets.Select(r => r == null ? "○" : "●"));
            GUILayout.Label(new GUIContent(ItemText.Name(item) + sockets, string.Join("\n", ItemText.Describe(item))), GUILayout.Width(_nameWidth));
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

        // Shown while a rune is picked and the item still has room for it.
        void RuneButton(Item item)
        {
            if (_selectedRune == null || !item.Sockets.Contains(null)) return;
            if (GUILayout.Button("끼우기", GUILayout.Width(56))) Send(_client.InsertRuneCommand(item.Id, _selectedRune), Sound.Craft);
        }

        static string CategoryName(SlotCategory category) => category switch
        {
            SlotCategory.Weapon => "무기",
            SlotCategory.Armor => "방어구",
            _ => "장신구",
        };
    }
}
