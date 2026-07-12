# OdfKit 版本與交付資訊

本文件整理 OdfKit 的交付管道、版本來源、安裝模式與相關參考文件。

## 1. 交付管道

| 項目 | 現況 |
|------|------|
| 原始碼 | GitHub repository |
| 套件資產 | GitHub Release |
| 公開套件倉庫 | 目前 **未發佈至 nuget.org** |

## 2. 版本身分與相容性資訊

| 面向 | 內容 |
|------|------|
| SDK 版本身分 | `v0.0.1` 持續維護；版本號不代表等待未來版本補完必要能力 |
| 權威版本來源 | `eng/OdfKit.Package.props` 與各 Release 標籤 |
| 版本變更紀錄 | [CHANGELOG](../CHANGELOG.md) |
| 目標框架 | 核心與擴充套件：`net10.0;netstandard2.0`；CLI／主測試：`net10.0;net8.0`；Windows consumer smoke：`net48` |
| 功能相容性基準 | [ODF 格式支援矩陣](odf-format-support.md) 與測試證據 |

`v0.0.1` 是持續完滿的產品身分，不是不可變快照。公開 API 在正式交付前仍可直接修正；每次
變更必須同步更新 PublicAPI 基線、測試、文件、範例與 CHANGELOG。GitHub tag／Release 若存在，
只表示特定提交的交付快照，不決定目前 `main` 是否完滿。

## 3. 安裝模式

| 模式 | 適用情境 | 參考文件 |
|------|----------|----------|
| 原始碼 / `ProjectReference` | 直接整合原始碼或追蹤最新主幹 | [快速開始](getting-started.md) |
| GitHub Release `.nupkg` | 以固定版本資產建立內部套件來源 | [GitHub Release 發佈指南](github-release-publishing.md) |
| 本機 / CI 驗證 | 建置、測試、封裝與 smoke 驗證 | [NuGet 相容矩陣](nuget-compatibility-matrix.md) |

## 4. 常見查閱文件

| 主題 | 建議先查 |
|------|----------|
| 安裝 / 相依 | [快速開始](getting-started.md)、[NuGet 相容矩陣](nuget-compatibility-matrix.md) |
| 功能是否支援 | [ODF 格式支援矩陣](odf-format-support.md) |
| 渲染 / LibreOffice 後端 | [Rendering 後端部署](rendering-backend-deployment.md) |
| 封裝 / Release | [GitHub Release 發佈指南](github-release-publishing.md) |
| 測試 / corpus | [Interop Corpus 總覽](interop-corpus.md)、[Corpus Manifest 規則](corpus-manifest.md) |
