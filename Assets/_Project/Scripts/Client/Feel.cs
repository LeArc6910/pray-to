using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Client
{
    // Each piece of a blow's feedback on its own switch (Docs/Feel.md, 2026-11-11, the research report's candidate 10):
    // the playtest's "feel" command turns them off one at a time so their pictures and GIFs can be set side by side and
    // a person can judge what each adds. All on in the game. Screen shake and hit-stop also follow the menu's
    // "화면 흔들림" (Settings.ScreenShake), as before.
    static class Feel
    {
        // The striker leans in two pixels and back.
        public static bool Lunge = true;
        // The crescent of a swing.
        public static bool Swing = true;
        // The struck body flashes white.
        public static bool Flash = true;
        // The struck body is pushed back a little and comes back.
        public static bool Knock = true;
        // A star where the blow landed.
        public static bool Impact = true;
        // The damage number rising off the struck.
        public static bool Numbers = true;
        // The screen jolts.
        public static bool Shake = true;
        // The picture holds still a moment on a heavy blow.
        public static bool HitStop = true;
        // The screen's edges glow red when the player is hurt.
        public static bool HurtGlow = true;
        // Bits fly out of what dies.
        public static bool Bits = true;
        // A pixel of squash when struck, of stretch while lunging (2026-11-11).
        public static bool Squash = true;

        static readonly Dictionary<string, Action<bool>> Switches = new Dictionary<string, Action<bool>>
        {
            ["lunge"] = on => Lunge = on,
            ["swing"] = on => Swing = on,
            ["flash"] = on => Flash = on,
            ["knock"] = on => Knock = on,
            ["impact"] = on => Impact = on,
            ["numbers"] = on => Numbers = on,
            ["shake"] = on => Shake = on,
            ["hitstop"] = on => HitStop = on,
            ["hurtglow"] = on => HurtGlow = on,
            ["bits"] = on => Bits = on,
            ["squash"] = on => Squash = on,
        };

        public static IEnumerable<string> Names => Switches.Keys;

        // Turns one piece (or "all") on or off. False for a name it does not know.
        public static bool Set(string name, bool on)
        {
            if (name == "all")
            {
                foreach (var set in Switches.Values) set(on);
                return true;
            }
            if (!Switches.TryGetValue(name, out var one)) return false;
            one(on);
            return true;
        }

        // "lunge swing …" of the pieces that are off, for the playtest's report.
        public static string Off => string.Join(" ", new (string Name, bool On)[]
        {
            ("lunge", Lunge), ("swing", Swing), ("flash", Flash), ("knock", Knock), ("impact", Impact), ("numbers", Numbers),
            ("shake", Shake), ("hitstop", HitStop), ("hurtglow", HurtGlow), ("bits", Bits), ("squash", Squash),
        }.Where(p => !p.On).Select(p => p.Name));
    }
}
