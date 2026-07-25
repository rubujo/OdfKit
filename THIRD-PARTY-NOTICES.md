# Third-Party Notices

> 內容語系：正體中文（臺灣）（`zh-TW`）；套件與授權名稱保留英文原文。

OdfKit 專案採用 [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/deed.zh_TW) 授權。下列建置與執行期相依套件維持各自授權。

| 套件 (Package) | 用途 (Purpose) | 授權 (License) |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | 提供加密、雜湊與金鑰衍生演算法支援 | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | 高效能記憶體與緩衝區工具 | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | XML 數位簽章處理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | PKCS7 / CMS 簽章處理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | 用於 ODS 檔案的 CSV 匯入與匯出 | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | LaTeX ↔ MathML 公式轉換引擎 | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | JSON 序列化（核心套件與 OdfKit.Extensions.Collaboration 之 netstandard2.0 目標均使用） | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | 公式聚合函式的向量化數值運算（僅 net10.0 目標使用） | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | CRC-32 校驗碼計算（僅 net10.0 目標使用） | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | netstandard2.0 平台相容性支援（補齊 net10.0 才內建的型別與 API） | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Windows EUDC 登錄來源解析的 netstandard2.0 相容性支援 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Extensions.Hosting.WindowsServices](https://github.com/dotnet/runtime) 與 Microsoft.Extensions Hosting／Logging 相依 | NativeAOT WebFont Sidecar 的 Windows Service Control Manager 生命週期與 Event Log 整合 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | Markdown AST 解析 backend（於 OdfKit.Extensions.Html 中使用） | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | 跨平台圖像繪製與文字排版支援（於 OdfKit.Extensions.Imaging 中使用） | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | 記憶體內圖表繪製與 fallback 影像視覺化（於 OdfKit.Extensions.Imaging 中使用） | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | 用於 OOXML 格式（如 Excel）之整合與匯入匯出（於 OdfKit.Extensions.Ooxml 中使用） | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | PDF 相關處理、排版與繪製擴充（於 OdfKit.Extensions.Pdf 中使用） | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | RDF 圖形與 SPARQL 查詢橋接（於 OdfKit.Extensions.Rdf 中使用） | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | 提供 `dynamic` 型別執行期繫結支援（於 OdfKit.Extensions.Ooxml 之 netstandard2.0 目標使用） | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | ODF 1.1 / 1.2 / 1.3 / 1.4 XML 結構驗證與程式碼產生（置於 tools/OdfSchemaGenerator/schemas/） | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |
| [Noto Sans TC](https://github.com/notofonts/noto-cjk) | WebFont 最小測試使用的繁體中文字型（僅測試時下載，不隨套件散布） | [SIL Open Font License 1.1](https://github.com/notofonts/noto-cjk/blob/main/LICENSE) |
| [Noto Sans Arabic／Devanagari](https://github.com/google/fonts) 與 [Noto Sans CJK](https://github.com/notofonts/noto-cjk) | 多國文字、複雜塑形、TTC face 與 OpenType CFF smoke（僅測試時下載，不隨套件散布） | [SIL Open Font License 1.1](https://openfontlicense.org/) |
| [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) | Color／bitmap font 明確拒絕 smoke（僅測試時下載，不隨套件散布） | [SIL Open Font License 1.1](https://github.com/googlefonts/noto-emoji/blob/main/LICENSE) |
| [IPAmj Mincho](https://moji.or.jp/mojikiban/font/) | 日本文字資訊基盤 IVS smoke（僅測試時下載，不隨套件散布） | [IPA Font License Agreement v1.0](https://moji.or.jp/ipafont/license/) |
| [全字庫宋體](https://www.cns11643.gov.tw/pageView.jsp?ID=59) | CNS 11643 Plane 15 PUA smoke（僅測試時下載，不隨套件散布） | 政府資料開放授權條款第 1 版／OFL-1.1（依資料集聲明） |

分發包含上述相依套件的應用程式時，請依各套件之授權條款，保留其必要的授權與著作權聲明。

關於 OASIS 結構描述檔案（Relax-NG Schemas）之著作權聲明：
* Copyright (c) OASIS Open 2021. All Rights Reserved.
* 詳細之智慧財產權政策請參見各 schema 檔案標頭以及 [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php)。
