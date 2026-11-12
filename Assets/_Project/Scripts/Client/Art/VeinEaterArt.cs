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

        static Moves MovesOf(BossView view, VeinPose pose, int frame)
        {
            float t = frame / 5f;
            float raiseA = 0, reachA = 0, raiseB = 0, reachB = 0, spread = 0, stepA = 0, liftA = 0, stepB = 0, liftB = 0;
            void Arms(float raise, float reach)
            {
                raiseA = raiseB = raise;
                reachA = reachB = reach;
            }
            int f = System.Math.Max(0, frame);
            switch (pose)
            {
                case VeinPose.Idle:
                    Arms(f % 2 == 1 ? 1 : 0, 0);
                    break;
                case VeinPose.Walk:
                    // A step each side in turn, the arms swinging against the legs.
                    stepA = new[] { 2, 0, -2, 0 }[f % 4];
                    liftA = f % 4 == 0 ? 2 : 0;
                    stepB = -stepA;
                    liftB = f % 4 == 2 ? 2 : 0;
                    reachA = -stepA;
                    reachB = stepA;
                    raiseA = liftB / 2;
                    raiseB = liftA / 2;
                    break;
                case VeinPose.Rear:
                    // The drills drawn up and back over its shoulders.
                    Arms(3 + 10 * t, -2 - 3 * t);
                    spread = 2 * t;
                    stepA = -t;
                    break;
                case VeinPose.Swipe:
                    // Brought down and through: high and out, across, low; the far arm a frame behind.
                    Arms(new[] { 7, 0, -3 }[System.Math.Min(f, 2)], new[] { 6, 10, 5 }[System.Math.Min(f, 2)]);
                    if (f == 0) (raiseB, reachB) = (11, 0);
                    stepA = 2;
                    break;
                case VeinPose.Lift:
                    // Scooping down for the rock, then up with it overhead.
                    if (t < 0.4f) Arms(-3, 4);
                    else Arms(11, -1);
                    if (t < 0.4f) stepA = 1;
                    break;
                case VeinPose.Throw:
                    Arms(new[] { 9, 2, -1 }[System.Math.Min(f, 2)], new[] { 6, 10, 4 }[System.Math.Min(f, 2)]);
                    stepA = 2;
                    break;
                case VeinPose.Crouch:
                    // Pawing the ground: the front drill scrapes back and forth, the back foot set.
                    raiseA = -1;
                    raiseB = -2;
                    reachB = f % 2 == 0 ? 4 : -1;
                    stepB = -2;
                    break;
                case VeinPose.Rush:
                    // Running: the drills swept back, the legs going fast.
                    Arms(1, -3);
                    stepA = new[] { 3, 0, -3, 0 }[f % 4];
                    liftA = f % 4 == 0 ? 2 : 0;
                    stepB = -stepA;
                    liftB = f % 4 == 2 ? 2 : 0;
                    break;
                case VeinPose.Sink:
                    // Digging in, drill after drill.
                    raiseA = f % 2 == 0 ? -3 : 2;
                    raiseB = f % 2 == 0 ? 2 : -3;
                    reachA = reachB = 3;
                    break;
                case VeinPose.Rise:
                    Arms(11, 0);
                    spread = 3;
                    break;
                case VeinPose.Slam:
                    // Both fists raised higher and higher for the cave-in.
                    Arms(4 + 11 * t, 0);
                    spread = 3 * t;
                    break;
                case VeinPose.Pound:
                    Arms(new[] { -2, -4, -2 }[System.Math.Min(f, 2)], new[] { 3, 5, 2 }[System.Math.Min(f, 2)]);
                    break;
                case VeinPose.Plunge:
                    // Up, then driven down into the floor ahead.
                    Arms(7 - 13 * t, 4);
                    stepA = 1;
                    break;
                case VeinPose.Curl:
                    Arms(2, 0);
                    spread = -3;
                    liftA = liftB = 1;
                    break;
                case VeinPose.Leap:
                    if (f == 0) Arms(-3, 2);
                    else
                    {
                        Arms(7, 0);
                        liftA = liftB = 3;
                    }
                    break;
                case VeinPose.Roar:
                    Arms(5 + (f % 2), 0);
                    spread = 5;
                    break;
            }
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
        static PixelImage Compose(BossView view, VeinPose pose, int frame)
        {
            var image = ViewOf(view);
            var parts = PartsOf(view);
            var moves = MovesOf(view, pose, frame);
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
        public static PixelImage Draw(BossView view, VeinPose pose, int frame, int phase, bool rage)
        {
            var body = Compose(view, pose, frame);
            bool side = view != BossView.Front && view != BossView.Back;
            float t = frame / 5f;
            // Lean (top moves ahead), stretch (taller and thinner), lift (up off the ground) and sink (into it).
            float lean = 0, stretch = 0, lift = 0;
            int sink = 0;
            switch (pose)
            {
                case VeinPose.Idle:
                    stretch = frame % 2 == 1 ? 0.02f : 0;
                    break;
                case VeinPose.Walk:
                    // Down a little as each foot lands.
                    stretch = frame % 2 == 1 ? -0.03f : 0;
                    lean = 1;
                    break;
                case VeinPose.Rear:
                    lean = -2 - 3 * t;
                    stretch = 0.04f + 0.06f * t;
                    break;
                case VeinPose.Swipe:
                    lean = new[] { 3, 6, 5 }[System.Math.Min(frame, 2)];
                    stretch = -0.06f;
                    break;
                case VeinPose.Lift:
                    stretch = t < 0.4f ? -0.12f : 0.06f;
                    lean = t < 0.4f ? 3 : -3;
                    break;
                case VeinPose.Throw:
                    lean = new[] { 3, 6, 4 }[System.Math.Min(frame, 2)];
                    stretch = -0.04f;
                    break;
                case VeinPose.Crouch:
                    lean = 5;
                    stretch = -0.1f;
                    break;
                case VeinPose.Rush:
                    lean = 7;
                    stretch = -0.1f;
                    break;
                case VeinPose.Sink:
                    sink = 6 + frame * 11;
                    break;
                case VeinPose.Rise:
                    stretch = 0.12f;
                    lift = 3;
                    break;
                case VeinPose.Slam:
                    lean = -2;
                    stretch = 0.06f + 0.06f * t;
                    break;
                case VeinPose.Pound:
                    lean = 2;
                    stretch = new[] { -0.1f, -0.16f, -0.08f }[System.Math.Min(frame, 2)];
                    break;
                case VeinPose.Plunge:
                    lean = 2 + 4 * t;
                    stretch = -0.06f - 0.1f * t;
                    break;
                case VeinPose.Curl:
                    stretch = -0.14f - 0.08f * t;
                    break;
                case VeinPose.Leap:
                    // Down into a deep crouch, then up and away out of the top of the picture.
                    stretch = frame == 0 ? -0.2f : 0.08f;
                    lift = frame == 0 ? 0 : frame * 14;
                    break;
                case VeinPose.Roar:
                    lean = -4;
                    stretch = 0.08f;
                    break;
            }
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

            // Where the head is: below the room left over the picture.
            int top = ground - tall + 1 + sink + (int)System.Math.Round(Headroom * tall / (float)h);
            int middle = Width / 2 + (int)System.Math.Round(lean * 0.8f);
            switch (pose)
            {
                case VeinPose.Lift when t >= 0.4f:
                    Boulder(image, middle - 1, top - 9, 7);
                    break;
                case VeinPose.Sink:
                    Mound(image, Width / 2, Height - 1, 12 + frame * 2, 3 + frame);
                    break;
                case VeinPose.Rise:
                    Mound(image, Width / 2, Height - 1, 18, 3);
                    break;
                case VeinPose.Pound:
                case VeinPose.Plunge when frame >= 3:
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
    }
}
