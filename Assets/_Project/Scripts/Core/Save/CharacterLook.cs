namespace Game.Core
{
    // How a character looks, picked when it is made (2026-11-25, the owner: "캐릭과 맵을 분리해서 캐릭 리스트에서 선택",
    // character making with a name and a look, Docs/TitleRework.md). Only choices are kept here, each an index into a set
    // the client draws (Client/Art/HeroArt: hair styles, hair colours, skin tones, eye colours, shirt and trouser colours),
    // so the rules never depend on it. It rides in the character save and, packed, to the other players.
    public readonly struct CharacterLook
    {
        public const int HairStyles = 8, HairColors = 9, Skins = 5, EyeColors = 6, ShirtColors = 12, PantsColors = 12;

        public byte Hair { get; }
        public byte HairColor { get; }
        public byte Skin { get; }
        public byte Eyes { get; }
        public byte Shirt { get; }
        public byte Pants { get; }

        public CharacterLook(int hair, int hairColor, int skin, int eyes, int shirt, int pants)
        {
            Hair = (byte)Wrap(hair, HairStyles);
            HairColor = (byte)Wrap(hairColor, HairColors);
            Skin = (byte)Wrap(skin, Skins);
            Eyes = (byte)Wrap(eyes, EyeColors);
            Shirt = (byte)Wrap(shirt, ShirtColors);
            Pants = (byte)Wrap(pants, PantsColors);
        }

        // The look every character had before looks were picked: brown hair, the blue tunic, dark trousers.
        public static CharacterLook Default => new CharacterLook(0, 0, 1, 0, 0, 0);

        // A look picked at random (the dice on the making screen).
        public static CharacterLook Random(GameRandom random) => new CharacterLook(random.Next(HairStyles), random.Next(HairColors),
            random.Next(Skins), random.Next(EyeColors), random.Next(ShirtColors), random.Next(PantsColors));

        // Four bits each, low to high: hair, hair colour, skin, eyes, shirt, trousers (24 bits in all).
        public uint Packed => (uint)(Hair | HairColor << 4 | Skin << 8 | Eyes << 12 | Shirt << 16 | Pants << 20);

        public static CharacterLook Unpack(uint packed) => new CharacterLook((int)(packed & 15), (int)(packed >> 4 & 15), (int)(packed >> 8 & 15),
            (int)(packed >> 12 & 15), (int)(packed >> 16 & 15), (int)(packed >> 20 & 15));

        public bool Equals(CharacterLook other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is CharacterLook other && Equals(other);
        public override int GetHashCode() => (int)Packed;

        static int Wrap(int value, int count) => (value % count + count) % count;
    }
}
