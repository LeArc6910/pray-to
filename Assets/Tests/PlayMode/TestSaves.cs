using System;
using System.Linq;

namespace Game.Client.Tests
{
    // The play mode tests run against this machine's real save folder (SaveStore), so each keeps to a character and
    // world name of its own and forgets everything under it before and after (2026-11-11: a test playing as the default
    // "플레이어" left a warrior and fire mage behind, which the next run's new character took up).
    static class TestSaves
    {
        public static void Forget(string name)
        {
            SaveStore.Delete(SaveStore.WorldKey(name));
            SaveStore.Delete(SaveStore.CharacterKey(name));
            // The explored maps, one per world seed played (SaveKeys.Map).
            foreach (string key in SaveStore.List().Where(k => k.EndsWith(":" + name, StringComparison.Ordinal)))
                SaveStore.Delete(key);
            Prefs.Delete("hotbar." + name);
            Prefs.Delete("skillorder3." + name);
        }
    }
}
