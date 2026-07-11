---
title: Príručka používania, súladu, bezpečnosti a dôkazov
_lang: sk
---

# Používanie, súlad, bezpečnosť a dôkazy

## Rozsah dokumentácie API

Referencia API sa generuje z verejných assemblies `net10.0` a dokumentácie XML. Ručne písané základné API a verejné rozšírenia majú samostatné stránky. Rozsiahly povrch `OdfKit.DOM` generovaný zo schém naďalej riadia Public API baseline pre oba TFM a pokrytie Typed DOM. Súhrny členov sú momentálne dostupné v angličtine a tradičnej čínštine; tento slovenský vstup netvrdí, že všetky členy API sú preložené.

## Licencia a tvorba pomocou AI

Pôvodný kód OdfKit a pôvodná dokumentácia lokality používajú CC0-1.0 Universal. Balíky, schémy, nástroje a fixtures tretích strán si zachovávajú vlastné licencie. Verejný obsah projektu je napísaný, usporiadaný alebo vytvorený pomocou nástrojov AI. Lokalita neposkytuje právne poradenstvo, SLA ani komerčné odškodnenie. OdfKit nie je oficiálnym ani schváleným projektom organizácií OASIS, The Document Foundation, LibreOffice alebo Apache.

## Hranice bezpečnosti a interoperability

Pri nedôveryhodných súboroch ponechajte zapnuté limity prostriedkov readera a package a vykonajte vhodnú validáciu alebo sanitizáciu. Tieto opatrenia znižujú riziko, ale nezaručujú absolútnu bezpečnosť pred škodlivými dokumentmi. Platnosť schémy, round-trip alebo test s jednou verziou LibreOffice neznamenajú pixelovo zhodné výsledky vo všetkých kancelárskych balíkoch.

## Schopnosti a dôkazy

Tvrdenia sú rozdelené na `PackageFidelity`, `SemanticApiDepth` a `InteropEvidence`; jedna dimenzia nedokazuje ostatné. Zverejnené výsledky výkonu musia uvádzať commit, runtime, prostredie a reprodukovateľnú metódu. Výkonnostné rozpočty sú stále vo fáze zberu vzoriek.

- [Otvoriť referenciu API [en + zh-TW]](xref:OdfKit)
- [Index tvrdení a dôkazov](project-docs/evidence-index.md)
- [Bezpečnostné limity](project-docs/security-limits.md)
- [Duševné vlastníctvo a súlad](project-docs/ip-compliance.md)
- [Licencia](articles/license.md)
- [Oznámenia tretích strán](project-docs/THIRD-PARTY-NOTICES.md)
