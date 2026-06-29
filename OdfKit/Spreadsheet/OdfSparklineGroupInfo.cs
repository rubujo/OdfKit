using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a LibreOffice calcext sparkline group in a worksheet.
/// 表示工作表中一個 LibreOffice calcext 走勢圖群組。
/// </summary>
/// <param name="type">The sparkline type. / 走勢圖類型。</param>
/// <param name="sparklines">The list of sparklines in the group. / 群組內的走勢圖清單。</param>
public sealed class OdfSparklineGroupInfo(SparklineType type, IReadOnlyList<OdfSparklineInfo> sparklines)
{
    /// <summary>
    /// Gets the sparkline type.
    /// 取得走勢圖類型。
    /// </summary>
    public SparklineType Type { get; } = type;

    /// <summary>
    /// Gets the list of sparklines in the group.
    /// 取得群組內的走勢圖清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineInfo> Sparklines { get; } = sparklines ?? [];
}
