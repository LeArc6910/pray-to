using System;
using System.IO;
using Game.Core;
using static Game.Client.Fx;

namespace Game.Client
{
    // --only weaponarts (Docs/Weapons.md 묶음 C 시안): each weapon kind's own art in three moments (wind-up, the blow, after),
    // drawn with FxMockKit on the floor beside the hero holding that kind (out/weapon_arts.png). Preview only: a mock-up
    // for a person to choose the look from before the game draws any of it.
    static class WeaponArtsPreview
    {
        const int T = TileArt.Size, W = 13 * T, H = 5 * T, Label = 9, Gap = 2, Scale = 2;
        const float Hx = 40, Hy = 46;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F"), Tag = Rgba.Hex("#E8E0C8"), Foe = Rgba.Hex("#C0504A");

        static readonly (string Kind, string Name, Action<FxCanvas, int> Draw)[] Arts =
        {
            ("sword", "SWORD  PARRY + COUNTER 250%", Parry),
            ("dagger", "DAGGER  VITAL STAB (CRIT) 200%", Stab),
            ("greatsword", "GREATSWORD  CHARGED CLEAVE 100-350%", Charge),
            ("spear", "SPEAR  DASH THRUST 3 TILES 180%", Dash),
            ("hammer", "HAMMER  GROUND SLAM 220% + STUN", Slam),
            ("bow", "BOW  BACKSTEP VOLLEY 3x90%", Backstep),
            ("crossbow", "CROSSBOW  PIERCING SHOT 12 TILES 400%", Pierce),
            ("throwing", "BOOMERANG  WHIRL 2.5 TILES 300%", Whirl),
            ("staff", "STAFF  WAVE PUSH 150%", Wave),
            ("wand", "WAND  BARRAGE 8x45%", Barrage),
            ("orb", "ORB  GRAVITY WELL 300%", Gravity),
        };

        public static void Write(string output)
        {
            Directory.CreateDirectory(output);
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0));
            int rowH = H + Label + Gap;
            var sheet = new PixelImage(3 * (W + Gap), Arts.Length * rowH);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Backdrop);
            for (int r = 0; r < Arts.Length; r++)
            {
                var (kind, name, draw) = Arts[r];
                sheet.Small(2, r * rowH + 1, name, Tag);
                for (int phase = 0; phase < 3; phase++)
                {
                    var cv = new FxCanvas(W, H, floor);
                    cv.Shade(0.75f);
                    float hx = Hx + (kind == "spear" && phase > 0 ? (phase == 1 ? 2 : 3) * T : 0) - (kind == "bow" && phase > 0 ? 2 * T : 0) + (kind == "bow" ? 2 * T : 0);
                    Foes(cv, kind);
                    var hero = HeroArt.Draw(Look.Side, phase == 1 ? HeroMove.Attack3 : HeroMove.Stand, 0, HeroGear.PlainKind(kind), Rgba.Hex("#3F7FD8"));
                    cv.Sprite(hero, (int)hx - HeroArt.Size / 2, (int)Hy - HeroArt.Size / 2 - 6);
                    draw(cv, phase);
                    sheet.Draw(cv.Image(), phase * (W + Gap), r * rowH + Label);
                }
            }
            string path = Path.Combine(output, "weapon_arts.png");
            Png.Save(SkillFxPreview.Scaled(sheet, Scale), path);
            Console.WriteLine($"Wrote {path}");
        }

        // Stand-ins for monsters: a dark blob with a red eye, in a row in front of the hero.
        static void Foes(FxCanvas cv, string kind)
        {
            float[] xs = kind == "crossbow" || kind == "spear" ? new[] { 4.2f, 6.2f, 8.5f } : kind == "throwing" || kind == "orb" ? new[] { 3.6f, 5.0f, 4.6f } : new[] { 4.0f, 5.4f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i] * T + 10, y = Hy + (kind == "throwing" || kind == "orb" ? (i - 1) * 14 : (i % 2) * 6 - 3);
                if (kind == "bow") x += 2 * T;
                Disc(cv, x, y + 6, 6, Smoke, 0.5f, squash: 0.4f);
                Disc(cv, x, y, 6, Rgba.Hex("#4A3A4A"));
                Disc(cv, x + 2, y - 1, 1.5f, Foe);
            }
        }

        static float Rad(float deg) => deg * (float)Math.PI / 180;

        static void Parry(FxCanvas cv, int p)
        {
            if (p == 0)
            {
                Ring(cv, Hx + 6, Hy, 13, 2, Steel[1], 0.9f, Rad(-60), Rad(120));
                Glow(cv, Hx + 10, Hy, 12, Steel[1], 0.35f);
            }
            else if (p == 1)
            {
                Glow(cv, Hx + 14, Hy - 2, 10, Rush[0], 0.8f);
                Star(cv, (int)Hx + 14, (int)Hy - 2, 5, Rush[0]);
                for (int i = 0; i < 6; i++) Spark(cv, Hx + 14, Hy - 2, Cos(i * 1.05f), Sin(i * 1.05f), 8, Rush[0], Rush[2]);
            }
            else
            {
                Crescent(cv, Hx + 4, Hy, 26, 6, Rad(40), Rad(170), Steel, 1);
                Glow(cv, Hx + 24, Hy, 14, Steel[1], 0.4f);
            }
        }

        static void Stab(FxCanvas cv, int p)
        {
            float fx = 4 * T + 10;
            if (p == 0) Line(cv, Hx + 8, Hy, fx - 4, Hy, Danger[2], 0.5f);
            else if (p == 1)
            {
                Shard(cv, fx - 4, Hy - 2, 22, 3, 0, Steel, 1, true);
                Glow(cv, fx, Hy - 2, 12, Danger[1], 0.7f);
                Star(cv, (int)fx, (int)Hy - 2, 6, Rush[0]);
            }
            else
            {
                for (int i = 0; i < 5; i++) Spark(cv, fx, Hy - 2, Cos(-0.6f + i * 0.3f), Sin(-0.6f + i * 0.3f), 10, Danger[0], Danger[3]);
                Star(cv, (int)fx - 6, (int)Hy - 16, 2, Rush[1]); Star(cv, (int)fx + 6, (int)Hy - 18, 2, Rush[1]);
            }
        }

        static void Charge(FxCanvas cv, int p)
        {
            if (p == 0)
            {
                Glow(cv, Hx, Hy - 4, 22, Rush[1], 0.55f);
                for (int i = 0; i < 8; i++) Spark(cv, Hx + Cos(i * 0.8f) * 20, Hy - 4 + Sin(i * 0.8f) * 16, -Cos(i * 0.8f), -Sin(i * 0.8f), 6, Rush[0], Rush[2]);
                Ring(cv, Hx, Hy - 4, 14, 1, Rush[0], 0.8f, 0, Tau * 0.7f);
            }
            else if (p == 1)
            {
                Crescent(cv, Hx, Hy, 30, 9, Rad(100), Rad(200), Fire, 1);
                Glow(cv, Hx + 18, Hy, 20, Rush[1], 0.5f);
            }
            else Crescent(cv, Hx, Hy, 32, 4, Rad(100), Rad(200), Fire, 0.45f);
        }

        static void Dash(FxCanvas cv, int p)
        {
            if (p == 0)
            {
                Warn(cv, BandShape(Hx + 8, Hy, 3 * T + 8, 8), 0, 0, W, H, 0.7f, 0.3f, Rush);
                return;
            }
            float hx = Hx + (p == 1 ? 2 : 3) * T;
            for (int i = 0; i < 4; i++) Line(cv, Hx - 6, Hy - 6 + i * 4, hx - 10, Hy - 6 + i * 4, Rush[1 + i % 2], 0.6f - i * 0.1f);
            if (p == 1) { Shard(cv, hx + 10, Hy - 2, 24, 4, 0, Rush, 1, true); Glow(cv, hx + 22, Hy, 10, Rush[0], 0.6f); }
            else for (int i = 0; i < 3; i++) Star(cv, (int)(new[] { 4.2f, 6.2f, 8.5f }[i] * T + 10), (int)Hy - 8, 3, Rush[0]);
        }

        static void Slam(FxCanvas cv, int p)
        {
            float cx = Hx + 20;
            if (p == 0) Warn(cv, CircleShape(cx, Hy + 4, 1.6f * T + 8), 0, 0, W, H, 0.6f, 0.2f, Danger);
            else if (p == 1)
            {
                Scorch(cv, cx, Hy + 4, 18, 3, 0.8f);
                Blast(cv, true, cx, Hy + 4, 34, 0.15f, Earth, 5, 12, 8, true);
            }
            else
            {
                Ring(cv, cx, Hy + 4, 34, 2, Earth[1], 0.5f);
                for (int i = 0; i < 2; i++) for (int s = 0; s < 3; s++) Star(cv, (int)(new[] { 4.0f, 5.4f }[i] * T + 10) - 6 + s * 6, (int)Hy - 12 + (i % 2) * 6, 1, Rush[0]);
            }
        }

        static void Backstep(FxCanvas cv, int p)
        {
            float start = Hx + 2 * T;
            if (p == 0) { Line(cv, start - 8, Hy + 8, start - 2 * T, Hy + 8, Dust, 0.7f, 2); return; }
            for (int i = 0; i < 3; i++) Disc(cv, start - 6 - i * 8, Hy + 10, 3 - i * 0.6f, Dust, 0.6f);
            float a0 = -0.18f;
            for (int i = 0; i < 3; i++)
            {
                float a = a0 + i * 0.18f, len = p == 1 ? 40 : 80;
                Spark(cv, Hx + 10 + Cos(a) * len, Hy - 2 + Sin(a) * len, -Cos(a), -Sin(a), 14, Steel[0], Steel[3]);
            }
        }

        static void Pierce(FxCanvas cv, int p)
        {
            if (p == 0)
            {
                Line(cv, Hx + 10, Hy - 2, W, Hy - 2, Danger[1], 0.6f);
                Ring(cv, Hx + 10, Hy - 2, 5, 1, Danger[1], 0.8f);
            }
            else if (p == 1)
            {
                Line(cv, Hx + 10, Hy - 2, W, Hy - 2, Ice[0], 1, 3, true);
                Line(cv, Hx + 10, Hy - 4, W, Hy - 4, Ice[2], 0.7f);
                Line(cv, Hx + 10, Hy, W, Hy, Ice[2], 0.7f);
                Glow(cv, Hx + 12, Hy - 2, 10, Ice[1], 0.8f);
                foreach (float f in new[] { 4.2f, 6.2f, 8.5f }) Star(cv, (int)(f * T + 10), (int)Hy - 2, 4, Ice[0]);
            }
            else Line(cv, Hx + 10, Hy - 2, W, Hy - 2, Ice[2], 0.35f, 2, true);
        }

        static void Whirl(FxCanvas cv, int p)
        {
            float r = 2.5f * T;
            Ring(cv, Hx, Hy, r, 1, Earth[1], 0.25f);
            for (int k = 0; k < 4; k++)
            {
                float a = p * 2.1f + k * 0.35f;
                float x = Hx + Cos(a) * r, y = Hy + Sin(a) * r * 0.8f;
                if (k == 0) Crescent(cv, x, y, 6, 3, a * 3, Rad(220), Earth, 1);
                else Disc(cv, x, y, 2.5f - k * 0.4f, Earth[1], 0.6f - k * 0.12f);
            }
        }

        static void Wave(FxCanvas cv, int p)
        {
            for (int i = 0; i <= p; i++)
            {
                float r = 14 + (p - i) * 6 + p * 12;
                Ring(cv, Hx + 8, Hy, r, 3 - i, Bolt_[1 + i], 0.9f - i * 0.25f, Rad(-35), Rad(70));
            }
            if (p == 2) foreach (float f in new[] { 4.6f, 6.0f }) Spark(cv, f * T + 10, Hy, 1, 0, 8, Bolt_[0], Bolt_[3]);
        }

        static void Barrage(FxCanvas cv, int p)
        {
            int shots = p == 0 ? 2 : p == 1 ? 5 : 8;
            for (int i = 0; i < shots; i++)
            {
                float a = (Rnd(i, 3) - 0.5f) * 0.5f, d = 14 + (shots - i) * 9;
                float x = Hx + 10 + Cos(a) * d, y = Hy - 2 + Sin(a) * d;
                Glow(cv, x, y, 5, Fire[1], 0.6f);
                Spark(cv, x, y, -Cos(a), -Sin(a), 6, Fire[0], Fire[3]);
            }
        }

        static void Gravity(FxCanvas cv, int p)
        {
            float cx = Hx + 3.5f * T + 10, cy = Hy;
            if (p < 2)
            {
                Disc(cv, cx, cy, 6 + p * 2, Fx.Void[4]);
                Ring(cv, cx, cy, 8 + p * 2, 1, Fx.Void[1], 0.9f);
                Glow(cv, cx, cy, 20, Fx.Void[2], 0.45f);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 0.63f + p, d = 30 - p * 8;
                    Spark(cv, cx + Cos(a) * d, cy + Sin(a) * d * 0.8f, -Cos(a), -Sin(a), 7, Fx.Void[0], Fx.Void[3]);
                }
            }
            else Blast(cv, false, cx, cy, 30, 0.2f, Fx.Void, 9, 14, 0, false);
        }
    }
}
