# OpenFormula 評估器支援

OdfKit 提供受控的純 .NET 公式評估器，也允許應用程式以執行個體範圍的函式註冊表
或外部後援擴充能力。這些擴充可以處理 OASIS Large Group 清單以外的函式，
但「功能超集合」與「正式 Large Group 一致性」是兩件不同的事。

## 目前等級

| 項目 | 狀態 | 說明 |
|------|------|------|
| ODF 1.0／1.1 公式互通 | 支援 | 辨識及評估常見的 `oooc:=` 前綴；這兩版早於標準化的 OpenFormula 一致性群組。 |
| ODF 1.2～1.4 OpenFormula | 部分實作 | 辨識 `of:=`、剖析常用運算式與參照，並提供受控重算。 |
| Small Group 強制函式名稱 | 110／110 | `OdfFormulaSupport.GetConformanceReport(Small)` 可機械化確認內建函式清單沒有名稱缺口。 |
| Small Group 正式一致性 | 尚未宣稱 | 尚須以規範 corpus 逐項證明基本限制、完整語法、隱含轉換、錯誤傳播及函式邊界語意。 |
| Medium Group 強制函式名稱 | 272／272 | 強制函式皆可由預設評估器派送，包含參照、矩陣、機率分佈、統計及財務函式。 |
| Large Group 強制函式名稱 | 388／388 | 強制函式名稱皆可派送，並包含 inline array、矩陣、複數、進位轉換與東亞位元組文字函式。 |
| Medium／Large 正式一致性 | 尚未宣稱 | 名稱覆蓋已完成；能力報告會另外列出 Best Effort 函式。內嵌陣列已支援逐元素運算與純量／單列／單欄廣播，仍須以規範 corpus 驗證自動交集、外部名稱、區域名稱、型別轉換、數值誤差及全部邊界語意。 |
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
語法、限制、型別轉換與函式語意的一致性證明。`BestEffortFunctions` 會列出需要額外
內容模型或 corpus 證據的函式；只有 `HasOnlyFullyEvaluatedFunctions` 為 `true` 時，
該群組才沒有已知的 Best Effort 函式。`HasCompleteFunctionSet` 仍只表示名稱沒有缺口，
以維持既有 API 的單一職責。

`DDE` 不會由核心建立外部程序或網路連線，而是依安全政策傳回 `#N/A`。
`IOdfFormulaWorkbookContext` 提供依文件順序排列的工作表目錄，以及 pivot 與
`MULTIPLE.OPERATIONS` 的選配求值服務。內建 ODF DOM 已提供真實工作表目錄，因此
`SHEET`／`SHEETS` 不再以固定值模擬；應用程式或外部引擎可實作其餘兩項服務，
未提供服務時安全傳回 `#N/A`。奇數首期／末期債券函式已依實際 stub 天數、應計利息與
票息日期折現；多自變數 `LINEST`／`LOGEST`／`TREND`／`GROWTH` 使用具欄位樞紐的
QR 最小平方法求值，共線欄位會以秩不足模型處理，而不再直接反解容易失穩的一般方程式。
`LINEST`／`LOGEST` 的 `Stats=TRUE` 會回傳五列係數、標準誤、決定係數、估計標準誤、
F 統計量、自由度、迴歸平方和及殘差平方和；沒有殘差自由度的模型依規範回傳錯誤。
這些函式在所有日期慣例與極端數值 corpus 完成前仍標記為 Best Effort。
`INFO` 也只揭露安全且跨平台可取得的執行環境資訊。

```csharp
public sealed class WorkbookFormulaContext : IOdfFormulaWorkbookContext
{
    // 實作一般儲存格存取，以及 SheetNames、TryGetPivotData
    // 與 TryEvaluateMultipleOperations。
}
```

## Volatile 計算工作階段

`NOW`、`TODAY`、`RAND` 與 `RANDBETWEEN` 會使用 `IOdfFormulaVolatileContext`。
同一次文件重算會共用一個時間戳記與執行緒安全的隨機序列，因此平行工作表求值不會讓
`NOW` 在不同儲存格漂移。直接呼叫評估器的應用程式也可提供固定時鐘與隨機來源，
建立可重現測試或受稽核的批次計算；未提供介面時維持系統時鐘與程序內隨機來源。

## 外部後援

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

## 邁向並超出 Large 的順序

1. 先建立 Small 的語法、限制、轉換與函式語意 corpus，達成可稽核的正式基線。
2. 依 Medium 要求補齊參照聯集、命名運算式及其強制函式，避免只追求函式數量。
3. 以 OASIS corpus 驗證已完成的 388／388 名稱覆蓋，補強自動交集、
   區域名稱與長尾函式邊界語意，並擴大目前逐元素陣列運算的形狀 corpus。
4. 保留 OdfKit Extended 註冊表與後援，讓應用程式在正式 Large 之上加入領域函式，
   同時個別揭露外部引擎與安全邊界。

規範依據為 OASIS
[ODF 1.4 Part 4: OpenFormula](https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part4-formula/OpenDocument-v1.4-os-part4-formula.html)。
