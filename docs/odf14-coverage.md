# ODF 1.4 Coverage Matrix

本文件呈現 OdfKit 專案對 ODF 1.4 規格的完成度事實盤點。

| 規格區域 | 程式入口 | Validator 規則 / Schema Pattern | 測試檔案 | 狀態 | 缺口 | 下一部目標 |
|---|---|---|---|---|---|---|
| **Package** | `OdfPackage.cs` | Zip Slip 防禦、路徑標準化 | `ComplianceTests.cs`, `CorpusComplianceTests.cs` | **validated** | 反射讀取 `ZipArchiveEntry` 私有欄位之相容風險 | 研究非反射式代替策略 |
| **Flat XML** | `OdfPackage.cs` | Sniffing、Flat XML/Package 互轉 | `ComplianceTests.cs` | **validated** | 扁平 XML 與 Package spec 對應覆蓋率待加強 | A8: 建立 Flat XML 完整 round-trip 測試 |
| **Manifest** | `META-INF/manifest.xml` | `manifest:version` 隨 Package 版本輸出 | `ComplianceTests.cs` | **validated** | 無 | 保持現狀 |
| **Mimetype** | `mimetype` | Mimetype 寫入與驗證 | `ComplianceTests.cs` | **complete** | 無 | 保持現狀 |
| **Encryption** | `OdfEncryptionInfo.cs` | Blowfish/AES 密碼學 metadata | `EncryptionTests.cs` | **validated** | 無 | 保持現狀 |
| **Signatures** | `OdfSigner.cs` | XML 數位簽章 | `AdvancedSecurityTests.cs` | **validated** | 無 | 保持現狀 |
| **Content/Styles/Meta/Settings Roots** | `OdfDocument.cs` | XML Roots 載入與預設範本 | `ComplianceTests.cs` | **validated** | 無 | A4: 將官方 1.4 schema validation 與 roots 銜接 |
| **ODT (Text Document)** | `TextDocument.cs` | Paragraph, Heading, List, Table, Change tracking | `M7CompletenessTests.cs`, `VerticalSliceRoundTripTests.cs` | **partial** | 預設版本先前為 1.3 (已於 A2 修正)；變更追蹤尚缺高階 API | A10: ODT 垂直切片、CJK/Ruby 與變更追蹤 API |
| **ODS (Spreadsheet)** | `SpreadsheetDocument.cs` | Sheets, Cells, Rows, Columns | `FormulaAndStylesTest.cs`, `OdsStreamWriterAndCommentAdversarialTests.cs` | **partial** | `OdsStreamWriter` 靜默吞錯與硬編碼 1.3 版本 (後者已於 A2 修正) | A11: ODS / Streaming writer 加固，移除靜默吞錯 |
| **OpenFormula** | `FormulaParser.cs` | 公式轉換與 AST 解析 | `FormulaTranslationStressTests.cs`, `FormulaEvaluatorStressTests.cs` | **partial** | Evaluator 限制範圍不明確 | A11: 定義有限的 evaluator 邊界，避免不實宣稱 |
| **ODP (Presentation)** | `PresentationDocument.cs` | Slides, Masters, Notes, Animations | `PresentationAndRenderingTests.cs`, `PresentationBoundaryTests.cs` | **partial** | 缺少與 LibreOffice oracle 互通的全面測試 | A12: ODP/ODG 樣式與 master 頁面加固 |
| **ODG (Drawing)** | `DrawingDocument.cs` | Draw pages, Shapes, Connectors | 無 | **partial** | 缺少獨立的 unit test 覆蓋 | A12: 補齊 ODG smoke tests |
| **ODC (Chart)** | `OdfChartDocument.cs` | Chart package, Data reference | 無 | **partial** | 目前僅為 package 結構包裝，缺乏語意解析 | A12: 圖表關係與 reference 驗證 |
| **ODF (Formula Package)** | `OdfFormulaDocument.cs` | MathML, Embedded package | 無 | **partial** | 偏向 package 結構，缺乏 MathML 自動驗證 | A12: MathML 元素嵌入驗證 |
| **Embedded Objects** | `OdfPackage.cs` | Embedded object merge | `CorpusComplianceTests.cs` | **partial** | Object 互轉保真度缺少 corpus 驗證 | A8: Embedded object round-trip 加固 |
| **Foreign Namespaces** | `OdfNode.cs` | XML namespace 獨立與保真 | `ComplianceTests.cs` | **validated** | 無 | A9: 證明 Typed DOM 不會遺失 unknown content |
| **Scripts/Macros** | `OdfPackage.cs` | Macro sanitization | `SecurityComplianceTests.cs` | **validated** | 無 | 保持現狀 |
| **External Resources** | `OdfPackage.cs` | External image, DTD block | `SecurityComplianceTests.cs` | **validated** | 無 | 保持現狀 |
| **OASIS Strict/Extended Profiles** | `OdfComplianceProfiles.cs` | Schema pattern validation | `ComplianceTests.cs` | **validated** | 仍缺乏真實官方 corpus 文件驗證 | A4: 銜接官方 ODF 1.4 schema 與真實 corpus |
| **ROC Taiwan CNS15251 / Gov Profiles** | `OdfComplianceProfiles.cs` | CNS15251 政策驗證與 Zip Slip | `CorpusComplianceTests.cs` | **validated** | 部分 profile 屬於 compatibility/draft | A13: 整理政策 profile，區分標準與草案 |
