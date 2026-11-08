using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Client.Tests
{
    // Scenes of the newer features played in the real client for a few seconds each: the host's world is set up
    // directly (GameRoot.HostWorld), the view draws it, and Unity's test framework fails the test on any error or
    // exception logged meanwhile. So a null reference in a machine's drawing, a missing sprite or a broken HUD line
    // shows here without anyone playing. The Core rules themselves are tested in Assets/Tests/EditMode.
    public class FeatureTests
    {
        const float SceneSeconds = 3;

        GameRoot _root;

        // A world and character of their own, so the scenes never touch the player's saves.
        const string Name = "playmode-feature-test";

        static void Forget()
        {
            SaveStore.Delete(SaveStore.WorldKey(Name));
            SaveStore.Delete(SaveStore.CharacterKey(Name));
            Prefs.Delete("hotbar." + Name);
        }

        [SetUp]
        public void CreateRoot()
        {
            Assert.IsNotNull(GameSheets.Load(), "GameSheets asset is missing. Run Game > Set Up Project first.");
            Forget();
            _root = new GameObject("Game").AddComponent<GameRoot>();
            _root.PlayerName = Name;
            _root.WorldNameField = Name;
        }

        [UnityTearDown]
        public IEnumerator DestroyRoot()
        {
            _root.Leave();
            Object.Destroy(_root.gameObject);
            yield return null;
            yield return null;
            Forget();
        }

        IEnumerator Start()
        {
            _root.StartSolo();
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            Assert.IsNotNull(_root.HostWorld, "A solo game hosts its own world.");
        }

        World World => _root.HostWorld;
        Player Player => _root.HostPlayer;

        // The tile under the player, offset by so many tiles.
        (int X, int Y) Near(int dx, int dy) => (TileMap.Cell(Player.Position.X) + dx, TileMap.Cell(Player.Position.Y) + dy);

        // Built, so the change reaches the view as a player's building would (World.Build).
        void Lay(params (int X, int Y, TileType Type)[] tiles)
        {
            foreach (var (x, y, type) in tiles) World.Build(x, y, type);
        }

        MaterialDefinition Material(string id) => _root.LootSheet.FindMaterial(id);

        void Give(string id, int count)
        {
            Player.Inventory.AddMaterial(Material(id), count);
            Player.InventoryVersion++;
        }

        Enemy Spawn(string monster, Vec2 at) => World.SpawnEnemy(_root.MonsterSheet.Find(monster), at);

        // Holding the attack against a ring of slimes: the combo's three steps, hit stars, knockback, hit-stop, and
        // the slimes striking back (the red edge of the screen).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheComboAndHitsDraw()
        {
            yield return Start();
            var slimes = Enumerable.Range(0, 5).Select(i => Spawn("slime", Player.Position + new Vec2(1.2f, (i - 2) * 0.5f))).ToList();
            foreach (var slime in slimes) slime.MaxHealth = slime.Health = 5000;
            _root.HoldAttack = true;
            _root.HeldAim = new Vec2(1, 0);
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("combo");
            // The same on a touch screen: the sticks and the skill buttons.
            Controls.ForceTouch(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Screens.Take("combo-touch");
            Controls.ForceTouch(false);
            _root.HoldAttack = false;
            Assert.IsTrue(_root.Playing);
            Assert.IsTrue(slimes.Any(s => s.Health < s.MaxHealth), "Holding the attack hit nothing.");
            Assert.Greater(Player.ComboStep + 1, 0);
        }

        // Every machine this round of work added, running: a water wheel at a pool, a solar panel, an arc furnace with
        // ore, a tesla tower and a turret with fire ammo with a slime to shoot, a filter with two picks, a harvester
        // with seeds over soil.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheNewMachinesDraw()
        {
            yield return Start();
            var water = Near(4, 3);
            var wheel = Near(3, 3);
            var solar = Near(-3, 3);
            var furnace = Near(0, 4);
            var tesla = Near(-4, -2);
            var turret = Near(4, -2);
            var filter = Near(-2, -4);
            var harvester = Near(2, -4);
            var soil = Near(2, -5);
            Lay((water.X, water.Y, TileType.Water), (wheel.X, wheel.Y, TileType.WaterWheel), (solar.X, solar.Y, TileType.SolarPanel),
                (furnace.X, furnace.Y, TileType.ArcFurnace), (tesla.X, tesla.Y, TileType.TeslaTower), (turret.X, turret.Y, TileType.Turret),
                (filter.X, filter.Y, TileType.Filter), (harvester.X, harvester.Y, TileType.Harvester), (soil.X, soil.Y, TileType.Soil));
            var none = System.Array.Empty<(MaterialDefinition, int)>();
            World.RestoreMachine(furnace.X, furnace.Y, 0, 0, null, 0, new[] { (Material("iron_ore"), 8) }, none);
            World.RestoreMachine(turret.X, turret.Y, 0, 0, null, 0, new[] { (Material(World.FireAmmo), 10) }, none);
            World.RestoreMachine(filter.X, filter.Y, 0, 0, null, 0, none, none, new[] { Material("iron_ore"), Material("stone") });
            World.RestoreMachine(harvester.X, harvester.Y, 1, 0, null, 0, new[] { (Material("mushroom_spore"), 3) }, none);
            var slime = Spawn("slime", TileMap.Center(turret.X + 2, turret.Y));
            slime.MaxHealth = slime.Health = 5000;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("machines");
            Assert.IsTrue(_root.Playing);
            Assert.IsTrue(World.IsPowered(World.MachineAt(wheel.X, wheel.Y)), "The water wheel did not turn beside water.");
        }

        // A defense under way at a stone beside the player, with built walls in the way to break.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ADefenseDraws()
        {
            yield return Start();
            var stone = Near(0, 1);
            var wall = Near(0, 3);
            Lay((stone.X, stone.Y, TileType.WardStone), (wall.X, wall.Y, TileType.Wall));
            World.RestoreDefense(1, 0);
            Assert.IsTrue(World.StartDefense(Player.Id), "The defense did not start beside the stone.");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("defense");
            Assert.IsTrue(_root.Playing);
        }

        // A rift opened at an altar with a key.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ARiftDraws()
        {
            yield return Start();
            var altar = Near(0, 1);
            Lay((altar.X, altar.Y, TileType.RiftAltar));
            Give(World.RiftKey, 1);
            Assert.IsTrue(World.OpenRift(Player.Id), "The rift did not open at the altar.");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("rift");
            Assert.IsTrue(_root.Playing);
        }

        // The weathers and the calendar's events, a skylight laid by the player, and the time of day passing over
        // the cave's light wells.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator WeatherEventsAndSkylightsDraw()
        {
            yield return Start();
            var skylight = Near(1, 1);
            Lay((skylight.X, skylight.Y, TileType.Skylight));
            foreach (var weather in new[] { Weather.Storm, Weather.Fog, Weather.Rain })
            {
                int day = Enumerable.Range(2, 400).First(d => World.WeatherOf(d) == weather);
                World.RestoreClock((day - 1) * Calendar.TicksPerDay);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Screens.Take("weather-" + weather.ToString().ToLowerInvariant());
            }
            // A full moon's afternoon and night, and a market day's morning. A day starts at six.
            foreach (var (day, hour) in new[] { (8, 13f), (8, 23f), (5, 9f) })
            {
                World.RestoreClock((long)((day - 1 + (hour - Calendar.MorningHour) / 24) * Calendar.TicksPerDay));
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Screens.Take($"day{day}-{hour:00}h");
            }
            Assert.IsTrue(_root.Playing);
        }

        // Skill points (Docs/Classes.md, 스킬 포인트와 단계): a character of level 9 with some skills learned and
        // raised, the bag's skills page with its icons, ranks and [배우기]/[올리기], then the skill keys with their
        // icons and the lean HUD with its goal list, a dash and a heal used.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator SkillPointsDraw()
        {
            yield return Start();
            World.GainExperience(Player, Leveling.TotalFor(9));
            foreach (string id in new[] { "war_cry", "charge", "whirlwind", "flame_brand" }) World.LearnSkill(Player.Id, _root.ClassSheet.Skills.First(s => s.Id == id));
            yield return new WaitForSecondsRealtime(1);
            _root.ShowBag("스킬");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("skills-page");
            _root.BagOpen = false;
            for (int slot = 0; slot < _root.SkillCount; slot++) _root.UseSkill(slot);
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("skill-keys");
            Assert.IsTrue(_root.Playing);
        }

        // Mana spent and drunk back, and the set and rift legendaries worn.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ManaAndSetsDraw()
        {
            yield return Start();
            for (int slot = 0; slot < _root.SkillCount; slot++) _root.UseSkill(slot);
            yield return new WaitForSecondsRealtime(1);
            Player.Mana = 5;
            var potion = _root.LootSheet.Consumables.First(c => c.RestoresMana);
            Player.Inventory.AddConsumable(potion, 2);
            Player.InventoryVersion++;
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(World.UseConsumable(Player.Id, potion), "The mana potion was refused.");
            var generator = new LootGenerator(_root.LootSheet);
            foreach (var piece in generator.LegendariesOf(World.RiftLegendaries).Where(b => b.Set != null))
            {
                Player.Equip(generator.RollLegendary(piece, new GameRandom(3)));
            }
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("mana-set");
            // The bag, with the set worn.
            _root.BagOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("bag");
            _root.BagOpen = false;
            Assert.IsTrue(_root.Playing);
            Assert.Greater(Player.Stats.MaxMana, 0);
        }
    }
}
