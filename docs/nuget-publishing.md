# OdfKit NuGet 發佈指南

本文件說明如何將 REL-1 可發佈套件驗證、封裝並推送至 [nuget.org](https://www.nuget.org/)。

## 前置條件

- .NET SDK 10.0.x
- PowerShell 7+
- （僅推送）nuget.org API 金鑰，具 **Push** 權限

套件清單與雙 TFM 矩陣見 [`nuget-compatibility-matrix.md`](nuget-compatibility-matrix.md)。

## 1. 本機驗證（必做）

```powershell
pwsh eng/Test-NuGetPack.ps1 -Configuration Release
```

驗收項目：

- 六個 `.nupkg` 皆含 `lib/net10.0` 與 `lib/netstandard2.0`
- `OdfKit` 含 `.snupkg` 符號套件
- `artifacts/nuget-consumer-smoke` net8.0 專案可引用本機套件並執行

## 2. 僅封裝（不推送）

```powershell
pwsh eng/Pack-NuGet.ps1 -Configuration Release
```

輸出目錄：`artifacts/nuget/`。

## 3. 乾跑發佈流程

```powershell
pwsh eng/Publish-NuGet.ps1
```

會先執行完整驗證，再列出將推送的檔案，**不會**上傳。

## 4. 推送至 nuget.org

```powershell
$env:NUGET_API_KEY = '<your-api-key>'
pwsh eng/Publish-NuGet.ps1 -Push
```

- 使用 `--skip-duplicate`，重複版本不會覆寫既有套件。
- 同時推送 `.nupkg` 與 `.snupkg`（若存在）。

## 5. CI 驗證

`/.github/workflows/nuget-pack.yml` 於 `main` 與 PR 執行 `Test-NuGetPack.ps1`，確保封裝結構不退化。

## 版本策略

- 目前穩定版：**1.0.0**（`eng/OdfKit.Package.props` 與 `OdfKit/OdfKit.csproj`）
- 發佈前請確認 `dotnet test` 全綠，並執行 `pwsh eng/Format-Safe.ps1`
- 修訂版本時同步更新所有可發佈專案之 `<Version>`

## 安全注意事項

- **勿**將 API 金鑰寫入 repo 或提交至 Git。
- 建議使用 nuget.org 專用 API 金鑰，並設定過期時間。
- 推送前再次確認 `THIRD-PARTY-NOTICES.md` 與套件 README 內容正確。

## 發佈後驗證

```powershell
dotnet add package OdfKit --version 1.0.0
```

若套件剛上傳，nuget.org 索引可能需數分鐘才會出現。