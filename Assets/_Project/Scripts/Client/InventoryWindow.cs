using System;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bag screen, opened with Tab or I: stats, worn gear, carried items and runes. Every button only asks the
    // host for the change; the window redraws from the next inventory message.
    sealed class InventoryWindow
    {
        const float MaxWidth = 520;
        // Room for the title line above the scrolling part.
        const float TitleHeight = 40;
        const float TooltipWidth = 320;
        // Narrower than this beside the minimap column, the window goes under it instead.
        const float MinWidth = 300;

        static GUIStyle _tooltipStyle;

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        RuneDefinition _selectedRune;
        Vector2 _scroll;
        float _nameWidth;

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

            // Left of the minimap column, full height; on narrow screens under the column. The whole window scrolls
            // when it does not fit.
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
            _nameWidth = Mathf.Min(240, area.width - 200);
            Controls.Block(area);
            // The box style is see-through; names drawn over the world would show through the list.
            var tint = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.09f, 0.95f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(Controls.UsingTouch ? "가방 (가방 버튼으로 닫기)" : "가방 (Tab 또는 I로 닫기)");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(area.height - TitleHeight));
            DrawStats();

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
                    if (GUILayout.Button("벗기", GUILayout.Width(56))) _send(_client.UnequipCommand(slot));
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
                if (GUILayout.Button("장착", GUILayout.Width(56))) _send(_client.EquipCommand(item.Id));
                if (GUILayout.Button("버리기", GUILayout.Width(56))) _send(_client.DiscardCommand(item.Id));
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

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawTooltip(area);
        }

        // The hovered item's description (or how to see one), in a box beside the window. Drawn outside the layout
        // so its changing height never shifts the window's controls.
        static void DrawTooltip(Rect window)
        {
            string text = !string.IsNullOrEmpty(GUI.tooltip) ? GUI.tooltip
                : Controls.UsingTouch ? "아이템 이름을 누르고 있으면 설명이 보입니다." : "아이템 이름에 마우스를 올리면 설명이 보입니다.";
            var content = new GUIContent(text);
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            // Left of the window and below the HUD when there is room, otherwise over the window's bottom edge. As wide
            // as the longest line needs (CalcSize ignores wrapping), so short hints do not wrap their last word.
            bool besideWindow = window.x >= TooltipWidth + 20;
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
                $"피해 {stats.Damage:0.#} · 초당 공격 {stats.AttacksPerSecond:0.##} · 치명타 {stats.CritChance:0.#}% (×{stats.CritDamage:0}%) · 아이템 희귀도 +{stats.ItemRarity:0}%");
            if (_client.Classes.Count == 0) return;
            var hits = _client.Skills
                .Where(skill => skill.Damage > 0)
                .Select(skill => $"{skill.Name} {stats.DamageFor(stats.ContextFor(skill)) * skill.Damage / 100f:0.#}");
            GUILayout.Label($"클래스 {string.Join(" + ", _client.Classes.Select(c => c.Name))} · 스킬 피해 {string.Join(" · ", hits)}");
        }

        void ItemLabel(Item item)
        {
            var previous = GUI.color;
            GUI.color = Palette.Of(item.Rarity);
            string sockets = item.Sockets.Count == 0 ? "" : " " + string.Concat(item.Sockets.Select(r => r == null ? "○" : "●"));
            GUILayout.Label(new GUIContent(ItemText.Name(item) + sockets, string.Join("\n", ItemText.Describe(item))), GUILayout.Width(_nameWidth));
            GUI.color = previous;
        }

        // Shown while a rune is picked and the item still has room for it.
        void RuneButton(Item item)
        {
            if (_selectedRune == null || !item.Sockets.Contains(null)) return;
            if (GUILayout.Button("끼우기", GUILayout.Width(56))) _send(_client.InsertRuneCommand(item.Id, _selectedRune));
        }

        static string CategoryName(SlotCategory category) => category switch
        {
            SlotCategory.Weapon => "무기",
            SlotCategory.Armor => "방어구",
            _ => "장신구",
        };
    }
}
