# ODS、ODT、ODP 標準效能基準

本文件定義 OdfKit 第一級標準效能格式與可重現工作負載。ODS、ODT、ODP 分別代表大量
表格資料、長篇結構化文字及複合簡報物件；三者的資料模型不同，因此不得合併為單一排名。
大型合成案例只用於效能與記憶體量測，真實世界互通性仍由 corpus 與 LibreOffice 測試負責。

## 標準矩陣

| 格式 | 標準規模 | 串流讀取 | 串流寫入 | DOM 載入／保存 | 語意檢查碼 |
|------|----------|----------|----------|-----------------|------------|
| ODS | 寫入 1,000,000 列、讀取 50,000 列 × 10 欄混合型別；另有三工作表複雜 DOM | `OdsStreamReader` | `OdsStreamWriter` | `SpreadsheetDocument` | 列號與所有儲存格值／XML 語意 |
| ODT | 100,000 個標題、段落與清單節點；另有表格與註解 DOM | `OdtStreamReader` | `OdtStreamWriter` | `TextDocument` | 節點類型、層級、樣式與文字 |
| ODP | 500 張結構密集投影片；100 張媒體密集投影片 | 不適用 | 不適用 | `PresentationDocument` | 投影片、文字框、圖形、圖片與講者備忘 |

ODP 沒有串流 API，基準只呈現 DOM 與封裝成本，不以列吞吐量與 ODS 比較。ODG、ODB、
ODC、ODF 與範本變體目前屬延伸層，不是第一級效能標準。

ODS 串流讀取目前沿用 64 MiB XML 字元安全上限，因此標準讀取資料集為 50,000 列；百萬列只用於
串流寫入。這個差異必須保留在報告中，不得將百萬列寫入能力描述為百萬列讀取能力。

## 量測層級

- `StandardPackageOpenBenchmarks` 單獨量測 `OdfPackage.Open`，避免把 ZIP 開啟成本混入文件模型。
- `StandardOdsBenchmarks`、`StandardOdtBenchmarks`、`StandardOdpBenchmarks` 使用 BenchmarkDotNet
  取得穩態耗時與 GC 配置量。
- `eng/Benchmark-StandardDocuments.ps1` 讓大型情境各自在獨立子行程執行，輸出完整冷啟動
  成本、配置量、峰值工作集、封裝大小、解壓 XML 大小及語意檢查碼。
- 每次讀取與來回保存都必須完成語意檢查碼計算，避免以漏讀內容換取較漂亮數字。

執行完整獨立行程報告：

```powershell
pwsh eng/Benchmark-StandardDocuments.ps1
```

執行同格式 BenchmarkDotNet 穩定量測：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdsBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdtBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdpBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardPackageOpenBenchmarks*"
```

## 結果政策

第一版由手動命令與每週排程蒐集資料，不設定固定毫秒或記憶體失敗門檻。固定機器累積至少
三次結果後，才能為各格式選擇一個穩定案例加入回歸基準；調整門檻時必須保留硬體、OS、
.NET 版本與原始 JSON。既有 ODS 對 MiniExcel／ClosedXML 報告仍是跨格式參考，不屬於本文件的
同格式內部基準矩陣。
