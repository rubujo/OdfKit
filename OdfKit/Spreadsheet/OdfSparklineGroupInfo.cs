using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中一個 LibreOffice calcext 走勢圖群組。
/// </summary>
/// <param name="type">走勢圖類型。</param>
/// <param name="sparklines">群組內的走勢圖清單。</param>
public sealed class OdfSparklineGroupInfo(SparklineType type, IReadOnlyList<OdfSparklineInfo> sparklines)
{
    /// <summary>
    /// 取得走勢圖類型。
    /// </summary>
    public SparklineType Type { get; } = type;

    /// <summary>
    /// 取得群組內的走勢圖清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineInfo> Sparklines { get; } = sparklines ?? [];
}
