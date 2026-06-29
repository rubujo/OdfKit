using System.Text.Json;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Collaboration;

/// <summary>
/// Imports portable operation logs into ODT documents.
/// 將 ODF Toolkit 相容的 JSON operations 序列單向 merge 至 <see cref="TextDocument"/>。
/// </summary>
/// <remarks>
/// 對應 <see cref="OdtOperationsExporter"/> 所能匯出的 <c>addParagraph</c>／<c>addText</c>／<c>addTab</c>／
/// <c>addLineBreak</c> 子集合；匯入端另提供 TDF ODF Toolkit operation 名稱的受限 clean-room 相容子集合。
/// 匯入端同時接受裸陣列與 TDF ODF Toolkit 的 <c>{ "changes": [...] }</c> 封包。
/// </remarks>
public static class OdtOperationsImporter
{
    /// <summary>
    /// Merges operation log content into the target document.
    /// 將 JSON operations 陣列字串重播至新建立的文字文件。
    /// </summary>
    /// <param name="operationsJson">The value to use. / JSON operations 陣列字串</param>
    /// <returns>The result. / 套用 operations 後的文字文件</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="operationsJson"/> 為 null 時擲出</exception>
    public static TextDocument Merge(string operationsJson)
    {
        TextDocument document = TextDocument.Create();
        Merge(document, operationsJson);
        return document;
    }

    /// <summary>
    /// Merges operation log content into the target document.
    /// 將 JSON operations 字串重播至新建立的文字文件，並回傳匯入診斷。
    /// </summary>
    /// <param name="operationsJson">The value to use. / JSON operations 陣列或 TDF changes 封包字串</param>
    /// <param name="options">The value to use. / ODF Toolkit 相容選項；若為 <see langword="null"/>，則使用預設診斷策略</param>
    /// <param name="report">The value to use. / 匯入重播與相容性診斷結果</param>
    /// <returns>The result. / 套用 operations 後的文字文件</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="operationsJson"/> 為 null 時擲出</exception>
    public static TextDocument Merge(
        string operationsJson,
        OdtOperationCompatibilityOptions? options,
        out OdtOperationImportReport report)
    {
        TextDocument document = TextDocument.Create();
        report = Merge(document, operationsJson, options);
        return document;
    }

    /// <summary>
    /// Merges operation log content into the target document.
    /// 將 JSON operations 陣列字串重播至既有的文字文件結尾。
    /// </summary>
    /// <param name="document">The source or target object. / 目標文字文件</param>
    /// <param name="operationsJson">The value to use. / JSON operations 陣列字串</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一參數為 null 時擲出</exception>
    public static void Merge(TextDocument document, string operationsJson)
        => Merge(document, operationsJson, null);

    /// <summary>
    /// Merges operation log content into the target document.
    /// 將 JSON operations 字串重播至既有的文字文件結尾，並回傳匯入診斷。
    /// </summary>
    /// <param name="document">The source or target object. / 目標文字文件</param>
    /// <param name="operationsJson">The value to use. / JSON operations 陣列或 TDF changes 封包字串</param>
    /// <param name="options">The value to use. / ODF Toolkit 相容選項；若為 <see langword="null"/>，則使用預設診斷策略</param>
    /// <returns>The result. / 匯入重播與相容性診斷結果</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時擲出</exception>
    public static OdtOperationImportReport Merge(
        TextDocument document,
        string operationsJson,
        OdtOperationCompatibilityOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (operationsJson is null)
        {
            throw new ArgumentNullException(nameof(operationsJson));
        }

        options ??= new OdtOperationCompatibilityOptions();
        var report = new OdtOperationImportReport();
        using JsonDocument parsed = JsonDocument.Parse(operationsJson);
        JsonElement operationsRoot = GetOperationsRoot(parsed.RootElement);
        int topLevelParagraphOffset = GetTopLevelParagraphNodes(document).Count;
        List<OdfParagraph> paragraphs = [];
        OdfParagraph? currentParagraph = null;
        OdfList? currentList = null;
        OdfTable? currentTable = null;
        int currentTableRow = 0;
        int currentTableColumn = 0;

        foreach (JsonElement operation in operationsRoot.EnumerateArray())
        {
            string? name = operation.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString()
                : null;

            switch (name)
            {
                case "addParagraph":
                    currentParagraph = CreateParagraph(document, operation, ref currentList);
                    paragraphs.Add(currentParagraph);
                    if (operation.TryGetProperty("attrs", out JsonElement attrs) &&
                        attrs.ValueKind == JsonValueKind.Object &&
                        TryGetParagraphStyleName(attrs, out string? styleName))
                    {
                        currentParagraph.StyleName = styleName;
                    }

                    report.RecordReplayed();
                    break;

                case "addText":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? textParagraph) &&
                        operation.TryGetProperty("text", out JsonElement textElement))
                    {
                        string? text = textElement.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            InsertText(textParagraph, operation, text!);
                            report.RecordReplayed();
                        }
                    }

                    break;

                case "addTab":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? tabParagraph))
                    {
                        tabParagraph.AddTab();
                        report.RecordReplayed();
                    }

                    break;

                case "addLineBreak":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? breakParagraph))
                    {
                        breakParagraph.AddLineBreak();
                        report.RecordReplayed();
                    }

                    break;

                case "delete":
                    if (TryDeleteSingleParagraphRange(paragraphs, operation))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "splitParagraph":
                    if (TrySplitParagraph(document, paragraphs, topLevelParagraphOffset, operation, out currentParagraph))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "mergeParagraph":
                    if (TryMergeParagraphs(document, paragraphs, topLevelParagraphOffset, operation, out currentParagraph))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "addTable":
                    if (TryCreateTable(document, operation, out currentTable))
                    {
                        currentTableRow = 0;
                        currentTableColumn = 0;
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "addRows":
                    if (currentTable is not null && TryMoveTableCursor(operation, ref currentTableRow, ref currentTableColumn))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "addCells":
                    if (currentTable is not null &&
                        TryFillTableCells(currentTable, operation, ref currentTableRow, ref currentTableColumn))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                case "documentLayout":
                case "addFontDecl":
                case "addStyle":
                case "addListStyle":
                    report.RecordIgnored(name, options);
                    break;

                case "format":
                    if (operation.TryGetProperty("attrs", out JsonElement formatAttrs) &&
                        formatAttrs.ValueKind == JsonValueKind.Object &&
                        (TryApplyFormatToRange(paragraphs, operation, formatAttrs) ||
                        (currentParagraph is not null && TryApplyFormatToLastRun(currentParagraph, formatAttrs))))
                    {
                        report.RecordReplayed();
                    }
                    else
                    {
                        report.RecordUnsupported(name, options);
                    }

                    break;

                default:
                    report.RecordUnsupported(name, options);
                    break;
            }
        }

        return report;
    }

    private static JsonElement GetOperationsRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("changes", out JsonElement changes) &&
            changes.ValueKind == JsonValueKind.Array)
        {
            return changes;
        }

        throw new JsonException();
    }

    private static bool TryGetParagraphStyleName(JsonElement attrs, out string? styleName)
    {
        if (attrs.TryGetProperty("styleName", out JsonElement styleNameElement))
        {
            styleName = styleNameElement.GetString();
            return !string.IsNullOrEmpty(styleName);
        }

        if (attrs.TryGetProperty("styleId", out JsonElement styleIdElement))
        {
            styleName = styleIdElement.GetString();
            return !string.IsNullOrEmpty(styleName);
        }

        styleName = null;
        return false;
    }

    private static OdfParagraph CreateParagraph(TextDocument document, JsonElement operation, ref OdfList? currentList)
    {
        if (operation.TryGetProperty("attrs", out JsonElement attrs) &&
            attrs.ValueKind == JsonValueKind.Object &&
            TryGetStringAttribute(attrs, "listStyleName", out string? listStyleName))
        {
            if (currentList is null || !string.Equals(currentList.StyleName, listStyleName, StringComparison.Ordinal))
            {
                currentList = document.AddList(listStyleName);
            }

            int level = TryGetInt32Attribute(attrs, "listLevel", out int parsedLevel) ? parsedLevel : 1;
            return level <= 1
                ? currentList.AddListItem().AddParagraph()
                : currentList.AddItem(string.Empty, level).Paragraphs[0];
        }

        return document.AddParagraph();
    }

    private static bool TryResolveParagraph(
        IReadOnlyList<OdfParagraph> paragraphs,
        JsonElement operation,
        OdfParagraph? fallback,
        out OdfParagraph paragraph)
    {
        if (TryGetPosition(operation, "start", out int paragraphIndex, out _) &&
            paragraphIndex >= 0 &&
            paragraphIndex < paragraphs.Count)
        {
            paragraph = paragraphs[paragraphIndex];
            return true;
        }

        if (fallback is not null)
        {
            paragraph = fallback;
            return true;
        }

        paragraph = null!;
        return false;
    }

    private static void InsertText(OdfParagraph paragraph, JsonElement operation, string text)
    {
        if (!TryGetPosition(operation, "start", out _, out int charIndex))
        {
            paragraph.AddTextRun(text);
            return;
        }

        string current = paragraph.TextContent;
        if (charIndex >= current.Length)
        {
            paragraph.AddTextRun(text);
            return;
        }

        int safeIndex = Clamp(charIndex, 0, current.Length);
        paragraph.TextContent = current.Insert(safeIndex, text);
    }

    private static bool TryDeleteSingleParagraphRange(IReadOnlyList<OdfParagraph> paragraphs, JsonElement operation)
    {
        if (!TryGetPosition(operation, "start", out int startParagraphIndex, out int startCharacterIndex) ||
            !TryGetPosition(operation, "end", out int endParagraphIndex, out int endCharacterIndex) ||
            startParagraphIndex != endParagraphIndex ||
            startParagraphIndex < 0 ||
            startParagraphIndex >= paragraphs.Count)
        {
            return false;
        }

        OdfParagraph paragraph = paragraphs[startParagraphIndex];
        string text = paragraph.TextContent;
        int startIndex = Clamp(startCharacterIndex, 0, text.Length);
        int endIndex = Clamp(endCharacterIndex, startIndex, text.Length);
        paragraph.TextContent = text.Remove(startIndex, endIndex - startIndex);
        return true;
    }

    private static bool TrySplitParagraph(
        TextDocument document,
        List<OdfParagraph> paragraphs,
        int topLevelParagraphOffset,
        JsonElement operation,
        out OdfParagraph currentParagraph)
    {
        currentParagraph = null!;
        if (!TryGetPosition(operation, "start", out int paragraphIndex, out int characterIndex) ||
            paragraphIndex < 0 ||
            paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        OdfParagraph paragraph = paragraphs[paragraphIndex];
        string text = paragraph.TextContent;
        int splitIndex = Clamp(characterIndex, 0, text.Length);
        string left = text.Substring(0, splitIndex);
        string right = text.Substring(splitIndex);
        paragraph.TextContent = left;

        List<OdfNode> paragraphNodes = GetTopLevelParagraphNodes(document);
        int actualParagraphIndex = topLevelParagraphOffset + paragraphIndex;
        if (actualParagraphIndex >= paragraphNodes.Count)
        {
            return false;
        }

        OdfNode paragraphNode = paragraphNodes[actualParagraphIndex];
        OdfNode newParagraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        newParagraphNode.TextContent = right;
        string? styleName = paragraph.StyleName;
        if (!string.IsNullOrEmpty(styleName))
        {
            newParagraphNode.SetAttribute("style-name", OdfNamespaces.Text, styleName!, "text");
        }

        document.BodyTextRoot.InsertAfter(newParagraphNode, paragraphNode);
        currentParagraph = document.Body.Paragraphs.ToList()[actualParagraphIndex + 1];
        paragraphs.Insert(paragraphIndex + 1, currentParagraph);
        return true;
    }

    private static bool TryMergeParagraphs(
        TextDocument document,
        List<OdfParagraph> paragraphs,
        int topLevelParagraphOffset,
        JsonElement operation,
        out OdfParagraph currentParagraph)
    {
        currentParagraph = null!;
        if (!TryGetPosition(operation, "start", out int paragraphIndex, out _) ||
            paragraphIndex < 0 ||
            paragraphIndex + 1 >= paragraphs.Count)
        {
            return false;
        }

        OdfParagraph paragraph = paragraphs[paragraphIndex];
        OdfParagraph nextParagraph = paragraphs[paragraphIndex + 1];
        List<OdfNode> paragraphNodes = GetTopLevelParagraphNodes(document);
        int actualParagraphIndex = topLevelParagraphOffset + paragraphIndex;
        if (actualParagraphIndex + 1 >= paragraphNodes.Count)
        {
            return false;
        }

        paragraph.TextContent += nextParagraph.TextContent;
        OdfNode nextParagraphNode = paragraphNodes[actualParagraphIndex + 1];
        nextParagraphNode.Parent?.RemoveChild(nextParagraphNode);
        paragraphs.RemoveAt(paragraphIndex + 1);
        currentParagraph = paragraph;
        return true;
    }

    private static bool TryCreateTable(TextDocument document, JsonElement operation, out OdfTable table)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        int rows = TryGetInt32Attribute(attrs, "rows", out int parsedRows) ||
            TryGetInt32Attribute(attrs, "rowCount", out parsedRows)
                ? parsedRows
                : 1;
        int columns = TryGetInt32Attribute(attrs, "columns", out int parsedColumns) ||
            TryGetInt32Attribute(attrs, "columnCount", out parsedColumns)
                ? parsedColumns
                : 1;
        if (rows < 1 || columns < 1)
        {
            table = null!;
            return false;
        }

        table = document.AddTable(rows, columns);
        if (TryGetStringAttribute(attrs, "name", out string? name) ||
            TryGetStringAttribute(attrs, "tableName", out name))
        {
            SetLastTableName(document, name!);
        }

        return true;
    }

    private static bool TryMoveTableCursor(JsonElement operation, ref int currentTableRow, ref int currentTableColumn)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        if (TryGetInt32Attribute(attrs, "row", out int row))
        {
            currentTableRow = Math.Max(0, row);
            currentTableColumn = 0;
            return true;
        }

        if (TryGetInt32Attribute(attrs, "count", out int count))
        {
            currentTableRow += Math.Max(0, count);
            currentTableColumn = 0;
            return true;
        }

        return true;
    }

    private static bool TryFillTableCells(
        OdfTable table,
        JsonElement operation,
        ref int currentTableRow,
        ref int currentTableColumn)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        if (TryGetInt32Attribute(attrs, "row", out int row))
        {
            currentTableRow = Math.Max(0, row);
        }

        if (TryGetInt32Attribute(attrs, "column", out int column) ||
            TryGetInt32Attribute(attrs, "col", out column))
        {
            currentTableColumn = Math.Max(0, column);
        }

        if (attrs.TryGetProperty("values", out JsonElement values) && values.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in values.EnumerateArray())
            {
                if (!TrySetTableCell(table, currentTableRow, currentTableColumn, value.ToString()))
                {
                    return false;
                }

                currentTableColumn++;
            }

            return true;
        }

        if (TryGetStringAttribute(attrs, "text", out string? text))
        {
            return TrySetTableCell(table, currentTableRow, currentTableColumn, text!);
        }

        return false;
    }

    private static bool TrySetTableCell(OdfTable table, int row, int column, string text)
    {
        try
        {
            table.GetCell(row, column).AddParagraph(text);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static List<OdfNode> GetTopLevelParagraphNodes(TextDocument document)
    {
        List<OdfNode> nodes = [];
        foreach (OdfNode child in document.BodyTextRoot.Children)
        {
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            {
                nodes.Add(child);
            }
        }

        return nodes;
    }

    private static void SetLastTableName(TextDocument document, string name)
    {
        OdfNode? lastTable = null;
        foreach (OdfNode child in document.BodyTextRoot.Children)
        {
            if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                lastTable = child;
            }
        }

        lastTable?.SetAttribute("name", OdfNamespaces.Table, name, "table");
    }

    private static bool TryGetPosition(JsonElement operation, string propertyName, out int paragraphIndex, out int characterIndex)
    {
        paragraphIndex = -1;
        characterIndex = 0;
        if (!operation.TryGetProperty(propertyName, out JsonElement position) ||
            position.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        int index = 0;
        foreach (JsonElement component in position.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Number || !component.TryGetInt32(out int value))
            {
                return false;
            }

            if (index == 0)
            {
                paragraphIndex = value;
            }
            else if (index == 1)
            {
                characterIndex = value;
                return true;
            }

            index++;
        }

        return paragraphIndex >= 0;
    }

    private static bool TryGetInt32Attribute(JsonElement attrs, string name, out int value)
    {
        if (attrs.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static bool TryApplyFormatToLastRun(OdfParagraph paragraph, JsonElement attrs)
    {
        OdfTextRun? targetRun = null;
        foreach (OdfTextRun run in paragraph.Runs)
        {
            targetRun = run;
        }

        if (targetRun is null)
        {
            return false;
        }

        return ApplyFormatAttributes(targetRun, attrs);
    }

    private static bool TryApplyFormatToRange(IReadOnlyList<OdfParagraph> paragraphs, JsonElement operation, JsonElement attrs)
    {
        if (!TryGetPosition(operation, "start", out int startParagraphIndex, out int startCharacterIndex) ||
            !TryGetPosition(operation, "end", out int endParagraphIndex, out int endCharacterIndex) ||
            startParagraphIndex != endParagraphIndex ||
            startParagraphIndex < 0 ||
            startParagraphIndex >= paragraphs.Count)
        {
            return false;
        }

        OdfParagraph paragraph = paragraphs[startParagraphIndex];
        string paragraphText = paragraph.TextContent;
        int startIndex = Clamp(startCharacterIndex, 0, paragraphText.Length);
        int endIndex = Clamp(endCharacterIndex, startIndex, paragraphText.Length);
        if (startIndex == endIndex)
        {
            return false;
        }

        List<RunSnapshot> sourceRuns = paragraph.Runs
            .Select(run => new RunSnapshot(run))
            .Where(run => run.Text.Length > 0)
            .ToList();
        if (sourceRuns.Count == 0 ||
            !string.Equals(string.Concat(sourceRuns.Select(run => run.Text)), paragraphText, StringComparison.Ordinal))
        {
            return false;
        }

        List<RunSegment> segments = [];
        int offset = 0;
        bool hasFormattedSegment = false;
        foreach (RunSnapshot run in sourceRuns)
        {
            int runStart = offset;
            int runEnd = runStart + run.Text.Length;
            int selectionStart = Math.Max(startIndex, runStart);
            int selectionEnd = Math.Min(endIndex, runEnd);
            if (selectionEnd <= runStart || selectionStart >= runEnd)
            {
                segments.Add(new RunSegment(run.Text, run, false));
                offset = runEnd;
                continue;
            }

            if (selectionStart > runStart)
            {
                segments.Add(new RunSegment(run.Text.Substring(0, selectionStart - runStart), run, false));
            }

            if (selectionEnd > selectionStart)
            {
                segments.Add(new RunSegment(run.Text.Substring(selectionStart - runStart, selectionEnd - selectionStart), run, true));
                hasFormattedSegment = true;
            }

            if (selectionEnd < runEnd)
            {
                segments.Add(new RunSegment(run.Text.Substring(selectionEnd - runStart), run, false));
            }

            offset = runEnd;
        }

        if (!hasFormattedSegment || !HasSupportedFormatAttribute(attrs))
        {
            return false;
        }

        paragraph.ClearRuns();
        foreach (RunSegment segment in segments)
        {
            OdfTextRun run = paragraph.AddTextRun(segment.Text);
            segment.Source.ApplyTo(run);
            if (segment.Format)
            {
                ApplyFormatAttributes(run, attrs);
            }
        }

        return true;
    }

    private static bool HasSupportedFormatAttribute(JsonElement attrs) =>
        attrs.TryGetProperty("bold", out _) ||
        attrs.TryGetProperty("fontWeight", out _) ||
        attrs.TryGetProperty("italic", out _) ||
        attrs.TryGetProperty("fontStyle", out _) ||
        attrs.TryGetProperty("underline", out _) ||
        attrs.TryGetProperty("strikethrough", out _) ||
        attrs.TryGetProperty("color", out _) ||
        attrs.TryGetProperty("backgroundColor", out _) ||
        attrs.TryGetProperty("highlightColor", out _) ||
        attrs.TryGetProperty("fontSize", out _) ||
        attrs.TryGetProperty("fontName", out _) ||
        attrs.TryGetProperty("textTransform", out _) ||
        attrs.TryGetProperty("fontVariant", out _) ||
        attrs.TryGetProperty("textPosition", out _) ||
        attrs.TryGetProperty("superscript", out _) ||
        attrs.TryGetProperty("subscript", out _) ||
        attrs.TryGetProperty("styleName", out _) ||
        attrs.TryGetProperty("styleId", out _);

    private static bool ApplyFormatAttributes(OdfTextRun targetRun, JsonElement attrs)
    {
        bool applied = false;
        if (TryGetBooleanAttribute(attrs, "bold", out bool bold) ||
            TryGetTokenBooleanAttribute(attrs, "fontWeight", "bold", out bold))
        {
            targetRun.IsBold = bold;
            applied = true;
        }

        if (TryGetBooleanAttribute(attrs, "italic", out bool italic) ||
            TryGetTokenBooleanAttribute(attrs, "fontStyle", "italic", out italic))
        {
            targetRun.IsItalic = italic;
            applied = true;
        }

        if (TryGetBooleanAttribute(attrs, "underline", out bool underline))
        {
            targetRun.IsUnderline = underline;
            applied = true;
        }

        if (TryGetBooleanAttribute(attrs, "strikethrough", out bool strikethrough))
        {
            targetRun.IsStrikethrough = strikethrough;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "color", out string? color))
        {
            targetRun.Color = color;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "backgroundColor", out string? backgroundColor) ||
            TryGetStringAttribute(attrs, "highlightColor", out backgroundColor))
        {
            targetRun.BackgroundColor = backgroundColor;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "fontSize", out string? fontSize) && fontSize is not null)
        {
            targetRun.SetFontSize(fontSize);
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "fontName", out string? fontName) && fontName is not null)
        {
            targetRun.SetFont(fontName);
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "textTransform", out string? textTransform))
        {
            targetRun.TextTransform = textTransform;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "fontVariant", out string? fontVariant))
        {
            targetRun.FontVariant = fontVariant;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "textPosition", out string? textPosition))
        {
            targetRun.TextPosition = textPosition;
            applied = true;
        }

        if (TryGetBooleanAttribute(attrs, "superscript", out bool superscript))
        {
            targetRun.IsSuperscript = superscript;
            applied = true;
        }

        if (TryGetBooleanAttribute(attrs, "subscript", out bool subscript))
        {
            targetRun.IsSubscript = subscript;
            applied = true;
        }

        if (TryGetStringAttribute(attrs, "styleName", out string? styleName) ||
            TryGetStringAttribute(attrs, "styleId", out styleName))
        {
            targetRun.StyleName = styleName;
            applied = true;
        }

        return applied;
    }

    private sealed class RunSnapshot
    {
        public RunSnapshot(OdfTextRun run)
        {
            Text = run.Text;
            StyleName = run.StyleName;
            FontName = run.FontName;
            FontSize = run.FontSize;
            IsBold = run.IsBold;
            IsItalic = run.IsItalic;
            IsUnderline = run.IsUnderline;
            IsStrikethrough = run.IsStrikethrough;
            TextPosition = run.TextPosition;
            Color = run.Color;
            BackgroundColor = run.BackgroundColor;
            TextTransform = run.TextTransform;
            FontVariant = run.FontVariant;
        }

        public string Text { get; }

        private string? StyleName { get; }

        private string? FontName { get; }

        private string? FontSize { get; }

        private bool IsBold { get; }

        private bool IsItalic { get; }

        private bool IsUnderline { get; }

        private bool IsStrikethrough { get; }

        private string? TextPosition { get; }

        private string? Color { get; }

        private string? BackgroundColor { get; }

        private string? TextTransform { get; }

        private string? FontVariant { get; }

        public void ApplyTo(OdfTextRun run)
        {
            if (!string.IsNullOrEmpty(StyleName))
            {
                run.StyleName = StyleName;
            }

            if (!string.IsNullOrEmpty(FontName))
            {
                run.SetFont(FontName!);
            }

            if (!string.IsNullOrEmpty(FontSize))
            {
                run.SetFontSize(FontSize!);
            }

            run.IsBold = IsBold;
            run.IsItalic = IsItalic;
            run.IsUnderline = IsUnderline;
            run.IsStrikethrough = IsStrikethrough;

            if (!string.IsNullOrEmpty(TextPosition))
            {
                run.TextPosition = TextPosition;
            }

            if (!string.IsNullOrEmpty(Color))
            {
                run.Color = Color;
            }

            if (!string.IsNullOrEmpty(BackgroundColor))
            {
                run.BackgroundColor = BackgroundColor;
            }

            if (!string.IsNullOrEmpty(TextTransform))
            {
                run.TextTransform = TextTransform;
            }

            if (!string.IsNullOrEmpty(FontVariant))
            {
                run.FontVariant = FontVariant;
            }
        }
    }

    private sealed class RunSegment
    {
        public RunSegment(string text, RunSnapshot source, bool format)
        {
            Text = text;
            Source = source;
            Format = format;
        }

        public string Text { get; }

        public RunSnapshot Source { get; }

        public bool Format { get; }
    }

    private static bool TryGetBooleanAttribute(JsonElement attrs, string name, out bool value)
    {
        if (attrs.TryGetProperty(name, out JsonElement element) &&
            (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetTokenBooleanAttribute(JsonElement attrs, string name, string positiveValue, out bool value)
    {
        if (TryGetStringAttribute(attrs, name, out string? token))
        {
            value = string.Equals(token, positiveValue, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetStringAttribute(JsonElement attrs, string name, out string? value)
    {
        if (attrs.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return !string.IsNullOrEmpty(value);
        }

        value = null;
        return false;
    }
}
