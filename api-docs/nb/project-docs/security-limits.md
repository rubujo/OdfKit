---
title: Sikkerhetsgrenser for lasting og strømlesere
_lang: nb
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

De fire ZIP-grensene må være positive; null eller negative verdier gir umiddelbart `ArgumentOutOfRangeException`. Bare `MaxXmlCharactersInDocument = 0` slår av XML-grensen. Alle XML-lesere må forby eksterne DTD-er og resolvere. Nye lastebaner må bruke `OdfLoadOptions`. Valideringsbaner for pakker og Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` og skanning av profilregler) bruker også `MaxXmlCharactersInDocument`: pakkevalidering bruker `package.LoadOptions`, mens Flat-validering bruker `OdfValidationOptions.LoadOptions` (standardverdien 64 MiB fra `OdfLoadOptions` når den utelates). Signaturer, tidsstempler, sertifikatopphevingsdata og eksterne nettverkssvar har egne mindre grenser; grensen for kjernepakken erstatter dem ikke. Bruk `OdfPackageValidator`, `SanitizeMacros`, signaturvalidering eller `pwsh eng/Test-OdfPolicy.ps1` for innholdspolicy.

## Andre ressurs- og utdatabeskyttelser

I tillegg til grensene for pakke og strømmende lesere beskytter også følgende faste grenser mot ikke-klarert inndata. Verdiene er foreløpig faste konstanter i koden og kan ennå ikke settes via `OdfLoadOptions` eller et alternativobjekt. Vurder konsekvensene for minne og stakk før en grense økes eller fjernes.

| Område | Grense | Oppførsel ved overskridelse |
|---|---|---|
| Faktisk utpakket størrelse for en ZIP-oppføring | Må ikke overskride den ukomprimerte størrelsen som er oppgitt i headeren (MMF-stien ved innlasting fra filsti) | `SecurityException` |
| Skadet ZIP-sentralkatalog eller ZIP64 | MMF-hurtigstien støtter ikke ZIP64, og ingen post som ikke kan tolkes fullstendig, må hoppes over i stillhet | Faller tilbake til validering og lesing via `ZipArchive` |
| Nestingsdybde for XML-elementer | 256 nivåer (`OdfXmlReader.MaxElementDepth`); gjelder DOM-innlasting, Flat ODF-innlasting, validering av profilregler og RDF-tolking | Innlasting kaster `SecurityException`; validering rapporterer `ODF0303` eller `ODF0301` |
| Nestingsdybde ved formeltolking | 256 nivåer hver for parenteser, funksjonsargumenter, innebygde matriser og påfølgende prefiksoperatorer | `InvalidOperationException` |
| Totalt antall operatornoder i en formel | 4 096 (`FormulaParser.MaxOperatorNodes`); binære, unære, prosent- og referanseoperatorer til sammen. Lenkede formler danner venstredype trær der evaluering og serialisering rekurserer nivå for nivå; grensen sikrer at en vanlig trådstakk rekker | `InvalidOperationException` |
| Stakkreserve ved formelrekursjon | Tolking, evaluering, henting av områder og serialisering kontrollerer gjenværende stakk (`RuntimeHelpers.EnsureSufficientExecutionStack`) før hvert rekursjonsnivå; selv formler nær grensene får ikke prosessen til å krasje på tråder med liten stakk på 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` gjør den om til et formelevalueringsunntak eller `#VALUE!` |
| Lengde på formelens strengresultat | 1 048 576 tegn (`&`, funksjonsresultater, `SUBSTITUTE`, `REPT`) | Returnerer `#VALUE!` |
| Formelfunksjoner med et argument som løkkegrense | `BINOMDIST` kumulativt 100 000; `CRITBINOM`, `HYPGEOMDIST` kumulativt 100 000; `POISSON` kumulativt 1 000 000; `DB`, `DDB`, `VDB`, `CUMIPMT` perioder 1 000 000 | Returnerer `#NUM!` |
| Rad-/kolonneindeks i regneark | Rad 1 048 575, kolonne 16 383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Tillegging av dokument | Et dokument kan ikke legges til seg selv | `ArgumentException` |
| Collaboration `addColumns` | Begrenset av antall kolonner og totalt antall celler i `OdtOperationSafetyOptions` | Logger sikkerhetsgrensen og hopper over operasjonen |
| Reservebilde for diagram | 4 096 px per side | Begrenses til grensen |
| LibreOffice-konverteringsformat | Filendelsesdelen før kolon kan ikke være tom og kan ikke inneholde `..`, NUL, CR eller LF | `ArgumentException` |
| SPARQL-spørring | `SERVICE`-klausuler er ikke tillatt (hindrer spørringsmotoren i å sende nettverksforespørsler til vilkårlige endepunkter) | `ArgumentException` |

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
