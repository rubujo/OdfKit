---
title: Beveiligingslimieten voor laden en streaming readers
_lang: nl
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Overige bescherming van bronnen en uitvoer

Naast de limieten voor het pakket en de streaming-readers beschermen ook de volgende vaste limieten tegen niet-vertrouwde invoer. Deze waarden zijn momenteel vaste constanten in de code en kunnen nog niet worden ingesteld via `OdfLoadOptions` of een opties-object. Beoordeel de gevolgen voor geheugen en stack voordat u een limiet verhoogt of verwijdert.

| Onderdeel | Limiet | Gedrag bij overschrijding |
|---|---|---|
| Werkelijke uitgepakte grootte van een ZIP-item | Mag de in de header opgegeven ongecomprimeerde grootte niet overschrijden (MMF-pad bij laden via bestandspad) | `SecurityException` |
| Beschadigde centrale ZIP-directory of ZIP64 | Het snelle MMF-pad ondersteunt geen ZIP64 en geen enkel record dat niet volledig kan worden ontleed, mag stilzwijgend worden overgeslagen | Valt terug op validatie en lezen via `ZipArchive` |
| Nestingsdiepte van XML-elementen | 256 niveaus (`OdfXmlReader.MaxElementDepth`); geldt voor DOM-laden, Flat ODF-laden, validatie van profielregels en RDF-parsing | Laden werpt `SecurityException`; validatie meldt `ODF0303` of `ODF0301` |
| Nestingsdiepte bij formuleparsing | Elk 256 niveaus voor haakjes, functieargumenten, inline arrays en opeenvolgende prefixoperatoren | `InvalidOperationException` |
| Totaal aantal operatorknooppunten in een formule | 4.096 (`FormulaParser.MaxOperatorNodes`); binaire, unaire, procent- en verwijzingsoperatoren samen. Geketende formules vormen linksdiepe bomen waarvan evaluatie en serialisatie niveau voor niveau recursief verlopen; deze limiet zorgt dat een gewone threadstack volstaat | `InvalidOperationException` |
| Stackmarge bij formulerecursie | Parsing, evaluatie, het ophalen van bereiken en serialisatie controleren de resterende stack (`RuntimeHelpers.EnsureSufficientExecutionStack`) voordat elk recursieniveau wordt betreden; zelfs formules dicht bij de limieten laten het proces niet crashen op threads met een kleine stack van 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` zet deze om in een formule-evaluatie-uitzondering of `#VALUE!` |
| Lengte van het tekenreeksresultaat van een formule | 1.048.576 tekens (`&`, functieresultaten, `SUBSTITUTE`, `REPT`) | Geeft `#VALUE!` terug |
| Formulefuncties waarvan de lusgrens een argument is | `BINOMDIST` cumulatief 100.000; `CRITBINOM`, `HYPGEOMDIST` cumulatief 100.000; `POISSON` cumulatief 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` perioden 1.000.000 | Geeft `#NUM!` terug |
| Rij-/kolomindex van het werkblad | Rij 1.048.575, kolom 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Document toevoegen | Een document mag niet aan zichzelf worden toegevoegd | `ArgumentException` |
| Collaboration `addColumns` | Begrensd door het aantal kolommen en het totale aantal cellen van `OdtOperationSafetyOptions` | Registreert de veiligheidslimiet en slaat de bewerking over |
| Terugvalafbeelding van grafiek | 4.096 px per zijde | Begrensd tot de limiet |
| LibreOffice-conversieformaat | Het extensiedeel vóór de dubbele punt mag niet leeg zijn en mag geen `..`, NUL, CR of LF bevatten | `ArgumentException` |
| SPARQL-query | `SERVICE`-clausules zijn niet toegestaan (voorkomt dat de query-engine netwerkverzoeken naar willekeurige eindpunten verstuurt) | `ArgumentException` |

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
