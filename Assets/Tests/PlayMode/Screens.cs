using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.Client.Tests
{
    // Screenshots of the play mode scenes, so the screen can be checked without anyone playing: the local one-line
    // command runs the tests in the Screens category once more in a visible editor (in batch mode nothing draws the
    // game view, nor runs OnGUI, so there is nothing to take) and pushes the pictures to the local-screens branch,
    // where the cloud session looks at them (DevTools/Local/setup.ps1, LOCAL_TASKS.md).
    static class Screens
    {
        public const string Category = "Screens";
        const int MaxWidth = 960;

        public static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "screens");

        // Saves what the game view shows now as Logs/screens/<name>.jpg, at most MaxWidth wide.
        public static IEnumerator Take(string name)
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D small = null;
            RenderTexture target = null;
            try
            {
                float scale = Mathf.Min(1, MaxWidth / (float)shot.width);
                int width = Mathf.Max(1, Mathf.RoundToInt(shot.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(shot.height * scale));
                target = RenderTexture.GetTemporary(width, height, 0);
                Graphics.Blit(shot, target);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                small.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(Path.Combine(Folder, name + ".jpg"), small.EncodeToJPG(80));
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                Object.Destroy(shot);
                if (small != null) Object.Destroy(small);
            }
        }
    }
}
