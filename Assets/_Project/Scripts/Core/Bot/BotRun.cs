using System;
using System.Linq;

namespace Game.Core
{
    // Everything a world of the game is made from, as the game's host has it (GameRoot.StartHost).
    public sealed class BotSheets
    {
        public MonsterData Monsters { get; set; }
        public LootData Loot { get; set; }
        public BossData Bosses { get; set; }
        public ClassData Classes { get; set; }
        public TileData Tiles { get; set; }
        public GuideData Guide { get; set; }
        public NpcData Npcs { get; set; }
        public StoryData Story { get; set; }
    }

    // A new character's first hour in a fresh world of the seed, played by FirstHourBot with no screen (CoreSandbox
    // --hour, FirstHourBotTests). The host is made as the game makes it, and the player joins as a new character does.
    public static class BotRun
    {
        public static BotRunResult FirstHour(BotSheets sheets, ulong seed, int minutes = 60, string stopAfter = "level5", Action<FirstHourBot> each = null,
            int mapSize = WorldGenerator.DefaultSize)
        {
            // mapSize: the tests keep the small map; CoreSandbox --hour plays the game's (WorldGenerator.GameSize).
            var host = new WorldHost(seed, sheets.Monsters, sheets.Loot, bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles,
                guide: sheets.Guide, npcs: sheets.Npcs, config: new HostConfig { MapSize = mapSize }, worldConfig: new WorldConfig { Forage = true }, story: sheets.Story);
            host.Receive(1, new JoinMessage("bot", "봇").Encode());
            var world = host.World;
            var player = world.Players[0];
            var bot = new FirstHourBot(world, player, sheets.Monsters, sheets.Loot, sheets.Classes, sheets.Guide, sheets.Tiles, stopAfter);
            // The first boss: the nearest of the caves' (the warden), chosen now, since a felled one leaves World.Bosses.
            var first = world.Bosses.Where(b => b.Definition.Place == null).OrderBy(b => Vec2.Distance(b.Home, world.Spawn)).FirstOrDefault();
            long end = world.Tick + (long)minutes * 60 * World.TicksPerSecond;
            while (world.Tick < end && !bot.Done)
            {
                bot.Tick();
                host.Tick();
                host.TakeOutgoing();
                each?.Invoke(bot);
            }
            bot.Memory.Observe();
            return new BotRunResult
            {
                Name = $"시드 {seed}",
                Memory = bot.Memory,
                Ticks = world.Tick - bot.Memory.StartTick,
                Level = player.Level,
                FirstBoss = first?.Definition.Id ?? "",
                Goals = sheets.Guide.Goals.Take(bot.Memory.GoalHorizon).ToList(),
            };
        }
    }
}
