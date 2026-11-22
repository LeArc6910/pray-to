using System.Globalization;

namespace Game.Core
{
    public enum SaveKind
    {
        Character,
        World,
        Map,
    }

    // The names saves go by on a device (Docs/Saving.md): "character:<name>", "world:<name>" and
    // "map:<seed>:<character>" for a character's map of the world of that seed, made by the caves of generator version
    // 1; "map:<seed>.<version>:<character>" for a world of a later generator (2026-11-22, Docs/WorldRework.md 묶음 1c),
    // so the same seed made anew does not take the old map. The version rides on the seed, not after it: a character's
    // name may have colons in it, and an old key never has a dot. The client makes file names of them; a bundle of saves
    // (SaveBundle) carries them along with the data.
    public static class SaveKeys
    {
        const string CharacterPrefix = "character:";
        const string WorldPrefix = "world:";
        const string MapPrefix = "map:";

        public static string Character(string name) => CharacterPrefix + name;

        public static string World(string name) => WorldPrefix + name;

        public static string Map(ulong seed, string character, int version = WorldGenerator.CaveVersion) => version <= WorldGenerator.CaveVersion
            ? $"{MapPrefix}{seed.ToString(CultureInfo.InvariantCulture)}:{character}"
            : $"{MapPrefix}{seed.ToString(CultureInfo.InvariantCulture)}.{version.ToString(CultureInfo.InvariantCulture)}:{character}";

        // The generator version a map key is for (1 for a key with none, as every key was before 2026-11-22), or 0 for
        // what is not a map key.
        public static int MapVersion(string key)
        {
            if (!TryParse(key, out var kind, out _) || kind != SaveKind.Map) return 0;
            string seed = key.Substring(MapPrefix.Length, key.IndexOf(':', MapPrefix.Length) - MapPrefix.Length);
            int dot = seed.IndexOf('.');
            return dot < 0 ? WorldGenerator.CaveVersion : int.Parse(seed.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        // What a key names: the kind of save, and the character's name (for a character or a map) or the world's.
        // False for anything else, an empty name included.
        public static bool TryParse(string key, out SaveKind kind, out string name)
        {
            kind = SaveKind.Character;
            name = null;
            if (key == null) return false;
            if (key.StartsWith(CharacterPrefix, System.StringComparison.Ordinal))
            {
                name = key.Substring(CharacterPrefix.Length);
            }
            else if (key.StartsWith(WorldPrefix, System.StringComparison.Ordinal))
            {
                kind = SaveKind.World;
                name = key.Substring(WorldPrefix.Length);
            }
            else if (key.StartsWith(MapPrefix, System.StringComparison.Ordinal))
            {
                kind = SaveKind.Map;
                int colon = key.IndexOf(':', MapPrefix.Length);
                if (colon < 0) return false;
                string seed = key.Substring(MapPrefix.Length, colon - MapPrefix.Length);
                int dot = seed.IndexOf('.');
                if (dot >= 0)
                {
                    // "<seed>.<version>", a version after the caves'.
                    if (!int.TryParse(seed.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int version) || version <= WorldGenerator.CaveVersion) return false;
                    seed = seed.Substring(0, dot);
                }
                if (!ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;
                name = key.Substring(colon + 1);
            }
            else
            {
                return false;
            }
            return name.Length > 0;
        }

        // The four bytes a save of this kind begins with.
        public static string MagicOf(SaveKind kind) => kind switch
        {
            SaveKind.Character => CharacterSave.Magic,
            SaveKind.World => WorldSave.Magic,
            _ => "PTMP", // MapSave writes it as a number
        };
    }
}
