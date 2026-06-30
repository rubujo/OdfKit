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
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        options ??= new OdtOperationCompatibilityOptions();
        List<OdtOperation> operations = [];
        int bodyIndex = 0;

        foreach (OdfNode child in document.BodyTextRoot.Children)
        {
            if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            if (child.LocalName is "p" or "h")
            {
                operations.Add(new OdtOperation
                {
                    Name = "addParagraph",
                    Start = [bodyIndex],
                    Attrs = BuildParagraphAttributes(child),
                });

                int characterIndex = 0;
                AppendTextOperations(child, bodyIndex, ref characterIndex, operations);
                bodyIndex++;
            }
        }

        if (options.EnvelopeMode == OdtOperationEnvelopeMode.TdfChangesObject)
        {
            return JsonSerializer.Serialize(new OdtOperationEnvelope(operations), SerializerOptions);
        }

        return JsonSerializer.Serialize(operations, SerializerOptions);
    }

    private static Dictionary<string, object>? BuildParagraphAttributes(OdfNode paragraphNode)
    {
        string? styleName = paragraphNode.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrEmpty(styleName))
        {
            return null;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["styleName"] = styleName!,
        };
    }

    private static void AppendTextOperations(
        OdfNode paragraphNode,
        int bodyIndex,
        ref int characterIndex,
        List<OdtOperation> operations)
    {
        foreach (OdfNode child in paragraphNode.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendAddTextOperation(bodyIndex, ref characterIndex, child.TextContent ?? string.Empty, operations);
                continue;
            }

            if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            if (child.LocalName == "span")
            {
                AppendTextOperations(child, bodyIndex, ref characterIndex, operations);
            }
            else if (child.LocalName == "s")
            {
                string? countAttr = child.GetAttribute("c", OdfNamespaces.Text);
                int count = int.TryParse(countAttr, out int parsed) && parsed > 0 ? parsed : 1;
                AppendAddTextOperation(bodyIndex, ref characterIndex, new string(' ', count), operations);
            }
            else if (child.LocalName == "tab")
            {
                operations.Add(new OdtOperation
                {
                    Name = "addTab",
                    Start = [bodyIndex, characterIndex],
                });
                characterIndex++;
            }
            else if (child.LocalName == "line-break")
            {
                operations.Add(new OdtOperation
                {
                    Name = "addLineBreak",
                    Start = [bodyIndex, characterIndex],
                });
                characterIndex++;
            }
            else if (!string.IsNullOrEmpty(child.TextContent))
            {
                AppendAddTextOperation(bodyIndex, ref characterIndex, child.TextContent ?? string.Empty, operations);
            }
        }

        if (characterIndex == 0 && !string.IsNullOrEmpty(paragraphNode.TextContent))
        {
            AppendAddTextOperation(bodyIndex, ref characterIndex, paragraphNode.TextContent, operations);
        }
    }

    private static void AppendAddTextOperation(
        int bodyIndex,
        ref int characterIndex,
        string text,
        List<OdtOperation> operations)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        operations.Add(new OdtOperation
        {
            Name = "addText",
            Start = [bodyIndex, characterIndex],
            Text = text,
        });
        characterIndex += text.Length;
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
