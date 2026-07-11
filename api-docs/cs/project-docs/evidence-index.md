---
title: Rejstřík tvrzení o schopnostech a důkazů
_lang: cs
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Rejstřík tvrzení o schopnostech a důkazů

> Tento překlad je pouze informativní; strojově čitelné identifikátory a hodnoty se nepřekládají.

Tento rejstřík rozděluje schopnosti do tří rozměrů, které nelze navzájem odvozovat. Strojově čitelným zdrojem
je [`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json). CI kontroluje identifikátory
tvrzení, cesty k důkazům a popisy omezení.

| Tvrzení | Formát | Rozměr | Úroveň | Shrnutí omezení |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Zpětné načtení a zápis balíčku neznamená přepočet vzorců ani úplnou sémantiku tabulkového procesoru. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Čte uložené hodnoty a vzorce, ale vzorce nepřepočítává. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Neposkytuje modul rozložení ani vykreslování. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP se načítá jako DOM a balíček; netvrdíme, že existuje proudové API pro snímky. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Neimplementuje rozložení SmartArt ani vykreslování na úrovni pixelů. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Testování s konkrétní verzí LibreOffice nezaručuje shodu pixelů ve všech kancelářských sadách. |

`PackageFidelity` odpovídá pouze na otázku, zda lze balíček bezpečně zpracovat. `SemanticApiDepth` určuje,
jakou část sémantiky dokumentu dokáže API pochopit a měnit. `InteropEvidence` uvádí skutečně testovaný externí
software a jeho verze. Nejvyšší úroveň v jednom rozměru nenahrazuje zbývající dva rozměry.

Jediným zdrojem pravdy pro sémantické skupiny, operace CRUD, části standardů, implementaci, testy, důkazy
interoperability a omezení čtyř hlavních formátů je
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json).
`eng/Test-SemanticCoverage.ps1` v CI blokuje neúplná tvrzení. Hranice zdrojů použitého postupu clean-room
jsou popsány v
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md).
