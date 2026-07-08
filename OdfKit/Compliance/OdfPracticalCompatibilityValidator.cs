using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

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
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<OdfPracticalCompatibilityIssue> issues = [];
        ScanPackage(document, profile, issues);
        ScanContent(document, profile, issues);
        return new OdfPracticalCompatibilityReport(profile, document.DocumentKind, issues);
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
            if (IsScriptPath(path) || mediaType.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0)
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

                if (node.NamespaceUri == OdfNamespaces.Style &&
                    (node.LocalName.IndexOf("header", StringComparison.Ordinal) >= 0 ||
                     node.LocalName.IndexOf("footer", StringComparison.Ordinal) >= 0))
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
