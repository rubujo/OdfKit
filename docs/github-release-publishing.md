# OdfKit GitHub Release 發佈指南

本文件說明 REL-1 套件如何驗證、封裝並以 **GitHub Release** 資產發佈。

## 發佈策略

| 管道 | 現況 |
|------|------|
| **GitHub 原始碼** | 主要使用方式（`ProjectReference` / clone） |
| **GitHub Release** | 附加 `.nupkg`、`.snupkg` 與彙整 zip，供本機 NuGet feed |
| **nuget.org** | **非目前目標**；未規劃公開推送 |

套件清單與雙 TFM 矩陣見 [`nuget-compatibility-matrix.md`](nuget-compatibility-matrix.md)。

## 1. 本機驗證（必做）

```powershell
pwsh eng/Test-NuGetPack.ps1 -Configuration Release
pwsh eng/Test-GpgSignatures.ps1
```

## 2. 封裝

```powershell
pwsh eng/Pack-NuGet.ps1 -Configuration Release
```

輸出：`artifacts/nuget/`（檔名含目前版本，例如 `OdfKit.0.0.1.nupkg`）。

## 3. 發佈步驟 (自動化 CD)

本專案現已導入 **Git Tag 觸發的自動化 CD 發佈流程**。發佈步驟如下：

1. **更新 props 檔版本號**：在 `eng/OdfKit.Package.props` 中更新 `<Version>`（例如改為 `0.0.2`）。
2. **提交變更並 Push**：將版本變更提交並推送到 GitHub 的 `main` 分支。
3. **本機或線上打 Tag 觸發 CD**：
   在命令列中建立並推送對應的 Git Tag（格式必須為 `v*`，例如 `v0.0.2`，且必須與 props 中的版本號完全一致）：
   ```powershell
   git tag v0.0.2
   git push origin v0.0.2
   ```
4. **追蹤發佈進度**：
   GitHub Actions 的 `GitHub Release CD` 工作流會被自動觸發。它會：
   - 驗證 Tag 版本與 props 檔版本是否對等。
   - 執行 NuGet 封裝結構檢查與消費端煙霧測試。
   - 在雲端自動進行 NuGet 打包。
   - 自動在 GitHub 上建立 Release，並利用 `GITHUB_TOKEN` 將 `.nupkg`、`.snupkg` 以及打包好的 ZIP 資產上傳至該 Release 中。

## 5. 消費端：自 Release 安裝套件

```powershell
dotnet nuget add source C:\path\to\downloaded-packages --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

多數情境仍建議直接以原始碼 `ProjectReference` 整合。

## 6. CI 驗證

`/.github/workflows/nuget-pack.yml` 使用 `actions/checkout@v6`、`actions/setup-dotnet@v5`，於 PR 與 `main` 執行 `Test-NuGetPack.ps1`。

## 版本策略

- 目前版本：**0.0.1**（`eng/OdfKit.Package.props`）
- 發佈前：`dotnet test` 全綠、`pwsh eng/Format-Safe.ps1`、`pwsh eng/Test-GpgSignatures.ps1`
- Git 標籤格式：`v{Version}`（例如 `v0.0.1`）
