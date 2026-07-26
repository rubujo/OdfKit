using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>
    /// Applies all properties represented by an immutable series snapshot to an existing series.
    /// 將不可變序列快照所表示的全部屬性套用至既有序列。
    /// </summary>
    /// <remarks>
    /// The existing series node and all managed, unknown, and foreign child content are preserved.
    /// 既有序列節點及其受管理、未知與外來子內容都會保留。
    /// </remarks>
    /// <param name="index">The zero-based target series index. / 目標序列索引（從 0 起算）。</param>
    /// <param name="snapshot">The desired immutable series snapshot. / 目標不可變序列快照。</param>
    /// <returns><see langword="true"/> if the series was updated; otherwise <see langword="false"/>. / 若已更新序列則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="snapshot"/> is <see langword="null"/>. / 當 <paramref name="snapshot"/> 為 <see langword="null"/> 時擲出。</exception>
    public bool ApplySeriesSnapshot(int index, OdfChartSeriesInfo snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (index < 0 || index >= nodes.Count)
        {
            return false;
        }

        ApplySeriesSnapshot(nodes[index], snapshot);
        return true;
    }

    /// <summary>
    /// Applies a batch of immutable series snapshots in request order.
    /// 依要求順序批次套用不可變序列快照。
    /// </summary>
    /// <remarks>
    /// Duplicate indices are applied sequentially. Missing indices are reported without changing the series collection.
    /// 重複索引會依序套用；不存在的索引會回報，且不會改變序列集合。
    /// </remarks>
    /// <param name="updates">The indexed series snapshot updates. / 含索引的序列快照更新要求。</param>
    /// <returns>The batch update result; index strings are used as identifiers. / 批次更新結果；以索引字串作為識別碼。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="updates"/> or an update snapshot is <see langword="null"/>. / 當 <paramref name="updates"/> 或任一更新快照為 <see langword="null"/> 時擲出。</exception>
    public OdfBatchUpdateResult ApplySeriesSnapshots(IEnumerable<OdfChartSeriesUpdate> updates)
    {
        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        var requests = new List<OdfChartSeriesUpdate>();
        foreach (OdfChartSeriesUpdate update in updates)
        {
            if (update is null || update.Snapshot is null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            requests.Add(update);
        }

        var result = new OdfBatchUpdateResult();
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        foreach (OdfChartSeriesUpdate update in requests)
        {
            string identifier = update.Index.ToString(CultureInfo.InvariantCulture);
            if (update.Index < 0 || update.Index >= nodes.Count)
            {
                result.MissingNames.Add(identifier);
                continue;
            }

            ApplySeriesSnapshot(nodes[update.Index], update.Snapshot);
            result.UpdatedCount++;
            result.UpdatedNames.Add(identifier);
        }

        return result;
    }

    private static void ApplySeriesSnapshot(OdfNode node, OdfChartSeriesInfo snapshot)
    {
        SetOrRemoveSeriesAttribute(node, "values-cell-range-address", snapshot.ValuesCellRangeAddress);
        SetOrRemoveSeriesAttribute(node, "label-cell-address", snapshot.LabelCellAddress);
        SetOrRemoveSeriesAttribute(node, "class", snapshot.SeriesClass);
        SetOrRemoveSeriesAttribute(node, "style-name", snapshot.StyleName);
        SetOrRemoveSeriesAttribute(node, "attached-axis", snapshot.AttachedAxis);
    }

    private static void SetOrRemoveSeriesAttribute(OdfNode node, string localName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            node.RemoveAttribute(localName, OdfNamespaces.Chart);
        }
        else
        {
            node.SetAttribute(localName, OdfNamespaces.Chart, value!, "chart");
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
