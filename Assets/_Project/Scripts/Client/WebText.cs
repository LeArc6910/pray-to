using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Client
{
    // A text field that takes letters put together by an input method (Korean, Japanese, Chinese) in a web build on a PC.
    // Unity's web player reads keys one press at a time, so an IMGUI text field there never gets a Korean syllable (the
    // sweep's "적은 이름으로 캐릭터를 만든다", 2026-11-25). A press on the field gives the keys to a text box of the page's
    // own laid over it (Plugins/WebGL/Display.jslib); the field shows that box's value, a caret at its end, until Enter,
    // Esc, Tab or a press elsewhere ends the typing. Everywhere else (the editor, a desktop build, a phone's browser,
    // which has Unity's own keyboard box) it is GUI.TextField.
    static class WebText
    {
        // The field the page's box serves (by its key), its text as last read, and the last frame it was drawn.
        static string _open;
        static int _drawnFrame = -1;
#if UNITY_WEBGL && !UNITY_EDITOR
        static string _text = "";
        static int _readFrame = -1;
        static bool _end;
#endif

        // Whether a field is being typed in through the page's box (the game's keys keep still meanwhile).
        public static bool Typing => _open != null;

        public static string Field(Rect rect, string text, int max, string key, GUIStyle style)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Application.isMobilePlatform) return Bridged(rect, text ?? "", max, key, style);
#endif
            return GUI.TextField(rect, text ?? "", max, style);
        }

        // Once a frame (Ui.Begin): a field no longer drawn lets go of the keys.
        public static void Frame()
        {
            if (_open != null && Time.frameCount - _drawnFrame > 2) Close();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static string Bridged(Rect rect, string text, int max, string key, GUIStyle style)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (rect.Contains(e.mousePosition))
                {
                    if (_open != key) Open(rect, text, max, key);
                    e.Use();
                }
                else if (_open == key) Close();
            }
            bool mine = _open == key;
            if (mine)
            {
                _drawnFrame = Time.frameCount;
                if (_readFrame != Time.frameCount)
                {
                    _readFrame = Time.frameCount;
                    _text = TextInputValue() ?? "";
                    int state = TextInputState();
                    _end |= state != 0;
                }
                text = _text.Length > max ? _text.Substring(0, max) : _text;
                if (_end) Close();
            }
            if (e.type == EventType.Repaint) Draw(rect, text, style, mine);
            return text;
        }

        static void Open(Rect rect, string text, int max, string key)
        {
            _open = key;
            _text = text;
            _end = false;
            _readFrame = _drawnFrame = Time.frameCount;
            GUIUtility.keyboardControl = 0;
            // GUI points are CSS pixels: the canvas fills the page from its top left at the device's pixel ratio (Ui.Scale).
            var at = GUIUtility.GUIToScreenRect(rect);
            float scale = Mathf.Max(0.01f, Ui.Scale);
            TextInputOpen(text, max, at.x / scale, at.y / scale, at.width / scale, at.height / scale);
        }
#endif

        static void Close()
        {
            _open = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            _end = false;
            TextInputClose();
#endif
        }

        static readonly GUIContent Words = new GUIContent();

        // The field as IMGUI draws one, lit while typed in, with a blinking caret after the words.
        static void Draw(Rect rect, string text, GUIStyle style, bool typing)
        {
            Words.text = text;
            bool hover = rect.Contains(Event.current.mousePosition);
            style.Draw(rect, Words, hover, false, false, typing);
            if (!typing || Mathf.Repeat(Time.unscaledTime, 1f) > 0.55f) return;
            var caret = style.GetCursorPixelPosition(rect, Words, text.Length);
            float height = style.lineHeight > 0 ? style.lineHeight : rect.height - 8;
            var previous = GUI.color;
            GUI.color = style.focused.textColor.a > 0 ? style.focused.textColor : style.normal.textColor;
            GUI.DrawTexture(new Rect(Mathf.Round(caret.x), Mathf.Round(caret.y), Mathf.Max(1, UiSkin.Pt / 2), height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void TextInputOpen(string text, int max, float x, float y, float width, float height);

        [DllImport("__Internal")]
        static extern string TextInputValue();

        [DllImport("__Internal")]
        static extern int TextInputState();

        [DllImport("__Internal")]
        static extern void TextInputClose();
#endif
    }
}
