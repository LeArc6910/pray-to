using System.Collections.Generic;
using System.Linq;

namespace Game.Client
{
    // What the cave warden is doing, which picks its frame (WorldView.Warden.cs, Docs/Bosses.md 동굴 수호자). The first
    // boss: every blow has a wind-up that is big, slow and different from the others, so a new player learns to read
    // the body before the floor. Both fists hauled up overhead and brought down ahead (Raise, then Slam) or at its own
    // feet (Raise, then Pound); one fist drawn back and swung across the front (Wind, Sweep); the arms folded over the
    // glowing core and flung open (Hunch, Burst); a foot lifted high and stamped (Lift, Stomp); one fist raised high
    // and driven down beside it (Coil, Punch); a fist drawn back low and gouged along the floor (Scoop, Gouge); a
    // crouch and both fists driven up into the roof (Heave, Uppercut); arms spread, the core blazing, then everything
    // brought down (Gather, then Pound); head down for a run (Brace, Charge); a roar with the arms flung wide.
    enum WardenPose
    {
        Idle,
        Walk,
        Raise,
        Slam,
        Pound,
        Wind,
        Sweep,
        Hunch,
        Burst,
        Lift,
        Stomp,
        Coil,
        Punch,
        Scoop,
        Gouge,
        Heave,
        Uppercut,
        Gather,
        Brace,
        Charge,
        Roar,
        // Its entrance (Docs/Bosses.md 동굴 수호자): crouched still as a heap of mossy stone among the ruins; its eyes
        // light, then the core in its chest; it pushes itself up, hauls both fists overhead and pounds the floor.
        Enter,
        // Its fall (WorldView.WardenFalls): staggered back, the core flickering out, down on its knees, slumped forward
        // onto its fists, and crumbling into a heap of rubble.
        Die,
    }

    // The cave warden of the fallen shrine (2026-11-26): a hulking golem of mossy cave rock with an amber crystal core
    // in its chest. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md,
    // $0.28); every pose is made in code: the arms and legs are a box each per view, moved apart from the body (the
    // gap they leave filled with shade, an arm moved far joined back to its shoulder by a stone upper arm drawn
    // here), and the whole body leans, rises, squashes on the blow and is carried into it. Its core and eyes glow with
    // the motion (dark while it sleeps, blazing before its biggest blows). Faces right.
    static class WardenArt
    {
        public const int Width = 112;
        public const int Height = 100;
        // Rows under its feet; WorldView stands the picture on this row (Pivot).
        public const int Floor = 4;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 50;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(0, 0, 0),
            ['b'] = new Rgba(2, 0, 2),
            ['c'] = new Rgba(2, 0, 15),
            ['d'] = new Rgba(4, 0, 17),
            ['e'] = new Rgba(15, 9, 5),
            ['f'] = new Rgba(44, 32, 25),
            ['g'] = new Rgba(56, 38, 32),
            ['h'] = new Rgba(60, 46, 37),
            ['i'] = new Rgba(70, 53, 43),
            ['j'] = new Rgba(78, 63, 52),
            ['k'] = new Rgba(36, 90, 31),
            ['l'] = new Rgba(83, 72, 61),
            ['m'] = new Rgba(34, 106, 30),
            ['n'] = new Rgba(29, 113, 28),
            ['o'] = new Rgba(15, 127, 25),
            ['p'] = new Rgba(93, 86, 70),
            ['q'] = new Rgba(103, 97, 80),
            ['r'] = new Rgba(93, 108, 79),
            ['s'] = new Rgba(114, 107, 85),
            ['t'] = new Rgba(101, 124, 82),
            ['u'] = new Rgba(233, 131, 37),
            ['v'] = new Rgba(241, 144, 46),
            ['w'] = new Rgba(252, 182, 55),
        };

        static readonly string[] FrontRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "...........................caaaaaaccac..........................",
            "..........................dssssssnnnmsa.........................",
            ".........................dsssssssnnnmrqc........................",
            ".........................clsssssssrkkrsa........................",
            ".........................alllsssssssssjd........................",
            "......................aaaalllllllllllllbad......................",
            ".....................amnkalhlllllllllhjanraaaaac................",
            "................caaaanonmaihguwllllwvsiannfesssha...............",
            "..............carsssamnomaihgswwllpwwiibimrgsssotd..............",
            ".............dfmmmsspltnmapelllllppllleaissgissnnma.............",
            ".............aonnmsspgstkbgsffilfgfllftaisssgsstnma.............",
            "............dtnnmsssshqsjbiqssffsssffspaisssgssmnma.............",
            "............bstttsskshqplbiijssgqssisspaijjjgssrmmra............",
            "............astmmsskshhilhbilsssssssssiaissjgsssktsc............",
            "............assmmsssshhssqgahssssqqssibjssssgssssssa............",
            "............assssssssgqssssgebbbaaaabbissmmqesqsqssa............",
            "............ahsssssssfqrmmtsiiihhfggiistnnmqafssssfsc...........",
            "...........csffssssghbqkmmmssssqqfppqssmnmrqaggggfssa...........",
            "...........assffggghhbpkmnnsssljljjjjsskmtsqaeeeisssa...........",
            "..........aisssffggghbhrmnmssllvvvvujiqqrspgaggssssfsc..........",
            "..........agsssqqgghbbhqsssssluwwvwvvjqqsjjbhggssssfsa..........",
            ".........cssgssqqpggafgflssssjuwwuvvvjsqqlabhgggjhfsssd.........",
            ".........asssfgsggggafhhffffijvvuuuuujffffaabbbaehssssa.........",
            "........apssssgghgga.ahhhqqqijvwwuwwujissiaaggggssssmsa.........",
            "........amtmsssfebba.chgqqqssijwwuwsiisspa.aggggssssrsa.........",
            "........annnssslllja..afgqnrispqqqqpjqrrec.aggggjssmmra.........",
            "........annnssslllja..afhrmrqsiijjjjgpkka..agggggssnoona........",
            "........enntsssjllja...aeefnliqqqqqqgkkfa..agggghssmoooa........",
            "........amtsskshqllc..cirtfffffffffffffqpc.cgggggpsmtooa........",
            "........bsssskrhsllc..bhhstmssskmmmmmsslla..ahhgggsstooa........",
            "........asssssshspfd..ehhlpmrrssssssssspfa..cheeffgistta........",
            "........bssssssghiia..deffllssssssssssffhpc.cheessqiiqqa........",
            "........asssssihssslasbplpgflssssssjqffissb.aafpssfsiipa........",
            "........aisshiissssssaisssshffsqqppfefsssslbhaipqefsssa.........",
            ".........ahhhsssshsssaissssqpjbbcaaaiqqsssjajaalbehsssd.........",
            ".........asssssssgajsapssssjlga...bfgpqppqlaiaaaagjissc.........",
            ".........cssssshihaaabpmmssllga...dggiqfiiilaagejjjslqc.........",
            "..........bpsshgllllahhhhgiihgc....aaaffsssiaahhjsssqa..........",
            "..........djiihllllpbhssssffba.....agggqsssiasddsssqa...........",
            "...........dplllllebbgssssiiha.....agggglllga...daac............",
            "............caacaadbgggljjihhd.....agggggggga...................",
            "...................bggfggggghb.....agggggggga...................",
            "...................bfeeeeeggha.....bggggsssssd..................",
            "...................assssslehha.....agggslssssqc.................",
            "..................cssssssslbhc.....cggfilllllpa.................",
            "..................assssrsslfac......caaaaabaaac.................",
            "..................aaaabbabbaa...................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] FrontSideRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "...............................aaaaaaaa.........................",
            ".............................aassssnnnna........................",
            "............................cpssssssmnnna.......................",
            "............................alssssssssmnra......................",
            "...........................aallssssssssssa......................",
            ".........................aaralllssssssssla......................",
            ".......................aanmfallllllllllllaaaa...................",
            "......................annnkfapflljhlllluwassna..................",
            ".....................anoonkfalgllgvwlllwpassnna.................",
            "..................aaapmoonmaflllllqwwllljaisnna.................",
            "................aasssggssmkaillpgflllllfsahssra.................",
            "...............cknnsssqfsskahlllssflljfhhafpssra................",
            "...............annmmrssqhsshafllsssfffssjaehsssa................",
            "..............amnnnnnmsshjsqgaeipssssjsssaigissa................",
            "..............annnnntmsshiqhigaahisssssssaqagssa................",
            ".............asnnnnnsrmshhhqssifafjsssaaasmaglsa................",
            ".............astnnmtsssshghssssqgaaaaaghirmagga.................",
            ".............asstttmmssqggsstmmssjhhhfpssnnagsa.................",
            ".............ajssssnnssleissmmnnsssssgppsnnassaa................",
            "..............ahsssssslgbissmmnnsssllljjpsagssbsa...............",
            "..............abghqssiggafjstmnnssllvvvujqahpfssa...............",
            "..............aqfggffghaffipsstmsslvwvwvjabbelsna...............",
            ".............cbsgssssshageeipsssspjvwuvvjagglssoa...............",
            ".............assgssssshagggffjqsifjvvuuvpagllssona..............",
            ".............ahhggssslhaggggfffffilvwvwskagllpstna..............",
            "............afsssgghhhaagggjsssssgjuwvspaggjllpsna..............",
            "............ashssssgfba.agfjpsssssgqqplmaggghiiiqa..............",
            "...........asshsssssspa..affhprmmrphhhiaagebissssa..............",
            "...........assgsssntsphaagaafikmkkhsspepaggghbssssa.............",
            "...........asggsrnnnmsjaagggbeefffffffsqaaggapsljsa.............",
            "............ajqsmnnonsjaagglqsrmtrmmssspaaggapqeisa.............",
            "............ajqrnnntntqjagghjqsmsssssslfqaggapaepsa.............",
            "............aiisnnntrssiaffffjpssssssgflsjaaaaagisa.............",
            "............aihstnmssssigaaqqffgsssslessslagebhlsqa.............",
            ".............ahssstsssshsssapsgffllbbpqssiagejjssa..............",
            ".............ahissssssgsssssasshaaaggpqsihaafqssla..............",
            "..............aisssphhsssissassha.agghphisasaaaaa...............",
            "..............aiijjgsssssiasassga.afgggfssa.....................",
            "...............agggssssqisfassqgasagggjqsia.....................",
            "................aissssffjssahhha.aggggjhhha.....................",
            ".................ajjgffijlqbssha.aghggjllfa.....................",
            ".................aaefllaaaasssha.aggggllfjsa....................",
            "...................aaaaahllljiha.agggglgssssaa..................",
            "......................akkllllia..aggggjhpssssa..................",
            ".....................aikkkffffa..daafggijlspa...................",
            ".....................aikkfsssssa....haaaaaaa....................",
            ".....................aiifsssssssa...............................",
            "......................aagppqsiia................................",
            "........................aaaaaaa.................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] SideRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "...............................aaaaaaa..........................",
            ".............................ccssmkksscc........................",
            "............................clssssssnnmsa.......................",
            "............................allllssssskmsa......................",
            "............................allllllllssssa......................",
            "..........................aaalllllllllllla......................",
            ".........................apalpfilllllillla......................",
            "........................assalpphllllljvwla......................",
            ".......................assshaillllllllswpa......................",
            "......................amssssbahlllllfillppc.....................",
            ".....................amsssmsiaabllllsfflfqa.....................",
            ".....................aqggggfqjaahllllslffsa.....................",
            "....................cahsssssgflaaglllqssisa.....................",
            "....................ahssnonsssgjaaehhqsssa......................",
            "....................assnnoonnsgpigbbbgjssa......................",
            "....................asnnnoonnssgisssgaaaa.......................",
            "...................assnooonntssgssnrsjhc........................",
            "...................asstnonttmtsgsmnnnssc........................",
            "...................assstnssnnssgssnnnnsfc.......................",
            "...................aissstssnnssasssnnnpva.......................",
            "...................afhqsssssssfassssnnuwa.......................",
            "...................asfflsssssgagqsssssvwa.......................",
            "...................asssffggggfafiqssssvva.......................",
            "...................cssssgsssssaeegjqljvva.......................",
            "...................cjsssgsssssaggffffjva........................",
            "....................aggggggqssagjssssjsa........................",
            "....................aghsssggghahlstnslha........................",
            "...................cishsssssifagjqnnshsa........................",
            "...................asshsssssssaggmnnjpga........................",
            "...................asshsssnnnsgaffffghksa.......................",
            "...................aqflssnnnnnriannnstnisa......................",
            "....................ajsssnnnntnsajsnrsfhsa......................",
            "....................ajsssnnnntsshagpssagsa......................",
            ".....................esssmnntsssshaaisagsa......................",
            ".....................aiqqsnnsssssissaaflsa......................",
            "......................aissssssssijsssalsc.......................",
            "......................ajlpssssslissgsahsc.......................",
            ".......................ailssssihssseapcc........................",
            ".......................aillllhssssisaqc.........................",
            "........................aggggssssiisaf..........................",
            ".........................agpssspfilqaa..........................",
            ".........................aahjjjfhfjaa...........................",
            "........................ajjaffflljapfad.........................",
            "........................alllaaaaaajgssaa........................",
            "........................allllppplahgqqqa........................",
            ".......................alllllllffaghpaaa........................",
            ".......................allllllfssssaa...........................",
            ".......................alllllgsssssqa...........................",
            ".......................alllllfsssspa............................",
            ".......................aaaaaaaaaaaa.............................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] BackSideRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            ".............................aaaaaaa............................",
            "............................assssmnnaa..........................",
            "...........................allpsssssssa.........................",
            "...........................allllllllllla........................",
            "...........................allllllllllla........................",
            "..........................aallllllllllla........................",
            ".......................eaaaalllllllllppa........................",
            "...................aaaaennsabbbblllleppa........................",
            "..................amsfsnotsssssealllella........................",
            ".................amsgsnoosshsssssqaaaajqa.......................",
            "................anmqfsnnssiqsssssssonfapa.......................",
            "................amsfptnsssqsssssotttrsgaaaa.....................",
            "...............asssfsnmsslqsssstoonsqghssssa....................",
            "...............asssbssssshsssstnooopgpsstnnna...................",
            "...............asssassssshsssnnnoongpsmnnnnona..................",
            "...............apsqassssshsssnnnonmhssmnnnoona..................",
            "..............ahfjgassssgssssttttsfisstmnmtnna..................",
            "..............assffassssgsssssssssfisssssstnnrb.................",
            "..............assssahssshsssssssssfhsssssssttsa.................",
            ".............aissspagiqqhssssssssqfflsssssssssa.................",
            "............cihssspgaghhghlsssssslfagsssssssssa.................",
            "............assgsggggaffffgijjjjiifagghsssssia..................",
            "............assgjggggaqsshffghhgggbahslgggggga..................",
            "............agssfbabaalsshssggggfefagsssssqqsia.................",
            "............agggfgggggahjhssssssssiagpsssssgsia.................",
            "............assssgghggaghigpssssspggagsssssggga.................",
            "............assssigghhgagggglqqqpgggagglsplgpssa................",
            "............asssssgghhgaagggggggggggaeefgggisssa................",
            "............asssssgghhaaneeeegggggggahpssssgssssa...............",
            "............assslggggaaaomssqfffggggahpssssgsssta...............",
            "............asjijgggfhgassssssssbbaaagfjplhqssnnna..............",
            "............aihhheffghgassssssssnnmgafgfffjsstnnna..............",
            "............agsspeigggeahssssssssspggaghjjisstonnsa.............",
            ".............bssphheebbqblpqissssspggaggjjijstnntsa.............",
            ".............assjfgggeaspbbgggsssspgeaaggiihssnnssa.............",
            ".............aipsfpghgasssieaggggfffjnaeghihgsssssa.............",
            "..............ahpqqqfgasssspfaaaaeissnnagghhgsssssa.............",
            "...............aaaaaaaifqppifaaahsssssnmaggggssssgsa............",
            ".....................assffffgggahsssssslaggggsssihsa............",
            ".....................aqqppllggfaaqsssssjeagggjjggsqa............",
            ".....................ansspppgga.ajqssssjbaaabfgssla.............",
            "....................akmsspllgga..aafgggfahgggpsspa..............",
            "....................amssspllgga.agssssssfaggglqpa...............",
            "....................akhhlllgggaaagssssmsjaaaaaaa................",
            "....................apsllllghhaaahsssoosja......................",
            "....................apsljjhghhaaahsstonslea.....................",
            "....................aaaaaaaaaa..ahsnotssgssa....................",
            "................................ahsssssissssa...................",
            "................................ahssssshsssaa...................",
            "................................aaaaaaaaaaa.....................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] BackRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "...........................ddcaaaaac............................",
            "..........................aoooossoooa...........................",
            ".........................allllllllllla..........................",
            ".........................allllllllllla..........................",
            ".........................allllllllllla..........................",
            ".........................alllllllllllaa.........................",
            "........................allllllllllllpa.........................",
            "........................aababbbbbaaaaaa.........................",
            "......................aaanttssssssssssnaaa......................",
            "................caaaaainnnntssssissstooooeaaaaac................",
            "...............annssqbsnnnntssssissstoooosejssnna...............",
            "..............annnnnsgsnnnntssssissstoooosqgsnnnna..............",
            ".............atnnnnnsgsnnnntsssshsssnnooosqgsnmnnsa.............",
            ".............asrnntssgsstmnrsssshsssnnoossqfssnntta.............",
            "............anntttsssfssstmmsssshsssnnttssqfsssttnna............",
            "............amnssssssfssssrmssssgsssmsssssqfsssssnma............",
            "............assssssssesssssmssssgsssssssssqbssssssra............",
            "............asssssssibqsssssssssgsssssssssqbjssssssa............",
            "............asppppgggahsssssssssgssssssssqgagggqqqqa............",
            "............ahgggggggagjssssssssgsssssssqigagggghhhc............",
            "............asssssspgaggglssssssgsssssslgggagssssssa............",
            "...........ahsssssspgabggggghijjejiiihggggbagssssssga...........",
            "...........agsssssqggagbfgggggggegggggggebgaglsssssga...........",
            "..........cgggssssgggaghggggggggigggggggfggaggpsssgggc..........",
            "..........assggppggga.ahssssqssshsssssssqgaagggpphgssa..........",
            ".........ahsssgffggaa.aghiiiiiiifiiiiiiiibdaaggffgsssia.........",
            ".........agssssspaaga..afggggggggggggggga..agbbisssssfa.........",
            ".........aggsssslbgga...agggggggeggggggga..aggbissssgga.........",
            "........ansgfffeeggga..aabbbeeeebeeebbbba...aggefffffsma........",
            "........anssssqlggga...akmmmsssssssssnnnnc..aggggppsssna........",
            "........anssssqgggga...akmrmmsssssssnnmnna...agggpssssna........",
            "........ansssspggga....akmsssssssssssssmma...aggglssssna........",
            "........aejssspgggaa...afsssssssssssssssga..aabgglsssjea........",
            "........aiffghgggggga.amfisssssssssssssffncfhgggggggffha........",
            "........aissjggfegggaak..fgglsssgsssjggfsnaagggeefgissia........",
            "........assssqlhhggggaosssfgggggggggfffsssnaeggggjpssssa........",
            "........asssphffaafhgansssshbaaaaabbfhssssnaifbbffiqsssa........",
            ".........bssggabggbhhasssssqgga...aagpqssssaieggbbfgssa.........",
            ".........csshblfggahahgggijhga....aagilffgiafbhhflbjssc.........",
            "..........asshlqegaaahlpliebaa....aaaabjppgacaheppissa..........",
            "...........ajqliaa..asssssggga....aagggssssa..aagqsla...........",
            "............aaaa....asssssggga.....agggssssa....aaac............",
            "....................assnssgga......agggsoosa....................",
            "...................aisoooqgga......agggnoooia...................",
            "..................asisooongga......agggooorisa..................",
            "..................ashssoosgga......agggsotsisa..................",
            "..................aaelssnsgga......cgggrnsibac..................",
            "....................aaaaaaaa........aaaaaaaa....................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
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

        // Room round the picture for the fists raised overhead or swung out ahead.
        const int Margin = 24;
        const int Headroom = 30;

        // ---- Parts. The arms (forearm and fist, under the shoulder) and the legs of each view are a box each (left,
        // top, right, bottom, inclusive), the left one on the screen first. Seen from the side only the near arm shows;
        // the far one is drawn from it, in shade behind the body, when it moves out from behind. ----

        enum Part : byte
        {
            Body,
            ArmA,
            ArmB,
            LegA,
            LegB,
        }

        struct Box
        {
            public int X0, Y0, X1, Y1;

            public Box(int x0, int y0, int x1, int y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public bool Empty => X1 < X0;

            public bool Holds(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

            // The middle of the fist, near the bottom of the arm.
            public (int X, int Y) Fist => ((X0 + X1) / 2, Y1 - 5);
        }

        static readonly Box None = new Box(0, 0, -1, -1);

        static (Box ArmA, Box ArmB, Box LegA, Box LegB) BoxesOf(BossView view) => view switch
        {
            BossView.Front => (new Box(7, 26, 21, 50), new Box(43, 25, 57, 50), new Box(17, 46, 30, 57), new Box(34, 45, 47, 56)),
            BossView.FrontSide => (new Box(11, 26, 24, 50), new Box(40, 25, 50, 44), new Box(21, 45, 32, 57), new Box(33, 45, 45, 55)),
            BossView.Side => (new Box(30, 23, 42, 46), None, new Box(22, 47, 32, 58), new Box(33, 47, 41, 55)),
            BossView.BackSide => (new Box(12, 25, 24, 44), new Box(36, 23, 52, 47), new Box(20, 45, 31, 54), new Box(32, 47, 45, 56)),
            _ => (new Box(8, 26, 22, 49), new Box(41, 26, 56, 49), new Box(18, 45, 29, 55), new Box(34, 45, 46, 55)),
        };

        // The trunk behind the arms in each view: where an arm leaves from in front of it, the trunk is drawn in shade
        // there (not the floor showing through the chest).
        static Box TrunkOf(BossView view) => view switch
        {
            BossView.Front => new Box(17, 20, 47, 52),
            BossView.FrontSide => new Box(16, 20, 45, 52),
            BossView.Side => new Box(18, 20, 40, 52),
            BossView.BackSide => new Box(17, 20, 47, 51),
            _ => new Box(17, 22, 47, 51),
        };

        static readonly Dictionary<BossView, Part[,]> PartMaps = new Dictionary<BossView, Part[,]>();

        static Part[,] PartsOf(BossView view)
        {
            if (PartMaps.TryGetValue(view, out var known)) return known;
            var image = ViewOf(view);
            var (armA, armB, legA, legB) = BoxesOf(view);
            var parts = new Part[image.Width, image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y].A == 0) continue;
                    parts[x, y] = legA.Holds(x, y) ? Part.LegA : legB.Holds(x, y) ? Part.LegB : armA.Holds(x, y) ? Part.ArmA : armB.Holds(x, y) ? Part.ArmB : Part.Body;
                }
            }
            PartMaps[view] = parts;
            return parts;
        }

        static float Luminance(Rgba c) => 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;

        static readonly Rgba Shade = new Rgba(30, 22, 18);
        static readonly Rgba Stone = new Rgba(93, 86, 70);
        static readonly Rgba StoneLight = new Rgba(114, 107, 85);
        static readonly Rgba StoneDark = new Rgba(56, 38, 32);
        static readonly Rgba Moss = new Rgba(29, 113, 28);
        static readonly Rgba Ink = new Rgba(0, 0, 0);
        // The core and eyes asleep, and blazing.
        static readonly Rgba Dormant = new Rgba(60, 46, 37);
        static readonly Rgba Blaze = new Rgba(255, 246, 210);
        static readonly Rgba Ember = new Rgba(255, 92, 40);

        // ---- A pose: for each arm how far it is raised (pixels up), carried ahead (pixels, from the side) and drawn in
        // toward the middle of the body (1) or out from it (-1, seen from the front); the steps and lifts of the feet;
        // the body's lean (top ahead), stretch (taller and thinner, or squashed), lift off the ground, how far it is
        // carried ahead (shove) and dropped (drop); how bright its core and its eyes are (0 asleep, 1 awake, 2 blazing). ----

        struct Pose
        {
            public float RaiseA, AheadA, InA, RaiseB, AheadB, InB;
            public float StepA, LiftA, StepB, LiftB, Lean, Stretch, Lift, Shove, Drop, Glow, Eyes;
            // How far it has broken apart into falling stones (0 whole, 1 a heap), as it dies.
            public float Crumble;

            // The same pose with one arm put elsewhere.
            public Pose A(float raise, float ahead, float inward = 0)
            {
                var p = this;
                p.RaiseA = raise;
                p.AheadA = ahead;
                p.InA = inward;
                return p;
            }

            public Pose B(float raise, float ahead, float inward = 0)
            {
                var p = this;
                p.RaiseB = raise;
                p.AheadB = ahead;
                p.InB = inward;
                return p;
            }

            public Pose Lit(float glow, float eyes = 1)
            {
                var p = this;
                p.Glow = glow;
                p.Eyes = eyes;
                return p;
            }
        }

        // A pose with both arms alike.
        static Pose P(float raise = 0, float ahead = 0, float inward = 0, float lean = 0, float stretch = 0, float stepA = 0, float stepB = 0,
            float liftA = 0, float liftB = 0, float drop = 0, float shove = 0, float lift = 0, float glow = 1, float eyes = 1) => new Pose
        {
            RaiseA = raise, AheadA = ahead, InA = inward, RaiseB = raise, AheadB = ahead, InB = inward, Lean = lean, Stretch = stretch,
            StepA = stepA, StepB = stepB, LiftA = liftA, LiftB = liftB, Drop = drop, Shove = shove, Lift = lift, Glow = glow, Eyes = eyes,
        };

        static readonly Pose Rest = P();

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return new Pose
            {
                RaiseA = L(a.RaiseA, b.RaiseA), AheadA = L(a.AheadA, b.AheadA), InA = L(a.InA, b.InA),
                RaiseB = L(a.RaiseB, b.RaiseB), AheadB = L(a.AheadB, b.AheadB), InB = L(a.InB, b.InB),
                StepA = L(a.StepA, b.StepA), LiftA = L(a.LiftA, b.LiftA), StepB = L(a.StepB, b.StepB), LiftB = L(a.LiftB, b.LiftB),
                Lean = L(a.Lean, b.Lean), Stretch = L(a.Stretch, b.Stretch), Lift = L(a.Lift, b.Lift), Shove = L(a.Shove, b.Shove),
                Drop = L(a.Drop, b.Drop), Glow = L(a.Glow, b.Glow), Eyes = L(a.Eyes, b.Eyes), Crumble = L(a.Crumble, b.Crumble),
            };
        }

        // Key poses at times 0..1, eased in and out between each pair (the weight of stone moving).
        static Pose Keys(float t, params (float At, Pose Pose)[] keys)
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

        // A blow's frames come in stages, each frame shown for its stage's time: the swing (too fast to see, a streak
        // behind the fist), the strike, the rebound, held in the floor (the opening a new player learns to hit), hauled
        // out, back to rest; and the slow parts of the entrance.
        enum Stage : byte
        {
            Swing,
            Strike,
            Rebound,
            Held,
            Haul,
            Return,
            Slow,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.03f,
            Stage.Strike => 0.1f,
            Stage.Rebound => 0.08f,
            Stage.Held => 0.14f,
            Stage.Haul => 0.1f,
            Stage.Slow => 0.085f,
            _ => 0.06f,
        };

        // A blow laid out from key poses: each stage moves from the pose before to its own over so many frames; a swing
        // speeds up toward the strike, the rest ease in and out. To smooth a stage, give it more frames.
        static (Pose[] Frames, Stage[] Stages) Expand(Pose from, params (Stage Stage, int Frames, Pose To)[] stages)
        {
            var frames = new List<Pose>();
            var kinds = new List<Stage>();
            var at = from;
            foreach (var (stage, count, to) in stages)
            {
                for (int k = 1; k <= count; k++)
                {
                    float u = k / (float)count;
                    bool fast = stage == Stage.Swing || stage == Stage.Strike;
                    float eased = fast ? (float)System.Math.Pow(u, 1.3) : u * u * (3 - 2 * u);
                    frames.Add(Lerp(at, to, eased));
                    kinds.Add(stage);
                }
                at = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        // ---- The wind-ups' last poses, and the blows out of them. ----

        // Both fists overhead, leaning back onto the back foot, the core brightening.
        static readonly Pose RaiseTop = P(33, -2, 0.55f, lean: -4, stretch: 0.08f, stepA: 2, stepB: -2, glow: 1.3f);
        // One fist drawn far back and up, the other across the chest; the body turned away.
        static readonly Pose WindBack = P(lean: -4, stretch: 0.03f, stepA: -2, stepB: 2, glow: 1.15f).A(10, -11, -0.4f).B(2, 4, 0.2f);
        // The arms folded over the core, crouched round it, the core blazing through them.
        static readonly Pose Hunched = P(8, 5, 1.05f, lean: 2, stretch: -0.12f, drop: 2, glow: 1.7f);
        // A foot lifted high, the arms out for balance.
        static readonly Pose Lifted = P(6, -1, -0.35f, lean: -3, stretch: 0.05f, stepA: 3, liftA: 11, glow: 1.2f);
        // One fist high over its head.
        static readonly Pose Coiled = P(lean: -3, stretch: 0.06f, stepA: 1, stepB: -2, glow: 1.2f).A(28, -3, 0.35f).B(4, 0, -0.2f);
        // One fist drawn back low beside it, the body crouched over it.
        static readonly Pose Scooped = P(lean: 4, stretch: -0.08f, stepA: -1, stepB: 2, drop: 2, glow: 1.15f).A(-3, -13, -0.2f).B(2, 3);
        // A deep crouch, both fists low.
        static readonly Pose Heaved = P(-3, 3, 0.55f, lean: 3, stretch: -0.16f, drop: 3, glow: 1.2f);
        // Arms spread, then both fists higher than ever, the core blazing.
        static readonly Pose Spread = P(18, -2, -0.85f, lean: -3, stretch: 0.07f, stepA: 2, stepB: -2, glow: 1.6f);
        static readonly Pose GatherTop = P(32, -2, 0.8f, lean: -5, stretch: 0.12f, stepA: 2, stepB: -2, glow: 2.2f);
        // Head down, fists ahead like horns.
        static readonly Pose Braced = P(-2, 7, 0.2f, lean: 10, stretch: -0.12f, stepA: 4, stepB: -4, drop: 2, glow: 1.2f);
        static readonly Pose RoarTop = P(20, -2, -0.95f, lean: -5, stretch: 0.09f, stepA: 2, stepB: -2, glow: 2);

        // Overhead and down ahead: the swing, the strike with the whole body squashed behind it, a rebound, the fists
        // held in the floor, hauled out, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Slam = Expand(RaiseTop,
            (Stage.Swing, 3, P(14, 8, 0.7f, lean: 3, stretch: -0.04f, stepA: 3, stepB: -2, shove: 2, glow: 1.3f)),
            (Stage.Strike, 1, P(-6, 12, 0.6f, lean: 8, stretch: -0.16f, stepA: 4, stepB: -3, drop: 3, shove: 4, glow: 1.4f)),
            (Stage.Rebound, 1, P(-4, 12, 0.6f, lean: 7, stretch: -0.1f, stepA: 4, stepB: -3, drop: 2, shove: 4, glow: 1.2f)),
            (Stage.Held, 3, P(-6, 12, 0.6f, lean: 7, stretch: -0.12f, stepA: 4, stepB: -3, drop: 2, shove: 4)),
            (Stage.Haul, 2, P(-1, 8, 0.4f, lean: 4, stretch: -0.05f, stepA: 2, stepB: -2, drop: 1, shove: 3)),
            (Stage.Return, 8, Rest));

        // Straight down at its own feet: the shockwave, the roof's fall, the end of the entrance.
        static (Pose[] Frames, Stage[] Stages) PoundFrom(Pose from) => Expand(from,
            (Stage.Swing, 3, P(12, 3, 0.85f, lean: 2, stretch: -0.06f, drop: 1, glow: 1.4f)),
            (Stage.Strike, 1, P(-8, 4, 0.9f, lean: 4, stretch: -0.2f, drop: 4, glow: 1.5f)),
            (Stage.Rebound, 1, P(-6, 4, 0.9f, lean: 3, stretch: -0.13f, drop: 3, glow: 1.2f)),
            (Stage.Held, 3, P(-7, 4, 0.9f, lean: 3, stretch: -0.14f, drop: 3)),
            (Stage.Haul, 2, P(-2, 2, 0.5f, stretch: -0.05f, drop: 1)),
            (Stage.Return, 8, Rest));

        static readonly (Pose[] Frames, Stage[] Stages) Pound = PoundFrom(RaiseTop);

        // Across the front: out of the wind-up behind, the fist past the viewer, out ahead and across, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Sweep = Expand(WindBack,
            (Stage.Swing, 2, P(lean: 1, shove: 1, glow: 1.15f).A(6, 2, 0.3f).B(1, 2)),
            (Stage.Swing, 1, P(lean: 5, stretch: -0.05f, stepA: 3, stepB: -2, shove: 3, glow: 1.2f).A(4, 11, 0.85f).B(1, 1)),
            (Stage.Strike, 1, P(lean: 7, stretch: -0.08f, stepA: 4, stepB: -3, shove: 4, glow: 1.3f).A(3, 14, 1.15f).B(0, 0)),
            (Stage.Rebound, 1, P(lean: 6, stretch: -0.06f, stepA: 4, stepB: -3, shove: 4, glow: 1.1f).A(2, 13, 1.1f).B(0, 0)),
            (Stage.Held, 1, P(lean: 5, stretch: -0.04f, stepA: 3, stepB: -2, shove: 3).A(1, 12, 1)),
            (Stage.Return, 9, Rest));

        // The arms flung open from over the core: shards of it thrown out all round.
        static readonly (Pose[] Frames, Stage[] Stages) Burst = Expand(Hunched,
            (Stage.Swing, 1, P(10, 2, 0, glow: 2)),
            (Stage.Strike, 1, P(16, -2, -0.9f, lean: -4, stretch: 0.1f, glow: 2.2f)),
            (Stage.Rebound, 1, P(15, -2, -0.85f, lean: -3, stretch: 0.08f, glow: 1.8f)),
            (Stage.Held, 2, P(14, -1, -0.8f, lean: -3, stretch: 0.07f, glow: 1.5f)),
            (Stage.Return, 7, Rest));

        // The lifted foot stamped down.
        static readonly (Pose[] Frames, Stage[] Stages) Stomp = Expand(Lifted,
            (Stage.Swing, 2, P(3, 0, -0.3f, lean: 1, stepA: 4, liftA: 4, glow: 1.2f)),
            (Stage.Strike, 1, P(-2, 0, -0.2f, lean: 3, stretch: -0.14f, stepA: 4, drop: 3, glow: 1.3f)),
            (Stage.Rebound, 1, P(-1, 0, -0.2f, lean: 2, stretch: -0.09f, stepA: 4, drop: 2)),
            (Stage.Held, 2, P(-1, 0, -0.1f, lean: 2, stretch: -0.1f, stepA: 4, drop: 2)),
            (Stage.Return, 7, Rest));

        // The high fist driven down beside it.
        static readonly (Pose[] Frames, Stage[] Stages) Punch = Expand(Coiled,
            (Stage.Swing, 2, P(lean: 3, shove: 2, glow: 1.2f).A(12, 10, 0.3f).B(2, 0)),
            (Stage.Strike, 1, P(lean: 8, stretch: -0.12f, drop: 2, shove: 4, glow: 1.3f).A(-6, 13, 0.25f).B(0, 0)),
            (Stage.Rebound, 1, P(lean: 7, stretch: -0.08f, drop: 2, shove: 4).A(-4, 13, 0.25f).B(0, 0)),
            (Stage.Held, 2, P(lean: 7, stretch: -0.09f, drop: 2, shove: 4).A(-5, 13, 0.25f).B(0, 0)),
            (Stage.Haul, 2, P(lean: 4, stretch: -0.04f, drop: 1, shove: 2).A(0, 8, 0.15f).B(0, 0)),
            (Stage.Return, 7, Rest));

        // The low fist gouged forward along the floor.
        static readonly (Pose[] Frames, Stage[] Stages) Gouge = Expand(Scooped,
            (Stage.Swing, 2, P(lean: 6, drop: 3, shove: 2, glow: 1.2f).A(-6, 4, 0.1f).B(1, 2)),
            (Stage.Strike, 1, P(lean: 9, stretch: -0.1f, drop: 3, shove: 5, glow: 1.3f).A(-7, 16, 0.3f).B(0, 2)),
            (Stage.Rebound, 1, P(lean: 8, stretch: -0.08f, drop: 2, shove: 5).A(-5, 17, 0.3f).B(0, 2)),
            (Stage.Held, 2, P(lean: 8, stretch: -0.08f, drop: 2, shove: 5).A(-6, 16, 0.3f).B(0, 2)),
            (Stage.Return, 8, Rest));

        // Both fists driven up into the roof.
        static readonly (Pose[] Frames, Stage[] Stages) Uppercut = Expand(Heaved,
            (Stage.Swing, 3, P(18, 3, 0.7f, stretch: 0.04f, drop: 1, glow: 1.3f)),
            (Stage.Strike, 1, P(32, 1, 0.8f, lean: -3, stretch: 0.12f, lift: 2, glow: 1.5f)),
            (Stage.Rebound, 1, P(30, 1, 0.8f, lean: -2, stretch: 0.1f, lift: 1, glow: 1.3f)),
            (Stage.Held, 3, P(31, 1, 0.8f, lean: -2, stretch: 0.1f, lift: 1, glow: 1.2f)),
            (Stage.Return, 12, Rest));

        // ---- The entrance: asleep as a heap of stone, its eyes lit, its core lit, up on its feet, both fists hauled up
        // and the floor pounded. ----

        static readonly Pose Asleep = P(-3, 4, 0.45f, lean: 5, stretch: -0.24f, stepA: 2, stepB: -2, drop: 6, glow: 0, eyes: 0);

        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Asleep,
            // A shiver through the stone.
            (Stage.Slow, 2, P(-2, 4, 0.45f, lean: 4, stretch: -0.22f, stepA: 2, stepB: -2, drop: 5, glow: 0, eyes: 0)),
            (Stage.Slow, 2, Asleep),
            // The eyes, then the core.
            (Stage.Slow, 3, Asleep.Lit(0, 1)),
            (Stage.Slow, 4, Asleep.Lit(1.4f, 1)),
            // Pushing itself up off the floor, onto its feet.
            (Stage.Slow, 6, P(2, 2, 0.2f, lean: 2, stretch: -0.06f, stepA: 1, stepB: -1, drop: 2, glow: 1.2f)),
            (Stage.Slow, 5, Rest.Lit(1.1f)),
            // Both fists hauled overhead.
            (Stage.Slow, 6, P(15, -1, 0.5f, lean: -2, stretch: 0.04f, stepA: 1, stepB: -1, glow: 1.3f)),
            (Stage.Slow, 5, RaiseTop));

        static readonly (Pose[] Frames, Stage[] Stages) EnterPound = PoundFrom(RaiseTop);

        static readonly Pose[] EnterFrames = new[] { Asleep }.Concat(Entrance.Frames).Concat(EnterPound.Frames).ToArray();
        static readonly Stage[] EnterStages = new[] { Stage.Slow }.Concat(Entrance.Stages).Concat(EnterPound.Stages).ToArray();
        // The frames its eyes and core light, and where it stands on its feet (seen before, the entrance stops there).
        public const int EnterEyes = 7;
        public const int EnterCore = 11;
        public const int EnterStood = 22;
        // The frame its fists strike the floor.
        public static int EnterStrike => 1 + Entrance.Frames.Length + System.Array.IndexOf(EnterPound.Stages, Stage.Strike);

        // The entrance's frames: the whole of it, or seen before, only getting up.
        public static (int From, int To) EnterSpan(bool again) => (again ? EnterCore - 3 : 0, again ? EnterStood : EnterFrames.Length - 1);

        // The frame of the entrance so far through it (0..1), each frame for its own stage's time.
        public static int EnterFrameAt(float progress, bool again)
        {
            var (from, to) = EnterSpan(again);
            float total = 0;
            for (int f = from; f <= to; f++) total += StageSeconds(EnterStages[f]);
            float at = System.Math.Max(0, System.Math.Min(1, progress)) * total;
            for (int f = from; f <= to; f++)
            {
                at -= StageSeconds(EnterStages[f]);
                if (at < 0) return f;
            }
            return to;
        }

        // ---- The fall: staggered back by the last blow, the core flickering and going out, down on its knees, slumped
        // forward onto its fists, then the stone giving way into a heap. ----

        static Pose Broken(Pose p, float crumble)
        {
            p.Crumble = crumble;
            return p;
        }

        static readonly (Pose[] Frames, Stage[] Stages) Death = Expand(Rest,
            (Stage.Slow, 4, P(4, -3, -0.35f, lean: -6, stretch: 0.04f, stepA: -2, stepB: 2, glow: 1.9f)),
            (Stage.Slow, 1, P(4, -3, -0.35f, lean: -6, stretch: 0.04f, stepA: -2, stepB: 2, glow: 0.2f, eyes: 0.6f)),
            (Stage.Slow, 1, P(3, -3, -0.3f, lean: -5, stretch: 0.03f, stepA: -2, stepB: 2, glow: 1.5f)),
            (Stage.Slow, 1, P(3, -2, -0.3f, lean: -5, stretch: 0.03f, stepA: -2, stepB: 2, glow: 0.1f, eyes: 0.4f)),
            (Stage.Slow, 1, P(2, -2, -0.25f, lean: -4, stretch: 0.02f, stepA: -2, stepB: 2, glow: 1.1f, eyes: 0.8f)),
            (Stage.Slow, 6, P(-2, 3, 0.3f, lean: 6, stretch: -0.14f, stepA: 1, stepB: -1, drop: 4, glow: 0.3f, eyes: 0.5f)),
            (Stage.Slow, 6, P(-6, 8, 0.5f, lean: 10, stretch: -0.24f, stepA: 2, stepB: -2, drop: 4, glow: 0.05f, eyes: 0.15f)),
            (Stage.Slow, 10, Broken(P(-3, 4, 0.45f, lean: 5, stretch: -0.4f, stepA: 2, stepB: -2, drop: 4, glow: 0, eyes: 0), 1)));

        // The frames its core goes out (the flicker ends), it drops to its knees, and it starts to crumble.
        public const int DieOut = 8;
        public const int DieKnees = 13;
        public const int DieCrumble = 20;

        // ---- Frames. ----

        // The frames a roar takes to fling the arms open, before it shakes there (frames RoarRise.. loop).
        public const int RoarRise = 12;

        static (Pose[] Frames, Stage[] Stages)? BlowOf(WardenPose pose) => pose switch
        {
            WardenPose.Slam => Slam,
            WardenPose.Pound => Pound,
            WardenPose.Sweep => Sweep,
            WardenPose.Burst => Burst,
            WardenPose.Stomp => Stomp,
            WardenPose.Punch => Punch,
            WardenPose.Gouge => Gouge,
            WardenPose.Uppercut => Uppercut,
            _ => null,
        };

        static Stage[] StagesOf(WardenPose pose) => pose == WardenPose.Enter ? EnterStages : pose == WardenPose.Die ? Death.Stages : BlowOf(pose)?.Stages;

        // How many frames each pose has: a wind-up through its telegraph, a blow through its swing and recovery, a loop.
        public static int FramesOf(WardenPose pose) => pose switch
        {
            WardenPose.Idle => 4,
            WardenPose.Walk => 8,
            WardenPose.Raise => 16,
            WardenPose.Wind => 12,
            WardenPose.Hunch => 10,
            WardenPose.Lift => 10,
            WardenPose.Coil => 14,
            WardenPose.Scoop => 10,
            WardenPose.Heave => 10,
            WardenPose.Gather => 20,
            WardenPose.Brace => 8,
            WardenPose.Charge => 8,
            WardenPose.Roar => RoarRise + 4,
            WardenPose.Enter => EnterFrames.Length,
            WardenPose.Die => Death.Frames.Length,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(WardenPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        // The frames of a blow in which a fist is moving too fast to see (a streak behind it), and the frame it strikes.
        public static bool Swinging(WardenPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(WardenPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static WardenPose WindUpOf(WardenPose blow) => blow switch
        {
            WardenPose.Slam or WardenPose.Pound => WardenPose.Raise,
            WardenPose.Sweep => WardenPose.Wind,
            WardenPose.Burst => WardenPose.Hunch,
            WardenPose.Stomp => WardenPose.Lift,
            WardenPose.Punch => WardenPose.Coil,
            WardenPose.Gouge => WardenPose.Scoop,
            WardenPose.Uppercut => WardenPose.Heave,
            _ => WardenPose.Raise,
        };

        // How the body turns with the motion, a step of the eight views: away from the viewer (-1) as a fist is drawn
        // back, round toward the viewer (+1) as the sweep comes across.
        public static int TwistOf(WardenPose pose, int frame) => pose switch
        {
            WardenPose.Wind or WardenPose.Scoop => frame >= FramesOf(pose) / 3 ? -1 : 0,
            WardenPose.Sweep => frame == 0 ? -1 : frame <= StrikeFrame(pose) ? 1 : 0,
            _ => 0,
        };

        static Pose PoseOf(WardenPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case WardenPose.Idle:
                {
                    // Breathing: the shoulders and fists rise a little, the core pulses.
                    float b = 0.5f - 0.5f * c;
                    return P(b, stretch: 0.015f * b, glow: 0.9f + 0.15f * b);
                }
                case WardenPose.Walk:
                    // One heavy stride each way per loop: the foot coming forward lifted, the body sinking as each
                    // lands (frames 2 and 6), the fists swinging against the feet.
                    return P(lean: 1, stretch: -0.02f * (1 - System.Math.Abs(c)), stepA: 3 * s, stepB: -3 * s, drop: 1.2f * (1 - System.Math.Abs(c)),
                        liftA: 2.5f * System.Math.Max(0, c), liftB: 2.5f * System.Math.Max(0, -c)).A(0.6f * System.Math.Max(0, -s), -3 * s).B(0.6f * System.Math.Max(0, s), 3 * s);
                case WardenPose.Raise:
                    // Both fists hauled up from its sides: a dip as it heaves, then up tall, leaning back.
                    return Keys(t,
                        (0, Rest),
                        (0.35f, P(8, -2, 0.2f, stretch: -0.04f, drop: 1, glow: 1.1f)),
                        (0.7f, P(20, -3, 0.6f, lean: -2, stretch: 0.04f, stepA: 1, stepB: -1, glow: 1.2f)),
                        (1, RaiseTop));
                case WardenPose.Wind:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(lean: -2, stretch: 0.01f, stepA: -1, stepB: 1, glow: 1.05f).A(5, -7, -0.2f).B(1, 2, 0.1f)),
                        (1, WindBack));
                case WardenPose.Hunch:
                    return Keys(t,
                        (0, Rest),
                        (0.5f, P(4, 3, 0.6f, lean: 1, stretch: -0.06f, drop: 1, glow: 1.3f)),
                        (1, Hunched));
                case WardenPose.Lift:
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(2, 0, -0.1f, lean: -1, stretch: 0.01f, stepA: 1, liftA: 4, glow: 1.05f)),
                        (1, Lifted));
                case WardenPose.Coil:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(lean: -1, stretch: 0.02f, stepB: -1, glow: 1.05f).A(12, -3, 0.2f).B(2, 0, -0.1f)),
                        (1, Coiled));
                case WardenPose.Scoop:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(lean: 2, stretch: -0.04f, stepB: 1, drop: 1, glow: 1.05f).A(-1, -6, -0.1f).B(1, 1)),
                        (1, Scooped));
                case WardenPose.Heave:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(-1, 2, 0.3f, lean: 1, stretch: -0.07f, drop: 1, glow: 1.1f)),
                        (1, Heaved));
                case WardenPose.Gather:
                    // The biggest wind-up: arms spread wide, then both fists higher than ever, the core blazing.
                    return Keys(t,
                        (0, Rest),
                        (0.45f, Spread),
                        (1, GatherTop));
                case WardenPose.Brace:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(-1, 3, 0.1f, lean: 4, stretch: -0.05f, stepA: 2, stepB: -2, drop: 1, glow: 1.1f)),
                        (1, Braced));
                case WardenPose.Charge:
                    // Running head down: long strides, the fists ahead swinging.
                    return P(-1 + s, 7, 0.2f, lean: 12, stretch: -0.12f, stepA: 6 * s, stepB: -6 * s, drop: 1 - System.Math.Abs(c),
                        liftA: 3 * System.Math.Max(0, c), liftB: 3 * System.Math.Max(0, -c), glow: 1.2f).A(-1 + s, 7 + 2 * s, 0.2f).B(-1 - s, 7 - 2 * s, 0.2f);
                case WardenPose.Roar:
                    // The arms flung open, then held wide and shaking with the roar.
                    if (f < RoarRise)
                        return Keys(f / (float)RoarRise,
                            (0, Rest),
                            (0.55f, P(10, -1, -0.5f, lean: -2, stretch: 0.03f, stepA: 1, stepB: -1, glow: 1.4f)),
                            (1, RoarTop));
                    return P(20 + s, -2, -0.95f, lean: -5, stretch: 0.09f, stepA: 2, stepB: -2, glow: 2 + 0.2f * s);
                case WardenPose.Enter:
                    return EnterFrames[f];
                case WardenPose.Die:
                    return Death.Frames[f];
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        // Where an arm's fist goes for a pose: up by its raise; ahead to the right from the side (a little less from
        // the three-quarter views), ahead as a dip toward the viewer from the front and a rise from behind; drawn in
        // toward the middle of the body (or out from it).
        static (int X, int Y) ArmShift(BossView view, Box box, float raise, float ahead, float inward)
        {
            var f = box.Fist;
            float fore = view == BossView.Side ? 1 : view == BossView.FrontSide || view == BossView.BackSide ? 0.8f : 0;
            float dip = view == BossView.Front ? 0.35f : view == BossView.Back ? -0.3f : 0;
            float pull = view == BossView.Side ? 0 : view == BossView.Front || view == BossView.Back ? 1 : 0.6f;
            return (R((31.5f - f.X) * inward * pull + ahead * fore), R(-raise + ahead * dip));
        }

        // The picture with each part at its place, on a canvas with room round it; and where the two fists are.
        static PixelImage Compose(BossView view, Pose pose, out (float X, float Y) fistA, out (float X, float Y) fistB)
        {
            var image = ViewOf(view);
            var parts = PartsOf(view);
            var (armA, armB, legA, legB) = BoxesOf(view);
            int w = image.Width, h = image.Height;
            bool side = view != BossView.Front && view != BossView.Back;
            var (ax, ay) = ArmShift(view, armA, pose.RaiseA, pose.AheadA, pose.InA);
            // Seen from the side the far arm is the near one's double, behind the body.
            var farBox = armB.Empty ? armA : armB;
            var (bx, by) = ArmShift(view, farBox, pose.RaiseB, pose.AheadB, pose.InB);
            float stepScale = view == BossView.Side ? 1 : side ? 0.7f : 0;
            int la = R(pose.StepA * stepScale), lb = R(pose.StepB * stepScale);
            int lay = R(-pose.LiftA - (side ? 0 : System.Math.Max(0, pose.StepA) * 0.5f));
            int lby = R(-pose.LiftB - (side ? 0 : System.Math.Max(0, pose.StepB) * 0.5f));
            fistA = (Margin + armA.Fist.X + ax, Headroom + armA.Fist.Y + ay);
            fistB = armB.Empty ? (Margin + armA.Fist.X + bx - 4, Headroom + armA.Fist.Y + by - 1) : (Margin + armB.Fist.X + bx, Headroom + armB.Fist.Y + by);

            var canvas = new PixelImage(w + 2 * Margin, h + Headroom);
            var trunk = TrunkOf(view);
            var left = new bool[canvas.Width, canvas.Height];
            bool Far(int dx, int dy) => System.Math.Abs(dx) >= 5 || System.Math.Abs(dy) >= 5;
            // The far arm seen from the side, when it comes out from behind: drawn first, the body over it.
            if (armB.Empty && Far(bx, by)) Arm(Part.ArmA, armA, bx - 4, by - 1, 0.55f);
            bool Moved(Part part) => part == Part.ArmA ? ax != 0 || ay != 0 : part == Part.ArmB ? bx != 0 || by != 0 : part == Part.LegA ? la != 0 || lay != 0 : part == Part.LegB ? lb != 0 || lby != 0 : false;
            for (int y = 0; y < h; y++)
            {
                bool Body(int x) => image[x, y].A > 0 && parts[x, y] == Part.Body;
                for (int x = 0; x < w; x++)
                {
                    var c = image[x, y];
                    if (c.A == 0) continue;
                    if (parts[x, y] == Part.Body || !Moved(parts[x, y]))
                    {
                        canvas[x + Margin, y + Headroom] = c;
                        continue;
                    }
                    // Where a part has moved away from inside the body, the body behind it in shade.
                    left[x + Margin, y + Headroom] = true;
                    int before = -1, after = -1;
                    for (int k = x - 1; k >= 0 && before < 0; k--) if (Body(k)) before = k;
                    for (int k = x + 1; k < w && after < 0; k++) if (Body(k)) after = k;
                    if (before >= 0 && after >= 0 || trunk.Holds(x, y) && (before >= 0 || after >= 0))
                    {
                        var near = image[before < 0 ? after : after < 0 ? before : x - before <= after - x ? before : after, y];
                        canvas[x + Margin, y + Headroom] = Luminance(near) < 20 ? Shade : near.Mix(Shade, 0.6f);
                    }
                }
            }
            void Lay(Part part, int dx, int dy, float dim)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (parts[x, y] == part) canvas[x + Margin + dx, y + Headroom + dy] = dim < 1 ? image[x, y].Scale(dim) : image[x, y];
            }
            // An arm moved far is drawn anew: a stone upper arm from the shoulder (the top of its box) to the fist, and
            // the fist (the bottom of the box) there.
            void Arm(Part part, Box box, int dx, int dy, float dim)
            {
                if (box.Empty) return;
                if (!Far(dx, dy))
                {
                    Lay(part, dx, dy, dim);
                    return;
                }
                var fist = box.Fist;
                float sx = (box.X0 + box.X1) / 2f + Margin, sy = box.Y0 + 3 + Headroom;
                // Raised above the shoulder the fist is turned over, knuckles up, the arm coming into it from below.
                bool up = fist.Y + dy < box.Y0 - 2;
                float ex = fist.X + dx + Margin, ey = (up ? fist.Y + 2 : fist.Y - 4) + dy + Headroom;
                float length = (float)System.Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
                float ux = (ex - sx) / System.Math.Max(1, length), uy = (ey - sy) / System.Math.Max(1, length);
                for (float d = 0; d <= length; d += 0.5f)
                {
                    for (float o = -5.5f; o <= 5.5f; o += 0.5f)
                    {
                        int x = R(sx + ux * d - uy * o), y = R(sy + uy * d + ux * o);
                        if (!canvas.Inside(x, y)) continue;
                        float edge = System.Math.Abs(o);
                        // Lit on the side facing up (or, held upright, facing left), a seam across at the elbow, moss here
                        // and there on the lit side.
                        bool lit = System.Math.Abs(ux) > 0.3f ? ux * o < 0 : uy * o < 0;
                        var c = edge > 4.6f ? Ink : System.Math.Abs(d - length * 0.5f) < 1 ? StoneDark : lit ? ((int)d % 9 == 4 && edge > 1.5f && edge < 3.5f ? Moss : edge > 3.4f ? Stone : StoneLight) : edge < 1.5f ? Stone : StoneDark.Mix(Stone, 0.5f);
                        canvas[x, y] = dim < 1 ? c.Scale(dim) : c;
                    }
                }
                for (int y = System.Math.Max(box.Y0, box.Y1 - 11); y <= box.Y1; y++)
                    for (int x = box.X0; x <= box.X1; x++)
                        if (parts[x, y] == part && image[x, y].A > 0)
                        {
                            int to = up ? 2 * fist.Y - y : y;
                            canvas[x + Margin + dx, to + Headroom + dy] = dim < 1 ? image[x, y].Scale(dim) : image[x, y];
                        }
            }
            Lay(Part.LegB, lb, lby, 1);
            Lay(Part.LegA, la, lay, 1);
            if (!armB.Empty) Arm(Part.ArmB, armB, bx, by, 1);
            Arm(Part.ArmA, armA, ax, ay, 1);
            // A narrow gap left between a part and the body (a fist moved a little off the trunk), or the notch between
            // hip and leg the hanging fist hid at rest, closed in shade.
            for (int y = trunk.Y0; y <= trunk.Y1; y++)
                for (int x = trunk.X0; x <= trunk.X1; x++)
                    left[x + Margin, y + Headroom] = true;
            canvas.CloseGaps(left, Shade, 6);
            return canvas;
        }

        // The amber of its core and eyes: the eyes are the amber above row 23 of the picture.
        static bool IsAmber(Rgba c) => c.R > 200 && c.G > 100 && c.B < 70;

        static Rgba Glowing(Rgba c, float glow, int phase)
        {
            if (phase >= 3) c = c.Mix(Ember, 0.45f);
            if (glow < 1) return Dormant.Mix(c, System.Math.Max(0, glow));
            return c.Mix(Blaze, System.Math.Min(1, (glow - 1) * 0.8f));
        }

        // One frame. A wind-up runs through its telegraph, a blow through its swing, strike, hold and recovery, a walk,
        // a charge and the idle loop. In the last phase its core burns red-hot. Faces right; WorldView flips it for the
        // left.
        public static PixelImage Draw(BossView view, WardenPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the fists and the body are in a frame (picture pixels): for the preview's check that nothing jumps from
        // one frame to the next.
        public struct Joints
        {
            public float FistAX, FistAY, FistBX, FistBY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, WardenPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static PixelImage Render(BossView view, WardenPose pose, int frame, int phase, out Joints joints)
        {
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            var p = PoseOf(pose, frame);
            bool side = view != BossView.Front && view != BossView.Back;
            var body = Compose(view, p, out var fistA, out var fistB);
            float lean = p.Lean, stretch = p.Stretch, shove = p.Shove, drop = p.Drop;
            if (!side)
            {
                // Seen from the front or back a lean shows as a dip or a rise, and a shove as a step down or up.
                stretch -= lean * 0.012f;
                drop += view == BossView.Front ? shove / 3 : -shove / 3;
                lean = 0;
                shove = 0;
            }
            int w = body.Width, h = body.Height;
            int tall = System.Math.Max(4, R(h * (1 + stretch)));
            int wide = System.Math.Max(4, R(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2 + R(shove);
            int ground = Height - 1 - Floor - (int)p.Lift + R(drop);
            tall -= R(System.Math.Max(0, drop));
            (float X, float Y) Place(float x, float y)
            {
                float row = ground - (tall - 1) + y * tall / h;
                float up = (ground - row) / (tall - 1);
                return (left + x * wide / w + lean * up, row);
            }
            var a = Place(fistA.X, fistA.Y);
            var b = Place(fistB.X, fistB.Y);
            joints.FistAX = a.X;
            joints.FistAY = a.Y;
            joints.FistBX = b.X;
            joints.FistBY = b.Y;
            for (int y = 0; y < tall; y++)
            {
                int row = ground - (tall - 1 - y);
                if (row < 0 || row >= Height) continue;
                int sy = y * h / tall;
                float up = (tall - 1 - y) / (float)(tall - 1);
                int shift = R(lean * up);
                for (int x = 0; x < wide; x++)
                {
                    var c = body[x * w / wide, sy];
                    if (c.A == 0) continue;
                    if (IsAmber(c)) c = Glowing(c, sy - Headroom < 23 ? p.Eyes : p.Glow, phase);
                    int px = left + x + shift;
                    if (p.Crumble > 0)
                    {
                        // Breaking up: some stones come away and tumble down toward the floor (always the same ones, so
                        // the heap grows from frame to frame), the rest dull to rubble.
                        float chance = Hash(x, sy);
                        c = c.Mix(Dormant, 0.35f * p.Crumble);
                        if (chance < 0.4f * p.Crumble)
                        {
                            int to = System.Math.Min(Height - 1 - Floor + 2, row + 2 + (int)(chance / 0.4f * 24 * p.Crumble));
                            int aside = (int)((Hash(sy, x) - 0.5f) * 10 * p.Crumble);
                            if (image[px + aside, to].A == 0) image[px + aside, to] = c.Scale(0.8f);
                            continue;
                        }
                    }
                    image[px, row] = c;
                    joints.Top = System.Math.Min(joints.Top, row);
                    joints.Bottom = System.Math.Max(joints.Bottom, row);
                    joints.Left = System.Math.Min(joints.Left, px);
                    joints.Right = System.Math.Max(joints.Right, px);
                }
            }
            // A fist too fast to see: a pale streak from where it was a frame before, behind the body.
            if (Swinging(pose, frame) && frame > 0)
            {
                Compose(view, PoseOf(pose, frame - 1), out var wasA, out var wasB);
                Streak(image, Place(wasA.X, wasA.Y), a);
                Streak(image, Place(wasB.X, wasB.Y), b);
            }
            return image;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65536f;
        }

        static void Streak(PixelImage image, (float X, float Y) from, (float X, float Y) to)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (length < 5) return;
            float ux = dx / length, uy = dy / length;
            for (float d = 0; d <= length; d += 0.5f)
            {
                float u = d / length;
                float width = 1 + 3.5f * u;
                for (float o = -width; o <= width; o += 0.5f)
                {
                    int x = R(from.X + ux * d - uy * o), y = R(from.Y + uy * d + ux * o);
                    if (!image.Inside(x, y) || image[x, y].A > 0) continue;
                    image[x, y] = System.Math.Abs(o) < width * 0.4f ? new Rgba(236, 230, 212, (int)(120 + 110 * u)) : new Rgba(170, 160, 140, (int)(60 + 80 * u));
                }
            }
        }
    }
}
