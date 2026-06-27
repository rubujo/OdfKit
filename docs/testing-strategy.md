# OdfKit 測試策略

本文件說明 OdfKit 測試套件的長期分層與命名規則，避免把歷史開發階段
名稱誤判為重複或不必要測試。整理測試時的原則是：先保留覆蓋率，再改
善可讀性；只有在證據顯示完全重複時才刪除測試。

## 測試分層

測試分類統一使用 xUnit `Trait` 與 `OdfKit.Tests.TestCategories` 常數，避免
GitHub Actions 或本機腳本硬編碼測試類別名稱。常用入口如下：

- 快速 PR 回歸：`dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release -f net10.0 --filter Category=Smoke`。
- 壓力與效能檢查：`--filter "Category=Stress|Category=Performance"`。
- 外部互通檢查：`--filter Category=Interop`。
- Corpus 檢查：`--filter Category=Corpus`。
- 規範與 schema 檢查：`--filter Category=Compliance`。
- 跨功能情境檢查：`--filter Category=Scenario`。

| 分層 | 目的 | 代表檔案 |
|------|------|----------|
| API usability | 驗證公開 API 的日常使用流程 | `*ApiUsabilityTests.cs`、`*HighLevelApiTests.cs` |
| Format round-trip | 驗證 ODF 封裝、Flat XML 與 unknown XML 保真 | `PackageRoundTripTests.cs` |
| Compliance | 驗證 ODF 規範、profile、corpus 與格式矩陣 | `ComplianceTests.cs`、`CorpusComplianceTests.cs` |
| Interop | 驗證 LibreOffice、OOXML 與外部工具互通 | `LibreOfficeInteropTests.cs`、`OfficeInteropConversionTests.cs` |
| Boundary | 驗證邊界輸入、錯誤復原與歷史回歸 | `*BoundaryTests.cs`、`*RegressionTests.cs` |
| Stress | 驗證大量資料、深層結構與效能邊界 | `*StressTests.cs` |
| Scenario | 驗證跨格式或跨功能工作流程 | `*ScenarioTests.cs`、`*E2ETests.cs`、`VerticalSliceRoundTripTests.cs` |
| Packaging / docs contract | 驗證 NuGet、發佈資產、文件／corpus 契約與消費端煙霧測試 | `NuGetPackagingTests.cs`、`PackageReadinessTests.cs`、`DocsAndCorpusContractTests.cs` |

## 命名規則

- `Phase*`、`Milestone*`、`Challenger*` 屬於歷史開發階段命名，不再新增。
- 邊界與錯誤復原測試使用 `*BoundaryTests` 或 `*RegressionTests`。
- 大量資料與資源壓力測試使用 `*StressTests`。
- 互通測試名稱應指出外部工具或格式，例如 `LibreOffice*`、`Ooxml*`。
- 單一測試檔若只有一個 `[Fact]` 或 `[Theory]`，優先併入相鄰領域檔案。

## 去重規則

測試可刪除必須同時符合下列條件：

1. 斷言、輸入資料與失敗模式完全由另一個測試覆蓋。
2. 刪除後相關領域測試仍能說明同一個需求。
3. `dotnet test` 或聚焦 filter 可證明沒有覆蓋率或行為回歸。

下列狀況不視為重複：

- 同一功能分別驗證 API 層、封裝 XML 層與外部互通層。
- 同一資料格式分別驗證 happy path、boundary、stress 與 regression。
- 測試方法名稱相近，但斷言不同的失敗模式。

## 目前整合候選

下列檔案保留覆蓋率，但後續整理時優先拆分或併入相鄰領域檔：

- `OptimizedRefactoringTests.cs`：近期重構驗收集合，應逐步搬回 package、DOM、transaction、stream writer 專檔。
- `FormulaAndStylesTest.cs`：公式、格式、字型、pivot 與 reference model 混合檔，應分散至對應專檔。
- `OdfFeatureE2ETests.cs`：保留跨功能 Tier 3/4 scenario，單一功能 happy path 可移回 high-level 或 boundary 測試。
- `EmpiricalStressTests.cs`、`OdfCoreStressTests.cs`：保留作為 `Category=Stress`，不進快速 CI。

## TEST-STRUCT 完成狀態

2026-06-20 收斂結果：

| 項目 | 狀態 | 結果 |
|------|------|------|
| TEST-STRUCT-1 | ✅ | 新增本測試策略文件，說明分層、命名與去重規則。 |
| TEST-STRUCT-2 | ✅ | Phase / Milestone / Challenger / Adversarial 歷史檔名已改為長期領域命名；`DocsAndCorpusContractTests` 保留文件與 corpus 契約驗證但移除 ODF Toolkit parity readiness 歷史命名。**2026-06-20 補充**：先前重命名遺留 12 個逐字元重複的舊檔案未刪除，已 diff 驗證後移除，雙 TFM 測試數由 1688 降為 1483，零覆蓋率損失。 |
| TEST-STRUCT-3 | ✅ | 原計畫將四個單宣告稀疏檔併入相鄰領域檔，先前文件曾誤標為已完成，實際從未執行（2026-06-23 以 `git log --follow` 查證後修正記錄）。**2026-06-23 補充**：已實際執行併入：`OdfFormatRoundTripTests.MinimalSupportedFormatRoundTrips`、`OdfUnknownXmlRoundTripTests.HighLevelSavePreservesUnknownXmlForeignContentAndProcessingInstructions` 併入 `PackageRoundTripTests`；`OoxmlVisualGoldenManifestTests.Manifest_DefinesExpectedScenarios` 併入 `OoxmlConversionTests`；`ChartFallbackRenderTests.RenderChartsToFallbackImages_GeneratesPngAndUpdatesXml` 併入 `ChartHighLevelApiTests`。四個來源檔已刪除，測試方法名與斷言保留，[interop-corpus.md](interop-corpus.md)／[odf-format-support.md](odf-format-support.md)／[foreign-extension-policy.md](foreign-extension-policy.md)／[ooxml-visual-golden-matrix.md](ooxml-visual-golden-matrix.md)／[corpus-manifest.md](corpus-manifest.md) 對應類別引用已同步更新。 |
| TEST-STRUCT-4 | ✅ | `E2ETests` 與 `OdfFeatureE2ETests` 已完成主題盤點；未發現可安全刪除的大量完全重複測試，保留兩者但明確分層。 |

## E2E 分層對照

| 測試檔 | 主要職責 | 與另一 E2E 檔的關係 | 處置 |
|--------|----------|----------------------|------|
| `E2ETests.cs` | 封裝／Flat XML／validator 邊界、template 與 real-world package workflow。 | 偏底層 package、validation 與格式偵測；雖有 feature coverage 名稱，但斷言多落在封裝與驗證行為。 | 保留；不標記 deprecated。 |
| `OdfFeatureE2ETests.cs` | ODF 高階功能矩陣：TOC、tracked changes、ruby/CJK、MathML、named range、sort/filter、pivot、conditional formatting、formula evaluator、presentation、SMIL、shape、chart 與 Tier 3/4 workflows。 | 偏高階語意與跨功能 scenario；與 `E2ETests` 只在「端到端」測試型態重疊，失敗模式不同。 | 保留；後續若拆分，依 F1-F13 領域移入對應 high-level/boundary 檔。 |

去重結論：目前沒有足夠證據刪除超過 5 個 E2E 測試；依 TEST-STRUCT 的保守規則，不為降低數字而刪除互補的 E2E 覆蓋。
## 維護入口

- 安全格式化：`pwsh eng/Format-Safe.ps1 -IncludeTests`。
- 合併衝突標記檢查：`pwsh eng/Test-MergeConflictMarkers.ps1`。
- 簽署稽核：`pwsh eng/Test-GpgSignatures.ps1`。
