using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // How long starting a world takes (Docs/TitleRework.md 4-4, T1): each stage of WorldGenerator.Steps on the game's map,
    // the longest one (what a frame of the loading screen waits for at worst), and a solo start as it was (the host and
    // the player's own client each made the map) against now (the client takes a copy of the host's).
    static class LoadTime
    {
        public static void Run(BotSheets sheets, IEnumerable<ulong> seeds, int version, int size)
        {
            Console.WriteLine($"generator {version}, map {size}x{size}");
            var totals = new Dictionary<string, double>();
            var longest = new Dictionary<string, double>();
            var before = new List<double>();
            var after = new List<double>();
            foreach (ulong seed in seeds)
            {
                // The stages one by one.
                var clock = Stopwatch.StartNew();
                string key = "";
                GeneratedWorld made = null;
                var line = new List<string>();
                foreach (var stage in WorldGenerator.Steps(seed, size, size, sheets.Story, sheets.Bosses.Arenas, version, sheets.Tiles.Terrain))
                {
                    double ms = clock.Elapsed.TotalMilliseconds;
                    if (key.Length > 0)
                    {
                        totals[key] = (totals.TryGetValue(key, out double t) ? t : 0) + ms;
                        longest[key] = Math.Max(longest.TryGetValue(key, out double l) ? l : 0, ms);
                        line.Add($"{key} {ms:0}");
                    }
                    key = stage.Key;
                    made = stage.World ?? made;
                    clock.Restart();
                }
                Console.WriteLine($"seed {seed}: " + string.Join(", ", line) + " ms");

                // A solo start as before: the host makes the world, the client makes it again on Welcome.
                clock.Restart();
                var host = new WorldHost(seed, sheets.Monsters, sheets.Loot, new HostConfig { MapSize = size, MapVersion = version },
                    bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles, guide: sheets.Guide, npcs: sheets.Npcs, story: sheets.Story);
                var client = new WorldClient("a", "봇", sheets.Monsters, sheets.Bosses, sheets.Loot, sheets.Classes, null, sheets.Tiles) { Story = sheets.Story };
                host.Receive(1, client.JoinMessage());
                foreach (var outgoing in host.TakeOutgoing()) client.Receive(outgoing.Data);
                before.Add(clock.Elapsed.TotalMilliseconds);
                if (client.Status != ClientStatus.Playing) throw new InvalidOperationException("The client did not start.");

                // Now: made once, the host built on it, the client on a copy of the map as made.
                clock.Restart();
                var world = WorldGenerator.Generate(seed, size, size, sheets.Story, sheets.Bosses.Arenas, version, sheets.Tiles.Terrain);
                var copy = world.Copy();
                host = new WorldHost(seed, sheets.Monsters, sheets.Loot, new HostConfig { MapSize = size, MapVersion = version },
                    bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles, guide: sheets.Guide, npcs: sheets.Npcs, story: sheets.Story, generated: world);
                client = new WorldClient("a", "봇", sheets.Monsters, sheets.Bosses, sheets.Loot, sheets.Classes, null, sheets.Tiles) { Story = sheets.Story, Made = copy };
                host.Receive(1, client.JoinMessage());
                foreach (var outgoing in host.TakeOutgoing()) client.Receive(outgoing.Data);
                after.Add(clock.Elapsed.TotalMilliseconds);
                if (client.Status != ClientStatus.Playing || !client.MapReused) throw new InvalidOperationException("The client did not take the host's map.");
            }
            int count = Math.Max(1, before.Count);
            Console.WriteLine();
            Console.WriteLine("stage      mean ms  longest ms  share");
            double sum = totals.Values.Sum();
            foreach (var pair in totals)
                Console.WriteLine($"{pair.Key,-10} {pair.Value / count,7:0} {longest[pair.Key],11:0}  {pair.Value / sum,5:P0}");
            Console.WriteLine($"generation {sum / count:0} ms a world");
            Console.WriteLine($"solo start before (made twice): {before.Average():0} ms, now (made once): {after.Average():0} ms");
        }
    }
}
