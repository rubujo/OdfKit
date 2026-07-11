# API 文件站台（12 語系 GitHub Pages）

本文件定義 `api-docs/` 站台的結構、語系契約與品質閘門。站台由
[`eng/Build-ApiDocs.ps1`](../eng/Build-ApiDocs.ps1) 建置、
[`.github/workflows/api-docs.yml`](../.github/workflows/api-docs.yml) 於 main push 部署至
GitHub Pages。執行期例外訊息的 i18n 機制屬另一套系統，見
[i18n-localization.md](i18n-localization.md)。

## 1. 設計原則

- **單一 DocFX 站，站內多語系**：DocFX（OSS 版）沒有原生多語系功能；本站採社群慣例，
  以「每語系一個內容資料夾」承載語系入口，全站共用一份 API reference。
- **在地化概念內容**：12 個語系均提供各自的入口頁與使用、合規、安全及證據指南，
  不以只有翻譯標題或首句的落地頁視為完成。
- **API 成員內容回退**：API 成員內容由雙語（英文＋正體中文）XML 文件產生
  （政策見 `AGENTS.md`）。非英文／正體中文語系的指南會明確揭露此範圍，
  不宣稱所有 API 成員已翻譯。
- **禁止站外手工 HTML**：所有頁面（含語系入口）一律是 DocFX 內容頁，
  確保共用導覽列、搜尋與模板；不得再以腳本產生站外 HTML 落地頁。
- **以 DocFX 原生能力為界**：使用 `default`＋`modern`、content mapping、TOC、
  `fileMetadata`、`globalMetadata` 與 Markdown；不建立自製多語系引擎、JavaScript 語系
  切換器、`hreflang` 注入或模板 partial。
- **根路徑是語言選擇頁**：根首頁保留 12 語系入口，不設定 `redirect_url` 強制導向
  `zh-TW`。這可讓每位讀者在進站時自行選擇語系。
- **權威來源與受控譯文**：正體中文正式文件直接以 DocFX file mapping 納入；其餘 11 語系的
  譯文提交至 `api-docs/<locale>/`，並以來源 SHA-256、必要 token 與 CI 契約防止漂移。
- **目的語系必須明示**：共用 API 入口標示 `[en + zh-TW]`；各語系正式文件連向同語系譯文，
  並在頁內揭露正體中文權威來源。全站 navbar 維持雙語，不模擬語系 session。
- **不公開原始維護檔**：`project-docs/` 僅發布四個權威 HTML 頁面。權威頁所引用的
  次級 repo 文件與機器可讀 manifest 連到 GitHub `blob/main` 渲染頁，不複製成
  Pages 的 `.md` 或 `.json` 資源。

## 2. 站台結構

```text
api-docs/
  docfx.json          # metadata + build 設定；output 為 ../artifacts/api-site
  filterConfig.yml    # 排除 schema-generated OdfKit.DOM wrapper
  locales.json        # 語系目錄（單一事實來源：default、locales、displayNames）
  index.md            # 站台首頁：語言總表 + API 入口（根層必須存在，否則模板 logo 連結 404）
  toc.yml             # 根層雙語導覽列
  <locale>/index.md   # 12 語系入口頁，front matter 設 _lang
  <locale>/guide.md   # 12 語系的使用、合規、安全與證據指南
  <locale>/toc.yml    # 12 語系各自的 DocFX 導覽
  <locale>/articles/  # 授權譯文（zh-TW 使用共用權威頁）
  <locale>/project-docs/ # IP、安全、證據及第三方聲明譯文
  articles/           # 站台說明、授權等共用文章
  translations.json  # 權威來源、目的路徑、來源雜湊與不可翻譯 token
  api/                # docfx metadata 產物（git 忽略，勿手改）
```

`docs/ip-compliance.md`、`docs/security-limits.md`、`docs/evidence-index.md`、根目錄
`THIRD-PARTY-NOTICES.md` 與 `api-docs/articles/license.md` 是 `zh-TW` 唯一權威來源。其他語系
譯文的工作流程見 `api-docs/TRANSLATING.md`。

## 3. 語系契約

- `locales.json` 是語系集合的單一事實來源；`Build-ApiDocs.ps1` 建置時驗證：
  1. 每個語系存在 `api-docs/<locale>/index.md`、`guide.md` 與 `toc.yml`；
  2. 根層 `index.md` 連到每個語系入口；
  3. 入口頁連到同語系指南與共用 API reference；
  4. 指南包含能力三維度、CC0、AI 產製、安全、互通邊界及證據入口；
  5. 指南與 TOC 連到同語系的授權、IP、第三方、安全及證據頁；
  6. `docfx.json` 的 `fileMetadata._lang` 與 front matter `_lang` 一致。
  7. 語系 TOC 以 DocFX `uid: OdfKit` 指向 API，不得使用 `href: xref:*`；
  8. API 入口標示實際內容語系，正式譯文具備來源路徑與 SHA-256 metadata；
  9. `Test-ApiDocsTranslations.ps1` 驗證 55 份譯文、必要 token 與導覽沒有漂移。
- 新增語系：於 `locales.json` 增列 → 新增 `<locale>/index.md`、`guide.md` 與 `toc.yml`
  （front matter `_lang`）→ 在根層 `index.md` 語言表與 `docfx.json` content 增列。
  缺一步建置即失敗。
- 語系入口與指南必須使用該語系撰寫；固定內容包含 API 範圍、內容回退揭露、
  AI 產製、授權、第三方權利、非官方關係、無 SLA／indemnity、安全限制、
  互通邊界、能力三維度及可追溯證據。

## 4. 品質閘門（建置內建，任一失敗即建置失敗）

| 閘門 | 說明 |
|------|------|
| 語系契約驗證 | 見上節；防止語系入口孤立或遺漏。 |
| 未渲染頁面 href 修復 | docfx metadata 對被 `filterConfig.yml` 排除的型別（如 `OdfKit.DOM.*`）仍會在 references 輸出本地 href；建置時移除指向未渲染頁面的 href，使其渲染為純文字而非失效連結。 |
| `--warningsAsErrors` | DocFX build 警告視為錯誤。 |
| 站內連結健檢 | 掃描全站 HTML 相對 `href`／`src`，任何指向不存在檔案者即失敗。 |
| 原始資源與 xref | 禁止內部 `.md` 連結、`project-docs/` 非核准資源及 modern 輸出殘留的 `xref:*`。 |
| DocFX 版本 | 必須與 repo-local tool manifest 固定的 2.78.5 一致。 |
| modern 輸出 | 驗證 footer、sitemap、搜尋索引、頁數及 12 語系 HTML `lang`。 |
| 權威文件 | 驗證 IP、安全、證據與第三方聲明均建置為站內頁面。 |
| 翻譯契約 | 驗證 55 份譯文的來源雜湊、metadata、必要技術／法律 token 與同語系導覽。 |

## 5. 本機建置與預覽

```powershell
pwsh eng/Build-ApiDocs.ps1                    # 完整建置（含八個組件）
pwsh eng/Build-ApiDocs.ps1 -NoRestore -SkipProjectBuild  # 組件未變更時的快速重建
pwsh eng/Build-ApiDocs.ps1 -NoRestore -SkipProjectBuild -OutputDirectory artifacts/api-site-check
dotnet docfx serve artifacts/api-site -p 8899 # 本機預覽
```

## 6. 已知限制

- DocFX modern 模板 UI 字串（Search、Namespace 等）不納入 12 語系翻譯承諾。
- 站台不自動輸出 `hreflang` alternates；語系入口以根層語言總表互連。
- 單一 DocFX 站共用 API reference；非英文／正體中文語系只翻譯概念頁與 TOC，
  不宣稱 API member 已完整翻譯。
- DocFX 不保存讀者的語系狀態；navbar 維持全站共用雙語。跨語系目的地以
  `[en + zh-TW]` 明示，不加入自製 JavaScript 動態切換。Footer 連回語言選擇頁，正式文件由
  各語系 TOC 導覽。
- 舊版站台（`reference/` 前綴與站外語系落地頁）的 URL 已隨結構重整移除，
  不提供轉址。
