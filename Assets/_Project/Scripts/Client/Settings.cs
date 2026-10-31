using UnityEngine;

namespace Game.Client
{
    // Choices a player makes in the menu that stay on this device (PlayerPrefs, which a browser keeps in its own
    // storage). Read once, then kept here.
    static class Settings
    {
        static readonly Choice DarknessChoice = new Choice("settings.darkness", true);
        static readonly Choice SoundChoice = new Choice("settings.sound", true);
        static readonly Choice DevInfoChoice = new Choice("settings.devinfo", false);

        // The dark of the caves and the lights in it. Off, everything is lit as before.
        public static bool Darkness
        {
            get => DarknessChoice.Value;
            set => DarknessChoice.Value = value;
        }

        // The sound effects.
        public static bool Sound
        {
            get => SoundChoice.Value;
            set => SoundChoice.Value = value;
        }

        // The panel of numbers for checking the game on a device (DevInfo).
        public static bool DevInfo
        {
            get => DevInfoChoice.Value;
            set => DevInfoChoice.Value = value;
        }

        // One on-or-off choice, saved under its key when it changes.
        sealed class Choice
        {
            readonly string _key;
            readonly bool _default;
            bool? _value;

            public Choice(string key, bool fallback)
            {
                _key = key;
                _default = fallback;
            }

            public bool Value
            {
                get => _value ??= PlayerPrefs.GetInt(_key, _default ? 1 : 0) == 1;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    PlayerPrefs.SetInt(_key, value ? 1 : 0);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
