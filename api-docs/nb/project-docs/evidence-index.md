---
title: Indeks over kapabilitetspåstander og bevis
_lang: nb
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Indeks over kapabilitetspåstander og bevis

> Informativ oversettelse; maskinlesbare ID-er og verdier oversettes ikke.

De tre dimensjonene innebærer ikke hverandre. Maskinlesbar kilde er
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensjon | Nivå | Begrensning |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Pakkerundtur innebærer ikke formelberegning eller full regnearksemantikk. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Leser lagrede verdier og formler, men beregner ikke på nytt. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Ingen sidelayout- eller gjengivelsesmotor. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | DOM-/pakkearbeid; ingen strøm-API for lysbilder. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Ingen SmartArt-layout eller pikselgjengivelse. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Én testet LibreOffice-versjon garanterer ikke pikselidentitet i alle kontorpakker. |

`PackageFidelity` gjelder pakken, `SemanticApiDepth` dokumentsemantikk og `InteropEvidence` testede programmer
og versjoner. Ingen dimensjon erstatter en annen. Dekningskilden er
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), kontrollert
av `eng/Test-SemanticCoverage.ps1`.
