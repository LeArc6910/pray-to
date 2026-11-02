using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Where saves live: files under persistentDataPath/saves on every platform (DeviceFiles; in a browser that is
    // IndexedDB). Saves are named by a key such as "world:내 월드" (SaveKeys); the key goes into file names as UTF-8
    // hex, so any name works. Web saves from before 2026-11-02 sat in PlayerPrefs ("save.<hex>"); one still there
    // becomes a file the first time it is asked for. See Docs/Saving.md.
    static class SaveStore
    {
        public static string CharacterKey(string name) => SaveKeys.Character(name);

        public static string WorldKey(string name) => SaveKeys.World(name);

        // The map a character has explored in the world of this seed (MapSave).
        public static string MapKey(ulong seed, string character) => SaveKeys.Map(seed, character);

        // The keys of every save on this device, in no set order. Only files: a save still in an old build's
        // PlayerPrefs is not listed until it is asked for by name (it then becomes a file).
        public static List<string> List()
        {
            var keys = new List<string>();
            if (!Directory.Exists(Folder)) return keys;
            foreach (string path in Directory.GetFiles(Folder, "*.sav"))
            {
                // Not one being written (DeviceFiles.Write), which Windows would match too.
                if (!path.EndsWith(".sav", StringComparison.Ordinal)) continue;
                string key = Unhex(Path.GetFileNameWithoutExtension(path));
                if (key != null && SaveKeys.TryParse(key, out _, out _)) keys.Add(key);
            }
            return keys;
        }

        public static bool Exists(string key)
        {
            MoveFromPlayerPrefs(key);
            return File.Exists(PathOf(key));
        }

        // Null when there is no such save.
        public static byte[] Read(string key)
        {
            MoveFromPlayerPrefs(key);
            string path = PathOf(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public static void Write(string key, byte[] data) => DeviceFiles.Write(PathOf(key), data);

        // Also forgets a copy left in PlayerPrefs, or it would come back.
        public static void Delete(string key)
        {
            DeviceFiles.Delete(PathOf(key));
            if (!PlayerPrefs.HasKey(OldKey(key))) return;
            PlayerPrefs.DeleteKey(OldKey(key));
            PlayerPrefs.Save();
        }

        static string Folder => DeviceFiles.PathOf("saves");

        internal static string PathOf(string key) => Path.Combine(Folder, Hex(key) + ".sav");

        // Where a web save was before it was a file: base64 in PlayerPrefs.
        internal static string OldKey(string key) => "save." + Hex(key);

        // A save still in PlayerPrefs becomes a file (unless the file is already there) and leaves PlayerPrefs.
        static void MoveFromPlayerPrefs(string key)
        {
            string old = OldKey(key);
            if (!PlayerPrefs.HasKey(old)) return;
            if (!File.Exists(PathOf(key)))
            {
                string text = PlayerPrefs.GetString(old, "");
                if (text.Length == 0) return;
                byte[] data;
                try
                {
                    data = Convert.FromBase64String(text);
                }
                catch (FormatException)
                {
                    Debug.LogWarning($"Save {key} in PlayerPrefs is not base64; left where it is.");
                    return;
                }
                DeviceFiles.Write(PathOf(key), data);
            }
            PlayerPrefs.DeleteKey(old);
            PlayerPrefs.Save();
        }

        static string Hex(string key)
        {
            var hex = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(key)) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        // The key a file name came from, or null for a name Hex did not make.
        static string Unhex(string hex)
        {
            if (hex.Length == 0 || hex.Length % 2 != 0) return null;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i])) return null;
            }
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
