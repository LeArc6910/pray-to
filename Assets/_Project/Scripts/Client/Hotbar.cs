using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The quick slots along the bottom of the screen (Docs/WebDemo.md, "퀵슬롯"): things from the bag put there with
    // the bag's [퀵] button, used with a key (1 to 6 unless changed, KeyBindings), a click or a tap. Gear is taken in
    // hand or put on, and whatever it replaces goes back to the bag; a potion or food is used; a material that
    // builds is placed, a seed planted and the watering can pours, all on the tile ahead.
    //
    // A slot names what it holds, not where it is: an item is found again wherever it is, in the bag or worn, and
    // when it is gone another of the same kind takes its place. Kept per character on this device (Prefs);
    // the host never hears of the slots, only of what using one does, as if the bag's button had been pressed.
    sealed class Hotbar
    {
        public const int Slots = 6;
        const float MaxSize = 52;
        const float Gap = 6;
        const float Margin = 12;
        const float TooltipWidth = 260;
        const float MessageSeconds = 3;

        enum Kind : byte
        {
            Empty,
            Gear,
            Consumable,
            Material,
        }

        // What a slot holds: a kind of consumable or material by id, or a piece of gear by its base's id and a
        // description of the very item (its signature).
        readonly struct Entry
        {
            public Entry(Kind kind, string id, string signature = "")
            {
                Kind = kind;
                Id = id ?? "";
                Signature = signature ?? "";
            }

            public Kind Kind { get; }
            public string Id { get; }
            public string Signature { get; }
            public bool IsEmpty => Kind == Kind.Empty;

            public bool Same(Entry other) => Kind == other.Kind && Id == other.Id && (Kind != Kind.Gear || Signature == other.Signature);

            public override string ToString() => IsEmpty ? "" : $"{(int)Kind}|{Id}|{Signature}";

            public static Entry Parse(string text)
            {
                string[] parts = text.Split(new[] { '|' }, 3);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int kind) || kind <= 0 || kind > (int)Kind.Material) return default;
                return new Entry((Kind)kind, parts[1], parts.Length > 2 ? parts[2] : "");
            }
        }

        readonly WorldClient _client;
        readonly LootData _loot;
        readonly string _key;
        readonly Entry[] _entries = new Entry[Slots];
        readonly Dictionary<Item, string> _signatures = new Dictionary<Item, string>();
        int _signaturesVersion = -1;
        // Whether the slots came from this device or got their first things; until then they wait for the bag.
        bool _ready;
        float _messageAt = float.NegativeInfinity;
        static GUIStyle _countStyle;
        static GUIStyle _keyStyle;
        static GUIStyle _tooltipStyle;

        public Hotbar(WorldClient client, LootData loot, string character)
        {
            _client = client;
            _loot = loot;
            _key = "hotbar." + character;
            if (!Prefs.Has(_key)) return;
            string[] lines = Prefs.Get(_key, "").Split('\n');
            for (int slot = 0; slot < Slots && slot < lines.Length; slot++) _entries[slot] = Entry.Parse(lines[slot]);
            _ready = true;
        }

        // What the last [퀵] press did when it could not, for the bag to show a moment.
        public string Message { get; private set; } = "";
        public bool ShowsMessage => Message.Length > 0 && Time.time - _messageAt < MessageSeconds;

        // The slot the item, consumable or material is in, or -1.
        public int SlotOf(Item item) => SlotOf(GearEntry(item));
        public int SlotOf(ConsumableDefinition consumable) => SlotOf(new Entry(Kind.Consumable, consumable.Id));
        public int SlotOf(MaterialDefinition material) => SlotOf(new Entry(Kind.Material, material.Id));

        // Puts it in the first empty slot, or takes it out if it is in one.
        public void Toggle(Item item) => Toggle(GearEntry(item));
        public void Toggle(ConsumableDefinition consumable) => Toggle(new Entry(Kind.Consumable, consumable.Id));
        public void Toggle(MaterialDefinition material) => Toggle(new Entry(Kind.Material, material.Id));

        // The message using the slot sends, and the sound to play with it; null when the slot cannot be used now:
        // empty, nothing of its kind left, the gear already worn, a potion still waiting.
        public (byte[] Command, Sound? Sound) Use(int slot)
        {
            FillFirstSlots();
            if (slot < 0 || slot >= Slots || _client.IsDead) return (null, null);
            var entry = _entries[slot];
            switch (entry.Kind)
            {
                case Kind.Gear:
                    var item = FindItem(entry);
                    if (item == null || IsWorn(item) || item.Base.RequiredLevel > _client.Level) return (null, null);
                    return (_client.EquipCommand(item.Id), Sound.Place);
                case Kind.Consumable:
                    var consumable = ConsumableOf(entry);
                    return (consumable == null ? null : _client.UseConsumableCommand(consumable), null);
                case Kind.Material:
                    var material = MaterialOf(entry);
                    if (material == null || _client.Bag.CountOf(material) == 0) return (null, null);
                    if (material.Places != null) return (_client.PlaceCommand(material), Sound.Place);
                    var crop = _client.CropFrom(material);
                    if (crop != null) return (_client.PlantCommand(crop), Sound.Place);
                    if (material.Id == World.WateringCan) return (_client.WaterCommand(), Sound.Water);
                    if (material.Id == World.FishingRod) return (_client.FishCommand(), _client.Fishing < 0 ? Sound.Cast : (Sound?)null);
                    if (material.Id == World.Hoe) return (_client.TillCommand(), Sound.Dig);
                    if (material.Id == World.Fertilizer) return (_client.FertilizeCommand(), Sound.Place);
                    return (null, null);
                default:
                    return (null, null);
            }
        }

        // Where the slot is: on PC in a row just above the skill keys; on touch screens beside the skill buttons on the
        // bottom row when there is room, else in a row above them. Slots shrink to fit a narrow screen.
        public static Rect Area(int slot, int skills)
        {
            float size = Mathf.Min(MaxSize, (Ui.Width - Margin * 2 - Gap * (Slots - 1)) / Slots);
            float width = Slots * size + (Slots - 1) * Gap;
            var keys = SkillBar.Bounds(skills);
            float x;
            float y;
            if (!Controls.UsingTouch)
            {
                x = (Ui.Width - width) / 2;
                y = keys.y - Gap - size;
            }
            else if (keys.x - Margin - Gap >= width)
            {
                x = keys.x - Gap - width;
                y = Ui.Height - Margin - size;
            }
            else
            {
                x = Mathf.Max(Margin, Ui.Width - Margin - width);
                y = keys.y - Gap - size;
            }
            return new Rect(x + slot * (size + Gap), y, size, size);
        }

        public static Rect Bounds(int skills) => Rect.MinMaxRect(Area(0, skills).xMin, Area(0, skills).yMin, Area(Slots - 1, skills).xMax, Area(0, skills).yMax);

        public void Draw(int skills)
        {
            FillFirstSlots();
            _countStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
            };
            _keyStyle ??= new GUIStyle(_countStyle) { alignment = TextAnchor.UpperLeft };
            Ui.Text(_countStyle, 12, bold: true);
            Ui.Text(_keyStyle, 12, bold: true);
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };

            string tooltip = null;
            Rect tooltipSlot = default;
            if (Event.current.type == EventType.Repaint)
            {
                PointsAtGround = PointsAtSoil = false;
                PointsAtSize = 1;
            }
            for (int slot = 0; slot < Slots; slot++)
            {
                var area = Area(slot, skills);
                Controls.Block(area);
                Controls.HotbarButton(area, slot);
                GUI.Box(area, GUIContent.none);
                float inner = UiArt.PanelBorder * UiArt.Scale;

                var (icon, count, usable, worn, name, does) = Describe(_entries[slot]);
                if (worn) Outline(area, Palette.Worn, 3);
                var previous = GUI.color;
                GUI.color = usable ? Color.white : new Color(1, 1, 1, 0.4f);
                if (icon != null)
                {
                    float iconSize = Mathf.Min(32, area.width - inner * 2 - 4);
                    Sprites.DrawInGui(new Rect(area.center.x - iconSize / 2, area.center.y - iconSize / 2, iconSize, iconSize), icon);
                }
                if (count >= 0) GUI.Label(new Rect(area.x + inner, area.y + inner, area.width - inner * 2 - 1, area.height - inner * 2), $"{count}", _countStyle);
                GUI.color = previous;
                if (!Controls.UsingTouch) GUI.Label(new Rect(area.x + inner + 1, area.y + inner, 24, 18), KeyBindings.Name(KeyAction.Hotbar1 + slot), _keyStyle);
                if (_entries[slot].Kind == Kind.Consumable && _client.ConsumableCooldownLeft > 0)
                {
                    Fill(area, new Color(0, 0, 0, 0.45f));
                }

                if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition))
                {
                    tooltip = _entries[slot].IsEmpty ? "빈 칸. 가방의 [퀵]으로 올린다" : $"{name}\n{does}";
                    tooltipSlot = area;
                    if (Event.current.type == EventType.Repaint && ActsAhead(_entries[slot]))
                    {
                        PointsAtGround = true;
                        PointsAtSoil = WorksSoil(_entries[slot]);
                        PointsAtSize = MaterialOf(_entries[slot])?.Places is TileType places ? World.SizeOf(places) : 1;
                    }
                }
            }
            if (tooltip != null) DrawTooltip(tooltip, tooltipSlot);
        }

        // Whether the pointer is on a slot whose use builds, plants or works the tile ahead, for WorldView's frame on
        // that tile (2026-11-09).
        public bool PointsAtGround { get; private set; }
        // And it works the ground right ahead (a seed, the can, the hoe, fertilizer, a floor): World.TileAhead, not
        // World.PlaceAhead.
        public bool PointsAtSoil { get; private set; }
        // How many tiles each way the slot's thing takes when put down (a big machine 2).
        public int PointsAtSize { get; private set; } = 1;

        bool WorksSoil(Entry entry)
        {
            var material = MaterialOf(entry);
            return material != null && (material.Places is TileType tile && TileMap.IsPaving(tile) || _client.CropFrom(material) != null
                || material.Id == World.WateringCan || material.Id == World.Hoe || material.Id == World.Fertilizer);
        }

        bool ActsAhead(Entry entry)
        {
            if (entry.Kind != Kind.Material) return false;
            var material = MaterialOf(entry);
            return material != null && (material.Places != null || _client.CropFrom(material) != null || material.Id == World.WateringCan
                || material.Id == World.Hoe || material.Id == World.Fertilizer);
        }

        // The icon, the count (-1 for gear), whether it can be used now, whether it is the gear worn, its name and
        // what using it does.
        (Sprite Icon, int Count, bool Usable, bool Worn, string Name, string Does) Describe(Entry entry)
        {
            switch (entry.Kind)
            {
                case Kind.Gear:
                    var item = FindItem(entry);
                    var itemBase = item?.Base ?? _loot?.Bases.FirstOrDefault(b => b.Id == entry.Id);
                    if (itemBase == null) return (null, -1, false, false, "", "");
                    bool worn = item != null && IsWorn(item);
                    string does = item == null ? "가방에 없다" : worn ? "지금 끼고 있다" : $"{ItemText.SlotName(itemBase.Slot)}에 낀다";
                    return (Sprites.Icon(itemBase), -1, item != null && !worn, worn, item != null ? ItemText.Name(item) : itemBase.Name, does);
                case Kind.Consumable:
                    var consumable = ConsumableOf(entry);
                    if (consumable == null) return (null, -1, false, false, "", "");
                    int left = _client.Bag.CountOf(consumable);
                    string action = consumable.IsFood ? "먹는다" : consumable.Recalls ? "침대(없으면 시작 지점)로 돌아간다" : "마신다";
                    return (Sprites.Icon(consumable), left, left > 0, false, consumable.Name, action);
                case Kind.Material:
                    var material = MaterialOf(entry);
                    if (material == null) return (null, -1, false, false, "", "");
                    int have = _client.Bag.CountOf(material);
                    string use = material.Places != null ? "앞에 놓는다"
                        : _client.CropFrom(material) != null ? "앞에 심는다"
                        : material.Id == World.WateringCan ? "앞 3×3칸에 물을 준다"
                        : material.Id == World.FishingRod ? "앞의 물에 던지고, 입질(!)이 오면 게이지가 초록 칸에 왔을 때 다시 눌러 낚는다"
                        : material.Id == World.Hoe ? "앞 바닥을 밭으로 만든다(밭이면 바닥으로)"
                        : material.Id == World.Fertilizer ? "앞 3×3칸의 밭에 거름을 준다"
                        : "쓸 수 없다(재료)";
                    return (Sprites.Icon(material), have, have > 0, false, material.Name, use);
                default:
                    return (null, -1, false, false, "", "");
            }
        }

        // A character with no slots kept on this device starts with its weapon, a pickaxe, its first potion and
        // torches, once the host has sent its bag.
        void FillFirstSlots()
        {
            if (_ready || (_client.Equipment.Items.Count() == 0 && _client.Bag.Items.Count == 0)) return;
            _ready = true;
            int next = 0;
            void Put(Entry entry)
            {
                if (!entry.IsEmpty && next < Slots && SlotOf(entry) < 0) _entries[next++] = entry;
            }

            var weapon = _client.Equipment[ItemSlot.Weapon];
            if (weapon != null) Put(GearEntry(weapon));
            var pickaxe = _client.Equipment.Items.Concat(_client.Bag.Items).FirstOrDefault(item => item.Base.IsPickaxe);
            if (pickaxe != null) Put(GearEntry(pickaxe));
            var potion = _client.QuickConsumable;
            if (potion != null) Put(new Entry(Kind.Consumable, potion.Id));
            var torch = _client.Bag.Materials.Keys.FirstOrDefault(m => m.Places == TileType.Torch);
            if (torch != null) Put(new Entry(Kind.Material, torch.Id));
            Save();
        }

        void Toggle(Entry entry)
        {
            _ready = true;
            int slot = SlotOf(entry);
            if (slot >= 0)
            {
                _entries[slot] = default;
                Save();
                return;
            }
            int empty = System.Array.FindIndex(_entries, e => e.IsEmpty);
            if (empty < 0)
            {
                Message = "퀵슬롯이 가득 찼다. 다른 것의 [퀵]을 눌러 먼저 내린다";
                _messageAt = Time.time;
                return;
            }
            _entries[empty] = entry;
            Save();
        }

        int SlotOf(Entry entry) => System.Array.FindIndex(_entries, e => !e.IsEmpty && e.Same(entry));

        void Save()
        {
            Prefs.Set(_key, string.Join("\n", _entries.Select(e => e.ToString())));
        }

        Entry GearEntry(Item item) => new Entry(Kind.Gear, item.Base.Id, Signature(item));

        // The item itself if it is still here, else another of the same kind: the worn one first.
        Item FindItem(Entry entry)
        {
            Item sameKind = null;
            foreach (var item in _client.Equipment.Items.Concat(_client.Bag.Items))
            {
                if (item.Base.Id != entry.Id) continue;
                if (Signature(item) == entry.Signature) return item;
                if (sameKind == null) sameKind = item;
            }
            return sameKind;
        }

        bool IsWorn(Item item) => _client.Equipment.Items.Contains(item);

        // An item's name, rarity and every stat, which tells it from others of its base. Items get new ids each
        // session, so ids would not find them again.
        string Signature(Item item)
        {
            if (_signaturesVersion != _client.InventoryVersion)
            {
                _signatures.Clear();
                _signaturesVersion = _client.InventoryVersion;
            }
            if (_signatures.TryGetValue(item, out string signature)) return signature;
            signature = $"{item.Rarity}:{ItemText.Name(item)}:{string.Join(";", ItemText.Describe(item))}";
            _signatures[item] = signature;
            return signature;
        }

        ConsumableDefinition ConsumableOf(Entry entry) => _loot?.Consumables.FirstOrDefault(c => c.Id == entry.Id);

        MaterialDefinition MaterialOf(Entry entry) => _loot?.Materials.FirstOrDefault(m => m.Id == entry.Id);

        static void DrawTooltip(string text, Rect slot)
        {
            var content = new GUIContent(Ui.Wrap(text, _tooltipStyle, TooltipWidth));
            float height = _tooltipStyle.CalcHeight(content, TooltipWidth);
            float x = Mathf.Clamp(slot.center.x - TooltipWidth / 2, 10, Ui.Width - TooltipWidth - 10);
            float y = Mathf.Max(10, slot.y - height - 8);
            GUI.Box(new Rect(x, y, TooltipWidth, height), content, _tooltipStyle);
        }

        static void Fill(Rect area, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void Outline(Rect area, Color color, float width)
        {
            Fill(new Rect(area.x, area.y, area.width, width), color);
            Fill(new Rect(area.x, area.yMax - width, area.width, width), color);
            Fill(new Rect(area.x, area.y, width, area.height), color);
            Fill(new Rect(area.xMax - width, area.y, width, area.height), color);
        }
    }
}
