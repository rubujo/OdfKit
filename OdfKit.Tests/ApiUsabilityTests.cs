using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定高階 API 的使用者故事形狀。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class ApiUsabilityTests
{
    /// <summary>
    /// 驗證 API 人體工學清單中的每個工作流都有可追溯證據。
    /// </summary>
    [Fact]
    public void ApiUsabilityManifestHasTraceableWorkflowEvidence()
    {
        string root = FindRepositoryRoot();
        string manifestPath = Path.Combine(root, "docs", "api-usability.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        JsonElement rootElement = manifest.RootElement;
        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(3, rootElement.GetProperty("statementBudget").GetInt32());

        JsonElement[] workflows = rootElement.GetProperty("workflows").EnumerateArray().ToArray();
        Assert.True(workflows.Length >= 16);
        Assert.Equal(workflows.Length, workflows.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(workflows, item => item.GetProperty("format").GetString() == "ODT");
        Assert.Contains(workflows, item => item.GetProperty("format").GetString() == "ODS");
        Assert.Contains(workflows, item => item.GetProperty("format").GetString() == "ODP");
        Assert.Contains(workflows, item => item.GetProperty("format").GetString() == "ODG");

        foreach (JsonElement workflow in workflows)
        {
            Assert.False(string.IsNullOrWhiteSpace(workflow.GetProperty("limitations").GetString()));
            AssertPathsExist(root, workflow, "implementation");
            AssertPathsExist(root, workflow, "tests");
            AssertPathsExist(root, workflow, "samples");
        }
    }

    private static void AssertPathsExist(string root, JsonElement workflow, string propertyName)
    {
        JsonElement[] paths = workflow.GetProperty(propertyName).EnumerateArray().ToArray();
        Assert.NotEmpty(paths);
        foreach (JsonElement path in paths)
        {
            string relativePath = Assert.IsType<string>(path.GetString());
            Assert.True(File.Exists(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))),
                $"{workflow.GetProperty("id").GetString()} references missing {propertyName} path '{relativePath}'.");
        }
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "OdfKit.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the OdfKit repository root.");
    }

    /// <summary>
    /// 驗證使用少量程式碼即可建立含兩個段落的 ODT。
    /// </summary>
    [Fact]
    public void CreateOdtWithTwoParagraphsInFewLines()
    {
        using var document = (TextDocument)OdfDocument.Create(OdfDocumentKind.Text);
        document.AddParagraph("第一段");
        document.AddParagraph("第二段");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("第一段", xml);
        Assert.Contains("第二段", xml);
        Assert.Equal("application/vnd.oasis.opendocument.text", package.MimeType);
    }

    /// <summary>
    /// 驗證使用少量程式碼即可建立含儲存格與公式的 ODS。
    /// </summary>
    [Fact]
    public void CreateOdsWithCellAndFormulaInFewLines()
    {
        using var workbook = (SpreadsheetDocument)OdfDocument.Create(OdfDocumentKind.Spreadsheet);
        OdfTableSheet sheet = workbook.AddSheet("Sheet1");
        sheet.GetCell("A1").SetValue(40d);
        sheet.GetCell("B1").SetValue(2d);
        sheet.GetCell("C1").Formula = "of:=[.A1]+[.B1]";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("Sheet1", xml);
        Assert.Contains("of:=[.A1]+[.B1]", xml);
        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", package.MimeType);
    }

    /// <summary>
    /// 驗證可用任意 ODF 高階入口載入並保存為相同格式。
    /// </summary>
    [Fact]
    public void LoadAnyOdfAndSaveAsSameFormat()
    {
        using var source = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(source, OdfDocumentKind.Presentation, leaveOpen: true))
        {
            package.Save();
        }

        source.Position = 0;
        using OdfDocument document = OdfDocument.Load(source, "slides.odp");

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
        Assert.Equal("application/vnd.oasis.opendocument.presentation", reopened.MimeType);
        Assert.True(reopened.HasEntry("content.xml"));
    }

    /// <summary>
    /// 驗證可用少量程式碼驗證任意 ODF 文件。
    /// </summary>
    [Fact]
    public void ValidateAnyOdfInFewLines()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        OdfValidationReport report = OdfValidator.Validate(
            stream,
            "document.odt",
            OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }
}
