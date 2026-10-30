using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // The pixel-art look of every IMGUI screen: a copy of Unity's default skin with its backgrounds swapped for the
    // frames in Art/UiArt.cs, and warmer text. Ui.Begin sets it at the start of every OnGUI, so styles made from
    // GUI.skin elsewhere (tooltips, slot labels) inherit it. Also draws the bars (health, the boss) in the same frames.
    static class UiSkin
    {
        static readonly Color Text = new Color(0.94f, 0.9f, 0.84f);
        static readonly Color TextBright = Color.white;
        static readonly Color TextPressed = new Color(1f, 0.9f, 0.68f);
        static readonly Rgba HealthColor = Rgba.Hex("#D24A3C");
        static readonly Rgba BossColor = Rgba.Hex("#A8324A");

#if UNITY_EDITOR
        // High-resolution backgrounds exist only in the editor; players have none to drop.
        static readonly Texture2D[] NoScaled = new Texture2D[0];
#endif

        static GUISkin _skin;
        static GUIStyle _barWell;
        static GUIStyle _barText;
        static Texture2D _healthFill;
        static Texture2D _bossFill;

        // Call inside OnGUI: Unity's default skin can only be read there.
        public static GUISkin Get()
        {
            if (_skin != null) return _skin;
            var skin = Object.Instantiate(GUI.skin);
            skin.name = "Pixel";
            var panel = Texture(UiArt.Panel);
            var inset = Texture(UiArt.Inset);
            var button = Texture(UiArt.Button());
            var hover = Texture(UiArt.Button(hover: true));
            var pressed = Texture(UiArt.Button(pressed: true));
            var chosen = Texture(UiArt.Button(chosen: true));
            var chosenPressed = Texture(UiArt.Button(pressed: true, chosen: true));

            Frame(skin.box, UiArt.PanelBorder, panel, panel, panel, panel, panel, panel);
            Frame(skin.window, UiArt.PanelBorder, panel, panel, panel, panel, panel, panel);
            // A toolbar draws its tabs with this style; the chosen tab is "on".
            Frame(skin.button, UiArt.ButtonBorder, button, hover, pressed, chosen, chosen, chosenPressed);
            skin.button.hover.textColor = TextBright;
            skin.button.active.textColor = TextPressed;
            skin.button.onNormal.textColor = TextBright;
            skin.button.onHover.textColor = TextBright;
            skin.button.onActive.textColor = TextPressed;
            Frame(skin.textField, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.textArea, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            skin.textField.focused.background = inset;
            skin.textField.focused.textColor = TextBright;
            Frame(skin.verticalScrollbar, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.horizontalScrollbar, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.verticalScrollbarThumb, UiArt.ButtonBorder, button, hover, pressed, button, hover, pressed);
            Frame(skin.horizontalScrollbarThumb, UiArt.ButtonBorder, button, hover, pressed, button, hover, pressed);
            skin.label.normal.textColor = Text;
            skin.scrollView.normal.background = null;
#if UNITY_EDITOR
            skin.scrollView.normal.scaledBackgrounds = NoScaled;
#endif

            _barWell = new GUIStyle { border = new RectOffset(Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder)) };
            _barWell.normal.background = inset;
            _healthFill = Texture(UiArt.BarFill(HealthColor));
            _bossFill = Texture(UiArt.BarFill(BossColor));
            _skin = skin;
            return skin;
        }

        // A bar in a dark well: the fill stretched to the share it shows, and the text over it with a shadow.
        public static void Bar(Rect area, float share, string text, bool boss = false)
        {
            if (_barWell == null) return;
            _barText ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            GUI.Box(area, GUIContent.none, _barWell);
            float edge = UiArt.InsetBorder * UiArt.Scale;
            var inside = new Rect(area.x + edge, area.y + edge, area.width - edge * 2, area.height - edge * 2);
            GUI.DrawTexture(new Rect(inside.x, inside.y, inside.width * Mathf.Clamp01(share), inside.height), boss ? _bossFill : _healthFill);
            if (string.IsNullOrEmpty(text)) return;
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f * previous.a);
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, _barText);
            GUI.color = previous;
            GUI.Label(area, text, _barText);
        }

        // Sets a style's backgrounds for its states (off, then on as for a chosen tab) and how many texture pixels at
        // each edge keep their size. In the editor the default skin's high-resolution backgrounds are dropped, or a
        // high-density screen would still show them.
        static void Frame(GUIStyle style, int border, Texture2D normal, Texture2D hover, Texture2D active, Texture2D onNormal, Texture2D onHover, Texture2D onActive)
        {
            int edge = Edge(border);
            style.border = new RectOffset(edge, edge, edge, edge);
            Set(style.normal, normal);
            Set(style.hover, hover);
            Set(style.active, active);
            Set(style.focused, normal);
            Set(style.onNormal, onNormal);
            Set(style.onHover, onHover);
            Set(style.onActive, onActive);
            Set(style.onFocused, onNormal);
        }

        static void Set(GUIStyleState state, Texture2D background)
        {
            state.background = background;
#if UNITY_EDITOR
            state.scaledBackgrounds = NoScaled;
#endif
            state.textColor = Text;
        }

        static int Edge(int border) => border * UiArt.Scale;

        static Texture2D Texture(PixelImage image) => Sprites.TextureOf(UiArt.Enlarged(image));
    }
}
