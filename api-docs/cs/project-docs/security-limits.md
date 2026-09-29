---
title: Bezpečnostní limity načítání a proudových čteček
_lang: cs
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
---

# Bezpečnostní limity načítání a proudových čteček

> Tento překlad je pouze informativní; v případě rozdílu má přednost zdroj v tradiční čínštině (`zh-TW`).

Načítání základního balíčku a `OdsStreamReader`/`OdtStreamReader` zpracovávají nedůvěryhodný vstup ZIP/XML. Čtečky nevytvářejí úplný DOM dokumentu, ale přidělují vyrovnávací paměti pro
aktuální řádek, text uzlů, dekompresi ZIP a XML Reader. Návrh s nízkými nároky na trvalou paměť neodstraňuje
vliv velikosti vstupu.

## Limity základního balíčku

`OdfDocument.Load`, fasády `Load` jednotlivých formátů a přímé volání `OdfPackage.Open` sdílejí rozpočty prostředků `OdfLoadOptions`.

| Limit | Výchozí hodnota | Účel ochrany |
|---|---:|---|
| Počet položek ZIP | 5,000 | Brání vyčerpání CPU a paměti mnoha malými položkami |
| Rozbalená velikost jedné položky | 500 MiB | Omezuje rozbalení jedné položky ZIP |
| Celková rozbalená velikost balíčku | 1 GiB | Omezuje souhrnné rozbalení položek |
| Velikost původního nevyhledávatelného vstupu | 1 GiB | Omezuje vyrovnávací paměť před rozbalením ZIP |
| Znaky v jednom dokumentu XML | 64 MiB | Omezuje náklady na zpracování XML a vytvoření DOM |

Počet položek, velikost položky, celkové rozbalení a velikost původního balíčku musí být kladné. Nula nebo záporná hodnota okamžitě vyvolá `ArgumentOutOfRangeException`. Pouze `MaxXmlCharactersInDocument = 0` vypne limit znaků XML; záporné hodnoty zůstávají neplatné.

Všechny základní XML Readery musí zakázat externí DTD a resolvery. Nové cesty načítání musí používat `OdfLoadOptions` nebo rovnocenné zdokumentované rozpočty. Validační cesty balíčku a Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` a kontroly pravidel profilů) také používají `MaxXmlCharactersInDocument`: balíček používá `package.LoadOptions`, zatímco Flat XML používá `OdfValidationOptions.LoadOptions` (při vynechání výchozích 64 MiB z `OdfLoadOptions`). Podpisy, časová razítka, data o odvolání certifikátů a externí síťové odpovědi mají vlastní menší limity; základní limit balíčku je nenahrazuje. Tyto limity chrání prostředky, nikoli obsah dokumentu; zásady vynucujte pomocí `OdfPackageValidator`, `SanitizeMacros`, ověření podpisu nebo `pwsh eng/Test-OdfPolicy.ps1`.

## Limity proudových čteček

| Reader | Limit | Výchozí hodnota |
|---|---|---:|
| ODS | Znaky XML | 64 MiB |
| ODS | Řádky na list | 1,048,576 |
| ODS | Sloupce na řádek | 16,384 |
| ODS | Jedna deklarace repeat | 1,048,576 řádků; 16,384 sloupců |
| ODS | Text načtený z jedné buňky | 16 MiB |
| ODT | Znaky XML | 64 MiB |
| ODT | Vrácené textové uzly | 1,000,000 |
| ODT | Text načtený z jednoho uzlu | 16 MiB |

Při překročení limitu načítání selže; repeat se nezkrátí a čtečka nepokračuje s vracením zdánlivě úplných
dat. Takové selhání považujte za výsledek ochrany prostředků a neopakujte operaci automaticky s vypnutými
limity.

## Vlastnictví datových proudů

Výchozí hodnota možnosti `LeaveOpen` je `false`. Při nastavení na `true` se po uvolnění Readeru stále zavře
datový proud položky XML a ZIP Reader, ale nejvzdálenější datový proud poskytnutý volajícím zůstane otevřený.

## Další ochrany zdrojů a výstupu

Kromě limitů balíčku a streamovaných readerů chrání před nedůvěryhodným vstupem také následující pevné limity. Tyto hodnoty jsou zatím pevné konstanty v kódu a nelze je nastavit přes `OdfLoadOptions` ani objekt voleb. Před zvýšením nebo odstraněním limitu posuďte dopad na paměť a zásobník.

| Oblast | Limit | Chování při překročení |
|---|---|---|
| Skutečná dekomprimovaná velikost položky ZIP | Nesmí překročit nekomprimovanou velikost deklarovanou v hlavičce (cesta MMF při načítání ze souboru) | `SecurityException` |
| Poškozený centrální adresář ZIP nebo ZIP64 | Rychlá cesta MMF nepodporuje ZIP64 a žádný záznam, který nelze plně zpracovat, nesmí být mlčky přeskočen | Přechod na ověření a čtení přes `ZipArchive` |
| Hloubka vnoření elementů XML | 256 úrovní (`OdfXmlReader.MaxElementDepth`); platí pro načítání DOM, načítání Flat ODF, ověřování pravidel profilu a parsování RDF | Načtení vyvolá `SecurityException`; ověření hlásí `ODF0303` nebo `ODF0301` |
| Hloubka vnoření při parsování vzorců | Po 256 úrovních pro závorky, argumenty funkcí, vložená pole a po sobě jdoucí prefixové operátory | `InvalidOperationException` |
| Celkový počet uzlů operátorů ve vzorci | 4 096 (`FormulaParser.MaxOperatorNodes`); dohromady binární, unární, procentní a referenční operátory. Řetězené vzorce tvoří levostranně hluboké stromy, jejichž vyhodnocení a serializace rekurzí procházejí úroveň po úrovni; tento limit zajišťuje dostatečný běžný zásobník vlákna | `InvalidOperationException` |
| Rezerva zásobníku při rekurzi vzorců | Parsování, vyhodnocení, získání rozsahů a serializace před vstupem do každé úrovně rekurze kontrolují zbývající zásobník (`RuntimeHelpers.EnsureSufficientExecutionStack`); ani vzorce blízké limitům neshodí proces na vláknech s malým zásobníkem 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` ji převede na výjimku vyhodnocení vzorce nebo `#VALUE!` |
| Délka řetězcového výsledku vzorce | 1 048 576 znaků (`&`, výsledky funkcí, `SUBSTITUTE`, `REPT`) | Vrací `#VALUE!` |
| Funkce vzorců s mezí cyklu daným argumentem | `BINOMDIST` kumulativně 100 000; `CRITBINOM`, `HYPGEOMDIST` kumulativně 100 000; `POISSON` kumulativně 1 000 000; `DB`, `DDB`, `VDB`, `CUMIPMT` období 1 000 000 | Vrací `#NUM!` |
| Index řádku/sloupce tabulky | Řádek 1 048 575, sloupec 16 383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Připojení dokumentu | Dokument nelze připojit sám k sobě | `ArgumentException` |
| Collaboration `addColumns` | Omezeno počtem sloupců a celkovým počtem buněk v `OdtOperationSafetyOptions` | Zaznamená bezpečnostní limit a operaci přeskočí |
| Záložní obrázek grafu | 4 096 px na stranu | Omezeno na limit |
| Formát převodu LibreOffice | Část přípony před dvojtečkou nesmí být prázdná a nesmí obsahovat `..`, NUL, CR ani LF | `ArgumentException` |
| Dotaz SPARQL | Klauzule `SERVICE` není povolena (brání dotazovacímu enginu odesílat síťové požadavky na libovolné koncové body) | `ArgumentException` |

## Hranice důvěry

Pro nedůvěryhodné dokumenty ponechte výchozí limity a nejprve proveďte ověření package a schema. Jednotlivé
limity lze zvýšit pro důvěryhodné velké dokumenty, které je skutečně nutné zpracovat. Zvýšením limitů XML
nebo textu se však zároveň zvyšuje riziko útoku na paměť a CPU DoS. `MaxXmlCharactersInDocument = 0`
vypne pouze limit počtu znaků XML; ostatní limity Readeru zůstávají účinné.

Možnosti Readerů ODS a ODT ověřují stejná pravidla již při nastavení vlastnosti: limit XML přijímá nulu, ale odmítá záporné hodnoty; limity řádků, sloupců, repeat, uzlů a textu musí být větší než nula.

Bezpečnostní limity, ověřování a čištění snižují riziko, ale nepředstavují záruku absolutní bezpečnosti vůči
škodlivým dokumentům.
