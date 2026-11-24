using System;
using Game.Core;

namespace Game.Client
{
    // Which things join their neighbours into one line (Docs/BiomeRework.md 4-4, 묶음 B0): a fence, a hedge, an iron
    // wall and the places' walls. A side is joined (N 1, E 2, S 4, W 8, the bits PropArt.Wall takes) when the tile
    // that way is one this kind runs on into: a fence into fences, its gates and walls; a hedge into hedges; a wall into
    // its own kind. A door with fences on two opposite sides is the fence's gate (drawn as a gate, still a door to the
    // game: players pass, monsters do not, a pen counts it shut). Only the drawing reads this: the map, the rules and
    // the saves are as before.
    static class Joins
    {
        public const int N = 1, E = 2, S = 4, W = 8;

        // The kinds that are drawn joined.
        public static bool Joined(TileType type) => type == TileType.Fence || type == TileType.Hedge || type == TileType.IronWall
            || type == TileType.CastleWall || type == TileType.TimberWall
            // A trap's grate and the hung walls stand and join too (2026-11-25, PropArt.Stood.cs).
            || type == TileType.Grate || Built(type) && type != TileType.IronWall;

        static bool Wall(TileType type) => type == TileType.CastleWall || type == TileType.TimberWall || Built(type);

        // The walls players build: iron plates and the two hung with something. They run on into each other.
        static bool Built(TileType type) => type == TileType.IronWall || type == TileType.TapestryWall || type == TileType.ShieldWall;

        // Which sides of the tile at (x, y) join: north 1, east 2, south 4, west 8.
        public static int Mask(TileMap map, int x, int y)
        {
            var here = map[x, y];
            if (TileMap.IsDoor(here)) return Gate(map, x, y) is bool along ? (along ? E | W : N | S) : 0;
            return (Runs(map, here, x, y + 1) ? N : 0) | (Runs(map, here, x + 1, y) ? E : 0) | (Runs(map, here, x, y - 1) ? S : 0) | (Runs(map, here, x - 1, y) ? W : 0);
        }

        static bool Runs(TileMap map, TileType here, int x, int y)
        {
            var there = map[x, y];
            return here switch
            {
                TileType.Fence => there == TileType.Fence || Wall(there) || TileMap.IsDoor(there) && Gate(map, x, y) != null,
                // A grate closes a way through rock: it runs on into the rock either side.
                TileType.Grate => there == TileType.Grate || TileArt.IsRock(there),
                _ when Built(here) => Built(there),
                _ => there == here,
            };
        }

        // Whether the tile is a door standing in a fence: true across an east-west run, false in a north-south one, null
        // when it is not a gate (a door in a wall, or alone).
        public static bool? Gate(TileMap map, int x, int y)
        {
            if (!TileMap.IsDoor(map[x, y])) return null;
            if (map[x - 1, y] == TileType.Fence && map[x + 1, y] == TileType.Fence) return true;
            if (map[x, y - 1] == TileType.Fence && map[x, y + 1] == TileType.Fence) return false;
            return null;
        }

        // Which way a door that is not a fence's gate stands: true across an east-west run (walls east and west of it,
        // or nothing to go by: seen from the front), false in a north-south one (walls north and south only).
        public static bool DoorAlong(TileMap map, int x, int y)
        {
            bool Shut(int nx, int ny) => !map.InBounds(nx, ny) || map.IsSolid(nx, ny) && !TileMap.IsDoor(map[nx, ny]);
            bool across = Shut(x, y + 1) && Shut(x, y - 1), along = Shut(x - 1, y) && Shut(x + 1, y);
            return along || !across;
        }

        // How many of the four sides join.
        public static int Count(int mask) => (mask & 1) + (mask >> 1 & 1) + (mask >> 2 & 1) + (mask >> 3 & 1);
    }

    // The joined pictures (Docs/BiomeRework.md 4-4 조각 다섯으로 16모양): one picture a tile, 16 wide and 24 tall (eight
    // pixels of headroom over the tile), from five pieces by the mask: the post (always), the rails east and west (their
    // faces, seen from the front) and the strips north and south (only the rails' tops, seen from above: a north-south run
    // shows its posts more than its rails, which is how three-quarter view sees it, not a sideways run turned). Drawn back
    // to front: the north strip, the post, the rails, the south strip. An end (one neighbour) has a taller round head, a
    // fence alone is a stake. Each sorts by its post's foot (FenceFoot), so a person just behind a run has their legs
    // hidden by it and one just in front stands over it. The look the person chose from the mockups (BiomeMockKit).
    static partial class PropArt
    {
        // Rows of headroom over the tile, and the row of the tile a post stands on.
        public const int FenceHead = 8;
        public const int FencePost = 9;
        // How far above the tile's bottom edge a fence sorts (its posts' feet), in pixels.
        public const int FenceFoot = T - 1 - FencePost;
        // A hedge's mass ends this far above the tile's bottom edge.
        public const int HedgeFoot = 2;

        static readonly Rgba FenceLight = Rgba.Hex("#E6B878"), FenceMain = Rgba.Hex("#B88448"), FenceShade = Rgba.Hex("#8A5C30"),
            FenceDark = Rgba.Hex("#5A3A20"), FenceInk = Rgba.Hex("#2A1A12");
        static readonly Rgba GateLight = Rgba.Hex("#F2D494"), GateMain = Rgba.Hex("#D0A05C"), GateShade = Rgba.Hex("#9E7038");
        static readonly Rgba Hasp = Rgba.Hex("#C8CCD8"), HaspShade = Rgba.Hex("#7A8090");

        // The crisp oval under a standing thing (Docs/BiomeRework.md 4-3 그림자): as wide as asked, a third as tall, black
        // at alpha 105 (0.41), hard-edged, straight down and never cast to a side.
        public static void Shade(PixelImage image, float cx, float cy, float width, int alpha = 105)
        {
            if (width < 2) return;
            float rx = width / 2f, ry = Math.Max(1.2f, width * 0.35f / 2f);
            for (int y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
            {
                for (int x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
                {
                    float u = (x + 0.5f - cx) / rx, v = (y + 0.5f - cy) / ry;
                    if (u * u + v * v <= 1f) image.Blend(x, y, new Rgba(0, 0, 0, alpha));
                }
            }
        }

        static void F(PixelImage g, int x, int y, Rgba c) => g[x, y + FenceHead] = c;

        static void FencePostAt(PixelImage g, int x, int top, int foot, bool round = false)
        {
            for (int y = top; y <= foot; y++)
            {
                F(g, x - 1, y, FenceInk);
                F(g, x + 4, y, FenceInk);
                F(g, x, y, FenceMain);
                F(g, x + 1, y, FenceMain);
                F(g, x + 2, y, FenceShade);
                F(g, x + 3, y, FenceShade);
            }
            F(g, x, top + 1, FenceLight);
            for (int i = 0; i < 4; i++)
            {
                F(g, x + i, top, FenceLight);
                F(g, x + i, top - 1, FenceInk);
                // The contact row: the darkest of the wood where it meets the floor.
                F(g, x + i, foot, FenceDark);
            }
            if (!round) return;
            F(g, x, top, FenceInk);
            F(g, x + 3, top, FenceInk);
            F(g, x - 1, top, Rgba.Clear);
            F(g, x + 4, top, Rgba.Clear);
            F(g, x, top - 1, Rgba.Clear);
            F(g, x + 3, top - 1, Rgba.Clear);
            F(g, x + 1, top - 1, FenceLight);
            F(g, x + 2, top - 1, FenceLight);
            F(g, x + 1, top - 2, FenceInk);
            F(g, x + 2, top - 2, FenceInk);
        }

        // Two rails seen from the front: a lit top edge over a face.
        static void FenceRails(PixelImage g, int x0, int x1)
        {
            foreach (int r in new[] { 0, 5 })
            {
                for (int x = x0; x <= x1; x++)
                {
                    F(g, x, r - 1, FenceInk);
                    F(g, x, r, FenceLight);
                    F(g, x, r + 1, FenceMain);
                    F(g, x, r + 2, FenceShade);
                    F(g, x, r + 3, FenceInk);
                }
            }
        }

        // The rails running away north: only their tops show, from the foot of the post behind to this post's cap.
        static void FenceNorth(PixelImage g)
        {
            for (int y = -6; y <= 0; y++)
            {
                F(g, 6, y, FenceInk);
                F(g, 7, y, FenceLight);
                F(g, 8, y, FenceShade);
                F(g, 9, y, FenceInk);
            }
        }

        // The rails leaving south, over the front of this post.
        static void FenceSouth(PixelImage g)
        {
            for (int y = 0; y <= FencePost; y++)
            {
                F(g, 7, y, FenceLight);
                F(g, 8, y, FenceMain);
            }
        }

        // A wooden fence's tile by which sides join (Joins.Mask).
        public static PixelImage Fence(int mask)
        {
            var g = new PixelImage(T, T + FenceHead);
            int bits = Joins.Count(mask);
            Shade(g, 8, FenceHead + FencePost, 7);
            if ((mask & Joins.N) != 0) FenceNorth(g);
            // Alone it is a short stake; an end has a taller round head.
            FencePostAt(g, 6, bits == 0 ? 2 : bits == 1 ? -4 : -3, FencePost, bits == 1);
            if ((mask & Joins.E) != 0) FenceRails(g, 11, 15);
            if ((mask & Joins.W) != 0) FenceRails(g, 0, 4);
            if ((mask & Joins.S) != 0) FenceSouth(g);
            return g;
        }

        // How far left of the picture's edge a gate's tile starts: an open gate in a north-south run swings its leaf
        // east over the next tile, so its picture is wider than the tile.
        public const int GateSwing = 12;

        public static int GateWidth(bool along, bool open) => !along && open ? T + GateSwing : T;

        // A gate in a straight run of fence (Joins.Gate): along, in an east-west run; open while someone is in it.
        public static PixelImage FenceGate(bool along, bool open)
        {
            var g = new PixelImage(GateWidth(along, open), T + FenceHead);
            if (along)
            {
                Shade(g, 2, FenceHead + FencePost, 6);
                Shade(g, 14, FenceHead + FencePost, 6);
                FencePostAt(g, 0, -3, FencePost);
                FencePostAt(g, 12, -3, FencePost);
                if (!open)
                {
                    // The leaf: upright planks between the posts, a brace across and the hasp.
                    for (int x = 4; x <= 11; x++)
                    {
                        F(g, x, -2, FenceInk);
                        for (int y = -1; y <= 7; y++) F(g, x, y, x == 7 ? GateShade : (x - 4) % 4 == 0 ? GateLight : GateMain);
                        F(g, x, -1, GateLight);
                        F(g, x, 7, GateShade);
                        F(g, x, 8, FenceInk);
                        F(g, x, 1, GateShade);
                        F(g, x, 5, GateShade);
                    }
                    F(g, 10, 3, Hasp);
                    F(g, 11, 3, HaspShade);
                }
                else
                {
                    // Swung toward the viewer on the west post: its edge, running down the screen.
                    for (int y = -1; y <= 13; y++)
                    {
                        F(g, 4, y, GateLight);
                        F(g, 5, y, GateMain);
                        F(g, 6, y, GateShade);
                        F(g, 7, y, FenceInk);
                    }
                    for (int x = 4; x <= 6; x++)
                    {
                        F(g, x, -2, FenceInk);
                        F(g, x, 14, FenceInk);
                        F(g, x, 4, GateShade);
                        F(g, x, 9, GateShade);
                    }
                    Shade(g, 6, FenceHead + 15, 5, 80);
                }
                return g;
            }
            // North-south: a post at the north end (over the tile's top edge) and one at the south end.
            Shade(g, 8, FenceHead + 15, 6);
            FencePostAt(g, 6, -8, -1);
            if (!open)
            {
                // The leaf seen from above: a wide strip of planks, darker every fourth row where a brace crosses.
                for (int y = -6; y <= 9; y++)
                {
                    F(g, 5, y, FenceInk);
                    F(g, 6, y, GateLight);
                    F(g, 7, y, GateLight);
                    F(g, 8, y, GateMain);
                    F(g, 9, y, GateShade);
                    F(g, 10, y, FenceInk);
                    if (y % 4 == 0)
                    {
                        F(g, 8, y, GateShade);
                        F(g, 9, y, FenceDark);
                    }
                }
            }
            else
            {
                // Swung east from the north post, so its face shows, standing on the tile's north edge and reaching over
                // the tile beside: planks across and a brace.
                const int left = 10, right = T + GateSwing - 2;
                for (int x = left; x <= right; x++)
                {
                    F(g, x, -8, FenceInk);
                    for (int y = -7; y <= -2; y++) F(g, x, y, (x - left) % 4 == 3 ? GateShade : (x - left) % 4 == 0 ? GateLight : GateMain);
                    F(g, x, -7, GateLight);
                    F(g, x, -4, GateShade);
                    F(g, x, -2, FenceDark);
                    F(g, x, -1, FenceInk);
                }
                for (int y = -8; y <= -1; y++) F(g, right + 1, y, FenceInk);
                Shade(g, (left + right) / 2f + 1, FenceHead, right - left - 2, 70);
            }
            FencePostAt(g, 6, 6, 15);
            return g;
        }

        // A clipped hedge that joins its neighbours: no post, one leafy mass whose arms reach the tile's edges where it
        // goes on, its top lit, ends round. Its mass is raised over its footprint by its height; the face shows below the
        // top's front edge, and a crisp shade lies under the face.
        public static PixelImage JoinedHedge(int mask)
        {
            const int height = 7;
            const float arm = 4.5f, node = 5.6f;
            bool Foot(int x, int y)
            {
                float dx = x + 0.5f - 8, dy = y + 0.5f - 8.5f;
                if (x < 0 || x >= T || y < 0 || y >= T) return false;
                if (dx * dx + dy * dy <= node * node) return true;
                if ((mask & Joins.N) != 0 && Math.Abs(dx) <= arm && dy <= 0) return true;
                if ((mask & Joins.S) != 0 && Math.Abs(dx) <= arm && dy >= 0) return true;
                if ((mask & Joins.W) != 0 && Math.Abs(dy) <= arm && dx <= 0) return true;
                if ((mask & Joins.E) != 0 && Math.Abs(dy) <= arm && dx >= 0) return true;
                return false;
            }
            bool Mass(int x, int y)
            {
                for (int k = 0; k <= height; k++)
                    if (Foot(x, y + k)) return true;
                return false;
            }
            var g = new PixelImage(T, T + FenceHead);
            Rgba light = Rgba.Hex("#8CC850"), main = Rgba.Hex("#5E9A3A"), shade = Rgba.Hex("#3E7030"), dark = Rgba.Hex("#2A4E2A"), ink = Rgba.Hex("#16241A");
            // The shade under the face first, so the mass covers what of it falls under itself.
            for (int x = 0; x < T; x++)
            {
                int bottom = -1;
                for (int y = T - 1; y >= 0 && bottom < 0; y--)
                    if (Foot(x, y)) bottom = y;
                if (bottom < 0) continue;
                g.Blend(x, bottom + 1 + FenceHead, new Rgba(0, 0, 0, 105));
                g.Blend(x, bottom + 2 + FenceHead, new Rgba(0, 0, 0, 60));
            }
            for (int y = -FenceHead; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    if (!Mass(x, y)) continue;
                    bool cutWest = (mask & Joins.W) != 0 && x == 0, cutEast = (mask & Joins.E) != 0 && x == T - 1;
                    if (Foot(x, y + height))
                    {
                        int toFront = 0;
                        while (toFront < 3 && Foot(x, y + height + toFront + 1)) toFront++;
                        bool edge = !cutWest && !Mass(x - 1, y) || !cutEast && !Mass(x + 1, y) || !Mass(x, y - 1) && !((mask & Joins.N) != 0 && y - 1 < -FenceHead + 1);
                        var color = toFront < 2 ? (PixelNoise.Value(x, y, 61) > 0.3f ? light : main)
                            : PixelNoise.Value(x, y, 61) > 0.72f ? light : PixelNoise.Value(x, y, 62) > 0.8f ? shade : main;
                        F(g, x, y, edge ? ink : color);
                    }
                    else
                    {
                        int toGround = 0;
                        while (Mass(x, y + toGround + 1)) toGround++;
                        bool edge = !cutWest && !Mass(x - 1, y) || !cutEast && !Mass(x + 1, y);
                        // The lowest row is the contact row, the darkest green.
                        var color = toGround == 0 ? ink : PixelNoise.Value(x, y, 63) > 0.62f ? dark : PixelNoise.Value(x, y, 64) > 0.85f ? main : shade;
                        F(g, x, y, edge ? ink : color);
                    }
                }
            }
            return g;
        }

        // A wall of iron plates that joins its own kind (TileType.IronWall, Docs/Defense.md): a wall block like the
        // places' (Wall), in riveted plates.
        static PixelImage IronPlates(int sides)
        {
            var image = Wall(sides, Iron, castle: false);
            const int height = 8;
            // Rivets at the plates' corners on the top and the face.
            for (int x = 1; x < T - 1; x += 4)
            {
                foreach (int y in new[] { 2, 13 })
                    if (image[x, y].A > 0 && image[x, y].R != Ink.R) image[x, y] = Iron.Shade;
                if (image[x, T + 2].A > 0) image[x, T + 2] = Iron.Light;
                if (image[x, T + height - 3].A > 0) image[x, T + height - 3] = Iron.Light;
            }
            return image;
        }
    }
}
