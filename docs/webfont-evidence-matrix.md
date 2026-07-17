# WebFont Phase 0～5 證據矩陣

> 更新日期：2026-07-17
>
> 版本政策：所有套件沿用 OdfKit 的 `0.0.1`、pack、Public API、文件與 CI 機制。

本矩陣只採計純 C#／.NET 產品路徑。2026-07-17 的本機與 GitHub Actions 證據使用官方 CNS 11643
宋體／楷體、鎖定 SHA-256 的 Noto Arabic／Devanagari、managed verifier 與 Playwright 瀏覽器；
不採計 FontTools／Python 產物。遠端整合證據見
[CI Build & Test 29564127333](https://github.com/rubujo/OdfKit/actions/runs/29564127333)。

狀態定義：

- 「已實證」：repository 與 CI 可在產品邊界內重現。
- 「Experimental」：已有有界實作，但缺少完整格式、部署或客戶證據。
- 「未完成」：設計或周邊 API 已存在，但必要產品能力尚未由 managed engine 證明。
- 「人工閘門」：GitHub runner 無法代替的法律、安全、市場或實際部署決策。

| Phase | 目前狀態 | 已實證 | 尚缺的產品證據 | 人工或外部閘門 |
| --- | --- | --- | --- | --- |
| 0 中性契約與 corpus | 已實證（工程） | 中性 sequence／IVS／PUA 契約、opaque font ID、實際 bytes SHA-256、版本化 JSON Profile、CNS EUC-TW provider、Windows EUDC `.tte`／`.ttf` 登錄來源 resolver、clean-room 紀錄；合法 CNS TrueType fixture 已複製為 `.tte` 通過 managed 產字與 cache 重用；Windows resolver 的 `netstandard2.0` 資產已由 net8 與 net48 consumer 載入；WebFont 專案共用 `0.0.1` 與 pack；CI 掃描 nupkg 的 managed-only 邊界並鎖定完整 NuGet 相依版本與 nuspec 授權宣告 | 真實客戶 corpus 的持續擴充 | 個別字型法律審查、客戶合法 `EUDC.TTE` 實檔與登錄關聯驗收 |
| 1 engine／format／browser | Experimental | 有界 sfnt／TTC parser、TrueType composite 與 GSUB output closure、`cmap` 4／12／14、TTF／OTF／zlib WOFF／net10 WOFF2、checksum、`fsType`；TrueType Variable Fonts 已實作 `gvar` short／long offset 重建與 `fvar`／`gvar` axis 驗證；standalone CID-keyed 靜態 CFF 1.0 已實作 INDEX／DICT／FDSelect／charset／Private／Subrs／Type 2 operand verifier；含 VariationStore 的 CFF2 variable 已實作 32-bit INDEX、DICT、FDSelect 0／3／4、ItemVariationData、`vsindex`、`blend`、subroutine 與 retain-GIDs；Source Han Sans 2.005R 靜態 CFF／CFF2 variable 均產生 OTF／WOFF／WOFF2；真實 CNS 宋體／楷體 Ext-B／PUA、IPAmj IVS 與雙 CNS face TTC 成功；複雜文字保留完整 glyph ID space、`cmap`、GDEF／GPOS／GSUB；Chromium／Firefox／WebKit 已完成 CFF、Arabic／Devanagari TrueType variable 的 `wdth`／`wght` 及 Source Han CFF2 的 300／500／700 `wght` Canvas／DOM 來源與 subset 像素差分 | CFF／CFF2 尚缺 compact INDEX／DICT／subroutine 重寫、名稱式 CFF 與 coverage-guided fuzz；擴大 variable／complex-script corpus；尚未做 layout table 重寫或 aggressive glyph pruning | Safari 實機、第三方惡意字型安全稽核；OTC／名稱式 CFF、無 VariationStore 的 CFF2、color 維持不支援 |
| 2 CLI／MSBuild／HTML | 已實證（套件 consumer） | Managed CLI／MSBuild、內容掃描、canonical `unicode-range`、固定 Unicode bucket、可設定 `font-display`／fallback metrics、選擇啟用 preload、manifest、CSS、TTF／WOFF／WOFF2、byte-identical 重建與 verifier；同批 `0.0.1` nupkg 的 library 與 dotnet tool clean consumer 均真實產字，build/run 使用 `--no-restore`；真實 CNS managed engine 的 128 路有界負載以 16 個 generation key 實證 87.5% cache hit | 真實 CNS 大型 corpus 的 CSS／manifest 大小與瀏覽器傳輸量基準；採用者 publish／CDN pipeline 驗收 | 採用者 publish／CDN pipeline 驗收 |
| 3 ASP.NET Core／System.Web | Experimental | ASP.NET Core managed dynamic endpoint 已用真實字型通過 401／429／hash GET 與 256 路平行 immutable GET；System.Web net48 Handler 已通過 API key、allowlist、格式拒絕、內容定址 GET，並在 CLR net48 由同批 nupkg 以官方 CNS Ext-B TrueType 與 Source Han Sans CFF2 真字型產生及 managed verifier 驗證 TTF／OTF／WOFF；靜態 fallback 與兩平台範例存在 | 真實 IIS classic／Integrated 部署與含峰值資源量測的持續負載 | 反向代理、身分提供者、WAF、CDN 與組織 CSP 驗收 |
| 4 runtime worker | Experimental | bounded Channel、single-flight、檔案 cache；兩個 OS process 僅產生一次、lease owner 強制終止後接手；verifier 拒絕截斷、內容損毀與超限展開長度的真實 WOFF2；真實來源、TTF／WOFF／WOFF2 與直接 CFF／CFF2 table 共 672 組 deterministic mutation 經有界結構入口驗證，無越界或非預期例外；所有有效 CFF／CFF2 產物另由公開 verifier 逐 glyph 驗證 CharString；真實 CNS managed engine 的 128 路有界負載記錄 elapsed、CPU、peak working set 與 allocation JSON 證據並套用 CI 資源上限 | 長時間 soak、不同硬體容量基線、coverage-guided fuzz；多節點維持關閉 | object store、fencing token、跨節點失敗注入、第三方安全測試 |
| 5 發布／產品化 | Experimental（工程發布閘門已實證） | 共用版本／pack／Public API／snupkg／DocFX／Markdown 機制；OpenType 雙 TFM、Public API、全量 pack consumer、WebFont 真實產字 clean consumer 與 net48 CLR smoke 已在遠端 CI 通過；同批 nupkg 產生可重現 SPDX 2.3 SBOM，並由 Linux、Windows x64／ARM64 與 macOS ARM64 consumer 對提交、SHA-256、32 個跨平台相依聯集及 nuspec 授權宣告重新驗證 | 正式 NuGet 發布演練、漏洞回應與 SBOM 消費流程演練 | 設計夥伴、市場採用、維護責任、外部法律與第三方安全審查 |

## 目前不能宣稱的事項

- WebFont 套件已完成或已達 production-ready。
- OdfKit 已完整支援 OTF／CFF／CFF2／所有 variable／color font 或所有 TTC／WOFF2 變體。
- 所有 CFF／CFF2、PostScript variable、color font 或所有語系的任意 complex-script shaping 已支援。
- 單機檔案 lease 等同 distributed lock，或 GitHub runner 的 load test 等同真實容量承諾。
- 本機三瀏覽器 smoke 等同跨平台實機、第三方安全稽核或 production-ready。

## 升級證據入口

實作順序、格式拒絕矩陣、授權準入與 clean consumer 定義見
[WebFont 純 .NET 架構契約](webfont-managed-architecture.md)。每個 Phase 只有在該文件列出的必要
證據全部進入 CI 後才能更新狀態。

目前仍可執行的中性驗證：

```powershell
dotnet test tests/OdfKit.WebFonts.Tests/OdfKit.WebFonts.Tests.csproj -c Release -f net10.0
dotnet run --project tests/OdfKit.WebFonts.SystemWebSmoke/OdfKit.WebFonts.SystemWebSmoke.csproj -c Release
pwsh eng/Test-NuGetPack.ps1
pwsh eng/Test-WebFontSupplyChain.ps1
pwsh eng/Build-ApiDocs.ps1
pwsh eng/Test-MarkdownLinks.ps1
```

真實 Managed CNS 與三瀏覽器證據可由下列指令重現：

```powershell
pwsh eng/Test-WebFontSmoke.ps1 -RunBrowser
pwsh eng/Test-WebFontFormatMatrix.ps1
pwsh eng/Test-WebFontLayoutBrowserSmoke.ps1
pwsh eng/Test-WebFontPackageConsumer.ps1 -FontPath <font> -SourceSha256 <sha256>
```
