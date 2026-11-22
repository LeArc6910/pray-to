using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The things to find in the rooms of a version 2 world (2026-11-22, Docs/WorldRework.md 3단계 볼거리, 묶음 3a): camps,
    // shrines, chests and abandoned carts, where the generator laid them (RoomGraph.Pois, pois.csv). What became of them
    // is game state: the chests and carts opened (once for the world), the shrines each player used today and the last
    // camp each rested at (by name, as beds and locks are: a character goes from world to world). All of it goes in the
    // world's save and to the clients (WorldHost, PoisMessage). A camp is safe (no monster comes within CampSafeReach),
    // heals by the fire and leaves those who rest there "rested" (more experience) and getting up there when they fall.
    public sealed partial class World
    {
        // How close a player must be to open a chest or a cart or to pray at a shrine, from their centre to its tile's.
        public const float PoiReach = 2.2f;
        // Round a camp's fire: no monster comes this near, and this near the fire heals and rests.
        public const float CampSafeReach = 8;
        public const float CampRestReach = 4;
        // Seconds by the fire before a player has rested (the camp is then the one they get up at).
        public const float RestSeconds = 3;
        // The players near an opened chest who each roll their own (Docs/WorldRework.md: 사람마다 따로 굴린다).
        public const float ChestShareReach = 12;
        // Experience while rested, in percent over the usual.
        public const int RestedExperiencePercent = 10;

        IReadOnlyList<Poi> _pois = Array.Empty<Poi>();
        bool[] _poiOpened = Array.Empty<bool>();
        List<Poi> _camps = new List<Poi>();
        // Shrine by shrine, the day each player last prayed there (by name).
        readonly Dictionary<string, Dictionary<int, int>> _prayed = new Dictionary<string, Dictionary<int, int>>();
        // The camp each player last rested at (by name).
        readonly Dictionary<string, int> _lastCamps = new Dictionary<string, int>();

        public IReadOnlyList<Poi> Pois => _pois;
        int _poiDay = -1;

        // Goes up whenever a chest or cart is opened, a shrine prayed at or a camp rested at, so the host tells the
        // players again (WorldHost, PoisMessage).
        public int PoiVersion { get; private set; }

        void SetPois(RoomGraph rooms)
        {
            _pois = rooms?.Pois ?? (IReadOnlyList<Poi>)Array.Empty<Poi>();
            _poiOpened = new bool[_pois.Count];
            _camps = _pois.Where(p => p.Kind == PoiKind.Camp).ToList();
            // The minecart lines and the waystones (World.Travel.cs, 3c), and where the wandering merchant stands today.
            SetTravel(rooms);
            _wandererDay = int.MinValue;
            MoveWanderer();
            PoiVersion++;
        }

        public bool Opened(int index) => index >= 0 && index < _poiOpened.Length && _poiOpened[index];

        // The chests and carts opened, by index, for the save and the clients.
        public IEnumerable<int> OpenedPois => Enumerable.Range(0, _poiOpened.Length).Where(i => _poiOpened[i]);

        // The shrines a player prayed at today.
        public IEnumerable<int> PrayedToday(string name)
        {
            if (name == null || !_prayed.TryGetValue(name, out var days)) return Enumerable.Empty<int>();
            int today = Calendar.Day(Clock);
            return days.Where(p => p.Value == today).Select(p => p.Key).OrderBy(i => i);
        }

        // Whether the player has ever prayed at a shrine of this world (the guide's poi:shrine, 묶음 4c).
        public bool PrayedEver(string name) => name != null && _prayed.TryGetValue(name, out var days) && days.Count > 0;

        public int? LastCampOf(string name) => name != null && _lastCamps.TryGetValue(name, out int camp) && camp < _pois.Count ? camp : (int?)null;

        // Everything the save keeps of the things to find: the opened, the shrines' days by name, the last camps by name.
        public IEnumerable<(string Name, int Poi, int Day)> Prayers => _prayed.SelectMany(p => p.Value.Select(d => (p.Key, d.Key, d.Value)));
        public IEnumerable<(string Name, int Poi)> LastCamps => _lastCamps.Select(p => (p.Key, p.Value));

        // Puts back what a save kept (WorldSave format 13). Indexes past this world's things are dropped.
        public void RestorePois(IEnumerable<int> opened, IEnumerable<(string Name, int Poi, int Day)> prayers, IEnumerable<(string Name, int Poi)> camps)
        {
            foreach (int index in opened ?? Enumerable.Empty<int>())
                if (index >= 0 && index < _poiOpened.Length) _poiOpened[index] = true;
            foreach (var (name, poi, day) in prayers ?? Enumerable.Empty<(string, int, int)>())
            {
                if (poi < 0 || poi >= _pois.Count) continue;
                if (!_prayed.TryGetValue(name, out var days)) _prayed[name] = days = new Dictionary<int, int>();
                days[poi] = day;
            }
            foreach (var (name, poi) in camps ?? Enumerable.Empty<(string, int)>())
                if (poi >= 0 && poi < _pois.Count && _pois[poi].Kind == PoiKind.Camp) _lastCamps[name] = poi;
            // A chest opened shows open.
            foreach (int index in OpenedPois) if (_pois[index].Kind == PoiKind.Chest) Map[_pois[index].X, _pois[index].Y] = TileType.OpenChest;
            // The save's tiles may hold the wandering merchant's stall by the camp of the day it was saved.
            _wandererDay = int.MinValue;
            MoveWanderer();
            PoiVersion++;
        }

        // The thing to find within PoiReach of the position that can be used now, nearest first; camps and waystones are
        // not used by hand (a waystone opens when touched and is gone from by its waypoint list).
        public Poi PoiNear(Vec2 position, Player player = null)
        {
            Poi best = null;
            float bestDistance = PoiReach;
            foreach (var poi in _pois)
            {
                if (poi.Kind == PoiKind.Camp || poi.Kind == PoiKind.Waystone || !CanUse(poi, player)) continue;
                float distance = Vec2.Distance(position, poi.Center);
                if (distance > bestDistance) continue;
                (best, bestDistance) = (poi, distance);
            }
            return best;
        }

        // Whether it gives now: a chest or cart not yet opened, a shrine the player has not prayed at today.
        public bool CanUse(Poi poi, Player player) => poi.Definition.Again switch
        {
            PoiAgain.Once => !Opened(poi.Index),
            PoiAgain.Daily => player == null || !PrayedToday(player.Name).Contains(poi.Index),
            _ => true,
        };

        // A player opens the chest or cart, or prays at the shrine, of that index within reach. False when it is out of
        // reach, already done, or not one used by hand.
        public bool UsePoi(int playerId, int index)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || index < 0 || index >= _pois.Count) return false;
            var poi = _pois[index];
            // A station's cart is ridden, or its broken rails mended (World.Travel.cs).
            if (poi.Kind == PoiKind.Station) return Vec2.Distance(player.Position, poi.Center) <= PoiReach && UseStation(player, poi);
            if (poi.Kind == PoiKind.Camp || poi.Kind == PoiKind.Waystone || Vec2.Distance(player.Position, poi.Center) > PoiReach || !CanUse(poi, player)) return false;
            var definition = poi.Definition;
            if (poi.Kind == PoiKind.Shrine)
            {
                if (!_prayed.TryGetValue(player.Name, out var days)) _prayed[player.Name] = days = new Dictionary<int, int>();
                days[index] = Calendar.Day(Clock);
                Grant(player, poi);
                _events.Add(new SimEvent(SimEventType.Blessed, player.Id, amount: index, position: poi.Center));
            }
            else
            {
                _poiOpened[index] = true;
                if (poi.Kind == PoiKind.Chest) SetTile(poi.X, poi.Y, TileType.OpenChest);
                // A chest each player near rolls for themselves; a cart is the opener's.
                var owners = definition.PerPlayer
                    ? _players.Where(p => !p.IsDead && (p == player || Vec2.Distance(p.Position, poi.Center) <= ChestShareReach)).ToList()
                    : new List<Player> { player };
                foreach (var owner in owners) Grant(owner, poi);
                _events.Add(new SimEvent(SimEventType.PoiOpened, player.Id, amount: index, position: poi.Center));
            }
            PoiVersion++;
            return true;
        }

        // What the thing gives one player, by its sheet's reward.
        void Grant(Player player, Poi poi)
        {
            var definition = poi.Definition;
            int level = Math.Max(ProgressLevel, ZoneLevelAt(poi.Center)) + definition.RewardLevel;
            int rarity = (int)player.Stats.ItemRarity;
            foreach (var reward in definition.Rewards)
            {
                int count = reward.Min == reward.Max ? reward.Min : _random.Next(reward.Min, reward.Max + 1);
                switch (reward.Kind)
                {
                    case PoiRewardKind.Items:
                        for (int i = 0; i < count && _lootGenerator != null; i++) Give(player, _lootGenerator.RollItem(level, _random, rarity, reward.Rarity));
                        break;
                    case PoiRewardKind.Runes:
                        for (int chance = count; chance > 0 && _lootGenerator != null; chance -= 100)
                        {
                            if (chance < 100 && _random.Next(100) >= chance) break;
                            if (_lootGenerator.RollRune(level, _random) is RuneDefinition rune) player.Inventory.AddRune(rune);
                        }
                        break;
                    case PoiRewardKind.Potions:
                        for (int i = 0; i < count && Loot != null; i++)
                            if (RollConsumable(level) is ConsumableDefinition potion) player.Inventory.AddConsumable(potion);
                        break;
                    case PoiRewardKind.Coins:
                        AddMaterial(player, Coin, count);
                        break;
                    case PoiRewardKind.Ore:
                        AddMaterial(player, OreAt(poi), count);
                        break;
                    case PoiRewardKind.Forage:
                        AddMaterial(player, ForageOf.TryGetValue(poi.Biome, out string forage) ? forage : null, count);
                        break;
                    case PoiRewardKind.Material:
                        AddMaterial(player, reward.Material, count);
                        break;
                    case PoiRewardKind.Heal:
                        if (!player.IsDead) player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.MaxHealth * count / 100f);
                        break;
                    case PoiRewardKind.Bless:
                        player.Bless(definition, Ticks(count));
                        // The blessing may raise the most health: a full heal after it fills that too.
                        if (definition.Reward(PoiRewardKind.Heal) is PoiReward heal && !player.IsDead)
                            player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.MaxHealth * heal.Min / 100f);
                        break;
                }
            }
            player.InventoryVersion++;
        }

        void AddMaterial(Player player, string id, int count)
        {
            var material = id == null ? null : Loot?.Materials.FirstOrDefault(m => m.Id == id);
            if (material != null && count > 0) player.Inventory.AddMaterial(material, count);
        }

        // The ore of the stage where a thing stands, as the ore rooms round it have (WorldGenerator.Veins): copper in
        // the first caves, iron, mithril by the crystal and the lake, obsidian in the lava fields, void stone in the abyss.
        static string OreAt(Poi poi) => poi.Biome switch
        {
            Biome.Abyss => "void_ore",
            Biome.Lava => "obsidian_ore",
            Biome.Crystal or Biome.Lake => "mithril_ore",
            Biome.Cave => "copper_ore",
            _ => "iron_ore",
        };

        // ---- Camps ----

        // Whether a spot is within a camp's safe ring.
        public bool InCamp(Vec2 position)
        {
            foreach (var camp in _camps)
                if (Vec2.Distance(position, camp.Center) <= CampSafeReach) return true;
            return false;
        }

        // A monster that came into a camp's ring is put back at its edge and gives up the chase, as at a place.
        void KeepOutOfCamps(Enemy enemy)
        {
            if (enemy.Rift != 0 || _camps.Count == 0) return;
            foreach (var camp in _camps)
            {
                float edge = CampSafeReach + enemy.Definition.Radius;
                var away = enemy.Position - camp.Center;
                float distance = away.Length;
                if (distance >= edge) continue;
                var outward = distance > 1e-3f ? away / distance : new Vec2(0, -1);
                enemy.Position = Collision.Resolve(Map, camp.Center + outward * edge, enemy.Definition.Radius, monster: true);
                if (enemy.State == EnemyState.Chase) enemy.State = EnemyState.Idle;
            }
        }

        // Each tick for each living player: the blessing and the rest running out, and by a camp's fire healing and,
        // after RestSeconds, resting there (rested again, and the camp the one they get up at).
        void UpdatePois(Player player)
        {
            player.TickPoiBuffs();
            // A new day: the shrines give again.
            int today = Calendar.Day(Clock);
            if (today != _poiDay)
            {
                _poiDay = today;
                PoiVersion++;
            }
            if (_camps.Count == 0) return;
            Poi near = null;
            foreach (var camp in _camps)
            {
                if (Vec2.Distance(player.Position, camp.Center) > CampRestReach) continue;
                near = camp;
                break;
            }
            if (near == null)
            {
                player.CampTicks = 0;
                return;
            }
            // The fire heals, and since rest away from it is slow (묶음 4c, World.Heal) it brings the mana back as fast.
            var rest = near.Definition.Reward(PoiRewardKind.Rest);
            if (rest != null && player.Health < player.Stats.MaxHealth)
                player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.MaxHealth * rest.Min / 100f * TickSeconds);
            if (rest != null && player.Mana < player.Stats.MaxMana)
                player.Mana = Math.Min(player.Stats.MaxMana, player.Mana + player.Stats.MaxMana * rest.Min / 100f * TickSeconds);
            if (++player.CampTicks != Ticks(RestSeconds)) return;
            bool moved = LastCampOf(player.Name) != near.Index;
            _lastCamps[player.Name] = near.Index;
            var rested = near.Definition.Reward(PoiRewardKind.Rested);
            bool fresh = rested != null && player.RestedTicks < Ticks(rested.Min) / 2;
            if (rested != null) player.RestedTicks = Ticks(rested.Min);
            if (moved || fresh)
            {
                _events.Add(new SimEvent(SimEventType.Blessed, player.Id, amount: near.Index, position: near.Center));
                PoiVersion++;
            }
        }

        // Where a fallen player gets up: by the last camp they rested at in this world, or at home (their bed).
        Vec2 RisingPlace(Player player)
        {
            if (LastCampOf(player.Name) is not int camp) return HomeOf(player);
            var fire = _pois[camp];
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (-1, 0), (0, 1), (1, -1), (-1, -1), (1, 1), (-1, 1), (0, -2), (2, 0), (-2, 0), (0, 2) })
            {
                int x = fire.X + dx, y = fire.Y + dy;
                if (Map.InBounds(x, y) && !Map.IsSolid(x, y) && Map[x, y] != TileType.Lava) return TileMap.Center(x, y);
            }
            return HomeOf(player);
        }
    }
}
