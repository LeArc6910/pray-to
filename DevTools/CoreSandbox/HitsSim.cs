using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // --hits [band|all]: what one hit of every monster, elite, boss attack and rift monster takes off a character of its
    // band (Docs/Difficulty.md): with no gear, with the band below's full set and with the band's own, against the
    // targets. The table the tests hold (DifficultyTests) and the numbers are tuned by: bots dodge worse than careful
    // players, so the difficulty is set by what a hit takes, not by whether a bot wins.
    static class HitsSim
    {
        public static void Run(LootData loot, MonsterData monsters, BossData bosses, string which)
        {
            var rows = HitTable.Build(loot, monsters, bosses);
            int only = Band(which);
            Console.WriteLine("한 방이 최대 생명력에서 가져가는 몫(Docs/Difficulty.md). 맨몸 / 앞 단계 장비 / 제 단계 장비(보통 등급 여섯 부위, 가운데 값),");
            Console.WriteLine("몬스터는 단계의 기준 레벨, 보스는 보스 레벨 + 1. 클래스 없음(전사는 생명력 +20%)");
            Console.WriteLine("방어: 받는 몫 = 바닥 + (1 − 바닥) / (1 + k × 비율^p), 비율 = 방어도 ÷ 단계 방어도 (Docs/ArmorCurve.md)");
            foreach (var (name, curve) in new[] { ("일반", DamageBands.NormalCurve), ("정예·L", DamageBands.EliteCurve), ("XL", DamageBands.HugeCurve),
                ("보스", DamageBands.BossCurve), ("필살기", DamageBands.UltimateCurve) })
                Console.WriteLine($"  {Pad(name, 8)}바닥 {curve.Floor * 100:0}% · p {curve.Power} · k {curve.K:0.00} · 비율 0.5 {curve.Share(0.5f) * 100:0}% · 1 {curve.Share(1) * 100:0}% · 2 {curve.Share(2) * 100:0}%");
            Console.WriteLine($"위 단계: 한 구간 위의 보통 세트. 체력 투자: 제 단계 세트에 생명력 +{HitTable.TankHealth * 100:0}%(구리 125 → 200)");
            Console.WriteLine($"✓ 목표 안(점검 줄: 제 단계가 범위 안, 맨몸이 바닥 넘게, 앞 단계가 제 단계의 {DamageBands.GateLow}~{DamageBands.GateHigh}배) · 큰 공격 줄은 목표의 {DamageBands.BigBlowCap}배 아래면 ✓");
            for (int band = 1; band <= DamageBands.Bands; band++)
            {
                if (only != 0 && band != only) continue;
                var mine = rows.Where(r => r.Band == band).ToList();
                if (mine.Count == 0) continue;
                var loadout = PlayerStats.From(HitTable.Wearing(loot, band), level: DamageBands.LevelOf(band));
                Console.WriteLine();
                Console.WriteLine($"=== {band}단계 {DamageBands.BandNames[band - 1]}: 기준 Lv {DamageBands.LevelOf(band)}, 단계 방어도 {DamageBands.ArmorOf(band)}, " +
                    $"제 단계 장비 생명력 {loadout.MaxHealth:0} · 방어도 {loadout.Armor:0} ===");
                foreach (var group in mine.GroupBy(r => r.Group))
                {
                    Console.WriteLine($"-- {group.Key}");
                    Console.WriteLine($"  {Pad("누구", 26)}{Pad("공격", 22)}{Pad("구분", 10)}{"원래",6}{"Lv",4}{"맨몸",7}{"앞 단계",8}{"제 단계",8}{"위 단계",8}{"체력",8}  {Pad("목표", 14)}{"배",5}");
                    foreach (var row in group)
                    {
                        var target = row.Target;
                        string aim = row.Checked ? $"{target.Low * 100:0}~{target.High * 100:0}%" : $"≤{target.Aim * DamageBands.BigBlowCap * 100:0}%";
                        Console.WriteLine($"  {Pad(row.Who, 26)}{Pad(row.What, 22)}{Pad(KindName(row.Kind), 10)}{row.Raw,6:0}{row.Level,4}" +
                            $"{Percent(row.Bare),7}{Percent(row.Below),8}{Percent(row.Geared),8}{Percent(row.Above),8}{Percent(row.Tank),8}  {Pad(aim, 14)}{(float.IsNaN(row.Gate) ? "" : $"{row.Gate:0.0}"),5} {(row.Meets ? "✓" : "✗")}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("요약: 점검 줄의 구분마다 제 단계 / 앞 단계 / 맨몸 / 위 단계 / 체력 투자(최소~최대)");
            foreach (var band in rows.Where(r => only == 0 || r.Band == only).GroupBy(r => r.Band))
            {
                foreach (var kind in band.Where(r => r.Checked).GroupBy(r => r.Kind).OrderBy(g => g.Key))
                {
                    string Range(Func<HitRow, float> of) =>
                        kind.Any(r => !float.IsNaN(of(r))) ? $"{kind.Where(r => !float.IsNaN(of(r))).Min(of) * 100:0}~{kind.Where(r => !float.IsNaN(of(r))).Max(of) * 100:0}%" : "-";
                    Console.WriteLine($"  {band.Key}단계 {Pad(DamageBands.BandNames[band.Key - 1], 8)}{Pad(KindName(kind.Key), 10)}{kind.Count(),4}줄  " +
                        $"{Pad(Range(r => r.Geared), 10)}{Pad(Range(r => r.Below), 11)}{Pad(Range(r => r.Bare), 11)}{Pad(Range(r => r.Above), 11)}{Pad(Range(r => r.Tank), 11)}{(kind.All(r => r.Meets) ? "✓" : $"✗ {kind.Count(r => !r.Meets)}")}");
                }
            }
            var missed = rows.Where(r => (only == 0 || r.Band == only) && !r.Meets).ToList();
            Console.WriteLine(missed.Count == 0 ? "모든 줄이 목표 안" : $"목표 밖 {missed.Count}줄");
        }

        static int Band(string which)
        {
            if (string.IsNullOrEmpty(which) || which == "all") return 0;
            if (int.TryParse(which, out int number)) return DamageBands.Clamp(number);
            int index = Array.IndexOf(DamageBands.TierNames, which);
            if (index < 0) index = Array.IndexOf(DamageBands.BandNames, which);
            return index < 0 ? 0 : index + 1;
        }

        static string Percent(float share) => float.IsNaN(share) ? "-" : $"{share * 100:0.0}%";

        public static string KindName(HitKind kind) => kind switch
        {
            HitKind.Normal => "일반",
            HitKind.Elite => "정예",
            HitKind.Large => "L",
            HitKind.Huge => "XL",
            HitKind.Quick => "빠른",
            HitKind.Slow => "느린",
            HitKind.Heavy => "무거운",
            HitKind.Ultimate => "필살기",
            HitKind.Barrage => "탄막 한 발",
            HitKind.Combo => "연속 돌진",
            HitKind.FollowUp => "이어치기",
            HitKind.Share => "나눠 맞기",
            HitKind.Roar => "포효",
            _ => "",
        };

        // Korean characters take two columns in a terminal.
        static string Pad(string text, int width)
        {
            int used = text.Sum(c => c >= 0x1100 ? 2 : 1);
            return text + new string(' ', Math.Max(1, width - used));
        }
    }
}
