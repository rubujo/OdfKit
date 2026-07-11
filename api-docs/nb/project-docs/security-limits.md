---
title: Sikkerhetsgrenser for strømlesere
_lang: nb
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Sikkerhetsgrenser for strømlesere

> Informativ oversettelse; ved avvik gjelder den autoritative zh-TW-kilden.

`OdsStreamReader` og `OdtStreamReader` bygger ikke et fullstendig DOM, men bruker buffere for gjeldende rad,
nodetekst, ZIP-dekomprimering og XML-leseren. Lavt minnebruk gjør ikke inndatastørrelsen irrelevant.

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
