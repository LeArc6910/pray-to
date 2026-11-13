using System.Collections.Generic;

namespace Game.Client
{
    // What the black knight is doing, which picks its frame (WorldView.Knight.cs). Every blow is slow and plain to
    // read and lands like a falling wall: the great hammer hauled up overhead and brought down (Raise, then Smash), drawn
    // far back low for a sweep across the front (Wind, Sweep), the shoulder lowered for a run (Brace, Charge), a deep
    // crouch and a jump (Crouch, Leap), the hammer lifted straight up and pounded into the floor at its feet (Raise, then
    // Pound), and a roar with the hammer held high. Walking it drags the hammer's head behind it.
    enum KnightPose
    {
        Idle,
        Walk,
        Raise,
        Smash,
        Wind,
        Sweep,
        Brace,
        Charge,
        Crouch,
        Leap,
        Pound,
        Roar,
    }

    // The black knight of the hero's hut (2026-11-12): a hulking knight in dark plate with a great helm and a torn blue
    // tabard. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md, $0.28);
    // the war hammer is drawn here in code between its gauntlets, and every pose is made in code: the arms and legs are
    // a box each per view, moved apart from the body (the gap they leave filled with shade, the arm joined back by its
    // forearm), and the whole body leans, rises, squashes on the blow and is carried into it. Faces right.
    static class KnightArt
    {
        public const int Width = 132;
        public const int Height = 116;
        // Rows under its feet for the hammer's head on the floor in front of it, seen from the front; WorldView stands
        // the picture on this row (Pivot).
        public const int Floor = 12;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 60;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(0, 0, 0),
            ['b'] = new Rgba(1, 0, 5),
            ['c'] = new Rgba(2, 1, 8),
            ['d'] = new Rgba(8, 7, 14),
            ['e'] = new Rgba(15, 12, 22),
            ['f'] = new Rgba(22, 21, 30),
            ['g'] = new Rgba(29, 25, 36),
            ['h'] = new Rgba(20, 33, 55),
            ['i'] = new Rgba(33, 30, 40),
            ['j'] = new Rgba(37, 35, 44),
            ['k'] = new Rgba(22, 43, 70),
            ['l'] = new Rgba(44, 45, 54),
            ['m'] = new Rgba(49, 53, 60),
            ['n'] = new Rgba(43, 63, 88),
            ['o'] = new Rgba(58, 63, 70),
            ['p'] = new Rgba(44, 72, 102),
            ['q'] = new Rgba(66, 73, 78),
            ['r'] = new Rgba(76, 82, 88),
            ['s'] = new Rgba(91, 95, 100),
            ['t'] = new Rgba(98, 109, 112),
            ['u'] = new Rgba(112, 121, 125),
            ['v'] = new Rgba(123, 130, 132),
            ['w'] = new Rgba(154, 162, 164),
            ['x'] = new Rgba(172, 179, 179),
            ['y'] = new Rgba(197, 196, 198),
        };

        static readonly string[] FrontRows =
        {
            "................................................................",
            "...............................accaaa...........................",
            ".............................ccqssurqca.........................",
            "............................ejtuuuuuutoaacc.....................",
            "............................csuuuuuuuuuofotcc...................",
            "............................cuvuuuuuuuuwcjmtmaaa................",
            ".................bcab....bbccllyuuuuuuxjcjjjvdllb...............",
            "................cqruwbaaahnoclljluxxxyijcjjijwerqc..............",
            "...............aooqqsxvalmnccoommjllltjjccijjvessoc.............",
            "...............ajlqqqrwtdocciooloooomvlmccbijqvlusoa............",
            ".............balgigooqqydoccjjoooooomullacbijjsdutoa............",
            "............aoqssssijrrrwdcajjjodqttowfaacccjjscqtmla...........",
            "...........aoquuuuuujiqswraajjloccccccamacccjlrbqqolb...........",
            "..........amotuuwyyvujorswaabilqrccacamqalccclqbiqqloc..........",
            "..........amotuxyywvvsgoowabcjmomrvwbaujalccclqajoqfob..........",
            ".........bmmqtwyvywvurgoowjaccjooiowbaujcmcccjqajjmgoc..........",
            ".........bmoqttyuuuurrqeqqoaqccloomtaaogcqcccjjbjjjioa..........",
            ".........bmoquttuuurrrofiqoblsccgomtccqasqccceedjjjrja..........",
            "........clioqrttuurrrrlffqoclquabamocccsrgccbsttdglca...........",
            "........cqimmqorruuqolllflqcllluwabcccvtjibblomrjceqa...........",
            "........cqtgoooortoqqlllflqcdjmomtwwwwqombjajmrljdoaa...........",
            ".........ctsgoooooolllledcjcgabmmoqqrwqqcmjajmmoccbga...........",
            "..........bwuigooojjljffxxfcggjcdjrrswqcjojbbmmcciifca..........",
            ".........aobxtsgjjjjlfdwqqqcimojlacccccmormjcccbiiigcob.........",
            ".........autccvuumgeeebsourcgmouqrrssiiorsqjjccggggcmoa.........",
            "........ajbstleeuurrrmcsooociqrssvvvvtgsuqqjjcggggcomqa.........",
            ".......balmccxsmaaaabbcfoofblqsvvvvxxygqsoljccffacdqoqc.........",
            "......bmmbmmmcalljjbcccccaaloootrswubbaaamjddcccglococcd........",
            "......astsalljjaccccccccaijjoooorrcarwgljccecdjilbcaccoc........",
            ".....bsvvvsaajjjijccccggiiejjllcdcmuwxfoqmgcccccccgabtoc........",
            ".....arvvvtoaaacedcccgbacccaccdrvvuttyeqmjgbcbigccaotjfb........",
            ".....agmrqwdoqgbddcccciieeomoorrrruttyfqjiaaa.bfoooqffia........",
            ".....almmmoaccijlbbbiccigggmmjqrrrrrsqeljdala.amfgfcjroa........",
            "......cmjjcggfbccbjbcgcbgigfjjjqmmmqrdllccjla..ajllgdrma........",
            "......cccccbbcigjaajcbggccffjjjjjjjllccceljcja.abjljerma........",
            "......eqswwyywcccdagfbajjjccccaaaaabceelmccomd..ajllelmaa.......",
            "......bcfgfeglrrmjaggggccillmmllmmrqrqmccccdrja..agbddeqb.......",
            "......ajlrttsmidfbiijmojjbcffjmlmmljfachkmcc.lja.aajldjqa.......",
            ".......blrwxtsoflcijjoossjbccbbbbbbbbhhkkpcccogc..bllcjqb.......",
            ".......amqsxtsofccijoqrtuobccnkhkkkhhhhnkpcjcogc.cclcfabb.......",
            ".......bmorwvsmicciiqrrttbcccpkkkkkkphhpkncigcca.cjcbljjb.......",
            "........coquusoaccjmqrrtuccccpphkpppphkpkncjgcb..cbdljlb........",
            "........bfomrjcbqcbmqqttbcfccpphnkkpphkpnegggcba...aaaa.........",
            ".........bcccdmaqqabettaejfbccphkppppkpppbfgccob................",
            ".........almstqbbmeabbbjjjgccbphkppppkpppacboqoc................",
            ".........amrouqabcmaiiigjjgcccpnkppppkpppaagjolb................",
            ".........aaabccmooabababjigbcbpnkppppkpppabglljc................",
            "..........allllmoebmrtsbcgba.bppkkppkpkppababaa.................",
            "...........cccaaacjorwsriba..apnkkppkpcppbggjja.................",
            "................abimrssmjba..bnkkkkppkcppdimjla.................",
            "...............aggalmmmmdc...bnnkfkppnccdcimmma.................",
            "...............ammgccbccec...bnhhbcpnc.cdcimelja................",
            "...............amqlgggiib....bnbb.ana..cieeclqqoa...............",
            "...............alqtmllia.....cab...a...cefailqmmrb..............",
            "...............alqrmlla.................aaagiimvwua.............",
            "...............acbcblab...................bbbcglmma.............",
            "...............cooodbb........................abba..............",
            "..............bmruumlb..........................................",
            "..............agggflla..........................................",
            "..............alqssela..........................................",
            "..............aqtxsqia..........................................",
            "..............ajqoooc...........................................",
            "...............aabbc............................................",
            "................................................................",
        };

        static readonly string[] FrontSideRows =
        {
            "................................................................",
            ".................................bbaaa..........................",
            "...............................cdrrsusba........................",
            "...............................cruuuuuuubb......................",
            "...............................ctwuuuuuuucb.....................",
            "............................aabcjryyuuuuuujbb...................",
            "..........................bbuscmoijjyuuuuutbb...................",
            ".........................aumjjcooljjjjtyyyxbwa..................",
            ".......................baaaiibbooojjjjjlllrawqa.................",
            ".....................abstvwaaablomjjjooooorbiva.................",
            "....................boqqqqsvxxbbjmjjlooooosaiva.................",
            "...................bdloqqqqqrrxobjjiodruuqebgja.................",
            "..................blllfffgqqrrrxtcjjobbbcclbciqa................",
            "..................aidjrsttggqqrsycjjqtaabbrbciqa................",
            "..................bclquvvvvugjqqsxcjmovwwbqbcgoa................",
            "..................bmotuvxywvujoqqxceooooobqbffbra...............",
            ".................bloquuwyyyvvtgoqqwcmoooocccjaora...............",
            ".................alosuuxyywuutriqqwbblmmmccqbiola...............",
            "................alloquuuxyuusrrilqsawbbbabulbija................",
            "................bjloqruuvuuurrrjgmsamwwwwwqebba.................",
            "...............bbjloootuvuurrrolelramoqrrwrfcb..................",
            "...............bogllooorrurrrrllelrbamoqrwqbma..................",
            "...............aqrilooooqsqqmlleccjbjaabbbcjoa..................",
            "................butgjoooooomllfgxxdcqsrsstsdoa..................",
            ".................bvwvgimooojjjcvvqmbrstvvvwdobb.................",
            ".................bccywugiiggfebvotmbruvvvwyejeb.................",
            ".................blobcyxxussqjblolblrrsswtbcbcb.................",
            ".................abcvvbbabbabbbbedcmrrssbbwfjbb.................",
            ".................blcccwvoliibbfdcjlmoojarvxfjbb.................",
            "................aaajooaaqqmgbfeefgiigbcvuuygjbb.................",
            "...............bmoqbbomlabbbbbbbbbbccrssuuxibma.................",
            "...............aotvwtagjjjfbfgggiimqrrrrssqdbcaa................",
            "...............aqvwyywoaabbbaafgiijjmoqoordcibma................",
            "...............amuwwtwbrmlcfffbbaiijjlllmlccbbjma...............",
            "...............alorrqtabbbbeggiifbaaabbbaajcbbcla...............",
            "...............afjmmmbeggfbbbfijjjjllllsqgbcibcba...............",
            "................aijicggbbabbiaaijjjlllllfchbbcja................",
            ".................bbccbdyuqmaijjbbbbbaabbbhkbjja.................",
            "................ammswxxeecbjjqqobnkkkkkkkhkbba..................",
            "................ababcdftrrbjjqqqcnnkkkknphnb....................",
            ".................agjltwwurajlqsccknhnnpppknkb...................",
            "..................ajlrvyurjboqtbbcphnnppphnnb...................",
            "..................ajlotwwrmboriebcphnnpppkpnb...................",
            "...................allrtwrbbasbcbbpknkpppkppb...................",
            "...................ajlmqribolaffbbpknknppkppa...................",
            "....................abbbbfmbqbifbbpknkkppkkpb...................",
            ".....................aloswsblbgdbbpknkkppphpb...................",
            ".....................alrrswaabdb.bppnkkpppcnc...................",
            ".....................aaaabbmobba.bppkkkkppccb...................",
            ".....................abllllldjab.annkkchpnbb....................",
            ".....................abaaaaabla..anffccbnbb.....................",
            "....................alcajllmmbb..bbecdjlcora....................",
            "....................ajliaaaaaa...bffdeggdjlla...................",
            "....................ajlqjjgga....aaaaaaaaaaa....................",
            "....................ajlrsljja...................................",
            "....................ajlrslia....................................",
            "....................ajjqbbab....................................",
            "....................abiblqoa....................................",
            "....................afclttomb...................................",
            "....................afcmofooa...................................",
            ".....................aaierwuqb..................................",
            ".......................aglmooa..................................",
            "........................aabba...................................",
            "................................................................",
        };

        static readonly string[] SideRows =
        {
            "................................................................",
            ".................................bbaaa..........................",
            ".........................aaaab..crruuubba.......................",
            ".......................bcwrjjjbbsuuuuuuuqb......................",
            "......................cvjjjjjjgbgxuuuuuuusb.....................",
            ".....................bwjjjjaaabcljwyyuuuuuua....................",
            "....................bvjjjbbwusbmoliijyyuuuma....................",
            "...................btjjjavlijibloojjjjjjjlla....................",
            "...................bsjjbwjjjbbbloomjjjrloola....................",
            "...................bljjbvjbaabaabbbjjmrlooma....................",
            "...................bljbbveabqvwxyyybbmbdtrb.....................",
            "...................blbnbfctsrrqqqrsuwbcbab......................",
            "..................ablbndbqqqrrqqqqqqrwaqvvb.....................",
            "..................arbkkbqoqqggggglqqqqwblmb.....................",
            "..................aobkkboogggrtttmgooqqwbjb.....................",
            "..................acnncooggmruuvvuuqfqqqwbb.....................",
            "...................bnnclofgquuuwywuurfqqwbb.....................",
            "...................bkkclleoruuuyyyuuuofqsara....................",
            "...................bkcclfjoruuuxyxuuuqforaoa....................",
            "...................bcccjejqquuuwywuuuqigrajb....................",
            "..................abcjdjejqqruuvvvuurqlfqabb....................",
            "..................abcjgccjlqqruuuuuurqlfqblb....................",
            ".................albcjbuuclooqqsssrrqllbbbfa....................",
            ".................atccccstclloooooqqqllcwwsbrb...................",
            ".................amcccclmbgiooooooolllcsssbra...................",
            ".................ajbbcblfcsqgjmooollggcmmfcra...................",
            "..................ambbbbbcouvvtiiiiirrcjibobb...................",
            "..................abbobbibbccsyyyywtrccbbmeoa...................",
            "...................bbcobiebjqebbbcccccefjjcqa...................",
            "...................abbgqbebbbcvxwsolgcedbcqqa...................",
            "....................aaggrbbgimbbbcccbbcdmoqa....................",
            "....................aafgigbbaaaoomjfbgjjmmla....................",
            ".....................aagigbmsuwabajfbggijja.....................",
            ".....................alaiabovyywwjabbceefb......................",
            ".....................arlbamovyyuwdilbbcbba......................",
            "....................arrmoajorurosbbbbdjjja......................",
            "....................asisrrajjmoobbjfbccbbb......................",
            "...................assissqbbjjjibjbbqibhhb......................",
            "...................arrlssibbcccbbcytgbchhb......................",
            "...................argsssgbbljjtwyffmbcckb......................",
            "....................aosslabrbmmccdutqabckbb.....................",
            "....................aqrmlaasjagjosxwsmbckka.....................",
            ".....................abqaabslajlosuyuqbbkka.....................",
            ".......................aaaarqobjmosvwrbbbkb.....................",
            "......................arlaabaabjjorssfbqoab.....................",
            "......................arjiibbbaabbbbcdgboma.....................",
            "......................aqjiibggiibblosvtabaa.....................",
            "......................aoiiibbabbfamrrrwaiaa.....................",
            "......................aoiiibga.boaaaabcjoab.....................",
            "......................ajiiigbcrcmqalllllcb......................",
            "......................abbbibbcbbjlaaabbbb.......................",
            "......................aljfbbqbbafjlja..a........................",
            "......................afgfejmsmoaaaa............................",
            "......................affffclqgrllia............................",
            ".......................bbbbbimgrtmia............................",
            "...........................ailgrtmc.............................",
            "...........................aijgrscb.............................",
            "...........................bbjgrbilb............................",
            "...........................aibbbowsob...........................",
            "...........................bfjcjqrxgob..........................",
            "...........................bffcijqevtrb.........................",
            "............................aabbffemmob.........................",
            "................................aaaaab..........................",
            "................................................................",
        };

        static readonly string[] BackSideRows =
        {
            "................................................................",
            ".............................bcbbbbb............................",
            "............................bquuvvurcc..........................",
            ".....................aab...btuuvvvuvvsb.........................",
            "...................bbujja..bjlsyyyyojjb.........................",
            ".................abrujjjiavcjmmlmmlllib.........................",
            "...............abmbwjjjiaabcjoooooqoljb.........................",
            "..............aorawljjbaurlcjoqooqqojja.........................",
            ".............bmsuaxjjcbsjiccjjooqoljjja..aaba...................",
            ".............aruvbvjjbbwgbccjjooqqjjjjbabxvrmb..................",
            "............bltuubwlbbbvcabbjjmommjjjjaiytqola..................",
            "............alstoawgbnblvvaaaaabbbaabbwwsrqojbbc................",
            "............blqqoasbnpbjjmwwwxxyxwwubwvqqqjjlqola...............",
            "............alqqmasbnphcdjjjjjllljifexqqqgouuuurma..............",
            "...........boimljarbnkppccfjjjjjiccbwqqqgsuwwwuuqjb.............",
            "...........bqimjjarcnkpppnecccccgnnbuqqgruvxyywvtmb.............",
            "...........briiibcjckkpppppnnpppppcoqqgorvvwwyywuojb............",
            "...........asiibsweckckppppkknnknpcsoogrruvvvwywuqjb............",
            "............asicstrlbbkkppphppppppcroilrrrsvuuuuuqjb............",
            "............acdclsllbjbkkppkpppnhpbrlilorrrruuuusqjb............",
            "............blbbglllblcbcppkpppccnbqjillmrrrrrssomjcb...........",
            "...........bbbmgbbbcfjlibkppppcjcccfcbjlloorrrsomljob...........",
            "..........adgebmlfeciijobkkppncmmccdwwdllmlmomommliqa...........",
            ".........amebljcccccijjobbhppcioobcqrurdlmmlmoomgiuqa...........",
            ".........aqwafjiiigccjjoomcnnclooibllrjclmmmmjgfswua............",
            ".........assobciiicccjjjmobnbcmojigallcoggggggtuyca.............",
            ".........arrsbiabbccccjimobbimooliigbbbfruuwwxwcbmja............",
            ".........ajmmcbiggcccciimmmclmoolgiigigccbbbbbblwda.............",
            "..........ajgqdbbbbbcciijlmmjlmmliiigigccjlostxwbgga............",
            "..........aaidammlbbbjbbccjljljjjgiiiciiccbbbbbfmlca............",
            "..........albbaaacb.biamccbbbejjjiiiicgcbbijjjlmlaqmb...........",
            "..........aamooolbb.biajjjrrrbbbbbaaaabcebijjiiabxtqa...........",
            "...........aaaaabb.bcbaijjjiissssaagggfffccigiasvwwvlb..........",
            "...........ajjiija.bjjbcjjjiiiiiijagiggfbcccfbtruwwscb..........",
            "...........aabbbccajmoqbbagiiiiiiiagifbbbbobbcmostwlbb..........",
            "...........albbbagaloqmallbbbdiiiibbbbbdicallfjmlllbjb..........",
            "...........albbblbjoqtbamsrqjaaabbabblqmgigbbdiiijccwa..........",
            "............abbjbbmqqtbamttwltsllllbbvqqiiibbbjbbbcxca..........",
            "............afljbbrquaaarttwlwttssacbwqqjjifalgmouwcsa..........",
            "..............aaaabttbearttwjwtttsaeawqqqjiiiabbabbuvqa.........",
            "..................baacfarttwjwtttlaffbwsqmjiiaafllrrvta.........",
            ".................bbbfffaqttwjwtttjaffawrrolqia.ajlmqtta.........",
            ".................aboabdaqstwjwtttjafga.rrqqga..aajlmlafb........",
            "..................baijcaabcmtrttijafia....aba..aababatoa........",
            ".................brsbbbggbbbbdgmteaeigaaaafbba.abbcctwa.........",
            ".................aqwrobbab....aaa..aiiiiiiccfacblbbbbaa.........",
            ".................arsroiiia.........agiijjccqojbaaimleb..........",
            ".................arqmgiiia.........abbbaabgqoja..aaa............",
            ".................aqllgiiia.........acilli.dqmja.................",
            ".................arllgiib...........aaaabrcjja..................",
            ".................aoljiibc..........alrxrabbbda..................",
            ".................abaaaibb..........alsyrvsiga...................",
            ".................agmlgccb..........ajrsrwomia...................",
            ".................agffggcb..........ajllqqomc....................",
            ".................agffgga............bjlogoma....................",
            "..................aaaaa.............bilmimfb....................",
            "....................................bdlljmbmba..................",
            "....................................bebcjcq.ria.................",
            "....................................agmlccqttima................",
            "....................................aggggbllela.................",
            "....................................afgffaaaaa..................",
            ".....................................aaaa.......................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] BackRows =
        {
            "................................................................",
            "...........................cccccb...............................",
            ".........................ccquuuuqdc.............................",
            "........................cruuuuuuuurb............................",
            "........................cijoyyyyqlib............................",
            "........................cjlmlrllllia............................",
            "........................cimqrsooolib.....bbb....................",
            "...................bb...cijqrsooolibb...bgj.b...................",
            "..................bwjb..cijjqoooijictaaaiii.b...................",
            "..................bwlbabbjjjqoooijjcdqsbbci.ccccb...............",
            "..............bcbclrxbaqbjjjjjjjjjjccbtbbbiygjmmmb..............",
            ".............boqqqirxbbvbbjjjjjjjjjbbbvbbbcygjuuuqb.............",
            "............bquvvtiqycbdtvwbbbbabbbwvtfbcccwgjwyxvrb............",
            "...........brtwyxuiowchcjlovwwxwwwvjjgfcpncwfjvwyxvqb...........",
            "..........bmsxywuuioowcpccjjjjjjjjjjccnppkcwfjuuvwwsa...........",
            "..........bqwywvuujiowcppkccfjjjjjccpppppkcwfqrtuuutmb..........",
            ".........bjstuuuurrgmtbpppppfcccccpppkpppkcseqrrsuutma..........",
            ".........blstuuurrqgjsbkkpppppkkkpppkppppkcremqrrrrola..........",
            ".........blorrrrroolfrckgppppppkpppppkppkcbrejlqqrmlla..........",
            "........bclmmrrqqollfrcbbkhpppphkpppkchpcgbrejlllllmgqb.........",
            "........cqfmmmmlllllemcjjbbpppppkppnbbencccecfllllmfosa.........",
            "........brugmmmlllgdxoeblmbneppppppcjmccibj..fcllffsua..........",
            ".........btuggimllcwqurblmbbckppppncoocbibj.sqbefltyb...........",
            "..........bvxvsiiecqtqmbilmobkkpppcooooggblqljbltwccma..........",
            "..........bccbbuuocmmmjbglmobkkpkncooooggbjlljccccmssa..........",
            "..........aoqrrcccccijbgggjobcbhkncoooigggbbccijmswvba..........",
            "..........bbwxsrjjjbccggiijmombhnbcmmmiiicbbccclocbbjaa.........",
            "...........abcxvroccccigiigmooibcimmmgiiibb.bggccgjlbuob........",
            "..........aammccccidccifiigloojbemmmgiiiibb.bgggilljbsub........",
            ".........amqallljiieccidiiglmmjjiiiiiiigbbgabbggiiecwrvva.......",
            ".........artxalljiicccccgiillljjiiiiibbbeggagbccccccjrrma.......",
            "........couwwtabjicccggbbbbabbbbbbbbdjbgggfafbbgiiiblmma........",
            "........couwuqrbeeccccggaiqqcsrssrrrogbgggba.ambcbciljja........",
            "........cctuqqgtbbcccbfgagiiiiggggigggcggbb..abjmmdligaa........",
            "........bcilmmftbjmcggbbajegggggiiiigecbbeja..aabbbabaqa........",
            "........bvbjjjgabbbbgiiiagdggegfiigiibbeqjia..abboqmoaa.........",
            "........bcwcbcdbbbcgijmqobbbbbbbbbbbbdbbrojja..bcbbbcqa.........",
            "........brbvummljciiijqquablllqrjslllmabtojjb..bgiiciqa.........",
            ".......bmttbbbbbbbiijqqqwbblssqwjwtrssaatrojjaajbbbccba.........",
            ".......bruqqjijaajjjmqqqwbbltttwjwtrrsadcrqojabcvbbbdqa.........",
            ".......aaqqjjbb.ammmqqvvbbbltttwjwtrrsadcbqlraalabbbccb.........",
            ".......aqbcjjba.abmrqqvadcbrtttwjwtrssagcavoab.albfcob..........",
            ".......aowjbbaa.vbbbvwwbffartttwlwtrssbfiabbbb..aellb...........",
            ".......aaabbbbb.bbffbbbffeaslutltjtrjsbfiiibbb...aab............",
            "........aqlbbmbacbfgggggiearrqolujmmmabfiibbbob.................",
            ".........bmllcbaqjbbdjjiicbaaaabbbbabbeebbblbqa.................",
            "..........aaaa.arbwcbbbbccb.........bbbbgmdtcma.................",
            "...............alaqbmolfbb...........aigabbbbb..................",
            "................abbbbbabia...........aaagwvlaa..................",
            ".................abswvfbb............ajmsyvsra..................",
            ".................arsxurja............bjmsutsra..................",
            ".................arssrqja............bjjlmlrra..................",
            ".................arlllljb.............blllmmra..................",
            ".................armljjb...............bimmmrb..................",
            "...............bbarmmiia...............aggllocbb................",
            "..............crubrmjiib...............abbbbodsjb...............",
            ".............bgrqbbbbbbb...............bejmlbeojb...............",
            ".............bgfobfllljb...............bigiigcjda...............",
            "..............abgcgffffb...............bgggggcbb................",
            "................abfffffb...............aggggdb..................",
            "..................aabbb.................aaab....................",
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

        // Room round the picture for the hammer raised overhead or swung out ahead.
        const int Margin = 34;
        const int Headroom = 40;

        // ---- Parts. The arms (forearm and gauntlet, under the pauldrons) and the legs of each view are a box each (left,
        // top, right, bottom, inclusive), the left one on the screen first. ----

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

            public bool Holds(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

            // Where the gauntlet closes, near the bottom of the arm.
            public (int X, int Y) Fist => ((X0 + X1) / 2, Y1 - 4);
        }

        static (Box ArmA, Box ArmB, Box LegA, Box LegB) BoxesOf(BossView view) => view switch
        {
            BossView.Front => (new Box(5, 24, 15, 49), new Box(44, 20, 57, 44), new Box(14, 47, 27, 62), new Box(36, 47, 52, 57)),
            BossView.FrontSide => (new Box(15, 27, 27, 46), new Box(40, 14, 49, 38), new Box(20, 46, 31, 62), new Box(32, 46, 44, 56)),
            BossView.Side => (new Box(19, 28, 28, 44), new Box(36, 22, 45, 40), new Box(22, 44, 32, 59), new Box(30, 46, 41, 62)),
            BossView.BackSide => (new Box(9, 22, 18, 46), new Box(44, 30, 55, 46), new Box(17, 44, 29, 61), new Box(34, 46, 48, 62)),
            _ => (new Box(7, 26, 18, 46), new Box(44, 24, 56, 44), new Box(15, 46, 29, 61), new Box(35, 46, 49, 61)),
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

        static readonly Rgba Shade = new Rgba(14, 12, 20);
        static readonly Rgba Plate = new Rgba(76, 82, 88);
        static readonly Rgba PlateLight = new Rgba(123, 130, 132);
        static readonly Rgba PlateDark = new Rgba(33, 30, 40);
        static readonly Rgba Ink = new Rgba(4, 3, 8);

        // ---- A pose: how far the arms are raised (pixels up), reach ahead and close in on each other to grip the haft
        // (0 apart .. 1 together); the steps and lifts of the feet; the body's lean (top ahead), stretch (taller and
        // thinner, or squashed), lift off the ground, how far it is carried ahead (shove) and dropped (drop); the hammer's
        // angle (degrees on the screen from the side: 0 ahead, 90 up, 180 behind) and a smear of its swing (from, to). ----

        struct Pose
        {
            public float Raise, Reach, Together, StepA, LiftA, StepB, LiftB, Lean, Stretch, Lift, Shove, Drop, Angle;
            public float? SmearFrom, SmearTo;
            // A swing across the front, seen from the side: the hammer foreshortened toward the viewer.
            public float Foreshorten;
        }

        // ---- The frames (2026-11-13, the person: "more frames, so the motion is natural; check every frame's body, the
        // hammer's place and way, and that it follows on from the frames either side"). Wind-ups and loops are a few key
        // poses eased into each other; each blow is laid out frame by frame. Every motion starts from and comes back to
        // the same rest (the hammer's head on the floor behind it, as it drags it walking), and the hammer's angle runs
        // on through each motion without jumping (from behind, up over the shoulder, overhead, down ahead, and dragged
        // back along the floor: -150, -205, -384, -510 are the same turns of one way round). ----

        // How many frames each pose has: a wind-up through its telegraph, a blow through its swing and recovery, a loop.
        public static int FramesOf(KnightPose pose) => pose switch
        {
            KnightPose.Idle => 4,
            KnightPose.Walk => 8,
            KnightPose.Raise => 12,
            KnightPose.Wind => 12,
            KnightPose.Smash => SmashFrames.Length,
            KnightPose.Pound => PoundFrames.Length,
            KnightPose.Sweep => SweepFrames.Length,
            KnightPose.Brace => 8,
            KnightPose.Charge => 8,
            KnightPose.Crouch => 8,
            KnightPose.Leap => 14,
            KnightPose.Roar => RoarRise + 4,
            _ => 4,
        };

        // A blow's frames come in stages, each frame shown for its stage's time: the swing (too fast to see, a smear
        // behind it), the strike, the rebound, held in the floor, hauled out, dragged back to rest.
        enum Stage : byte
        {
            Swing,
            Strike,
            Rebound,
            Held,
            Haul,
            // Dragged back to rest in two halves joined without a stop: speeding up to where the hammer swings past
            // beside it (shortened, pointing at the viewer, so its head does not sink into the floor), slowing to rest.
            DragStart,
            Drag,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.03f,
            Stage.Strike => 0.1f,
            Stage.Rebound => 0.1f,
            Stage.Held => 0.125f,
            Stage.Haul => 0.1f,
            _ => 0.075f,
        };

        static Stage[] StagesOf(KnightPose pose) => pose == KnightPose.Smash ? SmashStages : pose == KnightPose.Pound ? PoundStages : pose == KnightPose.Sweep ? SweepStages : null;

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(KnightPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        // The frames of a blow in which the hammer is swinging too fast to see (drawn with a smear behind it), and the
        // frame it strikes.
        public static bool Swinging(KnightPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(KnightPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // How the body turns with the motion, a step of the eight views: away from the viewer (-1) as the hammer goes up
        // and back, unwinding through the swing (a sweep turns on round toward the viewer, +1), square again after.
        public static int TwistOf(KnightPose pose, int frame) => pose switch
        {
            KnightPose.Raise or KnightPose.Wind => frame >= FramesOf(pose) / 3 ? -1 : 0,
            KnightPose.Smash or KnightPose.Pound => frame == 0 ? -1 : 0,
            KnightPose.Sweep => frame == 0 ? -1 : frame <= StrikeFrame(pose) ? 1 : 0,
            _ => 0,
        };

        // The frames a roar takes to lift the hammer overhead, before it shakes there (frames RoarRise.. loop).
        public const int RoarRise = 12;

        // The rest every motion starts from and returns to.
        const float RestAngle = -150;

        static Pose P(float raise, float reach, float angle, float lean = 0, float stretch = 0, float together = 0.6f, float stepA = 0, float stepB = 0,
            float drop = 0, float shove = 0, float lift = 0, float liftA = 0, float liftB = 0, float fore = 1) => new Pose
        {
            Raise = raise, Reach = reach, Angle = angle, Lean = lean, Stretch = stretch, Together = together, StepA = stepA, StepB = stepB,
            Drop = drop, Shove = shove, Lift = lift, LiftA = liftA, LiftB = liftB, Foreshorten = fore,
        };

        static readonly Pose Rest = P(0, -2, RestAngle);

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return new Pose
            {
                Raise = L(a.Raise, b.Raise), Reach = L(a.Reach, b.Reach), Together = L(a.Together, b.Together), StepA = L(a.StepA, b.StepA),
                LiftA = L(a.LiftA, b.LiftA), StepB = L(a.StepB, b.StepB), LiftB = L(a.LiftB, b.LiftB), Lean = L(a.Lean, b.Lean),
                Stretch = L(a.Stretch, b.Stretch), Lift = L(a.Lift, b.Lift), Shove = L(a.Shove, b.Shove), Drop = L(a.Drop, b.Drop),
                Angle = L(a.Angle, b.Angle), Foreshorten = L(a.Foreshorten, b.Foreshorten),
            };
        }

        // Key poses at times 0..1, eased in and out between each pair (the weight of a slow, heavy movement).
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

        static Pose Blow(Pose[] frames, int frame) => frames[System.Math.Max(0, System.Math.Min(frames.Length - 1, frame))];

        static Pose Smeared(Pose p, float from)
        {
            p.SmearFrom = from;
            p.SmearTo = p.Angle;
            return p;
        }

        // A blow laid out from key poses: each stage moves from the pose before to its own over so many frames; a swing
        // speeds up toward the strike and leaves a smear from where the hammer was a frame before, the rest ease in and
        // out. To smooth a stage, give it more frames.
        static (Pose[] Frames, Stage[] Stages) Expand(Pose from, params (Stage Stage, int Frames, Pose To)[] stages)
        {
            var frames = new List<Pose>();
            var kinds = new List<Stage>();
            var at = from;
            var previous = (Stage)255;
            foreach (var (stage, count, to) in stages)
            {
                bool joined = previous == Stage.DragStart;
                previous = stage;
                for (int k = 1; k <= count; k++)
                {
                    float u = k / (float)count;
                    bool fast = stage == Stage.Swing || stage == Stage.Strike;
                    float eased = fast ? (float)System.Math.Pow(u, 1.3) : stage == Stage.DragStart ? u * u : stage == Stage.Drag && joined ? 1 - (1 - u) * (1 - u) : u * u * (3 - 2 * u);
                    var p = Lerp(at, to, eased);
                    if (fast) p = Smeared(p, frames.Count > 0 ? frames[frames.Count - 1].Angle : from.Angle);
                    frames.Add(p);
                    kinds.Add(stage);
                }
                at = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        static readonly Pose RaiseTop = P(26, -5, -226, lean: -5, stretch: 0.08f, together: 0.95f, stepA: 3, stepB: -3);
        static readonly Pose WindBack = P(4, -11, -174, lean: -4, stretch: -0.06f, together: 0.75f, stepA: 2, stepB: -4);
        static readonly Pose Braced = P(0, -4, -162, lean: 9, stretch: -0.14f, stepA: 4, stepB: -4, drop: 1);
        static readonly Pose Crouched = P(8, 1, -250, lean: 3, stretch: -0.2f, together: 0.95f, stepA: 3, stepB: -3, drop: 2);
        // Back at rest after a blow: the same as Rest, the hammer having come one full way round.
        static readonly Pose Rested = P(0, -2, RestAngle - 360);

        // Overhead and down ahead: the swing speeding up over three frames, the strike with the whole body squashed
        // behind it, a rebound, held in the floor and straining, hauled out, and dragged back along the floor to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Smash = Expand(RaiseTop,
            (Stage.Swing, 4, P(4, 10, -345, lean: 5, stretch: -0.06f, together: 0.95f, stepA: 5, stepB: -4, drop: 1, shove: 4)),
            (Stage.Strike, 1, P(-3, 14, -384, lean: 9, stretch: -0.15f, together: 0.95f, stepA: 6, stepB: -4, drop: 3, shove: 6)),
            (Stage.Rebound, 1, P(-1, 14, -380, lean: 8, stretch: -0.1f, together: 0.95f, stepA: 6, stepB: -4, drop: 2, shove: 6)),
            (Stage.Held, 2, P(-2, 13, -384, lean: 7, stretch: -0.11f, together: 0.9f, stepA: 6, stepB: -4, drop: 2, shove: 5)),
            (Stage.Haul, 2, P(1, 9, -400, lean: 4, stretch: -0.04f, together: 0.85f, stepA: 4, stepB: -3, drop: 1, shove: 3)),
            (Stage.DragStart, 5, P(6, 4, -450, lean: 2, together: 0.7f, stepA: 2, stepB: -2, fore: 0.5f)),
            (Stage.Drag, 6, Rested));

        // Straight down at its own feet.
        static readonly (Pose[] Frames, Stage[] Stages) Pound = Expand(RaiseTop,
            (Stage.Swing, 4, P(5, 6, -360, lean: 4, stretch: -0.08f, together: 1, stepA: 4, stepB: -4, drop: 2)),
            (Stage.Strike, 1, P(-2, 9, -405, lean: 6, stretch: -0.17f, together: 1, stepA: 4, stepB: -4, drop: 4)),
            (Stage.Rebound, 1, P(0, 9, -400, lean: 5, stretch: -0.12f, together: 1, stepA: 4, stepB: -4, drop: 3)),
            (Stage.Held, 2, P(-1, 8, -404, lean: 4, stretch: -0.11f, together: 0.95f, stepA: 4, stepB: -4, drop: 2)),
            (Stage.Haul, 2, P(1, 6, -420, lean: 2, stretch: -0.04f, together: 0.85f, stepA: 3, stepB: -3, drop: 1)),
            (Stage.DragStart, 4, P(5, 3, -450, lean: 1, together: 0.7f, stepA: 2, stepB: -2, fore: 0.5f)),
            (Stage.Drag, 6, Rested));

        // Across the front at the waist: out of the wind-up behind, a flat smear past the viewer (the hammer pointing at
        // the viewer, shortened), out ahead, on a little, and dragged back round to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Sweep = Expand(WindBack,
            (Stage.Swing, 2, P(1, 4, -90, lean: 3, together: 0.8f, stepA: 4, stepB: -3, shove: 2, fore: 0.45f)),
            (Stage.Swing, 1, P(1, 11, -30, lean: 6, stretch: -0.05f, together: 0.8f, stepA: 5, stepB: -3, shove: 4, fore: 0.85f)),
            (Stage.Strike, 1, P(1, 13, 4, lean: 7, stretch: -0.07f, together: 0.8f, stepA: 5, stepB: -3, shove: 5)),
            (Stage.Rebound, 1, P(0, 12, -6, lean: 6, stretch: -0.06f, together: 0.8f, stepA: 5, stepB: -3, shove: 5)),
            (Stage.Held, 1, P(0, 10, -16, lean: 5, stretch: -0.04f, together: 0.75f, stepA: 4, stepB: -3, shove: 4)),
            (Stage.DragStart, 8, P(5, 4, -90, lean: 2, together: 0.7f, stepA: 2, stepB: -1, shove: 2, fore: 0.5f)),
            (Stage.Drag, 8, P(0, -2, RestAngle)));

        static Pose[] SmashFrames => Smash.Frames;
        static Stage[] SmashStages => Smash.Stages;
        static Pose[] PoundFrames => Pound.Frames;
        static Stage[] PoundStages => Pound.Stages;
        static Pose[] SweepFrames => Sweep.Frames;
        static Stage[] SweepStages => Sweep.Stages;

        static Pose PoseOf(KnightPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case KnightPose.Idle:
                {
                    // Breathing over the hammer resting behind it.
                    float b = 0.5f - 0.5f * c;
                    return P(b, -2, RestAngle + 2 * b, stretch: 0.015f * b);
                }
                case KnightPose.Walk:
                    // One stride each way per loop: the foot coming forward lifted, the body sinking as each lands
                    // (frames 2 and 6), the hammer's head dragged behind and wobbling with the steps.
                    return P(0, -3 + 0.6f * s, RestAngle - 2 + 2 * s, lean: 1, stretch: -0.025f * (1 - System.Math.Abs(c)), stepA: 3 * s, stepB: -3 * s,
                        drop: 1.2f * (1 - System.Math.Abs(c)), liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c));
                case KnightPose.Raise:
                    // Hauled up from the floor behind it, over the shoulder, high overhead: the body dips as it heaves, then
                    // rises tall and leans back onto the back foot.
                    return Keys(t,
                        (0, Rest),
                        (0.3f, P(5, -1, -178, lean: 0.5f, stretch: -0.03f, together: 0.8f, stepA: 2, stepB: -2, drop: 1)),
                        (0.65f, P(17, -3, -206, lean: -2.5f, stretch: 0.04f, together: 0.95f, stepA: 3, stepB: -3)),
                        (1, RaiseTop));
                case KnightPose.Wind:
                    // Drawn further back and low, the arms reaching behind, the body coiling away.
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(2, -6, -162, lean: -2, stretch: -0.03f, together: 0.7f, stepA: 1, stepB: -3)),
                        (1, WindBack));
                case KnightPose.Brace:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(-1, -3, -158, lean: 4, stretch: -0.06f, stepA: 2, stepB: -2)),
                        (1, Braced));
                case KnightPose.Charge:
                    // Running: longer strides, the hammer trailing.
                    return P(0, -4, -165 + 3 * s, lean: 10, stretch: -0.1f - 0.02f * (1 - System.Math.Abs(c)), stepA: 5 * s, stepB: -5 * s,
                        drop: 1 - System.Math.Abs(c), liftA: 3 * System.Math.Max(0, c), liftB: 3 * System.Math.Max(0, -c));
                case KnightPose.Crouch:
                    // The hammer swung up from behind to the shoulder while it sinks into a deep crouch.
                    return Keys(t,
                        (0, Rest),
                        (0.5f, P(4, 0, -205, lean: 2, stretch: -0.08f, together: 0.85f, stepA: 2, stepB: -2, drop: 1)),
                        (1, Crouched));
                case KnightPose.Leap:
                    // Out of the crouch: a last push down, the spring up (stretching tall), then up and out of the top of
                    // the picture with the hammer over its head.
                    return Keys(t,
                        (0, Crouched),
                        (0.15f, P(10, 1, -252, lean: 2, stretch: -0.06f, together: 1, stepA: 2, stepB: -2, drop: 1)),
                        (0.35f, P(14, 0, -255, lean: 1, stretch: 0.12f, together: 1, stepA: 1, stepB: -1, lift: 8, liftA: 2, liftB: 2)),
                        (1, P(22, 0, -260, stretch: 0.06f, together: 1, lift: 84, liftA: 3, liftB: 3)));
                case KnightPose.Smash:
                    return Blow(SmashFrames, f);
                case KnightPose.Pound:
                    return Blow(PoundFrames, f);
                case KnightPose.Sweep:
                    return Blow(SweepFrames, f);
                case KnightPose.Roar:
                    // The hammer lifted from rest up over its head, then held high, shaking with the roar.
                    if (f < RoarRise)
                        return Keys(f / (float)RoarRise,
                            (0, Rest),
                            (0.58f, P(12, -3, -222, lean: -1.5f, stretch: 0.025f, together: 0.9f, stepA: 2, stepB: -2)),
                            (1, P(22, 0, -270, lean: -3, stretch: 0.05f, together: 1, stepA: 3, stepB: -3)));
                    return P(22 + s, 0, -270 + 3 * s, lean: -3, stretch: 0.05f, together: 1, stepA: 3, stepB: -3);
                default:
                    return Rest;
            }
        }

        // The picture with each part at its place, on a canvas with room round it; and where the two fists are.
        static PixelImage Compose(BossView view, Pose pose, out (float X, float Y) hands)
        {
            var image = ViewOf(view);
            var parts = PartsOf(view);
            var (armA, armB, _, _) = BoxesOf(view);
            int w = image.Width, h = image.Height;
            bool side = view != BossView.Front && view != BossView.Back;
            int R(float v) => (int)System.Math.Round(v);
            // The arms close on each other, then rise and reach: from the side ahead is right; from the front a reach
            // shows as a little dip toward the viewer, from behind as a little rise.
            var fa = armA.Fist;
            var fb = armB.Fist;
            float mx = (fa.X + fb.X) / 2f;
            float dip = side ? 0 : view == BossView.Front ? 0.4f : -0.3f;
            float ahead = side ? pose.Reach : 0;
            int ax = R((mx - fa.X) * pose.Together + ahead), ay = R(-pose.Raise + pose.Reach * dip);
            int bx = R((mx - fb.X) * pose.Together + ahead), by = ay;
            int la = side ? R(pose.StepA) : 0, lb = side ? R(pose.StepB) : 0;
            int lay = R(-pose.LiftA - (side ? 0 : System.Math.Max(0, pose.StepA) * 0.5f));
            int lby = R(-pose.LiftB - (side ? 0 : System.Math.Max(0, pose.StepB) * 0.5f));
            hands = (Margin + (fa.X + ax + fb.X + bx) / 2f, Headroom + (fa.Y + ay + fb.Y + by) / 2f);

            var canvas = new PixelImage(w + 2 * Margin, h + Headroom);
            bool moved(Part part) => part == Part.ArmA ? ax != 0 || ay != 0 : part == Part.ArmB ? bx != 0 || by != 0 : part == Part.LegA ? la != 0 || lay != 0 : part == Part.LegB ? lb != 0 || lby != 0 : false;
            for (int y = 0; y < h; y++)
            {
                bool Body(int x) => image[x, y].A > 0 && parts[x, y] == Part.Body;
                for (int x = 0; x < w; x++)
                {
                    var c = image[x, y];
                    if (c.A == 0) continue;
                    if (parts[x, y] == Part.Body || !moved(parts[x, y]))
                    {
                        canvas[x + Margin, y + Headroom] = c;
                        continue;
                    }
                    // Where a part has moved away from inside the body, the body behind it in shade.
                    int left = -1, right = -1;
                    for (int k = x - 1; k >= 0 && left < 0; k--) if (Body(k)) left = k;
                    for (int k = x + 1; k < w && right < 0; k++) if (Body(k)) right = k;
                    if (left >= 0 && right >= 0)
                    {
                        var near = image[x - left <= right - x ? left : right, y];
                        canvas[x + Margin, y + Headroom] = Luminance(near) < 20 ? Shade : near.Mix(Shade, 0.6f);
                    }
                }
            }
            void Lay(Part part, int dx, int dy)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (parts[x, y] == part) canvas[x + Margin + dx, y + Headroom + dy] = image[x, y];
            }
            // An arm moved far is drawn anew: a plated arm from the shoulder (the top of its box, under the pauldron) to
            // the hand, and the gauntlet (the bottom of the box) closed round the haft there.
            void Arm(Part part, Box box, int dx, int dy)
            {
                if (System.Math.Abs(dx) < 4 && System.Math.Abs(dy) < 4)
                {
                    Lay(part, dx, dy);
                    return;
                }
                var fist = box.Fist;
                float sx = (box.X0 + box.X1) / 2f + Margin, sy = box.Y0 + 2 + Headroom;
                float ex = fist.X + dx + Margin, ey = fist.Y + dy + Headroom;
                float length = (float)System.Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
                float ux = (ex - sx) / System.Math.Max(1, length), uy = (ey - sy) / System.Math.Max(1, length);
                for (float d = 0; d <= length; d += 0.5f)
                {
                    for (float o = -3.5f; o <= 3.5f; o += 0.5f)
                    {
                        int x = (int)System.Math.Round(sx + ux * d - uy * o), y = (int)System.Math.Round(sy + uy * d + ux * o);
                        if (!canvas.Inside(x, y)) continue;
                        float edge = System.Math.Abs(o);
                        // Lit on the side facing up, a dark seam down the middle, banded at the elbow.
                        // The side facing up (or, held upright, facing right) catches the light.
                        bool lit = System.Math.Abs(ux) > 0.3f ? ux * o < 0 : -uy * o > 0;
                        var c = edge > 2.7f ? Ink : System.Math.Abs(d - length * 0.45f) < 1.2f ? PlateDark : edge < 0.6f ? PlateDark : lit ? PlateLight : Plate;
                        canvas[x, y] = c;
                    }
                }
                for (int y = System.Math.Max(box.Y0, box.Y1 - 9); y <= box.Y1; y++)
                    for (int x = box.X0; x <= box.X1; x++)
                        if (parts[x, y] == part) canvas[x + Margin + dx, y + Headroom + dy] = image[x, y];
            }
            Lay(Part.LegB, lb, lby);
            Lay(Part.LegA, la, lay);
            Arm(Part.ArmB, armB, bx, by);
            Arm(Part.ArmA, armA, ax, ay);
            return canvas;
        }

        // ---- The war hammer: a long dark haft bound in iron, a great square head of steel. ----

        static readonly Rgba Haft = new Rgba(70, 52, 40);
        static readonly Rgba HaftLight = new Rgba(104, 80, 60);
        static readonly Rgba Band = new Rgba(120, 128, 132);
        static readonly Rgba Steel = new Rgba(112, 121, 125);
        static readonly Rgba SteelLight = new Rgba(172, 179, 179);
        static readonly Rgba SteelShine = new Rgba(222, 226, 228);
        static readonly Rgba SteelDark = new Rgba(49, 53, 60);
        static readonly Rgba Ember = new Rgba(255, 120, 60);

        const int HaftLength = 30;

        // The hammer held at (hx, hy) pointing along (dx, dy) on the screen (dy up), shortened by foreshorten; its head
        // glows at the edge when hot (the last phase).
        static void Hammer(PixelImage image, float hx, float hy, float dx, float dy, float foreshorten, bool hot)
        {
            // A shortened way (seen end on) shortens the haft; the way itself is made whole again.
            float seen = (float)System.Math.Sqrt(dx * dx + dy * dy);
            foreshorten *= System.Math.Max(0.3f, seen);
            if (seen > 1e-3f)
            {
                dx /= seen;
                dy /= seen;
            }
            float length = HaftLength * foreshorten;
            var haft = new List<(int X, int Y, Rgba C)>();
            for (int i = -7; i <= (int)length; i++)
            {
                int x = (int)System.Math.Round(hx + dx * i), y = (int)System.Math.Round(hy - dy * i);
                haft.Add((x, y, i % 9 == 0 ? Band : i % 2 == 0 ? HaftLight : Haft));
                haft.Add((x + (int)System.Math.Round(dy), y + (int)System.Math.Round(dx), Haft));
            }
            foreach (var (x, y, _) in haft)
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (image.Inside(x + ox, y + oy) && image[x + ox, y + oy].A == 0) image[x + ox, y + oy] = Ink;
            foreach (var (x, y, c) in haft)
                if (image.Inside(x, y)) image[x, y] = c;
            // The head: across the haft at its end, eight long and fifteen wide, shaded from the top.
            float cx = hx + dx * length, cy = hy - dy * length;
            float ux = dx, uy = -dy;
            float vx = -uy, vy = ux;
            float halfAlong = 4.5f * System.Math.Max(0.6f, foreshorten), halfAcross = 7.5f;
            for (int y = (int)(cy - 12); y <= (int)(cy + 12); y++)
            {
                for (int x = (int)(cx - 12); x <= (int)(cx + 12); x++)
                {
                    if (!image.Inside(x, y)) continue;
                    float rx = x - cx, ry = y - cy;
                    float u = rx * ux + ry * uy, v = rx * vx + ry * vy;
                    float au = System.Math.Abs(u), av = System.Math.Abs(v);
                    if (au > halfAlong + 1 || av > halfAcross + 1) continue;
                    if (au > halfAlong || av > halfAcross)
                    {
                        image[x, y] = Ink;
                        continue;
                    }
                    var c = ry < -2.5f ? SteelLight : ry > 2.5f ? SteelDark : Steel;
                    if (av > halfAcross - 1.2f) c = hot ? Ember : SteelShine;
                    else if (System.Math.Abs(v) < 1 && au < halfAlong - 1) c = SteelDark;
                    image[x, y] = c;
                }
            }
        }

        // The swing too fast to see: a band along the arc the head travels, brightest at its outer edge.
        static void Smear(PixelImage image, float hx, float hy, float from, float to, float radius, float flatten)
        {
            float span = to - from;
            int steps = (int)(System.Math.Abs(span) / 3) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float a = (from + span * i / steps) * (float)System.Math.PI / 180;
                float width = 3 + 6f * i / steps;
                for (float r = radius - width; r <= radius + 2; r += 0.7f)
                {
                    int x = (int)System.Math.Round(hx + System.Math.Cos(a) * r);
                    int y = (int)System.Math.Round(hy - System.Math.Sin(a) * r * flatten);
                    if (!image.Inside(x, y) || image[x, y].A > 0) continue;
                    image[x, y] = r > radius ? new Rgba(240, 244, 248, 230) : r > radius - width / 2 ? new Rgba(200, 210, 220, 170) : new Rgba(150, 160, 175, 110);
                }
            }
        }

        // The hammer's way on the screen for an angle seen from the side, in this view: from the front ahead is toward
        // the viewer (down the screen), from behind away (up); the three-quarter views a little shortened.
        static (float X, float Y) Way(BossView view, float angle)
        {
            double a = angle * System.Math.PI / 180;
            float ahead = (float)System.Math.Cos(a), up = (float)System.Math.Sin(a);
            switch (view)
            {
                case BossView.Front:
                {
                    float x = ahead * 0.7f, y = up - ahead * 0.15f;
                    // Pointing at or away from the viewer it looks shorter.
                    float n = (float)System.Math.Sqrt(x * x + y * y);
                    float seen = System.Math.Max(0.45f, System.Math.Min(1, n));
                    return n < 1e-3f ? (0, seen) : (x / n * seen, y / n * seen);
                }
                case BossView.Back:
                {
                    float x = -ahead * 0.3f, y = up + ahead * 0.45f;
                    // Pointing at or away from the viewer it looks shorter.
                    float n = (float)System.Math.Sqrt(x * x + y * y);
                    float seen = System.Math.Max(0.45f, System.Math.Min(1, n));
                    return n < 1e-3f ? (0, seen) : (x / n * seen, y / n * seen);
                }
                default:
                    return (ahead, up);
            }
        }

        // Whether the hammer is behind the body: raised back over its head or trailing behind from the side, held
        // ahead seen from behind.
        static bool HammerBehind(BossView view, float angle)
        {
            double a = angle * System.Math.PI / 180;
            float ahead = (float)System.Math.Cos(a);
            return view == BossView.Back || view == BossView.BackSide ? ahead > -0.2f : ahead < -0.35f;
        }

        static bool IsTabard(Rgba c) => c.B > c.R + 25 && c.B > 50;

        static readonly Rgba Blood = new Rgba(120, 20, 28);

        // One frame. A wind-up (Raise, Wind, Brace, Crouch) runs frame 0..5 through its telegraph; a blow (Smash, Pound,
        // Sweep) 0..3, a smear, the impact, held, recovering; a walk or a charge 0..3, a leap 0..3 rising (and 0..1
        // breathing at rest). In the last phase the tabard is soaked dark red and the hammer's edge glows like a forge.
        // Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, KnightPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the hands, the hammer's head and the body are in a frame (canvas pixels): for the preview's check that
        // nothing jumps from one frame to the next.
        public struct Joints
        {
            public float HandX, HandY, HeadX, HeadY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, KnightPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static PixelImage Render(BossView view, KnightPose pose, int frame, int phase, out Joints joints)
        {
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            var p = PoseOf(pose, frame);
            bool side = view != BossView.Front && view != BossView.Back;
            var body = Compose(view, p, out var hands);
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
            int tall = System.Math.Max(4, (int)System.Math.Round(h * (1 + stretch)));
            int wide = System.Math.Max(4, (int)System.Math.Round(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2 + (int)System.Math.Round(shove);
            int bottom = Height - 1;
            int ground = Height - 1 - Floor - (int)p.Lift + (int)System.Math.Round(drop);
            tall -= (int)System.Math.Round(System.Math.Max(0, drop));
            // Where a canvas point lands once leaned and squashed.
            (float X, float Y) Place(float x, float y)
            {
                float row = ground - (tall - 1) + y * tall / h;
                float up = (ground - row) / (tall - 1);
                return (left + x * wide / w + lean * up, row);
            }
            var way = Way(view, p.Angle);
            var (hx, hy) = Place(hands.X, hands.Y);
            bool behind = HammerBehind(view, p.Angle);
            float seen = (float)System.Math.Sqrt(way.X * way.X + way.Y * way.Y);
            float reach = HaftLength * p.Foreshorten * System.Math.Max(0.3f, seen);
            joints.HandX = hx;
            joints.HandY = hy;
            joints.HeadX = hx + way.X / System.Math.Max(1e-3f, seen) * reach;
            joints.HeadY = hy - way.Y / System.Math.Max(1e-3f, seen) * reach;
            bool hot = phase >= 3;
            if (behind) Hammer(image, hx, hy, way.X, way.Y, p.Foreshorten, hot);
            for (int y = 0; y < tall; y++)
            {
                int row = ground - (tall - 1 - y);
                if (row < 0 || row > bottom) continue;
                int sy = y * h / tall;
                float up = (tall - 1 - y) / (float)(tall - 1);
                int shift = (int)System.Math.Round(lean * up);
                for (int x = 0; x < wide; x++)
                {
                    var c = body[x * w / wide, sy];
                    if (c.A == 0) continue;
                    if (phase >= 3 && IsTabard(c)) c = c.Mix(Blood, 0.7f);
                    int px = left + x + shift;
                    if (image.Inside(px, row)) image[px, row] = c;
                    joints.Top = System.Math.Min(joints.Top, row);
                    joints.Bottom = System.Math.Max(joints.Bottom, row);
                    joints.Left = System.Math.Min(joints.Left, px);
                    joints.Right = System.Math.Max(joints.Right, px);
                }
            }
            // A swing too fast to see: a smear along the way the head came this frame, the hammer where it is now.
            if (p.SmearFrom is float from && p.SmearTo is float to)
            {
                if (side) Smear(image, hx, hy, from, to, HaftLength + 4, pose == KnightPose.Sweep ? 0.45f : 1);
                else
                {
                    var a = Way(view, from);
                    float sweep = (float)(System.Math.Atan2(way.Y, way.X) - System.Math.Atan2(a.Y, a.X)) * 180 / (float)System.Math.PI;
                    if (sweep > 180) sweep -= 360;
                    if (sweep < -180) sweep += 360;
                    float start = (float)(System.Math.Atan2(a.Y, a.X) * 180 / System.Math.PI);
                    Smear(image, hx, hy, start, start + sweep, HaftLength * 0.8f, 1);
                }
            }
            if (!behind) Hammer(image, hx, hy, way.X, way.Y, p.Foreshorten, hot);
            return image;
        }
    }
}
