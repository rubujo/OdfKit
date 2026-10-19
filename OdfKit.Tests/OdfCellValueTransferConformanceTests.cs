using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests current value preservation through cell moves and tracked change rejection.
/// 測試移動儲存格與拒絕追蹤修訂時的目前值保存。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfCellValueTransferConformanceTests
{
    private static readonly OdfVersion[] Versions = [OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14];

    /// <summary>
    /// Moving values preserves native attributes and clears both prior source and target value attributes.
    /// 移動值會保留原生屬性，並清除來源與目標先前的值屬性。
    /// </summary>
    [Theory]
    [InlineData("time", "time-value", "P1M")]
    [InlineData("string", "string-value", "Stored")]
    [InlineData("string", "string-value", "")]
    [InlineData("currency", "currency", "USD")]
    [InlineData("currency", "currency", " 私有 ")]
    [InlineData("currency", "currency", "")]
    public void MovingCellsPreservesCurrentValueAttributes(string type, string attribute, string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, type, attribute, lexical);
            OdfTableSheet sheet = document.FindSheet("Data")!;
            sheet.GetCell(0, 1).CellValue = TimeSpan.FromHours(2);
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            sheet.MoveCell(0, 0, 0, 1);
            OdfCell target = sheet.GetCell(0, 1);
            Assert.Equal(type, target.ValueType);
            Assert.Equal(lexical, target.Node.GetAttribute(attribute, OdfNamespaces.Office));
            foreach (string name in new[] { "value-type", "value", "boolean-value", "date-value", "time-value", "string-value", "currency" })
                Assert.Null(sheet.GetCell(0, 0).Node.GetAttribute(name, OdfNamespaces.Office));
            if (type != "time")
                Assert.Null(target.Node.GetAttribute("time-value", OdfNamespaces.Office));
            if (type == "currency")
                Assert.Equal("1.25", target.RawValue);
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
        }
    }

    /// <summary>
    /// Rejecting an edited cell restores the original native value instead of only display text.
    /// 拒絕儲存格編輯會還原原生值，而不僅還原顯示文字。
    /// </summary>
    [Theory]
    [InlineData("time", "time-value", "P1M")]
    [InlineData("string", "string-value", "Stored")]
    [InlineData("string", "string-value", "")]
    [InlineData("currency", "currency", "USD")]
    [InlineData("currency", "currency", " 私有 ")]
    [InlineData("currency", "currency", "")]
    public void RejectingCellEditsRestoresCurrentValueAttributes(string type, string attribute, string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, type, attribute, lexical);
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            document.TrackedChanges = true;
            document.FindSheet("Data")!.GetCell(0, 0).CellValue = TimeSpan.FromHours(2);
            OdfSpreadsheetTrackedChangeInfo change = Assert.Single(document.GetTrackedChanges());
            document.RejectChange(change.ChangeId);
            Assert.Empty(document.GetTrackedChanges());
            OdfCell restored = document.FindSheet("Data")!.GetCell(0, 0);
            Assert.Equal(type, restored.ValueType);
            Assert.Equal(lexical, restored.Node.GetAttribute(attribute, OdfNamespaces.Office));
            if (type != "time")
                Assert.Null(restored.Node.GetAttribute("time-value", OdfNamespaces.Office));
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
        }
    }

    /// <summary>
    /// Rejecting a tracked movement restores current values to their original cell.
    /// 拒絕已追蹤的移動會將目前值還原至原儲存格。
    /// </summary>
    [Theory]
    [InlineData("time", "time-value", "P1M")]
    [InlineData("string", "string-value", "Stored")]
    [InlineData("string", "string-value", "")]
    [InlineData("currency", "currency", "USD")]
    [InlineData("currency", "currency", " 私有 ")]
    [InlineData("currency", "currency", "")]
    public void RejectingCellMovesRestoresCurrentValueAttributes(string type, string attribute, string lexical)
    {
        foreach (OdfVersion version in Versions)
        {
            using SpreadsheetDocument document = Document(version, type, attribute, lexical);
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
            document.TrackedChanges = true;
            OdfTableSheet sheet = document.FindSheet("Data")!;
            sheet.MoveCell(0, 0, 0, 1);
            OdfSpreadsheetTrackedChangeInfo change = Assert.Single(document.GetTrackedChanges());
            document.RejectChange(change.ChangeId);
            Assert.Empty(document.GetTrackedChanges());
            OdfCell restored = sheet.GetCell(0, 0);
            Assert.Equal(type, restored.ValueType);
            Assert.Equal(lexical, restored.Node.GetAttribute(attribute, OdfNamespaces.Office));
            foreach (string name in new[] { "value-type", "value", "boolean-value", "date-value", "time-value", "string-value", "currency" })
                Assert.Null(sheet.GetCell(0, 1).Node.GetAttribute(name, OdfNamespaces.Office));
            OdfCellCurrentValueConformanceTests.SaveAndValidate(document, version);
        }
    }

    private static SpreadsheetDocument Document(OdfVersion version, string type, string attribute, string lexical)
    {
        SpreadsheetDocument document = OdfCellCurrentValueConformanceTests.Document(version, type, attribute, lexical, "Displayed", false);
        if (type == "currency")
            document.FindSheet("Data")!.GetCell(0, 0).Node.SetAttribute("value", OdfNamespaces.Office, "1.25", "office");
        return document;
    }
}
