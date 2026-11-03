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
            (100 + player.Stats.MiningSpeed) / 100f * (100 + LifeSkills.Bonus(LifeLevel(player, LifeSkill.Mining), LifeSkills.DigPercent)) / 100f;
    }
}
