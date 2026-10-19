using System.Globalization;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests numeric, currency and date cell data independently of their display representations.
/// 測試數值、貨幣與日期儲存格資料，不依賴其顯示表示法。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfCellScalarValueConformanceTests
{
    private static readonly OdfVersion[] Versions = [OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14];

    /// <summary>
    /// Untyped date access keeps dates outside CLR range or precision as their original strings.
    /// 未型別化日期存取將超出 CLR 範圍或精度的日期保留為原字串。
    /// </summary>
    [Theory]
    [InlineData("-0001-01-01")]
    [InlineData("12026-01-01")]
    [InlineData("2026-10-16T12:30:00.123456789Z")]
    [InlineData("2026-10-16T12:30:00.000000001Z")]
    public async Task DatesPreserveValuesOutsideClrRepresentation(string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(version, "date", "date-value", lexical, "Displayed", false);
            byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            Assert.Equal(lexical, Assert.IsType<string>(document.FindSheet("Data")!.GetCell(0, 0).CellValue));
            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
            Assert.Equal(lexical, Assert.IsType<string>(loaded.FindSheet("Data")!.GetCell(0, 0).CellValue));
            foreach (bool asynchronous in new[] { false, true })
            {
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                Assert.True(asynchronous ? await reader.ReadAsync(TestContext.Current.CancellationToken) : reader.Read());
                Assert.Equal(lexical, Assert.IsType<string>(reader.GetValue(0)));
            }
        }
    }

    /// <summary>
    /// Representable date and date-time values keep their DateTime result types.
    /// 可表示的日期與日期時間值維持 DateTime 結果型別。
    /// </summary>
    [Theory]
    [InlineData("2026-10-16")]
    [InlineData("0001-01-01T00:00:00Z")]
    [InlineData("9999-12-31T23:59:59.9999999Z")]
    [InlineData("2026-10-16T12:30:00.1234567000Z")]
    public void RepresentableDatesRetainTypedResults(string lexical)
    {
        DateTime expected = DateTime.Parse(lexical, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(version, "date", "date-value", lexical, "Displayed", true);
            byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            Assert.Equal(expected, document.FindSheet("Data")!.GetCell(0, 0).CellValue);
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(expected, reader.GetValue(0));
        }
    }

    /// <summary>
    /// All numeric type flags use office:value, including XML Schema special floating-point values.
    /// 所有數值型別旗標均使用 office:value，包含 XML Schema 特殊浮點值。
    /// </summary>
    [Theory]
    [InlineData("float", "INF", double.PositiveInfinity)]
    [InlineData("float", "-INF", double.NegativeInfinity)]
    [InlineData("float", "NaN", double.NaN)]
    [InlineData("float", "1.25", 1.25)]
    [InlineData("percentage", "INF", double.PositiveInfinity)]
    [InlineData("percentage", "-INF", double.NegativeInfinity)]
    [InlineData("percentage", "NaN", double.NaN)]
    [InlineData("percentage", "1.25", 1.25)]
    [InlineData("currency", "INF", double.PositiveInfinity)]
    [InlineData("currency", "-INF", double.NegativeInfinity)]
    [InlineData("currency", "NaN", double.NaN)]
    [InlineData("currency", "1.25", 1.25)]
    public void NumericTypesUseCurrentValues(string type, string lexical, double expected)
    {
        foreach (OdfVersion version in Versions)
            foreach (bool padded in new[] { false, true })
            {
                using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(version, type, "value", " \t" + lexical + "\r\n", "Displayed", padded);
                byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
                Assert.Equal(expected, Assert.IsType<double>(document.FindSheet("Data")!.GetCell(0, 0).CellValue));
                using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
                Assert.Equal(expected, Assert.IsType<double>(loaded.FindSheet("Data")!.GetCell(0, 0).CellValue));
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                Assert.True(reader.Read());
                Assert.Equal(expected, Assert.IsType<double>(reader.GetValue(0)));
            }
    }

    /// <summary>
    /// Currency identifiers retain their native string model rather than an invented closed code list.
    /// 貨幣識別值維持原生字串模型，不套用自行增加的封閉代碼清單。
    /// </summary>
    [Theory]
    [InlineData("DEM")]
    [InlineData("EUR")]
    [InlineData("X-PRIVATE")]
    [InlineData("")]
    [InlineData(" 私有 ")]
    public void CurrencyIdentifiersKeepTheirOriginalData(string currency)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(version, "currency", "value", "12.5", "Displayed", false);
            OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
            cell.Node.SetAttribute("currency", OdfNamespaces.Office, currency, "office");
            byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
            Assert.Equal(currency, loaded.FindSheet("Data")!.GetCell(0, 0).Node.GetAttribute("currency", OdfNamespaces.Office));
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(currency, reader.GetCell(0).Currency);
        }
    }

    /// <summary>
    /// DOM numeric producers emit XML floating-point lexical values.
    /// DOM 數值產生器輸出 XML 浮點詞彙值。
    /// </summary>
    [Theory]
    [InlineData(double.PositiveInfinity, "INF")]
    [InlineData(double.NegativeInfinity, "-INF")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(1.25, "1.25")]
    public void NumericDomProducersUseXmlLexicalValues(double value, string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create();
            document.TargetVersion = version;
            OdfCell cell = document.AddSheet("Data").GetCell(0, 0);
            cell.SetValue(value);
            Assert.Equal(lexical, cell.RawValue);
            byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(value, Assert.IsType<double>(reader.GetValue(0)));
        }
    }

    /// <summary>
    /// Both direct and parallel worksheet writers produce native XML floating-point values.
    /// 直接與平行工作表寫入器均產生原生 XML 浮點值。
    /// </summary>
    [Theory]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    [InlineData(false, double.NaN)]
    [InlineData(false, 1.25)]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    [InlineData(true, double.NaN)]
    [InlineData(true, 1.25)]
    public async Task NumericStreamProducersUseXmlLexicalValues(bool parallel, double value)
    {
        foreach (OdfVersion version in Versions)
        {
            using MemoryStream output = new();
            using (OdsStreamWriter writer = new(output, version))
            {
                if (parallel)
                    await writer.WriteSheetsAsync([new OdsSheetWriteJob("Data", (sheet, _) =>
                    {
                        sheet.WriteStartRow();
                        sheet.WriteCell(value);
                        sheet.WriteEndRow();
                        return Task.CompletedTask;
                    })], maxConcurrency: 1, cancellationToken: TestContext.Current.CancellationToken);
                else
                {
                    writer.WriteStartSheet("Data");
                    writer.WriteStartRow();
                    writer.WriteCell(value);
                    writer.WriteEndRow();
                    writer.WriteEndSheet();
                }
            }
            byte[] bytes = output.ToArray();
            OdfCellCurrentValueConformanceTests.Validate(bytes, version);
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(value, Assert.IsType<double>(reader.GetValue(0)));
        }
    }
    /// <summary>
    /// Explicit typed or DOM column declarations are preserved without an extra default column.
    /// 明示的型別化或 DOM 欄宣告維持原狀，不額外插入缺省欄。
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitColumnsKeepTheirPositions(bool buffered, bool dom)
    {
        foreach (OdfVersion version in Versions)
        {
            using MemoryStream output = new();
            using (OdsStreamWriter writer = new(output, version))
            {
                if (buffered)
                    writer.SwitchToSheet("Data");
                else
                    writer.WriteStartSheet("Data");
                if (dom)
                {
                    OdfNode column = OdfNodeFactory.CreateElement("table-column", OdfNamespaces.Table, "renamed");
                    column.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "2", "renamed");
                    writer.WriteNode(column);
                }
                else
                {
                    writer.WriteColumn(OdfLength.FromCentimeters(2));
                    writer.WriteColumn(OdfLength.FromCentimeters(3));
                }
                writer.WriteStartRow();
                writer.WriteCell(1.25);
                writer.WriteCell(2.5);
                writer.WriteEndRow();
                if (!buffered)
                    writer.WriteEndSheet();
            }
            byte[] bytes = output.ToArray();
            OdfCellCurrentValueConformanceTests.Validate(bytes, version);
            XElement[] columns = Content(bytes).Descendants(XName.Get("table-column", OdfNamespaces.Table)).ToArray();
            Assert.Equal(dom ? 1 : 2, columns.Length);
            if (dom)
                Assert.Equal("2", (string?)columns[0].Attribute(XName.Get("number-columns-repeated", OdfNamespaces.Table)));
        }
    }

    /// <summary>
    /// Default column state stays independent for sequential and interleaved worksheets.
    /// 缺省欄狀態在順序及交錯寫入的工作表間維持獨立。
    /// </summary>
    [Fact]
    public void DefaultColumnsRemainScopedToTheirSheets()
    {
        foreach (OdfVersion version in Versions)
        {
            using MemoryStream output = new();
            using (OdsStreamWriter writer = new(output, version))
            {
                writer.WriteStartSheet("Direct");
                writer.WriteStartRow();
                writer.WriteCell(1d);
                writer.WriteEndRow();
                writer.WriteEndSheet();
                Assert.Equal(0, writer.BufferedSheetCountForTests);
                foreach (string name in new[] { "First", "Second", "First" })
                {
                    writer.SwitchToSheet(name);
                    writer.WriteStartRow();
                    writer.WriteCell(2d);
                    writer.WriteEndRow();
                }
            }
            byte[] bytes = output.ToArray();
            OdfCellCurrentValueConformanceTests.Validate(bytes, version);
            XElement[] sheets = Content(bytes).Descendants(XName.Get("table", OdfNamespaces.Table)).ToArray();
            Assert.Equal(3, sheets.Length);
            foreach (XElement sheet in sheets)
                Assert.Single(sheet.Elements(XName.Get("table-column", OdfNamespaces.Table)));
        }
    }

    /// <summary>
    /// Raw row subtrees receive one default declaration before the first row in both writer modes.
    /// 原始列子樹在兩種寫入模式的第一列之前取得一份缺省欄宣告。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRowsReceiveDefaultColumns(bool buffered)
    {
        foreach (OdfVersion version in Versions)
        {
            using MemoryStream output = new();
            using (OdsStreamWriter writer = new(output, version))
            {
                if (buffered)
                    writer.SwitchToSheet("Data");
                else
                    writer.WriteStartSheet("Data");
                OdfNode row = OdfNodeFactory.CreateElement("table-row", OdfNamespaces.Table, "renamed");
                OdfNode cell = OdfNodeFactory.CreateElement("table-cell", OdfNamespaces.Table, "renamed");
                cell.SetAttribute("value-type", OdfNamespaces.Office, "float", "renamedOffice");
                cell.SetAttribute("value", OdfNamespaces.Office, "1", "renamedOffice");
                row.AppendChild(cell);
                writer.WriteNode(row);
                writer.WriteStartRow();
                writer.WriteCell(2d);
                writer.WriteEndRow();
                if (!buffered)
                    writer.WriteEndSheet();
            }
            byte[] bytes = output.ToArray();
            OdfCellCurrentValueConformanceTests.Validate(bytes, version);
            Assert.Single(Content(bytes).Descendants(XName.Get("table-column", OdfNamespaces.Table)));
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(1d, reader.GetValue(0));
            Assert.True(reader.Read());
            Assert.Equal(2d, reader.GetValue(0));
        }
    }

    /// <summary>
    /// Legacy scalar values and currency identifiers remain limited to their declared value type conditions.
    /// 舊版純量值與貨幣識別值仍受宣告的值型別條件限制。
    /// </summary>
    [Theory]
    [InlineData("date", "date-value", "2026-10-16")]
    [InlineData("float", "value", "1.25")]
    [InlineData("percentage", "value", "1.25")]
    [InlineData("currency", "value", "1.25")]
    [InlineData("currency", "currency", "USD")]
    public void LegacyScalarModelsRetainTheirTypeConditions(string type, string attribute, string lexical)
    {
        using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(OdfVersion.Odf11, type, attribute, lexical, "Displayed", false);
        OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
        if (attribute == "currency")
            cell.Node.SetAttribute("value", OdfNamespaces.Office, "1.25", "office");
        OdfCellCurrentValueConformanceTests.SaveAndValidate(document, OdfVersion.Odf11);
        cell.ValueType = attribute == "value" ? "string" : "float";
        if (attribute == "date-value")
            cell.Node.SetAttribute("value", OdfNamespaces.Office, "1.25", "office");
        using MemoryStream output = new();
        document.SaveToStream(output);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
        Assert.Contains(OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf11).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// Legacy value type tokens reject unknown values and incorrect case.
    /// 舊版值型別 token 拒絕未知值與錯誤大小寫。
    /// </summary>
    [Theory]
    [InlineData("FLOAT")]
    [InlineData("DATE")]
    [InlineData("number")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("string boolean")]
    public void LegacyValueTypesRetainTheirDeclaredTokens(string type)
    {
        using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(OdfVersion.Odf11, "string", "string-value", "Stored", "Displayed", false);
        OdfCellCurrentValueConformanceTests.SaveAndValidate(document, OdfVersion.Odf11);
        document.FindSheet("Data")!.GetCell(0, 0).ValueType = type;
        using MemoryStream output = new();
        document.SaveToStream(output);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
        Assert.Contains(OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf11).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// The optional legacy value model permits display text without any cached type or value attributes.
    /// 可選的舊版值模型允許只有顯示文字而沒有快取型別或值屬性。
    /// </summary>
    [Fact]
    public void LegacyValueTypesRemainOptionalWithoutCachedValues()
    {
        using SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(OdfVersion.Odf11, "string", "string-value", "Stored", "Displayed", false);
        OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
        cell.Node.RemoveAttribute("value-type", OdfNamespaces.Office);
        cell.Node.RemoveAttribute("string-value", OdfNamespaces.Office);
        byte[] bytes = OdfCellCurrentValueConformanceTests.SaveAndValidate(document, OdfVersion.Odf11);
        Assert.Equal("Displayed", cell.CellValue);
        using OdsStreamReader reader = new(new MemoryStream(bytes));
        Assert.True(reader.Read());
        Assert.Equal("Displayed", reader.GetValue(0));
        Assert.Null(reader.GetCell(0).RawValueType);
    }

    private static XElement Content(byte[] bytes)
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
        using Stream content = package.GetEntryStream("content.xml");
        return XElement.Load(content);
    }
}
