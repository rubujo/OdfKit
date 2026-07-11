---
title: Index van capaciteitsclaims en bewijs
_lang: nl
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Index van capaciteitsclaims en bewijs

> Informatieve vertaling; machineleesbare identifiers en waarden worden niet vertaald.

De drie dimensies impliceren elkaar niet. De machineleesbare bron is
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formaat | Dimensie | Niveau | Beperking |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Een package-roundtrip impliceert geen formuleherberekening of volledige spreadsheetsemantiek. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Leest opgeslagen waarden en formules, maar berekent niet opnieuw. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Geen pagina-indelings- of renderingengine. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | DOM/package-workload; geen streaming dia-API. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Geen SmartArt-indeling of pixelrendering. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Eén geteste LibreOffice-versie garandeert geen pixelgelijkheid in elke suite. |

`PackageFidelity` betreft pakketverwerking, `SemanticApiDepth` documentsemantiek en `InteropEvidence`
geteste programma’s en versies. Geen dimensie vervangt een andere. De enige bron voor dekking is
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), door
`eng/Test-SemanticCoverage.ps1` in CI gecontroleerd.
