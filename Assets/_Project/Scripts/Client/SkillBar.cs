using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The quick slots: the skills, then the potion. On PC a row of Q E R F and C at the bottom middle; on touch
    // screens the skills are a block of buttons two wide in the bottom right corner, under the thumb that aims, with
    // the potion button left of its bottom row. Each is a panel (UiSkin) with a stripe in its colour, a shade that
    // shrinks as it cools down, and for a skill a coloured edge while its buff runs; the potion shows its icon. Using
    // them goes through Controls; this draws and marks the buttons.
    static class SkillBar
    {
        const float KeySize = 64;
        const float ButtonSize = 62;
        const float Gap = 6;
        const float Margin = 12;
        const float TooltipWidth = 260;
        // Inside the panel's frame.
        const float Inner = UiArt.PanelBorder * UiArt.Scale;
        const float IconSize = 28;

        static GUIStyle _nameStyle;
        static GUIStyle _timeStyle;
        static GUIStyle _keyStyle;
        static GUIStyle _tooltipStyle;

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

        // Everything the slots cover, for keeping other text clear of them.
        public static Rect Bounds(int skills)
        {
            var bounds = Area(skills, skills);
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
            _nameStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12 };
            _timeStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 20 };
            _keyStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold, fontSize = 12 };
            _tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };

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
                GUI.color = cooldown > 0 || client.IsDead ? new Color(1, 1, 1, 0.45f) : Color.white;
                GUI.Label(NameArea(area), Ui.Wrap(skill.Name, _nameStyle, area.width - 6), _nameStyle);
                GUI.color = previous;
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
                    DrawTooltip($"{skill.Name} ({skill.Class.Name}){tags}\n{skill.Description}\n{SkillText.Numbers(skill)}", area);
                }
            }
            DrawPotion(client, Area(count, count));
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

        // The key letter in the top left corner, inside the frame so its bevel does not hide the letter's top.
        static Rect KeyArea(Rect slot) => new Rect(slot.x + Inner, slot.y + Inner - 2, 20, 18);

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
            var content = new GUIContent(Ui.Wrap(text, _tooltipStyle, TooltipWidth));
            float height = _tooltipStyle.CalcHeight(content, TooltipWidth);
            float x = Mathf.Clamp(slot.center.x - TooltipWidth / 2, 10, Ui.Width - TooltipWidth - 10);
            float y = Mathf.Max(10, slot.y - height - 8);
            GUI.Box(new Rect(x, y, TooltipWidth, height), content, _tooltipStyle);
        }

        // The slot's panel with a stripe of its colour along the bottom, inside the frame.
        static void Panel(Rect area, Color color)
        {
            GUI.Box(area, GUIContent.none);
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
