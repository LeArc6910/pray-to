using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // Choices a player makes in the menu that stay on this device (Prefs). Read once, then kept here.
    static class Settings
    {
        static readonly Choice DarknessChoice = new Choice("settings.darkness", true);
        // Before there was a volume, sound was only on or off; that choice decides where the volume starts.
        static readonly Choice SoundChoice = new Choice("settings.sound", true);
        const string VolumeKey = "settings.volume";
        public const int MaxVolume = 4;
        static int? _volume;
        const string MusicKey = "settings.music";
        static int? _music;
        static readonly Choice DevInfoChoice = new Choice("settings.devinfo", false);
        static readonly Choice PixelFontChoice = new Choice("settings.pixelfont", true);
        static readonly Choice GuideChoice = new Choice("settings.guide", true);
        static readonly Choice ScreenShakeChoice = new Choice("settings.shake", true);

        // The dark of the caves and the lights in it. Off, everything is lit as before.
        public static bool Darkness
        {
            get => DarknessChoice.Value;
            set => DarknessChoice.Value = value;
        }

        // How loud the sound effects are, in quarters: 0 is off, MaxVolume full.
        public static int Volume
        {
            get => _volume ??= Mathf.Clamp(Prefs.Get(VolumeKey, SoundChoice.Value ? MaxVolume : 0), 0, MaxVolume);
            set
            {
                value = Mathf.Clamp(value, 0, MaxVolume);
                if (_volume == value) return;
                _volume = value;
                Prefs.Set(VolumeKey, value);
            }
        }

        public static bool Sound => Volume > 0;

        // How loud the background music is, in quarters like Volume. Half to begin with.
        public static int MusicVolume
        {
            get => _music ??= Mathf.Clamp(Prefs.Get(MusicKey, MaxVolume / 2), 0, MaxVolume);
            set
            {
                value = Mathf.Clamp(value, 0, MaxVolume);
                if (_music == value) return;
                _music = value;
                Prefs.Set(MusicKey, value);
            }
        }

        public static string MusicVolumeText => MusicVolume == 0 ? "끔" : $"{MusicVolume * 100 / MaxVolume}%";

        // "끔", "25%" … "100%".
        public static string VolumeText => Volume == 0 ? "끔" : $"{Volume * 100 / MaxVolume}%";

        // The panel of numbers for checking the game on a device (DevInfo).
        public static bool DevInfo
        {
            get => DevInfoChoice.Value;
            set => DevInfoChoice.Value = value;
        }

        // The pixel font (Galmuri11 Bold) for every screen's text; off, the smooth one (Noto Sans KR) as before (Ui).
        public static bool PixelFont
        {
            get => PixelFontChoice.Value;
            set => PixelFontChoice.Value = value;
        }

        // The goals for new players in the HUD (Docs/Guide.md). Off, they are not shown or announced, but goals met
        // still count and still bring their rewards, so turning it back on carries on where the character is.
        public static bool Guide
        {
            get => GuideChoice.Value;
            set => GuideChoice.Value = value;
        }

        // The view jolting when the player is hurt or something heavy lands nearby (WorldView).
        public static bool ScreenShake
        {
            get => ScreenShakeChoice.Value;
            set => ScreenShakeChoice.Value = value;
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
                get => _value ??= Prefs.Get(_key, _default ? 1 : 0) == 1;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    Prefs.Set(_key, value ? 1 : 0);
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
        Hotbar1,
        Hotbar2,
        Hotbar3,
        Hotbar4,
        Hotbar5,
        Hotbar6,
        Machine,
    }

    // Which key does what, changed in the menu and kept on this device (Prefs, by key name). Every action has
    // one key and no key does two things: giving an action a key another one has swaps them.
    static class KeyBindings
    {
        public static readonly KeyAction[] Actions = (KeyAction[])Enum.GetValues(typeof(KeyAction));
        static readonly Key[] Defaults =
        {
            Key.Q, Key.E, Key.R, Key.F, Key.C, Key.Tab, Key.M, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.G,
        };
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

        public static string Name(KeyAction action) => KeyFor(action) == Key.None ? "-" : KeyName(KeyFor(action));

        public static bool IsBound(Key key) => Array.IndexOf(Keys, key) >= 0;

        // Whether the action's key went down this frame. Nothing counts while a key is being chosen.
        public static bool Pressed(KeyAction action)
        {
            var keyboard = Keyboard.current;
            var key = KeyFor(action);
            return keyboard != null && Capturing == null && key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        public static string ActionName(KeyAction action) => action switch
        {
            KeyAction.Skill1 => "스킬 1",
            KeyAction.Skill2 => "스킬 2",
            KeyAction.Skill3 => "스킬 3",
            KeyAction.Skill4 => "스킬 4",
            KeyAction.Potion => "물약",
            KeyAction.Bag => "가방",
            KeyAction.Map => "지도",
            KeyAction.Machine => "기계 켜기/끄기",
            _ => $"퀵슬롯 {action - KeyAction.Hotbar1 + 1}",
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
                Message = old == Key.None ? $"{ActionName(Actions[other])}은 키가 없어졌다" : $"{ActionName(Actions[other])}은 {KeyName(old)}로 바꿨다";
            }
            Keys[(int)action] = key;
            Save(action);
        }

        static void Save(KeyAction action) => Prefs.Set("keys." + action, Keys[(int)action].ToString());

        // Saved keys, or the defaults for any missing, unknown or fixed. An action whose key an earlier one already has
        // (a key chosen for a skill before the quick slots took the digits, say) is left without one ("-") until the
        // player gives it one.
        static Key[] Load()
        {
            var keys = new Key[Actions.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                string saved = Prefs.Get("keys." + Actions[i], "");
                bool known = Enum.TryParse(saved, out Key key) && key != Key.None && Array.IndexOf(Fixed, key) < 0;
                keys[i] = known ? key : Defaults[i];
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != Key.None && Array.IndexOf(keys, keys[i]) != i) keys[i] = Key.None;
            }
            return keys;
        }
    }
}
