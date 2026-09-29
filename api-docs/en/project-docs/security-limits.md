---
title: Loading and streaming reader security limits
_lang: en
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

All core XML readers must prohibit external DTDs and resolvers. New loading paths must reuse `OdfLoadOptions` or provide equivalent documented budgets. Package and Flat XML validation paths (`OdfPackageValidator`, `OdfFlatDocumentValidator`, and profile rule scans) also apply `MaxXmlCharactersInDocument`: package validation uses `package.LoadOptions`, while Flat validation uses `OdfValidationOptions.LoadOptions` (the `OdfLoadOptions` default of 64 MiB when omitted). Signatures, timestamps, certificate revocation data, and external network responses have their own smaller limits; the core package limit does not replace them. These loading limits are resource defenses, not document-content policy; use `OdfPackageValidator`, `SanitizeMacros`, signature validation, or `pwsh eng/Test-OdfPolicy.ps1` for policy enforcement.

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

## Other resource and output safeguards

Besides the package and streaming reader limits, the following fixed limits also defend against untrusted input. These values are currently fixed constants in code; they cannot yet be configured through `OdfLoadOptions` or an options object. Evaluate the impact on memory and stack before raising or removing a limit.

| Aspect | Limit | Behavior when exceeded |
|---|---|---|
| Actual decompressed size of a ZIP entry | Must not exceed the uncompressed size declared in the header (MMF path of file-path loading) | `SecurityException` |
| Damaged ZIP central directory or ZIP64 | The MMF fast path does not support ZIP64, and no record that cannot be fully parsed may be skipped silently | Falls back to `ZipArchive` validation and reading |
| XML element nesting depth | 256 levels (`OdfXmlReader.MaxElementDepth`); applies to DOM loading, Flat ODF loading, profile rule validation, and RDF parsing | Loading throws `SecurityException`; validation reports `ODF0303` or `ODF0301` |
| Formula parse nesting depth | 256 levels each for parentheses, function arguments, inline arrays, and consecutive prefix operators | `InvalidOperationException` |
| Total formula operator nodes | 4,096 (`FormulaParser.MaxOperatorNodes`); binary, unary, percent, and reference operators combined. Chained formulas form left-deep trees whose evaluation and serialization recurse level by level; this limit keeps ordinary thread stacks sufficient | `InvalidOperationException` |
| Formula recursion stack headroom | Parsing, evaluation, range retrieval, and serialization check the remaining stack (`RuntimeHelpers.EnsureSufficientExecutionStack`) before entering each recursion level; even formulas near the limits do not crash the process on small 128–256 KB stack threads | `InsufficientExecutionStackException`; `EvaluateFormulas` converts it into a formula evaluation exception or `#VALUE!` |
| Formula string result length | 1,048,576 characters (`&`, function results, `SUBSTITUTE`, `REPT`) | Returns `#VALUE!` |
| Formula functions whose loop bound is an argument | `BINOMDIST` cumulative 100,000; `CRITBINOM`, `HYPGEOMDIST` cumulative 100,000; `POISSON` cumulative 1,000,000; `DB`, `DDB`, `VDB`, `CUMIPMT` periods 1,000,000 | Returns `#NUM!` |
| Spreadsheet row/column index | Row 1,048,575, column 16,383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Document append | A document must not be appended to itself | `ArgumentException` |
| Collaboration `addColumns` | Bounded by the column count and total cell count of `OdtOperationSafetyOptions` | Logs the safety limit and skips the operation |
| Chart fallback image | 4,096 px per side | Clamped to the limit |
| LibreOffice conversion format | The extension part before the colon must not be empty and must not contain `..`, NUL, CR, or LF | `ArgumentException` |
| SPARQL query | `SERVICE` clauses are not allowed (prevents the query engine from sending network requests to arbitrary endpoints) | `ArgumentException` |

## Trust boundary

Keep the default limits for untrusted documents and perform package and schema validation first.
Individual limits may be raised for trusted documents that genuinely require it; increasing XML or
text limits also increases memory and CPU DoS risk. `MaxXmlCharactersInDocument = 0` disables only the
XML character limit; all other reader limits remain active.

ODS and ODT reader options validate the same rules when properties are assigned: the XML limit accepts zero but rejects negative values, while row, column, repeat, node, and text limits must all be greater than zero.

Security limits, validation, and sanitization reduce risk but do not guarantee absolute safety from
malicious documents.
