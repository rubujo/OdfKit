using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

public partial class ComplianceTests
{
    /// <summary>
    /// 迴歸測試：<c>draw:image</c> 的內容模型為
    /// <c>Choice(common-draw-data-attlist | office-binary-data)</c> 後接 <c>draw-text</c>。
    /// 當圖片以 <c>xlink:href</c> 外部參照（而非內嵌 <c>office:binary-data</c>）表示時，
    /// 屬性剝離邏輯曾誤將整個 choice 當作純屬性節點移除其中一個分支，使僅存的元素內容分支
    /// （office-binary-data）變成強制要求，導致任何使用外部參照的合法圖片都被誤判為不合規。
    /// </summary>
    [Fact]
    public void SchemaPatternValidator_DrawImageWithExternalHref_IsValid()
    {
        XNamespace draw = OdfNamespaces.Draw;
        XNamespace xlink = OdfNamespaces.XLink;

        var validImage = new XElement(draw + "image",
            new XAttribute(xlink + "href", "Pictures/sample.png"),
            new XAttribute(xlink + "type", "simple"),
            new XAttribute(xlink + "show", "embed"),
            new XAttribute(xlink + "actuate", "onLoad"));

        OdfSchemaSet schema = OdfSchemaRegistry.Odf14;
        OdfSchemaPatternValidationResult result =
            OdfSchemaPatternValidator.ValidateElement(validImage, schema, "draw-image");

        Assert.True(result.IsMatch);
    }

    /// <summary>
    /// 迴歸測試的反例：確保上述修正沒有讓驗證變得過度寬鬆——缺少必填 <c>xlink:href</c>
    /// 且沒有內嵌 <c>office:binary-data</c> 的 <c>draw:image</c> 仍必須判定為不合規。
    /// </summary>
    [Fact]
    public void SchemaPatternValidator_DrawImageMissingHrefAndBinaryData_IsInvalid()
    {
        XNamespace draw = OdfNamespaces.Draw;
        XNamespace xlink = OdfNamespaces.XLink;

        var brokenImage = new XElement(draw + "image",
            new XAttribute(xlink + "type", "simple"));

        OdfSchemaSet schema = OdfSchemaRegistry.Odf14;
        OdfSchemaPatternValidationResult result =
            OdfSchemaPatternValidator.ValidateElement(brokenImage, schema, "draw-image");

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void LocalizerResolvesCorrectTranslationsForRegisteredLanguages()
    {
        var enFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("en"));
        Assert.Contains("alternative text", enFix);

        var zhFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("zh-TW"));
        Assert.Contains("為圖片加入 svg:title/svg:desc", zhFix);

        var deFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("de"));
        Assert.Contains("Alternativtext", deFix);
    }

    [Fact]
    public void LocalizerCorrectlyFallsBackToParentCulture()
    {
        var atFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("de-AT"));
        Assert.Contains("Alternativtext", atFix);

        var ptBrFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("pt-BR"));
        Assert.Contains("texto alternativo", ptBrFix);
    }

    [Fact]
    public void LocalizerCorrectlyFallsBackToDefaultEnglishForUnregisteredCulture()
    {
        var jaFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("ja-JP"));
        Assert.Contains("alternative text", jaFix);
    }

    [Fact]
    public void BuiltInComplianceSuggestedFixesResolveForAllSupportedCultures()
    {
        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko"];
        var ruleIds = new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
        foreach (OdfComplianceProfile profile in OdfComplianceProfiles.BuiltIn)
        {
            foreach (var rule in profile.Rules)
            {
                ruleIds.Add(rule.Id);
            }
        }

        foreach (string cultureName in cultures)
        {
            var culture = new CultureInfo(cultureName);
            foreach (string ruleId in ruleIds)
            {
                string fix = OdfLocalizer.GetSuggestedFix(ruleId, culture);

                Assert.NotEmpty(fix);
                Assert.NotEqual(ruleId, fix);
            }
        }
    }

    [Fact]
    public void ValidatorAutoDetectsLanguageBasedOnProfileTargetCulture()
    {
        // 使用含有事件監聽器（巨集）的內容，以觸發 DisallowMacroByDefault 規則。
        string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" office:version=\"1.2\">" +
                         "<office:scripts><office:event-listeners><script:event-listener script:event-name=\"dom-click\" script:language=\"ooo:script\" script:macro-name=\"MyMacro\" /></office:event-listeners></office:scripts>" +
                         "<office:body><office:text/></office:body></office:document-content>";
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(content));

        OdfValidationReport reportDe = OdfFlatDocumentValidator.Validate(
            ms,
            "document.fodt",
            OdfComplianceProfiles.DeGovernmentOdf);

        // 德國 Profile 自動偵測德文，應包含德文 SuggestedFix。
        Assert.Contains(reportDe.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && (issue.SuggestedFix.Contains("Entfernen") || issue.SuggestedFix.Contains("Makros")));

        ms.Position = 0;
        OdfValidationReport reportTw = OdfFlatDocumentValidator.Validate(
            ms,
            "document.fodt",
            OdfComplianceProfiles.RocTaiwanOdfCns15251);

        // 臺灣 CNS 15251 Profile 自動偵測繁中，應包含繁中 SuggestedFix。
        Assert.Contains(reportTw.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && (issue.SuggestedFix.Contains("移除") || issue.SuggestedFix.Contains("巨集")));
    }
}
