using Game.Core;

namespace Game.Client
{
    // The words on screen in the player's language (Docs/Strings.md, 2026-11-11, the research report's candidate 17).
    // Each call keeps the Korean beside its id, so a line the sheet lacks, or a game without the sheet, shows the
    // Korean. The menu is the first screen read through it; the rest follows screen by screen.
    static class Strings
    {
        static StringData _data = StringData.Empty;

        public static void Use(StringData data) => _data = data ?? StringData.Empty;

        // The line in the chosen language, else the Korean given.
        public static string T(string id, string korean) => _data.Get(id, Settings.Language, korean);

        // "켜짐" or "꺼짐" in the chosen language.
        public static string OnOff(bool on) => on ? T("common.on", "켜짐") : T("common.off", "꺼짐");

        // Whether there is another language with words in it to choose.
        public static bool HasOthers => _data.Languages.Count > 1 && _data.Filled(_data.Languages[1]) > 0;

        // The next language the sheet has, and from the last back to Korean.
        public static string NextLanguage()
        {
            var languages = _data.Languages;
            int at = -1;
            for (int i = 0; i < languages.Count; i++) if (languages[i] == Settings.Language) at = i;
            return languages[(at + 1) % languages.Count];
        }

        // The language's own name for itself.
        public static string NameOf(string language) => language switch
        {
            "en" => "English",
            "zh" => "简体中文",
            "ja" => "日本語",
            _ => "한국어",
        };
    }
}
