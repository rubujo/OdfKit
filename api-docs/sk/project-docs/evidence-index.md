---
title: Register tvrdení o schopnostiach a dôkazov
_lang: sk
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
---

# Register tvrdení o schopnostiach a dôkazov

> Informatívny preklad; strojovo čitateľné identifikátory a hodnoty sa neprekladajú.

Tri rozmery sa navzájom neimplikujú. Strojovo čitateľným zdrojom je
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formát | Rozmer | Úroveň | Obmedzenie |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Obojsmerný zápis balíka neznamená prepočet vzorcov ani úplnú sémantiku tabuľky. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Číta uložené hodnoty a vzorce, ale vzorce neprepočítava. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Neposkytuje nástroj rozloženia strán ani vykresľovania. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | Spracovanie DOM/balíka; bez streamovacieho API snímok. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Bez rozloženia SmartArt a vykresľovania na úrovni pixelov. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Jedna testovaná verzia LibreOffice nezaručuje pixelovú zhodu vo všetkých balíkoch. |

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
