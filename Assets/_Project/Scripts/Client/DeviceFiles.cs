using System.IO;
using UnityEngine;

namespace Game.Client
{
    // The files this device keeps: saves (SaveStore) and small choices (Prefs), under Application.persistentDataPath
    // on every platform. In a browser that folder lives in IndexedDB; the page's loader has Unity carry each change
    // there (autoSyncPersistentDataPath in Assets/WebGLTemplates/Fit/index.html).
    // Not PlayerPrefs: a web build keeps every PlayerPrefs key in one file, written whole by whatever sets any key
    // (Unity itself sets some as it starts). After the 2026-11-02 deploy that file held only Unity's own keys and the
    // saves in it were gone. As files of their own, saves are written only by the game and only when they change
    // (Docs/Saving.md).
    static class DeviceFiles
    {
        public static string PathOf(string name) => Path.Combine(Application.persistentDataPath, name);

        // Written beside and then moved over, so a crash mid-write cannot leave half a file.
        public static void Write(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path + ".new", data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".new", path);
        }

        public static void Delete(string path)
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
        }
    }
}
