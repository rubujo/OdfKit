---
title: Bezpečnostní limity proudových čteček
_lang: cs
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Bezpečnostní limity proudových čteček

> Tento překlad je pouze informativní; v případě rozdílu má přednost zdroj v tradiční čínštině (`zh-TW`).

`OdsStreamReader` a `OdtStreamReader` nevytvářejí úplný DOM dokumentu, ale přidělují vyrovnávací paměti pro
aktuální řádek, text uzlů, dekompresi ZIP a XML Reader. Návrh s nízkými nároky na trvalou paměť neodstraňuje
vliv velikosti vstupu.

## Výchozí limity

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

Bezpečnostní limity, ověřování a čištění snižují riziko, ale nepředstavují záruku absolutní bezpečnosti vůči
škodlivým dokumentům.
