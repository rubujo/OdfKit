# OdfKit 測試策略

本文件說明 OdfKit 測試套件的長期分層與命名規則，避免把歷史開發階段
名稱誤判為重複或不必要測試。整理測試時的原則是：先保留覆蓋率，再改
善可讀性；只有在證據顯示完全重複時才刪除測試。

## 測試分層

| 分層 | 目的 | 代表檔案 |
|------|------|----------|
| API usability | 驗證公開 API 的日常使用流程 | `*ApiUsabilityTests.cs`、`*HighLevelApiTests.cs` |
| Format round-trip | 驗證 ODF 封裝、Flat XML 與 unknown XML 保真 | `PackageRoundTripTests.cs` |
| Compliance | 驗證 ODF 規範、profile、corpus 與格式矩陣 | `ComplianceTests.cs`、`CorpusComplianceTests.cs` |
| Interop | 驗證 LibreOffice、OOXML 與外部工具互通 | `LibreOfficeInteropTests.cs`、`OfficeInteropConversionTests.cs` |
| Boundary | 驗證邊界輸入、錯誤復原與歷史回歸 | `*BoundaryTests.cs`、`*RegressionTests.cs` |
| Stress | 驗證大量資料、深層結構與效能邊界 | `*StressTests.cs` |
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

## TEST-STRUCT 完成狀態

2026-06-20 收斂結果：

| 項目 | 狀態 | 結果 |
|------|------|------|
| TEST-STRUCT-1 | ✅ | 新增本測試策略文件，說明分層、命名與去重規則。 |
| TEST-STRUCT-2 | ✅ | Phase / Milestone / Challenger / Adversarial 歷史檔名已改為長期領域命名；`DocsAndCorpusContractTests` 保留文件與 corpus 契約驗證但移除 ODF Toolkit parity readiness 歷史命名。 |
| TEST-STRUCT-3 | ✅ | 四個單宣告稀疏檔已併入相鄰領域檔：`OdfFormatRoundTripTests`、`OdfUnknownXmlRoundTripTests` 併入 `PackageRoundTripTests`；`OoxmlVisualGoldenManifestTests` 併入 `OoxmlConversionTests`；`ChartFallbackRenderTests` 併入 `ChartHighLevelApiTests`。測試方法名與斷言保留。 |
| TEST-STRUCT-4 | ✅ | `E2ETests` 與 `OdfFeatureE2ETests` 已完成主題盤點；未發現可安全刪除的大量完全重複測試，保留兩者但明確分層。 |

## E2E 分層對照

| 測試檔 | 主要職責 | 與另一 E2E 檔的關係 | 處置 |
|--------|----------|----------------------|------|
| `E2ETests.cs` | 封裝／Flat XML／validator 邊界、template 與 real-world package workflow。 | 偏底層 package、validation 與格式偵測；雖有 feature coverage 名稱，但斷言多落在封裝與驗證行為。 | 保留；不標記 deprecated。 |
| `OdfFeatureE2ETests.cs` | ODF 高階功能矩陣：TOC、tracked changes、ruby/CJK、MathML、named range、sort/filter、pivot、conditional formatting、formula evaluator、presentation、SMIL、shape、chart 與 Tier 3/4 workflows。 | 偏高階語意與跨功能 scenario；與 `E2ETests` 只在「端到端」測試型態重疊，失敗模式不同。 | 保留；後續若拆分，依 F1-F13 領域移入對應 high-level/boundary 檔。 |

去重結論：目前沒有足夠證據刪除超過 5 個 E2E 測試；依 TEST-STRUCT 的保守規則，不為降低數字而刪除互補的 E2E 覆蓋。
## 維護入口

- 測試結構整理計畫：`IMPLEMENTATION_PLAN.md` 的 `Phase TEST-STRUCT`。
- 安全格式化：`pwsh eng/Format-Safe.ps1 -IncludeTests`。
- 合併衝突標記檢查：`pwsh eng/Test-MergeConflictMarkers.ps1`。
- 簽署稽核：`pwsh eng/Test-GpgSignatures.ps1`。
