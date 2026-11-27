using System.Collections.Generic;
using System.Linq;

namespace Game.Client
{
    // What the mad general is doing, which picks its frame (WorldView.General.cs, Docs/Bosses.md 미쳐 버린 장군 모르그).
    // A commander, not a lone duellist: every blow is an order to an army that is no longer there. The greatsword
    // swung up and brought down in a great overhand cleave (Wind, Cleave); the sword thrust up to the sky and swept
    // down to point (Order, Command: the fire it calls lands where it points); the sword raised point-down and driven
    // into the floor (Heft, Stab: ghostly spears cross where it struck); the war banner pulled from his back, held high
    // and planted (Hoist, Plant: the fire rings out from the pole); crouched with the blade drawn back and spun round
    // (Coil, Spin); the sword levelled like a lance for a run (Brace, Charge); the banner waved over his head to call
    // the soldiers (Rally); a roar with the sword aloft. Kneels before his planted sword until woken (Enter).
    enum GeneralPose
    {
        Idle,
        Walk,
        Wind,
        Cleave,
        Order,
        Command,
        Heft,
        Stab,
        Hoist,
        Plant,
        Coil,
        Spin,
        Brace,
        Charge,
        Rally,
        Roar,
        Enter,
    }

    // The mad general Morg of the demon nest (2026-11-27): a crimson-skinned horned demon warlord with a wild white mane,
    // the late demon king's right hand. Its five views come from one Retro Diffusion Pro picture turned eight ways
    // (Docs/ArtSources.md, $0.28), drawn empty-handed; the flaming greatsword in his right hand and the tattered war
    // banner on his back are drawn here, and every pose is made in code as for the cave warden: the arms and legs are
    // a box each per view, moved apart from the body (an arm moved far drawn anew from the shoulder), and the whole
    // body leans, rises, squashes on the blow and is carried into it. In the last phase his madness shows: the skin
    // flushes, the blade burns white-hot. Faces right.
    static class GeneralArt
    {
        public const int Width = 112;
        public const int Height = 100;
        // Rows under its feet; WorldView stands the picture on this row (Pivot).
        public const int Floor = 4;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 54;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(9, 3, 7),
            ['b'] = new Rgba(6, 6, 16),
            ['c'] = new Rgba(40, 8, 13),
            ['d'] = new Rgba(25, 15, 22),
            ['e'] = new Rgba(49, 12, 17),
            ['f'] = new Rgba(32, 21, 28),
            ['g'] = new Rgba(44, 31, 34),
            ['h'] = new Rgba(77, 28, 37),
            ['i'] = new Rgba(66, 35, 40),
            ['j'] = new Rgba(90, 28, 36),
            ['k'] = new Rgba(59, 46, 48),
            ['l'] = new Rgba(100, 36, 41),
            ['m'] = new Rgba(117, 40, 45),
            ['n'] = new Rgba(80, 58, 51),
            ['o'] = new Rgba(133, 48, 51),
            ['p'] = new Rgba(96, 72, 66),
            ['q'] = new Rgba(150, 59, 56),
            ['r'] = new Rgba(99, 92, 82),
            ['s'] = new Rgba(167, 69, 71),
            ['t'] = new Rgba(183, 76, 72),
            ['u'] = new Rgba(119, 108, 97),
            ['v'] = new Rgba(154, 141, 132),
            ['w'] = new Rgba(181, 175, 174),
            ['x'] = new Rgba(220, 206, 186),
            ['y'] = new Rgba(240, 237, 229),
        };

        static readonly string[] FrontRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "...........................nf........dd.........................",
            "..........................dk.........dpf........................",
            ".........................grk..........frk.......................",
            ".........................bndvrrryyuu.vgrr.......................",
            ".........................bnrkvyywyyyuvvrr.......................",
            ".........................bgkrgwyxxyyyvrri.......................",
            "..........................fggrrbwwyywgrkg.......................",
            "........................rvvfgkrkqvwytkgi........................",
            "......................uuvxgwakgitqsotfg.........................",
            "........................rxeoeihsttstss..........................",
            ".......................uxxvmmcoehtqmtef.........................",
            "......................uywxwhjltowhchwh..........................",
            "......................rwwvvvllstmoqqoo..........................",
            "....................vrvxwvwvwkljssceld..........................",
            "...................vfvufpvvvwkjetshttknk...d....................",
            "..................gvvdppnvuwxufqsyyxevfugc......................",
            "...................aucuunvewxuidsstqcwfruug.....................",
            "...................dnivkupvdwxrleqihvxwdruu.....................",
            "..................bnprvvnkupuxrlohhmmwwepnpb....................",
            "..................ikprrgnkudmxrttmmttuudpkkr....................",
            ".................bnuiprnkudsspvtttttmurcfiuk....................",
            "..................ffvuuuuflssprttttqtlsjcdd.....................",
            "..................lmmcaaddhsstttttltsttjjjd.....................",
            "..................moosmldddlmttttlsmssjclsmn....................",
            ".................assssqhdhhiimmmttssslhglsmg....................",
            ".................osmssmadhelmtttqommmsmglqmsgg..................",
            "...............feosmmjk..hjlqtmmttmsoma.hllspd..................",
            "...............ugveqha....ammomttqlsoh..homuud..................",
            "...............vvrvema....alosmqtolslh..dhprpf..................",
            "................npupna....dfjlmshfchlfb..kkkpp..................",
            "................gnukk.....fkggffvwxwguf..bkiprb.................",
            "................gpukb.....fkkukkuwwwfpb...dnurd.................",
            "................gnpib....bddkkkkfdvffgf....clqh.................",
            "................bpvcc....fnnpdbbauuvdknk...lstj.................",
            "................bdmtte..fnnnraeifviuifpkb.glhlq.................",
            ".................msttskvdnnunhehidfihidp..dhios.................",
            ".................osttldvffdghlemhiihlghei..fle..................",
            ".................omssh..iihmolemmiiligmmh.......................",
            "..................mjc..bilmeomemmhhmigmelf......................",
            ".......................ehcceememmmmmligocb......................",
            ".......................ddoesofemmmmhgilse.......................",
            "......................bvlqqsocfmmmmihilts.......................",
            ".......................fjssmmdfhmmhdellso.......................",
            ".......................ljssmc.fjmmmdlllla.......................",
            "......................gqjjhh..f.dm.dllqj........................",
            "......................dqollh....hd.djmsc........................",
            "......................aqsmmd........hmmh........................",
            "......................dssmm.........dmlc........................",
            "......................dsol..........lmssh.......................",
            "......................home..........mmqssqd.....................",
            "......................lom............ecoqhs.....................",
            "......................sss................e......................",
            ".....................qqss.......................................",
            ".....................qolqm......................................",
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
            "................................fg..d...........................",
            "...............................dk...ff..........................",
            "..............................grk...gnf.........................",
            "..............................bpguuxxpg.........................",
            "..............................bnkwyyyyu.........................",
            "..............................bipgxyyxyr........................",
            "............................u.bggrfwyxyy........................",
            "...........................vyyybggrgsvwy........................",
            "...........................uyywwdgggstsl........................",
            ".................urr.......uyyesdghstttl........................",
            "................rwyyrr.....uyyqhqemochsq........................",
            "..................vyyv....rxxxwlemmsqxee........................",
            "................rrrwyywvvuuxxwwemmmttlosq.......................",
            "...............uyyyyyywkdxyxwwvwkhmmttqho.......................",
            "..............rxuvwyywfknwwwwwvwrimostqh........................",
            "...............uuyywvwffwddddkwywihosty.........................",
            "..............vryywwvrvavvpprrdyyrfeess.........................",
            "............uvvuyywvrrvbuvdurrvfyynhdce.........................",
            "............xvvuywwrvubkgnnvrruvdywpcwxi........................",
            "............xyrxywvrvubnprrvvpnugiyrmrxg........................",
            "............ryyyxwruvrbkpprrkkufdlxxtmwf........................",
            ".............rwwwru..aprvnppkuaqttlxtti.........................",
            "..............rrr.....dddvuuuahsttmrtoo.........................",
            "......................hmmlaaadhhotttoom.........................",
            "......................jtsmmmdhhiihmmtt..........................",
            "......................jttstqdjlmmostooa.........................",
            ".....................hsttqteljlmottmtoa.........................",
            "....................esqttmjaljmmqsotqmif........................",
            "....................ermttcnvajlmmsoqolpf........................",
            "....................dvnccab.dhllmoosccinf.......................",
            "....................fnvuvubvdffdddddwxgpub......................",
            "....................bkknupbvdkkkknkdwxufcb......................",
            ".....................bkpupbdddkkkukddpulssf.....................",
            ".....................bknppbdgffffdddvvdmllq.....................",
            "......................akpvadnnppudfgfudjhsh.....................",
            ".......................brfttcnppuhfiidddll......................",
            ".......................bdotttduudjemiid.........................",
            "........................hqstoeaammemhhdd........................",
            "........................hosthhjlmmemmmdd........................",
            ".........................hhhjmmmcoemmmd.........................",
            ".........................dhhjmmccmemmmb.........................",
            "........................bjjjccesedflmmb.........................",
            "........................ahddmmoqdeflmmd.........................",
            "........................dddmmstjdldjmd..........................",
            "........................baoqmqqedlldl...........................",
            ".........................cstole..dlma...........................",
            ".........................cqtsmd..cmh............................",
            ".........................dmqsl..dllqc...........................",
            ".........................bmsqe..dmmssee.........................",
            ".........................amsh...ccccosqe........................",
            ".........................aooh.......cee.........................",
            ".........................jjsj...................................",
            ".........................jqtte..................................",
            ".........................cottsc.................................",
            "..........................eollqo................................",
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
            "................................d.vdd...........................",
            "...............................ddvbn............................",
            "..............................dkdvbn............................",
            "..............................dknrbnf...........................",
            "..............................dxyybin...........................",
            ".............................uyyyybgpvy.........................",
            ".............................yyyyybggnxy........................",
            "............................yyyyyybgggvy........................",
            "............................yyyyyrvgggth........................",
            "............................yyyyweqdgoth........................",
            "............................yyyywehqjqes........................",
            ".................rrrr.....ryyywyweohmtvcn.......................",
            ".................wyyyrr...uxywyywweomtsmq.......................",
            ".................rwwyywruyywwxywwwvemmtle.......................",
            ".................yyyyywfdwxwyyywvwwpmmts........................",
            "...............ryvwyywwfdwyyyykvfwypiot.........................",
            "..............rxvwyywwvdwwyyyddvfdwyresj........................",
            "...............uvyywvrrwwwwwbrnuvpdrynhj........................",
            "...............rwywvrvr.wvvbprdkivrdvy..........................",
            "...........r...rwywvrvr..nbppnrruvurby..........................",
            "...........r.uvrwxvvrvr.rubknnppprkravx.........................",
            ".............yrrywvr..r...akuuuvvnnasl..........................",
            ".............wyyxwr.......ardddbbvrattm.........................",
            ".............rwwwr........hbdmmmmaahmtm.........................",
            "...............rr.........hccmtmoodiilm.........................",
            ".........................bmccmttttdjmm..........................",
            ".........................emcamttqclmst..........................",
            ".........................elloottmclssm..........................",
            ".........................dlcqoctmclosl..........................",
            "..........................bfcldvchhoqe..........................",
            "...........................baufvvubdddw.........................",
            "............................argkkubrkfvr........................",
            "............................akknpubukdp.........................",
            "...........................brbkkprnbd...........................",
            "...........................arnbkkpvaag..........................",
            "...........................aunnbnugtsk..........................",
            "...........................arundbdqtte..........................",
            "..........................dhafuuaoqtttd.........................",
            "..........................djhfddaoossh..........................",
            "..........................dmmhhhhhmhee..........................",
            "..........................dmmmmmhhhcdf..........................",
            "..........................dmmmmmmmdmdf..........................",
            "..........................dmmmdddcocdf..........................",
            "..........................dmddemltmvdd..........................",
            "..........................ddacoomtl.dd..........................",
            "..........................ddaqsolmg..d..........................",
            "............................astsmd..............................",
            "............................dmqsm...............................",
            "............................bmosmoh.............................",
            "............................alosclje............................",
            "............................amsmc...............................",
            "............................cmlmtc..............................",
            "............................coottte.............................",
            "............................ceejqttoc...........................",
            "................................ehmml...........................",
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
            "..........................fd....................................",
            ".........................gk...........df........................",
            ".........................pd...........di........................",
            "........................pkd....uuuu....id.......................",
            "........................bkgbvuyyyyyuvvbid.......................",
            "........................bkiiryyyyyyyvbigd.......................",
            ".........................bigdyyyyyyybggib.......................",
            "..........................bgwyyyyyyybggb........................",
            "...........................dxyyyyyyxwbd.........................",
            "..........................hexyyyyyyywdh.........................",
            "..........................hhwwyyyyyywfh.........................",
            "..........................uuuwyyyyxywlh.........................",
            "........................rxyyyuxyyxyywlp.........................",
            ".......................ryrwyyyuyyxyywu..........................",
            "....................uuvvdxyyyyryyyyyyu..........................",
            "....................bvvbuywyywnwxyywybddvvb.....................",
            ".....................uduvyxywvrwxyywdpppavvd....................",
            ".....................druywxywuwwwyywgpuucu......................",
            "....................dnppywxywuwwwwxgprvvpgf.....................",
            "....................bnnkxuwyyrwwwwdpppppppkd....................",
            "...................nruunwdwyxwrwwddunknppind....................",
            "....................ddddddwywwrweooduuuvuuurd...................",
            ".....................mmlerwywkjnoqqqddfddddd....................",
            "....................jtomerywrolmmqsqmhlmmmh.....................",
            "....................lmmmd..pmmlmmlqomcmmqtqf....................",
            "...................dtmmjdrpmmmmmmljmmcmmqqte....................",
            "..................rhqmldvrddprpplmmmmldlmqte....................",
            "..................ammlld..kppuvuvpfddd.bmtosd...................",
            "..................admmmb..bnkppppuuppk..cmtsad..................",
            "..................audddk..ddddddnnnkkk..cmlouip.................",
            "..................bkkkb...dknrkkfddddd..cmmerb..................",
            "..................bkkkb...ddffkkkunkkd..dffukb..................",
            "..................buppb..dnrffdddffffa...akkka..................",
            "...................baae.bnnuahhiduunnnd...dkkgb.................",
            "...................cmemdfnuahhmjdurppkd...bprub.................",
            "...................emhjddrfhlmmjdvrppnkd..dffcj.................",
            "....................elc.ddfjjmmjjavuurrb..ccmsj.................",
            ".......................dhhlljmmlljddaaad..dlmsj.................",
            ".......................bjmmmjmmlmmhhhhhhb..eme..................",
            ".......................blmmmmmmlmmmmlhhhb.......................",
            "......................avbmmmmmmmmmmmmmmmb.......................",
            "........................bemmmmmmmmmmddmmd.......................",
            "........................cdmmmmmmmmmdildb........................",
            ".......................dqmdmmmmmdddmlmmj........................",
            ".......................csmmdmmddd..moomj........................",
            ".......................dmmmdmd....bottmj........................",
            "........................jlcvd.....aottm.........................",
            "........................jld.......dostm.........................",
            "........................old........hmsj.........................",
            ".......................eslme.......bmsj.........................",
            ".......................esmc........dmsc.........................",
            ".......................ccc.........dsssd........................",
            "...................................esmstlj......................",
            "...................................ettoooj......................",
            "...................................cee..........................",
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
            ".........................dd.........dd..........................",
            "........................gnd.........dkg.........................",
            ".......................fnd...........dkd........................",
            ".......................nkd...uuuuu...dkd........................",
            ".......................nkdvvuyyyyyuvdkkd........................",
            ".......................dnkduyyyyyyyrkkid........................",
            "........................dkkdyyyyyyydggd.........................",
            ".........................bgdxyyyyyywgd..........................",
            ".........................pdwxyyyyyxwd...........................",
            ".........................epwyyyyyywwc...........................",
            ".........................emwyywyyxrrhe..........................",
            "..........................mwyywywryyxu..........................",
            "..........................pyyywyvyyyyxu.........................",
            "..........................uyyyyyryyyywu.........................",
            "...................a....bdvyyyyyryxyyxxb...va...................",
            "...................dvvdfppdywyyyrywyxyrppbvu....................",
            "....................uufvuppfwyywrwywxyrpuugb....................",
            ".....................dpvvppnwywwuwywwyxpvppb....................",
            "....................aprpppkndywwrwywvyknnprkb...................",
            "...................bnnppnkuvdccwwwywuygunnnuub..................",
            "...................guvuuuuddlllewvwyrybfuuudd...................",
            "....................ddddddoossqjggwyxrlcccch....................",
            "....................lommmcmostqllmhw.umcmmosc...................",
            "...................cmtsmmcmmshlmmmmi..lammttm...................",
            "...................cttmmmcmmjemmmmmhr.hammqttd..................",
            "...................htommaclmmmmunlmmmff.bmlstcv.................",
            "................evatttmbvdddfuuurpnbbb...almtsa.................",
            "................nvatoljb..kpvrnrnrnnnr...aommeup................",
            ".................kpjmmmb..annpnkdddddb...aooeub.................",
            ".................bnvmme..bddddddkkkkkb...adcukb.................",
            ".................bkkaaa..bgkkuukkkkukb....bkkka.................",
            "................bnkkka...bdddfdddddffk....bkkkb.................",
            "................gvnkk....fnppuaiihhuppd...aprrb.................",
            ".................acggd..fnnppuaihhhaunf...aaaa..................",
            ".................msmcc.dnnpppvamhhhauupb.emcql..................",
            ".................jseoc.dnuuvvahmmhhhaaab..cmom..................",
            "..................moe...ccccahmmmmhjhhhc...llj..................",
            ".......................bhhhhhhmmmmlmmlhc........................",
            "......................bmmhhmmmmmmmlmmmhjb.......................",
            "......................aodmmmmmmmmmmmmmddmb......................",
            "......................adddmmmmmmmmmmddddda......................",
            "......................avhdcmmmmmmmmeddfeva......................",
            "........................hjcmmmmmmmldlmlc........................",
            ".......................bltmdmmmmmmb.amstd.......................",
            ".......................mttmddmmmdd..amttm.......................",
            ".......................mttmd.dmd....amttm.......................",
            ".......................jommd..d.....dmmoh.......................",
            ".......................dold..........cmmh.......................",
            ".......................aol............mmh.......................",
            "......................jesl............jsmj......................",
            ".....................etqll............esmsc.....................",
            ".....................hoossc...........etsle.....................",
            ".......................ellc...........cll.......................",
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

        // Room round the picture for the sword held overhead or out ahead, and the banner over his head.
        const int Margin = 24;
        const int Headroom = 30;

        // ---- Parts. The arms (forearm and hand, under the shoulder) and the legs of each view are a box each (left,
        // top, right, bottom, inclusive), the left one on the screen first: arm A holds the sword, arm B the banner.
        // Seen from the side only the near arm and leg show; the far arm is drawn from the near one, in shade behind
        // the body, when it moves out from behind. ----

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

            // The middle of the hand, near the bottom of the arm.
            public (int X, int Y) Fist => ((X0 + X1) / 2, Y1 - 3);
        }

        static readonly Box None = new Box(0, 0, -1, -1);

        static (Box ArmA, Box ArmB, Box LegA, Box LegB) BoxesOf(BossView view) => view switch
        {
            BossView.Front => (new Box(15, 29, 23, 43), new Box(40, 29, 47, 41), new Box(20, 48, 29, 58), new Box(33, 48, 44, 57)),
            BossView.FrontSide => (new Box(20, 30, 27, 40), new Box(37, 33, 43, 40), new Box(24, 49, 31, 59), new Box(32, 49, 40, 56)),
            BossView.Side => (new Box(33, 27, 40, 43), None, new Box(27, 51, 38, 59), None),
            BossView.BackSide => (new Box(18, 28, 23, 41), new Box(39, 28, 46, 42), new Box(23, 48, 29, 55), new Box(33, 48, 42, 58)),
            _ => (new Box(16, 28, 22, 41), new Box(40, 28, 47, 41), new Box(20, 48, 28, 57), new Box(35, 48, 43, 57)),
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

        static readonly Rgba Shade = new Rgba(32, 12, 16);
        static readonly Rgba Ink = new Rgba(9, 3, 7);
        static readonly Rgba Skin = new Rgba(133, 48, 51);
        static readonly Rgba SkinLight = new Rgba(183, 76, 72);
        static readonly Rgba SkinDark = new Rgba(77, 28, 37);
        // The greatsword: dark iron with a bright edge, a bronze guard, wrapped grip; its flame.
        static readonly Rgba Steel = new Rgba(150, 140, 136);
        static readonly Rgba SteelLight = new Rgba(214, 206, 196);
        static readonly Rgba SteelDark = new Rgba(66, 56, 58);
        static readonly Rgba Bronze = new Rgba(170, 120, 60);
        static readonly Rgba Grip = new Rgba(52, 30, 26);
        static readonly Rgba Flame = new Rgba(255, 128, 40);
        static readonly Rgba FlameHot = new Rgba(255, 226, 140);
        // The banner: a crimson cloth with a gold hem and a black horned sigil on a dark wooden pole.
        static readonly Rgba Cloth = new Rgba(150, 26, 32);
        static readonly Rgba ClothDark = new Rgba(92, 14, 22);
        static readonly Rgba Hem = new Rgba(196, 152, 64);
        static readonly Rgba Pole = new Rgba(70, 46, 34);
        static readonly Rgba PoleDark = new Rgba(40, 24, 20);
        static readonly Rgba Madness = new Rgba(236, 40, 36);

        // ---- A pose: for each arm how far it is raised (pixels up), carried ahead (pixels, from the side) and drawn in
        // toward the middle of the body (1) or out from it (-1, seen from the front); the steps and lifts of the feet;
        // the body's lean (top ahead), stretch (taller and thinner, or squashed), lift off the ground, how far it is
        // carried ahead (shove) and dropped (drop); the sword's angle (degrees: 0 pointing ahead, 90 straight up, -90
        // point down, 180 behind); whether the banner is in his hand (over 0.5) or on his back. ----

        struct Pose
        {
            public float RaiseA, AheadA, InA, RaiseB, AheadB, InB;
            public float StepA, LiftA, StepB, LiftB, Lean, Stretch, Lift, Shove, Drop, Sword, Banner;

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
        }

        static Pose P(float sword = -55, float lean = 0, float stretch = 0, float stepA = 0, float stepB = 0, float liftA = 0, float liftB = 0,
            float drop = 0, float shove = 0, float lift = 0, float banner = 0) => new Pose
        {
            Sword = sword, Lean = lean, Stretch = stretch, StepA = stepA, StepB = stepB, LiftA = liftA, LiftB = liftB, Drop = drop,
            Shove = shove, Lift = lift, Banner = banner, AheadA = 2,
        };

        // At rest: the sword low at his side, point down and a little ahead.
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
                Drop = L(a.Drop, b.Drop), Sword = L(a.Sword, b.Sword), Banner = L(a.Banner, b.Banner),
            };
        }

        // Key poses at times 0..1, eased in and out between each pair.
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
        // behind the blade), the strike, the rebound, held (the opening to hit back), hauled back, back to rest; and
        // the slow parts of the entrance. Quicker than the black knight's: a commander's blows, not a hammer's.
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
            Stage.Swing => 0.035f,
            Stage.Strike => 0.08f,
            Stage.Rebound => 0.07f,
            Stage.Held => 0.12f,
            Stage.Haul => 0.08f,
            Stage.Slow => 0.09f,
            _ => 0.06f,
        };

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

        // The greatsword swung up behind his head, leaning back, the free hand out ahead to aim.
        static readonly Pose WindTop = P(130, lean: -4, stretch: 0.05f, stepA: 2, stepB: -2).A(14, -8, -0.2f).B(4, 5, 0.3f);
        static readonly Pose CleaveHit = P(-35, lean: 7, stretch: -0.1f, stepA: 3, stepB: -3, drop: 2, shove: 3).A(2, 12, 0.3f).B(2, 6, 0.2f);
        // The sword thrust up to the sky, the free hand pointing ahead: the order is given.
        static readonly Pose OrderTop = P(95, lean: -4, stretch: 0.08f, stepA: 2, stepB: -2).A(28, 2, 0.1f).B(5, 8, -0.3f);
        static readonly Pose CommandHit = P(5, lean: 4, stepA: 3, stepB: -2, shove: 2).A(10, 14).B(2, 4);
        // Both hands on the hilt overhead, the point down.
        static readonly Pose HeftTop = P(-90, lean: -3, stretch: 0.08f, stepA: 1, stepB: -1).A(26, 4, 0.45f).B(26, 4, 0.45f);
        static readonly Pose StabHit = P(-82, lean: 6, stretch: -0.14f, stepA: 2, stepB: -2, drop: 3).A(-2, 8, 0.45f).B(-2, 8, 0.45f);
        // The banner pulled from his back and held high.
        static readonly Pose HoistTop = P(-60, lean: -3, stretch: 0.07f, stepA: 1, stepB: -1, banner: 1).B(26, 3, 0.2f);
        static readonly Pose PlantHit = P(-60, lean: 5, stretch: -0.12f, stepA: 2, stepB: -2, drop: 3, banner: 1).B(-4, 8, 0.2f);
        // Crouched, the blade drawn far back level, ready to spin.
        static readonly Pose CoilTop = P(180, lean: 3, stretch: -0.1f, stepA: -2, stepB: 2, drop: 2).A(4, -10, -0.2f).B(4, 6, 0.3f);
        static readonly Pose SpinOut = P(0, lean: 2, stretch: -0.06f, stepA: 2, stepB: -2, drop: 1).A(8, 12).B(6, -6, -0.2f);
        // The sword levelled like a lance, head down.
        static readonly Pose Braced = P(-5, lean: 9, stretch: -0.1f, stepA: 4, stepB: -4, drop: 2).A(4, 10, 0.2f).B(3, 4, 0.2f);
        // The banner high over his head, to call the soldiers.
        static readonly Pose RallyTop = P(-60, lean: -4, stretch: 0.08f, stepA: 2, stepB: -2, banner: 1).B(30, 0, 0.1f);
        static readonly Pose RoarTop = P(85, lean: -5, stretch: 0.09f, stepA: 2, stepB: -2).A(26, 0, -0.3f).B(14, -2, -0.8f);

        // The great overhand cleave: over his head, down ahead to the floor, held there, hauled up, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Cleave = Expand(WindTop,
            (Stage.Swing, 2, P(100, lean: -1, stretch: 0.06f).A(26, 0, 0.2f).B(4, 6, 0.3f)),
            (Stage.Swing, 2, P(30, lean: 4).A(14, 10, 0.3f).B(3, 6, 0.2f)),
            (Stage.Strike, 1, CleaveHit),
            (Stage.Rebound, 1, P(-30, lean: 6, stretch: -0.06f, stepA: 3, stepB: -3, drop: 1, shove: 3).A(3, 12, 0.3f).B(2, 6, 0.2f)),
            (Stage.Held, 3, CleaveHit),
            (Stage.Haul, 4, P(-45, lean: 3, shove: 1).A(1, 7).B(1, 2)),
            (Stage.Return, 8, Rest));

        // The sword swept down from the sky to point at where the fire falls.
        static readonly (Pose[] Frames, Stage[] Stages) Command = Expand(OrderTop,
            (Stage.Swing, 2, P(55, lean: -1, stretch: 0.04f, stepA: 2, stepB: -2).A(20, 9).B(4, 7, -0.2f)),
            (Stage.Strike, 1, CommandHit),
            (Stage.Rebound, 1, P(10, lean: 3, stepA: 3, stepB: -2, shove: 2).A(11, 13).B(2, 4)),
            (Stage.Held, 4, CommandHit),
            (Stage.Return, 10, Rest));

        // Driven point-first into the floor before him.
        static readonly (Pose[] Frames, Stage[] Stages) Stab = Expand(HeftTop,
            (Stage.Swing, 3, P(-88, lean: 2, stretch: 0.02f).A(10, 6, 0.45f).B(10, 6, 0.45f)),
            (Stage.Strike, 1, StabHit),
            (Stage.Rebound, 1, P(-82, lean: 5, stretch: -0.08f, stepA: 2, stepB: -2, drop: 2).A(-1, 8, 0.45f).B(-1, 8, 0.45f)),
            (Stage.Held, 4, StabHit),
            (Stage.Haul, 5, P(-80, lean: 2).A(6, 6, 0.3f).B(4, 4, 0.2f)),
            (Stage.Return, 8, Rest));

        // The banner's pole driven into the floor at his feet; it goes back on his back.
        static readonly (Pose[] Frames, Stage[] Stages) Plant = Expand(HoistTop,
            (Stage.Swing, 3, P(-60, lean: 2, banner: 1).B(10, 6, 0.2f)),
            (Stage.Strike, 1, PlantHit),
            (Stage.Rebound, 1, P(-60, lean: 4, stretch: -0.07f, drop: 2, banner: 1).B(-3, 8, 0.2f)),
            (Stage.Held, 5, PlantHit),
            (Stage.Haul, 7, P(-55, lean: 1, banner: 1).B(14, 2, 0.2f)),
            (Stage.Return, 8, Rest));

        // Spun round with the blade out level (WorldView turns him a view a frame), then back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) Spin = Expand(CoilTop,
            (Stage.Swing, 2, SpinOut),
            (Stage.Strike, 1, SpinOut),
            (Stage.Held, 14, SpinOut),
            (Stage.Return, 8, Rest));

        // ---- The entrance: kneeling before his sword planted point-down, head bowed; woken, he rises, pulls the sword
        // from the floor and raises it with a roar. ----

        static readonly Pose Kneel = P(-90, lean: 5, stretch: -0.24f, stepA: 3, stepB: -3, drop: 7).A(4, 8, 0.45f).B(4, 8, 0.45f);
        static readonly Pose Stood = P(-90).A(4, 8, 0.45f).B(4, 8, 0.45f);

        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Kneel,
            // A shudder through the shoulders.
            (Stage.Slow, 3, P(-90, lean: 4, stretch: -0.22f, stepA: 3, stepB: -3, drop: 6).A(5, 8, 0.45f).B(5, 8, 0.45f)),
            (Stage.Slow, 3, Kneel),
            // Up off his knee, onto his feet.
            (Stage.Slow, 10, Stood),
            // The sword pulled from the floor and raised high.
            (Stage.Slow, 8, RoarTop),
            // The roar.
            (Stage.Slow, 6, P(85, lean: -6, stretch: 0.1f, stepA: 2, stepB: -2).A(27, 0, -0.3f).B(15, -2, -0.8f)),
            // The sword brought down to his side, ready.
            (Stage.Slow, 10, Rest));

        static readonly Pose[] EnterFrames = new[] { Kneel }.Concat(Entrance.Frames).ToArray();
        static readonly Stage[] EnterStages = new[] { Stage.Slow }.Concat(Entrance.Stages).ToArray();
        // The frames he stands on his feet, pulls the sword free and roars (seen before, the entrance stops on his feet).
        public const int EnterStood = 16;
        public const int EnterDrawn = 20;
        public const int EnterRoar = 24;

        public static (int From, int To) EnterSpan(bool again) => (again ? 6 : 0, again ? EnterStood : EnterFrames.Length - 1);

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

        // ---- Frames. ----

        // The frames a roar and a rally take to rise, before they shake there (frames RoarRise.. loop).
        public const int RoarRise = 10;
        public const int RallyRise = 18;

        static (Pose[] Frames, Stage[] Stages)? BlowOf(GeneralPose pose) => pose switch
        {
            GeneralPose.Cleave => Cleave,
            GeneralPose.Command => Command,
            GeneralPose.Stab => Stab,
            GeneralPose.Plant => Plant,
            GeneralPose.Spin => Spin,
            _ => null,
        };

        static Stage[] StagesOf(GeneralPose pose) => pose == GeneralPose.Enter ? EnterStages : BlowOf(pose)?.Stages;

        public static int FramesOf(GeneralPose pose) => pose switch
        {
            GeneralPose.Idle => 4,
            GeneralPose.Walk => 8,
            GeneralPose.Wind => 12,
            GeneralPose.Order => 14,
            GeneralPose.Heft => 12,
            GeneralPose.Hoist => 18,
            GeneralPose.Coil => 10,
            GeneralPose.Brace => 8,
            GeneralPose.Charge => 8,
            GeneralPose.Rally => RallyRise + 4,
            GeneralPose.Roar => RoarRise + 4,
            GeneralPose.Enter => EnterFrames.Length,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        public static float FrameSeconds(GeneralPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(GeneralPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && pose != GeneralPose.Enter && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(GeneralPose pose) => pose == GeneralPose.Enter ? -1 : System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The frames of the spin that turn him round (one view a frame).
        public static bool Spinning(GeneralPose pose, int frame) => pose == GeneralPose.Spin && frame <= StrikeFrame(pose) + 14;

        public static GeneralPose WindUpOf(GeneralPose blow) => blow switch
        {
            GeneralPose.Cleave => GeneralPose.Wind,
            GeneralPose.Command => GeneralPose.Order,
            GeneralPose.Stab => GeneralPose.Heft,
            GeneralPose.Plant => GeneralPose.Hoist,
            GeneralPose.Spin => GeneralPose.Coil,
            _ => GeneralPose.Wind,
        };

        // How the body turns with the motion, a step of the eight views: away from the viewer as the sword is drawn
        // back, round toward the viewer as the cleave comes over.
        public static int TwistOf(GeneralPose pose, int frame) => pose switch
        {
            GeneralPose.Wind or GeneralPose.Coil => frame >= FramesOf(pose) / 3 ? -1 : 0,
            GeneralPose.Cleave => frame == 0 ? -1 : 0,
            _ => 0,
        };

        static Pose PoseOf(GeneralPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case GeneralPose.Idle:
                {
                    // Breathing hard: the shoulders heave, the blade's point stirs.
                    float b = 0.5f - 0.5f * c;
                    var p = P(-55 + 3 * b, stretch: 0.02f * b);
                    return p.A(b, 2).B(b, 0);
                }
                case GeneralPose.Walk:
                    // A long, impatient stride, the sword swinging low against the step.
                    return P(-45 + 6 * s, lean: 2, stretch: -0.02f * (1 - System.Math.Abs(c)), stepA: 4 * s, stepB: -4 * s, drop: 1 - System.Math.Abs(c),
                        liftA: 2.5f * System.Math.Max(0, c), liftB: 2.5f * System.Math.Max(0, -c)).A(1, 2 - 3 * s).B(0.5f, 3 * s);
                case GeneralPose.Wind:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(60, lean: -2, stretch: 0.03f, stepA: 1, stepB: -1).A(16, -2).B(3, 4, 0.2f)),
                        (1, WindTop));
                case GeneralPose.Order:
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(40, lean: -1, stretch: 0.03f, stepA: 1, stepB: -1).A(14, 6).B(3, 6, -0.2f)),
                        (1, OrderTop));
                case GeneralPose.Heft:
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(-85, stretch: 0.02f).A(10, 6, 0.4f).B(8, 6, 0.4f)),
                        (1, HeftTop));
                case GeneralPose.Hoist:
                    // A hand over his shoulder to the banner's pole, then the banner pulled free and raised.
                    if (t < 0.35f)
                        return Keys(t / 0.35f, (0, Rest), (1, P(-60, lean: -1).B(22, -6, 0.6f)));
                    return Keys((t - 0.35f) / 0.65f, (0, P(-60, lean: -1, banner: 1).B(22, -6, 0.6f)), (1, HoistTop));
                case GeneralPose.Coil:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(120, lean: 1, stretch: -0.05f, stepA: -1, stepB: 1, drop: 1).A(8, -6).B(3, 5, 0.2f)),
                        (1, CoilTop));
                case GeneralPose.Brace:
                    return Keys(t,
                        (0, Rest),
                        (0.45f, P(-30, lean: 4, stretch: -0.05f, stepA: 2, stepB: -2, drop: 1).A(2, 7, 0.1f).B(2, 3)),
                        (1, Braced));
                case GeneralPose.Charge:
                    return P(-5 + 3 * s, lean: 11, stretch: -0.1f, stepA: 6 * s, stepB: -6 * s, drop: 1 - System.Math.Abs(c),
                        liftA: 3 * System.Math.Max(0, c), liftB: 3 * System.Math.Max(0, -c)).A(4 + s, 10, 0.2f).B(3 - s, 4, 0.2f);
                case GeneralPose.Rally:
                    // The banner hauled up over his head, then waved back and forth.
                    if (f < RallyRise)
                    {
                        float u = f / (float)(RallyRise - 1);
                        if (u < 0.35f) return Keys(u / 0.35f, (0, Rest), (1, P(-60, lean: -1).B(22, -6, 0.6f)));
                        return Keys((u - 0.35f) / 0.65f, (0, P(-60, lean: -1, banner: 1).B(22, -6, 0.6f)), (1, RallyTop));
                    }
                    return RallyTop.B(30, 6 * s, 0.1f);
                case GeneralPose.Roar:
                    if (f < RoarRise)
                        return Keys(f / (float)RoarRise,
                            (0, Rest),
                            (0.5f, P(40, lean: -2, stretch: 0.04f, stepA: 1, stepB: -1).A(16, 4).B(8, 0, -0.5f)),
                            (1, RoarTop));
                    return P(85 + 2 * s, lean: -5, stretch: 0.09f, stepA: 2, stepB: -2).A(26 + s, 0, -0.3f).B(14 - s, -2, -0.8f);
                case GeneralPose.Enter:
                    return EnterFrames[f];
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        // Where an arm's hand goes for a pose: up by its raise; ahead to the right from the side (a little less from
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

        // Which way "ahead" points on the screen for each view (up is up): the sword's angle is turned by it.
        static (float X, float Y) Ahead(BossView view) => view switch
        {
            BossView.Side => (1, 0),
            BossView.FrontSide => (0.8f, 0.3f),
            BossView.Front => (-0.35f, 0.42f),
            BossView.BackSide => (0.8f, -0.25f),
            _ => (-0.3f, -0.3f),
        };

        const float BladeLength = 29;

        // The sword's hilt and tip for a pose (canvas pixels), from the hand holding it.
        static ((float X, float Y) Hilt, (float X, float Y) Tip) SwordOf(BossView view, float angle, (float X, float Y) hand)
        {
            var (fx, fy) = Ahead(view);
            double a = angle * System.Math.PI / 180;
            float dx = (float)(fx * System.Math.Cos(a)), dy = (float)(fy * System.Math.Cos(a) - System.Math.Sin(a));
            return (hand, (hand.X + dx * BladeLength, hand.Y + dy * BladeLength));
        }

        // The flaming greatsword from its hilt to its tip: a wrapped grip behind the hand, a bronze guard across, a
        // broad dark blade with a bright edge, flame licking along it (flickering by frame; white-hot when mad).
        static void DrawSword(PixelImage canvas, (float X, float Y) hilt, (float X, float Y) tip, int frame, bool mad)
        {
            float dx = tip.X - hilt.X, dy = tip.Y - hilt.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.5f) return;
            float ux = dx / length, uy = dy / length;
            void Put(float x, float y, Rgba c)
            {
                int px = R(x), py = R(y);
                if (canvas.Inside(px, py)) canvas[px, py] = c;
            }
            void Glow(float x, float y, Rgba c)
            {
                int px = R(x), py = R(y);
                if (canvas.Inside(px, py) && canvas[px, py].A == 0) canvas[px, py] = c;
            }
            // Flame round the blade first, so the blade covers its inside.
            var flame = mad ? FlameHot : Flame;
            for (float d = 3; d <= length + 2; d += 0.5f)
            {
                float u = d / System.Math.Max(1, length);
                float lick = 2.2f + 1.6f * Hash(R(d), frame);
                for (float o = -lick; o <= lick; o += 0.5f)
                {
                    float x = hilt.X + ux * d - uy * o, y = hilt.Y + uy * d + ux * o;
                    float edge = System.Math.Abs(o) / lick;
                    // Flames rise: the side facing up licks further.
                    Glow(x, y - (1 - edge) * 1.5f * Hash(frame, R(d)), flame.WithAlpha((int)(170 * (1 - edge) * (0.5f + 0.5f * u))));
                }
            }
            // The grip behind the hand, and the pommel.
            for (float d = -5; d <= 0; d += 0.5f)
            {
                Put(hilt.X + ux * d, hilt.Y + uy * d, Grip);
                Put(hilt.X + ux * d - uy * 0.6f, hilt.Y + uy * d + ux * 0.6f, Grip);
            }
            Put(hilt.X - ux * 5.5f, hilt.Y - uy * 5.5f, Bronze);
            // The blade, broad at the guard and tapering to the point.
            for (float d = 2; d <= length; d += 0.5f)
            {
                float u = d / length;
                float half = 2.1f - 1.5f * u * u;
                for (float o = -half - 0.6f; o <= half + 0.6f; o += 0.5f)
                {
                    float x = hilt.X + ux * d - uy * o, y = hilt.Y + uy * d + ux * o;
                    float edge = System.Math.Abs(o);
                    var c = edge > half ? Ink : edge > half - 0.6f ? (o * ux < 0 ? SteelLight : SteelDark) : System.Math.Abs(o) < 0.4f ? SteelDark : Steel;
                    if (mad && edge <= half) c = c.Mix(FlameHot, 0.35f);
                    Put(x, y, c);
                }
            }
            // The guard across, at the hand.
            for (float o = -4; o <= 4; o += 0.5f)
            {
                Put(hilt.X + ux * 1.5f - uy * o, hilt.Y + uy * 1.5f + ux * o, System.Math.Abs(o) > 3.4f ? Ink : Bronze);
                Put(hilt.X + ux * 2f - uy * o, hilt.Y + uy * 2f + ux * o, System.Math.Abs(o) > 3.4f ? Ink : Bronze.Scale(0.7f));
            }
        }

        // The war banner on its pole from the foot to the top: the cloth flies from the top toward "behind" (-1 left,
        // 1 right), ragged at the bottom, rippling by frame, a horned sigil on it.
        static void DrawBanner(PixelImage canvas, (float X, float Y) foot, (float X, float Y) top, int behind, int frame)
        {
            float dx = top.X - foot.X, dy = top.Y - foot.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            for (float d = 0; d <= length; d += 0.5f)
            {
                float x = foot.X + dx * d / length, y = foot.Y + dy * d / length;
                if (canvas.Inside(R(x), R(y))) canvas[R(x), R(y)] = Pole;
                if (canvas.Inside(R(x) + 1, R(y))) canvas[R(x) + 1, R(y)] = PoleDark;
            }
            // A gold spearhead on top.
            for (int k = 1; k <= 3; k++)
                for (int o = -(3 - k) / 2; o <= (3 - k + 1) / 2; o++)
                    if (canvas.Inside(R(top.X) + o, R(top.Y) - k)) canvas[R(top.X) + o, R(top.Y) - k] = Hem;
            const int ClothWide = 12, ClothTall = 16;
            for (int i = 1; i <= ClothWide; i++)
            {
                int ragged = ClothTall - (int)(4 * Hash(i, 7)) - (i % 3 == 0 ? 2 : 0);
                // The far edge ripples more than the edge at the pole.
                float ripple = (float)System.Math.Sin(frame * 0.9f + i * 0.7f) * 1.4f * i / ClothWide;
                for (int j = 0; j < ragged; j++)
                {
                    int x = R(top.X + behind * i), y = R(top.Y + 1 + j + ripple);
                    if (!canvas.Inside(x, y)) continue;
                    bool hem = j < 2;
                    bool sigil = j >= 5 && j <= 10 && (i == 5 || i == 8) || j == 7 && i >= 5 && i <= 8 || (j == 4 && (i == 4 || i == 9));
                    var c = hem ? Hem : sigil ? Ink : (i + j + frame / 2) % 7 == 0 ? ClothDark : j >= ragged - 2 ? ClothDark : Cloth;
                    // Folds: every few columns a darker crease that moves with the ripple.
                    if (!hem && !sigil && ((i + frame) % 4 == 0)) c = c.Mix(ClothDark, 0.5f);
                    canvas[x, y] = i == ClothWide || j == ragged - 1 ? Ink.Mix(c, 0.3f) : c;
                }
            }
        }

        // Where the banner stands on his back for each view: the foot of the pole behind the shoulders, the top well
        // over his head, and which way the cloth flies.
        static ((float X, float Y) Foot, (float X, float Y) Top, int Behind) BackBanner(BossView view) => view switch
        {
            BossView.Side => ((29 + Margin, 30 + Headroom), (27 + Margin, -6 + Headroom), -1),
            BossView.FrontSide => ((29 + Margin, 30 + Headroom), (26 + Margin, -6 + Headroom), -1),
            BossView.Front => ((36 + Margin, 30 + Headroom), (37 + Margin, -6 + Headroom), 1),
            BossView.BackSide => ((30 + Margin, 30 + Headroom), (28 + Margin, -6 + Headroom), -1),
            _ => ((30 + Margin, 30 + Headroom), (30 + Margin, -6 + Headroom), 1),
        };

        // The picture with each part at its place, the sword and the banner, on a canvas with room round it; and where
        // the two hands and the sword's tip are.
        static PixelImage Compose(BossView view, Pose pose, int frame, bool mad, out (float X, float Y) fistA, out (float X, float Y) fistB, out (float X, float Y) tip)
        {
            var image = ViewOf(view);
            var parts = PartsOf(view);
            var (armA, armB, legA, legB) = BoxesOf(view);
            int w = image.Width, h = image.Height;
            bool side = view != BossView.Front && view != BossView.Back;
            bool back = view == BossView.Back || view == BossView.BackSide;
            var (ax, ay) = ArmShift(view, armA, pose.RaiseA, pose.AheadA, pose.InA);
            var farBox = armB.Empty ? armA : armB;
            var (bx, by) = ArmShift(view, farBox, pose.RaiseB, pose.AheadB, pose.InB);
            float stepScale = view == BossView.Side ? 1 : side ? 0.7f : 0;
            int la = R(pose.StepA * stepScale), lb = R(pose.StepB * stepScale);
            int lay = R(-pose.LiftA - (side ? 0 : System.Math.Max(0, pose.StepA) * 0.5f));
            int lby = R(-pose.LiftB - (side ? 0 : System.Math.Max(0, pose.StepB) * 0.5f));
            fistA = (Margin + armA.Fist.X + ax, Headroom + armA.Fist.Y + ay);
            fistB = armB.Empty ? (Margin + armA.Fist.X + bx - 4, Headroom + armA.Fist.Y + by - 1) : (Margin + armB.Fist.X + bx, Headroom + armB.Fist.Y + by);
            var sword = SwordOf(view, pose.Sword, fistA);
            tip = sword.Tip;
            bool held = pose.Banner > 0.5f;
            var banner = BackBanner(view);
            // The held banner: the pole through the hand, the cloth flying behind him.
            var heldFoot = (fistB.X - (side ? 2 : 0), fistB.Y + 10);
            var heldTop = (fistB.X + (side ? 3 : 0), fistB.Y - 26);

            var canvas = new PixelImage(w + 2 * Margin, h + Headroom);
            bool Far(int dx, int dy) => System.Math.Abs(dx) >= 4 || System.Math.Abs(dy) >= 4;
            // Behind him: the banner on his back seen from ahead, the sword when he is seen from behind, the far arm
            // seen from the side when it comes out from behind.
            if (!held && !back) DrawBanner(canvas, banner.Foot, banner.Top, banner.Behind, frame);
            if (back) DrawSword(canvas, sword.Hilt, sword.Tip, frame, mad);
            if (held && back) DrawBanner(canvas, heldFoot, heldTop, view == BossView.Back ? 1 : -1, frame);
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
                    int left = -1, right = -1;
                    for (int k = x - 1; k >= 0 && left < 0; k--) if (Body(k)) left = k;
                    for (int k = x + 1; k < w && right < 0; k++) if (Body(k)) right = k;
                    if (left >= 0 && right >= 0)
                    {
                        // The body's own texture mirrored in from the nearer side (2026-11-27, not a flat shade).
                        canvas[x + Margin, y + Headroom] = image.MirrorInto(x, y, x - left <= right - x ? left : right, Body);
                    }
                }
            }
            void Lay(Part part, int dx, int dy, float dim)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (parts[x, y] == part) canvas[x + Margin + dx, y + Headroom + dy] = dim < 1 ? image[x, y].Scale(dim) : image[x, y];
            }
            // An arm moved far is drawn anew: a red upper arm from the shoulder (the top of its box) to the hand, and
            // the hand (the bottom of the box) there.
            void Arm(Part part, Box box, int dx, int dy, float dim)
            {
                if (box.Empty) return;
                if (!Far(dx, dy))
                {
                    Lay(part, dx, dy, dim);
                    return;
                }
                var fist = box.Fist;
                float sx = (box.X0 + box.X1) / 2f + Margin, sy = box.Y0 + Headroom - 2;
                bool up = fist.Y + dy < box.Y0 - 2;
                float ex = fist.X + dx + Margin, ey = (up ? fist.Y + 1 : fist.Y - 3) + dy + Headroom;
                float length = (float)System.Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
                float ux = (ex - sx) / System.Math.Max(1, length), uy = (ey - sy) / System.Math.Max(1, length);
                for (float d = 0; d <= length; d += 0.5f)
                {
                    float width = 3.4f - 0.8f * d / System.Math.Max(1, length);
                    for (float o = -width; o <= width; o += 0.5f)
                    {
                        int x = R(sx + ux * d - uy * o), y = R(sy + uy * d + ux * o);
                        if (!canvas.Inside(x, y)) continue;
                        float edge = System.Math.Abs(o);
                        bool lit = System.Math.Abs(ux) > 0.3f ? ux * o < 0 : uy * o < 0;
                        // A dark bracer near the hand.
                        bool bracer = d > length - 4;
                        var c = edge > width - 0.7f ? Ink : bracer ? (lit ? Bronze.Scale(0.8f) : SteelDark) : lit ? (edge > width - 1.6f ? Skin : SkinLight) : edge < 1 ? Skin : SkinDark;
                        canvas[x, y] = dim < 1 ? c.Scale(dim) : c;
                    }
                }
                for (int y = System.Math.Max(box.Y0, box.Y1 - 6); y <= box.Y1; y++)
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
            if (held && !back) DrawBanner(canvas, heldFoot, heldTop, view == BossView.Front ? 1 : -1, frame);
            if (!back) DrawSword(canvas, sword.Hilt, sword.Tip, frame, mad);
            Arm(Part.ArmA, armA, ax, ay, 1);
            if (!held && back) DrawBanner(canvas, banner.Foot, banner.Top, banner.Behind, frame);
            return canvas;
        }

        // His skin: the reds of the picture, flushed with madness in the last phase.
        static bool IsSkin(Rgba c) => c.R > 70 && c.R > 2 * c.G && c.R > c.B + 20;

        // One frame. In the last phase his skin flushes and his blade burns white-hot. Faces right; WorldView flips it
        // for the left.
        public static PixelImage Draw(BossView view, GeneralPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        public struct Joints
        {
            public float FistAX, FistAY, FistBX, FistBY, TipX, TipY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, GeneralPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        // Where the sword's tip is in a frame (picture pixels, facing right), for WorldView's sparks off the blade.
        public static (float X, float Y) TipOf(BossView view, GeneralPose pose, int frame)
        {
            var j = JointsOf(view, pose, frame);
            return (j.TipX, j.TipY);
        }

        static PixelImage Render(BossView view, GeneralPose pose, int frame, int phase, out Joints joints)
        {
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            var p = PoseOf(pose, frame);
            bool side = view != BossView.Front && view != BossView.Back;
            bool mad = phase >= 3;
            var body = Compose(view, p, frame, mad, out var fistA, out var fistB, out var tip);
            float lean = p.Lean, stretch = p.Stretch, shove = p.Shove, drop = p.Drop;
            if (!side)
            {
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
            var t = Place(tip.X, tip.Y);
            joints.FistAX = a.X;
            joints.FistAY = a.Y;
            joints.FistBX = b.X;
            joints.FistBY = b.Y;
            joints.TipX = t.X;
            joints.TipY = t.Y;
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
                    if (mad && c.A == 255 && IsSkin(c)) c = c.Mix(Madness, 0.3f);
                    int px = left + x + shift;
                    image[px, row] = c;
                    if (c.A < 255) continue;
                    joints.Top = System.Math.Min(joints.Top, row);
                    joints.Bottom = System.Math.Max(joints.Bottom, row);
                    joints.Left = System.Math.Min(joints.Left, px);
                    joints.Right = System.Math.Max(joints.Right, px);
                }
            }
            // A blade too fast to see: a fiery streak from where its tip was a frame before.
            if (Swinging(pose, frame) && frame > 0 && pose != GeneralPose.Spin)
            {
                Compose(view, PoseOf(pose, frame - 1), frame - 1, mad, out _, out _, out var was);
                Streak(image, Place(was.X, was.Y), t, mad);
            }
            return image;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65536f;
        }

        static void Streak(PixelImage image, (float X, float Y) from, (float X, float Y) to, bool mad)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (length < 5) return;
            float ux = dx / length, uy = dy / length;
            for (float d = 0; d <= length; d += 0.5f)
            {
                float u = d / length;
                float width = 1 + 3 * u;
                for (float o = -width; o <= width; o += 0.5f)
                {
                    int x = R(from.X + ux * d - uy * o), y = R(from.Y + uy * d + ux * o);
                    if (!image.Inside(x, y) || image[x, y].A > 0) continue;
                    image[x, y] = System.Math.Abs(o) < width * 0.4f ? (mad ? FlameHot : new Rgba(255, 200, 120)).WithAlpha((int)(120 + 110 * u)) : Flame.WithAlpha((int)(60 + 90 * u));
                }
            }
        }
    }
}
