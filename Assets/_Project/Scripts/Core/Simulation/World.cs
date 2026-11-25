using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class WorldConfig
    {
        public int EnemyTarget { get; set; } = 24;
        public float SpawnIntervalSeconds { get; set; } = 2;
        // New enemies appear at least this far from every player and from the spawn point.
        public float SpawnDistance { get; set; } = 12;
        public float RespawnSeconds { get; set; } = 5;
        public int ZoneLevel { get; set; } = 1;
        // The zone level rises by one every this many tiles from the spawn, so tougher monsters live further out.
        public float ZoneRing { get; set; } = 30;
        // A defeated boss returns to its arena after this long.
        public float BossRespawnSeconds { get; set; } = 120;
        // Lays each biome's forage on the floor every day (World.Forage.cs). Off unless asked: the game's host turns it
        // on; tests and sims count what lies on the floor without it.
        public bool Forage { get; set; }
        // Minutes of play a day takes (Calendar.DayLengths); the host's choice (Docs/Time.md 하루 길이).
        public int DayMinutes { get; set; } = Calendar.BaseDayMinutes;
        // Resting heals as it did before 묶음 4c (6 seconds unhurt, then 3% a second) instead of the slower rest that
        // sends a player to camps, shrines and potions (8 seconds, 1.5%). Off unless the host chose it in the menu
        // (Settings.QuickRest, Docs/WorldRework.md 사람이 정한 것 2).
        public bool QuickRest { get; set; }
        // The pacing director acts (World.Director.cs, Docs/WorldRework.md 완급 감독): wanderers sent, ambushes, raids
        // and the quiet after a peak. Off unless asked: the game's host and the bots that play as it turn it on; tests
        // and sims count without it.
        public bool Director { get; set; }
    }

    // The host's simulation. It steps at a fixed TicksPerSecond and is the only thing that changes game state;
    // the one exception is a player's own position, which its client reports (see MovePlayer). Bosses, their
    // telegraphed areas and projectiles are in World.Boss.cs; drops, pickups and inventory commands in World.Loot.cs;
    // players joining with their classes and using skills in World.Skills.cs; digging, building and crafting in
    // World.Mining.cs; farming in World.Farming.cs; chests in World.Chests.cs; machines in World.Machines.cs; the
    // clock and sleeping in World.Time.cs.
    public sealed partial class World
    {
        public const int TicksPerSecond = 30;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const float PlayerRadius = 0.35f;
        // How far out from a place's plaza the monsters that keep to it come (monsters.csv place, 2026-11-10).
        public const float HauntReach = 16;
        // How far past the player's edge a swing reaches, and how wide it is.
        public const float MeleeReach = 1.1f;
        public const float MeleeArcDegrees = 120;
        // Basic attacks come in a combo of three (Docs/Combat.md, 연타): each within ComboWindowSeconds after the last
        // could be made goes on to the next step, and the third, the finisher, hits harder and wider, pushes further,
        // and takes a little longer to recover from. The attack event's amount is the kind plus ComboStride × step.
        public const int ComboSteps = 3;
        public const int ComboStride = 4;
        public const float ComboWindowSeconds = 0.6f;
        public const int FinisherDamagePercent = 150;
        public const int FinisherRecoveryPercent = 130;
        // 0.9 past a plain swing's reach (2026-11-19, the person: the finisher should reach wider; at 0.3 the first two
        // blows' knockback often pushed the target out of its reach).
        public const float FinisherReach = 0.9f;
        const float FinisherArcDegrees = 170;
        const float FinisherPush = 1.8f;
        const float FinisherShotRadius = 1.6f;

        // The attack kind and combo step an attack event carries.
        public static BasicAttackKind AttackKindOf(int amount) => (BasicAttackKind)(amount % ComboStride);
        public static int ComboStepOf(int amount) => amount / ComboStride;
        // Enemies chase until the player is this many times their aggro range away.
        const float LoseInterest = 1.5f;
        const float KnockbackSpeed = 7;
        const float KnockbackDecay = 0.75f;
        const int FlowFieldInterval = 10;
        const int FlowFieldRange = 24;
        // Reach is measured from the player's position when the swing started; give a little slack for
        // the enemy and player having moved during the wind-up.
        const float StrikeSlack = 0.2f;
        const float MonsterShotRadius = 0.2f;
        // A player who has not been hurt for this long heals this percent of their maximum health a second (묶음 4c,
        // Docs/WorldRework.md 탐험에서 얻는 회복: less than before, so camps, shrines and potions matter); with the host's
        // QuickRest, as before.
        public const float RestDelaySeconds = 8;
        public const float RestHealPercent = 1.5f;
        public const float QuickRestDelaySeconds = 6;
        public const float QuickRestHealPercent = 3;

        // Whether resting heals as before 묶음 4c (WorldConfig.QuickRest); the host may change it as the world runs.
        public bool QuickRest { get; set; }

        readonly List<Player> _players = new List<Player>();
        readonly List<Enemy> _enemies = new List<Enemy>();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly MonsterData _monsters;
        readonly WorldConfig _config;
        readonly GameRandom _random;
        readonly FlowField _flow;
        int _nextId = 1;
        int _spawnTimer;

        // Without loot data nothing drops; without tile data no wall can be dug.
        public World(TileMap map, Vec2 spawn, MonsterData monsters, ulong seed, WorldConfig config = null, LootData loot = null, TileData tiles = null,
            BiomeMap biomes = null)
        {
            Biomes = biomes ?? BiomeMap.Plain(map?.Width ?? 1, map?.Height ?? 1);
            Tiles = tiles;
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Spawn = spawn;
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _config = config ?? new WorldConfig();
            DayMinutes = Calendar.DayMinutesOf(_config.DayMinutes);
            QuickRest = _config.QuickRest;
            _random = new GameRandom(seed, stream: 2);
            _forageSeed = seed;
            Loot = loot;
            _lootGenerator = loot != null ? new LootGenerator(loot) : null;
            _flow = new FlowField(map);
            _toStone = new FlowField(map);
            _toStoneThrough = new FlowField(map) { Through = BuiltBarrier };
            // The dungeons' slots, when the map has the pocket (World.Dungeon.cs); the gates come with the rooms.
            _dungeons = new DungeonInstance[DungeonPocket.SlotsOf(map)];
            for (int i = 0; i < _config.EnemyTarget; i++) TrySpawnEnemy();
        }

        public TileMap Map { get; }
        public Vec2 Spawn { get; }
        // Which biome each tile is in, for monsters that only live in one. Without one everything is plain cave.
        public BiomeMap Biomes { get; }
        public long Tick { get; private set; }
        public IReadOnlyList<Player> Players => _players;
        public IReadOnlyList<Enemy> Enemies => _enemies;

        public static int Ticks(float seconds) => Math.Max(1, (int)MathF.Round(seconds * TicksPerSecond));

        public void RemovePlayer(int id) => _players.RemoveAll(p => p.Id == id);

        public Player FindPlayer(int id) => _players.FirstOrDefault(p => p.Id == id);

        public Enemy FindEnemy(int id) => _enemies.FirstOrDefault(e => e.Id == id);

        public Enemy SpawnEnemy(MonsterDefinition definition, Vec2 position)
        {
            var enemy = new Enemy(_nextId++, definition, position);
            _enemies.Add(enemy);
            return enemy;
        }

        // The owning client moves its player with PlayerMotor and reports where it ended up, so its own movement
        // never waits for the network. The host accepts the report unless the player could not have got there in
        // the elapsed ticks or it overlaps a wall; then the player stays put and the client is corrected. rolling: the
        // steps were those of a roll the host took (World.Roll), which are faster than walking.
        public bool MovePlayer(int id, Vec2 reported, Vec2 facing, int elapsedTicks, bool rolling = false)
        {
            var player = FindPlayer(id);
            if (player == null) return false;
            if (facing.LengthSquared > 1e-6f && !player.IsDead) player.Facing = facing.Normalized;
            if (reported.Equals(player.Position)) return true;
            // A rider goes where the cart goes (World.Travel.cs): what the client reports meanwhile is let pass.
            if (player.Riding) return true;

            float speed = rolling ? Math.Max(player.MoveSpeed, RollSpeed * RollSlow(player.Afflictions.Chilled, player.Afflictions.Snared)) : player.MoveSpeed;
            float allowed = speed * TickSeconds * Math.Clamp(elapsedTicks, 1, 10) * 1.25f + 0.05f;
            bool valid = !player.IsDead
                && reported.IsFinite
                && Vec2.Distance(player.Position, reported) <= allowed
                && !Collision.Overlaps(Map, reported, PlayerRadius - 0.01f, passable: DoorsFor(player));
            if (valid)
            {
                player.Position = reported;
                return true;
            }
            player.Corrections++;
            return false;
        }

        // While held, the player swings whenever the weapon is ready.
        public void SetAttacking(int id, bool attacking)
        {
            var player = FindPlayer(id);
            if (player != null) player.Attacking = attacking;
        }

        public List<SimEvent> TakeEvents()
        {
            var events = new List<SimEvent>(_events);
            _events.Clear();
            return events;
        }

        public void Step()
        {
            Tick++;
            foreach (var player in _players)
            {
                UpdatePlayer(player);
                RecordHoldings(player);
                PetBites(player);
            }
            // A spore cloud stays where its monster fell or went off (a puffball's blast, World.BiomeMonsters.cs).
            foreach (var dead in _enemies.Where(e => e.IsDead)) _hazards.RemoveAll(h => h.Source == dead.Id && h.Shape != HazardShape.Balm);
            NotePackFalls();
            NotePaceFalls();
            _enemies.RemoveAll(e => e.IsDead);
            UpdateMonsterExtras();
            RemoveDefeatedBosses();

            if (Tick % FlowFieldInterval == 1)
            {
                _flow.Build(_players.Where(p => !p.IsDead).Select(p => p.Position), FlowFieldRange);
            }
            foreach (var enemy in _enemies)
            {
                if (SkipsTick(enemy)) continue;
                UpdateEnemy(enemy);
                KeepOutOfPlaces(enemy);
                KeepOutOfCamps(enemy);
                KeepOutOfDungeonRooms(enemy);
            }
            UpdateElites();
            UpdateBosses();
            UpdateMinions();
            UpdateAfflictions();
            // The ice cave's falling icicles (World.IceSand.cs), laid before the hazards land this tick.
            UpdateIcicles();
            UpdateHazards();
            UpdateProjectiles();
            UpdateForage();
            UpdateStory();
            UpdateStones();
            UpdateWanderer();
            UpdateLoot();
            UpdateCrops();
            UpdateMachines();
            SeparateEnemies();
            UpdateClock();
            UpdateRift();
            UpdateDefense();
            UpdateWeather();
            UpdateDungeons();

            if (--_spawnTimer <= 0)
            {
                _spawnTimer = Ticks(_config.SpawnIntervalSeconds);
                if (SpawnsNearPlayers) ClearFarMonsters();
                // A world of rooms keeps its packs in their rooms (UpdatePacks) and brings only wanderers here; a big
                // world of caves brings clumps up to the parties' caps; anything else one monster at a time, as before
                // there were packs (World.Packs.cs).
                if (RoomPacks)
                {
                    if (WantsWanderer()) TrySpawnEnemy();
                }
                else if (ClumpPacks)
                {
                    if (FieldCount < EnemyTargetNow) TrySpawnEnemy();
                }
                else if (_enemies.Count < EnemyTargetNow) TrySpawnEnemy();
            }
            UpdatePacks();
            // The trap rooms' bands, and the pacing director (World.Traps.cs, World.Director.cs).
            UpdateTraps();
            UpdateDirector();
        }

        void UpdatePlayer(Player player)
        {
            player.TickSkills();
            RunCrafting(player);
            if (player.ConsumableCooldown > 0) player.ConsumableCooldown--;
            if (player.IsDead)
            {
                BreakDungeonCharge(player);
                if (Revive(player)) return;
                if (--player.RespawnTicks > 0) return;
                // At the entrance of the dungeon they fell in, however often (World.Dungeon.cs); else by the last camp
                // rested at, or at home (World.Pois.cs).
                var inDungeon = DungeonRising(player);
                player.Position = inDungeon ?? RisingPlace(player);
                player.Health = player.Stats.MaxHealth;
                player.Mana = player.Stats.MaxMana;
                player.Corrections++;
                if (inDungeon != null) _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonRose, position: player.Position));
                return;
            }

            Heal(player);
            UpdateRide(player);
            UpdatePois(player);
            BurnInLava(player);
            UpdateRecall(player);
            UpdateDungeonCharge(player);
            UpdateFishing(player);
            UpdateRoll(player);
            if (player.AttackCooldown > 0) player.AttackCooldown--;
            // No swing comes out in the middle of a roll.
            if (player.Attacking && player.AttackCooldown == 0 && !player.Rolling && !player.Riding) Attack(player);
        }

        // A swing that hits nobody strikes the tile ahead. Rock (walls and ore veins) only gives way to a pickaxe in
        // hand; anything takes down a placed object, and a bow or staff facing one takes it down like a swing instead
        // of shooting it point-blank. Struck with something else, rock says so (a Dug event with Amount -1).
        void Attack(Player player)
        {
            var stats = player.Stats;
            int interval = Math.Max(1, (int)MathF.Round(TicksPerSecond / Math.Max(0.1f, stats.AttacksPerSecond)));
            var target = DigTarget(player);
            bool rock = target != null && IsRock(target.Value.Tile);
            bool shoot = stats.Attack.Shoots && (target == null || rock);
            var kind = shoot ? stats.Attack.Kind : BasicAttackKind.Swing;
            // The next step of the combo if the last attack was just now, else the first.
            bool going = player.LastAttackTick >= 0 && Tick - player.LastAttackTick <= player.AttackRecovery + Ticks(ComboWindowSeconds);
            int step = going ? (player.ComboStep + 1) % ComboSteps : 0;
            bool finisher = step == ComboSteps - 1;
            int at = _events.Count;
            if (shoot) Shoot(player, finisher);
            else if (stats.Attack.Shoots || !Swing(player, finisher))
            {
                if (target != null)
                {
                    // Digging is no combo: it starts again at the first step, at the usual pace.
                    step = 0;
                    finisher = false;
                    if (!rock || HoldsPickaxe(player)) Dig(player);
                    else _events.Add(new SimEvent(SimEventType.Dug, player.Id, amount: -1, position: TileMap.Center(target.Value.X, target.Value.Y)));
                }
            }
            player.ComboStep = step;
            player.LastAttackTick = Tick;
            player.AttackRecovery = finisher ? interval * FinisherRecoveryPercent / 100 : interval;
            player.AttackCooldown = player.AttackRecovery;
            _events.Insert(at, new SimEvent(SimEventType.Attack, player.Id, amount: (int)kind + ComboStride * step, direction: player.Facing));
        }

        public static bool HoldsPickaxe(Player player) => player.Equipment[ItemSlot.Weapon]?.Base.IsPickaxe == true;

        // Walls and ore veins, which need a pickaxe; not the things players place.
        static bool IsRock(TileDefinition tile) => tile.Tile == TileType.Wall || tile.Tile == TileType.IronWall || tile.Drillable;

        // An arrow or bolt toward where the player faces, its damage and crit rolled now (see FlyFriendly). A finisher's
        // is bigger and hits harder.
        void Shoot(Player player, bool finisher = false)
        {
            var stats = player.Stats;
            var shot = stats.Attack;
            bool critical = _random.Chance(stats.CritChance / 100f);
            float damage = stats.Damage * (critical ? stats.CritDamage / 100f : 1f) * (finisher ? FinisherDamagePercent / 100f : 1);
            var direction = player.Facing;
            _projectiles.Add(new Projectile(
                _nextId++,
                player.Id,
                -1,
                player.Position + direction * PlayerRadius,
                direction * shot.Speed,
                shot.Radius * (finisher ? FinisherShotRadius : 1),
                damage,
                Ticks(shot.Range / shot.Speed),
                friendly: true,
                tags: stats.AttackTags,
                critical: critical,
                more: finisher ? FinisherDamagePercent / 100f : 1));
        }

        // Returns whether anything was hit. A finisher reaches further round and further out, hits harder and pushes
        // further.
        bool Swing(Player player, bool finisher = false)
        {
            var stats = player.Stats;
            bool hit = false;
            float reach = MeleeReach + (finisher ? FinisherReach : 0);
            float arc = finisher ? FinisherArcDegrees : MeleeArcDegrees;
            float more = finisher ? FinisherDamagePercent / 100f : 1;

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                if (!Combat.InArc(player.Position, player.Facing, enemy.Position, enemy.Definition.Radius, reach, arc)) continue;
                // Not through rock (2026-11-09): a monster behind a wall the player stands against is out of reach, and
                // the swing goes to the wall instead.
                if (!ClearLine(player.Position, enemy.Position, ThinLine)) continue;

                float damage = RollDamage(stats, out bool critical) * more;
                HitEnemy(player, enemy, damage, critical, player.Position, stats.AttackTags, push: finisher ? FinisherPush : 1);
                AddElements(player, enemy, null, critical, more);
                hit = true;
            }

            foreach (var boss in _bosses)
            {
                if (boss.IsDead) continue;
                if (!Combat.InArc(player.Position, player.Facing, boss.Position, boss.Definition.Radius, reach, arc)) continue;
                if (!ClearLine(player.Position, boss.Position, ThinLine)) continue;
                hit = true;
                if (boss.State == BossState.Dormant) Wake(boss);
                if (boss.Invulnerable) continue;
                HitBoss(player, boss, RollDamage(stats, out bool critical) * more, critical, stats.AttackTags);
                AddElements(player, null, boss, critical, more);
            }
            return hit;
        }

        // A basic attack's damage before armor and resistances.
        float RollDamage(PlayerStats stats, out bool critical)
        {
            critical = _random.Chance(stats.CritChance / 100f);
            return stats.Damage * (critical ? stats.CritDamage / 100f : 1f);
        }

        void UpdateEnemy(Enemy enemy)
        {
            var before = enemy.Position;
            MoveEnemy(enemy);
            // One that fears the light takes back a step into it, or goes out of it (World.BiomeMonsters.cs).
            if (enemy.Definition.Has(MonsterTraitKind.Fearlight)) ShunLight(enemy, before);
            // A tentacle takes back a step away from the water (World.CrystalLake.cs).
            if (enemy.Definition.Has(MonsterTraitKind.Shore)) KeepToShore(enemy, before);
        }

        void MoveEnemy(Enemy enemy)
        {
            var definition = enemy.Definition;
            if (enemy.Knockback.LengthSquared > 0.01f)
            {
                enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, enemy.Knockback * TickSeconds, monster: true);
                enemy.Knockback *= KnockbackDecay;
            }
            TickMonster(enemy);
            if (MonstersHold && (enemy.State == EnemyState.Idle || enemy.State == EnemyState.Chase)) return;
            // A pack's alarm, its leash and the way home, and a wanderer's (World.Packs.cs).
            if ((enemy.PackId != 0 || enemy.Wanders) && PackTick(enemy)) return;

            var target = FindPlayer(enemy.TargetId);
            // One with attacks in monster_attacks.csv fights from that sheet once it has someone to fight
            // (World.MonsterAttacks.cs); the others go on the old way below.
            if (definition.Attacks.Count > 0 && enemy.State != EnemyState.Idle)
            {
                UpdateFighter(enemy, target);
                return;
            }
            switch (enemy.State)
            {
                case EnemyState.Idle:
                    // One that lies in wait stays under the ground until someone is right over it (World.LieInWait).
                    if (WaitsInAmbush(enemy))
                    {
                        LieInWait(enemy);
                        break;
                    }
                    // One come for the ward stone goes after a player only if it can walk to them; walled out, it goes on
                    // for the stone and breaks in.
                    bool walledOut = enemy.Rift == DefenseMonster && _flow.DistanceAt(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y)) == FlowField.Unreached;
                    var nearest = walledOut ? null : _players
                        .Where(p => !p.IsDead && Vec2.Distance(p.Position, enemy.Position) <= (NoticeReach(enemy) ?? definition.Aggro * AggroScale) && !InSafePlace(p.Position) && !Unheeded(enemy, p))
                        .OrderBy(p => Vec2.Distance(p.Position, enemy.Position))
                        .FirstOrDefault();
                    if (nearest == null)
                    {
                        // One that came for the ward stone goes on for it; a pack's that strayed goes home, a wanderer
                        // goes on its way.
                        MarchOnStone(enemy);
                        IdleAbout(enemy);
                        break;
                    }
                    enemy.TargetId = nearest.Id;
                    enemy.State = EnemyState.Chase;
                    Noticed(enemy, nearest);
                    break;

                case EnemyState.Chase:
                    // A player in a place of the story is out of reach: the monster gives up.
                    if (target == null || target.IsDead || Vec2.Distance(target.Position, enemy.Position) > definition.Aggro * LoseInterest
                        || InSafePlace(target.Position))
                    {
                        enemy.State = EnemyState.Idle;
                        break;
                    }
                    var toTarget = target.Position - enemy.Position;
                    float gap = Gap(enemy, target);
                    // A shooter backs away from a player who comes too close, and only shoots from there once it is
                    // cornered.
                    if (definition.Behavior == MonsterBehavior.Ranged && gap < definition.KeepDistance)
                    {
                        var before = enemy.Position;
                        enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, -toTarget.Normalized * (enemy.Speed * TickSeconds), monster: true);
                        enemy.Facing = toTarget.Normalized;
                        if (Vec2.Distance(before, enemy.Position) > enemy.Speed * TickSeconds * 0.3f) break;
                    }
                    if (gap <= definition.Reach && CanStrike(enemy, target))
                    {
                        StartWindup(enemy, toTarget.Normalized);
                        break;
                    }
                    if (enemy.Rift == DefenseMonster && gap >= 1.5f
                        && _flow.DistanceAt(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y)) == FlowField.Unreached)
                    {
                        enemy.State = EnemyState.Idle;
                        break;
                    }
                    var direction = gap < 1.5f ? toTarget.Normalized : WayFor(enemy);
                    if (direction.LengthSquared < 1e-6f)
                    {
                        // A big body waits where no way fits it (WayFor).
                        if (definition.Big)
                        {
                            enemy.Facing = toTarget.Normalized;
                            break;
                        }
                        direction = toTarget.Normalized;
                    }
                    enemy.Facing = direction;
                    enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, direction * (enemy.Speed * TickSeconds), monster: true);
                    break;

                case EnemyState.Windup:
                    if (--enemy.StateTicks > 0) break;
                    switch (definition.Behavior)
                    {
                        case MonsterBehavior.Ranged:
                            if (target != null && !target.IsDead) enemy.Facing = (target.Position - enemy.Position).Normalized;
                            _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
                            _projectiles.Add(new Projectile(
                                _nextId++,
                                enemy.Id,
                                0,
                                enemy.Position + enemy.Facing * definition.Radius,
                                enemy.Facing * definition.ShotSpeed,
                                MonsterShotRadius,
                                enemy.Damage,
                                Ticks(definition.Reach * 1.5f / definition.ShotSpeed)));
                            break;
                        case MonsterBehavior.Charge:
                            _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.ChargeDirection));
                            enemy.State = EnemyState.Charge;
                            enemy.StateTicks = Ticks(definition.ChargeLength / definition.ChargeSpeed);
                            enemy.ChargeHits.Clear();
                            return;
                        default:
                            _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
                            if (target != null && !target.IsDead && Gap(enemy, target) <= definition.Reach + StrikeSlack && ClearLine(enemy.Position, target.Position, ThinLine))
                            {
                                AfflictPlayer(target, enemy.Id, HurtPlayer(target, enemy.Id, enemy.Damage));
                            }
                            break;
                    }
                    enemy.State = EnemyState.Recover;
                    enemy.StateTicks = Ticks(definition.Cooldown);
                    break;

                case EnemyState.Charge:
                    Charge(enemy);
                    break;

                case EnemyState.Recover:
                    if (--enemy.StateTicks <= 0) enemy.State = EnemyState.Chase;
                    break;
            }
        }

        // Faces the target and starts the telegraph. A charger marks the path it is about to run along.
        void StartWindup(Enemy enemy, Vec2 direction)
        {
            var definition = enemy.Definition;
            enemy.Facing = direction;
            enemy.State = EnemyState.Windup;
            enemy.StateTicks = Ticks(definition.Windup);
            if (definition.Behavior != MonsterBehavior.Charge) return;
            enemy.ChargeDirection = direction;
            _hazards.Add(new Hazard(_nextId++, enemy.Id, 0, HazardShape.Line, enemy.Position, direction, definition.ChargeLength,
                definition.Radius * 2, 0, Tick, Tick + enemy.StateTicks));
        }

        // Shooters and chargers only start when nothing solid is in the way; otherwise they keep closing in.
        bool CanStrike(Enemy enemy, Player target) =>
            enemy.Definition.Behavior == MonsterBehavior.Melee
            || ClearLine(enemy.Position, target.Position, enemy.Definition.Behavior == MonsterBehavior.Ranged ? MonsterShotRadius : enemy.Definition.Radius);

        // A blow's line between two bodies (a swing, a monster's bite): only rock in the way stops it.
        const float ThinLine = 0.1f;

        bool ClearLine(Vec2 from, Vec2 to, float radius)
        {
            float length = Vec2.Distance(from, to);
            for (float along = 0; along < length; along += 0.25f)
            {
                if (Collision.Overlaps(Map, Vec2.Lerp(from, to, along / length), radius * 0.5f)) return false;
            }
            return true;
        }

        // The rush: straight along the marked path, hurting each player it runs into once, stopping at a wall.
        void Charge(Enemy enemy)
        {
            var definition = enemy.Definition;
            var before = enemy.Position;
            var step = enemy.ChargeDirection * (definition.ChargeSpeed * TickSeconds);
            enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, step, monster: true);
            enemy.Facing = enemy.ChargeDirection;
            foreach (var player in _players)
            {
                if (player.IsDead || enemy.ChargeHits.Contains(player.Id)) continue;
                if (Vec2.Distance(player.Position, enemy.Position) > definition.Radius + PlayerRadius) continue;
                enemy.ChargeHits.Add(player.Id);
                AfflictPlayer(player, enemy.Id, HurtPlayer(player, enemy.Id, enemy.Damage));
            }
            bool blocked = Vec2.Distance(before, enemy.Position) < step.Length * 0.3f;
            if (--enemy.StateTicks > 0 && !blocked) return;
            enemy.State = EnemyState.Recover;
            enemy.StateTicks = Ticks(definition.Cooldown);
        }

        // Gear's regeneration always, resting once the player has gone unhurt for a while, and whatever a consumable
        // still has to give. See Docs/Combat.md (healing). Mana comes back at its own rate all the while.
        void Heal(Player player)
        {
            player.Mana = Math.Min(player.Stats.MaxMana, player.Mana + player.Stats.ManaRegen * TickSeconds);
            float perSecond = player.Stats.HealthRegen;
            int restTicks = Ticks(QuickRest ? QuickRestDelaySeconds : RestDelaySeconds);
            if (player.RestingTicks < restTicks) player.RestingTicks++;
            else perSecond += player.Stats.MaxHealth * (QuickRest ? QuickRestHealPercent : RestHealPercent) / 100f;

            float amount = perSecond * TickSeconds;
            if (player.HealTicks > 0)
            {
                amount += player.HealPerTick;
                player.HealTicks--;
            }
            player.Health = Math.Min(player.Stats.MaxHealth, player.Health + amount);
        }

        // For the cloud's playtests (Client/Playtest.cs): a hit on a player past armor, or on a boss, as a fight would
        // land it, so falling, a boss's phases and its drops happen the usual way.
        internal int Hurt(Player player, float damage) => HurtPlayer(player, 0, damage, armor: false);

        // For the playtests and tests: the player's hit on the monster, as a blow of theirs would land.
        internal void Hurt(Enemy enemy, Player by, float damage, SkillTag tags = SkillTag.None) => HitEnemy(by, enemy, damage, false, by.Position, tags);

        internal void Hurt(Boss boss, int amount)
        {
            if (!boss.IsDead) DamageBoss(boss, 0, amount, false);
        }

        // Returns the damage dealt. A burn's damage is not lessened by armor. A blow (dodge) passes through a player in
        // the unhurt ticks of a roll (World.Roll.cs); a burn already on them does not.
        int HurtPlayer(Player player, int source, float damage, int cause = 0, bool armor = true, bool dodge = true)
        {
            if (player.IsDead) return 0;
            // Nothing touches one riding a minecart (World.Travel.cs).
            if (player.Riding) return 0;
            if (dodge && Dodges(player, source)) return 0;
            int amount = armor ? Combat.Mitigate(damage, player.Stats.Armor) : Math.Max(1, (int)MathF.Round(damage));
            BreakRecall(player);
            BreakDungeonCharge(player);
            EndFishing(player, FishingReeled);
            player.Sleeping = false;
            player.Health -= amount;
            player.RestingTicks = 0;
            Strain(player, amount);
            _events.Add(new SimEvent(SimEventType.Hit, source, player.Id, amount, cause: cause));
            if (!player.IsDead) return amount;

            player.Health = 0;
            EndRoll(player);
            player.Attacking = false;
            player.HealTicks = 0;
            player.ClearBuffs();
            player.Afflictions.Clear();
            // With others in the world there is time for one of them to come (DownSeconds, World.Revive).
            player.RespawnTicks = Ticks(_players.Count > 1 ? Math.Max(DownSeconds, _config.RespawnSeconds) : _config.RespawnSeconds);
            player.ReviveTicks = 0;
            player.Falls++;
            NoteDungeonFall(player);
            _events.Add(new SimEvent(SimEventType.Died, player.Id));
            return amount;
        }

        static float Gap(Enemy enemy, Player player) =>
            Vec2.Distance(enemy.Position, player.Position) - enemy.Definition.Radius - PlayerRadius;

        // Overlapping enemies push each other apart so a crowd spreads out instead of stacking on one tile.
        void SeparateEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                for (int j = i + 1; j < _enemies.Count; j++)
                {
                    var a = _enemies[i];
                    var b = _enemies[j];
                    var apart = b.Position - a.Position;
                    float minimum = a.Definition.Radius + b.Definition.Radius;
                    float distance = apart.Length;
                    if (distance >= minimum) continue;

                    // Each gives way by the other's share of their bodies (2026-11-24): a swarm parts round a big one
                    // rather than shoving it, and two of a size share it as before.
                    var push = (distance > 1e-4f ? apart / distance : new Vec2(1, 0)) * (minimum - distance);
                    float ra = a.Definition.Radius * a.Definition.Radius, rb = b.Definition.Radius * b.Definition.Radius;
                    float giveA = rb / (ra + rb);
                    var toA = Collision.Resolve(Map, a.Position - push * giveA, a.Definition.Radius, monster: true);
                    var toB = Collision.Resolve(Map, b.Position + push * (1 - giveA), b.Definition.Radius, monster: true);
                    // One that fears the light is not shoved into it by the crowd at its edge (B1b, World.ShunLight).
                    if (!PushedIntoLight(a, toA)) a.Position = toA;
                    if (!PushedIntoLight(b, toB)) b.Position = toB;
                }
            }
        }

        bool PushedIntoLight(Enemy enemy, Vec2 to) =>
            enemy.Definition.Has(MonsterTraitKind.Fearlight) && Shuns(enemy, to) && !Shuns(enemy, enemy.Position);

        // The places of the story are safe (2026-11-11, "괴물이 마을 안까지 들어와"): no monster comes into being in
        // one (TrySpawnEnemy), none goes after a player inside one, and one that wanders or is knocked in is set back
        // just outside its edge. The ward stone's raiders and the bosses keep to their own ground and are not stopped.
        public const float SafeMargin = 1;

        public bool InSafePlace(Vec2 position) => Places.Any(p => Vec2.Distance(position, p.Center) <= p.Radius + SafeMargin) || InCamp(position);

        void KeepOutOfPlaces(Enemy enemy)
        {
            if (enemy.Rift == DefenseMonster || Places.Count == 0) return;
            foreach (var place in Places)
            {
                float edge = place.Radius + SafeMargin + enemy.Definition.Radius;
                var away = enemy.Position - place.Center;
                float distance = away.Length;
                if (distance >= edge) continue;
                var outward = distance > 1e-3f ? away / distance : new Vec2(0, -1);
                enemy.Position = Collision.Resolve(Map, place.Center + outward * edge, enemy.Definition.Radius, monster: true);
                if (enemy.State == EnemyState.Chase) enemy.State = EnemyState.Idle;
            }
        }

        // The zone level at a place: ZoneLevel at the spawn, one more every ZoneRing tiles out.
        // The ring grows with the map (WorldGenerator.ScaleOf, 2026-11-11), so a big map has the same zones, wider.
        // In a dungeon, the dungeon's (World.Dungeon.cs).
        public int ZoneLevelAt(Vec2 position) =>
            Map.InPocket(position) && DungeonAt(position) is DungeonInstance dungeon ? dungeon.Level
            : _config.ZoneLevel + (_config.ZoneRing > 0 ? (int)(Vec2.Distance(position, Spawn) / (_config.ZoneRing * MapScale)) : 0);

        // How many times WorldGenerator.DefaultSize the map is across.
        public int MapScale => WorldGenerator.ScaleOf(Map.Width, Map.Height);

        // On a big map (2026-11-11) monsters come round the players, not anywhere on the map: between SpawnDistance and
        // SpawnReach from one of them, and the ones of the field go when no one is within DespawnReach.
        public bool SpawnsNearPlayers => MapScale >= 2;
        public const float SpawnReach = 40;
        public const float DespawnReach = 64;

        // A field monster further out is tougher (2026-11-11, the owner's decision on the content audit's 4th point):
        // each zone past the first adds half its health, a third of its hits and the experience of a level.
        public const float ZoneHealthStep = 0.5f;
        public const float ZonePowerStep = 0.35f;

        void ToughenForZone(Enemy enemy, int zone)
        {
            int extra = Math.Max(0, zone - 1);
            if (extra == 0) return;
            enemy.MaxHealth = enemy.Health = enemy.Definition.Health * (1 + ZoneHealthStep * extra);
            enemy.Power = 1 + ZonePowerStep * extra;
            enemy.ZoneLevels = extra;
        }

        void ClearFarMonsters()
        {
            var alive = _players.Where(p => !p.IsDead).ToList();
            if (alive.Count == 0) return;
            // A room's pack goes with its room (World.UpdatePacks), not here.
            // Nor a dungeon's, which stays until its dungeon goes back to rock (World.Dungeon.cs).
            _enemies.RemoveAll(e => e.Rift == 0 && PackOf(e)?.Room == null && !Map.InPocket(e.Position) && alive.All(p => Vec2.Distance(p.Position, e.Position) > DespawnReach));
        }

        // Whether a monster comes at a tile of this zone level and biome. A biome's own monsters come one zone early
        // (2026-11-11, the content audit): the biomes lie out past the plain cave already (BiomeMap.CaveRadius), and on
        // a 128-tile map no floor is four zones out, so the abyss's level 4 monsters never came at all.
        public static bool Comes(MonsterDefinition monster, int zone, Biome biome) =>
            monster.Biome == null ? monster.Level <= zone : monster.Biome == biome && monster.Level <= zone + 1;

        // The kinds that come one by one at a tile: round a place some monsters keep to (monsters.csv place), only those;
        // elsewhere never them. A wanderer of a world of rooms walks: never one that is rooted.
        List<MonsterDefinition> FieldKinds(Vec2 position, int x, int y)
        {
            int zone = ZoneLevelAt(position);
            var biome = Biomes[x, y];
            var haunt = Places.FirstOrDefault(p => Vec2.Distance(position, p.Center) <= p.Radius + HauntReach
                && _monsters.Monsters.Any(m => m.Place == p.Definition.Id && m.Weight > 0));
            var choices = haunt != null
                ? _monsters.Monsters.Where(m => m.Place == haunt.Definition.Id && m.Weight > 0).ToList()
                : _monsters.Monsters.Where(m => m.Place == null && m.Weight > 0 && Comes(m, zone, biome)).ToList();
            if (RoomPacks) choices.RemoveAll(m => m.Has(MonsterTraitKind.Rooted));
            // Never a big body (L, XL) one by one: they come only in rooms, with their packs (World.Sizes.cs).
            choices.RemoveAll(m => m.Big);
            return choices;
        }

        void TrySpawnEnemy()
        {
            if (!_monsters.Monsters.Any(m => m.Level <= _config.ZoneLevel && m.Weight > 0)) return;
            // Not round a player the pacing director keeps quiet (World.Director.cs).
            // Nor round one in a dungeon: the field's monsters do not come into the pocket (World.Dungeon.cs).
            var round = SpawnsNearPlayers ? _players.Where(p => !p.IsDead && !Hushed(p) && !InPocket(p.Position)).ToList() : null;
            if (round != null && round.Count == 0) return;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int x, y;
                if (round != null)
                {
                    var near = round[_random.Next(round.Count)];
                    int reach = (int)SpawnReach;
                    x = TileMap.Cell(near.Position.X) + _random.Next(2 * reach + 1) - reach;
                    y = TileMap.Cell(near.Position.Y) + _random.Next(2 * reach + 1) - reach;
                    if (!Map.InBounds(x, y) || Vec2.Distance(TileMap.Center(x, y), near.Position) > SpawnReach) continue;
                }
                else
                {
                    x = _random.Next(Map.Width);
                    y = _random.Next(Map.Height);
                }
                var position = TileMap.Center(x, y);
                if (Map.IsSolid(x, y)
                    || Map.InPocket(y)
                    || Map[x, y] == TileType.Lava
                    // Not on the roads or in the places of the story (World.Story.cs).
                    || Structures.IsWay(Map[x, y]) || Map[x, y] == TileType.Waypoint || Places.Any(p => Vec2.Distance(position, p.Center) <= p.Radius + 2)
                    || Vec2.Distance(position, Spawn) < _config.SpawnDistance
                    || _players.Any(p => Vec2.Distance(p.Position, position) < _config.SpawnDistance)
                    // A world of rooms: wanderers only, between the rooms with no pack, off the roads (World.Packs.cs).
                    || RoomPacks && !WandererSpot(x, y))
                {
                    continue;
                }
                // A big world of caves: a pack as one clump, when one comes there.
                if (ClumpPacks && round != null && TryClump(position, round.OrderBy(p => Vec2.Distance(p.Position, position)).First())) return;
                int zone = ZoneLevelAt(position);
                var choices = FieldKinds(position, x, y);
                if (choices.Count == 0) continue;
                var kind = _random.PickWeighted(choices, m => m.Weight);
                var first = SpawnEnemy(kind, position);
                ToughenForZone(first, zone);
                if (!RoomPacks) return;
                first.Wanders = true;
                // One of those that always go in packs wanders with another.
                if (kind.Pack != "always") return;
                var beside = position + Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI) * PackSpacingMin;
                if (Collision.Overlaps(Map, beside, kind.Radius, monster: true)) return;
                var second = SpawnEnemy(kind, beside);
                ToughenForZone(second, zone);
                second.Wanders = true;
                return;
            }
        }
    }

    public static class PlayerMotor
    {
        // Floors that change the walk (2026-11-21, the person: ice slides, quicksand slows; Docs/Combat.md 바닥). On ice
        // the player keeps the speed they had and it turns toward the stick only so fast: with the stick held, IceGrip
        // move speeds a second (a quarter of a second from rest to full speed, half a second to turn right round, and
        // a quarter turn at a run is carried 0.9 of a tile wide); with it let go, IceDrag (0.4 of a second and one
        // tile to stop from a run). Tuned so a phone's stick still goes where it points: the slide is felt at a turn
        // and at a stop, never as a loss of the stick. Quicksand is walked at QuicksandSlow of the speed. A slide never
        // goes faster than a walk, so the host's check of a reported step (World.MovePlayer) holds as it is.
        public const float IceGrip = 4f;
        public const float IceDrag = 2.5f;
        public const float QuicksandSlow = 0.55f;
        // The lake's shallows (B3b) are waded at this share of the speed.
        public const float ShallowSlow = 0.7f;

        static TileType Under(TileMap map, Vec2 position) => map[TileMap.Cell(position.X), TileMap.Cell(position.Y)];

        // What share of their speed a player walks at on the floor under them.
        public static float GroundSpeed(TileMap map, Vec2 position) => Under(map, position) switch
        {
            TileType.Quicksand => QuicksandSlow,
            TileType.ShallowWater => ShallowSlow,
            _ => 1,
        };

        public static bool OnIce(TileMap map, Vec2 position) => Under(map, position) == TileType.IceFloor;

        // One tick of a player's own movement. Input is the stick or WASD direction; longer than 1 is clamped. doors are
        // the locked doors they walk through (World.DoorsFor), the same on the host and in the client's prediction.
        // This one keeps no speed from tick to tick, so it never slides (bots, tests); quicksand slows it all the same.
        public static Vec2 Step(TileMap map, Vec2 position, Vec2 input, float moveSpeed, ISet<int> doors = null) =>
            Collision.Move(map, position, World.PlayerRadius, input.ClampLength(1) * (moveSpeed * GroundSpeed(map, position) * World.TickSeconds), passable: doors);

        // The same with the speed carried from the tick before (tiles a second), which the caller keeps: what a player's
        // own client moves them with (WorldClient.Step). Off the ice it is the step above.
        public static Vec2 Step(TileMap map, Vec2 position, ref Vec2 velocity, Vec2 input, float moveSpeed, ISet<int> doors = null)
        {
            var wanted = input.ClampLength(1) * (moveSpeed * GroundSpeed(map, position));
            if (OnIce(map, position))
            {
                // Slowed since the last tick (chilled, snared): the slide slows with them.
                velocity = velocity.ClampLength(moveSpeed);
                float rate = (input.LengthSquared > 1e-6f ? IceGrip : IceDrag) * moveSpeed * World.TickSeconds;
                var change = wanted - velocity;
                velocity = change.Length <= rate ? wanted : velocity + change.Normalized * rate;
            }
            else velocity = wanted;
            var to = Collision.Move(map, position, World.PlayerRadius, velocity * World.TickSeconds, passable: doors);
            // A wall takes the speed it stopped.
            velocity = (to - position) / World.TickSeconds;
            return to;
        }

        // One tick of a roll along direction (World.Roll.cs): the same for everyone whatever their move speed, shorter
        // by slow (World.RollSlow) when chilled or snared. Walls stop it as they stop a walk.
        public static Vec2 Roll(TileMap map, Vec2 position, Vec2 direction, float slow = 1, ISet<int> doors = null) =>
            Collision.Move(map, position, World.PlayerRadius, direction.Normalized * (World.RollSpeed * slow * World.TickSeconds), passable: doors);
    }

    public static class Combat
    {
        // Armor 100 halves damage and 300 quarters it, so it never blocks everything. A hit always does at least 1.
        public static int Mitigate(float damage, float armor) =>
            Math.Max(1, (int)MathF.Round(damage * 100f / (100f + Math.Max(0, armor))));

        public static bool InArc(Vec2 origin, Vec2 facing, Vec2 target, float targetRadius, float reach, float arcDegrees)
        {
            var toTarget = target - origin;
            float distance = toTarget.Length;
            if (distance - targetRadius - World.PlayerRadius > reach) return false;
            if (distance <= MonsterSizes.Beyond(targetRadius) + 1e-4f) return true;
            float cosine = Vec2.Dot(toTarget / distance, facing.Normalized);
            // Any of the body inside the arc (2026-11-24): a big body is struck by a swing that takes its flank, as far
            // round as the part of it past an M body's widest (MonsterSizes.Beyond) makes it; an M body's as before.
            float widen = MathF.Asin(Math.Min(1, MonsterSizes.Beyond(targetRadius) / distance));
            float half = Math.Min(MathF.PI, arcDegrees * 0.5f * MathF.PI / 180f + widen);
            return cosine >= MathF.Cos(half);
        }
    }
}
