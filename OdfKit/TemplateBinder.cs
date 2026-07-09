using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit;

/// <summary>
/// Provides low-magic placeholder binding for common ODF document types.
/// 提供常見 ODF 文件類型的低魔法占位符繫結。
/// </summary>
public static partial class TemplateBinder
{
    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in a text document.
    /// 取代文字文件中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The text document. / 文字文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of replacement operations requested. / 要求執行的替換作業數量。</returns>
    public static int Bind(TextDocument document, IReadOnlyDictionary<string, object?> values) =>
        Bind(document, values, OdfTemplateBindOptions.Default).ReplacementCount;

    /// <summary>
    /// Replaces placeholders and expands collection paragraphs in a text document.
    /// 取代文字文件中的占位符並展開集合段落。
    /// </summary>
    /// <param name="document">The text document. / 文字文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <param name="options">The binding options. / 繫結選項。</param>
    /// <returns>The binding report. / 繫結報告。</returns>
    public static OdfTemplateBindReport Bind(
        TextDocument document,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        options ??= OdfTemplateBindOptions.Default;
        var report = new OdfTemplateBindReport();
        if (options.ExpandCollections)
        {
            ExpandCollectionParagraphs(document.BodyTextRoot, values, options, report);
        }

        if (options.EnableImagePlaceholders)
        {
            report.ChangedNodeCount += ReplaceImageParagraphs(document.Package, document.BodyTextRoot, values, options, report);
        }

        report.ChangedNodeCount += ReplaceBindableNodes(document.BodyTextRoot, values, options, report);
        return FinalizeReport(document.BodyTextRoot, "TextDocument", options, report);
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in used spreadsheet cells.
    /// 取代試算表已使用儲存格中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The spreadsheet document. / 試算表文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of cells changed. / 已變更的儲存格數量。</returns>
    public static int Bind(SpreadsheetDocument document, IReadOnlyDictionary<string, object?> values) =>
        Bind(document, values, OdfTemplateBindOptions.Default).ChangedNodeCount;

    /// <summary>
    /// Replaces placeholders and expands collection rows in a spreadsheet document.
    /// 取代試算表中的占位符並展開集合列。
    /// </summary>
    /// <param name="document">The spreadsheet document. / 試算表文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <param name="options">The binding options. / 繫結選項。</param>
    /// <returns>The binding report. / 繫結報告。</returns>
    public static OdfTemplateBindReport Bind(
        SpreadsheetDocument document,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        options ??= OdfTemplateBindOptions.Default;
        var report = new OdfTemplateBindReport();
        foreach (OdfTableSheet sheet in document.GetSheets())
        {
            if (options.ExpandCollections)
            {
                ExpandCollectionRows(sheet, values, options, report);
            }

            foreach (OdfCell cell in sheet.GetUsedCells())
            {
                string text = cell.DisplayText;
                ReportUnsupportedImagePlaceholder(text, "SpreadsheetDocument", cell.Node.LocalName, report);
                string replaced = ReplaceScalarTokens(text, values, options, report);
                if (!string.Equals(text, replaced, StringComparison.Ordinal))
                {
                    if (!options.DryRun)
                    {
                        cell.SetValue(replaced);
                    }

                    report.ChangedNodeCount++;
                }
            }
        }

        return FinalizeReport(document.SheetsRoot, "SpreadsheetDocument", options, report);
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in presentation text boxes.
    /// 取代簡報文字方塊中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The presentation document. / 簡報文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of text paragraphs changed. / 已變更的文字段落數量。</returns>
    public static int Bind(PresentationDocument document, IReadOnlyDictionary<string, object?> values) =>
        Bind(document, values, OdfTemplateBindOptions.Default).ChangedNodeCount;

    /// <summary>
    /// Replaces placeholders and expands collection paragraphs in presentation text boxes.
    /// 取代簡報文字方塊中的占位符並展開集合段落。
    /// </summary>
    /// <param name="document">The presentation document. / 簡報文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <param name="options">The binding options. / 繫結選項。</param>
    /// <returns>The binding report. / 繫結報告。</returns>
    public static OdfTemplateBindReport Bind(
        PresentationDocument document,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        options ??= OdfTemplateBindOptions.Default;
        var report = new OdfTemplateBindReport();
        foreach (OdfSlide slide in document.Slides)
        {
            foreach (OdfTextBox textBox in slide.TextBoxes)
            {
                if (options.ExpandCollections)
                {
                    ExpandCollectionParagraphs(textBox.Node, values, options, report);
                }

                if (options.EnableImagePlaceholders)
                {
                    report.ChangedNodeCount += ReplaceImageFrame(
                        document.Package,
                        textBox.Node,
                        values,
                        options,
                        report,
                        "PresentationDocument");
                }

                report.ChangedNodeCount += ReplaceTextParagraphs(textBox.Node, values, options, report);
            }
        }

        return FinalizeReport(document.GetPresentationNode(), "PresentationDocument", options, report);
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in drawing text boxes.
    /// 取代繪圖文件文字方塊中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The drawing document. / 繪圖文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of text paragraphs changed. / 已變更的文字段落數量。</returns>
    public static int Bind(DrawingDocument document, IReadOnlyDictionary<string, object?> values) =>
        Bind(document, values, OdfTemplateBindOptions.Default).ChangedNodeCount;

    /// <summary>
    /// Replaces placeholders and expands collection paragraphs in drawing text boxes.
    /// 取代繪圖文字方塊中的占位符並展開集合段落。
    /// </summary>
    /// <param name="document">The drawing document. / 繪圖文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <param name="options">The binding options. / 繫結選項。</param>
    /// <returns>The binding report. / 繫結報告。</returns>
    public static OdfTemplateBindReport Bind(
        DrawingDocument document,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        options ??= OdfTemplateBindOptions.Default;
        var report = new OdfTemplateBindReport();
        foreach (OdfDrawPage page in document.Pages)
        {
            foreach (OdfTextBox textBox in page.TextBoxes)
            {
                if (options.ExpandCollections)
                {
                    ExpandCollectionParagraphs(textBox.Node, values, options, report);
                }

                if (options.EnableImagePlaceholders)
                {
                    report.ChangedNodeCount += ReplaceImageFrame(
                        document.Package,
                        textBox.Node,
                        values,
                        options,
                        report,
                        "DrawingDocument");
                }

                report.ChangedNodeCount += ReplaceTextParagraphs(textBox.Node, values, options, report);
            }
        }

        return FinalizeReport(document.GetDrawingNode(), "DrawingDocument", options, report);
    }

    private static int ReplaceBindableNodes(
        OdfNode node,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        int changed = 0;
        OdfNode[] targets = new[] { node }
            .Concat(node.Descendants())
            .Where(IsBindableContentNode)
            .Where(target => !target.Descendants().Any(IsBindableContentNode))
            .ToArray();
        foreach (OdfNode target in targets)
        {
            string text = target.TextContent;
            IReadOnlyList<TextDocumentSearchReplaceEngine.TextReplacement> replacements =
                ResolveScalarTextReplacements(text, values, options, report);
            if (replacements.Count > 0)
            {
                if (!options.DryRun)
                {
                    TextDocumentSearchReplaceEngine.ReplaceTextRanges(target, replacements);
                }

                changed++;
            }
        }

        return changed;
    }

    private static int ReplaceTextParagraphs(
        OdfNode node,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        int changed = 0;
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                string text = child.TextContent;
                IReadOnlyList<TextDocumentSearchReplaceEngine.TextReplacement> replacements =
                    ResolveScalarTextReplacements(text, values, options, report);
                if (replacements.Count > 0)
                {
                    if (!options.DryRun)
                    {
                        TextDocumentSearchReplaceEngine.ReplaceTextRanges(child, replacements);
                    }

                    changed++;
                }
            }

            changed += ReplaceTextParagraphs(child, values, options, report);
        }

        return changed;
    }

    private static void ExpandCollectionRows(
        OdfTableSheet sheet,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        OdfNode[] templateRows = OdfTableSheetDomAccessEngine.GetRowsList(sheet.TableNode).ToArray();
        for (int rowIndex = templateRows.Length - 1; rowIndex >= 0; rowIndex--)
        {
            OdfNode row = templateRows[rowIndex];
            if (!TryGetCollectionName(row.TextContent, out string? collectionName, report) || collectionName is null)
            {
                continue;
            }

            if (!TryGetCollection(values, collectionName, out List<object?> items))
            {
                report.UnresolvedPlaceholders.Add(collectionName);
                continue;
            }

            if (!report.ExpandedCollections.Contains(collectionName))
            {
                report.ExpandedCollections.Add(collectionName);
            }

            OdfNode? parent = row.Parent;
            if (parent is null)
            {
                continue;
            }

            OdfNode template = row.CloneNode(deep: true);
            OdfNode anchor = row;
            int itemIndex = 0;
            bool structureChanged = false;
            foreach (object? item in items.Take(options.MaxCollectionItems))
            {
                OdfNode target = itemIndex == 0 ? row : template.CloneNode(deep: true);
                ReplaceCollectionTokens(target, collectionName, item, values, options, report);
                if (!options.DryRun)
                {
                    ShiftFormulaRows(target, itemIndex);
                    if (itemIndex > 0)
                    {
                        parent.InsertAfter(target, anchor);
                        anchor = target;
                        structureChanged = true;
                    }
                }

                report.ExpandedItemCount++;
                report.ChangedNodeCount++;
                itemIndex++;
            }

            if (itemIndex == 0 && !options.DryRun)
            {
                sheet.DeleteRows(rowIndex);
            }
            else if (structureChanged)
            {
                sheet.InvalidateAccessCache();
            }
        }
    }

    private static void ExpandCollectionParagraphs(
        OdfNode container,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        OdfNode[] paragraphs = container.Descendants()
            .Where(static node => node.NodeType is OdfNodeType.Element &&
                node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text)
            .ToArray();

        foreach (OdfNode paragraph in paragraphs)
        {
            if (!TryGetCollectionName(paragraph.TextContent, out string? collectionName, report) || collectionName is null)
            {
                continue;
            }

            if (!TryGetCollection(values, collectionName, out List<object?> items))
            {
                report.UnresolvedPlaceholders.Add(collectionName);
                continue;
            }

            if (!report.ExpandedCollections.Contains(collectionName))
            {
                report.ExpandedCollections.Add(collectionName);
            }

            OdfNode? parent = paragraph.Parent;
            if (parent is null)
            {
                continue;
            }

            OdfNode anchor = paragraph;
            foreach (object? item in items.Take(options.MaxCollectionItems))
            {
                OdfNode clone = paragraph.CloneNode(deep: true);
                ReplaceCollectionTokens(clone, collectionName, item, values, options, report);
                if (!options.DryRun)
                {
                    parent.InsertAfter(clone, anchor);
                    anchor = clone;
                }

                report.ExpandedItemCount++;
                report.ChangedNodeCount++;
            }

            if (!options.DryRun)
            {
                parent.RemoveChild(paragraph);
            }
        }
    }

    private static bool TryGetCollectionName(string text, out string? collectionName, OdfTemplateBindReport report)
    {
        collectionName = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        while (index < text.Length)
        {
            int start = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            string expression = text.Substring(start + 2, end - start - 2).Trim();
            int marker = expression.IndexOf("[].", StringComparison.Ordinal);
            if (marker > 0)
            {
                names.Add(expression.Substring(0, marker));
            }

            index = end + 2;
        }

        if (names.Count == 0)
        {
            return false;
        }

        if (names.Count > 1)
        {
            report.Warnings.Add("TPL0001: " + string.Join(", ", names));
            foreach (string name in names)
            {
                report.UnresolvedPlaceholders.Add(name);
            }

            return false;
        }

        collectionName = names.First();
        return true;
    }

    private static void ReplaceCollectionTokens(
        OdfNode node,
        string collectionName,
        object? item,
        IReadOnlyDictionary<string, object?> rootValues,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        IEnumerable<OdfNode> targets = node.Descendants()
            .Where(IsBindableContentNode);
        if (IsBindableContentNode(node))
        {
            targets = new[] { node }.Concat(targets);
        }

        foreach (OdfNode target in targets
            .Where(target => !target.Descendants().Any(IsBindableContentNode))
            .ToArray())
        {
            string text = target.TextContent;
            IReadOnlyList<TextDocumentSearchReplaceEngine.TextReplacement> replacements =
                ResolveCollectionTextReplacements(
                    text,
                    collectionName,
                    item,
                    rootValues,
                    options,
                    report);
            if (replacements.Count > 0 && !options.DryRun)
            {
                TextDocumentSearchReplaceEngine.ReplaceTextRanges(target, replacements);
            }
        }
    }

    private static bool IsBindableContentNode(OdfNode node) =>
        node.NodeType is OdfNodeType.Element &&
        ((node.NamespaceUri == OdfNamespaces.Text && node.LocalName == "p") ||
            (node.NamespaceUri == OdfNamespaces.Table && node.LocalName is "table-cell" or "covered-table-cell"));

    private static void ShiftFormulaRows(OdfNode row, int offset)
    {
        if (offset == 0)
        {
            return;
        }

        foreach (OdfNode cell in row.Descendants().Where(static node =>
            node.NodeType is OdfNodeType.Element &&
            node.NamespaceUri == OdfNamespaces.Table &&
            node.LocalName is "table-cell" or "covered-table-cell"))
        {
            string? formula = cell.GetAttribute("formula", OdfNamespaces.Table);
            if (!string.IsNullOrEmpty(formula))
            {
                cell.SetAttribute("formula", OdfNamespaces.Table, OdfFormulaTranslator.TranslateFormulaOffset(formula!, offset, 0), "table");
            }
        }
    }

    private static string ReplaceScalarTokens(
        string text,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        return ReplacePlaceholderExpressions(text ?? string.Empty, expression =>
        {
            if (expression.StartsWith("Image:", StringComparison.Ordinal))
            {
                return BuildToken(expression);
            }

            if (ResolvePath(values, expression) is object value)
            {
                report.ReplacementCount++;
                AddHit(report, expression);
                return ConvertValue(value);
            }

            AddUnresolved(report, expression);
            return ResolveUnknownPlaceholder(expression, options);
        });
    }

    private static IReadOnlyList<TextDocumentSearchReplaceEngine.TextReplacement> ResolveScalarTextReplacements(
        string text,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        List<TextDocumentSearchReplaceEngine.TextReplacement> replacements = [];
        int index = 0;
        while (index < text.Length)
        {
            int start = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            string expression = text.Substring(start + 2, end - start - 2).Trim();
            string originalToken = text.Substring(start, end + 2 - start);
            string replacement;
            if (expression.StartsWith("Image:", StringComparison.Ordinal))
            {
                replacement = BuildToken(expression);
            }
            else if (ResolvePath(values, expression) is object value)
            {
                report.ReplacementCount++;
                AddHit(report, expression);
                replacement = ConvertValue(value);
            }
            else
            {
                AddUnresolved(report, expression);
                replacement = ResolveUnknownPlaceholder(expression, options);
            }

            if (!string.Equals(originalToken, replacement, StringComparison.Ordinal))
            {
                replacements.Add(new TextDocumentSearchReplaceEngine.TextReplacement(
                    start,
                    originalToken.Length,
                    replacement));
            }

            index = end + 2;
        }

        return replacements;
    }

    private static IReadOnlyList<TextDocumentSearchReplaceEngine.TextReplacement> ResolveCollectionTextReplacements(
        string text,
        string collectionName,
        object? item,
        IReadOnlyDictionary<string, object?> rootValues,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        List<TextDocumentSearchReplaceEngine.TextReplacement> replacements = [];
        string collectionPrefix = collectionName + "[].";
        int index = 0;
        while (index < text.Length)
        {
            int start = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            string expression = text.Substring(start + 2, end - start - 2).Trim();
            string originalToken = text.Substring(start, end + 2 - start);
            string replacement;
            if (expression.StartsWith(collectionPrefix, StringComparison.Ordinal))
            {
                AddHit(report, expression);
                report.ReplacementCount++;
                replacement = ConvertValue(ResolvePath(item, expression.Substring(collectionPrefix.Length)));
            }
            else if (ResolvePath(rootValues, expression) is object value)
            {
                AddHit(report, expression);
                report.ReplacementCount++;
                replacement = ConvertValue(value);
            }
            else
            {
                AddUnresolved(report, expression);
                replacement = ResolveUnknownPlaceholder(expression, options);
            }

            if (!string.Equals(originalToken, replacement, StringComparison.Ordinal))
            {
                replacements.Add(new TextDocumentSearchReplaceEngine.TextReplacement(
                    start,
                    originalToken.Length,
                    replacement));
            }

            index = end + 2;
        }

        return replacements;
    }

    private static string ReplacePlaceholderExpressions(string text, Func<string, string> replace)
    {
        int index = 0;
        var parts = new List<string>();
        while (index < text.Length)
        {
            int start = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                parts.Add(text.Substring(index));
                break;
            }

            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                parts.Add(text.Substring(index));
                break;
            }

            parts.Add(text.Substring(index, start - index));
            string expression = text.Substring(start + 2, end - start - 2).Trim();
            parts.Add(replace(expression));
            index = end + 2;
        }

        return string.Concat(parts);
    }

    private static bool TryGetCollection(
        IReadOnlyDictionary<string, object?> values,
        string name,
        out List<object?> items)
    {
        items = new List<object?>();
        if (!values.TryGetValue(name, out object? value) || value is null || value is string)
        {
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                items.Add(item);
            }

            return true;
        }

        return false;
    }

    private static object? ResolvePath(object? source, string path)
    {
        object? current = source;
        foreach (string segment in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            if (current is IReadOnlyDictionary<string, object?> typed &&
                typed.TryGetValue(segment, out object? typedValue))
            {
                current = typedValue;
                continue;
            }

            if (current is IDictionary dictionary && dictionary.Contains(segment))
            {
                current = dictionary[segment];
                continue;
            }

            var property = current.GetType()
                .GetProperties()
                .FirstOrDefault(propertyInfo => string.Equals(propertyInfo.Name, segment, StringComparison.OrdinalIgnoreCase));
            current = property?.GetValue(current);
        }

        return current;
    }

    private static string BuildToken(string name) => "{{" + (name ?? string.Empty).Trim() + "}}";

    private static void AddHit(OdfTemplateBindReport report, string expression)
    {
        if (report.PlaceholderHits.TryGetValue(expression, out int count))
        {
            report.PlaceholderHits[expression] = count + 1;
        }
        else
        {
            report.PlaceholderHits[expression] = 1;
        }
    }

    private static void AddUnresolved(OdfTemplateBindReport report, string expression)
    {
        if (!report.UnresolvedPlaceholders.Contains(expression))
        {
            report.UnresolvedPlaceholders.Add(expression);
        }
    }

    private static OdfTemplateBindReport FinalizeReport(
        OdfNode root,
        string documentKind,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        CollectUnresolvedPlaceholders(root, documentKind, report);
        if (options.StrictMode && report.UnresolvedPlaceholders.Count > 0)
        {
            report.Warnings.Add("TPL0002: " + string.Join(", ", report.UnresolvedPlaceholders));
        }

        return report;
    }

    private static void CollectUnresolvedPlaceholders(OdfNode root, string documentKind, OdfTemplateBindReport report)
    {
        foreach (OdfNode node in new[] { root }.Concat(root.Descendants()))
        {
            string text = node.TextContent;
            int index = 0;
            while (index < text.Length)
            {
                int start = text.IndexOf("{{", index, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    break;
                }

                string expression = text.Substring(start + 2, end - start - 2).Trim();
                AddUnresolved(report, expression);
                if (!report.UnresolvedPlaceholderDetails.Any(detail =>
                    string.Equals(detail.Expression, expression, StringComparison.Ordinal) &&
                    string.Equals(detail.DocumentKind, documentKind, StringComparison.Ordinal) &&
                    string.Equals(detail.LocationHint, node.LocalName, StringComparison.Ordinal)))
                {
                    AddUnresolvedDetail(report, expression, documentKind, node.LocalName);
                }

                index = end + 2;
            }
        }
    }

    private static string ResolveUnknownPlaceholder(string expression, OdfTemplateBindOptions options) =>
        options.UnknownPlaceholderPolicy is OdfTemplateUnknownPlaceholderPolicy.EmptyString
            ? string.Empty
            : BuildToken(expression);

    private static void AddImageWarning(OdfTemplateBindReport report, string warning)
    {
        if (!report.Warnings.Contains(warning))
        {
            report.Warnings.Add(warning);
        }
    }

    private static void AddUnresolvedDetail(
        OdfTemplateBindReport report,
        string expression,
        string documentKind,
        string locationHint)
    {
        if (!report.UnresolvedPlaceholderDetails.Any(detail =>
            string.Equals(detail.Expression, expression, StringComparison.Ordinal) &&
            string.Equals(detail.DocumentKind, documentKind, StringComparison.Ordinal) &&
            string.Equals(detail.LocationHint, locationHint, StringComparison.Ordinal)))
        {
            report.UnresolvedPlaceholderDetails.Add(new OdfTemplateUnresolvedPlaceholder(
                expression,
                documentKind,
                locationHint));
        }
    }

    private static string ConvertValue(object? value) =>
        value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
}
