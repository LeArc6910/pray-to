# A big generated picture -> a small game sprite (Docs/ArtSources.md 로컬 AI로 보스 그림, 2026-11-28): the flat
# background flood-filled away, cut to the body, shrunk by averaging so the FIRST picture's longer side is <fit> pixels
# (every other view at the same scale), one shared palette of <colors> colours, single stray pixels cleaned, a dark
# outline round the silhouette; each put on a <canvas> square standing on its bottom row.
#   python -I to_sprite.py <fit> <colors> <canvas> <out_dir> front.png frontside.png side.png backside.png back.png
# Needs Pillow. The tide serpent: python -I to_sprite.py 60 24 64 views v_front.png v_frontside.png ...
import sys, os
from collections import Counter
from PIL import Image, ImageFilter

fit, ncol, canvas, out_dir = int(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
inputs = sys.argv[5:]
os.makedirs(out_dir, exist_ok=True)


def cut_background(im, tol=48):
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()
    border = Counter(px[x, y] for x in range(w) for y in (0, h - 1))
    border.update(px[x, y] for y in range(h) for x in (0, w - 1))
    bg = border.most_common(1)[0][0]
    near = lambda c: sum(abs(a - b) for a, b in zip(c, bg)) <= tol
    mask = Image.new("L", (w, h), 255)
    m = mask.load()
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if m[x, y] == 0 or not near(px[x, y]):
            continue
        m[x, y] = 0
        if x > 0: stack.append((x - 1, y))
        if x < w - 1: stack.append((x + 1, y))
        if y > 0: stack.append((x, y - 1))
        if y < h - 1: stack.append((x, y + 1))
    out = im.convert("RGBA")
    out.putalpha(mask.filter(ImageFilter.MinFilter(3)))
    return out


CELL = []
def shrink(im):
    box = im.getchannel("A").getbbox()
    im = im.crop(box)
    w, h = im.size
    if not CELL: CELL.append(max(w, h) / fit)  # the first input sets the scale for all
    cell = CELL[0]
    tw, th = max(1, round(w / cell)), max(1, round(h / cell))
    # premultiplied area average
    rgb = Image.new("RGB", im.size, (0, 0, 0))
    rgb.paste(im.convert("RGB"), (0, 0), im)
    a = im.getchannel("A")
    small_rgb = rgb.resize((tw, th), Image.BOX)
    small_a = a.resize((tw, th), Image.BOX)
    sp, sa = small_rgb.load(), small_a.load()
    out = Image.new("RGBA", (tw, th))
    o = out.load()
    for y in range(th):
        for x in range(tw):
            al = sa[x, y]
            if al < 110:
                o[x, y] = (0, 0, 0, 0)
            else:
                r, g, b = sp[x, y]
                k = 255 / al
                o[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), 255)
    return out


smalls = [(p, shrink(cut_background(Image.open(p)))) for p in inputs]
strip = Image.new("RGB", (sum(s.width for _, s in smalls), max(s.height for _, s in smalls)))
x = 0
for _, s in smalls:
    strip.paste(s.convert("RGB"), (x, 0), s)
    x += s.width
pal = strip.quantize(ncol, method=Image.Quantize.MAXCOVERAGE, kmeans=4)


def clean(q):
    px = q.load()
    w, h = q.size
    for _ in range(2):
        changes = []
        for y in range(h):
            for x in range(w):
                c = px[x, y]
                if c[3] == 0:
                    continue
                nb = [px[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if 0 <= x + dx < w and 0 <= y + dy < h]
                if c not in nb:
                    common = Counter(n for n in nb if n[3]).most_common(1)
                    if common and common[0][1] >= 2:
                        changes.append((x, y, common[0][0]))
        for x, y, c in changes:
            px[x, y] = c
    return q


def outline(q):
    px = q.load()
    w, h = q.size
    cols = sorted({px[x, y] for y in range(h) for x in range(w) if px[x, y][3]}, key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2])
    dark = cols[0]
    lum = lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]
    edge = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0:
                continue
            if any(not (0 <= x + dx < w and 0 <= y + dy < h) or px[x + dx, y + dy][3] == 0 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                if lum(px[x, y]) > lum(dark) + 40:
                    edge.append((x, y))
    for x, y in edge:
        px[x, y] = dark
    return q


for p, s in smalls:
    q = s.convert("RGB").quantize(palette=pal, dither=Image.Dither.NONE).convert("RGBA")
    q.putalpha(s.getchannel("A"))
    q = outline(clean(q))
    sheet = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    sheet.paste(q, ((canvas - q.width) // 2, canvas - q.height - 1), q)
    name = os.path.splitext(os.path.basename(p))[0]
    sheet.save(os.path.join(out_dir, name + "_px.png"))
    print(name, q.size)
