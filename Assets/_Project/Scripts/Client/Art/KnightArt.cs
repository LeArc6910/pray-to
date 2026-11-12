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

        static Pose PoseOf(KnightPose pose, int frame)
        {
            int f = System.Math.Max(0, frame);
            float t = f / 5f;
            var p = new Pose { Together = 0.6f, Angle = -38, Foreshorten = 1 };
            switch (pose)
            {
                case KnightPose.Idle:
                    // The hammer's head rests on the floor ahead, both hands on the haft; breathing.
                    p.Reach = 2;
                    p.Raise = f % 2;
                    p.Stretch = f % 2 == 1 ? 0.015f : 0;
                    break;
                case KnightPose.Walk:
                    // Each heavy step in turn, the head of the hammer dragged behind on the floor.
                    p.StepA = new[] { 3, 0, -3, 0 }[f % 4];
                    p.LiftA = f % 4 == 0 ? 2 : 0;
                    p.StepB = -p.StepA;
                    p.LiftB = f % 4 == 2 ? 2 : 0;
                    p.Reach = -3;
                    p.Angle = -152;
                    p.Lean = 1;
                    p.Stretch = f % 2 == 1 ? -0.025f : 0;
                    p.Drop = f % 2 == 1 ? 1 : 0;
                    break;
                case KnightPose.Raise:
                    // Hauled up overhead and back, slowly, rising tall on the back foot: the long anticipation.
                    p.Raise = 6 + 20 * t;
                    p.Reach = -2 - 4 * t;
                    p.Together = 0.95f;
                    p.Angle = 95 + 60 * t;
                    p.Lean = -1 - 4 * t;
                    p.Stretch = 0.02f + 0.06f * t;
                    p.StepA = 3;
                    p.StepB = -3;
                    break;
                case KnightPose.Smash:
                    // Down in one blur, the head driven into the floor ahead with the whole body squashed behind it,
                    // held there, then hauled out.
                    p.Together = 0.95f;
                    p.StepA = 6;
                    p.StepB = -4;
                    switch (System.Math.Min(f, 3))
                    {
                        case 0:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Shove) = (12, 9, 35, 4, 3);
                            (p.SmearFrom, p.SmearTo) = (150, 20);
                            break;
                        case 1:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Shove, p.Drop) = (-3, 14, -24, 9, -0.15f, 6, 3);
                            break;
                        case 2:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Shove, p.Drop) = (-3, 14, -24, 8, -0.11f, 6, 2);
                            break;
                        default:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Shove, p.Drop) = (0, 10, -20, 5, -0.05f, 4, 1);
                            break;
                    }
                    break;
                case KnightPose.Pound:
                    // Straight down at its own feet: the floor round it breaks.
                    p.Together = 1;
                    p.StepA = 4;
                    p.StepB = -4;
                    switch (System.Math.Min(f, 3))
                    {
                        case 0:
                            (p.Raise, p.Reach, p.Angle, p.Lean) = (14, 5, 60, 2);
                            (p.SmearFrom, p.SmearTo) = (140, -60);
                            break;
                        case 1:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Drop) = (-2, 9, -45, 6, -0.17f, 4);
                            break;
                        case 2:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Drop) = (-2, 9, -45, 5, -0.12f, 3);
                            break;
                        default:
                            (p.Raise, p.Reach, p.Angle, p.Lean, p.Stretch, p.Drop) = (0, 7, -38, 3, -0.05f, 1);
                            break;
                    }
                    break;
                case KnightPose.Wind:
                    // Drawn far back and low for the sweep, the body turning away with it.
                    p.Raise = 2 + 2 * t;
                    p.Reach = -6 - 5 * t;
                    p.Together = 0.75f;
                    p.Angle = 172 + 14 * t;
                    p.Lean = -2 - 2 * t;
                    p.Stretch = -0.02f - 0.04f * t;
                    p.StepA = 2;
                    p.StepB = -4;
                    break;
                case KnightPose.Sweep:
                    // Across the front at the waist: a flat smear toward the viewer, then out ahead and on.
                    p.Together = 0.8f;
                    p.StepA = 5;
                    p.StepB = -3;
                    switch (System.Math.Min(f, 3))
                    {
                        case 0:
                            (p.Reach, p.Raise, p.Angle, p.Lean, p.Foreshorten, p.Shove) = (6, 1, -90, 4, 0.45f, 2);
                            (p.SmearFrom, p.SmearTo) = (190, 360);
                            break;
                        case 1:
                            (p.Reach, p.Raise, p.Angle, p.Lean, p.Stretch, p.Shove) = (13, 1, 4, 7, -0.07f, 5);
                            break;
                        case 2:
                            (p.Reach, p.Raise, p.Angle, p.Lean, p.Stretch, p.Shove) = (12, 0, -16, 6, -0.05f, 5);
                            break;
                        default:
                            (p.Reach, p.Raise, p.Angle, p.Lean, p.Stretch, p.Shove) = (7, -1, -34, 3, -0.02f, 3);
                            break;
                    }
                    break;
                case KnightPose.Brace:
                    // The shoulder lowered, the hammer held low behind, sinking for the run.
                    p.Lean = 6 + 3 * t;
                    p.Stretch = -0.07f - 0.07f * t;
                    p.Reach = -4;
                    p.Angle = 198;
                    p.StepA = 4;
                    p.StepB = -4;
                    p.Drop = 1;
                    break;
                case KnightPose.Charge:
                    p.StepA = new[] { 5, 0, -5, 0 }[f % 4];
                    p.LiftA = f % 4 == 0 ? 3 : 0;
                    p.StepB = -p.StepA;
                    p.LiftB = f % 4 == 2 ? 3 : 0;
                    p.Lean = 10;
                    p.Stretch = -0.1f;
                    p.Reach = -4;
                    p.Angle = 195;
                    break;
                case KnightPose.Crouch:
                    // Down into a deep crouch for the jump, the hammer drawn up to the shoulder.
                    p.Stretch = -0.06f - 0.14f * t;
                    p.Lean = 3;
                    p.Raise = 2 + 6 * t;
                    p.Reach = 1;
                    p.Together = 0.9f;
                    p.Angle = 110;
                    p.StepA = 3;
                    p.StepB = -3;
                    break;
                case KnightPose.Leap:
                    // Up and out of the top of the picture with the hammer over its head.
                    p.Lift = new[] { 16, 34, 54, 78 }[System.Math.Min(f, 3)];
                    p.Stretch = 0.08f;
                    p.Raise = 22;
                    p.Together = 1;
                    p.Angle = 100;
                    p.LiftA = p.LiftB = 3;
                    break;
                case KnightPose.Roar:
                    p.Raise = 22 + f % 2;
                    p.Together = 1;
                    p.Angle = 90;
                    p.Lean = -3;
                    p.Stretch = 0.05f;
                    p.StepA = 3;
                    p.StepB = -3;
                    break;
            }
            return p;
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
            return view == BossView.Back || view == BossView.BackSide ? ahead > -0.2f : ahead < -0.35f && view != BossView.Front;
        }

        static bool IsTabard(Rgba c) => c.B > c.R + 25 && c.B > 50;

        static readonly Rgba Blood = new Rgba(120, 20, 28);

        // One frame. A wind-up (Raise, Wind, Brace, Crouch) runs frame 0..5 through its telegraph; a blow (Smash, Pound,
        // Sweep) 0..3, a smear, the impact, held, recovering; a walk or a charge 0..3, a leap 0..3 rising (and 0..1
        // breathing at rest). In the last phase the tabard is soaked dark red and the hammer's edge glows like a forge.
        // Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, KnightPose pose, int frame, int phase)
        {
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
                }
            }
            if (p.SmearFrom is float from && p.SmearTo is float to)
            {
                if (side) Smear(image, hx, hy, from, to, HaftLength + 4, pose == KnightPose.Sweep ? 0.45f : 1);
                else Smear(image, hx, hy, view == BossView.Front ? 160 : 20, view == BossView.Front ? 380 : -200, HaftLength * 0.6f, 0.5f);
            }
            else if (!behind) Hammer(image, hx, hy, way.X, way.Y, p.Foreshorten, hot);
            return image;
        }
    }
}
