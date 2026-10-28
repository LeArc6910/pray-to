using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // How a bot uses its class skills: each one as soon as it is ready and worth using. Also credits damage to the
    // basic attack or the skill that dealt it, from the order of events (an Attack, SkillUsed or Burst comes before
    // its hits).
    sealed class SkillBot
    {
        readonly Dictionary<string, int> _dealt = new Dictionary<string, int>();
        readonly Dictionary<string, int> _uses = new Dictionary<string, int>();
        string _current = "기본 공격";

        public SkillBot(Player player) { Player = player; }

        public Player Player { get; }

        // target is what the bot is fighting; fighting says whether it is close enough to bother.
        public void Use(World world, Vec2 target, bool fighting)
        {
            var player = Player;
            if (player.IsDead) return;
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
                case SimEventType.Attack:
                    _current = "기본 공격";
                    break;
                case SimEventType.SkillUsed:
                    _current = Player.Skills[e.Amount].Name;
                    _uses[_current] = _uses.GetValueOrDefault(_current) + 1;
                    break;
                case SimEventType.Burst:
                    _current = Player.Skills[e.Amount].Name;
                    break;
                case SimEventType.Hit:
                    _dealt[_current] = _dealt.GetValueOrDefault(_current) + e.Amount;
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
