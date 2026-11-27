using System;

namespace Game.Core
{
    // What gear does besides its stats (Docs/Gear.md, 특이 기능; the effects are GearEffects, summed in PlayerStats):
    // a ward when rolling, heals and mana for kills, blows given back, more damage at low health, on a full or burning
    // or chilled target and while warded, and burns from critical hits. Only the host's simulation runs them.
    public sealed partial class World
    {
        void RaiseWard(Player player)
        {
            var effects = player.Stats.Effects;
            if (effects.WardShare <= 0 || player.WardCooldown > 0) return;
            player.Ward = player.Stats.MaxHealth * effects.WardShare / 100f;
            player.WardTicks = Ticks(Math.Max(0.1f, effects.WardSeconds));
            player.WardCooldown = Ticks(effects.WardCooldown);
        }

        static void UpdateWard(Player player)
        {
            if (player.WardCooldown > 0) player.WardCooldown--;
            if (player.WardTicks > 0 && --player.WardTicks == 0) player.Ward = 0;
        }

        // A blow on the player as gear changes it: less at low health, then the ward takes what it can. Gives part
        // of it back to the monster or boss that struck (thorns).
        int GearTaken(Player player, int source, int amount)
        {
            var effects = player.Stats.Effects;
            if (!effects.Any || amount <= 0) return amount;
            float taken = amount * effects.TakenFactor(player.Health, player.Stats.MaxHealth);
            if (effects.Thorns > 0 && source != 0) GiveBack(player, source, taken * effects.Thorns / 100f);
            if (player.Ward > 0)
            {
                float held = Math.Min(player.Ward, taken);
                player.Ward -= held;
                taken -= held;
                if (player.Ward <= 0)
                {
                    player.Ward = 0;
                    player.WardTicks = 0;
                }
            }
            return Math.Max(0, (int)MathF.Round(taken));
        }

        void GiveBack(Player player, int source, float damage)
        {
            if (damage < 1) return;
            var enemy = FindEnemy(source);
            if (enemy != null && !enemy.IsDead)
            {
                HitEnemy(player, enemy, damage, false, player.Position, SkillTag.None, knockback: false, thorns: true);
                return;
            }
            var boss = FindBoss(source);
            if (boss != null && !boss.IsDead) HitBoss(player, boss, damage, false, SkillTag.None, thorns: true);
        }

        // How much more a blow of the player's deals on this target for their gear.
        static float GearDealt(Player player, float health, float maxHealth, Afflictions afflictions)
        {
            var effects = player.Stats.Effects;
            if (!effects.Any) return 1;
            return effects.DealtFactor(player.Health, player.Stats.MaxHealth, player.Ward > 0, health >= maxHealth, afflictions.Burning, afflictions.Chilled);
        }

        // After a blow of the player's landed: a critical hit's burn.
        void GearAfterHit(Player player, Afflictions afflictions, int dealt, bool critical)
        {
            var effects = player.Stats.Effects;
            if (critical && effects.CritBurn > 0 && dealt > 0) afflictions.Burn(dealt * effects.CritBurn / 100f, Ticks(BurnSeconds), player.Id);
        }

        // The player felled something: heals and mana for the kill.
        static void GearKill(Player player)
        {
            var effects = player.Stats.Effects;
            if (player.IsDead || !effects.Any) return;
            if (effects.KillHeal > 0) player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.MaxHealth * effects.KillHeal / 100f);
            if (effects.KillMana > 0) player.Mana = Math.Min(player.Stats.MaxMana, player.Mana + effects.KillMana);
        }
    }
}
