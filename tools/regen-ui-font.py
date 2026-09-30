#!/usr/bin/env python3
"""从完整 Noto Sans CJK 字体确定性重建 Unity UI 子集字体。"""
import argparse
import re
from pathlib import Path
from tempfile import NamedTemporaryFile

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
FONT = ROOT / "unity-hair-salon/Assets/Resources/Fonts/NotoSansSC-UI.otf"
DEFAULT_SOURCE = Path(
    "/Users/kker/Library/Application Support/TRAE SOLO CN/ModularData/ai-agent/"
    "vm/tools/share/fonts/opentype/noto-cjk/NotoSansCJKsc-Regular.otf"
)
CJK_RE = re.compile(r"[\u2e80-\u9fff]")


def source_texts():
    paths = list((ROOT / "unity-hair-salon/Assets/Scripts").rglob("*.cs"))
    paths += list((ROOT / "unity-hair-salon/Assets/Resources").rglob("*.json"))
    return (path.read_text(encoding="utf-8") for path in sorted(paths))


def required_characters():
    chars = set()
    for text in source_texts():
        chars.update(ord(match.group()) for match in CJK_RE.finditer(text))
    return chars


def rebuild(source):
    existing = TTFont(FONT)
    existing_cmap = set(existing.getBestCmap())
    keep = existing_cmap | required_characters()

    full = TTFont(source)
    missing = keep - set(full.getBestCmap())
    if missing:
        raise SystemExit(
            "源字体缺少字符：" + "".join(chr(codepoint) for codepoint in sorted(missing))
        )

    options = subset.Options()
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.name_languages = ["*"]
    worker = subset.Subsetter(options=options)
    worker.populate(unicodes=sorted(keep))
    worker.subset(full)

    with NamedTemporaryFile(dir=FONT.parent, suffix=".otf", delete=False) as tmp:
        temporary = Path(tmp.name)
    try:
        full.save(temporary)
        temporary.replace(FONT)
    finally:
        temporary.unlink(missing_ok=True)

    actual = set(TTFont(FONT).getBestCmap())
    print(
        f"UI font regenerated: {len(existing_cmap)} -> {len(actual)} packaged glyphs; "
        f"{len(required_characters())} source CJK characters scanned."
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    args = parser.parse_args()
    if not FONT.exists():
        raise SystemExit(f"目标字体不存在：{FONT}")
    if not args.source.exists():
        raise SystemExit(f"源字体不存在：{args.source}")
    rebuild(args.source)


if __name__ == "__main__":
    main()
