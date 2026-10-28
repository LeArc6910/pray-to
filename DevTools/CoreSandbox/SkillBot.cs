using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // How a bot uses its class skills and potions: each skill as soon as it is ready and worth using, a potion when
    // below half health. Starts with the potions a host hands out. Also credits damage to the basic attack or the
    // skill that dealt it, from each hit's Cause (skill slot + 1, or 0).
    sealed class SkillBot
    {
        readonly Dictionary<string, int> _dealt = new Dictionary<string, int>();
        readonly Dictionary<string, int> _uses = new Dictionary<string, int>();

        public SkillBot(Player player, LootData loot)
        {
            Player = player;
            foreach (string id in new HostConfig().StarterConsumables)
            {
                var consumable = loot.Consumables.FirstOrDefault(c => c.Id == id);
                if (consumable != null) player.Inventory.AddConsumable(consumable);
            }
        }

        public Player Player { get; }
        public int Drunk { get; private set; }

        // target is what the bot is fighting; fighting says whether it is close enough to bother.
        public void Use(World world, Vec2 target, bool fighting)
        {
            var player = Player;
            if (player.IsDead) return;
            if (player.Health < player.Stats.MaxHealth / 2)
            {
                var potion = player.Inventory.Consumables.Keys.FirstOrDefault();
                if (potion != null) world.UseConsumable(player.Id, potion);
            }
            var toTarget = target - player.Position;
            for (int slot = 0; slot < player.Skills.Count; slot++)
            {
                if (player.CooldownTicks(slot) > 0) continue;
                var skill = player.Skills[slot];
                bool worth = skill.Kind switch
                {
                    SkillKind.Nova => world.Enemies.Any(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) <= skill.Radius + e.Definition.Radius)
                        || world.Bosses.Any(b => !b.IsDead && b.State != BossState.Dormant && Vec2.Distance(b.Position, player.Position) <= skill.Radius + b.Definition.Radius),
                    SkillKind.Projectile => fighting && toTarget.Length <= skill.Range,
                    _ => fighting && toTarget.Length <= 4,
                };
                if (worth) world.UseSkill(player.Id, slot, toTarget);
            }
        }

        public void Count(SimEvent e)
        {
            if (e.Source != Player.Id) return;
            switch (e.Type)
            {
                case SimEventType.SkillUsed:
                    string used = Player.Skills[e.Amount].Name;
                    _uses[used] = _uses.GetValueOrDefault(used) + 1;
                    break;
                case SimEventType.Healed:
                    Drunk++;
                    break;
                case SimEventType.Hit:
                    string by = e.Cause == 0 ? "기본 공격" : Player.Skills[e.Cause - 1].Name;
                    _dealt[by] = _dealt.GetValueOrDefault(by) + e.Amount;
                    break;
            }
        }

        // "기본 공격 1200 (60%), 회전베기 500 (25%, 30회), 전투 함성 (8회)"
        public string Summary()
        {
            int total = Math.Max(1, _dealt.Values.Sum());
            var damage = _dealt.OrderByDescending(p => p.Value).Select(p =>
                $"{p.Key} {p.Value} ({p.Value * 100.0 / total:0}%{(_uses.TryGetValue(p.Key, out int uses) ? $", {uses}회" : "")})");
            var buffs = _uses.Where(p => !_dealt.ContainsKey(p.Key)).Select(p => $"{p.Key} ({p.Value}회)");
            return string.Join(", ", damage.Concat(buffs));
        }
    }
}
