using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Client
{
    // Where saves live: files under persistentDataPath/saves on PC, the browser's own storage (PlayerPrefs, kept in
    // IndexedDB) on the web, where a file would not outlast the page. Saves are named by a key such as
    // "world:내 월드"; the key goes into file names as UTF-8 hex, so any name works. See Docs/Saving.md.
    static class SaveStore
    {
        public static string CharacterKey(string name) => "character:" + name;

        public static string WorldKey(string name) => "world:" + name;

        public static bool Exists(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return PlayerPrefs.HasKey(PrefsKey(key));
#else
            return File.Exists(PathOf(key));
#endif
        }

        // Null when there is no such save.
        public static byte[] Read(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string text = PlayerPrefs.GetString(PrefsKey(key), "");
            return text.Length == 0 ? null : Convert.FromBase64String(text);
#else
            string path = PathOf(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
#endif
        }

        public static void Write(string key, byte[] data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PrefsKey(key), Convert.ToBase64String(data));
            PlayerPrefs.Save();
#else
            string path = PathOf(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Written beside and then moved over, so a crash mid-write cannot leave half a save.
            File.WriteAllBytes(path + ".new", data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".new", path);
#endif
        }

        public static void Delete(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey(PrefsKey(key));
            PlayerPrefs.Save();
#else
            string path = PathOf(key);
            if (File.Exists(path)) File.Delete(path);
#endif
        }

        static string Hex(string key)
        {
            var hex = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(key)) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static string PrefsKey(string key) => "save." + Hex(key);
#else
        static string PathOf(string key) => Path.Combine(Application.persistentDataPath, "saves", Hex(key) + ".sav");
#endif
    }
}
