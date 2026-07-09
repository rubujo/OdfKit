#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Rewrite generic convenience-overload XML summaries with parameter-aware bilingual text.

Targets high-frequency public API files listed in HIGH_FREQ_FILES (relative to repo root).
Only rewrites the fixed template:

  Convenience overload that uses default values for remaining parameters.
  便利多載：其餘參數使用預設值並轉呼叫最長多載。

Handles methods, static methods, and constructors; distinguishes `=>` forwarders from full bodies.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]

HIGH_FREQ_FILES = [
    "OdfKit/Core/OdfDocumentFactory.cs",
    "OdfKit/Core/OdfPackage.cs",
    "OdfKit/Core/OdfDocument.cs",
    "OdfKit/Compliance/OdfValidator.cs",
    "OdfKit/Compliance/OdfExternalValidator.cs",
    "OdfKit/Spreadsheet/OdsStreamWriter.cs",
    "OdfKit/Spreadsheet/OdfTableSheet.RangeDepth.cs",
    "OdfKit/Spreadsheet/SpreadsheetDocument.RangeDepth.cs",
]

# Match summary + declaration (method or constructor). Declaration may span lines until ) => or ) { or ) : this
BLOCK_RE = re.compile(
    r"(?P<indent>[ \t]*)/// <summary>\n"
    r"(?P=indent)/// Convenience overload that uses default values for remaining parameters\.\n"
    r"(?P=indent)/// 便利多載：其餘參數使用預設值並轉呼叫最長多載。\n"
    r"(?P=indent)/// </summary>\n"
    r"(?P=indent)(?P<decl>"
    r"(?:public|protected)(?:\s+static)?(?:\s+async)?(?:\s+partial)?\s+"
    r"(?:"
    # constructor: TypeName(
    r"(?P<ctor>[A-Z]\w*)\s*\("
    r"|"
    # method: ReturnType Name(
    r"(?:[\w.]+(?:<[^>\n]+>)?\[?\]?\??\s+)+(?P<method>\w+)\s*(?:<[^>\n]+>)?\s*\("
    r")"
    r"(?P<params>[\s\S]*?)"
    r"\)\s*(?P<body>=>|:|{)"
    r")",
    re.MULTILINE,
)

PARAM_RE = re.compile(
    r"(?:ref\s+|in\s+|out\s+|params\s+)?"
    r"(?:[\w.]+(?:<[^>]+>)?\[?\]?\??\s+)+"
    r"(?P<name>\w+)\s*(?:=\s*[^,)]+)?",
)


def param_names(params: str) -> list[str]:
    names: list[str] = []
    for part in params.split(","):
        part = " ".join(part.split())  # collapse whitespace/newlines
        if not part:
            continue
        m = PARAM_RE.search(part)
        if m:
            names.append(m.group("name"))
    return names


def zh_join(names: list[str]) -> str:
    if not names:
        return ""
    if len(names) == 1:
        return names[0]
    if len(names) == 2:
        return f"{names[0]} 與 {names[1]}"
    return "、".join(names[:-1]) + f" 與 {names[-1]}"


def en_join(names: list[str]) -> str:
    if not names:
        return ""
    if len(names) == 1:
        return names[0]
    if len(names) == 2:
        return f"{names[0]} and {names[1]}"
    return ", ".join(names[:-1]) + f", and {names[-1]}"


def build_summary(indent: str, symbol: str, names: list[str], body_kind: str) -> str:
    is_forward = body_kind == "=>" or body_kind == ":"
    if not names:
        if is_forward:
            en = (
                f"Short overload of {symbol} that uses default values for all optional parameters "
                f"and forwards to the full overload."
            )
            zh = f"便利多載：{symbol} 的所有可選參數使用預設值並轉呼叫最長多載。"
        else:
            en = f"Creates or invokes {symbol} using default values for optional parameters."
            zh = f"以可選參數的預設值建立或呼叫 {symbol}。"
    else:
        en_params = en_join(names)
        zh_params = zh_join(names)
        if is_forward:
            en = (
                f"Short overload of {symbol} that accepts {en_params}; "
                f"remaining optional parameters use defaults and forward to the full overload."
            )
            zh = (
                f"便利多載：提供 {zh_params}；其餘可選參數使用預設值並轉呼叫最長 {symbol} 多載。"
            )
        else:
            en = f"Full overload of {symbol} that accepts {en_params}."
            zh = f"{symbol} 完整多載：接受 {zh_params}。"
    return (
        f"{indent}/// <summary>\n"
        f"{indent}/// {en}\n"
        f"{indent}/// {zh}\n"
        f"{indent}/// </summary>\n"
        f"{indent}"
    )


def rewrite_text(text: str) -> tuple[str, int]:
    count = 0

    def repl(m: re.Match[str]) -> str:
        nonlocal count
        indent = m.group("indent")
        symbol = m.group("ctor") or m.group("method")
        names = param_names(m.group("params"))
        body_kind = m.group("body")
        count += 1
        return build_summary(indent, symbol, names, body_kind) + m.group("decl")

    return BLOCK_RE.subn(repl, text)


def main() -> int:
    total = 0
    for rel in HIGH_FREQ_FILES:
        path = REPO / rel
        if not path.is_file():
            print(f"SKIP missing {rel}", file=sys.stderr)
            continue
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
        had_crlf = "\r\n" in text
        normalized = text.replace("\r\n", "\n")
        rewritten, n = rewrite_text(normalized)
        if n == 0:
            print(f"OK   0  {rel}")
            continue
        if had_crlf:
            rewritten = rewritten.replace("\n", "\r\n")
        bom = raw.startswith(b"\xef\xbb\xbf")
        out = rewritten.encode("utf-8")
        if bom:
            out = b"\xef\xbb\xbf" + out
        path.write_bytes(out)
        total += n
        print(f"WROTE {n:3d}  {rel}")
    print(f"TOTAL rewritten: {total}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
