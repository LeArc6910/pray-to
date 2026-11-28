# The crown wraith's five 64x64 views (to_sprite.py's v_<view>_px.png) -> CrownWraithArt.Rows.cs: one shared palette,
# the picture rows, and a part map per view from the hand-set LAYOUT below: each part a polygon, the first that holds
# a pixel wins (K crown, H head, 1 and 2 the arms, each turned about its own shoulder pivot, T the ghostly tail), E the
# eyes; the rest is the body (B); "S" a few hand-set pixels of the skull's face. Also writes a picture of the part map
# over the views to check by eye.
#   python -I crown_wraith_rows.py <views dir> Assets/_Project/Scripts/Client/Art/CrownWraithArt.Rows.cs parts.png
import sys, os, json
from PIL import Image

src, out_cs, out_png = sys.argv[1], sys.argv[2], sys.argv[3]
names = ["Front", "FrontSide", "Side", "BackSide", "Back"]
files = ["front", "frontside", "side", "backside", "back"]

# Polygons per view and part, in picture pixels; kept in crown_wraith_layout.json next to this script.
LAYOUT = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "crown_wraith_layout.json"), encoding="utf-8"))
ORDER = ["K", "H", "1", "2", "T"]


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
# The skull's face is four or five pixels wide at this size and the shrink blurs it into a grey smear: each view may lay
# a few hand-set pixels over it ("S": its top left and rows of w bone, g bone in shade, k a socket, . left as it is).
BONE = {"w": (226, 222, 204), "g": (150, 146, 134), "k": (26, 22, 30)}
for name, im in zip(names, imgs):
    face = LAYOUT[name].get("S")
    if not face:
        continue
    for dy, row in enumerate(face["rows"]):
        for dx, ch in enumerate(row):
            if ch in BONE:
                im.putpixel((face["x"] + dx, face["y"] + dy), BONE[ch] + (255,))
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
lines.append("    // The crown wraith's five views as the local image models drew them (Docs/ArtSources.md), and which part of the")
lines.append("    // body each pixel belongs to (K crown, H head, 1 and 2 the arms, T the ghostly tail, E an eye, B the rest) with the")
lines.append("    // shoulder each arm turns about, for CrownWraithArt.")
lines.append("    static partial class CrownWraithArt")
lines.append("    {")
lines.append("        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>")
lines.append("        {")
for c in colours:
    lines.append(f"            ['{letter[c]}'] = new Rgba({c[0]}, {c[1]}, {c[2]}),")
lines.append("        };")
check = Image.new("RGBA", (5 * 68 * 6, 2 * 68 * 6), (40, 38, 50, 255))
tint = {"K": (255, 220, 60), "H": (255, 80, 80), "1": (80, 255, 120), "2": (40, 150, 60), "T": (180, 120, 255), "B": (60, 160, 255), "E": (255, 255, 255)}
for k, (name, im) in enumerate(zip(names, imgs)):
    lay = LAYOUT[name]
    eyes = {tuple(e) for e in lay.get("E", [])}
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
            part = "B"
            if (x, y) in eyes:
                part = "E"
            else:
                for key in ORDER:
                    polys = [lay[key]["poly"]] if key in "12" and key in lay else lay.get(key, [])
                    if any(inside(poly, x + 0.5, y + 0.5) for poly in polys):
                        part = key
                        break
            p += part
        rows.append(r)
        parts.append(p)
    lines.append("")
    lines.append(f"        static readonly string[] {name}Rows =")
    lines.append("        {")
    lines += [f'            "{r}",' for r in rows]
    lines.append("        };")
    lines.append("")
    pivots = [lay[k]["pivot"] if k in lay else [0, 0] for k in "12"]
    lines.append(f"        static readonly (int X, int Y)[] {name}Pivots = {{ ({pivots[0][0]}, {pivots[0][1]}), ({pivots[1][0]}, {pivots[1][1]}) }};")
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
