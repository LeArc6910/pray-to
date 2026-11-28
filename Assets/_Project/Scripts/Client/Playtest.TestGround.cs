using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The test ground's commands (Docs/TestGround.md). The panel's buttons (TestPanel.cs) run them, and so do the web
    // checks: "test ..." is refused outside the test ground, so nothing of it reaches ordinary play. Each answers
    // "ok test <what> | <what the panel shows, in Korean>". They change the host's world (the test ground is solo).
    //
    //   test                      JSON: on, undying, rising, monsters held, the hit log shown, the panel's section, the
    //                             player's level, armor and health, and the hits (who, what, amount, share, raw, armor,
    //                             band, band armor, share through, zone, saved, aim)
    //   test level <n> [points|attack|defense]     the level, its points left or spent (TestKit.Build)
    //   test classes <id,id>      those classes at once
    //   test gear <band 1-5> [sword|bow|staff] [rarity] [low|middle|high] [upgrade]   the band's full set worn
    //   test piece <base> [rarity] [roll] [upgrade]   one item worn;   test bare   nothing worn
    //   test rune <id> | runes clear    test eat <food>    test heal    test undying on|off    test rising here|off
    //   test hits show|hide|clear       test home        test boss <id> | revive <id>   test dungeon <id>
    //   test field <biome> <zone>       test mob <id> [normal|leader|champion|pack]   test clear   test hold on|off
    //   test panel open|close|<section>    test gender 0|1 (man, woman)
    static partial class Playtest
    {
        // A panel button: the command run as the harness would, its answer's words for the screen.
        internal static string Act(GameRoot root, string command)
        {
            string answer;
            try
            {
                answer = Do(root, command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) ?? "ok";
            }
            catch (Exception e)
            {
                answer = "error " + e.Message;
            }
            Log(answer);
            int bar = answer.IndexOf(" | ", StringComparison.Ordinal);
            return bar >= 0 ? answer.Substring(bar + 3) : answer.StartsWith("error", StringComparison.Ordinal) ? "할 수 없다: " + answer : answer;
        }

        static string Test(GameRoot root, string[] words)
        {
            if (!root.InTestGround) return "error test: not in the test ground";
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var loot = root.LootSheet;
            if (words.Length == 1) return "test " + TestJson(root);
            string Done(string what, string text) => $"ok test {what} | {text}";
            switch (words[1])
            {
                case "level":
                {
                    int level = Math.Clamp(Int(words[2]), 1, Leveling.MaxLevel);
                    var build = words.Length > 3 ? (TestKit.Build)Enum.Parse(typeof(TestKit.Build), words[3], ignoreCase: true) : TestKit.Build.Points;
                    TestKit.SetLevel(player, root.ClassSheet, loot, level, build);
                    string how = build switch
                    {
                        TestKit.Build.Attack => "공격 빌드로 포인트를 다 썼다",
                        TestKit.Build.Defense => "방어 빌드로 포인트를 다 썼다",
                        _ => $"스킬 포인트 {player.SkillPoints} · 특성 포인트 {player.PassivePoints}을 직접 쓴다",
                    };
                    return Done("level", $"Lv {player.Level}: {how} · {Stats(player)}");
                }
                case "classes":
                {
                    var chosen = ClassRules.Choose(root.ClassSheet, words[2].Split(','), ClassRules.DefaultMaxClasses);
                    if (chosen.Count == 0) throw new ArgumentException($"no classes '{words[2]}'");
                    player.ChangeKit(chosen, null);
                    foreach (var ready in player.Skills) player.ClearCooldown(ready);
                    player.InventoryVersion++;
                    return Done("classes", "클래스: " + string.Join(" + ", chosen.Select(c => c.Name)));
                }
                case "gear":
                {
                    int band = DamageBands.Clamp(Int(words[2]));
                    string weapon = words.Length > 3 ? words[3] : "sword";
                    var rarity = words.Length > 4 ? (Rarity)Enum.Parse(typeof(Rarity), words[4], ignoreCase: true) : Rarity.Common;
                    var roll = words.Length > 5 ? (TestKit.Roll)Enum.Parse(typeof(TestKit.Roll), words[5], ignoreCase: true) : TestKit.Roll.Middle;
                    int upgrade = words.Length > 6 ? Int(words[6]) : 0;
                    foreach (var item in TestKit.BandSet(loot, band, weapon, rarity, roll, upgrade)) world.WearForTest(player, item);
                    return Done("gear", $"{DamageBands.BandNames[band - 1]} 세트({ItemText.RarityName(rarity)} · {RollName(roll)} · +{Math.Clamp(upgrade, 0, Item.MaxUpgrade)}) · {Stats(player)}");
                }
                case "piece":
                {
                    var itemBase = loot.FindBase(words[2]) ?? throw new ArgumentException($"no item base '{words[2]}'");
                    var rarity = words.Length > 3 ? (Rarity)Enum.Parse(typeof(Rarity), words[3], ignoreCase: true) : Rarity.Common;
                    var roll = words.Length > 4 ? (TestKit.Roll)Enum.Parse(typeof(TestKit.Roll), words[4], ignoreCase: true) : TestKit.Roll.Middle;
                    var item = TestKit.Make(loot, itemBase, rarity, roll, words.Length > 5 ? Int(words[5]) : 0);
                    world.WearForTest(player, item);
                    return Done("piece", $"{ItemText.Name(item)}({ItemText.RarityName(item.Rarity)}) · {Stats(player)}");
                }
                case "spare":
                {
                    // A weapon into the spare slot (Docs/Weapons.md, 무기 두 벌); "none" takes it off.
                    if (words[2] == "none")
                    {
                        player.UnequipSpare();
                        return Done("spare none", "보조 무기를 뺐다 · " + Stats(player));
                    }
                    var itemBase = loot.FindBase(words[2]) ?? throw new ArgumentException($"no item base '{words[2]}'");
                    var roll = words.Length > 3 ? (TestKit.Roll)Enum.Parse(typeof(TestKit.Roll), words[3], ignoreCase: true) : TestKit.Roll.Middle;
                    var item = TestKit.Make(loot, itemBase, Rarity.Common, roll, words.Length > 4 ? Int(words[4]) : 0);
                    world.WearSpareForTest(player, item);
                    return Done("spare", $"보조 무기: {ItemText.Name(item)} ({itemBase.Kind.Name}) · {Stats(player)}");
                }
                case "art":
                    // The weapon's own art, the way the player faces (Docs/Weapons.md 묶음 C); a greatsword's lets go by
                    // itself when full.
                    if (!world.UseWeaponArt(player.Id, player.Facing)) throw new InvalidOperationException("no art now (none, cooling down or nobody ahead)");
                    return Done("art", $"{player.Stats.Kind.Art.Name} · {player.Stats.Kind.Name}");
                case "artready":
                    player.ArtReady.Clear();
                    return Done("artready", "무기 기술 대기를 모두 비웠다");
                case "swap":
                    if (!world.SwapWeapons(player.Id)) throw new InvalidOperationException("nothing to swap");
                    return Done("swap", $"손에: {player.Equipment[ItemSlot.Weapon]?.Base.Name ?? "맨손"} · 보조: {player.Equipment.Spare?.Base.Name ?? "없음"} · {Stats(player)}");
                case "set":
                {
                    // Every piece of a set (sets.csv), at the roll and upgrade (Docs/Gear.md).
                    var set = loot.Sets.FirstOrDefault(x => x.Id == words[2]) ?? throw new ArgumentException($"no set '{words[2]}'");
                    var roll = words.Length > 3 ? (TestKit.Roll)Enum.Parse(typeof(TestKit.Roll), words[3], ignoreCase: true) : TestKit.Roll.Middle;
                    int upgrade = words.Length > 4 ? Int(words[4]) : 0;
                    foreach (var piece in loot.Bases.Where(b => b.Set == set)) world.WearForTest(player, TestKit.Make(loot, piece, Rarity.Common, roll, upgrade));
                    return Done("set", $"{set.Name} 세트 {loot.Bases.Count(b => b.Set == set)}부위 · {Stats(player)}");
                }
                case "bare":
                    foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
                        if (player.Equipment[slot] != null) player.Unequip(slot);
                    if (player.Equipment.Spare != null) player.UnequipSpare();
                    return Done("bare", "맨몸 · " + Stats(player));
                case "rune":
                {
                    var rune = loot.Runes.FirstOrDefault(r => r.Id == words[2]) ?? throw new ArgumentException($"no rune '{words[2]}'");
                    var into = TestKit.InsertRune(player, rune, loot.Runewords);
                    return into == null ? Done("rune none", "빈 홈이 없다: 장비를 먼저 입는다")
                        : Done("rune", $"{rune.Name} 룬 → {ItemText.Name(into)}" + (into.Runeword != null ? $" · 룬어 {into.Runeword.Name}" : "") + " · " + Stats(player));
                }
                case "runes":
                    return Done("runes", $"룬 {TestKit.ClearRunes(player)}개를 뺐다 · {Stats(player)}");
                case "eat":
                {
                    var food = Consumable(loot, words[2]);
                    player.Inventory.AddConsumable(food, 1);
                    player.ConsumableCooldown = 0;
                    player.InventoryVersion++;
                    if (!world.UseConsumable(player.Id, food)) throw new InvalidOperationException($"could not eat {food.Id}");
                    return Done("eat", $"{food.Name}을(를) 먹었다");
                }
                case "gender":
                {
                    // The character as a man or a woman (Docs/Gender.md): to see the figure and hear the groan when struck.
                    var client = root.LocalClient;
                    client.Look = client.Look.WithGender(words[2] == "1" || words[2] == "female" ? CharacterLook.Female : CharacterLook.Male);
                    player.Look = client.Look.Packed;
                    return Done("gender", client.Look.IsFemale ? "여자 캐릭터로" : "남자 캐릭터로");
                }
                case "heal":
                    world.RefillForTest(player, Consumable(loot, "healing_potion"), TestGround.Potions);
                    return Done("heal", $"다 회복했다 · 회복 물약 {TestGround.Potions}개");
                case "undying":
                    world.SetUndying(player.Id, words[2] == "on");
                    return Done("undying", world.Undying(player.Id) ? "죽지 않기 켬: 생명력이 1 밑으로 내려가지 않는다" : "죽지 않기 끔");
                case "rising":
                    world.SetRising(player.Id, words[2] == "here" ? player.Position : (Vec2?)null);
                    return Done("rising", world.RisingOf(player.Id) != null ? "쓰러지면 이 자리에서 일어난다" : "쓰러지면 늘 일어나던 곳에서 일어난다");
                case "hits":
                    if (words[2] == "clear") world.ClearHits(player.Id);
                    else TestGround.ShowHits = words[2] == "show";
                    return Done("hits", words[2] == "clear" ? "맞은 기록을 지웠다" : TestGround.ShowHits ? "맞은 기록을 화면에 보인다" : "맞은 기록을 숨긴다");
                case "home":
                    TestGround.Move(player, world.Spawn);
                    return Done("home", "시작 마을");
                case "boss":
                case "revive":
                {
                    string id = words[2];
                    var definition = root.BossSheet?.Bosses.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException($"no boss '{id}'");
                    if (!world.ReviveBoss(id)) throw new InvalidOperationException($"no arena for '{id}' in this world");
                    if (words[1] == "revive") return Done("revive", $"{definition.Name}이(가) 되살아났다");
                    var home = world.BossHome(id).Value;
                    var boss = world.Bosses.First(b => b.Definition.Id == id);
                    var map = world.Map;
                    // At the arena's edge on the side of the way in, outside the fight until a step is taken.
                    var to = Nearest(map, TileMap.Cell(home.X), TileMap.Cell(home.Y) - (int)Math.Max(4, boss.ArenaRadius - 2), (x, y) => Walkable(map, x, y)
                            && !Game.Core.Collision.Overlaps(map, TileMap.Center(x, y), World.PlayerRadius))
                        ?? throw new InvalidOperationException("no floor in the arena");
                    TestGround.Move(player, to);
                    return Done("boss", $"{definition.Name}의 방 · Lv {definition.Level} · {DamageBands.BandNames[DamageBands.BandOfLevel(definition.Level) - 1]} 단계");
                }
                case "dungeon":
                {
                    var gate = world.Gates.Where(g => !g.Cellar && world.DungeonFor(g)?.Id == words[2]).OrderBy(g => Vec2.Distance(g.Center, player.Position)).FirstOrDefault()
                        ?? throw new InvalidOperationException($"no gate to '{words[2]}' in this world");
                    var definition = world.DungeonFor(gate);
                    TestGround.Move(player, Game.Core.Collision.Resolve(world.Map, gate.Center, World.PlayerRadius));
                    bool going = world.EnterDungeon(player.Id, gate.Index);
                    return Done("dungeon", $"{definition.NameFor(gate.Biome)} · 던전 레벨 {definition.Level}" + (going ? " · 들어가는 중" : " · 문 앞"));
                }
                case "field":
                {
                    var biome = (Biome)Enum.Parse(typeof(Biome), words[2], ignoreCase: true);
                    int zone = Int(words[3]);
                    var room = TestGround.FieldRoom(world, biome, zone) ?? throw new InvalidOperationException($"no room of {biome} in zone {zone}");
                    TestGround.Move(player, TileMap.Center(room.AnchorX, room.AnchorY));
                    return Done("field", $"{BiomeMap.Name(biome)} {zone}구역 · 몬스터의 피해 +{(zone - 1) * World.ZonePowerStep * 100:0}%");
                }
                case "mob":
                {
                    var kind = root.MonsterSheet.Find(words[2]) ?? throw new ArgumentException($"no monster '{words[2]}'");
                    string how = words.Length > 3 ? words[3] : "normal";
                    var rank = how == "leader" ? EliteRank.Leader : how == "champion" ? EliteRank.Champion : EliteRank.None;
                    var forward = player.Facing.LengthSquared > 0.01f ? player.Facing.Normalized : new Vec2(1, 0);
                    var made = world.SpawnForTest(kind, player.Position + forward * (3 + kind.Radius * 2), rank, how == "pack" ? 5 : 1);
                    if (made.Count == 0) return Done("mob none", $"{kind.Name}이(가) 설 자리가 없다");
                    string label = rank == EliteRank.Leader ? " 우두머리" : rank == EliteRank.Champion ? " 챔피언" : how == "pack" ? " 무리" : "";
                    return Done("mob", $"{kind.Name}{label} {made.Count}마리 · {world.ZoneLevelAt(made[0].Position)}구역");
                }
                case "clear":
                    return Done("clear", $"몬스터 {world.ClearMonstersNear(player.Position, 40)}마리를 치웠다");
                case "hold":
                    world.MonstersHold = words[2] == "on";
                    world.BossesHold = false;
                    return Done("hold", world.MonstersHold ? "몬스터가 멈췄다" : "몬스터가 다시 움직인다");
                case "panel":
                    if (words[2] == "close") root.ShowScreen(GameScreen.None);
                    else
                    {
                        if (words[2] != "open") TestPanel.Pick(words[2]);
                        root.ShowScreen(GameScreen.Test);
                    }
                    return Done("panel", root.ScreenName);
            }
            return $"error test {words[1]}: no such test command";
        }

        static string RollName(TestKit.Roll roll) => roll switch
        {
            TestKit.Roll.Low => "낮은 값",
            TestKit.Roll.High => "높은 값",
            _ => "가운데",
        };

        static string Stats(Player player) => $"방어 {player.Stats.Armor:0} · 생명력 {player.Stats.MaxHealth:0} · 피해 {player.Stats.Damage:0}";

        static string TestJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var hits = new Json('[');
            foreach (var hit in world.HitsOn(player.Id))
            {
                hits.Item(new Json('{').Field("who", hit.Who).Field("what", hit.What).Field("kind", hit.Kind.ToString()).Field("amount", hit.Amount)
                    .Field("pct", hit.Fraction * 100).Field("raw", hit.Raw).Field("armored", hit.Armored).Field("armor", hit.Armor).Field("band", hit.Band)
                    .Field("bandArmor", hit.BandArmor).Field("through", hit.Share * 100).Field("zone", hit.ZoneLevels).Field("saved", hit.Saved)
                    .Field("aim", hit.Aim * 100).Field("line", TestGround.Line(hit)).Raw('}'));
            }
            return new Json('{').Field("on", root.InTestGround).Field("undying", world.Undying(player.Id)).Field("rising", world.RisingOf(player.Id) != null)
                .Field("hold", world.MonstersHold).Field("showHits", TestGround.ShowHits).Field("section", TestPanel.SectionName).Field("status", TestPanel.Status)
                .Field("level", player.Level).Field("armor", player.Stats.Armor).Field("maxHealth", player.Stats.MaxHealth).Field("health", player.Health)
                .Field("worn", Strings(player.Equipment.Items.Select(i => $"{i.Base.Id}|{i.Rarity}|+{i.Upgrade}|{i.Sockets.Count(s => s != null)}")))
                .Field("count", world.HitCount(player.Id)).Field("overlay", Rect(TestGround.Shown)).Field("overlayLines", Strings(TestGround.ShownLines))
                .Field("hits", hits.Raw(']')).Close('}');
        }
    }
}
