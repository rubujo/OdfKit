---
title: Loading and streaming reader security limits
_lang: en
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Loading and streaming reader security limits

> Translation notice: this page is an English translation of the authoritative Traditional Chinese
> (Taiwan) document. If the texts differ, the authoritative source prevails.

Core package loading and `OdsStreamReader`/`OdtStreamReader` process untrusted ZIP/XML input. The readers do not build a complete document DOM, but they still allocate
buffers for the current row, node text, ZIP decompression, and the XML reader. A low-residency design
does not make resource use independent of input size.

## Core package limits

`OdfDocument.Load`, format-specific `Load` facades, and direct `OdfPackage.Open` calls share the `OdfLoadOptions` resource budgets.

| Limit | Default | Protection goal |
|---|---:|---|
| ZIP entries | 5,000 | Prevent CPU and memory exhaustion from many tiny entries |
| Uncompressed size of one entry | 500 MiB | Bound expansion of one ZIP entry |
| Total uncompressed package size | 1 GiB | Bound aggregate expansion across entries |
| Raw non-seekable input size | 1 GiB | Bound buffering before ZIP expansion |
| Characters in one XML document | 64 MiB | Bound XML parsing and DOM construction costs |

Entry count, entry size, total expansion, and raw package size must be positive. Zero or negative values immediately throw `ArgumentOutOfRangeException`. Only `MaxXmlCharactersInDocument = 0` disables the XML character limit; negative values remain invalid.

All core XML readers must prohibit external DTDs and resolvers. New loading paths must reuse `OdfLoadOptions` or provide equivalent documented budgets. These loading limits are resource defenses, not document-content policy; use `OdfPackageValidator`, `SanitizeMacros`, signature validation, or `pwsh eng/Test-OdfPolicy.ps1` for policy enforcement.

## Streaming reader limits

| Reader | Limit | Default |
|---|---|---:|
| ODS | XML characters | 64 MiB |
| ODS | Rows in one worksheet | 1,048,576 |
| ODS | Columns in one row | 16,384 |
| ODS | One repeat declaration | rows 1,048,576; columns 16,384 |
| ODS | Extracted text in one cell | 16 MiB |
| ODT | XML characters | 64 MiB |
| ODT | Returned text nodes | 1,000,000 |
| ODT | Extracted text in one node | 16 MiB |

Reading fails when a limit is exceeded. It does not truncate a repeat and continue with apparently
complete data. Treat such failures as resource-protection outcomes; do not automatically retry with
unlimited settings.

## Stream ownership

The `LeaveOpen` option defaults to `false`. When it is `true`, disposing the reader still closes its
XML entry stream and ZIP reader, but leaves the outermost caller-provided stream open.

## Trust boundary

Keep the default limits for untrusted documents and perform package and schema validation first.
Individual limits may be raised for trusted documents that genuinely require it; increasing XML or
text limits also increases memory and CPU DoS risk. `MaxXmlCharactersInDocument = 0` disables only the
XML character limit; all other reader limits remain active.

ODS and ODT reader options validate the same rules when properties are assigned: the XML limit accepts zero but rejects negative values, while row, column, repeat, node, and text limits must all be greater than zero.

Security limits, validation, and sanitization reduce risk but do not guarantee absolute safety from
malicious documents.
