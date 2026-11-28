using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The hero's looks (2026-11-25, Docs/TitleRework.md: a character is made with a name and a look, and the others in
    // a co-op game see it). Core keeps only the choices (CharacterLook: an index each for the hair's style and colour,
    // the skin, the eyes, the shirt and the trousers); what each index looks like is here. The hair is the head's
    // drawing redone for each style in the five views, so it turns, leans and nods with the head in every motion; the
    // colours are swapped in the palette the parts are painted from, each a ramp so the drawing's light stays.
    // CharacterLook.Default is the hero as drawn before looks were picked, pixel for pixel.
    static partial class HeroArt
    {
        // What the making screen calls each choice, in the order of the indices.
        public static readonly string[] HairNames = { "짧은 머리", "긴 머리", "묶은 머리", "뾰족 머리", "깎은 머리", "양갈래", "단발", "올림머리" };
        public static readonly string[] HairColorNames = { "갈색", "검정", "금빛", "붉은빛", "은빛", "파랑", "초록", "분홍", "보라" };
        public static readonly string[] SkinNames = { "밝은 살빛", "살빛", "볕에 탄 빛", "갈색", "짙은 갈색" };
        public static readonly string[] EyeNames = { "검정", "갈색", "파랑", "초록", "잿빛", "붉은빛" };
        public static readonly string[] ShirtNames = { "파랑", "초록", "주황", "보라", "청록", "빨강", "노랑", "분홍", "갈색", "흰색", "회색", "검정" };
        public static readonly string[] PantsNames = { "짙은 갈색", "남색", "숲빛", "밤색", "모래빛", "회색", "검정", "포도빛", "벽돌빛", "올리브", "청록", "흰색" };

        // Hair: its light and its dark.
        static readonly (Rgba Light, Rgba Dark)[] HairColors =
        {
            (Rgba.Hex("#6B412F"), Rgba.Hex("#49281F")),
            (Rgba.Hex("#3E3846"), Rgba.Hex("#25202C")),
            (Rgba.Hex("#E6BC5C"), Rgba.Hex("#B4843A")),
            (Rgba.Hex("#C8582E"), Rgba.Hex("#8C3620")),
            (Rgba.Hex("#DCE0E8"), Rgba.Hex("#9CA2B4")),
            (Rgba.Hex("#4C82D6"), Rgba.Hex("#2E4E96")),
            (Rgba.Hex("#5CAC62"), Rgba.Hex("#34703E")),
            (Rgba.Hex("#E686B4"), Rgba.Hex("#AA5080")),
            (Rgba.Hex("#9464D6"), Rgba.Hex("#5E3A9A")),
        };

        // Skin: in the light, turned from it, in shade (the jaw, a shut eye).
        static readonly (Rgba Light, Rgba Main, Rgba Shade)[] Skins =
        {
            (Rgba.Hex("#F8D8BC"), Rgba.Hex("#ECC2A2"), Rgba.Hex("#D2A68A")),
            (Rgba.Hex("#E7AF86"), Rgba.Hex("#DA9D76"), Rgba.Hex("#BF9178")),
            (Rgba.Hex("#D09868"), Rgba.Hex("#C08658"), Rgba.Hex("#A26E4A")),
            (Rgba.Hex("#A87048"), Rgba.Hex("#98623E"), Rgba.Hex("#7C4E32")),
            (Rgba.Hex("#7A4E36"), Rgba.Hex("#6A422E"), Rgba.Hex("#523224")),
        };

        static readonly Rgba[] EyeColors =
        {
            Ink, Rgba.Hex("#6E3A14"), Rgba.Hex("#2452C4"), Rgba.Hex("#1E7A3A"), Rgba.Hex("#5E6878"), Rgba.Hex("#B02430"),
        };

        // Shirts: each the middle of a ramp (Ramp.Of gives its shade and dark, as the tunic's colour always did).
        static readonly Rgba[] Shirts =
        {
            Rgba.Hex("#3F7FD8"), Rgba.Hex("#4DB06A"), Rgba.Hex("#E08A3C"), Rgba.Hex("#A070DC"), Rgba.Hex("#3CB8B0"), Rgba.Hex("#D04848"),
            Rgba.Hex("#E0C040"), Rgba.Hex("#E07AA8"), Rgba.Hex("#9A6A42"), Rgba.Hex("#DCD8CC"), Rgba.Hex("#7C8490"), Rgba.Hex("#454250"),
        };

        // Trousers: the near leg's colour. The far leg is a shade darker, except the first's, which is the hero's old
        // trousers: too dark for a shade to show, and kept as drawn.
        static readonly Rgba[] Trousers =
        {
            Rgba.Hex("#2B221F"), Rgba.Hex("#2E3E6A"), Rgba.Hex("#2E5038"), Rgba.Hex("#6E4830"), Rgba.Hex("#B49C6C"), Rgba.Hex("#606470"),
            Rgba.Hex("#1E1C24"), Rgba.Hex("#503264"), Rgba.Hex("#803C30"), Rgba.Hex("#5E5E32"), Rgba.Hex("#2A585C"), Rgba.Hex("#C8C4B8"),
        };

        // A choice's colour, for a swatch beside its name.
        public static Rgba HairColor(int index) => HairColors[Wrap(index, HairColors.Length)].Light;
        public static Rgba SkinColor(int index) => Skins[Wrap(index, Skins.Length)].Light;
        public static Rgba EyeColor(int index) => EyeColors[Wrap(index, EyeColors.Length)];
        public static Rgba ShirtColor(int index) => Shirts[Wrap(index, Shirts.Length)];
        public static Rgba PantsColor(int index) => Trousers[Wrap(index, Trousers.Length)];

        static int Wrap(int value, int count) => (value % count + count) % count;

        // Names a look, for a sprite's key.
        public static string Key(CharacterLook style) => "look" + style.Packed.ToString("x6");

        // What a figure is painted with: the palette of its parts and which hair it wears.
        sealed class Paint
        {
            public Dictionary<char, Rgba> Colors;
            public int Hair;
            // A woman's trunk and face (Docs/Gender.md).
            public bool Female;
        }

        static readonly Dictionary<uint, Paint> LookPaints = new Dictionary<uint, Paint>();
        static readonly Dictionary<int, Paint> TunicPaints = new Dictionary<int, Paint>();

        static Paint PaintOf(CharacterLook style)
        {
            if (!LookPaints.TryGetValue(style.Packed, out var paint)) LookPaints[style.Packed] = paint = Mixed(style, Shirts[style.Shirt]);
            return paint;
        }

        static Paint PaintOf(Rgba tunic)
        {
            int key = tunic.R << 16 | tunic.G << 8 | tunic.B;
            if (!TunicPaints.TryGetValue(key, out var paint)) TunicPaints[key] = paint = Mixed(CharacterLook.Default, tunic);
            return paint;
        }

        static Paint Mixed(CharacterLook style, Rgba shirt)
        {
            var ramp = Ramp.Of(shirt);
            var hair = HairColors[style.HairColor];
            var skin = Skins[style.Skin];
            var legs = Trousers[style.Pants];
            return new Paint
            {
                Hair = style.Hair,
                Female = style.IsFemale,
                Colors = new Dictionary<char, Rgba>(Colors)
                {
                    ['H'] = hair.Light,
                    ['h'] = hair.Dark,
                    ['s'] = skin.Light,
                    ['S'] = skin.Main,
                    ['r'] = skin.Shade,
                    ['e'] = EyeColors[style.Eyes],
                    ['T'] = ramp.Main,
                    ['t'] = ramp.Shade,
                    ['d'] = ramp.Dark,
                    ['p'] = legs,
                    ['q'] = style.Pants == 0 ? legs : legs.Scale(0.8f),
                    // A cheek's flush (a woman's face).
                    ['k'] = skin.Main.Mix(Rgba.Hex("#E0606A"), 0.45f),
                },
            };
        }

        // ---- The hair: for each style the head's rows in the five views (down, down and to the side, side, up and to
        // the side, up), twelve columns like the head's own. A style may add rows above the head's eight (Up of them: a
        // topknot, spikes) and below them, where they lie over the trunk (long hair on the shoulders and down the back,
        // a tail). A gap left beside a tail is closed by the outline, which is what sets the tail off from the head. ----

        sealed class HairStyle
        {
            public int Up;
            public string[][] Views;
        }

        static int HairUp(int hair) => hair == 0 ? 0 : Styles[hair].Up;

        // A woman's face (Docs/Gender.md) on any hair's head rows: the chin a pixel narrower each side (what was beside it,
        // hair or nothing, closes in) and a flush on the cheeks under the eyes. Only skin is changed, so every hair keeps
        // its own lines; the views from behind show no face and are left as they are.
        static readonly Dictionary<(int, Look), string[]> Faces = new Dictionary<(int, Look), string[]>();

        static string[] FaceOf(string[] rows, Look look, int hair, bool female)
        {
            if (!female || look == Look.Up || look == Look.UpSide) return rows;
            if (Faces.TryGetValue((hair, look), out var done)) return done;
            int top = HairUp(hair);
            var face = new char[rows.Length][];
            for (int y = 0; y < rows.Length; y++) face[y] = rows[y].ToCharArray();
            bool Skin(char c) => c == 's' || c == 'S' || c == 'r';
            void Chin(int y, int x, int outward)
            {
                if (y >= face.Length || !Skin(face[y][x])) return;
                char beside = face[y][x + outward];
                face[y][x] = beside == 'H' || beside == 'h' ? beside : '.';
            }
            void Flush(int y, int x)
            {
                if (y < face.Length && Skin(face[y][x])) face[y][x] = 'k';
            }
            // The head's own rows start at top: the eyes are on its rows 5 and 6, the chin is its row 7.
            int eyes = top + 6, chin = top + 7;
            switch (look)
            {
                case Look.Down:
                    Chin(chin, 3, -1);
                    Chin(chin, 8, 1);
                    break;
                case Look.DownSide:
                    Chin(chin, 8, 1);
                    break;
                case Look.Side:
                    Chin(chin, 9, 1);
                    Flush(eyes, 7);
                    break;
            }
            var result = new string[rows.Length];
            for (int y = 0; y < rows.Length; y++) result[y] = new string(face[y]);
            Faces[(hair, look)] = result;
            return result;
        }

        static string[] HeadRows(int hair, Look look) => hair == 0 ? HeadRows(look) : Styles[hair].Views[(int)ViewOf(look)];

        enum HairView { Down, DownSide, Side, UpSide, Up }

        static HairView ViewOf(Look look) => look switch { Look.Down => HairView.Down, Look.DownSide => HairView.DownSide, Look.Side => HairView.Side, Look.UpSide => HairView.UpSide, _ => HairView.Up };

        static readonly HairStyle[] Styles =
        {
            // 0: short and tousled, the head as first drawn (HeadDown and the rest).
            null,
            // 1: long, over the shoulders and down the back.
            new HairStyle
            {
                Views = new[]
                {
                    new[]
                    {
                        "...HHhHHH...",
                        "..HHhHHHHh..",
                        ".HHHhhHHhHH.",
                        ".HhhhshhhHh.",
                        ".hhsSssSshh.",
                        ".hHesssseHh.",
                        ".hsessssesh.",
                        ".hHsSssSsHh.",
                        ".hH......Hh.",
                        "..h......h..",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHHHhHH.",
                        ".HHhhHHHhHh.",
                        ".HhhHhhshhh.",
                        ".hhhhSssShh.",
                        ".hhSessseh..",
                        ".hHsessses..",
                        ".hHrsssss...",
                        ".hHh........",
                        "..hh........",
                    },
                    new[]
                    {
                        "....HHHHH...",
                        "..hHHHhhHHH.",
                        ".hHHHHHHhHH.",
                        ".HHhHhHHhhH.",
                        ".HhhhhhhSsh.",
                        ".hHhhSrhess.",
                        ".hHhrssSess.",
                        ".hHhrsssSs..",
                        ".hHhh.......",
                        "..hhh.......",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHhHHHH..",
                        ".hHHhHHhhHH.",
                        ".hHHHHHHhhH.",
                        ".HhHHHHhhhh.",
                        ".hhHHhhhhhS.",
                        ".hhHhhHhhSs.",
                        ".hHHhHHhh...",
                        ".hHhHHhHh...",
                        "..hhHhhh....",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHhHHH..",
                        ".HHHHhHHHHH.",
                        ".hHHhHHHHhh.",
                        ".HHHHhHHHhh.",
                        ".hhhHHHHhhh.",
                        ".hHhhHHhhHh.",
                        ".hHHhHHhHHh.",
                        "..hHhHHhHh..",
                        "..hhHhhHhh..",
                    },
                },
            },
            // 2: tied in a tail at the back.
            new HairStyle
            {
                Views = new[]
                {
                    new[]
                    {
                        "...HHhHHH...",
                        "..HHhHHHHh..",
                        ".HHHhhHHhHH.",
                        ".HhhhshhhHh.",
                        ".hhsSssSshh.",
                        ".hHesssseHh.",
                        "..sesssses.H",
                        "...sSssSs.HH",
                        "..........Hh",
                        "..........h.",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHHHhHH.",
                        ".HHhhHHHhHh.",
                        "hHhhHhhshhh.",
                        "HhhhhSssShh.",
                        "HhhSessseh..",
                        "H.rsessses..",
                        "Hh.rsssss...",
                        "hH..........",
                        ".h..........",
                    },
                    new[]
                    {
                        "....HHHHH...",
                        "..hHHHhhHHH.",
                        ".hHHHHHHhHH.",
                        "hHHhHhHHhhH.",
                        "HHhhhhhhSsh.",
                        "HhhhhSrhess.",
                        "Hh.hrssSess.",
                        "hH..rsssSs..",
                        ".h..........",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHhHHHH..",
                        ".hHHhHHhhHH.",
                        "hhHhhHHHhhH.",
                        "HHhHHHHhhhh.",
                        "HHhHHhhhhhS.",
                        "hH.hhhhhhSs.",
                        ".Hh.hhhhh...",
                        ".hH.........",
                        "..h.........",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHhHHH..",
                        ".HHHHhHHHHH.",
                        ".hHHhhhHHhh.",
                        ".HHHhHHhHhh.",
                        ".hhhhHHhhhh.",
                        "..rhhHHhhr..",
                        "...hhHHh....",
                        ".....HH.....",
                        ".....hH.....",
                        "......h.....",
                    },
                },
            },
            // 3: spikes standing up.
            new HairStyle
            {
                Up = 2,
                Views = new[]
                {
                    new[]
                    {
                        "...H...H....",
                        "..HH.HhH.HH.",
                        "..HHhHHHHHh.",
                        ".HHHhHHHHhH.",
                        ".HHHhhHHhHH.",
                        ".HhhhshhhHh.",
                        ".hhsSssSshh.",
                        ".hHesssseHh.",
                        "..sesssses..",
                        "...sSssSs...",
                    },
                    new[]
                    {
                        "...H...H....",
                        "..HH.HhH.HH.",
                        "..HHhHHHHHH.",
                        ".hHHHHHHhHH.",
                        ".HHhhHHHhHh.",
                        ".HhhHhhshhh.",
                        ".hhhhSssShh.",
                        ".hhSessseh..",
                        "..rsessses..",
                        "...rsssss...",
                    },
                    new[]
                    {
                        "....H...H...",
                        ".H.HH.HHH.H.",
                        ".HHHHHHHHHH.",
                        ".hHHHHhhHHH.",
                        ".hHHHHHHhHH.",
                        "HHHhHhHHhhH.",
                        ".HhhhhhhSsh.",
                        ".hhhhSrhess.",
                        "..hhrssSess.",
                        "....rsssSs..",
                    },
                    new[]
                    {
                        "....H...H...",
                        ".H.HH.HhH.H.",
                        ".HHHHHHHHHH.",
                        ".HHHHhHHHHH.",
                        ".hHHhHHhhHH.",
                        "HhHHHHHHhhH.",
                        ".HhHHHHhhhh.",
                        ".hhHHhhhhhS.",
                        "..hhhhhhhSs.",
                        "...hhhhhh...",
                    },
                    new[]
                    {
                        "....H...H...",
                        ".HH.HhH.HH..",
                        ".HHHhHHHHHH.",
                        ".hHHHHhHHHH.",
                        ".HHHHhHHHHH.",
                        ".hHHhHHHHhh.",
                        ".HHHHhHHHhh.",
                        ".hhhHHHHhhh.",
                        "..rhhhhhhr..",
                        "...hHHHh....",
                    },
                },
            },
            // 4: cropped close, the ears and the nape bare.
            new HairStyle
            {
                Views = new[]
                {
                    new[]
                    {
                        "....hhhh....",
                        "..hhhHhhhh..",
                        ".hhHhhhHhhh.",
                        ".hhssssssHh.",
                        ".hsSssssSsh.",
                        ".rsessssesr.",
                        "..sesssses..",
                        "...sSssSs...",
                    },
                    new[]
                    {
                        "....hhhhh...",
                        "..hhhHhhhhh.",
                        ".hhHhhhhhhh.",
                        ".hhhhssssh..",
                        ".hhhsSssSs..",
                        ".rsSessses..",
                        "..rsessses..",
                        "...rsssss...",
                    },
                    new[]
                    {
                        "....hhhhh...",
                        "..hhhHhhhhh.",
                        ".hhHhhhhhhh.",
                        ".hhhhhhhhss.",
                        ".hhhhhssSss.",
                        ".hhhhSrsess.",
                        "..rrsssSess.",
                        "....rsssSs..",
                    },
                    new[]
                    {
                        "....hhhhh...",
                        "..hhhhHhhh..",
                        ".hhhHhhhhhh.",
                        ".hhhhhhhhhh.",
                        ".hhhhhhhhhs.",
                        ".rhhhhhhhsS.",
                        "..rsssssrSs.",
                        "...rssss....",
                    },
                    new[]
                    {
                        "....hhhh....",
                        "..hhhHhhhh..",
                        ".hhHhhhhHhh.",
                        ".hhhhHhhhhh.",
                        ".hhhhhhhhhh.",
                        ".rhhhhhhhhr.",
                        "..rssssssr..",
                        "...rsssr....",
                    },
                },
            },
            // 5: two tails, one at each side.
            new HairStyle
            {
                Views = new[]
                {
                    new[]
                    {
                        "...HHhHHH...",
                        "..HHhHHHHh..",
                        ".HHHhhHHhHH.",
                        "hHhhhshhhHhh",
                        "HhhsSssSshhH",
                        "HhHesssseHhH",
                        "H.sesssses.H",
                        "Hh.sSssSs.hH",
                        "hH........Hh",
                        ".h........h.",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHHHhHH.",
                        ".HHhhHHHhHh.",
                        "hHhhHhhshhh.",
                        "HhhhhSssShhH",
                        "HhhSessseh.H",
                        "H.rsessses.h",
                        "Hh.rsssss...",
                        "hH..........",
                        ".h..........",
                    },
                    new[]
                    {
                        "....HHHHH...",
                        "..hHHHhhHHH.",
                        ".hHHHHHHhHH.",
                        ".HHhHhHHhhH.",
                        ".HhhhhhhSsh.",
                        ".hhhHSrhess.",
                        "..hHhssSess.",
                        "..hHHrssSs..",
                        "...hH.......",
                        "....h.......",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHhHHHH..",
                        ".hHHhHHhhHH.",
                        "hhHHHHHHhhH.",
                        "HHhHHHHhhhhH",
                        "HhhHHhhhhhS.",
                        "H.hhhhhhhSs.",
                        "Hh.hhhhhh...",
                        "hH..........",
                        ".h..........",
                    },
                    new[]
                    {
                        "...HHhHHH...",
                        "..hHHHhHHH..",
                        ".HHHHhHHHHH.",
                        "hhHHhHHHHhhh",
                        "HHHHHhHHHhhH",
                        "HhhhHHHHhhhH",
                        "H.rhhhhhhr.H",
                        "Hh.hHHHh..hH",
                        "hH........Hh",
                        ".h........h.",
                    },
                },
            },
            // 6: a bob, a straight fringe and the sides to the chin.
            new HairStyle
            {
                Views = new[]
                {
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHHHHHH..",
                        ".HHHhHHhHHH.",
                        ".HhHhHHhHhH.",
                        ".hhhhhhhhhh.",
                        ".hHesssseHh.",
                        ".hsessssesh.",
                        ".hHsSssSsHh.",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..hHHHHHHHH.",
                        ".HHhHHHHHHh.",
                        ".HhHHhHHhHh.",
                        ".hHhhhhhhhh.",
                        ".hhHessseh..",
                        ".hHsessses..",
                        ".hhrsssss...",
                    },
                    new[]
                    {
                        "....HHHHH...",
                        "..hHHHHHHHH.",
                        ".hHHHHHHHHH.",
                        ".HHhHHHHHhH.",
                        ".HhHhHhhhhh.",
                        ".hHhHhhhess.",
                        ".hhHhhsSess.",
                        ".hhhhrssSs..",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHHHHHH..",
                        ".hHHHHHHHHH.",
                        ".HHHhHHHHhH.",
                        ".HhHHHhHHhh.",
                        ".hHHhHHhhhS.",
                        ".hHhHHhHhSs.",
                        ".hhhhhhhh...",
                    },
                    new[]
                    {
                        "...HHHHHH...",
                        "..HHHHHHHH..",
                        ".HHHHhHHHHH.",
                        ".HHHhHHHhHH.",
                        ".HHhHHHhHHh.",
                        ".HhHHhHHhHh.",
                        ".hHhHHhHhhh.",
                        ".hhhhhhhhhh.",
                    },
                },
            },
            // 7: pulled up into a knot on top.
            new HairStyle
            {
                Up = 2,
                Views = new[]
                {
                    new[]
                    {
                        "....hHHh....",
                        "....HHhH....",
                        "...HhhhhH...",
                        "..HHhHHHHh..",
                        ".HHHhhHHhHH.",
                        ".HhssssssHh.",
                        ".hhsSssSshh.",
                        ".hHesssseHh.",
                        "..sesssses..",
                        "...sSssSs...",
                    },
                    new[]
                    {
                        "....hHHh....",
                        "....HHhH....",
                        "...HhhhhH...",
                        "..hHHHHHhHH.",
                        ".HHhhHHHhHh.",
                        ".HhhHhssshh.",
                        ".hhhhSssShh.",
                        ".hhSessseh..",
                        "..rsessses..",
                        "...rsssss...",
                    },
                    new[]
                    {
                        "...hHHh.....",
                        "...HHhH.....",
                        "...hhhhHH...",
                        "..hHHHhhHHH.",
                        ".hHHHHHHhHH.",
                        ".HHhHhHHhss.",
                        ".HhhhhhhSsh.",
                        ".hhhhSrhess.",
                        "..hhrssSess.",
                        "....rsssSs..",
                    },
                    new[]
                    {
                        "...hHHh.....",
                        "...HHhH.....",
                        "...hhhhHH...",
                        "..HHHhHHHH..",
                        ".hHHhHHhhHH.",
                        ".hHHHHHHhhH.",
                        ".HhHHHHhhhh.",
                        ".hhHHhhhhhS.",
                        "..hhhhhhhSs.",
                        "...hhhhhh...",
                    },
                    new[]
                    {
                        "....hHHh....",
                        "....HhHH....",
                        "...HhhhhH...",
                        "..hHHHhHHH..",
                        ".HHHHhHHHHH.",
                        ".hHHhHHHHhh.",
                        ".HHHHhHHHhh.",
                        ".hhhHHHHhhh.",
                        "..rhhhhhhr..",
                        "...hHHHh....",
                    },
                },
            },
        };
    }
}
