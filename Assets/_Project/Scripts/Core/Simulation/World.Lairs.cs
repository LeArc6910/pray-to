using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The rest of B1 (2026-11-24, Docs/BiomeRework.md 묶음 B1c) that is the field's: a mid-boss in its lair (the only XL out
    // of a dungeon, in a room with seven by seven of open floor, 사람이 정한 것) and a big beetle that smashes the dirt
    // cave's stalagmites and pit props when its charge runs into one. The camps' cellars are World.Dungeon.cs'.
    public sealed partial class World
    {
        // A lair's minions with its mid-boss, of the biome's own packs (not their big ones).
        public const int LairMinionsLeast = 2;
        public const int LairMinionsMost = 4;
        // MonsterFx: a charge smashed a prop (Position: the prop's tile).
        public const int MonsterFxSmash = 15;

        // The mid-boss a biome's lairs hold: the master of the biome's own dungeon when that is a monster in person
        // (dungeons.csv boss, an XL), else null.
        public MonsterDefinition LairBoss(Biome biome)
        {
            var row = DungeonSheetData?.For(biome);
            if (row == null || !row.Master || row.Biome != biome) return null;
            var kind = row.MasterKind(_monsters);
            return kind != null && kind.Size == MonsterSize.XL ? kind : null;
        }

        // A lair's roster for the day (Muster): its mid-boss as a leader with one affix, on the open floor nearest the
        // room's middle where its body fits, and a few of the biome's pack round it. False when the room no longer holds
        // it (built on, dug) or the biome has no mid-boss: then it holds an ordinary pack.
        bool MusterLair(Pack pack, GameRandom random)
        {
            var room = pack.Room;
            var boss = LairBoss(room.Biome);
            if (boss == null || !RoomHolds(room, boss)) return false;
            bool Fits(Vec2 at) => InRoom(room, at) && Clear(at) && !NearEntrance(room, at);
            var middle = TileMap.Center(room.AnchorX, room.AnchorY);
            var spot = BigSpot(boss, middle, room.Radius, Fits, new List<(Vec2, float)>());
            if (spot == null) return false;
            pack.Elite = EliteRank.Leader;
            pack.Affixes = RollAffixes(LeaderAffixes, 1, random);
            pack.Roster.Add(boss);
            pack.Spots.Add(spot.Value);
            var minions = pack.Definition.Members.Select(m => m.Monster).Where(m => !m.Big).ToList();
            int count = LairMinionsLeast + random.Next(LairMinionsMost - LairMinionsLeast + 1);
            float clear = boss.Radius + MonsterSizes.SlimRadius + MonsterSizes.PackGap;
            var marks = _rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Monster).Select(m => TileMap.Center(m.X, m.Y)).ToList();
            var around = Spread(spot.Value, marks, count, random, at => Fits(at) && Vec2.Distance(at, spot.Value) >= Math.Max(PackSpacingMin, clear));
            for (int i = 0; i < around.Count && minions.Count > 0; i++)
            {
                pack.Roster.Add(minions[random.Next(minions.Count)]);
                pack.Spots.Add(around[i]);
            }
            pack.Fallen = new bool[pack.Roster.Count];
            return true;
        }

        // The props a charge breaks (4-2 큰 딱정벌레: "갱목·석순에 부딪히면 부수고 비틀거린다").
        public static bool Smashed(TileType type) => type == TileType.Stalagmite || type == TileType.PitProp;

        // A big body's charge ran into something: the props it smashes in front of it go (changed tiles, as a pickaxe
        // would leave them, nothing dropped). The pocket is never changed. True when any went.
        bool SmashAhead(Enemy enemy)
        {
            if (!enemy.Definition.Big) return false;
            var front = enemy.Position + enemy.ChargeDirection * (enemy.Definition.Radius + 0.45f);
            float reach = Math.Max(0.6f, enemy.Definition.Radius * 0.8f);
            bool any = false;
            int x0 = TileMap.Cell(front.X - reach), x1 = TileMap.Cell(front.X + reach), y0 = TileMap.Cell(front.Y - reach), y1 = TileMap.Cell(front.Y + reach);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!Map.InBounds(x, y) || Map.IsEdge(x, y) || !Smashed(Map[x, y]) || !Touches(front, reach, x, y)) continue;
                    SetTile(x, y, TileType.Floor);
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSmash, position: TileMap.Center(x, y)));
                    any = true;
                }
            }
            return any;
        }
    }
}
