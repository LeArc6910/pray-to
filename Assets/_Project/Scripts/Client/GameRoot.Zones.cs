using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The levels a spot is for (2026-11-27, Docs/ZoneLevels.md): by the place's name under the minimap, in the banner
    // of a new area (a biome, a zone, a hunting ground), coloured by how far short of them the player is.
    public sealed partial class GameRoot
    {
        const int NoArea = int.MinValue;
        int _areaHere = NoArea, _areaAnnounced = NoArea;

        int ZoneHere(Vec2 at) => Zones.ZoneAt(_client.Spawn, at, WorldGenerator.ScaleOf(_client.Map.Width, _client.Map.Height));

        RoomNode HuntingGroundAt(Vec2 at)
        {
            var rooms = _client.Rooms;
            if (rooms == null) return null;
            foreach (int index in rooms.HuntingGrounds)
                if (Vec2.Distance(rooms.Nodes[index].Center, at) <= rooms.Nodes[index].Radius + 1) return rooms.Nodes[index];
            return null;
        }

        int AreaKey(Vec2 at, Biome biome) => (int)biome * 10000 + ZoneHere(at) * 10 + (HuntingGroundAt(at) != null ? 1 : 0);

        (int Low, int High) RangeHere(Vec2 at) =>
            _client.DungeonHere is DungeonInstance dungeon ? Zones.LevelRange(dungeon.Level) : Zones.LevelRange(_client.Biomes.At(at), ZoneHere(at));

        // "얼음 동굴 · Lv 15~19", "사냥터 · 흙 동굴 · Lv 3~7", and a warning when the player is short of it.
        string AreaBanner(Vec2 at, Biome biome)
        {
            var range = RangeHere(at);
            string text = $"{(HuntingGroundAt(at) != null ? "사냥터 · " : "")}{BiomeMap.Name(biome)} · {Zones.Text(range)}";
            int danger = Zones.Danger(_client.Level, range);
            return danger == 2 ? text + " · 매우 위험" : danger == 1 ? text + " · 위험" : text;
        }

        internal static Color DangerColor(int danger) => danger switch
        {
            1 => new Color(1f, 0.66f, 0.28f),
            2 => new Color(1f, 0.36f, 0.3f),
            _ => new Color(0.92f, 0.9f, 0.85f),
        };
    }
}
