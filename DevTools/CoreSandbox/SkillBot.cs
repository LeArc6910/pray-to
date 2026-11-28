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
        // --arts: the weapon in hand's own art as soon as it is ready and the target is in its reach (Docs/Weapons.md
        // 묶음 C); a greatsword's is held to the full and lets go by itself.
        public static bool Arts;

        public SkillBot(Player player, LootData loot)
        {
            Player = player;
            if (Level is int level) player.Experience = Leveling.TotalFor(level);
            var open = ClassRules.AllSkillsFor(player.Classes).Where(s => Level == null || s.Level <= Level).ToList();
            int rank = Build != null && Level is int built ? TestKit.BuiltRank(open, built) : Rank;
            foreach (var skill in open) player.SkillRanks[skill] = Math.Clamp(rank, 1, SkillRules.MaxRank);
            if (Build != null && Level != null)
            {
                TestKit.SpendPassives(player, Build, TestKit.AttackWorth);
                TestKit.SetCrystals(player, open, rank, loot);
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
            var art = World.ArtOf(player);
            if (Arts && fighting && art != null && player.Art == null && world.ArtTicksLeft(player, player.Stats.Kind) == 0)
            {
                // Near enough for it to land: a shooter's or a caster's at their range, a blade's at its reach.
                float reach = art.Kind switch
                {
                    WeaponArtKind.Slam or WeaponArtKind.Whirl => art.Radius + 0.8f,
                    WeaponArtKind.Backstep or WeaponArtKind.Barrage => 7,
                    WeaponArtKind.Gravity => art.Reach + art.Radius,
                    WeaponArtKind.Pierce => art.Reach,
                    _ => art.Reach + 0.8f,
                };
                if (toTarget.Length <= reach) world.UseWeaponArt(player.Id, toTarget);
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
                // Once a use: its beginning, or the blow of one that has none (a dagger's, a staff's, a bow's).
                case SimEventType.Art when World.ArtPhaseOf(e.Amount) == ArtPhase.Start
                    || World.ArtPhaseOf(e.Amount) == ArtPhase.Strike && World.ArtKindOf(e.Amount) is WeaponArtKind.Vital or WeaponArtKind.Wave or WeaponArtKind.Backstep:
                    _uses["무기 기술"] = _uses.GetValueOrDefault("무기 기술") + 1;
                    break;
                case SimEventType.Healed:
                    Drunk++;
                    break;
                case SimEventType.Hit:
                    string by = e.Cause == 0 ? "기본 공격" : e.Cause == World.ArtCause ? "무기 기술" : e.Cause <= Player.Skills.Count ? Player.Skills[e.Cause - 1].Name : "기타";
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
