#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Rewrite placeholder 'Executes the X operation' bilingual summaries into method-aware text.

Scans hand-written C# under OdfKit / OdfKit.Extensions.* (excludes Generated/bin/obj).
Rewrites only the English line when Chinese is already domain-specific; rewrites both when
Chinese is still the generic「執行 X 作業」form.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]

BLOCK_RE = re.compile(
    r"(?P<indent>[ \t]*)/// <summary>\n"
    r"(?P=indent)/// Executes the (?P<name>\w+) operation\.\n"
    r"(?P=indent)/// (?P<zh>[^\n]+)\n"
    r"(?P=indent)/// </summary>",
    re.MULTILINE,
)

GENERIC_ZH_RE = re.compile(r"^執行\s+(?P<name>\w+)\s+作業。$")

# Common leading verbs → (english_phrase_template, chinese_phrase_template)
# {rest} is lowercased remaining words joined by spaces / 中文常保留原文片段。
VERB_MAP: list[tuple[str, str, str]] = [
    ("TryGet", "Tries to get {rest}.", "嘗試取得 {rest_zh}。"),
    ("TryParse", "Tries to parse {rest}.", "嘗試剖析 {rest_zh}。"),
    ("TryRead", "Tries to read {rest}.", "嘗試讀取 {rest_zh}。"),
    ("TryWrite", "Tries to write {rest}.", "嘗試寫入 {rest_zh}。"),
    ("TryFind", "Tries to find {rest}.", "嘗試尋找 {rest_zh}。"),
    ("TryAdd", "Tries to add {rest}.", "嘗試加入 {rest_zh}。"),
    ("TryRemove", "Tries to remove {rest}.", "嘗試移除 {rest_zh}。"),
    ("TryCreate", "Tries to create {rest}.", "嘗試建立 {rest_zh}。"),
    ("TryOpen", "Tries to open {rest}.", "嘗試開啟 {rest_zh}。"),
    ("TryLoad", "Tries to load {rest}.", "嘗試載入 {rest_zh}。"),
    ("TrySave", "Tries to save {rest}.", "嘗試儲存 {rest_zh}。"),
    ("TryConvert", "Tries to convert {rest}.", "嘗試轉換 {rest_zh}。"),
    ("Get", "Gets the {rest}.", "取得 {rest_zh}。"),
    ("Set", "Sets the {rest}.", "設定 {rest_zh}。"),
    ("Is", "Returns whether this instance is {rest}.", "傳回此執行個體是否為 {rest_zh}。"),
    ("Has", "Returns whether {rest} is present.", "傳回是否具有 {rest_zh}。"),
    ("Can", "Returns whether {rest} is allowed.", "傳回是否可進行 {rest_zh}。"),
    ("Create", "Creates {rest}.", "建立 {rest_zh}。"),
    ("Load", "Loads {rest}.", "載入 {rest_zh}。"),
    ("Save", "Saves {rest}.", "儲存 {rest_zh}。"),
    ("Open", "Opens {rest}.", "開啟 {rest_zh}。"),
    ("Close", "Closes {rest}.", "關閉 {rest_zh}。"),
    ("Read", "Reads {rest}.", "讀取 {rest_zh}。"),
    ("Write", "Writes {rest}.", "寫入 {rest_zh}。"),
    ("Import", "Imports {rest}.", "匯入 {rest_zh}。"),
    ("Export", "Exports {rest}.", "匯出 {rest_zh}。"),
    ("Validate", "Validates {rest}.", "驗證 {rest_zh}。"),
    ("Convert", "Converts {rest}.", "轉換 {rest_zh}。"),
    ("Parse", "Parses {rest}.", "剖析 {rest_zh}。"),
    ("Format", "Formats {rest}.", "格式化 {rest_zh}。"),
    ("Find", "Finds {rest}.", "尋找 {rest_zh}。"),
    ("Search", "Searches {rest}.", "搜尋 {rest_zh}。"),
    ("Add", "Adds {rest}.", "加入 {rest_zh}。"),
    ("Remove", "Removes {rest}.", "移除 {rest_zh}。"),
    ("Delete", "Deletes {rest}.", "刪除 {rest_zh}。"),
    ("Clear", "Clears {rest}.", "清除 {rest_zh}。"),
    ("Insert", "Inserts {rest}.", "插入 {rest_zh}。"),
    ("Update", "Updates {rest}.", "更新 {rest_zh}。"),
    ("Replace", "Replaces {rest}.", "取代 {rest_zh}。"),
    ("Append", "Appends {rest}.", "附加 {rest_zh}。"),
    ("Merge", "Merges {rest}.", "合併 {rest_zh}。"),
    ("Copy", "Copies {rest}.", "複製 {rest_zh}。"),
    ("Clone", "Clones {rest}.", "複製 {rest_zh}。"),
    ("Build", "Builds {rest}.", "建置 {rest_zh}。"),
    ("Render", "Renders {rest}.", "轉譯 {rest_zh}。"),
    ("Draw", "Draws {rest}.", "繪製 {rest_zh}。"),
    ("Compute", "Computes {rest}.", "計算 {rest_zh}。"),
    ("Calculate", "Calculates {rest}.", "計算 {rest_zh}。"),
    ("Ensure", "Ensures {rest}.", "確保 {rest_zh}。"),
    ("Apply", "Applies {rest}.", "套用 {rest_zh}。"),
    ("Reset", "Resets {rest}.", "重設 {rest_zh}。"),
    ("Init", "Initializes {rest}.", "初始化 {rest_zh}。"),
    ("Initialize", "Initializes {rest}.", "初始化 {rest_zh}。"),
    ("Register", "Registers {rest}.", "註冊 {rest_zh}。"),
    ("Resolve", "Resolves {rest}.", "解析 {rest_zh}。"),
    ("Detect", "Detects {rest}.", "偵測 {rest_zh}。"),
    ("Collect", "Collects {rest}.", "收集 {rest_zh}。"),
    ("Enumerate", "Enumerates {rest}.", "列舉 {rest_zh}。"),
    ("Visit", "Visits {rest}.", "巡訪 {rest_zh}。"),
    ("Sign", "Signs {rest}.", "簽署 {rest_zh}。"),
    ("Verify", "Verifies {rest}.", "驗證 {rest_zh}。"),
    ("Encrypt", "Encrypts {rest}.", "加密 {rest_zh}。"),
    ("Decrypt", "Decrypts {rest}.", "解密 {rest_zh}。"),
    ("Compress", "Compresses {rest}.", "壓縮 {rest_zh}。"),
    ("Extract", "Extracts {rest}.", "解出 {rest_zh}。"),
    ("Bind", "Binds {rest}.", "繫結 {rest_zh}。"),
    ("Map", "Maps {rest}.", "對應 {rest_zh}。"),
    ("To", "Converts to {rest}.", "轉換為 {rest_zh}。"),
    ("From", "Creates from {rest}.", "自 {rest_zh} 建立。"),
    ("Dispose", "Releases resources for {rest}.", "釋放 {rest_zh} 資源。"),
    ("Pin", "Pins {rest}.", "釘住 {rest_zh}。"),
    ("Unpin", "Unpins {rest}.", "取消釘住 {rest_zh}。"),
]


def split_camel(name: str) -> list[str]:
    # FooBarBAZ -> Foo, Bar, BAZ; IOStream -> IO, Stream
    parts = re.findall(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", name)
    return parts if parts else [name]


def en_rest(words: list[str]) -> str:
    if not words:
        return "the operation"
    lowered = [w if w.isupper() and len(w) <= 3 else w.lower() for w in words]
    # Keep acronyms (ODF, XML, PDF) upper when short all-caps
    fixed = []
    for w, raw in zip(lowered, words):
        if raw.isupper() and len(raw) <= 4:
            fixed.append(raw)
        else:
            fixed.append(w)
    text = " ".join(fixed)
    # a/an not critical for IntelliSense stubs
    return text


def zh_rest(words: list[str]) -> str:
    if not words:
        return "作業"
    # Keep identifier fragments with spaces for 盤古之白 against CJK verbs around them
    return " ".join(words)


def summarize(name: str) -> tuple[str, str]:
    for prefix, en_t, zh_t in VERB_MAP:
        if name.startswith(prefix) and len(name) > len(prefix):
            rest_words = split_camel(name[len(prefix) :])
            en = en_t.format(rest=en_rest(rest_words))
            zh = zh_t.format(rest_zh=zh_rest(rest_words))
            return en, zh
        if name == prefix:
            # rare: method name is just the verb
            if prefix in ("Dispose",):
                return "Releases unmanaged resources.", "釋放非受控資源。"
            return f"Performs the {prefix} operation.", f"執行 {prefix} 作業。"

    words = split_camel(name)
    en = f"Performs {en_rest(words)}."
    zh = f"執行 {zh_rest(words)}。"
    return en, zh


def rewrite_text(text: str) -> tuple[str, int]:
    count = 0

    def repl(m: re.Match[str]) -> str:
        nonlocal count
        indent = m.group("indent")
        name = m.group("name")
        zh_old = m.group("zh").strip()
        en_new, zh_generated = summarize(name)

        # Keep domain Chinese when already better than generic stub
        gen_zh = GENERIC_ZH_RE.match(zh_old)
        if gen_zh and gen_zh.group("name") == name:
            zh_new = zh_generated
        elif zh_old.startswith("執行 ") and zh_old.endswith(" 作業。"):
            zh_new = zh_generated
        else:
            zh_new = zh_old

        count += 1
        return (
            f"{indent}/// <summary>\n"
            f"{indent}/// {en_new}\n"
            f"{indent}/// {zh_new}\n"
            f"{indent}/// </summary>"
        )

    return BLOCK_RE.subn(repl, text)


def iter_target_files() -> list[Path]:
    roots = [REPO / "OdfKit"]
    roots.extend(sorted(REPO.glob("OdfKit.Extensions.*")))
    files: list[Path] = []
    for root in roots:
        if not root.is_dir():
            continue
        for path in root.rglob("*.cs"):
            s = str(path).replace("\\", "/")
            if "/Generated/" in s or "/bin/" in s or "/obj/" in s:
                continue
            files.append(path)
    return files


def main() -> int:
    total = 0
    for path in iter_target_files():
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
        had_crlf = "\r\n" in text
        normalized = text.replace("\r\n", "\n")
        rewritten, n = rewrite_text(normalized)
        if n == 0:
            continue
        if had_crlf:
            rewritten = rewritten.replace("\n", "\r\n")
        bom = raw.startswith(b"\xef\xbb\xbf")
        out = rewritten.encode("utf-8")
        if bom:
            out = b"\xef\xbb\xbf" + out
        path.write_bytes(out)
        total += n
        print(f"WROTE {n:4d}  {path.relative_to(REPO).as_posix()}")
    print(f"TOTAL rewritten: {total}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
