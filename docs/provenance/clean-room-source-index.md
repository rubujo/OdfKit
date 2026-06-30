# Clean-room 來源索引

本文件集中記錄 `DefaultFormulaEvaluator.*`、`OdfSchemaPatternValidator.*`、
`OdfKit.Extensions.Collaboration` 與 managed conversion fidelity 的規格來源、測試證據與非目標，
避免日後維護者把外部參考實作或辦公套件原始碼當成可複製來源。

## 原則

- 實作必須以公開規格、專案自有測試與可再散布 fixture 為依據。
- 可以觀察外部工具輸出、文件與 JSON / ODF 樣本行為，但不可複製 LibreOffice、ODF Toolkit、
  Apache POI、NPOI 或商業元件原始碼。
- 若引用外部文件作為行為依據，PR 需在測試名稱、文件或註解中留下來源連結與範圍。
- 修改公式或 schema pattern 行為時，必須同時新增或更新 golden / regression 測試；不得只改演算法。

## 公式評估來源

| 範圍 | 權威來源 | OdfKit 實作入口 | Golden / regression 證據 |
|---|---|---|---|
| OpenFormula 表達式、型別、函式與 evaluator conformance | OASIS OpenDocument v1.4 Part 4 OpenFormula | `DefaultFormulaEvaluator.*`、`FormulaParser`、`FormulaTokenizer`、`OdfFormulaSupport` | `OpenFormulaSupportTests`、`FormulaHighLevelApiTests`、`FormulaEvaluatorStressTests`、`FormulaTranslationStressTests` |
| ODS 儲存格公式 round-trip 與 unsupported formula 保真 | OASIS OpenDocument v1.4 Part 3 schema 與 Part 4 OpenFormula | `SpreadsheetDocument.Formulas.cs`、`OdfTableSheet.Formulas.cs`、`FormulaPrefixNormalizer` | `OpenFormulaSupportTests.SpreadsheetFormulaRoundTripPreservesUnsupportedFormula`、`SpreadsheetApiUsabilityTests` |
| ODF / OOXML 公式語法轉換 | OASIS OpenFormula 與 ISO / ECMA OOXML 公開格式語法 | `OdfFormulaTranslator.*` | `OoxmlConversionTests`、`FormulaTranslationStressTests` |
| LibreOffice 相容擴充函式 | LibreOffice 公開文件與自有互通測試輸出 | `OdfFormulaSupport`、`DefaultFormulaEvaluator` | `OpenFormulaSupportTests.LibreOfficeEasterSundayEvaluatesToDateSerial`、`OpenFormulaSupportTests.LibreOfficeIsOmittedEvaluatesByArgumentCount` |

公式評估不是完整試算表引擎：不承諾重算所有 host-defined 行為、volatile function、
多使用者計算狀態或任意外部連結資料來源。未支援函式應診斷並保留原公式，而不是破壞 round-trip。

## Schema pattern 來源

| 範圍 | 權威來源 | OdfKit 實作入口 | Golden / regression 證據 |
|---|---|---|---|
| ODF 1.1 / 1.2 / 1.3 / 1.4 RELAX NG schema | OASIS OpenDocument schema artifacts | `OdfSchemaGenerator`、`Odf*OfficialSchemaProvider.g.cs`、`OdfSchemaPatternValidator.*` | `OdfSchemaGeneratorTests`、`TypedDomParityTests`、`DocsAndCorpusContractTests` |
| RELAX NG pattern 語意 | RELAX NG Specification | `OdfSchemaPatternValidator.Content.*`、`OdfSchemaPatternValidator.NameClasses.cs`、`OdfSchemaPatternValidator.Attributes.*` | `ComplianceTests.SchemaPatternValidator*` |
| XML Schema datatypes 與 facets | W3C XML Schema Part 2 Datatypes | `OdfSchemaPatternValidator.DataTypes.*` | `ComplianceTests.SchemaPatternValidatorHandlesTextDataAndValueNodes`、`ComplianceTests.SchemaPatternValidatorHandlesAttributeDataTypeNodes` |
| ODF package / flat XML corpus gate | OASIS ODF package 與 schema 規範、自有 generated corpus | `OdfProfileRuleValidator.SchemaPatterns.cs`、CLI `validate-corpus` | `CorpusComplianceTests`、`tests/fixtures/corpus/manifest.json`、`eng/Test-OdfCorpus.ps1` |

Schema pattern validator 目標是支援 OdfKit 內建 profile 的可驗證 ODF XML 結構，而不是通用
RELAX NG validator。若需要對外部 schema 做完整 RELAX NG 驗證，應新增獨立 validator 或接外部工具，
不應讓核心 ODF profile gate 承擔任意 schema 語意。

## JSON Collaboration 來源

| 範圍 | 權威來源 | OdfKit 實作入口 | Golden / regression 證據 |
|---|---|---|---|
| TDF ODF Toolkit operations wire shape | TDF ODF Toolkit 公開文件與 reference JSON | `OdfKit.Extensions.Collaboration/OdtOperationLog.cs`、`OdtOperationsImporter.cs`、`OdtOperationsExporter.cs` | `CollaborationOperationsTests`、`CollaborationFixtureManifestTests`、`tests/fixtures/collaboration/manifest.json` |
| TDF `{ "changes": [...] }` envelope 與裸陣列相容 | TDF ODF Toolkit reference operations corpus wire shape | `OdtOperationCompatibilityOptions`、`OdtOperationLog`、`OdtOperationImportReport` | `CollaborationOperationsTests.Merge_AcceptsTdfChangesEnvelope`、`ExportToJson_TdfEnvelope_WrapsOperationsInChanges`、`OperationLog_ParseSerialize_PreservesUnknownWireFields` |
| ODF Text operation replay：段落、文字、Tab、換行、基本格式、單段落刪除／移動、最上層 split/merge、基本清單、固定尺寸表格、欄位、comment、header/footer、font declaration 與安全 drawing placeholder | TDF operation 名稱與公開 JSON fixture 行為觀察 | `OdtOperationsImporter.Merge` | `CollaborationOperationsTests.Merge_Replays*`、`Merge_ReplaysExtendedTdfTextOperationSubset`、`CollaborationFixtureManifestTests` |

JSON Collaboration 是選用 extension-scoped compatibility subset，不是核心套件功能。clean-room 策略
只允許使用 TDF 公開文件、operation 名稱、wire shape 與 reference JSON 做行為對標；不得複製
Java ODF Toolkit 原始碼。完整多人協同演算法、OT、CRDT、undo stack、任意衝突合併、完整 drawing
DOM、動態表格擴張與 header/footer/note selection 完整語意仍屬非目標。

## Managed conversion fidelity 來源

| 範圍 | 權威來源 | OdfKit 實作入口 | Golden / regression 證據 |
|---|---|---|---|
| ODG enhanced-path 與 SVG path mapping | OASIS OpenDocument drawing schema、SVG path 公開規格與 LibreOffice 匯出行為觀察 | `OdfKit.Extensions.Html/OdfSvgExporter.cs` | `ManagedSvgExportTests.SvgExporterTranslatesEnhancedPathAngleEllipseArcs`、`ManagedSvgExportTests.SvgExporterSplitsFullEnhancedPathEllipseArcs`、`ManagedSvgExportTests.SvgExporterCoversLibreOfficeStyleCustomShapeCorpus` |
| ODT / RTF 控制字、註解、欄位與圖片 marker round-trip | RTF 1.9.1 Specification、ODF text schema 與自有 importer/exporter regression | `OdfKit.Extensions.Html/OdfRtfImporter.cs`、`OdfRtfExporter.cs` | `ManagedTextExportTests.RtfImporterConvertsSectionAndColumnBreaksToSoftPageBreaks`、`ManagedTextExportTests.RtfImporterConvertsSoftLineAndSoftPageControls`、`ManagedTextExportTests.RtfImporterPreservesStandardAnnotationGroups` |
| ODP / PPTX timing、theme 與 master-style mapping | PresentationML / Open XML SDK 文件、ODF presentation schema 與 PowerPoint / LibreOffice 互通觀察 | `OdfKit.Extensions.Ooxml/OdpToPptxConverter.cs`、`PptxToOdpConverter.cs` | `ManagedPptxConversionTests.PptxConvertersPreserveBasicObjectAnimations`、`PptxToOdpConverterExpandsBuildParagraphWhenEffectOmitsParagraphRange`、`PptxToOdpConverterParsesMasterInheritanceAndTimeline` |

Managed conversion fidelity 允許觀察 LibreOffice、PowerPoint 或其他辦公套件的輸出檔案與行為，
但觀察結果必須寫成相容性 regression，不得宣稱為規格要求。新增轉檔 fidelity 行為時，應以
最小 ODF / OOXML / RTF / SVG fixture 鎖定輸入與輸出，不以外部原始碼或不可再散布 golden
作為依據。

## 不可接受來源

- 不複製 LibreOffice C++、Java ODF Toolkit、Apache POI、NPOI 或商業 SDK 原始碼。
- 不以反編譯結果、私有測試資料、不可再散布文件或授權不明 corpus 作為 golden。
- 不把互通觀察結果寫成「規格要求」；若只是觀察 LibreOffice 行為，必須標示為相容性 regression。

## 更新流程

1. 先確認變更屬於公式評估、schema pattern、JSON Collaboration 或 managed conversion fidelity。
2. 在 PR 說明或測試名稱中標明來源：OASIS / RELAX NG / XML Schema / LibreOffice 觀察 / PowerPoint 觀察 / TDF 公開 JSON wire shape / 自有 corpus。
3. 新增最小 golden 或 regression 測試，覆蓋正向與至少一個失敗模式。
4. 執行相關入口：
   - 公式：`dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --filter FullyQualifiedName~Formula`
   - Schema pattern：`dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~ComplianceTests.SchemaPatternValidator|FullyQualifiedName~CorpusComplianceTests"`
   - JSON Collaboration：`dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --filter FullyQualifiedName~CollaborationOperationsTests`
   - Managed conversion：`dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~ManagedSvgExportTests|FullyQualifiedName~ManagedTextExportTests|FullyQualifiedName~ManagedPptxConversionTests"`
   - Corpus gate：`pwsh eng/Test-OdfCorpus.ps1`
