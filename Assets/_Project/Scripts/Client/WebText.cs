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
    // which has Unity's own keyboard box) it is GUI.TextField. Every field a player types into goes through here: the
    // title's (TitleDraw.Field), a pet's name and the cloud account's name and password (2026-11-26). Each is known to
    // the playtests by its key (Ui.Probe).
    static class WebText
    {
        // The field the page's box serves (by its key), its text as last read, and the last frame it was drawn.
        static string _open;
        static int _drawnFrame = -1;
        // The field whose typing Enter ended, until it is asked (Submitted).
        static string _submitted;
#if UNITY_WEBGL && !UNITY_EDITOR
        static string _text = "";
        static int _readFrame = -1;
        static int _endKey;
#endif

        // Whether a field is being typed in through the page's box (the game's keys keep still meanwhile).
        public static bool Typing => _open != null;

        // Whether this field is the one being typed in through the page's box.
        public static bool IsTyping(string key) => _open == key;

        public static string Field(Rect rect, string text, int max, string key, GUIStyle style, bool password = false)
        {
            Ui.Probe(key, rect);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Application.isMobilePlatform) return Bridged(rect, text ?? "", max, key, style, password);
#endif
            GUI.SetNextControlName(key);
            return password ? GUI.PasswordField(rect, text ?? "", '*', max, style) : GUI.TextField(rect, text ?? "", max, style);
        }

        // The same in a GUILayout row, as wide as the row gives (GUILayout.TextField's place).
        public static string LayoutField(string text, int max, string key, bool password = false, params GUILayoutOption[] options)
        {
            var style = GUI.skin.textField;
            var rect = GUILayoutUtility.GetRect(new GUIContent(password ? new string('*', (text ?? "").Length) : text ?? ""), style, options);
            return Field(rect, text, max, key, style, password);
        }

        // Whether Enter ended the typing in this field (once; the page's box on the web, or IMGUI's own field with the
        // keys while it has them).
        public static bool Submitted(string key)
        {
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == key)
            {
                e.Use();
                return true;
            }
            if (_submitted != key) return false;
            _submitted = null;
            return true;
        }

        // Once a frame (Ui.Begin): a field no longer drawn lets go of the keys.
        public static void Frame()
        {
            if (_open != null && Time.frameCount - _drawnFrame > 2) Close();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static string Bridged(Rect rect, string text, int max, string key, GUIStyle style, bool password)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (rect.Contains(e.mousePosition))
                {
                    // Pressed again while typed in: the page gave the canvas the keys as the press went down, so the box
                    // takes them back, the words kept.
                    Open(rect, _open == key ? _text : text, max, key, password);
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
                    if (state != 0 && _endKey == 0) _endKey = state;
                }
                text = _text.Length > max ? _text.Substring(0, max) : _text;
                if (_endKey != 0)
                {
                    // Enter (1) ends it as a press of the field's button would; Esc, Tab or a press elsewhere only end it.
                    if (_endKey == 1) _submitted = key;
                    Close();
                }
            }
            if (e.type == EventType.Repaint) Draw(rect, password ? new string('*', text.Length) : text, style, mine);
            return text;
        }

        static void Open(Rect rect, string text, int max, string key, bool password)
        {
            _open = key;
            _text = text;
            _endKey = 0;
            _readFrame = _drawnFrame = Time.frameCount;
            GUIUtility.keyboardControl = 0;
            // Where the field is on the whole screen in points (Ui.ToPoints, as the playtests' button rects), so to
            // device pixels by Ui.Scale, and those over the page's pixel ratio are the page's CSS pixels (Ui.Scale also
            // has the menu's UI size in it); the canvas fills the page from its top left.
            var at = Ui.ToPoints(rect);
            float css = Ui.Scale / Mathf.Max(0.01f, Ui.PageRatio);
            TextInputOpen(text, max, at.x * css, at.y * css, at.width * css, at.height * css, password ? 1 : 0);
        }
#endif

        static void Close()
        {
            _open = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            _endKey = 0;
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
        static extern void TextInputOpen(string text, int max, float x, float y, float width, float height, int password);

        [DllImport("__Internal")]
        static extern string TextInputValue();

        [DllImport("__Internal")]
        static extern int TextInputState();

        [DllImport("__Internal")]
        static extern void TextInputClose();
#endif
    }
}
