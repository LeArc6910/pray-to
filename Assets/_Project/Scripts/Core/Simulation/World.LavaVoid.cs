using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The rules the lava fields and the void abyss bring (2026-11-25, Docs/BiomeRework.md B4b): a steam vent
    // (TileType.SteamVent) hisses for a second and then bursts for a little over half a second, every few seconds on its
    // own beat, burning whoever stands over it (and the monsters too, the lava's own little: they shrug off fire); a
    // cracked plate of the abyss (TileType.CrackedPlate) gives way under a player who stands on it longer than a second,
    // leaving a hole into the void (TileType.BrokenPlate, solid) that comes back as a plate a while later, and whoever
    // was on it falls: a tenth of their health and back up on the nearest firm ground. The abyss's darker light is the
    // screen's (Client/Art/LavaVoidLight.cs). All of it is the host's, decided as the world runs and without randomness
    // (a vent's beat is a hash of its tile), so every world with the tiles has it: version 7 lays them in the field
    // (WorldGenerator.LavaVoidDungeons.cs), and the ember forge's and the broken gate's rooms in every world.
    public sealed partial class World
    {
        // ---- Steam vents (4-1 용암 지대: "김 뿜는 틈, 4초마다, 1초 예고 뒤 0.6초 분출, 반지름 1칸, 불") ----

        public const float VentPeriod = 4;
        public const float VentTelegraph = 1;
        public const float VentBurst = 0.6f;
        public const float VentRadius = 1;
        // A burst's damage (a hit of the band of the ground, Docs/Difficulty.md: about 5% of the health in mithril gear),
        // and the share of a monster's health it takes (less for a big body, its fire resistance besides).
        public const float VentDamage = 48;
        public const float VentMonsterShare = 0.15f;
        // Only the vents this near a player hiss: no one would see the rest.
        public const int VentReach = 14;

        // The vents hissing now: the hazard that marks one, where, when its burst begins and ends, and whom it has
        // burnt (a burst burns each once).
        readonly List<VentPuff> _puffs = new List<VentPuff>();
        readonly HashSet<int> _ventSeen = new HashSet<int>();

        sealed class VentPuff
        {
            public int Hazard;
            public Vec2 At;
            public long Bursts;
            public long Ends;
            public int Damage;
            public readonly HashSet<int> Burnt = new HashSet<int>();
            public readonly HashSet<int> Scalded = new HashSet<int>();
        }

        // How many vents have burst, and how many times one burnt a player (the playtests and tests).
        public int VentsBurst { get; private set; }
        public int VentBurns { get; private set; }

        // Where in its beat a vent at this tile starts: a hash of the tile, so neighbours do not puff together and each
        // keeps its own rhythm whoever is near.
        public static int VentPhase(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (int)(h % (uint)Ticks(VentPeriod));
        }

        // Whether the vent at this tile starts to hiss this tick.
        public bool VentStarts(int x, int y) => (Tick + VentPhase(x, y)) % Ticks(VentPeriod) == 0;

        void UpdateVents()
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var puff = _puffs[i];
                if (Tick > puff.Ends)
                {
                    _puffs.RemoveAt(i);
                    continue;
                }
                if (Tick < puff.Bursts) continue;
                if (Tick == puff.Bursts)
                {
                    VentsBurst++;
                    // Those over it now are struck by the hazard as it lands (UpdateHazards, after this); the burst burns
                    // whoever comes into it while it lasts.
                    foreach (var player in _players)
                        if (!player.IsDead && Vec2.Distance(player.Position, puff.At) <= VentRadius + PlayerRadius)
                        {
                            puff.Burnt.Add(player.Id);
                            VentBurns++;
                        }
                    ScaldMonsters(puff);
                    continue;
                }
                foreach (var player in _players)
                {
                    if (player.IsDead || player.Riding || puff.Burnt.Contains(player.Id) || Vec2.Distance(player.Position, puff.At) > VentRadius + PlayerRadius) continue;
                    puff.Burnt.Add(player.Id);
                    VentBurns++;
                    AfflictPlayer(player, 0, HurtPlayer(player, 0, puff.Damage, kind: "김 뿜는 틈"));
                }
                ScaldMonsters(puff);
            }

            if (_players.Count == 0) return;
            _ventSeen.Clear();
            int period = Ticks(VentPeriod);
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
                for (int y = py - VentReach; y <= py + VentReach; y++)
                {
                    for (int x = px - VentReach; x <= px + VentReach; x++)
                    {
                        if (!Map.InBounds(x, y) || Map[x, y] != TileType.SteamVent) continue;
                        if ((Tick + VentPhase(x, y)) % period != 0 || !_ventSeen.Add(y * Map.Width + x)) continue;
                        Puff(x, y);
                    }
                }
            }
        }

        // A vent starts to hiss: its mark on the floor for the telegraph, then the burst.
        VentPuff Puff(int x, int y)
        {
            var at = TileMap.Center(x, y);
            int damage = (int)VentDamage;
            var hazard = new Hazard(_nextId++, 0, 0, HazardShape.Circle, at, new Vec2(0, 1), VentRadius, 0, damage, Tick, Tick + Ticks(VentTelegraph))
            {
                Mark = Hazard.Steam,
            };
            _hazards.Add(hazard);
            var puff = new VentPuff
            {
                Hazard = hazard.Id,
                At = at,
                Bursts = hazard.ResolveTick,
                Ends = hazard.ResolveTick + Ticks(VentBurst),
                Damage = damage,
            };
            _puffs.Add(puff);
            return puff;
        }

        // A monster in a burst is scalded once for a share of its health (its fire resistance counts), credited to the
        // nearest player: luring them over a vent is the fields' question.
        void ScaldMonsters(VentPuff puff)
        {
            Player by = null;
            float nearest = 24;
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                float distance = Vec2.Distance(player.Position, puff.At);
                if (distance < nearest) (by, nearest) = (player, distance);
            }
            if (by == null) return;
            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.IsDead || enemy.Away || enemy.Buried || puff.Scalded.Contains(enemy.Id)) continue;
                if (Vec2.Distance(enemy.Position, puff.At) > VentRadius + enemy.Definition.Radius * 0.5f) continue;
                puff.Scalded.Add(enemy.Id);
                float share = enemy.Definition.Size == MonsterSize.XL ? CloudXlShare : enemy.Definition.Big ? CloudBigShare : 1;
                HitEnemy(by, enemy, enemy.MaxHealth * VentMonsterShare * share, false, puff.At, SkillTag.Fire, knockback: false);
            }
        }

        // For the playtests and tests: the vent at the tile hisses now, whatever its beat.
        internal Hazard PuffVent(int x, int y)
        {
            var puff = Puff(x, y);
            return _hazards.FirstOrDefault(h => h.Id == puff.Hazard);
        }

        // ---- Cracked plates (4-1 공허의 심연: "금 간 판 위에 1초 넘게 서 있으면 꺼진다. 떨어지면 생명력 10%와 가장 가까운
        // 단단한 땅으로. 20초 뒤 다시 생긴다") ----

        public const float PlateHold = 1;
        public const float PlateBack = 20;
        public const float FallShare = 0.1f;
        // How far a fall looks for firm ground.
        public const int FallSeek = 10;
        // MonsterFx (no monster's): a plate gave way (Position: its tile), a player fell (Source: the player, Position:
        // where they came up; Target: the plate's tile as y * width + x), a plate is back (Position: its tile).
        public const int MonsterFxCrumble = 21;
        public const int MonsterFxFall = 22;
        public const int MonsterFxPlate = 23;
        const int PlateScanTicks = 30;

        // How long each plate has been stood on without a break (ticks), and when each hole becomes a plate again.
        readonly Dictionary<int, int> _plateStood = new Dictionary<int, int>();
        readonly Dictionary<int, long> _platesBack = new Dictionary<int, long>();
        readonly HashSet<int> _platesUnder = new HashSet<int>();

        // How many plates have given way, and how many falls (the playtests and tests).
        public int PlatesBroken { get; private set; }
        public int PlateFalls { get; private set; }

        // How long the plate at the tile has been stood on, in seconds (0 when no one is on it).
        public float PlateStood(int x, int y) => _plateStood.TryGetValue(y * Map.Width + x, out int ticks) ? ticks / (float)TicksPerSecond : 0;

        // When the hole at the tile is a plate again (seconds from now), or -1 for none.
        public float PlateBackIn(int x, int y) => _platesBack.TryGetValue(y * Map.Width + x, out long back) ? Math.Max(0, back - Tick) / (float)TicksPerSecond : -1;

        void UpdatePlates()
        {
            _platesUnder.Clear();
            foreach (var player in _players)
            {
                if (player.IsDead || player.Riding) continue;
                int x = TileMap.Cell(player.Position.X), y = TileMap.Cell(player.Position.Y);
                if (Map.InBounds(x, y) && Map[x, y] == TileType.CrackedPlate) _platesUnder.Add(y * Map.Width + x);
            }
            // A plate no one stands on any more starts again from nothing.
            if (_plateStood.Count > 0)
                foreach (int key in _plateStood.Keys.Where(k => !_platesUnder.Contains(k)).ToList()) _plateStood.Remove(key);
            int hold = Ticks(PlateHold);
            foreach (int key in _platesUnder)
            {
                int stood = _plateStood.GetValueOrDefault(key) + 1;
                _plateStood[key] = stood;
                if (stood >= hold) Crumble(key % Map.Width, key / Map.Width);
            }

            if (_platesBack.Count > 0)
            {
                foreach (var (key, back) in _platesBack.Where(p => Tick >= p.Value).ToList())
                {
                    _platesBack.Remove(key);
                    int x = key % Map.Width, y = key / Map.Width;
                    if (!Map.InBounds(x, y) || Map[x, y] != TileType.BrokenPlate) continue;
                    SetTile(x, y, TileType.CrackedPlate);
                    _events.Add(new SimEvent(SimEventType.MonsterFx, 0, amount: MonsterFxPlate, position: TileMap.Center(x, y)));
                }
            }
            // A hole with no time on it (a world saved while a plate was down) comes back in time too.
            if (Tick % PlateScanTicks != 0) return;
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
                for (int y = py - VentReach; y <= py + VentReach; y++)
                    for (int x = px - VentReach; x <= px + VentReach; x++)
                        if (Map.InBounds(x, y) && Map[x, y] == TileType.BrokenPlate && !_platesBack.ContainsKey(y * Map.Width + x))
                            _platesBack[y * Map.Width + x] = Tick + Ticks(PlateBack);
            }
        }

        // The plate at the tile gives way: a hole until PlateBack is over. Whoever stands on it falls; whoever only
        // brushes its edge and any monster over it is put off it.
        void Crumble(int x, int y)
        {
            int key = y * Map.Width + x;
            _plateStood.Remove(key);
            SetTile(x, y, TileType.BrokenPlate);
            _platesBack[key] = Tick + Ticks(PlateBack);
            PlatesBroken++;
            _events.Add(new SimEvent(SimEventType.MonsterFx, 0, amount: MonsterFxCrumble, position: TileMap.Center(x, y)));
            foreach (var player in _players)
            {
                if (player.IsDead || player.Riding || !Touches(player.Position, PlayerRadius, x, y)) continue;
                if (TileMap.Cell(player.Position.X) == x && TileMap.Cell(player.Position.Y) == y) Fall(player, x, y);
                else
                {
                    player.Position = Collision.Resolve(Map, player.Position, PlayerRadius, passable: DoorsFor(player));
                    player.Corrections++;
                }
            }
            foreach (var enemy in _enemies)
                if (!enemy.IsDead && Touches(enemy.Position, enemy.Definition.Radius, x, y))
                    enemy.Position = Collision.Resolve(Map, enemy.Position, enemy.Definition.Radius, monster: true);
        }

        // A player goes down through a plate: a tenth of their health (no armor, no roll), and they come up on the firm
        // ground nearest the hole.
        void Fall(Player player, int x, int y)
        {
            PlateFalls++;
            var up = FirmGround(x, y) ?? Collision.Resolve(Map, player.Position, PlayerRadius, passable: DoorsFor(player));
            player.Position = up;
            player.Corrections++;
            _events.Add(new SimEvent(SimEventType.MonsterFx, player.Id, y * Map.Width + x, MonsterFxFall, position: up));
            HurtPlayer(player, 0, Math.Max(1, player.Stats.MaxHealth * FallShare), armor: false, dodge: false, kind: "추락");
        }

        // The middle of the nearest tile within FallSeek of the hole that is walked and is not itself a plate, lava, a
        // vent or a hole, where a player fits; null when there is none.
        Vec2? FirmGround(int x, int y)
        {
            Vec2? best = null;
            float bestDistance = float.MaxValue;
            var from = TileMap.Center(x, y);
            for (int r = 1; r <= FallSeek && best == null; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int tx = x + dx, ty = y + dy;
                        if (!Map.InBounds(tx, ty) || Map.IsSolid(tx, ty) || !Firm(Map[tx, ty])) continue;
                        var at = TileMap.Center(tx, ty);
                        float distance = Vec2.Distance(at, from);
                        if (distance >= bestDistance || Collision.Overlaps(Map, at, PlayerRadius)) continue;
                        bestDistance = distance;
                        best = at;
                    }
                }
            }
            return best;
        }

        static bool Firm(TileType type) => type != TileType.CrackedPlate && type != TileType.Lava && type != TileType.SteamVent && type != TileType.Quicksand;

        // For the playtests and tests: the plate at the tile gives way now.
        internal void BreakPlate(int x, int y)
        {
            if (Map.InBounds(x, y) && Map[x, y] == TileType.CrackedPlate) Crumble(x, y);
        }
    }
}
