"""建立並驗證 WebFont 最小測試所需的 WOFF2 子集。"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont


TEST_CASES = (
    (0x9F98, 0, "BMP 罕用疊字", None),
    (0x1F200, 1, "SMP 方框假名符號", None),
    (0x201A9, 2, "SIP／CNS 第 3 字面", "3-216F"),
    (0x20086, 2, "SIP／CNS 第 4 字面", "4-2121"),
    (0x200D1, 2, "SIP／CNS 第 5 字面", "5-2121"),
    (0x201A4, 2, "SIP／CNS 第 6 字面", "6-2135"),
    (0x20F64, 2, "SIP／CNS 第 7 字面", "7-2155"),
    (0x2003E, 2, "SIP／CNS 第 10 字面", "10-2143"),
    (0x270AE, 2, "SIP／CNS 第 11 字面", "11-2121"),
    (0x205EB, 2, "SIP／CNS 第 12 字面", "12-5250"),
    (0x20630, 2, "SIP／CNS 第 15 字面", "15-212D"),
    (0x30EDD, 3, "TIP 擴充漢字 G", None),
    (0x3106C, 3, "TIP 擴充漢字 H", None),
)
CODE_POINTS = tuple(item[0] for item in TEST_CASES)


def sha256(path: Path) -> str:
    """計算檔案 SHA-256。"""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def unicode_cmap(font: TTFont) -> set[int]:
    """取得字型所有 Unicode cmap 的聯集。"""

    return {
        code_point
        for table in font["cmap"].tables
        if table.isUnicode()
        for code_point in table.cmap
    }


def main() -> None:
    """執行子集化並寫出測試中繼資料。"""

    parser = argparse.ArgumentParser()
    parser.add_argument("--font", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--metadata", required=True, type=Path)
    arguments = parser.parse_args()

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    source_font = TTFont(arguments.font, recalcTimestamp=False)
    missing = set(CODE_POINTS) - unicode_cmap(source_font)
    if missing:
        raise ValueError(f"來源字型缺少測試字元：{sorted(missing)}")

    options = subset.Options()
    options.flavor = "woff2"
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.name_languages = ["*"]

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=CODE_POINTS)
    subsetter.subset(source_font)
    source_font.flavor = "woff2"
    source_font.save(arguments.output)
    source_font.close()

    signature = arguments.output.read_bytes()[:4]
    if signature != b"wOF2":
        raise ValueError(f"輸出不是 WOFF2：{signature!r}")

    output_font = TTFont(arguments.output)
    missing_output = set(CODE_POINTS) - unicode_cmap(output_font)
    glyph_count = len(output_font.getGlyphOrder())
    output_font.close()
    if missing_output:
        raise ValueError(f"WOFF2 子集缺少測試字元：{sorted(missing_output)}")

    metadata = {
        "sourceFile": arguments.font.name,
        "sourceBytes": arguments.font.stat().st_size,
        "subsetBytes": arguments.output.stat().st_size,
        "sourceSha256": sha256(arguments.font),
        "subsetSha256": sha256(arguments.output),
        "signature": signature.decode("ascii"),
        "glyphCount": glyph_count,
        "codePoints": [f"U+{code_point:X}" for code_point in CODE_POINTS],
        "testCases": [
            {
                "codePoint": f"U+{code_point:X}",
                "text": chr(code_point),
                "unicodePlane": unicode_plane,
                "label": label,
                "cnsCode": cns_code,
            }
            for code_point, unicode_plane, label, cns_code in TEST_CASES
        ],
    }
    arguments.metadata.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
