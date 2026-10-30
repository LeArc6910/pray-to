using UnityEngine;

namespace Game.Client
{
    // Choices a player makes in the menu that stay on this device (PlayerPrefs, which a browser keeps in its own
    // storage). Read once, then kept here.
    static class Settings
    {
        const string DarknessKey = "settings.darkness";

        static bool? _darkness;

        // The dark of the caves and the lights in it. Off, everything is lit as before.
        public static bool Darkness
        {
            get => _darkness ??= PlayerPrefs.GetInt(DarknessKey, 1) == 1;
            set
            {
                if (_darkness == value) return;
                _darkness = value;
                PlayerPrefs.SetInt(DarknessKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
