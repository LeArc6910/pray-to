# The tide serpent's five 64x64 views (to_sprite.py's v_<view>_px.png) -> SerpentArt.Rows.cs: one shared palette, the
# picture rows, and a part map per view (H head, N neck, C coils, E eye) from the hand-set LAYOUT below. Also writes
# a picture of the part map over the views to check by eye. Another boss: copy this and set its own LAYOUT.
#   python -I serpent_rows.py <views dir> Assets/_Project/Scripts/Client/Art/SerpentArt.Rows.cs parts.png
import sys, os
from PIL import Image, ImageDraw

src, out_cs, out_png = sys.argv[1], sys.argv[2], sys.argv[3]
names = ["Front", "FrontSide", "Side", "BackSide", "Back"]
files = ["front", "frontside", "side", "backside", "back"]

# Head: everything above this row (inside the x range); neck: inside the polygon; the rest is coils.
LAYOUT = {
    "Front":     dict(head=(17, 47, 23), neck=[(25, 23), (40, 23), (40, 52), (25, 52)], eyes=[(28, 19), (35, 19)]),
    "FrontSide": dict(head=(17, 47, 24), neck=[(26, 24), (40, 24), (45, 50), (30, 50)], eyes=[(34, 19), (37, 21)]),
    "Side":      dict(head=(19, 48, 23), neck=[(22, 23), (42, 23), (46, 34), (47, 45), (38, 45), (30, 34), (22, 31)], eyes=[(36, 16)]),
    "BackSide":  dict(head=(21, 48, 22), neck=[(21, 22), (35, 22), (37, 27), (46, 29), (47, 39), (36, 39), (28, 33), (21, 32)], eyes=[(38, 14)]),
    "Back":      dict(head=(17, 51, 19), neck=[(24, 19), (41, 19), (41, 40), (24, 40)], eyes=[]),
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
lines.append("    // The tide serpent's five views as the local image models drew them (Docs/ArtSources.md), and which part of the")
lines.append("    // body each pixel belongs to (H head, N neck, C coils, E an eye), for SerpentArt.")
lines.append("    static partial class SerpentArt")
lines.append("    {")
lines.append("        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>")
lines.append("        {")
for c in colours:
    lines.append(f"            ['{letter[c]}'] = new Rgba({c[0]}, {c[1]}, {c[2]}),")
lines.append("        };")
check = Image.new("RGBA", (5 * 68 * 6, 2 * 68 * 6), (40, 38, 50, 255))
tint = {"H": (255, 80, 80), "N": (255, 220, 60), "C": (60, 160, 255), "E": (255, 255, 255)}
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
            elif inside(lay["neck"], x + 0.5, y + 0.5):
                p += "N"
            else:
                p += "C"
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
    # check picture: plain on top, tinted parts below
    for y in range(64):
        for x in range(64):
            c = im.getpixel((x, y))
            if c[3] < 128:
                continue
            ch = parts[y][x]
            t = tint[ch]
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
