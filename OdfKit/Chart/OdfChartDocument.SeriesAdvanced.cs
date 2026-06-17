using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得圖表中的資料序列數量。
    /// </summary>
    public int SeriesCount => GetSeriesNodes().Count;

    /// <summary>
    /// 取得指定索引的可編輯資料序列。
    /// </summary>
    /// <param name="index">序列索引（從 0 起算）。</param>
    /// <returns>可編輯的序列物件。</returns>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時擲出。</exception>
    public OdfChartSeries GetSeriesEditor(int index)
    {
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (index < 0 || index >= nodes.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"序列索引 {index} 超出範圍（共 {nodes.Count} 筆）。");

        return new OdfChartSeries(nodes[index], index);
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
