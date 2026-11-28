using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // --only weaponarts (Docs/Weapons.md 묶음 C): each weapon kind's own art as the game draws it, its effect programs
    // (SkillFx.Arts.cs) laid out with the shipped numbers beside the hero holding that kind and a row of slimes, at four
    // moments (out/weapon_arts.png). The arrows and bolts of a bow's, a wand's art are projectiles, drawn as shots are.
    static class WeaponArtsPreview
    {
        const int T = TileArt.Size, W = 13 * T, H = 80, Label = 9, Gap = 2, Scale = 2, HeroX = 40, HeroY = 58;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F"), Tag = Rgba.Hex("#E8E0C8");

        // One moment: the program at its phase, how far along, the effect's origin and the hero's place, from where
        // the hero stands, in pixels (x right, y up).
        readonly struct Moment
        {
            public Moment(ArtPhase phase, float t, float x = 0, float y = 0, float hero = 0)
            {
                Phase = phase;
                Time = t;
                X = x;
                Y = y;
                Hero = hero;
            }

            public ArtPhase Phase { get; }
            public float Time { get; }
            public float X { get; }
            public float Y { get; }
            public float Hero { get; }
        }

        public static void Write(string output, LootData loot)
        {
            Directory.CreateDirectory(output);
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0));
            var slime = CreatureArt.Monster("slime", Rgba.Hex("#6FCF5A"), 0);
            int rowH = H + Label + Gap;
            var kinds = loot.WeaponKinds.Where(k => k.Art != null).ToList();
            var sheet = new PixelImage(4 * (W + Gap), kinds.Count * rowH);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Backdrop);
            for (int r = 0; r < kinds.Count; r++)
            {
                var kind = kinds[r];
                var art = kind.Art;
                sheet.Small(2, r * rowH + 1, $"{kind.Id.ToUpperInvariant()}  {art.Kind.ToString().ToUpperInvariant()}  {art.Power:0}%  {art.Cooldown:0.#}S", Tag);
                var moments = MomentsOf(art);
                for (int i = 0; i < moments.Count; i++)
                    sheet.Draw(Render(art, kind, moments[i], floor, slime), i * (W + Gap), r * rowH + Label);
            }
            string path = Path.Combine(output, "weapon_arts.png");
            Png.Save(SkillFxPreview.Scaled(sheet, Scale), path);
            Console.WriteLine($"Wrote {path}");
        }

        static List<Moment> MomentsOf(WeaponArt art)
        {
            float reach = art.Reach * T, well = art.Reach * T;
            return art.Kind switch
            {
                WeaponArtKind.Parry => new List<Moment> { new(ArtPhase.Start, 0.2f), new(ArtPhase.Blocked, 0.05f), new(ArtPhase.Strike, 0.08f), new(ArtPhase.Strike, 0.25f) },
                WeaponArtKind.Vital => new List<Moment> { new(ArtPhase.Strike, 0.03f, 20), new(ArtPhase.Strike, 0.1f, 20), new(ArtPhase.Strike, 0.2f, 20), new(ArtPhase.Strike, 0.32f, 20) },
                WeaponArtKind.Charge => new List<Moment> { new(ArtPhase.Start, 0.4f), new(ArtPhase.Start, 1.1f), new(ArtPhase.Strike, 0.08f), new(ArtPhase.Strike, 0.28f) },
                WeaponArtKind.Dash => new List<Moment> { new(ArtPhase.Strike, 0.02f, reach, 0, reach), new(ArtPhase.Strike, 0.1f, reach, 0, reach), new(ArtPhase.Strike, 0.2f, reach, 0, reach), new(ArtPhase.Strike, 0.35f, reach, 0, reach) },
                WeaponArtKind.Slam => new List<Moment> { new(ArtPhase.Start, 0.2f), new(ArtPhase.Strike, 0.05f), new(ArtPhase.Strike, 0.25f), new(ArtPhase.Strike, 0.6f) },
                WeaponArtKind.Backstep => new List<Moment> { new(ArtPhase.Strike, 0.03f, -reach, 0, -reach), new(ArtPhase.Strike, 0.1f, -reach, 0, -reach), new(ArtPhase.Strike, 0.18f, -reach, 0, -reach), new(ArtPhase.Strike, 0.32f, -reach, 0, -reach) },
                WeaponArtKind.Pierce => new List<Moment> { new(ArtPhase.Start, 0.12f), new(ArtPhase.Start, 0.44f), new(ArtPhase.Strike, 0.04f), new(ArtPhase.Strike, 0.2f) },
                WeaponArtKind.Whirl => new List<Moment> { new(ArtPhase.Start, 0.12f), new(ArtPhase.Start, 0.31f), new(ArtPhase.Start, 0.81f), new(ArtPhase.Start, 1.02f) },
                WeaponArtKind.Wave => new List<Moment> { new(ArtPhase.Strike, 0.05f), new(ArtPhase.Strike, 0.14f), new(ArtPhase.Strike, 0.24f), new(ArtPhase.Strike, 0.36f) },
                WeaponArtKind.Barrage => new List<Moment> { new(ArtPhase.Start, 0.04f), new(ArtPhase.Start, 0.33f), new(ArtPhase.Start, 0.56f), new(ArtPhase.Start, 0.78f) },
                WeaponArtKind.Gravity => new List<Moment> { new(ArtPhase.Start, 0.3f, well), new(ArtPhase.Start, 1.2f, well), new(ArtPhase.Strike, 0.08f, well), new(ArtPhase.Strike, 0.3f, well) },
                _ => new List<Moment>(),
            };
        }

        // The context the game gives the art at that phase (WorldView.ShowArt).
        static FxContext ContextOf(WeaponArt art, Moment moment)
        {
            var c = new FxContext { Dir = 0, Seed = 7, Main = FxRamp.Steel };
            switch (art.Kind)
            {
                case WeaponArtKind.Parry:
                case WeaponArtKind.Charge:
                case WeaponArtKind.Wave:
                    c.Radius = (art.Reach + World.PlayerRadius) * T;
                    break;
                case WeaponArtKind.Slam:
                case WeaponArtKind.Whirl:
                case WeaponArtKind.Gravity:
                    c.Radius = art.Radius * T;
                    c.Length = art.Time;
                    break;
                case WeaponArtKind.Barrage:
                    c.Length = art.Time;
                    break;
                case WeaponArtKind.Dash:
                    c.Length = art.Reach * T;
                    c.Radius = art.Radius * T;
                    break;
                case WeaponArtKind.Backstep:
                    c.Length = art.Reach * T;
                    break;
                case WeaponArtKind.Pierce:
                    c.Length = art.Reach * T;
                    break;
            }
            return c;
        }

        // The art's shots as the game draws them (its projectiles: a bow's arrows, a wand's sparks), where they are at
        // that moment, flying as the host lets them fly; a spark that has reached the nearest slime ends in its star.
        static void Shots(PixelImage image, SkillFxPreview.Scene scene, WeaponArt art, WeaponKind kind, Moment moment)
        {
            if (art.Kind != WeaponArtKind.Backstep && art.Kind != WeaponArtKind.Barrage) return;
            bool sparks = art.Kind == WeaponArtKind.Barrage;
            var shot = sparks ? ItemArt.Spark : ItemArt.Arrow;
            float speed = kind.Attack.Speed * T, range = kind.Attack.Range * T, near = scene.Slimes.Min(s => s.X) - scene.HeroX - 6;
            for (int i = 0; i < art.Count; i++)
            {
                float from = sparks ? i * art.Time / art.Count : 0;
                float side = sparks ? (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) / (float)((art.Count + 1) / 2) * art.Arc / 2 : (i - (art.Count - 1) / 2f) * art.Arc;
                float age = moment.Time - from, angle = side * (float)Math.PI / 180;
                if (age < 0) continue;
                float gone = World.PlayerRadius * T + speed * age, most = Math.Min(range, near / (float)Math.Cos(angle));
                if (gone < most)
                {
                    int x = scene.HeroX + (int)Math.Round(Math.Cos(angle) * gone), y = scene.HeroY - 8 - (int)Math.Round(Math.Sin(angle) * gone);
                    image.Draw(shot, x - shot.Width / 2, y - shot.Height / 2);
                    continue;
                }
                if (!sparks) continue;
                var stamps = new List<FxStamp>();
                SkillFx.Stamps(FxProgram.Cast, SkillFx.SparkHit, new FxContext { Seed = 7 + i, Main = FxRamp.Steel }, age - (most - World.PlayerRadius * T) / speed, stamps);
                foreach (var stamp in stamps) SkillFxPreview.Blit(image, scene, (float)Math.Cos(angle) * most, (float)Math.Sin(angle) * most + 8, stamp);
            }
        }

        static PixelImage Render(WeaponArt art, WeaponKind kind, Moment moment, PixelImage floor, PixelImage slime)
        {
            int heroX = HeroX + (int)moment.Hero + (art.Kind == WeaponArtKind.Backstep ? 32 : 0);
            var scene = new SkillFxPreview.Scene { Width = W, Height = H, HeroX = heroX, HeroY = HeroY, Slime = slime };
            var move = moment.Phase == ArtPhase.Strike && moment.Time < 0.2f ? HeroMove.Attack3 : HeroMove.Stand;
            scene.Hero = HeroArt.Draw(Look.Side, move, 0, HeroGear.PlainKind(kind.Id), Rgba.Hex("#3F7FD8"));
            foreach (float x in art.Kind == WeaponArtKind.Whirl || art.Kind == WeaponArtKind.Slam ? new[] { -20f, 24f, 0f } : new[] { 22f, 50f, 84f, 120f })
                scene.Slimes.Add((HeroX + (int)x + (art.Kind == WeaponArtKind.Backstep ? 32 : 0), HeroY + (x == 0 ? -30 : 0)));
            var image = new PixelImage(W, H);
            for (int y = 0; y < H; y += floor.Height)
                for (int x = 0; x < W; x += floor.Width)
                    image.Draw(floor, x, y);
            var stamps = new List<FxStamp>();
            SkillFx.Stamps(FxProgram.Cast, SkillFx.ArtId(art.Kind, moment.Phase), ContextOf(art, moment), moment.Time, stamps);
            // Laid from the art's origin; Blit measures from the hero, so the origin is given as the hero's offset.
            float ox = moment.X - moment.Hero, oy = moment.Y;
            foreach (var stamp in stamps) if (stamp.Ground) SkillFxPreview.Blit(image, scene, ox, oy, stamp);
            foreach (var stamp in stamps) if (stamp.Shape == FxShape.Light) SkillFxPreview.Blit(image, scene, ox, oy, stamp);
            foreach (var (x, y) in scene.Slimes) image.Draw(slime, x - slime.Width / 2, y - slime.Height + 2);
            image.Draw(scene.Hero, scene.HeroX - HeroArt.FeetX, scene.HeroY - HeroArt.FeetY);
            foreach (var stamp in stamps) if (!stamp.Ground && stamp.Shape != FxShape.Light) SkillFxPreview.Blit(image, scene, ox, oy, stamp);
            Shots(image, scene, art, kind, moment);
            return image;
        }
    }
}
