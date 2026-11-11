using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Core
{
    // What a playtest bot has found out on its run (Docs/Playtest.md, 상태로 움직이는 봇, 2026-11-11): the tiles it has
    // seen (from where it stood, through open floor, as far as a screen shows), the 4×4 cells it reached and when
    // (an archive in the way of Go-Explore, so it can go back to where it last got on), the biomes it walked in, the
    // monsters it felled by kind, when each of the guide's goals was first met, and a mark of progress, so a bot that
    // goes nowhere for a while can be told so. Also the numbers each run reports (BotReport).
    public sealed class BotMemory
    {
        // How far round the player the bot sees: about half a screen's height on a phone held wide.
        public const int Sight = 9;
        // The round about the spawn that the exploration rate is of: the first hour's caves (the warden's arena is
        // within it in every world).
        public const int Round = 40;
        public const int CellSize = 4;
        // How often (ticks) the guide's goals are looked at; they scan the bag and the whole map.
        const int GoalEvery = 60;

        readonly World _world;
        readonly Player _player;
        readonly GuideData _guide;
        readonly bool[] _seen;
        readonly Dictionary<(RecordKind, string), int> _startRecords;
        long _nextGoalCheck;
        int _seenInRound;
        int _roundTiles;
        readonly int _startFalls;

        public BotMemory(World world, Player player, GuideData guide = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _guide = guide;
            _seen = new bool[world.Map.Width * world.Map.Height];
            StartTick = world.Tick;
            _startFalls = player.Falls;
            LastProgressTick = world.Tick;
            _startRecords = new Dictionary<(RecordKind, string), int>(player.Records);
            int sx = TileMap.Cell(world.Spawn.X), sy = TileMap.Cell(world.Spawn.Y);
            for (int y = sy - Round; y <= sy + Round; y++)
            {
                for (int x = sx - Round; x <= sx + Round; x++)
                {
                    if (world.Map.InBounds(x, y) && InRound(x, y)) _roundTiles++;
                }
            }
        }

        public long StartTick { get; }
        public long LastProgressTick { get; private set; }

        // Waiting on something that is going on (a line in the water, a thing being made) counts as getting on.
        public void Busy() => LastProgressTick = _world.Tick;
        public int SeenTiles { get; private set; }
        // Seen tiles in the round about the spawn over all its tiles (0 to 1).
        public float Explored => _roundTiles == 0 ? 0 : (float)_seenInRound / _roundTiles;
        // Each 4×4 cell reached: the tick it was first reached and how many ticks were spent in it.
        public Dictionary<(int X, int Y), (long First, int Ticks)> Cells { get; } = new Dictionary<(int, int), (long, int)>();
        // The cell where the bot last got on (something new seen, gained or met), to go back to when it gets nowhere.
        public (int X, int Y) BestTile { get; private set; }
        public Dictionary<Biome, long> Biomes { get; } = new Dictionary<Biome, long>();
        // Each goal of the guide and the tick it was first met (not in order: a goal met before the guide comes to it
        // counts when it is met).
        public Dictionary<string, long> Goals { get; } = new Dictionary<string, long>();
        // How many of the guide's goals, from the first, are looked at (the rest scan the map for nothing in a first hour).
        public int GoalHorizon { get; set; } = int.MaxValue;
        // Named moments (BotReport.Moments): the first kill, the first boss's arena, its first warning, ...
        public Dictionary<string, long> Moments { get; } = new Dictionary<string, long>();
        // Falls since the run began, from the player's own count (a web bot does not look while it lies fallen).
        public int Deaths => _player.Falls - _startFalls;
        // Recent stretches of no progress the bot noticed, and where it gave up (BotPilot).
        public int Stalls { get; internal set; }
        public List<BotStuck> Stuck { get; } = new List<BotStuck>();
        // Goals the run left behind, and why (FirstHourBot).
        public Dictionary<string, string> Skipped { get; } = new Dictionary<string, string>();
        // Tiles the bot gave up reaching, with the tick to try them again.
        public Dictionary<(int X, int Y), long> Blocked { get; } = new Dictionary<(int, int), long>();

        public bool Seen(int x, int y) => _world.Map.InBounds(x, y) && _seen[y * _world.Map.Width + x];

        public bool IsBlocked(int x, int y) => Blocked.TryGetValue((x, y), out long until) && until > _world.Tick;

        public void Block((int X, int Y) tile, int ticks) => Blocked[tile] = _world.Tick + ticks;

        // The monsters felled on this run, by kind (from the bestiary, Records.cs).
        public Dictionary<string, int> Kills() => _player.Records
            .Where(r => r.Key.Kind == RecordKind.Kill)
            .Select(r => (r.Key.Id, Count: r.Value - _startRecords.GetValueOrDefault(r.Key)))
            .Where(k => k.Count > 0)
            .OrderBy(k => k.Id, StringComparer.Ordinal)
            .ToDictionary(k => k.Id, k => k.Count);

        public void Mark(string moment)
        {
            if (!Moments.ContainsKey(moment)) Moments[moment] = _world.Tick;
        }

        // Looks round from where the player stands; call it each tick (or each time the bot decides). True when
        // something counts as progress since the last call.
        public bool Observe()
        {
            if (_world.Tick == _observedTick) return false;
            _observedTick = _world.Tick;
            var map = _world.Map;
            int px = TileMap.Cell(_player.Position.X), py = TileMap.Cell(_player.Position.Y);
            bool progress = false;

            // Seen: a flood through open floor, Sight tiles round, and the solid tiles that bound it.
            int before = SeenTiles;
            var queue = new Queue<(int X, int Y)>();
            var reached = new HashSet<(int, int)>();
            queue.Enqueue((px, py));
            reached.Add((px, py));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                See(x, y);
                if (map.IsSolid(x, y) && (x != px || y != py)) continue;
                foreach (var (dx, dy) in BotNav.Ways)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny) || Math.Abs(nx - px) > Sight || Math.Abs(ny - py) > Sight || !reached.Add((nx, ny))) continue;
                    queue.Enqueue((nx, ny));
                }
            }
            if (SeenTiles > before) progress = true;

            var cell = (px / CellSize, py / CellSize);
            if (Cells.TryGetValue(cell, out var stay)) Cells[cell] = (stay.First, stay.Ticks + 1);
            else
            {
                Cells[cell] = (_world.Tick, 1);
                progress = true;
            }

            var biome = _world.Biomes[px, py];
            if (!_player.IsDead && !Biomes.ContainsKey(biome))
            {
                Biomes[biome] = _world.Tick;
                progress = true;
            }

            MarkMoments();
            int score = ProgressScore();
            if (score != _lastScore)
            {
                if (score > _lastScore) progress = true;
                _lastScore = score;
            }

            if (_guide != null && _world.Tick >= _nextGoalCheck)
            {
                _nextGoalCheck = _world.Tick + GoalEvery;
                if (CheckGoals()) progress = true;
            }

            if (progress)
            {
                LastProgressTick = _world.Tick;
                if (!_player.IsDead) BestTile = (px, py);
            }
            return progress;
        }

        int _lastScore = int.MinValue;
        long _observedTick = -1;

        // The first kill, and for each boss: the first time the player stood at its arena's edge, its first warning on
        // the floor (the boss's tell, BotReport "전조") and the first time it fell to them.
        void MarkMoments()
        {
            foreach (var record in _player.Records)
            {
                if (record.Value <= _startRecords.GetValueOrDefault(record.Key)) continue;
                if (record.Key.Kind == RecordKind.Kill) Mark(FirstKill);
                else if (record.Key.Kind == RecordKind.Boss) Mark(Felled + record.Key.Id);
            }
            foreach (var boss in _world.Bosses)
            {
                if (!_player.IsDead && Vec2.Distance(_player.Position, boss.Home) <= boss.ArenaRadius + 2) Mark(Arena + boss.Definition.Id);
                if (Moments.ContainsKey(Arena + boss.Definition.Id) && _world.Hazards.Any(h => h.Source == boss.Id)) Mark(Warning + boss.Definition.Id);
            }
        }

        public const string FirstKill = "first_kill";
        public const string Arena = "arena:";
        public const string Warning = "warning:";
        public const string Felled = "felled:";

        // What the player has gained: materials and things in the bag, experience, life skills and kills. It only goes
        // up with progress (spending a bag on a workbench is the workbench goal's progress instead).
        int ProgressScore()
        {
            int score = _player.Experience;
            foreach (var count in _player.Inventory.Materials.Values) score += count;
            foreach (var count in _player.Inventory.Consumables.Values) score += count;
            score += _player.Inventory.Items.Count;
            foreach (int experience in _player.LifeExperience) score += experience;
            foreach (var record in _player.Records) score += record.Value;
            return score;
        }

        // Looks at the guide's goals now (after something that may have met one: placed, planted, made, sold).
        public bool Refresh() => _guide != null && CheckGoals();

        bool CheckGoals()
        {
            GuideFacts facts = null;
            bool met = false;
            foreach (var goal in _guide.Goals.Take(GoalHorizon))
            {
                if (Goals.ContainsKey(goal.Id)) continue;
                facts ??= GuideFacts.Of(_world, _player);
                if (!goal.Conditions.Any(c => GuideProgress.IsMet(c, facts))) continue;
                Goals[goal.Id] = _world.Tick;
                met = true;
            }
            return met;
        }

        void See(int x, int y)
        {
            int at = y * _world.Map.Width + x;
            if (_seen[at]) return;
            _seen[at] = true;
            SeenTiles++;
            if (InRound(x, y)) _seenInRound++;
        }

        bool InRound(int x, int y)
        {
            float dx = x + 0.5f - _world.Spawn.X, dy = y + 0.5f - _world.Spawn.Y;
            return dx * dx + dy * dy <= Round * Round;
        }

        // A seen tile one could stand on with an unseen tile beside it: the edge of what the bot knows.
        public bool IsFrontier(int x, int y)
        {
            if (!Seen(x, y) || _world.Map.IsSolid(x, y) || _world.Map[x, y] == TileType.Lava) return false;
            foreach (var (dx, dy) in BotNav.Ways)
            {
                if (_world.Map.InBounds(x + dx, y + dy) && !Seen(x + dx, y + dy)) return true;
            }
            return false;
        }

        // The tiles round (cx, cy) as the bot knows them, a line a row, the top row north: @ the player, # rock,
        // c copper, i iron, o other ore, ~ water, ^ lava, . floor, = road or laid floor, + something placed or built,
        // m a monster, B a boss's home, x a tile given up on, * the path, and a space for unseen.
        public string MiniMap(int cx, int cy, int halfWidth = 15, int halfHeight = 8, IEnumerable<(int X, int Y)> path = null)
        {
            var map = _world.Map;
            var marks = new Dictionary<(int, int), char>();
            if (path != null) foreach (var tile in path) marks[tile] = '*';
            foreach (var blocked in Blocked.Keys) marks[blocked] = 'x';
            foreach (var enemy in _world.Enemies)
            {
                if (!enemy.IsDead) marks[(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y))] = 'm';
            }
            foreach (var boss in _world.Bosses) marks[(TileMap.Cell(boss.Home.X), TileMap.Cell(boss.Home.Y))] = 'B';
            marks[(TileMap.Cell(_player.Position.X), TileMap.Cell(_player.Position.Y))] = '@';
            var text = new StringBuilder();
            for (int y = cy + halfHeight; y >= cy - halfHeight; y--)
            {
                for (int x = cx - halfWidth; x <= cx + halfWidth; x++)
                {
                    if (marks.TryGetValue((x, y), out char mark)) text.Append(mark);
                    else if (!map.InBounds(x, y) || !Seen(x, y)) text.Append(' ');
                    else text.Append(Glyph(map[x, y]));
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        static char Glyph(TileType type)
        {
            switch (type)
            {
                case TileType.Floor:
                    return '.';
                case TileType.Wall:
                case TileType.IronWall:
                    return '#';
                case TileType.CopperOre:
                    return 'c';
                case TileType.IronOre:
                    return 'i';
                case TileType.Water:
                    return '~';
                case TileType.Lava:
                    return '^';
            }
            if (type.ToString().EndsWith("Ore")) return 'o';
            if (TileMap.IsPaving(type) || Structures.IsWay(type) || TileMap.IsSoil(type)) return '=';
            return '+';
        }
    }

    // Where and why a bot gave up on something (BotPilot): a report a person (or an AI session) can read.
    public sealed class BotStuck
    {
        public long Tick { get; set; }
        public string Goal { get; set; }
        public string Target { get; set; }
        public (int X, int Y) At { get; set; }
        public string Why { get; set; }
        public List<string> Recent { get; set; } = new List<string>();
        public string Map { get; set; }
    }
}
