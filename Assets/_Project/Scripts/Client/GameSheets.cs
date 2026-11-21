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
        public TextAsset monsters;
        // What the ordinary monsters do (monster_attacks.csv, 2026-11-21, Docs/Combat.md 몬스터의 공격 시트).
        public TextAsset monsterAttacks;
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
        public TextAsset story;
        // The words on screen in each language (Docs/Strings.md, 2026-11-11).
        public TextAsset strings;

        public static GameSheets Load() => Resources.Load<GameSheets>(ResourcePath);

        public LootData LoadLoot() =>
            LootSheets.Parse(Text(itemBases), Text(affixes), Text(runes), Text(runewords), Text(consumables), Text(materials), Text(recipes), Text(sets));

        // Without the terrain sheet (an asset made before it existed) the rooms are carved by the copy in code.
        public TileData LoadTiles(LootData loot) => TileSheet.Parse(Text(tiles), loot, Text(crops), Text(machines), Text(terrain));

        // Without the attacks' sheet (an asset made before it existed) every monster fights the old way.
        public MonsterData LoadMonsters() => MonsterSheet.Parse(Text(monsters), Text(monsterAttacks));

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
        public StoryData LoadStory(LootData loot, BossData bosses) => StorySheet.Parse(Text(places), Text(characters), Text(story), loot, bosses);

        // Without the sheet every line shows the Korean the code keeps beside it.
        public StringData LoadStrings() => StringSheet.Parse(Text(strings));

        public NpcData LoadNpcs(LootData loot) => NpcSheet.Parse(Text(npcs), Text(shop), loot, Text(requests));

        static string Text(TextAsset asset) => asset != null ? asset.text : "";
    }
}
