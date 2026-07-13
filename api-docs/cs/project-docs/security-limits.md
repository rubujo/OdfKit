---
title: Bezpečnostní limity načítání a proudových čteček
_lang: cs
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
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

Všechny základní XML Readery musí zakázat externí DTD a resolvery. Nové cesty načítání musí používat `OdfLoadOptions` nebo rovnocenné zdokumentované rozpočty. Tyto limity chrání prostředky, nikoli obsah dokumentu; zásady vynucujte pomocí `OdfPackageValidator`, `SanitizeMacros`, ověření podpisu nebo `pwsh eng/Test-OdfPolicy.ps1`.

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

## Hranice důvěry

Pro nedůvěryhodné dokumenty ponechte výchozí limity a nejprve proveďte ověření package a schema. Jednotlivé
limity lze zvýšit pro důvěryhodné velké dokumenty, které je skutečně nutné zpracovat. Zvýšením limitů XML
nebo textu se však zároveň zvyšuje riziko útoku na paměť a CPU DoS. `MaxXmlCharactersInDocument = 0`
vypne pouze limit počtu znaků XML; ostatní limity Readeru zůstávají účinné.

Možnosti Readerů ODS a ODT ověřují stejná pravidla již při nastavení vlastnosti: limit XML přijímá nulu, ale odmítá záporné hodnoty; limity řádků, sloupců, repeat, uzlů a textu musí být větší než nula.

Bezpečnostní limity, ověřování a čištění snižují riziko, ale nepředstavují záruku absolutní bezpečnosti vůči
škodlivým dokumentům.
