---
title: Sikkerhedsgrænser for indlæsning og streaminglæsere
_lang: da
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

Alle XML-læsere i kernen skal forbyde eksterne DTD'er og resolvere. Nye indlæsningsveje skal genbruge `OdfLoadOptions` eller tilsvarende dokumenterede budgetter. Valideringsveje for pakker og Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` og profilregler) anvender også `MaxXmlCharactersInDocument`: pakkevalidering bruger `package.LoadOptions`, mens Flat-validering bruger `OdfValidationOptions.LoadOptions` (standarden på 64 MiB fra `OdfLoadOptions`, når den udelades). Signaturer, tidsstempler, certifikatspærringsdata og eksterne netværkssvar har egne mindre grænser; kernepakkens grænse erstatter dem ikke. Grænserne beskytter ressourcer, ikke dokumentindhold; brug `OdfPackageValidator`, `SanitizeMacros`, signaturvalidering eller `pwsh eng/Test-OdfPolicy.ps1` til politikker.

## Øvrige ressource- og outputbeskyttelser

Ud over pakke- og streaming-reader-grænserne beskytter følgende faste grænser også mod ikke-betroet input. Værdierne er i øjeblikket faste konstanter i koden og kan endnu ikke konfigureres via `OdfLoadOptions` eller et indstillingsobjekt. Vurder konsekvensen for hukommelse og stak, før en grænse hæves eller fjernes.

| Område | Grænse | Adfærd ved overskridelse |
|---|---|---|
| Faktisk udpakket størrelse af en ZIP-post | Må ikke overstige den ikke-komprimerede størrelse, der er angivet i headeren (MMF-stien ved indlæsning fra filsti) | `SecurityException` |
| Beskadiget ZIP-centralkatalog eller ZIP64 | MMF-hurtigstien understøtter ikke ZIP64, og ingen post, der ikke kan fortolkes fuldt ud, må springes over i stilhed | Falder tilbage til validering og læsning via `ZipArchive` |
| Indlejringsdybde for XML-elementer | 256 niveauer (`OdfXmlReader.MaxElementDepth`); gælder ved DOM-indlæsning, Flat ODF-indlæsning, validering af profilregler og RDF-fortolkning | Indlæsning kaster `SecurityException`; validering rapporterer `ODF0303` eller `ODF0301` |
| Indlejringsdybde ved formelfortolkning | 256 niveauer hver for parenteser, funktionsargumenter, indlejrede arrays og efterfølgende præfiksoperatorer | `InvalidOperationException` |
| Samlet antal operatornoder i formler | 4.096 (`FormulaParser.MaxOperatorNodes`); binære, unære, procent- og referenceoperatorer tilsammen. Kædede formler danner venstredybe træer, hvis evaluering og serialisering rekurserer niveau for niveau; grænsen sikrer, at en almindelig trådstak rækker | `InvalidOperationException` |
| Stakreserve ved formelrekursion | Fortolkning, evaluering, hentning af områder og serialisering kontrollerer den resterende stak (`RuntimeHelpers.EnsureSufficientExecutionStack`), før hvert rekursionsniveau påbegyndes; selv formler tæt på grænserne får ikke processen til at gå ned på tråde med lille stak på 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` omdanner den til en formelevalueringsfejl eller `#VALUE!` |
| Længde af formlers strengresultat | 1.048.576 tegn (`&`, funktionsresultater, `SUBSTITUTE`, `REPT`) | Returnerer `#VALUE!` |
| Formelfunktioner med et argument som løkkegrænse | `BINOMDIST` kumulativt 100.000; `CRITBINOM`, `HYPGEOMDIST` kumulativt 100.000; `POISSON` kumulativt 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` perioder 1.000.000 | Returnerer `#NUM!` |
| Række-/kolonneindeks i regneark | Række 1.048.575, kolonne 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Tilføjelse af dokument | Et dokument må ikke tilføjes til sig selv | `ArgumentException` |
| Collaboration `addColumns` | Begrænset af antal kolonner og samlet antal celler i `OdtOperationSafetyOptions` | Logger sikkerhedsgrænsen og springer handlingen over |
| Reservebillede af diagram | 4.096 px pr. side | Begrænses til grænsen |
| LibreOffice-konverteringsformat | Filtypedelen før kolon må ikke være tom og må ikke indeholde `..`, NUL, CR eller LF | `ArgumentException` |
| SPARQL-forespørgsel | `SERVICE`-klausuler er ikke tilladt (forhindrer forespørgselsmotoren i at sende netværksanmodninger til vilkårlige endepunkter) | `ArgumentException` |

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
