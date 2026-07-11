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

## 2. 站台結構

```text
api-docs/
  docfx.json          # metadata + build 設定；dest 為 ../artifacts/api-site
  filterConfig.yml    # 排除 schema-generated OdfKit.DOM wrapper
  locales.json        # 語系目錄（單一事實來源：default、locales、displayNames）
  index.md            # 站台首頁：語言總表 + API 入口（根層必須存在，否則模板 logo 連結 404）
  toc.yml             # 根層導覽列（必須存在，否則 default 模板不顯示搜尋框）
  <locale>/index.md   # 12 語系入口頁，front matter 設 _lang
  <locale>/guide.md   # 12 語系的使用、合規、安全與證據指南
  articles/           # 站台說明、授權等共用文章
  api/                # docfx metadata 產物（git 忽略，勿手改）
```

> **為什麼根層 `toc.yml` 與 `index.md` 是硬需求**：DocFX default 模板的搜尋框預設隱藏，
> 只在成功載入根層導覽 TOC 後才顯示（`docfx.js` 的 `loadNavbar()` → `showSearch()`）；
> 模板 logo 固定連到站台根 `index.html`。缺任一者即造成搜尋不可見與全站 404 連結。

## 3. 語系契約

- `locales.json` 是語系集合的單一事實來源；`Build-ApiDocs.ps1` 建置時驗證：
  1. 每個語系存在 `api-docs/<locale>/index.md` 與 `guide.md`；
  2. 根層 `index.md` 連到每個語系入口；
  3. 入口頁連到同語系指南與共用 API reference；
  4. 指南包含能力三維度、CC0、AI 產製、安全、互通邊界及證據入口；
  5. `docfx.json` 的 `build.content` 以語系目錄 glob 收錄兩頁及後續內容。
- 新增語系：於 `locales.json` 增列 → 新增 `<locale>/index.md` 與 `guide.md`
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

## 5. 本機建置與預覽

```powershell
pwsh eng/Build-ApiDocs.ps1                    # 完整建置（含八個組件）
pwsh eng/Build-ApiDocs.ps1 -NoRestore -SkipProjectBuild  # 組件未變更時的快速重建
dotnet docfx serve artifacts/api-site -p 8899 # 本機預覽
```

## 6. 已知限制

- DocFX default 模板 UI 字串（Search、Namespace 等）為英文寫死，語系入口頁的頁面外框
  維持英文；與內容回退政策一致。
- 站台不自動輸出 `hreflang` alternates；語系入口以根層語言總表互連。
- 舊版站台（`reference/` 前綴與站外語系落地頁）的 URL 已隨結構重整移除，
  不提供轉址。
