using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What kind of field elite a monster is (Docs/WorldRework.md 들판 정예).
    public enum EliteRank : byte
    {
        None,
        // The head of a leader's pack: much tougher, its big attack long-warned and heavy, a name over its head.
        Leader,
        // One of three of a kind sharing one affix, fighting together with no leader.
        Champion,
    }

    // Field elites (Docs/WorldRework.md 들판 정예, 묶음 2-2): the camp rooms (RoomRole.Elite) hold a leader's pack or a
    // band of champions instead of an ordinary pack. The dangerous fights of the field are theirs: an ordinary monster is
    // worn down, an elite's big attack is dodged. The rift's four affixes and the field's four (RiftAffix).
    public sealed partial class World
    {
        // Seven camps in ten hold a leader's pack, three a band of champions.
        public const float ChampionShare = 0.3f;
        public const int ChampionCount = 3;
        // A leader's minions, before the swarm affix adds half again.
        public const int MinMinions = 3;
        public const int MaxMinions = 5;
        // A leader has a second affix half the time from this zone on; nearer the spawn one (the first boss's way).
        public const int SecondAffixZone = 3;
        // Body: health and hits against the monster as its zone made it. A leader has four times its health, not the
        // design's six (2026-11-22, --fight --rooms --camps: at six a lake crab's leader took a warrior in iron two to
        // three minutes, five times a pack).
        public const float LeaderHealth = 4;
        public const float BigLeaderHealth = 2;
        // Hits (2026-11-25, Docs/Difficulty.md): a leader's and a champion's usual hit about 10% of the health of a player
        // in the band's gear (35~40% with none), against an ordinary monster's 6%; a big (L) leader's a little over its
        // own kind's 10%.
        public const float LeaderPower = 1.7f;
        public const float BigLeaderPower = 1.2f;
        public const float ChampionHealth = 2.5f;
        public const float ChampionPower = 1.7f;
        // The affixes' numbers: a third faster, half again the health, a third harder, half again the minions.
        public const float SwiftShare = 1.33f;
        public const float ToughShare = 1.5f;
        public const float FierceShare = 1.33f;
        public const float SwarmShare = 1.5f;
        // A leader's big attack (BigAttackOf) warns at least this long, strongly, and hits this much harder again: a
        // hit of 35~45% of the health of a player of its stage (Docs/WorldRework.md 소모전과 회피전).
        public const float EliteTelegraph = 1.0f;
        public const float BigHitShare = 1.6f;
        // Fire: what it walked through burns this long. Frost: a circle this wide under it every FrostSeconds, going off
        // FrostFuse later for this share of its damage, chilling. Guard: its minions this near take this share less.
        // Summon: at these shares of its health it calls this many more.
        public const float FireSeconds = 2;
        public const float FrostSeconds = 4;
        public const float FrostFuse = 1.2f;
        public const float FrostRadius = 1.6f;
        public const float FrostShare = 0.8f;
        public const float GuardReach = 5;
        public const float EliteGuardShare = 0.4f;
        public const int SummonCount = 3;
        static readonly float[] SummonAt = { 0.66f, 0.33f };
        // What a felled elite gives: a leader's experience is eight times its kind's, a champion's three times; a leader
        // drops a rare or better piece, a rune this often and a potion; a champion a magic or better piece this often.
        public const int LeaderExperience = 8;
        public const int ChampionExperience = 3;
        // A mid-boss (XL) leading, a dungeon's master or a lair's (B1c): a body of its own already, so fewer times its
        // kind's (Docs/BiomeRework.md 4-5: a run's experience a minute at 1.0~1.3 times the field's).
        public const int MidBossExperience = 4;
        public const float LeaderRuneChance = 0.3f;
        public const float ChampionItemChance = 0.4f;
        // A MonsterFx event's Amount: an elite called its minions.
        public const int MonsterFxCall = 14;

        static readonly RiftAffix[] LeaderAffixes =
            { RiftAffix.Swift, RiftAffix.Tough, RiftAffix.Fierce, RiftAffix.Swarm, RiftAffix.Fire, RiftAffix.Frost, RiftAffix.Guard, RiftAffix.Summon };
        // Champions have no minions to guard, call or swell.
        static readonly RiftAffix[] ChampionAffixes = { RiftAffix.Swift, RiftAffix.Tough, RiftAffix.Fierce, RiftAffix.Fire, RiftAffix.Frost };

        // ---- Names (Docs/WorldRework.md: 접사 + 종 + 대장) ----

        public static string AffixName(RiftAffix affix) => affix switch
        {
            RiftAffix.Swift => "빠름",
            RiftAffix.Tough => "단단함",
            RiftAffix.Fierce => "사나움",
            RiftAffix.Swarm => "떼",
            RiftAffix.Fire => "불길",
            RiftAffix.Frost => "얼음 폭발",
            RiftAffix.Guard => "수호",
            _ => "부름",
        };

        // How the affix goes before a name.
        static string AffixPrefix(RiftAffix affix) => affix switch
        {
            RiftAffix.Swift => "빠른",
            RiftAffix.Tough => "단단한",
            RiftAffix.Fierce => "사나운",
            RiftAffix.Swarm => "떼 거느린",
            RiftAffix.Fire => "불길의",
            RiftAffix.Frost => "얼음의",
            RiftAffix.Guard => "수호의",
            _ => "부름의",
        };

        static IEnumerable<RiftAffix> Each(RiftAffix affixes) => LeaderAffixes.Where(a => (affixes & a) != 0);

        // "불길의 버섯 병정 대장" for a leader, "사나운 모래 전갈" for a champion: the last affix (the field's before the
        // rift's) before the kind's name. The screen and the tests make it from the same marks, so no name is sent.
        public static string EliteName(MonsterDefinition kind, EliteRank rank, RiftAffix affixes)
        {
            if (kind == null || rank == EliteRank.None) return null;
            var named = Each(affixes).LastOrDefault();
            string name = named == RiftAffix.None ? kind.Name : $"{AffixPrefix(named)} {kind.Name}";
            // A mid-boss (XL) leads under its own name: "어미 슬라임", not "어미 슬라임 대장" (B1c).
            return rank == EliteRank.Leader && kind.Size != MonsterSize.XL ? name + " 대장" : name;
        }

        // Under the name: every affix, "불길 · 단단함".
        public static string EliteAffixLine(RiftAffix affixes) => string.Join(" · ", Each(affixes).Select(AffixName));

        // ---- The big attack ----

        // The one attack of its kind an elite leader makes its big one: the heaviest that marks an area or rushes
        // (not a shot, which is not walked out of the same way), and of those the longest warned. Never a bomber's
        // blast (a leader does not blow itself up) or an ambush (made once, from under the ground). Null for a kind with
        // no such attack.
        public static MonsterAttack BigAttackOf(MonsterDefinition kind)
        {
            MonsterAttack best = null;
            if (kind == null) return null;
            foreach (var attack in kind.Attacks)
            {
                if (attack.Damage <= 0 || attack.Tell != Tell.Area && attack.Tell != Tell.Dash) continue;
                if (attack.Shape == MonsterAttackShape.Explode || attack.Shape == MonsterAttackShape.Ambush || attack.Shape == MonsterAttackShape.Mend
                    || attack.Shape == MonsterAttackShape.Empower) continue;
                if (best == null || attack.Damage > best.Damage || attack.Damage == best.Damage && attack.Telegraph > best.Telegraph) best = attack;
            }
            return best;
        }

        public static bool IsBigAttack(EliteRank rank, MonsterDefinition kind, MonsterAttack attack) =>
            rank == EliteRank.Leader && attack != null && attack == BigAttackOf(kind);

        // Its warning: a leader's big attack at least EliteTelegraph long; anything else as the sheet says. The host and
        // the screen both go by this.
        public static float TelegraphOf(EliteRank rank, MonsterDefinition kind, MonsterAttack attack) =>
            IsBigAttack(rank, kind, attack) ? Math.Max(EliteTelegraph, attack.Telegraph) : attack.Telegraph;

        public static AttackWarning WarningOf(EliteRank rank, MonsterDefinition kind, MonsterAttack attack) =>
            IsBigAttack(rank, kind, attack) ? AttackWarning.Strong : attack.Warning;

        float TelegraphOf(Enemy enemy, MonsterAttack attack) => TelegraphOf(enemy.Elite, enemy.Definition, attack);

        // ---- The camp's elites ----

        // The kind that leads a pack: the first of its sturdy, heavy, shooting or helping kinds, in that order; a pack of
        // one kind (slimes) is led by one of them.
        public static MonsterDefinition LeaderKind(PackDefinition pack, bool big = false)
        {
            int Rank(MonsterRole role) => role switch
            {
                MonsterRole.Heavy => 0,
                MonsterRole.Bruiser => 1,
                MonsterRole.Shooter => 2,
                MonsterRole.Support => 3,
                MonsterRole.None => 4,
                _ => 5,
            };
            // A big body (L) leads only where asked (a camp whose room holds it, B1b: a big beetle, a cap giant); a dungeon's
            // leaders stand where an M body does. Never an XL: the field has none (Docs/BiomeRework.md 사람이 정한 것).
            var members = pack.Members.Select(m => m.Monster).Where(m => big ? m.Size != MonsterSize.XL : !m.Big).DefaultIfEmpty(pack.Members[0].Monster);
            // Of the same rank, the big one first.
            return members.OrderBy(m => Rank(m.Role)).ThenByDescending(m => m.Big).First();
        }

        static RiftAffix RollAffixes(RiftAffix[] from, int count, GameRandom random)
        {
            var open = from.ToList();
            var affixes = RiftAffix.None;
            for (int i = 0; i < count && open.Count > 0; i++)
            {
                var pick = open[random.Next(open.Count)];
                open.Remove(pick);
                affixes |= pick;
            }
            return affixes;
        }

        // A camp's roster for the day (Muster, for a RoomRole.Elite room): its rank, affixes and monsters, the leader
        // first, standing on the chunk's E (or the room's middle) with the rest round it on the M marks.
        void MusterElite(Pack pack, GameRandom random)
        {
            var room = pack.Room;
            pack.Elite = random.NextFloat() < ChampionShare ? EliteRank.Champion : EliteRank.Leader;
            // A leader may be a big body where the room holds it (B1b); a band of champions never is (three big bodies).
            var lead = LeaderKind(pack.Definition, big: pack.Elite == EliteRank.Leader);
            if (!RoomHolds(room, lead)) lead = LeaderKind(pack.Definition);
            if (pack.Elite == EliteRank.Champion)
            {
                pack.Affixes = RollAffixes(ChampionAffixes, 1, random);
                for (int i = 0; i < ChampionCount; i++) pack.Roster.Add(lead);
            }
            else
            {
                int count = ZoneLevelAt(room.Center) >= SecondAffixZone && random.Chance(0.5f) ? 2 : 1;
                pack.Affixes = RollAffixes(LeaderAffixes, count, random);
                // Its minions: of the pack's own make, one of the leader's kind fewer, shuffled.
                var ordinary = new List<MonsterDefinition>();
                foreach (var member in pack.Definition.Members)
                {
                    int n = member.Min + random.Next(member.Max - member.Min + 1);
                    for (int i = 0; i < n; i++) ordinary.Add(member.Monster);
                }
                ordinary.RemoveAll(k => k.Big);
                ordinary.Remove(lead);
                for (int i = ordinary.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (ordinary[i], ordinary[j]) = (ordinary[j], ordinary[i]);
                }
                int minions = MinMinions + random.Next(MaxMinions - MinMinions + 1);
                if ((pack.Affixes & RiftAffix.Swarm) != 0) minions = (int)MathF.Round(minions * SwarmShare);
                pack.Roster.Add(lead);
                for (int i = 0; i < minions; i++) pack.Roster.Add(i < ordinary.Count ? ordinary[i] : MinionKind(pack.Definition, lead));
            }

            bool Fits(Vec2 at) => InRoom(room, at) && Clear(at) && !NearEntrance(room, at);
            var camp = _rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Elite).Select(m => TileMap.Center(m.X, m.Y)).ToList();
            var middle = camp.Count > 0 ? camp[0] : TileMap.Center(room.AnchorX, room.AnchorY);
            List<Vec2> head;
            if (lead.Big)
            {
                // A big leader where its body fits nearest the camp's mark, its minions clear of it (World.Sizes.cs).
                var bigSpot = BigSpot(lead, middle, room.Radius, Fits, new List<(Vec2, float)>());
                head = bigSpot == null ? new List<Vec2>() : new List<Vec2> { bigSpot.Value };
                if (head.Count == 0)
                {
                    // No room for it after all: an M leader of the pack.
                    lead = LeaderKind(pack.Definition);
                    for (int i = 0; i < pack.Roster.Count; i++)
                        if (pack.Roster[i].Big) pack.Roster[i] = lead;
                }
            }
            else head = null;
            head ??= Fits(middle) ? new List<Vec2> { middle } : Spread(middle, new List<Vec2>(), 1, random, Fits);
            if (head.Count == 0) return;
            var marks = _rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Monster).Select(m => TileMap.Center(m.X, m.Y)).ToList();
            pack.Spots.Add(head[0]);
            float clear = lead.Big ? lead.Radius + MonsterSizes.SlimRadius + MonsterSizes.PackGap : PackSpacingMin;
            pack.Spots.AddRange(Spread(head[0], marks, pack.Roster.Count - 1, random, at => Fits(at) && Vec2.Distance(at, head[0]) >= Math.Max(PackSpacingMin, clear)));
            Crowd(pack, middle, random);
        }

        // What a leader's minions are when the pack's own run out, and what it calls: the pack's swarm (its first kind),
        // or another of the leader's own when that is all the pack is.
        static MonsterDefinition MinionKind(PackDefinition pack, MonsterDefinition lead) =>
            pack.Members.Select(m => m.Monster).FirstOrDefault(m => m != lead && !m.Big) ?? lead;

        // An elite's body: as its zone made it, then its rank and its affixes.
        void MakeElite(Enemy enemy, EliteRank rank, RiftAffix affixes)
        {
            enemy.Elite = rank;
            enemy.Affixes = affixes;
            // A big leader (L, B1b) is already four times an M body: twice its own, not four times.
            // A mid-boss (XL, a dungeon's master or a lair's, B1c) is a fight of its own already: its health and hits as
            // they are, with the affixes on top.
            bool xl = enemy.Definition.Size == MonsterSize.XL;
            float health = rank == EliteRank.Leader ? xl ? 1 : enemy.Definition.Big ? BigLeaderHealth : LeaderHealth : ChampionHealth;
            float power = rank == EliteRank.Leader ? xl ? 1 : enemy.Definition.Big ? BigLeaderPower : LeaderPower : ChampionPower;
            if ((affixes & RiftAffix.Tough) != 0) health *= ToughShare;
            if ((affixes & RiftAffix.Fierce) != 0) power *= FierceShare;
            if ((affixes & RiftAffix.Swift) != 0) enemy.SpeedScale *= SwiftShare;
            enemy.MaxHealth = enemy.Health = enemy.MaxHealth * health;
            enemy.Power *= power;
            enemy.FrostTicks = Ticks(FrostSeconds);
        }

        // ---- What the affixes do, each tick after the monsters have moved ----

        void UpdateElites()
        {
            int count = _enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var enemy = _enemies[i];
                if (enemy.Elite == EliteRank.None || enemy.IsDead) continue;
                bool fighting = enemy.State != EnemyState.Idle && !enemy.Returning && FindPlayer(enemy.TargetId) is Player target && !target.IsDead;
                if ((enemy.Affixes & RiftAffix.Frost) != 0 && fighting && !enemy.Away && --enemy.FrostTicks <= 0)
                {
                    enemy.FrostTicks = Ticks(FrostSeconds);
                    _hazards.Add(new Hazard(_nextId++, enemy.Id, 0, HazardShape.Circle, enemy.Position, enemy.Facing, FrostRadius, 0,
                        Math.Max(1, (int)MathF.Round(enemy.Damage * FrostShare)), Tick, Tick + Ticks(FrostFuse))
                    {
                        Mark = (byte)((int)MonsterAttackShape.Delayed + 1),
                        Effect = HitEffect.Chill,
                        EffectAmount = PlayerChillSeconds,
                    });
                }
                if ((enemy.Affixes & RiftAffix.Summon) != 0 && enemy.Elite == EliteRank.Leader)
                {
                    while (enemy.Summons < SummonAt.Length && enemy.Health < enemy.MaxHealth * SummonAt[enemy.Summons])
                    {
                        enemy.Summons++;
                        CallMinions(enemy);
                    }
                }
            }
        }

        // SummonCount more of its pack's minions round it, after whoever it is after.
        void CallMinions(Enemy leader)
        {
            var pack = PackOf(leader);
            var kind = pack?.Definition != null ? MinionKind(pack.Definition, leader.Definition)
                : _enemies.FirstOrDefault(e => e.PackId != 0 && e.PackId == leader.PackId && e != leader)?.Definition ?? leader.Definition;
            int zone = ZoneLevelAt(leader.Position);
            float start = _random.NextFloat() * 2 * MathF.PI;
            for (int i = 0; i < SummonCount; i++)
            {
                var at = Collision.Resolve(Map, leader.Position + Vec2.FromAngle(start + 2 * MathF.PI * i / SummonCount) * 1.5f, kind.Radius, monster: true);
                if (Collision.Overlaps(Map, at, kind.Radius, monster: true)) continue;
                var minion = SpawnEnemy(kind, at);
                ToughenForZone(minion, zone);
                minion.PackId = leader.PackId;
                minion.PackSlot = -1;
                minion.Home = pack?.Home ?? leader.Position;
                minion.TargetId = leader.TargetId;
                if (FindPlayer(leader.TargetId) != null) minion.State = EnemyState.Chase;
            }
            _events.Add(new SimEvent(SimEventType.MonsterFx, leader.Id, amount: MonsterFxCall, position: leader.Position));
        }

        // The share less a minion takes with its guarding leader near (RiftAffix.Guard), else 0.
        float EliteGuard(Enemy enemy) => GuardOf(enemy) != null ? EliteGuardShare : 0;

        // The living leader with the guard affix of its pack within GuardReach of it, or null.
        public Enemy GuardOf(Enemy enemy)
        {
            if (enemy.PackId == 0 || enemy.Elite != EliteRank.None) return null;
            foreach (var other in _enemies)
            {
                if (other.PackId == enemy.PackId && other.Elite == EliteRank.Leader && (other.Affixes & RiftAffix.Guard) != 0 && !other.IsDead
                    && Vec2.Distance(other.Position, enemy.Position) <= GuardReach) return other;
            }
            return null;
        }

        // ---- What a felled elite gives ----

        // In place of its kind's own rolls: a leader a rare or better piece, a rune now and then and a potion; a
        // champion a magic or better piece now and then and its kind's own rolls. Experience: ExperienceFrom.
        void DropFromElite(Enemy enemy, Player killer, int level)
        {
            int rarity = (int)(killer?.Stats.ItemRarity ?? 0);
            DropEliteUnique(enemy);
            if (enemy.Elite == EliteRank.Leader)
            {
                DropItem(_lootGenerator.RollItem(level, _random, rarity, Rarity.Rare), Scatter(enemy.Position));
                if (_random.Chance(LeaderRuneChance))
                {
                    var rune = _lootGenerator.RollRune(level, _random);
                    if (rune != null) DropRune(rune, Scatter(enemy.Position));
                }
                var potion = RollConsumable(level);
                if (potion != null) DropConsumable(potion, Scatter(enemy.Position));
                DropCoins(2 * (1 + level / CoinLevels + _random.Next(level / CoinLevels + 1)), Scatter(enemy.Position));
                return;
            }
            if (_random.Chance(ChampionItemChance)) DropItem(_lootGenerator.RollItem(level, _random, rarity, Rarity.Uncommon), Scatter(enemy.Position));
        }

        // The uniques of the elite's band (item_bases.csv legendary "elite", Docs/Gear.md): a leader its chance of one
        // of them, a champion half that. Rolled only where a band has some, so older sheets keep their random sequence.
        void DropEliteUnique(Enemy enemy)
        {
            var uniques = _lootGenerator.LegendariesOf(ItemBase.EliteSource).Where(b => DamageBands.BandOfLevel(b.Level) == enemy.Band).ToList();
            if (uniques.Count == 0) return;
            var pick = uniques[_random.Next(uniques.Count)];
            float chance = pick.LegendaryChance / 100f * (enemy.Elite == EliteRank.Leader ? 1 : ChampionUniqueShare);
            if (_random.Chance(chance)) DropItem(_lootGenerator.RollLegendary(pick, _random), Scatter(enemy.Position));
        }

        // A champion's chance of a unique against a leader's.
        public const float ChampionUniqueShare = 0.5f;

        static int EliteExperience(Enemy enemy) =>
            enemy.Elite == EliteRank.Leader ? enemy.Definition.Size == MonsterSize.XL ? MidBossExperience : LeaderExperience
            : enemy.Elite == EliteRank.Champion ? ChampionExperience : 1;

        // ---- The wire: an elite's rank and affixes (Docs/WorldRework.md 통신과 저장) ----

        public static int EliteField(Enemy enemy) => enemy.Elite == EliteRank.None ? 0 : (int)enemy.Elite << 8 | (int)enemy.Affixes;

        // ---- For the playtests, the play mode scenes and the tests ----

        // An elite of the kind where it stands, made as a camp's would be (rank, affixes, body), and minions of the
        // other kind round it; all of them one pack. Returns the elite (the first of the champions).
        internal Enemy MakeEliteHere(MonsterDefinition kind, Vec2 at, EliteRank rank, RiftAffix affixes, MonsterDefinition minion = null, int minions = 0)
        {
            int zone = ZoneLevelAt(at);
            var members = new List<Enemy>();
            int elites = rank == EliteRank.Champion ? ChampionCount : 1;
            for (int i = 0; i < elites; i++)
            {
                var spot = Collision.Resolve(Map, at + (i == 0 ? Vec2.Zero : Vec2.FromAngle(2 * MathF.PI * i / elites) * 1.8f), kind.Radius, monster: true);
                var enemy = SpawnEnemy(kind, spot);
                ToughenForZone(enemy, zone);
                MakeElite(enemy, rank, affixes);
                members.Add(enemy);
            }
            for (int i = 0; minion != null && i < minions; i++)
            {
                var spot = Collision.Resolve(Map, at + Vec2.FromAngle(2 * MathF.PI * i / minions + 0.4f) * 2.2f, minion.Radius, monster: true);
                var enemy = SpawnEnemy(minion, spot);
                ToughenForZone(enemy, zone);
                members.Add(enemy);
            }
            var pack = Gather(members);
            pack.Elite = rank;
            pack.Affixes = affixes;
            return members[0];
        }
    }
}
