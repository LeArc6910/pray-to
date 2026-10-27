using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Client
{
    // Shared setup for the IMGUI screens. Layout is done in points, not pixels: a browser draws the game at the
    // device's full pixel density (about 3 pixels per point on phones), which would leave 13-pixel text unreadably
    // small, so the GUI matrix scales points up by that ratio. Elsewhere one point is one pixel.
    // The font travels with the game because web builds cannot use the system's fonts (no Korean otherwise).
    static class Ui
    {
        const string FontPath = "Fonts/NotoSansKR-Game";

        static Font _font;

        public static float Scale { get; private set; } = 1;

        // Screen size in points.
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        // Call at the start of every OnGUI.
        public static void Begin()
        {
            if (_font == null) _font = Resources.Load<Font>(FontPath);
            if (_font != null) GUI.skin.font = _font;
            Scale = PixelRatio();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
        }

        // Screen pixels as cameras and touches give them (origin bottom left) to GUI points (origin top left).
        public static Vector2 FromScreen(Vector2 pixels) => new Vector2(pixels.x / Scale, (Screen.height - pixels.y) / Scale);

        static float PixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Clamp(GetDevicePixelRatio(), 1, 4);
#else
            return 1;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Plugins/WebGL/Display.jslib
        [DllImport("__Internal")]
        static extern float GetDevicePixelRatio();
#endif
    }
}
