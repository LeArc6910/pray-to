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

        static void Forget() => TestSaves.Forget(Name);

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

        // A solo game with a warrior and fire mage: a new character has no class (picked in the game, 2026-11-09), so
        // the scenes give it the two whose skills they draw.
        IEnumerator Start()
        {
            _root.StartSolo();
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            Assert.IsNotNull(_root.HostWorld, "A solo game hosts its own world.");
            Player.ChangeKit(new[] { "warrior", "fire_mage" }.Select(id => _root.ClassSheet.Classes.First(c => c.Id == id)).ToList(), null);
            yield return new WaitForSecondsRealtime(0.3f);
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

        // Picking a class in the game (Docs/Classes.md, 클래스 고르기, 2026-11-09): a new character has none, and its
        // skills page offers every class with [고르기]; picked, its first skill comes to the bar and no more is offered
        // until level 10.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ClassPickDraws()
        {
            _root.StartSolo();
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            Assert.AreEqual(0, _root.SkillCount, "A new character starts with no class.");
            _root.ShowBag("스킬");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("class-pick");
            Assert.IsTrue(World.ChangeClasses(Player.Id, new[] { _root.ClassSheet.Classes.First(c => c.Id == "archer") }, null));
            yield return new WaitForSecondsRealtime(1);
            Assert.AreEqual(1, _root.SkillCount, "The archer's first skill.");
            yield return Screens.Take("class-picked");
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

        // Locking a door (Docs/Crafting.md, 문 잠그기, 2026-11-09): a door across a wall, locked from the bag; the bag
        // then offers [잠금 풀기], and the player who locked it walks through it.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ALockedDoorDraws()
        {
            yield return Start();
            var door = Near(1, 0);
            var tiles = new List<(int, int, TileType)> { (door.X, door.Y, TileType.Door) };
            for (int dy = -2; dy <= 2; dy++)
                if (dy != 0) tiles.Add((door.X, door.Y + dy, TileType.Wall));
            // Floor round the door, not on it (2026-11-11: dx != 1 laid floor over the door itself).
            for (int dx = -1; dx <= 2; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0) tiles.Add((door.X + dx, door.Y + dy, TileType.Floor));
            Lay(tiles.ToArray());
            Player.Position = TileMap.Center(door.X - 1, door.Y);
            Player.Corrections++;
            _root.AimHeld = true;
            _root.HeldAim = new Vec2(1, 0);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(World.LockDoor(Player.Id), "The door ahead did not lock.");
            _root.ShowBag("재료·제작");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("locked-door");
            Assert.AreEqual(TileType.LockedDoor, World.Map[door.X, door.Y]);
            Assert.IsTrue(_root.LocalClient.IsMyDoor(door.X, door.Y), "The client never heard the lock was its own.");
            _root.BagOpen = false;
            _root.AimHeld = false;
            Assert.IsTrue(_root.Playing);
        }

        // The story (Docs/Story.md, 2026-11-10): the solo world has its places carved (the sheets were read), and the
        // royal city shows its plaza, lamps, waypoint and people.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheStoryDraws()
        {
            yield return Start();
            Assert.GreaterOrEqual(World.Places.Count, 2, "No places in the world: the story sheets were not read (Game > Set Up Main Scene).");
            var city = World.Places[1];
            Player.Position = TileMap.Center(city.X, city.Y - 2);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("story-city");
            Assert.IsTrue(_root.Playing);
        }

        // The arenas and the entrances (Docs/Bosses.md 아레나 컨셉과 입장 연출, 2026-11-17): the black knight's hall is
        // dressed (the arenas' sheet was read), the knight kneels asleep in it, and woken it comes in before it fights.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ABossComesInToItsArena()
        {
            yield return Start();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "black_knight");
            Assert.IsNotNull(boss, "No black knight: the story's places were not carved.");
            Assert.IsNotNull(boss.Definition.Arena, "No arenas: arenas.csv was not read (Game > Set Up Main Scene).");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            Assert.AreEqual(BossState.Dormant, boss.State);
            yield return Screens.Take("arena-hall");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the knight comes in first.");
            yield return Screens.Take("entrance-knight");
            yield return new WaitForSecondsRealtime(4.5f);
            Assert.AreNotEqual(BossState.Enter, boss.State, "The entrance ends and the fight starts.");
            Assert.IsTrue(_root.Playing);
        }

        // Ordinary monsters' attacks from monster_attacks.csv (Docs/Combat.md 몬스터의 공격 시트, 2026-11-21): a slime's
        // pounce is marked where it will land, a frost wisp rings itself (a mark the screen makes from the monster's
        // state), and both draw their pieces; after they land nothing is left.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator MonstersWarnAndStrikeFromTheSheet()
        {
            yield return Start();
            Assert.IsNotEmpty(_root.MonsterSheet.Find("slime").Attacks, "No monster attacks: monster_attacks.csv was not read (Game > Set Up Main Scene).");
            World.MonstersHold = true;
            var slime = Spawn("slime", Player.Position + new Vec2(3.2f, 0));
            var wisp = Spawn("frost_wisp", Player.Position + new Vec2(-2.2f, 0));
            slime.MaxHealth = slime.Health = wisp.MaxHealth = wisp.Health = 5000;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(World.ForceAttack(slime, "pounce"));
            Assert.IsTrue(World.ForceAttack(wisp, "frost_ring"));
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.GreaterOrEqual(_root.MonsterMarksShown, 2, "The landing's mark and the ring made from the wisp's state are both drawn.");
            Assert.Greater(_root.MonsterFxPieces, 0);
            yield return Screens.Take("monster-warnings");
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Screens.Take("monster-blows");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(0, _root.MonsterMarksShown);
            Assert.AreEqual(0, _root.MonsterFxAlive);
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // A pack notices together (2026-11-22, Docs/WorldRework.md 무리): five slimes as one pack, most further off than
        // they see; one notices the player and the rest come too, each with a mark over its head.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator APackNoticesTogetherWithMarks()
        {
            yield return Start();
            World.MonstersHold = true;
            var members = Enumerable.Range(0, 5).Select(i => Spawn("slime", Player.Position + new Vec2(6 + i * 1.3f, (i % 2 == 0 ? 1 : -1) * (i * 0.4f)))).ToList();
            foreach (var member in members) member.MaxHealth = member.Health = 5000;
            var pack = World.Gather(members);
            yield return new WaitForSecondsRealtime(0.5f);
            int shown = _root.AlertsShown;
            World.MonstersHold = false;
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Screens.Take("pack-alert");
            Assert.IsTrue(pack.Alerted, "The nearest saw the player.");
            Assert.IsTrue(members.All(m => m.State != EnemyState.Idle), "And the rest came with it, the furthest well out of its sight.");
            Assert.GreaterOrEqual(_root.AlertsShown - shown, members.Count, "A mark over each head.");
            Assert.IsTrue(_root.Playing);
        }

        // Field elites (2026-11-22, Docs/WorldRework.md 들판 정예): a leader with fire and guard among three slimes and
        // three fierce champion beetles, each with its name over its head inside the screen, lines from the guard to its
        // minions, and the warning sign while the leader winds up its big attack.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ElitesWearTheirNamesAndWarnOfTheirBigAttack()
        {
            yield return Start();
            World.MonstersHold = true;
            var leader = World.MakeEliteHere(_root.MonsterSheet.Find("myconid"), Player.Position + new Vec2(4, 0.5f), EliteRank.Leader,
                RiftAffix.Fire | RiftAffix.Guard, _root.MonsterSheet.Find("slime"), 3);
            World.MakeEliteHere(_root.MonsterSheet.Find("cave_beetle"), Player.Position + new Vec2(-4.5f, 0), EliteRank.Champion, RiftAffix.Fierce);
            foreach (var monster in World.Enemies) monster.MaxHealth = monster.Health = 5000;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(4, _root.EliteTagsShown, "A name over the leader and each champion.");
            Assert.LessOrEqual(_root.EliteTagWidest, Ui.Width, "Every name fits the screen.");
            Assert.GreaterOrEqual(_root.EliteGuardLinksShown, 1, "The guard's lines to its minions.");
            yield return Screens.Take("elite-tags");
            Assert.IsTrue(World.ForceAttack(leader, "spore_smash"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(_root.WarningShown, "The warning sign over a leader winding up its big attack.");
            yield return Screens.Take("elite-warning");
            Assert.IsTrue(_root.Playing);
        }

        // The rest of the kinds on the sheet (Docs/MonsterPatterns.md, 2026-11-22): a scorpion lying in wait is a mound
        // and not a body, two demon soldiers side by side are joined by a line, a stalker's wedge and a caster's runes
        // are marked, and a blow a beetle's shell turns shows a grey number. Every blow shown is counted.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator EveryKindHasItsOwnAttacks()
        {
            yield return Start();
            Assert.IsNotEmpty(_root.MonsterSheet.Find("abyss_stalker").Attacks, "No rows for the later monsters: monster_attacks.csv is old (Game > Set Up Main Scene).");
            World.MonstersHold = true;
            var scorpion = Spawn("sand_scorpion", Player.Position + new Vec2(-3.5f, 2));
            var soldier = Spawn("demon_soldier", Player.Position + new Vec2(3, 2.2f));
            var second = Spawn("demon_soldier", Player.Position + new Vec2(3, 3.3f));
            var stalker = Spawn("abyss_stalker", Player.Position + new Vec2(2, -0.5f));
            var caster = Spawn("demon_caster", Player.Position + new Vec2(-4, -2));
            foreach (var monster in new[] { scorpion, soldier, second, stalker, caster }) monster.MaxHealth = monster.Health = 5000;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(scorpion.Buried, "A scorpion that has not made its ambush lies under the ground.");
            Assert.GreaterOrEqual(_root.MonsterMoundsShown, 1, "It is drawn as a mound.");
            Assert.GreaterOrEqual(_root.MonsterLinksShown, 1, "Two soldiers side by side are joined by a line.");
            int blows = _root.MonsterBlowsShown;
            Assert.IsTrue(World.ForceAttack(stalker, "cleave"));
            Assert.IsTrue(World.ForceAttack(caster, "rune_trap"));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.GreaterOrEqual(_root.MonsterMarksShown, 3, "The stalker's wedge and the caster's two runes are drawn.");
            yield return Screens.Take("monster-kinds-warnings");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Greater(_root.MonsterBlowsShown, blows, "The stalker's cut is shown when it lands.");
            yield return Screens.Take("monster-kinds-blows");

            var beetle = Spawn("cave_beetle", Player.Position + new Vec2(1.4f, 1));
            beetle.MaxHealth = beetle.Health = 5000;
            beetle.Facing = (Player.Position - beetle.Position).Normalized;
            int grey = _root.GuardedNumbersShown;
            World.Hurt(beetle, Player, 100);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(_root.GuardedNumbersShown, grey, "A blow its shell turned shows a grey number.");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }
    }
}
