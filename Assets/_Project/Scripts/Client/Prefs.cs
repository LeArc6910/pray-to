using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Client
{
    // The small choices kept on this device: settings, keys, quick slots, skill order, how far the guide has come
    // (Settings, KeyBindings, Hotbar, GameRoot). One text file, persistentDataPath/settings.txt, of key and value
    // lines (DeviceFiles), read once and written whole on each change. They were in PlayerPrefs before 2026-11-02;
    // a key the file does not have yet is taken from there and moved over.
    static class Prefs
    {
        const string FileName = "settings.txt";
        static Dictionary<string, string> _values;

        static Dictionary<string, string> Values => _values ??= Load();

        public static bool Has(string key) => Values.ContainsKey(key) || MoveFromPlayerPrefs(key);

        public static string Get(string key, string fallback) => Has(key) ? Values[key] : fallback;

        public static int Get(string key, int fallback) =>
            Has(key) && int.TryParse(Values[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        public static void Set(string key, string value)
        {
            if (Values.TryGetValue(key, out string old) && old == value) return;
            Values[key] = value;
            Write();
        }

        public static void Set(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

        public static void Delete(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            if (Values.Remove(key)) Write();
        }

        // Reads the file again (tests).
        internal static void Reload() => _values = null;

        static string PathOf => DeviceFiles.PathOf(FileName);

        static Dictionary<string, string> Load()
        {
            var values = new Dictionary<string, string>();
            if (!File.Exists(PathOf)) return values;
            foreach (string line in File.ReadAllText(PathOf, Encoding.UTF8).Split('\n'))
            {
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                values[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
            }
            return values;
        }

        static void Write()
        {
            var text = new StringBuilder();
            foreach (var pair in Values) text.Append(Escape(pair.Key)).Append('\t').Append(Escape(pair.Value)).Append('\n');
            DeviceFiles.Write(PathOf, Encoding.UTF8.GetBytes(text.ToString()));
        }

        // An old PlayerPrefs value of the key, a string or a whole number as it was set, into the file.
        static bool MoveFromPlayerPrefs(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return false;
            const string NotText = "\u0001";
            string value = PlayerPrefs.GetString(key, NotText);
            if (value == NotText) value = PlayerPrefs.GetInt(key, 0).ToString(CultureInfo.InvariantCulture);
            Values[key] = value;
            Write();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return true;
        }

        // Line breaks, tabs and backslashes in keys and values (a quick slot list has line breaks) as \n, \t and \\.
        static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t");

        static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0) return text;
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\\' || i + 1 == text.Length)
                {
                    result.Append(c);
                    continue;
                }
                char next = text[++i];
                result.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
            }
            return result.ToString();
        }
    }
}
