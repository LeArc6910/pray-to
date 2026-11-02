#!/usr/bin/env python3
"""Makes the game's pixel UI font from Galmuri11 Bold (SIL OFL 1.1, quiple), the bold 12-pixel cut of Galmuri.

Bold, because its every glyph is the author's own (the regular cut's Hangul started from a game console's font).
It keeps the same characters as the smooth font (make-korean-font.py): ASCII, Latin-1, punctuation, Hangul jamo and
the 2,350 syllables of KS X 1001, and every character in the game's scripts and sheets. The game draws it at 12
pixels or a multiple, with nearest-pixel filtering, so its pixels stay square (Ui.Size, Docs/ArtPipeline.md).

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
