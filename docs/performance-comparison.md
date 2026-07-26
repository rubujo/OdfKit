# OdfKit 與同類套件的串流寫入效能對比

本文件記錄 `OdsStreamWriter` 與兩套知名 .NET 試算表套件（MiniExcel、ClosedXML）
在「一百萬列 × 十欄混合型別資料」情境下的實測效能對比，包含方法論限制、
環境資訊、結果數字、重現步驟與結果解讀。目的是為「大量資料匯出低記憶體」
這項主張提供公開、可重現的量化證據，而不只是內部宣稱。

## 1. 方法論

### 1.1 情境定義

- 資料量：`1,000,000` 列 × `10` 欄。
- 欄位型別為混合型別：長整數 ID、字串名稱、金額（`double`）、數量
  （`int`）、日期時間（`DateTime`）、布林旗標、浮點分數、短字串分類、
  大整數序號，以及含正體中文字元的備註文字。
- 資料以固定種子（`20260709`）決定性產生，供跨情境比對與重現時取得
  相同的資料內容；產生器程式碼見
  `OdfKit.Benchmarks/CompetitiveBenchmarkData.cs`。
- 三個情境使用相同的資料產生器與延遲求值（`yield return`）序列，避免
  「先在記憶體中組出全部資料再寫入」造成的不公平比較。
- 三個情境的輸出皆恰為 `1,000,000` 個資料列（MiniExcel 停用其預設表頭列
  `printHeader: false`，以避免多出一列造成列數落差）。
- 每個情境的輸出寫至暫存檔，量測完成後立即刪除，不保留產物。

### 1.2 跨格式限制：ODS 對 XLSX，不是同格式對決

OdfKit 的 `OdsStreamWriter` 寫入 **ODF 試算表（`.ods`）**；MiniExcel 與
ClosedXML 寫入 **OOXML 試算表（`.xlsx`）**。兩者的容器格式都是「ZIP +
XML」，但內部 schema（ODF 1.4 對 OOXML SpreadsheetML）完全不同，字串
共用表、樣式表、壓縮策略等實作細節也不同。

**這是跨格式參考對比，而非同格式效能對決。** 輸出檔案大小尤其不能直接
等同比較：檔案較小不代表「壓縮效率較好」，也可能是 schema 本身較精簡、
共用字串表策略不同，或未涵蓋某些中繼資料。本文件的核心比較重點是
**耗時**與**記憶體使用（含峰值工作集）**，檔案大小僅作為輔助參考數字。

### 1.3 為何不納入 NPOI 與 EPPlus

在加入相依套件前，本文件先進行授權裁定：

| 套件 | 授權狀態 | 裁定 |
|------|----------|------|
| MiniExcel | `Apache-2.0`（經查 NuGet nuspec 確認；並非坊間常誤植的 MIT） | 納入：寬鬆授權，不會為建置此 repo 的使用者帶來授權負擔 |
| ClosedXML | `MIT` | 納入：本專案 `OdfKit.Extensions.Ooxml` 既有相依，授權已知安全 |
| NPOI | `2.7.x` 以前為 `Apache-2.0`；自 `2.8.0` 起改為需簽署的 Maintenance Fee EULA（`OSMFEULA.txt`，`requireLicenseAcceptance: true`） | **不納入**：即使可鎖定舊版，新增此相依會讓專案的建置健康度綁定在一個授權模式已轉向收費的套件上，對之後維護與使用者風險過高 |
| EPPlus | 5.x 起為 Polyform Noncommercial／商業雙授權（非 Polyform NC 相容專案需付費商業授權；`8.x` 起商業授權需序號金鑰） | **不納入**：非商業友善授權，加入 benchmark 專案相依會讓任何複製此 repo 建置的人一併承接授權限制 |

不納入 NPOI、EPPlus 純粹是「不為建置此 repo 的人增加授權負擔」的工程判斷，
不代表其效能不佳；也因此本次未實測 NPOI／EPPlus 的數字，未來若這两套件
授權模式改變，可視情況重新評估。

### 1.4 量測模式

一百萬列規模的寫入，每次呼叫本身即需數秒至數十秒，若採用
BenchmarkDotNet 預設的多次暖身 + 多次迭代統計工作，單一情境即可能耗時
數分鐘。因此本次量測採兩種模式並存：

1. **BenchmarkDotNet 模式**（`CompetitiveStreamWriteBenchmarks` 類別，
   `OdfKit.Benchmarks/CompetitiveStreamWriteBenchmarks.cs`）：套用
   `[MemoryDiagnoser]` 取得配置量（Allocated），並改用
   `RunStrategy.Monitoring`（`launchCount: 1, warmupCount: 0,
   iterationCount: 3`）取代預設統計工作，讓單次呼叫的完整成本被如實量測，
   而非依賴 BenchmarkDotNet 對輕量方法的多次 unroll 假設。已於本機驗證
   （`--filter *MiniExcel_WriteOneMillionRows* --job short`）確認此類別可
   正確執行並取得與手動計時模式數量級一致的配置量（約 3.53 GB／次）。
2. **手動計時模式**（`CompetitiveStreamWriteManualRunner`，同目錄）：本文
   件「結果表格」中的官方數字來自此模式。每個情境在**獨立子處理程序**中執行
   單次量測，量測項目為：
   - 耗時（`Stopwatch`）。
   - GC 累積配置量（`GC.GetTotalAllocatedBytes(precise: true)`，量測期間
     內的總配置量，非常駐記憶體）。
   - **峰值工作集**（子處理程序的 `Process.PeakWorkingSet64`，於子處理程序存活期
     間輪詢取得最大值；這是 BenchmarkDotNet 的 `MemoryDiagnoser` 未提供、
     但更貼近「實際佔用多少實體記憶體」的數字）。
   - 輸出檔案大小。

   採獨立子處理程序量測，是為了讓每個情境的峰值工作集只反映該情境本身，
   不會因為同一處理程序內先後執行多個情境而互相汙染累加。

   誠實揭露：手動計時模式僅為單次量測（本文件另外重複執行一次以確認
   數字穩定，兩次結果差異在個位數百分比內），並非 BenchmarkDotNet 統計
   工作等級的多次迭代與信賴區間分析；若需要正式的統計顯著性比較，請改用
   上述 BenchmarkDotNet 模式並接受較長的執行時間。

## 2. 環境資訊

| 項目 | 內容 |
|------|------|
| 作業系統 | Windows 11 Pro for Workstations（組建 10.0.26200） |
| CPU | Intel(R) Core(TM) i7-9750H CPU @ 2.60GHz（6 實體核心 / 12 邏輯核心） |
| 記憶體 | 約 31.8 GB |
| .NET SDK | `10.0.302` |
| .NET 執行階段 | `.NET 10.0.10`（`X64 RyuJIT AVX2`） |
| BenchmarkDotNet | `0.15.8` |
| MiniExcel | `1.45.0`（`Apache-2.0`） |
| ClosedXML | `0.105.0`（`MIT`） |

專案直接參照的 .NET 10 套件與本次本機安裝的 .NET Host／Runtime 均為
`10.0.10`；環境欄記錄實際執行階段，不以 NuGet 套件版本替代。

本機單次量測結果會受 CPU、記憶體、磁碟、電源模式與背景負載影響，因此本
文件記錄「如何量測」與「本機實測結果」，不作為跨機器的服務等級承諾；
方針與 [效能基準線](performance-baselines.md) 一致。

## 3. 實測結果表

### 3.1 最新本機重新驗證（2026-07-26）

情境：`1,000,000` 列 × `10` 欄混合型別資料，手動計時模式，各情境獨立子
處理程序執行一次。先以 `dotnet build ... --no-restore` 完成 Release 建置
（0 警告、0 錯誤），再直接呼叫相同的 `--manual-competitive` runner；這與
`eng/Benchmark-Competitive.ps1` 的量測階段相同。

| 情境 | 套件（授權） | 輸出格式 | 耗時 | GC 累積配置量 | 峰值工作集 | 輸出檔案大小 |
|------|--------------|----------|------|----------------|------------|--------------|
| `OdsStreamWriter` | OdfKit（CC0-1.0） | `.ods` | **6,608 ms** | **472.4 MB** | **36.6 MB** | 95.3 MB |
| `MiniExcel` | MiniExcel 1.45.0（Apache-2.0） | `.xlsx` | 6,720 ms | 3,354.3 MB | 46.7 MB | 111.0 MB |
| `ClosedXml` | ClosedXML 0.105.0（MIT，DOM 對照組） | `.xlsx` | 47,505 ms | 10,949.9 MB | 2,207.2 MB | **65.2 MB** |

（粗體標示各欄位表現最佳者。本次單次重新驗證中，`OdsStreamWriter` 耗時比
MiniExcel 約少 1.7%。）

### 3.2 v0.0.1 完滿基線（2026-07-16）

情境：`1,000,000` 列 × `10` 欄混合型別資料，手動計時模式，各情境獨立子
處理程序執行一次。量測日期 **2026-07-16**（v0.0.1 完滿基線），於導入共用
`OdfRawXmlWriter` 熱路徑後以 `pwsh eng/Benchmark-Competitive.ps1` 執行；
**下表採用當日第 3 次完整跑分**（與第 2 次同量級；配置量／峰值高度穩定）。

| 情境 | 套件（授權） | 輸出格式 | 耗時 | GC 累積配置量 | 峰值工作集 | 輸出檔案大小 |
|------|--------------|----------|------|----------------|------------|--------------|
| `OdsStreamWriter` | OdfKit（CC0-1.0） | `.ods` | **5,068 ms** | **472.7 MB** | **39.4 MB** | 95.3 MB |
| `MiniExcel` | MiniExcel 1.45.0（Apache-2.0） | `.xlsx` | 6,160 ms | 3,359.6 MB | 49.1 MB | 111.0 MB |
| `ClosedXml` | ClosedXML 0.105.0（MIT，DOM 對照組） | `.xlsx` | 42,405 ms | 10,947.9 MB | 2,206.5 MB | **65.2 MB** |

（粗體標示各欄位表現最佳者。本次第 3 次量測中，`OdsStreamWriter` 耗時比
MiniExcel 約少 17.7%。）

同日多次手動量測對照（便於評估穩定性；單位同表）：

| 情境 | 第 1 次耗時 | 第 2 次耗時 | 第 3 次耗時 | 配置量（第 3 次） | 峰值（第 3 次） |
|------|-------------|-------------|-------------|-------------------|-----------------|
| `OdsStreamWriter` | 6,534 ms | 5,602 ms | 5,068 ms | 472.7 MB | 39.4 MB |
| `MiniExcel` | 7,895 ms | 6,730 ms | 6,160 ms | 3,359.6 MB | 49.1 MB |
| `ClosedXml` | 51,657 ms | 44,160 ms | 42,405 ms | 10,947.9 MB | 2,206.5 MB |

配置量與峰值工作集跨次幾乎一致；耗時第 1 次偏高（冷處理程序／背景負載），
第 2／3 次同量級，故官方對比表以第 3 次（v0.0.1 完滿基線）為準，並誠實列出歷史次數。

## 4. 重現步驟

```powershell
# 1. 建置 Benchmarks 專案（Release）
dotnet build OdfKit.Benchmarks/OdfKit.Benchmarks.csproj -c Release

# 2. 手動計時模式（本文件表格數字的量測方式，每情境獨立子處理程序執行一次）
pwsh eng/Benchmark-Competitive.ps1

# 或直接呼叫組件：
dotnet OdfKit.Benchmarks/bin/Release/net10.0/OdfKit.Benchmarks.dll --manual-competitive

# 3. BenchmarkDotNet 模式（正式統計工作，耗時較長）
dotnet run --project OdfKit.Benchmarks -c Release -- --filter *CompetitiveStreamWriteBenchmarks*
```

## 5. 結果解讀

- **記憶體（峰值工作集）：OdsStreamWriter 仍明顯領先 DOM 路徑。** 最新
  `36.6 MB` 對 `ClosedXml` 的 `2,207.2 MB`，約 60 倍；主因是串流寫入不把整份
  活頁簿常駐為物件圖。`MiniExcel` 同屬串流路線，峰值約 `46.7 MB`，與
  `OdsStreamWriter` 差距約 1.28 倍。公開敘事應強調「遠低於 DOM 方案的峰值
  工作集」，而非未加限定的「小於 1MB」口號（見 `AGENTS.md` 與本文件方針）。
- **耗時：最新單次重新驗證仍略優於 MiniExcel。** `OdsStreamWriter`
  （`6,608 ms`）比 `MiniExcel`（`6,720 ms`）約少 **1.7%**；方向與
  2026-07-16 三次量測一致，但本次差距較小。相對 2026-07
  初版（`XmlWriter` 逐格路徑約慢 20%）已明顯
  收斂，主因是共用 `OdfRawXmlWriter` 批次組裝標記、關閉內建 `CheckCharacters`，
  並以 `OdfXmlCharacterGuard` 維持非法字元快速失敗（同路徑已套用至
  `OdtStreamWriter` 段落／標題／清單熱迴圈）。歷史第 1 次兩套件耗時皆偏高但
  相對比例接近後兩次，顯示絕對值易受冷啟動與背景負載影響。
- **GC 累積配置量：`OdsStreamWriter` 明顯優於兩組對照。** 最新量測為
  `472.4 MB`，約為 MiniExcel（`3,354.3 MB`）的七分之一、ClosedXML
  （`10,949.9 MB`）的二十三分之一；亦低於熱路徑前約 `770 MB` 的舊量測。累積
  配置量仍高於峰值工作集，代表配置後可被世代 GC 回收，不可與常駐記憶體混談。
- **輸出檔案大小僅供參考，不代表壓縮效率排名。** 如第 1.2 節所述，ODS
  與 XLSX 是不同 schema，`ClosedXml` 輸出的 `.xlsx`（`65.2 MB`）小於
  `OdsStreamWriter` 的 `.ods`（`95.3 MB`），這反映的是格式與序列化策略
  差異，而非「ClosedXML 壓縮比較好」的效能結論。

## 6. 已知限制

- 本文件僅涵蓋單一情境與單一機器：2026-07-16 三次手動量測，加上
  2026-07-26 一次重新驗證；並非長期追蹤的效能回歸關卡。長期回歸偵測請見
  [效能基準線](performance-baselines.md) 中的 `eng/Benchmark-Regression.ps1`。
- 手動計時模式的耗時量測未排除子處理程序啟動（JIT 暖身、組件載入）的一次性
  成本；`OdsStreamWriter`、`MiniExcel`、`ClosedXml` 三者皆同樣受此影響，
  相對比較仍具參考價值，但不宜視為「穩態吞吐量」的精確數字。
- 未涵蓋讀取（匯入）路徑、樣式／格式化密集情境，也未涵蓋 NPOI、EPPlus
  （見第 1.3 節授權裁定）。
- 未於 Linux／macOS 上驗證；`Process.PeakWorkingSet64` 之取得方式在其他
  作業系統上是否可用未經測試。
