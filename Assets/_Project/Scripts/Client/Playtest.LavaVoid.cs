using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The lava fields' and the void abyss's monsters, rules and dungeons for the playtests (2026-11-25, Docs/BiomeRework.md
    // B4b, Docs/Playtest.md b4mobs): the scenes "b4monsters", "vents", "plates", "platewalk" and "splitter", and the "b4" JSON.
    static partial class Playtest
    {
        static readonly string[] LavaVoidLineup = { "void_mote", "salamander", "obsidian_beetle", "forge_slime", "abyss_stalker", "void_eye", "star_slime", "shadow_stalker",
            "magma_hurler", "void_splitter", "forge_keeper", "gate_warden" };

        // The scenes of B4b; false for a name of another.
        static bool LavaVoidScene(GameRoot root, string scene)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            void Clear(int left, int right, int down, int up)
            {
                foreach (var monster in world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 30)) monster.Health = 0;
                for (int dy = down; dy <= up; dy++)
                    for (int dx = left; dx <= right; dx++)
                        if (dx != 0 || dy != 0) world.Build(px + dx, py + dy, TileType.Floor);
            }
            switch (scene)
            {
                // The two biomes' kinds (B4b) on cleared floor east of the player, each where a body its size fits, 5000
                // health, awake (the mid-bosses as themselves, not statues), the monsters held: the small and middle ones
                // in a row level with the player, the large ones in a second row four tiles south (one row ran past what
                // a small room fits on a phone's walk, 2026-11-25 web check).
                case "b4monsters":
                {
                    Clear(-1, 16, -7, 2);
                    world.MonstersHold = true;
                    float small = 1.5f, large = 1.5f;
                    foreach (var id in LavaVoidLineup)
                    {
                        var kind = root.MonsterSheet.Find(id);
                        bool big = kind.Size == MonsterSize.L || kind.Size == MonsterSize.XL;
                        ref float along = ref big ? ref large : ref small;
                        along += kind.Radius;
                        var made = world.SpawnFitting(kind, player.Position + new Vec2(along, big ? -4 : 0));
                        if (made != null)
                        {
                            made.MaxHealth = made.Health = 5000;
                            (made.TargetId, made.State) = (player.Id, EnemyState.Chase);
                        }
                        along += kind.Radius + 0.8f;
                    }
                    return true;
                }
                // Steam vents (B4b): open floor, three vents in a row three tiles north of the player, the middle one
                // hissing at once.
                case "vents":
                    Clear(-5, 5, -2, 5);
                    for (int dx = -3; dx <= 3; dx += 3) world.Build(px + dx, py + 3, TileType.SteamVent);
                    world.PuffVent(px, py + 3);
                    return true;
                // Cracked plates (B4b): open floor, a strip of plates three wide from the player's tile six tiles east (stand
                // on one a second and it gives way; walk across and it holds).
                case "plates":
                    Clear(-3, 9, -3, 3);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = 0; dx <= 5; dx++) world.Build(px + dx, py + dy, TileType.CrackedPlate);
                    return true;
                // The same strip a tile ahead of the player, so the walk across it starts on firm floor (a phone's
                // stick takes a moment to get going, and the plate under the player gave way first, 2026-11-25).
                case "platewalk":
                    Clear(-3, 9, -3, 3);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = 1; dx <= 6; dx++) world.Build(px + dx, py + dy, TileType.CrackedPlate);
                    return true;
                // A void splitter (B4b) two tiles east on open floor, held, low enough to fall to a few blows: it comes
                // apart into two shards, and they into motes.
                case "splitter":
                {
                    Clear(-2, 9, -4, 4);
                    world.MonstersHold = true;
                    var splitter = world.SpawnFitting(root.MonsterSheet.Find("void_splitter"), TileMap.Center(px + 2, py), 1);
                    if (splitter != null) splitter.Health = 20;
                    return true;
                }
            }
            return false;
        }

        // The two biomes' monsters and rules about the player (B4b).
        static string LavaVoidJson(GameRoot root)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            var list = new Json('[');
            foreach (var monster in world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < 24).OrderBy(e => Vec2.Distance(e.Position, player.Position)))
            {
                var kind = monster.Definition;
                var drawn = root.MonsterDrawn(monster.Id);
                list.Item(new RawJson(new Json('{')
                    .Field("id", monster.Id).Field("kind", kind.Id).Field("size", MonsterSizes.Name(kind.Size))
                    .Field("x", monster.Position.X).Field("y", monster.Position.Y)
                    .Field("distance", Vec2.Distance(monster.Position, player.Position))
                    .Field("health", monster.Health).Field("maxHealth", monster.MaxHealth).Field("state", monster.State.ToString())
                    .Field("dormant", World.IsDormant(monster)).Field("elite", monster.Elite.ToString())
                    .Field("drawn", drawn != null).Field("pixels", drawn?.Width ?? 0)
                    .Close('}')));
            }
            int ux = TileMap.Cell(player.Position.X), uy = TileMap.Cell(player.Position.Y);
            int vents = 0, plates = 0, holes = 0;
            for (int y = uy - 9; y <= uy + 9; y++)
                for (int x = ux - 9; x <= ux + 9; x++)
                {
                    var tile = world.Map[x, y];
                    if (tile == TileType.SteamVent) vents++;
                    else if (tile == TileType.CrackedPlate) plates++;
                    else if (tile == TileType.BrokenPlate) holes++;
                }
            var gates = new Json('[');
            foreach (var gate in world.Gates.Where(g => !g.Cellar && (g.Biome == Biome.Lava || g.Biome == Biome.Abyss)))
                gates.Item(new RawJson(new Json('{').Field("biome", gate.Biome.ToString()).Field("dungeon", world.DungeonFor(gate)?.Id ?? "")
                    .Field("master", world.DungeonFor(gate)?.Boss ?? "").Field("light", world.DungeonFor(gate)?.Light ?? "").Close('}')));
            return new Json('{').Field("monsters", list.Raw(']'))
                .Field("biome", world.Biomes.At(player.Position).ToString()).Field("under", world.Map[ux, uy].ToString())
                .Field("health", player.Health).Field("maxHealth", player.Stats.MaxHealth)
                .Field("vents", vents).Field("plates", plates).Field("holes", holes)
                .Field("ventsBurst", world.VentsBurst).Field("ventBurns", world.VentBurns)
                .Field("platesBroken", world.PlatesBroken).Field("plateFalls", world.PlateFalls).Field("stood", world.PlateStood(ux, uy))
                .Field("ventsShown", root.VentsShown).Field("burstsShown", root.VentBurstsShown).Field("crumblesShown", root.CrumblesShown)
                .Field("fallsShown", root.FallsShown).Field("platesBackShown", root.PlatesBackShown).Field("cracksShown", root.CracksShown)
                .Field("abyssLightsDimmed", root.AbyssLightsDimmed)
                .Field("generator", root.HostGenerated?.Version ?? 0)
                .Field("gates", gates.Raw(']'))
                .Close('}');
        }
    }
}
