using System;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // --only weapons (Docs/Weapons.md 묶음 B): a row a weapon kind, the iron one: its icon, the hero standing with it side
    // on and facing down, and the strike frame of each of the three combo steps (side on, three-quarters, side on), so
    // the held shapes can be looked at before the kinds get motions of their own (out/weapons_held.png).
    static class WeaponPreview
    {
        const int Scale = 3;
        static readonly Rgba Tunic = Rgba.Hex("#3F7FD8");
        static readonly string[] Kinds = { "sword", "dagger", "greatsword", "spear", "hammer", "bow", "crossbow", "throwing", "staff", "wand", "orb" };

        public static void Write(string output, LootData loot)
        {
            const int cell = HeroArt.Size;
            var columns = new (Look Look, HeroMove Move)[]
            {
                (Look.Side, HeroMove.Stand), (Look.Down, HeroMove.Stand), (Look.Side, HeroMove.Attack1), (Look.DownSide, HeroMove.Attack2), (Look.Side, HeroMove.Attack3),
            };
            var sheet = new PixelImage((columns.Length + 1) * cell, Kinds.Length * cell);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++) sheet[x, y] = (x / cell + y / cell) % 2 == 0 ? Rgba.Hex("#3A3240") : Rgba.Hex("#332C3A");
            for (int r = 0; r < Kinds.Length; r++)
            {
                string kind = Kinds[r];
                var item = loot.Bases.FirstOrDefault(b => b.Id == $"iron_{(kind == "throwing" ? "boomerang" : kind)}");
                var gear = item != null ? ItemArt.Held(item) : HeroGear.PlainKind(kind);
                if (item != null)
                {
                    var icon = ItemArt.Gear(item);
                    sheet.Draw(icon, (cell - icon.Width) / 2, r * cell + (cell - icon.Height) / 2);
                }
                for (int c = 0; c < columns.Length; c++)
                {
                    var (look, move) = columns[c];
                    int frame = 0;
                    if (move != HeroMove.Stand)
                    {
                        int frames = HeroArt.FramesOf(move, gear.Kind);
                        frame = Enumerable.Range(0, frames).FirstOrDefault(f => HeroArt.BeatOf(move, gear.Kind, f) == HeroArt.Beat.Strike);
                    }
                    sheet.Draw(HeroArt.Draw(look, move, frame, gear, Tunic), (c + 1) * cell, r * cell);
                }
            }
            var big = new PixelImage(sheet.Width * Scale, sheet.Height * Scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / Scale, y / Scale];
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "weapons_held.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 무기 종류 {Kinds.Length}줄 (아이콘, 서기 옆·아래, 연타 1·2·3단의 타격 장)");
        }
    }
}
