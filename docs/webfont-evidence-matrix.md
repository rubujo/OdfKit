# WebFont Phase 0～5 證據矩陣

> 更新日期：2026-07-18
>
> 版本政策：所有套件沿用 OdfKit 的 `0.0.1`、pack、Public API、文件與 CI 機制。

本矩陣只採計純 C#／.NET 產品路徑。2026-07-17 的本機與 GitHub Actions 證據使用官方 CNS 11643
宋體／楷體、鎖定 SHA-256 的 Noto Arabic／Devanagari、managed verifier 與 Playwright 瀏覽器；
不採計 FontTools／Python 產物。遠端整合證據見
[CI Build & Test 29584424505](https://github.com/rubujo/OdfKit/actions/runs/29584424505)。

狀態定義：

- 「已實證」：repository 與 CI 可在產品邊界內重現。
- 「Experimental」：已有有界實作，但缺少完整格式、部署或客戶證據。
- 「未完成」：設計或周邊 API 已存在，但必要產品能力尚未由 managed engine 證明。
- 「人工閘門」：GitHub runner 無法代替的法律、安全、市場或實際部署決策。

| Phase | 目前狀態 | 已實證 | 尚缺的產品證據 | 人工或外部閘門 |
| --- | --- | --- | --- | --- |
| 0 中性契約與 corpus | 已實證（工程） | 中性 sequence／IVS／PUA 契約、opaque font ID、實際 bytes SHA-256、版本化 JSON Profile、CNS EUC-TW provider、Windows EUDC `.tte`／`.ttf` 登錄來源 resolver、clean-room 紀錄；合法 CNS TrueType fixture 已複製為 `.tte` 通過 managed 產字與 cache 重用；Windows resolver 的 `netstandard2.0` 資產已由 net8 與 net48 consumer 載入；WebFont 專案共用 `0.0.1` 與 pack；CI 掃描 nupkg 的 managed-only 邊界並鎖定完整 NuGet 相依版本與 nuspec 授權宣告 | 真實客戶 corpus 的持續擴充 | 個別字型法律審查、客戶合法 `EUDC.TTE` 實檔與登錄關聯驗收 |
| 1 engine／format／browser | Experimental | 有界 sfnt／TTC／OTC parser、WOFF、standalone WOFF2 null／`glyf`／`loca`／`hmtx` transform，以及 WOFF2 collection directory／face table index／共享 transformed `glyf`／`loca` 配對輸入正規化；W3C 鎖版 corpus 與 Google Fonts production Noto Sans v42 Latin／Devanagari 已在 managed decoder 通過，Devanagari 要求字串、GSUB／GPOS 與 642 glyph 均有 JSON 證據；兩個 production WOFF2 另各執行 64 組固定種子 byte mutation，只有有效解析或有界 `InvalidDataException`／`NotSupportedException`；TrueType composite 與 GSUB output closure、`cmap` 4／12／14、TTF／OTF／zlib WOFF／net10 WOFF2、checksum、`fsType`；TrueType Variable Fonts 已實作 `gvar` short／long offset 重建與 `fvar`／`gvar` axis 驗證；standalone／OTC face 的 CID-keyed 靜態 CFF 1.0 已實作 INDEX／DICT／FDSelect／charset／Private／Subrs／Type 2 operand verifier；standalone／OTC face 且含 VariationStore 的 CFF2 variable 已實作 32-bit INDEX、DICT、FDSelect 0／3／4、ItemVariationData、`vsindex`、`blend`、subroutine 與 retain-GIDs；省略 VariationStore 的非變動 CFF2 已用規格 fixture 驗證，並明確拒絕無 store 的 `vsindex`／`blend`；官方 Noto CJK OTC face、Source Han Sans CFF／CFF2、Noto Color Emoji bitmap／COLRv1 均產生 deterministic TTF／OTF／WOFF／WOFF2；真實 CNS 宋體／楷體 Ext-B／PUA、IPAmj IVS、雙 CNS face TTC 與由相同官方 face 建立的 WOFF2 collection 均成功選 face 並輸出獨立資產；複雜文字與 color font 採保留完整 glyph ID space 的 correctness-first 路徑；Chromium／Firefox／WebKit 已完成 CFF、Arabic／Devanagari variable、Source Han CFF2 及 COLRv1 color 的來源／subset 像素差分，color 案例另要求非灰階像素 | WOFF2 transformed collection 的擴充 corpus 與直接 collection 輸出；CFF／CFF2 尚缺 compact INDEX／DICT／subroutine 重寫、名稱式 CFF、可再散布的真實非變動 CFF2 三瀏覽器 corpus 與 coverage-guided fuzz；COLR／SVG／sbix 分格式 closure 與 aggressive pruning；CBDT bitmap-only 不可作 Firefox 跨瀏覽器輸出；擴大 variable／complex-script corpus | Safari 實機、第三方惡意字型安全稽核；名稱式 CFF、靜態 CFF2 真實部署、SVG 主動內容與尚未具真實 corpus 的 color 模型 |
| 2 CLI／MSBuild／HTML | 已實證（套件 consumer） | Managed CLI／MSBuild、內容掃描、canonical `unicode-range`、固定 Unicode bucket、可設定 `font-display`／fallback metrics、選擇啟用 preload、manifest、CSS、TTF／WOFF／WOFF2、byte-identical 重建與 verifier；同批 `0.0.1` nupkg 的 library 與 dotnet tool clean consumer 均真實產字，build/run 使用 `--no-restore`；真實 CNS managed engine 的 128 路有界負載以 16 個 generation key 實證 87.5% cache hit；官方 67,492,856-byte CNS Ext-B 字型的 2,048 個 supplementary-plane scalar 依 256 code-point bucket 產生 8 個 deterministic WOFF2，冷啟字型／CSS／manifest payload 為 2,154,873 bytes，CSS 2,104 bytes、manifest 2,945 bytes | 不同內容分布與網路條件的傳輸比較；採用者 publish／CDN pipeline 驗收 | 採用者 publish／CDN pipeline 驗收 |
| 3 ASP.NET Core／System.Web | Experimental | ASP.NET Core managed dynamic endpoint 已用真實字型通過 401／429／hash GET 與 256 路平行 immutable GET；兩平台的 manifest、CSS 與字型資產皆實際通過 GET／HEAD、SHA-256 ETag、無本文 304 與原始 bytes 一致性，並拒絕無效 UTF-8 CSS；generation POST 的成功、錯誤、401 與 429 皆使用 `no-store`；System.Web net48 Handler 已通過 API key、allowlist、格式拒絕、內容定址 GET，並在 CLR net48 由同批 nupkg 以官方 CNS Ext-B TrueType 與 Source Han Sans CFF2 真字型產生及 managed verifier 驗證 TTF／OTF／WOFF；ProjectReference 與 nupkg consumer 使用隔離的 `obj`／`bin`，避免綠燈載入舊套件；官方 CNS Ext-B 已部署至隔離的 IIS Express Integrated pipeline，實際通過頁面編譯、`web.config` API key、401、動態 TTF／WOFF、GET／HEAD、SHA-256、ETag 與 304；ASP.NET Core 亦以完整隔離 `applicationhost.config` 及 ANCM V2 實際通過 In-Process／Out-of-Process、JSON／環境組態優先序、401、動態 WOFF2 與相同 cache 契約；靜態 fallback 與兩平台範例存在 | 完整 IIS Classic mode 與含峰值資源量測的持續負載 | 完整 IIS Integrated／Classic 客戶環境、反向代理、身分提供者、WAF、CDN 與組織 CSP 驗收 |
| 4 runtime worker | Experimental | bounded Channel、single-flight、檔案 cache；兩個 OS process 僅產生一次、lease owner 強制終止後接手；verifier 拒絕截斷、內容損毀與超限展開長度的真實 WOFF2；真實來源、TTF／WOFF／WOFF2 與直接 CFF／CFF2 table 共 672 組 deterministic mutation 經有界結構入口驗證，無越界或非預期例外；所有有效 CFF／CFF2 產物另由公開 verifier 逐 glyph 驗證 CharString；真實 CNS managed engine 的 128 路有界負載記錄 elapsed、CPU、peak working set 與 allocation JSON 證據並套用 CI 資源上限 | 長時間 soak、不同硬體容量基線、coverage-guided fuzz；多節點維持關閉 | object store、fencing token、跨節點失敗注入、第三方安全測試 |
| 5 發布／產品化 | Experimental（工程發布閘門已實證） | 共用版本／pack／Public API／snupkg／DocFX／Markdown 機制；OpenType 雙 TFM、Public API、全量 pack consumer、WebFont 真實產字 clean consumer 與 net48 CLR smoke 已在遠端 CI 通過；同批 nupkg 產生可重現 SPDX 2.3 SBOM，並由 Linux、Windows x64／ARM64 與 macOS ARM64 consumer 對提交、SHA-256、32 個跨平台相依聯集及 nuspec 授權宣告重新驗證；發布演練將同批 nupkg 實際 push 至隔離本機 feed，以 SBOM 精確 source mapping 供乾淨 net10 consumer／CLI 還原，並以 NuGet Audit `all` 對 moderate 以上 advisory 與 audit 來源故障 fail closed；演練撤除 OpenType nupkg、清空 cache、證明 restore 失敗，再由同批 SHA-256 快照復原並重新 restore／build／run；OpenType／Unicode／W3C 規範每 90 天複查，direct NuGet 在線上 CI 比對官方最新穩定版，唯一 Preview 傳遞相依具到期例外；tag workflow 會為發布資產建立 GitHub Sigstore provenance，並將 SPDX SBOM attestation 繫結 WebFont nupkg | 真實 GitHub Release 交付與平台端復原快照 | 設計夥伴、市場採用、維護責任、外部法律與第三方安全審查 |

## 目前不能宣稱的事項

- WebFont 套件已完成或已達 production-ready。
- OdfKit 已完整支援所有 OTF／CFF／CFF2／variable／color 模型、未知 WOFF2 transform version、
  所有 transformed WOFF2 collection 或直接 collection 輸出。
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
pwsh eng/Test-WebFontReleaseRehearsal.ps1
pwsh eng/Build-ApiDocs.ps1
pwsh eng/Test-MarkdownLinks.ps1
```

真實 Managed CNS 與三瀏覽器證據可由下列指令重現：

```powershell
pwsh eng/Test-WebFontSmoke.ps1 -RunBrowser
pwsh eng/Test-WebFontFormatMatrix.ps1
pwsh eng/Test-WebFontLayoutBrowserSmoke.ps1
pwsh eng/Test-WebFontIisExpressSmoke.ps1 -FontPath <font> -SourceSha256 <sha256>
pwsh eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1 -FontPath <font> -SourceSha256 <sha256>
pwsh eng/Test-WebFontPackageConsumer.ps1 -FontPath <font> -SourceSha256 <sha256>
```
