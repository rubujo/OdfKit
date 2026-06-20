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

## 3. 乾跑 Release 準備

```powershell
pwsh eng/Publish-GitHubRelease.ps1
```

會驗證封裝、依 `eng/OdfKit.Package.props` 推導標籤（例如 `v0.0.1`），並產生 `artifacts/OdfKit-nuget-packages.zip`。

## 4. 建立 GitHub Release

需已安裝並登入 [GitHub CLI](https://cli.github.com/)（`gh auth login`）：

```powershell
pwsh eng/Publish-GitHubRelease.ps1 -CreateRelease
```

亦可手動指定標籤：

```powershell
pwsh eng/Publish-GitHubRelease.ps1 -CreateRelease -Tag v0.0.1 -Title "OdfKit 0.0.1"
```

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
