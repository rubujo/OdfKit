using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表條件格式與走勢圖讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentConditionalFormatReadEngine
{
    internal static IReadOnlyList<OdfConditionalFormatInfo> GetConditionalFormats(SpreadsheetDocument document)
    {
        List<OdfConditionalFormatInfo> formats = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            foreach (OdfConditionalFormatInfo format in sheet.ConditionalFormats)
                formats.Add(format);
        }

        return formats.AsReadOnly();
    }

    internal static IReadOnlyList<OdfSparklineGroupInfo> GetSparklineGroups(SpreadsheetDocument document)
    {
        List<OdfSparklineGroupInfo> groups = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            foreach (OdfSparklineGroupInfo group in sheet.SparklineGroups)
                groups.Add(group);
        }

        return groups.AsReadOnly();
    }
}
