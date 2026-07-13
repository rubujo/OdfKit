---
title: Sikkerhedsgrænser for indlæsning og streaminglæsere
_lang: da
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Sikkerhedsgrænser for indlæsning og streaminglæsere

> Informativ oversættelse; ved afvigelser gælder den autoritative zh-TW-kilden.

Indlæsning af kernepakken og `OdsStreamReader`/`OdtStreamReader` behandler ZIP/XML-input, der ikke er tillid til. Læserne bygger ikke et fuldstændigt DOM, men bruger buffere for den aktuelle række,
nodetekst, ZIP-dekomprimering og XML-læseren. Lavt hukommelsesforbrug gør ikke inputstørrelsen irrelevant.

## Grænser for kernepakken

`OdfDocument.Load`, formatspecifikke `Load`-facader og direkte kald til `OdfPackage.Open` deler ressourcebudgetterne i `OdfLoadOptions`.

| Grænse | Standard | Beskyttelsesformål |
|---|---:|---|
| ZIP-poster | 5,000 | Forhindrer udtømning af CPU og hukommelse fra mange små poster |
| Udpakket størrelse af én post | 500 MiB | Begrænser udvidelsen af én ZIP-post |
| Samlet udpakket pakkestørrelse | 1 GiB | Begrænser samlet udvidelse på tværs af poster |
| Rå størrelse af ikke-søgbart input | 1 GiB | Begrænser buffering før ZIP-udvidelse |
| Tegn i ét XML-dokument | 64 MiB | Begrænser omkostninger til XML-behandling og DOM-opbygning |

Antal poster, poststørrelse, samlet udvidelse og rå pakkestørrelse skal være positive. Nul eller negative værdier udløser straks `ArgumentOutOfRangeException`. Kun `MaxXmlCharactersInDocument = 0` slår XML-tegngrænsen fra; negative værdier er stadig ugyldige.

Alle XML-læsere i kernen skal forbyde eksterne DTD'er og resolvere. Nye indlæsningsveje skal genbruge `OdfLoadOptions` eller tilsvarende dokumenterede budgetter. Grænserne beskytter ressourcer, ikke dokumentindhold; brug `OdfPackageValidator`, `SanitizeMacros`, signaturvalidering eller `pwsh eng/Test-OdfPolicy.ps1` til politikker.

## Grænser for streaminglæsere

| Læser | Grænse | Standard |
|---|---|---:|
| ODS | XML-tegn | 64 MiB |
| ODS | Rækker pr. regneark | 1,048,576 |
| ODS | Kolonner pr. række | 16,384 |
| ODS | Én repeat-erklæring | rækker 1,048,576; kolonner 16,384 |
| ODS | Tekst fra én celle | 16 MiB |
| ODT | XML-tegn | 64 MiB |
| ODT | Returnerede tekstnoder | 1,000,000 |
| ODT | Tekst fra én node | 16 MiB |

Læsning mislykkes når en grænse overskrides; repeat afkortes ikke for at returnere tilsyneladende komplette
data. Forsøg ikke automatisk igen uden grænser. `LeaveOpen` er normalt `false`; med `true` lukkes XML-
strømmen og ZIP-leseren, mens kalderens yderste stream forbliver åben.

Bevar grænserne for dokumenter, der ikke er tillid til og validér pakke og skema. Højere grænser øger hukommelses- og CPU DoS-
risiko. `MaxXmlCharactersInDocument = 0` slår bare af XML-tegngrensen. Grænser, validering og sanitering
reducerer risiko, men garanterer ikke absolut sikkerhed.

ODS- og ODT-læserindstillinger validerer de samme regler, når egenskaber tildeles: XML-grænsen tillader nul, men afviser negative værdier; grænser for rækker, kolonner, repeat, noder og tekst skal være større end nul.
