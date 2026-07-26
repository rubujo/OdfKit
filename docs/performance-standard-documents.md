# ODS、ODT、ODP 標準效能基準

本文件定義 OdfKit 第一級標準效能格式與可重現工作負載。ODS、ODT、ODP 分別代表大量
表格資料、長篇結構化文字及複合簡報物件；三者的資料模型不同，因此不得合併為單一排名。
大型合成案例只用於效能與記憶體量測，真實世界互通性仍由 corpus 與 LibreOffice 測試負責。

## 標準矩陣

| 格式 | 標準規模 | 串流讀取 | 串流寫入 | DOM 載入／儲存 | 語意檢查碼 |
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
- `eng/Benchmark-StandardDocuments.ps1` 讓大型情境各自在獨立子處理程序執行，輸出完整冷啟動
  成本、配置量、峰值工作集、封裝大小、解壓 XML 大小及語意檢查碼。
- 報告外層使用 schema v2，將 commit、量測時間、workflow run、runner、runtime、CPU 型號與 artifact 身分和
  九個 schema v1 情境結果一起儲存，避免不同提交或執行環境的樣本被混為同一基線。
- 每次讀取與來回儲存都必須完成語意檢查碼計算，避免以漏讀內容換取較漂亮數字。

執行完整獨立處理程序報告：

```powershell
pwsh eng/Benchmark-StandardDocuments.ps1
```

相依套件已事先還原而目前不宜連線套件來源時，可使用既有 dependency graph：

```powershell
pwsh eng/Benchmark-StandardDocuments.ps1 -NoRestore
```

執行同格式 BenchmarkDotNet 穩定量測：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdsBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdtBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardOdpBenchmarks*"
pwsh eng/Benchmark-Stable.ps1 -Filter "*StandardPackageOpenBenchmarks*"
```

## 最新本機重新驗證

2026-07-26 於 Windows 10.0.26200、.NET 10.0.10 執行
`pwsh eng/Benchmark-StandardDocuments.ps1 -NoRestore`。Release 建置為
0 警告、0 錯誤；下列九個大型情境各自在獨立子處理程序執行一次，且都完成
決定性語意檢查碼驗證。這些是單機冷啟動量測，不是跨機器效能承諾。

| 情境 | API／模型 | 規模 | 耗時 | GC 累積配置量 | 峰值工作集 |
|------|----------|------|------|----------------|------------|
| ODS 串流寫入 | `OdsStreamWriter` | 1,000,000 列 × 10 欄 | `5,793.4 ms` | `716.4 MB` | `316.0 MB` |
| ODS 串流讀取 | `OdsStreamReader` | 50,000 列 × 10 欄 | `2,758.3 ms` | `699.0 MB` | `56.8 MB` |
| ODS DOM 來回讀寫 | `SpreadsheetDocument` | 三工作表複雜 DOM | `2,154.5 ms` | `330.4 MB` | `220.3 MB` |
| ODT 串流寫入 | `OdtStreamWriter` | 100,000 個結構節點 | `281.3 ms` | `9.2 MB` | `39.5 MB` |
| ODT 串流讀取 | `OdtStreamReader` | 100,000 個結構節點 | `320.2 ms` | `101.2 MB` | `46.0 MB` |
| ODT DOM 來回讀寫 | `TextDocument` | 20,000 個複合節點 | `1,313.3 ms` | `337.2 MB` | `86.5 MB` |
| ODP 結構寫入 | `PresentationDocument` | 500 張結構密集投影片 | `373.5 ms` | `49.4 MB` | `58.2 MB` |
| ODP 結構讀取 | `PresentationDocument` | 500 張結構密集投影片 | `297.3 ms` | `56.9 MB` | `70.1 MB` |
| ODP 媒體 DOM 來回讀寫 | `PresentationDocument` | 100 張媒體密集投影片 | `223.1 ms` | `22.8 MB` | `63.6 MB` |

Reader 覆蓋依產品 API 分層：ODS 與 ODT 使用專用串流 Reader；ODP 目前沒有串流
Reader，因此以 `PresentationDocument` 載入及遍歷完整結構。三種格式另由
`StandardPackageOpenBenchmarks` 分離量測 `OdfPackage.Open` 的 ZIP 封裝開啟成本，
避免把封裝與文件模型讀取混為同一指標。

## 結果政策

第一版由手動命令與每週排程蒐集資料，不設定固定毫秒或記憶體失敗門檻。固定機器累積至少
三次結果後，才能為各格式選擇一個穩定案例加入回歸基準；調整門檻時必須保留硬體、OS、
.NET 版本與原始 JSON。既有 ODS 對 MiniExcel／ClosedXML 報告仍是跨格式參考，不屬於本文件的
同格式內部基準矩陣。
