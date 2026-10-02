using OdfKit.Collaboration;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：<c>addColumns</c> 的欄數必須受 <see cref="OdtOperationSafetyOptions"/> 的表格上限約束。
/// 修正前，只有 <c>addTable</c> 受 MaxTableColumns／MaxTableCells 限制；
/// 一個約 100 位元組的 <c>addColumns</c>（count 可達 Int32.MaxValue）即可耗盡記憶體
/// （100 萬欄約需 1.2 GB）。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class CollaborationColumnSafetyLimitTests
{
    private static OdtOperationImportReport Merge(string json)
    {
        using TextDocument document = TextDocument.Create();
        return OdtOperationsImporter.Merge(document, json, null);
    }

    private static string AddTableThenColumns(int rows, int columns, params int[] counts)
    {
        var operations = new System.Collections.Generic.List<string>
        {
            "{\"name\":\"addTable\",\"attrs\":{\"rows\":" + rows + ",\"columns\":" + columns + "}}",
        };
        foreach (int count in counts)
        {
            operations.Add("{\"name\":\"addColumns\",\"attrs\":{\"position\":0,\"count\":" + count + "}}");
        }

        return "[" + string.Join(",", operations) + "]";
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(int.MaxValue)]
    public void AddColumnsBeyondColumnLimitIsRejectedWithoutAllocating(int count)
    {
        OdtOperationImportReport report = Merge(AddTableThenColumns(2, 2, count));

        Assert.Equal(1, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
        Assert.NotNull(report.SafetyLimitHitReason);
    }

    [Fact]
    public void AddColumnsCumulativeGrowthBeyondLimitIsRejected()
    {
        OdtOperationImportReport report = Merge(AddTableThenColumns(2, 2, 600, 600));

        Assert.Equal(2, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
        Assert.NotNull(report.SafetyLimitHitReason);
    }

    [Fact]
    public void AddColumnsBeyondCellLimitIsRejected()
    {
        // 10,000 列 × 21 欄 = 210,000 格，超過預設 MaxTableCells（200,000）。
        OdtOperationImportReport report = Merge(AddTableThenColumns(10_000, 1, 21));

        Assert.Equal(1, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
    }

    [Fact]
    public void AddColumnsWithinLimitsStillReplays()
    {
        OdtOperationImportReport report = Merge(AddTableThenColumns(2, 2, 3));

        Assert.Equal(2, report.ReplayedCount);
        Assert.Equal(0, report.IgnoredCount);
        Assert.Null(report.SafetyLimitHitReason);
    }
}
