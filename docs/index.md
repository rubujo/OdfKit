# OdfKit 文件中心

本文件將 OdfKit 的現有文件整理為常用技術文件的閱讀結構，方便依照
「評估、導入、營運」三個階段快速找到需要的資訊。

## 建議閱讀路徑

| 如果您要… | 建議先讀 | 再延伸閱讀 |
|-----------|----------|------------|
| 快速評估 OdfKit 是否符合需求 | [README](../README.md) | [套件目錄與選型指南](package-catalog.md)、[ODF 格式支援矩陣](odf-format-support.md)、[效能對比報告](performance-comparison.md)、[智慧財產與合規說明](ip-compliance.md) |
| 決定要安裝哪些套件 | [套件目錄與選型指南](package-catalog.md) | [NuGet 相容矩陣](nuget-compatibility-matrix.md)、[渲染後端部署](rendering-backend-deployment.md) |
| 建立第一個範例或驗證 PoC | [快速開始](getting-started.md)、[核心 SDK 快速開始](core-quickstart.md) | [實作食譜](cookbook.md)、[samples/README.md](../samples/README.md) |
| 了解內建 Profile 與多語系機制 | [ODF Profile 來源](odf-profile-sources.md) | [i18n 與在地化](i18n-localization.md)、[i18n 詞彙表](i18n-glossary.md)、[ODF 格式支援矩陣](odf-format-support.md) |
| 規劃部署、升級與版本交付 | [版本與交付資訊](version-delivery.md) | [高階 API 遷移指南](migration-high-level-api.md)、[GitHub Release 發佈指南](github-release-publishing.md)、[CHANGELOG](../CHANGELOG.md) |
| 確認功能邊界、互通性與驗證證據 | [能力宣稱與證據索引](evidence-index.md) | [ODF 格式支援矩陣](odf-format-support.md)、[WebFont 證據矩陣](webfont-evidence-matrix.md)、[LibreOffice 互通矩陣](libreoffice-interop-matrix.md)、[OOXML 視覺驗收矩陣](ooxml-visual-golden-matrix.md) |
| 維護 CI/CD 與驗證分層 | [CI/CD 驗證設計](ci-cd.md) | [GitHub Release 發佈指南](github-release-publishing.md)、[Corpus Manifest 規則](corpus-manifest.md) |

## 依生命週期分類

### 1. 評估與決策

| 文件 | 用途 |
|------|------|
| [README](../README.md) | 產品概觀、安裝入口、文件導覽 |
| [套件目錄與選型指南](package-catalog.md) | 依情境挑選核心套件、擴充套件與工具 |
| [NuGet 相容矩陣](nuget-compatibility-matrix.md) | 套件清單、目標框架、安裝策略 |
| [ODF 格式支援矩陣](odf-format-support.md) | 功能覆蓋、狀態標記與測試證據 |
| [ODF Profile 來源](odf-profile-sources.md) | 內建 Profile 的來源、權威層級與驗證狀態 |
| [i18n 與在地化](i18n-localization.md) | 語系字典、訊息回退與 `OdfLocalizer` 使用方式 |
| [版本與交付資訊](version-delivery.md) | 交付管道、版本原則與安裝參考 |

### 2. 導入與開發

| 文件 | 用途 |
|------|------|
| [快速開始](getting-started.md) | 第一個專案、第一個文件、CLI 驗證 |
| [核心 SDK 快速開始](core-quickstart.md) | 核心 SDK 的純受控建立、載入、驗證與低記憶體匯出路徑 |
| [實作食譜](cookbook.md) | 常見操作片段與實作範例 |
| [WebFont 多國罕用字套件](webfonts.md) | ASP.NET Core／Web Forms、CSP、CDN、自動內容掃描、Big5／Big5E 與 ORM 整合 |
| [WebFont 純 .NET 架構契約](webfont-managed-architecture.md) | 純 C#／.NET 產品邊界、格式與授權準入、Phase 0～5 驗收 |
| [API 表面分層](api-surface-layers.md) | API 分層、使用路徑與新增 API 放置準則 |
| [API 表面一致性](api-surface-consistency.md) | 公開 API 分層、命名契約與非目標邊界 |
| [API 表面盤點](api-surface-inventory.md) | 高階外觀層命名分布、破壞性重新命名批次與文件掃描基線 |
| [API Reference](reference/index.md) | Spreadsheet、Chart、Template 與 Interop 的 options、report 與能力邊界 |
| [高階 API 遷移指南](migration-high-level-api.md) | 從早期 0.0.1 草稿遷移至四主格式一致生命週期契約 |
| [NPOI／Independentsoft 遷移指南](migration-npoi-independentsoft.md) | 從常見第三方文件 API 遷移至 OdfKit 的對照與注意事項 |
| [串流讀取安全限制](security-limits.md) | ODS／ODT Reader 的資源預算、資料流所有權與信任邊界 |
| [tools/README.md](../tools/README.md) | CLI、schema generator、corpus generator 與 trim smoke 工具總覽 |
| [samples/README.md](../samples/README.md) | 單檔 Script 範例與輸出說明 |
| [渲染後端部署](rendering-backend-deployment.md) | LibreOffice 渲染擴充的部署要求 |
| [Foreign 擴充政策](foreign-extension-policy.md) | 非標準命名空間與相容策略 |
| [UDX 非功能性目標](udx-non-goals.md) | 明確排除於實作範圍外的功能模組與非目標 |

### 3. 驗證、互通與營運

| 文件 | 用途 |
|------|------|
| [ODF Toolkit 對標線](odf-toolkit-parity.md) | ODF Toolkit / ODF Validator / ODFDOM 對標狀態 |
| [LibreOffice 互通矩陣](libreoffice-interop-matrix.md) | 與 LibreOffice 的行為驗證 |
| [OOXML 視覺驗收矩陣](ooxml-visual-golden-matrix.md) | OOXML 視覺與 golden 驗收 |
| [互通語料庫總覽](interop-corpus.md) | corpus 來源與使用方式 |
| [官方 Corpus 來源](odf-official-corpus-sources.md) | 官方 ODF corpus 來源說明 |
| [Corpus Manifest 規則](corpus-manifest.md) | corpus manifest 契約 |
| [CI/CD 驗證設計](ci-cd.md) | GitHub Actions 分層、逾時、煙霧測試與診斷產物規則 |
| [API 文件網站治理](api-docs-site.md) | DocFX 輸入、建置、連結檢查與發布邊界 |
| [能力宣稱與證據索引](evidence-index.md) | 將公開能力宣稱對應至測試、規格與可重現證據 |
| [WebFont 證據矩陣](webfont-evidence-matrix.md) | WebFont Phase 0～5 的已實證能力、實驗性功能與人工閘門 |
| [WebFont 純 .NET 架構契約](webfont-managed-architecture.md) | managed 引擎的實作順序、拒絕矩陣與 clean consumer 證據 |
| [可維護性與複雜度債](maintainability.md) | Partial、在地化 JSON 產線、Public API、Package Validation |
| [協作者地圖](architecture-collaborators.md) | 大型領域根與 partial／engine 邊界（v0.0.1 完滿基線） |
| [人機協作可維護性](human-agent-maintainability.md) | 人類／Agent 平衡；禁止為拆而拆 |
| [公開 API 可選參數規範](public-api-optional-parameters.md) | RS0026／RS0027 政策與新增 API 檢查清單 |
| [產品品質閘門](product-quality-gates.md) | Corpus／LibreOffice／OOXML／效能基線與 sample 可執行檢查入口 |
| [效能預算](performance-budgets.md) | CI 效能門檻、回歸判定與預算設定檔的維護原則 |
| [效能基準線](performance-baselines.md) | 基準測試回歸關卡、穩定量測設定檔與基準線報告產生方式 |
| [三格式標準效能基準](performance-standard-documents.md) | ODS、ODT、ODP 標準工作負載、checksum 與量測政策 |
| [效能對比報告](performance-comparison.md) | `OdsStreamWriter` 與 MiniExcel、ClosedXML 之跨套件串流寫入實測對比、方法論限制與授權裁定 |
| [GitHub Release 發佈指南](github-release-publishing.md) | 封裝、驗證與發佈步驟 |
| [ODF 1.4 逐章稽核紀錄](odf14-gap-audit.md) | 對照 ODF 1.4 四份正式規格文本逐章比對 schema／驗證層／公式引擎缺口 |
| [ODF 1.4 規格覆蓋契約](odf14-coverage-contract.md) | Schema、package lifecycle、高階 facade 與互通行為的持續完滿定義 |
| [ODF 1.4 覆蓋狀態](odf14-coverage-status.md) | 目前 coverage 摘要與持續驗收入口 |

## 其他治理文件

| 文件 | 用途 |
|------|------|
| [CHANGELOG](../CHANGELOG.md) | 版本變更與破壞性調整紀錄 |
| [THIRD-PARTY-NOTICES](../THIRD-PARTY-NOTICES.md) | 第三方授權與版權聲明 |
| [provenance/README.md](provenance/README.md) | 模組來源、授權與依據 |
| [Clean-room 來源索引](provenance/clean-room-source-index.md) | 公式評估、schema pattern、JSON Collaboration 與受控轉換保真度的規格來源、不可複製來源與 golden 測試契約 |
| [WebFont managed Clean-room 來源](provenance/webfont-managed-clean-room.md) | 字型 parser／writer 的允許規格、禁止實作來源與黑箱 oracle 隔離 |
| [智慧財產與合規說明](ip-compliance.md) | 複合授權、AI 產製、clean-room、DCO、採用者盡職調查清單（非正式法律意見） |
| [可維護性與複雜度債](maintainability.md) | Partial 準則、在地化拆分、產生碼、歷史腳本與後續債 |
