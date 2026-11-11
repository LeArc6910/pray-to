using System;
using System.Collections.Generic;
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
    // taken without playing up to it, and they read back what the screen shows (its buttons, the HUD's lines), so
    // the harness can press the real buttons with real clicks and taps. Changing the world only in a solo game:
    // never in one other people are in.
    //
    // Anywhere:
    //   solo [name] [keep]        start a solo game; a fresh world and character unless "keep"
    //   probe on|off              keep the drawn buttons (Ui.LastDrawn) for "buttons"
    //   buttons                   JSON: every button drawn last frame, with its rect in points; the skill and quick slots
    //   texts                     JSON: the HUD's lines, the corner lines, the banner, the menu's status line, the labels laid out with Ui.Wrap
    //   touch on|off              report (JSON: the player, the time, the monsters, the machines, the screen)
    // In a solo game:
    //   scene combo|machines|defense|rift|skylight|set
    //   clock <day> <hour>        weather clear|rain|storm|fog      speed <host ticks a step, 0 pauses>
    //   give <material> <count>   potion <consumable> <count>   drink <consumable>   mana <amount>   heal   hurt <amount>
    //   equip <base> ...          clear (the monsters nearby fall)   xp <amount>   learn <skill> (a point, as the button would)
    //   teach [rank]              every skill of the player's classes at the rank, no points needed
    //   classes <id,id> [order]   other classes at once, no workbench needed; order: which of their skills in each slot
    //   hearts <person> <n>       that many hearts with the person (merchant, blacksmith, fisher)
    //   spawn <monster> <dx> <dy> [health]                         boss <id> hurt <fraction of its health> | use <attack>
    //   drop item <base> [rarity] [sockets] | rune | potion <id> | material <id>   (at the player's feet)
    //   goto spawn | <dx> <dy> | boss <id> | biome <name> | tile <type> | near <type>
    //   build <type> <dx> <dy> [direction 0 east 1 north 2 west 3 south]   fill <dx> <dy> <material> <count>
    //   look <dx> <dy>            JSON: the tile, its machine, chest or crop
    //   inventory                 JSON: the bag
    //   attack on|off [x y]       face <x> <y> | face off   skill <slot>   bag on|off|<tab name>
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

        // A browser's console cuts a line off at about 8 KB, so a long answer (a long recipe list's buttons) goes in
        // pieces: the first on a "[playtest] " line, the rest each on a "[playtest]+" line that the harness joins on.
        const int Piece = 1500;

        static void Log(string text)
        {
            Debug.Log("[playtest] " + text.Substring(0, Math.Min(Piece, text.Length)));
            for (int at = Piece; at < text.Length; at += Piece) Debug.Log("[playtest]+" + text.Substring(at, Math.Min(Piece, text.Length - at)));
        }

        static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);

        static float Float(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        static string Do(GameRoot root, string[] words)
        {
            switch (words[0])
            {
                case "solo":
                    if (root.Playing) return "error solo: already playing";
                    string name = words.Length > 1 ? words[1] : Name;
                    if (!words.Contains("keep"))
                    {
                        SaveStore.Delete(SaveStore.WorldKey(name));
                        SaveStore.Delete(SaveStore.CharacterKey(name));
                    }
                    root.PlayerName = name;
                    root.WorldNameField = name;
                    root.ForgetCharacterClasses();
                    root.StartSolo();
                    return null;
                case "touch":
                    Controls.ForceTouch(words[1] == "on");
                    return null;
                case "probe":
                    Ui.Probing = words[1] == "on";
                    return null;
                case "buttons":
                    return "buttons " + Buttons(root);
                case "texts":
                    return "texts " + Texts(root);
                case "report":
                    return "report " + Report(root);
            }
            if (!root.Playing || !root.IsSolo || root.HostWorld == null) return $"error {words[0]}: not in a solo game";

            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            switch (words[0])
            {
                case "scene":
                    Scene(root, words[1]);
                    return null;
                case "clock":
                    // A day starts at six in the morning; an hour before six is that day's night.
                    float hour = Float(words[2]);
                    float into = ((hour - Calendar.MorningHour) % 24 + 24) % 24;
                    world.RestoreClock((long)((Int(words[1]) - 1 + into / 24) * Calendar.TicksPerDay));
                    return null;
                case "feel":
                    // feel <piece|all> <on|off>: one piece of a blow's feedback (Feel, Docs/Feel.md).
                    if (!Feel.Set(words[1], words.Length < 3 || words[2] != "off")) return $"no feel '{words[1]}' (all, {string.Join(", ", Feel.Names)})";
                    return $"ok feel off: {Feel.Off}";
                case "daylength":
                    // daylength <minutes>: how long a day of this world takes (Calendar.DayLengths).
                    world.SetDayMinutes(Int(words[1]));
                    return $"ok daylength {world.DayMinutes}";
                case "weather":
                    var weather = (Weather)Enum.Parse(typeof(Weather), words[1], ignoreCase: true);
                    int day = Enumerable.Range(2, 400).First(d => World.WeatherOf(d) == weather);
                    world.RestoreClock((long)((day - 1 + 7 / 24f) * Calendar.TicksPerDay));
                    return $"ok weather {weather} on day {day}";
                case "speed":
                    root.HostTicksPerStep = Mathf.Clamp(Int(words[1]), 0, 60);
                    return null;
                case "lifexp":
                    // lifexp <farming|fishing|mining|cooking> <experience>: sets a life skill's experience.
                    var lifeSkill = (LifeSkill)Enum.Parse(typeof(LifeSkill), words[1], ignoreCase: true);
                    player.LifeExperience[(int)lifeSkill] = Int(words[2]);
                    player.LifeVersion++;
                    return null;
                case "experience":
                    // Experience as if from monsters: the player's and, with a pet, the pet's (World.GainExperience).
                    world.GainExperience(player, Int(words[1]));
                    return null;
                case "give":
                    player.Inventory.AddMaterial(loot.FindMaterial(words[1]) ?? throw new ArgumentException($"no material '{words[1]}'"), Int(words[2]));
                    player.InventoryVersion++;
                    return null;
                case "potion":
                    player.Inventory.AddConsumable(Consumable(loot, words[1]), Int(words[2]));
                    player.InventoryVersion++;
                    return null;
                case "mana":
                    player.Mana = Float(words[1]);
                    return null;
                case "heal":
                    player.Health = player.Stats.MaxHealth;
                    player.Mana = player.Stats.MaxMana;
                    return null;
                case "hurt":
                    world.Hurt(player, Float(words[1]));
                    return null;
                case "xp":
                    world.GainExperience(player, Int(words[1]));
                    return null;
                case "learn":
                    var skill = root.ClassSheet.Skills.FirstOrDefault(k => k.Id == words[1]) ?? throw new ArgumentException($"no skill '{words[1]}'");
                    return world.LearnSkill(player.Id, skill) ? $"ok learn {skill.Id} {player.RankOf(skill)}" : $"ok learn refused {skill.Id}";
                case "classes":
                    var chosen = ClassRules.Choose(root.ClassSheet, words[1].Split(','), ClassRules.DefaultMaxClasses);
                    if (chosen.Count == 0) throw new ArgumentException($"no classes '{words[1]}'");
                    player.ChangeKit(chosen, words.Length > 2 ? words[2].Split(',').Select(Int).ToList() : null);
                    // Ready at once, as for a character that joined with them (a change in the game starts new skills
                    // cooling down; the checks give classes right after starting and use the skills straight away).
                    foreach (var ready in player.Skills) player.ClearCooldown(ready);
                    player.InventoryVersion++;
                    return null;
                // The people of the story hold still at home (on) or go about their day again (off), PlaceSite.Still.
                case "still":
                    PlaceSite.Still = words.Length < 2 || words[1] != "off";
                    return null;
                // The main story (Docs/Story.md): jump to a step (from 1), its giver not yet talked to.
                case "journey":
                    world.RestoreJourney(player, Int(words[1]) - 1, false, null);
                    return null;
                case "hearts":
                    var person = world.Npcs?.Find(words[1]) ?? throw new ArgumentException($"no person '{words[1]}'");
                    World.FriendshipOf(player, person).Points = Math.Clamp(Int(words[2]), 0, World.MaxHearts) * World.PointsPerHeart;
                    player.InventoryVersion++;
                    return null;
                case "teach":
                    World.TeachAll(player, words.Length > 1 ? Int(words[1]) : 1);
                    return null;
                case "clear":
                    foreach (var monster in world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 30)) monster.Health = 0;
                    return null;
                case "equip":
                    // Each base given at its best plain rolls, worn at once whatever its level.
                    foreach (string id in words.Skip(1)) player.Equip(Plain(loot, id, Rarity.Rare, 0));
                    return null;
                case "drink":
                    return world.UseConsumable(player.Id, Consumable(loot, words[1])) ? null : $"error drink {words[1]}: refused";
                case "spawn":
                    var enemy = world.SpawnEnemy(root.MonsterSheet.Find(words[1]) ?? throw new ArgumentException($"no monster '{words[1]}'"),
                        player.Position + new Vec2(Float(words[2]), Float(words[3])));
                    if (words.Length > 4) enemy.MaxHealth = enemy.Health = Float(words[4]);
                    return null;
                case "boss":
                    var boss = world.Bosses.FirstOrDefault(b => b.Definition.Id == words[1]) ?? throw new ArgumentException($"no boss '{words[1]}' standing");
                    if (words[2] == "hurt") world.Hurt(boss, Math.Max(1, (int)(boss.MaxHealth * Float(words[3]))));
                    else if (words[2] == "use" && !world.ForceAttack(boss, words[3])) throw new ArgumentException($"'{words[1]}' cannot use '{words[3]}' now (awake, someone in the arena?)");
                    return null;
                case "drop":
                    Drop(root, words);
                    return null;
                case "goto":
                    return Goto(root, words);
                case "build":
                    var type = Tile(words[1]);
                    int bx = px + Int(words[2]), by = py + Int(words[3]);
                    world.Build(bx, by, type);
                    if (words.Length > 4)
                    {
                        var machine = world.MachineAt(bx, by);
                        if (machine != null) machine.Direction = Int(words[4]) & 3;
                    }
                    return null;
                case "fill":
                    int fx = px + Int(words[1]), fy = py + Int(words[2]);
                    var material = loot.FindMaterial(words[3]) ?? throw new ArgumentException($"no material '{words[3]}'");
                    int count = Int(words[4]);
                    var chest = world.ChestAt(fx, fy);
                    if (chest != null) chest.AddMaterial(material, count);
                    else
                    {
                        var machine = world.MachineAt(fx, fy) ?? throw new ArgumentException($"nothing to fill at {fx},{fy}");
                        machine.Input[material] = machine.Input.TryGetValue(material, out int had) ? had + count : count;
                    }
                    return null;
                case "look":
                    return "look " + Look(root, px + Int(words[1]), py + Int(words[2]));
                case "inventory":
                    return "inventory " + Inventory(player, root.LocalClient);
                case "attack":
                    root.HoldAttack = words[1] == "on";
                    if (words.Length > 3) root.HeldAim = new Vec2(Float(words[2]), Float(words[3]));
                    return null;
                case "face":
                    root.AimHeld = words[1] != "off";
                    if (root.AimHeld) root.HeldAim = new Vec2(Float(words[1]), Float(words[2]));
                    return null;
                case "skill":
                    return root.UseSkill(Int(words[1])) ? null : $"error skill {words[1]}: not ready";
                case "bag":
                    if (words[1] == "on" || words[1] == "off") root.BagOpen = words[1] == "on";
                    else root.ShowBag(words[1]);
                    return null;
                default:
                    return $"error {words[0]}: no such command";
            }
        }

        static TileType Tile(string name) => (TileType)Enum.Parse(typeof(TileType), name, ignoreCase: true);

        static ConsumableDefinition Consumable(LootData loot, string id) =>
            loot.Consumables.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"no consumable '{id}'");

        static Item Plain(LootData loot, string id, Rarity rarity, int sockets)
        {
            var itemBase = loot.FindBase(id) ?? throw new ArgumentException($"no item base '{id}'");
            var implicits = itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, r.Max, r.Tags)).ToList();
            return new Item(itemBase, itemBase.Level, rarity, implicits, Array.Empty<ItemAffix>(), sockets);
        }

        // Things on the ground a step ahead of the player, to walk onto or be picked up.
        static void Drop(GameRoot root, string[] words)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            var at = player.Position + new Vec2(0.6f, 0);
            switch (words[1])
            {
                case "item":
                    var rarity = words.Length > 3 ? (Rarity)Enum.Parse(typeof(Rarity), words[3], ignoreCase: true) : Rarity.Rare;
                    world.DropItem(Plain(loot, words[2], rarity, words.Length > 4 ? Int(words[4]) : 0), at);
                    break;
                case "rune":
                    world.DropRune(new LootGenerator(loot).RollRune(1, new GameRandom(5)), at);
                    break;
                case "potion":
                    world.DropConsumable(Consumable(loot, words[2]), at);
                    break;
                case "material":
                    world.DropMaterial(loot.FindMaterial(words[2]) ?? throw new ArgumentException($"no material '{words[2]}'"), at);
                    break;
                default:
                    throw new ArgumentException($"no drop '{words[1]}'");
            }
        }

        static bool Walkable(TileMap map, int x, int y) =>
            map.InBounds(x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Water && map[x, y] != TileType.Lava;

        // Moves the player, as recalling does. To a boss's arena (beside it, so it wakes), into a biome (the open
        // floor in it nearest the spawn), onto the nearest tile of a type, or beside it facing it ("near").
        static string Goto(GameRoot root, string[] words)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var map = world.Map;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            Vec2 to;
            switch (words[1])
            {
                case "spawn":
                    to = world.Spawn;
                    break;
                // A place of the story (Docs/Story.md), just below its waypoint; or beside one of its people.
                case "place":
                    var site = world.Places.FirstOrDefault(p => p.Definition.Id == words[2]) ?? throw new ArgumentException($"no place '{words[2]}'");
                    to = TileMap.Center(site.X, site.Y - 2);
                    break;
                // Beside the first signpost of the roads (GeneratedWorld.Signposts), on the road side.
                case "signpost":
                    var signs = root.LocalClient?.Signposts;
                    if (signs == null || signs.Count == 0) throw new InvalidOperationException("no road signposts");
                    var (signX, signY, _) = signs[0];
                    var road = new[] { (0, -1), (1, 0), (-1, 0), (0, 1) }.FirstOrDefault(s => map.InBounds(signX + s.Item1, signY + s.Item2) && Structures.IsWay(map[signX + s.Item1, signY + s.Item2]));
                    to = TileMap.Center(signX + road.Item1, signY + road.Item2);
                    break;
                case "person":
                    var standing = world.Places.SelectMany(p => p.PeopleAt(world.Clock, world.DayMinutes)).FirstOrDefault(p => p.Character.Id == words[2]);
                    if (standing.Character == null) throw new ArgumentException($"no person '{words[2]}' in the story");
                    to = standing.At + new Vec2(0, -1);
                    break;
                case "boss":
                    var boss = world.Bosses.FirstOrDefault(b => b.Definition.Id == words[2]) ?? throw new ArgumentException($"no boss '{words[2]}' standing");
                    to = Nearest(map, TileMap.Cell(boss.Home.X), TileMap.Cell(boss.Home.Y) - 4, (x, y) => Walkable(map, x, y))
                        ?? throw new InvalidOperationException("no floor in the arena");
                    break;
                case "biome":
                    var biome = (Biome)Enum.Parse(typeof(Biome), words[2], ignoreCase: true);
                    // Open floor with open floor all round, so the player can move and things can be built there.
                    to = Nearest(map, TileMap.Cell(world.Spawn.X), TileMap.Cell(world.Spawn.Y),
                        (x, y) => world.Biomes[x, y] == biome && Enumerable.Range(-1, 3).All(dx => Enumerable.Range(-1, 3).All(dy => Walkable(map, x + dx, y + dy))))
                        ?? throw new InvalidOperationException($"no open floor in {biome}");
                    break;
                case "tile":
                    var onto = Tile(words[2]);
                    to = Nearest(map, px, py, (x, y) => map[x, y] == onto) ?? throw new InvalidOperationException($"no {onto} tile");
                    break;
                // The nearest monster (2026-11-10, the first-hour checks' hunts: near the spawn there are few, and the bot
                // cannot see further than the screen): open floor three or four tiles from it.
                case "monster":
                    var foe = world.Enemies.Where(e => e.Health > 0).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault()
                        ?? throw new InvalidOperationException("no monster");
                    int fx = TileMap.Cell(foe.Position.X), fy = TileMap.Cell(foe.Position.Y);
                    to = Nearest(map, fx, fy, (x, y) => Walkable(map, x, y) && Math.Abs(x - fx) + Math.Abs(y - fy) >= 3) ?? foe.Position;
                    break;
                case "near":
                    var type = Tile(words[2]);
                    // The nearest one there is room to stand beside (an ore vein can be shut in rock).
                    var target = Nearest(map, px, py, (x, y) => map[x, y] == type
                            && new[] { (0, -1), (1, 0), (-1, 0), (0, 1) }.Any(s => Walkable(map, x + s.Item1, y + s.Item2)))
                        ?? throw new InvalidOperationException($"no {type} tile with room beside it");
                    int tx = TileMap.Cell(target.X), ty = TileMap.Cell(target.Y);
                    var beside = new[] { (0, -1), (1, 0), (-1, 0), (0, 1) }.Where(s => Walkable(map, tx + s.Item1, ty + s.Item2)).ToList();
                    if (beside.Count == 0) throw new InvalidOperationException($"nowhere to stand beside {type}");
                    to = TileMap.Center(tx + beside[0].Item1, ty + beside[0].Item2);
                    root.AimHeld = true;
                    root.HeldAim = new Vec2(-beside[0].Item1, -beside[0].Item2);
                    break;
                default:
                    to = TileMap.Center(px + Int(words[1]), py + Int(words[2]));
                    break;
            }
            player.Position = to;
            player.Corrections++;
            return $"ok goto {words[1]} at {TileMap.Cell(to.X)},{TileMap.Cell(to.Y)}";
        }

        // The tile nearest (x, y), by rings outward, that passes the test.
        static Vec2? Nearest(TileMap map, int x, int y, Func<int, int, bool> test)
        {
            int reach = Math.Max(map.Width, map.Height);
            for (int ring = 0; ring < reach; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        int cx = x + dx, cy = y + dy;
                        if (map.InBounds(cx, cy) && test(cx, cy)) return TileMap.Center(cx, cy);
                    }
                }
            }
            return null;
        }

        // The play mode scenes (FeatureTests), laid around the player.
        static void Scene(GameRoot root, string scene)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            (int X, int Y) Near(int dx, int dy) => (px + dx, py + dy);
            void Lay(params (int X, int Y, TileType Type)[] tiles)
            {
                foreach (var (x, y, type) in tiles) world.Build(x, y, type);
            }
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

        // A small JSON writer for the answers.
        sealed class Json
        {
            readonly StringBuilder _text = new StringBuilder();
            bool _first = true;

            public Json(char open)
            {
                _text.Append(open);
            }

            public static string Quote(string s) =>
                "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\u00A0", " ") + "\"";

            static string Value(object value) => value switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                string s => Quote(s),
                float f => f.ToString("0.##", CultureInfo.InvariantCulture),
                double d => d.ToString("0.##", CultureInfo.InvariantCulture),
                RawJson raw => raw.Text,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };

            public Json Field(string name, object value)
            {
                if (!_first) _text.Append(',');
                _first = false;
                _text.Append(Quote(name)).Append(':').Append(Value(value));
                return this;
            }

            public Json Item(object value)
            {
                if (!_first) _text.Append(',');
                _first = false;
                _text.Append(Value(value));
                return this;
            }

            public string Close(char close) => _text.Append(close).ToString();

            public RawJson Raw(char close) => new RawJson(Close(close));
        }

        sealed class RawJson
        {
            public RawJson(string text)
            {
                Text = text;
            }

            public string Text { get; }
        }

        static RawJson Rect(Rect rect) =>
            new Json('[').Item(rect.x).Item(rect.y).Item(rect.width).Item(rect.height).Raw(']');

        static RawJson Strings(IEnumerable<string> lines)
        {
            var list = new Json('[');
            foreach (string line in lines) list.Item(line);
            return list.Raw(']');
        }

        static RawJson Counts<T>(IEnumerable<KeyValuePair<T, int>> pairs, Func<T, string> id)
        {
            var map = new Json('{');
            foreach (var pair in pairs) map.Field(id(pair.Key), pair.Value);
            return map.Raw('}');
        }

        // Every button the last frame drew: its label, its rect in points, whether it could be pressed and whether
        // it showed. The skill and quick slots (drawn as panels, pressed through Controls) come as "skill N",
        // "potion" and "quick N". The screen's size in points comes along, to turn points into the page's pixels.
        static string Buttons(GameRoot root)
        {
            var list = new Json('[');
            foreach (var drawn in Ui.LastDrawn)
            {
                list.Item(new Json('{').Field("label", drawn.Label).Field("row", drawn.Row.Replace('\n', ' ')).Field("rect", Rect(drawn.Rect)).Field("enabled", drawn.Enabled).Field("shown", drawn.Shown).Raw('}'));
            }
            if (root.Playing && !root.BagOpen && !root.MapOpen)
            {
                int skills = root.SkillCount;
                for (int slot = 0; slot <= skills; slot++)
                {
                    string label = slot == skills ? "potion" : $"skill {slot}";
                    list.Item(new Json('{').Field("label", label).Field("rect", Rect(SkillBar.Area(slot, skills))).Field("enabled", true).Field("shown", true).Raw('}'));
                }
                for (int slot = 0; slot < Hotbar.Slots; slot++)
                {
                    list.Item(new Json('{').Field("label", $"quick {slot}").Field("rect", Rect(Hotbar.Area(slot, skills))).Field("enabled", true).Field("shown", true).Raw('}'));
                }
            }
            return new Json('{').Field("width", Ui.Width).Field("height", Ui.Height).Field("buttons", list.Raw(']')).Close('}');
        }

        static string Texts(GameRoot root) =>
            new Json('{')
                .Field("hud", Strings(root.HudLines.Select(l => l.Replace('\n', ' '))))
                .Field("acquired", Strings(root.AcquiredLines))
                .Field("banner", root.Banner)
                .Field("floating", Strings(root.FloatingTexts))
                .Field("status", root.Status)
                .Field("written", Strings(Ui.LastWritten))
                .Close('}');

        static string Inventory(Player player, WorldClient client)
        {
            var items = new Json('[');
            foreach (var item in player.Inventory.Items) items.Item(new Json('{').Field("id", item.Id).Field("base", item.Base.Id).Field("upgrade", item.Upgrade)
                .Field("slot", (int)item.Base.Slot).Field("level", item.Level).Raw('}'));
            var worn = new Json('{');
            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
            {
                var item = player.Equipment[slot];
                if (item != null) worn.Field(slot.ToString(), item.Base.Id);
            }
            return new Json('{')
                .Field("items", items.Raw(']'))
                .Field("worn", worn.Raw('}'))
                .Field("materials", Counts(player.Inventory.Materials, m => m.Id))
                .Field("consumables", Counts(player.Inventory.Consumables, c => c.Id))
                .Field("runes", player.Inventory.Runes.Values.Sum())
                .Field("classes", string.Join(",", player.Classes.Select(c => c.Id)))
                .Field("skills", string.Join(",", player.Skills.Select(s => s.Id)))
                .Field("ranks", string.Join(",", player.SkillRanks.OrderBy(p => p.Key.Index).Select(p => $"{p.Key.Id}:{p.Value}")))
                .Field("skillPoints", player.SkillPoints)
                .Field("supports", string.Join(",", player.Supports.SelectMany(p => p.Value.Select(gem => $"{p.Key.Id}:{gem.Id}"))))
                .Field("records", player.Records.Count)
                .Field("pet", player.Pet?.Id ?? "")
                .Field("petLevel", player.PetLevel)
                .Field("petName", client?.PetName(player.Pet) ?? "")
                .Field("achievements", string.Join(",", player.Achievements))
                .Field("passives", player.Passives.Count)
                .Field("friends", Counts(player.Friendships.Select(f => new KeyValuePair<string, int>(f.Key, f.Value.Points)), id => id))
                .Field("stories", Counts(player.StorySteps, id => id))
                .Field("life", string.Join(",", player.LifeExperience))
                .Field("perks", string.Join(",", player.LifePerks))
                .Close('}');
        }

        static string Look(GameRoot root, int x, int y)
        {
            var world = root.HostWorld;
            var json = new Json('{').Field("x", x).Field("y", y).Field("tile", world.Map[x, y].ToString()).Field("biome", world.Biomes[x, y].ToString());
            var machine = world.MachineAt(x, y);
            if (machine != null)
            {
                json.Field("machine", new Json('{')
                    .Field("kind", machine.Kind.ToString())
                    .Field("direction", machine.Direction)
                    .Field("enabled", machine.Enabled)
                    .Field("powered", world.IsPowered(machine))
                    .Field("input", Counts(machine.Inputs, m => m.Id))
                    .Field("output", Counts(machine.Outputs, m => m.Id))
                    .Field("picks", string.Join(",", machine.Settings.Select(m => m.Id)))
                    .Field("recipe", machine.Recipe?.Id)
                    .Field("affection", machine.Affection)
                    .Raw('}'));
            }
            var chest = world.ChestAt(x, y);
            if (chest != null) json.Field("chest", Counts(chest.Materials, m => m.Id));
            var crop = world.CropAt(x, y);
            if (crop != null)
            {
                json.Field("crop", new Json('{')
                    .Field("id", crop.Definition.Id)
                    .Field("growth", crop.Growth(world.Tick))
                    .Field("ripe", crop.IsRipe(world.Tick))
                    .Field("wet", crop.IsWet(world.Tick))
                    .Raw('}'));
            }
            return json.Close('}');
        }

        // What a check needs to know, as one line of JSON.
        static string Report(GameRoot root)
        {
            var json = new Json('{');
            json.Field("playing", root.Playing);
            json.Field("screen", $"{Ui.Width:0}x{Ui.Height:0}");
            json.Field("touch", Controls.UsingTouch);
            json.Field("bag", root.BagOpen);
            json.Field("target", root.TargetShown);
            json.Field("music", root.MusicPlaying);
            json.Field("doorsOpen", root.OpenDoors);
            json.Field("typing", Controls.Typing);
            json.Field("map", root.MapOpen);
            json.Field("speed", root.HostTicksPerStep);
            var world = root.HostWorld;
            var player = root.HostPlayer;
            if (world != null && player != null)
            {
                json.Field("x", player.Position.X);
                json.Field("y", player.Position.Y);
                // The main story (Docs/Story.md): the step (from 1), whether taken, the open waypoints, where the player is.
                json.Field("journey", player.JourneyStep + 1);
                json.Field("journeyTaken", player.JourneyTaken);
                json.Field("waypoints", string.Join(",", player.Waypoints.OrderBy(w => w)));
                // The step past the story's last (the end), and each side story begun: "harbor:13*" (step from 1, * taken).
                json.Field("storyEnd", (world.Story?.Steps.Count ?? 0) + 1);
                json.Field("sides", string.Join(",", player.SideSteps.Select(side => $"{side.Key}:{side.Value + 1}{(player.SideTaken.Contains(side.Key) ? "*" : "")}")
                    .Concat(player.SideTaken.Where(chain => !player.SideSteps.ContainsKey(chain)).Select(chain => $"{chain}:{(world.Story?.Chain(chain)?.First.Index ?? 0) + 1}*"))));
                json.Field("place", world.PlaceAt(player.Position)?.Definition.Id ?? "");
                json.Field("places", world.Places.Count);
                // Where the first place's people are now: "chief:12.5,30.5;mina:...".
                json.Field("people", world.Places.Count == 0 ? "" : string.Join(";", world.Places[0].PeopleAt(world.Clock, world.DayMinutes)
                    .Select(p => $"{p.Character.Id}:{p.At.X:0.0},{p.At.Y:0.0}")));
                json.Field("spawnX", world.Spawn.X);
                json.Field("spawnY", world.Spawn.Y);
                var (ax, ay) = World.TileAhead(player.Position, player.Facing);
                json.Field("ahead", world.Map[ax, ay].ToString());
                json.Field("biome", world.Biomes.At(player.Position).ToString());
                json.Field("health", player.Health);
                json.Field("maxHealth", player.Stats.MaxHealth);
                json.Field("dead", player.IsDead);
                json.Field("mana", player.Mana);
                json.Field("maxMana", player.Stats.MaxMana);
                json.Field("level", player.Level);
                json.Field("experience", player.Experience);
                json.Field("attacking", player.Attacking);
                json.Field("combo", player.ComboStep);
                json.Field("weapon", player.Equipment[ItemSlot.Weapon]?.Base.Id);
                json.Field("day", world.Today);
                json.Field("clock", Calendar.Text(world.Clock));
                json.Field("clockTicks", world.Clock);
                json.Field("tick", world.Tick);
                json.Field("dayMinutes", world.DayMinutes);
                json.Field("feelOff", Feel.Off);
                json.Field("weather", world.WeatherToday.ToString());
                json.Field("event", world.EventToday.ToString());
                var line = player.Fishing;
                if (line != null)
                {
                    int fishing = LifeSkills.LevelOf(player.LifeExperience[(int)LifeSkill.Fishing]);
                    float gauge = World.GaugeAt(line.BiteElapsed, line.Difficulty);
                    json.Field("fishing", line.BiteTicks > 0 ? (World.InCatchZone(gauge, line.Difficulty, fishing) ? "bite in" : "bite out") : "wait");
                }
                var near = world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < 12).ToList();
                json.Field("monsters", near.Count);
                json.Field("monsterKinds", string.Join(",", near.Select(e => e.Definition.Id).Distinct()));
                json.Field("hurtMonsters", near.Count(e => e.Health < e.MaxHealth));
                // Where the nearest monster and the nearest awake boss are from the player, for a bot to aim at.
                var foe = near.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                json.Field("foe", foe == null ? null : $"{foe.Position.X - player.Position.X:0.00},{foe.Position.Y - player.Position.Y:0.00}");
                var awake = world.Bosses.Where(b => b.State != BossState.Dormant && !b.IsDead).OrderBy(b => Vec2.Distance(b.Position, player.Position)).FirstOrDefault();
                json.Field("bossAt", awake == null ? null : $"{awake.Position.X - player.Position.X:0.00},{awake.Position.Y - player.Position.Y:0.00}");
                // Standing in a warning on the floor: the nearest way out of every warning (a share marker is to stand
                // in), or 0,0 when there is none; null when safe.
                bool Danger(Vec2 at) => world.Hazards.Any(h => h.Shape != HazardShape.Share && h.Contains(at, World.PlayerRadius));
                if (Danger(player.Position))
                {
                    string way = "0,0";
                    foreach (float reach in new[] { 1.5f, 2.5f, 3.5f, 5f })
                    {
                        for (int k = 0; k < 8 && way == "0,0"; k++)
                        {
                            var step = new Vec2(MathF.Cos(k * MathF.PI / 4), MathF.Sin(k * MathF.PI / 4)) * reach;
                            var to = player.Position + step;
                            if (!world.Map.IsSolid(TileMap.Cell(to.X), TileMap.Cell(to.Y)) && !Danger(to)) way = $"{step.X:0.00},{step.Y:0.00}";
                        }
                        if (way != "0,0") break;
                    }
                    json.Field("danger", way);
                }
                json.Field("projectiles", world.Projectiles.Count(p => p.Source == player.Id));
                json.Field("hazards", world.Hazards.Count);
                json.Field("bossShots", world.Projectiles.Count(p => !p.Friendly));
                json.Field("loot", world.GroundLoot.Count(l => Vec2.Distance(l.Position, player.Position) < 6));
                // The nearest copper vein within 16 tiles, as the minimap marks it (MapView.Sense).
                (int X, int Y)? vein = null;
                int px0 = TileMap.Cell(player.Position.X), py0 = TileMap.Cell(player.Position.Y), veinDistance = int.MaxValue;
                for (int vy = py0 - 16; vy <= py0 + 16; vy++)
                {
                    for (int vx = px0 - 16; vx <= px0 + 16; vx++)
                    {
                        if (!world.Map.InBounds(vx, vy) || world.Map[vx, vy] != TileType.CopperOre) continue;
                        int d = (vx - px0) * (vx - px0) + (vy - py0) * (vy - py0);
                        if (d <= 256 && d < veinDistance)
                        {
                            veinDistance = d;
                            vein = (vx, vy);
                        }
                    }
                }
                json.Field("veinAt", vein is (int ox, int oy) ? $"{ox - px0},{oy - py0}" : null);
                var drop = world.GroundLoot.Where(l => Vec2.Distance(l.Position, player.Position) < 10).OrderBy(l => Vec2.Distance(l.Position, player.Position)).FirstOrDefault();
                json.Field("lootAt", drop == null ? null : $"{drop.Position.X - player.Position.X:0.00},{drop.Position.Y - player.Position.Y:0.00}");
                var machines = world.Machines.Where(m => Vec2.Distance(TileMap.Center(m.X, m.Y), player.Position) < 12).ToList();
                json.Field("machines", string.Join(" ", machines.Select(m => $"{m.Kind}{(world.IsPowered(m) ? "+" : "")}{(m.Enabled ? "" : "-off")}")));
                json.Field("bosses", string.Join(" ", world.Bosses.Select(b => $"{b.Definition.Id}:{b.Health:0}/{b.MaxHealth:0}:p{b.Phase}:{b.State}:{b.Attack?.Id ?? "-"}")));
                json.Field("defense", world.ActiveDefense == null ? null : $"wave {world.ActiveDefense.Wave} stone {world.ActiveDefense.StoneHealth:0}/{world.ActiveDefense.MaxStoneHealth:0}");
                json.Field("rift", world.ActiveRift == null ? null : $"tier {world.ActiveRift.Tier} wave {world.ActiveRift.Wave}");
            }
            return json.Close('}');
        }
    }
}
