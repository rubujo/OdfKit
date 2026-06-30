using System.Collections.Generic;
using System.Text.Json;
using OdfKit.Compliance;
using OdfKit.Text;

namespace OdfKit.Collaboration;

/// <summary>
/// Represents a parsed ODT JSON operation log.
/// 表示已剖析的 ODT JSON operation log。
/// </summary>
public sealed class OdtOperationLog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdtOperationLog"/> class.
    /// 初始化 <see cref="OdtOperationLog"/> 類別的新執行個體。
    /// </summary>
    /// <param name="operations">The operation sequence. / operation 序列。</param>
    /// <param name="envelopeMode">The envelope shape detected or selected for serialization. / 偵測或選擇用於序列化的封包形狀。</param>
    public OdtOperationLog(IReadOnlyList<OdtOperation> operations, OdtOperationEnvelopeMode envelopeMode)
    {
        Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        EnvelopeMode = envelopeMode;
    }

    /// <summary>
    /// Gets the operation sequence.
    /// 取得 operation 序列。
    /// </summary>
    public IReadOnlyList<OdtOperation> Operations { get; }

    /// <summary>
    /// Gets the envelope shape detected or selected for serialization.
    /// 取得偵測或選擇用於序列化的封包形狀。
    /// </summary>
    public OdtOperationEnvelopeMode EnvelopeMode { get; }

    /// <summary>
    /// Parses an ODT JSON operation log.
    /// 剖析 ODT JSON operation log。
    /// </summary>
    /// <param name="json">The JSON operation log. / JSON operation log。</param>
    /// <param name="options">The compatibility options. / 相容性選項。</param>
    /// <returns>The parsed operation log. / 已剖析的 operation log。</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="json"/> 為 null 時擲出。</exception>
    /// <exception cref="JsonException">Thrown when the documented condition occurs. / 當 JSON 格式或安全限制不符時擲出。</exception>
    public static OdtOperationLog Parse(string json, OdtOperationCompatibilityOptions? options = null)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        options ??= new OdtOperationCompatibilityOptions();
        OdtOperationSafetyOptions safety = options.Safety;
        if (json.Length > safety.MaxJsonLength)
        {
            throw new JsonException(OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "jsonLength",
                json.Length,
                safety.MaxJsonLength));
        }

        var documentOptions = new JsonDocumentOptions
        {
            MaxDepth = safety.MaxJsonDepth,
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
        };
        using JsonDocument parsed = JsonDocument.Parse(json, documentOptions);
        JsonElement operationsRoot = GetOperationsRoot(parsed.RootElement, out OdtOperationEnvelopeMode envelopeMode);
        var operations = new List<OdtOperation>();
        int index = 0;
        foreach (JsonElement operationElement in operationsRoot.EnumerateArray())
        {
            if (index >= safety.MaxOperationCount)
            {
                throw new JsonException(OdfLocalizer.GetMessage(
                    "Err_OdtOperationLog_SafetyLimitExceeded",
                    "operationCount",
                    index + 1,
                    safety.MaxOperationCount));
            }

            operations.Add(OdtOperation.Parse(operationElement, index, safety));
            index++;
        }

        return new OdtOperationLog(operations, envelopeMode);
    }

    /// <summary>
    /// Serializes the operation log to JSON.
    /// 將 operation log 序列化為 JSON。
    /// </summary>
    /// <param name="options">The compatibility options. / 相容性選項。</param>
    /// <returns>The serialized JSON operation log. / 序列化後的 JSON operation log。</returns>
    public string Serialize(OdtOperationCompatibilityOptions? options = null)
    {
        options ??= new OdtOperationCompatibilityOptions { EnvelopeMode = EnvelopeMode };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (options.EnvelopeMode == OdtOperationEnvelopeMode.TdfChangesObject)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("changes");
            }

            writer.WriteStartArray();
            foreach (OdtOperation operation in Operations)
            {
                operation.WriteTo(writer, options.PreserveUnknownProperties);
            }

            writer.WriteEndArray();
            if (options.EnvelopeMode == OdtOperationEnvelopeMode.TdfChangesObject)
            {
                writer.WriteEndObject();
            }
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Applies this operation log to a text document.
    /// 將此 operation log 套用至文字文件。
    /// </summary>
    /// <param name="document">The target text document. / 目標文字文件。</param>
    /// <param name="options">The compatibility options. / 相容性選項。</param>
    /// <returns>The import report. / 匯入報告。</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="document"/> 為 null 時擲出。</exception>
    public OdtOperationImportReport Apply(TextDocument document, OdtOperationCompatibilityOptions? options = null)
        => OdtOperationsImporter.Merge(document, this, options);

    private static JsonElement GetOperationsRoot(JsonElement root, out OdtOperationEnvelopeMode envelopeMode)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            envelopeMode = OdtOperationEnvelopeMode.BareArray;
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("changes", out JsonElement changes) &&
            changes.ValueKind == JsonValueKind.Array)
        {
            envelopeMode = OdtOperationEnvelopeMode.TdfChangesObject;
            return changes;
        }

        throw new JsonException(OdfLocalizer.GetMessage("Err_OdtOperationLog_InvalidEnvelope"));
    }
}

/// <summary>
/// Identifies a documented ODT JSON collaboration operation.
/// 識別已文件化的 ODT JSON collaboration operation。
/// </summary>
public enum OdtOperationKind
{
    /// <summary>
    /// The operation name is unknown or absent.
    /// operation 名稱未知或不存在。
    /// </summary>
    Unknown,

    /// <summary>
    /// Deletes text or a structural range.
    /// 刪除文字或結構範圍。
    /// </summary>
    Delete,

    /// <summary>
    /// Moves text or a structural range.
    /// 移動文字或結構範圍。
    /// </summary>
    Move,

    /// <summary>
    /// Adds a paragraph.
    /// 新增段落。
    /// </summary>
    AddParagraph,

    /// <summary>
    /// Splits a paragraph.
    /// 分割段落。
    /// </summary>
    SplitParagraph,

    /// <summary>
    /// Merges paragraphs.
    /// 合併段落。
    /// </summary>
    MergeParagraph,

    /// <summary>
    /// Adds text.
    /// 新增文字。
    /// </summary>
    AddText,

    /// <summary>
    /// Adds a tab.
    /// 新增定位字元。
    /// </summary>
    AddTab,

    /// <summary>
    /// Adds a line break.
    /// 新增換行。
    /// </summary>
    AddLineBreak,

    /// <summary>
    /// Adds or updates a field.
    /// 新增或更新欄位。
    /// </summary>
    Field,

    /// <summary>
    /// Adds or changes table content.
    /// 新增或變更表格內容。
    /// </summary>
    Table,

    /// <summary>
    /// Adds or changes list metadata.
    /// 新增或變更清單 metadata。
    /// </summary>
    List,

    /// <summary>
    /// Adds or deletes header or footer content.
    /// 新增或刪除頁首頁尾內容。
    /// </summary>
    HeaderFooter,

    /// <summary>
    /// Adds a note or comment.
    /// 新增註腳、尾註或批注。
    /// </summary>
    Note,

    /// <summary>
    /// Records document layout metadata.
    /// 記錄文件版面 metadata。
    /// </summary>
    DocumentLayout,

    /// <summary>
    /// Adds a font declaration.
    /// 新增字型宣告。
    /// </summary>
    FontDeclaration,

    /// <summary>
    /// Applies formatting.
    /// 套用格式。
    /// </summary>
    Format,

    /// <summary>
    /// Adds, changes, or deletes style metadata.
    /// 新增、變更或刪除樣式 metadata。
    /// </summary>
    Style,

    /// <summary>
    /// Adds drawing content.
    /// 新增繪圖內容。
    /// </summary>
    Drawing,
}

/// <summary>
/// Represents a single ODT JSON operation while preserving unknown wire fields.
/// 表示單一 ODT JSON operation，並保留未知 wire 欄位。
/// </summary>
public sealed class OdtOperation
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "name",
        "start",
        "end",
        "to",
        "target",
        "text",
        "attrs",
        "selection",
    };

    internal OdtOperation(
        string? name,
        int[] start,
        int[] end,
        int[] to,
        int[] target,
        string? text,
        OdtOperationKind kind,
        JsonElement? attrs,
        JsonElement? selection,
        IReadOnlyDictionary<string, JsonElement> unknownProperties,
        JsonElement rawElement,
        int sourceIndex)
    {
        Name = name;
        Start = start;
        End = end;
        To = to;
        Target = target;
        Text = text;
        Kind = kind;
        Attrs = attrs;
        Selection = selection;
        UnknownProperties = unknownProperties;
        RawElement = rawElement;
        SourceIndex = sourceIndex;
    }

    /// <summary>
    /// Gets the operation name.
    /// 取得 operation 名稱。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the start position components.
    /// 取得起始位置元件。
    /// </summary>
    public int[] Start { get; }

    /// <summary>
    /// Gets the end position components.
    /// 取得結束位置元件。
    /// </summary>
    public int[] End { get; }

    /// <summary>
    /// Gets the TDF destination position components.
    /// 取得 TDF 目的地位置元件。
    /// </summary>
    public int[] To { get; }

    /// <summary>
    /// Gets the target position components.
    /// 取得目標位置元件。
    /// </summary>
    public int[] Target { get; }

    /// <summary>
    /// Gets the text payload.
    /// 取得文字內容 payload。
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the normalized operation kind.
    /// 取得正規化後的 operation 類型。
    /// </summary>
    public OdtOperationKind Kind { get; }

    /// <summary>
    /// Gets the attributes payload.
    /// 取得屬性 payload。
    /// </summary>
    public JsonElement? Attrs { get; }

    /// <summary>
    /// Gets the selection payload.
    /// 取得 selection payload。
    /// </summary>
    public JsonElement? Selection { get; }

    /// <summary>
    /// Gets unknown wire properties preserved for round-trip.
    /// 取得為 round-trip 保留的未知 wire 欄位。
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> UnknownProperties { get; }

    /// <summary>
    /// Gets the original JSON object for compatibility replay.
    /// 取得供相容性重播使用的原始 JSON 物件。
    /// </summary>
    public JsonElement RawElement { get; }

    /// <summary>
    /// Gets the zero-based source index in the parsed operation log.
    /// 取得已剖析 operation log 中以零為基底的來源索引。
    /// </summary>
    public int SourceIndex { get; }

    internal static OdtOperation Parse(JsonElement element, int sourceIndex, OdtOperationSafetyOptions safety)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(OdfLocalizer.GetMessage("Err_OdtOperationLog_OperationMustBeObject", sourceIndex));
        }

        string? name = element.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        int[] start = ReadPosition(element, "start", safety);
        int[] end = ReadPosition(element, "end", safety);
        int[] to = ReadPosition(element, "to", safety);
        int[] target = ReadPosition(element, "target", safety);
        string? text = element.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String
            ? textElement.GetString()
            : null;
        if (text is not null && text.Length > safety.MaxTextLength)
        {
            throw new JsonException(OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "textLength",
                text.Length,
                safety.MaxTextLength));
        }

        JsonElement? attrs = null;
        if (element.TryGetProperty("attrs", out JsonElement attrsElement))
        {
            string rawAttrs = attrsElement.GetRawText();
            if (rawAttrs.Length > safety.MaxAttributesLength)
            {
                throw new JsonException(OdfLocalizer.GetMessage(
                    "Err_OdtOperationLog_SafetyLimitExceeded",
                    "attrsLength",
                    rawAttrs.Length,
                    safety.MaxAttributesLength));
            }

            attrs = attrsElement.Clone();
            ValidateAttributePayloadSize(attrsElement, name, safety);
        }

        JsonElement? selection = element.TryGetProperty("selection", out JsonElement selectionElement)
            ? selectionElement.Clone()
            : null;
        if (selection is JsonElement parsedSelection)
        {
            string rawSelection = parsedSelection.GetRawText();
            if (rawSelection.Length > safety.MaxSelectionLength)
            {
                throw new JsonException(OdfLocalizer.GetMessage(
                    "Err_OdtOperationLog_SafetyLimitExceeded",
                    "selectionLength",
                    rawSelection.Length,
                    safety.MaxSelectionLength));
            }
        }

        var unknown = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!KnownProperties.Contains(property.Name))
            {
                unknown[property.Name] = property.Value.Clone();
            }
        }

        return new OdtOperation(name, start, end, to, target, text, GetKind(name), attrs, selection, unknown, element.Clone(), sourceIndex);
    }

    /// <summary>
    /// Attempts to get a string attribute from the attrs payload.
    /// 嘗試從 attrs payload 取得字串屬性。
    /// </summary>
    /// <param name="name">The attribute name. / 屬性名稱。</param>
    /// <param name="value">The parsed value. / 剖析出的值。</param>
    /// <returns><see langword="true"/> when the attribute exists as a non-empty string. / 屬性存在且為非空字串時傳回 <see langword="true"/>。</returns>
    public bool TryGetStringAttribute(string name, out string? value)
    {
        if (Attrs is JsonElement attrs &&
            attrs.ValueKind == JsonValueKind.Object &&
            attrs.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return !string.IsNullOrEmpty(value);
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempts to get an integer attribute from the attrs payload.
    /// 嘗試從 attrs payload 取得整數屬性。
    /// </summary>
    /// <param name="name">The attribute name. / 屬性名稱。</param>
    /// <param name="value">The parsed value. / 剖析出的值。</param>
    /// <returns><see langword="true"/> when the attribute exists as an integer. / 屬性存在且為整數時傳回 <see langword="true"/>。</returns>
    public bool TryGetInt32Attribute(string name, out int value)
    {
        if (Attrs is JsonElement attrs &&
            attrs.ValueKind == JsonValueKind.Object &&
            attrs.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    internal void WriteTo(Utf8JsonWriter writer, bool preserveUnknownProperties)
    {
        writer.WriteStartObject();
        if (Name is not null)
        {
            writer.WriteString("name", Name);
        }

        WritePosition(writer, "start", Start);
        WritePosition(writer, "end", End);
        WritePosition(writer, "to", To);
        WritePosition(writer, "target", Target);
        if (Text is not null)
        {
            writer.WriteString("text", Text);
        }

        if (Attrs is JsonElement attrs)
        {
            writer.WritePropertyName("attrs");
            attrs.WriteTo(writer);
        }

        if (Selection is JsonElement selection)
        {
            writer.WritePropertyName("selection");
            selection.WriteTo(writer);
        }

        if (preserveUnknownProperties)
        {
            foreach (KeyValuePair<string, JsonElement> property in UnknownProperties)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static int[] ReadPosition(JsonElement element, string propertyName, OdtOperationSafetyOptions safety)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement position) || position.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<int>();
        foreach (JsonElement component in position.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Number || !component.TryGetInt32(out int value))
            {
                throw new JsonException(OdfLocalizer.GetMessage("Err_OdtOperationLog_InvalidPosition", propertyName));
            }

            if (value < 0 || value > safety.MaxPositionComponent)
            {
                throw new JsonException(OdfLocalizer.GetMessage(
                    "Err_OdtOperationLog_SafetyLimitExceeded",
                    propertyName,
                    value,
                    safety.MaxPositionComponent));
            }

            values.Add(value);
        }

        return values.ToArray();
    }

    private static OdtOperationKind GetKind(string? name) => name switch
    {
        "delete" => OdtOperationKind.Delete,
        "move" => OdtOperationKind.Move,
        "addParagraph" => OdtOperationKind.AddParagraph,
        "splitParagraph" => OdtOperationKind.SplitParagraph,
        "mergeParagraph" => OdtOperationKind.MergeParagraph,
        "addText" => OdtOperationKind.AddText,
        "addTab" => OdtOperationKind.AddTab,
        "addLineBreak" => OdtOperationKind.AddLineBreak,
        "addField" or "updateField" => OdtOperationKind.Field,
        "addTable" or "addRows" or "addCells" or "addColumn" or "addColumns" or "deleteColumns" => OdtOperationKind.Table,
        "addListStyle" => OdtOperationKind.List,
        "addHeaderFooter" or "deleteHeaderFooter" or "deleteHeaderFooterContent" => OdtOperationKind.HeaderFooter,
        "addNote" => OdtOperationKind.Note,
        "documentLayout" => OdtOperationKind.DocumentLayout,
        "addFontDecl" => OdtOperationKind.FontDeclaration,
        "format" => OdtOperationKind.Format,
        "addStyle" or "changeStyle" or "deleteStyle" => OdtOperationKind.Style,
        "addDrawing" => OdtOperationKind.Drawing,
        _ => OdtOperationKind.Unknown,
    };

    private static void ValidateAttributePayloadSize(JsonElement attrs, string? operationName, OdtOperationSafetyOptions safety)
    {
        if (string.Equals(operationName, "addDrawing", StringComparison.Ordinal) &&
            attrs.GetRawText().Length > safety.MaxDrawingAttributesLength)
        {
            throw new JsonException(OdfLocalizer.GetMessage(
                "Err_OdtOperationLog_SafetyLimitExceeded",
                "drawingAttrsLength",
                attrs.GetRawText().Length,
                safety.MaxDrawingAttributesLength));
        }

        ValidateAttributePayloadSizeRecursive(attrs, safety);
    }

    private static void ValidateAttributePayloadSizeRecursive(JsonElement element, OdtOperationSafetyOptions safety)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    ValidateAttributePayloadSizeRecursive(property.Value, safety);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ValidateAttributePayloadSizeRecursive(item, safety);
                }

                break;
            case JsonValueKind.String:
                string? value = element.GetString();
                if (value is not null && IsBinaryLike(value) && value.Length > safety.MaxBinaryLikeAttributeLength)
                {
                    throw new JsonException(OdfLocalizer.GetMessage(
                        "Err_OdtOperationLog_SafetyLimitExceeded",
                        "binaryLikeAttrs",
                        value.Length,
                        safety.MaxBinaryLikeAttributeLength));
                }

                break;
        }
    }

    private static bool IsBinaryLike(string value)
    {
        if (value.Length < 128)
        {
            return false;
        }

        int base64Chars = 0;
        foreach (char c in value)
        {
            if ((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c is '+' or '/' or '=')
            {
                base64Chars++;
            }
        }

        return base64Chars >= value.Length * 9 / 10;
    }

    private static void WritePosition(Utf8JsonWriter writer, string propertyName, int[] values)
    {
        if (values.Length == 0)
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (int value in values)
        {
            writer.WriteNumberValue(value);
        }

        writer.WriteEndArray();
    }
}
