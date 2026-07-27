using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Image;

namespace OdfKit.Compliance;

/// <summary>
/// Provides practical interoperability checks for common ODF editing workflows.
/// 提供常見 ODF 編輯工作流程的實務互通性檢查。
/// </summary>
public static class OdfPracticalCompatibilityValidator
{
    /// <summary>
    /// Validates practical interoperability risks for an ODF document.
    /// 驗證 ODF 文件的實務互通性風險。
    /// </summary>
    /// <param name="document">The document to validate. / 要驗證的文件。</param>
    /// <param name="profile">The practical compatibility profile. / 實務相容性設定檔。</param>
    /// <returns>The practical compatibility report. / 實務相容性報告。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>. / 當 <paramref name="document"/> 為 <see langword="null"/> 時擲出。</exception>
    public static OdfPracticalCompatibilityReport Validate(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile)
    {
        return Validate(document, profile, null);
    }

    /// <summary>
    /// Validates practical interoperability risks for an ODF document with post-processing options.
    /// 使用後處理選項驗證 ODF 文件的實務互通性風險。
    /// </summary>
    /// <param name="document">The document to validate. / 要驗證的文件。</param>
    /// <param name="profile">The practical compatibility profile. / 實務相容性設定檔。</param>
    /// <param name="options">The post-processing options. / 後處理選項。</param>
    /// <returns>The practical compatibility report. / 實務相容性報告。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>. / 當 <paramref name="document"/> 為 <see langword="null"/> 時擲出。</exception>
    public static OdfPracticalCompatibilityReport Validate(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile,
        OdfPracticalCompatibilityOptions? options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));

        List<OdfPracticalCompatibilityIssue> issues = [];
        ScanPackage(document, profile, issues);
        ScanContent(document, profile, issues);
        ScanEmbeddedChartContent(document, profile, issues);
        ScanImageInspection(document, profile, issues);
        issues = ApplyOptions(issues, options);
        return new OdfPracticalCompatibilityReport(profile, document.DocumentKind, issues);
    }

    private static List<OdfPracticalCompatibilityIssue> ApplyOptions(
        List<OdfPracticalCompatibilityIssue> issues,
        OdfPracticalCompatibilityOptions? options)
    {
        if (options is null)
        {
            return issues;
        }

        IEnumerable<OdfPracticalCompatibilityIssue> filtered = issues
            .Where(issue => !options.DisabledRuleIds.Contains(issue.RuleId))
            .Select(issue => options.SeverityOverrides.TryGetValue(issue.RuleId, out OdfIssueSeverity severity)
                ? issue with { Severity = severity }
                : issue);

        if (options.MaximumIssueCount.HasValue)
        {
            filtered = filtered.Take(Math.Max(0, options.MaximumIssueCount.Value));
        }

        return filtered.ToList();
    }

    private static void ScanImageInspection(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile,
        List<OdfPracticalCompatibilityIssue> issues)
    {
        if (document is not OdfImageDocument imageDocument)
        {
            return;
        }

        OdfImageInspectionReport report = imageDocument.InspectImages(null, profile);
        foreach (OdfImageInspectionIssue issue in report.Issues)
        {
            issues.Add(new OdfPracticalCompatibilityIssue(
                issue.Severity,
                issue.RuleId,
                issue.MessageKey ?? issue.RuleId,
                issue.Message,
                issue.Suggestion,
                document.DocumentKind,
                issue.ImageHref,
                new Dictionary<string, string?>
                {
                    ["frameName"] = issue.FrameName,
                    ["profile"] = profile.ToString()
                }));
        }
    }

    private static void ScanPackage(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile,
        List<OdfPracticalCompatibilityIssue> issues)
    {
        foreach (KeyValuePair<string, string> entry in document.Package.Manifest)
        {
            string path = entry.Key;
            string mediaType = entry.Value;
            if (IsScriptPath(path) || global::OdfKit.Internal.OdfStringHelper.Contains(mediaType, "script", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(document, issues, "PRAC0001", "Msg_Practical_MacroOrScript", path);
            }

            if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !IsPortableImageMediaType(mediaType))
            {
                AddIssue(document, issues, "PRAC0002", "Msg_Practical_NonPortableImage", path);
            }
        }
    }

    private static void ScanContent(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile,
        List<OdfPracticalCompatibilityIssue> issues)
    {
        int automaticStyles = 0;
        int textBoxDepth = 0;
        int groupDepth = 0;
        bool hasAdvancedChart = false;
        bool hasHeaderFooter = false;
        bool hasSpreadsheetSizing = false;
        bool hasSpreadsheetPrintSettings = false;
        bool hasImageTransform = false;
        bool hasTextIndex = false;
        bool hasTextSection = false;
        bool hasEmbeddedObject = false;

        var stack = new Stack<(OdfNode Node, int TextBoxDepth, int GroupDepth)>();
        stack.Push((document.ContentRoot, 0, 0));
        while (stack.Count > 0)
        {
            (OdfNode node, int currentTextBoxDepth, int currentGroupDepth) = stack.Pop();
            int nextTextBoxDepth = currentTextBoxDepth;
            int nextGroupDepth = currentGroupDepth;

            if (node.NodeType is OdfNodeType.Element)
            {
                if (node.LocalName == "text-box" && node.NamespaceUri == OdfNamespaces.Draw)
                {
                    nextTextBoxDepth++;
                    textBoxDepth = Math.Max(textBoxDepth, nextTextBoxDepth);
                }

                if (node.LocalName == "g" && node.NamespaceUri == OdfNamespaces.Draw)
                {
                    nextGroupDepth++;
                    groupDepth = Math.Max(groupDepth, nextGroupDepth);
                }

                if (node.LocalName == "style" && node.NamespaceUri == OdfNamespaces.Style)
                {
                    automaticStyles++;
                }

                if (IsAdvancedChartNode(node))
                {
                    hasAdvancedChart = true;
                }

                if (IsSpreadsheetSizingNode(node))
                {
                    hasSpreadsheetSizing = true;
                }

                if (IsSpreadsheetPrintSettingsNode(node))
                {
                    hasSpreadsheetPrintSettings = true;
                }

                if (IsImageTransformNode(node))
                {
                    hasImageTransform = true;
                }

                if (IsTextIndexNode(node))
                {
                    hasTextIndex = true;
                }

                if (node.LocalName == "section" && node.NamespaceUri == OdfNamespaces.Text)
                {
                    hasTextSection = true;
                }

                if (node.LocalName == "object" && node.NamespaceUri == OdfNamespaces.Draw)
                {
                    hasEmbeddedObject = true;
                }

                if (node.NamespaceUri == OdfNamespaces.Style &&
                    (global::OdfKit.Internal.OdfStringHelper.Contains(node.LocalName, "header", StringComparison.Ordinal) ||
                     global::OdfKit.Internal.OdfStringHelper.Contains(node.LocalName, "footer", StringComparison.Ordinal)))
                {
                    hasHeaderFooter = true;
                }
            }

            foreach (OdfNode child in node.Children)
            {
                stack.Push((child, nextTextBoxDepth, nextGroupDepth));
            }
        }

        if (textBoxDepth > 1)
        {
            AddIssue(document, issues, "PRAC0100", "Msg_Practical_NestedTextBox", "content.xml");
        }

        if (groupDepth > 1)
        {
            AddIssue(document, issues, "PRAC0101", "Msg_Practical_ComplexGraphicGroup", "content.xml");
        }

        if (automaticStyles > 200)
        {
            AddIssue(document, issues, "PRAC0102", "Msg_Practical_StyleFragmentation", "content.xml",
                new Dictionary<string, string?> { ["styleCount"] = automaticStyles.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        if (hasAdvancedChart && profile is not OdfPracticalCompatibilityProfile.LibreOfficeCurrent)
        {
            AddIssue(document, issues, "PRAC0200", "Msg_Practical_AdvancedChart", "content.xml");
        }

        if (hasHeaderFooter && profile is OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf)
        {
            AddIssue(document, issues, "PRAC0300", "Msg_Practical_HeaderFooterLayout", "content.xml");
        }

        if (IsTextKind(document.DocumentKind) &&
            profile is OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf &&
            (hasEmbeddedObject || (hasTextIndex && hasTextSection)))
        {
            AddIssue(
                document,
                issues,
                "PRAC0301",
                "Msg_Practical_WordOdtRepairRisk",
                "content.xml",
                new Dictionary<string, string?>
                {
                    ["hasTextIndex"] = hasTextIndex.ToString(),
                    ["hasTextSection"] = hasTextSection.ToString(),
                    ["hasEmbeddedObject"] = hasEmbeddedObject.ToString()
                });
        }

        if (hasSpreadsheetSizing &&
            IsSpreadsheetKind(document.DocumentKind) &&
            profile is not OdfPracticalCompatibilityProfile.LibreOfficeCurrent)
        {
            AddIssue(document, issues, "PRAC0400", "Msg_Practical_SpreadsheetSizing", "content.xml");
        }

        if (hasSpreadsheetPrintSettings &&
            IsSpreadsheetKind(document.DocumentKind) &&
            profile is not OdfPracticalCompatibilityProfile.LibreOfficeCurrent)
        {
            AddIssue(document, issues, "PRAC0401", "Msg_Practical_SpreadsheetPrintSettings", "content.xml");
        }

        if (hasImageTransform &&
            (IsPresentationOrGraphicsKind(document.DocumentKind) || IsImageKind(document.DocumentKind)) &&
            profile is not OdfPracticalCompatibilityProfile.LibreOfficeCurrent)
        {
            AddIssue(document, issues, "PRAC0500", "Msg_Practical_ImageTransform", "content.xml");
        }
    }

    private static void ScanEmbeddedChartContent(
        OdfDocument document,
        OdfPracticalCompatibilityProfile profile,
        List<OdfPracticalCompatibilityIssue> issues)
    {
        if (profile is OdfPracticalCompatibilityProfile.LibreOfficeCurrent)
        {
            return;
        }

        foreach (KeyValuePair<string, string> entry in document.Package.Manifest)
        {
            string path = entry.Key;
            if (!path.StartsWith("Object ", StringComparison.Ordinal) ||
                !path.EndsWith("/content.xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var stream = document.Package.GetEntryStream(path);
                OdfNode root = OdfXmlReader.Parse(stream, document.Package.LoadOptions);
                if (ContainsAdvancedChartNode(root))
                {
                    AddIssue(document, issues, "PRAC0200", "Msg_Practical_AdvancedChart", path);
                }
            }
            catch
            {
                // 實務相容性檢查不取代封裝驗證；無法解析的嵌入物件交由 schema/package validator 回報。
            }
        }
    }

    private static bool ContainsAdvancedChartNode(OdfNode root)
    {
        var stack = new Stack<OdfNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            OdfNode node = stack.Pop();
            if (IsAdvancedChartNode(node))
            {
                return true;
            }

            foreach (OdfNode child in node.Children)
            {
                stack.Push(child);
            }
        }

        return false;
    }

    private static bool IsAdvancedChartNode(OdfNode node)
    {
        if (node.LocalName == "chart" && node.NamespaceUri == OdfNamespaces.Chart)
        {
            string? chartClass = node.GetAttribute("class", OdfNamespaces.Chart);
            if (chartClass is "chart:bubble" or "bubble" or "chart:stock" or "stock")
            {
                return true;
            }
        }

        if (node.NamespaceUri == OdfNamespaces.Chart &&
            node.LocalName is "wall" or "floor" or "stock-gain-marker" or "stock-loss-marker" or "stock-range-line")
        {
            return true;
        }

        if (node.NamespaceUri == OdfNamespaces.Dr3d && node.LocalName == "light")
        {
            return true;
        }

        if (node.NodeType is OdfNodeType.Element &&
            string.Equals(node.GetAttribute("three-dimensional", OdfNamespaces.Chart), "true", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsSpreadsheetSizingNode(OdfNode node) =>
        node.NamespaceUri == OdfNamespaces.Style &&
        (node.GetAttribute("column-width", OdfNamespaces.Style) is not null ||
         node.GetAttribute("row-height", OdfNamespaces.Style) is not null);

    private static bool IsSpreadsheetPrintSettingsNode(OdfNode node) =>
        (node.NamespaceUri == OdfNamespaces.Table && node.GetAttribute("print-ranges", OdfNamespaces.Table) is not null) ||
        (node.NamespaceUri == OdfNamespaces.Style && global::OdfKit.Internal.OdfStringHelper.Contains(node.LocalName, "print", StringComparison.OrdinalIgnoreCase));

    private static bool IsImageTransformNode(OdfNode node) =>
        (node.NamespaceUri == OdfNamespaces.Draw &&
         node.LocalName == "frame" &&
         node.GetAttribute("transform", OdfNamespaces.Draw) is not null) ||
        (node.NamespaceUri == OdfNamespaces.Draw &&
         node.LocalName == "image" &&
         node.GetAttribute("clip", OdfNamespaces.Fo) is not null);

    private static bool IsTextIndexNode(OdfNode node) =>
        node.NamespaceUri == OdfNamespaces.Text &&
        (node.LocalName == "table-of-content" ||
         node.LocalName == "illustration-index" ||
         node.LocalName == "table-index" ||
         node.LocalName == "object-index" ||
         node.LocalName == "user-index" ||
         node.LocalName == "alphabetical-index" ||
         node.LocalName == "bibliography");

    private static bool IsTextKind(OdfDocumentKind kind) =>
        kind is OdfDocumentKind.Text or OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or
            OdfDocumentKind.TextWeb or OdfDocumentKind.FlatText;

    private static bool IsSpreadsheetKind(OdfDocumentKind kind) =>
        kind is OdfDocumentKind.Spreadsheet or OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet;

    private static bool IsPresentationOrGraphicsKind(OdfDocumentKind kind) =>
        kind is OdfDocumentKind.Presentation or OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation or
            OdfDocumentKind.Graphics or OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics;

    private static bool IsImageKind(OdfDocumentKind kind) =>
        kind is OdfDocumentKind.Image or OdfDocumentKind.ImageTemplate or OdfDocumentKind.FlatImage;

    private static void AddIssue(
        OdfDocument document,
        List<OdfPracticalCompatibilityIssue> issues,
        string ruleId,
        string messageKey,
        string? packagePath,
        IReadOnlyDictionary<string, string?>? details = null)
    {
        issues.Add(new OdfPracticalCompatibilityIssue(
            OdfIssueSeverity.Warning,
            ruleId,
            messageKey,
            OdfLocalizer.GetMessage(messageKey),
            OdfLocalizer.GetSuggestedFix(ruleId),
            document.DocumentKind,
            packagePath,
            details));
    }

    private static bool IsScriptPath(string path) =>
        path.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("Basic/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".xba", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".xlb", StringComparison.OrdinalIgnoreCase);

    private static bool IsPortableImageMediaType(string mediaType) =>
        mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
        mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
        mediaType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ||
        mediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase);
}
