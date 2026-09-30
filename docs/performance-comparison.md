# OdfKit 與同類套件的串流寫入與讀取效能對比

本文件記錄 `OdsStreamWriter` 與兩套知名 .NET 試算表套件（MiniExcel、ClosedXML）
在「一百萬列 × 十欄混合型別資料」情境下的實測效能對比，包含方法論限制、
環境資訊、結果數字、重現步驟與結果解讀。目的是為「大量資料匯出低記憶體」
這項主張提供公開、可重現的量化證據，而不只是內部宣稱。
第 1 至 6 節為寫入對比；第 7 節為讀取對比。讀取對比的第一次量測發現 `OdsStreamReader` 比
MiniExcel 慢，經成因分析與最佳化後已成為三者中最快，第 7 節同時記錄最佳化前後的數字與成因。

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

# 只執行寫入對比，或只執行讀取對比（預設兩者都執行）
pwsh eng/Benchmark-Competitive.ps1 -Mode Write
pwsh eng/Benchmark-Competitive.ps1 -Mode Read

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
- 寫入對比未涵蓋樣式／格式化密集情境，也未涵蓋 NPOI、EPPlus（見第 1.3 節授權裁定）。
  讀取路徑見第 7 節。
- 未於 Linux／macOS 上驗證；`Process.PeakWorkingSet64` 之取得方式在其他
  作業系統上是否可用未經測試。

## 7. 讀取對比

本節是第 1 至 6 節寫入對比的讀取端對應：同一份 `1,000,000` 列 × `10` 欄的決定性混合型別資料，
比較 `OdsStreamReader` 與 MiniExcel（串流）、ClosedXML（DOM 對照組）的讀取成本。

### 7.1 方法論

- **輸入檔：** 每個讀取器讀取由對應寫入器產生的檔案。OdfKit 讀取 `OdsStreamWriter` 產生的
  `.ods`（`95.3 MB`）；MiniExcel 與 ClosedXML 讀取**同一份**由 MiniExcel 串流寫入器產生的
  `.xlsx`（`111.0 MB`），讓兩者面對相同輸入。輸入檔的產生時間不計入量測。
- **仍是跨格式參考對比：** 與第 1.2 節相同，ODS 與 XLSX 的 schema 與容器細節不同，這不是同格式對決。
- **內容檢查碼（正確性）：** 每個讀取器逐列累加檢查碼（列數、各欄位的總和或總長度、日期分鐘數、
  布林旗標計數），並與直接由產生器算出的預期值比對。整數欄位必須完全相同，浮點總和允許
  `1e-9` 相對誤差；任何一個不符，整次量測作廢且指令碼失敗。這是為了避免讀得快卻略過或讀錯資料。
- **量測模式：** 與第 1.4 節相同。手動計時模式，每個情境在**獨立子處理程序**讀取一次，量測
  耗時、GC 累積配置量與峰值工作集；本文件另外重複執行一次以確認穩定性。
  BenchmarkDotNet 對應類別為 `CompetitiveStreamReadBenchmarks`。
- **ClosedXML 是非串流對照組：** 它把整份活頁簿載入記憶體，預期耗時與記憶體都較高。

**必須揭露的設定：** `OdsStreamReader` 預設限制單一 XML 文件為 `64 MiB` 字元，這是對不可信輸入的
預設防護（見 [安全限制](security-limits.md)）。`1,000,000` 列 × `10` 欄的 `content.xml` 遠超過此上限，
未調整時會擲出 `XmlException`。此基準資料是自行產生的可信任資料，因此**只**把
`MaxXmlCharactersInDocument` 設為 `0`（停用這一項）；列數、欄數、repeat 與儲存格文字等其他限制維持預設。
本文件未調整 MiniExcel 與 ClosedXML 的任何限制設定。處理不可信文件時不應停用該上限。

### 7.2 環境

| 項目 | 內容 |
|------|------|
| 作業系統 | Windows 11 Pro for Workstations（組建 10.0.26300） |
| CPU | Intel(R) Core(TM) i7-9750H CPU @ 2.60GHz（6 實體核心 / 12 邏輯核心） |
| 記憶體 | 約 31.8 GB |
| .NET SDK | `10.0.401` |
| .NET 執行階段 | `.NET 10.0.12` |
| MiniExcel | `1.46.0`（`Apache-2.0`） |
| ClosedXML | `0.105.1`（`MIT`） |

第 2 節的環境表對應的是第 3 節寫入數字的量測時點（MiniExcel `1.45.0`、ClosedXML `0.105.0`）；本節使用
目前專案鎖定的版本，兩節的數字不可直接混合比較。

### 7.3 實測結果（2026-09-30）

情境：`1,000,000` 列 × `10` 欄混合型別資料，手動計時模式，各情境獨立子處理程序執行一次，
共執行兩次。三個情境的內容檢查碼在兩次都**完全相符**。

**最佳化後（目前的程式碼）：**

| 情境 | 套件（授權） | 輸入格式 | 輸入檔案大小 | 耗時（第 1 次／第 2 次） | GC 累積配置量 | 峰值工作集 |
|------|--------------|----------|--------------|---------------------------|----------------|------------|
| `OdsStreamReader` | OdfKit（CC0-1.0） | `.ods` | 95.3 MB | **7,616 ms／7,755 ms** | **1,837.6 MB** | **39.8 MB** |
| `MiniExcel` | MiniExcel 1.46.0（Apache-2.0） | `.xlsx` | 111.0 MB | 23,359 ms／20,371 ms | 19,688.4 MB／19,690.3 MB | 46.8 MB |
| `ClosedXml` | ClosedXML 0.105.1（MIT，DOM 對照組） | `.xlsx` | 111.0 MB | 47,661 ms／47,109 ms | 10,383.9 MB／10,332.6 MB | 1,211.2 MB／1,211.8 MB |

（粗體標示各欄位表現最佳者。`ClosedXml` 的 GC 累積配置量低於 MiniExcel，是因為 DOM 載入把資料留在記憶體內，
而不是對每個儲存格配置暫時物件；它的代價反映在峰值工作集。）

**`OdsStreamReader` 最佳化前（同日、同機器、同版本的 MiniExcel 與 ClosedXML）：**

| 情境 | 耗時（第 1 次／第 2 次） | GC 累積配置量 | 峰值工作集 |
|------|---------------------------|----------------|------------|
| `OdsStreamReader`（最佳化前） | 25,276 ms／29,538 ms | 13,472.5 MB | 47.5 MB／47.6 MB |
| `MiniExcel` | 19,510 ms／18,674 ms | 19,688.6 MB／19,686.8 MB | 43.5 MB／43.6 MB |
| `ClosedXml` | 45,486 ms／45,641 ms | 10,385.1 MB／10,384.3 MB | 1,211.8 MB／1,210.8 MB |

最佳化前 `OdsStreamReader` 比 MiniExcel 慢，經成因分析（第 7.6 節）並最佳化後，耗時約降為原來的
`0.26` 至 `0.30` 倍、配置量降 `86%`。MiniExcel 在兩組量測之間的耗時有落差（約 `18.7` 至 `23.4` 秒），
顯示絕對耗時易受背景負載影響；`OdsStreamReader` 與 ClosedXML 的重複量測則較為穩定。

### 7.4 重現步驟

```powershell
# 讀取對比（本節數字的量測方式，每情境獨立子處理程序執行一次，含內容檢查碼）
pwsh eng/Benchmark-Competitive.ps1 -Mode Read

# 或直接呼叫組件：
dotnet OdfKit.Benchmarks/bin/Release/net10.0/OdfKit.Benchmarks.dll --manual-competitive-read

# BenchmarkDotNet 模式（正式統計工作，耗時較長）
dotnet run --project OdfKit.Benchmarks -c Release -- --filter *CompetitiveStreamReadBenchmarks*
```

### 7.5 結果解讀

- **耗時：最佳化後 OdfKit 是三者中最快的。** `OdsStreamReader` 約 `7.6` 至 `7.8` 秒，MiniExcel 約
  `20.4` 至 `23.4` 秒（約為前者的 `2.6` 至 `3.1` 倍），ClosedXML 約 `47` 秒（約 `6.1` 至 `6.2` 倍）。
  這個結論是在第 7.6 節的最佳化之後才成立；最佳化前的結果相反（見第 7.3 節的對照表）。
- **GC 累積配置量：`OdsStreamReader` 約為 MiniExcel 的十分之一。** `1,837.6 MB` 對 `19,688 MB`；
  每個儲存格約 `183 B`。
- **峰值工作集：兩個串流讀取器相近，都遠低於 DOM 路徑。** `OdsStreamReader` 約 `39.8 MB`、MiniExcel
  約 `46.8 MB`，而 `ClosedXml` 約 `1,211 MB`，約為前者的 30 倍。
- **MiniExcel 的讀取方式會影響它的數字。** 本文件使用 `Query(useHeaderRow: false)`，每列會建立
  `IDictionary<string, object>`（動態列）；強型別的 `Query<T>` 對映可能較快，本文件未量測。
- **輸入檔案大小僅供參考**，理由同第 1.2 節。
- 此資料集每個儲存格只有單一段落、無行內元素，檢查碼驗證的是這個情境下的資料一致性；第 7.6 節提到的
  缺陷修正由單元測試（`OdsStreamReaderCharacterizationTests`）涵蓋。

### 7.6 成因分析與最佳化

最佳化前 `OdsStreamReader` 比 MiniExcel 慢，且每個儲存格配置約 `1.3 KB`。以下是當時以 20 萬列（200 萬個
儲存格）拆解成本的實測，每個實驗只改變一個變因；配置量按型別的分布由 .NET 執行階段的 `AllocationTick`
事件取樣統計，耗時與配置量為單一執行緒、單次量測。

| 實驗 | 耗時 | GC 累積配置量 | 每格配置 |
|------|------|----------------|----------|
| 純 `XmlReader` 走訪（下限，只解析 XML） | 1,370 ms | 0.8 MB | 0 B |
| 加上每格 9 次 `GetAttribute` | 2,582 ms | 121 MB | 64 B |
| 加上每列 `ReadSubtree`（無屬性） | 2,155 ms | 306 MB | 160 B |
| 每列子樹加屬性 | 3,141 ms | 426 MB | 224 B |
| 模擬原本 `OdsStreamReader` 結構（列子樹、屬性、每格子樹與文字串接） | 5,220 ms | 2,197 MB | 1,152 B |
| 原本 `OdsStreamReader` 實際 | 6,265 ms | 2,680 MB | 1,405 B |

模擬結構只用純 `XmlReader` 的 API，就已配置 `1,152 B` 每格，而實際實作只多 `253 B`，因此慢的主因是
**讀取結構**，而非 OdfKit 特有的物件。以 `6,265 ms` 為 100% 的粗略分解：

| 成本來源 | 約佔比 |
|----------|--------|
| XML 解析本身（下限） | 22% |
| 每格 9 次以名稱查詢的 `GetAttribute` | 19% |
| 每列 `ReadSubtree` | 11% |
| **每格 `ReadSubtree`（`ReadCellText`）與文字串接** | **35%** |
| OdfKit 自己的物件（`OdsCellValue`、空白佔位物件、每列 `List`、裝箱） | 16% |

配置量按型別：`NamespaceDeclaration[]` 16.4%、`NodeData` 14.4%、`XmlSubtreeReader` 9.6%、
`XmlNamespaceManager` 5.5%、`NodeData[]` 4.3%，合計約 50% 來自 `ReadSubtree` 的內部物件。

**已實作的最佳化**（`OdfKit/Spreadsheet/OdsStreamReader.cs`）：

1. 每列與每格不再呼叫 `ReadSubtree`，改以深度判斷範圍。
2. 屬性改為一次走訪，只對需要的屬性取值。
3. 重用每列的暫存清單、所有空白欄位共用同一個不可變的空白儲存格，單一段落時不建立 `List` 與 `Join`。

最佳化後以同樣的 20 萬列實驗量測為 `3,173` 至 `3,497 ms`、`352.5 MB`、每格 `185 B`（耗時約減半、配置量降 87%）。
剩餘的配置主要是字串（約 55%，屬性值與文字）與每格一個 `OdsCellValue`（約 34%），後者是公開資料形狀。
在 100 萬列規模，實測改善更大（耗時降為約 `0.26` 至 `0.30` 倍，見第 7.3 節）：這比成因分析當時
「下限約降 40%」的推估更好。可能是大量配置在長時間讀取下觸發更多世代 GC，但這一點未另外驗證。

這次分析同時發現並修正 `OdsStreamReader` 的三個資料正確性缺陷（與最佳化無關）：多段落儲存格會遺失每隔一個的段落；
段落內含 `text:span`、`text:s`、`text:tab` 等行內元素會擲出 `XmlException`；第一列為空列時同步的 `Read()`
讀不到任何列。三者都由 `OdsStreamReaderCharacterizationTests` 鎖定（同步與非同步兩條路徑的逐格結果），
最佳化在未修正缺陷的狀態下先以該測試驗證為行為不變，之後才修正缺陷。

### 7.7 已知限制

- 僅涵蓋單一讀取情境與單一機器；手動計時為單次量測，本節另外重複一次。
- 耗時包含子處理程序啟動（JIT 暖身、組件載入）的一次性成本，三者相同。
- 讀取端停用了 `OdsStreamReader` 的 XML 字元上限，見第 7.1 節的揭露。
- OdfKit 與另外兩者讀取的是不同格式；MiniExcel 與 ClosedXML 讀取的 `.xlsx` 由 MiniExcel 寫出，
  若改用其他工具寫出的 `.xlsx`，讀取成本可能不同。
- MiniExcel 使用動態列讀取，未量測強型別對映（見第 7.5 節）。
- 未涵蓋 DOM 式讀取（例如 `SpreadsheetDocument.Load`）、樣式密集情境，以及 Linux／macOS。
