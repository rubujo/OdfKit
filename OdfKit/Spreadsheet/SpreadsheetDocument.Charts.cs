using System;
using OdfKit.Chart;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    /// <summary>
    /// 取得嵌入圖表的子封裝圖表文件，以供進階編輯。
    /// </summary>
    /// <param name="chartInfo">嵌入圖表摘要資訊。</param>
    /// <returns>可編輯的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    /// <remarks>
    /// 傳回的執行個體會從目前封裝中既有的 <c>content.xml</c> 位元組重新解析出獨立的 DOM 樹，
    /// 對其呼叫的任何修改方法（例如 <see cref="OdfChartDocument.ApplyDefinition"/>、
    /// <see cref="OdfChartDocument.ClearSeries"/> 等）僅變更此記憶體中的 DOM。
    /// 必須明確呼叫傳回執行個體的 <c>Save</c> 方法，才會將變更寫回共用封裝，
    /// 否則後續儲存母文件時不會包含這些圖表變更。
    /// </remarks>
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
    /// <remarks>
    /// 對傳回執行個體所做的修改僅存在於記憶體中，必須明確呼叫其
    /// <c>Save</c> 方法，才會寫回共用封裝並於母文件儲存時保留。
    /// </remarks>
    public OdfChartDocument GetEmbeddedChartDocument(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_EmbeddedCannotBeEmpty"), nameof(objectPath));

        string normalized = objectPath.Trim();
        if (normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - 1);

        return GetEmbeddedDocument<OdfChartDocument>(normalized);
    }
}
