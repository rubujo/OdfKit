---
title: Indeks over kapacitetspåstande og evidens
_lang: da
translation_source: docs/evidence-index.md
translation_source_sha256: 931370015f608c7efcf929a70e06c14f533aa58a4869d98b67f6a62debd20b9e
---

# Indeks over kapacitetspåstande og evidens

> Informativ oversættelse; maskinlæsbare ID-er og værdier oversættes ikke.

De tre dimensioner indebærer ikke hinanden. Maskinlæsbar kilde er
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensjon | Nivå | Begrænsning |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | round-trip-verified | Pakkeroundtrip indebærer ikke formelgenberegning eller fuld regnearkssemantik. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-contract-verified | Læser gemte værdier og formler, men genberegner ikke igen. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-contract-verified | Ingen sidelayout- eller renderingsmotor. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-contract-verified | DOM-/pakkearbejde; ingen streaming-API for dias. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-contract-verified | Ingen SmartArt-layout eller pixelrendering. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | interop-tested | Én testet LibreOffice-version garanterer ikke pixelidentitet i alle kontorpakker. |

`PackageFidelity` gælder pakken, `SemanticApiDepth` dokumentsemantik og `InteropEvidence` testede programmer
og versioner. Ingen dimension erstatter en anden. Dekningskilden er
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), kontrolleret
af `eng/Test-SemanticCoverage.ps1`.

Semantic coverage schema v4 kræver desuden, at hvert emne har evidens for `Create`, `Get`, `Find`,
`Set`, `Update`, `Remove`, `Clear`, `RoundTrip` og `Interop`, knyttet til specifikationer,
implementering, test, begrænsninger og clean-room-proveniens. Hver familie skal også have
maskinverificeret evidens for eksisterende dokumenter, bevarelse af ukendt indhold, ODF 1.1–1.3,
nedgraderingsdiagnostik og ugyldigt input. Se [migreringsvejledningen](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
og [referencen til de fire formaters semantiske facader](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
