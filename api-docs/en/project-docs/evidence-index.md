---
title: Capability claims and evidence index
_lang: en
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Capability claims and evidence index

> Translation notice: this page is an English translation of the authoritative Traditional Chinese
> (Taiwan) document. Claim identifiers and machine-readable values are not translated.

This index separates capability into three dimensions that do not imply one another. The
machine-readable source is [`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json);
CI validates claim IDs, evidence paths, and limitation descriptions.

| Claim | Format | Dimension | Level | Limitation summary |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Package round trips do not imply formula recalculation or complete spreadsheet semantics. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Reads stored values and formulas but does not recalculate formulas. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | No page-layout or rendering engine is provided. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP is a DOM/package workload; no streaming slide API is claimed. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | SmartArt layout and pixel-level rendering are not implemented. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Tests against a specific LibreOffice version do not guarantee pixel-identical output in every suite. |

`PackageFidelity` addresses safe package handling; `SemanticApiDepth` addresses how much document
meaning an API can understand and change; `InteropEvidence` records external applications and versions
that were tested. The highest level in one dimension cannot replace either of the other dimensions.

The single source of truth for semantic families, CRUD operations, specification sections,
implementation, tests, interoperability evidence, and limitations is
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json). CI
enforces it through `eng/Test-SemanticCoverage.ps1`. See
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md)
for clean-room source boundaries.
