# OdfKit 文件中心

本文件將 OdfKit 的現有文件整理為常用技術文件的閱讀結構，方便依照
「評估、導入、營運」三個階段快速找到需要的資訊。

## 建議閱讀路徑

| 如果您要… | 建議先讀 | 再延伸閱讀 |
|-----------|----------|------------|
| 快速評估 OdfKit 是否符合需求 | [README](../README.md) | [套件目錄與選型指南](package-catalog.md)、[ODF 格式支援矩陣](odf-format-support.md) |
| 決定要安裝哪些套件 | [套件目錄與選型指南](package-catalog.md) | [NuGet 相容矩陣](nuget-compatibility-matrix.md)、[Rendering 後端部署](rendering-backend-deployment.md) |
| 建立第一個範例或驗證 PoC | [快速開始](getting-started.md) | [Cookbook](cookbook.md)、[samples/README.md](../samples/README.md) |
| 了解內建 Profile 與多語系機制 | [ODF Profile 來源](odf-profile-sources.md) | [i18n 與在地化](i18n-localization.md)、[ODF 格式支援矩陣](odf-format-support.md) |
| 規劃部署、升級與版本交付 | [版本與交付資訊](version-delivery.md) | [GitHub Release 發佈指南](github-release-publishing.md)、[CHANGELOG](../CHANGELOG.md) |
| 確認功能邊界、互通性與驗證證據 | [ODF 格式支援矩陣](odf-format-support.md) | [ODF 1.4 覆蓋](odf14-coverage.md)、[LibreOffice 互通矩陣](libreoffice-interop-matrix.md)、[OOXML 視覺驗收矩陣](ooxml-visual-golden-matrix.md) |

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
| [Managed-first 轉檔策略](managed-first-conversion-strategy.md) | 純 managed 與外部後端的分工原則 |
| [版本與交付資訊](version-delivery.md) | 交付管道、版本原則與安裝參考 |

### 2. 導入與開發

| 文件 | 用途 |
|------|------|
| [快速開始](getting-started.md) | 第一個專案、第一個文件、CLI 驗證 |
| [Cookbook](cookbook.md) | 常見操作片段與實作範例 |
| [tools/README.md](../tools/README.md) | CLI、schema generator、corpus generator 與 trim smoke 工具總覽 |
| [samples/README.md](../samples/README.md) | 單檔 Script 範例與輸出說明 |
| [Rendering 後端部署](rendering-backend-deployment.md) | LibreOffice 渲染擴充的部署要求 |
| [Foreign 擴充政策](foreign-extension-policy.md) | 非標準命名空間與相容策略 |
| [typed DOM 覆蓋](typed-dom-coverage.md) | DOM wrapper 覆蓋範圍 |

### 3. 驗證、互通與營運

| 文件 | 用途 |
|------|------|
| [ODF 1.4 覆蓋](odf14-coverage.md) | 規格對照與完成度 |
| [ODF Toolkit 對標線](odf-toolkit-parity.md) | 與外部工具能力對照 |
| [LibreOffice 互通矩陣](libreoffice-interop-matrix.md) | 與 LibreOffice 的行為驗證 |
| [OOXML 視覺驗收矩陣](ooxml-visual-golden-matrix.md) | OOXML 視覺與 golden 驗收 |
| [Interop Corpus 總覽](interop-corpus.md) | corpus 來源與使用方式 |
| [官方 Corpus 來源](odf-official-corpus-sources.md) | 官方 ODF corpus 來源說明 |
| [Corpus Manifest 規則](corpus-manifest.md) | corpus manifest 契約 |
| [GitHub Release 發佈指南](github-release-publishing.md) | 封裝、驗證與發佈步驟 |
| [測試策略](testing-strategy.md) | 測試分層、命名與整理規則 |

## 其他治理文件

| 文件 | 用途 |
|------|------|
| [CHANGELOG](../CHANGELOG.md) | 版本變更與破壞性調整紀錄 |
| [THIRD-PARTY-NOTICES](../THIRD-PARTY-NOTICES.md) | 第三方授權與版權聲明 |
| [provenance/README.md](provenance/README.md) | 模組來源、授權與依據 |
| [Clean-room 來源索引](provenance/clean-room-source-index.md) | 公式評估、schema pattern、JSON Collaboration 與 managed conversion fidelity 的規格來源、不可複製來源與 golden 測試契約 |
