using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // 16px icons for the class skills (skills.csv), for the skill keys, the touch buttons and the bag's skill page.
    // Each is a dark tile in the skill's colour (its tags, as Palette.Of colours its effects) with a picture made of a
    // few shapes: rings, bursts, flames, arrows, bolts, snowflakes, spirits. A skill without a picture of its own is
    // drawn by its kind, so a new row in the sheet still gets an icon.
    static class SkillArt
    {
        public const int Size = 16;

        static readonly Rgba Outline = Rgba.Hex("#15100E");
        static readonly Rgba White = Rgba.Hex("#FFFFFF");
        static readonly Rgba Steel = Rgba.Hex("#E6E8F0");
        static readonly Rgba SteelDark = Rgba.Hex("#8A90A0");
        static readonly Rgba Wood = Rgba.Hex("#9A6A3C");
        static readonly Rgba Gold = Rgba.Hex("#F2CC5C");
        static readonly Rgba Leaf = Rgba.Hex("#8CD94D");
        static readonly Rgba Rock = Rgba.Hex("#6E5248");
        static readonly Rgba Heal = Rgba.Hex("#74F07E");

        // The skill's colour, the same choice as Palette.Of(SkillTag) makes for its effects.
        public static Rgba ColorOf(SkillTag tags)
        {
            if ((tags & SkillTag.Fire) != 0) return Rgba.Hex("#FF8026");
            if ((tags & SkillTag.Cold) != 0) return Rgba.Hex("#8CD9FF");
            if ((tags & SkillTag.Lightning) != 0) return Rgba.Hex("#FFF266");
            if ((tags & SkillTag.Summon) != 0) return Rgba.Hex("#73E6CC");
            if ((tags & SkillTag.Magic) != 0) return Rgba.Hex("#BF8CFF");
            if ((tags & SkillTag.Melee) != 0) return Rgba.Hex("#E6E6F2");
            return Rgba.Hex("#FFCC59");
        }

        public static PixelImage Icon(SkillDefinition skill)
        {
            var ramp = Ramp.Of(ColorOf(skill.Tags | skill.Grants));
            var image = Tile(ramp);
            var c = new Canvas(image, ramp);
            if (!Pictures.TryGetValue(skill.Id, out var draw)) draw = ByKind(skill.Kind);
            draw(c);
            return image;
        }

        // Whether the skill has a picture of its own rather than its kind's (ArtPreview checks the shipped ones do).
        public static bool HasOwnPicture(SkillDefinition skill) => Pictures.ContainsKey(skill.Id);

        // The tile: rounded corners, a dark fill of the colour lighter toward the top, and a bright top edge.
        static PixelImage Tile(Ramp ramp)
        {
            var image = new PixelImage(Size, Size);
            var deep = ramp.Dark.Scale(0.55f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool corner = (x == 0 || x == Size - 1) && (y == 0 || y == Size - 1);
                    if (corner) continue;
                    bool edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
                    image[x, y] = edge ? Outline : deep.Mix(ramp.Dark, 1 - y / (float)Size * 0.8f);
                }
            }
            for (int x = 2; x < Size - 2; x++) image[x, 1] = ramp.Shade;
            return image;
        }

        static Action<Canvas> ByKind(SkillKind kind) => kind switch
        {
            SkillKind.Nova => c => c.Burst(8, 8, 6),
            SkillKind.Projectile => c => c.Orb(9, 7, 3, trail: true),
            SkillKind.Buff => c => c.UpArrow(8, 3),
            SkillKind.Summon => c => c.Wisp(8, 8),
            SkillKind.Dash => c => c.Chevrons(4, 8),
            SkillKind.Heal => c => c.Cross(8, 8, Heal),
            _ => c => c.Disc(8, 8, 4, c.Ramp.Main),
        };

        static readonly Dictionary<string, Action<Canvas>> Pictures = new Dictionary<string, Action<Canvas>>
        {
            // Warrior
            ["whirlwind"] = c =>
            {
                c.Arc(8, 8, 6, 100, 240, c.Ramp.Light);
                c.Arc(8, 8, 6, 280, 420, c.Ramp.Light);
                c.Arc(8, 8, 4, 120, 210, c.Ramp.Shade);
                c.Arc(8, 8, 4, 300, 390, c.Ramp.Shade);
                c.Sword(8, 13, 8, 2);
            },
            ["war_cry"] = c =>
            {
                c.Horn(3, 8);
                c.Line(11, 4, 13, 2, c.Ramp.Light);
                c.Line(12, 8, 14, 8, c.Ramp.Light);
                c.Line(11, 12, 13, 14, c.Ramp.Light);
            },
            ["charge"] = c =>
            {
                c.Line(2, 5, 6, 5, c.Ramp.Shade);
                c.Line(1, 8, 6, 8, c.Ramp.Main);
                c.Line(2, 11, 6, 11, c.Ramp.Shade);
                c.Shield(10, 8);
            },
            ["ground_slam"] = c =>
            {
                c.Hammer(8, 2);
                c.Line(2, 13, 14, 13, Rock);
                c.Line(4, 12, 6, 10, c.Ramp.Light);
                c.Line(12, 12, 10, 10, c.Ramp.Light);
                c.Dot(8, 14, c.Ramp.Light);
            },
            ["iron_skin"] = c =>
            {
                c.Shield(8, 8);
                c.UpArrow(13, 5, small: true);
            },
            ["blade_storm"] = c =>
            {
                c.Sword(3, 13, 9, 7);
                c.Sword(13, 13, 7, 7);
                c.Sword(8, 14, 8, 6);
                c.Arc(8, 7, 6, 200, 340, c.Ramp.Light);
            },
            // Fire mage
            ["fireball"] = c => c.Orb(9, 7, 3, trail: true),
            ["flame_brand"] = c =>
            {
                c.Sword(4, 13, 12, 5);
                c.Flame(10, 9, 5, small: true);
            },
            ["fire_ring"] = c =>
            {
                c.Ring(8, 9, 5, c.Ramp.Main);
                c.Flame(3, 11, 4, small: true);
                c.Flame(13, 11, 4, small: true);
                c.Flame(8, 6, 4, small: true);
            },
            ["meteor"] = c =>
            {
                c.Line(2, 2, 7, 7, c.Ramp.Light);
                c.Line(4, 1, 8, 5, c.Ramp.Main);
                c.Line(1, 4, 5, 8, c.Ramp.Main);
                c.Disc(10, 10, 3, Rock);
                c.Dot(9, 9, c.Ramp.Light);
                c.Dot(11, 11, Rock.Scale(0.6f));
            },
            ["flame_burst"] = c =>
            {
                c.Orb(4, 4, 2, trail: false);
                c.Orb(11, 4, 2, trail: false);
                c.Orb(8, 10, 2, trail: false);
                c.Dot(2, 13, c.Ramp.Shade);
                c.Dot(13, 13, c.Ramp.Shade);
            },
            ["inferno"] = c =>
            {
                c.Flame(8, 14, 12, small: false);
                c.Flame(4, 14, 6, small: true);
                c.Flame(12, 14, 6, small: true);
            },
            // Archer
            ["volley"] = c =>
            {
                c.Arrow(1, 8, 9, 2);
                c.Arrow(3, 11, 12, 5);
                c.Arrow(5, 14, 14, 8);
            },
            ["eagle_eye"] = c => c.Eye(8, 8),
            ["gale_step"] = c =>
            {
                c.Feather(9, 3);
                c.Line(1, 10, 5, 10, c.Ramp.Shade);
                c.Line(2, 13, 7, 13, c.Ramp.Shade);
            },
            ["power_shot"] = c =>
            {
                c.Arrow(2, 13, 13, 2, thick: true);
                c.Line(9, 2, 13, 6, c.Ramp.Light);
            },
            ["explosive_arrow"] = c =>
            {
                c.Arrow(2, 13, 9, 6);
                c.Burst(11, 5, 3);
            },
            ["arrow_rain"] = c =>
            {
                c.Arrow(3, 1, 3, 9);
                c.Arrow(8, 3, 8, 12);
                c.Arrow(13, 1, 13, 9);
                c.Arrow(5, 6, 5, 14);
                c.Arrow(11, 6, 11, 14);
            },
            // Summoner
            ["spirits"] = c =>
            {
                c.Wisp(5, 6);
                c.Wisp(11, 10);
            },
            ["spirit_pact"] = c =>
            {
                c.Ring(6, 8, 3, c.Ramp.Main);
                c.Ring(10, 8, 3, c.Ramp.Light);
                c.UpArrow(13, 3, small: true);
            },
            ["life_spring"] = c =>
            {
                c.Drop(8, 2);
                c.Cross(8, 11, Heal, small: true);
            },
            ["spirit_horde"] = c =>
            {
                c.Wisp(4, 5, small: true);
                c.Wisp(11, 4, small: true);
                c.Wisp(4, 11, small: true);
                c.Wisp(11, 11, small: true);
            },
            ["spirit_blast"] = c =>
            {
                c.Burst(8, 8, 6);
                c.Wisp(8, 8, small: true);
            },
            ["guardian"] = c =>
            {
                c.Wisp(8, 9, big: true);
                c.Crown(8, 2);
            },
            // Storm caller
            ["chain_lightning"] = c =>
            {
                c.Bolt(4, 1, 9, 14);
                c.Line(9, 8, 13, 6, c.Ramp.Light);
                c.Dot(14, 5, c.Ramp.Light);
            },
            ["frost_ring"] = c =>
            {
                c.Ring(8, 8, 6, c.Ramp.Main);
                c.Snowflake(8, 8, 3);
            },
            ["ice_lance"] = c =>
            {
                c.Line(2, 13, 11, 4, c.Ramp.Main);
                c.Line(3, 13, 12, 4, c.Ramp.Shade);
                c.Line(11, 2, 14, 2, c.Ramp.Light);
                c.Line(14, 2, 14, 5, c.Ramp.Light);
                c.Line(11, 4, 13, 2, White);
            },
            ["storm_mantle"] = c =>
            {
                c.Cloud(8, 5);
                c.Bolt(7, 7, 9, 14, small: true);
                c.Snowflake(13, 12, 1);
            },
            ["thunderclap"] = c =>
            {
                c.Bolt(6, 1, 9, 10, small: true);
                c.Line(2, 13, 14, 13, c.Ramp.Shade);
                c.Line(3, 11, 1, 9, c.Ramp.Light);
                c.Line(13, 11, 15, 9, c.Ramp.Light);
            },
            ["blizzard"] = c =>
            {
                c.Snowflake(5, 5, 2);
                c.Snowflake(11, 9, 2);
                c.Snowflake(5, 12, 1);
                c.Line(9, 2, 14, 2, c.Ramp.Shade);
                c.Line(1, 9, 6, 9, c.Ramp.Shade);
            },
        };

        // The shapes, drawn into the tile's inside (the outline pixels stay).
        sealed class Canvas
        {
            readonly PixelImage _image;

            public Canvas(PixelImage image, Ramp ramp)
            {
                _image = image;
                Ramp = ramp;
            }

            public Ramp Ramp { get; }

            public void Dot(int x, int y, Rgba color)
            {
                if (x >= 1 && y >= 1 && x < Size - 1 && y < Size - 1) _image[x, y] = color;
            }

            public void Line(int x0, int y0, int x1, int y1, Rgba color)
            {
                int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
                int error = dx + dy;
                for (;;)
                {
                    Dot(x0, y0, color);
                    if (x0 == x1 && y0 == y1) return;
                    int e2 = 2 * error;
                    if (e2 >= dy)
                    {
                        error += dy;
                        x0 += sx;
                    }
                    if (e2 <= dx)
                    {
                        error += dx;
                        y0 += sy;
                    }
                }
            }

            public void Disc(int cx, int cy, int r, Rgba color)
            {
                for (int y = -r; y <= r; y++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + y * y <= r * r + r) Dot(cx + x, cy + y, color);
                    }
                }
            }

            public void Ring(int cx, int cy, int r, Rgba color) => Arc(cx, cy, r, 0, 360, color);

            // Degrees counted from east toward north (up on screen), from one angle round to the other.
            public void Arc(int cx, int cy, int r, float from, float to, Rgba color)
            {
                for (float a = from; a <= to; a += 4)
                {
                    double t = a * Math.PI / 180;
                    Dot(cx + (int)Math.Round(Math.Cos(t) * r), cy - (int)Math.Round(Math.Sin(t) * r), color);
                }
            }

            // Rays every 45 degrees around a bright middle.
            public void Burst(int cx, int cy, int r)
            {
                for (int i = 0; i < 8; i++)
                {
                    double t = i * Math.PI / 4;
                    int length = i % 2 == 0 ? r : r - 1;
                    Line(cx + (int)Math.Round(Math.Cos(t) * 2), cy - (int)Math.Round(Math.Sin(t) * 2),
                        cx + (int)Math.Round(Math.Cos(t) * length), cy - (int)Math.Round(Math.Sin(t) * length), i % 2 == 0 ? Ramp.Light : Ramp.Main);
                }
                Disc(cx, cy, 1, White);
            }

            // A ball with a light core, flying up and right with a trail behind it.
            public void Orb(int cx, int cy, int r, bool trail)
            {
                if (trail)
                {
                    Line(cx - r - 4, cy + r + 4, cx - 1, cy + 1, Ramp.Shade);
                    Line(cx - r - 2, cy + r + 4, cx, cy + 2, Ramp.Main);
                    Line(cx - r - 4, cy + r + 2, cx - 2, cy, Ramp.Main);
                }
                Disc(cx, cy, r, Ramp.Main);
                Disc(cx - 1, cy - 1, Math.Max(0, r - 2), Ramp.Light);
                Dot(cx - 1, cy - 1, White);
            }

            public void UpArrow(int cx, int top, bool small = false)
            {
                int height = small ? 4 : 10;
                Line(cx, top, cx, top + height, Ramp.Light);
                Line(cx - (small ? 1 : 3), top + (small ? 1 : 3), cx, top, Ramp.Light);
                Line(cx + (small ? 1 : 3), top + (small ? 1 : 3), cx, top, Ramp.Light);
                if (!small) Line(cx + 1, top + 1, cx + 1, top + height, Ramp.Main);
            }

            public void Chevrons(int x, int cy)
            {
                for (int i = 0; i < 3; i++)
                {
                    int left = x + i * 3;
                    Line(left, cy - 3, left + 3, cy, i == 2 ? Ramp.Light : Ramp.Main);
                    Line(left + 3, cy, left, cy + 3, i == 2 ? Ramp.Light : Ramp.Main);
                }
            }

            public void Cross(int cx, int cy, Rgba color, bool small = false)
            {
                int arm = small ? 2 : 4;
                for (int t = -1; t <= 0; t++)
                {
                    Line(cx - arm, cy + t, cx + arm - 1, cy + t, color);
                    Line(cx + t, cy - arm, cx + t, cy + arm - 1, color);
                }
            }

            // A blade from the hilt end (x0, y0) to the point (x1, y1), with a crossguard near the hilt.
            public void Sword(int x0, int y0, int x1, int y1)
            {
                int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
                int gx = x0 + dx * 2, gy = y0 + dy * 2;
                Line(gx, gy, x1, y1, Steel);
                Line(gx + (dy != 0 ? 1 : 0), gy + (dx != 0 ? 1 : 0), x1, y1, SteelDark);
                Line(gx, gy, x1, y1, Steel);
                // The guard across the blade.
                Line(gx - dy * 2 + (dx == 0 ? -2 : 0), gy + dx * 2, gx + dy * 2 + (dx == 0 ? 2 : 0), gy - dx * 2, Gold);
                Line(x0, y0, gx - dx, gy - dy, Wood);
            }

            public void Shield(int cx, int cy)
            {
                string[] rows =
                {
                    "sssssss",
                    "sMMLMMs",
                    "sMMLMMs",
                    "sLLLLLs",
                    "sMMLMMs",
                    ".sMLMs.",
                    "..sMs..",
                    "...s...",
                };
                Pattern(cx - 3, cy - 4, rows);
            }

            public void Hammer(int cx, int top)
            {
                Line(cx, top + 4, cx, top + 10, Wood);
                for (int y = top; y < top + 4; y++) Line(cx - 3, y, cx + 3, y, y == top ? Steel : SteelDark);
            }

            public void Horn(int x, int cy)
            {
                string[] rows =
                {
                    "......LL",
                    "....LLML",
                    "..yLMMML",
                    "yyyMMMSL",
                    "yyyMMMSL",
                    "..ySSSSL",
                    "....SSSL",
                    "......LL",
                };
                Pattern(x, cy - 4, rows);
            }

            // Tall tongues of fire standing on (cx, bottom).
            public void Flame(int cx, int bottom, int height, bool small)
            {
                int half = small ? 1 : 3;
                for (int i = 0; i < height; i++)
                {
                    int y = bottom - i;
                    float share = 1 - i / (float)height;
                    int width = Math.Max(0, (int)Math.Round(half * Math.Sqrt(share) + (i < height / 3 ? 0.5 : 0)));
                    for (int x = -width; x <= width; x++)
                    {
                        var color = Math.Abs(x) == width && width > 0 ? Ramp.Shade : i < height / 2 ? Ramp.Main : Ramp.Light;
                        Dot(cx + x, y, color);
                    }
                }
                Dot(cx, bottom - 1, White.Mix(Ramp.Light, 0.4f));
            }

            // An arrow from its nock (x0, y0) to its point (x1, y1).
            public void Arrow(int x0, int y0, int x1, int y1, bool thick = false)
            {
                Line(x0, y0, x1, y1, Wood);
                if (thick) Line(x0 + 1, y0, x1 + 1, y1, Wood.Scale(0.75f));
                int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
                // The head: the point and two barbs back from it.
                Dot(x1, y1, Steel);
                Dot(x1 - dx, y1 - dy, Steel);
                Dot(x1 - dx - (dy != 0 ? 1 : 0), y1 - dy + (dx != 0 && dy == 0 ? 1 : 0), Steel);
                Dot(x1 - dx + (dy != 0 && dx == 0 ? 1 : 0) - (dx != 0 && dy != 0 ? dx * 2 : 0), y1 - dy + (dx != 0 && dy == 0 ? -1 : 0), Steel);
                // Feathers at the nock.
                Dot(x0, y0, Leaf);
                Dot(x0 + dx, y0 + dy, Leaf);
                Dot(x0 - (dy != 0 ? 1 : 0), y0 + (dx != 0 ? 1 : 0), Leaf.Scale(0.75f));
            }

            public void Eye(int cx, int cy)
            {
                string[] rows =
                {
                    "...LLLLLL...",
                    ".LL......LL.",
                    "L....MM....L",
                    "L...MkkM...L",
                    "L...MkwM...L",
                    "L....MM....L",
                    ".LL......LL.",
                    "...LLLLLL...",
                };
                Pattern(cx - 6, cy - 4, rows);
            }

            public void Feather(int cx, int top)
            {
                string[] rows =
                {
                    "....L",
                    "...LM",
                    "..LMM",
                    ".LMMS",
                    ".LMS.",
                    "LMSs.",
                    "LMs..",
                    "Ls...",
                    "s....",
                };
                Pattern(cx - 2, top, rows);
            }

            // A spirit: a round head with a tail curling down to the left, and two dark eyes.
            public void Wisp(int cx, int cy, bool small = false, bool big = false)
            {
                int r = small ? 2 : big ? 4 : 3;
                Line(cx - 1, cy + r, cx - r - 1, cy + r + (small ? 1 : 3), Ramp.Shade);
                Line(cx, cy + r, cx - r, cy + r + (small ? 1 : 2), Ramp.Main);
                Disc(cx, cy, r, Ramp.Main);
                Disc(cx - 1, cy - 1, Math.Max(0, r - 2), Ramp.Light);
                Dot(cx - 1, cy, Outline);
                Dot(cx + 1, cy, Outline);
                if (big)
                {
                    Dot(cx - 2, cy, Outline);
                    Dot(cx + 2, cy, Outline);
                }
            }

            public void Crown(int cx, int top)
            {
                string[] rows =
                {
                    "y.y.y",
                    "yyyyy",
                    "yryry",
                };
                Pattern(cx - 2, top, rows);
            }

            public void Drop(int cx, int top)
            {
                string[] rows =
                {
                    "..L..",
                    "..L..",
                    ".LML.",
                    ".MMM.",
                    "LMwMS",
                    "LMMMS",
                    ".SSS.",
                };
                Pattern(cx - 2, top, rows);
            }

            // A zigzag of lightning from (x0, top) down to (x1, bottom).
            public void Bolt(int x0, int top, int x1, int bottom, bool small = false)
            {
                int middle = (top + bottom) / 2;
                int step = small ? 2 : 3;
                Line(x0 + step, top, x0 - 1 + step / 2, middle, Ramp.Light);
                Line(x0 - 1 + step / 2, middle, x1 + 1, middle, Ramp.Light);
                Line(x1 + 1, middle, x1 - 1, bottom, Ramp.Light);
                Line(x0 + step + 1, top, x0 + step / 2, middle - 1, Ramp.Main);
                Line(x1 + 2, middle, x1, bottom, Ramp.Main);
                Dot(x1 - 1, bottom, White);
            }

            public void Snowflake(int cx, int cy, int r)
            {
                Line(cx - r, cy, cx + r, cy, Ramp.Light);
                Line(cx, cy - r, cx, cy + r, Ramp.Light);
                Line(cx - r, cy - r, cx + r, cy + r, Ramp.Main);
                Line(cx - r, cy + r, cx + r, cy - r, Ramp.Main);
                Dot(cx, cy, White);
            }

            public void Cloud(int cx, int cy)
            {
                Disc(cx - 3, cy + 1, 2, Ramp.Shade);
                Disc(cx + 3, cy + 1, 2, Ramp.Shade);
                Disc(cx, cy - 1, 3, Ramp.Main);
                Disc(cx - 1, cy - 2, 1, Ramp.Light);
            }

            // Rows of characters: L M S D the skill's shades, s and t steel, w white, k dark, y gold, r red.
            void Pattern(int left, int top, string[] rows)
            {
                for (int y = 0; y < rows.Length; y++)
                {
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        Rgba? color = rows[y][x] switch
                        {
                            'L' => Ramp.Light,
                            'M' => Ramp.Main,
                            'S' => Ramp.Shade,
                            'D' => Ramp.Dark,
                            's' => Steel,
                            't' => SteelDark,
                            'w' => White,
                            'k' => Outline,
                            'y' => Gold,
                            'r' => Rgba.Hex("#D8483A"),
                            _ => null,
                        };
                        if (color is Rgba set) Dot(left + x, top + y, set);
                    }
                }
            }
        }
    }
}
