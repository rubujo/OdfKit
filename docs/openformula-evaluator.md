# OpenFormula 評估器支援

OdfKit 提供受控的純 .NET 公式評估器，也允許應用程式以執行個體範圍的函式註冊表
或外部後援擴充能力。這些擴充可以處理 OASIS Large Group 清單以外的函式，
但「功能超集合」與「正式 Large Group 一致性」是兩件不同的事。

文件層的計算設定檔稱為 **OdfKit Safe Large**：Large Group 的 388 個強制函式名稱
皆可派送，但 `DDE` 永久列入安全排除，不啟動程序、不連網，也不求值引數。因此
`IsSafeProfileComplete` 可以為 `true`，但 OdfKit 不把它描述成未附條件的 OASIS
Large 正式一致性。

## 交易式計算與儲存策略

`EvaluateFormulas` 先在不可變輸入快照與暫存 DOM 上完成剖析、相依排序及求值；只有
整輪成功才依文件順序提交結果。不支援公式、剖析失敗、未授權外部參照或資源超限會
擲出含 `OdfFormulaEvaluationReport` 的 `OdfFormulaEvaluationException`，而原文件
與既有快取保持不變。標準結果 `#DIV/0!`、`#VALUE!` 等是合法公式值，不會觸發回復；
取消則維持 `OperationCanceledException`。

```csharp
var limits = new OdfFormulaEvaluationOptions
{
    MaxFormulaCount = 100_000,
    MaxCellReads = 10_000_000,
    TimeLimit = TimeSpan.FromSeconds(30),
    MaxDegreeOfParallelism = 0
};

OdfFormulaEvaluationReport report =
    document.EvaluateFormulas(limits, cancellationToken);
Console.WriteLine(
    $"{report.EvaluatedFormulaCount} formulas, " +
    $"{report.CellReadCount} reads, {report.Elapsed}");
```

預設預算另限制每式 32,768 字元、AST 深度 256、2,000,000 條公式相依邊、
10,000,000 次操作及儲存格讀取，以及 1,000,000 個陣列結果元素。整欄或大型範圍
相依只建立公式對公式的拓撲邊，不按範圍面積展開。平行求值只發生在同一拓撲層，
工作數同時受選項及全域 `OdfParallelScheduler` 限制；`NOW`／`TODAY` 共用工作階段
時間戳，提交順序固定，因此輸出可重現。

儲存時以 `OdfSaveOptions.FormulaStrategy` 明確選擇行為：

- `PreserveCachedValues`（預設）：公式、快取及顯示文字完全保持。
- `MarkForRecalculation`：保留公式，清除結果屬性與顯示文字，並設定自動計算。
- `Calculate`：使用相同的交易式引擎重算；失敗或取消不會留下部分結果。

```csharp
document.Save(
    "calculated.ods",
    new OdfSaveOptions
    {
        FormulaStrategy = OdfFormulaSaveStrategy.Calculate,
        FormulaEvaluationOptions = limits
    });
```

## 目前等級

| 項目 | 狀態 | 說明 |
|------|------|------|
| ODF 1.0／1.1 公式互通 | 支援 | 辨識及評估常見的 `oooc:=` 前綴；這兩版早於標準化的 OpenFormula 一致性群組。 |
| ODF 1.2～1.4 OpenFormula | 廣泛實作 | 辨識 `of:=` 與強制重算標記，支援科學記號、常數錯誤、參照範圍／交集／聯集、引號標籤、自動交集、命名運算式、外部名稱、inline array、矩陣公式寫回及受控重算。 |
| Small Group 強制函式名稱 | 110／110 | `OdfFormulaSupport.GetConformanceReport(Small)` 可機械化確認內建函式清單沒有名稱缺口。 |
| Small Group 正式一致性 | 尚未宣稱 | 尚須以規範 corpus 逐項證明基本限制、完整語法、隱含轉換、錯誤傳播及函式邊界語意。 |
| Medium Group 強制函式名稱 | 272／272 | 強制函式皆可由預設評估器派送，包含參照、矩陣、機率分佈、統計及財務函式。 |
| Large Group 強制函式名稱 | 388／388 | 強制函式名稱皆可派送，並包含 inline array、矩陣、複數、進位轉換與東亞位元組文字函式。 |
| Medium／Large 正式一致性 | 尚未宣稱 | 名稱覆蓋已完成；能力報告會另外列出刻意安全拒絕的 `DDE`。內嵌陣列、矩陣公式寫回、自動交集、文件／工作表名稱、外部名稱、Bessel 高階數值、奇數票息及 pivot 替代語法皆已有執行測試；仍須持續擴充逐函式 corpus，以涵蓋所有限制、locale／主機屬性、數值誤差及極端邊界。 |
| OdfKit Extended | 已實作擴充邊界 | 可註冊規範外或尚未內建的函式，也可把整條不受支援公式交給外部服務。這不是新的 OASIS 一致性等級。 |

OASIS 規定 OpenDocument Formula Evaluator 必須符合 Small、Medium 或 Large
其中一個群組；它可以另外實作規範的子集或超集合。OdfKit 因此不會用自訂函式數量
取代群組要求，也不會把 LibreOffice 重算結果誤標為核心評估器已符合 Large。

## 執行個體範圍自訂函式

註冊表不使用全域靜態狀態，不同租戶或文件可以有不同的函式集合。內建標準函式永遠
優先，應用程式無法以同名註冊覆寫其行為。

```csharp
var functions = new OdfFormulaFunctionRegistry();
functions.Register("ACME.DOUBLE", static (arguments, context) =>
    (double)arguments[0] * 2);

var evaluator = new DefaultFormulaEvaluator(functions);
document.EvaluateFormulas(evaluator);
```

`OdfFormulaSupport.Analyze(formula, functions)` 與
`OdfFormulaSupport.IsFunctionSupported(name, functions)` 會納入指定註冊表，
因此寫入前診斷與實際求值使用同一份能力描述。

`OdfFormulaSupport.GetConformanceReport(group, functions)` 另以 ODF 1.4 正式標準的
累計強制函式清單回報缺口。報告只證明函式名稱可派送，不會把名稱覆蓋誤當成完整
語法、限制、型別轉換與函式語意的一致性證明。`MissingFunctions`、
`BestEffortFunctions` 與 `SecurityExcludedFunctions` 分開呈現；`DDE` 位於最後一類。
`IsSafeProfileComplete` 表示除了明列的安全排除外沒有名稱或 Best Effort 缺口。
`HasCompleteFunctionSet` 仍只表示名稱沒有缺口，以維持既有 API 的單一職責。

`DDE` 不會由核心建立外部程序或網路連線，而是依安全政策傳回 `#N/A`。
`IOdfFormulaWorkbookContext` 提供依文件順序排列的工作表目錄，以及 pivot 與
`MULTIPLE.OPERATIONS` 的求值服務。內建 ODF DOM 已提供真實工作表目錄、依來源範圍
彙總的 `GETPIVOTDATA` 慣用語法，以及以暫時輸入替代值重新評估公式的
`MULTIPLE.OPERATIONS`。`SHEET`／`SHEETS` 不再以固定值模擬。
`IOdfFormulaEnvironmentContext` 可覆寫 `INFO` 類別；未覆寫時，評估器仍提供規範要求的
十個環境類別。奇數首期／末期債券函式已依實際 stub 天數、應計利息、票息日期及
日期基準折現，並以公開範例驗證價格與殖利率反算；Bessel 函式採用級數、漸近展開、
穩定遞迴及自適應積分，涵蓋高階、極小結果、負引數奇偶性與定義域邊界。
`GETPIVOTDATA` 支援慣用語法，以及包含引號、`Field[Member]`、唯一成員省略欄位名稱、
省略唯一資料欄位、subtotal 函式和重疊目標範圍文件順序的相容性替代語法；無法唯一
解析的成員會傳回 `#N/A`。多自變數 `LINEST`／`LOGEST`／`TREND`／`GROWTH` 使用具欄位樞紐的
QR 最小平方法求值，共線欄位會以秩不足模型處理，而不再直接反解容易失穩的一般方程式。
`LINEST`／`LOGEST` 的 `Stats=TRUE` 會回傳五列係數、標準誤、決定係數、估計標準誤、
F 統計量、自由度、迴歸平方和及殘差平方和；沒有殘差自由度的模型依規範回傳錯誤。
目前 Large Group 能力報告的 `SecurityExcludedFunctions` 只包含 `DDE`。核心不求值
其引數，也不會建立外部程序、網路或資料連線；任何呼叫固定傳回 `#N/A`。這是刻意的
安全政策，不是待補的演算法缺陷。

試算表文件仍可安全辨識並保留標準 ODF DDE 連結宣告。使用
`SpreadsheetDocument.ContainsDdeLinks` 快速檢查是否存在連結，或使用
`SpreadsheetDocument.GetDdeLinks()` 唯讀取得 application、topic、item、自動更新、
轉換模式與快取表格中繼資料。這些 API 不會連線至 DDE 伺服器，也不會更新快取資料。

```csharp
public sealed class WorkbookFormulaContext : IOdfFormulaWorkbookContext
{
    // 實作一般儲存格存取，以及 SheetNames、TryGetPivotData
    // 與 TryEvaluateMultipleOperations。
}
```

矩陣公式使用範圍 facade 宣告輸出形狀；重算時，二維結果會逐格寫回，形狀不符或
輸出範圍與其它公式衝突時會回傳公式錯誤，避免靜默覆寫。

```csharp
OdfTableSheet sheet = document.Worksheets.Add("Data");
sheet.Ranges["A1:B2"].SetArrayFormula("of:={1;2|3;4}+10");
document.EvaluateFormulas();
sheet.Ranges["A1:B2"].ClearArrayFormula();
```

## Volatile 計算工作階段

`NOW`、`TODAY`、`RAND` 與 `RANDBETWEEN` 會使用 `IOdfFormulaVolatileContext`。
同一次文件重算會共用一個時間戳記與執行緒安全的隨機序列，因此平行工作表求值不會讓
`NOW` 在不同儲存格漂移。直接呼叫評估器的應用程式也可提供固定時鐘與隨機來源，
建立可重現測試或受稽核的批次計算；未提供介面時維持系統時鐘與程序內隨機來源。

## 持久化增量重算

`CreateFormulaEvaluationSession` 會保留公式、值與相依圖快照。第一次
`Recalculate` 執行完整交易式重算；後續呼叫只評估已變更輸入或公式的下游子圖。
未變更的活頁簿會回報零個評估與零個寫回。公式新增、移除或改寫會在候選相依圖上
處理，只有整輪成功才取代工作階段狀態；失敗或取消不會污染下一輪重算。

```csharp
OdfFormulaEvaluationSession session =
    document.CreateFormulaEvaluationSession(options);
session.Recalculate(cancellationToken);

document.Worksheets["Data"].Cells["A1"].CellValue = 42;
OdfFormulaEvaluationReport incremental =
    session.Recalculate(cancellationToken);
Console.WriteLine($"只重算 {incremental.EvaluatedFormulaCount} 個公式");
```

相依圖以公式對公式拓撲邊搭配每工作表範圍索引追蹤輸入；整欄或大型範圍不會展開成
逐格相依集合。工作階段不是執行緒安全物件；文件結構由非儲存格 API 大幅改寫後，可
呼叫 `Invalidate`，讓下一次重算重新建立完整狀態。

## 外部後援

文件評估預設採 `CachedOnly`：外部參照只能讀取文件內既有快取，不會呼叫應用程式
resolver。只有呼叫端明確設定
`ExternalReferencePolicy = OdfFormulaExternalReferencePolicy.AllowConfiguredResolver`
才會使用已注入的 resolver。自訂函式、resolver 與整式 fallback 都是呼叫端信任的
程式碼，不受核心資源預算完整隔離。

`IOdfFormulaEvaluationFallback` 接收完整公式與目前的 `IEvaluationContext`。
只有當純 .NET 評估結果為不受支援名稱錯誤時才會呼叫後援；後援拒絕處理時，
原始錯誤會保持不變。介面可以連接 LibreOffice worker、企業試算服務或領域專用引擎，
OdfKit 核心不會自行啟動程序、存取網路或執行巨集。

```csharp
var evaluator = new DefaultFormulaEvaluator(functions, fallback);
object result = evaluator.Evaluate("of:=XLOOKUP(1;[.A1:.A3];[.B1:.B3])", context);
```

外部實作必須自行處理隔離、逾時、取消、資源限制、資料外洩與版本固定。若使用
LibreOffice 進行重算，結果代表該 LibreOffice 版本的行為，不代表 OASIS 規範逐位元
定義相同結果，也不代表巨集或外部連結可以安全執行。

## 一致性證據策略

專案內的 `OpenFormulaConformanceCorpusTests` 以 ODF 1.2～1.4 分組驗證科學記號、
強制重算、常數錯誤、左側錯誤傳播、型別比較、陣列形狀、矩陣、Unicode、Bessel
數值邊界、奇數票息公開範例、pivot 替代語法歧義與 `DDE` 安全拒絕。這是專案依正式
文本自行撰寫的可稽核 corpus，不冒充 OASIS 官方測試套件。

機器可讀的 [OpenFormula conformance manifest](openformula-conformance-manifest.json)
逐一列出 388 個 Large Group 函式、ODF 1.2／1.3／1.4、Safe Large 分類與測試證據。
每個函式都對應參數數量、正常型別、隱含轉換、空值、錯誤傳播、邊界及版本差異
七個可執行安全契約，共 2,716 個案例，並標示為 `safe-contract-covered`。
這些案例保證剖析、派送、封閉結果型別與安全排除，不是獨立的 OASIS 數值 oracle；
`normativeOracleStatus` 仍會如實標示 `pending-independent-oasis-oracle`，不得被工具
或文件當成官方 Large 正式一致性通過。
`pwsh eng/Generate-OpenFormulaConformanceManifest.ps1 -VerifyOnly` 會防止 manifest
與實際強制函式清單漂移。

後續證據工作應持續擴充每個函式的限制、空值、locale、日期基準、浮點容許誤差及
極端輸入案例；同時保留 OdfKit Extended 註冊表與後援，讓應用程式在 Large 清單之外
加入領域函式，並個別揭露外部引擎與安全邊界。是否正式宣稱 Small、Medium 或 Large
一致性，應以整份 corpus 的可重現通過證據決定，而不是只看 388／388 名稱覆蓋。

規範依據為 OASIS
[ODF 1.4 Part 4: OpenFormula](https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part4-formula/OpenDocument-v1.4-os-part4-formula.html)；
Bessel 數值方法另依
[NIST DLMF Chapter 10](https://dlmf.nist.gov/10) 的公開數學定義與計算方法實作；
奇數首期公式、日期基準名詞與公開範例另以 Microsoft Support 的
[ODDFPRICE](https://support.microsoft.com/en-us/excel/functions/oddfprice-function) 與
[ODDLPRICE](https://support.microsoft.com/en-us/excel/functions/oddlprice-function)
文件交叉驗證。
