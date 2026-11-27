using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The quick slots: the skills, then the potion. On PC a row of Q E R F and C at the bottom middle; on touch
    // screens the skills are a block of buttons two wide in the bottom right corner, under the thumb that aims, with
    // the potion button left of its bottom row. The dodge roll has a smaller slot of its own: right of the potion on
    // PC, above the potion button on touch screens (beside the skills' upper row, nearest the thumb that aims).
    // Each is a panel (UiSkin) with a stripe in its colour, a shade that
    // shrinks as it cools down, and for a skill a coloured edge while its buff runs; the potion shows its icon. Using
    // them goes through Controls; this draws and marks the buttons.
    static class SkillBar
    {
        const float KeySize = 64;
        // The most skill slots a player can have, for keeping room for all of them.
        public const int MostSkills = ClassRules.SkillSlots;
        const float ButtonSize = 62;
        // The roll's slot, on PC and touch alike (Docs/WorldRework.md 구르기: a 48-point button).
        public const float RollSize = 48;
        const float Gap = 6;
        const float Margin = 12;
        const float TooltipWidth = 260;
        // Inside the panel's frame.
        const float Inner = UiArt.PanelBorder * UiArt.Scale;
        const float IconSize = 28;

        static GUIStyle _nameStyle;
        static GUIStyle _timeStyle;
        static GUIStyle _keyStyle;

        // Slot `skills` (one past the last skill) is the potion.
        public static Rect Area(int slot, int skills)
        {
            if (Controls.UsingTouch)
            {
                int columns = Mathf.Min(2, skills);
                float blockLeft = Ui.Width - Margin - columns * ButtonSize - Mathf.Max(0, columns - 1) * Gap;
                float bottom = Ui.Height - Margin - ButtonSize;
                if (slot == skills) return new Rect(blockLeft - (columns > 0 ? Gap : 0) - ButtonSize, bottom, ButtonSize, ButtonSize);
                int rows = (skills + 1) / 2;
                float top = bottom - (rows - 1) * (ButtonSize + Gap);
                return new Rect(blockLeft + slot % 2 * (ButtonSize + Gap), top + slot / 2 * (ButtonSize + Gap), ButtonSize, ButtonSize);
            }
            int count = skills + 1;
            float width = count * KeySize + (count - 1) * Gap;
            return new Rect((Ui.Width - width) / 2 + slot * (KeySize + Gap), Ui.Height - Margin - KeySize, KeySize, KeySize);
        }

        // The dodge roll's slot.
        public static Rect RollArea(int skills)
        {
            var potion = Area(skills, skills);
            return Controls.UsingTouch
                ? new Rect(potion.xMax - RollSize, potion.y - Gap - RollSize, RollSize, RollSize)
                : new Rect(potion.xMax + Gap, potion.yMax - RollSize, RollSize, RollSize);
        }

        // The weapons' slot (Docs/Weapons.md, 무기 두 벌): on the PC bar left of the first skill, on touch left of the
        // roll button.
        public static Rect WeaponArea(int skills)
        {
            if (Controls.UsingTouch)
            {
                var roll = RollArea(skills);
                return new Rect(roll.x - Gap - RollSize, roll.y, RollSize, RollSize);
            }
            var first = Area(0, skills);
            return new Rect(first.x - Gap - KeySize, first.y, KeySize, KeySize);
        }

        // Everything the slots cover, for keeping other text clear of them.
        public static Rect Bounds(int skills)
        {
            var bounds = Area(skills, skills);
            var roll = RollArea(skills);
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, roll.xMin), Mathf.Min(bounds.yMin, roll.yMin), Mathf.Max(bounds.xMax, roll.xMax), Mathf.Max(bounds.yMax, roll.yMax));
            var weapons = WeaponArea(skills);
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, weapons.xMin), Mathf.Min(bounds.yMin, weapons.yMin), Mathf.Max(bounds.xMax, weapons.xMax), Mathf.Max(bounds.yMax, weapons.yMax));
            for (int slot = 0; slot < skills; slot++)
            {
                var area = Area(slot, skills);
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, area.xMin), Mathf.Min(bounds.yMin, area.yMin), Mathf.Max(bounds.xMax, area.xMax), Mathf.Max(bounds.yMax, area.yMax));
            }
            return bounds;
        }

        public static void Draw(WorldClient client)
        {
            _nameStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _timeStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            // Unclipped and without padding: the Korean font's tall lines put a letter low in its box, and a clipped box
            // cut the letter's lower half off (LOCAL_TASKS.md, the 2026-10-31 UI recheck).
            _keyStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
            };
            Ui.Text(_nameStyle, 12);
            Ui.Text(_timeStyle, 20, bold: true);
            Ui.Text(_keyStyle, 12, bold: true);

            int count = client.Skills.Count;
            for (int slot = 0; slot < count; slot++)
            {
                var skill = client.Skills[slot];
                var area = Area(slot, count);
                if (Controls.UsingTouch)
                {
                    Controls.Block(area);
                    Controls.SkillButton(area, slot);
                }

                float cooldown = client.CooldownLeft(slot);
                float buff = client.BuffLeft(slot);
                var color = Palette.Of(skill.Tags | skill.Grants);
                Panel(area, color);
                if (buff > 0) Outline(area, color, 3);

                var previous = GUI.color;
                // The skill's icon, dim while it waits, and also when there is not mana enough for it.
                GUI.color = cooldown > 0 || client.IsDead || !client.HasManaFor(slot) ? new Color(1, 1, 1, 0.45f) : Color.white;
                Sprites.DrawInGui(IconArea(area), Sprites.Icon(skill));
                GUI.color = previous;
                int rank = client.PowerOf(skill);
                if (rank > 1) GUI.Label(new Rect(area.xMax - Inner - 26, area.y + Inner, 24, 18), RankMark(rank), _keyStyle);
                if (!Controls.UsingTouch) GUI.Label(KeyArea(area), Controls.SkillKeyName(slot), _keyStyle);

                Shade(area, cooldown, skill.Cooldown);
                if (buff > 0)
                {
                    GUI.color = color;
                    GUI.Label(new Rect(area.x + Inner, area.yMax - Inner - 22, area.width - Inner * 2, 18), Mathf.CeilToInt(buff) + "초", _keyStyle);
                    GUI.color = previous;
                }

                if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition))
                {
                    string tags = skill.Tags == SkillTag.None ? "" : " · " + StatText.TagNames(skill.Tags);
                    var gems = client.GemsIn(skill);
                    DrawTooltip($"{skill.Name} {rank}단계 ({skill.Class.Name}){tags}\n{skill.Description}\n{SkillText.Numbers(skill, rank, client.Stats, client.SupportsOf(skill))}"
                        + (gems.Count > 0 ? $"\n변형: {string.Join(", ", gems.Select(g => g.Name))}" : "")
                        + SkillText.WeaponNote(skill, client.Stats), area);
                }
            }
            DrawPotion(client, Area(count, count));
            DrawRoll(client, RollArea(count));
            DrawWeapons(client, WeaponArea(count));
        }

        // The weapon in hand, big, and the spare, small in the corner; tapped, clicked or its key (X) swaps them
        // (Docs/Weapons.md, 무기 두 벌). Dim without a spare.
        static void DrawWeapons(WorldClient client, Rect area)
        {
            var held = client.Equipment[ItemSlot.Weapon];
            var spare = client.Equipment.Spare;
            Controls.Block(area);
            Controls.SwapButton(area);
            Panel(area, Palette.Weapons);

            var previous = GUI.color;
            GUI.color = spare != null || held == null ? Color.white : new Color(1, 1, 1, 0.6f);
            float big = area.width - Inner * 2 - 10;
            if (held != null) Sprites.DrawInGui(new Rect(area.x + Inner + 1, area.y + Inner + 1, big, big), Sprites.Icon(held.Base));
            float small = area.width * 0.42f;
            var corner = new Rect(area.xMax - Inner - small, area.yMax - Inner - small, small, small);
            Fill(corner, new Color(0, 0, 0, 0.45f));
            if (spare != null) Sprites.DrawInGui(corner, Sprites.Icon(spare.Base));
            GUI.color = previous;
            if (!Controls.UsingTouch) GUI.Label(KeyArea(area), Controls.SwapKeyName, _keyStyle);

            if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition))
            {
                string Line(string label, Item item) => item == null ? $"{label}: 없음" : $"{label}: {ItemText.Name(item)} ({item.Base.Kind.Name})";
                DrawTooltip($"무기 바꾸기 ({Controls.SwapKeyName})\n{Line("손", held)}\n{Line("보조", spare)}\n"
                    + "클래스 스킬은 손에 든 것과 상관없이 그 클래스 무기의 힘을 받는다. 보조 무기는 가방에서 [보조로]", area);
            }
        }

        // The dodge roll: its picture, dim and shaded while it cannot be used again yet. Tapped or clicked through
        // Controls, like the potion, so it works while both sticks are held.
        static void DrawRoll(WorldClient client, Rect area)
        {
            Controls.Block(area);
            Controls.RollButton(area);
            Panel(area, Palette.Roll);

            var previous = GUI.color;
            GUI.color = client.CanRoll ? Color.white : new Color(1, 1, 1, 0.45f);
            float icon = SkillArt.Size * 2;
            Sprites.DrawInGui(new Rect(area.center.x - icon / 2, area.y + Inner + (area.height - Inner * 2 - 6 - icon) / 2, icon, icon), Sprites.RollIcon);
            GUI.color = previous;
            if (!Controls.UsingTouch) GUI.Label(KeyArea(area), Controls.RollKeyName, _keyStyle);
            float share = client.RollCooldownShare;
            if (share > 0) Fill(new Rect(area.x, area.y, area.width, area.height * share), new Color(0, 0, 0, 0.5f));

            if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition))
            {
                DrawTooltip($"구르기 ({Controls.RollKeyName})\n움직이는 쪽으로 {World.RollDistance:0.#}칸을 구른다. 구르는 앞 {World.DodgeSeconds:0.#}초 동안은 공격이 지나간다. {World.RollCooldownSeconds:0.#}초 뒤에 다시 쓸 수 있다", area);
            }
        }

        // The first consumable in the bag and how many there are; dim when there is none or it is still waiting.
        static void DrawPotion(WorldClient client, Rect area)
        {
            if (Controls.UsingTouch)
            {
                Controls.Block(area);
                Controls.PotionButton(area);
            }
            var potion = client.QuickConsumable;
            int count = potion != null ? client.Bag.CountOf(potion) : 0;
            float wait = client.ConsumableCooldownLeft;
            Panel(area, Palette.Potion);

            var previous = GUI.color;
            GUI.color = count == 0 || wait > 0 || client.IsDead ? new Color(1, 1, 1, 0.45f) : Color.white;
            if (potion != null)
            {
                Sprites.DrawInGui(new Rect(area.center.x - IconSize / 2, area.y + Inner + 6, IconSize, IconSize), Sprites.Icon(potion));
                GUI.Label(new Rect(area.x + 3, area.y + Inner + 32, area.width - 6, 18), $"×{count}", _nameStyle);
            }
            else
            {
                GUI.Label(NameArea(area), "물약\n×0", _nameStyle);
            }
            GUI.color = previous;
            if (!Controls.UsingTouch) GUI.Label(KeyArea(area), Controls.PotionKeyName, _keyStyle);
            Shade(area, wait, potion?.Cooldown ?? wait);

            if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition))
            {
                DrawTooltip(potion != null ? $"{potion.Name} ×{count}\n{potion.Description}" : "물약이 없다. 몬스터와 보스가 떨어뜨린다", area);
            }
        }

        // The icon fills the slot inside its frame, above the colour stripe.
        static Rect IconArea(Rect slot)
        {
            float size = Mathf.Floor((slot.height - Inner * 2 - 8) / SkillArt.Size) * SkillArt.Size;
            size = Mathf.Max(SkillArt.Size * 2, size);
            return new Rect(slot.center.x - size / 2, slot.y + Inner + (slot.height - Inner * 2 - 6 - size) / 2, size, size);
        }

        // A rank past the first as Roman numerals, small in the top right corner.
        public static string RankMark(int rank) => rank switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => "I" };

        // The key letter in the top left corner, inside the frame so its bevel does not hide the letter's top.
        static Rect KeyArea(Rect slot) => new Rect(slot.x + Inner + 2, slot.y + Inner, 24, 20);

        // The name, under the key letter and above the stripe.
        static Rect NameArea(Rect slot) => new Rect(slot.x + 3, slot.y + Inner + 10, slot.width - 6, slot.height - Inner * 2 - 16);

        // Darkens the share of the slot still cooling down and shows the seconds left.
        static void Shade(Rect area, float left, float total)
        {
            if (left <= 0) return;
            float share = Mathf.Clamp01(left / Mathf.Max(0.01f, total));
            Fill(new Rect(area.x, area.y, area.width, area.height * share), new Color(0, 0, 0, 0.5f));
            GUI.Label(area, left >= 1 ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0"), _timeStyle);
        }

        // Above the hovered slot, kept on the screen.
        static void DrawTooltip(string text, Rect slot)
        {
            // A card drawn last (Ui.LateCard), over everything: drawn here the quick slots, drawn after the skills, hid
            // the roll's description (Docs/UiRework.md 2절, pc-boss-bar).
            var size = Ui.CardSize(text, TooltipWidth, out string wrapped);
            float x = Mathf.Clamp(slot.center.x - size.x / 2, 10, Ui.Width - size.x - 10);
            float y = Mathf.Max(10, slot.y - size.y - 8);
            Ui.LateCard(new Rect(x, y, size.x, size.y), wrapped);
        }

        // The slot's panel with a stripe of its colour along the bottom, inside the frame.
        static void Panel(Rect area, Color color)
        {
            UiSkin.Panel(area);
            Fill(new Rect(area.x + Inner, area.yMax - Inner - 4, area.width - Inner * 2, 4), color);
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
