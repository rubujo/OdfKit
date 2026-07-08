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
    /// 驗證 ODT 集合占位符會複製模板段落。
    /// </summary>
    [Fact]
    public void Bind_ExpandsTextDocumentCollectionParagraphs()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("項目：{{Items[].Name}}={{Items[].Amount}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["Items"] = new[]
                {
                    new Dictionary<string, object?> { ["Name"] = "A", ["Amount"] = 10 },
                    new Dictionary<string, object?> { ["Name"] = "B", ["Amount"] = 20 },
                }
            },
            new OdfTemplateBindOptions());

        Assert.Equal(2, report.ExpandedItemCount);
        Assert.Contains("Items", report.ExpandedCollections);
        Assert.True(report.PlaceholderHits["Items[].Name"] >= 2);
        Assert.Contains("項目：A=10", document.BodyTextRoot.TextContent);
        Assert.Contains("項目：B=20", document.BodyTextRoot.TextContent);
        Assert.DoesNotContain("{{Items[]", document.BodyTextRoot.TextContent);
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
    /// 驗證 ODS 集合占位符會複製模板列並保留相鄰欄位。
    /// </summary>
    [Fact]
    public void Bind_ExpandsSpreadsheetCollectionRows()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "{{Items[].Name}}";
        sheet.Cells["B1"].CellValue = "{{Items[].Amount}}";

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["Items"] = new[]
                {
                    new { Name = "設計", Amount = 100 },
                    new { Name = "驗證", Amount = 200 },
                }
            },
            new OdfTemplateBindOptions());

        Assert.Equal(2, report.ExpandedItemCount);
        Assert.Equal("設計", sheet.Cells["A1"].DisplayText);
        Assert.Equal("100", sheet.Cells["B1"].DisplayText);
        Assert.Equal("驗證", sheet.Cells["A2"].DisplayText);
        Assert.Equal("200", sheet.Cells["B2"].DisplayText);
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
    /// 驗證 ODP 文字方塊集合段落可展開。
    /// </summary>
    [Fact]
    public void Bind_ExpandsPresentationTextBoxCollectionParagraphs()
    {
        using PresentationDocument document = PresentationDocument.Create();
        document.AddSlide("Intro").AddTextBox(1.Cm(), 1.Cm(), 8.Cm(), 2.Cm(), "{{Items[].Name}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?> { ["Items"] = new[] { new { Name = "Alpha" }, new { Name = "Beta" } } },
            new OdfTemplateBindOptions());

        Assert.Equal(2, report.ExpandedItemCount);
        Assert.Contains("Alpha", document.Slides[0].TextBoxes[0].Node.TextContent);
        Assert.Contains("Beta", document.Slides[0].TextBoxes[0].Node.TextContent);
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

    /// <summary>
    /// 驗證 ODG 文字方塊集合段落可展開。
    /// </summary>
    [Fact]
    public void Bind_ExpandsDrawingTextBoxCollectionParagraphs()
    {
        using DrawingDocument document = DrawingDocument.Create();
        document.Pages.Add("Canvas").AddTextBox(1.Cm(), 1.Cm(), 8.Cm(), 2.Cm(), "{{Items[].Name}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?> { ["Items"] = new[] { new { Name = "開始" }, new { Name = "結束" } } },
            new OdfTemplateBindOptions());

        Assert.Equal(2, report.ExpandedItemCount);
        Assert.Contains("開始", document.Pages[0].TextBoxes[0].Node.TextContent);
        Assert.Contains("結束", document.Pages[0].TextBoxes[0].Node.TextContent);
    }

    /// <summary>
    /// 驗證混用多個集合來源時會記入 report 而不展開。
    /// </summary>
    [Fact]
    public void Bind_MultipleCollectionsInOneTemplateNode_AreReported()
    {
        using DrawingDocument document = DrawingDocument.Create();
        document.Pages.Add("Canvas").AddTextBox(1.Cm(), 1.Cm(), 8.Cm(), 2.Cm(), "{{A[].Name}} {{B[].Name}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["A"] = new[] { new { Name = "One" } },
                ["B"] = new[] { new { Name = "Two" } },
            },
            new OdfTemplateBindOptions());

        Assert.Equal(0, report.ExpandedItemCount);
        Assert.Contains("A", report.UnresolvedPlaceholders);
        Assert.Contains("B", report.UnresolvedPlaceholders);
        Assert.Contains(report.Warnings, warning => warning.Contains("TPL0001", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// 驗證 dry run 不修改文件，但會回報實際 placeholder 命中數。
    /// </summary>
    [Fact]
    public void Bind_DryRunReportsHitsWithoutMutatingDocument()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("{{Name}} / {{Name}} / {{Missing}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?> { ["Name"] = "OdfKit" },
            new OdfTemplateBindOptions
            {
                DryRun = true,
                StrictMode = true
            });

        Assert.Equal(2, report.ReplacementCount);
        Assert.Equal(1, report.ChangedNodeCount);
        Assert.Equal(2, report.PlaceholderHits["Name"]);
        Assert.Contains("{{Name}}", document.BodyTextRoot.TextContent);
        Assert.Contains("Missing", report.UnresolvedPlaceholders);
        Assert.Contains(report.UnresolvedPlaceholderDetails, detail => detail.Expression == "Missing" && detail.DocumentKind == "TextDocument");
        Assert.Contains(report.Warnings, warning => warning.Contains("TPL0002", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// 驗證未知 placeholder 可依選項清成空字串。
    /// </summary>
    [Fact]
    public void Bind_UnknownPlaceholderPolicyCanClearUnresolvedTokens()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "Hello {{Missing}}";

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>(),
            new OdfTemplateBindOptions
            {
                UnknownPlaceholderPolicy = OdfTemplateUnknownPlaceholderPolicy.EmptyString
            });

        Assert.Equal("Hello ", sheet.Cells["A1"].DisplayText);
        Assert.Contains("Missing", report.UnresolvedPlaceholders);
    }

    /// <summary>
    /// 驗證保留未知 placeholder 時會保留原 token 並寫入 report。
    /// </summary>
    [Fact]
    public void Bind_UnknownPlaceholderPolicyPreserve_ReportsUnresolvedToken()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Hello {{Missing}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>(),
            new OdfTemplateBindOptions
            {
                UnknownPlaceholderPolicy = OdfTemplateUnknownPlaceholderPolicy.Keep
            });

        Assert.Contains("{{Missing}}", document.BodyTextRoot.TextContent);
        Assert.Contains("Missing", report.UnresolvedPlaceholders);
        Assert.Contains(report.UnresolvedPlaceholderDetails, detail => detail.Expression == "Missing");
    }


    /// <summary>
    /// 驗證 ODT 圖片占位符會替換成圖片 frame 並寫入封裝媒體。
    /// </summary>
    [Fact]
    public void Bind_ImagePlaceholderInTextDocument_InsertsImageFrame()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("{{Image:Logo}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["Logo"] = new OdfTemplateImageValue(CreatePngBytes(), "logo.png", AltText: "Logo")
            },
            new OdfTemplateBindOptions());

        Assert.Equal(1, report.ImageReplacementCount);
        Assert.Contains("Pictures/", string.Join("|", document.Package.Manifest.Keys));
        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
    }

    /// <summary>
    /// 驗證 ODP 文字方塊圖片占位符會以圖片取代同一 frame 內容。
    /// </summary>
    [Fact]
    public void Bind_ImagePlaceholderInPresentationTextBox_ReplacesFrameContent()
    {
        using PresentationDocument document = PresentationDocument.Create();
        document.AddSlide("Intro").AddTextBox(1.Cm(), 1.Cm(), 4.Cm(), 3.Cm(), "{{Image:Hero}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["Hero"] = new OdfTemplateImageValue(CreatePngBytes(), "hero.png", Width: 4.Cm(), Height: 3.Cm())
            },
            new OdfTemplateBindOptions());

        Assert.Equal(1, report.ImageReplacementCount);
        Assert.Contains(document.GetPresentationNode().Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Empty(document.GetTextBoxes());
    }

    /// <summary>
    /// 驗證 ODG 圖片占位符會以圖片取代文字方塊內容。
    /// </summary>
    [Fact]
    public void Bind_ImagePlaceholderInDrawingTextBox_ReplacesFrameContent()
    {
        using DrawingDocument document = DrawingDocument.Create();
        document.Pages.Add("Canvas").AddTextBox(1.Cm(), 1.Cm(), 4.Cm(), 3.Cm(), "{{Image:Diagram}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?>
            {
                ["Diagram"] = new OdfTemplateImageValue(CreatePngBytes(), "diagram.png", Width: 4.Cm(), Height: 3.Cm())
            },
            new OdfTemplateBindOptions());

        Assert.Equal(1, report.ImageReplacementCount);
        Assert.Contains(document.GetDrawingNode().Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Empty(document.Pages[0].TextBoxes);
    }

    /// <summary>
    /// 驗證圖片占位符缺值或型別錯誤時會回報精準診斷。
    /// </summary>
    [Fact]
    public void Bind_ImagePlaceholderDiagnostics_ReportMissingAndWrongType()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("{{Image:Missing}}");
        document.AddParagraph("{{Image:Title}}");

        OdfTemplateBindReport report = TemplateBinder.Bind(
            document,
            new Dictionary<string, object?> { ["Title"] = "not an image" },
            new OdfTemplateBindOptions { StrictMode = true });

        Assert.Equal(0, report.ImageReplacementCount);
        Assert.Contains("Image:Missing", report.UnresolvedPlaceholders);
        Assert.Contains("Image:Title", report.UnresolvedPlaceholders);
        Assert.Contains(report.Warnings, warning => warning.Contains("TPLIMG0002", System.StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("TPLIMG0003", System.StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("TPL0002", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// 驗證行內圖片占位符與 ODS 圖片占位符會被回報而不替換。
    /// </summary>
    [Fact]
    public void Bind_ImagePlaceholderUnsupportedShapes_AreReported()
    {
        using TextDocument text = TextDocument.Create();
        text.AddParagraph("Logo: {{Image:Logo}}");
        OdfTemplateBindReport textReport = TemplateBinder.Bind(
            text,
            new Dictionary<string, object?> { ["Logo"] = new OdfTemplateImageValue(CreatePngBytes(), "logo.png") },
            new OdfTemplateBindOptions());

        using SpreadsheetDocument sheetDocument = SpreadsheetDocument.Create();
        sheetDocument.Worksheets.Add("Data").Cells["A1"].CellValue = "{{Image:Logo}}";
        OdfTemplateBindReport sheetReport = TemplateBinder.Bind(
            sheetDocument,
            new Dictionary<string, object?> { ["Logo"] = new OdfTemplateImageValue(CreatePngBytes(), "logo.png") },
            new OdfTemplateBindOptions());

        Assert.Equal(0, textReport.ImageReplacementCount);
        Assert.Contains(textReport.Warnings, warning => warning.Contains("TPLIMG0004", System.StringComparison.Ordinal));
        Assert.Equal(0, sheetReport.ImageReplacementCount);
        Assert.Contains(sheetReport.Warnings, warning => warning.Contains("TPLIMG0005", System.StringComparison.Ordinal));
    }

    private static byte[] CreatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
