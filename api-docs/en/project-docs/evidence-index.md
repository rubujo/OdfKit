---
title: Capability claims and evidence index
_lang: en
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
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

Semantic coverage schema v3 additionally requires every topic to have evidence for `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip`, and `Interop`, linked to specifications,
implementation, tests, limitations, and clean-room provenance. Every family must also have
machine-verified evidence for existing documents, unknown-content preservation, ODF 1.1–1.3,
downgrade diagnostics, and invalid input. See the [migration guide](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
and the [four-format semantic facade reference](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
