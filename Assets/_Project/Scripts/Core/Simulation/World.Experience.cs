using System;
using System.Linq;

namespace Game.Core
{
    // Character levels (Docs/Loot.md, 레벨과 착용 레벨): experience from monsters and bosses, more health and damage
    // each level with a jump at every tenth (the level the next tier of gear is worn at, Docs/Bosses.md 계단식 성장),
    // and gear that asks for a level before it can be worn.
    public static class Leveling
    {
        public const int MaxLevel = 50;
        // Maximum health each level past the first adds.
        public const int HealthPerLevel = 3;
        // Percent more damage (basic attacks, skills and summons alike) each level past the first adds.
        public const int DamagePerLevel = 1;
        // A step: every StepLevels levels (10, 20, 30, 40, 50: where the next tier of gear opens) the character
        // jumps by this much health and percent damage on top, as much as ten ordinary levels.
        public const int StepLevels = 10;
        public const int StepHealth = 30;
        public const int StepDamage = 10;

        // How many steps the level has climbed: 0 below level 10, 5 at the top.
        public static int StepsAt(int level) => Math.Clamp(level, 1, MaxLevel) / StepLevels;

        // What the level alone adds: maximum health, and percent more damage.
        public static int HealthAt(int level) => (Math.Clamp(level, 1, MaxLevel) - 1) * HealthPerLevel + StepsAt(level) * StepHealth;

        public static int DamageAt(int level) => (Math.Clamp(level, 1, MaxLevel) - 1) * DamagePerLevel + StepsAt(level) * StepDamage;

        // One line for the character page: what the level has added so far and where the next step is.
        public static string Summary(int level)
        {
            string so = $"레벨 성장 생명력 +{HealthAt(level)} · 피해 +{DamageAt(level)}%";
            int next = NextStep(level);
            return next > 0 ? $"{so} · 다음 계단 Lv {next}" : so;
        }

        // The level the next step comes at, or 0 past the last.
        public static int NextStep(int level) => StepsAt(level) * StepLevels + StepLevels <= MaxLevel ? StepsAt(level) * StepLevels + StepLevels : 0;
        // Players this far from a monster when it dies share in its experience, each getting all of it.
        public const float ShareRange = 16;
        // Experience it takes from nothing to reach the level: 14 × (level − 1)².
        public static int TotalFor(int level) => 14 * (Math.Clamp(level, 1, MaxLevel) - 1) * (Math.Clamp(level, 1, MaxLevel) - 1);

        public static int LevelFor(int experience)
        {
            int level = 1;
            while (level < MaxLevel && experience >= TotalFor(level + 1)) level++;
            return level;
        }

        // How far through the level the experience is, 0 to 1 (1 at the top level).
        public static float Progress(int experience)
        {
            int level = LevelFor(experience);
            if (level >= MaxLevel) return 1;
            int from = TotalFor(level);
            return (experience - from) / (float)(TotalFor(level + 1) - from);
        }

        // A monster's worth at the level the world drops loot at (World.ProgressLevel), like its drops: once a boss
        // has fallen, every monster gives more.
        public static int ForMonster(int level) => 3 * level + 6;

        public static int ForBoss(BossDefinition boss) => 30 * boss.DropLevel;

        // The level a character from before levels starts at: enough to wear everything they carry.
        public static int StartFor(Equipment equipment, Inventory bag) =>
            equipment.Items.Concat(bag.Items).Select(i => i.Base.RequiredLevel).DefaultIfEmpty(1).Max();
    }

    public sealed partial class World
    {
        // Everyone alive near where the monster fell gets its experience.
        void ExperienceFrom(Enemy enemy)
        {
            int amount = Leveling.ForMonster(Math.Max(enemy.Definition.Level, ProgressLevel) + enemy.ZoneLevels);
            if (FullMoonNight) amount = amount * FullMoonExperiencePercent / 100;
            foreach (var player in _players)
            {
                if (!player.IsDead && Vec2.Distance(player.Position, enemy.Position) <= Leveling.ShareRange) GainExperience(player, amount);
            }
        }

        void ExperienceFrom(Boss boss, System.Collections.Generic.IEnumerable<Player> fighters)
        {
            foreach (var player in fighters) GainExperience(player, Leveling.ForBoss(boss.Definition));
        }

        // Adds experience; each level gained makes a LevelUp event (Amount: the new level) and a little more health.
        public void GainExperience(Player player, int amount)
        {
            if (amount <= 0) return;
            FeedPet(player, amount);
            int before = player.Level;
            player.Experience = Math.Min(player.Experience + amount, Leveling.TotalFor(Leveling.MaxLevel));
            if (player.Level == before) return;
            player.RefreshStats();
            for (int level = before + 1; level <= player.Level; level++)
            {
                _events.Add(new SimEvent(SimEventType.LevelUp, player.Id, amount: level));
            }
        }
    }
}
