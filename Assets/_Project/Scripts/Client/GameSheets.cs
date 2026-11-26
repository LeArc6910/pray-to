using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The data sheets as TextAssets, so a built game can read them too. Game > Set Up Main Scene creates this in a
    // Resources folder and fills it in; run it again after adding a sheet.
    public sealed class GameSheets : ScriptableObject
    {
        public const string ResourcePath = "GameSheets";

        public TextAsset itemBases;
        public TextAsset affixes;
        public TextAsset runes;
        public TextAsset runewords;
        public TextAsset consumables;
        public TextAsset materials;
        public TextAsset recipes;
        public TextAsset sets;
        public TextAsset tiles;
        public TextAsset crops;
        public TextAsset machines;
        // How each biome's rooms are carved (terrain.csv, 2026-11-21, Docs/Biomes.md 지형).
        public TextAsset terrain;
        // The room chunks drawn in letters and the roles' numbers (rooms.csv, room_roles.csv, 2026-11-21, Docs/Biomes.md 방 조각).
        public TextAsset rooms;
        public TextAsset roomRoles;
        // The things to find in the rooms: camps, shrines, chests, carts (pois.csv, 2026-11-22, Docs/Biomes.md 볼거리).
        public TextAsset pois;
        // The instance dungeons and their graphs (dungeons.csv, dungeon_graphs.csv, 2026-11-22, Docs/Biomes.md 던전).
        public TextAsset dungeons;
        public TextAsset dungeonGraphs;
        public TextAsset monsters;
        // What the ordinary monsters do (monster_attacks.csv, 2026-11-21, Docs/Combat.md 몬스터의 공격 시트).
        public TextAsset monsterAttacks;
        // The packs the pack rooms hold (packs.csv, 2026-11-22, Docs/Combat.md 무리).
        public TextAsset packs;
        public TextAsset bosses;
        public TextAsset bossAttacks;
        // Each boss's arena and entrance (arenas.csv, 2026-11-17, Docs/Bosses.md 아레나 컨셉).
        public TextAsset arenas;
        public TextAsset classes;
        public TextAsset skills;
        public TextAsset passives;
        public TextAsset goals;
        public TextAsset npcs;
        public TextAsset shop;
        public TextAsset requests;
        // The days (events.csv, 2026-11-09).
        public TextAsset events;
        // The story: its places, people and steps (Docs/Story.md, 2026-11-10).
        public TextAsset places;
        public TextAsset characters;
        public TextAsset townsfolk;
        public TextAsset story;
        // The words on screen in each language (Docs/Strings.md, 2026-11-11).
        public TextAsset strings;

        public static GameSheets Load() => Resources.Load<GameSheets>(ResourcePath);

        public LootData LoadLoot() =>
            LootSheets.Parse(Text(itemBases), Text(affixes), Text(runes), Text(runewords), Text(consumables), Text(materials), Text(recipes), Text(sets));

        // Without the terrain sheet (an asset made before it existed) the rooms are carved by the copy in code.
        public TileData LoadTiles(LootData loot) => TileSheet.Parse(Text(tiles), loot, Text(crops), Text(machines), Text(terrain), Text(rooms), Text(roomRoles), Text(pois),
            Text(dungeons), Text(dungeonGraphs));

        // Without the attacks' sheet (an asset made before it existed) every monster fights the old way; without the
        // packs' sheet monsters come one by one, as before there were packs.
        public MonsterData LoadMonsters()
        {
            var data = MonsterSheet.Parse(Text(monsters), Text(monsterAttacks));
            foreach (string error in PackSheet.Parse(Text(packs), data).Errors) Debug.LogError(error);
            return data;
        }

        // Without the arenas' sheet (an asset made before it existed) every arena is plain and no boss has an entrance.
        public BossData LoadBosses(MonsterData monsterData)
        {
            var data = BossSheet.Parse(Text(bosses), Text(bossAttacks), monsterData);
            foreach (string error in ArenaSheet.Parse(Text(arenas), data).Errors) Debug.LogError(error);
            return data;
        }

        public ClassData LoadClasses() => ClassSheet.Parse(Text(classes), Text(skills), Text(passives));

        // Without the sheet (an asset made before it existed) there are no goals.
        public GuideData LoadGuide(LootData loot) => GuideSheet.Parse(Text(goals), loot);

        // Without the sheets (an asset made before they existed) there is no one.
        public DayEventData LoadEvents() => DayEventSheet.Parse(Text(events));
        public StoryData LoadStory(LootData loot, BossData bosses)
        {
            var data = StorySheet.Parse(Text(places), Text(characters), Text(story), loot, bosses);
            // The grown towns' people (Docs/Towns.md); a sheet set up before them has none.
            if (townsfolk != null) data.WithTownsfolk(TownsfolkSheet.Parse(townsfolk.text));
            return data;
        }

        // Without the sheet every line shows the Korean the code keeps beside it.
        public StringData LoadStrings() => StringSheet.Parse(Text(strings));

        public NpcData LoadNpcs(LootData loot) => NpcSheet.Parse(Text(npcs), Text(shop), loot, Text(requests));

        static string Text(TextAsset asset) => asset != null ? asset.text : "";
    }
}
