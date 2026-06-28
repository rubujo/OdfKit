# Changelog

本檔案依 [Keep a Changelog](https://keepachangelog.com/) 慣例，記錄 OdfKit 對外可見的重大里程碑。

## [0.0.1] - 未發行（GitHub Release 資產，非 nuget.org）

### 新增

- **核心 ODF 支援**：24 種主要 ODF extension（ODT/ODS/ODP/ODG 及其範本、母片、Flat XML、次格式變體）之偵測、建立、載入、保存、驗證與 round-trip。
- **四主格式高階 API**：ODT、ODS、ODP、ODG 已達 `complete` 分級，涵蓋常用建立、編輯、樣式、公式、加密、追蹤修訂、條件格式、樞紐分析表等場景。
- **規範可信度**：ODF 1.1/1.2/1.3/1.4 官方 RELAX NG schema 驗證、profile 規則（OASIS Strict/Extended、ISO/IEC 26300、EU、ROC Taiwan）、200+ corpus fixtures。
- **安全性**：PBKDF2（≥ 50,000 次迭代）、Argon2id、OpenPGP（RSA/ElGamal/ECDH X25519 與傳統曲線）加密；XAdES 數位簽章與時間戳記驗證；XXE／Zip Slip／OOM DoS 防禦。
- **轉換與互通**：
  - OOXML：ODT↔DOCX、ODS↔XLSX（含具名段落／字元樣式、公式、圖表）。
  - PresentationML：ODP↔PPTX（投影片、主題色票、表格、動畫時間軸與 build list）。
  - Managed-first 淨室轉換：ODT↔Markdown／RTF、ODG→SVG，LibreOffice 降為 fallback。
  - LibreOffice headless 互通矩陣（26.x）、OOXML 視覺 golden file 比對。
- **協作格式**：ODT ↔ JSON operations 雙向轉換（對標 ODF Toolkit CLI，`OdfKit.Extensions.Collaboration`）。
- **RDF／中繼資料**：`manifest.rdf` triple CRUD 與 SPARQL 查詢橋接（`OdfKit.Extensions.Rdf`）。
- **效能**：`OdsStreamWriter` 串流寫入記憶體佔用 < 1MB；公式剖析採 `ref struct` + `ReadOnlySpan<char>` 零配置設計；XML 標籤字串池化；ZIP 載入 `ArrayPool` 緩衝。
- **套件與發行**：8 個套件（`OdfKit` 核心 + 7 個 `OdfKit.Extensions.*`）雙 TFM（`net10.0` + `netstandard2.0`）NuGet 封裝，透過 GitHub Release 資產發佈（非 nuget.org）。

### 架構

- 採用協作者抽取模式拆分上帝類別。
- 所有公開 `*Async` 方法統一帶 `CancellationToken cancellationToken = default`。
- 測試套件依分層命名規則整理，移除歷史開發階段命名與重複測試檔。
