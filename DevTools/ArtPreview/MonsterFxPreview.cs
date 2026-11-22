using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // Ordinary monsters' warnings and effects (MonsterFx, Docs/Combat.md 몬스터의 공격 시트) as the game draws them: for
    // every monster with rows in monster_attacks.csv a sheet with a row to each attack (twelve moments from the first
    // of the warning to the last of what thins out), the same in grey (it must read without colour), and a GIF; and
    // pieces.png with the pools, the traits' one-offs and the marks of the kinds no monster has yet. Fails when an
    // effect draws more than MonsterFx.Most pieces at once, or nothing at the start of a warning or after a blow.
    static class MonsterFxPreview
    {
        const int Tile = TileArt.Size;
        const int Columns = 12;
        const int Wide = 10 * Tile, Tall = 8 * Tile;
        const float After = 0.45f;
        // A body's feet are this far below where the host has it (its middle), in pixels.
        const int Feet = 5;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F");

        public static bool Write(string folder, MonsterData monsters, PixelImage floor, PixelImage hero)
        {
            Directory.CreateDirectory(folder);
            bool fine = true;
            var report = new StringBuilder();
            report.AppendLine("monster.attack            shape      telegraph  most pieces (mark, cue, blow)");
            foreach (var monster in monsters.Monsters.Where(m => m.Attacks.Count > 0))
            {
                var rows = new List<List<PixelImage>>();
                var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
                foreach (var attack in monster.Attacks)
                {
                    var scene = new Scene(monster, attack, floor, hero, monsters);
                    float whole = scene.Whole;
                    rows.Add(Enumerable.Range(0, Columns).Select(i => scene.Render(whole * i / Columns)).ToList());
                    for (float t = 0; t < whole; t += 0.04f) frames.Add((SkillFxPreview.Flat(SkillFxPreview.Scaled(scene.Render(t), 2)), 4));
                    frames.Add((frames[frames.Count - 1].Pixels, 30));
                    report.AppendLine($"{monster.Id + "." + attack.Id,-25} {attack.Shape,-10} {attack.Telegraph,9:0.00}  {scene.MostMark,4} {scene.MostTell,4} {scene.MostLand,4}");
                    foreach (var (what, most) in new[] { ("mark", scene.MostMark), ("cue", scene.MostTell), ("blow", scene.MostLand), ("shot", scene.MostShot) })
                    {
                        if (most <= MonsterFx.Most) continue;
                        Console.Error.WriteLine($"monsterfx {monster.Id}.{attack.Id}: its {what} draws {most} pieces at once, more than {MonsterFx.Most}");
                        fine = false;
                    }
                    if (scene.Count(0.04f) == 0)
                    {
                        Console.Error.WriteLine($"monsterfx {monster.Id}.{attack.Id}: nothing is drawn at the start of its warning");
                        fine = false;
                    }
                    // (A rush's blow is the monster itself running its path.)
                    if (attack.Tell != Tell.Dash && attack.Tell != Tell.Call && scene.Count(scene.Lands + 0.05f) == 0)
                    {
                        Console.Error.WriteLine($"monsterfx {monster.Id}.{attack.Id}: nothing is drawn when it lands");
                        fine = false;
                    }
                }
                var sheet = SkillFxPreview.Sheet(rows);
                Png.Save(SkillFxPreview.Scaled(sheet, 3), Path.Combine(folder, monster.Id + ".png"));
                Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(sheet), 3), Path.Combine(folder, monster.Id + "_gray.png"));
                Gif.Save(Path.Combine(folder, monster.Id + ".gif"), Wide * 2, Tall * 2, frames);
            }
            var (pieces, piecesFine) = Pieces(floor);
            fine &= piecesFine;
            Png.Save(SkillFxPreview.Scaled(pieces, 3), Path.Combine(folder, "pieces.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(pieces), 3), Path.Combine(folder, "pieces_gray.png"));
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Console.WriteLine($"Wrote monsterfx/<monster>.png, <monster>_gray.png, <monster>.gif, pieces.png, pieces_gray.png and report.txt to {folder}");
            return fine;
        }

        // One attack played out: the monster on the left, a player to its right, the marks where the host would lay
        // them (a sweep's wedges to either side, a push's circles walking out, a trap's runes round the player), what
        // the blow leaves on the floor, and for a call the ally it mends or rouses.
        sealed class Scene
        {
            readonly MonsterDefinition _monster;
            readonly MonsterAttack _attack;
            readonly PixelImage _floor, _hero;
            readonly Rgba _color;
            readonly FxRamp _ramp;
            readonly uint _tint;
            readonly MobLook _look;
            readonly MonsterDefinition _ally;
            // In pixels, y down, on the picture.
            readonly float _mx, _my, _px, _py, _ax, _ay;
            readonly List<(float Begins, float Lands, float X, float Y, float Dir)> _marks = new List<(float, float, float, float, float)>();
            public int MostMark, MostTell, MostLand, MostShot;

            public Scene(MonsterDefinition monster, MonsterAttack attack, PixelImage floor, PixelImage hero, MonsterData monsters)
            {
                _monster = monster;
                _attack = attack;
                _floor = floor;
                _hero = hero;
                _color = Rgba.Hex(monster.Color.Length > 0 ? monster.Color : "#C8C8C8");
                _ramp = MonsterFx.RampOf(monster.Id, monster.Element);
                _tint = MonsterFx.TintOf(_color);
                _look = MonsterFx.LookOf(monster.Id);
                float gap = Math.Clamp(float.IsInfinity(attack.RangeMax) ? 3 : (attack.RangeMin + attack.RangeMax) / 2 + 0.75f, 1.2f, 5.5f);
                if (attack.Tell == Tell.Call) gap = 5;
                _mx = (5 - gap / 2) * Tile;
                _my = 4.5f * Tile;
                _px = _mx + gap * Tile;
                _py = _my;
                // Whoever a call is for: the kind it lives beside.
                _ally = monsters.Find(monster.Id == "tide_wisp" ? "lake_crab" : monster.Id == "demon_caster" ? "demon_soldier" : monster.Id);
                _ax = _mx + 2.2f * Tile;
                _ay = _my - 1.8f * Tile;

                float tele = attack.Telegraph;
                switch (attack.Shape)
                {
                    case MonsterAttackShape.Sweep:
                        for (int i = 0; i < Math.Max(1, attack.Count); i++)
                            _marks.Add((0, tele + i * World.SweepSeconds, _mx, _my, attack.Count <= 1 ? 0 : (i % 2 == 0 ? -1 : 1) * attack.Angle * World.SweepTurn * (float)Math.PI / 180));
                        break;
                    case MonsterAttackShape.Push:
                        float step = attack.Length / attack.Count;
                        for (int i = 0; i < attack.Count; i++) _marks.Add((0, tele + step * i / attack.Speed, _mx + step * (i + 1) * Tile, _my, 0));
                        break;
                    case MonsterAttackShape.Mine:
                        // Armed when the telegraph is over; here stepped on a moment later.
                        for (int i = 0; i < attack.Count; i++)
                            _marks.Add((0, tele + 0.8f, _px, _py + (attack.Count == 1 ? 0 : (i == 0 ? -1 : 1) * Math.Min(attack.Spread, 1.6f) * Tile), 0));
                        break;
                    case MonsterAttackShape.Lob:
                    case MonsterAttackShape.Delayed:
                        // The later circles a step along, as if the player had walked on.
                        for (int i = 0; i < Math.Max(1, attack.Count); i++) _marks.Add((i * World.LobSeconds, i * World.LobSeconds + tele, _px - i * 1.6f * Tile, _py + i * 1.1f * Tile, 0));
                        break;
                    case MonsterAttackShape.Blink:
                        // Away and up from the player, or (a shadow step) right behind them.
                        if (attack.Behind) _marks.Add((0, tele, Math.Min(Wide - Tile, _px + (attack.Length + 0.8f) * Tile), _py, 0));
                        else _marks.Add((0, tele, Math.Max(Tile, _mx - attack.Length * Tile * 0.6f), _my - attack.Length * Tile * 0.5f, 0));
                        break;
                    default:
                        if (attack.Tell == Tell.Barrage || attack.Tell == Tell.Call) break;
                        _marks.Add(attack.Attached ? (0, tele, _mx, _my, 0f) : (0, tele, _px, _py, 0f));
                        break;
                }
            }

            // The blow: when the telegraph is over (a lob's second circle a moment later).
            public float Lands => _marks.Count > 0 ? _marks[0].Lands : _attack.Telegraph;
            public float Whole => (_marks.Count > 0 ? _marks.Max(m => m.Lands) : _attack.Telegraph) + After + (Shoots ? 1.2f : 0)
                + (_attack.Tell == Tell.Dash && _attack.Shape != MonsterAttackShape.Blink ? 0.3f : 0) + (_attack.Pool != MonsterPool.None ? 0.5f : 0) + (_attack.Tell == Tell.Call ? 0.8f : 0);

            bool Shoots => _attack.Tell == Tell.Barrage;
            bool Under => _attack.Shape == MonsterAttackShape.Burrow || _attack.Shape == MonsterAttackShape.Ambush;
            bool Rushes => _attack.Shape == MonsterAttackShape.Charge || _attack.Shape == MonsterAttackShape.Swoop;

            (HazardShape Area, float Size, float Width) Geometry()
            {
                float r = World.PlayerRadius;
                switch (_attack.Shape)
                {
                    case MonsterAttackShape.Strike:
                    case MonsterAttackShape.Sweep:
                        return (HazardShape.Cone, (_attack.Length + r) * Tile, _attack.Angle);
                    case MonsterAttackShape.Thrust:
                    case MonsterAttackShape.Beam:
                    case MonsterAttackShape.Charge:
                    case MonsterAttackShape.Swoop:
                        return (HazardShape.Line, (_attack.Length + r) * Tile, (_attack.Width + 2 * r) * Tile);
                    case MonsterAttackShape.Nova when _attack.Length > 0:
                        return (HazardShape.Ring, (_attack.Length + r) * Tile, _attack.Radius * Tile);
                    case MonsterAttackShape.Pull:
                        return (HazardShape.Pull, (_attack.Radius + r) * Tile, 0);
                    case MonsterAttackShape.Mine:
                        return (HazardShape.Mine, (_attack.Radius + r) * Tile, 0);
                    case MonsterAttackShape.Blink:
                        return (HazardShape.Circle, (_monster.Radius + 0.25f) * Tile, 0);
                    default:
                        return (HazardShape.Circle, (_attack.Radius + r) * Tile, 0);
                }
            }

            static HazardShape PoolShape(MonsterPool pool) => pool switch
            {
                MonsterPool.Fire => HazardShape.Fire,
                MonsterPool.Ice => HazardShape.Ice,
                MonsterPool.Spore => HazardShape.Spore,
                _ => HazardShape.Sand,
            };

            List<(float X, float Y, FxStamp Stamp)> Gather(float t, bool count)
            {
                var all = new List<(float, float, FxStamp)>();
                var o = new List<FxStamp>();
                int Take(MobFx fx, MobContext c, float time, float x, float y)
                {
                    o.Clear();
                    MonsterFx.Stamps(fx, c, time, o);
                    foreach (var stamp in o) all.Add((x, y, stamp));
                    return o.Count;
                }
                var (area, size, width) = Geometry();
                float feetY = _my + Feet;
                float tele = _attack.Telegraph;
                var context = new MobContext { Shape = _attack.Shape, Area = area, Ramp = _ramp, Tint = _tint, Dir = 0, Size = size, Width = width, Life = tele, Seed = 7, Look = _look };
                var (bx, by, _) = Body(t);
                if (t < tele && !Under && !_attack.Behind)
                {
                    int tell = Take(MobFx.Tell, context, t, bx, by + Feet);
                    if (count) MostTell = Math.Max(MostTell, tell);
                }
                // One that lives under the ground comes up to throw; one that burrows or lies in wait is a mound that
                // runs to its mark; a shadow step goes to smoke where it stood.
                if (_monster.Has(MonsterTraitKind.Burrowed) && !Under && t < 0.4f) Take(MobFx.Surface, context, t, _mx, feetY);
                if (Under && t < tele * 0.7f && _marks.Count > 0)
                {
                    float u = t / (tele * 0.7f);
                    var mound = context;
                    mound.Armed = true;
                    Take(MobFx.Mound, mound, t, _mx + (_marks[0].X - _mx) * u, feetY + (_marks[0].Y - _my) * u);
                }
                if (_attack.Behind && t < 0.4f) Take(MobFx.Vanish, context, t, _mx, feetY);
                if (Shoots)
                {
                    // The shot leaves when the telegraph ends; a returning one goes out, stands and comes back; a fan's
                    // others fly to either side.
                    float flown = t - tele;
                    if (flown >= 0)
                    {
                        float reach = Math.Min(4.5f, _attack.Length > 0 ? _attack.Length : 4.5f) * Tile, pace = _attack.Speed * Tile;
                        float outFor = reach / pace;
                        bool back = _attack.Shape == MonsterAttackShape.Boomerang;
                        float x = flown < outFor ? flown * pace : back ? reach - Math.Max(0, flown - outFor - World.TurnPause) * pace : -1;
                        bool still = back && flown >= outFor && flown < outFor + World.TurnPause;
                        if (x >= 0)
                        {
                            int shots = _attack.Shape == MonsterAttackShape.Spread ? Math.Max(1, _attack.Count) : 1;
                            for (int i = 0; i < shots; i++)
                            {
                                float turn = shots == 1 ? 0 : (-_attack.Angle / 2 + _attack.Angle * i / (shots - 1)) * (float)Math.PI / 180;
                                var shot = context;
                                shot.Dir = back && flown > outFor + World.TurnPause ? (float)Math.PI : turn;
                                shot.Armed = still;
                                int pieces = Take(MobFx.Shot, shot, t, _mx + 6 + x * (float)Math.Cos(turn), _my - 6 - x * (float)Math.Sin(turn));
                                if (count) MostShot = Math.Max(MostShot, pieces);
                            }
                        }
                        if (back && flown >= outFor && flown < outFor + 0.3f) Take(MobFx.Turn, context, flown - outFor, _mx + 6 + reach, _my - 6);
                        if (_attack.Shape == MonsterAttackShape.Bounce && flown >= outFor && flown < outFor + 0.25f) Take(MobFx.Bounce, context, flown - outFor, _mx + 6 + reach, _my - 6);
                    }
                    return all;
                }
                if (_attack.Tell == Tell.Call)
                {
                    // Made when the telegraph is over: a line to the ally and plus signs over it, or the ring going out
                    // and the flames at the feet of the roused.
                    float since = t - tele;
                    if (since < 0) return all;
                    if (_attack.Shape == MonsterAttackShape.Mend)
                    {
                        float dx = _ax - _mx, dy = -(_ay - _my);
                        Take(MobFx.Tether, new MobContext { Dir = (float)Math.Atan2(dy, dx), Size = (float)Math.Sqrt(dx * dx + dy * dy) }, t, _mx, feetY);
                        Take(MobFx.Mended, new MobContext(), t, _ax, _ay + Feet);
                    }
                    else if (_attack.Shape == MonsterAttackShape.Empower)
                    {
                        Take(MobFx.Rally, new MobContext { Size = Math.Min(_attack.Radius, 2.5f) * Tile }, since, _mx, feetY);
                        Take(MobFx.Empowered, new MobContext(), t, _ax, _ay + Feet);
                    }
                    return all;
                }
                foreach (var (begins, lands, mx0, my0, dir) in _marks)
                {
                    float x = mx0, y = my0;
                    // A bomber carries its circle with it.
                    if (_attack.Shape == MonsterAttackShape.Explode) (x, y) = (bx, by);
                    var here = context;
                    here.Dir = dir;
                    if (t >= begins && t < lands)
                    {
                        here.Progress = (t - begins) / (lands - begins);
                        here.Armed = _attack.Shape == MonsterAttackShape.Mine && t >= tele;
                        int pieces = Take(MobFx.Mark, here, t, x, y);
                        if (count) MostMark = Math.Max(MostMark, pieces);
                    }
                    else if (t >= lands && _attack.Shape != MonsterAttackShape.Blink && _attack.Tell != Tell.Dash)
                    {
                        int pieces = Take(MobFx.Land, here, t - lands, x, y);
                        if (count) MostLand = Math.Max(MostLand, pieces);
                        // What it leaves on the floor (a line leaves a few along it).
                        if (_attack.Pool != MonsterPool.None)
                        {
                            float fade = Math.Min(1, (t - lands) / 0.2f);
                            if (area == HazardShape.Line)
                            {
                                int along = Math.Min(World.MaxPools, Math.Max(1, (int)Math.Round(_attack.Length / _attack.Width)));
                                for (int k = 0; k < along; k++)
                                    Take(MobFx.Pool, new MobContext { Area = PoolShape(_attack.Pool), Size = _attack.Width / 2 * Tile, Progress = fade, Seed = 3 + k }, t, x + _attack.Length * Tile * (k + 0.5f) / along, y);
                            }
                            else
                            {
                                Take(MobFx.Pool, new MobContext { Area = PoolShape(_attack.Pool), Size = (_attack.PoolRadius > 0 ? _attack.PoolRadius : _attack.Radius) * Tile, Progress = fade, Seed = 3 }, t, x, y);
                            }
                        }
                    }
                    else if (t >= lands && _attack.Shape == MonsterAttackShape.Blink && t < lands + 0.35f && !_attack.Behind)
                    {
                        Take(MobFx.BlinkOut, context, t - lands, _mx, feetY);
                    }
                }
                if (Rushes && t >= tele)
                {
                    // A swoop's afterimages while it flies, and the pools it leaves along the way it has come.
                    float ran = Math.Clamp((t - tele) * _attack.Speed, 0, _attack.Length);
                    if (_attack.Shape == MonsterAttackShape.Swoop && ran < _attack.Length) Take(MobFx.Wake, context, t, bx, by + Feet);
                    if (_attack.Pool != MonsterPool.None)
                        for (float at = _attack.Width; at <= ran; at += _attack.Width)
                            Take(MobFx.Pool, new MobContext { Area = PoolShape(_attack.Pool), Size = _attack.Width / 2 * Tile, Progress = 1, Seed = (int)(at * 10) }, t, _mx + at * Tile, _my);
                }
                return all;
            }

            public int Count(float t) => Gather(t, false).Count;

            // Where the monster's feet are at t (Lift under 0: not there to be seen): a leaper hops onto its mark in the
            // last moment, a blinker is at its mark once the telegraph is over, a rusher runs its path, a burrower is
            // under the ground until it comes up at its mark, a bomber flies at the player and is gone with its blast.
            (float X, float Y, float Lift) Body(float t)
            {
                var (lx, ly) = _marks.Count > 0 ? (_marks[0].X, _marks[0].Y) : (_mx, _my);
                float tele = _attack.Telegraph;
                switch (_attack.Shape)
                {
                    case MonsterAttackShape.Leap:
                    {
                        float start = tele - World.HopSeconds;
                        if (t < start) return (_mx, _my, 0);
                        float u = Math.Min(1, (t - start) / World.HopSeconds);
                        return (_mx + (lx - _mx) * u, _my + (ly - _my) * u, (float)Math.Sin(u * Math.PI) * 11);
                    }
                    case MonsterAttackShape.Blink:
                        return t >= tele ? (lx, ly, 0) : _attack.Behind && t > 0.12f ? (_mx, _my, -1) : (_mx, _my, 0);
                    case MonsterAttackShape.Burrow:
                    case MonsterAttackShape.Ambush:
                        return t < tele ? (_mx, _my, -1) : (lx, ly, 0);
                    case MonsterAttackShape.Explode:
                    {
                        float flown = Math.Min(Math.Min(t, tele) * _monster.Speed * Tile, Math.Max(0, _px - _mx - 0.8f * Tile));
                        return (_mx + flown, _my, t < tele ? 0 : -1);
                    }
                    case MonsterAttackShape.Charge:
                    case MonsterAttackShape.Swoop:
                    {
                        float ran = Math.Clamp((t - tele) * _attack.Speed, 0, _attack.Length) * Tile;
                        return (_mx + ran, _my, 0);
                    }
                    default:
                        return (_mx, _my, 0);
                }
            }

            public PixelImage Render(float t)
            {
                var image = new PixelImage(Wide, Tall);
                for (int y = 0; y < Tall; y += _floor.Height)
                    for (int x = 0; x < Wide; x += _floor.Width)
                        image.Draw(_floor, x, y);
                var stamps = Gather(t, true);
                foreach (var (x, y, stamp) in stamps)
                    if (stamp.Ground) Blit(image, x, y, stamp);
                image.Draw(_hero, (int)_px - HeroArt.FeetX, (int)_py + Feet - HeroArt.FeetY);
                if (_attack.Tell == Tell.Call && _ally != null)
                {
                    var ally = CreatureArt.Monster(_ally.Id, Rgba.Hex(_ally.Color.Length > 0 ? _ally.Color : "#C8C8C8"), 0, MonsterView.Side, MonsterPose.Idle);
                    image.Draw(ally, (int)Math.Round(_ax - ally.Width / 2f), (int)Math.Round(_ay + Feet - ally.Height + 2));
                }
                var (bx, by, lift) = Body(t);
                if (lift >= 0)
                {
                    bool winding = t < _attack.Telegraph;
                    var sprite = CreatureArt.Monster(_monster.Id, _color, 0, MonsterView.Side, winding ? MonsterPose.Windup : t < _attack.Telegraph + 0.15f ? MonsterPose.Strike : MonsterPose.Idle);
                    image.Draw(sprite, (int)Math.Round(bx - sprite.Width / 2f), (int)Math.Round(by + Feet - lift - sprite.Height + 2));
                }
                foreach (var (x, y, stamp) in stamps)
                    if (!stamp.Ground) Blit(image, x, y, stamp);
                return image;
            }
        }

        // The pools, the one-offs of traits and of the new engine pieces, and the marks no monster has yet: a row each.
        static (PixelImage Sheet, bool Fine) Pieces(PixelImage floor)
        {
            bool fine = true;
            const int cell = 5 * Tile;
            var rows = new List<List<PixelImage>>();
            List<PixelImage> Row(MobFx fx, MobContext c, float span, bool loop, Func<MobContext, float, MobContext> each = null)
            {
                var row = new List<PixelImage>();
                var o = new List<FxStamp>();
                for (int i = 0; i < Columns; i++)
                {
                    float t = span * i / Columns;
                    var image = new PixelImage(cell, cell);
                    for (int y = 0; y < cell; y += floor.Height)
                        for (int x = 0; x < cell; x += floor.Width)
                            image.Draw(floor, x, y);
                    o.Clear();
                    MonsterFx.Stamps(fx, each != null ? each(c, t / span) : c, t, o);
                    if (o.Count > MonsterFx.Most)
                    {
                        Console.Error.WriteLine($"monsterfx {fx} ({c.Area}, {c.Shape}): {o.Count} pieces at once, more than {MonsterFx.Most}");
                        fine = false;
                    }
                    // A pool fades in, so it is looked at in its middle.
                    if (i == (fx == MobFx.Pool ? Columns / 2 : 0) && o.Count == 0)
                    {
                        Console.Error.WriteLine($"monsterfx {fx} ({c.Area}): nothing drawn at its start");
                        fine = false;
                    }
                    foreach (var stamp in o) if (stamp.Ground) Blit(image, cell / 2f, cell / 2f + 8, stamp);
                    foreach (var stamp in o) if (!stamp.Ground) Blit(image, cell / 2f, cell / 2f + 8, stamp);
                    row.Add(image);
                }
                return row;
            }
            uint green = MonsterFx.TintOf(Rgba.Hex("#8CD94D"));
            foreach (var pool in new[] { HazardShape.Fire, HazardShape.Ice, HazardShape.Spore, HazardShape.Sand, HazardShape.Balm })
                rows.Add(Row(MobFx.Pool, new MobContext { Area = pool, Size = 1.2f * Tile, Seed = 3 }, 3, true, (c, u) => { c.Progress = Math.Min(1, Math.Min(u * 6, (1 - u) * 6)); return c; }));
            // Marks: a pull, a trap (armed halfway), a ring, a rush's path, the flash of the fallen, a wide sweep.
            MobContext Growing(MobContext c, float u) { c.Progress = u; c.Armed = u > 0.4f; return c; }
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Pull, Area = HazardShape.Pull, Ramp = FxRamp.Violet, Size = 2.0f * Tile, Seed = 5 }, 2, false, Growing));
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Mine, Area = HazardShape.Mine, Ramp = FxRamp.Violet, Size = 1.35f * Tile, Seed = 5 }, 2, false, Growing));
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Nova, Area = HazardShape.Ring, Ramp = FxRamp.Cold, Size = 2.2f * Tile, Width = 1.0f * Tile, Seed = 5 }, 1, false, Growing));
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Charge, Area = HazardShape.Line, Ramp = FxRamp.Plain, Size = 2.2f * Tile, Width = 1.5f * Tile, Dir = 0.5f, Seed = 5 }, 0.7f, false, Growing));
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Slam, Area = HazardShape.Circle, Ramp = FxRamp.Cold, Size = 0.75f * Tile, Harmless = true, Seed = 5 }, 0.5f, false, Growing));
            rows.Add(Row(MobFx.Mark, new MobContext { Shape = MonsterAttackShape.Sweep, Area = HazardShape.Cone, Ramp = FxRamp.Plain, Size = 2.2f * Tile, Width = 100, Dir = 0.4f, Seed = 5 }, 0.7f, false, Growing));
            // Blows of kinds no monster has yet.
            foreach (var shape in new[] { MonsterAttackShape.Sweep, MonsterAttackShape.Beam, MonsterAttackShape.Slam, MonsterAttackShape.Push, MonsterAttackShape.Mine })
                rows.Add(Row(MobFx.Land, new MobContext { Shape = shape, Ramp = shape == MonsterAttackShape.Mine ? FxRamp.Violet : shape == MonsterAttackShape.Push ? FxRamp.Spirit : FxRamp.Plain, Tint = green,
                    Size = (shape == MonsterAttackShape.Beam ? 2.2f : 1.6f) * Tile, Width = shape == MonsterAttackShape.Sweep ? 100 : 0.7f * Tile, Dir = 0.3f, Seed = 9 }, 0.45f, false));
            // One-offs and loops.
            foreach (var fx in new[] { MobFx.Bounce, MobFx.Turn, MobFx.BlinkOut, MobFx.Split, MobFx.Stagger, MobFx.Rally, MobFx.Deathburst, MobFx.Trip, MobFx.Guard })
                rows.Add(Row(fx, new MobContext { Ramp = fx == MobFx.BlinkOut || fx == MobFx.Turn ? FxRamp.Violet : fx == MobFx.Deathburst ? FxRamp.Cold : FxRamp.Plain, Tint = green, Size = 2 * Tile, Seed = 11 }, 0.4f, false));
            foreach (var fx in new[] { MobFx.Stars, MobFx.Empowered, MobFx.Tether, MobFx.Mended })
                rows.Add(Row(fx, new MobContext { Size = 2 * Tile, Dir = 0.3f, Seed = 11 }, 1, true));
            // The rest of the kinds (2b-2): a shadow step's smoke, a melted slime's steam, up out of the ground and back
            // under; then the loops: a mound lying in wait and one that travels, the line between two soldiers, poison,
            // a swoop's afterimages (crystal, ember, spore).
            foreach (var fx in new[] { MobFx.Vanish, MobFx.Quench, MobFx.Surface, MobFx.Dive })
                rows.Add(Row(fx, new MobContext { Ramp = fx == MobFx.Vanish ? FxRamp.Violet : FxRamp.Dust, Size = 2 * Tile, Seed = 11 }, 0.45f, false));
            rows.Add(Row(MobFx.Mound, new MobContext { Ramp = FxRamp.Dust, Seed = 2 }, 3, true));
            rows.Add(Row(MobFx.Mound, new MobContext { Ramp = FxRamp.Dust, Seed = 2, Armed = true, Dir = 0.3f }, 1.5f, true));
            rows.Add(Row(MobFx.Link, new MobContext { Size = 1.3f * Tile, Dir = 0.2f, Seed = 11 }, 1, true));
            rows.Add(Row(MobFx.Poisoned, new MobContext { Seed = 11 }, 1, true));
            foreach (var look in new[] { MobLook.Crystal, MobLook.Ember, MobLook.Spore })
                rows.Add(Row(MobFx.Wake, new MobContext { Look = look, Ramp = look == MobLook.Ember ? FxRamp.Fire : look == MobLook.Spore ? FxRamp.Spirit : FxRamp.Plain,
                    Tint = MonsterFx.TintOf(Rgba.Hex("#7CD2E2")), Dir = 0, Seed = 11 }, 1, true));
            foreach (var shape in new[] { MonsterAttackShape.Spread, MonsterAttackShape.Bounce, MonsterAttackShape.Boomerang })
                rows.Add(Row(MobFx.Shot, new MobContext { Shape = shape, Ramp = shape == MonsterAttackShape.Boomerang ? FxRamp.Violet : FxRamp.Plain, Tint = MonsterFx.TintOf(Rgba.Hex("#C8F4FF")), Dir = 0.3f, Seed = 11 }, 1, true));
            return (SkillFxPreview.Sheet(rows), fine);
        }

        // A piece laid on the picture as a sprite would be: centred on its place, turned without smoothing, in its
        // monster's colour when it is one of the greys.
        static void Blit(PixelImage image, float originX, float originY, FxStamp stamp)
        {
            var piece = SkillFxArt.Draw(stamp.Shape, stamp.Ramp, stamp.Size, stamp.Frame);
            if (piece == null) return;
            if (stamp.Tint != 0)
            {
                var tinted = new PixelImage(piece.Width, piece.Height);
                float r = (stamp.Tint >> 16 & 255) / 255f, g = (stamp.Tint >> 8 & 255) / 255f, b = (stamp.Tint & 255) / 255f;
                for (int y = 0; y < piece.Height; y++)
                    for (int x = 0; x < piece.Width; x++)
                    {
                        var c = piece[x, y];
                        if (c.A > 0) tinted[x, y] = new Rgba((int)(c.R * r), (int)(c.G * g), (int)(c.B * b), c.A);
                    }
                piece = tinted;
            }
            float cx = originX + stamp.X, cy = originY - stamp.Y;
            if (Math.Abs(stamp.Angle % 360) < 0.01f)
            {
                SkillFxPreview.Over(image, piece, (int)Math.Round(cx - piece.Width / 2f), (int)Math.Round(cy - piece.Height / 2f), stamp.Alpha);
                return;
            }
            double radians = stamp.Angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            int reach = (int)Math.Ceiling(Math.Sqrt(piece.Width * piece.Width + piece.Height * piece.Height) / 2) + 1;
            for (int y = (int)cy - reach; y <= (int)cy + reach; y++)
            {
                for (int x = (int)cx - reach; x <= (int)cx + reach; x++)
                {
                    double dx = x + 0.5 - cx, dy = -(y + 0.5 - cy);
                    double ux = dx * cos + dy * sin, uy = -dx * sin + dy * cos;
                    int sx = (int)Math.Floor(ux + piece.Width / 2.0), sy = (int)Math.Floor(-uy + piece.Height / 2.0);
                    if (!piece.Inside(sx, sy)) continue;
                    var c = piece[sx, sy];
                    if (c.A > 0 && image.Inside(x, y)) image.Blend(x, y, c.WithAlpha((int)(c.A * stamp.Alpha)));
                }
            }
        }
    }
}
