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
    // "map:<seed>:<character>" for a character's map of the world of that seed. The client makes file names of them;
    // a bundle of saves (SaveBundle) carries them along with the data.
    public static class SaveKeys
    {
        const string CharacterPrefix = "character:";
        const string WorldPrefix = "world:";
        const string MapPrefix = "map:";

        public static string Character(string name) => CharacterPrefix + name;

        public static string World(string name) => WorldPrefix + name;

        public static string Map(ulong seed, string character) => $"{MapPrefix}{seed.ToString(CultureInfo.InvariantCulture)}:{character}";

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
