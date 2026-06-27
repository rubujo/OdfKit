using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 提供 ODF DOM 樹結構拓撲相容性檢查的驗證器。
/// </summary>
public static class OdfDocumentValidator
{
    /// <summary>
    /// 驗證指定的 OdfDocument 拓撲結構，並傳回診斷報告。
    /// </summary>
    /// <param name="document">要驗證的 ODF 文件</param>
    /// <returns>包含所有錯誤與警告的驗證結果報告</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="document"/> 為 null 時拋出</exception>
    public static OdfValidationReport Validate(OdfDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        var issues = new List<OdfValidationIssue>();

        // 遍歷所有節點，檢查拓撲關係
        foreach (var node in document.ContentDom.Descendants())
        {
            if (node.NodeType == OdfNodeType.Element)
            {
                ValidateNodeTopology(node, issues);
            }
        }

        return new OdfValidationReport(OdfVersion.Odf14, document.DocumentKind, issues);
    }

    private static void ValidateNodeTopology(OdfNode node, List<OdfValidationIssue> issues)
    {
        string path = GetXmlPath(node);

        // 規則 1：table-row 必須直接或間接在 table (或 table-header-rows 等) 內
        if (string.Equals(node.LocalName, "table-row", StringComparison.Ordinal) &&
            string.Equals(node.NamespaceUri, OdfNamespaces.Table, StringComparison.Ordinal))
        {
            if (!HasAncestor(node, "table", OdfNamespaces.Table))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "Rule_Topology_OrphanRow",
                    OdfLocalizer.GetMessage("Rule_Topology_OrphanRow_Msg", node.LocalName, path),
                    packagePath: "content.xml",
                    xPath: path));
            }
        }

        // 規則 2：table-cell 必須直接在 table-row 內
        if ((string.Equals(node.LocalName, "table-cell", StringComparison.Ordinal) ||
             string.Equals(node.LocalName, "covered-table-cell", StringComparison.Ordinal)) &&
            string.Equals(node.NamespaceUri, OdfNamespaces.Table, StringComparison.Ordinal))
        {
            if (node.Parent == null ||
                !string.Equals(node.Parent.LocalName, "table-row", StringComparison.Ordinal) ||
                !string.Equals(node.Parent.NamespaceUri, OdfNamespaces.Table, StringComparison.Ordinal))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "Rule_Topology_OrphanCell",
                    OdfLocalizer.GetMessage("Rule_Topology_OrphanCell_Msg", node.LocalName, path),
                    packagePath: "content.xml",
                    xPath: path));
            }
        }
    }

    private static bool HasAncestor(OdfNode node, string localName, string nsUri)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (string.Equals(parent.LocalName, localName, StringComparison.Ordinal) &&
                string.Equals(parent.NamespaceUri, nsUri, StringComparison.Ordinal))
            {
                return true;
            }
            parent = parent.Parent;
        }
        return false;
    }

    private static string GetXmlPath(OdfNode node)
    {
        var parts = new List<string>();
        var current = node;
        while (current != null)
        {
            string prefixPart = string.IsNullOrEmpty(current.Prefix) ? string.Empty : (current.Prefix + ":");
            parts.Insert(0, $"{prefixPart}{current.LocalName}");
            current = current.Parent;
        }
        return "/" + string.Join("/", parts);
    }
}
