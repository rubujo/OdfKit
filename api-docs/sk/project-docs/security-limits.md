---
title: Bezpečnostné limity streamovacích čítačiek
_lang: sk
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Bezpečnostné limity streamovacích čítačiek

> Informatívny preklad; pri rozdiele má prednosť autoritatívny zdroj zh-TW.

`OdsStreamReader` a `OdtStreamReader` nevytvárajú úplný DOM dokumentu, ale prideľujú vyrovnávacie pamäte
pre aktuálny riadok, text uzlov, dekompresiu ZIP a čítačku XML. Nízka rezidentná pamäť neodstraňuje vplyv
veľkosti vstupu.

| Čítačka | Limit | Predvolená hodnota |
|---|---|---:|
| ODS | Znaky XML | 64 MiB |
| ODS | Riadky na hárok | 1,048,576 |
| ODS | Stĺpce na riadok | 16,384 |
| ODS | Jedna deklarácia repeat | riadky 1,048,576; stĺpce 16,384 |
| ODS | Text jednej bunky | 16 MiB |
| ODT | Znaky XML | 64 MiB |
| ODT | Vrátené textové uzly | 1,000,000 |
| ODT | Text jedného uzla | 16 MiB |

Po prekročení limitu čítanie zlyhá; repeat sa neskráti tak, aby vrátil zdanlivo úplné údaje. Neopakujte
automaticky pokus bez limitov. `LeaveOpen` má predvolenú hodnotu `false`; pri `true` sa zatvorí prúd položky
XML a čítačka ZIP, ale vonkajší prúd volajúceho zostane otvorený.

Pre nedôveryhodné dokumenty ponechajte limity a overte balík aj schému. Vyššie limity zvyšujú riziko pamäte
a CPU DoS. `MaxXmlCharactersInDocument = 0` vypína iba limit znakov XML. Limity, validácia a sanitizácia
znižujú riziko, ale nezaručujú absolútnu bezpečnosť.
