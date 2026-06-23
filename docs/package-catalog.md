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
| `OdfKit.Extensions.Rdf` | 中介資料擴充 | `net10.0;netstandard2.0` | `manifest.rdf`、RDF 圖形與 SPARQL 查詢橋接 | 依賴 dotNetRdf |
| `OdfKit.Extensions.Collaboration` | 協作擴充 | `net10.0;netstandard2.0` | ODF Toolkit 相容 JSON operations 匯出 | 適合協作編輯流程整合 |

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

## 3. 依情境選型

| 如果您的需求是… | 建議組合 |
|------------------|----------|
| 純 ODF 建檔、讀寫與驗證 | `OdfKit` |
| ODF 匯出成 PDF 或 HTML | `OdfKit` + `OdfKit.Extensions.Pdf` / `OdfKit.Extensions.Html` |
| 產生圖片預覽或圖表 fallback | `OdfKit` + `OdfKit.Extensions.Imaging` |
| 與 Office 生態互通 | `OdfKit` + `OdfKit.Extensions.Ooxml` |
| 必須依賴 LibreOffice 視覺後端 | `OdfKit` + `OdfKit.Extensions.Rendering` |
| 要保留或查詢 RDF 中介資料 | `OdfKit` + `OdfKit.Extensions.Rdf` |
| 協作編輯或操作序列輸出 | `OdfKit` + `OdfKit.Extensions.Collaboration` |
| 在 CI / 批次流程中做驗證或轉檔 | `OdfKit.Cli` |

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
- [版本與交付資訊](version-delivery.md)
