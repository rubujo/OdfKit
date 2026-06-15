using System;
using System.IO;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdsStreamReader 流式讀取 API 的整合測試。
/// </summary>
public class SpreadsheetTests
{
    /// <summary>
    /// 驗證基本流式讀取：字串、浮點數、布林值均正確讀取。
    /// </summary>
    [Fact]
    public void OdsStreamReader_BasicRead_ReturnsCorrectValues()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("工作表1");
        sheet.Cells["A1"].CellValue = "標題";
        sheet.Cells["B1"].CellValue = 42d;
        sheet.Cells["C1"].CellValue = "Hello";
        sheet.Cells["A2"].CellValue = "第二列";
        sheet.Cells["B2"].CellValue = 3.14d;

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);

        Assert.Single(reader.SheetNames);
        Assert.Equal("工作表1", reader.SheetNames[0]);

        // 第一列
        Assert.True(reader.Read());
        Assert.Equal(0, reader.RowIndex);
        Assert.Equal("標題", reader.GetValue(0));
        Assert.Equal(42d, reader.GetValue(1));
        Assert.Equal("Hello", reader.GetValue(2));

        // 第二列
        Assert.True(reader.Read());
        Assert.Equal(1, reader.RowIndex);
        Assert.Equal("第二列", reader.GetValue(0));
        Assert.Equal(3.14d, reader.GetValue(1));

        // 工作表結束
        Assert.False(reader.Read());
    }

    /// <summary>
    /// 驗證 SelectSheet 可切換至第二個工作表讀取。
    /// </summary>
    [Fact]
    public void OdsStreamReader_SelectSheet_ReadsSecondSheet()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet1 = doc.Worksheets.Add("Sheet1");
        sheet1.Cells["A1"].CellValue = "第一工作表";

        var sheet2 = doc.Worksheets.Add("Sheet2");
        sheet2.Cells["A1"].CellValue = "第二工作表";
        sheet2.Cells["B1"].CellValue = 99d;

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);

        Assert.Equal(2, reader.SheetNames.Count);
        Assert.Equal("Sheet1", reader.SheetNames[0]);
        Assert.Equal("Sheet2", reader.SheetNames[1]);

        reader.SelectSheet(1);

        Assert.True(reader.Read());
        Assert.Equal("第二工作表", reader.GetValue(0));
        Assert.Equal(99d, reader.GetValue(1));

        Assert.False(reader.Read());
    }

    /// <summary>
    /// 驗證 SelectSheet 在 Read() 之後呼叫時拋出例外。
    /// </summary>
    [Fact]
    public void OdsStreamReader_SelectSheetAfterRead_Throws()
    {
        using var doc = SpreadsheetDocument.Create();
        doc.Worksheets.Add("Sheet1").Cells["A1"].CellValue = "資料";

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);
        reader.Read();

        Assert.Throws<InvalidOperationException>(() => reader.SelectSheet(0));
    }

    /// <summary>
    /// 驗證越界欄位索引回傳 null（不拋出例外）。
    /// </summary>
    [Fact]
    public void OdsStreamReader_GetValue_OutOfRange_ReturnsNull()
    {
        using var doc = SpreadsheetDocument.Create();
        doc.Worksheets.Add("Sheet1").Cells["A1"].CellValue = "單格";

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);
        reader.Read();

        Assert.Null(reader.GetValue(-1));
        Assert.Null(reader.GetValue(100));
    }

    /// <summary>
    /// 驗證 FieldCount 回傳目前列最後非空欄之欄數。
    /// </summary>
    [Fact]
    public void OdsStreamReader_FieldCount_ReflectsLastNonNullColumn()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.Cells["A1"].CellValue = "A";
        sheet.Cells["C1"].CellValue = "C"; // B1 為空

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);
        reader.Read();

        // A(0)、B(1=null)、C(2) — 共 3 個欄位
        Assert.Equal(3, reader.FieldCount);
        Assert.Equal("A", reader.GetValue(0));
        Assert.Null(reader.GetValue(1));
        Assert.Equal("C", reader.GetValue(2));
    }

    /// <summary>
    /// 驗證流式讀取 10 萬列的大型 ODS 不會 OOM，且列數正確。
    /// </summary>
    [Fact]
    public void OdsStreamReader_LargeFile_StreamsCorrectly()
    {
        const int RowCount = 100_000;

        using var ms = new MemoryStream();

        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("大型資料");
            for (int r = 0; r < RowCount; r++)
            {
                writer.WriteStartRow();
                writer.WriteCell(r);
                writer.WriteCell($"row_{r}");
                writer.WriteEndRow();
            }
        }

        ms.Position = 0;

        using var reader = new OdsStreamReader(ms);
        int count = 0;
        while (reader.Read())
        {
            count++;
            if (count == 1)
            {
                Assert.Equal(0d, reader.GetValue(0));
                Assert.Equal("row_0", reader.GetValue(1));
            }
        }

        Assert.Equal(RowCount, count);
    }
}
