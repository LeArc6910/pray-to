using System;
using System.Globalization;
using Calendar = Game.Core.Calendar;
using System.Linq;
using System.Text;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Remote control for the cloud's playtests of the web demo (Docs/Playtest.md, DevTools/WebPlaytest): the page
    // calls unityInstance.SendMessage("Game", "Playtest", "<commands>") with commands split by ';', and each answers
    // with a "[playtest] ..." line in the browser console, which the harness reads. They set a solo game's world up
    // the way the play mode scenes do (Assets/Tests/PlayMode/FeatureTests.cs), so a picture of any feature can be
    // taken without playing up to it. Only in a solo game: never in one other people are in.
    //
    //   solo                      start a solo game in a fresh world named "playtest"
    //   scene combo|machines|defense|rift|skylight|set
    //   clock <day> <hour>        weather clear|rain|storm|fog
    //   give <material> <count>   potion <consumable> <count>   drink <consumable>   mana <amount>
    //   spawn <monster> <dx> <dy> [health]
    //   attack on|off [x y]       skill <slot>   touch on|off   bag on|off
    //   report                    one line of JSON: the player, the time, the monsters, the machines, the screen
    static class Playtest
    {
        const string Name = "playtest";

        public static void Run(GameRoot root, string commands)
        {
            foreach (string raw in (commands ?? "").Split(';'))
            {
                string command = raw.Trim();
                if (command.Length == 0) continue;
                try
                {
                    Log(Do(root, command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) ?? "ok " + command);
                }
                catch (Exception e)
                {
                    Log($"error {command}: {e.Message}");
                }
            }
        }

        static void Log(string text) => Debug.Log("[playtest] " + text);

        static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);

        static float Float(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        static string Do(GameRoot root, string[] words)
        {
            if (words[0] == "solo")
            {
                if (root.Playing) return "error solo: already playing";
                SaveStore.Delete(SaveStore.WorldKey(Name));
                SaveStore.Delete(SaveStore.CharacterKey(Name));
                root.PlayerName = Name;
                root.WorldNameField = Name;
                root.StartSolo();
                return null;
            }
            if (words[0] == "touch")
            {
                Controls.ForceTouch(words[1] == "on");
                return null;
            }
            if (words[0] == "report") return "report " + Report(root);
            if (!root.Playing || !root.IsSolo || root.HostWorld == null) return $"error {words[0]}: not in a solo game";

            var world = root.HostWorld;
            var player = root.HostPlayer;
            switch (words[0])
            {
                case "scene":
                    Scene(root, words[1]);
                    return null;
                case "clock":
                    world.RestoreClock((long)((Int(words[1]) - 1 + Float(words[2]) / 24) * Calendar.TicksPerDay));
                    return null;
                case "weather":
                    var weather = (Weather)Enum.Parse(typeof(Weather), words[1], ignoreCase: true);
                    int day = Enumerable.Range(2, 400).First(d => World.WeatherOf(d) == weather);
                    world.RestoreClock((long)((day - 1 + 7 / 24f) * Calendar.TicksPerDay));
                    return $"ok weather {weather} on day {day}";
                case "give":
                    player.Inventory.AddMaterial(root.LootSheet.FindMaterial(words[1]), Int(words[2]));
                    player.InventoryVersion++;
                    return null;
                case "potion":
                    player.Inventory.AddConsumable(root.LootSheet.Consumables.First(c => c.Id == words[1]), Int(words[2]));
                    player.InventoryVersion++;
                    return null;
                case "mana":
                    player.Mana = Float(words[1]);
                    return null;
                case "drink":
                    return world.UseConsumable(player.Id, root.LootSheet.Consumables.First(c => c.Id == words[1])) ? null : $"error drink {words[1]}: refused";
                case "spawn":
                    var enemy = world.SpawnEnemy(root.MonsterSheet.Find(words[1]), player.Position + new Vec2(Float(words[2]), Float(words[3])));
                    if (words.Length > 4) enemy.MaxHealth = enemy.Health = Float(words[4]);
                    return null;
                case "attack":
                    root.HoldAttack = words[1] == "on";
                    if (words.Length > 3) root.HeldAim = new Vec2(Float(words[2]), Float(words[3]));
                    return null;
                case "skill":
                    return root.UseSkill(Int(words[1])) ? null : $"error skill {words[1]}: not ready";
                case "bag":
                    root.BagOpen = words[1] == "on";
                    return null;
                default:
                    return $"error {words[0]}: no such command";
            }
        }

        // The play mode scenes (FeatureTests), laid around the player.
        static void Scene(GameRoot root, string scene)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            (int X, int Y) Near(int dx, int dy) => (px + dx, py + dy);
            void Lay(params (int X, int Y, TileType Type)[] tiles) =>
                world.Restore(tiles.Select(t => new TileChange(t.X, t.Y, t.Type)), Array.Empty<(CropDefinition, int, int, long, long)>(), 0);
            var none = Array.Empty<(MaterialDefinition, int)>();
            Enemy Spawn(string monster, Vec2 at, float health)
            {
                var enemy = world.SpawnEnemy(root.MonsterSheet.Find(monster), at);
                enemy.MaxHealth = enemy.Health = health;
                return enemy;
            }

            switch (scene)
            {
                case "combo":
                    for (int i = 0; i < 5; i++) Spawn("slime", player.Position + new Vec2(1.2f, (i - 2) * 0.5f), 5000);
                    root.HoldAttack = true;
                    root.HeldAim = new Vec2(1, 0);
                    break;
                case "machines":
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
                    world.RestoreMachine(furnace.X, furnace.Y, 0, 0, null, 0, new[] { (loot.FindMaterial("iron_ore"), 8) }, none);
                    world.RestoreMachine(turret.X, turret.Y, 0, 0, null, 0, new[] { (loot.FindMaterial(World.FireAmmo), 10) }, none);
                    world.RestoreMachine(filter.X, filter.Y, 0, 0, null, 0, none, none, new[] { loot.FindMaterial("iron_ore"), loot.FindMaterial("stone") });
                    world.RestoreMachine(harvester.X, harvester.Y, 1, 0, null, 0, new[] { (loot.FindMaterial("mushroom_spore"), 3) }, none);
                    Spawn("slime", TileMap.Center(turret.X + 2, turret.Y), 5000);
                    break;
                case "defense":
                    var stone = Near(0, 1);
                    var wall = Near(0, 3);
                    Lay((stone.X, stone.Y, TileType.WardStone), (wall.X, wall.Y, TileType.Wall));
                    world.RestoreDefense(1, 0);
                    if (!world.StartDefense(player.Id)) throw new InvalidOperationException("the defense did not start");
                    break;
                case "rift":
                    var altar = Near(0, 1);
                    Lay((altar.X, altar.Y, TileType.RiftAltar));
                    player.Inventory.AddMaterial(loot.FindMaterial(World.RiftKey), 1);
                    player.InventoryVersion++;
                    if (!world.OpenRift(player.Id)) throw new InvalidOperationException("the rift did not open");
                    break;
                case "skylight":
                    var floor = Near(1, 1);
                    Lay((floor.X, floor.Y, TileType.Skylight));
                    break;
                case "set":
                    var generator = new LootGenerator(loot);
                    foreach (var piece in generator.LegendariesOf(World.RiftLegendaries).Where(b => b.Set != null))
                        player.Equip(generator.RollLegendary(piece, new GameRandom(3)));
                    break;
                default:
                    throw new ArgumentException($"no scene '{scene}'");
            }
        }

        // What a check needs to know, as one line of JSON.
        static string Report(GameRoot root)
        {
            var json = new StringBuilder("{");
            void Field(string name, object value)
            {
                if (json.Length > 1) json.Append(',');
                json.Append('"').Append(name).Append("\":");
                switch (value)
                {
                    case null:
                        json.Append("null");
                        break;
                    case bool b:
                        json.Append(b ? "true" : "false");
                        break;
                    case string s:
                        json.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                        break;
                    case float f:
                        json.Append(f.ToString("0.##", CultureInfo.InvariantCulture));
                        break;
                    default:
                        json.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                        break;
                }
            }
            Field("playing", root.Playing);
            Field("screen", $"{Ui.Width:0}x{Ui.Height:0}");
            Field("touch", Controls.UsingTouch);
            Field("bag", root.BagOpen);
            var world = root.HostWorld;
            var player = root.HostPlayer;
            if (world != null && player != null)
            {
                Field("x", player.Position.X);
                Field("y", player.Position.Y);
                Field("health", player.Health);
                Field("maxHealth", player.Stats.MaxHealth);
                Field("mana", player.Mana);
                Field("maxMana", player.Stats.MaxMana);
                Field("level", player.Level);
                Field("combo", player.ComboStep);
                Field("weapon", player.Equipment[ItemSlot.Weapon]?.Base.Id);
                Field("day", world.Today);
                Field("clock", Calendar.Text(world.Clock));
                Field("weather", world.WeatherToday.ToString());
                Field("event", world.EventToday.ToString());
                var near = world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < 12).ToList();
                Field("monsters", near.Count);
                Field("hurtMonsters", near.Count(e => e.Health < e.MaxHealth));
                var machines = world.Machines.Where(m => Vec2.Distance(TileMap.Center(m.X, m.Y), player.Position) < 12).ToList();
                Field("machines", string.Join(" ", machines.Select(m => $"{m.Kind}{(world.IsPowered(m) ? "+" : "")}")));
                Field("defense", world.ActiveDefense == null ? null : $"wave {world.ActiveDefense.Wave} stone {world.ActiveDefense.StoneHealth:0}/{world.ActiveDefense.MaxStoneHealth:0}");
                Field("rift", world.ActiveRift == null ? null : $"tier {world.ActiveRift.Tier} wave {world.ActiveRift.Wave}");
            }
            return json.Append('}').ToString();
        }
    }
}
