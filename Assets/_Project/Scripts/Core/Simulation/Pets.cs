using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A pet (Docs/Animals.md, 펫): a small creature that follows its player and gives a little bonus. It hatches from
    // an egg (a material in materials.csv) that its kind of monster sometimes drops.
    public sealed class PetDefinition
    {
        internal PetDefinition(string id, string name, string eggId, string[] sources, string look, string bonus, bool bites, params Modifier[] modifiers)
        {
            Id = id;
            Name = name;
            EggId = eggId;
            Sources = sources;
            Look = look;
            Bonus = bonus;
            Bites = bites;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string Name { get; }
        public string EggId { get; }
        // The monsters whose fall may leave its egg.
        public IReadOnlyList<string> Sources { get; }
        // The monster it is drawn as, small (CreatureArt.Monster).
        public string Look { get; }
        // What it gives, for the screen.
        public string Bonus { get; }
        // Whether it nips at monsters near its player.
        public bool Bites { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public static class Pets
    {
        public static readonly IReadOnlyList<PetDefinition> All = new[]
        {
            new PetDefinition("pet_slime", "말랑이", "slime_egg", new[] { "slime", "ice_slime", "magma_slime" }, "slime", "생명력 재생 +1", false,
                new Modifier(StatType.HealthRegen, ModifierKind.Flat, 1)),
            new PetDefinition("pet_bat", "꼬마 박쥐", "bat_egg", new[] { "glow_bat", "crystal_bat", "fire_bat" }, "glow_bat", "이동 속도 +5%", false,
                new Modifier(StatType.MoveSpeed, ModifierKind.Increased, 5)),
            new PetDefinition("pet_wisp", "꼬마 정령", "wisp_egg", new[] { "frost_wisp", "crystal_wisp", "void_wisp", "tide_wisp" }, "tide_wisp", "최대 마나 +15, 마나 재생 +2", false,
                new Modifier(StatType.MaxMana, ModifierKind.Flat, 15), new Modifier(StatType.ManaRegen, ModifierKind.Flat, 2)),
            new PetDefinition("pet_crab", "꼬마 게", "crab_egg", new[] { "lake_crab", "cave_beetle" }, "lake_crab", "방어도 +8, 곁의 몬스터를 2초마다 문다", true,
                new Modifier(StatType.Armor, ModifierKind.Flat, 8)),
        };

        // How often a monster that has an egg leaves it.
        public const float EggChance = 0.02f;
        // A biting pet: how often, how far from its player, and what share of its player's damage.
        public const float BiteSeconds = 2;
        public const float BiteReach = 2.5f;
        public const float BiteShare = 0.3f;

        public static PetDefinition Find(string id) => All.FirstOrDefault(p => p.Id == id);

        public static PetDefinition ByEgg(string materialId) => All.FirstOrDefault(p => p.EggId == materialId);

        public static PetDefinition FromMonster(string monsterId) => All.FirstOrDefault(p => p.Sources.Contains(monsterId));

        // Its number in snapshots (0 for none).
        public static int IndexOf(PetDefinition pet) => pet == null ? 0 : All.ToList().IndexOf(pet) + 1;

        public static PetDefinition ByIndex(int index) => index >= 1 && index <= All.Count ? All[index - 1] : null;
    }

    public sealed partial class World
    {
        // The egg's pet follows the player from now on; the pet they had goes back into its egg in the bag.
        public bool Hatch(int playerId, MaterialDefinition egg)
        {
            var player = FindPlayer(playerId);
            var pet = egg == null ? null : Pets.ByEgg(egg.Id);
            if (player == null || pet == null || player.Inventory.CountOf(egg) == 0 || player.Pet == pet) return false;
            player.Inventory.TakeMaterial(egg, 1);
            if (player.Pet != null)
            {
                var old = Loot?.Materials.FirstOrDefault(m => m.Id == player.Pet.EggId);
                if (old != null) player.Inventory.AddMaterial(old, 1);
            }
            player.Pet = pet;
            player.RefreshStats();
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Hatched, player.Id, target: Pets.IndexOf(pet)));
            return true;
        }

        public static void RestorePet(Player player, string id)
        {
            player.Pet = string.IsNullOrEmpty(id) ? null : Pets.Find(id);
            player.RefreshStats();
        }

        // A monster with an egg sometimes leaves it where it fell.
        void DropEgg(Enemy enemy, Player killer)
        {
            var pet = Pets.FromMonster(enemy.Definition.Id);
            if (pet == null || killer == null || Loot == null || !_random.Chance(Pets.EggChance)) return;
            var egg = Loot.Materials.FirstOrDefault(m => m.Id == pet.EggId);
            if (egg != null) DropMaterial(egg, Scatter(enemy.Position));
        }

        // A biting pet nips the nearest monster beside its player now and then.
        void PetBites(Player player)
        {
            if (player.Pet == null || !player.Pet.Bites || player.IsDead || Tick % Ticks(Pets.BiteSeconds) != 0) return;
            var target = _enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) <= Pets.BiteReach + e.Definition.Radius)
                .OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            if (target == null) return;
            HitEnemy(player, target, player.Stats.DamageFor(SkillTag.Melee) * Pets.BiteShare, false, player.Position, SkillTag.None, knockback: false);
        }
    }
}
