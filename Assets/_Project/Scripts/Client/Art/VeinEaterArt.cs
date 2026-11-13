using System.Collections.Generic;
using System.Linq;

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
        Walk,
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
        public const int Width = 66;
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

        // ---- Parts (2026-11-12, the person: "a walk when it moves, and more frames when it attacks so its arms and legs
        // really move"). The two drill arms are found as the metal of the picture and the dark edge round it; the two
        // feet are a box each per view; everything else is the body. Each part is drawn at its own offset, and where
        // a part has moved away from inside the body the gap is filled with shadow. ----

        enum Part : byte
        {
            Body,
            ArmA,
            ArmB,
            LegA,
            LegB,
        }

        // Room round the picture for arms raised overhead or flung out.
        const int Margin = 10;
        const int Headroom = 14;

        static readonly Rgba Shadow = new Rgba(56, 33, 33);
        static readonly Rgba Limb = new Rgba(88, 56, 44);
        static readonly Dictionary<BossView, Part[,]> PartMaps = new Dictionary<BossView, Part[,]>();

        // The feet of each view in the picture (left, top, right, bottom, inclusive): the near or left one, then the
        // other. The two side views are the same picture.
        static ((int X0, int Y0, int X1, int Y1) A, (int X0, int Y0, int X1, int Y1) B) FeetOf(BossView view) => view switch
        {
            BossView.Front => ((4, 32, 15, 38), (21, 32, 31, 38)),
            BossView.Back => ((6, 32, 14, 38), (17, 33, 32, 38)),
            BossView.BackSide => ((5, 29, 13, 38), (18, 34, 27, 38)),
            _ => ((9, 34, 18, 38), (21, 31, 28, 34)),
        };

        static float Luminance(Rgba c) => 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;

        static bool IsMetal(Rgba c) => c.A > 0 && System.Math.Max(c.R, System.Math.Max(c.G, c.B)) - System.Math.Min(c.R, System.Math.Min(c.G, c.B)) < 22 && Luminance(c) > 85;

        static bool IsDark(Rgba c) => c.A > 0 && Luminance(c) < 30;

        static Part[,] PartsOf(BossView view)
        {
            if (PartMaps.TryGetValue(view, out var known)) return known;
            var image = ViewOf(view);
            int w = image.Width, h = image.Height;
            var parts = new Part[w, h];
            var (a, b) = FeetOf(view);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (image[x, y].A == 0) continue;
                    if (x >= a.X0 && x <= a.X1 && y >= a.Y0 && y <= a.Y1) parts[x, y] = Part.LegA;
                    else if (x >= b.X0 && x <= b.X1 && y >= b.Y0 && y <= b.Y1) parts[x, y] = Part.LegB;
                }
            }
            // The drills: metal, and the dark edge touching it.
            var arm = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    arm[x, y] = parts[x, y] == Part.Body && IsMetal(image[x, y]);
            var edge = new List<(int, int)>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (arm[x, y] || parts[x, y] != Part.Body || !IsDark(image[x, y])) continue;
                    bool touches = false;
                    for (int dy = -1; dy <= 1 && !touches; dy++)
                        for (int dx = -1; dx <= 1 && !touches; dx++)
                            touches = x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h && arm[x + dx, y + dy];
                    if (touches) edge.Add((x, y));
                }
            }
            foreach (var (x, y) in edge) arm[x, y] = true;
            // The two biggest pieces of it are the arms, the left one first; specks are left on the body.
            var seen = new bool[w, h];
            var pieces = new List<List<(int X, int Y)>>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!arm[x, y] || seen[x, y]) continue;
                    var piece = new List<(int X, int Y)>();
                    var stack = new Stack<(int X, int Y)>();
                    stack.Push((x, y));
                    seen[x, y] = true;
                    while (stack.Count > 0)
                    {
                        var (px, py) = stack.Pop();
                        piece.Add((px, py));
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = px + dx, ny = py + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !arm[nx, ny]) continue;
                                seen[nx, ny] = true;
                                stack.Push((nx, ny));
                            }
                        }
                    }
                    if (piece.Count >= 8) pieces.Add(piece);
                }
            }
            pieces.Sort((p, q) => q.Count.CompareTo(p.Count));
            if (pieces.Count > 2) pieces.RemoveRange(2, pieces.Count - 2);
            pieces.Sort((p, q) => p.Average(c => c.X).CompareTo(q.Average(c => c.X)));
            for (int i = 0; i < pieces.Count; i++)
                foreach (var (x, y) in pieces[i]) parts[x, y] = i == 0 ? Part.ArmA : Part.ArmB;
            PartMaps[view] = parts;
            return parts;
        }

        // How far each part is moved (pixels, right and down) for a pose's frame. Arms are moved by how high they are
        // raised, how far they reach forward and how far apart they spread; feet by how far they step and lift. From
        // the side forward is to the right; from the front or back an arm's reach shows as a little dip and its
        // spread as outward.
        struct Moves
        {
            public int ArmAX, ArmAY, ArmBX, ArmBY, LegAX, LegAY, LegBX, LegBY;
        }

        // ---- The frames (2026-11-13, the person: more frames for natural motion, each checked against the frames either
        // side). A pose is a set of numbers eased between key poses: how high each drill arm is raised and how far it
        // reaches, how far apart they spread, the feet's steps and lifts, the body's lean, stretch, lift off the ground
        // and sink into it, and the rock and the mound of earth. Every motion starts from rest and comes back to it; a
        // blow is laid out in stages (swing, strike, follow, recover), a wind-up it came out of plays backward to stand
        // up again (the charge's crouch, the spin's curl, the drills pulled out of the floor). ----

        struct VPose
        {
            public float RaiseA, ReachA, RaiseB, ReachB, Spread, StepA, LiftA, StepB, LiftB, Lean, Stretch, Lift, Sink;
            // The rock held overhead (over 0.5 shows it); the mound of earth (half width, height; ahead of it from the
            // side when Ahead).
            public float Rock, MoundHalf, MoundHeight;
            public bool Ahead;
        }

        static VPose V(float raise = 0, float reach = 0, float spread = 0, float lean = 0, float stretch = 0, float stepA = 0, float stepB = 0,
            float liftA = 0, float liftB = 0, float lift = 0, float sink = 0, float? raiseB = null, float? reachB = null, float rock = 0,
            float mound = 0, float moundHeight = 0, bool ahead = false) => new VPose
        {
            RaiseA = raise, ReachA = reach, RaiseB = raiseB ?? raise, ReachB = reachB ?? reach, Spread = spread, Lean = lean, Stretch = stretch,
            StepA = stepA, StepB = stepB, LiftA = liftA, LiftB = liftB, Lift = lift, Sink = sink, Rock = rock, MoundHalf = mound, MoundHeight = moundHeight,
            Ahead = ahead,
        };

        static VPose Lerp(VPose a, VPose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return new VPose
            {
                RaiseA = L(a.RaiseA, b.RaiseA), ReachA = L(a.ReachA, b.ReachA), RaiseB = L(a.RaiseB, b.RaiseB), ReachB = L(a.ReachB, b.ReachB),
                Spread = L(a.Spread, b.Spread), StepA = L(a.StepA, b.StepA), LiftA = L(a.LiftA, b.LiftA), StepB = L(a.StepB, b.StepB),
                LiftB = L(a.LiftB, b.LiftB), Lean = L(a.Lean, b.Lean), Stretch = L(a.Stretch, b.Stretch), Lift = L(a.Lift, b.Lift), Sink = L(a.Sink, b.Sink),
                Rock = L(a.Rock, b.Rock), MoundHalf = L(a.MoundHalf, b.MoundHalf), MoundHeight = L(a.MoundHeight, b.MoundHeight),
                Ahead = u < 0.5f ? a.Ahead : b.Ahead,
            };
        }

        static VPose Keys(float t, params (float At, VPose Pose)[] keys)
        {
            if (t <= keys[0].At) return keys[0].Pose;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].At) continue;
                float u = (t - keys[i - 1].At) / (keys[i].At - keys[i - 1].At);
                return Lerp(keys[i - 1].Pose, keys[i].Pose, u * u * (3 - 2 * u));
            }
            return keys[keys.Length - 1].Pose;
        }

        static readonly VPose Rest = V();

        // A blow's stages, each frame shown for its stage's time.
        enum Stage : byte
        {
            Swing,
            Strike,
            Follow,
            Recover,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.04f,
            Stage.Strike => 0.1f,
            Stage.Follow => 0.12f,
            _ => 0.07f,
        };

        static (VPose[] Frames, Stage[] Stages) Expand(VPose from, params (Stage Stage, int Frames, VPose To)[] stages)
        {
            var frames = new List<VPose>();
            var kinds = new List<Stage>();
            var at = from;
            foreach (var (stage, count, to) in stages)
            {
                for (int k = 1; k <= count; k++)
                {
                    float u = k / (float)count;
                    bool fast = stage == Stage.Swing || stage == Stage.Strike;
                    frames.Add(Lerp(at, to, fast ? (float)System.Math.Pow(u, 1.3) : u * u * (3 - 2 * u)));
                    kinds.Add(stage);
                }
                at = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        // The wind-ups' last poses, which the blows start from.
        static readonly VPose Reared = V(13, -5, spread: 2, lean: -5, stretch: 0.1f, stepA: -1);
        static readonly VPose Lifted = V(11, -1, lean: -3, stretch: 0.06f, rock: 1);
        static readonly VPose Slammed = V(15, 0, spread: 3, lean: -2, stretch: 0.12f);
        static readonly VPose Sunk = V(sink: 39, raise: 2, reach: 3, mound: 18, moundHeight: 7);
        static readonly VPose Sprung = V(7, 0, stretch: 0.08f, lift: 70, liftA: 3, liftB: 3);

        // The claw: the drills brought down and across, the far one a frame behind, then back to rest.
        static readonly (VPose[] Frames, Stage[] Stages) Swipe = Expand(Reared,
            (Stage.Swing, 3, V(7, 6, lean: 3, stretch: -0.04f, stepA: 2, raiseB: 11, reachB: 1)),
            (Stage.Strike, 1, V(0, 10, lean: 6, stretch: -0.06f, stepA: 2, raiseB: 4, reachB: 7)),
            (Stage.Follow, 1, V(-3, 5, lean: 5, stretch: -0.06f, stepA: 2)),
            (Stage.Recover, 6, Rest));

        // The rock thrown: brought forward from overhead and let go, the arms following through.
        static readonly (VPose[] Frames, Stage[] Stages) Throw = Expand(Lifted,
            (Stage.Swing, 2, V(9, 6, lean: 3, stretch: -0.04f, stepA: 2, rock: 0.6f)),
            (Stage.Strike, 1, V(2, 10, lean: 6, stretch: -0.04f, stepA: 2)),
            (Stage.Follow, 1, V(-1, 4, lean: 4, stretch: -0.04f, stepA: 2)),
            (Stage.Recover, 6, Rest));

        // Both fists brought down on the floor (the cave-in, and landing from the quake).
        static readonly (VPose[] Frames, Stage[] Stages) Pound = Expand(Slammed,
            (Stage.Swing, 2, V(5, 2, lean: 1, stretch: -0.02f)),
            (Stage.Strike, 1, V(-4, 5, lean: 2, stretch: -0.16f, mound: 8, moundHeight: 2, ahead: true)),
            (Stage.Follow, 2, V(-2, 2, lean: 2, stretch: -0.08f, mound: 8, moundHeight: 2, ahead: true)),
            (Stage.Recover, 6, Rest));

        // Up out of the floor where the mark was: bursting up tall, arms high, then settling.
        static readonly (VPose[] Frames, Stage[] Stages) Rise = Expand(Sunk,
            (Stage.Swing, 5, V(9, 0, spread: 2, stretch: 0.06f, sink: 8, mound: 18, moundHeight: 4)),
            (Stage.Strike, 1, V(11, 0, spread: 3, stretch: 0.12f, lift: 3, mound: 18, moundHeight: 3)),
            (Stage.Follow, 2, V(6, 0, spread: 2, stretch: 0.04f, mound: 14, moundHeight: 2)),
            (Stage.Recover, 6, Rest));

        static (VPose[] Frames, Stage[] Stages) BlowOf(VeinPose pose) => pose switch
        {
            VeinPose.Swipe => Swipe,
            VeinPose.Throw => Throw,
            VeinPose.Pound => Pound,
            VeinPose.Rise => Rise,
            _ => (null, null),
        };

        // How many frames each pose has.
        public static int FramesOf(VeinPose pose) => BlowOf(pose).Frames?.Length ?? pose switch
        {
            VeinPose.Idle => 4,
            VeinPose.Walk => 8,
            VeinPose.Rush => 8,
            VeinPose.Leap => 18,
            VeinPose.Roar => RoarRise + 4,
            _ => 12,
        };

        // The frames a roar takes to rear up, before it shakes there.
        public const int RoarRise = 8;

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(VeinPose pose, int frame)
        {
            var stages = BlowOf(pose).Stages;
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static int StrikeFrame(VeinPose pose) => System.Array.IndexOf(BlowOf(pose).Stages ?? new Stage[0], Stage.Strike);

        public static bool Swinging(VeinPose pose, int frame)
        {
            var stages = BlowOf(pose).Stages;
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        static VPose PoseOf(VeinPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            var blow = BlowOf(pose).Frames;
            if (blow != null) return blow[f];
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case VeinPose.Idle:
                {
                    float b = 0.5f - 0.5f * c;
                    return V(b, stretch: 0.02f * b);
                }
                case VeinPose.Walk:
                    // A stride each way: the foot coming forward lifted, the drills swinging against the legs, the body
                    // dipping as each foot lands.
                    return V(raise: System.Math.Max(0, -c), reach: -2 * s, raiseB: System.Math.Max(0, c), reachB: 2 * s, lean: 1,
                        stretch: -0.03f * (1 - System.Math.Abs(c)), stepA: 2 * s, stepB: -2 * s, liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c));
                case VeinPose.Rear:
                    // The drills drawn up and back over its shoulders, rearing up onto the back foot.
                    return Keys(t, (0, Rest), (0.45f, V(6, -2, spread: 1, lean: -2, stretch: 0.05f, stepA: -0.5f)), (1, Reared));
                case VeinPose.Lift:
                    // Scooping down for the rock, then up with it overhead.
                    return Keys(t, (0, Rest), (0.35f, V(-3, 4, lean: 3, stretch: -0.12f, stepA: 1)), (0.55f, V(3, 3, lean: 1, stretch: -0.06f, stepA: 1, rock: 1)),
                        (1, Lifted));
                case VeinPose.Crouch:
                {
                    // Head down, pawing the ground: settling into the crouch, then the front drill scraping back and forth.
                    float settle = System.Math.Min(1, t / 0.25f);
                    settle = settle * settle * (3 - 2 * settle);
                    float paw = (float)System.Math.Sin(t * System.Math.PI * 6) * settle;
                    return V(raise: -settle, reach: 0, raiseB: -2 * settle, reachB: 1.5f * settle + 2.5f * paw, lean: 5 * settle, stretch: -0.1f * settle, stepB: -2 * settle);
                }
                case VeinPose.Rush:
                    // Running: the drills swept back, the legs going fast.
                    return V(1, -3, lean: 7, stretch: -0.1f - 0.02f * (1 - System.Math.Abs(c)), stepA: 3 * s, stepB: -3 * s,
                        liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c));
                case VeinPose.Sink:
                {
                    // Digging in, drill after drill, going down into the earth.
                    float dig = (float)System.Math.Sin(t * System.Math.PI * 4);
                    var p = Keys(t, (0, Rest), (1, Sunk));
                    p.RaiseA = 2 + 3 * dig;
                    p.RaiseB = 2 - 3 * dig;
                    p.ReachA = p.ReachB = 3 * System.Math.Min(1, t * 4);
                    return p;
                }
                case VeinPose.Slam:
                    // Both fists raised higher and higher for the cave-in.
                    return Keys(t, (0, Rest), (0.5f, V(8, 0, spread: 1.5f, lean: -1, stretch: 0.07f)), (1, Slammed));
                case VeinPose.Plunge:
                    // Up, then driven down into the floor ahead; earth heaped where they go in.
                    return Keys(t, (0, Rest), (0.4f, V(7, 4, lean: 3, stretch: -0.07f, stepA: 1)), (0.75f, V(-4, 4, lean: 5, stretch: -0.14f, stepA: 1, mound: 6, moundHeight: 1, ahead: true)),
                        (1, V(-6, 4, lean: 6, stretch: -0.16f, stepA: 1, mound: 8, moundHeight: 2, ahead: true)));
                case VeinPose.Curl:
                    // Curling into a ball for the spin.
                    return Keys(t, (0, Rest), (1, V(2, 0, spread: -3, stretch: -0.22f, liftA: 1, liftB: 1)));
                case VeinPose.Leap:
                    // A deep crouch, a last push down, then up and out of the top of the picture.
                    return Keys(t, (0, Rest), (0.4f, V(-3, 2, stretch: -0.2f)), (0.5f, V(1, 1, stretch: -0.1f)), (0.6f, V(6, 0, stretch: 0.1f, lift: 6, liftA: 2, liftB: 2)), (1, Sprung));
                case VeinPose.Roar:
                    if (f < RoarRise)
                        return Keys(f / (float)RoarRise, (0, Rest), (1, V(5, 0, spread: 5, lean: -4, stretch: 0.08f)));
                    return V(5 + (s + 1) / 2, 0, spread: 5, lean: -4, stretch: 0.08f);
                default:
                    return Rest;
            }
        }

        static Moves MovesOf(BossView view, VPose p)
        {
            float raiseA = p.RaiseA, reachA = p.ReachA, raiseB = p.RaiseB, reachB = p.ReachB, spread = p.Spread;
            float stepA = p.StepA, liftA = p.LiftA, stepB = p.StepB, liftB = p.LiftB;
            bool side = view != BossView.Front && view != BossView.Back;
            int R(float v) => (int)System.Math.Round(v);
            var moves = new Moves();
            if (side)
            {
                moves.ArmAX = R(reachA);
                moves.ArmAY = R(-raiseA);
                moves.ArmBX = R(reachB);
                moves.ArmBY = R(-raiseB);
                moves.LegAX = R(stepA);
                moves.LegAY = R(-liftA);
                moves.LegBX = R(stepB);
                moves.LegBY = R(-liftB);
            }
            else
            {
                float dip = view == BossView.Front ? 0.4f : -0.2f;
                moves.ArmAX = R(-spread);
                moves.ArmAY = R(-raiseA + reachA * dip);
                moves.ArmBX = R(spread);
                moves.ArmBY = R(-raiseB + reachB * dip);
                moves.LegAY = R(-liftA - System.Math.Max(0, stepA) * 0.5f);
                moves.LegBY = R(-liftB - System.Math.Max(0, stepB) * 0.5f);
            }
            return moves;
        }

        // The picture with each part at its place for the pose, on a canvas with room above and to the sides.
        static PixelImage Compose(BossView view, VPose pose)
        {
            var image = ViewOf(view);
            var parts = PartsOf(view);
            var moves = MovesOf(view, pose);
            int w = image.Width, h = image.Height;
            var canvas = new PixelImage(w + 2 * Margin, h + Headroom);
            // The body first; where a part that has moved was inside the body, shadow.
            for (int y = 0; y < h; y++)
            {
                bool Body(int x) => image[x, y].A > 0 && parts[x, y] == Part.Body;
                for (int x = 0; x < w; x++)
                {
                    var c = image[x, y];
                    if (c.A == 0) continue;
                    if (parts[x, y] == Part.Body) canvas[x + Margin, y + Headroom] = c;
                    else
                    {
                        // The nearest body on each side of the gap; between them, the nearer one's colour in shade.
                        int left = -1, right = -1;
                        for (int k = x - 1; k >= 0 && left < 0; k--) if (Body(k)) left = k;
                        for (int k = x + 1; k < w && right < 0; k++) if (Body(k)) right = k;
                        if (left >= 0 && right >= 0)
                        {
                            var near = image[x - left <= right - x ? left : right, y];
                            canvas[x + Margin, y + Headroom] = IsDark(near) ? Shadow : near.Mix(Shadow, 0.55f);
                        }
                    }
                }
            }
            void Lay(Part part, int dx, int dy)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (parts[x, y] == part) canvas[x + Margin + dx, y + Headroom + dy] = image[x, y];
            }
            // An arm moved far is joined back to the body by its forearm: its shape laid dark along the way.
            void Reach(Part part, int dx, int dy)
            {
                int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) / 3;
                for (int i = 1; i <= steps; i++)
                {
                    int sx = dx * i / (steps + 1), sy = dy * i / (steps + 1);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (parts[x, y] == part && !IsMetal(image[x, y])) canvas[x + Margin + sx, y + Headroom + sy] = IsDark(image[x, y]) ? image[x, y] : Limb;
                }
            }
            Lay(Part.LegB, moves.LegBX, moves.LegBY);
            Lay(Part.LegA, moves.LegAX, moves.LegAY);
            Reach(Part.ArmB, moves.ArmBX, moves.ArmBY);
            Lay(Part.ArmB, moves.ArmBX, moves.ArmBY);
            Reach(Part.ArmA, moves.ArmAX, moves.ArmAY);
            Lay(Part.ArmA, moves.ArmAX, moves.ArmAY);
            return canvas;
        }

        // One frame. A wind-up runs frame 0..5 through its telegraph, a follow-through (Swipe, Throw, Rise, Pound)
        // 0..2, a walk or a run 0..3, sinking 0..3, a leap 0 crouched and 1..3 rising (and 0..1 breathing at rest);
        // phase 3 burns its ore hot; rage turns it red (the frenzy). Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, VeinPose pose, int frame, int phase, bool rage) => Render(view, pose, frame, phase, rage, out _);

        // Where the two drill arms (the middle of each) and the body's edges are in a frame: for the preview's check that
        // nothing jumps from one frame to the next.
        public struct Joints
        {
            public float AX, AY, BX, BY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, VeinPose pose, int frame)
        {
            Render(view, pose, frame, 1, false, out var joints);
            return joints;
        }

        static readonly Dictionary<BossView, (float AX, float AY, float BX, float BY)> ArmMiddles = new Dictionary<BossView, (float, float, float, float)>();

        static (float AX, float AY, float BX, float BY) ArmMiddle(BossView view)
        {
            if (ArmMiddles.TryGetValue(view, out var known)) return known;
            var parts = PartsOf(view);
            float ax = 0, ay = 0, bx = 0, by = 0;
            int na = 0, nb = 0;
            for (int y = 0; y < parts.GetLength(1); y++)
                for (int x = 0; x < parts.GetLength(0); x++)
                {
                    if (parts[x, y] == Part.ArmA) { ax += x; ay += y; na++; }
                    if (parts[x, y] == Part.ArmB) { bx += x; by += y; nb++; }
                }
            known = (ax / System.Math.Max(1, na), ay / System.Math.Max(1, na), bx / System.Math.Max(1, nb), by / System.Math.Max(1, nb));
            ArmMiddles[view] = known;
            return known;
        }

        // One frame. Phase 3 burns its ore hot; rage turns it red (the frenzy); rearing for the cave-in, the quake or bursting
        // up out of the floor heats it. Faces right; WorldView flips it for the left.
        static PixelImage Render(BossView view, VeinPose pose, int frame, int phase, bool rage, out Joints joints)
        {
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            var p = PoseOf(pose, frame);
            var body = Compose(view, p);
            bool side = view != BossView.Front && view != BossView.Back;
            // Lean (top moves ahead), stretch (taller and thinner), lift (up off the ground) and sink (into it).
            float lean = p.Lean, stretch = p.Stretch, lift = p.Lift;
            int sink = (int)System.Math.Round(p.Sink);
            if (!side)
            {
                // Seen from the front or back a lean shows as a dip or a rise.
                stretch -= lean * 0.012f;
                lean = 0;
            }

            int w = body.Width;
            int h = body.Height;
            int tall = System.Math.Max(4, (int)System.Math.Round(h * (1 + stretch)));
            int wide = System.Math.Max(4, (int)System.Math.Round(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2;
            int ground = Height - 1 - (int)System.Math.Round(lift);
            bool heated = pose == VeinPose.Slam || pose == VeinPose.Leap || pose == VeinPose.Rise;
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
                        else if (heated) c = c.Mix(Hot, 0.35f);
                    }
                    int px = left + x + shift;
                    if (!image.Inside(px, row)) continue;
                    image[px, row] = c;
                    joints.Top = System.Math.Min(joints.Top, row);
                    joints.Bottom = System.Math.Max(joints.Bottom, row);
                    joints.Left = System.Math.Min(joints.Left, px);
                    joints.Right = System.Math.Max(joints.Right, px);
                }
            }
            // The arms' middles, moved and carried with the body.
            var moves = MovesOf(view, p);
            var middle0 = ArmMiddle(view);
            (float X, float Y) Place(float x, float y)
            {
                float row = ground - (tall - 1) + sink + y * tall / h;
                float up = (ground - row + sink) / (tall - 1);
                return (left + x * wide / w + lean * up, row);
            }
            (joints.AX, joints.AY) = Place(middle0.AX + Margin + moves.ArmAX, middle0.AY + Headroom + moves.ArmAY);
            (joints.BX, joints.BY) = Place(middle0.BX + Margin + moves.ArmBX, middle0.BY + Headroom + moves.ArmBY);

            // The rock is held between the two drills, just above the higher one.
            if (p.Rock > 0.5f) Boulder(image, (int)System.Math.Round((joints.AX + joints.BX) / 2), (int)System.Math.Round(System.Math.Min(joints.AY, joints.BY)) - 7, 7);
            if (p.MoundHeight >= 0.5f) Mound(image, Width / 2 + (p.Ahead && side ? 10 : 0), Height - 1, (int)System.Math.Round(p.MoundHalf), (int)System.Math.Round(p.MoundHeight));
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
    }
}
