using System;
using System.Collections.Generic;

namespace Game.Client
{
    // What a face in a portrait shows (Docs/UiRework.md 4-2).
    enum Expression { Neutral, Happy, Surprised, Angry, Sad, Thinking }

    // The speaker's portrait in the talk box (Docs/UiRework.md 4-2, the person's choice on 2026-11-22: drawn anew,
    // with a few expressions): head and shoulders of every person of the story (characters.csv) and every keeper of
    // a stall (npcs.csv), each the person PeopleArt draws in the world — the same hair, hat, clothes and colours —
    // seen closer. An expression changes the brows, eyes and mouth and adds a small mark (a blush, a tear, a drop of
    // sweat, an anger mark, thinking dots); the rest of the drawing stays put, so a face can change mid-sentence
    // without the portrait jumping. Lit from the upper left and outlined like the sprites; the outline is left off
    // where the bust runs out of the picture. Meant to be drawn at a whole scale: 3 on a PC, 2 on a phone.
    static class PortraitArt
    {
        public const int Width = 42;
        public const int Height = 52;

        public static readonly Expression[] Expressions =
        {
            Expression.Neutral, Expression.Happy, Expression.Surprised, Expression.Angry, Expression.Sad, Expression.Thinking,
        };

        // Everyone drawn here, with their look and colour as the sheets have them today. A sheet that recolours
        // someone is followed by passing its look and colour to the longer Of.
        public static readonly (string Id, string Look, string Color)[] People =
        {
            ("chief", "elder", "#8A6A4A"), ("mina", "villager", "#C85A6A"), ("queen", "queen", "#E8C860"), ("captain", "guard", "#5A7AB8"),
            ("hero", "hero", "#4A7AD8"), ("lu", "girl", "#B84AC8"), ("demon_elder", "demon", "#6A3A8A"), ("demon_watch", "demon", "#8A3A3A"),
            ("harbor_master", "villager", "#3A7AA8"), ("fisher_ida", "girl", "#5AA88A"), ("foreman", "elder", "#A8743A"), ("miner", "villager", "#7A6A5A"),
            ("temple_keeper", "elder", "#8A9AC8"),
            ("merchant", "merchant", "#9A5AC8"), ("blacksmith", "blacksmith", "#C86A3A"), ("fisher", "fisher", "#3E8CC8"), ("alchemist", "alchemist", "#9A5CD8"),
            ("royal_trader", "villager", "#E8B848"), ("demon_trader", "demon", "#7A3A9A"), ("ore_trader", "elder", "#B8743A"), ("wanderer", "wanderer", "#2EA89A"),
        };

        static readonly Dictionary<(string, string, int, Expression), PixelImage> Drawn = new Dictionary<(string, string, int, Expression), PixelImage>();

        // The portrait of a person by their id in characters.csv or npcs.csv. Someone not known is a plain villager.
        public static PixelImage Of(string personId, Expression expression)
        {
            foreach (var (id, look, color) in People)
            {
                if (id == personId) return Of(id, look, Rgba.Hex(color), expression);
            }
            return Of(personId, "villager", Rgba.Hex("#8A7A6A"), expression);
        }

        // The same with the look and colour from the sheet (a character's look, a stall keeper's colour). A look not
        // drawn here is a villager, as in PeopleArt. The image is kept and shared: do not draw on it.
        public static PixelImage Of(string personId, string look, Rgba color, Expression expression)
        {
            personId ??= "";
            look = LookOf(personId, look);
            var key = (personId, look, color.R << 16 | color.G << 8 | color.B, expression);
            if (!Drawn.TryGetValue(key, out var image)) Drawn[key] = image = Draw(personId, look, color, expression);
            return image;
        }

        // The stall keepers with a drawing of their own are themselves whatever look is asked; the rest stand in a
        // look of the story's people (PeopleArt.Person).
        static string LookOf(string id, string look) => id switch
        {
            "merchant" or "blacksmith" or "fisher" or "alchemist" or "wanderer" => id,
            "royal_trader" => "villager",
            "demon_trader" => "demon",
            "ore_trader" => "elder",
            _ => look ?? "villager",
        };

        // ---- Colours (PeopleArt's, so a portrait and its sprite match) ----

        static readonly Rgba Ink = Rgba.Hex("#140E10");
        static readonly Rgba Skin = Rgba.Hex("#F2C49C"), SkinShade = Rgba.Hex("#D39A74"), SkinDeep = Rgba.Hex("#A86E52"), SkinLight = Rgba.Hex("#FFDDBA");
        static readonly Rgba Eye = Rgba.Hex("#1C1626"), White = Rgba.Hex("#F6F2E8");
        static readonly Rgba HairDark = Rgba.Hex("#5A3420"), HairLight = Rgba.Hex("#8A5632"), HairDeep = Rgba.Hex("#3C2216");
        static readonly Rgba QueenHair = Rgba.Hex("#8A5632"), QueenHairLight = Rgba.Hex("#B47A48"), QueenHairDeep = Rgba.Hex("#5A3420");
        static readonly Rgba GirlHair = Rgba.Hex("#2C2940"), GirlHairLight = Rgba.Hex("#4A4668"), GirlHairDeep = Rgba.Hex("#1C1A2C");
        static readonly Rgba Grey = Rgba.Hex("#ECEAF4"), GreyShade = Rgba.Hex("#B4B0CC"), GreyDeep = Rgba.Hex("#8A90A0");
        static readonly Rgba Gold = Rgba.Hex("#F0C850"), GoldShade = Rgba.Hex("#B8862A"), GoldLight = Rgba.Hex("#FFE9A0");
        static readonly Rgba Red = Rgba.Hex("#A83232"), RedLight = Rgba.Hex("#E05A4A"), RedDeep = Rgba.Hex("#7A2028");
        static readonly Rgba Iron = Rgba.Hex("#B8BEC8"), IronShade = Rgba.Hex("#5A5E6A"), IronLight = Rgba.Hex("#E4E8F0"), IronMid = Rgba.Hex("#8A90A0");
        static readonly Rgba Wood = Rgba.Hex("#9A6A3A"), WoodShade = Rgba.Hex("#6A4424");
        static readonly Rgba Leather = Rgba.Hex("#7A4A2A"), LeatherShade = Rgba.Hex("#5A3420"), LeatherLight = Rgba.Hex("#9A6238");
        static readonly Rgba Beard = Rgba.Hex("#4A2C1C"), BeardLight = Rgba.Hex("#6E4228");
        static readonly Rgba Band = Rgba.Hex("#4A3020");
        static readonly Rgba Robe = Rgba.Hex("#2C2940"), RobeLight = Rgba.Hex("#3E3A56"), RobeDeep = Rgba.Hex("#1E1C2E");
        static readonly Rgba Cream = Rgba.Hex("#F6F2E8"), CreamShade = Rgba.Hex("#C8C2B8");
        static readonly Rgba Blush = Rgba.Hex("#EE9080"), Lip = Rgba.Hex("#8A4038"), Tongue = Rgba.Hex("#C8504A");
        static readonly Rgba Drop = Rgba.Hex("#8AD0F4"), DropLight = Rgba.Hex("#E0F6FF");
        static readonly Rgba Mark = Rgba.Hex("#E8403A");

        // ---- The canvas: rows mirrored about the middle (between columns 20 and 21), the whole figure moved down
        // for a child ----

        sealed class Canvas
        {
            public readonly PixelImage Image = new PixelImage(Width, Height);
            public int Down;

            public void Dot(int x, int y, Rgba c) => Image[x, y + Down] = c;

            public Rgba At(int x, int y) => Image[x, y + Down];

            public void Row(int y, int x0, int x1, Rgba c)
            {
                for (int x = x0; x <= x1; x++) Image[x, y + Down] = c;
            }

            public void Box(int x0, int y0, int x1, int y1, Rgba c)
            {
                for (int y = y0; y <= y1; y++) Row(y, x0, x1, c);
            }

            // A row of the half width about the middle.
            public void Sym(int y, int half, Rgba c) => Row(y, 21 - half, 20 + half, c);

            // The pixel and its mirror.
            public void Both(int x, int y, Rgba c)
            {
                Dot(x, y, c);
                Dot(Width - 1 - x, y, c);
            }

            public void BothRow(int y, int x0, int x1, Rgba c)
            {
                for (int x = x0; x <= x1; x++) Both(x, y, c);
            }

            // Rows from the top, each a half width: a shape about the middle, shaded on its right edge.
            public void Shape(int top, int[] halves, Rgba main, Rgba shade, int shadeWidth = 2)
            {
                for (int i = 0; i < halves.Length; i++)
                {
                    if (halves[i] <= 0) continue;
                    Sym(top + i, halves[i], main);
                    if (shadeWidth > 0) Row(top + i, Math.Max(21, 20 + halves[i] - shadeWidth + 1), 20 + halves[i], shade);
                }
            }
        }

        // What differs between faces.
        sealed class Face
        {
            public Rgba Main = Skin, Shade = SkinShade, Deep = SkinDeep;
            public Rgba Eye = PortraitArt.Eye, EyeLight = White, EyeWhite = White;
            public Rgba Brow = HairDeep, Mouth = Lip, Blush = PortraitArt.Blush;
            public bool Child, Nose = true, Fangs;
            // Bushy brows are two rows thick.
            public bool Bushy;
        }

        // ---- A person ----

        static PixelImage Draw(string id, string look, Rgba color, Expression e)
        {
            var g = new Canvas();
            var cloth = Ramp.Of(color);
            var face = new Face();
            switch (look)
            {
                case "queen": Queen(g, cloth, face, e); break;
                case "hero": Hero(g, cloth, face, e); break;
                case "elder": Elder(g, id, cloth, face, e); break;
                case "girl": Girl(g, id, cloth, face, e); break;
                case "demon": Demon(g, id, cloth, face, e); break;
                case "guard": Guard(g, cloth, face, e); break;
                case "merchant": Merchant(g, cloth, face, e); break;
                case "blacksmith": Blacksmith(g, cloth, face, e); break;
                case "fisher": Fisher(g, cloth, face, e); break;
                case "alchemist": Alchemist(g, cloth, face, e); break;
                case "wanderer": Wanderer(g, cloth, face, e); break;
                default: Villager(g, id, cloth, face, e); break;
            }
            // Outlined, then cut back to size: what touches an edge runs out of the picture without a line.
            var outlined = g.Image.Outlined(Ink);
            var image = new PixelImage(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) image[x, y] = outlined[x + 1, y + 1];
            }
            Marks(image, e, g.Down, look == "guard");
            return image;
        }

        // ---- The parts every person shares ----

        static readonly int[] HeadHalves = { 6, 8, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 9, 9, 8, 6 };
        const int HeadTop = 10;
        const int Chin = 33;
        static readonly int[] WideShoulders = { 10, 14, 17, 19, 20 };
        static readonly int[] NarrowShoulders = { 8, 11, 13, 14, 15 };
        const int ShoulderTop = 38;

        // Shoulders and chest in the cloth, lit along the top of the left shoulder and shaded down the right.
        static void Shoulders(Canvas g, Ramp cloth, bool narrow = false)
        {
            var halves = narrow ? NarrowShoulders : WideShoulders;
            int before = 0;
            for (int y = ShoulderTop; y < Height; y++)
            {
                int half = halves[Math.Min(y - ShoulderTop, halves.Length - 1)];
                g.Sym(y, half, cloth.Main);
                g.Row(y, 20 + half - 2, 20 + half, cloth.Shade);
                if (half > before) g.Row(y, 21 - half, Math.Min(20 - before, 20), cloth.Light);
                before = half;
            }
        }

        static void Neck(Canvas g, Face f, int half = 4)
        {
            for (int y = Chin; y <= ShoulderTop + 1; y++)
            {
                g.Sym(y, half, f.Shade);
                g.Row(y, 21 - half, 21 - half, f.Main);
            }
            // The chin's shadow.
            g.Sym(Chin + 1, half, f.Deep);
            g.Sym(Chin + 2, half - 1, f.Deep);
        }

        // The skin where a collar opens below the neck.
        static void Opening(Canvas g, Face f, int rows = 3)
        {
            for (int i = 0; i < rows; i++) g.Sym(ShoulderTop + i, Math.Max(1, 4 - i), i == 0 ? f.Shade : f.Main);
        }

        static void Head(Canvas g, Face f, bool ears = true)
        {
            g.Shape(HeadTop, HeadHalves, f.Main, f.Shade);
            // The jaw turns under.
            g.Sym(Chin, 6, f.Shade);
            g.Row(Chin - 1, 24, 28, f.Shade);
            if (!ears) return;
            for (int y = 23; y <= 26; y++) g.Both(10, y, y == 26 ? f.Shade : f.Main);
            g.Dot(31, 23, f.Shade);
            g.Dot(31, 24, f.Shade);
            g.Dot(31, 25, f.Deep);
            g.Dot(10, 25, f.Shade);
        }

        // A cap of hair over the skull: a pixel wider and higher than the head, lit on its upper left, down to the
        // given row, where the fringe is cut by the caller.
        static void HairCap(Canvas g, Rgba main, Rgba light, Rgba deep, int bottom = 17)
        {
            int[] halves = { 7, 9, 10, 11 };
            for (int y = 9; y <= bottom; y++)
            {
                int half = halves[Math.Min(y - 9, halves.Length - 1)];
                g.Sym(y, half, main);
                g.Row(y, 20 + half - 1, 20 + half, deep);
            }
            g.Row(10, 15, 19, light);
            g.Row(11, 13, 16, light);
            g.Row(12, 12, 13, light);
            g.Dot(11, 14, light);
        }

        // A day's growth along the jaw and upper lip: the skin a little darker and greyer, the mouth left clear.
        static void Stubble(Canvas g, Face f)
        {
            var c = f.Main.Mix(HairDark, 0.2f);
            var shade = f.Shade.Mix(HairDark, 0.25f);
            for (int y = 27; y <= Chin; y++)
            {
                for (int x = 11; x <= 30; x++)
                {
                    bool jaw = y >= 32 || x <= 14 + (y - 27) / 2 || x >= 27 - (y - 27) / 2;
                    if (!jaw || g.At(x, y).A == 0) continue;
                    g.Dot(x, y, x >= 28 || y == Chin ? shade : c);
                }
            }
        }

        // Long hair before the shoulders at both sides of the face, from the row to the row, the ends tapering.
        static void Locks(Canvas g, Rgba main, Rgba light, Rgba deep, int top, int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                int inner = y > 36 ? 11 : 10;
                int outer = y > bottom - 1 ? 10 : y > bottom - 3 ? 9 : 8;
                if (y > bottom - 1) inner = 10;
                g.Row(y, outer, inner, main);
                g.Row(y, Width - 1 - inner, Width - 1 - outer, deep);
            }
            for (int y = top + 2; y <= bottom - 4; y += 3) g.Dot(9, y, light);
        }

        // ---- The face ----

        const int LeftEye = 15, RightEye = 25, EyeTop = 22, MouthRow = 30;

        static void Features(Canvas g, Face f, Expression e, bool beard = false)
        {
            int tall = f.Child ? 4 : 3;
            int top = EyeTop;
            int mouth = f.Child ? MouthRow - 1 : MouthRow;
            var lid = f.Shade;

            if (f.Nose)
            {
                g.Dot(21, 27, f.Shade);
                g.Dot(21, 28, f.Shade);
                g.Dot(20, 28, f.Shade);
            }

            // ---- Eyes ----
            void Eyes(int dx, int y0, int y1, Rgba c)
            {
                for (int y = y0; y <= y1; y++)
                {
                    g.Dot(LeftEye + dx, y, c);
                    g.Dot(RightEye + dx, y, c);
                }
            }
            switch (e)
            {
                case Expression.Happy:
                    // Closed and curved up.
                    g.Both(LeftEye - 1, top + 2, f.Eye);
                    g.Both(LeftEye, top + 1, f.Eye);
                    g.Both(LeftEye + 1, top + 1, f.Eye);
                    g.Both(LeftEye + 2, top + 2, f.Eye);
                    g.BothRow(top + 4, LeftEye - 2, LeftEye, f.Blush);
                    break;
                case Expression.Surprised:
                    // Wide: the white all around a small pupil.
                    for (int dx = -1; dx <= 2; dx++) Eyes(dx, top - 1, top + tall - 1, f.EyeWhite);
                    Eyes(0, top, top + 1, f.Eye);
                    Eyes(1, top, top + 1, f.Eye);
                    g.BothRow(top - 2, LeftEye - 1, LeftEye + 2, lid);
                    break;
                case Expression.Angry:
                    // Narrowed under the lowered brow.
                    Eyes(0, top + 1, top + 2, f.Eye);
                    Eyes(1, top + 1, top + 2, f.Eye);
                    g.Both(LeftEye - 1, top + 1, f.EyeWhite);
                    g.BothRow(top + 3, LeftEye, LeftEye + 1, lid);
                    break;
                case Expression.Sad:
                    // Half shut, looking down, a tear under the right eye.
                    g.BothRow(top, LeftEye - 1, LeftEye + 1, lid);
                    Eyes(0, top + 1, top + tall - 1, f.Eye);
                    Eyes(1, top + 1, top + tall - 1, f.Eye);
                    g.Dot(LeftEye + 1, top + tall - 1, f.EyeLight);
                    g.Dot(RightEye + 1, top + tall - 1, f.EyeLight);
                    g.Dot(RightEye + 2, top + tall, Drop);
                    g.Dot(RightEye + 2, top + tall + 1, Drop);
                    g.Dot(RightEye + 2, top + tall + 2, DropLight);
                    break;
                case Expression.Thinking:
                    // Looking up and aside.
                    Eyes(0, top, top + tall - 1, f.EyeWhite);
                    Eyes(1, top, top + tall - 2, f.Eye);
                    Eyes(2, top, top + tall - 2, f.Eye);
                    Eyes(1, top + tall - 1, top + tall - 1, f.EyeWhite);
                    Eyes(2, top + tall - 1, top + tall - 1, f.EyeWhite);
                    break;
                default:
                    Eyes(0, top, top + tall - 1, f.Eye);
                    Eyes(1, top, top + tall - 1, f.Eye);
                    g.Dot(LeftEye, top, f.EyeLight);
                    g.Dot(RightEye, top, f.EyeLight);
                    break;
            }

            // ---- Brows (x from the outer end of the left one; the right mirrors) ----
            void Brow(int x, int y)
            {
                g.Both(x, y, f.Brow);
                if (f.Bushy) g.Both(x, y - 1, f.Brow);
            }
            switch (e)
            {
                case Expression.Happy:
                    for (int x = 14; x <= 17; x++) Brow(x, top - 3);
                    break;
                case Expression.Surprised:
                    for (int x = 14; x <= 17; x++) Brow(x, x == 14 ? top - 4 : top - 5);
                    break;
                case Expression.Angry:
                    Brow(13, top - 3); Brow(14, top - 3); Brow(15, top - 2); Brow(16, top - 2); Brow(17, top - 1); Brow(18, top - 1);
                    Brow(16, top - 1); Brow(17, top);
                    break;
                case Expression.Sad:
                    Brow(13, top - 1); Brow(14, top - 2); Brow(15, top - 2); Brow(16, top - 3); Brow(17, top - 3); Brow(18, top - 4);
                    break;
                case Expression.Thinking:
                    // One up, one level.
                    for (int x = 14; x <= 17; x++)
                    {
                        g.Dot(x, top - 2, f.Brow);
                        if (f.Bushy) g.Dot(x, top - 3, f.Brow);
                        g.Dot(Width - 1 - x, x == 14 ? top - 3 : top - 4, f.Brow);
                        if (f.Bushy) g.Dot(Width - 1 - x, x == 14 ? top - 4 : top - 5, f.Brow);
                    }
                    break;
                default:
                    for (int x = 14; x <= 17; x++) Brow(x, top - 2);
                    break;
            }

            // ---- Mouth ----
            var line = beard ? Ink : f.Mouth;
            switch (e)
            {
                case Expression.Happy:
                    // Open, the corners up.
                    g.Both(17, mouth - 1, line);
                    g.Row(mouth, 18, 23, line);
                    g.Row(mouth + 1, 18, 23, Tongue);
                    g.Both(18, mouth + 1, line);
                    g.Row(mouth, 19, 22, f.Fangs ? White : Tongue);
                    g.Row(mouth - 1, 18, 23, line);
                    g.Row(mouth + 2, 19, 22, line);
                    if (!f.Fangs) g.Row(mouth, 19, 22, White);
                    break;
                case Expression.Surprised:
                    g.Row(mouth - 1, 20, 21, line);
                    g.Row(mouth, 19, 22, line);
                    g.Row(mouth + 1, 19, 22, line);
                    g.Row(mouth + 2, 20, 21, line);
                    g.Row(mouth, 20, 21, RedDeep);
                    g.Row(mouth + 1, 20, 21, RedDeep);
                    break;
                case Expression.Angry:
                    // Teeth bared, the corners down.
                    g.Row(mouth - 1, 18, 23, line);
                    g.Row(mouth, 18, 23, White);
                    g.Row(mouth + 1, 18, 23, line);
                    g.Both(17, mouth, line);
                    g.Both(17, mouth + 1, line);
                    g.Dot(20, mouth, CreamShade);
                    g.Dot(22, mouth, CreamShade);
                    break;
                case Expression.Sad:
                    g.Row(mouth, 19, 22, line);
                    g.Both(18, mouth + 1, line);
                    break;
                case Expression.Thinking:
                    // Pursed to one side.
                    g.Row(mouth, 21, 23, line);
                    g.Dot(24, mouth - 1, line);
                    break;
                default:
                    g.Row(mouth, 19, 22, line);
                    if (!beard) g.Row(mouth + 1, 20, 21, f.Shade);
                    break;
            }
            if (f.Fangs && e != Expression.Happy && e != Expression.Surprised)
            {
                // The lower fangs over the lip.
                g.Dot(18, mouth - 1, White);
                g.Dot(23, mouth - 1, White);
            }
        }

        // The marks beside the head, drawn over the outline with a line of their own.
        static void Marks(PixelImage image, Expression e, int down, bool left)
        {
            void Dot(int x, int y, Rgba c) => image[left ? Width - 1 - x : x, y + down] = c;
            void Lined(IReadOnlyList<(int X, int Y, Rgba C)> dots)
            {
                foreach (var (x, y, _) in dots)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            if (Math.Abs(i) + Math.Abs(j) == 1) Dot(x + i, y + j, Ink);
                        }
                    }
                }
                foreach (var (x, y, c) in dots) Dot(x, y, c);
            }
            switch (e)
            {
                case Expression.Surprised:
                    // A drop of sweat at the temple and two ticks of alarm.
                    Lined(new[] { (34, 16, Drop), (34, 17, Drop), (33, 18, DropLight), (34, 18, Drop), (33, 19, Drop), (34, 19, Drop), (33, 20, Drop), (34, 20, Drop),
                        (35, 11, White), (36, 10, White), (37, 14, White), (38, 14, White) });
                    break;
                case Expression.Angry:
                    // The crossed vein.
                    Lined(new[] { (32, 9, Mark), (33, 10, Mark), (36, 9, Mark), (35, 10, Mark), (32, 14, Mark), (33, 13, Mark), (36, 14, Mark), (35, 13, Mark) });
                    break;
                case Expression.Thinking:
                    Lined(new[] { (33, 14, White), (36, 11, White), (37, 11, White), (36, 12, White), (37, 12, White),
                        (39, 6, White), (40, 6, White), (39, 7, White), (40, 7, White), (39, 8, White), (40, 8, White), (38, 7, White) });
                    break;
            }
        }

        // ---- The looks of the story ----

        // Brown hair cut short, a tunic in their colour over a cream shirt. Mina is a child with two tufts tied in
        // her colour; the harbour master has a stubbled jaw; the miner a smudge of dust and a rougher fringe; the
        // royal trader hair to her jaw and a gold earring.
        static void Villager(Canvas g, string id, Ramp cloth, Face f, Expression e)
        {
            bool child = id == "mina";
            bool bob = id == "royal_trader";
            f.Child = child;
            f.Nose = !child;
            g.Down = child ? 4 : 0;
            if (bob) g.Box(8, 14, 33, 31, HairDeep);
            Shoulders(g, cloth, child);
            Neck(g, f, child ? 3 : 4);
            Opening(g, f);
            // The shirt between the tunic's edges.
            for (int y = ShoulderTop + 3; y < Height; y++)
            {
                g.Row(y, 17, 24, Cream);
                g.Row(y, 23, 24, CreamShade);
                g.Both(16, y, cloth.Shade);
            }
            g.Row(ShoulderTop + 3, 17, 24, cloth.Shade);
            Head(g, f, ears: !bob);
            if (id == "harbor_master")
            {
                Stubble(g, f);
                f.Bushy = true;
            }
            if (id == "miner")
            {
                g.Row(26, 26, 28, f.Shade.Mix(IronShade, 0.5f));
                g.Row(27, 27, 28, f.Shade.Mix(IronShade, 0.5f));
                g.Dot(13, 19, f.Shade.Mix(IronShade, 0.5f));
            }
            Features(g, f, e);
            HairCap(g, HairDark, HairLight, HairDeep);
            // The fringe and the hair before the ears.
            g.Row(18, 10, 14, HairDark);
            g.Row(18, 27, 31, HairDeep);
            g.Row(19, 10, 12, HairDark);
            g.Row(19, 29, 31, HairDeep);
            g.Both(10, 20, HairDark);
            g.Both(10, 21, HairDark);
            g.Dot(31, 20, HairDeep);
            g.Dot(31, 21, HairDeep);
            if (id == "miner")
            {
                g.Row(18, 17, 19, HairDark);
                g.Row(18, 22, 24, HairDark);
                g.Dot(18, 19, HairDark);
                g.Row(8, 17, 19, HairDark);
                g.Row(8, 23, 24, HairDark);
            }
            else if (child)
            {
                g.Row(18, 15, 22, HairDark);
                g.Row(19, 13, 14, HairDark);
                g.Row(19, 17, 18, HairDark);
                // Tufts tied at the sides.
                foreach (int x in new[] { 6, 32 })
                {
                    g.Box(x, 17, x + 3, 22, x == 6 ? HairDark : HairDeep);
                    g.Box(x + 1, 23, x + 2, 24, x == 6 ? HairDark : HairDeep);
                }
                g.Box(9, 18, 9, 20, cloth.Main);
                g.Box(32, 18, 32, 20, cloth.Main);
                g.Dot(7, 18, HairLight);
            }
            else if (bob)
            {
                // To the jaw on both sides, parted on the left.
                g.Box(8, 13, 9, 30, HairDark);
                g.Box(10, 18, 10, 31, HairDark);
                g.Box(31, 18, 31, 31, HairDeep);
                g.Box(32, 13, 33, 30, HairDeep);
                g.Row(18, 15, 20, HairDark);
                g.Row(19, 13, 16, HairDark);
                g.Dot(9, 16, HairLight);
                g.Dot(9, 17, HairLight);
                g.Dot(10, 27, Gold);
                g.Dot(10, 28, GoldShade);
                // A gold clasp at the collar.
                g.Row(ShoulderTop + 3, 19, 22, Gold);
                g.Row(ShoulderTop + 4, 20, 21, GoldShade);
            }
            else
            {
                g.Row(18, 15, 17, HairDark);
                g.Dot(22, 18, HairDark);
                g.Dot(23, 18, HairDark);
            }
        }

        // Long dark hair over the shoulders, a dress in her colour with a light collar. Lu is a child with a full
        // fringe; Ida is older, her hair parted and tucked behind a band.
        static void Girl(Canvas g, string id, Ramp cloth, Face f, Expression e)
        {
            bool child = id != "fisher_ida";
            f.Child = child;
            f.Nose = !child;
            f.Brow = GirlHairDeep;
            g.Down = child ? 3 : 0;
            // The hair behind.
            for (int y = 10; y <= 40; y++) g.Sym(y, y < 12 ? 11 : 13, GirlHair);
            for (int y = 12; y <= 40; y++) g.Row(y, 31, 33, GirlHairDeep);
            Shoulders(g, cloth, narrow: true);
            Neck(g, f, 3);
            Opening(g, f, 2);
            // The collar and the light front.
            g.Sym(ShoulderTop + 2, 5, cloth.Light);
            g.Sym(ShoulderTop + 3, 4, cloth.Light);
            g.Sym(ShoulderTop + 2, 2, f.Main);
            g.Box(19, ShoulderTop + 6, 22, ShoulderTop + 8, cloth.Light);
            Head(g, f, ears: false);
            Features(g, f, e);
            HairCap(g, GirlHair, GirlHairLight, GirlHairDeep);
            if (child)
            {
                g.Row(18, 10, 31, GirlHair);
                g.Row(19, 10, 13, GirlHair);
                g.Row(19, 16, 17, GirlHair);
                g.Row(19, 21, 22, GirlHair);
                g.Row(19, 26, 31, GirlHairDeep);
                g.Row(18, 28, 31, GirlHairDeep);
            }
            else
            {
                g.Row(18, 10, 17, GirlHair);
                g.Row(19, 10, 14, GirlHair);
                g.Row(20, 10, 12, GirlHair);
                g.Row(18, 26, 31, GirlHairDeep);
                g.Row(19, 28, 31, GirlHairDeep);
                g.Row(20, 30, 31, GirlHairDeep);
                // A band in her colour.
                g.Row(13, 10, 31, cloth.Main);
                g.Row(13, 10, 14, cloth.Light);
                g.Row(13, 28, 31, cloth.Shade);
            }
            Locks(g, GirlHair, GirlHairLight, GirlHairDeep, 20, 45);
        }

        // A gold crown with a red stone over long light brown hair, a gown in her colour with a jewelled collar.
        static void Queen(Canvas g, Ramp cloth, Face f, Expression e)
        {
            f.Brow = QueenHairDeep;
            for (int y = 10; y <= 40; y++) g.Sym(y, y < 12 ? 11 : 13, QueenHair);
            for (int y = 12; y <= 40; y++) g.Row(y, 31, 33, QueenHairDeep);
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f, 4);
            g.Sym(ShoulderTop + 1, 6, f.Main);
            g.Sym(ShoulderTop + 2, 5, f.Main);
            g.Sym(ShoulderTop + 3, 3, f.Main);
            // The necklace and the gold down the front.
            for (int x = 15; x <= 26; x++) g.Dot(x, ShoulderTop + 4 - Math.Abs(x * 2 - 41) / 8, Gold);
            g.Box(20, ShoulderTop + 4, 21, ShoulderTop + 5, RedLight);
            g.Dot(21, ShoulderTop + 5, Red);
            for (int y = ShoulderTop + 6; y < Height; y++)
            {
                g.Row(y, 19, 22, GoldLight);
                g.Dot(22, y, GoldShade);
                g.Dot(18, y, cloth.Shade);
                g.Dot(23, y, cloth.Dark);
            }
            // The puffed shoulders' folds.
            for (int y = 43; y < Height; y += 3)
            {
                g.Both(8, y, cloth.Shade);
                g.Both(9, y + 1, cloth.Shade);
            }
            Head(g, f, ears: false);
            Features(g, f, e);
            HairCap(g, QueenHair, QueenHairLight, QueenHairDeep);
            // Parted in the middle, swept to the sides.
            g.Row(18, 10, 16, QueenHair);
            g.Row(19, 10, 13, QueenHair);
            g.Row(20, 10, 11, QueenHair);
            g.Row(18, 25, 31, QueenHairDeep);
            g.Row(19, 28, 31, QueenHairDeep);
            g.Row(20, 30, 31, QueenHairDeep);
            Locks(g, QueenHair, QueenHairLight, QueenHairDeep, 20, 47);
            // The crown: a band, five points, the stone in front.
            g.Box(12, 9, 29, 11, Gold);
            g.Row(11, 12, 29, GoldShade);
            g.Row(9, 12, 20, GoldLight);
            foreach (int x in new[] { 12, 16, 20, 24, 28 })
            {
                int tall = x == 20 ? 5 : x == 16 || x == 24 ? 3 : 4;
                g.Box(x, 9 - tall, x + 1, 8, Gold);
                g.Dot(x, 9 - tall, GoldLight);
                g.Box(x + 1, 10 - tall, x + 1, 8, GoldShade);
            }
            g.Box(20, 8, 21, 10, RedLight);
            g.Dot(21, 10, Red);
            g.Dot(20, 8, Rgba.Hex("#FFB0A0"));
            g.Dot(14, 10, Rgba.Hex("#7AC8F0"));
            g.Dot(27, 10, Rgba.Hex("#7AC8F0"));
        }

        // Dark brown hair in spikes, an iron breastplate with a gold buckle, the cape in his colour on the shoulders.
        static void Hero(Canvas g, Ramp cloth, Face f, Expression e)
        {
            Shoulders(g, cloth);
            Neck(g, f);
            // The breastplate between the cape's folds.
            for (int y = ShoulderTop + 1; y < Height; y++)
            {
                int half = Math.Min(11, 6 + (y - ShoulderTop));
                g.Sym(y, half, Iron);
                g.Row(y, 20 + half - 2, 20 + half, IronMid);
                g.Row(y, 21 - half, 21 - half + 1, IronLight);
            }
            g.Sym(ShoulderTop + 1, 7, IronShade);
            g.Sym(ShoulderTop, 5, IronShade);
            g.Sym(ShoulderTop, 3, f.Shade);
            g.Sym(ShoulderTop + 1, 2, f.Shade);
            for (int y = ShoulderTop + 3; y < Height; y++) g.Both(9, y, IronShade);
            // The buckle.
            g.Box(18, 46, 23, 50, Rgba.Hex("#E0B850"));
            g.Box(19, 47, 22, 49, Band);
            g.Row(46, 18, 22, GoldLight);
            g.Box(20, 47, 21, 49, GoldShade);
            // The cape's clasps.
            g.Both(11, ShoulderTop + 2, Gold);
            g.Both(11, ShoulderTop + 3, GoldShade);
            Head(g, f);
            Stubble(g, f);
            // A scar from the war across the left cheek.
            g.Dot(13, 26, f.Deep);
            g.Dot(14, 27, f.Deep);
            g.Dot(15, 28, f.Deep);
            f.Bushy = true;
            Features(g, f, e);
            HairCap(g, HairDark, HairLight, HairDeep);
            // Spikes on top and a jagged fringe.
            foreach (var (x, y) in new[] { (12, 9), (13, 8), (14, 8), (16, 7), (17, 7), (17, 6), (20, 8), (21, 7), (22, 7), (22, 6), (25, 8), (26, 7), (27, 8), (29, 9), (30, 10) })
                g.Dot(x, y, x > 24 ? HairDeep : HairDark);
            g.Row(8, 15, 27, HairDark);
            g.Row(8, 15, 18, HairLight);
            g.Row(18, 10, 13, HairDark);
            g.Row(18, 16, 18, HairDark);
            g.Row(18, 21, 22, HairDark);
            g.Row(18, 25, 31, HairDeep);
            g.Row(19, 10, 11, HairDark);
            g.Dot(17, 19, HairDark);
            g.Row(19, 29, 31, HairDeep);
            for (int y = 20; y <= 23; y++) g.Dot(10, y, HairDark);
            g.Dot(31, 20, HairDeep);
            g.Dot(31, 21, HairDeep);
            g.Dot(31, 22, HairDeep);
            g.Dot(31, 23, HairDeep);
        }

        // Bald, with white hair at the sides and a white beard, in a robe of their colour. The chief's beard is
        // long and he leans on a staff; the foreman's is short and square under heavy brows; the temple keeper wears
        // her white hair long and has no beard; the ore trader's beard ends in a plait tied with gold.
        static void Elder(Canvas g, string id, Ramp cloth, Face f, Expression e)
        {
            bool keeper = id == "temple_keeper";
            bool foreman = id == "foreman";
            bool trader = id == "ore_trader";
            f.Brow = keeper ? GreyDeep : Grey;
            f.Bushy = !keeper;
            if (keeper)
            {
                for (int y = 14; y <= 40; y++) g.Sym(y, 13, Grey);
                for (int y = 14; y <= 40; y++) g.Row(y, 31, 33, GreyShade);
            }
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f, 2);
            // The robe's fold and collar.
            for (int y = ShoulderTop + 2; y < Height; y++) g.Row(y, 20, 21, cloth.Shade);
            g.Sym(ShoulderTop + 2, 6, cloth.Light);
            g.Sym(ShoulderTop + 2, 3, f.Shade);
            if (keeper)
            {
                // A rune stone on a cord.
                for (int x = 16; x <= 25; x++) g.Dot(x, ShoulderTop + 3 + (5 - Math.Abs(x * 2 - 41) / 2) / 2, cloth.Dark);
                g.Box(20, ShoulderTop + 6, 21, ShoulderTop + 8, Rgba.Hex("#7AC8F0"));
                g.Dot(21, ShoulderTop + 8, Rgba.Hex("#3A7AA8"));
                g.Dot(20, ShoulderTop + 6, DropLight);
            }
            Head(g, f, ears: !keeper);
            // The bald crown catches the light; lines of age by the eyes.
            g.Row(12, 14, 17, SkinLight);
            g.Row(13, 13, 14, SkinLight);
            g.Both(13, 26, f.Shade);
            g.Row(16, 17, 24, f.Shade);
            if (!keeper)
            {
                // The beard: a moustache over it, the mouth in the gap.
                int[] halves = foreman ? new[] { 10, 10, 10, 10, 10, 9, 9, 8 } : trader ? new[] { 10, 10, 10, 10, 9, 9, 8, 7, 5, 3, 2, 2, 2, 2, 1 } : new[] { 10, 10, 10, 10, 9, 9, 9, 8, 8, 7, 7, 6, 5, 4, 2 };
                g.Shape(28, halves, Grey, GreyShade, 3);
                g.Both(11, 26, Grey);
                g.Both(11, 27, Grey);
                g.Dot(30, 26, GreyShade);
                g.Dot(30, 27, GreyShade);
                g.Row(28, 15, 26, Grey);
                g.Row(29, 13, 28, Grey);
                g.Row(29, 19, 22, f.Shade);
                // Strands.
                for (int i = 0; i < Math.Min(halves.Length - 3, trader ? 5 : 99); i += 2)
                {
                    g.Dot(15, 32 + i, GreyShade);
                    g.Dot(24, 33 + i, GreyShade);
                }
                if (trader)
                {
                    // The end plaited and tied with gold; a gold ring in the ear.
                    g.Row(39, 19, 22, Gold);
                    g.Row(40, 19, 22, GoldShade);
                    g.Dot(19, 39, GoldLight);
                    g.Dot(10, 27, Gold);
                    g.Dot(10, 28, GoldShade);
                }
                f.Nose = true;
            }
            Features(g, f, e, beard: !keeper);
            if (!keeper)
            {
                // White tufts over the ears.
                for (int y = 17; y <= 25; y++)
                {
                    g.Row(y, y < 19 || y > 23 ? 9 : 8, 10, Grey);
                    g.Row(y, 31, y < 19 || y > 23 ? 32 : 33, GreyShade);
                }
                g.Dot(11, 18, Grey);
                g.Dot(11, 19, Grey);
                g.Dot(30, 18, GreyShade);
                g.Dot(30, 19, GreyShade);
            }
            else
            {
                // Long white hair from a high brow, a braid's band in her colour.
                g.Shape(9, new[] { 7, 9, 10, 11, 11, 11 }, Grey, GreyShade);
                g.Row(10, 15, 19, White);
                g.Row(15, 10, 14, Grey);
                g.Row(16, 10, 12, Grey);
                g.Row(15, 27, 31, GreyShade);
                g.Row(16, 29, 31, GreyShade);
                Locks(g, Grey, GreyShade, GreyShade, 15, 45);
                g.Row(39, 8, 11, cloth.Main);
                g.Row(39, 30, 33, cloth.Shade);
            }
            if (id == "chief" || (!keeper && !foreman && !trader))
            {
                // The staff in his right hand, at the picture's left.
                g.Box(2, 16, 3, Height - 1, Wood);
                for (int y = 16; y < Height; y++) g.Dot(3, y, WoodShade);
                g.Box(1, 12, 4, 16, Wood);
                g.Box(4, 13, 4, 16, WoodShade);
                g.Dot(1, 12, Rgba.Clear);
                g.Dot(4, 12, Rgba.Clear);
                g.Dot(2, 13, Rgba.Hex("#C08A52"));
            }
            if (foreman)
            {
                // A miner's lamp on a strap round the bald head.
                g.Row(14, 10, 31, Band);
                g.Row(15, 10, 31, LeatherShade);
                g.Box(19, 12, 22, 16, IronMid);
                g.Box(20, 13, 21, 15, Rgba.Hex("#FFE07A"));
                g.Dot(20, 13, White);
                g.Row(12, 19, 22, Iron);
            }
        }

        // Horns, pointed ears and red eyes; the skin is their colour, the robe dark with a gold pendant. The elder's
        // horns are long and his chin whiskered white; the watchman's are short, with a scar over one eye and iron
        // on his shoulders; the trader has one horn broken and a gold ring in his ear.
        static void Demon(Canvas g, string id, Ramp skin, Face f, Expression e)
        {
            bool elder = id == "demon_elder";
            bool trader = id == "demon_trader";
            bool watch = !elder && !trader;
            f.Main = skin.Main;
            f.Shade = skin.Shade;
            f.Deep = skin.Dark;
            f.Eye = RedLight;
            f.EyeLight = Rgba.Hex("#FFD0A0");
            f.EyeWhite = Rgba.Hex("#FFE07A");
            f.Brow = Ink;
            f.Mouth = skin.Dark.Scale(0.5f);
            f.Blush = skin.Main.Mix(RedLight, 0.5f);
            f.Fangs = true;
            f.Bushy = true;
            Shoulders(g, new Ramp(RobeLight, Robe, RobeDeep, RobeDeep));
            Neck(g, f);
            // A high collar and the pendant.
            for (int y = 35; y <= ShoulderTop + 1; y++)
            {
                g.Row(y, 13 + (y < 37 ? 1 : 0), 16, RobeLight);
                g.Row(y, 25, 28 - (y < 37 ? 1 : 0), Robe);
            }
            g.Sym(ShoulderTop + 2, 4, Robe);
            g.Sym(ShoulderTop + 2, 2, f.Shade);
            for (int x = 15; x <= 26; x++) g.Dot(x, ShoulderTop + 3 + (6 - Math.Abs(x * 2 - 41) / 2) / 2, GoldShade);
            g.Box(19, ShoulderTop + 6, 22, ShoulderTop + 9, Gold);
            g.Row(ShoulderTop + 9, 19, 22, GoldShade);
            g.Box(20, ShoulderTop + 7, 21, ShoulderTop + 8, trader ? Rgba.Hex("#3AC86A") : Mark);
            g.Dot(19, ShoulderTop + 6, GoldLight);
            if (watch)
            {
                // Iron on the shoulders.
                for (int y = ShoulderTop + 1; y <= ShoulderTop + 6; y++)
                {
                    int inner = 12 - (y - ShoulderTop - 1) / 2;
                    for (int x = 1; x <= inner - 3; x++)
                    {
                        if (g.At(x, y).A > 0) g.Dot(x, y, y == ShoulderTop + 6 ? IronShade : x < 6 ? Iron : IronMid);
                        if (g.At(Width - 1 - x, y).A > 0) g.Dot(Width - 1 - x, y, y == ShoulderTop + 6 ? IronShade : IronMid);
                    }
                }
                g.Dot(5, ShoulderTop + 3, IronLight);
                g.Dot(4, ShoulderTop + 4, IronLight);
            }
            // The horns behind the head: up and out, the tips turning in.
            int tall = elder ? 11 : trader ? 8 : 7;
            for (int i = 0; i < tall; i++)
            {
                int y = 13 - i;
                int x = 11 - Math.Min(i, 5) - (i > 7 ? 0 : 0) + (i > tall - 3 ? i - (tall - 3) : 0);
                int wide = i < tall - 3 ? 4 : i < tall - 1 ? 3 : 2;
                bool broken = trader && i > 3;
                g.Row(y, x, x + wide - 1, White);
                g.Dot(x + wide - 1, y, GreyShade);
                if (i % 3 == 2) g.Row(y, x, x + wide - 1, GreyShade);
                if (broken) continue;
                int right = Width - 1 - x;
                g.Row(y, right - wide + 1, right, GreyShade);
                g.Dot(right - wide + 1, y, White);
                if (i % 3 == 2) g.Row(y, right - wide + 1, right, GreyDeep);
            }
            Head(g, f, ears: false);
            // Pointed ears.
            foreach (bool rightSide in new[] { false, true })
            {
                var c = rightSide ? f.Shade : f.Main;
                void Ear(int x, int y) => g.Dot(rightSide ? Width - 1 - x : x, y, c);
                Ear(10, 22); Ear(10, 23); Ear(10, 24); Ear(10, 25); Ear(9, 21); Ear(9, 22); Ear(9, 23); Ear(9, 24); Ear(8, 20); Ear(8, 21); Ear(8, 22); Ear(7, 20);
                g.Dot(rightSide ? Width - 1 - 9 : 9, 23, f.Deep);
            }
            // The brow ridge and the hollows of the cheeks.
            g.Row(11, 14, 18, skin.Light);
            g.Row(12, 13, 14, skin.Light);
            g.Both(13, 27, f.Shade);
            g.Both(14, 28, f.Shade);
            if (elder)
            {
                g.Row(15, 16, 25, f.Shade);
                g.Row(17, 17, 24, f.Shade);
                g.Both(12, 25, f.Shade);
                // White whiskers at the chin.
                g.Shape(32, new[] { 3, 3, 2, 2, 1, 1 }, Grey, GreyShade, 1);
                f.Brow = Grey;
            }
            if (watch)
            {
                // A scar down through the left brow.
                for (int y = 17; y <= 26; y++)
                {
                    if (y < 21 || y > 24) g.Dot(13 + (y > 21 ? 1 : 0), y, skin.Light);
                }
            }
            if (trader)
            {
                g.Dot(8, 23, Gold);
                g.Dot(8, 24, Gold);
                g.Dot(7, 24, GoldShade);
                g.Dot(8, 25, GoldShade);
            }
            Features(g, f, e, beard: false);
        }

        // An iron helmet with cheek guards, iron on the shoulders over a tunic in his colour with a gold badge, the
        // spear upright at his side.
        static void Guard(Canvas g, Ramp cloth, Face f, Expression e)
        {
            Shoulders(g, cloth);
            Neck(g, f);
            g.Sym(ShoulderTop, 6, cloth.Shade);
            g.Sym(ShoulderTop + 1, 8, cloth.Shade);
            g.Sym(ShoulderTop, 3, f.Shade);
            // Pauldrons.
            for (int y = ShoulderTop + 1; y <= ShoulderTop + 8; y++)
            {
                for (int x = 1; x <= 9; x++)
                {
                    if (g.At(x, y).A > 0) g.Dot(x, y, y == ShoulderTop + 8 ? IronShade : x + (y - ShoulderTop) < 9 ? IronLight : Iron);
                    if (g.At(Width - 1 - x, y).A > 0) g.Dot(Width - 1 - x, y, y == ShoulderTop + 8 ? IronShade : IronMid);
                }
            }
            // The badge.
            g.Box(19, 44, 22, 48, Gold);
            g.Row(49, 20, 21, Gold);
            g.Box(22, 44, 22, 48, GoldShade);
            g.Dot(19, 44, GoldLight);
            g.Dot(21, 49, GoldShade);
            Head(g, f, ears: false);
            Features(g, f, e);
            // The helmet: a dome with a ridge, a brim, guards down the cheeks.
            g.Shape(8, new[] { 6, 9, 10, 11, 11, 11, 11, 11, 11 }, Iron, IronMid, 3);
            g.Row(17, 9, 32, IronShade);
            g.Row(18, 9, 32, IronShade);
            g.Row(17, 9, 20, IronMid);
            g.Row(9, 14, 18, IronLight);
            g.Row(10, 12, 15, IronLight);
            g.Row(11, 11, 12, IronLight);
            g.Box(20, 6, 21, 16, IronLight);
            g.Box(21, 7, 21, 16, IronMid);
            for (int y = 19; y <= 28; y++)
            {
                int wide = y < 26 ? 3 : y < 28 ? 2 : 1;
                g.Row(y, 9, 9 + wide - 1, Iron);
                g.Dot(9 + wide - 1, y, IronMid);
                g.Row(y, 32 - wide + 1, 32, IronMid);
                g.Dot(32 - wide + 1, y, IronShade);
            }
            // The nose guard.
            g.Box(20, 19, 21, 21, IronMid);
            g.Dot(20, 19, Iron);
            // The spear at the right of the picture.
            g.Box(37, 13, 38, Height - 1, Wood);
            for (int y = 13; y < Height; y++) g.Dot(38, y, WoodShade);
            int[] tip = { 1, 1, 2, 2, 3, 3, 3, 2, 2, 1 };
            for (int i = 0; i < tip.Length; i++)
            {
                int half = tip[i];
                g.Row(3 + i, 38 - half, 37 + half, Iron);
                g.Row(3 + i, 38, 37 + half, IronMid);
                g.Dot(38 - half, 3 + i, IronLight);
            }
            g.Box(36, 13, 39, 14, GoldShade);
        }

        // ---- The keepers of the stalls ----

        // Dora: a red headscarf sewn with gold coins over brown hair, gold at the collar of a dress in her colour.
        static void Merchant(Canvas g, Ramp cloth, Face f, Expression e)
        {
            // Hair behind, to the shoulders.
            for (int y = 14; y <= 40; y++) g.Sym(y, 12, HairDark);
            for (int y = 14; y <= 40; y++) g.Row(y, 31, 32, HairDeep);
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f, 3);
            // The gold collar, the light front, the belt's buckle.
            for (int x = 14; x <= 27; x++)
            {
                int y = ShoulderTop + 4 - Math.Abs(x * 2 - 41) / 7;
                g.Dot(x, y, x > 23 ? GoldShade : Gold);
                g.Dot(x, y + 1, x > 23 ? GoldShade : Gold);
            }
            for (int y = ShoulderTop + 6; y < Height; y++) g.Row(y, 20, 21, cloth.Light);
            g.Box(19, ShoulderTop + 5, 22, ShoulderTop + 6, GoldLight);
            Head(g, f, ears: false);
            Features(g, f, e);
            // Hair at the sides of the face, an earring of a coin.
            for (int y = 18; y <= 39; y++)
            {
                g.Row(y, 9, y < 22 ? 11 : 10, HairDark);
                g.Row(y, y < 22 ? 30 : 31, 32, HairDeep);
            }
            g.Dot(10, 21, HairLight);
            g.Dot(10, 24, HairLight);
            g.Dot(9, 28, HairLight);
            g.Box(10, 27, 11, 28, Gold);
            g.Dot(11, 28, GoldShade);
            // The scarf: over the crown, a darker band at the brow, three coins, the knot's tail at the right.
            g.Shape(7, new[] { 7, 9, 11, 12, 12, 12, 12, 12, 12, 12, 12 }, RedLight, Red, 3);
            g.Row(16, 9, 32, Red);
            g.Row(17, 9, 32, Red);
            g.Row(17, 22, 32, RedDeep);
            g.Row(8, 15, 19, Rgba.Hex("#F08A78"));
            g.Row(9, 12, 15, Rgba.Hex("#F08A78"));
            g.Dot(11, 10, Rgba.Hex("#F08A78"));
            foreach (int x in new[] { 12, 20, 28 })
            {
                g.Box(x, 12, x + 1, 13, Gold);
                g.Dot(x, 12, GoldLight);
                g.Dot(x + 1, 13, GoldShade);
            }
            g.Box(33, 15, 35, 19, Red);
            g.Box(34, 20, 36, 24, Red);
            g.Box(35, 15, 35, 19, RedDeep);
            g.Box(36, 20, 36, 24, RedDeep);
            g.Dot(35, 25, Red);
            g.Dot(33, 16, RedLight);
            // A fringe under the scarf.
            g.Row(18, 12, 15, HairDark);
            g.Row(18, 27, 29, HairDeep);
            g.Dot(12, 19, HairDark);
        }

        // Gangcheol: bald under a headband, heavy brows and a big brown beard, a leather apron over bare shoulders
        // and a shirt of his colour.
        static void Blacksmith(Canvas g, Ramp cloth, Face f, Expression e)
        {
            f.Brow = Beard;
            f.Bushy = true;
            Shoulders(g, cloth);
            Neck(g, f, 5);
            Opening(g, f, 2);
            // The apron: a bib on two straps.
            for (int y = ShoulderTop + 4; y < Height; y++)
            {
                g.Row(y, 12, 29, Leather);
                g.Row(y, 27, 29, LeatherShade);
                g.Dot(12, y, LeatherLight);
            }
            g.Row(ShoulderTop + 4, 12, 29, LeatherLight);
            for (int y = ShoulderTop; y <= ShoulderTop + 3; y++)
            {
                g.Row(y, 13, 15, Leather);
                g.Row(y, 26, 28, LeatherShade);
            }
            g.Both(14, ShoulderTop + 5, IronMid);
            g.Box(18, 47, 23, 51, LeatherShade);
            Head(g, f);
            g.Row(11, 14, 18, SkinLight);
            g.Row(12, 13, 14, SkinLight);
            // The beard from ear to ear, lighter strands, a moustache.
            g.Shape(27, new[] { 10, 11, 11, 11, 11, 11, 11, 10, 9, 8, 6, 4 }, Beard, HairDeep, 3);
            for (int y = 23; y <= 26; y++)
            {
                g.Row(y, 11, 12, Beard);
                g.Row(y, 29, 30, HairDeep);
            }
            g.Row(27, 14, 27, f.Main);
            g.Row(27, 26, 27, f.Shade);
            g.Row(28, 16, 25, BeardLight);
            g.Row(29, 14, 27, BeardLight);
            g.Row(29, 19, 22, Beard);
            foreach (var (x, y) in new[] { (13, 31), (14, 33), (17, 34), (18, 32), (23, 33), (16, 36), (21, 35), (20, 37) }) g.Dot(x, y, BeardLight);
            Features(g, f, e, beard: true);
            // The headband.
            g.Row(15, 10, 31, Band);
            g.Row(16, 10, 31, Band);
            g.Row(15, 10, 20, Leather);
            g.Row(16, 29, 31, HairDeep);
        }

        // Haru: a wide straw hat with a red band, brown hair under it, a shirt of his colour open over cream.
        static void Fisher(Canvas g, Ramp cloth, Face f, Expression e)
        {
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f);
            for (int y = ShoulderTop + 3; y < Height; y++)
            {
                g.Row(y, 18, 23, Cream);
                g.Row(y, 22, 23, CreamShade);
                g.Both(17, y, cloth.Shade);
            }
            g.Row(ShoulderTop + 3, 18, 23, cloth.Shade);
            Head(g, f);
            Features(g, f, e);
            // Hair under the brim.
            g.Row(17, 10, 31, HairDark);
            g.Row(18, 10, 14, HairDark);
            g.Row(18, 27, 31, HairDeep);
            g.Row(19, 10, 12, HairDark);
            g.Row(19, 29, 31, HairDeep);
            g.Row(18, 17, 18, HairDark);
            for (int y = 20; y <= 22; y++)
            {
                g.Dot(10, y, HairDark);
                g.Dot(31, y, HairDeep);
            }
            // The hat: crown, band, brim (its underside in shadow).
            g.Shape(4, new[] { 6, 8, 9, 9, 9, 9, 9 }, Gold, GoldShade, 3);
            g.Row(5, 15, 19, GoldLight);
            g.Row(6, 13, 15, GoldLight);
            for (int y = 6; y <= 10; y += 2)
            {
                for (int x = 13 + y % 4; x <= 26; x += 4) g.Dot(x, y, GoldShade);
            }
            g.Row(11, 11, 30, RedLight);
            g.Row(12, 11, 30, Red);
            g.Row(11, 26, 30, Red);
            g.Row(13, 3, 38, Gold);
            g.Row(14, 1, 40, Gold);
            g.Row(15, 1, 40, GoldShade);
            g.Row(16, 3, 38, GoldShade);
            g.Row(13, 5, 18, GoldLight);
            g.Row(16, 9, 32, Rgba.Hex("#8A6420"));
            for (int x = 3; x <= 38; x += 4) g.Dot(x, 14, GoldShade);
        }

        // Yuri: a pointed hat in her colour with a gold band, its tip bent over, long white hair, a robe trimmed
        // with gold.
        static void Alchemist(Canvas g, Ramp cloth, Face f, Expression e)
        {
            f.Brow = GreyDeep;
            for (int y = 14; y <= 40; y++) g.Sym(y, 13, Grey);
            for (int y = 14; y <= 40; y++) g.Row(y, 31, 33, GreyShade);
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f, 2);
            g.Sym(ShoulderTop + 2, 5, Gold);
            g.Sym(ShoulderTop + 2, 2, f.Shade);
            for (int y = ShoulderTop + 3; y < Height; y++)
            {
                g.Row(y, 20, 21, Gold);
                g.Dot(21, y, GoldShade);
            }
            // A vial on a cord.
            g.Box(26, ShoulderTop + 6, 27, ShoulderTop + 9, Rgba.Hex("#3AC86A"));
            g.Dot(26, ShoulderTop + 6, Rgba.Hex("#B8FFB0"));
            g.Row(ShoulderTop + 5, 26, 27, GreyShade);
            Head(g, f, ears: false);
            Features(g, f, e);
            // The hair down both sides.
            g.Row(18, 10, 14, Grey);
            g.Row(19, 10, 12, Grey);
            g.Row(18, 27, 31, GreyShade);
            g.Row(19, 29, 31, GreyShade);
            Locks(g, Grey, GreyShade, GreyShade, 17, 45);
            // The hat: the cone leaning right, the band, the brim.
            (int Half, int Shift)[] cone = { (1, 9), (2, 8), (2, 6), (3, 5), (3, 3), (4, 2), (5, 1), (6, 1), (7, 0), (8, 0), (9, 0) };
            for (int i = 0; i < cone.Length; i++)
            {
                var (half, shift) = cone[i];
                int y = 2 + i;
                g.Row(y, 21 - half + shift, 20 + half + shift, cloth.Main);
                g.Row(y, Math.Max(21 + shift, 20 + half + shift - 2), 20 + half + shift, cloth.Shade);
                g.Dot(21 - half + shift, y, cloth.Light);
            }
            g.Row(13, 11, 30, Gold);
            g.Row(14, 11, 30, GoldShade);
            g.Row(13, 11, 18, GoldLight);
            g.Row(15, 5, 36, cloth.Main);
            g.Row(16, 4, 37, cloth.Shade);
            g.Row(17, 6, 35, cloth.Dark);
            g.Row(15, 5, 16, cloth.Light);
            g.Row(15, 30, 36, cloth.Shade);
            // A star on the cone.
            g.Dot(19, 9, GoldLight);
            g.Dot(18, 10, Gold);
            g.Dot(19, 10, GoldLight);
            g.Dot(20, 10, Gold);
            g.Dot(19, 11, Gold);
        }

        // Moa, who walks between the camps: a hooded travelling cloak in his colour, a bell at the collar, a
        // bedroll over one shoulder, a traveller's stubble.
        static void Wanderer(Canvas g, Ramp cloth, Face f, Expression e)
        {
            f.Bushy = true;
            // The hood's inside behind the head.
            for (int y = 8; y <= 37; y++) g.Sym(y, y < 10 ? 10 : 13, cloth.Dark);
            // The bedroll behind the right shoulder.
            g.Box(30, 33, 40, 39, Wood);
            g.Box(30, 38, 40, 39, WoodShade);
            g.Row(33, 31, 39, Rgba.Hex("#C08A52"));
            g.Box(34, 33, 34, 39, LeatherShade);
            g.Box(38, 33, 38, 39, LeatherShade);
            Shoulders(g, cloth);
            Neck(g, f);
            Opening(g, f, 2);
            // The cloak's edges meet at a clasp; the bell hangs from it.
            for (int y = ShoulderTop + 2; y < Height; y++)
            {
                g.Row(y, 20, 21, cloth.Dark);
                g.Dot(19, y, cloth.Light);
            }
            g.Sym(ShoulderTop + 2, 6, cloth.Light);
            g.Sym(ShoulderTop + 2, 2, f.Shade);
            g.Row(ShoulderTop + 3, 20, 21, Band);
            g.Row(ShoulderTop + 4, 20, 21, Gold);
            g.Box(19, ShoulderTop + 5, 22, ShoulderTop + 7, Gold);
            g.Row(ShoulderTop + 8, 18, 23, Gold);
            g.Box(22, ShoulderTop + 5, 22, ShoulderTop + 7, GoldShade);
            g.Row(ShoulderTop + 8, 22, 23, GoldShade);
            g.Dot(19, ShoulderTop + 5, GoldLight);
            g.Dot(20, ShoulderTop + 5, GoldLight);
            g.Row(ShoulderTop + 9, 20, 21, GoldShade);
            g.Row(ShoulderTop + 7, 20, 21, Band);
            Head(g, f, ears: false);
            Stubble(g, f);
            Features(g, f, e);
            // Brown hair under the hood.
            g.Row(15, 11, 30, HairDark);
            g.Row(16, 11, 30, HairDark);
            g.Row(17, 11, 30, HairDark);
            g.Row(18, 11, 15, HairDark);
            g.Row(18, 19, 21, HairDark);
            g.Row(18, 26, 30, HairDeep);
            g.Row(19, 11, 12, HairDark);
            g.Row(19, 29, 30, HairDeep);
            g.Row(15, 22, 30, HairDeep);
            g.Row(16, 13, 16, HairLight);
            // The hood over the crown and down the cheeks.
            g.Shape(6, new[] { 6, 9, 11, 12, 13, 13, 13, 13, 13 }, cloth.Main, cloth.Shade, 3);
            g.Row(14, 9, 32, cloth.Shade);
            g.Row(7, 14, 19, cloth.Light);
            g.Row(8, 11, 14, cloth.Light);
            g.Row(9, 10, 11, cloth.Light);
            for (int y = 15; y <= 37; y++)
            {
                int wide = y < 20 ? 3 : y < 32 ? 2 : 3 + (y - 32) / 2;
                g.Row(y, 8, 8 + wide - 1, cloth.Main);
                g.Dot(8, y, cloth.Light);
                g.Row(y, 33 - wide + 1, 33, cloth.Shade);
            }
        }
    }
}
