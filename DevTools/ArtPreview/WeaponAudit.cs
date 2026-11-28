using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // --only weaponaudit (2026-11-28, the owner: "the void sword in the test ground has no hit sound and no cut in front;
    // is it only this one? check them all"): every weapon in item_bases.csv, as the game draws and sounds its basic
    // attack's three steps, and whether each piece is there:
    //  (a) the attack's effect: a swing's arc (its feel's size and EffectArt.Swing's frames), or the shot's picture;
    //  (b) the sounds: the swing or shot let go and the blow landing, each a sound with a picture of it and heard (volume);
    //  (c) the weapon in the hero's hand at each step's strike (drawn otherwise than with empty hands);
    //  (d) the kind's own motion: the hero's style is the kind's, its three steps have clips with a strike, its feel set.
    // Writes out/weapon_audit.md (the table, the missing ones first) and fails when anything is missing.
    static class WeaponAudit
    {
        static readonly Rgba Tunic = Rgba.Hex("#3F7FD8");

        public static bool Write(string output, LootData loot)
        {
            var rows = new List<(ItemBase Item, string Kind, string Style, List<string> Missing)>();
            foreach (var item in loot.Bases.Where(b => b.Slot == ItemSlot.Weapon))
                rows.Add((item, item.Kind?.Id ?? "-", ItemArt.Held(item).Style.ToString(), Check(item)));

            var text = new StringBuilder();
            int missing = rows.Count(r => r.Missing.Count > 0);
            text.AppendLine("# 무기 점검 (ArtPreview --only weaponaudit)\n");
            text.AppendLine($"무기 {rows.Count}종, 빠진 것이 있는 무기 {missing}종. 연타 1~3단마다 (a) 휘두르기 호·탄 그림 (b) 휘두를 때·맞힐 때 소리와 음량 (c) 손에 든 무기 그림 (d) 종류의 동작·느낌.\n");
            text.AppendLine("| 무기 | 이름 | 종류 | 동작 | 빠진 것 |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (var row in rows.OrderBy(r => r.Missing.Count == 0).ThenBy(r => r.Kind, StringComparer.Ordinal))
                text.AppendLine($"| {row.Item.Id} | {row.Item.Name} | {row.Kind} | {row.Style} | {(row.Missing.Count == 0 ? "-" : string.Join("; ", row.Missing))} |");
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "weapon_audit.md");
            File.WriteAllText(path, text.ToString());
            Console.WriteLine($"{path}: 무기 {rows.Count}종, 빠진 것이 있는 무기 {missing}종");
            foreach (var row in rows.Where(r => r.Missing.Count > 0))
                Console.WriteLine($"  {row.Item.Id} ({row.Kind}, {row.Style}): {string.Join("; ", row.Missing)}");
            return missing == 0;
        }

        public static List<string> Check(ItemBase item)
        {
            var missing = new List<string>();
            var kind = item.Kind;
            if (kind == null)
            {
                missing.Add("무기 종류 없음");
                return missing;
            }
            var attack = kind.Attack;
            var gear = ItemArt.Held(item);
            var style = gear.Style;
            var feel = WeaponFeel.Of(style);

            // (d) the kind's own motion and feel.
            var expected = item.IsPickaxe ? HeroStyle.Pick : HeroGear.StyleOf(HeroGear.MotionOf(kind.Id), HeroGear.ShapeOf(kind.Id));
            if (style != expected) missing.Add($"(d) 동작 {style}, 종류의 것은 {expected}");
            if (!feel.Felt) missing.Add($"(d) 느낌 비어 있음(호 {feel.Arc}, 음량 {feel.Volume}, 밀림 {feel.Knock}, 멈춤 {feel.Stop})");

            for (int step = 0; step < World.ComboSteps; step++)
            {
                var move = (HeroMove)((int)HeroMove.Attack1 + step);
                bool finisher = step == World.ComboSteps - 1;
                string at = $"{step + 1}단";
                if (HeroArt.SecondsOf(move, style) <= 0 || HeroArt.FramesOf(move, style) == 0) missing.Add($"(d) {at} 동작 클립 없음");
                int strike = HeroArt.StrikeFrame(move, style);
                if (strike < 0) missing.Add($"(d) {at} 타격 장 없음");

                // (c) the weapon in hand at the strike: drawn otherwise than with nothing in hand.
                int frame = Math.Max(0, strike);
                var held = HeroArt.Draw(Look.Side, move, frame, gear, Tunic);
                if (Differs(held, HeroArt.Draw(Look.Side, move, frame, HeroGear.None, Tunic)) < 4)
                    missing.Add($"(c) {at} 손에 든 무기 그림 없음");

                // (a) the effect: a swing's arc, or the shot's picture.
                if (attack.Kind == BasicAttackKind.Swing)
                {
                    if (feel.Arc <= 0 || feel.Thin <= 0) missing.Add($"(a) {at} 휘두르기 호 크기 0");
                    for (int f = 0; f < EffectArt.SwingFrames; f++)
                        if (Opaque(EffectArt.Swing(EffectArt.Diameter(2 + World.MeleeReach * 2), f)) == 0)
                        {
                            missing.Add($"(a) {at} 휘두르기 호 그림 {f}장 비어 있음");
                            break;
                        }
                }
                else if (Opaque(ShotImage(attack)) == 0) missing.Add($"(a) {at} 탄 그림 없음");

                // (b) the sounds: let go and landing, each with a sound made for it and heard.
                var swing = feel.SwingSound(attack.Kind, finisher);
                if (!Audible(swing)) missing.Add($"(b) {at} 휘두르기 소리({swing}) 없음");
                if (feel.Volume <= 0) missing.Add($"(b) {at} 휘두르기 소리 음량 0");
                foreach (bool critical in new[] { false, true })
                {
                    var hit = feel.HitSound(finisher, critical);
                    if (!Audible(hit)) missing.Add($"(b) {at} 맞힘 소리({hit}) 없음");
                }
            }
            return missing.Distinct().ToList();
        }

        // The picture a basic shot flies as (WorldView: the plain arrow or ball, else the kind's own look).
        static PixelImage ShotImage(BasicAttack attack) => attack.Look switch
        {
            ShotLook.Bolt => ItemArt.CrossbowBolt,
            ShotLook.Boomerang => ItemArt.BoomerangShot,
            ShotLook.Orb => ItemArt.OrbShot,
            ShotLook.Spark => ItemArt.Spark,
            _ => attack.Kind == BasicAttackKind.Arrow ? ItemArt.Arrow : ItemArt.Orb,
        };

        // A sound with something to hear: the one made in code (the recording, where there is one, stands in for it).
        static bool Audible(Sound sound)
        {
            var samples = SoundArt.Make(sound);
            return samples != null && samples.Any(s => Math.Abs(s) > 0.01f);
        }

        static int Opaque(PixelImage image)
        {
            if (image == null) return 0;
            int count = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image[x, y].A > 0) count++;
            return count;
        }

        static int Differs(PixelImage a, PixelImage b)
        {
            int count = 0;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (!a[x, y].Equals(b[x, y])) count++;
            return count;
        }
    }
}
