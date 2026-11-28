# The lava titan's five 64x64 views (to_sprite.py's v_<view>_px.png) -> LavaTitanArt.Rows.cs: one shared palette, the
# picture rows, and a part map per view (H head, A the near arm or the body's right one, B the far or other one,
# L the near or left leg, M the other, T the trunk, E an eye) from the hand-set LAYOUT below. Also writes a picture of
# the part map over the views to check by eye. Made after serpent_rows.py (2026-11-28).
#   python -I titan_rows.py <views dir> Assets/_Project/Scripts/Client/Art/LavaTitanArt.Rows.cs parts.png
import sys, os
from PIL import Image

src, out_cs, out_png = sys.argv[1], sys.argv[2], sys.argv[3]
names = ["Front", "FrontSide", "Side", "BackSide", "Back"]
files = ["front", "frontside", "side", "backside", "back"]

# head: (x0, x1, below this row); arms: polygons; legs: below the hip row, split at x (L left of it unless near_right);
# eyes: pixels.
LAYOUT = {
    "Front":     dict(head=(28, 42, 21), a=[(2, 12), (21, 12), (21, 57), (2, 57)], b=[(45, 12), (63, 12), (63, 57), (45, 57)],
                      hip=50, split=32, near_right=False, eyes=[(33, 14), (38, 14)]),
    "FrontSide": dict(head=(31, 45, 20), a=[(5, 14), (23, 14), (25, 28), (22, 54), (5, 54)], b=[(47, 24), (59, 24), (59, 45), (47, 45)],
                      hip=48, split=30, near_right=False, eyes=[(39, 14), (43, 14)]),
    "Side":      dict(head=(32, 46, 19), a=[(27, 19), (40, 19), (50, 26), (50, 40), (41, 46), (30, 43), (27, 30)], b=[],
                      hip=46, split=31, near_right=True, eyes=[(42, 13)]),
    "BackSide":  dict(head=(30, 43, 13), a=[(36, 30), (48, 30), (55, 46), (55, 59), (44, 59), (38, 44)], b=[(7, 13), (21, 13), (21, 34), (7, 34)],
                      hip=44, split=34, near_right=True, eyes=[]),
    "Back":      dict(head=(27, 39, 17), a=[(45, 13), (62, 13), (62, 51), (45, 51)], b=[(3, 13), (20, 13), (21, 51), (3, 51)],
                      hip=46, split=33, near_right=False, eyes=[]),
}


def inside(poly, x, y):
    c = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c


imgs = [Image.open(os.path.join(src, f"v_{f}_px.png")).convert("RGBA") for f in files]
lum = lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
colours = sorted({im.getpixel((x, y))[:3] for im in imgs for y in range(64) for x in range(64) if im.getpixel((x, y))[3] >= 128}, key=lum)
letters = "abcdefghijklmnopqrstuvwxyz"
assert len(colours) <= len(letters), len(colours)
letter = {c: letters[i] for i, c in enumerate(colours)}

lines = []
lines.append("using System.Collections.Generic;")
lines.append("")
lines.append("namespace Game.Client")
lines.append("{")
lines.append("    // The lava titan's five views as the local image models drew them (Docs/ArtSources.md), and which part of the")
lines.append("    // body each pixel belongs to (H head, A/B the arms, L/M the legs, T the trunk, E an eye), for LavaTitanArt.")
lines.append("    static partial class LavaTitanArt")
lines.append("    {")
lines.append("        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>")
lines.append("        {")
for c in colours:
    lines.append(f"            ['{letter[c]}'] = new Rgba({c[0]}, {c[1]}, {c[2]}),")
lines.append("        };")
check = Image.new("RGBA", (5 * 68 * 6, 2 * 68 * 6), (40, 38, 50, 255))
tint = {"H": (255, 80, 80), "A": (255, 220, 60), "B": (200, 120, 255), "L": (60, 160, 255), "M": (60, 230, 140), "T": (150, 150, 150), "E": (255, 255, 255)}
for k, (name, im) in enumerate(zip(names, imgs)):
    lay = LAYOUT[name]
    rows, parts = [], []
    for y in range(64):
        r, p = "", ""
        for x in range(64):
            c = im.getpixel((x, y))
            if c[3] < 128:
                r += "."
                p += "."
                continue
            r += letter[c[:3]]
            hx0, hx1, hy = lay["head"]
            if (x, y) in lay["eyes"]:
                p += "E"
            elif y < hy and hx0 <= x <= hx1:
                p += "H"
            elif lay["a"] and inside(lay["a"], x + 0.5, y + 0.5):
                p += "A"
            elif lay["b"] and inside(lay["b"], x + 0.5, y + 0.5):
                p += "B"
            elif y >= lay["hip"]:
                left = x < lay["split"]
                p += ("M" if left else "L") if lay["near_right"] else ("L" if left else "M")
            else:
                p += "T"
        rows.append(r)
        parts.append(p)
    lines.append("")
    lines.append(f"        static readonly string[] {name}Rows =")
    lines.append("        {")
    lines += [f'            "{r}",' for r in rows]
    lines.append("        };")
    lines.append("")
    lines.append(f"        static readonly string[] {name}Parts =")
    lines.append("        {")
    lines += [f'            "{p}",' for p in parts]
    lines.append("        };")
    for y in range(64):
        for x in range(64):
            c = im.getpixel((x, y))
            if c[3] < 128:
                continue
            t = tint[parts[y][x]]
            m = tuple(int(c[i] * 0.45 + t[i] * 0.55) for i in range(3)) + (255,)
            for dy in range(6):
                for dx in range(6):
                    check.putpixel((k * 408 + 12 + x * 6 + dx, 12 + y * 6 + dy), c)
                    check.putpixel((k * 408 + 12 + x * 6 + dx, 408 + 12 + y * 6 + dy), m)
lines.append("    }")
lines.append("}")
with open(out_cs, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
check.save(out_png)
print(len(colours), "colours")
