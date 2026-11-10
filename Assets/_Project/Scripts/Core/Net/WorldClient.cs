using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Core
{
    public enum ClientStatus
    {
        Joining,
        Playing,
        Refused,
    }

    // What the screen draws for one entity, already interpolated.
    public readonly struct EntityView
    {
        public EntityView(
            int id,
            EntityKind kind,
            MonsterDefinition monster,
            BossDefinition boss,
            string name,
            Vec2 position,
            Vec2 facing,
            float health,
            float maxHealth,
            byte state,
            int phase,
            ItemBase dropBase = null,
            RuneDefinition dropRune = null,
            ConsumableDefinition dropConsumable = null,
            MaterialDefinition dropMaterial = null,
            ItemBase weapon = null,
            SkillDefinition skill = null)
        {
            Weapon = weapon;
            Skill = skill;
            Id = id;
            Kind = kind;
            Monster = monster;
            Boss = boss;
            Name = name;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            State = state;
            Phase = phase & Protocol.PhaseMask;
            Status = (byte)(phase >> 6);
            DropBase = dropBase;
            DropRune = dropRune;
            DropConsumable = dropConsumable;
            DropMaterial = dropMaterial;
        }

        public int Id { get; }
        public EntityKind Kind { get; }
        public bool IsPlayer => Kind == EntityKind.Player;
        // Set for monsters and bosses respectively.
        public MonsterDefinition Monster { get; }
        public BossDefinition Boss { get; }
        public string Name { get; }
        public Vec2 Position { get; }
        public Vec2 Facing { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public byte State { get; }
        public EnemyState EnemyState => (EnemyState)State;
        public BossState BossState => (BossState)State;
        public int Phase { get; }
        // Burning, chilled (StatusBits).
        public byte Status { get; }
        public bool Burning => (Status & StatusBits.Burning) != 0;
        public bool Chilled => (Status & StatusBits.Chilled) != 0;
        // For ground loot: the item's base and rarity, the rune, the consumable or the material.
        public ItemBase DropBase { get; }
        public RuneDefinition DropRune { get; }
        public ConsumableDefinition DropConsumable { get; }
        public MaterialDefinition DropMaterial { get; }
        public Rarity DropRarity => (Rarity)State;
        // For a player: the weapon in their hands, or null.
        public ItemBase Weapon { get; }
        // For a summoned spirit: the skill that called it. Its Health share is how much of its time is left.
        public SkillDefinition Skill { get; }
        public bool IsDead => Health <= 0;
        public float Radius => Kind switch
        {
            EntityKind.Boss => Boss.Radius,
            EntityKind.Monster => Monster.Radius,
            EntityKind.Drop => 0.2f,
            EntityKind.Minion => World.MinionRadius,
            _ => World.PlayerRadius,
        };
    }

    public readonly struct ProjectileView
    {
        public ProjectileView(int id, Vec2 position, Vec2 velocity, float radius, bool friendly, SkillTag tags)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Friendly = friendly;
            Tags = tags;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        // For pointing arrows the way they fly.
        public Vec2 Velocity { get; }
        public float Radius { get; }
        // A player's, and the tags it hits with; a boss's has none.
        public bool Friendly { get; }
        public SkillTag Tags { get; }
    }

    public readonly struct MachineView
    {
        public MachineView(int x, int y, TileType kind, int direction, MaterialDefinition carried, float progress, bool on = true,
            MaterialDefinition setting = null, bool powered = false, Recipe recipe = null, int supply = -1, int demand = -1, int holding = 0, int held = 0,
            int animals = 0, IReadOnlyList<MaterialDefinition> settings = null, int affection = 0, bool petted = false)
        {
            Affection = affection;
            Petted = petted;
            Settings = settings ?? Array.Empty<MaterialDefinition>();
            Animals = animals;
            Holding = holding;
            Held = held;
            Supply = supply;
            Demand = demand;
            Setting = setting;
            Recipe = recipe;
            Powered = powered;
            X = x;
            Y = y;
            Kind = kind;
            Direction = direction;
            Carried = carried;
            Progress = progress;
            On = on;
        }

        public int X { get; }
        public int Y { get; }
        public TileType Kind { get; }
        // 0 right, 1 up, 2 left, 3 down.
        public int Direction { get; }
        public MaterialDefinition Carried { get; }
        public float Progress { get; }
        public bool On { get; }
        // A filter's first chosen material, or null, and all of them.
        public MaterialDefinition Setting { get; }
        public IReadOnlyList<MaterialDefinition> Settings { get; }
        // An assembler's recipe, or null.
        public Recipe Recipe { get; }
        public bool Powered { get; }
        // For a generator or power pole: what its grid gives and draws (World.PowerGrid), or -1.
        public int Supply { get; }
        public int Demand { get; }
        // How much it holds to work on, and how much it made that waits to go out.
        public int Holding { get; }
        public int Held { get; }
        // A coop's chickens, a barn's cows or a fold's sheep; how fond they are (0 to World.AffectionMost), and whether
        // someone petted them today.
        public int Animals { get; }
        public int Affection { get; }
        public bool Petted { get; }
    }

    public readonly struct HazardView
    {
        public HazardView(HazardState state, float progress)
        {
            State = state;
            Progress = progress;
        }

        public HazardState State { get; }
        // 0 when it appears, 1 when it lands.
        public float Progress { get; }
    }

    // One player's side of the game. It moves its own player at once with the same PlayerMotor the host uses and
    // reports the result, and shows everyone else a little in the past, smoothly between the host's snapshots.
    // Telegraphs and projectiles are shown at the host's present instead, because the player dodges them now.
    public sealed class WorldClient
    {
        // How far behind the newest snapshot other entities are drawn. Two snapshot intervals and a bit, so one
        // late or lost snapshot does not make them stop.
        const double InterpolationDelayTicks = 5;
        const int KeptSnapshots = 12;
        // Without new snapshots, guessing the host's clock further ahead than this does more harm than good.
        const float MaxGuessSeconds = 0.25f;

        readonly MonsterData _monsters;
        readonly BossData _bosses;
        readonly LootData _loot;
        readonly ClassData _classes;
        readonly TileData _tiles;
        readonly Dictionary<int, Crop> _crops = new Dictionary<int, Crop>();
        readonly List<string> _acquired = new List<string>();
        readonly List<SnapshotMessage> _snapshots = new List<SnapshotMessage>();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        readonly List<TileChange> _changedTiles = new List<TileChange>();
        readonly Dictionary<int, List<ClassDefinition>> _rosterClasses = new Dictionary<int, List<ClassDefinition>>();
        int[] _cooldowns = Array.Empty<int>();
        int[] _buffs = Array.Empty<int>();
        int _consumableCooldown;
        // The host tick the cooldowns and buffs above were true at.
        double _skillsTick;
        long _clock;
        long _clockTick;
        int _sequence;
        int _seenCorrections;
        double _renderTick = -1;
        float _sinceLatest;

        // chosenClasses are class ids in the order the player picked them; the host has the final say.
        public WorldClient(
            string playerKey,
            string name,
            MonsterData monsters,
            BossData bosses = null,
            LootData loot = null,
            ClassData classes = null,
            IEnumerable<string> chosenClasses = null,
            TileData tiles = null)
        {
            PlayerKey = playerKey ?? "";
            Name = name ?? "";
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _bosses = bosses;
            _loot = loot;
            _classes = classes;
            _tiles = tiles;
            ChosenClasses = (chosenClasses ?? Enumerable.Empty<string>()).ToList();
        }

        public string PlayerKey { get; }
        public string Name { get; }
        public IReadOnlyList<string> ChosenClasses { get; }
        // What the host accepted, and the skills that gives, one per slot.
        public IReadOnlyList<ClassDefinition> Classes { get; private set; } = Array.Empty<ClassDefinition>();
        public IReadOnlyList<SkillDefinition> Skills { get; private set; } = Array.Empty<SkillDefinition>();
        public ClientStatus Status { get; private set; } = ClientStatus.Joining;
        public string RefusedReason { get; private set; }
        public int PlayerId { get; private set; }
        public TileMap Map { get; private set; }
        // The world's seed, from the host: what this client's own records of the world (its explored map) are kept under.
        public ulong Seed { get; private set; }
        // What the tiles do (tiles.csv); null when the client was made without the sheet.
        public TileData Tiles => _tiles;
        public MonsterData MonsterKinds => _monsters;
        // Where this player came into the world.
        public Vec2 Spawn { get; private set; }
        // Where the boss's arena is, known from the seed like the rest of the map.
        public Vec2 Arena { get; private set; }
        // Every boss arena, the first one's first (GeneratedWorld.Arenas).
        public IReadOnlyList<Vec2> Arenas { get; private set; } = new Vec2[0];
        // Which kind of cave each tile is in, worked out from the seed like the map.
        public BiomeMap Biomes { get; private set; } = BiomeMap.Plain(1, 1);
        public float ArenaRadius { get; private set; }

        // The local player, moved here first; PreviousPosition is where it was one tick earlier, for smoothing.
        public Vec2 Position { get; private set; }
        public Vec2 PreviousPosition { get; private set; }
        public Vec2 Facing { get; private set; } = new Vec2(0, -1);
        public float Health { get; private set; }
        public float MaxHealth { get; private set; } = 1;
        // Full until the first snapshot says otherwise, as a player joins with.
        public float Mana { get; private set; } = PlayerStats.BaseMana;
        public float MaxMana { get; private set; } = PlayerStats.BaseMana;
        public float MoveSpeed { get; private set; } = PlayerStats.BaseMoveSpeed;
        // The player's own burning and chilled marks (StatusBits).
        public byte SelfStatus { get; private set; }
        public bool Sleeping => (SelfStatus & StatusBits.Sleeping) != 0;
        // How many players are lying in their beds, and this player's bed (null without one).
        public int Sleepers { get; private set; }
        // The world's void rifts as the host last said (World.Rift.cs).
        public RiftState Rift { get; private set; }

        // Beside an altar with a key and no rift open: may open one.
        public bool CanOpenRift =>
            !IsDead && !Rift.Open && Map != null && Map.Near(Position, TileType.RiftAltar, World.StationReach)
            && _loot?.FindMaterial(World.RiftKey) is MaterialDefinition key && Bag.CountOf(key) > 0;

        public byte[] OpenRiftCommand() => new InventoryCommandMessage(InventoryCommand.OpenRift, 0).Encode();

        // The world's base defense as of the last snapshot (World.Defense.cs).
        public DefenseState Defense { get; private set; }

        // Whether the button over a ward stone would start a defense: beside one, none (nor a rift) under way, not
        // done today.
        public bool CanStartDefense =>
            !IsDead && !Defense.Active && !Rift.Open && !Defense.DoneToday && Map != null && Map.Near(Position, TileType.WardStone, World.StationReach);

        public byte[] StartDefenseCommand() => new InventoryCommandMessage(InventoryCommand.StartDefense, 0).Encode();
        public (int X, int Y)? Bed { get; private set; }

        // What this player is making by hand, how far through the one now (0 to 1) and how many more after it.
        public Recipe Crafting { get; private set; }
        public float CraftProgress { get; private set; }
        public int CraftLeft { get; private set; }

        // Seconds a recipe takes by hand (CraftRules.Seconds).
        public float CraftSeconds(Recipe recipe) => CraftRules.Seconds(recipe, _tiles);

        // The world's clock (Calendar) now, as the host's last word plus the time since.
        public long Clock => _clock + (long)Math.Max(0, HostTick - _clockTick);

        // Beside their own bed in the evening or at night, so they may lie down (World.Sleep says the same).
        public bool CanSleep =>
            !IsDead && Bed is (int x, int y) && Vec2.Distance(Position, TileMap.Center(x, y)) <= World.BedReach
            && (Calendar.Phase(Clock) == DayPhase.Evening || Calendar.Phase(Clock) == DayPhase.Night);

        public byte[] SleepCommand() => new InventoryCommandMessage(InventoryCommand.Sleep, 0).Encode();

        // Sets the assembler ahead to the recipe, or stops it with null (World.SetRecipe).
        public byte[] SetRecipeCommand(Recipe recipe) =>
            new InventoryCommandMessage(InventoryCommand.SetRecipe, recipe == null ? 0 : 1, index: recipe == null ? 0 : ItemCodec.Index(_loot.Recipes, recipe)).Encode();

        // The sheets this client reads items by.
        public LootData Loot => _loot;

        // Standing by an anvil, so gear can be worked (World.Refining.cs).
        public bool NearAnvil => Map != null && Map.Near(Position, TileType.Anvil, World.StationReach);

        public byte[] RefineCommand(InventoryCommand how, int itemId) => new InventoryCommandMessage(how, itemId).Encode();

        public byte[] WakeCommand() => new InventoryCommandMessage(InventoryCommand.Wake, 0).Encode();
        public bool IsDead => Status == ClientStatus.Playing && _snapshots.Count > 0 && Health <= 0;
        public IReadOnlyDictionary<int, string> Names => _names;

        // The saved character (CharacterSave) to bring along when joining, or null for a new one.
        public byte[] Character { get; set; }

        public byte[] JoinMessage() => new JoinMessage(PlayerKey, Name, ChosenClasses, character: Character, skillOrder: JoinSkillOrder).Encode();

        // Every class in the sheet, for choosing others in the game.
        public IReadOnlyList<ClassDefinition> AllClasses => _classes?.Classes ?? (IReadOnlyList<ClassDefinition>)Array.Empty<ClassDefinition>();

        // The order to put the skills in when joining (ClassRules.SkillsFor), or null.
        public IReadOnlyList<int> JoinSkillOrder { get; set; }

        // Which of the classes' skills is in each slot, as the host has them.
        public IReadOnlyList<int> SkillOrder { get; private set; } = Array.Empty<int>();

        // Goes up whenever the classes or the order change, so the screen can keep them.
        public int KitVersion { get; private set; }

        // Asks for other classes, or the same in another order (World.ChangeClasses).
        public byte[] ClassCommand(IReadOnlyList<ClassDefinition> classes, IReadOnlyList<int> order) =>
            new KitMessage(MessageType.ClassCommand, classes.Select(c => _classes.Classes.ToList().IndexOf(c)).Where(i => i >= 0).ToList(), order).Encode();

        void ApplyKit(IReadOnlyList<int> classes, IReadOnlyList<int> order)
        {
            Classes = ClassesAt(classes);
            Skills = ClassRules.Slotted(Classes, order);
            var natural = ClassRules.AllSkillsFor(Classes);
            SkillOrder = Skills.Select(s => natural.IndexOf(s)).ToList();
            _cooldowns = new int[Skills.Count];
            _buffs = new int[Skills.Count];
            RefreshStats();
            KitVersion++;
        }

        // What to save as this player's character: gear and bag as of the host's last inventory message, or null
        // before the first one.
        public byte[] SaveCharacter() => InventoryVersion == 0 ? null : CharacterSave.Write(Equipment, Bag, Experience, ClaimedGoals, Passives.Select(p => p.Id),
            Friendships.Select(pair => (pair.Key, pair.Value.Points, pair.Value.TalkedDay, pair.Value.GiftedDay, pair.Value.RequestDay, pair.Value.HeartGifts)),
            LifeExperience, SkillRanks.Select(pair => (pair.Key.Id, pair.Value)), StorySteps.Select(pair => (pair.Key, pair.Value)),
            Supports.SelectMany(pair => pair.Value.Select(gem => (pair.Key.Id, gem.Id))),
            Records.Select(pair => (pair.Key.Kind.ToString(), pair.Key.Id, pair.Value)), Achievements, Pet?.Id,
            PetExperience.Select(pair => (pair.Key, pair.Value)), LifePerks, PetNames.Select(pair => (pair.Key, pair.Value)),
            Classes.Select(c => c.Id), SkillOrder, JourneyStep, JourneyTaken, Waypoints.Select(i => Story?.Places[i].Id).Where(id => id != null),
            StoryChoices.Select(pair => (pair.Key, pair.Value)));

        // The names this player gave their pets (Pets.CleanName), by pet id: kept in the character save only, so other
        // players see the kind's name.
        public IReadOnlyDictionary<string, string> PetNames
        {
            get
            {
                if (_petNames != null) return _petNames;
                _petNames = new Dictionary<string, string>();
                if (Character == null || Character.Length == 0 || _loot == null) return _petNames;
                try
                {
                    foreach (var (pet, name) in CharacterSave.Read(Character, _loot).PetNames) _petNames[pet] = name;
                }
                catch (System.IO.InvalidDataException)
                {
                }
                return _petNames;
            }
        }

        Dictionary<string, string> _petNames;

        // Bumped whenever a name changes, so the screen knows to save the character.
        public int PetNamesVersion { get; private set; }

        public void NamePet(PetDefinition pet, string name)
        {
            if (pet == null) return;
            _ = PetNames;
            string clean = Pets.CleanName(name);
            if (clean.Length == 0 || clean == pet.Name) _petNames.Remove(pet.Id);
            else _petNames[pet.Id] = clean;
            PetNamesVersion++;
        }

        // What the pet is called: the name given, or its kind's.
        public string PetName(PetDefinition pet) => pet == null ? "" : PetNames.TryGetValue(pet.Id, out var name) ? name : pet.Name;

        // The pet following this player, as the snapshots say (PlayerPhase.PetOf).
        public PetDefinition Pet { get; private set; }

        // Each pet's experience by id (Pets.LevelOf): from the save until the host first tells, then as it tells.
        public IReadOnlyDictionary<string, int> PetExperience
        {
            get
            {
                if (_petExperience != null) return _petExperience;
                _petExperience = new Dictionary<string, int>();
                if (Character == null || Character.Length == 0 || _loot == null) return _petExperience;
                try
                {
                    foreach (var (pet, amount) in CharacterSave.Read(Character, _loot).PetExperience) _petExperience[pet] = amount;
                }
                catch (System.IO.InvalidDataException)
                {
                }
                return _petExperience;
            }
        }

        Dictionary<string, int> _petExperience;

        public int PetLevel => Pet == null ? 0 : Pets.LevelOf(PetExperience.TryGetValue(Pet.Id, out int had) ? had : 0);

        public byte[] InviteCommand() => new InventoryCommandMessage(InventoryCommand.Invite, 0).Encode();

        // Moving in (World.Housing.cs), from this client's map: whether the person at (x, y) lives in a fit room, and
        // why they could not move in now (null when they could).
        public bool LivesHere(NpcDefinition npc, int x, int y) => Npcs != null && Map != null && Housing.HomeAt(Map, x, y, PersonTiles) != null;

        public string InviteFault(NpcDefinition npc, int x, int y)
        {
            if (Npcs == null || Map == null) return "곁에 아무도 없다";
            if (HeartsWith(npc) < Housing.InviteHearts) return $"호감 {Housing.InviteHearts}부터 이사를 권할 수 있다";
            if (LivesHere(npc, x, y)) return "이미 방에 산다";
            var near = Bed is (int bx, int by) ? TileMap.Center(bx, by) : Spawn;
            return Housing.FreeRoom(Map, PersonTiles, near) == null ? Housing.NoRoom(Map, PersonTiles, near) : null;
        }

        ICollection<TileType> PersonTiles => Npcs.Npcs.Select(n => n.Tile).ToList();

        public byte[] HatchCommand(MaterialDefinition egg) => new InventoryCommandMessage(InventoryCommand.Hatch, ItemCodec.Index(_loot.Materials, egg)).Encode();

        // The bestiary (Records.cs) and the achievements had: from the save until the host first tells, then as it
        // tells.
        public IReadOnlyDictionary<(RecordKind Kind, string Id), int> Records => RecordsHeld().Records;
        public IReadOnlyCollection<string> Achievements => RecordsHeld().Achievements;

        (Dictionary<(RecordKind Kind, string Id), int> Records, HashSet<string> Achievements) _records;

        (Dictionary<(RecordKind Kind, string Id), int> Records, HashSet<string> Achievements) RecordsHeld()
        {
            if (_records.Records != null) return _records;
            _records = (new Dictionary<(RecordKind, string), int>(), new HashSet<string>());
            if (Character == null || Character.Length == 0 || _loot == null) return _records;
            try
            {
                var saved = CharacterSave.Read(Character, _loot);
                foreach (var (kind, id, count) in saved.Records)
                {
                    if (Enum.TryParse<RecordKind>(kind, out var parsed)) _records.Records[(parsed, id)] = count;
                }
                foreach (string id in saved.Achievements) _records.Achievements.Add(id);
            }
            catch (System.IO.InvalidDataException)
            {
            }
            return _records;
        }

        // What there is to find in a group, as the sheets have it here.
        public IReadOnlyList<(RecordKind Kind, string Id, string Name)> RecordEntries(RecordGroup group) =>
            Game.Core.Records.Entries(group, _monsters, _bosses?.Bosses, _loot, Tiles);

        // Experience in each life skill (LifeSkill order): from the save, and as the host tells.
        public int[] LifeExperience => _life ??= ReadLife();

        int[] _life;

        int[] ReadLife()
        {
            var life = new int[LifeSkills.Count];
            if (Character == null || Character.Length == 0 || _loot == null) return life;
            try
            {
                var saved = CharacterSave.Read(Character, _loot).LifeExperience;
                for (int i = 0; i < life.Length && i < saved.Count; i++) life[i] = saved[i];
            }
            catch (System.IO.InvalidDataException)
            {
            }
            return life;
        }

        public int LifeLevel(LifeSkill skill) => LifeSkills.LevelOf(LifeExperience[(int)skill]);

        // The life skill perks picked (LifeSkills.PerkIndex order): from the save, and as the host tells.
        public int[] LifePerks => _perks ??= ReadPerks();

        int[] _perks;

        int[] ReadPerks()
        {
            var perks = new int[LifeSkills.Count * LifeSkills.PerkTiers];
            if (Character == null || Character.Length == 0 || _loot == null) return perks;
            try
            {
                var saved = CharacterSave.Read(Character, _loot).LifePerks;
                for (int i = 0; i < perks.Length && i < saved.Count; i++) perks[i] = saved[i];
            }
            catch (System.IO.InvalidDataException)
            {
            }
            return perks;
        }

        public byte[] RepairCommand() => new InventoryCommandMessage(InventoryCommand.Repair, 0).Encode();

        public byte[] PetAnimalsCommand() => new InventoryCommandMessage(InventoryCommand.PetAnimals, 0).Encode();
        // The story (World.Story.cs, 2026-11-10): its sheets, set before joining, as the host's (the places are carved
        // from them); where the places are; and this player's step, whether it is taken, and the open waypoints.
        public StoryData Story { get; set; }
        public IReadOnlyList<PlaceSite> Places { get; private set; } = Array.Empty<PlaceSite>();
        public int JourneyStep { get; private set; }
        public bool JourneyTaken { get; private set; }
        public HashSet<int> Waypoints { get; } = new HashSet<int>();
        // The answers this player gave where the story asked, by step index (JourneyMessage).
        public Dictionary<int, int> StoryChoices { get; } = new Dictionary<int, int>();
        // Goes up whenever the host tells the player's place in the story, for the screen to notice.
        public int JourneyVersion { get; private set; }

        public StoryStep CurrentStep => Story?.Step(JourneyStep);
        public string BossName(string id) => _bosses?.Bosses.FirstOrDefault(b => b.Id == id)?.Name ?? id;
        public PlaceSite SiteOf(PlaceDefinition place) => place == null ? null : Places.FirstOrDefault(p => p.Definition == place);
        public (CharacterDefinition Character, Vec2 At)? CharacterNear() => World.CharacterNear(Places, Position);
        public PlaceSite WaypointNear() => World.WaypointNear(Places, Position);
        public PlaceSite PlaceHere() => Places.FirstOrDefault(p => p.Contains(Position));
        public byte[] TalkStoryCommand() => new InventoryCommandMessage(InventoryCommand.TalkStory, 0).Encode();

        // Answers what the person of the story asked (World.ChooseInStory, 2026-11-10).
        public byte[] ChooseStoryCommand(int choice) => new InventoryCommandMessage(InventoryCommand.ChooseStory, 0, index: choice).Encode();
        public byte[] TeleportCommand(PlaceDefinition place) => new InventoryCommandMessage(InventoryCommand.Teleport, 0, index: place.Index).Encode();

        public byte[] LockDoorCommand() => new InventoryCommandMessage(InventoryCommand.LockDoor, 0).Encode();
        public byte[] UnlockDoorCommand() => new InventoryCommandMessage(InventoryCommand.UnlockDoor, 0).Encode();

        // Locked doors as the host last told (World.Locks): who locked each, by its key (y × width + x), and the ones
        // this player locked, which they walk through.
        readonly Dictionary<int, string> _locks = new Dictionary<int, string>();
        readonly HashSet<int> _myDoors = new HashSet<int>();

        // Who locked the door at the tile, or null.
        public string LockOwner(int x, int y) => Map != null && Map.InBounds(x, y) && _locks.TryGetValue(y * Map.Width + x, out var owner) ? owner : null;
        public bool IsMyDoor(int x, int y) => Map != null && Map.InBounds(x, y) && _myDoors.Contains(y * Map.Width + x);

        // The door ahead to lock or unlock (World.DoorAhead), or null.
        public (int X, int Y)? DoorAhead() => Map == null ? null : World.DoorAhead(Map, Position, Facing);

        public byte[] StopCraftCommand() => new InventoryCommandMessage(InventoryCommand.StopCraft, 0).Encode();

        public byte[] ChoosePerkCommand(LifeSkill skill, int tier, int choice) =>
            new InventoryCommandMessage(InventoryCommand.ChoosePerk, choice, index: LifeSkills.PerkIndex(skill, tier)).Encode();

        // The people, to name and trade with them (World.Merchant.cs).
        public NpcData Npcs { get; set; }

        // How well this character knows each person, by id: from its save, and as the host tells.
        public Dictionary<string, (int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> Friendships => _friendships ??= ReadFriendships();

        readonly Dictionary<ShopEntry, int> _sold = new Dictionary<ShopEntry, int>();

        // How many more of the row's thing its person takes today, as the host last said (World.SellLeft).
        public int SellLeft(ShopEntry entry) => Math.Max(0, World.DailySellLimit - _sold.GetValueOrDefault(entry));

        Dictionary<string, (int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> _friendships;

        Dictionary<string, (int, int, int, int, int)> ReadFriendships()
        {
            var known = new Dictionary<string, (int, int, int, int, int)>();
            if (Character == null || Character.Length == 0 || _loot == null) return known;
            try
            {
                foreach (var (npc, points, talked, gifted, request, heartGifts) in CharacterSave.Read(Character, _loot).Friendships)
                    known[npc] = (points, talked, gifted, request, heartGifts);
            }
            catch (System.IO.InvalidDataException)
            {
            }
            return known;
        }

        public int Today => Calendar.Day(Clock);

        public int HeartsWith(NpcDefinition npc) => Friendships.TryGetValue(npc.Id, out var f) ? World.HeartsOf(f.Points) : 0;

        public int PointsWith(NpcDefinition npc) => Friendships.TryGetValue(npc.Id, out var f) ? f.Points : 0;

        public bool TalkedToday(NpcDefinition npc) => Friendships.TryGetValue(npc.Id, out var f) && f.TalkedDay == Today;

        public bool GiftedToday(NpcDefinition npc) => Friendships.TryGetValue(npc.Id, out var f) && f.GiftedDay == Today;

        public bool RequestDoneToday(NpcDefinition npc) => Friendships.TryGetValue(npc.Id, out var f) && f.RequestDay == Today;

        // Whether the person has given the heart gift for this many hearts (and so told that story).
        public bool HadHeartGift(NpcDefinition npc, int hearts) => Friendships.TryGetValue(npc.Id, out var f) && (f.HeartGifts & (1 << hearts)) != 0;

        // What the merchant has on sale today (World.ShopFor), or another person.
        public IReadOnlyList<ShopEntry> ShopToday => World.ShopFor(Npcs, Today);

        public IReadOnlyList<ShopEntry> ShopTodayOf(NpcDefinition npc) => World.ShopFor(Npcs, Today, npc);

        // What the person asks of this player today (World.RequestFor).
        public RequestDefinition RequestOf(NpcDefinition npc) => World.RequestFor(Npcs, npc, HeartsWith(npc), Today);

        public byte[] RequestCommand() => new InventoryCommandMessage(InventoryCommand.Request, 0).Encode();

        // How far through each person's story this player is, by person id, as the host last said; the next step
        // with the person (null when the story is over), and the command that hands it in (World.CompleteStory).
        public IReadOnlyDictionary<string, int> StorySteps => _storySteps;
        readonly Dictionary<string, int> _storySteps = new Dictionary<string, int>();

        public RequestDefinition StoryStepOf(NpcDefinition npc) => World.StoryStepFor(Npcs, npc, _storySteps.TryGetValue(npc.Id, out int done) ? done : 0);

        public byte[] StoryCommand() => new InventoryCommandMessage(InventoryCommand.Story, 0).Encode();

        // The crystals in each learned skill, in their slots, as the host last said (Docs/Classes.md, 스킬 변형),
        // and the commands that set one into a free slot or take the one in a slot out.
        public IReadOnlyDictionary<SkillDefinition, List<MaterialDefinition>> Supports => _supports;
        readonly Dictionary<SkillDefinition, List<MaterialDefinition>> _supports = new Dictionary<SkillDefinition, List<MaterialDefinition>>();

        static readonly SupportKind[] NoSupports = new SupportKind[0];

        public IReadOnlyList<SupportKind> SupportsOf(SkillDefinition skill) =>
            skill != null && _supports.TryGetValue(skill, out var gems) && gems.Count > 0
                ? gems.Select(SkillSupports.KindOf).Where(k => k != null).Select(k => k.Value).ToArray()
                : NoSupports;

        public IReadOnlyList<MaterialDefinition> GemsIn(SkillDefinition skill) =>
            skill != null && _supports.TryGetValue(skill, out var gems) ? gems : (IReadOnlyList<MaterialDefinition>)Array.Empty<MaterialDefinition>();

        public int ManaCost(SkillDefinition skill) => SkillSupports.ManaCost(skill, SupportsOf(skill));

        public byte[] SupportCommand(SkillDefinition skill, MaterialDefinition gem) =>
            new InventoryCommandMessage(InventoryCommand.Support, ItemCodec.Index(_loot.Materials, gem) + 1, index: ItemCodec.Index(_classes.Skills, skill)).Encode();

        public byte[] RemoveSupportCommand(SkillDefinition skill, int slot) =>
            new InventoryCommandMessage(InventoryCommand.Support, -slot, index: ItemCodec.Index(_classes.Skills, skill)).Encode();

        public byte[] SortBagCommand() => new InventoryCommandMessage(InventoryCommand.SortBag, 0).Encode();

        // The person whose tile is nearest within reach, and where it is, as World.NpcNear sees it.
        public (NpcDefinition Npc, int X, int Y)? NpcNear()
        {
            if (Npcs == null || Map == null) return null;
            int r = (int)MathF.Ceiling(World.StationReach) + 1;
            int cx = TileMap.Cell(Position.X), cy = TileMap.Cell(Position.Y);
            (NpcDefinition, int, int)? best = null;
            float bestDistance = float.MaxValue;
            foreach (var npc in Npcs.Npcs)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (!Map.InBounds(x, y) || Map[x, y] != npc.Tile) continue;
                        float distance = Vec2.Distance(Position, TileMap.Center(x, y));
                        if (distance > World.StationReach || distance >= bestDistance) continue;
                        best = (npc, x, y);
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        public byte[] BuyCommand(ShopEntry entry, int count) =>
            new InventoryCommandMessage(InventoryCommand.Buy, count, index: Npcs.Shop.ToList().IndexOf(entry)).Encode();

        public byte[] SellCommand(object thing, int count) => thing is ConsumableDefinition consumable
            ? new InventoryCommandMessage(InventoryCommand.Sell, (int)StackKind.Consumable + 4 * count, index: ItemCodec.Index(_loot.Consumables, consumable)).Encode()
            : new InventoryCommandMessage(InventoryCommand.Sell, (int)StackKind.Material + 4 * count, index: ItemCodec.Index(_loot.Materials, (MaterialDefinition)thing)).Encode();

        public byte[] SellItemCommand(Item item) => new InventoryCommandMessage(InventoryCommand.SellItem, item.Id).Encode();

        public byte[] GiftCommand(object thing) => thing is ConsumableDefinition consumable
            ? new InventoryCommandMessage(InventoryCommand.Gift, (int)StackKind.Consumable, index: ItemCodec.Index(_loot.Consumables, consumable)).Encode()
            : new InventoryCommandMessage(InventoryCommand.Gift, (int)StackKind.Material, index: ItemCodec.Index(_loot.Materials, (MaterialDefinition)thing)).Encode();

        public byte[] LoadCommand(MaterialDefinition material, int count) =>
            new InventoryCommandMessage(InventoryCommand.Load, count, index: ItemCodec.Index(_loot.Materials, material)).Encode();

        // A tile's name from the tile sheet (tiles.csv), or its type.
        public string TileName(TileType type) => _tiles?.Find(type)?.Name ?? type.ToString();

        public byte[] UnloadCommand() => new InventoryCommandMessage(InventoryCommand.Unload, 0).Encode();

        // Recipes a machine of the kind works through by itself (World.ProcessesOf), as the client knows them.
        public IEnumerable<Recipe> ProcessesOf(TileType kind) => Recipes.Where(r => r.Material != null
            && (kind == TileType.Smelter || kind == TileType.ArcFurnace ? r.Station == TileType.Furnace : (kind == TileType.Keg || kind == TileType.PreservesJar) && r.Station == kind));

        // Whether the recipe is made only by a machine, never by hand (World.MadeByMachine).
        public bool MadeByMachine(Recipe recipe) => recipe.Station is TileType station && _tiles?.Machine(station) != null;

        public byte[] TalkCommand() => new InventoryCommandMessage(InventoryCommand.Talk, 0).Encode();

        // The guide, to name the goals whose rewards come back (GoalReward events carry their index).
        public GuideData Guide { get; set; }

        // The guide goals this character has had rewards for: from its save, and as the host gives them.
        public HashSet<string> ClaimedGoals => _claimed ??= Character != null && Character.Length > 0 && _loot != null
            ? new HashSet<string>(ReadClaims())
            : new HashSet<string>();

        HashSet<string> _claimed;

        IEnumerable<string> ReadClaims()
        {
            try
            {
                return CharacterSave.Read(Character, _loot).ClaimedGoals;
            }
            catch (System.IO.InvalidDataException)
            {
                return Array.Empty<string>();
            }
        }

        public byte[] ClaimGoalCommand(int index) => new InventoryCommandMessage(InventoryCommand.ClaimGoal, 0, index: index).Encode();

        // As of the host's last word (SelfState). The level follows from it.
        public int Experience { get; private set; }
        public int Level => Leveling.LevelFor(Experience);

        public void Receive(byte[] data)
        {
            try
            {
                switch (Protocol.TypeOf(data))
                {
                    case MessageType.Welcome:
                        Welcome(WelcomeMessage.Decode(data));
                        break;
                    case MessageType.Refused:
                        Status = ClientStatus.Refused;
                        RefusedReason = RefusedMessage.Decode(data).Reason;
                        break;
                    case MessageType.Kit:
                        var kit = KitMessage.Decode(data, MessageType.Kit);
                        ApplyKit(kit.Classes, kit.SkillOrder);
                        break;
                    case MessageType.Roster:
                        var roster = RosterMessage.Decode(data);
                        _names.Clear();
                        _rosterClasses.Clear();
                        foreach (var pair in roster.Names) _names[pair.Key] = pair.Value;
                        foreach (var pair in roster.Classes) _rosterClasses[pair.Key] = ClassesAt(pair.Value);
                        break;
                    case MessageType.Inventory:
                        if (_loot != null) ApplyInventory(InventoryMessage.Decode(data, _loot));
                        break;
                    case MessageType.TileChanges:
                        if (Map != null) ApplyTileChanges(TileChangesMessage.Decode(data));
                        break;
                    case MessageType.CropChanges:
                        if (Map != null) ApplyCropChanges(CropChangesMessage.Decode(data));
                        break;
                    case MessageType.Chest:
                        if (_loot != null) Chest = ChestMessage.Decode(data, _loot);
                        break;
                    case MessageType.Stock:
                        _stock.Clear();
                        foreach (var (material, count) in StockMessage.Decode(data).Stock)
                        {
                            if (_loot != null && material < _loot.Materials.Count) _stock[_loot.Materials[material]] = count;
                        }
                        break;
                    case MessageType.Records:
                        var told = RecordsMessage.Decode(data);
                        var held = (new Dictionary<(RecordKind, string), int>(), new HashSet<string>());
                        foreach (var (kind, index, count) in told.Records)
                        {
                            string id = kind switch
                            {
                                RecordKind.Kill => index < _monsters.Monsters.Count ? _monsters.Monsters[index].Id : null,
                                RecordKind.Boss => _bosses != null && index < _bosses.Bosses.Count ? _bosses.Bosses[index].Id : null,
                                RecordKind.Found => _loot != null && index < _loot.Materials.Count ? _loot.Materials[index].Id : null,
                                _ => _loot != null && index < _loot.Bases.Count ? _loot.Bases[index].Id : null,
                            };
                            if (id != null) held.Item1[(kind, id)] = count;
                        }
                        foreach (int index in told.Achievements)
                        {
                            if (index < Game.Core.Records.Achievements.Count) held.Item2.Add(Game.Core.Records.Achievements[index].Id);
                        }
                        _records = held;
                        var grown = new Dictionary<string, int>();
                        foreach (var (pet, amount) in told.Pets)
                        {
                            var known = Pets.ByIndex(pet);
                            if (known != null) grown[known.Id] = amount;
                        }
                        _petExperience = grown;
                        break;
                    case MessageType.Life:
                        var lifeMessage = LifeMessage.Decode(data);
                        var experience = lifeMessage.Experience;
                        for (int i = 0; i < LifeExperience.Length && i < experience.Count; i++) LifeExperience[i] = experience[i];
                        for (int i = 0; i < LifePerks.Length && i < lifeMessage.Perks.Count; i++) LifePerks[i] = lifeMessage.Perks[i];
                        break;
                    case MessageType.Friendships:
                        var friendships = FriendshipsMessage.Decode(data);
                        foreach (var (npc, points, talked, gifted, request, heartGifts) in friendships.Friendships)
                        {
                            if (Npcs != null && npc < Npcs.Npcs.Count) Friendships[Npcs.Npcs[npc].Id] = (points, talked, gifted, request, heartGifts);
                        }
                        _sold.Clear();
                        foreach (var (entry, count) in friendships.Sold)
                        {
                            if (Npcs != null && entry < Npcs.Shop.Count) _sold[Npcs.Shop[entry]] = count;
                        }
                        break;
                    case MessageType.Production:
                        var production = ProductionMessage.Decode(data);
                        ProductionMinutes = production.Minutes;
                        Production = production.Made
                            .Select(p => (p.Kind == 1
                                ? (object)(_loot != null && p.Index < _loot.Consumables.Count ? _loot.Consumables[p.Index] : null)
                                : _loot != null && p.Index < _loot.Materials.Count ? _loot.Materials[p.Index] : null, p.Count))
                            .Where(p => p.Item1 != null)
                            .ToList();
                        break;
                    case MessageType.Machines:
                        // The host sends what changed (2026-11-09): each one replaces what was heard of it before.
                        foreach (var machine in MachinesMessage.Decode(data).Machines) _machineStates[machine.Y * 65536 + machine.X] = (machine, HostTick);
                        break;
                    case MessageType.Journey:
                        var journey = JourneyMessage.Decode(data);
                        JourneyStep = journey.Step;
                        JourneyTaken = journey.Taken;
                        Waypoints.Clear();
                        foreach (int index in journey.Waypoints) Waypoints.Add(index);
                        StoryChoices.Clear();
                        foreach (var (at, choice) in journey.Choices) StoryChoices[at] = choice;
                        JourneyVersion++;
                        break;
                    case MessageType.Locks:
                        _locks.Clear();
                        _myDoors.Clear();
                        if (Map == null) break;
                        foreach (var (x, y, owner, mine) in LocksMessage.Decode(data).Locks)
                        {
                            if (!Map.InBounds(x, y)) continue;
                            _locks[y * Map.Width + x] = owner;
                            if (mine) _myDoors.Add(y * Map.Width + x);
                        }
                        break;
                    case MessageType.Snapshot:
                        if (Status == ClientStatus.Playing) Apply(SnapshotMessage.Decode(data));
                        break;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                // A garbled message is dropped; the next snapshot replaces it.
            }
        }

        // One fixed tick of the local player. Returns the input message to send to the host, or null before joining.
        public byte[] Step(Vec2 move, Vec2 aim, bool attack)
        {
            if (Status != ClientStatus.Playing) return null;
            PreviousPosition = Position;
            if (!IsDead)
            {
                Position = PlayerMotor.Step(Map, Position, move, MoveSpeed, _myDoors);
                if (aim.LengthSquared > 1e-6f) Facing = aim.Normalized;
            }
            _sequence++;
            return new InputMessage(_sequence, Position, Facing, attack && !IsDead).Encode();
        }

        // Moves the clock that other entities are drawn at. Call once per rendered frame.
        public void AdvanceRenderClock(float seconds)
        {
            if (_snapshots.Count == 0) return;
            _sinceLatest += seconds;
            double target = _snapshots[_snapshots.Count - 1].Tick - InterpolationDelayTicks;
            _renderTick += seconds * World.TicksPerSecond;
            // Far off (first snapshot, a stall, a burst) jumps; a small drift is eased out so motion stays smooth.
            if (Math.Abs(_renderTick - target) > World.TicksPerSecond / 2.0) _renderTick = target;
            else _renderTick += (target - _renderTick) * 0.05;
        }

        // Everyone but the local player, interpolated at the render clock.
        public List<EntityView> Others()
        {
            var views = new List<EntityView>();
            if (_snapshots.Count == 0) return views;

            int after = _snapshots.FindIndex(s => s.Tick > _renderTick);
            var newer = after < 0 ? _snapshots[_snapshots.Count - 1] : _snapshots[after];
            var older = after <= 0 ? newer : _snapshots[after - 1];
            float t = newer.Tick == older.Tick ? 1 : (float)Math.Clamp((_renderTick - older.Tick) / (newer.Tick - older.Tick), 0, 1);
            var before = older.Entities.ToDictionary(e => e.Id);

            foreach (var entity in newer.Entities)
            {
                if (entity.Id == PlayerId) continue;
                var position = before.TryGetValue(entity.Id, out var then) ? Vec2.Lerp(then.Position, entity.Position, t) : entity.Position;
                var shown = t < 0.5f && before.ContainsKey(entity.Id) ? then : entity;
                views.Add(new EntityView(
                    entity.Id,
                    entity.Kind,
                    entity.Kind == EntityKind.Monster ? _monsters.Monsters[entity.Type] : null,
                    entity.Kind == EntityKind.Boss ? _bosses.Bosses[entity.Type] : null,
                    entity.IsPlayer && _names.TryGetValue(entity.Id, out string name) ? name : "",
                    position,
                    shown.Facing,
                    shown.Health,
                    shown.MaxHealth,
                    shown.State,
                    shown.Phase,
                    entity.Kind == EntityKind.Drop && entity.State < DropState.Material ? _loot.Bases[entity.Type] : null,
                    entity.Kind == EntityKind.Drop && entity.State == DropState.Rune ? _loot.Runes[entity.Type] : null,
                    entity.Kind == EntityKind.Drop && entity.State == DropState.Consumable ? _loot.Consumables[entity.Type] : null,
                    entity.Kind == EntityKind.Drop && entity.State == DropState.Material ? _loot.Materials[entity.Type] : null,
                    entity.IsPlayer && entity.Type > 0 ? _loot.Bases[entity.Type - 1] : null,
                    entity.Kind == EntityKind.Minion ? _classes.Skills[entity.Type] : null));
            }
            return views;
        }

        // Seconds until the skill in the slot is ready, and until its buff runs out, counting down between snapshots.
        public float CooldownLeft(int slot) => SkillTimeLeft(_cooldowns, slot);

        // Whether the mana the host last said is enough for the skill in the slot.
        public bool HasManaFor(int slot) => slot >= 0 && slot < Skills.Count && Mana >= ManaCost(Skills[slot]);

        public float BuffLeft(int slot) => SkillTimeLeft(_buffs, slot);

        float SkillTimeLeft(int[] ticks, int slot)
        {
            if (slot < 0 || slot >= ticks.Length) return 0;
            return (float)Math.Max(0, (ticks[slot] - (HostTick - _skillsTick)) / World.TicksPerSecond);
        }

        // The message that uses the skill in the slot, or null when it plainly cannot be used yet. The cooldown
        // starts on screen at once; the host decides and the next snapshot says what really happened.
        public byte[] SkillCommand(int slot, Vec2 aim)
        {
            if (Status != ClientStatus.Playing || IsDead || slot < 0 || slot >= Skills.Count || CooldownLeft(slot) > 0 || !HasManaFor(slot)) return null;
            _cooldowns[slot] = (int)Math.Round(SkillRules.CooldownTicks(Skills[slot], PowerOf(Skills[slot]), Stats.CooldownRecoveryFor(Skills[slot].Tags))
                * SkillSupports.CooldownFactor(SupportsOf(Skills[slot]))) + (int)Math.Ceiling(HostTick - _skillsTick);
            Mana -= ManaCost(Skills[slot]);
            return new SkillCommandMessage(slot, aim).Encode();
        }

        // Another player's classes as the host's roster has them, and the skill in one of their slots.
        public IReadOnlyList<ClassDefinition> ClassesOf(int playerId) =>
            playerId == PlayerId ? Classes
            : _rosterClasses.TryGetValue(playerId, out var classes) ? classes
            : (IReadOnlyList<ClassDefinition>)Array.Empty<ClassDefinition>();

        // Another player's slots are learned from the skills they are seen using (SkillUsed names the skill); until
        // then, their classes' first skills stand in.
        public SkillDefinition SkillOf(int playerId, int slot)
        {
            if (playerId != PlayerId && _seenSlots.TryGetValue(playerId, out var seen) && seen.TryGetValue(slot, out var used)) return used;
            var skills = playerId == PlayerId ? Skills : ClassRules.SkillsFor(ClassesOf(playerId));
            return slot >= 0 && slot < skills.Count ? skills[slot] : null;
        }

        readonly Dictionary<int, Dictionary<int, SkillDefinition>> _seenSlots = new Dictionary<int, Dictionary<int, SkillDefinition>>();

        // The skill a SkillUsed or SkillLearned event names (its Target), or null.
        public SkillDefinition SkillNamed(int target) =>
            _classes != null && target > 0 && target <= _classes.Skills.Count ? _classes.Skills[target - 1] : null;

        // Every skill of the chosen classes, in the order ClassRules.AllSkillsFor gives (the order SkillOrder counts in).
        public IReadOnlyList<SkillDefinition> ClassSkills => ClassRules.AllSkillsFor(Classes);

        // The ranks of the skills this player has learned, as the host last said (Docs/Classes.md, 스킬 포인트와 단계).
        public IReadOnlyDictionary<SkillDefinition, int> SkillRanks => _skillRanks;
        readonly Dictionary<SkillDefinition, int> _skillRanks = new Dictionary<SkillDefinition, int>();

        public int RankOf(SkillDefinition skill) => skill != null && _skillRanks.TryGetValue(skill, out int rank) ? rank : 0;

        public int SkillPoints => SkillRules.PointsLeft(Level, _skillRanks);

        // The rank the skill works at, the learned one and what the passive tree and gear add (SkillRules.Power).
        public int PowerOf(SkillDefinition skill) => SkillRules.Power(RankOf(skill), Stats, skill);

        // What forgetting every skill costs now (World.ResetSkills), and the command.
        public int SkillResetCost => SkillRules.SpentPoints(_skillRanks) * SkillRules.ResetCoinsPerPoint;

        public byte[] ResetSkillsCommand() => new InventoryCommandMessage(InventoryCommand.ResetSkills, 0).Encode();

        public bool CanRaise(SkillDefinition skill) => SkillRules.CanRaise(skill, Classes, Level, SkillPoints, RankOf(skill));

        public byte[] LearnSkillCommand(SkillDefinition skill) =>
            new InventoryCommandMessage(InventoryCommand.LearnSkill, 0, index: ItemCodec.Index(_classes.Skills, skill)).Encode();

        // The host's current tick as best this client can tell: the newest snapshot plus the time since it came.
        public double HostTick =>
            _snapshots.Count == 0 ? 0 : _snapshots[_snapshots.Count - 1].Tick + Math.Min(_sinceLatest, MaxGuessSeconds) * World.TicksPerSecond;

        // Projectiles moved on to where they are now.
        public List<ProjectileView> Projectiles()
        {
            var views = new List<ProjectileView>();
            if (_snapshots.Count == 0) return views;
            var latest = _snapshots[_snapshots.Count - 1];
            float seconds = (float)((HostTick - latest.Tick) / World.TicksPerSecond);
            foreach (var projectile in latest.Projectiles)
            {
                views.Add(new ProjectileView(
                    projectile.Id, projectile.Position + projectile.Velocity * seconds, projectile.Velocity, projectile.Radius, projectile.Friendly, projectile.Tags));
            }
            return views;
        }

        // Telegraphs with how far along they are right now; ones that have already landed are left out.
        public List<HazardView> Hazards()
        {
            var views = new List<HazardView>();
            if (_snapshots.Count == 0) return views;
            var latest = _snapshots[_snapshots.Count - 1];
            double now = HostTick;
            foreach (var hazard in latest.Hazards)
            {
                double lands = latest.Tick + hazard.TicksLeft;
                if (now > lands) continue;
                double starts = lands - Math.Max(1, hazard.Duration);
                views.Add(new HazardView(hazard, (float)Math.Clamp((now - starts) / Math.Max(1, hazard.Duration), 0, 1)));
            }
            return views;
        }

        // What this player wears and carries, as of the host's last inventory message.
        public Equipment Equipment { get; private set; } = new Equipment();
        public Inventory Bag { get; private set; } = new Inventory();
        // Worked out here the same way the host does, from the gear, classes and buffs it reported.
        public PlayerStats Stats { get; private set; } = PlayerStats.From(new Equipment());
        // Goes up with every inventory message, so a screen knows to redraw.
        public int InventoryVersion { get; private set; }

        public byte[] EquipCommand(int itemId) => new InventoryCommandMessage(InventoryCommand.Equip, itemId).Encode();

        public byte[] UnequipCommand(ItemSlot slot) => new InventoryCommandMessage(InventoryCommand.Unequip, 0, slot).Encode();

        public byte[] InsertRuneCommand(int itemId, RuneDefinition rune) =>
            new InventoryCommandMessage(InventoryCommand.InsertRune, itemId, index: ItemCodec.Index(_loot.Runes, rune)).Encode();

        // The message that drinks or eats one, or null when it plainly cannot be used: none in the bag, still waiting,
        // or health already full. The wait starts on screen at once, like a skill's cooldown.
        public byte[] UseConsumableCommand(ConsumableDefinition consumable)
        {
            if (Status != ClientStatus.Playing || _snapshots.Count == 0 || IsDead || consumable == null || Bag.CountOf(consumable) == 0) return null;
            if (ConsumableCooldownLeft > 0 || (Health >= MaxHealth && !consumable.UsableAtFullHealth)) return null;
            if (consumable.RestoresMana && Mana >= MaxMana) return null;
            _consumableCooldown = World.Ticks(consumable.Cooldown) + (int)Math.Ceiling(HostTick - _skillsTick);
            return new InventoryCommandMessage(InventoryCommand.UseConsumable, 0, index: ItemCodec.Index(_loot.Consumables, consumable)).Encode();
        }

        // In the order the crafting list shows them.
        public IReadOnlyList<Recipe> Recipes => _loot?.Recipes ?? (IReadOnlyList<Recipe>)Array.Empty<Recipe>();

        // Each machine as last heard of and when. One the host has not mentioned for MachineStaleSeconds is out of view
        // or gone; one whose tile is no longer that machine went with it.
        readonly Dictionary<int, (MachineState State, double Tick)> _machineStates = new Dictionary<int, (MachineState State, double Tick)>();
        const double MachineStaleSeconds = 2.5;

        // What machines made lately (World.Production): a MaterialDefinition or ConsumableDefinition each, most first,
        // over ProductionMinutes.
        public IReadOnlyList<(object Made, int Count)> Production { get; private set; } = Array.Empty<(object, int)>();
        public float ProductionMinutes { get; private set; }

        // The machines in view, with conveyor loads moved on to where they are now.
        public List<MachineView> Machines()
        {
            var views = new List<MachineView>(_machineStates.Count);
            foreach (var key in _machineStates.Keys.ToList())
            {
                var (m, heard) = _machineStates[key];
                if (HostTick - heard > MachineStaleSeconds * World.TicksPerSecond || Map != null && Map.InBounds(m.X, m.Y) && Map[m.X, m.Y] != m.Kind)
                {
                    _machineStates.Remove(key);
                    continue;
                }
                var carried = m.Carried >= 0 && _loot != null && m.Carried < _loot.Materials.Count ? _loot.Materials[m.Carried] : null;
                float progress = m.Progress;
                var definition = _tiles?.Machine(m.Kind);
                // Loads move on between messages (twice as fast with power); a generator's fuel does not.
                if (carried != null && definition != null && m.On && !World.IsGenerator(m.Kind))
                {
                    progress = Math.Min(1, progress + (float)((HostTick - heard) / definition.Ticks * (m.Powered ? 2 : 1)));
                }
                bool assembler = World.IsAssembler(m.Kind);
                bool node = World.IsPowerNode(m.Kind);
                bool coop = World.HouseOf(m.Kind) != null;
                int supply = node && m.Setting >= 0 ? m.Setting / 256 : -1;
                int demand = node && m.Setting >= 0 ? m.Setting % 256 : -1;
                var setting = !assembler && !node && !coop && m.Setting >= 0 && _loot != null && m.Setting < _loot.Materials.Count ? _loot.Materials[m.Setting] : null;
                var recipe = assembler && m.Setting >= 0 && _loot != null && m.Setting < _loot.Recipes.Count ? _loot.Recipes[m.Setting] : null;
                var picks = _loot == null ? new List<MaterialDefinition>() : m.Picks.Where(i => i >= 0 && i < _loot.Materials.Count).Select(i => _loot.Materials[i]).ToList();
                views.Add(new MachineView(m.X, m.Y, m.Kind, m.Direction, carried, progress, m.On, setting, m.Powered, recipe, supply, demand, m.Holding, m.Held,
                    coop ? World.AnimalsOf(m.Setting) : 0, picks, coop ? World.AffectionOf(m.Setting) : 0, coop && World.PettedOf(m.Setting)));
            }
            return views;
        }

        // The machine a switch press would turn off or on (World.MachineFacing), as of the host's last word, or null.
        public MachineView? MachineAhead()
        {
            if (Map == null) return null;
            var (x, y) = World.TileAhead(Position, Facing);
            // A big machine's part stands for the machine.
            if (World.AnchorOf(Map, x, y) is (int ax, int ay)) (x, y) = (ax, ay);
            int ux = TileMap.Cell(Position.X);
            int uy = TileMap.Cell(Position.Y);
            MachineView? under = null;
            foreach (var m in Machines())
            {
                if (m.X == x && m.Y == y) return m;
                if (m.X == ux && m.Y == uy) under = m;
            }
            return under;
        }

        // Sets the filter ahead to pass this material straight on, or nothing.
        public byte[] SetFilterCommand(MaterialDefinition material) =>
            new InventoryCommandMessage(InventoryCommand.SetFilter, material == null ? 0 : 1, index: material == null ? 0 : ItemCodec.Index(_loot.Materials, material)).Encode();

        public byte[] RefuelCommand(MaterialDefinition fuel) =>
            new InventoryCommandMessage(InventoryCommand.Refuel, 0, index: ItemCodec.Index(_loot.Materials, fuel)).Encode();

        public byte[] TillCommand() => new InventoryCommandMessage(InventoryCommand.Till, 0).Encode();

        public byte[] FertilizeCommand() => new InventoryCommandMessage(InventoryCommand.Fertilize, 0).Encode();

        // Casts, lands a biting fish or reels in (World.Fish).
        // While a fish bites, it says how far the bite had gone on this screen when the player pressed.
        public byte[] FishCommand() => new InventoryCommandMessage(InventoryCommand.Fish, Fishing == World.FishingBite ? BiteTicksSeen + 1 : 0).Encode();

        // Where this player's line is at: World.FishingCast while waiting, World.FishingBite while a fish bites, or -1.
        public int Fishing { get; private set; } = -1;

        // While a fish bites: how hard it is, and when (in host ticks) this client heard of the bite.
        public int FishingDifficulty { get; private set; } = 1;
        public double BiteTick { get; private set; }

        // Where the reel gauge's marker is now, as this client shows it (World.GaugeAt).
        public int BiteTicksSeen => (int)Math.Max(0, HostTick - BiteTick);

        public byte[] ToggleMachineCommand() => new InventoryCommandMessage(InventoryCommand.ToggleMachine, 0).Encode();

        // The chest this player stands by and what it holds, or a closed one.
        public ChestMessage Chest { get; private set; } = ChestMessage.Closed;

        public byte[] StoreItemCommand(int itemId) => new InventoryCommandMessage(InventoryCommand.StoreItem, itemId).Encode();

        public byte[] TakeItemCommand(int itemId) => new InventoryCommandMessage(InventoryCommand.TakeItem, itemId).Encode();

        public byte[] StoreStackCommand(StackKind kind, object definition) => StackCommand(InventoryCommand.StoreStack, kind, definition);

        public byte[] TakeStackCommand(StackKind kind, object definition) => StackCommand(InventoryCommand.TakeStack, kind, definition);

        byte[] StackCommand(InventoryCommand command, StackKind kind, object definition)
        {
            int index = kind switch
            {
                StackKind.Rune => ItemCodec.Index(_loot.Runes, (RuneDefinition)definition),
                StackKind.Consumable => ItemCodec.Index(_loot.Consumables, (ConsumableDefinition)definition),
                _ => ItemCodec.Index(_loot.Materials, (MaterialDefinition)definition),
            };
            return new InventoryCommandMessage(command, (int)kind, index: index).Encode();
        }

        // The station the recipe needs and does not have in reach, or null when it can be made here.
        public TileType? MissingStation(Recipe recipe) =>
            recipe.Station != null && (Map == null || !Map.Near(Position, recipe.Station.Value, World.StationReach)) ? recipe.Station : null;

        // What is growing, as of the host's last word. How ripe each is comes from HostTick.
        public IReadOnlyCollection<Crop> Crops => _crops.Values;

        // The crop this material grows, or null when it is not a seed.
        public CropDefinition CropFrom(MaterialDefinition material) => _tiles?.CropFrom(material);

        public byte[] WaterCommand() => new InventoryCommandMessage(InventoryCommand.Water, 0).Encode();

        // Null, with a notice saying where it grows, when the tile ahead is not one of the crop's biomes (the host would
        // refuse it the same way, World.Plant).
        public byte[] PlantCommand(CropDefinition crop)
        {
            if (!GrowsAhead(crop))
            {
                _notices.Add($"{crop.Name}: {crop.BiomeNames}에서만 자란다. 그곳의 흙을 가져와 깔아도 된다");
                return null;
            }
            return new InventoryCommandMessage(InventoryCommand.Plant, 0, index: ItemCodec.Index(_tiles.Crops, crop)).Encode();
        }

        // Whether the crop grows on the tile ahead: its biome, or the biome of a soil laid there (World.FarmBiomeAt).
        public bool GrowsAhead(CropDefinition crop)
        {
            if (Map == null || crop == null) return false;
            var (x, y) = World.TileAhead(Position, Facing);
            if (!Map.InBounds(x, y)) return false;
            return crop.GrowsIn(TileMap.SoilBiome(Map[x, y]) ?? Biomes[x, y]);
        }

        // Things to tell the player that came from this side, not the host: why a seed was not planted.
        public List<string> TakeNotices()
        {
            var lines = new List<string>(_notices);
            _notices.Clear();
            return lines;
        }

        // times over makes it that many times, as far as the materials go (WorldHost.MaxCraftAtOnce at most).
        public byte[] CraftCommand(Recipe recipe, int times = 1) =>
            new InventoryCommandMessage(InventoryCommand.Craft, times, index: ItemCodec.Index(_loot.Recipes, recipe)).Encode();

        // With a notice when the tile it would go on is not open floor, so a refused [놓기] is not silent (the host
        // decides, World.PlaceTile).
        public byte[] PlaceCommand(MaterialDefinition material)
        {
            if (Map != null && material?.Places != null)
            {
                var (fx, fy) = World.TileAhead(Position, Facing);
                var (x, y) = TileMap.IsPaving(material.Places.Value) ? (fx, fy) : World.PlaceAhead(Position, Facing);
                if (!Map.InBounds(x, y) || Map.IsEdge(x, y) || !TileMap.IsGround(Map[x, y]) || Map[x, y] == material.Places)
                {
                    // The tile right ahead is open but the body is in it, and the one after is not: step back.
                    bool tooClose = (x, y) != (fx, fy) && Map.InBounds(fx, fy) && TileMap.IsGround(Map[fx, fy]);
                    _notices.Add(tooClose ? $"{material.Name}: 너무 붙어 섰다. 한 걸음 물러서서 놓는다" : $"{material.Name}: 앞이 막혀 있다. 빈 바닥을 바라보고 놓는다");
                }
            }
            return new InventoryCommandMessage(InventoryCommand.Place, 0, index: ItemCodec.Index(_loot.Materials, material)).Encode();
        }

        // Whether the bag holds everything the recipe takes, and room for an item it makes.
        public bool CanCraft(Recipe recipe) => CraftableTimes(recipe) > 0;

        // How many times over the recipe can be made here, from the bag and the chests nearby (StockNear), up to
        // WorldHost.MaxCraftAtOnce: 0 when something is missing, the station is out of reach or an item has no room.
        public int CraftableTimes(Recipe recipe)
        {
            if (MissingStation(recipe) != null) return 0;
            int times = WorldHost.MaxCraftAtOnce;
            foreach (var (material, count) in recipe.Ingredients) times = Math.Min(times, Available(material) / count);
            if (recipe.Item != null) times = Math.Min(times, Inventory.Capacity - Bag.Items.Count);
            return Math.Max(0, times);
        }

        // In the bag and in the chests nearby.
        // Any quality of a crop counts (MaterialDefinition.Kind).
        public int Available(MaterialDefinition material) => Bag.CountOfKind(material) + material.AllQualities.Sum(StockOf);

        // In the chests nearby, as of the host's last word.
        public int StockOf(MaterialDefinition material) => _stock.TryGetValue(material, out int count) ? count : 0;

        readonly Dictionary<MaterialDefinition, int> _stock = new Dictionary<MaterialDefinition, int>();

        // Cells whose tile changed since the last call, for redrawing them.
        public List<TileChange> TakeChangedTiles()
        {
            var changes = new List<TileChange>(_changedTiles);
            _changedTiles.Clear();
            return changes;
        }

        // What the quick key and button use: the first consumable in the sheet that the bag has, or null.
        public ConsumableDefinition QuickConsumable => _loot?.Consumables.FirstOrDefault(c => Bag.CountOf(c) > 0);

        // Seconds until any consumable can be used again.
        // The consumable at a sheet index (as an Ate event names it), or null.
        public ConsumableDefinition ConsumableAt(int index) =>
            _loot != null && index >= 0 && index < _loot.Consumables.Count ? _loot.Consumables[index] : null;

        // The food whose buff is on, or null, and the seconds it has left, counting down between snapshots.
        public ConsumableDefinition Food { get; private set; }
        int _foodTicks;
        public float FoodLeft => Food == null ? 0 : (float)Math.Max(0, (_foodTicks - (HostTick - _skillsTick)) / World.TicksPerSecond);

        public float ConsumableCooldownLeft => (float)Math.Max(0, (_consumableCooldown - (HostTick - _skillsTick)) / World.TicksPerSecond);

        public byte[] DiscardCommand(int itemId) => new InventoryCommandMessage(InventoryCommand.Discard, itemId).Encode();

        readonly List<string> _notices = new List<string>();

        // Lines like "획득: [희귀] 철 검" for whatever arrived in the bag since the last call.
        public List<string> TakeAcquired()
        {
            var lines = new List<string>(_acquired);
            _acquired.Clear();
            return lines;
        }

        void ApplyInventory(InventoryMessage message)
        {
            if (InventoryVersion > 0)
            {
                var known = new HashSet<int>(Bag.Items.Concat(Equipment.Items).Select(item => item.Id));
                foreach (var item in message.Bag.Items.Where(item => !known.Contains(item.Id)))
                {
                    _acquired.Add($"획득: [{ItemText.RarityName(item.Rarity)}] {ItemText.Name(item)}");
                }
                foreach (var pair in message.Bag.Runes.Where(pair => pair.Value > Bag.CountOf(pair.Key)))
                {
                    _acquired.Add($"획득: {pair.Key.Name}");
                }
                foreach (var pair in message.Bag.Consumables.Where(pair => pair.Value > Bag.CountOf(pair.Key)))
                {
                    _acquired.Add($"획득: {pair.Key.Name}");
                }
                foreach (var pair in message.Bag.Materials.Where(pair => pair.Value > Bag.CountOf(pair.Key)))
                {
                    _acquired.Add($"획득: {pair.Key.Name} ×{pair.Value - Bag.CountOf(pair.Key)}");
                }
            }
            Equipment = message.Equipment;
            Bag = message.Bag;
            var passives = _classes?.Passives;
            Passives = message.Passives.Where(i => passives != null && i < passives.Count).Select(i => passives[i]).ToList();
            _skillRanks.Clear();
            foreach (var (skill, rank) in message.Skills)
            {
                if (_classes != null && skill < _classes.Skills.Count) _skillRanks[_classes.Skills[skill]] = rank;
            }
            _supports.Clear();
            foreach (var (skill, gem) in message.Supports)
            {
                if (_classes == null || skill >= _classes.Skills.Count || gem >= _loot.Materials.Count) continue;
                var key = _classes.Skills[skill];
                if (!_supports.TryGetValue(key, out var gems)) _supports[key] = gems = new List<MaterialDefinition>();
                gems.Add(_loot.Materials[gem]);
            }
            _storySteps.Clear();
            foreach (var (npc, step) in message.Stories)
            {
                if (Npcs != null && npc < Npcs.Npcs.Count) _storySteps[Npcs.Npcs[npc].Id] = step;
            }
            RefreshStats();
            InventoryVersion++;
        }

        public List<SimEvent> TakeEvents()
        {
            var events = new List<SimEvent>(_events);
            _events.Clear();
            return events;
        }

        void RefreshStats()
        {
            var buffs = Enumerable.Range(0, Math.Min(Skills.Count, _buffs.Length)).Where(slot => _buffs[slot] > 0).Select(slot => Skills[slot]);
            Stats = PlayerStats.From(Equipment, Classes, buffs, Food, Level, Passives, Pet?.Modifiers);
        }

        // The passive tree points taken, as the host last said (World.Passives.cs), and the tree itself.
        public IReadOnlyList<PassiveNode> Passives { get; private set; } = new List<PassiveNode>();
        public IReadOnlyList<PassiveNode> PassiveTree => _classes?.Passives ?? (IReadOnlyList<PassiveNode>)Array.Empty<PassiveNode>();
        public int PassivePoints => Math.Max(0, Level - 1 - Passives.Count);

        // Whether the point could be taken now: next to the start or a point taken, and a point to spend.
        public bool CanAllocate(PassiveNode node) =>
            node != null && !node.IsStart && PassivePoints > 0 && !Passives.Contains(node) && node.Links.Any(n => n.IsStart || Passives.Contains(n));

        public byte[] AllocatePassiveCommand(PassiveNode node) =>
            new InventoryCommandMessage(InventoryCommand.AllocatePassive, 0, index: ItemCodec.Index(_classes.Passives, node)).Encode();

        public byte[] ResetPassivesCommand() => new InventoryCommandMessage(InventoryCommand.ResetPassives, 0).Encode();

        List<ClassDefinition> ClassesAt(IReadOnlyList<int> indexes)
        {
            if (indexes.Count == 0) return new List<ClassDefinition>();
            if (_classes == null || indexes.Any(i => i >= _classes.Classes.Count)) throw new InvalidDataException("The host names a class this client does not know.");
            return indexes.Select(i => _classes.Classes[i]).ToList();
        }

        void ApplyTileChanges(TileChangesMessage message)
        {
            foreach (var change in message.Changes)
            {
                if (!Map.InBounds(change.X, change.Y)) throw new InvalidDataException("Tile change outside the map.");
                Map[change.X, change.Y] = change.Type;
                _changedTiles.Add(change);
            }
        }

        void ApplyCropChanges(CropChangesMessage message)
        {
            foreach (var (x, y, crop, grown, wetFrom, wetUntil) in message.Changes)
            {
                if (!Map.InBounds(x, y)) throw new InvalidDataException("Crop outside the map.");
                int key = y * Map.Width + x;
                if (crop < 0)
                {
                    _crops.Remove(key);
                    continue;
                }
                if (_tiles == null || crop >= _tiles.Crops.Count) throw new InvalidDataException("The host names a crop this client does not know.");
                _crops[key] = new Crop(_tiles.Crops[crop], x, y, grown, wetFrom, wetUntil);
            }
        }

        void Welcome(WelcomeMessage welcome)
        {
            ApplyKit(welcome.Classes, welcome.SkillOrder);
            PlayerId = welcome.PlayerId;
            Seed = welcome.Seed;
            var generated = WorldGenerator.Generate(welcome.Seed, welcome.Width, welcome.Height, Story);
            Map = generated.Map;
            Places = generated.Places;
            Arena = generated.Arena;
            Arenas = generated.Arenas;
            Biomes = generated.Biomes;
            ArenaRadius = generated.ArenaRadius;
            Position = welcome.Spawn;
            PreviousPosition = welcome.Spawn;
            Spawn = welcome.Spawn;
            Status = ClientStatus.Playing;
        }

        void Apply(SnapshotMessage snapshot)
        {
            if (_snapshots.Count > 0 && snapshot.Tick <= _snapshots[_snapshots.Count - 1].Tick) return;
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Monster && e.Type >= _monsters.Monsters.Count))
                throw new InvalidDataException("Snapshot names a monster this client does not know.");
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Drop
                && (_loot == null || e.Type >= (e.State == DropState.Rune ? _loot.Runes.Count
                    : e.State == DropState.Consumable ? _loot.Consumables.Count
                    : e.State == DropState.Material ? _loot.Materials.Count
                    : _loot.Bases.Count))))
                throw new InvalidDataException("Snapshot names loot this client does not know.");
            if (snapshot.Entities.Any(e => e.IsPlayer && e.Type > 0 && (_loot == null || e.Type > _loot.Bases.Count)))
                throw new InvalidDataException("Snapshot names a weapon this client does not know.");
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Minion && (_classes == null || e.Type >= _classes.Skills.Count)))
                throw new InvalidDataException("Snapshot names a skill this client does not know.");
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Boss && (_bosses == null || e.Type >= _bosses.Bosses.Count)))
                throw new InvalidDataException("Snapshot names a boss this client does not know.");
            // A snapshot sent before a change of classes reached here: the next one will match.
            if (snapshot.Self.Cooldowns.Count != Skills.Count) return;

            _snapshots.Add(snapshot);
            _sinceLatest = 0;
            if (_snapshots.Count > KeptSnapshots) _snapshots.RemoveAt(0);
            _events.AddRange(snapshot.Events);
            foreach (var e in snapshot.Events)
            {
                if (e.Type == SimEventType.SkillUsed && e.Source != PlayerId && SkillNamed(e.Target) is SkillDefinition used)
                {
                    if (!_seenSlots.TryGetValue(e.Source, out var slots)) _seenSlots[e.Source] = slots = new Dictionary<int, SkillDefinition>();
                    slots[e.Amount] = used;
                }
                if (e.Type == SimEventType.Fishing && e.Source == PlayerId)
                {
                    int kind = World.FishingKind(e.Amount);
                    Fishing = kind == World.FishingCast || kind == World.FishingBite ? kind : -1;
                    if (kind == World.FishingBite)
                    {
                        FishingDifficulty = World.FishingDifficultyOf(e.Amount);
                        BiteTick = HostTick;
                    }
                }
                if (e.Type == SimEventType.GoalReward && e.Source == PlayerId && Guide != null && e.Amount >= 0 && e.Amount < Guide.Goals.Count)
                {
                    ClaimedGoals.Add(Guide.Goals[e.Amount].Id);
                    InventoryVersion++;
                }
            }

            var self = snapshot.Self;
            bool buffsChanged = false;
            for (int slot = 0; slot < Skills.Count; slot++)
            {
                buffsChanged |= _buffs[slot] > 0 != self.Buffs[slot] > 0;
                _cooldowns[slot] = self.Cooldowns[slot];
                _buffs[slot] = self.Buffs[slot];
            }
            _consumableCooldown = self.ConsumableCooldown;
            var food = self.Food >= 0 && _loot != null && self.Food < _loot.Consumables.Count ? _loot.Consumables[self.Food] : null;
            buffsChanged |= food != Food;
            Food = food;
            _foodTicks = self.FoodTicks;
            _skillsTick = snapshot.Tick;
            buffsChanged |= Leveling.LevelFor(self.Experience) != Level;
            var me = snapshot.Entities.FirstOrDefault(e => e.Id == PlayerId);
            var pet = me.Id == PlayerId ? PlayerPhase.PetOf(me.Phase) : Pet;
            buffsChanged |= pet != Pet;
            Pet = pet;
            Experience = self.Experience;
            if (buffsChanged) RefreshStats();
            Health = self.Health;
            MaxHealth = self.MaxHealth;
            Mana = self.Mana;
            MaxMana = self.MaxMana;
            MoveSpeed = self.MoveSpeed;
            SelfStatus = self.Status;
            Bed = self.BedX >= 0 && self.BedY >= 0 ? (self.BedX, self.BedY) : ((int, int)?)null;
            Crafting = self.CraftRecipe >= 0 && _loot != null && self.CraftRecipe < _loot.Recipes.Count ? _loot.Recipes[self.CraftRecipe] : null;
            CraftProgress = self.CraftProgress;
            CraftLeft = self.CraftLeft;
            _clock = snapshot.Clock;
            _clockTick = snapshot.Tick;
            Sleepers = snapshot.Sleepers;
            Rift = snapshot.Rift;
            Defense = snapshot.Defense;
            if (self.Corrections != _seenCorrections)
            {
                _seenCorrections = self.Corrections;
                Position = self.Position;
                PreviousPosition = self.Position;
            }
        }
    }
}
