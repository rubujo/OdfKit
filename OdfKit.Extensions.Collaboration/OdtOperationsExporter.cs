using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Collaboration;

/// <summary>
/// Exports ODT tracked changes as portable operation logs.
/// 將 <see cref="TextDocument"/> 匯出為 ODF Toolkit 相容的 JSON operations 序列。
/// </summary>
public static class OdtOperationsExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// Exports the document body as a JSON operation log.
    /// 將文字文件本文匯出為 JSON operations 陣列字串。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <returns>The result. / JSON operations 陣列</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="document"/> 為 null 時擲出</exception>
    public static string ExportToJson(TextDocument document) => ExportToJson(document, null);

    /// <summary>
    /// Exports the document body as a typed operation log.
    /// 將文字文件本文匯出為 typed operation log。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <param name="options">The value to use. / ODF Toolkit 相容選項；若為 <see langword="null"/>，則使用裸陣列輸出</param>
    /// <returns>The result. / typed operation log</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="document"/> 為 null 時擲出</exception>
    public static OdtOperationLog Export(TextDocument document, OdtOperationCompatibilityOptions? options = null)
    {
        options ??= new OdtOperationCompatibilityOptions();
        string json = ExportToJson(document, options);
        return OdtOperationLog.Parse(json, options);
    }

    /// <summary>
    /// Exports the document body as a JSON operation log.
    /// 將文字文件本文匯出為 JSON operations 字串。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <param name="options">The value to use. / ODF Toolkit 相容選項；若為 <see langword="null"/>，則使用裸陣列輸出</param>
    /// <returns>The result. / JSON operations 或 TDF changes 封包</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="document"/> 為 null 時擲出</exception>
    public static string ExportToJson(TextDocument document, OdtOperationCompatibilityOptions? options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));

        options ??= new OdtOperationCompatibilityOptions();
        ValidateSafety(options.Safety);
        List<OdtOperation> operations = [];
        int bodyIndex = 0;

        // 段落、標題、清單（含巢狀）、表格與區段內容都要輸出；先前只輸出最上層的 p／h，
        // 真實文件裡的清單項目與表格內文會整個遺失。
        void VisitBlocks(OdfNode container, string? listStyle, int listLevel)
        {
            foreach (OdfNode child in container.Children)
            {
                if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text &&
                    child.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                if (child.NamespaceUri == OdfNamespaces.Table)
                {
                    if (child.LocalName == "table")
                    {
                        ExportTable(child, operations, options.Safety);
                    }

                    continue;
                }

                switch (child.LocalName)
                {
                    case "p" or "h":
                        operations.Add(new OdtOperation
                        {
                            Name = "addParagraph",
                            Start = [bodyIndex],
                            Attrs = BuildParagraphAttributes(child, listStyle, listLevel),
                        });
                        EnsureOperationCount(operations, options.Safety);

                        int characterIndex = 0;
                        AppendTextOperations(child, bodyIndex, ref characterIndex, operations, options.Safety);
                        bodyIndex++;
                        break;
                    case "list":
                        string? style = child.GetAttribute("style-name", OdfNamespaces.Text) ?? listStyle ?? "OdfKitList";
                        foreach (OdfNode item in child.Children)
                        {
                            if (item.NodeType == OdfNodeType.Element &&
                                item.NamespaceUri == OdfNamespaces.Text &&
                                item.LocalName is "list-item" or "list-header")
                            {
                                VisitBlocks(item, style, listLevel + 1);
                            }
                        }

                        break;
                    case "section":
                        VisitBlocks(child, listStyle, listLevel);
                        break;
                }
            }
        }

        VisitBlocks(document.BodyTextRoot, null, 0);

        string json = options.EnvelopeMode == OdtOperationEnvelopeMode.TdfChangesObject
            ? JsonSerializer.Serialize(new OdtOperationEnvelope(operations), SerializerOptions)
            : JsonSerializer.Serialize(operations, SerializerOptions);
        if (json.Length > options.Safety.MaxJsonLength)
            throw new InvalidOperationException();
        return json;
    }

    private static Dictionary<string, object>? BuildParagraphAttributes(OdfNode paragraphNode, string? listStyle, int listLevel)
    {
        string? styleName = paragraphNode.GetAttribute("style-name", OdfNamespaces.Text);
        string? outline = paragraphNode.LocalName == "h" ? paragraphNode.GetAttribute("outline-level", OdfNamespaces.Text) : null;
        if (string.IsNullOrEmpty(styleName) && listStyle is null && string.IsNullOrEmpty(outline))
        {
            return null;
        }

        var attributes = new Dictionary<string, object>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(styleName))
        {
            attributes["styleName"] = styleName!;
        }

        if (listStyle is not null)
        {
            attributes["listStyleName"] = listStyle;
            attributes["listLevel"] = Math.Max(1, listLevel);
        }

        if (int.TryParse(outline, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int outlineLevel) && outlineLevel > 0)
        {
            attributes["outlineLevel"] = outlineLevel;
        }

        return attributes;
    }

    // 表格輸出成 addTable（列數、欄數）與每列一個 addCells（儲存格文字）；合併儲存格的被涵蓋格輸出為空字串。
    private static void ExportTable(OdfNode table, List<OdtOperation> operations, OdtOperationSafetyOptions safety)
    {
        int columns = 0;
        var rows = new List<List<string>>();

        void CollectColumns(OdfNode parent)
        {
            foreach (OdfNode child in parent.Children)
            {
                if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                if (child.LocalName == "table-column")
                {
                    columns = checked(columns + ReadRepeat(child, "number-columns-repeated", safety.MaxTableColumns));
                }
                else if (child.LocalName is "table-columns" or "table-header-columns" or "table-column-group")
                {
                    CollectColumns(child);
                }
            }
        }

        void CollectRows(OdfNode parent)
        {
            foreach (OdfNode child in parent.Children)
            {
                if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                if (child.LocalName == "table-row")
                {
                    var values = new List<string>();
                    foreach (OdfNode cell in child.Children)
                    {
                        if (cell.NodeType != OdfNodeType.Element ||
                            cell.NamespaceUri != OdfNamespaces.Table ||
                            cell.LocalName is not ("table-cell" or "covered-table-cell"))
                        {
                            continue;
                        }

                        string text = cell.LocalName == "covered-table-cell" ? string.Empty : ReadCellText(cell);
                        int repeat = ReadRepeat(cell, "number-columns-repeated", safety.MaxTableColumns);
                        for (int i = 0; i < repeat && values.Count < safety.MaxTableColumns; i++)
                        {
                            values.Add(text);
                        }
                    }

                    // LibreOffice 對有欄格式的資料會寫到 1,048,576 列的空白重複列；空列只保留一列，尾端空列去掉。
                    int rowRepeat = ReadRepeat(child, "number-rows-repeated", safety.MaxTableRows);
                    bool empty = values.All(static value => value.Length == 0);
                    for (int i = 0; i < (empty ? Math.Min(rowRepeat, 1) : rowRepeat) && rows.Count < safety.MaxTableRows; i++)
                    {
                        rows.Add(values);
                    }
                }
                else if (child.LocalName is "table-rows" or "table-header-rows" or "table-row-group")
                {
                    CollectRows(child);
                }
            }
        }

        CollectColumns(table);
        CollectRows(table);
        while (rows.Count > 1 && rows[rows.Count - 1].All(static value => value.Length == 0))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        columns = Math.Max(columns, rows.Count == 0 ? 0 : rows.Max(static row => row.Count));
        if (rows.Count == 0 || columns == 0)
        {
            return;
        }

        if (rows.Count > safety.MaxTableRows || columns > safety.MaxTableColumns || (long)rows.Count * columns > safety.MaxTableCells)
        {
            throw new InvalidOperationException();
        }

        operations.Add(new OdtOperation
        {
            Name = "addTable",
            Start = [],
            Attrs = new Dictionary<string, object> { ["rows"] = rows.Count, ["columns"] = columns },
        });
        EnsureOperationCount(operations, safety);
        for (int row = 0; row < rows.Count; row++)
        {
            if (rows[row].All(static value => value.Length == 0))
            {
                continue;
            }

            operations.Add(new OdtOperation
            {
                Name = "addCells",
                Start = [],
                Attrs = new Dictionary<string, object>
                {
                    ["row"] = row,
                    ["column"] = 0,
                    ["values"] = rows[row].ToArray(),
                },
            });
            EnsureOperationCount(operations, safety);
        }
    }

    private static int ReadRepeat(OdfNode node, string attribute, int limit)
    {
        string? text = node.GetAttribute(attribute, OdfNamespaces.Table);
        return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) && value > 1
            ? Math.Min(value, Math.Max(1, limit))
            : 1;
    }

    private static string ReadCellText(OdfNode cell)
    {
        var paragraphs = new List<string>();
        void Collect(OdfNode parent)
        {
            foreach (OdfNode child in parent.Children)
            {
                if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text)
                {
                    continue;
                }

                if (child.LocalName is "p" or "h")
                {
                    paragraphs.Add(child.TextContent ?? string.Empty);
                }
                else if (child.LocalName is "list" or "list-item" or "section")
                {
                    Collect(child);
                }
            }
        }

        Collect(cell);
        return string.Join(" ", paragraphs.Where(static text => text.Length > 0));
    }

    private static void AppendTextOperations(
        OdfNode paragraphNode,
        int bodyIndex,
        ref int characterIndex,
        List<OdtOperation> operations,
        OdtOperationSafetyOptions safety)
    {
        foreach (OdfNode child in paragraphNode.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendAddTextOperation(bodyIndex, ref characterIndex, child.TextContent ?? string.Empty, operations, safety);
                continue;
            }

            if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            if (child.LocalName == "span")
            {
                AppendTextOperations(child, bodyIndex, ref characterIndex, operations, safety);
            }
            else if (child.LocalName == "s")
            {
                string? countAttr = child.GetAttribute("c", OdfNamespaces.Text);
                int count = int.TryParse(countAttr, out int parsed) && parsed > 0 ? parsed : 1;
                if (count > safety.MaxTextLength)
                    throw new InvalidOperationException();
                AppendAddTextOperation(bodyIndex, ref characterIndex, new string(' ', count), operations, safety);
            }
            else if (child.LocalName == "tab")
            {
                operations.Add(new OdtOperation
                {
                    Name = "addTab",
                    Start = [bodyIndex, characterIndex],
                });
                EnsureOperationCount(operations, safety);
                characterIndex++;
            }
            else if (child.LocalName == "line-break")
            {
                operations.Add(new OdtOperation
                {
                    Name = "addLineBreak",
                    Start = [bodyIndex, characterIndex],
                });
                EnsureOperationCount(operations, safety);
                characterIndex++;
            }
            else if (!string.IsNullOrEmpty(child.TextContent))
            {
                AppendAddTextOperation(bodyIndex, ref characterIndex, child.TextContent ?? string.Empty, operations, safety);
            }
        }

        if (characterIndex == 0 && !string.IsNullOrEmpty(paragraphNode.TextContent))
        {
            AppendAddTextOperation(bodyIndex, ref characterIndex, paragraphNode.TextContent, operations, safety);
        }
    }

    private static void AppendAddTextOperation(
        int bodyIndex,
        ref int characterIndex,
        string text,
        List<OdtOperation> operations,
        OdtOperationSafetyOptions safety)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (text.Length > safety.MaxTextLength)
            throw new InvalidOperationException();

        operations.Add(new OdtOperation
        {
            Name = "addText",
            Start = [bodyIndex, characterIndex],
            Text = text,
        });
        EnsureOperationCount(operations, safety);
        characterIndex = checked(characterIndex + text.Length);
        if (characterIndex > safety.MaxPositionComponent)
            throw new InvalidOperationException();
    }

    private static void EnsureOperationCount(List<OdtOperation> operations, OdtOperationSafetyOptions safety)
    {
        if (operations.Count > safety.MaxOperationCount)
            throw new InvalidOperationException();
    }

    private static void ValidateSafety(OdtOperationSafetyOptions safety)
    {
        if (safety.MaxJsonLength < 1 ||
            safety.MaxOperationCount < 1 ||
            safety.MaxTextLength < 1 ||
            safety.MaxPositionComponent < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(safety));
        }
    }

    private sealed class OdtOperation
    {
        public string Name { get; set; } = string.Empty;

        public int[] Start { get; set; } = [];

        public string? Text { get; set; }

        public Dictionary<string, object>? Attrs { get; set; }
    }

    private sealed class OdtOperationEnvelope(List<OdtOperation> changes)
    {
        public List<OdtOperation> Changes { get; } = changes;
    }
}
