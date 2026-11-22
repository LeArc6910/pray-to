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
    //   scene combo|machines|defense|rift|skylight|set|roll   (roll: a lane of floor east of the player, a wall 8 tiles on)
    //   scene ice|quicksand       a lane of that floor east of the player, a wall 12 tiles on (PlayerMotor)
    //   scene pack                five slimes east of the player as one pack (World.Gather), most of them out of its sight
    //   scene elite               a mushroom soldiers' leader with fire and guard and three slime minions east of the player,
    //                             and three fierce champion beetles west of it (World.MakeEliteHere), all held still
    //   clock [day] <hour>        weather clear|rain|storm|fog      speed <host ticks a step>
    //   give <material> <count>   potion <consumable> <count>   drink <consumable>   mana <amount>   heal   hurt <amount>
    //   equip <base> ...          clear (the monsters nearby fall)   xp <amount>   learn <skill> (a point, as the button would)
    //   teach [rank]              every skill of the player's classes at the rank, no points needed
    //   classes <id,id> [order]   other classes at once, no workbench needed; order: which of their skills in each slot
    //   hearts <person> <n>       that many hearts with the person (merchant, blacksmith, fisher)
    //   spawn <monster> <dx> <dy> [health]                         boss <id> hurt <fraction of its health> | use <attack>
    //   bosses hold|wake          (asleep whoever walks in)        boss <id> sleep | enter [again]   (its entrance, arenas.csv)
    //   drop item <base> [rarity] [sockets] | rune | potion <id> | material <id>   (at the player's feet)
    //   goto spawn | <dx> <dy> | boss <id> | arena <id> (its middle) | pool | biome <name> | tile <type> | near <type>
    //   goto room <role> [biome|any] [n]   into the nth room of the role (the generator's version 2), nearest the spawn first
    //   goto road <n> [percent]   onto the nth road of a world of rooms (GeneratedWorld.Roads), that far along it (50)
    //   goto byway <n>            onto the nth by-way (places.csv links), a few steps before its cracked wall
    //   goto station [broken|fixed] [n]   beside the nth nearest minecart station (3c), or the broken end of a line not
    //                             mended, or a station of a mended line;   goto wanderer   below the wandering merchant's stall
    //   travel                    JSON: the minecart lines (length, mended, broken end), the player's ride on host and screen,
    //                             the waystones open, where the wandering merchant stands today and how many rows they sell
    //   mend <line|all>   stones all (every waystone open to the player)   waypoints (the waypoint list, by an open one)
    //   talk                      to the person of the story in reach, as their button would: JSON of the lines and the rumour
    //   rumors                    JSON (묶음 4c): the menu's two choices, the rumours heard, how well the way to where the story
    //                             leads and to each arena is known, and the goal list's story line
    //   guidance rumor|arrow      the menu's [길 안내]     quickrest on|off   the menu's [쉴 때 회복], and the running world's
    //   director [build|peak|relax|rest [tension] | ambush | raid]   JSON: the pacing director for the player (state, tension,
    //                             clocks, what it sent, the dust and howls the screen showed); or that state set, or one now
    //   goto narrow [n]           the middle of the nth nearest narrow way (two tiles or less) where an ambush can come
    //   traps                     JSON: the nearest trap room: phase, band, wave, the grates down on host and screen, who is
    //                             shut in, the chest (opened, usable on host and screen), the banners shown
    //   rooms                     JSON: the generator's version, how many rooms of each role, chunks, cracked ways, roads
    //   packs                     JSON: the packs about the player (World.Packs.cs): each one's room, kind, size, monsters standing,
    //                             noticing and out of their own sight; the wanderers, the field's monsters and the caps
    //   goto pack                 beside the nearest pack out in the world, on the far side of it from its middle, so most
    //                             of it is out of sight (to see it notice together)
    //   goto elite                five tiles from the nearest elite out in the world, toward the player (goto room elite first)
    //   elites                    JSON: the field elites about the player (World.Elites.cs): rank, name, affixes, health, and
    //                             the name tags, the widest of them against the screen, guard lines and the warning sign drawn
    //   room <role> [biome|any] [n]   JSON: that room (where, its chunk, steps walked from the spawn; -1 for no way)
    //   build <type> <dx> <dy> [direction 0 east 1 north 2 west 3 south]   fill <dx> <dy> <material> <count>
    //   look <dx> <dy>            JSON: the tile, its machine, chest or crop
    //   inventory                 JSON: the bag
    //   attack on|off [x y]       face <x> <y> | face off   skill <slot>   bag on|off|<tab name>
    //   roll                      the dodge roll, as its key or button would (the way walked, or faced)
    //   rollhit <amount> <ticks>  a blow on the player that many host ticks into their next roll (its unhurt ticks)
    //   nav <target>   sense   botreport [name]   botreset      the bot by the Core's state (PlaytestBot.cs), with probe on
    static partial class Playtest
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

            if (words[0] == "nav" || words[0] == "sense" || words[0] == "botreport" || words[0] == "botreset") return Bot(root, words);

            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            switch (words[0])
            {
                case "scene":
                    root.Coasted = 0;
                    Scene(root, words[1]);
                    return null;
                case "clock":
                    // "clock <day> <hour>", or "clock <hour>" for today. A day starts at six in the morning; an hour before
                    // six is that day's night.
                    float hour = Float(words.Length > 2 ? words[2] : words[1]);
                    int clockDay = words.Length > 2 ? Int(words[1]) : world.Today;
                    float into = ((hour - Calendar.MorningHour) % 24 + 24) % 24;
                    world.RestoreClock((long)((clockDay - 1 + into / 24) * Calendar.TicksPerDay));
                    return null;
                case "feel":
                    // feel <piece|all> <on|off>: one piece of a blow's feedback (Feel, Docs/Feel.md).
                    if (!Feel.Set(words[1], words.Length < 3 || words[2] != "off")) return $"no feel '{words[1]}' (all, {string.Join(", ", Feel.Names)})";
                    return $"ok feel off: {Feel.Off}";
                case "fxhold":
                    // fxhold <seconds|off>: skill effects stop at that age, for a picture of one phase (Docs/SkillEffects.md).
                    Feel.SkillFxHold = words[1] == "off" ? -1 : Float(words[1]);
                    return $"ok fxhold {Feel.SkillFxHold}";
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
                    // journey <step> [taken]: at that step of the main story, taken or not yet.
                    world.RestoreJourney(player, Int(words[1]) - 1, words.Length > 2 && words[2] == "taken", null);
                    return null;
                case "guide":
                    // guide <goal id>: every guide goal before it passed (GameRoot.GuideUpTo).
                    root.GuideUpTo(words[1]);
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
                case "hero":
                    // The hero's attack with a weapon (2026-11-19, HeroArt): the weapon in hand (none: bare hands), three
                    // sturdy slimes the way given, and the attack held that way so the three steps go round.
                    if (words[1] == "none") player.Unequip(ItemSlot.Weapon);
                    else player.Equip(Plain(loot, words[1], Rarity.Rare, 0));
                    var way = words.Length > 3 ? new Vec2(Float(words[2]), Float(words[3])) : new Vec2(1, 0);
                    var ahead = way.Normalized;
                    var across = new Vec2(-ahead.Y, ahead.X);
                    foreach (var monster in world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 30)) monster.Health = 0;
                    for (int i = -1; i <= 1; i++)
                    {
                        var dummy = world.SpawnEnemy(root.MonsterSheet.Find("slime"), player.Position + ahead * 1.3f + across * (i * 0.6f));
                        dummy.MaxHealth = dummy.Health = 50000;
                    }
                    root.HoldAttack = true;
                    root.HeldAim = way;
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
                    // Its entrance (arenas.csv, 2026-11-17): asleep again, or coming in now (the whole way, or "again").
                    else if (words[2] == "sleep") world.ForceSleep(boss);
                    else if (words[2] == "enter" && !world.ForceEntrance(boss, words.Length > 3 && words[3] == "again")) throw new ArgumentException($"'{words[1]}' has no entrance (arenas.csv)");
                    return null;
                case "mob":
                {
                    // mob <id> use <attack>: the nearest monster of that kind (one is put three tiles ahead if none is
                    // near) starts that attack of monster_attacks.csv now. mob <id> kill [zone]: it falls to the
                    // player, as one of that zone would (a split comes past the first). mob <id> hurt <share>,
                    // melt (felled by fire), low <share> (its health set).
                    var kind = root.MonsterSheet.Find(words[1]);
                    var mob = world.Enemies.Where(e => !e.IsDead && e.Definition == kind && Vec2.Distance(e.Position, player.Position) < 12)
                        .OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                    if (mob == null)
                    {
                        mob = world.SpawnEnemy(kind, player.Position + (player.Facing.LengthSquared > 0.01f ? player.Facing.Normalized : new Vec2(1, 0)) * 3);
                        mob.MaxHealth = mob.Health = 5000;
                    }
                    if (words[2] == "use" && !world.ForceAttack(mob, words[3]))
                        throw new ArgumentException($"'{words[1]}' cannot use '{words[3]}' now (its attacks: {string.Join(", ", kind.Attacks.Select(a => a.Id))})");
                    else if (words[2] == "hurt") world.Hurt(mob, player, Math.Max(1, mob.MaxHealth * Float(words[3])));
                    // mob <id> face: it turns to the player (one held by "monsters hold" keeps the way it was made facing).
                    else if (words[2] == "face") mob.Facing = (player.Position - mob.Position).Normalized;
                    // mob <id> melt: it falls to fire (an ice slime then leaves no shards).
                    else if (words[2] == "melt")
                    {
                        mob.Health = 1;
                        world.Hurt(mob, player, 100000, SkillTag.Fire);
                    }
                    // mob <id> low <share>: its health is set to that share (a bomber goes off under 0.3).
                    else if (words[2] == "low") mob.Health = Math.Max(1, mob.MaxHealth * Float(words[3]));
                    else if (words[2] == "kill")
                    {
                        if (words.Length > 3) mob.ZoneLevels = Math.Max(0, Int(words[3]) - 1);
                        mob.MaxHealth = kind.Health;
                        mob.Health = 1;
                        world.Hurt(mob, player, 100000);
                    }
                    return null;
                }
                // Monsters neither notice anyone nor start anything by themselves ("hold"), so a picture shows only
                // the attack that was asked for; or go about as they do ("go").
                case "monsters":
                    world.MonstersHold = words[1] == "hold";
                    return null;
                // Bosses stay asleep whoever walks in ("hold"), to look at an arena; or wake as they do ("wake").
                case "bosses":
                    world.BossesHold = words[1] == "hold";
                    return null;
                case "drop":
                    Drop(root, words);
                    return null;
                case "goto":
                    return Goto(root, words);
                case "rooms":
                    return "rooms " + Rooms(root);
                case "packs":
                    return "packs " + PacksJson(root);
                case "elites":
                    return "elites " + ElitesJson(root);
                // The things to find in the rooms (2026-11-22, Docs/WorldRework.md 묶음 3a·3b).
                case "pois":
                    return "pois " + PoisJson(root);
                case "usepoi":
                    return root.UsePoi() ? "ok usepoi" : "error usepoi: nothing to use within reach";
                // The pacing director and the trap rooms (2026-11-22, 묶음 4b). "director": the player's pace; "director
                // <build|peak|relax|rest> [tension]": set as if it had come; "director ambush|raid": one now, where the player
                // stands (an ambush only in a narrow way: "goto narrow" first). "traps": the nearest trap room.
                case "director":
                    if (words.Length == 1) return "director " + DirectorJson(root);
                    if (words[1] == "ambush") return world.Ambush(player) ? "ok director ambush" : "error director ambush: not in a narrow way with room before and behind";
                    if (words[1] == "raid") return world.Raid(player) ? "ok director raid" : "error director raid: no room for a raid out of sight here";
                    var paceState = (PaceState)Enum.Parse(typeof(PaceState), words[1], ignoreCase: true);
                    world.SetPace(player, paceState, words.Length > 2 ? Float(words[2]) : paceState == PaceState.Peak ? World.PeakTension : 0);
                    return null;
                case "traps":
                    return "traps " + TrapsJson(root);
                // The minecarts, waystones and the wandering merchant (2026-11-22, 묶음 3c).
                case "travel":
                    return "travel " + TravelJson(root);
                case "mend":
                    // mend <line|all>: the line's broken end mended, as a player with five iron bars would.
                    foreach (var line in world.Lines.Where(l => words[1] == "all" || l.Index == Int(words[1]))) world.MendRail(line.Index);
                    return $"ok mend {world.FixedRails.Count()}/{world.Lines.Count}";
                case "stones":
                    // stones all: every waystone open to the player, as if they had touched each.
                    foreach (var stone in world.Stones) world.OpenStone(player, stone);
                    return $"ok stones {world.OpenStones(player).Count()}/{world.Stones.Count}";
                // Rumours instead of arrows, and slow rest (2026-11-22, 묶음 4c).
                case "talk":
                    if (!root.TalkNear()) return "error talk: nobody of the story in reach";
                    return "talk " + new Json('{').Field("text", root.TalkText ?? "").Field("rumor", root.LastRumor ?? "").Close('}');
                case "rumors":
                    return "rumors " + RumorsJson(root);
                case "guidance":
                    Settings.Rumors = words[1] != "arrow";
                    return $"ok guidance {(Settings.Rumors ? "rumor" : "arrow")}";
                case "quickrest":
                    Settings.QuickRest = world.QuickRest = words[1] == "on";
                    return $"ok quickrest {world.QuickRest}";
                case "waypoints":
                    // The list of waypoints and waystones, as the button over one opens it.
                    return root.OpenWaypoints() ? "ok waypoints" : "error waypoints: no open waypoint or waystone within reach";
                case "room":
                    return "room " + RoomJson(root, RoomOf(root, words, 1));
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
                case "roll":
                    root.RollPressed = true;
                    return null;
                case "rollhit":
                    // rollhit <amount> <ticks>: a blow on the player that many host ticks into their next roll.
                    root.ArmRollHit(Float(words[1]), Int(words[2]));
                    return null;
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
                // A room of a role (2026-11-21, Docs/WorldRework.md 방의 역할): the tile the generator keeps walked to
                // (its middle, or outside the door of the chunk on its middle).
                case "room":
                    var room = RoomOf(root, words, 2);
                    to = TileMap.Center(room.AnchorX, room.AnchorY);
                    break;
                // Beside a thing to find (2026-11-22, 묶음 3a): "poi <camp|shrine|chest|cart|treasure|hidden|locked|trap> [n]
                // [tiles]": the nth nearest that still gives (a camp always), on open floor in its reach, or that many
                // tiles off on the spawn's side (to see its signal from afar). A hidden or locked room's chest is reached
                // only once its wall is dug: "crack" and "lock" stand before the wall.
                case "poi":
                    var (thing, room0) = PoiOf(root, words);
                    float off = words.Length > 4 ? float.Parse(words[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    if (off > 0)
                    {
                        var away = (world.Spawn - thing.Center).LengthSquared > 1e-4f ? (world.Spawn - thing.Center).Normalized : new Vec2(-1, 0);
                        var far = thing.Center + away * off;
                        to = Nearest(map, TileMap.Cell(far.X), TileMap.Cell(far.Y), (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius))
                            ?? throw new InvalidOperationException("no floor that far from it");
                    }
                    else
                    {
                        to = Nearest(map, thing.X, thing.Y, (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius)
                                && Vec2.Distance(TileMap.Center(x, y), thing.Center) <= World.PoiReach - 0.3f)
                            ?? TileMap.Center(room0.AnchorX, room0.AnchorY);
                    }
                    break;
                // In a narrow way where an ambush comes (묶음 4b): "narrow [n]", the middle of the nth nearest way two tiles
                // wide or less and long enough, out of the places the director leaves alone, facing along it.
                case "narrow":
                    int nthWay = words.Length > 2 ? Int(words[2]) : 0;
                    var ways = root.HostGenerated?.Rooms ?? throw new InvalidOperationException("not a world of rooms");
                    var narrowAt = ways.Edges.Where(e => e.Kind == RoomEdgeKind.Way && e.Width <= World.AmbushWidth && e.Path != null && e.Path.Count >= 24)
                        .Select(e => (Edge: e, Middle: e.Path[e.Path.Count / 2]))
                        .Where(w => Walkable(map, w.Middle.X, w.Middle.Y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(w.Middle.X, w.Middle.Y), World.PlayerRadius))
                        .OrderBy(w => Vec2.Distance(TileMap.Center(w.Middle.X, w.Middle.Y), player.Position))
                        .Where(w =>
                        {
                            var was = player.Position;
                            player.Position = TileMap.Center(w.Middle.X, w.Middle.Y);
                            bool alone = world.LeftAlone(player);
                            player.Position = was;
                            return !alone;
                        }).Skip(nthWay).FirstOrDefault();
                    if (narrowAt.Edge == null) throw new InvalidOperationException($"no narrow way {nthWay}");
                    to = TileMap.Center(narrowAt.Middle.X, narrowAt.Middle.Y);
                    var next = narrowAt.Edge.Path[narrowAt.Edge.Path.Count / 2 + 1];
                    root.AimHeld = true;
                    root.HeldAim = new Vec2(next.X - narrowAt.Middle.X, next.Y - narrowAt.Middle.Y);
                    break;
                // Before the crack of a hidden room in the rock, or the obsidian or void wall of a locked room, facing it
                // (묶음 3b): "crack [n]", "lock [n]".
                // Beside a minecart station (3c): "station [broken|fixed] [n]": the nth nearest, or the nth nearest broken end
                // of a line not mended, or a station of a mended line; on the rails beside its cart.
                case "station":
                    string state = words.Length > 2 && (words[2] == "broken" || words[2] == "fixed") ? words[2] : "any";
                    int nthStation = words.Length > (state == "any" ? 2 : 3) ? Int(words[state == "any" ? 2 : 3]) : 0;
                    var station = world.Lines.SelectMany(l => new[] { (l, world.Pois[l.StationA]), (l, world.Pois[l.StationB]) })
                        .Where(s => state == "any" || state == "broken" && !world.RailFixed(s.l.Index) && s.Item2.Index == s.l.Broken
                            || state == "fixed" && world.RailFixed(s.l.Index))
                        .OrderBy(s => Vec2.Distance(s.Item2.Center, player.Position)).Skip(nthStation).Select(s => s.Item2).FirstOrDefault()
                        ?? throw new InvalidOperationException($"no {state} station {nthStation}");
                    to = Nearest(map, station.X, station.Y, (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius)
                            && Vec2.Distance(TileMap.Center(x, y), station.Center) is float d && d >= 0.9f && d <= World.PoiReach - 0.4f)
                        ?? station.Center;
                    break;
                // Below the wandering merchant's stall, facing it (3c).
                case "wanderer":
                    var stall = world.WandererAt ?? throw new InvalidOperationException("no wandering merchant (no camps)");
                    to = Nearest(map, stall.X, stall.Y - 1, (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius))
                        ?? throw new InvalidOperationException("nowhere to stand by the wandering merchant");
                    root.AimHeld = true;
                    root.HeldAim = new Vec2(stall.X + 0.5f - to.X, stall.Y + 0.5f - to.Y);
                    break;
                case "crack":
                case "lock":
                    int which = words.Length > 2 ? int.Parse(words[2]) : 0;
                    var generated = root.HostGenerated?.Rooms ?? throw new InvalidOperationException("not a world of rooms");
                    var walls = words[1] == "crack"
                        ? generated.Nodes.Where(n => n.Buried).Select(n => (n, Wall: Faced(map, n.AnchorX, n.AnchorY, t => t == TileType.CrackedWall)))
                        : generated.Nodes.Where(n => n.Role == RoomRole.Locked && n.Box != null).Select(n => (n, Wall: LockOf(map, n)));
                    var picked = walls.Where(w => w.Wall != null).OrderBy(w => Vec2.Distance(w.n.Center, player.Position)).Skip(which).FirstOrDefault();
                    if (picked.n == null) throw new InvalidOperationException($"no {words[1]} {which}");
                    var ((wx, wy), (sx, sy)) = picked.Wall.Value;
                    to = TileMap.Center(sx, sy);
                    root.AimHeld = true;
                    root.HeldAim = new Vec2(wx - sx, wy - sy);
                    break;
                // Beside the nearest pack out in the world (2026-11-22, Docs/WorldRework.md 묶음 2-1): three tiles
                // past the one of it furthest from the others' middle, so the rest stand further off than they see.
                case "pack":
                    var pack = world.Packs.Where(k => k.Active && world.MembersOf(k).Any()).OrderBy(k => Vec2.Distance(k.Home, player.Position)).FirstOrDefault()
                        ?? throw new InvalidOperationException("no pack out (goto room pack first)");
                    var members = world.MembersOf(pack).ToList();
                    var middle = members.Aggregate(Vec2.Zero, (sum, m) => sum + m.Position) / members.Count;
                    var edge = members.OrderByDescending(m => Vec2.Distance(m.Position, middle)).First();
                    var outward = (edge.Position - middle).LengthSquared > 1e-4f ? (edge.Position - middle).Normalized : new Vec2(1, 0);
                    int ex = TileMap.Cell(edge.Position.X + outward.X * 3), ey = TileMap.Cell(edge.Position.Y + outward.Y * 3);
                    to = Nearest(map, ex, ey, (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius))
                        ?? throw new InvalidOperationException("no floor beside the pack");
                    break;
                // Five tiles from the nearest elite out in the world, on the player's side of it (2026-11-22, 묶음 2-2).
                case "elite":
                    var elite = world.Enemies.Where(e => e.Elite != EliteRank.None && !e.IsDead).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault()
                        ?? throw new InvalidOperationException("no elite out (goto room elite first)");
                    var back = (player.Position - elite.Position).LengthSquared > 1e-4f ? (player.Position - elite.Position).Normalized : new Vec2(-1, 0);
                    int lx = TileMap.Cell(elite.Position.X + back.X * 5), ly = TileMap.Cell(elite.Position.Y + back.Y * 5);
                    to = Nearest(map, lx, ly, (x, y) => Walkable(map, x, y) && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius))
                        ?? throw new InvalidOperationException("no floor before the elite");
                    break;
                // Along a road of a world of rooms (2026-11-22, Docs/WorldRework.md 묶음 1c), or before a by-way's
                // cracked wall.
                case "road":
                case "byway":
                    var routes = root.HostGenerated?.Roads.Where(r => r.Kind == (words[1] == "road" ? RoadKind.Road : RoadKind.Link)).ToList();
                    int nth = words.Length > 2 ? int.Parse(words[2]) : 0;
                    if (routes == null || nth >= routes.Count) throw new InvalidOperationException($"no {words[1]} {nth}");
                    var route = routes[nth];
                    int at = words[1] == "road"
                        ? Math.Min(route.Tiles.Count - 1, route.Tiles.Count * (words.Length > 3 ? int.Parse(words[3]) : 50) / 100)
                        : route.Cracked is (int cx, int cy) ? Math.Max(0, route.Tiles.ToList().FindIndex(t => t.X == cx && t.Y == cy) - 4) : route.Tiles.Count / 2;
                    var (roadX, roadY) = route.Tiles[Math.Max(0, at)];
                    to = Nearest(map, roadX, roadY, (x, y) => Walkable(map, x, y)) ?? throw new InvalidOperationException("no floor by the road");
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
                // The middle of a boss's arena, to look at how it is dressed (with "bosses hold", or it wakes).
                case "arena":
                    var keeper = world.Bosses.FirstOrDefault(b => b.Definition.Id == words[2]) ?? throw new ArgumentException($"no boss '{words[2]}' standing");
                    to = Nearest(map, TileMap.Cell(keeper.Home.X), TileMap.Cell(keeper.Home.Y) - 2, (x, y) => Walkable(map, x, y))
                        ?? throw new InvalidOperationException("no floor in the arena");
                    break;
                // The middle of the nearest web or pool of venom a boss's blow left on the floor (2026-11-13).
                case "pool":
                    var pool = world.Hazards.Where(h => h.Lingers).OrderBy(h => Vec2.Distance(h.Position, player.Position)).FirstOrDefault()
                        ?? throw new InvalidOperationException("no web or pool on the floor");
                    to = pool.Position;
                    break;
                case "biome":
                    var biome = (Biome)Enum.Parse(typeof(Biome), words[2], ignoreCase: true);
                    // Open floor with open floor all round, so the player can move and things can be built there.
                    to = Nearest(map, TileMap.Cell(world.Spawn.X), TileMap.Cell(world.Spawn.Y),
                        (x, y) => world.Biomes[x, y] == biome && Enumerable.Range(-1, 3).All(dx => Enumerable.Range(-1, 3).All(dy => Walkable(map, x + dx, y + dy))))
                        ?? throw new InvalidOperationException($"no open floor in {biome}");
                    break;
                // Open floor outside the story's places, where monsters may come (World.InSafePlace keeps them out).
                case "wild":
                    to = Nearest(map, px, py, (x, y) => !world.InSafePlace(TileMap.Center(x, y))
                            && Enumerable.Range(-2, 5).All(dx => Enumerable.Range(-2, 5).All(dy => Walkable(map, x + dx, y + dy))))
                        ?? throw new InvalidOperationException("no open floor outside the places");
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

        // The nth nearest thing to find of a kind or of a room role that still gives, and its room: "poi <kind|role> [n]".
        static (Poi Poi, RoomNode Room) PoiOf(GameRoot root, string[] words)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var rooms = root.HostGenerated?.Rooms ?? throw new InvalidOperationException("not a world of rooms");
            string what = words.Length > 2 ? words[2] : "chest";
            int nth = words.Length > 3 ? int.Parse(words[3]) : 0;
            bool kind = Enum.TryParse(what, true, out PoiKind wanted);
            var role = kind ? RoomRole.Plain : (RoomRole)Enum.Parse(typeof(RoomRole), what, ignoreCase: true);
            var poi = world.Pois.Where(p => (kind ? p.Kind == wanted : rooms.Nodes[p.Room].Role == role) && world.CanUse(p, player))
                .OrderBy(p => Vec2.Distance(p.Center, player.Position)).Skip(nth).FirstOrDefault()
                ?? throw new InvalidOperationException($"no {what} {nth} that still gives");
            return (poi, rooms.Nodes[poi.Room]);
        }

        // A tile of the wanted wall beside the floor tile (x, y), and that floor tile; or null.
        static ((int X, int Y) Wall, (int X, int Y) Stand)? Faced(TileMap map, int x, int y, Func<TileType, bool> wanted)
        {
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (-1, 0), (0, -1) })
                if (map.InBounds(x + dx, y + dy) && wanted(map[x + dx, y + dy])) return ((x + dx, y + dy), (x, y));
            return null;
        }

        // An obsidian or void wall tile of a locked room and the floor before it, walked to from the room (RoomGraph.LockOf).
        static ((int X, int Y) Wall, (int X, int Y) Stand)? LockOf(TileMap map, RoomNode room) => RoomGraph.LockOf(map, room);

        // The minecarts, waystones and the wandering merchant (3c): the lines (length, mended, which end is broken), the
        // player's ride as the host and the screen see it, the waystones open, and where the merchant stands today.
        static string TravelJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var client = root.LocalClient;
            var lines = new Json('[');
            foreach (var line in world.Lines)
                lines.Item(new RawJson(new Json('{').Field("index", line.Index).Field("length", line.Length).Field("fixed", world.RailFixed(line.Index))
                    .Field("a", line.StationA).Field("b", line.StationB).Field("broken", line.Broken)
                    .Field("distance", Math.Min(Vec2.Distance(world.Pois[line.StationA].Center, player.Position), Vec2.Distance(world.Pois[line.StationB].Center, player.Position)))
                    .Close('}')));
            var wanderer = world.WandererAt;
            var npc = world.Npcs?.Npcs.FirstOrDefault(n => n.Wanders);
            return new Json('{').Field("lines", lines.Raw(']'))
                .Field("riding", player.Riding).Field("rideLine", player.RideLine).Field("rides", player.Rides)
                .Field("clientRiding", client?.Riding ?? false)
                .Field("x", player.Position.X).Field("y", player.Position.Y)
                .Field("stones", world.Stones.Count).Field("stonesOpen", world.OpenStones(player).Count())
                .Field("clientStonesOpen", client?.OpenStones.Count() ?? 0)
                .Field("stoneNear", world.StoneNear(player.Position)?.Index ?? -1)
                .Field("wanderer", wanderer != null).Field("wandererX", wanderer?.X ?? -1).Field("wandererY", wanderer?.Y ?? -1)
                .Field("wandererBiome", wanderer is (int wx, int wy) ? world.Biomes[wx, wy].ToString() : "")
                .Field("wandererShop", npc == null || client == null ? 0 : client.ShopTodayOf(npc).Count)
                .Field("inReach", world.PoiNear(player.Position, player)?.Definition.Id ?? "")
                .Close('}');
        }

        // The rumours (묶음 4c): the choices, what has been heard, how well the ways are known, the story's goal line.
        static string RumorsJson(GameRoot root)
        {
            var client = root.LocalClient;
            var map = root.Map;
            var heard = new Json('[');
            foreach (var rumor in map?.Heard ?? new List<HeardRumor>())
                heard.Item(new RawJson(new Json('{').Field("kind", rumor.Kind.ToString()).Field("index", rumor.Index).Field("day", rumor.Day).Close('}')));
            var leads = client?.SiteOf(client.CurrentStep?.Target);
            string arenas = map == null || client == null ? "" : string.Join(",", Enumerable.Range(0, client.Arenas.Count).Select(i => map.KnowledgeOfArena(i).ToString()));
            return new Json('{').Field("rumors", Settings.Rumors).Field("quickRest", root.HostWorld?.QuickRest ?? false).Field("quickRestChoice", Settings.QuickRest)
                .Field("heard", heard.Raw(']')).Field("firstChapter", client != null && Rumors.FirstChapter(client.Story, client.JourneyStep))
                .Field("leads", leads?.Definition.Id ?? "").Field("leadsKnown", leads == null || map == null ? "" : map.KnowledgeOf(leads).ToString())
                .Field("arenas", arenas)
                .Field("story", string.Join(" | ", root.HudLines.Where(l => l.StartsWith("이야기") || l.Contains("장 ·")).Select(l => l.Replace('\n', ' '))))
                .Field("health", root.HostPlayer?.Health ?? 0).Field("maxHealth", root.HostPlayer?.Stats.MaxHealth ?? 0)
                .Close('}');
        }

        // The pacing director for the player (World.Director.cs, 묶음 4b): state, tension, its clocks, what it did, the
        // monsters it has sent after someone, and what the screen showed of the ambush dust and the raids' howls.
        static string DirectorJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var pace = world.PaceOf(player);
            var shown = root.PaceShown;
            return new Json('{').Field("acts", world.Directs).Field("state", pace.State.ToString()).Field("tension", pace.Tension)
                .Field("seconds", (world.Tick - pace.Since) / (float)World.TicksPerSecond)
                .Field("restLeft", Math.Max(0, pace.RestUntil - world.Tick) / (float)World.TicksPerSecond)
                .Field("nextSend", (pace.NextSend - world.Tick) / (float)World.TicksPerSecond).Field("nextSurge", (pace.NextSurge - world.Tick) / (float)World.TicksPerSecond)
                .Field("leftAlone", world.LeftAlone(player))
                .Field("peaks", pace.Peaks).Field("rests", pace.Rests).Field("sends", pace.Sends).Field("ambushes", pace.Ambushes).Field("raids", pace.Raids)
                .Field("hunters", world.Hunters)
                .Field("chasing", world.Enemies.Count(e => !e.IsDead && e.TargetId == player.Id && e.State != EnemyState.Idle))
                .Field("near", world.Enemies.Count(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < 12))
                .Field("dustShown", shown.Dust).Field("howlsHeard", shown.Howls)
                .Close('}');
        }

        // The trap room nearest the player (World.Traps.cs, 묶음 4b): its phase and band, the doors the grates fell on
        // (and how many the screen sees), who is shut in, and the chest as the host and the screen have it.
        static string TrapsJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var client = root.LocalClient;
            var chest = world.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap)
                .OrderBy(p => Vec2.Distance(p.Center, player.Position)).FirstOrDefault();
            var json = new Json('{').Field("chests", world.Pois.Count(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap));
            var shown = root.PaceShown;
            json.Field("bannersShown", shown.Banners).Field("gratesShown", shown.Grates);
            if (chest == null) return json.Close('}');
            var trap = world.TrapAt(chest.Index);
            int band = trap?.Band == null ? 0 : world.MembersOf(trap.Band).Count();
            return json.Field("chest", chest.Index).Field("distance", Vec2.Distance(chest.Center, player.Position))
                .Field("phase", trap?.Phase.ToString() ?? "Ready").Field("wave", trap?.Wave ?? 0).Field("band", band)
                .Field("doors", trap?.Doors.Count ?? 0)
                .Field("grates", trap == null ? 0 : trap.Doors.Count(d => world.Map[d.X, d.Y] == TileType.Grate))
                .Field("clientGrates", trap == null || client?.Map == null ? 0 : trap.Doors.Count(d => client.Map[d.X, d.Y] == TileType.Grate))
                .Field("inside", trap?.Inside.Contains(player.Id) ?? false)
                .Field("opened", world.Opened(chest.Index)).Field("tile", world.Map[chest.X, chest.Y].ToString())
                .Field("canUse", world.CanUse(chest, player)).Field("clientCanUse", client?.CanUse(chest) ?? false)
                .Field("items", player.Inventory.Items.Count)
                .Close('}');
        }

        // The things to find: how many of each kind, opened, near the player, and the player's blessing and rest.
        static string PoisJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var client = root.LocalClient;
            var near = new Json('[');
            foreach (var poi in world.Pois.OrderBy(p => Vec2.Distance(p.Center, player.Position)).Take(6))
            {
                near.Item(new RawJson(new Json('{').Field("index", poi.Index).Field("id", poi.Definition.Id).Field("kind", poi.Kind.ToString())
                    .Field("x", poi.X).Field("y", poi.Y).Field("distance", Vec2.Distance(poi.Center, player.Position)).Field("gives", world.CanUse(poi, player))
                    .Field("tile", world.Map[poi.X, poi.Y].ToString()).Close('}')));
            }
            var json = new Json('{').Field("count", world.Pois.Count);
            foreach (PoiKind kind in Enum.GetValues(typeof(PoiKind))) json.Field(kind.ToString().ToLowerInvariant(), world.Pois.Count(p => p.Kind == kind));
            return json.Field("opened", world.OpenedPois.Count()).Field("prayedToday", world.PrayedToday(player.Name).Count())
                .Field("inReach", world.PoiNear(player.Position, player)?.Definition.Id ?? "")
                .Field("blessing", player.Blessing?.Id ?? "").Field("blessingSeconds", player.BlessingTicks / (float)World.TicksPerSecond)
                .Field("restedSeconds", player.RestedTicks / (float)World.TicksPerSecond).Field("lastCamp", world.LastCampOf(player.Name) ?? -1)
                .Field("clientOpened", client == null ? 0 : client.Pois.Count(p => client.PoiOpened(p.Index)))
                .Field("clientBlessing", client?.Blessing?.Id ?? "")
                .Field("near", near.Raw(']')).Close('}');
        }

        // The nth room of a role, in a biome or any, the rooms nearest the spawn (by rooms) first: "<role> [biome|any] [n]".
        static RoomNode RoomOf(GameRoot root, string[] words, int at)
        {
            var rooms = root.HostGenerated?.Rooms ?? throw new InvalidOperationException("this world has no rooms (the generator's version 1)");
            var role = (RoomRole)Enum.Parse(typeof(RoomRole), words[at], ignoreCase: true);
            Biome? biome = words.Length > at + 1 && words[at + 1] != "any" ? (Biome)Enum.Parse(typeof(Biome), words[at + 1], ignoreCase: true) : (Biome?)null;
            int nth = words.Length > at + 2 ? Int(words[at + 2]) : 0;
            var found = rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == role && (biome == null || n.Biome == biome)).OrderBy(n => n.Depth).ThenBy(n => n.Index).ToList();
            if (found.Count == 0) throw new InvalidOperationException($"no {role} room{(biome == null ? "" : " in " + biome)}");
            return found[Math.Min(nth, found.Count - 1)];
        }

        // Steps walked from the spawn to a tile, not through anything solid, water or lava; -1 for no way.
        static int StepsFromSpawn(World world, int tx, int ty)
        {
            var map = world.Map;
            int start = TileMap.Cell(world.Spawn.Y) * map.Width + TileMap.Cell(world.Spawn.X), goal = ty * map.Width + tx;
            var steps = new Dictionary<int, int> { [start] = 0 };
            var open = new Queue<int>();
            open.Enqueue(start);
            while (open.Count > 0)
            {
                int tile = open.Dequeue();
                if (tile == goal) return steps[tile];
                int x = tile % map.Width, y = tile / map.Width;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!Walkable(map, nx, ny) || steps.ContainsKey(ny * map.Width + nx)) continue;
                    steps[ny * map.Width + nx] = steps[tile] + 1;
                    open.Enqueue(ny * map.Width + nx);
                }
            }
            return -1;
        }

        static string RoomJson(GameRoot root, RoomNode room) => new Json('{')
            .Field("index", room.Index).Field("role", room.Role.ToString()).Field("biome", room.Biome.ToString())
            .Field("x", room.AnchorX).Field("y", room.AnchorY).Field("middleX", room.X).Field("middleY", room.Y).Field("radius", room.Radius)
            .Field("chunk", room.Chunk?.Id ?? "").Field("depth", room.Depth).Field("ways", room.Edges.Count).Field("roadside", room.Roadside)
            .Field("steps", StepsFromSpawn(root.HostWorld, room.AnchorX, room.AnchorY))
            // A room behind a wall that is dug: whether its inside is shut (as it should be until the wall is dug).
            .Field("shut", room.Inside is (int ix, int iy) && StepsFromSpawn(root.HostWorld, ix, iy) < 0)
            .Close('}');

        // The packs within 80 tiles of the player, nearest first (World.Packs.cs): its room and kind, how many it has
        // and how many stand, how many go after someone and how many of those are further from the player than they
        // see (they took up the alarm), and the counts against the caps.
        static string PacksJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var json = new Json('{').Field("rooms", world.Rooms != null);
            var list = new Json('[');
            foreach (var pack in world.Packs.Where(k => Vec2.Distance(k.Home, player.Position) < World.PackSleepReach).OrderBy(k => Vec2.Distance(k.Home, player.Position)))
            {
                var members = world.MembersOf(pack).ToList();
                var after = members.Where(m => m.State != EnemyState.Idle || m.Returning).ToList();
                list.Item(new RawJson(new Json('{')
                    .Field("id", pack.Id).Field("room", pack.Room?.Index ?? -1).Field("role", pack.Room?.Role.ToString() ?? "")
                    .Field("biome", (pack.Room?.Biome ?? world.Biomes.At(pack.Home)).ToString()).Field("kind", pack.Definition?.Id ?? "")
                    .Field("size", pack.Size).Field("standing", members.Count).Field("active", pack.Active).Field("cleared", pack.Cleared)
                    .Field("alerted", pack.Alerted).Field("chasing", after.Count)
                    .Field("unseen", after.Count(m => Vec2.Distance(m.Position, player.Position) > m.Definition.Aggro))
                    .Field("distance", Vec2.Distance(pack.Home, player.Position))
                    .Field("inRoom", pack.Room == null ? members.Count : members.Count(m => World.InPackRoom(pack.Room, m.Position)))
                    .Field("kinds", string.Join(",", members.Select(m => m.Definition.Id).Distinct()))
                    .Close('}')));
            }
            json.Field("packs", list.Raw(']'));
            json.Field("wanderers", world.Enemies.Count(e => e.Wanders && !e.IsDead));
            json.Field("field", world.Enemies.Count(e => e.Rift == 0 && !e.IsDead));
            json.Field("near", world.Enemies.Count(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < World.PackWakeReach));
            json.Field("partyCap", World.PartyCap).Field("hostCap", World.HostCap);
            json.Field("alertsShown", root.AlertsShown);
            return json.Close('}');
        }

        // The field elites within 80 tiles of the player, nearest first (World.Elites.cs, 묶음 2-2): rank, name as the
        // screen makes it, affixes, health, state; the camps about; and what the screen drew: name tags, the widest of
        // them against the screen's width (a phone held upright), the guard's lines and the warning sign.
        static string ElitesJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var list = new Json('[');
            foreach (var elite in world.Enemies.Where(e => e.Elite != EliteRank.None && !e.IsDead && Vec2.Distance(e.Position, player.Position) < World.PackSleepReach)
                .OrderBy(e => Vec2.Distance(e.Position, player.Position)))
            {
                var pack = world.PackOf(elite);
                list.Item(new RawJson(new Json('{')
                    .Field("id", elite.Id).Field("rank", elite.Elite.ToString()).Field("kind", elite.Definition.Id)
                    .Field("name", World.EliteName(elite.Definition, elite.Elite, elite.Affixes)).Field("affixes", World.EliteAffixLine(elite.Affixes))
                    .Field("health", elite.Health).Field("maxHealth", elite.MaxHealth).Field("state", elite.State.ToString())
                    .Field("distance", Vec2.Distance(elite.Position, player.Position))
                    .Field("pack", pack == null ? 0 : world.MembersOf(pack).Count()).Field("room", pack?.Room?.Role.ToString() ?? "")
                    .Close('}')));
            }
            return new Json('{').Field("elites", list.Raw(']'))
                .Field("camps", world.Packs.Count(p => p.Elite != EliteRank.None && p.Active))
                .Field("tagsShown", root.EliteTagsShown).Field("tagWidest", root.EliteTagWidest).Field("screenWidth", Ui.Width)
                .Field("guardLinks", root.EliteGuardLinksShown).Field("warningSign", root.WarningShown)
                .Close('}');
        }

        // The rooms of the world: which generator, how many of each role, how many have a chunk, the ways shut by a
        // cracked wall, and the tiles the rooms brought.
        static string Rooms(GameRoot root)
        {
            var generated = root.HostGenerated;
            var json = new Json('{').Field("generator", generated?.Version ?? 0);
            var rooms = generated?.Rooms;
            if (rooms == null) return json.Field("rooms", 0).Close('}');
            json.Field("rooms", rooms.Nodes.Count(n => n.Kind == RoomKind.Room));
            json.Field("chunks", rooms.Nodes.Count(n => n.Chunk != null));
            json.Field("cracked", rooms.Edges.Count(e => e.Kind == RoomEdgeKind.Cracked));
            json.Field("marks", rooms.Marks.Count);
            json.Field("roads", generated.Roads.Count(r => r.Kind == RoadKind.Road));
            json.Field("byways", generated.Roads.Count(r => r.Kind == RoadKind.Link));
            json.Field("bywayWalls", generated.Roads.Count(r => r.Kind == RoadKind.Link && r.Cracked != null));
            json.Field("roadRooms", generated.Roads.Where(r => r.Kind == RoadKind.Road).Sum(r => r.Rooms.Count));
            var roles = new Json('{');
            foreach (RoomRole role in Enum.GetValues(typeof(RoomRole)))
                roles.Field(role.ToString().ToLowerInvariant(), rooms.Nodes.Count(n => n.Kind == RoomKind.Room && n.Role == role));
            json.Field("roles", new RawJson(roles.Close('}')));
            return json.Close('}');
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
                // A pack of five slimes east of the player, the nearest six tiles off and the furthest eleven (most out
                // of sight), standing still under "monsters hold" until let go (2026-11-22, Docs/WorldRework.md 무리).
                case "pack":
                    var members = new List<Enemy>();
                    for (int i = 0; i < 5; i++)
                    {
                        var at = player.Position + new Vec2(6 + i * 1.3f, (i % 2 == 0 ? 1 : -1) * (i * 0.4f));
                        if (!Game.Core.Collision.Overlaps(world.Map, at, 0.45f, monster: true)) members.Add(Spawn("slime", at, 5000));
                    }
                    world.Gather(members);
                    break;
                // A field elite's camp in small (2026-11-22, Docs/WorldRework.md 들판 정예): a leader of mushroom
                // soldiers with fire and guard, three slimes round it, six tiles east; three fierce champion beetles six
                // tiles west. Under "monsters hold" they stand until let go.
                case "elite":
                    var spot = player.Position + new Vec2(6, 0);
                    world.MakeEliteHere(root.MonsterSheet.Find("myconid"), spot, EliteRank.Leader, RiftAffix.Fire | RiftAffix.Guard, root.MonsterSheet.Find("slime"), 3);
                    world.MakeEliteHere(root.MonsterSheet.Find("cave_beetle"), player.Position + new Vec2(-6, 0), EliteRank.Champion, RiftAffix.Fierce);
                    break;
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
                case "roll":
                    // Room to roll three times east and then a wall to roll into (World.Roll.cs).
                    foreach (var monster in world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 30)) monster.Health = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -2; dx <= 7; dx++) world.Build(px + dx, py + dy, TileType.Floor);
                        world.Build(px + 8, py + dy, TileType.Wall);
                    }
                    break;
                case "ice":
                case "quicksand":
                    // A lane of the floor east of the player with a wall at its end, to slide along or wade through
                    // (PlayerMotor, Docs/Combat.md 바닥).
                    foreach (var monster in world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 30)) monster.Health = 0;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 11; dx++) world.Build(px + dx, py + dy, dx < 0 ? TileType.Floor : scene == "ice" ? TileType.IceFloor : TileType.Quicksand);
                        world.Build(px + 12, py + dy, TileType.Wall);
                    }
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
        // "potion", "roll" and "quick N". The screen's size in points comes along, to turn points into the page's pixels.
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
                list.Item(new Json('{').Field("label", "roll").Field("rect", Rect(SkillBar.RollArea(skills))).Field("enabled", true).Field("shown", true).Raw('}'));
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
                // The boss whose entrance is on show (its card: name and title), or null (WorldView.Entrance.cs).
                .Field("entrance", root.EntranceShown)
                .Field("entranceFits", root.EntranceFits)
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
                // The floor underfoot (ice slides, quicksand slows: PlayerMotor), the speed the client carries, and the
                // room the player is nearest the middle of with what it is for (the generator's version 2).
                json.Field("underfoot", world.Map[TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y)].ToString());
                json.Field("slide", root.LocalClient?.Velocity.Length ?? 0f);
                // How far the player has gone with nothing held since the last scene: the slide on ice, measured where it
                // happens (the screen of a headless browser runs late, so asking for the speed can miss a short slide).
                json.Field("coast", root.Coasted);
                json.Field("generator", root.HostGenerated?.Version ?? 0);
                var roomHere = root.HostGenerated?.Rooms?.Nearest(TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y));
                json.Field("room", roomHere?.Role.ToString() ?? "");
                json.Field("roomChunk", roomHere?.Chunk?.Id ?? "");
                json.Field("health", player.Health);
                json.Field("maxHealth", player.Stats.MaxHealth);
                json.Field("dead", player.IsDead);
                json.Field("mana", player.Mana);
                json.Field("maxMana", player.Stats.MaxMana);
                json.Field("level", player.Level);
                json.Field("experience", player.Experience);
                json.Field("attacking", player.Attacking);
                json.Field("combo", player.ComboStep);
                // The dodge roll (World.Roll.cs): how many the host took and how many blows went through them; whether
                // one is going and its unhurt ticks; and what the screen has shown of them.
                json.Field("rolls", player.Rolls);
                json.Field("dodges", player.Dodges);
                json.Field("rolling", player.Rolling);
                json.Field("dodging", player.Dodging);
                json.Field("rollsShown", root.RollsShown);
                json.Field("dodgesShown", root.DodgesShown);
                json.Field("weapon", player.Equipment[ItemSlot.Weapon]?.Base.Id);
                json.Field("day", world.Today);
                json.Field("clock", Calendar.Text(world.Clock));
                json.Field("clockTicks", world.Clock);
                json.Field("tick", world.Tick);
                json.Field("facing", $"{player.Facing.X:0.00},{player.Facing.Y:0.00}");
                json.Field("steps", Feedback.Summary);
                json.Field("skillAim", $"{root.LastSkillAim.X:0.00},{root.LastSkillAim.Y:0.00}");
                json.Field("corners", root.CornersNear);
                json.Field("dayMinutes", world.DayMinutes);
                // The CC0 recordings loaded, and what the ground underfoot sounds like (Recordings, 2026-11-11).
                json.Field("recordedSets", Recordings.SetCount);
                // Timber walls round the player (a shop's, 2026-11-11, WorldGenerator.CarveShops).
                int timber = 0, px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
                for (int ty = py - 4; ty <= py + 4; ty++)
                    for (int tx = px - 4; tx <= px + 4; tx++)
                        if (world.Map[tx, ty] == TileType.TimberWall) timber++;
                json.Field("timberNear", timber);
                json.Field("ground", Recordings.GroundAt(world.Map, world.Biomes, TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y)).ToString());
                // The arena the player stands in: its kind and how much of its dressing is round them (arenas.csv).
                var arenaKind = root.LocalClient == null ? null : WorldView.ArenaAt(root.LocalClient, px, py);
                int dressing = 0;
                for (int ty = py - 12; ty <= py + 12; ty++)
                    for (int tx = px - 12; tx <= px + 12; tx++)
                        if (Structures.IsArenaFloor(world.Map[tx, ty]) || Structures.IsDecor(world.Map[tx, ty])) dressing++;
                json.Field("arena", arenaKind?.ToString());
                json.Field("arenaDressing", dressing);
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
                bool Danger(Vec2 at) => world.Hazards.Any(h => h.Shape != HazardShape.Share && h.Shape != HazardShape.Web && h.Contains(at, World.PlayerRadius));
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
                // What a player sees of them: marks drawn on the ground and the warning sign over the boss (an attack
                // the vein eater announces only by its wind-up lays hazards no one sees, 2026-11-12).
                json.Field("marksShown", root.MarksShown);
                // Skill effects running and the pieces they draw (WorldView.SkillFx).
                json.Field("skillFx", root.SkillFxAlive);
                json.Field("skillFxPieces", root.SkillFxPieces);
                // Projectiles drawn in their skill's own look (not the plain orb or arrow).
                json.Field("skillShots", root.SkillShotsDrawn);
                json.Field("warningSign", root.WarningShown);
                // What the silk mother's blows left on the floor: webs and pools of venom (in the world, and drawn), and
                // whether the player is caught in a web (2026-11-13).
                json.Field("pools", world.Hazards.Count(h => h.Lingers));
                json.Field("poolsShown", root.PoolsShown);
                json.Field("snared", player.Afflictions.Snared);
                // Ordinary monsters' attacks (monster_attacks.csv, 2026-11-21): those near that fight from the sheet with
                // their state and attack, their marks and what they left in the world, and what the screen draws of them
                // (marks on the ground with those made from a monster's state, pools, one-off effects, pieces, shots).
                json.Field("mobs", string.Join(" ", near.Where(e => e.Definition.Attacks.Count > 0).Select(e => $"{e.Definition.Id}:{e.State}:{e.Attack?.Id ?? "-"}")));
                json.Field("mobHazards", world.Hazards.Count(h => h.Mark != 0 && !h.Lingers));
                json.Field("mobPools", world.Hazards.Count(h => h.Mark != 0 && h.Lingers));
                json.Field("mobMarksShown", root.MonsterMarksShown);
                json.Field("mobPoolsShown", root.MonsterPoolsShown);
                json.Field("mobFx", root.MonsterFxAlive);
                json.Field("mobFxPieces", root.MonsterFxPieces);
                json.Field("mobShots", root.MonsterShotsDrawn);
                json.Field("rooted", player.Afflictions.Rooted);
                // The rest of the kinds (2026-11-22): blows shown so far (a count that only grows), those under the
                // ground near and their mounds drawn, lines between soldiers, bombers' blasts, grey numbers for blows a
                // guard turned, and whether the player is poisoned.
                json.Field("mobBlows", root.MonsterBlowsShown);
                // Marks shown over the heads of packs that noticed someone (a count that only grows, World.Packs.cs).
                json.Field("alertsShown", root.AlertsShown);
                json.Field("mobBuried", near.Count(e => e.Buried));
                json.Field("mobMounds", root.MonsterMoundsShown);
                json.Field("mobLinks", root.MonsterLinksShown);
                json.Field("explosions", root.ExplosionsShown);
                json.Field("guardedNumbers", root.GuardedNumbersShown);
                json.Field("poisoned", player.Afflictions.Poisoned);
                json.Field("renderTrace", root.RenderTrace);
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
                json.Field("bosses", string.Join(" ", world.Bosses.Select(b => $"{b.Definition.Id}:{b.Health:0}/{b.MaxHealth:0}:p{b.Phase}:{b.State}:{b.Attack?.Id ?? "-"}:{(b.State == BossState.Telegraph && b.Attack != null ? World.TellOf(b.Attack.Shape).ToString() : "-")}")));
                json.Field("defense", world.ActiveDefense == null ? null : $"wave {world.ActiveDefense.Wave} stone {world.ActiveDefense.StoneHealth:0}/{world.ActiveDefense.MaxStoneHealth:0}");
                json.Field("rift", world.ActiveRift == null ? null : $"tier {world.ActiveRift.Tier} wave {world.ActiveRift.Wave}");
            }
            return json.Close('}');
        }
    }
}
