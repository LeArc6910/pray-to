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
        public TextAsset monsters;
        public TextAsset bosses;
        public TextAsset bossAttacks;
        public TextAsset classes;
        public TextAsset skills;
        public TextAsset passives;
        public TextAsset goals;
        public TextAsset npcs;
        public TextAsset shop;
        public TextAsset requests;
        // The days (events.csv, 2026-11-09).
        public TextAsset events;

        public static GameSheets Load() => Resources.Load<GameSheets>(ResourcePath);

        public LootData LoadLoot() =>
            LootSheets.Parse(Text(itemBases), Text(affixes), Text(runes), Text(runewords), Text(consumables), Text(materials), Text(recipes), Text(sets));

        public TileData LoadTiles(LootData loot) => TileSheet.Parse(Text(tiles), loot, Text(crops), Text(machines));

        public MonsterData LoadMonsters() => MonsterSheet.Parse(Text(monsters));

        public BossData LoadBosses(MonsterData monsterData) => BossSheet.Parse(Text(bosses), Text(bossAttacks), monsterData);

        public ClassData LoadClasses() => ClassSheet.Parse(Text(classes), Text(skills), Text(passives));

        // Without the sheet (an asset made before it existed) there are no goals.
        public GuideData LoadGuide(LootData loot) => GuideSheet.Parse(Text(goals), loot);

        // Without the sheets (an asset made before they existed) there is no one.
        public DayEventData LoadEvents() => DayEventSheet.Parse(Text(events));

        public NpcData LoadNpcs(LootData loot) => NpcSheet.Parse(Text(npcs), Text(shop), loot, Text(requests));

        static string Text(TextAsset asset) => asset != null ? asset.text : "";
    }
}
