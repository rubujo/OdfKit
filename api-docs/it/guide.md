---
title: Guida a utilizzo, conformità, sicurezza e prove
_lang: it
---

# Utilizzo, conformità, sicurezza e prove

## Ambito della documentazione API

Il riferimento API viene generato dagli assembly pubblici `net10.0` e dalla documentazione XML. Le API principali scritte a mano e le estensioni pubbliche sono presentate in pagine dedicate. L'ampia superficie `OdfKit.DOM` generata dagli schemi resta controllata dalle baseline Public API per entrambi i TFM e dalla copertura Typed DOM. I riepiloghi dei membri sono attualmente disponibili in inglese e cinese tradizionale; questa voce italiana non dichiara tradotti tutti i membri API.

## Licenza e produzione tramite IA

Il codice originale OdfKit e la documentazione originale del sito adottano CC0-1.0 Universal. Pacchetti, schemi, strumenti e fixture di terze parti conservano le proprie licenze. I contenuti pubblici del progetto sono scritti, organizzati o prodotti con strumenti di IA. Il sito non costituisce consulenza legale e non offre SLA o indennizzi commerciali. OdfKit non è un progetto ufficiale né approvato da OASIS, The Document Foundation, LibreOffice o Apache.

## Limiti di sicurezza e interoperabilità

Per file non attendibili, mantenere attivi i limiti di risorse di reader e package ed eseguire la validazione o la sanitizzazione appropriata. Questi controlli riducono il rischio, ma non garantiscono sicurezza assoluta contro documenti dannosi. La validità dello schema, il round-trip o i test con una versione di LibreOffice non implicano risultati identici al pixel in ogni suite per ufficio.

## Capacità e prove

Le dichiarazioni sono suddivise in `PackageFidelity`, `SemanticApiDepth` e `InteropEvidence`; una dimensione non dimostra le altre. I risultati prestazionali pubblicati devono indicare commit, runtime, ambiente e metodo riproducibile. I budget prestazionali sono ancora nella fase di raccolta.

- [Apri il riferimento API](xref:OdfKit)
- [Indice delle dichiarazioni e prove](../../docs/evidence-index.md)
- [Limiti di sicurezza](../../docs/security-limits.md)
- [Proprietà intellettuale e conformità](../../docs/ip-compliance.md)
- [Licenza](../articles/license.md)
- [Avvisi di terze parti](../../THIRD-PARTY-NOTICES.md)
