using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Where a player's play stands in the pacing director's rhythm (Docs/WorldRework.md 완급 감독).
    public enum PaceState : byte
    {
        // Building up: monsters come from out of sight now and then, and an ambush or a raid may.
        Build,
        // The tension ran high: held a few seconds as it is.
        Peak,
        // After a peak: nothing new is sent until the tension is down.
        Relax,
        // A quiet while: no wanderer, ambush or raid comes, and no wanderer heeds the player. The packs in their rooms
        // stay (whoever goes in fights).
        Rest,
    }

    // One player's place in the rhythm. Kept by the host while the world runs; never saved (상태는 저장하지 않는다).
    public sealed class Pace
    {
        internal Pace(long tick)
        {
            Since = tick;
            LastFight = long.MinValue / 2;
        }

        public PaceState State { get; internal set; }
        // 0 to World.MaxTension: hurt taken, monsters felled near, elites met; it falls once the fighting stops.
        public float Tension { get; internal set; }
        // The tick the state began.
        public long Since { get; internal set; }
        // The last tick the player was hurt, hunted or saw a monster fall near.
        public long LastFight { get; internal set; }
        // When a rest ends, when the next wanderer is sent, and when an ambush or a raid may come again (one clock).
        public long RestUntil { get; internal set; }
        public long NextSend { get; internal set; }
        public long NextSurge { get; internal set; }
        // What the director did for this player, for the playtests and --pace.
        public int Peaks { get; internal set; }
        public int Rests { get; internal set; }
        public int Sends { get; internal set; }
        public int Ambushes { get; internal set; }
        public int Raids { get; internal set; }
        // Where the player was each of the last few seconds: which way they came.
        internal readonly Vec2[] Track = new Vec2[4];
        internal int Tracked;
        internal readonly HashSet<int> Elites = new HashSet<int>();
        internal bool Started;
    }

    // The pacing director (Docs/WorldRework.md 완급 감독, 묶음 4b): [11] moved to the field. For each player a tension that
    // rises with the fighting and falls after it, and four states it goes round: building up, a peak, coming down and a
    // rest. While building up it sends a wanderer from out of sight every so often, and now and then springs an ambush
    // in a narrow way or a raid in a wide place; after a peak it sends nothing and then gives a quiet while. The packs in
    // their rooms and the elites are the map's (World.Packs.cs, World.Elites.cs): the director fills between them and
    // empties. Only the host's, by GameRandom; never in a small world (the tests' and sims'), and it acts only when the
    // host asks (WorldConfig.Director). A world of caves (version 1) is only measured: the sending, the ambushes and
    // raids and the quiet go by the rooms and ways of version 2.
    public sealed partial class World
    {
        public const float MaxTension = 100;
        // A hurt adds its share of the player's most health, in percent, times this.
        public const float HurtTension = 1.5f;
        // A monster felled this near adds this; an elite that comes after the player adds EliteTension once.
        public const float FallTension = 3;
        public const float FallReach = 6;
        public const float EliteTension = 10;
        // With no fighting for CalmSeconds the tension falls this much a second.
        public const float CalmSeconds = 5;
        public const float CalmFall = 4;
        // A monster this near that is after the player is fighting them.
        public const float FightReach = 14;
        // At PeakTension the peak, held PeakSeconds; then down, until under RelaxTension; then a rest of RestMin~RestMax.
        public const float PeakTension = 70;
        public const float PeakSeconds = 4;
        public const float RelaxTension = 25;
        public const float RestMin = 35;
        public const float RestMax = 45;
        // While building up: a wanderer sent every SendMin~SendMax seconds, from SurgeNear~SurgeFar tiles away out of
        // sight, hunting the player for HuntSeconds at most.
        public const float SendMin = 20;
        public const float SendMax = 30;
        public const float HuntSeconds = 60;
        // An ambush or a raid at most every SurgeMin~SurgeMax seconds, on one clock.
        public const float SurgeMin = 90;
        public const float SurgeMax = 180;
        // An ambush: in a way this narrow at most and this long at least, AmbushMin~AmbushMax of a pack out of the walls
        // before and behind, AmbushWarn seconds after the dust falls.
        public const int AmbushWidth = 2;
        public const int AmbushLength = 10;
        public const int AmbushMin = 4;
        public const int AmbushMax = 6;
        public const float AmbushWarn = 1.0f;
        // A raid: in a wide place, RaidMin~RaidMax from SurgeNear~SurgeFar tiles out of sight, most from behind.
        public const int RaidMin = 6;
        public const int RaidMax = 10;
        public const float SurgeNear = 18;
        public const float SurgeFar = 26;
        public const float RaidBehind = 0.75f;
        // Left alone: within this of a boss's arena, round the places and the camps, a rift or a defense, and a
        // character under DirectorLevel (their first ten minutes: the design's level 3 is reached in a minute and a half by
        // the first-hour bot, level 5 in three to four; 2026-11-22, --hour). No ambush or raid comes before the world's
        // first boss fell (World.ProgressLevel): the first chapter is as it was.
        public const float ArenaQuiet = 8;
        public const float PlaceQuiet = 12;
        public const float CampQuiet = 6;
        public const float EventQuiet = 40;
        public const int DirectorLevel = 5;
        // The events' amounts (SimEventType.Ambush): dust falling off a wall before an ambush, a raid's far howl.
        public const int AmbushDust = 0;
        public const int RaidHowl = 1;
        const int PaceTicks = 6;

        readonly Dictionary<int, Pace> _paces = new Dictionary<int, Pace>();
        // The monsters sent after a player (a wanderer, an ambush's, a raid's): whom, and until when.
        readonly Dictionary<int, (int Target, long Until)> _hunts = new Dictionary<int, (int, long)>();
        // An ambush's monsters waiting in the walls: when they come out, where, what, after whom.
        readonly List<(long At, List<(Vec2 Spot, MonsterDefinition Kind)> Members, int Target, int Zone)> _lurking =
            new List<(long, List<(Vec2, MonsterDefinition)>, int, int)>();
        GameRandom _paceRandom;
        FlowField _huntFlow;
        long _huntFlowTick = long.MinValue;
        // The tiles of the narrow ways (RoomEdge.Width ≤ AmbushWidth): tile index → the way and its step.
        Dictionary<int, (int Edge, int Step)> _narrow;

        // Whether the director acts: the host asked for it, in a big world (WorldConfig.Director).
        public bool Directs => _config.Director && SpawnsNearPlayers;

        // The pace of a player, measured in any big world (acting or not, for --pace to set the two beside each other).
        public Pace PaceOf(Player player)
        {
            if (player == null) return null;
            if (!_paces.TryGetValue(player.Id, out var pace)) _paces[player.Id] = pace = new Pace(Tick);
            return pace;
        }

        public int Hunters => _hunts.Count;

        GameRandom PaceRandom => _paceRandom ??= new GameRandom(_forageSeed, stream: 53);

        // A hurt landed on a player (HurtPlayer).
        void Strain(Player player, int amount)
        {
            if (!SpawnsNearPlayers || amount <= 0) return;
            var pace = PaceOf(player);
            pace.Tension = Math.Min(MaxTension, pace.Tension + amount * 100f / Math.Max(1f, player.Stats.MaxHealth) * HurtTension);
            pace.LastFight = Tick;
        }

        // Before the fallen leave the world: each one near a player adds to theirs.
        void NotePaceFalls()
        {
            if (!SpawnsNearPlayers) return;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsDead) continue;
                foreach (var player in _players)
                {
                    if (player.IsDead || Vec2.Distance(player.Position, enemy.Position) > FallReach) continue;
                    var pace = PaceOf(player);
                    pace.Tension = Math.Min(MaxTension, pace.Tension + FallTension);
                    pace.LastFight = Tick;
                }
            }
        }

        // Each tick, after the packs: the ambushes coming out of the walls, and every PaceTicks each player's tension and
        // state, then (acting) what each party building up gets.
        void UpdateDirector()
        {
            if (!SpawnsNearPlayers) return;
            if (_hunts.Count > 0) ForgetHunts();
            if (_lurking.Count > 0) ComeOut();
            if (Tick % PaceTicks != 0) return;
            float seconds = PaceTicks * TickSeconds;
            foreach (var player in _players)
            {
                var pace = PaceOf(player);
                if (!pace.Started)
                {
                    pace.Started = true;
                    pace.NextSend = Tick + Ticks(Between(SendMin, SendMax));
                    pace.NextSurge = Tick + Ticks(Between(SurgeMin, SurgeMax));
                }
                if (Tick % TicksPerSecond < PaceTicks)
                {
                    Array.Copy(pace.Track, 0, pace.Track, 1, pace.Track.Length - 1);
                    pace.Track[0] = player.Position;
                    pace.Tracked = Math.Min(pace.Track.Length, pace.Tracked + 1);
                }
                if (player.IsDead) continue;
                foreach (var enemy in _enemies)
                {
                    if (enemy.IsDead || enemy.TargetId != player.Id || enemy.State == EnemyState.Idle || enemy.Returning || enemy.Rift != 0
                        || Vec2.Distance(enemy.Position, player.Position) > FightReach) continue;
                    pace.LastFight = Tick;
                    if (enemy.Elite != EliteRank.None && pace.Elites.Add(enemy.Id)) pace.Tension = Math.Min(MaxTension, pace.Tension + EliteTension);
                }
                if (Tick - pace.LastFight >= Ticks(CalmSeconds)) pace.Tension = Math.Max(0, pace.Tension - CalmFall * seconds);
                Turn(pace);
            }
            if (!Directs) return;
            foreach (var party in Parties())
            {
                if (party.Any(p => PaceOf(p).State != PaceState.Build || LeftAlone(p))) continue;
                Direct(party);
            }
        }

        float Between(float least, float most) => least + PaceRandom.NextFloat() * (most - least);

        // Round the four states.
        void Turn(Pace pace)
        {
            switch (pace.State)
            {
                case PaceState.Build:
                case PaceState.Rest:
                    if (pace.Tension >= PeakTension) Enter(pace, PaceState.Peak);
                    else if (pace.State == PaceState.Rest && Tick >= pace.RestUntil) Enter(pace, PaceState.Build);
                    break;
                case PaceState.Peak:
                    if (Tick - pace.Since >= Ticks(PeakSeconds)) Enter(pace, PaceState.Relax);
                    break;
                case PaceState.Relax:
                    if (pace.Tension < RelaxTension) Enter(pace, PaceState.Rest);
                    break;
            }
        }

        void Enter(Pace pace, PaceState state)
        {
            pace.State = state;
            pace.Since = Tick;
            if (state == PaceState.Peak) pace.Peaks++;
            if (state == PaceState.Rest)
            {
                pace.Rests++;
                pace.RestUntil = Tick + Ticks(Between(RestMin, RestMax));
            }
            // Out of a rest the first wanderer comes a while later, not at once.
            if (state == PaceState.Build) pace.NextSend = Math.Max(pace.NextSend, Tick + Ticks(Between(SendMin, SendMax) / 2));
        }

        // For the playtests (Client/Playtest.cs "director <state>") and the tests: a player's state set as if it had come.
        internal void SetPace(Player player, PaceState state, float tension)
        {
            var pace = PaceOf(player);
            pace.Started = true;
            pace.Tension = Math.Clamp(tension, 0, MaxTension);
            Enter(pace, state);
            if (state == PaceState.Build)
            {
                pace.NextSend = Tick + Ticks(Between(SendMin, SendMax));
                pace.NextSurge = Tick + Ticks(Between(SurgeMin, SurgeMax));
            }
        }

        // Where the director leaves a player alone (빼는 곳).
        public bool LeftAlone(Player player)
        {
            // Nor in a dungeon (World.Dungeon.cs): it keeps its own pace.
            if (player.IsDead || player.Riding || player.Level < DirectorLevel || InPocket(player.Position)) return true;
            if (Quiet(player.Position)) return true;
            if (ActiveRift != null && Vec2.Distance(ActiveRift.Center, player.Position) <= EventQuiet) return true;
            if (ActiveDefense != null && Vec2.Distance(ActiveDefense.Center, player.Position) <= EventQuiet) return true;
            return TrapOf(player) != null;
        }

        // By a boss's arena, a place or a camp: nothing of the director's comes or stands there.
        bool Quiet(Vec2 at)
        {
            foreach (var boss in _bosses)
                if (Vec2.Distance(at, boss.Home) <= boss.ArenaRadius + ArenaQuiet) return true;
            foreach (var respawn in _bossRespawns)
                if (Vec2.Distance(at, respawn.Home) <= respawn.ArenaRadius + ArenaQuiet) return true;
            foreach (var place in Places)
                if (Vec2.Distance(at, place.Center) <= place.Radius + PlaceQuiet) return true;
            foreach (var camp in _camps)
                if (Vec2.Distance(at, camp.Center) <= CampSafeReach + CampQuiet) return true;
            return false;
        }

        // A world of rooms brings nothing new near a player who is not building up: the wanderers and clumps of
        // TrySpawnEnemy come round the others only. Not where the director leaves the player alone: there the world goes
        // as it did before it (2026-11-22: a new character at a peak got no wanderers, and the first-hour bot came to
        // level 5 a minute later). Not in a world of caves either: it has no pack rooms to go into, and a rest with no
        // clump at all stalled the first-hour bot fourteen times on a goal of felling (--hour --gen 1, seed 2).
        bool Hushed(Player player) => Directs && RoomPacks && !player.IsDead && PaceOf(player).State != PaceState.Build && !LeftAlone(player);

        // A wanderer or one the director sent heeds no player at rest: the rest is quiet (쉬기 동안 맞은 횟수 0). The
        // packs in their rooms and the elites heed everyone.
        bool Unheeded(Enemy enemy, Player player) =>
            Directs && (enemy.Wanders || _hunts.ContainsKey(enemy.Id)) && PaceOf(player).State == PaceState.Rest && !LeftAlone(player);

        // What a party building up gets: an ambush in a narrow way or a raid in a wide place once the surge clock allows
        // and no fight is on, else a wanderer every so often.
        void Direct(List<Player> party)
        {
            if (!RoomPacks) return;
            var lead = party.OrderBy(p => p.Id).First();
            var pace = PaceOf(lead);
            bool fighting = party.Any(p => Tick - PaceOf(p).LastFight < Ticks(CalmSeconds));
            if (!fighting && Tick >= pace.NextSurge && ProgressLevel > 1)
            {
                foreach (var player in party)
                {
                    if (!Ambush(player)) continue;
                    Surged(party);
                    PaceOf(player).Ambushes++;
                    return;
                }
                // A wide place is most places: a raid comes within a few seconds of the clock, giving a narrow way the
                // first chance.
                foreach (var player in party)
                {
                    if (!Wide(player.Position) || !PaceRandom.Chance(0.1f) || !Raid(player, party)) continue;
                    Surged(party);
                    PaceOf(player).Raids++;
                    return;
                }
            }
            if (Tick < pace.NextSend) return;
            long next = Tick + Ticks(Between(SendMin, SendMax));
            foreach (var player in party) PaceOf(player).NextSend = next;
            if (Send(lead, party)) pace.Sends++;
        }

        void Surged(List<Player> party)
        {
            long next = Tick + Ticks(Between(SurgeMin, SurgeMax));
            foreach (var player in party)
            {
                PaceOf(player).NextSurge = next;
                PaceOf(player).NextSend = Math.Max(PaceOf(player).NextSend, Tick + Ticks(SendMin));
            }
        }

        // Room for more within the party's cap and the host's.
        bool Room(List<Player> party, int more)
        {
            var parties = new List<List<Player>> { party };
            return PartyCounts(parties)[0] + more <= PartyCap && FieldCount + more <= HostCap;
        }

        // ---- Sending a wanderer (떠돌이가 안 보이는 곳에서 온다) ----

        bool Send(Player lead, List<Player> party)
        {
            if (!Room(party, 1)) return false;
            var spot = Unseen(lead.Position, null);
            if (spot == null) return false;
            var at = spot.Value;
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            var kinds = FieldKinds(at, x, y);
            if (kinds.Count == 0) return false;
            var kind = PaceRandom.PickWeighted(kinds, m => m.Weight);
            int zone = ZoneLevelAt(at);
            // One: the pairs of the kinds that go in packs are the wanderers' (TrySpawnEnemy), not the director's.
            var enemy = SpawnEnemy(kind, at);
            ToughenForZone(enemy, zone);
            enemy.Wanders = true;
            Hunt(enemy, lead);
            return true;
        }

        void Hunt(Enemy enemy, Player player) => _hunts[enemy.Id] = (player.Id, Tick + Ticks(HuntSeconds));

        // Open floor SurgeNear~SurgeFar tiles from the player that no player sees (every one at least SurgeNear off),
        // walked to from the player within twice that, out of the quiet places and off the roads; toward a way when one
        // is given (within 50 degrees, then anywhere). Null when there is none.
        Vec2? Unseen(Vec2 from, Vec2? toward)
        {
            BuildHuntFlow(new[] { from });
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = toward is Vec2 way && attempt < 24
                    ? MathF.Atan2(way.Y, way.X) + (PaceRandom.NextFloat() * 2 - 1) * 50 * MathF.PI / 180
                    : PaceRandom.NextFloat() * 2 * MathF.PI;
                var at = from + Vec2.FromAngle(angle) * Between(SurgeNear, SurgeFar);
                int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
                if (!Map.InBounds(x, y) || Map.IsSolid(x, y) || Map[x, y] == TileType.Lava || Map[x, y] == TileType.Water) continue;
                at = TileMap.Center(x, y);
                if (Collision.Overlaps(Map, at, 0.45f, monster: true)) continue;
                if (_players.Any(p => !p.IsDead && Vec2.Distance(p.Position, at) < SurgeNear)) continue;
                if (Quiet(at) || InSafePlace(at) || !WandererSpot(x, y)) continue;
                int steps = _huntFlow.DistanceAt(x, y);
                if (steps == FlowField.Unreached || steps > SurgeFar * 2) continue;
                return at;
            }
            return null;
        }

        // ---- The monsters sent after someone ----

        // The way to the hunted, made again every half second while any is hunted.
        void BuildHuntFlow(IEnumerable<Vec2> to)
        {
            _huntFlow ??= new FlowField(Map);
            _huntFlow.Build(to, (int)(SurgeFar * 2.5f));
            _huntFlowTick = Tick;
        }

        // Hunts that ran out, or whose monster or player is gone: the monster goes on as a wanderer where it is.
        void ForgetHunts()
        {
            foreach (var pair in _hunts.ToList())
            {
                var enemy = FindEnemy(pair.Key);
                var player = FindPlayer(pair.Value.Target);
                if (enemy != null && !enemy.IsDead && player != null && !player.IsDead && Tick < pair.Value.Until && !InSafePlace(player.Position)
                    && PaceOf(player).State != PaceState.Rest) continue;
                _hunts.Remove(pair.Key);
                if (enemy == null) continue;
                enemy.Home = enemy.Position;
                enemy.Trail.Clear();
            }
        }

        // One sent after a player and idle (out of its sight) walks to them by the way; true when it did.
        bool HuntOn(Enemy enemy)
        {
            if (!_hunts.TryGetValue(enemy.Id, out var hunt)) return false;
            var player = FindPlayer(hunt.Target);
            if (player == null || player.IsDead) return false;
            if (Tick - _huntFlowTick >= 15) BuildHuntFlow(_hunts.Values.Select(h => FindPlayer(h.Target)).Where(p => p != null && !p.IsDead).Select(p => p.Position).Distinct());
            var way = _huntFlow.DirectionFrom(enemy.Position);
            if (way.LengthSquared < 1e-6f) way = (player.Position - enemy.Position).Normalized;
            enemy.Facing = way;
            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, way * (enemy.Speed * TickSeconds), monster: true);
            return true;
        }

        // ---- The ambush (매복): out of the walls of a narrow way, before and behind ----

        // The narrow ways' tiles, made once from the rooms.
        void MapNarrow()
        {
            _narrow = new Dictionary<int, (int, int)>();
            if (_rooms == null) return;
            for (int index = 0; index < _rooms.Edges.Count; index++)
            {
                var edge = _rooms.Edges[index];
                if (edge.Kind != RoomEdgeKind.Way || edge.Width > AmbushWidth || edge.Path == null || edge.Path.Count < AmbushLength) continue;
                var a = _rooms.Nodes[edge.A];
                var b = _rooms.Nodes[edge.B];
                for (int step = 0; step < edge.Path.Count; step++)
                {
                    var (x, y) = edge.Path[step];
                    var at = TileMap.Center(x, y);
                    if (Vec2.Distance(at, a.Center) <= a.Radius + 1 || Vec2.Distance(at, b.Center) <= b.Radius + 1) continue;
                    _narrow[y * Map.Width + x] = (index, step);
                }
            }
        }

        // The narrow way a player stands in, and the step of it: on its tiles (or one off) with the rock close round.
        (int Edge, int Step)? NarrowAt(Vec2 position)
        {
            if (_narrow == null) MapNarrow();
            int px = TileMap.Cell(position.X), py = TileMap.Cell(position.Y);
            (int, int)? found = null;
            for (int dy = -1; dy <= 1 && found == null; dy++)
                for (int dx = -1; dx <= 1 && found == null; dx++)
                    if (Map.InBounds(px + dx, py + dy) && _narrow.TryGetValue((py + dy) * Map.Width + px + dx, out var way)) found = way;
            if (found == null) return null;
            // A way laid narrow may have opened into a cave since: rock close round still (open ≤ 14 of 25).
            int open = 0;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (Map.InBounds(px + dx, py + dy) && !Map.IsSolid(px + dx, py + dy)) open++;
            return open <= 14 ? found : null;
        }

        // Open floor round the player: a wide room or a wide way (half of the 9×9 round them and more).
        bool Wide(Vec2 position)
        {
            int px = TileMap.Cell(position.X), py = TileMap.Cell(position.Y), open = 0;
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                    if (Map.InBounds(px + dx, py + dy) && !Map.IsSolid(px + dx, py + dy) && Map[px + dx, py + dy] != TileType.Water && Map[px + dx, py + dy] != TileType.Lava) open++;
            return open >= 50;
        }

        // Which way along a way the player goes, by where they were a few seconds ago (1 or -1), else 1.
        int AlongWay(Pace pace, RoomEdge edge, int step)
        {
            var heading = Heading(pace);
            if (heading.LengthSquared < 1e-4f) return 1;
            int ahead = Math.Min(edge.Path.Count - 1, step + 2), behind = Math.Max(0, step - 2);
            var along = TileMap.Center(edge.Path[ahead].X, edge.Path[ahead].Y) - TileMap.Center(edge.Path[behind].X, edge.Path[behind].Y);
            return Vec2.Dot(along, heading) >= 0 ? 1 : -1;
        }

        // Where the player has been going these last seconds; zero when standing.
        static Vec2 Heading(Pace pace)
        {
            if (pace.Tracked < 2) return Vec2.Zero;
            var way = pace.Track[0] - pace.Track[pace.Tracked - 1];
            return way.LengthSquared < 1 ? Vec2.Zero : way.Normalized;
        }

        // Ambushes in the narrow way the player is in, if it has room before and behind: AmbushMin~AmbushMax of the
        // way's biome's pack, split between a wall six steps ahead and one five behind. The dust falls now; they come
        // out AmbushWarn later (ComeOut).
        internal bool Ambush(Player player)
        {
            if (!RoomPacks || !(NarrowAt(player.Position) is (int index, int step))) return false;
            var edge = _rooms.Edges[index];
            var pace = PaceOf(player);
            int way = AlongWay(pace, edge, step);
            int front = step + 6 * way, back = step - 5 * way;
            if (Math.Min(front, back) < 1 || Math.Max(front, back) > edge.Path.Count - 2) return false;
            var choices = PackChoices(player.Position, Biomes.At(player.Position), big: false);
            if (choices.Count == 0) return false;
            int count = AmbushMin + PaceRandom.Next(AmbushMax - AmbushMin + 1);
            var party = Parties().FirstOrDefault(p => p.Contains(player)) ?? new List<Player> { player };
            if (!Room(party, count)) return false;
            var roster = Roster(PaceRandom.PickWeighted(choices, k => k.Weight), count);
            var members = new List<(Vec2, MonsterDefinition)>();
            int half = (roster.Count + 1) / 2;
            foreach (var (at, from, to) in new[] { (front, 0, half), (back, half, roster.Count) })
            {
                var (x, y) = edge.Path[at];
                var wall = WallBeside(x, y);
                _events.Add(new SimEvent(SimEventType.Ambush, player.Id, amount: AmbushDust, position: wall ?? TileMap.Center(x, y)));
                for (int i = from; i < to; i++)
                {
                    // Along the way about the spot, the first by the wall.
                    int along = Math.Clamp(at + (i - from - (to - from) / 2) * way, 0, edge.Path.Count - 1);
                    var spot = Collision.Resolve(Map, TileMap.Center(edge.Path[along].X, edge.Path[along].Y), roster[i].Radius, monster: true);
                    members.Add((spot, roster[i]));
                }
            }
            _lurking.Add((Tick + Ticks(AmbushWarn), members, player.Id, ZoneLevelAt(player.Position)));
            return true;
        }

        // The middle of a rock tile beside a floor tile (the wall the dust falls off), or null.
        Vec2? WallBeside(int x, int y)
        {
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (-1, 1), (1, -1), (-1, -1) })
                if (Map.InBounds(x + dx, y + dy) && Map.IsSolid(x + dx, y + dy)) return TileMap.Center(x + dx, y + dy) - new Vec2(dx, dy) * 0.4f;
            return null;
        }

        // count monsters of a pack's kinds: each member its least, then round its members again up to count.
        List<MonsterDefinition> Roster(PackDefinition pack, int count)
        {
            var roster = new List<MonsterDefinition>();
            foreach (var member in pack.Members)
                for (int i = 0; i < member.Min && roster.Count < count; i++) roster.Add(member.Monster);
            for (int i = 0; roster.Count < count && i < 64; i++)
            {
                var member = pack.Members[i % pack.Members.Count];
                if (roster.Count(m => m == member.Monster) < Math.Max(member.Max, 2)) roster.Add(member.Monster);
                else if (i >= pack.Members.Count * 4) roster.Add(pack.Members[0].Monster);
            }
            return roster;
        }

        // The ambushes whose time came: out of the walls, after the player, a pack together.
        void ComeOut()
        {
            for (int i = _lurking.Count - 1; i >= 0; i--)
            {
                var (at, members, target, zone) = _lurking[i];
                if (Tick < at) continue;
                _lurking.RemoveAt(i);
                var player = FindPlayer(target);
                if (player == null || player.IsDead) continue;
                Band(members, player, zone, player.Position);
            }
        }

        // A band the director sends: one pack (they notice together), sent after the player, already coming.
        Pack Band(List<(Vec2 Spot, MonsterDefinition Kind)> members, Player player, int zone, Vec2 home)
        {
            var band = new List<Enemy>();
            foreach (var (spot, kind) in members)
            {
                if (Collision.Overlaps(Map, spot, kind.Radius, monster: true)) continue;
                var enemy = SpawnEnemy(kind, spot);
                ToughenForZone(enemy, zone);
                enemy.Wanders = true;
                enemy.TargetId = player.Id;
                enemy.State = EnemyState.Chase;
                Hunt(enemy, player);
                band.Add(enemy);
            }
            if (band.Count == 0) return null;
            var pack = Gather(band);
            pack.Home = home;
            pack.Alerted = true;
            return pack;
        }

        // ---- The raid (습격): a pack from far off, running in ----

        internal bool Raid(Player player, List<Player> party = null)
        {
            if (!RoomPacks) return false;
            party ??= Parties().FirstOrDefault(p => p.Contains(player)) ?? new List<Player> { player };
            // Fewer near the spawn: RaidMin in the first two zones' first, two more a zone on up to RaidMax (2026-11-22,
            // --hour: ten slimes at a level 6 bot in copper felled it again and again).
            int zone = ZoneLevelAt(player.Position);
            int most = Math.Min(RaidMax, RaidMin + 2 * Math.Max(0, zone - 1));
            int count = RaidMin + PaceRandom.Next(most - RaidMin + 1) + PackExtraPerPlayer * (party.Count - 1);
            if (!Room(party, count)) return false;
            // Most from behind: the way the player came.
            var heading = Heading(PaceOf(player));
            Vec2? toward = heading.LengthSquared > 0 && PaceRandom.Chance(RaidBehind) ? heading * -1 : (Vec2?)null;
            var spot = Unseen(player.Position, toward);
            if (spot == null) return false;
            var choices = PackChoices(spot.Value, Biomes.At(spot.Value), big: true);
            if (choices.Count == 0) return false;
            var roster = Roster(PaceRandom.PickWeighted(choices, k => k.Weight), count);
            var spots = Spread(spot.Value, new List<Vec2>(), roster.Count, PaceRandom,
                at => Map.InBounds(TileMap.Cell(at.X), TileMap.Cell(at.Y)) && !Map.IsSolid(TileMap.Cell(at.X), TileMap.Cell(at.Y))
                    && !Collision.Overlaps(Map, at, 0.45f, monster: true) && !InSafePlace(at) && _players.All(p => Vec2.Distance(p.Position, at) >= SurgeNear - 4));
            if (spots.Count < RaidMin) return false;
            _events.Add(new SimEvent(SimEventType.Ambush, player.Id, amount: RaidHowl, position: spot.Value));
            var band = Band(spots.Select((s, i) => (s, roster[i])).ToList(), player, ZoneLevelAt(spot.Value), player.Position);
            return band != null;
        }
    }
}
