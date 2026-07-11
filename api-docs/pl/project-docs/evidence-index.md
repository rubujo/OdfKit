---
title: Indeks deklaracji możliwości i dowodów
_lang: pl
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Indeks deklaracji możliwości i dowodów

> To tłumaczenie ma charakter informacyjny; identyfikatory i wartości przeznaczone do odczytu maszynowego nie są tłumaczone.

Ten indeks dzieli możliwości na trzy wymiary, których nie można wzajemnie z siebie wywodzić. Źródłem do
odczytu maszynowego jest [`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json). CI
sprawdza identyfikatory deklaracji, ścieżki dowodów i opisy ograniczeń.

| Deklaracja | Format | Wymiar | Poziom | Podsumowanie ograniczenia |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Odczyt i zapis pakietu w obie strony nie oznacza ponownego obliczania formuł ani pełnej semantyki arkusza kalkulacyjnego. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Odczytuje zapisane wartości i formuły, ale nie oblicza ich ponownie. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Nie udostępnia mechanizmu układu ani renderowania. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP jest ładowany jako DOM i pakiet; nie deklarujemy strumieniowego interfejsu API slajdów. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Nie implementuje układu SmartArt ani renderowania na poziomie pikseli. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Testy z określoną wersją LibreOffice nie gwarantują zgodności pikseli we wszystkich pakietach biurowych. |

`PackageFidelity` odpowiada jedynie na pytanie, czy pakiet można bezpiecznie przetworzyć. `SemanticApiDepth`
określa, jaką część semantyki dokumentu interfejs API potrafi zrozumieć i zmienić. `InteropEvidence` wskazuje
faktycznie przetestowane oprogramowanie zewnętrzne i jego wersje. Najwyższy poziom w jednym wymiarze nie
zastępuje pozostałych dwóch wymiarów.

Jedynym źródłem prawdy dla grup semantycznych, operacji CRUD, części standardów, implementacji, testów,
dowodów interoperacyjności i ograniczeń czterech głównych formatów jest
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json).
`eng/Test-SemanticCoverage.ps1` blokuje w CI niepełne deklaracje. Granice źródeł zastosowanego procesu
clean-room opisano w
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md).
