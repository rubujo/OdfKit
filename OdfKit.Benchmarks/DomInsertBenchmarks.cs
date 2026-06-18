using BenchmarkDotNet.Attributes;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Benchmarks;

/// <summary>
/// DOM 子節點連續插入（<see cref="OdfNode.InsertAfter"/>）效能基準。
/// </summary>
[MemoryDiagnoser]
public class DomInsertBenchmarks
{
    private const int RowCount = 2_000;

    [Benchmark]
    public int SequentialInsertAfter()
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        var firstRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
        table.AppendChild(firstRow);

        OdfNode previous = firstRow;
        for (int i = 1; i < RowCount; i++)
        {
            var row = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            table.InsertAfter(row, previous);
            previous = row;
        }

        return table.Children.Count;
    }
}
