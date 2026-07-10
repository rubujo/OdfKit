# ODF 1.4 規格覆蓋契約

OdfKit 的定位是 C# / .NET 的 ODF 界 NPOI：提供任務導向的高階 API，並保留可完整表達規格與
foreign extension 的低階存取路徑。本契約是持續維護的 `v0.0.1` 完滿條件，不是未來版本路線圖。

## 分層契約

| 層級 | 範圍 | 完滿條件 | 永久非目標 |
| --- | --- | --- | --- |
| L0 | schema coverage / typed DOM | ODF 1.1～1.4 官方元素與屬性可由 schema provider 盤點，並可透過 generated wrapper 或 schema-aware DOM 表達、讀寫、保留及 round-trip | 每個元素都有專屬高階 C# API |
| L1 | package lifecycle | 24 種主要 extension 可偵測、建立、載入、儲存、驗證與 round-trip | 內建完整 Office layout、rendering 或 calculation engine |
| L2 | high-level facade | Text、Spreadsheet、Presentation、Drawing、Chart、Image、Formula、Database 的列明常用工作流可直接由 C# API 完成 | 完整動畫引擎、ODB 執行引擎、CAS、完整 TeX parser 與 3D 設計器 |
| L3 | interop behavior | 對 LibreOffice、Microsoft Office ODF 與 portable editing 提供可追溯的真機證據或實務風險提示 | 宣稱跨套件像素級一致 |

## L0：Schema 與 DOM

`OdfTypedDomCoverage.Build()` 與 CLI `typed-dom-coverage` 必須穩定輸出 schema 元素、屬性、typed
wrapper 與 attribute datatype coverage。完滿判定採「可表達且可保留」而不是「每個節點都有
facade」：沒有專屬 wrapper 的 foreign 或冷門節點仍須能由 `OdfUnknownElement`／`OdfElement`
安全表示並 round-trip。

Coverage 報告不得含未分類缺口。差異只能歸為：已有 typed wrapper、由 schema-aware DOM
涵蓋、需要修復的契約缺陷，或明列的高階 API 非目標。

## L1：格式生命週期

生命週期矩陣以 [ODF 格式支援矩陣](odf-format-support.md) 為權威來源，涵蓋：

- ODT／OTT／ODM／OTH／FODT。
- ODS／OTS／FODS。
- ODP／OTP／FODP。
- ODG／OTG／FODG。
- ODC／OTC／FODC。
- ODF／OTF／FDF。
- ODI／OTI／FODI。
- ODB。

每個格式都必須有建立、載入、儲存、驗證與 round-trip 證據。Template 與 Flat XML 變體可沿用
主格式語意 facade，但不得缺少相容入口。LibreOffice 不接受的獨立格式以 package、schema 與
round-trip 證據驗收，且必須誠實記錄上游限制。

## L2：高階工作流

高階 API 只承諾文件化的實務工作流：TemplateBinder、物件與工作表資料繫結、嵌入式／獨立圖表、
常用文字與樣式、投影片與繪圖操作、Formula／Database 的建立與常見編輯。完整重算、排版、
資料庫執行與協作合併演算法不因 schema 存在而成為 facade 契約。

API 工作流與限制見 [API Reference](reference/index.md)；新增 facade 時必須在同一變更中補上
reference、cookbook 或 scenario test。

## L3：互通風險

`OdfPracticalCompatibilityValidator` 是實務風險提示器，不取代 OASIS schema 驗證，也不保證
跨套件呈現一致。LibreOffice 真機測試由專用 workflow 持續執行；Microsoft Office、PDF pixel
diff 與大型外部 corpus 依可用環境執行，不混入快速 smoke。

## 持續閘門

- CLI：`dotnet run --project tools/OdfKit.Cli --framework net10.0 -- typed-dom-coverage --format json`
- 契約測試：`OdfCoverageContractTests`
- CI：`typed-dom-coverage.yml` 產生可追溯 artifact。
- 規格更新：先更新 generator、coverage status 與差異稽核，再判斷是否需要高階 API。
- `main` 不得含契約內 `planned` 狀態或未分類 coverage 差異。
