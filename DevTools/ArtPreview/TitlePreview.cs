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
            return ok;
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
