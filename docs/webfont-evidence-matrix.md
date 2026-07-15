# WebFont Phase 0～5 證據矩陣

> 更新日期：2026-07-16
>
> 版本政策：所有套件沿用 OdfKit 的 `0.0.1`、pack、Public API、文件與 CI 機制。

本矩陣只把可由 repository、GitHub Actions 或鎖定外部資料重現的結果列為「已實證」。
`experimental` 表示已有有界實作，但不構成 production、安全或容量承諾；「人工閘門」不能由
GitHub runner 代替。

| Phase | 狀態 | 已實證且可重跑 | Experimental／限制 | 人工或外部閘門 |
| --- | --- | --- | --- | --- |
| 0 中性契約與 corpus | 已實證 | Unicode sequence／IVS／PUA、opaque font ID、來源 SHA-256、版本化 JSON Profile、CNS 11643 EUC-TW provider；官方 CNS、Noto、IPAmj 等測試資料皆鎖版本與 SHA-256 | 未隨 nupkg 內建完整第三方資料或字型；自訂 C# provider 由部署者負責 | 真實客戶 corpus、`EUDC.TTE`／`educ.ttc` 實檔、個別字型散布法律審查 |
| 1 engine／format／browser | 部分已實證 | TTF／OTF／TTC face／CFF／variable source；WOFF2／WOFF／TTF／OTF；cmap、IVS format 14、GSUB／GPOS presence、可重現 hash；Playwright Chromium／Firefox／WebKit 載入與截圖 | Playwright WebKit 不等同 Safari；CFF2、COLR／CPAL、CBDT／CBLC、`sbix` 與 SVG table 目前由產品 engine 明確拒絕；AAT／Graphite 也沒有完整證據；產品 verifier 仍以 signature／大小為主 | Safari 實機、真實裝置、第三方惡意字型安全稽核、完整 shaping golden |
| 2 CLI／MSBuild／HTML | 已實證 | CLI、確定性 content scan、大小／scalar 上限、buildTransitive、content-addressed manifest、CSS hash、WOFF2 優先多來源 `src`、HTML requirement collector | HTML integration 是整份文件需求收集器，尚非所有 ODF run 的完整 coverage planner | 採用者實際 publish／CDN pipeline 驗收 |
| 3 ASP.NET Core／System.Web | 已實證 | ASP.NET Core DI／唯讀 endpoint／CORS／CORP／CSP helper／ETag／immutable／nosniff；net48 handler／helper；256 並行 GET；pack 與 DocFX 共用閘門 | System.Web 不在 IIS process 內產字；高流量應使用 CDN／object storage | 真實 IIS、反向代理、WAF、CDN 與組織 CSP 驗收 |
| 4 runtime worker | Experimental | bounded Channel、queue-full 快速拒絕、timeout、同鍵 single-flight、1,000 同鍵測試；外部 process tree 取消 | 不提供公開 request-time generation endpoint；不是 OS sandbox；沒有 distributed lock、durable object store 或跨節點 single-flight | 設計夥伴證明靜態資產不足後，才進行隔離容器、durable store、跨節點 load／fuzz |
| 5 發布／產品化 | 人工閘門 | v0.0.1 由共同 props 取得；所有 WebFont csproj 共用 package validation、Public API、snupkg、DocFX、Markdown 與 NuGet consumer gate；發布使用同一批已驗證 bytes 及 `SHA256SUMS` | GitHub tag／release 本身不可用同一 `v0.0.1` tag 重複建立；滾動驗證產物以 commit SHA artifact 區分 | 設計夥伴、市場採用、維護責任人、漏洞回應策略、第三方安全與法律審查 |

## 可重現指令

```powershell
dotnet test tests/OdfKit.WebFonts.Tests/OdfKit.WebFonts.Tests.csproj -c Release -f net10.0
dotnet run --project tests/OdfKit.WebFonts.SystemWebSmoke/OdfKit.WebFonts.SystemWebSmoke.csproj -c Release
pwsh eng/Test-WebFontSmoke.ps1 -RunBrowser
pwsh eng/Test-NuGetPack.ps1 -GenerateHashManifest
pwsh eng/Build-ApiDocs.ps1
pwsh eng/Test-MarkdownLinks.ps1
```

真實字型 smoke 的下載 URI、版本、授權與 SHA-256 位於 `eng/external-tools.json`。CNS 11643
官方資料集為不定期更新，因此上游內容改變時必須先人工審查，再更新版本與 hash；不得自動接受
未知內容。
