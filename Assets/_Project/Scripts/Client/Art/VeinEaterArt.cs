using System.Collections.Generic;

namespace Game.Client
{
    // What the vein eater is doing, which picks its frame (WorldView.VeinEater.cs). Most of its attacks are announced
    // by nothing but these (Docs/Bosses.md 광맥 포식자 그룸), so each wind-up has a shape of its own: rearing up with the
    // drills high (the claw), crouching for a rock and lifting it overhead (the throw), head down pawing the ground
    // (the rush and the frenzy), sinking into the ground (the burrow), fists up (the cave-in), drills driven into the
    // floor (the spikes), curling into a ball (the spin), a deep crouch and a jump (the quake), head thrown back (the
    // call and the roar).
    enum VeinPose
    {
        Idle,
        Rear,
        Swipe,
        Lift,
        Throw,
        Crouch,
        Rush,
        Sink,
        Rise,
        Slam,
        Pound,
        Plunge,
        Curl,
        Leap,
        Roar,
    }

    // The vein eater of the mining village (2026-11-12): a hulking armoured mole with drill claws and orange ore
    // growing from its back. Its five views come from one Retro Diffusion Pro picture turned eight ways
    // (Docs/ArtSources.md, $0.28 in all); every pose is made from those in code by leaning, squashing, lifting and
    // sinking them, and by drawing a rock, dust or the ore's glow over them. The picture is 40 by 39 pixels on a
    // canvas with room above it for a leap.
    static class VeinEaterArt
    {
        public const int Width = 60;
        public const int Height = 76;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 39;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(255, 255, 255),
            ['b'] = new Rgba(213, 213, 207),
            ['c'] = new Rgba(203, 202, 196),
            ['d'] = new Rgba(238, 157, 69),
            ['e'] = new Rgba(238, 135, 71),
            ['f'] = new Rgba(159, 157, 157),
            ['g'] = new Rgba(131, 138, 139),
            ['h'] = new Rgba(147, 126, 111),
            ['i'] = new Rgba(207, 100, 41),
            ['j'] = new Rgba(135, 111, 98),
            ['k'] = new Rgba(100, 97, 99),
            ['l'] = new Rgba(113, 91, 84),
            ['m'] = new Rgba(121, 80, 58),
            ['n'] = new Rgba(100, 84, 76),
            ['p'] = new Rgba(88, 80, 94),
            ['q'] = new Rgba(97, 72, 65),
            ['r'] = new Rgba(140, 54, 31),
            ['s'] = new Rgba(81, 68, 70),
            ['t'] = new Rgba(95, 62, 50),
            ['u'] = new Rgba(86, 57, 49),
            ['v'] = new Rgba(67, 57, 57),
            ['w'] = new Rgba(81, 51, 41),
            ['x'] = new Rgba(68, 48, 42),
            ['y'] = new Rgba(58, 50, 56),
            ['z'] = new Rgba(49, 42, 49),
            ['A'] = new Rgba(82, 25, 17),
            ['B'] = new Rgba(56, 33, 33),
            ['C'] = new Rgba(45, 27, 28),
            ['D'] = new Rgba(51, 18, 10),
            ['E'] = new Rgba(37, 19, 24),
            ['F'] = new Rgba(25, 1, 0),
            ['G'] = new Rgba(4, 1, 0),
            ['o'] = new Rgba(0, 0, 0),
        };

        static readonly string[] FrontRows =
        {
            "........................................",
            "........DDD....DtD......................",
            "........dddD...rdr......................",
            ".......DddedD.Drdd......................",
            ".......DddediDCreFGGGooGoG..............",
            ".......BiedddrFFFjjjGnlhxjGG............",
            "........DeiidFlhhhqGnjhhhthnG...........",
            ".........reiFljhhnGlqwwhhhwhlG..........",
            "......rrDrFGnnqljGslljlwhhthlno.........",
            "......ideGGjGGquuGzllqjljjjwjno.........",
            "......riFsjjhhGuuGBCCjjjjjjxhnG.........",
            ".......GqjtthhjoxGvCCEFjlllwtvG.........",
            "......EGnjthhllsoCGllEAAlqEExxG.........",
            ".....EokllllllssoCGsjhDeFGqhjjG.........",
            "......oGGGjlnsssoCEvvxnsnnjhhiiF........",
            ".....oGxvvGnsssoBBEGvvGGBsqqueeG........",
            "....omGCqwxGssGGBBBEooyzzFsssFhG........",
            "...BGmwmDBBxGoCBBttBEEooovGxxzGo........",
            "...GummmmBGBBCFBtmmmwBECBGkGGGxBo.......",
            "...GtmmmmmmGCGCwtmmmmtCBBxxGGwwwoo......",
            "...otBBBBBquoCCCutmmmmqCqmtGBwEEpG......",
            "..GGEkfacgEuoCCuCBtttBBCBtxGBEkkvGG.....",
            "..GsgcyvyckEoCwtuEEBmmmxmFooCpkzzkG.....",
            "..GjzzzyyyzjGCwwwmmmmmmxmmBosCCsgfG.....",
            "..GGsvgffkpvGCwwmmmmmmmwmmBoEzsgpso.....",
            "...GvjcaagsvGCwwwmmmmmttmwGGysjzzsso....",
            "...ogzzvvckGxBCwwuwmmmBmwxGxGkzvvjfo....",
            "...GEssakykottBCwwwmqxxwwEuuGyyvjgCo....",
            "....osbbckowtmmuCBwxxxxxEutquGypkzjG....",
            "....GkyvssowttmqxCwxxxEEuwtqlGozzzfG....",
            ".....GkcsozqmmwwCCEEEEECBwwxlG.ovsfo....",
            ".....GkbsoBwmmwwGGoooGFCCxxBxo..GGjo....",
            "......GcGExxwwxo......GCCCCGo.....G.....",
            "......GGxExEECG.......GCxxEBxGF.........",
            ".....BzyGwtuwBo........GxBnwhfG.........",
            ".......GfFfFfo..........ooooooo.........",
            ".......GGGGGoo..........................",
            "........................................",
            "........................................",
        };

        static readonly string[] FrontSideRows =
        {
            "........................................",
            "........................................",
            "...........DDD..........................",
            "..........Drdi..........oooGGo..........",
            "...DDDD...DrddD..GGGGGoonhhhtlo.........",
            "...idddD..FredrGGjjjjovnhhwuhjjG........",
            "...DidddD.FiieGljhhhtolhhhjnqhwjG.......",
            "...DieeddDFDiGlhhhhlosvllhhhnljxjo......",
            "...DiiieddAFFnnllllnovvllllhhljlno......",
            "....DiieiddFnGGooowwCovyzzBBhjljtqG.....",
            ".....DieiiFEGnhhhjGuCoyszBBEGFlvEEo.....",
            "...DDFFiiiFGnhhhhjjoCoysljjBDeDoshjo....",
            "...dddFFiFGvlhhhttlvCCoyynjjjlsllhhjG...",
            "..DeedddFGovlhhujllsoCEoGyxuszCxsqqseF..",
            "..BiiddddoGvqnnnnlssoCEEGozzzovnFxsEvE..",
            "...DiiiiFxGooooGssssoBBBCFoooooyjGGoo...",
            "....FiiiFxoGBwuuoovoowxBCCCo...Goo......",
            ".....FiiGnoxGCCBwFoCCtmmxCCo............",
            "..DDDDFDGGotmmCEBBoCutmmmBooG...........",
            "..iddddFvoummmmmBoECwtmmmtoko...........",
            "..AiiiiFBoummmmmmwoECBttxEokgo..........",
            "...DiiiFBGutmmmBBBooCCCBtmozyo..........",
            "....DDiGtoutmmCgaagoxtmmmmGCsfG.........",
            "....riFGtowuBBgayzzGBtmmmmoykzzo........",
            "....ArrGuoCCkcfysfkoBtmmmuovCsfo........",
            "..GoGhDowCGxfvzsffbGCCummwoCylfo........",
            "..GlogoCwCokzzjgaayvCCwuwxEovsCo........",
            "..GnvonEwCoyyskcgygpoCwwwCuoGonfo.......",
            ".GxBoouwwECoosgpzgcfoCCxExuo..ofo.......",
            ".GnBooquwECBovsvqbaczCCoCwxo...ko.......",
            ".owlBxwwCCCBxosBkgyvvoEEBxo....zo.......",
            "..oxttwBCCoBwwoGsyfafoCEoo..............",
            "...oCCCEGooBwqqBoGkbcoCEBuoG............",
            "...sooGGooCBwxxBCGoncGBxfFgo............",
            ".........oCwCCGGG..oGooooooo............",
            ".........oCtwtuto.......................",
            "..........oBfGfxxnG.....................",
            "...........oooooGGG.....................",
            "........................................",
        };

        static readonly string[] SideRows =
        {
            "........................................",
            "........................................",
            "........................GGooG...........",
            "...........DD.........oGnhhhtGG.........",
            "...........DdD....oGGGslhhhwhhqG........",
            "..........DedeD.GGjjhoslnhhhwwhlG.......",
            "...DDDD...DeddDGlhhhhossjhhhjjwqlo......",
            "...DdddDD.FiidFlhhhllossnlllljjqlo......",
            "...DedddiFFiiFqhhlluwBosszzBxxhjlnG.....",
            "...DieedddFiiFljuGoooBosszzBBoGGlxG.....",
            "...DiieidddFFllwGlhhhoozslllvGDeGGjGG...",
            "....FiiiiiddFluGnhhhhjloyynjjjjlsjjjeF..",
            ".....DiiiiiFqlGsltthhujsozzxxvszzsqqqF..",
            ".....DFriiiFltoslhhttjnsooCzzzozsFEsC...",
            "....AddDAiFqluosqllllqssoCooooooGlooo...",
            "...DdedddeFnluosssooosszECo.....oo......",
            "...DieddddFlluovGoxxxovzuuo.............",
            "....FiiiiiFllwCoxwwwwBooqmo.............",
            "..DDFFFieiFllwoCCCEECooBtmo.............",
            "..DdddeDFFqllwoCBummmwoCumoG............",
            "..DieddddFsllwoCwtmmmmtowuo.............",
            "...DiiiiiFslwwoCutmmmmxBoGso............",
            "....FiiiiFllwwoCwummmwCggGff............",
            "...DFFiiFqllwwCGBwwtxBagzovzo...........",
            "...AiiFFFqlnwwCCEwwtCbkygoygo...........",
            "....DiirullwwwCCoCxCckzfffGpfo..........",
            "..oGgFFovlwwwwCCCGygkzfgavyogo..........",
            "..ojoggovlwwwwCBxokzzggapkEgoo..........",
            ".ouqsoolCwwwwEBuuxGzskfvvfgo............",
            ".GqCGooluwwwCCwtmmxGykzgcago............",
            "..GlBqGluCCCCBwtmmmBoyvkcvvo............",
            "..owlwlwwCCCGCwtmmtCEGspyfbgG...........",
            "...oxxxxCCoEoCwutqxCoEooGpffG...........",
            "....GGGGGoooCCwwBBBGCxkoooofG...........",
            "...........oCBCEEGGoGGoo...oo...........",
            "...........oCwuuwtto....................",
            "............GwCgfFfxvG..................",
            ".............oGGGGGGG...................",
            "........................................",
        };

        static readonly string[] BackSideRows =
        {
            "........................................",
            "....................oGGGGG..............",
            "..................GGqlhhllG.............",
            ".................GslhhhhhllG............",
            "............DDD..GqjhhhhhlllG...........",
            "............iddDoGGooGjlllllxG..........",
            "...........DedddDlllllonlvxBGo..........",
            "...........DeedddDhhhlnovxCErG..........",
            "......DDD..FeedddDhhhqnuoxxjBG..........",
            "......DddeDriiiddDhhlqBooovsG...........",
            "......DedddDriieddDnnwojhhoyo...........",
            "......DiedddDiiiedDnnGlhhhjG............",
            "......DiiedddDiieeqltGlhhhujo...........",
            "......DiiiedddDieDllwosllhtjqo..........",
            "......kDiiieeeddDnjnwosllljnso..........",
            ".......DiiiiieedDnhlwossnjvooo..........",
            ".......GriiiiieeDlhlwCGvvoGwBo..........",
            ".....DDdFrirdieDDljlwCooowwxo...........",
            ".....riddFDddddddDjlwCCoBBCEBGG.........",
            ".....DiieeDeddeeeDjlwCoBwFBmmoo.........",
            "......AiiiDieeeeDljnwCoBwummmmxo........",
            "......DiiiiFiieDtljnwCGCwutmmmmCo.......",
            ".......FiieiFiiDjjjuwCCGCuutmmuCG.......",
            ".....vDFDreAieDujjjtwuCGExwwutCfoo......",
            ".....DdiDdddddDljjlwuuCEGCBwwEfkso......",
            ".....rieFieeeiFljjtuwuCCEGCCCfkCfo......",
            ".....xDiiFiieDljjuwwwwCCCoykkszvfo......",
            ".......DDiDDiDllluwwuEBBBCoCzzsgCo......",
            ".......oEDoCDunlwwuuEBttwCozvykkpG......",
            ".....oooECoEuwwnwwwuCuummuoGoGyzfG......",
            ".....oloColEuwwwwwBCBwummqo...ooso......",
            ".....oqqCojxuwxEEECCBuutmqo....oo.......",
            ".....otuoolwuECEECCECuuutuo.............",
            "......GwlCqwCooooooCCwuuBo..............",
            "......oBwwBCoooG..oCwwuEEooG............",
            ".......ooooooo....oCwwCwwwjo............",
            "..................oCwwtCffoo............",
            "...................ooooooo..............",
            "........................................",
        };

        static readonly string[] BackRows =
        {
            "........................................",
            "..............GGGGGG....................",
            "............GGnhhhhno...................",
            "...........GnlhhhhhjnG..................",
            "..........GslhhhhhhlDDG.................",
            "..........GslhlooGooido.................",
            "..........GsssGljjlDdeioo...............",
            "..........GzvGnhhhDddeeDoo..............",
            ".........ooooqnlhhDddeeDloooo...........",
            "........GlhlGsnhhDddieeDDnoluo..........",
            ".......GjhhlGsnhhDddiiAddDonjjG.........",
            "......olthhqosnlnDdiDDdddDsGuujG........",
            "......oljnlsounlqeeDDddeeDBGstjG........",
            "......onllssoullqieDddeeiiBGsssoo.......",
            "......osnsssGnlllDDddeeiieDxossoG.......",
            ".....CoosssoxlljlDdeeeiiiDlxosootG......",
            ".....GqGooGoxljjjDdeeeiiiDlBoooxtG......",
            "....GxmBBBFCwljjDddeiiiiiDluCGBttBG.....",
            "...GtmmqwBoCwljjDdeeiiDDDDluCoBtttG.....",
            "...GqmmwwBoCuljjDdeiDDddADluCoButtG.....",
            "...GmmuwwBoCuljjeeiDddddADluCoBBxtG.....",
            "...GmwuwwBoCulljDeiDdeeieDluCoCBBuG.....",
            "...GxwwwwoCCulljDeDddeiiDDDDCoCBBBG.....",
            "...GEwwwBoCCwlljFDDdeiiDddDuEoCBCoo.....",
            "...GgEwBBoCwwljDddddeiiDdiDuEooCgo......",
            "...GGooGooCwwqlDedeeeiieiiDuEopoo.......",
            "...GkpGsvoCwwwljlDieiiieiFuwEoEko.......",
            "...GkEszowCCwwwFDjFiiiiADwuuEokGo.......",
            "...GfvoouuuCxwwujjlFiiFCuwuCwuoo........",
            "...Gko.omwuBEwwwwjouGDxwwwwCwuo.........",
            ".....o.oqwwBEwwwwBollooEEwCCxuo.........",
            ".......oBwwBCEBwwCollGnuEECCwwo.........",
            "........owwwCEECCCooCloxEECCwoo.........",
            "........owwwCCooGGokoloooCCwwo..........",
            ".......ouwwwCCo..ooxqBoooCCwwwoo........",
            "......GgCBwwwCo...ooBBo.oCBwwCFG........",
            ".......GGoGGGGG....ooo..oGGGGoG.........",
            "........................................",
            "........................................",
        };

        static readonly Dictionary<BossView, PixelImage> Views = new Dictionary<BossView, PixelImage>();

        static PixelImage ViewOf(BossView view)
        {
            if (Views.TryGetValue(view, out var image)) return image;
            image = PixelImage.FromRows(Palette, view switch
            {
                BossView.Front => FrontRows,
                BossView.FrontSide => FrontSideRows,
                BossView.Side => SideRows,
                BossView.BackSide => BackSideRows,
                _ => BackRows,
            });
            Views[view] = image;
            return image;
        }

        // The ore and the eyes: the colours that glow when it winds up a big blow, and in its last phase.
        static bool IsOre(Rgba c) => c.R > 135 && c.B < 110 && c.R - c.B > 100 && c.R - c.G > 40;

        static readonly Rgba Hot = new Rgba(255, 236, 150);
        static readonly Rgba Rage = new Rgba(255, 70, 40);
        static readonly Rgba Rock = new Rgba(120, 104, 92);
        static readonly Rgba RockLight = new Rgba(162, 146, 128);
        static readonly Rgba RockDark = new Rgba(74, 62, 56);
        static readonly Rgba Dirt = new Rgba(104, 76, 56);
        static readonly Rgba DirtLight = new Rgba(140, 106, 78);
        static readonly Rgba Outline = new Rgba(20, 12, 10);

        // One frame. frame runs 0..3 through a wind-up (or 0..1 breathing at rest); phase 3 burns its ore hot; rage
        // turns it red (the frenzy). Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, VeinPose pose, int frame, int phase, bool rage)
        {
            var body = ViewOf(view);
            bool side = view != BossView.Front && view != BossView.Back;
            float t = frame / 3f;
            // Lean (top moves ahead), stretch (taller and thinner), lift (up off the ground) and sink (into it).
            float lean = 0, stretch = 0, lift = 0;
            int sink = 0;
            switch (pose)
            {
                case VeinPose.Idle:
                    stretch = frame % 2 == 1 ? 0.03f : 0;
                    break;
                case VeinPose.Rear:
                    lean = -3 - 3 * t;
                    stretch = 0.06f + 0.06f * t;
                    break;
                case VeinPose.Swipe:
                    lean = 6;
                    stretch = -0.08f;
                    break;
                case VeinPose.Lift:
                    // A crouch to take the rock, then up with it.
                    stretch = frame < 2 ? -0.14f : 0.08f;
                    lean = frame < 2 ? 3 : -3;
                    break;
                case VeinPose.Throw:
                    lean = 6;
                    stretch = -0.04f;
                    break;
                case VeinPose.Crouch:
                    lean = 5;
                    stretch = -0.1f;
                    break;
                case VeinPose.Rush:
                    lean = 8;
                    stretch = -0.12f;
                    break;
                case VeinPose.Sink:
                    sink = 6 + frame * 11;
                    break;
                case VeinPose.Rise:
                    stretch = 0.14f;
                    lift = 3;
                    break;
                case VeinPose.Slam:
                    lean = -2;
                    stretch = 0.08f + 0.06f * t;
                    break;
                case VeinPose.Pound:
                    lean = 2;
                    stretch = -0.16f;
                    break;
                case VeinPose.Plunge:
                    lean = 6;
                    stretch = -0.14f - 0.04f * t;
                    break;
                case VeinPose.Curl:
                    stretch = -0.2f - 0.06f * t;
                    break;
                case VeinPose.Leap:
                    // Down into a deep crouch, then up and away out of the top of the picture.
                    stretch = frame == 0 ? -0.22f : 0.1f;
                    lift = frame == 0 ? 0 : frame * 14;
                    break;
                case VeinPose.Roar:
                    lean = -4;
                    stretch = 0.1f;
                    break;
            }
            if (!side)
            {
                // Seen from the front or back a lean shows as a dip or a rise.
                stretch -= lean * 0.015f;
                lean = 0;
            }

            int w = body.Width;
            int h = body.Height;
            int tall = System.Math.Max(4, (int)System.Math.Round(h * (1 + stretch)));
            int wide = System.Math.Max(4, (int)System.Math.Round(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2;
            int ground = Height - 1 - (int)lift;
            // Sinking: the body goes down into the ground, cut off at the ground line.
            for (int y = 0; y < tall; y++)
            {
                int row = ground - (tall - 1 - y) + sink;
                if (row < 0 || row > ground) continue;
                int sy = y * h / tall;
                float up = (tall - 1 - y) / (float)(tall - 1);
                int shift = (int)System.Math.Round(lean * up);
                for (int x = 0; x < wide; x++)
                {
                    int sx = x * w / wide;
                    var c = body[sx, sy];
                    if (c.A == 0) continue;
                    if (IsOre(c))
                    {
                        if (rage) c = c.Mix(Rage, 0.7f);
                        else if (phase >= 3) c = c.Mix(Hot, 0.55f);
                        else if (pose == VeinPose.Slam || pose == VeinPose.Leap || pose == VeinPose.Rise) c = c.Mix(Hot, 0.35f);
                    }
                    int px = left + x + shift;
                    if (image.Inside(px, row)) image[px, row] = c;
                }
            }

            int top = ground - tall + 1 + sink;
            int middle = Width / 2 + (int)System.Math.Round(lean);
            switch (pose)
            {
                case VeinPose.Lift when frame >= 2:
                    Boulder(image, middle - 2, top - 4, 7);
                    break;
                case VeinPose.Sink:
                    Mound(image, Width / 2, Height - 1, 12 + frame * 2, 3 + frame);
                    break;
                case VeinPose.Rise:
                    Mound(image, Width / 2, Height - 1, 18, 3);
                    break;
                case VeinPose.Slam when frame >= 1:
                    // The drills up over its head, glinting.
                    Glint(image, middle - 9, top + 1);
                    Glint(image, middle + 9, top + 1);
                    break;
                case VeinPose.Rear when frame >= 2:
                    Glint(image, middle + 6, top + 6);
                    break;
                case VeinPose.Pound:
                case VeinPose.Plunge:
                    Mound(image, Width / 2 + (side ? 10 : 0), Height - 1, 8, 2);
                    break;
            }
            return image;
        }

        // A rock held up for a throw.
        static void Boulder(PixelImage image, int cx, int cy, int r)
        {
            for (int y = -r - 1; y <= r + 1; y++)
            {
                for (int x = -r - 1; x <= r + 1; x++)
                {
                    float d = (x * x) / (float)(r * r) + (y * y) / (float)((r - 1) * (r - 1));
                    if (!image.Inside(cx + x, cy + y) || d > 1.35f) continue;
                    image[cx + x, cy + y] = d > 1 ? Outline : y < -r / 3 && x < r / 3 ? RockLight : y > r / 2 ? RockDark : (x * 7 + y * 3) % 5 == 0 ? RockDark : Rock;
                }
            }
        }

        // Earth heaped up where it digs.
        static void Mound(PixelImage image, int cx, int bottom, int half, int height)
        {
            for (int x = -half; x <= half; x++)
            {
                int column = (int)System.Math.Round(height * System.Math.Sqrt(System.Math.Max(0, 1 - x * x / (float)(half * half))));
                for (int y = 0; y <= column; y++)
                {
                    int px = cx + x;
                    int py = bottom - y;
                    if (!image.Inside(px, py)) continue;
                    image[px, py] = y == column ? Outline : y == column - 1 ? DirtLight : (px + py) % 4 == 0 ? RockDark : Dirt;
                }
            }
        }

        // A star of light on a drill's tip.
        static void Glint(PixelImage image, int x, int y)
        {
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (image.Inside(x + dx, y + dy)) image[x + dx, y + dy] = dx == 0 && dy == 0 ? Rgba.White : new Rgba(255, 240, 200);
            }
        }
    }
}
