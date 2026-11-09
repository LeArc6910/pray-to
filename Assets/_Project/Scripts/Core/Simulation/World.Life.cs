using System;
using System.Linq;

namespace Game.Core
{
    // The skills of everyday life (Docs/LifeSkills.md): each grows with what is done by hand, never by machines, and
    // makes that work go a little better.
    public enum LifeSkill
    {
        Farming,
        Fishing,
        Mining,
        Cooking,
    }

    public static class LifeSkills
    {
        public const int Count = 4;
        public const int MaxLevel = 10;
        // Per level above the first: the chance of one more crop from a hand harvest, how much sooner a fish bites and
        // how much wider the catch zone of the reel gauge is, and how much faster walls give way (percent).
        public const int ExtraCropPercent = 5;
        public const int BitePercent = 4;
        public const int ZonePercent = 5;
        public const int DigPercent = 4;
        // And the chance of one more of a dish cooked by hand.
        public const int ExtraDishPercent = 5;

        // Experience to have reached the level: 0, 100, 300, 600 … 4500 for level 10. A few hours of one kind of work
        // reach the top (DevTools/CoreSandbox --economy).
        public static int TotalFor(int level) => 50 * (Math.Clamp(level, 1, MaxLevel) - 1) * Math.Clamp(level, 1, MaxLevel);

        public static int LevelOf(int experience)
        {
            int level = 1;
            while (level < MaxLevel && experience >= TotalFor(level + 1)) level++;
            return level;
        }

        public static string Name(LifeSkill skill) => skill switch
        {
            LifeSkill.Farming => "농사",
            LifeSkill.Fishing => "낚시",
            LifeSkill.Cooking => "요리",
            _ => "채굴",
        };

        // ---- Special perks (2026-11-09): at PerkLevels[tier] a player picks one of two for each skill, for good ----

        public static readonly int[] PerkLevels = { 5, 10 };
        public const int PerkTiers = 2;

        // Where a skill's pick at a tier is kept in Player.LifePerks: 0 none, 1 the first, 2 the second.
        public static int PerkIndex(LifeSkill skill, int tier) => (int)skill * PerkTiers + tier;

        public static (string Name, string Text) PerkOf(LifeSkill skill, int tier, int choice) => (skill, tier, choice) switch
        {
            (LifeSkill.Farming, 0, 1) => ("풍작", "직접 거둘 때 하나 더 나올 확률 +15%"),
            (LifeSkill.Farming, 0, 2) => ("좋은 값", "상인이 작물을 20% 비싸게 산다"),
            (LifeSkill.Farming, 1, 1) => ("씨앗 받기", "거둘 때 25% 확률로 씨앗 하나"),
            (LifeSkill.Farming, 1, 2) => ("큰 수확", "직접 거둘 때 하나 더 나올 확률 +25%"),
            (LifeSkill.Fishing, 0, 1) => ("느긋한 손", "입질 뒤 낚아챌 시간 +1초"),
            (LifeSkill.Fishing, 0, 2) => ("보물 사냥꾼", "보물이 두 배로 잘 걸린다"),
            (LifeSkill.Fishing, 1, 1) => ("대어", "두 마리 낚일 확률 +20%"),
            (LifeSkill.Fishing, 1, 2) => ("미끼 장인", "미끼가 절반은 남는다"),
            (LifeSkill.Mining, 0, 1) => ("광부", "광맥에서 25% 확률로 광석 하나 더"),
            (LifeSkill.Mining, 0, 2) => ("날쌘 곡괭이", "캐는 속도 +15%"),
            (LifeSkill.Mining, 1, 1) => ("보석 눈", "광맥을 캘 때 3% 확률로 룬"),
            (LifeSkill.Mining, 1, 2) => ("광맥 부자", "광맥에서 50% 확률로 광석 하나 더"),
            (LifeSkill.Cooking, 0, 1) => ("넉넉한 솜씨", "요리할 때 하나 더 나올 확률 +15%"),
            (LifeSkill.Cooking, 0, 2) => ("요리 상인", "상인이 요리를 25% 비싸게 산다"),
            (LifeSkill.Cooking, 1, 1) => ("잔치", "요리할 때 하나 더 나올 확률 +25%"),
            _ => ("별미", "상인이 요리를 50% 비싸게 산다"),
        };

        // What a level is worth as a bonus: nothing at level 1.
        public static int Bonus(int level, int percent) => (Math.Clamp(level, 1, MaxLevel) - 1) * percent;

        // What the skill's level gives, for the view.
        public static string Perk(LifeSkill skill, int level) => skill switch
        {
            LifeSkill.Farming => $"직접 거둘 때 {Bonus(level, ExtraCropPercent)}% 확률로 하나 더",
            LifeSkill.Fishing => $"입질 {Bonus(level, BitePercent)}% 빨리 · 잡는 칸 {Bonus(level, ZonePercent)}% 넓게",
            LifeSkill.Cooking => $"요리할 때 {Bonus(level, ExtraDishPercent)}% 확률로 하나 더",
            _ => $"캐는 속도 +{Bonus(level, DigPercent)}%",
        };
    }

    public sealed partial class World
    {
        public static int LifeLevel(Player player, LifeSkill skill) => LifeSkills.LevelOf(player.LifeExperience[(int)skill]);

        // Whether the player picked that perk of the skill at that tier (LifeSkills.PerkOf).
        public static bool HasPerk(Player player, LifeSkill skill, int tier, int choice) => player.LifePerks[LifeSkills.PerkIndex(skill, tier)] == choice;

        // Picks a perk of a skill at a tier the player's level has reached and has not picked yet. False otherwise.
        public bool ChoosePerk(int playerId, LifeSkill skill, int tier, int choice)
        {
            var player = FindPlayer(playerId);
            if (player == null || tier < 0 || tier >= LifeSkills.PerkTiers || choice < 1 || choice > 2) return false;
            int index = LifeSkills.PerkIndex(skill, tier);
            if (player.LifePerks[index] != 0 || LifeLevel(player, skill) < LifeSkills.PerkLevels[tier]) return false;
            player.LifePerks[index] = choice;
            player.LifeVersion++;
            return true;
        }

        // Puts back the perks a character save remembered.
        public static void RestoreLifePerks(Player player, System.Collections.Generic.IReadOnlyList<int> perks)
        {
            for (int i = 0; i < player.LifePerks.Length && i < perks.Count; i++) player.LifePerks[i] = Math.Clamp(perks[i], 0, 2);
            player.LifeVersion++;
        }

        // Adds experience to a life skill; a LifeLevel event when it reaches a new level (Amount: skill × 16 + level).
        internal void GainLifeExperience(Player player, LifeSkill skill, int amount)
        {
            if (amount <= 0) return;
            int before = LifeLevel(player, skill);
            player.LifeExperience[(int)skill] = Math.Min(LifeSkills.TotalFor(LifeSkills.MaxLevel), player.LifeExperience[(int)skill] + amount);
            player.LifeVersion++;
            int after = LifeLevel(player, skill);
            if (after > before) _events.Add(new SimEvent(SimEventType.LifeLevel, player.Id, amount: (int)skill * 16 + after, position: player.Position));
        }

        // Puts back what a character save remembered.
        public static void RestoreLifeExperience(Player player, System.Collections.Generic.IReadOnlyList<int> experience)
        {
            for (int i = 0; i < LifeSkills.Count && i < experience.Count; i++)
            {
                player.LifeExperience[i] = Math.Clamp(experience[i], 0, LifeSkills.TotalFor(LifeSkills.MaxLevel));
            }
            player.LifeVersion++;
        }

        // How much a wall is dug by one swing: 1, more with mining speed and the mining skill.
        internal static float DigPowerOf(Player player) =>
            (100 + player.Stats.MiningSpeed) / 100f * (100 + LifeSkills.Bonus(LifeLevel(player, LifeSkill.Mining), LifeSkills.DigPercent)
                + (HasPerk(player, LifeSkill.Mining, 0, 2) ? 15 : 0)) / 100f;
    }
}
