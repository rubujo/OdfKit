---
title: Sikkerhedsgrænser for streaminglæsere
_lang: da
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Sikkerhedsgrænser for streaminglæsere

> Informativ oversættelse; ved afvigelser gælder den autoritative zh-TW-kilden.

`OdsStreamReader` og `OdtStreamReader` bygger ikke et fuldstændigt DOM, men bruger buffere for den aktuelle række,
nodetekst, ZIP-dekomprimering og XML-læseren. Lavt hukommelsesforbrug gør ikke inputstørrelsen irrelevant.

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
