using System.Text;

namespace OdfKit.Csv;

/// <summary>
/// Provides the OdfCsvOptions API.
/// CSV 匯入與匯出的選項設定。
/// </summary>
public sealed class OdfCsvOptions
{
    /// <summary>
    /// Gets the Delimiter value.
    /// 取得或設定欄位分隔字元，預設為逗號。
    /// </summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Gets a value indicating the HasHeaders state.
    /// 取得或設定第一列是否為標題列，預設為 true。
    /// </summary>
    public bool HasHeaders { get; init; } = true;

    /// <summary>
    /// Gets the Encoding value.
    /// 取得或設定 CSV 的文字編碼，預設為 UTF-8，不包含 BOM。
    /// </summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    /// <summary>
    /// Gets the SheetName value.
    /// 取得或設定匯入後的工作表名稱，預設為 Sheet1。
    /// </summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>
    /// Gets the ExportSheetIndex value.
    /// 取得或設定匯出時的工作表索引（從 0 開始），預設為 0。
    /// </summary>
    public int ExportSheetIndex { get; init; }

    /// <summary>
    /// Gets a value indicating the SanitizeFormulas state.
    /// 取得或設定是否在匯出時，將以公式觸發字元（<c>=</c>、<c>+</c>、<c>-</c>、<c>@</c>、跳格字元或歸位字元）開頭的文字值前面加上一個單引號，以防範 CSV 於試算表應用程式開啟時遭到公式注入攻擊，預設為 <see langword="true"/>。
    /// </summary>
    public bool SanitizeFormulas { get; init; } = true;
}
