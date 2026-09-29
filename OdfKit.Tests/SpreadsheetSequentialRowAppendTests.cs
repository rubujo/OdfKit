using System;
using System.Diagnostics;
using System.Text;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：逐列由上往下建立工作表（<c>GetCell</c> / <c>Cells[]</c>）不得因每新增一列就全表重掃而呈二次方成本，
/// 且快速路徑建出的 DOM 必須與「每次存取都清除快取、走原本引擎路徑」的結果完全相同。
/// 修正前，8,000 列約需 9.5 秒。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class SpreadsheetSequentialRowAppendTests
{
    private static string Dump(OdfNode node)
    {
        var builder = new StringBuilder();
        Append(builder, node);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, OdfNode node)
    {
        builder.Append('<').Append(node.LocalName);
        foreach (var attribute in node.Attributes)
            builder.Append(' ').Append(attribute.Key.LocalName).Append('=').Append(attribute.Value);
        builder.Append('>');
        foreach (OdfNode child in node.Children)
            Append(builder, child);
        builder.Append("</").Append(node.LocalName).Append('>');
    }

    private static string Build(bool invalidateEachAccess, int rows, int columns)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (invalidateEachAccess)
                    sheet.InvalidateAccessCache();
                sheet.GetCell(r, c).CellValue = r * 100 + c;
            }
        }

        return Dump(sheet.TableNode);
    }

    [Fact]
    public void SequentialAppend_ProducesTheSameDomAsTheEnginePath()
    {
        Assert.Equal(Build(true, 40, 5), Build(false, 40, 5));
    }

    [Fact]
    public void SequentialAppend_AfterGapFillAndRepeatedRows_StaysConsistent()
    {
        static string Run(bool invalidate)
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create();
            OdfTableSheet sheet = document.AddSheet("D");
            void Set(int r, int c, double v)
            {
                if (invalidate)
                    sheet.InvalidateAccessCache();
                sheet.GetCell(r, c).CellValue = v;
            }

            Set(0, 0, 1);
            Set(1, 0, 2);
            Set(6, 1, 3); // 跳列：引擎補建 2..6 列
            Set(7, 0, 4); // 之後的循序附加
            Set(8, 2, 5);
            sheet.InsertRows(3, 4);
            Set(20, 0, 6);
            Set(21, 1, 7);
            return Dump(sheet.TableNode);
        }

        Assert.Equal(Run(true), Run(false));
    }

    [Fact]
    public void SequentialAppend_WithInvalidColumn_DoesNotLeaveAnEmptyRowBehind()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");
        sheet.GetCell(0, 0).CellValue = 1;
        string before = Dump(sheet.TableNode);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetCell(1, OdfSpreadsheetLimits.MaxColumnIndex + 1));

        Assert.Equal(before, Dump(sheet.TableNode));
    }

    [Fact]
    public void SequentialAppend_OfManyRows_IsNotQuadratic()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");

        var stopwatch = Stopwatch.StartNew();
        for (int r = 0; r < 20_000; r++)
            sheet.GetCell(r, 0).CellValue = r;

        // 修正前 8,000 列即約 9.5 秒；20,000 列在線性成本下遠低於此預算。
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), stopwatch.Elapsed.ToString());
        Assert.Equal(19_999d, Convert.ToDouble(sheet.GetCell(19_999, 0).CellValue));
    }
}
