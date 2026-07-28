# ODF 1.0～1.4 規範支援邊界

本文件說明 OdfKit 對 ODF 規範的實際支援深度。OdfKit 的產品定位是受控的
ZIP／XML 函式庫，不是辦公軟體執行環境；因此「能安全載入、驗證、修改並保留 XML」
不等於「能重現 LibreOffice 或 Microsoft Office 的排版、計算及執行結果」。

## 狀態定義

| 狀態 | 意義 |
|------|------|
| 完整支援 | 在 ZIP／XML 邊界內已有公開 API、正反向測試及來回讀寫證據。 |
| 結構性支援 | 可辨識、驗證、建立或修改規範結構，但不承諾執行該結構的應用程式語意。 |
| 僅保留 | 載入及儲存時保留原始內容；核心不解譯或執行。 |
| 外部引擎 | 由 LibreOffice、瀏覽器、字型 shaping／rendering 或其它專用引擎完成。 |
| 刻意不支援 | 與 ZIP／XML 函式庫定位衝突，核心不會建立同等執行環境。 |

## 版本與一致性規範

| 版本 | 官方 schema | 內建規範 | 邊界 |
|------|-------------|----------|------|
| ODF 1.0 | 官方 RELAX NG 內建並產生版本化 metadata | `OasisOdf10` | 規格尚未採用 1.2 之後的 Strict／Extended 分類；不虛構此分類。 |
| ODF 1.1 | 官方 RELAX NG 內建並產生版本化 metadata | `OasisOdf11` | 同上；`manifest:version` 不會被錯誤要求。 |
| ODF 1.2 | 官方 RELAX NG 內建並產生版本化 metadata | `OasisOdf12Strict`、`OasisOdf12Extended`；保留 `OasisOdf12` 相容入口 | Strict 拒絕未定義的 ODF 命名空間內容；Extended 報告外來命名空間隔離風險。 |
| ODF 1.3 | 官方 RELAX NG 內建並產生版本化 metadata | `OasisOdf13Strict`、`OasisOdf13Extended`；保留 `OasisOdf13` 相容入口 | 與 1.2 相同，使用 1.3 schema 與版本限制。 |
| ODF 1.4 | 官方 RELAX NG 內建並產生版本化 metadata | `OasisOdf14Strict`、`OasisOdf14Extended` | 使用 ODF 1.4 正式標準的 schema、封裝與版本限制。 |

所有版本都以 `NamespaceURI` 與 `LocalName` 比對 XML，不依賴文件使用的前綴。
版本化文件、manifest 與 dsig schema provider 的來源、日期及輸出由
`tools/OdfSchemaGenerator/oasis-odf*-schema.json`、`oasis-odf*-manifest-schema.json`
與 `oasis-odf*-dsig-schema.json` 管理；外部 Jing corpus 是補充證據，
不是執行期相依套件。

## 按規格層分解

| 規格層 | 支援狀態 | 已實作 | 不代表 |
|--------|----------|--------|--------|
| 文件 XML／schema（ODF 1.0～1.4） | 結構性支援 | 版本偵測、官方 RELAX NG metadata、元素／屬性／pattern 驗證、Strict／Extended 命名空間診斷、flat document 驗證。 | 每一種應用程式語意都有高階 facade，或畫面能像素級一致。 |
| ZIP 封裝與 manifest | 完整支援 | `mimetype`、安全相對路徑、重複項目、root file-entry、payload 對應、media type、目錄路徑、加密 metadata、核心 XML 的 `office:version` 一致性，以及 ODF 1.0～1.4 官方 manifest RNG 機械驗證；ODF 1.2～1.4 另區分一般 Package 與 Extended Package 的 `META-INF` 規則。 | 具備任意第三方 RNG 的通用編譯器，也不能破解加密或證明內容沒有惡意行為。manifest schema 版本不必等同內容 XML 版本。 |
| 數位簽章 XML | 結構性支援 | ODF 1.2～1.4 官方 dsig RNG、任意 `META-INF/*signatures*` 入口驗證、XMLDSIG 建立／驗證、封裝引用與選配指令碼簽署工作流程。 | 作業系統或企業 PKI 信任；信任政策必須由呼叫端明確提供。ODF 1.0／1.1 規範沒有獨立 dsig RNG。 |
| OpenFormula | OdfKit Safe Large 受控評估 | ODF 1.2～1.4 的 `of:=` 與 ODF 1.0／1.1 常見的 `oooc:=` 語法剖析、參照範圍／交集／聯集、引號標籤、自動交集、文件／工作表／外部名稱、inline array、矩陣公式寫回、複數、Bessel 高階數值、奇數票息、pivot 兩種語法、Small 110／110、Medium 272／272、Large 388／388 累計強制函式名稱覆蓋、交易式重算、安全預算、取消、三種儲存策略，以及執行個體範圍自訂函式與整式後援。 | 函式名稱覆蓋不等於正式 Small／Medium／Large 一致性；`DDE` 是唯一明確的 `SecurityExcludedFunctions` 項目，固定傳回 `#N/A` 且不求值引數。完整正式一致性仍須以持續擴充的逐函式 corpus 證明所有限制、locale 與極端值。 |
| RDF／metadata | 結構性支援 | 封裝 RDF、文件 metadata 與未知 XML 內容保留。 | RDF 推論器、SPARQL 伺服器或網路資源擷取。 |
| 指令碼與事件 | 結構性支援＋僅保留 | 選配 Scripting 套件提供 ODF 1.0～1.4 巨集 CRUD、簽章、靜態政策與診斷。 | 在核心或 Scripting 套件內執行巨集；實際編譯／執行交由隔離的 LibreOffice／Python worker。 |
| 樣式、繪圖、簡報動畫 | 結構性支援 | 常用高階 API 與 XML 來回讀寫；未知合法內容盡量保留。 | 字型 shaping、分頁、動畫播放、SmartArt 佈局或像素級渲染。 |
| 資料庫、pivot、外部連結 | 結構性支援＋僅保留 | 描述結構、連線 metadata、pivot 定義及外部連結的受控讀寫。 | 啟動資料庫驅動程式、執行 SQL、擷取網路內容或完整 pivot 重算。 |

## 刻意不支援

下列功能不是「尚未補完的 XML 節點」，而是刻意不放入核心的執行環境能力：

- 巨集、Python、Java、UNO、SQL 或嵌入物件的程式碼執行。
- 任意外部 URI 的自動下載、外部資料來源刷新與連線憑證管理。
- 未經完整 corpus 證明的正式 OpenFormula Medium／Large 一致性，以及辦公軟體所有實作定義差異；核心已提供 272／272 與 388／388 強制函式名稱派送、矩陣公式寫回、Bessel 高階數值、奇數票息、pivot 兩種語法及資料表替代求值，但 `DDE` 維持明確的安全拒絕。
- 字型 shaping、物理分頁、列印、動畫播放及像素級版面配置。
- 憑證信任、撤銷可用性或惡意程式碼安全性的最終判定。
- 通用 RELAX NG 編譯器；核心使用由官方 ODF 1.0～1.4 schema 產生的受控 metadata。

需要上述能力時，應使用明確的外部引擎或選配擴充套件，並把它視為不同的安全邊界。
例如 LibreOffice 可用於真機互通、重算及渲染驗收，但其結果不會被包裝成核心 ZIP／XML
驗證已保證的事項。

## 證據入口

- 格式與工作流程：[ODF 格式支援矩陣](odf-format-support.md)
- 能力宣稱：[能力宣稱與證據索引](evidence-index.md)
- ODF Toolkit／Validator 對標：[ODF Toolkit 對標線](odf-toolkit-parity.md)
- ODF 1.4 逐章稽核：[ODF 1.4 逐章稽核紀錄](odf14-gap-audit.md)
- 高階 API 語意範圍：[`semantic-coverage.json`](semantic-coverage.json)
- LibreOffice 真機證據：[LibreOffice 互通矩陣](libreoffice-interop-matrix.md)
- OpenFormula 等級與擴充邊界：[OpenFormula 評估器支援](openformula-evaluator.md)

規範來源以 OASIS 正式文本為準：ODF 1.0、1.1、1.2、1.3 及
[ODF 1.4 正式標準](https://docs.oasis-open.org/office/OpenDocument/v1.4/)。
