using System;
using System.Linq;

namespace Game.Core
{
    // Character levels (Docs/Loot.md, 레벨과 착용 레벨): experience from monsters and bosses, a little more health
    // each level, and gear that asks for a level before it can be worn.
    public static class Leveling
    {
        public const int MaxLevel = 50;
        // Maximum health each level past the first adds.
        public const int HealthPerLevel = 2;
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
            int amount = Leveling.ForMonster(Math.Max(enemy.Definition.Level, ProgressLevel));
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
