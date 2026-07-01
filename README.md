# OdfKit

OdfKit 是純 managed C# / .NET 的 ODF (OpenDocument Format) 文件處理
SDK。它讓應用程式在不啟動 LibreOffice、OpenOffice、UNO、Microsoft
Office 或 Java 的情況下，建立、載入、保存、驗證並保留 ODF 文件內容。

目前預設新建文件為 ODF 1.4。核心程式庫與可封裝擴充套件採
`net10.0` 與 `netstandard2.0` 雙目標框架；CLI 與測試專案目前覆蓋
`net10.0` 與 `net8.0`。

> **AI 產製聲明**
> 本專案目前公開之原始碼、文件、範例、測試與相關內容，均為使用
> AI 工具撰寫、整理或產製。

## SDK 概觀

| 面向 | 說明 |
|------|------|
| 核心能力 | 支援 24 種主要 ODF extension 的格式偵測、最小建立、載入、保存、驗證與 round-trip |
| 高階 API | ODT、ODS、ODP、ODG 提供常用建立與編輯 API；ODC、ODF、ODI、ODB 提供可用的高階入口 |
| 資料庫互通 | 透過 `ObjectDataReader<T>` 將任意物件序列或 Entity Framework Core 查詢投影以 `DbDataReader` 邊界串流匯出／匯入，不綁定特定 ORM 或資料庫 provider |
| 相容性 | Package 與 DOM round-trip 會保留未知 package entries、foreign XML、processing instructions、comments 與 prefix |
| 驗證 | 內建 package / flat XML 檢查、ODF 1.4 schema metadata、profile rules、positive / negative corpus 測試 |
| 在地化 | 透過 `OdfLocalizer` 提供多語系訊息與文化回退機制 |
| 工具鏈 | CLI 提供 `validate`、`validate-corpus`、`info`、`metadata`、`sanitize`、`convert-flat`、`convert-csv` 與 `pack` |

完整支援範圍請見 [ODF 格式支援矩陣](docs/odf-format-support.md)、
[ODF Profile 來源](docs/odf-profile-sources.md)、
[LibreOffice 互通矩陣](docs/libreoffice-interop-matrix.md) 與
[i18n 與在地化](docs/i18n-localization.md)。

## 套件組合

| 類別 | 內容 | 典型用途 |
|------|------|----------|
| 核心 SDK | `OdfKit` | ODF 文件建立、載入、保存、驗證、round-trip |
| 匯出與轉換擴充 | `OdfKit.Extensions.Html`、`OdfKit.Extensions.Pdf`、`OdfKit.Extensions.Ooxml` | HTML / Markdown / RTF、PDF、DOCX / XLSX 互通 |
| 渲染與資料擴充 | `OdfKit.Extensions.Imaging`、`OdfKit.Extensions.Rendering`、`OdfKit.Extensions.Rdf`、`OdfKit.Extensions.Collaboration` | 影像渲染、LibreOffice 後端渲染、RDF、協作操作匯出 |
| 工具 | `OdfKit.Cli`、samples、測試與 corpus 工具 | 自動化驗證、批次轉檔、範例與工程驗證 |

套件挑選與相依說明請見 [套件目錄與選型指南](docs/package-catalog.md)；
工具與範例導覽請見 [tools/README.md](tools/README.md) 與
[samples/README.md](samples/README.md)。

## 安裝與快速開始

### 1. 選擇導入方式

**原始碼整合**（主要方式）：

```powershell
dotnet build
dotnet test
```

**GitHub Release 套件整合**（本機 NuGet feed）：

```powershell
dotnet nuget add source C:\path\to\release-assets --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

發佈與封裝流程請見 [GitHub Release 發佈指南](docs/github-release-publishing.md)
與 [NuGet 相容矩陣](docs/nuget-compatibility-matrix.md)。

### 2. 第一個 ODT

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("報告", 1);
document.Body.Paragraphs.Add("這是一份 ODF 文字文件。");
document.Save("report.odt");
```

### 3. 驗證文件

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate report.odt
```

完整入門流程、CLI 範例與後續文件請見
[docs/getting-started.md](docs/getting-started.md) 與
[samples/README.md](samples/README.md)。

## 文件中心

OdfKit 的文件已依常用技術文件結構重整為「評估 → 導入 → 營運」導覽：

| 階段 | 建議先讀 | 內容 |
|------|----------|------|
| 評估 | [文件中心](docs/index.md) | 文件入口、閱讀路徑與角色導覽 |
| 選型 | [套件目錄與選型指南](docs/package-catalog.md) | 核心套件、擴充套件、工具與情境對照 |
| 導入 | [快速開始](docs/getting-started.md) | 安裝模式、第一個文件、CLI、下一步 |
| 相容性 | [NuGet 相容矩陣](docs/nuget-compatibility-matrix.md) | 套件清單、目標框架、安裝策略 |
| 功能邊界 | [ODF 格式支援矩陣](docs/odf-format-support.md) | 支援狀態、測試證據、已知缺口 |
| 規則與語系 | [ODF Profile 來源](docs/odf-profile-sources.md)、[i18n 與在地化](docs/i18n-localization.md) | 內建 Profile、語系字典與訊息回退 |
| 版本與交付 | [版本與交付資訊](docs/version-delivery.md) | 交付管道、版本原則與安裝參考 |

更細部的規格、互通、測試與 corpus 文件，請由
[docs/index.md](docs/index.md) 進入。

## 版本與交付資訊

- 目前主要交付管道為 **GitHub 原始碼** 與 **GitHub Release** 資產，
  **未發佈至 nuget.org**。
- OdfKit 目前仍屬 `0.x` 階段；相容性承諾與破壞性變更將記錄於
  [CHANGELOG](CHANGELOG.md)。
- 版本、交付與安裝參考已整理於
  [docs/version-delivery.md](docs/version-delivery.md)。

## 已知限制

- 高階 API 覆蓋度依格式不同而不同；請以
  [ODF 格式支援矩陣](docs/odf-format-support.md) 與測試證據為準。
- Template、master 與 Flat XML 變體已具專屬 typed 文件類別與常用變體功能；
  但其高階語意 API 仍以對應主格式為主，完整變體專屬物件模型不屬於目前承諾範圍。
- `OdfKit.Extensions.Rendering` 需本機 LibreOffice 或相容程序後端，
  不屬於核心 OdfKit 的純 managed 路徑。

## 授權

OdfKit 專案採用
[CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/deed.zh_TW)。
第三方相依套件維持各自授權；詳見 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
