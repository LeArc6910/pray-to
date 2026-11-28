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
    static partial class Program
    {
        const int Px = TileArt.Size;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F");

        static int Main(string[] args)
        {
            // --only spider: just that boss's motions (gif, frame strips, continuity report), for working on them; --only hero
            // the player's.
            // --only flat: every tile drawn flat on the object layer, and the rails as they join (out/flat/, FlatPreview).
            // How plainly each boss's plain blow shows in its wind-up alone (out/anim/tells.txt, Docs/BossRedesign.md 9).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "tells")
            {
                string anim = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "anim");
                Directory.CreateDirectory(anim);
                TellsCheck.Write(anim);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "lint")
            {
                // --only lint: just the pixel slips' counts and out/lint.md (ArtLint), without drawing the sheets.
                string folder = FindFolder(Path.Combine("DevTools", "ArtPreview"));
                string beasts = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
                Directory.CreateDirectory(Path.Combine(folder, "out"));
                return ArtLint.Run(LoadLoot(), MonsterSheet.Parse(File.ReadAllText(Path.Combine(beasts, MonsterSheet.File)), File.ReadAllText(Path.Combine(beasts, MonsterAttackSheet.File))), folder, Path.Combine(folder, "out")) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "title")
            {
                // --only title: the title rework's art (out/title/, TitlePreview, Docs/TitleRework.md 묶음 T2).
                return TitlePreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out")) ? 0 : 1;
            }
            if (args.Length > 2 && args[0] == "--only" && args[1] == "gapframes")
            {
                // --only gapframes <boss:View:Pose:frame>...: those frames side by side, the gaps in red
                // (out/anim/gaps_frames.png), to look at a fix before and after.
                var preview = FindFolder(Path.Combine("DevTools", "ArtPreview"));
                return GapCheck.WriteFrames(Path.Combine(preview, "out", "anim"), Path.GetFullPath(Path.Combine(preview, "..", "..")), args.Skip(2).ToArray()) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "gaps")
            {
                // --only gaps: see-through gaps inside every boss's and monster's body, frame by frame (out/anim/gaps.txt,
                // gaps.png, GapCheck).
                var preview = FindFolder(Path.Combine("DevTools", "ArtPreview"));
                return GapCheck.Write(Path.Combine(preview, "out", "anim"), Path.GetFullPath(Path.Combine(preview, "..", ".."))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "skillfx")
            {
                // --only skillfx: just the skills' effects (out/skillfx/, SkillFxPreview).
                return WriteSkillFx(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out")) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "fxmock")
            {
                // --only fxmock: the effects rework's now-and-new mock-ups (out/fxmock/, FxMockPreview, Docs/EffectsRework.md).
                return WriteFxMock(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out")) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "hazard")
            {
                // --only hazard: the boss's warnings and the blow that lands in their shape (out/hazard/, HazardPreview).
                return HazardPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "hazard"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "shot")
            {
                // --only shot: the enemies' shots by element, flying and ending (out/shot/, ShotPreview).
                return ShotPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "shot"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "hitfx")
            {
                // --only hitfx: what a blow looks like on the body it lands on, by element (out/hitfx/, HitFxPreview, E7).
                return HitFxPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "hitfx"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "bossfx")
            {
                // --only bossfx: the bosses' own effects over their blows' shapes (out/bossfx/, BossFxPreview, E6).
                return BossFxPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "bossfx"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "ground")
            {
                // --only ground: a boss's lava, rock, water, cracks, edge and vent on one sheet (out/ground/, GroundPreview).
                return GroundPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "ground"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "forces")
            {
                // --only forces: R3's sand pit, shallows, wall of water, thread, wake and grains (out/forces/, ForcesPreview).
                string forces = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "forces");
                return ForcesPreview.Write(forces, TileArt.Ground(TileArt.ArenaIndex(ArenaKind.Pit, 0, 0)),
                    TileArt.Ground(TileArt.ArenaIndex(ArenaKind.Flooded, 0, 0)), TileArt.Ground(TileArt.ArenaIndex(ArenaKind.Nest, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "r1b")
            {
                // --only r1b: the sheathed counter, the helm off and the two marks on one sheet (out/r1b/, R1bPreview).
                return R1bPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "r1b"),
                    TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0))) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "monsterfx")
            {
                // --only monsterfx: just the ordinary monsters' warnings and effects (out/monsterfx/, MonsterFxPreview).
                return WriteMonsterFx(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out")) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "sizes")
            {
                // --only sizes: just the monsters' size classes as the game draws them (out/sizes/, SizesPreview, B0b).
                return WriteSizes(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out")) ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] != "arenas" && args[1] != "gear" && args[1] != "weapons" && args[1] != "weaponarts" && args[1] != "weaponaudit" && args[1] != "map" && args[1] != "uimock" && args[1] != "biomemock" && args[1] != "dungeons" && args[1] != "portraits" && args[1] != "ui" && args[1] != "depth" && args[1] != "sizes" && args[1] != "biomes" && args[1] != "flat")
            {
                string anim = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out", "anim");
                Directory.CreateDirectory(anim);
                return Motions.WriteOnly(anim, args[1]) ? 0 : 1;
            }
            string output = args.Length > 0 && args[0] != "--only" ? args[0] : Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
            Directory.CreateDirectory(output);
            var loot = LoadLoot();
            string monsterFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(monsterFolder, MonsterSheet.File)), File.ReadAllText(Path.Combine(monsterFolder, MonsterAttackSheet.File)));
            // The packs, for the dungeons' rooms (DungeonPreview).
            PackSheet.Parse(File.ReadAllText(Path.Combine(monsterFolder, PackSheet.File)), monsters);
            string world = FindFolder(Path.Combine("Assets", "_Project", "Data", "World"));
            var tiles = TileSheet.Parse(
                File.ReadAllText(Path.Combine(world, TileSheet.File)),
                loot,
                File.ReadAllText(Path.Combine(world, TileSheet.CropsFile)),
                File.ReadAllText(Path.Combine(world, TileSheet.MachinesFile)),
                File.ReadAllText(Path.Combine(world, TerrainSheet.File)),
                File.ReadAllText(Path.Combine(world, RoomSheet.File)),
                File.ReadAllText(Path.Combine(world, RoomSheet.RolesFile)),
                File.ReadAllText(Path.Combine(world, PoiSheet.File)),
                File.ReadAllText(Path.Combine(world, DungeonSheet.File)),
                File.ReadAllText(Path.Combine(world, DungeonSheet.GraphsFile)));
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
            if (args.Length == 2 && args[0] == "--only" && args[1] == "flat")
            {
                string flatOut = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
                FlatPreview.Write(flatOut);
                WriteFlatScenes(flatOut, npcs);
                return 0;
            }
            // --only arenas: just the arenas' pictures (arenas.png, arenas_unlit.png, arenas/).
            // --only map: just the generated map's pictures, three seeds for each generator version (out/map/, MapPreview).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "map")
            {
                string mapOut = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
                MapPreview.Write(mapOut, story, WorldArenaSheet(monsters), tiles.Terrain, new ulong[] { 1, 2, 3 }, rooms: world => RoomViews(world, lights, Path.Combine(mapOut, "map")));
                return 0;
            }
            if (args.Length == 2 && args[0] == "--only" && args[1] == "uimock")
            {
                // --only uimock: the UI overhaul's candidate looks as mockups over a village (out/uimock/, UiMockPreview).
                string classSheets = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
                string passives = File.ReadAllText(Path.Combine(classSheets, ClassSheet.PassivesFile));
                var mockClasses = ClassSheet.Parse(File.ReadAllText(Path.Combine(classSheets, ClassSheet.ClassesFile)), File.ReadAllText(Path.Combine(classSheets, ClassSheet.SkillsFile)), passives);
                var mockWorld = WorldGenerator.Generate(7, story: story);
                UiMockPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot, mockClasses, passives, Place(mockWorld, mockWorld.Places[0], lights));
                return 0;
            }
            // --only biomemock: the biome rework's mockups (out/biomemock/, BiomeMockPreview, Docs/BiomeRework.md).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "biomemock")
            {
                BiomeMockPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"));
                return 0;
            }
            // --only depth: the standing layer as the game draws it (out/depth/, DepthPreview, Docs/BiomeRework.md 묶음 B0).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "depth")
            {
                WriteDepth(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), story, lights);
                return 0;
            }
            // --only biomes: the biomes' looks as the game draws them (out/biomes/, BiomesPreview, Docs/BiomeRework.md 묶음 B1a).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "biomes")
                return WriteBiomes(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), lights, tiles, story, monsters) ? 0 : 1;
            // --only portraits: just the talk box's portraits, every person in every expression (out/portraits/, PortraitPreview).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "portraits")
                return PortraitPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), story, npcs) ? 0 : 1;
            // --only weapons: the kinds of weapon held by the hero (out/weapons_held.png, WeaponPreview, Docs/Weapons.md 묶음 B).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "weapons")
            {
                WeaponPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot);
                return 0;
            }
            // --only weaponaudit: every weapon's basic attack drawn, sounded and held, a table of what is missing
            // (out/weapon_audit.md, WeaponAudit); fails when anything is.
            if (args.Length == 2 && args[0] == "--only" && args[1] == "weaponaudit")
                return WeaponAudit.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot) ? 0 : 1;
            // --only weaponarts: the weapon kinds' own arts as a mock-up (out/weapon_arts.png, WeaponArtsPreview, Docs/Weapons.md 묶음 C).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "weaponarts")
            {
                WeaponArtsPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot);
                return 0;
            }
            // --only gear: the hunting sets', elite uniques' and boss sets' icons and the hunting trophies (out/gear.png, GearPreview).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "gear")
            {
                GearPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot);
                return 0;
            }
            // --only ui: just the game's UI skin, its pieces and the existing screens in it (out/ui/, UiPreview).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "ui")
            {
                UiPreview.Write(Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), loot, LoadClassesWithTree(), RecordsWorld(story, monsters, tiles.Terrain), monsters);
                return 0;
            }
            // --only dungeons: just the instance dungeons as they are laid (dungeons.png, dungeons/, DungeonPreview).
            if (args.Length == 2 && args[0] == "--only" && args[1] == "dungeons")
            {
                Dungeons(tiles.Terrain, monsters, lights, Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out"), story);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--only")
            {
                output = Path.Combine(FindFolder(Path.Combine("DevTools", "ArtPreview")), "out");
                Arenas(lights, output);
                WorldArenas(lights, output, monsters, story);
                return 0;
            }
            // How long the game takes to draw every ground tile when it builds its atlas (Sprites).
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < TileArt.GroundCount; i++) TileArt.Ground(i);
            Console.WriteLine($"{TileArt.GroundCount} ground tiles in {clock.ElapsedMilliseconds} ms");
            Save(Sheet(loot, monsters, npcs), Path.Combine(output, "sheet.png"), 4);
            Save(UiMock(), Path.Combine(output, "ui_mock.png"), 3);
            UiPreview.Write(output, loot, LoadClassesWithTree(), RecordsWorld(story, monsters, tiles.Terrain), monsters);
            // The shapes taken from CC0 packs (Docs/ArtSources.md), in each tier's colours: picks and ingots.
            var imported = new[] { "_bar", "cave_minnow", "ice_trout", "crystal_eel", "void_angler", "warden_core", "queen_heart", "void_essence", "frost_berry",
                "berry_juice", "moon_wine", "milk", "lotus_tea", "hoe", "watering_can" };
            Save(Row(loot.Bases.Where(b => b.IsPickaxe).Select(ItemArt.Gear)
                .Concat(loot.Materials.Where(m => imported.Any(id => m.Id == id || id.StartsWith("_") && m.Id.EndsWith(id))).Select(ItemArt.Material))
                .Concat(loot.Consumables.Where(c => c.Id == "healing_potion" || c.Id == "mana_potion" || c.Id == "recall_scroll" || c.Id == "grilled_fish").Select(ItemArt.Consumable))
                .ToList()), Path.Combine(output, "imported.png"), 5);
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
            // Getting about (묶음 3c): the waystone, a station's cart (and a rider's), the broken end of a line, the
            // wandering merchant's stall under its awning.
            Save(Row(new[] { PropArt.Prop(TileType.Waystone), PropArt.RideCart(), PropArt.BrokenRail(), TileArt.Object(TileType.WandererStall),
                TileArt.Awning(TileType.WandererStall) }.Concat(loot.Consumables.Where(c => c.Id == "map_scrap").Select(ItemArt.Consumable)).ToList()),
                Path.Combine(pieces, "travel.png"), 6);
            Save(Row(new[] { 0, 2 | 8, 1 | 4, 1 | 2, 15 }.Select(sides => PropArt.Prop(TileType.CastleWall, sides)).ToList()), Path.Combine(pieces, "castle_walls.png"), 6);
            var storyWorld = WorldGenerator.Generate(7, story: story);
            // The standing layer (묶음 B0): joined fences, gates, a pen, places with roofs fading, the biomes' decor.
            WriteDepth(output, story, lights);
            foreach (var site in storyWorld.Places) Save(Place(storyWorld, site, lights), Path.Combine(pieces, $"place_{site.Definition.Id}.png"), 3);
            // A road between places with its lamps, signposts and rest spot (2026-11-11).
            Save(RoadView(storyWorld, lights), Path.Combine(pieces, "road.png"), 3);
            // Square cave corners beside rounded ones, for a person to judge (Corners, 2026-11-11).
            Save(Corners.Compare(), Path.Combine(pieces, "corners_compare.png"), 3);
            Save(Players(), Path.Combine(output, "player.png"), 3);
            Save(Bosses(), Path.Combine(output, "bosses.png"), 3);
            Motions.Write(Path.Combine(output, "anim"), loot, monsters, npcs);
            // The hero's motions, every frame checked against the one before (HeroPreview): a jump fails the run.
            int heroJumps = HeroPreview.Write(Path.Combine(output, "anim"), TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0)), monsters, npcs);
            if (heroJumps > 0)
            {
                Console.Error.WriteLine($"The hero's motions jump in {heroJumps} places (anim/hero_*_continuity.txt)");
                return 1;
            }
            Save(Cave(loot, monsters, lights, dark: true), Path.Combine(output, "cave.png"), 3);
            Save(Cave(loot, monsters, lights, dark: false), Path.Combine(output, "cave_unlit.png"), 3);
            Save(Arena(lights), Path.Combine(output, "arena.png"), 3);
            Arenas(lights, output);
            WorldArenas(lights, output, monsters, story);
            // The generated map, one seed for each generator version (map/, MapPreview).
            MapPreview.Write(output, story, WorldArenaSheet(monsters), tiles.Terrain, new ulong[] { 1 }, rooms: world => RoomViews(world, lights, Path.Combine(output, "map")));
            Dungeons(tiles.Terrain, monsters, lights, output, story);
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
            // Every skill's effect played out (2026-11-21, Docs/SkillEffects.md).
            if (!WriteSkillFx(output)) return 1;
            if (!WriteMonsterFx(output)) return 1;
            if (!WriteSizes(output)) return 1;
            // The biomes' looks (묶음 B1a): prop sheets, a real room of each unlit and lit, the critters.
            if (!WriteBiomes(output, lights, tiles, story, monsters)) return 1;
            // The flat tiles' audit (2026-11-25): every object tile, the rails, and the scenes as the game draws them.
            FlatPreview.Write(output);
            WriteFlatScenes(output, npcs);
            // The title rework's art (out/title/): fails when the default look is not the old hero pixel for pixel.
            if (!TitlePreview.Write(output)) return 1;
            // The pixel slips the eye misses at sheet size (ArtLint): fails only when a count goes up.
            if (!ArtLint.Run(loot, monsters, FindFolder(Path.Combine("DevTools", "ArtPreview")), output)) return 1;
            if (!WriteSounds(Path.Combine(output, "sounds"))) return 1;
            if (!WriteSkillSounds(Path.Combine(output, "sounds", "skills"), classes)) return 1;
            if (!WriteMusic(Path.Combine(output, "music"))) return 1;
            Console.WriteLine($"Wrote sheet.png, player.png, bosses.png, anim/, cave.png, cave_unlit.png, arena.png, skills.png, effects.png, sounds/ and music/ to {output}");
            return 0;
        }

        static bool WriteMonsterFx(string output)
        {
            string monsterFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(monsterFolder, MonsterSheet.File)), File.ReadAllText(Path.Combine(monsterFolder, MonsterAttackSheet.File)));
            if (!monsters.Success)
            {
                foreach (string error in monsters.Errors) Console.Error.WriteLine(error);
                return false;
            }
            return MonsterFxPreview.Write(Path.Combine(output, "monsterfx"), monsters, TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0)),
                HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Sword), Rgba.Hex("#4D7FD9")));
        }

        // The monsters' size classes (out/sizes/, SizesPreview, Docs/BiomeRework.md B0b).
        static bool WriteSizes(string output)
        {
            string monsterFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(monsterFolder, MonsterSheet.File)), File.ReadAllText(Path.Combine(monsterFolder, MonsterAttackSheet.File)));
            if (!monsters.Success)
            {
                foreach (string error in monsters.Errors) Console.Error.WriteLine(error);
                return false;
            }
            return SizesPreview.Write(output, monsters, HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Sword), Rgba.Hex("#4D7FD9")));
        }

        static bool WriteFxMock(string output)
        {
            string classFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var classes = ClassSheet.Parse(File.ReadAllText(Path.Combine(classFolder, ClassSheet.ClassesFile)), File.ReadAllText(Path.Combine(classFolder, ClassSheet.SkillsFile)));
            string monsterFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var monsters = MonsterSheet.Parse(File.ReadAllText(Path.Combine(monsterFolder, MonsterSheet.File)), File.ReadAllText(Path.Combine(monsterFolder, MonsterAttackSheet.File)));
            if (!classes.Success || !monsters.Success)
            {
                foreach (string error in classes.Errors.Concat(monsters.Errors)) Console.Error.WriteLine(error);
                return false;
            }
            return FxMockPreview.Write(Path.Combine(output, "fxmock"), classes, monsters, TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0)),
                HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Sword), Rgba.Hex("#4D7FD9")));
        }

        static bool WriteSkillFx(string output)
        {
            string classFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var classes = ClassSheet.Parse(File.ReadAllText(Path.Combine(classFolder, ClassSheet.ClassesFile)), File.ReadAllText(Path.Combine(classFolder, ClassSheet.SkillsFile)));
            if (!classes.Success)
            {
                foreach (string error in classes.Errors) Console.Error.WriteLine(error);
                return false;
            }
            return SkillFxPreview.Write(Path.Combine(output, "skillfx"), classes, TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0)), Rgba.Hex("#6FCF5A"));
        }

        static readonly string[] BossIds = { "cave_warden", "crystal_queen", "lava_titan", "void_lord", "tide_serpent", "antlion", "mad_general", "crown_wraith", "silk_mother" };

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

        // The UI frames as the game stretches them (nine-slice, UiSkin): a menu page with a title bar, buttons and a
        // text field, to look at the frames together (ui_mock.png).
        static PixelImage UiMock()
        {
            var mock = new PixelImage(240, 170);
            for (int y = 0; y < mock.Height; y++)
                for (int x = 0; x < mock.Width; x++) mock[x, y] = Rgba.Hex("#0A0807");
            void Slice(PixelImage frame, int border, int left, int top, int width, int height)
            {
                for (int y = 0; y < height; y++)
                {
                    int sy = y < border ? y : y >= height - border ? frame.Height - (height - y) : border + (y - border) % (frame.Height - 2 * border);
                    for (int x = 0; x < width; x++)
                    {
                        int sx = x < border ? x : x >= width - border ? frame.Width - (width - x) : border + (x - border) % (frame.Width - 2 * border);
                        var c = frame[sx, sy];
                        if (c.A > 0) mock[left + x, top + y] = c;
                    }
                }
            }
            Slice(UiArt.Panel, UiArt.PanelBorder, 8, 8, 224, 154);
            Slice(UiArt.Button(), UiArt.ButtonBorder, 18, 18, 40, 16);
            Slice(UiArt.Inset, UiArt.InsetBorder, 18, 44, 204, 14);
            Slice(UiArt.Button(), UiArt.ButtonBorder, 18, 66, 204, 18);
            Slice(UiArt.Button(hover: true), UiArt.ButtonBorder, 18, 88, 204, 18);
            Slice(UiArt.Button(pressed: true), UiArt.ButtonBorder, 18, 110, 204, 18);
            Slice(UiArt.Button(chosen: true), UiArt.ButtonBorder, 18, 132, 100, 18);
            Slice(UiArt.Button(), UiArt.ButtonBorder, 122, 132, 100, 18);
            return mock;
        }

        // ---- Skill icons ----

        // Each class's skills in a row, in sheet order. Fails when a shipped skill has no picture of its own (it would
        // show its kind's) or two skills look the same.
        static PixelImage Skills(ClassData classes, out bool fine)
        {
            fine = true;
            const int gap = 2;
            int columns = classes.Classes.Max(c => c.Skills.Count);
            var sheet = new PixelImage(gap + columns * (SkillArt.Size + gap), gap + (classes.Classes.Count + 1) * (SkillArt.Size + gap));
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Backdrop);
            // Under the classes: the dodge roll's slot, which is no class's skill (SkillBar).
            sheet.Draw(SkillArt.Roll(), gap, gap + classes.Classes.Count * (SkillArt.Size + gap));
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
            // Drawn in their own colours, not tinted: the samurai's warning sign, rocks and cherry blossom (2026-11-12).
            images.Add(new List<PixelImage> { EffectArt.WarningSign(), EffectArt.Rock(7), EffectArt.Rock(13), EffectArt.Petal(0), EffectArt.Petal(1), EffectArt.Petal(2) });
            // The lines the samurai's cuts leave (2026-11-13): straight, and bent round a wedge.
            images.Add(new List<PixelImage> { EffectArt.Slash(32), EffectArt.Slash(96), EffectArt.SlashArc(44, 150) });
            // What the silk mother leaves on the floor and throws (2026-11-13): webs, a pool of venom bubbling, gobs.
            images.Add(new List<PixelImage> { EffectArt.Web(61, 0), EffectArt.Web(61, 1), EffectArt.Web(90, 2), EffectArt.Glob(9, true), EffectArt.Glob(7, false) });
            images.Add(Enumerable.Range(0, EffectArt.VenomFrames).Select(f => EffectArt.VenomPool(48, f)).ToList());
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
                    TileType.CrackedWall, TileType.Grate, TileType.ObsidianWall, TileType.VoidWall, TileType.IceFloor, TileType.Quicksand,
                }
                .Select(TileArt.Object).Append(TileArt.OpenDoor).Append(TileArt.BigAssembler).Append(TileArt.BigSmelter).Concat(Enumerable.Range(0, ItemArt.BeltFrames).Select(f => ItemArt.Belt(f)))
                .Concat(new[] { ItemArt.Belt(0, fast: true), ItemArt.Underground(0), TileArt.MerchantCounter, ItemArt.Chicken(0), ItemArt.Chicken(1), ItemArt.Cow(0), ItemArt.Cow(1), ItemArt.Sheep(0), ItemArt.Sheep(1), ItemArt.TurretBarrel })
                .Concat(new[] { ItemArt.DrillBit, ItemArt.Chevron, ItemArt.SplitterArrows, ItemArt.RobotArm, ItemArt.SprinklerHead, ItemArt.HarvesterReel,
                    ItemArt.Flame(0), ItemArt.Flame(1), ItemArt.LeverHandle, ItemArt.WaterWheel })
                .Concat(Enumerable.Range(1, TileArt.CrackStages).Select(TileArt.Cracks)).ToList());

            var players = new List<PixelImage>();
            foreach (var look in new[] { Look.Down, Look.DownSide, Look.Side, Look.UpSide, Look.Up })
            {
                for (int frame = 0; frame < HeroArt.WalkFrames; frame++) players.Add(Hero(look, HeroMove.Walk, frame, "#3F7FD8"));
            }
            players.Add(Hero(Look.Down, HeroMove.Stand, 0, "#4DB06A"));
            players.Add(Hero(Look.Down, HeroMove.Stand, 0, "#E08A3C"));
            rows.Add(players);

            // The people, each beside their stall.
            var people = new List<PixelImage>();
            foreach (var npc in npcs.Npcs)
            {
                people.Add(PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), 0) ?? Hero(Look.Down, HeroMove.Stand, 0, npc.Color));
                people.Add(TileArt.Object(npc.Tile));
            }
            rows.Add(people);

            // Standing (the breath's frames), flinching and casting, in three directions. Every motion is in player.png
            // and anim/hero_*.
            var poses = new List<PixelImage>();
            foreach (var look in new[] { Look.Down, Look.Side, Look.Up })
            {
                for (int frame = 0; frame < HeroArt.FramesOf(HeroMove.Stand, HeroWeapon.None); frame += 2) poses.Add(Hero(look, HeroMove.Stand, frame, "#3F7FD8"));
                poses.Add(Hero(look, HeroMove.Hurt, HeroArt.StrikeFrame(HeroMove.Hurt, HeroWeapon.None), "#3F7FD8"));
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

        // The hero alone, standing in a tunic of a colour with nothing in hand, cut to the figure (sheets).
        static PixelImage Hero(Look look, HeroMove move, int frame, string tunic) => HeroArt.Figure(HeroArt.Draw(look, move, frame, HeroGear.None, Rgba.Hex(tunic)));

        // The hero as a scene stands them: the canvas down to the feet, a plain sword in hand.
        static PixelImage HeroStanding(Look look, HeroMove move, int frame, string tunic, HeroWeapon weapon = HeroWeapon.Sword)
        {
            var canvas = HeroArt.Draw(look, move, frame, HeroGear.Plain(weapon), Rgba.Hex(tunic));
            var result = new PixelImage(HeroArt.Size, HeroArt.FeetY);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                    result[x, y] = canvas[x, y];
            return result;
        }

        // Every look of the player (eight ways: the five drawn and three of them flipped), a row a look, in the telling
        // frames of every motion (2026-11-19, HeroArt): standing, the walk's eight, then with each weapon its three
        // attacks (wound up, the blow, held after), and with a sword the cast, the thrust, the turn, the flinch, and
        // with nothing drinking and lying down. Every frame of every motion is in anim/hero_*_frames_*.png.
        static PixelImage Players()
        {
            var tunic = Rgba.Hex("#3F7FD8");
            var columns = new List<(HeroMove Move, int Frame, HeroWeapon Weapon)> { (HeroMove.Stand, 0, HeroWeapon.Sword) };
            for (int frame = 0; frame < HeroArt.WalkFrames; frame++) columns.Add((HeroMove.Walk, frame, HeroWeapon.Sword));
            foreach (var weapon in new[] { HeroWeapon.Sword, HeroWeapon.Bow, HeroWeapon.Staff, HeroWeapon.Pick, HeroWeapon.None })
            {
                foreach (var move in new[] { HeroMove.Attack1, HeroMove.Attack2, HeroMove.Attack3 })
                {
                    int strike = HeroArt.StrikeFrame(move, weapon);
                    columns.Add((move, HeroArt.FrameAt(move, weapon, HeroArt.ChainSeconds(move, weapon)), weapon));
                    columns.Add((move, strike, weapon));
                    columns.Add((move, Math.Min(strike + 2, HeroArt.FramesOf(move, weapon) - 1), weapon));
                }
            }
            foreach (var move in new[] { HeroMove.Cast, HeroMove.Thrust, HeroMove.Spread, HeroMove.Hurt })
                columns.Add((move, Math.Max(HeroArt.StrikeFrame(move, HeroWeapon.Sword), HeroArt.FramesOf(move, HeroWeapon.Sword) / 3), HeroWeapon.Sword));
            columns.Add((HeroMove.Drink, HeroArt.FramesOf(HeroMove.Drink, HeroWeapon.None) / 2, HeroWeapon.None));
            columns.Add((HeroMove.Fall, HeroArt.FramesOf(HeroMove.Fall, HeroWeapon.None) - 1, HeroWeapon.None));
            var rows = new List<IList<PixelImage>>();
            foreach (var (look, flip) in new[] { (Look.Down, false), (Look.DownSide, false), (Look.Side, false), (Look.UpSide, false), (Look.Up, false),
                (Look.UpSide, true), (Look.Side, true), (Look.DownSide, true) })
            {
                var row = new List<PixelImage>();
                foreach (var (move, frame, weapon) in columns)
                {
                    var canvas = HeroArt.Draw(look, move, frame, HeroGear.Plain(weapon), tunic);
                    if (flip) canvas = canvas.FlippedX();
                    row.Add(canvas);
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
            scene.Machine(sx + 3, sy - 4, 0, BeltArt.Belt(false, 0, BeltArt.Straight, 0));
            scene.Machine(sx + 4, sy - 4, 0, BeltArt.Belt(false, 0, BeltArt.Straight, 0));
            scene.Machine(sx + 5, sy - 4, 0, ItemArt.Chevron);
            scene.Machine(sx + 6, sy - 4, 0, BeltArt.Belt(false, 0, BeltArt.Straight, 0));
            scene.Icon(sx + 3.5f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));
            scene.Icon(sx + 6.8f, sy - 3.5f, ItemArt.Material(loot.FindMaterial("copper_ore")));

            for (int i = 0; i < 4; i++) scene.Crop(sx - 5 + i, sy - 2, i);
            // Rock beside the drill, cracked by a few hits.
            scene.Machine(sx + 1, sy - 3, 0, TileArt.Cracks(2));
            scene.Machine(sx + 1, sy - 5, 0, TileArt.Cracks(TileArt.CrackStages));

            scene.Player(sx + 0.5f, sy + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(sx + 2.5f, sy + 1.2f, HeroStanding(Look.Side, HeroMove.Walk, 2, "#4DB06A"));
            scene.Player(sx - 1.5f, sy + 1.6f, HeroStanding(Look.Up, HeroMove.Walk, 2, "#E08A3C"));
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
            // Wide enough for its shops (2026-11-11), each with a stall at its counter as the game puts one.
            int half = site.Radius + (site.Shops.Count > 0 ? 15 : 5);
            var stalls = new[] { TileType.MerchantStall, TileType.SmithStall, TileType.FisherHut, TileType.AlchemyTable };
            for (int i = 0; i < site.Shops.Count; i++) world.Map[site.Shops[i].X, site.Shops[i].Y] = stalls[i % stalls.Length];
            var scene = new Scene(world.Map, site.X - half, site.Y - half, half * 2, half * 2, _ => false);
            scene.Lit(lights, null);
            scene.Places(world.Places);
            foreach (var (character, at) in site.People)
                scene.Player(at.X, at.Y - 0.3f, PeopleArt.Look(character.Look, Rgba.Hex(character.Color), 0));
            scene.Player(site.X + 0.5f, site.Y - 1.5f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
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

        // The rooms of each role as the game draws them (2026-11-21, Docs/WorldRework.md 방의 역할): up to three rooms
        // of a role with different chunks, a row to a role, once with the lights off and once as the dark cave shows
        // them (rooms_art.png, rooms_art_lit.png), and the six new tiles by themselves with the patches' edges
        // (room_tiles.png).
        static void RoomViews(GeneratedWorld world, WorldLights lights, string folder)
        {
            const int across = 26, up = 18, columns = 3;
            var roles = Enum.GetValues(typeof(RoomRole)).Cast<RoomRole>().Where(r => r != RoomRole.Plain).ToList();
            foreach (bool lit in new[] { false, true })
            {
                var sheet = new PixelImage(columns * (across * TileArt.Size + 4), roles.Count * (up * TileArt.Size + 4));
                sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#30303A"));
                for (int row = 0; row < roles.Count; row++)
                {
                    var rooms = world.Rooms.Nodes.Where(n => n.Role == roles[row] && (n.Chunk != null || roles[row] == RoomRole.Ore))
                        .GroupBy(n => n.Chunk?.Id ?? "").Select(g => g.First()).GroupBy(n => n.Biome).Select(g => g.First()).Take(columns).ToList();
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        var room = rooms[i];
                        var scene = new Scene(world.Map, room.X - across / 2, room.Y - up / 2, across, up, _ => false) { Biome = room.Biome };
                        if (lit) scene.Lit(lights, null);
                        scene.Places(world.Places);
                        scene.Player(room.AnchorX + 0.5f, room.AnchorY + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                        sheet.Draw(scene.Image, i * (across * TileArt.Size + 4) + 2, row * (up * TileArt.Size + 4) + 2);
                    }
                }
                Save(sheet, Path.Combine(folder, lit ? "rooms_art_lit.png" : "rooms_art.png"), 1);
            }
            var tiles = new[] { TileType.CrackedWall, TileType.Grate, TileType.ObsidianWall, TileType.VoidWall }.Select(type =>
            {
                // Over the rock it stands on, as in the world (the grate over floor).
                var tile = TileArt.Ground(TileArt.Index(type == TileType.Grate ? GroundKind.Floor : GroundKind.Rock, type == TileType.Grate ? 0 : TileArt.Up | TileArt.Left | TileArt.Right, 0)).Copy();
                var over = TileArt.Object(type);
                for (int y = 0; y < TileArt.Size; y++)
                    for (int x = 0; x < TileArt.Size; x++)
                        if (over.IsSet(x, y)) tile[x, y] = over[x, y];
                return tile;
            }).ToList();
            foreach (var type in new[] { TileType.IceFloor, TileType.Quicksand })
            {
                // A patch three by three with one corner tile left bare, on its biome's floor.
                var biome = type == TileType.IceFloor ? Biome.Ice : Biome.Sand;
                var map = new TileMap(5, 5, TileType.Floor);
                for (int y = 1; y <= 3; y++)
                    for (int x = 1; x <= 3; x++)
                        if (x != 3 || y != 3) map[x, y] = type;
                var patch = new PixelImage(5 * TileArt.Size, 5 * TileArt.Size);
                for (int y = 0; y < 5; y++)
                {
                    for (int x = 0; x < 5; x++)
                    {
                        patch.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x + y) % TileArt.Variants, biome)), x * TileArt.Size, (4 - y) * TileArt.Size);
                        if (map[x, y] == type) patch.Draw(RoomTileArt.Patch(type, RoomTileArt.PatchMask(map, x, y), RoomTileArt.Laying(x, y)), x * TileArt.Size, (4 - y) * TileArt.Size);
                    }
                }
                tiles.Add(patch);
            }
            Save(Row(tiles), Path.Combine(folder, "room_tiles.png"), 6);
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
            scene.Player(ax - 2.5f, ay - 3f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(ax + 3.5f, ay - 2.5f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#4DB06A").FlippedX());
            scene.Shot(ax - 1.5f, ay - 1.5f, ItemArt.Orb);
            scene.Shot(ax + 2f, ay - 0.5f, ItemArt.Spore);
            return scene.Image;
        }

        // Every boss's arena as its kind dresses it (arenas.csv, WorldGenerator.Arenas.cs), the boss in the middle and
        // someone walking in: one picture each (arenas/<boss>.png, lit as in the game; arenas/<boss>_unlit.png to see
        // the dressing plainly) and all of them on one sheet (arenas.png).
        static readonly (string Boss, ArenaKind Kind)[] ArenaBosses =
        {
            ("cave_warden", ArenaKind.Ruin), ("crystal_queen", ArenaKind.Nest), ("lava_titan", ArenaKind.Caldera), ("void_lord", ArenaKind.Altar),
            ("tide_serpent", ArenaKind.Flooded), ("antlion", ArenaKind.Pit), ("mad_general", ArenaKind.Warcamp), ("crown_wraith", ArenaKind.Chapel),
            ("vein_eater", ArenaKind.Mine), ("shrine_samurai", ArenaKind.Dojo), ("black_knight", ArenaKind.Hall), ("silk_mother", ArenaKind.Web),
        };

        static PixelImage BossPicture(string id) => id switch
        {
            "vein_eater" => VeinEaterArt.Draw(BossView.Front, VeinPose.Idle, 0, 1, false),
            "shrine_samurai" => SamuraiArt.Draw(BossView.Front, SamuraiPose.Idle, 0, 1),
            "black_knight" => KnightArt.Draw(BossView.Front, KnightPose.Idle, 0, 1),
            "cave_warden" => WardenArt.Draw(BossView.Front, WardenPose.Idle, 0, 1),
            "mad_general" => GeneralArt.Draw(BossView.Front, GeneralPose.Idle, 0, 1),
            "silk_mother" => SpiderArt.Draw(BossView.Front, SpiderPose.Idle, 0, 1),
            _ => CreatureArt.Boss(id, BossColor, 0),
        };

        // The same arenas where a real world has them (seed 7, the game's size, the story's places and roads): among
        // the caves, turned to their ways in, with the tunnels and roads that reach them (arenas_world.png, and
        // arenas/world_<boss>.png each).
        static ArenaData WorldArenaSheet(MonsterData monsters)
        {
            string bossFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Bosses"));
            var bosses = BossSheet.Parse(File.ReadAllText(Path.Combine(bossFolder, BossSheet.BossesFile)), File.ReadAllText(Path.Combine(bossFolder, BossSheet.AttacksFile)), monsters);
            return ArenaSheet.Parse(File.ReadAllText(Path.Combine(bossFolder, ArenaSheet.File)), bosses);
        }

        static void WorldArenas(WorldLights lights, string output, MonsterData monsters, StoryData story)
        {
            string bossFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Bosses"));
            var bosses = BossSheet.Parse(File.ReadAllText(Path.Combine(bossFolder, BossSheet.BossesFile)), File.ReadAllText(Path.Combine(bossFolder, BossSheet.AttacksFile)), monsters);
            var arenas = ArenaSheet.Parse(File.ReadAllText(Path.Combine(bossFolder, ArenaSheet.File)), bosses);
            foreach (string error in arenas.Errors) Console.Error.WriteLine(error);
            var world = WorldGenerator.Generate(7, WorldGenerator.GameSize, WorldGenerator.GameSize, story, arenas);
            var images = new List<PixelImage>();
            int cave = 0;
            foreach (var boss in bosses.Bosses)
            {
                Vec2 at;
                if (boss.Place == null) at = world.Arenas[cave++];
                else if (!world.PlaceArenas.TryGetValue(boss.Place, out at)) continue;
                int cx = TileMap.Cell(at.X), cy = TileMap.Cell(at.Y);
                var kind = world.ArenaKindAt(at) ?? ArenaKind.Plain;
                var scene = new Scene(world.Map, cx - 13, cy - 13, 27, 27, p => Vec2.Distance(p, at) <= WorldGenerator.ArenaRadius + 1) { Arena = kind };
                scene.Places(world.Places);
                scene.Lit(lights, WorldLights.ArenaAmbient(new[] { at }, WorldGenerator.ArenaRadius));
                scene.Body(at.X, at.Y, 1.2f, BossPicture(boss.Id));
                var image = scene.Image;
                Save(image, Path.Combine(output, "arenas", "world_" + boss.Id + ".png"), 2);
                images.Add(image);
            }
            int w = images[0].Width + 4, h = images[0].Height + 4;
            var sheet = new PixelImage(4 * w, (images.Count + 3) / 4 * h);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#0A0A0E"));
            for (int i = 0; i < images.Count; i++) sheet.Draw(images[i], i % 4 * w + 2, i / 4 * h + 2);
            Save(sheet, Path.Combine(output, "arenas_world.png"), 1);
        }

        static void Arenas(WorldLights lights, string output)
        {
            string folder = Path.Combine(output, "arenas");
            Directory.CreateDirectory(folder);
            var lit = new List<PixelImage>();
            var plain = new List<PixelImage>();
            foreach (var (boss, kind) in ArenaBosses)
            {
                var map = WorldGenerator.ArenaSample(kind, 7);
                int c = map.Width / 2;
                var middle = TileMap.Center(c, c);
                foreach (bool dark in new[] { true, false })
                {
                    var scene = new Scene(map, 1, 1, map.Width - 2, map.Height - 2, p => Vec2.Distance(p, middle) <= WorldGenerator.ArenaRadius + 1) { Arena = kind };
                    scene.Places(null);
                    if (dark) scene.Lit(lights, WorldLights.ArenaAmbient(new[] { middle }, WorldGenerator.ArenaRadius));
                    scene.Body(c + 0.5f, c + 0.5f, 1.2f, BossPicture(boss));
                    scene.Player(c + 0.5f, c - 6.5f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#3F7FD8"));
                    var image = scene.Image;
                    Save(image, Path.Combine(folder, boss + (dark ? ".png" : "_unlit.png")), 2);
                    (dark ? lit : plain).Add(image);
                }
            }
            foreach (var (images, name) in new[] { (lit, "arenas.png"), (plain, "arenas_unlit.png") })
            {
                int w = images[0].Width + 4, h = images[0].Height + 4;
                var sheet = new PixelImage(4 * w, 3 * h);
                sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#0A0A0E"));
                for (int i = 0; i < images.Count; i++) sheet.Draw(images[i], i % 4 * w + 2, i / 4 * h + 2);
                Save(sheet, Path.Combine(output, name), 1);
            }
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
            // The kind of the arena in the scene (its floor's stone).
            public ArenaKind Arena = ArenaKind.Plain;
            // The biome whose colours the ground is drawn in.
            public Biome Biome = Biome.Cave;
            readonly List<(float Y, Action<PixelImage> Draw)> _bodies = new List<(float, Action<PixelImage>)>();
            IReadOnlyList<PlaceSite> _places;

            // The places of the story: their floors under things, and what stands on the floor (structures, what players
            // build that stands, fences, hedges, walls, gates), each by its foot (StandArt, as WorldView's standing layer).
            public void Places(IReadOnlyList<PlaceSite> places)
            {
                _places = places;
                for (int y = _bottom - 4; y < _bottom + _height + 1; y++)
                {
                    for (int x = _left - 4; x < _left + _width + 1; x++)
                    {
                        if (!_map.InBounds(x, y)) continue;
                        var kind = StandArt.KindAt(_map, x, y);
                        if (kind == StandArt.Kind.None) continue;
                        var type = _map[x, y];
                        bool open = (kind == StandArt.Kind.Gate || kind == StandArt.Kind.Door) && Open != null && Open(x, y);
                        int shape = StandArt.ShapeAt(_map, x, y, open, BiomeAt?.Invoke(x, y));
                        var image = StandArt.Picture(type, kind, shape, Night);
                        if (image == null) continue;
                        int pivot = StandArt.PivotX(kind, shape, image);
                        var (w, _) = Structures.Footprint(type);
                        float cx = x + w / 2f;
                        float foot = y + StandArt.FootLift(type, kind);
                        int rows = StandArt.CanopyRows(type);
                        var canopy = StandArt.Canopy(cx, y, image.Width, image.Height, rows);
                        var (px, py) = Corner(cx, y);
                        _bodies.Add((Key(foot, StandArt.PropTie), canvas =>
                        {
                            // A canopy or roof fades while someone stands behind it (StandArt.Hides), as in the game.
                            bool faded = Fade && rows > 0 && _feet.Any(f => StandArt.Hides(f.X, f.Y, foot, canopy));
                            if (faded) Faded++;
                            for (int j = 0; j < image.Height; j++)
                            {
                                float alpha = faded && j < rows ? StandArt.FadeAlpha : 1;
                                for (int i = 0; i < image.Width; i++)
                                {
                                    var c = image[i, j];
                                    if (c.A > 0) canvas.Blend(px - pivot + i, py - image.Height + j, alpha >= 1 ? c : c.WithAlpha((int)(c.A * alpha)));
                                }
                            }
                        }));
                    }
                }
            }

            // The floor's decor of a seed (Decor, StandArt.DecorAt), kept off the places as the game keeps it.
            public void Decor(ulong seed, Func<int, int, Biome> biome)
            {
                for (int y = _bottom - 1; y < _bottom + _height + 1; y++)
                {
                    for (int x = _left - 1; x < _left + _width + 1; x++)
                    {
                        if (!_map.InBounds(x, y)) continue;
                        var tileBiome = biome(x, y);
                        var pick = StandArt.DecorAt(_map, seed, tileBiome, x, y, (cx, cy) => InPlace(cx, cy));
                        if (!pick.Any) continue;
                        var image = StandArt.DecorPicture(pick, tileBiome);
                        var (fx, fy) = StandArt.DecorFoot(x, y, pick);
                        var (px, py) = Corner(fx, fy - StandArt.DecorDrop);
                        DecorShown++;
                        // What lies flat (moss, threads, a cobweb) is under everything that stands, as in the game.
                        if (Game.Core.Decor.Flat(pick.Kind)) _overlays.Add(canvas => canvas.Draw(image, px - image.Width / 2, py - image.Height));
                        else _bodies.Add((Key(fy, StandArt.DecorTie), canvas => canvas.Draw(image, px - image.Width / 2, py - image.Height)));
                    }
                }
            }

            bool InPlace(int x, int y)
            {
                if (_places == null) return false;
                foreach (var site in _places)
                {
                    int dx = x - site.X, dy = y - site.Y;
                    if (dx * dx + dy * dy <= (site.Radius + 2) * (site.Radius + 2)) return true;
                }
                return false;
            }

            // Back to front: the higher the foot the further back, and at one row decor, then props, then bodies.
            static float Key(float foot, int tie) => foot + (StandArt.Ties - tie) * 0.0001f;

            // Whether a gate stands open, the houses' windows lit, canopies fading; how many faded and how much decor.
            public Func<int, int, bool> Open;
            // The biome of a tile, for the things drawn by it (the places' tall tree); null draws them as they were.
            public Func<int, int, Biome> BiomeAt;
            public bool Night = true;
            public bool Fade = true;
            public int Faded;
            public int DecorShown;
            readonly List<(float X, float Y)> _feet = new List<(float, float)>();
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
                            image.Draw(TileArt.Ground(TileArt.GroundAt(_map, x, y, _inArena(TileMap.Center(x, y)), Biome, Arena)), px, py);
                            // The floor that runs under things (PropArt.Underlay), as WorldView's underlay layer draws it.
                            var under = PropArt.Underlay(_map, _places, x, y);
                            if (under != null) image.Draw(TileArt.Object(under.Value), px, py);
                            // Things that stand (StandArt.KindAt) are drawn with the bodies, as WorldView's standing layer does.
                            var placed = StandArt.KindAt(_map, x, y) != StandArt.Kind.None || PropArt.Stands(_map[x, y]) || _map[x, y] == TileType.Water ? null
                                : _map[x, y] == TileType.Road ? TileArt.Road(TileArt.RoadVariant(_map, x, y))
                                : _map[x, y] == TileType.Rail ? RailArt.Rail(RailArt.Mask(_map, x, y))
                                : RoomTileArt.IsPatch(_map[x, y]) ? RoomTileArt.Patch(_map[x, y], RoomTileArt.PatchMask(_map, x, y), RoomTileArt.Laying(x, y))
                                : World.IsBelt(_map[x, y]) ? BeltArt.Bed
                                : _map[x, y] == TileType.Splitter || _map[x, y] == TileType.Filter ? BeltArt.Housing(_map[x, y])
                                : FlatArt.LaidIndex(_map[x, y]) >= 0 ? FlatArt.Tile(_map[x, y], FlatArt.Mask(_map, x, y))
                                : FlatArt.BlockIndex(_map[x, y]) >= 0 ? FlatArt.Block(_map[x, y], FlatArt.BlockMask(_map, x, y))
                                : TileArt.Object(_map[x, y]);
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
            public void Lit(WorldLights lights, Func<int, int, float> ambient, Func<int, int, Rgba> tint = null, Func<int, int, Rgba?> recolor = null)
            {
                _lit = true;
                _lights = lights;
                _ambient = ambient;
                _tint = tint;
                _recolor = recolor;
            }

            Func<int, int, Rgba> _tint;
            // The lights burning another colour (a dungeon gate's, B1c: WorldLights.GateColors).
            Func<int, int, Rgba?> _recolor;

            // The light map stretched over the scene with bilinear smoothing between tile centres, as the view's
            // texture is, then laid over it.
            void Darken(PixelImage image)
            {
                const int margin = 4;
                int size = Math.Max(_width, _height) + margin * 2;
                int left = _left - margin;
                int bottom = _bottom - margin;
                var sources = _carriers.Select(WorldLights.Carried).ToList();
                _lights.AddTiles(sources, _map, left, bottom, size, 0, BiomeAt, _recolor);
                var lightMap = new LightMap(size);
                lightMap.Compute(_map, left, bottom, sources, _ambient, _tint);
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

            // A sprite standing from its foot at (x, foot), sorted by it (a big machine's body, a stall's keeper).
            public void Stand(float x, float foot, PixelImage sprite, int tie)
            {
                var (px, py) = Corner(x, foot);
                _bodies.Add((Key(foot, tie), image => image.Draw(sprite, px - sprite.Width / 2, py - sprite.Height)));
            }

            // A sprite centred on (x, y), sorted as if its foot were at `foot` (a machine's part on its raised body).
            public void Centered(float x, float y, PixelImage sprite, float foot, int tie)
            {
                var (px, py) = Corner(x, y);
                _bodies.Add((Key(foot, tie) - 0.00001f, image => image.Draw(sprite, px - sprite.Width / 2, py - sprite.Height / 2)));
            }

            // A sprite laid on the floor, centred on (x, y), under everything that stands.
            public void Flat(float x, float y, PixelImage sprite)
            {
                var (px, py) = Corner(x, y);
                _overlays.Add(image => image.Draw(sprite, px - sprite.Width / 2, py - sprite.Height / 2));
            }

            // Bodies stand with their feet a little below their centre, as in WorldView.Place.
            public void Body(float x, float y, float radius, PixelImage sprite)
            {
                var (px, py) = Corner(x, y);
                int feet = py + (int)MathF.Round(radius * 0.8f * Px);
                var shadow = CreatureArt.Shadow(Math.Max(6, (int)MathF.Round(radius * 2 * Px * 0.9f)));
                _feet.Add((x, y - radius * 0.8f));
                _bodies.Add((Key(y - radius * 0.8f, StandArt.BodyTie), image =>
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

        // A world as the game makes one (seed 1, rooms), for the map's pictures (UiPreview.Records.cs).
        static GeneratedWorld RecordsWorld(StoryData story, MonsterData monsters, TerrainData terrain) =>
            WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, story, WorldArenaSheet(monsters), WorldGenerator.RoomVersion, terrain);

        // The classes, skills and passive tree, for the growth screens' pictures (UiPreview.Growth.cs).
        static ClassData LoadClassesWithTree()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            return ClassSheet.Parse(File.ReadAllText(Path.Combine(folder, ClassSheet.ClassesFile)), File.ReadAllText(Path.Combine(folder, ClassSheet.SkillsFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.PassivesFile)));
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
                Read(LootSheets.SetsFile),
                Read(WeaponKind.File));
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
