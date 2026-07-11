---
title: Beveiligingslimieten voor streaming readers
_lang: nl
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Beveiligingslimieten voor streaming readers

> Informatieve vertaling; bij verschillen geldt de gezaghebbende zh-TW-bron.

`OdsStreamReader` en `OdtStreamReader` bouwen geen volledig DOM, maar reserveren buffers voor de huidige
rij, knooptekst, ZIP-decompressie en XML-reader. Laag resident geheugen maakt invoergrootte niet irrelevant.

| Reader | Limiet | Standaard |
|---|---|---:|
| ODS | XML-tekens | 64 MiB |
| ODS | Rijen per werkblad | 1,048,576 |
| ODS | Kolommen per rij | 16,384 |
| ODS | Eén repeat-declaratie | rijen 1,048,576; kolommen 16,384 |
| ODS | Tekst uit één cel | 16 MiB |
| ODT | XML-tekens | 64 MiB |
| ODT | Teruggegeven tekstknopen | 1,000,000 |
| ODT | Tekst uit één knoop | 16 MiB |

Bij overschrijding mislukt het lezen; repeat wordt niet afgekapt om ogenschijnlijk volledige gegevens te
leveren. Probeer niet automatisch opnieuw zonder limieten. `LeaveOpen` is standaard `false`; bij `true`
sluiten XML-entry en ZIP-reader, maar blijft de buitenste stream van de aanroeper open.

Behoud limieten voor niet-vertrouwde documenten en valideer pakket en schema. Hogere limieten vergroten
geheugen- en CPU DoS-risico. `MaxXmlCharactersInDocument = 0` schakelt alleen de XML-tekenlimiet uit.
Limieten, validatie en opschoning beperken risico maar garanderen geen absolute veiligheid.
