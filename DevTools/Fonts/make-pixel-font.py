#!/usr/bin/env python3
"""Makes the game's pixel UI font from Galmuri11 Bold (SIL OFL 1.1, quiple), the bold 12-pixel cut of Galmuri.

Bold, because its every glyph is the author's own (the regular cut's Hangul started from a game console's font).
It keeps the same characters as the smooth font (make-korean-font.py): ASCII, Latin-1, punctuation, Hangul jamo and
the 2,350 syllables of KS X 1001, and every character in the game's scripts and sheets. The game draws it at 12
pixels or a multiple, with nearest-pixel filtering, so its pixels stay square (Ui.Text, Docs/WebDemo.md). Its line
is moved so the letters sit in the middle of it (center_vertically).

    curl -fsSL -o galmuri.tgz https://registry.npmjs.org/galmuri/-/galmuri-2.40.3.tgz && tar xzf galmuri.tgz
    python3 DevTools/Fonts/make-pixel-font.py package/dist/Galmuri11-Bold.ttf Assets/_Project/Resources/Fonts/Galmuri11Bold-Game.ttf

Needs fonttools (pip install fonttools).
"""
import importlib.util
import pathlib
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

sys.dont_write_bytecode = True  # no __pycache__ beside the scripts
HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("korean", HERE / "make-korean-font.py")
korean = importlib.util.module_from_spec(spec)
spec.loader.exec_module(korean)


# Galmuri11 puts its 11-pixel letters (0 to 1100 of 1200 units a 12-pixel em) under a 12-pixel ascent, with 2
# pixels of descent and 2 of line gap below: a 16-pixel line with the letters 1 pixel from its top and 4 from its
# bottom, so Unity's centred labels sat 1.5 pixels high and touched the top of bars and buttons (2026-11-02 check).
# The same 16-pixel line as 13 above the baseline and 3 below, no gap: 2 pixels over the letters and 3 under,
# whole pixels so the baseline stays on the pixel grid.
ASCENT = 1300
DESCENT = -300


def center_vertically(font):
    hhea, os2 = font["hhea"], font["OS/2"]
    hhea.ascent, hhea.descent, hhea.lineGap = ASCENT, DESCENT, 0
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ASCENT, DESCENT, 0
    os2.usWinAscent = max(ASCENT, font["head"].yMax)
    os2.usWinDescent = max(-DESCENT, -font["head"].yMin)


def main(source, target):
    font = TTFont(source)
    options = subset.Options()
    options.name_IDs = ["*"]  # keep the copyright and license records
    options.name_languages = ["*"]
    options.layout_features = ["*"]
    options.hinting = True
    subsetter = subset.Subsetter(options)
    wanted = korean.characters()
    subsetter.populate(unicodes=wanted)
    subsetter.subset(font)
    center_vertically(font)
    have = font.getBestCmap()
    missing = sorted(c for c in wanted if c not in have)
    pathlib.Path(target).parent.mkdir(parents=True, exist_ok=True)
    font.save(target)
    print(f"{target}: {len(have)} characters, {pathlib.Path(target).stat().st_size / 1024:.0f} KB")
    if missing:
        print("Missing:", "".join(chr(c) for c in missing))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
