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

        // What the bots know (Docs/Classes.md, 스킬 포인트와 단계): every skill of their classes at Rank, or with a
        // Level only those open by then (and the bots are that level). --rank and --level set them.
        public static int Rank = 1;
        public static int? Level;
        // --build (with --level): what a player of that level would have made of it (2026-11-09, Docs/Classes.md
        // 성장 곡선): the skill points spread over the open skills (Rank is worked out, not taken), the passive points
        // spent greedily on damage first, and the best crystals in every damage skill's slots.
        public static ClassData Build;

        public SkillBot(Player player, LootData loot)
        {
            Player = player;
            if (Level is int level) player.Experience = Leveling.TotalFor(level);
            var open = ClassRules.AllSkillsFor(player.Classes).Where(s => Level == null || s.Level <= Level).ToList();
            int rank = Build != null && Level is int built ? BuiltRank(open, built) : Rank;
            foreach (var skill in open) player.SkillRanks[skill] = Math.Clamp(rank, 1, SkillRules.MaxRank);
            if (Build != null && Level != null)
            {
                SpendPassives(player, Build);
                SetCrystals(player, open, rank, loot);
            }
            player.ChangeKit(player.Classes, null);
            foreach (var skill in player.Skills) player.ClearCooldown(skill);
            player.Health = player.Stats.MaxHealth;
            foreach (string id in new HostConfig().StarterConsumables)
            {
                var consumable = loot.Consumables.FirstOrDefault(c => c.Id == id);
                if (consumable != null) player.Inventory.AddConsumable(consumable);
            }
        }

        // The rank the four skills in the slots reach with the level's points, once every open skill is learned (a
        // class's first skill is free).
        static int BuiltRank(IReadOnlyList<SkillDefinition> open, int level)
        {
            int learning = open.Count(s => !s.IsBasic);
            int left = Math.Max(0, level - 1 - learning);
            return Math.Clamp(1 + left / ClassRules.SkillSlots, 1, SkillRules.MaxRank);
        }

        // How much a passive point is worth to a fighter, by what it gives: damage and speed first, then crits,
        // skill ranks and cooldowns, then health and armour.
        static float Worth(PassiveNode node) => node.Modifiers.Sum(m => m.Stat switch
        {
            StatType.Damage => m.Kind == ModifierKind.Flat ? 3f * m.Value : m.Kind == ModifierKind.More ? 1.5f * m.Value : m.Value,
            StatType.AttackSpeed => 0.9f * m.Value,
            StatType.CritChance => 0.6f * m.Value,
            StatType.CritDamage => 0.3f * m.Value,
            StatType.SkillRank => 15f * m.Value,
            StatType.CooldownRecovery => 0.6f * m.Value,
            StatType.MaxHealth => m.Kind == ModifierKind.Flat ? 0.1f * m.Value : 0.4f * m.Value,
            StatType.Armor => 0.1f * m.Value,
            StatType.HealthRegen => 0.5f * m.Value,
            StatType.ManaRegen or StatType.MaxMana => 0.1f * m.Value,
            _ => 0f,
        });

        // Spends every passive point, each on the open point (next to one taken) worth most, looking one step past it.
        static void SpendPassives(Player player, ClassData classes)
        {
            player.Passives.Clear();
            while (player.PassivePoints > 0)
            {
                var open = classes.Passives.Where(n => !n.IsStart && !player.Passives.Contains(n) && n.Links.Any(l => l.IsStart || player.Passives.Contains(l))).ToList();
                if (open.Count == 0) break;
                var next = open.OrderByDescending(n => Worth(n) + 0.5f * n.Links.Where(l => !l.IsStart && !player.Passives.Contains(l)).Select(Worth).DefaultIfEmpty(0).Max())
                    .First();
                player.Passives.Add(next);
            }
            player.RefreshStats();
        }

        // The crystals a player would set: in every skill that deals damage, as many as its rank holds, the first that
        // fit of power, split, spread, haste, pierce, then the rest.
        static readonly SupportKind[] Preferred =
        {
            SupportKind.Power, SupportKind.Multi, SupportKind.Area, SupportKind.Haste, SupportKind.Pierce, SupportKind.Leech, SupportKind.Duration,
        };

        static void SetCrystals(Player player, IEnumerable<SkillDefinition> skills, int rank, LootData loot)
        {
            var crystals = SkillSupports.Ids.Select(id => loot.Materials.FirstOrDefault(m => m.Id == id)).Where(m => m != null)
                .ToDictionary(m => SkillSupports.KindOf(m).Value);
            foreach (var skill in skills)
            {
                var held = new List<SupportKind>();
                foreach (var kind in Preferred)
                {
                    if (held.Count >= SkillSupports.SlotsFor(rank) || !crystals.ContainsKey(kind)) continue;
                    if (SkillSupports.CanAdd(skill, rank, held, kind)) held.Add(kind);
                }
                if (held.Count > 0) player.Supports[skill] = held.Select(k => crystals[k]).ToList();
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
                    SkillKind.Summon => fighting && toTarget.Length <= skill.Range,
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
