using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Gets the number of data series in the chart.
    /// 取得圖表中的資料序列數量。
    /// </summary>
    public int SeriesCount => GetSeriesNodes().Count;

    /// <summary>
    /// Gets the editable data series at the specified index.
    /// 取得指定索引的可編輯資料序列。
    /// </summary>
    /// <param name="index">The zero-based series index. / 序列索引（從 0 起算）。</param>
    /// <returns>The editable series object. / 可編輯的序列物件。</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range. / 索引超出範圍時擲出。</exception>
    public OdfChartSeries GetSeriesEditor(int index)
    {
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (index < 0 || index >= nodes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), OdfLocalizer.GetMessage("Err_OdfChartDocument_SequenceIndexOutRange_2", index, nodes.Count));

        return new OdfChartSeries(this, nodes[index], index);
    }

    private IReadOnlyList<OdfNode> GetSeriesNodes()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
            return [];

        List<OdfNode> nodes = [];
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
                nodes.Add(child);
        }

        return nodes;
    }
}
