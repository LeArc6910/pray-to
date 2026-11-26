using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Hunting grounds (2026-11-26, Docs/HuntingGrounds.md, the owner: "일부 구역은 아주 많이 지속 스폰되는 사냥터로"): the
    // rooms the generator marked (version 8, WorldGenerator.Hunting.cs) hold no pack of the day but monsters of their
    // biome that keep coming while a player is near: a crowd at first, then one every HuntRefillSeconds up to the cap.
    // They give less experience and fewer drops each (they keep coming). Everyone gone, they go.
    public sealed partial class World
    {
        // A hunting ground fills when a player is this far past its edge or nearer, and empties when all are this far.
        public const float HuntWakeReach = 24;
        public const float HuntSleepReach = 60;
        // So many at once for one player, more for each other of the party and at night; so many come at first.
        public const int HuntCap = 10;
        public const int HuntCapPerPlayer = 3;
        public const int HuntFirst = 6;
        public const float HuntRefillSeconds = 4;
        // Never nearer a player than this when it comes.
        public const float HuntSpawnGap = 7;
        // What one of them gives against one of a room's pack (World.RewardShare).
        public const float HuntExperienceShare = 0.7f;
        public const float HuntDropShare = 0.4f;

        List<RoomNode> _huntRooms = new List<RoomNode>();
        readonly Dictionary<int, Pack> _huntPacks = new Dictionary<int, Pack>();

        // The hunting grounds of a world of rooms made by version 8 or later; none before.
        public IReadOnlyList<RoomNode> HuntingGrounds => _huntRooms;

        // The hunting ground at a spot, or null.
        public RoomNode HuntingGroundAt(Vec2 at) => _huntRooms.FirstOrDefault(r => Vec2.Distance(at, r.Center) <= r.Radius + 1);

        void SetHunts(RoomGraph rooms)
        {
            foreach (var pack in _huntPacks.Values) _packs.Remove(pack.Id);
            _huntPacks.Clear();
            _huntRooms = rooms == null ? new List<RoomNode>() : rooms.HuntingGrounds.Select(i => rooms.Nodes[i]).ToList();
        }

        void UpdateHunts()
        {
            if (_huntRooms.Count == 0 || !RoomPacks || Tick % PackCheckTicks != 0) return;
            var parties = Parties();
            if (parties.Count == 0) return;
            var players = parties.SelectMany(p => p).ToList();
            var counts = PartyCounts(parties);
            int total = FieldCount;
            foreach (var room in _huntRooms)
            {
                float nearest = players.Min(p => Vec2.Distance(p.Position, room.Center)) - room.Radius;
                _huntPacks.TryGetValue(room.Index, out var pack);
                if (nearest > HuntSleepReach)
                {
                    if (pack == null) continue;
                    Sleep(pack);
                    _packs.Remove(pack.Id);
                    _huntPacks.Remove(room.Index);
                    continue;
                }
                if (nearest > HuntWakeReach || InSafePlace(room.Center)) continue;
                if (pack == null)
                {
                    pack = new Pack(_nextPackId++, room, -1, room.Center) { Active = true, Day = Today, Shares = true, Hunt = true };
                    _packs[pack.Id] = pack;
                    _huntPacks[room.Index] = pack;
                }
                if (Tick < pack.NextRefill) continue;
                int party = PartyOf(parties, room.Center);
                int cap = HuntCap + HuntCapPerPlayer * (parties[party].Count - 1) + NightExtra;
                int living = _enemies.Count(e => e.PackId == pack.Id && !e.IsDead);
                int coming = Math.Min(cap - living, living == 0 ? HuntFirst : 1);
                coming = Math.Min(coming, Math.Min(PartyCap - counts[party], HostCap - total));
                if (coming <= 0) continue;
                int came = 0;
                for (int attempt = 0; attempt < coming * 12 && came < coming; attempt++)
                {
                    var spot = room.Center + Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI) * (_random.NextFloat() * (room.Radius - 1));
                    int x = TileMap.Cell(spot.X), y = TileMap.Cell(spot.Y);
                    if (!InRoom(room, spot) || !Clear(spot) || players.Any(p => Vec2.Distance(p.Position, spot) < HuntSpawnGap)) continue;
                    // Never a big body: they stand one or two to a room, not in a stream.
                    var choices = FieldKinds(spot, x, y).Where(k => !k.Big).ToList();
                    if (choices.Count == 0) break;
                    Join(pack, _random.PickWeighted(choices, m => m.Weight), spot, -1, ZoneLevelAt(spot), _random);
                    came++;
                }
                counts[party] += came;
                total += came;
                pack.NextRefill = Tick + Ticks(HuntRefillSeconds);
            }
        }
    }
}
