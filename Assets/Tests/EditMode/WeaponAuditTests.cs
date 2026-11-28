using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The host's half of the weapon audit (2026-11-28, ArtPreview --only weaponaudit is the screen's and sound's half):
    // every weapon in item_bases.csv, held with the attack down, lets go its kind's basic attack through all three
    // combo steps, so the screen has an Attack event for each step to draw and sound.
    public class WeaponAuditTests
    {
        [Test]
        public void EveryWeaponAttacksThroughItsWholeCombo()
        {
            var failed = new List<string>();
            foreach (var weapon in Gear.Data.Bases.Where(b => b.Slot == ItemSlot.Weapon))
            {
                var world = new World(Maps.Room(20), TileMap.Center(10, 10), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
                var player = world.AddPlayer("a", Gear.Wearing(weapon.Id));
                world.SetAttacking(player.Id, true);
                var attacks = new List<SimEvent>();
                for (int i = 0; i < World.Ticks(4); i++)
                {
                    world.Step();
                    attacks.AddRange(world.TakeEvents().Where(e => e.Type == SimEventType.Attack && e.Source == player.Id));
                }
                var steps = attacks.Select(e => World.ComboStepOf(e.Amount)).Distinct().Count();
                if (attacks.Count < World.ComboSteps || steps < World.ComboSteps || attacks.Any(e => World.AttackKindOf(e.Amount) != weapon.Kind.Attack.Kind))
                    failed.Add($"{weapon.Id}: {attacks.Count} attacks, {steps} steps");
            }
            CollectionAssert.IsEmpty(failed);
        }
    }
}
