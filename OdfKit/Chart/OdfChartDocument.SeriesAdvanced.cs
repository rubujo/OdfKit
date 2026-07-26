using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;
/// <summary>
/// Provides the OdfChartDocument API.
/// 提供 OdfChartDocument API。
/// </summary>

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

    /// <summary>
    /// Removes the data series at the specified index.
    /// 移除指定索引的資料序列。
    /// </summary>
    /// <param name="index">The zero-based series index. / 序列索引（從 0 起算）。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range. / 索引超出範圍時擲出。</exception>
    public void RemoveSeriesAt(int index)
    {
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (index < 0 || index >= nodes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                OdfLocalizer.GetMessage("Err_OdfChartDocument_SequenceIndexOutRange_2", index, nodes.Count));
        }

        nodes[index].Parent!.RemoveChild(nodes[index]);
    }

    /// <summary>
    /// Moves a data series to another position while preserving the series node and its unknown content.
    /// 將資料序列移至另一個位置，同時保留序列節點及其未知內容。
    /// </summary>
    /// <param name="sourceIndex">The zero-based source series index. / 來源序列索引（從 0 起算）。</param>
    /// <param name="destinationIndex">The zero-based destination series index. / 目的序列索引（從 0 起算）。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is out of range. / 任一索引超出範圍時擲出。</exception>
    public void MoveSeries(int sourceIndex, int destinationIndex)
    {
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (sourceIndex < 0 || sourceIndex >= nodes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceIndex),
                OdfLocalizer.GetMessage("Err_OdfChartDocument_SequenceIndexOutRange_2", sourceIndex, nodes.Count));
        }

        if (destinationIndex < 0 || destinationIndex >= nodes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationIndex),
                OdfLocalizer.GetMessage("Err_OdfChartDocument_SequenceIndexOutRange_2", destinationIndex, nodes.Count));
        }

        if (sourceIndex == destinationIndex)
        {
            return;
        }

        OdfNode series = nodes[sourceIndex];
        OdfNode parent = series.Parent!;
        parent.RemoveChild(series);
        if (destinationIndex < sourceIndex)
        {
            parent.InsertBefore(series, nodes[destinationIndex]);
        }
        else
        {
            parent.InsertAfter(series, nodes[destinationIndex]);
        }
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
