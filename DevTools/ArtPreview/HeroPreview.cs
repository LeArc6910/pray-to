using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // The hero's motions laid out to be looked at and checked (2026-11-19, HeroArt): for each weapon a moving preview
    // of its attacks in all five drawn views (anim/hero_<weapon>.gif), every frame in strips over a ghost of the frame
    // before (hero_<weapon>_frames_<motion>.png) and a report of how far the hands and the weapon's end move from
    // frame to frame (hero_<weapon>_continuity.txt, MotionCheck); the motions every weapon shares the same way
    // (hero_common); and stills: the eight ways with each weapon (hero_turn.png), the hero beside the people and
    // monsters of the world for size (hero_size.png).
    static class HeroPreview
    {
        static readonly Rgba Tunic = Rgba.Hex("#3F7FD8");
        static readonly BossView[] Views = { BossView.Front, BossView.FrontSide, BossView.Side, BossView.BackSide, BossView.Back };

        static Look LookOf(BossView view) => view switch
        {
            BossView.Front => Look.Down,
            BossView.FrontSide => Look.DownSide,
            BossView.Side => Look.Side,
            BossView.BackSide => Look.UpSide,
            _ => Look.Up,
        };

        // How far a tracked point may move in an ordinary frame (pixels); a swing may move 3.3 times that.
        const float Point = HeroArt.StepPixels;

        static MotionCheck.Step Frame(HeroMove move, int frame, HeroGear gear, string label = null, float seconds = 0)
        {
            return new MotionCheck.Step
            {
                Label = label ?? $"{move}{frame:00}",
                Seconds = seconds > 0 ? seconds : HeroArt.FrameSeconds(move, gear.Style, frame),
                Kind = label != null ? MotionCheck.Kind.Normal : HeroArt.BeatOf(move, gear.Style, frame) switch
                {
                    HeroArt.Beat.Swing => MotionCheck.Kind.Swing,
                    HeroArt.Beat.Strike => MotionCheck.Kind.Strike,
                    _ => MotionCheck.Kind.Normal,
                },
                Draw = view => HeroArt.Draw(LookOf(view), move, frame, gear, Tunic),
                Measure = view =>
                {
                    var j = HeroArt.JointsOf(LookOf(view), move, frame, gear);
                    return new MotionCheck.Measure { Points = new[] { (j.RightX, j.RightY), (j.LeftX, j.LeftY), (j.TipX, j.TipY) }, Top = j.Top, Bottom = j.Bottom };
                },
            };
        }

        static IEnumerable<MotionCheck.Step> Rest(HeroGear gear, float seconds = 0.3f) => new[] { Frame(HeroMove.Stand, 0, gear, "rest", seconds) };

        static IEnumerable<MotionCheck.Step> All(HeroMove move, HeroGear gear, int from = 0) =>
            Enumerable.Range(from, HeroArt.FramesOf(move, gear.Style) - from).Select(f => Frame(move, f, gear));

        static IEnumerable<MotionCheck.Step> Once(HeroMove move, HeroGear gear) => Rest(gear).Concat(All(move, gear)).Concat(Rest(gear));

        public static int Write(string folder, PixelImage floor, MonsterData monsters = null, NpcData npcs = null)
        {
            Directory.CreateDirectory(folder);
            int jumps = 0;
            var weapons = new[] { HeroWeapon.Sword, HeroWeapon.Bow, HeroWeapon.Staff, HeroWeapon.Pick, HeroWeapon.None };
            // The four of old and bare hands, then the kinds with motions of their own (HeroStyle, Docs/Weapons.md 묶음 B).
            var gears = weapons.Select(HeroGear.Plain)
                .Concat(new[] { "dagger", "greatsword", "spear", "hammer", "crossbow", "throwing", "wand", "orb" }.Select(HeroGear.PlainKind)).ToList();
            foreach (var gear in gears)
            {
                var weapon = gear.Style;
                string name = "hero_" + (weapon == HeroStyle.None ? "fists" : weapon.ToString().ToLowerInvariant());
                var attacks = new[] { HeroMove.Attack1, HeroMove.Attack2, HeroMove.Attack3 };
                // The three steps each from rest, then the three straight on one another as a held attack plays them
                // (each next strike starting past its own lead-in, WorldView).
                var rows = attacks.Select(a => Once(a, gear).ToList()).ToList();
                var chain = Rest(gear).Concat(All(HeroMove.Attack1, gear));
                foreach (var next in new[] { HeroMove.Attack2, HeroMove.Attack3 })
                {
                    int from = ChainFrame(next, weapon);
                    // The strike before is cut short where the next begins: the frame it had reached is not checked
                    // against the new one's first (a cut), as the game jumps there too.
                    var steps = All(next, gear, from).ToList();
                    steps[0].Kind = MotionCheck.Kind.Cut;
                    chain = chain.Take(chain.Count() - RecoveryFrames(next == HeroMove.Attack2 ? HeroMove.Attack1 : HeroMove.Attack2, weapon)).Concat(steps);
                }
                rows.Add(chain.Concat(Rest(gear)).ToList());
                // The dodge roll, which looks different by what is held (HeroMove.Roll).
                rows.Add(Once(HeroMove.Roll, gear).ToList());
                jumps += MotionCheck.Write(folder, name, new[] { "attack1", "attack2", "attack3", "chain", "roll" }, rows.ToArray(), floor, HeroArt.Size, HeroArt.Size, Point, Views, 3);
            }

            // What every weapon does the same way, with a sword in hand and with nothing.
            foreach (var weapon in new[] { HeroWeapon.Sword, HeroWeapon.None })
            {
                var gear = HeroGear.Plain(weapon);
                var common = new List<List<MotionCheck.Step>>
                {
                    All(HeroMove.Walk, gear).Concat(All(HeroMove.Walk, gear)).ToList(),
                    All(HeroMove.Stand, gear).ToList(),
                    Once(HeroMove.Cast, gear).ToList(),
                    Once(HeroMove.Thrust, gear).ToList(),
                    Once(HeroMove.Spread, gear).ToList(),
                    Once(HeroMove.Hurt, gear).ToList(),
                };
                var names = new List<string> { "walk", "stand", "cast", "thrust", "spread", "hurt" };
                if (weapon == HeroWeapon.None)
                {
                    common.Add(Once(HeroMove.Drink, gear).ToList());
                    common.Add(Rest(gear).Concat(All(HeroMove.Fall, gear)).Concat(new[] { Frame(HeroMove.Fall, HeroArt.FramesOf(HeroMove.Fall, weapon) - 1, gear, "down", 0.5f) })
                        .Concat(All(HeroMove.Rise, gear)).Concat(Rest(gear)).ToList());
                    names.Add("drink");
                    names.Add("fall");
                }
                jumps += MotionCheck.Write(folder, weapon == HeroWeapon.None ? "hero_common" : "hero_common_sword", names.ToArray(), common.ToArray(), floor, HeroArt.Size, HeroArt.Size, Point, Views, 3);
            }

            // How many frames each motion has, how long it takes and where its blow lands (hero_frames.txt).
            var table = new System.Text.StringBuilder();
            foreach (var weapon in gears.Select(g => g.Style))
            {
                table.AppendLine($"## {weapon}");
                foreach (HeroMove move in Enum.GetValues(typeof(HeroMove)))
                {
                    int count = HeroArt.FramesOf(move, weapon), strike = HeroArt.StrikeFrame(move, weapon);
                    float until = 0;
                    for (int f = 0; f < Math.Max(0, strike); f++) until += HeroArt.FrameSeconds(move, weapon, f);
                    table.AppendLine($"{move,-8} {count,3} frames {HeroArt.SecondsOf(move, weapon):0.00} s" + (strike >= 0 ? $", blow at frame {strike} ({until:0.000} s), chain from {HeroArt.ChainSeconds(move, weapon):0.000} s" : ""));
                }
            }
            File.WriteAllText(Path.Combine(folder, "hero_frames.txt"), table.ToString());

            Turn(Path.Combine(folder, "hero_turn.png"), floor);
            if (monsters != null && npcs != null) Sizes(Path.Combine(folder, "hero_size.png"), floor, monsters, npcs);
            return jumps;
        }

        static int ChainFrame(HeroMove move, HeroStyle weapon)
        {
            float at = HeroArt.ChainSeconds(move, weapon);
            return HeroArt.FrameAt(move, weapon, at + 1e-4f);
        }

        // The frames at the end of a strike in which it goes back to rest (after the hold), which a strike that follows
        // straight on cuts off.
        static int RecoveryFrames(HeroMove move, HeroStyle weapon)
        {
            int frames = HeroArt.FramesOf(move, weapon), strike = HeroArt.StrikeFrame(move, weapon);
            return Math.Max(0, frames - strike - 3);
        }

        // The eight ways faced (columns: down, down-right, right, up-right, up, up-left, left, down-left) with each
        // weapon (rows), standing; then the three players' tunics.
        static void Turn(string path, PixelImage floor)
        {
            const int scale = 4, cell = 40;
            var looks = new (Look Look, bool Flip)[]
            {
                (Look.Down, false), (Look.DownSide, false), (Look.Side, false), (Look.UpSide, false), (Look.Up, false), (Look.UpSide, true), (Look.Side, true), (Look.DownSide, true),
            };
            var weapons = new[] { HeroWeapon.None, HeroWeapon.Sword, HeroWeapon.Bow, HeroWeapon.Staff, HeroWeapon.Pick };
            var tunics = new[] { "#3F7FD8", "#4DB06A", "#E08A3C", "#C05AC8" };
            var sheet = new PixelImage(looks.Length * cell, (weapons.Length + 1) * cell);
            for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++)
                    sheet[x, y] = floor[x % floor.Width, y % floor.Height];
            for (int row = 0; row <= weapons.Length; row++)
            {
                for (int column = 0; column < looks.Length; column++)
                {
                    var gear = row < weapons.Length ? HeroGear.Plain(weapons[row]) : HeroGear.Plain(HeroWeapon.Sword);
                    var tunic = Rgba.Hex(row < weapons.Length ? tunics[0] : tunics[column % tunics.Length]);
                    var image = HeroArt.Draw(looks[column].Look, HeroMove.Stand, 0, gear, tunic);
                    if (looks[column].Flip) image = image.FlippedX();
                    Over(sheet, image, column * cell + cell / 2 - HeroArt.FeetX, row * cell + cell - 6 - HeroArt.FeetY);
                }
            }
            Png.Save(Scaled(sheet, scale), path);
        }

        // The hero among the others of the world: villagers and story people, monsters, on tiles of 16.
        static void Sizes(string path, PixelImage floor, MonsterData monsters, NpcData npcs)
        {
            const int scale = 4;
            var others = new List<PixelImage>();
            foreach (var npc in npcs.Npcs.Take(4))
            {
                var person = PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), 0);
                if (person != null) others.Add(person);
            }
            foreach (string look in PeopleArt.Looks.Take(3)) others.Add(PeopleArt.Look(look, Rgba.Hex("#B04848"), 0));
            foreach (var monster in monsters.Monsters.Take(6)) others.Add(CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), 0));
            int width = 16 * (4 + others.Count * 2), height = 16 * 4;
            var sheet = new PixelImage(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    sheet[x, y] = floor[x % floor.Width, y % floor.Height];
            int ground = height - 12;
            Over(sheet, HeroArt.Draw(Look.Down, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Sword), Tunic), 16 - HeroArt.FeetX, ground - HeroArt.FeetY);
            Over(sheet, HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(HeroWeapon.Bow), Rgba.Hex("#4DB06A")), 44 - HeroArt.FeetX, ground - HeroArt.FeetY);
            int at = 64;
            foreach (var other in others)
            {
                Over(sheet, other, at + 16 - other.Width / 2, ground - other.Height);
                at += 32;
            }
            Png.Save(Scaled(sheet, scale), path);
        }

        static void Over(PixelImage sheet, PixelImage image, int left, int top)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y) && sheet.Inside(left + x, top + y)) sheet[left + x, top + y] = image[x, y];
        }

        static PixelImage Scaled(PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                    result[x, y] = image[x / scale, y / scale];
            return result;
        }
    }
}
