---
title: Beveiligingslimieten voor laden en streaming readers
_lang: nl
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Beveiligingslimieten voor laden en streaming readers

> Informatieve vertaling; bij verschillen geldt de gezaghebbende zh-TW-bron.

Pakketladen en `OdsStreamReader`/`OdtStreamReader` verwerken niet-vertrouwde ZIP/XML-invoer. De readers bouwen geen volledig DOM, maar reserveren buffers voor de huidige
rij, knooptekst, ZIP-decompressie en XML-reader. Laag resident geheugen maakt invoergrootte niet irrelevant.

## Limieten voor het kernpakket

`OdfDocument.Load`, formaatspecifieke `Load`-facades en `OdfPackage.Open` delen de resourcebudgetten van `OdfLoadOptions`.

| Limiet | Standaard | Beschermingsdoel |
|---|---:|---|
| ZIP-items | 5,000 | Voorkomt uitputting van CPU en geheugen door veel kleine items |
| Uitgepakte grootte van één item | 500 MiB | Begrens de expansie van één ZIP-item |
| Totale uitgepakte grootte | 1 GiB | Begrens de totale expansie van het pakket |
| Ruwe niet-zoekbare invoergrootte | 1 GiB | Begrens buffering vóór ZIP-expansie |
| Tekens in één XML-document | 64 MiB | Begrens XML-verwerking en DOM-opbouw |

De vier ZIP-limieten moeten positief zijn; nul of negatieve waarden veroorzaken direct `ArgumentOutOfRangeException`. Alleen `MaxXmlCharactersInDocument = 0` schakelt de XML-limiet uit. Alle XML-readers moeten externe DTD's en resolvers verbieden. Nieuwe laadpaden moeten `OdfLoadOptions` gebruiken. Validatiepaden voor pakketten en Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` en scans van profielregels) passen ook `MaxXmlCharactersInDocument` toe: pakketvalidatie gebruikt `package.LoadOptions`, terwijl Flat-validatie `OdfValidationOptions.LoadOptions` gebruikt (standaard 64 MiB van `OdfLoadOptions` wanneer niet opgegeven). Handtekeningen, tijdstempels, gegevens over ingetrokken certificaten en externe netwerkreacties hebben eigen, kleinere limieten; de kernpakketlimiet vervangt deze niet. Gebruik voor inhoudsbeleid `OdfPackageValidator`, `SanitizeMacros`, handtekeningvalidatie of `pwsh eng/Test-OdfPolicy.ps1`.

## Limieten voor streaming readers

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

ODS- en ODT-readeropties valideren de regels bij toewijzing: de XML-limiet accepteert nul, terwijl limieten voor rijen, kolommen, repeat, knopen en tekst groter dan nul moeten zijn.
