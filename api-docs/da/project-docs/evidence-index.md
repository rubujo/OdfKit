---
title: Indeks over kapacitetspåstande og evidens
_lang: da
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Indeks over kapacitetspåstande og evidens

> Informativ oversættelse; maskinlæsbare ID-er og værdier oversættes ikke.

De tre dimensioner indebærer ikke hinanden. Maskinlæsbar kilde er
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensjon | Nivå | Begrænsning |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Pakkeroundtrip indebærer ikke formelgenberegning eller fuld regnearkssemantik. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Læser gemte værdier og formler, men genberegner ikke igen. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Ingen sidelayout- eller renderingsmotor. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | DOM-/pakkearbejde; ingen streaming-API for dias. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Ingen SmartArt-layout eller pixelrendering. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Én testet LibreOffice-version garanterer ikke pixelidentitet i alle kontorpakker. |

`PackageFidelity` gælder pakken, `SemanticApiDepth` dokumentsemantik og `InteropEvidence` testede programmer
og versioner. Ingen dimension erstatter en anden. Dekningskilden er
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), kontrolleret
af `eng/Test-SemanticCoverage.ps1`.
