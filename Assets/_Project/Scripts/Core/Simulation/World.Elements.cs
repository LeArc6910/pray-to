using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What an element left on someone: a burn still to deal its damage, a chill still slowing them. See
    // Docs/Combat.md (원소).
    public sealed class Afflictions
    {
        public bool Burning => BurnTicks > 0;
        public bool Chilled => ChillTicks > 0;

        internal float BurnPerTick { get; set; }
        internal int BurnTicks { get; set; }
        // Whoever lit the burn, who gets its damage and its kill.
        internal int BurnSource { get; set; }
        // Burn damage dealt but not yet reported.
        internal float BurnOwed { get; set; }
        internal int ChillTicks { get; set; }

        // As the network carries it (EntityState.Status).
        public byte Bits => (byte)((Burning ? StatusBits.Burning : 0) | (Chilled ? StatusBits.Chilled : 0));

        internal void Clear()
        {
            BurnTicks = 0;
            BurnOwed = 0;
            ChillTicks = 0;
        }

        // A new burn replaces the old one when it has more damage still to deal.
        internal void Burn(float total, int ticks, int source)
        {
            if (total <= BurnPerTick * BurnTicks) return;
            BurnPerTick = total / ticks;
            BurnTicks = ticks;
            BurnSource = source;
        }

        // One tick of burn: the damage it deals now, reported every ReportTicks (World.TickBurn).
        internal float TickBurn()
        {
            if (ChillTicks > 0) ChillTicks--;
            if (BurnTicks <= 0) return 0;
            BurnTicks--;
            BurnOwed += BurnPerTick;
            return BurnPerTick;
        }
    }

    public static class StatusBits
    {
        public const byte Burning = 1;
        public const byte Chilled = 2;
        // Only in the player's own state: on the way home, and lying in bed (World.Sleep).
        public const byte Recalling = 4;
        public const byte Sleeping = 8;
    }

    // Fire burns for a share of the hit over a few seconds, cold slows, lightning jumps to enemies nearby. A monster
    // resists or is weak to each (monsters.csv resist); one that resists an element by Immune or more takes none of
    // its effect. Bosses burn and pass lightning on but are never slowed. Monsters whose attacks carry an element
    // (monsters.csv element) burn or chill the players they hit.
    public sealed partial class World
    {
        public const float BurnShare = 0.3f;
        public const float BurnSeconds = 3;
        public const float ChillSeconds = 2;
        public const float PlayerChillSeconds = 1.5f;
        // A chilled monster only acts on this share of ticks; a chilled player moves this much slower.
        const int ChillSkip = 2;
        const int ChillCycle = 5;
        public const float PlayerChillSpeed = 0.7f;
        public const float ChainShare = 0.5f;
        public const float ChainRange = 3.5f;
        public const int ChainTargets = 2;
        public const int Immune = 75;
        // Burn damage is reported as one hit this often.
        const int ReportTicks = World.TicksPerSecond / 2;

        public static SkillTag Elements(SkillTag tags) => tags & (SkillTag.Fire | SkillTag.Cold | SkillTag.Lightning);

        // How much of a hit with these tags the monster takes: its weakest resistance among the hit's elements.
        public static float ElementFactor(MonsterDefinition monster, SkillTag tags)
        {
            var elements = Elements(tags);
            if (elements == SkillTag.None) return 1;
            float factor = 0;
            foreach (var element in new[] { SkillTag.Fire, SkillTag.Cold, SkillTag.Lightning })
            {
                if ((elements & element) != 0) factor = Math.Max(factor, 1 - monster.Resist(element) / 100f);
            }
            return factor;
        }

        // A weapon's added elemental damage (PlayerStats.FireAdded and the others, 2026-11-09): after a basic attack's
        // own hit, each element it adds strikes the same target again as that element alone, so the target's
        // resistance, burning, slowing and chaining follow that element's rules. It does not knock back. Its hit carries
        // ElementalCause + the element's place in Elements, so the screen shows its number in the element's colour.
        public const int ElementalCause = 100;
        public static readonly SkillTag[] ElementOrder = { SkillTag.Fire, SkillTag.Cold, SkillTag.Lightning };

        public static SkillTag ElementOfCause(int cause) =>
            cause >= ElementalCause && cause < ElementalCause + ElementOrder.Length ? ElementOrder[cause - ElementalCause] : SkillTag.None;

        void AddElements(Player player, Enemy enemy, Boss boss, bool critical, float more = 1)
        {
            var stats = player.Stats;
            for (int i = 0; i < ElementOrder.Length; i++)
            {
                float added = stats.AddedFor(ElementOrder[i]) * more * (critical ? stats.CritDamage / 100f : 1f);
                if (added <= 0) continue;
                if (enemy != null && !enemy.IsDead)
                    HitEnemy(player, enemy, added, critical, enemy.Position, ElementOrder[i], ElementalCause + i, knockback: false);
                else if (boss != null && !boss.IsDead && !boss.Invulnerable)
                    HitBoss(player, boss, added, critical, ElementOrder[i], ElementalCause + i);
            }
        }

        // What the hit leaves behind on a monster, and lightning's jump to others. dealt is the damage it took.
        void Afflict(Player player, Enemy enemy, SkillTag tags, int dealt, float damage, bool critical, int cause, bool chained)
        {
            if (enemy.IsDead) return;
            var definition = enemy.Definition;
            if ((tags & SkillTag.Fire) != 0 && definition.Resist(SkillTag.Fire) < Immune)
                enemy.Afflictions.Burn(dealt * BurnShare, Ticks(BurnSeconds), player.Id);
            if ((tags & SkillTag.Cold) != 0 && definition.Resist(SkillTag.Cold) < Immune)
                enemy.Afflictions.ChillTicks = Math.Max(enemy.Afflictions.ChillTicks, Ticks(ChillSeconds));
            if ((tags & SkillTag.Lightning) != 0 && !chained) Chain(player, enemy.Id, enemy.Position, damage, critical, tags, cause);
        }

        void AfflictBoss(Player player, Boss boss, SkillTag tags, int dealt, float damage, bool critical, int cause)
        {
            if (boss.IsDead) return;
            if ((tags & SkillTag.Fire) != 0) boss.Afflictions.Burn(dealt * BurnShare, Ticks(BurnSeconds), player.Id);
            if ((tags & SkillTag.Lightning) != 0) Chain(player, boss.Id, boss.Position, damage, critical, tags, cause);
        }

        // Lightning jumps from what it hit to the nearest few other monsters in reach, for a share of the damage.
        // Those hits do not jump again.
        void Chain(Player player, int fromId, Vec2 from, float damage, bool critical, SkillTag tags, int cause)
        {
            var targets = _enemies
                .Where(e => !e.IsDead && e.Id != fromId && Vec2.Distance(e.Position, from) <= ChainRange + e.Definition.Radius)
                .OrderBy(e => Vec2.Distance(e.Position, from))
                .Take(ChainTargets)
                .ToList();
            foreach (var target in targets)
            {
                _events.Add(new SimEvent(SimEventType.Chained, fromId, target.Id));
                HitEnemy(player, target, damage * ChainShare, critical, from, tags, cause, knockback: false, chained: true);
            }
        }

        // Burns deal their damage and chills wear off, every tick. A burn's damage counts as its lighter's, kill and
        // all; it is reported as one hit every half second.
        void UpdateAfflictions()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                var afflictions = enemy.Afflictions;
                enemy.Health -= afflictions.TickBurn();
                if (!ReportBurn(afflictions, enemy.Id, enemy.IsDead)) continue;
                _events.Add(new SimEvent(SimEventType.Died, enemy.Id));
                DropFrom(enemy, FindPlayer(afflictions.BurnSource));
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead) continue;
                var afflictions = boss.Afflictions;
                afflictions.ChillTicks = 0;
                afflictions.TickBurn();
                int amount = TakeOwed(afflictions, false);
                if (amount > 0 && !boss.Invulnerable) DamageBoss(boss, afflictions.BurnSource, amount, false);
            }
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                var afflictions = player.Afflictions;
                afflictions.TickBurn();
                int amount = TakeOwed(afflictions, false);
                if (amount > 0) HurtPlayer(player, afflictions.BurnSource, amount, armor: false);
            }
        }

        // Reports the burn's damage when it is due (or the burn is over, or killed); returns whether it killed.
        bool ReportBurn(Afflictions afflictions, int targetId, bool dead)
        {
            int amount = TakeOwed(afflictions, dead);
            if (amount > 0) _events.Add(new SimEvent(SimEventType.Hit, afflictions.BurnSource, targetId, amount));
            return dead;
        }

        // The whole points of burn damage due now: every ReportTicks while it burns, keeping the fraction for the
        // next, and what is left, rounded, once it is over (or the target fell).
        int TakeOwed(Afflictions afflictions, bool now)
        {
            if (afflictions.BurnOwed <= 0) return 0;
            bool over = now || afflictions.BurnTicks <= 0;
            if (!over && Tick % ReportTicks != 0) return 0;
            int amount = over ? (int)MathF.Round(afflictions.BurnOwed) : (int)afflictions.BurnOwed;
            afflictions.BurnOwed = over ? 0 : afflictions.BurnOwed - amount;
            return amount;
        }

        // A chilled monster skips some ticks altogether, moving and winding up more slowly.
        bool SkipsTick(Enemy enemy) => enemy.Afflictions.Chilled && Tick % ChillCycle < ChillSkip;

        // A monster or boss whose attacks carry an element burns or chills the player it hits.
        void AfflictPlayer(Player player, int sourceId, int dealt)
        {
            var element = ElementOf(sourceId);
            if (element == SkillTag.None || player.IsDead || dealt <= 0) return;
            if (element == SkillTag.Fire) player.Afflictions.Burn(dealt * BurnShare, Ticks(BurnSeconds), sourceId);
            if (element == SkillTag.Cold) player.Afflictions.ChillTicks = Math.Max(player.Afflictions.ChillTicks, Ticks(PlayerChillSeconds));
        }

        SkillTag ElementOf(int sourceId) =>
            FindEnemy(sourceId)?.Definition.Element ?? _bosses.FirstOrDefault(b => b.Id == sourceId)?.Definition.Element ?? SkillTag.None;

        // Standing in lava keeps a burn going: about LavaBurn a second while in it, and a little after.
        const float LavaBurn = 8;

        void BurnInLava(Player player)
        {
            if (player.IsDead || Map[TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y)] != TileType.Lava) return;
            player.Afflictions.Burn(LavaBurn, TicksPerSecond, 0);
        }
    }
}
