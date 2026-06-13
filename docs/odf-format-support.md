# ODF 格式支援矩陣

本文件記錄 OdfKit 對主要 ODF 格式的實作狀態。狀態以目前程式碼與
測試證據為準，不把路線圖中的目標預先標為完成。

ODF Toolkit / ODF Validator 對標線另見 [odf-toolkit-parity.md](odf-toolkit-parity.md)。

## 狀態標記

- `complete`：已有直接 API 與測試覆蓋，可作為目前支援能力使用。
- `validated`：已有驗證或偵測測試證據，但高階 API 仍可能有限。
- `package-level`：可建立、載入、保存與驗證最小封裝，但高階語意 API 尚未完整。
- `partial`：已有部分高階模型或 round-trip 能力，但仍有明確缺口。
- `planned`：尚未有足夠程式與測試證據支撐。

## 矩陣

| Extension | MIME type | `OdfDocumentKind` | Detect | Create | Load | Save | Validate | Round-trip | High-level API | Test evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| `.odt` | `application/vnd.oasis.opendocument.text` | `Text` | complete | complete | complete | complete | validated | complete | partial | `TextApiUsabilityTests`, `TextAdvancedFidelityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.ott` | `application/vnd.oasis.opendocument.text-template` | `TextTemplate` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.odm` | `application/vnd.oasis.opendocument.text-master` | `TextMaster` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.fodt` | `application/vnd.oasis.opendocument.text` | `FlatText` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests` |
| `.ods` | `application/vnd.oasis.opendocument.spreadsheet` | `Spreadsheet` | complete | complete | complete | complete | validated | complete | partial | `SpreadsheetApiUsabilityTests`, `SpreadsheetCommonApiTests`, `OpenFormulaSupportTests`, `InteropCorpusTests` |
| `.ots` | `application/vnd.oasis.opendocument.spreadsheet-template` | `SpreadsheetTemplate` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fods` | `application/vnd.oasis.opendocument.spreadsheet` | `FlatSpreadsheet` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests` |
| `.odp` | `application/vnd.oasis.opendocument.presentation` | `Presentation` | complete | complete | complete | complete | validated | complete | partial | `PresentationApiUsabilityTests`, `PresentationAndRenderingTests`, `PresentationBoundaryTests`, `InteropCorpusTests` |
| `.otp` | `application/vnd.oasis.opendocument.presentation-template` | `PresentationTemplate` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fodp` | `application/vnd.oasis.opendocument.presentation` | `FlatPresentation` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests` |
| `.odg` | `application/vnd.oasis.opendocument.graphics` | `Graphics` | complete | complete | complete | complete | validated | complete | partial | `DrawingApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.otg` | `application/vnd.oasis.opendocument.graphics-template` | `GraphicsTemplate` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fodg` | `application/vnd.oasis.opendocument.graphics` | `FlatGraphics` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests` |
| `.odc` | `application/vnd.oasis.opendocument.chart` | `Chart` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.odf` | `application/vnd.oasis.opendocument.formula` | `Formula` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `PackageRoundTripTests`, `InteropCorpusTests` |
| `.odi` | `application/vnd.oasis.opendocument.image` | `Image` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.odb` | `application/vnd.oasis.opendocument.database` | `Database` | complete | complete | complete | complete | validated | complete | partial | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |

## 目前缺口

- 統一的 `OdfDocument.Load` / `OdfDocument.Create` 與
  `OdfDocumentFactory.LoadDocument` / `CreateDocument` 高階入口已建立。
- ODT、ODS、ODP、ODG 已有常用高階建立與編輯 API，但尚非完整辦公套件物件模型。
- `.ott`、`.ots`、`.otp` 與 `.otg` 已可透過通用高階 factory 建立、
  載入、保存與 round-trip，並提供文件層格式摘要；完整範本專屬語意模型仍屬後續擴充。
- `.odm` 已可透過通用高階 factory 建立、載入、保存與 round-trip，並提供
  主控文件格式摘要；完整主控文件專屬語意模型仍屬後續擴充。
- `.fodt`、`.fods`、`.fodp` 與 `.fodg` 已可透過通用高階 factory 建立、
  載入、保存與 round-trip，並提供 Flat XML 格式摘要；完整 flat 專屬語意模型仍屬後續擴充。
- `.odc` 已有圖表類型、標題、圖例位置與序列摘要 API；`.odf` 已有 MathML 根節點與純文字摘要 API；`.odi` 已有主要影像路徑、媒體類型與大小摘要 API；`.odb` 已有連線參照與資料表摘要 API。完整圖表、MathML、影像與資料庫語意模型仍屬後續擴充。
- 全格式最小 round-trip corpus 已由 `OdfFormatRoundTripTests` 覆蓋；後續仍需加入
  更接近真實文件的 corpus。
- 外部 ODF Validator baseline 已可透過 CLI `validate --baseline odf-validator` 與
  `validate-corpus` 選用，但完整官方 parity corpus 仍需擴充。
