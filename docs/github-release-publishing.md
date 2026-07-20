# OdfKit GitHub Release 發佈指南

本文件說明 REL-1 套件如何驗證、封裝並以 **GitHub Release** 資產發佈。這是已備妥的
發佈程序；目前尚未建立公開 Release。

## 發佈策略

| 管道 | 現況 |
|------|------|
| **GitHub 原始碼** | 主要使用方式（`ProjectReference` / clone） |
| **CI 候選資產** | 目前可用的 commit-bound 驗證與人工決策輸入 |
| **GitHub Release** | 自動化已備妥；目前尚未建立公開 Release |
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

## 3. 發佈步驟（自動化 CD）

套件版本只由 `eng/OdfKit.Package.props` 取得，目前持續維持 `0.0.1` 滾動更新；發布流程不得為
WebFont 或 Release 另造 `0.0.2` 等第二套版本。Git tag／Release 是某一提交的不可變交付快照，
不是 `main` 的版本來源，也不表示後續 `0.0.1` 工作停止。

1. **確認共同版本來源**：`pwsh eng/Get-PackageVersion.ps1` 必須輸出 `0.0.1`；不要修改版本號。
2. **完成提交與驗證**：將變更提交並推送到 GitHub 的 `main` 分支，等待必要 CI 全綠。
3. **建立一次性交付快照**：僅在 `v0.0.1` tag 尚不存在時建立並推送；tag 必須與共同版本來源
   完全一致：
   ```powershell
   git tag -s v0.0.1 -m "OdfKit 0.0.1"
   git push origin v0.0.1
   ```
   若 tag 或 Release 已存在，禁止 force-move tag 或靜默覆寫同名資產。由於套件採 `0.0.1`
   滾動政策，後續 `main` 仍以原始碼與每次 CI 的 commit-bound artifact 交付；新的公開快照必須
   另經人工交付決策，但不得因此改成 `0.0.2`。
4. **追蹤發佈進度**：
   GitHub Actions 的 `GitHub Release CD` 工作流會被自動觸發。它會：
   - 驗證 Tag 版本與 props 檔版本是否對等。
   - 執行 NuGet 封裝結構檢查與消費端煙霧測試。
   - 將同批套件發布至隔離本機 feed，由乾淨 consumer 還原、執行 NuGet Audit 並驗證 SBOM。
   - 對套件、雜湊 manifest、SBOM 與彙整 ZIP 建立 GitHub Sigstore provenance；WebFont nupkg
     另繫結 SPDX SBOM attestation。
   - 自動建立 GitHub Release，並利用 `GITHUB_TOKEN` 上傳 `.nupkg`、`.snupkg`、`SHA256SUMS`、
     SPDX SBOM 與 ZIP 資產。

## 4. 消費端：首個公開 Release 建立後安裝套件

經人工核准並建立公開 Release 後，先將下載的 `.nupkg` 與 `.snupkg` 放在固定資料夾，例如
`C:\packages\odfkit`。本機開發可以用具名 package source：

```powershell
dotnet nuget add source C:\packages\odfkit --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

若團隊希望 repo 內可重現 restore，建議提交 `nuget.config` 範本並以相對路徑指向
CI 下載或快取的 Release 套件資料夾：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="odfkit-github-release" value="./.nuget/odfkit" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

CI 可先下載 GitHub Release 資產到 `.nuget/odfkit`，再執行 restore：

```powershell
dotnet restore --configfile nuget.config
dotnet build --no-restore
```

若不想提交 `nuget.config`，也可以在 CI 以 MSBuild 屬性臨時加入來源：

```powershell
dotnet restore -p:RestoreAdditionalProjectSources="$PWD/.nuget/odfkit"
```

多數情境仍建議直接以原始碼 `ProjectReference` 整合。

## 5. CI 驗證

`/.github/workflows/nuget-pack.yml` 於 PR 與 `main` 先在 Ubuntu 封裝一次並產生
`SHA256SUMS`，再透過短期 artifact 將相同的 `.nupkg`／`.snupkg` 快照分送至 Linux x64、
Windows x64、Windows ARM64 與 macOS ARM64。各 runner 會先驗證 SHA-256，再執行八套件
consumer smoke 與 Imaging native runtime smoke，避免各平台重新封裝出不同內容。

工作流程使用 `actions/checkout@v7`、`actions/upload-artifact@v7` 與
`actions/download-artifact@v8`，並透過共用複合 action
`./.github/actions/setup-dotnet-odfkit`（內部使用 `actions/setup-dotnet@v6` 與
`actions/cache@v6`）安裝 .NET SDK。artifact 只保留一天；NuGet restore cache 依作業系統與
明確 revision 分區，不依 CPU 架構、RID 或任意專案檔雜湊重複建立。

封裝 job 另執行 `eng/Test-WebFontReleaseRehearsal.ps1`：它使用 `dotnet nuget push` 將同批
nupkg 放入隔離本機 feed，以 package source mapping 強制 `OdfKit*` 只來自該 feed，再由乾淨
net10 consumer 還原與執行。外部相依清單取自同批 SPDX，不接受未列入 SBOM 的 package id；
NuGet Audit 使用 `all` 模式與 `https://data.nuget.org/v3/index.json`，audit 通訊錯誤以及
moderate、high、critical advisory 都會使演練失敗。演練會再從隔離 feed 撤除
`OdfKit.WebFonts.OpenType`、清空 consumer cache，要求 restore fail closed；之後只允許由同批
SHA-256 不可變套件快照復原，並重新通過 restore、build 與 run。這是可重現的發布撤除／復原與
漏洞偵測閘門，不等同真實 GitHub Release 復原、完整事件指揮或第三方安全審查。

`eng/Test-WebFontStandardsAndDependencies.ps1` 另在封裝前向 NuGet 官方 flat-container 查詢
WebFont 所有 direct package 的最新穩定版本，並向 GitHub 官方 API 驗證所有 `actions/*`
工作流程元件的最新穩定 release；Preview 只能存在於具精確版本、理由、移除條件與複查期限的
例外。規範政策每 90 天失效，OpenType 除版本外也必須追蹤官方 errata；IFT 依 2025-11-18
Candidate Recommendation Draft 追蹤，不把 draft 誤列為已支援功能。

## 版本策略

- 目前版本：**0.0.1**（`eng/OdfKit.Package.props`）
- 版本政策：維持 **0.0.1 滾動更新**，不得建立 `0.0.2` 路線或 WebFont 專屬版本來源
- 發佈前：`dotnet test` 全綠、`pwsh eng/Format-Safe.ps1`、`pwsh eng/Test-GpgSignatures.ps1`
- Git 標籤格式：`v{Version}`（例如 `v0.0.1`）
