using System;
using System.Collections.Generic;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the SpreadsheetDocument API.
/// 提供 SpreadsheetDocument API。
/// </summary>

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

    /// <summary>
    /// Finds an embedded chart by its subpackage path.
    /// 依子封裝路徑尋找嵌入圖表。
    /// </summary>
    /// <param name="objectPath">The embedded chart subpackage path. / 嵌入圖表子封裝路徑。</param>
    /// <returns>The chart summary when found; otherwise, <see langword="null"/>. / 找到時為圖表摘要；否則為 <see langword="null"/>。</returns>
    public OdfEmbeddedChartInfo? FindEmbeddedChart(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
            return null;

        string normalized = NormalizeEmbeddedChartPath(objectPath);
        foreach (OdfEmbeddedChartInfo chart in GetEmbeddedCharts())
        {
            if (string.Equals(NormalizeEmbeddedChartPath(chart.ObjectPath), normalized, StringComparison.Ordinal))
                return chart;
        }
        return null;
    }

    /// <summary>
    /// Removes an embedded chart and its package subdocument.
    /// 移除嵌入圖表及其封裝子文件。
    /// </summary>
    /// <param name="chartInfo">The embedded chart summary to remove. / 要移除的嵌入圖表摘要。</param>
    /// <returns><see langword="true"/> if the chart was removed; otherwise, <see langword="false"/>. / 若已移除圖表則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveEmbeddedChart(OdfEmbeddedChartInfo chartInfo)
    {
        if (chartInfo is null)
            throw new ArgumentNullException(nameof(chartInfo));

        return RemoveEmbeddedChart(chartInfo.ObjectPath);
    }

    /// <summary>
    /// Removes an embedded chart by subpackage path and cleans all package relations.
    /// 依子封裝路徑移除嵌入圖表，並清理所有封裝關聯。
    /// </summary>
    /// <param name="objectPath">The embedded chart subpackage path. / 嵌入圖表子封裝路徑。</param>
    /// <returns><see langword="true"/> if the chart was removed; otherwise, <see langword="false"/>. / 若已移除圖表則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveEmbeddedChart(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
            return false;

        string normalized = NormalizeEmbeddedChartPath(objectPath);
        bool removed = false;
        foreach (OdfTableSheet sheet in Worksheets)
        {
            OdfNode? shapes = OdfTableSheetDomHelper.FindChildElement(sheet.TableNode, "shapes", OdfNamespaces.Table);
            if (shapes is null)
                continue;

            var frames = new List<OdfNode>();
            foreach (OdfNode frame in shapes.Children)
            {
                if (frame.LocalName != "frame" || frame.NamespaceUri != OdfNamespaces.Draw)
                    continue;
                foreach (OdfNode child in frame.Children)
                {
                    if (child.LocalName == "object" &&
                        child.NamespaceUri == OdfNamespaces.Draw &&
                        string.Equals(
                            NormalizeEmbeddedChartPath(child.GetAttribute("href", OdfNamespaces.XLink) ?? string.Empty),
                            normalized,
                            StringComparison.Ordinal))
                    {
                        frames.Add(frame);
                        break;
                    }
                }
            }

            foreach (OdfNode frame in frames)
                removed |= shapes.RemoveChild(frame);
            if (shapes.Children.Count == 0)
                sheet.TableNode.RemoveChild(shapes);
        }

        if (!removed)
            return false;

        UntrackEmbeddedDocuments(normalized);
        RemoveEmbeddedChartPackage(normalized);
        return true;
    }

    /// <summary>
    /// Removes all embedded charts and their package subdocuments.
    /// 移除所有嵌入圖表及其封裝子文件。
    /// </summary>
    /// <returns>The number of removed embedded charts. / 已移除的嵌入圖表數量。</returns>
    public int ClearEmbeddedCharts()
    {
        var objectPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (OdfEmbeddedChartInfo chart in GetEmbeddedCharts())
            objectPaths.Add(NormalizeEmbeddedChartPath(chart.ObjectPath));

        int removed = 0;
        foreach (string objectPath in objectPaths)
        {
            if (RemoveEmbeddedChart(objectPath))
                removed++;
        }
        return removed;
    }

    private void RemoveEmbeddedChartPackage(string objectPath)
    {
        string prefix = objectPath + "/";
        var entries = new List<string>();
        foreach (string entry in Package.Entries.Keys)
        {
            if (entry.StartsWith(prefix, StringComparison.Ordinal))
                entries.Add(entry);
        }
        foreach (string entry in entries)
            Package.RemoveEntry(entry);
        Package.RemoveEntry(prefix);
        Package.SaveManifestToEntries();
    }

    private static string NormalizeEmbeddedChartPath(string objectPath) =>
        objectPath.Replace('\\', '/').Trim().TrimStart('.').Trim('/');
}
