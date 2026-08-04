---
title: Własność intelektualna i zgodność
_lang: pl
translation_source: docs/ip-compliance.md
translation_source_sha256: bccec797a382b4bf3fae941a34d0dd406fdc97cac84a38d6c20dc09109164b6f
---

# Własność intelektualna i zgodność (IP Compliance)

> To tłumaczenie ma charakter informacyjny; nie stanowi porady prawnej ani nie zastępuje konsultacji prawa właściwej jurysdykcji.

Ten dokument jest przeznaczony dla osób prowadzących **kontrolę zgodności i należytą staranność zakupową
podmiotów wdrażających projekt** oraz dla **współtwórców**. Nie stanowi porady prawnej ani nie zastępuje
konsultacji prawa właściwej jurysdykcji.

Powiązane audyty źródeł opisano w
[provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) oraz
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

## 1. Model licencjonowania (licencje złożone)

| Zakres | Licencja | Opis |
|---|---|---|
| Oryginalny kod projektu OdfKit | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | Projekt zrzeka się praw autorskich w największym możliwym zakresie; zobacz `LICENSE` w katalogu głównym |
| Zależności czasu kompilacji i wykonania | Głównie MIT, BSD i podobne | **Nie stają się domeną publiczną wskutek użycia CC0**; podczas dalszego rozpowszechniania należy zachować odpowiednie informacje i oświadczenia o prawach autorskich |
| Schematy OASIS ODF RELAX NG | OASIS Copyright | Znajdują się w `tools/OdfSchemaGenerator/schemas/`; zobacz `THIRD-PARTY-NOTICES.md` |
| Przypadki testowe Corpus i Collaboration | Pole `license` każdego przypadku | Zobacz `docs/corpus-manifest.md` i poszczególne pliki `manifest.json` |

**Ważne:** podczas rozpowszechniania aplikacji lub pakietu zawierającego OdfKit i jego zależności należy
jednocześnie spełnić:

1. skutki projektowej licencji `LICENSE` (CC0) dla kodu oryginalnego; oraz
2. obowiązki wynikające z licencji innych firm wymienione w
   [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md).

Nie wolno publicznie twierdzić, że „cały produkt wynikowy znajduje się w domenie publicznej”.

### Granice CC0 dotyczące patentów i znaków towarowych

Zgodnie z sekcją 4(a) CC0 1.0 prawa patentowe i prawa do znaków towarowych nie są udzielane ani zrzekane.
OdfKit nie zapewnia licencji patentowej, gwarancji nienaruszania patentów, wyszukiwania patentów ani odszkodowania.
Użytkownicy muszą przeprowadzić własną analizę. Rozstrzyga
[tekst prawny CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

## 2. Podmioty uprawnione i oświadczenie o treściach tworzonych przy użyciu AI

- README informuje, że znaczna część opublikowanego kodu źródłowego, dokumentacji, przykładów i testów została napisana, uporządkowana lub utworzona przy użyciu narzędzi AI.
- Affirmer licencji CC0 musi mieć prawo rozporządzania prawami, których się zrzeka. Przed przesłaniem wkładu współtwórca powinien potwierdzić, że ma prawo włączyć treść na licencji projektu; zobacz część dotyczącą DCO poniżej.
- Ocena praw autorskich do treści utworzonych wyłącznie przez maszyny różni się między jurysdykcjami. Podmiot wymagający jasno określonego właściciela praw i zobowiązania do odszkodowania za naruszenie powinien rozważyć rozwiązania komercyjne lub osobną umowę wsparcia. **Ten projekt o otwartym kodzie źródłowym domyślnie nie zapewnia komercyjnego odszkodowania**.

## 3. Proces clean-room i źródła zabronione

Autorytatywne źródła, dozwolone działania oraz **źródła, których nie wolno kopiować**, dla modułów wysokiego
ryzyka, takich jak obliczanie OpenFormula, walidacja schema pattern, szyfrowanie OpenPGP, JSON Collaboration
i kontrolowane konwersje formatów, wymieniono w
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

Podsumowanie zasad:

- **Dozwolone:** publiczne normy OASIS, ISO, RFC, W3C i innych organizacji; publiczne wire shape; redystrybuowalne reference JSON i przypadki testowe; porównania zachowania i własne testy regresji.
- **Zabronione:** kopiowanie kodu źródłowego LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI lub komercyjnych SDK; używanie zdekompilowanych zamkniętych plików binarnych jako źródła implementacji.
- **Zgodne, lecz nie przeniesione:** JSON Collaboration jest wyłącznie zgodnym podzbiorem publicznych operations TDF w zakresie rozszerzenia; nie jest portem kodu źródłowego Toolkit.

## 4. Implementacja standardów i znaki towarowe

- ODF, OpenFormula i OOXML są otwartymi lub publicznie udokumentowanymi formatami dokumentów; implementowanie readerów, writerów i validatorów zgodnie ze specyfikacjami jest normalnym działaniem na rzecz interoperacyjności.
- Określeń „OpenDocument”, „ODF” i „testy zgodności z LibreOffice” można używać opisowo.
- **Nie wolno** sugerować, że projekt jest oficjalnym projektem, certyfikowanym produktem lub produktem wspieranym przez OASIS, The Document Foundation, LibreOffice albo Apache.
- „Porównanie z ODF Toolkit” oznacza porównanie możliwości i dowodów testowych; **nie** oznacza oficjalnego portu ani wspólnego produktu.

## 5. Developer Certificate of Origin dla współtwórców (DCO)

Przesyłając kod lub obszerną dokumentację, współtwórca powinien móc oświadczyć, zgodnie z modelem Developer
Certificate of Origin:

1. że wkład jest jego autorstwa albo ma prawo przesłać go na licencji projektu;
2. że świadomie nie zawarł kodu źródłowego innych firm, którego nie ma prawa dalej rozpowszechniać;
3. że w przypadku implementacji na podstawie publicznych norm lub dokumentów przestrzegał indeksu źródeł procesu clean-room;
4. że podczas dodawania zależności innej firmy zaktualizował `THIRD-PARTY-NOTICES.md` i niezbędne metadane pakietu.

Zaleca się umieszczenie `Signed-off-by: Name <email>` w komunikacie commitu lub opisie PR. Zasady Git projektu
wymagają również podpisu GPG.

## 6. Lista należytej staranności dla podmiotów wdrażających

| Element | Zalecane działanie |
|---|---|
| Licencje | Przeczytaj `LICENSE` i `THIRD-PARTY-NOTICES.md`; włącz SBOM i skanowanie licencji do CI |
| Wersja | Bieżąca wersja to `0.x`; zobowiązania dotyczące zgodności opisano w `CHANGELOG` i [version-delivery.md](https://github.com/rubujo/OdfKit/blob/main/docs/version-delivery.md) |
| Granice funkcjonalne | Kieruj się [odf-format-support.md](https://github.com/rubujo/OdfKit/blob/main/docs/odf-format-support.md) i dowodami testowymi, a nie wyłącznie przekazem marketingowym |
| Cele poza zakresem | Zobacz [udx-non-goals.md](https://github.com/rubujo/OdfKit/blob/main/docs/udx-non-goals.md), w tym pełny mechanizm układu oraz interaktywne funkcje pakietu biurowego, takie jak pamięć podręczna tabel przestawnych i fragmentatory |
| Bezpieczeństwo | Używaj limitów zasobów `OdfLoadOptions`; dla niezaufanych danych wejściowych uruchamiaj `Validate` i oczyszczanie |
| Źródła | Przejrzyj `docs/provenance/`; w razie potrzeby porównaj katalogi wysokiego ryzyka z projektami upstream pod kątem podobieństw |
| Wsparcie | Projekt o otwartym kodzie źródłowym nie zapewnia SLA; systemy krytyczne powinny mieć nadmiarowość i własny plan utrzymania |

## 7. Zgłaszanie luk i problemów bezpieczeństwa

Projekt nie udostępnia obecnie publicznego issue tracker ani prywatnego kanału do zgłaszania problemów
bezpieczeństwa. Dopóki opiekunowie nie ogłoszą formalnego kanału, projekt nie deklaruje możliwości odbierania,
śledzenia ani obsługiwania zgłoszeń bezpieczeństwa zgodnie z poziomem usługi. Jeśli w przyszłości zostanie
otwarty publiczny tracker, nie należy publikować w nim pełnych szczegółów wykorzystania luki. Problemy
bezpieczeństwa należy rozpatrywać oddzielnie od spraw licencyjnych i naruszeń praw.

## 8. Powiązane dokumenty

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [Indeks źródeł procesu clean-room](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [Porównanie z ODF Toolkit](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [Zasady rozszerzeń zewnętrznych](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Reguły Corpus Manifest](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
