using System;
using System.Diagnostics;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：文件不可附加至自身。
/// 修正前，<c>doc.AppendDocument(doc)</c> 會一邊迭代來源子節點、一邊把節點加回同一份 DOM，
/// 迴圈永不結束，記憶體在 8 秒內暴增至約 2.6 GB。此問題由反射式 API 模糊測試發現。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class DocumentSelfMergeGuardTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    [Fact]
    public void TextDocument_AppendSelf_ThrowsArgumentException()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("x");

        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<ArgumentException>(() => document.AppendDocument(document));
        Assert.Throws<ArgumentException>(() => document.AppendDocument(document, null));
        Assert.Throws<ArgumentException>(() => ((OdfDocument)document).AppendDocument(document));

        Assert.True(stopwatch.Elapsed < Budget);
    }

    [Fact]
    public void TextDocument_AppendSelf_LeavesDocumentUnchanged()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("only");
        string before = document.ExtractText();

        Assert.Throws<ArgumentException>(() => document.AppendDocument(document));

        Assert.Equal(before, document.ExtractText());
    }

    [Fact]
    public void SpreadsheetDocument_AppendSelf_ThrowsArgumentException()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.AddSheet("D").Cells["A1"].CellValue = 1d;

        Assert.Throws<ArgumentException>(() => document.AppendDocument(document));
    }

    [Fact]
    public void TextDocument_AppendDifferentDocument_StillWorks()
    {
        using TextDocument target = TextDocument.Create();
        target.AddParagraph("first");
        using TextDocument source = TextDocument.Create();
        source.AddParagraph("second");

        target.AppendDocument(source);

        string text = target.ExtractText();
        Assert.Contains("first", text);
        Assert.Contains("second", text);
    }
}
