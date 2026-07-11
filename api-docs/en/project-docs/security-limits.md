---
title: Streaming reader security limits
_lang: en
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Streaming reader security limits

> Translation notice: this page is an English translation of the authoritative Traditional Chinese
> (Taiwan) document. If the texts differ, the authoritative source prevails.

`OdsStreamReader` and `OdtStreamReader` do not build a complete document DOM, but they still allocate
buffers for the current row, node text, ZIP decompression, and the XML reader. A low-residency design
does not make resource use independent of input size.

## Default limits

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

Security limits, validation, and sanitization reduce risk but do not guarantee absolute safety from
malicious documents.
