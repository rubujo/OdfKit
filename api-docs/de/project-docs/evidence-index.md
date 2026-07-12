---
title: Index der Fähigkeitsaussagen und Nachweise
_lang: de
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
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

Das Semantic-Coverage-Schema v3 verlangt zusätzlich für jedes Thema Nachweise zu `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` und `Interop`, verknüpft mit Spezifikationen,
Implementierung, Tests, Einschränkungen und Clean-Room-Provenienz. Jede Familie benötigt außerdem
maschinengeprüfte Nachweise für bestehende Dokumente, den Erhalt unbekannter Inhalte, ODF 1.1–1.3,
Downgrade-Diagnosen und ungültige Eingaben. Siehe [Migrationsleitfaden](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
und [Referenz der semantischen Fassaden für vier Formate](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
