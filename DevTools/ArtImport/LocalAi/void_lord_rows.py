# The void lord's five 64x64 views (to_sprite.py's v_<view>_px.png) -> VoidLordArt.Rows.cs: one shared palette, the
# picture rows, a part map per view (K crown, H head, E eye, B body, A the orb arm, L the empty arm, T the hem) and the
# rig (shoulders, the hand that holds the orb, whether the orb is behind him) from the hand-set LAYOUT below. The orb and
# the sparkles the picture had round him are left out (the code draws its own orb and stars): only the largest piece of
# the picture is kept, less anything inside a view's "erase" polygons. Also writes a picture of the part map over the
# views to check by eye.
#   python -I void_lord_rows.py <views dir> Assets/_Project/Scripts/Client/Art/VoidLordArt.Rows.cs parts.png
import sys, os
from PIL import Image

src, out_cs, out_png = sys.argv[1], sys.argv[2], sys.argv[3]
names = ["Front", "FrontSide", "Side", "BackSide", "Back"]
files = ["front", "frontside", "side", "backside", "back"]

# crown: above row cy inside x range; head: box; eyes; arm/off: polygons (the orb arm and the empty arm); hem: rows at
# and below hem (outside the arms); rig: (shoulder x, y, empty shoulder x, y, hand x, y, orb behind).
LAYOUT = {
    "Front": dict(crown=(23, 34, 10), head=(25, 32, 10, 13), eyes=[(27, 11), (30, 11)], hem=40,
                  arm=[[(34, 24), (40, 20), (44, 17), (50, 17), (50, 21), (44, 23), (39, 26), (34, 26)]],
                  off=[[(16, 17), (23, 17), (22, 24), (19, 38), (12, 38), (13, 27)]],
                  rig=(36, 24, 21, 19, 47, 18, False)),
    "FrontSide": dict(crown=(24, 33, 15), head=(25, 32, 15, 17), eyes=[(29, 16), (31, 16)], hem=44, erase=[[(43, 10), (52, 10), (52, 15.5), (43, 15.5)]],
                      arm=[[(34, 24), (40, 21), (44, 15.5), (51, 15.5), (51, 19), (45, 22), (40, 25), (34, 26)]],
                      off=[[(17, 25), (24, 25), (23, 44), (15, 44), (15, 30)]],
                      rig=(37, 23, 21, 26, 46, 17, False)),
    "Side": dict(crown=(26, 34, 18), head=(26, 34, 18, 21), eyes=[(32, 20)], hem=46, erase=[[(38, 8), (47, 8), (47, 14.5), (38, 14.5)]],
                 arm=[[(32, 24), (37, 19), (39, 14.5), (46, 14.5), (46, 18), (41, 21), (37, 25), (32, 26)]],
                 rig=(34, 23, 34, 23, 42, 16, False)),
    "BackSide": dict(crown=(28, 35, 14), head=(29, 35, 14, 17), eyes=[], hem=43, erase=[[(35.5, 8), (42, 8), (42, 13.5), (35.5, 13.5)]],
                     arm=[[(32, 13.5), (37, 13.5), (37, 22), (33, 22)]],
                     off=[[(37, 26), (44, 26), (45, 36), (38, 36)]],
                     rig=(34, 21, 39, 26, 35, 14, True)),
    "Back": dict(crown=(29, 38, 9), head=(29, 38, 9, 13), eyes=[], hem=37,
                 arm=[[(12, 16), (20, 16), (23, 22), (24, 25), (19, 25), (12, 21)]],
                 rig=(22, 23, 22, 23, 16, 18, True)),
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


def keep_largest(im, erase):
    px = im.load()
    for y in range(64):
        for x in range(64):
            if px[x, y][3] >= 128 and any(inside(p, x + 0.5, y + 0.5) for p in erase):
                px[x, y] = (0, 0, 0, 0)
    seen, best = set(), []
    for y in range(64):
        for x in range(64):
            if (x, y) in seen or px[x, y][3] < 128:
                continue
            piece, stack = [], [(x, y)]
            seen.add((x, y))
            while stack:
                a, b = stack.pop()
                piece.append((a, b))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        n = (a + dx, b + dy)
                        if 0 <= n[0] < 64 and 0 <= n[1] < 64 and n not in seen and px[n][3] >= 128:
                            seen.add(n)
                            stack.append(n)
            if len(piece) > len(best):
                best = piece
    keep = set(best)
    for y in range(64):
        for x in range(64):
            if (x, y) not in keep:
                px[x, y] = (0, 0, 0, 0)
    return im


imgs = [keep_largest(Image.open(os.path.join(src, f"v_{f}_px.png")).convert("RGBA"), LAYOUT[n].get("erase", [])) for f, n in zip(files, names)]
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
lines.append("    // The void lord's five views as the local image models drew them (Docs/ArtSources.md), which part of him each")
lines.append("    // pixel belongs to (K crown, H head, E an eye, B body, A the orb arm, L the empty arm, T the hem) and where his")
lines.append("    // shoulders and the orb hand are, for VoidLordArt. Made by DevTools/ArtImport/LocalAi/void_lord_rows.py.")
lines.append("    static partial class VoidLordArt")
lines.append("    {")
lines.append("        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>")
lines.append("        {")
for c in colours:
    lines.append(f"            ['{letter[c]}'] = new Rgba({c[0]}, {c[1]}, {c[2]}),")
lines.append("        };")
check = Image.new("RGBA", (5 * 68 * 6, 2 * 68 * 6), (40, 38, 50, 255))
tint = {"K": (120, 255, 255), "H": (255, 80, 80), "E": (255, 255, 255), "B": (60, 160, 255), "A": (255, 220, 60), "L": (80, 255, 120), "T": (200, 90, 255)}
for k, (name, im) in enumerate(zip(names, imgs)):
    lay = LAYOUT[name]
    rows, parts = [], []
    kx0, kx1, ky = lay["crown"]
    hx0, hx1, hy0, hy1 = lay["head"]
    for y in range(64):
        r, p = "", ""
        for x in range(64):
            c = im.getpixel((x, y))
            if c[3] < 128:
                r += "."
                p += "."
                continue
            r += letter[c[:3]]
            if (x, y) in lay["eyes"]:
                p += "E"
            elif y < ky and kx0 <= x <= kx1:
                p += "K"
            elif hx0 <= x <= hx1 and hy0 <= y <= hy1:
                p += "H"
            elif any(inside(poly, x + 0.5, y + 0.5) for poly in lay.get("arm", [])):
                p += "A"
            elif any(inside(poly, x + 0.5, y + 0.5) for poly in lay.get("off", [])):
                p += "L"
            elif y >= lay["hem"]:
                p += "T"
            else:
                p += "B"
        rows.append(r)
        parts.append(p)
    sx, sy, ox, oy, hx, hy, behind = lay["rig"]
    lines.append("")
    lines.append(f"        static readonly (float, float, float, float, float, float, bool) {name}Rig = ({sx}f, {sy}f, {ox}f, {oy}f, {hx}f, {hy}f, {'true' if behind else 'false'});")
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
            m = tuple(int(c[i] * 0.4 + t[i] * 0.6) for i in range(3)) + (255,)
            for dy in range(6):
                for dx in range(6):
                    check.putpixel((k * 408 + 12 + x * 6 + dx, 12 + y * 6 + dy), c)
                    check.putpixel((k * 408 + 12 + x * 6 + dx, 408 + 12 + y * 6 + dy), m)
    for (px_, py_) in ((sx, sy), (ox, oy), (hx, hy)):
        for d in range(-4, 5):
            for qx, qy in ((px_ * 6 + d, py_ * 6), (px_ * 6, py_ * 6 + d)):
                X, Y = int(k * 408 + 12 + qx + 3), int(408 + 12 + qy + 3)
                if 0 <= X < check.width and 0 <= Y < check.height:
                    check.putpixel((X, Y), (255, 0, 255, 255))
lines.append("    }")
lines.append("}")
with open(out_cs, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
check.save(out_png)
print(len(colours), "colours")
