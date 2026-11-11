using System.Collections.Generic;

namespace Game.Client
{
    // Frames for the menus and windows, in the colours of the caves: dark stone panels in a thin gold rim, dark
    // buttons with a gold-brown rim that sink when pressed, and dark insets for text fields, scroll tracks and bars. Each is drawn small and
    // stretched by its middle (nine-slice), so its edge pixels keep their size; Border says how many pixels at each
    // edge to keep. UiSkin turns them into the IMGUI skin.
    static class UiArt
    {
        // Every frame is drawn at twice its pixel size, so its edges read on a desktop screen too.
        public const int Scale = 2;

        // Dark stone with a thin rim of old gold and a bright stud at each corner (2026-11-11, the owner: the UI looked
        // crude; after the framed dark panels of Core Keeper and Buch's Golden UI, CC0, in our own colours).
        static readonly Dictionary<char, Rgba> PanelColors = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#0B0807"),
            ['W'] = Rgba.Hex("#F4DE9C"),
            ['G'] = Rgba.Hex("#C49A55"),
            ['s'] = Rgba.Hex("#6B5230"),
            ['k'] = Rgba.Hex("#100C0A"),
            ['F'] = Rgba.Hex("#2A211B"),
            ['f'] = Rgba.Hex("#1D1713"),
            ['d'] = Rgba.Hex("#16110E"),
        };

        public const int PanelBorder = 4;

        public static readonly PixelImage Panel = PixelImage.FromRows(PanelColors,
            ".oooooooooo.",
            "oWGGGGGGGGWo",
            "oGkkkkkkkkso",
            "oGkFFFFFFkso",
            "oGkFffffFkso",
            "oGkFffffFkso",
            "oGkFffffFkso",
            "oGkFffffFkso",
            "oGkddddddkso",
            "oGkkkkkkkkso",
            "oWssssssssWo",
            ".oooooooooo.");

        public const int ButtonBorder = 3;

        static readonly string[] Raised =
        {
            ".oooooooo.",
            "orrrrrrrro",
            "orLLLLLLro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orddddddro",
            "oqqqqqqqqo",
            ".oooooooo.",
        };

        static readonly string[] Sunk =
        {
            ".oooooooo.",
            "oqqqqqqqqo",
            "orddddddro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orMMMMMMro",
            "orLLLLLLro",
            "orrrrrrrro",
            ".oooooooo.",
        };

        // Dark leather-brown with a gold-brown rim; under the pointer the rim lights up, and the chosen tab is gold.
        public static PixelImage Button(bool hover = false, bool pressed = false, bool chosen = false)
        {
            var fill = chosen ? Ramp.Of("#8C6A34") : hover ? Ramp.Of("#4A3A2C") : Ramp.Of("#3A2D23");
            var rim = chosen ? Rgba.Hex("#F0D490") : hover ? Rgba.Hex("#D8B06A") : Rgba.Hex("#8C6C40");
            var colors = new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#0B0807"),
                ['r'] = rim,
                ['q'] = rim.Mix(Rgba.Hex("#0B0807"), 0.45f),
                ['L'] = fill.Light,
                ['M'] = fill.Main,
                ['d'] = fill.Dark,
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
