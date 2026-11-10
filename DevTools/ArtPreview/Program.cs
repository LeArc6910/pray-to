using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // Draws every sprite to sheet.png and two made-up scenes (the spawn with a small base, the boss arena) to
    // cave.png and arena.png, laid out and lit the way WorldView lays them out and lights them; cave_unlit.png is
    // the first without the dark. Draws every frame of the pixel effects, tinted as the game tints them, to
    // effects.png. Writes every sound effect to sounds/<name>.wav. Fails if any drawing is malformed, an effect frame
    // is empty or spills out of its circle, or a sound comes out silent, too long or out of range.
    //
    //   dotnet run --project DevTools/ArtPreview [-- <output folder>]
    static class Program
    {
        const int Px = TileArt.Size;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F");

        static int Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
            Directory.CreateDirectory(output);
            var loot = LoadLoot();
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters")), MonsterSheet.File)));
            string world = FindFolder(Path.Combine("Assets", "_Project", "Data", "World"));
            var tiles = TileSheet.Parse(
                File.ReadAllText(Path.Combine(world, TileSheet.File)),
                loot,
                File.ReadAllText(Path.Combine(world, TileSheet.CropsFile)),
                File.ReadAllText(Path.Combine(world, TileSheet.MachinesFile)));
            string npcFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Npcs"));
            var npcs = NpcSheet.Parse(File.ReadAllText(Path.Combine(npcFolder, NpcSheet.NpcsFile)), File.ReadAllText(Path.Combine(npcFolder, NpcSheet.ShopFile)), loot,
                File.ReadAllText(Path.Combine(npcFolder, NpcSheet.RequestsFile)));
            string storyFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Story"));
            var story = StorySheet.Parse(File.ReadAllText(Path.Combine(storyFolder, StorySheet.PlacesFile)), File.ReadAllText(Path.Combine(storyFolder, StorySheet.CharactersFile)),
                File.ReadAllText(Path.Combine(storyFolder, StorySheet.StoryFile)), loot);
            if (!loot.Success || !monsters.Success || !tiles.Success || !npcs.Success || !story.Success)
            {
                foreach (string error in loot.Errors.Concat(monsters.Errors).Concat(tiles.Errors).Concat(npcs.Errors).Concat(story.Errors)) Console.Error.WriteLine(error);
                return 1;
            }

            var lights = new WorldLights(tiles);
            // How long the game takes to draw every ground tile when it builds its atlas (Sprites).
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < TileArt.GroundCount; i++) TileArt.Ground(i);
            Console.WriteLine($"{TileArt.GroundCount} ground tiles in {clock.ElapsedMilliseconds} ms");
            Save(Sheet(loot, monsters, npcs), Path.Combine(output, "sheet.png"), 4);
            // The newest pieces one by one, for a closer look (2026-11-09).
            var pieces = Directory.CreateDirectory(Path.Combine(output, "pieces")).FullName;
            Save(TileArt.BigSmelter, Path.Combine(pieces, "big_smelter.png"), 6);
            Save(TileArt.BigAssembler, Path.Combine(pieces, "big_assembler.png"), 6);
            Save(TileArt.Object(TileType.Scarecrow), Path.Combine(pieces, "scarecrow.png"), 6);
            Save(TileArt.Object(TileType.LockedDoor), Path.Combine(pieces, "locked_door.png"), 6);
            // The story (2026-11-10): its people in their looks, the road and the waypoint, and each place as the game
            // lays it out and lights it.
            var looks = PeopleArt.Looks.Select(look => PeopleArt.Look(look, Rgba.Hex(story.Characters.FirstOrDefault(c => c.Look == look)?.Color ?? "#8A7A6A"), 0)).ToList();
            Save(Row(looks), Path.Combine(pieces, "story_people.png"), 6);
            Save(Row(new[] { TileArt.Object(TileType.Road), TileArt.Object(TileType.Waypoint) }.ToList()), Path.Combine(pieces, "road_waypoint.png"), 6);
            // Every structure of the places, standing on its feet, and castle wall blocks joined as on a wall.
            Save(Row(PropArt.All.Select(type => PropArt.Prop(type)).ToList()), Path.Combine(pieces, "structures.png"), 4);
            Save(Row(new[] { 0, 2 | 8, 1 | 4, 1 | 2, 15 }.Select(sides => PropArt.Prop(TileType.CastleWall, sides)).ToList()), Path.Combine(pieces, "castle_walls.png"), 6);
            var storyWorld = WorldGenerator.Generate(7, story: story);
            foreach (var site in storyWorld.Places) Save(Place(storyWorld, site, lights), Path.Combine(pieces, $"place_{site.Definition.Id}.png"), 3);
            // A road between places with its lamps, signposts and rest spot (2026-11-11).
            Save(RoadView(storyWorld, lights), Path.Combine(pieces, "road.png"), 3);
            Save(Players(), Path.Combine(output, "player.png"), 6);
            Save(Bosses(), Path.Combine(output, "bosses.png"), 3);
            Motions.Write(Path.Combine(output, "anim"), loot, monsters, npcs);
            Save(Cave(loot, monsters, lights, dark: true), Path.Combine(output, "cave.png"), 3);
            Save(Cave(loot, monsters, lights, dark: false), Path.Combine(output, "cave_unlit.png"), 3);
            Save(Arena(lights), Path.Combine(output, "arena.png"), 3);
            string classFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var classes = ClassSheet.Parse(File.ReadAllText(Path.Combine(classFolder, ClassSheet.ClassesFile)), File.ReadAllText(Path.Combine(classFolder, ClassSheet.SkillsFile)));
            if (!classes.Success)
            {
                foreach (string error in classes.Errors) Console.Error.WriteLine(error);
                return 1;
            }
            Save(Skills(classes, out bool skillsFine), Path.Combine(output, "skills.png"), 6);
            if (!skillsFine) return 1;
            var effects = Effects(out bool effectsFine);
            Save(effects, Path.Combine(output, "effects.png"), 3);
            if (!effectsFine) return 1;
            if (!WriteSounds(Path.Combine(output, "sounds"))) return 1;
            if (!WriteSkillSounds(Path.Combine(output, "sounds", "skills"), classes)) return 1;
            if (!WriteMusic(Path.Combine(output, "music"))) return 1;
            Console.WriteLine($"Wrote sheet.png, player.png, bosses.png, anim/, cave.png, cave_unlit.png, arena.png, skills.png, effects.png, sounds/ and music/ to {output}");
            return 0;
        }

        static readonly string[] BossIds = { "cave_warden", "crystal_queen", "lava_titan", "void_lord", "tide_serpent", "antlion", "mad_general", "crown_wraith" };

        static bool WriteSounds(string folder)
        {
            Directory.CreateDirectory(folder);
            bool fine = true;
            foreach (Sound sound in Enum.GetValues(typeof(Sound)))
            {
                var samples = SoundArt.Make(sound);
                float seconds = samples.Length / (float)SoundArt.SampleRate;
                float loudest = samples.Max(Math.Abs);
                if (samples.Length == 0 || seconds > 2 || loudest < 0.05f || loudest > 1 || samples.Any(float.IsNaN))
                {
                    Console.Error.WriteLine($"{sound}: {seconds:0.00}s, peak {loudest:0.00}");
                    fine = false;
                }
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, sound.ToString().ToLowerInvariant() + ".wav"));
            }
            // Each kind of monster voice (2026-11-09) to creatures/<kind>_<voice>.wav.
            string creatures = Path.Combine(folder, "creatures");
            Directory.CreateDirectory(creatures);
            foreach (CreatureVoice creature in Enum.GetValues(typeof(CreatureVoice)))
            {
                foreach (Voice voice in Enum.GetValues(typeof(Voice)))
                {
                    var samples = SoundArt.Creature(creature, voice);
                    float seconds = samples.Length / (float)SoundArt.SampleRate;
                    float loudest = samples.Max(Math.Abs);
                    if (samples.Length == 0 || seconds > 1 || loudest < 0.05f || loudest > 1 || samples.Any(float.IsNaN))
                    {
                        Console.Error.WriteLine($"{creature} {voice}: {seconds:0.00}s, peak {loudest:0.00}");
                        fine = false;
                    }
                    Wav.Save(samples, SoundArt.SampleRate, Path.Combine(creatures, $"{creature}_{voice}".ToLowerInvariant() + ".wav"));
                }
            }
            return fine;
        }

        // Every skill's own sound to sounds/skills/<id>.wav (2026-11-09). Each shipped skill must have one, in range,
        // and no two may be the same.
        static bool WriteSkillSounds(string folder, ClassData classes)
        {
            Directory.CreateDirectory(folder);
            bool fine = true;
            var seen = new Dictionary<string, string>();
            foreach (var skill in classes.Classes.SelectMany(c => c.Skills))
            {
                var samples = SoundArt.Skill(skill.Id);
                if (samples == null)
                {
                    Console.Error.WriteLine($"skill {skill.Id}: no sound of its own");
                    fine = false;
                    continue;
                }
                float seconds = samples.Length / (float)SoundArt.SampleRate;
                float loudest = samples.Max(Math.Abs);
                if (seconds > 2 || loudest < 0.05f || loudest > 1 || samples.Any(float.IsNaN))
                {
                    Console.Error.WriteLine($"skill {skill.Id}: {seconds:0.00}s, peak {loudest:0.00}");
                    fine = false;
                }
                string print = $"{samples.Length}:{samples.Sum(Math.Abs):0.000}";
                if (seen.TryGetValue(print, out string twin))
                {
                    Console.Error.WriteLine($"skill {skill.Id} sounds the same as {twin}");
                    fine = false;
                }
                seen[print] = skill.Id;
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, skill.Id + ".wav"));
            }
            return fine;
        }

        // Every tune and ambience loop to music/<name>.wav. Each must be a real loop (a few seconds at least), in
        // range, and must run on from its end into its start without a jump.
        static bool WriteMusic(string folder)
        {
            Directory.CreateDirectory(folder);
            bool fine = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            // Every field's tune calm and in a fight, every boss's theme at each phase, and the ambience.
            var songs = ((Tune[])Enum.GetValues(typeof(Tune))).Where(t => t != Tune.Boss)
                .SelectMany(t => new[] { Song.Field(t), Song.Field(t, battle: true) })
                .Concat(MusicArt.PlaceTunes.Select(t => Song.Field(t, night: true)))
                .Concat(BossIds.SelectMany(id => Enumerable.Range(1, MusicArt.BossPhases).Select(phase => Song.Of(id, phase))))
                .ToList();
            var loops = songs.Select(song => (song.Key, MusicArt.Make(song)))
                .Concat(((Ambience[])Enum.GetValues(typeof(Ambience))).Select(a => ("ambience_" + a, MusicArt.Make(a))))
                .ToList();
            // A field's two versions, and a boss's first two phases, must line up sample for sample (Music plays one
            // on from the other's place).
            foreach (var a in songs)
            {
                foreach (var b in songs)
                {
                    if (string.CompareOrdinal(a.Key, b.Key) >= 0 || !a.InStepWith(b)) continue;
                    int left = loops.First(l => l.Item1 == a.Key).Item2.Length, right = loops.First(l => l.Item1 == b.Key).Item2.Length;
                    if (left == right) continue;
                    Console.Error.WriteLine($"{a.Key} and {b.Key} do not line up: {left} and {right} samples");
                    fine = false;
                }
            }
            Console.WriteLine($"{loops.Count} music and ambience loops in {clock.ElapsedMilliseconds} ms");
            foreach (var (name, samples) in loops)
            {
                float seconds = samples.Length / (float)SoundArt.SampleRate;
                float loudest = samples.Max(Math.Abs);
                float seam = Math.Abs(samples[0] - samples[samples.Length - 1]);
                // The largest step between neighbouring samples inside the loop; the seam may not jump much more.
                float step = 0;
                for (int i = 1; i < samples.Length; i++) step = Math.Max(step, Math.Abs(samples[i] - samples[i - 1]));
                if (seconds < 1.5f || seconds > 40 || loudest < 0.05f || loudest > 1 || samples.Any(float.IsNaN) || seam > step * 1.5f + 0.01f)
                {
                    Console.Error.WriteLine($"{name}: {seconds:0.00}s, peak {loudest:0.00}, seam {seam:0.000} (largest step {step:0.000})");
                    fine = false;
                }
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, name.ToLowerInvariant() + ".wav"));
            }
            return fine;
        }

        static void Save(PixelImage image, string path, int scale) => Png.Save(Scaled(image, scale), path);

        // ---- Skill icons ----

        // Each class's skills in a row, in sheet order. Fails when a shipped skill has no picture of its own (it would
        // show its kind's) or two skills look the same.
        static PixelImage Skills(ClassData classes, out bool fine)
        {
            fine = true;
            const int gap = 2;
            int columns = classes.Classes.Max(c => c.Skills.Count);
            var sheet = new PixelImage(gap + columns * (SkillArt.Size + gap), gap + classes.Classes.Count * (SkillArt.Size + gap));
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Backdrop);
            var seen = new Dictionary<string, string>();
            for (int row = 0; row < classes.Classes.Count; row++)
            {
                var skills = classes.Classes[row].Skills;
                for (int column = 0; column < skills.Count; column++)
                {
                    var skill = skills[column];
                    var icon = SkillArt.Icon(skill);
                    if (!SkillArt.HasOwnPicture(skill))
                    {
                        Console.Error.WriteLine($"skill {skill.Id}: no picture of its own in SkillArt");
                        fine = false;
                    }
                    string key = string.Concat(Enumerable.Range(0, icon.Width * icon.Height).Select(i => icon[i % icon.Width, i / icon.Width].R.ToString("X2")));
                    if (seen.TryGetValue(key, out string same))
                    {
                        Console.Error.WriteLine($"skill {skill.Id}: looks the same as {same}");
                        fine = false;
                    }
                    seen[key] = skill.Id;
                    sheet.Draw(icon, gap + column * (SkillArt.Size + gap), gap + row * (SkillArt.Size + gap));
                }
            }
            return sheet;
        }

        // ---- Effects ----

        // Each effect's frames in a row, small and large, tinted in a colour the game uses for it.
        static PixelImage Effects(out bool fine)
        {
            fine = true;
            var rows = new List<(string Name, int Diameter, Rgba Tint, int Frames, Func<int, int, PixelImage> Draw)>
            {
                ("swing", EffectArt.Diameter(0.8f + 2 * 0.75f), Rgba.White, EffectArt.SwingFrames, EffectArt.Swing),
                ("burst (fire)", EffectArt.Diameter(2 * 1.6f), Rgba.Hex("#FF8A3C"), EffectArt.Frames, EffectArt.Burst),
                ("burst (dust)", EffectArt.Diameter(2 * 0.35f), Rgba.Hex("#B8A890"), EffectArt.Frames, EffectArt.Burst),
                ("ring (frost)", EffectArt.Diameter(2 * 2.8f), Rgba.Hex("#8CD8FF"), EffectArt.Frames, EffectArt.Ring),
                ("vortex (blade storm)", EffectArt.Diameter(2 * 3.2f), Rgba.Hex("#E6E6F2"), EffectArt.Frames, EffectArt.Vortex),
                ("sparkles (heal)", EffectArt.Diameter(2 * 0.8f), Rgba.Hex("#7CE07C"), EffectArt.Frames, EffectArt.Sparkles),
                ("impact (crit)", EffectArt.Diameter(2 * 0.55f), Rgba.Hex("#FFD84C"), EffectArt.Frames, EffectArt.Impact),
                ("aura (buff)", EffectArt.Diameter(0.8f * 1.7f), Rgba.Hex("#F2C440"), EffectArt.AuraFrames, EffectArt.Aura),
            };
            var images = new List<List<PixelImage>>();
            foreach (var (name, diameter, tint, frames, draw) in rows)
            {
                var row = new List<PixelImage>();
                for (int frame = 0; frame < frames; frame++)
                {
                    var image = draw(diameter, frame);
                    string problem = EffectProblem(image, diameter);
                    if (problem != null)
                    {
                        Console.Error.WriteLine($"{name} frame {frame}: {problem}");
                        fine = false;
                    }
                    row.Add(Tinted(image, tint));
                }
                images.Add(row);
            }
            const int gap = 4;
            int width = images.Max(row => row.Sum(image => image.Width + gap)) + gap;
            int height = images.Sum(row => row.Max(image => image.Height) + gap) + gap;
            var sheet = new PixelImage(width, height);
            sheet.Fill(0, 0, width, height, Rgba.Hex("#24202A"));
            int y = gap;
            foreach (var row in images)
            {
                int x = gap;
                foreach (var image in row)
                {
                    sheet.Draw(image, x, y);
                    x += image.Width + gap;
                }
                y += row.Max(image => image.Height) + gap;
            }
            return sheet;
        }

        // Why an effect frame is wrong: the wrong size, nothing drawn, or pixels outside the circle it fills.
        static string EffectProblem(PixelImage image, int diameter)
        {
            if (image.Width != diameter || image.Height != diameter) return $"{image.Width}x{image.Height}, not {diameter}";
            int set = 0;
            float centre = diameter / 2f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    if (!image.IsSet(x, y)) continue;
                    set++;
                    float u = x + 0.5f - centre;
                    float v = y + 0.5f - centre;
                    if (u * u + v * v > centre * centre + 1) return $"a pixel at ({x}, {y}) is outside the circle";
                }
            }
            return set == 0 ? "empty" : null;
        }

        // As a SpriteRenderer tints it: each channel multiplied.
        static PixelImage Tinted(PixelImage image, Rgba tint)
        {
            var tinted = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    if (c.A > 0) tinted[x, y] = new Rgba(c.R * tint.R / 255, c.G * tint.G / 255, c.B * tint.B / 255, c.A);
                }
            }
            return tinted;
        }

        // ---- Every sprite ----

        static PixelImage Sheet(LootData loot, MonsterData monsters, NpcData npcs)
        {
            var rows = new List<List<PixelImage>>();
            var tiles = new List<PixelImage>();
            foreach (var kind in (GroundKind[])Enum.GetValues(typeof(GroundKind)))
            {
                foreach (int mask in new[] { 0, TileArt.Up, TileArt.Down, TileArt.Left | TileArt.Right, 15 })
                {
                    tiles.Add(TileArt.Ground(TileArt.Index(kind, mask, 0)));
                }
            }
            rows.Add(tiles);
            // Each biome's floor, rock and ores.
            foreach (var biome in ((Biome[])Enum.GetValues(typeof(Biome))).Where(b => b != Biome.Cave))
            {
                var row = new List<PixelImage>();
                foreach (var kind in new[] { GroundKind.Floor, GroundKind.Rock, GroundKind.CopperOre, GroundKind.IronOre, GroundKind.MithrilOre, GroundKind.ObsidianOre, GroundKind.VoidOre, GroundKind.Lava, GroundKind.Water })
                {
                    for (int variant = 0; variant < 2; variant++)
                    {
                        row.Add(TileArt.Ground(TileArt.Index(kind, 0, variant, biome)));
                        row.Add(TileArt.Ground(TileArt.Index(kind, 15 & ~TileArt.Down, variant, biome)));
                    }
                }
                rows.Add(row);
            }
            rows.Add(new[]
                {
                    TileType.Workbench, TileType.Furnace, TileType.Chest, TileType.Drill, TileType.Smelter, TileType.Conveyor, TileType.Torch,
                    TileType.Splitter, TileType.Arm, TileType.Sprinkler, TileType.Harvester, TileType.StoneFloor, TileType.BrickFloor,
                    TileType.GlowFloor, TileType.Door, TileType.Table, TileType.Banner, TileType.Pot, TileType.Soil, TileType.FertileSoil,
                    TileType.Filter, TileType.Generator, TileType.Lever, TileType.MushroomSoil, TileType.FrostSoil, TileType.AshSoil, TileType.VoidSoil,
                    TileType.Anvil, TileType.RiftAltar, TileType.Assembler, TileType.PowerPole, TileType.FastConveyor, TileType.UndergroundBelt, TileType.MerchantStall, TileType.SmithStall, TileType.FisherHut, TileType.Keg, TileType.PreservesJar, TileType.Coop, TileType.CrabPot, TileType.WardStone, TileType.Turret, TileType.Barn,
                    TileType.ArcFurnace, TileType.WaterWheel, TileType.SolarPanel, TileType.TeslaTower, TileType.Skylight, TileType.IronWall, TileType.Fold,
                    TileType.RedCarpet, TileType.BlueCarpet, TileType.YellowCarpet, TileType.TapestryWall, TileType.ShieldWall, TileType.Scarecrow, TileType.Fence,
                    TileType.LockedDoor, TileType.Road, TileType.Waypoint,
                }
                .Select(TileArt.Object).Append(TileArt.OpenDoor).Append(TileArt.BigAssembler).Append(TileArt.BigSmelter).Concat(Enumerable.Range(0, ItemArt.BeltFrames).Select(f => ItemArt.Belt(f)))
                .Concat(new[] { ItemArt.Belt(0, fast: true), ItemArt.Underground(0), TileArt.MerchantCounter, ItemArt.Chicken(0), ItemArt.Chicken(1), ItemArt.Cow(0), ItemArt.Cow(1), ItemArt.Sheep(0), ItemArt.Sheep(1), ItemArt.TurretBarrel })
                .Concat(new[] { ItemArt.DrillBit, ItemArt.Chevron, ItemArt.SplitterArrows, ItemArt.RobotArm, ItemArt.SprinklerHead, ItemArt.HarvesterReel,
                    ItemArt.Flame(0), ItemArt.Flame(1), ItemArt.LeverHandle, ItemArt.WaterWheel })
                .Concat(Enumerable.Range(1, TileArt.CrackStages).Select(TileArt.Cracks)).ToList());

            var players = new List<PixelImage>();
            foreach (var look in new[] { Look.Down, Look.DownSide, Look.Side, Look.UpSide, Look.Up })
            {
                for (int frame = 0; frame < CreatureArt.WalkFrames; frame++) players.Add(CreatureArt.Player(look, frame, Rgba.Hex("#3F7FD8")));
            }
            players.Add(CreatureArt.Player(Look.Down, 0, Rgba.Hex("#4DB06A")));
            players.Add(CreatureArt.Player(Look.Down, 0, Rgba.Hex("#E08A3C")));
            rows.Add(players);

            // The people, each beside their stall.
            var people = new List<PixelImage>();
            foreach (var npc in npcs.Npcs)
            {
                people.Add(PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), 0) ?? CreatureArt.Player(Look.Down, 0, Rgba.Hex(npc.Color), Pose.Stand));
                people.Add(TileArt.Object(npc.Tile));
            }
            rows.Add(people);

            // Every pose but walking, in each direction: standing (still, breathing in, blinking), the strike's two
            // frames, casting, flinching.
            var poses = new List<PixelImage>();
            foreach (var look in new[] { Look.Down, Look.Side, Look.Up })
            {
                for (int frame = 0; frame < CreatureArt.StandFrames; frame++) poses.Add(CreatureArt.Player(look, frame, Rgba.Hex("#3F7FD8"), Pose.Stand));
                for (int frame = 0; frame < CreatureArt.StrikeFrames; frame++) poses.Add(CreatureArt.Player(look, frame, Rgba.Hex("#3F7FD8"), Pose.Strike));
                poses.Add(CreatureArt.Player(look, 0, Rgba.Hex("#3F7FD8"), Pose.Cast));
                poses.Add(CreatureArt.Player(look, 0, Rgba.Hex("#3F7FD8"), Pose.Hurt));
            }
            rows.Add(poses);

            var creatures = new List<PixelImage>();
            foreach (var monster in monsters.Monsters)
            {
                for (int frame = 0; frame < CreatureArt.IdleFrames; frame++) creatures.Add(CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), frame));
            }
            creatures.Add(CreatureArt.Monster("unknown", Rgba.Hex("#D05050"), 0));
            creatures.Add(CreatureArt.Minion(Rgba.Hex("#73E6CC"), 0));
            creatures.Add(CreatureArt.Minion(Rgba.Hex("#73E6CC"), 1));
            creatures.Add(CreatureArt.Boss("cave_warden", BossColor, 0));
            creatures.Add(CreatureArt.Boss("cave_warden", BossColor, 1));
            creatures.Add(CreatureArt.Boss("crystal_queen", BossColor, 0));
            creatures.Add(CreatureArt.Boss("crystal_queen", BossColor, 1));
            creatures.Add(CreatureArt.Boss("lava_titan", BossColor, 0));
            creatures.Add(CreatureArt.Boss("lava_titan", BossColor, 1));
            creatures.Add(CreatureArt.Boss("void_lord", BossColor, 0));
            creatures.Add(CreatureArt.Boss("void_lord", BossColor, 1));
            creatures.Add(CreatureArt.Boss("tide_serpent", BossColor, 0));
            creatures.Add(CreatureArt.Boss("tide_serpent", BossColor, 1));
            creatures.Add(CreatureArt.Boss("antlion", BossColor, 0));
            creatures.Add(CreatureArt.Boss("antlion", BossColor, 1));
            rows.Add(creatures);

            rows.Add(new[]
            {
                UiArt.Panel, UiArt.Button(), UiArt.Button(hover: true), UiArt.Button(pressed: true), UiArt.Button(chosen: true), UiArt.Inset,
                UiArt.BarFill(Rgba.Hex("#D24A3C")), UiArt.BarFill(Rgba.Hex("#A8324A")),
            }.Select(UiArt.Enlarged).ToList());
            rows.Add(loot.Bases.Select(ItemArt.Gear).ToList());
            rows.Add(loot.Materials.Select(ItemArt.Material)
                .Concat(loot.Consumables.Select(ItemArt.Consumable))
                .Concat(loot.Runes.Take(1).Select(ItemArt.Rune))
                .Concat(Enumerable.Range(0, 4).Select(ItemArt.Crop))
                .Concat(Enumerable.Range(0, 4).Select(stage => ItemArt.Crop("fire_pepper", stage, watered: stage % 2 == 1)))
                .Concat(new[] { "glow_cap", "frost_berry", "star_melon", "cave_potato", "cave_berry", "moon_grape", "snow_cabbage", "ember_corn", "void_bean" }.SelectMany(id => Enumerable.Range(0, 4).Select(stage => ItemArt.Crop(id, stage))))
                .Concat(new[] { ItemArt.Arrow, ItemArt.Orb, ItemArt.Spore, CreatureArt.Shadow(12) }).ToList());

            return Rows(rows);
        }

        static PixelImage Rows(IEnumerable<IList<PixelImage>> lines)
        {
            var rows = lines.ToList();
            const int gap = 4;
            int width = rows.Max(row => row.Sum(image => image.Width + gap)) + gap;
            int height = rows.Sum(row => row.Max(image => image.Height) + gap) + gap;
            var sheet = new PixelImage(width, height);
            sheet.Fill(0, 0, width, height, Rgba.Hex("#24202A"));
            int y = gap;
            foreach (var row in rows)
            {
                int x = gap;
                foreach (var image in row)
                {
                    sheet.Draw(image, x, y);
                    x += image.Width + gap;
                }
                y += row.Max(image => image.Height) + gap;
            }
            return sheet;
        }

        // Every look of the player (eight ways: the five drawn and three of them flipped) in every pose and frame, a row
        // a look, for checking the motions side by side (2026-11-09): walk 0-3, stand 0-2, strike 0-1, cast, hurt,
        // shoot 0-1, zap 0-1, drink 0-1, thrust, spread.
        static PixelImage Players()
        {
            var tunic = Rgba.Hex("#3F7FD8");
            var rows = new List<IList<PixelImage>>();
            var poses = new List<(Pose Pose, int Frames)>
            {
                (Pose.Walk, CreatureArt.WalkFrames), (Pose.Stand, CreatureArt.StandFrames), (Pose.Strike, CreatureArt.StrikeFrames), (Pose.Cast, 1),
                (Pose.Hurt, 1), (Pose.Shoot, 2), (Pose.Zap, 2), (Pose.Drink, 2), (Pose.Thrust, 1), (Pose.Spread, 1),
            };
            foreach (var (look, flip) in new[] { (Look.Down, false), (Look.DownSide, false), (Look.Side, false), (Look.UpSide, false), (Look.Up, false),
                (Look.UpSide, true), (Look.Side, true), (Look.DownSide, true) })
            {
                var row = new List<PixelImage>();
                foreach (var (pose, frames) in poses)
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        var image = CreatureArt.Player(look, frame, tunic, pose);
                        row.Add(flip ? image.FlippedX() : image);
                    }
                }
                rows.Add(row);
            }
            return Rows(rows);
        }

        // Every boss in every view (front, front three-quarter, side, back three-quarter, back) and pose (idle in both
        // frames, wind-up, strike, charge, roar), a row a boss and view (2026-11-09).
        static PixelImage Bosses()
        {
            var rows = new List<IList<PixelImage>>();
            foreach (string id in BossIds)
            {
                foreach (var view in (BossView[])Enum.GetValues(typeof(BossView)))
                {
                    var row = new List<PixelImage>
                    {
                        CreatureArt.Boss(id, BossColor, 0, view), CreatureArt.Boss(id, BossColor, 1, view),
                    };
                    foreach (var pose in new[] { BossPose.Windup, BossPose.Strike, BossPose.Charge, BossPose.Roar }) row.Add(CreatureArt.Boss(id, BossColor, 0, view, pose));
                    rows.Add(row);
                }
            }
            return Rows(rows);
        }

        static readonly Rgba BossColor = new Rgba(158, 143, 128);

        // ---- Scenes ----

        // The spawn of seed 7 with a small base built next to it: stations, a drill on a copper vein feeding belts
        // into a smelter and a chest, torches, a field of mushrooms, three players, monsters and some loot.
        static PixelImage Cave(LootData loot, MonsterData monsters, WorldLights lights, bool dark)
        {
            var world = WorldGenerator.Generate(7);
            var map = world.Map;
            int sx = world.SpawnX;
            int sy = world.SpawnY;
            var scene = new Scene(map, sx - 14, sy - 8, 28, 16, _ => false);
            if (dark) scene.Lit(lights, null);

            void Put(int dx, int dy, TileType type) => map[sx + dx, sy + dy] = type;
            for (int dx = -5; dx <= 7; dx++) Put(dx, 3, TileType.Floor);
            Put(-4, 3, TileType.Workbench);
            Put(-3, 3, TileType.Furnace);
            Put(-2, 3, TileType.Chest);
            // The rest of what stands on the floor (PropArt.Stands, 2026-11-10).
            Put(-1, 3, TileType.Anvil);
            Put(1, 3, TileType.Bed);
            Put(2, 3, TileType.Table);
            Put(3, 3, TileType.Pot);
            Put(4, 3, TileType.Lamp);
            Put(5, 3, TileType.Banner);
            Put(6, 3, TileType.WardStone);
            Put(7, 3, TileType.RiftAltar);
            for (int dx = 1; dx <= 9; dx++) Put(dx, -4, TileType.Floor);
            Put(1, -4, TileType.CopperOre);
            Put(1, -3, TileType.Wall);
            Put(1, -5, TileType.Wall);
            Put(2, -4, TileType.Drill);
            for (int dx = 3; dx <= 4; dx++) Put(dx, -4, TileType.Conveyor);
            Put(5, -4, TileType.Smelter);
            Put(6, -4, TileType.Conveyor);
            Put(7, -4, TileType.Chest);
            Put(-6, 3, TileType.Torch);
            Put(9, -3, TileType.Torch);
            scene.Places(null);
            scene.Machine(sx + 2, sy - 4, 2, ItemArt.DrillBit);
            scene.Machine(sx + 3, sy - 4, 0, ItemArt.Belt(0));
            scene.Machine(sx + 4, sy - 4, 0, ItemArt.Belt(0));
            scene.Machine(sx + 5, sy - 4, 0, ItemArt.Chevron);
            scene.Machine(sx + 6, sy - 4, 0, ItemArt.Belt(0));
            scene.Icon(sx + 3.5f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));
            scene.Icon(sx + 6.8f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));

            for (int i = 0; i < 4; i++) scene.Crop(sx - 5 + i, sy - 2, i);
            // Rock beside the drill, cracked by a few hits.
            scene.Machine(sx + 1, sy - 3, 0, TileArt.Cracks(2));
            scene.Machine(sx + 1, sy - 5, 0, TileArt.Cracks(TileArt.CrackStages));

            scene.Player(sx + 0.5f, sy + 0.5f, CreatureArt.Player(Look.Down, 0, Rgba.Hex("#3F7FD8")));
            scene.Player(sx + 2.5f, sy + 1.2f, CreatureArt.Player(Look.Side, 1, Rgba.Hex("#4DB06A")));
            scene.Player(sx - 1.5f, sy + 1.6f, CreatureArt.Player(Look.Up, 1, Rgba.Hex("#E08A3C")));
            float mx = sx + 6;
            foreach (var monster in monsters.Monsters)
            {
                var image = CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), 0);
                scene.Body(mx, sy + 0.5f, monster.Radius, mx > sx + 7 ? image.FlippedX() : image);
                mx += 1.6f;
            }
            scene.Icon(sx - 1, sy - 0.5f, ItemArt.Gear(loot.Bases.First(b => b.Id == "iron_sword")));
            scene.Icon(sx - 0.2f, sy - 0.8f, ItemArt.Consumable(loot.Consumables[0]));
            scene.Icon(sx + 1.2f, sy - 0.6f, ItemArt.Rune(loot.Runes[0]));
            scene.Icon(sx + 2.2f, sy - 1.2f, ItemArt.Material(loot.FindMaterial("slime_gel")));
            scene.Shot(sx + 3.4f, sy + 0.2f, ItemArt.Arrow);
            return scene.Image;
        }

        // A place of the story as the game lays it out (Docs/Story.md): its plaza, lamps, waypoint, roads and people, lit.
        static PixelImage Place(GeneratedWorld world, PlaceSite site, WorldLights lights)
        {
            int half = site.Radius + 5;
            var scene = new Scene(world.Map, site.X - half, site.Y - half, half * 2, half * 2, _ => false);
            scene.Lit(lights, null);
            scene.Places(world.Places);
            foreach (var (character, at) in site.People)
                scene.Player(at.X, at.Y - 0.3f, PeopleArt.Look(character.Look, Rgba.Hex(character.Color), 0));
            scene.Player(site.X + 0.5f, site.Y - 1.5f, CreatureArt.Player(Look.Up, 0, Rgba.Hex("#3F7FD8")));
            return scene.Image;
        }

        // The stretch of road round its first rest spot (a campfire outside every place), lit, with a player on the road.
        static PixelImage RoadView(GeneratedWorld world, WorldLights lights)
        {
            var map = world.Map;
            int fx = map.Width / 2, fy = map.Height / 2;
            for (int y = 0; y < map.Height && fx == map.Width / 2; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (map[x, y] != TileType.Campfire || world.Places.Any(p => (x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) <= p.Radius * p.Radius)) continue;
                    fx = x;
                    fy = y;
                    break;
                }
            const int half = 14;
            var scene = new Scene(map, fx - half, fy - half / 2 - 2, half * 2, half + 4, _ => false);
            scene.Lit(lights, null);
            scene.Places(world.Places);
            return scene.Image;
        }

        // Images side by side with a gap, on a dark ground.
        static PixelImage Row(List<PixelImage> images)
        {
            int width = images.Sum(i => i.Width + 2) + 2, height = images.Max(i => i.Height) + 4;
            var row = new PixelImage(width, height);
            row.Fill(0, 0, width, height, Rgba.Hex("#2A2420"));
            int x = 2;
            foreach (var image in images)
            {
                row.Draw(image, x, 2);
                x += image.Width + 2;
            }
            return row;
        }

        // The boss in its arena, awake, with two players in front of it.
        static PixelImage Arena(WorldLights lights)
        {
            var world = WorldGenerator.Generate(7);
            int ax = world.ArenaX;
            int ay = world.ArenaY;
            var arena = world.Arena;
            var scene = new Scene(world.Map, ax - 13, ay - 8, 26, 16, p => Vec2.Distance(p, arena) <= world.ArenaRadius);
            scene.Lit(lights, WorldLights.ArenaAmbient(world.Arenas, world.ArenaRadius));
            scene.Body(ax + 0.5f, ay + 1.5f, 1.2f, CreatureArt.Boss("cave_warden", BossColor, 0));
            scene.Player(ax - 2.5f, ay - 3f, CreatureArt.Player(Look.Up, 0, Rgba.Hex("#3F7FD8")));
            scene.Player(ax + 3.5f, ay - 2.5f, CreatureArt.Player(Look.Side, 0, Rgba.Hex("#4DB06A")).FlippedX());
            scene.Shot(ax - 1.5f, ay - 1.5f, ItemArt.Orb);
            scene.Shot(ax + 2f, ay - 0.5f, ItemArt.Spore);
            return scene.Image;
        }

        // A window of the map drawn the way WorldView draws it, with things placed in world units (y up).
        sealed class Scene
        {
            readonly TileMap _map;
            readonly int _left;
            readonly int _bottom;
            readonly int _width;
            readonly int _height;
            readonly Func<Vec2, bool> _inArena;
            readonly List<(float Y, Action<PixelImage> Draw)> _bodies = new List<(float, Action<PixelImage>)>();
            IReadOnlyList<PlaceSite> _places;

            // The places of the story: their floors under things, and their structures standing (as WorldView.DrawProps).
            public void Places(IReadOnlyList<PlaceSite> places)
            {
                _places = places;
                for (int y = _bottom - 3; y < _bottom + _height + 1; y++)
                {
                    for (int x = _left - 3; x < _left + _width + 1; x++)
                    {
                        if (!_map.InBounds(x, y)) continue;
                        var type = _map[x, y];
                        var image = PropArt.Prop(type, PropArt.IsWall(type) ? PropArt.Sides(_map, x, y) : 0);
                        if (image == null) continue;
                        var (w, _) = Structures.Footprint(type);
                        var (px, py) = Corner(x + w / 2f, y);
                        _bodies.Add((y, canvas => canvas.Draw(image, px - image.Width / 2, py - image.Height)));
                    }
                }
            }
            readonly List<Action<PixelImage>> _overlays = new List<Action<PixelImage>>();
            readonly List<Action<PixelImage>> _icons = new List<Action<PixelImage>>();
            readonly List<Action<PixelImage>> _shots = new List<Action<PixelImage>>();
            readonly List<Vec2> _carriers = new List<Vec2>();
            WorldLights _lights;
            Func<int, int, float> _ambient;
            bool _lit;

            public Scene(TileMap map, int left, int bottom, int width, int height, Func<Vec2, bool> inArena)
            {
                _map = map;
                _left = left;
                _bottom = bottom;
                _width = width;
                _height = height;
                _inArena = inArena;
            }

            public PixelImage Image
            {
                get
                {
                    var image = new PixelImage(_width * Px, _height * Px);
                    image.Fill(0, 0, image.Width, image.Height, Backdrop);
                    for (int y = _bottom; y < _bottom + _height; y++)
                    {
                        for (int x = _left; x < _left + _width; x++)
                        {
                            var (px, py) = Corner(x, y + 1);
                            image.Draw(TileArt.Ground(TileArt.GroundAt(_map, x, y, _inArena(TileMap.Center(x, y)))), px, py);
                            // The floor that runs under things (PropArt.Underlay), as WorldView's underlay layer draws it.
                            var under = PropArt.Underlay(_map, _places, x, y);
                            if (under != null) image.Draw(TileArt.Object(under.Value), px, py);
                            // Things that stand (PropArt.Stands) are drawn with the bodies, as WorldView.DrawProps does.
                            var placed = PropArt.Stands(_map[x, y]) ? null
                                : _map[x, y] == TileType.Road ? TileArt.Road(TileArt.RoadVariant(_map, x, y)) : TileArt.Object(_map[x, y]);
                            if (placed != null) image.Draw(placed, px, py);
                        }
                    }
                    foreach (var draw in _overlays) draw(image);
                    foreach (var (_, draw) in _bodies.OrderByDescending(b => b.Y)) draw(image);
                    foreach (var draw in _icons) draw(image);
                    if (_lit) Darken(image);
                    foreach (var draw in _shots) draw(image);
                    return image;
                }
            }

            // Lights the scene the way WorldView does: every player carries a light, burning tiles light their
            // surroundings, and the ambient function (the arena's glow) adds to it.
            public void Lit(WorldLights lights, Func<int, int, float> ambient)
            {
                _lit = true;
                _lights = lights;
                _ambient = ambient;
            }

            // The light map stretched over the scene with bilinear smoothing between tile centres, as the view's
            // texture is, then laid over it.
            void Darken(PixelImage image)
            {
                const int margin = 4;
                int size = Math.Max(_width, _height) + margin * 2;
                int left = _left - margin;
                int bottom = _bottom - margin;
                var sources = _carriers.Select(WorldLights.Carried).ToList();
                _lights.AddTiles(sources, _map, left, bottom, size, 0);
                var lightMap = new LightMap(size);
                lightMap.Compute(_map, left, bottom, sources, _ambient);
                for (int py = 0; py < image.Height; py++)
                {
                    for (int px = 0; px < image.Width; px++)
                    {
                        float u = _left + (px + 0.5f) / Px - left - 0.5f;
                        float v = _bottom + _height - (py + 0.5f) / Px - bottom - 0.5f;
                        int i = (int)MathF.Floor(u);
                        int j = (int)MathF.Floor(v);
                        float fu = u - i;
                        float fv = v - j;
                        float r = 0, g = 0, b = 0, a = 0;
                        void Add(int ti, int tj, float weight)
                        {
                            var o = lightMap.Overlay(Math.Clamp(ti, 0, size - 1), Math.Clamp(tj, 0, size - 1));
                            r += o.R * weight;
                            g += o.G * weight;
                            b += o.B * weight;
                            a += o.A * weight;
                        }
                        Add(i, j, (1 - fu) * (1 - fv));
                        Add(i + 1, j, fu * (1 - fv));
                        Add(i, j + 1, (1 - fu) * fv);
                        Add(i + 1, j + 1, fu * fv);
                        image.Blend(px, py, new Rgba((int)r, (int)g, (int)b, (int)a));
                    }
                }
            }

            (int X, int Y) Corner(float x, float y) => ((int)MathF.Round((x - _left) * Px), (int)MathF.Round((_bottom + _height - y) * Px));

            public void Machine(int x, int y, int direction, PixelImage part)
            {
                var (px, py) = Corner(x, y + 1);
                var turned = Turn(part, direction);
                _overlays.Add(image => image.Draw(turned, px + (Px - turned.Width) / 2, py + (Px - turned.Height) / 2));
            }

            public void Crop(int x, int y, int stage)
            {
                var (px, py) = Corner(x, y);
                var crop = ItemArt.Crop(stage);
                _overlays.Add(image => image.Draw(crop, px + (Px - crop.Width) / 2, py - crop.Height));
            }

            // Bodies stand with their feet a little below their centre, as in WorldView.Place.
            public void Body(float x, float y, float radius, PixelImage sprite)
            {
                var (px, py) = Corner(x, y);
                int feet = py + (int)MathF.Round(radius * 0.8f * Px);
                var shadow = CreatureArt.Shadow(Math.Max(6, (int)MathF.Round(radius * 2 * Px * 0.9f)));
                _bodies.Add((y, image =>
                {
                    image.Draw(shadow, px - shadow.Width / 2, feet - shadow.Height / 2 - 1);
                    image.Draw(sprite, px - sprite.Width / 2, feet - sprite.Height);
                }));
            }

            // A player, who carries a light.
            public void Player(float x, float y, PixelImage sprite)
            {
                _carriers.Add(new Vec2(x, y));
                Body(x, y, World.PlayerRadius, sprite);
            }

            public void Icon(float x, float y, PixelImage icon)
            {
                var (px, py) = Corner(x, y);
                _icons.Add(image => image.Draw(icon, px - icon.Width / 2, py - icon.Height / 2));
            }

            // Projectiles are drawn over the dark, as in WorldView.
            public void Shot(float x, float y, PixelImage icon)
            {
                var (px, py) = Corner(x, y);
                _shots.Add(image => image.Draw(icon, px - icon.Width / 2, py - icon.Height / 2));
            }
        }

        // Quarter turns counter-clockwise, as a rotation of 90 degrees per step does in Unity.
        static PixelImage Turn(PixelImage image, int quarters)
        {
            var result = image;
            for (int q = 0; q < (quarters % 4 + 4) % 4; q++)
            {
                var turned = new PixelImage(result.Height, result.Width);
                for (int y = 0; y < result.Height; y++)
                {
                    for (int x = 0; x < result.Width; x++) turned[y, result.Width - 1 - x] = result[x, y];
                }
                result = turned;
            }
            return result;
        }

        static PixelImage Scaled(PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x / scale, y / scale];
            }
            return result;
        }

        static LootData LoadLoot()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Loot"));
            string Read(string file) => File.ReadAllText(Path.Combine(folder, file));
            return LootSheets.Parse(
                Read(LootSheets.BasesFile),
                Read(LootSheets.AffixesFile),
                Read(LootSheets.RunesFile),
                Read(LootSheets.RunewordsFile),
                Read(LootSheets.ConsumablesFile),
                Read(LootSheets.MaterialsFile),
                Read(LootSheets.RecipesFile),
                Read(LootSheets.SetsFile));
        }

        static string FindFolder(string relativePath)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            throw new DirectoryNotFoundException(relativePath);
        }
    }
}
