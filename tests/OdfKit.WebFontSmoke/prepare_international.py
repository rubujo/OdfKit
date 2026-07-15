"""建立多國文字、IVS、PUA、TTC face 與多格式 WebFont 驗證資產。"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from dataclasses import dataclass
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont


@dataclass(frozen=True)
class SmokeCase:
    """描述一組會在瀏覽器中驗證的文字與來源字型。"""

    case_id: str
    title: str
    language: str
    direction: str
    font_family: str
    text: str
    source: Path
    face_index: int | None = None
    description: str = ""


def sha256(path: Path) -> str:
    """計算檔案 SHA-256。"""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def unicode_cmap(font: TTFont) -> set[int]:
    """取得一般 Unicode cmap 的碼位聯集，但排除 format 14 UVS 表。"""

    return {
        code_point
        for table in font["cmap"].tables
        if table.isUnicode() and table.format != 14
        for code_point in table.cmap
    }


def open_font(path: Path, face_index: int | None) -> TTFont:
    """以明確 face index 開啟單一字型或 collection。"""

    return TTFont(
        path,
        fontNumber=face_index if face_index is not None else -1,
        recalcTimestamp=False,
    )


def find_ivs_sequence(path: Path) -> tuple[int, int, str]:
    """從 IPAmj cmap format 14 選出可辨識的非預設 IVS。"""

    font = open_font(path, None)
    try:
        format14_tables = [table for table in font["cmap"].tables if table.format == 14]
        if len(format14_tables) != 1:
            raise ValueError("IPAmj 必須包含恰好一個 cmap format 14。")

        preferred_bases = (0x9089, 0x8FBB, 0x908A, 0x4FAE)
        variants = [
            (base, selector, glyph_name)
            for selector, entries in format14_tables[0].uvsDict.items()
            for base, glyph_name in entries
            if glyph_name is not None
        ]
        for preferred_base in preferred_bases:
            match = next(
                (variant for variant in variants if variant[0] == preferred_base),
                None,
            )
            if match is not None:
                return match

        if not variants:
            raise ValueError("IPAmj 未提供非預設 IVS glyph。")
        return variants[0]
    finally:
        font.close()


def signature_for(path: Path) -> str:
    """以可讀形式回傳 sfnt／WebFont signature。"""

    signature = path.read_bytes()[:4]
    return signature.decode("ascii") if signature != b"\x00\x01\x00\x00" else "00010000"


def subset_font(
    smoke_case: SmokeCase,
    output_path: Path,
    flavor: str | None,
) -> dict[str, object]:
    """保留完整 layout closure 並產生指定格式的字型子集。"""

    font = open_font(smoke_case.source, smoke_case.face_index)
    requested_code_points = {ord(character) for character in smoke_case.text}
    variation_selectors = {
        code_point
        for code_point in requested_code_points
        if 0xFE00 <= code_point <= 0xFE0F or 0xE0100 <= code_point <= 0xE01EF
    }
    regular_code_points = requested_code_points - variation_selectors
    missing = regular_code_points - unicode_cmap(font)
    if missing:
        font.close()
        raise ValueError(
            f"{smoke_case.case_id} 來源字型缺少碼位："
            f"{[f'U+{code_point:X}' for code_point in sorted(missing)]}"
        )

    source_tables = set(font.keys())
    options = subset.Options()
    options.flavor = flavor
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.name_languages = ["*"]

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=requested_code_points)
    subsetter.subset(font)
    font.flavor = flavor
    output_path.parent.mkdir(parents=True, exist_ok=True)
    font.save(output_path)
    font.close()

    output_font = TTFont(output_path)
    try:
        output_tables = set(output_font.keys())
        missing_output = regular_code_points - unicode_cmap(output_font)
        if missing_output:
            raise ValueError(
                f"{output_path.name} 缺少碼位："
                f"{[f'U+{code_point:X}' for code_point in sorted(missing_output)]}"
            )
        glyph_count = len(output_font.getGlyphOrder())
    finally:
        output_font.close()

    signature = signature_for(output_path)
    expected_signature = {
        "woff2": "wOF2",
        "woff": "wOFF",
        None: "OTTO" if "CFF " in source_tables or "CFF2" in source_tables else "00010000",
    }[flavor]
    if signature != expected_signature:
        raise ValueError(
            f"{output_path.name} signature 為 {signature}，預期 {expected_signature}。"
        )

    for layout_table in ("GSUB", "GPOS"):
        if layout_table in source_tables and layout_table not in output_tables:
            raise ValueError(f"{output_path.name} 遺失來源的 {layout_table} 表。")

    return {
        "fileName": output_path.name,
        "bytes": output_path.stat().st_size,
        "sha256": sha256(output_path),
        "signature": signature,
        "glyphCount": glyph_count,
        "hasGsub": "GSUB" in output_tables,
        "hasGpos": "GPOS" in output_tables,
    }


def assert_ivs_preserved(path: Path, base: int, selector: int) -> None:
    """確認子集的 cmap format 14 仍包含指定 IVS。"""

    font = TTFont(path)
    try:
        format14_tables = [table for table in font["cmap"].tables if table.format == 14]
        if len(format14_tables) != 1:
            raise ValueError(f"{path.name} 未保留 cmap format 14。")
        entries = format14_tables[0].uvsDict.get(selector, [])
        if not any(entry_base == base for entry_base, _ in entries):
            raise ValueError(
                f"{path.name} 未保留 U+{base:X} U+{selector:X} variation sequence。"
            )
    finally:
        font.close()


def write_manifest(
    output: Path,
    smoke_cases: list[SmokeCase],
    case_outputs: dict[str, list[dict[str, object]]],
    ivs: tuple[int, int, str],
) -> None:
    """寫出供 ASP.NET Core 展示站使用的中性 manifest。"""

    base, selector, glyph_name = ivs
    manifest = {
        "schemaVersion": 1,
        "cases": [
            {
                "id": smoke_case.case_id,
                "title": smoke_case.title,
                "language": smoke_case.language,
                "direction": smoke_case.direction,
                "fontFamily": smoke_case.font_family,
                "text": smoke_case.text,
                "codePoints": [f"U+{ord(character):X}" for character in smoke_case.text],
                "description": smoke_case.description,
                "sourceFile": smoke_case.source.name,
                "sourceBytes": smoke_case.source.stat().st_size,
                "sourceSha256": sha256(smoke_case.source),
                "faceIndex": smoke_case.face_index,
                "outputs": case_outputs[smoke_case.case_id],
                "ivsBaseText": chr(base) if smoke_case.case_id == "japan-ivs" else None,
                "ivsGlyphName": glyph_name if smoke_case.case_id == "japan-ivs" else None,
            }
            for smoke_case in smoke_cases
        ],
    }
    (output / "international.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    format_by_signature = {
        "wOF2": "Woff2",
        "wOFF": "Woff",
        "00010000": "TrueType",
        "OTTO": "OpenType",
    }
    hosting_manifest = {
        "schemaVersion": 1,
        "profileId": "international-smoke-v1",
        "assets": [
            {
                "fileName": asset["fileName"],
                "sha256": asset["sha256"],
                "byteLength": asset["bytes"],
                "format": format_by_signature[str(asset["signature"])],
                "fontFamily": smoke_case.font_family,
                "unicodeRanges": list(
                    dict.fromkeys(f"U+{ord(character):X}" for character in smoke_case.text)
                ),
            }
            for smoke_case in smoke_cases
            for asset in case_outputs[smoke_case.case_id]
        ],
    }
    for asset in hosting_manifest["assets"]:
        source_path = output / str(asset["fileName"])
        hash_directory = output / str(asset["sha256"])
        hash_directory.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, hash_directory / source_path.name)

    (output / "webfonts.json").write_text(
        json.dumps(hosting_manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    """建立所有國際化 smoke-test 資產並執行二進位驗證。"""

    parser = argparse.ArgumentParser()
    parser.add_argument("--arabic", required=True, type=Path)
    parser.add_argument("--devanagari", required=True, type=Path)
    parser.add_argument("--cjk-collection", required=True, type=Path)
    parser.add_argument("--cjk-opentype", required=True, type=Path)
    parser.add_argument("--ipamj", required=True, type=Path)
    parser.add_argument("--cns-pua", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    arguments = parser.parse_args()

    ivs_base, ivs_selector, ivs_glyph = find_ivs_sequence(arguments.ipamj)
    smoke_cases = [
        SmokeCase(
            "arabic",
            "阿拉伯文連寫與附加符號",
            "ar",
            "rtl",
            "OdfKit Arabic",
            "السَّلَامُ عَلَيْكُمْ",
            arguments.arabic,
            description="驗證 RTL、GSUB／GPOS 與 combining marks。",
        ),
        SmokeCase(
            "devanagari",
            "印度天城文 conjunct",
            "hi",
            "ltr",
            "OdfKit Devanagari",
            "क्षेत्रज्ञ भारत",
            arguments.devanagari,
            description="驗證 virama、conjunct 與 reordering 所需 layout closure。",
        ),
        SmokeCase(
            "cjk-hk-ttc",
            "香港／東亞 TTC face",
            "zh-HK",
            "ltr",
            "OdfKit CJK HK",
            "香港邨裏𠮷",
            arguments.cjk_collection,
            face_index=4,
            description="從十個 face 的 Noto CJK TTC 明確抽取香港 face。",
        ),
        SmokeCase(
            "cjk-hk-cff",
            "香港／東亞 OpenType CFF",
            "zh-HK",
            "ltr",
            "OdfKit CJK HK CFF",
            "香港邨裏𠮷",
            arguments.cjk_opentype,
            description="驗證 OpenType CFF 輸入與獨立 OTF 輸出。",
        ),
        SmokeCase(
            "japan-ivs",
            "日本 Moji_Joho IVS",
            "ja",
            "ltr",
            "OdfKit IPAmj",
            chr(ivs_base) + chr(ivs_selector),
            arguments.ipamj,
            description=(
                f"保留 cmap format 14：U+{ivs_base:X} U+{ivs_selector:X} → {ivs_glyph}。"
            ),
        ),
        SmokeCase(
            "cns-pua",
            "全字庫 Plane 15 自造字",
            "zh-Hant-TW",
            "ltr",
            "OdfKit CNS PUA",
            "".join(chr(code_point) for code_point in (0xF0000, 0xF0587, 0xFFE39)),
            arguments.cns_pua,
            description="驗證非 BMP PUA，語意由 CNS profile／mapping 版本決定。",
        ),
    ]

    arguments.output.mkdir(parents=True, exist_ok=True)
    case_outputs: dict[str, list[dict[str, object]]] = {}
    for smoke_case in smoke_cases:
        formats = [("woff2", ".woff2")]
        if smoke_case.case_id == "arabic":
            formats.extend((("woff", ".woff"), (None, ".ttf")))
        elif smoke_case.case_id == "cjk-hk-ttc":
            formats.append((None, ".ttf"))
        elif smoke_case.case_id == "cjk-hk-cff":
            formats.extend((("woff", ".woff"), (None, ".otf")))

        outputs: list[dict[str, object]] = []
        for flavor, extension in formats:
            output_path = arguments.output / f"{smoke_case.case_id}{extension}"
            outputs.append(subset_font(smoke_case, output_path, flavor))
        case_outputs[smoke_case.case_id] = outputs

    ivs_output = arguments.output / "japan-ivs.woff2"
    assert_ivs_preserved(ivs_output, ivs_base, ivs_selector)

    reproducible_path = arguments.output / "arabic-repro.woff2"
    reproducible = subset_font(smoke_cases[0], reproducible_path, "woff2")
    if reproducible["sha256"] != case_outputs["arabic"][0]["sha256"]:
        raise ValueError("相同 Arabic 輸入未產生位元組相同的 WOFF2。")
    reproducible_path.unlink()

    write_manifest(arguments.output, smoke_cases, case_outputs, (ivs_base, ivs_selector, ivs_glyph))
    print(
        json.dumps(
            {
                "status": "PASS",
                "cases": len(smoke_cases),
                "assets": sum(len(outputs) for outputs in case_outputs.values()),
                "ivs": f"U+{ivs_base:X} U+{ivs_selector:X}",
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
