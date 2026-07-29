# 跨格式文字自動版面配置

OdfKit 的文字自動版面配置分為共用量測層與格式適配層。核心套件提供不讀取字型檔的
`Fast` 模式及 ODF 原生 `Reader` 模式；`OdfKit.Extensions.Imaging` 提供使用
HarfBuzzSharp／SkiaSharp 的 `Precise` 模式。核心不依賴原生渲染套件。

## 模式

| 模式 | 行為 | 適用情境 |
| --- | --- | --- |
| `Reader` | 寫入 `use-optimal-*` 或 `auto-grow-*`，由閱讀器排版 | 最低成本、由 LibreOffice 等閱讀器開啟 |
| `Fast` | 以 grapheme cluster、Unicode 字寬、有效字級與樣式估算 | 伺服器、無原生相依、可預測批次處理 |
| `Precise` | 使用呼叫端提供的 `IOdfTextLayoutMeasurer` | 需要實際字型 shaping 與行高 |

`Fast` 是預設模式。`Precise` 不會靜默啟用；呼叫端必須提供量測器，確保原生字型解析
是明確選擇。

## ODS 欄寬與列高

```csharp
var options = new OdfAutoFitOptions
{
    Mode = OdfAutoFitMode.Fast,
    MaximumColumnWidth = OdfLength.FromCentimeters(20)
};

sheet.AutoFitColumnWidths([0, 1, 2], options);
sheet.AutoFitRowHeights(Enumerable.Range(0, 100), options);
```

欄寬使用 `OdfCell.FormattedValue`、有效字型名稱／字級、粗斜體、writing mode、padding
與旋轉角度。列高依目前欄寬進行換行量測，因此批次作業應先計算欄寬，再計算列高。
重複壓縮的列與儲存格不會為了量測而展開。

`SetRowOptimalHeight` 保留原有語意，只設定閱讀器最佳列高。需要 OdfKit 寫入確定性
`row-height` 時，使用 `AutoFitRowHeight`／`AutoFitRowHeights`。

## 精確量測

```csharp
using var layout = new OdfTextLayoutSession(document.FontContext);
var options = new OdfAutoFitOptions
{
    Mode = OdfAutoFitMode.Precise,
    TextMeasurer = layout
};

sheet.AutoFitColumnWidths([0, 1, 2], options);
sheet.AutoFitRowHeights(Enumerable.Range(0, 100), options);
```

`OdfTextLayoutSession` 會在單次批次作業中重用字型資料、typeface 與短文字結果，並以
字型數、字型總位元組及量測項目數限制快取。工作階段會序列化量測與釋放，以保護原生
handle；需要平行量測時，每個 worker 應各自使用一個工作階段。工作階段應由呼叫端明確
釋放。

## ODT、ODP 與 ODG

`OdfKit.Extensions.Imaging.OdfTextBoxLayoutExtensions.AutoFit` 適用於：

- ODT 的 `OdfFloatingTextBox`。
- ODP／ODG 共用的 `OdfTextBox`。

`Reader` 模式寫入 `draw:auto-grow-width`／`draw:auto-grow-height`。`Fast` 與
`Precise` 模式可依 `ResizeTextBoxWidth`／`ResizeTextBoxHeight` 寫回 `svg:width` 與
`svg:height`。此 API 不會自動變更頁面、投影片或畫布尺寸。

## 效能與安全邊界

- 所有長時間批次 API 都有 `CancellationToken` 多載。
- `MaximumCells`、`MaximumTextElements`、`MaximumTextElementsPerBlock` 與
  `MaximumMeasurementCacheEntries` 限制作業資源。
- `Reader`／`Fast` 不解析字型檔、不建立處理程序、不連網。
- `Precise` 只解析 `OdfFontContext` 可解析的系統字型、呼叫端明確註冊字型，或在
  `UseEmbeddedFonts` 明確啟用時解析文件封裝內的字型；不會下載遠端字型。
- 字型檔會先以位元組上限完整讀入，再交給原生解析器；字型快取具數量與總位元組上限。
  HarfBuzz 失敗時改採已建立 typeface 的 Skia 量測。
- 無效、負值、NaN、Infinity 與超出設定上限的輸出不會寫入文件。

效能基準位於 `OdfKit.Benchmarks/TextAutoFitBenchmarks.cs`，涵蓋 Fast／Precise 批次欄寬
及欄寬確定後的批次列高。
