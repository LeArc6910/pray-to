using System.Collections.Generic;

namespace Game.Client
{
    // The three combo steps of the weapon kinds that strike in their own way (Docs/Weapons.md 묶음 B, HeroStyle). As
    // with the four of old the blow lands early (the attack event comes as it lands, Docs/Combat.md 시간 맞추기) and
    // a kind's weight shows after it: a heavy one is held long where it struck and hauled home slowly, a light one
    // is back in a blink. Each clip ends within its kind's time between attacks (weapon_kinds.csv speed: 0.5 s at 1).
    static partial class HeroArt
    {
        static Clip KindAttack(HeroStyle style, int step, Pose rest) => style switch
        {
            HeroStyle.Dagger => step == 2 ? DaggerSpin(rest) : DaggerStab(rest, step == 1),
            HeroStyle.Greatsword => step == 0 ? Cleave(rest) : step == 1 ? Heave(rest) : GreatTurn(rest),
            HeroStyle.Spear => step == 0 ? Lance(rest, false) : step == 1 ? HaftSweep(rest) : Lance(rest, true),
            HeroStyle.Hammer => step == 0 ? Smash(rest) : step == 1 ? Swat(rest) : LeapSmash(rest),
            HeroStyle.Crossbow => Loose(rest, step),
            HeroStyle.Boomerang => Throw(rest, step),
            HeroStyle.Wand => Flick(rest, step),
            _ => Push(rest, step),
        };

        // Dagger (0.31 s between attacks): a quick stab from the hip, a backhand stab from the other side, and a
        // spin on the heel with the blade out.
        static Clip DaggerStab(Pose rest, bool backhand)
        {
            var cocked = With(rest, (ref Pose p) =>
            {
                p.HandR = backhand ? V(-1.5f, 1.5f, 9) : V(5.5f, -1, 7.5f);
                p.HandL = V(-5, 1, 7);
                p.Yaw = backhand ? 70 : 0;
                p.Pitch = 15;
                p.Twist = backhand ? 30 : -25;
                p.Lean = -0.5f;
            });
            var stabbed = With(rest, (ref Pose p) =>
            {
                p.HandR = backhand ? V(3, 9, 8.5f) : V(4.5f, 9, 8);
                p.HandL = V(-5, -1, 7);
                p.Yaw = backhand ? -15 : 0;
                p.Pitch = 0;
                p.Twist = backhand ? -20 : 20;
                p.Lean = 2.5f;
                p.Drop = 1;
                p.FootL = backhand ? V(-3, -2, 0) : V(-3, 4, 0);
                p.FootR = backhand ? V(3, 4, 0) : V(3, -2, 0);
            });
            return Build(rest,
                St(0.025f, cocked, Ease.Out, Beat.Swing, least: 2),
                St(0.02f, stabbed, Ease.In, Beat.Strike, chain: true, least: 2),
                St(0.05f, With(stabbed, (ref Pose p) => { p.HandR.F -= 1.5f; p.Lean = 2; })),
                St(0.13f, rest, Ease.Both, least: 4));
        }

        static Clip DaggerSpin(Pose rest)
        {
            var wound = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, -2, 8);
                p.HandL = V(-3, 3, 8);
                p.Yaw = -120;
                p.Pitch = 5;
                p.Twist = -40;
                p.Drop = 1.5f;
            });
            var turning = With(rest, (ref Pose p) =>
            {
                p.Pivot = 180;
                p.HandR = V(7.5f, 1, 8.5f);
                p.HandL = V(-6, -1, 8);
                p.Yaw = -90;
                p.Pitch = 0;
                p.Hop = 1;
                p.FootR = V(3.5f, 1, 1);
                p.FootL = V(-3.5f, -1, 0);
            });
            var round = With(turning, (ref Pose p) =>
            {
                p.Pivot = 360;
                p.HandR = V(5, 6.5f, 8);
                p.Yaw = -20;
                p.Twist = 20;
                p.Lean = 2;
                p.Hop = 0;
                p.FootR = V(4, 3, 0);
                p.FootL = V(-4, -2, 0);
            });
            var home = With(rest, (ref Pose p) => p.Pivot = 360);
            return Build(rest,
                St(0.03f, wound, Ease.Out, Beat.Swing, least: 3),
                St(0.03f, turning, Ease.Even, Beat.Swing, chain: true, least: 4),
                St(0.025f, round, Ease.Even, Beat.Strike, least: 4),
                St(0.06f, With(round, (ref Pose p) => { p.Lean = 1.5f; p.Drop = 1; })),
                St(0.06f, With(Upright(rest, round), (ref Pose p) => p.Pivot = 360), least: 3),
                St(0.07f, home, Ease.Out, least: 3));
        }

        // Greatsword (0.83 s): two hands always; hauled high over the head and cleft down into the floor, heaved back
        // up across the body, and a whole turn with the blade out level. Each is held where it ends, heavy.
        static Clip Cleave(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, -1.5f, 15.5f);
                p.Yaw = 0;
                p.Pitch = 135;
                p.Both = 1;
                p.Twist = -10;
                p.Lean = -2.5f;
                p.Drop = -1;
                p.FootL = V(-3, 2, 0);
            });
            var struck = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3.5f, 7.5f, 6);
                p.Yaw = 0;
                p.Pitch = -30;
                p.Both = 1;
                p.Drop = 3;
                p.Lean = 3.5f;
                p.FootL = V(-4, 5, 0);
                p.FootR = V(4, -3, 0);
            });
            return Build(rest,
                St(0.06f, raised, Ease.Out, Beat.Swing, least: 5),
                St(0.04f, With(struck, (ref Pose p) => { p.HandR = V(3.5f, 5, 13); p.Pitch = 55; p.Drop = 0; p.Lean = 1; }), Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.035f, struck, Ease.Even, Beat.Strike, least: 3),
                St(0.2f, With(struck, (ref Pose p) => { p.Pitch = -27; p.Lean = 3; p.Drop = 2.5f; }), least: 3),
                St(0.15f, Upright(rest, struck), Ease.Both, least: 5),
                St(0.15f, rest, Ease.Out, least: 4));
        }

        static Clip Heave(Pose rest)
        {
            var low = With(rest, (ref Pose p) =>
            {
                p.HandR = V(-3, 4, 4.5f);
                p.Yaw = 80;
                p.Pitch = -15;
                p.Both = 1;
                p.Twist = 40;
                p.Lean = 2;
                p.Drop = 2.5f;
                p.FootL = V(-3, 3, 0);
                p.FootR = V(3, -2, 0);
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, 7.5f, 9);
                p.Yaw = 0;
                p.Pitch = 20;
                p.Both = 1;
                p.Lean = 2.5f;
                p.Drop = 0.5f;
                p.FootR = V(3, 3, 0);
                p.FootL = V(-3, -1, 0);
            });
            var high = With(through, (ref Pose p) =>
            {
                p.HandR = V(5, 2, 15);
                p.Yaw = -70;
                p.Pitch = 80;
                p.Twist = -40;
                p.Lean = 1;
                p.Drop = -1;
                p.Hop = 1;
            });
            return Build(rest,
                St(0.06f, low, Ease.Out, Beat.Swing, least: 4),
                St(0.04f, through, Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.035f, high, Ease.Even, Beat.Strike, least: 3),
                St(0.18f, With(high, (ref Pose p) => { p.Hop = 0; p.Drop = 0.5f; p.Pitch = 85; })),
                St(0.26f, rest, Ease.Both, least: 7));
        }

        static Clip GreatTurn(Pose rest)
        {
            var wound = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, -3, 8);
                p.Yaw = -150;
                p.Pitch = 15;
                p.Both = 1;
                p.Twist = -50;
                p.Drop = 2.5f;
                p.Lean = -1.5f;
                p.FootR = V(4, -2, 0);
                p.FootL = V(-3, 2, 0);
            });
            var turning = With(rest, (ref Pose p) =>
            {
                p.Pivot = 180;
                p.HandR = V(7, 2, 8.5f);
                p.Yaw = -90;
                p.Pitch = 2;
                p.Both = 1;
                p.Drop = 1.5f;
                p.FootR = V(4, 1, 0);
                p.FootL = V(-4, -1, 0);
            });
            var round = With(turning, (ref Pose p) =>
            {
                p.Pivot = 360;
                p.HandR = V(4, 7, 8);
                p.Yaw = -15;
                p.Pitch = -2;
                p.Twist = 25;
                p.Lean = 2.5f;
                p.Drop = 2.5f;
                p.FootR = V(4.5f, 3, 0);
                p.FootL = V(-4.5f, -3, 0);
            });
            var home = With(rest, (ref Pose p) => p.Pivot = 360);
            return Build(rest,
                St(0.06f, wound, Ease.Out, Beat.Swing, least: 5),
                St(0.05f, turning, Ease.Even, Beat.Swing, chain: true, least: 6),
                St(0.04f, round, Ease.Even, Beat.Strike, least: 6),
                St(0.2f, With(round, (ref Pose p) => { p.Lean = 2; p.Drop = 2; })),
                St(0.15f, With(Upright(rest, round), (ref Pose p) => p.Pivot = 360), Ease.Both, least: 5),
                St(0.14f, home, Ease.Out, least: 4));
        }

        // Spear (0.59 s): drawn back and thrust level with a step, the haft swung across in a wide sweep, and the
        // finisher drawn far back crouched and driven deep in a lunge.
        static Clip Lance(Pose rest, bool deep)
        {
            var drawn = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4.5f, deep ? -4 : -2.5f, 8);
                p.Yaw = 0;
                p.Pitch = 3;
                p.Both = 1;
                p.Twist = deep ? -40 : -25;
                p.Lean = deep ? -2 : -1;
                p.Drop = deep ? 2 : 0;
                p.FootL = V(-3, 1.5f, 0);
            });
            var thrust = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, deep ? 11 : 8, deep ? 7.5f : 8.5f);
                p.Yaw = 0;
                p.Pitch = 0;
                p.Both = 1;
                p.Twist = 15;
                p.Lean = deep ? 5 : 3;
                p.Drop = deep ? 2.5f : 1;
                p.FootL = V(-3.5f, deep ? 7 : 4, 0);
                p.FootR = V(3.5f, deep ? -4 : -2, 0);
            });
            return Build(rest,
                St(deep ? 0.05f : 0.035f, drawn, Ease.Out, Beat.Swing, least: 3),
                St(deep ? 0.03f : 0.025f, thrust, Ease.In, Beat.Strike, chain: true, least: 2),
                St(deep ? 0.12f : 0.08f, With(thrust, (ref Pose p) => { p.HandR.F -= 1.5f; p.Lean -= 0.5f; })),
                St(deep ? 0.25f : 0.22f, rest, Ease.Both, least: 7));
        }

        static Clip HaftSweep(Pose rest)
        {
            var back = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, -1, 9);
                p.Yaw = -110;
                p.Pitch = 5;
                p.Both = 1;
                p.Twist = -40;
                p.Drop = 1;
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, 6, 9);
                p.Yaw = 0;
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
                p.HandR = V(-3, 4, 9);
                p.Yaw = 100;
                p.Twist = 45;
                p.Lean = 2;
            });
            return Build(rest,
                St(0.04f, back, Ease.Out, Beat.Swing, least: 4),
                St(0.035f, through, Ease.In, Beat.Swing, chain: true, least: 4),
                St(0.03f, past, Ease.Even, Beat.Strike, least: 3),
                St(0.08f, With(past, (ref Pose p) => p.Lean = 1.5f)),
                St(0.1f, Upright(rest, past), Ease.Even, least: 4),
                St(0.12f, rest, Ease.Out, least: 4));
        }

        // Hammer (1 s): hauled up over the shoulder and smashed down, swung round from far back, and a leap high
        // with it overhead to land with the whole body behind it. Held in the floor a long moment, then hauled out.
        static Clip Smash(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, -2.5f, 16);
                p.Yaw = 0;
                p.Pitch = 150;
                p.Both = 1;
                p.Drop = -1;
                p.Lean = -3;
                p.FootL = V(-3, 2, 0);
            });
            var struck = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4.5f, 7, 5.5f);
                p.Yaw = 0;
                p.Pitch = -35;
                p.Both = 1;
                p.Drop = 3.5f;
                p.Lean = 4;
                p.FootL = V(-4, 4.5f, 0);
                p.FootR = V(4, -3, 0);
            });
            return Build(rest,
                St(0.07f, raised, Ease.Out, Beat.Swing, least: 5),
                St(0.04f, With(struck, (ref Pose p) => { p.HandR = V(4.5f, 5, 13); p.Pitch = 55; p.Drop = 0; p.Lean = 1; }), Ease.In, Beat.Swing, chain: true, least: 3),
                St(0.035f, struck, Ease.Even, Beat.Strike, least: 3),
                St(0.25f, With(struck, (ref Pose p) => { p.Pitch = -30; p.Lean = 3.5f; p.Drop = 3; }), least: 3),
                St(0.3f, rest, Ease.Both, least: 8));
        }

        static Clip Swat(Pose rest)
        {
            var back = With(rest, (ref Pose p) =>
            {
                p.HandR = V(6, -3.5f, 9);
                p.Yaw = -170;
                p.Pitch = 10;
                p.Both = 1;
                p.Twist = -55;
                p.Drop = 1.5f;
                p.Lean = -1;
            });
            var through = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, 7, 8.5f);
                p.Yaw = -10;
                p.Pitch = 0;
                p.Both = 1;
                p.Twist = 10;
                p.Lean = 2.5f;
                p.Drop = 1.5f;
                p.FootR = V(3.5f, 3, 0);
                p.FootL = V(-3.5f, -2, 0);
            });
            var past = With(through, (ref Pose p) =>
            {
                p.HandR = V(-3, 5, 8);
                p.Yaw = 90;
                p.Twist = 50;
                p.Lean = 3;
            });
            return Build(rest,
                St(0.07f, back, Ease.Out, Beat.Swing, least: 5),
                St(0.04f, through, Ease.In, Beat.Swing, chain: true, least: 4),
                St(0.03f, past, Ease.Even, Beat.Strike, least: 3),
                St(0.22f, With(past, (ref Pose p) => { p.Lean = 2.5f; p.Drop = 2; })),
                St(0.18f, Upright(rest, past), Ease.Both, least: 5),
                St(0.15f, rest, Ease.Out, least: 4));
        }

        static Clip LeapSmash(Pose rest)
        {
            var raised = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4, -2.5f, 16);
                p.Yaw = 0;
                p.Pitch = 150;
                p.Both = 1;
                p.Drop = 2.5f;
                p.Lean = -3;
            });
            var air = With(raised, (ref Pose p) =>
            {
                p.Hop = 6;
                p.Drop = -1;
                p.Pitch = 140;
                p.Lean = -1;
                p.FootR = V(3, -2, 2);
                p.FootL = V(-3, 2, 1.5f);
            });
            var struck = With(rest, (ref Pose p) =>
            {
                p.HandR = V(4.5f, 7, 5);
                p.Yaw = 0;
                p.Pitch = -38;
                p.Both = 1;
                p.Drop = 4;
                p.Lean = 4;
                p.FootL = V(-4.5f, 4.5f, 0);
                p.FootR = V(4.5f, -3, 0);
            });
            return Build(rest,
                St(0.05f, raised, Ease.Out, Beat.Swing, least: 4),
                St(0.06f, air, Ease.Out, Beat.Swing, chain: true, least: 4),
                St(0.04f, With(struck, (ref Pose p) => { p.HandR = V(4.5f, 5, 13); p.Pitch = 55; p.Drop = 0; p.Hop = 3; p.Lean = 1; }), Ease.In, Beat.Swing, least: 4),
                St(0.035f, struck, Ease.Even, Beat.Strike, least: 3),
                St(0.3f, With(struck, (ref Pose p) => { p.Pitch = -32; p.Lean = 3.5f; p.Drop = 3.5f; }), least: 3),
                St(0.3f, rest, Ease.Both, least: 8));
        }

        // Crossbow (1.1 s): brought up to the shoulder in both hands and loosed (it kicks), then tipped down, the
        // string hauled back to the latch and a bolt set before it comes back up. The second shot from a lower
        // stance, the third knelt, braced and kicking hardest.
        static Clip Loose(Pose rest, int step)
        {
            float drop = step == 0 ? 0 : step == 1 ? 1 : 2.5f;
            var aimed = With(rest, (ref Pose p) =>
            {
                p.HandL = V(-1, 6.5f, 10.5f - drop * 0.6f);
                p.HandR = V(1.5f, 2, 10.5f - drop * 0.6f);
                p.Yaw = 0;
                p.Pitch = 0;
                p.Twist = -20;
                p.Arrow = 1;
                p.Drop = drop;
                p.Lean = 0.5f;
                p.FootL = V(-3, step == 2 ? 4 : 2.5f, 0);
                p.FootR = V(3, step == 2 ? -3.5f : -1.5f, 0);
            });
            float kick = step == 2 ? 22 : 12;
            var loosed = With(aimed, (ref Pose p) =>
            {
                p.HandL = V(-1, 5, 11.5f - drop * 0.6f);
                p.HandR = V(1.5f, 0.5f, 11 - drop * 0.6f);
                p.Pitch = kick;
                p.Lean = step == 2 ? -2.5f : -1.5f;
                p.Arrow = 0;
            });
            var lowered = With(rest, (ref Pose p) =>
            {
                p.HandL = V(-2, 5, 6.5f);
                p.HandR = V(1, 2.5f, 8);
                p.Yaw = 0;
                p.Pitch = -55;
                p.Twist = -10;
                p.Lean = 1.5f;
                p.Drop = 1;
            });
            var hauled = With(lowered, (ref Pose p) =>
            {
                p.HandR = V(2, -1, 10);
                p.Lean = 0.5f;
                p.Twist = -20;
            });
            var set = With(hauled, (ref Pose p) =>
            {
                p.HandR = V(0.5f, 3.5f, 8);
                p.Arrow = 1;
            });
            return Build(rest,
                St(0.03f, aimed, Ease.Out, Beat.Swing, least: 3),
                St(0.03f, With(aimed, (ref Pose p) => p.Lean = 0), chain: true),
                St(0.025f, loosed, Ease.Even, Beat.Strike, least: 2),
                St(step == 2 ? 0.16f : 0.12f, With(loosed, (ref Pose p) => { p.Pitch = kick * 0.5f; p.Lean *= 0.5f; }), Ease.Out, least: 3),
                St(0.13f, lowered, Ease.Both, least: 4),
                St(0.2f, hauled, Ease.Both, least: 4),
                St(0.12f, set, Ease.Both, least: 3),
                St(0.15f, With(rest, (ref Pose p) => p.Arrow = 0.6f), Ease.Both, least: 4));
        }

        // Boomerang (0.83 s): thrown overarm, sidearm and, the finisher, out of a whole turn; the hand stays open and
        // empty while it flies and catches it as it comes back.
        static Clip Throw(Pose rest, int step)
        {
            var cocked = With(rest, (ref Pose p) =>
            {
                p.HandR = step == 1 ? V(6, -2.5f, 6) : V(5, -4, 11.5f);
                p.HandL = V(-4, 4, 8);
                p.Yaw = step == 1 ? -120 : -160;
                p.Pitch = step == 1 ? -5 : 25;
                p.Twist = -40;
                p.Lean = -1;
                p.FootL = V(-3, 2, 0);
                if (step == 2) { p.Pivot = 0; p.Drop = 1.5f; }
            });
            var turned = With(cocked, (ref Pose p) =>
            {
                p.Pivot = 300;
                p.Hop = 1;
                p.HandR = V(7, -1, 9);
                p.Yaw = -120;
                p.Pitch = 5;
            });
            var thrown = With(rest, (ref Pose p) =>
            {
                p.Pivot = step == 2 ? 360 : 0;
                p.HandR = step == 1 ? V(3, 8.5f, 7) : V(4, 8.5f, 10);
                p.HandL = V(-5, -1, 7);
                p.Yaw = 0;
                p.Pitch = step == 1 ? 0 : 10;
                p.Twist = 20;
                p.Lean = 2.5f;
                p.Empty = 1;
                p.FootL = V(-3, 4, 0);
                p.FootR = V(3, -2, 0);
            });
            var open = With(rest, (ref Pose p) =>
            {
                p.Pivot = step == 2 ? 360 : 0;
                p.HandR = V(4, 5, 9.5f);
                p.HandL = V(-5, 0.5f, 6.5f);
                p.Yaw = 0;
                p.Pitch = 30;
                p.Empty = 1;
                p.Lean = 0.5f;
            });
            var caught = With(open, (ref Pose p) =>
            {
                p.HandR = V(4.5f, 3.5f, 9);
                p.Empty = 0;
                p.Lean = -0.5f;
            });
            var stages = new List<Stage> { St(0.04f, cocked, Ease.Out, Beat.Swing, least: 3) };
            if (step == 2) stages.Add(St(0.05f, turned, Ease.Even, Beat.Swing, chain: true, least: 5));
            stages.Add(St(0.025f, thrown, Ease.In, Beat.Strike, chain: step != 2, least: 3));
            stages.Add(St(0.12f, open, Ease.Out, least: 3));
            stages.Add(St(step == 2 ? 0.34f : 0.4f, With(open, (ref Pose p) => p.HandR.U += 0.5f)));
            stages.Add(St(0.06f, caught, Ease.In, least: 2));
            stages.Add(St(0.1f, With(rest, (ref Pose p) => p.Pivot = step == 2 ? 360 : 0), Ease.Out, least: 3));
            return Build(rest, stages.ToArray());
        }

        // Wand (0.29 s): a flick of the wrist from the right, one from the left, and a little twirl overhead before
        // the snap; the stone lights at each.
        static Clip Flick(Pose rest, int step)
        {
            var cocked = With(rest, (ref Pose p) =>
            {
                p.HandR = step == 1 ? V(1, 3, 11) : V(5, 2, 11);
                p.Yaw = step == 1 ? 40 : -20;
                p.Pitch = step == 1 ? 50 : 70;
                p.Glow = 1;
                p.Twist = step == 1 ? 15 : -10;
            });
            var twirl = With(cocked, (ref Pose p) =>
            {
                p.HandR = V(4, 1, 13);
                p.Yaw = -100;
                p.Pitch = 35;
                p.Hop = 1;
            });
            var snapped = With(rest, (ref Pose p) =>
            {
                p.HandR = V(5, 7, 10);
                p.Yaw = step == 1 ? -10 : 0;
                p.Pitch = 5;
                p.Glow = 1;
                p.Lean = 1;
                p.Twist = step == 1 ? -10 : 10;
                p.FootL = V(-3, step == 2 ? 3 : 1.5f, 0);
            });
            var stages = new List<Stage> { St(0.02f, cocked, Ease.Out, Beat.Swing, least: 2) };
            if (step == 2) stages.Add(St(0.05f, twirl, Ease.Even, Beat.Swing, least: 3));
            stages.Add(St(0.02f, snapped, Ease.In, Beat.Strike, chain: true, least: 2));
            stages.Add(St(0.04f, With(snapped, (ref Pose p) => { p.Glow = 0.3f; p.Pitch = 10; })));
            stages.Add(St(0.12f, rest, Ease.Both, least: 4));
            return Build(rest, stages.ToArray());
        }

        // Orb (0.74 s): drawn in to the chest in both hands as it lights and pushed out at the target with a step,
        // again from the other foot, and the finisher lifted high overhead and driven down and out.
        static Clip Push(Pose rest, int step)
        {
            bool high = step == 2, other = step == 1;
            var gathered = With(rest, (ref Pose p) =>
            {
                p.HandR = high ? V(2, 0, 15) : V(3.5f, 0, 9);
                p.HandL = high ? V(-2, 0, 15) : V(-2, 1, 9.5f);
                p.Yaw = 0;
                p.Pitch = high ? 90 : 40;
                p.Glow = 1;
                p.Lean = -1.5f;
                p.Drop = high ? -1 : 1;
                p.Hop = high ? 1 : 0;
                p.Twist = other ? 20 : -20;
            });
            var pushed = With(rest, (ref Pose p) =>
            {
                p.HandR = V(3, 8, high ? 8 : 9.5f);
                p.HandL = V(-1, 7.5f, high ? 8 : 9.5f);
                p.Yaw = 0;
                p.Pitch = high ? -5 : 10;
                p.Glow = 1;
                p.Lean = high ? 3.5f : 3;
                p.Drop = high ? 2 : 1;
                p.Twist = other ? -15 : 15;
                p.FootL = V(-3, other ? -2 : 4, 0);
                p.FootR = V(3, other ? 4 : -2, 0);
            });
            return Build(rest,
                St(high ? 0.05f : 0.035f, gathered, Ease.Out, Beat.Swing, least: 4),
                St(0.03f, pushed, Ease.In, Beat.Strike, chain: true, least: 3),
                St(0.12f, With(pushed, (ref Pose p) => { p.Glow = 0.4f; p.Lean -= 0.5f; })),
                St(0.25f, rest, Ease.Both, least: 7));
        }
    }
}
