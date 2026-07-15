---
title: Avisos de terceros
_lang: es
translation_source: THIRD-PARTY-NOTICES.md
translation_source_sha256: 8fd1b78ed38af561f353eb48b1671c0dc7331f4b2912c678fdfb36a900bb3f20
---

# Avisos de terceros

> Traducción informativa; los nombres de los paquetes y de las licencias se conservan en su forma original.

El proyecto OdfKit se publica bajo [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Las siguientes dependencias de compilación y ejecución conservan sus propias licencias.

| Paquete | Finalidad | Licencia |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | Compatibilidad con algoritmos de cifrado, hash y derivación de claves | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | Herramientas de memoria y búferes de alto rendimiento | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | Procesamiento de firmas digitales XML | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | Procesamiento de firmas PKCS7 / CMS | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | Importación y exportación de CSV para archivos ODS | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | Motor de conversión de fórmulas LaTeX ↔ MathML | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | Serialización JSON usada por el paquete principal y el destino netstandard2.0 de OdfKit.Extensions.Collaboration | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | Operaciones numéricas vectorizadas para funciones de agregación de fórmulas; solo para el destino net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | Cálculo de sumas de comprobación CRC-32; solo para el destino net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | Compatibilidad con la plataforma netstandard2.0 para completar los tipos y las API integrados en net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | Motor de análisis de AST de Markdown usado por OdfKit.Extensions.Html | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Dibujo de imágenes y composición tipográfica multiplataforma usados por OdfKit.Extensions.Imaging | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | Dibujo de gráficos en memoria y visualización de imágenes de reserva usados por OdfKit.Extensions.Imaging | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | Integración, importación y exportación de formatos OOXML como Excel, usadas por OdfKit.Extensions.Ooxml | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | Extensiones de procesamiento, maquetación y dibujo de PDF usadas por OdfKit.Extensions.Pdf | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | Puente de gráficos RDF y consultas SPARQL usado por OdfKit.Extensions.Rdf | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | Enlace en tiempo de ejecución del tipo `dynamic` usado por el destino netstandard2.0 de OdfKit.Extensions.Ooxml | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | Validación de estructuras XML ODF 1.1 / 1.2 / 1.3 / 1.4 y generación de código; se encuentra en tools/OdfSchemaGenerator/schemas/ | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

Al distribuir una aplicación que incluya estas dependencias, conserve los avisos de licencia y derechos de autor exigidos por las condiciones de cada paquete.

Aviso de derechos de autor para los archivos de esquema OASIS (Relax-NG Schemas):

## Dependencias de pruebas WebFont

- FontTools / Brotli — MIT / MIT
- Noto Sans Arabic / Devanagari / CJK — SIL Open Font License 1.1
- IPAmj Mincho — IPA Font License Agreement v1.0
- CNS 11643 fonts — Government Open Data License v1 / OFL-1.1

* Copyright (c) OASIS Open 2021. All Rights Reserved.
* Consulte las cabeceras de cada archivo schema y la [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php) para obtener la política de propiedad intelectual completa.
