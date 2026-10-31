using System.Collections.Generic;

namespace Game.Client
{
    // Frames for the menus and windows, in the colours of the caves: dark stone panels with a lit bevel, raised wooden
    // buttons that sink when pressed, and dark insets for text fields, scroll tracks and bars. Each is drawn small and
    // stretched by its middle (nine-slice), so its edge pixels keep their size; Border says how many pixels at each
    // edge to keep. UiSkin turns them into the IMGUI skin.
    static class UiArt
    {
        // Every frame is drawn at twice its pixel size, so its edges read on a desktop screen too.
        public const int Scale = 2;

        static readonly Dictionary<char, Rgba> PanelColors = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#0E0A08"),
            ['L'] = Rgba.Hex("#7A6450"),
            ['M'] = Rgba.Hex("#52402F"),
            ['d'] = Rgba.Hex("#33281F"),
            ['f'] = Rgba.Hex("#1C1612"),
        };

        public const int PanelBorder = 3;

        public static readonly PixelImage Panel = PixelImage.FromRows(PanelColors,
            ".oooooooo.",
            "oLLLLLLLMo",
            "oLMMMMMMdo",
            "oLMffffMdo",
            "oLMffffMdo",
            "oLMffffMdo",
            "oLMffffMdo",
            "oMMMMMMMdo",
            "oMdddddddo",
            ".oooooooo.");

        public const int ButtonBorder = 2;

        static readonly string[] Raised =
        {
            ".oooooo.",
            "oLLLLLLo",
            "oLMMMMdo",
            "oLMMMMdo",
            "oLMMMMdo",
            "oLMMMMdo",
            "oddddddo",
            ".oooooo.",
        };

        static readonly string[] Sunk =
        {
            ".oooooo.",
            "oddddddo",
            "odMMMMLo",
            "odMMMMLo",
            "odMMMMLo",
            "odMMMMLo",
            "oLLLLLLo",
            ".oooooo.",
        };

        // Wood for a button, a little lighter under the pointer, gold when it is the chosen tab.
        public static PixelImage Button(bool hover = false, bool pressed = false, bool chosen = false)
        {
            var wood = chosen ? Ramp.Of("#9A7436") : hover ? Ramp.Of("#7E6046") : Ramp.Of("#6A503A");
            var colors = new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#0E0A08"),
                ['L'] = wood.Light,
                ['M'] = wood.Main,
                ['d'] = wood.Dark,
            };
            return PixelImage.FromRows(colors, pressed ? Sunk : Raised);
        }

        public const int InsetBorder = 2;

        static readonly Dictionary<char, Rgba> InsetColors = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#0E0A08"),
            ['s'] = Rgba.Hex("#0A0806"),
            ['f'] = Rgba.Hex("#17120F"),
            ['h'] = Rgba.Hex("#4A3C30"),
        };

        // A dark well with its shadow at the top left and light at the bottom right.
        public static readonly PixelImage Inset = PixelImage.FromRows(InsetColors,
            "oooooooo",
            "ossssssh",
            "osffffhh",
            "osffffhh",
            "osffffhh",
            "osffffhh",
            "ohhhhhhh",
            "oooooooo");

        // The fill of a bar (health, the boss), stretched to the share it shows: a lit top row over the colour.
        public static PixelImage BarFill(Rgba color)
        {
            var ramp = Ramp.Of(color);
            var image = new PixelImage(4, 6);
            for (int x = 0; x < 4; x++)
            {
                image[x, 0] = ramp.Light;
                image[x, 1] = ramp.Main.Mix(ramp.Light, 0.4f);
                for (int y = 2; y < 5; y++) image[x, y] = ramp.Main;
                image[x, 5] = ramp.Shade;
            }
            return image;
        }

        // Each pixel as a Scale-by-Scale block.
        public static PixelImage Enlarged(PixelImage image)
        {
            var result = new PixelImage(image.Width * Scale, image.Height * Scale);
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x / Scale, y / Scale];
            }
            return result;
        }
    }
}
