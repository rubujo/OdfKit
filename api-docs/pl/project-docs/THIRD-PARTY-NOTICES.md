---
title: Informacje o składnikach innych firm
_lang: pl
translation_source: THIRD-PARTY-NOTICES.md
translation_source_sha256: 107cfa6e885e599c7eb9ba318d6b91f3b755b99f52c18c2adc6d6314d02f4ad2
---

# Informacje o składnikach innych firm

> To tłumaczenie ma charakter informacyjny; nazwy pakietów i licencji zachowano w oryginalnej formie.

Projekt OdfKit jest udostępniany na podstawie licencji [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Poniższe zależności czasu kompilacji i wykonania zachowują własne licencje.

| Pakiet | Zastosowanie | Licencja |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | Obsługa algorytmów szyfrowania, skrótu i wyprowadzania kluczy | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | Wysokowydajne narzędzia do obsługi pamięci i buforów | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | Przetwarzanie podpisów cyfrowych XML | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | Przetwarzanie podpisów PKCS7 / CMS | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | Import i eksport CSV dla plików ODS | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | Mechanizm konwersji formuł LaTeX ↔ MathML | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | Serializacja JSON używana przez pakiet główny i element docelowy netstandard2.0 rozszerzenia OdfKit.Extensions.Collaboration | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | Wektoryzowane operacje numeryczne funkcji agregujących formuły; tylko element docelowy net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | Obliczanie sumy kontrolnej CRC-32; tylko element docelowy net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | Obsługa zgodności platformy netstandard2.0 uzupełniająca typy i API wbudowane w net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Obsługa zgodności netstandard2.0 do rozpoznawania źródeł rejestru Windows EUDC | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | Mechanizm analizy AST Markdown używany w OdfKit.Extensions.Html | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Wieloplatformowe rysowanie obrazów i skład tekstu używane w OdfKit.Extensions.Imaging | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | Rysowanie wykresów w pamięci i rezerwowa wizualizacja obrazów używane w OdfKit.Extensions.Imaging | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | Integracja, import i eksport formatów OOXML, takich jak Excel, używane w OdfKit.Extensions.Ooxml | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | Rozszerzenia przetwarzania, układu i rysowania PDF używane w OdfKit.Extensions.Pdf | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | Most grafów RDF i zapytań SPARQL używany w OdfKit.Extensions.Rdf | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | Powiązanie typu `dynamic` w czasie wykonywania używane przez element docelowy netstandard2.0 rozszerzenia OdfKit.Extensions.Ooxml | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | Walidacja struktur XML ODF 1.1 / 1.2 / 1.3 / 1.4 i generowanie kodu; znajduje się w tools/OdfSchemaGenerator/schemas/ | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

Podczas rozpowszechniania aplikacji zawierającej powyższe zależności zachowaj informacje o licencji i prawach autorskich wymagane przez warunki poszczególnych pakietów.

Informacja o prawach autorskich do plików schematów OASIS (Relax-NG Schemas):

## Zależności testów WebFont

- Noto Sans Arabic / Devanagari / CJK — SIL Open Font License 1.1
- Noto Color Emoji — SIL Open Font License 1.1
- IPAmj Mincho — IPA Font License Agreement v1.0
- CNS 11643 fonts — Government Open Data License v1 / OFL-1.1

* Copyright (c) OASIS Open 2021. All Rights Reserved.
* Pełne zasady własności intelektualnej opisano w nagłówkach poszczególnych plików schema oraz w dokumencie [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php).
