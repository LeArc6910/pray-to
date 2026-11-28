using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Core
{
    public sealed class HostConfig
    {
        public int MaxPlayers { get; set; } = 4;
        // How many classes one player may mix. See Docs/Classes.md before raising it.
        public int MaxClasses { get; set; } = ClassRules.DefaultMaxClasses;
        // Ticks between snapshots. Clients interpolate between them.
        public int SnapshotInterval { get; set; } = 2;
        // Entities further than this from a player are left out of that player's snapshots.
        public float ViewRange { get; set; } = 22;
        // How many tiles across the generated map is (WorldGenerator.DefaultSize for tests and sims; the game asks for
        // WorldGenerator.GameSize, or a saved world's own size).
        public int MapSize { get; set; } = WorldGenerator.DefaultSize;
        // Which generator makes the map (WorldGenerator.CaveVersion for tests and sims; the game asks for
        // WorldGenerator.NewWorldVersion, or a saved world's own).
        public int MapVersion { get; set; } = WorldGenerator.CaveVersion;
        // These keep a snapshot inside one network packet (Netcode's limit is about 1250 bytes). With 43 bytes for
        // the header and the player's own state, 17 an entity or drop, 14 a projectile, 19 a hazard and 13 an event,
        // the maximum with 20 entities comes to about 1.19 KB. Entities are 32 since packs (two packs and four players,
        // 2026-11-22, Docs/WorldRework.md 상한); a snapshot that would come past MaxSnapshotBytes leaves out the
        // furthest monsters (WorldHost.SnapshotBytes).
        public int MaxEntitiesPerSnapshot { get; set; } = 32;
        public int MaxSnapshotBytes { get; set; } = 1200;
        public int MaxDropsPerSnapshot { get; set; } = 8;
        public int MaxProjectilesPerSnapshot { get; set; } = 20;
        public int MaxHazardsPerSnapshot { get; set; } = 16;
        public int MaxEventsPerSnapshot { get; set; } = 14;
        public string[] StarterGear { get; set; } = { "copper_sword" };
        // Put in the bag of a new character, and of a saved one that has no pickaxe (from before rock needed one).
        public string StarterPickaxe { get; set; } = "copper_pickaxe";
        // Machines nearer than ViewRange go out every MachineInterval ticks, at most this many, nearest first.
        public int MachineInterval { get; set; } = 4;
        public int MaxMachinesPerMessage { get; set; } = 60;
        // One entry per consumable handed out on joining. Ids the sheet does not have are skipped.
        public string[] StarterConsumables { get; set; } = { "healing_potion", "healing_potion", "healing_potion" };
        // One entry per material handed out on joining, likewise.
        public string[] StarterMaterials { get; set; } = { "mushroom_spore", "mushroom_spore", "torch", "torch", "torch", "torch", "torch", "torch" };
    }

    public readonly struct Outgoing
    {
        public Outgoing(ulong connection, byte[] data, bool reliable)
        {
            Connection = connection;
            Data = data;
            Reliable = reliable;
        }

        public ulong Connection { get; }
        public byte[] Data { get; }
        // Join replies and the roster must arrive; snapshots are replaced by the next one anyway.
        public bool Reliable { get; }
    }

    // Runs the world for everyone. The transport hands it whatever arrives with Receive and Disconnect, calls Tick
    // TicksPerSecond times a second, and sends whatever TakeOutgoing returns. A solo game is a host whose only
    // connection is the local player.
    public sealed class WorldHost
    {
        sealed class Connection
        {
            public Player Player;
            public int LastSequence;
            // The roll the host last took from this player (InputMessage.RollStart), or 0.
            public int RollStart;
            // The player's InventoryVersion when their inventory was last sent.
            public int SentInventory = -1;
            // The chest whose contents were last sent (its tile index, -1 for none) and that chest's version then.
            public int SentChest = -1;
            public int SentChestVersion = -1;
            // Whether the last machines message had any, so an empty one is sent once when they go out of view.
            // Each machine in view as last sent and when (2026-11-09: only what changed goes, and each again now and then).
            public readonly Dictionary<int, (MachineState State, long Tick)> SentMachineStates = new Dictionary<int, (MachineState State, long Tick)>();
            // The last stock message sent, to send again only when it changes.
            public byte[] SentStock;
            // The last production message sent, likewise.
            public byte[] SentProduction;
            // The last friendships message sent, likewise.
            public byte[] SentFriendships;
            // The player's LifeVersion when their life skills were last sent.
            public int SentLife = -1;
            // The player's KitVersion when their classes and slots were last sent (a learned skill fills a slot).
            public int SentKit = -1;
            // The player's RecordsVersion when their bestiary was last sent.
            public int SentRecords = -1;
            // The world's LockVersion when the locked doors were last sent.
            public int SentLocks = -1;
            // The world's PoiVersion when the things to find were last sent (PoisMessage).
            public int SentPois = -1;
            // The world's DungeonVersion when the dungeons standing were last sent (DungeonsMessage); 0 sends none to one
            // who joins before any was made.
            public int SentDungeons;
            // The player's JourneyVersion when their place in the story was last sent.
            public int SentJourney = -1;
        }

        // How many times over one craft command may make its recipe.
        public const int MaxCraftAtOnce = 10;

        readonly Dictionary<ulong, Connection> _connections = new Dictionary<ulong, Connection>();
        readonly List<Outgoing> _outgoing = new List<Outgoing>();
        readonly List<SimEvent> _pendingEvents = new List<SimEvent>();
        readonly MonsterData _monsters;
        readonly BossData _bosses;
        readonly LootData _loot;
        // The guide's goals, for their rewards; null gives none.
        readonly GuideData _guide;
        readonly ClassData _classes;
        readonly TileData _tiles;
        readonly HostConfig _config;
        readonly GameRandom _gearRandom;
        readonly ulong _seed;
        readonly GeneratedWorld _generated;
        // The world as the seed made it: its arenas and what each is dressed as.
        public GeneratedWorld Generated => _generated;

        // The first boss in the sheet waits in the arena; without boss data the caves have none. Without class data
        // everyone plays classless. generated: the world already made from this seed, size and version (WorldGenerator.Steps,
        // so the loading screen moves on as it is made, Docs/TitleRework.md 4-4); without it the host makes it here.
        public WorldHost(
            ulong seed,
            MonsterData monsters,
            LootData loot,
            HostConfig config = null,
            WorldConfig worldConfig = null,
            BossData bosses = null,
            ClassData classes = null,
            TileData tiles = null,
            GuideData guide = null,
            NpcData npcs = null,
            StoryData story = null,
            GeneratedWorld generated = null)
        {
            if (generated != null && (generated.Map.Width != (config ?? new HostConfig()).MapSize || generated.Map.Height != (config ?? new HostConfig()).MapSize))
                throw new ArgumentException("The world made is not of the map size asked for.", nameof(generated));
            _npcs = npcs;
            _guide = guide;
            _seed = seed;
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _bosses = bosses;
            _classes = classes;
            _tiles = tiles;
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _config = config ?? new HostConfig();
            _gearRandom = new GameRandom(seed, stream: 3);
            _generated = generated ?? WorldGenerator.Generate(seed, _config.MapSize, _config.MapSize, story, bosses?.Arenas, _config.MapVersion, tiles?.Terrain);
            // The map as made, before anything is dug or built: what a joiner's own should come out as (Docs/CrossPlay.md).
            MapPrintMade = MapPrint.Of(_generated.Map, _generated.Biomes);
            World = new World(_generated.Map, _generated.Spawn, monsters, seed, worldConfig, loot, tiles, _generated.Biomes);
            // Each boss in its own arena, in sheet order: the first near the spawn, the next in the deep zone.
            // A boss with a place (bosses.csv place, 2026-11-10) waits in the arena beside it, when the world has it.
            var caveBosses = bosses?.Bosses.Where(b => b.Place == null).ToList() ?? new List<BossDefinition>();
            for (int i = 0; i < caveBosses.Count && i < _generated.CaveArenas; i++)
            {
                World.PlaceBoss(caveBosses[i], _generated.Arenas[i], _generated.ArenaRadius);
            }
            foreach (var boss in bosses?.Bosses.Where(b => b.Place != null) ?? Enumerable.Empty<BossDefinition>())
            {
                if (_generated.PlaceArenas.TryGetValue(boss.Place, out var arena)) World.PlaceBoss(boss, arena, _generated.ArenaRadius);
            }
            // The places first: some people keep shop in them.
            if (story != null) World.SetStory(story, _generated.Places);
            if (npcs != null) World.SetNpcs(npcs);
            // A world of rooms keeps its monsters in packs in its pack rooms (World.Packs.cs).
            World.SetRooms(_generated.Rooms);
        }

        readonly NpcData _npcs;

        public World World { get; }
        public ulong MapPrintMade { get; }
        // How many joiners asked for the map (their own came out otherwise).
        public int MapsSent { get; private set; }
        public int PlayerCount => _connections.Values.Count(c => c.Player != null);

        public void Receive(ulong connection, byte[] data)
        {
            try
            {
                switch (Protocol.TypeOf(data))
                {
                    case MessageType.Join:
                        Join(connection, JoinMessage.Decode(data));
                        break;
                    // A joiner's map came out other than ours: the map as it is now, in parts (Docs/CrossPlay.md).
                    case MessageType.MapRequest:
                        if (_connections.TryGetValue(connection, out var asking) && asking.Player != null)
                        {
                            MapsSent++;
                            foreach (var part in MapDataMessage.Split(MapPrint.Pack(World.Map, _generated.Biomes))) Send(connection, part, reliable: true);
                        }
                        break;
                    case MessageType.Input:
                        if (_connections.TryGetValue(connection, out var sender)) ApplyInput(sender, InputMessage.Decode(data));
                        break;
                    case MessageType.InventoryCommand:
                        if (_connections.TryGetValue(connection, out var owner) && owner.Player != null) Apply(owner.Player, InventoryCommandMessage.Decode(data));
                        break;
                    case MessageType.ClassCommand:
                        if (_connections.TryGetValue(connection, out var learner) && learner.Player != null) ChangeKit(connection, learner.Player, data);
                        break;
                    case MessageType.SkillCommand:
                        if (_connections.TryGetValue(connection, out var caster) && caster.Player != null)
                        {
                            var command = SkillCommandMessage.Decode(data);
                            if (command.Slot == SkillCommandMessage.ArtPress) World.UseWeaponArt(caster.Player.Id, command.Aim);
                            else if (command.Slot == SkillCommandMessage.ArtRelease) World.ReleaseWeaponArt(caster.Player.Id, command.Aim);
                            else World.UseSkill(caster.Player.Id, command.Slot, command.Aim);
                        }
                        break;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                // A truncated or garbled message from one client must not take the host down; drop it.
            }
        }

        public void Disconnect(ulong connection)
        {
            if (!_connections.Remove(connection, out var gone) || gone.Player == null) return;
            World.RemovePlayer(gone.Player.Id);
            BroadcastRoster();
        }

        public void Tick()
        {
            World.Step();
            NoteVisits();
            _pendingEvents.AddRange(World.TakeEvents());

            // Dug and built tiles go out before the snapshot, so nobody walks into a wall that is not there.
            var tileChanges = World.TakeTileChanges();
            var cropChanges = World.TakeCropChanges().Select(CropEntry).ToList();
            foreach (byte[] message in TileChangesMessage.EncodeAll(tileChanges).Concat(CropChangesMessage.EncodeAll(cropChanges)))
            {
                foreach (var pair in _connections.Where(pair => pair.Value.Player != null)) Send(pair.Key, message, reliable: true);
            }

            // The dungeons standing (World.Dungeon.cs), before the snapshot that brings a player in, so their client has laid it.
            foreach (var pair in _connections)
            {
                if (pair.Value.Player == null || pair.Value.SentDungeons == World.DungeonVersion) continue;
                pair.Value.SentDungeons = World.DungeonVersion;
                Send(pair.Key, DungeonsNow().Encode(), reliable: true);
            }

            // Where each player is in the story.
            foreach (var pair in _connections)
            {
                var player = pair.Value.Player;
                if (player == null || pair.Value.SentJourney == player.JourneyVersion) continue;
                pair.Value.SentJourney = player.JourneyVersion;
                var open = World.Story?.Places.Where(p => player.Waypoints.Contains(p.Id)).Select(p => p.Index).ToList() ?? new List<int>();
                var answers = player.StoryChoices.Select(c => (c.Key, c.Value)).ToList();
                var sides = player.SideSteps.Select(side => (side.Key, side.Value, player.SideTaken.Contains(side.Key)))
                    .Concat(player.SideTaken.Where(chain => !player.SideSteps.ContainsKey(chain)).Select(chain => (chain, World.Story?.Chain(chain)?.First.Index ?? 0, true)))
                    .ToList();
                Send(pair.Key, new JourneyMessage(player.JourneyStep, player.JourneyTaken, open, answers, sides).Encode(), reliable: true);
            }
            // Locked doors before the snapshot too, so an owner's client lets them through as soon as the host does.
            foreach (var pair in _connections)
            {
                var player = pair.Value.Player;
                if (player == null || pair.Value.SentLocks == World.LockVersion) continue;
                pair.Value.SentLocks = World.LockVersion;
                var locks = World.Locks.Select(l => (l.X, l.Y, l.Owner, l.Owner == player.Name)).ToList();
                Send(pair.Key, new LocksMessage(locks).Encode(), reliable: true);
            }
            // The things to find (World.Pois.cs): what was opened, and this player's shrines, camp, blessing and rest.
            foreach (var pair in _connections)
            {
                var player = pair.Value.Player;
                if (player == null || World.Pois.Count == 0 || pair.Value.SentPois == World.PoiVersion) continue;
                pair.Value.SentPois = World.PoiVersion;
                Send(pair.Key, PoisFor(player).Encode(), reliable: true);
            }
            foreach (var pair in _connections)
            {
                var player = pair.Value.Player;
                if (player == null || pair.Value.SentKit == player.KitVersion) continue;
                pair.Value.SentKit = player.KitVersion;
                Send(pair.Key, new KitMessage(MessageType.Kit, ClassIndexes(player), player.SkillOrder).Encode(), reliable: true);
            }
            foreach (var pair in _connections)
            {
                var player = pair.Value.Player;
                if (player == null || pair.Value.SentInventory == player.InventoryVersion) continue;
                pair.Value.SentInventory = player.InventoryVersion;
                var passives = player.Passives.Select(p => IndexOf(_classes?.Passives, p)).Where(i => i >= 0).ToList();
                var skills = player.SkillRanks.Select(p => (p.Key.Index, p.Value)).OrderBy(p => p.Index).ToList();
                var stories = player.StorySteps.Select(p => (World.Npcs?.IndexOf(World.Npcs.Find(p.Key)) ?? -1, p.Value)).Where(p => p.Item1 >= 0).ToList();
                var supports = player.Supports.SelectMany(p => p.Value.Select(gem => (p.Key.Index, IndexOf(_loot.Materials, gem)))).Where(p => p.Item2 >= 0).ToList();
                Send(pair.Key, new InventoryMessage(player.Equipment, player.Inventory, passives, skills, stories, supports).Encode(_loot), reliable: true);
            }
            foreach (var pair in _connections.Where(pair => pair.Value.Player != null)) SendChest(pair.Key, pair.Value);
            if (World.Tick % _config.MachineInterval == 0)
            {
                foreach (var pair in _connections.Where(pair => pair.Value.Player != null))
                {
                    SendMachines(pair.Key, pair.Value);
                    SendStock(pair.Key, pair.Value);
                    SendFriendships(pair.Key, pair.Value);
                    if (pair.Value.SentLife != pair.Value.Player.LifeVersion)
                    {
                        pair.Value.SentLife = pair.Value.Player.LifeVersion;
                        Send(pair.Key, new LifeMessage(pair.Value.Player.LifeExperience, pair.Value.Player.LifePerks).Encode(), reliable: true);
                    }
                    if (pair.Value.SentRecords != pair.Value.Player.RecordsVersion)
                    {
                        pair.Value.SentRecords = pair.Value.Player.RecordsVersion;
                        Send(pair.Key, RecordsFor(pair.Value.Player).Encode(), reliable: true);
                    }
                }
            }

            if (World.Tick % ProductionInterval == 0) SendProduction();

            if (World.Tick % _config.SnapshotInterval != 0) return;

            foreach (var pair in _connections)
            {
                if (pair.Value.Player != null) Send(pair.Key, SnapshotBytes(pair.Value.Player), reliable: false);
            }
            _pendingEvents.Clear();
        }

        public List<Outgoing> TakeOutgoing()
        {
            var outgoing = new List<Outgoing>(_outgoing);
            _outgoing.Clear();
            return outgoing;
        }

        void Join(ulong connection, JoinMessage join)
        {
            if (_connections.TryGetValue(connection, out var existing) && existing.Player != null) return;
            if (join.Version != Protocol.Version)
            {
                Send(connection, new RefusedMessage(Protocol.VersionRefusal(join.Version)).Encode(), reliable: true);
                return;
            }
            if (PlayerCount >= _config.MaxPlayers)
            {
                Send(connection, new RefusedMessage($"자리가 없습니다 (최대 {_config.MaxPlayers}명).").Encode(), reliable: true);
                return;
            }

            // A saved character brings its own gear and bag; a new one gets the starter kit.
            CharacterSave saved = null;
            if (join.Character.Length > 0)
            {
                try
                {
                    saved = CharacterSave.Read(join.Character, _loot);
                }
                catch (InvalidDataException e)
                {
                    Send(connection, new RefusedMessage(e.Message).Encode(), reliable: true);
                    return;
                }
            }
            string name = string.IsNullOrWhiteSpace(join.Name) ? "플레이어" : join.Name.Trim();
            // A new character picks its classes in the game (Docs/Classes.md, 클래스 고르기): at most the first slot's
            // worth from the menu, which no longer asks; a saved one comes back with the classes it had.
            var classes = ClassRules.Choose(_classes, join.Classes, saved == null ? Math.Min(_config.MaxClasses, ClassRules.SlotsAt(1)) : _config.MaxClasses);
            var player = World.AddPlayer(name.Length > 16 ? name.Substring(0, 16) : name, saved?.Equipment ?? StarterGear(), saved?.Bag, classes.ToArray());
            if (saved == null) GiveStarterStacks(player);
            // A character from before levels starts at a level that lets it keep wearing what it has.
            player.Experience = saved == null ? 0 : saved.Experience >= 0 ? saved.Experience : Leveling.TotalFor(Leveling.StartFor(saved.Equipment, saved.Bag));
            foreach (string goal in saved?.ClaimedGoals ?? new List<string>()) player.ClaimedGoals.Add(goal);
            if (saved != null) World.RestorePassives(player, _classes, saved.Passives);
            if (saved != null) World.RestoreFriendships(player, saved.Friendships);
            if (saved != null) World.RestoreLifeExperience(player, saved.LifeExperience);
            if (saved != null) World.RestoreLifePerks(player, saved.LifePerks);
            if (saved != null) World.RestoreSkills(player, _classes, saved.SkillRanks);
            if (saved != null) World.RestoreStories(player, saved.Stories);
            if (saved != null) World.RestoreSupports(player, _classes, _loot, saved.Supports);
            if (saved != null) World.RestoreRecords(player, saved.Records, saved.Achievements);
            if (saved != null) World.RestorePet(player, saved.Pet, saved.PetExperience);
            if (saved != null) World.RestoreJourney(player, saved.JourneyStep, saved.JourneyTaken, saved.Waypoints, saved.StoryChoices, saved.SideStories);
            player.RefreshStats();
            GivePickaxe(player);
            // The skills in the order the player keeps them in.
            World.ChangeClasses(player.Id, player.Classes, join.SkillOrder);
            // Someone coming back to a world where they have a bed starts beside it (the client snaps there).
            World.SendHome(player);
            player.Look = CharacterLook.Unpack(join.Look).Packed;
            _connections[connection] = new Connection { Player = player, SentKit = player.KitVersion };

            var map = _generated.Map;
            Send(connection, new WelcomeMessage(player.Id, _seed, map.Width, map.Height, World.Spawn, ClassIndexes(player), player.SkillOrder, _generated.Version, MapPrintMade).Encode(),
                reliable: true);
            foreach (byte[] message in TileChangesMessage.EncodeAll(World.ChangedTiles())) Send(connection, message, reliable: true);
            var crops = World.Crops.Select(c => CropEntry(new CropChange(c.X, c.Y, c))).ToList();
            foreach (byte[] message in CropChangesMessage.EncodeAll(crops)) Send(connection, message, reliable: true);
            BroadcastRoster();
        }

        // The player's records by sheet index; records whose sheet row is gone stay in the save but are not sent.
        RecordsMessage RecordsFor(Player player)
        {
            var records = new List<(RecordKind, int, int)>();
            foreach (var pair in player.Records)
            {
                int index = pair.Key.Kind switch
                {
                    RecordKind.Kill => IndexOf(_monsters.Monsters, _monsters.Monsters.FirstOrDefault(m => m.Id == pair.Key.Id)),
                    RecordKind.Boss => IndexOf(_bosses?.Bosses, _bosses?.Bosses.FirstOrDefault(b => b.Id == pair.Key.Id)),
                    RecordKind.Found => IndexOf(_loot?.Materials, _loot?.Materials.FirstOrDefault(m => m.Id == pair.Key.Id)),
                    RecordKind.Dungeon => World.Tiles?.Terrain?.Dungeons?.IndexOfKey(pair.Key.Id) ?? -1,
                    _ => IndexOf(_loot?.Bases, _loot?.Bases.FirstOrDefault(b => b.Id == pair.Key.Id)),
                };
                if (index >= 0) records.Add((pair.Key.Kind, index, pair.Value));
            }
            var had = Enumerable.Range(0, Records.Achievements.Count).Where(i => player.Achievements.Contains(Records.Achievements[i].Id)).ToList();
            var pets = player.PetExperience.Select(pair => (Pets.IndexOf(Pets.Find(pair.Key)), pair.Value)).Where(p => p.Item1 > 0).ToList();
            return new RecordsMessage(records, had, pets);
        }

        Equipment StarterGear()
        {
            var equipment = new Equipment();
            foreach (string id in _config.StarterGear)
            {
                var itemBase = _loot.FindBase(id);
                equipment.Equip(new LootGenerator(_loot).RollItem(itemBase, itemBase.Level, Rarity.Common, _gearRandom));
            }
            return equipment;
        }

        // Rock only gives way to a pickaxe, so nobody may be without one. A full bag gets it at the player's feet.
        void GivePickaxe(Player player)
        {
            var pickaxe = _loot?.Bases.FirstOrDefault(b => b.Id == _config.StarterPickaxe);
            if (pickaxe == null || player.Equipment.Items.Concat(player.Inventory.Items).Any(item => item.Base.IsPickaxe)) return;
            var item = new LootGenerator(_loot).RollItem(pickaxe, pickaxe.Level, Rarity.Common, _gearRandom);
            World.Give(player, item);
        }

        void GiveStarterStacks(Player player)
        {
            foreach (string id in _config.StarterConsumables)
            {
                var consumable = _loot.Consumables.FirstOrDefault(c => c.Id == id);
                if (consumable != null) player.Inventory.AddConsumable(consumable);
            }
            foreach (string id in _config.StarterMaterials)
            {
                var material = _loot.Materials.FirstOrDefault(m => m.Id == id);
                if (material != null) player.Inventory.AddMaterial(material);
            }
        }

        PoisMessage PoisFor(Player player)
        {
            var opened = Enumerable.Range(0, World.Pois.Count).Select(World.Opened).ToList();
            var rows = _tiles?.Terrain?.Pois?.Pois;
            int blessing = player.Blessing == null || rows == null ? -1 : rows.ToList().IndexOf(player.Blessing);
            return new PoisMessage(opened, World.PrayedToday(player.Name).ToList(), World.LastCampOf(player.Name) ?? -1, blessing,
                blessing >= 0 ? player.BlessingTicks : 0, player.RestedTicks, World.OpenStones(player).ToList(), World.FixedRails.ToList(), World.Riders.ToList(),
                World.HeldTraps.ToList());
        }

        DungeonsMessage DungeonsNow() => new DungeonsMessage(World.Dungeons.Select(DungeonStatus.Of).ToList());

        // The world as a save file, stamped with the caller's clock (seconds since 1970): saved now, made at CreatedAt (now,
        // for a world first saved), played for PlayedSeconds, its players having stood in Visited.
        public byte[] SaveWorld(long now)
        {
            if (CreatedAt == 0) CreatedAt = now;
            return WorldSave.Write(World, _seed, now, _generated.Version, CreatedAt, PlayedSeconds, Visited);
        }

        // When the world was made, in seconds since 1970 as the caller counted them (0 until first saved or restored),
        // and the seconds it has been played while hosted (the caller adds them, NotePlayed: Core reads no clock).
        public long CreatedAt { get; private set; }
        public double PlayedSeconds { get; private set; }

        public void NotePlayed(double seconds)
        {
            if (seconds > 0 && !double.IsInfinity(seconds)) PlayedSeconds += seconds;
        }

        // The biomes any player has stood in here (BiomeVisits), outside the dungeons' pocket; kept in the save for the
        // world list's deepest biome.
        public int Visited { get; private set; }

        void NoteVisits()
        {
            foreach (var player in World.Players)
            {
                if (World.Map.InPocket(player.Position)) continue;
                Visited = BiomeVisits.Mark(Visited, _generated.Biomes.At(player.Position));
            }
        }

        // Puts a world save back into this freshly made host, which must have been made with the save's seed. The world
        // carries on where it was: while it was closed no time passed, for the clock or the crops (Docs/Time.md).
        public void Restore(WorldSave save)
        {
            if (save.Seed != _seed) throw new ArgumentException("The save is of a world with another seed.", nameof(save));
            CreatedAt = save.CreatedAt;
            PlayedSeconds = save.PlayedSeconds;
            Visited |= save.Visited;
            World.Restore(save.Tiles, save.Crops.Select(c => (c.Crop, c.X, c.Y, c.Grown, c.WetLeft)), save.ProgressLevel, save.Chests, save.Beds, save.Locks);
            World.RestoreClock(save.Clock);
            World.RestoreRiftTier(save.RiftTier);
            World.RestoreDefense(save.DefenseLevel, save.DefenseDay);
            foreach (var m in save.Machines) World.RestoreMachine(m.X, m.Y, m.Direction, m.Timer, m.Carried, m.Progress, m.Input, m.Output, m.Settings, m.Recipe,
                m.Affection, m.PettedDay);
            World.PlaceNpcs();
            World.RestorePois(save.OpenedPois, save.Prayers, save.LastCamps);
            World.RestoreRails(save.FixedRails);
            World.RestoreDungeons(save.DungeonRecords, save.DungeonEntries);
        }

        void Apply(Player player, InventoryCommandMessage command)
        {
            switch (command.Command)
            {
                case InventoryCommand.Equip:
                    World.EquipItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Unequip:
                    World.UnequipItem(player.Id, command.Slot);
                    break;
                case InventoryCommand.EquipSpare:
                    World.EquipSpare(player.Id, command.Item);
                    break;
                case InventoryCommand.UnequipSpare:
                    World.UnequipSpare(player.Id);
                    break;
                case InventoryCommand.SwapWeapons:
                    World.SwapWeapons(player.Id);
                    break;
                case InventoryCommand.InsertRune:
                    if (command.Index < _loot.Runes.Count) World.InsertRune(player.Id, command.Item, _loot.Runes[command.Index]);
                    break;
                case InventoryCommand.UseConsumable:
                    if (command.Index < _loot.Consumables.Count) World.UseConsumable(player.Id, _loot.Consumables[command.Index]);
                    break;
                case InventoryCommand.Craft:
                    // Item says how many times over (0 for once, as before), up to MaxCraftAtOnce.
                    // Things that take time are made one after another while the player stays (World.StartCrafting).
                    if (command.Index < _loot.Recipes.Count) World.StartCrafting(player.Id, _loot.Recipes[command.Index], Math.Clamp(command.Item, 1, MaxCraftAtOnce));
                    break;
                case InventoryCommand.StopCraft:
                    World.StopCrafting(player.Id);
                    break;
                case InventoryCommand.Place:
                    if (command.Index < _loot.Materials.Count) World.PlaceTile(player.Id, _loot.Materials[command.Index]);
                    break;
                case InventoryCommand.Plant:
                    if (_tiles != null && command.Index < _tiles.Crops.Count) World.Plant(player.Id, _tiles.Crops[command.Index]);
                    break;
                case InventoryCommand.Water:
                    World.Water(player.Id);
                    break;
                case InventoryCommand.ToggleMachine:
                    World.ToggleMachine(player.Id);
                    break;
                case InventoryCommand.EnterDungeon:
                    World.EnterDungeon(player.Id, command.Item);
                    break;
                case InventoryCommand.LeaveDungeon:
                    World.LeaveDungeon(player.Id, byMenu: command.Item != 0);
                    break;
                case InventoryCommand.UseDungeonThing:
                    World.UseDungeonThing(player.Id, command.Item);
                    break;
                case InventoryCommand.UsePoi:
                    World.UsePoi(player.Id, command.Item);
                    break;
                case InventoryCommand.Fish:
                    World.Fish(player.Id, command.Item > 0 ? command.Item - 1 : (int?)null);
                    break;
                case InventoryCommand.SetRecipe:
                    // Index is the recipe's sheet index; Item 0 stops the assembler.
                    World.SetRecipe(player.Id, command.Item == 0 || command.Index >= _loot.Recipes.Count ? null : _loot.Recipes[command.Index]);
                    break;
                case InventoryCommand.SetFilter:
                    // Index is the material's sheet index; Item 0 clears the filter.
                    World.SetFilter(player.Id, command.Item == 0 || command.Index >= _loot.Materials.Count ? null : _loot.Materials[command.Index]);
                    break;
                case InventoryCommand.Refuel:
                    if (command.Index < _loot.Materials.Count) World.Refuel(player.Id, _loot.Materials[command.Index]);
                    break;
                case InventoryCommand.Stash:
                    if (command.Index < _loot.Materials.Count) World.StashMaterial(player.Id, _loot.Materials[command.Index]);
                    break;
                case InventoryCommand.Repair:
                    World.Repair(player.Id);
                    break;
                case InventoryCommand.ChoosePerk:
                    // Index: LifeSkills.PerkIndex; item: the choice (1 or 2).
                    World.ChoosePerk(player.Id, (LifeSkill)(command.Index / LifeSkills.PerkTiers), command.Index % LifeSkills.PerkTiers, command.Item);
                    break;
                case InventoryCommand.ClaimGoal:
                    if (_guide != null && command.Index < _guide.Goals.Count) World.ClaimGoal(player.Id, _guide.Goals[command.Index], command.Index);
                    break;
                case InventoryCommand.Till:
                    World.Till(player.Id);
                    break;
                case InventoryCommand.Fertilize:
                    World.Fertilize(player.Id);
                    break;
                case InventoryCommand.Sleep:
                    World.Sleep(player.Id);
                    break;
                case InventoryCommand.Wake:
                    World.Wake(player.Id);
                    break;
                case InventoryCommand.AllocatePassive:
                    if (_classes != null && command.Index < _classes.Passives.Count) World.AllocatePassive(player.Id, _classes.Passives[command.Index]);
                    break;
                case InventoryCommand.ResetPassives:
                    World.ResetPassives(player.Id);
                    break;
                case InventoryCommand.OpenRift:
                    World.OpenRift(player.Id);
                    break;
                case InventoryCommand.StartDefense:
                    World.StartDefense(player.Id);
                    break;
                case InventoryCommand.Upgrade:
                    World.UpgradeItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Salvage:
                    World.SalvageItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Unsocket:
                    World.UnsocketItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Reroll:
                    World.RerollItem(player.Id, command.Item);
                    break;
                case InventoryCommand.TransferUpgrade:
                    World.TransferUpgrade(player.Id, command.Item);
                    break;
                case InventoryCommand.GetUp:
                    World.GetUp(player.Id);
                    break;
                case InventoryCommand.StoreItem:
                case InventoryCommand.TakeItem:
                    World.MoveItem(player.Id, command.Item, command.Command == InventoryCommand.StoreItem);
                    break;
                case InventoryCommand.StoreStack:
                case InventoryCommand.TakeStack:
                    World.MoveStack(player.Id, (StackKind)command.Item, command.Index, command.Command == InventoryCommand.StoreStack);
                    break;
                case InventoryCommand.Discard:
                    World.DiscardItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Buy:
                    if (_npcs != null && command.Index < _npcs.Shop.Count) World.Buy(player.Id, _npcs.Shop[command.Index], command.Item);
                    break;
                case InventoryCommand.Sell:
                    World.Sell(player.Id, Stack((StackKind)(command.Item & 3), command.Index), command.Item >> 2);
                    break;
                case InventoryCommand.SellItem:
                    World.SellItem(player.Id, command.Item);
                    break;
                case InventoryCommand.Gift:
                    World.Gift(player.Id, Stack((StackKind)command.Item, command.Index));
                    break;
                case InventoryCommand.Talk:
                    World.Talk(player.Id);
                    break;
                case InventoryCommand.Request:
                    World.CompleteRequest(player.Id);
                    break;
                case InventoryCommand.Support:
                    if (_classes == null || command.Index >= _classes.Skills.Count) break;
                    if (command.Item > 0 && command.Item <= _loot.Materials.Count) World.AddSupport(player.Id, _classes.Skills[command.Index], _loot.Materials[command.Item - 1]);
                    else if (command.Item <= 0) World.RemoveSupport(player.Id, _classes.Skills[command.Index], -command.Item);
                    break;
                case InventoryCommand.Hatch:
                    if (_loot != null && command.Item >= 0 && command.Item < _loot.Materials.Count) World.Hatch(player.Id, _loot.Materials[command.Item]);
                    break;
                case InventoryCommand.Invite:
                    World.Invite(player.Id);
                    break;
                case InventoryCommand.SortBag:
                    player.Inventory.Sort();
                    player.InventoryVersion++;
                    break;
                case InventoryCommand.Story:
                    World.CompleteStory(player.Id);
                    break;
                case InventoryCommand.ResetSkills:
                    World.ResetSkills(player.Id);
                    break;
                case InventoryCommand.LearnSkill:
                    if (_classes != null && command.Index < _classes.Skills.Count) World.LearnSkill(player.Id, _classes.Skills[command.Index]);
                    break;
                case InventoryCommand.Load:
                    if (command.Index < _loot.Materials.Count) World.LoadMachine(player.Id, _loot.Materials[command.Index], Math.Clamp(command.Item, 1, World.MaxTrade));
                    break;
                case InventoryCommand.Unload:
                    World.UnloadMachine(player.Id);
                    break;
                case InventoryCommand.PetAnimals:
                    World.PetAnimals(player.Id);
                    break;
                case InventoryCommand.LockDoor:
                    World.LockDoor(player.Id);
                    break;
                case InventoryCommand.UnlockDoor:
                    World.UnlockDoor(player.Id);
                    break;
                case InventoryCommand.TalkStory:
                    World.TalkInStory(player.Id);
                    break;
                case InventoryCommand.Teleport:
                    World.Teleport(player.Id, command.Index);
                    break;
                case InventoryCommand.ChooseStory:
                    World.ChooseInStory(player.Id, command.Index);
                    break;
            }
        }

        // A material or consumable by its kind and sheet index, or null.
        object Stack(StackKind kind, int index) => kind switch
        {
            StackKind.Material when index < _loot.Materials.Count => _loot.Materials[index],
            StackKind.Consumable when index < _loot.Consumables.Count => _loot.Consumables[index],
            _ => null,
        };

        void ApplyInput(Connection sender, InputMessage input)
        {
            if (sender.Player == null || input.Sequence <= sender.LastSequence) return;
            int elapsed = sender.LastSequence == 0 ? 1 : input.Sequence - sender.LastSequence;
            sender.LastSequence = input.Sequence;
            // A roll (World.Roll.cs) is named by the tick it began. One the host has not heard of begins now, if it may;
            // the faster steps are taken only for a roll the host took, and only for as many ticks as a roll lasts.
            bool rolling = false;
            if (input.RollAge > 0 && input.RollAge <= World.RollTicks)
            {
                if (input.RollStart > sender.RollStart && World.Roll(sender.Player.Id, input.RollDirection)) sender.RollStart = input.RollStart;
                rolling = input.RollStart == sender.RollStart;
            }
            World.MovePlayer(sender.Player.Id, input.Position, input.Facing, elapsed, rolling);
            World.SetAttacking(sender.Player.Id, input.Attacking);
        }

        SnapshotMessage Snapshot(Player viewer)
        {
            float Distance(Vec2 position) => Vec2.Distance(position, viewer.Position);
            // Only what is in the viewer's part of the map: the world, or their dungeon's slot of the pocket (World.AreaOf).
            int area = World.AreaOf(viewer.Position);
            bool InView(Vec2 position, float size = 0) => Distance(position) <= _config.ViewRange + size && World.AreaOf(position) == area;

            var slots = Enumerable.Range(0, viewer.Skills.Count).ToList();
            var self = new SelfState(
                viewer.Corrections,
                viewer.Position,
                viewer.Health,
                viewer.Stats.MaxHealth,
                viewer.MoveSpeed,
                slots.Select(viewer.CooldownTicks).ToList(),
                slots.Select(viewer.BuffTicks).ToList(),
                viewer.ConsumableCooldown,
                viewer.Food == null ? -1 : IndexOf(_loot?.Consumables, viewer.Food),
                viewer.FoodTicks,
                (byte)(viewer.Afflictions.Bits | (viewer.Recalling ? StatusBits.Recalling : 0) | (viewer.Sleeping ? StatusBits.Sleeping : 0)),
                viewer.Experience,
                World.BedOf(viewer)?.X ?? -1,
                World.BedOf(viewer)?.Y ?? -1,
                viewer.Mana,
                viewer.Stats.MaxMana,
                viewer.Crafting == null ? -1 : IndexOf(_loot?.Recipes, viewer.Crafting.Recipe),
                viewer.Crafting?.Progress ?? 0,
                viewer.Crafting?.Left ?? 0,
                World.ArtTicksLeft(viewer, viewer.Stats.Kind),
                World.ArtTicksLeft(viewer, viewer.Equipment.Spare?.Base.Kind),
                viewer.Art?.Busy == true ? viewer.Art.Age : -1);
            // A player's type is the weapon in their hands (its base's index plus one), so others can draw it.
            var players = World.Players.Select(p => new EntityState(
                p.Id, EntityKind.Player, HeldWeapon(p), p.Position, p.Facing, p.Health, p.Stats.MaxHealth, BuffBits(p),
                Protocol.WithStatus((p.Sleeping ? PlayerPhase.Sleeping : 0) | Pets.IndexOf(p.Pet) << PlayerPhase.PetShift, p.Afflictions.Bits)));
            var bosses = World.Bosses
                .Where(b => !b.IsDead && InView(b.Position, b.Definition.Radius))
                .Select(b => new EntityState(
                    b.Id, EntityKind.Boss, IndexOf(_bosses?.Bosses, b.Definition), b.Position, b.Facing, b.Health, b.MaxHealth, World.BossStateByte(b),
                    Protocol.WithStatus(World.BossPhaseField(b), b.Afflictions.Bits), aim: World.BossAim(b)));
            var minions = World.Minions
                .Where(m => InView(m.Position))
                .Select(m => new EntityState(
                    m.Id, EntityKind.Minion, IndexOf(_classes?.Skills, m.Skill), m.Position, m.Facing, m.TicksLeft, m.Skill.DurationTicks, 0));
            var shownSoFar = players.Concat(bosses).Concat(minions).ToList();
            var enemies = World.Enemies
                // One under the ground for a burrow or gone for a shadow step is not there to be seen; one that lives
                // under it or lies in wait is sent, to be drawn as its mound (World.EnemyBuriedTell).
                .Where(e => InView(e.Position) && (!e.Away || e.Buried))
                .OrderBy(e => Distance(e.Position))
                .Take(Math.Max(0, _config.MaxEntitiesPerSnapshot - shownSoFar.Count))
                .Select(e => new EntityState(
                    e.Id, EntityKind.Monster, IndexOf(_monsters.Monsters, e.Definition), e.Position, e.Facing, e.Health, e.MaxHealth, World.EnemyStateByte(e),
                    Protocol.WithStatus(World.EnemyPhaseField(e), e.Afflictions.Bits), World.EliteField(e), World.EnemyAim(e)));
            var drops = World.GroundLoot
                .Where(l => InView(l.Position) && l.For(viewer.Id, World.Tick))
                .OrderBy(l => Distance(l.Position))
                .Take(_config.MaxDropsPerSnapshot)
                .Select(l => new EntityState(
                    l.Id,
                    EntityKind.Drop,
                    l.Item != null ? IndexOf(_loot.Bases, l.Item.Base)
                        : l.Rune != null ? IndexOf(_loot.Runes, l.Rune)
                        : l.Consumable != null ? IndexOf(_loot.Consumables, l.Consumable)
                        : IndexOf(_loot.Materials, l.Material),
                    l.Position,
                    Vec2.Zero,
                    0,
                    0,
                    l.Item != null ? (byte)l.Item.Rarity : l.Rune != null ? DropState.Rune : l.Consumable != null ? DropState.Consumable : DropState.Material));
            var entities = shownSoFar.Concat(enemies).Concat(drops).ToList();

            var projectiles = World.Projectiles
                .Where(p => InView(p.Position))
                .OrderBy(p => Distance(p.Position))
                .Take(_config.MaxProjectilesPerSnapshot)
                .Select(p => new ProjectileState(p.Id, p.Position, p.Velocity, p.Radius, p.Friendly, p.Tags, p.Friendly ? p.RemainingTicks : 0, p.Look))
                .ToList();

            // A monster's attack that starts at the monster is drawn from its state and the sheet, so its area is not
            // sent (Hazard.Attached, Docs/WorldRework.md 통신).
            var hazards = World.Hazards
                .Where(h => !h.Attached && InView(h.Position, h.Size))
                .OrderBy(h => h.ResolveTick)
                .Take(_config.MaxHazardsPerSnapshot)
                .Select(h => new HazardState(
                    h.Id, h.Shape, h.Position, h.Direction, h.Size, h.Width,
                    (int)(h.ResolveTick - World.Tick), (int)(h.ResolveTick - h.StartTick), h.Damage > 0,
                    WarningOf(h), h.Mark != 0 ? (byte)(HazardState.MonsterMark | h.Mark) : (byte)Math.Min(h.Cause, HazardState.MonsterMark - 1)))
                .ToList();

            // When there are too many, what changes the fight matters most and swings least. A fallen boss is no longer
            // among the entities, but its fall (which carries where) still reaches whoever could see it ("○○ 처치!").
            var shown = new HashSet<int>(entities.Select(e => e.Id));
            var events = _pendingEvents
                .Where(e => shown.Contains(e.Source) || shown.Contains(e.Target) || e.Type == SimEventType.Morning || e.Type == SimEventType.Rift
                    || e.Type == SimEventType.Died && e.Position.LengthSquared > 0 && InView(e.Position)
                    || e.Type == SimEventType.MonsterFx && InView(e.Position)
                    // A mark no one laid landing (an icicle, a steam vent's burst: Source 0) is told to whoever sees where.
                    || e.Type == SimEventType.Impact && e.Source == 0 && InView(e.Position)
                    // A dungeon's own turns (room cleared, key, master down, door open) come from no one: told to
                    // everyone in that dungeon's part of the pocket, wherever in it they stand.
                    || e.Type == SimEventType.Dungeon && e.Source == 0 && World.AreaOf(e.Position) == area
                    || e.Type == SimEventType.Defense || e.Type == SimEventType.TurretShot || e.Type == SimEventType.Rain)
                .OrderBy(e => e.Type == SimEventType.Attack ? 2 : e.Type == SimEventType.Hit ? 1 : 0)
                .Take(_config.MaxEventsPerSnapshot)
                .ToList();
            var open = World.ActiveRift;
            var rift = new RiftState(World.RiftTier, open?.Tier ?? 0, open?.Wave ?? 0, open?.MonstersLeft ?? 0,
                (int)((open?.TicksLeft ?? 0) / World.TicksPerSecond), open?.Affixes ?? RiftAffix.None);
            var defending = World.ActiveDefense;
            var defense = new DefenseState(World.DefenseLevel, World.DefendedToday, defending?.Level ?? 0, defending?.Wave ?? 0, defending?.MonstersLeft ?? 0,
                defending == null ? 0 : (int)Math.Ceiling(defending.StoneHealth / defending.MaxStoneHealth * 100));
            return new SnapshotMessage(World.Tick, self, entities, projectiles, hazards, events, World.Clock, World.Sleepers, rift, defense, World.DayMinutes);
        }

        // The snapshot as it goes out: up to MaxEntitiesPerSnapshot (two packs and four players, 2026-11-22), but never
        // past MaxSnapshotBytes; over it, the furthest monsters are left out first (Docs/WorldRework.md 상한).
        byte[] SnapshotBytes(Player viewer)
        {
            var message = Snapshot(viewer);
            var data = message.Encode();
            while (data.Length > _config.MaxSnapshotBytes)
            {
                var entities = message.Entities.ToList();
                int over = (data.Length - _config.MaxSnapshotBytes + EntityBytes - 1) / EntityBytes;
                int dropped = 0;
                for (int i = entities.Count - 1; i >= 0 && dropped < over; i--)
                {
                    if (entities[i].Kind != EntityKind.Monster) continue;
                    entities.RemoveAt(i);
                    dropped++;
                }
                if (dropped == 0) break;
                message = new SnapshotMessage(message.Tick, message.Self, entities, message.Projectiles, message.Hazards, message.Events, message.Clock, message.Sleepers,
                    message.Rift, message.Defense, message.DayMinutes);
                data = message.Encode();
            }
            return data;
        }

        // What one entity takes in a snapshot.
        const int EntityBytes = 17;

        (int X, int Y, int Crop, long Grown, long WetFrom, long WetUntil) CropEntry(CropChange change) =>
            (change.X, change.Y, change.Crop == null ? -1 : IndexOf(_tiles?.Crops, change.Crop.Definition), change.Crop?.Grown ?? 0,
                change.Crop?.WetFrom ?? 0, change.Crop?.WetUntil ?? 0);

        // How often the production list goes out.
        static readonly int ProductionInterval = World.Ticks(5);

        void SendProduction()
        {
            if (_loot == null) return;
            var made = World.Production()
                .Select(p => p.Made is ConsumableDefinition c ? (1, IndexOf(_loot.Consumables, c), p.Count) : (0, IndexOf(_loot.Materials, p.Made as MaterialDefinition), p.Count))
                .Where(entry => entry.Item2 >= 0)
                .ToList();
            byte[] message = new ProductionMessage(World.ProductionSpan, made).Encode();
            foreach (var pair in _connections.Where(pair => pair.Value.Player != null))
            {
                if (pair.Value.SentProduction != null && pair.Value.SentProduction.AsSpan().SequenceEqual(message)) continue;
                pair.Value.SentProduction = message;
                Send(pair.Key, message, reliable: true);
            }
        }

        void SendFriendships(ulong connection, Connection sent)
        {
            if (_npcs == null) return;
            var known = sent.Player.Friendships
                .Select(pair => (IndexOf(_npcs.Npcs, _npcs.Npcs.FirstOrDefault(n => n.Id == pair.Key)), pair.Value.Points, pair.Value.TalkedDay, pair.Value.GiftedDay,
                    pair.Value.RequestDay, pair.Value.HeartGifts))
                .Where(entry => entry.Item1 >= 0)
                .OrderBy(entry => entry.Item1)
                .ToList();
            // Everyone sees the same day's sales: they are the people's, not the player's.
            var sold = _npcs.Shop.Select((entry, index) => (index, World.SoldToday(entry))).Where(s => s.Item2 > 0).ToList();
            byte[] message = new FriendshipsMessage(known, sold).Encode();
            if (sent.SentFriendships != null && sent.SentFriendships.AsSpan().SequenceEqual(message)) return;
            sent.SentFriendships = message;
            Send(connection, message, reliable: true);
        }

        void SendStock(ulong connection, Connection sent)
        {
            if (_loot == null) return;
            var stock = World.StockNear(sent.Player.Position)
                .Select(pair => (IndexOf(_loot.Materials, pair.Key), pair.Value))
                .Where(entry => entry.Item1 >= 0)
                .OrderBy(entry => entry.Item1)
                .ToList();
            byte[] message = new StockMessage(stock).Encode();
            if (sent.SentStock != null && sent.SentStock.AsSpan().SequenceEqual(message)) return;
            sent.SentStock = message;
            Send(connection, message, reliable: true);
        }

        // Machines in view, a message of at most MaxMachinesPerMessage at a time (2026-11-09, Docs/Automation.md 통신):
        // only the ones that changed since they were last sent, and each again every MachineRefreshSeconds in case a
        // message was lost, so a still base costs next to nothing and a big one is not cut off at the cap. Past
        // MachineMessagesPerTick messages the rest wait a tick, nearest first.
        void SendMachines(ulong connection, Connection sent)
        {
            var viewer = sent.Player.Position;
            long now = World.Tick;
            long refresh = (long)(MachineRefreshSeconds * World.TicksPerSecond);
            var inView = World.Machines
                .Where(m => Vec2.Distance(TileMap.Center(m.X, m.Y), viewer) <= _config.ViewRange)
                .OrderBy(m => Vec2.Distance(TileMap.Center(m.X, m.Y), viewer))
                .ToList();
            var keys = new HashSet<int>();
            var due = new List<(int Key, MachineState State)>();
            foreach (var m in inView)
            {
                int key = m.Y * World.Map.Width + m.X;
                keys.Add(key);
                var state = new MachineState(m.X, m.Y, m.Kind, m.Direction, m.Carried == null ? -1 : IndexOf(_loot.Materials, m.Carried), m.Progress, m.Enabled,
                    SettingOf(m), World.IsPowered(m), m.Inputs.Values.Sum(), m.Outputs.Values.Sum(), m.Settings.Select(s => IndexOf(_loot.Materials, s)).ToList());
                if (sent.SentMachineStates.TryGetValue(key, out var last) && last.State.SameAs(state) && now - last.Tick < refresh) continue;
                due.Add((key, state));
            }
            // Out of view: forgotten, so coming back sends them at once.
            foreach (int gone in sent.SentMachineStates.Keys.Where(k => !keys.Contains(k)).ToList()) sent.SentMachineStates.Remove(gone);
            int most = _config.MaxMachinesPerMessage;
            for (int start = 0; start < due.Count && start < most * MachineMessagesPerTick; start += most)
            {
                var part = due.Skip(start).Take(most).ToList();
                foreach (var (key, state) in part) sent.SentMachineStates[key] = (state, now);
                Send(connection, new MachinesMessage(part.Select(p => p.State).ToList()).Encode(), reliable: false);
            }

            int SettingOf(Machine m)
            {
                if (World.IsAssembler(m.Kind)) return m.Recipe == null ? -1 : IndexOf(_loot.Recipes, m.Recipe);
                if (World.HouseOf(m.Kind) != null) return World.HouseSetting(World.AnimalsIn(m), m.Affection, m.PettedDay == World.Today);
                if (World.IsPowerNode(m.Kind)) return World.GridOf(m) is PowerGrid grid ? MachineState.Grid(grid.Supply, grid.Demand) : -1;
                return m.Setting == null ? -1 : IndexOf(_loot.Materials, m.Setting);
            }
        }

        const float MachineRefreshSeconds = 1;
        const int MachineMessagesPerTick = 3;

        // The chest the player stands by, when it is a different one or it changed; a closed message when they left it.
        void SendChest(ulong connection, Connection sent)
        {
            var near = World.ChestNear(sent.Player.Position);
            int key = near == null ? -1 : near.Value.Y * World.Map.Width + near.Value.X;
            int version = near == null ? 0 : World.ChestVersion(near.Value.X, near.Value.Y);
            if (key == sent.SentChest && version == sent.SentChestVersion) return;
            sent.SentChest = key;
            sent.SentChestVersion = version;
            var message = near == null ? ChestMessage.Closed : new ChestMessage(near.Value.X, near.Value.Y, World.ChestAt(near.Value.X, near.Value.Y));
            Send(connection, message.Encode(_loot), reliable: true);
        }

        int HeldWeapon(Player player)
        {
            var weapon = player.Equipment[ItemSlot.Weapon];
            return weapon == null ? 0 : IndexOf(_loot.Bases, weapon.Base) + 1;
        }

        static byte BuffBits(Player player)
        {
            int bits = 0;
            for (int slot = 0; slot < Math.Min(8, player.Skills.Count); slot++)
            {
                if (player.HasBuff(slot)) bits |= 1 << slot;
            }
            return (byte)bits;
        }

        List<int> ClassIndexes(Player player) => player.Classes.Select(c => IndexOf(_classes?.Classes, c)).ToList();

        // How plainly the attack that laid a hazard is announced (a roar's blast, cause 0, is marked).
        AttackWarning WarningOf(Hazard hazard)
        {
            // A monster's: as its sheet says, or strong for an elite leader's big attack (World.WarningOf).
            var enemy = hazard.Mark != 0 ? World.FindEnemy(hazard.Source) : null;
            var monster = enemy?.Definition.Attacks;
            if (monster != null)
                return hazard.Cause >= 1 && hazard.Cause <= monster.Count ? World.WarningOf(enemy.Elite, enemy.Definition, monster[hazard.Cause - 1]) : AttackWarning.Marked;
            var attacks = World.FindBoss(hazard.Source)?.Definition.Attacks;
            return attacks != null && hazard.Cause >= 1 && hazard.Cause <= attacks.Count ? attacks[hazard.Cause - 1].Warning : AttackWarning.Marked;
        }

        static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] == item) return i;
            }
            throw new InvalidOperationException($"{typeof(T).Name} is not in the host's sheets, so clients could not name it.");
        }

        // New classes or a new order of skills. The player always hears back what they have now, done or not, so the
        // screen never shows a kit the host did not take.
        void ChangeKit(ulong connection, Player player, byte[] data)
        {
            var request = KitMessage.Decode(data, MessageType.ClassCommand);
            var ids = request.Classes.Where(i => _classes != null && i >= 0 && i < _classes.Classes.Count).Select(i => _classes.Classes[i].Id);
            var classes = ClassRules.Choose(_classes, ids, _config.MaxClasses);
            bool changed = World.ChangeClasses(player.Id, classes, request.SkillOrder);
            Send(connection, new KitMessage(MessageType.Kit, ClassIndexes(player), player.SkillOrder).Encode(), reliable: true);
            if (_connections.TryGetValue(connection, out var sent)) sent.SentKit = player.KitVersion;
            if (changed) BroadcastRoster();
        }

        void BroadcastRoster()
        {
            var players = _connections.Values.Where(c => c.Player != null).Select(c => c.Player).ToList();
            byte[] roster = new RosterMessage(
                players.ToDictionary(p => p.Id, p => p.Name),
                players.ToDictionary(p => p.Id, p => (IReadOnlyList<int>)ClassIndexes(p)),
                players.ToDictionary(p => p.Id, p => p.Look)).Encode();
            foreach (var pair in _connections)
            {
                if (pair.Value.Player != null) Send(pair.Key, roster, reliable: true);
            }
        }

        void Send(ulong connection, byte[] data, bool reliable) => _outgoing.Add(new Outgoing(connection, data, reliable));
    }
}
