---
title: Limiti di sicurezza dei lettori streaming
_lang: it
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Limiti di sicurezza dei lettori streaming

> Traduzione informativa: in caso di divergenza prevale la fonte zh-TW.

`OdsStreamReader` e `OdtStreamReader` non creano il DOM completo, ma allocano buffer per riga corrente,
testo dei nodi, decompressione ZIP e lettore XML. Bassa memoria residente non significa indipendenza
dalla dimensione dell’input.

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
