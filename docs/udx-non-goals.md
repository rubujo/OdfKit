# OdfKit 專案 UDX 非功能性目標說明 (udx-non-goals.md)

本文件明確列出 OdfKit 在邁向「極致好用 (UDX)」的過程中，**不予納入**實作範圍的功能模組與非目標（Non-Goals）。此設計取捨旨在保持核心程式庫的輕量性與高效能，並避免開發資源的重複投入。

---

## 1. 高保真物理分頁排版引擎
* **說明**：計算文字在 PDF 或實體列印時的精確換頁、孤行控制（Orphan Control）、雙欄對齊等物理分頁計算極為龐大，且受限於作業系統的字型渲染引擎差異。
* **決策**：OdfKit 將不自行開發完整的物理分頁排版器。物理分頁的渲染（如 ODT 轉 PDF 或圖片）將完全交由後端的 LibreOffice（本地進程或雲原生容器）進行高保真轉譯。

## 2. 試算表樞紐表與公式記憶體彙總重算核心
* **說明**：ODS 試算表中的樞紐表（Pivot Table）、大範圍公式重算（Formula Re-calculation）等記憶體彙總引擎涉及複雜模型依賴圖與高效計算優化。
* **決策**：OdfKit 不在此階段自研記憶體內的樞紐表彙總重算引擎。圖表與資料的公式計算通常交由辦公軟體（如 Microsoft Excel、LibreOffice Calc）於開檔時由其核心執行實質彙總重算。

## 3. SmartArt 智慧圖形與複雜形狀佈局器
* **說明**：ODF 規格對 SmartArt 與複合三維圖形的定義極為模糊，且 Microsoft Office 與 LibreOffice 對此類圖形的 XML 實作互不相容。
* **決策**：OdfKit 不予支援 SmartArt 佈局器封裝，以避免跨平台開檔時的版面混亂。

## 4. ODF Toolkit 風格 JSON Collaboration operations merge
* **說明**：TDF ODF Toolkit 的 Collaboration API 將 ODT 語意變更序列化為 JSON operations（`addParagraph`、`delete`、`format` 等），供 web office 場景做變更交換與 merge。這與 OdfKit 核心已支援的 `META-INF/manifest.rdf` 封裝 RDF metadata 屬不同層次；後者由核心 `OdfRdfMetadata` 負責，前者需另行設計 position 模型與 merge 語意。
* **目前範圍**：JSON Collaboration 不再是純 non-goal。OdfKit 將其限定為 extension-scoped compatibility subset，由選用套件 `OdfKit.Extensions.Collaboration` 對標 TDF ODF Toolkit 的公開 operation 名稱、wire shape 與 reference JSON；核心 `OdfKit` 不新增依賴。
* **已納入子集合**：匯入端接受裸陣列與 TDF 相容的 `{ "changes": [...] }` 封包；匯出端預設維持裸陣列，並可用 `OdtOperationCompatibilityOptions` 選擇 TDF changes 封包。第一波支援 `addParagraph`、`addText`、`addTab`、`addLineBreak` 與基本 `format` range 字元屬性（含前景色、背景色、大小寫轉換、small-caps 與上標／下標），並容忍 `documentLayout`、`addFontDecl`、`addStyle` 等 metadata-only operation。第二波支援單段落範圍 `delete`、最上層段落 `splitParagraph`／`mergeParagraph`、metadata-only `addListStyle`、基本清單段落，以及固定尺寸文字表格的 `addTable`／`addRows`／`addCells` 依序填值。
* **仍屬非目標**：完整多人協同演算法、任意衝突合併、undo stack、OT、CRDT、跨段落刪除／移動、drawing operation、動態表格擴張，以及 header/footer/note selection 的完整語意，仍不納入核心與本輪承諾。授權策略採 clean-room：只使用 TDF 公開文件、operation 名稱、wire shape 與 reference JSON 作行為對標，不複製 Java 原始碼。
