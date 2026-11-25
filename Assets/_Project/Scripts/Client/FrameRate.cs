using UnityEngine;

namespace Game.Client
{
    // How many frames a second the game asks for (Docs/NativeBuild.md). A phone's own build runs at 30 unless told
    // otherwise, so it asks for 60, or 30 when the menu's "프레임" says so to spare the battery (Settings.SmoothFrames).
    // A PC's own build waits for the display's refresh (vSync), as fast as the screen shows and no faster. A browser
    // draws when the page does (requestAnimationFrame), so the web demo is left as it was; so is the editor.
    static class FrameRate
    {
        // Whether the menu offers the choice: only a phone's own build has one to make.
        public static bool Choosable
        {
            get
            {
#if UNITY_EDITOR || UNITY_WEBGL
                return false;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        // At the start (GameRoot.Awake) and whenever the menu's choice changes.
        public static void Apply()
        {
#if !UNITY_EDITOR && !UNITY_WEBGL
            if (Application.isMobilePlatform)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Settings.SmoothFrames ? 60 : 30;
            }
            else
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }
#endif
        }

        // What the game asks for now, for the device panel (DevInfo): "60", "30", "화면 주사율", "브라우저".
        public static string Described
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return "브라우저";
#else
                if (Application.targetFrameRate > 0) return Application.targetFrameRate.ToString();
                return QualitySettings.vSyncCount > 0 ? "화면 주사율" : "제한 없음";
#endif
            }
        }
    }
}
