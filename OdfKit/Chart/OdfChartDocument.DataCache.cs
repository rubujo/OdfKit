using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    private OdfChartDataCache? _localDataCache;

    internal int LocalDataCacheBuildCount { get; private set; }

    /// <summary>
    /// Gets a lazily loaded snapshot of the chart's embedded local data table.
    /// 取得圖表內嵌本地資料表的延遲載入快照。
    /// </summary>
    /// <returns>The local data table snapshot; an empty snapshot if the chart has no embedded data table. / 本地資料表快照；若圖表沒有內嵌資料表，則回傳空快照。</returns>
    public OdfChartDataCache GetLocalDataCache()
    {
        if (_localDataCache is not null)
        {
            return _localDataCache;
        }

        _localDataCache = BuildLocalDataCache();
        LocalDataCacheBuildCount++;
        return _localDataCache;
    }

    /// <summary>
    /// Clears the chart's local data snapshot so the next read rescans the current DOM.
    /// 清除圖表本地資料快照，讓下一次讀取重新掃描目前 DOM。
    /// </summary>
    protected void InvalidateLocalDataCache()
    {
        _localDataCache = null;
    }

    private OdfChartDataCache BuildLocalDataCache()
    {
        OdfNode? table = FindLocalDataTable();
        if (table is null)
        {
            return new OdfChartDataCache(Array.Empty<IReadOnlyList<object?>>());
        }

        List<IReadOnlyList<object?>> rows = [];
        foreach (OdfNode rowNode in table.Children)
        {
            if (rowNode.NodeType is not OdfNodeType.Element ||
                rowNode.LocalName != "table-row" ||
                rowNode.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            List<object?> row = [];
            foreach (OdfNode cellNode in rowNode.Children)
            {
                if (cellNode.NodeType is not OdfNodeType.Element ||
                    cellNode.LocalName != "table-cell" ||
                    cellNode.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                int repeatCount = GetPositiveRepeatCount(cellNode, "number-columns-repeated");
                object? value = ReadCellValue(cellNode);
                for (int i = 0; i < repeatCount; i++)
                {
                    row.Add(value);
                }
            }

            int rowRepeatCount = GetPositiveRepeatCount(rowNode, "number-rows-repeated");
            for (int i = 0; i < rowRepeatCount; i++)
            {
                rows.Add(row.ToArray());
            }
        }

        return new OdfChartDataCache(rows);
    }

    private OdfNode? FindLocalDataTable()
    {
        foreach (OdfNode node in ContentDom.Descendants())
        {
            if (node.NodeType is OdfNodeType.Element &&
                node.LocalName == "table" &&
                node.NamespaceUri == OdfNamespaces.Table)
            {
                return node;
            }
        }

        return null;
    }

    private static object? ReadCellValue(OdfNode cellNode)
    {
        string? valueType = cellNode.GetAttribute("value-type", OdfNamespaces.Office);
        if (string.IsNullOrEmpty(valueType))
        {
            string text = cellNode.TextContent;
            return text.Length == 0 ? null : text;
        }

        return valueType switch
        {
            "float" => TryReadDouble(cellNode),
            "boolean" => TryReadBoolean(cellNode),
            "date" => TryReadDate(cellNode),
            _ => cellNode.TextContent,
        };
    }

    private static object? TryReadDouble(OdfNode cellNode)
    {
        string? value = cellNode.GetAttribute("value", OdfNamespaces.Office);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            ? number
            : cellNode.TextContent;
    }

    private static object? TryReadBoolean(OdfNode cellNode)
    {
        string? value = cellNode.GetAttribute("boolean-value", OdfNamespaces.Office);
        return bool.TryParse(value, out bool flag) ? flag : cellNode.TextContent;
    }

    private static object? TryReadDate(OdfNode cellNode)
    {
        string? value = cellNode.GetAttribute("date-value", OdfNamespaces.Office);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
            ? date
            : cellNode.TextContent;
    }

    private static int GetPositiveRepeatCount(OdfNode node, string attributeName)
    {
        string? value = node.GetAttribute(attributeName, OdfNamespaces.Table);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 0
            ? count
            : 1;
    }
}
