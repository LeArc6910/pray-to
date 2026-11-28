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
            Ui.InsetOverride = null;
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
            yield return TestSaves.UntilPlaying(_root, 1);
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

        // Out of the places onto open floor, floored round and cleared, as the web check's monster sections do (Playtest
        // "goto wild", sweep.js clearRoom): a solo game starts inside the dawn village, and World.KeepOutOfPlaces sets a
        // monster put there back to the safe ring's edge, off the screen, before it acts (the local run of 2026-11-27:
        // the monster scenes found no marks, no mound and no names).
        IEnumerator ToTheWild()
        {
            Playtest.Run(_root, "goto wild");
            var (px, py) = Near(0, 0);
            for (int dx = -6; dx <= 6; dx++)
                for (int dy = -4; dy <= 4; dy++)
                    if (dx != 0 || dy != 0) Lay((px + dx, py + dy, TileType.Floor));
            Playtest.Run(_root, "clear");
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // Up to the void gear's level, wearing it, healed, as the web check's boss sections are (sweep.js bosses): bare at
        // level 1 the boss's first blows knocked the player down four tiles off its home, it went back to sleep, and
        // World.ForceAttack had no one it fights (the local run of 2026-11-27; a Core run of the same scenes forces both
        // attacks of all four bosses this way).
        void Toughen() => Playtest.Run(_root, "xp 2000000; equip void_sword void_helmet void_armor void_boots; heal");

        // The things "scene places" lays, beside the player.
        static readonly (int Dx, int Dy, TileType Type)[] PlaceThings =
            { (-9, 3, TileType.Cottage), (-4, 3, TileType.Cabin), (1, 3, TileType.Well), (3, 3, TileType.LampPost), (3, 5, TileType.Crates) };

        // "scene places" in the first of a biome's first four pack rooms (the player stands in the first) where every
        // thing laid takes the biome's look, as the web check does (sweep.js places): each takes the look of the biome
        // under it, so in a room by another biome's edge some take that one's (the local run of 2026-11-27: the ice
        // cave's lamp post, the abyss's well).
        IEnumerator PlacesInARoomOfItsOwn(string room, int look)
        {
            for (int n = 1; n <= 4; n++)
            {
                if (n > 1)
                {
                    Playtest.Run(_root, $"clear; goto room pack {room} {n}");
                    yield return new WaitForSecondsRealtime(1.5f);
                }
                Playtest.Run(_root, "clear; scene places");
                yield return new WaitForSecondsRealtime(1.5f);
                int px = TileMap.Cell(_root.LocalClient.Position.X), py = TileMap.Cell(_root.LocalClient.Position.Y);
                if (PlaceThings.All(t => _root.View.StandAt(px + t.Dx, py + t.Dy).Shape == look)) yield break;
            }
        }

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
            // A notched phone held sideways (Screen.safeArea stood in): the HUD, minimap, sticks and skill buttons keep
            // inside the safe area, the world still fills the whole screen.
            Ui.InsetOverride = new Ui.Edges(44, 16, 44, 20);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Screens.Take("combo-touch-safearea");
            Ui.InsetOverride = null;
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
            yield return TestSaves.UntilPlaying(_root, 1);
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

        // The screens of their own (Docs/UiRework.md U2): the bag, the growth screens, the journal and the map, one at a
        // time, each by its name as the playtests' "screen" command opens it, then the touch hub ([메뉴]) on a touch
        // screen. A picture of each.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator EveryScreenDraws()
        {
            yield return Start();
            World.GainExperience(Player, Leveling.TotalFor(5));
            yield return new WaitForSecondsRealtime(0.5f);
            try
            {
                foreach (var screen in ScreenStack.All)
                {
                    _root.ShowScreen(screen);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.AreEqual(screen, _root.OpenScreen, "One screen at a time, the one asked for.");
                    Assert.AreEqual(screen != GameScreen.Map, _root.BagOpen, "The bag's old tabs count as the bag; the map does not.");
                    yield return Screens.Take("screen-" + screen.ToString().ToLowerInvariant());
                }
                _root.ShowScreen(GameScreen.None);
                Controls.ForceTouch(true);
                _root.ShowHub(true);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsTrue(_root.HubOpen);
                yield return Screens.Take("screen-hub");
                _root.ShowScreen(GameScreen.Passives);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsFalse(_root.HubOpen, "Opening a screen shuts the hub.");
                yield return Screens.Take("screen-passives-phone");
                _root.ShowScreen(GameScreen.None);
            }
            finally
            {
                Controls.ForceTouch(false);
            }
            Assert.IsTrue(_root.Playing);
        }

        // The growth screens laid out anew (Docs/UiRework.md U4): a warrior and fire mage of level 12 with skills learned
        // and raised, a crystal in the whirlwind, points spent on the tree's left and a pet; the skills' cards and the
        // picked skill's panel, the tree on its night, the character's sheet; then on a touch screen the same three. A
        // picture of each.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheGrowthScreensDraw()
        {
            yield return Start();
            World.MonstersHold = true;
            World.GainExperience(Player, Leveling.TotalFor(12));
            foreach (string id in new[] { "war_cry", "war_cry", "charge", "whirlwind", "whirlwind", "flame_brand" })
                World.LearnSkill(Player.Id, _root.ClassSheet.Skills.First(s => s.Id == id));
            Give("support_area", 1);
            Give("support_leech", 1);
            Assert.IsTrue(World.AddSupport(Player.Id, _root.ClassSheet.Skills.First(s => s.Id == "whirlwind"), Material("support_area")), "The crystal goes into the whirlwind.");
            foreach (string id in new[] { "str1", "str2", "str3", "str5", "dex1", "int1" })
                Assert.IsTrue(World.AllocatePassive(Player.Id, _root.ClassSheet.FindPassive(id)), $"The tree's {id} is learned.");
            Give("slime_egg", 1);
            World.Hatch(Player.Id, Material("slime_egg"));
            Give("coin", 300);
            yield return new WaitForSecondsRealtime(1);
            try
            {
                foreach (bool touch in new[] { false, true })
                {
                    Controls.ForceTouch(touch);
                    foreach (var screen in ScreenStack.Members(ScreenGroup.Growth))
                    {
                        _root.ShowScreen(screen);
                        yield return new WaitForSecondsRealtime(0.8f);
                        Assert.AreEqual(screen, _root.OpenScreen, "The growth screen asked for is up.");
                        yield return Screens.Take($"growth-{screen.ToString().ToLowerInvariant()}{(touch ? "-touch" : "")}");
                    }
                    _root.ShowScreen(GameScreen.None);
                }
            }
            finally
            {
                Controls.ForceTouch(false);
            }
            Assert.IsTrue(_root.Playing);
        }

        // The records laid out anew (Docs/UiRework.md U5): the journal as a parchment book on each of its five sections
        // (goals, story, rumours, bestiary, deeds), then the map on its sheet with its legend. On a PC and on a touch
        // screen. A picture of each.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheRecordsScreensDraw()
        {
            yield return Start();
            World.MonstersHold = true;
            World.GainExperience(Player, Leveling.TotalFor(6));
            Give("copper_ore", 12);
            Give("coin", 300);
            yield return new WaitForSecondsRealtime(1);
            try
            {
                foreach (bool touch in new[] { false, true })
                {
                    Controls.ForceTouch(touch);
                    for (int i = 0; i < InventoryWindow.JournalNames.Length; i++)
                    {
                        _root.ShowJournal((InventoryWindow.JournalSection)i);
                        yield return new WaitForSecondsRealtime(0.6f);
                        Assert.AreEqual(GameScreen.Journal, _root.OpenScreen, "The journal is up.");
                        Assert.AreEqual(InventoryWindow.JournalNames[i], _root.JournalSection, "On the section asked for.");
                        yield return Screens.Take($"records-journal-{i}{(touch ? "-touch" : "")}");
                    }
                    _root.ShowScreen(GameScreen.Map);
                    yield return new WaitForSecondsRealtime(0.8f);
                    Assert.IsTrue(_root.MapOpen, "The map is up.");
                    Assert.Greater(_root.Map.Zoom, 0f, "The map has a zoom.");
                    yield return Screens.Take($"records-map{(touch ? "-touch" : "")}");
                    _root.ShowScreen(GameScreen.None);
                }
            }
            finally
            {
                Controls.ForceTouch(false);
            }
            Assert.IsTrue(_root.Playing);
        }

        // The bag and the HUD (Docs/UiRework.md U6, U7): the bag on each kind chip with a picked sword's card, the HUD with a
        // food's buff and at low health, and the fallen card over the journal left open; on a PC and a touch screen.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheBagAndHudDraw()
        {
            yield return Start();
            World.MonstersHold = true;
            World.GainExperience(Player, Leveling.TotalFor(6));
            Give("stone", 20);
            Give("slime_gel", 6);
            yield return new WaitForSecondsRealtime(1);
            try
            {
                foreach (bool touch in new[] { false, true })
                {
                    Controls.ForceTouch(touch);
                    string tag = touch ? "-touch" : "";
                    _root.ShowBag("장비");
                    foreach (string chip in InventoryWindow.FilterNames)
                    {
                        Assert.IsTrue(_root.SetBagFilter(chip), $"No chip {chip}.");
                        yield return new WaitForSecondsRealtime(0.4f);
                        Assert.AreEqual(chip, _root.BagFilter);
                        yield return Screens.Take($"bag-{chip}{tag}");
                    }
                    _root.SetBagFilter("전체");
                    _root.BagOpen = false;
                    World.UseConsumable(Player.Id, _root.LootSheet.Consumables.First(c => c.Id == "mushroom_soup"));
                    World.Hurt(Player, Player.Health - Player.Stats.MaxHealth * 0.15f);
                    yield return new WaitForSecondsRealtime(0.8f);
                    StringAssert.Contains("버섯 수프", GameRoot.BuffNames, "The food's buff has no icon.");
                    Assert.Greater(UiMotion.LowHealthGlow(GameRoot.LowHealthShare), 0f, "No red at the edges at low health.");
                    yield return Screens.Take($"hud-low{tag}");
                    _root.ShowScreen(GameScreen.Journal);
                    World.Hurt(Player, Player.Health + 100000);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.Greater(GameRoot.DeathShown.width, 0f, "No fallen card over the journal.");
                    Assert.AreEqual(GameScreen.Journal, _root.OpenScreen, "The journal shut on falling.");
                    yield return Screens.Take($"death-window{tag}");
                    World.GetUp(Player.Id);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.IsFalse(Player.IsDead, "Not up after GetUp.");
                    _root.ShowScreen(GameScreen.None);
                    Player.Health = Player.Stats.MaxHealth;
                }
            }
            finally
            {
                Controls.ForceTouch(false);
            }
            Assert.IsTrue(_root.Playing);
        }

        // The encounter windows (Docs/UiRework.md U3): a chest, a workbench, a furnace and an anvil laid round the player
        // open their windows (each counts as the bag's, and shuts once its thing is out of reach); a merchant's stall: her
        // talk with her portrait and choices, then her shop; the village child's talk with her portrait and the line's face.
        // A picture of each, the station's and the talk's on a touch screen too.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheEncounterWindowsDraw()
        {
            yield return Start();
            World.MonstersHold = true;
            var chest = Near(-1, 0);
            var bench = Near(1, 0);
            var furnace = Near(0, 1);
            var anvil = Near(0, -1);
            Lay((chest.X, chest.Y, TileType.Chest), (bench.X, bench.Y, TileType.Workbench), (furnace.X, furnace.Y, TileType.Furnace), (anvil.X, anvil.Y, TileType.Anvil));
            Give("stone", 30);
            Give("copper_ore", 12);
            Give("coin", 300);
            Player.Inventory.TryAdd(new LootGenerator(_root.LootSheet).RollItem(5, new GameRandom(7)));
            Player.InventoryVersion++;
            yield return new WaitForSecondsRealtime(1);
            try
            {
                foreach (var screen in new[] { GameScreen.Chest, GameScreen.Station, GameScreen.Anvil })
                {
                    _root.ShowScreen(screen);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.AreEqual(screen, _root.OpenScreen, $"The {screen} window shut: nothing of its kind in reach.");
                    Assert.IsTrue(_root.BagOpen, "An encounter window counts as the bag's.");
                    yield return Screens.Take("encounter-" + screen.ToString().ToLowerInvariant());
                }
                Controls.ForceTouch(true);
                _root.ShowScreen(GameScreen.Station);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Screens.Take("encounter-station-touch");
                Controls.ForceTouch(false);
                _root.ShowScreen(GameScreen.None);

                // The merchant at a stall of her own away from the rest: walking off the chest shuts its window.
                var merchant = World.Npcs.Merchant;
                Assert.IsNotNull(merchant, "npcs.csv has no merchant at a stall.");
                var stall = Near(6, 0);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -2; dy <= -1; dy++) Lay((stall.X + dx, stall.Y + dy, TileType.Floor));
                Lay((stall.X, stall.Y, merchant.Tile));
                _root.ShowScreen(GameScreen.Chest);
                Player.Position = TileMap.Center(stall.X, stall.Y - 1);
                Player.Corrections++;
                yield return new WaitForSecondsRealtime(1);
                Assert.AreEqual(GameScreen.None, _root.OpenScreen, "Walked away from the chest, its window shut.");
                Assert.IsTrue(_root.TalkMerchantNear(), "The stall keeper is in reach.");
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsTrue(_root.Talking);
                StringAssert.StartsWith(merchant.Id + ":", _root.TalkPortrait, "Her portrait is in the talk box.");
                yield return Screens.Take("encounter-merchant-talk");
                _root.ShowBag("상인");
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.AreEqual(GameScreen.Shop, _root.OpenScreen, "\"bag 상인\" opens her shop.");
                Assert.IsFalse(_root.Talking, "The shop takes the talk's place.");
                yield return Screens.Take("encounter-shop");
                _root.ShowScreen(GameScreen.None);

                // A person of the story: the child's talk, her portrait.
                PlaceSite.Still = true;
                var mina = World.Places.SelectMany(p => p.PeopleAt(World.Clock, World.DayMinutes)).FirstOrDefault(p => p.Character.Id == "mina");
                Assert.IsNotNull(mina.Character, "No village child: characters.csv has no mina.");
                Player.Position = mina.At + new Vec2(0, -1);
                Player.Corrections++;
                yield return new WaitForSecondsRealtime(1);
                Assert.IsTrue(_root.TalkNear(), "The child is in reach.");
                yield return new WaitForSecondsRealtime(0.6f);
                StringAssert.StartsWith("mina:", _root.TalkPortrait);
                yield return Screens.Take("encounter-talk");
                Controls.ForceTouch(true);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Screens.Take("encounter-talk-touch");
            }
            finally
            {
                Controls.ForceTouch(false);
                PlaceSite.Still = false;
                World.MonstersHold = false;
            }
            Assert.IsTrue(_root.Playing);
        }

        // Each weapon kind's own combo and feel (Docs/Weapons.md 묶음 B): the attack held at three slimes with each of
        // the eight, a picture mid-combo (its motion, its arc or shot, its stars).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator EachWeaponKindStrikesItsOwnWay()
        {
            yield return Start();
            foreach (var kind in new[] { "dagger", "greatsword", "spear", "hammer", "crossbow", "boomerang", "wand", "orb" })
            {
                Playtest.Run(_root, $"heal; hero iron_{kind} 1 0");
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Screens.Take("weapon-" + kind);
                Assert.IsTrue(World.Enemies.Any(e => e.Health < e.MaxHealth), $"The {kind}'s attack hit nothing.");
                Playtest.Run(_root, "attack off; clear");
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Assert.IsTrue(_root.Playing);
        }

        // The second weapon (Docs/Weapons.md): the weapons' slot left of the skills with a sword in hand and a bow behind,
        // swapped, on a touch screen, and the bag's worn slots with the spare under the weapon in hand.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheTwoWeaponsDraw()
        {
            yield return Start();
            Playtest.Run(_root, "equip iron_sword; spare iron_bow");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("weapons");
            Assert.IsTrue(_root.SwapWeapons());
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual("iron_bow", Player.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreEqual("iron_sword", Player.Equipment.Spare.Base.Id);
            yield return Screens.Take("weapons-swapped");
            Controls.ForceTouch(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Screens.Take("weapons-touch");
            Controls.ForceTouch(false);
            _root.BagOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("weapons-bag");
            _root.BagOpen = false;
            Assert.IsTrue(_root.Playing);
        }

        // The weapon arts (Docs/Weapons.md 묶음 C): the art's slot beside the weapons' slot, a greatsword's charge held,
        // a hammer's slam among slimes, an orb's well, and the slot on a touch screen right above the weapons' slot.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheWeaponArtsDraw()
        {
            yield return Start();
            Playtest.Run(_root, "equip iron_hammer; spare iron_orb; hero iron_hammer 1 0; attack off");
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("arts");
            Assert.IsTrue(_root.UseWeaponArt(new Vec2(1, 0)));
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Screens.Take("arts-slam-lifted");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Screens.Take("arts-slam");
            Assert.Greater(_root.ArtsShown, 0);
            Assert.IsTrue(_root.SwapWeapons());
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(_root.UseWeaponArt(new Vec2(1, 0)), "the orb's own art is ready though the hammer's is cooling");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Screens.Take("arts-orb");
            Playtest.Run(_root, "art ready; hero iron_greatsword 1 0; attack off");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(_root.UseWeaponArt(new Vec2(1, 0)));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Screens.Take("arts-charge");
            _root.ReleaseWeaponArt(new Vec2(1, 0));
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Screens.Take("arts-cleave");
            Controls.ForceTouch(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Screens.Take("arts-touch");
            Controls.ForceTouch(false);
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
            // [잠금 풀기] stands over the door in the world (Docs/UiRework.md U6), no longer in the bag.
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("locked-door");
            Assert.AreEqual(TileType.LockedDoor, World.Map[door.X, door.Y]);
            Assert.IsTrue(_root.LocalClient.IsMyDoor(door.X, door.Y), "The client never heard the lock was its own.");
            Assert.AreEqual(GameRoot.Meeting.Door, _root.NextMeeting(), "G does not meet the locked door ahead.");
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

        // The mad general (Docs/Bosses.md 미쳐 버린 장군 모르그, 2026-11-27): kneeling before his planted sword by the demon
        // nest, rising with a roar, then mid wind-up of his order (the sword thrust up, the war drums) and of his
        // banner planted (the banner pulled from his back and held high).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheMadGeneralRisesAndGivesOrders()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "mad_general");
            Assert.IsNotNull(boss, "No mad general.");
            Assert.AreEqual(BossEntrance.Kneel, boss.Definition.Arena.Entrance, "arenas.csv gives the general his own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("general-kneeling");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the general comes in first.");
            yield return Screens.Take("entrance-general");
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsTrue(World.ForceAttack(boss, "hellfire"), "The order can be forced once he fights.");
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Screens.Take("general-order-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "flame_ring"));
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Screens.Take("general-banner-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The cave warden (Docs/Bosses.md 동굴 수호자, 2026-11-26): asleep as a heap of mossy stone in the fallen shrine,
        // waking (eyes and core lit, getting up), then mid wind-up of its slam (both fists overhead) and of its cave-in
        // (arms spread, the core blazing, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheCaveWardenWakesAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "cave_warden");
            Assert.IsNotNull(boss, "No cave warden.");
            Assert.AreEqual(BossEntrance.Awaken, boss.Definition.Arena.Entrance, "arenas.csv gives the warden its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("warden-asleep");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the warden comes in first.");
            yield return Screens.Take("entrance-warden");
            yield return new WaitForSecondsRealtime(4.5f);
            Assert.IsTrue(World.ForceAttack(boss, "slam"), "The slam can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Screens.Take("warden-slam-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "cave_in"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("warden-cave-in-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The antlion (Docs/Bosses.md 개미귀신, 2026-11-27): buried at the bottom of its pit with the jaw tips showing,
        // bursting up out of the sand, then mid wind-up of its bite (the jaws spread wide) and of its sinkholes (gone
        // under the sand, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheAntlionBurstsUpAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "antlion");
            Assert.IsNotNull(boss, "No antlion.");
            Assert.AreEqual(BossEntrance.Ambush, boss.Definition.Arena.Entrance, "arenas.csv gives the antlion its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("antlion-buried");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the antlion comes in first.");
            yield return Screens.Take("entrance-antlion");
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsTrue(World.ForceAttack(boss, "bite"), "The bite can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Screens.Take("antlion-bite-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "maw"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("antlion-maw-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The void lord (Docs/Bosses.md 공허의 군주, 2026-11-28): under the altar's floor before he wakes, rising out of a rift
        // in it, then mid wind-up of his beam (the orb held out swelling) and of his black holes (the hand raised and
        // clenching, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheVoidLordRisesOutOfARiftAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "void_lord");
            Assert.IsNotNull(boss, "No void lord.");
            Assert.AreEqual(BossEntrance.Rift, boss.Definition.Arena.Entrance, "arenas.csv gives the void lord his own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("voidlord-under");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the void lord comes in first.");
            yield return Screens.Take("entrance-voidlord");
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsTrue(World.ForceAttack(boss, "beam"), "The beam can be forced once he fights.");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Screens.Take("voidlord-beam-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "black_holes"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("voidlord-blackholes-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The tide serpent (Docs/Bosses.md 물살 뱀, 2026-11-28): under the water of its hall with only ripples showing,
        // rising out of it, then mid wind-up of its water jet (the head reared, the throat swelling) and of its spray
        // rain (rearing as high as it goes, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheTideSerpentSurfacesAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "tide_serpent");
            Assert.IsNotNull(boss, "No tide serpent.");
            Assert.AreEqual(BossEntrance.Surface, boss.Definition.Arena.Entrance, "arenas.csv gives the tide serpent its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("serpent-under");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the tide serpent comes in first.");
            yield return Screens.Take("entrance-serpent");
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsTrue(World.ForceAttack(boss, "water_jet"), "The water jet can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("serpent-jet-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "tsunami"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("serpent-tsunami-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The lava titan (Docs/Bosses.md 용암 거인, 2026-11-28): under the lava of its caldera with only bubbles
        // showing, heaving itself out, then mid wind-up of its slam (both fists overhead) and of its fire rain (risen
        // white hot, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheLavaTitanEruptsAndWindsUp()
        {
            yield return Start();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "lava_titan");
            Assert.IsNotNull(boss, "No lava titan.");
            Assert.AreEqual(BossEntrance.Erupt, boss.Definition.Arena.Entrance, "arenas.csv gives the lava titan its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("titan-under");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.8f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the lava titan comes in first.");
            yield return Screens.Take("entrance-titan");
            // Its blows are heavy: the player is kept up and in the arena while it fights, to be there to force them.
            IEnumerator Survive(float seconds)
            {
                for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                {
                    if (Player.IsDead) World.GetUp(Player.Id);
                    Player.Health = Player.Stats.MaxHealth;
                    yield return null;
                }
            }
            yield return Survive(4.5f);
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            Assert.IsTrue(World.ForceAttack(boss, "slam"), "The slam can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Screens.Take("titan-slam-windup");
            yield return Survive(2.5f);
            if (Player.IsDead) World.GetUp(Player.Id);
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            Assert.IsTrue(World.ForceAttack(boss, "fire_rain"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("titan-rain-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The crown wraith (Docs/Bosses.md 왕관의 망령, 2026-11-28): only its rusted crown lying on the chapel floor, the
        // crown rising and the body gathering under it, then mid wind-up of its bolts (both arms raised to the dark) and of
        // its brands (rising with the crown storming, the warning sign).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheCrownWraithGathersAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "crown_wraith");
            Assert.IsNotNull(boss, "No crown wraith.");
            Assert.AreEqual(BossEntrance.Manifest, boss.Definition.Arena.Entrance, "arenas.csv gives the crown wraith its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("wraith-crown");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the crown wraith comes in first.");
            yield return Screens.Take("entrance-wraith");
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsTrue(World.ForceAttack(boss, "lightning_fall"), "The bolts can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Screens.Take("wraith-bolts-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "storm_brand"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Screens.Take("wraith-brand-windup");
            Assert.IsTrue(_root.Playing);
        }

        // The crystal spider queen (Docs/Bosses.md 수정 거미 여왕, 2026-11-27): asleep in a cocoon of crystal in its nest,
        // the cocoon cracking and bursting as it wakes, then mid wind-up of its crystal cross (the crown gathering light,
        // the front legs opened like a fan) and of its eggs (the abdomen lowered and glowing).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheCrystalQueenHatchesAndWindsUp()
        {
            yield return Start();
            Toughen();
            var boss = World.Bosses.FirstOrDefault(b => b.Definition.Id == "crystal_queen");
            Assert.IsNotNull(boss, "No crystal queen.");
            Assert.AreEqual(BossEntrance.Cocoon, boss.Definition.Arena.Entrance, "arenas.csv gives the queen its own entrance.");
            World.BossesHold = true;
            Player.Position = boss.Home + new Vec2(0, -4);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(SceneSeconds);
            yield return Screens.Take("queen-cocoon");
            World.BossesHold = false;
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.AreEqual(BossState.Enter, boss.State, "Woken, the queen comes in first.");
            yield return Screens.Take("entrance-queen");
            yield return new WaitForSecondsRealtime(4.0f);
            Assert.IsTrue(World.ForceAttack(boss, "crystal_cross"), "The cross can be forced once it fights.");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Screens.Take("queen-prism-windup");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(World.ForceAttack(boss, "call_bats"));
            yield return new WaitForSecondsRealtime(0.55f);
            yield return Screens.Take("queen-brood-windup");
            Assert.IsTrue(_root.Playing);
        }

        // Ordinary monsters' attacks from monster_attacks.csv (Docs/Combat.md 몬스터의 공격 시트, 2026-11-21): a slime's
        // pounce is marked where it will land, a frost wisp rings itself (a mark the screen makes from the monster's
        // state), and both draw their pieces; after they land nothing is left.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator MonstersWarnAndStrikeFromTheSheet()
        {
            yield return Start();
            yield return ToTheWild();
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

        // The enemies' shots (E2 of Docs/EffectsRework.md, WorldView.Shots.cs): every element hung beside the player,
        // each drawn in its own picture; when they go each leaves its ring, which is gone within a quarter second; and
        // the dotted lines of an aimed volley stay within their dots and go with it.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator EnemyShotsFlyInTheirElementAndEndInARing()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            for (int e = 0; e < 9; e++)
                for (int i = 0; i < 3; i++)
                    view.StageShot(Player.Position + new Vec2(-8 + (e % 3) * 6 + i * 1.2f, 3 - (e / 3) * 2.4f), new Vec2(8, i - 1), 0.3f, e);
            view.StageShot(Player.Position + new Vec2(-3, -4), new Vec2(8, 0), 0.3f, -1);
            view.StageAim(Player.Position + new Vec2(2, -4), new Vec2(1, 0), 1.2f, 50, 5, 0.6f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(27, _root.EnemyShots, "Nine elements, three of each, drawn here; the player's is not one of them.");
            Assert.That(_root.AimDots, Is.InRange(10, 72), "Five dotted lines, within their dots.");
            yield return Screens.Take("shots-elements");
            view.EndStagedShots();
            yield return null;
            yield return null;
            Assert.AreEqual(27, _root.ShotEnds, "Each enemy's shot ends in its ring.");
            Assert.AreEqual(0, _root.EnemyShots);
            Assert.AreEqual(0, _root.AimDots);
            yield return Screens.Take("shots-ends");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0, _root.ShotEnds, "The rings are gone in a quarter of a second.");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // What the host says before it happens (0.113.0, Docs/EffectsRework.md 14절): a monster's single shot is aimed
        // as it leaves, so the host names whom it is for and the dotted line goes to them while it winds up; once the
        // shot is out the line is gone.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ASingleShotIsLinedToItsTargetBeforeItFlies()
        {
            yield return Start();
            World.MonstersHold = true;
            var wisp = Spawn("crystal_wisp", Player.Position + new Vec2(4, 1.5f));
            wisp.MaxHealth = wisp.Health = 5000;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(World.ForceAttack(wisp, "prism"));
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.Greater(_root.View.TargetAimCount, 0, "A line to the player the shot is for.");
            Assert.That(_root.AimDots, Is.InRange(1, 72));
            yield return Screens.Take("foretold-single-aim");
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(0, _root.View.TargetAimCount, "No line once the shot is out.");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // Ordinary monsters' warnings, shots and pools since the effects rework (E5 of Docs/EffectsRework.md,
        // WorldView.MonsterFx.cs): four wind up at once round the player. Each mark is a picture of its shape, the
        // fan of shots shows where it will fly, the shots fly as the enemy's shots do, the charge raises dust, and
        // what the sac and the wall of fire leave lies on the floor; all of it within the screen's pieces and lights.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator MonsterWarningsFillAsShapesAndLeavePools()
        {
            yield return Start();
            yield return ToTheWild();
            World.MonstersHold = true;
            var shooter = Spawn("crystal_shooter", Player.Position + new Vec2(-5, 2.6f));
            var spore = Spawn("spore_shooter", Player.Position + new Vec2(-5, -2.6f));
            var mammoth = Spawn("frost_mammoth", Player.Position + new Vec2(6, 3.2f));
            var caster = Spawn("demon_caster", Player.Position + new Vec2(5, -3));
            foreach (var monster in new[] { shooter, spore, mammoth, caster })
            {
                monster.MaxHealth = monster.Health = 5000;
                monster.Facing = (Player.Position - monster.Position).Normalized;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(World.ForceAttack(shooter, "spray"));
            Assert.IsTrue(World.ForceAttack(spore, "spore_sac"));
            Assert.IsTrue(World.ForceAttack(mammoth, "trample"));
            Assert.IsTrue(World.ForceAttack(caster, "fire_wall"));
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.GreaterOrEqual(_root.MonsterWarnsShown, 3, "The sac's circle, the charge's path and the wall's line are each a picture of their shape.");
            Assert.That(_root.AimDots, Is.InRange(1, 72), "The fan of crystal shots shows where each will fly.");
            Assert.LessOrEqual(_root.MonsterFxPieces, 420);
            yield return Screens.Take("mobwarn-together");
            int shots = 0, rushes = 0, pools = 0;
            for (float waited = 0; waited < 5 && (shots == 0 || rushes == 0 || pools < 2); waited += 0.05f)
            {
                shots = Mathf.Max(shots, _root.MonsterShotsDrawn);
                rushes = Mathf.Max(rushes, _root.MonsterRushesShown);
                pools = Mathf.Max(pools, _root.MonsterPoolsShown);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.Greater(shots, 0, "The crystal shots fly in their monster's own picture.");
            Assert.Greater(rushes, 0, "The charging body raises dust and lines of speed.");
            Assert.GreaterOrEqual(pools, 2, "The sac's pool and the wall's fires lie on the floor.");
            Assert.LessOrEqual(_root.SkillFxLights, 12, "The floor lights stay within the screen's twelve.");
            yield return Screens.Take("mobwarn-pools");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // The rework's blast (E3 of Docs/EffectsRework.md, FxLayers.Blast): a fireball's and a meteor's landings put
        // beside the player and held at the swelling ball, then at the smoke. Each stays within an effect's pieces and
        // lights the floor with no more than three lights of its own; both are over within 1.4 seconds.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator BlastsSwellHollowAndLightTheFloor()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            Feel.SkillFxHold = 0.12f;
            view.StageBlast("fireball", Player.Position + new Vec2(-4, 1), 1.2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_root.SkillFxPieces, Is.InRange(8, 72), "A fireball's blast: ball, ring, edge, sparks and soot.");
            Assert.That(_root.SkillFxLights, Is.InRange(1, 3), "It lights the floor with its own lights.");
            view.StageBlast("meteor", Player.Position + new Vec2(3.5f, 0.5f), 2.6f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_root.SkillFxPieces, Is.InRange(30, 144), "Two blasts, each within 72 pieces.");
            Assert.That(_root.SkillFxLights, Is.InRange(2, 6));
            yield return Screens.Take("blast-ball");
            Feel.SkillFxHold = 0.5f;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Greater(_root.SkillFxPieces, 0, "Smoke, soot and the split floor are still there.");
            yield return Screens.Take("blast-smoke");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0, _root.SkillFxAlive, "Both are over within 1.4 seconds.");
            Assert.AreEqual(0, _root.SkillFxLights);
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // The players' skills in the rework's layers (E4 of Docs/EffectsRework.md 10): six area skills laid round the
        // player as if cast there and held at the blow, then at what is left. Together they stay within the screen's
        // pieces and its twelve lights, and all are over within 1.4 seconds.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator SkillsLayerTheFloorTheEdgeAndLight()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            Feel.SkillFxHold = 0.2f;
            view.StageCast("frost_ring", Player.Position + new Vec2(-7, 2.8f), 2.55f, 0);
            view.StageCast("thunderclap", Player.Position + new Vec2(0, 3), 2.95f, 0);
            view.StageCast("fire_ring", Player.Position + new Vec2(7, 2.8f), 2.75f, 0);
            view.StageCast("whirlwind", Player.Position + new Vec2(-7, -3), 2.15f, 0);
            view.StageCast("ground_slam", Player.Position + new Vec2(0, -3.4f), 3.15f, 0);
            view.StageCast("spirit_blast", Player.Position + new Vec2(7, -3), 2.95f, 0);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(6, _root.SkillFxAlive);
            Assert.That(_root.SkillFxPieces, Is.InRange(60, 420), "Six effects, each within 72 pieces.");
            Assert.That(_root.SkillFxLights, Is.InRange(4, 12), "The elements light the floor; steel and dust do not.");
            yield return Screens.Take("skill-layers");
            Feel.SkillFxHold = 0.55f;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Greater(_root.SkillFxPieces, 0, "Frost, soot, splits and crystals breaking are still there.");
            yield return Screens.Take("skill-layers-after");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0, _root.SkillFxAlive, "All are over within 1.4 seconds.");
            Assert.AreEqual(0, _root.SkillFxLights);
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // A boss's warnings and the blow that lands in their shape (E1 of Docs/EffectsRework.md, WorldView.Hazards.cs):
        // every shape laid beside the player and held part filled, then landed. The blow stays within its pieces, the
        // floor keeps a scar for each harmful one (a charge's path leaves none) and gives them up in time.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator BossWarningsFillAndLandInTheirShape()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            var right = new Vec2(1, 0);
            view.StageWarning(HazardShape.Ring, Player.Position, right, 3.4f, 1.7f, true, 0.6f);
            view.StageWarning(HazardShape.Circle, Player.Position + new Vec2(-6.5f, 2.6f), right, 2, 0, true, 0.6f);
            view.StageWarning(HazardShape.Share, Player.Position + new Vec2(6.5f, 2.6f), right, 1.8f, 0, true, 0.6f);
            view.StageWarning(HazardShape.Cone, Player.Position + new Vec2(-8, -3.2f), right, 4, 80, true, 0.6f);
            view.StageWarning(HazardShape.Line, Player.Position + new Vec2(1.5f, -4), new Vec2(0.98f, -0.2f).Normalized, 6, 1.3f, true, 0.93f);
            view.StageWarning(HazardShape.Line, Player.Position + new Vec2(-3, 4.6f), right, 6, 1.2f, false, 0.6f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(6, _root.WarnsShown, "Ring, circle, shared circle, wedge, line and a charge's path are all drawn.");
            yield return Screens.Take("hazard-warnings");
            view.EndStagedWarnings(land: true);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(_root.BlastPieces, Is.InRange(1, 72), "A blow stays within the pieces an effect may have.");
            Assert.AreEqual(5, _root.FloorScars, "Each harmful warning scars the floor; the path does not.");
            yield return Screens.Take("hazard-blows");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("hazard-scars");
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.AreEqual(0, _root.FloorScars, "The floor's scars are gone within 1.6 seconds.");
            Assert.AreEqual(0, _root.WarnsShown);
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // The bosses' own effects (E6, Docs/EffectsRework.md 11): blows laid as the queen's, the general's, the cave
        // warden's, the antlion's and the plain bosses' land with crystal, fire, stone, sand and their element, each
        // within the pieces an effect may have.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator BossesLandBlowsOfTheirOwn()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            var right = new Vec2(1, 0);
            void Lay(string boss, string attack, HazardShape shape, float dx, float dy, float size, float width)
            {
                view.StageWarningAs(boss, attack);
                view.StageWarning(shape, Player.Position + new Vec2(dx, dy), right, size, width, true, 0.9f);
            }
            Feel.SkillFxHold = 0.14f;
            Lay("crystal_queen", "bite", HazardShape.Cone, -8, 2.6f, 3.2f, 90);
            Lay("crystal_queen", "resonance", HazardShape.Circle, -2.5f, 3.2f, 1.4f, 0);
            Lay("crystal_queen", "web_quake", HazardShape.Circle, 2, 3.2f, 2, 0);
            Lay("crystal_queen", "venom_share", HazardShape.Share, 6.8f, 2.6f, 1.8f, 0);
            Lay("crystal_queen", "crystal_cross", HazardShape.Line, -8, -3.2f, 8, 1.2f);
            yield return new WaitForSecondsRealtime(0.2f);
            view.EndStagedWarnings(land: true);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.GreaterOrEqual(_root.BossFxCount, 5, "Each of the queen's blows lays an effect of her own.");
            Assert.That(_root.BossFxPieces, Is.InRange(1, 72), "An effect stays within its pieces.");
            yield return Screens.Take("bossfx-queen");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1.3f);
            Feel.SkillFxHold = 0.14f;
            Lay("mad_general", "cleave", HazardShape.Cone, -8, 2.4f, 3.4f, 110);
            Lay("mad_general", "hellfire", HazardShape.Circle, -2.2f, 3.2f, 1.5f, 0);
            Lay("mad_general", "ember_wake", HazardShape.Ring, 6, 1, 3.6f, 1.2f);
            Lay("mad_general", "dark_cross", HazardShape.Line, -8, -3.6f, 9, 1.2f);
            Lay("cave_warden", "slam", HazardShape.Circle, 0, -3.2f, 2.2f, 0);
            Lay("antlion", "pit", HazardShape.Circle, 5, -3.4f, 2, 0);
            view.EndStagedWarnings(land: true);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.GreaterOrEqual(_root.BossFxCount, 6);
            Assert.That(_root.BossFxPieces, Is.InRange(1, 72));
            yield return Screens.Take("bossfx-general");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1.3f);
            Feel.SkillFxHold = 0.14f;
            Lay("lava_titan", "slam", HazardShape.Circle, -6, 2.8f, 2, 0);
            Lay("void_lord", "star_fall", HazardShape.Circle, -1, 3, 1.8f, 0);
            Lay("tide_serpent", "dive", HazardShape.Circle, 3.5f, 3, 1.6f, 0);
            Lay("crown_wraith", "grasp", HazardShape.Cone, -6, -3.2f, 3.2f, 80);
            Lay("crown_wraith", "lightning_fall", HazardShape.Circle, 2, -3.2f, 1.5f, 0);
            view.EndStagedWarnings(land: true);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.GreaterOrEqual(_root.BossFxCount, 5, "The plain bosses' blows land in their element.");
            yield return Screens.Take("bossfx-elements");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual(0, _root.BossFxCount, "The effects end.");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // Blows on the bodies they land on and their numbers (E7, Docs/EffectsRework.md 12): each element's own pieces
        // within the pieces a blow may have, a crowd struck at once within the screen's, and numbers off one body
        // standing side by side.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator BlowsLandInTheirElementAndNumbersStandApart()
        {
            yield return Start();
            World.MonstersHold = true;
            var view = _root.View;
            Feel.SkillFxHold = 0.08f;
            for (int i = 0; i < HitFx.Elements; i++)
            {
                view.StageHit((HitElement)i, Player.Position + new Vec2(-6.75f + i * 1.5f, 2.5f), 0, false, 0, false, false);
                view.StageHit((HitElement)i, Player.Position + new Vec2(-6.75f + i * 1.5f, -2.5f), 0, true, 0, false, false);
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(2 * HitFx.Elements, _root.HitFxCount, "Each blow lays an effect.");
            Assert.That(_root.HitFxPieces, Is.InRange(1, HitFx.Most), "A blow stays within its pieces.");
            yield return Screens.Take("hits-elements");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(0, _root.HitFxCount, "The effects end.");
            Feel.SkillFxHold = 0.2f;
            var body = Player.Position + new Vec2(-3, 1.5f);
            view.StageHit(HitElement.Plain, body, 0, false, 12, false, false);
            view.StageHit(HitElement.Fire, body, 0, false, 5, true, false);
            view.StageHit(HitElement.Plain, body, 0, true, 31, false, false);
            view.StageHit(HitElement.Cold, body, 0, false, 4, true, false);
            view.StageHit(HitElement.Bolt, body, 0, false, 3, true, false);
            view.StageHit(HitElement.Plain, Player.Position + new Vec2(3, 1.5f), 180, false, 9, false, true);
            for (int i = 0; i < 24; i++) view.StageHit(HitElement.Fire, Player.Position + new Vec2(-5.5f + i % 8 * 1.5f, -1.5f - i / 8 * 1.2f), 0, false, 7 + i, false, false);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(5, _root.NumberSlots, "Five numbers off one body take five places.");
            Assert.That(_root.HitFxAll, Is.InRange(1, 420), "A crowd struck at once stays within the screen's pieces.");
            yield return Screens.Take("hits-numbers");
            Feel.SkillFxHold = -1;
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(0, _root.NumbersShown, "The numbers end.");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // A pack notices together (2026-11-22, Docs/WorldRework.md 무리): five slimes as one pack, most further off than
        // they see; one notices the player and the rest come too, each with a mark over its head.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator APackNoticesTogetherWithMarks()
        {
            yield return Start();
            yield return ToTheWild();
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
            yield return ToTheWild();
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
            yield return ToTheWild();
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

        // The things to find in the rooms (2026-11-22, Docs/WorldRework.md 묶음 3a·3b): a chest opened by its button's
        // path (GameRoot.UsePoi), a shrine's blessing on the status line, a camp rested at, and a shrine's pillar of light
        // seen from twelve tiles off. The solo game's world is one of rooms, so its things are there.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheThingsToFindDrawAndGive()
        {
            yield return Start();
            World.MonstersHold = true;
            Assert.IsNotEmpty(World.Pois, "No things to find: pois.csv is not in GameSheets (Game > Set Up Main Scene), or the world is of caves.");
            Poi Nearest(PoiKind kind) => World.Pois.Where(p => p.Kind == kind && World.CanUse(p, Player) && Beside(p) != null)
                .OrderBy(p => Vec2.Distance(p.Center, Player.Position)).First();
            void StandBy(Poi poi)
            {
                Player.Position = Beside(poi).Value;
                Player.Corrections++;
            }

            // Not a trap room's: that one shuts the room first (TrapsShutTheRoomAndTheDirectorSendsAmbushes).
            var chest = World.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role != RoomRole.Trap && World.CanUse(p, Player) && Beside(p) != null)
                .OrderBy(p => Vec2.Distance(p.Center, Player.Position)).First();
            StandBy(chest);
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("poi-chest");
            Assert.IsTrue(_root.UsePoi(), "The chest within reach opens.");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(World.Opened(chest.Index));
            Assert.AreEqual(TileType.OpenChest, World.Map[chest.X, chest.Y]);
            yield return Screens.Take("poi-chest-open");

            var shrine = Nearest(PoiKind.Shrine);
            StandBy(shrine);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(_root.UsePoi(), "The shrine within reach blesses.");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(Player.Blessing);
            yield return Screens.Take("poi-shrine-blessed");

            var camp = World.Pois.Where(p => p.Kind == PoiKind.Camp && Beside(p) != null).OrderBy(p => Vec2.Distance(p.Center, Player.Position)).First();
            StandBy(camp);
            yield return new WaitForSecondsRealtime(World.RestSeconds + 1);
            Assert.Greater(Player.RestedTicks, 0, "Rested by the fire.");
            yield return Screens.Take("poi-camp");

            // A shrine not yet prayed at, from twelve tiles off: its pillar of light over the dark.
            var far = Nearest(PoiKind.Shrine);
            var away = (World.Spawn - far.Center).LengthSquared > 0.01f ? (World.Spawn - far.Center).Normalized : new Vec2(-1, 0);
            for (float reach = 12; reach > 6; reach -= 0.5f)
            {
                var at = far.Center + away * reach;
                if (Game.Core.Collision.Overlaps(World.Map, at, World.PlayerRadius)) continue;
                Player.Position = at;
                Player.Corrections++;
                break;
            }
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("poi-signal-far");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // Getting about (2026-11-22, Docs/WorldRework.md 묶음 3c): a line's broken end mended and its cart ridden (the
        // cart under the player, the rails running by), a waystone with its blue flame and the waypoint list it opens,
        // and the wandering merchant's stall by a camp with its shop open. The solo game's world is one of rooms.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator MinecartsWaystonesAndTheWanderingMerchant()
        {
            yield return Start();
            World.MonstersHold = true;
            Assert.IsNotEmpty(World.Lines, "No minecart lines: pois.csv has no station row, or the world is of caves.");
            void StandAt(Vec2 at)
            {
                Player.Position = at;
                Player.Corrections++;
            }

            var line = World.Lines.OrderBy(l => Vec2.Distance(World.Pois[l.StationB].Center, Player.Position)).First();
            var broken = World.Pois[line.StationB];
            StandAt(Beside(broken) ?? broken.Center);
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("travel-station-broken");
            Assert.IsFalse(_root.UsePoi() && World.RailFixed(line.Index), "Without iron bars the line stays broken.");
            Player.Inventory.AddMaterial(Material("iron_bar"), 5);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(_root.UsePoi(), "The broken end is mended with five iron bars.");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(World.RailFixed(line.Index));
            Assert.IsTrue(_root.UsePoi(), "The cart is ridden.");
            yield return new WaitForSecondsRealtime(2);
            Assert.IsTrue(Player.Riding);
            Assert.IsTrue(_root.LocalClient.Riding, "The screen knows the player rides.");
            yield return Screens.Take("travel-riding");
            yield return new WaitForSecondsRealtime(line.Length / 16f);
            Assert.IsFalse(Player.Riding, "Off at the other end.");
            Assert.Less(Vec2.Distance(Player.Position, World.Pois[line.StationA].Center), 2f);

            var stone = World.Stones.OrderBy(s => Vec2.Distance(s.Center, Player.Position)).First();
            StandAt(Beside(stone) ?? stone.Center);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsTrue(World.OpenStones(Player).Contains(stone.Index), "Touched, the stone opens.");
            yield return Screens.Take("travel-waystone");
            Assert.IsTrue(_root.OpenWaypoints(), "The waypoint list opens by an open stone.");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("travel-waypoints");

            var stall = World.WandererAt ?? throw new AssertionException("No wandering merchant: npcs.csv has no row with place camps.");
            StandAt(Game.Core.Collision.Resolve(World.Map, TileMap.Center(stall.X, stall.Y - 1), World.PlayerRadius));
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("travel-wanderer");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // Rumours instead of arrows and slow rest (2026-11-22, Docs/WorldRework.md 묶음 4c): the village child tells of
        // something to find with a line and a faint ring on the map; past the first chapter the story's goal line says
        // only which way and in which biome; the old arrows come back with the menu's choice. Rest heals slowly unless
        // the host chose otherwise.
        [UnityTest]
        public IEnumerator RumoursAndSlowRest()
        {
            bool rumors = Settings.Rumors, quick = Settings.QuickRest;
            Settings.Rumors = true;
            Settings.QuickRest = false;
            yield return Start();
            World.MonstersHold = true;
            Assert.IsFalse(World.QuickRest, "Slow rest unless the menu chose quick.");
            PlaceSite.Still = true;
            var mina = World.Places.SelectMany(p => p.PeopleAt(World.Clock, World.DayMinutes)).FirstOrDefault(p => p.Character.Id == "mina");
            Assert.IsNotNull(mina.Character, "No village child: characters.csv has no mina.");
            Player.Position = mina.At + new Vec2(0, -1);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.TalkNear(), "The child is in reach.");
            Assert.IsNotEmpty(_root.LastRumor ?? "", "A rumour of a chest, a cart or the wandering merchant: " + _root.TalkText);
            Assert.IsNotEmpty(_root.Map.Heard, "The rumour is kept for the map.");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Screens.Take("guide-rumor-talk");

            // Past the first chapter, the hero's hut is a rumour on the map and in the goal list.
            World.RestoreJourney(Player, 4, true, null);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue(_root.HudLines.Any(l => l.Contains("어딘가") || l.Contains("쪽")), "The goal list says only which way: " + string.Join(" / ", _root.HudLines));
            yield return Screens.Take("guide-rumor-tracker");
            _root.Map.FullOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("guide-rumor-map");
            _root.Map.FullOpen = false;
            Settings.Rumors = false;
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.HudLines.Any(l => l.Contains("칸")), "With arrows, the distance again: " + string.Join(" / ", _root.HudLines));
            yield return Screens.Take("guide-arrow-tracker");

            World.MonstersHold = false;
            PlaceSite.Still = false;
            Settings.Rumors = rumors;
            Settings.QuickRest = quick;
            Assert.IsTrue(_root.Playing);
        }

        // A trap room and the pacing director (2026-11-22, Docs/WorldRework.md 묶음 4b): the trap chest opened by its button's
        // path shuts the room with grates (on the screen too) and the first band comes; won, the grates lift and the
        // chest is open; then an ambush's dust in a narrow way and the band out of the walls; then a raid's dot on the
        // screen's edge and the band reaching the player (B5c).
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TrapsShutTheRoomAndTheDirectorSendsAmbushes()
        {
            yield return Start();
            World.MonstersHold = true;
            Player.Experience = Leveling.TotalFor(6);
            Player.Health = Player.Stats.MaxHealth;
            var chest = World.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap && World.CanUse(p, Player) && Beside(p) != null)
                .OrderBy(p => Vec2.Distance(p.Center, Player.Position)).FirstOrDefault();
            Assert.IsNotNull(chest, "No trap chest: rooms.csv has no trap chunk with a chest, or the world is of caves.");
            Player.Position = Beside(chest).Value;
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(1);
            yield return Screens.Take("trap-before");
            Assert.IsTrue(_root.UsePoi(), "The trap chest within reach springs the trap.");
            yield return new WaitForSecondsRealtime(0.6f);
            var trap = World.TrapAt(chest.Index);
            Assert.IsNotNull(trap);
            Assert.IsTrue(trap.Active);
            Assert.IsTrue(trap.Doors.All(d => _root.LocalClient.Map[d.X, d.Y] == TileType.Grate), "The screen has the grates.");
            yield return Screens.Take("trap-shut");
            yield return new WaitForSecondsRealtime(World.TrapFirstSeconds + 0.3f);
            Assert.AreEqual(TrapPhase.Fighting, trap.Phase);
            yield return Screens.Take("trap-wave");
            for (int wave = trap.Wave; wave <= World.TrapWaves; wave++)
            {
                foreach (var enemy in World.Enemies.Where(e => e.PackId == trap.Band?.Id)) enemy.Health = 0;
                yield return new WaitForSecondsRealtime(World.TrapBetweenSeconds + 0.6f);
            }
            Assert.AreEqual(TrapPhase.Won, trap.Phase);
            Assert.IsTrue(World.Opened(chest.Index));
            yield return Screens.Take("trap-won");

            // An ambush in the nearest narrow way the director acts in.
            var rooms = _root.HostGenerated.Rooms;
            bool sprung = false;
            foreach (var edge in rooms.Edges.Where(e => e.Kind == RoomEdgeKind.Way && e.Width <= World.AmbushWidth && e.Path != null && e.Path.Count >= 24)
                .OrderBy(e => Vec2.Distance(TileMap.Center(e.Path[e.Path.Count / 2].X, e.Path[e.Path.Count / 2].Y), Player.Position)))
            {
                var (x, y) = edge.Path[edge.Path.Count / 2];
                Player.Position = TileMap.Center(x, y);
                if (Game.Core.Collision.Overlaps(World.Map, Player.Position, World.PlayerRadius) || World.LeftAlone(Player)) continue;
                Player.Corrections++;
                if (!World.Ambush(Player)) continue;
                sprung = true;
                break;
            }
            Assert.IsTrue(sprung, "No narrow way for an ambush.");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Screens.Take("ambush-dust");
            yield return new WaitForSecondsRealtime(World.AmbushWarn + 0.3f);
            Assert.GreaterOrEqual(World.Enemies.Count(e => e.TargetId == Player.Id && e.State == EnemyState.Chase), World.AmbushMin);
            yield return Screens.Take("ambush");

            // A raid (B5c): its howl and its dot on the screen's edge while it is out of sight (the monsters held), then
            // the band running in and reaching the player about World.RaidRunSeconds after the howl.
            foreach (var enemy in World.Enemies) enemy.Health = 0;
            yield return new WaitForSecondsRealtime(0.3f);
            bool raided = false;
            foreach (var room in rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Plain && n.Radius >= 6)
                .OrderBy(n => Vec2.Distance(n.Center, Player.Position)).Take(12))
            {
                Player.Position = TileMap.Center(room.AnchorX, room.AnchorY);
                if (Game.Core.Collision.Overlaps(World.Map, Player.Position, World.PlayerRadius) || World.LeftAlone(Player)) continue;
                Player.Corrections++;
                if (!World.Raid(Player)) continue;
                raided = true;
                break;
            }
            Assert.IsTrue(raided, "No room with room for a raid out of sight.");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.GreaterOrEqual(_root.RaidSigns.Shown + _root.RaidSigns.OnScreen, 1, "The raid's dot on the screen's edge, or the raid on the screen.");
            yield return Screens.Take("raid-dot");
            int reached = World.PaceOf(Player).RaidReaches.Count;
            World.MonstersHold = false;
            for (float waited = 0; waited < World.RaidRunSeconds + 3 && World.PaceOf(Player).RaidReaches.Count == reached; waited += 0.1f)
            {
                Player.Health = Player.Stats.MaxHealth;
                yield return new WaitForSecondsRealtime(0.1f);
            }
            World.MonstersHold = true;
            Assert.Greater(World.PaceOf(Player).RaidReaches.Count, reached, "The raid reached the player.");
            Assert.LessOrEqual(World.PaceOf(Player).RaidReaches[reached], 6.5f, "Within the design's 4~6 seconds of its howl.");
            yield return Screens.Take("raid");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // A dungeon (2026-11-22, Docs/WorldRework.md 묶음 5a): the gate room's button, going in (the bar filling, then the dark
        // and the dungeon's name in a band), the dungeon alone on the full map, a fall and getting up at its entrance, and
        // out again by the door of its entrance room. The solo game's world is one of rooms.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ADungeonGateLeadsIntoThePocketAndOut()
        {
            yield return Start();
            World.MonstersHold = true;
            Assert.IsNotEmpty(World.Gates, "No dungeon gate: rooms.csv has no gate chunk, or the world is of caves.");
            var gate = World.Gates.OrderBy(g => Vec2.Distance(g.Center, Player.Position)).First();
            Player.Position = Game.Core.Collision.Resolve(World.Map, gate.Center, World.PlayerRadius);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(1);
            Assert.IsNotNull(_root.LocalClient.GateNear(), "The screen knows the gate is in reach.");
            yield return Screens.Take("dungeon-gate");
            Assert.IsTrue(_root.UseDungeon(), "The gate's button starts going in.");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsNotNull(_root.LocalClient.DungeonOf(_root.LocalClient.GateNear()), "The screen laid the dungeon while gathering.");
            yield return Screens.Take("dungeon-entering");
            yield return new WaitForSecondsRealtime(World.EnterSeconds);
            var dungeon = World.DungeonAt(Player.Position);
            Assert.IsNotNull(dungeon, "In the dungeon.");
            Assert.AreEqual(dungeon.Slot, _root.LocalClient.DungeonHere?.Slot, "The screen is in it too.");
            Assert.IsTrue(_root.DungeonCardShowing, "Its name card shows.");
            yield return Screens.Take("dungeon-inside");
            _root.Map.FullOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("dungeon-map");
            _root.Map.FullOpen = false;

            var last = dungeon.Layout.Rooms[dungeon.Layout.Rooms.Count - 1];
            Player.Position = TileMap.Center(last.Left + last.W / 2, last.Bottom + last.H / 2);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(0.8f);
            World.Hurt(Player, Player.Health + 100000);
            yield return new WaitForSecondsRealtime(new WorldConfig().RespawnSeconds + 1);
            Assert.IsFalse(Player.IsDead);
            Assert.Less(Vec2.Distance(Player.Position, dungeon.Layout.Entrance), 1.5f, "Up again at the dungeon's entrance.");
            yield return Screens.Take("dungeon-rose");

            var door = dungeon.Layout.Exits[0];
            Player.Position = TileMap.Center(door.X, door.Y);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(_root.LocalClient.ExitNear(), "The screen knows the door out is in reach.");
            yield return Screens.Take("dungeon-door");
            Assert.IsTrue(_root.UseDungeon(), "The door's button starts going out.");
            yield return new WaitForSecondsRealtime(World.LeaveSeconds + 0.6f);
            Assert.IsFalse(World.InPocket(Player.Position), "Out at the gate.");
            Assert.Less(Vec2.Distance(Player.Position, gate.Center), 1.5f);
            yield return Screens.Take("dungeon-out");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // A run through a dungeon of dungeons.csv (Docs/WorldRework.md 묶음 5b): a pack room with its pack standing, the map
        // inside with the rooms' marks, the boss room (its arena's inlay, the master's name and bar), the end chest by its
        // button and the clear card. The monsters are held for the pictures and felled by hand.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ADungeonRunGoesToItsMasterAndItsEndChest()
        {
            yield return Start();
            World.MonstersHold = true;
            var gate = World.Gates.OrderBy(g => Vec2.Distance(g.Center, Player.Position)).First();
            Player.Position = Game.Core.Collision.Resolve(World.Map, gate.Center, World.PlayerRadius);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(_root.UseDungeon(), "The gate's button starts going in.");
            yield return new WaitForSecondsRealtime(World.EnterSeconds + 0.6f);
            var dungeon = World.DungeonAt(Player.Position);
            Assert.IsNotNull(dungeon?.Definition, "In a dungeon of dungeons.csv.");
            var layout = dungeon.Layout;

            IEnumerator GoTo(DungeonRoom room)
            {
                var from = room.Parent >= 0 ? layout.Nodes[room.Parent].Center : room.Center;
                var side = from - room.Center;
                side = Mathf.Abs(side.X) > Mathf.Abs(side.Y) ? new Vec2(Mathf.Sign(side.X), 0) : new Vec2(0, Mathf.Sign(side.Y));
                Player.Position = Game.Core.Collision.Resolve(World.Map, room.Center + new Vec2(side.X * (room.Box.W / 2f - 1.5f), side.Y * (room.Box.H / 2f - 1.5f)), World.PlayerRadius);
                Player.Corrections++;
                yield return new WaitForSecondsRealtime(1.2f);
            }
            void Fell(DungeonRoom room)
            {
                foreach (var enemy in World.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 6)).ToList()) enemy.Health = 0;
            }

            var first = layout.Room('C');
            yield return GoTo(first);
            Assert.GreaterOrEqual(World.Enemies.Count(e => !e.IsDead && first.Contains(e.Position, 2)), World.DungeonPackLeast, "The pack room's pack came.");
            yield return Screens.Take("dungeon-pack");
            _root.Map.FullOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("dungeon-run-map");
            _root.Map.FullOpen = false;
            foreach (var room in layout.Nodes.Where(n => DungeonSheet.HasPack(n.Letter) && n.Letter != 'B'))
            {
                yield return GoTo(room);
                Fell(room);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            var boss = layout.Room('B');
            yield return GoTo(boss);
            Player.Health = Player.Stats.MaxHealth;
            Assert.IsTrue(World.Enemies.Any(e => e.Elite == EliteRank.Leader && boss.Contains(e.Position, 2)), "The master in its room.");
            yield return Screens.Take("dungeon-boss");
            Fell(boss);
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.LocalClient.DungeonHere.Has(DungeonFlags.BossDown), "The screen knows the master fell.");

            var chest = layout.Things.First(t => t.End);
            Player.Position = Game.Core.Collision.Resolve(World.Map, chest.Center + new Vec2(0, -1.3f), World.PlayerRadius);
            Player.Corrections++;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(chest.Index, _root.LocalClient.DungeonThingNear()?.Index, "The end chest's button shows.");
            yield return Screens.Take("dungeon-chest");
            Assert.IsTrue(_root.UseDungeon(), "Its button opens it.");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(DungeonState.Done, _root.LocalClient.DungeonHere.State);
            Assert.AreEqual(TileType.OpenChest, _root.LocalClient.Map[chest.X, chest.Y]);
            yield return Screens.Take("dungeon-clear");
            World.MonstersHold = false;
            Assert.IsTrue(_root.Playing);
        }

        // The standing layer (묶음 B0, Docs/BiomeRework.md 4-3·4-4): the pen of "scene depth" joined at its corners, T's
        // and cross, with a gate across its bottom run and one in its east run; behind the mushroom tree and the cottage
        // their cap and roof fade (and stay solid with [물체 뒤 흐리게] off); just behind a fence run it is in front of
        // the player, just in front of one behind; standing in the east gate opens it.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheStandingLayerJoinsSortsAndFades()
        {
            yield return Start();
            bool fade = Settings.FadeBehind;
            Settings.FadeBehind = true;
            var (px, py) = Near(0, 0);
            Playtest.Run(_root, "scene depth");
            yield return new WaitForSecondsRealtime(1);
            var view = _root.View;
            void Shape(int dx, int dy, string kind, int shape, string what)
            {
                var stand = view.StandAt(px + dx, py + dy);
                Assert.AreEqual((kind, shape), (stand.Kind, stand.Shape), what);
            }
            Shape(-6, 2, "Joined", 2 | 4, "the north-west corner joins east and south");
            Shape(-3, 2, "Joined", 2 | 4 | 8, "a T on the north run");
            Shape(-3, -1, "Joined", 15, "the cross in the middle");
            Shape(-6, -4, "Joined", 1 | 2, "the south-west corner runs on into the gate");
            Shape(-5, -4, "Gate", 1, "a shut gate across the south run");
            Shape(0, 1, "Joined", 1 | 4, "the east run runs on into its gate");
            Assert.Greater(view.PropsDrawn, 40, "the pen's pieces, the tree and the cottage stand");
            yield return Screens.Take("depth-pen");

            Playtest.Run(_root, "depth at tree");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Less(view.FadeOf(px + 3, py + 2), 0.5f, "behind the mushroom tree its cap fades");
            yield return Screens.Take("depth-tree");
            Playtest.Run(_root, "depth at house");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Less(view.FadeOf(px + 2, py - 6), 0.5f, "behind the cottage its roof fades");
            Assert.AreEqual(1, view.FadeOf(px + 3, py + 2), 0.01f, "the tree's cap is solid again once the player has gone");
            yield return Screens.Take("depth-house");
            Settings.FadeBehind = false;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1, view.FadeOf(px + 2, py - 6), 0.01f, "with the setting off the roof stays solid");
            Settings.FadeBehind = fade;

            Playtest.Run(_root, "depth at behind");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Less(view.SelfOrder, view.StandAt(px - 4, py + 2).Order, "just behind the north run the fence is in front of the player");
            Playtest.Run(_root, "depth at front");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Greater(view.SelfOrder, view.StandAt(px - 4, py - 4).Order, "just in front of the south run the player is in front of it");
            Playtest.Run(_root, "depth at gate");
            yield return new WaitForSecondsRealtime(0.6f);
            Shape(0, 0, "Gate", 2, "the east gate opens while the player stands in it");
            yield return Screens.Take("depth-gate");
            Assert.IsTrue(_root.Playing);
        }

        // The biomes' looks (묶음 B1a, Docs/BiomeRework.md 4-1·4-3): the dirt cave's set of "scene biomes" stands as things
        // apart from the floor (each prop drawn by the standing layer, a lit lamp among them), the biome's sound bed
        // plays, critters and motes come within their caps, and a swing lights the lamp that is out.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheDirtCaveStandsItsPropsAndALampIsLit()
        {
            yield return Start();
            // In a room of the dirt cave, as the web check (sweep.js biomes): the solo game starts inside the dawn village,
            // whose bed is the hearth's (the local run of 2026-11-27: "Hearth", not "DirtCave").
            Playtest.Run(_root, "monsters hold; clear; goto room pack cave 1");
            yield return new WaitForSecondsRealtime(1.5f);
            var (px, py) = Near(0, 0);
            Playtest.Run(_root, "scene biomes");
            yield return new WaitForSecondsRealtime(1.5f);
            var view = _root.View;
            int props = 0;
            for (int dx = -8; dx <= 8; dx++)
            {
                var type = World.Map[px + dx, py + 3];
                if (!BiomeProps.Is(type)) continue;
                props++;
                Assert.AreEqual("Prop", view.StandAt(px + dx, py + 3).Kind, $"{type} stands apart from the floor");
            }
            Assert.GreaterOrEqual(props, 7, "one of each of the dirt cave's props");
            Assert.AreEqual(TileType.LitLamp, World.Map[px - 4, py]);
            Assert.AreEqual(1, view.StandAt(px - 5, py - 3).Shape, "the places' tree is a mine timber in the dirt cave");
            yield return new WaitForSecondsRealtime(4);
            Assert.That(view.CrittersAlive, Is.InRange(1, WorldView.CritterCap), "critters within the cap");
            Assert.That(view.MotesDrawn, Is.InRange(1, WorldView.MoteCap), "motes within the cap");
            Assert.AreEqual("DirtCave", Music.AmbienceNow);
            yield return Screens.Take("biomes-cave");

            Playtest.Run(_root, "biomes at lamp");
            yield return new WaitForSecondsRealtime(0.3f);
            int lit = Count(TileType.LitLamp);
            Playtest.Run(_root, "attack on 0 1");
            yield return new WaitForSecondsRealtime(1);
            Playtest.Run(_root, "attack off");
            Assert.AreEqual(lit + 1, Count(TileType.LitLamp), "a swing lights the lamp");
            Assert.IsTrue(TileLight.Lit(World.Map, _root.LocalClient.Tiles, _root.HostPlayer.Position, 6));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screens.Take("biomes-cave-lamp");
            Assert.IsTrue(_root.Playing);

            int Count(TileType type)
            {
                int count = 0;
                for (int dy = -6; dy <= 6; dy++)
                    for (int dx = -9; dx <= 9; dx++)
                        if (World.Map[px + dx, py + dy] == type) count++;
                return count;
            }
        }

        // The places' looks and the landmarks (묶음 B1c, Docs/BiomeRework.md): in the dirt cave the set of "scene places"
        // draws the cottage, the well and the rest in the mining camp's look, the miner's hut and the drift's frame stand
        // as things apart from the floor, and the cottage's roof fades with the player behind it.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator ThePlacesTakeTheDirtCavesLookAndItsLandmarksStand()
        {
            yield return Start();
            var (px, py) = Near(0, 0);
            Playtest.Run(_root, "monsters hold; scene places");
            yield return new WaitForSecondsRealtime(1.5f);
            var view = _root.View;
            Assert.AreEqual(Biome.Cave, _root.LocalClient.Biomes[px, py], "the spawn is in the dirt cave");
            foreach (var (dx, dy, type) in new[] { (-9, 3, TileType.Cottage), (-4, 3, TileType.Cabin), (1, 3, TileType.Well), (3, 3, TileType.LampPost), (3, 5, TileType.Crates) })
            {
                Assert.AreEqual(type, World.Map[px + dx, py + dy]);
                var stand = view.StandAt(px + dx, py + dy);
                Assert.AreEqual("Prop", stand.Kind, $"{type} stands apart from the floor");
                Assert.AreEqual(PropArt.CaveLook, stand.Shape, $"{type} in the dirt cave's look");
            }
            Assert.AreEqual(TileType.MinerHut, World.Map[px - 9, py - 5]);
            Assert.AreEqual("Prop", view.StandAt(px - 9, py - 5).Kind, "the miner's hut stands");
            Assert.AreEqual("Prop", view.StandAt(px + 3, py - 3).Kind, "the drift's frame stands");
            Assert.AreEqual("Joined", view.StandAt(px - 3, py - 4).Kind, "the yard's fence joins");
            yield return Screens.Take("places-cave");
            Playtest.Run(_root, "places at behind");
            yield return new WaitForSecondsRealtime(0.6f);
            if (Settings.FadeBehind) Assert.Less(view.FadeOf(px - 9, py + 3), 0.9f, "the cottage's roof fades with the player behind it");
            yield return Screens.Take("places-cave-behind");
            Assert.IsTrue(_root.Playing);
        }

        // The ice cave's and the sand cave's looks (묶음 B2a): in a room of each, the biome's own props stand apart from the
        // floor, the biome has its light colour, and the places' things take its look with its landmarks beside them.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheIceAndSandCavesStandTheirPropsAndThePlacesTakeTheirLooks()
        {
            yield return Start();
            foreach (var (biome, room, look, mark) in new[] { (Biome.Ice, "ice", PropArt.IceLook, TileType.FrozenBeast), (Biome.Sand, "sand", PropArt.SandLook, TileType.GiantSkull) })
            {
                Playtest.Run(_root, $"monsters hold; clear; goto room pack {room} 1");
                yield return new WaitForSecondsRealtime(1.5f);
                Playtest.Run(_root, "scene biomes");
                yield return new WaitForSecondsRealtime(1.5f);
                var view = _root.View;
                int px = TileMap.Cell(_root.LocalClient.Position.X), py = TileMap.Cell(_root.LocalClient.Position.Y);
                Assert.AreEqual(biome, _root.LocalClient.Biomes[px, py], $"a room of the {biome}");
                Assert.Greater(WorldLights.BiomeTint(biome).A, 0, $"the {biome} has its light colour");
                int col = -7;
                foreach (var kind in IceSandProps.Of(biome))
                {
                    Assert.AreEqual(kind.Type, World.Map[px + col, py + 3]);
                    Assert.AreEqual("Prop", view.StandAt(px + col, py + 3).Kind, $"{kind.Type} stands apart from the floor");
                    col += Structures.Footprint(kind.Type).W + 1;
                }
                yield return Screens.Take($"biomes-{room}");
                yield return PlacesInARoomOfItsOwn(room, look);
                px = TileMap.Cell(_root.LocalClient.Position.X);
                py = TileMap.Cell(_root.LocalClient.Position.Y);
                foreach (var (dx, dy, type) in PlaceThings)
                {
                    var stand = view.StandAt(px + dx, py + dy);
                    Assert.AreEqual("Prop", stand.Kind, $"{type} stands apart from the floor");
                    Assert.AreEqual(look, stand.Shape, $"{type} in the {biome}'s look");
                }
                bool found = false;
                for (int dy = -9; dy <= 0 && !found; dy++)
                    for (int dx = -10; dx <= 10 && !found; dx++)
                        found = World.Map[px + dx, py + dy] == mark && view.StandAt(px + dx, py + dy).Kind == "Prop";
                Assert.IsTrue(found, $"the {mark} stands");
                yield return Screens.Take($"places-{room}");
                Playtest.Run(_root, "clear");
            }
            Assert.IsTrue(_root.Playing);
        }

        // The crystal cave's and the underground lake's looks (묶음 B3a): in a room of each, the biome's own props stand
        // apart from the floor (the lake's islet in a pool laid for it), the biome has its light colour, and the places'
        // things take its look with its landmarks beside them; the crystal arch's span is walked under.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheCrystalCaveAndTheLakeStandTheirPropsAndThePlacesTakeTheirLooks()
        {
            yield return Start();
            foreach (var (biome, room, look, mark) in new[] { (Biome.Crystal, "crystal", PropArt.CrystalLook, TileType.GiantGeode), (Biome.Lake, "lake", PropArt.LakeLook, TileType.SunkenBoat) })
            {
                Playtest.Run(_root, $"monsters hold; clear; goto room pack {room} 1");
                yield return new WaitForSecondsRealtime(1.5f);
                Playtest.Run(_root, "scene biomes");
                yield return new WaitForSecondsRealtime(1.5f);
                var view = _root.View;
                int px = TileMap.Cell(_root.LocalClient.Position.X), py = TileMap.Cell(_root.LocalClient.Position.Y);
                Assert.AreEqual(biome, _root.LocalClient.Biomes[px, py], $"a room of the {biome}");
                Assert.Greater(WorldLights.BiomeTint(biome).A, 0, $"the {biome} has its light colour");
                int col = -7;
                foreach (var kind in CrystalLakeProps.Any(biome))
                {
                    if (kind.Type == TileType.MossIsle)
                    {
                        Assert.AreEqual(TileType.MossIsle, World.Map[px + 4, py - 4]);
                        Assert.AreEqual("Prop", view.StandAt(px + 4, py - 4).Kind, "the islet stands in its pool");
                        continue;
                    }
                    Assert.AreEqual(kind.Type, World.Map[px + col, py + 3]);
                    Assert.AreEqual("Prop", view.StandAt(px + col, py + 3).Kind, $"{kind.Type} stands apart from the floor");
                    col += Structures.Footprint(kind.Type).W + 1;
                }
                yield return Screens.Take($"biomes-{room}");
                yield return PlacesInARoomOfItsOwn(room, look);
                px = TileMap.Cell(_root.LocalClient.Position.X);
                py = TileMap.Cell(_root.LocalClient.Position.Y);
                foreach (var (dx, dy, type) in PlaceThings)
                {
                    var stand = view.StandAt(px + dx, py + dy);
                    Assert.AreEqual("Prop", stand.Kind, $"{type} stands apart from the floor");
                    Assert.AreEqual(look, stand.Shape, $"{type} in the {biome}'s look");
                }
                bool found = false;
                for (int dy = -9; dy <= 0 && !found; dy++)
                    for (int dx = -10; dx <= 10 && !found; dx++)
                        found = World.Map[px + dx, py + dy] == mark && view.StandAt(px + dx, py + dy).Kind == "Prop";
                Assert.IsTrue(found, $"the {mark} stands");
                if (biome == Biome.Crystal)
                {
                    Assert.AreEqual(TileType.CrystalArch, World.Map[px + 5, py - 6]);
                    Assert.IsFalse(World.Map.IsSolid(px + 5, py - 6), "the arch's span is walked under");
                }
                yield return Screens.Take($"places-{room}");
                Playtest.Run(_root, "clear");
            }
            Assert.IsTrue(_root.Playing);
        }

        // The lava zone's and the void abyss's looks (묶음 B4a): in a room of each, the biome's own props stand apart from
        // the floor (the lava zone's rock in a pool of lava laid for it), the biome has its light colour, and the places'
        // things take its look with its landmarks beside them.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheLavaZoneAndTheAbyssStandTheirPropsAndThePlacesTakeTheirLooks()
        {
            yield return Start();
            foreach (var (biome, room, look, mark) in new[] { (Biome.Lava, "lava", PropArt.LavaLook, TileType.GiantAnvil), (Biome.Abyss, "abyss", PropArt.AbyssLook, TileType.StarAltar) })
            {
                Playtest.Run(_root, $"monsters hold; clear; goto room pack {room} 1");
                yield return new WaitForSecondsRealtime(1.5f);
                Playtest.Run(_root, "scene biomes");
                yield return new WaitForSecondsRealtime(1.5f);
                var view = _root.View;
                int px = TileMap.Cell(_root.LocalClient.Position.X), py = TileMap.Cell(_root.LocalClient.Position.Y);
                Assert.AreEqual(biome, _root.LocalClient.Biomes[px, py], $"a room of the {biome}");
                Assert.Greater(WorldLights.BiomeTint(biome).A, 0, $"the {biome} has its light colour");
                int col = -7;
                foreach (var kind in LavaVoidProps.Any(biome))
                {
                    if (kind.Type == TileType.LavaRock)
                    {
                        Assert.AreEqual(TileType.LavaRock, World.Map[px + 4, py - 4]);
                        Assert.AreEqual("Prop", view.StandAt(px + 4, py - 4).Kind, "the rock stands in its lava");
                        continue;
                    }
                    Assert.AreEqual(kind.Type, World.Map[px + col, py + 3]);
                    Assert.AreEqual("Prop", view.StandAt(px + col, py + 3).Kind, $"{kind.Type} stands apart from the floor");
                    col += Structures.Footprint(kind.Type).W + 1;
                }
                yield return Screens.Take($"biomes-{room}");
                yield return PlacesInARoomOfItsOwn(room, look);
                px = TileMap.Cell(_root.LocalClient.Position.X);
                py = TileMap.Cell(_root.LocalClient.Position.Y);
                foreach (var (dx, dy, type) in PlaceThings)
                {
                    var stand = view.StandAt(px + dx, py + dy);
                    Assert.AreEqual("Prop", stand.Kind, $"{type} stands apart from the floor");
                    Assert.AreEqual(look, stand.Shape, $"{type} in the {biome}'s look");
                }
                bool found = false;
                for (int dy = -9; dy <= 0 && !found; dy++)
                    for (int dx = -10; dx <= 10 && !found; dx++)
                        found = World.Map[px + dx, py + dy] == mark && view.StandAt(px + dx, py + dy).Kind == "Prop";
                Assert.IsTrue(found, $"the {mark} stands");
                yield return Screens.Take($"places-{room}");
                Playtest.Run(_root, "clear");
            }
            Assert.IsTrue(_root.Playing);
        }

        // Open floor within reach of a thing to find, or null.
        Vec2? Beside(Poi poi)
        {
            for (float reach = 1; reach <= World.PoiReach - 0.3f; reach += 0.25f)
                for (int k = 0; k < 32; k++)
                {
                    var at = poi.Center + Vec2.FromAngle(2 * Mathf.PI * k / 32) * reach;
                    if (!Game.Core.Collision.Overlaps(World.Map, at, World.PlayerRadius)) return at;
                }
            return null;
        }
    }
}
