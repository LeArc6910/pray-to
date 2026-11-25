using System;
using System.Globalization;
using System.Text;

namespace Game.Core
{
    // What the new world page offers (Docs/TitleRework.md 4-2, 묶음 T3): a name made of two words picked at random (the
    // dice), and the seed from what was typed into its field: nothing for a random one, a number as it is, any other text
    // hashed the same way every time, so a seed told to a friend makes the same world.
    public static class NewWorld
    {
        // The longest name the field takes.
        public const int NameLength = 24;
        public const int SeedLength = 20;

        static readonly string[] FirstWords = { "잠든", "깊은", "푸른", "붉은", "고요한", "잊힌", "빛나는", "오래된", "숨은", "젖은", "따뜻한", "차가운", "무너진", "작은", "먼", "검은" };
        static readonly string[] LastWords = { "동굴", "굴", "광산", "골짜기", "샘", "뿌리", "등불", "왕국", "숲", "호수", "틈", "바위", "길", "마을", "심장", "땅" };
        static readonly string[] FirstWordsEn = { "Sleeping", "Deep", "Blue", "Red", "Quiet", "Lost", "Shining", "Old", "Hidden", "Wet", "Warm", "Cold", "Fallen", "Little", "Far", "Black" };
        static readonly string[] LastWordsEn = { "Cave", "Burrow", "Mine", "Hollow", "Spring", "Roots", "Lantern", "Kingdom", "Wood", "Lake", "Rift", "Rock", "Way", "Village", "Heart", "Land" };

        // Two words picked at random: "잠든 광산", or "Sleeping Mine" in English.
        public static string RandomName(GameRandom random, bool english = false)
        {
            var firsts = english ? FirstWordsEn : FirstWords;
            var lasts = english ? LastWordsEn : LastWords;
            return firsts[random.Next(firsts.Length)] + " " + lasts[random.Next(lasts.Length)];
        }

        // The smallest and largest random seed: nine digits, short enough to read out and type again (2026-11-26; the
        // clock's ticks gave eighteen).
        public const ulong RandomSeedLeast = 100_000_000;
        public const ulong RandomSeedMost = 999_999_999;

        // A random seed of nine digits from any number that differs each time (the caller's clock: Core reads none),
        // mixed (SplitMix64) so close clock readings give far-apart seeds. Once picked it is an ordinary number seed.
        public static ulong RandomSeed(ulong entropy)
        {
            ulong z = unchecked(entropy + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            return RandomSeedLeast + z % (RandomSeedMost - RandomSeedLeast + 1);
        }

        // How the world list shows the seed: what was typed when it was words ("잠든 광산"), else the number.
        public static string SeedLabel(string typed, ulong seed)
        {
            string text = (typed ?? "").Trim();
            if (text.Length > 0 && !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return text;
            return seed.ToString(CultureInfo.InvariantCulture);
        }

        // The seed from what was typed: random when it is empty, the number when it is one, else the text hashed
        // (FNV-1a over its UTF-8 bytes, so every device makes the same of it).
        public static ulong Seed(string typed, ulong random)
        {
            string text = (typed ?? "").Trim();
            if (text.Length == 0) return random;
            if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number)) return number;
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
