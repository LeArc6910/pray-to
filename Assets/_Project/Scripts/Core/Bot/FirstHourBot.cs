using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A new character's first hour, played on the host's Core with no screen (Docs/Playtest.md, 첫 한 시간 봇,
    // 2026-11-11): the guide's goals in order, each by what it asks for (Goal.Conditions), going about the world with
    // BotPilot the way the web playtest does. It acts as a player's client would: moves the player and reports where
    // it got to (World.MovePlayer), holds the attack, and sends the bag's commands (craft, place, plant, fish, sell)
    // straight to the World. A goal it cannot do, or gives up on, is skipped with the reason (BotMemory.Skipped) and
    // the next is taken, so one stuck goal does not hide the rest. BotReport writes what the run came to.
    public sealed class FirstHourBot
    {
        // Ticks a goal may take before it is skipped; the boss and the levels get longer.
        public const int GoalTicks = 8 * 60 * World.TicksPerSecond;
        public const int LongGoalTicks = 15 * 60 * World.TicksPerSecond;
        const int ClaimEvery = 30;
        // Levels over a boss's own that the bot gets before it goes in (it dodges slower than a person).
        public const int BossMargin = 2;

        readonly World _world;
        readonly Player _player;
        readonly LootData _loot;
        readonly MonsterData _monsters;
        readonly ClassData _classes;
        readonly GuideData _guide;
        readonly TileData _tiles;
        readonly List<Goal> _goals;
        // The bosses standing at the start (a felled one leaves World.Bosses until it comes back).
        readonly List<BossDefinition> _bosses;
        readonly Dictionary<string, long> _goalStart = new Dictionary<string, long>();
        string _goalId;
        int _tries;
        long _waitUntil;
        int _deathsAtGoal;
        int _noWayTicks;

        public FirstHourBot(World world, Player player, MonsterData monsters, LootData loot, ClassData classes, GuideData guide, TileData tiles, string stopAfter = "level5")
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _classes = classes;
            _guide = guide ?? throw new ArgumentNullException(nameof(guide));
            _tiles = tiles;
            int last = guide.Goals.ToList().FindIndex(g => g.Id == stopAfter);
            _goals = guide.Goals.Take(last < 0 ? guide.Goals.Count : last + 1).ToList();
            _bosses = world.Bosses.Select(b => b.Definition).ToList();
            Memory = new BotMemory(world, player, guide) { GoalHorizon = _goals.Count };
            Pilot = new BotPilot(world, player, Memory);
        }

        public BotMemory Memory { get; }
        public BotPilot Pilot { get; }
        public string ClassId { get; set; } = "warrior";
        // The goal the bot is on (null when done).
        public Goal Current => _goals.FirstOrDefault(g => !Memory.Goals.ContainsKey(g.Id) && !Memory.Skipped.ContainsKey(g.Id));
        public bool Done => Current == null;
        // What the bot did on the last tick, for a person following along.
        public string Doing { get; private set; } = "";

        // One tick of the player's client: call before the host's tick.
        public void Tick()
        {
            Memory.Observe();
            if (_world.Tick % ClaimEvery == 0) Claim();
            if (_player.IsDead)
            {
                _world.SetAttacking(_player.Id, false);
                Memory.Observe();
                return;
            }
            Drink();
            var goal = Current;
            if (goal == null)
            {
                _world.SetAttacking(_player.Id, false);
                return;
            }
            if (goal.Id != _goalId)
            {
                _goalId = goal.Id;
                _goalStart[goal.Id] = _world.Tick;
                _tries = 0;
                _deathsAtGoal = Memory.Deaths;
                _noWayTicks = 0;
                Pilot.Goal = goal.Id;
            }
            int budget = goal.Conditions.Any(c => c.Kind == "level" || c.Kind == "have" && BossFor(c.Id) != null) ? LongGoalTicks : GoalTicks;
            if (_world.Tick - _goalStart[goal.Id] > budget)
            {
                Skip(goal, $"{budget / World.TicksPerSecond / 60}분 안에 못 이룸");
                return;
            }
            if (_waitUntil > _world.Tick) return;
            Act(goal);
        }

        void Skip(Goal goal, string why)
        {
            if (goal == null || Memory.Refresh() && Memory.Goals.ContainsKey(goal.Id)) return;
            Memory.Skipped[goal.Id] = why;
            Pilot.Report(null, $"{goal.Id}: {why}");
            _world.SetAttacking(_player.Id, false);
        }

        // The guide's rewards, as the player's client claims them for each goal met.
        void Claim()
        {
            for (int i = 0; i < _guide.Goals.Count; i++)
            {
                var goal = _guide.Goals[i];
                if (Memory.Goals.ContainsKey(goal.Id) && !_player.ClaimedGoals.Contains(goal.Id)) _world.ClaimGoal(_player.Id, goal, i);
            }
        }

        void Drink()
        {
            if (_player.Health >= _player.Stats.MaxHealth * 0.4f || _player.ConsumableCooldown > 0) return;
            var potion = _player.Inventory.Consumables.Keys.FirstOrDefault(c => c.Id == "healing_potion" && _player.Inventory.CountOf(c) > 0);
            if (potion != null) _world.UseConsumable(_player.Id, potion);
        }

        void Act(Goal goal)
        {
            // The first condition the bot knows how to work at.
            foreach (var condition in goal.Conditions)
            {
                switch (condition.Kind)
                {
                    case "classes":
                        PickClass();
                        return;
                    case "level":
                        Hunt();
                        return;
                    case "life" when condition.Id == "mining":
                        Go(BotTarget.Wall);
                        return;
                    case "plant":
                        Plant(goal);
                        return;
                    case "request":
                        Request(goal);
                        return;
                    case "place" when condition.Tile == TileType.Soil:
                        Till(goal);
                        return;
                    case "place":
                        Place(goal, condition.Tile);
                        return;
                    case "have":
                        if (Have(goal, condition)) return;
                        break;
                }
            }
            Skip(goal, "봇이 아직 하지 못하는 목표");
        }

        // ---- What each kind of goal comes to ----

        void PickClass()
        {
            Doing = "클래스 고르기";
            var chosen = ClassRules.Choose(_classes, new[] { ClassId }, 1);
            if (chosen.Count == 0 || !_world.ChangeClasses(_player.Id, chosen, null)) Skip(Current, "클래스를 고를 수 없음");
            Memory.Mark("class");
        }

        // Works at having the thing: dug, made, caught, sold for, or a boss's drop. False when the bot cannot.
        bool Have(Goal goal, GoalCondition condition)
        {
            string id = condition.Id;
            var boss = BossFor(id);
            if (boss != null)
            {
                Boss(boss);
                return true;
            }
            if (id == "coin")
            {
                Sell(condition.Count);
                return true;
            }
            return Obtain(goal, id);
        }

        // Works at getting one more of a material, consumable or item: fished, dug, hunted, or gathered for and made.
        // False when the bot does not know how.
        bool Obtain(Goal goal, string id)
        {
            var material = _loot.Materials.FirstOrDefault(m => m.Id == id);
            if (material?.Fish != null)
            {
                Fish(goal);
                return true;
            }
            if (material != null && Source(material) is BotTarget source)
            {
                Go(source);
                return true;
            }
            var recipe = _loot.Recipes.FirstOrDefault(r => (r.Item?.Id ?? r.Material?.Id ?? r.Consumable?.Id) == id);
            if (recipe == null) return false;
            if (!Gather(recipe)) return true;
            if (Make(recipe)) WearIfGear(id);
            return true;
        }

        // Where a material comes from: the tile it is dug out of, or the monster that drops it.
        BotTarget Source(MaterialDefinition material)
        {
            var tile = _world.Tiles?.Tiles.FirstOrDefault(t => t.Material == material && (t.Tile == TileType.Wall || t.Drillable));
            if (tile != null) return tile.Tile == TileType.Wall ? BotTarget.Wall : BotTarget.Dig(tile.Tile);
            var monster = _monsters.Monsters.Where(m => m.MaterialId == material.Id && m.Weight > 0 && m.Place == null).OrderBy(m => m.Level).FirstOrDefault();
            if (monster != null) return BotTarget.Monster(monster.Id);
            return null;
        }

        BossDefinition BossFor(string material) => _bosses.FirstOrDefault(b => b.MaterialId == material);

        // Has everything the recipe takes, or goes after the first thing missing. True when all is there.
        bool Gather(Recipe recipe)
        {
            foreach (var (material, count) in recipe.Ingredients)
            {
                if (_player.Inventory.CountOfKind(material) >= count) continue;
                // A fish (grilled fish takes two minnows, 2026-11-11): caught.
                if (material.Fish != null)
                {
                    Doing = $"{recipe.Name} 재료: {material.Name} 낚기";
                    Fish(Current);
                    return false;
                }
                var source = Source(material);
                if (source == null)
                {
                    Skip(Current, $"{material.Name}을(를) 구할 줄 모름");
                    return false;
                }
                Doing = $"{recipe.Name} 재료: {material.Name}";
                Go(source);
                return false;
            }
            return true;
        }

        // Makes the recipe once, beside its station. True once made (or already making).
        bool Make(Recipe recipe)
        {
            if (recipe.Station is TileType station && !_world.Map.Near(_player.Position, station, World.StationReach - 0.2f))
            {
                Doing = $"{recipe.Name}: {station}로";
                if (!HasAny(station))
                {
                    // The station first (a workbench before what is made at it).
                    var stationRecipe = _loot.Recipes.FirstOrDefault(r => r.Material?.Places == station);
                    if (stationRecipe == null || _player.Inventory.CountOf(stationRecipe.Material) == 0 && !(Gather(stationRecipe) && Make(stationRecipe))) return false;
                    PlaceDown(stationRecipe.Material);
                    return false;
                }
                Go(BotTarget.Beside(station));
                return false;
            }
            _world.SetAttacking(_player.Id, false);
            // Still at it (World.RunCrafting): asking again would start it over.
            if (_player.Crafting != null) return false;
            int before = Count(recipe);
            if (_world.StartCrafting(_player.Id, recipe, 1) <= 0)
            {
                if (++_tries > 20) Skip(Current, $"{recipe.Name}을(를) 만들 수 없음");
                return false;
            }
            // Things that take time are made while the player stays (World.RunCrafting).
            _waitUntil = _world.Tick + 2;
            Memory.Mark("first_craft");
            Memory.Refresh();
            Doing = $"{recipe.Name} 만들기";
            return Count(recipe) > before;
        }

        int Count(Recipe recipe) =>
            recipe.Material != null ? _player.Inventory.CountOf(recipe.Material)
            : recipe.Consumable != null ? _player.Inventory.CountOf(recipe.Consumable)
            : _player.Inventory.Items.Count(i => i.Base == recipe.Item);

        void WearIfGear(string id)
        {
            var item = _player.Inventory.Items.FirstOrDefault(i => i.Base.Id == id && i.Base.RequiredLevel <= _player.Level);
            if (item != null) _world.EquipItem(_player.Id, item.Id);
        }

        bool HasAny(TileType type)
        {
            var map = _world.Map;
            int px = TileMap.Cell(_player.Position.X), py = TileMap.Cell(_player.Position.Y);
            for (int y = Math.Max(0, py - 60); y < Math.Min(map.Height, py + 60); y++)
            {
                for (int x = Math.Max(0, px - 60); x < Math.Min(map.Width, px + 60); x++)
                {
                    if (map[x, y] == type) return true;
                }
            }
            return false;
        }

        void Place(Goal goal, TileType type)
        {
            var material = _loot.Materials.FirstOrDefault(m => m.Places == type);
            if (material == null)
            {
                Skip(goal, $"{type}을(를) 놓는 재료가 없음");
                return;
            }
            if (_player.Inventory.CountOf(material) == 0)
            {
                var recipe = _loot.Recipes.FirstOrDefault(r => r.Material == material);
                if (recipe == null)
                {
                    Skip(goal, $"{material.Name} 조합법 없음");
                    return;
                }
                if (Gather(recipe)) Make(recipe);
                return;
            }
            PlaceDown(material);
        }

        // Puts it on open floor beside the player, trying each way; else a step elsewhere and again.
        void PlaceDown(MaterialDefinition material)
        {
            Doing = material.Name + " 놓기";
            _world.SetAttacking(_player.Id, false);
            bool paving = material.Places is TileType laid && TileMap.IsPaving(laid);
            if (FaceEach(() => (paving || Roomy(World.PlaceAhead(_player.Position, _player.Facing))) && _world.PlaceTile(_player.Id, material))) return;
            Wander();
        }

        // Whether something solid can go on the tile and leave room round it (2026-11-11: the bot set its workbench and
        // torch one by one in the nook it had dug among the village's things and water, and stood shut in by them): the
        // tile has open floor on two sides, and with it taken the player can still walk to forty tiles or more.
        bool Roomy((int X, int Y) tile)
        {
            var nav = Pilot.Nav;
            if (!nav.Walkable(tile.X, tile.Y) || BotNav.Ways.Count(w => nav.Walkable(tile.X + w.X, tile.Y + w.Y)) < 2) return false;
            var start = (TileMap.Cell(_player.Position.X), TileMap.Cell(_player.Position.Y));
            var reached = new HashSet<(int, int)> { start, tile };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            while (queue.Count > 0 && reached.Count < 42)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in BotNav.Ways)
                {
                    if (nav.Walkable(x + dx, y + dy) && reached.Add((x + dx, y + dy))) queue.Enqueue((x + dx, y + dy));
                }
            }
            return reached.Count >= 42;
        }

        // Faces each of the four ways in turn and does it; true when it took.
        bool FaceEach(Func<bool> act)
        {
            foreach (var (dx, dy) in BotNav.Ways)
            {
                Face(new Vec2(dx, dy));
                if (!act()) continue;
                Memory.Refresh();
                return true;
            }
            return false;
        }

        // Somewhere else to try: a little way on, past the edge of what is seen.
        void Wander()
        {
            if (++_tries > 40)
            {
                Skip(Current, "놓을 자리를 못 찾음");
                return;
            }
            var intent = Pilot.Next(BotTarget.Explore);
            Execute(intent);
            _waitUntil = _world.Tick + 10;
        }

        void Plant(Goal goal)
        {
            var crop = _tiles?.Crops.FirstOrDefault(c => _player.Inventory.CountOf(c.Seed) > 0);
            if (crop == null)
            {
                Skip(goal, "심을 씨앗이 없음");
                return;
            }
            Doing = crop.Name + " 심기";
            _world.SetAttacking(_player.Id, false);
            if (FaceEach(() => _world.Plant(_player.Id, crop))) return;
            // Not here (2026-11-11: with the bed before it, the bot came to planting far out in the ice or the abyss,
            // where a mushroom does not grow, and wandered on): to the nearest floor where the crop grows, beside it.
            int hx = TileMap.Cell(_player.Position.X), hy = TileMap.Cell(_player.Position.Y);
            if (_plantAt is (int px, int py) && (!Plantable(crop, px, py) || Memory.IsBlocked(px, py))) _plantAt = null;
            if (_plantAt == null)
            {
                var path = Pilot.Nav.Find(hx, hy, (x, y) => Plantable(crop, x, y) && !Memory.IsBlocked(x, y), enter: false);
                if (path == null || path.Count == 0)
                {
                    Skip(goal, $"{crop.Name}이(가) 자라는 바닥을 못 찾음");
                    return;
                }
                _plantAt = path[path.Count - 1];
                _plantFrom = path.Count >= 2 ? path[path.Count - 2] : (hx, hy);
            }
            var (sx, sy) = _plantFrom;
            if (hx == sx && hy == sy)
            {
                var (tx, ty) = _plantAt.Value;
                Face(new Vec2(tx - hx, ty - hy));
                if (_world.Plant(_player.Id, crop)) Memory.Refresh();
                else
                {
                    Memory.Block((tx, ty), 120 * World.TicksPerSecond);
                    _plantAt = null;
                }
                return;
            }
            Go(BotTarget.At(sx, sy));
        }

        (int X, int Y)? _plantAt;
        (int X, int Y) _plantFrom;

        // Where World.Plant would take the crop: farmland of one of its biomes, nothing growing, not by a boss's arena.
        bool Plantable(CropDefinition crop, int x, int y) =>
            !_world.Map.IsEdge(x, y) && TileMap.IsFarmland(_world.Map[x, y]) && _world.CropAt(x, y) == null
            && crop.GrowsIn(_world.FarmBiomeAt(x, y))
            && !_world.Bosses.Any(b => Vec2.Distance(TileMap.Center(x, y), b.Home) <= b.ArenaRadius + 1);

        void Till(Goal goal)
        {
            var hoe = _loot.Materials.FirstOrDefault(m => m.Id == World.Hoe);
            if (hoe == null)
            {
                Skip(goal, "호미 재료가 없음");
                return;
            }
            if (_player.Inventory.CountOf(hoe) == 0)
            {
                var recipe = _loot.Recipes.FirstOrDefault(r => r.Material == hoe);
                if (recipe != null && Gather(recipe)) Make(recipe);
                return;
            }
            Doing = "밭 갈기";
            _world.SetAttacking(_player.Id, false);
            if (!FaceEach(() => _world.Map[World.TileAhead(_player.Position, _player.Facing).X, World.TileAhead(_player.Position, _player.Facing).Y] == TileType.Floor
                && _world.Till(_player.Id))) Wander();
        }

        void Fish(Goal goal)
        {
            var rod = _loot.Materials.FirstOrDefault(m => m.Id == World.FishingRod);
            if (rod == null)
            {
                Skip(goal, "낚싯대 재료가 없음");
                return;
            }
            if (_player.Inventory.CountOf(rod) == 0)
            {
                var recipe = _loot.Recipes.FirstOrDefault(r => r.Material == rod);
                if (recipe != null && Gather(recipe)) Make(recipe);
                return;
            }
            var line = _player.Fishing;
            if (line != null)
            {
                Doing = "낚시";
                int level = World.LifeLevel(_player, LifeSkill.Fishing);
                if (line.BiteTicks > 0 && World.InCatchZone(World.GaugeAt(line.BiteElapsed, line.Difficulty), line.Difficulty, level) && _world.Fish(_player.Id)) Memory.Refresh();
                return;
            }
            var intent = Pilot.Next(BotTarget.Beside(TileType.Water));
            if (intent.Do == BotDo.Arrived && intent.Tile != null)
            {
                Face(intent.Direction);
                _world.SetAttacking(_player.Id, false);
                if (!_world.Fish(_player.Id) && ++_tries > 20) Skip(goal, "물을 보고 낚시를 할 수 없음");
                return;
            }
            Execute(intent);
        }

        // Sells to the merchant at the stall what is not needed next (fish, mushrooms, gel; ore and stone last). With
        // too little to sell, more fish first, as a person would go back to the water.
        void Sell(int coins)
        {
            var coin = _loot.Materials.FirstOrDefault(m => m.Id == "coin");
            int Coins() => coin == null ? 0 : _player.Inventory.CountOf(coin);
            if (_fishFor > FishCount())
            {
                Doing = "팔 물고기 낚기";
                Fish(Current);
                return;
            }
            var intent = Pilot.Next(BotTarget.Beside(TileType.MerchantStall));
            if (intent.Do != BotDo.Arrived)
            {
                Doing = "상인에게";
                Execute(intent);
                return;
            }
            _world.SetAttacking(_player.Id, false);
            Doing = "팔기";
            int before = Coins();
            foreach (var material in _player.Inventory.Materials.Keys.OrderBy(m => m.Id == "stone" || m.Id.EndsWith("_ore") ? 1 : 0).ThenBy(m => m.Id, StringComparer.Ordinal).ToList())
            {
                if (material == coin || material.Places != null || material.Id == World.FishingRod || material.Id == World.Hoe) continue;
                if (Coins() >= coins) return;
                if (_world.Sell(_player.Id, material, _player.Inventory.CountOf(material))) Memory.Refresh();
            }
            if (Coins() >= coins) return;
            if (++_tries > 6)
            {
                Skip(Current, "상인이 사지 않음(팔 것이 없거나 상인이 자리에 없음)");
                return;
            }
            if (Coins() == before) _fishFor = FishCount() + 2;
            _waitUntil = _world.Tick + 30;
        }

        int _fishFor;

        int FishCount() => _player.Inventory.Materials.Where(m => m.Key.Fish != null).Sum(m => m.Value);

        // The merchant's request of the day (World.RequestFor, the bag's [건네기]): what it needs gathered or made, then
        // handed over at the stall (World.CompleteRequest).
        void Request(Goal goal)
        {
            var npcs = _world.Npcs;
            var npc = npcs?.Npcs.FirstOrDefault(n => n.Tile == TileType.MerchantStall);
            var request = npc == null ? null : World.RequestFor(npcs, npc, World.FriendshipOf(_player, npc).Hearts, _world.Today);
            if (request == null)
            {
                Skip(goal, "오늘 상인의 부탁이 없음");
                return;
            }
            foreach (var (thing, count) in request.Need)
            {
                string id = thing is MaterialDefinition m ? m.Id : ((ConsumableDefinition)thing).Id;
                int have = thing is MaterialDefinition material ? _player.Inventory.CountOfKind(material) : _player.Inventory.CountOf((ConsumableDefinition)thing);
                if (have >= count) continue;
                Doing = $"부탁 \"{request.Text}\": {id} {have}/{count}";
                if (!Obtain(goal, id)) Skip(goal, $"부탁의 {id}을(를) 구할 줄 모름");
                return;
            }
            var intent = Pilot.Next(BotTarget.Beside(TileType.MerchantStall));
            if (intent.Do != BotDo.Arrived)
            {
                Doing = "부탁을 건네러 상인에게";
                Execute(intent);
                return;
            }
            _world.SetAttacking(_player.Id, false);
            Doing = "부탁 건네기";
            if (_world.CompleteRequest(_player.Id)) Memory.Refresh();
            else if (++_tries > 10) Skip(goal, "상인이 부탁을 받지 않음");
            _waitUntil = _world.Tick + 30;
        }

        // Monsters till the level: the nearest within reach of the player's level, picking up what falls.
        void Hunt()
        {
            if (_world.GroundLoot.Any(l => l.For(_player.Id, _world.Tick) && Vec2.Distance(l.Position, _player.Position) < 6 && !Memory.IsBlocked(TileMap.Cell(l.Position.X), TileMap.Cell(l.Position.Y))))
            {
                Doing = "줍기";
                var intent = Pilot.Next(BotTarget.Loot);
                // A drop that cannot be got at (in water, past what is placed) is left, not the hunt.
                if (intent.Do == BotDo.NoWay && Pilot.LastLoot is (int x, int y)) Memory.Block((x, y), 120 * World.TicksPerSecond);
                Execute(intent);
                return;
            }
            Doing = "사냥";
            Go(BotTarget.Monster());
        }

        // A boss: levels first, BossMargin over the fight's own (bosses.csv level; 2026-11-10 the web bot fell five to
        // eight times at the warden at level 3 or 4 and none at 6, and so did this one at 3), into the arena, then the
        // fight with the class's skills; up again after a fall. The bot dodges only what stands on the floor.
        void Boss(BossDefinition boss)
        {
            if (Memory.Deaths - _deathsAtGoal >= 6)
            {
                Skip(Current, $"{boss.Name}에게 6번 쓰러짐");
                return;
            }
            if (_player.Level < boss.Level + BossMargin && !Memory.Moments.ContainsKey(BotMemory.Arena + boss.Id + ":ready"))
            {
                Doing = $"{boss.Name} 앞 사냥(레벨 {_player.Level}/{boss.Level + BossMargin})";
                Hunt();
                return;
            }
            Memory.Mark(BotMemory.Arena + boss.Id + ":ready");
            if (!Memory.Moments.ContainsKey(BotMemory.Arena + boss.Id))
            {
                Doing = boss.Name + " 아레나로";
                Execute(Pilot.Next(BotTarget.Boss(boss.Id)));
                return;
            }
            var standing = _world.Bosses.FirstOrDefault(b => b.Definition == boss);
            if (standing == null || standing.IsDead)
            {
                Doing = "보스가 떨어뜨린 것 줍기";
                Go(BotTarget.Loot);
                return;
            }
            Doing = boss.Name + "와 싸움";
            var intent = Pilot.Next(BotTarget.Fight(boss.Id));
            Execute(intent);
            if (intent.Do == BotDo.Fight)
            {
                for (int slot = 0; slot < _player.Skills.Count; slot++) _world.UseSkill(_player.Id, slot, intent.Direction);
            }
        }

        // ---- Moving and swinging, as the client would ----

        // Goes for the target; gives the goal up when the pilot does, or finds no way for a while.
        void Go(BotTarget target)
        {
            var intent = Pilot.Next(target);
            if (intent.Do == BotDo.NoWay)
            {
                if (intent.Stalled || ++_noWayTicks > 10 * World.TicksPerSecond)
                {
                    Skip(Current, $"{target}: {intent.Why}");
                    return;
                }
            }
            else _noWayTicks = 0;
            Execute(intent);
        }

        public void Execute(BotIntent intent)
        {
            switch (intent.Do)
            {
                case BotDo.Move:
                    _world.SetAttacking(_player.Id, false);
                    Walk(intent.Direction);
                    break;
                case BotDo.Dig:
                    Hold(pickaxe: true);
                    if (intent.Tile is (int x, int y) && World.DigAhead(_player.Position, intent.Direction) != (x, y))
                    {
                        // Off the middle of its tile, a swing would strike the tile beside: to the middle first.
                        _world.SetAttacking(_player.Id, false);
                        var here = TileMap.Center(TileMap.Cell(_player.Position.X), TileMap.Cell(_player.Position.Y));
                        Walk(here - _player.Position, intent.Direction);
                        break;
                    }
                    Face(intent.Direction);
                    _world.SetAttacking(_player.Id, true);
                    break;
                case BotDo.Fight:
                    Hold(pickaxe: false);
                    Face(intent.Direction);
                    _world.SetAttacking(_player.Id, true);
                    break;
                default:
                    _world.SetAttacking(_player.Id, false);
                    break;
            }
        }

        void Walk(Vec2 direction, Vec2? facing = null)
        {
            if (direction.LengthSquared < 1e-4f) return;
            var input = direction.Length > 1 ? direction.Normalized : direction;
            var to = PlayerMotor.Step(_world.Map, _player.Position, input, _player.MoveSpeed, _world.DoorsFor(_player));
            _world.MovePlayer(_player.Id, to, facing ?? input, 1);
        }

        void Face(Vec2 direction)
        {
            if (direction.LengthSquared > 1e-6f) _world.MovePlayer(_player.Id, _player.Position, direction, 1);
        }

        // The pickaxe in hand for rock, else the best weapon the player may wear.
        void Hold(bool pickaxe)
        {
            if (World.HoldsPickaxe(_player) == pickaxe && _player.Equipment[ItemSlot.Weapon] != null) return;
            var item = _player.Inventory.Items
                .Where(i => i.Base.Slot == ItemSlot.Weapon && i.Base.IsPickaxe == pickaxe && i.Base.RequiredLevel <= _player.Level)
                .OrderByDescending(i => i.Base.Level).ThenBy(i => i.Id).FirstOrDefault();
            if (item != null) _world.EquipItem(_player.Id, item.Id);
        }
    }
}
