using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Bosses: they sleep in their arena until a player walks in, then pick attacks by weight for their current
    // phase, walk into range, mark where the attack will land and let it land after the telegraph. Every hit can
    // be dodged by reading the marks. Crossing a health threshold starts the next phase with a roar. If everyone
    // leaves or falls, the boss resets.
    public sealed partial class World
    {
        // The kind of warning each attack shape gives (Tell, Docs/Bosses.md 전조 말).
        public static Tell TellOf(AttackShape shape) => shape switch
        {
            // A burrow or a leap marks where it comes down, like any area.
            AttackShape.Charge => Tell.Dash,
            AttackShape.Share => Tell.Gather,
            AttackShape.Burst or AttackShape.Spiral => Tell.Barrage,
            AttackShape.Summon => Tell.Call,
            _ => Tell.Area,
        };

        // A boss's state for the wire: its BossState in the low four bits, and while it winds up, its Tell above them.
        public static byte BossStateByte(Boss boss) =>
            (byte)((byte)boss.State | (boss.State == BossState.Telegraph && boss.Attack != null ? (int)TellOf(boss.Attack.Shape) << 4 : 0));

        // The boss's phase for the wire with the attack it is on (its place in the sheet, plus one) above it, so the
        // view can draw that attack's own wind-up (2026-11-12). An attack past the fifteenth is sent as none.
        // A burst or a spiral all the way round starts at an angle no screen could know: it was rolled as the shots
        // left. It is settled as the wind-up begins instead (StartTelegraph) and sent with the boss while it winds up,
        // in AimSteps to a turn, so the lines drawn before the shots are the ways the shots go. The roll comes from a
        // stream of its own (the world's seed, the tick and the boss), and Unleash still draws the number it used to
        // draw there and throws it away, so nothing else in the world rolls differently for it.
        public const int AimSteps = 1024;

        public static float AimAngle(int step) => step * (2 * MathF.PI / AimSteps);

        // Whether the attack's shots start at a rolled angle: all the way round, as a burst, a spiral, or the rocks a
        // burrow or a leap flings.
        public static bool AimsRound(BossAttack attack) =>
            attack != null && attack.Count > 0 && (attack.Shape == AttackShape.Spiral
                || attack.Shape == AttackShape.Burst && !(attack.Angle > 0 && attack.Angle < 360)
                || attack.Shape == AttackShape.Burrow || attack.Shape == AttackShape.Leap);

        // For the wire: the settled start plus one while the boss winds up a burst or a spiral all the way round, or a
        // burrow or a leap that flings rocks; 0 otherwise. The rocks leave from where it comes down, not from where
        // it is seen winding up: that is the middle of the circle it marked (WorldClient.ComesDownAt), so the screen
        // draws their lines from there (0.114.0).
        public static int BossAim(Boss boss) => boss.State == BossState.Telegraph && AimsRound(boss.Attack) ? boss.AimStep + 1 : 0;

        public static int BossPhaseField(Boss boss)
        {
            // Coming in, the attack's place says which entrance it is: the whole one or the short one (EntranceWhole,
            // EntranceAgain).
            if (boss.State == BossState.Enter) return Math.Min(boss.Phase, BossPhaseMask) | (boss.EntersAgain ? EntranceAgain : EntranceWhole) << BossAttackShift;
            int index = boss.Attack == null || boss.State == BossState.Dormant || boss.State == BossState.Roar ? 0 : Cause(boss, boss.Attack);
            return Math.Min(boss.Phase, BossPhaseMask) | (index <= 15 ? index : 0) << BossAttackShift;
        }

        public const int BossPhaseMask = 3;
        public const int BossAttackShift = 2;
        public const int EntranceWhole = 1;
        public const int EntranceAgain = 2;

        // The bosses that have come in once since this world was started (by id). A boss comes in the short way
        // (arenas.csv again) when it has, or when everyone at hand has felled it before (their records, which their
        // characters keep): nobody sits through an entrance they have seen, and a newcomer beside a veteran still sees it.
        readonly HashSet<string> _bossesMet = new HashSet<string>();
        public IReadOnlyCollection<string> BossesMet => _bossesMet;

        bool SeenBefore(Boss boss)
        {
            if (_bossesMet.Contains(boss.Definition.Id)) return true;
            var near = _players.Where(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius + ArenaMargin + NearArena).ToList();
            return near.Count > 0 && near.All(p => p.Records.TryGetValue((RecordKind.Boss, boss.Definition.Id), out int felled) && felled > 0);
        }

        // How far outside the arena players still count as fighting it.
        const float ArenaMargin = 6;
        const float ResetSeconds = 5;
        // An attack starts after this long even if the boss could not get into range.
        const float ApproachSeconds = 3;
        const float ProjectileSeconds = 4;

        readonly List<Boss> _bosses = new List<Boss>();
        readonly List<Hazard> _hazards = new List<Hazard>();
        readonly List<Projectile> _projectiles = new List<Projectile>();
        readonly List<(BossDefinition Definition, Vec2 Home, float ArenaRadius, int Ticks)> _bossRespawns =
            new List<(BossDefinition Definition, Vec2 Home, float ArenaRadius, int Ticks)>();

        public IReadOnlyList<Boss> Bosses => _bosses;
        public IReadOnlyList<Hazard> Hazards => _hazards;
        public IReadOnlyList<Projectile> Projectiles => _projectiles;

        public Boss PlaceBoss(BossDefinition definition, Vec2 home, float arenaRadius)
        {
            var boss = new Boss(_nextId++, definition, home, arenaRadius);
            _bosses.Add(boss);
            return boss;
        }

        public Boss FindBoss(int id) => _bosses.FirstOrDefault(b => b.Id == id);

        // Players near enough to the arena to be in the fight.
        List<Player> Fighters(Boss boss) =>
            _players.Where(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius + ArenaMargin).ToList();

        // How far beyond the arena's edge a player still counts toward the boss's health when it wakes.
        public const float NearArena = 8;

        void Wake(Boss boss)
        {
            var definition = boss.Definition;
            // Tougher for each player at hand (in the arena or about to come in), not for everyone in the world: a
            // friend at the base does not make the boss harder (2026-11-11, the research report's candidate 14).
            int players = Math.Max(1, _players.Count(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius + ArenaMargin + NearArena));
            boss.MaxHealth = definition.Health * (1 + definition.HealthPerPlayer / 100f * (players - 1));
            boss.Health = boss.MaxHealth;
            boss.Phase = 1;
            boss.EmptyTicks = 0;
            _events.Add(new SimEvent(SimEventType.BossAwake, boss.Id));
            // It comes in first (arenas.csv): a moment in which it neither attacks nor can be hurt. The host decides
            // it, so everyone in the game sees the same entrance and the fight starts for all at once.
            boss.EntersAgain = SeenBefore(boss);
            float seconds = definition.Arena == null ? 0 : boss.EntersAgain ? definition.Arena.Again : definition.Arena.Seconds;
            int enter = seconds > 0 ? Ticks(seconds) : 0;
            _bossesMet.Add(definition.Id);
            if (enter > 0)
            {
                boss.Attack = null;
                boss.State = BossState.Enter;
                boss.StateTicks = enter;
                return;
            }
            ChooseAttack(boss, Fighters(boss));
        }

        void UpdateBosses()
        {
            for (int i = _bossRespawns.Count - 1; i >= 0; i--)
            {
                var respawn = _bossRespawns[i];
                if (--respawn.Ticks > 0)
                {
                    _bossRespawns[i] = respawn;
                    continue;
                }
                _bossRespawns.RemoveAt(i);
                PlaceBoss(respawn.Definition, respawn.Home, respawn.ArenaRadius);
            }

            foreach (var boss in _bosses)
            {
                if (!boss.IsDead) UpdateBoss(boss);
            }
        }

        void UpdateBoss(Boss boss)
        {
            if (boss.State == BossState.Dormant)
            {
                if (!BossesHold && _players.Any(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius - 1)) Wake(boss);
                return;
            }

            var fighters = Fighters(boss);
            if (fighters.Count == 0)
            {
                if (++boss.EmptyTicks >= Ticks(ResetSeconds)) ResetBoss(boss);
                return;
            }
            boss.EmptyTicks = 0;

            switch (boss.State)
            {
                case BossState.Approach:
                    var target = FindPlayer(boss.TargetId);
                    if (target == null || target.IsDead || !fighters.Contains(target))
                    {
                        ChooseAttack(boss, fighters);
                        break;
                    }
                    if (Vec2.Distance(target.Position, boss.Position) <= boss.Attack.Range || --boss.StateTicks <= 0)
                    {
                        StartTelegraph(boss, target, fighters);
                        break;
                    }
                    MoveBoss(boss, (target.Position - boss.Position).Normalized * (boss.Definition.Speed * GaitFactor(boss) * TickSeconds));
                    break;

                case BossState.Telegraph:
                    if (--boss.StateTicks <= 0) Unleash(boss);
                    break;

                case BossState.Charge:
                    Charge(boss);
                    break;

                case BossState.Enter:
                    if (--boss.StateTicks <= 0) ChooseAttack(boss, fighters);
                    break;

                case BossState.Recover:
                case BossState.Roar:
                    // A spiral fires its later volleys while the boss rests, each turned on from the last.
                    if (boss.State == BossState.Recover && boss.VolleysLeft > 0 && --boss.VolleyTicks <= 0 && boss.Attack?.Shape == AttackShape.Spiral)
                    {
                        boss.VolleysLeft--;
                        boss.VolleyTicks = Ticks(SpiralSeconds);
                        boss.VolleyAngle += boss.Attack.Angle * MathF.PI / 180f;
                        Volley(boss, boss.Attack, boss.VolleyAngle);
                        _events.Add(new SimEvent(SimEventType.Attack, boss.Id, direction: Vec2.FromAngle(boss.VolleyAngle)));
                    }
                    if (--boss.StateTicks <= 0) ChooseAttack(boss, fighters);
                    break;
            }
        }

        // ---- Phases (2026-11-09): each phase is felt, not only its health bar. The attacks a phase brings come
        // first after its roar and come up most often in it; the ones from before fade; the boss rests less between
        // attacks; and from phase 2 it sometimes lays a second area attack at once on someone else, from phase 3
        // often. ----

        // How much shorter the rest after an attack is in each phase.
        static float Tempo(int phase) => phase >= 3 ? 0.7f : phase == 2 ? 0.85f : 1f;

        // The chance a phase lays a second area attack with the one chosen.
        static float DoubleChance(int phase) => phase >= 3 ? 0.5f : phase == 2 ? 0.25f : 0f;

        static int FirstPhase(BossAttack attack) => attack.Phases.Count == 0 ? 1 : attack.Phases.Min();

        // An attack's weight in a phase (in tenths of the sheet's): two and a half times for one the phase brought,
        // less for each phase since an older one came.
        internal static int PhaseWeight(BossAttack attack, int phase)
        {
            int first = FirstPhase(attack);
            float factor = phase > 1 && first == phase ? 2.5f : 1 / (1 + 0.5f * Math.Max(0, phase - first));
            return Math.Max(1, (int)MathF.Round(attack.Weight * 10 * factor));
        }

        void ChooseAttack(Boss boss, List<Player> fighters)
        {
            if (fighters.Count == 0) return;
            var choices = boss.Definition.Attacks.Where(a => a.InPhase(boss.Phase) && a.Weight > 0).ToList();
            if (boss.OpensPhase)
            {
                boss.OpensPhase = false;
                var fresh = choices.Where(a => FirstPhase(a) == boss.Phase).ToList();
                if (fresh.Count > 0) choices = fresh;
            }
            boss.Attack = _random.PickWeighted(choices, a => PhaseWeight(a, boss.Phase));
            boss.VolleysLeft = 0;
            boss.GaitTicks = 0;
            boss.TargetId = PickTarget(boss, boss.Attack.Target, fighters).Id;
            boss.State = BossState.Approach;
            boss.StateTicks = Ticks(ApproachSeconds);
        }

        // For the playtests: bosses stay asleep whoever walks in, so an arena can be looked at; a boss is put back to
        // sleep; and a boss asleep is made to come in now, the whole way or the short one.
        internal bool BossesHold { get; set; }

        internal void ForceSleep(Boss boss)
        {
            if (!boss.IsDead) ResetBoss(boss);
        }

        internal bool ForceEntrance(Boss boss, bool again)
        {
            if (boss.IsDead || boss.Definition.Arena == null) return false;
            ResetBoss(boss);
            if (again) _bossesMet.Add(boss.Definition.Id);
            else _bossesMet.Remove(boss.Definition.Id);
            Wake(boss);
            return boss.State == BossState.Enter;
        }

        // For the playtests: the awake boss lays down that attack's telegraph at once, on the target its rule picks.
        internal bool ForceAttack(Boss boss, string attackId)
        {
            var attack = boss.Definition.Attacks.FirstOrDefault(a => a.Id == attackId);
            var fighters = Fighters(boss);
            if (attack == null || fighters.Count == 0 || boss.State == BossState.Dormant || boss.IsDead) return false;
            boss.Attack = attack;
            boss.VolleysLeft = 0;
            var target = PickTarget(boss, attack.Target, fighters);
            boss.TargetId = target.Id;
            StartTelegraph(boss, target, fighters);
            return true;
        }

        Player PickTarget(Boss boss, AttackTarget mode, List<Player> fighters)
        {
            switch (mode)
            {
                case AttackTarget.Farthest:
                    return fighters.OrderByDescending(p => Vec2.Distance(p.Position, boss.Position)).First();
                case AttackTarget.Random:
                    return fighters[_random.Next(fighters.Count)];
                default:
                    return fighters.OrderBy(p => Vec2.Distance(p.Position, boss.Position)).First();
            }
        }

        // Marks where the attack will land. Circles stay where their target stood, so moving away dodges them. A charge
        // that runs again (count) winds up the later runs in less time (again).
        void StartTelegraph(Boss boss, Player target, List<Player> fighters, bool again = false)
        {
            var attack = boss.Attack;
            var aim = target.Position - boss.Position;
            if (aim.LengthSquared > 1e-6f) boss.Facing = aim.Normalized;
            boss.State = BossState.Telegraph;
            boss.StateTicks = Ticks(again ? attack.Telegraph * AgainTelegraph : attack.Telegraph);
            if (!again) boss.ChargesLeft = attack.Shape == AttackShape.Charge ? Math.Max(0, attack.Count - 1) : 0;
            long resolve = Tick + boss.StateTicks;
            int cause = Cause(boss, attack);
            if (AimsRound(attack)) boss.AimStep = (int)(new GameRandom(_forageSeed + (ulong)Tick, stream: 40 + (ulong)boss.Id).NextUInt() % AimSteps);

            LayHazards(boss, attack, target, fighters, boss.Facing, resolve, cause);
            // Its follow-up (follow column) is marked now too and lands a moment after: one roll does not clear both.
            // Only in the follow-up's own phases (2026-11-28: the rings come from phase 2, so phase 1 lays one pattern).
            bool follows = attack.FollowUp != null && !again && attack.FollowUp.InPhase(boss.Phase);
            if (follows)
                LayHazards(boss, attack.FollowUp, target, fighters, boss.Facing, resolve + Ticks(attack.FollowDelay), Cause(boss, attack.FollowUp));
            if (attack.Shape == AttackShape.Charge) boss.ChargeDirection = boss.Facing;
            if (attack.GoesAway) boss.LandAt = LandingFor(boss, target.Position);

            // A later phase sometimes lays another area attack of this phase at once, on someone (else, when there is
            // anyone else), resolving a moment after. Not with a follow-up laid: at most two patterns at once (2026-11-28).
            if (attack.LaysAreas && !follows && _random.NextFloat() < DoubleChance(boss.Phase))
            {
                var others = boss.Definition.Attacks.Where(a => a != attack && a.InPhase(boss.Phase) && a.Weight > 0 && a.LaysAreas).ToList();
                if (others.Count > 0)
                {
                    var second = _random.PickWeighted(others, a => PhaseWeight(a, boss.Phase));
                    var victim = fighters.Count > 1 ? fighters.Where(p => p != target).ElementAt(_random.Next(fighters.Count - 1)) : target;
                    var toward = victim.Position - boss.Position;
                    var facing = toward.LengthSquared > 1e-6f ? toward.Normalized : boss.Facing;
                    LayHazards(boss, second, victim, fighters, facing, resolve + Ticks(SecondDelay), Cause(boss, second));
                }
            }
        }

        // Seconds after the first that a second attack laid with it lands.
        const float SecondDelay = 0.4f;

        // A bursting gait (2026-11-12): a rush at five times its pace for a short moment, then barely moving, over and over.
        const float BurstSeconds = 0.12f;
        const float BurstCycle = 0.7f;
        const float BurstRush = 5;
        const float BurstRest = 0.3f;

        static float GaitFactor(Boss boss)
        {
            if (!boss.Definition.Bursts) return 1;
            int at = boss.GaitTicks++ % Ticks(BurstCycle);
            return at < Ticks(BurstSeconds) ? BurstRush : BurstRest;
        }

        // How much of its telegraph a charge's later runs take.
        public const float AgainTelegraph = 0.5f;

        // Where a burrow or leap aimed at a spot can come down: on open ground inside the arena.
        Vec2 LandingFor(Boss boss, Vec2 spot)
        {
            var fromHome = spot - boss.Home;
            float limit = Math.Max(0, boss.ArenaRadius - boss.Definition.Radius);
            if (fromHome.Length > limit) spot = boss.Home + fromHome.Normalized * limit;
            return Collision.Resolve(Map, spot, boss.Definition.Radius);
        }

        // The marked areas of an attack, aimed along facing (cones, lines, trails) at the target, resolving at tick
        // resolve. A charge's path is marked without harm: the charge itself hurts.
        void LayHazards(Boss boss, BossAttack attack, Player target, List<Player> fighters, Vec2 facing, long resolve, int cause)
        {
            switch (attack.Shape)
            {
                case AttackShape.Circle:
                    var centres = attack.Target == AttackTarget.Self ? new[] { boss.Position }
                        : attack.Target == AttackTarget.Each ? fighters.Select(p => p.Position).ToArray()
                        : new[] { target.Position };
                    foreach (var centre in centres)
                    {
                        for (int i = 0; i < attack.Count; i++)
                        {
                            var offset = attack.Spread > 0
                                ? Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI) * (MathF.Sqrt(_random.NextFloat()) * attack.Spread)
                                : Vec2.Zero;
                            AddHazard(boss, cause, HazardShape.Circle, centre + offset, facing, attack.Radius, 0, attack.Damage, resolve);
                        }
                    }
                    break;
                case AttackShape.Cone:
                    AddHazard(boss, cause, HazardShape.Cone, boss.Position, facing, attack.Length, attack.Angle, attack.Damage, resolve);
                    break;
                case AttackShape.Line:
                    AddHazard(boss, cause, HazardShape.Line, boss.Position, facing, attack.Length, attack.Width, attack.Damage, resolve);
                    break;
                case AttackShape.Charge:
                    AddHazard(boss, cause, HazardShape.Line, boss.Position, facing, attack.Length, attack.Width, 0, resolve);
                    break;
                case AttackShape.Ring:
                    var middle = attack.Target == AttackTarget.Self ? boss.Position : target.Position;
                    AddHazard(boss, cause, HazardShape.Ring, middle, facing, attack.Length, attack.Radius, attack.Damage, resolve);
                    break;
                case AttackShape.Cross:
                    var crossing = attack.Target == AttackTarget.Self ? boss.Position : target.Position;
                    float turn = attack.Angle * MathF.PI / 180f;
                    if (attack.Count >= 2)
                    {
                        // A star (2026-11-13, the silk mother's eight legs): count pairs of beams evenly turned, each arm
                        // laid by itself from radius out to length, so the middle is clear and no arm lies on another.
                        int arms = 4 * attack.Count;
                        for (int arm = 0; arm < arms; arm++)
                        {
                            var outward = Vec2.FromAngle(turn + arm * 2 * MathF.PI / arms);
                            AddHazard(boss, cause, HazardShape.Line, crossing + outward * attack.Radius, outward, attack.Length - attack.Radius, attack.Width, attack.Damage, resolve);
                        }
                        break;
                    }
                    for (int arm = 0; arm < 2; arm++)
                    {
                        var along = Vec2.FromAngle(turn + arm * MathF.PI / 2);
                        AddHazard(boss, cause, HazardShape.Line, crossing - along * attack.Length, along, attack.Length * 2, attack.Width, attack.Damage, resolve);
                    }
                    break;
                case AttackShape.Trail:
                    // The blasts walk out from the boss toward where the target stood, one after another.
                    float gap = attack.Length / attack.Count;
                    for (int i = 0; i < attack.Count; i++)
                    {
                        long at = resolve + Ticks(gap * i / attack.Speed);
                        AddHazard(boss, cause, HazardShape.Circle, boss.Position + facing * (gap * (i + 1)), facing, attack.Radius, 0, attack.Damage, at);
                    }
                    break;
                case AttackShape.Share:
                    AddHazard(boss, cause, HazardShape.Share, target.Position, facing, attack.Radius, 0, attack.Damage, resolve);
                    break;
                case AttackShape.Burrow:
                case AttackShape.Leap:
                    // Where it will come down, which is where the boss will stand.
                    AddHazard(boss, cause, HazardShape.Circle, LandingFor(boss, target.Position), facing, attack.Radius, 0, attack.Damage, resolve);
                    break;
                case AttackShape.Pluck:
                    // Every web of its own on the floor, a little past its edge; none down, under each player.
                    var webs = _hazards.Where(h => h.Shape == HazardShape.Web && h.Source == boss.Id).ToList();
                    foreach (var web in webs)
                        AddHazard(boss, cause, HazardShape.Circle, web.Position, facing, web.Size + PluckReach, 0, attack.Damage, resolve).Plucks = web.Id;
                    if (webs.Count == 0)
                        foreach (var fighter in fighters) AddHazard(boss, cause, HazardShape.Circle, fighter.Position, facing, attack.Radius, 0, attack.Damage, resolve);
                    break;
            }
        }

        // How far past a plucked web its burst reaches.
        public const float PluckReach = 0.4f;

        // The telegraph is over. Marked areas resolve by themselves in UpdateHazards; this fires, charges or summons.
        void Unleash(Boss boss)
        {
            var attack = boss.Attack;
            _events.Add(new SimEvent(SimEventType.Attack, boss.Id, direction: boss.Facing));
            switch (attack.Shape)
            {
                case AttackShape.Charge:
                    boss.State = BossState.Charge;
                    boss.StateTicks = Ticks(attack.Length / attack.Speed);
                    boss.ChargeHits.Clear();
                    return;
                case AttackShape.Burst:
                    Burst(boss, attack);
                    break;
                case AttackShape.Spiral:
                    // The first volley now; the rest while the boss rests (UpdateBoss).
                    _random.NextFloat();
                    boss.VolleyAngle = AimAngle(boss.AimStep);
                    boss.VolleysLeft = Math.Max(1, (int)attack.Width) - 1;
                    boss.VolleyTicks = Ticks(SpiralSeconds);
                    Volley(boss, attack, boss.VolleyAngle);
                    break;
                case AttackShape.Burrow:
                case AttackShape.Leap:
                    // Up out of the ground, or down out of the air, where it marked; rocks fly out all round.
                    var from = boss.Position;
                    boss.Position = boss.LandAt;
                    var moved = boss.Position - from;
                    if (moved.LengthSquared > 1e-6f) boss.Facing = moved.Normalized;
                    _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: (int)MathF.Ceiling(attack.Radius) + 2));
                    if (attack.Count > 0) Burst(boss, attack, 0.4f);
                    break;
                case AttackShape.Summon:
                    for (int i = 0; i < attack.Count; i++)
                    {
                        float angle = (i + _random.NextFloat() * 0.5f) * 2 * MathF.PI / attack.Count;
                        var position = Collision.Resolve(Map, boss.Position + Vec2.FromAngle(angle) * attack.Spread, attack.Summon.Radius);
                        if (!Collision.Overlaps(Map, position, attack.Summon.Radius)) SpawnEnemy(attack.Summon, position);
                    }
                    break;
            }
            boss.State = BossState.Recover;
            // A quicker phase rests less, but never so little that a spiral's later volleys go unfired.
            boss.StateTicks = Math.Max(Ticks(attack.Cooldown * Tempo(boss.Phase)), boss.VolleysLeft * Ticks(SpiralSeconds) + 1);
        }

        // Seconds between a spiral's volleys.
        const float SpiralSeconds = 0.3f;

        // One ring of a spiral: projectiles evenly all the way round from the angle given.
        void Volley(Boss boss, BossAttack attack, float start)
        {
            for (int i = 0; i < attack.Count; i++)
            {
                var direction = Vec2.FromAngle(start + 2 * MathF.PI * i / attack.Count);
                _projectiles.Add(new Projectile(_nextId++, boss.Id, Cause(boss, attack), boss.Position + direction * boss.Definition.Radius,
                    direction * attack.Speed, attack.Radius, attack.Damage, Ticks(ProjectileSeconds)));
            }
        }

        // Evenly spaced projectiles: all the way round from a random start, or fanned out toward the target. A burrow or
        // leap flings rocks of the size given (its own radius is the circle's).
        void Burst(Boss boss, BossAttack attack, float rockRadius = 0)
        {
            bool fan = attack.Angle > 0 && attack.Angle < 360;
            float spread = (fan ? attack.Angle : 360) * MathF.PI / 180f;
            // All the way round starts where the wind-up settled (AimStep); the roll once made here is still drawn.
            if (!fan) _random.NextFloat();
            float start = fan ? boss.Facing.Angle - spread / 2 : AimAngle(boss.AimStep);
            int gaps = fan ? Math.Max(1, attack.Count - 1) : attack.Count;
            for (int i = 0; i < attack.Count; i++)
            {
                var direction = Vec2.FromAngle(start + (fan && attack.Count == 1 ? spread / 2 : spread * i / gaps));
                _projectiles.Add(new Projectile(
                    _nextId++,
                    boss.Id,
                    Cause(boss, attack),
                    boss.Position + direction * boss.Definition.Radius,
                    direction * attack.Speed,
                    rockRadius > 0 ? rockRadius : attack.Radius,
                    rockRadius > 0 ? Math.Max(1, attack.Damage / 3) : attack.Damage,
                    Ticks(ProjectileSeconds)));
            }
        }

        void Charge(Boss boss)
        {
            var attack = boss.Attack;
            var before = boss.Position;
            var step = boss.ChargeDirection * (attack.Speed * TickSeconds);
            MoveBoss(boss, step);
            boss.Facing = boss.ChargeDirection;

            foreach (var player in _players)
            {
                if (player.IsDead || boss.ChargeHits.Contains(player.Id)) continue;
                if (Vec2.Distance(player.Position, boss.Position) > boss.Definition.Radius + PlayerRadius) continue;
                boss.ChargeHits.Add(player.Id);
                AfflictPlayer(player, boss.Id, HurtPlayer(player, boss.Id, attack.Damage, Cause(boss, attack)));
            }

            bool blocked = Vec2.Distance(before, boss.Position) < step.Length * 0.3f;
            if (--boss.StateTicks > 0 && !blocked) return;
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 6));
            // Another run at whoever is nearest now, wound up in less time.
            var fighters = Fighters(boss);
            if (boss.ChargesLeft > 0 && fighters.Count > 0)
            {
                boss.ChargesLeft--;
                var next = PickTarget(boss, AttackTarget.Nearest, fighters);
                boss.TargetId = next.Id;
                StartTelegraph(boss, next, fighters, again: true);
                return;
            }
            boss.State = BossState.Recover;
            boss.StateTicks = Ticks(attack.Cooldown * Tempo(boss.Phase));
        }

        // Bosses stay in their arena so a fight cannot be dragged off through the caves.
        void MoveBoss(Boss boss, Vec2 delta)
        {
            var position = Collision.Move(Map, boss.Position, boss.Definition.Radius, delta, monster: true);
            var fromHome = position - boss.Home;
            float limit = Math.Max(0, boss.ArenaRadius - boss.Definition.Radius);
            if (fromHome.Length > limit) position = boss.Home + fromHome.Normalized * limit;
            if (delta.LengthSquared > 1e-8f) boss.Facing = delta.Normalized;
            boss.Position = position;
        }

        void DamageBoss(Boss boss, int source, int amount, bool critical, int cause = 0, SkillTag tags = SkillTag.None)
        {
            boss.Health -= amount;
            _events.Add(new SimEvent(SimEventType.Hit, source, boss.Id, amount, critical, cause: cause, tags: tags));
            if (boss.IsDead)
            {
                ClearAttacksFrom(boss);
                // Everyone in the fight has it in their bestiary.
                foreach (var fighter in Fighters(boss)) Record(fighter, RecordKind.Boss, boss.Definition.Id);
                DropFrom(boss);
                // Where it fell, for the host to send the fall to whoever could see it: the boss itself leaves the
                // snapshots at once (WorldHost).
                _events.Add(new SimEvent(SimEventType.Died, boss.Id, position: boss.Position));
                _events.Add(new SimEvent(SimEventType.BossDefeated, boss.Id));
                return;
            }

            int phase = 1;
            float percent = boss.Health * 100 / boss.MaxHealth;
            foreach (int threshold in boss.Definition.Thresholds)
            {
                if (percent <= threshold) phase++;
            }
            if (phase <= boss.Phase) return;

            // A new phase: roar, unhurt, then blast everyone standing too close.
            var definition = boss.Definition;
            boss.Phase = phase;
            boss.Attack = null;
            boss.OpensPhase = true;
            boss.State = BossState.Roar;
            boss.StateTicks = Ticks(definition.Roar);
            _events.Add(new SimEvent(SimEventType.PhaseChanged, boss.Id, amount: phase));
            if (definition.RoarDamage > 0 && definition.RoarRadius > 0)
            {
                AddHazard(boss, 0, HazardShape.Circle, boss.Position, boss.Facing, definition.RoarRadius, 0, definition.RoarDamage, Tick + boss.StateTicks);
            }
        }

        void ResetBoss(Boss boss)
        {
            ClearAttacksFrom(boss);
            boss.Health = boss.Definition.Health;
            boss.MaxHealth = boss.Definition.Health;
            boss.Phase = 1;
            boss.Position = boss.Home;
            boss.Attack = null;
            boss.State = BossState.Dormant;
            boss.EmptyTicks = 0;
            boss.OpensPhase = false;
        }

        void RemoveDefeatedBosses()
        {
            foreach (var boss in _bosses.Where(b => b.IsDead).ToList())
            {
                ClearAttacksFrom(boss);
                _bosses.Remove(boss);
                _bossRespawns.Add((boss.Definition, boss.Home, boss.ArenaRadius, Ticks(_config.BossRespawnSeconds)));
            }
        }

        void ClearAttacksFrom(Boss boss)
        {
            _hazards.RemoveAll(h => h.Source == boss.Id);
            _projectiles.RemoveAll(p => p.Source == boss.Id);
        }

        Hazard AddHazard(Boss boss, int cause, HazardShape shape, Vec2 position, Vec2 direction, float size, float width, int damage, long resolve)
        {
            var hazard = new Hazard(_nextId++, boss.Id, cause, shape, position, direction, size, width, damage, Tick, resolve);
            _hazards.Add(hazard);
            return hazard;
        }

        static int Cause(Boss boss, BossAttack attack)
        {
            for (int i = 0; i < boss.Definition.Attacks.Count; i++)
            {
                if (boss.Definition.Attacks[i] == attack) return i + 1;
            }
            return 0;
        }

        void UpdateHazards()
        {
            List<int> plucked = null;
            // Webs that lie over each other burst together: whoever stands in two of them is struck once.
            HashSet<int> burst = null;
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                var hazard = _hazards[i];
                if (hazard.Lingers)
                {
                    if (Tick >= hazard.ResolveTick) _hazards.RemoveAt(i);
                    else Linger(hazard);
                    continue;
                }
                if (Tick < hazard.ResolveTick)
                {
                    Pending(hazard);
                    if (Tick < hazard.ResolveTick) continue;
                }
                _hazards.RemoveAt(i);
                LeavePool(hazard);
                LeaveMonsterPool(hazard);
                if (hazard.Plucks != 0) (plucked ??= new List<int>()).Add(hazard.Plucks);
                if (hazard.Damage <= 0) continue;
                var hit = _players.Where(p => !p.IsDead && hazard.Contains(p.Position, PlayerRadius)).ToList();
                if (hazard.Plucks != 0)
                {
                    burst ??= new HashSet<int>();
                    hit.RemoveAll(p => !burst.Add(p.Id));
                }
                // A shared circle splits its damage among everyone in it; alone, one takes it all.
                int damage = hazard.Shape == HazardShape.Share ? (int)MathF.Ceiling(hazard.Damage / (float)Math.Max(1, hit.Count)) : hazard.Damage;
                foreach (var player in hit)
                {
                    AfflictPlayer(player, hazard.Source, HurtPlayer(player, hazard.Source, damage, hazard.Cause));
                    Affect(player, hazard.Effect, hazard.EffectAmount, hazard.Position, hazard.Source, damage);
                }
                // Where it landed rides along, so a mark no one laid (an icicle, a vent's burst) still reaches those who can
                // see it (WorldHost's events).
                _events.Add(new SimEvent(SimEventType.Impact, hazard.Source, hazard.Id, (int)MathF.Ceiling(hazard.Size), position: hazard.Position));
            }
            // A plucked web bursts and is gone.
            if (plucked != null) _hazards.RemoveAll(h => h.Shape == HazardShape.Web && plucked.Contains(h.Id));
        }

        // What a landed circle leaves behind (boss_attacks.csv pool): a web or a pool of venom the same size, for the
        // attack's linger. Too many at once and the oldest goes: the arena fills but never wholly, and with a star's
        // eight arms marked as well they still fit in a snapshot (WorldHost.MaxHazardsPerSnapshot).
        public const int MaxPools = 6;

        void LeavePool(Hazard hazard)
        {
            if (hazard.Shape != HazardShape.Circle || hazard.Cause <= 0) return;
            var boss = FindBoss(hazard.Source);
            if (boss == null || boss.IsDead || hazard.Cause > boss.Definition.Attacks.Count) return;
            var attack = boss.Definition.Attacks[hazard.Cause - 1];
            if (attack.Pool == PoolKind.None) return;
            var pools = _hazards.Where(h => h.Lingers && h.Source == boss.Id).OrderBy(h => h.StartTick).ToList();
            if (pools.Count >= MaxPools) _hazards.Remove(pools[0]);
            bool web = attack.Pool == PoolKind.Web;
            int damage = web ? 0 : Math.Max(1, (int)MathF.Round(attack.Damage * VenomShare));
            _hazards.Add(new Hazard(_nextId++, boss.Id, hazard.Cause, web ? HazardShape.Web : HazardShape.Venom, hazard.Position, hazard.Direction,
                hazard.Size, 0, damage, Tick, Tick + Ticks(attack.Linger)));
        }

        // A web snares whoever is in it; venom hurts whoever stands in it, every VenomSeconds from when it fell.
        void Linger(Hazard pool)
        {
            if (pool.Shape != HazardShape.Web && pool.Shape != HazardShape.Venom)
            {
                LingerMonsterPool(pool);
                return;
            }
            bool bites = pool.Shape == HazardShape.Venom && Tick > pool.StartTick && (Tick - pool.StartTick) % Ticks(VenomSeconds) == 0;
            if (pool.Shape != HazardShape.Web && !bites) return;
            foreach (var player in _players)
            {
                if (player.IsDead || !pool.Contains(player.Position, PlayerRadius)) continue;
                if (pool.Shape == HazardShape.Web) player.Afflictions.SnareTicks = Math.Max(player.Afflictions.SnareTicks, Ticks(SnareSeconds));
                else HurtPlayer(player, pool.Source, pool.Damage, pool.Cause);
            }
        }

        void UpdateProjectiles()
        {
            // A player's projectile can fell a boss, which takes the boss's projectiles with it, so this goes
            // through a copy of the list.
            var flying = _projectiles.ToArray();
            for (int i = flying.Length - 1; i >= 0; i--)
            {
                var projectile = flying[i];
                if (!_projectiles.Contains(projectile)) continue;
                projectile.Position += projectile.Velocity * TickSeconds;
                // A monster's shot may come off the wall or turn back there (World.MonsterAttacks.cs).
                bool wall = Collision.Overlaps(Map, projectile.Position, projectile.Radius * 0.5f);
                // Crystal turns any shot, a player's too (World.CrystalLake.cs, B3b).
                if (wall && Glance(projectile)) wall = false;
                if (wall && !projectile.Friendly && Rebound(projectile))
                {
                    wall = false;
                    Refract(projectile);
                }
                // A crystal tortoise's shell turns a player's shot back at them.
                if (projectile.Friendly && ReflectOff(projectile))
                {
                    _projectiles.Remove(projectile);
                    continue;
                }
                bool ended = --projectile.RemainingTicks <= 0 || wall || ComesBack(projectile);
                bool gone = projectile.Friendly ? FlyFriendly(projectile, ended) : ended || StrikePlayer(projectile);
                if (gone) _projectiles.Remove(projectile);
            }
        }

        // A boss's projectile stops at the first player it touches.
        bool StrikePlayer(Projectile projectile)
        {
            // A returning shot goes on through, striking each player once on the way out and once on the way back.
            bool returns = projectile.TurnTicks != 0;
            var victim = _players.FirstOrDefault(p => !p.IsDead && Vec2.Distance(p.Position, projectile.Position) <= projectile.Radius + PlayerRadius
                && !(returns && projectile.Pierced.Contains(p.Id)));
            if (victim == null) return false;
            // It flies on through a rolling player (World.Roll.cs).
            if (Dodges(victim, projectile.Source)) return false;
            AfflictPlayer(victim, projectile.Source, HurtPlayer(victim, projectile.Source, projectile.Damage, projectile.Cause));
            Affect(victim, projectile.Effect, projectile.EffectAmount, projectile.Position - projectile.Velocity * TickSeconds, projectile.Source, projectile.Damage);
            if (!returns) return true;
            projectile.Pierced.Add(victim.Id);
            return false;
        }
    }
}
