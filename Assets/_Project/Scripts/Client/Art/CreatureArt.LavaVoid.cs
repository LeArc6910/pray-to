using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The lava fields' and the void abyss's monsters (2026-11-25, Docs/BiomeRework.md B4b), in the creature style of the
    // others and at their size class (MonsterSizes: XS 8~10 pixels, S 12~14, M 14~18, L 24~32, XL 40~56 with the
    // outline), all laid out from parts on a canvas like the other biomes' big ones (CreatureArt.Biomes.cs): the
    // salamander, the obsidian beetle and the forge slime of the fields; the abyss stalker (its own body at last, no
    // longer the sand scorpion's), the void eye, the star slime, the shadow stalker and the void shard and mote of the
    // abyss; the magma hurler and the void splitter (L); the ember smith and the gate warden (XL). Standing dormant
    // (World.IsDormant) the smith is cold iron with its forge out and the warden grey stone with its gate dark.
    static partial class CreatureArt
    {
        // ---- The lava fields ----

        // A salamander (M): a long low lizard of glowing orange, yellow-hot spots down its back, a dark crest, four
        // splayed legs that step between the frames and a tail curled up to a flickering flame. About 16 by 10.
        static string[] Salamander(int frame)
        {
            var g = Canvas(16, 11);
            // Legs: splayed, the far ones dark, stepping.
            foreach (var (x, far) in new[] { (4, true), (10, true), (3, false), (9, false) })
            {
                int lean = (x < 7) == (frame == 0) ? -1 : 1;
                Stroke(g, x, 7, x + lean, 9, far ? 'd' : 'K');
                Set(g, 10, x + lean + (far ? 0 : 1), far ? 'd' : 'K');
            }
            // The body: a long low oval, the head forward on the right.
            for (int x = 2; x <= 12; x++)
                for (int y = 4; y <= 7; y++) Set(g, y, x, y == 4 ? 'L' : y == 7 ? 'm' : 'M');
            Oval(g, 13, 5, 2, 2, 'M');
            Set(g, 3, 13, 'L');
            Set(g, 3, 12, 'L');
            Set(g, 5, 14, 'e');
            Set(g, 6, 15, 'm');
            Set(g, 7, 14, 'd');
            // The crest, dark, and the hot spots.
            for (int x = 3; x <= 11; x += 2) Set(g, 3, x, 'K');
            foreach (int x in new[] { 4, 7, 10 }) Set(g, 5, x, 'y');
            Set(g, 6, 6, 'y');
            // The tail: back and up, curling, a flame at its tip.
            Stroke(g, 2, 6, 0, 4, 'm');
            Stroke(g, 0, 4, 1, 1, 'M');
            Set(g, 0, 1 + frame, 'f');
            Set(g, 0, 2 - frame, 'F');
            Set(g, 1, 2, 'f');
            return Rows(g);
        }

        static readonly string[][] SalamanderFrames = { Salamander(0), Salamander(1) };

        // An obsidian beetle (M, the beetle of the fields): a domed shell of black volcanic glass in plates, its seams
        // glowing like cooling lava, a glint of glass, a dark horned head with mandibles in front, three jointed legs a
        // side that step between the frames. About 16 by 12.
        static string[] ObsidianBeetle(int frame)
        {
            var g = Canvas(16, 12);
            int[] feet = { 3, 7, 11 };
            for (int i = 0; i < feet.Length; i++)
            {
                int lean = (i + frame) % 2 == 0 ? -1 : 1;
                Stroke(g, feet[i] + 1, 8, feet[i] + lean, 11, 'K');
            }
            Dome(g, 1, 1, 12, 8);
            // The seams: down the middle and across, glowing.
            Stroke(g, 7, 2, 7, 8, 'o');
            Stroke(g, 2, 5, 12, 5, 'o');
            Set(g, 2, 4, 'w');
            Set(g, 3, 3, 'w');
            // The head: dark, low, a horn and two mandibles.
            Oval(g, 13, 6, 2, 2, 'K');
            Set(g, 5, 13, 'e');
            Stroke(g, 14, 4, 15, 2, 'h');
            Set(g, 7, 15, 'h');
            Set(g, 8, 15 - frame, 'h');
            return Rows(g);
        }

        static readonly string[][] ObsidianBeetleFrames = { ObsidianBeetle(0), ObsidianBeetle(1) };

        // A forge slime (M, the fields' elite): a magma slime grown big under a helm of black slag iron, its two horns
        // swept up and back and a band of rivets across its brow, the lava's cracks glowing through its crust and
        // dripping from its foot. The cracks shift between the frames. About 16 by 15.
        static string[] ForgeSlime(int frame)
        {
            var g = Canvas(16, 15);
            // The body: a slumped dome.
            Dome(g, 1, 5, 14, 10);
            // The helm: iron over its top, rivets, a horn either side.
            for (int x = 3; x <= 12; x++)
            {
                Set(g, 5, x, 'k');
                Set(g, 6, x, x % 3 == 0 ? 'w' : 'K');
            }
            for (int x = 4; x <= 11; x++) Set(g, 4, x, 'k');
            foreach (int side in new[] { -1, 1 })
            {
                int bx = side < 0 ? 3 : 12;
                Stroke(g, bx, 5, bx + 2 * side, 2, 'K');
                Stroke(g, bx + 2 * side, 2, bx + 3 * side, 0, 'k');
            }
            // The eyes under the helm's brow, angry slits.
            foreach (int x in new[] { 5, 6, 9, 10 }) Set(g, 8, x, 'e');
            // The cracks of lava.
            var cracks = frame == 0 ? new[] { (3, 10), (4, 11), (8, 12), (11, 10), (12, 11), (7, 9) } : new[] { (3, 11), (5, 12), (8, 11), (11, 11), (12, 12), (9, 9) };
            foreach (var (x, y) in cracks) Set(g, y, x, 'y');
            // A drip at the foot.
            Set(g, 14, 6 + frame * 3, 'y');
            return Rows(g);
        }

        static readonly string[][] ForgeSlimeFrames = { ForgeSlime(0), ForgeSlime(1) };

        // A magma hurler (L): a crouched hulk of black basalt, glowing seams in its hide, a small ember-eyed head sunk in
        // its shoulders, one knuckle on the ground and the other arm raised high with a glob of lava in its fist (lifted
        // higher in the second frame). About 30 by 26.
        static string[] MagmaHurler(int frame)
        {
            var g = Canvas(31, 28);
            int lift = frame;
            // The legs: short, thick.
            foreach (int x0 in new[] { 7, 16 })
                for (int y = 22; y <= 27; y++)
                    for (int x = x0; x <= x0 + 5; x++) Set(g, y, x, y == 27 ? 'd' : x == x0 ? 'L' : x == x0 + 5 ? 'm' : 'M');
            // The body: a great hunched dome.
            Dome(g, 3, 9, 24, 15);
            // Seams of lava through the hide.
            Stroke(g, 8, 13, 11, 18, 'o');
            Stroke(g, 11, 18, 10, 22, 'o');
            Stroke(g, 19, 12, 22, 16, 'o');
            Stroke(g, 16, 18, 19, 21, 'o');
            // The head, low between the shoulders, ember eyes.
            Dome(g, 12, 6, 8, 6);
            foreach (int x in new[] { 14, 17 }) Set(g, 8, x, 'e');
            Stroke(g, 14, 10, 17, 10, 'd');
            // The near arm: down to a knuckle on the ground, in front.
            for (int y = 14; y <= 25; y++)
                for (int x = 22; x <= 26; x++) Set(g, y, x, x == 22 ? 'M' : x == 26 ? 'm' : 'M');
            Oval(g, 25, 25, 3, 2, 'm');
            Set(g, 27, 23, 'd');
            Set(g, 27, 26, 'd');
            // The far arm: raised over its head, the fist round a glob of lava.
            Stroke(g, 4, 13, 2, 6 - lift, 'L');
            for (int k = 5; k <= 7; k++) Stroke(g, k, 13, k - 2, 6 - lift, 'M');
            Stroke(g, 8, 13, 6, 6 - lift, 'm');
            // A band of glowing seam round the forearm.
            Stroke(g, 2, 9 - lift, 6, 9 - lift, 'o');
            Oval(g, 4, 4 - lift, 3, 2, 'M');
            Oval(g, 4, 1 - lift, 2, 2, 'g');
            Set(g, 0, 4, 'G');
            Set(g, 1 - lift, 3, 'G');
            Set(g, 0, 5, 'f');
            return Rows(g);
        }

        // The ember smith (XL, the forge's master): a smith of black iron and basalt, a horned helm with a glowing slit, a
        // leather apron, a chest that is a forge's mouth glowing gold, banded arms, and a great hammer held up in its
        // right hand (raised higher in the second frame, the forge's glow pulsing). About 48 by 52.
        static string[] ForgeKeeper(int frame)
        {
            var g = Canvas(50, 54);
            int lift = frame * 2;
            // Legs: two pillars of iron, boots.
            foreach (int x0 in new[] { 15, 27 })
                for (int y = 42; y <= 53; y++)
                    for (int x = x0; x <= x0 + 7; x++) Set(g, y, x, y >= 51 ? 'K' : x == x0 ? 'L' : x == x0 + 7 ? 'm' : 'M');
            // The body: broad, from shoulders to hips.
            Dome(g, 9, 15, 32, 30);
            // The apron: leather, down the front from the chest.
            for (int y = 32; y <= 47; y++)
                for (int x = 17; x <= 32; x++) Set(g, y, x, x == 17 || x == 32 ? 'a' : y == 47 ? 'a' : 'A');
            // The forge in its chest: an iron frame, a glowing mouth, bars.
            for (int y = 22; y <= 30; y++)
                for (int x = 19; x <= 30; x++) Set(g, y, x, y == 22 || y == 30 || x == 19 || x == 30 ? 'K' : frame == 0 ? (y < 26 ? 'y' : 'o') : (y < 25 ? 'Y' : 'y'));
            foreach (int x in new[] { 22, 25, 28 }) Stroke(g, x, 23, x, 29, 'K');
            // Bands of iron round the shoulders, rivets.
            for (int x = 11; x <= 38; x++) Set(g, 19, x, x % 4 == 0 ? 'w' : 'k');
            // The helm: a block with two horns, the eye slit glowing.
            Dome(g, 19, 5, 12, 12, 'k', 'K', 'K', 'd');
            for (int x = 21; x <= 28; x++) Set(g, 11, x, frame == 0 ? 'y' : 'Y');
            foreach (int side in new[] { -1, 1 })
            {
                int bx = side < 0 ? 20 : 29;
                Stroke(g, bx, 8, bx + 4 * side, 4, 'k');
                Stroke(g, bx + 4 * side, 4, bx + 5 * side, 0, 'h');
            }
            // The left arm (its far side): hanging, banded, a fist.
            for (int y = 18; y <= 37; y++)
                for (int x = 3; x <= 9; x++) Set(g, y, x, x == 3 ? 'L' : x == 9 ? 'm' : y % 6 == 0 ? 'k' : 'M');
            Oval(g, 6, 39, 4, 3, 'M');
            // The right arm: up and out to the hammer.
            for (int y = 18 - lift; y <= 30; y++)
                for (int x = 40; x <= 46; x++) Set(g, y, x, x == 46 ? 'm' : y % 6 == 0 ? 'k' : 'M');
            // The hammer: a long haft up over its shoulder to a great iron head.
            Stroke(g, 43, 18 - lift, 43, 4 - lift, 'h');
            Stroke(g, 44, 18 - lift, 44, 4 - lift, 'H');
            for (int y = -1 - lift; y <= 5 - lift; y++)
                for (int x = 36; x <= 49; x++) Set(g, y, x, y == -1 - lift ? 'k' : x == 36 || x == 49 ? 'K' : y == 5 - lift ? 'd' : 'K');
            for (int x = 38; x <= 47; x += 3) Set(g, 1 - lift, x, 'w');
            return Rows(g);
        }

        // ---- The void abyss ----

        // An abyss stalker (M): a lean beast of the void on long legs, low at the shoulder like a hunting cat, its back a
        // mane of smoke that streams behind it, a cold glowing eye in a narrow head, and one long scything claw forward.
        // The legs stride and the smoke streams between the frames. About 16 by 12.
        static string[] Stalker(int frame)
        {
            var g = Canvas(16, 12);
            // Legs: long and thin, striding.
            foreach (var (x, far) in new[] { (4, true), (10, true), (3, false), (9, false) })
            {
                int lean = (x < 7) == (frame == 0) ? -1 : 1;
                Stroke(g, x, 7, x + lean, 11, far ? 'd' : 'K');
            }
            // The body: low, narrow at the waist, the shoulders high.
            for (int x = 2; x <= 11; x++)
                for (int y = 4; y <= 7; y++)
                    if (!(y == 4 && x < 5)) Set(g, y, x, y == 4 ? 'L' : y == 7 ? 'm' : 'M');
            // The mane of smoke over its back, streaming back.
            for (int x = 1; x <= 10; x++)
            {
                int top = 3 - (x % 3 == frame % 3 ? 1 : 0);
                Set(g, top, x, 'h');
                if (x < 8) Set(g, top + 1, x - 1, 'h');
            }
            Set(g, 2, 0, frame == 0 ? 'h' : '.');
            // The head: narrow, forward, the cold eye.
            Oval(g, 13, 4, 2, 1, 'M');
            Set(g, 3, 13, 'L');
            Set(g, 4, 14, 'r');
            Set(g, 5, 15, 'm');
            // The scything claw forward from the near foreleg.
            Stroke(g, 11, 7, 14, 8, 'K');
            Stroke(g, 14, 8, 15, 10, 'r');
            return Rows(g);
        }

        static readonly string[][] StalkerFrames = { Stalker(0), Stalker(1) };

        // A shadow stalker (M, the abyss's elite): the stalker grown taller, a crown of black spikes rising from its
        // mane, two pale horns swept back, both eyes burning, a claw on each foreleg; its spikes shift between the
        // frames. About 16 by 15.
        static string[] ShadowStalker(int frame)
        {
            var g = Canvas(16, 15);
            foreach (var (x, far) in new[] { (4, true), (10, true), (3, false), (9, false) })
            {
                int lean = (x < 7) == (frame == 0) ? -1 : 1;
                Stroke(g, x, 9, x + lean, 14, far ? 'd' : 'K');
            }
            for (int x = 2; x <= 11; x++)
                for (int y = 6; y <= 9; y++) Set(g, y, x, y == 6 ? 'L' : y == 9 ? 'm' : 'M');
            // The crown of spikes over the mane.
            for (int x = 2; x <= 10; x += 2)
            {
                int h = 3 + ((x / 2 + frame) % 2) * 2;
                Stroke(g, x, 5, x + 1, 5 - h, 'K');
                Set(g, 5 - h, x + 1, 'h');
            }
            // The head, the horns swept back, the eyes.
            Oval(g, 13, 6, 2, 2, 'M');
            Stroke(g, 12, 4, 10, 1, 'h');
            Stroke(g, 13, 4, 12, 0, 'H');
            Set(g, 6, 13, 'r');
            Set(g, 6, 15, 'r');
            Set(g, 7, 15, 'm');
            // A claw on each foreleg.
            Stroke(g, 11, 9, 14, 10, 'K');
            Stroke(g, 14, 10, 15, 12, 'r');
            Stroke(g, 9, 10, 12, 12, 'K');
            Set(g, 13, 12, 'r');
            return Rows(g);
        }

        static readonly string[][] ShadowStalkerFrames = { ShadowStalker(0), ShadowStalker(1) };

        // A void eye (M): one great eye floating in a ball of violet flesh, a pale iris round a black pupil (it looks
        // where it aims), lids above and below, three tendrils trailing under it that sway between the frames. About 15
        // by 16.
        static string[] VoidEye(int frame)
        {
            var g = Canvas(16, 17);
            Oval(g, 7, 6, 6, 6, 'M');
            for (int x = 2; x <= 12; x++)
            {
                Set(g, 1, x, x < 9 ? 'L' : 'M');
                Set(g, 11, x, 'm');
            }
            Set(g, 2, 2, 'L');
            Set(g, 3, 1, 'L');
            // The eye: white, the iris, the pupil (its 'e', hidden from behind with the rest of the eye).
            Oval(g, 8, 6, 4, 3, 'j');
            Oval(g, 8, 6, 2, 2, 'i');
            Set(g, 6, 8, 'e');
            Set(g, 5, 8, 'e');
            Set(g, 5, 9, 'w');
            // Lids.
            for (int x = 4; x <= 12; x++) Set(g, 2, x, 'L');
            for (int x = 5; x <= 11; x++) Set(g, 10, x, 'm');
            // Tendrils.
            foreach (int x in new[] { 4, 7, 10 })
            {
                int sway = (x + frame) % 2 == 0 ? -1 : 1;
                Stroke(g, x, 12, x + sway, 14, 'm');
                Set(g, 15, x + sway, 'd');
                Set(g, 16, x, x == 7 ? 'd' : '.');
            }
            return Rows(g);
        }

        static readonly string[][] VoidEyeFrames = { VoidEye(0), VoidEye(1) };

        // A star slime (M, the abyss's slime): a slime of the night sky, deep indigo with pale stars in it that twinkle in
        // turn between the frames, a violet sheen on top. About 14 by 11.
        static string[] StarSlime(int frame)
        {
            var g = Canvas(14, 11);
            Dome(g, 0, 1, 14, 10);
            foreach (int x in new[] { 4, 9 })
            {
                Set(g, 5, x, 'e');
                Set(g, 6, x, 'e');
            }
            var stars = frame == 0 ? new[] { (2, 4), (6, 3), (11, 6), (7, 8), (3, 8) } : new[] { (3, 3), (7, 4), (10, 4), (5, 8), (11, 8) };
            for (int i = 0; i < stars.Length; i++) Set(g, stars[i].Item2, stars[i].Item1, i % 2 == 0 ? 's' : 'S');
            Set(g, 2, 4, 'w');
            Set(g, 2, 5, 'w');
            return Rows(g);
        }

        static readonly string[][] StarSlimeFrames = { StarSlime(0), StarSlime(1) };

        // A void shard (M, a half of a splitter): a jagged lump of violet void with a crack of light down it, an eye in
        // each side, stubby feet. About 14 by 12.
        static string[] VoidShard(int frame)
        {
            var g = Canvas(14, 12);
            Dome(g, 1, 1, 12, 10);
            // Jags at its top.
            foreach (int x in new[] { 3, 7, 10 }) Set(g, 0, x, 'L');
            Stroke(g, 7, 2, 6, 9, 's');
            Set(g, 5, 4, 'e');
            Set(g, 5, 9, 'e');
            foreach (int x in new[] { 3, 9 }) Set(g, 11, x + frame, 'd');
            return Rows(g);
        }

        static readonly string[][] VoidShardFrames = { VoidShard(0), VoidShard(1) };

        // A void mote (XS): a little blob of violet light with one eye and a spark over it. About 8 by 7.
        static string[] VoidMote(int frame)
        {
            var g = Canvas(8, 7);
            Dome(g, 0, 1, 8, 6);
            Set(g, 3, 4, 'e');
            Set(g, 0, 2 + frame * 3, 's');
            return Rows(g);
        }

        static readonly string[][] VoidMoteFrames = { VoidMote(0), VoidMote(1) };

        // A void splitter (L): a great bulb of the void split down its middle by a seam of light (where it comes apart
        // when it falls), stars in its dark, an eye on either half, a fringe of short tentacle feet that creep between the
        // frames. About 28 by 22.
        static string[] VoidSplitter(int frame)
        {
            var g = Canvas(29, 23);
            Dome(g, 1, 1, 26, 19);
            // The seam down the middle, glowing.
            for (int y = 1; y <= 19; y++) Set(g, y, 13 + ((y / 3) % 2), y % 2 == 0 ? 's' : 'S');
            // Stars in its dark.
            foreach (var (x, y) in new[] { (5, 6), (9, 12), (19, 8), (22, 14), (16, 16), (6, 15) }) Set(g, y, x, (x + y + frame) % 2 == 0 ? 's' : 'S');
            // An eye on either half.
            foreach (int x in new[] { 8, 9, 18, 19 }) Set(g, 9, x, 'e');
            foreach (int x in new[] { 8, 19 }) Set(g, 8, x, 'e');
            // Tentacle feet.
            for (int x = 3; x <= 24; x += 3)
            {
                int sway = (x / 3 + frame) % 2 == 0 ? -1 : 1;
                Stroke(g, x, 19, x + sway, 22, 'd');
            }
            return Rows(g);
        }

        // The gate warden (XL, the broken gate's master): a giant of dark stones with the broken arch of a gate risen
        // over its head (its crown), runes on the arch, a portal of violet void swirling in its chest, heavy arms ending
        // in chained manacles, and fragments of the gate floating round it that drift between the frames. About 50 by 54.
        static string[] GateWarden(int frame)
        {
            var g = Canvas(52, 56);
            // The base: a skirt of stones, like a robe, wide at the foot.
            for (int y = 38; y <= 55; y++)
            {
                int half = 10 + (y - 38) / 2;
                for (int x = 26 - half; x <= 25 + half; x++) Set(g, y, x, y == 55 ? 'd' : x <= 27 - half ? 'L' : x >= 24 + half ? 'm' : (x + y) % 7 == 0 ? 'd' : 'M');
            }
            // The body: shoulders and chest of stone blocks.
            Dome(g, 11, 17, 30, 24);
            for (int y = 22; y <= 38; y += 5) Stroke(g, 13, y, 38, y, 'd');
            // The portal in its chest: a ring of runes round a swirl.
            Oval(g, 26, 29, 7, 7, 'k');
            Oval(g, 26, 29, 6, 6, 'v');
            Oval(g, 26, 29, 4, 4, 'V');
            Oval(g, 26, 29, 2, 2, 'p');
            for (int i = 0; i < 6; i++)
            {
                double a = (i * 60 + frame * 30) * Math.PI / 180;
                Set(g, 29 + (int)Math.Round(Math.Sin(a) * 5), 26 + (int)Math.Round(Math.Cos(a) * 5), 'p');
            }
            Set(g, 29, 26, 'w');
            // The head: small, hooded in stone, two eyes of void.
            Dome(g, 21, 10, 10, 9);
            foreach (int x in new[] { 23, 24, 27, 28 }) Set(g, 15, x, 'p');
            // The broken arch over its head: two uprights and the half of a lintel, runes.
            for (int y = 1; y <= 14; y++)
            {
                Set(g, y, 15, 'L');
                Set(g, y, 16, 'M');
                if (y >= 5) Set(g, y, 36, 'M');
                if (y >= 5) Set(g, y, 37, 'm');
            }
            for (int x = 15; x <= 31; x++)
            {
                Set(g, 0, x, 'L');
                Set(g, 1, x, x % 4 == 0 ? 'p' : 'M');
                Set(g, 2, x, 'm');
            }
            Stroke(g, 32, 2, 34, 4, 'm');
            Set(g, 4, 36, 'L');
            // The arms: heavy, hanging, manacles and a hanging chain.
            foreach (int x0 in new[] { 2, 42 })
            {
                for (int y = 20; y <= 38; y++)
                    for (int x = x0; x <= x0 + 7; x++) Set(g, y, x, x == x0 ? 'L' : x == x0 + 7 ? 'm' : 'M');
                for (int x = x0 - 1; x <= x0 + 8; x++)
                {
                    Set(g, 33, x, 'k');
                    Set(g, 34, x, 'K');
                }
                for (int y = 39; y <= 45 + frame; y++) Set(g, y, x0 + 3 + (y % 2), y % 2 == 0 ? 'k' : 'K');
            }
            // Fragments of the gate floating round it.
            foreach (var (x, y) in new[] { (5, 9), (45, 12), (8, 47), (46, 47) })
            {
                int dy = frame == 0 ? 0 : (x < 26 ? -1 : 1);
                for (int k = 0; k < 3; k++)
                {
                    Set(g, y + dy, x + k, 'L');
                    Set(g, y + dy + 1, x + k, k == 1 ? 'p' : 'm');
                }
            }
            return Rows(g);
        }

        // The colours of the two biomes' kinds besides their ramps.
        static readonly Dictionary<string, Dictionary<char, Rgba>> LavaVoidExtras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            // Hot spots, the flame at its tail, its dark crest and claws.
            ["salamander"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#FFE070"),
                ['f'] = Rgba.Hex("#FF8A30"),
                ['F'] = Rgba.Hex("#FFF0A0"),
                ['K'] = Rgba.Hex("#5A1E10"),
            },
            // Glowing seams, glass glints, a dark head, pale horn and mandibles.
            ["obsidian_beetle"] = new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#FF7A30"),
                ['K'] = Rgba.Hex("#1A1420"),
                ['h'] = Rgba.Hex("#C8B8D8"),
            },
            // The helm's slag iron and its rivets, the lava's cracks.
            ["forge_slime"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#2A2224"),
                ['k'] = Rgba.Hex("#5A4E50"),
                ['y'] = Rgba.Hex("#FFD45A"),
            },
            // Seams of lava, the glob of lava in its fist and its bright core, a lick of flame.
            ["magma_hurler"] = new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#FF6A20"),
                ['g'] = Rgba.Hex("#FF8A2A"),
                ['G'] = Rgba.Hex("#FFE07A"),
                ['f'] = Rgba.Hex("#FFC040"),
            },
            // The smith: black iron, banded iron's light, the forge's gold and orange, its leather apron, the haft.
            ["forge_keeper"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#24201E"),
                ['k'] = Rgba.Hex("#5C5450"),
                ['y'] = Rgba.Hex("#FFC040"),
                ['Y'] = Rgba.Hex("#FFF0A0"),
                ['o'] = Rgba.Hex("#FF6A20"),
                ['A'] = Rgba.Hex("#6A4028"),
                ['a'] = Rgba.Hex("#4A2A18"),
                ['h'] = Rgba.Hex("#8A6A4A"),
                ['H'] = Rgba.Hex("#5A4030"),
            },
            // The shadow stalker: night-black spikes, pale horns, burning eyes and claws.
            ["shadow_stalker"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#120A22"),
                ['h'] = Rgba.Hex("#E0D0FF"),
                ['H'] = Rgba.Hex("#A890E0"),
                ['r'] = Rgba.Hex("#FF7AF0"),
            },
            // The void eye's white and its iris.
            ["void_eye"] = new Dictionary<char, Rgba>
            {
                ['j'] = Rgba.Hex("#EDE4FF"),
                ['i'] = Rgba.Hex("#C060FF"),
                ['e'] = Rgba.Hex("#0A0614"),
            },
            // Stars.
            ["star_slime"] = new Dictionary<char, Rgba>
            {
                ['s'] = Rgba.Hex("#F4ECFF"),
                ['S'] = Rgba.Hex("#A890FF"),
            },
            ["void_shard"] = new Dictionary<char, Rgba>
            {
                ['s'] = Rgba.Hex("#E6D6FF"),
            },
            ["void_mote"] = new Dictionary<char, Rgba>
            {
                ['s'] = Rgba.Hex("#F4ECFF"),
            },
            ["void_splitter"] = new Dictionary<char, Rgba>
            {
                ['s'] = Rgba.Hex("#F4ECFF"),
                ['S'] = Rgba.Hex("#B48CFF"),
            },
            // The warden: the portal's violets and its rune light, iron manacles.
            ["gate_warden"] = new Dictionary<char, Rgba>
            {
                ['v'] = Rgba.Hex("#1A0E30"),
                ['V'] = Rgba.Hex("#5A2AA0"),
                ['p'] = Rgba.Hex("#C080FF"),
                ['k'] = Rgba.Hex("#5A5468"),
                ['K'] = Rgba.Hex("#2A2634"),
            },
        };

        // The small kinds of the two biomes (and the abyss stalker's own body), or null.
        static string[][] LavaVoidFrames(string id) => id switch
        {
            "salamander" => SalamanderFrames,
            "obsidian_beetle" => ObsidianBeetleFrames,
            "forge_slime" => ForgeSlimeFrames,
            "abyss_stalker" => StalkerFrames,
            "shadow_stalker" => ShadowStalkerFrames,
            "void_eye" => VoidEyeFrames,
            "star_slime" => StarSlimeFrames,
            "void_shard" => VoidShardFrames,
            "void_mote" => VoidMoteFrames,
            _ => null,
        };

        // The big kinds of the two biomes, or null.
        static string[][] LavaVoidBigFrames(string id) => id switch
        {
            "magma_hurler" => Sides(MagmaHurler(0), MagmaHurler(1)),
            "void_splitter" => Sides(VoidSplitter(0), VoidSplitter(1)),
            "forge_keeper" => Sides(ForgeKeeper(0), ForgeKeeper(1)),
            "gate_warden" => Sides(GateWarden(0), GateWarden(1)),
            _ => null,
        };

        // Seen from behind, the void eye shows no eye at all (its white and iris are its own letters).
        static void LavaVoidView(string id, MonsterView view, Dictionary<char, Rgba> colors)
        {
            if (view != MonsterView.Back || id != "void_eye") return;
            colors['j'] = colors['M'];
            colors['i'] = colors['M'];
        }

        // What a pose changes: winding up, the salamander's flame and the forge slime's cracks flare white-hot, the
        // hurler's glob burns white, the void eye's iris blazes (its gaze is coming) and the warden's portal brightens.
        static void LavaVoidPose(string id, MonsterPose pose, int frame, Dictionary<char, Rgba> colors)
        {
            if (pose != MonsterPose.Windup) return;
            bool flare = frame % 2 == 0;
            switch (id)
            {
                case "salamander":
                    colors['f'] = Rgba.Hex(flare ? "#FFFFFF" : "#FFE070");
                    colors['y'] = Rgba.Hex("#FFF4C0");
                    break;
                case "forge_slime":
                    colors['y'] = Rgba.Hex(flare ? "#FFFFF0" : "#FFE070");
                    break;
                case "magma_hurler":
                    colors['g'] = Rgba.Hex(flare ? "#FFF0B0" : "#FFC040");
                    colors['G'] = Rgba.Hex("#FFFFFF");
                    break;
                case "forge_keeper":
                    colors['y'] = Rgba.Hex(flare ? "#FFFFF0" : "#FFE07A");
                    colors['o'] = Rgba.Hex("#FFC040");
                    break;
                case "void_eye":
                    colors['i'] = Rgba.Hex(flare ? "#FFE0FF" : "#FF60F0");
                    break;
                case "gate_warden":
                    colors['p'] = Rgba.Hex(flare ? "#FFFFFF" : "#E0B0FF");
                    colors['V'] = Rgba.Hex("#9A5AF0");
                    break;
            }
        }

        // ---- Dormant ----

        static bool LavaVoidSleeps(string id) => id == "forge_keeper" || id == "gate_warden";

        static readonly Rgba ColdIron = Rgba.Hex("#6A6466"), GreyStone = Rgba.Hex("#7A7486");

        // A dormant kind of these biomes, or null for another: the smith cold iron, its forge and slit dark; the warden
        // grey stone, its portal shut.
        static PixelImage LavaVoidDormant(string id, PixelImage image)
        {
            if (!LavaVoidSleeps(id)) return null;
            var tone = id == "forge_keeper" ? ColdIron : GreyStone;
            var result = image.Copy();
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    var c = result[x, y];
                    if (c.A == 0) continue;
                    float light = (c.R * 0.3f + c.G * 0.55f + c.B * 0.15f) / 255f;
                    // What glowed (bright and warm, or bright and violet) goes out darkest.
                    bool glowed = c.R > 200 && c.G > 120 || c.B > 200 && c.R > 150 && c.G < 160;
                    var shown = glowed ? tone.Scale(0.45f) : tone.Scale(0.5f + 0.7f * light);
                    result[x, y] = new Rgba(shown.R, shown.G, shown.B, c.A);
                }
            }
            return result;
        }
    }
}
