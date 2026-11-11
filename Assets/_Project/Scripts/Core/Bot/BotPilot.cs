using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    // What a playtest bot is heading for (BotPilot), as the web playtest writes it ("nav <target>", Docs/Playtest.md).
    public enum BotAim
    {
        // The nearest plain rock, to dig for stone (and mining experience).
        Wall,
        // The nearest tile of a type that is dug (an ore vein), through rock in the way.
        Tile,
        // Beside the nearest tile of a type (a workbench, water, the merchant's stall), facing it.
        Beside,
        // The nearest monster (of a kind, with an id) not much above the player's level.
        Monster,
        // Into a boss's arena: the first tile inside its ring.
        Boss,
        // At a boss, to fight it.
        BossFight,
        // The nearest edge of what has been seen.
        Explore,
        // The nearest drop on the floor that is the player's.
        Loot,
        // One tile.
        At,
    }

    public sealed class BotTarget
    {
        public BotAim Aim { get; private set; }
        public TileType Tile { get; private set; }
        public string Id { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        public static BotTarget Wall => new BotTarget { Aim = BotAim.Wall };
        public static BotTarget Explore => new BotTarget { Aim = BotAim.Explore };
        public static BotTarget Loot => new BotTarget { Aim = BotAim.Loot };
        public static BotTarget Dig(TileType tile) => new BotTarget { Aim = BotAim.Tile, Tile = tile };
        public static BotTarget Beside(TileType tile) => new BotTarget { Aim = BotAim.Beside, Tile = tile };
        public static BotTarget Monster(string id = null) => new BotTarget { Aim = BotAim.Monster, Id = id };
        public static BotTarget Boss(string id) => new BotTarget { Aim = BotAim.Boss, Id = id };
        public static BotTarget Fight(string id) => new BotTarget { Aim = BotAim.BossFight, Id = id };
        public static BotTarget At(int x, int y) => new BotTarget { Aim = BotAim.At, X = x, Y = y };

        // "wall", "tile CopperOre", "near Workbench", "monster [id]", "boss cave_warden", "fight cave_warden", "explore",
        // "loot", "at x y".
        public static BotTarget Parse(IReadOnlyList<string> words)
        {
            string Word(int i) => i < words.Count ? words[i] : throw new ArgumentException("nav: what to go to? " + string.Join(" ", words));
            TileType Type(int i) => (TileType)Enum.Parse(typeof(TileType), Word(i), ignoreCase: true);
            switch (Word(0))
            {
                case "wall": return Wall;
                case "tile": return Dig(Type(1));
                case "near": return Beside(Type(1));
                case "monster": return Monster(words.Count > 1 ? words[1] : null);
                case "boss": return Boss(Word(1));
                case "fight": return Fight(Word(1));
                case "explore": return Explore;
                case "loot": return Loot;
                case "at": return At(int.Parse(Word(1), CultureInfo.InvariantCulture), int.Parse(Word(2), CultureInfo.InvariantCulture));
            }
            throw new ArgumentException("nav: unknown target " + words[0]);
        }

        public override string ToString() => Aim switch
        {
            BotAim.Wall => "wall",
            BotAim.Tile => "tile " + Tile,
            BotAim.Beside => "near " + Tile,
            BotAim.Monster => Id == null ? "monster" : "monster " + Id,
            BotAim.Boss => "boss " + Id,
            BotAim.BossFight => "fight " + Id,
            BotAim.Explore => "explore",
            BotAim.Loot => "loot",
            _ => $"at {X} {Y}",
        };
    }

    public enum BotDo
    {
        // Walk toward Direction.
        Move,
        // Swing the pickaxe at Tile (Direction points at it, along one axis).
        Dig,
        // Swing the weapon toward Direction: a monster or the boss is in reach.
        Fight,
        // There: Tile (if any) is what was gone to; Direction faces it.
        Arrived,
        // Nothing to do this moment (fallen, waiting for the world).
        Wait,
        // It cannot be reached, or the bot gave up on it (Why says which).
        NoWay,
    }

    // One decision of the pilot. Direction is a world offset from the player (tiles, y north).
    public sealed class BotIntent
    {
        public BotDo Do { get; set; }
        public Vec2 Direction { get; set; }
        public (int X, int Y)? Tile { get; set; }
        public (int X, int Y)? Goal { get; set; }
        public int Steps { get; set; }
        public bool Stalled { get; set; }
        public string Why { get; set; } = "";

        public override string ToString() =>
            $"{Do}{(Tile is (int x, int y) ? $" {x},{y}" : "")}{(Goal is (int gx, int gy) ? $" → {gx},{gy} ({Steps})" : "")}{(Why.Length > 0 ? " " + Why : "")}";
    }

    // Takes a playtest bot to what it heads for, the same way on the host's Core in a sim (FirstHourBot), in the editor
    // and on the web demo (Client/PlaytestBot.cs): by the Core's state, not the screen (Docs/Research2026-11.md, 시험 봇은
    // 화면이 아니라 상태와 길찾기로). Each call looks round (BotMemory), then: out of a warning on the floor first, a
    // monster within reach is fought, else the next tile of the cheapest way there (BotNav), digging what is in the way.
    // When nothing has counted as progress for StallTicks (nothing new seen, gained, met, and no nearer the goal), the
    // goal tile is given up for a while and the bot goes back to the tile where it last got on (Go-Explore); at the
    // GiveUp-th time on one target it says NoWay and writes a stuck report (BotMemory.Stuck).
    public sealed class BotPilot
    {
        // How far the bot looks for monsters and veins: a screen and a half (the minimap marks veins 16 tiles out).
        public const float Sense = 28;
        public const int Replan = 15;

        readonly World _world;
        readonly Player _player;
        readonly List<string> _recent = new List<string>();
        string _targetKey;
        string _lastLine;
        long _targetProgressTick;
        int _bestSteps = int.MaxValue;
        int _stallsHere;
        long _backUntil;
        long _planTick = long.MinValue;
        int _foeId;
        float _foeHealth;
        // Monsters given up on (they took no harm), till a tick.
        readonly Dictionary<int, long> _ignored = new Dictionary<int, long>();

        public BotPilot(World world, Player player, BotMemory memory)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            Nav = new BotNav(world);
        }

        public BotMemory Memory { get; }
        public BotNav Nav { get; }
        public int StallTicks { get; set; } = 20 * World.TicksPerSecond;
        public int GiveUp { get; set; } = 3;
        // What the bot is on, for the stuck report (the guide's goal, say).
        public string Goal { get; set; } = "";
        public List<(int X, int Y)> Path { get; private set; } = new List<(int, int)>();
        // The tile of the drop the last "loot" went for.
        public (int X, int Y)? LastLoot { get; private set; }
        // The last twenty things the bot set out to do, with when.
        public IReadOnlyList<string> Recent => _recent;

        (int X, int Y) Here => (TileMap.Cell(_player.Position.X), TileMap.Cell(_player.Position.Y));

        public BotIntent Next(BotTarget target)
        {
            Memory.Observe();
            var intent = Decide(target);
            string line = $"{Clock(_world.Tick - Memory.StartTick)} {target} {intent}";
            string key = $"{target} {intent.Do} {intent.Goal}";
            if (key != _lastLine)
            {
                _lastLine = key;
                _recent.Add(line);
                if (_recent.Count > 20) _recent.RemoveAt(0);
            }
            return intent;
        }

        public static string Clock(long ticks)
        {
            long seconds = ticks / World.TicksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        BotIntent Decide(BotTarget target)
        {
            if (_player.IsDead) return new BotIntent { Do = BotDo.Wait, Why = "쓰러짐" };
            Nav.Fit(_player);
            Nav.Avoid = (x, y) => Memory.IsBlocked(x, y);
            string key = target.ToString();
            if (key != _targetKey)
            {
                _targetKey = key;
                _targetProgressTick = _world.Tick;
                _bestSteps = int.MaxValue;
                _stallsHere = 0;
                _backUntil = 0;
                Path = new List<(int, int)>();
                _planTick = long.MinValue;
            }

            var danger = WayOutOfDanger();
            if (danger is Vec2 away) return new BotIntent { Do = BotDo.Move, Direction = away, Why = "예고를 피함" };

            // Getting nowhere: give the goal up for a while and go back to where the bot last got on.
            long since = _world.Tick - Math.Max(Memory.LastProgressTick, _targetProgressTick);
            bool stalled = false;
            if (since > StallTicks)
            {
                stalled = true;
                Memory.Stalls++;
                _stallsHere++;
                _targetProgressTick = _world.Tick;
                _bestSteps = int.MaxValue;
                // With many to choose from (rock, veins, monsters, the edge of the seen) that one is given up; going to
                // one place, the next step of the way is, so another way is tried.
                bool many = target.Aim == BotAim.Wall || target.Aim == BotAim.Tile || target.Aim == BotAim.Monster
                    || target.Aim == BotAim.Explore || target.Aim == BotAim.Loot;
                if (Path.Count > 0 && (many || Path.Count > 1)) Memory.Block(many ? Path[Path.Count - 1] : Path[0], 120 * World.TicksPerSecond);
                if (_stallsHere >= GiveUp)
                {
                    _stallsHere = 0;
                    Report(target, $"{StallTicks / World.TicksPerSecond}초씩 {GiveUp}번 진척 없음");
                    return new BotIntent { Do = BotDo.NoWay, Stalled = true, Why = "막힘" };
                }
                _backUntil = _world.Tick + 6 * World.TicksPerSecond;
                Path = new List<(int, int)>();
                _planTick = long.MinValue;
            }
            // A monster within reach is fought; as long as its health goes down that is getting on. One that takes no
            // harm (2026-11-11: the sim's bot swung at a slime past a corner of rock for half an hour) is let be.
            if (!stalled && FoeInReach(target) is (int id, Vec2 at, float health))
            {
                if (id != _foeId || health < _foeHealth) _targetProgressTick = _world.Tick;
                _foeId = id;
                _foeHealth = health;
                return new BotIntent { Do = BotDo.Fight, Direction = at - _player.Position, Why = "곁의 적" };
            }
            if (stalled && _foeId != 0)
            {
                _ignored[_foeId] = _world.Tick + 60 * World.TicksPerSecond;
                _foeId = 0;
            }

            if (_backUntil > _world.Tick && Memory.BestTile != Here)
            {
                var back = Nav.To(Here.X, Here.Y, Memory.BestTile.X, Memory.BestTile.Y);
                if (back != null && back.Count > 0)
                {
                    var step = Follow(back);
                    step.Stalled = stalled;
                    step.Why = "멈춤: 마지막으로 진척한 칸으로";
                    return step;
                }
            }

            if (Path.Count > 0 && Path[0] == Here) Path.RemoveAt(0);
            bool adjacent = Path.Count > 0 && Math.Abs(Path[0].X - Here.X) + Math.Abs(Path[0].Y - Here.Y) == 1;
            int every = target.Aim == BotAim.Monster || target.Aim == BotAim.BossFight || target.Aim == BotAim.Loot ? Replan / 2 : Replan;
            if (Path.Count == 0 || !adjacent || _world.Tick - _planTick >= every || Nav.CostOf(Path[0].X, Path[0].Y) < 0)
            {
                var (found, why, arrived) = Plan(target);
                if (arrived != null) return arrived;
                if (found == null)
                {
                    Path = new List<(int, int)>();
                    return new BotIntent { Do = BotDo.NoWay, Stalled = stalled, Why = why };
                }
                Path = found;
                _planTick = _world.Tick;
            }
            if (Path.Count < _bestSteps)
            {
                _bestSteps = Path.Count;
                _targetProgressTick = _world.Tick;
            }
            var intent = Follow(Path);
            intent.Stalled = stalled;
            return intent;
        }

        // The next step along a path: dig the next tile if it is rock, else walk at its middle. Beside a tile (the last
        // of a path that ends beside it) is arriving.
        BotIntent Follow(List<(int X, int Y)> path)
        {
            var goal = path[path.Count - 1];
            var next = path[0];
            var toward = TileMap.Center(next.X, next.Y) - _player.Position;
            if (!Nav.Walkable(next.X, next.Y) && Nav.Diggable(next.X, next.Y))
            {
                var axis = new Vec2(Math.Sign(next.X - Here.X), Math.Sign(next.Y - Here.Y));
                return new BotIntent { Do = BotDo.Dig, Direction = axis, Tile = next, Goal = goal, Steps = path.Count };
            }
            return new BotIntent { Do = BotDo.Move, Direction = toward, Goal = goal, Steps = path.Count };
        }

        // Finds the way for the target; or says it is there (arrived) or why there is none.
        (List<(int X, int Y)> Path, string Why, BotIntent Arrived) Plan(BotTarget target)
        {
            var (hx, hy) = Here;
            var map = _world.Map;
            switch (target.Aim)
            {
                case BotAim.Wall:
                {
                    var path = Nav.Find(hx, hy, (x, y) => map[x, y] == TileType.Wall && Nav.Diggable(x, y) && !Memory.IsBlocked(x, y));
                    return (path, path == null ? "캘 벽 없음" : "", null);
                }
                case BotAim.Tile:
                {
                    var path = Nav.Find(hx, hy, (x, y) => map[x, y] == target.Tile && !Memory.IsBlocked(x, y) && Near(x, y, Sense));
                    if (path == null) return Explore(target.Tile + " 없음: 탐험");
                    return (path, "", null);
                }
                case BotAim.Beside:
                {
                    if (BesideOf(target.Tile) is (int bx, int by)) return (null, "", Arrive((bx, by)));
                    var path = Nav.Find(hx, hy, (x, y) => map[x, y] == target.Tile && !Memory.IsBlocked(x, y), enter: false);
                    return (path, path == null ? target.Tile + "에 갈 길 없음" : "", null);
                }
                case BotAim.Monster:
                {
                    var foe = _world.Enemies
                        .Where(e => !e.IsDead && (target.Id == null || e.Definition.Id == target.Id)
                            && !(_ignored.TryGetValue(e.Id, out long until) && until > _world.Tick)
                            && e.Definition.Level <= _player.Level + 2
                            && Vec2.Distance(e.Position, _player.Position) <= Sense
                            && !Memory.IsBlocked(TileMap.Cell(e.Position.X), TileMap.Cell(e.Position.Y)))
                        .OrderBy(e => Vec2.Distance(e.Position, _player.Position)).ThenBy(e => e.Id)
                        .FirstOrDefault();
                    if (foe == null) return Explore("몬스터 없음: 탐험");
                    int fx = TileMap.Cell(foe.Position.X), fy = TileMap.Cell(foe.Position.Y);
                    var path = Nav.Find(hx, hy, (x, y) => Math.Max(Math.Abs(x - fx), Math.Abs(y - fy)) <= 1,
                        heuristic: (x, y) => Math.Max(0, Math.Abs(x - fx) + Math.Abs(y - fy) - 2) * Nav.WalkTicks);
                    if (path == null)
                    {
                        Memory.Block((fx, fy), 30 * World.TicksPerSecond);
                        return (null, "몬스터에게 갈 길 없음", null);
                    }
                    if (path.Count == 0) path.Add((fx, fy));
                    return (path, "", null);
                }
                case BotAim.Boss:
                {
                    var boss = BossOf(target.Id);
                    if (boss == null) return (null, "보스 없음 " + target.Id, null);
                    float ring = boss.ArenaRadius + 1;
                    if (Vec2.Distance(_player.Position, boss.Home) <= ring + 1)
                        return (null, "", new BotIntent { Do = BotDo.Arrived, Direction = boss.Home - _player.Position, Why = "아레나" });
                    int bx = TileMap.Cell(boss.Home.X), by = TileMap.Cell(boss.Home.Y);
                    var path = Nav.Find(hx, hy, (x, y) => Vec2.Distance(TileMap.Center(x, y), boss.Home) <= ring && Nav.Walkable(x, y),
                        heuristic: (x, y) => Math.Max(0, (int)(Math.Abs(x - bx) + Math.Abs(y - by) - ring - 1)) * Nav.WalkTicks);
                    return (path, path == null ? "아레나에 갈 길 없음" : "", null);
                }
                case BotAim.BossFight:
                {
                    var boss = BossOf(target.Id);
                    if (boss == null || boss.IsDead) return (null, "", new BotIntent { Do = BotDo.Arrived, Why = "보스 없음" });
                    int bx = TileMap.Cell(boss.Position.X), by = TileMap.Cell(boss.Position.Y);
                    var path = Nav.Find(hx, hy, (x, y) => Math.Max(Math.Abs(x - bx), Math.Abs(y - by)) <= 1,
                        heuristic: (x, y) => Math.Max(0, Math.Abs(x - bx) + Math.Abs(y - by) - 2) * Nav.WalkTicks);
                    if (path == null || path.Count == 0)
                        return (null, "", new BotIntent { Do = BotDo.Move, Direction = boss.Position - _player.Position, Why = "보스에게" });
                    return (path, "", null);
                }
                case BotAim.Explore:
                    return Explore("탐험할 곳 없음");
                case BotAim.Loot:
                {
                    var drop = _world.GroundLoot
                        .Where(l => l.For(_player.Id, _world.Tick) && Vec2.Distance(l.Position, _player.Position) <= 12
                            && !Memory.IsBlocked(TileMap.Cell(l.Position.X), TileMap.Cell(l.Position.Y)))
                        .OrderBy(l => Vec2.Distance(l.Position, _player.Position)).ThenBy(l => l.Id).FirstOrDefault();
                    if (drop == null) return (null, "", new BotIntent { Do = BotDo.Arrived, Why = "주울 것 없음" });
                    LastLoot = (TileMap.Cell(drop.Position.X), TileMap.Cell(drop.Position.Y));
                    var path = Nav.To(hx, hy, TileMap.Cell(drop.Position.X), TileMap.Cell(drop.Position.Y));
                    if (path == null) return (null, "주울 것에 갈 길 없음", null);
                    if (path.Count == 0)
                        return (null, "", new BotIntent { Do = BotDo.Move, Direction = drop.Position - _player.Position, Why = "줍기" });
                    return (path, "", null);
                }
                default:
                {
                    if (hx == target.X && hy == target.Y) return (null, "", Arrive(null));
                    var path = Nav.To(hx, hy, target.X, target.Y);
                    return (path, path == null ? "길 없음" : "", null);
                }
            }
        }

        // The nearest edge of the seen, by the ticks it takes (Go-Explore: from where the bot is, on into the unknown).
        (List<(int X, int Y)> Path, string Why, BotIntent Arrived) Explore(string why)
        {
            var (hx, hy) = Here;
            var path = Nav.Find(hx, hy, (x, y) => (x != hx || y != hy) && Memory.IsFrontier(x, y) && !Memory.IsBlocked(x, y));
            return (path, path == null ? why + " (갈 곳 없음)" : why, null);
        }

        bool Near(int x, int y, float reach) => Vec2.Distance(TileMap.Center(x, y), _player.Position) <= reach;

        Boss BossOf(string id) => _world.Bosses.Where(b => id == null || b.Definition.Id == id)
            .OrderBy(b => Vec2.Distance(b.Home, _player.Position)).FirstOrDefault();

        // A tile of the type right beside the player (four ways), if any.
        (int X, int Y)? BesideOf(TileType type)
        {
            var (hx, hy) = Here;
            foreach (var (dx, dy) in BotNav.Ways)
            {
                if (_world.Map.InBounds(hx + dx, hy + dy) && _world.Map[hx + dx, hy + dy] == type && !Memory.IsBlocked(hx + dx, hy + dy)) return (hx + dx, hy + dy);
            }
            return null;
        }

        BotIntent Arrive((int X, int Y)? tile) => new BotIntent
        {
            Do = BotDo.Arrived,
            Tile = tile,
            Direction = tile is (int x, int y) ? new Vec2(x - Here.X, y - Here.Y) : default,
        };

        // A monster (or, fighting, the boss) close enough to swing at: its id, where it is and its health.
        (int Id, Vec2 At, float Health)? FoeInReach(BotTarget target)
        {
            if (target.Aim == BotAim.BossFight)
            {
                var boss = BossOf(target.Id);
                if (boss != null && !boss.IsDead && Vec2.Distance(boss.Position, _player.Position) <= boss.Definition.Radius + World.PlayerRadius + World.MeleeReach * 0.9f)
                    return (-boss.Id, boss.Position, boss.Health);
            }
            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in _world.Enemies)
            {
                if (enemy.IsDead || _ignored.TryGetValue(enemy.Id, out long until) && until > _world.Tick) continue;
                float d = Vec2.Distance(enemy.Position, _player.Position);
                if (d > enemy.Definition.Radius + World.PlayerRadius + World.MeleeReach * 0.9f || d >= best) continue;
                best = d;
                nearest = enemy;
            }
            return nearest == null ? ((int, Vec2, float)?)null : (nearest.Id, nearest.Position, nearest.Health);
        }

        // Standing in a warning on the floor (not a share marker): toward the nearest spot out of every one.
        Vec2? WayOutOfDanger()
        {
            bool Danger(Vec2 at) => _world.Hazards.Any(h => h.Shape != HazardShape.Share && h.Contains(at, World.PlayerRadius));
            if (!Danger(_player.Position)) return null;
            foreach (float reach in new[] { 1.5f, 2.5f, 3.5f, 5f })
            {
                for (int k = 0; k < 8; k++)
                {
                    var step = new Vec2(MathF.Cos(k * MathF.PI / 4), MathF.Sin(k * MathF.PI / 4)) * reach;
                    var to = _player.Position + step;
                    if (Nav.Walkable(TileMap.Cell(to.X), TileMap.Cell(to.Y)) && !Danger(to)) return step;
                }
            }
            return null;
        }

        // Writes down where the bot gave up and why (BotMemory.Stuck).
        public void Report(BotTarget target, string why)
        {
            var (hx, hy) = Here;
            Memory.Stuck.Add(new BotStuck
            {
                Tick = _world.Tick - Memory.StartTick,
                Goal = Goal,
                Target = target?.ToString() ?? "-",
                At = (hx, hy),
                Why = why,
                Recent = _recent.ToList(),
                Map = Memory.MiniMap(hx, hy, path: Path),
            });
        }
    }
}
