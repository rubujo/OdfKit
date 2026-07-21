---
title: Bezpečnostné limity načítania a streamovacích čítačiek
_lang: sk
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Bezpečnostné limity načítania a streamovacích čítačiek

> Informatívny preklad; pri rozdiele má prednosť autoritatívny zdroj zh-TW.

Načítanie balíka a `OdsStreamReader`/`OdtStreamReader` spracúvajú nedôveryhodný vstup ZIP/XML. Čítačky nevytvárajú úplný DOM dokumentu, ale prideľujú vyrovnávacie pamäte
pre aktuálny riadok, text uzlov, dekompresiu ZIP a čítačku XML. Nízka rezidentná pamäť neodstraňuje vplyv
veľkosti vstupu.

## Limity základného balíka

`OdfDocument.Load`, fasády `Load` jednotlivých formátov a `OdfPackage.Open` zdieľajú rozpočty `OdfLoadOptions`.

| Limit | Predvolená hodnota | Účel ochrany |
|---|---:|---|
| Položky ZIP | 5,000 | Bráni vyčerpaniu CPU a pamäte mnohými malými položkami |
| Rozbalená veľkosť jednej položky | 500 MiB | Obmedzuje rozbalenie jednej položky ZIP |
| Celková rozbalená veľkosť | 1 GiB | Obmedzuje celkové rozbalenie balíka |
| Veľkosť neskenovateľného vstupu | 1 GiB | Obmedzuje vyrovnávaciu pamäť pred rozbalením ZIP |
| Znaky v jednom dokumente XML | 64 MiB | Obmedzuje spracovanie XML a vytvorenie DOM |

Štyri limity ZIP musia byť kladné; nula alebo záporné hodnoty okamžite vyvolajú `ArgumentOutOfRangeException`. Iba `MaxXmlCharactersInDocument = 0` vypne limit XML. Všetky XML čítačky musia zakázať externé DTD a resolvery. Nové cesty musia použiť `OdfLoadOptions`. Validačné cesty balíkov a Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` a kontroly pravidiel profilov) tiež používajú `MaxXmlCharactersInDocument`: validácia balíka používa `package.LoadOptions`, zatiaľ čo validácia Flat používa `OdfValidationOptions.LoadOptions` (pri vynechaní predvolených 64 MiB z `OdfLoadOptions`). Podpisy, časové pečiatky, údaje o zrušení certifikátov a externé sieťové odpovede majú vlastné menšie limity; limit základného balíka ich nenahrádza. Pre zásady obsahu použite `OdfPackageValidator`, `SanitizeMacros`, overenie podpisu alebo `pwsh eng/Test-OdfPolicy.ps1`.

## Limity streamovacích čítačiek

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

Možnosti čítačiek ODS a ODT overujú pravidlá pri priradení vlastností: limit XML povoľuje nulu, ale limity riadkov, stĺpcov, repeat, uzlov a textu musia byť väčšie než nula.
