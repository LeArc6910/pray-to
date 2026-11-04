using System;
using System.Collections.Generic;
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
            Classes,
            Passives,
            Chest,
            Merchant,
        }

        const float MaxWidth = 520;
        const float TabHeight = 40;
        const float TooltipWidth = 320;
        // Narrower than this beside the minimap column, the window goes under it instead.
        const float MinWidth = 300;
        // Points one notch of the mouse wheel scrolls; IMGUI's own step is a fraction of a line in browsers.
        const float WheelStep = 30;

        static GUIStyle _tooltipStyle;
        static readonly string[] PageNames = { "장비", "재료·제작", "클래스", "특성", "상자", "상인" };
        static readonly GUIContent[] PageLabels = PageNames.Select(name => new GUIContent(name)).ToArray();

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        readonly Hotbar _hotbar;
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

        public InventoryWindow(WorldClient client, Action<byte[]> send, Hotbar hotbar = null)
        {
            _client = client;
            _send = send;
            _hotbar = hotbar;
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
            // Under the HUD where the two would overlap. While the bag is open the HUD is only its health bar.
            var hud = Ui.HudArea;
            if (area.x < hud.xMax + 10 && area.y < hud.yMax + 10) area = Rect.MinMaxRect(area.x, hud.yMax + 10, area.xMax, area.yMax);
            bool besideWindow = area.x >= TooltipWidth + 20;
            float inner = area.width - GUI.skin.box.padding.horizontal;
            float scrollbar = GUI.skin.verticalScrollbar.fixedWidth + 8;
            // A label in the list is that wide less its own margins.
            _textWidth = inner - scrollbar - GUI.skin.label.margin.horizontal;
            // Room for two buttons and [퀵] beside each name, and for [끼우기] while a rune is picked and [다듬기] by
            // an anvil: without that room a row ran past the window, its [퀵] cut off.
            float extra = (_selectedRune != null ? ExtraButton : 0) + (_client.NearAnvil ? ExtraButton : 0);
            _nameWidth = Mathf.Clamp(_textWidth - IconRoom - 150 - QuickWidth - extra, 60, 240);
            _recipeWidth = _textWidth - IconRoom - 120;
            Controls.Block(area);
            Wheel(area);
            Ui.TouchScroll(area, ref _scroll[(int)_page]);
            GUILayout.BeginArea(area, GUI.skin.box);
            string close = Controls.UsingTouch ? "가방 버튼으로 닫기" : "Tab 또는 I로 닫기";
            // Without room beside the window the hint goes in the title, so nothing sits over the list.
            string hint = besideWindow ? "" : Controls.UsingTouch ? " · 이름을 누르고 있으면 설명" : " · 이름에 마우스를 올리면 설명";
            GUILayout.Label(Fit($"가방 ({close}){hint}", inner));
            // The chest tab is there only while a chest is in reach, the merchant's while she is.
            bool chest = _client.Chest.IsOpen;
            var person = _client.NpcNear();
            bool merchant = person != null;
            // The tab is named for whoever is near: 상인, 대장장이, 어부.
            if (merchant) PageLabels[(int)Page.Merchant].text = person.Value.Npc.Role;
            if (!chest && _page == Page.Chest || !merchant && _page == Page.Merchant) _page = Page.Gear;
            _pages.Clear();
            _pages.AddRange(new[] { Page.Gear, Page.Materials, Page.Classes, Page.Passives });
            if (chest) _pages.Add(Page.Chest);
            if (merchant) _pages.Add(Page.Merchant);
            _page = Tabs(_page, _pages);
            if (_hotbar != null && _hotbar.ShowsMessage) GUILayout.Label(Fit(_hotbar.Message, inner));
            int page = (int)_page;
            // The list takes whatever height the title and tabs leave, so it never runs past the frame.
            _scroll[page] = Ui.BeginScrollView(_scroll[page], GUILayout.ExpandHeight(true));
            if (_page == Page.Gear) DrawGear();
            else if (_page == Page.Materials) DrawMaterialsPage();
            else if (_page == Page.Classes) DrawClasses();
            else if (_page == Page.Passives) DrawPassives();
            else if (_page == Page.Merchant) DrawMerchant();
            else DrawChest();
            Ui.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                var rows = GUILayoutUtility.GetLastRect();
                _rows = new Rect(area.x + rows.x, area.y + rows.y, rows.width, rows.height);
            }
            GUILayout.EndArea();
            DrawTooltip(area, besideWindow);
        }

        // Tabs that switch as soon as they are pressed. IMGUI's toolbar switches on release, and in the browser its
        // first click after the bag opened was sometimes lost (LOCAL_TASKS.md, the 2026-10-31 UI check).
        readonly List<Page> _pages = new List<Page>();

        Page Tabs(Page page, IReadOnlyList<Page> pages)
        {
            int count = pages.Count;
            var row = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.button, GUILayout.Height(TabHeight), GUILayout.ExpandWidth(true));
            const float gap = 4;
            float width = (row.width - gap * (count - 1)) / count;
            var e = Event.current;
            for (int i = 0; i < count; i++)
            {
                var tab = new Rect(row.x + i * (width + gap), row.y, width, row.height);
                if (e.type == EventType.MouseDown && tab.Contains(e.mousePosition))
                {
                    if (page != pages[i])
                    {
                        page = pages[i];
                        // A description from the old tab must not stay up over the new one.
                        GUI.tooltip = "";
                        Sounds.Play(Sound.Click);
                    }
                    e.Use();
                }
                Ui.Probe(PageLabels[(int)pages[i]].text, tab);
                if (e.type == EventType.Repaint) GUI.skin.button.Draw(tab, PageLabels[(int)pages[i]], tab.Contains(e.mousePosition), false, page == pages[i], false);
            }
            return page;
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

        void DrawGear()
        {
            DrawRefining();
            DrawStats();
            DrawLifeSkills();
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
                    RefineButton(item);
                    if (Ui.LayoutButton("벗기", GUILayout.Width(56))) Send(_client.UnequipCommand(slot), Sound.Click);
                    QuickButton(_hotbar?.SlotOf(item), () => _hotbar.Toggle(item));
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
                RefineButton(item);
                // Too high a level to wear yet: the button says which level instead.
                bool wearable = item.Base.RequiredLevel <= _client.Level;
                GUI.enabled = wearable;
                if (Ui.LayoutButton(wearable ? "장착" : $"Lv{item.Base.RequiredLevel}", GUILayout.Width(56))) Send(_client.EquipCommand(item.Id), Sound.Place);
                GUI.enabled = true;
                if (Ui.LayoutButton("버리기", GUILayout.Width(56))) Send(_client.DiscardCommand(item.Id), Sound.Click);
                QuickButton(_hotbar?.SlotOf(item), () => _hotbar.Toggle(item));
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
                if (Ui.LayoutButton(new GUIContent($"{pair.Key.Name} ×{pair.Value}", tooltip)))
                {
                    _selectedRune = pair.Key == _selectedRune ? null : pair.Key;
                }
                GUI.color = previous;
            }
            GUILayout.EndHorizontal();
            if (_selectedRune != null) GUILayout.Label(Fit($"{_selectedRune.Name}: 넣을 아이템의 [끼우기]를 누르세요. 넣은 룬은 모루에서 정수를 들여야 뺄 수 있습니다.", _textWidth));
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
            TileType.Assembler => machine.Recipe != null && machine.Recipe.Ingredients.Any(i => i.Material == material.Kind),
            TileType.Coop or TileType.Barn => material.Id == World.HouseOf(machine.Kind).Animal || material.Id == World.Feed,
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
            bool hovering = !string.IsNullOrEmpty(GUI.tooltip) && _rows.Contains(pointer);
            if (!hovering && !besideWindow) return;
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            string text = hovering ? GUI.tooltip
                : Controls.UsingTouch ? "아이템 이름을 누르고 있으면 설명이 보입니다." : "아이템 이름에 마우스를 올리면 설명이 보입니다.";
            float room = Mathf.Min(TooltipWidth, Ui.Width - 20);
            var content = new GUIContent(Ui.Wrap(text, _tooltipStyle, room));
            // As wide as the longest line needs, so short descriptions get a small box.
            float width = Mathf.Min(room, Mathf.Ceil(_tooltipStyle.CalcSize(content).x) + 2);
            float height = _tooltipStyle.CalcHeight(content, width);
            float x;
            float y;
            if (besideWindow)
            {
                x = window.x - width - 10;
                y = hovering ? pointer.y - 12 : Mathf.Max(window.y, Ui.HudArea.yMax + 10);
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
            GUI.Box(new Rect(x, y, width, height), content, _tooltipStyle);
        }

        // With the classes, what each damaging skill would hit for now: the same tag rules as the host, so swapping
        // gear or starting a buff shows its effect on every skill.
        void DrawStats()
        {
            var stats = _client.Stats;
            GUILayout.Label(Fit(
                $"생명력 {stats.MaxHealth:0} · 재생 {stats.HealthRegen:0.#}/초 · 마나 {stats.MaxMana:0} · 재생 {stats.ManaRegen:0.#}/초 · 방어도 {stats.Armor:0} · 이동 {stats.MoveSpeed:0.#}\n" +
                $"{stats.Attack.Name} 피해 {stats.Damage:0.#} · 초당 공격 {stats.AttacksPerSecond:0.##} · 치명타 {stats.CritChance:0.#}% (×{stats.CritDamage:0}%) · 아이템 희귀도 +{stats.ItemRarity:0}%",
                _textWidth));
            if (_client.Classes.Count == 0) return;
            var hits = _client.Skills
                .Where(skill => skill.Damage > 0)
                .Select(skill => $"{skill.Name} {stats.DamageFor(stats.ContextFor(skill)) * skill.Damage / 100f:0.#}");
            GUILayout.Label(Fit($"클래스 {string.Join(" + ", _client.Classes.Select(c => c.Name))} · 스킬 피해 {string.Join(" · ", hits)}", _textWidth));
        }

        // Each life skill's level, how far to the next, and what it gives (World.Life.cs).
        void DrawLifeSkills()
        {
            GUILayout.Space(6);
            GUILayout.Label("생활 기술");
            foreach (LifeSkill skill in Enum.GetValues(typeof(LifeSkill)))
            {
                int experience = _client.LifeExperience[(int)skill];
                int level = LifeSkills.LevelOf(experience);
                string next = level >= LifeSkills.MaxLevel ? "최고" : $"{experience - LifeSkills.TotalFor(level)}/{LifeSkills.TotalFor(level + 1) - LifeSkills.TotalFor(level)}";
                GUILayout.Label(Fit($"{LifeSkills.Name(skill)} {level} ({next}) · {LifeSkills.Perk(skill, level)}", _textWidth));
            }
        }

        // Materials, counted by kind. One that builds gets [놓기], a seed gets [심기] and the watering can [물 주기];
        // all act on the tile the player faces.
        void DrawMaterials()
        {
            GUILayout.Label("재료");
            if (_client.Bag.Materials.Count == 0) GUILayout.Label(Fit("아직 없다. 벽을 캐거나 몬스터를 잡으면 모인다.", _textWidth));
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
                    GUILayout.Label(Fit($"닭 {machine.Animals}/{World.CoopChickens}마리. 사료가 있으면 하루(20분)마다 닭마다 달걀 하나", _textWidth));
                if (machine.Kind == TileType.Barn)
                    GUILayout.Label(Fit($"소 {machine.Animals}/{World.HouseOf(TileType.Barn).Most}마리. 사료 둘이면 하루(20분)마다 소마다 우유 하나", _textWidth));
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
            foreach (var pair in _client.Bag.Materials.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(new GUIContent(Fit($"{pair.Key.Name} ×{pair.Value}", _nameWidth), pair.Key.Description), GUILayout.Width(_nameWidth));
                if (pair.Key.Places != null && Ui.LayoutButton("놓기", GUILayout.Width(56))) Send(_client.PlaceCommand(pair.Key), Sound.Place);
                var crop = _client.CropFrom(pair.Key);
                if (crop != null && Ui.LayoutButton("심기", GUILayout.Width(56))) Send(_client.PlantCommand(crop), Sound.Place);
                if (pair.Key.Id == World.WateringCan && Ui.LayoutButton("물 주기", GUILayout.Width(64))) Send(_client.WaterCommand(), Sound.Water);
                if (pair.Key.Id == World.FishingRod && Ui.LayoutButton(FishingLabel(_client), GUILayout.Width(72))) Send(_client.FishCommand(), Sound.Cast);
                if (pair.Key.Id == World.Hoe && Ui.LayoutButton("밭 갈기", GUILayout.Width(64))) Send(_client.TillCommand(), Sound.Dig);
                // Facing a filter, any material can be what it picks out; facing a generator, fuel goes in.
                if (ahead?.Kind == TileType.Filter && Ui.LayoutButton(ahead.Value.Settings.Contains(pair.Key.Kind) ? "빼기" : "거르기", GUILayout.Width(56)))
                    Send(_client.SetFilterCommand(pair.Key), Sound.Click);
                if (ahead?.Kind == TileType.Generator && pair.Key.Fuel > 0 && Ui.LayoutButton("연료", GUILayout.Width(48))) Send(_client.RefuelCommand(pair.Key), Sound.Place);
                // Facing a machine that makes things from this, it goes in (all of it, up to what the machine holds).
                if (loads && Takes(ahead.Value, pair.Key) && Ui.LayoutButton("넣기", GUILayout.Width(48)))
                    Send(_client.LoadCommand(pair.Key, Math.Min(pair.Value, World.MaxTrade)), Sound.Place);
                if (pair.Key.Id == World.Fertilizer && Ui.LayoutButton("거름 주기", GUILayout.Width(72))) Send(_client.FertilizeCommand(), Sound.Place);
                // Only what a quick slot can use: things that build, seeds and the tools.
                if (pair.Key.Places != null || crop != null || IsTool(pair.Key))
                {
                    var material = pair.Key;
                    QuickButton(_hotbar?.SlotOf(material), () => _hotbar.Toggle(material));
                }
                GUILayout.EndHorizontal();
            }
        }

        // Both sides of the chest: what it holds, each with [꺼내기], and what the bag holds, each with [넣기]. Stacks
        // move whole.
        enum ShopMode
        {
            Buy,
            Sell,
            Gift,
            Request,
        }

        ShopMode _shopMode;
        static readonly string[] ShopModeNames = { "사기", "팔기", "선물", "부탁" };

        // The merchant (World.Merchant.cs): who she is and how well this player knows her, today's line, a chat once a
        // day, and buying, selling and a gift once a day.
        void DrawMerchant()
        {
            var near = _client.NpcNear();
            if (near == null) return;
            var npc = near.Value.Npc;
            int hearts = _client.HeartsWith(npc);
            var coin = _client.Bag.Materials.Keys.FirstOrDefault(m => m.Id == World.Coin);
            int coins = coin == null ? 0 : _client.Bag.CountOf(coin);
            GUILayout.Label(Fit($"{npc.Name} · 호감 {hearts}/{World.MaxHearts} ({_client.PointsWith(npc)}/{World.MaxHearts * World.PointsPerHeart}) · 금화 {coins}", _textWidth));
            GUILayout.Label(Fit($"\"{npc.LineFor(_client.Today, hearts)}\"", _textWidth));
            // The stories told with each heart gift, once had.
            foreach (var story in npc.Stories.OrderBy(p => p.Key))
            {
                if (_client.HadHeartGift(npc, story.Key)) GUILayout.Label(Fit($"(호감 {story.Key}) \"{story.Value}\"", _textWidth));
            }
            GUILayout.BeginHorizontal();
            var enabled = GUI.enabled;
            GUI.enabled = enabled && !_client.TalkedToday(npc);
            if (Ui.LayoutButton(_client.TalkedToday(npc) ? "오늘은 이야기했다" : "말 걸기")) Send(_client.TalkCommand(), Sound.Click);
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            GUILayout.Label(Fit($"호감이 오를 때마다 사는 값은 {World.HeartPercent}% 싸지고 파는 값은 {World.HeartPercent}% 비싸진다.", _textWidth));
            int mode = Ui.Toolbar((int)_shopMode, ShopModeNames);
            if (mode != (int)_shopMode)
            {
                _shopMode = (ShopMode)mode;
                Sounds.Play(Sound.Click);
            }
            GUILayout.Space(4);
            if (_shopMode == ShopMode.Buy) DrawBuying(npc, hearts, coins);
            else if (_shopMode == ShopMode.Sell) DrawSelling(npc, hearts);
            else if (_shopMode == ShopMode.Gift) DrawGifts(npc);
            else DrawRequest(npc);
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

        void DrawBuying(NpcDefinition npc, int hearts, int coins)
        {
            var enabled = GUI.enabled;
            foreach (var entry in _client.ShopTodayOf(npc))
            {
                GUILayout.BeginHorizontal();
                Icon(entry.Material != null ? Sprites.Icon(entry.Material) : Sprites.Icon(entry.Consumable));
                int price = World.BuyPrice(entry, hearts, Calendar.Day(_client.Clock));
                string note = entry.Daily ? " (오늘만)" : "";
                bool open = hearts >= entry.Hearts;
                string text = open ? $"{entry.Name}{note} · 금화 {price}" : $"{entry.Name} · 호감 {entry.Hearts}에 열린다";
                GUILayout.Label(new GUIContent(Fit(text, _recipeWidth), entry.Material?.Description ?? entry.Consumable.Description), GUILayout.Width(_recipeWidth));
                GUI.enabled = enabled && open && coins >= price;
                if (Ui.LayoutButton("사기", GUILayout.Width(56))) Send(_client.BuyCommand(entry, 1), Sound.Craft);
                GUI.enabled = enabled && open && coins >= price * 5;
                if (Ui.LayoutButton("×5", GUILayout.Width(40))) Send(_client.BuyCommand(entry, 5), Sound.Craft);
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
        }

        void DrawSelling(NpcDefinition npc, int hearts)
        {
            bool any = false;
            foreach (var entry in _client.Npcs.Shop.Where(e => e.Sell > 0 && e.Npc == npc))
            {
                // Each person takes only so many of a thing a day (World.DailySellLimit).
                int left = _client.SellLeft(entry);
                // A crop in each quality the bag holds, the better ones dearer.
                var kinds = entry.Material != null ? entry.Material.AllQualities.Cast<object>() : new object[] { entry.Consumable };
                foreach (var thing in kinds)
                {
                    var material = thing as MaterialDefinition;
                    int have = material != null ? _client.Bag.CountOf(material) : _client.Bag.CountOf(entry.Consumable);
                    if (have == 0) continue;
                    any = true;
                    GUILayout.BeginHorizontal();
                    Icon(material != null ? Sprites.Icon(material) : Sprites.Icon(entry.Consumable));
                    string name = material?.Name ?? entry.Name;
                    string room = left > 0 ? $" · 오늘 {left}개 더" : " · 오늘은 그만 산다";
                    GUILayout.Label(Fit($"{name} ×{have} · 하나에 금화 {World.SellPrice(entry, hearts, material?.Quality ?? 0, Calendar.Day(_client.Clock))}{room}", _recipeWidth), GUILayout.Width(_recipeWidth));
                    GUI.enabled = left > 0;
                    if (Ui.LayoutButton("팔기", GUILayout.Width(56))) Send(_client.SellCommand(thing, 1), Sound.Pickup);
                    if (Ui.LayoutButton("모두", GUILayout.Width(48))) Send(_client.SellCommand(thing, Math.Min(Math.Min(have, left), World.MaxTrade)), Sound.Pickup);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            foreach (var item in npc.BuysGear ? _client.Bag.Items.ToList() : new List<Item>())
            {
                any = true;
                GUILayout.BeginHorizontal();
                ItemLabel(item);
                if (Ui.LayoutButton($"팔기 {World.ItemValue(item, hearts)}", GUILayout.Width(88))) Send(_client.SellItemCommand(item), Sound.Pickup);
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label(Fit(npc.BuysGear ? "팔 것이 없다. 입은 장비는 벗어야 판다." : "이 사람이 사는 것이 가방에 없다.", _textWidth));
        }

        void DrawGifts(NpcDefinition npc)
        {
            if (_client.GiftedToday(npc))
            {
                GUILayout.Label(Fit("오늘은 선물을 주었다. 내일 또 줄 수 있다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit("하루에 하나. 좋아하는 것을 주면 호감이 크게 오른다.", _textWidth));
            foreach (var pair in _client.Bag.Consumables.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(Fit($"{pair.Key.Name} ×{pair.Value}", _recipeWidth), GUILayout.Width(_recipeWidth));
                if (Ui.LayoutButton("선물", GUILayout.Width(56))) Send(_client.GiftCommand(pair.Key), Sound.Pop);
                GUILayout.EndHorizontal();
            }
            foreach (var pair in _client.Bag.Materials.Where(p => p.Key.Id != World.Coin).ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(Fit($"{pair.Key.Name} ×{pair.Value}", _recipeWidth), GUILayout.Width(_recipeWidth));
                if (Ui.LayoutButton("선물", GUILayout.Width(56))) Send(_client.GiftCommand(pair.Key), Sound.Pop);
                GUILayout.EndHorizontal();
            }
        }

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
                if (Ui.LayoutButton(action, GUILayout.Width(64))) Send(moveItem(item), Sound.Click);
                GUILayout.EndHorizontal();
            }
            void Stack(StackKind kind, object definition, Sprite icon, string name, int count, string description)
            {
                GUILayout.BeginHorizontal();
                Icon(icon);
                GUILayout.Label(new GUIContent(Fit($"{name} ×{count}", _nameWidth), description), GUILayout.Width(_nameWidth));
                if (Ui.LayoutButton(action, GUILayout.Width(64))) Send(moveStack(kind, definition), Sound.Click);
                GUILayout.EndHorizontal();
            }
            foreach (var pair in contents.Consumables.ToList()) Stack(StackKind.Consumable, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Materials.ToList()) Stack(StackKind.Material, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, pair.Key.Description);
            foreach (var pair in contents.Runes.ToList()) Stack(StackKind.Rune, pair.Key, Sprites.Icon(pair.Key), pair.Key.Name, pair.Value, "");
        }

        // Every recipe, with what it takes against what the bag and the chests nearby hold, and the station it needs
        // when that is not in reach. [만들기] only works with everything at hand; [×N] makes it up to five times over.
        void DrawCrafting()
        {
            var recipes = _client.Recipes;
            if (recipes.Count == 0) return;
            GUILayout.Space(6);
            GUILayout.Label("제작");
            var enabled = GUI.enabled;
            // Facing an assembler, a recipe it can make gets [조립기에] (World.SetRecipe).
            var assembler = _client.MachineAhead() is MachineView m && m.Kind == TileType.Assembler ? m : (MachineView?)null;
            if (assembler != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Fit($"조립기: {assembler.Value.Recipe?.Name ?? "정하지 않음"} (조합법의 [조립기에]로 정한다)", _textWidth - 64));
                if (assembler.Value.Recipe != null && Ui.LayoutButton("멈추기", GUILayout.Width(56))) Send(_client.SetRecipeCommand(null), Sound.Click);
                GUILayout.EndHorizontal();
            }
            foreach (var recipe in recipes)
            {
                // A keg's or a jar's: only that machine makes it (the materials page lists them by it).
                if (_client.MadeByMachine(recipe)) continue;
                string needs = string.Join(" · ", recipe.Ingredients.Select(i => $"{i.Material.Name} {_client.Available(i.Material)}/{i.Count}"));
                var station = _client.MissingStation(recipe);
                if (station != null) needs += $" · {StationName(station.Value)} 필요";
                string name = recipe.Count > 1 ? $"{recipe.Name} ×{recipe.Count}" : recipe.Name;
                GUILayout.BeginHorizontal();
                Icon(recipe.Item != null ? Sprites.Icon(recipe.Item) : recipe.Consumable != null ? Sprites.Icon(recipe.Consumable) : Sprites.Icon(recipe.Material));
                // Facing an assembler the row has [조립기에] too: the text gives it room, or the row ran past the window
                // and the button was out of sight.
                bool toAssembler = assembler != null && World.Assembles(recipe);
                float width = toAssembler ? Mathf.Max(80, _recipeWidth - 76) : _recipeWidth;
                GUILayout.Label(new GUIContent(Fit($"{name}\n{needs}", width), RecipeTooltip(recipe)), GUILayout.Width(width));
                int times = _client.CraftableTimes(recipe);
                GUI.enabled = enabled && times > 0;
                if (Ui.LayoutButton("만들기", GUILayout.Width(64))) Send(_client.CraftCommand(recipe), Sound.Craft);
                GUI.enabled = enabled && times > 1;
                int bulk = Mathf.Clamp(times, 2, BulkCraft);
                if (Ui.LayoutButton($"×{bulk}", GUILayout.Width(40))) Send(_client.CraftCommand(recipe, bulk), Sound.Craft);
                GUI.enabled = enabled;
                if (toAssembler && Ui.LayoutButton("조립기에", GUILayout.Width(72)))
                    Send(_client.SetRecipeCommand(recipe), Sound.Click);
                GUILayout.EndHorizontal();
            }
        }

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

        void DrawConsumables()
        {
            if (_client.Bag.Consumables.Count == 0) return;
            GUILayout.Space(6);
            float wait = _client.ConsumableCooldownLeft;
            GUILayout.Label(wait > 0 ? $"소모품 (다시 쓰기까지 {Mathf.CeilToInt(wait)}초)" : "소모품");
            foreach (var pair in _client.Bag.Consumables.ToList())
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(pair.Key));
                GUILayout.Label(new GUIContent(Fit($"{pair.Key.Name} ×{pair.Value}", _nameWidth), pair.Key.Description), GUILayout.Width(_nameWidth));
                // A potion is drunk; a food is eaten, and still gives its buff at full health.
                if (Ui.LayoutButton(pair.Key.IsFood ? "먹기" : pair.Key.Recalls ? "쓰기" : "마시기", GUILayout.Width(56)))
                {
                    byte[] command = _client.UseConsumableCommand(pair.Key);
                    if (command != null) _send(command);
                }
                var consumable = pair.Key;
                QuickButton(_hotbar?.SlotOf(consumable), () => _hotbar.Toggle(consumable));
                GUILayout.EndHorizontal();
            }
        }

        // The classes picked here but not yet taken, as ids; null shows the ones the player has.
        System.Collections.Generic.List<string> _pickedClasses;
        int _pickedForKit = -1;

        // The skills in their slots, each with buttons to move it up or down (anywhere, at once), and the classes,
        // which change beside a workbench (Docs/Classes.md, 게임 중에 바꾸기).
        // The skill slot being filled from the unused skills, or -1.
        int _swapSlot = -1;

        void DrawClasses()
        {
            if (_pickedForKit != _client.KitVersion)
            {
                _pickedClasses = null;
                _pickedForKit = _client.KitVersion;
            }
            GUILayout.Label(Fit($"지금 클래스: {(_client.Classes.Count == 0 ? "없음" : string.Join(" + ", _client.Classes.Select(c => c.Name)))}", _textWidth));
            GUILayout.Space(4);
            GUILayout.Label(Fit("스킬 칸(어디서나 바꾼다). [바꾸기]를 누르고 아래에서 그 칸에 넣을 스킬을 고른다", _textWidth));
            var order = _client.SkillOrder.ToList();
            var all = ClassRules.AllSkillsFor(_client.Classes);
            if (_swapSlot >= _client.Skills.Count) _swapSlot = -1;
            for (int slot = 0; slot < _client.Skills.Count; slot++)
            {
                GUILayout.BeginHorizontal();
                string key = Controls.UsingTouch ? $"{slot + 1}" : Controls.SkillKeyName(slot);
                GUILayout.Label(Fit($"{key}  {_client.Skills[slot].Name}", _nameWidth), GUILayout.Width(_nameWidth));
                GUI.enabled = slot > 0;
                if (Ui.LayoutButton("▲", GUILayout.Width(40))) Swap(order, slot, slot - 1);
                GUI.enabled = slot < _client.Skills.Count - 1;
                if (Ui.LayoutButton("▼", GUILayout.Width(40))) Swap(order, slot, slot + 1);
                GUI.enabled = all.Count > _client.Skills.Count;
                if (Ui.LayoutButton(_swapSlot == slot ? "그만" : "바꾸기", GUILayout.Width(56))) _swapSlot = _swapSlot == slot ? -1 : slot;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            // The class skills not in a slot; with a slot picked, each can go in it.
            var unused = all.Where(s => !_client.Skills.Contains(s)).ToList();
            if (unused.Count > 0)
            {
                GUILayout.Label(Fit($"칸에 없는 스킬: {string.Join(", ", unused.Select(s => s.Name))}", _textWidth));
                if (_swapSlot >= 0)
                {
                    foreach (var skill in unused)
                    {
                        if (!Ui.LayoutButton(Ui.Wrap($"{_swapSlot + 1}번 칸에: {skill.Name} — {skill.Description}", GUI.skin.button, _textWidth))) continue;
                        order[_swapSlot] = all.IndexOf(skill);
                        _swapSlot = -1;
                        Send(_client.ClassCommand(_client.Classes, order), Sound.Click);
                    }
                }
            }

            GUILayout.Space(8);
            bool near = _client.Map != null && _client.Map.Near(_client.Position, TileType.Workbench, World.StationReach);
            GUILayout.Label(Fit(near
                ? $"클래스 바꾸기(최대 {ClassRules.DefaultMaxClasses}개, 고른 순서대로 칸을 채운다. 새 스킬은 재사용 대기부터 시작한다)"
                : "클래스는 작업대 곁에서 바꿀 수 있다", _textWidth));
            _pickedClasses ??= _client.Classes.Select(c => c.Id).ToList();
            GUI.enabled = near;
            foreach (var option in _client.AllClasses)
            {
                int picked = _pickedClasses.IndexOf(option.Id);
                bool full = picked < 0 && _pickedClasses.Count >= ClassRules.DefaultMaxClasses;
                GUI.enabled = near && !full;
                if (Ui.LayoutButton(new GUIContent((picked >= 0 ? $"[{picked + 1}] " : "[ ] ") + option.Name, option.Description)))
                {
                    if (picked >= 0) _pickedClasses.RemoveAt(picked);
                    else _pickedClasses.Add(option.Id);
                    Sounds.Play(Sound.Click);
                }
            }
            bool differs = !_pickedClasses.SequenceEqual(_client.Classes.Select(c => c.Id));
            GUI.enabled = near && differs;
            if (Ui.LayoutButton("이대로 바꾸기"))
            {
                var classes = _pickedClasses.Select(id => _client.AllClasses.First(c => c.Id == id)).ToList();
                Send(_client.ClassCommand(classes, null), Sound.Buff);
            }
            GUI.enabled = true;

            void Swap(System.Collections.Generic.List<int> list, int a, int b)
            {
                (list[a], list[b]) = (list[b], list[a]);
                Send(_client.ClassCommand(_client.Classes, list), Sound.Click);
            }
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
            GUILayout.Label(new GUIContent(Fit(ItemText.Name(item) + sockets, _nameWidth), string.Join("\n", ItemText.Describe(item))), GUILayout.Width(_nameWidth));
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

        // The passive tree (Docs/Classes.md, 패시브 트리): the points spent and left, the picked point with [배우기], the
        // tree drawn as buttons joined by dotted lines, and the points that can be taken now as big buttons too, for
        // fingers when the tree is drawn small.
        PassiveNode _pickedPassive;

        void DrawPassives()
        {
            var tree = _client.PassiveTree;
            if (tree.Count == 0)
            {
                GUILayout.Label(Fit("특성 트리 시트가 없다. 명령 한 줄로 프로젝트를 다시 설정한다.", _textWidth));
                return;
            }
            GUILayout.Label(Fit($"특성 포인트 {_client.PassivePoints}개 (레벨마다 1개). 시작점이나 배운 점과 이어진 점을 배운다.", _textWidth));
            GUILayout.BeginHorizontal();
            if (_client.Passives.Count > 0 && Ui.LayoutButton("모두 되돌리기", GUILayout.Width(110))) Send(_client.ResetPassivesCommand(), Sound.Click);
            GUILayout.EndHorizontal();

            if (_pickedPassive != null)
            {
                bool taken = _client.Passives.Contains(_pickedPassive);
                string stats = _pickedPassive.Modifiers.Count == 0 ? "" : " · " + string.Join(", ", _pickedPassive.Modifiers.Select(StatText.Describe));
                GUILayout.Label(Fit($"{_pickedPassive.Name}{(_pickedPassive.Notable ? " (큰 점)" : "")}{stats}", _textWidth));
                GUILayout.BeginHorizontal();
                GUI.enabled = _client.CanAllocate(_pickedPassive);
                if (Ui.LayoutButton(taken ? "배움" : "배우기", GUILayout.Width(80))) Send(_client.AllocatePassiveCommand(_pickedPassive), Sound.LevelUp);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            DrawPassiveTree(tree);

            var open = tree.Where(_client.CanAllocate).ToList();
            if (open.Count == 0) return;
            GUILayout.Label("지금 배울 수 있는 점");
            foreach (var node in open)
            {
                string stats = string.Join(", ", node.Modifiers.Select(StatText.Describe));
                if (Ui.LayoutButton(Ui.Wrap($"{node.Name}: {stats}", GUI.skin.button, _textWidth))) _pickedPassive = node;
            }
        }

        void DrawPassiveTree(System.Collections.Generic.IReadOnlyList<PassiveNode> tree)
        {
            int minX = tree.Min(n => n.X), maxX = tree.Max(n => n.X), minY = tree.Min(n => n.Y), maxY = tree.Max(n => n.Y);
            // Steps that fit the column, between small and roomy.
            float step = Mathf.Clamp((_textWidth - 40) / Mathf.Max(1, maxX - minX), 26, 48);
            float size = Mathf.Min(34, step - 6);
            var canvas = GUILayoutUtility.GetRect((maxX - minX) * step + size + 8, (maxY - minY) * step + size + 8, GUILayout.ExpandWidth(false));
            Vector2 At(PassiveNode n) => new Vector2(canvas.x + 4 + size / 2 + (n.X - minX) * step, canvas.y + 4 + size / 2 + (n.Y - minY) * step);
            bool Taken(PassiveNode n) => n.IsStart || _client.Passives.Contains(n);

            if (Event.current.type == EventType.Repaint)
            {
                foreach (var node in tree)
                {
                    foreach (var other in node.Links.Where(o => string.CompareOrdinal(o.Id, node.Id) > 0))
                    {
                        var color = Taken(node) && Taken(other) ? Palette.Worn : new Color(0.55f, 0.5f, 0.45f);
                        Dots(At(node), At(other), color);
                    }
                }
            }
            foreach (var node in tree)
            {
                float s = node.Notable || node.IsStart ? size : size * 0.75f;
                var center = At(node);
                var rect = new Rect(center.x - s / 2, center.y - s / 2, s, s);
                var previous = GUI.color;
                GUI.color = node == _pickedPassive ? new Color(1f, 0.6f, 0.8f)
                    : Taken(node) ? Palette.Worn
                    : _client.CanAllocate(node) ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f);
                // The start as a dot: neither game font has "★" (Galmuri11, Noto Sans KR subsets), which drew as a box.
                if (Ui.Button(rect, node.IsStart ? "●" : node.Name.Substring(0, 1))) _pickedPassive = node.IsStart ? null : node;
                GUI.color = previous;
            }
        }

        // A dotted line: small squares every few points.
        static void Dots(Vector2 from, Vector2 to, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            float length = Vector2.Distance(from, to);
            for (float d = 0; d <= length; d += 5) GUI.DrawTexture(new Rect(Vector2.Lerp(from, to, d / length) - Vector2.one, Vector2.one * 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // Beside an anvil: picks the item to work on, or puts it back.
        void RefineButton(Item item)
        {
            if (!_client.NearAnvil) return;
            if (Ui.LayoutButton(_refining == item.Id ? "닫기" : "다듬기", GUILayout.Width(56))) _refining = _refining == item.Id ? 0 : item.Id;
        }

        // At the top of the gear page while an anvil is near: what can be done to the picked item and what it costs.
        void DrawRefining()
        {
            if (!_client.NearAnvil)
            {
                _refining = 0;
                return;
            }
            var loot = _client.Loot;
            var essence = loot?.FindMaterial(World.Essence);
            int have = essence == null ? 0 : _client.Bag.CountOf(essence);
            var worn = _client.Equipment.Items.FirstOrDefault(i => i.Id == _refining);
            var item = worn ?? _client.Bag.Find(_refining);
            if (item == null)
            {
                _refining = 0;
                GUILayout.Label(Fit($"모루: 장비의 [다듬기]를 누르면 강화, 분해, 룬 빼기, 접사 다시 굴리기를 한다. 강화 정수 {have}개", _textWidth));
                GUILayout.Space(6);
                return;
            }
            GUILayout.Label(Fit($"모루: {ItemText.Name(item)} · 강화 정수 {have}개", _textWidth));
            GUILayout.BeginHorizontal();
            if (World.UpgradeCost(item, loot) is var (cost, metal, metalCount))
            {
                bool can = have >= cost && (metal == null || _client.Bag.CountOf(metal) >= metalCount);
                string needs = metal == null ? $"정수 {cost}" : $"정수 {cost}, {metal.Name} {metalCount}";
                GUI.enabled = can;
                if (Ui.LayoutButton($"강화 +{item.Upgrade + 1} ({needs})")) Send(_client.RefineCommand(InventoryCommand.Upgrade, item.Id), Sound.Craft);
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"최대 강화(+{Item.MaxUpgrade})");
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (worn == null && Ui.LayoutButton($"분해 (정수 +{World.SalvageValue(item)})")) Send(_client.RefineCommand(InventoryCommand.Salvage, item.Id), Sound.Break);
            GUI.enabled = have >= World.UnsocketEssence;
            if (item.Sockets.Any(r => r != null) && Ui.LayoutButton($"룬 빼기 (정수 {World.UnsocketEssence})"))
                Send(_client.RefineCommand(InventoryCommand.Unsocket, item.Id), Sound.Click);
            int reroll = World.RerollCost(item);
            GUI.enabled = have >= reroll;
            if (reroll > 0 && Ui.LayoutButton($"접사 다시 (정수 {reroll})")) Send(_client.RefineCommand(InventoryCommand.Reroll, item.Id), Sound.Craft);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // Shown while a rune is picked and the item still has room for it.
        void RuneButton(Item item)
        {
            if (_selectedRune == null || !item.Sockets.Contains(null)) return;
            if (Ui.LayoutButton("끼우기", GUILayout.Width(56))) Send(_client.InsertRuneCommand(item.Id, _selectedRune), Sound.Craft);
        }

        static string CategoryName(SlotCategory category) => category switch
        {
            SlotCategory.Weapon => "무기",
            SlotCategory.Armor => "방어구",
            _ => "장신구",
        };
    }
}
