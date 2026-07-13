---
title: Limiti di sicurezza del caricamento e dei lettori streaming
_lang: it
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Limiti di sicurezza del caricamento e dei lettori streaming

> Traduzione informativa: in caso di divergenza prevale la fonte zh-TW.

Il caricamento dei pacchetti e `OdsStreamReader`/`OdtStreamReader` elaborano input ZIP/XML non attendibili. I lettori non creano il DOM completo, ma allocano buffer per riga corrente,
testo dei nodi, decompressione ZIP e lettore XML. Bassa memoria residente non significa indipendenza
dalla dimensione dell’input.

## Limiti del pacchetto principale

`OdfDocument.Load`, le facade `Load` e `OdfPackage.Open` condividono i budget di `OdfLoadOptions`.

| Limite | Predefinito | Protezione |
|---|---:|---|
| Voci ZIP | 5,000 | Evita l’esaurimento di CPU e memoria con molte voci piccole |
| Dimensione decompressa di una voce | 500 MiB | Limita l’espansione di una voce ZIP |
| Dimensione decompressa totale | 1 GiB | Limita l’espansione complessiva del pacchetto |
| Input grezzo non ricercabile | 1 GiB | Limita il buffering prima dell’espansione ZIP |
| Caratteri in un documento XML | 64 MiB | Limita l’analisi XML e la costruzione del DOM |

I quattro limiti ZIP devono essere positivi; zero o valori negativi generano subito `ArgumentOutOfRangeException`. Solo `MaxXmlCharactersInDocument = 0` disattiva il limite XML. Tutti i lettori XML devono vietare DTD e resolver esterni. I nuovi percorsi devono riutilizzare `OdfLoadOptions`; per le regole sul contenuto usare `OdfPackageValidator`, `SanitizeMacros`, la verifica delle firme o `pwsh eng/Test-OdfPolicy.ps1`.

## Limiti dei lettori streaming

| Lettore | Limite | Predefinito |
|---|---|---:|
| ODS | Caratteri XML | 64 MiB |
| ODS | Righe per foglio | 1,048,576 |
| ODS | Colonne per riga | 16,384 |
| ODS | Una dichiarazione repeat | righe 1,048,576; colonne 16,384 |
| ODS | Testo di una cella | 16 MiB |
| ODT | Caratteri XML | 64 MiB |
| ODT | Nodi di testo restituiti | 1,000,000 |
| ODT | Testo di un nodo | 16 MiB |

Il superamento di un limite causa un errore: repeat non viene troncato restituendo dati apparentemente
completi. Non riprovare automaticamente senza limiti. `LeaveOpen` è `false` per impostazione predefinita;
con `true` vengono chiusi stream XML e lettore ZIP, ma resta aperto lo stream esterno del chiamante.

Per documenti non attendibili mantenere i limiti e validare pacchetto e schema. Limiti XML o testuali più
alti aumentano i rischi di memoria e CPU DoS. `MaxXmlCharactersInDocument = 0` disattiva solo il limite dei
caratteri XML. Limiti, convalida e sanificazione riducono il rischio senza garantire sicurezza assoluta.

Le opzioni dei lettori ODS e ODT convalidano le regole all’assegnazione: il limite XML accetta zero, mentre i limiti di righe, colonne, repeat, nodi e testo devono essere maggiori di zero.
