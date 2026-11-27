namespace Game.Core
{
    // How a character looks, picked when it is made (2026-11-25, the owner: "캐릭과 맵을 분리해서 캐릭 리스트에서 선택",
    // character making with a name and a look, Docs/TitleRework.md). Only choices are kept here, each an index into a set
    // the client draws (Client/Art/HeroArt: hair styles, hair colours, skin tones, eye colours, shirt and trouser colours),
    // so the rules never depend on it. It rides in the character save and, packed, to the other players.
    public readonly struct CharacterLook
    {
        public const int HairStyles = 8, HairColors = 9, Skins = 5, EyeColors = 6, ShirtColors = 12, PantsColors = 12, Genders = 2;
        // The genders (2026-11-28, the owner: "add a gender, and let it change how the character looks"): 0 the hero as
        // first drawn, 1 the woman (Docs/Gender.md). Every hair style is open to both; each has its own to start from.
        public const int Male = 0, Female = 1;

        public byte Hair { get; }
        public byte HairColor { get; }
        public byte Skin { get; }
        public byte Eyes { get; }
        public byte Shirt { get; }
        public byte Pants { get; }
        public byte Gender { get; }

        public bool IsFemale => Gender == Female;

        public CharacterLook(int hair, int hairColor, int skin, int eyes, int shirt, int pants, int gender = Male)
        {
            Gender = (byte)Wrap(gender, Genders);
            Hair = (byte)Wrap(hair, HairStyles);
            HairColor = (byte)Wrap(hairColor, HairColors);
            Skin = (byte)Wrap(skin, Skins);
            Eyes = (byte)Wrap(eyes, EyeColors);
            Shirt = (byte)Wrap(shirt, ShirtColors);
            Pants = (byte)Wrap(pants, PantsColors);
        }

        // The look every character had before looks were picked: brown hair, the blue tunic, dark trousers.
        public static CharacterLook Default => new CharacterLook(0, 0, 1, 0, 0, 0);

        // The hair styles that suit each gender best, the first its own to start from (the making screen moves to it when
        // the gender is changed and the hair was the other's start; the dice pick among them three times in four).
        // By HeroArt.HairNames: short, long, tied, spiky, shaved, pigtails, bob, bun.
        static readonly int[] MaleHair = { 0, 3, 4, 2 };
        static readonly int[] FemaleHair = { 1, 5, 6, 7, 2 };

        public static int StartHair(int gender) => gender == Female ? FemaleHair[0] : MaleHair[0];

        public static bool Suits(int hair, int gender) => System.Array.IndexOf(gender == Female ? FemaleHair : MaleHair, hair) >= 0;

        // A woman with the first look's colours: long brown hair, the blue tunic, dark trousers.
        public static CharacterLook DefaultOf(int gender) => gender == Female ? new CharacterLook(StartHair(Female), 0, 1, 0, 0, 0, Female) : Default;

        // The same look in the other gender: the hair goes to that one's start when it was this one's start.
        public CharacterLook WithGender(int gender)
        {
            int hair = Hair == StartHair(Gender) ? StartHair(gender) : Hair;
            return new CharacterLook(hair, HairColor, Skin, Eyes, Shirt, Pants, gender);
        }

        // A look picked at random (the dice on the making screen): a gender, mostly a hair that suits it, any colours.
        public static CharacterLook Random(GameRandom random)
        {
            int gender = random.Next(Genders);
            var suits = gender == Female ? FemaleHair : MaleHair;
            int hair = random.Next(4) < 3 ? suits[random.Next(suits.Length)] : random.Next(HairStyles);
            return new CharacterLook(hair, random.Next(HairColors), random.Next(Skins), random.Next(EyeColors), random.Next(ShirtColors),
                random.Next(PantsColors), gender);
        }

        // Four bits each, low to high: hair, hair colour, skin, eyes, shirt, trousers (24 bits), then the gender's bit
        // (format 21, 0.115.0): a look packed before it has 0 there, the hero as first drawn.
        public uint Packed => (uint)(Hair | HairColor << 4 | Skin << 8 | Eyes << 12 | Shirt << 16 | Pants << 20 | Gender << 24);

        public static CharacterLook Unpack(uint packed) => new CharacterLook((int)(packed & 15), (int)(packed >> 4 & 15), (int)(packed >> 8 & 15),
            (int)(packed >> 12 & 15), (int)(packed >> 16 & 15), (int)(packed >> 20 & 15), (int)(packed >> 24 & 1));

        public bool Equals(CharacterLook other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is CharacterLook other && Equals(other);
        public override int GetHashCode() => (int)Packed;

        static int Wrap(int value, int count) => (value % count + count) % count;
    }
}
