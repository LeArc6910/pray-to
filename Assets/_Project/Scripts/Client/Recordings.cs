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
        // Laid bricks, an iron grate, and water deep enough to splash through (2026-11-27).
        Brick,
        Grate,
        Splash,
    }

    // What a spell's burst is made of, for its sound (Recordings.NovaName).
    enum SpellElement
    {
        None,
        Fire,
        Ice,
        Lightning,
        Poison,
        Void,
        Water,
        Spirit,
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

        // The recorded set for a sound made in code, or null for one that keeps its own (2026-11-26: the fight, the
        // roll, the elements, the UI, chests and coins, water, crafting and the jingles; Docs/SoundCredits.md).
        public static string NameOf(Sound sound) => sound switch
        {
            Sound.Dig => "sfx_pickaxe",
            Sound.Break => "sfx_rockbreak",
            Sound.Swing => "sfx_swing",
            Sound.Hit => "sfx_hit",
            Sound.Crit => "sfx_crit",
            Sound.Hurt => "sfx_hurt",
            Sound.Thump => "sfx_thump",
            Sound.Pickup => "sfx_pickup",
            Sound.Click => "sfx_ui_click",
            Sound.Door => "sfx_door_open",
            Sound.DoorShut => "sfx_door_close",
            Sound.Splash or Sound.Cast => "sfx_splash",
            // A boss's wind-up and a finisher's warning: a gong; a monster calling for help: a bell.
            Sound.Warning => "sfx_gong",
            Sound.MobCall or Sound.Chime => "sfx_bell",
            Sound.Roar => "sfx_roar",
            // A player's groan when struck, by the character's gender (2026-11-28; sets to come, Docs/SoundCredits.md 5차).
            Sound.GruntMale => "voice_player_male_hurt",
            Sound.GruntFemale => "voice_player_female_hurt",
            Sound.Roll => "sfx_roll",
            Sound.Dodge or Sound.Dash => "sfx_whoosh",
            // The elements: fire, frost, lightning; ice and crystal breaking; a spell's burst.
            Sound.Boom => "sfx_fire",
            Sound.Ice => "sfx_ice",
            Sound.Zap => "sfx_zap",
            Sound.Shatter => "sfx_shatter",
            Sound.Nova => "sfx_spell",
            Sound.Plop => "sfx_plop",
            Sound.Sizzle => "sfx_sizzle",
            Sound.Water => "sfx_bubble",
            Sound.Hammer or Sound.Clang => "sfx_anvil",
            Sound.Craft => "sfx_craft",
            Sound.Place => "sfx_place",
            Sound.Harvest => "sfx_harvest",
            Sound.Eat => "sfx_eat",
            Sound.Buy => "sfx_coin_buy",
            Sound.Chest => "sfx_chest_open",
            Sound.Victory => "jingle_victory",
            Sound.LevelUp => "jingle_levelup",
            Sound.Learn => "jingle_learn",
            Sound.Story => "jingle_story",
            Sound.Down => "jingle_down",
            Sound.UiOpen => "sfx_ui_open",
            Sound.UiClose => "sfx_ui_close",
            Sound.UiHover => "sfx_ui_hover",
            Sound.UiConfirm => "sfx_ui_confirm",
            Sound.UiCancel => "sfx_ui_cancel",
            Sound.UiFail => "sfx_ui_fail",
            _ => null,
        };

        // How loud a recording plays against the made-in-code sound it stands in for (the recordings peak higher).
        public static float GainOf(Sound sound) => sound switch
        {
            Sound.Click => 0.5f,
            Sound.Pickup => 0.6f,
            Sound.Swing or Sound.Hit => 0.75f,
            Sound.UiHover => 0.4f,
            Sound.UiOpen or Sound.UiClose or Sound.UiConfirm or Sound.UiCancel or Sound.UiFail => 0.55f,
            Sound.Victory or Sound.LevelUp or Sound.Learn or Sound.Story or Sound.Down => 0.6f,
            Sound.Crit or Sound.Roar or Sound.Warning => 0.85f,
            _ => 0.7f,
        };

        // A skill's recorded set by its element (fire, frost, lightning), or null for one that keeps its own sound.
        public static string SkillName(string id) => id switch
        {
            "fireball" or "flame_brand" or "fire_ring" or "meteor" or "flame_burst" or "inferno" or "explosive_arrow" => "sfx_fire",
            "frost_ring" or "ice_lance" or "blizzard" => "sfx_ice",
            "chain_lightning" or "thunderclap" or "storm_mantle" => "sfx_zap",
            _ => null,
        };

        // A spell's burst (Sound.Nova) by its element (2026-11-28, the owner: "a fire spell's burst should sound like
        // fire burning"): the element's own set; poison, the void, water and spirits have sets of their own to come
        // (Docs/SoundCredits.md 5차), and until then, like a burst of no element, the old spell burst.
        public static string NovaName(SpellElement element) => element switch
        {
            SpellElement.Fire => "sfx_fire",
            SpellElement.Ice => "sfx_ice",
            SpellElement.Lightning => "sfx_zap",
            SpellElement.Poison => "sfx_spell_poison",
            SpellElement.Void => "sfx_spell_void",
            SpellElement.Water => "sfx_spell_water",
            SpellElement.Spirit => "sfx_spell_spirit",
            _ => null,
        };

        // A boss's or a shot's element as a spell's.
        public static SpellElement SpellOf(ShotElement element) => element switch
        {
            ShotElement.Fire or ShotElement.Lava or ShotElement.Ember => SpellElement.Fire,
            ShotElement.Frost => SpellElement.Ice,
            ShotElement.Spore => SpellElement.Poison,
            ShotElement.Void or ShotElement.Hex => SpellElement.Void,
            ShotElement.Water => SpellElement.Water,
            ShotElement.Wraith or ShotElement.Mote => SpellElement.Spirit,
            _ => SpellElement.None,
        };

        public static string RoarName(string boss) => boss == null ? null : "roar_" + boss;

        // A harvest by what was picked (2026-11-28, the owner: "a different sound for each thing harvested"): the
        // biomes' harvest props when picked or broken (Docs/Harvest.md) and the field's crops, in ten kinds by how
        // they come off. Sets to come (Docs/SoundCredits.md 5차); until then the old harvest sound, or for what breaks
        // (a geode, a rock of ore, a bed of clams) the rock breaking.
        public static string HarvestName(TileType grown) => grown switch
        {
            TileType.MushroomPatch => "sfx_harvest_mushroom",
            TileType.HerbBush or TileType.PepperBush => "sfx_harvest_bush",
            TileType.VoidPod => "sfx_harvest_pod",
            TileType.FrostGeode => "sfx_harvest_geode",
            TileType.MithrilRock or TileType.AmberRock => "sfx_harvest_ore",
            TileType.ClamBed => "sfx_harvest_clam",
            _ => null,
        };

        // Broken rather than picked: it falls back on the rock breaking.
        public static bool HarvestBreaks(TileType grown) =>
            grown == TileType.FrostGeode || grown == TileType.MithrilRock || grown == TileType.AmberRock || grown == TileType.ClamBed;

        public static string HarvestName(string crop) => crop switch
        {
            "mushroom" or "glow_cap" => "sfx_harvest_mushroom",
            "fire_pepper" => "sfx_harvest_bush",
            "cave_berry" or "frost_berry" or "moon_grape" or "cactus_fruit" => "sfx_harvest_berry",
            "cave_potato" or "crystal_radish" or "lotus_root" => "sfx_harvest_root",
            "snow_cabbage" or "ember_corn" => "sfx_harvest_leaf",
            "void_bean" => "sfx_harvest_pod",
            "star_melon" => "sfx_harvest_melon",
            _ => null,
        };

        // What a boss's magic is made of (ShotArt.ElementOf names every other boss ember, which is no spell of theirs).
        public static SpellElement SpellOfBoss(string boss) => boss switch
        {
            "lava_titan" or "mad_general" => SpellElement.Fire,
            "void_lord" => SpellElement.Void,
            "tide_serpent" => SpellElement.Water,
            "crown_wraith" => SpellElement.Spirit,
            _ => SpellElement.None,
        };

        // What a monster's attack is made of, as a spell's (its nova's sound).
        public static SpellElement SpellOf(MobLook look) => look switch
        {
            MobLook.Ember => SpellElement.Fire,
            MobLook.Frost => SpellElement.Ice,
            MobLook.Spore => SpellElement.Poison,
            MobLook.Shadow or MobLook.Hex => SpellElement.Void,
            MobLook.Water => SpellElement.Water,
            _ => SpellElement.None,
        };

        // A monster's recorded voice: voice_<kind>_<cry|attack|hurt>; the kinds without one keep the made-in-code voice.
        public static string VoiceName(CreatureVoice creature, Voice voice) =>
            "voice_" + creature.ToString().ToLowerInvariant() + "_" + voice.ToString().ToLowerInvariant();

        // The recorded music (2026-11-26, Resources/Music, CC0 loops): the title, the village, the eight biomes, a
        // dungeon, a boss. A field's fight and night play its one recording; the rest keep the music made in code.
        public static string MusicName(Song song)
        {
            if (song.Boss != null) return "music_boss";
            if (song.Scene != null) return "music_" + song.Scene;
            // Every place has one (2026-11-27: the kingdom, the hut, the nest, the harbour, the ruins and the mine too).
            return "music_" + song.Tune.ToString().ToLowerInvariant();
        }

        public static string AmbienceName(Ambience ambience) => ambience switch
        {
            Ambience.Lakeside => "amb_lakeside",
            Ambience.Waves => "amb_waves",
            Ambience.Lava or Ambience.Forge => "amb_lava",
            _ => null,
        };

        static readonly Dictionary<string, AudioClip> Loops = new Dictionary<string, AudioClip>();

        // A long recorded loop (Music/ or Ambience/), loaded when first wanted; null when there is none.
        public static AudioClip Loop(string folder, string name)
        {
            if (name == null) return null;
            string path = folder + "/" + name;
            // Remembered also when there is none, so a song without one is not looked for every frame.
            if (Loops.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);
            if (clip != null) clip.LoadAudioData();
            Loops[path] = clip;
            return clip;
        }

        public static bool IsLoop(AudioClip clip) => clip != null && Loops.ContainsValue(clip);

        // A loop no longer playing is let go, so the music of every place visited does not pile up in memory.
        public static void Release(AudioClip clip)
        {
            string key = null;
            foreach (var pair in Loops)
            {
                if (pair.Value == clip) key = pair.Key;
            }
            if (key == null) return;
            Loops.Remove(key);
            Resources.UnloadAsset(clip);
        }

        public static string StepName(Ground ground) => ground switch
        {
            Ground.Dirt => "footstep_dirt",
            // The heel-and-toe takes: the first stone and wood sets were short dull taps (Docs/SoundCredits.md).
            Ground.Stone => "footstep_stone_alt",
            Ground.Wood => "footstep_wood_alt",
            Ground.Rock => "footstep_rock",
            Ground.Sand => "footstep_sand",
            Ground.Snow => "footstep_snow",
            Ground.Ice => "footstep_ice",
            Ground.Brick => "footstep_brick",
            Ground.Grate => "footstep_grate",
            Ground.Splash => "footstep_splash",
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
            Ground.Grate => 0.7f,
            _ => 1f,
        };

        // The ground at a tile: what is laid on it first, then the floor of its biome.
        public static Ground GroundAt(TileMap map, BiomeMap biomes, int x, int y)
        {
            var type = map[x, y];
            if (type == TileType.RedCarpet || type == TileType.BlueCarpet || type == TileType.YellowCarpet) return Ground.Carpet;
            if (type == TileType.Dock) return Ground.Wood;
            if (type == TileType.BrickFloor) return Ground.Brick;
            if (type == TileType.Grate) return Ground.Grate;
            if (type == TileType.Rail || World.IsBelt(type) || type == TileType.Splitter || type == TileType.Filter) return Ground.Metal;
            if (type == TileType.Road || type == TileType.Waypoint || TileMap.IsPaving(type)) return Ground.Stone;
            if (TileMap.IsSoil(type)) return Ground.Dirt;
            if (type == TileType.IceFloor) return Ground.Ice;
            if (type == TileType.Quicksand) return Ground.Sand;
            // The lake's shallows splash underfoot (B3b).
            if (type == TileType.ShallowWater) return Ground.Splash;
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
