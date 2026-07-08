using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定低魔法 template binder 在常見文件類型的替換行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class TemplateBinderScenarioTests
{
    /// <summary>
    /// 驗證 ODT 段落占位符可替換。
    /// </summary>
    [Fact]
    public void Bind_ReplacesTextDocumentParagraphPlaceholders()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Hello {{Name}}");

        int count = TemplateBinder.Bind(document, new Dictionary<string, object?> { ["Name"] = "OdfKit" });

        Assert.Equal(1, count);
        Assert.Contains("Hello OdfKit", document.BodyTextRoot.TextContent);
    }

    /// <summary>
    /// 驗證 ODS 已使用儲存格占位符可替換。
    /// </summary>
    [Fact]
    public void Bind_ReplacesSpreadsheetCellPlaceholders()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "{{Name}}";
        sheet.Cells["A2"].CellValue = "NT$ {{Amount}}";

        int changed = TemplateBinder.Bind(document, new Dictionary<string, object?>
        {
            ["Name"] = "星河股份有限公司",
            ["Amount"] = 1200,
        });

        Assert.Equal(2, changed);
        Assert.Equal("星河股份有限公司", sheet.Cells["A1"].DisplayText);
        Assert.Equal("NT$ 1200", sheet.Cells["A2"].DisplayText);
    }

    /// <summary>
    /// 驗證 ODP 文字方塊占位符可替換。
    /// </summary>
    [Fact]
    public void Bind_ReplacesPresentationTextBoxPlaceholders()
    {
        using PresentationDocument document = PresentationDocument.Create();
        document.AddSlide("Intro").AddTextBox(1.Cm(), 1.Cm(), 8.Cm(), 2.Cm(), "歡迎 {{Name}}");

        int changed = TemplateBinder.Bind(document, new Dictionary<string, object?> { ["Name"] = "OdfKit" });

        Assert.Equal(1, changed);
        Assert.Equal("歡迎 OdfKit", document.Slides[0].TextBoxes[0].Text);
    }

    /// <summary>
    /// 驗證 ODG 文字方塊占位符可替換。
    /// </summary>
    [Fact]
    public void Bind_ReplacesDrawingTextBoxPlaceholders()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Canvas");
        page.AddTextBox(1.Cm(), 1.Cm(), 8.Cm(), 2.Cm(), "流程：{{Step}}");

        int changed = TemplateBinder.Bind(document, new Dictionary<string, object?> { ["Step"] = "驗證" });

        Assert.Equal(1, changed);
        Assert.Equal("流程：驗證", document.Pages[0].TextBoxes[0].Text);
    }
}
