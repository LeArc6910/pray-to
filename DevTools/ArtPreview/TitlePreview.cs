using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The title rework's art laid out to be looked at (묶음 T2, Docs/TitleRework.md, --only title, out/title/): the
    // hero's looks (every hair style in the five views, every colour of each choice, the default beside the hero as
    // drawn before looks), random looks through the motions, the logo and its glint, the text buttons, rows and panel
    // on a dark cave, the biomes' icons and a loading scene for each biome. False when the default look is not the
    // old hero pixel for pixel.
    static class TitlePreview
    {
        static readonly Rgba Back = Rgba.Hex("#14121A"), Text = Rgba.Hex("#E8E4F0"), Dim = Rgba.Hex("#8E8AA4"), Ground = Rgba.Hex("#3A3038");
        static readonly Rgba OldTunic = Rgba.Hex("#3F7FD8");

        public static bool Write(string output)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "title")).FullName;
            bool ok = Looks(Path.Combine(folder, "looks.png"));
            Moves(Path.Combine(folder, "looks_moves.png"));
            LogoSheet(folder);
            Buttons(Path.Combine(folder, "buttons.png"));
            BiomeIcons(Path.Combine(folder, "biome_icons.png"));
            // The screens as T3 lays them out (TitleScreensPreview).
            TitleScreensPreview.Write(folder);
            foreach (var biome in TitleArt.BiomeOrder)
            {
                // Each biome twice: its own monsters alone, and another seed with the default hero standing in it.
                var pair = new PixelImage(LoadingArt.Width * 2 + 4, LoadingArt.Height);
                pair.Fill(0, 0, pair.Width, pair.Height, Back);
                pair.Draw(LoadingArt.Scene(biome, 1), 0, 0);
                pair.Draw(LoadingArt.Scene(biome, 2, hero: new CharacterLook(1 + (int)biome, (int)biome, (int)biome, 0, 2 + (int)biome, (int)biome)), LoadingArt.Width + 4, 0);
                Png.Save(pair.Times(4), Path.Combine(folder, $"loading_{biome.ToString().ToLowerInvariant()}.png"));
            }
            return ok;
        }

        // The logo at rest at one, two and three times its size on the cave, and every step of the glint.
        static void LogoSheet(string folder)
        {
            var logo = TitleArt.Logo;
            var back = Cave(LoadingArt.Width * 2, 150, 5);
            var g = back.Times(1);
            Over(g, logo, 8, 6);
            Over(g, logo.Times(2), 8 + logo.Width + 10, 6);
            Over(g, logo.Times(3), 40, 62);
            Png.Save(g.Times(2), Path.Combine(folder, "logo.png"));
            const int columns = 4;
            int rows = (TitleArt.GlintFrames + columns - 1) / columns;
            var sheet = new PixelImage(columns * (logo.Width + 6) + 6, rows * (logo.Height + 6) + 6);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Back);
            for (int f = 0; f < TitleArt.GlintFrames; f++) Over(sheet, TitleArt.LogoWithGlint(f), 6 + f % columns * (logo.Width + 6), 6 + f / columns * (logo.Height + 6));
            Png.Save(sheet.Times(3), Path.Combine(folder, "logo_glint.png"));
        }

        // A dim cave to lay the menus' pieces on: a loading scene drawn wide and darkened.
        static PixelImage Cave(int width, int height, int seed)
        {
            var image = LoadingArt.Scene(Biome.Cave, seed, new (string, Rgba)[0], null, width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    image[x, y] = image[x, y].Scale(0.6f);
            return image;
        }

        // An image stretched by its middle, its border kept (as the screens draw a panel or a row).
        static void Nine(PixelImage g, PixelImage piece, int border, int left, int top, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sy = y < border ? y : y >= height - border ? piece.Height - (height - y) : border + (y - border) % (piece.Height - border * 2);
                for (int x = 0; x < width; x++)
                {
                    int sx = x < border ? x : x >= width - border ? piece.Width - (width - x) : border + (x - border) % (piece.Width - border * 2);
                    g.Blend(left + x, top + y, piece[sx, sy]);
                }
            }
        }

        static readonly TitleState[] States = { TitleState.Normal, TitleState.Hover, TitleState.Selected, TitleState.Pressed };

        // A text item as the title screen draws one: its words, the marks at its sides and the strip under it.
        static void TextItem(PixelImage g, int middle, int y, string words, TitleState state)
        {
            int scale = 1;
            int width = MockFont.Width(words, scale);
            int x = middle - width / 2;
            var strip = TitleArt.Strip(width + 28, state);
            if (strip != null) Over(g, strip, middle - strip.Width / 2, y + 8);
            g.Text(x, y + (state == TitleState.Pressed ? 1 : 0), words, TitleArt.Ink(state), TitleArt.InkShadow, scale);
            var mark = TitleArt.Mark(state);
            if (mark == null) return;
            Over(g, mark, x - 6 - mark.Width, y);
            Over(g, mark, x + width + 6, y);
        }

        static void Buttons(string path)
        {
            const int width = 420, height = 236;
            var g = Cave(width, height, 3);
            g.Small(6, 4, "TEXT BUTTONS: REST, POINTED AT, PICKED, PRESSED (NO BOX: INK, MARKS, STRIP)", Dim, Back);
            Over(g, TitleArt.Logo.Times(2), 100 - TitleArt.LogoWidth, 16);
            string[] words = { "GAME START", "PLAY TOGETHER", "SAVES", "SETTINGS" };
            for (int i = 0; i < 4; i++) TextItem(g, 100, 74 + i * 18, words[i], States[i]);
            TextItem(g, 100, 74 + 4 * 18, "FEEDBACK", TitleState.Normal);
            g.Text(100 - MockFont.Width("QUIT") / 2, 74 + 5 * 18, "QUIT", TitleArt.InkOff, TitleArt.InkShadow);
            // Arrows and brackets.
            g.Small(6, 190, "ARROWS (REST, POINTED AT, PRESSED), BRACKETS, PLUS", Dim, Back);
            int ax = 8;
            foreach (var state in new[] { TitleState.Normal, TitleState.Hover, TitleState.Pressed })
            {
                Over(g, TitleArt.Arrow(true, state), ax, 202);
                g.Text(ax + 10, 203, "LONG", TitleArt.Ink(state == TitleState.Normal ? TitleState.Normal : TitleState.Hover), TitleArt.InkShadow);
                Over(g, TitleArt.Arrow(false, state), ax + 10 + MockFont.Width("LONG") + 4, 202);
                ax += 54;
            }
            Over(g, TitleArt.Bracket(true), ax, 201);
            g.Text(ax + 8, 203, "PAGE", TitleArt.Ink(TitleState.Hover), TitleArt.InkShadow);
            Over(g, TitleArt.Bracket(false), ax + 8 + MockFont.Width("PAGE") + 3, 201);
            ax += 50;
            foreach (var state in States)
            {
                Over(g, TitleArt.Plus(state), ax, 202);
                ax += 12;
            }

            // The panel with a page's name, the line under it, rows in each state and the row that makes a new one.
            const int px = 200, py = 16, pw = 210, ph = 212;
            Nine(g, TitleArt.Panel, TitleArt.PanelBorder, px, py, pw, ph);
            string title = "CHARACTERS";
            g.Text(px + pw / 2 - MockFont.Width(title) / 2, py + 10, title, TitleArt.Ink(TitleState.Selected), TitleArt.InkShadow);
            Over(g, TitleArt.Divider(pw - 40), px + 20, py + 21);
            var random = new GameRandom(7);
            string[] names = { "MIRA", "DOLAN", "ASH", "YUNA" };
            for (int i = 0; i < 4; i++)
            {
                int ry = py + 32 + i * 34;
                Nine(g, TitleArt.Row(States[i]), TitleArt.RowBorder, px + 10, ry, pw - 20, 30);
                var style = i == 0 ? CharacterLook.Default : CharacterLook.Random(random);
                Over(g, HeroArt.Figure(HeroArt.Draw(Look.Down, HeroMove.Walk, i * 2, HeroGear.None, style)), px + 16, ry + 5);
                g.Text(px + 38, ry + 6, names[i], TitleArt.Ink(States[i]), TitleArt.InkShadow);
                g.Small(px + 38, ry + 18, $"LV {3 + i * 7}  WARRIOR  {2 + i * 5}H", Dim, default);
                Over(g, TitleArt.BiomeIcon(TitleArt.BiomeOrder[i * 2]), px + pw - 34, ry + 7);
            }
            foreach (var (state, i) in new[] { (TitleState.Normal, 0), (TitleState.Hover, 1) })
            {
                int rx = px + 10 + i * 97, ry = py + 32 + 4 * 34, rw = 93;
                Over(g, TitleArt.NewRow(rw, 26, state), rx, ry);
                Over(g, TitleArt.Plus(state), rx + 8, ry + 9);
                g.Text(rx + 22, ry + 10, "NEW", TitleArt.Ink(state), TitleArt.InkShadow);
            }
            Png.Save(g.Times(3), path);
        }

        static void BiomeIcons(string path)
        {
            var g = new PixelImage(8 * 22 + 6, 16 + 28);
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(4, 3, "CAVE MUSHROOM ICE SAND CRYSTAL LAKE LAVA ABYSS", Dim, Back);
            for (int i = 0; i < TitleArt.BiomeOrder.Length; i++)
            {
                Over(g, TitleArt.BiomeIcon(TitleArt.BiomeOrder[i]), 6 + i * 22, 12);
            }
            // On a row, as a world's row shows one.
            Nine(g, TitleArt.Row(TitleState.Selected), TitleArt.RowBorder, 4, 30, g.Width - 8, 12);
            Png.Save(g.Times(6), path);
        }

        static void Over(PixelImage sheet, PixelImage image, int left, int top)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y)) sheet.Blend(left + x, top + y, image[x, y]);
        }

        static PixelImage Stand(Look view, CharacterLook style, bool flip = false)
        {
            var figure = HeroArt.Figure(HeroArt.Draw(view, HeroMove.Stand, 0, HeroGear.None, style));
            return flip ? figure.FlippedX() : figure;
        }

        static bool Same(PixelImage a, PixelImage b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                {
                    var p = a[x, y];
                    var q = b[x, y];
                    if (p.R != q.R || p.G != q.G || p.B != q.B || p.A != q.A) return false;
                }
            return true;
        }

        // Every style in the five views, in three hair colours; then a row for each choice's colours; then the default
        // look beside the old hero, checked pixel for pixel through every move, weapon and view.
        static bool Looks(string path)
        {
            const int cell = 18, rowH = 24, left = 6;
            var views = new[] { Look.Down, Look.DownSide, Look.Side, Look.UpSide, Look.Up };
            var g = new PixelImage(470, 16 + CharacterLook.HairStyles * rowH + 6 * (rowH + 10) + 8);
            g.Fill(0, 0, g.Width, g.Height, Back);
            int y = 4;
            g.Small(left, y, "HAIR STYLES 0-7 (ROWS) IN THE FIVE VIEWS, IN THREE COLOURS AND SKINS", Text, Back);
            y += 10;
            for (int hair = 0; hair < CharacterLook.HairStyles; hair++)
            {
                g.Fill(left, y + rowH - 4, g.Width - left * 2, 3, Ground);
                for (int set = 0; set < 3; set++)
                {
                    var style = new CharacterLook(hair, new[] { 0, 2, 7 }[set], new[] { 1, 0, 3 }[set], set, new[] { 0, 5, 9 }[set], new[] { 0, 1, 4 }[set]);
                    for (int v = 0; v < views.Length; v++) Over(g, Stand(views[v], style), left + set * (cell * 5 + 10) + v * cell, y);
                }
                y += rowH;
            }
            void Row(string name, int count, Func<int, CharacterLook> make)
            {
                g.Small(left, y, name, Dim, Back);
                y += 8;
                g.Fill(left, y + rowH - 4, g.Width - left * 2, 3, Ground);
                for (int i = 0; i < count; i++)
                {
                    Over(g, Stand(Look.Down, make(i)), left + i * (cell * 2 + 2), y);
                    Over(g, Stand(Look.Side, make(i)), left + i * (cell * 2 + 2) + cell - 2, y);
                }
                y += rowH + 2;
            }
            Row("HAIR COLOURS 0-8", CharacterLook.HairColors, i => new CharacterLook(1, i, 1, 0, 0, 0));
            Row("SKINS 0-4 (CROPPED HAIR, THEN LONG)", CharacterLook.Skins * 2, i => new CharacterLook(i < 5 ? 4 : 1, 0, i % 5, 0, 9, 0));
            Row("EYES 0-5", CharacterLook.EyeColors, i => new CharacterLook(0, 2, 0, i, 0, 0));
            Row("SHIRTS 0-11", CharacterLook.ShirtColors, i => new CharacterLook(0, 0, 1, 0, i, 0));
            Row("TROUSERS 0-11", CharacterLook.PantsColors, i => new CharacterLook(0, 0, 1, 0, 0, i));

            // The default look against the hero as drawn before looks.
            int differ = 0, compared = 0;
            foreach (var weapon in new[] { HeroWeapon.None, HeroWeapon.Sword, HeroWeapon.Bow, HeroWeapon.Staff, HeroWeapon.Pick })
            {
                var gear = HeroGear.Plain(weapon);
                foreach (HeroMove move in Enum.GetValues(typeof(HeroMove)))
                    foreach (var view in views)
                        for (int frame = 0; frame < HeroArt.FramesOf(move, weapon); frame++)
                        {
                            compared++;
                            if (!Same(HeroArt.Draw(view, move, frame, gear, CharacterLook.Default), HeroArt.Draw(view, move, frame, gear, OldTunic))) differ++;
                        }
            }
            g.Small(left, y, $"DEFAULT LOOK (LEFT) AND THE OLD HERO (RIGHT): {compared} FRAMES COMPARED, {differ} DIFFER", differ == 0 ? Text : Rgba.Hex("#FF6060"), Back);
            y += 8;
            g.Fill(left, y + rowH - 4, 120, 3, Ground);
            for (int v = 0; v < views.Length; v++)
            {
                Over(g, Stand(views[v], CharacterLook.Default), left + v * cell, y);
            }
            for (int v = 0; v < views.Length; v++)
                Over(g, HeroArt.Figure(HeroArt.Draw(views[v], HeroMove.Stand, 0, HeroGear.None, OldTunic)), left + 100 + v * cell, y);
            Png.Save(g.Times(4), path);
            Console.WriteLine($"title: default look against the old hero, {compared} frames, {differ} differ.");
            return differ == 0;
        }

        // Random looks (the making screen's dice) through a walk and the three attacks of three weapons, all facing
        // down and to the side.
        static void Moves(string path)
        {
            const int cell = 40, looks = 6;
            var random = new GameRandom(20261125);
            var styles = Enumerable.Range(0, looks).Select(_ => CharacterLook.Random(random)).ToList();
            var columns = new List<(HeroMove Move, HeroWeapon Weapon, int Frame, Look View)>();
            foreach (int f in new[] { 0, 2, 5 }) columns.Add((HeroMove.Walk, HeroWeapon.None, f, Look.DownSide));
            foreach (var weapon in new[] { HeroWeapon.Sword, HeroWeapon.Bow, HeroWeapon.Staff })
            {
                int strike = Math.Max(0, HeroArt.StrikeFrame(HeroMove.Attack1, weapon));
                columns.Add((HeroMove.Attack1, weapon, Math.Max(0, strike - 2), Look.Side));
                columns.Add((HeroMove.Attack1, weapon, strike, Look.DownSide));
                columns.Add((HeroMove.Attack3, weapon, Math.Max(0, HeroArt.StrikeFrame(HeroMove.Attack3, weapon)), Look.UpSide));
            }
            columns.Add((HeroMove.Roll, HeroWeapon.Sword, 5, Look.Side));
            columns.Add((HeroMove.Fall, HeroWeapon.None, HeroArt.FramesOf(HeroMove.Fall, HeroWeapon.None) - 1, Look.Down));
            var g = new PixelImage(columns.Count * cell + 8, looks * cell + 14);
            g.Fill(0, 0, g.Width, g.Height, Back);
            g.Small(4, 4, "RANDOM LOOKS: WALK x3, SWORD / BOW / STAFF (ATTACK1 WIND, ATTACK1 BLOW, ATTACK3 BLOW), ROLL, DOWN", Text, Back);
            for (int row = 0; row < looks; row++)
            {
                g.Fill(4, 14 + row * cell + cell - 5, g.Width - 8, 3, Ground);
                for (int c = 0; c < columns.Count; c++)
                {
                    var (move, weapon, frame, view) = columns[c];
                    var image = HeroArt.Draw(view, move, frame, HeroGear.Plain(weapon), styles[row]);
                    Over(g, image, 4 + c * cell + cell / 2 - HeroArt.FeetX, 14 + row * cell + cell - 4 - HeroArt.FeetY);
                }
            }
            Png.Save(g.Times(3), path);
        }
    }
}
