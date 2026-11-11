# Turns a small PNG (a CC0 pack's icon, Docs/ArtSources.md) into the rows the code draws from: PixelImage.FromRows(
# colours, rows...), as in Assets/_Project/Scripts/Client/Art/*.cs. Each colour gets a letter; the darkest is 'o'
# (the outline). With --ramp, the colours of one material are given the letters of a ramp (L light, M main, m shade,
# d dark, from lightest to darkest) so the shape can be painted in any tier's colours, as ItemArt.Bar is.
#
#   python3 DevTools/ArtImport/to_rows.py icon.png
#   python3 DevTools/ArtImport/to_rows.py icon.png --ramp "#B6BEB0,#8D977F,#5F7160,#485341"
#
# Needs Pillow (pip install pillow). Pass the PNG's path as an argument; the tool only reads it.
import argparse
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("png")
parser.add_argument("--ramp", help="comma-separated colours of one material, lightest first")
args = parser.parse_args()

image = Image.open(args.png).convert("RGBA")
ramp = [c.strip().upper() for c in args.ramp.split(",")] if args.ramp else []
ramp_letters = "LMmd"
spare = "abcefghijklnpqrstuvwxyzABCDEFGHIJKNOPQRSTUVWXYZ0123456789"
colours = {}
for y in range(image.height):
    for x in range(image.width):
        r, g, b, a = image.getpixel((x, y))
        if a >= 128:
            colours.setdefault("#%02X%02X%02X" % (r, g, b), 0)
            colours["#%02X%02X%02X" % (r, g, b)] += 1

def luminance(hex_colour):
    r, g, b = (int(hex_colour[i:i + 2], 16) for i in (1, 3, 5))
    return 0.299 * r + 0.587 * g + 0.114 * b

darkest = min(colours, key=luminance) if colours else None
letters = {}
for colour in sorted(colours, key=luminance, reverse=True):
    if colour == darkest:
        letters[colour] = "o"
    elif colour in ramp and ramp.index(colour) < len(ramp_letters):
        letters[colour] = ramp_letters[ramp.index(colour)]
    else:
        letters[colour] = spare[len([c for c in letters.values() if c in spare])]

print("var colors = new Dictionary<char, Rgba>\n{")
for colour, letter in letters.items():
    print(f"    ['{letter}'] = Rgba.Hex(\"{colour}\"),")
print("};")
print("PixelImage.FromRows(colors,")
rows = []
for y in range(image.height):
    row = ""
    for x in range(image.width):
        r, g, b, a = image.getpixel((x, y))
        row += "." if a < 128 else letters["#%02X%02X%02X" % (r, g, b)]
    rows.append(row)
print(",\n".join(f'    "{row}"' for row in rows) + ");")
