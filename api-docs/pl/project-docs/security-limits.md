---
title: Limity bezpieczeństwa czytników strumieniowych
_lang: pl
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Limity bezpieczeństwa czytników strumieniowych

> To tłumaczenie ma charakter informacyjny; w razie rozbieżności pierwszeństwo ma źródło w języku chińskim tradycyjnym (`zh-TW`).

`OdsStreamReader` i `OdtStreamReader` nie tworzą pełnego modelu DOM dokumentu, ale przydzielają bufory dla
bieżącego wiersza, tekstu węzłów, dekompresji ZIP i XML Reader. Konstrukcja o niskim użyciu pamięci
rezydentnej nie eliminuje wpływu rozmiaru danych wejściowych.

## Limity domyślne

| Reader | Limit | Wartość domyślna |
|---|---|---:|
| ODS | Znaki XML | 64 MiB |
| ODS | Wiersze w arkuszu | 1,048,576 |
| ODS | Kolumny w wierszu | 16,384 |
| ODS | Jedna deklaracja repeat | 1,048,576 wierszy; 16,384 kolumny |
| ODS | Tekst pobrany z jednej komórki | 16 MiB |
| ODT | Znaki XML | 64 MiB |
| ODT | Zwrócone węzły tekstowe | 1,000,000 |
| ODT | Tekst pobrany z jednego węzła | 16 MiB |

Po przekroczeniu limitu odczyt kończy się niepowodzeniem; repeat nie jest obcinany, aby nadal zwracać dane,
które wyglądają na kompletne. Takie niepowodzenie należy traktować jako wynik ochrony zasobów i nie ponawiać
operacji automatycznie z wyłączonymi limitami.

## Własność strumieni

Domyślna wartość opcji `LeaveOpen` to `false`. Po ustawieniu jej na `true` zwolnienie Readeru nadal zamyka
strumień wpisu XML i ZIP Reader, ale pozostawia otwarty najbardziej zewnętrzny strumień dostarczony przez
obiekt wywołujący.

## Granica zaufania

Dla niezaufanych dokumentów zachowaj limity domyślne i najpierw wykonaj walidację package i schema. Można
zwiększyć poszczególne limity dla zaufanych dużych dokumentów, które rzeczywiście trzeba przetworzyć, ale
zwiększenie limitów XML lub tekstu podnosi także ryzyko ataku na pamięć i CPU DoS.
`MaxXmlCharactersInDocument = 0` wyłącza tylko limit znaków XML; pozostałe limity Readeru nadal obowiązują.

Limity bezpieczeństwa, walidacja i oczyszczanie zmniejszają ryzyko, ale nie gwarantują całkowitego
bezpieczeństwa wobec złośliwych dokumentów.
