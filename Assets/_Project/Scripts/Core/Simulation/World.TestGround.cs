using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // One hit a player took in the test ground (Docs/TestGround.md 맞은 기록): who and what, how hard before and after
    // armor, the armor weighed against the band of whatever hit (DamageBands), and the share of the player's health it
    // took. The owner reads these to judge how hard the game hits (Docs/Difficulty.md).
    public sealed class HitRecord
    {
        public long Tick { get; internal set; }
        // The monster's or boss's name ("동굴 수호자", "슬라임 우두머리"), or what the ground did ("땅").
        public string Who { get; internal set; }
        // Its attack's name (monster_attacks.csv, boss_attacks.csv name), "몸통 공격" for a plain blow, "화상", "독",
        // "추락", "포효".
        public string What { get; internal set; }
        // What kind of hit it is (Docs/Difficulty.md): its target is the share of the health of a character of its band
        // in that band's gear it should take. None for burns, poison and falls.
        public HitKind Kind { get; internal set; }
        // Before armor, and what got through.
        public float Raw { get; internal set; }
        public int Amount { get; internal set; }
        public float MaxHealth { get; internal set; }
        public float HealthAfter { get; internal set; }
        // Whether armor weighed it (a burn, a poison's bite and a fall go past armor), the player's armor, the band of
        // whatever hit and that band's armor (DamageBands.ArmorOf), and the share that got through.
        public bool Armored { get; internal set; }
        public float Armor { get; internal set; }
        public int Band { get; internal set; }
        public float BandArmor { get; internal set; }
        public float Share { get; internal set; }
        // Zones past the first the monster was made in (World.ToughenForZone: +35% a zone); 0 for a boss.
        public int ZoneLevels { get; internal set; }
        // It would have felled the player, and "죽지 않기" kept them at 1.
        public bool Saved { get; internal set; }

        // The share of the player's maximum health it took.
        public float Fraction => MaxHealth > 0 ? Amount / MaxHealth : 0;

        // The target share for its kind, 0 when it has none.
        public float Aim => Kind == HitKind.None ? 0 : DamageBands.TargetOf(Kind).Aim;
    }

    // The test ground (Docs/TestGround.md): a world that is never saved, where the owner tries the difficulty by playing.
    // The host keeps a log of the hits each player takes, can keep a player from falling, and can let them get up where
    // they choose. Nothing here runs in an ordinary world (TestGround off).
    public sealed partial class World
    {
        // Set by the host that makes the test ground; nothing else turns it on.
        public bool TestGround { get; set; }

        public const int HitLogSize = 12;

        readonly Dictionary<int, List<HitRecord>> _hitLogs = new Dictionary<int, List<HitRecord>>();
        readonly Dictionary<int, int> _hitCounts = new Dictionary<int, int>();
        readonly HashSet<int> _undying = new HashSet<int>();
        readonly Dictionary<int, Vec2> _risings = new Dictionary<int, Vec2>();

        // The last HitLogSize hits on the player, oldest first; empty outside the test ground.
        public IReadOnlyList<HitRecord> HitsOn(int playerId) =>
            _hitLogs.TryGetValue(playerId, out var log) ? log : (IReadOnlyList<HitRecord>)Array.Empty<HitRecord>();

        // How many hits the log has had since it began (it goes up even when the oldest fall off).
        public int HitCount(int playerId) => _hitCounts.TryGetValue(playerId, out int count) ? count : 0;

        public void ClearHits(int playerId)
        {
            _hitLogs.Remove(playerId);
            _hitCounts.Remove(playerId);
        }

        // "죽지 않기": the player's health never goes under 1 (only in the test ground).
        public bool Undying(int playerId) => TestGround && _undying.Contains(playerId);

        public void SetUndying(int playerId, bool on)
        {
            if (on && TestGround) _undying.Add(playerId);
            else _undying.Remove(playerId);
        }

        // "여기서 일어나기": a fallen player gets up here instead of at the last camp or at home (only in the test ground).
        public Vec2? RisingOf(int playerId) => TestGround && _risings.TryGetValue(playerId, out var at) ? at : (Vec2?)null;

        public void SetRising(int playerId, Vec2? at)
        {
            if (at is Vec2 spot && TestGround) _risings[playerId] = spot;
            else _risings.Remove(playerId);
        }

        // HurtPlayer's last say: keeps an undying player at 1 and notes the hit. Returns whether it kept them.
        bool NoteHitOnPlayer(Player player, int source, int cause, float raw, int amount, bool armor, string kind)
        {
            if (!TestGround) return false;
            bool saved = false;
            if (player.Health < 1 && _undying.Contains(player.Id))
            {
                player.Health = 1;
                saved = true;
            }
            int band = BandOf(source, player);
            var (who, what, hitKind, zone) = Describe(source, cause, armor, kind);
            if (!_hitLogs.TryGetValue(player.Id, out var log)) _hitLogs[player.Id] = log = new List<HitRecord>();
            log.Add(new HitRecord
            {
                Tick = Tick,
                Who = who,
                What = what,
                Kind = hitKind,
                Raw = raw,
                Amount = amount,
                MaxHealth = player.Stats.MaxHealth,
                HealthAfter = Math.Max(0, player.Health),
                Armored = armor,
                Armor = player.Stats.Armor,
                Band = band,
                BandArmor = DamageBands.ArmorOf(band),
                Share = armor ? DamageBands.Share(player.Stats.Armor, band) : 1,
                ZoneLevels = zone,
                Saved = saved,
            });
            if (log.Count > HitLogSize) log.RemoveAt(0);
            _hitCounts[player.Id] = HitCount(player.Id) + 1;
            return saved;
        }

        // Who dealt a hit and with what, by the source's id and the hit's cause (a monster's attack's Index, a boss's
        // attack's place from 1, 0 for a plain blow or a roar).
        (string Who, string What, HitKind Kind, int Zone) Describe(int source, int cause, bool armor, string kind)
        {
            if (source != 0 && FindEnemy(source) is Enemy enemy)
            {
                var definition = enemy.Definition;
                string who = definition.Name + (enemy.Elite == EliteRank.Leader ? " 우두머리" : enemy.Elite == EliteRank.Champion ? " 챔피언" : "");
                var hit = enemy.Elite != EliteRank.None && definition.Size != MonsterSize.XL ? HitKind.Elite : DamageBands.KindOf(definition);
                if (kind != null) return (who, kind, HitKind.None, enemy.ZoneLevels);
                var attack = cause > 0 ? definition.Attacks.FirstOrDefault(a => a.Index == cause) : null;
                return (who, attack?.Name ?? "몸통 공격", hit, enemy.ZoneLevels);
            }
            if (source != 0 && FindBoss(source) is Boss boss)
            {
                if (kind != null) return (boss.Definition.Name, kind, HitKind.None, 0);
                var attacks = boss.Definition.Attacks;
                if (cause >= 1 && cause <= attacks.Count) return (boss.Definition.Name, attacks[cause - 1].Name, DamageBands.KindOf(attacks[cause - 1]), 0);
                return (boss.Definition.Name, "포효", HitKind.Roar, 0);
            }
            // A gone monster's shot or pool, a vent, an icicle, a fall.
            return ("땅", kind ?? (armor ? "함정" : "불길"), HitKind.None, 0);
        }

        // ---- What the test panel does, on the host (Client/TestPanel.cs through Playtest.cs's "test" commands) ----

        // Every monster within reach of the player is gone at once (no drops, no experience), as the playtests' "clear".
        // Bosses are left.
        public int ClearMonstersNear(Vec2 at, float reach)
        {
            int cleared = 0;
            foreach (var enemy in _enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, at) <= reach))
            {
                enemy.Health = 0;
                cleared++;
            }
            return cleared;
        }

        // A monster of the kind near the spot as one of the field there would be (World.ToughenForZone): as it is, as a
        // pack's leader with two of its own kind beside it, as a band of champions, or a pack of five. Returns them.
        public List<Enemy> SpawnForTest(MonsterDefinition kind, Vec2 near, EliteRank rank, int count = 1)
        {
            var made = new List<Enemy>();
            if (kind == null) return made;
            if (rank != EliteRank.None)
            {
                var first = SpawnFitting(kind, near);
                if (first == null) return made;
                var at = first.Position;
                _enemies.Remove(first);
                var elite = MakeEliteHere(kind, at, rank, rank == EliteRank.Leader ? RiftAffix.Guard : RiftAffix.Fierce,
                    rank == EliteRank.Leader && kind.Size != MonsterSize.XL ? kind : null, rank == EliteRank.Leader && kind.Size != MonsterSize.XL ? 2 : 0);
                made.AddRange(_enemies.Where(e => e == elite || PackOf(e) != null && PackOf(e) == PackOf(elite)));
                return made;
            }
            for (int i = 0; i < Math.Max(1, count); i++)
            {
                var spot = near + (i == 0 ? Vec2.Zero : Vec2.FromAngle(2 * MathF.PI * i / count) * 1.6f);
                var enemy = SpawnFitting(kind, spot);
                if (enemy == null) continue;
                ToughenForZone(enemy, ZoneLevelAt(enemy.Position));
                made.Add(enemy);
            }
            if (made.Count > 1) Gather(made);
            return made;
        }

        // Where a boss's arena is, whether it stands now or waits to come back.
        public Vec2? BossHome(string id)
        {
            var boss = _bosses.FirstOrDefault(b => b.Definition.Id == id);
            if (boss != null) return boss.Home;
            foreach (var respawn in _bossRespawns) if (respawn.Definition.Id == id) return respawn.Home;
            return null;
        }

        // The boss back in its arena now, asleep and whole: one felled comes back at once, one awake goes back to sleep.
        public bool ReviveBoss(string id)
        {
            var boss = _bosses.FirstOrDefault(b => b.Definition.Id == id);
            if (boss != null)
            {
                if (!boss.IsDead) ResetBoss(boss);
                return true;
            }
            for (int i = 0; i < _bossRespawns.Count; i++)
            {
                var respawn = _bossRespawns[i];
                if (respawn.Definition.Id != id) continue;
                _bossRespawns.RemoveAt(i);
                PlaceBoss(respawn.Definition, respawn.Home, respawn.ArenaRadius);
                return true;
            }
            return false;
        }

        // Puts the item on the player, as given (whatever its level); what was worn there goes.
        public void WearForTest(Player player, Item item)
        {
            if (player == null || item == null) return;
            Register(item);
            player.Equip(item);
        }

        // Whole again, mana full, a burn or poison washed off, potions back up to the count.
        public void RefillForTest(Player player, ConsumableDefinition potion, int potions)
        {
            if (player == null || player.IsDead) return;
            player.Afflictions.Clear();
            player.Health = player.Stats.MaxHealth;
            player.Mana = player.Stats.MaxMana;
            player.ConsumableCooldown = 0;
            if (potion != null)
            {
                int have = player.Inventory.CountOf(potion);
                if (have < potions) player.Inventory.AddConsumable(potion, potions - have);
                player.InventoryVersion++;
            }
        }
    }
}
