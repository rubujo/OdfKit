using System;
using System.Globalization;
using System.Text;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// 從 ODF DOM 擷取邏輯純文字。
/// </summary>
internal static class OdfDocumentTextExtractionEngine
{
    internal static string Extract(OdfNode root, OdfTextExtractionOptions options)
    {
        var builder = new StringBuilder();
        AppendNode(FindDocumentBody(root) ?? root, options, builder);
        TrimTrailingSeparators(builder, options.BlockSeparator);
        return builder.ToString();
    }

    private static OdfNode? FindDocumentBody(OdfNode node)
    {
        if (node.NodeType == OdfNodeType.Element &&
            node.NamespaceUri == OdfNamespaces.Office &&
            node.LocalName == "body")
        {
            return node;
        }

        foreach (OdfNode child in node.Children)
        {
            OdfNode? body = FindDocumentBody(child);
            if (body is not null)
            {
                return body;
            }
        }

        return null;
    }

    private static void AppendNode(OdfNode node, OdfTextExtractionOptions options, StringBuilder builder)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            builder.Append(node.TextContent);
            return;
        }

        if (node.NodeType != OdfNodeType.Element || ShouldSkip(node, options))
        {
            return;
        }

        if (node.NamespaceUri == OdfNamespaces.Text)
        {
            switch (node.LocalName)
            {
                case "s":
                    AppendRepeated(builder, ' ', ParsePositiveCount(node.GetAttribute("c", OdfNamespaces.Text)));
                    return;
                case "tab":
                    builder.Append('\t');
                    return;
                case "line-break":
                case "soft-page-break":
                    AppendSeparator(builder, options.BlockSeparator);
                    return;
            }
        }

        foreach (OdfNode child in node.Children)
        {
            AppendNode(child, options, builder);
        }

        if (IsBlock(node) && !IsLastParagraphInTableCell(node))
        {
            AppendSeparator(builder, options.BlockSeparator);
        }
        else if (IsTableCell(node) && !IsLastTableCellInRow(node))
        {
            builder.Append('\t');
        }
    }

    private static bool ShouldSkip(OdfNode node, OdfTextExtractionOptions options)
    {
        if (node.NamespaceUri == OdfNamespaces.Office &&
            !options.IncludeAnnotations &&
            node.LocalName is "annotation" or "annotation-end")
        {
            return true;
        }

        if (node.NamespaceUri == OdfNamespaces.Text)
        {
            if (!options.IncludeTrackedChanges && node.LocalName == "tracked-changes")
            {
                return true;
            }
        }

        return !options.IncludePresentationNotes &&
            node.NamespaceUri == OdfNamespaces.Presentation &&
            node.LocalName == "notes";
    }

    private static bool IsBlock(OdfNode node)
    {
        if (node.NamespaceUri == OdfNamespaces.Text && node.LocalName is "p" or "h" or "list-item" or "section")
        {
            return true;
        }

        if (node.NamespaceUri == OdfNamespaces.Table && node.LocalName is "table-row" or "table")
        {
            return true;
        }

        return node.NamespaceUri == OdfNamespaces.Draw && node.LocalName == "page";
    }

    private static bool IsTableCell(OdfNode node) =>
        node.NamespaceUri == OdfNamespaces.Table &&
        node.LocalName is "table-cell" or "covered-table-cell";

    private static bool IsLastTableCellInRow(OdfNode node)
    {
        if (node.Parent is null)
        {
            return true;
        }

        for (int index = node.SiblingIndex + 1; index < node.Parent.Children.Count; index++)
        {
            if (IsTableCell(node.Parent.Children[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLastParagraphInTableCell(OdfNode node)
    {
        if (node.NamespaceUri != OdfNamespaces.Text ||
            node.LocalName is not ("p" or "h") ||
            node.Parent is null ||
            !IsTableCell(node.Parent))
        {
            return false;
        }

        for (int index = node.SiblingIndex + 1; index < node.Parent.Children.Count; index++)
        {
            OdfNode sibling = node.Parent.Children[index];
            if (sibling.NodeType == OdfNodeType.Element &&
                sibling.NamespaceUri == OdfNamespaces.Text &&
                sibling.LocalName is "p" or "h")
            {
                return false;
            }
        }

        return true;
    }

    private static int ParsePositiveCount(string? value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0
            ? count
            : 1;
    }

    private static void AppendRepeated(StringBuilder builder, char value, int count)
    {
        for (int index = 0; index < count; index++)
        {
            builder.Append(value);
        }
    }

    private static void AppendSeparator(StringBuilder builder, string separator)
    {
        if (string.IsNullOrEmpty(separator) || builder.Length == 0 || EndsWith(builder, separator))
        {
            return;
        }

        builder.Append(separator);
    }

    private static void TrimTrailingSeparators(StringBuilder builder, string separator)
    {
        while (!string.IsNullOrEmpty(separator) && EndsWith(builder, separator))
        {
            builder.Length -= separator.Length;
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == '\t')
        {
            builder.Length--;
        }
    }

    private static bool EndsWith(StringBuilder builder, string value)
    {
        if (value.Length == 0 || builder.Length < value.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (builder[builder.Length - value.Length + index] != value[index])
            {
                return false;
            }
        }

        return true;
    }
}
