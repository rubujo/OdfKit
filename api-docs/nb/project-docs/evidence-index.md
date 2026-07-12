---
title: Indeks over kapabilitetspåstander og bevis
_lang: nb
translation_source: docs/evidence-index.md
translation_source_sha256: 931370015f608c7efcf929a70e06c14f533aa58a4869d98b67f6a62debd20b9e
---

# Indeks over kapabilitetspåstander og bevis

> Informativ oversettelse; maskinlesbare ID-er og verdier oversettes ikke.

De tre dimensjonene innebærer ikke hverandre. Maskinlesbar kilde er
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensjon | Nivå | Begrensning |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | round-trip-verified | Pakkerundtur innebærer ikke formelberegning eller full regnearksemantikk. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-contract-verified | Leser lagrede verdier og formler, men beregner ikke på nytt. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-contract-verified | Ingen sidelayout- eller gjengivelsesmotor. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-contract-verified | DOM-/pakkearbeid; ingen strøm-API for lysbilder. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-contract-verified | Ingen SmartArt-layout eller pikselgjengivelse. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | interop-tested | Én testet LibreOffice-versjon garanterer ikke pikselidentitet i alle kontorpakker. |

`PackageFidelity` gjelder pakken, `SemanticApiDepth` dokumentsemantikk og `InteropEvidence` testede programmer
og versjoner. Ingen dimensjon erstatter en annen. Dekningskilden er
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), kontrollert
av `eng/Test-SemanticCoverage.ps1`.

Semantic coverage schema v4 krever i tillegg at hvert emne har bevis for `Create`, `Get`, `Find`,
`Set`, `Update`, `Remove`, `Clear`, `RoundTrip` og `Interop`, knyttet til spesifikasjoner,
implementasjon, tester, begrensninger og clean-room-proveniens. Hver familie må også ha
maskinverifiserte bevis for eksisterende dokumenter, bevaring av ukjent innhold, ODF 1.1–1.3,
nedgraderingsdiagnostikk og ugyldige inndata. Se [migreringsveiledningen](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
og [referansen for semantiske fasader i fire formater](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
