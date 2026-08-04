---
title: Duševní vlastnictví a shoda
_lang: cs
translation_source: docs/ip-compliance.md
translation_source_sha256: 02ec7aa4649cae3c94cd515424f1c787d21909239c98c0fedffca85214a7eb6c
---

# Duševní vlastnictví a shoda (IP Compliance)

> Tento překlad je pouze informativní; nejde o právní radu ani náhradu konzultace práva příslušné jurisdikce.

Tento dokument je určen pro **kontrolu shody a nákupní due diligence uživatelů projektu** a pro
**přispěvatele**. Nejde o právní radu ani náhradu konzultace práva příslušné jurisdikce.

Související audity zdrojů naleznete v
[provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) a
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

## 1. Licenční model (kombinované licence)

| Rozsah | Licence | Popis |
|---|---|---|
| Původní kód projektu OdfKit | [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | Projekt se v maximální možné míře vzdává autorských práv; viz `LICENSE` v kořenovém adresáři |
| Závislosti pro sestavení a běh | Převážně MIT, BSD a podobné | **Použitím CC0 se nestávají volným dílem**; při dalším šíření je nutné zachovat příslušná oznámení a prohlášení o autorských právech |
| Schémata OASIS ODF RELAX NG | OASIS Copyright | Jsou v `tools/OdfSchemaGenerator/schemas/`; viz `THIRD-PARTY-NOTICES.md` |
| Testovací případy Corpus a Collaboration | Pole `license` každého případu | Viz `docs/corpus-manifest.md` a jednotlivé soubory `manifest.json` |

**Důležité:** při distribuci aplikace nebo balíčku obsahujícího OdfKit a jeho závislosti je nutné současně
splnit:

1. účinky projektové licence `LICENSE` (CC0) na původní kód; a
2. povinnosti licencí třetích stran uvedené v
   [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md).

Není dovoleno veřejně tvrdit, že „celý výsledný produkt je volným dílem“.

### Patentové a známkové hranice CC0

CC0 se vztahuje pouze na autorská a související práva. Podle oddílu 4(a) CC0 1.0 nejsou patentová práva
ani ochranné známky uděleny, vzdány ani jinak dotčeny. OdfKit proto neposkytuje výslovnou ani
implicitní patentovou licenci, záruku neporušení patentů, patentovou rešerši ani indemnity. Uživatelé musí
provést vlastní kontrolu podle jurisdikce, účelu a integrovaných technologií. V případě rozporu má přednost
[právní text CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

## 2. Držitelé práv a prohlášení o obsahu vytvořeném pomocí AI

- README uvádí, že velká část zveřejněného zdrojového kódu, dokumentace, příkladů a testů byla napsána, uspořádána nebo vytvořena pomocí nástrojů AI.
- Affirmer licence CC0 musí být oprávněn nakládat s právy, kterých se vzdává. Přispěvatel musí před odesláním ověřit, že má právo začlenit obsah pod licencí projektu; viz část o DCO níže.
- Posouzení autorských práv k obsahu vytvořenému výhradně strojem se v jednotlivých jurisdikcích liší. Uživatel, který potřebuje jasně určeného držitele práv a závazek odškodnění za porušení práv, by měl posoudit komerční alternativy nebo sjednat samostatnou smlouvu o podpoře. **Tento open-source projekt standardně neposkytuje komerční indemnity**.

## 3. Postup clean-room a zakázané zdroje

Autoritativní zdroje, povolené postupy a **zdroje, které se nesmějí kopírovat**, pro vysoce rizikové moduly,
například vyhodnocování OpenFormula, ověřování schema pattern, šifrování OpenPGP, JSON Collaboration a řízené
převody formátů, jsou uvedeny v
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

Shrnutí zásad:

- **Povoleno:** veřejné normy OASIS, ISO, RFC, W3C a dalších organizací; veřejné wire shape; redistribuovatelné reference JSON a testovací případy; porovnání chování a vlastní regresní testy.
- **Zakázáno:** kopírování zdrojového kódu LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI nebo komerčních SDK; použití dekompilovaných uzavřených binárních souborů jako zdroje implementace.
- **Kompatibilní, nikoli portované:** JSON Collaboration je pouze kompatibilní podmnožina veřejných operations TDF v rozsahu rozšíření; nejde o port zdrojového kódu Toolkit.

## 4. Implementace standardů a ochranné známky

- ODF, OpenFormula a OOXML jsou otevřené nebo veřejně zdokumentované formáty dokumentů; implementace readerů, writerů a validatorů podle specifikací je běžnou součástí interoperability.
- Výrazy „OpenDocument“, „ODF“ a „testy kompatibility s LibreOffice“ lze používat popisně.
- **Nesmí se** naznačovat, že projekt je oficiálním projektem, certifikovaným produktem nebo produktem podporovaným organizacemi OASIS, The Document Foundation, LibreOffice či Apache.
- „Porovnání s ODF Toolkit“ označuje porovnání schopností a testovacích důkazů; **nejde** o oficiální port ani společný produkt.

## 5. Developer Certificate of Origin pro přispěvatele (DCO)

Při odeslání kódu nebo rozsáhlejší dokumentace musí být přispěvatel schopen v duchu Developer Certificate of
Origin prohlásit:

1. že příspěvek vytvořil sám nebo že má právo jej odeslat pod licencí projektu;
2. že vědomě nezahrnul zdrojový kód třetí strany, který nemá právo dále šířit;
3. že při implementaci podle veřejných norem nebo dokumentů dodržel rejstřík zdrojů postupu clean-room;
4. že při přidání závislosti třetí strany aktualizoval `THIRD-PARTY-NOTICES.md` a potřebná metadata balíčku.

Doporučuje se uvést `Signed-off-by: Name <email>` ve zprávě commitu nebo v popisu PR. Pravidla projektu pro
Git vyžadují také podpis GPG.

## 6. Kontrolní seznam due diligence pro uživatele projektu

| Položka | Doporučený postup |
|---|---|
| Licence | Přečtěte `LICENSE` a `THIRD-PARTY-NOTICES.md`; zahrňte SBOM a kontrolu licencí do CI |
| Verze | Aktuální verze je `0.x`; závazky kompatibility jsou popsány v `CHANGELOG` a [version-delivery.md](https://github.com/rubujo/OdfKit/blob/main/docs/version-delivery.md) |
| Funkční hranice | Řiďte se [odf-format-support.md](https://github.com/rubujo/OdfKit/blob/main/docs/odf-format-support.md) a testovacími důkazy, nikoli pouze marketingovými výroky |
| Cíle mimo rozsah | Viz [udx-non-goals.md](https://github.com/rubujo/OdfKit/blob/main/docs/udx-non-goals.md), včetně úplného modulu rozložení a interaktivních funkcí kancelářského balíku, jako jsou pivot cache a slicery |
| Zabezpečení | Používejte limity prostředků `OdfLoadOptions`; u nedůvěryhodných vstupů spusťte `Validate` a čištění |
| Zdroje | Projděte `docs/provenance/`; podle potřeby porovnejte vysoce rizikové adresáře s upstream projekty a vyhledejte podobnosti |
| Podpora | Open-source projekt neposkytuje SLA; kritické systémy musí mít redundanci a vlastní plán údržby |

## 7. Hlášení zranitelností a bezpečnostních problémů

Projekt v současnosti neposkytuje veřejný issue tracker ani soukromý kanál pro hlášení bezpečnostních
problémů. Dokud správci neoznámí formální kanál, projekt netvrdí, že dokáže přijímat, sledovat nebo zpracovávat
bezpečnostní hlášení podle úrovně služby. Pokud bude v budoucnu otevřen veřejný tracker, neměly by se v něm
zveřejňovat úplné podrobnosti zneužití. Bezpečnostní problémy je nutné řešit odděleně od licenčních problémů
a porušení práv.

## 8. Související dokumenty

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [Rejstřík zdrojů postupu clean-room](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [Porovnání s ODF Toolkit](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [Zásady externích rozšíření](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Pravidla Corpus Manifest](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
