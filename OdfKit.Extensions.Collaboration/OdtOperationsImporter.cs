using System.Globalization;
using System.Text.Json;
using OdfKit.Compliance;
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
        OdtOperationLog operationLog = OdtOperationLog.Parse(operationsJson, options);
        return Merge(document, operationLog, options);
    }

    /// <summary>
    /// Merges a parsed operation log into the target document.
    /// 將已剖析的 operation log 重播至目標文件。
    /// </summary>
    /// <param name="document">The target text document. / 目標文字文件。</param>
    /// <param name="operationLog">The parsed operation log. / 已剖析的 operation log。</param>
    /// <param name="options">The compatibility options. / ODF Toolkit 相容選項；若為 <see langword="null"/>，則使用預設診斷策略。</param>
    /// <returns>The import replay report. / 匯入重播與相容性診斷結果。</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時擲出。</exception>
    public static OdtOperationImportReport Merge(
        TextDocument document,
        OdtOperationLog operationLog,
        OdtOperationCompatibilityOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (operationLog is null)
        {
            throw new ArgumentNullException(nameof(operationLog));
        }

        options ??= new OdtOperationCompatibilityOptions();
        var report = new OdtOperationImportReport();
        int topLevelParagraphOffset = GetTopLevelParagraphNodes(document).Count;
        List<OdfParagraph> paragraphs = [];
        OdfParagraph? currentParagraph = null;
        OdfList? currentList = null;
        OdfTable? currentTable = null;
        int currentTableRow = 0;
        int currentTableColumn = 0;

        foreach (OdtOperation parsedOperation in operationLog.Operations)
        {
            JsonElement operation = parsedOperation.RawElement;
            string? name = parsedOperation.Name;
            if (HasUnsafeReplayAttribute(parsedOperation, options.Safety, out string? unsafeReason))
            {
                report.RecordUnsupported(name, options, parsedOperation, unsafeReason);
                continue;
            }

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

                    report.RecordReplayed(parsedOperation);
                    break;

                case "addText":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? textParagraph) &&
                        operation.TryGetProperty("text", out JsonElement textElement))
                    {
                        string? text = textElement.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            InsertText(textParagraph, operation, text!);
                            report.RecordReplayed(parsedOperation);
                        }
                    }

                    break;

                case "addTab":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? tabParagraph))
                    {
                        tabParagraph.AddTab();
                        report.RecordReplayed(parsedOperation);
                    }

                    break;

                case "addLineBreak":
                    if (TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph? breakParagraph))
                    {
                        breakParagraph.AddLineBreak();
                        report.RecordReplayed(parsedOperation);
                    }

                    break;

                case "delete":
                    if (TryDeleteSingleParagraphRange(paragraphs, operation))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "move":
                    if (TryMoveSingleParagraphRange(paragraphs, operation))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "splitParagraph":
                    if (TrySplitParagraph(document, paragraphs, topLevelParagraphOffset, operation, out currentParagraph))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "mergeParagraph":
                    if (TryMergeParagraphs(document, paragraphs, topLevelParagraphOffset, operation, out currentParagraph))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addTable":
                    if (TryCreateTable(document, operation, options.Safety, report, out currentTable))
                    {
                        currentTableRow = 0;
                        currentTableColumn = 0;
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation, report.SafetyLimitHitReason);
                    }

                    break;

                case "addRows":
                    if (currentTable is not null && TryMoveTableCursor(operation, ref currentTableRow, ref currentTableColumn))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addCells":
                    if (currentTable is not null &&
                        TryFillTableCells(currentTable, operation, ref currentTableRow, ref currentTableColumn))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addColumn":
                case "addColumns":
                    if (currentTable is not null && TryMutateColumns(currentTable, operation, insert: true))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "deleteColumns":
                    if (currentTable is not null && TryMutateColumns(currentTable, operation, insert: false))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "documentLayout":
                case "addListStyle":
                case "addStyle":
                    report.RecordIgnored(name, options, parsedOperation);
                    break;

                case "addFontDecl":
                    if (TryAddFontDeclaration(document, operation))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordIgnored(name, options, parsedOperation);
                    }

                    break;

                case "changeStyle":
                    if (TryReplayStyleMetadata(document, operation, remove: false))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordIgnored(name, options, parsedOperation);
                    }

                    break;

                case "deleteStyle":
                    if (TryReplayStyleMetadata(document, operation, remove: true))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordIgnored(name, options, parsedOperation);
                    }

                    break;

                case "addField":
                case "updateField":
                    if (TryReplayField(paragraphs, operation, currentParagraph))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addNote":
                    if (TryReplayNote(paragraphs, operation, currentParagraph))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addHeaderFooter":
                    if (TryReplayHeaderFooter(document, operation, delete: false))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "deleteHeaderFooter":
                case "deleteHeaderFooterContent":
                    if (TryReplayHeaderFooter(document, operation, delete: true))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "addDrawing":
                    if (TryReplaySafeDrawingPlaceholder(paragraphs, operation, currentParagraph))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                case "format":
                    if (operation.TryGetProperty("attrs", out JsonElement formatAttrs) &&
                        formatAttrs.ValueKind == JsonValueKind.Object &&
                        (TryApplyFormatToRange(paragraphs, operation, formatAttrs) ||
                        (currentParagraph is not null && TryApplyFormatToLastRun(currentParagraph, formatAttrs))))
                    {
                        report.RecordReplayed(parsedOperation);
                    }
                    else
                    {
                        report.RecordUnsupported(name, options, parsedOperation);
                    }

                    break;

                default:
                    report.RecordUnsupported(name, options, parsedOperation);
                    break;
            }
        }

        return report;
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

    private static bool TryCreateTable(
        TextDocument document,
        JsonElement operation,
        OdtOperationSafetyOptions safety,
        OdtOperationImportReport report,
        out OdfTable table)
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

        long totalCells = (long)rows * columns;
        if (rows > safety.MaxTableRows || columns > safety.MaxTableColumns || totalCells > safety.MaxTableCells)
        {
            report.RecordSafetyLimit(OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "tableSize",
                rows.ToString(CultureInfo.InvariantCulture) + "x" + columns.ToString(CultureInfo.InvariantCulture),
                safety.MaxTableRows.ToString(CultureInfo.InvariantCulture) + "x" +
                safety.MaxTableColumns.ToString(CultureInfo.InvariantCulture) + "/" +
                safety.MaxTableCells.ToString(CultureInfo.InvariantCulture)));
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

    private static bool TryMoveSingleParagraphRange(IReadOnlyList<OdfParagraph> paragraphs, JsonElement operation)
    {
        if (!TryGetPosition(operation, "start", out int startParagraphIndex, out int startCharacterIndex) ||
            !TryGetPosition(operation, "end", out int endParagraphIndex, out int endCharacterIndex) ||
            !TryGetPositionAny(operation, out int targetParagraphIndex, out int targetCharacterIndex, "to", "target") ||
            startParagraphIndex != endParagraphIndex ||
            startParagraphIndex != targetParagraphIndex ||
            startParagraphIndex < 0 ||
            startParagraphIndex >= paragraphs.Count)
        {
            return false;
        }

        OdfParagraph paragraph = paragraphs[startParagraphIndex];
        string text = paragraph.TextContent;
        int startIndex = Clamp(startCharacterIndex, 0, text.Length);
        int endIndex = Clamp(endCharacterIndex, startIndex, text.Length);
        if (startIndex == endIndex)
        {
            return false;
        }

        string movedText = text.Substring(startIndex, endIndex - startIndex);
        string remaining = text.Remove(startIndex, endIndex - startIndex);

        // targetCharacterIndex 是原始字串座標；移除 [startIndex, endIndex) 後，落在移除範圍之後的目標
        // 必須扣除已移除的長度才能對應到 remaining 的正確位置，否則插入位置會被偏移。
        int adjustedTargetIndex = targetCharacterIndex <= startIndex
            ? targetCharacterIndex
            : targetCharacterIndex >= endIndex
                ? targetCharacterIndex - (endIndex - startIndex)
                : startIndex;
        int targetIndex = Clamp(adjustedTargetIndex, 0, remaining.Length);
        paragraph.TextContent = remaining.Insert(targetIndex, movedText);
        return true;
    }

    private static bool TryMutateColumns(OdfTable table, JsonElement operation, bool insert)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        int position = TryGetInt32Attribute(attrs, "startGrid", out int parsedPosition) ||
            TryGetInt32Attribute(attrs, "position", out parsedPosition) ||
            TryGetInt32Attribute(attrs, "column", out parsedPosition) ||
            TryGetInt32Attribute(attrs, "col", out parsedPosition)
                ? parsedPosition
                : 0;
        int count;
        if (TryGetInt32Attribute(attrs, "endGrid", out int endGrid))
        {
            count = endGrid - position + 1;
        }
        else
        {
            count = TryGetInt32Attribute(attrs, "count", out int parsedCount) ? parsedCount : 1;
        }
        if (position < 0 || count < 1)
        {
            return false;
        }

        try
        {
            if (insert)
            {
                table.InsertColumns(position, count);
            }
            else
            {
                table.DeleteColumns(position, count);
            }

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

    private static bool TryAddFontDeclaration(TextDocument document, JsonElement operation)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        if (!TryGetStringAttribute(attrs, "name", out string? name) &&
            !TryGetStringAttribute(attrs, "fontName", out name))
        {
            return false;
        }

        string family = TryGetStringAttribute(attrs, "fontFamily", out string? parsedFamily) ||
            TryGetStringAttribute(attrs, "family", out parsedFamily)
                ? parsedFamily!
                : name!;
        TryGetStringAttribute(attrs, "genericFamily", out string? genericFamily);
        TryGetStringAttribute(attrs, "pitch", out string? pitch);
        document.AddFontFace(name!, family, genericFamily, pitch);
        return true;
    }

    private static bool TryReplayStyleMetadata(TextDocument document, JsonElement operation, bool remove)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        if (!TryGetStringAttribute(attrs, "styleId", out string? styleName) &&
            !TryGetStringAttribute(attrs, "styleName", out styleName) &&
            !TryGetStringAttribute(attrs, "name", out styleName))
        {
            return false;
        }

        if (remove)
        {
            return document.Styles.StyleExists(styleName!);
        }

        return true;
    }

    private static bool TryReplayField(
        IReadOnlyList<OdfParagraph> paragraphs,
        JsonElement operation,
        OdfParagraph? currentParagraph)
    {
        if (!TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph paragraph))
        {
            return false;
        }

        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        string fieldType = TryGetStringAttribute(attrs, "fieldType", out string? parsedType) ||
            TryGetStringAttribute(attrs, "type", out parsedType)
                ? parsedType!
                : "variable";
        switch (fieldType)
        {
            case "date":
                paragraph.AddDateField();
                return true;
            case "time":
                paragraph.AddTimeField();
                return true;
            case "author":
            case "author-name":
                paragraph.AddAuthorField();
                return true;
            case "chapter":
                paragraph.AddChapterField();
                return true;
            case "sequence":
                if (TryGetStringAttribute(attrs, "name", out string? sequenceName))
                {
                    string numFormat = TryGetStringAttribute(attrs, "numFormat", out string? parsedFormat) ? parsedFormat! : "1";
                    paragraph.AddSequenceField(sequenceName!, numFormat);
                    return true;
                }

                return false;
            case "reference":
            case "reference-ref":
                if (TryGetStringAttribute(attrs, "refName", out string? refName) ||
                    TryGetStringAttribute(attrs, "ref-name", out refName))
                {
                    paragraph.AddReferenceField(refName!);
                    return true;
                }

                return false;
            case "bookmark-ref":
                if (TryGetStringAttribute(attrs, "bookmarkName", out string? bookmarkName) ||
                    TryGetStringAttribute(attrs, "refName", out bookmarkName))
                {
                    string format = TryGetStringAttribute(attrs, "referenceFormat", out string? parsedFormat) ? parsedFormat! : "text";
                    paragraph.AddBookmarkReferenceField(bookmarkName!, format);
                    return true;
                }

                return false;
            default:
                if (TryGetStringAttribute(attrs, "name", out string? variableName))
                {
                    string value = TryGetStringAttribute(attrs, "value", out string? parsedValue) ? parsedValue! : string.Empty;
                    paragraph.AddVariableSetField(variableName!, value);
                    return true;
                }

                return false;
        }
    }

    private static bool TryReplayNote(
        IReadOnlyList<OdfParagraph> paragraphs,
        JsonElement operation,
        OdfParagraph? currentParagraph)
    {
        if (!TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph paragraph))
        {
            return false;
        }

        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;

        // 以「是否存在 author 屬性（即使為空字串）」作為留言（comment）與註腳/尾註的判斷依據，
        // 不可用 string.IsNullOrEmpty 篩除空字串，否則作者名稱為空的留言會被誤判為註腳/尾註重播。
        string? author = null;
        bool isComment = false;
        if (attrs.TryGetProperty("author", out JsonElement attrsAuthorElement) &&
            attrsAuthorElement.ValueKind == JsonValueKind.String)
        {
            author = attrsAuthorElement.GetString() ?? string.Empty;
            isComment = true;
        }
        else if (operation.TryGetProperty("author", out JsonElement authorElement) &&
            authorElement.ValueKind == JsonValueKind.String)
        {
            author = authorElement.GetString() ?? string.Empty;
            isComment = true;
        }

        if (isComment)
        {
            string commentText = TryGetStringAttribute(attrs, "text", out string? parsedCommentText)
                ? parsedCommentText!
                : operation.TryGetProperty("text", out JsonElement commentTextElement)
                    ? commentTextElement.GetString() ?? string.Empty
                    : string.Empty;
            DateTime date = TryGetStringAttribute(attrs, "date", out string? parsedDate) &&
                DateTime.TryParse(parsedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedDateTime)
                    ? parsedDateTime
                    : DateTime.UtcNow;
            string name = TryGetStringAttribute(attrs, "id", out string? parsedId) ||
                operation.TryGetProperty("id", out JsonElement idElement) &&
                idElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(parsedId = idElement.GetString())
                    ? parsedId!
                    : Guid.NewGuid().ToString("N");
            paragraph.AddComment(new OdfComment(author!, commentText, date, name));
            return true;
        }

        string citation = TryGetStringAttribute(attrs, "citation", out string? parsedCitation) ? parsedCitation! : "*";
        string bodyText = TryGetStringAttribute(attrs, "bodyText", out string? parsedBody) ||
            TryGetStringAttribute(attrs, "text", out parsedBody)
                ? parsedBody!
                : operation.TryGetProperty("text", out JsonElement textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
        string noteClass = TryGetStringAttribute(attrs, "noteClass", out string? parsedClass) ||
            TryGetStringAttribute(attrs, "class", out parsedClass)
                ? parsedClass!
                : "footnote";
        if (string.Equals(noteClass, "endnote", StringComparison.OrdinalIgnoreCase))
        {
            paragraph.AddEndnote(citation, bodyText);
        }
        else
        {
            paragraph.AddFootnote(citation, bodyText);
        }

        return true;
    }

    private static bool TryReplayHeaderFooter(TextDocument document, JsonElement operation, bool delete)
    {
        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        string region = TryGetStringAttribute(attrs, "region", out string? parsedRegion) ||
            TryGetStringAttribute(attrs, "type", out parsedRegion)
                ? parsedRegion!
                : "header";
        string? text = operation.TryGetProperty("text", out JsonElement textElement)
            ? textElement.GetString()
            : null;
        if (text is null)
        {
            TryGetStringAttribute(attrs, "text", out text);
        }

        OdfPageSetup setup = document.GetDefaultPageSetup();
        switch (NormalizeHeaderFooterRegion(region))
        {
            case "header":
                setup.Header.Text = delete ? null : text ?? string.Empty;
                return true;
            case "header-left":
                setup.HeaderLeft.Text = delete ? null : text ?? string.Empty;
                return true;
            case "header-first":
                setup.HeaderFirst.Text = delete ? null : text ?? string.Empty;
                return true;
            case "footer":
                setup.Footer.Text = delete ? null : text ?? string.Empty;
                return true;
            case "footer-left":
                setup.FooterLeft.Text = delete ? null : text ?? string.Empty;
                return true;
            case "footer-first":
                setup.FooterFirst.Text = delete ? null : text ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeHeaderFooterRegion(string region)
    {
        return region switch
        {
            "header_default" => "header",
            "footer_default" => "footer",
            "header_even" => "header-left",
            "footer_even" => "footer-left",
            "header_first" => "header-first",
            "footer_first" => "footer-first",
            _ => region,
        };
    }

    private static bool TryReplaySafeDrawingPlaceholder(
        IReadOnlyList<OdfParagraph> paragraphs,
        JsonElement operation,
        OdfParagraph? currentParagraph)
    {
        if (!TryResolveParagraph(paragraphs, operation, currentParagraph, out OdfParagraph paragraph))
        {
            return false;
        }

        JsonElement attrs = operation.TryGetProperty("attrs", out JsonElement parsedAttrs) && parsedAttrs.ValueKind == JsonValueKind.Object
            ? parsedAttrs
            : operation;
        string label = TryGetStringAttribute(attrs, "name", out string? name) ? name! : "drawing";
        paragraph.AddTextRun($"[{label}]");
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

    private static bool HasUnsafeReplayAttribute(OdtOperation operation, OdtOperationSafetyOptions safety, out string? reason)
    {
        reason = null;
        if (operation.Attrs is not JsonElement attrs)
        {
            return false;
        }

        if (safety.RejectScriptAndEventAttributes &&
            TryFindScriptOrEventAttribute(attrs, out string? unsafeName))
        {
            reason = OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "unsafeAttribute",
                unsafeName ?? string.Empty,
                "script/event");
            return true;
        }

        if (safety.RejectExternalResourceAttributes &&
            TryFindExternalUriAttribute(attrs, out string? unsafeUri))
        {
            reason = OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "unsafeAttribute",
                unsafeUri ?? string.Empty,
                "externalUri");
            return true;
        }

        return false;
    }

    private static bool TryFindScriptOrEventAttribute(JsonElement element, out string? name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (IsEventHandlerAttributeName(property.Name) ||
                    IsScriptAttributeName(property.Name))
                {
                    name = property.Name;
                    return true;
                }

                if (TryFindScriptOrEventAttribute(property.Value, out name))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (TryFindScriptOrEventAttribute(item, out name))
                {
                    return true;
                }
            }
        }

        name = null;
        return false;
    }

    private static bool IsEventHandlerAttributeName(string name) =>
        name.Length > 2 &&
        name.StartsWith("on", StringComparison.OrdinalIgnoreCase);

    private static bool IsScriptAttributeName(string name) =>
        string.Equals(name, "script", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "scriptType", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "scriptContent", StringComparison.OrdinalIgnoreCase);

    private static bool TryFindExternalUriAttribute(JsonElement element, out string? uri)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (TryFindExternalUriAttribute(property.Value, out uri))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (TryFindExternalUriAttribute(item, out uri))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.String:
                string? value = element.GetString();
                if (IsExternalUri(value))
                {
                    uri = value;
                    return true;
                }

                break;
        }

        uri = null;
        return false;
    }

    private static bool IsExternalUri(string? value) =>
        value is not null &&
        (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase));

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

    private static bool TryGetPositionAny(
        JsonElement operation,
        out int paragraphIndex,
        out int characterIndex,
        params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (TryGetPosition(operation, propertyName, out paragraphIndex, out characterIndex))
            {
                return true;
            }
        }

        paragraphIndex = -1;
        characterIndex = 0;
        return false;
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
