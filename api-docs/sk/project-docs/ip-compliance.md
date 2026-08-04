---
title: Duševné vlastníctvo a súlad
_lang: sk
translation_source: docs/ip-compliance.md
translation_source_sha256: 02ec7aa4649cae3c94cd515424f1c787d21909239c98c0fedffca85214a7eb6c
---

# Duševné vlastníctvo a súlad

> Informatívny preklad, nie právne poradenstvo. Rozhodujú pôvodné právne texty.

Dokument slúži na kontrolu súladu, obstarávanie a pre prispievateľov. Pozrite si
[register pôvodu](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md).

## 1. Zložený licenčný model
Pôvodný kód používa CC0-1.0 Universal; závislosti, schémy OASIS a fixtures si zachovávajú vlastné licencie
a OASIS Copyright. Distribúcia musí dodržiavať `LICENSE` aj
[THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md) a celý produkt
sa nesmie označovať za verejné vlastníctvo.

### Patentové a známkové hranice CC0

Podľa oddielu 4(a) CC0 1.0 sa patentové práva ani ochranné známky neudeľujú ani nevzdávajú. OdfKit
neposkytuje patentovú licenciu, záruku neporušenia, patentovú rešerš ani indemnity. Používatelia musia
vykonať vlastnú kontrolu. Rozhoduje [právny text CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

## 2. Práva a obsah AI
Verejný kód, dokumentácia, príklady a testy boli prevažne vytvorené alebo usporiadané pomocou AI. CC0
Affirmer a prispievatelia musia mať príslušné práva; ochrana sa líši podľa jurisdikcie. Projekt neposkytuje
komerčnú indemnity.

## 3. Clean-room a zakázané zdroje
Verejné normy OASIS/ISO/RFC/W3C, verejné formáty, redistribuovateľné fixtures a nezávislé testy sú povolené.
Kopírovanie LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI, komerčných SDK alebo dekompilovaných
uzavretých binárnych súborov je zakázané. JSON Collaboration je kompatibilný, nie port zdrojového kódu.

## 4. Normy a ochranné známky
Opisné odkazy sú povolené; nesmie sa naznačovať certifikácia alebo podpora OASIS, TDF, LibreOffice či Apache.

## 5. Developer Certificate of Origin (DCO)
Prispievatelia musia potvrdiť autorstvo alebo právo odoslať obsah, absenciu neredistribuovateľného kódu,
súlad s clean-room a aktualizované oznámenia. Podľa potreby použite `Signed-off-by: Name <email>`; commity
vyžadujú aj podpis GPG.

## 6. Kontrola používateľa
Skontrolujte licencie a SBOM, verziu `0.x`, funkčné a zdrojové limity, pôvod a podporu. SLA sa neposkytuje.

## 7. Hlásenie bezpečnosti
Momentálne neexistuje verejný tracker, súkromný kanál ani prísľub spracovania. Nezverejňujte úplné detaily
zneužitia a oddeľte bezpečnostné otázky od licenčných alebo právnych sporov.

## 8. Súvisiace dokumenty
Pozrite si [register clean-room](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md),
[politiku rozšírení](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md) a
[pravidlá korpusu](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md).
