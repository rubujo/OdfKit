---
title: Bezpečnostné limity načítania a streamovacích čítačiek
_lang: sk
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Ďalšie ochrany zdrojov a výstupu

Okrem limitov balíka a streamovaných readerov chránia pred nedôveryhodným vstupom aj nasledujúce pevné limity. Tieto hodnoty sú zatiaľ pevné konštanty v kóde a nedajú sa nastaviť cez `OdfLoadOptions` ani objekt volieb. Pred zvýšením alebo odstránením limitu posúďte vplyv na pamäť a zásobník.

| Oblasť | Limit | Správanie pri prekročení |
|---|---|---|
| Skutočná dekomprimovaná veľkosť položky ZIP | Nesmie prekročiť nekomprimovanú veľkosť deklarovanú v hlavičke (cesta MMF pri načítaní zo súboru) | `SecurityException` |
| Poškodený centrálny adresár ZIP alebo ZIP64 | Rýchla cesta MMF nepodporuje ZIP64 a žiadny záznam, ktorý sa nedá úplne spracovať, sa nesmie ticho preskočiť | Prechod na overenie a čítanie cez `ZipArchive` |
| Hĺbka vnorenia elementov XML | 256 úrovní (`OdfXmlReader.MaxElementDepth`); platí pre načítanie DOM, načítanie Flat ODF, overovanie pravidiel profilu a spracovanie RDF | Načítanie vyvolá `SecurityException`; overenie hlási `ODF0303` alebo `ODF0301` |
| Hĺbka vnorenia pri spracovaní vzorcov | Po 256 úrovní pre zátvorky, argumenty funkcií, vložené polia a po sebe idúce prefixové operátory | `InvalidOperationException` |
| Celkový počet uzlov operátorov vo vzorci | 4 096 (`FormulaParser.MaxOperatorNodes`); spolu binárne, unárne, percentuálne a referenčné operátory. Reťazené vzorce tvoria ľavostranne hlboké stromy, ktorých vyhodnotenie a serializácia rekurzívne postupujú úroveň po úrovni; tento limit zaisťuje, že postačuje bežný zásobník vlákna | `InvalidOperationException` |
| Rezerva zásobníka pri rekurzii vzorcov | Spracovanie, vyhodnotenie, získanie rozsahov a serializácia pred vstupom do každej úrovne rekurzie kontrolujú zostávajúci zásobník (`RuntimeHelpers.EnsureSufficientExecutionStack`); ani vzorce blízke limitom nezhodia proces na vláknach s malým zásobníkom 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` ju prevedie na výnimku vyhodnotenia vzorca alebo `#VALUE!` |
| Dĺžka reťazcového výsledku vzorca | 1 048 576 znakov (`&`, výsledky funkcií, `SUBSTITUTE`, `REPT`) | Vracia `#VALUE!` |
| Funkcie vzorcov s hranicou cyklu určenou argumentom | `BINOMDIST` kumulatívne 100 000; `CRITBINOM`, `HYPGEOMDIST` kumulatívne 100 000; `POISSON` kumulatívne 1 000 000; `DB`, `DDB`, `VDB`, `CUMIPMT` obdobia 1 000 000 | Vracia `#NUM!` |
| Index riadka/stĺpca tabuľky | Riadok 1 048 575, stĺpec 16 383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Pripojenie dokumentu | Dokument nemožno pripojiť sám k sebe | `ArgumentException` |
| Collaboration `addColumns` | Obmedzené počtom stĺpcov a celkovým počtom buniek v `OdtOperationSafetyOptions` | Zaznamená bezpečnostný limit a operáciu preskočí |
| Záložný obrázok grafu | 4 096 px na stranu | Obmedzené na limit |
| Formát konverzie LibreOffice | Časť prípony pred dvojbodkou nesmie byť prázdna a nesmie obsahovať `..`, NUL, CR ani LF | `ArgumentException` |
| Dopyt SPARQL | Klauzuly `SERVICE` nie sú povolené (bráni dopytovému enginu odosielať sieťové požiadavky na ľubovoľné koncové body) | `ArgumentException` |

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
