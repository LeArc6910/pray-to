using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // How well a player knows the way to somewhere (Docs/WorldRework.md 덜 정확한 안내, 묶음 4c): only by hearsay (which
    // way and in which biome, a faint ring on the map), the way (which way, no distance) or for sure (which way and how
    // far, as before: once been there or seen it, in the story's first chapter, or with the old arrows chosen).
    public enum WayKnown : byte
    {
        Rumor,
        Path,
        Known,
    }

    // What a rumour can be about: the things to find in the rooms (묶음 3a~3c), an elite camp, the wandering merchant
    // where they stand today, and a boss's arena.
    public enum RumorKind : byte
    {
        Camp,
        Shrine,
        // A chest of a treasure room (or a trap's).
        Chest,
        // A chest of a room behind a cracked wall.
        Hidden,
        // A vault behind obsidian or void stone.
        Vault,
        Cart,
        // A minecart line's broken end.
        Station,
        Waystone,
        Elite,
        Wanderer,
        Arena,
    }

    // A thing a rumour can point at: its kind, its number (the thing's Poi.Index, the elite camp's room, the arena's
    // place in Arenas, 0 for the wandering merchant), its tile and its biome.
    public readonly struct RumorTarget : IEquatable<RumorTarget>
    {
        public RumorTarget(RumorKind kind, int index, int x, int y, Biome biome)
        {
            Kind = kind;
            Index = index;
            X = x;
            Y = y;
            Biome = biome;
        }

        public RumorKind Kind { get; }
        public int Index { get; }
        public int X { get; }
        public int Y { get; }
        public Biome Biome { get; }
        public Vec2 Center => TileMap.Center(X, Y);

        public bool Equals(RumorTarget other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is RumorTarget other && Equals(other);
        public override int GetHashCode() => (int)Kind * 7919 + Index;
    }

    // A rumour heard: what it points at, and the day it was heard (the wandering merchant's holds for that day only).
    public readonly struct HeardRumor
    {
        public HeardRumor(RumorKind kind, int index, int day)
        {
            Kind = kind;
            Index = index;
            Day = day;
        }

        public RumorKind Kind { get; }
        public int Index { get; }
        public int Day { get; }
    }

    // Rumours instead of arrows (Docs/WorldRework.md 덜 정확한 안내, 묶음 4c, the person: "I never feel I am exploring,
    // only walking to where I am going"). The people of the story tell, a line a day each, of something to find not yet
    // seen (characters.csv rumor); the goal list says only which way and in which biome until the player has been near;
    // the map rings a wide faint circle about it, not its spot. Everything is worked out from the world's seed and the
    // day, so the host, the clients and the tests come to the same (no state but what each device remembers hearing,
    // MapSave). The clients choose; nothing of it goes over the wire.
    public static class Rumors
    {
        // Nearer than this (in tiles) the way is known, though not how far.
        public const float PathReach = 150;
        // The faint ring a rumour draws on the map, and the smaller one once the way is known.
        public const float RumorRadius = 40;
        public const float PathRadius = 20;
        // A rumour is of one of the nearest this many things not yet known, so it is worth going for.
        public const int Nearest = 6;

        static readonly string[] Ways = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };

        // Which of the eight ways (y up is north) from one spot to another.
        public static string Compass(float dx, float dy)
        {
            if (Math.Abs(dx) < 1e-4f && Math.Abs(dy) < 1e-4f) return "여기";
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            int index = (int)Math.Round((angle + 360) % 360 / 45) % 8;
            return Ways[index];
        }

        // How well the way is known: for sure where the old arrows are chosen or the way is plain (been there, seen it,
        // the story's first chapter); the way within PathReach or once a road's signpost toward it has been seen;
        // else by hearsay.
        public static WayKnown Knowledge(bool rumors, bool sure, float distance, bool signposted = false)
        {
            if (!rumors || sure) return WayKnown.Known;
            return distance <= PathReach || signposted ? WayKnown.Path : WayKnown.Rumor;
        }

        // Whether the main story is still in its first chapter (the way to the royal city, the warden and the hero's
        // hut): there the way is told as before, so the first hour is no harder (Docs/WorldRework.md 덜 정확한 안내).
        public static bool FirstChapter(StoryData story, int journeyStep)
        {
            if (story == null || story.Steps.Count == 0) return false;
            var step = story.Step(journeyStep);
            return step != null && step.Chain.Length == 0 && step.Chapter == story.Steps[0].Chapter;
        }

        // The words after a place's name in the goal list: "북동 34칸", "북동쪽" or "북동쪽, 버섯 숲 어딘가" (only "북동쪽
        // 어딘가" when it is in the biome the player stands in).
        public static string Way(WayKnown known, Vec2 from, Vec2 to, Biome there, Biome here)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            string way = Compass(dx, dy);
            switch (known)
            {
                case WayKnown.Known:
                    return $"{way} {(int)Math.Round(Math.Sqrt(dx * dx + dy * dy))}칸";
                case WayKnown.Path:
                    return way + "쪽";
                default:
                    return there == here ? $"{way}쪽 어딘가" : $"{way}쪽, {BiomeMap.Name(there)} 어딘가";
            }
        }

        // The middle of the faint ring a rumour draws: the thing is inside it, but not at its middle (up to 0.6 of the
        // radius off, by the seed and the thing), so the ring cannot be read as a marker.
        public static Vec2 Circle(ulong seed, RumorKind kind, int index, Vec2 at, float radius)
        {
            var random = new GameRandom(seed + (ulong)((int)kind * 100003 + index) * 2654435761UL, stream: 61);
            float angle = random.NextFloat() * 2 * (float)Math.PI;
            float off = radius * (0.25f + 0.35f * random.NextFloat());
            return at + Vec2.FromAngle(angle) * off;
        }

        public static Vec2 Circle(ulong seed, RumorTarget target, float radius) => Circle(seed, target.Kind, target.Index, target.Center, radius);

        // What there is to hear of in a world of rooms: the things to find (a station only at a line's broken end), the
        // elite camps (where the leader stands), and the wandering merchant's stall today. Nothing for a world of caves.
        public static List<RumorTarget> Targets(RoomGraph rooms, (int X, int Y)? wanderer, BiomeMap biomes, IReadOnlyList<Vec2> arenas = null)
        {
            var targets = new List<RumorTarget>();
            if (rooms != null)
            {
                var broken = new HashSet<int>(rooms.Lines.Where(l => l.Broken >= 0).Select(l => l.Broken));
                foreach (var poi in rooms.Pois)
                {
                    RumorKind? kind = poi.Kind switch
                    {
                        PoiKind.Camp => RumorKind.Camp,
                        PoiKind.Shrine => RumorKind.Shrine,
                        PoiKind.Cart => RumorKind.Cart,
                        PoiKind.Waystone => RumorKind.Waystone,
                        PoiKind.Station => broken.Contains(poi.Index) ? RumorKind.Station : (RumorKind?)null,
                        PoiKind.Chest => poi.Definition.Role == RoomRole.Hidden ? RumorKind.Hidden
                            : poi.Definition.Role == RoomRole.Locked ? RumorKind.Vault : RumorKind.Chest,
                        _ => null,
                    };
                    if (kind.HasValue) targets.Add(new RumorTarget(kind.Value, poi.Index, poi.X, poi.Y, poi.Biome));
                }
                foreach (var room in rooms.Nodes)
                {
                    if (room.Role != RoomRole.Elite) continue;
                    var leader = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Elite).Select(m => ((int, int)?)(m.X, m.Y)).FirstOrDefault();
                    var (x, y) = leader ?? (room.X, room.Y);
                    targets.Add(new RumorTarget(RumorKind.Elite, room.Index, x, y, room.Biome));
                }
            }
            if (wanderer is (int wx, int wy) && biomes != null) targets.Add(new RumorTarget(RumorKind.Wanderer, 0, wx, wy, biomes[wx, wy]));
            if (arenas != null && biomes != null)
            {
                for (int i = 0; i < arenas.Count; i++)
                {
                    int ax = TileMap.Cell(arenas[i].X), ay = TileMap.Cell(arenas[i].Y);
                    targets.Add(new RumorTarget(RumorKind.Arena, i, ax, ay, biomes[ax, ay]));
                }
            }
            return targets;
        }

        // The thing someone tells of on a day: among the targets of the kinds they speak of and not yet known to the
        // listener, one of the Nearest to where they stand, by the seed, who they are and the day. Null when there is
        // nothing left to tell. The wandering merchant is never "known" (they move every day) and never too far.
        public static RumorTarget? Pick(IReadOnlyList<RumorTarget> targets, ulong seed, string speaker, int day, Vec2 from,
            IEnumerable<RumorKind> kinds, Func<RumorTarget, bool> known)
        {
            var allowed = new HashSet<RumorKind>(kinds ?? Enumerable.Empty<RumorKind>());
            if (targets == null || allowed.Count == 0) return null;
            var near = targets.Where(t => allowed.Contains(t.Kind) && t.Kind != RumorKind.Wanderer && (known == null || !known(t)))
                .OrderBy(t => Vec2.Distance(t.Center, from)).ThenBy(t => t.Kind).ThenBy(t => t.Index)
                .Take(Nearest).ToList();
            // Where the wandering merchant is today is news however far.
            if (allowed.Contains(RumorKind.Wanderer)) near.AddRange(targets.Where(t => t.Kind == RumorKind.Wanderer));
            if (near.Count == 0) return null;
            var random = new GameRandom(seed + Hash(speaker) * 31UL + (ulong)Math.Max(0, day) * 7919UL, stream: 60);
            return near[random.Next(near.Count)];
        }

        // A line of the rumour: the speaker's words for its kind with {dir} the way from where they stand ("북동쪽") and
        // {biome} the biome it is in ("버섯 숲").
        public static string Say(string template, RumorTarget target, Vec2 from)
        {
            var at = target.Center;
            string way = Compass(at.X - from.X, at.Y - from.Y);
            return (template ?? "").Replace("{dir}", way == "여기" ? "이 근처" : way + "쪽").Replace("{biome}", BiomeMap.Name(target.Biome));
        }

        // The rumour the person tells today, as a line and what it points at, or null (no rumour lines, or nothing
        // left to tell of the kinds they speak of).
        public static (string Line, RumorTarget Target)? Tell(CharacterDefinition who, IReadOnlyList<RumorTarget> targets, ulong seed, int day, Vec2 from,
            Func<RumorTarget, bool> known)
        {
            if (who == null || who.Rumors.Count == 0) return null;
            var target = Pick(targets, seed, who.Id, day, from, who.Rumors.Select(r => r.Kind), known);
            if (target is not RumorTarget found) return null;
            var lines = who.Rumors.Where(r => r.Kind == found.Kind).ToList();
            var random = new GameRandom(seed + Hash(who.Id) + (ulong)Math.Max(0, day), stream: 62);
            return (Say(lines[random.Next(lines.Count)].Text, found, from), found);
        }

        // A hash of a word that is the same on every machine (string.GetHashCode is not, from run to run).
        public static ulong Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text ?? "")
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        // The kinds characters.csv rumor names, by their word there.
        public static bool TryParseKind(string word, out RumorKind kind)
        {
            switch (word)
            {
                case "camp": kind = RumorKind.Camp; return true;
                case "shrine": kind = RumorKind.Shrine; return true;
                case "chest": kind = RumorKind.Chest; return true;
                case "hidden": kind = RumorKind.Hidden; return true;
                case "vault": kind = RumorKind.Vault; return true;
                case "cart": kind = RumorKind.Cart; return true;
                case "station": kind = RumorKind.Station; return true;
                case "waystone": kind = RumorKind.Waystone; return true;
                case "elite": kind = RumorKind.Elite; return true;
                case "wanderer": kind = RumorKind.Wanderer; return true;
                case "arena": kind = RumorKind.Arena; return true;
                default: kind = RumorKind.Camp; return false;
            }
        }

        // What it is called in a banner ("소문: 야영지").
        public static string Name(RumorKind kind) => kind switch
        {
            RumorKind.Camp => "야영지",
            RumorKind.Shrine => "제단",
            RumorKind.Chest => "보물 상자",
            RumorKind.Hidden => "금 간 벽 너머의 방",
            RumorKind.Vault => "금고",
            RumorKind.Cart => "버려진 수레",
            RumorKind.Station => "끊긴 광차역",
            RumorKind.Waystone => "길잡이 돌",
            RumorKind.Elite => "정예 야영지",
            RumorKind.Wanderer => "떠돌이 상인",
            _ => "보스 아레나",
        };
    }
}
