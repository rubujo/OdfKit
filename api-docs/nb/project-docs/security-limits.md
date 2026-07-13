---
title: Sikkerhetsgrenser for lasting og strømlesere
_lang: nb
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Sikkerhetsgrenser for lasting og strømlesere

> Informativ oversettelse; ved avvik gjelder den autoritative zh-TW-kilden.

Pakkelasting og `OdsStreamReader`/`OdtStreamReader` behandler ikke-klarert ZIP/XML-inndata. Leserne bygger ikke et fullstendig DOM, men bruker buffere for gjeldende rad,
nodetekst, ZIP-dekomprimering og XML-leseren. Lavt minnebruk gjør ikke inndatastørrelsen irrelevant.

## Grenser for kjernepakken

`OdfDocument.Load`, formatspesifikke `Load`-fasader og `OdfPackage.Open` deler ressursbudsjettene i `OdfLoadOptions`.

| Grense | Standard | Beskyttelsesformål |
|---|---:|---|
| ZIP-oppføringer | 5,000 | Hindrer uttømming av CPU og minne fra mange små oppføringer |
| Ukomprimert størrelse for én oppføring | 500 MiB | Begrenser utvidelsen av én ZIP-oppføring |
| Samlet ukomprimert størrelse | 1 GiB | Begrenser total utvidelse av pakken |
| Rå størrelse på ikke-søkbare inndata | 1 GiB | Begrenser bufring før ZIP-utvidelse |
| Tegn i ett XML-dokument | 64 MiB | Begrenser XML-behandling og DOM-bygging |

De fire ZIP-grensene må være positive; null eller negative verdier gir umiddelbart `ArgumentOutOfRangeException`. Bare `MaxXmlCharactersInDocument = 0` slår av XML-grensen. Alle XML-lesere må forby eksterne DTD-er og resolvere. Nye lastebaner må bruke `OdfLoadOptions`; bruk `OdfPackageValidator`, `SanitizeMacros`, signaturvalidering eller `pwsh eng/Test-OdfPolicy.ps1` for innholdspolicy.

## Grenser for strømlesere

| Leser | Grense | Standard |
|---|---|---:|
| ODS | XML-tegn | 64 MiB |
| ODS | Rader per ark | 1,048,576 |
| ODS | Kolonner per rad | 16,384 |
| ODS | Én repeat-erklæring | rader 1,048,576; kolonner 16,384 |
| ODS | Tekst fra én celle | 16 MiB |
| ODT | XML-tegn | 64 MiB |
| ODT | Returnerte tekstnoder | 1,000,000 |
| ODT | Tekst fra én node | 16 MiB |

Lesing mislykkes når en grense overskrides; repeat avkortes ikke for å returnere tilsynelatende komplette
data. Ikke prøv automatisk på nytt uten grenser. `LeaveOpen` er normalt `false`; med `true` lukkes XML-
strømmen og ZIP-leseren, mens innringerens ytterste strøm forblir åpen.

Behold grensene for ukjente dokumenter og valider pakke og skjema. Høyere grenser øker minne- og CPU DoS-
risiko. `MaxXmlCharactersInDocument = 0` slår bare av XML-tegngrensen. Grenser, validering og sanitering
reduserer risiko, men garanterer ikke absolutt sikkerhet.

ODS- og ODT-leserinnstillinger validerer reglene når egenskaper tilordnes: XML-grensen tillater null, mens grenser for rader, kolonner, repeat, noder og tekst må være større enn null.
