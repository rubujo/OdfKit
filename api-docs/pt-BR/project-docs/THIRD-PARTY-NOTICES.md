---
title: Avisos de terceiros
_lang: pt-BR
translation_source: THIRD-PARTY-NOTICES.md
translation_source_sha256: 1f6420f237bd28ad1fd71200b41661ef336448631262196c5601841160e7b2ce
---

# Avisos de terceiros

> Tradução informativa; os nomes dos pacotes e das licenças são mantidos na forma original.

O projeto OdfKit é disponibilizado sob a licença [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). As dependências de compilação e execução abaixo mantêm suas próprias licenças.

| Pacote | Finalidade | Licença |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | Suporte a algoritmos de criptografia, hash e derivação de chaves | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | Ferramentas de memória e buffers de alto desempenho | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | Processamento de assinaturas digitais XML | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | Processamento de assinaturas PKCS7 / CMS | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | Importação e exportação de CSV para arquivos ODS | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | Mecanismo de conversão de fórmulas LaTeX ↔ MathML | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | Serialização JSON usada pelo pacote principal e pelo destino netstandard2.0 de OdfKit.Extensions.Collaboration | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | Operações numéricas vetorizadas para funções de agregação de fórmulas; somente no destino net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | Cálculo da soma de verificação CRC-32; somente no destino net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | Compatibilidade com a plataforma netstandard2.0 para complementar os tipos e as APIs integrados ao net10.0 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Compatibilidade com netstandard2.0 para resolver origens do Registro do Windows EUDC | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | Backend de análise de AST Markdown usado em OdfKit.Extensions.Html | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Desenho de imagens e composição de texto multiplataforma usados em OdfKit.Extensions.Imaging | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | Desenho de gráficos em memória e visualização alternativa de imagens usados em OdfKit.Extensions.Imaging | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | Integração, importação e exportação de formatos OOXML, como Excel, usadas em OdfKit.Extensions.Ooxml | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | Extensões de processamento, layout e desenho de PDF usadas em OdfKit.Extensions.Pdf | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | Ponte entre grafos RDF e consultas SPARQL usada em OdfKit.Extensions.Rdf | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | Vinculação em tempo de execução do tipo `dynamic` usada pelo destino netstandard2.0 de OdfKit.Extensions.Ooxml | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | Validação de estruturas XML ODF 1.1 / 1.2 / 1.3 / 1.4 e geração de código; localizado em tools/OdfSchemaGenerator/schemas/ | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

Ao distribuir um aplicativo que contenha as dependências acima, preserve os avisos de licença e de direitos autorais exigidos pelos termos de cada pacote.

Aviso de direitos autorais dos arquivos de esquema da OASIS (Relax-NG Schemas):

## Dependências dos testes WebFont

- Noto Sans Arabic / Devanagari / CJK — SIL Open Font License 1.1
- Noto Color Emoji — SIL Open Font License 1.1
- IPAmj Mincho — IPA Font License Agreement v1.0
- CNS 11643 fonts — Government Open Data License v1 / OFL-1.1

* Copyright (c) OASIS Open 2021. All Rights Reserved.
* Consulte os cabeçalhos de cada arquivo schema e a [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php) para obter a política completa de propriedade intelectual.
