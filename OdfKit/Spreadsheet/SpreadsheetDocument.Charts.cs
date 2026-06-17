using System;
using OdfKit.Chart;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    /// <summary>
    /// 取得嵌入圖表的子封裝圖表文件，以供進階編輯。
    /// </summary>
    /// <param name="chartInfo">嵌入圖表摘要資訊。</param>
    /// <returns>可編輯的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    public OdfChartDocument GetEmbeddedChartDocument(OdfEmbeddedChartInfo chartInfo)
    {
        if (chartInfo is null)
            throw new ArgumentNullException(nameof(chartInfo));

        return GetEmbeddedChartDocument(chartInfo.ObjectPath);
    }

    /// <summary>
    /// 依子封裝路徑取得嵌入圖表文件。
    /// </summary>
    /// <param name="objectPath">嵌入圖表子封裝路徑（例如 <c>Object 1/</c>）。</param>
    /// <returns>可編輯的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    public OdfChartDocument GetEmbeddedChartDocument(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("嵌入圖表路徑不可為空。", nameof(objectPath));

        string normalized = objectPath.Trim();
        if (normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - 1);

        return GetEmbeddedDocument<OdfChartDocument>(normalized);
    }
}
