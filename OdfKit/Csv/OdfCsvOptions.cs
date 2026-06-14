using System.Text;

namespace OdfKit.Csv;

/// <summary>
/// CSV 匯入與匯出的選項設定。
/// </summary>
public sealed class OdfCsvOptions
{
    /// <summary>
    /// 取得或設定欄位分隔字元，預設為逗號。
    /// </summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// 取得或設定第一列是否為標題列，預設為 true。
    /// </summary>
    public bool HasHeaders { get; init; } = true;

    /// <summary>
    /// 取得或設定 CSV 的文字編碼，預設為 UTF-8，不包含 BOM。
    /// </summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    /// <summary>
    /// 取得或設定匯入後的工作表名稱，預設為 Sheet1。
    /// </summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>
    /// 取得或設定匯出時的工作表索引（從 0 開始），預設為 0。
    /// </summary>
    public int ExportSheetIndex { get; init; } = 0;
}
