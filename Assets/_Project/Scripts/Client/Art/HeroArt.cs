using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // What the hero holds, which decides how they stand and strike (HeroArt). A pickaxe is the only one that digs rock.
    enum HeroWeapon : byte
    {
        None,
        Sword,
        Bow,
        Staff,
        Pick,
    }

    // What the hero is doing. The three attacks are the steps of the basic-attack combo (World.ComboSteps) and differ
    // by weapon (HeroArt.ClipOf): a sword cuts down, cuts back up and then turns right round in a wide sweep; a bow
    // draws and looses, the third drawn further; a staff is raised and thrust, the third whirled overhead first; a
    // pickaxe chops, swings across and slams; bare hands jab, cross and swing an uppercut.
    enum HeroMove : byte
    {
        Stand,
        Walk,
        Attack1,
        Attack2,
        Attack3,
        // Arms raised: a buff, a heal, a summons.
        Cast,
        // Sent forward: a thrown skill, a dash, a poke at something placed with a bow or staff.
        Thrust,
        // All round: arms flung wide through a full turn.
        Spread,
        Drink,
        Hurt,
        // Knees give, down on the floor (the last frame is held while they lie there); and getting up again.
        Fall,
        Rise,
        // The dodge roll (World.Roll.cs), the same rule for everyone and a different look by what they hold: with a
        // sword, a pickaxe or bare hands a tumble over the floor, with a bow a low slide, with a staff a gliding step
        // that leaves afterimages (WorldView). It is drawn facing the way they roll and lasts as long as the roll.
        Roll,
    }

    // The weapon as the hero's picture needs it: its kind and the colours of its icon (ItemArt.Held).
    readonly struct HeroGear
    {
        public HeroGear(HeroWeapon kind, string key, Rgba light, Rgba main, Rgba shade, Rgba gem, Rgba gemDark)
        {
            Kind = kind;
            Key = key;
            Light = light;
            Main = main;
            Shade = shade;
            Gem = gem;
            GemDark = gemDark;
        }

        public HeroWeapon Kind { get; }
        // Names the colours, for a sprite's key.
        public string Key { get; }
        public Rgba Light { get; }
        public Rgba Main { get; }
        public Rgba Shade { get; }
        public Rgba Gem { get; }
        public Rgba GemDark { get; }

        public static readonly HeroGear None = new HeroGear(HeroWeapon.None, "none", default, default, default, default, default);

        // A plain iron one of a kind, for previews.
        public static HeroGear Plain(HeroWeapon kind)
        {
            var metal = Ramp.Of("#B4BECC");
            return kind == HeroWeapon.None ? None : new HeroGear(kind, "plain_" + kind, metal.Light, metal.Main, metal.Shade, Rgba.Hex("#C88CFF"), Rgba.Hex("#7E46D0"));
        }
    }

    // The player's hero (2026-11-19, the person: "draw the hero with RD too, and add motions by weapon"). The head and
    // the trunk of each of five views are a Retro Diffusion drawing (rd_pro__topdown 32 px and its eight-way rotation,
    // Docs/ArtSources.md) brought to a fixed palette, its tunic repainted in each player's colour. The arms, legs and
    // the weapon are drawn by code over a small rig, so every motion is made once, as key poses in the hero's own
    // terms (to their right, ahead, up), and comes out in all eight ways with the weapon pointing where they strike:
    //  - the body turns (the trunk's and the head's view follow the turn), leans, drops, hops and steps;
    //  - the hands are placed, the arms drawn shoulder to hand, the weapon drawn from the hand it is held in, in
    //    front of the body or behind it by where it points;
    //  - key poses are eased frame to frame in stages, each frame shown for its stage's time (Clip).
    // The picture is a 48-pixel canvas with the feet at FeetX, FeetY, as drawn at one pixel a world pixel: the figure
    // is 12 wide and 18 tall, the size of the world's other people (PeopleArt), the room round it is for the weapon.
    // (2026-11-19, the person: "the character is too big, make it about an NPC's size": the 32-pixel drawing was
    // redrawn by hand at two thirds its size, the same head, hair, tunic and belt, and the rig brought down with it:
    // the poses are still written at the drawing's first size and scaled where they are placed, Solve.)
    static partial class HeroArt
    {
        public const int Size = 48;
        // The canvas pixel the feet stand on: the middle of the bottom of the outline under the boots.
        public const int FeetX = 24;
        public const int FeetY = 36;
        // The figure's height, outline and all (pixels).
        public const int Tall = 18;

        static readonly Rgba Ink = Rgba.Hex("#140A0E");

        static readonly Dictionary<char, Rgba> Colors = new Dictionary<char, Rgba>
        {
            ['h'] = Rgba.Hex("#49281F"),
            ['H'] = Rgba.Hex("#6B412F"),
            ['s'] = Rgba.Hex("#E7AF86"),
            ['S'] = Rgba.Hex("#DA9D76"),
            ['r'] = Rgba.Hex("#BF9178"),
            ['e'] = Ink,
            ['b'] = Rgba.Hex("#38282A"),
            ['p'] = Rgba.Hex("#2B221F"),
            ['o'] = Rgba.Hex("#503025"),
            ['O'] = Rgba.Hex("#704F44"),
        };

        // ---- The drawing: the head (eight rows) and the trunk with its sleeve caps (five rows), each twelve columns
        // about the figure's middle, redrawn by hand from the 32-pixel drawing's five views at two thirds their size;
        // no outlines (each part is outlined where it lands). 'e' is an eye, shut in a blink or a flinch. ----

        static readonly string[] HeadDown =
        {
            "...HHhHHH...",
            "..HHhHHHHh..",
            ".HHHhhHHhHH.",
            ".HhhhshhhHh.",
            ".hhsSssSshh.",
            ".hHesssseHh.",
            "..sesssses..",
            "...sSssSs...",
        };

        static readonly string[] HeadDownSide =
        {
            "...HHhHHH...",
            "..hHHHHHhHH.",
            ".HHhhHHHhHh.",
            ".HhhHhhshhh.",
            ".hhhhSssShh.",
            ".hhSessseh..",
            "..rsessses..",
            "...rsssss...",
        };

        static readonly string[] HeadSide =
        {
            "....HHHHH...",
            "..hHHHhhHHH.",
            ".hHHHHHHhHH.",
            ".HHhHhHHhhH.",
            ".HhhhhhhSsh.",
            ".hhhhSrhess.",
            "..hhrssSess.",
            "....rsssSs..",
        };

        static readonly string[] HeadUpSide =
        {
            "...HHHHHH...",
            "..HHHhHHHH..",
            ".hHHhHHhhHH.",
            ".hHHHHHHhhH.",
            ".HhHHHHhhhh.",
            ".hhHHhhhhhS.",
            "..hhhhhhhSs.",
            "...hhhhhh...",
        };

        static readonly string[] HeadUp =
        {
            "...HHhHHH...",
            "..hHHHhHHH..",
            ".HHHHhHHHHH.",
            ".hHHhHHHHhh.",
            ".HHHHhHHHhh.",
            ".hhhHHHHhhh.",
            "..rhhhhhhr..",
            "...hHHHh....",
        };

        static readonly string[] TrunkDown =
        {
            "...tdsSdt...",
            "..TTTddTTT..",
            "...TTTTTT...",
            "...bbOObb...",
            "...tTTTTt...",
        };

        static readonly string[] TrunkDownSide =
        {
            "....tdsdd...",
            "..TTTTddTd..",
            "...dTTTTT...",
            "...bbbOOb...",
            "...ttTTTT...",
        };

        static readonly string[] TrunkSide =
        {
            "....tddd....",
            "...TTTTdT...",
            "...tTTddT...",
            "...bbbbbO...",
            "...tTTTTT...",
        };

        static readonly string[] TrunkUpSide =
        {
            "...ddddT....",
            "..TdTTTTTd..",
            "...dTTTTd...",
            "...bbbbbb...",
            "...tTTTTd...",
        };

        static readonly string[] TrunkUp =
        {
            "...tddddT...",
            "..TTdTTdTT..",
            "...dTTTTd...",
            "...bbbbbb...",
            "...dTTTTd...",
        };

        // Where the parts' top left corners sit on the canvas at rest.
        const int HeadX = FeetX - 6, HeadY = FeetY - 17, TrunkX = FeetX - 6, TrunkY = FeetY - 9;

        // The eight ways round from facing right, turning counterclockwise (as WorldView.Sectors): the view drawn and
        // whether it is mirrored.
        static readonly (Look Look, bool Flip)[] Ways =
        {
            (Look.Side, false), (Look.UpSide, false), (Look.Up, false), (Look.UpSide, true),
            (Look.Side, true), (Look.DownSide, true), (Look.Down, false), (Look.DownSide, false),
        };

        static float AngleOf(Look look) => look switch { Look.Down => -90, Look.DownSide => -45, Look.Side => 0, Look.UpSide => 45, _ => 90 };

        static string[] HeadRows(Look look) => look switch { Look.Down => HeadDown, Look.DownSide => HeadDownSide, Look.Side => HeadSide, Look.UpSide => HeadUpSide, _ => HeadUp };

        static string[] TrunkRows(Look look) => look switch { Look.Down => TrunkDown, Look.DownSide => TrunkDownSide, Look.Side => TrunkSide, Look.UpSide => TrunkUpSide, _ => TrunkUp };

        // ---- The rig ----

        struct V3
        {
            // To the hero's right, ahead of them, up from the floor (pixels).
            public float S, F, U;

            public V3(float s, float f, float u)
            {
                S = s;
                F = f;
                U = u;
            }

            public static V3 Lerp(V3 a, V3 b, float t) => new V3(a.S + (b.S - a.S) * t, a.F + (b.F - a.F) * t, a.U + (b.U - a.U) * t);
        }

        static V3 V(float s, float f, float u) => new V3(s, f, u);

        // A pose, in the hero's own terms whichever way they face.
        struct Pose
        {
            // Turns in degrees, counterclockwise seen from above: the whole body on the spot, the trunk over the hips,
            // the head (0 looks where the hips face).
            public float Pivot, Twist, Head;
            // The trunk and head carried ahead and to the right (the head twice as far as the trunk), let down (the
            // knees bend), the whole body off the floor, the head let down.
            public float Lean, Shift, Drop, Hop, Nod;
            public V3 HandR, HandL;
            // Feet: to the right, ahead, lifted.
            public V3 FootR, FootL;
            // Where the weapon points: round from ahead (counterclockwise) and up from level.
            public float Yaw, Pitch;
            // The other hand on the weapon as well (0 to 1).
            public float Both;
            // A bow: how far the string is drawn to the string hand (0 to 1), an arrow on it (over a half).
            public float Draw, Arrow;
            // A staff's stone alight (0 to 1).
            public float Glow;
            // Eyes shut (over a half).
            public float Shut;
            // The whole picture tipped over onto the floor (degrees).
            public float Tip;
            // The whole body rolled over forwards, head first the way it faces (degrees; 360 is up on the feet again).
            public float Roll;
        }

        static Pose Lerp(Pose a, Pose b, float t)
        {
            float L(float x, float y) => x + (y - x) * t;
            return new Pose
            {
                Pivot = L(a.Pivot, b.Pivot), Twist = L(a.Twist, b.Twist), Head = L(a.Head, b.Head),
                Lean = L(a.Lean, b.Lean), Shift = L(a.Shift, b.Shift), Drop = L(a.Drop, b.Drop), Hop = L(a.Hop, b.Hop), Nod = L(a.Nod, b.Nod),
                HandR = V3.Lerp(a.HandR, b.HandR, t), HandL = V3.Lerp(a.HandL, b.HandL, t),
                FootR = V3.Lerp(a.FootR, b.FootR, t), FootL = V3.Lerp(a.FootL, b.FootL, t),
                Yaw = L(a.Yaw, b.Yaw), Pitch = L(a.Pitch, b.Pitch), Both = L(a.Both, b.Both),
                Draw = L(a.Draw, b.Draw), Arrow = L(a.Arrow, b.Arrow), Glow = L(a.Glow, b.Glow), Shut = L(a.Shut, b.Shut), Tip = L(a.Tip, b.Tip), Roll = L(a.Roll, b.Roll),
            };
        }

        delegate void Edit(ref Pose pose);

        static Pose With(Pose pose, Edit edit)
        {
            edit(ref pose);
            return pose;
        }

        // How a frame moves from the one before, for the timing and for the continuity check (MotionCheck): an
        // ordinary step, a swing too fast to follow, the blow itself.
        public enum Beat : byte
        {
            Step,
            Swing,
            Strike,
        }

        enum Ease : byte
        {
            Even,
            // Starting slow.
            In,
            // Ending slow.
            Out,
            Both,
        }

        static float Eased(Ease ease, float t) => ease switch
        {
            Ease.In => t * t,
            Ease.Out => 1 - (1 - t) * (1 - t),
            Ease.Both => t * t * (3 - 2 * t),
            _ => t,
        };

        // A motion's frames, how long each is shown and how it moves; the frame a strike that follows straight on
        // another starts from (past the frames that bring the weapon up from rest); whether it goes round.
        sealed class Clip
        {
            public Pose[] Frames;
            public float[] Seconds;
            public Beat[] Beats;
            public int Chain;
            public bool Loops;
            public float Length;
        }

        struct Stage
        {
            public float Seconds;
            public Pose To;
            public Ease Ease;
            public Beat Beat;
            public bool ChainHere;
            public int Least;
        }

        // A stage: how long it takes in all, the pose it ends in, how it eases there and how it moves; at least so
        // many frames.
        static Stage St(float seconds, Pose to, Ease ease = Ease.Even, Beat beat = Beat.Step, bool chain = false, int least = 1) =>
            new Stage { Seconds = seconds, To = to, Ease = ease, Beat = beat, ChainHere = chain, Least = least };

        // How far a hand or the weapon's end may move from one frame to the next, in any of the five views (pixels):
        // an ordinary frame, and a swing or a blow. The preview's continuity check (MotionCheck) holds every frame to
        // the same (HeroPreview).
        public const float StepPixels = 3.5f, SwingPixels = 8.5f;
        const int MostFrames = 28;

        static HeroWeapon _making;

        // The frames from a pose through stages, each stage easing from where the last ended to its own pose. A stage
        // gets as many frames as it takes for nothing to move further in one frame than its kind allows (the person:
        // "enough frames; where it looks awkward, more"), its time shared among them.
        static Clip Build(Pose from, params Stage[] stages)
        {
            var frames = new List<Pose>();
            var seconds = new List<float>();
            var beats = new List<Beat>();
            int chain = 0;
            var last = from;
            foreach (var stage in stages)
            {
                if (stage.ChainHere) chain = frames.Count;
                bool step = stage.Beat == Beat.Step;
                float most = (step ? StepPixels : SwingPixels) * 0.97f;
                int count = Math.Max(1, stage.Least);
                for (; count < MostFrames; count++)
                {
                    bool smooth = true;
                    var before = last;
                    for (int i = 1; i <= count && smooth; i++)
                    {
                        var pose = Lerp(last, stage.To, Eased(stage.Ease, (float)i / count));
                        smooth = Moved(before, pose, step ? 3 : 6, step ? 2 : 4) <= most;
                        before = pose;
                    }
                    if (smooth) break;
                }
                for (int i = 1; i <= count; i++)
                {
                    frames.Add(Lerp(last, stage.To, Eased(stage.Ease, (float)i / count)));
                    seconds.Add(stage.Seconds / count);
                    beats.Add(stage.Beat == Beat.Strike && i < count ? Beat.Swing : stage.Beat);
                }
                last = stage.To;
            }
            return Finish(frames, seconds, beats, chain, false);
        }

        static readonly Look[] Looks = { Look.Down, Look.DownSide, Look.Side, Look.UpSide, Look.Up };

        // The furthest either hand or the weapon's end moves between two poses, in any view.
        // Past everything if the head's top or the feet's line moves more rows than given.
        static float Moved(Pose a, Pose b, int top, int bottom)
        {
            float most = 0;
            foreach (var look in Looks)
            {
                var ja = Solve(look, a, _making).Joints;
                var jb = Solve(look, b, _making).Joints;
                most = MathF.Max(most, Far(ja.RightX - jb.RightX, ja.RightY - jb.RightY));
                most = MathF.Max(most, Far(ja.LeftX - jb.LeftX, ja.LeftY - jb.LeftY));
                most = MathF.Max(most, Far(ja.TipX - jb.TipX, ja.TipY - jb.TipY));
                if (Math.Abs(ja.Top - jb.Top) > top || Math.Abs(ja.Bottom - jb.Bottom) > bottom) return float.MaxValue;
            }
            return most;
        }

        static float Far(float x, float y) => MathF.Sqrt(x * x + y * y);

        static Clip Finish(List<Pose> frames, List<float> seconds, List<Beat> beats, int chain, bool loops)
        {
            float length = 0;
            foreach (float s in seconds) length += s;
            return new Clip { Frames = frames.ToArray(), Seconds = seconds.ToArray(), Beats = beats.ToArray(), Chain = chain, Loops = loops, Length = length };
        }

        // ---- Rest: how each weapon is carried ----

        static Pose RestOf(HeroWeapon weapon)
        {
            var pose = new Pose
            {
                HandR = V(5.5f, 0, 5.5f), HandL = V(-5.5f, 0, 5.5f),
                FootR = V(3, 0, 0), FootL = V(-3, 0, 0),
            };
            switch (weapon)
            {
                case HeroWeapon.Sword:
                    // Upright at the side, tipped out and a little ahead.
                    pose.HandR = V(6, 1.5f, 6.5f);
                    pose.Yaw = -30;
                    pose.Pitch = 72;
                    break;
                case HeroWeapon.Pick:
                    pose.HandR = V(6, 1.5f, 6.5f);
                    pose.Yaw = -30;
                    pose.Pitch = 72;
                    break;
                case HeroWeapon.Staff:
                    // Stood on the floor beside them like a walking staff.
                    pose.HandR = V(6.5f, 1.5f, 7);
                    pose.Yaw = -80;
                    pose.Pitch = 80;
                    break;
                case HeroWeapon.Bow:
                    // In the left hand, its back turned out.
                    pose.HandL = V(-6, 1.5f, 6.5f);
                    pose.Yaw = 50;
                    pose.Pitch = 0;
                    break;
            }
            return pose;
        }

        // ---- The motions ----

        static readonly Dictionary<int, Clip> Clips = new Dictionary<int, Clip>();

        static Clip ClipOf(HeroMove move, HeroWeapon weapon)
        {
            int key = (int)move * 8 + (int)weapon;
            if (!Clips.TryGetValue(key, out var clip)) Clips[key] = clip = Make(move, weapon);
            return clip;
        }

        public static int FramesOf(HeroMove move, HeroWeapon weapon) => ClipOf(move, weapon).Frames.Length;

        // How long the motion takes, once through.
        public static float SecondsOf(HeroMove move, HeroWeapon weapon) => ClipOf(move, weapon).Length;

        public static float FrameSeconds(HeroMove move, HeroWeapon weapon, int frame) => ClipOf(move, weapon).Seconds[Clamp(frame, move, weapon)];

        public static Beat BeatOf(HeroMove move, HeroWeapon weapon, int frame) => ClipOf(move, weapon).Beats[Clamp(frame, move, weapon)];

        // How far into the motion a strike that follows straight on another starts (seconds).
        public static float ChainSeconds(HeroMove move, HeroWeapon weapon)
        {
            var clip = ClipOf(move, weapon);
            float at = 0;
            for (int i = 0; i < clip.Chain; i++) at += clip.Seconds[i];
            return at;
        }

        // The frame shown this long into the motion; past its end the last one (the first again if it goes round).
        public static int FrameAt(HeroMove move, HeroWeapon weapon, float elapsed)
        {
            var clip = ClipOf(move, weapon);
            if (clip.Loops) elapsed %= clip.Length;
            for (int i = 0; i < clip.Seconds.Length; i++)
            {
                elapsed -= clip.Seconds[i];
                if (elapsed < 0) return i;
            }
            return clip.Seconds.Length - 1;
        }

        // How far into the motion its blow has landed (the end of the strike's frame); 0 with no blow.
        public static float StrikeSeconds(HeroMove move, HeroWeapon weapon)
        {
            var clip = ClipOf(move, weapon);
            int strike = Array.IndexOf(clip.Beats, Beat.Strike);
            float at = 0;
            for (int i = 0; i <= strike; i++) at += clip.Seconds[i];
            return at;
        }

        // The frame of an attack in which the blow lands.
        public static int StrikeFrame(HeroMove move, HeroWeapon weapon) => Array.IndexOf(ClipOf(move, weapon).Beats, Beat.Strike);

        static int Clamp(int frame, HeroMove move, HeroWeapon weapon) => Math.Max(0, Math.Min(ClipOf(move, weapon).Frames.Length - 1, frame));

        static Clip Make(HeroMove move, HeroWeapon weapon)
        {
            // The weapon is put away to drink.
            if (move == HeroMove.Drink) weapon = HeroWeapon.None;
            _making = weapon;
            var rest = RestOf(weapon);
            switch (move)
            {
                case HeroMove.Stand: return Standing(rest);
                case HeroMove.Walk: return Walking(rest, weapon);
                case HeroMove.Attack1:
                case HeroMove.Attack2:
                case HeroMove.Attack3:
                    int step = move - HeroMove.Attack1;
                    return weapon switch
                    {
                        HeroWeapon.Sword => step == 0 ? CutDown(rest) : step == 1 ? CutUp(rest) : Sweep(rest),
                        HeroWeapon.Bow => Shot(rest, step == 2),
                        HeroWeapon.Staff => step == 2 ? Whirl(rest) : Bolt(rest, step == 1),
                        HeroWeapon.Pick => step == 0 ? Chop(rest) : step == 1 ? Across(rest) : Slam(rest),
                        _ => step == 0 ? Jab(rest) : step == 1 ? Cross(rest) : Uppercut(rest),
                    };
                case HeroMove.Cast: return Casting(rest, weapon);
                case HeroMove.Thrust: return Thrusting(rest, weapon);
                case HeroMove.Spread: return Spreading(rest, weapon);
                case HeroMove.Drink: return Drinking(rest);
                case HeroMove.Hurt: return Flinch(rest);
                case HeroMove.Fall: return Falling(rest);
                case HeroMove.Roll: return weapon == HeroWeapon.Bow ? Sliding(rest) : weapon == HeroWeapon.Staff ? Gliding(rest) : Tumbling(rest, weapon);
                default: return Rising(rest);
            }
        }

        // Breathing: the chest up a pixel and down again over six frames, the hands a little out as it fills.
        static Clip Standing(Pose rest)
        {
            var frames = new List<Pose>();
            var seconds = new List<float>();
            var beats = new List<Beat>();
            float[] breath = { 0, 0, 0.5f, 1, 1, 0.5f };
            foreach (float b in breath)
            {
                frames.Add(With(rest, (ref Pose p) =>
                {
                    p.Hop = 0;
                    p.Drop = -b;
                    p.HandR.U += b * 0.5f;
                    p.HandL.U += b * 0.5f;
                }));
                seconds.Add(0.27f);
                beats.Add(Beat.Step);
            }
            return Finish(frames, seconds, beats, 0, true);
        }

        public const int WalkFrames = 8;

        // A step cycle of eight: each foot swings ahead lifted and comes back along the floor, the arms swing against
        // the legs (the weapon hand less), the trunk turns a little with the arms and the body bobs on each step.
        static Clip Walking(Pose rest, HeroWeapon weapon)
        {
            var frames = new List<Pose>();
            var seconds = new List<float>();
            var beats = new List<Beat>();
            for (int i = 0; i < WalkFrames; i++)
            {
                float phase = i / (float)WalkFrames * 2 * MathF.PI;
                float swing = MathF.Sin(phase), lift = MathF.Cos(phase);
                frames.Add(With(rest, (ref Pose p) =>
                {
                    p.FootR = V(2.5f, 3 * swing, MathF.Max(0, lift) * 1.6f);
                    p.FootL = V(-2.5f, -3 * swing, MathF.Max(0, -lift) * 1.6f);
                    bool right = weapon != HeroWeapon.None && weapon != HeroWeapon.Bow, left = weapon == HeroWeapon.Bow;
                    p.HandR.F += -swing * (right ? 1 : 2.6f);
                    p.HandL.F += swing * (left ? 1 : 2.6f);
                    p.HandR.U += right ? 0 : MathF.Abs(swing) * 0.6f;
                    p.HandL.U += left ? 0 : MathF.Abs(swing) * 0.6f;
                    p.Twist = -10 * swing;
                    p.Lean = 1;
                    p.Hop = MathF.Abs(swing) > 0.8f ? 1 : 0;
                }));
                seconds.Add(0.0625f);
                beats.Add(Beat.Step);
            }
            return Finish(frames, seconds, beats, 0, true);
        }

        // Between a blow's end and rest: the weapon brought upright in front first, so on its way home it never
        // swings through pointing straight at the viewer (where its picture would shrink to nothing and flip).
        static Pose Upright(Pose rest, Pose from) => With(rest, (ref Pose p) =>
        {
            p.HandR = V(3.5f, 3.5f, 9);
            p.HandL = from.HandL;
            p.Yaw = (from.Yaw + rest.Yaw) / 2;
            p.Pitch = 80;
            p.Twist = from.Twist / 2;
            p.Lean = from.Lean / 2;
            p.Drop = from.Drop / 2;
            p.FootR = from.FootR;
            p.FootL = from.FootL;
            p.Both = from.Both / 2;
        });

        // Timings (seconds a frame): bringing the weapon up, the swing, the blow, held, the way back.
        const float Up = 0.022f, Fast = 0.016f, Hit = 0.05f, Hold = 0.045f, Back = 0.04f;

        // Sword, first step: up over the right shoulder, a cut down across to the left as the left foot steps in and
        // the trunk turns with it, held low, back to rest.
        static Clip CutDown(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, -2, 14);
                p.HandL = V(-4, 3, 8);
                p.Yaw = -130;
                p.Pitch = 55;
                p.Twist = -35;
                p.Lean = -1;
                p.Drop = -1;
                p.FootL = V(-3, 1, 0);
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 6.5f, 8);
                p.HandL = V(-5, -1, 7);
                p.Yaw = 0;
                p.Pitch = 5;
                p.Twist = 10;
                p.Lean = 2;
                p.Drop = 1;
                p.FootL = V(-3, 4, 0);
                p.FootR = V(3, -2, 0);
            });
            var low = With(through, (ref Pose p) =>
            {
                p.HandR = V(-3, 5, 4.5f);
                p.HandL = V(-6, -2, 7);
                p.Yaw = 75;
                p.Pitch = -12;
                p.Twist = 35;
                p.Lean = 3;
                p.Drop = 2;
            });
            return Build(rest,
                St(0.04f, raised, Ease.Out, Beat.Swing, least: 3),
                St(0.03f, through, Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.03f, low, Ease.Even, Beat.Strike, least: 3),
                St(0.09f, With(low, (ref Pose p) => { p.Lean = 2.5f; p.Pitch = -8; p.Drop = 1.5f; }), least: 2),
                St(0.09f, Upright(rest, low), Ease.Even, least: 4),
                St(0.09f, rest, Ease.Out, least: 4));
        }

        // Sword, second step: from low on the left the blade comes back up across to the right, the body rising with
        // it onto the toes, the right foot stepping in.
        static Clip CutUp(Pose rest)
        {
            var low = With(rest, (ref Pose p) =>
            {
                p.HandR = V(-3, 4, 4.5f);
                p.HandL = V(-6, -2, 7);
                p.Yaw = 75;
                p.Pitch = -12;
                p.Twist = 35;
                p.Lean = 2;
                p.Drop = 2;
                p.FootL = V(-3, 3, 0);
                p.FootR = V(3, -2, 0);
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 7, 9);
                p.HandL = V(-5, 0, 6);
                p.Yaw = 0;
                p.Pitch = 15;
                p.Twist = -5;
                p.Lean = 2.5f;
                p.Drop = 0.5f;
                p.FootR = V(3, 3, 0);
                p.FootL = V(-3, -1, 0);
            });
            var high = With(through, (ref Pose p) =>
            {
                p.HandR = V(6, 2, 15);
                p.HandL = V(-5, -2, 6);
                p.Yaw = -75;
                p.Pitch = 70;
                p.Twist = -40;
                p.Lean = 1;
                p.Drop = -1;
                p.Hop = 1;
            });
            return Build(rest,
                St(0.04f, low, Ease.Out, Beat.Swing, least: 3),
                St(0.03f, through, Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.03f, high, Ease.Even, Beat.Strike, least: 3),
                St(0.09f, With(high, (ref Pose p) => { p.Hop = 0; p.Drop = 0; p.Pitch = 75; }), least: 2),
                St(0.17f, rest, Ease.Both, least: 6));
        }

        // Sword, the finisher: wound far back to the right and crouched, then the whole body turns right round on the
        // spot with the blade held out level, sweeping everything about, and ends low and wide with the blade across.
        static Clip Sweep(Pose rest)
        {
            var wound = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, -3, 8);
                p.HandL = V(-3, 4, 8);
                p.Yaw = -140;
                p.Pitch = 15;
                p.Twist = -45;
                p.Drop = 2;
                p.Lean = -1;
                p.FootR = V(4, -2, 0);
                p.FootL = V(-3, 2, 0);
            });
            var turning = With(rest, (ref Pose p) =>
            {
                p.Pivot = 180;
                p.HandR = V(8, 1, 9);
                p.HandL = V(-7, -1, 9);
                p.Yaw = -95;
                p.Pitch = 4;
                p.Twist = -10;
                p.Drop = 1;
                p.Hop = 1;
                p.FootR = V(3.5f, 1, 1);
                p.FootL = V(-3.5f, -1, 0);
            });
            var round = With(turning, (ref Pose p) =>
            {
                p.Pivot = 360;
                p.HandR = V(5, 6, 8.5f);
                p.HandL = V(-7, -2, 8);
                p.Yaw = -30;
                p.Pitch = 0;
                p.Twist = 25;
                p.Lean = 2;
                p.Hop = 0;
                p.FootR = V(4, 3, 0);
                p.FootL = V(-4, -2, 0);
            });
            var spent = With(round, (ref Pose p) =>
            {
                p.HandR = V(-3, 5, 6);
                p.Yaw = 60;
                p.Pitch = -15;
                p.Twist = 45;
                p.Lean = 3;
                p.Drop = 2.5f;
                p.FootR = V(4.5f, 3, 0);
                p.FootL = V(-4.5f, -3, 0);
            });
            var home = With(rest, (ref Pose p) => p.Pivot = 360);
            return Build(rest,
                St(0.045f, wound, Ease.Out, Beat.Swing, least: 4),
                St(0.045f, turning, Ease.Even, Beat.Swing, chain: true, least: 5),
                St(0.045f, round, Ease.Even, Beat.Swing, least: 5),
                St(0.03f, spent, Ease.Out, Beat.Strike, least: 3),
                St(0.14f, With(spent, (ref Pose p) => { p.Lean = 2.5f; p.Drop = 2; }), least: 3),
                St(0.1f, With(Upright(rest, spent), (ref Pose p) => p.Pivot = 360), Ease.Even, least: 4),
                St(0.1f, home, Ease.Out, least: 4));
        }

        // A bow: raised with an arrow on the string, drawn to the cheek as the trunk turns side on, loosed (the string
        // snaps home, the string hand flies back, the bow kicks up), then the hand goes back over the shoulder for
        // the next arrow, sets it on the string and the bow comes down. The finisher leans back and draws further,
        // holds a moment, and the shot throws the body back.
        static Clip Shot(Pose rest, bool big)
        {
            var nocked = With(rest, (ref Pose p) =>
            {
                p.HandL = V(-2, 7, 10);
                p.HandR = V(-1, 3.5f, 10);
                p.Yaw = 0;
                p.Pitch = 0;
                p.Twist = -30;
                p.Arrow = 1;
                p.FootL = V(-3, 2.5f, 0);
                p.FootR = V(3, -1.5f, 0);
            });
            var drawn = With(nocked, (ref Pose p) =>
            {
                p.HandL = V(-2, 7.5f, 10);
                p.HandR = V(2.5f, big ? -3.5f : -1.5f, 10.5f);
                p.Draw = 1;
                p.Twist = big ? -65 : -55;
                p.Lean = big ? -2.5f : -1;
                p.Drop = big ? 1.5f : 0;
                p.FootL = V(-3, big ? 4 : 3, 0);
                p.FootR = V(3, big ? -3.5f : -2, 0);
            });
            var loosed = With(drawn, (ref Pose p) =>
            {
                p.HandR = V(2, big ? -6 : -4, 11.5f);
                p.HandL = V(-2, 7.5f, 10);
                p.Draw = 0;
                p.Arrow = 0;
                p.Pitch = big ? 14 : 6;
                p.Lean = big ? -3.5f : -1.5f;
                p.Hop = big ? 1 : 0;
            });
            var kicked = With(loosed, (ref Pose p) =>
            {
                p.Pitch = big ? 32 : 16;
                p.HandL = V(-2, 7, big ? 12 : 11);
                p.HandR = V(2.5f, -4, 11);
                p.Lean = big ? -2 : -0.5f;
                p.Hop = 0;
                p.Drop = big ? 1 : 0;
            });
            var reaching = With(rest, (ref Pose p) =>
            {
                p.HandL = V(-2, 6, 9);
                p.HandR = V(3, -3, 13.5f);
                p.Yaw = 15;
                p.Pitch = 10;
                p.Twist = -30;
                p.FootL = V(-3, 2, 0);
                p.FootR = V(3, -1, 0);
            });
            var set = With(reaching, (ref Pose p) =>
            {
                p.HandR = V(-1, 3, 9);
                p.Arrow = 1;
                p.Pitch = 0;
                p.Twist = -20;
            });
            var stages = new List<Stage>
            {
                St(0.025f, nocked, Ease.Out, Beat.Swing, least: 2),
                St(big ? 0.055f : 0.045f, drawn, Ease.Out, Beat.Swing, chain: true, least: big ? 5 : 4),
            };
            if (big) stages.Add(St(0.02f, With(drawn, (ref Pose p) => p.Lean = -3)));
            stages.Add(St(0.03f, loosed, Ease.Even, Beat.Strike, least: 2));
            stages.Add(St(0.1f, kicked, Ease.Out, least: 4));
            stages.Add(St(0.12f, reaching, Ease.Both, least: 4));
            stages.Add(St(0.12f, set, Ease.Both, least: 4));
            stages.Add(St(0.13f, With(rest, (ref Pose p) => p.Arrow = 0.6f), Ease.Both, least: 4));
            stages.Add(St(0.03f, rest));
            return Build(rest, stages.ToArray());
        }

        // A staff: lifted upright in both hands, its stone lighting as it gathers, then brought down and thrust out at
        // the target with a step, held there, drawn back. The second step thrusts from the other foot.
        static Clip Bolt(Pose rest, bool other)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, 2, 13.5f);
                p.HandL = V(-4, 2, 9);
                p.Yaw = -20;
                p.Pitch = 88;
                p.Both = 1;
                p.Glow = 1;
                p.Drop = -1;
                p.Lean = -1.5f;
                p.Twist = other ? 20 : -20;
            });
            var thrust = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 7, 9);
                p.Yaw = 0;
                p.Pitch = 4;
                p.Both = 1;
                p.Glow = 1;
                p.Lean = 3;
                p.Drop = 1;
                p.Twist = other ? -20 : 20;
                p.FootL = V(-3, other ? -2 : 4, 0);
                p.FootR = V(3, other ? 4 : -2, 0);
            });
            return Build(rest,
                St(0.04f, raised, Ease.Out, Beat.Swing, least: 4),
                St(0.025f, With(thrust, (ref Pose p) => { p.HandR = V(5.5f, 5, 11); p.Pitch = 30; p.Lean = 1.5f; }), Ease.In, Beat.Swing, chain: true, least: 2),
                St(0.025f, thrust, Ease.Even, Beat.Strike, least: 2),
                St(0.1f, With(thrust, (ref Pose p) => { p.Glow = 0.3f; p.Lean = 2; p.HandR = V(5, 6, 9); }), least: 2),
                St(0.2f, rest, Ease.Both, least: 6));
        }

        // A staff, the finisher: whipped up and once round overhead in both hands, its stone alight, and brought
        // crashing down ahead with a long step; then, the shot gone, swung on round in a flourish and stood again.
        static Clip Whirl(Pose rest)
        {
            var up = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, -1, 15);
                p.HandL = V(-3, -1, 14);
                p.Yaw = -150;
                p.Pitch = 14;
                p.Both = 1;
                p.Glow = 0.6f;
                p.Drop = -1;
                p.Twist = -30;
                p.Lean = -1.5f;
            });
            var over = With(up, (ref Pose p) =>
            {
                p.Yaw = 0;
                p.Pitch = 60;
                p.HandR = V(2, 2, 15.5f);
                p.Twist = 0;
                p.Glow = 1;
                p.Hop = 1;
                p.Lean = 0;
            });
            var down = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 7, 8.5f);
                p.Yaw = 0;
                p.Pitch = -4;
                p.Both = 1;
                p.Glow = 1;
                p.Lean = 3.5f;
                p.Drop = 2;
                p.Twist = 15;
                p.FootL = V(-3.5f, 5, 0);
                p.FootR = V(3.5f, -3, 0);
            });
            var swept = With(rest, (ref Pose p) =>
            {
                p.HandR = V(-2, 4, 9);
                p.Yaw = 110;
                p.Pitch = 10;
                p.Both = 1;
                p.Glow = 0.3f;
                p.Lean = 1.5f;
                p.Drop = 1;
                p.Twist = 40;
                p.FootL = V(-3.5f, 3, 0);
                p.FootR = V(3.5f, -2, 0);
            });
            var behind = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, -1, 12);
                p.Yaw = 220;
                p.Pitch = 40;
                p.Both = 0.5f;
                p.Twist = 0;
            });
            var home = With(rest, (ref Pose p) => p.Yaw += 360);
            return Build(rest,
                St(0.04f, up, Ease.Out, Beat.Swing, least: 4),
                St(0.035f, over, Ease.In, Beat.Swing, chain: true, least: 4),
                St(0.03f, down, Ease.Even, Beat.Strike, least: 3),
                St(0.12f, With(down, (ref Pose p) => { p.Glow = 0.3f; p.Lean = 3; }), least: 3),
                St(0.1f, swept, Ease.In, Beat.Swing, least: 4),
                St(0.1f, behind, Ease.Even, Beat.Swing, least: 4),
                St(0.14f, home, Ease.Out, least: 5));
        }

        // A pickaxe: hauled up over the head in both hands, the body stretching, and brought down ahead into the rock
        // with the knees bending; stuck a moment, pulled free.
        static Clip Chop(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, -1, 15.5f);
                p.Yaw = 0;
                p.Pitch = 125;
                p.Both = 1;
                p.Drop = -1;
                p.Lean = -2;
                p.FootL = V(-3, 2, 0);
            });
            var struck = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4.5f, 7, 6.5f);
                p.Yaw = 0;
                p.Pitch = -25;
                p.Both = 1;
                p.Drop = 2;
                p.Lean = 3;
                p.FootL = V(-3, 4, 0);
                p.FootR = V(3, -2, 0);
            });
            return Build(rest,
                St(0.045f, raised, Ease.Out, Beat.Swing, least: 4),
                St(0.03f, With(struck, (ref Pose p) => { p.HandR = V(4.5f, 5, 13); p.Pitch = 50; p.Drop = 0; p.Lean = 1; }), Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.03f, struck, Ease.Even, Beat.Strike, least: 3),
                St(0.1f, With(struck, (ref Pose p) => { p.Pitch = -20; p.Lean = 2.5f; }), least: 2),
                St(0.2f, rest, Ease.Both, least: 6));
        }

        // A pickaxe, second step: swung level across from the right as the trunk turns.
        static Clip Across(Pose rest)
        {
            var back = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, -3, 9);
                p.Yaw = -150;
                p.Pitch = 10;
                p.Both = 0.5f;
                p.Twist = -40;
                p.Drop = 1;
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, 7, 8.5f);
                p.Yaw = -10;
                p.Pitch = 0;
                p.Both = 1;
                p.Twist = 10;
                p.Lean = 2;
                p.Drop = 1;
                p.FootR = V(3, 3, 0);
                p.FootL = V(-3, -2, 0);
            });
            var past = With(through, (ref Pose p) =>
            {
                p.HandR = V(-3, 5, 8);
                p.Yaw = 70;
                p.Twist = 40;
                p.Lean = 2.5f;
            });
            return Build(rest,
                St(0.045f, back, Ease.Out, Beat.Swing, least: 4),
                St(0.035f, through, Ease.In, Beat.Swing, chain: true, least: 4),
                St(0.025f, past, Ease.Even, Beat.Strike, least: 2),
                St(0.1f, With(past, (ref Pose p) => { p.Lean = 2; p.Drop = 1.5f; }), least: 2),
                St(0.1f, Upright(rest, past), Ease.Even, least: 4),
                St(0.1f, rest, Ease.Out, least: 4));
        }

        // A pickaxe, the finisher: a hop with the pick hauled far back overhead, landing with the whole body behind
        // the blow.
        static Clip Slam(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, -2, 16);
                p.Yaw = 0;
                p.Pitch = 140;
                p.Both = 1;
                p.Drop = 2;
                p.Lean = -2.5f;
            });
            var air = With(raised, (ref Pose p) =>
            {
                p.Hop = 4;
                p.Drop = -1;
                p.Pitch = 130;
                p.Lean = -1;
                p.FootR = V(3, -2, 1.5f);
                p.FootL = V(-3, 2, 1);
            });
            var struck = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4.5f, 7, 5.5f);
                p.Yaw = 0;
                p.Pitch = -32;
                p.Both = 1;
                p.Drop = 3;
                p.Lean = 3.5f;
                p.FootL = V(-4, 4.5f, 0);
                p.FootR = V(4, -3, 0);
            });
            return Build(rest,
                St(0.04f, raised, Ease.Out, Beat.Swing, least: 4),
                St(0.04f, air, Ease.Out, Beat.Swing, chain: true, least: 3),
                St(0.035f, With(struck, (ref Pose p) => { p.HandR = V(4.5f, 5, 13); p.Pitch = 55; p.Drop = 0; p.Hop = 2; p.Lean = 1; }), Ease.In, Beat.Swing, least: 3),
                St(0.03f, struck, Ease.Even, Beat.Strike, least: 3),
                St(0.15f, With(struck, (ref Pose p) => { p.Pitch = -26; p.Lean = 3; p.Drop = 2.5f; }), least: 3),
                St(0.22f, rest, Ease.Both, least: 6));
        }

        static Pose Guard(Pose rest) => With(rest, (ref Pose p) =>
        {
            p.HandR = V(3.5f, 3, 10);
            p.HandL = V(-3.5f, 3.5f, 10);
            p.Drop = 1;
            p.Lean = 1;
            p.FootL = V(-3, 2, 0);
            p.FootR = V(3, -1, 0);
        });

        // Bare hands, first step: the right fist straight out from a guard, the shoulder turned in behind it.
        static Clip Jab(Pose rest)
        {
            var guard = Guard(rest);
            var out_ = With(guard, (ref Pose p) =>
            {
                p.HandR = V(4, 8, 10);
                p.HandL = V(-4, 1, 9.5f);
                p.Twist = 35;
                p.Lean = 2.5f;
                p.FootL = V(-3, 3.5f, 0);
            });
            return Build(rest,
                St(0.04f, With(guard, (ref Pose p) => { p.HandR = V(4.5f, 0, 9.5f); p.Twist = -20; }), Ease.Out, Beat.Swing, least: 3),
                St(0.035f, out_, Ease.In, Beat.Strike, chain: true, least: 4),
                St(0.08f, With(out_, (ref Pose p) => p.Lean = 2), least: 2),
                St(0.06f, guard, Ease.Both, least: 3),
                St(0.1f, rest, Ease.Both, least: 4));
        }

        // Bare hands, second step: the left fist across as the right comes back to guard.
        static Clip Cross(Pose rest)
        {
            var guard = Guard(rest);
            var out_ = With(guard, (ref Pose p) =>
            {
                p.HandL = V(-4, 8, 10);
                p.HandR = V(4, 1, 9.5f);
                p.Twist = -35;
                p.Lean = 2.5f;
                p.FootR = V(3, 3, 0);
                p.FootL = V(-3, -1.5f, 0);
            });
            return Build(rest,
                St(0.04f, With(guard, (ref Pose p) => { p.HandL = V(-4.5f, 0, 9.5f); p.Twist = 20; }), Ease.Out, Beat.Swing, least: 3),
                St(0.035f, out_, Ease.In, Beat.Strike, chain: true, least: 4),
                St(0.08f, With(out_, (ref Pose p) => p.Lean = 2), least: 2),
                St(0.06f, guard, Ease.Both, least: 3),
                St(0.1f, rest, Ease.Both, least: 4));
        }

        // Bare hands, the finisher: down into a crouch with the right fist drawn back low, then up through an
        // uppercut that lifts the body off the floor.
        static Clip Uppercut(Pose rest)
        {
            var coiled = With(Guard(rest), (ref Pose p) =>
            {
                p.HandR = V(5.5f, -2, 4.5f);
                p.HandL = V(-3, 4, 9);
                p.Twist = -45;
                p.Drop = 3;
                p.Lean = 1;
            });
            var up = With(Guard(rest), (ref Pose p) =>
            {
                p.HandR = V(1.5f, 6, 15.5f);
                p.HandL = V(-5, -1, 7);
                p.Twist = 45;
                p.Drop = -1;
                p.Hop = 3;
                p.Lean = 2;
                p.Nod = -1;
                p.FootR = V(3, 2, 1);
                p.FootL = V(-3, 1, 0.5f);
            });
            return Build(rest,
                St(0.05f, coiled, Ease.Out, Beat.Swing, least: 4),
                St(0.03f, With(up, (ref Pose p) => { p.HandR = V(3, 6, 9); p.Hop = 0; p.Drop = 1; p.Twist = 10; }), Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.03f, up, Ease.Even, Beat.Strike, least: 3),
                St(0.1f, With(up, (ref Pose p) => { p.Hop = 2; p.HandR = V(1.5f, 5, 16); }), least: 2),
                St(0.08f, With(up, (ref Pose p) => { p.Hop = 0; p.Drop = 1; p.HandR = V(3, 3, 12); p.Twist = 20; p.Nod = 0; }), least: 3),
                St(0.16f, rest, Ease.Both, least: 5));
        }

        // Both arms lifted high and the weapon with them, the head back, held with a tremble, let down.
        static Clip Casting(Pose rest, HeroWeapon weapon)
        {
            var high = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, 1, 15.5f);
                p.HandL = V(-6, 1, 15.5f);
                p.Drop = -1;
                p.Nod = -1;
                p.Lean = -1;
                p.Glow = 1;
                if (weapon == HeroWeapon.Bow)
                {
                    p.Yaw = 90;
                    p.Pitch = 60;
                }
                else
                {
                    p.Yaw = -80;
                    p.Pitch = 84;
                }
            });
            return Build(rest,
                St(0.08f, high, Ease.Out, Beat.Swing, least: 5),
                St(0.045f, With(high, (ref Pose p) => { p.HandR.U = 16.5f; p.HandL.U = 16.5f; p.Hop = 1; })),
                St(0.045f, high),
                St(0.045f, With(high, (ref Pose p) => { p.HandR.U = 16.5f; p.HandL.U = 16.5f; p.Hop = 1; })),
                St(0.045f, high),
                St(0.18f, rest, Ease.Both, least: 6));
        }

        // Sent forward: a stride, the weapon (or both open hands) driven straight out ahead, held, drawn back.
        static Clip Thrusting(Pose rest, HeroWeapon weapon)
        {
            var back = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, -2, 9);
                p.HandL = V(-5, -1, 9);
                p.Twist = -25;
                p.Lean = -1.5f;
                p.Drop = 1;
                p.Yaw = weapon == HeroWeapon.Bow ? 20 : -10;
                p.Pitch = weapon == HeroWeapon.Bow ? 0 : 20;
            });
            var out_ = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 7.5f, 9.5f);
                p.HandL = V(-5, 7.5f, 9.5f);
                p.Twist = 15;
                p.Lean = 3.5f;
                p.Drop = 1.5f;
                p.Yaw = 0;
                p.Pitch = 0;
                p.Glow = 1;
                p.FootL = V(-3, 4.5f, 0);
                p.FootR = V(3, -3, 0);
            });
            return Build(rest,
                St(0.04f, back, Ease.Out, Beat.Swing, least: 3),
                St(0.035f, out_, Ease.In, Beat.Strike, chain: true, least: 4),
                St(0.14f, With(out_, (ref Pose p) => { p.Lean = 3; p.Glow = 0.4f; }), least: 3),
                St(0.18f, rest, Ease.Both, least: 6));
        }

        // All round: crouched with the arms drawn in, then right round on the spot with both arms and the weapon
        // flung wide, settling.
        static Clip Spreading(Pose rest, HeroWeapon weapon)
        {
            var coiled = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, 2, 9);
                p.HandL = V(-3, 2, 9);
                p.Drop = 2;
                p.Twist = -30;
            });
            Pose Wide(float pivot, float hop) => With(rest, (ref Pose p) =>
            {
                p.Pivot = pivot;
                p.HandR = V(8.5f, 0, 10);
                p.HandL = V(-8.5f, 0, 10);
                p.Drop = 0.5f;
                p.Hop = hop;
                p.Glow = 1;
                if (weapon == HeroWeapon.Bow)
                {
                    p.Yaw = 90;
                    p.Pitch = 0;
                }
                else
                {
                    p.Yaw = -90;
                    p.Pitch = 6;
                }
                p.FootR = V(3.5f, 1, hop > 0 ? 1 : 0);
                p.FootL = V(-3.5f, -1, 0);
            });
            var home = With(rest, (ref Pose p) => p.Pivot = 360);
            return Build(rest,
                St(0.04f, coiled, Ease.Out, Beat.Swing, least: 3),
                St(0.06f, Wide(90, 1), Ease.In, Beat.Swing, least: 3),
                St(0.06f, Wide(180, 1), Ease.Even, Beat.Swing, least: 3),
                St(0.06f, Wide(270, 1), Ease.Even, Beat.Swing, least: 3),
                St(0.06f, Wide(360, 0), Ease.Even, Beat.Strike, least: 3),
                St(0.18f, home, Ease.Both, least: 6));
        }

        // The right hand brings the bottle to the mouth, the head goes back as it tips, and comes down again. The
        // weapon is put away while they drink (the bottle is drawn by the game at the hand, JointsOf).
        static Clip Drinking(Pose rest)
        {
            var lifted = With(rest, (ref Pose p) =>
            {
                p.HandR = V(2.5f, 4, 12);
                p.Pitch = 70;
                p.Yaw = 0;
            });
            var tipped = With(lifted, (ref Pose p) =>
            {
                p.HandR = V(2, 3.5f, 13.5f);
                p.Pitch = 150;
                p.Nod = -1;
                p.Lean = -1.5f;
                p.Shut = 1;
            });
            return Build(rest,
                St(0.13f, lifted, Ease.Out, least: 4),
                St(0.15f, tipped, Ease.Both, least: 4),
                St(0.2f, With(tipped, (ref Pose p) => p.Pitch = 160), least: 3),
                St(0.16f, rest, Ease.Both, least: 5));
        }

        // Struck: thrown back with the head down, the eyes shut and the arms flung up, then upright again.
        static Clip Flinch(Pose rest)
        {
            var hit = With(rest, (ref Pose p) =>
            {
                p.Lean = -3;
                p.Drop = 1.5f;
                p.Nod = 1;
                p.Shut = 1;
                p.HandR.F = 3;
                p.HandR.U += 4;
                p.HandL = V(-5, 3, 10);
                p.Twist = 20;
                p.FootR = V(3, -2.5f, 0);
                p.FootL = V(-3, 1, 0);
            });
            return Build(rest,
                St(0.04f, hit, Ease.Out, Beat.Strike, least: 3),
                St(0.09f, With(hit, (ref Pose p) => { p.Lean = -2.5f; p.Drop = 1; }), least: 2),
                St(0.12f, rest, Ease.Both, least: 5));
        }

        // The knees give and the arms drop, the body tips over onto the floor and settles there.
        static Clip Falling(Pose rest)
        {
            var sagging = With(rest, (ref Pose p) =>
            {
                p.Drop = 3.5f;
                p.Nod = 2;
                p.Lean = -1;
                p.Shut = 1;
                p.HandR = V(5.5f, 1, 4);
                p.HandL = V(-5.5f, 1, 4);
            });
            var down = With(sagging, (ref Pose p) => p.Tip = 90);
            return Build(rest,
                St(0.15f, sagging, Ease.Out, least: 4),
                St(0.16f, down, Ease.In, Beat.Swing, least: 6),
                St(0.05f, With(down, (ref Pose p) => p.Tip = 84)),
                St(0.06f, down));
        }

        // Up again: the body comes off the floor, kneels and stands.
        static Clip Rising(Pose rest)
        {
            var down = With(rest, (ref Pose p) =>
            {
                p.Drop = 3.5f;
                p.Nod = 2;
                p.Lean = -1;
                p.Shut = 1;
                p.HandR = V(5.5f, 1, 4);
                p.HandL = V(-5.5f, 1, 4);
                p.Tip = 90;
            });
            return Build(down,
                St(0.2f, With(down, (ref Pose p) => p.Tip = 0), Ease.Out, Beat.Swing, least: 6),
                St(0.12f, With(rest, (ref Pose p) => { p.Drop = 2; p.Nod = 1; }), least: 3),
                St(0.15f, rest, Ease.Both, least: 4));
        }

        // ---- The dodge roll: 0.3 s in all (World.RollSeconds), ahead being the way they roll ----

        // A forward roll: a dive toward the hands, the body tucked into a ball and gone over head first the way it
        // goes (Pose.Roll), and up onto the feet again. The weapon is held in against the back and goes over with it.
        static Clip Tumbling(Pose rest, HeroWeapon weapon)
        {
            bool armed = weapon != HeroWeapon.None;
            var dive = With(rest, (ref Pose p) =>
            {
                p.Roll = 50;
                p.Lean = 2.5f;
                p.Drop = 5.5f;
                p.Nod = 2.5f;
                p.HandR = armed ? V(4, 1.5f, 4.5f) : V(3.5f, 4, 4);
                p.HandL = V(-3.5f, 4, 4);
                p.FootR = V(2.5f, -1.5f, 0.5f);
                p.FootL = V(-2.5f, 0.5f, 0);
                if (armed)
                {
                    // Upright against the back, so it goes over with the body without sweeping round.
                    p.Yaw = -150;
                    p.Pitch = 78;
                }
            });
            var tucked = With(dive, (ref Pose p) =>
            {
                p.Roll = 180;
                p.Lean = 0;
                p.Drop = 9;
                p.Nod = 4.5f;
                p.Hop = 1.5f;
                p.HandR = armed ? V(3, 1, 3.5f) : V(2.5f, 2.5f, 3);
                p.HandL = V(-2.5f, 2.5f, 3);
                p.FootR = V(1.5f, 1, 2);
                p.FootL = V(-1.5f, 1, 2);
            });
            var over = With(tucked, (ref Pose p) =>
            {
                p.Roll = 300;
                p.Drop = 8;
                p.Hop = 0;
                p.Nod = 4;
                p.FootR = V(2, 1.5f, 0.5f);
                p.FootL = V(-2, 0, 0.5f);
            });
            var up = With(rest, (ref Pose p) => p.Roll = 360);
            return Build(rest,
                St(0.05f, dive, Ease.Out, Beat.Swing, least: 2),
                St(0.09f, tucked, Ease.Even, Beat.Swing, least: 4),
                St(0.09f, over, Ease.Even, Beat.Swing, least: 4),
                St(0.07f, up, Ease.Out, Beat.Swing, least: 3));
        }

        // A slide: the body thrown low and back over the leading leg, the string hand down on the floor behind, the bow
        // kept up ahead; held, and up again.
        static Clip Sliding(Pose rest)
        {
            var low = With(rest, (ref Pose p) =>
            {
                p.Lean = -3;
                p.Drop = 4.5f;
                p.Twist = 25;
                p.HandL = V(-5.5f, 4, 6.5f);
                p.HandR = V(5, -4.5f, 2);
                p.FootL = V(-2, 6, 0);
                p.FootR = V(3, 2, 0);
                p.Yaw = 20;
            });
            var held = With(low, (ref Pose p) =>
            {
                p.Lean = -3.5f;
                p.Drop = 5;
                p.FootL = V(-2, 6.5f, 0);
                p.HandR = V(5, -5, 1.5f);
            });
            return Build(rest,
                St(0.06f, low, Ease.Out, Beat.Swing, least: 3),
                St(0.15f, held, Ease.Even, least: 3),
                St(0.09f, rest, Ease.Both, Beat.Swing, least: 4));
        }

        // A gliding step: up off the floor and carried ahead upright, the staff drawn in close with its stone alight,
        // the free hand and the feet trailing; set down again. The afterimages are the screen's (WorldView).
        static Clip Gliding(Pose rest)
        {
            var lifted = With(rest, (ref Pose p) =>
            {
                p.Hop = 2;
                p.Lean = 2.5f;
                p.Drop = 0.5f;
                p.HandR = V(5, 2.5f, 8);
                p.HandL = V(-5.5f, -3, 6.5f);
                p.FootR = V(2, -2, 0.5f);
                p.FootL = V(-2, -3.5f, 1);
                p.Pitch = 70;
                p.Yaw = -60;
                p.Glow = 1;
            });
            var carried = With(lifted, (ref Pose p) =>
            {
                p.Hop = 2.5f;
                p.Lean = 3;
                p.FootL = V(-2, -4, 1.5f);
            });
            return Build(rest,
                St(0.05f, lifted, Ease.Out, Beat.Swing, least: 3),
                St(0.17f, carried, Ease.Even, least: 3),
                St(0.08f, With(rest, (ref Pose p) => p.Glow = 0.4f), Ease.Both, Beat.Swing, least: 4));
        }

        // ---- Drawing ----

        // Where things are in a frame, in canvas pixels: the hands, the weapon's far end (the hand again with none),
        // the top and bottom rows of the picture, and for something held to the mouth, whether the right hand is in
        // front of the body and which way up it is (degrees from pointing right, counterclockwise).
        public struct Joints
        {
            public float RightX, RightY, LeftX, LeftY, TipX, TipY;
            public int Top, Bottom;
            public bool RightInFront;
            public float RightAngle;
        }

        // The hero as they were before looks were picked, in a tunic of the given colour (the people of the world, a
        // player whose look is not known): CharacterLook.Default with that colour for its shirt.
        public static PixelImage Draw(Look look, HeroMove move, int frame, HeroGear gear, Rgba tunic, bool blink = false) => Draw(look, move, frame, gear, PaintOf(tunic), blink);

        // The hero in a look picked on the making screen (HeroArt.Looks.cs). Draws afresh each time: keep what it
        // returns under Key(style) with the rest of a sprite's key (Sprites), as the tunic's colour was kept.
        public static PixelImage Draw(Look look, HeroMove move, int frame, HeroGear gear, CharacterLook style, bool blink = false) => Draw(look, move, frame, gear, PaintOf(style), blink);

        static PixelImage Draw(Look look, HeroMove move, int frame, HeroGear gear, Paint paint, bool blink) =>
            move == HeroMove.Drink && gear.Kind != HeroWeapon.None ? Draw(look, move, frame, HeroGear.None, paint, blink) :
            Render(look, ClipOf(move, gear.Kind).Frames[Clamp(frame, move, gear.Kind)], gear, paint, blink, out _);

        public static Joints JointsOf(Look look, HeroMove move, int frame, HeroGear gear) =>
            move == HeroMove.Drink && gear.Kind != HeroWeapon.None ? JointsOf(look, move, frame, HeroGear.None) :
            Solve(look, ClipOf(move, gear.Kind).Frames[Clamp(frame, move, gear.Kind)], gear.Kind).Joints;

        // The figure alone, cut from the canvas (previews and sheets): standing still with nothing in hand it is
        // exactly this box.
        public static PixelImage Figure(PixelImage canvas)
        {
            var result = new PixelImage(16, Tall + 2);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                    result[x, y] = canvas[FeetX - 8 + x, FeetY - Tall - 2 + y];
            return result;
        }

        const float Deg = MathF.PI / 180;
        // How much of a step away from the viewer shows as a step up the screen: for where the body's own parts sit
        // (the drawing gives the views almost no depth), for the feet, for how far a hand reaches, for the weapon.
        const float BodyDepth = 0.2f, FootDepth = 0.35f, ReachDepth = 0.8f, WeaponDepth = 0.8f;
        // Half the head's width and a little.
        const float HeadHalf = 5.5f;
        const float ShoulderWide = 4.5f, ShoulderHigh = 6.5f, HipWide = 2.4f, HipHigh = 3, ArmLong = 5.2f;
        // The poses are written for the first, larger drawing: how much of their sideways and of their other measures
        // the smaller figure takes (its arms hang just outside the trunk, so sideways shrinks less), and how much of
        // a weapon's length.
        const float Sideways = 0.8f, Scale = 0.68f, GearScale = 0.78f;

        static (Look Look, bool Flip) WayOf(float degrees)
        {
            int sector = (int)MathF.Round(degrees / 45);
            return Ways[(sector % 8 + 8) % 8];
        }

        // Where everything is in a pose seen one way: what Render draws, worked out without drawing (the frames are
        // counted out by how far things move, Build).
        struct Rig
        {
            public Joints Joints;
            public int TrunkDx, TrunkDy, HeadDx, HeadDy;
            public Look TrunkLook, HeadLook;
            public bool TrunkFlip, HeadFlip;
            public (float X, float Y, float Depth) ShoulderR, ShoulderL, HandR, HandL, Grip;
            // The weapon's way on the screen, how much of its length shows, how far its middle is from the viewer.
            public float WayX, WayY, Reach, WeaponDepth;
            // Where a bow's string is drawn back to.
            public float NockX, NockY;
            public (float X, float Y, float Depth, float Hip) FootR, FootL;
            public float HipY;
            public int Toe;
        }

        static float LengthOf(HeroWeapon weapon) => weapon switch { HeroWeapon.Sword => 13, HeroWeapon.Staff => 11.5f, HeroWeapon.Pick => 10.5f, HeroWeapon.Bow => 2, _ => 0 };

        static Rig Solve(Look look, Pose pose, HeroWeapon weapon)
        {
            var rig = new Rig();
            pose.Lean *= Scale;
            pose.Shift *= Scale;
            pose.Drop *= Scale;
            pose.Hop *= Scale;
            pose.Nod *= Scale;
            pose.HandR = new V3(pose.HandR.S * Sideways, pose.HandR.F * Scale, pose.HandR.U * Scale);
            pose.HandL = new V3(pose.HandL.S * Sideways, pose.HandL.F * Scale, pose.HandL.U * Scale);
            pose.FootR = new V3(pose.FootR.S * Sideways, pose.FootR.F * Scale, pose.FootR.U * Scale);
            pose.FootL = new V3(pose.FootL.S * Sideways, pose.FootL.F * Scale, pose.FootL.U * Scale);
            float hips = (AngleOf(look) + pose.Pivot) * Deg;
            float fx = MathF.Cos(hips), fy = MathF.Sin(hips), rx = fy, ry = -fx;
            float trunkTurn = hips + pose.Twist * Deg;
            float tfx = MathF.Cos(trunkTurn), tfy = MathF.Sin(trunkTurn), trx = tfy, try_ = -tfx;

            // The trunk and the head carried over the feet.
            float carryX = fx * pose.Lean * 0.5f + rx * pose.Shift, carryY = fy * pose.Lean * 0.5f + ry * pose.Shift;
            float headX = fx * pose.Lean + rx * pose.Shift, headY = fy * pose.Lean + ry * pose.Shift;
            float rise = pose.Hop - pose.Drop;
            rig.TrunkDx = Round(carryX);
            rig.TrunkDy = Round(-carryY * BodyDepth - rise);
            rig.HeadDx = Round(headX);
            rig.HeadDy = Round(-headY * BodyDepth - rise + pose.Nod);
            (rig.TrunkLook, rig.TrunkFlip) = WayOf(AngleOf(look) + pose.Pivot + pose.Twist);
            (rig.HeadLook, rig.HeadFlip) = WayOf(AngleOf(look) + pose.Pivot + pose.Head);

            // Shoulders, on the turned trunk, a little behind its middle.
            (float X, float Y, float Depth) Shoulder(float side)
            {
                float gx = carryX + trx * side - tfx * 0.5f, gy = carryY + try_ * side - tfy * 0.5f;
                return (FeetX + gx, FeetY - 1 - (ShoulderHigh + rise) - gy * BodyDepth, gy);
            }
            rig.ShoulderR = Shoulder(ShoulderWide);
            rig.ShoulderL = Shoulder(-ShoulderWide);

            // A hand: placed in the hips' terms and carried with the trunk, its reach toward or away from the viewer
            // shown from the shoulder; never further from the shoulder than the arm is long.
            (float X, float Y, float Depth) Hand(V3 at, (float X, float Y, float Depth) shoulder, float side)
            {
                float gx = carryX + rx * at.S + fx * at.F, gy = carryY + ry * at.S + fy * at.F;
                float x = FeetX + gx;
                float y = shoulder.Y + (ShoulderHigh - at.U) - (gy - shoulder.Depth) * ReachDepth;
                float dx = x - shoulder.X, dy = y - shoulder.Y, length = MathF.Sqrt(dx * dx + dy * dy);
                if (length > ArmLong)
                {
                    x = shoulder.X + dx / length * ArmLong;
                    y = shoulder.Y + dy / length * ArmLong;
                }
                // Reaching away from the viewer the hand would be lost behind the head: it goes out past the head on
                // its own side (more the further away it reaches), so what it holds shows beside the head.
                float away = Math.Max(0, Math.Min(1, (gy - Behind) / 3));
                float toSide = Math.Max(-1, Math.Min(1, rx * side * 3));
                float clear = HeadHalf * away;
                float pushed = toSide > 0 ? MathF.Max(x, FeetX + clear) : MathF.Min(x, FeetX - clear);
                x += (pushed - x) * MathF.Abs(toSide);
                return Within(shoulder, (x, y, gy), ArmLong + 1);
            }
            var handR = Hand(pose.HandR, rig.ShoulderR, 1);
            var handL = Hand(pose.HandL, rig.ShoulderL, -1);

            // The weapon's way on the screen from where it points in the hips' terms, shorter as it points at the
            // viewer or away; and how far away from the viewer it leads.
            float yaw = pose.Yaw * Deg, pitch = pose.Pitch * Deg;
            float ahead = MathF.Cos(pitch) * MathF.Cos(yaw), aside = -MathF.Cos(pitch) * MathF.Sin(yaw), up = MathF.Sin(pitch);
            float wayGx = rx * aside + fx * ahead, wayGy = ry * aside + fy * ahead;
            float wx = wayGx, wy = -wayGy * WeaponDepth - up;
            float shown = MathF.Sqrt(wx * wx + wy * wy);
            if (shown < 1e-3f) (wx, wy, shown) = (0, -1, 0.5f);
            else (wx, wy) = (wx / shown, wy / shown);
            float reach = MathF.Max(0.25f, MathF.Min(1, shown));
            rig.WayX = wx;
            rig.WayY = wy;
            rig.Reach = reach;

            bool bow = weapon == HeroWeapon.Bow;
            var grip = bow ? handL : handR;
            // Drawing a bow, the string hand is on the arrow's line behind the bow hand, as far back as it is drawn.
            // Seen from behind (the bow out past the head, away from the viewer) the string hand stays by the cheek, where
            // the head hides the string anyway.
            if (bow)
            {
                float draw = Math.Max(0, Math.Min(1, pose.Draw));
                float back = (2.6f + 1.4f * draw + 6.5f * pose.Draw) * GearScale * MathF.Max(0.6f, reach);
                rig.NockX = grip.X - wx * back;
                rig.NockY = grip.Y - wy * back;
                float on = MathF.Min(1, pose.Draw * 3) * Math.Max(0, Math.Min(1, 1 - (grip.Depth - Behind) / 3));
                handR = Within(rig.ShoulderR, (handR.X + (rig.NockX - handR.X) * on, handR.Y + (rig.NockY - handR.Y) * on, handR.Depth), ArmLong + 1.5f);
            }
            // The other hand on the haft as well.
            if (pose.Both > 0 && weapon != HeroWeapon.None && !bow)
            {
                float along = (weapon == HeroWeapon.Sword ? -2 : 4) * GearScale;
                float tx = handR.X + wx * along * reach, ty = handR.Y + wy * along * reach;
                handL = (handL.X + (tx - handL.X) * pose.Both, handL.Y + (ty - handL.Y) * pose.Both, handL.Depth + (handR.Depth - handL.Depth) * pose.Both);
                // As far along it as the arm reaches.
                handL = Within(rig.ShoulderL, handL, ArmLong + 1);
            }
            rig.HandR = handR;
            rig.HandL = handL;
            rig.Grip = grip;
            rig.WeaponDepth = grip.Depth + wayGy * 4.5f;

            (float X, float Y, float Depth, float Hip) Foot(V3 at, float side)
            {
                float gx = rx * at.S + fx * at.F, gy = ry * at.S + fy * at.F;
                float hip = FeetX + carryX * 0.5f + rx * side;
                return (FeetX + gx, FeetY - 1 - at.U - pose.Hop - gy * FootDepth, gy, hip);
            }
            rig.FootR = Foot(pose.FootR, HipWide);
            rig.FootL = Foot(pose.FootL, -HipWide);
            rig.HipY = FeetY - 1 - (HipHigh + rise);
            rig.Toe = MathF.Abs(fx) > 0.5f ? (fx > 0 ? 1 : -1) : 0;

            float length_ = LengthOf(weapon) * GearScale;
            var joints = new Joints
            {
                RightX = handR.X, RightY = handR.Y, LeftX = handL.X, LeftY = handL.Y,
                TipX = grip.X + wx * length_ * reach, TipY = grip.Y + wy * length_ * reach,
                RightInFront = ArmDepth(rig.ShoulderR, handR) <= Behind,
                RightAngle = MathF.Atan2(-wy, wx) / Deg,
                // The top of the hair's outline and the floor under the lower foot.
                Top = HeadY + rig.HeadDy - 1,
                Bottom = Round(MathF.Max(rig.FootR.Y, rig.FootL.Y)),
            };
            if (pose.Tip != 0)
            {
                (joints.RightX, joints.RightY) = Turned(joints.RightX, joints.RightY, pose.Tip);
                (joints.LeftX, joints.LeftY) = Turned(joints.LeftX, joints.LeftY, pose.Tip);
                (joints.TipX, joints.TipY) = Turned(joints.TipX, joints.TipY, pose.Tip);
                var (_, top) = Turned(FeetX, joints.Top, pose.Tip);
                joints.Top = Round(MathF.Min(top, FeetY - 9));
                joints.Bottom = FeetY - 1;
            }
            if (Rolls(pose.Roll))
            {
                var m = RollOf(look, pose.Roll, false);
                (joints.RightX, joints.RightY) = m.Put(joints.RightX, joints.RightY);
                (joints.LeftX, joints.LeftY) = m.Put(joints.LeftX, joints.LeftY);
                (joints.TipX, joints.TipY) = m.Put(joints.TipX, joints.TipY);
                joints.Top = Round(RollY - 7);
                joints.Bottom = FeetY - 1;
            }
            rig.Joints = joints;
            return rig;
        }

        // How far from the viewer an arm is, for whether it shows in front of the body: mostly where its hand is.
        static float ArmDepth((float X, float Y, float Depth) shoulder, (float X, float Y, float Depth) hand) => shoulder.Depth * 0.3f + hand.Depth * 0.7f;

        // A hand no further from its shoulder than given.
        static (float X, float Y, float Depth) Within((float X, float Y, float Depth) shoulder, (float X, float Y, float Depth) hand, float most)
        {
            float dx = hand.X - shoulder.X, dy = hand.Y - shoulder.Y, length = MathF.Sqrt(dx * dx + dy * dy);
            return length <= most ? hand : (shoulder.X + dx / length * most, shoulder.Y + dy / length * most, hand.Depth);
        }

        // Things further from the viewer than this (pixels behind the body's middle) are drawn behind the body.
        const float Behind = 1;

        static PixelImage Render(Look look, Pose pose, HeroGear gear, Paint paint, bool blink, out Joints joints)
        {
            if (!Rolls(pose.Roll)) return Upright(look, pose, gear, paint, blink, out joints);
            // Rolled over: the tucked body drawn as it stands and turned over forwards. Once it is past lying flat its
            // other side shows, which is the view from the opposite way.
            joints = Solve(look, pose, gear.Kind).Joints;
            var m = RollOf(look, pose.Roll);
            var plain = pose;
            plain.Roll = 0;
            return Rolled(Upright(m.Back ? Opposite(look) : look, plain, gear, paint, blink, out _), m);
        }

        static PixelImage Upright(Look look, Pose pose, HeroGear gear, Paint paint, bool blink, out Joints joints)
        {
            var colors = paint.Colors;
            bool shut = blink || pose.Shut > 0.5f;
            var rig = Solve(look, pose, gear.Kind);
            joints = rig.Joints;

            var canvas = new PixelImage(Size, Size);
            var layers = new List<(float Depth, int Order, PixelImage Image)>();
            if (gear.Kind != HeroWeapon.None)
            {
                var weapon = new PixelImage(Size, Size);
                Weapon(weapon, gear, rig.Grip.X, rig.Grip.Y, rig.WayX, rig.WayY, rig.Reach, pose, rig.NockX, rig.NockY);
                layers.Add((rig.WeaponDepth, 0, weapon));
            }
            var armR = new PixelImage(Size, Size);
            Arm(armR, rig.ShoulderR.X, rig.ShoulderR.Y, rig.HandR.X, rig.HandR.Y, colors);
            layers.Add((ArmDepth(rig.ShoulderR, rig.HandR), 1, armR));
            var armL = new PixelImage(Size, Size);
            Arm(armL, rig.ShoulderL.X, rig.ShoulderL.Y, rig.HandL.X, rig.HandL.Y, colors);
            layers.Add((ArmDepth(rig.ShoulderL, rig.HandL), 1, armL));

            // On each side of the body the weapon goes under the arms, so the hand closes over its grip; the arms far
            // one first.
            layers.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : b.Depth.CompareTo(a.Depth));
            foreach (var layer in layers)
                if (layer.Depth > Behind) Stamp(canvas, layer.Image, false);

            // Legs: from the hips down to each foot, the far one first.
            var legs = new PixelImage(Size, Size);
            var (far, near) = rig.FootR.Depth > rig.FootL.Depth ? (rig.FootR, rig.FootL) : (rig.FootL, rig.FootR);
            Leg(legs, far.Hip, rig.HipY, far.X, far.Y, rig.Toe, colors['q']);
            Leg(legs, near.Hip, rig.HipY, near.X, near.Y, rig.Toe, colors['p']);
            Stamp(canvas, legs, false);

            // The trunk and the head, each in the view its own turn calls for.
            var trunk = new PixelImage(Size, Size);
            Part(trunk, colors, TrunkRows(rig.TrunkLook), rig.TrunkFlip, TrunkX + rig.TrunkDx, TrunkY + rig.TrunkDy, false);
            Stamp(canvas, trunk, true);
            var head = new PixelImage(Size, Size);
            // The hair may stand above the head's eight rows and hang below them, over the trunk (HeroArt.Looks.cs).
            Part(head, colors, HeadRows(paint.Hair, rig.HeadLook), rig.HeadFlip, HeadX + rig.HeadDx, HeadY + rig.HeadDy - HairUp(paint.Hair), shut);
            Stamp(canvas, head, false);

            foreach (var layer in layers)
                if (layer.Depth <= Behind) Stamp(canvas, layer.Image, false);

            return pose.Tip != 0 ? Tipped(canvas, pose.Tip) : canvas;
        }

        static int Round(float value) => (int)MathF.Floor(value + 0.5f);

        // A drawn part (rows of palette letters) at a place on a layer, mirrored about the canvas's middle if asked.
        static void Part(PixelImage layer, Dictionary<char, Rgba> colors, string[] rows, bool flip, int left, int top, bool shut)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[flip ? row.Length - 1 - x : x];
                    if (c == '.') continue;
                    if (c == 'e' && shut) c = 'r';
                    int px = left + x, py = top + y;
                    if (layer.Inside(px, py)) layer[px, py] = colors[c];
                }
            }
        }

        // A layer onto the canvas with a dark line round it. The trunk's line is left off the legs and what it merges
        // with below, so the tunic's hem sits straight on the trousers.
        static void Stamp(PixelImage canvas, PixelImage layer, bool softBelow)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (layer.IsSet(x, y))
                    {
                        canvas[x, y] = layer[x, y];
                        continue;
                    }
                    bool edge = x > 0 && layer.IsSet(x - 1, y) || x < Size - 1 && layer.IsSet(x + 1, y) || y > 0 && layer.IsSet(x, y - 1) || y < Size - 1 && layer.IsSet(x, y + 1);
                    if (!edge) continue;
                    if (softBelow && canvas.IsSet(x, y) && y > 0 && layer.IsSet(x, y - 1) && !(x > 0 && layer.IsSet(x - 1, y)) && !(x < Size - 1 && layer.IsSet(x + 1, y))) continue;
                    canvas[x, y] = Ink;
                }
            }
        }

        // Every pixel whose middle is within a radius of the line.
        static void Line(PixelImage image, float x0, float y0, float x1, float y1, float radius, Rgba color)
        {
            int left = (int)MathF.Floor(MathF.Min(x0, x1) - radius), right = (int)MathF.Ceiling(MathF.Max(x0, x1) + radius);
            int top = (int)MathF.Floor(MathF.Min(y0, y1) - radius), bottom = (int)MathF.Ceiling(MathF.Max(y0, y1) + radius);
            float dx = x1 - x0, dy = y1 - y0, lengthSquared = dx * dx + dy * dy;
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (!image.Inside(x, y)) continue;
                    float px = x + 0.5f - x0, py = y + 0.5f - y0;
                    float t = lengthSquared < 1e-6f ? 0 : Math.Max(0, Math.Min(1, (px * dx + py * dy) / lengthSquared));
                    float ex = px - dx * t, ey = py - dy * t;
                    if (ex * ex + ey * ey <= radius * radius) image[x, y] = color;
                }
            }
        }

        // A line one pixel wide.
        static void Thin(PixelImage image, float x0, float y0, float x1, float y1, Rgba color)
        {
            int steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int x = (int)MathF.Floor(x0 + (x1 - x0) * t), y = (int)MathF.Floor(y0 + (y1 - y0) * t);
                if (image.Inside(x, y)) image[x, y] = color;
            }
        }

        static void Dot(PixelImage image, float x, float y, Rgba color)
        {
            int px = (int)MathF.Floor(x), py = (int)MathF.Floor(y);
            if (image.Inside(px, py)) image[px, py] = color;
        }

        // A bare arm a pixel thick from under the sleeve to the hand (its outline makes it three), the hand in shade.
        static void Arm(PixelImage image, float x0, float y0, float x1, float y1, Dictionary<char, Rgba> colors)
        {
            Thin(image, x0, y0, x1, y1, colors['s']);
            Dot(image, x1, y1, colors['S']);
        }

        // A leg from the hip to the foot: trousers two pixels wide, a boot under them with a toe the way they face.
        static void Leg(PixelImage image, float hipX, float hipY, float footX, float footY, int toe, Rgba trousers)
        {
            int x = Round(footX), y = Round(footY), hx = Round(hipX), hy = Round(hipY);
            if (y - 1 > hy) Line(image, hx, hy, x, y - 1, 0.95f, trousers);
            image.Fill(x - 1, y - 1, 2, 1, Colors['O']);
            if (toe != 0 && image.Inside(x + (toe > 0 ? 0 : -1), y - 1)) image[x + (toe > 0 ? 0 : -1), y - 1] = Colors['o'];
        }

        static readonly Rgba Wood = Rgba.Hex("#8E5C34"), WoodDark = Rgba.Hex("#5E3C20"), GripRed = Rgba.Hex("#A8442E"), Gold = Rgba.Hex("#F2CC5C"), GoldDark = Rgba.Hex("#B88A2A"),
            String = Rgba.Hex("#ECE4D2"), White = Rgba.Hex("#FFFFFF");

        // The weapon from the hand that holds it, along its way on the screen.
        static void Weapon(PixelImage image, HeroGear gear, float hx, float hy, float wx, float wy, float reach, Pose pose, float stringX, float stringY)
        {
            float nx = -wy, ny = wx;
            (float X, float Y) At(float along, float across = 0) => (hx + wx * along * GearScale * reach + nx * across * GearScale, hy + wy * along * GearScale * reach + ny * across * GearScale);
            void Thick(float from, float to, float radius, Rgba color, float across = 0)
            {
                var a = At(from, across);
                var b = At(to, across);
                Line(image, a.X, a.Y, b.X, b.Y, radius, color);
            }
            void Fine(float from, float fromAcross, float to, float toAcross, Rgba color)
            {
                var a = At(from, fromAcross);
                var b = At(to, toAcross);
                Thin(image, a.X, a.Y, b.X, b.Y, color);
            }
            switch (gear.Kind)
            {
                case HeroWeapon.Sword:
                {
                    Thick(3, 12, 0.75f, gear.Main);
                    Fine(3, 0.45f, 12.4f, 0.2f, gear.Light);
                    Fine(3, -0.6f, 11, -0.6f, gear.Shade);
                    var tip = At(12.8f);
                    Dot(image, tip.X, tip.Y, gear.Light);
                    Thick(-2.5f, 1.5f, 0.7f, GripRed);
                    var pommel = At(-3.2f);
                    Dot(image, pommel.X, pommel.Y, GoldDark);
                    Fine(2.2f, -2.6f, 2.2f, 2.6f, Gold);
                    break;
                }
                case HeroWeapon.Staff:
                {
                    Thick(-5.5f, 8, 0.7f, WoodDark);
                    Fine(-5.5f, 0.3f, 8, 0.3f, Wood);
                    var stone = At(9.6f);
                    Line(image, stone.X, stone.Y, stone.X, stone.Y, 1.6f, gear.Main);
                    Line(image, stone.X, stone.Y, stone.X, stone.Y, 0.9f, pose.Glow > 0.5f ? gear.Gem.Mix(White, 0.5f) : gear.Gem);
                    Dot(image, stone.X + 0.6f, stone.Y + 0.6f, pose.Glow > 0.5f ? gear.Gem : gear.GemDark);
                    if (pose.Glow > 0.25f)
                    {
                        // Rays about the stone as it gathers.
                        float far = pose.Glow > 0.75f ? 3.2f : 2.6f;
                        foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
                            Dot(image, stone.X + dx * far, stone.Y + dy * far, gear.Gem.Mix(White, 0.35f));
                        if (pose.Glow > 0.75f) Dot(image, stone.X, stone.Y, White);
                    }
                    break;
                }
                case HeroWeapon.Pick:
                {
                    Thick(-3.5f, 9, 0.7f, WoodDark);
                    Fine(-3.5f, 0.3f, 9, 0.3f, Wood);
                    var top = At(9.6f);
                    foreach (int side in new[] { -1, 1 })
                    {
                        var end = At(7.4f, side * 4.3f);
                        var mid = At(9.4f, side * 2.2f);
                        Line(image, top.X, top.Y, mid.X, mid.Y, 0.95f, gear.Main);
                        Line(image, mid.X, mid.Y, end.X, end.Y, 0.75f, gear.Main);
                        var outer = At(10.1f, side * 2f);
                        Thin(image, top.X + wx * 0.6f, top.Y + wy * 0.6f, outer.X, outer.Y, gear.Light);
                        Dot(image, end.X, end.Y, gear.Shade);
                    }
                    break;
                }
                default:
                {
                    // A bow: the limbs bent back from the hand, more as it is drawn; the string from tip to tip by way
                    // of the string hand as far as it is drawn; an arrow from the string out past the hand.
                    float draw = Math.Max(0, Math.Min(1, pose.Draw));
                    float sweep = 2.6f + 1.4f * draw;
                    float span = 6.5f - 0.5f * draw;
                    var tipA = At(-sweep, -span);
                    var tipB = At(-sweep, span);
                    var kneeA = At(0.4f - 0.3f * draw, -span * 0.55f);
                    var kneeB = At(0.4f - 0.3f * draw, span * 0.55f);
                    var belly = At(1);
                    float nockX = stringX, nockY = stringY;
                    Thin(image, tipA.X, tipA.Y, nockX, nockY, String);
                    Thin(image, tipB.X, tipB.Y, nockX, nockY, String);
                    if (pose.Arrow > 0.5f)
                    {
                        float ax = wx, ay = wy;
                        float shaft = 10.5f * GearScale * MathF.Max(0.6f, reach);
                        Thin(image, nockX, nockY, nockX + ax * shaft, nockY + ay * shaft, Wood);
                        Dot(image, nockX, nockY, String);
                        Line(image, nockX + ax * shaft, nockY + ay * shaft, nockX + ax * (shaft + 1), nockY + ay * (shaft + 1), 0.75f, gear.Light);
                    }
                    Line(image, tipA.X, tipA.Y, kneeA.X, kneeA.Y, 0.7f, Wood);
                    Line(image, kneeA.X, kneeA.Y, belly.X, belly.Y, 0.7f, WoodDark);
                    Line(image, belly.X, belly.Y, kneeB.X, kneeB.Y, 0.7f, WoodDark);
                    Line(image, kneeB.X, kneeB.Y, tipB.X, tipB.Y, 0.7f, Wood);
                    Dot(image, tipA.X, tipA.Y, gear.Light);
                    Dot(image, tipB.X, tipB.Y, gear.Light);
                    Dot(image, belly.X, belly.Y, GripRed);
                    break;
                }
            }
        }

        // ---- Rolling over forwards (Pose.Roll) ----

        // The middle of the tucked body, which the roll turns about.
        const float RollX = FeetX, RollY = FeetY - 7;
        // How much of a step away from the viewer shows as a step up the screen for the rolling body: what keeps it
        // from being seen edge on as it goes over toward the viewer or away.
        const float RollDepth = 0.45f;

        static bool Rolls(float degrees)
        {
            float turn = degrees % 360;
            return MathF.Abs(turn) > 0.5f && MathF.Abs(turn) < 359.5f;
        }

        static Look Opposite(Look look) => look switch { Look.Down => Look.Up, Look.DownSide => Look.UpSide, Look.UpSide => Look.DownSide, Look.Up => Look.Down, _ => Look.Side };

        // Where a point of the upright picture goes on the screen once the body is rolled over (X, Y from the middle,
        // down the screen), and whether the body's other side shows.
        readonly struct RollTurn
        {
            public readonly float A, B, C, D;
            public readonly bool Back;

            public RollTurn(float a, float b, float c, float d)
            {
                (A, B, C, D) = (a, b, c, d);
                Back = a * d - b * c < 0;
            }

            public (float X, float Y) Put(float x, float y)
            {
                float dx = x - RollX, dy = y - RollY;
                return (RollX + A * dx + B * dy, RollY + C * dx + D * dy);
            }
        }

        // The body is a flat picture standing on the floor, turned head first toward the way it goes about the line
        // across that way. To the side this is the picture turning on the screen; toward the viewer or away it is the
        // picture closing flat and opening upside down with its other side showing. The diagonal views roll mostly
        // sideways (a picture turned about a diagonal line would be edge on halfway).
        static RollTurn RollOf(Look look, float degrees, bool drawn = true)
        {
            float way = look switch { Look.Down => -90, Look.DownSide => -20, Look.Side => 0, Look.UpSide => 20, _ => 90 } * Deg;
            float tx = MathF.Cos(way), td = MathF.Sin(way);
            float c = MathF.Cos(degrees * Deg), s = MathF.Sin(degrees * Deg);
            // Right and up the screen from right and up the picture.
            float xx = td * td + tx * tx * c, xu = tx * s;
            float ux = -tx * s - RollDepth * td * tx * (1 - c), uu = c + RollDepth * td * s;
            // Never much thinner than half: lying flat it is still a body, not a line.
            // (Where the hands are is left to pass through the line, so they do not jump from frame to frame.)
            if (drawn && MathF.Abs(tx) < 0.01f && MathF.Abs(uu) < 0.6f) uu = uu < 0 ? -0.6f : 0.6f;
            return new RollTurn(xx, -xu, -ux, uu);
        }

        // The picture rolled over (each pixel taken from where it came from; the other side's picture mirrored, as it
        // is seen from behind).
        static PixelImage Rolled(PixelImage image, RollTurn m)
        {
            var result = new PixelImage(Size, Size);
            float det = m.A * m.D - m.B * m.C;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - RollX, dy = y + 0.5f - RollY;
                    float sx = (m.D * dx - m.B * dy) / det, sy = (-m.C * dx + m.A * dy) / det;
                    if (m.Back) sx = -sx;
                    int px = (int)MathF.Floor(RollX + sx), py = (int)MathF.Floor(RollY + sy);
                    if (image.Inside(px, py) && image.IsSet(px, py)) result[x, y] = image[px, py];
                }
            }
            return result;
        }

        // The floor point the body tips over about, and how far the picture slides back as it goes down so it lies
        // across where it stood.
        const float TipX = FeetX + 3, TipY = FeetY - 2, TipSlide = 7.5f;

        static (float X, float Y) Turned(float x, float y, float degrees)
        {
            float a = degrees * Deg, c = MathF.Cos(a), s = MathF.Sin(a);
            float dx = x - TipX, dy = y - TipY;
            return (TipX + dx * c - dy * s - TipSlide * degrees / 90, TipY + dx * s + dy * c + degrees / 90);
        }

        // The picture turned clockwise about the tipping point (each pixel taken from where it came from).
        static PixelImage Tipped(PixelImage image, float degrees)
        {
            var result = new PixelImage(Size, Size);
            float a = degrees * Deg, c = MathF.Cos(a), s = MathF.Sin(a);
            float slide = TipSlide * degrees / 90, sink = degrees / 90;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f + slide - TipX, dy = y + 0.5f - sink - TipY;
                    int sx = (int)MathF.Floor(TipX + dx * c + dy * s), sy = (int)MathF.Floor(TipY - dx * s + dy * c);
                    if (image.Inside(sx, sy)) result[x, y] = image[sx, sy];
                }
            }
            return result;
        }
    }
}
