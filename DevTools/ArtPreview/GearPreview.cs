using System;
using System.IO;
using System.Linq;

using Game.Core;

namespace Game.Client
{
    // --only gear: the icons of the hunting sets, elite uniques and boss sets, one row a set (or band of uniques), and the
    // hunting trophies (out/gear.png, Docs/Gear.md).
    static class GearPreview
    {
        const int Cell = 20;
        const int Scale = 4;

        public static void Write(string output, LootData loot)
        {
            var rows = loot.Sets.Where(s => s.Id != "pilgrim").Select(s => loot.Bases.Where(b => b.Set == s).ToList()).ToList();
            for (int band = 1; band <= 5; band++)
                rows.Add(loot.Bases.Where(b => b.IsEliteUnique && DamageBands.BandOfLevel(b.Level) == band).ToList());
            var trophies = loot.Materials.Where(m => m.Drops.Count > 0).ToList();
            int columns = Math.Max(rows.Max(r => r.Count), trophies.Count);
            var sheet = new PixelImage(columns * Cell + 4, (rows.Count + 1) * Cell + 4);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++) sheet[x, y] = Rgba.Hex("#2A2430");
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Count; c++) Put(sheet, ItemArt.Gear(rows[r][c]), c, r);
            for (int c = 0; c < trophies.Count; c++) Put(sheet, ItemArt.Material(trophies[c]), c, rows.Count);
            var big = new PixelImage(sheet.Width * Scale, sheet.Height * Scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / Scale, y / Scale];
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "gear.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 세트·고유 {rows.Count}줄, 사냥 재료 {trophies.Count}개");
            WriteWeapons(output, loot);
        }

        // out/weapons.png (Docs/Weapons.md): a row a band of the eleven ore weapons, sword to orb, and a last row of the
        // set, elite and boss weapons by kind.
        static void WriteWeapons(string output, LootData loot)
        {
            string[] kinds = TestKit.Weapons;
            var rows = DamageBands.TierNames.Select(t => kinds.Select(k => loot.Bases.FirstOrDefault(b => b.Id == $"{t}_{k}")).Where(b => b != null).ToList()).ToList();
            rows.Add(loot.Bases.Where(b => b.Slot == ItemSlot.Weapon && b.Weight == 0 && !b.IsPickaxe && b.Kind.Id is not ("sword" or "bow" or "staff")).ToList());
            int columns = rows.Max(r => r.Count);
            var sheet = new PixelImage(columns * Cell + 4, rows.Count * Cell + 4);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++) sheet[x, y] = Rgba.Hex("#2A2430");
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Count; c++) Put(sheet, ItemArt.Gear(rows[r][c]), c, r);
            var big = new PixelImage(sheet.Width * Scale, sheet.Height * Scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++) big[x, y] = sheet[x / Scale, y / Scale];
            string path = Path.Combine(output, "weapons.png");
            Png.Save(big, path);
            Console.WriteLine($"{path}: 무기 종류 {columns}칸 × {rows.Count}줄");
        }

        static void Put(PixelImage sheet, PixelImage icon, int column, int row) =>
            sheet.Draw(icon, 2 + column * Cell + (Cell - icon.Width) / 2, 2 + row * Cell + (Cell - icon.Height) / 2);
    }
}
