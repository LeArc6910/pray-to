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
        const float Width = 520;

        readonly WorldClient _client;
        readonly Action<byte[]> _send;
        RuneDefinition _selectedRune;
        Vector2 _scroll;

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

            GUILayout.BeginArea(new Rect(Screen.width - Width - 20, 60, Width, Screen.height - 100), GUI.skin.box);
            GUILayout.Label("가방 (Tab 또는 I로 닫기)");
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
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(120, Screen.height - 560)));
            foreach (var item in _client.Bag.Items.ToList())
            {
                GUILayout.BeginHorizontal();
                ItemLabel(item);
                RuneButton(item);
                if (GUILayout.Button("장착", GUILayout.Width(56))) _send(_client.EquipCommand(item.Id));
                if (GUILayout.Button("버리기", GUILayout.Width(56))) _send(_client.DiscardCommand(item.Id));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

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

            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Box(GUI.tooltip);
            GUILayout.EndArea();
        }

        void DrawStats()
        {
            var stats = _client.Stats;
            GUILayout.Label(
                $"생명력 {stats.MaxHealth:0} · 재생 {stats.HealthRegen:0.#}/초 · 방어도 {stats.Armor:0} · 이동 {stats.MoveSpeed:0.#}\n" +
                $"피해 {stats.Damage:0.#} · 초당 공격 {stats.AttacksPerSecond:0.##} · 치명타 {stats.CritChance:0.#}% (×{stats.CritDamage:0}%) · 아이템 희귀도 +{stats.ItemRarity:0}%");
        }

        static void ItemLabel(Item item)
        {
            var previous = GUI.color;
            GUI.color = Palette.Of(item.Rarity);
            string sockets = item.Sockets.Count == 0 ? "" : " " + string.Concat(item.Sockets.Select(r => r == null ? "○" : "●"));
            GUILayout.Label(new GUIContent(ItemText.Name(item) + sockets, string.Join("\n", ItemText.Describe(item))), GUILayout.Width(240));
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
