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
        if (rootNode is null) throw new ArgumentNullException(nameof(rootNode));
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= OdfSaveOptions.Default;

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false), // 不含位元組順序標記 (BOM) 的 UTF-8
            Indent = options.IndentXml,
            CloseOutput = false
        };

        // 1. 預先掃描 DOM 樹以收集所有命名空間及其前綴
        Dictionary<string, string> nsDict = new(StringComparer.Ordinal);
        CollectNamespaces(rootNode, nsDict);

        // 確保標準 XML 命名空間不會被明確宣告為 xmlns:xml
        nsDict.Remove("http://www.w3.org/XML/1998/namespace");

        using (var writer = XmlWriter.Create(stream, settings))
        {
            int openElementsCount = 0;
            try
            {
                writer.WriteStartDocument();
                
                // 遞迴寫入
                WriteNode(rootNode, writer, nsDict, ref openElementsCount, isRoot: true);

                writer.WriteEndDocument();
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Exception during XML serialization: {ex.Message}. Force-closing open tags to salvage XML structure.", ex);
                try
                {
                    // 自動關閉任何未關閉的標籤以保持 XML 結構有效
                    while (openElementsCount > 0)
                    {
                        writer.WriteEndElement();
                        openElementsCount--;
                    }
                    writer.Flush();
                }
                catch
                {
                    // 忽略救援嘗試時的次要錯誤
                }
                throw;
            }
        }
    }

    private static void WriteNode(OdfNode node, XmlWriter writer, Dictionary<string, string> nsDict, ref int openElementsCount, bool isRoot)
    {
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

        // 寫入開始元素
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

        // 如果是根元素，則在最上層宣告所有收集到的命名空間
        if (isRoot)
        {
            foreach (var ns in nsDict)
            {
                if (string.IsNullOrEmpty(ns.Value))
                {
                    // 預設命名空間宣告
                    writer.WriteAttributeString("xmlns", ns.Key);
                }
                else
                {
                    // 前綴命名空間宣告 (xmlns:prefix="uri")
                    writer.WriteAttributeString("xmlns", ns.Value, "http://www.w3.org/2000/xmlns/", ns.Key);
                }
            }
        }

        // 寫入屬性
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

        // 寫入子節點
        foreach (var child in node.Children)
        {
            WriteNode(child, writer, nsDict, ref openElementsCount, isRoot: false);
        }

        // 寫入結束元素
        writer.WriteEndElement();
        openElementsCount--;
    }

    private static string GetNamespacePrefix(string nsUri, string? preferredPrefix, Dictionary<string, string> nsDict)
    {
        if (string.IsNullOrEmpty(nsUri)) return string.Empty;
        if (nsDict.TryGetValue(nsUri, out string? prefix))
        {
            return prefix;
        }
        return preferredPrefix ?? string.Empty;
    }

    private static void CollectNamespaces(OdfNode node, Dictionary<string, string> nsDict)
    {
        if (node.NodeType != OdfNodeType.Element) return;

        // 收集元素的命名空間
        if (!string.IsNullOrEmpty(node.NamespaceUri))
        {
            if (!nsDict.ContainsKey(node.NamespaceUri))
            {
                // 後備使用標準前綴，接著是節點的前綴，最後產生一個
                string prefix = OdfNamespaces.GetPrefix(node.NamespaceUri);
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = node.Prefix ?? $"ns{nsDict.Count + 1}";
                }
                nsDict[node.NamespaceUri] = prefix;
            }
        }

        // 收集屬性的命名空間
        foreach (var attr in node.Attributes.Keys)
        {
            if (!string.IsNullOrEmpty(attr.NamespaceUri))
            {
                if (!nsDict.ContainsKey(attr.NamespaceUri))
                {
                    string prefix = OdfNamespaces.GetPrefix(attr.NamespaceUri);
                    if (string.IsNullOrEmpty(prefix))
                    {
                        prefix = node.GetAttributePrefix(attr) ?? $"ns{nsDict.Count + 1}";
                    }
                    nsDict[attr.NamespaceUri] = prefix;
                }
            }
        }

        // 遞迴子節點
        foreach (var child in node.Children)
        {
            CollectNamespaces(child, nsDict);
        }
    }
}
