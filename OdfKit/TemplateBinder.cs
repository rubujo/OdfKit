using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit;

/// <summary>
/// Provides low-magic placeholder binding for common ODF document types.
/// 提供常見 ODF 文件類型的低魔法占位符繫結。
/// </summary>
public static class TemplateBinder
{
    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in a text document.
    /// 取代文字文件中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The text document. / 文字文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of replacement operations requested. / 要求執行的替換作業數量。</returns>
    public static int Bind(TextDocument document, IReadOnlyDictionary<string, object?> values)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        int count = 0;
        foreach (KeyValuePair<string, object?> value in values)
        {
            string token = BuildToken(value.Key);
            document.ReplaceText(token, ConvertValue(value.Value));
            count++;
        }

        return count;
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in used spreadsheet cells.
    /// 取代試算表已使用儲存格中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The spreadsheet document. / 試算表文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of cells changed. / 已變更的儲存格數量。</returns>
    public static int Bind(SpreadsheetDocument document, IReadOnlyDictionary<string, object?> values)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        int changed = 0;
        foreach (OdfTableSheet sheet in document.GetSheets())
        {
            foreach (OdfCell cell in sheet.GetUsedCells())
            {
                string text = cell.DisplayText;
                string replaced = ReplaceTokens(text, values);
                if (!string.Equals(text, replaced, StringComparison.Ordinal))
                {
                    cell.SetValue(replaced);
                    changed++;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in presentation text boxes.
    /// 取代簡報文字方塊中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The presentation document. / 簡報文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of text paragraphs changed. / 已變更的文字段落數量。</returns>
    public static int Bind(PresentationDocument document, IReadOnlyDictionary<string, object?> values)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        int changed = 0;
        foreach (OdfSlide slide in document.Slides)
        {
            foreach (OdfTextBox textBox in slide.TextBoxes)
            {
                changed += ReplaceTextParagraphs(textBox.Node, values);
            }
        }

        return changed;
    }

    /// <summary>
    /// Replaces <c>{{Name}}</c> placeholders in drawing text boxes.
    /// 取代繪圖文件文字方塊中的 <c>{{Name}}</c> 占位符。
    /// </summary>
    /// <param name="document">The drawing document. / 繪圖文件。</param>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The number of text paragraphs changed. / 已變更的文字段落數量。</returns>
    public static int Bind(DrawingDocument document, IReadOnlyDictionary<string, object?> values)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        int changed = 0;
        foreach (OdfDrawPage page in document.Pages)
        {
            foreach (OdfTextBox textBox in page.TextBoxes)
            {
                changed += ReplaceTextParagraphs(textBox.Node, values);
            }
        }

        return changed;
    }

    private static int ReplaceTextParagraphs(OdfNode node, IReadOnlyDictionary<string, object?> values)
    {
        int changed = 0;
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                string text = child.TextContent;
                string replaced = ReplaceTokens(text, values);
                if (!string.Equals(text, replaced, StringComparison.Ordinal))
                {
                    child.TextContent = replaced;
                    changed++;
                }
            }

            changed += ReplaceTextParagraphs(child, values);
        }

        return changed;
    }

    private static string ReplaceTokens(string text, IReadOnlyDictionary<string, object?> values)
    {
        string current = text ?? string.Empty;
        foreach (KeyValuePair<string, object?> value in values)
        {
            current = current.Replace(BuildToken(value.Key), ConvertValue(value.Value));
        }

        return current;
    }

    private static string BuildToken(string name) => "{{" + (name ?? string.Empty).Trim() + "}}";

    private static string ConvertValue(object? value) =>
        value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
}
