using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The characters and worlds on this device for the title's lists (Docs/TitleRework.md 4-2): each with its summary read
    // from the save's bytes without building anything (CharacterSave.ReadSummary, WorldSave.ReadSummary), the most
    // recently played first. A save that cannot be read is listed as broken with the reason, never thrown.
    static class SaveList
    {
        public sealed class Entry<T> where T : class
        {
            public Entry(string name, string key, T summary, string broken)
            {
                Name = name;
                Key = key;
                Summary = summary;
                Broken = broken;
            }

            public string Name { get; }
            // The save's key (SaveKeys.Character, SaveKeys.World).
            public string Key { get; }
            // Null when broken.
            public T Summary { get; }
            // Why it cannot be read ("" when it can): show it, and offer [지우기].
            public string Broken { get; }
            public bool IsBroken => Summary == null;
            // When last played (seconds since 1970), 0 for unknown or broken.
            public long LastPlayedAt => Summary switch
            {
                CharacterSummary character => character.LastPlayedAt,
                WorldSummary world => world.LastPlayedAt,
                _ => 0,
            };
        }

        public static List<Entry<CharacterSummary>> Characters() => Characters(SaveStore.List());

        public static List<Entry<WorldSummary>> Worlds() => Worlds(SaveStore.List());

        public static List<Entry<CharacterSummary>> Characters(IEnumerable<string> keys) =>
            Sorted(keys, SaveKind.Character, (data, name) => CharacterSave.ReadSummary(data, name));

        public static List<Entry<WorldSummary>> Worlds(IEnumerable<string> keys) =>
            Sorted(keys, SaveKind.World, (data, name) => WorldSave.ReadSummary(data, name));

        // Most recently played first; those not known (old saves) after them by name; broken ones last.
        static List<Entry<T>> Sorted<T>(IEnumerable<string> keys, SaveKind kind, Func<byte[], string, T> read) where T : class
        {
            var entries = new List<Entry<T>>();
            foreach (string key in keys)
            {
                if (!SaveKeys.TryParse(key, out var found, out string name) || found != kind) continue;
                T summary = null;
                string broken = "";
                try
                {
                    byte[] data = SaveStore.Read(key);
                    if (data == null) continue;
                    summary = read(data, name);
                }
                catch (InvalidDataException e)
                {
                    broken = e.Message;
                }
                catch (IOException e)
                {
                    broken = e.Message;
                }
                entries.Add(new Entry<T>(name, key, summary, broken));
            }
            return entries.OrderBy(e => e.IsBroken).ThenByDescending(e => e.LastPlayedAt).ThenBy(e => e.Name, StringComparer.Ordinal).ToList();
        }

        // What this device keeps for a character besides its save, by its name: the quick slots and their locks, the skill
        // order, how far the guide has come.
        public static IEnumerable<string> CharacterPrefs(string name) => new[]
        {
            "hotbar." + name, "locks." + name, "skillorder." + name, "skillorder3." + name, "guide." + name, "guidepassed." + name,
        };

        // Deletes a character with its maps of every world (SaveTransfer.CharacterSaves) and what this device kept for it.
        public static void DeleteCharacter(string name)
        {
            foreach (string key in SaveTransfer.CharacterSaves(name, SaveStore.List()).Append(SaveStore.CharacterKey(name)).Distinct()) SaveStore.Delete(key);
            foreach (string pref in CharacterPrefs(name)) Prefs.Delete(pref);
        }

        // Deletes a world. The characters' maps of it stay with them (they are the characters' own, by the world's seed).
        public static void DeleteWorld(string name) => SaveStore.Delete(SaveStore.WorldKey(name));
    }
}
