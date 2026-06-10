using System.Text;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.DOM
{
    public static class OdfXmlWriter
    {
        /// <summary>
        /// 將記憶體 DOM 節點樹序列化寫入 XML 檔案流。
        /// </summary>
        public static void Write(OdfNode rootNode, Stream stream, OdfSaveOptions? options = null)
        {
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            options ??= OdfSaveOptions.Default;

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                Indent = options.IndentXml,
                CloseOutput = false
            };

            // 1. Pre-scan DOM tree to collect all namespaces and their prefixes
            var nsDict = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectNamespaces(rootNode, nsDict);

            // Ensure standard XML namespace is not explicitly declared as xmlns:xml
            nsDict.Remove("http://www.w3.org/XML/1998/namespace");

            using (var writer = XmlWriter.Create(stream, settings))
            {
                int openElementsCount = 0;
                try
                {
                    writer.WriteStartDocument();
                    
                    // Recursive write
                    WriteNode(rootNode, writer, nsDict, ref openElementsCount, isRoot: true);

                    writer.WriteEndDocument();
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Exception during XML serialization: {ex.Message}. Force-closing open tags to salvage XML structure.", ex);
                    try
                    {
                        // Auto-close any unclosed tags to keep XML structure valid
                        while (openElementsCount > 0)
                        {
                            writer.WriteEndElement();
                            openElementsCount--;
                        }
                        writer.Flush();
                    }
                    catch
                    {
                        // Ignore secondary errors on salvage attempt
                    }
                    throw;
                }
            }
        }

        private static void WriteNode(OdfNode node, XmlWriter writer, Dictionary<string, string> nsDict, ref int openElementsCount, bool isRoot)
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                writer.WriteString(node.TextContent);
                return;
            }

            // Write start element
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

            // If it is the root element, declare all collected namespaces at the top level
            if (isRoot)
            {
                foreach (var ns in nsDict)
                {
                    if (string.IsNullOrEmpty(ns.Value))
                    {
                        // Default namespace declaration
                        writer.WriteAttributeString("xmlns", ns.Key);
                    }
                    else
                    {
                        // Prefix namespace declaration (xmlns:prefix="uri")
                        writer.WriteAttributeString("xmlns", ns.Value, "http://www.w3.org/2000/xmlns/", ns.Key);
                    }
                }
            }

            // Write attributes
            foreach (var attr in node.Attributes)
            {
                string attrPrefix = GetNamespacePrefix(attr.Key.NamespaceUri, null, nsDict);
                if (!string.IsNullOrEmpty(attr.Key.NamespaceUri))
                {
                    writer.WriteAttributeString(attrPrefix, attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value);
                }
                else
                {
                    writer.WriteAttributeString(attr.Key.LocalName, attr.Value);
                }
            }

            // Write children
            foreach (var child in node.Children)
            {
                WriteNode(child, writer, nsDict, ref openElementsCount, isRoot: false);
            }

            // Write end element
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

            // Collect namespace of the element
            if (!string.IsNullOrEmpty(node.NamespaceUri))
            {
                if (!nsDict.ContainsKey(node.NamespaceUri))
                {
                    // Fallback to standard prefix, then node's prefix, then generate one
                    string prefix = OdfNamespaces.GetPrefix(node.NamespaceUri);
                    if (string.IsNullOrEmpty(prefix))
                    {
                        prefix = node.Prefix ?? $"ns{nsDict.Count + 1}";
                    }
                    nsDict[node.NamespaceUri] = prefix;
                }
            }

            // Collect namespaces of attributes
            foreach (var attr in node.Attributes.Keys)
            {
                if (!string.IsNullOrEmpty(attr.NamespaceUri))
                {
                    if (!nsDict.ContainsKey(attr.NamespaceUri))
                    {
                        string prefix = OdfNamespaces.GetPrefix(attr.NamespaceUri);
                        if (string.IsNullOrEmpty(prefix))
                        {
                            prefix = $"ns{nsDict.Count + 1}";
                        }
                        nsDict[attr.NamespaceUri] = prefix;
                    }
                }
            }

            // Recurse children
            foreach (var child in node.Children)
            {
                CollectNamespaces(child, nsDict);
            }
        }
    }
}
