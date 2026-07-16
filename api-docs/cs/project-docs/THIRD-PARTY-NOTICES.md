---
title: Oznámení třetích stran
_lang: cs
translation_source: THIRD-PARTY-NOTICES.md
translation_source_sha256: 107cfa6e885e599c7eb9ba318d6b91f3b755b99f52c18c2adc6d6314d02f4ad2
---

# Oznámení třetích stran

> Tento překlad je pouze informativní; názvy balíčků a licencí zůstávají v původním znění.

Projekt OdfKit je poskytován pod licencí [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Následující závislosti pro sestavení a běh si zachovávají vlastní licence.

| Balíček | Účel | Licence |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | Podpora algoritmů šifrování, hashování a odvozování klíčů | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | Vysoce výkonné nástroje pro paměť a vyrovnávací paměti | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | Zpracování digitálních podpisů XML | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | Zpracování podpisů PKCS7 / CMS | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | Import a export CSV pro soubory ODS | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | Modul převodu vzorců LaTeX ↔ MathML | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | Serializace JSON používaná jádrem a cílem netstandard2.0 rozšíření OdfKit.Extensions.Collaboration | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | Vektorizované numerické operace agregačních funkcí vzorců; pouze cíl net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | Výpočet kontrolního součtu CRC-32; pouze cíl net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | Podpora kompatibility platformy netstandard2.0, která doplňuje typy a API integrované v net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Podpora kompatibility netstandard2.0 pro rozlišení zdrojů registru Windows EUDC | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | Backend pro analýzu AST Markdown používaný v OdfKit.Extensions.Html | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Multiplatformní kreslení obrázků a sazba textu používané v OdfKit.Extensions.Imaging | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | Kreslení grafů v paměti a záložní vizualizace obrázků používané v OdfKit.Extensions.Imaging | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | Integrace, import a export formátů OOXML, například Excelu, používané v OdfKit.Extensions.Ooxml | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | Rozšíření pro zpracování, rozložení a kreslení PDF používaná v OdfKit.Extensions.Pdf | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | Propojení grafů RDF a dotazů SPARQL používané v OdfKit.Extensions.Rdf | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | Běhová vazba typu `dynamic` používaná cílem netstandard2.0 rozšíření OdfKit.Extensions.Ooxml | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | Ověření struktur XML ODF 1.1 / 1.2 / 1.3 / 1.4 a generování kódu; umístěno v tools/OdfSchemaGenerator/schemas/ | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

Při distribuci aplikace obsahující výše uvedené závislosti zachovejte oznámení o licenci a autorských právech vyžadovaná podmínkami jednotlivých balíčků.

Oznámení o autorských právech pro soubory schémat OASIS (Relax-NG Schemas):

## Závislosti testů WebFont

- Noto Sans Arabic / Devanagari / CJK — SIL Open Font License 1.1
- Noto Color Emoji — SIL Open Font License 1.1
- IPAmj Mincho — IPA Font License Agreement v1.0
- CNS 11643 fonts — Government Open Data License v1 / OFL-1.1

* Copyright (c) OASIS Open 2021. All Rights Reserved.
* Úplné zásady duševního vlastnictví naleznete v záhlaví jednotlivých souborů schema a v dokumentu [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php).
