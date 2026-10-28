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
        public TextAsset monsters;
        public TextAsset bosses;
        public TextAsset bossAttacks;
        public TextAsset classes;
        public TextAsset skills;

        public static GameSheets Load() => Resources.Load<GameSheets>(ResourcePath);

        public LootData LoadLoot() => LootSheets.Parse(Text(itemBases), Text(affixes), Text(runes), Text(runewords), Text(consumables));

        public MonsterData LoadMonsters() => MonsterSheet.Parse(Text(monsters));

        public BossData LoadBosses(MonsterData monsterData) => BossSheet.Parse(Text(bosses), Text(bossAttacks), monsterData);

        public ClassData LoadClasses() => ClassSheet.Parse(Text(classes), Text(skills));

        static string Text(TextAsset asset) => asset != null ? asset.text : "";
    }
}
