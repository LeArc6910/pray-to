using System;
using System.Linq;

namespace Game.Core
{
    // ---- Hunting grounds (2026-11-26, Docs/HuntingGrounds.md, the owner: "일부 구역은 아주 많이 지속 스폰되는 사냥터로").
    // Version 8 marks a few rooms of each biome whose monsters keep coming while someone is there (World.Hunt.cs). Only
    // marked: the rooms keep their chunks, so a world made by version 8 is the same tiles as by 7. ----
    public static partial class WorldGenerator
    {
        // The first version with hunting grounds, and monsters by the roads (World.Packs.cs).
        public const int HuntVersion = 8;
        const ulong HuntStream = 50;
        // So many hunting grounds to a biome, at least this wide, at least this many rooms apart.
        public const int HuntPerBiome = 2;
        public const int HuntMinRadius = 9;
        const int HuntGap = 4;

        sealed partial class Furnisher
        {
            // Of each biome's rooms off the roads, past the spawn's quiet rooms, with no town over them, no arena beside them and
            // nothing kept in them (a pack's or plain): the widest, HuntGap rooms apart, HuntPerBiome of them.
            void HuntingGrounds(GameRandom random)
            {
                foreach (var biome in _graph.Nodes.Select(n => n.Biome).Distinct().OrderBy(b => b))
                {
                    var choices = _graph.Nodes.Where(n => n.Biome == biome && n.Kind == RoomKind.Room && !n.Roadside && !_built[n.Index]
                            && n.Depth > NoDangerDepth && n.Radius >= HuntMinRadius && (n.Role == RoomRole.Pack || n.Role == RoomRole.Plain)
                            && !_graph.LairList.Contains(n.Index) && !Neighbours(n).Any(o => o.Kind == RoomKind.Arena))
                        .Select(n => (Node: n, Roll: random.Next(1000)))
                        .OrderByDescending(c => c.Node.Radius).ThenBy(c => c.Roll).Select(c => c.Node).ToList();
                    int taken = 0;
                    foreach (var node in choices)
                    {
                        if (taken >= HuntPerBiome) break;
                        if (Near(node, HuntGap, n => _graph.HuntList.Contains(n.Index))) continue;
                        _graph.HuntList.Add(node.Index);
                        taken++;
                    }
                }
            }
        }
    }
}
