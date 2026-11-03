using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A placed machine: which way it faces, whether it is running, and what it holds. A drill, a smelter and a
    // harvester keep what they made until it can go out; a smelter also keeps what came in; a conveyor, a splitter
    // and a robot arm carry one material along. See Docs/Automation.md.
    public sealed class Machine
    {
        internal Machine(MachineDefinition definition, int x, int y, int direction)
        {
            Definition = definition;
            X = x;
            Y = y;
            Direction = direction;
        }

        public MachineDefinition Definition { get; }
        public TileType Kind => Definition.Tile;
        public int X { get; }
        public int Y { get; }
        // 0 right, 1 up, 2 left, 3 down: the way it faced when placed.
        public int Direction { get; internal set; }
        // A machine switched off does nothing by itself, but keeps what it holds, and others still put things in and
        // take them out.
        public bool Enabled { get; internal set; } = true;
        // A filter's chosen material, sent straight on; null lets everything go aside.
        public MaterialDefinition Setting { get; internal set; }
        // An assembler's chosen recipe, or null (World.SetRecipe).
        public Recipe Recipe { get; internal set; }
        // A conveyor's, splitter's or arm's load and how far along the tile it has come (or the arm has swung), 0 to 1.
        public MaterialDefinition Carried { get; internal set; }
        public float Progress { get; internal set; }
        // Ticks toward the next ore or bar; for a splitter, which of its ways out is next; for an underground belt, 1
        // while its load has come up from underground (it goes out of the front, not down again).
        internal int Timer { get; set; }
        // Power owed toward the next powered step while its grid has less than it draws (World.PoweredStep).
        internal int Charge { get; set; }
        // For a splitter: the side its load came in from (a direction), so it never sends it back, or -1.
        internal int CameFrom { get; set; } = -1;
        internal Dictionary<MaterialDefinition, int> Input { get; } = new Dictionary<MaterialDefinition, int>();
        internal Dictionary<MaterialDefinition, int> Output { get; } = new Dictionary<MaterialDefinition, int>();

        public IReadOnlyDictionary<MaterialDefinition, int> Inputs => Input;
        public IReadOnlyDictionary<MaterialDefinition, int> Outputs => Output;

        public (int X, int Y) Front => (X + Steps[Direction].X, Y + Steps[Direction].Y);
        public (int X, int Y) Back => (X - Steps[Direction].X, Y - Steps[Direction].Y);

        public static readonly (int X, int Y)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

        // Set on the direction byte, in a world save and on the wire, for a machine that is switched off.
        public const int OffBit = 0x80;

        // The nearest of the four directions to a facing.
        public static int DirectionOf(Vec2 facing) =>
            Math.Abs(facing.X) >= Math.Abs(facing.Y) ? (facing.X >= 0 ? 0 : 2) : (facing.Y >= 0 ? 1 : 3);
    }

    // Drills, conveyors, smelters, splitters, robot arms, sprinklers and harvesters, stepped every tick in tile order
    // so the result never depends on anything else.
    public sealed partial class World
    {
        // How far a sprinkler waters: the crops in the square this many tiles around it.
        public const int SprinklerReach = 2;
        // A running generator powers the machines this far from it, which then work twice as fast (World.Power.cs); a
        // lever switches the machines this far from it; an underground belt comes up at the next one facing its way
        // this far ahead.
        public const float PowerReach = 6;
        public const float LeverReach = 5;
        public const int UndergroundReach = 5;

        // Tiles that carry a load along toward their front.
        public static bool IsBelt(TileType type) => type == TileType.Conveyor || type == TileType.FastConveyor || type == TileType.UndergroundBelt;

        readonly SortedDictionary<int, Machine> _machines = new SortedDictionary<int, Machine>();
        List<Recipe> _smelts;

        public IEnumerable<Machine> Machines => _machines.Values;

        public Machine MachineAt(int x, int y) => _machines.TryGetValue(y * Map.Width + x, out var machine) ? machine : null;

        // The furnace's melting recipes: materials in, a material out.
        IReadOnlyList<Recipe> Smelts => _smelts ??= Loot?.Recipes.Where(r => r.Station == TileType.Furnace && r.Material != null).ToList() ?? new List<Recipe>();

        readonly Dictionary<TileType, IReadOnlyList<Recipe>> _processes = new Dictionary<TileType, IReadOnlyList<Recipe>>();

        // What a machine that works through recipes on its own makes: a smelter the furnace's melting recipes, a keg
        // and a preserves jar the recipes made at them (materials in, a material out). Empty for any other machine.
        public IReadOnlyList<Recipe> ProcessesOf(TileType kind)
        {
            if (kind == TileType.Smelter) return Smelts;
            if (kind != TileType.Keg && kind != TileType.PreservesJar) return Array.Empty<Recipe>();
            if (!_processes.TryGetValue(kind, out var recipes))
            {
                recipes = Loot?.Recipes.Where(r => r.Station == kind && r.Material != null).ToList() ?? new List<Recipe>();
                _processes[kind] = recipes;
            }
            return recipes;
        }

        // Whether a recipe is only ever made by a machine (a keg's, a jar's), never by hand.
        public bool MadeByMachine(Recipe recipe) => recipe.Station is TileType station && Tiles?.Machine(station) != null;

        // A machine tile appeared or went: give it its state, or drop it.
        void MachineTileChanged(int x, int y, TileType type)
        {
            int key = y * Map.Width + x;
            var definition = Tiles?.Machine(type);
            if (definition == null) _machines.Remove(key);
            else _machines[key] = new Machine(definition, x, y, 0);
            _gridsStale = true;
        }

        // Whatever the machine held goes to whoever dug it up.
        void Empty(Machine machine, Player player)
        {
            foreach (var pair in machine.Input.Concat(machine.Output)) player.Inventory.AddMaterial(pair.Key, pair.Value);
            if (machine.Carried != null) player.Inventory.AddMaterial(machine.Carried);
        }

        void UpdateMachines()
        {
            foreach (var generator in _machines.Values.Where(m => m.Kind == TileType.Generator && m.Enabled).ToList()) RunGenerator(generator);
            CountPower();
            foreach (var machine in _machines.Values)
            {
                if (!machine.Enabled || machine.Kind == TileType.Generator) continue;
                Run(machine);
                // Powered: a second step.
                if (PoweredStep(machine)) Run(machine);
            }
        }

        static float Distance(Machine a, Machine b) => Vec2.Distance(TileMap.Center(a.X, a.Y), TileMap.Center(b.X, b.Y));

        void Run(Machine machine)
        {
            switch (machine.Kind)
            {
                case TileType.Drill:
                    RunDrill(machine);
                    break;
                case TileType.Smelter:
                case TileType.Keg:
                case TileType.PreservesJar:
                    RunProcessor(machine);
                    break;
                case TileType.Conveyor:
                case TileType.FastConveyor:
                    RunConveyor(machine);
                    break;
                case TileType.UndergroundBelt:
                    RunUnderground(machine);
                    break;
                case TileType.Splitter:
                    RunSplitter(machine);
                    break;
                case TileType.Arm:
                    RunArm(machine);
                    break;
                case TileType.Sprinkler:
                    RunSprinkler(machine);
                    break;
                case TileType.Harvester:
                    RunHarvester(machine);
                    break;
                case TileType.Filter:
                    RunFilter(machine);
                    break;
                case TileType.Assembler:
                    RunAssembler(machine);
                    break;
            }
        }

        // What an assembler may be set to: a workbench recipe (or one needing no station) that makes a material or a
        // consumable. Gear is made by hand.
        public static bool Assembles(Recipe recipe) =>
            recipe != null && (recipe.Station == null || recipe.Station == TileType.Workbench) && (recipe.Material != null || recipe.Consumable != null);

        // Makes its recipe whenever its input holds the ingredients, every so often. A material comes out of the front
        // like a smelter's; a consumable only into a chest in front, and without one it waits.
        void RunAssembler(Machine assembler)
        {
            var recipe = assembler.Recipe;
            var (fx, fy) = assembler.Front;
            bool room = recipe?.Material != null ? Count(assembler.Output) + recipe.Count <= assembler.Definition.Capacity : ChestAt(fx, fy) != null;
            bool ready = recipe != null && room && recipe.Ingredients.All(i => assembler.Input.GetValueOrDefault(i.Material) >= i.Count);
            if (!ready)
            {
                assembler.Timer = 0;
            }
            else if (++assembler.Timer >= assembler.Definition.Ticks)
            {
                assembler.Timer = 0;
                foreach (var (material, count) in recipe.Ingredients) Add(assembler.Input, material, -count);
                CountMade((object)recipe.Material ?? recipe.Consumable, recipe.Count);
                if (recipe.Material != null)
                {
                    Add(assembler.Output, recipe.Material, recipe.Count);
                }
                else
                {
                    ChestAt(fx, fy).AddConsumable(recipe.Consumable, recipe.Count);
                    int key = fy * Map.Width + fx;
                    _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
                }
            }
            SendOut(assembler, assembler.Front);
        }

        // Whether the assembler takes one more of the material: an ingredient of its recipe, up to twice what one
        // make needs, so one ingredient cannot fill it while another is missing.
        static bool AssemblerTakes(Machine assembler, MaterialDefinition material)
        {
            var need = assembler.Recipe?.Ingredients.FirstOrDefault(i => i.Material == material);
            return need?.Material != null && assembler.Input.GetValueOrDefault(material) < need.Value.Count * 2;
        }

        // Sets the assembler ahead to make the recipe (null to stop). What it held that the new recipe does not use
        // goes to the player. False when the machine ahead is no assembler or the recipe is not one it makes.
        public bool SetRecipe(int playerId, Recipe recipe)
        {
            var player = FindPlayer(playerId);
            var assembler = player == null || player.IsDead ? null : MachineFacing(player.Position, player.Facing);
            if (assembler == null || assembler.Kind != TileType.Assembler || recipe != null && !Assembles(recipe)) return false;
            assembler.Recipe = recipe;
            assembler.Timer = 0;
            foreach (var pair in assembler.Input.ToList())
            {
                if (recipe != null && recipe.Ingredients.Any(i => i.Material == pair.Key)) continue;
                player.Inventory.AddMaterial(pair.Key, pair.Value);
                assembler.Input.Remove(pair.Key);
                player.InventoryVersion++;
            }
            return true;
        }

        // Burns one fuel when the last has burnt out; runs (Timer above 0) while it burns. Progress is how much of
        // the fuel is left, for the view.
        void RunGenerator(Machine generator)
        {
            if (generator.Timer > 0) generator.Timer--;
            if (generator.Timer == 0)
            {
                var fuel = generator.Input.Keys.FirstOrDefault(m => m.Fuel > 0);
                if (fuel != null)
                {
                    Add(generator.Input, fuel, -1);
                    generator.Timer = Ticks(fuel.Fuel);
                    generator.Carried = fuel;
                }
                else
                {
                    generator.Carried = null;
                }
            }
            generator.Progress = generator.Carried == null || generator.Timer == 0 ? 0 : generator.Timer / (float)Ticks(generator.Carried.Fuel);
        }

        // A filter sends its chosen material straight ahead, and anything else left and right in turn, never back the
        // way it came. It waits while the way it should go cannot take it.
        void RunFilter(Machine filter)
        {
            if (filter.Carried == null) return;
            filter.Progress = Math.Min(1, filter.Progress + 1f / filter.Definition.Ticks);
            if (filter.Progress < 1) return;
            int[] turns = filter.Carried == filter.Setting ? new[] { 0 } : new[] { 1, 3 };
            for (int k = 0; k < turns.Length; k++)
            {
                int turn = turns[(filter.Timer + k) % turns.Length];
                int direction = (filter.Direction + turn) & 3;
                if (direction == filter.CameFrom) continue;
                var (dx, dy) = Machine.Steps[direction];
                if (!Push(filter, filter.Carried, filter.X + dx, filter.Y + dy)) continue;
                filter.Carried = null;
                filter.Progress = 0;
                filter.CameFrom = -1;
                filter.Timer = (filter.Timer + k + 1) % turns.Length;
                return;
            }
        }

        // Sets the filter ahead of the player to pass this material straight on (null for none). False when the
        // machine ahead is not a filter.
        public bool SetFilter(int playerId, MaterialDefinition material)
        {
            var player = FindPlayer(playerId);
            var filter = player == null || player.IsDead ? null : MachineFacing(player.Position, player.Facing);
            if (filter == null || filter.Kind != TileType.Filter) return false;
            filter.Setting = material;
            return true;
        }

        // Machines a player can fill by hand ([넣기]): what they make things from.
        public static bool LoadsByHand(TileType kind) =>
            kind == TileType.Smelter || kind == TileType.Keg || kind == TileType.PreservesJar || kind == TileType.Assembler || kind == TileType.Generator;

        // Puts up to count of the player's material into the machine ahead, as much of it as the machine takes, the way
        // a belt would. Returns how many went in.
        public int LoadMachine(int playerId, MaterialDefinition material, int count)
        {
            var player = FindPlayer(playerId);
            var machine = player == null || player.IsDead || material == null ? null : MachineFacing(player.Position, player.Facing);
            if (machine == null || !LoadsByHand(machine.Kind)) return 0;
            int moved = 0;
            // Pushing from the machine to itself: none of these care where a load comes from.
            while (moved < count && player.Inventory.CountOf(material) > 0 && Push(machine, material, machine.X, machine.Y))
            {
                player.Inventory.TakeMaterial(material);
                moved++;
            }
            if (moved > 0) player.InventoryVersion++;
            return moved;
        }

        // Takes everything the machine ahead has made into the player's bag ([꺼내기]). Returns how many.
        public int UnloadMachine(int playerId)
        {
            var player = FindPlayer(playerId);
            var machine = player == null || player.IsDead ? null : MachineFacing(player.Position, player.Facing);
            if (machine == null || machine.Output.Count == 0) return 0;
            int moved = 0;
            foreach (var pair in machine.Output.ToList())
            {
                player.Inventory.AddMaterial(pair.Key, pair.Value);
                moved += pair.Value;
            }
            machine.Output.Clear();
            player.InventoryVersion++;
            return moved;
        }

        // Puts the player's fuel of this kind into the generator ahead, as much as it holds. False when the machine
        // ahead is not a generator, the material does not burn, or there is no room.
        public bool Refuel(int playerId, MaterialDefinition fuel)
        {
            var player = FindPlayer(playerId);
            var generator = player == null || player.IsDead || fuel == null || fuel.Fuel <= 0 ? null : MachineFacing(player.Position, player.Facing);
            if (generator == null || generator.Kind != TileType.Generator) return false;
            int room = generator.Definition.Capacity - Count(generator.Input);
            int moved = Math.Min(room, player.Inventory.CountOf(fuel));
            if (moved <= 0) return false;
            player.Inventory.TakeMaterial(fuel, moved);
            player.InventoryVersion++;
            Add(generator.Input, fuel, moved);
            return true;
        }

        // A splitter sends each load out of its front, left and right in turn, skipping a side that cannot take it and
        // the side it came in from.
        void RunSplitter(Machine splitter)
        {
            if (splitter.Carried == null) return;
            splitter.Progress = Math.Min(1, splitter.Progress + 1f / splitter.Definition.Ticks);
            if (splitter.Progress < 1) return;
            for (int k = 0; k < SplitterTurns.Length; k++)
            {
                int turn = (splitter.Timer + k) % SplitterTurns.Length;
                int direction = (splitter.Direction + SplitterTurns[turn]) & 3;
                if (direction == splitter.CameFrom) continue;
                var (dx, dy) = Machine.Steps[direction];
                if (!Push(splitter, splitter.Carried, splitter.X + dx, splitter.Y + dy)) continue;
                splitter.Carried = null;
                splitter.Progress = 0;
                splitter.CameFrom = -1;
                splitter.Timer = (turn + 1) % SplitterTurns.Length;
                return;
            }
        }

        // Front, left (a quarter turn anticlockwise) and right.
        static readonly int[] SplitterTurns = { 0, 1, 3 };

        // A robot arm picks one material up from the tile behind it, one that the tile ahead will take, swings it over
        // and puts it down there. It takes from a chest (the first material in sheet order that fits), a drill's,
        // smelter's or harvester's output, a splitter, or the end of a conveyor that faces it.
        void RunArm(Machine arm)
        {
            var (fx, fy) = arm.Front;
            if (arm.Carried == null)
            {
                arm.Progress = 0;
                arm.Carried = TakeFor(arm, fx, fy);
                return;
            }
            arm.Progress = Math.Min(1, arm.Progress + 1f / arm.Definition.Ticks);
            if (arm.Progress < 1) return;
            if (!Push(arm, arm.Carried, fx, fy)) return;
            arm.Carried = null;
            arm.Progress = 0;
        }

        MaterialDefinition TakeFor(Machine arm, int fx, int fy)
        {
            var (bx, by) = arm.Back;
            bool Fits(MaterialDefinition material) => Takes(arm, material, fx, fy);
            var source = MachineAt(bx, by);
            if (source != null)
            {
                switch (source.Kind)
                {
                    case TileType.Drill:
                    case TileType.Smelter:
                    case TileType.Harvester:
                    case TileType.Assembler:
                    case TileType.Keg:
                    case TileType.PreservesJar:
                        var made = source.Output.Keys.FirstOrDefault(Fits);
                        if (made != null) Add(source.Output, made, -1);
                        return made;
                    case TileType.Conveyor:
                    case TileType.FastConveyor:
                    case TileType.UndergroundBelt:
                    case TileType.Splitter:
                    case TileType.Filter:
                        var load = source.Carried;
                        // From a belt, only at its end, facing the arm; never a load about to go underground.
                        bool ready = !IsBelt(source.Kind) || (source.Progress >= 1 && source.Front == (arm.X, arm.Y)
                            && (source.Kind != TileType.UndergroundBelt || source.Timer == 1 || UndergroundExit(source) == null));
                        if (load == null || !ready || !Fits(load)) return null;
                        source.Carried = null;
                        source.Progress = 0;
                        return load;
                    default:
                        return null;
                }
            }
            var chest = ChestAt(bx, by);
            var taken = chest == null || Loot == null ? null : Loot.Materials.FirstOrDefault(m => chest.CountOf(m) > 0 && Fits(m));
            if (taken == null) return null;
            chest.TakeMaterial(taken);
            int key = by * Map.Width + bx;
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
            return taken;
        }

        // Pulls an ore from the vein ahead every so often, and sends one out of the back each tick it can.
        void RunDrill(Machine drill)
        {
            var (fx, fy) = drill.Front;
            var vein = Tiles.Find(Map[fx, fy]);
            if (vein != null && vein.Drillable && Count(drill.Output) < drill.Definition.Capacity && ++drill.Timer >= drill.Definition.Ticks)
            {
                drill.Timer = 0;
                Add(drill.Output, vein.Material, 1);
                CountMade(vein.Material, 1);
            }
            SendOut(drill, drill.Back);
        }

        // Makes whatever of its recipes its input allows every so often (a smelter melts, a keg brews, a jar pickles),
        // and sends results out of the front.
        void RunProcessor(Machine smelter)
        {
            // The time only runs while there is something it can make and room for the result.
            var recipe = ProcessesOf(smelter.Kind).FirstOrDefault(r => r.Ingredients.All(i => smelter.Input.GetValueOrDefault(i.Material) >= i.Count)
                && Count(smelter.Output) + r.Count <= smelter.Definition.Capacity);
            if (recipe == null)
            {
                smelter.Timer = 0;
            }
            else if (++smelter.Timer >= smelter.Definition.Ticks)
            {
                smelter.Timer = 0;
                foreach (var (material, count) in recipe.Ingredients) Add(smelter.Input, material, -count);
                Add(smelter.Output, recipe.Material, recipe.Count);
                CountMade(recipe.Material, recipe.Count);
            }
            SendOut(smelter, smelter.Front);
        }

        // Every so often waters the crops around it whose water would run out before it comes round again
        // (World.WaterCrop), so they never dry.
        void RunSprinkler(Machine sprinkler)
        {
            if (++sprinkler.Timer < sprinkler.Definition.Ticks) return;
            sprinkler.Timer = 0;
            int watered = 0;
            for (int y = sprinkler.Y - SprinklerReach; y <= sprinkler.Y + SprinklerReach; y++)
            {
                for (int x = sprinkler.X - SprinklerReach; x <= sprinkler.X + SprinklerReach; x++)
                {
                    var crop = Map.InBounds(x, y) ? CropAt(x, y) : null;
                    if (crop != null && crop.WetLeft(Tick) <= sprinkler.Definition.Ticks && WaterCrop(x, y)) watered++;
                }
            }
            // From no one: the view splashes the whole square.
            if (watered > 0) _events.Add(new SimEvent(SimEventType.Watered, 0, amount: watered, position: TileMap.Center(sprinkler.X, sprinkler.Y)));
        }

        // Every so often reaps one ripe crop in the three rows of three tiles ahead of it (nearest row first), plants
        // it again from one of the seeds, and keeps the harvest and the other seeds; it sends one out of the back each
        // tick it can, like a drill. It waits while what a crop gives would not fit, and passes over crops that give
        // food rather than a material.
        void RunHarvester(Machine harvester)
        {
            if (++harvester.Timer >= harvester.Definition.Ticks)
            {
                harvester.Timer = 0;
                var crop = HarvesterTiles(harvester).Select(t => CropAt(t.X, t.Y)).FirstOrDefault(c => c != null && c.IsRipe(Tick)
                    && c.Definition.HarvestMaterial != null
                    && Count(harvester.Output) + c.Definition.Count + c.Definition.Seeds + 1 <= harvester.Definition.Capacity);
                if (crop != null)
                {
                    var definition = crop.Definition;
                    int seeds = Reap(crop, out int count);
                    Add(harvester.Output, definition.HarvestMaterial, count);
                    CountMade(definition.HarvestMaterial, count);
                    // One that regrows is still there.
                    if (seeds > 0 && CropAt(crop.X, crop.Y) == null)
                    {
                        PlantCrop(definition, crop.X, crop.Y);
                        seeds--;
                    }
                    Add(harvester.Output, definition.Seed, seeds);
                    _events.Add(new SimEvent(SimEventType.Harvested, 0, amount: definition.Count, position: TileMap.Center(crop.X, crop.Y)));
                }
            }
            SendOut(harvester, harvester.Back);
        }

        // The nine tiles a harvester reaps, in the order it looks at them: three rows ahead of it, nearest first.
        public IEnumerable<(int X, int Y)> HarvesterTiles(Machine harvester)
        {
            var (dx, dy) = Machine.Steps[harvester.Direction];
            // A quarter turn anticlockwise from the way it faces: its left.
            var (lx, ly) = Machine.Steps[(harvester.Direction + 1) & 3];
            for (int ahead = 1; ahead <= 3; ahead++)
            {
                for (int side = -1; side <= 1; side++)
                {
                    int x = harvester.X + dx * ahead + lx * side;
                    int y = harvester.Y + dy * ahead + ly * side;
                    if (Map.InBounds(x, y)) yield return (x, y);
                }
            }
        }

        // Turns the machine ahead of the player (or the one they stand on) off, or back on. False when there is none.
        public bool ToggleMachine(int playerId)
        {
            var player = FindPlayer(playerId);
            var machine = player == null || player.IsDead ? null : MachineFacing(player.Position, player.Facing);
            if (machine == null) return false;
            machine.Enabled = !machine.Enabled;
            // A lever takes every machine in its reach with it.
            if (machine.Kind == TileType.Lever)
            {
                foreach (var other in _machines.Values.Where(m => m != machine && Distance(m, machine) <= LeverReach)) other.Enabled = machine.Enabled;
            }
            return true;
        }

        // The machine on the tile ahead, the one a player would build on, or else the one underfoot (a belt or a
        // splitter).
        public Machine MachineFacing(Vec2 position, Vec2 facing)
        {
            var (x, y) = TileAhead(position, facing);
            return (Map.InBounds(x, y) ? MachineAt(x, y) : null) ?? MachineAt(TileMap.Cell(position.X), TileMap.Cell(position.Y));
        }

        void RunConveyor(Machine belt)
        {
            if (belt.Carried == null) return;
            belt.Progress = Math.Min(1, belt.Progress + 1f / belt.Definition.Ticks);
            if (belt.Progress < 1) return;
            var (fx, fy) = belt.Front;
            if (Push(belt, belt.Carried, fx, fy)) belt.Carried = null;
        }

        // A load that came onto an underground belt from above goes down and comes up at the next underground belt
        // facing the same way within World.UndergroundReach ahead, passing under whatever lies between. With none
        // there, or once it has come up, it goes out of the front like any belt.
        void RunUnderground(Machine belt)
        {
            if (belt.Carried == null) return;
            belt.Progress = Math.Min(1, belt.Progress + 1f / belt.Definition.Ticks);
            if (belt.Progress < 1) return;
            var exit = belt.Timer == 1 ? null : UndergroundExit(belt);
            if (exit != null)
            {
                if (exit.Carried != null) return;
                exit.Carried = belt.Carried;
                exit.Progress = 0;
                exit.Timer = 1;
                belt.Carried = null;
                return;
            }
            var (fx, fy) = belt.Front;
            if (!Push(belt, belt.Carried, fx, fy)) return;
            belt.Carried = null;
            belt.Timer = 0;
        }

        // Where an underground belt's loads come up: the nearest underground belt ahead facing the same way, within
        // reach, or null.
        public Machine UndergroundExit(Machine belt)
        {
            var (dx, dy) = Machine.Steps[belt.Direction];
            for (int k = 1; k <= UndergroundReach; k++)
            {
                int x = belt.X + dx * k, y = belt.Y + dy * k;
                if (!Map.InBounds(x, y)) return null;
                var other = MachineAt(x, y);
                if (other != null && other.Kind == TileType.UndergroundBelt && other.Direction == belt.Direction) return other;
            }
            return null;
        }

        void SendOut(Machine machine, (int X, int Y) to)
        {
            var first = machine.Output.Keys.FirstOrDefault();
            if (first != null && Push(machine, first, to.X, to.Y)) Add(machine.Output, first, -1);
        }

        // Whether the tile ever takes the material from the sender, busy or not: a conveyor not facing back at it, a
        // splitter, a chest, or a smelter that melts it. A robot arm only picks up what the tile ahead takes, so it
        // never sits holding something that can go nowhere.
        bool Takes(Machine from, MaterialDefinition material, int x, int y)
        {
            var target = MachineAt(x, y);
            if (target == null) return ChestAt(x, y) != null;
            return target.Kind switch
            {
                TileType.Conveyor or TileType.FastConveyor or TileType.UndergroundBelt => target.Front != (from.X, from.Y),
                TileType.Splitter => true,
                TileType.Filter => true,
                TileType.Generator => material.Fuel > 0,
                TileType.Smelter or TileType.Keg or TileType.PreservesJar => ProcessesOf(target.Kind).Any(r => r.Ingredients.Any(i => i.Material == material)),
                TileType.Assembler => target.Recipe != null && target.Recipe.Ingredients.Any(i => i.Material == material),
                _ => false,
            };
        }

        // Hands one material to the tile: an empty conveyor not facing back at the sender, an empty splitter, a chest,
        // or a smelter that melts it and has room.
        bool Push(Machine from, MaterialDefinition material, int x, int y)
        {
            var target = MachineAt(x, y);
            if (target != null)
            {
                switch (target.Kind)
                {
                    case TileType.Conveyor:
                    case TileType.FastConveyor:
                    case TileType.UndergroundBelt:
                        if (target.Carried != null || target.Front == (from.X, from.Y)) return false;
                        target.Carried = material;
                        target.Progress = 0;
                        // Onto an underground belt from above: it goes down.
                        target.Timer = 0;
                        return true;
                    case TileType.Splitter:
                    case TileType.Filter:
                        if (target.Carried != null) return false;
                        target.Carried = material;
                        target.Progress = 0;
                        target.CameFrom = Array.FindIndex(Machine.Steps, s => (target.X + s.X, target.Y + s.Y) == (from.X, from.Y));
                        return true;
                    case TileType.Generator:
                        if (material.Fuel <= 0 || Count(target.Input) >= target.Definition.Capacity) return false;
                        Add(target.Input, material, 1);
                        return true;
                    case TileType.Smelter:
                    case TileType.Keg:
                    case TileType.PreservesJar:
                        if (!ProcessesOf(target.Kind).Any(r => r.Ingredients.Any(i => i.Material == material)) || Count(target.Input) >= target.Definition.Capacity) return false;
                        Add(target.Input, material, 1);
                        return true;
                    case TileType.Assembler:
                        if (!AssemblerTakes(target, material)) return false;
                        Add(target.Input, material, 1);
                        return true;
                    default:
                        return false;
                }
            }
            var chest = ChestAt(x, y);
            if (chest == null) return false;
            chest.AddMaterial(material);
            int key = y * Map.Width + x;
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
            return true;
        }

        static int Count(Dictionary<MaterialDefinition, int> stacks) => stacks.Values.Sum();

        static void Add(Dictionary<MaterialDefinition, int> stacks, MaterialDefinition material, int count)
        {
            int total = stacks.GetValueOrDefault(material) + count;
            if (total > 0) stacks[material] = total;
            else stacks.Remove(material);
        }

        // Puts back a machine as a world save held it.
        internal void RestoreMachine(int x, int y, int direction, int timer, MaterialDefinition carried, float progress,
            IEnumerable<(MaterialDefinition Material, int Count)> input, IEnumerable<(MaterialDefinition Material, int Count)> output,
            MaterialDefinition setting = null, Recipe recipe = null)
        {
            var machine = MachineAt(x, y);
            if (machine == null) return;
            machine.Direction = direction & 3;
            machine.Enabled = (direction & Machine.OffBit) == 0;
            _gridsStale = true;
            machine.Setting = setting;
            machine.Recipe = machine.Kind == TileType.Assembler && Assembles(recipe) ? recipe : null;
            machine.Timer = timer;
            machine.Carried = carried;
            machine.Progress = progress;
            foreach (var (material, count) in input) Add(machine.Input, material, count);
            foreach (var (material, count) in output) Add(machine.Output, material, count);
        }
    }
}
