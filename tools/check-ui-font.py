#!/usr/bin/env python3
"""Check Chinese UI glyph coverage; optionally rebuild the existing Noto subset."""
import argparse
import re
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools import subset

ROOT=Path(__file__).resolve().parents[1]
FONT=ROOT/'unity-hair-salon/Assets/Resources/Fonts/NotoSansSC-UI.otf'

def required_glyphs():
    characters=set()
    for path in (ROOT/'unity-hair-salon/Assets/Scripts').rglob('*.cs'):
        for literal in re.findall(r'"(?:\\.|[^"\\])*"', path.read_text(encoding='utf-8')):
            characters.update(ord(c) for c in literal if '\u2e80' <= c <= '\u9fff')
    return characters

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,help='Full NotoSansCJKsc-Regular.otf from the licensed upstream distribution')
    args=parser.parse_args()
    original=TTFont(FONT)
    required=required_glyphs()
    if args.source:
        font=TTFont(args.source)
        keep=required | set(original.getBestCmap())
        missing=keep-set(font.getBestCmap())
        if missing:raise SystemExit('Source font is missing required glyphs: '+''.join(map(chr,sorted(missing))))
        options=subset.Options()
        options.name_IDs=['*'];options.name_legacy=True;options.name_languages=['*']
        worker=subset.Subsetter(options=options)
        worker.populate(unicodes=keep);worker.subset(font)
        font.save(FONT)
    actual=set(TTFont(FONT).getBestCmap())
    missing=required-actual
    if missing:raise SystemExit('UI font missing glyphs: '+''.join(map(chr,sorted(missing))))
    print(f'UI font verified: {len(required)} required CJK glyphs, {len(actual)} packaged glyphs.')

if __name__=='__main__':main()
