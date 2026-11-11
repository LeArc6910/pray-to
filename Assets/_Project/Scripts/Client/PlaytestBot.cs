using System;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The web playtest's bot by the host's Core state, not the screen (Docs/Playtest.md, 상태로 움직이는 봇,
    // 2026-11-11): the same BotPilot and BotMemory as the sim's FirstHourBot. The page asks where to go next ("nav
    // monster") and does it with real keys, clicks and touches; "sense" gives the monsters round, the map as the bot
    // knows it and the run's numbers, and "botreport" the run as Markdown tables (BotReport). Only while a test has
    // turned "probe on" and only in a solo game, so a player never pays for it or sees it in the console.
    //
    //   nav <wall | tile <type> | near <type> | monster [id] | boss <id> | fight <id> | explore | loot | at <x> <y>>
    //       JSON: do (move, dig, fight, arrived, wait, noway), x and y (the way to go or face, tiles, y north), tile
    //       (offset of the tile to dig or that was gone to), goal, steps, stalled, why
    //   sense       JSON: the run's numbers, the monsters within 30 tiles, the bot's small map
    //   botreport [name]   the run as BotReport's tables
    //   botreset    a fresh memory (a new run)
    static partial class Playtest
    {
        static World _botWorld;
        static Player _botPlayer;
        static BotPilot _pilot;
        static long _botStart;
        // The first boss (the caves' nearest, the warden), chosen at the run's start: a felled one leaves World.Bosses.
        static string _firstBoss;

        static BotPilot Pilot(GameRoot root, bool fresh = false)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            if (fresh || _pilot == null || world != _botWorld || player != _botPlayer)
            {
                _botWorld = world;
                _botPlayer = player;
                _pilot = new BotPilot(world, player, new BotMemory(world, player, root.GuideSheet));
                _botStart = world.Tick;
                _firstBoss = world.Bosses.Where(b => b.Definition.Place == null).OrderBy(b => Vec2.Distance(b.Home, world.Spawn)).FirstOrDefault()?.Definition.Id ?? "cave_warden";
            }
            return _pilot;
        }

        static string Bot(GameRoot root, string[] words)
        {
            if (!Ui.Probing) return $"error {words[0]}: probe off (playtests only)";
            var player = root.HostPlayer;
            if (player == null) return $"error {words[0]}: no player";
            switch (words[0])
            {
                case "botreset":
                    Pilot(root, fresh: true);
                    return "ok botreset";
                case "nav":
                    return "nav " + Nav(Pilot(root), player, words);
                case "sense":
                    return "sense " + Sense(root, Pilot(root), player);
                default:
                    var pilot = Pilot(root);
                    pilot.Memory.Observe();
                    var run = new[]
                    {
                        new BotRunResult
                        {
                            Name = words.Length > 1 ? string.Join(" ", words.Skip(1)) : "웹",
                            Memory = pilot.Memory,
                            Ticks = root.HostWorld.Tick - _botStart,
                            Level = player.Level,
                            FirstBoss = _firstBoss,
                            Goals = root.GuideSheet?.Goals.Take(17).ToList() ?? new System.Collections.Generic.List<Goal>(),
                        },
                    };
                    return "botreport " + Json.Quote(BotReport.Table(run) + "\n" + BotReport.Goals(run) + "\n" + BotReport.Stuck(run));
            }
        }

        static string Nav(BotPilot pilot, Player player, string[] words)
        {
            var intent = pilot.Next(BotTarget.Parse(words.Skip(1).ToList()));
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            var json = new Json('{')
                .Field("do", intent.Do.ToString().ToLowerInvariant())
                .Field("x", intent.Direction.X)
                .Field("y", intent.Direction.Y);
            if (intent.Tile is (int tx, int ty)) json.Field("tile", $"{tx - px},{ty - py}");
            if (intent.Goal is (int gx, int gy)) json.Field("goal", $"{gx - px},{gy - py}");
            return json
                .Field("steps", intent.Steps)
                .Field("stalled", intent.Stalled)
                .Field("why", intent.Why)
                .Close('}');
        }

        static string Sense(GameRoot root, BotPilot pilot, Player player)
        {
            var memory = pilot.Memory;
            memory.Observe();
            var world = root.HostWorld;
            long Ago(long tick) => tick - _botStart;
            string Seconds(long ticks) => (Ago(ticks) / (float)World.TicksPerSecond).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            var monsters = new Json('[');
            foreach (var enemy in world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) <= 30)
                         .OrderBy(e => Vec2.Distance(e.Position, player.Position)).Take(12))
            {
                monsters.Item(new Json('[').Item(enemy.Definition.Id).Item(enemy.Position.X - player.Position.X).Item(enemy.Position.Y - player.Position.Y)
                    .Item(enemy.Health).Raw(']'));
            }
            return new Json('{')
                .Field("seconds", Seconds(world.Tick))
                .Field("explored", memory.Explored)
                .Field("seen", memory.SeenTiles)
                .Field("biomes", string.Join(",", memory.Biomes.OrderBy(b => b.Value).Select(b => b.Key.ToString())))
                .Field("kills", string.Join(",", memory.Kills().Select(k => $"{k.Key}:{k.Value}")))
                .Field("moments", string.Join(",", memory.Moments.OrderBy(m => m.Value).Select(m => $"{m.Key}@{Seconds(m.Value)}")))
                .Field("goals", string.Join(",", memory.Goals.OrderBy(g => g.Value).Select(g => $"{g.Key}@{Seconds(g.Value)}")))
                .Field("deaths", memory.Deaths)
                .Field("stalls", memory.Stalls)
                .Field("stuck", memory.Stuck.Count)
                .Field("recent", string.Join(" / ", pilot.Recent.Skip(Math.Max(0, pilot.Recent.Count - 6))))
                .Field("monsters", monsters.Raw(']'))
                .Field("map", memory.MiniMap(px, py, path: pilot.Path))
                .Close('}');
        }
    }
}
