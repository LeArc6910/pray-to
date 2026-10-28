using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The skill slots: a row of Q E R F at the bottom middle, or on touch screens a block of buttons two wide in the
    // bottom right corner, under the thumb that aims. Each shows the skill, a shade that shrinks as it cools down, and
    // a coloured edge while its buff runs. Using a skill goes through Controls; this draws and marks the buttons.
    static class SkillBar
    {
        const float KeySize = 64;
        const float ButtonSize = 62;
        const float Gap = 6;
        const float Margin = 12;
        const float TooltipWidth = 260;

        static GUIStyle _nameStyle;
        static GUIStyle _timeStyle;
        static GUIStyle _keyStyle;
        static GUIStyle _tooltipStyle;

        public static Rect Area(int slot, int count)
        {
            if (Controls.UsingTouch)
            {
                int rows = (count + 1) / 2;
                float left = Ui.Width - Margin - 2 * ButtonSize - Gap;
                float top = Ui.Height - Margin - rows * ButtonSize - (rows - 1) * Gap;
                return new Rect(left + slot % 2 * (ButtonSize + Gap), top + slot / 2 * (ButtonSize + Gap), ButtonSize, ButtonSize);
            }
            float width = count * KeySize + (count - 1) * Gap;
            return new Rect((Ui.Width - width) / 2 + slot * (KeySize + Gap), Ui.Height - Margin - KeySize, KeySize, KeySize);
        }

        // Everything the slots cover, for keeping other text clear of them.
        public static Rect Bounds(int count)
        {
            if (count == 0) return Rect.zero;
            var bounds = Area(0, count);
            for (int slot = 1; slot < count; slot++)
            {
                var area = Area(slot, count);
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
                Fill(area, new Color(0, 0, 0, 0.65f));
                Fill(new Rect(area.x, area.yMax - 4, area.width, 4), color);
                if (buff > 0) Outline(area, color, 3);

                var previous = GUI.color;
                GUI.color = cooldown > 0 || client.IsDead ? new Color(1, 1, 1, 0.45f) : Color.white;
                GUI.Label(new Rect(area.x + 3, area.y + 10, area.width - 6, area.height - 18), skill.Name, _nameStyle);
                GUI.color = previous;
                if (!Controls.UsingTouch) GUI.Label(new Rect(area.x + 4, area.y + 2, 20, 18), Controls.SkillKeyName(slot), _keyStyle);

                if (cooldown > 0)
                {
                    float share = Mathf.Clamp01(cooldown / Mathf.Max(0.01f, skill.Cooldown));
                    Fill(new Rect(area.x, area.y, area.width, area.height * share), new Color(0, 0, 0, 0.5f));
                    GUI.Label(area, cooldown >= 1 ? Mathf.CeilToInt(cooldown).ToString() : cooldown.ToString("0.0"), _timeStyle);
                }
                if (buff > 0)
                {
                    GUI.color = color;
                    GUI.Label(new Rect(area.x, area.yMax - 22, area.width - 4, 18), Mathf.CeilToInt(buff) + "초", _keyStyle);
                    GUI.color = previous;
                }

                if (!Controls.UsingTouch && area.Contains(Event.current.mousePosition)) DrawTooltip(skill, area);
            }
        }

        // Above the hovered slot: what the skill does, its tags and numbers.
        static void DrawTooltip(SkillDefinition skill, Rect slot)
        {
            string tags = skill.Tags == SkillTag.None ? "" : " · " + StatText.TagNames(skill.Tags);
            var content = new GUIContent($"{skill.Name} ({skill.Class.Name}){tags}\n{skill.Description}\n{SkillText.Numbers(skill)}");
            float height = _tooltipStyle.CalcHeight(content, TooltipWidth);
            float x = Mathf.Clamp(slot.center.x - TooltipWidth / 2, 10, Ui.Width - TooltipWidth - 10);
            GUI.Box(new Rect(x, slot.y - height - 8, TooltipWidth, height), content, _tooltipStyle);
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
