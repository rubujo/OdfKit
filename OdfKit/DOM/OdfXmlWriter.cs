using System.Security;
using System.Text;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 提供將 ODF DOM 節點樹序列化為 XML 格式並寫入串流之功能的類別。
/// </summary>
public static class OdfXmlWriter
{
    /// <summary>
    /// 將記憶體 DOM 節點樹序列化寫入 XML 檔案流。
    /// </summary>
    /// <param name="rootNode">要寫入的根節點</param>
    /// <param name="stream">要寫入的輸出串流</param>
    /// <param name="options">儲存選項；如果為 <see langword="null"/>，則使用預設選項</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="rootNode"/> 或 <paramref name="stream"/> 為 <see langword="null"/> 時擲出</exception>
    public static void Write(OdfNode rootNode, Stream stream, OdfSaveOptions? options = null)
    {
        if (rootNode is null)
            throw new ArgumentNullException(nameof(rootNode));
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        options ??= OdfSaveOptions.Default;

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false), // 不含位元組順序標記 (BOM) 的 UTF-8
            Indent = options.IndentXml,
            CloseOutput = false
        };

        Dictionary<string, string> nsDict = new(StringComparer.Ordinal);
        CollectNamespaces(rootNode, nsDict);

        // 確保標準 XML 命名空間不會被重複宣告為 xmlns:xml
        nsDict.Remove("http://www.w3.org/XML/1998/namespace");

        using (var writer = XmlWriter.Create(stream, settings))
        {
            int openElementsCount = 0;
            try
            {
                writer.WriteStartDocument();

                // 單次走訪寫入；命名空間於根元素一次宣告（PERF-4j：避免子元素插入 xmlns 破壞屬性順序）
                WriteNode(rootNode, writer, nsDict, ref openElementsCount, isRoot: true, depth: 1);

                writer.WriteEndDocument();
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Exception during XML serialization: {ex.Message}. Force-closing open tags to salvage XML structure.", ex);
                try
                {
                    while (openElementsCount > 0)
                    {
                        writer.WriteEndElement();
                        openElementsCount--;
                    }

                    writer.Flush();
                }
                catch (Exception salvageEx)
                {
                    OdfKitDiagnostics.Warn($"XML 序列化救援關閉標籤時發生次要錯誤：{salvageEx.Message}", salvageEx);
                }

                throw;
            }
        }
    }

    internal static void WriteNode(
        OdfNode node,
        XmlWriter writer,
        Dictionary<string, string> nsDict,
        ref int openElementsCount,
        bool isRoot,
        int depth)
    {
        if (depth > OdfXmlReader.MaxElementDepth)
        {
            throw new SecurityException(
                $"XML element nesting depth limit exceeded during serialization ({depth} > {OdfXmlReader.MaxElementDepth}).");
        }

        if (node.NodeType == OdfNodeType.Comment)
        {
            writer.WriteComment(node.TextContent);
            return;
        }

        if (node.NodeType == OdfNodeType.ProcessingInstruction)
        {
            writer.WriteProcessingInstruction(node.LocalName, node.TextContent);
            return;
        }

        if (node.NodeType == OdfNodeType.Text)
        {
            writer.WriteString(node.TextContent);
            return;
        }

        if (node.TryWriteOverride(writer, nsDict))
        {
            return;
        }

        string prefix = GetNamespacePrefix(node.NamespaceUri, node.Prefix, nsDict);
        if (!string.IsNullOrEmpty(node.NamespaceUri))
        {
            writer.WriteStartElement(prefix, node.LocalName, node.NamespaceUri);
        }
        else
        {
            writer.WriteStartElement(node.LocalName);
        }

        openElementsCount++;

        if (isRoot)
        {
            foreach (var ns in nsDict)
            {
                if (string.IsNullOrEmpty(ns.Value))
                {
                    writer.WriteAttributeString("xmlns", ns.Key);
                }
                else
                {
                    writer.WriteAttributeString("xmlns", ns.Value, "http://www.w3.org/2000/xmlns/", ns.Key);
                }
            }
        }

        foreach (var attr in node.Attributes)
        {
            string attrPrefix = GetNamespacePrefix(attr.Key.NamespaceUri, node.GetAttributePrefix(attr.Key), nsDict);
            if (!string.IsNullOrEmpty(attr.Key.NamespaceUri))
            {
                writer.WriteAttributeString(attrPrefix, attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value);
            }
            else
            {
                writer.WriteAttributeString(attr.Key.LocalName, attr.Value);
            }
        }

        if (node.TryWriteLazyXml(writer))
        {
            writer.WriteEndElement();
            openElementsCount--;
            return;
        }

        foreach (var child in node.Children)
        {
            WriteNode(child, writer, nsDict, ref openElementsCount, isRoot: false, depth: depth + 1);
        }

        writer.WriteEndElement();
        openElementsCount--;
    }

    private static string GetNamespacePrefix(string? nsUri, string? preferredPrefix, Dictionary<string, string> nsDict)
    {
        if (nsUri is not { Length: > 0 })
        {
            return string.Empty;
        }

        if (nsDict.TryGetValue(nsUri, out string? prefix))
        {
            return prefix;
        }

        return preferredPrefix ?? string.Empty;
    }

    private static void CollectNamespaces(OdfNode node, Dictionary<string, string> nsDict)
    {
        var stack = new Stack<OdfNode>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            OdfNode current = stack.Pop();
            if (current.NodeType != OdfNodeType.Element)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(current.NamespaceUri) &&
                !nsDict.ContainsKey(current.NamespaceUri))
            {
                string prefix = OdfNamespaces.GetPrefix(current.NamespaceUri);
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = current.Prefix ?? $"ns{nsDict.Count + 1}";
                }

                nsDict[current.NamespaceUri] = prefix;
            }

            foreach (var attrEntry in current.Attributes)
            {
                OdfAttributeName attr = attrEntry.Key;
                if (!string.IsNullOrEmpty(attr.NamespaceUri) &&
                    !nsDict.ContainsKey(attr.NamespaceUri))
                {
                    string prefix = OdfNamespaces.GetPrefix(attr.NamespaceUri);
                    if (string.IsNullOrEmpty(prefix))
                    {
                        prefix = current.GetAttributePrefix(attr) ?? $"ns{nsDict.Count + 1}";
                    }

                    nsDict[attr.NamespaceUri] = prefix;
                }

                // 公式／條件式屬性值內可帶有 "of:" 前綴（OpenFormula，用於 table:formula="of:=..." 與
                // table:condition="of:cell-content-is-..."）或 "oooc:" 前綴（OpenOffice.org Calc 相容
                // 公式，例如 table:formula="oooc:=..."）。這些前綴只存在於屬性值字串內，不屬於 XML
                // 結構化命名空間限定名稱，須額外掃描屬性值才能正確補上對應的 xmlns 宣告。
                // 注意："of:" 不可只比對 "of:="（公式賦值語法）：table:condition 內的條件函式（例如
                // cell-content-is-decimal-number()、cell-content-is-in-list(...)）同樣以 "of:" 開頭
                // 但沒有等號，曾經因為只檢查 "of:=" 而在「文件內沒有任何公式格、只有驗證規則」時遺漏宣告，
                // 造成 LibreOffice 載入時整條驗證規則被靜默捨棄。
                if (attrEntry.Value is { Length: > 3 } value)
                {
                    if (!nsDict.ContainsKey(OdfNamespaces.Of) &&
                        value.StartsWith("of:", System.StringComparison.Ordinal))
                    {
                        nsDict[OdfNamespaces.Of] = "of";
                    }

                    if (!nsDict.ContainsKey(OdfNamespaces.Oooc) &&
                        value.Contains("oooc:", System.StringComparison.Ordinal))
                    {
                        nsDict[OdfNamespaces.Oooc] = "oooc";
                    }
                }
            }

            if (current._isLazy && current.Children.LoadedCount == 0)
            {
                continue;
            }

            for (int i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }
}
