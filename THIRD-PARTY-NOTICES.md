# Third-Party Notices

OdfKit 專案採用 [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/deed.zh_TW) 授權。下列建置與執行期相依套件維持各自授權。

| 套件 (Package) | 用途 (Purpose) | 授權 (License) |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | 提供加密、雜湊與金鑰衍生演算法支援 | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | 高效能記憶體與緩衝區工具 | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | XML 數位簽章處理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | PKCS7 / CMS 簽章處理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | 用於 ODS 檔案的 CSV 匯入與匯出 | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [System.Memory 等相容性支援](https://github.com/dotnet/runtime) | netstandard2.0 平台相容性支援 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [AngleSharp](https://github.com/AngleSharp/AngleSharp) | 用於 HTML 解析與轉換擴充（於 OdfKit.Extensions.Html 中使用） | [MIT](https://github.com/AngleSharp/AngleSharp/blob/master/LICENSE) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | 跨平台圖像繪製與文字排版支援（於 OdfKit.Extensions.Imaging 中使用） | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | 記憶體內圖表繪製與 fallback 影像視覺化（於 OdfKit.Extensions.Imaging 中使用） | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | 用於 OOXML 格式（如 Excel）之整合與匯入匯出（於 OdfKit.Extensions.Ooxml 中使用） | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | PDF 相關處理、排版與繪製擴充（於 OdfKit.Extensions.Pdf 中使用） | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | RDF 圖形與 SPARQL 查詢橋接（於 OdfKit.Extensions.Rdf 中使用） | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | ODF 1.1 / 1.2 / 1.3 / 1.4 XML 結構驗證與代碼生成（置於 tools/OdfSchemaGenerator/schemas/） | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

分發包含上述相依套件的應用程式時，請依各套件之授權條款，保留其必要的授權與著作權聲明。

關於 OASIS 結構描述檔案（Relax-NG Schemas）之著作權聲明：
* Copyright (c) OASIS Open 2021. All Rights Reserved.
* 詳細之智慧財產權政策請參見各 schema 檔案標頭以及 [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php)。
