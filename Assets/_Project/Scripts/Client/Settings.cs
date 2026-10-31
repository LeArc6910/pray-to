using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // Choices a player makes in the menu that stay on this device (PlayerPrefs, which a browser keeps in its own
    // storage). Read once, then kept here.
    static class Settings
    {
        static readonly Choice DarknessChoice = new Choice("settings.darkness", true);
        // Before there was a volume, sound was only on or off; that choice decides where the volume starts.
        static readonly Choice SoundChoice = new Choice("settings.sound", true);
        const string VolumeKey = "settings.volume";
        public const int MaxVolume = 4;
        static int? _volume;
        static readonly Choice DevInfoChoice = new Choice("settings.devinfo", false);

        // The dark of the caves and the lights in it. Off, everything is lit as before.
        public static bool Darkness
        {
            get => DarknessChoice.Value;
            set => DarknessChoice.Value = value;
        }

        // How loud the sound effects are, in quarters: 0 is off, MaxVolume full.
        public static int Volume
        {
            get => _volume ??= Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, SoundChoice.Value ? MaxVolume : 0), 0, MaxVolume);
            set
            {
                value = Mathf.Clamp(value, 0, MaxVolume);
                if (_volume == value) return;
                _volume = value;
                PlayerPrefs.SetInt(VolumeKey, value);
                PlayerPrefs.Save();
            }
        }

        public static bool Sound => Volume > 0;

        // "끔", "25%" … "100%".
        public static string VolumeText => Volume == 0 ? "끔" : $"{Volume * 100 / MaxVolume}%";

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

    // What the keys a player may change do. Moving (WASD and the arrows), attacking (space) and Esc keep theirs.
    enum KeyAction
    {
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        Potion,
        Bag,
        Map,
    }

    // Which key does what, changed in the menu and kept on this device (PlayerPrefs, by key name). Every action has
    // one key and no key does two things: giving an action a key another one has swaps them.
    static class KeyBindings
    {
        public static readonly KeyAction[] Actions = (KeyAction[])Enum.GetValues(typeof(KeyAction));
        static readonly Key[] Defaults = { Key.Q, Key.E, Key.R, Key.F, Key.C, Key.Tab, Key.M };
        static readonly Key[] Fixed =
        {
            Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Space, Key.Escape,
        };

        static Key[] _keys;

        // The action waiting for its new key, or null.
        public static KeyAction? Capturing { get; private set; }
        // What the last change did, for the menu to show.
        public static string Message { get; private set; } = "";

        static Key[] Keys => _keys ??= Load();

        public static Key KeyFor(KeyAction action) => Keys[(int)action];

        public static string Name(KeyAction action) => KeyName(KeyFor(action));

        public static bool IsBound(Key key) => Array.IndexOf(Keys, key) >= 0;

        // Whether the action's key went down this frame. Nothing counts while a key is being chosen.
        public static bool Pressed(KeyAction action)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && Capturing == null && keyboard[KeyFor(action)].wasPressedThisFrame;
        }

        public static string ActionName(KeyAction action) => action switch
        {
            KeyAction.Skill1 => "스킬 1",
            KeyAction.Skill2 => "스킬 2",
            KeyAction.Skill3 => "스킬 3",
            KeyAction.Skill4 => "스킬 4",
            KeyAction.Potion => "물약",
            KeyAction.Bag => "가방",
            _ => "지도",
        };

        // Short enough for a skill slot's corner: "1" for Digit1, "Num1", "LShift".
        public static string KeyName(Key key)
        {
            string name = key.ToString();
            if (name.StartsWith("Digit")) return name.Substring(5);
            if (name.StartsWith("Numpad")) return "Num" + name.Substring(6);
            if (name.StartsWith("Left")) return "L" + name.Substring(4);
            if (name.StartsWith("Right")) return "R" + name.Substring(5);
            return name;
        }

        public static void StartCapture(KeyAction action)
        {
            Capturing = action;
            Message = "";
        }

        // Once per Update: while an action waits for a key, the next key pressed becomes its key. Esc stops waiting.
        public static void Poll()
        {
            if (Capturing is not KeyAction action) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                var key = control.keyCode;
                if (key == Key.Escape)
                {
                    Capturing = null;
                    return;
                }
                if (Array.IndexOf(Fixed, key) >= 0)
                {
                    Message = $"{KeyName(key)}는 이동·공격에 써서 바꿀 수 없다";
                    return;
                }
                Bind(action, key);
                Capturing = null;
                return;
            }
        }

        public static void ResetAll()
        {
            Capturing = null;
            _keys = (Key[])Defaults.Clone();
            foreach (var action in Actions) Save(action);
            PlayerPrefs.Save();
            Message = "처음 키로 돌렸다";
        }

        static void Bind(KeyAction action, Key key)
        {
            var old = Keys[(int)action];
            int other = Array.IndexOf(Keys, key);
            Message = "";
            if (other >= 0 && other != (int)action)
            {
                Keys[other] = old;
                Save(Actions[other]);
                Message = $"{ActionName(Actions[other])}은 {KeyName(old)}로 바꿨다";
            }
            Keys[(int)action] = key;
            Save(action);
            PlayerPrefs.Save();
        }

        static void Save(KeyAction action) => PlayerPrefs.SetString("keys." + action, Keys[(int)action].ToString());

        // Saved keys, or the defaults for any missing, unknown or fixed. If two actions end up on one key, all go back
        // to the defaults.
        static Key[] Load()
        {
            var keys = new Key[Actions.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                string saved = PlayerPrefs.GetString("keys." + Actions[i], "");
                bool known = Enum.TryParse(saved, out Key key) && key != Key.None && Array.IndexOf(Fixed, key) < 0;
                keys[i] = known ? key : Defaults[i];
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (Array.IndexOf(keys, keys[i]) != i) return (Key[])Defaults.Clone();
            }
            return keys;
        }
    }
}
