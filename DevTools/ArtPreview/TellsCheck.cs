using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Client
{
    // How plainly each boss's plain blow shows in its wind-up alone (2026-11-28, Docs/BossRedesign.md 9; the person:
    // "the silk mother's fangs come with no mark on the ground, yet the motion is big and easy to read — could the
    // others' plain blows be dodged by their wind-up too?"). For each plain blow, the rest pose against every wind-up
    // frame on the side and front views: the share of the body's pixels that came or went (the silhouette), the share
    // that changed colour (a glow, a lit fist), and the pixels that changed in all (how much of the screen moves).
    // Written to out/anim/tells.txt by --only tells.
    static class TellsCheck
    {
        sealed class Blow
        {
            public string Boss, Attack;
            public Func<BossView, PixelImage> Rest;
            public List<Func<BossView, PixelImage>> WindUp = new List<Func<BossView, PixelImage>>();
        }

        static Blow Make<TPose>(string boss, string attack, Func<BossView, TPose, int, PixelImage> draw, TPose rest, params (TPose Pose, int Frames)[] windUp)
        {
            var blow = new Blow { Boss = boss, Attack = attack, Rest = v => draw(v, rest, 0) };
            foreach (var (pose, frames) in windUp)
                for (int f = 0; f < frames; f++)
                {
                    int frame = f;
                    blow.WindUp.Add(v => draw(v, pose, frame));
                }
            return blow;
        }

        public static void Write(string folder)
        {
            var blows = new List<Blow>
            {
                Make<WardenPose>("cave_warden", "sweep", (v, p, f) => WardenArt.Draw(v, p, f, 1), WardenPose.Idle, (WardenPose.Wind, WardenArt.FramesOf(WardenPose.Wind))),
                Make<WardenPose>("cave_warden", "slam", (v, p, f) => WardenArt.Draw(v, p, f, 1), WardenPose.Idle, (WardenPose.Raise, WardenArt.FramesOf(WardenPose.Raise))),
                Make<AntlionPose>("antlion", "bite", (v, p, f) => AntlionArt.Draw(v, p, f, 1), AntlionPose.Idle, (AntlionPose.Gape, AntlionArt.FramesOf(AntlionPose.Gape))),
                Make<CrystalQueenPose>("crystal_queen", "bite", (v, p, f) => CrystalQueenArt.Draw(v, p, f, 1), CrystalQueenPose.Idle, (CrystalQueenPose.Rear, CrystalQueenArt.FramesOf(CrystalQueenPose.Rear))),
                Make<GeneralPose>("mad_general", "cleave", (v, p, f) => GeneralArt.Draw(v, p, f, 1), GeneralPose.Idle, (GeneralPose.Wind, GeneralArt.FramesOf(GeneralPose.Wind))),
                Make<KnightPose>("black_knight", "smash", (v, p, f) => KnightArt.Draw(v, p, f, 1), KnightPose.Idle, (KnightPose.Raise, KnightArt.FramesOf(KnightPose.Raise))),
                Make<KnightPose>("black_knight", "sweep", (v, p, f) => KnightArt.Draw(v, p, f, 1), KnightPose.Idle, (KnightPose.Wind, KnightArt.FramesOf(KnightPose.Wind))),
                Make<SamuraiPose>("shrine_samurai", "iai", (v, p, f) => SamuraiArt.Draw(v, p, f, 1), SamuraiPose.Idle,
                    (SamuraiPose.Stance, SamuraiArt.FramesOf(SamuraiPose.Stance)), (SamuraiPose.Pull, SamuraiArt.FramesOf(SamuraiPose.Pull))),
                Make<SerpentPose>("tide_serpent", "tail_sweep", (v, p, f) => SerpentArt.Draw(v, p, f, 1), SerpentPose.Idle, (SerpentPose.Coil, SerpentArt.FramesOf(SerpentPose.Coil))),
                Make<SpiderPose>("silk_mother", "fang", (v, p, f) => SpiderArt.Draw(v, p, f, 1), SpiderPose.Idle, (SpiderPose.Rear, SpiderArt.FramesOf(SpiderPose.Rear))),
                Make<SpiderPose>("silk_mother", "leg_stab", (v, p, f) => SpiderArt.Draw(v, p, f, 1), SpiderPose.Idle, (SpiderPose.Cock, SpiderArt.FramesOf(SpiderPose.Cock))),
                Make<LavaTitanPose>("lava_titan", "fire_wave", (v, p, f) => LavaTitanArt.Draw(v, p, f, 1), LavaTitanPose.Idle, (LavaTitanPose.Wind, LavaTitanArt.FramesOf(LavaTitanPose.Wind))),
                Make<LavaTitanPose>("lava_titan", "slam", (v, p, f) => LavaTitanArt.Draw(v, p, f, 1), LavaTitanPose.Idle, (LavaTitanPose.Heave, LavaTitanArt.FramesOf(LavaTitanPose.Heave))),
                Make<VeinPose>("vein_eater", "claw", (v, p, f) => VeinEaterArt.Draw(v, p, f, 1, false), VeinPose.Idle, (VeinPose.Rear, VeinEaterArt.FramesOf(VeinPose.Rear))),
                Make<VoidLordPose>("void_lord", "grasp", (v, p, f) => VoidLordArt.Draw(v, p, f, 1), VoidLordPose.Idle, (VoidLordPose.Reach, VoidLordArt.FramesOf(VoidLordPose.Reach))),
                Make<CrownWraithPose>("crown_wraith", "grasp", (v, p, f) => CrownWraithArt.Draw(v, p, f, 1), CrownWraithPose.Idle, (CrownWraithPose.Clutch, CrownWraithArt.FramesOf(CrownWraithPose.Clutch))),
            };

            var report = new StringBuilder();
            report.AppendLine("# How plainly each plain blow shows in its wind-up (rest against the most changed wind-up frame)");
            report.AppendLine("# boss attack view frames silhouette colour changed_px body_px");
            foreach (var blow in blows)
            {
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    var rest = blow.Rest(view);
                    int body = 0;
                    for (int y = 0; y < rest.Height; y++)
                        for (int x = 0; x < rest.Width; x++)
                            if (rest[x, y].A > 0) body++;
                    float shape = 0, colour = 0;
                    int changed = 0;
                    foreach (var draw in blow.WindUp)
                    {
                        var image = draw(view);
                        int moved = 0, lit = 0;
                        int w = Math.Max(rest.Width, image.Width), h = Math.Max(rest.Height, image.Height);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                var a = x < rest.Width && y < rest.Height ? rest[x, y] : default;
                                var b = x < image.Width && y < image.Height ? image[x, y] : default;
                                if ((a.A > 0) != (b.A > 0)) moved++;
                                else if (a.A > 0 && Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 60) lit++;
                            }
                        shape = Math.Max(shape, body > 0 ? moved / (float)body : 0);
                        colour = Math.Max(colour, body > 0 ? lit / (float)body : 0);
                        changed = Math.Max(changed, moved + lit);
                    }
                    report.AppendLine($"{blow.Boss} {blow.Attack} {view} {blow.WindUp.Count} {shape:0.00} {colour:0.00} {changed} {body}");
                }
            }
            File.WriteAllText(Path.Combine(folder, "tells.txt"), report.ToString());
            Console.Write(report.ToString());
        }
    }
}
