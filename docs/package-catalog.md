# OdfKit 套件目錄與選型指南

本文件以「套件目錄 + 使用情境」格式整理 OdfKit 可交付的
核心套件、擴充套件與開發工具。

## 1. 可封裝套件

| 套件 ID | 類型 | 目標框架 | 主要用途 | 注意事項 |
|---------|------|----------|----------|----------|
| `OdfKit` | 核心 SDK | `net10.0;netstandard2.0` | ODF 文件建立、載入、保存、驗證與 round-trip | 所有擴充套件的共同基礎 |
| `OdfKit.Extensions.Html` | 匯出擴充 | `net10.0;netstandard2.0` | HTML / Markdown / RTF 匯出 | 適合 Web 預覽或內容轉出 |
| `OdfKit.Extensions.Imaging` | 渲染擴充 | `net10.0;netstandard2.0` | 影像渲染與圖表 fallback 視覺化 | 依賴 SkiaSharp / HarfBuzzSharp |
| `OdfKit.Extensions.Ooxml` | 互通擴充 | `net10.0;netstandard2.0` | DOCX / XLSX 轉換與互通 | 依賴 ClosedXML 與 Open XML SDK |
| `OdfKit.Extensions.Pdf` | 匯出擴充 | `net10.0;netstandard2.0` | PDF 匯出 | 依賴 PDFsharp-MigraDoc |
| `OdfKit.Extensions.Rendering` | 後端渲染擴充 | `net10.0;netstandard2.0` | 以 LibreOffice 後端產生視覺輸出 | 需外部 LibreOffice 或相容程序 |
| `OdfKit.Extensions.Rdf` | 中繼資料擴充 | `net10.0;netstandard2.0` | `manifest.rdf`、RDF 圖形與 SPARQL 查詢橋接 | 依賴 dotNetRdf |
| `OdfKit.Extensions.Collaboration` | 協作擴充 | `net10.0;netstandard2.0` | ODF Toolkit 相容 JSON operations 匯出 | 適合協作編輯流程整合 |
| `OdfKit.WebFonts.Abstractions` | WebFont 契約 | `net10.0;netstandard2.0` | Unicode sequence、manifest、Profile 與子集引擎契約 | 不相依 Web 或 ORM |
| `OdfKit.WebFonts.Encoding.Legacy` | 編碼擴充 | `net10.0;netstandard2.0` | 嚴格 Big5、明確 Big5E 與 PUA mapping | 不猜測來源 code page |
| `OdfKit.WebFonts.Data.SqlServer` | 資料存取橋接 | `net10.0;netstandard2.0` | 有界讀取 SQL Unicode／legacy bytes | 可搭配 ADO.NET、Dapper 或 ORM |
| `OdfKit.WebFonts.OpenType` | 字型引擎 | `net10.0;netstandard2.0` | 純 .NET TTF／OTF／TTC／OTC／TTE／WOFF 輸入、net10 standalone WOFF2 null／`glyf`／`loca`／`hmtx` transform、net10 WOFF2 collection 指定 face、standalone／OTC face 的 CID-keyed 與名稱式靜態 CFF 1.0、含 VariationStore 的 CFF2 variable、不含 VariationStore 的非變動 CFF2、color correctness-first 子集化；輸出 TTF／OTF／WOFF、net10 WOFF2 | Variable／CFF／CFF2／color 與 WOFF2 collection 輸入能力為 experimental；名稱式 CFF 的 `seac`、缺少 VariationStore 卻使用 `vsindex`／`blend` 的 CFF2 與直接 collection 輸出明確拒絕 |
| `OdfKit.WebFonts.Worker` | 背景工作 | `net10.0` | 有界 queue、timeout 與 single-flight | 不提供公開同步 generation endpoint |
| `OdfKit.WebFonts.Profiles` | Profile 擴充 | `net10.0;netstandard2.0` | 有界、版本化 JSON mapping | PUA 必須明確選擇 Profile |
| `OdfKit.WebFonts.Hosting.AspNetCore` | Web 託管 | `net10.0` | 須經授權及限流的動態產生、唯讀 hash 資產、CSP/CDN URL、CORS 與 cache headers | 大規模部署應置於 CDN 後方 |
| `OdfKit.WebFonts.Hosting.SystemWeb` | Web Forms 託管 | `net48` | API key、allowlist 與有界並行的動態產生、不可變資產、靜態 fallback 及 HTML helper | request-time 只支援 TTF／WOFF；多節點 generation 須外部協調 |
| `OdfKit.WebFonts.Windows` | Windows EUDC 來源 | `net10.0;netstandard2.0` | 唯讀解析目前使用者 EUDC 登錄關聯與 `.tte`／`.ttf` 路徑；相容 ASP.NET Core 與 net48 consumer | 不寫登錄、不從 HTTP request 接受來源 |
| `OdfKit.Extensions.Html.WebFonts` | HTML 整合 | `net10.0;netstandard2.0` | ODF 文字需求收集與外部 CSS link | 不掃描瀏覽器 DOM |

## 2. 非封裝工具與工程元件

| 專案 | 類型 | 用途 |
|------|------|------|
| `OdfKit.Cli` | CLI 工具 | 驗證、資訊查詢、sanitize、flat XML / CSV 轉換 |
| `samples/Sample.cs` | 範例 | 單檔 Script 展示主要功能 |
| `OdfCorpusGenerator` | 開發工具 | 產生 corpus 與測試資料 |
| `OdfSchemaGenerator` | 開發工具 | schema 衍生與 DOM wrapper 產生 |
| `OdfKit.TrimSmoke` | 開發工具 | trimming / Native AOT API 根煙霧測試 |
| `OdfKit.Tests` | 測試套件 | 單元、整合、互通與 packaging 驗證 |
| `OdfKit.Benchmarks` | 基準測試 | 效能與資源使用量量測 |
| `OdfKit.WebFonts.Build` | .NET Tool／MSBuild | 自動掃描受信任內容並產生 content-addressed WebFont 資產 |

## 3. 依情境選型

| 如果您的需求是… | 建議組合 |
|------------------|----------|
| 純 ODF 建檔、讀寫與驗證 | `OdfKit` |
| ODF 匯出成 PDF 或 HTML | `OdfKit` + `OdfKit.Extensions.Pdf` / `OdfKit.Extensions.Html` |
| 產生圖片預覽或圖表 fallback | `OdfKit` + `OdfKit.Extensions.Imaging` |
| 與 Office 生態互通 | `OdfKit` + `OdfKit.Extensions.Ooxml` |
| 必須依賴 LibreOffice 視覺後端 | `OdfKit` + `OdfKit.Extensions.Rendering` |
| 要保留或查詢 RDF 中繼資料 | `OdfKit` + `OdfKit.Extensions.Rdf` |
| 協作編輯或操作序列輸出 | `OdfKit` + `OdfKit.Extensions.Collaboration` |
| 在 CI / 批次流程中做驗證或轉檔 | `OdfKit.Cli` |
| 將資料庫查詢（含 Entity Framework Core）或任意物件序列匯出成 ODS，或反向以 `DbDataReader` 邊界串流灌入 `SqlBulkCopy` 等 bulk copy API | `OdfKit`（核心即可，透過 `ObjectDataReader<T>` 與 `OdsStreamWriter.WriteDataAsync<T>`，無需額外擴充套件） |
| 在 ASP.NET Core／Web Forms 顯示多國罕用字、IVS 或機構 PUA | `OdfKit.WebFonts.Build` + `OdfKit.WebFonts.Hosting.AspNetCore`／`OdfKit.WebFonts.Hosting.SystemWeb`；Big5／Big5E 或 SQL bytes 再加入對應 Encoding／Data 套件 |

## 4. 選型原則

1. 若需求只涵蓋 ODF 建立、載入、保存、驗證，先從 `OdfKit` 開始。
2. 只有在需求涉及匯出、渲染、互通或協作時，再加入對應擴充套件。
3. 若部署環境禁止外部程序，避免將 `OdfKit.Extensions.Rendering` 視為核心依賴。
4. 若需要最穩定的相依面，優先依據
   [NuGet 相容矩陣](nuget-compatibility-matrix.md) 鎖定固定版本與目標框架。

## 5. 相關文件

- [快速開始](getting-started.md)
- [NuGet 相容矩陣](nuget-compatibility-matrix.md)
- [tools/README.md](../tools/README.md)
- [Rendering 後端部署](rendering-backend-deployment.md)
- [WebFont 多國罕用字套件](webfonts.md)
- [WebFont 純 .NET 架構契約](webfont-managed-architecture.md)
- [版本與交付資訊](version-delivery.md)
