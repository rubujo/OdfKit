using System;
using OdfKit.Chart;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    /// <summary>
    /// Gets the chart document for an embedded chart subpackage for advanced editing.
    /// 取得嵌入圖表的子封裝圖表文件，以供進階編輯。
    /// </summary>
    /// <param name="chartInfo">The embedded chart summary information. / 嵌入圖表摘要資訊。</param>
    /// <returns>The editable <see cref="OdfChartDocument"/> instance. / 可編輯的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    /// <remarks>
    /// 傳回的執行個體會從目前封裝中既有的 <c>content.xml</c> 位元組重新解析出獨立的 DOM 樹，
    /// 對其呼叫的任何修改方法（例如 <see cref="OdfChartDocument.ApplyDefinition"/>、
    /// <see cref="OdfChartDocument.ClearSeries"/> 等）會先變更此記憶體中的 DOM。
    /// 父文件儲存時會自動 flush 已追蹤的嵌入圖表；呼叫端也可手動呼叫傳回執行個體的
    /// <c>Save</c> 方法，以提早將變更寫回共用封裝。
    /// </remarks>
    public OdfChartDocument GetEmbeddedChartDocument(OdfEmbeddedChartInfo chartInfo)
    {
        if (chartInfo is null)
            throw new ArgumentNullException(nameof(chartInfo));

        return GetEmbeddedChartDocument(chartInfo.ObjectPath);
    }

    /// <summary>
    /// Gets an embedded chart document by subpackage path.
    /// 依子封裝路徑取得嵌入圖表文件。
    /// </summary>
    /// <param name="objectPath">The embedded chart subpackage path (e.g. <c>Object 1/</c>). / 嵌入圖表子封裝路徑（例如 <c>Object 1/</c>）。</param>
    /// <returns>The editable <see cref="OdfChartDocument"/> instance. / 可編輯的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    /// <remarks>
    /// 父文件儲存時會自動 flush 對傳回執行個體所做的修改；呼叫端仍可手動呼叫其
    /// <c>Save</c> 方法，以提早寫回共用封裝。
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
