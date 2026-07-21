# 從 NPOI 或 Independentsoft 遷移至 OdfKit

本指南只比較公開可觀察的工作流程與 ODF 規格概念，不讀取、翻譯或移植競品原始碼。
OdfKit 是 ODF-first SDK；遷移時應把既有程式意圖映射至任務與領域 API，而不是逐型別
仿造 OOXML 或商業 SDK 的物件模型。

## 入口選擇

| 原工作意圖 | OdfKit 入口 | 層級 |
| --- | --- | --- |
| 建立、載入、修改及儲存文件 | `TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument` | L1 |
| 建立表格、投影片、圖形、樣式或範本 | 各格式 builder 與 domain facade | L2 |
| 大量資料、低常駐記憶體讀寫 | `OdsStreamWriter`、`OdsStreamReader`、`OdtStreamWriter`、`OdtStreamReader` | L3 |
| 未知擴充、封裝 entry 或罕見規格語意 | typed DOM 與 `OdfPackage` | L3 escape hatch |

高頻工作原則上應在三個主要 statement 內完成；生命週期的 `using`、選項初始化與最終
儲存不計入領域操作 statement。若工作流必須手動維護 XML 前綴、style ID、shape ID 或
manifest entry，應先確認是否已有 L1／L2 facade，而不是直接複製舊模型的低階操作。

## 試算表遷移

- 使用 A1 位址處理使用者可見的 cell／range；集合索引一律從零開始。
- 單格使用 `GetCell("A1").SetValue(...)`，矩陣使用 `SetValues(...)`。
- 記錄匯入與讀回使用 object binding facade；轉型、重複標頭與未知欄位由 typed
  options 控制，問題寫入 `OdfObjectBindingReport.Diagnostics`。
- 大量循序資料改用 streaming adapter，避免先建立完整 DOM。
- OdfKit 儲存 OpenFormula；不應假設它等同試算表應用程式的完整計算引擎。

## 文字、簡報與繪圖遷移

- ODT 的查找取代、範本、書籤、欄位、圖片與文件附加應使用文字領域 facade。
- ODP 的投影片複製、移動及刪除由 `PresentationDocument` 管理 master、layout、媒體與
  package 關係；不要 clone 裸 XML node。
- ODG 的 connector、align、distribute、group 與 z-order 使用 page／drawing facade；
  不建立跨四格式的萬能 content abstraction。

## 樣式與匯出

樣式由 fluent facade 與 style engine 管理命名、繼承、去重與跨文件碰撞。呼叫端不應
建立 `CS1`、`N1` 之類的內部名稱。HTML、Markdown、SVG 與 PDF 位於 extension
套件；選擇 backend 時應保留 diagnostics，並區分 path 與 stream ownership。純 DOM
mutation 是同步操作，不提供沒有實際非同步 I/O 的假 async。

## 驗證遷移結果

遷移測試至少涵蓋建立、載入既有文件、修改、儲存重載、未知內容保留與錯誤輸入。
API 工作流與限制以 [`api-usability.json`](api-usability.json) 為機器可讀契約；規格語意
證據則以 [`semantic-coverage.json`](semantic-coverage.json) 為準。兩者都不能單獨取代
LibreOffice 互通、外部驗證器、封裝及跨 TFM 閘門。
