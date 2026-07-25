using System;
using System.IO;
using System.Security;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對 SpreadsheetDocumentEmbeddedChartReadEngine.cs 的專屬測試（修正 3：
/// MaxCharactersInDocument 誤用 MaxEntrySize 的單位混淆，以及超過 Int32.MaxValue 的長度
/// 轉型溢位防護）。
/// </summary>
public class SpreadsheetDocumentEmbeddedChartReadEngineTests
{
    /// <summary>
    /// 修正前：MaxCharactersInDocument 直接套用了以位元組為單位、且數值遠大於
    /// MaxXmlCharactersInDocument 預設值的 MaxEntrySize，導致使用者刻意調低的
    /// MaxXmlCharactersInDocument 完全不會對嵌入圖表的 content.xml 生效。
    /// 這裡把 MaxXmlCharactersInDocument 調到遠小於實際圖表 content.xml 長度的值，
    /// 但保留 MaxEntrySize 為預設（遠大於此測試內容），以精準鎖定「用錯選項」本身，
    /// 而非位元組層級的大小限制。
    /// </summary>
    [Fact]
    public void MaxXmlCharactersInDocument_LowerThanChartContentLength_ChartIsNotReturned()
    {
        using var stream = new MemoryStream();
        using (SpreadsheetDocument workbook = BuildSpreadsheetWithChart())
        {
            workbook.SaveToStream(stream);
        }

        stream.Position = 0;

        // 先以預設選項正常載入（package 本身的 manifest.xml／content.xml 等其他 XML 亦共用
        // 同一份 LoadOptions，若在載入當下就調低此值會連文件本身都載入失敗，蓋過本測試要
        // 鎖定的行為）。載入完成後才調低 MaxXmlCharactersInDocument，確認 GetEmbeddedCharts()
        // 於呼叫當下即時讀取 LoadOptions（而非快取載入時的舊值），藉此精準鎖定嵌入圖表
        // content.xml 的字元數上限是否確實生效。
        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        loaded.Package.LoadOptions.MaxXmlCharactersInDocument = 50;

        Assert.Empty(loaded.GetEmbeddedCharts());
    }

    /// <summary>
    /// 對照組：MaxXmlCharactersInDocument 維持預設值時，嵌入圖表應正常可讀，
    /// 確認上面的測試確實是受 MaxXmlCharactersInDocument 生效所影響，而非其他因素。
    /// </summary>
    [Fact]
    public void MaxXmlCharactersInDocument_DefaultValue_ChartIsReturned()
    {
        using var stream = new MemoryStream();
        using (SpreadsheetDocument workbook = BuildSpreadsheetWithChart())
        {
            workbook.SaveToStream(stream);
        }

        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);

        Assert.Single(loaded.GetEmbeddedCharts());
    }

    [Fact]
    public void EnsureLengthFitsInInt32_LengthWithinRange_DoesNotThrow()
    {
        SpreadsheetDocumentEmbeddedChartReadEngine.EnsureLengthFitsInInt32(
            int.MaxValue, "Err_SpreadsheetDocumentEmbeddedChartReadEngine_ChartXmlSizeLimitExceeded");
    }

    /// <summary>
    /// 修正前：MaxEntrySize（long）可由使用者調高到遠超過 Int32.MaxValue，
    /// 之後對 boundedStream.Length 的 (int) 轉型會靜默溢位並截斷實際字元數，而非丟出例外。
    /// 修正後：超過 Int32.MaxValue 必須明確擲出例外。
    /// </summary>
    [Fact]
    public void EnsureLengthFitsInInt32_LengthExceedsInt32MaxValue_ThrowsSecurityExceptionWithLocalizedMessage()
    {
        long overflowingLength = (long)int.MaxValue + 1000L;

        SecurityException ex = Assert.Throws<SecurityException>(() =>
            SpreadsheetDocumentEmbeddedChartReadEngine.EnsureLengthFitsInInt32(
                overflowingLength, "Err_SpreadsheetDocumentEmbeddedChartReadEngine_ChartXmlSizeLimitExceeded"));

        Assert.Equal(
            OdfLocalizer.GetMessage(
                "Err_SpreadsheetDocumentEmbeddedChartReadEngine_ChartXmlSizeLimitExceeded",
                overflowingLength,
                int.MaxValue),
            ex.Message);
    }

    private static SpreadsheetDocument BuildSpreadsheetWithChart()
    {
        var workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("Sheet1");
        workbook.AddChart("Sheet1", new OdfCellAddress(0, 2, "Sheet1"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "Trend",
            DataRange = new OdfCellRange(0, 0, 3, 1, "Sheet1"),
        });
        return workbook;
    }
}
