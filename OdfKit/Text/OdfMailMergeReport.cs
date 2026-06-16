using System.Collections.Generic;

namespace OdfKit.Text;

/// <summary>
/// 表示郵件合併作業執行完畢後的結果報告，可用於範本資料繫結除錯。
/// </summary>
public sealed class OdfMailMergeReport
{
    /// <summary>
    /// 取得在合併作業中未能被資料來源成功解析的預留位置（Placeholder）欄位清單。
    /// </summary>
    public List<string> UnresolvedPlaceholders { get; } = new();
}
