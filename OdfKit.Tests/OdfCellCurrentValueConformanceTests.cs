using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests current cell values independently of their display text and CLR representation capacity.
/// 測試目前儲存格值與顯示文字的區別，以及 CLR 表示容量的邊界。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfCellCurrentValueConformanceTests
{
    private static readonly OdfVersion[] Versions = [OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14];

    /// <summary>
    /// Explicit string values take precedence over display text, including an explicit empty string.
    /// 明示的字串值優先於顯示文字，包含明示的空字串。
    /// </summary>
    [Theory]
    [InlineData("Stored", "Displayed")]
    [InlineData("", "Displayed")]
    [InlineData("文字😀", "")]
    public void ExplicitStringValuesRemainDistinctFromDisplayText(string stored, string displayed)
    {
        foreach (OdfVersion version in Versions)
            foreach (bool padded in new[] { false, true })
            {
                using SpreadsheetDocument document = Document(version, "string", "string-value", stored, displayed, padded);
                byte[] bytes = SaveAndValidate(document, version);
                Assert.Equal(stored, document.FindSheet("Data")!.GetCell(0, 0).CellValue);
                using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
                Assert.Equal(stored, loaded.FindSheet("Data")!.GetCell(0, 0).CellValue);
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                Assert.True(reader.Read());
                Assert.Equal(stored, reader.GetValue(0));
                Assert.Equal(displayed, reader.GetCell(0).DisplayText ?? string.Empty);
                Assert.Equal(padded ? " \tstring\r\n" : "string", reader.GetCell(0).RawValueType);
            }
    }

    /// <summary>
    /// Duration values without an exact TimeSpan representation remain their original lexical strings.
    /// 無法以 TimeSpan 精確表示的時間長度值保留原始詞彙字串。
    /// </summary>
    [Theory]
    [InlineData("P1Y")]
    [InlineData("P1M")]
    [InlineData("PT0.000000001S")]
    [InlineData("PT0.123456789S")]
    [InlineData("P999999999999999999999D")]
    public void DomTimeValuesPreserveUnrepresentableDurations(string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, "time", "time-value", lexical, "Displayed", false);
            byte[] bytes = SaveAndValidate(document, version);
            Assert.Equal(lexical, Assert.IsType<string>(document.FindSheet("Data")!.GetCell(0, 0).CellValue));
            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
            Assert.Equal(lexical, Assert.IsType<string>(loaded.FindSheet("Data")!.GetCell(0, 0).CellValue));
        }
    }

    /// <summary>
    /// Both streaming paths preserve calendar units, excess fractional precision and large duration components.
    /// 兩條串流路徑均保留曆法單位、超出的小數精度與大型時間長度分量。
    /// </summary>
    [Theory]
    [InlineData("P1Y")]
    [InlineData("P1M")]
    [InlineData("PT0.000000001S")]
    [InlineData("PT0.123456789S")]
    [InlineData("P999999999999999999999D")]
    public async Task StreamTimeValuesPreserveUnrepresentableDurations(string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, "time", "time-value", lexical, "Displayed", false);
            byte[] bytes = SaveAndValidate(document, version);
            foreach (bool asynchronous in new[] { false, true })
            {
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                bool read = asynchronous ? await reader.ReadAsync(TestContext.Current.CancellationToken) : reader.Read();
                Assert.True(read);
                Assert.Equal(lexical, Assert.IsType<string>(reader.GetValue(0)));
                Assert.Equal("Displayed", reader.GetCell(0).DisplayText);
            }
        }
    }

    /// <summary>
    /// Exactly representable time values keep typed results, including zero calendar components and tick boundaries.
    /// 可精確表示的時間值維持型別化結果，包含零曆法分量與刻度邊界。
    /// </summary>
    [Theory]
    [InlineData("PT1H", 36000000000L)]
    [InlineData("P0Y0M1DT2H", 936000000000L)]
    [InlineData("-PT0.0000001S", -1L)]
    [InlineData("P10675199DT2H48M5.4775807S", long.MaxValue)]
    [InlineData("-P10675199DT2H48M5.4775808S", long.MinValue)]
    public void RepresentableTimesRetainTypedValues(string lexical, long ticks)
    {
        foreach (OdfVersion version in Versions)
            foreach (bool padded in new[] { false, true })
            {
                using SpreadsheetDocument document = Document(version, "time", "time-value", lexical, "Displayed", padded);
                byte[] bytes = SaveAndValidate(document, version);
                Assert.Equal(ticks, Assert.IsType<TimeSpan>(document.FindSheet("Data")!.GetCell(0, 0).CellValue).Ticks);
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                Assert.True(reader.Read());
                Assert.Equal(ticks, Assert.IsType<TimeSpan>(reader.GetValue(0)).Ticks);
            }
    }

    /// <summary>
    /// Boolean values follow the current value attribute and token whitespace without using display text.
    /// 布林值依目前值屬性與 token 空白處理，不使用顯示文字代替。
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(" \ttrue\r\n", true)]
    [InlineData(" \tfalse\r\n", false)]
    public void BooleanCurrentValuesUseTheirDeclaredType(string lexical, bool expected)
    {
        foreach (OdfVersion version in Versions)
            foreach (bool padded in new[] { false, true })
            {
                using SpreadsheetDocument document = Document(version, "boolean", "boolean-value", lexical, "Displayed", padded);
                byte[] bytes = SaveAndValidate(document, version);
                Assert.Equal(expected, Assert.IsType<bool>(document.FindSheet("Data")!.GetCell(0, 0).CellValue));
                using OdsStreamReader reader = new(new MemoryStream(bytes));
                Assert.True(reader.Read());
                Assert.Equal(expected, Assert.IsType<bool>(reader.GetValue(0)));
            }
    }

    /// <summary>
    /// Legacy current value attributes retain their type conditions while string values remain optional.
    /// 舊版目前值屬性維持型別條件，而字串值保持可選。
    /// </summary>
    [Theory]
    [InlineData("boolean", "boolean-value", "true")]
    [InlineData("string", "string-value", "Stored")]
    [InlineData("time", "time-value", "PT1H")]
    public void LegacyCurrentValueModelsRetainTheirConditions(string type, string attribute, string lexical)
    {
        using SpreadsheetDocument document = Document(OdfVersion.Odf11, type, attribute, lexical, "Displayed", false);
        SaveAndValidate(document, OdfVersion.Odf11);
        OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
        cell.ValueType = "float";
        cell.Node.SetAttribute("value", OdfNamespaces.Office, "1", "office");
        using MemoryStream invalid = new();
        document.SaveToStream(invalid);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(invalid.ToArray()));
        Assert.False(OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf11).IsValid);
        if (type == "string")
        {
            cell.ValueType = "string";
            cell.Node.RemoveAttribute("value", OdfNamespaces.Office);
            cell.Node.RemoveAttribute("string-value", OdfNamespaces.Office);
            SaveAndValidate(document, OdfVersion.Odf11);
            Assert.Equal("Displayed", cell.CellValue);
        }
    }

    /// <summary>
    /// Assigning a TimeSpan writes a native time value and removes cached attributes for other types.
    /// 指定 TimeSpan 寫入原生時間值，並移除其他型別的快取屬性。
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void AssigningTypedTimesProducesNativeDurationValues(long ticks)
    {
        TimeSpan expected = TimeSpan.FromTicks(ticks);
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, "string", "string-value", "Stale", "Displayed", false);
            OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
            cell.Node.SetAttribute("boolean-value", OdfNamespaces.Office, "true", "office");
            cell.Node.SetAttribute("date-value", OdfNamespaces.Office, "2026-10-16", "office");
            cell.Node.SetAttribute("currency", OdfNamespaces.Office, "USD", "office");
            cell.Node.SetAttribute("value", OdfNamespaces.Office, "1", "office");
            cell.CellValue = expected;
            Assert.Equal("time", cell.ValueType);
            Assert.Equal(expected, Assert.IsType<TimeSpan>(cell.CellValue));
            foreach (string name in new[] { "string-value", "boolean-value", "date-value", "currency", "value" })
                Assert.Null(cell.Node.GetAttribute(name, OdfNamespaces.Office));
            byte[] bytes = SaveAndValidate(document, version);
            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(new MemoryStream(bytes));
            Assert.Equal(expected, loaded.FindSheet("Data")!.GetCell(0, 0).CellValue);
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(expected, reader.GetValue(0));
        }
    }

    /// <summary>
    /// Assigning a Boolean retains native Boolean values and removes prior string values.
    /// 指定布林值維持原生布林資料，並移除先前的字串值。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssigningBooleansProducesNativeCurrentValues(bool expected)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, "string", "string-value", "Stale", "Displayed", false);
            OdfCell cell = document.FindSheet("Data")!.GetCell(0, 0);
            cell.CellValue = expected;
            Assert.Equal("boolean", cell.ValueType);
            Assert.Null(cell.Node.GetAttribute("string-value", OdfNamespaces.Office));
            byte[] bytes = SaveAndValidate(document, version);
            Assert.Equal(expected, Assert.IsType<bool>(cell.CellValue));
            using OdsStreamReader reader = new(new MemoryStream(bytes));
            Assert.True(reader.Read());
            Assert.Equal(expected, reader.GetValue(0));
        }
    }

    /// <summary>
    /// Large valid duration components and fractional precision remain raw values through both readers.
    /// 大型合法時間長度分量與小數精度在兩種讀取器中維持原始值。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongDurationValuesRetainTheirData(bool fractional)
    {
        string lexical = fractional ? "PT0." + new string('0', 100_000) + "1S" : "P" + new string('9', 100_000) + "D";
        using SpreadsheetDocument document = Document(OdfVersion.Odf14, "time", "time-value", lexical, "Displayed", false);
        byte[] bytes = SaveAndValidate(document, OdfVersion.Odf14);
        Assert.Equal(lexical, document.FindSheet("Data")!.GetCell(0, 0).CellValue);
        using OdsStreamReader reader = new(new MemoryStream(bytes));
        Assert.True(reader.Read());
        Assert.Equal(lexical, reader.GetValue(0));
    }

    internal static SpreadsheetDocument Document(OdfVersion version, string type, string attribute, string lexical, string displayed, bool padded)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.TargetVersion = version;
        OdfCell cell = document.AddSheet("Data").GetCell(0, 0);
        cell.CellValue = displayed;
        cell.ValueType = padded ? " \t" + type + "\r\n" : type;
        cell.Node.SetAttribute(attribute, OdfNamespaces.Office, lexical, "office");
        return document;
    }

    internal static byte[] SaveAndValidate(SpreadsheetDocument document, OdfVersion version)
    {
        using MemoryStream output = new();
        document.SaveToStream(output);
        byte[] bytes = output.ToArray();
        Validate(bytes, version);
        return bytes;
    }

    internal static void Validate(byte[] bytes, OdfVersion version)
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
        OdfComplianceProfile[] profiles = version switch
        {
            OdfVersion.Odf10 => [OdfComplianceProfiles.OasisOdf10],
            OdfVersion.Odf11 => [OdfComplianceProfiles.OasisOdf11],
            OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
            OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
            _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
        };
        foreach (OdfComplianceProfile profile in profiles)
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }
}
