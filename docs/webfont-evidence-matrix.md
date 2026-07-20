# WebFont Phase 0～5 證據矩陣

> 更新日期：2026-07-20
>
> 版本政策：所有套件沿用 OdfKit 的 `0.0.1`、pack、Public API、文件與 CI 機制。

本矩陣只採計純 C#／.NET 產品路徑。截至 2026-07-20 的 repository 證據使用官方 CNS 11643
宋體／楷體、鎖定 SHA-256 的 Noto Arabic／Devanagari／Bengali／Khmer／Thai、AFDKO
`seac`／靜態 CFF2、managed verifier 與 Playwright 瀏覽器；
不採計 FontTools／Python 產物；新 corpus 仍須由本次提交的 GitHub Actions 重現。既有遠端整合
基線見
[CI Build & Test 29584424505](https://github.com/rubujo/OdfKit/actions/runs/29584424505)。

WebFont 套件的「工程完成」只以 repository 內可由 .NET、NuGet、瀏覽器與 CI 重現的能力判定。
客戶 WAF／CDN 現場、Safari 實機、第三方安全／法律審查、市場採用及正式對外發布營運不屬於
套件工程目標；表格最後一欄只提供採用者驗收參考，不得用來阻擋工程完成。

狀態定義：

- 「已實證」：repository 與 CI 可在產品邊界內重現。
- 「Experimental」：已有有界實作，但仍缺少 repository 內可重現的格式或正確性證據。
- 「未完成」：設計或周邊 API 已存在，但必要產品能力尚未由 managed engine 證明。
- 「採用者外部驗收」：GitHub runner 無法代替的法律、安全、市場或實際部署決策；不計入工程完成。

| Phase | 目前狀態 | 已實證 | 尚缺的工程證據 | 採用者外部驗收（目標外） |
| --- | --- | --- | --- | --- |
| 0 中性契約與 corpus | 已實證（工程） | 中性 sequence／IVS／PUA 契約、opaque font ID、實際 bytes SHA-256、版本化 JSON Profile、CNS EUC-TW provider、Windows EUDC `.tte`／`.ttf` 登錄來源 resolver、clean-room 紀錄；合法 CNS TrueType fixture 已複製為 `.tte` 通過 managed 產字與 cache 重用；Windows resolver 的 `netstandard2.0` 資產已由 net8 與 net48 consumer 載入；WebFont 專案共用 `0.0.1` 與 pack；CI 掃描 nupkg 的 managed-only 邊界並鎖定完整 NuGet 相依版本與 nuspec 授權宣告 | 無 | 個別字型法律審查、客戶合法 `EUDC.TTE` 實檔與登錄關聯驗收，以及客戶 corpus 的持續擴充 |
| 1 engine／format／browser | 已實證（有界格式矩陣） | 有界 sfnt／TTF／OTF／TTC／OTC／WOFF／WOFF2 parser、writer、checksum、`fsType` 與 face selection；W3C transformed WOFF2 standalone／collection 逐表比對；TrueType composite、GSUB output、`cmap` 4／12／14、`gvar` short／long offset；CID-keyed／名稱式 CFF、`seac` closure、variable／非變動 CFF2；CNS 宋體／楷體 Ext-B／PUA、IPAmj IVS、Noto CJK、Source Han、Source Code Pro、AFDKO `seac`／靜態 CFF2、Noto Arabic／Devanagari／Bengali／Khmer／Thai 與 color corpus 均鎖定來源、版本、授權及 SHA-256；32 個真實 managed 格式案例產生 deterministic TTF／OTF／WOFF／WOFF2；Chromium／Firefox／WebKit 比較 CFF／CFF2、五種 complex script、variable axis 與 COLRv1，Chromium 另驗證 `sbix`、Firefox 另驗證 OpenType SVG，且不渲染組合記錄為 `browser-unavailable`；AAT／Graphite layout 明確拒絕；`cmap` format 4 建構的連續範圍合併、超過 16-bit `length` 時省略 format 4、encoding record 規格排序，以及解析端的 `segCount`／length 約束與 segment 不重疊規則，均由 `CmapMappingTests` 以 20,000 字大字集與負向輸入釘住；`cmap` 規模路徑另以鎖定的 Source Han Sans TC `2.005R` 取得三瀏覽器實機證據——12,000 字單片子集（`(0,3)`／`(3,1)`／`(3,10)`）與 9,000 字稀疏子集（format 4 依規格省略，僅 `(3,10)`）在 Chromium／Firefox／WebKit 均通過 `FontFace` 狀態與逐字 canvas 墨跡檢查，同批截斷資產的負向對照在三個引擎均正確遭拒，證明量測非觀察 fallback | 無。直接 collection 輸出與跨 color 技術轉碼不屬於獨立瀏覽器資產契約；EBDT／EBLC、SVG aggressive pruning、CFF subroutine 剪枝及更多 script corpus 是選用優化或擴充，不作工程完成條件 | Safari 實機、任意客戶字型、第三方惡意字型安全稽核與 production 部署。Coverage-guided fuzz 只作外部可選證據 |
| 2 CLI／MSBuild／HTML | 已實證（套件 consumer） | Managed CLI／MSBuild、內容掃描、canonical `unicode-range`、固定 Unicode bucket、可設定 `font-display`／fallback metrics、選擇啟用 preload、manifest、CSS、TTF／WOFF／WOFF2、byte-identical 重建與 verifier；同批 `0.0.1` nupkg 的 library 與 dotnet tool clean consumer 均真實產字，build/run 使用 `--no-restore`；真實 CNS managed engine 的 128 路有界負載以 16 個 generation key 實證 87.5% cache hit；官方 67,492,856-byte CNS Ext-B 字型的 2,048 個 supplementary-plane scalar 依 256 code-point bucket 產生 8 個 deterministic WOFF2，冷啟字型／CSS／manifest payload 為 2,154,873 bytes，CSS 2,104 bytes、manifest 2,945 bytes | 無 | 不同內容分布與網路條件的傳輸比較，以及採用者 publish／CDN pipeline 驗收 |
| 3 ASP.NET Core／System.Web | 已實證（有界託管） | ASP.NET Core managed dynamic endpoint 已用真實字型通過 401／429／hash GET 與 256 路平行 immutable GET；兩平台的 manifest、CSS 與字型資產皆實際通過 GET／HEAD、SHA-256 ETag、無本文 304 與原始 bytes 一致性，並拒絕無效 UTF-8 CSS；generation POST 的成功、錯誤、401 與 429 皆使用 `no-store`；System.Web net48 Handler 已通過 API key、allowlist、格式拒絕、內容定址 GET，並在 CLR net48 由同批 nupkg 以官方 CNS Ext-B TrueType 與 Source Han Sans CFF2 真字型產生及 managed verifier 驗證 TTF／OTF／WOFF；ProjectReference 與 nupkg consumer 使用隔離的 `obj`／`bin`，避免綠燈載入舊套件；官方 CNS Ext-B 已部署至隔離的 IIS Express Integrated／Classic pipeline，實際通過頁面編譯、`web.config` API key、401、動態 TTF／WOFF、GET／HEAD、SHA-256、ETag 與 304；Classic 使用 ASP.NET 4 ISAPI mapping 與 `system.web/httpHandlers`，兩種 pipeline 均由獨立 `applicationhost.config` 指定應用程式集區；ASP.NET Core 亦以完整隔離 `applicationhost.config` 及 ANCM V2 實際通過 In-Process／Out-of-Process、JSON／環境組態優先序、401、動態 WOFF2 與相同 cache 契約；四種 IIS hosting 路徑各以 16 路併發、至少 256 且最多 1,024 次 immutable GET 執行日常有界 hosted load；另以同一官方 CNS 字型、16 路併發在本機持續驗證 Integrated 10,928／30.033 秒、Classic 10,960／30.036 秒、In-Process 8,288／30.047 秒與 Out-of-Process 4,096／67.107 秒；每個回應皆重算 SHA-256，並記錄總 bytes、elapsed、CPU、initial／peak working set；Out-of-Process 同時計入 IIS proxy 與 Kestrel `dotnet` 子程序；較長負載在 GitHub 僅允許手動 opt-in；靜態 fallback 與兩平台範例存在 | 無 | 長時間 soak、不同硬體容量基線，以及完整 IIS Integrated／Classic 客戶環境、反向代理、身分提供者、WAF、CDN 與組織 CSP 驗收 |
| 4 runtime worker | 已實證（有界單機） | bounded Channel、single-flight、檔案 cache；兩個 OS process 僅產生一次、lease owner 強制終止後接手；verifier 拒絕截斷、內容損毀與超限展開長度的真實 WOFF2；真實來源、TTF／WOFF／WOFF2 與直接 CFF／CFF2 table 共 736 組 deterministic mutation 經有界結構入口驗證，無越界或非預期例外；所有有效 CFF／CFF2 產物另由公開 verifier 逐 glyph 驗證 CharString；真實 CNS managed engine 的 128 路有界負載記錄 elapsed、CPU、peak working set 與 allocation JSON 證據並套用 CI 資源上限；多節點能力依原始範圍維持關閉閘門，不宣稱支援 | 無 | 長時間 soak、不同硬體容量基線、object store、fencing token、跨節點失敗注入與第三方安全測試；coverage-guided fuzz 僅為外部可選證據，不納入套件或 CI 目標 |
| 5 工程發布 | 已實證（工程閘門） | 共用版本／pack／Public API／snupkg／DocFX／Markdown 機制；OpenType 雙 TFM、Public API、全量 pack consumer、WebFont 真實產字 clean consumer 與 net48 CLR smoke 已在遠端 CI 通過；同批 nupkg 產生可重現 SPDX 2.3 SBOM，並由 Linux、Windows x64／ARM64 與 macOS ARM64 consumer 對提交、SHA-256、32 個跨平台相依聯集及 nuspec 授權宣告重新驗證；發布演練將同批 nupkg 實際 push 至隔離本機 feed，以 SBOM 精確 source mapping 供乾淨 net10 consumer／CLI 還原，並以 NuGet Audit `all` 對 moderate 以上 advisory 與 audit 來源故障 fail closed；演練撤除 OpenType nupkg、清空 cache、證明 restore 失敗，再由同批 SHA-256 快照復原並重新 restore／build／run；OpenType／Unicode／W3C 規範每 90 天複查，direct NuGet 在線上 CI 比對官方最新穩定版，唯一 Preview 傳遞相依具到期例外；tag workflow 會為發布資產建立 GitHub Sigstore provenance，並將 SPDX SBOM attestation 繫結 WebFont nupkg | 無 | 真實 GitHub Release、平台端復原快照、設計夥伴、市場採用、維護責任、外部法律與第三方安全審查 |

## 目前不能宣稱的事項

- 不得把有界 repository 工程閘門完成解讀為任意客戶字型、任意部署或 production-ready。
- 不得宣稱 OdfKit 已完整支援所有 OTF／CFF／CFF2／variable／color 模型或未知 WOFF2 transform version。
- 不得宣稱 OdfKit 會直接輸出 TTC／OTC／WOFF2 collection；產品契約只抽取指定 face 並輸出獨立資產。
- 不得宣稱所有 CFF／CFF2、PostScript variable、color font 或所有語系的任意 complex-script shaping 已支援。
- 不得宣稱單機檔案 lease 等同 distributed lock，或 GitHub runner 的 load test 等同真實容量承諾。
- 不得宣稱本機三瀏覽器 smoke 等同跨平台實機、第三方安全稽核或 production-ready。

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
pwsh eng/Test-WebFontIisExpressSmoke.ps1 -Pipeline Integrated -FontPath <font> -SourceSha256 <sha256>
pwsh eng/Test-WebFontIisExpressSmoke.ps1 -Pipeline Classic -FontPath <font> -SourceSha256 <sha256>
pwsh eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1 -FontPath <font> -SourceSha256 <sha256>
pwsh eng/Test-WebFontIisSustainedLoad.ps1
pwsh eng/Test-WebFontPackageConsumer.ps1 -FontPath <font> -SourceSha256 <sha256>
```
