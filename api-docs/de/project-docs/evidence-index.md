---
title: Index der Fähigkeitsaussagen und Nachweise
_lang: de
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Index der Fähigkeitsaussagen und Nachweise

> Diese Übersetzung dient nur zur Information. Claim-IDs und maschinenlesbare Werte bleiben unverändert.

Der Index trennt drei Dimensionen, die einander nicht implizieren. Maschinenlesbare Quelle ist
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimension | Stufe | Einschränkung |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Paket-Roundtrips bedeuten weder Formelneuberechnung noch vollständige Tabellenkalkulationssemantik. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Gespeicherte Werte und Formeln werden gelesen, Formeln aber nicht neu berechnet. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Keine Seitenlayout- oder Rendering-Engine. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | DOM-/Paketverarbeitung; keine Streaming-Folien-API. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Kein SmartArt-Layout oder pixelgenaues Rendering. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Ein getestetes LibreOffice garantiert keine Pixelgleichheit in jeder Office-Suite. |

`PackageFidelity` betrifft sichere Paketverarbeitung, `SemanticApiDepth` das Verstehen und Ändern von
Dokumentsemantik und `InteropEvidence` getestete externe Programme und Versionen. Keine Dimension ersetzt
eine andere. [`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json)
ist die maßgebliche Quelle für semantische Abdeckung; `eng/Test-SemanticCoverage.ps1` prüft sie in CI.
