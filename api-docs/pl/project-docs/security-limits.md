---
title: Limity bezpieczeństwa ładowania i czytników strumieniowych
_lang: pl
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Limity bezpieczeństwa ładowania i czytników strumieniowych

> To tłumaczenie ma charakter informacyjny; w razie rozbieżności pierwszeństwo ma źródło w języku chińskim tradycyjnym (`zh-TW`).

Ładowanie pakietów oraz `OdsStreamReader`/`OdtStreamReader` przetwarzają niezaufane dane ZIP/XML. Czytniki nie tworzą pełnego modelu DOM dokumentu, ale przydzielają bufory dla
bieżącego wiersza, tekstu węzłów, dekompresji ZIP i XML Reader. Konstrukcja o niskim użyciu pamięci
rezydentnej nie eliminuje wpływu rozmiaru danych wejściowych.

## Limity pakietu podstawowego

`OdfDocument.Load`, fasady `Load` poszczególnych formatów i `OdfPackage.Open` współdzielą budżety `OdfLoadOptions`.

| Limit | Wartość domyślna | Cel ochrony |
|---|---:|---|
| Wpisy ZIP | 5,000 | Zapobiega wyczerpaniu CPU i pamięci przez wiele małych wpisów |
| Rozpakowany rozmiar jednego wpisu | 500 MiB | Ogranicza rozwinięcie jednego wpisu ZIP |
| Łączny rozpakowany rozmiar | 1 GiB | Ogranicza łączne rozwinięcie pakietu |
| Surowy rozmiar danych bez wyszukiwania | 1 GiB | Ogranicza buforowanie przed rozwinięciem ZIP |
| Znaki w jednym dokumencie XML | 64 MiB | Ogranicza analizę XML i budowę DOM |

Cztery limity ZIP muszą być dodatnie; zero lub wartości ujemne natychmiast powodują `ArgumentOutOfRangeException`. Tylko `MaxXmlCharactersInDocument = 0` wyłącza limit XML. Wszystkie XML Readery muszą blokować zewnętrzne DTD i resolvery. Nowe ścieżki muszą używać `OdfLoadOptions`; zasady treści realizuj przez `OdfPackageValidator`, `SanitizeMacros`, walidację podpisu lub `pwsh eng/Test-OdfPolicy.ps1`.

## Limity czytników strumieniowych

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

Opcje Readerów ODS i ODT sprawdzają reguły przy przypisaniu: limit XML dopuszcza zero, a limity wierszy, kolumn, repeat, węzłów i tekstu muszą być większe od zera.

Limity bezpieczeństwa, walidacja i oczyszczanie zmniejszają ryzyko, ale nie gwarantują całkowitego
bezpieczeństwa wobec złośliwych dokumentów.
