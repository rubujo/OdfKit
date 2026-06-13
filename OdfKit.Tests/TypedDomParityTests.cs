using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 typed DOM 與 ODFDOM 對標線的基本覆蓋能力。
/// </summary>
public class TypedDomParityTests
{
    /// <summary>
    /// 驗證 factory 會建立 generated 與手寫 typed wrapper，未知元素則回退為通用元素。
    /// </summary>
    [Fact]
    public void NodeFactoryCreatesGeneratedHandWrittenAndFallbackElements()
    {
        OdfNode generated = OdfNodeFactory.CreateElement(
            "animate",
            "urn:oasis:names:tc:opendocument:xmlns:animation:1.0",
            "anim");
        OdfNode handWritten = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        OdfNode fallback = OdfNodeFactory.CreateElement("custom-node", "urn:example:custom", "x");

        Assert.IsType<AnimationAnimateElement>(generated);
        Assert.IsType<TextPElement>(handWritten);
        Assert.IsType<OdfElement>(fallback);
        Assert.Equal("animate", generated.LocalName);
        Assert.Equal("p", handWritten.LocalName);
        Assert.Equal("custom-node", fallback.LocalName);
    }

    /// <summary>
    /// 驗證 generated DOM wrapper 的 class、factory case 與屬性數量沒有意外退化。
    /// </summary>
    [Fact]
    public void GeneratedDomWrapperCoverageDoesNotRegressBelowParityFloor()
    {
        string repoRoot = FindRepositoryRoot();
        string generatedPath = Path.Combine(repoRoot, "OdfKit", "DOM", "Generated", "GeneratedDomWrappers.g.cs");
        string generated = File.ReadAllText(generatedPath);

        int classCount = Regex.Matches(generated, @"public partial class \w+Element").Count;
        int factoryCaseCount = Regex.Matches(generated, "case \".*\": return new .*Element\\(prefix\\);").Count;
        int propertyCount = Regex.Matches(generated, @"public string\? \w+").Count;

        Assert.True(classCount >= 550, "generated typed element class count regressed: " + classCount);
        Assert.True(factoryCaseCount >= 590, "generated factory case count regressed: " + factoryCaseCount);
        Assert.True(propertyCount >= 100000, "generated attribute property count regressed: " + propertyCount);
    }

    /// <summary>
    /// 驗證 typed DOM coverage report 可輸出 schema-to-wrapper 對照。
    /// </summary>
    [Fact]
    public void TypedDomCoverageReportDeclaresSchemaWrapperMappings()
    {
        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();

        Assert.True(report.SchemaElementCount >= 550, "schema element count too low: " + report.SchemaElementCount);
        Assert.True(report.TypedElementCount >= 550, "typed element count too low: " + report.TypedElementCount);
        Assert.True(report.SchemaAttributeCount >= 100, "schema attribute count too low: " + report.SchemaAttributeCount);
        Assert.Contains(
            report.Elements,
            element => element.NamespaceUri == OdfNamespaces.Text &&
                element.LocalName == "p" &&
                element.HasTypedWrapper &&
                element.WrapperType.Contains("TextPElement", StringComparison.Ordinal));
        Assert.Contains(report.AttributeValueTypeCounts, pair => pair.Key.Length > 0 && pair.Value > 0);

        string json = JsonSerializer.Serialize(report.ToJsonModel());
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(report.SchemaElementCount, document.RootElement.GetProperty("summary").GetProperty("schemaElementCount").GetInt32());
        Assert.True(document.RootElement.GetProperty("elements").GetArrayLength() >= report.SchemaElementCount);
    }

    /// <summary>
    /// 驗證 typed DOM coverage 文件列出 ODFDOM 對標缺口與 coverage guard。
    /// </summary>
    [Fact]
    public void TypedDomCoverageDocumentDeclaresParityGaps()
    {
        string repoRoot = FindRepositoryRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "typed-dom-coverage.md"));

        Assert.Contains("ODFDOM", document, StringComparison.Ordinal);
        Assert.Contains("Coverage guard", document, StringComparison.Ordinal);
        Assert.Contains("Generated typed element classes", document, StringComparison.Ordinal);
        Assert.Contains("schema-to-wrapper coverage report", document, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }
}
