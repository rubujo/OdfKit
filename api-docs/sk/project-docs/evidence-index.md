---
title: Register tvrdení o schopnostiach a dôkazov
_lang: sk
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
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
