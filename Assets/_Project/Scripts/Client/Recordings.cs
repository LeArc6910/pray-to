using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // What the ground underfoot sounds like (Recordings.StepName).
    enum Ground
    {
        Dirt,
        Stone,
        Rock,
        Wood,
        Sand,
        Snow,
        Ice,
        Leaves,
        Metal,
        Water,
        Carpet,
    }

    // Recorded sound effects (2026-11-11, the owner: "steps too loud and not like walking; a sound for each floor; free
    // public sounds may be used, and the other sounds too"): CC0 recordings in Resources/Sfx, each a set of takes
    // named name_1, name_2 and so on, played in place of the sounds made in code (Art/SoundArt.cs) where there is a
    // set. Where each came from is in Docs/SoundCredits.md. Settings.RecordedSounds turns them off to compare.
    static class Recordings
    {
        const string Folder = "Sfx";
        static Dictionary<string, List<AudioClip>> _sets;
        static readonly Dictionary<string, int> Last = new Dictionary<string, int>();

        // One take of the set, not the one played last time; null when there is no such set.
        public static AudioClip Pick(string name)
        {
            if (name == null) return null;
            Load();
            if (!_sets.TryGetValue(name, out var takes) || takes.Count == 0) return null;
            int last = Last.TryGetValue(name, out int l) ? l : -1;
            int pick = Random.Range(0, takes.Count);
            if (takes.Count > 1 && pick == last) pick = (pick + 1) % takes.Count;
            Last[name] = pick;
            return takes[pick];
        }

        // How many sets were found (Playtest: none means the recordings did not make it into the build).
        public static int SetCount
        {
            get
            {
                Load();
                return _sets.Count;
            }
        }

        static void Load()
        {
            if (_sets != null) return;
            _sets = new Dictionary<string, List<AudioClip>>();
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder))
            {
                int cut = clip.name.LastIndexOf('_');
                string set = cut > 0 && int.TryParse(clip.name.Substring(cut + 1), out _) ? clip.name.Substring(0, cut) : clip.name;
                if (!_sets.TryGetValue(set, out var takes)) _sets[set] = takes = new List<AudioClip>();
                takes.Add(clip);
            }
            foreach (var takes in _sets.Values) takes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        // The recorded set for a sound made in code, or null for one that keeps its own.
        public static string NameOf(Sound sound) => sound switch
        {
            Sound.Dig => "sfx_pickaxe",
            Sound.Break => "sfx_rockbreak",
            Sound.Swing => "sfx_swing",
            Sound.Hit => "sfx_hit",
            Sound.Pickup => "sfx_pickup",
            Sound.Click => "sfx_ui_click",
            Sound.Door => "sfx_door_open",
            Sound.DoorShut => "sfx_door_close",
            Sound.Splash or Sound.Cast => "sfx_splash",
            _ => null,
        };

        // How loud a recording plays against the made-in-code sound it stands in for (the recordings peak higher).
        public static float GainOf(Sound sound) => sound switch
        {
            Sound.Click => 0.5f,
            Sound.Pickup => 0.6f,
            Sound.Swing or Sound.Hit => 0.75f,
            _ => 0.7f,
        };

        public static string StepName(Ground ground) => ground switch
        {
            Ground.Dirt => "footstep_dirt",
            // The heel-and-toe takes: the first stone and wood sets were short dull taps (Docs/SoundCredits.md).
            Ground.Stone => "footstep_stone_alt",
            Ground.Wood => "footstep_wood_alt",
            Ground.Rock => "footstep_rock",
            Ground.Sand => "footstep_sand",
            Ground.Snow or Ground.Ice => "footstep_snow",
            Ground.Leaves => "footstep_leaves",
            Ground.Metal => "footstep_metal",
            Ground.Water => "footstep_water",
            Ground.Carpet => "footstep_carpet",
            _ => "footstep_dirt",
        };

        // Evening out how loud the sets are (their peaks are alike, their weight is not): sand is soft, wood heavy.
        public static float StepGain(Ground ground) => ground switch
        {
            Ground.Sand => 1.4f,
            Ground.Wood => 0.75f,
            Ground.Metal => 0.7f,
            Ground.Carpet => 1.2f,
            _ => 1f,
        };

        // The ground at a tile: what is laid on it first, then the floor of its biome.
        public static Ground GroundAt(TileMap map, BiomeMap biomes, int x, int y)
        {
            var type = map[x, y];
            if (type == TileType.RedCarpet || type == TileType.BlueCarpet || type == TileType.YellowCarpet) return Ground.Carpet;
            if (type == TileType.Dock) return Ground.Wood;
            if (type == TileType.Rail || World.IsBelt(type) || type == TileType.Splitter || type == TileType.Filter) return Ground.Metal;
            if (type == TileType.Road || type == TileType.Waypoint || TileMap.IsPaving(type)) return Ground.Stone;
            if (TileMap.IsSoil(type)) return Ground.Dirt;
            return biomes[x, y] switch
            {
                Biome.Mushroom => Ground.Leaves,
                Biome.Ice => Ground.Snow,
                Biome.Sand => Ground.Sand,
                Biome.Crystal or Biome.Lava or Biome.Abyss => Ground.Rock,
                Biome.Lake => Ground.Water,
                _ => Ground.Dirt,
            };
        }
    }
}
