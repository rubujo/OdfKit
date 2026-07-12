---
title: Register tvrdení o schopnostiach a dôkazov
_lang: sk
translation_source: docs/evidence-index.md
translation_source_sha256: 931370015f608c7efcf929a70e06c14f533aa58a4869d98b67f6a62debd20b9e
---

# Register tvrdení o schopnostiach a dôkazov

> Informatívny preklad; strojovo čitateľné identifikátory a hodnoty sa neprekladajú.

Tri rozmery sa navzájom neimplikujú. Strojovo čitateľným zdrojom je
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formát | Rozmer | Úroveň | Obmedzenie |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | round-trip-verified | Obojsmerný zápis balíka neznamená prepočet vzorcov ani úplnú sémantiku tabuľky. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-contract-verified | Číta uložené hodnoty a vzorce, ale vzorce neprepočítava. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-contract-verified | Neposkytuje nástroj rozloženia strán ani vykresľovania. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-contract-verified | Spracovanie DOM/balíka; bez streamovacieho API snímok. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-contract-verified | Bez rozloženia SmartArt a vykresľovania na úrovni pixelov. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | interop-tested | Jedna testovaná verzia LibreOffice nezaručuje pixelovú zhodu vo všetkých balíkoch. |

`PackageFidelity` opisuje balík, `SemanticApiDepth` sémantiku dokumentu a `InteropEvidence` testované
programy a verzie. Žiadny rozmer nenahrádza iný. Jediným zdrojom pokrytia je
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), ktorý v CI
overuje `eng/Test-SemanticCoverage.ps1`.

Schéma sémantického pokrytia v3 navyše vyžaduje, aby každá téma mala dôkazy pre `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` a `Interop`, prepojené so špecifikáciami,
implementáciou, testami, obmedzeniami a pôvodom clean-room. Každá skupina musí mať aj strojovo
overené dôkazy pre existujúce dokumenty, zachovanie neznámeho obsahu, ODF 1.1–1.3, diagnostiku
zníženia verzie a neplatné vstupy. Pozrite si [migračnú príručku](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
a [referenciu sémantických fasád štyroch formátov](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
