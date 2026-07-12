---
title: Indice delle dichiarazioni di capacità e delle prove
_lang: it
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
---

# Indice delle dichiarazioni di capacità e delle prove

> Traduzione informativa; identificatori e valori leggibili dalla macchina non vengono tradotti.

Le tre dimensioni non si implicano a vicenda. La fonte leggibile dalla macchina è
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formato | Dimensione | Livello | Limite |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Il round trip del pacchetto non implica ricalcolo delle formule o semantica completa. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Legge valori e formule salvati, ma non ricalcola. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Nessun motore di layout o rendering. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | Carico DOM/pacchetto; nessuna API streaming per diapositive. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Nessun layout SmartArt o rendering a livello di pixel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Una versione LibreOffice provata non garantisce uguaglianza pixel in ogni suite. |

`PackageFidelity` riguarda il pacchetto, `SemanticApiDepth` la semantica del documento e
`InteropEvidence` programmi e versioni provati; nessuna dimensione sostituisce le altre.
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json) è la fonte
unica della copertura, verificata da `eng/Test-SemanticCoverage.ps1`.

Lo schema v3 della copertura semantica richiede inoltre, per ogni argomento, prove relative a
`Create`, `Get`, `Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` e `Interop`, collegate a
specifiche, implementazione, test, limitazioni e provenienza clean-room. Ogni famiglia deve inoltre
disporre di prove verificate automaticamente per documenti esistenti, conservazione dei contenuti
sconosciuti, ODF 1.1–1.3, diagnostica del downgrade e input non validi. Vedere la [guida alla migrazione](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
e il [riferimento alle facade semantiche dei quattro formati](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
